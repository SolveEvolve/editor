using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PascalScene
{
    public interface IPascalAssetResolver
    {
        string CatalogVersion { get; }
        GameObject ResolveModel(string assetId);
    }

    public sealed class PascalSceneBuildSettings
    {
        public const string RootName = "PascalSceneRoot";
        public const float DefaultLevelHeight = 2.5f;
        public const float DefaultWallThickness = 0.1f;
        public const float DefaultSlabElevation = 0.05f;
        public const float DefaultSlabThickness = 0.05f;

        public Material WallMaterial { get; set; }
        public Material SlabMaterial { get; set; }
        public Material CeilingMaterial { get; set; }
        public Action<Mesh, string> PersistMesh { get; set; }
    }

    public static class PascalSceneBuilder
    {
        private delegate float NodeBuilder(
            GameObject gameObject,
            PascalSceneNode node,
            float containingLevelHeight,
            PascalSceneBuildContext context);

        private static readonly IReadOnlyDictionary<string, NodeBuilder> NodeBuilders =
            new Dictionary<string, NodeBuilder>(StringComparer.Ordinal)
            {
                ["site"] = BuildContainer,
                ["building"] = BuildContainer,
                ["level"] = BuildLevel,
                ["wall"] = BuildWallNode,
                ["slab"] = BuildSlabNode,
                ["ceiling"] = BuildCeilingNode,
                ["item"] = BuildItemNode
            };

        public static PascalSceneBuildReport Build(
            PascalSceneDocument document,
            Transform sceneParent,
            PascalSceneBuildSettings settings,
            IPascalAssetResolver assetResolver = null)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            settings ??= new PascalSceneBuildSettings();
            RemoveExistingRoot(sceneParent);

            var report = new PascalSceneBuildReport();
            if (document.IsLegacyDocument)
            {
                report.Warnings.Add(
                    "Imported legacy v0 Pascal JSON. Scene materials and catalog version were not exported.");
            }
            else if (assetResolver != null &&
                     !string.Equals(document.CatalogVersion, assetResolver.CatalogVersion, StringComparison.Ordinal))
            {
                report.CatalogVersionMismatch = true;
                report.Warnings.Add(
                    $"Scene requires catalog '{document.CatalogVersion}', but Unity has '{assetResolver.CatalogVersion}'.");
            }
            var root = new GameObject(PascalSceneBuildSettings.RootName);
            if (sceneParent != null)
            {
                root.transform.SetParent(sceneParent, false);
            }

            var context = new PascalSceneBuildContext(
                document,
                CalculateLevelElevations(document),
                settings,
                assetResolver,
                report);
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();
            foreach (var rootId in document.RootNodeIds)
            {
                BuildNode(
                    rootId,
                    root.transform,
                    context,
                    visited,
                    visiting,
                    PascalSceneBuildSettings.DefaultLevelHeight);
            }

            foreach (var nodeId in document.Nodes.Keys.OrderBy(id => id, StringComparer.Ordinal))
            {
                if (visited.Contains(nodeId))
                {
                    continue;
                }

                report.Warnings.Add($"Node '{nodeId}' is unreachable from rootNodeIds; attached to scene root.");
                BuildNode(
                    nodeId,
                    root.transform,
                    context,
                    visited,
                    visiting,
                    PascalSceneBuildSettings.DefaultLevelHeight);
            }

            return report;
        }

        public static IReadOnlyDictionary<string, float> CalculateLevelElevations(
            PascalSceneDocument document)
        {
            var levels = document.Nodes.Values
                .Where(node => node.Type == "level")
                .ToList();
            var result = new Dictionary<string, float>();
            if (levels.Count == 0)
            {
                return result;
            }

            foreach (var buildingLevels in levels.GroupBy(ResolveBuildingId, StringComparer.Ordinal))
            {
                var groups = buildingLevels
                    .GroupBy(node => node.Level ?? 0f)
                    .OrderBy(group => group.Key)
                    .ToList();
                var elevation = 0f;
                foreach (var group in groups.Where(group => group.Key >= 0f))
                {
                    foreach (var level in group)
                    {
                        result[level.Id] = elevation;
                    }

                    elevation += group.Max(level => level.Height ?? PascalSceneBuildSettings.DefaultLevelHeight);
                }

                elevation = 0f;
                foreach (var group in groups.Where(group => group.Key < 0f).OrderByDescending(group => group.Key))
                {
                    elevation -= group.Max(level => level.Height ?? PascalSceneBuildSettings.DefaultLevelHeight);
                    foreach (var level in group)
                    {
                        result[level.Id] = elevation;
                    }
                }
            }

            return result;
        }

        public static Vector3 ToUnityPosition(float[] source)
        {
            if (source == null || source.Length < 3)
            {
                return Vector3.zero;
            }

            return PascalSceneTransform.ToUnityPosition(source);
        }

        public static Vector3 ToUnityPlanPoint(float[] source, float elevation = 0f)
        {
            if (source == null || source.Length < 2)
            {
                return new Vector3(0f, elevation, 0f);
            }

            return new Vector3(source[0], elevation, source[1]);
        }

        private static void BuildNode(
            string nodeId,
            Transform parent,
            PascalSceneBuildContext context,
            HashSet<string> visited,
            HashSet<string> visiting,
            float containingLevelHeight)
        {
            if (visited.Contains(nodeId))
            {
                return;
            }

            if (!context.Document.Nodes.TryGetValue(nodeId, out var node))
            {
                context.Report.Warnings.Add($"Child reference '{nodeId}' does not exist.");
                return;
            }

            if (!visiting.Add(nodeId))
            {
                context.Report.Warnings.Add($"Cycle detected at node '{nodeId}'.");
                return;
            }

            var gameObject = new GameObject(GetDisplayName(node));
            gameObject.transform.SetParent(parent, false);
            gameObject.AddComponent<PascalSceneIdentity>().Configure(node.Id, node.Type);
            gameObject.SetActive(node.Visible);
            context.Report.ImportedNodeCount++;

            try
            {
                if (NodeBuilders.TryGetValue(node.Type, out var nodeBuilder))
                {
                    containingLevelHeight = nodeBuilder(gameObject, node, containingLevelHeight, context);
                }
                else
                {
                    context.Report.SkippedNodeCount++;
                    context.Report.UnsupportedNodes.Add($"{node.Type}:{node.Id}");
                }
            }
            catch (Exception exception)
            {
                context.Report.SkippedNodeCount++;
                context.Report.Warnings.Add($"{node.Type}:{node.Id} failed: {exception.Message}");
            }

            foreach (var childId in node.Children ?? Enumerable.Empty<string>())
            {
                BuildNode(
                    childId,
                    gameObject.transform,
                    context,
                    visited,
                    visiting,
                    containingLevelHeight);
            }

            visiting.Remove(nodeId);
            visited.Add(nodeId);
        }

        private static float BuildContainer(
            GameObject gameObject,
            PascalSceneNode node,
            float containingLevelHeight,
            PascalSceneBuildContext context)
        {
            ApplyTransform(gameObject.transform, node);
            return containingLevelHeight;
        }

        private static float BuildLevel(
            GameObject gameObject,
            PascalSceneNode node,
            float containingLevelHeight,
            PascalSceneBuildContext context)
        {
            var elevation = context.LevelElevations.TryGetValue(node.Id, out var value) ? value : 0f;
            gameObject.transform.localPosition = new Vector3(0f, elevation, 0f);
            return node.Height ?? PascalSceneBuildSettings.DefaultLevelHeight;
        }

        private static float BuildWallNode(
            GameObject gameObject,
            PascalSceneNode node,
            float containingLevelHeight,
            PascalSceneBuildContext context)
        {
            BuildWall(gameObject, node, containingLevelHeight, context, context.Settings, context.Report);
            return containingLevelHeight;
        }

        private static float BuildSlabNode(
            GameObject gameObject,
            PascalSceneNode node,
            float containingLevelHeight,
            PascalSceneBuildContext context)
        {
            BuildSlab(gameObject, node, context.Settings, context.Report);
            return containingLevelHeight;
        }

        private static float BuildCeilingNode(
            GameObject gameObject,
            PascalSceneNode node,
            float containingLevelHeight,
            PascalSceneBuildContext context)
        {
            BuildCeiling(gameObject, node, containingLevelHeight, context.Settings, context.Report);
            return containingLevelHeight;
        }

        private static float BuildItemNode(
            GameObject gameObject,
            PascalSceneNode node,
            float containingLevelHeight,
            PascalSceneBuildContext context)
        {
            BuildItem(gameObject, node, context.AssetResolver, context.Report);
            return containingLevelHeight;
        }

        private static void BuildWall(
            GameObject gameObject,
            PascalSceneNode node,
            float containingLevelHeight,
            PascalSceneBuildContext context,
            PascalSceneBuildSettings settings,
            PascalSceneBuildReport report)
        {
            var baseElevation = ResolveSupportElevation(node, context.Document);
            var height = node.Height ?? containingLevelHeight - baseElevation;
            if (height <= 0f)
            {
                report.SkippedNodeCount++;
                report.UnsupportedNodes.Add($"wall non-positive effective height:{node.Id}");
                return;
            }
            var mesh = Mathf.Abs(node.CurveOffset ?? 0f) > 0.00001f
                ? PascalSceneMeshFactory.CreateWall(
                    node.Start,
                    node.End,
                    height,
                    node.Thickness ?? PascalSceneBuildSettings.DefaultWallThickness,
                    node.CurveOffset ?? 0f,
                    baseElevation)
                : PascalSceneMeshFactory.CreateWallFromFootprint(
                    GetMiteredWallFootprint(node, context.Document), height, baseElevation);
            AddMesh(gameObject, mesh, settings.WallMaterial, settings, node.Id);
        }

        private static void BuildSlab(
            GameObject gameObject,
            PascalSceneNode node,
            PascalSceneBuildSettings settings,
            PascalSceneBuildReport report)
        {
            var elevation = node.Elevation ?? PascalSceneBuildSettings.DefaultSlabElevation;
            var thickness = node.Thickness ?? PascalSceneBuildSettings.DefaultSlabThickness;
            var mesh = PascalSceneMeshFactory.CreatePolygonPrism(
                ToVector2Polygon(node.GetPolygonPoints()),
                elevation - thickness,
                elevation,
                "Pascal Slab Mesh",
                ToVector2Holes(node.Holes));
            AddMesh(gameObject, mesh, settings.SlabMaterial, settings, node.Id);
        }

        private static void BuildCeiling(
            GameObject gameObject,
            PascalSceneNode node,
            float containingLevelHeight,
            PascalSceneBuildSettings settings,
            PascalSceneBuildReport report)
        {
            var height = node.Height ?? containingLevelHeight;
            var mesh = PascalSceneMeshFactory.CreateDoubleSidedSurface(
                ToVector2Polygon(node.GetPolygonPoints()),
                height,
                "Pascal Ceiling Mesh",
                ToVector2Holes(node.Holes));
            AddMesh(gameObject, mesh, settings.CeilingMaterial, settings, node.Id);
        }

        private static void BuildItem(
            GameObject gameObject,
            PascalSceneNode node,
            IPascalAssetResolver assetResolver,
            PascalSceneBuildReport report)
        {
            ApplyTransform(gameObject.transform, node);
            var assetId = node.Asset?.Id;
            var model = string.IsNullOrWhiteSpace(assetId)
                ? null
                : assetResolver?.ResolveModel(assetId);
            if (model == null)
            {
                report.MissingModelIds.Add(string.IsNullOrWhiteSpace(assetId) ? node.Id : assetId);
                return;
            }

            var instance = UnityEngine.Object.Instantiate(model, gameObject.transform, false);
            instance.name = $"{(node.Asset.Name ?? assetId)} Model";
            instance.transform.localPosition = ToUnityPosition(node.Asset.Offset);
            instance.transform.localRotation = ToUnityRotation(node.Asset.Rotation);
            instance.transform.localScale = ToUnityScale(node.Asset.Scale);
        }

        private static void AddMesh(
            GameObject gameObject,
            Mesh mesh,
            Material material,
            PascalSceneBuildSettings settings,
            string nodeId)
        {
            settings.PersistMesh?.Invoke(mesh, nodeId);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static void ApplyTransform(Transform transform, PascalSceneNode node)
        {
            transform.localPosition = ToUnityPosition(node.Position);
            transform.localRotation = ToUnityRotation(node.Rotation);
            transform.localScale = ToUnityScale(node.Scale);
        }

        private static Quaternion ToUnityRotation(float[] radians)
        {
            if (radians == null || radians.Length < 3)
            {
                return Quaternion.identity;
            }

            return PascalSceneTransform.ToUnityRotationXyzRadians(radians);
        }

        private static Vector3 ToUnityScale(float[] scale)
        {
            if (scale == null || scale.Length < 3)
            {
                return Vector3.one;
            }

            return PascalSceneTransform.ToUnityScale(scale);
        }

        private static List<Vector2> ToVector2Polygon(List<float[]> points)
        {
            var result = new List<Vector2>(points.Count);
            foreach (var point in points)
            {
                if (point == null || point.Length < 2)
                {
                    throw new ArgumentException("Polygon contains an invalid [x, z] point.");
                }

                result.Add(new Vector2(point[0], point[1]));
            }

            return result;
        }

        private static bool HasHoles(PascalSceneNode node)
        {
            return node.Holes != null && node.Holes.Any(hole => hole != null && hole.Count >= 3);
        }

        private static List<IReadOnlyList<Vector2>> ToVector2Holes(List<List<float[]>> holes)
        {
            var result = new List<IReadOnlyList<Vector2>>();
            foreach (var hole in holes ?? Enumerable.Empty<List<float[]>>())
            {
                if (hole == null || hole.Count < 3) continue;
                result.Add(ToVector2Polygon(hole));
            }

            return result;
        }

        private static string ResolveBuildingId(PascalSceneNode level)
        {
            return level.ParentId ?? string.Empty;
        }

        private static float ResolveSupportElevation(PascalSceneNode node, PascalSceneDocument document)
        {
            if (!string.IsNullOrWhiteSpace(node.SupportSlabId) &&
                document.Nodes.TryGetValue(node.SupportSlabId, out var slab) && slab.Type == "slab")
            {
                return slab.Elevation ?? PascalSceneBuildSettings.DefaultSlabElevation;
            }

            return 0f;
        }

        private static List<Vector2> GetMiteredWallFootprint(
            PascalSceneNode wall,
            PascalSceneDocument document)
        {
            var start = ToPlanPoint(wall.Start);
            var end = ToPlanPoint(wall.End);
            var direction = (end - start).normalized;
            var halfThickness = (wall.Thickness ?? PascalSceneBuildSettings.DefaultWallThickness) * 0.5f;
            var normal = new Vector2(-direction.y, direction.x);
            var startPair = GetMiterPair(wall, start, -direction, normal, halfThickness, document);
            var endPair = GetMiterPair(wall, end, direction, normal, halfThickness, document);
            return new List<Vector2> { startPair.right, endPair.right, endPair.left, startPair.left };
        }

        private static (Vector2 left, Vector2 right) GetMiterPair(
            PascalSceneNode wall,
            Vector2 joint,
            Vector2 outward,
            Vector2 normal,
            float halfThickness,
            PascalSceneDocument document)
        {
            var left = joint + normal * halfThickness;
            var right = joint - normal * halfThickness;
            var sibling = document.Nodes.Values.FirstOrDefault(candidate =>
                candidate.Id != wall.Id && candidate.Type == "wall" &&
                Mathf.Abs(candidate.CurveOffset ?? 0f) <= 0.00001f &&
                (Approximately(ToPlanPoint(candidate.Start), joint) || Approximately(ToPlanPoint(candidate.End), joint)));
            if (sibling == null) return (left, right);

            var siblingOther = Approximately(ToPlanPoint(sibling.Start), joint)
                ? ToPlanPoint(sibling.End)
                : ToPlanPoint(sibling.Start);
            var siblingDirection = (siblingOther - joint).normalized;
            if (Mathf.Abs(Cross2(outward, siblingDirection)) < 0.02f) return (left, right);
            var siblingNormal = new Vector2(-siblingDirection.y, siblingDirection.x);
            var siblingHalf = (sibling.Thickness ?? PascalSceneBuildSettings.DefaultWallThickness) * 0.5f;
            var siblingLeft = joint + siblingNormal * siblingHalf;
            var siblingRight = joint - siblingNormal * siblingHalf;
            return (
                IntersectLines(left, outward, Vector2.Dot(normal, siblingNormal) >= 0f ? siblingLeft : siblingRight, siblingDirection, left),
                IntersectLines(right, outward, Vector2.Dot(-normal, siblingNormal) >= 0f ? siblingLeft : siblingRight, siblingDirection, right));
        }

        private static Vector2 IntersectLines(Vector2 pointA, Vector2 directionA, Vector2 pointB, Vector2 directionB, Vector2 fallback)
        {
            var cross = Cross2(directionA, directionB);
            if (Mathf.Abs(cross) < 0.00001f) return fallback;
            var t = Cross2(pointB - pointA, directionB) / cross;
            return Mathf.Abs(t) > 0.5f ? fallback : pointA + directionA * t;
        }

        private static Vector2 ToPlanPoint(float[] point) => new Vector2(point[0], point[1]);

        private static bool Approximately(Vector2 a, Vector2 b) => (a - b).sqrMagnitude <= 0.000001f;

        private static float Cross2(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static string GetDisplayName(PascalSceneNode node)
        {
            return string.IsNullOrWhiteSpace(node.Name)
                ? $"{node.Type} ({node.Id})"
                : node.Name;
        }

        private static void RemoveExistingRoot(Transform sceneParent)
        {
            Transform existing = null;
            if (sceneParent != null)
            {
                existing = sceneParent.Find(PascalSceneBuildSettings.RootName);
            }
            else
            {
                var existingObject = GameObject.Find(PascalSceneBuildSettings.RootName);
                existing = existingObject != null ? existingObject.transform : null;
            }

            if (existing == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(existing.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
        }

        private sealed class PascalSceneBuildContext
        {
            public PascalSceneBuildContext(
                PascalSceneDocument document,
                IReadOnlyDictionary<string, float> levelElevations,
                PascalSceneBuildSettings settings,
                IPascalAssetResolver assetResolver,
                PascalSceneBuildReport report)
            {
                Document = document;
                LevelElevations = levelElevations;
                Settings = settings;
                AssetResolver = assetResolver;
                Report = report;
            }

            public PascalSceneDocument Document { get; }
            public IReadOnlyDictionary<string, float> LevelElevations { get; }
            public PascalSceneBuildSettings Settings { get; }
            public IPascalAssetResolver AssetResolver { get; }
            public PascalSceneBuildReport Report { get; }
        }
    }
}

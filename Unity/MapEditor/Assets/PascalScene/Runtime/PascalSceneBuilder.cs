using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PascalScene
{
    public interface IPascalAssetResolver
    {
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
            var root = new GameObject(PascalSceneBuildSettings.RootName);
            if (sceneParent != null)
            {
                root.transform.SetParent(sceneParent, false);
            }

            var levelElevations = CalculateLevelElevations(document);
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();
            foreach (var rootId in document.RootNodeIds)
            {
                BuildNode(
                    rootId,
                    root.transform,
                    document,
                    levelElevations,
                    settings,
                    assetResolver,
                    report,
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
                    document,
                    levelElevations,
                    settings,
                    assetResolver,
                    report,
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
                .OrderBy(node => node.Level ?? 0f)
                .ThenBy(node => node.Id, StringComparer.Ordinal)
                .ToList();
            var result = new Dictionary<string, float>();
            if (levels.Count == 0)
            {
                return result;
            }

            var groups = levels
                .GroupBy(node => node.Level ?? 0f)
                .OrderBy(group => group.Key)
                .ToList();
            var nonNegative = groups.Where(group => group.Key >= 0f).ToList();
            var elevation = 0f;
            foreach (var group in nonNegative)
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

            return result;
        }

        public static Vector3 ToUnityPosition(float[] source)
        {
            if (source == null || source.Length < 3)
            {
                return Vector3.zero;
            }

            return new Vector3(source[0], source[1], source[2]);
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
            PascalSceneDocument document,
            IReadOnlyDictionary<string, float> levelElevations,
            PascalSceneBuildSettings settings,
            IPascalAssetResolver assetResolver,
            PascalSceneBuildReport report,
            HashSet<string> visited,
            HashSet<string> visiting,
            float containingLevelHeight)
        {
            if (visited.Contains(nodeId))
            {
                return;
            }

            if (!document.Nodes.TryGetValue(nodeId, out var node))
            {
                report.Warnings.Add($"Child reference '{nodeId}' does not exist.");
                return;
            }

            if (!visiting.Add(nodeId))
            {
                report.Warnings.Add($"Cycle detected at node '{nodeId}'.");
                return;
            }

            var gameObject = new GameObject(GetDisplayName(node));
            gameObject.transform.SetParent(parent, false);
            gameObject.AddComponent<PascalSceneIdentity>().Configure(node.Id, node.Type);
            gameObject.SetActive(node.Visible);
            report.ImportedNodeCount++;

            try
            {
                switch (node.Type)
                {
                    case "site":
                    case "building":
                        ApplyTransform(gameObject.transform, node);
                        break;
                    case "level":
                        var levelElevation = levelElevations.TryGetValue(node.Id, out var value) ? value : 0f;
                        gameObject.transform.localPosition = new Vector3(0f, levelElevation, 0f);
                        containingLevelHeight =
                            node.Height ?? PascalSceneBuildSettings.DefaultLevelHeight;
                        break;
                    case "wall":
                        BuildWall(gameObject, node, containingLevelHeight, settings, report);
                        break;
                    case "slab":
                        BuildSlab(gameObject, node, settings, report);
                        break;
                    case "ceiling":
                        BuildCeiling(gameObject, node, containingLevelHeight, settings, report);
                        break;
                    case "item":
                        BuildItem(gameObject, node, assetResolver, report);
                        break;
                    default:
                        report.SkippedNodeCount++;
                        report.UnsupportedNodes.Add($"{node.Type}:{node.Id}");
                        break;
                }
            }
            catch (Exception exception)
            {
                report.SkippedNodeCount++;
                report.Warnings.Add($"{node.Type}:{node.Id} failed: {exception.Message}");
            }

            foreach (var childId in node.Children ?? Enumerable.Empty<string>())
            {
                BuildNode(
                    childId,
                    gameObject.transform,
                    document,
                    levelElevations,
                    settings,
                    assetResolver,
                    report,
                    visited,
                    visiting,
                    containingLevelHeight);
            }

            visiting.Remove(nodeId);
            visited.Add(nodeId);
        }

        private static void BuildWall(
            GameObject gameObject,
            PascalSceneNode node,
            float containingLevelHeight,
            PascalSceneBuildSettings settings,
            PascalSceneBuildReport report)
        {
            if (Mathf.Abs(node.CurveOffset ?? 0f) > 0.00001f)
            {
                report.SkippedNodeCount++;
                report.UnsupportedNodes.Add($"curved wall:{node.Id}");
                return;
            }

            var height = node.Height ?? containingLevelHeight;
            var mesh = PascalSceneMeshFactory.CreateWall(
                node.Start,
                node.End,
                height,
                node.Thickness ?? PascalSceneBuildSettings.DefaultWallThickness);
            AddMesh(gameObject, mesh, settings.WallMaterial, settings, node.Id);
        }

        private static void BuildSlab(
            GameObject gameObject,
            PascalSceneNode node,
            PascalSceneBuildSettings settings,
            PascalSceneBuildReport report)
        {
            if (HasHoles(node))
            {
                report.SkippedNodeCount++;
                report.UnsupportedNodes.Add($"slab holes:{node.Id}");
                return;
            }

            var elevation = node.Elevation ?? PascalSceneBuildSettings.DefaultSlabElevation;
            var thickness = node.Thickness ?? PascalSceneBuildSettings.DefaultSlabThickness;
            var mesh = PascalSceneMeshFactory.CreatePolygonPrism(
                ToVector2Polygon(node.GetPolygonPoints()),
                elevation - thickness,
                elevation,
                "Pascal Slab Mesh");
            AddMesh(gameObject, mesh, settings.SlabMaterial, settings, node.Id);
        }

        private static void BuildCeiling(
            GameObject gameObject,
            PascalSceneNode node,
            float containingLevelHeight,
            PascalSceneBuildSettings settings,
            PascalSceneBuildReport report)
        {
            if (HasHoles(node))
            {
                report.SkippedNodeCount++;
                report.UnsupportedNodes.Add($"ceiling holes:{node.Id}");
                return;
            }

            var height = node.Height ?? containingLevelHeight;
            var mesh = PascalSceneMeshFactory.CreateDoubleSidedSurface(
                ToVector2Polygon(node.GetPolygonPoints()),
                height,
                "Pascal Ceiling Mesh");
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

            return Quaternion.Euler(
                radians[0] * Mathf.Rad2Deg,
                radians[1] * Mathf.Rad2Deg,
                radians[2] * Mathf.Rad2Deg);
        }

        private static Vector3 ToUnityScale(float[] scale)
        {
            if (scale == null || scale.Length < 3)
            {
                return Vector3.one;
            }

            return new Vector3(scale[0], scale[1], scale[2]);
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
    }
}

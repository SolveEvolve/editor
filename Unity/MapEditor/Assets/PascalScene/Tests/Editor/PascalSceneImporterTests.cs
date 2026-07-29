using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace PascalScene.Tests
{
    public sealed class PascalSceneImporterTests
    {
        private GameObject parent;

        [SetUp]
        public void SetUp()
        {
            parent = new GameObject("Pascal Test Parent");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parent);
        }

        [Test]
        public void ParsesGraphAndUsesChildrenAsHierarchySource()
        {
            const string json =
                "{\"nodes\":{\"site_a\":{\"id\":\"site_a\",\"type\":\"site\",\"visible\":true,\"children\":[\"level_a\"]}," +
                "\"level_a\":{\"id\":\"level_a\",\"type\":\"level\",\"parentId\":null,\"level\":0,\"height\":3,\"children\":[\"wall_a\"]}," +
                "\"wall_a\":{\"id\":\"wall_a\",\"type\":\"wall\",\"parentId\":null,\"start\":[0,0],\"end\":[2,0],\"children\":[]}}," +
                "\"rootNodeIds\":[\"site_a\"]}";
            var document = PascalSceneDocument.Parse(json);

            PascalSceneBuilder.Build(document, parent.transform, new PascalSceneBuildSettings());

            var root = parent.transform.Find(PascalSceneBuildSettings.RootName);
            var site = root.Find("site (site_a)");
            var level = site.Find("level (level_a)");
            Assert.That(level.Find("wall (wall_a)"), Is.Not.Null);
        }

        [Test]
        public void ParsesVersionedDocumentAndReportsCatalogMismatch()
        {
            const string json =
                "{\"schemaVersion\":1,\"catalogVersion\":\"other-catalog\",\"materialLibraryVersion\":\"materials-2026-07-29\"," +
                "\"collections\":{},\"materials\":{},\"nodes\":{},\"rootNodeIds\":[]}";
            var document = PascalSceneDocument.Parse(json);
            var report = PascalSceneBuilder.Build(
                document,
                parent.transform,
                new PascalSceneBuildSettings(),
                new DummyModelResolver());

            Assert.That(document.IsLegacyDocument, Is.False);
            Assert.That(report.CatalogVersionMismatch, Is.True);
        }

        [Test]
        public void RejectsMissingChildReferencesBeforeBuilding()
        {
            const string json =
                "{\"nodes\":{\"site_a\":{\"id\":\"site_a\",\"type\":\"site\",\"children\":[\"missing\"]}}," +
                "\"rootNodeIds\":[\"site_a\"]}";

            Assert.That(
                () => PascalSceneDocument.Parse(json),
                Throws.Exception);
        }

        [Test]
        public void CalculatesLevelElevationsAboveAndBelowGround()
        {
            var document = PascalSceneDocument.Parse(
                "{\"nodes\":{\"below\":{\"id\":\"below\",\"type\":\"level\",\"level\":-1,\"height\":2}," +
                "\"ground\":{\"id\":\"ground\",\"type\":\"level\",\"level\":0,\"height\":3}," +
                "\"upper\":{\"id\":\"upper\",\"type\":\"level\",\"level\":1,\"height\":2.5}}," +
                "\"rootNodeIds\":[\"ground\",\"upper\",\"below\"]}");

            var elevations = PascalSceneBuilder.CalculateLevelElevations(document);

            Assert.That(elevations["below"], Is.EqualTo(-2f).Within(0.0001f));
            Assert.That(elevations["ground"], Is.EqualTo(0f).Within(0.0001f));
            Assert.That(elevations["upper"], Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void CalculatesIndependentVerticalStacksPerBuilding()
        {
            var document = PascalSceneDocument.Parse(
                "{\"nodes\":{\"a0\":{\"id\":\"a0\",\"type\":\"level\",\"parentId\":\"a\",\"level\":0,\"height\":3}," +
                "\"a1\":{\"id\":\"a1\",\"type\":\"level\",\"parentId\":\"a\",\"level\":1,\"height\":2}," +
                "\"b0\":{\"id\":\"b0\",\"type\":\"level\",\"parentId\":\"b\",\"level\":0,\"height\":4}}," +
                "\"rootNodeIds\":[\"a0\",\"a1\",\"b0\"]}");

            var elevations = PascalSceneBuilder.CalculateLevelElevations(document);

            Assert.That(elevations["a1"], Is.EqualTo(3f));
            Assert.That(elevations["b0"], Is.EqualTo(0f));
        }

        [Test]
        public void CoordinateConversionPreservesMetersAndAxes()
        {
            Assert.That(
                PascalSceneBuilder.ToUnityPosition(new[] { 1.25f, 2f, -3.5f }),
                Is.EqualTo(new Vector3(1.25f, 2f, -3.5f)));
            Assert.That(
                PascalSceneBuilder.ToUnityPlanPoint(new[] { 1.25f, -3.5f }, 2f),
                Is.EqualTo(new Vector3(1.25f, 2f, -3.5f)));
        }

        [Test]
        public void TransformAdapterMatchesPascalXyzEulerSemantics()
        {
            var rotation = PascalSceneTransform.ToUnityRotationXyzRadians(
                new[] { Mathf.PI * 0.5f, Mathf.PI * 0.5f, 0f });

            Assert.That(rotation.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(rotation.y, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(rotation.z, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(rotation.w, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void WallMeshUsesRequestedLengthHeightAndThickness()
        {
            var mesh = PascalSceneMeshFactory.CreateWall(
                new[] { 0f, 0f },
                new[] { 4f, 0f },
                2.5f,
                0.1f);

            Assert.That(mesh.bounds.size.x, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(mesh.bounds.size.y, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(mesh.bounds.size.z, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(mesh.uv.Any(uv => Mathf.Abs(uv.x) > 1f), Is.True);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void CurvedWallMeshUsesArcAndBaseElevation()
        {
            var mesh = PascalSceneMeshFactory.CreateWall(
                new[] { 0f, 0f }, new[] { 4f, 0f }, 2f, 0.1f, 1f, 0.5f);

            Assert.That(mesh.bounds.min.y, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(mesh.bounds.size.y, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(mesh.vertexCount, Is.GreaterThan(16));
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void ConnectedStraightWallsProduceMiteredFootprints()
        {
            var document = PascalSceneDocument.Parse(
                "{\"nodes\":{\"level\":{\"id\":\"level\",\"type\":\"level\",\"level\":0,\"children\":[\"a\",\"b\"]}," +
                "\"a\":{\"id\":\"a\",\"type\":\"wall\",\"start\":[0,0],\"end\":[2,0],\"children\":[]}," +
                "\"b\":{\"id\":\"b\",\"type\":\"wall\",\"start\":[2,0],\"end\":[2,2],\"children\":[]}},\"rootNodeIds\":[\"level\"]}");

            PascalSceneBuilder.Build(document, parent.transform, new PascalSceneBuildSettings());

            var walls = parent.GetComponentsInChildren<MeshFilter>();
            Assert.That(walls.Count(filter => filter.sharedMesh != null), Is.EqualTo(2));
        }

        [Test]
        public void SlabAndCeilingMeshesMatchPolygonBounds()
        {
            var polygon = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(4f, 0f),
                new Vector2(4f, 3f),
                new Vector2(0f, 3f)
            };
            var slab = PascalSceneMeshFactory.CreatePolygonPrism(polygon, 0f, 0.05f);
            var ceiling = PascalSceneMeshFactory.CreateDoubleSidedSurface(polygon, 2.5f);

            Assert.That(slab.bounds.size, Is.EqualTo(new Vector3(4f, 0.05f, 3f)));
            Assert.That(ceiling.bounds.size, Is.EqualTo(new Vector3(4f, 0f, 3f)));
            Object.DestroyImmediate(slab);
            Object.DestroyImmediate(ceiling);
        }

        [Test]
        public void SlabAndCeilingMeshesPreservePolygonHoles()
        {
            var outer = new[]
            {
                new Vector2(0f, 0f), new Vector2(4f, 0f), new Vector2(4f, 4f), new Vector2(0f, 4f)
            };
            var holes = new IReadOnlyList<Vector2>[]
            {
                new[] { new Vector2(1f, 1f), new Vector2(1f, 3f), new Vector2(3f, 3f), new Vector2(3f, 1f) }
            };
            var slab = PascalSceneMeshFactory.CreatePolygonPrism(outer, 0f, 0.1f, holes: holes);
            var ceiling = PascalSceneMeshFactory.CreateDoubleSidedSurface(outer, 2.5f, holes: holes);

            Assert.That(slab.triangles.Length, Is.GreaterThan(0));
            Assert.That(ceiling.triangles.Length, Is.GreaterThan(0));
            Assert.That(slab.bounds.size, Is.EqualTo(new Vector3(4f, 0.1f, 4f)));
            Object.DestroyImmediate(slab);
            Object.DestroyImmediate(ceiling);
        }

        [Test]
        public void ReimportReplacesOnlyPascalRoot()
        {
            var external = new GameObject("External");
            external.transform.SetParent(parent.transform);
            var document = PascalSceneDocument.Parse(
                "{\"nodes\":{\"site_a\":{\"id\":\"site_a\",\"type\":\"site\",\"children\":[]}},\"rootNodeIds\":[\"site_a\"]}");

            PascalSceneBuilder.Build(document, parent.transform, new PascalSceneBuildSettings());
            PascalSceneBuilder.Build(document, parent.transform, new PascalSceneBuildSettings());

            Assert.That(parent.transform.Cast<Transform>()
                .Count(child => child.name == PascalSceneBuildSettings.RootName), Is.EqualTo(1));
            Assert.That(parent.transform.Find("External"), Is.Not.Null);
        }

        [Test]
        public void SampleSceneBuildsExpectedSupportedNodes()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/PascalScene/Scenes/layout_2026-07-29.json");
            Assert.That(asset, Is.Not.Null);
            var document = PascalSceneDocument.Parse(asset.text);

            var report = PascalSceneBuilder.Build(
                document,
                parent.transform,
                new PascalSceneBuildSettings(),
                new DummyModelResolver());
            var identities = parent.GetComponentsInChildren<PascalSceneIdentity>(true);

            Assert.That(identities.Count(identity => identity.NodeType == "wall"), Is.EqualTo(5));
            Assert.That(identities.Count(identity => identity.NodeType == "slab"), Is.EqualTo(1));
            Assert.That(identities.Count(identity => identity.NodeType == "ceiling"), Is.EqualTo(1));
            Assert.That(identities.Count(identity => identity.NodeType == "item"), Is.EqualTo(3));
            Assert.That(report.MissingModelIds, Is.Empty);
            Object.DestroyImmediate(GameObject.Find("Dummy Model"));
        }

        private sealed class DummyModelResolver : IPascalAssetResolver
        {
            private readonly GameObject model = new("Dummy Model");

            public string CatalogVersion => "catalog-2026-07-29";

            public GameObject ResolveModel(string assetId)
            {
                return model;
            }
        }
    }
}

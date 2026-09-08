using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PascalScene.Editor
{
    public static class PascalSceneEditorImporter
    {
        private const string CatalogManifestPath = "Assets/PascalScene/Library/pascal-catalog.json";
        private const string MaterialsFolder = "Assets/PascalScene/Materials";
        private const string GeneratedFolder = "Assets/PascalScene/Generated";
        private const string UrpLitShader = "Universal Render Pipeline/Lit";

        [MenuItem("Tools/Pascal Scene/Import Selected JSON", priority = 100)]
        public static void ImportSelectedJson()
        {
            if (Selection.activeObject is not TextAsset sceneAsset)
            {
                Debug.LogError("Select a Pascal .json TextAsset before importing.");
                return;
            }

            Import(sceneAsset);
        }

        [MenuItem("Tools/Pascal Scene/Import Selected JSON", true)]
        private static bool ValidateImportSelectedJson()
        {
            return Selection.activeObject is TextAsset &&
                   AssetDatabase.GetAssetPath(Selection.activeObject)
                       .EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        }

        public static PascalSceneBuildReport Import(TextAsset sceneAsset)
        {
            if (sceneAsset == null)
            {
                throw new ArgumentNullException(nameof(sceneAsset));
            }

            EnsureFolder(GeneratedFolder);
            var settings = new PascalSceneBuildSettings
            {
                WallMaterial = LoadOrCreateMaterial("Pascal Wall", new Color(0.91f, 0.90f, 0.88f)),
                SlabMaterial = LoadOrCreateMaterial("Pascal Floor", new Color(0.52f, 0.46f, 0.39f)),
                CeilingMaterial = LoadOrCreateMaterial("Pascal Ceiling", new Color(0.96f, 0.96f, 0.94f)),
                GuideMaterial = LoadOrCreateMaterial("Pascal Guide", new Color(0.61f, 1f, 0.2f)),
                TargetMaterial = LoadOrCreateMaterial("Pascal Target", new Color(0.13f, 0.83f, 0.93f)),
                SpawnMaterial = LoadOrCreateMaterial("Pascal Spawn", new Color(0.51f, 0.55f, 0.95f)),
                PersistMesh = PersistMesh
            };

            var document = PascalSceneDocument.Parse(sceneAsset.text);
            var report = PascalSceneBuilder.Build(
                document,
                null,
                settings,
                new EditorModelResolver());
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = GameObject.Find(PascalSceneBuildSettings.RootName);
            Debug.Log($"Pascal scene import complete. {report}");
            return report;
        }

        private static Material LoadOrCreateMaterial(string name, Color color)
        {
            EnsureFolder(MaterialsFolder);
            var path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            var shader = Shader.Find(UrpLitShader);
            if (shader == null)
            {
                throw new InvalidOperationException($"Required shader '{UrpLitShader}' was not found.");
            }

            material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else
            {
                material.color = color;
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void PersistMesh(Mesh mesh, string nodeId)
        {
            var safeId = string.Join("_", nodeId.Split(Path.GetInvalidFileNameChars()));
            var path = $"{GeneratedFolder}/{safeId}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(mesh, path);
        }

        private static void EnsureFolder(string assetPath)
        {
            var parts = assetPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private sealed class EditorModelResolver : IPascalAssetResolver
        {
            private readonly PascalCatalogManifest manifest;

            public EditorModelResolver()
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(CatalogManifestPath);
                if (asset == null)
                {
                    throw new InvalidOperationException(
                        $"Pascal catalog manifest is missing at '{CatalogManifestPath}'.");
                }

                manifest = PascalCatalogManifest.Parse(asset.text);
            }

            public string CatalogVersion => manifest.CatalogVersion;

            public GameObject ResolveModel(string assetId)
            {
                var entry = manifest.Find(assetId);
                return entry == null
                    ? null
                    : AssetDatabase.LoadAssetAtPath<GameObject>(entry.LocalPath);
            }
        }
    }
}

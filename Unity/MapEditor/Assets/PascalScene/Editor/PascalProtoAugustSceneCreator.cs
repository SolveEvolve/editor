using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PascalScene.Editor
{
    public static class PascalProtoAugustSceneCreator
    {
        private const string SourcePath =
            "Assets/PascalScene/Scenes/proto-august-default.json";
        private const string ScenePath = "Assets/Scenes/ProtoAugustScene.unity";

        [MenuItem("Tools/Pascal Scene/Create Proto August Scene", priority = 110)]
        public static void Create()
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(SourcePath);
            if (source == null)
            {
                throw new InvalidOperationException($"Proto scene JSON is missing at '{SourcePath}'.");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            PascalSceneEditorImporter.Import(source);
            EditorSceneManager.SaveScene(scene, ScenePath, false);
            Debug.Log($"Created static Pascal Proto August scene at '{ScenePath}'.");
        }
    }
}

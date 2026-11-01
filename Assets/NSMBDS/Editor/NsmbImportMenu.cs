using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace NsmbDs.Editor {
    public static class NsmbImportMenu {
        private const string DebugScenePath = "Assets/NSMBDS/Scenes/NSMBDS_DebugSelect.unity";
        private const string CheckOutput = "RomAnalysis/unity_check";
        private const int CheckWidth = 1024;
        private const int CheckHeight = 256;

        [MenuItem("NSMB DS/Import (keep existing scenes)")]
        public static void Import() {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                NsmbImporter.Run(false);
            }
        }

        [MenuItem("NSMB DS/Reimport All (overwrites scene edits)")]
        public static void ReimportAll() {
            if (EditorUtility.DisplayDialog("Reimport all", "This replaces every area scene, tile and palette, and discards any edits in them.", "Overwrite", "Cancel")) {
                NsmbImporter.Run(true);
            }
        }

        [MenuItem("NSMB DS/Create Debug Select Scene")]
        public static void CreateDebugScene() {
            Directory.CreateDirectory(Path.GetDirectoryName(DebugScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.38f, 0.56f, 0.97f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            new GameObject("Debug Select").AddComponent<NsmbDebugSelect>();
            EditorSceneManager.SaveScene(scene, DebugScenePath);
        }

        /// <summary>Batch entry point: imports everything and renders a few areas to check the result.</summary>
        public static void ImportBatch() {
            NsmbImporter.Run(true);
            CreateDebugScene();
            RenderChecks();
        }

        /// <summary>Renders the listed areas from their prefabs so they can be compared with the extractor's renders.</summary>
        public static void RenderChecks() {
            Directory.CreateDirectory(CheckOutput);
            foreach (string areaName in new[] { "A01_1", "A01_3", "C05_1", "C01_1", "H12_1" }) {
                RenderArea(areaName);
            }
        }

        private static void RenderArea(string areaName) {
            string path = NsmbAreaIndex.Load().entries.Find(e => e.name == areaName).scenePath;
            EditorSceneManager.OpenScene(path);
            Tilemap tilemap = Object.FindAnyObjectByType<NsmbArea>().tilemap;
            tilemap.CompressBounds();
            Bounds bounds = tilemap.localBounds;

            GameObject cameraObject = new("Check Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.38f, 0.56f, 0.97f);
            camera.aspect = (float) CheckWidth / CheckHeight;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect);
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);

            RenderTexture target = new(CheckWidth, CheckHeight, 24);
            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            Texture2D image = new(CheckWidth, CheckHeight, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, CheckWidth, CheckHeight), 0, 0);
            RenderTexture.active = null;
            File.WriteAllBytes($"{CheckOutput}/{areaName}.png", image.EncodeToPNG());
            Debug.Log($"[NSMBDS] Check render {areaName}: {tilemap.GetUsedTilesCount()} tile kinds, bounds {bounds.size}");

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
    }
}

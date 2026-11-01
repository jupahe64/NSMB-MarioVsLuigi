using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NsmbDs.Editor {
    /// <summary>Batch check of the debug scene in Play mode: every area prefab loads, and rendering works.</summary>
    [InitializeOnLoad]
    public static class NsmbPlayCheck {
        private const string ActiveKey = "NsmbPlayCheckActive";
        private const string ScenePath = "Assets/NSMBDS/Scenes/NSMBDS_DebugSelect.unity";
        private const string Output = "RomAnalysis/unity_check";
        private const int Width = 512;
        private const int Height = 384;

        private static int waitFrames;
        private static int step;

        static NsmbPlayCheck() {
            if (SessionState.GetBool(ActiveKey, false)) {
                EditorApplication.update += Tick;
            }
        }

        public static void Run() {
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick() {
            if (!EditorApplication.isPlaying || ++waitFrames < 30) {
                return;
            }
            waitFrames = 0;

            NsmbDebugSelect select = Object.FindAnyObjectByType<NsmbDebugSelect>();
            if (step > 0 && select.Current == null) {
                return;
            }

            switch (step++) {
            case 0:
                Debug.Log($"[NSMBDS] Play check: scene running, select present: {select != null}");
                select.LoadArea(select.FindEntry("C05_1").Value);
                break;
            case 1:
                Debug.Log($"[NSMBDS] Play check: loaded {select.Current?.areaName}, tile kinds {select.Current?.tilemap.GetUsedTilesCount()}");
                Capture("play_C05_1");
                select.Current.SetBehaviorOverlay(true);
                break;
            case 2:
                Capture("play_C05_1_overlay");
                CheckAllAreas();
                select.LoadArea(select.FindEntry("A01_1").Value);
                break;
            case 3:
                Debug.Log($"[NSMBDS] Play check: switched to {select.Current?.areaName}, area objects {Object.FindObjectsByType<NsmbArea>(FindObjectsSortMode.None).Length}");
                Capture("play_A01_1");
                select.Current.SetBehaviorOverlay(true);
                break;
            case 4:
                Capture("play_A01_1_overlay");
                SessionState.SetBool(ActiveKey, false);
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
                break;
            }
        }

        private static void CheckAllAreas() {
            NsmbAreaIndex index = NsmbAreaIndex.Load();
            int missing = 0;
            foreach (NsmbAreaIndex.Entry entry in index.entries) {
                if (!File.Exists(entry.scenePath)) {
                    missing++;
                }
            }
            Debug.Log($"[NSMBDS] Play check: {index.entries.Count} areas, {missing} scenes missing");
        }

        private static void Capture(string name) {
            Camera camera = Camera.main;
            RenderTexture target = new(Width, Height, 24);
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;

            RenderTexture.active = target;
            Texture2D image = new(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            RenderTexture.active = null;
            Directory.CreateDirectory(Output);
            File.WriteAllBytes($"{Output}/{name}.png", image.EncodeToPNG());
            Object.Destroy(target);
            Object.Destroy(image);
        }
    }
}

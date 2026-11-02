using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace NsmbDs {
    /// <summary>In-game list of worlds and areas with a free camera, used to inspect imported levels.</summary>
    public sealed class NsmbDebugSelect : MonoBehaviour {
        private const float MoveSpeed = 12f;
        private const float DefaultOrthoSize = 1.5f;
        private const float MinOrthoSize = 0.75f;
        private const float MaxOrthoSize = 30f;
        private const float ZoomStep = 1.2f;
        private const float PanelWidth = 220f;
        private const string MarioPrefab = "NsmbMario";

        private readonly List<string> worlds = new();
        private NsmbAreaIndex index;
        private Camera view;
        private NsmbArea current;
        private Scene loadedScene;
        private string selectedWorld;
        private Vector2 scroll;
        private bool showPanel = true;
        private NsmbPlayer player;
        private NsmbAreaIndex.Entry currentEntry;

        private void Start() {
            view = Camera.main;
            // The DS backdrop colour is black in levels; it shows through empty background tiles.
            view.backgroundColor = Color.black;
            index = NsmbAreaIndex.Load();
            if (index == null) {
                Debug.LogWarning("[NSMBDS] No imported areas. Run NSMB DS > Import first.");
                return;
            }

            foreach (NsmbAreaIndex.Entry entry in index.entries) {
                if (!worlds.Contains(entry.world)) {
                    worlds.Add(entry.world);
                }
            }
            selectedWorld = worlds.Count > 0 ? worlds[0] : null;
        }

        private void Update() {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) {
                return;
            }

            if (keyboard.tabKey.wasPressedThisFrame) {
                showPanel = !showPanel;
            }
            if (keyboard.oKey.wasPressedThisFrame && current != null) {
                current.SetBehaviorOverlay(!current.OverlayVisible);
            }
            if (player != null && player.enabled) {
                return;
            }

            Vector3 move = Vector3.zero;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) {
                move.x -= 1f;
            }
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) {
                move.x += 1f;
            }
            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) {
                move.y -= 1f;
            }
            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) {
                move.y += 1f;
            }
            float boost = keyboard.leftShiftKey.isPressed ? 3f : 1f;
            view.transform.position += boost * MoveSpeed * Time.unscaledDeltaTime * (view.orthographicSize / DefaultOrthoSize) * move;

            float wheel = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if (wheel != 0f && !showPanel) {
                float size = wheel > 0f ? view.orthographicSize / ZoomStep : view.orthographicSize * ZoomStep;
                view.orthographicSize = Mathf.Clamp(size, MinOrthoSize, MaxOrthoSize);
            }
        }

        private void OnGUI() {
            if (index == null) {
                GUILayout.Label("No imported areas. Run NSMB DS > Import first.");
                return;
            }
            if (!showPanel) {
                return;
            }

            GUILayout.BeginArea(new Rect(8f, 8f, PanelWidth, Screen.height - 16f), GUI.skin.box);
            GUILayout.Label(current != null ? current.areaName : "Pick an area");
            GUILayout.Label("Tab panel, O behaviour overlay");

            GUILayout.BeginHorizontal();
            foreach (string world in worlds) {
                if (GUILayout.Toggle(world == selectedWorld, world, GUI.skin.button)) {
                    selectedWorld = world;
                }
            }
            GUILayout.EndHorizontal();

            scroll = GUILayout.BeginScrollView(scroll);
            foreach (NsmbAreaIndex.Entry entry in index.entries) {
                if (entry.world == selectedWorld && GUILayout.Button($"{entry.name}  ({entry.level}-{entry.area})")) {
                    LoadArea(entry);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        public NsmbArea Current => current;

        public NsmbPlayer Player => player;

        public void LoadArea(NsmbAreaIndex.Entry entry) {
            StartCoroutine(SwitchArea(entry, -1));
        }

        // Pipes and doors name an area of the same course.
        private void Travel(int destArea, int destEntrance) {
            // Destination area 0 means the same area.
            if (destArea == 0) {
                player.EnterArea(current, destEntrance);
                return;
            }

            foreach (NsmbAreaIndex.Entry entry in index.entries) {
                if (entry.world == currentEntry.world && entry.level == currentEntry.level && entry.area == destArea) {
                    StartCoroutine(SwitchArea(entry, destEntrance));
                    return;
                }
            }
            Debug.LogWarning($"[NSMBDS] {currentEntry.name} has no area {destArea}.");
        }

        public NsmbAreaIndex.Entry? FindEntry(string areaName) {
            foreach (NsmbAreaIndex.Entry entry in index.entries) {
                if (entry.name == areaName) {
                    return entry;
                }
            }
            return null;
        }

        private IEnumerator SwitchArea(NsmbAreaIndex.Entry entry, int entrance) {
            currentEntry = entry;
            if (player != null) {
                player.LeaveArea();
            }
            if (loadedScene.IsValid()) {
                current = null;
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }

            int sceneCount = SceneManager.sceneCount;
#if UNITY_EDITOR
            // Area scenes are not in the build list, so the Editor loads them by asset path.
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(entry.scenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#else
            yield return SceneManager.LoadSceneAsync(entry.name, LoadSceneMode.Additive);
#endif
            loadedScene = SceneManager.GetSceneAt(sceneCount);
            foreach (GameObject root in loadedScene.GetRootGameObjects()) {
                if (root.TryGetComponent(out NsmbArea area)) {
                    current = area;
                }
            }
            if (current == null) {
                Debug.LogWarning($"[NSMBDS] Scene {entry.scenePath} has no NsmbArea.");
                yield break;
            }

            if (player == null) {
                player = CreatePlayer();
                player.TravelRequested += Travel;
            }
            player.UsedBlock = currentEntry.usedBlock;
            player.EnterArea(current, entrance >= 0 ? entrance : current.startEntrance);
        }

        private static NsmbPlayer CreatePlayer() {
            GameObject mario = Instantiate(Resources.Load<GameObject>(MarioPrefab));
            mario.name = "Mario";
            NsmbPlayer player = mario.AddComponent<NsmbPlayer>();
            mario.AddComponent<NsmbPlayerAnimator>();
            mario.AddComponent<NsmbStage>();
            return player;
        }
    }
}

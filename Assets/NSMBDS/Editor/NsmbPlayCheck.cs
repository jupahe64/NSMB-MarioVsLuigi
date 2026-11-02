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
        private const double StepSeconds = 2.5;
        private const float CloseUpSize = 0.5f;

        private static double nextStepTime;
        private static int step;
        private static float startX;
        private static NsmbActor stomped;
        private static NsmbKoopa koopa;

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
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextStepTime) {
                return;
            }
            nextStepTime = EditorApplication.timeSinceStartup + StepSeconds;

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
                Debug.Log($"[NSMBDS] Play check: player at {select.Player?.X}, {select.Player?.Y}, grounded {select.Player?.Grounded}, tile 0x{select.Player?.TileUnderFeet:X8}");
                Capture("play_A01_1");
                select.Current.SetBehaviorOverlay(true);
                break;
            case 4:
                Capture("play_A01_1_overlay");
                select.Current.SetBehaviorOverlay(false);
                startX = select.Player.X;
                select.Player.ScriptedInput = new NsmbPlayerInput { Right = true };
                break;
            case 5:
                Debug.Log($"[NSMBDS] Play check: walked from {startX} to {select.Player.X}, speed {select.Player.SpeedX}, grounded {select.Player.Grounded}");
                Capture("play_A01_1_walked");
                CaptureCloseUp("play_mario_walking", select.Player);
                select.Player.ScriptedInput = default(NsmbPlayerInput);
                select.Player.Teleport(2368f, 400f);
                break;
            case 6:
                Debug.Log($"[NSMBDS] Play check: dropped on pipe, at {select.Player.X}, {select.Player.Y}, grounded {select.Player.Grounded}, tile 0x{select.Player.TileUnderFeet:X8}");
                Capture("play_pipe_top");
                CaptureCloseUp("play_mario_standing", select.Player);
                select.Player.ScriptedInput = new NsmbPlayerInput { Down = true };
                break;
            case 7:
                Debug.Log($"[NSMBDS] Play check: pressed down, in warp {select.Player.InWarp}, area {select.Player.AreaName}");
                break;
            case 8:
                select.Player.ScriptedInput = default(NsmbPlayerInput);
                Debug.Log($"[NSMBDS] Play check: after pipe, area {select.Player.AreaName}, at {select.Player.X}, {select.Player.Y}, camera {Camera.main.transform.position}, size {Camera.main.orthographicSize}, player transform {select.Player.transform.position}");
                foreach (NsmbRegionMarker region in select.Current.GetComponentsInChildren<NsmbRegionMarker>()) {
                    Debug.Log($"[NSMBDS] Play check: region zone={region.isZone} {region.WorldRect}");
                }
                Debug.Log($"[NSMBDS] Play check: tilemap bounds {select.Current.tilemap.cellBounds}, tile at feet 0x{select.Player.TileUnderFeet:X8}, renderer on {select.Current.tilemap.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().enabled}");
                Capture("play_after_pipe");
                select.LoadArea(select.FindEntry("A01_1").Value);
                break;
            case 9:
                select.Player.Teleport(22f * 16f + 8f, 480f);
                select.Player.Motor.Grounded = true;
                select.Player.ScriptedInput = new NsmbPlayerInput { Jump = true };
                break;
            case 10:
                select.Player.ScriptedInput = default(NsmbPlayerInput);
                Debug.Log($"[NSMBDS] Play check: block hit, tile now 0x{(select.Current.tilemap.GetTile(new Vector3Int(22, -27, 0)) as NsmbTile)?.behavior:X8}, items {Object.FindObjectsByType<NsmbItem>(FindObjectsSortMode.None).Length}, power {select.Player.Motor.Power}, coins {select.Player.Coins}");
                Capture("play_block_hit");
                NsmbItem mushroom = Object.FindAnyObjectByType<NsmbItem>();
                select.Player.Teleport(mushroom.transform.position.x * NsmbUnits.PixelsPerUnit, -mushroom.transform.position.y * NsmbUnits.PixelsPerUnit);
                break;
            case 11:
                Debug.Log($"[NSMBDS] Play check: collected {select.Player.GetComponent<NsmbStage>().LastCollected}, power now {select.Player.Motor.Power}");
                Capture("play_big");
                CaptureCloseUp("play_big_close", select.Player);
                select.Player.GetComponent<NsmbStage>().Hurt();
                select.Player.Teleport(344f, 479f);
                break;
            case 12:
                NsmbActor goomba = Object.FindAnyObjectByType<NsmbActor>();
                Debug.Log($"[NSMBDS] Play check: actors {select.Player.GetComponent<NsmbStage>().Actors.AliveCount}, goomba at {goomba?.X}, {goomba?.Y}, power {select.Player.Motor.Power}, player {select.Player.X}");
                Capture("play_goomba");
                stomped = goomba;
                if (goomba != null) {
                    select.Player.Teleport(goomba.X - 6f, goomba.Y - 40f);
                }
                break;
            case 13:
                Debug.Log($"[NSMBDS] Play check: stomp, goomba gone {stomped == null}, power {select.Player.Motor.Power}, area start respawn {select.Player.X}");
                select.Player.Teleport(1960f, 479f);
                break;
            case 14:
                Debug.Log($"[NSMBDS] Play check: koopa area, actors {select.Player.GetComponent<NsmbStage>().Actors.AliveCount}, player {select.Player.X}");
                Capture("play_koopa");
                koopa = Object.FindAnyObjectByType<NsmbKoopa>();
                select.Player.Teleport(koopa.X - 4f, koopa.Y - 40f);
                break;
            case 15:
                Capture("play_shell");
                Debug.Log($"[NSMBDS] Play check: stomped koopa, shell moving {koopa != null && koopa.IsMovingShell}, at {koopa?.X}, player {select.Player.X}");
                select.Player.Teleport(4000f, 479f);
                select.Player.ScriptedInput = new NsmbPlayerInput { Right = true };
                break;
            case 16:
                select.Player.ScriptedInput = default(NsmbPlayerInput);
                Debug.Log($"[NSMBDS] Play check: goal pole, finished {select.Player.GetComponent<NsmbStage>().LevelFinished}, player {select.Player.X}");
                select.LoadArea(select.FindEntry("A02_1").Value);
                break;
            case 17:
                select.Player.Teleport(2600f, 479f);
                break;
            case 18:
                NsmbPiranha piranha = Object.FindAnyObjectByType<NsmbPiranha>();
                Debug.Log($"[NSMBDS] Play check: piranha actors {select.Player.GetComponent<NsmbStage>().Actors.AliveCount}, at {piranha?.X}, {piranha?.Y}, sprite {piranha?.GetComponent<SpriteRenderer>().sprite?.name}");
                Capture("play_piranha");
                select.LoadArea(select.FindEntry("C01_1").Value);
                break;
            case 19:
                select.Player.Teleport(912f, 224f);
                break;
            case 20:
                Debug.Log($"[NSMBDS] Play check: water {select.Player.Motor.Liquid} surface {select.Player.Motor.LiquidSurfaceY}, underwater {select.Player.Motor.Underwater}, at {select.Player.X}, {select.Player.Y}");
                Capture("play_water");
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

        private static void CaptureCloseUp(string name, NsmbPlayer player) {
            Camera camera = Camera.main;
            Vector3 position = camera.transform.position;
            float size = camera.orthographicSize;
            camera.transform.position = new Vector3(player.transform.position.x, player.transform.position.y + 0.25f, position.z);
            camera.orthographicSize = CloseUpSize;
            Capture(name);
            camera.transform.position = position;
            camera.orthographicSize = size;
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

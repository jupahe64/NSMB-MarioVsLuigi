using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NsmbDs.Editor {
    /// <summary>Plays the emulator scenarios through the real game code, frame by frame, and writes the same columns for comparison.</summary>
    [InitializeOnLoad]
    public static class NsmbTraceCheck {
        private const string ActiveKey = "NsmbTraceCheckActive";
        private const string ScenePath = "Assets/NSMBDS/Scenes/NSMBDS_DebugSelect.unity";
        private const string TraceRoot = "RomAnalysis/emu/trace";

        private static readonly List<string> Scenarios = new();
        private static int next;
        private static bool waiting;
        private static double readyTime;

        static NsmbTraceCheck() {
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
            if (!EditorApplication.isPlaying) {
                return;
            }

            if (Scenarios.Count == 0) {
                Scenarios.AddRange(Directory.GetDirectories(TraceRoot).OrderBy(d => d));
            }

            NsmbDebugSelect select = Object.FindAnyObjectByType<NsmbDebugSelect>();
            if (select == null) {
                return;
            }

            if (next >= Scenarios.Count) {
                SessionState.SetBool(ActiveKey, false);
                EditorApplication.update -= Tick;
                Debug.Log("[NSMBDS] Trace check finished");
                EditorApplication.Exit(0);
                return;
            }

            string dir = Scenarios[next];
            string[] parts = File.ReadAllText(dir + "/scenario.txt").Trim().Split((char[]) null, System.StringSplitOptions.RemoveEmptyEntries);
            if (!waiting) {
                select.LoadArea(select.FindEntry(parts[1]).Value);
                waiting = true;
                readyTime = EditorApplication.timeSinceStartup + 1.5;
                return;
            }
            if (EditorApplication.timeSinceStartup < readyTime || select.Player == null || select.Current == null || select.Current.areaName != parts[1]) {
                return;
            }

            RunScenario(select.Player, dir, parts);
            waiting = false;
            next++;
        }

        private static void RunScenario(NsmbPlayer player, string dir, string[] parts) {
            string[] lines = File.ReadAllLines(dir + "/state.csv");
            string[] first = lines[1].Split(',');
            float x = float.Parse(first[1], CultureInfo.InvariantCulture);
            float y = float.Parse(first[2], CultureInfo.InvariantCulture);
            List<(string Key, int From, int To)> spans = parts[5].Split(',').Select(p => {
                string[] kv = p.Split(':');
                string[] range = kv[1].Split('-');
                return (kv[0], int.Parse(range[0]), int.Parse(range[1]));
            }).ToList();

            player.ManualStepping = true;
            player.Teleport(x, y);
            for (int i = 0; i < 8; i++) {
                player.StepManually(default);
            }
            player.Teleport(x, y);
            player.Motor.Grounded = true;

            string[] header = lines[0].Split(',');
            int oid = System.Array.IndexOf(header, "oid");
            float actorX = oid > 0 ? float.Parse(first[oid + 1], CultureInfo.InvariantCulture) : 0f;
            float actorY = oid > 0 ? float.Parse(first[oid + 2], CultureInfo.InvariantCulture) : 0f;
            NsmbActor actor = null;
            if (actorX != 0f) {
                float best = float.MaxValue;
                foreach (NsmbActor candidate in Object.FindObjectsByType<NsmbActor>(FindObjectsSortMode.None)) {
                    float distance = Mathf.Abs(candidate.X - actorX);
                    if (distance < best) {
                        best = distance;
                        actor = candidate;
                    }
                }
                actor?.PlaceAt(actorX, actorY);
            }

            StringBuilder output = new("frame,x,y,vx,vy,ax,ay\n");
            int frames = lines.Length - 2;
            for (int frame = 0; frame < frames; frame++) {
                NsmbPlayerInput input = default;
                foreach ((string key, int from, int to) in spans) {
                    if (frame >= from && frame < to) {
                        switch (key) {
                        case "left": input.Left = true; break;
                        case "right": input.Right = true; break;
                        case "up": input.Up = true; break;
                        case "down": input.Down = true; break;
                        case "a": input.Jump = true; break;
                        case "y": input.Run = true; break;
                        }
                    }
                }
                player.StepManually(input);
                output.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5},{6}", frame, player.X, player.Y, player.SpeedX, player.SpeedY, actor != null ? actor.X : 0f, actor != null ? actor.Y : 0f));
            }
            player.ManualStepping = false;
            File.WriteAllText(dir + "/unity.csv", output.ToString());
        }
    }
}

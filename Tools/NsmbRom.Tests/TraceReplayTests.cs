using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NsmbDs;
using NsmbRom.Nds;
using NsmbRom.Nsmb;
using Xunit;
using Xunit.Abstractions;

namespace NsmbRom.Tests {
    /// <summary>Replays scripted inputs against per-frame traces read from the emulator's memory. Traces stay in RomAnalysis.</summary>
    public class TraceReplayTests {
        private const float MaxFrameError = 2f;

        // The left edge of the view in C01_1, which NsmbPlayer enforces in the game.
        private const float LedgeLeftLimit = 768f;
        private readonly ITestOutputHelper output;

        public TraceReplayTests(ITestOutputHelper output) {
            this.output = output;
        }

        private static string? TraceRoot() {
            for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent) {
                string path = Path.Combine(dir.FullName, "RomAnalysis", "emu", "trace");
                if (Directory.Exists(path)) {
                    return path;
                }
            }
            return null;
        }

        private sealed record Span(string Key, int From, int To);

        private static NsmbPlayerInput InputAt(List<Span> spans, int frame) {
            NsmbPlayerInput input = default;
            foreach (Span span in spans) {
                if (frame < span.From || frame >= span.To) {
                    continue;
                }
                switch (span.Key) {
                case "left": input.Left = true; break;
                case "right": input.Right = true; break;
                case "up": input.Up = true; break;
                case "down": input.Down = true; break;
                case "a": input.Jump = true; break;
                case "y": input.Run = true; break;
                }
            }
            return input;
        }

        [RomFact("NSMB_USA.nds")]
        public void MovementFollowsTheGame() {
            string? root = TraceRoot();
            if (root == null) {
                return;
            }

            NsmbGame game = new(NdsRom.Load(File.ReadAllBytes(RomPaths.Find("NSMB_USA.nds")!)));
            StringBuilder report = new();
            double worst = 0;
            foreach (string dir in Directory.GetDirectories(root).OrderBy(d => d)) {
                string[] parts = File.ReadAllText(Path.Combine(dir, "scenario.txt")).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string name = parts[0];

                // Scenarios with enemies need the actors, so they only run in Unity (NsmbTraceCheck).
                if (name is "gtouch" or "gstomp" or "kstomp") {
                    continue;
                }

                List<Span> spans = parts[5].Split(',').Select(p => {
                    string[] kv = p.Split(':');
                    string[] range = kv[1].Split('-');
                    return new Span(kv[0], int.Parse(range[0]), int.Parse(range[1]));
                }).ToList();

                LevelArea area = game.LoadArea(parts[1]);
                AreaTilesets tilesets = game.GetTilesets(area);
                NsmbPlayerMotor motor = new(new NsmbPlayerTuning(), new GridTiles(game.ExpandArea(area, tilesets), tilesets));
                if (parts[1] == "C01_1") {
                    motor.Liquid = NsmbLiquidKind.Water;
                    motor.LiquidSurfaceY = 208f;
                }

                double[][] rows = File.ReadAllLines(Path.Combine(dir, "state.csv")).Skip(1)
                    .Select(l => l.Split(',').Take(9).Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray()).ToArray();
                motor.Place((float) rows[0][1], (float) rows[0][2]);
                motor.Step(default);
                motor.Place((float) rows[0][1], (float) rows[0][2]);
                motor.Grounded = true;

                double worstStep = 0, worstPos = 0;
                int worstFrame = 0;
                float lastX = motor.X, lastY = motor.Y;
                StringBuilder compare = new("frame,mx,gx,my,gy,gvx,gvy,mvx,mvy\n");
                for (int i = 1; i < rows.Length; i++) {
                    motor.Step(InputAt(spans, i - 1));
                    if (name == "ledgeL") {
                        motor.ClampX(LedgeLeftLimit, float.MaxValue);
                    }
                    double stepX = Math.Abs((motor.X - lastX) - (rows[i][1] - rows[i - 1][1]));
                    double stepY = Math.Abs((motor.Y - lastY) - (rows[i][2] - rows[i - 1][2]));
                    double pos = Math.Max(Math.Abs(motor.X - rows[i][1]), Math.Abs(motor.Y - rows[i][2]));
                    if (Math.Max(stepX, stepY) > worstStep) {
                        worstStep = Math.Max(stepX, stepY);
                        worstFrame = i - 1;
                    }
                    worstPos = Math.Max(worstPos, pos);
                    lastX = motor.X;
                    lastY = motor.Y;
                    compare.AppendLine(FormattableString.Invariant($"{i - 1},{motor.X:F2},{rows[i][1]:F2},{motor.Y:F2},{rows[i][2]:F2},{rows[i][3]:F3},{rows[i][4]:F3},{motor.SpeedX:F3},{motor.SpeedY:F3}"));
                }
                worst = Math.Max(worst, worstStep);
                File.WriteAllText(Path.Combine(dir, "compare.csv"), compare.ToString());
                report.AppendLine($"{name,-9} step error {worstStep:F3} at f{worstFrame}, position error {worstPos:F2}, end motor {motor.X:F1},{motor.Y:F1} game {rows[^1][1]:F1},{rows[^1][2]:F1}");
            }

            File.WriteAllText(Path.Combine(root, "report.txt"), report.ToString());
            output.WriteLine(report.ToString());
            Assert.True(worst <= MaxFrameError, report.ToString());
        }
    }
}

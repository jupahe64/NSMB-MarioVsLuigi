using System;
using NsmbDs;
using Xunit;

namespace NsmbRom.Tests {
    public class BlockTests {
        // Measured in the running game by hitting these tiles in A01_1.
        [Theory]
        [InlineData(0x00050000u, NsmbItemKind.Mushroom)]
        [InlineData(0x00050007u, NsmbItemKind.Coin)]
        [InlineData(0x0005000Du, NsmbItemKind.Mega)]
        [InlineData(0x00100004u, NsmbItemKind.Coin)]
        [InlineData(0x00100000u, NsmbItemKind.None)]
        public void DecodesBlockContent(uint word, NsmbItemKind expected) {
            Assert.Equal(expected, NsmbBlocks.Content(new NsmbTileBehavior(word)));
        }

        [Theory]
        [InlineData(NsmbPower.Small, NsmbItemKind.Mushroom, NsmbPower.Big)]
        [InlineData(NsmbPower.Fire, NsmbItemKind.Mushroom, NsmbPower.Fire)]
        [InlineData(NsmbPower.Big, NsmbItemKind.FireFlower, NsmbPower.Fire)]
        [InlineData(NsmbPower.Fire, NsmbItemKind.Star, NsmbPower.Fire)]
        public void ItemsChangePower(NsmbPower before, NsmbItemKind item, NsmbPower after) {
            Assert.Equal(after, NsmbBlocks.Collect(before, item));
        }

        [Fact]
        public void HittingATileFromBelowReportsIt() {
            TestTiles tiles = new() { GroundRow = 30 };
            tiles.Tiles[(5, 26)] = 0x00050000;
            NsmbPlayerMotor motor = new(new NsmbPlayerTuning(), tiles);
            motor.Place(88f, 479f);
            for (int i = 0; i < 8; i++) {
                motor.Step(default);
            }

            (int, int)? hit = null;
            motor.HeadHit += (column, row) => hit = (column, row);
            NsmbPlayerInput jump = new() { Jump = true };
            for (int i = 0; i < 30; i++) {
                motor.Step(jump);
            }

            Assert.Equal((5, 26), hit);
        }

        [Fact]
        public void LavaKillsAtTheSurface() {
            NsmbPlayerMotor motor = new(new NsmbPlayerTuning(), new TestTiles()) { Liquid = NsmbLiquidKind.Lava, LiquidSurfaceY = 300f };
            bool killed = false;
            motor.KilledByLiquid += () => killed = true;
            motor.Place(88f, 280f);
            for (int i = 0; i < 40; i++) {
                motor.Step(default);
            }

            Assert.True(killed);
        }

        [Theory]
        [InlineData(231, NsmbLiquidKind.Water, 0f)]
        [InlineData(234, NsmbLiquidKind.Lava, 3f)]
        [InlineData(259, NsmbLiquidKind.Poison, 8f)]
        public void LiquidSprites(int type, NsmbLiquidKind kind, float offset) {
            Assert.Equal(kind, NsmbBlocks.LiquidOf(type, out float actual));
            Assert.Equal(offset, actual);
        }
    }

    /// <summary>Replays inputs from a log of the real game in a real level. Skipped when the log is missing.</summary>
    public class SwimReplayTests {
        [RomFact("NSMB_USA.nds")]
        public void SwimmingFollowsTheRealGame() {
            string? path = null;
            for (System.IO.DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null && path == null; dir = dir.Parent) {
                string candidate = System.IO.Path.Combine(dir.FullName, "RomAnalysis", "emu", "hl", "swim1", "state.csv");
                if (System.IO.File.Exists(candidate)) {
                    path = candidate;
                }
            }
            if (path == null) {
                return;
            }

            NsmbRom.Nsmb.NsmbGame game = new(NsmbRom.Nds.NdsRom.Load(System.IO.File.ReadAllBytes(RomPaths.Find("NSMB_USA.nds")!)));
            NsmbRom.Nsmb.LevelArea area = game.LoadArea("C01_1");
            NsmbRom.Nsmb.AreaTilesets tilesets = game.GetTilesets(area);
            string[][] rows = System.Array.ConvertAll(System.IO.File.ReadAllLines(path), l => l.Split(','));
            double Read(int row, int column) => double.Parse(rows[row][column], System.Globalization.CultureInfo.InvariantCulture);

            NsmbPlayerMotor motor = new(new NsmbPlayerTuning(), new GridTiles(game.ExpandArea(area, tilesets), tilesets)) { Liquid = NsmbLiquidKind.Water, LiquidSurfaceY = 208f };
            motor.Place((float) Read(1, 1), (float) Read(1, 2));
            motor.Step(default);
            double worstX = 0, worstY = 0;
            System.Text.StringBuilder trace = new();
            for (int frame = 2; frame < rows.Length; frame++) {
                int f = frame - 1;
                motor.Step(new NsmbPlayerInput { Right = true, Jump = (f >= 120 && f < 130) || (f >= 160 && f < 170) });
                worstX = Math.Max(worstX, Math.Abs(motor.X - Read(frame, 1)));
                worstY = Math.Max(worstY, Math.Abs(motor.Y - Read(frame, 2)));
                if (f % 12 == 0) {
                    trace.Append($" f{f}:{motor.Y:F1}/{Read(frame, 2):F1}");
                }
            }

            Assert.True(worstX < 6 && worstY < 6, $"worst x {worstX:F2}, y {worstY:F2}{trace}");
        }
    }
}

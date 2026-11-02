using System;
using System.Collections.Generic;
using System.IO;
using NsmbDs;
using NsmbRom.Nds;
using NsmbRom.Nsmb;
using Xunit;
using Xunit.Abstractions;

namespace NsmbRom.Tests {
    internal sealed class GridTiles : NsmbTileSource {
        private readonly LevelGrid grid;
        private readonly AreaTilesets tilesets;

        public GridTiles(LevelGrid grid, AreaTilesets tilesets) {
            this.grid = grid;
            this.tilesets = tilesets;
        }

        public override NsmbTileBehavior GetTile(int column, int row) {
            if (column < 0 || row < 0 || column >= LevelGrid.Width || row >= LevelGrid.Height) {
                return default;
            }

            ushort cell = grid.Cells[(row * LevelGrid.Width) + column];
            TilesetSlot slot = LevelGrid.SplitCell(cell, out int tile);
            uint[] behaviors = tilesets[slot].Behaviors;
            return new NsmbTileBehavior(cell != 0 && tile < behaviors.Length ? behaviors[tile] : 0u);
        }
    }

    /// <summary>Walks Mario into every pipe and door of the real levels.</summary>
    public class WarpTests {
        private readonly ITestOutputHelper output;

        public WarpTests(ITestOutputHelper output) {
            this.output = output;
        }

        private const int SettleFrames = 60;
        private const int ApproachFrames = 60;
        private const float PipeApproach = 8f;
        private const int KnownTightExits = 3;
        private const int KnownUnreachable = 1;

        private static NsmbPlayerMotor Motor(NsmbTileSource tiles, float x, float y) {
            NsmbPlayerMotor motor = new(new NsmbPlayerTuning(), tiles);
            motor.Place(x, y);
            return motor;
        }

        private static void Settle(NsmbPlayerMotor motor) {
            for (int i = 0; i < 12; i++) {
                motor.Step(default);
            }
        }

        /// <summary>Side pipes over a gap have nothing to stand on in front of them.</summary>
        private static bool HasFloorInFront(NsmbWarp warp, NsmbTileSource tiles, LevelEntrance entrance) {
            float x = warp.Kind == NsmbEntranceKind.PipeLeft ? entrance.X - 8f : entrance.X + 24f;
            float feet = entrance.Y + (warp.IsMini ? 16f : 32f);
            return warp.Kind is NsmbEntranceKind.PipeUp or NsmbEntranceKind.PipeDown || tiles.GetTileAt(x, feet + 1f).IsFullSolid;
        }

        /// <summary>The way out of a pipe's mouth tile; an entrance deeper inside can't be reached.</summary>
        private static bool IsAtMouth(NsmbWarp warp, NsmbTileSource tiles, LevelEntrance entrance) {
            (float stepX, float stepY) = warp.EnterDirection;
            return !tiles.GetTileAt(entrance.X - (stepX * 16f) + 1f, entrance.Y - (stepY * 16f) + 1f).IsPipe;
        }

        private static bool Reaches(NsmbWarp warp, NsmbTileSource tiles, float x, float y, NsmbPlayerInput input, float bodyHeight) {
            NsmbPlayerMotor motor = Motor(tiles, x, y);
            if (input.Left || input.Right) {
                for (int i = 0; i < SettleFrames; i++) {
                    motor.Step(default);
                }
            }

            for (int i = 0; i < ApproachFrames; i++) {
                motor.Step(input);
                if (warp.CanEnter(motor, input, bodyHeight)) {
                    return true;
                }
            }
            return false;
        }

        private static bool CanEnter(NsmbWarp warp, NsmbTileSource tiles, LevelEntrance entrance, float height) {
            float x = entrance.X;
            float y = entrance.Y;
            float middle = x + (warp.IsMini ? 8f : 16f);
            float sideFeet = y + (warp.IsMini ? 16f : 32f) - 1f;
            switch (warp.Kind) {
            case NsmbEntranceKind.PipeUp:
                return Reaches(warp, tiles, middle, y - 2f, new NsmbPlayerInput { Down = true }, height);
            case NsmbEntranceKind.PipeDown:
                return Reaches(warp, tiles, middle, y + 16f + height + 1f, new NsmbPlayerInput { Up = true }, height);
            case NsmbEntranceKind.PipeLeft:
                return Reaches(warp, tiles, x - PipeApproach, sideFeet, new NsmbPlayerInput { Right = true }, height);
            case NsmbEntranceKind.PipeRight:
                return Reaches(warp, tiles, x + 16f + PipeApproach, sideFeet, new NsmbPlayerInput { Left = true }, height);
            default:
                return false;
            }
        }

        private static bool OverlapsSolid(NsmbTileSource tiles, float x, float y, NsmbPlayerTuning tuning) {
            foreach (float bodyX in new[] { x - tuning.halfWidth + 1f, x + tuning.halfWidth - 1f }) {
                foreach (float bodyY in new[] { y - 1f, y - (tuning.height / 2f), y - tuning.height + 1f }) {
                    if (tiles.GetTileAt(bodyX, bodyY).IsFullSolid) {
                        return true;
                    }
                }
            }
            return false;
        }

        [RomFact("NSMB_USA.nds")]
        public void EveryPipeInEveryLevelCanBeEnteredAndLeft() {
            NsmbGame game = new(NdsRom.Load(File.ReadAllBytes(RomPaths.Find("NSMB_USA.nds")!)));
            NsmbPlayerTuning tuning = new();
            List<string> cannotEnter = new();
            List<string> stuckOnExit = new();
            List<string> floorless = new();
            int pipes = 0;

            foreach (string name in game.AreaNames) {
                LevelArea area = game.LoadArea(name);
                AreaTilesets tilesets = game.GetTilesets(area);
                GridTiles tiles = new(game.ExpandArea(area, tilesets), tilesets);
                foreach (LevelEntrance entrance in area.Entrances) {
                    NsmbWarp warp = new(entrance.Type, entrance.X, entrance.Y);
                    if (warp.Kind is NsmbEntranceKind.Other or NsmbEntranceKind.Door) {
                        continue;
                    }

                    (float exitX, float exitY) = warp.ExitPosition(tiles, tuning.halfWidth, tuning.height);
                    if (OverlapsSolid(tiles, exitX, exitY, tuning)) {
                        stuckOnExit.Add($"{name} e{entrance.Number} type {entrance.Type} at {entrance.X},{entrance.Y}");
                    }

                    if ((entrance.Settings & NsmbWarp.ExitOnlySetting) == 0 && IsAtMouth(warp, tiles, entrance)) {
                        pipes++;
                        if (!HasFloorInFront(warp, tiles, entrance)) {
                            floorless.Add($"{name} e{entrance.Number}");
                        } else if (!CanEnter(warp, tiles, entrance, tuning.height)) {
                            cannotEnter.Add($"{name} e{entrance.Number} type {entrance.Type} at {entrance.X},{entrance.Y}");
                        }
                    }
                }
            }

            Assert.True(pipes > 200, $"only {pipes} pipes checked");
            output.WriteLine($"{pipes} pipes, no floor in front of {floorless.Count}: {string.Join(", ", floorless)}");
            Assert.True(cannotEnter.Count <= KnownUnreachable && stuckOnExit.Count <= KnownTightExits,
                $"{pipes} pipes. Cannot enter {cannotEnter.Count}: {string.Join("; ", cannotEnter)}\nStuck on exit {stuckOnExit.Count}: {string.Join("; ", stuckOnExit)}");
        }

        [Fact]
        public void DoorWorksAcrossItsWholeWidth() {
            NsmbWarp door = new(2, 160f, 448f);
            TestTiles tiles = new() { GroundRow = 30 };
            foreach (float x in new[] { 161f, 176f, 191f }) {
                NsmbPlayerMotor motor = Motor(tiles, x, 479f);
                Settle(motor);
                Assert.True(door.CanEnter(motor, new NsmbPlayerInput { Up = true }, 13.44f), $"at {x}");
            }
        }

        [Theory]
        [InlineData(5, 480f - 32f, 160f, false)]
        [InlineData(5, 480f - 16f, 160f, true)]
        [InlineData(18, 480f - 16f, 160f, false)]
        public void SidePipeNeedsFeetAtItsBottom(int type, float y, float x, bool tooLow) {
            NsmbWarp pipe = new(type, x, y);
            NsmbPlayerMotor motor = Motor(new TestTiles { GroundRow = 30 }, x - 6f, 479f);
            Settle(motor);

            Assert.Equal(!tooLow, pipe.CanEnter(motor, new NsmbPlayerInput { Right = true }, 13.44f));
        }

        [Fact]
        public void LeavesSidePipeClearOfIt() {
            TestTiles tiles = new() { GroundRow = 30 };
            tiles.Tiles[(10, 28)] = 0x01010000;
            tiles.Tiles[(10, 29)] = 0x01010000;
            tiles.Tiles[(11, 28)] = 0x01010000;
            tiles.Tiles[(11, 29)] = 0x01010000;

            (float x, float y) = new NsmbWarp(5, 176f, 448f).ExitPosition(tiles, 6f, 13.44f);

            Assert.Equal(153f, x);
            Assert.Equal(480f, y);
        }
    }
}

using System;
using System.Collections.Generic;
using NsmbDs;
using Xunit;

namespace NsmbRom.Tests {
    internal sealed class TestTiles : NsmbTileSource {
        public readonly Dictionary<(int, int), uint> Tiles = new();
        public int GroundRow = int.MaxValue;

        public override NsmbTileBehavior GetTile(int column, int row) {
            if (Tiles.TryGetValue((column, row), out uint word)) {
                return new NsmbTileBehavior(word);
            }
            return new NsmbTileBehavior(row >= GroundRow ? 0x00010000u : 0u);
        }
    }

    public class TileBehaviorTests {
        [Theory]
        [InlineData(0x00010000u, true)]
        [InlineData(0x00000000u, false)]
        [InlineData(0x00020000u, false)]
        [InlineData(0x80000000u, false)]
        [InlineData(0x0020000Au, false)]
        [InlineData(0x01010024u, true)]
        public void RecognisesSolidTiles(uint word, bool solid) {
            Assert.Equal(solid, new NsmbTileBehavior(word).IsFullSolid);
        }

        [Theory]
        [InlineData(0, 8f, 8f)]
        [InlineData(1, 4f, 12f)]
        [InlineData(3, 8f, 12f)]
        [InlineData(13, 8f, 10f)]
        [InlineData(18, 8f, 2f)]
        public void GivesSlopeHeights(int id, float column, float expected) {
            Assert.Equal(expected, new NsmbTileBehavior(0x00200000u | (uint) id).SlopeHeight(column));
        }
    }

    public class PlayerMotorCollisionTests {
        private static NsmbPlayerMotor Motor(TestTiles tiles, float x, float y) {
            NsmbPlayerMotor motor = new(new NsmbPlayerTuning(), tiles);
            motor.Place(x, y);
            return motor;
        }

        private static void Run(NsmbPlayerMotor motor, NsmbPlayerInput input, int frames) {
            for (int i = 0; i < frames; i++) {
                motor.Step(input);
            }
        }

        [Fact]
        public void LandsOnGround() {
            NsmbPlayerMotor motor = Motor(new TestTiles { GroundRow = 30 }, 88f, 400f);

            Run(motor, default, 60);

            Assert.True(motor.Grounded);
            Assert.Equal(480f, motor.Y);
        }

        [Fact]
        public void WallStopsMovement() {
            TestTiles tiles = new() { GroundRow = 30 };
            tiles.Tiles[(10, 29)] = 0x00010000;
            tiles.Tiles[(10, 28)] = 0x00010000;
            NsmbPlayerMotor motor = Motor(tiles, 100f, 480f);

            Run(motor, new NsmbPlayerInput { Right = true }, 120);

            Assert.Equal(154f, motor.X);
            Assert.Equal(0f, motor.VelocityX, 1f);
            Assert.True(motor.TouchingRightWall);
        }

        [Fact]
        public void SolidOnTopHoldsFromAboveAndLetsYouJumpThrough() {
            TestTiles tiles = new() { GroundRow = 30 };
            for (int column = 0; column < 40; column++) {
                tiles.Tiles[(column, 27)] = 0x80000000;
            }

            NsmbPlayerMotor from = Motor(tiles, 88f, 400f);
            Run(from, default, 60);
            Assert.Equal(432f, from.Y);

            NsmbPlayerMotor through = Motor(tiles, 88f, 480f);
            Run(through, default, 2);
            Run(through, new NsmbPlayerInput { Jump = true }, 40);
            Run(through, default, 60);
            Assert.Equal(432f, through.Y);
        }

        [Fact]
        public void CeilingStopsJump() {
            TestTiles tiles = new() { GroundRow = 30 };
            tiles.Tiles[(5, 26)] = 0x00010000;
            NsmbPlayerMotor motor = Motor(tiles, 88f, 480f);
            Run(motor, default, 2);

            float top = motor.Y;
            NsmbPlayerInput jump = new() { Jump = true };
            for (int i = 0; i < 40; i++) {
                motor.Step(jump);
                top = Math.Min(top, motor.Y);
            }

            Assert.Equal(445.44f, top, 2);
        }

        [Fact]
        public void WalksUpSlope() {
            TestTiles tiles = new() { GroundRow = 30 };
            tiles.Tiles[(10, 29)] = 0x00200000;
            for (int column = 11; column < 40; column++) {
                tiles.Tiles[(column, 29)] = 0x00010000;
            }

            NsmbPlayerMotor motor = Motor(tiles, 100f, 480f);
            Run(motor, new NsmbPlayerInput { Right = true }, 80);

            Assert.True(motor.X > 176f, $"stopped at {motor.X}");
            Assert.Equal(464f, motor.Y);
        }
    }

    /// <summary>Checks the moves ported from the base project.</summary>
    public class PlayerMotorMoveTests {
        private static NsmbPlayerMotor Grounded(TestTiles tiles, float x = 88f) {
            NsmbPlayerMotor motor = new(new NsmbPlayerTuning(), tiles);
            motor.Place(x, (tiles.GroundRow * 16f) - 1f);
            Run(motor, default, 4);
            Assert.True(motor.Grounded);
            return motor;
        }

        private static NsmbPlayerMotor FlatGround(float x = 88f) {
            return Grounded(new TestTiles { GroundRow = 30 }, x);
        }

        private static void Run(NsmbPlayerMotor motor, NsmbPlayerInput input, int frames) {
            for (int i = 0; i < frames; i++) {
                motor.Step(input);
            }
        }

        private static float JumpPeak(NsmbPlayerMotor motor, NsmbPlayerInput held) {
            float start = motor.Y;
            float top = start;
            do {
                motor.Step(held);
                top = Math.Min(top, motor.Y);
            } while (!motor.Grounded);
            return start - top;
        }

        [Theory]
        [InlineData(false, 1.5f)]
        [InlineData(true, 3f)]
        public void ReachesTopSpeed(bool run, float pixelsPerFrame) {
            NsmbPlayerMotor motor = FlatGround();

            Run(motor, new NsmbPlayerInput { Right = true, Run = run }, 120);

            Assert.Equal(pixelsPerFrame, motor.SpeedX, 3);
        }

        [Fact]
        public void HoldingJumpGoesHigher() {
            float full = JumpPeak(FlatGround(), new NsmbPlayerInput { Jump = true });

            NsmbPlayerMotor tap = FlatGround();
            tap.Step(new NsmbPlayerInput { Jump = true });
            float tapped = JumpPeak(tap, default);

            Assert.InRange(full, 60f, 80f);
            Assert.True(tapped < full - 16f, $"tap {tapped}, full {full}");
        }

        [Fact]
        public void JumpPressedJustBeforeLandingIsBuffered() {
            NsmbPlayerMotor motor = new(new NsmbPlayerTuning(), new TestTiles { GroundRow = 30 });
            motor.Place(88f, 470f);
            motor.Step(default);

            bool jumped = false;
            NsmbPlayerInput jump = new() { Jump = true };
            for (int i = 0; i < 12 && !jumped; i++) {
                motor.Step(jump);
                jumped = motor.VelocityY > 1f;
            }

            Assert.True(jumped);
        }

        [Fact]
        public void CanJumpJustAfterWalkingOffALedge() {
            TestTiles tiles = new() { GroundRow = 30 };
            for (int column = 7; column < 40; column++) {
                tiles.Tiles[(column, 30)] = 0u;
                tiles.Tiles[(column, 31)] = 0u;
            }

            NsmbPlayerMotor motor = Grounded(tiles, 104f);
            while (motor.Grounded) {
                motor.Step(new NsmbPlayerInput { Right = true });
            }
            motor.Step(new NsmbPlayerInput { Right = true, Jump = true });

            Assert.True(motor.VelocityY > 6f);
        }

        [Fact]
        public void ChainsDoubleAndTripleJumps() {
            NsmbPlayerMotor motor = FlatGround();
            NsmbPlayerInput run = new() { Right = true, Run = true };
            Run(motor, run, 120);

            List<NsmbJumpState> seen = new();
            bool wasGrounded = true;
            for (int i = 0; i < 400; i++) {
                NsmbPlayerInput frame = run;
                frame.Jump = motor.Grounded && !wasGrounded;
                if (seen.Count == 0 && i == 0) {
                    frame.Jump = true;
                }
                wasGrounded = motor.Grounded;
                motor.Step(frame);
                if (!motor.Grounded && (seen.Count == 0 || seen[^1] != motor.JumpState)) {
                    seen.Add(motor.JumpState);
                }
            }

            Assert.Equal(new[] { NsmbJumpState.Single, NsmbJumpState.Double, NsmbJumpState.Triple }, seen.GetRange(0, 3));
        }

        [Fact]
        public void SkidsThenTurnsAround() {
            NsmbPlayerMotor motor = FlatGround(800f);
            Run(motor, new NsmbPlayerInput { Right = true, Run = true }, 120);

            NsmbPlayerInput back = new() { Left = true, Run = true };
            motor.Step(back);
            Assert.True(motor.Skidding);

            Run(motor, back, 80);
            Assert.False(motor.Skidding);
            Assert.True(motor.VelocityX < 0f);
        }

        [Fact]
        public void SlidesDownWallAndWallJumps() {
            TestTiles tiles = new() { GroundRow = 60 };
            for (int row = 0; row < 60; row++) {
                tiles.Tiles[(10, row)] = 0x00010000;
            }

            NsmbPlayerMotor motor = new(new NsmbPlayerTuning(), tiles);
            motor.Place(140f, 200f);
            NsmbPlayerInput right = new() { Right = true };
            Run(motor, right, 40);
            Assert.True(motor.WallslideRight);
            Assert.True(motor.VelocityY >= -4.6875f);

            bool walljumped = false;
            motor.Walljumped += () => walljumped = true;
            motor.Step(new NsmbPlayerInput { Right = true, Jump = true });

            Assert.True(walljumped);
            Assert.True(motor.VelocityX < 0f);
            Assert.True(motor.VelocityY > 0f);
        }

        [Fact]
        public void GroundpoundHangsThenDropsFast() {
            NsmbPlayerMotor motor = new(new NsmbPlayerTuning(), new TestTiles { GroundRow = 60 });
            motor.Place(88f, 300f);
            motor.Step(default);
            NsmbPlayerInput down = new() { Down = true };
            Run(motor, down, 6);
            Assert.True(motor.Groundpounding);
            Assert.True(motor.VelocityY > -1f);

            Run(motor, down, 18);
            Assert.Equal(-11.25f, motor.VelocityY, 2);

            while (!motor.Grounded) {
                motor.Step(down);
            }
            Run(motor, default, 2);
            Assert.False(motor.Groundpounding);
        }

        [Fact]
        public void CrouchingStopsWalkingButNotJumping() {
            NsmbPlayerMotor motor = FlatGround();
            NsmbPlayerInput crouch = new() { Down = true, Right = true };
            Run(motor, crouch, 30);

            Assert.True(motor.Crouching);
            Assert.Equal(0f, motor.VelocityX);
        }

        [Fact]
        public void SlidesDownSlopeWhenHoldingDown() {
            TestTiles tiles = new() { GroundRow = 30 };
            for (int i = 0; i < 12; i++) {
                tiles.Tiles[(10 + i, 18 + i)] = 0x00200001;
                for (int row = 19 + i; row < 30; row++) {
                    tiles.Tiles[(10 + i, row)] = 0x00010000;
                }
            }

            NsmbPlayerMotor motor = new(new NsmbPlayerTuning(), tiles);
            motor.Place(170f, 280f);
            Run(motor, default, 30);
            Assert.Equal(45f, motor.FloorAngle, 1);

            Run(motor, new NsmbPlayerInput { Down = true }, 30);

            Assert.True(motor.Sliding);
            Assert.True(motor.VelocityX > 2f, $"speed {motor.VelocityX}");
        }

        [Fact]
        public void SwimsUpOnPress() {
            TestTiles tiles = new() { GroundRow = 40 };
            for (int column = 0; column < 20; column++) {
                for (int row = 0; row < 40; row++) {
                    tiles.Tiles[(column, row)] = 0x02000000;
                }
            }

            NsmbPlayerMotor motor = new(new NsmbPlayerTuning(), tiles);
            motor.Place(88f, 300f);
            Run(motor, default, 30);
            Assert.True(motor.Underwater);
            Assert.InRange(motor.VelocityY, -2.82f, 0f);

            motor.Step(new NsmbPlayerInput { Jump = true });

            Assert.True(motor.VelocityY > 0f);
        }
    }
}

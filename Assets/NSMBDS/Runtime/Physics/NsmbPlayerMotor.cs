using System;

namespace NsmbDs {
    /// <summary>Tunable movement values, copied from the base project's MarioPhysics asset. Speeds are units per second, y up.</summary>
    [Serializable]
    public sealed class NsmbPlayerTuning {
        public float[] walkMaxVelocity = { 0.9375f, 2.8125f, 4.21875f, 5.625f, 8.4375f };
        public float[] walkAcceleration = { 7.91015625f, 3.955081725f, 3.515625f, 2.63671875f, 84.375f };
        public float[] walkIceAcceleration = { 1.9775390625f, 3.955081725f, 3.515625f, 2.63671875f, 84.375f };
        public int walkSpeedStage = 1;
        public int runSpeedStage = 3;
        public float walkReleaseDeceleration = 3.9550781196f;
        public float[] walkReleaseIceDeceleration = { 0.439453125f, 1.483154296875f, 1.483154296875f, 1.483154296875f, 1.483154296875f };
        public float[] slowTurnaroundAcceleration = { 3.955078125f, 8.7890625f, 8.7890625f, 21.093756f };
        public float slowTurnaroundIceAcceleration = 2.63671875f;
        public float fastTurnaroundAcceleration = 28.125f;
        public int fastTurnaroundFrames = 10;
        public float skiddingMinimumVelocity = 4.6875f;
        public float skiddingDeceleration = 10.54687536f;
        public float skiddingIceDeceleration = 3.955078125f;
        public float skiddingFastDeceleration = 84.375f;
        public float airReverseMultiplier = 0.85f;
        public float slopeMaxSpeedPerDegree = -0.03047180176f;
        public float crouchOffEdgeVelocity = -3.75f;

        public int jumpBufferFrames = 12;
        public int coyoteTimeFrames = 3;
        public int specialJumpLandingFrames = 12;
        public float jumpVelocity = 6.62109375f;
        public float jumpSpeedBonusVelocity = 0.46875f;
        public float jumpTripleBonusVelocity = 0.5f;
        public float walljumpHorizontalVelocity = 4.21875f;
        public float walljumpVerticalVelocity = 6.4453125f;
        public int walljumpFrames = 16;
        public int wallslideEndFrames = 16;

        public float[] gravityVelocity = { 4.16015625f, 2.109375f, 0f, -5.859375f };
        public float[] gravityAcceleration = { -7.03125f, -28.125f, -38.671875f, -28.125f, -38.671875f };
        public float[] gravitySwimmingVelocity = { 0f };
        public float[] gravitySwimmingAcceleration = { -4.833984375f, -3.076171875f };
        public float gravityGroundpoundStart = 0.15f;
        public float terminalVelocity = -7.5f;
        public float terminalVelocityWallslide = -4.6875f;
        public float terminalVelocityGroundpound = -11.25f;

        public bool groundpoundWhileHoldingSideways = true;
        public int groundpoundInputDelayFrames = 5;
        public int groundpoundStartFrames = 16;
        public float groundpoundStartVelocity = 1.5f;
        public int groundpoundStandFrames = 15;
        public int groundpoundCancelFrames = 12;

        public float swimJumpVelocity = 2.26318359375f;
        public float swimMaxVerticalVelocity = 4.833984375f;
        public float swimTerminalVelocity = -2.8125f;
        public float swimTerminalVelocityHeld = -0.9375f;
        public float swimDeceleration = 1.7578125f;
        public float swimGroundpoundDeceleration = 38.671875f;
        public float[] swimMaxVelocity = { 0f, 2.109375f };
        public float[] swimAcceleration = { 1.7578125f, 3.076171875f, 0.439453125f };
        public float[] swimWalkMaxVelocity = { 1.0546875f, 1.0546875f };
        public float[] swimWalkAcceleration = { 3.07617875f, 1.7578125f };

        public float slideMaxVelocity = 7.5f;
        public float slideFastAcceleration = 13.1835975f;
        public float slideSlowAcceleration = 5.2734375f;
        public float slideMinimumAngle = 12.5f;

        public float halfWidth = 6f;
        public float height = 13.44f;
        public float groundSnap = 8f;
        public int hoverFrames = 3;
        public float wallProbe = 0.5f;
    }

    public struct NsmbPlayerInput {
        public bool Left;
        public bool Right;
        public bool Up;
        public bool Down;
        public bool Jump;
        public bool Run;
    }

    public enum NsmbJumpState { None, Single, Double, Triple }

    /// <summary>Port of the base project's MarioPlayerSystem to plain C#: no Quantum, only what the campaign needs, and tile collision.</summary>
    /// <remarks>The handlers run in the base project's order. Items, stomps, knockback and versus rules are left out.</remarks>
    public sealed class NsmbPlayerMotor {
        private const float StepSeconds = 1f / 60f;
        private const float PixelsPerFrame = 32f / 60f;
        private const float SmallSpeed = 0.05f;

        private readonly NsmbPlayerTuning tuning;
        private readonly NsmbTileSource tiles;
        private NsmbPlayerInput input;
        private NsmbPlayerInput previousInput;
        private int frame;
        private int walljumpFrames;
        private int groundpoundStandFrames;
        private int groundpoundCooldownFrames;
        private int groundpoundStartFrames;
        private int fastTurnaroundFrames;
        private int slowTurnaroundFrames;
        private int jumpBufferFrames;
        private int coyoteTimeFrames;
        private int wallslideEndFrames;
        private int hoverFrames;
        private int landedFrame;
        private NsmbJumpState previousJumpState;
        private float gravity;
        private float terminalVelocity;

        /// <summary>Feet position in DS pixels, y growing downwards.</summary>
        public float X;
        public float Y;

        /// <summary>Velocity in units per second, y growing upwards, as in the base project.</summary>
        public float VelocityX;
        public float VelocityY;

        public bool Grounded;
        public bool WasGrounded;
        public bool TouchingLeftWall;
        public bool TouchingRightWall;
        public bool Underwater;
        public bool OnSlippery;
        public bool OnSlideable;

        /// <summary>Degrees; positive when the floor descends to the right.</summary>
        public float FloorAngle;

        public bool FacingRight = true;
        public bool Crouching;
        public bool Skidding;
        public bool Turnaround;
        public bool Sliding;
        public bool Groundpounding;
        public bool GroundpoundActive;
        public bool WallslideLeft;
        public bool WallslideRight;
        public NsmbJumpState JumpState;

        public event Action LandedStill = delegate { };
        public event Action Walljumped = delegate { };
        public event Action Paddled = delegate { };

        public NsmbPlayerMotor(NsmbPlayerTuning tuning, NsmbTileSource tiles) {
            this.tuning = tuning;
            this.tiles = tiles;
        }

        /// <summary>Speed in DS pixels per frame, y growing downwards.</summary>
        public float SpeedX => VelocityX * PixelsPerFrame;
        public float SpeedY => -VelocityY * PixelsPerFrame;
        public bool Wallsliding => WallslideLeft || WallslideRight;
        public bool FastTurnaround => fastTurnaroundFrames > 0;
        public bool GroundpoundStanding => groundpoundStandFrames > 0;
        public bool CoyoteTime => coyoteTimeFrames > 0;

        public void Place(float x, float y) {
            X = x;
            Y = y;
            Stop();
            Grounded = false;
            WasGrounded = false;
            Crouching = false;
            Skidding = false;
            Turnaround = false;
            Sliding = false;
            Groundpounding = false;
            GroundpoundActive = false;
            WallslideLeft = false;
            WallslideRight = false;
            JumpState = NsmbJumpState.None;
            previousJumpState = NsmbJumpState.None;
            walljumpFrames = 0;
            groundpoundStandFrames = 0;
            groundpoundStartFrames = 0;
            fastTurnaroundFrames = 0;
            hoverFrames = 0;
        }

        /// <summary>Stops at the given limits, like an invisible wall.</summary>
        public void ClampX(float min, float max) {
            if (X < min) {
                X = min;
                VelocityX = Math.Max(0f, VelocityX);
            } else if (X > max) {
                X = max;
                VelocityX = Math.Min(0f, VelocityX);
            }
        }

        public void Stop() {
            VelocityX = 0f;
            VelocityY = 0f;
        }

        /// <summary>Advances one 60 Hz frame.</summary>
        public void Step(in NsmbPlayerInput newInput) {
            input = newInput;
            frame++;

            bool wasGroundpounding = Groundpounding;
            StepCrouching();
            StepGroundpound();
            StepSliding();
            StepWalkingRunning();
            StepJumping(wasGroundpounding);
            StepSwimming();
            StepWallslide();
            StepGravity();
            StepTerminalVelocity();
            StepFacingDirection();

            Move();
            ApplyGravity();
            previousInput = input;
        }

        private bool Pressed(bool now, bool before) {
            return now && !before;
        }

        private static bool Decrement(ref int timer) {
            return timer <= 0 || --timer == 0;
        }

        private static float Clamp01(float value) {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static int Sign(float value) {
            return value > 0f ? 1 : value < 0f ? -1 : 0;
        }

        private int SpeedStage() {
            float speed = Math.Abs(VelocityX) - 0.01f;
            float[] stages = tuning.walkMaxVelocity;
            if (Underwater) {
                stages = Grounded ? tuning.swimWalkMaxVelocity : tuning.swimMaxVelocity;
            }

            for (int i = 0; i < stages.Length; i++) {
                if (speed <= stages[i]) {
                    return i;
                }
            }
            return stages.Length - 1;
        }

        private int GravityStage() {
            float[] stages = Underwater ? tuning.gravitySwimmingVelocity : tuning.gravityVelocity;
            for (int i = 0; i < stages.Length; i++) {
                if (VelocityY >= stages[i]) {
                    return i;
                }
            }
            return stages.Length;
        }

        private void StepCrouching() {
            if (Crouching && WasGrounded && !Grounded && VelocityY < 0.1f) {
                VelocityY = !Sliding && !Groundpounding ? tuning.crouchOffEdgeVelocity : 0f;
                hoverFrames = 0;
            }

            if (Sliding || Wallsliding) {
                Crouching = false;
                return;
            }

            Crouching = (Grounded && input.Down && !Groundpounding)
                || (!Grounded && (input.Down || VelocityY > 0f) && Crouching && !Underwater);
        }

        private void StepGroundpound() {
            if (Pressed(input.Down, previousInput.Down) && groundpoundCooldownFrames == 0) {
                groundpoundCooldownFrames = tuning.groundpoundInputDelayFrames;
            }

            bool allowStart = groundpoundCooldownFrames == 1;
            Decrement(ref groundpoundCooldownFrames);
            if (input.Down && allowStart) {
                TryStartGroundpound();
            }

            StepGroundpoundStart();
            if (Grounded && Groundpounding && GroundpoundActive) {
                GroundpoundActive = false;
            }

            if (Underwater && Groundpounding) {
                VelocityY += tuning.swimGroundpoundDeceleration * StepSeconds;
                if (VelocityY >= tuning.swimTerminalVelocityHeld) {
                    Groundpounding = false;
                }
            } else if (Groundpounding) {
                if (Grounded && WasGrounded && !input.Down) {
                    groundpoundStandFrames = tuning.groundpoundStandFrames;
                    Groundpounding = false;
                } else if (input.Up && groundpoundStartFrames == 0) {
                    groundpoundCooldownFrames = tuning.groundpoundCancelFrames;
                    Groundpounding = false;
                }
            }
            GroundpoundActive &= Groundpounding;
        }

        private void TryStartGroundpound() {
            if (Grounded || Groundpounding || Crouching || Sliding || Wallsliding || groundpoundCooldownFrames > 0 || Underwater) {
                return;
            }
            if (!tuning.groundpoundWhileHoldingSideways && (input.Left || input.Right)) {
                return;
            }

            WallslideLeft = false;
            WallslideRight = false;
            Groundpounding = true;
            JumpState = NsmbJumpState.None;
            VelocityX = 0f;
            VelocityY = tuning.groundpoundStartVelocity;
            groundpoundStartFrames = tuning.groundpoundStartFrames;
        }

        private void StepGroundpoundStart() {
            if (!Groundpounding || groundpoundStartFrames == 0) {
                return;
            }

            if (Decrement(ref groundpoundStartFrames)) {
                GroundpoundActive = true;
            }

            VelocityX = 0f;
            VelocityY = groundpoundStartFrames switch {
                0 => tuning.terminalVelocityGroundpound,
                >= 4 => tuning.groundpoundStartVelocity,
                _ => 0f,
            };
        }

        private void StepSliding() {
            bool validFloorAngle = Math.Abs(FloorAngle) >= tuning.slideMinimumAngle;
            Crouching &= !Sliding;

            bool wallAhead = (FacingRight && TouchingRightWall) || (!FacingRight && TouchingLeftWall);
            if (OnSlideable && validFloorAngle && !wallAhead && (Crouching || input.Down) && !Underwater) {
                Sliding = true;
                Crouching = false;
                Groundpounding = false;
                GroundpoundActive = false;
            }

            if (!Sliding) {
                return;
            }

            if (Grounded && validFloorAngle) {
                float runningMax = tuning.walkMaxVelocity[tuning.runSpeedStage];
                bool uphill = Sign(FloorAngle) != Sign(VelocityX);
                float speed = StepSeconds * 5f * (uphill ? Clamp01(1f - (Math.Abs(VelocityX) / runningMax)) : 4f);
                float limit = runningMax * 1.25f;
                VelocityX = Math.Max(-limit, Math.Min(limit, VelocityX + ((float) Math.Sin(FloorAngle * Math.PI / 180f) * speed)));
            }

            bool stationary = Math.Abs(VelocityX) < 0.01f && Grounded;
            if (input.Up || ((input.Left ^ input.Right) && !input.Down) || (stationary && !input.Down) || wallAhead) {
                Sliding = false;
            }
        }

        private void StepWalkingRunning() {
            if (!Decrement(ref walljumpFrames)) {
                return;
            }

            if (groundpoundStandFrames > 0) {
                if (!Grounded) {
                    groundpoundStandFrames = 0;
                } else {
                    groundpoundStandFrames--;
                    return;
                }
            }

            if (Groundpounding) {
                return;
            }

            if (!Grounded || Underwater) {
                Skidding = false;
            }

            float[] maxArray = tuning.walkMaxVelocity;
            int maxStage = input.Run ? tuning.runSpeedStage : tuning.walkSpeedStage;
            if (Underwater) {
                maxArray = Grounded ? tuning.swimWalkMaxVelocity : tuning.swimMaxVelocity;
                maxStage = tuning.swimMaxVelocity.Length - 1;
            }

            int stage = SpeedStage();
            float acc;
            if (Underwater) {
                acc = Grounded ? tuning.swimWalkAcceleration[stage] : tuning.swimAcceleration[stage];
            } else if (OnSlippery) {
                acc = tuning.walkIceAcceleration[stage];
            } else {
                acc = tuning.walkAcceleration[stage];
            }

            float xVel = VelocityX;
            float xVelAbs = Math.Abs(xVel);
            int sign = Sign(xVel);
            bool uphill = Math.Abs(FloorAngle) > tuning.slideMinimumAngle && Sign(FloorAngle) != sign;

            if (!Grounded) {
                fastTurnaroundFrames = 0;
            }

            if (fastTurnaroundFrames > 0) {
                VelocityX = 0f;
                if (Decrement(ref fastTurnaroundFrames)) {
                    Turnaround = true;
                }
            } else if (Turnaround && !OnSlippery) {
                Turnaround = Grounded && !Crouching && xVelAbs < tuning.walkMaxVelocity[1] && !TouchingLeftWall && !TouchingRightWall;
                Skidding = Turnaround;
                VelocityX += tuning.fastTurnaroundAcceleration * (FacingRight ? -1 : 1) * StepSeconds;
            } else if ((input.Left ^ input.Right) && (!Crouching || !Grounded) && !Sliding) {
                WalkInDirection(input.Left ? -1 : 1, acc, maxArray, maxStage, uphill);
            } else if (Grounded || Underwater) {
                Decelerate(acc, stage, uphill);
            }

            Crouching &= !Sliding;
        }

        private void WalkInDirection(int direction, float acc, float[] maxArray, int maxStage, bool uphill) {
            float xVel = VelocityX;
            float xVelAbs = Math.Abs(xVel);
            int sign = Sign(xVel);
            if (Skidding) {
                direction = -sign;
            }

            bool reverse = xVel != 0f && direction != sign;

            float max = maxArray[maxStage];
            if (!Underwater) {
                max += tuning.slopeMaxSpeedPerDegree * Math.Abs(FloorAngle) * (uphill ? 1 : -1);
            }

            float maxAcceleration = Math.Abs(max - xVelAbs) / StepSeconds;
            acc = Math.Max(-maxAcceleration, Math.Min(maxAcceleration, acc));
            if (xVelAbs > max) {
                acc = -acc;
            }

            if (reverse) {
                Turnaround = false;
                if (Grounded) {
                    if (!Underwater && xVelAbs >= tuning.skiddingMinimumVelocity) {
                        Skidding = true;
                        FacingRight = sign == 1;
                    }

                    if (Skidding) {
                        if (OnSlippery) {
                            acc = tuning.skiddingIceDeceleration;
                        } else if (xVelAbs > maxArray[Math.Min(tuning.runSpeedStage, maxArray.Length - 1)]) {
                            acc = tuning.skiddingFastDeceleration;
                        } else {
                            acc = tuning.skiddingDeceleration;
                        }
                        slowTurnaroundFrames = 0;
                    } else if (OnSlippery) {
                        acc = tuning.slowTurnaroundIceAcceleration;
                    } else {
                        slowTurnaroundFrames = Math.Min(slowTurnaroundFrames + 1, tuning.slowTurnaroundAcceleration.Length - 1);
                        acc = tuning.slowTurnaroundAcceleration[slowTurnaroundFrames];
                    }
                } else {
                    acc = tuning.walkAcceleration[0] * tuning.airReverseMultiplier;
                }
            } else {
                slowTurnaroundFrames = 0;
                Skidding &= !Turnaround;
            }

            float newX = xVel + (acc * StepSeconds * direction);
            if ((xVel < max && newX > max) || (xVel > -max && newX < -max)) {
                newX = Math.Max(-max, Math.Min(max, newX));
            }

            if (Skidding && !Turnaround && (Sign(newX) != sign || xVelAbs < SmallSpeed)) {
                fastTurnaroundFrames = tuning.fastTurnaroundFrames;
                newX = 0f;
            }

            VelocityX = newX;
        }

        private void Decelerate(float acc, int stage, bool uphill) {
            Skidding = false;
            Turnaround = false;

            float angle = Math.Abs(FloorAngle);
            if (Underwater) {
                acc = Crouching ? -tuning.walkAcceleration[0] : -tuning.swimDeceleration;
            } else if (Sliding) {
                if (angle > tuning.slideMinimumAngle) {
                    acc = (angle > 30f ? tuning.slideFastAcceleration : tuning.slideSlowAcceleration) * (uphill ? -1 : 1);
                } else {
                    acc = -tuning.walkAcceleration[0];
                }
            } else if (OnSlippery) {
                acc = -tuning.walkReleaseIceDeceleration[stage];
            } else {
                acc = -tuning.walkReleaseDeceleration;
            }

            int sign = Sign(VelocityX);
            float newX = VelocityX + (acc * StepSeconds * sign);
            float target = angle > 30f && OnSlideable ? Sign(FloorAngle) * tuning.walkMaxVelocity[0] : 0f;
            if ((sign == -1) ^ (newX <= target)) {
                newX = target;
            }

            if (Sliding) {
                newX = Math.Max(-tuning.slideMaxVelocity, Math.Min(tuning.slideMaxVelocity, newX));
            }

            VelocityX = newX;
            if (newX != 0f) {
                FacingRight = newX > 0f;
            }
        }

        private void StepJumping(bool wasGroundpounding) {
            if (Pressed(input.Jump, previousInput.Jump)) {
                jumpBufferFrames = tuning.jumpBufferFrames;
            }

            if (Grounded) {
                coyoteTimeFrames = tuning.coyoteTimeFrames;
            }

            if (!WasGrounded && Grounded) {
                landedFrame = frame;
                if (JumpState == NsmbJumpState.Triple && !input.Left && !input.Right) {
                    VelocityX = 0f;
                }
                if (previousJumpState != NsmbJumpState.None && previousJumpState == JumpState) {
                    JumpState = NsmbJumpState.None;
                }
                if (Math.Abs(VelocityX) < SmallSpeed && !wasGroundpounding) {
                    LandedStill();
                }
                previousJumpState = JumpState;
            }

            bool doJump = jumpBufferFrames > 0 && (Grounded || coyoteTimeFrames > 0);
            Decrement(ref coyoteTimeFrames);
            Decrement(ref jumpBufferFrames);

            if (Underwater || !doJump || Wallsliding) {
                return;
            }

            bool topSpeed = Math.Abs(VelocityX) >= tuning.walkMaxVelocity[tuning.runSpeedStage] - 0.1f;
            bool canSpecialJump = topSpeed
                && !input.Down
                && JumpState != NsmbJumpState.None
                && frame - landedFrame < tuning.specialJumpLandingFrames
                && JumpState != NsmbJumpState.Triple
                && !Crouching
                && VelocityX < 0f != FacingRight;

            Skidding = false;
            Turnaround = false;
            Sliding = false;
            wallslideEndFrames = 0;
            Groundpounding = false;
            groundpoundStartFrames = 0;
            jumpBufferFrames = 0;
            coyoteTimeFrames = 0;
            WasGrounded = false;
            Grounded = false;

            float walkSpeed = tuning.walkMaxVelocity[1];
            float alpha = Clamp01(Math.Abs(VelocityX) - walkSpeed + (walkSpeed * 0.5f));
            float newY = tuning.jumpVelocity + (tuning.jumpSpeedBonusVelocity * alpha);
            if (Sign(VelocityX) != 0 && Sign(VelocityX) != Sign(FloorAngle)) {
                newY += Math.Abs(FloorAngle) * 0.01f * alpha;
            }

            if (canSpecialJump && JumpState == NsmbJumpState.Single) {
                JumpState = NsmbJumpState.Double;
            } else if (canSpecialJump && JumpState == NsmbJumpState.Double) {
                JumpState = NsmbJumpState.Triple;
                newY += tuning.jumpTripleBonusVelocity;
            } else {
                JumpState = NsmbJumpState.Single;
            }

            VelocityY = newY;
        }

        private void StepSwimming() {
            if (!Underwater) {
                return;
            }

            WallslideLeft = false;
            WallslideRight = false;
            Crouching |= Grounded && Sliding;
            Sliding = false;
            Skidding = false;
            Turnaround = false;
            JumpState = NsmbJumpState.None;

            if (jumpBufferFrames > 0) {
                if (Grounded) {
                    VelocityY = tuning.swimJumpVelocity * 0.75f;
                }
                VelocityY += tuning.swimJumpVelocity;
                Grounded = false;
                jumpBufferFrames = 0;
                Crouching = false;
                Paddled();
            }
        }

        private void StepWallslide() {
            if (Groundpounding || Crouching || Underwater) {
                return;
            }

            int wall;
            if (WallslideLeft) {
                wall = -1;
            } else if (WallslideRight) {
                wall = 1;
            } else if (input.Left ^ input.Right) {
                wall = input.Left ? -1 : 1;
            } else {
                return;
            }

            if (Wallsliding) {
                StepWallslideStopChecks(wall);
            }

            if (wallslideEndFrames > 0 && Decrement(ref wallslideEndFrames)) {
                FacingRight = WallslideRight;
                WallslideRight = false;
                WallslideLeft = false;
                return;
            }

            if (Wallsliding) {
                VelocityX = Math.Max(-0.25f, Math.Min(0.25f, VelocityX));
                FacingRight = WallslideLeft;
                if (jumpBufferFrames > 0 && walljumpFrames == 0) {
                    VelocityX = tuning.walljumpHorizontalVelocity * (WallslideLeft ? 1 : -1);
                    VelocityY = tuning.walljumpVerticalVelocity;
                    JumpState = NsmbJumpState.Single;
                    Grounded = false;
                    walljumpFrames = tuning.walljumpFrames;
                    WallslideRight = false;
                    WallslideLeft = false;
                    wallslideEndFrames = 0;
                    jumpBufferFrames = 0;
                    Walljumped();
                }
            } else if (TouchingLeftWall || TouchingRightWall) {
                bool canWallslide = VelocityY < -0.1f && !Grounded && !Sliding && walljumpFrames == 0 && wallslideEndFrames == 0;
                if (!canWallslide || (wall == 1) != FacingRight) {
                    return;
                }

                WallslideRight = wall == 1 && TouchingRightWall;
                WallslideLeft = wall == -1 && TouchingLeftWall;
                wallslideEndFrames = 0;
            }
        }

        private void StepWallslideStopChecks(int wall) {
            if (Grounded || VelocityY >= 0f) {
                WallslideRight = false;
                WallslideLeft = false;
                wallslideEndFrames = 0;
                return;
            }

            bool held = wall < 0 ? input.Left && TouchingLeftWall : input.Right && TouchingRightWall;
            if (held) {
                wallslideEndFrames = 0;
            } else if (wallslideEndFrames == 0) {
                wallslideEndFrames = tuning.wallslideEndFrames;
                X -= wall * 0.32f;
            }
        }

        private void StepGravity() {
            float[] accelerations = tuning.gravityAcceleration;
            if (Grounded && !Underwater) {
                gravity = accelerations[0];
                return;
            }

            if ((Groundpounding && !Underwater) || Grounded || coyoteTimeFrames > 0) {
                gravity = groundpoundStartFrames > 0 ? tuning.gravityGroundpoundStart : accelerations[accelerations.Length - 1];
                return;
            }

            if (Underwater) {
                accelerations = tuning.gravitySwimmingAcceleration;
            }

            int stage = GravityStage();
            gravity = accelerations[stage];
            if (stage == 0 && !input.Jump && !Underwater) {
                gravity = accelerations[accelerations.Length - 1];
            }
        }

        private void StepTerminalVelocity() {
            if (Underwater && !Groundpounding) {
                terminalVelocity = input.Jump ? tuning.swimTerminalVelocityHeld : tuning.swimTerminalVelocity;
                VelocityY = Math.Min(VelocityY, tuning.swimMaxVerticalVelocity);
            } else if (Wallsliding) {
                terminalVelocity = tuning.terminalVelocityWallslide;
            } else if (Groundpounding) {
                terminalVelocity = tuning.terminalVelocityGroundpound;
                VelocityX = 0f;
            } else {
                terminalVelocity = tuning.terminalVelocity;
            }
        }

        private void StepFacingDirection() {
            if (walljumpFrames > 0 || (Groundpounding && !Grounded) || (Crouching && Grounded && Math.Abs(VelocityX) > SmallSpeed)) {
                return;
            }

            bool rightOrLeft = input.Right ^ input.Left;
            if (!Sliding && !Skidding && !Turnaround) {
                if (rightOrLeft) {
                    FacingRight = input.Right;
                }
            } else if (!Skidding && !Turnaround) {
                if (((Math.Abs(VelocityX) < 0.5f && Crouching) || OnSlippery) && rightOrLeft) {
                    FacingRight = input.Right;
                } else if (Grounded && Math.Abs(VelocityX) > SmallSpeed && !Crouching) {
                    FacingRight = VelocityX > 0f;
                } else if (rightOrLeft) {
                    FacingRight = input.Right;
                }
            }
        }

        private void Move() {
            WasGrounded = Grounded;
            MoveX();
            MoveY();
            TouchingLeftWall = WallAt(X - tuning.halfWidth - tuning.wallProbe);
            TouchingRightWall = WallAt(X + tuning.halfWidth + tuning.wallProbe);
            UpdateFloor();
            Underwater = tiles.GetTileAt(X, Y - (tuning.height / 2f)).IsWater;
        }

        private void ApplyGravity() {
            if (Decrement(ref hoverFrames)) {
                VelocityY += gravity * StepSeconds;
            }
            VelocityY = Math.Max(VelocityY, terminalVelocity);
        }

        private void MoveX() {
            float speed = SpeedX;
            X += speed;
            if (speed == 0f) {
                return;
            }

            float edge = X + (speed > 0f ? tuning.halfWidth : -tuning.halfWidth);
            if (!WallAt(edge)) {
                return;
            }

            float column = (float) Math.Floor(edge / NsmbTileSource.TileSize) * NsmbTileSource.TileSize;
            X = speed > 0f ? column - tuning.halfWidth : column + NsmbTileSource.TileSize + tuning.halfWidth;
            VelocityX = 0f;
        }

        private bool WallAt(float x) {
            float lowest = Y - (Grounded ? tuning.groundSnap : 2f);
            return tiles.GetTileAt(x, lowest).IsFullSolid
                || tiles.GetTileAt(x, Y - (tuning.height / 2f)).IsFullSolid
                || tiles.GetTileAt(x, Y - tuning.height + 1f).IsFullSolid;
        }

        private void MoveY() {
            if (Grounded && VelocityY <= 0f) {
                float snapped = FindGround(Y - tuning.groundSnap, Y + tuning.groundSnap);
                if (snapped < float.MaxValue) {
                    Y = snapped;
                    VelocityY = 0f;
                    return;
                }

                Grounded = false;
                VelocityY = 0f;
                hoverFrames = tuning.hoverFrames;
                return;
            }

            float previousY = Y;
            Y += SpeedY;

            if (VelocityY > 0f) {
                float head = Y - tuning.height;
                foreach (float bodyX in new[] { X - tuning.halfWidth + 1f, X + tuning.halfWidth - 1f }) {
                    NsmbTileBehavior tile = tiles.GetTileAt(bodyX, head);
                    if (tile.IsFullSolid || tile.IsFlippedSlope) {
                        Y = ((float) Math.Floor(head / NsmbTileSource.TileSize) * NsmbTileSource.TileSize) + NsmbTileSource.TileSize + tuning.height;
                        VelocityY = 0f;
                        break;
                    }
                }
                return;
            }

            float landing = FindGround(previousY, Y);
            if (landing < float.MaxValue) {
                Y = landing;
                VelocityY = 0f;
                Grounded = true;
            }
        }

        /// <summary>Highest ground surface between two heights under the feet, or float.MaxValue.</summary>
        private float FindGround(float fromY, float toY) {
            // A slope under the centre wins: the solid tiles under its edges are not ground.
            float best = SurfaceBelow(X, fromY, toY, true, out bool onSlope);
            if (onSlope) {
                return best;
            }

            best = Math.Min(best, SurfaceBelow(X - tuning.halfWidth + 1f, fromY, toY, false, out _));
            return Math.Min(best, SurfaceBelow(X + tuning.halfWidth - 1f, fromY, toY, false, out _));
        }

        private float SurfaceBelow(float x, float fromY, float toY, bool usesSlopes, out bool onSlope) {
            onSlope = false;
            int firstRow = (int) Math.Floor(fromY / NsmbTileSource.TileSize);
            int lastRow = (int) Math.Floor(toY / NsmbTileSource.TileSize);
            int column = (int) Math.Floor(x / NsmbTileSource.TileSize);
            for (int row = firstRow; row <= lastRow; row++) {
                NsmbTileBehavior tile = tiles.GetTile(column, row);
                float top = row * (float) NsmbTileSource.TileSize;
                if (tile.IsSlope && !tile.IsFlippedSlope) {
                    if (!usesSlopes) {
                        continue;
                    }
                    float surface = top + NsmbTileSource.TileSize - tile.SlopeHeight(x - (column * NsmbTileSource.TileSize));
                    if (surface >= fromY && surface <= toY) {
                        onSlope = true;
                        return surface;
                    }
                } else if ((tile.IsFullSolid || tile.IsSolidOnTop) && top >= fromY - 0.01f && top <= toY) {
                    return top;
                }
            }
            return float.MaxValue;
        }

        private void UpdateFloor() {
            FloorAngle = 0f;
            OnSlideable = false;
            OnSlippery = false;
            if (!Grounded) {
                return;
            }

            NsmbTileBehavior tile = tiles.GetTileAt(X, Y - 0.5f);
            if (!tile.IsSlope || tile.IsFlippedSlope) {
                tile = tiles.GetTileAt(X, Y + 0.5f);
            }

            OnSlippery = tile.SubType == NsmbTileBehavior.SubTypeIce;
            if (tile.IsSlope && !tile.IsFlippedSlope) {
                float drop = tile.SlopeHeight(0f) - tile.SlopeHeight(NsmbTileSource.TileSize);
                FloorAngle = (float) (Math.Atan2(drop, NsmbTileSource.TileSize) * 180f / Math.PI);
                OnSlideable = tile.SubType != NsmbTileBehavior.SubTypeNoSliding;
            }
        }
    }
}

using System;

namespace NsmbDs {
    /// <summary>Tunable movement values, copied from the base project's MarioPhysics asset. Speeds are units per second, y up.</summary>
    [Serializable]
    public sealed class NsmbPlayerTuning {
        // Measured from the running game, in DS pixels and frames.
        public float maxWalkSpeed = 1.5f;
        public float maxRunSpeed = 3f;
        public float startAcceleration = 0.0703125f;
        public float startAccelerationBelow = 0.5625f;
        public float walkAccelerationPx = 0.03515625f;
        public float runAcceleration = 0.04296875f;
        public float runAccelerationMid = 0.03125f;
        public float runAccelerationHigh = 0.0234375f;
        public float runMidSpeed = 1.5f;
        public float runHighSpeed = 2.25f;
        public float brakeDeceleration = 0.03515625f;
        public float skidDeceleration = 0.09375f;
        public float skidStopSpeed = 1.5f;
        public int turnPauseFrames = 9;
        public float turnAcceleration = 0.25f;
        public float turnBoostUntil = 1.75f;
        public float jumpSpeedStanding = 3.59375f;
        public float jumpSpeedMid = 3.71875f;
        public float jumpSpeedWalking = 3.84375f;
        public float jumpMidFrom = 1f;
        public float jumpWalkFrom = 1.5f;
        public float jumpRunFrom = 3f;
        public float ledgeDrop = 2.34375f;
        public float ledgeHoverSpeed = -0.64453125f;
        public int ledgeHoverFrames = 5;
        public int turnStartFrames = 3;
        public float wallPushSpeed = 1.875f;

        // Speed limits on slopes, measured at walk and run. Steep is 45 degrees, shallow 2:1.
        public float steepAngle = 30f;
        public float steepUpWalk = 0.75f;
        public float steepUpRun = 1.5f;
        public float steepDownWalk = 2.15625f;
        public float steepDownRun = 4.3125f;
        public float shallowUpWalk = 1.03125f;
        public float shallowUpRun = 2.0625f;
        public float shallowDownWalk = 1.8f;
        public float shallowDownRun = 3.5f;
        public float slopeUpDeceleration = 0.05859375f;
        public float shallowUpAcceleration = 0.046875f;
        public float steepDownAcceleration = 0.125f;
        public float shallowDownAcceleration = 0.0625f;
        public float overspeedBrake = 0.03125f;
        public float jumpSpeedRunning = 3.875f;
        public float gravityPx = 0.34375f;
        public float gravityRisingHeld = 0.0625f;
        public float gravityRisingMid = 0.25f;
        public float gravityFalling = 0.25f;
        public float heldFastAbove = 2.5f;
        public float heldMidAbove = 1.5f;
        public float fallSlowBelow = 2f;

        // Tile sensors of small Mario, from the sensor tables in memory.
        public float feetLeft = -5f;
        public float feetRight = 4f;
        public float headLeft = -2f;
        public float headRight = 1f;
        public float sideLow = 4f;
        public float sideHigh = 11f;

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

        public int jumpBufferFrames = 1;
        public int coyoteTimeFrames = 1;
        public int specialJumpLandingFrames = 12;
        public float jumpVelocity = 6.62109375f;
        public float jumpSpeedBonusVelocity = 0.46875f;
        public float jumpTripleBonusVelocity = 0.5f;
        public float walljumpHorizontalVelocity = 4.21875f;
        public float walljumpVerticalVelocity = 6.4453125f;
        public int walljumpFrames = 1;
        public int wallslideEndFrames = 16;

        public float[] gravityVelocity = { 4.16015625f, 2.109375f, 0f, -5.859375f };
        public float[] gravityAcceleration = { -7.03125f, -28.125f, -38.671875f, -28.125f, -38.671875f };
        public float[] gravitySwimmingVelocity = { 0f };
        public float[] gravitySwimmingAcceleration = { -4.833984375f, -3.076171875f };
        public float gravityGroundpoundStart = 0f;
        public float terminalVelocity = -7.5f;
        public float terminalVelocityWallslide = -4.6875f;
        public float terminalVelocityGroundpound = -11.25f;

        public bool groundpoundWhileHoldingSideways = true;
        public int groundpoundInputDelayFrames = 5;
        public int groundpoundStartFrames = 17;
        public float groundpoundStartVelocity = 1.875f;
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

        public float halfWidth = 7f;
        public float sideLeft = 8f;
        public float height = 16f;
        public float bigHeight = 32f;
        public float liquidEntrySpeedLimit = -5.625f;
        public float liquidEntrySpeed = -1.875f;
        public float liquidEntryDrag = 10.546875f;
        public int liquidEntryFrames = 9;
        public float liquidBodyCenter = 8f;
        public float groundSnap = 8f;
        public int hoverFrames = 0;
        public float wallProbe = 1f;
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
        private const int GroundpoundHoldFrames = 6;

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
        private int liquidEntryLeft;
        private int turnPause;
        private bool turnBoost;
        private bool stopAfterMove;
        private bool jumpHeldSinceTakeoff;
        private bool tookOff;
        private int turnStartLeft;
        private bool walljumpPending;
        private bool walljumpLocked;

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
        public NsmbPower Power;
        public NsmbLiquidKind Liquid;

        /// <summary>Pixels, y growing downwards. Below this the liquid is the player's problem.</summary>
        public float LiquidSurfaceY = float.MaxValue;

        public event Action LandedStill = delegate { };
        public event Action Walljumped = delegate { };
        public event Action Paddled = delegate { };

        /// <summary>A tile was hit from below: column, row.</summary>
        public event Action<int, int> HeadHit = delegate { };

        /// <summary>A ground pound landed on a tile: column, row.</summary>
        public event Action<int, int> Pounded = delegate { };

        /// <summary>Touching lava or poison.</summary>
        public event Action KilledByLiquid = delegate { };

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
        public NsmbTileSource Tiles => tiles;
        public bool IsBig => Power != NsmbPower.Small && Power != NsmbPower.Mini;

        /// <summary>Body height in pixels for the current power and pose.</summary>
        public float BodyHeight {
            get {
                float height = IsBig ? tuning.bigHeight : tuning.height;
                return IsBig && (Crouching || Sliding) ? height / 2f : height;
            }
        }

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
            turnPause = 0;
            turnBoost = false;
            stopAfterMove = false;
            jumpHeldSinceTakeoff = false;
            walljumpPending = false;
            walljumpLocked = false;
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

            // Gravity comes before moving, except on the frame a jump or kick set the speed.
            if (!tookOff) {
                ApplyGravity();
            }
            tookOff = false;
            Move();
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
                >= GroundpoundHoldFrames => tuning.groundpoundStartVelocity,
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

            // After a wall jump the sideways speed stays until landing.
            walljumpLocked &= !Grounded;
            if (walljumpLocked) {
                return;
            }

            if (!Underwater && !Sliding) {
                StepMeasuredSpeed();
                return;
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

        // The walking and running rules measured from the game; replays stay within a pixel of it.
        private void StepMeasuredSpeed() {
            float speedX = SpeedX;
            int direction = Crouching && Grounded ? 0 : (input.Right ? 1 : 0) - (input.Left ? 1 : 0);

            // Standing and pressing the way Mario is not facing: he turns before moving.
            if (Grounded && speedX == 0f && direction != 0 && direction > 0 != FacingRight && turnPause == 0) {
                turnStartLeft = tuning.turnStartFrames;
            }
            if (turnStartLeft > 0) {
                turnStartLeft--;
                direction = 0;
            }

            if (!Grounded && ((direction > 0 && TouchingRightWall) || (direction < 0 && TouchingLeftWall))) {
                speedX = 0f;
            }

            if (turnPause > 0) {
                turnPause--;
                speedX = 0f;
                if (turnPause == 0 && direction != 0) {
                    speedX = direction * tuning.turnAcceleration;
                    turnBoost = true;
                }
            } else if (direction == 0) {
                Skidding = false;
                turnBoost = false;
                speedX = speedX > 0f ? Math.Max(0f, speedX - tuning.brakeDeceleration) : Math.Min(0f, speedX + tuning.brakeDeceleration);
            } else if (speedX * direction < 0f) {
                float speed = Math.Abs(speedX);
                Skidding |= speed > tuning.skidStopSpeed;
                float stopAt = input.Run ? tuning.skidDeceleration : tuning.skidStopSpeed;
                if (Skidding && speed <= stopAt) {
                    speedX = Math.Sign(speedX) * (speed - tuning.skidDeceleration);
                    stopAfterMove = true;
                    Skidding = false;
                    turnPause = Grounded ? tuning.turnPauseFrames : 0;
                } else {
                    speedX = Math.Sign(speedX) * Math.Max(0f, speed - tuning.skidDeceleration);
                }
            } else if (turnBoost) {
                float boosted = Math.Abs(speedX) + tuning.turnAcceleration;
                turnBoost = boosted < tuning.turnBoostUntil;
                speedX = direction * Math.Min(boosted, tuning.turnBoostUntil);
            } else {
                float limit = input.Run ? tuning.maxRunSpeed : tuning.maxWalkSpeed;
                float acceleration = 0f;
                float brake = tuning.overspeedBrake;
                bool onSlope = Grounded && FloorAngle != 0f;
                if (onSlope) {
                    bool steep = Math.Abs(FloorAngle) >= tuning.steepAngle;
                    if (Sign(FloorAngle) != direction) {
                        limit = steep ? (input.Run ? tuning.steepUpRun : tuning.steepUpWalk) : (input.Run ? tuning.shallowUpRun : tuning.shallowUpWalk);
                        brake = tuning.slopeUpDeceleration;
                        acceleration = steep ? 0f : tuning.shallowUpAcceleration;
                    } else {
                        limit = steep ? (input.Run ? tuning.steepDownRun : tuning.steepDownWalk) : (input.Run ? tuning.shallowDownRun : tuning.shallowDownWalk);
                        acceleration = steep ? tuning.steepDownAcceleration : tuning.shallowDownAcceleration;
                    }
                }

                float current = Math.Abs(speedX);
                if (current > limit) {
                    // Running off a slope drops straight to the run limit.
                    speedX = direction * (input.Run && !onSlope ? limit : Math.Max(limit, current - brake));
                } else {
                    float step = acceleration > 0f ? acceleration : MeasuredAcceleration(current);
                    speedX = direction * Math.Min(limit, current + step);
                }
            }

            VelocityX = speedX / PixelsPerFrame;
        }

        private float MeasuredAcceleration(float speed) {
            if (speed < tuning.startAccelerationBelow) {
                return tuning.startAcceleration;
            }
            if (!input.Run) {
                return tuning.walkAccelerationPx;
            }
            if (speed < tuning.runMidSpeed) {
                return tuning.runAcceleration;
            }
            return speed < tuning.runHighSpeed ? tuning.runAccelerationMid : tuning.runAccelerationHigh;
        }

        private float MeasuredJumpSpeed(float speed) {
            if (speed < tuning.jumpMidFrom) {
                return tuning.jumpSpeedStanding;
            }
            if (speed < tuning.jumpWalkFrom) {
                return tuning.jumpSpeedMid;
            }
            return speed < tuning.jumpRunFrom ? tuning.jumpSpeedWalking : tuning.jumpSpeedRunning;
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

            float newY = MeasuredJumpSpeed(Math.Abs(SpeedX)) / PixelsPerFrame;
            jumpHeldSinceTakeoff = true;

            if (canSpecialJump && JumpState == NsmbJumpState.Single) {
                JumpState = NsmbJumpState.Double;
            } else if (canSpecialJump && JumpState == NsmbJumpState.Double) {
                JumpState = NsmbJumpState.Triple;
                newY += tuning.jumpTripleBonusVelocity;
            } else {
                JumpState = NsmbJumpState.Single;
            }

            VelocityY = newY;
            tookOff = true;
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

            if (Pressed(input.Jump, previousInput.Jump)) {
                VelocityY += tuning.swimJumpVelocity;
                tookOff = true;
                Grounded = false;
                jumpBufferFrames = 0;
                Crouching = false;
                Paddled();
            }
        }

        private void StepWallslide() {
            if (walljumpPending) {
                walljumpPending = false;
                PerformWalljump();
                return;
            }

            if (Groundpounding || Crouching || Underwater) {
                return;
            }

            // The game takes the jump a frame after the press.
            if (Wallsliding && Pressed(input.Jump, previousInput.Jump)) {
                walljumpPending = true;
                VelocityY = 0f;
                tookOff = true;
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
            } else if ((TouchingLeftWall || TouchingRightWall) && !WallIsPipe(wall)) {
                bool canWallslide = VelocityY < -0.1f && !Grounded && !Sliding && walljumpFrames == 0 && wallslideEndFrames == 0;
                if (!canWallslide || (wall == 1) != FacingRight) {
                    return;
                }

                WallslideRight = wall == 1 && TouchingRightWall;
                WallslideLeft = wall == -1 && TouchingLeftWall;
                wallslideEndFrames = 0;
            }
        }

        private void PerformWalljump() {
            VelocityX = tuning.walljumpHorizontalVelocity * (WallslideLeft ? 1 : -1);
            FacingRight = WallslideLeft;
            VelocityY = tuning.walljumpVerticalVelocity;
            JumpState = NsmbJumpState.Single;
            Grounded = false;
            tookOff = true;
            jumpHeldSinceTakeoff = true;
            walljumpLocked = true;
            WallslideRight = false;
            WallslideLeft = false;
            wallslideEndFrames = 0;
            Walljumped();
        }

        // Pipes cannot be slid down.
        private bool WallIsPipe(int wall) {
            float x = X + (wall > 0 ? tuning.halfWidth + tuning.wallProbe : -tuning.sideLeft - tuning.wallProbe);
            return tiles.GetTileAt(x, Y - tuning.sideHigh).IsPipe;
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
            if (Underwater && !Groundpounding) {
                accelerations = tuning.gravitySwimmingAcceleration;
                gravity = accelerations[GravityStage()];
                return;
            }

            if (Grounded && !Underwater) {
                gravity = 0f;
                return;
            }

            if (Groundpounding && !Underwater) {
                gravity = groundpoundStartFrames > 0 ? tuning.gravityGroundpoundStart : accelerations[accelerations.Length - 1];
                return;
            }

            if (!input.Jump) {
                jumpHeldSinceTakeoff = false;
            }

            float pull = tuning.gravityPx;
            float speedY = SpeedY;
            if (speedY < 0f) {
                if (jumpHeldSinceTakeoff && -speedY > tuning.heldFastAbove) {
                    pull = tuning.gravityRisingHeld;
                } else if (jumpHeldSinceTakeoff && -speedY > tuning.heldMidAbove) {
                    pull = tuning.gravityRisingMid;
                }
            } else if (speedY < tuning.fallSlowBelow) {
                pull = tuning.gravityFalling;
            }
            gravity = -pull / PixelsPerFrame / StepSeconds;
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
            if (stopAfterMove) {
                VelocityX = 0f;
                stopAfterMove = false;
            }
            TouchingLeftWall = WallAt(X - tuning.sideLeft - tuning.wallProbe);
            TouchingRightWall = WallAt(X + tuning.halfWidth + tuning.wallProbe);
            UpdateFloor();
            UpdateLiquid();
        }

        private void UpdateLiquid() {
            bool inLiquid = Liquid != NsmbLiquidKind.None && Y >= LiquidSurfaceY;
            if (Liquid != NsmbLiquidKind.Water) {
                Underwater = false;
                if (inLiquid) {
                    KilledByLiquid();
                }
                return;
            }

            bool wasUnderwater = Underwater;
            Underwater = Y - (wasUnderwater ? 0f : tuning.liquidBodyCenter) >= LiquidSurfaceY;
            if (Underwater && !wasUnderwater && VelocityY < tuning.liquidEntrySpeedLimit) {
                VelocityY = tuning.liquidEntrySpeed;
                liquidEntryLeft = tuning.liquidEntryFrames;
            }
            if (Underwater && !wasUnderwater) {
                float limit = tuning.swimMaxVelocity[tuning.swimMaxVelocity.Length - 1];
                VelocityX = Math.Max(-limit, Math.Min(limit, VelocityX));
            }
        }

        private void ApplyGravity() {
            if (liquidEntryLeft > 0) {
                liquidEntryLeft--;
                VelocityY += tuning.liquidEntryDrag * StepSeconds;
            } else if (Decrement(ref hoverFrames)) {
                VelocityY += gravity * StepSeconds;
            }
            VelocityY = Math.Max(VelocityY, terminalVelocity);
        }

        private void MoveX() {
            // Speed runs along the slope, so less of it goes sideways.
            float along = Grounded && FloorAngle != 0f ? (float) Math.Cos(FloorAngle * Math.PI / 180f) : 1f;
            float speed = SpeedX * along;
            float before = X;
            X += speed;
            if (speed == 0f) {
                return;
            }

            float edge = X + (speed > 0f ? tuning.halfWidth : -tuning.sideLeft);
            if (!WallAt(edge)) {
                return;
            }

            float column = (float) Math.Floor(edge / NsmbTileSource.TileSize) * NsmbTileSource.TileSize;
            float clear = speed > 0f ? column - tuning.halfWidth : column + NsmbTileSource.TileSize + tuning.sideLeft;

            // Already inside the wall, as after rising into a block's side: pushed out over a few frames.
            bool wasInside = speed > 0f ? before > clear : before < clear;
            if (wasInside) {
                X = before + Math.Max(-tuning.wallPushSpeed, Math.Min(tuning.wallPushSpeed, clear - before));
            } else {
                // In the air the step is cancelled; on the ground Mario ends up against the wall.
                X = Grounded ? clear : before;
            }
            VelocityX = Grounded ? 0f : Math.Sign(speed) * tuning.startAcceleration / PixelsPerFrame;
        }

        private bool WallAt(float x) {
            float scale = BodyHeight / tuning.height;
            bool lowSensor = !(Grounded && FloorAngle != 0f) && tiles.GetTileAt(x, Y - (tuning.sideLow * scale)).IsFullSolid;
            return lowSensor || tiles.GetTileAt(x, Y - (tuning.sideHigh * scale)).IsFullSolid;
        }

        private void MoveY() {
            if (Grounded && VelocityY <= 0f) {
                float snapped = FindGround(Y - tuning.groundSnap, Y + tuning.groundSnap);
                if (snapped < float.MaxValue) {
                    Y = snapped;
                    VelocityY = 0f;
                    return;
                }

                // The ground press carries over for a frame, then Mario floats briefly.
                Grounded = false;
                Y += tuning.ledgeDrop;
                VelocityY = tuning.ledgeHoverSpeed;
                hoverFrames = tuning.ledgeHoverFrames;
                return;
            }

            float previousY = Y;
            Y += SpeedY;

            if (VelocityY > 0f) {
                HitCeiling();
                return;
            }

            float landing = FindGround(previousY, Y);
            if (landing < float.MaxValue) {
                Y = landing;
                VelocityY = 0f;
                Grounded = true;
                if (Groundpounding) {
                    Pounded((int) Math.Floor(X / NsmbTileSource.TileSize), (int) Math.Floor(Y / NsmbTileSource.TileSize));
                }
            }
        }

        private void HitCeiling() {
            float height = BodyHeight;
            float head = Y - height;
            int row = (int) Math.Floor(head / NsmbTileSource.TileSize);

            // The column under the middle of the head counts first, as that is the block the player sees being hit.
            foreach (float bodyX in new[] { X + tuning.headRight, X + tuning.headLeft }) {
                NsmbTileBehavior tile = tiles.GetTileAt(bodyX, head);
                if (tile.IsFullSolid || tile.IsFlippedSlope || tile.IsHidden) {
                    Y = (row * (float) NsmbTileSource.TileSize) + NsmbTileSource.TileSize + height;
                    VelocityY = 0f;
                    HeadHit((int) Math.Floor(bodyX / NsmbTileSource.TileSize), row);
                    return;
                }
            }
        }

        /// <summary>Highest ground surface between two heights under the feet, or float.MaxValue.</summary>
        private float FindGround(float fromY, float toY) {
            // A slope under the centre wins: the solid tiles under its edges are not ground.
            float best = SurfaceBelow(X, fromY, toY, true, out bool onSlope);
            if (onSlope) {
                return best;
            }

            best = Math.Min(best, SurfaceBelow(X + tuning.feetLeft, fromY, toY, false, out _));
            return Math.Min(best, SurfaceBelow(X + tuning.feetRight, fromY, toY, false, out _));
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

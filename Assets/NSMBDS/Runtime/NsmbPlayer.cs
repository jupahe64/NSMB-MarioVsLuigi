using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NsmbDs {
    /// <summary>Runs Mario's motor at 60 Hz and handles warps, the pit and the camera.</summary>
    public sealed class NsmbPlayer : MonoBehaviour {
        private const float StepSeconds = NsmbUnits.StepSeconds;
        private const int MaxStepsPerFrame = 4;
        private const float ScreenWidth = 256f;
        private const float ScreenHeight = 192f;
        private const int WarpFrames = 32;
        private const float PitMargin = 48f;
        private const float ViewProbe = 0.25f;
        private const float DepthNormal = -4f;
        private const float DepthInPipe = 1f;

        private enum Warp { None, Entering, Leaving }

        public NsmbPlayerTuning tuning = new();
        public bool allowMiniPipes = true;
        public bool keepInsideArea = true;

        /// <summary>Replaces device input while set, for automated checks.</summary>
        public NsmbPlayerInput? ScriptedInput;

        private NsmbArea area;
        private NsmbPlayerMotor motor;
        private NsmbTileSource tiles;
        private NsmbEntranceMarker[] entrances;
        private NsmbRegionMarker[] regions;
        private Camera view;
        private NsmbRegionMarker currentView;
        private NsmbPower previousPower;
        private float pendingTime;
        private NsmbPlayerInput input;
        private Warp warp;
        private int warpFrames;
        private Vector2 warpStep;
        private NsmbEntranceMarker warpEntrance;

        /// <summary>Asks the owner to load an area and place the player at an entrance.</summary>
        public event Action<int, int> TravelRequested;

        /// <summary>After each 60 Hz step of the motor.</summary>
        public event Action FrameStepped;

        public NsmbPlayerMotor Motor => motor;
        public NsmbPlayerInput CurrentInput => input;
        public float X => motor?.X ?? 0f;
        public float Y => motor?.Y ?? 0f;
        public float SpeedX => motor?.SpeedX ?? 0f;
        public float SpeedY => motor?.SpeedY ?? 0f;
        public bool Grounded => motor != null && motor.Grounded;
        public bool InWarp => warp != Warp.None;
        public string AreaName => area != null ? area.areaName : "";
        public NsmbArea Area => area;
        public NsmbTile UsedBlock { get; set; }
        public int Coins { get; set; }
        public int ExtraLives { get; set; }

        public void Teleport(float x, float y) {
            motor.Place(x, y);
            UpdateTransform();
        }

        public uint TileUnderFeet => tiles?.GetTileAt(X, Y + 1f).Word ?? 0u;

        /// <summary>Stops the simulation before the current area unloads.</summary>
        public void LeaveArea() {
            previousPower = motor?.Power ?? previousPower;
            motor = null;
            tiles = null;
            warp = Warp.None;
        }

        public void EnterArea(NsmbArea newArea, int entranceNumber) {
            area = newArea;
            view = Camera.main;
            tiles = new NsmbTilemapSource(area.tilemap);
            NsmbPower power = motor?.Power ?? previousPower;
            motor = new NsmbPlayerMotor(tuning, tiles) { Power = power };
            motor.KilledByLiquid += Die;
            entrances = area.GetComponentsInChildren<NsmbEntranceMarker>();
            regions = area.GetComponentsInChildren<NsmbRegionMarker>();
            currentView = null;
            warp = Warp.None;

            NsmbEntranceMarker entrance = area.FindEntrance(entranceNumber) ?? area.FindEntrance(area.startEntrance);
            if (entrance != null) {
                PlaceAt(entrance);
            }
            UpdateTransform();
        }

        private void PlaceAt(NsmbEntranceMarker entrance) {
            (float x, float y) = EntrancePixels(entrance);
            NsmbWarp warpData = new(entrance.type, x, y);
            (float exitX, float exitY) = warpData.ExitPosition(tiles, tuning.halfWidth, motor.BodyHeight);
            (float stepX, float stepY) = warpData.EnterDirection;
            if (stepX == 0f && stepY == 0f) {
                motor.Place(exitX, exitY);
                return;
            }

            // The slide out runs against the way in.
            motor.Place(exitX + (stepX * WarpFrames), exitY + (stepY * WarpFrames));
            warp = Warp.Leaving;
            warpFrames = WarpFrames;
            warpStep = new Vector2(-stepX, -stepY);
        }

        private static (float X, float Y) EntrancePixels(NsmbEntranceMarker entrance) {
            Vector2 pixels = entrance.transform.position * NsmbUnits.PixelsPerUnit;
            return (pixels.x, -pixels.y);
        }

        private void Update() {
            if (motor == null) {
                return;
            }

            input = ScriptedInput ?? ReadInput();
            pendingTime = Mathf.Min(pendingTime + Time.deltaTime, StepSeconds * MaxStepsPerFrame);
            while (pendingTime >= StepSeconds) {
                pendingTime -= StepSeconds;
                StepFrame();

                // Finishing a warp can unload this area.
                if (motor == null) {
                    return;
                }
            }
            UpdateTransform();
            UpdateCamera();
        }

        private static NsmbPlayerInput ReadInput() {
            NsmbPlayerInput result = default;
            Keyboard keys = Keyboard.current;
            if (keys != null) {
                result.Left = keys.leftArrowKey.isPressed;
                result.Right = keys.rightArrowKey.isPressed;
                result.Up = keys.upArrowKey.isPressed;
                result.Down = keys.downArrowKey.isPressed;
                result.Jump = keys.zKey.isPressed;
                result.Run = keys.xKey.isPressed;
            }

            Gamepad pad = Gamepad.current;
            if (pad != null) {
                result.Left |= pad.dpad.left.isPressed || pad.leftStick.left.isPressed;
                result.Right |= pad.dpad.right.isPressed || pad.leftStick.right.isPressed;
                result.Up |= pad.dpad.up.isPressed || pad.leftStick.up.isPressed;
                result.Down |= pad.dpad.down.isPressed || pad.leftStick.down.isPressed;
                result.Jump |= pad.buttonSouth.isPressed;
                result.Run |= pad.buttonWest.isPressed;
            }
            return result;
        }

        /// <summary>Back to the start of the area until lives and game over exist.</summary>
        public void Die() {
            motor.Power = NsmbPower.Small;
            PlaceAt(area.FindEntrance(area.startEntrance));
        }

        private void StepFrame() {
            if (warp != Warp.None) {
                StepWarp();
                return;
            }

            motor.Step(input);
            FrameStepped?.Invoke();
            if (motor == null) {
                return;
            }

            TryEnterWarp();
            if (warp == Warp.None) {
                KeepInsideArea();
            }
            if (motor.Y > PitBottom() + PitMargin) {
                Die();
            }
        }

        private void StepWarp() {
            motor.X += warpStep.x;
            motor.Y += warpStep.y;
            if (--warpFrames > 0) {
                return;
            }

            Warp finished = warp;
            warp = Warp.None;
            if (finished == Warp.Entering) {
                NsmbEntranceMarker partner = ConnectedPartner(warpEntrance);
                if (partner != null) {
                    PlaceAt(partner);
                } else {
                    TravelRequested?.Invoke(warpEntrance.destArea, warpEntrance.destEntrance);
                }
            } else {
                motor.Place(motor.X, motor.Y);
            }
        }

        /// <summary>Connected pipes pair up by number inside an area; their destination fields only say which end they are.</summary>
        private NsmbEntranceMarker ConnectedPartner(NsmbEntranceMarker entrance) {
            if ((entrance.settings & NsmbWarp.ConnectedSetting) == 0) {
                return null;
            }

            foreach (NsmbEntranceMarker other in entrances) {
                if (other != entrance && (other.settings & NsmbWarp.ConnectedSetting) != 0 && other.connectedPipe == entrance.connectedPipe) {
                    return other;
                }
            }
            return null;
        }

        private void TryEnterWarp() {
            foreach (NsmbEntranceMarker entrance in entrances) {
                if ((entrance.settings & NsmbWarp.ExitOnlySetting) != 0) {
                    continue;
                }

                (float x, float y) = EntrancePixels(entrance);
                NsmbWarp candidate = new(entrance.type, x, y);
                if ((allowMiniPipes || !candidate.IsMini) && candidate.CanEnter(motor, input, motor.BodyHeight)) {
                    warpEntrance = entrance;
                    warp = Warp.Entering;
                    warpFrames = WarpFrames;
                    (float stepX, float stepY) = candidate.EnterDirection;
                    warpStep = new Vector2(stepX, stepY);
                    (motor.X, motor.Y) = candidate.EnterPosition(motor.X, motor.Y);
                    motor.Stop();
                    return;
                }
            }
        }

        private void KeepInsideArea() {
            NsmbRegionMarker region = CurrentView();
            if (!keepInsideArea || region == null) {
                return;
            }

            Rect rect = region.WorldRect;
            float y = Mathf.Clamp(-motor.Y / NsmbUnits.PixelsPerUnit, rect.yMin + ViewProbe, rect.yMax - ViewProbe);
            float left = (rect.xMin * NsmbUnits.PixelsPerUnit) + tuning.halfWidth;
            float right = (rect.xMax * NsmbUnits.PixelsPerUnit) - tuning.halfWidth;
            if (ViewAt(new Vector2(rect.xMin - ViewProbe, y)) != null) {
                left = float.MinValue;
            }
            if (ViewAt(new Vector2(rect.xMax + ViewProbe, y)) != null) {
                right = float.MaxValue;
            }
            motor.ClampX(left, right);
        }

        private float PitBottom() {
            NsmbRegionMarker region = CurrentView();
            return region != null ? -region.WorldRect.yMin * NsmbUnits.PixelsPerUnit : float.MaxValue;
        }

        private NsmbRegionMarker ViewAt(Vector2 position) {
            foreach (NsmbRegionMarker region in regions) {
                if (!region.isZone && region.WorldRect.Contains(position)) {
                    return region;
                }
            }
            return null;
        }

        /// <summary>The view holding the player; above or below it stays the same view, so the camera keeps following sideways.</summary>
        private NsmbRegionMarker CurrentView() {
            Vector2 position = transform.position;
            if (currentView != null) {
                Rect rect = currentView.WorldRect;
                if (rect.Contains(position) || (ViewAt(position) == null && position.x >= rect.xMin && position.x <= rect.xMax)) {
                    return currentView;
                }
            }

            currentView = ViewAt(position) ?? NearestView(position);
            return currentView;
        }

        private NsmbRegionMarker NearestView(Vector2 position) {
            NsmbRegionMarker nearest = null;
            float best = float.MaxValue;
            foreach (NsmbRegionMarker region in regions) {
                if (region.isZone) {
                    continue;
                }

                Rect rect = region.WorldRect;
                Vector2 closest = new(Mathf.Clamp(position.x, rect.xMin, rect.xMax), Mathf.Clamp(position.y, rect.yMin, rect.yMax));
                float distance = (closest - position).sqrMagnitude;
                if (distance < best) {
                    best = distance;
                    nearest = region;
                }
            }
            return nearest;
        }

        private void UpdateTransform() {
            float depth = InWarp ? DepthInPipe : DepthNormal;
            transform.position = new Vector3(motor.X / NsmbUnits.PixelsPerUnit, -motor.Y / NsmbUnits.PixelsPerUnit, depth);
        }

        private void UpdateCamera() {
            NsmbRegionMarker region = CurrentView();
            if (view == null || region == null) {
                return;
            }

            float halfWidth = ScreenWidth / NsmbUnits.PixelsPerUnit / 2f;
            float halfHeight = ScreenHeight / NsmbUnits.PixelsPerUnit / 2f;
            Rect rect = region.WorldRect;
            Vector3 position = transform.position;
            float x = rect.width > halfWidth * 2f ? Mathf.Clamp(position.x, rect.xMin + halfWidth, rect.xMax - halfWidth) : rect.center.x;
            float y = rect.height > halfHeight * 2f ? Mathf.Clamp(position.y, rect.yMin + halfHeight, rect.yMax - halfHeight) : rect.center.y;
            view.orthographicSize = halfHeight;
            view.transform.position = new Vector3(x, y, view.transform.position.z);
        }

        private void OnGUI() {
            if (motor == null) {
                return;
            }

            GUI.Label(new Rect(Screen.width - 260f, 8f, 250f, 130f),
                $"Coins {Coins}  Lives +{ExtraLives}  {motor.Power}\nPos {motor.X:F1}, {motor.Y:F1}\nSpeed {motor.SpeedX:F3}, {motor.SpeedY:F3}\n{(motor.Grounded ? "Grounded" : "Air")} {motor.JumpState}\nTile 0x{TileUnderFeet:X8}\nZ jump, X run, Up door, Down pipe");
        }
    }
}

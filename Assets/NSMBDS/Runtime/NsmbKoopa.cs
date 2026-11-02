using UnityEngine;

namespace NsmbDs {
    /// <summary>Sprites 149 (green, walks off ledges) and 150 (red, turns at ledges). A stomp leaves a shell, a touch kicks it.</summary>
    public sealed class NsmbKoopa : NsmbActor {
        private const float WalkSpeed = 0.5f;
        private const float ShellSpeed = 2.5f;
        private const int WalkFrames = 19;
        private const int ShellSpinFrames = 4;
        private const int FramesPerSprite = 2;
        private const int KickGraceFrames = 12;
        private const float ShellStopRate = 0.1f;
        private const float ShellDrift = 0.5f;
        private const int RedSprite = 150;

        private Sprite[] frames;
        private bool shell;
        private int age;
        private int grace;
        private bool sliding;

        private bool IsRed => Marker.type == RedSprite;

        public bool IsMovingShell => shell && Mathf.Abs(SpeedX) > ShellDrift;

        protected override void OnSpawn() {
            frames = Stage.Assets == null ? null : IsRed ? Stage.Assets.koopaRed : Stage.Assets.koopa;
            SpeedX = Player.X < X ? -WalkSpeed : WalkSpeed;
            ShowFrame();
        }

        protected override void StepActor() {
            if (grace > 0) {
                grace--;
            }

            // A stomped shell slides to a stop over a few frames.
            if (sliding) {
                SpeedX = Mathf.MoveTowards(SpeedX, 0f, ShellStopRate);
                sliding = SpeedX != 0f;
            }

            if (IsRed && !shell && Grounded && !FloorAhead()) {
                SpeedX = -SpeedX;
            }

            WalkOnTiles();
            if (IsMovingShell) {
                Stage.Actors.DefeatTouching(this);
            }
            age++;
            ShowFrame();
        }

        private bool FloorAhead() {
            NsmbTileBehavior tile = Tiles.GetTileAt(X + (SpeedX > 0f ? HalfWidth : -HalfWidth), Y + 1f);
            return tile.IsFullSolid || tile.IsSolidOnTop || tile.IsSlope;
        }

        protected override void OnStomped() {
            if (!shell || Mathf.Abs(SpeedX) > ShellDrift) {
                shell = true;
                sliding = true;
                grace = KickGraceFrames;
            } else {
                Kick();
            }
        }

        protected override void OnPlayerTouch() {
            if (grace > 0) {
                return;
            }

            if (shell && !IsMovingShell) {
                Kick();
            } else {
                base.OnPlayerTouch();
            }
        }

        private void Kick() {
            sliding = false;
            SpeedX = Player.X < X ? ShellSpeed : -ShellSpeed;
            grace = KickGraceFrames;
        }

        private void ShowFrame() {
            if (frames == null || frames.Length < WalkFrames + ShellSpinFrames) {
                return;
            }

            int spin = SpeedX != 0f ? (age / FramesPerSprite) % ShellSpinFrames : 0;
            View.sprite = shell ? frames[WalkFrames + spin] : frames[(age / FramesPerSprite) % WalkFrames];
            View.flipX = SpeedX > 0f;
        }
    }
}

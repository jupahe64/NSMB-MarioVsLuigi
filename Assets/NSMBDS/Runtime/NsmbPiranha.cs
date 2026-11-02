using UnityEngine;

namespace NsmbDs {
    /// <summary>Sprites 23 (in an upward pipe) and 24 (in a downward pipe): rises 32 px at 1 px/frame, bites, hides. Cannot be stomped.</summary>
    public sealed class NsmbPiranha : NsmbActor {
        private const int MoveFrames = 32;
        private const int ExposedFrames = 64;
        private const int HiddenFrames = 57;
        private const int DownPipeSprite = 24;
        private const int BiteFrames = 8;
        private const float PipeOffsetX = 8f;

        private enum Phase { Exposed, Hiding, Hidden, Rising }

        private Sprite[] frames;
        private Phase phase = Phase.Exposed;
        private int timer = ExposedFrames;
        private float direction;
        private int age;

        protected override bool CanBeStomped => false;
        protected override float Height => 24f;
        protected override float HalfWidth => 6f;

        protected override void OnSpawn() {
            frames = Stage.Assets != null ? Stage.Assets.piranha : null;
            direction = Marker.type == DownPipeSprite ? -1f : 1f;
            X += PipeOffsetX;
            Y -= NsmbTileSource.TileSize;
            View.flipY = direction < 0f;
            View.sortingOrder = -1;
        }

        protected override void StepActor() {
            age++;
            if (frames != null && frames.Length > 1) {
                View.sprite = frames[(age / BiteFrames) % 2];
            }

            if (--timer > 0) {
                if (phase == Phase.Hiding) {
                    Y += direction;
                } else if (phase == Phase.Rising) {
                    Y -= direction;
                }
                return;
            }

            phase = (Phase) (((int) phase + 1) % 4);
            timer = phase switch {
                Phase.Exposed => ExposedFrames,
                Phase.Hidden => HiddenFrames,
                _ => MoveFrames,
            };
        }

        protected override bool CanTouchPlayer => phase != Phase.Hidden;
    }
}

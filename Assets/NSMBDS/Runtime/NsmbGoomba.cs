using UnityEngine;

namespace NsmbDs {
    /// <summary>Sprites 148 and 222: walks at 0.5 px/frame towards the player's side, falls off ledges, flattens when stomped.</summary>
    public sealed class NsmbGoomba : NsmbActor {
        private const float WalkSpeed = 0.5f;
        private const int FlatFrames = 30;
        private const int FrameLength = 12;

        private Sprite[] frames;
        private int flat;
        private int age;

        protected override float Height => 14f;

        protected override void OnSpawn() {
            frames = Stage.Assets != null ? Stage.Assets.goomba : null;
            SpeedX = Player.X < X ? -WalkSpeed : WalkSpeed;
            ShowFrame();
        }

        protected override void StepActor() {
            if (flat > 0) {
                if (--flat == 0) {
                    Remove();
                }
                return;
            }

            WalkOnTiles();
            age++;
            ShowFrame();
        }

        protected override void OnStomped() {
            base.OnStomped();
            flat = FlatFrames;
            transform.localScale = new Vector3(1f, 0.4f, 1f);
        }

        private void ShowFrame() {
            if (frames != null && frames.Length > 1) {
                View.sprite = frames[(age / FrameLength) % 2];
                View.flipX = SpeedX > 0f;
            }
        }
    }
}

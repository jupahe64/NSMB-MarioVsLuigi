using UnityEngine;

namespace NsmbDs {
    /// <summary>Sprite 32: touching the pole ends the level.</summary>
    public sealed class NsmbGoalPole : NsmbActor {
        private const float PoleOffsetX = 8f;
        private const float PoleHeight = 160f;

        protected override bool CanBeStomped => false;
        protected override float HalfWidth => 2f;
        protected override float Height => PoleHeight;

        protected override void OnSpawn() {
            X += PoleOffsetX;
            Y += NsmbTileSource.TileSize;
        }

        protected override void StepActor() {
        }

        protected override void OnPlayerTouch() {
            Stage.FinishLevel();
        }
    }
}

using UnityEngine;

namespace NsmbDs {
    /// <summary>Sprite 57: a coin placed as a sprite rather than a tile.</summary>
    public sealed class NsmbCoinSprite : NsmbActor {
        protected override void OnSpawn() {
            View.sprite = Stage.CoinSprite;
            Y -= NsmbTileSource.TileSize / 2f;
        }

        protected override void StepActor() {
        }

        protected override bool CanBeStomped => false;

        protected override void OnPlayerTouch() {
            Stage.AddCoins(1);
            Defeated = true;
            Remove();
        }
    }
}

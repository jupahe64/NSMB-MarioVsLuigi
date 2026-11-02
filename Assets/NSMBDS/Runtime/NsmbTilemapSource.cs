using UnityEngine;
using UnityEngine.Tilemaps;

namespace NsmbDs {
    /// <summary>Reads tile behaviours from an area scene's Tilemap, so painted tiles collide too.</summary>
    public sealed class NsmbTilemapSource : NsmbTileSource {
        private readonly Tilemap tilemap;

        public NsmbTilemapSource(Tilemap tilemap) {
            this.tilemap = tilemap;
        }

        public override NsmbTileBehavior GetTile(int column, int row) {
            return tilemap.GetTile(new Vector3Int(column, -row - 1, 0)) is NsmbTile tile
                ? new NsmbTileBehavior(tile.behavior)
                : default;
        }
    }
}

using UnityEngine;
using UnityEngine.Tilemaps;

namespace NsmbDs {
    /// <summary>A Map16 tile. Keeps its behaviour word, so tiles painted with the Tilemap tools stay correct.</summary>
    [CreateAssetMenu(menuName = "NSMB DS/Tile")]
    public sealed class NsmbTile : Tile {
        public uint behavior;
    }
}

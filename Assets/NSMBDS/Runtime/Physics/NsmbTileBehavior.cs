using System;

namespace NsmbDs {
    /// <summary>Meaning of the 32-bit tile behaviour word: flags in the high half, subtype and parameter below.</summary>
    public readonly struct NsmbTileBehavior {
        private const uint Solid = 0x0001;
        private const uint Coin = 0x0002;
        private const uint Slope = 0x0020;
        private const uint FlippedSlope = 0x0040;
        private const uint DoorOrPipe = 0x0100;
        private const uint Water = 0x0200;
        private const uint Climbable = 0x0400;
        private const uint PartialSolid = 0x0800;
        private const uint Harmful = 0x1000;
        private const uint Invisible = 0x2000;
        private const uint SolidOnTop = 0x8000;

        public const int SubTypeIce = 1;
        public const int SubTypeNoSliding = 11;

        public readonly uint Word;

        public NsmbTileBehavior(uint word) {
            Word = word;
        }

        private uint Flags => Word >> 16;

        public bool IsSlope => (Flags & Slope) != 0;
        public bool IsFlippedSlope => (Flags & FlippedSlope) != 0;
        public bool IsSolidOnTop => (Flags & SolidOnTop) != 0;
        public bool IsCoin => (Flags & Coin) != 0;
        public bool IsPipe => (Flags & DoorOrPipe) != 0;
        public bool IsWater => (Flags & Water) != 0;
        public bool IsClimbable => (Flags & Climbable) != 0;
        public bool IsHarmful => (Flags & Harmful) != 0;
        public int Parameter => (int) (Word & 0xFF);
        public int SubType => (int) ((Word >> 12) & 0xF);

        /// <summary>Blocks from every side. Slopes and one-way tiles are handled separately.</summary>
        public bool IsFullSolid {
            get {
                if (IsSlope || IsFlippedSlope || IsSolidOnTop || (Flags & Invisible) != 0) {
                    return false;
                }
                return (Flags & (Solid | PartialSolid)) != 0;
            }
        }

        /// <summary>Solid part of a slope tile at a column, 0-16 px measured from the tile's bottom (or top when flipped).</summary>
        public float SlopeHeight(float columnInTile) {
            int id = Parameter;
            float t = Math.Max(0f, Math.Min(1f, columnInTile / NsmbTileSource.TileSize));
            switch (id) {
            case 0: return 16f * t;
            case 1: return 16f * (1f - t);
            case 2: return 8f * t;
            case 3: return 8f + (8f * t);
            case 4: return 16f - (8f * t);
            case 5: return 8f - (8f * t);
            case 6: return Math.Min(16f, 32f * t);
            case 7: return Math.Max(0f, (32f * t) - 16f);
            case 8: return Math.Min(16f, 32f * (1f - t));
            case 9: return Math.Max(0f, 16f - (32f * t));
            case 11: case 12: case 13: case 14:
                return (4f * (id - 11)) + (4f * t);
            case 15: case 16: case 17: case 18:
                return 16f - (4f * (id - 15)) - (4f * t);
            default:
                return 16f;
            }
        }
    }

    /// <summary>Tile lookup in DS pixels, with y growing downwards.</summary>
    public abstract class NsmbTileSource {
        public const int TileSize = 16;

        public abstract NsmbTileBehavior GetTile(int column, int row);

        public NsmbTileBehavior GetTileAt(float x, float y) {
            return GetTile((int) Math.Floor(x / TileSize), (int) Math.Floor(y / TileSize));
        }
    }
}

namespace NsmbDs {
    public enum NsmbPower { Small, Big, Fire, Shell, Mini, Mega }

    public enum NsmbLiquidKind { None, Water, Lava, Poison }

    public enum NsmbItemKind { None, Mushroom, FireFlower, Coin, MultiCoin, Mega, Star, Mini, OneUp, Shell }

    /// <summary>What a block gives when hit, from the game's content tables in StageEntity::func_ov000_0209e7d0.</summary>
    public static class NsmbBlocks {
        public const int HitToUsedFrames = 14;
        public const int MultiCoinHits = 10;
        public const int UsedBlockMap16 = 0x18;
        private const int ParameterMask = 0x3F;
        private const int NoItem = 15;

        // Item numbers as the Item actor reads them, by the tile's low six bits.
        private static readonly int[] BrickItems = {
            15, 0, 1, 7, 2, 4, 10, 31, 15, 15, 15, 15, 15, 15, 15, 15,
            2, 0, 1, 7, 9, 25, 11, 5, 15, 15, 15, 15, 15, 15, 15, 15,
        };

        private static readonly int[] BlockItems = {
            0, 1, 27, 7, 13, 25, 26, 2, 10, 21, 24, 17, 30, 5, 15, 15,
            0, 29, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
            15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
            0, 1, 7, 2, 10, 24,
        };

        public static bool IsHittable(NsmbTileBehavior tile) {
            return tile.IsQuestion || tile.IsBrick;
        }

        public static NsmbItemKind Content(NsmbTileBehavior tile) {
            int index = tile.Parameter & ParameterMask;
            int[] table = tile.IsBrick ? BrickItems : BlockItems;
            return KindOf(index < table.Length ? table[index] : NoItem);
        }

        public static NsmbItemKind KindOf(int item) {
            switch (item) {
            case 0: return NsmbItemKind.Mushroom;
            case 1: case 27: return NsmbItemKind.FireFlower;
            case 2: return NsmbItemKind.Coin;
            case 4: return NsmbItemKind.MultiCoin;
            case 5: return NsmbItemKind.Mega;
            case 7: return NsmbItemKind.Star;
            case 9: return NsmbItemKind.Mini;
            case 10: return NsmbItemKind.OneUp;
            case 11: return NsmbItemKind.Shell;
            default: return NsmbItemKind.None;
            }
        }

        /// <summary>The power a player ends up with after collecting an item. A mushroom never takes a better power away.</summary>
        public static NsmbPower Collect(NsmbPower current, NsmbItemKind item) {
            switch (item) {
            case NsmbItemKind.Mushroom:
                return current == NsmbPower.Small || current == NsmbPower.Mini ? NsmbPower.Big : current;
            case NsmbItemKind.FireFlower:
                return NsmbPower.Fire;
            case NsmbItemKind.Shell:
                return NsmbPower.Shell;
            case NsmbItemKind.Mini:
                return NsmbPower.Mini;
            case NsmbItemKind.Mega:
                return NsmbPower.Mega;
            default:
                return current;
            }
        }

        /// <summary>Liquid sprites 231, 234 and 259; the surface sits a little below lava and poison sprites.</summary>
        public static NsmbLiquidKind LiquidOf(int spriteType, out float surfaceOffset) {
            switch (spriteType) {
            case 231:
                surfaceOffset = 0f;
                return NsmbLiquidKind.Water;
            case 234:
                surfaceOffset = 3f;
                return NsmbLiquidKind.Lava;
            case 259:
                surfaceOffset = 8f;
                return NsmbLiquidKind.Poison;
            default:
                surfaceOffset = 0f;
                return NsmbLiquidKind.None;
            }
        }
    }
}

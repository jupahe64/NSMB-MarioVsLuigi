using System.Collections.Generic;

namespace NsmbRom.Nsmb {
    public enum Overlay0Table {
        TilesetObjectIndex,
        TilesetObjects,
        TilesetBehaviors,
        TilesetAnimatedGraphics,
        BackgroundGraphics,
        TilesetGraphics,
        ForegroundGraphics,
        ForegroundScreen,
        BackgroundScreen,
        BackgroundPalette,
        TilesetPalette,
        ForegroundPalette,
        TilesetMap16,
    }

    /// <summary>Offsets of the data tables inside decompressed overlay 0, per region. Always validated against the ROM.</summary>
    public sealed class NsmbRegionProfile {
        public const int TilesetCount = 76;
        public const int SpriteCount = 326;
        public const int CommonBehaviorsSize = 0x400;

        /// <summary>Table values are relative to the first file after the overlays.</summary>
        public int FileIdBase { get; }
        public IReadOnlyDictionary<Overlay0Table, int> TableOffsets { get; }
        public int CommonBehaviorsOffset { get; }
        public int SpriteModifiersOffset { get; }
        /// <summary>Sprite to class ID table; only known for USA, where the behaviour analysis happens.</summary>
        public int? SpriteClassIdsOffset { get; }

        private NsmbRegionProfile(int fileIdBase, int[] tableOffsets, int commonBehaviors, int spriteModifiers, int? spriteClassIds) {
            FileIdBase = fileIdBase;
            Dictionary<Overlay0Table, int> tables = new();
            for (int i = 0; i < tableOffsets.Length; i++) {
                tables[(Overlay0Table) i] = tableOffsets[i];
            }
            TableOffsets = tables;
            CommonBehaviorsOffset = commonBehaviors;
            SpriteModifiersOffset = spriteModifiers;
            SpriteClassIdsOffset = spriteClassIds;
        }

        public static NsmbRegionProfile For(RomRegion region) {
            return region == RomRegion.Usa ? Usa : Europe;
        }

        private static readonly NsmbRegionProfile Usa = new(131, new[] {
            0x2F8E4, 0x2FA14, 0x2FB44, 0x2FC74, 0x30D74, 0x30EA4, 0x30FD4,
            0x31104, 0x31234, 0x31364, 0x31494, 0x315C4, 0x316F4,
        }, 0x2FDA4, 0x2C930, 0x29BD8);

        private static readonly NsmbRegionProfile Europe = new(135, new[] {
            0x2F0F8, 0x2F228, 0x2F358, 0x2F488, 0x30588, 0x306B8, 0x307E8,
            0x30918, 0x30A48, 0x30B78, 0x30CA8, 0x30DD8, 0x30F08,
        }, 0x2F5B8, 0x2BDF0, null);
    }
}

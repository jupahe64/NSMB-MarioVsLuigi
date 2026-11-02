using System;
using System.Collections.Generic;
using NsmbRom.Nitro;

namespace NsmbRom.Nsmb {
    /// <summary>
    /// Pipes are not BG graphics: the game draws them as 3D quads over tiles whose behaviour marks them as pipe.
    /// The behaviour's low byte holds the colour (bits 6-7), a collision variant (bit 5) and the piece (bits 0-4).
    /// The piece to texture map is inferred from texture names and level layouts, not read from code.
    /// </summary>
    public sealed class PipeArt {
        private const int TextureSize = 16;
        private const int PaletteColors = 256;
        private const uint PipeBehaviorHigh = 0x0101;
        private const int PieceMask = 0x1F;
        private const int ColorShift = 6;
        private const int ColorCount = 3;

        private static readonly Dictionary<int, string> PieceTextures = new() {
            [0x00] = "I_do_lu",
            [0x01] = "I_do_ru",
            [0x02] = "I_do_d_ld",
            [0x03] = "I_do_d_rd",
            [0x04] = "I_do_ld",
            [0x05] = "I_do_rd",
            [0x08] = "I_do_yoko_lu",
            [0x09] = "I_do_yoko_ld",
            [0x0A] = "I_do_yoko_ru",
            [0x0B] = "I_do_yoko_rd",
            [0x0C] = "I_do_yoko_u",
            [0x0D] = "I_do_yoko_d",
            [0x11] = "I_do_hahen_l",
            [0x13] = "I_do_hahen_r",
            [0x15] = "I_do_hahen_ld",
            [0x17] = "I_do_hahen_rd",
        };

        // Mini pipes come from their own texture file, with one palette.
        private static readonly Dictionary<int, string> MiniPieceTextures = new() {
            [0x10] = "I_mamedokan_u",
            [0x12] = "I_mamedokan_d",
            [0x16] = "I_mamedokan_tt",
            [0x18] = "I_mamedokan_l",
            [0x1A] = "I_mamedokan_r",
            [0x1C] = "I_mamedokan_tate",
            [0x1D] = "I_mamedokan_yoko",
            [0x1E] = "I_mamedokan_yt",
        };

        // Junctions share one behaviour per column or row, so only the common tile number tells their four corners apart.
        // They exist in the first colour only.
        private static readonly Dictionary<int, string> JunctionTiles = new() {
            [0x78] = "I_do_kousa_t_lu",
            [0x79] = "I_do_kousa_t_ru",
            [0x88] = "I_do_kousa_t_ld",
            [0x89] = "I_do_kousa_t_rd",
            [0x76] = "I_do_kousa_y_lu",
            [0x77] = "I_do_kousa_y_ru",
            [0x86] = "I_do_kousa_y_ld",
            [0x87] = "I_do_kousa_y_rd",
        };

        // Metal hub block where pipes join, confirmed against the running game. Each corner is its own texture file.
        public static readonly IReadOnlyDictionary<int, string> HubFiles = new Dictionary<int, string> {
            [0x7C] = "I_do_kanagu_lu.nsbtx",
            [0x7D] = "I_do_kanagu_ru.nsbtx",
            [0x8C] = "I_do_kanagu_ld.nsbtx",
            [0x8D] = "I_do_kanagu_rd.nsbtx",
        };

        private readonly Dictionary<int, byte[]> tiles = new();
        private readonly Dictionary<int, byte[]> junctions = new();
        private readonly Dictionary<int, byte[]> miniTiles = new();

        public PipeArt(Nsbtx textures, IReadOnlyDictionary<int, Nsbtx> hubs, Nsbtx miniPipes) {
            foreach (NsbtxTexture texture in miniPipes.Textures) {
                foreach (KeyValuePair<int, string> piece in MiniPieceTextures) {
                    if (texture.Name == piece.Value) {
                        miniTiles[piece.Key] = miniPipes.Decode(texture, miniPipes.Palettes[0])
                            ?? throw new RomFormatException($"Mini pipe texture {texture.Name} uses an unsupported format.");
                    }
                }
            }

            foreach (KeyValuePair<int, Nsbtx> hub in hubs) {
                junctions[hub.Key] = hub.Value.Decode(hub.Value.Textures[0], hub.Value.Palettes[0])
                    ?? throw new RomFormatException("Pipe hub texture uses an unsupported format.");
            }

            Dictionary<string, NsbtxTexture> byName = new(StringComparer.Ordinal);
            foreach (NsbtxTexture texture in textures.Textures) {
                byName[texture.Name] = texture;
            }

            if (textures.Palettes.Count != 1 || textures.Palettes[0].Size < ColorCount * PaletteColors * 2) {
                throw new RomFormatException("Pipe textures do not have the expected three colour palettes.");
            }

            for (int color = 0; color < ColorCount; color++) {
                foreach (KeyValuePair<int, string> piece in PieceTextures) {
                    tiles[(color << ColorShift) | piece.Key] = Decode(textures, byName, piece.Value, color);
                }
            }
            foreach (KeyValuePair<int, string> junction in JunctionTiles) {
                junctions[junction.Key] = Decode(textures, byName, junction.Value, 0);
            }
        }

        private static byte[] Decode(Nsbtx textures, Dictionary<string, NsbtxTexture> byName, string name, int color) {
            if (!byName.TryGetValue(name, out NsbtxTexture? texture) || texture.Width != TextureSize || texture.Height != TextureSize) {
                throw new RomFormatException($"Pipe texture '{name}' is missing or not {TextureSize}x{TextureSize}.");
            }
            return textures.Decode(texture, textures.Palettes[0].Slice(color * PaletteColors * 2, PaletteColors * 2))!;
        }

        /// <summary>Returns the 16x16 RGBA art of a tile, or null if it is not a pipe tile with visible art.</summary>
        public byte[]? GetTile(TilesetSlot slot, int tile, uint behavior) {
            if (behavior >> 16 != PipeBehaviorHigh) {
                return null;
            }
            if (slot == TilesetSlot.Common && junctions.TryGetValue(tile, out byte[]? junction)) {
                return junction;
            }
            if (miniTiles.TryGetValue((int) behavior & PieceMask, out byte[]? mini)) {
                return mini;
            }

            int code = (int) behavior & ((3 << ColorShift) | PieceMask);
            return tiles.TryGetValue(code, out byte[]? art) ? art : null;
        }
    }
}

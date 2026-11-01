using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace NsmbRom.Nsmb {
    public sealed class LevelSettings {
        public int StartEntrance { get; internal set; }
        public int MidwayEntrance { get; internal set; }
        public int TimeLimit { get; internal set; }
        public bool Wraps { get; internal set; }
        public bool ForceMini { get; internal set; }
        public bool MiniPhysics { get; internal set; }
        public int Background { get; internal set; }
        public int Tileset { get; internal set; }
        public int Foreground { get; internal set; }
        public int SoundSet { get; internal set; }
    }

    public sealed class LevelEntrance {
        public int X { get; internal set; }
        public int Y { get; internal set; }
        public int CameraX { get; internal set; }
        public int CameraY { get; internal set; }
        public int Number { get; internal set; }
        public int DestArea { get; internal set; }
        public int ConnectedPipe { get; internal set; }
        public int DestEntrance { get; internal set; }
        public int Type { get; internal set; }
        public int Settings { get; internal set; }
        public int Unknown1 { get; internal set; }
        public int EntryView { get; internal set; }
        public int Unknown2 { get; internal set; }
    }

    public sealed class LevelSprite {
        public int Type { get; internal set; }
        /// <summary>Position in 16 px tiles.</summary>
        public int X { get; internal set; }
        public int Y { get; internal set; }
        /// <summary>The six settings bytes, in the byte order the game reads them.</summary>
        public int[] Data { get; internal set; } = Array.Empty<int>();
    }

    public sealed class LevelView {
        public int X { get; internal set; }
        public int Y { get; internal set; }
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public int Number { get; internal set; }
        public int CameraId { get; internal set; }
        public int Music { get; internal set; }
        public int Unknown1 { get; internal set; }
        public int Unknown2 { get; internal set; }
        public int Unknown3 { get; internal set; }
        public int Lighting { get; internal set; }
        public int FlagpoleId { get; internal set; }
        public bool HasCamera { get; internal set; }
        public int CameraTop { get; internal set; }
        public int CameraBottom { get; internal set; }
        public int CameraTopSpin { get; internal set; }
        public int CameraBottomSpin { get; internal set; }
        public int CameraBottomStick { get; internal set; }
    }

    public sealed class LevelZone {
        public int X { get; internal set; }
        public int Y { get; internal set; }
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public int Number { get; internal set; }
    }

    public sealed class LevelPathPoint {
        public int X { get; internal set; }
        public int Y { get; internal set; }
        public int[] Unknown { get; internal set; } = Array.Empty<int>();
    }

    public sealed class LevelPath {
        public int Id { get; internal set; }
        public List<LevelPathPoint> Points { get; } = new();
    }

    public sealed class LevelObject {
        public int Id { get; internal set; }
        public int Tileset { get; internal set; }
        public int X { get; internal set; }
        public int Y { get; internal set; }
        public int Width { get; internal set; }
        public int Height { get; internal set; }
    }

    /// <summary>One area: a course/*.bin file with its 14 blocks, plus the tile objects of its _bgdat file.</summary>
    public sealed class LevelArea {
        private const int BlockCount = 14;
        private const int BlockTableEntry = 8;
        private const int SettingsBlock = 0;
        private const int CameraBlock = 1;
        private const int BackgroundBlock = 2;
        private const int EntranceBlock = 5;
        private const int SpriteBlock = 6;
        private const int ViewBlock = 7;
        private const int ZoneBlock = 8;
        private const int ProgressPathBlock = 9;
        private const int PathBlock = 10;
        private const int ProgressPathPointBlock = 11;
        private const int PathPointBlock = 12;
        private const int ObjectRecordSize = 10;
        private const int EntranceRecordSize = 20;
        private const int SpriteRecordSize = 12;
        private const int ViewRecordSize = 16;
        private const int ZoneRecordSize = 12;
        private const int CameraRecordSize = 24;
        private const int PathRecordSize = 8;
        private const int PathPointRecordSize = 16;
        private const int SpriteTerminatorSize = 4;

        public string Name { get; }
        public LevelSettings Settings { get; } = new();
        /// <summary>Background screen ID, which also picks the common tileset's palette.</summary>
        public int BackgroundScreen { get; private set; }
        public List<LevelEntrance> Entrances { get; } = new();
        public List<LevelSprite> Sprites { get; } = new();
        public List<LevelView> Views { get; } = new();
        public List<LevelZone> Zones { get; } = new();
        public List<LevelPath> Paths { get; } = new();
        public List<LevelPath> ProgressPaths { get; } = new();
        public List<LevelObject> Objects { get; } = new();
        /// <summary>Every block as hex, because several still have unknown fields.</summary>
        public List<string> RawBlocks { get; } = new();

        private LevelArea(string name) {
            Name = name;
        }

        public static LevelArea Parse(string name, byte[] level, byte[] bgdat) {
            LevelArea area = new(name);
            byte[][] blocks = area.ReadBlocks(level);

            area.ReadSettings(blocks[SettingsBlock], blocks[BackgroundBlock]);
            area.ReadEntrances(blocks[EntranceBlock]);
            area.ReadSprites(blocks[SpriteBlock]);
            area.ReadViews(blocks[ViewBlock], blocks[CameraBlock]);
            area.ReadZones(blocks[ZoneBlock]);
            area.ReadPaths(blocks[PathBlock], blocks[PathPointBlock], area.Paths);
            area.ReadPaths(blocks[ProgressPathBlock], blocks[ProgressPathPointBlock], area.ProgressPaths);
            area.ReadObjects(bgdat);
            return area;
        }

        private byte[][] ReadBlocks(byte[] level) {
            if (level.Length < BlockCount * BlockTableEntry) {
                throw Error($"file is too small for the block table ({level.Length} bytes)");
            }

            byte[][] blocks = new byte[BlockCount][];
            for (int i = 0; i < BlockCount; i++) {
                uint offset = BinaryPrimitives.ReadUInt32LittleEndian(level.AsSpan(i * BlockTableEntry));
                uint size = BinaryPrimitives.ReadUInt32LittleEndian(level.AsSpan((i * BlockTableEntry) + 4));
                if ((ulong) offset + size > (ulong) level.Length) {
                    throw Error($"block {i} (0x{offset:X}+0x{size:X}) runs past the file");
                }
                blocks[i] = level.AsSpan((int) offset, (int) size).ToArray();
                RawBlocks.Add(ToHex(blocks[i]));
            }
            return blocks;
        }

        private void ReadSettings(byte[] settings, byte[] background) {
            if (settings.Length < 0x1B) {
                throw Error($"settings block is {settings.Length} bytes");
            }
            if (background.Length < 3) {
                throw Error($"background block is {background.Length} bytes");
            }

            Settings.StartEntrance = settings[0];
            Settings.MidwayEntrance = settings[1];
            Settings.ForceMini = (settings[2] & 0x01) != 0;
            Settings.MiniPhysics = (settings[2] & 0x02) != 0;
            Settings.Wraps = (settings[2] & 0x20) != 0;
            Settings.TimeLimit = settings[4] | (settings[5] << 8);
            Settings.Background = settings[0x06];
            Settings.Tileset = settings[0x0C];
            Settings.Foreground = settings[0x12];
            Settings.SoundSet = settings[0x1A];
            BackgroundScreen = background[2];
        }

        private void ReadEntrances(byte[] block) {
            RequireMultiple(block, EntranceRecordSize, "entrance");
            for (int pos = 0; pos < block.Length; pos += EntranceRecordSize) {
                ReadOnlySpan<byte> r = block.AsSpan(pos, EntranceRecordSize);
                Entrances.Add(new LevelEntrance {
                    X = U16(r, 0),
                    Y = U16(r, 2),
                    CameraX = U16(r, 4),
                    CameraY = U16(r, 6),
                    Number = r[8],
                    DestArea = r[9],
                    ConnectedPipe = r[10],
                    DestEntrance = r[12],
                    Type = r[14],
                    Settings = r[15],
                    Unknown1 = r[16],
                    EntryView = r[18],
                    Unknown2 = r[19],
                });
            }
        }

        private void ReadSprites(byte[] block) {
            int count = (block.Length - SpriteTerminatorSize) / SpriteRecordSize;
            if (block.Length < SpriteTerminatorSize || (block.Length - SpriteTerminatorSize) % SpriteRecordSize != 0) {
                throw Error($"sprite block is {block.Length} bytes");
            }

            for (int i = 0; i < count; i++) {
                ReadOnlySpan<byte> r = block.AsSpan(i * SpriteRecordSize, SpriteRecordSize);
                Sprites.Add(new LevelSprite {
                    Type = U16(r, 0),
                    X = U16(r, 2),
                    Y = U16(r, 4),
                    Data = new int[] { r[7], r[6], r[11], r[10], r[9], r[8] },
                });
            }
        }

        private void ReadViews(byte[] block, byte[] cameras) {
            RequireMultiple(block, ViewRecordSize, "view");
            for (int pos = 0; pos < block.Length; pos += ViewRecordSize) {
                ReadOnlySpan<byte> r = block.AsSpan(pos, ViewRecordSize);
                LevelView view = new() {
                    X = U16(r, 0),
                    Y = U16(r, 2),
                    Width = U16(r, 4),
                    Height = U16(r, 6),
                    Number = r[8],
                    CameraId = r[9],
                    Music = r[10],
                    Unknown1 = r[11],
                    Unknown2 = r[12],
                    Unknown3 = r[13],
                    Lighting = r[14],
                    FlagpoleId = r[15],
                };
                ReadCamera(view, cameras);
                Views.Add(view);
            }
        }

        private static void ReadCamera(LevelView view, byte[] cameras) {
            for (int pos = 0; pos + CameraRecordSize <= cameras.Length; pos += CameraRecordSize) {
                ReadOnlySpan<byte> r = cameras.AsSpan(pos, CameraRecordSize);
                if (U16(r, 16) != view.CameraId) {
                    continue;
                }
                view.HasCamera = true;
                view.CameraTop = BinaryPrimitives.ReadInt32LittleEndian(r);
                view.CameraBottom = BinaryPrimitives.ReadInt32LittleEndian(r.Slice(4));
                view.CameraTopSpin = BinaryPrimitives.ReadInt32LittleEndian(r.Slice(8));
                view.CameraBottomSpin = BinaryPrimitives.ReadInt32LittleEndian(r.Slice(12));
                view.CameraBottomStick = U16(r, 18);
                return;
            }
        }

        private void ReadZones(byte[] block) {
            RequireMultiple(block, ZoneRecordSize, "zone");
            for (int pos = 0; pos < block.Length; pos += ZoneRecordSize) {
                ReadOnlySpan<byte> r = block.AsSpan(pos, ZoneRecordSize);
                Zones.Add(new LevelZone {
                    X = U16(r, 0),
                    Y = U16(r, 2),
                    Width = U16(r, 4),
                    Height = U16(r, 6),
                    Number = r[8],
                });
            }
        }

        private void ReadPaths(byte[] block, byte[] points, List<LevelPath> target) {
            RequireMultiple(block, PathRecordSize, "path");
            for (int pos = 0; pos < block.Length; pos += PathRecordSize) {
                ReadOnlySpan<byte> r = block.AsSpan(pos, PathRecordSize);
                LevelPath path = new() { Id = U16(r, 0) };
                int first = U16(r, 2);
                int count = U16(r, 4);
                if ((first + count) * PathPointRecordSize > points.Length) {
                    throw Error($"path {path.Id} runs past its point block");
                }

                for (int i = 0; i < count; i++) {
                    ReadOnlySpan<byte> p = points.AsSpan((first + i) * PathPointRecordSize, PathPointRecordSize);
                    path.Points.Add(new LevelPathPoint {
                        X = U16(p, 0),
                        Y = U16(p, 2),
                        Unknown = new[] { U16(p, 4), U16(p, 6), U16(p, 8), U16(p, 10), U16(p, 12), U16(p, 14) },
                    });
                }
                target.Add(path);
            }
        }

        private void ReadObjects(byte[] bgdat) {
            // The file ends with a 2-byte terminator when it holds whole records.
            for (int pos = 0; pos + ObjectRecordSize <= bgdat.Length; pos += ObjectRecordSize) {
                ReadOnlySpan<byte> r = bgdat.AsSpan(pos, ObjectRecordSize);
                int id = U16(r, 0);
                Objects.Add(new LevelObject {
                    Id = id & 0xFFF,
                    Tileset = id >> 12,
                    X = BinaryPrimitives.ReadInt16LittleEndian(r.Slice(2)),
                    Y = BinaryPrimitives.ReadInt16LittleEndian(r.Slice(4)),
                    Width = U16(r, 6),
                    Height = U16(r, 8),
                });
            }
        }

        private void RequireMultiple(byte[] block, int size, string what) {
            if (block.Length % size != 0) {
                throw Error($"{what} block is {block.Length} bytes, not a multiple of {size}");
            }
        }

        private RomFormatException Error(string message) {
            return new RomFormatException($"Level {Name}: {message}.");
        }

        private static int U16(ReadOnlySpan<byte> data, int offset) {
            return BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset));
        }

        private static string ToHex(byte[] data) {
            StringBuilder hex = new(data.Length * 2);
            foreach (byte b in data) {
                hex.Append(b.ToString("x2"));
            }
            return hex.ToString();
        }
    }
}

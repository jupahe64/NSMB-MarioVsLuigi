using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace NsmbDs.Editor {
    /// <summary>Turns the extractor's output into Tile assets and one prefab per area.</summary>
    public static class NsmbImporter {
        public const string ExtractedRoot = "Assets/NSMBDS/Extracted";
        private const string LevelsFolder = ExtractedRoot + "/levels";
        private const string TilesetsFolder = ExtractedRoot + "/tilesets";
        private const string LayersFolder = ExtractedRoot + "/layers";
        private const string TileAssetFolder = ExtractedRoot + "/Tiles";
        private const string ResourceFolder = ExtractedRoot + "/Resources/NSMBDS";
        private const string LegacyAreaFolder = ResourceFolder + "/Areas";
        private const string SceneFolder = ExtractedRoot + "/Scenes";
        private const string PaletteFolder = ExtractedRoot + "/Palettes";
        private const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const int GridWidth = 512;
        private const int GridHeight = 256;
        private const int TilePixels = 16;
        private const int AtlasColumns = 16;
        private const int PaletteSlotWidth = AtlasColumns + 1;
        private const int BackgroundOrder = -20;
        private const int ForegroundOrder = -10;
        private const float LayerWidth = 1024f;
        private const int SlotShift = 12;
        private const int TileMask = (1 << SlotShift) - 1;
        private static readonly string[] SlotNames = { "common", "level", "sub" };
        // Tilemaps anchor tiles at the cell centre.
        private static readonly Vector2 CenterPivot = new(0.5f, 0.5f);

        [Serializable]
        private sealed class IndexFile {
            public IndexRow[] rows;
        }

        [Serializable]
        private sealed class IndexRow {
            public string Name;
            public string World;
            public int Level;
            public int Area;
            public string TilesetGroup;
        }

        [Serializable]
        private sealed class LevelFile {
            public SettingsRow Settings;
            public EntranceRow[] Entrances;
            public SpriteRow[] Sprites;
            public ViewRow[] Views;
            public ViewRow[] Zones;
            public PathRow[] Paths;
            public PathRow[] ProgressPaths;
        }

        [Serializable]
        private sealed class SettingsRow {
            public int StartEntrance;
            public int MidwayEntrance;
            public int TimeLimit;
            public int Background;
            public int Foreground;
        }

        [Serializable]
        private sealed class EntranceRow {
            public int X, Y, CameraX, CameraY, Number, DestArea, ConnectedPipe, DestEntrance, Type, Settings, EntryView;
        }

        [Serializable]
        private sealed class SpriteRow {
            public int Type, X, Y;
            public int[] Data;
        }

        [Serializable]
        private sealed class ViewRow {
            public int X, Y, Width, Height, Number, CameraId, Music, Lighting;
        }

        [Serializable]
        private sealed class PathRow {
            public int Id;
            public PathPointRow[] Points;
        }

        [Serializable]
        private sealed class PathPointRow {
            public int X, Y;
        }

        private sealed class TileGroup {
            public Dictionary<int, NsmbTile> Tiles = new();
        }

        private sealed class TilesetStore {
            public string Name;
            public NsmbTileGroupAsset Asset;
            public Dictionary<string, NsmbTile> Tiles = new();
            public bool Changed;
        }

        /// <summary>Existing area scenes, tiles and palettes are kept unless overwrite is set, so edits survive.</summary>
        public static void Run(bool overwrite) {
            if (!File.Exists(ExtractedRoot + "/manifest.json") && !RunExtractor()) {
                return;
            }

            AssetDatabase.Refresh();
            string json = File.ReadAllText(LevelsFolder + "/index.json");
            IndexRow[] rows = JsonUtility.FromJson<IndexFile>("{\"rows\":" + json + "}").rows;

            if (overwrite) {
                AssetDatabase.DeleteAsset(TileAssetFolder);
                AssetDatabase.DeleteAsset(PaletteFolder);
            }
            EnsureFolder(TileAssetFolder);
            EnsureFolder(PaletteFolder);
            EnsureFolder(ResourceFolder);
            AssetDatabase.DeleteAsset(LegacyAreaFolder);

            Dictionary<string, ushort[]> grids = new();
            Dictionary<string, HashSet<int>> usedCells = new();
            foreach (IndexRow row in rows) {
                ushort[] grid = ReadGrid(row.Name);
                grids[row.Name] = grid;
                if (!usedCells.TryGetValue(row.TilesetGroup, out HashSet<int> used)) {
                    used = new HashSet<int>();
                    usedCells[row.TilesetGroup] = used;
                }
                foreach (ushort cell in grid) {
                    if (cell != 0) {
                        used.Add(cell);
                    }
                }
                // Not placed in levels, but every block turns into it once hit.
                used.Add(NsmbBlocks.UsedBlockMap16);
            }

            Dictionary<string, TileGroup> groups = new();
            Dictionary<string, TilesetStore> stores = new();
            int done = 0;
            foreach (KeyValuePair<string, HashSet<int>> group in usedCells) {
                EditorUtility.DisplayProgressBar("NSMB DS import", "Tiles " + group.Key, (float) done++ / usedCells.Count);
                groups[group.Key] = BuildTileGroup(group.Key, group.Value, stores);
            }

            AssetDatabase.SaveAssets();
            foreach (TilesetStore store in stores.Values) {
                if (store.Changed || !File.Exists($"{PaletteFolder}/{store.Name}.prefab")) {
                    BuildPalette(store);
                }
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            // Built last: creating a new scene unloads unreferenced objects.
            List<NsmbAreaIndex.Entry> entries = new();
            done = 0;
            int kept = 0;
            foreach (IndexRow row in rows) {
                EditorUtility.DisplayProgressBar("NSMB DS import", "Area " + row.Name, (float) done++ / rows.Length);
                string scenePath = $"{SceneFolder}/{row.World}/{row.Name}.unity";
                if (!overwrite && File.Exists(scenePath)) {
                    kept++;
                    AddLayersToExistingScene(row, scenePath, material);
                } else {
                    EnsureFolder($"{SceneFolder}/{row.World}");
                    BuildAreaScene(row, grids[row.Name], groups[row.TilesetGroup], material, scenePath);
                }
                entries.Add(new NsmbAreaIndex.Entry {
                    name = row.Name, world = row.World, level = row.Level, area = row.Area, scenePath = scenePath,
                    usedBlock = groups[row.TilesetGroup].Tiles[NsmbBlocks.UsedBlockMap16],
                });
            }

            NsmbAreaIndex index = ScriptableObject.CreateInstance<NsmbAreaIndex>();
            index.entries = entries;
            AssetDatabase.CreateAsset(index, ResourceFolder + "/AreaIndex.asset");
            AssetDatabase.SaveAssets();
            EditorUtility.ClearProgressBar();
            Debug.Log($"[NSMBDS] Imported {rows.Length - kept} area scenes, kept {kept} existing, {stores.Count} tilesets.");
        }

        private static bool RunExtractor() {
            string[] roms = Directory.Exists("Roms") ? Directory.GetFiles("Roms", "*USA*.nds") : Array.Empty<string>();
            if (roms.Length == 0) {
                Debug.LogError("[NSMBDS] Nothing extracted yet and no USA ROM in Roms/.");
                return false;
            }

            ProcessStartInfo start = new("dotnet", $"run --project Tools/NsmbExtractor -- extract \"{roms[0]}\"") {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process process = Process.Start(start);
            process.WaitForExit();
            if (process.ExitCode != 0) {
                Debug.LogError($"[NSMBDS] Extractor failed with exit code {process.ExitCode}.");
                return false;
            }
            return true;
        }

        private static ushort[] ReadGrid(string area) {
            byte[] bytes = File.ReadAllBytes($"{LevelsFolder}/{area}.map16");
            ushort[] grid = new ushort[GridWidth * GridHeight];
            Buffer.BlockCopy(bytes, 0, grid, 0, bytes.Length);
            return grid;
        }

        private static TilesetStore GetStore(string name, Dictionary<string, TilesetStore> stores) {
            if (stores.TryGetValue(name, out TilesetStore store)) {
                return store;
            }

            string path = $"{TileAssetFolder}/{name}.asset";
            store = new TilesetStore { Name = name, Asset = AssetDatabase.LoadAssetAtPath<NsmbTileGroupAsset>(path) };
            if (store.Asset == null) {
                store.Asset = ScriptableObject.CreateInstance<NsmbTileGroupAsset>();
                AssetDatabase.CreateAsset(store.Asset, path);
            } else {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path)) {
                    if (asset is NsmbTile tile) {
                        store.Tiles[tile.name.Substring(1)] = tile;
                    }
                }
            }
            stores[name] = store;
            return store;
        }

        // Tiles are shared across tileset combinations: one asset per tile number and distinct pixels.
        private static TileGroup BuildTileGroup(string key, HashSet<int> cells, Dictionary<string, TilesetStore> stores) {
            string[] parts = key.Split('_');
            string[] storeNames = { "common_" + parts[2], "level_" + parts[1], "sub" };
            string folder = $"{TilesetsFolder}/{key}";
            Texture2D[] atlases = new Texture2D[SlotNames.Length];
            Texture2D[] readable = new Texture2D[SlotNames.Length];
            Color32[][] pixels = new Color32[SlotNames.Length][];
            uint[][] behaviors = new uint[SlotNames.Length][];
            for (int slot = 0; slot < SlotNames.Length; slot++) {
                atlases[slot] = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{SlotNames[slot]}.png");
                readable[slot] = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                readable[slot].LoadImage(File.ReadAllBytes($"{folder}/{SlotNames[slot]}.png"));
                pixels[slot] = readable[slot].GetPixels32();
                byte[] bytes = File.ReadAllBytes($"{folder}/{SlotNames[slot]}.behaviors");
                behaviors[slot] = new uint[bytes.Length / 4];
                Buffer.BlockCopy(bytes, 0, behaviors[slot], 0, bytes.Length);
            }

            TileGroup group = new();
            foreach (int cell in cells) {
                int slot = cell >> SlotShift;
                int tile = cell & TileMask;
                Texture2D atlas = atlases[slot];
                int rectX = (tile % AtlasColumns) * TilePixels;
                int rectY = atlas.height - (((tile / AtlasColumns) + 1) * TilePixels);
                uint behavior = tile < behaviors[slot].Length ? behaviors[slot][tile] : 0u;
                uint hash = HashTile(pixels[slot], readable[slot].width, rectX, rectY, behavior);

                TilesetStore store = GetStore(storeNames[slot], stores);
                string id = $"{tile}_{hash:x8}";
                if (!store.Tiles.TryGetValue(id, out NsmbTile asTile)) {
                    Sprite sprite = Sprite.Create(atlas, new Rect(rectX, rectY, TilePixels, TilePixels), CenterPivot, NsmbUnits.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprite.name = "s" + id;
                    asTile = ScriptableObject.CreateInstance<NsmbTile>();
                    asTile.name = "t" + id;
                    asTile.sprite = sprite;
                    asTile.colliderType = Tile.ColliderType.None;
                    asTile.behavior = behavior;
                    AssetDatabase.AddObjectToAsset(sprite, store.Asset);
                    AssetDatabase.AddObjectToAsset(asTile, store.Asset);
                    store.Tiles[id] = asTile;
                    store.Changed = true;
                }
                group.Tiles[cell] = asTile;
            }

            foreach (Texture2D texture in readable) {
                Object.DestroyImmediate(texture);
            }
            return group;
        }

        private static uint HashTile(Color32[] pixels, int width, int originX, int originY, uint behavior) {
            uint hash = 2166136261 ^ behavior;
            for (int y = 0; y < TilePixels; y++) {
                for (int x = 0; x < TilePixels; x++) {
                    Color32 pixel = pixels[((originY + y) * width) + originX + x];
                    hash = (hash ^ (uint) ((pixel.r << 24) | (pixel.g << 16) | (pixel.b << 8) | pixel.a)) * 16777619;
                }
            }
            return hash;
        }

        // One Tile Palette per tileset, laid out like its atlas. A tile that looks different
        // between tileset combinations gets further columns to the right.
        private static void BuildPalette(TilesetStore store) {
            string path = $"{PaletteFolder}/{store.Name}.prefab";
            AssetDatabase.DeleteAsset(path);
            float cell = TilePixels / NsmbUnits.PixelsPerUnit;
            GridPaletteUtility.CreateNewPalette(PaletteFolder, store.Name, GridLayout.CellLayout.Rectangle, GridPalette.CellSizing.Manual,
                new Vector3(cell, cell, 0f), GridLayout.CellSwizzle.XYZ);

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            Tilemap tilemap = contents.GetComponentInChildren<Tilemap>();
            Dictionary<int, int> copies = new();
            List<string> ids = new(store.Tiles.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids) {
                int tile = int.Parse(id.Substring(0, id.IndexOf('_')));
                copies.TryGetValue(tile, out int copy);
                copies[tile] = copy + 1;
                tilemap.SetTile(new Vector3Int((copy * PaletteSlotWidth) + (tile % AtlasColumns), -(tile / AtlasColumns) - 1, 0), store.Tiles[id]);
            }
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static void BuildAreaScene(IndexRow row, ushort[] grid, TileGroup group, Material material, string scenePath) {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LevelFile level = JsonUtility.FromJson<LevelFile>(File.ReadAllText($"{LevelsFolder}/{row.Name}.json"));
            GameObject root = new(row.Name);
            NsmbArea area = root.AddComponent<NsmbArea>();
            area.areaName = row.Name;
            area.startEntrance = level.Settings.StartEntrance;
            area.midwayEntrance = level.Settings.MidwayEntrance;
            area.timeLimit = level.Settings.TimeLimit;

            Grid unityGrid = root.AddComponent<Grid>();
            unityGrid.cellSize = new Vector3(TilePixels / NsmbUnits.PixelsPerUnit, TilePixels / NsmbUnits.PixelsPerUnit, 0f);

            GameObject tiles = new("Tiles");
            tiles.transform.SetParent(root.transform, false);
            Tilemap tilemap = tiles.AddComponent<Tilemap>();
            TilemapRenderer renderer = tiles.AddComponent<TilemapRenderer>();
            renderer.mode = TilemapRenderer.Mode.Chunk;
            if (material != null) {
                renderer.sharedMaterial = material;
            }
            area.tilemap = tilemap;

            FillTilemap(area, grid, group);
            AddLayers(area, level, material);
            AddEntrances(root.transform, level);
            AddSprites(root.transform, level);
            AddRegions(root.transform, "Views", level.Views, false);
            AddRegions(root.transform, "Zones", level.Zones, true);
            AddPaths(root.transform, level.Paths, false);
            AddPaths(root.transform, level.ProgressPaths, true);

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        // Keeps tile edits: the scene is only opened, extended and saved.
        private static void AddLayersToExistingScene(IndexRow row, string scenePath, Material material) {
            Scene scene = EditorSceneManager.OpenScene(scenePath);
            NsmbArea area = Object.FindAnyObjectByType<NsmbArea>();
            if (area == null) {
                return;
            }

            LevelFile level = JsonUtility.FromJson<LevelFile>(File.ReadAllText($"{LevelsFolder}/{row.Name}.json"));
            if (area.backgroundRenderer == null || area.foregroundRenderer == null) {
                AddLayers(area, level, material);
            } else {
                LoadLayerSprites(area, level);
                area.backgroundRenderer.sprite = area.background;
                area.foregroundRenderer.sprite = area.foreground;
            }
            EditorUtility.SetDirty(area);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // Layer files are named by background and foreground, because each can show chars of the other.
        private static void LoadLayerSprites(NsmbArea area, LevelFile level) {
            string pair = $"{level.Settings.Background:D2}_{level.Settings.Foreground:D2}";
            area.background = AssetDatabase.LoadAssetAtPath<Sprite>($"{LayersFolder}/bg_{pair}.png");
            area.foreground = AssetDatabase.LoadAssetAtPath<Sprite>($"{LayersFolder}/fg_{pair}.png");
        }

        private static void AddLayers(NsmbArea area, LevelFile level, Material material) {
            LoadLayerSprites(area, level);

            float bottom = level.Views.Length > 0 ? ToWorld(0f, level.Views[0].Y + level.Views[0].Height).y : 0f;
            area.backgroundRenderer = AddLayer(area.transform, "Background", area.background, BackgroundOrder, bottom, material);
            area.foregroundRenderer = AddLayer(area.transform, "Foreground", area.foreground, ForegroundOrder, bottom, material);
        }

        private static SpriteRenderer AddLayer(Transform parent, string name, Sprite sprite, int order, float bottom, Material material) {
            float height = sprite != null ? sprite.bounds.size.y : 0f;
            SpriteRenderer layer = AddChild(parent, name, new Vector3(0f, bottom + (height / 2f), 0f)).AddComponent<SpriteRenderer>();
            layer.sprite = sprite;
            layer.drawMode = SpriteDrawMode.Tiled;
            layer.size = new Vector2(LayerWidth, height);
            layer.sortingOrder = order;
            if (material != null) {
                layer.sharedMaterial = material;
            }
            return layer;
        }

        private static void FillTilemap(NsmbArea area, ushort[] grid, TileGroup group) {
            List<Vector3Int> positions = new();
            List<TileBase> tiles = new();
            for (int y = 0; y < GridHeight; y++) {
                for (int x = 0; x < GridWidth; x++) {
                    int cell = grid[(y * GridWidth) + x];
                    if (cell == 0) {
                        continue;
                    }

                    positions.Add(new Vector3Int(x, -y - 1, 0));
                    tiles.Add(group.Tiles[cell]);
                }
            }

            area.tilemap.SetTiles(positions.ToArray(), tiles.ToArray());
        }

        private static Vector3 ToWorld(float x, float y) {
            return new Vector3(x / NsmbUnits.PixelsPerUnit, -y / NsmbUnits.PixelsPerUnit, 0f);
        }

        private static GameObject AddChild(Transform parent, string name, Vector3 position) {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            child.transform.position = position;
            return child;
        }

        private static void AddEntrances(Transform root, LevelFile level) {
            Transform parent = AddChild(root, "Entrances", Vector3.zero).transform;
            foreach (EntranceRow row in level.Entrances) {
                NsmbEntranceMarker marker = AddChild(parent, $"Entrance {row.Number}", ToWorld(row.X, row.Y)).AddComponent<NsmbEntranceMarker>();
                marker.number = row.Number;
                marker.type = row.Type;
                marker.destArea = row.DestArea;
                marker.destEntrance = row.DestEntrance;
                marker.connectedPipe = row.ConnectedPipe;
                marker.settings = row.Settings;
                marker.entryView = row.EntryView;
                marker.cameraPixels = new Vector2Int(row.CameraX, row.CameraY);
            }
        }

        private static void AddSprites(Transform root, LevelFile level) {
            Transform parent = AddChild(root, "Sprites", Vector3.zero).transform;
            foreach (SpriteRow row in level.Sprites) {
                NsmbSpriteMarker marker = AddChild(parent, $"Sprite {row.Type}", ToWorld(row.X * TilePixels, row.Y * TilePixels)).AddComponent<NsmbSpriteMarker>();
                marker.type = row.Type;
                marker.data = row.Data;
            }
        }

        private static void AddRegions(Transform root, string name, ViewRow[] rows, bool isZone) {
            Transform parent = AddChild(root, name, Vector3.zero).transform;
            foreach (ViewRow row in rows) {
                NsmbRegionMarker marker = AddChild(parent, $"{name} {row.Number}", ToWorld(row.X, row.Y)).AddComponent<NsmbRegionMarker>();
                marker.number = row.Number;
                marker.isZone = isZone;
                marker.size = new Vector2(row.Width, row.Height) / NsmbUnits.PixelsPerUnit;
                marker.cameraId = row.CameraId;
                marker.music = row.Music;
                marker.lighting = row.Lighting;
            }
        }

        private static void AddPaths(Transform root, PathRow[] rows, bool isProgress) {
            Transform parent = AddChild(root, isProgress ? "ProgressPaths" : "Paths", Vector3.zero).transform;
            foreach (PathRow row in rows) {
                NsmbPathMarker marker = AddChild(parent, $"Path {row.Id}", Vector3.zero).AddComponent<NsmbPathMarker>();
                marker.id = row.Id;
                marker.isProgressPath = isProgress;
                marker.points = new Vector3[row.Points.Length];
                for (int i = 0; i < row.Points.Length; i++) {
                    marker.points[i] = ToWorld(row.Points[i].X, row.Points[i].Y);
                }
            }
        }

        private static void EnsureFolder(string path) {
            if (AssetDatabase.IsValidFolder(path)) {
                return;
            }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NsmbDs {
    /// <summary>Tile interaction for the player: blocks, coins, items, harmful tiles, power states and liquids.</summary>
    [RequireComponent(typeof(NsmbPlayer))]
    public sealed class NsmbStage : MonoBehaviour {
        private const int BumpFrames = NsmbBlocks.HitToUsedFrames;
        private const float BumpHeight = 0.25f;
        private const int HurtInvincibleFrames = 120;
        private const int StarFrames = 600;
        private const int LiquidSpawnReach = 48;
        private const int ExtraLifeCoins = 100;
        private const float LiquidDepth = 40f;
        private const string AssetsResource = "NsmbItems";

        private sealed class Bump {
            public Vector3Int Cell;
            public NsmbTile Tile;
            public int Frames;
            public SpriteRenderer View;
            public NsmbItemKind Item;
            public bool Used;
            public int Column;
            public int Row;
        }

        private readonly List<Bump> bumps = new();
        private readonly Dictionary<Vector3Int, int> multiCoinHits = new();
        private readonly List<(float X, float Y, NsmbLiquidKind Kind)> liquids = new();
        private NsmbPlayer player;
        private NsmbPlayerMotor motor;
        private NsmbItemAssets assets;
        private SpriteRenderer liquidView;
        private NsmbActors actors;
        private int invincibleFrames;
        private int starFrames;

        public int InvincibleFrames => invincibleFrames;
        public NsmbPlayerMotor Motor => motor;
        public NsmbPlayerMotor CurrentMotor => player.Motor;
        public NsmbItemKind LastCollected { get; private set; }
        public NsmbItemAssets Assets => assets;
        public NsmbActors Actors => actors;
        public bool LevelFinished { get; private set; }
        public bool JumpHeld => player.CurrentInput.Jump;
        public bool ManualStepping => player.ManualStepping;

        /// <summary>The art of this area's coin tile, for coins placed as sprites.</summary>
        public Sprite CoinSprite {
            get {
                TileBase[] used = new TileBase[Tilemap.GetUsedTilesCount()];
                Tilemap.GetUsedTilesNonAlloc(used);
                foreach (TileBase tile in used) {
                    if (tile is NsmbTile known && new NsmbTileBehavior(known.behavior).IsCoin) {
                        return known.sprite;
                    }
                }
                return null;
            }
        }
        public bool HasStar => starFrames > 0;

        private void Awake() {
            player = GetComponent<NsmbPlayer>();
            assets = Resources.Load<NsmbItemAssets>(AssetsResource);
            player.FrameStepped += StepFrame;
            actors = new NsmbActors(this);
        }

        private void Follow(NsmbPlayerMotor newMotor) {
            foreach (NsmbItem item in FindObjectsByType<NsmbItem>(FindObjectsSortMode.None)) {
                Destroy(item.gameObject);
            }
            foreach (Bump bump in bumps) {
                Destroy(bump.View.gameObject);
            }
            bumps.Clear();

            motor = newMotor;
            LevelFinished = false;
            motor.HeadHit += OnHeadHit;
            motor.Pounded += OnPounded;
            FindLiquids();
            actors.EnterArea(player.Area);
        }

        private void FindLiquids() {
            liquids.Clear();
            motor.Liquid = NsmbLiquidKind.None;
            motor.LiquidSurfaceY = float.MaxValue;
            foreach (NsmbSpriteMarker marker in player.Area.GetComponentsInChildren<NsmbSpriteMarker>(true)) {
                NsmbLiquidKind kind = NsmbBlocks.LiquidOf(marker.type, out float offset);
                if (kind != NsmbLiquidKind.None) {
                    Vector2 pixels = marker.transform.position * NsmbUnits.PixelsPerUnit;
                    liquids.Add((pixels.x, -pixels.y + offset, kind));
                }
            }
        }

        private void StepFrame() {
            if (player.Motor != motor) {
                Follow(player.Motor);
            }

            UpdateLiquid();
            actors.Step(Camera.main);
            if (player.ManualStepping) {
                actors.StepAllOnce();
            }
            CollectCoins();
            CheckHarmfulTiles();
            StepBumps();
            if (invincibleFrames > 0) {
                invincibleFrames--;
            }
            if (starFrames > 0) {
                starFrames--;
            }
        }

        // The game spawns a liquid sprite as it comes into view, and the newest one replaces the level's surface.
        private void UpdateLiquid() {
            Camera view = Camera.main;
            float left = (view.transform.position.x * NsmbUnits.PixelsPerUnit) - (view.orthographicSize * view.aspect * NsmbUnits.PixelsPerUnit) - LiquidSpawnReach;
            float right = (view.transform.position.x * NsmbUnits.PixelsPerUnit) + (view.orthographicSize * view.aspect * NsmbUnits.PixelsPerUnit) + LiquidSpawnReach;
            foreach ((float x, float y, NsmbLiquidKind kind) in liquids) {
                if (x >= left && x <= right && (motor.Liquid == NsmbLiquidKind.None || NearerToPlayer(x))) {
                    motor.Liquid = kind;
                    motor.LiquidSurfaceY = y;
                    activeLiquidX = x;
                }
            }
            DrawLiquid(view);
        }

        private float activeLiquidX;

        private bool NearerToPlayer(float x) {
            return Mathf.Abs(x - motor.X) < Mathf.Abs(activeLiquidX - motor.X);
        }

        private void DrawLiquid(Camera view) {
            if (liquidView == null) {
                GameObject body = new("Liquid");
                liquidView = body.AddComponent<SpriteRenderer>();
                Texture2D pixel = Texture2D.whiteTexture;
                liquidView.sprite = Sprite.Create(pixel, new Rect(0f, 0f, pixel.width, pixel.height), new Vector2(0.5f, 1f), pixel.width);
                liquidView.sortingOrder = 50;
            }

            liquidView.enabled = motor.Liquid != NsmbLiquidKind.None;
            if (!liquidView.enabled) {
                return;
            }

            liquidView.color = motor.Liquid switch {
                NsmbLiquidKind.Water => new Color(0.25f, 0.55f, 0.95f, 0.45f),
                NsmbLiquidKind.Lava => new Color(1f, 0.35f, 0.05f, 0.95f),
                _ => new Color(0.45f, 0.1f, 0.6f, 0.9f),
            };
            float top = -motor.LiquidSurfaceY / NsmbUnits.PixelsPerUnit;
            liquidView.transform.position = new Vector3(view.transform.position.x, top, -1f);
            liquidView.transform.localScale = new Vector3(LiquidDepth, LiquidDepth, 1f);
        }

        private Tilemap Tilemap => player.Area.tilemap;

        private static Vector3Int CellOf(int column, int row) {
            return new Vector3Int(column, -row - 1, 0);
        }

        private void OnHeadHit(int column, int row) {
            HitTile(column, row, false);
        }

        private void OnPounded(int column, int row) {
            HitTile(column, row, true);
        }

        private void HitTile(int column, int row, bool fromAbove) {
            Vector3Int cell = CellOf(column, row);
            if (Tilemap.GetTile(cell) is not NsmbTile tile) {
                return;
            }

            NsmbTileBehavior behavior = new(tile.behavior);
            if (!NsmbBlocks.IsHittable(behavior)) {
                return;
            }

            NsmbItemKind item = NsmbBlocks.Content(behavior);
            bool plainBrick = behavior.IsBrick && item == NsmbItemKind.None;
            if (plainBrick && motor.IsBig) {
                Tilemap.SetTile(cell, null);
                return;
            }
            if (fromAbove && plainBrick) {
                return;
            }

            bool used = !plainBrick;
            if (item == NsmbItemKind.MultiCoin) {
                multiCoinHits.TryGetValue(cell, out int hits);
                multiCoinHits[cell] = ++hits;
                used = hits >= NsmbBlocks.MultiCoinHits;
            }

            Tilemap.SetTile(cell, null);
            bumps.Add(new Bump { Cell = cell, Tile = tile, Frames = BumpFrames, View = MakeBumpView(tile, cell), Item = item, Used = used, Column = column, Row = row });
            if (item == NsmbItemKind.Coin || item == NsmbItemKind.MultiCoin) {
                AddCoins(1);
            }
        }

        private SpriteRenderer MakeBumpView(NsmbTile tile, Vector3Int cell) {
            GameObject body = new("Bump");
            SpriteRenderer view = body.AddComponent<SpriteRenderer>();
            view.sprite = tile.sprite;
            view.sortingOrder = 20;
            body.transform.position = Tilemap.GetCellCenterWorld(cell);
            return view;
        }

        private void StepBumps() {
            for (int i = bumps.Count - 1; i >= 0; i--) {
                Bump bump = bumps[i];
                bump.Frames--;
                float t = 1f - (bump.Frames / (float) BumpFrames);
                bump.View.transform.position = Tilemap.GetCellCenterWorld(bump.Cell) + (Vector3.up * (Mathf.Sin(t * Mathf.PI) * BumpHeight));
                if (bump.Frames > 0) {
                    continue;
                }

                Destroy(bump.View.gameObject);
                bumps.RemoveAt(i);
                Tilemap.SetTile(bump.Cell, bump.Used ? player.UsedBlock : bump.Tile);
                if (bump.Item != NsmbItemKind.None && bump.Item != NsmbItemKind.Coin && bump.Item != NsmbItemKind.MultiCoin) {
                    NsmbItem.Spawn(bump.Item, bump.Column, bump.Row, player, assets, Collect);
                }
            }
        }

        private void CollectCoins() {
            float height = motor.BodyHeight;
            foreach (float bodyY in new[] { motor.Y - 1f, motor.Y - height + 1f }) {
                foreach (float bodyX in new[] { motor.X - player.tuning.halfWidth, motor.X + player.tuning.halfWidth }) {
                    int column = Mathf.FloorToInt(bodyX / NsmbTileSource.TileSize);
                    int row = Mathf.FloorToInt(bodyY / NsmbTileSource.TileSize);
                    Vector3Int cell = CellOf(column, row);
                    if (Tilemap.GetTile(cell) is NsmbTile tile && new NsmbTileBehavior(tile.behavior).IsCoin) {
                        Tilemap.SetTile(cell, null);
                        AddCoins(1);
                    }
                }
            }
        }

        private void CheckHarmfulTiles() {
            if (invincibleFrames > 0 || starFrames > 0) {
                return;
            }

            NsmbTileBehavior feet = new(TileWord(motor.X, motor.Y + 1f));
            NsmbTileBehavior body = new(TileWord(motor.X, motor.Y - (motor.BodyHeight / 2f)));
            if (feet.IsHarmful || body.IsHarmful) {
                Hurt();
            }
        }

        private uint TileWord(float x, float y) {
            Vector3Int cell = CellOf(Mathf.FloorToInt(x / NsmbTileSource.TileSize), Mathf.FloorToInt(y / NsmbTileSource.TileSize));
            return Tilemap.GetTile(cell) is NsmbTile tile ? tile.behavior : 0u;
        }

        public void FinishLevel() {
            if (!LevelFinished) {
                LevelFinished = true;
                Debug.Log($"[NSMBDS] Level finished: {player.AreaName}, coins {player.Coins}");
            }
        }

        public void AddCoins(int count) {
            player.Coins += count;
            if (player.Coins >= ExtraLifeCoins) {
                player.Coins -= ExtraLifeCoins;
                player.ExtraLives++;
            }
        }

        /// <summary>Enemy contact; ignored while blinking.</summary>
        public void HurtPlayer() {
            if (invincibleFrames == 0) {
                Hurt();
            }
        }

        /// <summary>One hit: big players shrink and blink, small players die.</summary>
        public void Hurt() {
            if (motor.Power == NsmbPower.Small || motor.Power == NsmbPower.Mini) {
                player.Die();
                return;
            }

            motor.Power = motor.Power == NsmbPower.Big ? NsmbPower.Small : NsmbPower.Big;
            invincibleFrames = HurtInvincibleFrames;
        }

        private void Collect(NsmbItemKind item) {
            LastCollected = item;
            motor.Power = NsmbBlocks.Collect(motor.Power, item);
            if (item == NsmbItemKind.Star) {
                starFrames = StarFrames;
            } else if (item == NsmbItemKind.OneUp) {
                player.ExtraLives++;
            }
        }
    }
}

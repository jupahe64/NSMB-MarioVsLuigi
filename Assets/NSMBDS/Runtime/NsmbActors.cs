using System;
using System.Collections.Generic;
using UnityEngine;

namespace NsmbDs {
    /// <summary>Spawns actors from the level's sprites around the camera, and removes them again out of range, as the game does.</summary>
    public sealed class NsmbActors {
        private const float SpawnLeft = 32f;
        private const float SpawnRight = 48f;
        private const float SpawnTop = 16f;
        private const float SpawnBottom = 32f;
        private const float DespawnMargin = 64f;

        private static readonly Dictionary<int, Type> Registry = new() {
            { 148, typeof(NsmbGoomba) },
            { 149, typeof(NsmbKoopa) },
            { 150, typeof(NsmbKoopa) },
            { 23, typeof(NsmbPiranha) },
            { 24, typeof(NsmbPiranha) },
            { 32, typeof(NsmbGoalPole) },
            { 57, typeof(NsmbCoinSprite) },
        };

        private readonly NsmbStage stage;
        private readonly List<NsmbActor> alive = new();
        private readonly HashSet<NsmbSpriteMarker> inRange = new();
        private NsmbSpriteMarker[] markers;

        public NsmbActors(NsmbStage stage) {
            this.stage = stage;
        }

        public int AliveCount => alive.Count;

        public void EnterArea(NsmbArea area) {
            Clear();
            List<NsmbSpriteMarker> known = new();
            foreach (NsmbSpriteMarker marker in area.GetComponentsInChildren<NsmbSpriteMarker>(true)) {
                if (Registry.ContainsKey(marker.type)) {
                    known.Add(marker);
                }
            }
            markers = known.ToArray();
        }

        public void Clear() {
            foreach (NsmbActor actor in alive) {
                if (actor != null) {
                    UnityEngine.Object.Destroy(actor.gameObject);
                }
            }
            alive.Clear();
            inRange.Clear();
        }

        public void Step(Camera view) {
            float halfWidth = view.orthographicSize * view.aspect * NsmbUnits.PixelsPerUnit;
            float halfHeight = view.orthographicSize * NsmbUnits.PixelsPerUnit;
            float cameraX = view.transform.position.x * NsmbUnits.PixelsPerUnit;
            float cameraY = -view.transform.position.y * NsmbUnits.PixelsPerUnit;
            Rect spawn = Rect.MinMaxRect(cameraX - halfWidth - SpawnLeft, cameraY - halfHeight - SpawnTop, cameraX + halfWidth + SpawnRight, cameraY + halfHeight + SpawnBottom);

            foreach (NsmbSpriteMarker marker in markers) {
                Vector2 pixels = marker.transform.position * NsmbUnits.PixelsPerUnit;
                Vector2 position = new(pixels.x, -pixels.y);
                bool nowInRange = spawn.Contains(position);
                // Only entering the range spawns, so an actor that left its spot stays gone until the camera has moved away.
                if (nowInRange && inRange.Add(marker)) {
                    Spawn(marker, position);
                } else if (!nowInRange) {
                    inRange.Remove(marker);
                }
            }

            Rect keep = new(spawn.x - DespawnMargin, spawn.y - DespawnMargin, spawn.width + (DespawnMargin * 2f), spawn.height + (DespawnMargin * 2f));
            for (int i = alive.Count - 1; i >= 0; i--) {
                NsmbActor actor = alive[i];
                if (actor == null) {
                    alive.RemoveAt(i);
                } else if (!keep.Contains(new Vector2(actor.X, actor.Y))) {
                    UnityEngine.Object.Destroy(actor.gameObject);
                    alive.RemoveAt(i);
                }
            }
        }

        public void DefeatTouching(NsmbActor source) {
            foreach (NsmbActor actor in alive) {
                if (actor != null && actor != source && !actor.Defeated && source.Touches(actor)) {
                    actor.DefeatByActor();
                }
            }
        }

        private void Spawn(NsmbSpriteMarker marker, Vector2 position) {
            GameObject body = new($"Actor {marker.type}");
            NsmbActor actor = (NsmbActor) body.AddComponent(Registry[marker.type]);
            actor.Setup(stage, marker, position.x + (NsmbTileSource.TileSize / 2f), position.y + NsmbTileSource.TileSize);
            alive.Add(actor);
        }
    }
}

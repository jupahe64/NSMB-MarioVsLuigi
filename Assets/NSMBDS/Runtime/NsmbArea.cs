using UnityEngine;
using UnityEngine.Tilemaps;

namespace NsmbDs {
    /// <summary>Root object of an area scene.</summary>
    public sealed class NsmbArea : MonoBehaviour {
        public string areaName;
        public Tilemap tilemap;
        public int startEntrance;
        public int midwayEntrance;
        public int timeLimit;

        [Header("Backdrop")]
        public Sprite background;
        public Sprite foreground;
        [Tooltip("How much of the camera movement each layer follows: 0 stays on screen, 1 scrolls with the level.")]
        public float backgroundParallax = 0.25f;
        public float foregroundParallax = 0.5f;
        public SpriteRenderer backgroundRenderer;
        public SpriteRenderer foregroundRenderer;

        private bool overlayVisible;
        private NsmbRegionMarker[] views;

        public bool OverlayVisible => overlayVisible;

        private void OnValidate() {
            if (backgroundRenderer != null) {
                backgroundRenderer.sprite = background;
            }
            if (foregroundRenderer != null) {
                foregroundRenderer.sprite = foreground;
            }
        }

        private void LateUpdate() {
            Camera view = Camera.main;
            if (view == null) {
                return;
            }

            views ??= GetComponentsInChildren<NsmbRegionMarker>();
            Vector3 camera = view.transform.position;
            float bottom = FindViewBottom(camera);
            PlaceLayer(backgroundRenderer, camera.x * (1f - backgroundParallax), bottom);
            PlaceLayer(foregroundRenderer, camera.x * (1f - foregroundParallax), bottom);
        }

        private float FindViewBottom(Vector3 camera) {
            float bottom = 0f;
            foreach (NsmbRegionMarker region in views) {
                if (region.isZone) {
                    continue;
                }
                Rect rect = region.WorldRect;
                bottom = rect.yMin;
                if (rect.Contains(camera)) {
                    break;
                }
            }
            return bottom;
        }

        private static void PlaceLayer(SpriteRenderer layer, float x, float bottom) {
            if (layer != null && layer.sprite != null) {
                layer.transform.position = new Vector3(x, bottom + (layer.sprite.bounds.size.y / 2f), 0f);
            }
        }

        public NsmbEntranceMarker FindEntrance(int number) {
            foreach (NsmbEntranceMarker entrance in GetComponentsInChildren<NsmbEntranceMarker>()) {
                if (entrance.number == number) {
                    return entrance;
                }
            }
            return null;
        }

        /// <summary>Tints every tile by the class in its behaviour word, for inspecting collision data.</summary>
        public void SetBehaviorOverlay(bool visible) {
            overlayVisible = visible;
            foreach (Vector3Int position in tilemap.cellBounds.allPositionsWithin) {
                if (tilemap.GetTile(position) is NsmbTile tile) {
                    tilemap.SetTileFlags(position, TileFlags.None);
                    tilemap.SetColor(position, visible ? OverlayColor(tile.behavior) : Color.white);
                }
            }
        }

        private static Color OverlayColor(uint behavior) {
            if (behavior == 0) {
                return new Color(1f, 1f, 1f, 0.35f);
            }

            uint group = behavior >> 16;
            float hue = (group * 0.61803398875f) % 1f;
            return Color.HSVToRGB(hue, 0.75f, 1f);
        }
    }
}

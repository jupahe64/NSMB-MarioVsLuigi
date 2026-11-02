using UnityEngine;

namespace NsmbDs {
    public sealed class NsmbEntranceMarker : MonoBehaviour {
        public int number;
        public int type;
        public int destArea;
        public int destEntrance;
        public int connectedPipe;
        public int settings;
        public int entryView;
        public Vector2Int cameraPixels;

        private void OnDrawGizmos() {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + new Vector3(0.25f, -0.25f), new Vector3(0.5f, 0.5f));
        }
    }
}

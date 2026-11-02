using UnityEngine;

namespace NsmbDs {
    public sealed class NsmbSpriteMarker : MonoBehaviour {
        public int type;
        public int[] data;

        private void OnDrawGizmos() {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + new Vector3(0.25f, -0.25f), new Vector3(0.4f, 0.4f));
        }
    }
}

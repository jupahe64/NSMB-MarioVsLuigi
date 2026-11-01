using UnityEditor;
using UnityEngine;

namespace NsmbDs.Editor {
    public static class NsmbImportMenu {
        [MenuItem("NSMB DS/Import")]
        private static void Import() {
            Debug.Log($"[NSMBDS] Import is not implemented yet (M3). DS pixels per unit: {NsmbUnits.PixelsPerUnit}");
        }
    }
}

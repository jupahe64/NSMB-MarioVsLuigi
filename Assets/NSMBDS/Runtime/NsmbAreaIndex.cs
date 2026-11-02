using System;
using System.Collections.Generic;
using UnityEngine;

namespace NsmbDs {
    /// <summary>Generated list of imported areas; committed code finds it by resource name, never by GUID.</summary>
    public sealed class NsmbAreaIndex : ScriptableObject {
        public const string ResourcePath = "NSMBDS/AreaIndex";

        [Serializable]
        public struct Entry {
            public string name;
            public string world;
            public int level;
            public int area;
            public string scenePath;
            public NsmbTile usedBlock;
        }

        public List<Entry> entries = new();

        public static NsmbAreaIndex Load() {
            return Resources.Load<NsmbAreaIndex>(ResourcePath);
        }
    }
}

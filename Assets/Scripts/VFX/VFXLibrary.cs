using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>Id → prefab table for every visual effect (built by Tools/RPG/Build VFX).</summary>
    [CreateAssetMenu(menuName = "RPG/VFX Library")]
    public class VFXLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string id;
            public GameObject prefab;
        }

        public List<Entry> entries = new List<Entry>();
        Dictionary<string, GameObject> map;

        public GameObject Get(string id)
        {
            if (map == null || map.Count != entries.Count)
            {
                map = new Dictionary<string, GameObject>();
                foreach (var e in entries)
                    if (e != null && e.prefab != null && !string.IsNullOrEmpty(e.id)) map[e.id] = e.prefab;
            }
            map.TryGetValue(id, out var p);
            return p;
        }

        void OnValidate() => map = null;
    }
}

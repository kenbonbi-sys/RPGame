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

        [Header("Budget per effect (plan §13), checked in the VFX Gallery")]
        [Tooltip("Most particles alive at once in one effect.")]
        public int particleBudget = 150;
        [Tooltip("Most Light2D in one effect.")]
        public int lightBudget = 1;
        [Tooltip("Effects of a Tuyệt kỹ get twice the budget.")]
        public List<string> ultimateIds = new List<string> { "storm_circle", "lightning_strike" };

        Dictionary<string, GameObject> map;

        public bool IsUltimate(string id) => ultimateIds != null && ultimateIds.Contains(id);
        public int ParticleLimit(string id) => IsUltimate(id) ? particleBudget * 2 : particleBudget;
        public int LightLimit(string id) => IsUltimate(id) ? lightBudget * 2 : lightBudget;

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

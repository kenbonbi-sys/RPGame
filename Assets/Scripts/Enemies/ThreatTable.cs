using System;
using System.Collections.Generic;

namespace RPG
{
    /// <summary>
    /// Who an enemy is angry with. Damage adds threat (as much as it dealt) and a hero who walks
    /// into the aggro range adds a little; the enemy goes after the living hero with the most.
    /// With a single hero this picks the same target as before heroes could be many.
    /// </summary>
    public class ThreatTable
    {
        readonly Dictionary<PlayerController, float> threat = new Dictionary<PlayerController, float>();
        readonly List<PlayerController> gone = new List<PlayerController>();

        public int Count => threat.Count;

        public void Add(PlayerController p, float amount)
        {
            if (p == null || amount <= 0f) return;
            threat.TryGetValue(p, out float t);
            threat[p] = t + amount;
        }

        public float Of(PlayerController p) => p != null && threat.TryGetValue(p, out float t) ? t : 0f;

        public void Clear() => threat.Clear();

        /// <summary>
        /// Forgets heroes that are gone, dead or fail <paramref name="keep"/> (out of range), then
        /// returns the one with the most threat; null when nobody is left.
        /// </summary>
        public PlayerController Top(Func<PlayerController, bool> keep = null)
        {
            gone.Clear();
            PlayerController best = null;
            float most = float.MinValue;
            foreach (var kv in threat)
            {
                var p = kv.Key;
                if (p == null || p.IsDead || (keep != null && !keep(p)))
                {
                    gone.Add(p);
                    continue;
                }
                if (kv.Value > most)
                {
                    most = kv.Value;
                    best = p;
                }
            }
            foreach (var p in gone) threat.Remove(p);
            return best;
        }
    }
}

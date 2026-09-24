using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Circular region with a name. A hero entering it reaches that place (Reach objectives);
    /// for the hero on this screen it also shows the zone title and updates the minimap label.
    /// </summary>
    public class ZoneArea : MonoBehaviour
    {
        /// <summary>The area the hero on this screen is in.</summary>
        public static ZoneArea Current { get; private set; }
        static readonly List<ZoneArea> All = new List<ZoneArea>();
        static readonly Dictionary<PlayerController, ZoneArea> Inside = new Dictionary<PlayerController, ZoneArea>();

        public string zoneName = "Rừng Thì Thầm";
        public float radius = 12f;
        [Tooltip("Higher priority wins when zones overlap.")]
        public int priority;
        public string music = "music_forest";

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>The area at a point (the highest priority one when they overlap), or null.</summary>
        public static ZoneArea At(Vector2 pos)
        {
            ZoneArea best = null;
            foreach (var z in All)
            {
                if (Vector2.Distance(pos, z.transform.position) > z.radius) continue;
                if (best == null || z.priority > best.priority) best = z;
            }
            return best;
        }

        /// <summary>Checks which area a hero is in; entering a new one counts as reaching it.</summary>
        public static void Tick(PlayerController p)
        {
            if (p == null) return;
            var best = At(p.transform.position);
            Inside.TryGetValue(p, out var was);
            if (best == null || best == was) return;
            Inside[p] = best;
            if (p.quests != null) p.quests.NotifyReached(best.zoneName);
            if (!p.IsLocal) return;
            Current = best;
            GameEvents.RaiseZoneEntered(best.zoneName);
        }

        /// <summary>Forgets where heroes were (the game scene boots).</summary>
        public static void Reset()
        {
            Inside.Clear();
            Current = null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

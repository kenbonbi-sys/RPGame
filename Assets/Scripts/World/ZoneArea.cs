using UnityEngine;

namespace RPG
{
    /// <summary>Circular region with a name. Entering shows the zone title and updates the minimap label.</summary>
    public class ZoneArea : MonoBehaviour
    {
        public static ZoneArea Current { get; private set; }
        static readonly System.Collections.Generic.List<ZoneArea> All = new System.Collections.Generic.List<ZoneArea>();

        public string zoneName = "Rừng Thì Thầm";
        public float radius = 12f;
        [Tooltip("Higher priority wins when zones overlap.")]
        public int priority;
        public string music = "music_forest";

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static void Tick(Vector2 playerPos)
        {
            ZoneArea best = null;
            foreach (var z in All)
            {
                if (Vector2.Distance(playerPos, z.transform.position) > z.radius) continue;
                if (best == null || z.priority > best.priority) best = z;
            }
            if (best != null && best != Current)
            {
                Current = best;
                GameEvents.RaiseZoneEntered(best.zoneName);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// A named region: a circle, or a rectangle when <see cref="size"/> is set. A hero entering it
    /// reaches that place (Reach objectives); for the hero on this screen it also shows the zone
    /// title, updates the minimap label and brings in the place's music, ambience, light tint,
    /// mist and wind (the world is one seamless map: regions change as the hero walks).
    /// </summary>
    public class ZoneArea : MonoBehaviour
    {
        /// <summary>The area the hero on this screen is in.</summary>
        public static ZoneArea Current { get; private set; }
        static readonly List<ZoneArea> All = new List<ZoneArea>();
        static readonly Dictionary<PlayerController, ZoneArea> Inside = new Dictionary<PlayerController, ZoneArea>();

        public string zoneName = "Rừng Thì Thầm";
        public float radius = 12f;
        [Tooltip("Width and height of a rectangular area centred here (0: the circle of the radius).")]
        public Vector2 size;
        [Tooltip("Higher priority wins when zones overlap.")]
        public int priority;
        [Tooltip("Music while the hero is here (empty: keep what plays).")]
        public string music = "music_forest";
        [Tooltip("Ambience loop while the hero is here (empty: keep what plays).")]
        public string ambience;
        [Tooltip("Colour the daylight takes here (white: none).")]
        public Color tint = Color.white;
        [Tooltip("Low mist drifting over the ground here, 0–1.")]
        [Range(0f, 1f)] public float mist;
        [Tooltip("Under the rock (a cave), 0–1: the tint is the whole light here, whatever the hour, and every " +
                 "lamp and the heroes' own light burn as they do at night.")]
        [Range(0f, 1f)] public float underground;
        [Tooltip("How hard the wind blows here, 0-1 (Thảo Nguyên Gió): it pushes walkers and bends shots (Wind).")]
        [Range(0f, 1f)] public float windy;

        void OnEnable()
        {
            All.Add(this);
            if (windy > 0f) Wind.Register(this);
        }

        void OnDisable()
        {
            All.Remove(this);
            Wind.Unregister(this);
        }

        public bool Contains(Vector2 pos)
        {
            Vector2 c = transform.position;
            if (size.x > 0f && size.y > 0f) return Mathf.Abs(pos.x - c.x) <= size.x * 0.5f && Mathf.Abs(pos.y - c.y) <= size.y * 0.5f;
            return (pos - c).sqrMagnitude <= radius * radius;
        }

        /// <summary>The area at a point (the highest priority one when they overlap), or null.</summary>
        public static ZoneArea At(Vector2 pos)
        {
            ZoneArea best = null;
            foreach (var z in All)
            {
                if (!z.Contains(pos)) continue;
                if (best == null || z.priority > best.priority) best = z;
            }
            return best;
        }

        /// <summary>The first area of that name (tools, tests), or null.</summary>
        public static ZoneArea Named(string zoneName) => All.Find(z => z != null && z.zoneName == zoneName);

        /// <summary>
        /// Tint, mist and how far underground the region the hero on this screen is in is (the
        /// region it belongs to when a smaller place has none).
        /// </summary>
        public static void Mood(out Color tint, out float mist, out float underground)
        {
            tint = Color.white;
            mist = 0f;
            underground = 0f;
            var me = Players.Local;
            if (me == null) return;
            MoodAt(me.transform.position, out tint, out mist, out underground);
        }

        /// <summary>The mood of the region at a point (see <see cref="Mood"/>).</summary>
        public static void MoodAt(Vector2 pos, out Color tint, out float mist, out float underground)
        {
            tint = Color.white;
            mist = 0f;
            underground = 0f;
            // the largest area with a mood around the point: a village inside the swamp keeps the swamp's light
            float best = -1f;
            foreach (var z in All)
            {
                if (z.tint == Color.white && z.mist <= 0f && z.underground <= 0f || !z.Contains(pos)) continue;
                float area = z.size.x > 0f ? z.size.x * z.size.y : Mathf.PI * z.radius * z.radius;
                if (area <= best) continue;
                best = area;
                tint = z.tint;
                mist = z.mist;
                underground = z.underground;
            }
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
            if (!GameSession.HasScreen) return;
            // a boss fight keeps its own music; it goes back to the place's when it ends
            if (!string.IsNullOrEmpty(best.music) && !BossBase.FightShown) AudioManager.PlayMusic(best.music, 3f);
            if (!string.IsNullOrEmpty(best.ambience)) AudioManager.PlayAmbience(best.ambience);
        }

        /// <summary>Forgets where heroes were (the game scene boots). Not "Reset": Unity would take it for the component's own.</summary>
        public static void ForgetAll()
        {
            Inside.Clear();
            Current = null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
            if (size.x > 0f && size.y > 0f) Gizmos.DrawWireCube(transform.position, new Vector3(size.x, size.y, 0f));
            else Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

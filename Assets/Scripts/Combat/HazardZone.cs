using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// A patch of ground that hurts the heroes standing in it for a while: the swamp's poison
    /// pools (Cóc Tía's lobbed spit, the snake mother's venom). It only exists where the world's
    /// rules run (offline, a server); every screen sees the pool as the "venom_pool" effect,
    /// which lasts <see cref="PoolSeconds"/>.
    /// </summary>
    public class HazardZone : MonoBehaviour
    {
        /// <summary>How long a poison pool stays (its effect is made to last as long).</summary>
        public const float PoolSeconds = 6f;
        /// <summary>The radius the "venom_pool" effect is drawn at when its scale is 1.</summary>
        public const float PoolEffectRadius = 1.4f;

        public float radius = 1.4f;
        public float tickSeconds = 0.75f;
        [System.NonSerialized] public DamageInfo hit;

        float until;
        float nextTick;
        static readonly List<PlayerController> Buffer = new List<PlayerController>();

        /// <summary>Pools on the ground now (tests, AI that wants to pull heroes into them).</summary>
        public static readonly List<HazardZone> All = new List<HazardZone>();

        /// <summary>
        /// A poison pool at <paramref name="at"/>: every tick, each hero in it takes
        /// <paramref name="damage"/> and <paramref name="poison"/> stacks of Độc. Returns the pool
        /// where the rules run, null on a screen that only shows it.
        /// </summary>
        public static HazardZone Poison(Vector2 at, float radius, float damage, int poison, GameObject source, string skill)
        {
            NetCues.Vfx("venom_pool", at, 0f, radius / PoolEffectRadius);
            if (!GameSession.IsAuthority) return null;
            var go = new GameObject("PoisonPool");
            go.transform.position = at;
            var z = go.AddComponent<HazardZone>();
            z.radius = radius;
            z.until = Time.time + PoolSeconds;
            z.nextTick = Time.time + 0.25f;
            var d = DamageInfo.Make(damage, Team.Enemy, source, at, Vector2.down, DamageType.Poison);
            d.dot = true;   // no stagger, no perfect dodge: step out of it
            d.status.poison = poison;
            d.skillName = skill;
            z.hit = d;
            return z;
        }

        /// <summary>Whether a point is inside any pool.</summary>
        public static bool AnyAt(Vector2 p)
        {
            foreach (var z in All)
                if (z != null && ((Vector2)z.transform.position - p).sqrMagnitude <= z.radius * z.radius) return true;
            return false;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            float now = Time.time;
            if (now >= until)
            {
                Destroy(gameObject);
                return;
            }
            if (now < nextTick) return;
            nextTick = now + tickSeconds;
            Players.Within(transform.position, radius, Buffer);
            foreach (var p in Buffer)
            {
                if (p == null || p.IsDead) continue;
                var d = hit;
                d.point = p.transform.position;
                p.health.TakeDamage(d);
            }
        }
    }
}

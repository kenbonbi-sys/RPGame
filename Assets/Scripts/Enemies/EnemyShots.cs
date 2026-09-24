using UnityEngine;

namespace RPG
{
    /// <summary>
    /// The swamp's poison shots, shared by its creatures: a straight spit (Cóc Độc, the snake
    /// mother) and a lobbed glob that leaves a poison pool where it lands (Cóc Tía). The copy
    /// fired where the world's rules run deals the damage; players' screens get one to watch.
    /// </summary>
    public static class EnemyShots
    {
        /// <summary>A poison spit from <paramref name="start"/> toward <paramref name="target"/>, adding <paramref name="poison"/> stacks of Độc.</summary>
        public static Projectile Venom(GameObject source, Vector2 start, Vector2 target, float damage, float speed, int poison,
                                       string skill = null, float lifetime = 1.8f)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db == null || db.venomPrefab == null) return null;
            var go = Pool.Get(db.venomPrefab, start, Quaternion.identity);
            var fx = go.GetComponent<PooledFX>();
            if (fx != null) fx.Persistent = true;
            var pr = go.GetComponent<Projectile>();
            pr.team = Team.Enemy;
            pr.damage = damage;
            pr.speed = speed;
            pr.damageType = DamageType.Poison;
            pr.status = new StatusHit { poison = poison };
            pr.hitVfx = "venom_hit";
            pr.hitSfx = "sfx_splat";
            pr.shake = 0.05f;
            pr.lifetime = lifetime;
            pr.critChance = 0f;
            pr.explodeRadius = 0f;
            pr.knockback = 2f;
            pr.pierce = false;
            pr.skillName = skill;
            pr.Launch(target - start, source);
            NetCues.Projectile("venom", start, target - start, pr.speed, pr.lifetime, Team.Enemy, pr.hitVfx, pr.hitSfx, pr.shake, 0f);
            return pr;
        }

        /// <summary>
        /// A sticky ball of web (Nhện Hang) from <paramref name="start"/> toward <paramref name="target"/>:
        /// whoever it hits is bound (Trói) for <paramref name="root"/> seconds.
        /// </summary>
        public static Projectile Web(GameObject source, Vector2 start, Vector2 target, float damage, float speed, float root,
                                     string skill = null, float lifetime = 1.3f)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db == null || db.webPrefab == null) return null;
            var go = Pool.Get(db.webPrefab, start, Quaternion.identity);
            var fx = go.GetComponent<PooledFX>();
            if (fx != null) fx.Persistent = true;
            var pr = go.GetComponent<Projectile>();
            pr.team = Team.Enemy;
            pr.damage = damage;
            pr.speed = speed;
            pr.damageType = DamageType.Physical;
            pr.status = new StatusHit { root = root };
            pr.hitVfx = "web_hit";
            pr.hitSfx = "sfx_web";
            pr.shake = 0.04f;
            pr.lifetime = lifetime;
            pr.critChance = 0f;
            pr.explodeRadius = 0f;
            pr.knockback = 0f;
            pr.pierce = false;
            pr.skillName = skill;
            pr.Launch(target - start, source);
            NetCues.Projectile("web", start, target - start, pr.speed, pr.lifetime, Team.Enemy, pr.hitVfx, pr.hitSfx, pr.shake, 0f);
            return pr;
        }

        /// <summary>
        /// A glob lobbed onto <paramref name="target"/> in <paramref name="flight"/> seconds: it splashes
        /// everyone within <paramref name="radius"/> and leaves a poison pool (<see cref="HazardZone"/>).
        /// </summary>
        public static void VenomArc(GameObject source, Vector2 start, Vector2 target, float flight, float damage, float radius,
                                    int poison, string skill)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db == null || db.venomArcPrefab == null) return;
            var go = Pool.Get(db.venomArcPrefab, start, Quaternion.identity);
            var fx = go.GetComponent<PooledFX>();
            if (fx != null) fx.Persistent = true;
            NetCues.Arc(start, target, flight, "venom");
            go.GetComponent<ArcProjectile>().Launch(start, target, flight, at =>
            {
                NetCues.Vfx("venom_hit", at, 0f, 1.3f);
                NetCues.Sound("sfx_splat", 0.7f, 0.1f, at);
                var d = DamageInfo.Make(damage, Team.Enemy, source, at, Vector2.down, DamageType.Poison, 3f);
                d.status.poison = poison;
                d.skillName = skill;
                Combat.DamageCircle(at, radius, d);
                HazardZone.Poison(at, radius, Mathf.Max(2f, damage * 0.2f), 1, source, skill);
            });
        }

        /// <summary>
        /// Shortest distance from <paramref name="p"/> to the segment <paramref name="a"/>–<paramref name="b"/>
        /// (a tongue, a charge's path).
        /// </summary>
        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len = ab.sqrMagnitude;
            float t = len > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>A dotted ground warning along a line (a tongue, a charge), <paramref name="step"/> apart.</summary>
        public static void WarnLine(Component source, Vector2 from, Vector2 dir, float length, float radius, float step, float duration,
                                    System.Action<Telegraph> keep = null)
        {
            dir = dir.sqrMagnitude > 0f ? dir.normalized : Vector2.right;
            for (float s = step * 0.5f; s <= length; s += step)
            {
                var t = NetCues.Circle(source, from + dir * s, radius, duration);
                keep?.Invoke(t);
            }
        }
    }
}

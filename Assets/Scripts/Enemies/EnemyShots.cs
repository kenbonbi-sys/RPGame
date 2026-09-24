using UnityEngine;

namespace RPG
{
    /// <summary>
    /// The enemies' shots, shared by their creatures: the swamp's poison spit (Cóc Độc, the snake
    /// mother) and lobbed glob that leaves a poison pool (Cóc Tía), the cave's balls of web and
    /// splinters of crystal, and beams of light that bounce off rock (Mắt Hang, the cave's bosses).
    /// The copy fired where the world's rules run deals the damage; players' screens get one to watch.
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
        /// A splinter of crystal (Slime Pha Lê bursting, a shot turned back by crystal, the old
        /// golem's Mưa Mảnh) from <paramref name="start"/> toward <paramref name="target"/>.
        /// </summary>
        public static Projectile Shard(GameObject source, Vector2 start, Vector2 target, float damage, float speed,
                                       string skill = null, float lifetime = 1.2f)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db == null || db.shardPrefab == null) return null;
            var go = Pool.Get(db.shardPrefab, start, Quaternion.identity);
            var fx = go.GetComponent<PooledFX>();
            if (fx != null) fx.Persistent = true;
            var pr = go.GetComponent<Projectile>();
            pr.team = Team.Enemy;
            pr.damage = damage;
            pr.speed = speed;
            pr.damageType = DamageType.Physical;
            pr.status = default;
            pr.hitVfx = "crystal_hit";
            pr.hitSfx = "sfx_crystal";
            pr.shake = 0.04f;
            pr.lifetime = lifetime;
            pr.critChance = 0f;
            pr.explodeRadius = 0f;
            pr.knockback = 2.5f;
            pr.pierce = false;
            pr.skillName = skill;
            pr.Launch(target - start, source);
            NetCues.Projectile("shard", start, target - start, pr.speed, pr.lifetime, Team.Enemy, pr.hitVfx, pr.hitSfx, pr.shake, 0f);
            return pr;
        }

        /// <summary>
        /// Where a beam of light goes: from <paramref name="from"/> along its direction until it
        /// meets rock (or anything solid), then, once, off it like off a mirror (crystal pillars
        /// too) for the rest of its length. <see cref="bounced"/>: the second leg exists.
        /// </summary>
        public struct BeamPath
        {
            public Vector2 a, b, c;
            public bool bounced;
            /// <summary>What it bounced off (null: nothing).</summary>
            public Collider2D mirror;

            /// <summary>Distance from a point to the beam (either leg).</summary>
            public float DistanceTo(Vector2 p)
            {
                float d = DistanceToSegment(p, a, b);
                return bounced ? Mathf.Min(d, DistanceToSegment(p, b, c)) : d;
            }
        }

        /// <summary>
        /// The path of a beam <paramref name="length"/> long; <paramref name="ignore"/> (the one
        /// shooting it) is not a mirror.
        /// </summary>
        public static BeamPath Trace(Vector2 from, Vector2 dir, float length, bool bounce = true, GameObject ignore = null)
        {
            dir = dir.sqrMagnitude > 0f ? dir.normalized : Vector2.right;
            var path = new BeamPath { a = from };
            var hit = FirstSolid(from, dir, length, ignore);
            if (hit.collider == null)
            {
                path.b = path.c = from + dir * length;
                return path;
            }
            path.b = hit.point;
            if (!bounce)
            {
                path.c = path.b;
                return path;
            }
            path.bounced = true;
            path.mirror = hit.collider;
            Vector2 r = Vector2.Reflect(dir, hit.normal);
            float rest = Mathf.Max(0f, length - hit.distance);
            var hit2 = FirstSolid(hit.point + r * 0.05f, r, rest, ignore);
            path.c = hit2.collider != null ? hit2.point : hit.point + r * rest;
            return path;
        }

        static readonly RaycastHit2D[] RayHits = new RaycastHit2D[8];

        /// <summary>The nearest solid thing along a ray: rock, a prop, a pillar; not a creature, not <paramref name="ignore"/>.</summary>
        static RaycastHit2D FirstSolid(Vector2 from, Vector2 dir, float length, GameObject ignore)
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(Layers.ObstacleMask);
            filter.useTriggers = false;
            int n = Physics2D.Raycast(from, dir, filter, RayHits, length);
            RaycastHit2D best = default;
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = RayHits[i].collider;
                if (c == null || RayHits[i].distance < 0.01f) continue;
                if (ignore != null && c.transform.IsChildOf(ignore.transform)) continue;
                if (c.GetComponentInParent<EnemyBase>() != null || c.GetComponentInParent<BossBase>() != null ||
                    c.GetComponentInParent<PlayerController>() != null) continue;
                if (RayHits[i].distance < bestD)
                {
                    bestD = RayHits[i].distance;
                    best = RayHits[i];
                }
            }
            return best;
        }

        /// <summary>The ground warning of a beam: dotted along both legs.</summary>
        public static void WarnBeam(Component source, BeamPath path, float radius, float duration, System.Action<Telegraph> keep = null)
        {
            Vector2 ab = path.b - path.a;
            WarnLine(source, path.a, ab, ab.magnitude, radius, radius * 2.2f, duration, keep);
            if (!path.bounced) return;
            Vector2 bc = path.c - path.b;
            WarnLine(source, path.b, bc, bc.magnitude, radius, radius * 2.2f, duration, keep);
        }

        /// <summary>
        /// Fires a beam along <paramref name="path"/>: every hero within <paramref name="width"/>
        /// of it is hit once (<paramref name="template"/>), every screen near it draws it.
        /// Returns how many heroes it hit.
        /// </summary>
        public static int Beam(BeamPath path, float width, float seconds, Color color, DamageInfo template)
        {
            NetCues.Beam(path.a, path.b, width, seconds, color);
            if (path.bounced) NetCues.Beam(path.b, path.c, width, seconds, color);
            int n = 0;
            foreach (var h in Players.All)
            {
                if (h == null || h.IsDead) continue;
                Vector2 at = (Vector2)h.transform.position + Vector2.up * 0.35f;
                if (path.DistanceTo(at) > width) continue;
                var d = template;
                d.point = at;
                d.direction = (at - path.a).sqrMagnitude > 0.01f ? (at - path.a).normalized : Vector2.right;
                d.feedback = true;
                if (h.health.TakeDamage(d) > 0f) Combat.OnHitFeedback(h.health, d);
                n++;
            }
            return n;
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

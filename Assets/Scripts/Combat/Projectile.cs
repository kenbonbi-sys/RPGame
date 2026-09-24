using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Straight-flying projectile (fireball, spores). Explodes on hit or on obstacles. Online, the
    /// server's copy deals the damage; a screen's copy (its own player's fireball shown at once,
    /// others' seen) only flies and bursts.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public Team team = Team.Player;
        public float speed = 9f;
        public float radius = 0.3f;
        public float lifetime = 2.5f;
        public float damage = 20f;
        public DamageType damageType = DamageType.Physical;
        public float explodeRadius = 0f;
        public float knockback = 3f;
        public StatusHit status;
        public float critChance = 0.1f;
        public float poise;
        [Tooltip("Damage already includes the shooter's Attack (set by ability projectiles).")]
        public bool attackScaled;
        public string skillName;
        public string hitVfx = "hit_spark";
        public string hitSfx = "sfx_hit";
        public float shake = 0.1f;
        public bool rotateToDirection = true;
        public bool pierce;
        public GameObject owner;

        Vector2 dir;
        float age;
        bool dead;
        bool live = true;
        bool visual = true;
        readonly List<Health> buffer = new List<Health>();
        readonly HashSet<Health> alreadyHit = new HashSet<Health>();

        /// <summary>Deals damage (where the world's rules run), or only flies and bursts (a screen's copy).</summary>
        public bool Live => live;

        /// <param name="dealsDamage">False for a copy that is only shown (online).</param>
        /// <param name="shows">False on a server without a screen.</param>
        public void Launch(Vector2 direction, GameObject ownerGo, bool dealsDamage = true, bool shows = true)
        {
            dir = direction.sqrMagnitude > 0 ? direction.normalized : Vector2.right;
            owner = ownerGo;
            age = 0;
            dead = false;
            live = dealsDamage && GameSession.IsAuthority;
            visual = shows && GameSession.HasScreen;
            alreadyHit.Clear();
            if (rotateToDirection) transform.rotation = Quaternion.Euler(0, 0, Util.Angle(dir));
        }

        void Update()
        {
            if (dead) return;
            float dt = Time.deltaTime;
            age += dt;
            Vector2 pos = transform.position;
            Vector2 next = pos + dir * (speed * dt);

            // obstacles without health stop the projectile
            var hit = Physics2D.CircleCast(pos, radius * 0.6f, dir, speed * dt, Layers.ObstacleMask);
            if (hit.collider != null && hit.collider.GetComponentInParent<Health>() == null)
            {
                transform.position = hit.point;
                Explode(hit.point);
                return;
            }
            transform.position = next;

            Util.HealthsInCircle(next, radius, team, buffer);
            foreach (var h in buffer)
            {
                if (alreadyHit.Contains(h)) continue;
                if (!pierce || explodeRadius > 0)
                {
                    Explode(next);
                    return;
                }
                alreadyHit.Add(h);
                ApplyTo(h, next);
            }
            if (age >= lifetime) Explode(next);
        }

        void ApplyTo(Health h, Vector2 point)
        {
            if (!live) return;
            var d = DamageInfo.Make(damage, team, owner, point, (Vector2)h.transform.position - point, damageType, knockback).RollCrit(critChance);
            d.status = status;
            d.poise = poise;
            d.attackScaled = attackScaled;
            d.skillName = skillName;
            d.feedback = true;
            // no knockback or flash on a target that dodged (invulnerable)
            if (h.TakeDamage(d) > 0) Combat.OnHitFeedback(h, d);
        }

        void Explode(Vector2 point)
        {
            if (dead) return;
            dead = true;
            if (explodeRadius > 0)
            {
                Util.HealthsInCircle(point, explodeRadius, team, buffer);
                foreach (var h in buffer) ApplyTo(h, point);
            }
            else
            {
                Util.HealthsInCircle(point, radius, team, buffer);
                foreach (var h in buffer)
                {
                    if (alreadyHit.Contains(h)) continue;
                    ApplyTo(h, point);
                    break;
                }
            }
            if (visual)
            {
                if (!string.IsNullOrEmpty(hitVfx)) VFX.Spawn(hitVfx, point, Quaternion.identity);
                AudioManager.Play(hitSfx, 0.9f, 0.08f, point);
                if (shake > 0 && Felt(point)) CameraRig.Shake(shake);
            }
            // let trails fade out naturally
            VFX.ReleaseAfterTrails(gameObject);
        }

        /// <summary>In a shared world a burst shakes the screen of the hero who fired it or who stands in it.</summary>
        bool Felt(Vector2 point)
        {
            if (!GameSession.Online) return true;
            var me = Players.Local;
            if (me == null) return false;
            if (owner != null && owner == me.gameObject) return true;
            return ((Vector2)me.transform.position - point).sqrMagnitude < 3f * 3f;
        }
    }
}

using UnityEngine;

namespace RPG
{
    /// <summary>Nấm Độc: keeps its distance and puffs poison spores that slow.</summary>
    public class ShroomAI : EnemyBase
    {
        public float preferredDistance = 4.2f;
        public float shootRange = 6.5f;
        public float sporeDamage = 9f;
        public float sporeSpeed = 5.5f;

        bool fired;
        float strafeSeed;

        protected override void OnEnable()
        {
            base.OnEnable();
            strafeSeed = Random.value * 100f;
        }

        protected override void ChaseBehaviour(PlayerController p, float dist)
        {
            Vector2 to = (Vector2)p.transform.position - Pos;
            if (dist <= shootRange && Time.time >= nextAttack && !Util.LineBlocked(Pos, p.transform.position))
            {
                SetState(State.Attack);
                fired = false;
                motor.Stop();
                if (anim != null) anim.Play("attack", true);
                return;
            }
            if (dist < preferredDistance * 0.65f)
            {
                motor.Move(-to.normalized, 0.8f * (status != null ? status.SpeedMultiplier : 1f));
                PlayMove();
            }
            else if (dist > preferredDistance)
            {
                MoveTo(p.transform.position, preferredDistance * 0.9f);
            }
            else
            {
                // strafe a little
                Vector2 side = Util.Rotate(to.normalized, 90f) * Mathf.Sign(Mathf.Sin(Time.time * 0.7f + strafeSeed));
                motor.Move(side, 0.5f);
                PlayMove();
            }
        }

        protected override void AttackBehaviour(PlayerController p, float dist)
        {
            motor.Stop();
            if (!fired && stateTime > 0.32f && p != null)
            {
                fired = true;
                Fire(p);
            }
            if (stateTime > 0.75f)
            {
                nextAttack = Time.time + attackCooldown * Random.Range(0.9f, 1.3f) / AttackSpeed;
                SetState(State.Chase);
            }
        }

        void Fire(PlayerController p)
        {
            var db = GameManager.I.db;
            if (db.sporePrefab == null) return;
            Vector2 start = Pos + Vector2.up * 0.7f;
            Vector2 target = (Vector2)p.transform.position + Vector2.up * 0.4f + p.motor.Velocity * 0.25f;
            var go = Pool.Get(db.sporePrefab, start, Quaternion.identity);
            var fx = go.GetComponent<PooledFX>();
            if (fx != null) fx.Persistent = true;
            var pr = go.GetComponent<Projectile>();
            pr.team = Team.Enemy;
            pr.damage = sporeDamage;
            pr.speed = sporeSpeed;
            pr.damageType = DamageType.Poison;
            pr.status = new StatusHit { slow = 0.35f, slowDuration = 1.8f };
            pr.hitVfx = "spore_hit";
            pr.hitSfx = "sfx_shroom_puff";
            pr.shake = 0.05f;
            pr.lifetime = 2.2f;
            pr.critChance = 0;
            pr.explodeRadius = 0f;
            pr.pierce = false;
            pr.Launch(target - start, gameObject);
            // players' screens fly a spore of their own; the hit is this one's
            NetCues.Projectile("spore", start, target - start, pr.speed, pr.lifetime, Team.Enemy, pr.hitVfx, pr.hitSfx, pr.shake, 0f);
            NetCues.Vfx("spore_puff", start);
            NetCues.Sound("sfx_spore_shot", 0.6f, 0.1f, transform.position);
        }
    }
}

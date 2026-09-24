using UnityEngine;

namespace RPG
{
    /// <summary>Slime Rêu: moves in hops and lunges at the player.</summary>
    public class SlimeAI : EnemyBase
    {
        public float hopTime = 0.45f;
        public float hopPause = 0.5f;
        public float lungeSpeed = 7f;
        public float lungeDamage = 10f;

        float hopTimer;
        bool hopping;
        bool lunged;

        protected override void OnEnable()
        {
            base.OnEnable();
            hopTimer = Random.Range(0f, hopPause);
            hopping = false;
        }

        protected override void ChaseBehaviour(PlayerController p, float dist)
        {
            if (dist <= attackRange + 0.6f && Time.time >= nextAttack && !hopping)
            {
                SetState(State.Attack);
                lunged = false;
                motor.Stop();
                if (anim != null) anim.Play("attack", true);
                return;
            }
            Hop(p.transform.position);
        }

        void Hop(Vector2 target)
        {
            hopTimer -= Time.deltaTime;
            if (hopping)
            {
                Vector2 to = target - Pos;
                motor.Move(to.sqrMagnitude > 0.01f ? to.normalized : Vector2.zero, status != null ? status.SpeedMultiplier : 1f);
                if (hopTimer <= 0)
                {
                    hopping = false;
                    hopTimer = hopPause * Random.Range(0.8f, 1.3f);
                    motor.Stop();
                    if (anim != null) anim.Play("idle");
                    if (Random.value < 0.35f) NetCues.Sound("sfx_slime_hop", 0.3f, 0.2f, transform.position, 0.1f);
                }
            }
            else
            {
                motor.Stop();
                if (hopTimer <= 0)
                {
                    hopping = true;
                    hopTimer = hopTime;
                    if (anim != null) anim.Play("move", true);
                }
            }
        }

        protected override void AttackBehaviour(PlayerController p, float dist)
        {
            // windup (0.25s) -> lunge (0.25s) -> recover
            if (stateTime < 0.25f)
            {
                motor.Stop();
                return;
            }
            if (!lunged && p != null)
            {
                lunged = true;
                Vector2 dir = ((Vector2)p.transform.position - Pos).normalized;
                motor.Dash(dir * lungeSpeed, 0.25f);
                NetCues.Sound("sfx_slime_hop", 0.6f, 0.1f, transform.position);
            }
            if (stateTime > 0.3f && stateTime < 0.55f && p != null && !p.IsDead &&
                Vector2.Distance(Pos, p.transform.position) < 0.8f)
            {
                var d = DamageInfo.Make(lungeDamage, Team.Enemy, gameObject, p.transform.position,
                                        (Vector2)p.transform.position - Pos, DamageType.Physical, 5f);
                p.health.TakeDamage(d);
                stateTime = 0.56f; // only once
            }
            if (stateTime > 0.9f)
            {
                nextAttack = Time.time + attackCooldown / AttackSpeed;
                SetState(State.Chase);
            }
        }

        protected override void PlayMove()
        {
            // wander also hops
            if (anim != null && anim.Current != "move") anim.Play("move");
        }

        protected override void OnDied(DamageInfo d)
        {
            base.OnDied(d);
            if (!GameSession.HasScreen) return;   // every screen plays the fall of its own copy
            AudioManager.Play("sfx_slime_die", 0.8f, 0.1f, transform.position);
            VFX.Spawn("slime_splat", transform.position + Vector3.up * 0.2f, Quaternion.identity);
        }
    }
}

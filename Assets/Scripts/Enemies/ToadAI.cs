using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Cóc Độc (Đầm Lầy Sương Mù): hops in long leaps, spits poison from a distance and
    /// body-slams whoever comes close. It swims: water does not slow it.
    /// </summary>
    public class ToadAI : EnemyBase
    {
        [Header("Toad")]
        public float hopTime = 0.36f;
        public float hopPause = 0.55f;
        public float hopSpeed = 1.6f;
        public float spitRange = 6.5f;
        public float spitMinRange = 2.2f;
        public float spitDamage = 12f;
        public float spitSpeed = 6.5f;
        public float spitCooldown = 2.6f;
        public float lungeSpeed = 7.5f;
        public float lungeDamage = 15f;

        float hopTimer;
        bool hopping;
        bool acted;
        bool spitting;
        float nextSpit;
        float nextCroak;

        protected override void OnEnable()
        {
            base.OnEnable();
            hopTimer = Random.Range(0f, hopPause);
            hopping = false;
            nextSpit = Time.time + Random.Range(0.6f, 1.6f);
            nextCroak = Time.time + Random.Range(2f, 8f);
        }

        protected override void Think()
        {
            base.Think();
            // a croak now and then, so the swamp sounds alive
            if (Time.time >= nextCroak && (state == State.Idle || state == State.Wander))
            {
                nextCroak = Time.time + Random.Range(5f, 11f);
                NetCues.Sound("sfx_croak", 0.32f, 0.18f, transform.position, 0.4f);
            }
        }

        protected override void ChaseBehaviour(PlayerController p, float dist)
        {
            if (!hopping && Time.time >= nextSpit && dist <= spitRange && dist >= spitMinRange && !Util.LineBlocked(Pos, p.transform.position))
            {
                Begin(p, true);
                return;
            }
            if (!hopping && dist <= attackRange + 0.6f && Time.time >= nextAttack)
            {
                Begin(p, false);
                return;
            }
            Hop(p.transform.position);
        }

        void Begin(PlayerController p, bool spit)
        {
            SetState(State.Attack);
            spitting = spit;
            acted = false;
            motor.Stop();
            Face(p.transform.position);
            if (anim != null) anim.Play("attack", true);
        }

        void Hop(Vector2 goal)
        {
            hopTimer -= Time.deltaTime;
            if (hopping)
            {
                Vector2 to = goal - Pos;
                motor.Move(to.sqrMagnitude > 0.01f ? to.normalized : Vector2.zero, hopSpeed * (status != null ? status.SpeedMultiplier : 1f));
                if (hopTimer <= 0)
                {
                    hopping = false;
                    hopTimer = hopPause * Random.Range(0.8f, 1.3f);
                    motor.Stop();
                    if (anim != null) anim.Play("idle");
                    if (Random.value < 0.3f) NetCues.Sound("sfx_slime_hop", 0.25f, 0.2f, transform.position, 0.1f);
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
            if (spitting)
            {
                motor.Stop();
                if (!acted && stateTime > 0.35f && p != null && !p.IsDead)
                {
                    acted = true;
                    Spit(p);
                }
                if (stateTime > 0.7f)
                {
                    nextSpit = Time.time + spitCooldown * Random.Range(0.85f, 1.25f) / AttackSpeed;
                    nextAttack = Mathf.Max(nextAttack, Time.time + 0.5f);
                    SetState(State.Chase);
                }
                return;
            }
            // windup (0.25 s) -> belly lunge (0.22 s) -> recover
            if (stateTime < 0.25f)
            {
                motor.Stop();
                return;
            }
            if (!acted && p != null)
            {
                acted = true;
                Vector2 dir = ((Vector2)p.transform.position - Pos).normalized;
                motor.Dash(dir * lungeSpeed, 0.22f);
                NetCues.Sound("sfx_croak", 0.5f, 0.12f, transform.position, 0.1f);
            }
            if (stateTime > 0.28f && stateTime < 0.52f && p != null && !p.IsDead && Vector2.Distance(Pos, p.transform.position) < 0.85f)
            {
                var d = DamageInfo.Make(lungeDamage, Team.Enemy, gameObject, p.transform.position,
                                        (Vector2)p.transform.position - Pos, DamageType.Physical, 5f);
                p.health.TakeDamage(d);
                stateTime = 0.53f;   // only once
            }
            if (stateTime > 0.85f)
            {
                nextAttack = Time.time + attackCooldown / AttackSpeed;
                SetState(State.Chase);
            }
        }

        void Spit(PlayerController p)
        {
            bool left = body != null && body.flipX;
            Vector2 start = Pos + new Vector2(left ? -0.3f : 0.3f, 0.55f);
            Vector2 aim = (Vector2)p.transform.position + Vector2.up * 0.4f + p.motor.Velocity * 0.3f;
            EnemyShots.Venom(gameObject, start, aim, spitDamage, spitSpeed, 1, "Phun Độc");
            NetCues.Vfx("venom_puff", start);
            NetCues.Sound("sfx_spit", 0.6f, 0.1f, transform.position);
        }

        protected override void PlayMove()
        {
            if (anim != null && anim.Current != "move") anim.Play("move");
        }

        protected override void OnDied(DamageInfo d)
        {
            base.OnDied(d);
            if (!GameSession.HasScreen) return;   // every screen plays the fall of its own copy
            AudioManager.Play("sfx_croak", 0.7f, 0.05f, transform.position);
            VFX.Spawn("venom_hit", transform.position + Vector3.up * 0.3f, Quaternion.identity);
        }
    }
}

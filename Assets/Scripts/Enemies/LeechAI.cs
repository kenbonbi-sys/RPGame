using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Đỉa Bùn (Đầm Lầy Sương Mù): lies hidden under the water until a hero wades close, lunges,
    /// latches on and drinks: it deals damage and heals itself until it is shaken off (a dash, a
    /// hard hit) or has had its fill. It swims and stays near its pool.
    /// </summary>
    public class LeechAI : EnemyBase
    {
        [Header("Leech")]
        public float lungeSpeed = 8.5f;
        public float lungeDamage = 10f;
        public float latchSeconds = 2.6f;
        public float drainDamage = 5f;
        public float drainEvery = 0.4f;
        [Tooltip("Share of the drained health it heals.")]
        public float healShare = 1f;
        [Tooltip("A hit this big knocks it off.")]
        public float shakeOffHit = 14f;
        [Tooltip("A hero moving faster than this (a dash) shakes it off.")]
        public float shakeOffSpeed = 9f;

        PlayerController latched;
        float latchUntil;
        float nextDrain;
        bool lunged;
        Vector2 latchOffset;
        Vector2 heroLast;

        protected override void OnEnable()
        {
            base.OnEnable();
            latched = null;
            SetIntangible(false);
        }

        protected override void OnDisable()
        {
            Release(false);
            base.OnDisable();
        }

        protected override void ChaseBehaviour(PlayerController p, float dist)
        {
            if (dist <= attackRange + 1.4f && Time.time >= nextAttack)
            {
                SetState(State.Attack);
                lunged = false;
                motor.Stop();
                Face(p.transform.position);
                if (anim != null) anim.Play("attack", true);
                return;
            }
            MoveTo(p.transform.position, attackRange * 0.8f);
        }

        protected override void AttackBehaviour(PlayerController p, float dist)
        {
            if (latched != null)
            {
                Drink();
                return;
            }
            if (stateTime < 0.3f)
            {
                motor.Stop();
                return;
            }
            if (!lunged && p != null)
            {
                lunged = true;
                Vector2 dir = ((Vector2)p.transform.position - Pos).normalized;
                motor.Dash(dir * lungeSpeed, 0.25f);
                NetCues.Vfx("water_splash", Pos);
                NetCues.Sound("sfx_splash", 0.5f, 0.15f, transform.position, 0.1f);
            }
            if (stateTime > 0.3f && stateTime < 0.62f && p != null && !p.IsDead && Vector2.Distance(Pos, p.transform.position) < 0.8f)
            {
                var d = DamageInfo.Make(lungeDamage, Team.Enemy, gameObject, p.transform.position,
                                        (Vector2)p.transform.position - Pos, DamageType.Physical, 2f);
                d.skillName = "Bám Hút";
                if (p.health.TakeDamage(d) > 0f) Latch(p);
                return;
            }
            if (stateTime > 1f)
            {
                nextAttack = Time.time + attackCooldown / AttackSpeed;
                SetState(State.Chase);
            }
        }

        void Latch(PlayerController p)
        {
            latched = p;
            latchUntil = Time.time + latchSeconds;
            nextDrain = Time.time + drainEvery;
            Vector2 off = Pos - (Vector2)p.transform.position;
            latchOffset = Vector2.ClampMagnitude(off.sqrMagnitude > 0.01f ? off : Vector2.right * 0.3f, 0.35f);
            heroLast = p.transform.position;
            motor.HardStop();
            SetIntangible(true);   // clings on without shoving the hero around
            NetCues.WorldText("Bị Đỉa bám!", (Vector3)(Vector2)p.transform.position + Vector3.up * 1.6f, new Color(0.85f, 0.35f, 0.45f));
        }

        void Drink()
        {
            var p = latched;
            if (p == null || p.IsDead || Time.time >= latchUntil)
            {
                Release(true);
                return;
            }
            Vector2 hero = p.transform.position;
            float speed = Time.deltaTime > 0f ? Vector2.Distance(hero, heroLast) / Time.deltaTime : 0f;
            heroLast = hero;
            if (speed > shakeOffSpeed)
            {
                Release(true);
                return;
            }
            motor.Teleport(hero + latchOffset);
            if (body != null) body.flipX = latchOffset.x > 0f;
            if (Time.time < nextDrain) return;
            nextDrain = Time.time + drainEvery;
            if (anim != null) anim.Play("attack", true);
            var d = DamageInfo.Make(drainDamage, Team.Enemy, gameObject, hero, hero - Pos, DamageType.Physical);
            d.dot = true;   // a steady drain: no stagger, no perfect dodge
            d.skillName = "Bám Hút";
            float dealt = p.health.TakeDamage(d);
            if (dealt > 0f) health.Heal(dealt * healShare);
        }

        /// <summary>Lets go of the hero (thrown off when <paramref name="thrown"/>).</summary>
        void Release(bool thrown)
        {
            if (latched == null) return;
            var p = latched;
            latched = null;
            SetIntangible(false);
            if (!thrown || state == State.Dead) return;
            Vector2 away = p != null ? Pos - (Vector2)p.transform.position : Vector2.right;
            motor.AddKnockback((away.sqrMagnitude > 0.01f ? away.normalized : Vector2.right) * 6f);
            nextAttack = Time.time + attackCooldown / AttackSpeed;
            SetState(State.Chase);
        }

        protected override void OnDamaged(DamageInfo d, float amount)
        {
            base.OnDamaged(d, amount);
            if (latched != null && !d.dot && amount >= shakeOffHit && GameSession.IsAuthority) Release(true);
        }

        protected override void OnDied(DamageInfo d)
        {
            Release(false);
            base.OnDied(d);
            if (!GameSession.HasScreen) return;
            AudioManager.Play("sfx_slime_die", 0.6f, 0.1f, transform.position);
            VFX.Spawn("water_splash", transform.position, Quaternion.identity);
        }
    }
}

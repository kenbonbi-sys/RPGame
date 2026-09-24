using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RPG
{
    /// <summary>
    /// Ma Trơi (Đầm Lầy Sương Mù, only at night): a cold ghost-fire. Seen, it drifts away just out
    /// of reach, flickering out and back a few steps further on, leading whoever follows it off
    /// the trails and around its haunt. Caught up with, or when it tires of the game, it swells
    /// (a ring on the ground warns) and bursts: cold damage, Lạnh and Nguyền. A wisp that bursts
    /// is gone and earns nothing; only one struck down first leaves its essence. It floats: over
    /// the water and through the reeds (its body is a trigger), and it comes and goes with the
    /// night in front of whoever watches.
    /// </summary>
    public class WispAI : EnemyBase
    {
        [Header("Wisp")]
        [Tooltip("It keeps about this far ahead of the hero it lures.")]
        public float lureDistance = 4.5f;
        [Tooltip("After luring this long it turns and bursts.")]
        public float lureSeconds = 8f;
        [Tooltip("A hero this close makes it burst at once.")]
        public float catchDistance = 1.7f;
        public float blinkEvery = 2.6f;
        public float blinkDistance = 3f;
        public float swellSeconds = 0.9f;
        public float burstRadius = 2.4f;
        public float burstDamage = 32f;
        public float curseSeconds = 4f;
        public Light2D glow;

        float lureStart = -1f, nextBlink;
        bool bursting;

        public override bool FadesInView => true;

        protected override void OnEnable()
        {
            base.OnEnable();
            lureStart = -1f;
            bursting = false;
            if (glow != null) glow.enabled = true;
        }

        protected override void Think()
        {
            if (state != State.Chase) lureStart = -1f;
            base.Think();
        }

        protected override void ChaseBehaviour(PlayerController p, float dist)
        {
            if (lureStart < 0f)
            {
                lureStart = Time.time;
                nextBlink = Time.time + blinkEvery * Random.Range(0.6f, 1.1f);
                NetCues.Sound("sfx_wisp", 0.45f, 0.1f, transform.position, 0.5f);
            }
            if ((dist <= catchDistance || Time.time - lureStart >= lureSeconds) && Time.time >= nextAttack)
            {
                BeginSwell(p);
                return;
            }
            Vector2 away = Pos - (Vector2)p.transform.position;
            away = away.sqrMagnitude > 0.001f ? away.normalized : Random.insideUnitCircle.normalized;
            // away from the hero, bending back toward its haunt the further it strays: it leads them in circles
            Vector2 toHome = home - Pos;
            float stray = Mathf.Clamp01((toHome.magnitude - 2.5f) / Mathf.Max(1f, leashRange - 4f));
            Vector2 dir = toHome.sqrMagnitude > 0.01f ? Vector2.Lerp(away, toHome.normalized, stray * 0.85f).normalized : away;
            if (Time.time >= nextBlink && dist < lureDistance + 1.5f)
            {
                Blink(dir);
                return;
            }
            Face(p.transform.position);
            if (dist < lureDistance) motor.Move(dir, 0.95f * (status != null ? status.SpeedMultiplier : 1f));
            else if (dist > lureDistance + 2.5f) motor.Move(-away, 0.45f);   // hangs back, beckoning
            else motor.Stop();
            PlayMove();
        }

        /// <summary>Gutters out and flares up again a few steps further on.</summary>
        void Blink(Vector2 dir)
        {
            nextBlink = Time.time + blinkEvery * Random.Range(0.8f, 1.25f);
            Vector2 to = Pos + dir * blinkDistance;
            if (Vector2.Distance(to, home) > leashRange - 1f) to = home + (to - home).normalized * (leashRange - 1f);
            NetCues.Vfx("wisp_blink", Pos + Vector2.up * 0.9f);
            motor.Teleport(to);
            NetCues.Vfx("wisp_blink", to + Vector2.up * 0.9f);
            NetCues.Sound("sfx_wisp", 0.35f, 0.15f, transform.position, 0.3f);
        }

        /// <summary>Swells to burst by <paramref name="p"/> now (AutoShot, tests). Where the rules run.</summary>
        public void DebugSwell(PlayerController p)
        {
            if (!GameSession.IsAuthority || p == null || IsDead) return;
            target = p;
            threat.Add(p, 1f);
            BeginSwell(p);
        }

        void BeginSwell(PlayerController p)
        {
            SetState(State.Attack);
            motor.Stop();
            Face(p.transform.position);
            if (anim != null) anim.Play("attack", true);
            Warn(NetCues.Circle(this, Pos, burstRadius, swellSeconds, new Color(0.45f, 0.85f, 1f, 0.85f)));
            NetCues.Sound("sfx_telegraph", 0.5f, 0.05f, transform.position);
        }

        protected override void AttackBehaviour(PlayerController p, float dist)
        {
            motor.Stop();
            if (stateTime >= swellSeconds) Burst();
        }

        protected override void OnAttackInterrupted()
        {
            // stunned while it swelled: the burst is off
            nextAttack = Time.time + attackCooldown / AttackSpeed;
            lureStart = -1f;
            SetState(State.Chase);
        }

        /// <summary>It bursts: whoever is in the ring is chilled and cursed, and the wisp is gone for good.</summary>
        void Burst()
        {
            bursting = true;
            ForgetWarnings();
            Vector2 at = Pos;
            NetCues.Vfx("wisp_burst", at + Vector2.up * 0.6f, 0f, burstRadius / 2.4f);
            NetCues.Sound("sfx_wisp_burst", 0.9f, 0.08f, transform.position);
            NetCues.Shake(0.2f, at);
            var d = DamageInfo.Make(burstDamage, Team.Enemy, gameObject, at, Vector2.down, DamageType.Ice, 4f);
            d.status.chill = 1;
            d.status.curse = curseSeconds;
            d.skillName = "Ma Trơi Nổ";
            Combat.DamageCircle(at, burstRadius, d);
            DieUncredited();
        }

        public override void Arrive()
        {
            NetCues.Vfx("wisp_blink", transform.position + Vector3.up * 0.9f, 0f, 1.3f);
            NetCues.Sound("sfx_wisp", 0.4f, 0.12f, transform.position, 0.3f);
        }

        public override void Retire(bool quietly = false)
        {
            if (!quietly && GameSession.IsAuthority && !IsDead)
            {
                // it fades into the dawn
                NetCues.Vfx("wisp_blink", transform.position + Vector3.up * 0.9f, 0f, 1.3f);
                NetCues.Sound("sfx_wisp", 0.35f, 0.12f, transform.position, 0.3f);
            }
            base.Retire(true);
        }

        protected override void PlayIdle()
        {
            if (anim != null && anim.Current != "idle") anim.Play("idle");
        }

        protected override void PlayMove()
        {
            if (anim != null && anim.Current != "move") anim.Play("move");
        }

        protected override void OnDied(DamageInfo d)
        {
            base.OnDied(d);
            if (glow != null) glow.enabled = false;
            if (!GameSession.HasScreen || bursting) return;
            // struck down before it could burst: it gutters out
            AudioManager.Play("sfx_wisp", 0.6f, 0.1f, transform.position);
            VFX.Spawn("wisp_blink", transform.position + Vector3.up * 0.9f, Quaternion.identity, 1.2f);
        }
    }
}

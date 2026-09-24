using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Bọ Giáp Đá (Hang Pha Lê): a beetle under a shell of cave rock. Blows at its face glance off
    /// the shell; its soft back takes them in full and more (<see cref="Health.Guard"/>). It turns
    /// slowly, so a hero can circle behind it. It snaps with its mandibles up close and, from a
    /// few steps away, paws the ground (a line warns) and charges; a charge into rock leaves it
    /// dazed and open on every side for a moment.
    /// </summary>
    public class StoneBeetleAI : EnemyBase
    {
        [Header("Shell")]
        [Tooltip("Share of a blow at its face that gets through.")]
        [Range(0f, 1f)] public float frontGuard = 0.2f;
        [Tooltip("A blow at its back hurts this much more.")]
        public float backBonus = 1.5f;
        [Tooltip("Seconds it needs before it turns around again.")]
        public float turnDelay = 1f;

        [Header("Attacks")]
        public float biteDamage = 26f;
        public float biteRadius = 1.5f;
        public float chargeMinRange = 2.6f;
        public float chargeMaxRange = 7.5f;
        public float chargeWindup = 0.8f;
        public float chargeSpeed = 9.5f;
        public float chargeSeconds = 0.75f;
        public float chargeDamage = 32f;
        public float chargeCooldown = 6f;
        [Tooltip("Seconds of Choáng after it rams rock.")]
        public float wallStun = 2.2f;

        enum Step { Bite, Charge }
        Step step;
        bool acted;
        float facing = 1f;
        float nextTurn;
        float nextCharge;
        float nextText;
        Vector2 aim;
        float dazedUntil;
        readonly HashSet<PlayerController> rammed = new HashSet<PlayerController>();
        readonly RaycastHit2D[] cast = new RaycastHit2D[6];
        Collider2D[] own;

        /// <summary>Which way it looks (+1 right, −1 left).</summary>
        public float Facing => facing;
        /// <summary>Knocked silly by a wall: soft on every side.</summary>
        public bool Dazed => Time.time < dazedUntil;

        protected override void Awake()
        {
            base.Awake();
            own = GetComponentsInChildren<Collider2D>(true);
            health.Guard = Guard;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            nextCharge = Time.time + Random.Range(1.5f, 3f);
            dazedUntil = 0f;
            facing = body != null && body.flipX ? -1f : 1f;
        }

        /// <summary>The shell: most of a blow at its face stops there, its back takes more.</summary>
        float Guard(DamageInfo d)
        {
            if (Dazed) return backBonus;
            float side = Armour.Side(Pos, facing, Armour.Origin(d, Pos));
            if (side > 0.3f)
            {
                if (Time.time >= nextText)
                {
                    nextText = Time.time + 0.7f;
                    NetCues.WorldText("Giáp chặn!", health.HeadPosition + Vector3.up * 0.2f, new Color(0.75f, 0.8f, 0.9f));
                    NetCues.Vfx("hit_spark", d.point, 0f, 0.8f);
                }
                return frontGuard;
            }
            if (side < -0.3f)
            {
                if (Time.time >= nextText)
                {
                    nextText = Time.time + 0.7f;
                    NetCues.WorldText("Trúng lưng!", health.HeadPosition + Vector3.up * 0.2f, Palette.Crit);
                }
                return backBonus;
            }
            return 1f;
        }

        /// <summary>Turns toward <paramref name="at"/> if it may (slowly), or at once.</summary>
        void TurnToward(Vector2 at, bool now = false)
        {
            float want = at.x < Pos.x ? -1f : 1f;
            if (Mathf.Abs(at.x - Pos.x) < 0.15f || want == facing) return;
            if (!now && Time.time < nextTurn) return;
            facing = want;
            nextTurn = Time.time + turnDelay;
        }

        protected override void FaceMovement()
        {
            if (body != null) body.flipX = facing < 0f;
        }

        protected override void Think()
        {
            if (state == State.Wander || state == State.Return)
            {
                var v = motor.Velocity;
                if (Mathf.Abs(v.x) > 0.1f) TurnToward(Pos + v);
            }
            base.Think();
        }

        protected override void ChaseBehaviour(PlayerController p, float dist)
        {
            Vector2 at = p.transform.position;
            TurnToward(at);
            if (dist <= biteRadius + 0.2f && Time.time >= nextAttack)
            {
                Begin(p, Step.Bite);
                return;
            }
            if (Time.time >= nextCharge && dist >= chargeMinRange && dist <= chargeMaxRange && !Util.LineBlocked(Pos, at))
            {
                Begin(p, Step.Charge);
                return;
            }
            MoveTo(at, biteRadius * 0.8f);
        }

        /// <summary>Charges at <paramref name="p"/> now (AutoShot, tests). Where the rules run.</summary>
        public void DebugCharge(PlayerController p)
        {
            if (!GameSession.IsAuthority || p == null || IsDead) return;
            target = p;
            threat.Add(p, 1f);
            Begin(p, Step.Charge);
        }

        void Begin(PlayerController p, Step what)
        {
            SetState(State.Attack);
            step = what;
            acted = false;
            motor.Stop();
            Vector2 to = (Vector2)p.transform.position - Pos;
            aim = to.sqrMagnitude > 0.01f ? to.normalized : new Vector2(facing, 0f);
            if (anim != null) anim.Play("windup", true);
            if (what == Step.Charge)
            {
                TurnToward(p.transform.position, true);   // it squares up to charge
                EnemyShots.WarnLine(this, Pos + Vector2.up * 0.2f, aim, chargeSpeed * chargeSeconds, 0.55f, 1f, chargeWindup, t => Warn(t));
                NetCues.Sound("sfx_telegraph", 0.5f, 0.05f, transform.position);
            }
            else
            {
                // a snap straight ahead: only what is in front of its mandibles
                aim = new Vector2(facing, Mathf.Clamp(aim.y, -0.6f, 0.6f)).normalized;
                Warn(NetCues.Cone(this, Pos + Vector2.up * 0.2f, aim, biteRadius, 0.4f));
            }
        }

        protected override void AttackBehaviour(PlayerController p, float dist)
        {
            if (step == Step.Bite)
            {
                motor.Stop();
                if (!acted && stateTime >= 0.4f)
                {
                    acted = true;
                    ForgetWarnings();
                    if (anim != null) anim.Play("attack", true);
                    NetCues.Sound("sfx_boss_swipe", 0.6f, 0.1f, transform.position);
                    var d = DamageInfo.Make(biteDamage, Team.Enemy, gameObject, Pos, aim, DamageType.Physical, 4f);
                    d.skillName = "Kẹp Hàm";
                    Combat.DamageCone(Pos + Vector2.up * 0.2f, aim, biteRadius, 90f, d);
                }
                if (stateTime > 0.95f)
                {
                    nextAttack = Time.time + attackCooldown * Random.Range(0.9f, 1.2f) / AttackSpeed;
                    SetState(State.Chase);
                }
                return;
            }
            // the charge: paws the ground, then runs straight
            if (stateTime < chargeWindup)
            {
                motor.Stop();
                return;
            }
            if (!acted)
            {
                acted = true;
                ForgetWarnings();
                rammed.Clear();
                if (anim != null) anim.Play("move", true);
                if (anim != null) anim.speed = 2f;
                NetCues.Sound("sfx_boss_leap", 0.6f, 0.1f, transform.position);
            }
            float t = stateTime - chargeWindup;
            if (t < chargeSeconds && Rush()) return;
            if (anim != null) anim.speed = 1f;
            motor.Stop();
            if (t > chargeSeconds + 0.35f)
            {
                nextCharge = Time.time + chargeCooldown * Random.Range(0.85f, 1.2f) / AttackSpeed;
                nextAttack = Time.time + 0.6f;
                SetState(State.Chase);
            }
        }

        /// <summary>One frame of the charge; false once it stopped (a wall, the end of its leash).</summary>
        bool Rush()
        {
            float stepLen = chargeSpeed * Time.deltaTime * (status != null ? status.SpeedMultiplier : 1f);
            var filter = new ContactFilter2D();
            filter.SetLayerMask(Layers.ObstacleMask);
            filter.useTriggers = false;
            int n = Physics2D.CircleCast(Pos + Vector2.up * 0.25f, 0.4f, aim, filter, cast, stepLen + 0.15f);
            for (int i = 0; i < n; i++)
            {
                var c = cast[i].collider;
                if (c == null || System.Array.IndexOf(own, c) >= 0 || c.GetComponentInParent<Health>() != null) continue;
                // rock: it rams it and is dazed, its soft belly open to everyone
                NetCues.Vfx("rock_impact", cast[i].point);
                NetCues.Sound("sfx_rock_impact", 0.9f, 0.08f, cast[i].point);
                NetCues.Shake(0.3f, Pos);
                NetCues.WorldText("Choáng váng!", health.HeadPosition + Vector3.up * 0.3f, Palette.Status);
                status.ForceStun(wallStun);
                dazedUntil = Time.time + wallStun;
                if (anim != null) anim.speed = 1f;
                SetState(State.Chase);
                nextCharge = Time.time + chargeCooldown / AttackSpeed;
                return false;
            }
            Vector2 next = Pos + aim * stepLen;
            if (Vector2.Distance(next, home) > leashRange) return false;
            motor.Teleport(next);
            foreach (var h in Players.All)
            {
                if (h == null || h.IsDead || rammed.Contains(h) || Vector2.Distance(h.transform.position, Pos) > 0.9f) continue;
                rammed.Add(h);
                var d = DamageInfo.Make(chargeDamage, Team.Enemy, gameObject, h.transform.position, aim, DamageType.Physical, 8f);
                d.skillName = "Húc Giáp";
                d.feedback = true;
                if (h.health.TakeDamage(d) > 0f) Combat.OnHitFeedback(h.health, d);
            }
            return true;
        }

        protected override void OnAttackInterrupted()
        {
            if (anim != null) anim.speed = 1f;
            if (acted && step == Step.Bite) return;
            nextAttack = Time.time + attackCooldown / AttackSpeed;
            nextCharge = Time.time + chargeCooldown * 0.5f;
            SetState(State.Chase);
        }

        protected override void OnDied(DamageInfo d)
        {
            if (anim != null) anim.speed = 1f;
            base.OnDied(d);
            if (!GameSession.HasScreen) return;
            AudioManager.Play("sfx_rock_impact", 0.6f, 0.15f, transform.position);
            VFX.Spawn("rock_chips", transform.position + Vector3.up * 0.3f, Quaternion.identity, 1.3f);
        }
    }
}

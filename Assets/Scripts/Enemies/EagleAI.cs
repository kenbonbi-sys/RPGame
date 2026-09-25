using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Chim Ưng Đá (Thảo Nguyên Gió): circles high above its rocks, only its shadow on the grass.
    /// When it picks a hero it screams and a ring marks where it will strike; it hangs a moment,
    /// folds its wings and drops like a stone. Whoever is still in the ring is struck and knocked
    /// off their feet. Then it sits on the ground a moment, wings spread, open to every blade
    /// (it takes more there), before it beats its way back up. Its height is its <see cref="EnemyBase.lift"/>:
    /// every screen draws it.
    /// </summary>
    public class EagleAI : EnemyBase
    {
        [Header("Eagle")]
        public float cruiseHeight = 2.4f;
        public float circleRadius = 4.5f;
        [Tooltip("Seconds the ring shows before it drops.")]
        public float markSeconds = 0.9f;
        public float diveSeconds = 0.32f;
        public float strikeRadius = 1.35f;
        public float strikeDamage = 36f;
        public float strikeStun = 0.45f;
        [Tooltip("Seconds it sits on the ground after a strike, open to blades.")]
        public float groundSeconds = 1.2f;
        public float climbSeconds = 0.6f;
        [Tooltip("Damage it takes while on the ground, times.")]
        public float groundedBonus = 1.4f;

        enum Step { Mark, Dive, Ground, Climb }
        Step step;
        Vector2 strikeAt, diveFrom;
        float height, orbitAngle, orbitSign;
        bool circling, struck;
        float nextText;
        readonly List<PlayerController> near = new List<PlayerController>();

        /// <summary>Sitting on the ground after a strike.</summary>
        public bool Grounded => state == State.Attack && (step == Step.Ground || (step == Step.Dive && stateTime > diveSeconds * 0.7f));

        protected override void Awake()
        {
            base.Awake();
            health.Guard = Guard;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            height = cruiseHeight;
            orbitSign = Random.value < 0.5f ? -1f : 1f;
            circling = false;
            ApplyHeight();
        }

        float Guard(DamageInfo d)
        {
            if (!Grounded) return 1f;
            if (Time.time >= nextText)
            {
                nextText = Time.time + 0.8f;
                NetCues.WorldText("Trúng lúc đáp!", health.HeadPosition + Vector3.up * 0.2f, Palette.Crit);
            }
            return groundedBonus;
        }

        void ApplyHeight()
        {
            if (lift != null) lift.localPosition = new Vector3(0f, height, 0f);
        }

        protected override void Update()
        {
            base.Update();
            if (!GameSession.IsAuthority || IsDead) return;
            // back up to the sky when it is not striking
            if (state != State.Attack) height = Mathf.MoveTowards(height, cruiseHeight, Time.deltaTime * 3f);
            ApplyHeight();
        }

        protected override void ChaseBehaviour(PlayerController p, float dist)
        {
            if (!circling)
            {
                circling = true;
                Vector2 from = Pos - (Vector2)p.transform.position;
                orbitAngle = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
            }
            if (Time.time >= nextAttack && dist < circleRadius + 3f && height > cruiseHeight - 0.2f)
            {
                Begin(p);
                return;
            }
            orbitAngle += orbitSign * 45f * Time.deltaTime;
            Vector2 goal = (Vector2)p.transform.position + Util.FromAngle(orbitAngle) * circleRadius;
            MoveTo(goal, 0.3f);
        }

        /// <summary>Marks <paramref name="p"/> and swoops now (AutoShot, tests). Where the rules run.</summary>
        public void DebugSwoop(PlayerController p)
        {
            if (!GameSession.IsAuthority || p == null || IsDead) return;
            target = p;
            threat.Add(p, 1f);
            height = cruiseHeight;
            Begin(p);
        }

        void Begin(PlayerController p)
        {
            SetState(State.Attack);
            step = Step.Mark;
            struck = false;
            circling = false;
            motor.Stop();
            // where the hero will be in a moment
            strikeAt = (Vector2)p.transform.position + p.motor.Velocity * 0.35f;
            Face(strikeAt);
            if (anim != null) anim.Play("windup", true);
            Warn(NetCues.Circle(this, strikeAt, strikeRadius, markSeconds + diveSeconds));
            NetCues.Sound("sfx_eagle", 0.9f, 0.08f, transform.position);
            NetCues.Announce(health, "Kỹ năng: Bổ Nhào");
        }

        protected override void AttackBehaviour(PlayerController p, float dist)
        {
            switch (step)
            {
                case Step.Mark:
                    // hangs over its mark, rising a little
                    motor.Stop();
                    height = Mathf.MoveTowards(height, cruiseHeight + 0.4f, Time.deltaTime);
                    if (stateTime < markSeconds) return;
                    step = Step.Dive;
                    stateTime = 0f;
                    diveFrom = Pos;
                    if (anim != null) anim.Play("attack", true);
                    motor.Dash((strikeAt - Pos) / diveSeconds, diveSeconds);
                    return;
                case Step.Dive:
                    height = Mathf.Lerp(cruiseHeight + 0.4f, 0.15f, Mathf.Clamp01(stateTime / diveSeconds));
                    if (stateTime < diveSeconds) return;
                    Strike();
                    step = Step.Ground;
                    stateTime = 0f;
                    motor.HardStop();
                    if (anim != null) anim.Play("idle", true);
                    return;
                case Step.Ground:
                    motor.Stop();
                    height = 0.15f;
                    if (stateTime < groundSeconds) return;
                    step = Step.Climb;
                    stateTime = 0f;
                    if (anim != null) anim.Play("move", true);
                    return;
                default:
                    motor.Stop();
                    height = Mathf.Lerp(0.15f, cruiseHeight, Mathf.Clamp01(stateTime / climbSeconds));
                    if (stateTime < climbSeconds) return;
                    nextAttack = Time.time + attackCooldown * Random.Range(0.85f, 1.25f) / AttackSpeed;
                    SetState(State.Chase);
                    return;
            }
        }

        /// <summary>The talons land: whoever is still in the ring is struck and knocked down.</summary>
        void Strike()
        {
            if (struck) return;
            struck = true;
            ForgetWarnings();
            NetCues.Vfx("rock_chips", strikeAt, 0f, 1.1f);
            NetCues.Vfx("dash_burst", strikeAt, 0f, 1.1f);
            NetCues.Sound("sfx_hit_heavy", 0.7f, 0.1f, strikeAt);
            NetCues.Shake(0.12f, strikeAt);
            Players.Within(strikeAt, strikeRadius, near);
            Vector2 dir = strikeAt - diveFrom;
            foreach (var p in near)
            {
                if (p == null || p.IsDead) continue;
                var d = DamageInfo.Make(strikeDamage, Team.Enemy, gameObject, p.transform.position,
                                        dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.down, DamageType.Physical, 4f);
                d.skillName = "Bổ Nhào";
                d.status.stun = strikeStun;
                d.feedback = true;
                if (p.health.TakeDamage(d) > 0f) Combat.OnHitFeedback(p.health, d);
            }
        }

        protected override void OnAttackInterrupted()
        {
            // stunned in the air: it drops where it is and climbs back
            if (step != Step.Mark) return;
            nextAttack = Time.time + attackCooldown / AttackSpeed;
            SetState(State.Chase);
        }

        protected override void OnDied(DamageInfo d)
        {
            base.OnDied(d);
            height = 0f;
            ApplyHeight();
            if (GameSession.HasScreen) AudioManager.Play("sfx_eagle", 0.35f, 0.2f, transform.position);
        }
    }
}

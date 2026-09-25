using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Linh Cẩu Gió (Thảo Nguyên Gió): hunts in packs. When one sees a hero the whole pack comes;
    /// they trot in a ring around their prey, cackling, and take turns: one crouches (a line
    /// warns), lunges, bites and runs straight back out before the hero can answer, while the
    /// others keep circling. Only one of a pack bites a hero at a time.
    /// </summary>
    public class HyenaAI : EnemyBase
    {
        [Header("Hyena")]
        public float circleRadius = 3.6f;
        public float lungeWindup = 0.45f;
        public float lungeSpeed = 11f;
        public float lungeSeconds = 0.26f;
        public float biteDamage = 30f;
        public float retreatSeconds = 0.75f;
        [Tooltip("How far a hyena's cackle calls the rest of its pack.")]
        public float packCall = 9f;
        [Tooltip("Seconds between two bites of the same pack at the same hero.")]
        public float packTurn = 0.9f;

        enum Step { Windup, Lunge, Retreat }
        Step step;
        bool bit, circling, calledPack;
        Vector2 aim;
        float orbitAngle, orbitSign, turnAfter;

        /// <summary>When a hyena last went for each hero: the pack takes turns.</summary>
        static readonly Dictionary<PlayerController, float> LastBite = new Dictionary<PlayerController, float>();

        protected override void OnEnable()
        {
            base.OnEnable();
            orbitSign = Random.value < 0.5f ? -1f : 1f;
            circling = calledPack = false;
            turnAfter = Time.time + Random.Range(1f, 2.5f);
        }

        protected override void ChaseBehaviour(PlayerController p, float dist)
        {
            if (!calledPack) CallPack(p);
            if (!circling)
            {
                circling = true;
                Vector2 from = Pos - (Vector2)p.transform.position;
                orbitAngle = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
            }
            if (Time.time >= nextAttack && Time.time >= turnAfter && dist < circleRadius + 2.5f && !Util.LineBlocked(Pos, p.transform.position) && MyTurn(p))
            {
                Begin(p);
                return;
            }
            orbitAngle += orbitSign * 60f * Time.deltaTime;
            Vector2 goal = (Vector2)p.transform.position + Util.FromAngle(orbitAngle) * circleRadius;
            if (Random.value < 0.003f) orbitSign = -orbitSign;
            MoveTo(goal, 0.3f, dist > circleRadius + 3f ? 1.25f : 1f);
        }

        /// <summary>The pack takes turns: no two bites at one hero within <see cref="packTurn"/> seconds.</summary>
        bool MyTurn(PlayerController p)
        {
            if (LastBite.TryGetValue(p, out float t) && Time.time - t < packTurn) return false;
            LastBite[p] = Time.time + lungeWindup;
            return true;
        }

        /// <summary>A cackle: every hyena of the pack near it joins the hunt.</summary>
        void CallPack(PlayerController p)
        {
            calledPack = true;
            NetCues.Sound("sfx_hyena", 0.8f, 0.12f, transform.position, 0.4f);
            foreach (var e in All)
                if (e != this && e is HyenaAI h && !h.IsDead && !h.Busy && Vector2.Distance(e.transform.position, Pos) <= packCall)
                {
                    h.calledPack = true;   // one cackle is enough
                    h.Alert(p);
                }
        }

        /// <summary>Lunges at <paramref name="p"/> now (AutoShot, tests). Where the rules run.</summary>
        public void DebugLunge(PlayerController p)
        {
            if (!GameSession.IsAuthority || p == null || IsDead) return;
            target = p;
            threat.Add(p, 1f);
            if (!calledPack) CallPack(p);
            Begin(p);
        }

        void Begin(PlayerController p)
        {
            SetState(State.Attack);
            step = Step.Windup;
            bit = false;
            circling = false;
            motor.Stop();
            Face(p.transform.position);
            Vector2 to = (Vector2)p.transform.position + Vector2.up * 0.25f - Pos;
            aim = to.sqrMagnitude > 0.01f ? to.normalized : Vector2.right;
            if (anim != null) anim.Play("windup", true);
            EnemyShots.WarnLine(this, Pos + Vector2.up * 0.2f, aim, lungeSpeed * lungeSeconds + 0.4f, 0.35f, 0.7f, lungeWindup, t => Warn(t));
        }

        protected override void AttackBehaviour(PlayerController p, float dist)
        {
            switch (step)
            {
                case Step.Windup:
                    motor.Stop();
                    if (stateTime < lungeWindup) return;
                    ForgetWarnings();
                    step = Step.Lunge;
                    stateTime = 0f;
                    if (anim != null) anim.Play("attack", true);
                    motor.Dash(aim * lungeSpeed, lungeSeconds);
                    NetCues.Sound("sfx_swing", 0.5f, 0.15f, transform.position);
                    return;
                case Step.Lunge:
                    if (!bit && p != null && !p.IsDead && Vector2.Distance(Pos, p.transform.position) < 1f)
                    {
                        bit = true;
                        var d = DamageInfo.Make(biteDamage, Team.Enemy, gameObject, p.transform.position, aim, DamageType.Physical, 2.5f);
                        d.skillName = "Cắn Xé";
                        d.feedback = true;
                        if (p.health.TakeDamage(d) > 0f) Combat.OnHitFeedback(p.health, d);
                    }
                    if (stateTime < lungeSeconds) return;
                    step = Step.Retreat;
                    stateTime = 0f;
                    return;
                default:
                    // straight back out of reach
                    Vector2 away = p != null ? (Pos - (Vector2)p.transform.position) : -aim;
                    motor.Move(away.sqrMagnitude > 0.01f ? away.normalized : -aim, 1.35f * (status != null ? status.SpeedMultiplier : 1f));
                    PlayMove();
                    if (stateTime < retreatSeconds) return;
                    nextAttack = Time.time + attackCooldown * Random.Range(0.85f, 1.3f) / AttackSpeed;
                    turnAfter = Time.time + Random.Range(0.3f, 1.2f);
                    SetState(State.Chase);
                    return;
            }
        }

        protected override void OnAttackInterrupted()
        {
            if (step != Step.Windup) return;
            nextAttack = Time.time + attackCooldown / AttackSpeed;
            SetState(State.Chase);
        }

        protected override void OnDamaged(DamageInfo d, float amount)
        {
            base.OnDamaged(d, amount);
            if (GameSession.IsAuthority && !calledPack && d.SourcePlayer != null) CallPack(d.SourcePlayer);
        }

        protected override void PlayMove()
        {
            if (anim != null && anim.Current != "move") anim.Play("move");
        }

        protected override void OnDied(DamageInfo d)
        {
            base.OnDied(d);
            if (!GameSession.HasScreen) return;
            AudioManager.Play("sfx_hyena", 0.4f, 0.2f, transform.position);
        }
    }
}

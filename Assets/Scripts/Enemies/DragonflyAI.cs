using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Chuồn Chuồn Kim (Đầm Lầy Sương Mù, by day): a needle-thin dragonfly that never stays put. It
    /// flits about in short straight bursts, circles a hero it has seen, then hangs still for a
    /// breath (a line on the ground shows its path), shoots straight through them with its sting
    /// and flies off before anyone can answer. It flies: over water, reeds and heroes alike (its
    /// body is a trigger: still hit by attacks, never in anyone's way).
    /// </summary>
    public class DragonflyAI : EnemyBase
    {
        [Header("Dragonfly")]
        public float orbitRadius = 3.4f;
        public float dartSpeed = 13f;
        public float dartSeconds = 0.3f;
        public float windup = 0.45f;
        public float stingDamage = 12f;
        public float retreatSeconds = 0.8f;
        [Tooltip("Seconds of one straight flit, and of the hover between two.")]
        public float flitSeconds = 0.32f, hoverSeconds = 0.16f;
        [Tooltip("The body flies this high over its shadow; it drops to the ground when it falls.")]
        public Transform flight;
        [Tooltip("Its wings as it takes aim and darts (a bat's squeak).")]
        public string wingSound = "sfx_buzz";
        [Tooltip("What its dart is called in the log.")]
        public string stingName = "Kim Chích";

        enum Step { Windup, Dart, Retreat }
        Step step;
        Vector2 dartDir, flitDir, wanderGoal;
        float flitLeft, hoverLeft, nextGoal, retreatFor;
        bool orbiting;
        float orbitAngle, orbitSign, attackAfter;
        readonly HashSet<PlayerController> stung = new HashSet<PlayerController>();
        Vector3 flightHeight;

        protected override void Awake()
        {
            base.Awake();
            if (flight != null) flightHeight = flight.localPosition;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            orbiting = false;
            flitLeft = hoverLeft = 0f;
            nextGoal = 0f;
            if (flight != null)
            {
                flight.localPosition = flightHeight;
                var bob = flight.GetComponent<Bobber>();
                if (bob != null) bob.enabled = true;
            }
        }

        protected override void Think()
        {
            if (state == State.Idle || state == State.Wander)
            {
                // never still: it flits about its haunt until someone comes by
                if (Notice())
                {
                    SetState(State.Chase);
                    return;
                }
                if (Time.time >= nextGoal)
                {
                    wanderGoal = home + Util.RandomInCircle(wanderRadius);
                    nextGoal = Time.time + Random.Range(1f, 2.4f);
                }
                Flit(wanderGoal, 0.7f);
                return;
            }
            if (state != State.Chase) orbiting = false;
            base.Think();
        }

        /// <summary>Short straight bursts toward <paramref name="goal"/>, each a little off, with a hover between.</summary>
        void Flit(Vector2 goal, float speed)
        {
            if (hoverLeft > 0f)
            {
                hoverLeft -= Time.deltaTime;
                motor.Stop();
                if (anim != null) anim.Play("idle");
                return;
            }
            if (flitLeft <= 0f)
            {
                Vector2 to = goal - Pos;
                Vector2 dir = to.sqrMagnitude > 0.04f ? to.normalized : Random.insideUnitCircle.normalized;
                flitDir = Util.Rotate(dir, Random.Range(-40f, 40f));
                flitLeft = flitSeconds * Random.Range(0.7f, 1.3f);
                if (to.magnitude < 0.5f) flitLeft *= 0.4f;
            }
            flitLeft -= Time.deltaTime;
            if (flitLeft <= 0f) hoverLeft = hoverSeconds * Random.Range(0.6f, 1.5f);
            motor.Move(flitDir, speed * (status != null ? status.SpeedMultiplier : 1f));
            if (anim != null) anim.Play("move");
        }

        protected override void ChaseBehaviour(PlayerController p, float dist)
        {
            if (!orbiting)
            {
                orbiting = true;
                Vector2 from = Pos - (Vector2)p.transform.position;
                orbitAngle = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
                orbitSign = Random.value < 0.5f ? -1f : 1f;
                attackAfter = Time.time + Random.Range(1f, 2.2f);
            }
            if (Time.time >= attackAfter && Time.time >= nextAttack && dist > 1.5f && dist < orbitRadius + 3f)
            {
                BeginDart(p);
                return;
            }
            orbitAngle += orbitSign * 75f * Time.deltaTime;
            Vector2 goal = (Vector2)p.transform.position + Util.FromAngle(orbitAngle) * orbitRadius;
            Flit(goal, 1.25f);
        }

        /// <summary>Darts at <paramref name="p"/> now (AutoShot, tests). Where the rules run.</summary>
        public void DebugDart(PlayerController p)
        {
            if (!GameSession.IsAuthority || p == null || IsDead) return;
            target = p;
            threat.Add(p, 1f);
            BeginDart(p);
        }

        void BeginDart(PlayerController p)
        {
            SetState(State.Attack);
            step = Step.Windup;
            orbiting = false;
            stung.Clear();
            motor.Stop();
            Face(p.transform.position);
            Vector2 aim = (Vector2)p.transform.position + Vector2.up * 0.3f - Pos;
            dartDir = aim.sqrMagnitude > 0.01f ? aim.normalized : Vector2.right;
            if (anim != null) anim.Play("windup", true);
            NetCues.Sound(wingSound, 0.5f, 0.1f, transform.position, 0.15f);
            EnemyShots.WarnLine(this, Pos, dartDir, dartSpeed * dartSeconds, 0.35f, 0.7f, windup, t => Warn(t));
        }

        protected override void AttackBehaviour(PlayerController p, float dist)
        {
            switch (step)
            {
                case Step.Windup:
                    motor.Stop();
                    if (stateTime < windup) return;
                    step = Step.Dart;
                    stateTime = 0f;
                    ForgetWarnings();
                    if (anim != null) anim.Play("attack", true);
                    motor.Dash(dartDir * dartSpeed, dartSeconds);
                    NetCues.Sound(wingSound, 0.8f, 0.08f, transform.position, 0.1f);
                    return;
                case Step.Dart:
                    Sting();
                    if (stateTime < dartSeconds + 0.02f) return;
                    // off and away, before anyone can answer
                    Vector2 away = p != null ? Pos - (Vector2)p.transform.position : -dartDir;
                    BeginRetreat(away.sqrMagnitude > 0.01f ? away.normalized : -dartDir, retreatSeconds);
                    return;
                default:
                    motor.Move(flitDir, 1.6f * (status != null ? status.SpeedMultiplier : 1f));
                    if (anim != null) anim.Play("move");
                    if (stateTime < retreatFor) return;
                    nextAttack = Time.time + attackCooldown * Random.Range(0.9f, 1.3f) / AttackSpeed;
                    SetState(State.Chase);
                    return;
            }
        }

        protected override void OnAttackInterrupted()
        {
            // stunned while it took aim: the dart is off
            if (step != Step.Windup) return;
            nextAttack = Time.time + attackCooldown / AttackSpeed;
            SetState(State.Chase);
        }

        void BeginRetreat(Vector2 away, float seconds)
        {
            step = Step.Retreat;
            stateTime = 0f;
            retreatFor = seconds;
            flitDir = Util.Rotate(away, Random.Range(-35f, 35f));
        }

        /// <summary>
        /// Flies off away from <paramref name="from"/> for <paramref name="seconds"/>, whatever it was
        /// doing (a bat dazzled by a bright hit). Where the rules run.
        /// </summary>
        protected void Scatter(Vector2 from, float seconds)
        {
            if (!GameSession.IsAuthority || IsDead) return;
            ClearWarnings();
            orbiting = false;
            SetState(State.Attack);
            Vector2 away = Pos - from;
            BeginRetreat(away.sqrMagnitude > 0.01f ? away.normalized : Random.insideUnitCircle.normalized, seconds);
        }

        /// <summary>Everyone the dart goes through is stung, once.</summary>
        void Sting()
        {
            foreach (var h in Players.All)
            {
                if (h == null || h.IsDead || stung.Contains(h)) continue;
                if (Vector2.Distance(h.transform.position, Pos) > 0.8f) continue;
                stung.Add(h);
                var d = DamageInfo.Make(stingDamage, Team.Enemy, gameObject, h.transform.position, dartDir, DamageType.Physical, 2f);
                d.skillName = stingName;
                d.feedback = true;
                float dealt = h.health.TakeDamage(d);
                if (dealt > 0f) Combat.OnHitFeedback(h.health, d);
                OnStung(h, dealt);
            }
        }

        /// <summary>A dart went through <paramref name="h"/> for <paramref name="dealt"/> (0: it did not hurt).</summary>
        protected virtual void OnStung(PlayerController h, float dealt) { }

        protected override void OnDied(DamageInfo d)
        {
            base.OnDied(d);
            if (flight != null) StartCoroutine(Drop());
            if (!GameSession.HasScreen) return;
            AudioManager.Play(wingSound, 0.4f, 0.2f, transform.position);
        }

        /// <summary>A fallen dragonfly tumbles out of the air onto the ground.</summary>
        IEnumerator Drop()
        {
            var bob = flight.GetComponent<Bobber>();
            if (bob != null) bob.enabled = false;
            Vector3 from = flight.localPosition;
            Vector3 to = new Vector3(from.x, 0.1f, from.z);
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                float k = t / 0.35f;
                flight.localPosition = Vector3.Lerp(from, to, k * k);
                yield return null;
            }
            flight.localPosition = to;
        }
    }
}

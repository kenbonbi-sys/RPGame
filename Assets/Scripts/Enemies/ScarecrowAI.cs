using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Bù Nhìn Sống (Thảo Nguyên Gió): stands still on its post among the straw scarecrows of the
    /// abandoned fields, no different from them but for two tells: the straw ones sway in the
    /// wind and it does not, and its head turns to follow whoever walks near. A hero who comes too
    /// close, or strikes it, wakes it (a blow while it stands still bites deeper: "Bất ngờ!").
    /// Awake, it hops at its prey (Nhảy Vồ: a ring marks where it lands) and spins its sickle arms
    /// (Liềm Xoay: a ring around it). Left alone, it walks back to its post and stands still again.
    /// Its hop rides on its <see cref="EnemyBase.lift"/>: every screen draws it.
    /// </summary>
    public class ScarecrowAI : EnemyBase
    {
        [Header("Scarecrow")]
        [Tooltip("How close a hero may come before it wakes.")]
        public float wakeRange = 2.4f;
        [Tooltip("How far its head follows a hero.")]
        public float watchRange = 7f;
        [Tooltip("Damage a blow does while it pretends, times.")]
        public float surpriseBonus = 1.5f;
        public float wakeSeconds = 0.6f;
        public float spinRadius = 1.9f, spinWindup = 0.55f, spinDamage = 30f, spinCooldown = 2.4f;
        public float hopMin = 2.6f, hopMax = 5.5f, hopWindup = 0.5f, hopSeconds = 0.4f, hopHeight = 1.4f;
        public float hopRadius = 1.3f, hopDamage = 26f, hopCooldown = 3.2f;

        enum Step { Wake, Spin, Hop }
        Step step;
        bool still = true, acted;
        float nextSpin, nextHop, nextText;
        Vector2 hopFrom, hopTo;
        readonly List<PlayerController> near = new List<PlayerController>();

        /// <summary>Standing still on its post, a scarecrow like the others.</summary>
        public bool Still => still && state != State.Dead;

        protected override void Awake()
        {
            base.Awake();
            health.Guard = Guard;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            BeStill();
        }

        void BeStill()
        {
            still = true;
            SetHeight(0f);
            if (anim != null) anim.Play("still", true);
        }

        void SetHeight(float h)
        {
            if (lift != null) lift.localPosition = new Vector3(0f, h, 0f);
        }

        float Guard(DamageInfo d)
        {
            if (!still) return 1f;
            if (Time.time >= nextText)
            {
                nextText = Time.time + 0.8f;
                NetCues.WorldText("Bất ngờ!", health.HeadPosition + Vector3.up * 0.2f, Palette.Crit);
            }
            return surpriseBonus;
        }

        protected override void Think()
        {
            if (state == State.Idle || state == State.Wander)
            {
                // on its post like any scarecrow; only its head follows a hero walking by
                motor.Stop();
                if (!still) BeStill();
                var watched = Players.Nearest(Pos, watchRange);
                if (watched != null) Face(watched.transform.position);
                var p = Players.Nearest(Pos, wakeRange);
                if (p != null) Wake(p);
                return;
            }
            base.Think();
        }

        /// <summary>It jerks alive: the fight begins.</summary>
        void Wake(PlayerController p)
        {
            if (!still || !GameSession.IsAuthority) return;
            still = false;
            threat.Add(p, 1f);
            target = p;
            Face(p.transform.position);
            SetState(State.Attack);
            step = Step.Wake;
            acted = false;
            if (anim != null) anim.Play("wake", true);
            NetCues.Sound("sfx_scarecrow", 0.9f, 0.08f, transform.position);
            NetCues.Announce(health, "Bù Nhìn Sống!");
            NetCues.Vfx("dash_burst", Pos + Vector2.up * 0.4f, 0f, 0.8f);
            nextSpin = Time.time + wakeSeconds + 0.4f;
            nextHop = Time.time + wakeSeconds + 1.2f;
        }

        /// <summary>Wakes now for <paramref name="p"/> (AutoShot, tests). Where the rules run.</summary>
        public void DebugWake(PlayerController p)
        {
            if (!GameSession.IsAuthority || IsDead || p == null) return;
            Wake(p);
        }

        /// <summary>Hops at (or spins by) <paramref name="p"/> now, awake (AutoShot, tests). Where the rules run.</summary>
        public void DebugAttack(PlayerController p, bool hop)
        {
            if (!GameSession.IsAuthority || IsDead || p == null) return;
            still = false;
            threat.Add(p, 1f);
            target = p;
            Begin(p, hop ? Step.Hop : Step.Spin);
        }

        protected override void OnDamaged(DamageInfo d, float amount)
        {
            if (GameSession.IsAuthority && still && !IsDead)
            {
                var by = d.SourcePlayer ?? Players.Nearest(Pos, watchRange * 2f);
                if (by != null) Wake(by);
            }
            base.OnDamaged(d, amount);
        }

        protected override void ChaseBehaviour(PlayerController p, float dist)
        {
            if (dist <= spinRadius + 0.2f && Time.time >= nextSpin)
            {
                Begin(p, Step.Spin);
                return;
            }
            if (dist >= hopMin && dist <= hopMax && Time.time >= nextHop && !Util.LineBlocked(Pos, p.transform.position))
            {
                Begin(p, Step.Hop);
                return;
            }
            MoveTo(p.transform.position, spinRadius * 0.7f);
        }

        void Begin(PlayerController p, Step s)
        {
            SetState(State.Attack);
            step = s;
            acted = false;
            motor.Stop();
            Face(p.transform.position);
            if (anim != null) anim.Play("windup", true);
            if (s == Step.Spin)
            {
                Warn(NetCues.Circle(this, Pos, spinRadius, spinWindup));
                return;
            }
            // where it will land: where the hero is heading
            hopFrom = Pos;
            Vector2 at = (Vector2)p.transform.position + p.motor.Velocity * 0.25f;
            Vector2 d = at - hopFrom;
            if (d.magnitude > hopMax) at = hopFrom + d.normalized * hopMax;
            hopTo = at;
            Warn(NetCues.Circle(this, hopTo, hopRadius, hopWindup + hopSeconds));
        }

        protected override void AttackBehaviour(PlayerController p, float dist)
        {
            switch (step)
            {
                case Step.Wake:
                    motor.Stop();
                    if (stateTime < wakeSeconds) return;
                    SetState(State.Chase);
                    return;
                case Step.Spin:
                    motor.Stop();
                    if (!acted && stateTime >= spinWindup)
                    {
                        acted = true;
                        ForgetWarnings();
                        if (anim != null) anim.Play("attack", true);
                        NetCues.Sound("sfx_swing", 0.8f, 0.1f, transform.position);
                        NetCues.Vfx("slash_big", Pos + Vector2.up * 0.4f, 0f, 1.2f);
                        var d = DamageInfo.Make(spinDamage, Team.Enemy, gameObject, Pos, Vector2.down, DamageType.Physical, 4f);
                        d.skillName = "Liềm Xoay";
                        Combat.DamageCircle(Pos, spinRadius, d);
                    }
                    if (stateTime < spinWindup + 0.45f) return;
                    nextSpin = Time.time + spinCooldown / AttackSpeed;
                    SetState(State.Chase);
                    return;
                default:
                    if (stateTime < hopWindup)
                    {
                        motor.Stop();
                        return;
                    }
                    float t = (stateTime - hopWindup) / hopSeconds;
                    if (t < 1f)
                    {
                        if (!acted)
                        {
                            acted = true;
                            if (anim != null) anim.Play("attack", true);
                            motor.Dash((hopTo - hopFrom) / hopSeconds, hopSeconds);
                            NetCues.Sound("sfx_scarecrow", 0.6f, 0.15f, transform.position);
                        }
                        SetHeight(Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * hopHeight);
                        return;
                    }
                    if (acted && lift != null && lift.localPosition.y > 0f)
                    {
                        SetHeight(0f);
                        Land();
                    }
                    if (stateTime < hopWindup + hopSeconds + 0.35f) return;
                    nextHop = Time.time + hopCooldown / AttackSpeed;
                    SetState(State.Chase);
                    return;
            }
        }

        /// <summary>It comes down on its mark: whoever is still in the ring is struck.</summary>
        void Land()
        {
            ForgetWarnings();
            motor.HardStop();
            NetCues.Vfx("step_dust", Pos, 0f, 1.6f);
            NetCues.Sound("sfx_hit_heavy", 0.6f, 0.1f, transform.position);
            NetCues.Shake(0.08f, Pos);
            Players.Within(Pos, hopRadius, near);
            foreach (var h in near)
            {
                if (h == null || h.IsDead) continue;
                var d = DamageInfo.Make(hopDamage, Team.Enemy, gameObject, h.transform.position,
                                        (Vector2)h.transform.position - Pos, DamageType.Physical, 5f);
                d.skillName = "Nhảy Vồ";
                d.feedback = true;
                if (h.health.TakeDamage(d) > 0f) Combat.OnHitFeedback(h.health, d);
            }
        }

        protected override void OnAttackInterrupted()
        {
            SetHeight(0f);
            if (step == Step.Wake) return;
            SetState(State.Chase);
        }

        protected override void PlayIdle()
        {
            if (anim != null) anim.Play(still ? "still" : "idle");
        }

        protected override void OnDied(DamageInfo d)
        {
            SetHeight(0f);
            base.OnDied(d);
            if (!GameSession.HasScreen) return;
            AudioManager.Play("sfx_scarecrow", 0.5f, 0.2f, transform.position);
            VFX.Spawn("dig_dust", transform.position + Vector3.up * 0.3f, Quaternion.identity, 0.9f);
        }
    }
}

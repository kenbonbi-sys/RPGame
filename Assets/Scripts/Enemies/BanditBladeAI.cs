using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Đao Thủ Hắc Phong (Thảo Nguyên Gió): a bandit with a curved blade who fights in combos. Lướt
    /// Chém: it crouches (a line shows its path), dashes through its prey cutting whoever is in the
    /// way, then turns and slashes twice (Chém Liên Hoàn, a short arc warning before each). After a
    /// combo it stands panting a moment: "Hở sườn!", every blow bites deeper. A hero already at
    /// its side gets the two slashes without the dash.
    /// </summary>
    public class BanditBladeAI : EnemyBase
    {
        [Header("Blade")]
        public float dashMin = 2.4f, dashMax = 6.5f, dashWindup = 0.5f, dashSpeed = 16f, dashDamage = 30f, dashWidth = 0.9f;
        public float slashRadius = 1.8f, slashWarn = 0.3f, slashDamage = 22f;
        public float recoverSeconds = 1.1f, recoverBonus = 1.35f;

        enum Step { Windup, Dash, Slash1, Slash2, Recover }
        Step step;
        bool acted;
        Vector2 aim, dashFrom;
        float dashLength, nextText;
        readonly HashSet<PlayerController> cut = new HashSet<PlayerController>();

        /// <summary>Standing open after a combo: blows bite deeper.</summary>
        public bool Open => state == State.Attack && step == Step.Recover;

        protected override void Awake()
        {
            base.Awake();
            health.Guard = Guard;
        }

        float Guard(DamageInfo d)
        {
            if (!Open) return 1f;
            if (Time.time >= nextText)
            {
                nextText = Time.time + 0.8f;
                NetCues.WorldText("Hở sườn!", health.HeadPosition + Vector3.up * 0.2f, Palette.Crit);
            }
            return recoverBonus;
        }

        protected override void ChaseBehaviour(PlayerController p, float dist)
        {
            if (Time.time >= nextAttack)
            {
                if (dist <= slashRadius + 0.3f)
                {
                    Begin(p, Step.Slash1);
                    return;
                }
                if (dist >= dashMin && dist <= dashMax && !Util.LineBlocked(Pos, p.transform.position))
                {
                    Begin(p, Step.Windup);
                    return;
                }
            }
            MoveTo(p.transform.position, slashRadius * 0.8f);
        }

        void Begin(PlayerController p, Step s)
        {
            SetState(State.Attack);
            step = s;
            acted = false;
            motor.Stop();
            Vector2 to = (Vector2)p.transform.position - Pos;
            aim = to.sqrMagnitude > 0.01f ? to.normalized : Vector2.right;
            Face(p.transform.position);
            if (s == Step.Windup)
            {
                // through the hero and a little beyond
                dashLength = Mathf.Min(to.magnitude + 1.6f, dashMax + 1.6f);
                var hit = Physics2D.CircleCast(Pos + Vector2.up * 0.2f, 0.3f, aim, dashLength, Layers.ObstacleMask);
                if (hit.collider != null && hit.collider.GetComponentInParent<Health>() == null) dashLength = Mathf.Max(0.5f, hit.distance - 0.3f);
                if (anim != null) anim.Play("windup", true);
                EnemyShots.WarnLine(this, Pos + Vector2.up * 0.2f, aim, dashLength, dashWidth * 0.5f, 0.7f, dashWindup, t => Warn(t));
                NetCues.Announce(health, "Kỹ năng: Lướt Chém");
                return;
            }
            SlashWarn();
        }

        void SlashWarn()
        {
            if (anim != null) anim.Play("windup", true);
            Warn(NetCues.Cone(this, Pos, aim, slashRadius, slashWarn));
        }

        protected override void AttackBehaviour(PlayerController p, float dist)
        {
            switch (step)
            {
                case Step.Windup:
                    motor.Stop();
                    if (stateTime < dashWindup) return;
                    ForgetWarnings();
                    step = Step.Dash;
                    stateTime = 0f;
                    dashFrom = Pos;
                    cut.Clear();
                    if (anim != null) anim.Play("dash", true);
                    motor.Dash(aim * dashSpeed, dashLength / dashSpeed);
                    NetCues.Sound("sfx_dash", 0.8f, 0.1f, transform.position);
                    NetCues.Vfx("dash_burst", Pos, Util.Angle(aim), 0.9f);
                    return;
                case Step.Dash:
                    CutAlong();
                    if (stateTime < dashLength / dashSpeed + 0.05f) return;
                    motor.HardStop();
                    // turn on the prey for the combo
                    if (p != null && !p.IsDead)
                    {
                        Vector2 back = (Vector2)p.transform.position - Pos;
                        if (back.sqrMagnitude > 0.01f) aim = back.normalized;
                        Face(p.transform.position);
                    }
                    step = Step.Slash1;
                    stateTime = 0f;
                    acted = false;
                    SlashWarn();
                    return;
                case Step.Slash1:
                case Step.Slash2:
                    motor.Stop();
                    if (!acted && stateTime >= slashWarn)
                    {
                        acted = true;
                        ForgetWarnings();
                        if (anim != null) anim.Play("attack", true);
                        NetCues.Sound("sfx_swing", 0.8f, 0.12f, transform.position);
                        NetCues.Vfx("slash", Pos + aim * 0.8f + Vector2.up * 0.4f, Util.Angle(aim), 1.1f);
                        var d = DamageInfo.Make(slashDamage, Team.Enemy, gameObject, Pos, aim, DamageType.Physical, 3f);
                        d.skillName = "Chém Liên Hoàn";
                        Combat.DamageCone(Pos + Vector2.up * 0.2f, aim, slashRadius, 100f, d);
                    }
                    if (stateTime < slashWarn + 0.22f) return;
                    if (step == Step.Slash1)
                    {
                        if (p != null && !p.IsDead)
                        {
                            Vector2 again = (Vector2)p.transform.position - Pos;
                            if (again.sqrMagnitude > 0.01f) aim = again.normalized;
                            Face(p.transform.position);
                        }
                        step = Step.Slash2;
                        stateTime = 0f;
                        acted = false;
                        SlashWarn();
                        return;
                    }
                    step = Step.Recover;
                    stateTime = 0f;
                    if (anim != null) anim.Play("idle", true);
                    return;
                default:
                    motor.Stop();
                    if (stateTime < recoverSeconds) return;
                    nextAttack = Time.time + attackCooldown * Random.Range(0.85f, 1.2f) / AttackSpeed;
                    SetState(State.Chase);
                    return;
            }
        }

        /// <summary>Whoever the dash passes is cut, once.</summary>
        void CutAlong()
        {
            foreach (var h in Players.All)
            {
                if (h == null || h.IsDead || cut.Contains(h)) continue;
                Vector2 hp = h.transform.position;
                Vector2 seg = Pos - dashFrom;
                float t = seg.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(hp - dashFrom, seg) / seg.sqrMagnitude) : 0f;
                if (Vector2.Distance(hp, dashFrom + seg * t) > dashWidth) continue;
                cut.Add(h);
                var d = DamageInfo.Make(dashDamage, Team.Enemy, gameObject, hp, aim, DamageType.Physical, 4f);
                d.skillName = "Lướt Chém";
                d.feedback = true;
                if (h.health.TakeDamage(d) > 0f) Combat.OnHitFeedback(h.health, d);
            }
        }

        /// <summary>Starts the combo on <paramref name="p"/> now, from the dash (AutoShot, tests). Where the rules run.</summary>
        public void DebugCombo(PlayerController p)
        {
            if (!GameSession.IsAuthority || p == null || IsDead) return;
            target = p;
            threat.Add(p, 1f);
            Begin(p, Vector2.Distance(Pos, p.transform.position) <= slashRadius + 0.3f ? Step.Slash1 : Step.Windup);
        }

        protected override void OnAttackInterrupted()
        {
            if (step == Step.Dash) motor.HardStop();
            SetState(State.Chase);
        }
    }
}

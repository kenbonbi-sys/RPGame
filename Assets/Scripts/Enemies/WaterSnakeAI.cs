using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Rắn Nước (Đầm Lầy Sương Mù): waits under the water of its pool, only its eyes and rings on
    /// the surface showing, and cannot be hit there. A hero within reach sees it rise, hiss and draw
    /// back (a line on the ground shows where it will strike), then it lunges out along that line
    /// and bites (Độc). After a strike it lies in the open for a moment (the time to punish it),
    /// then slides back into the water and sinks. Now and then it comes up to breathe on its own.
    /// It swims: water does not slow it, and it never leaves the water except in a lunge.
    /// </summary>
    public class WaterSnakeAI : EnemyBase
    {
        [Header("Water snake")]
        public float lungeRange = 5f;
        public float lungeSpeed = 12f;
        public float lungeSeconds = 0.34f;
        public float windup = 0.6f;
        public float biteDamage = 18f;
        public int bitePoison = 1;
        [Tooltip("Lying in the open after a strike, before it heads back to the water.")]
        public float exposedSeconds = 1.3f;
        [Tooltip("Idle, it comes up to breathe about this often (seconds).")]
        public float breatheEvery = 8f;
        public float breatheSeconds = 2.5f;

        enum Step { Windup, Lunge, Exposed }
        Step step;
        bool submerged;
        Vector2 lungeDir;
        float breatheAt, breatheUntil;
        readonly HashSet<PlayerController> bitten = new HashSet<PlayerController>();

        /// <summary>Hidden under the water: nothing can hit it.</summary>
        public bool Submerged => submerged;

        static bool Water(Vector2 p)
        {
            var zone = ZoneRoot.Current;
            return zone == null || zone.terrain == null || zone.IsWater(p);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            submerged = false;
            // it starts under the water; a player's copy just follows the server's body and clips
            if (GameSession.IsAuthority) Submerge(true);
            else if (anim != null) anim.Play("hidden", true);
            breatheAt = Time.time + Random.Range(2f, breatheEvery);
            bitten.Clear();
        }

        protected override void Think()
        {
            bool calm = state == State.Idle || state == State.Wander || state == State.Return;
            if (calm)
            {
                // up for a breath now and then (the moment to strike first), back down after
                if (submerged && Time.time >= breatheAt && Water(Pos))
                {
                    Surface(false);
                    breatheUntil = Time.time + breatheSeconds;
                }
                else if (!submerged && Time.time >= breatheUntil && Water(Pos))
                {
                    Submerge(false);
                    breatheAt = Time.time + breatheEvery * Random.Range(0.7f, 1.3f);
                }
            }
            base.Think();
        }

        protected override void ChaseBehaviour(PlayerController p, float dist)
        {
            if (!submerged)
            {
                // after a strike: back into the water first, in the open all the way
                if (!Water(Pos))
                {
                    MoveTo(home, 0.3f);
                    return;
                }
                Submerge(false);
            }
            if (dist <= lungeRange && Time.time >= nextAttack && !Util.LineBlocked(Pos, p.transform.position))
            {
                BeginStrike(p);
                return;
            }
            // closer under the water, never onto the bank
            Vector2 to = (Vector2)p.transform.position - Pos;
            if (to.magnitude > lungeRange * 0.7f && Water(Pos + to.normalized * 0.7f)) MoveTo(p.transform.position, lungeRange * 0.7f);
            else
            {
                motor.Stop();
                PlayIdle();
                Face(p.transform.position);
            }
        }

        /// <summary>Strikes at <paramref name="p"/> now (AutoShot, tests). Where the rules run.</summary>
        public void DebugStrike(PlayerController p)
        {
            if (!GameSession.IsAuthority || p == null || IsDead) return;
            target = p;
            threat.Add(p, 1f);
            BeginStrike(p);
        }

        void BeginStrike(PlayerController p)
        {
            SetState(State.Attack);
            step = Step.Windup;
            bitten.Clear();
            motor.Stop();
            Face(p.transform.position);
            Vector2 aim = (Vector2)p.transform.position + p.motor.Velocity * 0.25f;
            lungeDir = (aim - Pos).sqrMagnitude > 0.01f ? (aim - Pos).normalized : Vector2.right;
            Surface(true);
            if (anim != null) anim.Play("windup", true);
            NetCues.Sound("sfx_hiss", 0.45f, 0.1f, transform.position, 0.1f);
            float length = lungeSpeed * lungeSeconds;
            EnemyShots.WarnLine(this, Pos + Vector2.up * 0.2f, lungeDir, length, 0.42f, 0.8f, windup, t => Warn(t));
        }

        protected override void AttackBehaviour(PlayerController p, float dist)
        {
            switch (step)
            {
                case Step.Windup:
                    motor.Stop();
                    if (stateTime < windup) return;
                    step = Step.Lunge;
                    stateTime = 0f;
                    ForgetWarnings();
                    if (anim != null) anim.Play("attack", true);
                    motor.Dash(lungeDir * lungeSpeed, lungeSeconds);
                    if (Water(Pos)) NetCues.Vfx("water_splash", Pos, 0f, 0.9f);
                    NetCues.Sound("sfx_splash", 0.45f, 0.12f, transform.position, 0.1f);
                    return;
                case Step.Lunge:
                    Bite();
                    if (stateTime < lungeSeconds + 0.05f) return;
                    step = Step.Exposed;
                    stateTime = 0f;
                    motor.Stop();
                    if (anim != null) anim.Play("idle", true);
                    return;
                default:
                    motor.Stop();
                    if (stateTime < exposedSeconds) return;
                    nextAttack = Time.time + attackCooldown * Random.Range(0.9f, 1.2f) / AttackSpeed;
                    SetState(State.Chase);
                    return;
            }
        }

        protected override void OnAttackInterrupted()
        {
            // stunned before it struck: the strike is off (it would come late, without its warning)
            if (step == Step.Exposed) return;
            nextAttack = Time.time + attackCooldown / AttackSpeed;
            SetState(State.Chase);
        }

        /// <summary>Everyone the lunge passes over is bitten, once.</summary>
        void Bite()
        {
            Vector2 mouth = Pos + lungeDir * 0.5f + Vector2.up * 0.25f;
            foreach (var h in Players.All)
            {
                if (h == null || h.IsDead || bitten.Contains(h)) continue;
                if (Vector2.Distance((Vector2)h.transform.position + Vector2.up * 0.4f, mouth) > 0.85f) continue;
                bitten.Add(h);
                var d = DamageInfo.Make(biteDamage, Team.Enemy, gameObject, h.transform.position, lungeDir, DamageType.Physical, 3f);
                d.status.poison = bitePoison;
                d.skillName = "Lao Cắn";
                d.feedback = true;
                if (h.health.TakeDamage(d) > 0f) Combat.OnHitFeedback(h.health, d);
            }
        }

        /// <summary>Down under the water: out of sight and out of reach.</summary>
        void Submerge(bool quiet)
        {
            if (submerged) return;
            submerged = true;
            SetHittable(false);
            if (anim != null) anim.Play("hidden", true);
            if (quiet) return;
            NetCues.Vfx("water_ripple", Pos, 0f, 1.2f);
            NetCues.Sound("sfx_wade", 0.35f, 0.15f, transform.position, 0.1f);
        }

        /// <summary>Up out of the water: it can be hit until it sinks again.</summary>
        void Surface(bool striking)
        {
            if (!submerged) return;
            submerged = false;
            SetHittable(true);
            if (anim != null) anim.Play(striking ? "windup" : "idle", true);
            NetCues.Vfx("water_splash", Pos, 0f, striking ? 1f : 0.6f);
            if (!striking) NetCues.Sound("sfx_wade", 0.35f, 0.15f, transform.position, 0.1f);
        }

        protected override void PlayIdle()
        {
            if (anim == null) return;
            anim.Play(submerged ? "hidden" : "idle");
        }

        protected override void PlayMove()
        {
            if (anim == null) return;
            anim.Play(submerged ? "hidden" : "move");
        }

        protected override void OnDied(DamageInfo d)
        {
            base.OnDied(d);
            if (!GameSession.HasScreen) return;
            AudioManager.Play("sfx_hiss", 0.5f, 0.1f, transform.position);
            VFX.Spawn("water_splash", transform.position, Quaternion.identity, 0.7f);
        }
    }
}

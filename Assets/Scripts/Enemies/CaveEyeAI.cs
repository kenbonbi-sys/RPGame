using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Mắt Hang (Hang Pha Lê): a great eye grown into the cave wall, never moving. Shut, its lid
    /// of stone takes most of a blow (<see cref="closedGuard"/>). When a hero comes into sight it
    /// opens, stares (a dotted line shows where) and fires a beam of crystal light that bounces
    /// once off the rock (<see cref="EnemyShots.Trace"/>); after the beam it stays open and soft
    /// for a moment before it shuts again. Rock between it and a hero hides them.
    /// </summary>
    public class CaveEyeAI : EnemyBase
    {
        [Header("Eye")]
        [Tooltip("Share of a blow that gets through its shut lid.")]
        [Range(0f, 1f)] public float closedGuard = 0.3f;
        [Tooltip("A blow on its open eye hurts this much more.")]
        public float openBonus = 1.3f;
        public float beamRange = 11f;
        public float beamWidth = 0.45f;
        public float beamDamage = 30f;
        public DamageType beamType = DamageType.Lightning;
        public float stare = 0.95f;
        [Tooltip("Seconds it stays open after the beam.")]
        public float openAfter = 1.8f;
        public Color beamColor = new Color(0.45f, 0.95f, 1f, 1f);

        bool fired;
        bool open;
        EnemyShots.BeamPath path;
        float nextText;

        /// <summary>Its eye is open (soft) or shut (stone).</summary>
        public bool Open => open;

        protected override void Awake()
        {
            base.Awake();
            health.Guard = d =>
            {
                if (open) return openBonus;
                if (Time.time >= nextText)
                {
                    nextText = Time.time + 0.8f;
                    NetCues.WorldText("Mí đá!", health.HeadPosition + Vector3.up * 0.2f, new Color(0.75f, 0.8f, 0.9f));
                }
                return closedGuard;
            };
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            open = false;
            nextAttack = Time.time + Random.Range(0.5f, 1.5f);
        }

        protected override void ChaseBehaviour(PlayerController p, float dist)
        {
            motor.Stop();
            PlayIdle();
            Vector2 at = (Vector2)p.transform.position + Vector2.up * 0.35f;
            if (Time.time >= nextAttack && dist <= beamRange && !Util.LineBlocked(Eye, at)) Stare(p);
        }

        /// <summary>Where its beam comes out.</summary>
        Vector2 Eye => Pos + Vector2.up * 0.45f;

        /// <summary>Stares at <paramref name="p"/> and fires (AutoShot, tests). Where the rules run.</summary>
        public void DebugBeam(PlayerController p)
        {
            if (!GameSession.IsAuthority || p == null || IsDead) return;
            target = p;
            threat.Add(p, 1f);
            Stare(p);
        }

        void Stare(PlayerController p)
        {
            SetState(State.Attack);
            fired = false;
            open = true;
            Vector2 to = (Vector2)p.transform.position + Vector2.up * 0.35f - Eye;
            path = EnemyShots.Trace(Eye, to, beamRange, true, gameObject);
            EnemyShots.WarnBeam(this, path, beamWidth * 0.8f, stare, t => Warn(t));
            if (anim != null) anim.Play("windup", true);
            NetCues.Sound("sfx_lightning_charge", 0.45f, 0.1f, transform.position);
        }

        protected override void AttackBehaviour(PlayerController p, float dist)
        {
            motor.Stop();
            if (!fired && stateTime >= stare)
            {
                fired = true;
                ForgetWarnings();
                if (anim != null) anim.Play("attack", true);
                NetCues.Sound("sfx_beam", 0.8f, 0.08f, transform.position);
                var d = DamageInfo.Make(beamDamage, Team.Enemy, gameObject, path.a, path.b - path.a, beamType, 3f);
                d.skillName = "Tia Mắt Hang";
                EnemyShots.Beam(path, beamWidth, 0.45f, beamColor, d);
            }
            if (stateTime >= stare + openAfter)
            {
                open = false;
                nextAttack = Time.time + attackCooldown * Random.Range(0.85f, 1.2f) / AttackSpeed;
                SetState(State.Chase);
            }
        }

        protected override void OnAttackInterrupted()
        {
            // stunned while it stared: the beam is off, the eye stays open until it recovers
            if (fired) return;
            nextAttack = Time.time + attackCooldown * 0.5f / AttackSpeed;
            SetState(State.Chase);
        }

        protected override void Think()
        {
            if (state != State.Attack && open && (status == null || !status.IsStunned)) open = false;
            base.Think();
        }

        protected override void PlayIdle()
        {
            if (anim != null && anim.Current != "idle") anim.Play("idle");
        }

        protected override void PlayMove() => PlayIdle();

        protected override void FaceMovement() { }   // it looks with its eye, not its body
    }
}

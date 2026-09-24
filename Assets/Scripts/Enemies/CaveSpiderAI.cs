using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Nhện Hang (Hang Pha Lê): fights from a few steps away. It rears up (a line on the ground
    /// shows where) and spits a ball of web that binds whoever it hits (Trói); a hero caught like
    /// that sees it rush in for a poisoned bite. Up close it bites anyway; otherwise it keeps its
    /// distance, sidling around its prey.
    /// </summary>
    public class CaveSpiderAI : EnemyBase
    {
        [Header("Spider")]
        public float webRange = 7f;
        public float webMinRange = 2.2f;
        public float webSpeed = 8.5f;
        public float webDamage = 10f;
        [Tooltip("Seconds a web ball binds whoever it hits.")]
        public float webRoot = 1.6f;
        public float webCooldown = 4.5f;
        public float webWindup = 0.55f;
        public float biteDamage = 22f;
        public int bitePoison = 1;
        public float lungeSpeed = 8f;
        [Tooltip("It likes to fight from about this far.")]
        public float keepAway = 4.2f;

        enum Step { Web, Bite }
        Step step;
        bool acted;
        float nextWeb;
        float sidle;
        Vector2 aim;

        protected override void OnEnable()
        {
            base.OnEnable();
            nextWeb = Time.time + Random.Range(0.6f, 1.6f);
            sidle = Random.value < 0.5f ? -1f : 1f;
        }

        protected override void ChaseBehaviour(PlayerController p, float dist)
        {
            bool bound = p.status != null && p.status.IsRooted;
            if (dist <= attackRange + 0.4f && Time.time >= nextAttack)
            {
                Begin(p, Step.Bite);
                return;
            }
            if (bound)
            {
                // caught in its web: in for the bite
                MoveTo(p.transform.position, attackRange * 0.7f, 1.35f);
                return;
            }
            if (Time.time >= nextWeb && dist >= webMinRange && dist <= webRange && !Util.LineBlocked(Pos, p.transform.position))
            {
                Begin(p, Step.Web);
                return;
            }
            // keep a few steps away, sidling around its prey
            Vector2 to = (Vector2)p.transform.position - Pos;
            Vector2 dir;
            if (dist < keepAway - 1f) dir = -to.normalized;
            else if (dist > keepAway + 1.5f) dir = to.normalized;
            else dir = Vector2.Perpendicular(to.normalized) * sidle;
            if (Random.value < 0.004f) sidle = -sidle;
            motor.Move(dir, 0.85f * (status != null ? status.SpeedMultiplier : 1f));
            PlayMove();
            Face(p.transform.position);
        }

        /// <summary>Spits web at <paramref name="p"/> now (AutoShot, tests). Where the rules run.</summary>
        public void DebugWeb(PlayerController p)
        {
            if (!GameSession.IsAuthority || p == null || IsDead) return;
            target = p;
            threat.Add(p, 1f);
            Begin(p, Step.Web);
        }

        void Begin(PlayerController p, Step what)
        {
            SetState(State.Attack);
            step = what;
            acted = false;
            motor.Stop();
            Face(p.transform.position);
            Vector2 lead = (Vector2)p.transform.position + Vector2.up * 0.35f + (what == Step.Web ? p.motor.Velocity * 0.3f : Vector2.zero);
            aim = (lead - Pos).sqrMagnitude > 0.01f ? (lead - Pos).normalized : Vector2.right;
            if (anim != null) anim.Play("windup", true);
            if (what == Step.Web)
            {
                EnemyShots.WarnLine(this, Pos + Vector2.up * 0.3f, aim, Mathf.Min(webRange, Vector2.Distance(Pos, lead) + 1f), 0.3f, 0.8f, webWindup,
                                    t => Warn(t));
                NetCues.Sound("sfx_hiss", 0.35f, 0.15f, transform.position, 0.2f);
            }
        }

        protected override void AttackBehaviour(PlayerController p, float dist)
        {
            if (step == Step.Web)
            {
                motor.Stop();
                if (!acted && stateTime >= webWindup)
                {
                    acted = true;
                    ForgetWarnings();
                    if (anim != null) anim.Play("attack", true);
                    Vector2 mouth = Pos + new Vector2(aim.x * 0.6f, 0.35f);
                    EnemyShots.Web(gameObject, mouth, mouth + aim * webRange, webDamage, webSpeed, webRoot, "Tơ Trói",
                                   webRange / webSpeed + 0.1f);
                    NetCues.Sound("sfx_web", 0.8f, 0.1f, transform.position);
                    nextWeb = Time.time + webCooldown * Random.Range(0.85f, 1.2f) / AttackSpeed;
                }
                if (stateTime >= webWindup + 0.5f) SetState(State.Chase);
                return;
            }
            // the bite: a short windup, a lunge, a bite as it lands
            if (stateTime < 0.3f)
            {
                motor.Stop();
                return;
            }
            if (!acted)
            {
                acted = true;
                if (anim != null) anim.Play("attack", true);
                motor.Dash(aim * lungeSpeed, 0.18f);
            }
            if (stateTime < 0.55f && p != null && !p.IsDead && Vector2.Distance(Pos, p.transform.position) < 0.95f)
            {
                var d = DamageInfo.Make(biteDamage, Team.Enemy, gameObject, p.transform.position, aim, DamageType.Physical, 3f);
                d.status.poison = bitePoison;
                d.skillName = "Cắn";
                d.feedback = true;
                if (p.health.TakeDamage(d) > 0f) Combat.OnHitFeedback(p.health, d);
                stateTime = 0.56f;   // only once
            }
            if (stateTime > 0.9f)
            {
                nextAttack = Time.time + attackCooldown * Random.Range(0.9f, 1.2f) / AttackSpeed;
                SetState(State.Chase);
            }
        }

        protected override void OnAttackInterrupted()
        {
            // stunned before it spat or bit: that attack is off
            if (acted) return;
            nextAttack = Time.time + attackCooldown / AttackSpeed;
            SetState(State.Chase);
        }

        protected override void PlayMove()
        {
            if (anim != null && anim.Current != "move") anim.Play("move");
        }

        protected override void OnDied(DamageInfo d)
        {
            base.OnDied(d);
            if (!GameSession.HasScreen) return;
            AudioManager.Play("sfx_hiss", 0.5f, 0.2f, transform.position);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Shared enemy logic: idle/wander, aggro + leash, hurt stagger, death + loot. With several
    /// heroes around it chases the one it has the most threat on (<see cref="ThreatTable"/>).
    /// Online the server thinks for it; a player's copy only shows it (<see cref="NetEntity"/>):
    /// hurt flashes, its name plate and its fall still play on every screen.
    /// With no hero within <see cref="SleepRadius"/> it sleeps where it is (a big world only
    /// thinks where someone plays).
    /// </summary>
    public class EnemyBase : MonoBehaviour
    {
        [Header("Identity")]
        public string enemyId = "slime";
        public string displayName = "Slime Rêu";
        public int level = 2;
        public EnemyRank rank = EnemyRank.Normal;

        [Header("Stats")]
        public float maxHp = 60f;
        public float aggroRange = 6f;
        public float leashRange = 13f;
        public float attackRange = 1.2f;
        public float attackCooldown = 1.5f;
        public float contactDamage = 8f;
        public float wanderRadius = 2.5f;
        public List<LootEntry> loot = new List<LootEntry>();

        [Header("Refs")]
        public CharacterMotor motor;
        public SpriteAnimator anim;
        public Health health;
        public StatusEffects status;
        public HitFlash flash;
        public SpriteRenderer body;
        public SpriteStyle style;

        [HideInInspector] public EnemySpawner spawner;

        /// <summary>Every enabled enemy (debug tools and AI queries use this instead of scene searches).</summary>
        public static readonly List<EnemyBase> All = new List<EnemyBase>();

        protected enum State { Idle, Wander, Chase, Attack, Return, Dead }
        protected State state;
        protected float stateTime;
        protected Vector2 home;
        protected Vector2 wanderTarget;
        protected float nextAttack;
        protected float staggerUntil;
        protected NameplateUI plate;
        /// <summary>The hero being chased or attacked (null while idle).</summary>
        protected PlayerController target;
        protected readonly ThreatTable threat = new ThreatTable();
        float nextContact;
        Collider2D[] colliders;
        float nextWakeCheck;
        bool asleep;

        /// <summary>Beyond every hero's view (<see cref="ServerPlayers.ViewRadius"/>): nobody sees it stand still.</summary>
        public const float SleepRadius = 40f;

        /// <summary>Threat a hero gets for walking into the aggro range (damage adds what it dealt).</summary>
        const float NoticeThreat = 1f;

        /// <summary>The hero this enemy is after, if any.</summary>
        public PlayerController Target => target;
        protected Vector2 Pos => transform.position;
        /// <summary>Attack speed from statuses (Lạnh lowers it): attack cooldowns are divided by it.</summary>
        protected float AttackSpeed => status != null ? Mathf.Max(0.1f, status.AttackSpeedMultiplier) : 1f;
        public bool IsDead => state == State.Dead;

        protected virtual void Awake()
        {
            if (motor == null) motor = GetComponent<CharacterMotor>();
            if (health == null) health = GetComponent<Health>();
            if (status == null) status = GetComponent<StatusEffects>();
            colliders = GetComponentsInChildren<Collider2D>(true);
            if (style == null) style = GetComponentInChildren<SpriteStyle>();
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        protected virtual void OnEnable()
        {
            All.Add(this);
            health.displayName = displayName;
            health.level = level;
            health.ResetHealth(maxHp);
            home = transform.position;
            threat.Clear();
            target = null;
            SetState(State.Idle);
            foreach (var c in colliders) c.enabled = true;
            if (body != null) body.color = Color.white;
            if (anim != null) anim.Play("idle", true);
        }

        protected virtual void OnDisable()
        {
            All.Remove(this);
            if (plate != null)
            {
                plate.Release();
                plate = null;
            }
        }

        protected void SetState(State s)
        {
            state = s;
            stateTime = 0;
        }

        protected virtual void Update()
        {
            if (state == State.Dead || !GameSession.IsAuthority) return;
            if (Time.time >= nextWakeCheck)
            {
                nextWakeCheck = Time.time + 0.5f + Random.value * 0.25f;
                bool sleep = !Players.AnyWithin(Pos, SleepRadius);
                if (sleep && !asleep) motor.Stop();
                asleep = sleep;
            }
            if (asleep) return;
            stateTime += Time.deltaTime;
            if (status != null && status.IsStunned)
            {
                motor.Stop();
                if (anim != null) anim.Play("hurt");
                return;
            }
            if (Time.time < staggerUntil)
            {
                motor.Stop();
                return;
            }
            Think();
            ContactDamage();
            FaceMovement();
        }

        protected virtual void Think()
        {
            switch (state)
            {
                case State.Idle:
                    motor.Stop();
                    PlayIdle();
                    if (Notice()) SetState(State.Chase);
                    else if (stateTime > Random.Range(1.5f, 3.5f))
                    {
                        wanderTarget = home + Util.RandomInCircle(wanderRadius);
                        SetState(State.Wander);
                    }
                    break;
                case State.Wander:
                    if (Notice()) { SetState(State.Chase); break; }
                    if (MoveTo(wanderTarget, 0.45f) || stateTime > 4f) SetState(State.Idle);
                    break;
                case State.Chase:
                    target = PickTarget();
                    if (target == null || Vector2.Distance(Pos, home) > leashRange) { GiveUp(); break; }
                    ChaseBehaviour(target, DistanceTo(target));
                    break;
                case State.Attack:
                    AttackBehaviour(target, target != null && !target.IsDead ? DistanceTo(target) : 999f);
                    break;
                case State.Return:
                    if (MoveTo(home, 1f) || stateTime > 6f)
                    {
                        health.Heal(health.maxHp, false);
                        health.ForgetAttackers();
                        SetState(State.Idle);
                    }
                    break;
            }
        }

        float DistanceTo(PlayerController p) => Vector2.Distance(Pos, p.transform.position);

        /// <summary>A hero walked into the aggro range: they are the first target.</summary>
        bool Notice()
        {
            var p = Players.Nearest(Pos, aggroRange);
            if (p == null) return false;
            threat.Add(p, NoticeThreat);
            return true;
        }

        /// <summary>
        /// The hero with the most threat who is still alive and within 1.8 × the aggro range; the
        /// nearest one in that range when nobody has threat yet (hit by a script or a trap).
        /// </summary>
        PlayerController PickTarget()
        {
            float range = aggroRange * 1.8f;
            var p = threat.Top(h => DistanceTo(h) <= range);
            if (p == null)
            {
                p = Players.Nearest(Pos, range);
                if (p != null) threat.Add(p, NoticeThreat);
            }
            return p;
        }

        /// <summary>Everyone is gone, dead or too far, or the enemy strayed past its leash: walk home.</summary>
        void GiveUp()
        {
            threat.Clear();
            target = null;
            SetState(State.Return);
        }

        /// <summary>Default chase: walk to the player, attack in range.</summary>
        protected virtual void ChaseBehaviour(PlayerController p, float dist)
        {
            if (dist <= attackRange && Time.time >= nextAttack)
            {
                SetState(State.Attack);
                return;
            }
            MoveTo(p.transform.position, attackRange * 0.8f);
        }

        protected virtual void AttackBehaviour(PlayerController p, float dist)
        {
            nextAttack = Time.time + attackCooldown / AttackSpeed;
            SetState(State.Chase);
        }

        /// <summary>Moves toward a point. Returns true once arrived.</summary>
        protected bool MoveTo(Vector2 target, float stopDist, float speedMul = 1f)
        {
            Vector2 to = target - Pos;
            if (to.magnitude <= stopDist)
            {
                motor.Stop();
                PlayIdle();
                return true;
            }
            motor.Move(to.normalized, speedMul * (status != null ? status.SpeedMultiplier : 1f));
            PlayMove();
            return false;
        }

        protected virtual void PlayIdle()
        {
            if (anim != null) anim.Play("idle");
        }

        protected virtual void PlayMove()
        {
            if (anim != null) anim.Play("move");
        }

        void FaceMovement()
        {
            if (body == null) return;
            var v = motor.Velocity;
            if (Mathf.Abs(v.x) > 0.1f) body.flipX = v.x < 0;
        }

        /// <summary>Bumping into the enemy's body hurts whichever hero is touching it.</summary>
        void ContactDamage()
        {
            if (contactDamage <= 0 || Time.time < nextContact) return;
            var p = Players.Nearest(Pos, 0.55f);
            if (p == null) return;
            nextContact = Time.time + 1f;
            var d = DamageInfo.Make(contactDamage, Team.Enemy, gameObject, p.transform.position,
                                    (Vector2)p.transform.position - Pos, DamageType.Physical, 4f);
            d.contact = true;
            p.health.TakeDamage(d);
        }

        protected virtual void OnDamaged(DamageInfo d, float amount)
        {
            if (state == State.Dead) return;
            if (GameSession.IsAuthority)
            {
                threat.Add(d.SourcePlayer, amount);
                if (state == State.Idle || state == State.Wander || state == State.Return) SetState(State.Chase);
            }
            if (d.dot) return;   // Bỏng / Độc ticks do not stagger
            staggerUntil = Time.time + 0.12f;
            if (anim != null) anim.Play("hurt", true);
            if (plate == null && HUD.I != null)
                plate = HUD.I.CreateNameplate(transform, $"{displayName} · Cấp {level}", false, new Color(0.9f, 0.25f, 0.25f), health, 1.25f);
            if (GameSession.HasScreen) AudioManager.Play("sfx_hit", 0.5f, 0.15f, transform.position);
        }

        protected virtual void OnDied(DamageInfo d)
        {
            SetState(State.Dead);
            motor.HardStop();
            foreach (var c in colliders) c.enabled = false;
            if (anim != null) anim.Play("dead", true);
            if (GameSession.IsAuthority)
            {
                // every hero who helped gets the kill; online each of them rolls their own loot
                var credited = new List<PlayerController>(health.Attackers);
                ServerPlayers.ShareKill(credited, transform.position);   // party members nearby
                Loot.Roll(loot, transform.position, credited);
                GameEvents.RaiseEnemyKilled(new KillInfo
                {
                    id = enemyId, name = displayName, level = level, rank = rank, position = transform.position, credited = credited
                });
            }
            threat.Clear();
            target = null;
            if (GameSession.HasScreen) VFX.Spawn("enemy_death", transform.position + Vector3.up * 0.4f, Quaternion.identity);
            if (plate != null)
            {
                plate.Release();
                plate = null;
            }
            StartCoroutine(Despawn());
        }

        IEnumerator Despawn()
        {
            yield return new WaitForSeconds(1.2f);
            if (style != null && style.Supported) yield return style.Dissolve(0.6f);
            else
            {
                for (float t = 0; t < 0.5f; t += Time.deltaTime)
                {
                    if (body != null) body.color = new Color(1, 1, 1, 1 - t / 0.5f);
                    yield return null;
                }
            }
            if (spawner != null && GameSession.IsAuthority) spawner.NotifyDead(this);
            gameObject.SetActive(false);
        }

        public void Revive(Vector2 at)
        {
            transform.position = at;
            gameObject.SetActive(true);
        }
    }
}

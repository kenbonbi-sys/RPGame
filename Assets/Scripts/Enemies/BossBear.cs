using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Gấu Ma Rừng Già — the forest boss.
    /// Patterns: Vồ (claw swipe), Dậm Đất (ground stomp, stuns), Ném Đá Lớn (throws a boulder
    /// that stays on the field), Chụp Quăng (leap slam; landing on a boulder stuns the bear).
    /// Enrages at 50% HP. Wakes for any hero, fights the one it has the most threat on and resets
    /// once every hero is down or has left the arena.
    /// Online (Docs/KeHoach-Online.md, phase 3) the server fights; every screen near the arena gets
    /// its warnings, roars and effects (<see cref="NetCues"/>), its big moments
    /// (<see cref="Present"/>), and shows its bar and music while its hero is in the fight. It
    /// comes back <see cref="respawnSeconds"/> after it falls, and everyone who hurt it gets the
    /// kill and their own loot.
    /// </summary>
    public class BossBear : MonoBehaviour, ISaveable
    {
        [Header("Identity")]
        public string bossId = "bear";
        public string displayName = "Gấu Ma Rừng Già";
        public string title = "Chúa Tể Rừng Già";
        public int level = 6;
        public float maxHp = 3200f;

        [Header("Arena")]
        public Transform arenaCenter;
        public float arenaRadius = 11f;
        public float wakeRadius = 7.5f;
        [Tooltip("Online: seconds after its fall before the bear is back (a shared world has more than one hero).")]
        public float respawnSeconds = 180f;

        [Header("Refs")]
        public CharacterMotor motor;
        public SpriteAnimator anim;
        public Health health;
        public StatusEffects status;
        public HitFlash flash;
        public Transform bodyRoot;
        public SpriteRenderer body;
        public AfterImageSpawner afterImages;
        public SpriteStyle style;
        public Poise poise;

        [Header("Tuning")]
        public float walkSpeed = 2.3f;
        public float swipeRange = 2.7f, swipeDamage = 18f;
        public float stompRadius = 4.2f, stompDamage = 26f, stompStun = 1.2f;
        public float rockRadius = 1.6f, rockDamage = 22f, rockFlight = 1.05f;
        public float pounceRadius = 2.4f, pounceDamage = 30f, pounceAir = 0.75f;
        public List<LootEntry> loot = new List<LootEntry>();

        /// <summary>The bear's big moments, shown by every screen near it.</summary>
        public enum Moment : byte
        {
            Intro = 1,
            Enrage = 2,
            Death = 3,
            Reset = 4
        }

        enum State { Dormant, Intro, Chase, Busy, Stunned, Returning, Dead }
        State state = State.Dormant;
        bool enraged;
        float recoverUntil;
        float baseWalkSpeed;
        readonly Dictionary<string, float> ready = new Dictionary<string, float>();
        readonly List<Telegraph> liveTelegraphs = new List<Telegraph>();
        Coroutine routine;
        GameObject auraFx;
        Vector2 home;
        PlayerController target;
        readonly ThreatTable threat = new ThreatTable();
        // a client's copy: what the server says
        bool remoteEngaged, remoteEnraged;
        // this screen shows the fight (bar, music) — online, only while its hero is in it
        bool shownHere;

        public bool Engaged => GameSession.IsAuthority
            ? state != State.Dormant && state != State.Dead && state != State.Returning
            : remoteEngaged;
        public bool Enraged => GameSession.IsAuthority ? enraged : remoteEnraged;
        /// <summary>The hero the bear is fighting (null while dormant).</summary>
        public PlayerController Target => target;

        /// <summary>Every boss in the loaded scenes, alive or defeated.</summary>
        public static readonly List<BossBear> All = new List<BossBear>();
        Vector2 Pos => transform.position;
        /// <summary>Heroes this far from the arena centre have left the fight.</summary>
        float LeashRadius => arenaRadius + 7f;
        float CdMul => (enraged ? 0.65f : 1f) / Mathf.Max(0.1f, status != null ? status.AttackSpeedMultiplier : 1f);   // Lạnh slows its attacks

        void Awake()
        {
            if (motor == null) motor = GetComponent<CharacterMotor>();
            if (health == null) health = GetComponent<Health>();
            if (status == null) status = GetComponent<StatusEffects>();
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            if (poise == null) poise = GetComponent<Poise>();
            if (style == null) style = GetComponentInChildren<SpriteStyle>();
            if (poise != null) poise.Broken += OnPoiseBroken;
            baseWalkSpeed = walkSpeed;
            All.Add(this);
            SaveRegistry.Register(this);
        }

        void OnDestroy()
        {
            All.Remove(this);
            SaveRegistry.Unregister(this);
        }

        void OnPoiseBroken()
        {
            if (state == State.Chase || state == State.Busy) EnterStun();
        }

        void Start()
        {
            home = arenaCenter != null ? (Vector2)arenaCenter.position : Pos;
            health.displayName = displayName;
            health.level = level;
            health.ResetHealth(maxHp);
            anim.Play("idle", true);
            MinimapUI.Register(transform, MinimapUI.MarkerKind.Boss);
        }

        // ================================================================= loop
        void Update()
        {
            if (!GameSession.IsAuthority) return;   // online the server fights; this copy only shows
            switch (state)
            {
                case State.Dormant:
                    motor.Stop();
                    anim.Play("idle");
                    var waker = Players.Nearest(home, wakeRadius);
                    if (waker != null)
                    {
                        threat.Add(waker, 1f);
                        routine = StartCoroutine(Intro());
                    }
                    break;
                case State.Chase:
                    target = PickTarget();
                    if (target == null)
                    {
                        ResetFight();
                        break;
                    }
                    if (status.IsStunned) { EnterStun(); break; }
                    ChaseAndDecide(target);
                    break;
                case State.Stunned:
                    motor.Stop();
                    anim.Play("hurt");
                    if (!status.IsStunned) state = State.Chase;
                    break;
                case State.Returning:
                    if (Vector2.Distance(Pos, home) > 0.4f)
                    {
                        motor.Move((home - Pos).normalized, 1.3f);
                        anim.Play("walk");
                    }
                    else
                    {
                        motor.Stop();
                        state = State.Dormant;
                    }
                    break;
            }
            if (body != null && Mathf.Abs(motor.Velocity.x) > 0.2f && state == State.Chase) body.flipX = motor.Velocity.x < 0;
        }

        /// <summary>
        /// The living hero in the fight (near the arena) the bear has the most threat on; the one
        /// nearest the arena when nobody has threat yet. Null: everyone is down or gone.
        /// </summary>
        PlayerController PickTarget()
        {
            var p = threat.Top(h => Vector2.Distance(h.transform.position, home) <= LeashRadius);
            if (p == null)
            {
                p = Players.Nearest(home, LeashRadius);
                if (p != null) threat.Add(p, 1f);
            }
            return p;
        }

        void ResetFight()
        {
            if (routine != null) StopCoroutine(routine);
            ClearTelegraphs();
            threat.Clear();
            target = null;
            enraged = false;
            walkSpeed = baseWalkSpeed;
            motor.moveSpeed = walkSpeed;
            if (auraFx != null) { VFX.Release(auraFx); auraFx = null; }
            health.ResetHealth(maxHp);
            health.ForgetAttackers();
            if (poise != null) poise.ResetPoise();
            state = State.Returning;
            if (bodyRoot != null) bodyRoot.localPosition = Vector3.zero;
            NetCues.Boss(this, Moment.Reset);
        }

        /// <summary>Music of the zone the boss lives in (back to it after the fight).</summary>
        static string ZoneMusic => ZoneRoot.Current != null && ZoneRoot.Current.def != null ? ZoneRoot.Current.def.music : "music_forest";

        void ChaseAndDecide(PlayerController p)
        {
            Vector2 to = (Vector2)p.transform.position - Pos;
            float dist = to.magnitude;
            if (Time.time < recoverUntil)
            {
                motor.Move(to.normalized, 0.4f);
                anim.Play("walk");
                return;
            }
            if (!enraged && health.Fraction <= 0.5f)
            {
                Run(Enrage());
                return;
            }
            string pick = PickAttack(dist);
            if (pick != null)
            {
                switch (pick)
                {
                    case "swipe": Run(Swipe(p)); break;
                    case "stomp": Run(Stomp()); break;
                    case "rock": Run(RockThrow(p)); break;
                    case "pounce": Run(Pounce(p)); break;
                }
                return;
            }
            // wander toward the player but keep inside the arena
            Vector2 goal = p.transform.position;
            if (Vector2.Distance(goal, home) > arenaRadius) goal = home + (goal - home).normalized * arenaRadius;
            Vector2 mv = goal - Pos;
            if (mv.magnitude > 1.6f)
            {
                motor.Move(mv.normalized, (enraged ? 1.35f : 1f) * status.SpeedMultiplier);
                anim.Play("walk");
            }
            else
            {
                motor.Stop();
                anim.Play("idle");
            }
        }

        string PickAttack(float dist)
        {
            var options = new List<(string id, float w)>();
            void Add(string id, float w, float cd)
            {
                if (ready.TryGetValue(id, out float t) && Time.time < t) return;
                options.Add((id, w));
            }
            if (dist < swipeRange) Add("swipe", 5f, 1.3f);
            if (dist < stompRadius * 0.85f) Add("stomp", 3f, 5f);
            if (dist > 3.5f) Add("rock", 3f, 4.5f);
            if (dist > 2.5f && dist < 9f) Add("pounce", 2.5f, 6f);
            if (options.Count == 0) return null;
            float total = 0;
            foreach (var o in options) total += o.w;
            float r = Random.value * total;
            foreach (var o in options)
            {
                r -= o.w;
                if (r <= 0) return o.id;
            }
            return options[0].id;
        }

        void SetCooldown(string id, float seconds) => ready[id] = Time.time + seconds * CdMul;

        void Run(IEnumerator r)
        {
            state = State.Busy;
            motor.Stop();
            routine = StartCoroutine(Wrap(r));
        }

        IEnumerator Wrap(IEnumerator r)
        {
            yield return r;
            if (state == State.Busy)
            {
                state = State.Chase;
                recoverUntil = Time.time + Random.Range(0.45f, 0.85f) * CdMul;
            }
        }

        void EnterStun()
        {
            if (routine != null) StopCoroutine(routine);
            ClearTelegraphs();
            if (bodyRoot != null) bodyRoot.localPosition = Vector3.zero;
            // an interrupted leap leaves the colliders off
            foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = true;
            state = State.Stunned;
        }

        void Face(Vector2 target)
        {
            if (body != null) body.flipX = target.x < Pos.x;
        }

        void Announce(string skill)
        {
            NetCues.Announce(health, "Kỹ năng: " + skill);
            Bestiary.ForWitnesses(Pos, b => b.RecordSkill(displayName, skill));
        }

        Telegraph Warn(Telegraph t)
        {
            if (t != null) liveTelegraphs.Add(t);
            return t;
        }

        void ClearTelegraphs()
        {
            foreach (var t in liveTelegraphs)
                if (t != null && t.gameObject.activeInHierarchy) t.Cancel();
            liveTelegraphs.Clear();
            NetCues.CancelTelegraphs(this);
        }

        // ================================================================= phases
        IEnumerator Intro()
        {
            state = State.Intro;
            motor.Stop();
            var waker = PickTarget();
            if (waker != null) Face(waker.transform.position);
            NetCues.Boss(this, Moment.Intro);
            yield return new WaitForSeconds(0.4f);
            anim.Play("roar", true);
            NetCues.FlatSound("sfx_boss_roar", Pos, 1f, 0.02f);
            NetCues.Vfx("boss_roar", Pos + Vector2.up * 2.4f);
            NetCues.Shake(0.7f, Pos, NetCues.FarRadius);
            NetCues.Impact(0.8f, 0.8f, Pos, NetCues.FarRadius);
            Bestiary.ForWitnesses(Pos, b => b.RecordSeen(bossId, displayName));
            yield return new WaitForSeconds(1.6f);
            anim.Play("idle", true);
            SetCooldown("stomp", 2.5f);
            SetCooldown("pounce", 4f);
            state = State.Chase;
            recoverUntil = Time.time + 0.5f;
        }

        IEnumerator Enrage()
        {
            enraged = true;
            anim.Play("roar", true);
            NetCues.Announce(health, "Cuồng Nộ!");
            NetCues.Log($"{displayName} nổi cơn cuồng nộ!", new Color(1f, 0.5f, 0.5f), Pos);
            NetCues.FlatSound("sfx_enrage", Pos, 1f, 0.02f);
            NetCues.Vfx("enrage_burst", Pos + Vector2.up * 1.6f);
            NetCues.Boss(this, Moment.Enrage);
            NetCues.Flash(new Color(0.6f, 0.2f, 0.9f), 0.35f, 0.5f, Pos, NetCues.FarRadius);
            NetCues.Impact(1f, 0.9f, Pos, NetCues.FarRadius);
            NetCues.Shake(0.8f, Pos, NetCues.FarRadius);
            walkSpeed = baseWalkSpeed * 1.2f;
            motor.moveSpeed = walkSpeed;
            yield return new WaitForSeconds(1.4f);
        }

        IEnumerator Swipe(PlayerController p)
        {
            SetCooldown("swipe", 1.4f);
            Vector2 dir = ((Vector2)p.transform.position - Pos).normalized;
            Face(p.transform.position);
            Announce("Vồ");
            float windup = enraged ? 0.42f : 0.55f;
            Warn(NetCues.Cone(this, Pos + Vector2.up * 0.3f, dir, swipeRange + 0.3f, windup));
            anim.Play("windup", true);
            NetCues.Sound("sfx_telegraph", 0.5f, 0.05f, transform.position);
            yield return new WaitForSeconds(windup);
            anim.Play("slam", true);
            motor.Dash(dir * 5f, 0.12f);
            NetCues.Sound("sfx_boss_swipe", 1f, 0.08f, transform.position);
            NetCues.Vfx("claw_swipe", Pos + Vector2.up * 0.9f + dir * 1.4f, Util.Angle(dir) - 90f, 1.4f);
            var d = DamageInfo.Make(swipeDamage, Team.Enemy, gameObject, Pos, dir, DamageType.Physical, 7f);
            d.skillName = "Vồ";
            Combat.DamageCone(Pos + Vector2.up * 0.3f, dir, swipeRange + 0.3f, 100f, d);
            NetCues.Shake(0.2f, Pos);
            yield return new WaitForSeconds(0.45f);
            if (enraged && Random.value < 0.5f)
            {
                // quick second swipe, at whoever it is angriest with now
                var next = PickTarget();
                if (next != null) yield return Swipe(next);
            }
        }

        IEnumerator Stomp()
        {
            SetCooldown("stomp", 6f);
            int count = enraged ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                Announce("Dậm Đất");
                float windup = i == 0 ? 1.0f : 0.7f;
                float radius = stompRadius + i * 1.2f;
                Warn(NetCues.Circle(this, Pos, radius, windup));
                anim.Play("windup", true);
                NetCues.Sound("sfx_telegraph", 0.6f, 0.05f, transform.position);
                yield return new WaitForSeconds(windup);
                anim.Play("slam", true);
                NetCues.Sound("sfx_boss_stomp", 1f, 0.05f, transform.position);
                NetCues.Vfx("stomp_shockwave", Pos + Vector2.up * 0.1f, 0f, radius / 4.2f);
                NetCues.Shake(0.65f, Pos);
                NetCues.Impact(0.6f, 0.4f, Pos);
                if (GameSession.HasScreen) TimeFX.HitStop(0.06f, gameObject);
                var d = DamageInfo.Make(stompDamage, Team.Enemy, gameObject, Pos, Vector2.down, DamageType.Physical, 8f);
                d.status.stun = stompStun;
                d.skillName = "Dậm Đất";
                Combat.DamageCircle(Pos, radius, d);
                yield return new WaitForSeconds(0.55f);
            }
        }

        IEnumerator RockThrow(PlayerController p)
        {
            SetCooldown("rock", 5f);
            Announce("Ném Đá Lớn");
            Face(p.transform.position);
            anim.Play("throw", true);
            anim.speed = 0; // hold the "rock overhead" frame
            int rocks = enraged ? 3 : 1;
            var targets = new List<Vector2>();
            for (int i = 0; i < rocks; i++)
            {
                Vector2 t = (Vector2)p.transform.position + p.motor.Velocity * 0.5f;
                if (i > 0) t += Util.RandomInCircle(3.2f);
                if (arenaCenter != null && Vector2.Distance(t, home) > arenaRadius) t = home + (t - home).normalized * arenaRadius;
                targets.Add(t);
                Warn(NetCues.Circle(this, t, rockRadius, 0.75f + rockFlight + i * 0.18f));
            }
            NetCues.Sound("sfx_telegraph", 0.5f, 0.05f, transform.position);
            yield return new WaitForSeconds(0.75f);
            anim.speed = 1;
            anim.Play("throw", true);
            for (int i = 0; i < rocks; i++)
            {
                LaunchRock(targets[i]);
                yield return new WaitForSeconds(0.18f);
            }
            yield return new WaitForSeconds(0.4f);
        }

        void LaunchRock(Vector2 target)
        {
            var db = GameManager.I.db;
            if (db.rockProjectilePrefab == null) return;
            Vector2 start = Pos + new Vector2(body != null && body.flipX ? -1f : 1f, 3f);
            var go = Pool.Get(db.rockProjectilePrefab, start, Quaternion.identity);
            var fx = go.GetComponent<PooledFX>();
            if (fx != null) fx.Persistent = true;
            NetCues.Sound("sfx_rock_throw", 0.9f, 0.08f, transform.position);
            NetCues.Arc(start, target, rockFlight);
            go.GetComponent<ArcProjectile>().Launch(start, target, rockFlight, RockLanded);
        }

        void RockLanded(Vector2 at)
        {
            NetCues.Vfx("rock_impact", at);
            NetCues.Sound("sfx_rock_impact", 1f, 0.06f, at);
            NetCues.Shake(0.35f, at);
            var d = DamageInfo.Make(rockDamage, Team.Enemy, gameObject, at, Vector2.down, DamageType.Physical, 6f);
            d.skillName = "Ném Đá Lớn";
            Combat.DamageCircle(at, rockRadius, d);
            // the rock stays on the field as a "Tảng Đá Lớn"
            var db = GameManager.I.db;
            bool free = Boulder.Nearest(at, 1.6f) == null && Boulder.All.Count < 6 &&
                        Physics2D.OverlapCircle(at, 0.5f, Layers.ObstacleMask) == null;
            if (db.boulderPrefab != null && free)
            {
                var rock = Instantiate(db.boulderPrefab, at, Quaternion.identity, transform.parent);
                NetWorld.BoulderMade(rock.GetComponent<Boulder>());
            }
        }

        IEnumerator Pounce(PlayerController p)
        {
            SetCooldown("pounce", 6.5f);
            Announce("Chụp Quăng");
            Face(p.transform.position);
            anim.Play("crouch", true);
            Vector2 target = p.transform.position;
            if (Vector2.Distance(target, home) > arenaRadius) target = home + (target - home).normalized * arenaRadius;
            float crouch = enraged ? 0.35f : 0.5f;
            Warn(NetCues.Circle(this, target, pounceRadius, crouch + pounceAir));
            NetCues.Sound("sfx_telegraph", 0.6f, 0.05f, transform.position);
            yield return new WaitForSeconds(crouch);

            anim.Play("air", true);
            NetCues.Sound("sfx_boss_leap", 1f, 0.05f, transform.position);
            NetCues.Vfx("step_dust", Pos, 0f, 3f);
            if (afterImages != null && GameSession.HasScreen) afterImages.Emit(pounceAir, new Color(0.6f, 0.4f, 1f, 0.5f));
            var cols = GetComponentsInChildren<Collider2D>();
            foreach (var c in cols) c.enabled = false;
            Vector2 start = Pos;
            float t = 0;
            while (t < pounceAir)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / pounceAir);
                motor.Teleport(Vector2.Lerp(start, target, Util.EaseInOut(k)));
                if (bodyRoot != null) bodyRoot.localPosition = new Vector3(0, 4f * 3f * k * (1 - k), 0);
                yield return null;
            }
            if (bodyRoot != null) bodyRoot.localPosition = Vector3.zero;
            foreach (var c in cols) c.enabled = true;

            anim.Play("slam", true);
            NetCues.Vfx("pounce_land", target);
            NetCues.Sound("sfx_boss_stomp", 0.8f, 0.1f, target);
            NetCues.Shake(0.55f, target);
            NetCues.Impact(0.5f, 0.35f, target);
            var d = DamageInfo.Make(pounceDamage, Team.Enemy, gameObject, target, Vector2.down, DamageType.Physical, 9f);
            d.skillName = "Chụp Quăng";
            Combat.DamageCircle(target, pounceRadius, d);

            // landing on a boulder shatters it and dazes the bear
            var rock = Boulder.Nearest(target, 1.9f);
            if (rock != null)
            {
                rock.Shatter(false);
                status.ForceStun(2.8f);
                NetCues.Log($"{displayName} đâm sầm vào Tảng Đá Lớn và bị choáng!", Palette.Status, target);
                EnterStun();
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>Forces an attack (used by AutoShot / debugging).</summary>
        public void DebugForce(string attack)
        {
            if (!GameSession.IsAuthority) return;
            var p = PickTarget() ?? Players.Local;
            if (p == null || state == State.Dead) return;
            if (state == State.Dormant) state = State.Chase;
            if (routine != null) StopCoroutine(routine);
            ClearTelegraphs();
            if (bodyRoot != null) bodyRoot.localPosition = Vector3.zero;
            switch (attack)
            {
                case "swipe": Run(Swipe(p)); break;
                case "stomp": Run(Stomp()); break;
                case "rock": Run(RockThrow(p)); break;
                case "pounce": Run(Pounce(p)); break;
            }
        }

        // ================================================================= damage
        void OnDamaged(DamageInfo d, float amount)
        {
            if (flash != null) flash.Flash(Color.white, 0.85f, 0.1f);
            if (!GameSession.IsAuthority) return;
            if (state != State.Dead) threat.Add(d.SourcePlayer, amount);
            if (state == State.Dormant && d.sourceTeam == Team.Player) routine = StartCoroutine(Intro());
            if (state == State.Chase && status.IsStunned) EnterStun();
        }

        void OnDied(DamageInfo d)
        {
            if (!GameSession.IsAuthority)
            {
                state = State.Dead;   // the server's death sequence reaches this screen as a moment
                return;
            }
            if (routine != null) StopCoroutine(routine);
            StopAllCoroutines();
            ClearTelegraphs();
            state = State.Dead;
            motor.HardStop();
            foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
            if (bodyRoot != null) bodyRoot.localPosition = Vector3.zero;
            if (auraFx != null) { VFX.Release(auraFx); auraFx = null; }
            StartCoroutine(DeathSequence());
        }

        IEnumerator DeathSequence()
        {
            anim.Play("dead", true);
            NetCues.Boss(this, Moment.Death);
            yield return new WaitForSecondsRealtime(1.2f);
            var credited = new List<PlayerController>(health.Attackers);
            Loot.Roll(loot, Pos, credited);
            Loot.DropCoins(Pos, 12, credited);
            GameEvents.RaiseEnemyKilled(new KillInfo
            {
                id = bossId, name = displayName, level = level, rank = EnemyRank.Boss, position = Pos, credited = credited
            });
            threat.Clear();
            target = null;
            yield return new WaitForSeconds(4f);
            if (!GameSession.HasScreen) yield return new WaitForSeconds(1.8f);
            else if (style != null && style.Supported) yield return style.Dissolve(1.8f);
            else
            {
                for (float t = 0; t < 1.5f; t += Time.deltaTime)
                {
                    if (body != null) body.color = new Color(1, 1, 1, 1 - t / 1.5f);
                    yield return null;
                }
            }
            gameObject.SetActive(false);
            // a shared world has more heroes to come: the bear returns
            if (GameSession.Online && respawnSeconds > 0f && GameManager.I != null) GameManager.I.StartCoroutine(ReturnLater(respawnSeconds));
        }

        IEnumerator ReturnLater(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (this == null || state != State.Dead) yield break;
            transform.position = home + Vector2.up * 1.2f;
            gameObject.SetActive(true);
            foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = true;
            enraged = false;
            walkSpeed = baseWalkSpeed;
            motor.moveSpeed = walkSpeed;
            health.ResetHealth(maxHp);
            if (poise != null) poise.ResetPoise();
            if (status != null) status.Cleanse();
            if (body != null) body.color = Color.white;
            anim.speed = 1f;
            anim.Play("idle", true);
            state = State.Dormant;
            NetCues.Boss(this, Moment.Reset);
            NetCues.Log($"{displayName} đã trở lại Rừng Già Cổ Thụ.", Palette.LogQuest, home, 60f);
        }

        // ================================================================= what screens show
        /// <summary>
        /// A big moment on this screen: the intro's camera and title, the rage aura, the fall and
        /// its victory banner, the walk home. Offline all of it; online the camera, the pause and
        /// the banners only for a hero near the arena (bar and music follow <see cref="LateUpdate"/>).
        /// </summary>
        public void Present(Moment moment)
        {
            var me = Players.Local;
            bool near = !GameSession.Online || me != null && Vector2.Distance(me.transform.position, home) <= LeashRadius;
            switch (moment)
            {
                case Moment.Intro:
                    if (!near) return;
                    // on the game manager: a fall during the intro must not leave the screen frozen
                    if (GameManager.I != null) GameManager.I.StartCoroutine(Cinematic(2f));
                    CameraRig.SetZoom(0.8f);
                    CameraRig.SetFocus(transform, 0.35f);
                    StartCoroutine(IntroTitle());
                    if (!GameSession.Online) ShowFight(true);
                    break;
                case Moment.Enrage:
                    if (auraFx == null) auraFx = VFX.Spawn("enrage_aura", Pos, Quaternion.identity, 1f, transform, true);
                    if (flash != null) flash.SetTint(new Color(0.75f, 0.3f, 1f), 0.18f);
                    break;
                case Moment.Death:
                    if (auraFx != null) { VFX.Release(auraFx); auraFx = null; }
                    StartCoroutine(DeathShow(near));
                    break;
                case Moment.Reset:
                    if (auraFx != null) { VFX.Release(auraFx); auraFx = null; }
                    if (flash != null) flash.SetTint(Color.white, 0);
                    if (!GameSession.Online)
                    {
                        ShowFight(false);
                        CameraRig.SetZoom(1f);
                        CameraRig.SetFocus(null);
                    }
                    break;
            }
        }

        IEnumerator Cinematic(float seconds)
        {
            GameManager.I.SetCinematic(true);
            yield return new WaitForSeconds(seconds);
            if (GameManager.I != null) GameManager.I.SetCinematic(false);
        }

        IEnumerator IntroTitle()
        {
            yield return new WaitForSeconds(0.4f);
            GameEvents.RaiseBanner(BannerKind.Title, displayName, title, new Color(1f, 0.45f, 0.4f));
        }

        IEnumerator DeathShow(bool near)
        {
            if (near)
            {
                TimeFX.SlowMo(0.2f, 1.6f);
                AudioManager.Play("sfx_boss_roar", 0.9f, 0f);
                ScreenFX.Flash(Color.white, 0.7f, 0.6f);
                ScreenFX.Impact(1f, 1.2f);
                CameraRig.Shake(1f);
            }
            VFX.Spawn("boss_death", Pos + Vector2.up * 1.5f, Quaternion.identity);
            yield return new WaitForSecondsRealtime(1.2f);
            if (near)
            {
                GameEvents.RaiseBanner(BannerKind.Victory, "CHIẾN THẮNG!", $"Đã đánh bại {displayName}");
                AudioManager.Play("sfx_victory", 1f, 0f);
                CameraRig.SetZoom(1f);
                CameraRig.SetFocus(null);
            }
            if (!GameSession.Online) GameEvents.RaiseBossDisengaged(1.5f);
            yield return new WaitForSeconds(4f);
            if (!GameSession.Online) AudioManager.PlayMusic(ZoneMusic, 3f);
            // a client fades its copy with the server's
            if (GameSession.IsAuthority) yield break;
            if (style != null && style.Supported) yield return style.Dissolve(1.8f);
        }

        /// <summary>The fight's bar and music on this screen.</summary>
        void ShowFight(bool on)
        {
            if (on == shownHere) return;
            shownHere = on;
            if (on)
            {
                GameEvents.RaiseBossEngaged(health, displayName, level);
                AudioManager.PlayMusic("music_boss", 0.8f);
                return;
            }
            GameEvents.RaiseBossDisengaged(health.IsDead ? 1.5f : 0f);
            AudioManager.PlayMusic(ZoneMusic, 2f);
            CameraRig.SetZoom(1f);
            CameraRig.SetFocus(null);
        }

        /// <summary>Online: the bar and the boss music show while this screen's hero is in a fight with the bear.</summary>
        void LateUpdate()
        {
            if (!GameSession.Online || !GameSession.HasScreen) return;
            var me = Players.Local;
            bool fight = Engaged && me != null && Vector2.Distance(me.transform.position, home) <= LeashRadius;
            ShowFight(fight);
        }

        void OnDisable()
        {
            if (GameSession.Online && shownHere && GameSession.HasScreen) ShowFight(false);
        }

        /// <summary>A client's copy: whether the server's bear is fighting and enraged.</summary>
        public void SetRemoteFlags(bool engaged, bool isEnraged)
        {
            remoteEngaged = engaged;
            if (isEnraged != remoteEnraged)
            {
                remoteEnraged = isEnraged;
                Present(isEnraged ? Moment.Enrage : Moment.Reset);
            }
        }

        // ================================================================= save
        [System.Serializable]
        class SaveState
        {
            public bool defeated;
        }

        public string SaveKey => "boss:" + bossId;

        public string CaptureState() => JsonUtility.ToJson(new SaveState { defeated = state == State.Dead || health.IsDead });

        public void RestoreState(string json)
        {
            if (!JsonUtility.FromJson<SaveState>(json).defeated) return;
            StopAllCoroutines();
            ClearTelegraphs();
            state = State.Dead;
            gameObject.SetActive(false);
        }

        void OnDrawGizmosSelected()
        {
            Vector3 c = arenaCenter != null ? arenaCenter.position : transform.position;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(c, arenaRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(c, wakeRadius);
        }
    }
}

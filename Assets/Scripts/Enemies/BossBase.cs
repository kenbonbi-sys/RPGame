using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// What every boss and mini-boss shares; the attacks are the subclass's (<see cref="BossBear"/>
    /// the forest bear, the swamp's toad and snake mother). A boss sleeps in its arena, wakes for
    /// any hero who comes near or hurts it, fights the one it has the most threat on, enrages at
    /// <see cref="enrageAt"/> of its health, and resets once every hero is down or has left.
    /// More heroes in the fight make it tougher (<see cref="hpPerExtraHero"/>).
    /// A grinding game (Terraria-like, not a linear story): it comes back
    /// <see cref="respawnSeconds"/> after every fall, online and offline, and everyone who hurt it
    /// gets the kill and a chest of their own loot in the middle of the arena each time
    /// (<see cref="TreasureChest"/>). Nothing about it is saved.
    /// Online (Docs/KeHoach-Online.md, phase 3) the server fights; every screen near the arena gets
    /// its warnings, roars and effects (<see cref="NetCues"/>), its big moments
    /// (<see cref="Present"/>), and shows its bar and music while its hero is in the fight.
    /// Its animation set needs: idle, walk, roar, hurt, dead.
    /// </summary>
    public abstract class BossBase : MonoBehaviour
    {
        [Header("Identity")]
        public string bossId = "bear";
        public string displayName = "Gấu Ma Rừng Già";
        public string title = "Chúa Tể Rừng Già";
        public int level = 6;
        public float maxHp = 3200f;
        [Tooltip("Boss or MiniBoss: quests, the Bách Khoa Trùm and saves after big kills tell them apart.")]
        public EnemyRank rank = EnemyRank.Boss;
        [Tooltip("Where it lives, for the line when it comes back (\"… đã trở lại Rừng Già Cổ Thụ.\").")]
        public string homeName = "Rừng Già Cổ Thụ";

        [Header("Arena")]
        public Transform arenaCenter;
        public float arenaRadius = 11f;
        public float wakeRadius = 7.5f;
        [Tooltip("Seconds after its fall before it is back (every time, to be farmed again).")]
        public float respawnSeconds = 180f;
        [Tooltip("Extra health for every hero in the fight after the first, as a share of maxHp (0.7: two heroes face 170%). " +
                 "Counted as heroes arrive; it does not shrink before the fight ends.")]
        public float hpPerExtraHero = 0.7f;
        [Tooltip("Share of health left when it enrages (0: never).")]
        [Range(0f, 1f)] public float enrageAt = 0.5f;

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

        [Header("Movement and loot")]
        public float walkSpeed = 2.3f;
        public List<LootEntry> loot = new List<LootEntry>();
        [Tooltip("Coins dropped for each hero with the kill.")]
        public int coins = 12;

        /// <summary>A boss's big moments, shown by every screen near it.</summary>
        public enum Moment : byte
        {
            Intro = 1,
            Enrage = 2,
            Death = 3,
            Reset = 4
        }

        protected enum State { Dormant, Intro, Chase, Busy, Stunned, Returning, Dead }
        protected State state = State.Dormant;
        protected bool enraged;
        protected float recoverUntil;
        protected float baseWalkSpeed;
        readonly Dictionary<string, float> ready = new Dictionary<string, float>();
        readonly List<Telegraph> liveTelegraphs = new List<Telegraph>();
        protected Coroutine routine;
        protected GameObject auraFx;
        protected Vector2 home;
        protected PlayerController target;
        protected readonly ThreatTable threat = new ThreatTable();
        // a client's copy: what the server says
        bool remoteEngaged, remoteEnraged;
        // this screen shows the fight (bar, music) — online, only while its hero is in it
        bool shownHere;
        // most heroes in the fight at once since it began (its health follows)
        int fightHeroes;
        float nextHeroCount;

        public bool Engaged => GameSession.IsAuthority
            ? state != State.Dormant && state != State.Dead && state != State.Returning
            : remoteEngaged;
        public bool Enraged => GameSession.IsAuthority ? enraged : remoteEnraged;
        /// <summary>The hero it is fighting (null while dormant).</summary>
        public PlayerController Target => target;
        /// <summary>The middle of its arena.</summary>
        public Vector2 Home => home;

        /// <summary>Every boss and mini-boss in the loaded scenes, alive or defeated.</summary>
        public static readonly List<BossBase> All = new List<BossBase>();

        static int fightsShown;
        /// <summary>A boss fight's bar and music are on this screen (a place's music waits for it to end).</summary>
        public static bool FightShown => fightsShown > 0;

        /// <summary>The first boss of that id (tests, tools), or null.</summary>
        public static BossBase Find(string id) => All.Find(b => b != null && b.bossId == id);

        protected Vector2 Pos => transform.position;
        /// <summary>Heroes this far from the arena centre have left the fight.</summary>
        protected float LeashRadius => arenaRadius + 7f;
        /// <summary>Cooldowns shrink when enraged and grow with Lạnh.</summary>
        protected float CdMul => (enraged ? 0.65f : 1f) / Mathf.Max(0.1f, status != null ? status.AttackSpeedMultiplier : 1f);

        protected virtual void Awake()
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
            // the Bí Kíp of the Sách Chiêu its region keeps (T63)
            Spellbook.AddBossBooks(bossId, loot);
            All.Add(this);
        }

        protected virtual void OnDestroy()
        {
            All.Remove(this);
            if (shownHere) fightsShown = Mathf.Max(0, fightsShown - 1);
        }

        void OnPoiseBroken()
        {
            if (state == State.Chase || state == State.Busy) EnterStun();
        }

        protected virtual void Start()
        {
            home = arenaCenter != null ? (Vector2)arenaCenter.position : Pos;
            health.displayName = displayName;
            health.level = level;
            health.ResetHealth(maxHp);
            if (motor != null) motor.moveSpeed = walkSpeed;
            anim.Play("idle", true);
            MinimapUI.Register(transform, MinimapUI.MarkerKind.Boss);
        }

        // ================================================================= loop
        protected virtual void Update()
        {
            if (!GameSession.IsAuthority) return;   // online the server fights; this copy only shows
            if (Engaged && Time.time >= nextHeroCount)
            {
                nextHeroCount = Time.time + 0.5f;
                GrowWithHeroes();
            }
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
            if (TurnsFreely && body != null && Mathf.Abs(motor.Velocity.x) > 0.2f && state == State.Chase) body.flipX = motor.Velocity.x < 0;
        }

        /// <summary>
        /// The living hero in the fight (near the arena) it has the most threat on; the one
        /// nearest the arena when nobody has threat yet. Null: everyone is down or gone.
        /// </summary>
        protected PlayerController PickTarget()
        {
            var p = threat.Top(h => Vector2.Distance(h.transform.position, home) <= LeashRadius);
            if (p == null)
            {
                p = Players.Nearest(home, LeashRadius);
                if (p != null) threat.Add(p, 1f);
            }
            return p;
        }

        protected void ResetFight()
        {
            if (routine != null) StopCoroutine(routine);
            ClearTelegraphs();
            threat.Clear();
            target = null;
            fightHeroes = 0;
            enraged = false;
            walkSpeed = baseWalkSpeed;
            motor.moveSpeed = walkSpeed;
            if (auraFx != null) { VFX.Release(auraFx); auraFx = null; }
            health.ResetHealth(maxHp);
            health.ForgetAttackers();
            if (poise != null) poise.ResetPoise();
            state = State.Returning;
            if (bodyRoot != null) bodyRoot.localPosition = Vector3.zero;
            OnFightReset();
            NetCues.Boss(this, Moment.Reset);
        }

        /// <summary>The fight was reset or the boss came back: undo what the fight changed (the subclass's own state).</summary>
        protected virtual void OnFightReset() { }

        /// <summary>Health for a fight with <paramref name="heroes"/> heroes in it.</summary>
        public static float MaxHpFor(float baseHp, int heroes, float perExtraHero) =>
            baseHp * (1f + Mathf.Max(0f, perExtraHero) * Mathf.Max(0, heroes - 1));

        /// <summary>Heroes on their feet near the arena: the ones in the fight.</summary>
        int HeroesInFight()
        {
            int n = 0;
            foreach (var p in Players.All)
                if (p != null && !p.IsDead && Vector2.Distance(p.transform.position, home) <= LeashRadius) n++;
            return n;
        }

        /// <summary>A hero joined the fight: more health, the same share of it left.</summary>
        void GrowWithHeroes()
        {
            int n = HeroesInFight();
            if (n <= fightHeroes) return;
            fightHeroes = n;
            float max = MaxHpFor(maxHp, n, hpPerExtraHero);
            if (max <= health.maxHp) return;
            health.ScaleMax(max);
            NetCues.Log($"{displayName} mạnh lên: {n} người trong trận.", new Color(1f, 0.7f, 0.5f), Pos);
        }

        /// <summary>Music of the place the boss lives in (back to it after the fight).</summary>
        protected string AreaMusic
        {
            get
            {
                var area = ZoneArea.At(home);
                if (area != null && !string.IsNullOrEmpty(area.music)) return area.music;
                return ZoneRoot.Current != null && ZoneRoot.Current.def != null ? ZoneRoot.Current.def.music : "music_forest";
            }
        }

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
            if (!enraged && enrageAt > 0f && health.Fraction <= enrageAt)
            {
                Run(Enrage());
                return;
            }
            if (Decide(p, dist)) return;
            Approach(p);
        }

        /// <summary>
        /// Whether it turns to face the way it walks at once; a boss with a weak back (the old
        /// crystal golem) turns on its own, slowly.
        /// </summary>
        protected virtual bool TurnsFreely => true;

        /// <summary>Picks and starts an attack on <paramref name="p"/> (<see cref="Run"/>); false: nothing ready, walk on.</summary>
        protected abstract bool Decide(PlayerController p, float dist);

        /// <summary>Walks toward the hero but stays inside the arena.</summary>
        protected virtual void Approach(PlayerController p)
        {
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

        /// <summary>Picks one of the attacks that are off cooldown, by weight; null when none is.</summary>
        protected string PickWeighted(params (string id, float weight, bool usable)[] options)
        {
            float total = 0;
            foreach (var o in options)
                if (o.usable && Ready(o.id)) total += o.weight;
            if (total <= 0f) return null;
            float r = Random.value * total;
            foreach (var o in options)
            {
                if (!o.usable || !Ready(o.id)) continue;
                r -= o.weight;
                if (r <= 0) return o.id;
            }
            return null;
        }

        protected bool Ready(string id) => !ready.TryGetValue(id, out float t) || Time.time >= t;

        protected void SetCooldown(string id, float seconds) => ready[id] = Time.time + seconds * CdMul;

        /// <summary>Plays an attack; the boss chases again when it ends.</summary>
        protected void Run(IEnumerator r)
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

        protected void EnterStun()
        {
            if (routine != null) StopCoroutine(routine);
            ClearTelegraphs();
            if (bodyRoot != null) bodyRoot.localPosition = Vector3.zero;
            // an interrupted leap or dive leaves the colliders off
            foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = true;
            OnInterrupted();
            state = State.Stunned;
        }

        /// <summary>An attack was cut short (stunned, poise broken): put back what it changed.</summary>
        protected virtual void OnInterrupted() { }

        protected void Face(Vector2 at)
        {
            if (body != null) body.flipX = at.x < Pos.x;
        }

        protected void Announce(string skill)
        {
            NetCues.Announce(health, "Kỹ năng: " + skill);
            Bestiary.ForWitnesses(Pos, b => b.RecordSkill(displayName, skill));
        }

        protected Telegraph Warn(Telegraph t)
        {
            if (t != null) liveTelegraphs.Add(t);
            return t;
        }

        protected void ClearTelegraphs()
        {
            foreach (var t in liveTelegraphs)
                if (t != null && t.gameObject.activeInHierarchy) t.Cancel();
            liveTelegraphs.Clear();
            NetCues.CancelTelegraphs(this);
        }

        // ================================================================= phases
        protected virtual IEnumerator Intro()
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
            OnFightStart();
            state = State.Chase;
            recoverUntil = Time.time + 0.5f;
        }

        /// <summary>The intro is over and the fight begins (first cooldowns).</summary>
        protected virtual void OnFightStart() { }

        protected virtual IEnumerator Enrage()
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

        /// <summary>Forces an attack by its id (AutoShot, the console); false when there is no such attack.</summary>
        public bool DebugForce(string attack)
        {
            if (!GameSession.IsAuthority) return false;
            var p = PickTarget() ?? Players.Local;
            if (p == null || state == State.Dead) return false;
            var r = AttackRoutine(attack, p);
            if (r == null) return false;
            if (state == State.Dormant) state = State.Chase;
            if (routine != null) StopCoroutine(routine);
            ClearTelegraphs();
            if (bodyRoot != null) bodyRoot.localPosition = Vector3.zero;
            foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = true;
            OnInterrupted();   // whatever the attack it cut short had changed
            Run(r);
            return true;
        }

        /// <summary>The routine of the attack <paramref name="id"/> on <paramref name="p"/>, or null.</summary>
        protected abstract IEnumerator AttackRoutine(string id, PlayerController p);

        /// <summary>Its attacks' ids (the console lists them).</summary>
        public abstract string[] Attacks { get; }

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
            OnInterrupted();
            StartCoroutine(DeathSequence());
        }

        IEnumerator DeathSequence()
        {
            anim.Play("dead", true);
            NetCues.Boss(this, Moment.Death);
            yield return new WaitForSecondsRealtime(1.2f);
            var credited = new List<PlayerController>(health.Attackers);
            ServerPlayers.ShareKill(credited, Pos);   // party members nearby
            // the loot waits in a chest in the middle of the arena, one for each hero with the kill
            TreasureChest.Leave(home, loot, coins, credited);
            GameEvents.RaiseEnemyKilled(new KillInfo
            {
                id = bossId, name = displayName, level = level, rank = rank, position = Pos, credited = credited
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
            // it returns, to be fought again (on the game manager: this object is switched off)
            if (respawnSeconds > 0f && GameManager.I != null) GameManager.I.StartCoroutine(ReturnLater(respawnSeconds));
        }

        IEnumerator ReturnLater(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (this == null || state != State.Dead) yield break;
            transform.position = home + Vector2.up * 1.2f;
            gameObject.SetActive(true);
            foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = true;
            enraged = false;
            fightHeroes = 0;
            walkSpeed = baseWalkSpeed;
            motor.moveSpeed = walkSpeed;
            health.ResetHealth(maxHp);
            if (poise != null) poise.ResetPoise();
            if (status != null) status.Cleanse();
            if (body != null) body.color = Color.white;
            anim.speed = 1f;
            anim.Play("idle", true);
            OnFightReset();
            state = State.Dormant;
            NetCues.Boss(this, Moment.Reset);
            NetCues.Log(string.IsNullOrEmpty(homeName) ? $"{displayName} đã trở lại." : $"{displayName} đã trở lại {homeName}.", Palette.LogQuest, home, 60f);
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
            if (!GameSession.Online) AudioManager.PlayMusic(AreaMusic, 3f);
            // a client fades its copy with the server's
            if (GameSession.IsAuthority) yield break;
            if (style != null && style.Supported) yield return style.Dissolve(1.8f);
        }

        /// <summary>The fight's bar and music on this screen.</summary>
        void ShowFight(bool on)
        {
            if (on == shownHere) return;
            shownHere = on;
            fightsShown = Mathf.Max(0, fightsShown + (on ? 1 : -1));
            if (on)
            {
                GameEvents.RaiseBossEngaged(health, displayName, level);
                AudioManager.PlayMusic("music_boss", 0.8f);
                return;
            }
            GameEvents.RaiseBossDisengaged(health.IsDead ? 1.5f : 0f);
            AudioManager.PlayMusic(AreaMusic, 2f);
            CameraRig.SetZoom(1f);
            CameraRig.SetFocus(null);
        }

        /// <summary>Online: the bar and the boss music show while this screen's hero is in a fight with it.</summary>
        protected virtual void LateUpdate()
        {
            if (!GameSession.Online || !GameSession.HasScreen) return;
            var me = Players.Local;
            bool fight = Engaged && me != null && Vector2.Distance(me.transform.position, home) <= LeashRadius;
            ShowFight(fight);
        }

        protected virtual void OnDisable()
        {
            if (GameSession.Online && shownHere && GameSession.HasScreen) ShowFight(false);
        }

        /// <summary>A client's copy: whether the server's boss is fighting and enraged.</summary>
        public void SetRemoteFlags(bool engaged, bool isEnraged)
        {
            remoteEngaged = engaged;
            if (isEnraged != remoteEnraged)
            {
                remoteEnraged = isEnraged;
                Present(isEnraged ? Moment.Enrage : Moment.Reset);
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Vector3 c = arenaCenter != null ? arenaCenter.position : transform.position;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(c, arenaRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(c, wakeRadius);
        }
    }
}

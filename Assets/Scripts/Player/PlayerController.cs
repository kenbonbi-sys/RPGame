using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>An active buff on the hero (see <see cref="BuffSpec"/>), shown in the buff bar.</summary>
    public class Buff
    {
        public string id;
        public string name;
        public Sprite icon;
        public float until;
        public float duration;
        public BuffSpec spec;
        public GameObject vfx;
        public float Remaining => Mathf.Max(0, until - Time.time);
    }

    /// <summary>
    /// A hero. The one this machine controls (<see cref="Players.Local"/>) turns the right mouse
    /// button or arrow keys (walk), the left one (attack the enemy clicked, or swing toward the
    /// mouse), a click on an NPC (talk), Q W E R A S D Space (skills), 1 2 3 (potions), F (talk)
    /// and T (<see cref="AutoHunt"/>) into a <see cref="PlayerIntent"/>. Every
    /// hero acts on its intent the same way, wherever it comes from (online: the network).
    /// The hero carries its own stats, bag, quest log and Bách Khoa Trùm.
    /// </summary>
    public class PlayerController : MonoBehaviour, ICharacterSaveable, IAbilityCaster
    {
        [Header("Refs")]
        public CharacterMotor motor;
        public SpriteAnimator anim;
        public Health health;
        public StatusEffects status;
        public HitFlash flash;
        public AfterImageSpawner afterImages;
        public SpriteRenderer body;
        public PlayerSkills skills;
        public PlayerStats stats;
        public PerfectDodge perfectDodge;
        public Inventory inventory;
        public QuestSystem quests;
        public Bestiary bestiary;
        public WaystoneLog waystones;

        [Header("Stats")]
        public float maxEnergy = 63f;
        public float energy = 50f;
        public float energyRegen = 3.5f;
        public float hpRegen = 2.5f;
        public float hpRegenDelay = 5f;
        public float interactRadius = 1.8f;
        public float basicAttackRange = 1.5f;

        [Header("Potions (slots 1 2 3)")]
        public string[] potionIds = { "potion_red", "potion_blue", "potion_green" };
        public float potionCooldown = 1f;

        public bool IsDead => health != null && health.IsDead;
        /// <summary>The hero this machine shows and controls: HUD, camera, input and screen effects follow it.</summary>
        public bool IsLocal => Players.Local == this;
        /// <summary>
        /// Online, the copy of someone else's hero: it follows the positions the network sends,
        /// animates from its motion and does nothing on its own (no input, intent or regeneration).
        /// </summary>
        public bool Puppet { get; private set; }
        /// <summary>The local hero follows <see cref="SetIntent"/> instead of the keyboard and mouse (automated runs, bots).</summary>
        public bool Autopilot { get; set; }
        public readonly List<Buff> buffs = new List<Buff>();
        public float PotionReadyIn => Mathf.Max(0, potionReadyAt - Time.time);

        float actionUntil;
        float actionMoveMul = 1f;
        Vector2 actionDir;
        string actionAnim;
        float potionReadyAt;
        float hurtAnimUntil;
        float regenFraction;
        PlayerIntent intent;
        // where this machine's clicks sent the hero (input only)
        Vector2 moveTarget;
        bool hasMoveTarget;
        Health attackTarget;
        NPC npcTarget;
        bool swinging;
        readonly List<Collider2D> clickHits = new List<Collider2D>();

        /// <summary>Tự Động: this machine's hero hunting on its own (T).</summary>
        public readonly AutoHunt auto = new AutoHunt();

        void Awake()
        {
            if (motor == null) motor = GetComponent<CharacterMotor>();
            if (health == null) health = GetComponent<Health>();
            if (status == null) status = GetComponent<StatusEffects>();
            if (skills == null) skills = GetComponent<PlayerSkills>();
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (stats == null) stats = gameObject.AddComponent<PlayerStats>();   // prefabs made before stats existed
            if (perfectDodge == null) perfectDodge = gameObject.GetOrAdd<PerfectDodge>();
            // prefabs made before the hero carried its own bag, quest log and bestiary
            if (inventory == null) inventory = gameObject.GetOrAdd<Inventory>();
            if (quests == null) quests = gameObject.GetOrAdd<QuestSystem>();
            if (bestiary == null) bestiary = gameObject.GetOrAdd<Bestiary>();
            if (waystones == null) waystones = gameObject.GetOrAdd<WaystoneLog>();
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            if (anim != null) anim.FrameChanged += OnFrame;
            skills.BufferedCast += OnKeySkillCast;
            stats.LookChanged += OnLookChanged;
            SaveRegistry.Register(this);
        }

        void Start() => OnLookChanged();

        /// <summary>
        /// Their people, class, weapon or looks changed: the skill bar takes the class's skills
        /// (Q from the weapon), the sprite is drawn again, and a people with darkvision carries
        /// a wider light.
        /// </summary>
        void OnLookChanged()
        {
            if (stats == null) return;
            if (stats.HasClass) skills.ApplyKit(stats.Class, stats.Weapon);
            HeroArt.ApplyTo(this);
            var lamp = GetComponentInChildren<NightLight>(true);
            if (lamp != null) lamp.rangeScale = stats.Darkvision ? 1.6f : 1f;
        }

        void OnEnable() => Players.Register(this);

        void OnDisable() => Players.Unregister(this);

        void OnDestroy() => SaveRegistry.Unregister(this);

        // ------------------------------------------------------------------ ability caster
        MonoBehaviour IAbilityCaster.Runner => this;
        Team IAbilityCaster.Team => health.team;
        Health IAbilityCaster.Health => health;
        CharacterMotor IAbilityCaster.Motor => motor;
        StatusEffects IAbilityCaster.Status => status;
        AfterImageSpawner IAbilityCaster.AfterImages => afterImages;
        int IAbilityCaster.Level => stats != null ? stats.level : 1;

        /// <summary>Physical skills use Công vật lý, every element Công phép (plan §04).</summary>
        public float Attack(DamageType type)
        {
            if (stats == null) return ProgressionConfig.Current.PhysicalAttack(ProgressionConfig.Current.referenceScore);
            return stats.Stats.Get(type == DamageType.Physical ? StatId.PhysicalAttack : StatId.MagicAttack);
        }

        public float DamageDealt(AbilityDef ability) => stats != null ? stats.DamageDealt(ability) : 1f;

        // ------------------------------------------------------------------ buffs
        /// <summary>Adds or refreshes a buff; its speed, damage-taken and stun-immunity apply while it lasts.</summary>
        public void AddBuff(BuffSpec spec) => AddBuff(spec, -1f);

        /// <summary>
        /// Adds or refreshes a buff for <paramref name="remaining"/> seconds (its full duration when
        /// negative). A server tells every screen, so all of them show it and its player's buff bar
        /// and speed follow it.
        /// </summary>
        public void AddBuff(BuffSpec spec, float remaining)
        {
            if (spec == null) return;
            var b = buffs.Find(x => x.id == spec.id);
            if (b == null)
            {
                b = new Buff { id = spec.id };
                buffs.Add(b);
                if (!string.IsNullOrEmpty(spec.attachedVfx) && GameSession.HasScreen)
                    b.vfx = VFX.Spawn(spec.attachedVfx, transform.position, Quaternion.identity, 1f, transform, true);
            }
            b.spec = spec;
            b.name = spec.displayName;
            b.icon = spec.icon;
            b.duration = spec.duration;
            b.until = Time.time + (remaining >= 0f ? remaining : spec.duration);
            ApplyBuffs();
            if (GameSession.Serving) NetWorld.BuffChanged(this, spec.id, b.Remaining, true);
        }

        public void RemoveBuff(string id)
        {
            var b = buffs.Find(x => x.id == id);
            if (b == null) return;
            buffs.Remove(b);
            EndBuff(b, true);
            ApplyBuffs();
            if (GameSession.Serving) NetWorld.BuffChanged(this, id, 0f, false);
        }

        public bool HasBuff(string id) => buffs.Exists(b => b.id == id && b.Remaining > 0);

        void EndBuff(Buff b, bool showEnd)
        {
            if (b.vfx != null) VFX.Release(b.vfx);
            b.vfx = null;
            if (showEnd && !IsDead && b.spec != null && !string.IsNullOrEmpty(b.spec.endVfx) && GameSession.HasScreen)
                VFX.Spawn(b.spec.endVfx, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        }

        /// <summary>The fastest this hero may walk now (its buffs; slows left out): the server's move check (<see cref="MoveCheck"/>).</summary>
        public float TopWalkSpeed => (motor != null ? motor.moveSpeed : 4f) * Mathf.Max(1f, BuffSpeed);

        /// <summary>Speed multiplier of all buffs together.</summary>
        float BuffSpeed
        {
            get
            {
                float m = 1f;
                foreach (var b in buffs)
                    if (b.spec != null) m *= b.spec.speedMultiplier;
                return m;
            }
        }

        /// <summary>The source of the stat modifiers buffs give (Cuồng Nộ's damage, Bùng Nổ Hành Động's speed).</summary>
        readonly object buffMods = new object();

        void ApplyBuffs()
        {
            float taken = 1f, dealt = 1f, speed = 0f;
            bool immune = false;
            foreach (var b in buffs)
            {
                if (b.spec == null) continue;
                taken *= b.spec.damageTakenMultiplier;
                dealt *= b.spec.damageDealtMultiplier > 0f ? b.spec.damageDealtMultiplier : 1f;
                speed += b.spec.attackSpeedBonus;
                immune |= b.spec.stunImmune;
            }
            health.damageTakenMultiplier = taken;
            if (status != null) status.stunImmune = immune;
            if (stats != null)
            {
                stats.Stats.RemoveFrom(buffMods);
                if (!Mathf.Approximately(dealt, 1f)) stats.Stats.Add(new StatModifier(StatId.DamageDealt, ModKind.PercentMult, dealt - 1f, buffMods));
                if (speed != 0f) stats.Stats.Add(new StatModifier(StatId.AttackSpeed, ModKind.Flat, speed, buffMods));
            }
        }

        void ClearBuffs()
        {
            foreach (var b in buffs) EndBuff(b, false);
            buffs.Clear();
            ApplyBuffs();
        }

        // ------------------------------------------------------------------ actions
        /// <summary>Called by skills: locks the pose for a moment and faces the aim.</summary>
        public void BeginAction(string animBase, Vector2 dir, float lockTime, float moveMul)
        {
            actionAnim = animBase;
            actionDir = dir;
            actionUntil = Time.time + lockTime;
            actionMoveMul = moveMul;
            motor.Facing = dir;
            if (anim != null && !string.IsNullOrEmpty(animBase)) anim.PlayDir(animBase, dir, true);
        }

        public bool IsActing => Time.time < actionUntil;
        /// <summary>Seconds left in the current attack / cast pose.</summary>
        public float ActionRemaining => Mathf.Max(0f, actionUntil - Time.time);

        /// <summary>
        /// What a hero controlled from elsewhere wants to do (online: the network; tests). Presses
        /// act once; walking and aim carry on until the next intent. The local hero reads its own input.
        /// </summary>
        public void SetIntent(PlayerIntent i) => intent = i;

        void Update()
        {
            UpdateBuffs();
            // regeneration is the world's rule: offline, on a host and on a server (every hero there)
            if (GameSession.IsAuthority && !IsDead) Regen();
            if (IsLocal) UpdateDanger();
            if (Puppet)
            {
                AnimateFromMotion();
                return;
            }
            if (IsDead) return;
            if (IsLocal && !Autopilot) intent = ReadLocalInput();
            Act(intent);
            intent = intent.Held;
        }

        // ------------------------------------------------------------------ puppet (online)
        Vector2 puppetLastPos;
        float puppetWalkUntil, puppetDashUntil;

        /// <summary>Turns this hero into a puppet of the network, or back into a hero that acts on its own.</summary>
        public void SetPuppet(bool on)
        {
            Puppet = on;
            intent = default;
            hasMoveTarget = false;
            attackTarget = null;
            npcTarget = null;
            if (motor != null)
            {
                motor.HardStop();
                motor.enabled = !on;   // the network moves a puppet, not physics
            }
            puppetLastPos = transform.position;
        }

        /// <summary>
        /// Walk, dash or idle from how fast the puppet moved; updates arrive in steps, so a short
        /// pause keeps walking. A skill's pose (the server shows it) plays through; a stun shows hurt.
        /// </summary>
        void AnimateFromMotion()
        {
            Vector2 pos = transform.position;
            float dt = Time.deltaTime;
            Vector2 v = dt > 0f ? (pos - puppetLastPos) / dt : Vector2.zero;
            puppetLastPos = pos;
            float speed = v.magnitude;
            if (speed > 0.3f)
            {
                if (!IsActing) motor.Facing = v / speed;
                puppetWalkUntil = Time.time + 0.15f;
            }
            if (speed > 9f) puppetDashUntil = Time.time + 0.12f;
            if (anim == null || IsDead) return;
            if (Time.time < puppetDashUntil) anim.PlayDir("dash", motor.Facing);
            else if (IsActing) { }   // BeginAction started the attack or cast pose
            else if (status != null && status.IsStunned) anim.PlayDir("hurt", motor.Facing);
            else if (Time.time < puppetWalkUntil) anim.PlayDir("walk", motor.Facing);
            else anim.PlayDir("idle", motor.Facing);
        }

        /// <summary>Acts on an intent: potions, skills, talking, walking. The same for every hero.</summary>
        void Act(PlayerIntent i)
        {
            if (status != null && status.IsStunned)
            {
                motor.Stop();
                if (anim != null) anim.PlayDir("hurt", motor.Facing);
                return;
            }
            for (int s = 0; s < 3; s++)
                if (i.PotionPressed(s)) UsePotion(s);
            for (int s = 0; s < 8; s++)
                if (i.SkillPressed(s) && skills.Request(s, i.aim)) OnKeySkillCast(s);
            if (i.basicAttack) skills.TryCast(0, i.basicAttackAt);
            if (i.talkTo != null && Vector2.Distance(transform.position, i.talkTo.transform.position) <= interactRadius)
                i.talkTo.Interact(this);
            float speedMul = (status != null ? status.SpeedMultiplier : 1f) * (IsActing ? actionMoveMul : 1f);
            speedMul *= BuffSpeed * (stats != null ? stats.SpeedMultiplier : 1f);
            if (i.face.sqrMagnitude > 0.01f) motor.Facing = i.face;
            motor.Move(i.move, speedMul);
            if (IsActing) motor.Facing = actionDir;
            UpdateAnimation(i.move);
        }

        void Regen()
        {
            energy = Mathf.Min(maxEnergy, energy + energyRegen * Time.deltaTime);
            if (Time.time - health.LastDamageTime > hpRegenDelay && health.hp < health.maxHp)
            {
                regenFraction += hpRegen * Time.deltaTime;
                if (regenFraction >= 1f)
                {
                    float whole = Mathf.Floor(regenFraction);
                    regenFraction -= whole;
                    health.Heal(whole, false);
                }
            }
        }

        /// <summary>The red vignette of low health, on this screen.</summary>
        void UpdateDanger() => ScreenFX.SetDanger(!IsDead && health.Fraction < 0.3f ? 1f - health.Fraction / 0.3f : 0f);

        void UpdateBuffs()
        {
            bool changed = false;
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                if (buffs[i].Remaining > 0) continue;
                var b = buffs[i];
                buffs.RemoveAt(i);
                EndBuff(b, true);
                changed = true;
            }
            if (changed) ApplyBuffs();
        }

        /// <summary>
        /// Drinks the potion of slot 1–3. Online, a player's machine asks the server (the bottle,
        /// the healing and the effect come back from there); the cooldown shows at once.
        /// </summary>
        public bool UsePotion(int slot)
        {
            if (slot < 0 || slot >= potionIds.Length || Time.time < potionReadyAt) return false;
            var db = GameManager.I != null ? GameManager.I.db : null;
            var item = db != null ? db.Item(potionIds[slot]) : null;
            if (item == null || inventory == null || inventory.Count(item) <= 0)
            {
                Notify.WorldText(this, "Hết bình!", health.HeadPosition + Vector3.up * 0.4f, new Color(0.8f, 0.8f, 0.8f));
                Notify.Sound(this, "sfx_denied", 0.5f);
                return false;
            }
            potionReadyAt = Time.time + potionCooldown;
            if (!GameSession.IsAuthority)
            {
                OnlineSession.Ask(new ActRequest { kind = ActKind.Potion, value = slot });
                return true;
            }
            inventory.Remove(item, 1);
            if (item.healAmount > 0) health.Heal(item.healAmount);
            if (item.energyAmount > 0)
            {
                float before = energy;
                energy = Mathf.Min(maxEnergy, energy + item.energyAmount);
                NetCues.WorldText($"+{Mathf.RoundToInt(energy - before)}", health.HeadPosition + Vector3.right * 0.4f, Palette.Energy);
            }
            if (item.cleanse && status != null) status.Cleanse();
            string fx = slot == 0 ? "potion_red" : slot == 1 ? "potion_blue" : "potion_green";
            NetCues.VfxOn(fx, this);
            NetCues.Sound("sfx_potion", 0.8f, 0.06f, transform.position);
            return true;
        }

        // ------------------------------------------------------------------ this machine's input
        /// <summary>
        /// The keyboard and mouse as an intent. Remembers where clicks sent the hero (walk there,
        /// attack that enemy, talk to that NPC) and keeps going there on later frames.
        /// </summary>
        PlayerIntent ReadLocalInput()
        {
            var i = new PlayerIntent();
            var gm = GameManager.I;
            bool playing = gm == null || gm.State == GameState.Playing;
            if (!playing || (status != null && status.IsStunned))
            {
                hasMoveTarget = false;
                return i;
            }
            for (int s = 0; s < 3; s++)
                if (InputReader.PotionPressed(s)) i.potionPresses |= 1 << s;
            for (int s = 0; s < 8; s++)
                if (InputReader.SkillPressed(s)) i.skillPresses |= 1 << s;
            i.aim = InputReader.MouseWorld;
            if (InputReader.ToggleAuto) auto.Set(this, !auto.On);
            if (auto.On)
            {
                // walking by hand takes the hero back
                bool byHand = InputReader.ArrowMove.sqrMagnitude > 0.01f ||
                              ((InputReader.RightPressed || InputReader.LeftPressed) && !InputReader.PointerOverUI);
                if (!byHand) return auto.Think(this, i);
                auto.Set(this, false);
            }
            if (attackTarget != null && (attackTarget.IsDead || !attackTarget.gameObject.activeInHierarchy || !HasBody(attackTarget)))
                attackTarget = null;
            if (InputReader.Interact) i.talkTo = NPC.Nearest(transform.position, interactRadius);
            if (npcTarget != null && Vector2.Distance(transform.position, npcTarget.transform.position) <= interactRadius)
            {
                i.talkTo = npcTarget;
                npcTarget = null;
                hasMoveTarget = false;
            }
            i.move = ComputeMove(ref i);
            // a clicked enemy within reach: the basic attack, again and again until it falls
            if (attackTarget != null && Reach(transform.position, attackTarget) <= basicAttackRange && !IsActing)
            {
                i.basicAttack = true;
                i.basicAttackAt = attackTarget.transform.position;
            }
            return i;
        }

        /// <summary>
        /// What a left click on an enemy does: walk up to it and strike it with the basic attack
        /// again and again until it falls (or the player does something else).
        /// </summary>
        public void Attack(Health enemy)
        {
            attackTarget = enemy;
            npcTarget = null;
            swinging = false;
            hasMoveTarget = false;
        }

        /// <summary>A key-pressed skill fired (now or from the input buffer): stop walking / auto-attacking.</summary>
        void OnKeySkillCast(int slot)
        {
            hasMoveTarget = false;
            attackTarget = null;
        }

        Vector2 ComputeMove(ref PlayerIntent i)
        {
            Vector2 arrows = InputReader.ArrowMove;
            if (arrows.sqrMagnitude > 0.01f)
            {
                hasMoveTarget = false;
                attackTarget = null;
                npcTarget = null;
                return arrows;
            }

            bool overUI = InputReader.PointerOverUI;
            Vector2 m = InputReader.MouseWorld;
            Vector2 at = transform.position;
            // right button: walk there (a click on an NPC walks over and talks)
            if (InputReader.RightPressed && !overUI)
            {
                attackTarget = null;
                swinging = false;
                npcTarget = PickNpc(m);
                moveTarget = m;
                hasMoveTarget = true;
                if (npcTarget == null) VFX.Spawn("click_marker", m, Quaternion.identity);
            }
            if (InputReader.RightHeld && !overUI && npcTarget == null)
            {
                attackTarget = null;
                moveTarget = m;
                hasMoveTarget = true;
            }
            // left button: fight (a click on an enemy goes after it; elsewhere a swing toward the mouse)
            if (InputReader.LeftPressed && !overUI)
            {
                var enemy = PickEnemy(m);
                Attack(enemy);
                npcTarget = enemy == null ? PickNpc(m) : null;
                swinging = enemy == null && npcTarget == null;
            }
            if (!InputReader.LeftHeld) swinging = false;
            if (swinging)
            {
                // held over an enemy: go after that one
                attackTarget = PickEnemy(m);
                if (attackTarget != null) swinging = false;
                else
                {
                    Vector2 dir = m - at;
                    if (dir.sqrMagnitude > 0.01f) i.face = dir.normalized;
                    if (!IsActing)
                    {
                        i.basicAttack = true;
                        i.basicAttackAt = m;
                    }
                    return Vector2.zero;
                }
            }

            if (attackTarget != null)
            {
                Vector2 to = (Vector2)attackTarget.transform.position - at;
                if (Reach(at, attackTarget) > basicAttackRange * 0.7f) return to.normalized;
                i.face = to.normalized;
                return Vector2.zero;
            }
            if (npcTarget != null)
            {
                Vector2 to = (Vector2)npcTarget.transform.position - (Vector2)transform.position;
                return to.magnitude > interactRadius * 0.8f ? to.normalized : Vector2.zero;
            }
            if (hasMoveTarget)
            {
                Vector2 to = moveTarget - (Vector2)transform.position;
                if (to.magnitude < 0.15f)
                {
                    hasMoveTarget = false;
                    return Vector2.zero;
                }
                return to.magnitude < 0.6f ? to / 0.6f : to.normalized;
            }
            return Vector2.zero;
        }

        // ------------------------------------------------------------------ what the mouse is on
        static readonly List<Collider2D> Cols = new List<Collider2D>(8);

        /// <summary>
        /// The enemy under the mouse: anywhere on its body from feet to head, not only on the small
        /// collider at its feet; the one nearest the pointer when bodies overlap. Anything else that
        /// can be hit (a boulder) by its collider.
        /// </summary>
        Health PickEnemy(Vector2 m)
        {
            Health best = null;
            float bestD = float.MaxValue;
            void Consider(Health h)
            {
                if (h == null || h.IsDead || !h.gameObject.activeInHierarchy || !h.CanBeDamagedBy(Team.Player) || !HasBody(h)) return;
                float d = DistToSegment(m, h.transform.position, h.HeadPosition);
                if (d < Mathf.Max(0.45f, BodyRadius(h) + 0.2f) && d < bestD)
                {
                    bestD = d;
                    best = h;
                }
            }
            foreach (var e in EnemyBase.All) if (e != null) Consider(e.health);
            foreach (var b in BossBase.All) if (b != null) Consider(b.health);
            if (best != null) return best;
            clickHits.Clear();
            var filter = new ContactFilter2D();
            filter.SetLayerMask(Layers.EnemyMask | Layers.ObstacleMask);
            filter.useTriggers = true;
            Physics2D.OverlapCircle(m, 0.45f, filter, clickHits);
            foreach (var c in clickHits)
            {
                var h = c.GetComponentInParent<Health>();
                if (h != null && h != health && !h.IsDead && h.CanBeDamagedBy(Team.Player)) return h;
            }
            return null;
        }

        /// <summary>The NPC under the mouse (anywhere on them), the nearest one when two stand together.</summary>
        static NPC PickNpc(Vector2 m)
        {
            NPC best = null;
            float bestD = 0.6f;
            foreach (var n in NPC.All)
            {
                if (n == null || !n.gameObject.activeInHierarchy) continue;
                Vector2 feet = n.transform.position;
                float d = DistToSegment(m, feet, feet + Vector2.up * 1.4f);
                if (d < bestD)
                {
                    bestD = d;
                    best = n;
                }
            }
            return best;
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>
        /// The solid colliders of a character (its trigger ones when it has only those, like a bat):
        /// what a blow has to reach.
        /// </summary>
        static List<Collider2D> BodyOf(Health h)
        {
            Cols.Clear();
            h.GetComponentsInChildren(false, Cols);
            bool solid = Cols.Exists(c => c.enabled && !c.isTrigger);
            Cols.RemoveAll(c => !c.enabled || (solid && c.isTrigger));
            return Cols;
        }

        /// <summary>Whether a character can be hit at all now (a snake under the water, a wisp faded away cannot).</summary>
        public static bool HasBody(Health h) => h != null && BodyOf(h).Count > 0;

        static float BodyRadius(Health h)
        {
            float r = 0f;
            foreach (var c in BodyOf(h)) r = Mathf.Max(r, c.bounds.extents.x);
            return r;
        }

        /// <summary>
        /// How far a blow from <paramref name="from"/> has to reach to touch <paramref name="h"/>: to the
        /// edge of its body, not its middle (a big enemy's middle is out of reach of a hero pressed against it).
        /// </summary>
        public static float Reach(Vector2 from, Health h)
        {
            float best = float.MaxValue;
            foreach (var c in BodyOf(h)) best = Mathf.Min(best, Vector2.Distance(from, c.ClosestPoint(from)));
            return best < float.MaxValue ? best : Mathf.Max(0f, Vector2.Distance(from, h.transform.position) - 0.3f);
        }

        // ------------------------------------------------------------------ anim
        void UpdateAnimation(Vector2 move)
        {
            if (anim == null) return;
            if (motor.IsDashing)
            {
                anim.PlayDir("dash", motor.Facing);
                return;
            }
            if (IsActing)
            {
                // action clip was started in BeginAction; keep it (it is non-looping for attacks)
                return;
            }
            if (Time.time < hurtAnimUntil)
            {
                anim.PlayDir("hurt", motor.Facing);
                return;
            }
            if (move.sqrMagnitude > 0.01f) anim.PlayDir("walk", move);
            else anim.PlayDir("idle", motor.Facing);
        }

        void OnFrame(string clip, int frame)
        {
            if (clip != null && clip.StartsWith("walk") && (frame == 0 || frame == 2))
            {
                var zone = ZoneRoot.Current;
                bool wading = zone != null && zone.IsWater(transform.position);
                AudioManager.Play(wading ? "sfx_wade" : "sfx_step", wading ? 0.3f : 0.25f, 0.15f, IsLocal ? (Vector3?)null : transform.position, 0.1f);
                if (wading) VFX.Spawn("water_step", transform.position + new Vector3(Random.Range(-0.1f, 0.1f), 0.05f), Quaternion.identity);
                else if (Random.value < 0.6f) VFX.Spawn("step_dust", transform.position + new Vector3(Random.Range(-0.1f, 0.1f), 0.05f), Quaternion.identity);
            }
        }

        // ------------------------------------------------------------------ damage
        void OnDamaged(DamageInfo d, float amount)
        {
            if (d.dot)
            {
                // Bỏng / Độc ticks: a light flash, no shake or hurt sound every half second
                if (flash != null) flash.Flash(new Color(1f, 0.3f, 0.3f), 0.4f, 0.1f);
                return;
            }
            if (flash != null) flash.Flash(new Color(1f, 0.3f, 0.3f), 0.9f, 0.18f);
            if (amount >= 12f) hurtAnimUntil = Time.time + 0.18f;
            if (IsLocal)
            {
                CameraRig.Shake(Mathf.Clamp(amount / 60f, 0.1f, 0.45f));
                ScreenFX.Flash(new Color(0.9f, 0.1f, 0.1f), Mathf.Clamp(amount / 80f, 0.12f, 0.35f), 0.3f);
                AudioManager.Play("sfx_player_hurt", 0.8f);
            }
            // the machine that moves the hero pushes it (a server's copy of someone else's hero does not)
            if (motor != null && motor.enabled && d.knockback > 0) motor.AddKnockback(d.direction * d.knockback);
            if (GameSession.IsAuthority) Rebuke(d);
        }

        /// <summary>Quỷ Duệ: Trả Đòn Địa Ngục — now and then an attacker is set alight.</summary>
        void Rebuke(DamageInfo d)
        {
            var race = stats != null ? stats.Race : null;
            if (race == null || race.rebukeChance <= 0f || d.source == null || Random.value >= race.rebukeChance) return;
            var foe = d.source.GetComponentInParent<Health>();
            if (foe == null || foe.IsDead || foe == health || !foe.CanBeDamagedBy(Team.Player)) return;
            var hit = DamageInfo.Make(6f, Team.Player, gameObject, foe.transform.position, Vector2.up, DamageType.Fire);
            hit.status.burn = 2;
            hit.skillName = "Trả Đòn Địa Ngục";
            foe.TakeDamage(hit);
            NetCues.Vfx("hit_fire", foe.transform.position + Vector3.up * 0.5f);
        }

        void OnDied(DamageInfo d)
        {
            motor.HardStop();
            hasMoveTarget = false;
            attackTarget = null;
            swinging = false;
            auto.Set(this, false, "gục ngã");
            ClearBuffs();
            if (anim != null) anim.Play("dead", true);
            if (IsLocal)
            {
                AudioManager.Play("sfx_player_die");
                TimeFX.SlowMo(0.3f, 1.2f);
            }
            else if (GameSession.HasScreen) AudioManager.Play("sfx_player_die", 1f, 0.06f, transform.position);
            if (GameManager.I != null) GameManager.I.OnPlayerDied(this);
        }

        public void Respawn(Vector2 at)
        {
            motor.Teleport(at);
            puppetLastPos = at;
            health.ResetHealth();
            energy = maxEnergy;
            if (status != null) status.Cleanse();
            skills.ResetCooldowns();
            health.invulnerable = false;
            if (anim != null) anim.Play("idle_down", true);
            // the server shows it to everyone, its player included
            if (GameSession.IsAuthority) NetCues.Vfx("respawn", at);
        }

        // ------------------------------------------------------------------ online: the server's word
        float energyHoldUntil;

        /// <summary>This screen just spent energy on a skill it showed at once: the server's number catches up in a moment.</summary>
        public void HoldEnergy(float seconds) => energyHoldUntil = Time.time + seconds;

        /// <summary>
        /// A client's copy takes the server's numbers: health, energy, statuses. Other players'
        /// heroes also fall and get up with them; this player's own hero falls and gets up when the
        /// server says so directly (<see cref="ControlKind.Downed"/>, <see cref="ControlKind.Respawn"/>).
        /// </summary>
        public void ApplyServerState(HeroState s)
        {
            bool dead = (s.flags & 1) != 0;
            if (!IsLocal)
            {
                if (dead && !IsDead) health.SetRemoteDead(true);
                else if (!dead && IsDead)
                {
                    health.SetRemoteDead(false);
                    if (anim != null) anim.Play("idle_down", true);
                }
                remoteLevel = s.level;
            }
            if (!IsDead) health.SetRemote(s.hp, s.maxHp);
            maxEnergy = s.maxEnergy;
            if (Time.time >= energyHoldUntil) energy = Mathf.Min(s.energy, maxEnergy);
            health.invulnerable = (s.flags & 2) != 0;
            if (status != null) status.ApplyView(s.status);
        }

        int remoteLevel;

        /// <summary>The hero's level as this screen knows it (other players' heroes: from the server).</summary>
        public int Level => stats != null && (IsLocal || GameSession.IsAuthority) ? stats.level : Mathf.Max(1, remoteLevel);

        // ------------------------------------------------------------------ save
        [System.Serializable]
        class SaveState
        {
            public float x, y, hp, energy;
            public int level, xp, statPoints, talentPoints, skillPoints;
            public int[] allocated;
            /// <summary>Their people, class, weapon and looks (<see cref="HeroLook"/> as JSON); empty in saves made before classes.</summary>
            public string look;
        }

        public string SaveKey => "player";

        PlayerController ICharacterSaveable.Owner => this;

        public string CaptureState()
        {
            var s = new SaveState { x = transform.position.x, y = transform.position.y, hp = health.hp, energy = energy };
            if (IsDead)
            {
                // saved while down (online: a player left mid-fall): back on their feet where they would get up
                if (GameManager.I != null && GameManager.I.RespawnPointFor(this, out Vector2 spawn))
                {
                    s.x = spawn.x;
                    s.y = spawn.y;
                }
                s.hp = health.maxHp;
                s.energy = maxEnergy;
            }
            if (stats != null)
            {
                s.level = stats.level;
                s.xp = stats.xp;
                s.allocated = stats.allocated;
                s.statPoints = stats.statPoints;
                s.talentPoints = stats.talentPoints;
                s.skillPoints = stats.skillPoints;
                s.look = stats.look.ToJson();
            }
            return JsonUtility.ToJson(s);
        }

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<SaveState>(json);
            // who they are, then the stats: they set max HP / energy
            if (stats != null) stats.LoadLook(HeroLook.FromJson(s.look));
            if (stats != null && s.level > 0) stats.SetState(s.level, s.xp, s.allocated, s.statPoints, s.talentPoints, s.skillPoints);
            motor.Teleport(new Vector2(s.x, s.y));
            puppetLastPos = transform.position;
            hasMoveTarget = false;
            attackTarget = null;
            health.hp = Mathf.Clamp(s.hp, 1f, health.maxHp);
            energy = Mathf.Clamp(s.energy, 0f, maxEnergy);
        }

        /// <summary>
        /// Level, XP and points from a saved "player" section, leaving where the hero stands and
        /// its health alone: online, the server keeps its player's character sheet up to date this way.
        /// </summary>
        public void RestoreProgress(string json)
        {
            var s = JsonUtility.FromJson<SaveState>(json);
            if (stats != null && s.look != null)
            {
                var look = HeroLook.FromJson(s.look);
                if (!look.SameAs(stats.look)) stats.LoadLook(look);
            }
            if (stats != null && s.level > 0) stats.SetState(s.level, s.xp, s.allocated, s.statPoints, s.talentPoints, s.skillPoints);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}

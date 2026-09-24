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
    /// A hero. The one this machine controls (<see cref="Players.Local"/>) turns the mouse (hold
    /// left/right button) or arrow keys, a click on an enemy (attack it) or an NPC (talk), Q W E R
    /// A S D Space (skills), 1 2 3 (potions) and F (talk) into a <see cref="PlayerIntent"/>. Every
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
        readonly List<Collider2D> clickHits = new List<Collider2D>();

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
            SaveRegistry.Register(this);
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
            if (stats == null) return ProgressionConfig.Current.PhysicalAttack(ProgressionConfig.Current.startStrength);
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

        void ApplyBuffs()
        {
            float taken = 1f;
            bool immune = false;
            foreach (var b in buffs)
            {
                if (b.spec == null) continue;
                taken *= b.spec.damageTakenMultiplier;
                immune |= b.spec.stunImmune;
            }
            health.damageTakenMultiplier = taken;
            if (status != null) status.stunImmune = immune;
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
            speedMul *= BuffSpeed;
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
            // auto basic attack on a clicked enemy
            if (attackTarget != null)
            {
                if (attackTarget.IsDead || !attackTarget.gameObject.activeInHierarchy)
                {
                    attackTarget = null;
                }
                else if (Vector2.Distance(transform.position, attackTarget.transform.position) <= basicAttackRange && !IsActing)
                {
                    i.basicAttack = true;
                    i.basicAttackAt = attackTarget.transform.position;
                }
            }
            if (InputReader.Interact) i.talkTo = NPC.Nearest(transform.position, interactRadius);
            if (npcTarget != null && Vector2.Distance(transform.position, npcTarget.transform.position) <= interactRadius)
            {
                i.talkTo = npcTarget;
                npcTarget = null;
                hasMoveTarget = false;
            }
            i.move = ComputeMove(ref i);
            return i;
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
            if ((InputReader.LeftPressed || InputReader.RightPressed) && !overUI)
            {
                Vector2 m = InputReader.MouseWorld;
                attackTarget = null;
                npcTarget = null;
                if (InputReader.LeftPressed)
                {
                    clickHits.Clear();
                    var filter = new ContactFilter2D();
                    filter.SetLayerMask(Layers.EnemyMask | Layers.NPCMask | Layers.ObstacleMask);
                    filter.useTriggers = true;
                    Physics2D.OverlapCircle(m, 0.45f, filter, clickHits);
                    foreach (var c in clickHits)
                    {
                        var npc = c.GetComponentInParent<NPC>();
                        if (npc != null) { npcTarget = npc; break; }
                        var h = c.GetComponentInParent<Health>();
                        if (h != null && !h.IsDead && h.CanBeDamagedBy(Team.Player)) { attackTarget = h; break; }
                    }
                }
                moveTarget = m;
                hasMoveTarget = true;
                if (npcTarget == null && attackTarget == null) VFX.Spawn("click_marker", m, Quaternion.identity);
            }
            if ((InputReader.LeftHeld || InputReader.MoveHeld) && !overUI && attackTarget == null && npcTarget == null)
            {
                moveTarget = InputReader.MouseWorld;
                hasMoveTarget = true;
            }

            if (attackTarget != null)
            {
                Vector2 to = (Vector2)attackTarget.transform.position - (Vector2)transform.position;
                if (to.magnitude > basicAttackRange * 0.85f) return to.normalized;
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
        }

        void OnDied(DamageInfo d)
        {
            motor.HardStop();
            hasMoveTarget = false;
            attackTarget = null;
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
            }
            return JsonUtility.ToJson(s);
        }

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<SaveState>(json);
            // stats first: they set max HP / energy
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
            if (stats != null && s.level > 0) stats.SetState(s.level, s.xp, s.allocated, s.statPoints, s.talentPoints, s.skillPoints);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}

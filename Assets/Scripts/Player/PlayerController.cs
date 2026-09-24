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
    /// The hero. Mouse (hold left/right button) or arrow keys to move, click an enemy to
    /// attack it, Q W E R A S D Space for skills, 1 2 3 potions, F to talk.
    /// </summary>
    public class PlayerController : MonoBehaviour, ISaveable, IAbilityCaster
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
        public readonly List<Buff> buffs = new List<Buff>();
        public float PotionReadyIn => Mathf.Max(0, potionReadyAt - Time.time);

        float actionUntil;
        float actionMoveMul = 1f;
        Vector2 actionDir;
        string actionAnim;
        Vector2 moveTarget;
        bool hasMoveTarget;
        Health attackTarget;
        NPC npcTarget;
        float potionReadyAt;
        float hurtAnimUntil;
        float regenFraction;
        readonly List<Collider2D> clickHits = new List<Collider2D>();

        void Awake()
        {
            if (motor == null) motor = GetComponent<CharacterMotor>();
            if (health == null) health = GetComponent<Health>();
            if (status == null) status = GetComponent<StatusEffects>();
            if (skills == null) skills = GetComponent<PlayerSkills>();
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (stats == null) stats = gameObject.AddComponent<PlayerStats>();   // prefabs made before stats existed
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            if (anim != null) anim.FrameChanged += OnFrame;
            skills.BufferedCast += OnKeySkillCast;
            SaveRegistry.Register(this);
        }

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

        // ------------------------------------------------------------------ buffs
        /// <summary>Adds or refreshes a buff; its speed, damage-taken and stun-immunity apply while it lasts.</summary>
        public void AddBuff(BuffSpec spec)
        {
            if (spec == null) return;
            var b = buffs.Find(x => x.id == spec.id);
            if (b == null)
            {
                b = new Buff { id = spec.id };
                buffs.Add(b);
                if (!string.IsNullOrEmpty(spec.attachedVfx))
                    b.vfx = VFX.Spawn(spec.attachedVfx, transform.position, Quaternion.identity, 1f, transform, true);
            }
            b.spec = spec;
            b.name = spec.displayName;
            b.icon = spec.icon;
            b.duration = spec.duration;
            b.until = Time.time + spec.duration;
            ApplyBuffs();
        }

        public void RemoveBuff(string id)
        {
            var b = buffs.Find(x => x.id == id);
            if (b == null) return;
            buffs.Remove(b);
            EndBuff(b, true);
            ApplyBuffs();
        }

        public bool HasBuff(string id) => buffs.Exists(b => b.id == id && b.Remaining > 0);

        void EndBuff(Buff b, bool showEnd)
        {
            if (b.vfx != null) VFX.Release(b.vfx);
            b.vfx = null;
            if (showEnd && !IsDead && b.spec != null && !string.IsNullOrEmpty(b.spec.endVfx))
                VFX.Spawn(b.spec.endVfx, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        }

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

        void Update()
        {
            UpdateBuffs();
            if (IsDead) return;
            Regen();

            var gm = GameManager.I;
            bool playing = gm == null || gm.State == GameState.Playing;
            if (!playing)
            {
                motor.Stop();
                hasMoveTarget = false;
                UpdateAnimation(Vector2.zero);
                return;
            }
            if (status != null && status.IsStunned)
            {
                motor.Stop();
                hasMoveTarget = false;
                if (anim != null) anim.PlayDir("hurt", motor.Facing);
                return;
            }

            HandlePotions();
            HandleSkills();
            HandleInteract();
            Vector2 move = ComputeMove();
            float speedMul = (status != null ? status.SpeedMultiplier : 1f) * (IsActing ? actionMoveMul : 1f);
            speedMul *= BuffSpeed;
            motor.Move(move, speedMul);
            if (IsActing) motor.Facing = actionDir;
            UpdateAnimation(move);
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
            ScreenFX.SetDanger(health.Fraction < 0.3f ? 1f - health.Fraction / 0.3f : 0f);
        }

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

        // ------------------------------------------------------------------ input
        void HandleSkills()
        {
            for (int i = 0; i < 8; i++)
            {
                if (!InputReader.SkillPressed(i)) continue;
                if (skills.Request(i, InputReader.MouseWorld)) OnKeySkillCast(i);
            }
            // auto basic attack on a clicked enemy
            if (attackTarget != null)
            {
                if (attackTarget.IsDead || !attackTarget.gameObject.activeInHierarchy)
                {
                    attackTarget = null;
                }
                else
                {
                    float d = Vector2.Distance(transform.position, attackTarget.transform.position);
                    if (d <= basicAttackRange && !IsActing)
                        skills.TryCast(0, attackTarget.transform.position);
                }
            }
        }

        /// <summary>A key-pressed skill fired (now or from the input buffer): stop walking / auto-attacking.</summary>
        void OnKeySkillCast(int slot)
        {
            hasMoveTarget = false;
            attackTarget = null;
        }

        void HandlePotions()
        {
            for (int i = 0; i < 3; i++)
                if (InputReader.PotionPressed(i)) UsePotion(i);
        }

        public bool UsePotion(int slot)
        {
            if (slot < 0 || slot >= potionIds.Length || Time.time < potionReadyAt) return false;
            var db = GameManager.I != null ? GameManager.I.db : null;
            var item = db != null ? db.Item(potionIds[slot]) : null;
            if (item == null || Inventory.I == null || Inventory.I.Count(item) <= 0)
            {
                GameEvents.RaiseWorldText("Hết bình!", health.HeadPosition + Vector3.up * 0.4f, new Color(0.8f, 0.8f, 0.8f));
                AudioManager.Play("sfx_denied", 0.5f);
                return false;
            }
            Inventory.I.Remove(item, 1);
            potionReadyAt = Time.time + potionCooldown;
            if (item.healAmount > 0) health.Heal(item.healAmount);
            if (item.energyAmount > 0)
            {
                float before = energy;
                energy = Mathf.Min(maxEnergy, energy + item.energyAmount);
                GameEvents.RaiseWorldText($"+{Mathf.RoundToInt(energy - before)}", health.HeadPosition + Vector3.right * 0.4f, Palette.Energy);
            }
            if (item.cleanse && status != null) status.Cleanse();
            string fx = slot == 0 ? "potion_red" : slot == 1 ? "potion_blue" : "potion_green";
            VFX.Spawn(fx, transform.position, Quaternion.identity, 1f, transform);
            AudioManager.Play("sfx_potion", 0.8f);
            return true;
        }

        void HandleInteract()
        {
            if (InputReader.Interact)
            {
                var npc = NPC.Nearest(transform.position, interactRadius);
                if (npc != null) npc.Interact(this);
            }
            if (npcTarget != null && Vector2.Distance(transform.position, npcTarget.transform.position) <= interactRadius)
            {
                var n = npcTarget;
                npcTarget = null;
                hasMoveTarget = false;
                n.Interact(this);
            }
        }

        Vector2 ComputeMove()
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
                motor.Facing = to.normalized;
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
                AudioManager.Play("sfx_step", 0.25f, 0.15f, null, 0.1f);
                if (Random.value < 0.6f) VFX.Spawn("step_dust", transform.position + new Vector3(Random.Range(-0.1f, 0.1f), 0.05f), Quaternion.identity);
            }
        }

        // ------------------------------------------------------------------ damage
        void OnDamaged(DamageInfo d, float amount)
        {
            if (flash != null) flash.Flash(new Color(1f, 0.3f, 0.3f), 0.9f, 0.18f);
            if (amount >= 12f) hurtAnimUntil = Time.time + 0.18f;
            CameraRig.Shake(Mathf.Clamp(amount / 60f, 0.1f, 0.45f));
            ScreenFX.Flash(new Color(0.9f, 0.1f, 0.1f), Mathf.Clamp(amount / 80f, 0.12f, 0.35f), 0.3f);
            AudioManager.Play("sfx_player_hurt", 0.8f);
            var m = GetComponent<CharacterMotor>();
            if (m != null && d.knockback > 0) m.AddKnockback(d.direction * d.knockback);
        }

        void OnDied(DamageInfo d)
        {
            motor.HardStop();
            hasMoveTarget = false;
            attackTarget = null;
            ClearBuffs();
            if (anim != null) anim.Play("dead", true);
            AudioManager.Play("sfx_player_die");
            TimeFX.SlowMo(0.3f, 1.2f);
            if (GameManager.I != null) GameManager.I.OnPlayerDied();
        }

        public void Respawn(Vector2 at)
        {
            motor.Teleport(at);
            health.ResetHealth();
            energy = maxEnergy;
            if (status != null) status.Cleanse();
            skills.ResetCooldowns();
            health.invulnerable = false;
            if (anim != null) anim.Play("idle_down", true);
            VFX.Spawn("respawn", transform.position, Quaternion.identity);
        }

        // ------------------------------------------------------------------ save
        [System.Serializable]
        class SaveState
        {
            public float x, y, hp, energy;
            public int level, xp, statPoints, talentPoints, skillPoints;
            public int[] allocated;
        }

        public string SaveKey => "player";

        public string CaptureState()
        {
            var s = new SaveState { x = transform.position.x, y = transform.position.y, hp = health.hp, energy = energy };
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
            hasMoveTarget = false;
            attackTarget = null;
            health.hp = Mathf.Clamp(s.hp, 1f, health.maxHp);
            energy = Mathf.Clamp(s.energy, 0f, maxEnergy);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}

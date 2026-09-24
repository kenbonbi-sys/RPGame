using System;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Level, XP and the four attributes of the hero (plan §05). Owns the StatBlock that
    /// equipment, talents and buffs will add modifiers to, and pushes the derived numbers
    /// (max HP, max energy, armor, resist) into Health and PlayerController. Each hero has
    /// their own; the HUD shows <see cref="Players.Local"/>'s.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerStats : MonoBehaviour
    {
        [Tooltip("Leave empty to use GameDatabase.progression.")]
        public ProgressionConfig config;

        public int level = 1;
        public int xp;
        [Tooltip("Points spent per attribute: Strength, Intelligence, Agility, Vitality.")]
        public int[] allocated = new int[4];
        public int statPoints;
        public int talentPoints;
        public int skillPoints;

        public readonly StatBlock Stats = new StatBlock();

        /// <summary>Level, XP, points or any stat changed.</summary>
        public event Action Changed;
        /// <summary>This hero reached a new level (their quests unlock).</summary>
        public event Action<int> LevelledUp;

        public ProgressionConfig Config => config != null ? config : ProgressionConfig.Current;
        public int XpToNext => Config.XpToNext(level);
        public bool IsMaxLevel => level >= Config.maxLevel;

        PlayerController pc;

        void Awake()
        {
            pc = GetComponent<PlayerController>();
            if (allocated == null || allocated.Length != 4) allocated = new int[4];
            Stats.Changed += OnStatsChanged;
            Recalculate();
            FillUp();
        }

        bool IsLocal => pc == null || pc.IsLocal;

        void OnEnable()
        {
            GameEvents.EnemyKilled += OnEnemyKilled;
        }

        void OnDisable()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
        }

        // ------------------------------------------------------------------ attributes
        public int Attribute(CoreStat a) => Mathf.RoundToInt(Stats.Get((StatId)(int)a));

        public int Allocated(CoreStat a) => allocated[(int)a];

        public bool Spend(CoreStat a)
        {
            if (statPoints <= 0) return false;
            statPoints--;
            allocated[(int)a]++;
            Recalculate();
            if (IsLocal) AudioManager.Play("sfx_ui_click", 0.7f);
            return true;
        }

        /// <summary>Rebuilds every derived stat from level + attributes (+ modifiers).</summary>
        public void Recalculate()
        {
            var c = Config;
            foreach (CoreStat a in Enum.GetValues(typeof(CoreStat)))
                Stats.SetBase((StatId)(int)a, c.StartValue(a) + allocated[(int)a], false);
            float str = Stats.Get(StatId.Strength);
            float intel = Stats.Get(StatId.Intelligence);
            float agi = Stats.Get(StatId.Agility);
            float vit = Stats.Get(StatId.Vitality);
            Stats.SetBase(StatId.MaxHp, c.MaxHp(level, vit), false);
            Stats.SetBase(StatId.MaxEnergy, c.MaxEnergy(level, intel), false);
            Stats.SetBase(StatId.PhysicalAttack, c.PhysicalAttack(str), false);
            Stats.SetBase(StatId.MagicAttack, c.MagicAttack(intel), false);
            Stats.SetBase(StatId.CritChance, c.critPerAgility * agi, false);
            Stats.SetBase(StatId.CritDamage, c.critMultiplier, false);
            Stats.SetBase(StatId.AttackSpeed, c.attackSpeedPerAgility * agi, false);
            Stats.SetBase(StatId.DashCooldownReduction, c.dashCooldownPerAgility * agi, false);
            Stats.SetBase(StatId.Armor, c.armorPerVitality * vit, false);
            Stats.SetBase(StatId.ElementalResist, c.resistPerVitality * vit, false);
            Stats.SetBase(StatId.PoiseDamage, 1f + c.poisePerStrength * str, false);
            Stats.SetBase(StatId.DamageDealt, 1f, false);
            Apply();
            Changed?.Invoke();
        }

        void OnStatsChanged() => Recalculate();

        /// <summary>Pushes the derived values into the components that use them.</summary>
        void Apply()
        {
            var h = pc != null ? pc.health : GetComponent<Health>();
            if (h != null)
            {
                float max = Mathf.Round(Stats.Get(StatId.MaxHp));
                float frac = h.maxHp > 0 ? h.hp / h.maxHp : 1f;
                h.maxHp = max;
                h.hp = Mathf.Min(max, Mathf.Round(max * frac));
                h.level = level;
                h.armor = Stats.Get(StatId.Armor);
                h.elementalResist = Mathf.Min(Config.resistMax, Stats.Get(StatId.ElementalResist));
            }
            if (pc != null)
            {
                pc.maxEnergy = Mathf.Round(Stats.Get(StatId.MaxEnergy));
                pc.energy = Mathf.Min(pc.energy, pc.maxEnergy);
            }
        }

        void FillUp()
        {
            if (pc == null) return;
            if (pc.health != null) pc.health.hp = pc.health.maxHp;
            pc.energy = pc.maxEnergy;
        }

        // ------------------------------------------------------------------ combat numbers
        /// <summary>Multiplier for the hero's outgoing damage of a type (physical uses Strength, the rest Intelligence).</summary>
        public float DamageScale(DamageType type)
        {
            bool magic = type != DamageType.Physical;
            return Config.DamageScale(Stats.Get(magic ? StatId.MagicAttack : StatId.PhysicalAttack), magic);
        }

        /// <summary>Outgoing damage multiplier for an ability: untagged bonuses plus those for its tags (#Lửa, #Đạn…).</summary>
        public float DamageDealt(AbilityDef ability) => Stats.Get(StatId.DamageDealt, ability != null ? ability.TagList : null);

        /// <summary>A skill's crit chance plus the Agility bonus, capped.</summary>
        public float CritChance(float skillChance) => Mathf.Min(Config.critMax, skillChance + Stats.Get(StatId.CritChance));

        public float CritMultiplier => Stats.Get(StatId.CritDamage);

        /// <summary>Cooldown multiplier for a slot: the basic attack (Q) speeds up with Agility, and so does Lướt.</summary>
        public float CooldownMultiplier(int slot, AbilityDef ability)
        {
            if (ability != null && ability.HasTag(AbilityTags.Movement)) return Mathf.Max(0.5f, 1f - Stats.Get(StatId.DashCooldownReduction));
            if (slot == 0) return 1f / (1f + Stats.Get(StatId.AttackSpeed));
            return 1f;
        }

        public float PoiseMultiplier => Stats.Get(StatId.PoiseDamage);

        // ------------------------------------------------------------------ XP
        /// <summary>Every hero who shares a kill gets its full XP (PvE: helping never costs XP).</summary>
        void OnEnemyKilled(KillInfo k)
        {
            if (!k.Credits(pc)) return;
            AddXp(Config.KillXp(k.level, k.rank, level), k.position);
        }

        /// <summary>Adds XP, levelling up as many times as it covers. Shows "+N XP" at <paramref name="at"/> when given.</summary>
        public void AddXp(int amount, Vector3? at = null)
        {
            if (amount <= 0 || IsMaxLevel) return;
            xp += amount;
            if (at.HasValue && IsLocal) GameEvents.RaiseWorldText($"+{amount} XP", at.Value + Vector3.up * 1.4f, Palette.Xp);
            int gained = 0;
            while (!IsMaxLevel && xp >= XpToNext)
            {
                xp -= XpToNext;
                level++;
                gained++;
                statPoints += Config.statPointsPerLevel;
                talentPoints += Config.talentPointsPerLevel;
                skillPoints += Config.skillPointsPerLevel;
            }
            if (IsMaxLevel) xp = 0;
            if (gained > 0) OnLevelUp();
            else Changed?.Invoke();
        }

        void OnLevelUp()
        {
            Recalculate();
            FillUp();
            LevelledUp?.Invoke(level);
            VFX.Spawn("quest_complete", transform.position, Quaternion.identity, 1.2f, transform);
            if (pc != null && pc.health != null)
                GameEvents.RaiseWorldText($"Cấp {level}!", pc.health.HeadPosition + Vector3.up * 0.5f, Palette.Xp);
            if (!IsLocal) return;   // the rest is this screen's HUD
            GameEvents.RaiseLevelUp(level);
            GameEvents.RaiseLog($"Lên cấp {level}! Nhận {Config.statPointsPerLevel} điểm chỉ số — bấm C để phân bổ.", Palette.Xp);
            GameEvents.RaiseBanner(BannerKind.Quest, "Lên cấp!", $"Cấp {level}");
            AudioManager.Play("sfx_levelup", 1f, 0f);
        }

        /// <summary>Sets level/XP/points directly (loading a save). Does not fill HP/energy.</summary>
        public void SetState(int newLevel, int newXp, int[] newAllocated, int stat, int talent, int skill)
        {
            level = Mathf.Clamp(newLevel, 1, Config.maxLevel);
            xp = Mathf.Max(0, newXp);
            allocated = newAllocated != null && newAllocated.Length == 4 ? (int[])newAllocated.Clone() : new int[4];
            statPoints = stat;
            talentPoints = talent;
            skillPoints = skill;
            Recalculate();
        }
    }
}

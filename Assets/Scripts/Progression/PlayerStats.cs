using System;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Level, XP and the six D&D ability scores of the hero (plan §05). Who the hero is (their
    /// people, class and weapon: <see cref="look"/>) sets the starting scores — the standard
    /// array placed by the class plus the people's increases — and the rules around them: the
    /// class's hit die, armor and casting ability, the weapon's attack, the people's traits.
    /// Owns the StatBlock that equipment, talents and buffs add modifiers to, and pushes the
    /// derived numbers into Health, StatusEffects and PlayerController. Each hero has their own;
    /// the HUD shows <see cref="Players.Local"/>'s.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerStats : MonoBehaviour
    {
        [Tooltip("Leave empty to use GameDatabase.progression.")]
        public ProgressionConfig config;

        public int level = 1;
        public int xp;
        [Tooltip("Points spent per ability score, by CoreStat: STR, INT, DEX, CON, WIS, CHA.")]
        public int[] allocated = new int[CoreStats.Count];
        public int statPoints;
        public int talentPoints;
        public int skillPoints;
        [Tooltip("Their people, class, weapon and looks. An empty class: an older character who has not chosen one yet.")]
        public HeroLook look = new HeroLook();

        public readonly StatBlock Stats = new StatBlock();

        /// <summary>Level, XP, points or any stat changed.</summary>
        public event Action Changed;
        /// <summary>This hero reached a new level (their quests unlock).</summary>
        public event Action<int> LevelledUp;
        /// <summary>Their people, class, weapon or looks changed (the skill bar and the sprite follow).</summary>
        public event Action LookChanged;

        public ProgressionConfig Config => config != null ? config : ProgressionConfig.Current;
        public int XpToNext => Config.XpToNext(level);
        public bool IsMaxLevel => level >= Config.maxLevel;

        static GameDatabase Db => GameManager.I != null ? GameManager.I.db : null;
        public RaceDef Race => Db != null ? Db.Race(look.race) : null;
        public ClassDef Class => Db != null ? Db.Class(look.cls) : null;
        public bool HasClass => Class != null;
        /// <summary>The weapon in hand: the chosen one if the class may carry it, else the class's first.</summary>
        public WeaponKind Weapon
        {
            get
            {
                var cls = Class;
                var w = WeaponKinds.Get(look.weapon);
                if (w != null && (cls == null || cls.Allows(w.id))) return w;
                return WeaponKinds.Get(cls != null ? cls.DefaultWeapon : "sword");
            }
        }

        PlayerController pc;
        Resistances baseResist;
        float endureReadyAt;

        void Awake()
        {
            pc = GetComponent<PlayerController>();
            FixAllocated();
            var h = pc != null ? pc.health : GetComponent<Health>();
            if (h != null) baseResist = h.resistances;
            Stats.Changed += OnStatsChanged;
            var bag = pc != null ? pc.inventory : GetComponent<Inventory>();
            if (bag != null)
            {
                gearBag = bag;
                bag.Changed += WearGear;
            }
            WearGear();
            Recalculate();
            FillUp();
        }

        void OnDestroy()
        {
            if (gearBag != null) gearBag.Changed -= WearGear;
        }

        // ------------------------------------------------------------------ gear
        Inventory gearBag;
        readonly ItemDef[] wornNow = new ItemDef[Gear.SlotCount];
        readonly System.Collections.Generic.List<StatModifier> gearMods = new System.Collections.Generic.List<StatModifier>();

        /// <summary>The worn gear's bonuses as modifiers of <see cref="Stats"/> (only when what is worn changed).</summary>
        void WearGear()
        {
            if (gearBag == null) return;
            bool same = true;
            for (int i = 0; i < wornNow.Length; i++)
                if (wornNow[i] != gearBag.equipped[i])
                {
                    same = false;
                    wornNow[i] = gearBag.equipped[i];
                }
            if (same && gearMods.Count > 0) return;
            Gear.Collect(wornNow, gearBag, gearMods);
            Stats.ReplaceFrom(gearBag, gearMods);
        }

        void FixAllocated()
        {
            if (allocated != null && allocated.Length == CoreStats.Count) return;
            var grown = new int[CoreStats.Count];
            if (allocated != null) Array.Copy(allocated, grown, Mathf.Min(allocated.Length, grown.Length));
            allocated = grown;
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

        // ------------------------------------------------------------------ ability scores
        /// <summary>An ability score as it stands (base, points spent, items and buffs).</summary>
        public float Score(CoreStat a) => Stats.Get(CoreStats.Stat(a));

        public int Attribute(CoreStat a) => Mathf.RoundToInt(Score(a));

        public int Allocated(CoreStat a) => allocated[(int)a];

        /// <summary>The score before points: the class's place in the standard array plus the people's increase.</summary>
        public int BaseScore(CoreStat a)
        {
            var cls = Class;
            if (cls == null) return CoreStats.Unclassed[(int)a];
            var race = Race;
            return cls.ArrayScore(a) + (race != null ? race.Bonus(a) : 0);
        }

        /// <summary>The ability the weapon in hand strikes with (D&D: Sức Mạnh; finesse the better of it and Khéo Léo; a bow Khéo Léo; a focus the casting ability).</summary>
        public float AttackScore
        {
            get
            {
                var w = Weapon;
                if (w != null && w.focus) return CastingScore;
                if (w != null && w.ranged) return Score(CoreStat.Dexterity);
                if (w != null && w.finesse) return Mathf.Max(Score(CoreStat.Strength), Score(CoreStat.Dexterity));
                return Score(CoreStat.Strength);
            }
        }

        /// <summary>The ability the class casts with (a martial class: the one it fights with).</summary>
        public float CastingScore
        {
            get
            {
                var cls = Class;
                if (cls == null) return Mathf.Max(Score(CoreStat.Intelligence), Score(CoreStat.Strength));
                return Score(cls.casting);
            }
        }

        /// <summary>Puts a stat point into an ability. Online a player's machine asks the server, which sends the new sheet back.</summary>
        public bool Spend(CoreStat a)
        {
            if (statPoints <= 0) return false;
            if (IsLocal) AudioManager.Play("sfx_ui_click", 0.7f);
            if (!GameSession.IsAuthority)
            {
                OnlineSession.Ask(new ActRequest { kind = ActKind.SpendStat, value = (int)a });
                return true;
            }
            statPoints--;
            allocated[(int)a]++;
            Recalculate();
            return true;
        }

        // ------------------------------------------------------------------ who they are
        /// <summary>
        /// Takes a people, class, weapon and looks (the character creator, a save, the server).
        /// Choosing a class for the first time — or another one — gives back every point spent,
        /// to be spent again on the new sheet.
        /// </summary>
        public void SetLook(HeroLook newLook)
        {
            if (newLook == null) return;
            bool classChanged = newLook.cls != look.cls || newLook.race != look.race;
            look = newLook.Clone();
            if (classChanged) Refund();
            Recalculate();
            LookChanged?.Invoke();
        }

        /// <summary>Takes who the hero is from a save or the server, points untouched (they come with it).</summary>
        public void LoadLook(HeroLook saved)
        {
            look = saved != null ? saved.Clone() : new HeroLook();
            Recalculate();
            LookChanged?.Invoke();
        }

        void Refund()
        {
            FixAllocated();
            for (int i = 0; i < allocated.Length; i++)
            {
                statPoints += allocated[i];
                allocated[i] = 0;
            }
        }

        /// <summary>Walking speed from the hero's people (D&D: 25 ft against 30).</summary>
        public float SpeedMultiplier
        {
            get
            {
                var r = Race;
                return r != null ? Mathf.Max(0.5f, r.speedMultiplier) : 1f;
            }
        }

        public bool Darkvision
        {
            get
            {
                var r = Race;
                return r != null && r.darkvision;
            }
        }

        // ------------------------------------------------------------------ derived numbers
        /// <summary>Rebuilds every derived stat from level, scores, class, people and weapon (+ modifiers).</summary>
        public void Recalculate()
        {
            FixAllocated();
            var c = Config;
            foreach (CoreStat a in Enum.GetValues(typeof(CoreStat)))
                Stats.SetBase(CoreStats.Stat(a), BaseScore(a) + allocated[(int)a], false);
            var cls = Class;
            var race = Race;
            var weapon = Weapon;
            float str = Score(CoreStat.Strength);
            float dex = Score(CoreStat.Dexterity);
            float con = Score(CoreStat.Constitution);
            float intel = Score(CoreStat.Intelligence);
            float wis = Score(CoreStat.Wisdom);
            float cha = Score(CoreStat.Charisma);
            float hitDie = cls != null ? cls.HitDieAverage : 4.5f;
            float weaponAtk = WeaponKinds.AttackOf(weapon, look.upgrade);
            float hp = c.MaxHp(level, hitDie, con) + (race != null ? race.hpPerLevel * level : 0f);
            Stats.SetBase(StatId.MaxHp, hp, false);
            Stats.SetBase(StatId.MaxEnergy, c.MaxEnergy(level, CastingScore), false);
            Stats.SetBase(StatId.PhysicalAttack, c.Attack(AttackScore, weaponAtk), false);
            Stats.SetBase(StatId.MagicAttack, c.Attack(CastingScore, weaponAtk), false);
            Stats.SetBase(StatId.CritChance, c.critPerDexterity * (dex - 10f), false);
            Stats.SetBase(StatId.CritDamage, c.critMultiplier + (race != null ? race.savageCrit : 0f), false);
            Stats.SetBase(StatId.AttackSpeed, c.attackSpeedPerDexterity * (dex - 10f), false);
            Stats.SetBase(StatId.DashCooldownReduction, c.dashCooldownPerDexterity * (dex - 10f), false);
            float baseArmor = cls != null ? cls.BaseArmor(this) : 2f;
            Stats.SetBase(StatId.Armor, Mathf.Max(0f, baseArmor + c.armorPerConstitution * (con - 10f)), false);
            Stats.SetBase(StatId.ElementalResist, Mathf.Max(0f, c.resistPerWisdom * (wis - 10f)), false);
            Stats.SetBase(StatId.PoiseDamage, 1f + c.poisePerStrength * (str - 10f), false);
            Stats.SetBase(StatId.DamageDealt, 1f, false);
            Stats.SetBase(StatId.CooldownReduction, Mathf.Clamp(c.cooldownPerIntelligence * (intel - 10f), 0f, c.cooldownReductionMax), false);
            Stats.SetBase(StatId.HealingPower, Mathf.Max(0.5f, 1f + c.healingPerWisdom * (wis - 10f)), false);
            Stats.SetBase(StatId.GoldFind, Mathf.Max(0f, c.goldPerCharisma * (cha - 10f)), false);
            Apply();
            Changed?.Invoke();
        }

        void OnStatsChanged() => Recalculate();

        /// <summary>Pushes the derived values and the people's traits into the components that use them.</summary>
        void Apply()
        {
            var race = Race;
            var cls = Class;
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
                var res = baseResist;
                if (race != null)
                {
                    foreach (DamageType t in Enum.GetValues(typeof(DamageType))) res[t] = res[t] + race.resist[t];
                    if (race.draconic) res[look.DraconicElement] = res[look.DraconicElement] + 0.5f;
                }
                h.resistances = res;
                float lucky = race != null ? race.luckyDodge : 0f;
                h.Evade = lucky > 0f ? (Func<DamageInfo, bool>)(d => UnityEngine.Random.value < lucky) : null;
                float relentless = race != null ? race.relentlessCooldown : 0f;
                h.Endure = relentless > 0f ? (Func<bool>)(() =>
                {
                    if (Time.time < endureReadyAt) return false;
                    endureReadyAt = Time.time + relentless;
                    return true;
                }) : null;
            }
            var st = pc != null ? pc.status : GetComponent<StatusEffects>();
            if (st != null)
            {
                // the people's traits, and D&D's saving throws: Thông Thái stands firm, Thể Chất shrugs off poison
                float control = race != null ? race.controlShorter : 0f;
                float poison = race != null ? race.poisonShorter : 0f;
                if (cls != null && cls.Saves(CoreStat.Wisdom)) control += 0.2f;
                if (cls != null && cls.Saves(CoreStat.Constitution)) poison += 0.25f;
                st.controlShorter = Mathf.Min(0.6f, control);
                st.poisonShorter = Mathf.Min(0.75f, poison);
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
        /// <summary>Multiplier for the hero's outgoing damage of a type (physical: the weapon's ability; the rest: the casting ability).</summary>
        public float DamageScale(DamageType type)
        {
            bool magic = type != DamageType.Physical;
            return Config.DamageScale(Stats.Get(magic ? StatId.MagicAttack : StatId.PhysicalAttack), magic);
        }

        /// <summary>Outgoing damage multiplier for an ability: untagged bonuses plus those for its tags (#Lửa, #Đạn…).</summary>
        public float DamageDealt(AbilityDef ability) => Stats.Get(StatId.DamageDealt, ability != null ? ability.TagList : null);

        /// <summary>A skill's crit chance plus the Khéo Léo bonus, capped.</summary>
        public float CritChance(float skillChance) => Mathf.Min(Config.critMax, skillChance + Stats.Get(StatId.CritChance));

        public float CritMultiplier => Stats.Get(StatId.CritDamage);

        /// <summary>Healing done multiplier (Thông Thái).</summary>
        public float HealingPower => Stats.Get(StatId.HealingPower);

        /// <summary>Extra share of gold found (Sức Hút).</summary>
        public float GoldFind => Stats.Get(StatId.GoldFind);

        /// <summary>Cooldown multiplier for a slot: the basic attack (Q) speeds up with Khéo Léo, and so does Lướt; the rest shortens with Trí Tuệ.</summary>
        public float CooldownMultiplier(int slot, AbilityDef ability)
        {
            if (ability != null && ability.HasTag(AbilityTags.Movement)) return Mathf.Max(0.5f, 1f - Stats.Get(StatId.DashCooldownReduction));
            if (slot == 0) return 1f / Mathf.Max(0.5f, 1f + Stats.Get(StatId.AttackSpeed));
            return 1f - Stats.Get(StatId.CooldownReduction);
        }

        public float PoiseMultiplier => Stats.Get(StatId.PoiseDamage);

        // ------------------------------------------------------------------ XP
        /// <summary>Every hero who shares a kill gets its full XP (PvE: helping never costs XP); Con Người learn a little faster.</summary>
        void OnEnemyKilled(KillInfo k)
        {
            if (!GameSession.IsAuthority || !k.Credits(pc)) return;
            var race = Race;
            float bonus = race != null ? race.xpBonus : 0f;
            AddXp(Mathf.RoundToInt(Config.KillXp(k.level, k.rank, level) * (1f + bonus)), k.position);
        }

        /// <summary>Adds XP, levelling up as many times as it covers. Shows "+N XP" at <paramref name="at"/> when given.</summary>
        public void AddXp(int amount, Vector3? at = null)
        {
            // XP is the world's rule: online a player's copy gets its level from the server
            if (amount <= 0 || IsMaxLevel || !GameSession.IsAuthority) return;
            xp += amount;
            if (at.HasValue) Notify.WorldText(pc, $"+{amount} XP", at.Value + Vector3.up * 1.4f, Palette.Xp);
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
            // everyone sees the glow and "Cấp N!"; the rest is its player's HUD
            NetCues.VfxOn("quest_complete", this, 1.2f);
            if (pc != null && pc.health != null)
                NetCues.WorldText($"Cấp {level}!", pc.health.HeadPosition + Vector3.up * 0.5f, Palette.Xp);
            Notify.LevelUp(pc, level);
            Notify.Log(pc, $"Lên cấp {level}! Nhận {Config.statPointsPerLevel} điểm chỉ số — bấm C để phân bổ.", Palette.Xp);
            Notify.Banner(pc, BannerKind.Quest, "Lên cấp!", $"Cấp {level}");
            Notify.Sound(pc, "sfx_levelup", 1f, 0f);
        }

        /// <summary>Sets level/XP/points directly (loading a save). Does not fill HP/energy.</summary>
        public void SetState(int newLevel, int newXp, int[] newAllocated, int stat, int talent, int skill)
        {
            level = Mathf.Clamp(newLevel, 1, Config.maxLevel);
            xp = Mathf.Max(0, newXp);
            allocated = newAllocated != null ? (int[])newAllocated.Clone() : new int[CoreStats.Count];
            FixAllocated();
            statPoints = stat;
            talentPoints = talent;
            skillPoints = skill;
            Recalculate();
        }
    }
}

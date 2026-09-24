using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    public enum StatId
    {
        // the four attributes the player spends points on
        Strength,
        Intelligence,
        Agility,
        Vitality,
        // derived
        MaxHp,
        MaxEnergy,
        Armor,
        PhysicalAttack,
        MagicAttack,
        CritChance,
        CritDamage,
        AttackSpeed,
        DashCooldownReduction,
        ElementalResist,
        PoiseDamage,
        /// <summary>Outgoing damage multiplier, base 1: the "(1 + Tăng%)" of plan §04. "+20% #Đạn" = PercentAdd 0.2 tagged #Đạn.</summary>
        DamageDealt
    }

    public enum ModKind
    {
        /// <summary>Added to the base value.</summary>
        Flat,
        /// <summary>Summed with other PercentAdd modifiers, then applied once: ×(1 + Σ).</summary>
        PercentAdd,
        /// <summary>Applied on its own: ×(1 + value) each.</summary>
        PercentMult
    }

    /// <summary>One change to a stat, from a source (item, talent, buff), optionally limited to a skill tag.</summary>
    [Serializable]
    public struct StatModifier
    {
        public StatId stat;
        public ModKind kind;
        public float value;
        [Tooltip("Optional skill tag (#Lửa, #Đạn…). Tagged modifiers only count when a query asks for that tag.")]
        public string tag;
        [NonSerialized] public object source;

        public StatModifier(StatId stat, ModKind kind, float value, object source = null, string tag = null)
        {
            this.stat = stat;
            this.kind = kind;
            this.value = value;
            this.source = source;
            this.tag = tag;
        }
    }

    /// <summary>
    /// Base values + modifiers. Final = (base + ΣFlat) × (1 + ΣPercentAdd) × Π(1 + PercentMult).
    /// Plain C# so equipment, talents and buffs can all feed the same numbers.
    /// </summary>
    public class StatBlock
    {
        static readonly int Count = Enum.GetValues(typeof(StatId)).Length;

        readonly float[] baseValues = new float[Count];
        readonly List<StatModifier> mods = new List<StatModifier>();

        public event Action Changed;

        public IReadOnlyList<StatModifier> Modifiers => mods;

        public float GetBase(StatId s) => baseValues[(int)s];

        public void SetBase(StatId s, float value, bool notify = true)
        {
            baseValues[(int)s] = value;
            if (notify) Changed?.Invoke();
        }

        public void Add(StatModifier m)
        {
            mods.Add(m);
            Changed?.Invoke();
        }

        /// <summary>Removes every modifier that came from <paramref name="source"/>. Returns how many.</summary>
        public int RemoveFrom(object source)
        {
            int n = mods.RemoveAll(m => Equals(m.source, source));
            if (n > 0) Changed?.Invoke();
            return n;
        }

        /// <summary>Final value. Untagged modifiers always count; tagged ones only for a matching tag.</summary>
        public float Get(StatId s, string tag = null) => Compute(s, tag, null);

        /// <summary>Final value for something with several tags (an ability's "#Lửa #Đạn"): a tagged modifier counts when its tag is among them.</summary>
        public float Get(StatId s, IReadOnlyList<string> tags) => Compute(s, null, tags);

        float Compute(StatId s, string tag, IReadOnlyList<string> tags)
        {
            float flat = baseValues[(int)s];
            float add = 0f;
            float mult = 1f;
            foreach (var m in mods)
            {
                if (m.stat != s) continue;
                if (!string.IsNullOrEmpty(m.tag) && m.tag != tag && !Contains(tags, m.tag)) continue;
                switch (m.kind)
                {
                    case ModKind.Flat: flat += m.value; break;
                    case ModKind.PercentAdd: add += m.value; break;
                    case ModKind.PercentMult: mult *= 1f + m.value; break;
                }
            }
            return flat * (1f + add) * mult;
        }

        static bool Contains(IReadOnlyList<string> tags, string tag)
        {
            if (tags == null) return false;
            for (int i = 0; i < tags.Count; i++)
                if (tags[i] == tag) return true;
            return false;
        }
    }
}

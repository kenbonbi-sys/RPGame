using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Resistance per damage type (plan §04): 0.75 takes 75% less, −0.5 is a weakness that takes
    /// 50% more. Health adds its all-element resist for the elements and clamps the total to
    /// ProgressionConfig.resistMin..resistMax. Physical hits are reduced by armor first.
    /// </summary>
    [System.Serializable]
    public struct Resistances
    {
        [Range(-0.5f, 0.75f)] public float physical;
        [Range(-0.5f, 0.75f)] public float fire;
        [Range(-0.5f, 0.75f)] public float ice;
        [Range(-0.5f, 0.75f)] public float lightning;
        [Range(-0.5f, 0.75f)] public float poison;
        [Range(-0.5f, 0.75f)] public float holy;

        public float this[DamageType type]
        {
            get
            {
                switch (type)
                {
                    case DamageType.Fire: return fire;
                    case DamageType.Ice: return ice;
                    case DamageType.Lightning: return lightning;
                    case DamageType.Poison: return poison;
                    case DamageType.Holy: return holy;
                    default: return physical;
                }
            }
            set
            {
                switch (type)
                {
                    case DamageType.Fire: fire = value; break;
                    case DamageType.Ice: ice = value; break;
                    case DamageType.Lightning: lightning = value; break;
                    case DamageType.Poison: poison = value; break;
                    case DamageType.Holy: holy = value; break;
                    default: physical = value; break;
                }
            }
        }

        /// <summary>The most negative resistance: the weakness Bách Khoa Trùm reveals. Null when there is none.</summary>
        public DamageType? Weakness
        {
            get
            {
                DamageType? weakest = null;
                float lowest = 0f;
                foreach (DamageType t in System.Enum.GetValues(typeof(DamageType)))
                {
                    if (this[t] >= lowest) continue;
                    lowest = this[t];
                    weakest = t;
                }
                return weakest;
            }
        }
    }
}

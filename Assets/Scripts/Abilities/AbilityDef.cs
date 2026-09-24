using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RPG
{
    /// <summary>Element and form tags of plan §06; talents and gear read them ("+20% #Đạn").</summary>
    [Flags]
    public enum AbilityTags
    {
        None = 0,
        // element
        Physical = 1 << 0,
        Fire = 1 << 1,
        Ice = 1 << 2,
        Lightning = 1 << 3,
        Wind = 1 << 4,
        Dark = 1 << 5,
        Holy = 1 << 6,
        // form
        Melee = 1 << 10,
        Projectile = 1 << 11,
        Area = 1 << 12,
        Channel = 1 << 13,
        Summon = 1 << 14,
        Movement = 1 << 15,
        Support = 1 << 16,
        Ultimate = 1 << 17
    }

    public enum AbilityTargeting
    {
        /// <summary>Toward the mouse (the aim point is clamped to maxRange).</summary>
        Direction,
        /// <summary>At the mouse position (clamped to maxRange).</summary>
        Point,
        /// <summary>On the caster.</summary>
        Self
    }

    /// <summary>
    /// A skill as data (plan §06, Ability System v2): costs, cooldown and a timeline of effect
    /// blocks (Damage, Projectile, Dash, Heal, Buff, Cue, and the Line / Burst / Pulse / Combo
    /// blocks that repeat other blocks). Player and enemies use the same assets, so a boss move
    /// is also the Chiêu Quái the player can learn later. New skills need no code.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Ability")]
    public class AbilityDef : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea(2, 5)] public string description;
        public Sprite icon;
        public AbilityTags tags;

        [Header("Cost")]
        public float cooldown = 1f;
        public float energyCost;

        [Header("Casting")]
        public AbilityTargeting targeting = AbilityTargeting.Direction;
        public float maxRange = 12f;
        [Tooltip("How long the caster is held in the attack / cast pose.")]
        public float lockTime = 0.3f;
        [Tooltip("Animation base name: attack or cast (empty = none).")]
        public string animBase = "attack";
        [Tooltip("Movement speed multiplier while held in the pose.")]
        public float moveWhileCasting = 0.25f;
        public string castSfx;

        [Header("Levels 1–5")]
        [Tooltip("Power added per level above 1 (plan: +12%).")]
        public float powerPerLevel = 0.12f;
        [Tooltip("Cooldown removed per level above 1 (plan: −4%).")]
        public float cooldownPerLevel = 0.04f;

        [Header("Timeline")]
        [Tooltip("Run in order when the ability is cast; each block can wait its own delay.")]
        [SerializeReference, PickEffect] public List<AbilityEffect> effects = new List<AbilityEffect>();

        public bool HasTag(AbilityTags t) => (tags & t) != 0;

        public float CooldownAt(int level) => cooldown * Mathf.Max(0.2f, 1f - cooldownPerLevel * (Mathf.Max(1, level) - 1));

        public virtual string Tooltip()
        {
            var sb = new StringBuilder(description);
            string cost = energyCost > 0 ? $"<color=#86c4f5>{energyCost:0} năng lượng</color>" : "<color=#aaaaaa>Không tốn năng lượng</color>";
            sb.Append($"\n\n{cost}   ·   <color=#ffe07a>Hồi chiêu {cooldown:0.#}s</color>");
            string tagText = TagText();
            if (tagText.Length > 0) sb.Append($"\n<color=#9a93a8>{tagText}</color>");
            return sb.ToString();
        }

        static readonly (AbilityTags tag, string name)[] TagNames =
        {
            (AbilityTags.Physical, "#Vật lý"), (AbilityTags.Fire, "#Lửa"), (AbilityTags.Ice, "#Băng"), (AbilityTags.Lightning, "#Lôi"),
            (AbilityTags.Wind, "#Phong"), (AbilityTags.Dark, "#Ám"), (AbilityTags.Holy, "#Thánh Mộc"),
            (AbilityTags.Melee, "#Cận"), (AbilityTags.Projectile, "#Đạn"), (AbilityTags.Area, "#Vùng"), (AbilityTags.Channel, "#Kênh"),
            (AbilityTags.Summon, "#Triệu hồi"), (AbilityTags.Movement, "#Dịch chuyển"), (AbilityTags.Support, "#Hỗ trợ"),
            (AbilityTags.Ultimate, "#Tuyệt kỹ"),
        };

        /// <summary>"#Lửa #Đạn" — the tag names talents and gear refer to.</summary>
        public string TagText()
        {
            var parts = new List<string>();
            foreach (var (tag, name) in TagNames)
                if (HasTag(tag)) parts.Add(name);
            return string.Join(" ", parts);
        }
    }

    /// <summary>Marks a [SerializeReference] effect field so the Inspector offers a list of effect types.</summary>
    public class PickEffectAttribute : PropertyAttribute { }
}

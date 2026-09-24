using UnityEngine;

namespace RPG
{
    public class SkillContext
    {
        public PlayerController caster;
        public Vector2 origin;     // caster feet
        public Vector2 aim;        // mouse world position
        public Vector2 dir;        // normalized origin -> aim
        public Team team;
        public float AimDistance => Vector2.Distance(origin, aim);
    }

    /// <summary>Base class of every player skill. Subclasses implement Execute().</summary>
    public abstract class SkillDef : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea(2, 5)] public string description;
        public Sprite icon;
        public float cooldown = 1f;
        public float energyCost;
        [Tooltip("How long the caster is locked in the attack/cast pose.")]
        public float lockTime = 0.3f;
        [Tooltip("Animation base name: attack or cast.")]
        public string animBase = "attack";
        [Tooltip("Movement speed multiplier while locked.")]
        public float moveWhileCasting = 0.25f;
        public string castSfx;
        public float maxRange = 12f;

        public abstract void Execute(SkillContext ctx);

        public virtual string Tooltip()
        {
            string cost = energyCost > 0 ? $"<color=#86c4f5>{energyCost:0} năng lượng</color>" : "<color=#aaaaaa>Không tốn năng lượng</color>";
            return $"{description}\n\n{cost}   ·   <color=#ffe07a>Hồi chiêu {cooldown:0.#}s</color>";
        }
    }
}

using UnityEngine;

namespace RPG
{
    /// <summary>Starts an <see cref="AbilityDef"/> for a caster: pose, cast sound, then its timeline.</summary>
    public static class AbilityRunner
    {
        /// <summary>
        /// Casts toward <paramref name="aim"/> (already clamped to range by the caller).
        /// Costs and cooldowns are the caller's business (PlayerSkills for the hero).
        /// </summary>
        public static AbilityContext Cast(AbilityDef ability, IAbilityCaster caster, Vector2 aim, int level = 1)
        {
            if (ability == null || caster == null) return null;
            Vector2 origin = caster.Runner.transform.position;
            if (ability.targeting == AbilityTargeting.Self) aim = origin;
            Vector2 to = aim - origin;
            Vector2 dir = to.sqrMagnitude > 0.0001f ? to.normalized
                : caster.Motor != null ? caster.Motor.Facing : Vector2.down;
            var ctx = new AbilityContext
            {
                ability = ability,
                caster = caster,
                level = Mathf.Clamp(level, 1, 5),
                origin = origin,
                aim = aim,
                dir = dir,
                point = aim
            };
            caster.BeginAction(ability.animBase, dir, ability.lockTime, ability.moveWhileCasting);
            if (!string.IsNullOrEmpty(ability.castSfx)) AudioManager.Play(ability.castSfx, 0.9f, 0.06f);
            ctx.Run(ability.effects);
            return ctx;
        }
    }
}

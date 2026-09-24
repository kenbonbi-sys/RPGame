using UnityEngine;

namespace RPG
{
    /// <summary>Starts an <see cref="AbilityDef"/> for a caster: pose, cast sound, then its timeline.</summary>
    public static class AbilityRunner
    {
        /// <summary>
        /// Casts toward <paramref name="aim"/> (already clamped to range by the caller).
        /// Costs and cooldowns are the caller's business (PlayerSkills for the hero).
        /// <paramref name="damageMultiplier"/> scales every hit of this cast. Online,
        /// <paramref name="mode"/> says how much of it happens here, <paramref name="seed"/> makes
        /// its random spikes and bolts the same on every machine, and <paramref name="origin"/> is
        /// where the caster stood on its player's screen.
        /// </summary>
        public static AbilityContext Cast(AbilityDef ability, IAbilityCaster caster, Vector2 aim, int level = 1, float damageMultiplier = 1f,
                                          CastMode mode = CastMode.Live, int seed = 0, Vector2? origin = null)
        {
            if (ability == null || caster == null) return null;
            Vector2 from = origin ?? (Vector2)caster.Runner.transform.position;
            if (ability.targeting == AbilityTargeting.Self) aim = from;
            Vector2 to = aim - from;
            Vector2 dir = to.sqrMagnitude > 0.0001f ? to.normalized
                : caster.Motor != null ? caster.Motor.Facing : Vector2.down;
            var ctx = new AbilityContext
            {
                ability = ability,
                caster = caster,
                level = Mathf.Clamp(level, 1, 5),
                origin = from,
                aim = aim,
                dir = dir,
                point = aim,
                damageMultiplier = damageMultiplier,
                live = mode == CastMode.Live || mode == CastMode.Rules,
                visual = mode != CastMode.Rules && GameSession.HasScreen,
                predicted = mode == CastMode.Predicted,
                rng = seed != 0 ? new System.Random(seed) : new System.Random()
            };
            caster.BeginAction(ability.animBase, dir, ability.lockTime, ability.moveWhileCasting);
            if (ctx.visual && !string.IsNullOrEmpty(ability.castSfx))
                AudioManager.Play(ability.castSfx, 0.9f, 0.06f, ctx.predicted || IsLocalCaster(caster) ? (Vector3?)null : caster.Runner.transform.position);
            ctx.Run(ability.effects);
            return ctx;
        }

        /// <summary>The caster is the hero this screen plays (its sounds are flat, everyone else's come from where they stand).</summary>
        static bool IsLocalCaster(IAbilityCaster caster) => !GameSession.Online || caster is PlayerController p && p.IsLocal;
    }
}

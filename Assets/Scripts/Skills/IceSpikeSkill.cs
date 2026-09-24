using System.Collections;
using UnityEngine;

namespace RPG
{
    /// <summary>E — Mũi Băng: a line of ice spikes erupting toward the cursor, slowing enemies.</summary>
    [CreateAssetMenu(menuName = "RPG/Skills/Ice Spikes")]
    public class IceSpikeSkill : SkillDef
    {
        public int count = 7;
        public float spacing = 1.05f;
        public float startOffset = 1.1f;
        public float delay = 0.06f;
        public float damage = 30f;
        public float hitRadius = 0.95f;
        public float slow = 0.5f;
        public float slowDuration = 2.5f;

        public override void Execute(SkillContext ctx)
        {
            ctx.caster.StartCoroutine(Run(ctx));
        }

        IEnumerator Run(SkillContext ctx)
        {
            VFX.Spawn("cast_ice", ctx.origin + Vector2.up * 0.5f, Quaternion.identity);
            for (int i = 0; i < count; i++)
            {
                Vector2 p = ctx.origin + ctx.dir * (startOffset + i * spacing) + Util.RandomInCircle(0.18f);
                if (Util.LineBlocked(ctx.origin, p)) yield break;
                VFX.Spawn("ice_spike", p, Quaternion.identity, 0.9f + i * 0.04f);
                AudioManager.Play("sfx_ice_shatter", 0.35f, 0.15f, p, 0.02f);
                var d = DamageInfo.Make(damage, Team.Player, ctx.caster.gameObject, p, ctx.dir, DamageType.Ice, 1.5f);
                d.slow = slow;
                d.slowDuration = slowDuration;
                d.skillName = displayName;
                if (Combat.DamageCircle(p, hitRadius, d, 0.1f) > 0) CameraRig.Shake(0.05f);
                yield return new WaitForSeconds(delay);
            }
        }
    }
}

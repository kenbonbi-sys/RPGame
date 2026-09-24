using System.Collections;
using UnityEngine;

namespace RPG
{
    /// <summary>D — Bão Kiếm: spectral blades orbit the hero, shredding nearby enemies.</summary>
    [CreateAssetMenu(menuName = "RPG/Skills/Blade Storm")]
    public class BladeStormSkill : SkillDef
    {
        public float duration = 3f;
        public float radius = 2.2f;
        public float tick = 0.25f;
        public float damagePerTick = 11f;

        public override void Execute(SkillContext ctx)
        {
            ctx.caster.StartCoroutine(Run(ctx.caster));
        }

        IEnumerator Run(PlayerController pc)
        {
            pc.AddBuff("bladestorm", displayName, icon, duration);
            var fx = VFX.Spawn("blade_storm", pc.transform.position, Quaternion.identity, 1f, pc.transform, true);
            float end = Time.time + duration;
            float nextTick = 0;
            float nextSfx = 0;
            while (Time.time < end && !pc.IsDead)
            {
                if (Time.time >= nextTick)
                {
                    nextTick = Time.time + tick;
                    Vector2 c = (Vector2)pc.transform.position + Vector2.up * 0.4f;
                    var d = DamageInfo.Make(damagePerTick, Team.Player, pc.gameObject, c, Vector2.zero, DamageType.Physical, 1.2f);
                    d.skillName = displayName;
                    if (Combat.DamageCircle(c, radius, d, 0.1f) > 0) AudioManager.Play("sfx_hit", 0.35f, 0.2f, null, 0.05f);
                }
                if (Time.time >= nextSfx)
                {
                    nextSfx = Time.time + 0.8f;
                    AudioManager.Play("sfx_bladestorm", 0.6f, 0.05f);
                }
                yield return null;
            }
            VFX.Release(fx);
        }
    }
}

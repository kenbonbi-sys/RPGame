using System.Collections;
using UnityEngine;

namespace RPG
{
    /// <summary>A — Hồi Phục: instant heal plus a regeneration aura.</summary>
    [CreateAssetMenu(menuName = "RPG/Skills/Heal")]
    public class HealSkill : SkillDef
    {
        public float instantHeal = 40f;
        public float healPerSecond = 6f;
        public float duration = 4f;

        public override void Execute(SkillContext ctx)
        {
            var pc = ctx.caster;
            pc.health.Heal(instantHeal);
            VFX.Spawn("heal_burst", pc.transform.position, Quaternion.identity, 1f, pc.transform);
            ScreenFX.Flash(new Color(0.4f, 1f, 0.5f), 0.08f, 0.3f);
            pc.AddBuff("regen", displayName, GameManager.I.db.regenIcon, duration);
            pc.StartCoroutine(Regen(pc));
        }

        IEnumerator Regen(PlayerController pc)
        {
            var aura = VFX.Spawn("heal_aura", pc.transform.position, Quaternion.identity, 1f, pc.transform, true);
            float t = 0;
            float acc = 0;
            while (t < duration && !pc.IsDead)
            {
                t += Time.deltaTime;
                acc += healPerSecond * Time.deltaTime;
                if (acc >= healPerSecond * 0.5f)
                {
                    pc.health.Heal(acc);
                    acc = 0;
                }
                yield return null;
            }
            VFX.Release(aura);
        }
    }
}

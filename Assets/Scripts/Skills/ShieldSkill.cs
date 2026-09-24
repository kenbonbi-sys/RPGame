using System.Collections;
using UnityEngine;

namespace RPG
{
    /// <summary>S — Khiên Thánh: a holy barrier that reduces damage and blocks stuns.</summary>
    [CreateAssetMenu(menuName = "RPG/Skills/Shield")]
    public class ShieldSkill : SkillDef
    {
        [Range(0, 1)] public float damageReduction = 0.6f;
        public float duration = 5f;

        public override void Execute(SkillContext ctx)
        {
            ctx.caster.StartCoroutine(Run(ctx.caster));
        }

        IEnumerator Run(PlayerController pc)
        {
            pc.AddBuff("shield", displayName, GameManager.I.db.shieldIcon, duration);
            pc.health.damageTakenMultiplier = 1f - damageReduction;
            if (pc.status != null)
            {
                pc.status.stunImmune = true;
                pc.status.ClearStun();
            }
            VFX.Spawn("shield_cast", pc.transform.position, Quaternion.identity);
            var bubble = VFX.Spawn("shield_bubble", pc.transform.position, Quaternion.identity, 1f, pc.transform, true);
            float end = Time.time + duration;
            while (Time.time < end && !pc.IsDead) yield return null;
            pc.health.damageTakenMultiplier = 1f;
            if (pc.status != null) pc.status.stunImmune = false;
            VFX.Release(bubble);
            if (!pc.IsDead) VFX.Spawn("shield_break", pc.transform.position + Vector3.up * 0.5f, Quaternion.identity);
        }
    }
}

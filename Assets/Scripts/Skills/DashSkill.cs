using System.Collections;
using UnityEngine;

namespace RPG
{
    /// <summary>Space — Lướt: a quick invulnerable dash leaving afterimages.</summary>
    [CreateAssetMenu(menuName = "RPG/Skills/Dash")]
    public class DashSkill : SkillDef
    {
        public float distance = 4.2f;
        public float time = 0.16f;
        public float invulnerableTime = 0.28f;

        public override void Execute(SkillContext ctx)
        {
            var pc = ctx.caster;
            // dash where the player is moving, otherwise toward the cursor
            Vector2 dir = pc.motor.Velocity.sqrMagnitude > 0.5f ? pc.motor.Velocity.normalized : ctx.dir;
            pc.motor.Dash(dir * (distance / time), time);
            if (pc.afterImages != null) pc.afterImages.Emit(time + 0.05f);
            VFX.Spawn("dash_burst", pc.transform.position + Vector3.up * 0.3f, Quaternion.Euler(0, 0, Util.Angle(dir)));
            pc.StartCoroutine(IFrames(pc));
        }

        IEnumerator IFrames(PlayerController pc)
        {
            pc.health.invulnerable = true;
            yield return new WaitForSeconds(invulnerableTime);
            if (!pc.IsDead) pc.health.invulnerable = false;
            VFX.Spawn("step_dust", pc.transform.position, Quaternion.identity, 1.5f);
        }
    }
}

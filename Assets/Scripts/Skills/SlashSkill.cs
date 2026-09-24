using System.Collections;
using UnityEngine;

namespace RPG
{
    /// <summary>Q — Chém Gió: a sweeping sword arc. Every third hit in a combo is a heavy finisher.</summary>
    [CreateAssetMenu(menuName = "RPG/Skills/Slash")]
    public class SlashSkill : SkillDef
    {
        public float damage = 22f;
        public float radius = 1.9f;
        public float angle = 150f;
        public float knockback = 4f;
        public float critChance = 0.15f;
        public float comboWindow = 0.9f;
        public float finisherMultiplier = 1.7f;

        [System.NonSerialized] int combo;
        [System.NonSerialized] float lastCast = -99f;

        public override void Execute(SkillContext ctx)
        {
            combo = Time.time - lastCast <= comboWindow ? combo + 1 : 0;
            lastCast = Time.time;
            bool finisher = combo % 3 == 2;
            ctx.caster.StartCoroutine(Run(ctx, combo % 2 == 1, finisher));
        }

        IEnumerator Run(SkillContext ctx, bool flip, bool finisher)
        {
            yield return new WaitForSeconds(0.05f);
            var pc = ctx.caster;
            Vector2 origin = (Vector2)pc.transform.position + Vector2.up * 0.45f;
            float ang = Util.Angle(ctx.dir);
            var fx = VFX.Spawn(finisher ? "slash_big" : "slash", origin + ctx.dir * 0.55f, Quaternion.Euler(0, 0, ang), finisher ? 1.35f : 1f);
            if (fx != null && flip)
            {
                var s = fx.transform.localScale;
                fx.transform.localScale = new Vector3(s.x, -s.y, s.z);
            }
            AudioManager.Play("sfx_swing", 0.8f, 0.1f);

            var d = DamageInfo.Make(damage * (finisher ? finisherMultiplier : 1f), Team.Player, pc.gameObject, origin, ctx.dir,
                                    DamageType.Physical, knockback * (finisher ? 2f : 1f));
            d.hitStop = finisher ? 0.07f : 0.035f;
            d.skillName = displayName;
            int hits = Combat.DamageCone(origin, ctx.dir, radius * (finisher ? 1.2f : 1f), angle, d, critChance);
            if (hits > 0)
            {
                AudioManager.Play(finisher ? "sfx_hit_heavy" : "sfx_hit", 0.9f, 0.1f);
                CameraRig.Shake(finisher ? 0.22f : 0.08f);
                if (finisher) ScreenFX.Impact(0.35f, 0.2f);
            }
        }
    }
}

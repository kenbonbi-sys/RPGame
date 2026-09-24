using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>R — Lôi Phạt: summons a thunder storm at the cursor; bolts seek enemies and stun them.</summary>
    [CreateAssetMenu(menuName = "RPG/Skills/Lightning Storm")]
    public class LightningSkill : SkillDef
    {
        public int bolts = 8;
        public float areaRadius = 3f;
        public float boltRadius = 1.1f;
        public float damage = 52f;
        public float stun = 0.8f;
        public float chargeTime = 0.4f;
        public float interval = 0.14f;
        [Tooltip("Trấn Áp per bolt hit.")]
        public float poise = 8f;

        readonly List<Health> buffer = new List<Health>();

        public override void Execute(SkillContext ctx)
        {
            ctx.caster.StartCoroutine(Run(ctx));
        }

        IEnumerator Run(SkillContext ctx)
        {
            Vector2 center = ctx.aim;
            var circle = VFX.Spawn("storm_circle", center, Quaternion.identity, areaRadius / 2.5f, null, true);
            VFX.Spawn("cast_lightning", ctx.origin + Vector2.up * 0.6f, Quaternion.identity);
            AudioManager.Play("sfx_lightning_charge", 0.8f);
            yield return new WaitForSeconds(chargeTime);
            for (int i = 0; i < bolts; i++)
            {
                Vector2 p = center + Util.RandomInCircle(areaRadius * 0.85f);
                // prefer enemies inside the storm
                Util.HealthsInCircle(center, areaRadius, Team.Player, buffer);
                if (buffer.Count > 0 && Random.value < 0.75f)
                    p = (Vector2)buffer[Random.Range(0, buffer.Count)].transform.position + Util.RandomInCircle(0.2f);
                if (i == 0) p = center;
                Strike(ctx, p);
                yield return new WaitForSeconds(interval * Random.Range(0.7f, 1.3f));
            }
            yield return new WaitForSeconds(0.2f);
            VFX.Release(circle);
        }

        void Strike(SkillContext ctx, Vector2 p)
        {
            VFX.Spawn("lightning_strike", p, Quaternion.identity);
            AudioManager.Play("sfx_thunder", 0.7f, 0.12f, p, 0.05f);
            CameraRig.Shake(0.22f);
            ScreenFX.Flash(new Color(0.85f, 0.85f, 1f), 0.12f, 0.12f);
            var d = DamageInfo.Make(damage, Team.Player, ctx.caster.gameObject, p, Vector2.down, DamageType.Lightning, 2f);
            d.stun = stun;
            d.poise = poise;
            d.skillName = displayName;
            Combat.DamageCircle(p, boltRadius, d, 0.15f);
        }
    }
}

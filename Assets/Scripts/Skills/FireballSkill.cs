using UnityEngine;

namespace RPG
{
    /// <summary>W — Cầu Lửa: a fireball that explodes in an area and sets targets on fire.</summary>
    [CreateAssetMenu(menuName = "RPG/Skills/Fireball")]
    public class FireballSkill : SkillDef
    {
        public float damage = 46f;
        public float speed = 11f;
        public float explodeRadius = 1.7f;
        public float burnDps = 8f;
        public float burnDuration = 3f;
        public float critChance = 0.12f;
        [Tooltip("Trấn Áp per target hit by the explosion.")]
        public float poise = 12f;

        public override void Execute(SkillContext ctx)
        {
            var db = GameManager.I.db;
            if (db.fireballPrefab == null) return;
            Vector2 start = ctx.origin + Vector2.up * 0.55f + ctx.dir * 0.5f;
            var go = Pool.Get(db.fireballPrefab, start, Quaternion.identity);
            var fx = go.GetComponent<PooledFX>();
            if (fx != null) fx.Persistent = true;
            var p = go.GetComponent<Projectile>();
            p.team = Team.Player;
            p.damage = damage;
            p.speed = speed;
            p.explodeRadius = explodeRadius;
            p.burnDps = burnDps;
            p.burnDuration = burnDuration;
            p.critChance = critChance;
            p.poise = poise;
            p.damageType = DamageType.Fire;
            p.hitVfx = "fire_explosion";
            p.hitSfx = "sfx_fireball_explode";
            p.shake = 0.28f;
            p.lifetime = maxRange / speed;
            p.Launch(ctx.dir, ctx.caster.gameObject);
            VFX.Spawn("cast_fire", start, Quaternion.identity);
        }
    }
}

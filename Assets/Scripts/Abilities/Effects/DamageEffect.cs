using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>What a hit carries besides damage: element, crit, knockback, statuses, poise, hit-stop.</summary>
    [System.Serializable]
    public class HitSpec
    {
        [Tooltip("Share of the caster's Attack: 1.4 = 140%.")]
        public float power = 1f;
        public DamageType type = DamageType.Physical;
        [Range(0, 1)] public float critChance;
        public float knockback;
        [Tooltip("Thanh Trấn Áp damage per target.")]
        public float poise;
        public float hitStop;
        [Tooltip("Statuses of plan §04 the hit applies.")]
        public StatusHit status;

        public DamageInfo Make(AbilityContext ctx, Vector2 at, Vector2 dir)
        {
            var d = ctx.MakeDamage(power, type, at, dir, knockback);
            d.poise = poise;
            d.hitStop = hitStop;
            d.status = status;
            return d;
        }
    }

    /// <summary>Damages everyone in a circle or a cone (#Cận, #Vùng).</summary>
    [System.Serializable]
    public class DamageEffect : AbilityEffect
    {
        public enum Shape { Circle, Cone }

        public Shape shape = Shape.Circle;
        public Anchor at = new Anchor(Anchor.From.Point);
        public float radius = 1.5f;
        [Tooltip("Cone width in degrees.")]
        public float angle = 120f;
        public HitSpec hit = new HitSpec();
        [Tooltip("Run once when at least one target was hit (hit sound, shake).")]
        [SerializeReference, PickEffect] public List<AbilityEffect> onAnyHit = new List<AbilityEffect>();

        public override void Run(AbilityContext ctx)
        {
            Vector2 p = at.Resolve(ctx);
            var d = hit.Make(ctx, p, ctx.dir);
            float r = radius * ctx.scale;
            int n = shape == Shape.Cone
                ? Combat.DamageCone(p, ctx.dir, r, angle, d, hit.critChance)
                : Combat.DamageCircle(p, r, d, hit.critChance);
            if (n > 0) ctx.At(p).Run(onAnyHit);
        }
    }

    /// <summary>A pooled projectile prefab (Projectile component) that damages on hit or explodes (#Đạn).</summary>
    [System.Serializable]
    public class ProjectileEffect : AbilityEffect
    {
        [Tooltip("Prefab with a Projectile component (e.g. Prefabs/Gameplay/Fireball).")]
        public GameObject prefab;
        public Anchor spawn = new Anchor(Anchor.From.Caster, 0.55f, 0.5f);
        public float speed = 10f;
        [Tooltip("0 = fly up to the ability's range.")]
        public float lifetime;
        [Tooltip("0 = hit one target; above 0 = explode and hit everyone in this radius.")]
        public float explodeRadius;
        public bool pierce;
        public HitSpec hit = new HitSpec { knockback = 3f };
        public string hitVfx = "hit_spark";
        public string hitSfx = "sfx_hit";
        public float hitShake = 0.1f;

        public override void Run(AbilityContext ctx)
        {
            if (prefab == null) return;
            Vector2 start = spawn.Resolve(ctx);
            var go = Pool.Get(prefab, start, Quaternion.identity);
            var fx = go.GetComponent<PooledFX>();
            if (fx != null) fx.Persistent = true;
            var p = go.GetComponent<Projectile>();
            if (p == null)
            {
                Debug.LogWarning($"[Ability] {prefab.name} has no Projectile component.");
                Pool.Release(go);
                return;
            }
            p.team = ctx.Team;
            p.damage = hit.power * ctx.HitScale(hit.type);
            p.attackScaled = true;
            p.skillName = ctx.ability.displayName;
            p.damageType = hit.type;
            p.critChance = hit.critChance;
            p.knockback = hit.knockback;
            p.poise = hit.poise;
            p.status = hit.status;
            p.speed = speed;
            p.explodeRadius = explodeRadius;
            p.pierce = pierce;
            p.hitVfx = hitVfx;
            p.hitSfx = hitSfx;
            p.shake = hitShake;
            p.lifetime = lifetime > 0 ? lifetime : ctx.ability.maxRange / Mathf.Max(0.1f, speed);
            p.Launch(ctx.dir, ctx.caster.Runner.gameObject);
        }
    }
}

using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace RPG
{
    /// <summary>A quick dash with invulnerability and after-images (#Dịch chuyển).</summary>
    [System.Serializable]
    public class DashEffect : AbilityEffect
    {
        public float distance = 4f;
        public float time = 0.16f;
        public float invulnerableTime = 0.28f;
        [Tooltip("Dash where the caster is moving; otherwise toward the aim.")]
        public bool preferMoveDirection = true;
        public bool afterImages = true;
        public string startVfx = "dash_burst";
        public string endVfx = "step_dust";
        public float endVfxScale = 1.5f;
        [Tooltip("An attack dodged right after the start is a Lướt Hoàn Hảo (casters with a PerfectDodge: the hero).")]
        public bool perfectDodge = true;
        [Tooltip("Leaps away from the aim instead of toward it (Nhảy Lùi).")]
        public bool away;

        public override void Run(AbilityContext ctx)
        {
            var motor = ctx.caster.Motor;
            if (motor == null) return;
            Vector2 dir = preferMoveDirection && motor.Velocity.sqrMagnitude > 0.5f ? motor.Velocity.normalized : ctx.dir;
            if (away) dir = -ctx.dir;
            // the machine that moves the caster dashes it (online, a hero's own player); the others see it move
            if (motor.enabled) motor.Dash(dir * (distance / Mathf.Max(0.01f, time)), time);
            if (ctx.visual)
            {
                if (afterImages && ctx.caster.AfterImages != null) ctx.caster.AfterImages.Emit(time + 0.05f);
                if (!string.IsNullOrEmpty(startVfx))
                    VFX.Spawn(startVfx, (Vector3)ctx.CasterPosition + Vector3.up * 0.3f, Quaternion.Euler(0, 0, Util.Angle(dir)));
            }
            ctx.Start(Invulnerable(ctx));
            if (perfectDodge && ctx.live)
            {
                var pd = ctx.caster.Runner.GetComponent<PerfectDodge>();
                if (pd != null) pd.Open(ctx.ability.icon);
            }
        }

        IEnumerator Invulnerable(AbilityContext ctx)
        {
            var h = ctx.caster.Health;
            if (ctx.live) h.invulnerable = true;
            yield return new WaitForSeconds(invulnerableTime);
            if (ctx.live && !ctx.caster.IsDead) h.invulnerable = false;
            if (ctx.visual && !string.IsNullOrEmpty(endVfx)) VFX.Spawn(endVfx, ctx.CasterPosition, Quaternion.identity, endVfxScale);
        }
    }

    /// <summary>Heals the caster at once and/or over time (#Hỗ trợ).</summary>
    [System.Serializable]
    public class HealEffect : AbilityEffect
    {
        public float instant;
        [Range(0, 1)] public float instantPercentOfMaxHp;
        [Tooltip("Healing per second for the duration.")]
        public float perSecond;
        [Range(0, 1)] public float perSecondPercentOfMaxHp;
        public float duration;
        [Tooltip("VFX that follows the caster while healing over time.")]
        public string auraVfx;
        [Tooltip("Heroes within this distance of the caster are healed too (0: the caster alone).")]
        public float allies;

        static readonly List<Health> Buffer = new List<Health>(8);

        /// <summary>The caster's healing power (Thông Thái).</summary>
        static float Power(AbilityContext ctx)
        {
            var pc = ctx.caster as PlayerController;
            return pc != null && pc.stats != null ? pc.stats.HealingPower : 1f;
        }

        List<Health> Targets(AbilityContext ctx, List<Health> into)
        {
            into.Clear();
            var me = ctx.caster.Health;
            if (me != null) into.Add(me);
            if (allies <= 0f) return into;
            foreach (var p in Players.All)
                if (p != null && p.health != null && p.health != me && !p.IsDead &&
                    Vector2.Distance(p.transform.position, ctx.CasterPosition) <= allies)
                    into.Add(p.health);
            return into;
        }

        public override void Run(AbilityContext ctx)
        {
            float power = Power(ctx);
            foreach (var h in Targets(ctx, Buffer))
            {
                float now = (instant + instantPercentOfMaxHp * h.maxHp) * power;
                if (now > 0 && ctx.live) h.Heal(now);
                if (ctx.visual && h != ctx.caster.Health) VFX.Spawn("heal_burst", h.transform.position, Quaternion.identity, 0.7f);
            }
            if (duration > 0 && (perSecond > 0 || perSecondPercentOfMaxHp > 0)) ctx.Start(OverTime(ctx, power));
        }

        IEnumerator OverTime(AbilityContext ctx, float power)
        {
            var targets = new List<Health>(Targets(ctx, new List<Health>(8)));
            var aura = string.IsNullOrEmpty(auraVfx) || !ctx.visual ? null : VFX.Spawn(auraVfx, ctx.CasterPosition, Quaternion.identity, 1f, ctx.CasterTransform, true);
            float t = 0, acc = 0;
            while (t < duration && ctx.Alive)
            {
                t += Time.deltaTime;
                acc += Time.deltaTime;
                if (acc >= 0.5f)
                {
                    if (ctx.live)
                        foreach (var h in targets)
                            if (h != null && !h.IsDead) h.Heal((perSecond + perSecondPercentOfMaxHp * h.maxHp) * power * acc);
                    acc = 0;
                }
                yield return null;
            }
            if (aura != null) VFX.Release(aura);
        }
    }

    /// <summary>A timed effect on the caster, shown in the buff bar.</summary>
    [System.Serializable]
    public class BuffSpec
    {
        public string id = "buff";
        public string displayName;
        public Sprite icon;
        public float duration = 5f;
        [Tooltip("1 = unchanged, 0.85 = 15% slower.")]
        public float speedMultiplier = 1f;
        [Tooltip("1 = unchanged, 0.4 = takes 60% less damage.")]
        public float damageTakenMultiplier = 1f;
        public bool stunImmune;
        [Tooltip("Ends a stun already in progress.")]
        public bool clearStun;
        [Tooltip("VFX that follows the caster while the buff lasts.")]
        public string attachedVfx;
        [Tooltip("VFX when the buff ends (not on death).")]
        public string endVfx;
        [Tooltip("1 = unchanged, 1.3 = deals 30% more damage (Cuồng Nộ).")]
        public float damageDealtMultiplier = 1f;
        [Tooltip("Attack speed added to the basic attack, 0.5 = half again as fast (Bùng Nổ Hành Động).")]
        public float attackSpeedBonus;
        [Tooltip("The weapon is charged (Lôi Ấn): the basic attack (Q) hits this much harder, 0.35 = +35%.")]
        public float imbuePower;
        [Tooltip("Stacks of Tích Điện every charged basic attack adds.")]
        public int imbueCharge;
        [Tooltip("Crit chance added (Nhật Thực), 0.5 = +50%.")]
        public float critBonus;
        [Tooltip("Stacks of Lạnh on whoever strikes the hero from close by (Giáp Sương).")]
        public int chillAttackers;
    }

    [System.Serializable]
    public class BuffEffect : AbilityEffect
    {
        public BuffSpec buff = new BuffSpec();
        [Tooltip("Heroes within this distance of the caster get it too (a bard's song, a paladin's aura; 0: the caster alone).")]
        public float allies;

        public override void Run(AbilityContext ctx)
        {
            // where the rules run it is real (and told to every screen); the caster's own screen feels it at once
            if (!ctx.live && !ctx.predicted) return;
            if (buff.clearStun && ctx.caster.Status != null) ctx.caster.Status.ClearStun();
            ctx.caster.AddBuff(buff);
            if (allies <= 0f || !ctx.live) return;
            foreach (var p in Players.All)
                if (p != null && !p.IsDead && (object)p != ctx.caster.Runner && Vector2.Distance(p.transform.position, ctx.CasterPosition) <= allies)
                    p.AddBuff(buff);
        }
    }
}

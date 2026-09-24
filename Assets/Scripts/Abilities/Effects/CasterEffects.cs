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

        public override void Run(AbilityContext ctx)
        {
            var motor = ctx.caster.Motor;
            if (motor == null) return;
            Vector2 dir = preferMoveDirection && motor.Velocity.sqrMagnitude > 0.5f ? motor.Velocity.normalized : ctx.dir;
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

        public override void Run(AbilityContext ctx)
        {
            var h = ctx.caster.Health;
            float now = instant + instantPercentOfMaxHp * h.maxHp;
            if (now > 0 && ctx.live) h.Heal(now);
            if (duration > 0 && (perSecond > 0 || perSecondPercentOfMaxHp > 0)) ctx.Start(OverTime(ctx));
        }

        IEnumerator OverTime(AbilityContext ctx)
        {
            var h = ctx.caster.Health;
            var aura = string.IsNullOrEmpty(auraVfx) || !ctx.visual ? null : VFX.Spawn(auraVfx, ctx.CasterPosition, Quaternion.identity, 1f, ctx.CasterTransform, true);
            float rate = perSecond + perSecondPercentOfMaxHp * h.maxHp;
            float t = 0, acc = 0;
            while (t < duration && ctx.Alive)
            {
                t += Time.deltaTime;
                acc += rate * Time.deltaTime;
                if (acc >= rate * 0.5f)
                {
                    if (ctx.live) h.Heal(acc);
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
    }

    [System.Serializable]
    public class BuffEffect : AbilityEffect
    {
        public BuffSpec buff = new BuffSpec();

        public override void Run(AbilityContext ctx)
        {
            // where the rules run it is real (and told to every screen); the caster's own screen feels it at once
            if (!ctx.live && !ctx.predicted) return;
            if (buff.clearStun && ctx.caster.Status != null) ctx.caster.Status.ClearStun();
            ctx.caster.AddBuff(buff);
        }
    }
}

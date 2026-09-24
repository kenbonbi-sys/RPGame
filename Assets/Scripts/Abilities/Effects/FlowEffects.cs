using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>Runs its blocks at points marching out along the cast direction (a row of ice spikes).</summary>
    [System.Serializable]
    public class LineEffect : AbilityEffect
    {
        public int count = 5;
        public float startOffset = 1f;
        public float spacing = 1f;
        [Tooltip("Seconds between two points.")]
        public float interval = 0.06f;
        [Tooltip("Random offset of each point.")]
        public float jitter;
        [Tooltip("Stop when a wall is between the caster and the next point.")]
        public bool stopAtWalls = true;
        [Tooltip("Size of the first point; each next one adds scaleStep.")]
        public float startScale = 1f;
        public float scaleStep;
        [SerializeReference, PickEffect] public List<AbilityEffect> each = new List<AbilityEffect>();

        public override void Run(AbilityContext ctx) => ctx.Start(Walk(ctx));

        IEnumerator Walk(AbilityContext ctx)
        {
            for (int i = 0; i < count && ctx.Alive; i++)
            {
                Vector2 p = ctx.origin + ctx.dir * (startOffset + i * spacing) + ctx.RandInCircle(jitter);
                if (stopAtWalls && Util.LineBlocked(ctx.origin, p)) yield break;
                var c = ctx.At(p);
                c.scale = startScale + i * scaleStep;
                c.Run(each);
                if (interval > 0) yield return new WaitForSeconds(interval);
            }
        }
    }

    /// <summary>Runs its blocks several times at points inside an area, preferring enemies (a lightning storm).</summary>
    [System.Serializable]
    public class BurstEffect : AbilityEffect
    {
        public Anchor center = new Anchor(Anchor.From.Aim);
        public float radius = 3f;
        public int count = 6;
        [Tooltip("Wind-up before the first point (the area VFX shows meanwhile).")]
        public float chargeTime = 0.4f;
        public float interval = 0.14f;
        [Range(0, 1)] public float intervalJitter = 0.3f;
        [Tooltip("Chance that a point lands on an enemy inside the area.")]
        [Range(0, 1)] public float preferTargets = 0.75f;
        public bool firstAtCenter = true;
        [Tooltip("VFX kept on the area while the burst runs.")]
        public string areaVfx;
        public float areaVfxScale = 1f;
        [SerializeReference, PickEffect] public List<AbilityEffect> each = new List<AbilityEffect>();

        readonly List<Health> buffer = new List<Health>();

        public override void Run(AbilityContext ctx) => ctx.Start(Burst(ctx));

        IEnumerator Burst(AbilityContext ctx)
        {
            Vector2 c = center.Resolve(ctx);
            var area = string.IsNullOrEmpty(areaVfx) || !ctx.visual ? null : VFX.Spawn(areaVfx, c, Quaternion.identity, areaVfxScale, null, true);
            if (chargeTime > 0) yield return new WaitForSeconds(chargeTime);
            for (int i = 0; i < count && ctx.Alive; i++)
            {
                Vector2 p = c + ctx.RandInCircle(radius * 0.85f);
                Util.HealthsInCircle(c, radius, ctx.Team, buffer);
                if (buffer.Count > 0 && ctx.Rand01() < preferTargets)
                    p = (Vector2)buffer[ctx.RandInt(buffer.Count)].transform.position + ctx.RandInCircle(0.2f);
                if (i == 0 && firstAtCenter) p = c;
                ctx.At(p).Run(each);
                yield return new WaitForSeconds(interval * ctx.RandRange(1f - intervalJitter, 1f + intervalJitter));
            }
            yield return new WaitForSeconds(0.2f);
            if (area != null) VFX.Release(area);
        }
    }

    /// <summary>Runs its blocks every interval for a while around the caster (a channelled blade storm).</summary>
    [System.Serializable]
    public class PulseEffect : AbilityEffect
    {
        public float duration = 3f;
        public float interval = 0.25f;
        [Tooltip("Where each pulse happens.")]
        public Anchor at = new Anchor(Anchor.From.Caster, 0.4f);
        [Tooltip("VFX that follows the caster for the duration.")]
        public string attachedVfx;
        [Tooltip("Sound repeated while pulsing.")]
        public string loopSfx;
        [Range(0, 1)] public float loopSfxVolume = 0.6f;
        public float loopSfxInterval = 0.8f;
        [SerializeReference, PickEffect] public List<AbilityEffect> each = new List<AbilityEffect>();

        public override void Run(AbilityContext ctx) => ctx.Start(Pulse(ctx));

        IEnumerator Pulse(AbilityContext ctx)
        {
            var fx = string.IsNullOrEmpty(attachedVfx) || !ctx.visual ? null : VFX.Spawn(attachedVfx, ctx.CasterPosition, Quaternion.identity, 1f, ctx.CasterTransform, true);
            float end = Time.time + duration;
            float nextPulse = 0, nextSfx = 0;
            while (Time.time < end && ctx.Alive)
            {
                if (Time.time >= nextPulse)
                {
                    nextPulse = Time.time + interval;
                    ctx.At(at.Resolve(ctx)).Run(each);
                }
                if (ctx.visual && !string.IsNullOrEmpty(loopSfx) && Time.time >= nextSfx)
                {
                    nextSfx = Time.time + loopSfxInterval;
                    bool mine = !GameSession.Online || ctx.caster.Runner is PlayerController pc && pc.IsLocal;
                    AudioManager.Play(loopSfx, loopSfxVolume, 0.05f, mine ? (Vector3?)null : ctx.CasterPosition);
                }
                yield return null;
            }
            if (fx != null) VFX.Release(fx);
        }
    }

    /// <summary>
    /// Casting again within the window moves to the next stage (a 3-hit sword combo); stages
    /// loop. The running count is handed down as <see cref="AbilityContext.combo"/>.
    /// </summary>
    [System.Serializable]
    public class ComboEffect : AbilityEffect
    {
        [System.Serializable]
        public class Stage
        {
            public string name;
            [SerializeReference, PickEffect] public List<AbilityEffect> effects = new List<AbilityEffect>();
        }

        [Tooltip("Seconds after a cast in which the next cast continues the combo.")]
        public float window = 0.9f;
        public List<Stage> stages = new List<Stage>();

        class Progress
        {
            public int count;
            public float last = -99f;
        }

        // per caster: combos are remembered on whoever uses the ability
        static readonly Dictionary<(Object, ComboEffect), Progress> State = new Dictionary<(Object, ComboEffect), Progress>();

        public override void Run(AbilityContext ctx)
        {
            if (stages.Count == 0) return;
            var key = ((Object)ctx.caster.Runner, this);
            if (!State.TryGetValue(key, out var p)) State[key] = p = new Progress();
            p.count = Time.time - p.last <= window ? p.count + 1 : 0;
            p.last = Time.time;
            var c = ctx.Copy();
            c.combo = p.count;
            c.Run(stages[p.count % stages.Count].effects);
        }
    }
}

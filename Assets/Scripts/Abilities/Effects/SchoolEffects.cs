using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Lightning that leaps from foe to foe (Xích Lôi): the one nearest the aim first, then the
    /// nearest not yet struck within <see cref="jumpRange"/>, each leap weaker by <see cref="falloff"/>.
    /// Every screen draws the same leaps; where the rules run each strikes for real.
    /// </summary>
    [System.Serializable]
    public class ChainEffect : AbilityEffect
    {
        [Tooltip("How far from the caster the first foe may be.")]
        public float range = 8f;
        public float jumpRange = 4.5f;
        [Tooltip("Foes struck in all.")]
        public int jumps = 4;
        [Tooltip("Share of the power lost at every leap.")]
        [Range(0, 1)] public float falloff = 0.15f;
        public float jumpDelay = 0.07f;
        public HitSpec hit = new HitSpec();
        public string boltVfx = "chain_bolt";
        public string hitVfx = "hit_lightning";
        public string sfx = "sfx_thunder";

        static readonly List<Health> Buffer = new List<Health>(16);

        public override void Run(AbilityContext ctx) => ctx.Start(Chain(ctx));

        IEnumerator Chain(AbilityContext ctx)
        {
            Vector2 from = ctx.CasterPosition + Vector2.up * 0.55f;
            var struck = new HashSet<Health>();
            var target = Nearest(ctx.aim, range, ctx.CasterPosition, ctx.Team, struck);
            if (target == null)
            {
                // nothing to strike: the bolt crackles out toward the aim
                if (ctx.visual) Bolt(from, ctx.aim);
                yield break;
            }
            if (ctx.visual) AudioManager.Play(sfx, 0.6f, 0.08f, (Vector3)from);
            for (int i = 0; i < jumps && target != null && ctx.Alive; i++)
            {
                Vector2 at = target.transform.position;
                Vector2 chest = at + Vector2.up * 0.45f;
                if (ctx.visual)
                {
                    Bolt(from, chest);
                    if (!string.IsNullOrEmpty(hitVfx)) VFX.Spawn(hitVfx, chest, Quaternion.identity, 0.8f);
                }
                if (ctx.live)
                {
                    var d = hit.Make(ctx, at, (at - from).normalized);
                    d.amount *= Mathf.Max(0.1f, 1f - falloff * i);
                    d = d.RollCrit(hit.critChance);
                    d.feedback = true;
                    if (target.TakeDamage(d) > 0f) Combat.OnHitFeedback(target, d);
                }
                struck.Add(target);
                from = chest;
                target = Nearest(at, jumpRange, at, ctx.Team, struck);
                if (jumpDelay > 0f) yield return new WaitForSeconds(jumpDelay);
            }
        }

        /// <summary>The foe nearest <paramref name="near"/> within <paramref name="r"/> of <paramref name="center"/>, in sight, not yet struck.</summary>
        static Health Nearest(Vector2 near, float r, Vector2 center, Team team, HashSet<Health> skip)
        {
            Util.HealthsInCircle(center, r, team, Buffer);
            Health best = null;
            float bd = float.MaxValue;
            foreach (var h in Buffer)
            {
                if (h == null || h.IsDead || skip.Contains(h)) continue;
                Vector2 p = h.transform.position;
                if (Util.LineBlocked(center, p)) continue;
                float d = (p - near).sqrMagnitude;
                if (d < bd)
                {
                    bd = d;
                    best = h;
                }
            }
            return best;
        }

        void Bolt(Vector2 a, Vector2 b)
        {
            var go = VFX.Spawn(boltVfx, b, Quaternion.identity);
            var bolt = go != null ? go.GetComponentInChildren<LightningBolt>() : null;
            if (bolt != null) bolt.Between(a, b);
        }
    }

    /// <summary>
    /// A channelled beam at one foe (Hút Hồn): every <see cref="interval"/> it strikes the foe nearest
    /// the aim and gives the caster back <see cref="lifesteal"/> of what it dealt. It looks for a new
    /// foe when the first falls or slips out of reach.
    /// </summary>
    [System.Serializable]
    public class BeamEffect : AbilityEffect
    {
        public float duration = 2f;
        public float interval = 0.2f;
        public float range = 7f;
        public float width = 0.35f;
        public HitSpec hit = new HitSpec();
        [Range(0, 1)] public float lifesteal = 0.3f;
        public Color color = new Color(0.8f, 0.2f, 0.45f);
        public string tickVfx = "dark_hit";
        public string loopSfx = "sfx_beam";

        static readonly List<Health> Buffer = new List<Health>(16);

        public override void Run(AbilityContext ctx) => ctx.Start(Beam(ctx));

        IEnumerator Beam(AbilityContext ctx)
        {
            Health target = null;
            BeamFX fx = null;
            float end = Time.time + duration, next = 0f, nextSfx = 0f;
            while (Time.time < end && ctx.Alive)
            {
                Vector2 hand = ctx.CasterPosition + Vector2.up * 0.6f;
                if (target == null || target.IsDead || Vector2.Distance(target.transform.position, ctx.CasterPosition) > range + 1f)
                    target = Pick(ctx);
                if (target == null)
                {
                    if (fx != null) fx.gameObject.SetActive(false);
                    yield return null;
                    continue;
                }
                Vector2 chest = (Vector2)target.transform.position + Vector2.up * 0.45f;
                if (ctx.visual)
                {
                    if (fx == null)
                    {
                        var go = VFX.Spawn("crystal_beam", hand, Quaternion.identity, 1f, null, true);
                        fx = go != null ? go.GetComponent<BeamFX>() : null;
                        if (fx != null) fx.Set(hand, chest, width, end - Time.time, color);
                    }
                    if (fx != null)
                    {
                        fx.gameObject.SetActive(true);
                        fx.Move(hand, chest);
                    }
                    if (!string.IsNullOrEmpty(loopSfx) && Time.time >= nextSfx)
                    {
                        nextSfx = Time.time + 0.5f;
                        AudioManager.Play(loopSfx, 0.35f, 0.1f, (Vector3)chest, 0.2f);
                    }
                }
                if (Time.time >= next)
                {
                    next = Time.time + interval;
                    if (ctx.visual && !string.IsNullOrEmpty(tickVfx)) VFX.Spawn(tickVfx, chest, Quaternion.identity, 0.6f);
                    if (ctx.live)
                    {
                        var d = hit.Make(ctx, target.transform.position, (chest - hand).normalized).RollCrit(hit.critChance);
                        float dealt = target.TakeDamage(d);
                        if (dealt > 0f)
                        {
                            Combat.OnHitFeedback(target, d);
                            if (lifesteal > 0f && ctx.caster.Health != null) ctx.caster.Health.Heal(dealt * lifesteal);
                        }
                    }
                }
                yield return null;
            }
            if (fx != null) VFX.Release(fx.gameObject);
        }

        Health Pick(AbilityContext ctx)
        {
            Util.HealthsInCircle(ctx.CasterPosition, range, ctx.Team, Buffer);
            Health best = null;
            float bd = float.MaxValue;
            foreach (var h in Buffer)
            {
                if (h == null || h.IsDead || Util.LineBlocked(ctx.CasterPosition, h.transform.position)) continue;
                float d = ((Vector2)h.transform.position - ctx.aim).sqrMagnitude;
                if (d < bd)
                {
                    bd = d;
                    best = h;
                }
            }
            return best;
        }
    }

    /// <summary>
    /// A slow orb (Lôi Cầu) that drifts along the cast, zapping every foe near it every
    /// <see cref="interval"/>, and bursts at the end of its flight or on rock.
    /// </summary>
    [System.Serializable]
    public class OrbEffect : AbilityEffect
    {
        public Anchor spawn = new Anchor(Anchor.From.Caster, 0.55f, 0.5f);
        public float speed = 3f;
        public float duration = 2.6f;
        public float radius = 2f;
        public float interval = 0.3f;
        public HitSpec tick = new HitSpec();
        public float burstRadius = 2.6f;
        public HitSpec burst = new HitSpec();
        public string orbVfx = "lightning_orb";
        public string zapVfx = "chain_bolt";
        public string burstVfx = "lightning_burst";
        public string burstSfx = "sfx_thunder";

        static readonly List<Health> Buffer = new List<Health>(16);

        public override void Run(AbilityContext ctx) => ctx.Start(Fly(ctx));

        IEnumerator Fly(AbilityContext ctx)
        {
            Vector2 p = spawn.Resolve(ctx);
            var fx = ctx.visual && !string.IsNullOrEmpty(orbVfx) ? VFX.Spawn(orbVfx, p, Quaternion.identity, 1f, null, true) : null;
            float t = 0f, next = 0.15f;
            while (t < duration && ctx.Alive)
            {
                float dt = Time.deltaTime;
                t += dt;
                Vector2 step = ctx.dir * speed * dt;
                if (Util.LineBlocked(p, p + step * 3f)) break;   // rock stops it: it bursts there
                p += step;
                if (fx != null) fx.transform.position = p;
                if (t >= next)
                {
                    next = t + interval;
                    Util.HealthsInCircle(p, radius, ctx.Team, Buffer);
                    foreach (var h in Buffer)
                    {
                        if (h == null || h.IsDead) continue;
                        Vector2 chest = (Vector2)h.transform.position + Vector2.up * 0.45f;
                        if (ctx.visual)
                        {
                            var go = VFX.Spawn(zapVfx, chest, Quaternion.identity);
                            var bolt = go != null ? go.GetComponentInChildren<LightningBolt>() : null;
                            if (bolt != null) bolt.Between(p, chest);
                        }
                        if (ctx.live)
                        {
                            var d = tick.Make(ctx, h.transform.position, (chest - p).normalized);
                            d.feedback = false;
                            h.TakeDamage(d);
                        }
                    }
                }
                yield return null;
            }
            if (fx != null) VFX.Release(fx);
            if (!ctx.Alive) yield break;
            if (ctx.visual)
            {
                VFX.Spawn(burstVfx, p, Quaternion.identity, burstRadius / 2.5f);
                AudioManager.Play(burstSfx, 0.7f, 0.08f, (Vector3)p);
            }
            if (ctx.live) Combat.DamageCircle(p, burstRadius, burst.Make(ctx, p, ctx.dir), burst.critChance);
        }
    }

    /// <summary>
    /// Phân Thân: a shadow of the caster steps out beside them for <see cref="duration"/> seconds,
    /// copying their every move in dark smoke, and cuts the foe nearest it every
    /// <see cref="interval"/>; when its time is up it bursts. It stands where it was cast, so every
    /// screen draws it in the same place.
    /// </summary>
    [System.Serializable]
    public class CloneEffect : AbilityEffect
    {
        public Anchor at = new Anchor(Anchor.From.Caster, 0f, 1.2f);
        public float duration = 8f;
        public float interval = 0.8f;
        public float reach = 2.4f;
        public HitSpec hit = new HitSpec();
        public float burstRadius = 2.2f;
        public HitSpec burst = new HitSpec();
        public Color tint = new Color(0.45f, 0.25f, 0.7f, 0.85f);

        static readonly List<Health> Buffer = new List<Health>(16);

        public override void Run(AbilityContext ctx) => ctx.Start(Stand(ctx));

        IEnumerator Stand(AbilityContext ctx)
        {
            Vector2 p = at.Resolve(ctx);
            if (Util.LineBlocked(ctx.CasterPosition, p)) p = ctx.CasterPosition;
            SpriteRenderer shadow = null, source = null;
            GameObject go = null;
            if (ctx.visual)
            {
                var anim = ctx.caster.Runner.GetComponentInChildren<SpriteAnimator>();
                source = anim != null ? anim.target : ctx.caster.Runner.GetComponentInChildren<SpriteRenderer>();
                go = new GameObject("PhanThan");
                go.transform.position = p;
                shadow = go.AddComponent<SpriteRenderer>();
                if (source != null)
                {
                    shadow.sharedMaterial = source.sharedMaterial;
                    shadow.sortingLayerID = source.sortingLayerID;
                    shadow.sortingOrder = source.sortingOrder;
                    shadow.transform.localScale = source.transform.lossyScale;
                }
                shadow.color = tint;
                VFX.Spawn("smoke_cloud", p, Quaternion.identity, 0.5f);
                VFX.Spawn("dark_hit", p + Vector2.up * 0.5f, Quaternion.identity, 1f);
            }
            float end = Time.time + duration, next = Time.time + 0.4f;
            bool faceLeft = ctx.dir.x < 0f;
            float lunge = 0f;
            while (Time.time < end && ctx.Alive)
            {
                if (Time.time >= next)
                {
                    next = Time.time + interval;
                    var foe = Nearest(p, ctx.Team);
                    if (foe != null)
                    {
                        Vector2 at2 = foe.transform.position;
                        faceLeft = at2.x < p.x;
                        lunge = 0.25f;
                        if (ctx.visual)
                            VFX.Spawn("slash", Vector2.Lerp(p, at2, 0.6f) + Vector2.up * 0.4f, Quaternion.Euler(0, 0, Util.Angle(at2 - p)), 0.9f);
                        if (ctx.live)
                        {
                            var d = hit.Make(ctx, at2, (at2 - p).normalized).RollCrit(hit.critChance);
                            d.feedback = true;
                            if (foe.TakeDamage(d) > 0f) Combat.OnHitFeedback(foe, d);
                        }
                    }
                }
                if (shadow != null)
                {
                    // it copies the caster's every move, in shadow
                    if (source != null) shadow.sprite = source.sprite;
                    shadow.flipX = faceLeft;
                    lunge = Mathf.MoveTowards(lunge, 0f, Time.deltaTime);
                    shadow.transform.position = p + new Vector2(faceLeft ? -lunge : lunge, 0f);
                    float fade = Mathf.Clamp01((end - Time.time) / 0.5f);
                    shadow.color = new Color(tint.r, tint.g, tint.b, tint.a * fade * (0.85f + 0.15f * Mathf.Sin(Time.time * 9f)));
                }
                yield return null;
            }
            if (go != null) Object.Destroy(go);
            if (!ctx.Alive) yield break;
            if (ctx.visual)
            {
                VFX.Spawn("smoke_cloud", p, Quaternion.identity, 0.6f);
                VFX.Spawn("dark_strike", p, Quaternion.identity, 0.8f);
            }
            if (ctx.live && burst.power > 0f) Combat.DamageCircle(p, burstRadius, burst.Make(ctx, p, ctx.dir), burst.critChance);
        }

        Health Nearest(Vector2 p, Team team)
        {
            Util.HealthsInCircle(p, reach, team, Buffer);
            Health best = null;
            float bd = float.MaxValue;
            foreach (var h in Buffer)
            {
                if (h == null || h.IsDead) continue;
                float d = ((Vector2)h.transform.position - p).sqrMagnitude;
                if (d < bd)
                {
                    bd = d;
                    best = h;
                }
            }
            return best;
        }
    }
}

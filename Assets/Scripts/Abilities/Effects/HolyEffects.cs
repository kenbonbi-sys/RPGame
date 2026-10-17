using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Tinh Linh Rừng: little forest spirits that fly around the caster for <see cref="duration"/>
    /// seconds, taking turns to shoot a mote of light at the foe nearest them; when their time is up
    /// they melt into the caster, healing <see cref="healOnEnd"/> of the caster's health. They fly
    /// around the hero on every screen; where the rules run the motes strike and the heal lands.
    /// </summary>
    [System.Serializable]
    public class SpiritsEffect : AbilityEffect
    {
        public int count = 2;
        public float duration = 12f;
        [Tooltip("Seconds between two shots of the same spirit (they take turns).")]
        public float interval = 1.2f;
        [Tooltip("How far from a spirit its foe may be.")]
        public float range = 6f;
        public HitSpec hit = new HitSpec();
        [Tooltip("Share of the caster's health healed when the spirits go.")]
        [Range(0, 1)] public float healOnEnd = 0.05f;
        [Tooltip("Radius of their flight around the caster's head.")]
        public float orbit = 0.9f;
        public string spiritVfx = "forest_spirit";
        public string shotVfx = "spirit_shot";
        public string hitVfx = "holy_hit";
        public string shotSfx = "sfx_crystal";

        static readonly List<Health> Buffer = new List<Health>(16);

        public override void Run(AbilityContext ctx) => ctx.Start(Fly(ctx));

        /// <summary>Where spirit <paramref name="i"/> wants to be <paramref name="t"/> seconds after the cast.</summary>
        Vector2 Slot(AbilityContext ctx, int i, float t)
        {
            float a = t * 1.7f + i * Mathf.PI * 2f / Mathf.Max(1, count);
            return ctx.CasterPosition + new Vector2(Mathf.Cos(a) * orbit, 1.05f + Mathf.Sin(a) * orbit * 0.4f + Mathf.Sin(t * 4f + i) * 0.08f);
        }

        IEnumerator Fly(AbilityContext ctx)
        {
            int n = Mathf.Max(1, count);
            var pos = new Vector2[n];
            var fx = new GameObject[n];
            for (int i = 0; i < n; i++)
            {
                pos[i] = Slot(ctx, i, 0f);
                if (!ctx.visual) continue;
                fx[i] = string.IsNullOrEmpty(spiritVfx) ? null : VFX.Spawn(spiritVfx, pos[i], Quaternion.identity, 1f, null, true);
                VFX.Spawn("holy_hit", pos[i], Quaternion.identity, 0.6f);
            }
            float t = 0f, next = 0.5f;
            int turn = 0;
            while (t < duration && ctx.Alive)
            {
                float dt = Time.deltaTime;
                t += dt;
                float follow = 1f - Mathf.Exp(-7f * dt);
                for (int i = 0; i < n; i++)
                {
                    pos[i] = Vector2.Lerp(pos[i], Slot(ctx, i, t), follow);
                    if (fx[i] != null) fx[i].transform.position = pos[i];
                }
                if (t >= next)
                {
                    next = t + interval / n;
                    int i = turn++ % n;
                    var foe = Nearest(pos[i], ctx.Team);
                    if (foe != null) ctx.Start(Shoot(ctx, pos[i], foe));
                }
                yield return null;
            }
            for (int i = 0; i < n; i++)
                if (fx[i] != null) VFX.Release(fx[i]);
            if (!ctx.Alive) yield break;
            var me = ctx.caster.Health;
            if (ctx.visual)
            {
                foreach (var p in pos) VFX.Spawn("holy_hit", p, Quaternion.identity, 0.7f);
                VFX.Spawn("heal_burst", ctx.CasterPosition, Quaternion.identity, 0.7f);
                AudioManager.Play("sfx_heal", 0.5f, 0.05f, (Vector3)ctx.CasterPosition);
            }
            if (ctx.live && me != null && healOnEnd > 0f) me.Heal(me.maxHp * healOnEnd * HealEffect.Power(ctx));
        }

        /// <summary>The foe nearest a spirit, within reach and in sight.</summary>
        Health Nearest(Vector2 from, Team team)
        {
            Util.HealthsInCircle(from, range, team, Buffer);
            Health best = null;
            float bd = float.MaxValue;
            foreach (var h in Buffer)
            {
                if (h == null || h.IsDead) continue;
                Vector2 p = h.transform.position;
                if (Util.LineBlocked(from, p)) continue;
                float d = (p - from).sqrMagnitude;
                if (d < bd)
                {
                    bd = d;
                    best = h;
                }
            }
            return best;
        }

        IEnumerator Shoot(AbilityContext ctx, Vector2 from, Health foe)
        {
            Vector2 to = (Vector2)foe.transform.position + Vector2.up * 0.45f;
            float flight = Mathf.Clamp(Vector2.Distance(from, to) / 14f, 0.06f, 0.4f);
            var mote = ctx.visual && !string.IsNullOrEmpty(shotVfx) ? VFX.Spawn(shotVfx, from, Quaternion.identity, 1f, null, true) : null;
            if (ctx.visual) AudioManager.Play(shotSfx, 0.3f, 0.2f, (Vector3)from, 0.08f);
            for (float t = 0f; t < flight; t += Time.deltaTime)
            {
                // the mote follows its foe
                if (foe != null) to = (Vector2)foe.transform.position + Vector2.up * 0.45f;
                if (mote != null) mote.transform.position = Vector2.Lerp(from, to, t / flight);
                yield return null;
            }
            if (mote != null) VFX.Release(mote);
            if (foe == null || foe.IsDead || !ctx.Alive) yield break;
            if (ctx.visual && !string.IsNullOrEmpty(hitVfx)) VFX.Spawn(hitVfx, to, Quaternion.identity, 0.55f);
            if (ctx.live)
            {
                var d = hit.Make(ctx, foe.transform.position, (to - from).normalized).RollCrit(hit.critChance);
                d.feedback = false;
                foe.TakeDamage(d);
            }
        }
    }

    /// <summary>
    /// Thánh Địa: holy ground where it is cast, for <see cref="duration"/> seconds. Heroes standing
    /// in it heal <see cref="healPerSecond"/> of their health a second and shrug off stuns, roots and
    /// freezes (<see cref="buff"/>, which they keep while they stay inside); foes in it are slowed.
    /// The ground stays where it was cast, and stays if the caster falls. Every screen draws it;
    /// where the rules run it heals, protects and slows, and the buff is told to every screen.
    /// </summary>
    [System.Serializable]
    public class SanctuaryEffect : AbilityEffect
    {
        public Anchor at = new Anchor(Anchor.From.Origin);
        public float radius = 5f;
        public float duration = 8f;
        public float interval = 0.5f;
        [Tooltip("Share of a hero's health healed every second inside.")]
        [Range(0, 1)] public float healPerSecond = 0.04f;
        [Tooltip("How much slower foes inside move, 0.25 = 25%.")]
        [Range(0, 1)] public float slow = 0.25f;
        [Tooltip("What heroes inside have (immunity to stuns, roots and freezes; it ends when they step out).")]
        public BuffSpec buff = new BuffSpec { id = "sanctuary", stunImmune = true, clearStun = true };
        [Tooltip("The ground's VFX, drawn for a radius of 5 at scale 1.")]
        public string zoneVfx = "sanctuary";
        public string pulseSfx = "sfx_crystal";
        public float pulseSfxInterval = 1.6f;

        static readonly List<Health> Buffer = new List<Health>(16);

        public override void Run(AbilityContext ctx) => ctx.Start(Hold(ctx));

        IEnumerator Hold(AbilityContext ctx)
        {
            Vector2 p = at.Resolve(ctx);
            var fx = ctx.visual && !string.IsNullOrEmpty(zoneVfx) ? VFX.Spawn(zoneVfx, p, Quaternion.identity, radius / 5f, null, true) : null;
            float power = HealEffect.Power(ctx);
            float end = Time.time + duration, next = Time.time, nextSfx = Time.time + 0.4f;
            var inside = new List<PlayerController>();
            // the ground outlives its caster (the hero may fall in it), not their leaving the game
            while (Time.time < end && ctx.caster != null && ctx.caster.Runner != null)
            {
                if (Time.time >= next)
                {
                    next = Time.time + interval;
                    if (ctx.live) Pulse(p, end - Time.time, power, inside, ctx.Team);
                }
                if (ctx.visual && !string.IsNullOrEmpty(pulseSfx) && Time.time >= nextSfx)
                {
                    nextSfx = Time.time + pulseSfxInterval;
                    AudioManager.Play(pulseSfx, 0.35f, 0.1f, (Vector3)p);
                }
                yield return null;
            }
            if (fx != null) VFX.Release(fx);
            foreach (var hero in inside)
                if (hero != null) hero.RemoveBuff(buff.id);
        }

        void Pulse(Vector2 p, float left, float power, List<PlayerController> inside, Team team)
        {
            foreach (var hero in Players.All)
            {
                if (hero == null || hero.health == null) continue;
                bool standing = !hero.IsDead && Vector2.Distance(hero.transform.position, p) <= radius;
                if (standing)
                {
                    hero.health.Heal(hero.health.maxHp * healPerSecond * interval * power);
                    if (!inside.Contains(hero))
                    {
                        inside.Add(hero);
                        hero.AddBuff(buff, left);
                    }
                }
                else if (inside.Remove(hero)) hero.RemoveBuff(buff.id);
            }
            Util.HealthsInCircle(p, radius, team, Buffer);
            foreach (var h in Buffer)
                if (h != null && !h.IsDead && h.Status != null) h.Status.Slow(slow, interval + 0.3f);
        }
    }
}

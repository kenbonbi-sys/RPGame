using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Cóc Tía — the swamp's mini-boss, in Ao Cóc Tía (the shared boss rules: <see cref="BossBase"/>).
    /// Lưỡi Kéo: a long tongue that pulls the hero in (toward the pools it spat); Nhảy Đè: a high
    /// leap onto the hero; Phun Độc: lobbed globs that leave poison pools; Bụng Đè: a belly slam
    /// around itself when crowded. Enraged: more globs, a quicker tongue, and Gọi Bầy — it
    /// croaks toads up from its pond (<see cref="brood"/>). It swims.
    /// </summary>
    public class BossToadKing : BossBase
    {
        [Header("Tuning")]
        public float tongueRange = 7f, tongueWidth = 0.75f, tongueDamage = 16f, tonguePull = 6.5f, tongueWindup = 0.65f;
        public float leapRadius = 2.6f, leapDamage = 30f, leapAir = 0.8f;
        public float spitDamage = 14f, spitRadius = 1.3f, spitFlight = 0.9f;
        public float bellyRadius = 3f, bellyDamage = 22f;
        [Tooltip("Toads it croaks up from the pond when enraged (placed with the arena).")]
        public Brood brood;
        public int summonCount = 2;

        static readonly string[] AttackIds = { "tongue", "leap", "spit", "belly", "summon" };
        public override string[] Attacks => AttackIds;

        /// <summary>Where the tongue leaves the mouth.</summary>
        Vector2 Mouth => Pos + new Vector2(body != null && body.flipX ? -0.7f : 0.7f, 1.1f);

        protected override void OnFightStart()
        {
            SetCooldown("tongue", 2f);
            SetCooldown("leap", 4f);
            SetCooldown("summon", 2f);
        }

        protected override void OnFightReset()
        {
            if (brood != null) brood.Dismiss();
        }

        protected override void OnInterrupted()
        {
            if (anim != null) anim.speed = 1f;
        }

        protected override bool Decide(PlayerController p, float dist)
        {
            string pick = PickWeighted(
                ("belly", 5f, dist < bellyRadius * 0.8f),
                ("tongue", 4f, dist > 2.8f && dist < tongueRange - 0.3f),
                ("leap", 3f, dist > 2.5f && dist < 10f),
                ("spit", 3f, dist > 1.5f),
                ("summon", 6f, enraged && brood != null && brood.Waiting > 0 && brood.Out < summonCount));
            if (pick == null) return false;
            Run(AttackRoutine(pick, p));
            return true;
        }

        protected override IEnumerator AttackRoutine(string id, PlayerController p)
        {
            switch (id)
            {
                case "tongue": return Tongue(p);
                case "leap": return Leap(p);
                case "spit": return Spit(p);
                case "belly": return Belly();
                case "summon": return Summon(p);
                default: return null;
            }
        }

        IEnumerator Tongue(PlayerController p)
        {
            SetCooldown("tongue", 5.5f);
            Face(p.transform.position);
            Announce("Lưỡi Kéo");
            float windup = enraged ? tongueWindup * 0.75f : tongueWindup;
            Vector2 from = Mouth;
            Vector2 dir = ((Vector2)p.transform.position + Vector2.up * 0.3f - from).normalized;
            EnemyShots.WarnLine(this, from, dir, tongueRange, 0.42f, 0.8f, windup, t => Warn(t));
            anim.Play("windup", true);
            NetCues.Sound("sfx_telegraph", 0.55f, 0.05f, transform.position);
            yield return new WaitForSeconds(windup);

            anim.Play("tongue", true);
            NetCues.Sound("sfx_tongue", 1f, 0.08f, transform.position);
            for (float s = 0.6f; s <= tongueRange; s += 0.75f)
                NetCues.Vfx("tongue_lash", from + dir * s, Util.Angle(dir), 1f);
            Vector2 to = from + dir * tongueRange;
            foreach (var h in new List<PlayerController>(Players.All))
            {
                if (h == null || h.IsDead) continue;
                Vector2 hp = (Vector2)h.transform.position + Vector2.up * 0.4f;
                if (EnemyShots.DistanceToSegment(hp, from, to) > tongueWidth) continue;
                // pulled toward the toad: into its pools, under its belly
                var d = DamageInfo.Make(tongueDamage, Team.Enemy, gameObject, hp, -dir, DamageType.Physical, tonguePull);
                d.status.slow = 0.35f;
                d.status.slowDuration = 1.5f;
                d.skillName = "Lưỡi Kéo";
                d.feedback = true;
                if (h.health.TakeDamage(d) > 0f) Combat.OnHitFeedback(h.health, d);
            }
            yield return new WaitForSeconds(0.55f);
        }

        IEnumerator Leap(PlayerController p)
        {
            SetCooldown("leap", 6.5f);
            Announce("Nhảy Đè");
            Face(p.transform.position);
            anim.Play("windup", true);
            Vector2 target = (Vector2)p.transform.position + p.motor.Velocity * 0.3f;
            if (Vector2.Distance(target, home) > arenaRadius) target = home + (target - home).normalized * arenaRadius;
            float crouch = enraged ? 0.4f : 0.55f;
            Warn(NetCues.Circle(this, target, leapRadius, crouch + leapAir));
            NetCues.Sound("sfx_telegraph", 0.6f, 0.05f, transform.position);
            yield return new WaitForSeconds(crouch);

            anim.Play("air", true);
            NetCues.Sound("sfx_boss_leap", 0.9f, 0.08f, transform.position);
            NetCues.Vfx("water_splash", Pos, 0f, 1.6f);
            var cols = GetComponentsInChildren<Collider2D>();
            foreach (var c in cols) c.enabled = false;
            Vector2 start = Pos;
            for (float t = 0; t < leapAir;)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / leapAir);
                motor.Teleport(Vector2.Lerp(start, target, Util.EaseInOut(k)));
                if (bodyRoot != null) bodyRoot.localPosition = new Vector3(0, 4f * 2.6f * k * (1 - k), 0);
                yield return null;
            }
            if (bodyRoot != null) bodyRoot.localPosition = Vector3.zero;
            foreach (var c in cols) c.enabled = true;

            anim.Play("slam", true);
            var zone = ZoneRoot.Current;
            bool wet = zone != null && zone.IsWater(target);
            NetCues.Vfx(wet ? "water_splash" : "pounce_land", target, 0f, wet ? 2.2f : 1f);
            NetCues.Sound("sfx_boss_stomp", 0.85f, 0.1f, target);
            NetCues.Shake(0.5f, target);
            NetCues.Impact(0.45f, 0.3f, target);
            var d = DamageInfo.Make(leapDamage, Team.Enemy, gameObject, target, Vector2.down, DamageType.Physical, 8f);
            d.skillName = "Nhảy Đè";
            Combat.DamageCircle(target, leapRadius, d);
            yield return new WaitForSeconds(0.55f);
        }

        IEnumerator Spit(PlayerController p)
        {
            SetCooldown("spit", 5f);
            Announce("Phun Độc");
            Face(p.transform.position);
            anim.Play("spit", true);
            anim.speed = 0f;   // cheeks puffed while it aims
            int globs = enraged ? 5 : 3;
            var targets = new List<Vector2>();
            for (int i = 0; i < globs; i++)
            {
                Vector2 t = (Vector2)p.transform.position + p.motor.Velocity * 0.45f;
                if (i > 0) t += Util.RandomInCircle(2.6f);
                if (Vector2.Distance(t, home) > arenaRadius) t = home + (t - home).normalized * arenaRadius;
                targets.Add(t);
                Warn(NetCues.Circle(this, t, spitRadius, 0.55f + spitFlight + i * 0.12f));
            }
            NetCues.Sound("sfx_telegraph", 0.5f, 0.05f, transform.position);
            yield return new WaitForSeconds(0.55f);
            anim.speed = 1f;
            anim.Play("spit", true);
            for (int i = 0; i < globs; i++)
            {
                NetCues.Sound("sfx_spit", 0.8f, 0.12f, transform.position);
                EnemyShots.VenomArc(gameObject, Mouth, targets[i], spitFlight, spitDamage, spitRadius, 1, "Phun Độc");
                yield return new WaitForSeconds(0.12f);
            }
            yield return new WaitForSeconds(0.4f);
        }

        IEnumerator Belly()
        {
            SetCooldown("belly", 4.5f);
            Announce("Bụng Đè");
            float windup = enraged ? 0.55f : 0.75f;
            Warn(NetCues.Circle(this, Pos, bellyRadius, windup));
            anim.Play("windup", true);
            NetCues.Sound("sfx_croak", 0.9f, 0.05f, transform.position);
            yield return new WaitForSeconds(windup);
            anim.Play("slam", true);
            NetCues.Vfx("stomp_shockwave", Pos + Vector2.up * 0.1f, 0f, bellyRadius / 4.2f);
            NetCues.Vfx("mud_splat", Pos, 0f, 1.6f);
            NetCues.Sound("sfx_boss_stomp", 1f, 0.05f, transform.position);
            NetCues.Shake(0.55f, Pos);
            var d = DamageInfo.Make(bellyDamage, Team.Enemy, gameObject, Pos, Vector2.down, DamageType.Physical, 7f);
            d.skillName = "Bụng Đè";
            Combat.DamageCircle(Pos, bellyRadius, d);
            yield return new WaitForSeconds(0.6f);
        }

        IEnumerator Summon(PlayerController p)
        {
            SetCooldown("summon", 22f);
            Announce("Gọi Bầy");
            anim.Play("roar", true);
            NetCues.FlatSound("sfx_croak", Pos, 1f, 0.02f, NetCues.FarRadius);
            NetCues.Shake(0.3f, Pos);
            yield return new WaitForSeconds(0.6f);
            int n = brood != null ? brood.Release(home + Util.RandomInCircle(arenaRadius * 0.5f), summonCount, p, 2.5f) : 0;
            if (n > 0) NetCues.Log($"{displayName} gọi {n} Cóc Độc lên từ ao!", new Color(1f, 0.7f, 0.5f), Pos);
            yield return new WaitForSeconds(0.8f);
        }
    }
}

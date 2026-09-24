using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Xà Mẫu Đầm Lầy — the swamp's boss, in Đầm Xà Mẫu: a lake with four mounds (the shared boss
    /// rules: <see cref="BossBase"/>). Cắn: a quick lunging bite; Quật Đuôi: a tail sweep across
    /// the whole half in front of it; Phun Nọc: a cone of venom that leaves poison pools;
    /// Lặn–Trồi: dives, swims under the hero (ripples warn) and bursts up beneath them.
    /// Enraged: Lao Thẳng — a straight charge; dodge it into a mound and it is stunned (the
    /// arena's trick) — and Gọi Đỉa, leeches from the lake (<see cref="brood"/>). It swims.
    /// </summary>
    public class BossSnakeMother : BossBase
    {
        [Header("Tuning")]
        public float biteRange = 3.2f, biteDamage = 28f;
        public float tailRadius = 4.4f, tailDamage = 32f;
        public float venomRange = 6.5f, venomDamage = 22f;
        public int venomPoison = 2;
        public float diveRadius = 2.6f, diveDamage = 40f, diveSwimSpeed = 8f, diveWarn = 1f;
        public float chargeSpeed = 13f, chargeDamage = 34f, chargeSeconds = 1.1f, chargeStun = 3f;
        [Tooltip("Leeches it calls from the lake when enraged (placed with the arena).")]
        public Brood brood;
        public int summonCount = 3;

        static readonly string[] AttackIds = { "bite", "tail", "venom", "dive", "charge", "summon" };
        public override string[] Attacks => AttackIds;

        bool submerged;

        Vector2 Mouth => Pos + new Vector2(body != null && body.flipX ? -0.4f : 0.4f, 2.4f);

        protected override void OnFightStart()
        {
            SetCooldown("dive", 5f);
            SetCooldown("venom", 2.5f);
            SetCooldown("charge", 2f);
            SetCooldown("summon", 3f);
        }

        protected override void OnFightReset()
        {
            Surface();
            if (brood != null) brood.Dismiss();
        }

        protected override void OnInterrupted()
        {
            if (anim != null) anim.speed = 1f;
            Surface();
        }

        protected override bool Decide(PlayerController p, float dist)
        {
            string pick = PickWeighted(
                ("bite", 5f, dist < biteRange),
                ("tail", 4f, dist < tailRadius * 0.9f),
                ("venom", 3f, dist > 2f && dist < venomRange + 1f),
                ("dive", 3f, dist > 3.5f),
                ("charge", 4f, enraged && dist > 4f),
                ("summon", 5f, enraged && brood != null && brood.Waiting > 0 && brood.Out < summonCount));
            if (pick == null) return false;
            Run(AttackRoutine(pick, p));
            return true;
        }

        protected override IEnumerator AttackRoutine(string id, PlayerController p)
        {
            switch (id)
            {
                case "bite": return Bite(p);
                case "tail": return Tail(p);
                case "venom": return Venom(p);
                case "dive": return Dive(p);
                case "charge": return Charge(p);
                case "summon": return Summon(p);
                default: return null;
            }
        }

        IEnumerator Bite(PlayerController p)
        {
            SetCooldown("bite", 1.6f);
            Face(p.transform.position);
            Announce("Cắn");
            Vector2 dir = ((Vector2)p.transform.position - Pos).normalized;
            float windup = enraged ? 0.35f : 0.48f;
            Warn(NetCues.Cone(this, Pos + Vector2.up * 0.3f, dir, biteRange + 0.2f, windup));
            anim.Play("windup", true);
            NetCues.Sound("sfx_hiss", 0.6f, 0.08f, transform.position);
            yield return new WaitForSeconds(windup);
            anim.Play("bite", true);
            motor.Dash(dir * 7f, 0.14f);
            NetCues.Sound("sfx_boss_swipe", 0.9f, 0.1f, transform.position);
            var d = DamageInfo.Make(biteDamage, Team.Enemy, gameObject, Pos, dir, DamageType.Physical, 6f);
            d.status.poison = 1;
            d.skillName = "Cắn";
            Combat.DamageCone(Pos + Vector2.up * 0.3f, dir, biteRange + 0.2f, 70f, d);
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator Tail(PlayerController p)
        {
            SetCooldown("tail", 5f);
            Face(p.transform.position);
            Announce("Quật Đuôi");
            Vector2 dir = ((Vector2)p.transform.position - Pos).normalized;
            float windup = enraged ? 0.6f : 0.8f;
            // the half in front of it: two quarter warnings side by side
            Warn(NetCues.Cone(this, Pos, Util.Rotate(dir, 45f), tailRadius, windup));
            Warn(NetCues.Cone(this, Pos, Util.Rotate(dir, -45f), tailRadius, windup));
            anim.Play("windup", true);
            NetCues.Sound("sfx_telegraph", 0.6f, 0.05f, transform.position);
            yield return new WaitForSeconds(windup);
            anim.Play("tail", true);
            NetCues.Sound("sfx_boss_swipe", 1f, 0.06f, transform.position);
            NetCues.Vfx("claw_swipe", Pos + Vector2.up * 0.6f + dir * 1.6f, Util.Angle(dir) - 90f, 2.2f);
            NetCues.Vfx("water_splash", Pos + dir * 2.5f, 0f, 1.6f);
            NetCues.Shake(0.35f, Pos);
            var d = DamageInfo.Make(tailDamage, Team.Enemy, gameObject, Pos, dir, DamageType.Physical, 7f);
            d.skillName = "Quật Đuôi";
            Combat.DamageCone(Pos, dir, tailRadius, 180f, d);
            yield return new WaitForSeconds(0.6f);
        }

        IEnumerator Venom(PlayerController p)
        {
            SetCooldown("venom", 7f);
            Face(p.transform.position);
            Announce("Phun Nọc");
            Vector2 dir = ((Vector2)p.transform.position - Pos).normalized;
            float windup = enraged ? 0.6f : 0.8f;
            Warn(NetCues.Cone(this, Pos + Vector2.up * 0.3f, dir, venomRange, windup, new Color(0.7f, 0.3f, 0.95f, 0.8f)));
            anim.Play("spit", true);
            anim.speed = 0f;   // rears back while it aims
            NetCues.Sound("sfx_hiss", 0.9f, 0.05f, transform.position);
            yield return new WaitForSeconds(windup);
            anim.speed = 1f;
            anim.Play("spit", true);
            NetCues.Sound("sfx_spit", 1f, 0.05f, transform.position);
            NetCues.Vfx("venom_puff", Mouth, 0f, 1.6f);
            for (float s = 1.2f; s <= venomRange; s += 1.1f)
                NetCues.Vfx("venom_hit", Pos + Util.Rotate(dir, Random.Range(-30f, 30f)) * s, 0f, 0.8f + s * 0.12f);
            var d = DamageInfo.Make(venomDamage, Team.Enemy, gameObject, Pos, dir, DamageType.Poison, 3f);
            d.status.poison = venomPoison;
            d.skillName = "Phun Nọc";
            Combat.DamageCone(Pos + Vector2.up * 0.3f, dir, venomRange, 90f, d);
            // the venom stays on the ground in three pools
            float[] at = { 2.4f, 4.1f, 5.6f };
            float[] turn = { 0f, -22f, 22f };
            for (int i = 0; i < at.Length; i++)
                HazardZone.Poison(Pos + Util.Rotate(dir, turn[i] + Random.Range(-8f, 8f)) * at[i], 1.25f, 4f, 1, gameObject, "Phun Nọc");
            yield return new WaitForSeconds(0.6f);
        }

        IEnumerator Dive(PlayerController p)
        {
            SetCooldown("dive", 8f);
            Announce("Lặn");
            anim.Play("submerge", true);
            NetCues.Sound("sfx_splash", 1f, 0.05f, transform.position);
            NetCues.Vfx("water_splash", Pos, 0f, 2f);
            yield return new WaitForSeconds(0.5f);
            Submerge();
            // swims under the hero, ripples trailing
            float ripple = 0f;
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                var hero = PickTarget() ?? p;
                if (hero == null) break;
                Vector2 goal = ClampToArena((Vector2)hero.transform.position);
                Vector2 to = goal - Pos;
                if (to.magnitude < 0.3f && t > 0.6f) break;
                motor.Teleport(Pos + Vector2.ClampMagnitude(to, diveSwimSpeed * Time.deltaTime));
                ripple -= Time.deltaTime;
                if (ripple <= 0f)
                {
                    ripple = 0.22f;
                    NetCues.Vfx("water_ripple", Pos);
                }
                yield return null;
            }
            Vector2 spot = FreeSpot(Pos);
            motor.Teleport(spot);
            Warn(NetCues.Circle(this, spot, diveRadius, diveWarn));
            NetCues.Vfx("water_ripple", spot, 0f, 1.8f);
            NetCues.Sound("sfx_telegraph", 0.65f, 0.05f, spot);
            yield return new WaitForSeconds(diveWarn);
            Surface();
            anim.Play("emerge", true);
            NetCues.Vfx("water_splash", spot, 0f, 2.6f);
            NetCues.Sound("sfx_splash", 1f, 0.05f, spot);
            NetCues.Sound("sfx_boss_stomp", 0.7f, 0.1f, spot);
            NetCues.Shake(0.55f, spot);
            NetCues.Impact(0.45f, 0.3f, spot);
            var d = DamageInfo.Make(diveDamage, Team.Enemy, gameObject, spot, Vector2.up, DamageType.Physical, 6f);
            d.status.stun = 0.5f;
            d.skillName = "Trồi Lên";
            Combat.DamageCircle(spot, diveRadius, d);
            yield return new WaitForSeconds(0.7f);
        }

        IEnumerator Charge(PlayerController p)
        {
            SetCooldown("charge", 9f);
            Face(p.transform.position);
            Announce("Lao Thẳng");
            Vector2 dir = ((Vector2)p.transform.position - Pos).normalized;
            float length = chargeSpeed * chargeSeconds;
            EnemyShots.WarnLine(this, Pos, dir, length, 0.9f, 1.3f, 0.9f, t => Warn(t));
            anim.Play("windup", true);
            NetCues.Sound("sfx_hiss", 1f, 0.05f, transform.position);
            yield return new WaitForSeconds(0.9f);

            anim.Play("bite", true);
            NetCues.Sound("sfx_boss_leap", 0.9f, 0.08f, transform.position);
            if (afterImages != null && GameSession.HasScreen) afterImages.Emit(chargeSeconds, new Color(0.5f, 1f, 0.6f, 0.45f));
            var hit = new HashSet<PlayerController>();
            var own = GetComponentsInChildren<Collider2D>();
            var cast = new RaycastHit2D[6];
            var filter = new ContactFilter2D();
            filter.SetLayerMask(Layers.ObstacleMask);
            filter.useTriggers = false;
            for (float t = 0f; t < chargeSeconds; t += Time.deltaTime)
            {
                float step = chargeSpeed * Time.deltaTime;
                // a mound (anything solid that is not alive) in the way: it crashes and is dazed
                int n = Physics2D.CircleCast(Pos + Vector2.up * 0.4f, 0.8f, dir, filter, cast, step + 0.2f);
                for (int i = 0; i < n; i++)
                {
                    var c = cast[i].collider;
                    if (c == null || System.Array.IndexOf(own, c) >= 0 || c.GetComponentInParent<Health>() != null) continue;
                    NetCues.Vfx("rock_impact", cast[i].point);
                    NetCues.Sound("sfx_rock_impact", 1f, 0.05f, cast[i].point);
                    NetCues.Shake(0.6f, Pos);
                    NetCues.Log($"{displayName} đâm sầm vào gò đất và bị choáng!", Palette.Status, Pos);
                    status.ForceStun(chargeStun);
                    EnterStun();
                    yield break;
                }
                Vector2 next = Pos + dir * step;
                if (Vector2.Distance(next, home) > arenaRadius + 1f) break;
                motor.Teleport(next);
                foreach (var h in Players.All)
                {
                    if (h == null || h.IsDead || hit.Contains(h) || Vector2.Distance(h.transform.position, Pos) > 1.4f) continue;
                    hit.Add(h);
                    var d = DamageInfo.Make(chargeDamage, Team.Enemy, gameObject, h.transform.position, dir, DamageType.Physical, 7f);
                    d.skillName = "Lao Thẳng";
                    d.feedback = true;
                    if (h.health.TakeDamage(d) > 0f) Combat.OnHitFeedback(h.health, d);
                }
                yield return null;
            }
            motor.Stop();
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator Summon(PlayerController p)
        {
            SetCooldown("summon", 24f);
            Announce("Gọi Đỉa");
            anim.Play("roar", true);
            NetCues.FlatSound("sfx_hiss", Pos, 1f, 0.02f, NetCues.FarRadius);
            yield return new WaitForSeconds(0.7f);
            int n = brood != null ? brood.Release(home + Util.RandomInCircle(arenaRadius * 0.6f), summonCount, p, 3f) : 0;
            if (n > 0) NetCues.Log($"{displayName} gọi {n} Đỉa Bùn trồi lên từ đầm!", new Color(1f, 0.7f, 0.5f), Pos);
            yield return new WaitForSeconds(0.6f);
        }

        /// <summary>Under the water: no body to hit or bump into.</summary>
        void Submerge()
        {
            submerged = true;
            health.invulnerable = true;
            foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
        }

        void Surface()
        {
            if (!submerged) return;
            submerged = false;
            health.invulnerable = false;
            foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = true;
        }

        Vector2 ClampToArena(Vector2 p) => Vector2.Distance(p, home) > arenaRadius ? home + (p - home).normalized * arenaRadius : p;

        /// <summary>The nearest spot to <paramref name="p"/> where its body fits (not inside a mound).</summary>
        Vector2 FreeSpot(Vector2 p)
        {
            for (int ring = 0; ring < 6; ring++)
            {
                for (int k = 0; k < (ring == 0 ? 1 : 8); k++)
                {
                    Vector2 c = ClampToArena(p + Util.FromAngle(k * 45f) * ring * 0.6f);
                    if (Physics2D.OverlapCircle(c + Vector2.up * 0.4f, 0.9f, Layers.ObstacleMask) == null) return c;
                }
            }
            return home;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Nhện Chúa Pha Lê — the boss of Hang Pha Lê, in Hang Nhện Chúa: a round hall ringed by six
    /// crystal pillars (the shared boss rules: <see cref="BossBase"/>). Cắn Độc: a poisoned bite;
    /// Tơ Trói: three balls of web that bind; Mưa Nhện Con: cave spiders dropping from the webs
    /// (<see cref="brood"/>); Tia Pha Lê: a beam of crystal light, straight at a hero or banked off
    /// a pillar (<see cref="CrystalPillar"/>) to reach them. The hall's trick: strike the pillar
    /// her beam is about to bounce off before it fires — the pillar shatters, the beam goes back
    /// into her and she is stunned. Enraged she climbs into the dark of the ceiling, follows a
    /// hero there (her shadow warns) and drops on them (Rơi Từ Trần), and fires her beam twice.
    /// </summary>
    public class BossSpiderQueen : BossBase
    {
        [Header("Tuning")]
        public float biteRange = 3f, biteDamage = 40f;
        public int bitePoison = 2;
        public int webCount = 3;
        public float webSpread = 40f, webDamage = 18f, webSpeed = 9f, webRoot = 1.3f;
        public float beamRange = 16f, beamWidth = 0.6f, beamDamage = 52f, beamWarn = 1.2f;
        public Color beamColor = new Color(0.7f, 0.85f, 1f, 1f);
        [Tooltip("Share of her health a beam sent back into her costs her, and seconds of Choáng.")]
        public float backfireShare = 0.05f, backfireStun = 3f;
        public float dropRadius = 2.8f, dropDamage = 58f, dropHeight = 9f, dropFollow = 1.6f, dropWarn = 0.8f, dropChase = 7f;
        [Tooltip("Cave spiders she calls from the webs (placed with the arena).")]
        public Brood brood;
        public int summonCount = 3;

        static readonly string[] AttackIds = { "bite", "web", "beam", "brood", "drop" };
        public override string[] Attacks => AttackIds;

        /// <summary>The pillar her beam in flight would bounce off (null: none), and since when (tests, AutoShot).</summary>
        public CrystalPillar Mirror { get; private set; }
        /// <summary>How many times her own beam came back into her (tests).</summary>
        public int Backfires { get; private set; }

        bool aloft;

        Vector2 Eye => Pos + new Vector2(body != null && body.flipX ? -0.9f : 0.9f, 0.9f);

        protected override void OnFightStart()
        {
            SetCooldown("beam", 3f);
            SetCooldown("web", 1.5f);
            SetCooldown("brood", 6f);
            SetCooldown("drop", 2f);
        }

        protected override void OnFightReset()
        {
            Land();
            Mirror = null;
            if (brood != null) brood.Dismiss();
        }

        protected override void OnInterrupted()
        {
            if (anim != null) anim.speed = 1f;
            Mirror = null;
            Land();
        }

        protected override bool Decide(PlayerController p, float dist)
        {
            string pick = PickWeighted(
                ("bite", 5f, dist < biteRange),
                ("web", 3f, dist > 2.5f && dist < 10f),
                ("beam", 5f, dist > 2.5f),
                ("brood", 4f, brood != null && brood.Waiting > 0 && brood.Out < summonCount),
                ("drop", 5f, enraged && dist > 3f));
            if (pick == null) return false;
            Run(AttackRoutine(pick, p));
            return true;
        }

        protected override IEnumerator AttackRoutine(string id, PlayerController p)
        {
            switch (id)
            {
                case "bite": return Bite(p);
                case "web": return Webs(p);
                case "beam": return Beams(p);
                case "brood": return Summon(p);
                case "drop": return Drop(p);
                default: return null;
            }
        }

        IEnumerator Bite(PlayerController p)
        {
            SetCooldown("bite", 1.8f);
            Face(p.transform.position);
            Announce("Cắn Độc");
            Vector2 dir = ((Vector2)p.transform.position - Pos).normalized;
            float windup = enraged ? 0.38f : 0.5f;
            Warn(NetCues.Cone(this, Pos + Vector2.up * 0.3f, dir, biteRange, windup));
            anim.Play("windup", true);
            NetCues.Sound("sfx_hiss", 0.8f, 0.08f, transform.position);
            yield return new WaitForSeconds(windup);
            anim.Play("bite", true);
            motor.Dash(dir * 7f, 0.14f);
            NetCues.Sound("sfx_boss_swipe", 0.9f, 0.1f, transform.position);
            var d = DamageInfo.Make(biteDamage, Team.Enemy, gameObject, Pos, dir, DamageType.Physical, 6f);
            d.status.poison = bitePoison;
            d.skillName = "Cắn Độc";
            Combat.DamageCone(Pos + Vector2.up * 0.3f, dir, biteRange, 75f, d);
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator Webs(PlayerController p)
        {
            SetCooldown("web", 6f);
            Face(p.transform.position);
            Announce("Tơ Trói");
            anim.Play("cast", true);
            Vector2 mouth = Pos + new Vector2(body != null && body.flipX ? -0.8f : 0.8f, 0.9f);
            Vector2 dir = ((Vector2)p.transform.position + Vector2.up * 0.3f - mouth).normalized;
            for (int i = 0; i < webCount; i++)
            {
                float a = webCount > 1 ? -webSpread * 0.5f + webSpread * i / (webCount - 1) : 0f;
                EnemyShots.WarnLine(this, mouth, Util.Rotate(dir, a), 7f, 0.3f, 1.1f, 0.5f, t => Warn(t));
            }
            NetCues.Sound("sfx_hiss", 0.9f, 0.05f, transform.position);
            yield return new WaitForSeconds(enraged ? 0.4f : 0.55f);
            for (int i = 0; i < webCount; i++)
            {
                float a = webCount > 1 ? -webSpread * 0.5f + webSpread * i / (webCount - 1) : 0f;
                Vector2 d = Util.Rotate(dir, a);
                EnemyShots.Web(gameObject, mouth + d * 0.3f, mouth + d * 9f, webDamage, webSpeed, webRoot, "Tơ Trói", 1f);
            }
            NetCues.Sound("sfx_web", 1f, 0.05f, transform.position);
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator Beams(PlayerController p)
        {
            SetCooldown("beam", 9f);
            yield return Beam(p);
            if (!enraged || state == State.Dead) yield break;
            yield return new WaitForSeconds(0.35f);
            var again = PickTarget() ?? p;
            if (again != null) yield return Beam(again);
        }

        /// <summary>
        /// Tia Pha Lê: rears up, stares (dotted lines show the path, bounce and all) and fires. A
        /// hero striking the pillar it would bounce off during the stare sends it back into her.
        /// </summary>
        IEnumerator Beam(PlayerController p)
        {
            Face(p.transform.position);
            Announce("Tia Pha Lê");
            anim.Play("cast", true);
            anim.speed = 0f;   // reared up, eyes blazing, while it aims
            var path = Plan(p, out var mirror);
            Mirror = mirror;
            EnemyShots.WarnBeam(this, path, beamWidth * 0.8f, beamWarn, t => Warn(t));
            NetCues.Sound("sfx_lightning_charge", 0.9f, 0.05f, transform.position);
            if (mirror != null) NetCues.Vfx("crystal_burst", (Vector2)mirror.transform.position + Vector2.up * 1.4f, 0f, 0.8f);
            float start = Time.time;
            while (Time.time - start < beamWarn)
            {
                if (mirror != null && mirror.StruckAt >= start)
                {
                    yield return Backfire(path, mirror);
                    yield break;
                }
                yield return null;
            }
            anim.speed = 1f;
            Mirror = null;
            NetCues.Sound("sfx_beam", 1f, 0.05f, transform.position);
            NetCues.Shake(0.35f, Pos);
            var d = DamageInfo.Make(beamDamage, Team.Enemy, gameObject, path.a, path.b - path.a, DamageType.Lightning, 4f);
            d.skillName = "Tia Pha Lê";
            EnemyShots.Beam(path, beamWidth, 0.6f, beamColor, d);
            yield return new WaitForSeconds(0.6f);
        }

        /// <summary>The pillar shattered under the beam: the light comes back into her and she reels.</summary>
        IEnumerator Backfire(EnemyShots.BeamPath path, CrystalPillar mirror)
        {
            Backfires++;
            Mirror = null;
            anim.speed = 1f;
            ClearTelegraphs();
            mirror.Shatter();
            Vector2 me = Pos + Vector2.up * 1.2f;
            NetCues.Beam(path.a, path.b, beamWidth, 0.5f, beamColor);
            NetCues.Beam(path.b, me, beamWidth * 1.3f, 0.6f, Color.white);
            NetCues.Sound("sfx_beam", 1f, 0.05f, transform.position);
            NetCues.Vfx("crystal_shatter", me, 0f, 1.6f);
            NetCues.Flash(Color.white, 0.4f, 0.3f, Pos);
            NetCues.Shake(0.6f, Pos);
            NetCues.Log($"Tia Pha Lê dội ngược vào {displayName}!", Palette.Status, Pos);
            // whoever broke the pillar gets the hit
            var by = Players.Nearest(mirror.transform.position, 6f);
            var d = DamageInfo.Make(health.maxHp * backfireShare, Team.Player, by != null ? by.gameObject : null, me, Vector2.down,
                                    DamageType.Lightning, 0f);
            d.pure = true;
            d.skillName = "Tia Pha Lê Dội Ngược";
            health.TakeDamage(d);
            if (state == State.Dead) yield break;
            status.ForceStun(backfireStun);
            EnterStun();
            yield break;
        }

        /// <summary>
        /// Where her beam goes: straight at the hero, or often banked off a whole pillar when the
        /// bounce passes close to them.
        /// </summary>
        EnemyShots.BeamPath Plan(PlayerController p, out CrystalPillar mirror)
        {
            Vector2 heroAt = (Vector2)p.transform.position + Vector2.up * 0.35f;
            var straight = EnemyShots.Trace(Eye, heroAt - Eye, beamRange, true, gameObject);
            mirror = CrystalPillar.Of(straight.mirror);
            if (mirror != null && Vector2.Distance(straight.b, Eye) + 0.8f < Vector2.Distance(heroAt, Eye)) return straight;
            mirror = null;
            // a bank shot: aim at a pillar, see where the light goes after it
            EnemyShots.BeamPath best = straight;
            CrystalPillar bestPillar = null;
            float bestMiss = 1.1f;
            foreach (var pillar in CrystalPillar.All)
            {
                if (pillar == null || pillar.Broken || Vector2.Distance(pillar.transform.position, home) > arenaRadius + 3f) continue;
                Vector2 c = (Vector2)pillar.transform.position + Vector2.up * 0.3f;   // its round foot
                Vector2 side = Vector2.Perpendicular((c - Eye).normalized);
                foreach (float off in new[] { -0.45f, -0.2f, 0f, 0.2f, 0.45f })
                {
                    var path = EnemyShots.Trace(Eye, c + side * off - Eye, beamRange, true, gameObject);
                    if (CrystalPillar.Of(path.mirror) != pillar) continue;
                    float miss = EnemyShots.DistanceToSegment(heroAt, path.b, path.c);
                    if (miss >= bestMiss) continue;
                    bestMiss = miss;
                    best = path;
                    bestPillar = pillar;
                }
            }
            if (bestPillar != null && Random.value < 0.7f)
            {
                mirror = bestPillar;
                return best;
            }
            return straight;
        }

        IEnumerator Summon(PlayerController p)
        {
            SetCooldown("brood", 20f);
            Announce("Mưa Nhện Con");
            anim.Play("roar", true);
            NetCues.FlatSound("sfx_hiss", Pos, 1f, 0.02f, NetCues.FarRadius);
            yield return new WaitForSeconds(0.6f);
            int n = brood != null ? brood.Release(home + Util.RandomInCircle(arenaRadius * 0.5f), summonCount, p, 2.5f) : 0;
            if (n > 0) NetCues.Log($"{n} Nhện Hang buông tơ xuống từ trần hang!", new Color(1f, 0.7f, 0.5f), Pos);
            yield return new WaitForSeconds(0.6f);
        }

        /// <summary>Rơi Từ Trần: up into the dark, along a hero's steps (her shadow shows where), then down on them.</summary>
        IEnumerator Drop(PlayerController p)
        {
            SetCooldown("drop", 10f);
            Announce("Rơi Từ Trần");
            anim.Play("climb", true);
            NetCues.Sound("sfx_hiss", 1f, 0.05f, transform.position);
            aloft = true;
            health.invulnerable = true;
            foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                if (bodyRoot != null) bodyRoot.localPosition = new Vector3(0f, dropHeight * Util.EaseInCubic(t / 0.6f), 0f);
                yield return null;
            }
            if (bodyRoot != null) bodyRoot.localPosition = new Vector3(0f, dropHeight, 0f);
            // her shadow follows the hero across the floor
            for (float t = 0f; t < dropFollow; t += Time.deltaTime)
            {
                var hero = PickTarget() ?? p;
                if (hero == null) break;
                Vector2 goal = (Vector2)hero.transform.position;
                if (Vector2.Distance(goal, home) > arenaRadius) goal = home + (goal - home).normalized * arenaRadius;
                motor.Teleport(Pos + Vector2.ClampMagnitude(goal - Pos, dropChase * Time.deltaTime));
                yield return null;
            }
            Vector2 at = Pos;
            Warn(NetCues.Circle(this, at, dropRadius, dropWarn));
            NetCues.Sound("sfx_telegraph", 0.8f, 0.05f, at);
            yield return new WaitForSeconds(dropWarn);
            anim.Play("air", true);
            for (float t = 0f; t < 0.15f; t += Time.deltaTime)
            {
                if (bodyRoot != null) bodyRoot.localPosition = new Vector3(0f, dropHeight * (1f - t / 0.15f), 0f);
                yield return null;
            }
            Land();
            anim.Play("land", true);
            NetCues.Vfx("rock_impact", at, 0f, 2f);
            NetCues.Vfx("crystal_burst", at + Vector2.up * 0.4f, 0f, 1.4f);
            NetCues.Sound("sfx_boss_stomp", 1f, 0.05f, at);
            NetCues.Shake(0.7f, at);
            NetCues.Impact(0.5f, 0.35f, at);
            var d = DamageInfo.Make(dropDamage, Team.Enemy, gameObject, at, Vector2.down, DamageType.Physical, 8f);
            d.status.stun = 0.6f;
            d.skillName = "Rơi Từ Trần";
            Combat.DamageCircle(at, dropRadius, d);
            yield return new WaitForSeconds(0.9f);
        }

        /// <summary>Back on the floor: her body where it belongs, hittable again.</summary>
        void Land()
        {
            if (!aloft) return;
            aloft = false;
            health.invulnerable = false;
            if (bodyRoot != null) bodyRoot.localPosition = Vector3.zero;
            foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = true;
        }
    }
}

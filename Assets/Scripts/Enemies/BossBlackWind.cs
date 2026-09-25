using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Thủ Lĩnh Hắc Phong — the boss of Thảo Nguyên Gió (the shared boss rules: <see cref="BossBase"/>).
    /// The bandits' chief on the windmill hill, where the wind turns every 12 seconds. Song Đao Chém
    /// Gió: his two blades cut the air and two crescents of wind fly out in a V (a hero at his side is
    /// cut by the blades themselves); the wind bends them. Lốc Xoáy: whirlwinds crawl out at his prey,
    /// drifting with the wind, and throw whoever they run over. Gọi Cung Thủ: two archers of his band
    /// come up the hill (<see cref="archers"/>).
    /// Enraged: the sand rises (Bão Cát: the hill disappears in a storm on every screen near it, see
    /// <see cref="ZoneArea.storm"/>), and he dashes three times in a row through his prey (Lướt Gió
    /// Liên Hoàn, a line before each run). A hero who dodges the third run with a Lướt (its
    /// invulnerability turns it aside, Lướt Hoàn Hảo) throws him off balance: he reels for
    /// <see cref="offBalanceSeconds"/> and every blow bites deeper. Once a fight he calls a duel
    /// (Thách Đấu): his archers withdraw and he comes at the one he challenged without a breath
    /// between blows for <see cref="duelSeconds"/>.
    /// </summary>
    public class BossBlackWind : BossBase
    {
        [Header("The band")]
        [Tooltip("His archers, waiting below the hill until he calls them.")]
        public Brood archers;
        public int archersPerCall = 2;
        [Tooltip("The hill's area: the sandstorm rises over it while he rages.")]
        public ZoneArea hill;

        [Header("Tuning")]
        public float bladeWindup = 0.6f, bladeSpeed = 11f, bladeDamage = 40f, bladeFan = 28f;
        public float slashRadius = 2.3f, slashDamage = 44f;
        public float tornadoWindup = 0.8f, tornadoSpeed = 3.2f, tornadoLife = 5.5f, tornadoDamage = 36f, tornadoStun = 0.7f;
        public float dashWindup = 0.55f, dashSpeed = 18f, dashLength = 8f, dashDamage = 48f, dashWidth = 1.1f;
        public float offBalanceSeconds = 2.2f, offBalanceBonus = 1.5f;
        public float duelSeconds = 9f;

        static readonly string[] AttackIds = { "blades", "tornado", "archers", "dashes", "duel" };
        public override string[] Attacks => AttackIds;

        float offBalanceUntil, duelUntil, nextText;
        bool dueled;
        PlayerController challenged;
        // the third run of Lướt Gió Liên Hoàn: a dodge of it (Health.Evaded) throws him off balance
        bool listening;
        readonly List<Health> heard = new List<Health>();
        readonly HashSet<PlayerController> cut = new HashSet<PlayerController>();

        /// <summary>Reeling after a hero dodged his third run.</summary>
        public bool OffBalance => Time.time < offBalanceUntil;
        /// <summary>In the duel he called: his archers gone, no breath between his blows.</summary>
        public bool Dueling => Time.time < duelUntil;
        /// <summary>The hero he challenged (null out of a duel).</summary>
        public PlayerController Challenged => Dueling ? challenged : null;

        protected override void Awake()
        {
            base.Awake();
            health.Guard = Guard;
        }

        float Guard(DamageInfo d)
        {
            if (!OffBalance) return 1f;
            if (Time.time >= nextText)
            {
                nextText = Time.time + 0.7f;
                NetCues.WorldText("Mất thăng bằng!", health.HeadPosition + Vector3.up * 0.2f, Palette.Crit);
            }
            return offBalanceBonus;
        }

        protected override void OnFightStart()
        {
            SetCooldown("blades", 1f);
            SetCooldown("tornado", 3.5f);
            SetCooldown("archers", 6f);
            SetCooldown("dashes", 1.5f);
            dueled = false;
        }

        protected override void OnFightReset()
        {
            if (archers != null) archers.Dismiss("smoke_cloud");
            offBalanceUntil = duelUntil = 0f;
            challenged = null;
            dueled = false;
            StopListening();
        }

        protected override void OnInterrupted()
        {
            StopListening();
            if (anim != null) anim.speed = 1f;
        }

        protected override bool Decide(PlayerController p, float dist)
        {
            if (Dueling && challenged != null && !challenged.IsDead) p = challenged;
            string pick = PickWeighted(
                ("duel", 20f, enraged && !dueled && health.Fraction < 0.4f),
                ("dashes", 9f, enraged && dist > 2f && dist < dashLength + 2f),
                ("archers", 6f, !Dueling && archers != null && archers.Waiting > 0 && archers.Out < archersPerCall),
                ("tornado", 5f, dist > 3f),
                ("blades", 7f, true));
            if (pick == null) return false;
            Run(AttackRoutine(pick, p));
            return true;
        }

        protected override IEnumerator AttackRoutine(string id, PlayerController p)
        {
            switch (id)
            {
                case "blades": return Blades(p);
                case "tornado": return Tornado(p);
                case "archers": return CallArchers(p);
                case "dashes": return Dashes(p);
                case "duel": return Duel(p);
                default: return null;
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            // Bão Cát: every machine raises the sand from what it knows of his rage
            if (hill != null) hill.storm = Mathf.MoveTowards(hill.storm, Enraged && Engaged ? 1f : 0f, Time.deltaTime * 0.45f);
            // the duel: no breath between his blows
            if (GameSession.IsAuthority && Dueling && recoverUntil > Time.time + 0.15f) recoverUntil = Time.time + 0.15f;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (hill != null) hill.storm = 0f;
            StopListening();
        }

        Vector2 AimAt(PlayerController p)
        {
            Vector2 to = (Vector2)p.transform.position - Pos;
            return to.sqrMagnitude > 0.01f ? to.normalized : new Vector2(body != null && body.flipX ? -1f : 1f, 0f);
        }

        // ================================================================= Song Đao Chém Gió
        IEnumerator Blades(PlayerController p)
        {
            SetCooldown("blades", 3f);
            Announce("Song Đao Chém Gió");
            Vector2 aim = AimAt(p);
            Face(p.transform.position);
            anim.Play("windup", true);
            Warn(NetCues.Cone(this, Pos, aim, slashRadius, bladeWindup));
            for (int k = -1; k <= 1; k += 2)
                EnemyShots.WarnLine(this, Pos + Vector2.up * 0.6f, Quaternion.Euler(0, 0, k * bladeFan * 0.5f) * aim,
                                    bladeSpeed * 1.1f, 0.3f, 1f, bladeWindup, t => Warn(t));
            yield return new WaitForSeconds(bladeWindup);
            ClearTelegraphs();
            anim.Play("attack", true);
            NetCues.Sound("sfx_swing", 1f, 0.05f, transform.position);
            NetCues.Sound("sfx_gust", 0.6f, 0.1f, transform.position);
            NetCues.Vfx("slash_big", Pos + aim * 1f + Vector2.up * 0.6f, Util.Angle(aim), 1.3f);
            var d = DamageInfo.Make(slashDamage, Team.Enemy, gameObject, Pos, aim, DamageType.Physical, 5f);
            d.skillName = "Song Đao Chém Gió";
            Combat.DamageCone(Pos + Vector2.up * 0.3f, aim, slashRadius, 110f, d);
            for (int k = -1; k <= 1; k += 2)
                EnemyShots.WindBlade(gameObject, Pos + Vector2.up * 0.6f, Quaternion.Euler(0, 0, k * bladeFan * 0.5f) * aim,
                                     bladeDamage, bladeSpeed, "Song Đao Chém Gió");
            yield return new WaitForSeconds(0.45f);
        }

        // ================================================================= Lốc Xoáy
        IEnumerator Tornado(PlayerController p)
        {
            SetCooldown("tornado", enraged ? 6f : 8f);
            Announce("Lốc Xoáy");
            Face(p.transform.position);
            anim.Play("roar", true);
            NetCues.Vfx("dash_burst", Pos, 0f, 1.4f);
            NetCues.Sound("sfx_tornado", 1f, 0.05f, transform.position);
            Vector2 aim = AimAt(p);
            int n = enraged ? 2 : 1;
            for (int i = 0; i < n; i++)
            {
                Vector2 d = n == 1 ? aim : (Vector2)(Quaternion.Euler(0, 0, (i == 0 ? -1 : 1) * 30f) * aim);
                EnemyShots.WarnLine(this, Pos + d * 1.2f, d, tornadoSpeed * 1.4f, 0.8f, 1.2f, tornadoWindup, t => Warn(t));
            }
            yield return new WaitForSeconds(tornadoWindup);
            ClearTelegraphs();
            anim.Play("attack", true);
            for (int i = 0; i < n; i++)
            {
                Vector2 d = n == 1 ? aim : (Vector2)(Quaternion.Euler(0, 0, (i == 0 ? -1 : 1) * 30f) * aim);
                EnemyShots.Tornado(gameObject, Pos + d * 1.2f, d, tornadoDamage, tornadoSpeed, tornadoLife, tornadoStun, "Lốc Xoáy");
            }
            yield return new WaitForSeconds(0.5f);
        }

        // ================================================================= Gọi Cung Thủ
        IEnumerator CallArchers(PlayerController p)
        {
            SetCooldown("archers", 18f);
            Announce("Gọi Cung Thủ");
            anim.Play("roar", true);
            NetCues.FlatSound("sfx_boss_roar", Pos, 0.8f, 0.05f);
            yield return new WaitForSeconds(0.6f);
            if (archers != null)
            {
                int came = archers.Release(home + Util.RandomInCircle(arenaRadius * 0.4f) + Vector2.up * arenaRadius * 0.5f,
                                           archersPerCall - archers.Out, p, 3f, "smoke_cloud");
                if (came > 0) NetCues.Log("Cung Thủ Hắc Phong lên đồi!", new Color(1f, 0.7f, 0.5f), Pos);
            }
            yield return new WaitForSeconds(0.6f);
        }

        // ================================================================= Lướt Gió Liên Hoàn
        IEnumerator Dashes(PlayerController p)
        {
            SetCooldown("dashes", 11f);
            Announce("Lướt Gió Liên Hoàn");
            for (int run = 0; run < 3; run++)
            {
                var prey = Dueling && challenged != null && !challenged.IsDead ? challenged : Target != null && !Target.IsDead ? Target : p;
                if (prey == null || prey.IsDead) yield break;
                Vector2 aim = AimAt(prey);
                Face(prey.transform.position);
                float length = Mathf.Min(dashLength, Vector2.Distance(Pos, prey.transform.position) + 2.5f);
                var hit = Physics2D.CircleCast(Pos + Vector2.up * 0.3f, 0.4f, aim, length, Layers.ObstacleMask);
                if (hit.collider != null && hit.collider.GetComponentInParent<Health>() == null) length = Mathf.Max(1f, hit.distance - 0.4f);
                float windup = dashWindup * (1f - run * 0.15f);
                anim.Play("windup", true);
                EnemyShots.WarnLine(this, Pos + Vector2.up * 0.2f, aim, length, dashWidth * 0.5f, 0.8f, windup, t => Warn(t));
                yield return new WaitForSeconds(windup);
                ClearTelegraphs();
                bool last = run == 2;
                if (last) Listen();
                cut.Clear();
                Vector2 from = Pos;
                anim.Play("dash", true);
                NetCues.Sound("sfx_dash", 1f, 0.05f, transform.position);
                NetCues.Vfx("dash_burst", Pos, Util.Angle(aim), 1.2f);
                float t0 = Time.time, seconds = length / dashSpeed;
                motor.Dash(aim * dashSpeed, seconds);
                while (Time.time - t0 < seconds + 0.05f)
                {
                    CutAlong(from, aim, last);
                    yield return null;
                }
                motor.HardStop();
                if (last)
                {
                    // a dodge held back by a slow connection still counts
                    float until = Time.time + 0.45f;
                    while (Time.time < until && !OffBalance) yield return null;
                    StopListening();
                    if (OffBalance) yield break;
                }
                yield return new WaitForSeconds(last ? 0.5f : 0.15f);
            }
        }

        void CutAlong(Vector2 from, Vector2 aim, bool last)
        {
            Vector2 seg = Pos - from;
            foreach (var h in Players.All)
            {
                if (h == null || h.IsDead || cut.Contains(h)) continue;
                Vector2 hp = h.transform.position;
                float t = seg.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(hp - from, seg) / seg.sqrMagnitude) : 0f;
                if (Vector2.Distance(hp, from + seg * t) > dashWidth) continue;
                cut.Add(h);
                var d = DamageInfo.Make(dashDamage, Team.Enemy, gameObject, hp, aim, DamageType.Physical, 6f);
                d.skillName = last ? "Lướt Gió (nhát cuối)" : "Lướt Gió";
                d.feedback = true;
                if (h.health.TakeDamage(d) > 0f) Combat.OnHitFeedback(h.health, d);
            }
        }

        void Listen()
        {
            StopListening();
            listening = true;
            foreach (var h in Players.All)
            {
                if (h == null || h.health == null) continue;
                h.health.Evaded += OnHeroEvaded;
                heard.Add(h.health);
            }
        }

        void StopListening()
        {
            foreach (var h in heard)
                if (h != null) h.Evaded -= OnHeroEvaded;
            heard.Clear();
            listening = false;
        }

        /// <summary>A hero turned his last run aside with a Lướt: he loses his balance.</summary>
        void OnHeroEvaded(DamageInfo d)
        {
            if (!listening || d.source != gameObject || OffBalance) return;
            StopListening();
            offBalanceUntil = Time.time + offBalanceSeconds;
            status.ForceStun(offBalanceSeconds);
            NetCues.WorldText("Mất thăng bằng!", health.HeadPosition + Vector3.up * 0.4f, Palette.Crit);
            NetCues.Sound("sfx_stun", 1f, 0.05f, transform.position);
            NetCues.Shake(0.3f, Pos);
            NetCues.Log("Lướt Hoàn Hảo! Thủ Lĩnh Hắc Phong mất thăng bằng.", new Color(1f, 0.85f, 0.4f), Pos);
        }

        /// <summary>Throws him off balance at once (tests of what follows it).</summary>
        public void DebugOffBalance()
        {
            if (!GameSession.IsAuthority) return;
            listening = true;
            OnHeroEvaded(new DamageInfo { source = gameObject });
        }

        // ================================================================= Thách Đấu
        IEnumerator Duel(PlayerController p)
        {
            dueled = true;
            challenged = p;
            duelUntil = Time.time + duelSeconds + 1.2f;
            threat.Add(p, health.maxHp);
            Announce("Thách Đấu");
            anim.Play("roar", true);
            NetCues.FlatSound("sfx_boss_roar", Pos, 1f, 0.02f);
            NetCues.Log($"Thủ Lĩnh Hắc Phong thách đấu {ServerPlayers.NameOf(p) ?? "ngươi"}! Cung thủ rút lui.", new Color(1f, 0.6f, 0.4f), Pos);
            if (archers != null) archers.Dismiss("smoke_cloud");
            yield return new WaitForSeconds(1.2f);
        }
    }
}

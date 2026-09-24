using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Mimic Tham Lam, the cave's hidden boss: a treasure chest somewhere in Hang Pha Lê
    /// (<see cref="hideSpots"/>) that springs on whoever walks up to open it. Its bite swallows
    /// part of the hero's gold; then it digs into the ground and comes up in another chamber,
    /// running from the heroes. The third time it digs down it is gone with the gold (it waits as
    /// a chest again later, somewhere else). Felled before that, it spits out twice what it took
    /// from each hero. Where the rules run it thinks; the players' screens follow its snapshots.
    /// </summary>
    public class MimicAI : EnemyBase
    {
        [Header("Mimic")]
        [Tooltip("Where it may wait as a chest and come up after digging down (placed with the cave).")]
        public Vector2[] hideSpots = new Vector2[0];
        [Tooltip("How close a hero walks up to the 'chest' before it springs.")]
        public float springRange = 1.5f;
        public float fightAggro = 9f;
        public float biteRadius = 1.5f;
        public float biteWindup = 0.45f;
        public float biteDamage = 38f;
        [Tooltip("Share of a bitten hero's gold it swallows (at least minSteal, at most maxSteal).")]
        [Range(0f, 1f)] public float stealShare = 0.1f;
        public int minSteal = 5;
        public int maxSteal = 250;
        public float slamRadius = 1.9f;
        public float slamWindup = 0.8f;
        public float slamDamage = 44f;
        public float slamCooldown = 7f;
        public float fleeSpeed = 1.35f;
        [Tooltip("Seconds it runs about after coming up before it digs down again.")]
        public float fleeSeconds = 7f;
        [Tooltip("The time it digs down for good (the third).")]
        public int escapeAt = 3;

        enum Phase { Hidden, Spring, Fight, Flee, Burrow, Emerge }
        Phase phase;
        float phaseTime;
        bool acted;
        bool slamming;
        float nextSlam;
        Vector2 aim, slamAt;
        int burrows;
        float hpAtPhase;
        readonly Dictionary<PlayerController, int> stolen = new Dictionary<PlayerController, int>();

        /// <summary>Still pretending to be a chest.</summary>
        public bool Disguised => phase == Phase.Hidden;
        /// <summary>How many times it has dug down.</summary>
        public int Burrows => burrows;
        /// <summary>Gold it holds of <paramref name="p"/>'s.</summary>
        public int StolenFrom(PlayerController p) => p != null && stolen.TryGetValue(p, out int n) ? n : 0;

        protected override void OnEnable()
        {
            base.OnEnable();
            stolen.Clear();
            burrows = 0;
            SetPhase(Phase.Hidden);
            aggroRange = springRange;
            SetHittable(true);
            SetIntangible(false);
            if (GameSession.IsAuthority && hideSpots != null && hideSpots.Length > 0)
            {
                // straight onto the spot: its motor may not be awake yet (the first time it is enabled)
                var spot = hideSpots[Random.Range(0, hideSpots.Length)];
                transform.position = spot;
                var rb = GetComponent<Rigidbody2D>();
                if (rb != null) rb.position = spot;
                home = spot;
            }
            if (anim != null) anim.Play("hidden", true);
        }

        void SetPhase(Phase p)
        {
            phase = p;
            phaseTime = 0f;
            acted = false;
        }

        protected override void Think()
        {
            phaseTime += Time.deltaTime;
            switch (phase)
            {
                case Phase.Hidden:
                    motor.Stop();
                    if (anim != null && anim.Current != "hidden") anim.Play("hidden");
                    if (state == State.Chase || Notice()) Spring();
                    return;
                case Phase.Spring:
                    motor.Stop();
                    if (phaseTime >= 0.7f)
                    {
                        SetPhase(Phase.Fight);
                        hpAtPhase = health.hp;
                        SetState(State.Chase);
                    }
                    return;
                case Phase.Burrow:
                    motor.Stop();
                    if (phaseTime >= 1f) Surface();
                    return;
                case Phase.Emerge:
                    motor.Stop();
                    if (phaseTime >= 0.8f)
                    {
                        SetPhase(Phase.Flee);
                        hpAtPhase = health.hp;
                        SetHittable(true);
                        SetIntangible(false);
                        SetState(State.Chase);
                    }
                    return;
            }
            // everyone left or fell: it digs down (the third time, gone with the gold)
            if (state == State.Return)
            {
                Burrow();
                return;
            }
            base.Think();
        }

        /// <summary>The chest opens its lid and teeth: the fight begins.</summary>
        void Spring()
        {
            SetPhase(Phase.Spring);
            aggroRange = fightAggro;
            leashRange = 40f;
            SetState(State.Chase);
            var p = Players.Nearest(Pos, fightAggro);
            if (p != null)
            {
                threat.Add(p, 1f);
                Face(p.transform.position);
            }
            if (anim != null) anim.Play("spring", true);
            NetCues.Sound("sfx_chest_open", 0.9f, 0.05f, transform.position);
            NetCues.Sound("sfx_mimic", 1f, 0.05f, transform.position);
            NetCues.Shake(0.25f, Pos);
            NetCues.Log($"Rương kho báu há miệng: {displayName}!", new Color(1f, 0.75f, 0.35f), Pos, NetCues.NearRadius);
            NetCues.Announce(health, "Mimic!");
            Bestiary.ForWitnesses(Pos, b => b.RecordSeen(enemyId, displayName));
            nextAttack = Time.time + 0.9f;
            nextSlam = Time.time + 3f;
        }

        /// <summary>Springs at once (AutoShot, tests). Where the rules run.</summary>
        public void DebugSpring()
        {
            if (!GameSession.IsAuthority || IsDead || !Disguised) return;
            Spring();
        }

        /// <summary>Digs down now (AutoShot, tests). Where the rules run.</summary>
        public void DebugBurrow()
        {
            if (!GameSession.IsAuthority || IsDead || Disguised) return;
            Burrow();
        }

        protected override void ChaseBehaviour(PlayerController p, float dist)
        {
            // it digs down when it has taken gold and been hurt enough, or has run long enough
            bool hurt = health.hp < hpAtPhase - health.maxHp * 0.22f;
            if (phase == Phase.Fight && (stolen.Count > 0 && phaseTime > 2.5f || hurt)) { Burrow(); return; }
            if (phase == Phase.Flee && (phaseTime > fleeSeconds || hurt)) { Burrow(); return; }
            if (dist <= biteRadius + 0.2f && Time.time >= nextAttack)
            {
                Begin(p, false);
                return;
            }
            if (phase == Phase.Fight && Time.time >= nextSlam && dist > 2f && dist < 5.5f)
            {
                Begin(p, true);
                return;
            }
            if (phase == Phase.Flee)
            {
                // away from whoever is nearest, the walls turning it aside
                var near = Players.Nearest(Pos, 30f) ?? p;
                Vector2 away = Pos - (Vector2)near.transform.position;
                if (away.sqrMagnitude < 0.01f) away = Random.insideUnitCircle;
                Vector2 dir = away.normalized;
                if (Util.LineBlocked(Pos, Pos + dir * 1.5f)) dir = Vector2.Perpendicular(dir) * (Random.value < 0.5f ? 1f : -1f);
                motor.Move(dir, fleeSpeed * (status != null ? status.SpeedMultiplier : 1f));
                PlayMove();
                return;
            }
            MoveTo(p.transform.position, biteRadius * 0.8f);
        }

        void Begin(PlayerController p, bool slam)
        {
            SetState(State.Attack);
            slamming = slam;
            acted = false;
            motor.Stop();
            Face(p.transform.position);
            Vector2 to = (Vector2)p.transform.position - Pos;
            aim = to.sqrMagnitude > 0.01f ? to.normalized : Vector2.right;
            if (anim != null) anim.Play("windup", true);
            if (slam)
            {
                slamAt = p.transform.position;
                Warn(NetCues.Circle(this, slamAt, slamRadius, slamWindup));
                NetCues.Sound("sfx_telegraph", 0.5f, 0.05f, transform.position);
            }
            else Warn(NetCues.Cone(this, Pos + Vector2.up * 0.2f, aim, biteRadius, biteWindup));
        }

        protected override void AttackBehaviour(PlayerController p, float dist)
        {
            if (slamming)
            {
                if (stateTime < slamWindup * 0.5f)
                {
                    motor.Stop();
                    return;
                }
                if (!acted)
                {
                    acted = true;
                    if (anim != null) anim.Play("move", true);
                    motor.Dash((slamAt - Pos) / Mathf.Max(0.1f, slamWindup * 0.5f), slamWindup * 0.5f);
                }
                if (stateTime >= slamWindup && stateTime < slamWindup + 0.05f + Time.deltaTime)
                {
                    stateTime = slamWindup + 0.1f;
                    ForgetWarnings();
                    if (anim != null) anim.Play("attack", true);
                    NetCues.Vfx("rock_impact", slamAt, 0f, 1.2f);
                    NetCues.Sound("sfx_boss_stomp", 0.8f, 0.08f, slamAt);
                    NetCues.Shake(0.3f, slamAt);
                    var d = DamageInfo.Make(slamDamage, Team.Enemy, gameObject, slamAt, Vector2.down, DamageType.Physical, 6f);
                    d.skillName = "Nhảy Đè";
                    Combat.DamageCircle(slamAt, slamRadius, d);
                }
                if (stateTime > slamWindup + 0.7f)
                {
                    nextSlam = Time.time + slamCooldown * Random.Range(0.9f, 1.2f) / AttackSpeed;
                    SetState(State.Chase);
                }
                return;
            }
            motor.Stop();
            if (!acted && stateTime >= biteWindup)
            {
                acted = true;
                ForgetWarnings();
                if (anim != null) anim.Play("attack", true);
                NetCues.Sound("sfx_mimic", 0.8f, 0.1f, transform.position);
                var hit = new List<Health>();
                Util.HealthsInCone(Pos + Vector2.up * 0.2f, aim, biteRadius, 100f, Team.Enemy, hit);
                foreach (var h in hit)
                {
                    var hero = h.GetComponent<PlayerController>();
                    if (hero == null) continue;
                    var d = DamageInfo.Make(biteDamage, Team.Enemy, gameObject, h.transform.position, aim, DamageType.Physical, 5f);
                    d.skillName = "Cắn Tham";
                    d.feedback = true;
                    if (h.TakeDamage(d) > 0f)
                    {
                        Combat.OnHitFeedback(h, d);
                        Swallow(hero);
                    }
                }
            }
            if (stateTime > biteWindup + 0.5f)
            {
                nextAttack = Time.time + attackCooldown * Random.Range(0.9f, 1.2f) / AttackSpeed;
                SetState(State.Chase);
            }
        }

        /// <summary>Its bite takes part of a hero's gold.</summary>
        void Swallow(PlayerController hero)
        {
            var bag = hero.inventory;
            if (bag == null || bag.gold <= 0) return;
            int n = Mathf.Min(bag.gold, Mathf.Clamp(Mathf.RoundToInt(bag.gold * stealShare), minSteal, maxSteal));
            if (n <= 0) return;
            bag.gold -= n;
            bag.NotifyChanged();
            stolen[hero] = StolenFrom(hero) + n;
            NetCues.WorldText($"-{n} vàng", hero.health.HeadPosition + Vector3.up * 0.5f, new Color(1f, 0.8f, 0.3f));
            NetCues.Vfx("coin_burst", (Vector2)hero.transform.position + Vector2.up * 0.6f, 0f, 0.8f);
            Notify.Log(hero, $"{displayName} nuốt mất {n} vàng của bạn! Hạ nó trước khi nó trốn thoát để lấy lại gấp đôi.", new Color(1f, 0.75f, 0.35f));
        }

        void Burrow()
        {
            burrows++;
            SetPhase(Phase.Burrow);
            SetState(State.Chase);
            motor.Stop();
            ClearWarnings();
            SetHittable(false);
            SetIntangible(true);
            if (anim != null) anim.Play("burrow", true);
            NetCues.Vfx("dig_dust", Pos, 0f, 1.3f);
            NetCues.Sound("sfx_rock_impact", 0.7f, 0.15f, transform.position);
            NetCues.Announce(health, burrows >= escapeAt ? "Trốn Thoát" : "Chui Xuống Đất");
        }

        /// <summary>Comes up in another chamber, or, the last time, is gone with the gold.</summary>
        void Surface()
        {
            if (burrows >= escapeAt)
            {
                Escape();
                return;
            }
            Vector2 spot = FarSpot();
            motor.Teleport(spot);
            home = spot;
            SetPhase(Phase.Emerge);
            if (anim != null) anim.Play("emerge", true);
            NetCues.Vfx("dig_dust", spot, 0f, 1.3f);
            NetCues.Sound("sfx_rock_impact", 0.7f, 0.15f, spot);
            NetCues.Log($"{displayName} trồi lên ở chỗ khác trong hang!", new Color(1f, 0.75f, 0.35f), spot, NetCues.FarRadius);
        }

        /// <summary>A hide spot away from where it went down (the farthest of two picked at random).</summary>
        Vector2 FarSpot()
        {
            if (hideSpots == null || hideSpots.Length == 0) return Pos + Random.insideUnitCircle.normalized * 6f;
            Vector2 a = hideSpots[Random.Range(0, hideSpots.Length)], b = hideSpots[Random.Range(0, hideSpots.Length)];
            return Vector2.Distance(a, Pos) >= Vector2.Distance(b, Pos) ? a : b;
        }

        /// <summary>Gone with the gold; it waits as a chest again after its camp's delay.</summary>
        void Escape()
        {
            foreach (var kv in stolen)
                if (kv.Key != null) Notify.Log(kv.Key, $"{displayName} đã trốn mất cùng {kv.Value} vàng của bạn.", new Color(1f, 0.6f, 0.5f));
            NetCues.Log($"{displayName} đã trốn mất!", new Color(1f, 0.6f, 0.5f), Pos, NetCues.FarRadius);
            stolen.Clear();
            threat.Clear();
            target = null;
            health.ForgetAttackers();
            if (spawner != null) spawner.NotifyDead(this);
            motor.HardStop();
            gameObject.SetActive(false);
        }

        protected override void OnDied(DamageInfo d)
        {
            base.OnDied(d);
            if (GameSession.IsAuthority)
            {
                // it spits out twice what it swallowed, to whom it belonged
                foreach (var kv in stolen)
                {
                    var hero = kv.Key;
                    if (hero == null || hero.inventory == null) continue;
                    hero.inventory.gold += kv.Value * 2;
                    hero.inventory.NotifyChanged();
                    NetCues.WorldText($"+{kv.Value * 2} vàng", hero.health.HeadPosition + Vector3.up * 0.5f, new Color(1f, 0.85f, 0.35f));
                    Notify.Log(hero, $"{displayName} nhả ra {kv.Value * 2} vàng (gấp đôi số nó nuốt)!", Palette.Xp);
                }
                stolen.Clear();
                NetCues.Vfx("coin_burst", Pos + Vector2.up * 0.5f, 0f, 1.6f);
            }
            if (!GameSession.HasScreen) return;
            AudioManager.Play("sfx_chest_open", 0.8f, 0.1f, transform.position);
        }

        protected override void OnAttackInterrupted()
        {
            if (acted) return;
            nextAttack = Time.time + attackCooldown / AttackSpeed;
            SetState(State.Chase);
        }

        protected override void PlayIdle()
        {
            if (anim == null) return;
            if (phase == Phase.Hidden) { if (anim.Current != "hidden") anim.Play("hidden"); }
            else if (anim.Current != "idle") anim.Play("idle");
        }

        protected override void PlayMove()
        {
            if (anim != null && anim.Current != "move") anim.Play("move");
        }
    }
}

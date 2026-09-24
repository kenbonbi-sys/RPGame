using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>The statuses one hit applies (plan §04). Stacks add up to each status's cap.</summary>
    [Serializable]
    public struct StatusHit
    {
        [Tooltip("Bỏng stacks: each burns 30% of the hit's damage per second for 3 s (up to 3, a new stack refreshes them).")]
        public int burn;
        [Tooltip("Lạnh stacks: −12% move and attack speed each for 4 s; the 4th freezes (Đóng Băng).")]
        public int chill;
        [Tooltip("Tích Điện stacks for 5 s; the 3rd discharges 80% of the hit to 3 enemies nearby.")]
        public int charge;
        [Tooltip("Độc stacks: 1.5% of max HP per second each for 6 s (up to 5).")]
        public int poison;
        [Tooltip("Choáng seconds: cannot act.")]
        public float stun;
        [Tooltip("Trói seconds: cannot move, can still attack and cast.")]
        public float root;
        [Tooltip("Làm Chậm: share of move speed lost (0.3 = −30%).")]
        [Range(0, 1)] public float slow;
        [Tooltip("Làm Chậm seconds (0 = the default of the combat config).")]
        public float slowDuration;
        [Tooltip("Nguyền seconds: +15% damage taken, −20% damage dealt.")]
        public float curse;
        [Tooltip("Phán Xét seconds: +20% damage taken from every source.")]
        public float judgment;

        public bool Any => burn > 0 || chill > 0 || charge > 0 || poison > 0 || stun > 0 || root > 0 || slow > 0 || curse > 0 || judgment > 0;
    }

    /// <summary>
    /// The ten statuses of plan §04 on a character: Bỏng, Lạnh → Đóng Băng, Tích Điện, Độc, Choáng,
    /// Trói, Làm Chậm, Đẩy Lùi (a knockback into a wall stuns), Nguyền and Phán Xét, with stacks,
    /// damage ticks and visuals. Hard crowd control (Choáng, Đóng Băng, Trói) lasts 40% less each
    /// time it lands again within 6 s, and a boss or elite (whoever has a Thanh Trấn Áp) shrugs it
    /// off for 4 s after a stun or freeze ends. The numbers are in <see cref="CombatConfig"/>.
    /// </summary>
    public class StatusEffects : MonoBehaviour
    {
        [Tooltip("Immune to hard crowd control: Choáng, Đóng Băng, Trói (e.g. while shielded).")]
        public bool stunImmune;
        [Tooltip("Multiplier applied to stun durations (bosses resist).")]
        public float stunResist = 1f;
        /// <summary>A frozen character holds its pose here. Off on a client's copy of an enemy: the server's animation speed arrives with it.</summary>
        [System.NonSerialized] public bool drivesAnimation = true;

        /// <summary>The clock of every status; tests pin it.</summary>
        public static Func<float> Clock = () => Time.time;

        float Now => Clock();

        // ------------------------------------------------------------------ state for movers, AI and UI
        /// <summary>Cannot act: stunned or frozen.</summary>
        public bool IsStunned => Now < stunUntil || IsFrozen;
        public bool IsFrozen => Now < freezeUntil;
        public bool IsRooted => Now < rootUntil;
        public bool IsSlowed => Now < slowUntil;
        public bool IsBurning => burnStacks.Count > 0 && Now < burnUntil;
        public bool IsPoisoned => poisonStacks > 0 && Now < poisonUntil;
        public bool IsCursed => Now < curseUntil;
        public bool IsJudged => Now < judgmentUntil;
        public int BurnStacks => IsBurning ? burnStacks.Count : 0;
        public int ChillStacks => Now < chillUntil ? chillStacks : 0;
        public int ChargeStacks => Now < chargeUntil ? chargeStacks : 0;
        public int PoisonStacks => IsPoisoned ? poisonStacks : 0;
        public float StunRemaining => Mathf.Max(0f, Mathf.Max(stunUntil, freezeUntil) - Now);
        /// <summary>A boss or elite: shorter freezes, crowd-control immunity after a stun.</summary>
        public bool IsHeavy => Poise != null;
        /// <summary>A boss or elite shrugging off crowd control after a stun or freeze.</summary>
        public bool CrowdControlImmune => IsHeavy && Now < ccImmuneUntil;

        /// <summary>Burn damage per second of all stacks.</summary>
        public float BurnDps
        {
            get
            {
                if (!IsBurning) return 0f;
                float sum = 0f;
                foreach (var s in burnStacks) sum += s;
                return sum;
            }
        }

        float ChillMultiplier => 1f - CombatConfig.Current.chillSlowPerStack * ChillStacks;

        /// <summary>Movement: 0 while frozen or rooted, lowered by Làm Chậm and Lạnh.</summary>
        public float SpeedMultiplier => IsFrozen || IsRooted ? 0f : (IsSlowed ? 1f - slowAmount : 1f) * ChillMultiplier;
        /// <summary>Attack speed: lowered by Lạnh (enemies' attack cooldowns, the hero's basic attack).</summary>
        public float AttackSpeedMultiplier => ChillMultiplier;
        /// <summary>Damage this character takes: Nguyền +15%, Phán Xét +20%.</summary>
        public float DamageTakenMultiplier
        {
            get
            {
                var c = CombatConfig.Current;
                return 1f + (IsCursed ? c.curseDamageTaken : 0f) + (IsJudged ? c.judgmentDamageTaken : 0f);
            }
        }
        /// <summary>Damage this character deals: Nguyền −20%.</summary>
        public float DamageDealtMultiplier => IsCursed ? 1f - CombatConfig.Current.curseDamageDealt : 1f;

        float stunUntil, freezeUntil, rootUntil, slowUntil, curseUntil, judgmentUntil;
        float slowAmount;
        readonly List<float> burnStacks = new List<float>(3);   // damage per second of each stack
        float burnUntil, nextBurnTick;
        Team burnTeam;
        int poisonStacks;
        float poisonUntil, nextPoisonTick;
        Team poisonTeam;
        int chillStacks, chargeStacks;
        float chillUntil, chargeUntil;
        int ccRepeats;
        float ccRepeatUntil, ccImmuneUntil;

        Health health;
        Poise poise;
        HitFlash flash;
        SpriteAnimator anim;
        CharacterMotor motor;
        bool ready;
        GameObject stunFx, burnFx;
        float animSpeedBeforeFreeze = 1f;
        bool animFrozen, wasFrozen;
        static readonly List<Health> Nearby = new List<Health>(16);

        Poise Poise
        {
            get
            {
                Ready();
                return poise;
            }
        }

        void Awake() => Ready();

        /// <summary>Finds the neighbour components (in Awake, or on first use when made in a test).</summary>
        void Ready()
        {
            if (ready) return;
            ready = true;
            health = GetComponent<Health>();
            poise = GetComponent<Poise>();
            flash = GetComponentInChildren<HitFlash>();
            anim = GetComponentInChildren<SpriteAnimator>();
            motor = GetComponent<CharacterMotor>();
            if (motor != null) motor.WallSlammed += OnWallSlam;
        }

        void OnDestroy()
        {
            if (motor != null) motor.WallSlammed -= OnWallSlam;
        }

        // ------------------------------------------------------------------ applying
        /// <summary>
        /// Applies what a hit carries. <paramref name="dealt"/> is the hit's damage on the attacker's
        /// side, before this character's armor and resistances: Bỏng and Tích Điện scale with it.
        /// </summary>
        public void Apply(DamageInfo d, float dealt)
        {
            var s = d.status;
            var c = CombatConfig.Current;
            if (s.burn > 0) Burn(s.burn, dealt * c.burnShare, d.sourceTeam);
            if (s.chill > 0) Chill(s.chill);
            if (s.poison > 0) Poison(s.poison, d.sourceTeam);
            if (s.stun > 0) Stun(s.stun);
            if (s.root > 0) Root(s.root);
            if (s.slow > 0) Slow(s.slow, s.slowDuration > 0 ? s.slowDuration : c.slowSeconds);
            if (s.curse > 0) Curse(s.curse);
            if (s.judgment > 0) Judge(s.judgment);
            if (s.charge > 0) Charge(s.charge, dealt, d.sourceTeam, d.source);   // last: a discharge hits others
        }

        /// <summary>Bỏng: <paramref name="stacks"/> stacks of <paramref name="dpsPerStack"/>; when full, a stronger stack replaces the weakest. Refreshes the timer.</summary>
        public void Burn(int stacks, float dpsPerStack, Team team)
        {
            Ready();
            if (stacks <= 0 || dpsPerStack <= 0f) return;
            var c = CombatConfig.Current;
            if (!IsBurning)
            {
                burnStacks.Clear();
                nextBurnTick = Now + c.tickInterval;
            }
            for (int i = 0; i < stacks; i++)
            {
                if (burnStacks.Count < c.burnMaxStacks)
                {
                    burnStacks.Add(dpsPerStack);
                    continue;
                }
                int weakest = 0;
                for (int k = 1; k < burnStacks.Count; k++)
                    if (burnStacks[k] < burnStacks[weakest]) weakest = k;
                if (burnStacks[weakest] < dpsPerStack) burnStacks[weakest] = dpsPerStack;
            }
            burnUntil = Now + c.burnSeconds;
            burnTeam = team;
        }

        /// <summary>Lạnh: adds stacks and refreshes them; the 4th stack turns into Đóng Băng.</summary>
        public void Chill(int stacks)
        {
            Ready();
            if (stacks <= 0) return;
            var c = CombatConfig.Current;
            int have = ChillStacks + stacks;
            chillUntil = Now + c.chillSeconds;
            if (have < c.chillMaxStacks)
            {
                chillStacks = have;
                return;
            }
            chillStacks = 0;
            chillUntil = 0f;
            Freeze();
        }

        void Freeze()
        {
            var c = CombatConfig.Current;
            if (IsHeavy) poise.AddPoise(c.heavyFreezePoise);   // plan: boss 0.6 s + 60 Trấn Áp
            if (stunImmune) return;
            float seconds = CrowdControl(IsHeavy ? c.heavyFreezeSeconds : c.freezeSeconds);
            if (seconds <= 0f) return;
            freezeUntil = Mathf.Max(freezeUntil, Now + seconds);
            if (IsHeavy) ccImmuneUntil = Mathf.Max(ccImmuneUntil, freezeUntil + c.heavyCrowdControlImmunity);
            if (health != null) NetCues.WorldText("Đóng Băng!", health.HeadPosition + Vector3.up * 0.3f, Palette.Ice);
            NetCues.Vfx("ice_spike", transform.position, 0f, 0.9f);
            NetCues.Sound("sfx_ice_cast", 0.6f, 0.05f, transform.position);
        }

        /// <summary>Tích Điện: adds stacks; the 3rd discharges <c>dischargeShare</c> of <paramref name="dealt"/> to the nearest others.</summary>
        public void Charge(int stacks, float dealt, Team team, GameObject source)
        {
            Ready();
            if (stacks <= 0) return;
            var c = CombatConfig.Current;
            int have = ChargeStacks + stacks;
            chargeUntil = Now + c.chargeSeconds;
            if (have < c.chargeMaxStacks)
            {
                chargeStacks = have;
                return;
            }
            chargeStacks = 0;
            chargeUntil = 0f;
            Discharge(dealt * c.dischargeShare, team, source);
        }

        void Discharge(float amount, Team team, GameObject source)
        {
            var c = CombatConfig.Current;
            Vector2 at = transform.position;
            NetCues.Vfx("hit_lightning", (Vector3)at + Vector3.up * 0.5f, 0f, 1.4f);
            NetCues.Sound("sfx_thunder", 0.5f, 0.1f, at);
            if (health != null) NetCues.WorldText("Phóng Điện!", health.HeadPosition + Vector3.up * 0.3f, Palette.Lightning);
            if (amount <= 0f) return;
            Util.HealthsInCircle(at, c.dischargeRadius, team, Nearby);
            Nearby.Remove(health);
            Nearby.Sort((a, b) => ((Vector2)a.transform.position - at).sqrMagnitude.CompareTo(((Vector2)b.transform.position - at).sqrMagnitude));
            int n = Mathf.Min(c.dischargeTargets, Nearby.Count);
            for (int i = 0; i < n; i++)
            {
                var h = Nearby[i];
                if (h == null || h.IsDead) continue;
                Vector2 p = h.transform.position;
                var hit = DamageInfo.Make(amount, team, source, p, p - at, DamageType.Lightning, 1.5f);
                hit.attackScaled = true;
                hit.skillName = "Phóng Điện";
                NetCues.Vfx("lightning_strike", p, 0f, 0.6f);
                if (h.TakeDamage(hit) > 0) Combat.OnHitFeedback(h, hit);
            }
        }

        /// <summary>Độc: adds stacks (up to 5) and refreshes them.</summary>
        public void Poison(int stacks, Team team)
        {
            Ready();
            if (stacks <= 0) return;
            var c = CombatConfig.Current;
            if (!IsPoisoned)
            {
                poisonStacks = 0;
                nextPoisonTick = Now + c.tickInterval;
                NetCues.Vfx("spore_puff", transform.position + Vector3.up * 0.6f, 0f, 0.8f);
            }
            poisonStacks = Mathf.Min(c.poisonMaxStacks, poisonStacks + stacks);
            poisonUntil = Now + c.poisonSeconds;
            poisonTeam = team;
        }

        /// <summary>Choáng, with resistance, immunity and diminishing returns.</summary>
        public void Stun(float seconds)
        {
            Ready();
            if (stunImmune || seconds <= 0f) return;
            seconds = CrowdControl(seconds * stunResist);
            if (seconds > 0f) ApplyStun(seconds);
        }

        /// <summary>A stun that ignores resistance, immunity and diminishing returns (poise breaks, crashing into a boulder).</summary>
        public void ForceStun(float seconds)
        {
            Ready();
            if (seconds > 0f) ApplyStun(seconds);
        }

        void ApplyStun(float seconds)
        {
            bool was = Now < stunUntil;
            stunUntil = Mathf.Max(stunUntil, Now + seconds);
            if (IsHeavy) ccImmuneUntil = Mathf.Max(ccImmuneUntil, stunUntil + CombatConfig.Current.heavyCrowdControlImmunity);
            if (!was && health != null)
            {
                NetCues.WorldText("Choáng!", health.HeadPosition + Vector3.up * 0.3f, Palette.Status);
                NetCues.Sound("sfx_stun", 0.7f, 0.05f, transform.position);
            }
        }

        /// <summary>Ends a stun or freeze in progress (Khiên Thánh).</summary>
        public void ClearStun() => stunUntil = freezeUntil = 0f;

        /// <summary>Trói: cannot move (no dashes either), can still attack and cast.</summary>
        public void Root(float seconds)
        {
            Ready();
            if (stunImmune || seconds <= 0f) return;
            seconds = CrowdControl(seconds);
            if (seconds <= 0f) return;
            bool was = IsRooted;
            rootUntil = Mathf.Max(rootUntil, Now + seconds);
            if (was) return;
            if (health != null) NetCues.WorldText("Trói!", health.HeadPosition + Vector3.up * 0.3f, Palette.Status);
            NetCues.Vfx("step_dust", transform.position, 0f, 1.6f);
        }

        /// <summary>Làm Chậm: the strongest slow wins; the longest lasts.</summary>
        public void Slow(float amount, float seconds)
        {
            Ready();
            slowAmount = Mathf.Max(IsSlowed ? slowAmount : 0f, Mathf.Clamp01(amount));
            slowUntil = Mathf.Max(slowUntil, Now + seconds);
        }

        /// <summary>Nguyền: +15% damage taken, −20% damage dealt.</summary>
        public void Curse(float seconds)
        {
            Ready();
            if (seconds <= 0f) return;
            bool was = IsCursed;
            curseUntil = Mathf.Max(curseUntil, Now + seconds);
            if (!was && health != null) NetCues.WorldText("Nguyền!", health.HeadPosition + Vector3.up * 0.3f, Palette.Dark);
        }

        /// <summary>Phán Xét: +20% damage taken from every source.</summary>
        public void Judge(float seconds)
        {
            Ready();
            if (seconds <= 0f) return;
            bool was = IsJudged;
            judgmentUntil = Mathf.Max(judgmentUntil, Now + seconds);
            if (!was && health != null) NetCues.WorldText("Phán Xét!", health.HeadPosition + Vector3.up * 0.3f, Palette.Holy);
        }

        /// <summary>Removes every status (Thuốc Thảo Mộc, respawn).</summary>
        public void Cleanse()
        {
            stunUntil = freezeUntil = rootUntil = slowUntil = curseUntil = judgmentUntil = 0f;
            burnUntil = poisonUntil = chillUntil = chargeUntil = 0f;
            burnStacks.Clear();
            poisonStacks = chillStacks = chargeStacks = 0;
        }

        /// <summary>
        /// The duration hard crowd control really gets: 40% less for each earlier one within the
        /// last 6 s, none while a boss or elite is immune.
        /// </summary>
        float CrowdControl(float seconds)
        {
            var c = CombatConfig.Current;
            if (seconds <= 0f || CrowdControlImmune) return 0f;
            ccRepeats = Now < ccRepeatUntil ? ccRepeats + 1 : 0;
            ccRepeatUntil = Now + c.crowdControlRepeatWindow;
            return seconds * Mathf.Pow(c.crowdControlRepeatFactor, ccRepeats);
        }

        /// <summary>Đẩy Lùi into a wall (the motor reports it): a short stun and a puff of dust.</summary>
        void OnWallSlam(Vector2 point)
        {
            if (health != null && health.IsDead) return;
            NetCues.Vfx("rock_chips", point);
            NetCues.Vfx("step_dust", point, 0f, 1.4f);
            NetCues.Sound("sfx_hit_heavy", 0.6f, 0.1f, point);
            NetCues.Shake(0.12f, point);
            Stun(CombatConfig.Current.wallSlamStun);
        }

        // ------------------------------------------------------------------ online
        static byte Tenths(float seconds) => (byte)Mathf.Clamp(Mathf.CeilToInt(seconds * 10f), 0, 255);

        /// <summary>What screens need to know (server side of online play): times left, stacks, flags.</summary>
        public StatusView CaptureView()
        {
            Ready();
            float now = Now;
            byte flags = 0;
            if (IsCursed) flags |= 1;
            if (IsJudged) flags |= 2;
            if (CrowdControlImmune) flags |= 4;
            return new StatusView
            {
                stun = Tenths(stunUntil - now),
                freeze = Tenths(freezeUntil - now),
                root = Tenths(rootUntil - now),
                slow = (byte)(IsSlowed ? Mathf.RoundToInt(slowAmount * 100f) : 0),
                slowLeft = Tenths(slowUntil - now),
                chill = (byte)ChillStacks,
                charge = (byte)ChargeStacks,
                burn = (byte)BurnStacks,
                poison = (byte)PoisonStacks,
                flags = flags
            };
        }

        /// <summary>
        /// A client's copy of a character takes the server's statuses as they are: it shows them
        /// (tint, stars, frozen pose) and moves by them, but deals no damage over time itself.
        /// </summary>
        public void ApplyView(StatusView v)
        {
            Ready();
            float now = Now;
            float Until(byte tenths) => tenths > 0 ? now + tenths / 10f : 0f;
            stunUntil = Until(v.stun);
            freezeUntil = Until(v.freeze);
            rootUntil = Until(v.root);
            slowAmount = v.slow / 100f;
            slowUntil = v.slow > 0 ? Until(v.slowLeft) : 0f;
            // stacks without timers of their own here: held until the next word from the server
            const float hold = 0.6f;
            chillStacks = v.chill;
            chillUntil = v.chill > 0 ? now + hold : 0f;
            chargeStacks = v.charge;
            chargeUntil = v.charge > 0 ? now + hold : 0f;
            while (burnStacks.Count > v.burn) burnStacks.RemoveAt(burnStacks.Count - 1);
            while (burnStacks.Count < v.burn) burnStacks.Add(0f);
            burnUntil = v.burn > 0 ? now + hold : 0f;
            poisonStacks = v.poison;
            poisonUntil = v.poison > 0 ? now + hold : 0f;
            curseUntil = (v.flags & 1) != 0 ? now + hold : 0f;
            judgmentUntil = (v.flags & 2) != 0 ? now + hold : 0f;
            ccImmuneUntil = (v.flags & 4) != 0 ? now + hold : 0f;
        }

        // ------------------------------------------------------------------ ticking
        void Update() => Tick();

        /// <summary>Damage ticks, expiry and visuals. Called every frame (tests call it after moving the clock).</summary>
        public void Tick()
        {
            Ready();
            if (health != null && health.IsDead)
            {
                // nothing carries over to a revived enemy or a respawned hero
                Cleanse();
                SetFx(ref stunFx, "stun_stars", false);
                SetFx(ref burnFx, "burning", false);
                SetAnimFrozen(false);
                wasFrozen = false;
                if (motor != null) motor.Rooted = false;
                return;
            }
            var c = CombatConfig.Current;
            float now = Now;
            // damage over time is the world's rule: a client's copy only shows the statuses
            bool rules = GameSession.IsAuthority;
            // Bỏng: every tick until the timer, the last one included
            while (rules && burnStacks.Count > 0 && nextBurnTick <= now && nextBurnTick <= burnUntil + 0.001f && !Dead)
            {
                DotTick(BurnDpsRaw() * c.tickInterval, DamageType.Fire, burnTeam);
                nextBurnTick += c.tickInterval;
            }
            if (burnStacks.Count > 0 && now >= burnUntil) burnStacks.Clear();
            // Độc: a share of max HP per stack
            while (rules && poisonStacks > 0 && nextPoisonTick <= now && nextPoisonTick <= poisonUntil + 0.001f && !Dead)
            {
                float maxHp = health != null ? health.maxHp : 0f;
                DotTick(maxHp * c.poisonMaxHpPerSecond * poisonStacks * c.tickInterval, DamageType.Poison, poisonTeam);
                nextPoisonTick += c.tickInterval;
            }
            if (poisonStacks > 0 && now >= poisonUntil) poisonStacks = 0;

            bool frozen = IsFrozen;
            if (wasFrozen && !frozen) AudioManager.Play("sfx_ice_shatter", 0.5f, 0.1f, transform.position);
            wasFrozen = frozen;
            if (motor != null) motor.Rooted = IsRooted || frozen;
            SetFx(ref stunFx, "stun_stars", now < stunUntil);
            SetFx(ref burnFx, "burning", IsBurning);
            SetAnimFrozen(frozen);
            Tint(now);
        }

        bool Dead => health != null && health.IsDead;

        float BurnDpsRaw()
        {
            float sum = 0f;
            foreach (var s in burnStacks) sum += s;
            return sum;
        }

        void DotTick(float amount, DamageType type, Team team)
        {
            if (amount <= 0f || health == null) return;
            var d = DamageInfo.Make(amount, team, null, transform.position, Vector2.up, type);
            d.attackScaled = true;   // already worked out from a hit or from max HP
            d.dot = true;
            health.TakeDamage(d);
        }

        /// <summary>One tint at a time, the most telling first: frozen, burning, poisoned, chilled or slowed, cursed, judged.</summary>
        void Tint(float now)
        {
            if (flash == null) return;
            if (IsFrozen) flash.SetTint(Palette.Ice, 0.6f);
            else if (IsBurning) flash.SetTint(Palette.Fire, 0.18f + 0.1f * Mathf.Sin(now * 18f));
            else if (IsPoisoned) flash.SetTint(Palette.Poison, 0.2f + 0.08f * Mathf.Sin(now * 6f));
            else if (ChillStacks > 0) flash.SetTint(Palette.Ice, 0.12f + 0.08f * ChillStacks);
            else if (IsSlowed) flash.SetTint(Palette.Ice, 0.35f);
            else if (ChargeStacks > 0) flash.SetTint(Palette.Lightning, 0.12f + 0.12f * Mathf.Abs(Mathf.Sin(now * 25f)));
            else if (IsCursed) flash.SetTint(Palette.Dark, 0.25f);
            else if (IsJudged) flash.SetTint(Palette.Holy, 0.25f);
            else flash.SetTint(Color.white, 0f);
        }

        /// <summary>A frozen character holds its pose.</summary>
        void SetAnimFrozen(bool on)
        {
            if (anim == null || on == animFrozen || !drivesAnimation) return;
            animFrozen = on;
            if (on)
            {
                animSpeedBeforeFreeze = anim.speed;
                anim.speed = 0f;
            }
            else anim.speed = animSpeedBeforeFreeze;
        }

        void SetFx(ref GameObject fx, string id, bool on)
        {
            if (on && fx == null)
            {
                var anchor = health != null && health.head != null ? health.head : transform;
                Vector3 pos = id == "stun_stars" ? anchor.position + Vector3.up * 0.2f : transform.position + Vector3.up * 0.4f;
                fx = VFX.Spawn(id, pos, Quaternion.identity, 1f, id == "stun_stars" ? anchor : transform, true);
            }
            else if (!on && fx != null)
            {
                VFX.Release(fx);
                fx = null;
            }
        }

        void OnDisable()
        {
            if (stunFx != null) { VFX.Release(stunFx); stunFx = null; }
            if (burnFx != null) { VFX.Release(burnFx); burnFx = null; }
            SetAnimFrozen(false);
            if (motor != null) motor.Rooted = false;
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// The statuses of plan §04 without Play Mode: stacks and caps, damage ticks, Đóng Băng, the
    /// damage multipliers and diminishing returns on crowd control. The clock of StatusEffects is
    /// pinned and moved by hand; Tick() runs what Update would each frame.
    /// </summary>
    public class StatusEffectsTests
    {
        float now;
        System.Func<float> savedClock, savedRoll;
        readonly List<GameObject> made = new List<GameObject>();
        CombatConfig c;

        [SetUp]
        public void SetUp()
        {
            savedClock = StatusEffects.Clock;
            savedRoll = Health.SpreadRoll;
            now = 100f;
            StatusEffects.Clock = () => now;
            Health.SpreadRoll = () => 0.5f;   // no random spread: exact numbers
            c = CombatConfig.Current;
        }

        [TearDown]
        public void TearDown()
        {
            StatusEffects.Clock = savedClock;
            Health.SpreadRoll = savedRoll;
            foreach (var go in made)
                if (go != null) Object.DestroyImmediate(go);
            made.Clear();
        }

        StatusEffects Target(float hp = 1000f, bool heavy = false, Team team = Team.Enemy)
        {
            var go = new GameObject("status target");
            made.Add(go);
            var h = go.AddComponent<Health>();
            h.team = team;
            h.maxHp = h.hp = hp;
            var s = go.AddComponent<StatusEffects>();
            if (heavy) go.AddComponent<Poise>();
            return s;
        }

        /// <summary>Moves the clock forward in frames of 0.05 s, ticking the targets.</summary>
        void Advance(float seconds, params StatusEffects[] targets)
        {
            float end = now + seconds;
            while (now < end - 1e-4f)
            {
                now = Mathf.Min(end, now + 0.05f);
                foreach (var t in targets) t.Tick();
            }
        }

        static DamageInfo Hit(float amount, Team team = Team.Player, DamageType type = DamageType.Physical)
        {
            var d = DamageInfo.Make(amount, team, null, Vector2.zero, Vector2.up, type);
            d.attackScaled = true;
            return d;
        }

        [Test]
        public void BurnTakesThirtyPercentOfTheHitPerSecondInUpToThreeStacks()
        {
            var s = Target();
            var h = s.GetComponent<Health>();
            var d = Hit(100f, type: DamageType.Fire);
            d.status.burn = 1;
            Assert.AreEqual(100f, h.TakeDamage(d), 0.01f);
            Assert.AreEqual(1, s.BurnStacks, "the hit set a stack");
            Assert.AreEqual(30f, s.BurnDps, 1e-3f, "30% of the hit per second");
            Advance(c.burnSeconds + 0.3f, s);
            Assert.AreEqual(900f - 90f, h.hp, 0.01f, "3 s × 30");
            Assert.AreEqual(0, s.BurnStacks, "burnt out");

            s.Burn(5, 10f, Team.Player);
            Assert.AreEqual(3, s.BurnStacks, "3 stacks at most");
            s.Burn(1, 50f, Team.Player);
            Assert.AreEqual(70f, s.BurnDps, 1e-3f, "a stronger stack replaces the weakest");
            Advance(2f, s);
            s.Burn(1, 5f, Team.Player);
            Assert.AreEqual(70f, s.BurnDps, 1e-3f, "a weaker one only refreshes the timer");
            Advance(2f, s);
            Assert.AreEqual(3, s.BurnStacks, "refreshed: still burning 4 s after the first stack");
        }

        [Test]
        public void ChillSlowsAndTheFourthStackFreezes()
        {
            var s = Target();
            s.Chill(1);
            Assert.AreEqual(1f - c.chillSlowPerStack, s.SpeedMultiplier, 1e-4f, "−12% move speed");
            Assert.AreEqual(1f - c.chillSlowPerStack, s.AttackSpeedMultiplier, 1e-4f, "−12% attack speed");
            s.Chill(2);
            Assert.AreEqual(3, s.ChillStacks);
            Assert.AreEqual(1f - 3 * c.chillSlowPerStack, s.SpeedMultiplier, 1e-4f);
            Assert.IsFalse(s.IsFrozen);

            s.Chill(1);
            Assert.IsTrue(s.IsFrozen, "the 4th stack freezes");
            Assert.IsTrue(s.IsStunned, "frozen: cannot act");
            Assert.AreEqual(0f, s.SpeedMultiplier, "nor move");
            Assert.AreEqual(0, s.ChillStacks, "the stacks turned into the freeze");
            Assert.AreEqual(c.freezeSeconds, s.StunRemaining, 1e-3f);
            Advance(c.freezeSeconds + 0.1f, s);
            Assert.IsFalse(s.IsFrozen);

            s.Chill(2);
            Advance(c.chillSeconds + 0.1f, s);
            Assert.AreEqual(0, s.ChillStacks, "stacks wear off");
        }

        [Test]
        public void BossesFreezeBrieflyGainPoiseAndShrugOffCrowdControlAfterAStun()
        {
            var s = Target(heavy: true);
            var poise = s.GetComponent<Poise>();
            s.Chill(4);
            Assert.IsTrue(s.IsFrozen);
            Assert.AreEqual(c.heavyFreezeSeconds, s.StunRemaining, 1e-3f, "boss: 0.6 s");
            Assert.AreEqual(c.heavyFreezePoise, poise.Current, 1e-3f, "+60 Trấn Áp");

            Advance(c.heavyFreezeSeconds + 0.1f, s);
            Assert.IsFalse(s.IsStunned);
            Assert.IsTrue(s.CrowdControlImmune, "immune after the freeze");
            s.Stun(1f);
            s.Root(1f);
            Assert.IsFalse(s.IsStunned, "no stun while immune");
            Assert.IsFalse(s.IsRooted, "no root either");
            s.ForceStun(3f);
            Assert.IsTrue(s.IsStunned, "a poise break still stuns");

            Advance(3f + c.heavyCrowdControlImmunity + 0.1f, s);
            Assert.IsFalse(s.CrowdControlImmune, "4 s after the stun ended");
            s.Stun(1f);
            Assert.AreEqual(1f, s.StunRemaining, 1e-3f, "crowd control works again, at full length");
        }

        [Test]
        public void CrowdControlLastsFortyPercentShorterEachTimeWithinSixSeconds()
        {
            var s = Target();
            s.Stun(1f);
            Assert.AreEqual(1f, s.StunRemaining, 1e-4f);
            Advance(2f, s);
            s.Stun(1f);
            Assert.AreEqual(0.6f, s.StunRemaining, 1e-4f, "the second time");
            Advance(1f, s);
            s.Root(1f);
            Assert.IsTrue(s.IsRooted);
            Assert.AreEqual(0f, s.SpeedMultiplier, "rooted: cannot move");
            Assert.IsFalse(s.IsStunned, "but can act");
            Advance(0.3f, s);
            Assert.IsTrue(s.IsRooted, "the third lasts 0.36 s");
            Advance(0.1f, s);
            Assert.IsFalse(s.IsRooted);

            Advance(7f, s);
            s.Stun(1f);
            Assert.AreEqual(1f, s.StunRemaining, 1e-4f, "full length after 6 quiet seconds");

            var resistant = Target();
            resistant.stunResist = 0.5f;
            resistant.Stun(0.8f);
            Assert.AreEqual(0.4f, resistant.StunRemaining, 1e-4f, "stun resistance still halves it");
            var shielded = Target();
            shielded.stunImmune = true;
            shielded.Stun(1f);
            shielded.Root(1f);
            shielded.Chill(4);
            Assert.IsFalse(shielded.IsStunned || shielded.IsRooted || shielded.IsFrozen, "Khiên Thánh stops all of them");
        }

        [Test]
        public void PoisonTakesOneAndAHalfPercentOfMaxHpPerStackPerSecond()
        {
            var s = Target(1000f);
            var h = s.GetComponent<Health>();
            s.Poison(2, Team.Player);
            Assert.AreEqual(2, s.PoisonStacks);
            Advance(c.poisonSeconds + 0.3f, s);
            Assert.AreEqual(1000f - 1000f * 0.015f * 2f * 6f, h.hp, 0.01f, "2 stacks for 6 s");
            Assert.AreEqual(0, s.PoisonStacks, "worn off");

            s.Poison(9, Team.Player);
            Assert.AreEqual(c.poisonMaxStacks, s.PoisonStacks, "5 stacks at most");
        }

        [Test]
        public void ChargeDischargesOnTheThirdStack()
        {
            var s = Target();
            s.Charge(2, 100f, Team.Player, null);
            Assert.AreEqual(2, s.ChargeStacks);
            s.Charge(1, 100f, Team.Player, null);
            Assert.AreEqual(0, s.ChargeStacks, "the 3rd stack discharges (the smoke test checks who it hits)");
            s.Charge(1, 100f, Team.Player, null);
            Advance(c.chargeSeconds + 0.1f, s);
            Assert.AreEqual(0, s.ChargeStacks, "stacks wear off after 5 s");
        }

        [Test]
        public void CurseAndJudgmentChangeTheDamageTakenAndDealt()
        {
            var s = Target(1000f);
            var h = s.GetComponent<Health>();
            s.Curse(8f);
            Assert.AreEqual(1f + c.curseDamageTaken, s.DamageTakenMultiplier, 1e-4f, "Nguyền: +15% taken");
            Assert.AreEqual(1f - c.curseDamageDealt, s.DamageDealtMultiplier, 1e-4f, "−20% dealt");
            s.Judge(5f);
            Assert.AreEqual(135f, h.TakeDamage(Hit(100f)), 0.01f, "Nguyền and Phán Xét: +35%");
            Advance(5.1f, s);
            Assert.IsFalse(s.IsJudged, "Phán Xét lasts 5 s");
            Assert.IsTrue(s.IsCursed, "Nguyền 8 s");

            var hero = Target(1000f, team: Team.Player);
            var d = Hit(100f, Team.Enemy);
            d.source = s.gameObject;   // the cursed one attacks
            Assert.AreEqual(80f, hero.GetComponent<Health>().TakeDamage(d), 0.01f, "a cursed attacker deals 20% less");
            Advance(3f, s);
            Assert.AreEqual(1f, s.DamageTakenMultiplier, 1e-4f, "all over");
        }

        [Test]
        public void SlowKeepsTheStrongestAndTheLongest()
        {
            var s = Target();
            s.Slow(0.3f, 2f);
            s.Slow(0.1f, 4f);
            Assert.AreEqual(0.7f, s.SpeedMultiplier, 1e-4f, "the stronger slow wins");
            Advance(3f, s);
            Assert.IsTrue(s.IsSlowed, "the longer one lasts");
            s.Chill(1);
            Assert.AreEqual(0.7f * (1f - c.chillSlowPerStack), s.SpeedMultiplier, 1e-4f, "Lạnh adds on top");
        }

        [Test]
        public void CleanseRemovesEveryStatus()
        {
            var s = Target();
            s.Burn(2, 10f, Team.Player);
            s.Chill(2);
            s.Charge(1, 50f, Team.Player, null);
            s.Poison(3, Team.Player);
            s.Stun(1f);
            s.Slow(0.3f, 2f);
            s.Curse(8f);
            s.Judge(5f);
            s.Cleanse();
            Assert.IsFalse(s.IsBurning || s.IsPoisoned || s.IsStunned || s.IsSlowed || s.IsCursed || s.IsJudged || s.IsRooted);
            Assert.AreEqual(0, s.ChillStacks + s.ChargeStacks + s.BurnStacks + s.PoisonStacks);
            Assert.AreEqual(1f, s.SpeedMultiplier);
        }

        [Test]
        public void TheSkillsCarryThePlanStatuses()
        {
            var db = AssetFactory.Database;
            var fireball = (ProjectileEffect)db.Ability("fireball").effects[1];
            Assert.AreEqual(1, fireball.hit.status.burn, "Cầu Lửa: 1 tầng Bỏng (plan §06)");
            var iceLine = (LineEffect)db.Ability("ice").effects[1];
            Assert.AreEqual(2, ((DamageEffect)iceLine.each[1]).hit.status.chill, "Mũi Băng: 2 tầng Lạnh");
            var storm = (BurstEffect)db.Ability("lightning").effects[1];
            Assert.AreEqual(0.8f, ((DamageEffect)storm.each[1]).hit.status.stun, 1e-4f, "Lôi Phạt: Choáng 0.8 s");
        }
    }
}

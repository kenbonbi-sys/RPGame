using NUnit.Framework;
using UnityEngine;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// Combat v2 of plan §04 without Play Mode: the commit windows of the skills (hủy đòn), the
    /// three hit-stop tiers and the one-cast damage multiplier Lướt Hoàn Hảo hands the next skill.
    /// The smoke tests play the same rules in the game scene.
    /// </summary>
    public class CombatFeelTests
    {
        GameObject a, b;

        [TearDown]
        public void TearDown()
        {
            if (a != null) Object.DestroyImmediate(a);
            if (b != null) Object.DestroyImmediate(b);
        }

        [Test]
        public void CommitWindowStaysInsideThePoseAndCoversAllOfATuyetKy()
        {
            var def = ScriptableObject.CreateInstance<AbilityDef>();
            def.lockTime = 0.4f;
            def.commitTime = 0.15f;
            Assert.AreEqual(0.15f, def.CommitTime, 1e-5f);
            def.commitTime = 0.9f;
            Assert.AreEqual(0.4f, def.CommitTime, 1e-5f, "never longer than the pose");
            def.commitTime = 0.05f;
            def.tags = AbilityTags.Lightning | AbilityTags.Ultimate;
            Assert.AreEqual(0.4f, def.CommitTime, 1e-5f, "a Tuyệt kỹ cannot be cancelled");
            Object.DestroyImmediate(def);
        }

        [Test]
        public void TheSkillsCommitWithinTheirPoses()
        {
            var db = AssetFactory.Database;
            Assert.NotNull(db, "Assets/Data/GameDatabase.asset");
            foreach (var s in db.abilities)
            {
                if (s == null) continue;
                Assert.GreaterOrEqual(s.commitTime, 0f, s.id);
                Assert.LessOrEqual(s.commitTime, s.lockTime, s.id + ": commits no longer than its pose");
            }
            var slash = db.Ability("slash");
            var hit = FindDamage(((ComboEffect)slash.effects[0]).stages[0].effects);
            Assert.AreEqual(hit.delay, slash.commitTime, 1e-5f, "Chém Gió commits until its hit frame; a dash cancels the recovery after it");
            var dash = db.Ability("dash");
            Assert.AreEqual(0f, dash.commitTime);
            Assert.IsTrue(((DashEffect)dash.effects[0]).perfectDodge, "Lướt opens the Lướt Hoàn Hảo window");
            Assert.NotNull(db.combat, "GameDatabase.combat is Assets/Data/Combat.asset");
        }

        static DamageEffect FindDamage(System.Collections.Generic.List<AbilityEffect> effects)
        {
            foreach (var e in effects)
                if (e is DamageEffect d) return d;
            Assert.Fail("no Damage block");
            return null;
        }

        Health Target(ref GameObject go, string name)
        {
            go = new GameObject(name);
            var h = go.AddComponent<Health>();
            h.team = Team.Enemy;
            h.maxHp = h.hp = 100f;
            return h;
        }

        static DamageInfo HeroHit(float hitStop)
        {
            var d = DamageInfo.Make(10f, Team.Player, null, Vector2.zero, Vector2.up);
            d.hitStop = hitStop;
            return d;
        }

        [Test]
        public void HitStopHasThreeTiers()
        {
            var c = CombatConfig.Current;
            Assert.AreEqual(0.035f, c.hitStopLight, 1e-5f, "plan §04: 35 ms");
            Assert.AreEqual(0.07f, c.hitStopHeavy, 1e-5f, "70 ms");
            Assert.AreEqual(0.12f, c.hitStopBreak, 1e-5f, "120 ms");

            var slime = Target(ref a, "slime");
            var swing = HeroHit(c.hitStopLight);
            Assert.AreEqual(c.hitStopLight, Combat.HitStopFor(slime, swing), 1e-5f, "an ordinary swing keeps its own");
            var finisher = HeroHit(c.hitStopHeavy);
            Assert.AreEqual(c.hitStopHeavy, Combat.HitStopFor(slime, finisher), 1e-5f, "a combo finisher is heavy");
            swing.crit = true;
            Assert.AreEqual(c.hitStopHeavy, Combat.HitStopFor(slime, swing), 1e-5f, "a crit is heavy");
            var pulse = HeroHit(0f);
            pulse.crit = true;
            Assert.AreEqual(0f, Combat.HitStopFor(slime, pulse), "hits without a hit-stop (storms, pulses) never stutter");
            var enemy = swing;
            enemy.sourceTeam = Team.Enemy;
            Assert.AreEqual(c.hitStopLight, Combat.HitStopFor(slime, enemy), 1e-5f, "enemies' crits keep their own");

            slime.Kill();
            Assert.IsTrue(slime.IsDead);
            Assert.AreEqual(c.hitStopLight, Combat.HitStopFor(slime, pulse), 1e-5f, "every kill gets at least the light tier");
            Assert.AreEqual(c.hitStopHeavy, Combat.HitStopFor(slime, finisher), 1e-5f);

            var boss = Target(ref b, "boss");
            b.AddComponent<Poise>();
            boss.Kill();
            Assert.AreEqual(c.hitStopBreak, Combat.HitStopFor(boss, pulse), 1e-5f, "the killing blow on a boss or elite is a break");
        }

        [Test]
        public void TheCastMultiplierScalesEveryHitOfTheCast()
        {
            var def = ScriptableObject.CreateInstance<AbilityDef>();
            var ctx = new AbilityContext { ability = def, caster = new Caster(), level = 1 };
            Assert.AreEqual(20f * 1.5f, ctx.HitScale(DamageType.Fire), 1e-4f, "Attack × damage bonus");
            ctx.damageMultiplier = CombatConfig.Current.perfectDamageMultiplier;
            Assert.AreEqual(20f * 1.5f * 1.3f, ctx.HitScale(DamageType.Fire), 1e-4f, "the skill after Lướt Hoàn Hảo: +30%");
            Assert.AreEqual(1.3f, ctx.Copy().damageMultiplier, 1e-5f, "blocks that copy the context keep it");
            Object.DestroyImmediate(def);
        }

        /// <summary>Attack 20 and +50% damage; nothing else is needed for HitScale.</summary>
        class Caster : IAbilityCaster
        {
            public MonoBehaviour Runner => null;
            public Team Team => Team.Player;
            public Health Health => null;
            public CharacterMotor Motor => null;
            public StatusEffects Status => null;
            public AfterImageSpawner AfterImages => null;
            public int Level => 1;
            public bool IsDead => false;
            public float Attack(DamageType type) => 20f;
            public float DamageDealt(AbilityDef ability) => 1.5f;
            public void BeginAction(string animBase, Vector2 dir, float lockTime, float moveMul) { }
            public void AddBuff(BuffSpec buff) { }
            public void RemoveBuff(string id) { }
        }
    }
}

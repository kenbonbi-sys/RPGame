using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine;

namespace RPG.EditorTools.Tests
{
    /// <summary>The numbers of plan §05 — run with Window › General › Test Runner › EditMode.</summary>
    public class ProgressionTests
    {
        ProgressionConfig c;

        [SetUp]
        public void SetUp() => c = ScriptableObject.CreateInstance<ProgressionConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(c);

        [TestCase(1, 50)]
        [TestCase(5, 657)]
        [TestCase(10, 1991)]
        [TestCase(20, 6034)]
        [TestCase(30, 11544)]
        [TestCase(39, 17566)]
        public void XpCurveMatchesPlan(int level, int expected) => Assert.AreEqual(expected, c.XpToNext(level));

        [Test]
        public void TotalXpToMaxLevelIsAbout272k() => Assert.AreEqual(272332, c.TotalXpTo(40));

        [Test]
        public void NoXpNeededAtMaxLevel() => Assert.AreEqual(0, c.XpToNext(40));

        [Test]
        public void HealthFollowsTheHitDieAndConstitution()
        {
            Assert.AreEqual(104f, Mathf.Round(c.MaxHp(1, 4.5f, 14)), "a d8 hero with Thể Chất 14: the prototype's 104");
            Assert.AreEqual(175f, Mathf.Round(c.MaxHp(6, 5.5f, 16)), "a d10 hero of level 6 with Thể Chất 16");
            Assert.Greater(c.MaxHp(5, 6.5f, 14), c.MaxHp(5, 3.5f, 14), "a d12 barbarian outlasts a d6 wizard");
            Assert.AreEqual(55.5f, c.MaxEnergy(1, 16), 1e-4f, "the prototype's energy");
        }

        [Test]
        public void KillXp()
        {
            Assert.AreEqual(1280, c.KillXp(6, EnemyRank.Boss, 6), "Gấu Ma cấp 6");
            Assert.AreEqual(16, c.KillXp(2, EnemyRank.Normal, 1), "Slime Rêu cấp 2");
            Assert.AreEqual(64, c.KillXp(2, EnemyRank.Elite, 2));
            Assert.AreEqual(8, c.KillXp(2, EnemyRank.Normal, 7), "5 levels below: 50%");
            Assert.AreEqual(2, c.KillXp(2, EnemyRank.Normal, 10), "8 levels below: 10%");
        }

        [Test]
        public void ArmorReduction()
        {
            Assert.AreEqual(0f, c.ArmorReduction(0, 10));
            Assert.AreEqual(50f / (50f + 50f + 25f), c.ArmorReduction(50, 5), 1e-5f);
        }

        [Test]
        public void StartingStatsKeepPrototypeDamage()
        {
            Assert.AreEqual(1f, c.DamageScale(c.PhysicalAttack(c.referenceScore), false), 1e-5f);
            Assert.AreEqual(1f, c.DamageScale(c.MagicAttack(c.referenceScore), true), 1e-5f);
            Assert.Greater(c.DamageScale(c.PhysicalAttack(c.referenceScore + 1), false), 1f);
            Assert.AreEqual(3, CoreStats.Modifier(16), "D&D: 16 is +3");
            Assert.AreEqual(-1, CoreStats.Modifier(8), "8 is −1");
            Assert.AreEqual(0, CoreStats.Modifier(11));
        }
    }

    public class StatBlockTests
    {
        [Test]
        public void FlatThenPercentAddThenPercentMult()
        {
            var b = new StatBlock();
            b.SetBase(StatId.MaxHp, 100);
            b.Add(new StatModifier(StatId.MaxHp, ModKind.Flat, 20));
            b.Add(new StatModifier(StatId.MaxHp, ModKind.PercentAdd, 0.1f));
            b.Add(new StatModifier(StatId.MaxHp, ModKind.PercentAdd, 0.1f));
            b.Add(new StatModifier(StatId.MaxHp, ModKind.PercentMult, 0.5f));
            Assert.AreEqual(120f * 1.2f * 1.5f, b.Get(StatId.MaxHp), 1e-4f);
        }

        [Test]
        public void TaggedModifiersOnlyCountForTheirTag()
        {
            var b = new StatBlock();
            b.SetBase(StatId.MagicAttack, 10);
            b.Add(new StatModifier(StatId.MagicAttack, ModKind.PercentAdd, 0.2f, null, "#Lửa"));
            Assert.AreEqual(10f, b.Get(StatId.MagicAttack), 1e-4f);
            Assert.AreEqual(12f, b.Get(StatId.MagicAttack, "#Lửa"), 1e-4f);
            Assert.AreEqual(10f, b.Get(StatId.MagicAttack, "#Băng"), 1e-4f);
        }

        [Test]
        public void RemoveFromSource()
        {
            var b = new StatBlock();
            var ring = new object();
            b.Add(new StatModifier(StatId.Armor, ModKind.Flat, 5, ring));
            b.Add(new StatModifier(StatId.Armor, ModKind.Flat, 3));
            int changes = 0;
            b.Changed += () => changes++;
            Assert.AreEqual(1, b.RemoveFrom(ring));
            Assert.AreEqual(3f, b.Get(StatId.Armor));
            Assert.AreEqual(1, changes);
        }
    }

    public class ControlsTests
    {
        [TearDown]
        public void TearDown() => InputReader.ResetBindings();

        [Test]
        public void DefaultSkillKeysFollowTheReferenceLayout()
        {
            string[] expected = { "Q", "W", "E", "R", "A", "S", "D", "Space" };
            for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], InputReader.SkillLabel(i));
        }

        [Test]
        public void RemappedKeysShowInLabelsAndSurviveAReload()
        {
            var fireball = InputReader.Asset.FindAction("Gameplay/Skill2", true);
            fireball.ApplyBindingOverride("<Keyboard>/z");
            InputReader.SaveBindingOverrides();
            Assert.AreEqual("Z", InputReader.SkillLabel(1));
            StringAssert.Contains("<Keyboard>/z", UnityEngine.PlayerPrefs.GetString("rtt.controls.overrides"));
            InputReader.ResetBindings();
            Assert.AreEqual("W", InputReader.SkillLabel(1));
        }
    }
}

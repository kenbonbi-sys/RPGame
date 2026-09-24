using NUnit.Framework;
using UnityEngine;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// The damage formula of plan §04 on the defender's side (armor, per-element resistance and
    /// weakness, the random spread) and the tagged damage bonus on the attacker's side.
    /// Plain EditMode tests: no scene, no Play Mode.
    /// </summary>
    public class DamageFormulaTests
    {
        ProgressionConfig c;
        GameObject target;
        System.Func<float> savedRoll;

        [SetUp]
        public void SetUp()
        {
            c = ScriptableObject.CreateInstance<ProgressionConfig>();
            savedRoll = Health.SpreadRoll;
            Health.SpreadRoll = () => 0.5f;   // spread ×1: exact numbers
        }

        [TearDown]
        public void TearDown()
        {
            Health.SpreadRoll = savedRoll;
            Object.DestroyImmediate(c);
            if (target != null) Object.DestroyImmediate(target);
        }

        Health Target(float hp)
        {
            target = new GameObject("damage target");
            var h = target.AddComponent<Health>();
            h.team = Team.Enemy;
            h.maxHp = hp;
            h.hp = hp;
            return h;
        }

        /// <summary>A hit that already carries the attacker's Attack, like every ability hit.</summary>
        static DamageInfo Hit(float amount, DamageType type = DamageType.Physical)
        {
            var d = DamageInfo.Make(amount, Team.Player, null, Vector2.zero, Vector2.up, type);
            d.attackScaled = true;
            return d;
        }

        [Test]
        public void PlanExampleFireballOnAMushroom()
        {
            // Cầu Lửa 140% · Công 40 → 56 · Nấm Độc yếu Lửa (−30%) → 73 · giáp 10, người đánh cấp 5 → ≈ 64
            float hit = 1.4f * 40f;
            Assert.AreEqual(56f, hit, 1e-4f);
            Assert.AreEqual(73f, Mathf.Round(c.Mitigate(hit, 0f, 5, -0.3f, 0.5f)));
            Assert.AreEqual(64f, Mathf.Round(c.Mitigate(hit, 10f, 5, -0.3f, 0.5f)));
        }

        [Test]
        public void ResistanceStaysBetweenWeaknessFloorAndCap()
        {
            Assert.AreEqual(1.3f, c.ResistMultiplier(-0.3f), 1e-5f);
            Assert.AreEqual(1.5f, c.ResistMultiplier(-0.9f), 1e-5f, "weakness floor −50%");
            Assert.AreEqual(0.25f, c.ResistMultiplier(0.9f), 1e-5f, "resist cap 75%");
        }

        [Test]
        public void SpreadIsPlusOrMinusFivePercent()
        {
            Assert.AreEqual(0.95f, c.Spread(0f), 1e-5f);
            Assert.AreEqual(1f, c.Spread(0.5f), 1e-5f);
            Assert.AreEqual(1.05f, c.Spread(1f), 1e-5f);
            c.damageSpread = 0f;
            Assert.AreEqual(1f, c.Spread(0.9f), 1e-5f, "spread 0 turns it off");
        }

        [Test]
        public void HealthAppliesWeaknessAndResistance()
        {
            var h = Target(10000f);
            h.resistances.fire = -0.3f;
            h.resistances.ice = 0.5f;
            Assert.AreEqual(130f, h.TakeDamage(Hit(100, DamageType.Fire)), "weak to fire: +30%");
            Assert.AreEqual(50f, h.TakeDamage(Hit(100, DamageType.Ice)), "resists ice: −50%");
            Assert.AreEqual(100f, h.TakeDamage(Hit(100, DamageType.Lightning)));
            Assert.AreEqual(DamageType.Fire, h.resistances.Weakness);
        }

        [Test]
        public void AllElementResistAddsUpAndIsCapped()
        {
            var h = Target(100f);
            h.resistances.fire = -0.3f;
            h.resistances.ice = 0.5f;
            h.elementalResist = 0.5f;   // what Vitality gives the hero, for every element
            Assert.AreEqual(0.2f, h.Resistance(DamageType.Fire), 1e-5f);
            Assert.AreEqual(0.75f, h.Resistance(DamageType.Ice), 1e-5f, "capped at 75%");
            Assert.AreEqual(0f, h.Resistance(DamageType.Physical), 1e-5f, "armor, not elemental resist, covers physical");
            h.resistances.poison = -0.9f;
            h.elementalResist = 0f;
            Assert.AreEqual(-0.5f, h.Resistance(DamageType.Poison), 1e-5f, "weakness floor");
        }

        [Test]
        public void SpreadVariesEveryHitWithinFivePercent()
        {
            var h = Target(10000f);
            Health.SpreadRoll = () => 0f;
            Assert.AreEqual(95f, h.TakeDamage(Hit(100)));
            Health.SpreadRoll = () => 1f;
            Assert.AreEqual(105f, h.TakeDamage(Hit(100)));
        }

        [Test]
        public void KillIgnoresArmorShieldsAndSpread()
        {
            var h = Target(500f);
            h.armor = 200f;
            h.damageTakenMultiplier = 0.4f;
            Health.SpreadRoll = () => 0f;
            h.Kill();
            Assert.IsTrue(h.IsDead);
            Assert.AreEqual(0f, h.hp);
        }

        [Test]
        public void TaggedDamageBonusCountsForMatchingAbilities()
        {
            var b = new StatBlock();
            b.SetBase(StatId.DamageDealt, 1f);
            b.Add(new StatModifier(StatId.DamageDealt, ModKind.PercentAdd, 0.1f));                 // every skill
            b.Add(new StatModifier(StatId.DamageDealt, ModKind.PercentAdd, 0.2f, tag: "#Đạn"));    // "+20% #Đạn"
            b.Add(new StatModifier(StatId.DamageDealt, ModKind.PercentAdd, 0.5f, tag: "#Băng"));
            var ability = ScriptableObject.CreateInstance<AbilityDef>();
            try
            {
                ability.tags = AbilityTags.Fire | AbilityTags.Projectile;
                CollectionAssert.AreEqual(new[] { "#Lửa", "#Đạn" }, ability.TagList);
                Assert.AreEqual(1.3f, b.Get(StatId.DamageDealt, ability.TagList), 1e-5f);
                Assert.AreEqual(1.1f, b.Get(StatId.DamageDealt), 1e-5f, "no tags: only the untagged bonus");
                ability.tags = AbilityTags.Ice | AbilityTags.Projectile;
                Assert.AreEqual(1.8f, b.Get(StatId.DamageDealt, ability.TagList), 1e-5f, "the tag list follows the tags");
            }
            finally
            {
                Object.DestroyImmediate(ability);
            }
        }
    }
}

using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// Classes and peoples (D&D 5e) in the real world scene played offline: every class takes its
    /// skill bar and its drawn look, every one of its skills casts without an error, the standard
    /// array and the people's increases make the scores, a few traits work (Bán Orc's Kiên Trì
    /// Bất Khuất, Quỷ Duệ's fire resistance, Người Lùn's health), a class is chosen once, and
    /// who the hero is survives a save.
    /// </summary>
    public class ClassTests
    {
        string tempSaves;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempSaves = Path.Combine(Path.GetTempPath(), "rtt_test_saves_classes");
            if (Directory.Exists(tempSaves)) Directory.Delete(tempSaves, true);
            SaveManager.FolderOverride = tempSaves;
            Health.SpreadRoll = () => 0.5f;
            EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
            yield return new EnterPlayMode();
            yield return GameSmokeTests.Frames(3);
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((SceneLoader.I == null || SceneLoader.I.Busy) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsNotNull(ZoneRoot.Current, "the world loaded");
            yield return GameSmokeTests.Frames(2);
            if (HUD.I != null && HUD.I.help != null) HUD.I.help.Close();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return new ExitPlayMode();
            SaveManager.FolderOverride = null;
            Health.SpreadRoll = () => Random.value;
            if (Directory.Exists(tempSaves)) Directory.Delete(tempSaves, true);
        }

        static HeroLook Look(string cls, string race, string weapon = null)
        {
            var c = GameManager.I.db.Class(cls);
            return new HeroLook { cls = cls, race = race, weapon = weapon ?? c.DefaultWeapon };
        }

        [UnityTest]
        public IEnumerator EveryClassTakesItsSkillsAndItsLook()
        {
            var hero = Players.Local;
            var db = GameManager.I.db;
            Assert.AreEqual(12, db.classes.Count, "the twelve classes of D&D");
            Assert.AreEqual(9, db.races.Count, "the nine peoples of the Player's Handbook");
            var before = hero.anim.set;
            foreach (var cls in db.classes)
            {
                // a fresh hero each time: a class is chosen once
                hero.stats.look = new HeroLook();
                Assert.IsTrue(CharacterChoice.Apply(hero, Look(cls.id, "human")), cls.id);
                var weapon = WeaponKinds.Get(cls.DefaultWeapon);
                for (int i = 0; i < 7; i++)
                {
                    string id = i == 0 && !weapon.focus ? weapon.basic : cls.kit[i];
                    Assert.NotNull(db.Ability(id), $"{cls.id}: skill {id} exists");
                    Assert.AreEqual(id, hero.skills.slots[i].id, $"{cls.id}: slot {i}");
                    Assert.NotNull(hero.skills.slots[i].icon, $"{cls.id}: {id} has an icon");
                }
                Assert.AreEqual("dash", hero.skills.slots[7].id);
                Assert.AreNotEqual(before, hero.anim.set, $"{cls.id}: the hero is drawn in its look");
                Assert.NotNull(cls.emblem, $"{cls.id}: an emblem for its card");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator EverySkillOfEveryClassCasts()
        {
            var hero = Players.Local;
            hero.health.invulnerable = true;
            var db = GameManager.I.db;
            var target = EnemyBase.All.Find(e => !e.IsDead && e.enemyId == "slime");
            Assert.NotNull(target);
            target.health.invulnerable = true;
            foreach (var cls in db.classes)
            {
                hero.stats.look = new HeroLook();
                CharacterChoice.Apply(hero, Look(cls.id, "human"));
                for (int i = 0; i < 8; i++)
                {
                    hero.motor.Teleport((Vector2)target.transform.position + Vector2.left * 2f);
                    hero.energy = hero.maxEnergy;
                    hero.skills.ResetCooldowns();
                    yield return GameSmokeTests.GameSeconds(0.05f);
                    Assert.IsTrue(hero.skills.TryCast(i, target.transform.position), $"{cls.id}: {hero.skills.slots[i].id} casts");
                    yield return GameSmokeTests.GameSeconds(0.75f);
                }
            }
            // anything that threw or logged an error fails the test
        }

        [UnityTest]
        public IEnumerator ScoresComeFromTheArrayAndThePeople()
        {
            var hero = Players.Local;
            var s = hero.stats;
            s.look = new HeroLook();
            CharacterChoice.Apply(hero, Look("wizard", "dwarf"));
            Assert.AreEqual(15, s.Attribute(CoreStat.Intelligence), "a wizard's 15 in Trí Tuệ");
            Assert.AreEqual(16, s.Attribute(CoreStat.Constitution), "14 + 2 for a dwarf");
            Assert.AreEqual(13, s.Attribute(CoreStat.Wisdom), "12 + 1 for a hill dwarf");
            Assert.AreEqual(8, s.Attribute(CoreStat.Strength), "the wizard's dump stat");
            var c = s.Config;
            Assert.AreEqual(Mathf.Round(c.MaxHp(1, 3.5f, 16) + 1.8f), hero.health.maxHp, "d6 with Thể Chất 16 and Dẻo Dai");
            Assert.Less(hero.stats.SpeedMultiplier, 1f, "dwarves walk a little slower");
            Assert.Greater(hero.health.resistances.poison, 0.25f, "Kiên Cường");
            Assert.IsFalse(CharacterChoice.Apply(hero, Look("fighter", "dwarf")), "a class is chosen once");
            Assert.AreEqual("wizard", s.look.cls);
            var looks = Look("wizard", "dwarf");
            looks.hair = 3;
            looks.cloth = 2;
            looks.weapon = "tome";
            Assert.IsTrue(CharacterChoice.Apply(hero, looks), "looks and the weapon change any time");
            Assert.AreEqual("tome", s.Weapon.id);
            Assert.AreEqual("missile", hero.skills.slots[0].id, "a focus leaves Q to the cantrip");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PeoplesTraitsWork()
        {
            var hero = Players.Local;
            hero.stats.look = new HeroLook();
            CharacterChoice.Apply(hero, Look("barbarian", "halforc"));
            var lethal = DamageInfo.Make(99999f, Team.Enemy, null, hero.transform.position, Vector2.up);
            lethal.pure = true;
            hero.health.TakeDamage(lethal);
            Assert.IsFalse(hero.health.IsDead, "Kiên Trì Bất Khuất: a lethal blow leaves 1 health");
            Assert.AreEqual(1f, hero.health.hp);
            yield return null;
            hero.health.hp = hero.health.maxHp;
            hero.health.TakeDamage(lethal);
            Assert.IsTrue(hero.health.IsDead, "once every 90 seconds");
            yield return GameSmokeTests.GameSeconds(0.2f);

            hero.Respawn(hero.transform.position);
            yield return null;
            hero.stats.look = new HeroLook();
            CharacterChoice.Apply(hero, Look("warlock", "tiefling"));
            Assert.AreEqual(0.5f, hero.health.resistances.fire, 0.01f, "Quỷ Duệ resist fire");
            hero.stats.look = new HeroLook();
            var dragon = Look("paladin", "dragonborn");
            dragon.skin = 4;   // white scales: ice
            CharacterChoice.Apply(hero, dragon);
            Assert.AreEqual(0.5f, hero.health.resistances.ice, 0.01f, "a white dragon's scion resists ice");
        }

        [UnityTest]
        public IEnumerator TheSmithTempersTheWeapon()
        {
            var hero = Players.Local;
            var db = GameManager.I.db;
            hero.stats.look = new HeroLook();
            CharacterChoice.Apply(hero, Look("fighter", "human"));
            var smith = NPC.All.Find(n => n.forge);
            Assert.NotNull(smith, "Thợ Rèn in the village");
            hero.motor.Teleport((Vector2)smith.transform.position + Vector2.left * 8f);
            yield return null;
            Assert.NotNull(Forge.Upgrade(hero), "only next to the smith");
            hero.motor.Teleport((Vector2)smith.transform.position + Vector2.left * 1.2f);
            yield return null;
            hero.inventory.gold = 1000;
            Assert.NotNull(Forge.Upgrade(hero), "no slime gel yet");
            hero.inventory.Add(db.Item("gel"), 3, false);
            float before = hero.stats.Stats.Get(StatId.PhysicalAttack);
            Assert.IsNull(Forge.Upgrade(hero), "gold and 3 gel: +1");
            Assert.AreEqual(1, hero.stats.look.upgrade);
            Assert.AreEqual(960, hero.inventory.gold);
            Assert.AreEqual(0, hero.inventory.Count("gel"));
            Assert.AreEqual(before + 20f * WeaponKinds.AttackPerUpgrade, hero.stats.Stats.Get(StatId.PhysicalAttack), 0.01f, "+8% of the sword's attack");
            Assert.NotNull(Forge.ChangeMetal(hero, 2), "gold needs a +3 weapon");
            Assert.IsNull(Forge.ChangeMetal(hero, 1), "bronze is free");
            Assert.AreEqual(1, hero.stats.look.metal);
            Assert.IsNull(Forge.ChangeWeapon(hero, "axe"));
            Assert.AreEqual("cleave", hero.skills.slots[0].id, "an axe swings its own way");
            Assert.AreEqual(1, hero.stats.look.upgrade, "the forge level stays with the new weapon");
            Assert.NotNull(Forge.ChangeWeapon(hero, "staff"), "not a fighter's weapon");
        }

        [UnityTest]
        public IEnumerator WhoTheHeroIsSurvivesASave()
        {
            var hero = Players.Local;
            hero.stats.look = new HeroLook();
            var look = Look("ranger", "elf", "bow");
            look.hair = 2;
            look.hairColor = 5;
            look.cloth = 3;
            CharacterChoice.Apply(hero, look);
            hero.stats.statPoints = 2;
            hero.stats.Spend(CoreStat.Dexterity);
            string json = hero.CaptureState();
            hero.stats.look = new HeroLook();
            hero.RestoreState(json);
            Assert.AreEqual("ranger", hero.stats.look.cls);
            Assert.AreEqual("elf", hero.stats.look.race);
            Assert.AreEqual(2, hero.stats.look.hair);
            Assert.AreEqual(1, hero.stats.Allocated(CoreStat.Dexterity), "points spent stay spent");
            Assert.AreEqual(18, hero.stats.Attribute(CoreStat.Dexterity), "15 + 2 elf + 1 spent");
            Assert.AreEqual("arrow", hero.skills.slots[0].id);
            yield return null;
        }
    }
}

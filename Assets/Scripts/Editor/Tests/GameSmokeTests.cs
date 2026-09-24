using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// Plays the real Game scene for a few frames: levels, XP from kills, save + load round trip.
    /// Saves go to a temp folder. Runs in the EditMode test platform (enters Play Mode itself).
    /// </summary>
    public class GameSmokeTests
    {
        string tempSaves;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempSaves = Path.Combine(Path.GetTempPath(), "rtt_test_saves");
            if (Directory.Exists(tempSaves)) Directory.Delete(tempSaves, true);
            SaveManager.FolderOverride = tempSaves;
            EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
            yield return new EnterPlayMode();
            yield return Frames(3);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return new ExitPlayMode();
            SaveManager.FolderOverride = null;
            if (Directory.Exists(tempSaves)) Directory.Delete(tempSaves, true);
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator HeroStartsAtLevel1AndLevelsUp()
        {
            var s = PlayerStats.I;
            var p = GameManager.I.player;
            Assert.NotNull(s, "PlayerStats on the hero");
            Assert.AreEqual(1, s.level);
            Assert.AreEqual(104f, p.health.maxHp, "55 + 9×1 + 10×4");
            Assert.AreEqual(p.health.maxHp, p.health.hp);

            s.AddXp(60);
            Assert.AreEqual(2, s.level);
            Assert.AreEqual(10, s.xp);
            Assert.AreEqual(3, s.statPoints);
            Assert.AreEqual(p.health.maxHp, p.health.hp, "level up refills HP");

            Assert.IsTrue(s.Spend(CoreStat.Vitality));
            Assert.AreEqual(55f + 18f + 50f, p.health.maxHp);
            Assert.AreEqual(5f, p.health.armor, 1e-4f, "1 armor per Vitality");
            yield return null;
        }

        [UnityTest]
        public IEnumerator KillingAnEnemyGivesXp()
        {
            EnemyBase enemy = null;
            foreach (var e in Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Exclude))
                if (!e.IsDead) { enemy = e; break; }
            Assert.NotNull(enemy, "an enemy in the scene");
            int before = PlayerStats.I.xp + PlayerStats.I.level * 100000;
            enemy.health.Kill();
            yield return null;
            int after = PlayerStats.I.xp + PlayerStats.I.level * 100000;
            Assert.Greater(after, before);
        }

        [UnityTest]
        public IEnumerator SaveAndLoadRoundTrip()
        {
            var gm = GameManager.I;
            PlayerStats.I.SetState(3, 42, new[] { 1, 0, 2, 3 }, 0, 2, 2);
            Inventory.I.gold = 777;
            QuestSystem.I.main = QuestSystem.Main.SlayBoss;
            QuestSystem.I.slimes = 4;
            Vector2 spot = (Vector2)gm.respawnPoint.position + new Vector2(3f, 1f);
            gm.player.motor.Teleport(spot);
            DayNightCycle.I.time = 0.8f;
            yield return null;

            Assert.IsTrue(SaveManager.I.Save(1), "save to slot 1");
            Assert.IsTrue(File.Exists(SaveManager.PathFor(1)));
            var summary = SaveManager.Read(1);
            Assert.AreEqual(3, summary.level);
            Assert.AreEqual(SaveFile.CurrentVersion, summary.version);

            // a second save keeps the first as .bak
            Assert.IsTrue(SaveManager.I.Save(1));
            Assert.IsTrue(File.Exists(SaveManager.PathFor(1) + ".bak"));

            Assert.IsTrue(SaveManager.I.Load(1));
            yield return Frames(6);

            var p = GameManager.I.player;
            Assert.AreEqual(3, PlayerStats.I.level);
            Assert.AreEqual(42, PlayerStats.I.xp);
            Assert.AreEqual(3, PlayerStats.I.Allocated(CoreStat.Vitality));
            Assert.AreEqual(777, Inventory.I.gold);
            Assert.AreEqual(QuestSystem.Main.SlayBoss, QuestSystem.I.main);
            Assert.AreEqual(4, QuestSystem.I.slimes);
            Assert.Less(Vector2.Distance(p.transform.position, spot), 0.05f);
            Assert.AreEqual(0.8f, DayNightCycle.I.time, 0.02f);
        }

        [UnityTest]
        public IEnumerator DamagedSaveFallsBackToBackup()
        {
            Inventory.I.gold = 123;
            Assert.IsTrue(SaveManager.I.Save(2));
            Inventory.I.gold = 456;
            Assert.IsTrue(SaveManager.I.Save(2));
            File.WriteAllText(SaveManager.PathFor(2), "{ not json");
            var f = SaveManager.Read(2);
            Assert.NotNull(f, "reads the .bak");
            StringAssert.Contains("123", f.Get("inventory"));
            yield return null;
        }
    }

    public class SaveFileTests
    {
        [Test]
        public void SectionsRoundTripThroughJson()
        {
            var f = new SaveFile { level = 5, zone = "Rừng Thì Thầm" };
            f.Set("a", "{\"x\":1}");
            f.Set("b", "{}");
            f.Set("a", "{\"x\":2}");
            var back = JsonUtility.FromJson<SaveFile>(JsonUtility.ToJson(f));
            Assert.AreEqual(2, back.sections.Count);
            Assert.AreEqual("{\"x\":2}", back.Get("a"));
            Assert.AreEqual("Rừng Thì Thầm", back.zone);
            Assert.IsNull(back.Get("missing"));
        }

        [Test]
        public void RefusesFilesFromANewerBuild()
        {
            Assert.IsTrue(SaveFile.Migrate(new SaveFile { version = 1 }));
            Assert.IsFalse(SaveFile.Migrate(new SaveFile { version = SaveFile.CurrentVersion + 1 }));
        }
    }
}

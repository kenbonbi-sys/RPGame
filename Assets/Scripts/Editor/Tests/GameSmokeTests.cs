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
            if (HUD.I != null && HUD.I.help != null) HUD.I.help.Close();   // the start-up help panel blocks input
            yield return null;
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

        /// <summary>WaitForSeconds does not wait in EditMode-driven tests; count game time instead.</summary>
        static IEnumerator GameSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
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
        public IEnumerator BossPoiseBreaksStunsAndRecovers()
        {
            var boss = Object.FindAnyObjectByType<BossBear>();
            Assert.NotNull(boss, "boss in the scene");
            var poise = boss.poise;
            Assert.NotNull(poise, "boss prefab has a Poise component");
            Assert.AreEqual(300f, poise.Threshold);
            var hero = GameManager.I.player.gameObject;
            float baseMul = boss.health.damageTakenMultiplier;

            var d = DamageInfo.Make(1, Team.Player, hero, boss.transform.position, Vector2.up);
            d.poise = 100f;
            boss.health.TakeDamage(d);
            Assert.IsFalse(poise.IsBroken);
            Assert.Greater(poise.Current, 100f, "Strength adds poise");

            d.poise = 250f;
            boss.health.TakeDamage(d);
            Assert.IsTrue(poise.IsBroken, "full bar breaks");
            Assert.IsTrue(boss.status.IsStunned);
            Assert.AreEqual(1, poise.Breaks);
            Assert.AreEqual(375f, poise.Threshold, 0.01f, "+25% after a break");
            Assert.AreEqual(baseMul * 1.5f, boss.health.damageTakenMultiplier, 1e-4f, "+50% damage taken");

            yield return GameSeconds(poise.breakStun + 0.3f);
            Assert.IsFalse(poise.IsBroken);
            Assert.AreEqual(baseMul, boss.health.damageTakenMultiplier, 1e-4f, "bonus removed");

            d.poise = 50f;
            boss.health.TakeDamage(d);
            float filled = poise.Current;
            yield return GameSeconds(poise.decayDelay + 0.5f);
            Assert.Less(poise.Current, filled, "drains when not hit");
        }

        [UnityTest]
        public IEnumerator EarlyKeyPressIsBuffered()
        {
            var p = GameManager.I.player;
            var sk = p.skills;
            Vector2 aim = (Vector2)p.transform.position + Vector2.right * 3f;
            int casts = 0;
            sk.SkillCast += _ => casts++;

            Assert.IsTrue(sk.Request(0, aim), "first press fires");
            Assert.AreEqual(1, casts);
            Assert.IsFalse(sk.Request(0, aim), "pressed far too early");
            Assert.IsFalse(sk.HasBuffered, "outside the 150 ms window: not buffered");

            yield return GameSeconds(sk.ReadyIn(0) - 0.08f);
            Assert.IsFalse(sk.Request(0, aim), "not ready yet");
            Assert.IsTrue(sk.HasBuffered, "inside the window: buffered");
            yield return GameSeconds(0.2f);
            Assert.AreEqual(2, casts, "buffered press fired once");
            Assert.IsFalse(sk.HasBuffered);
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

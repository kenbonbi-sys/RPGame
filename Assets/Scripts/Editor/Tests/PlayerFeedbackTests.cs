using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// Players' first feedback (25/09/2026), in the real world scene played offline: a click on a
    /// big enemy walks up to it and lands blows (reach is measured to the edge of its body, not its
    /// middle), Tự Động hunts on its own around where it was switched on, and a fallen boss leaves a
    /// chest in the middle of its arena that bursts open with its loot when the hero walks up.
    /// </summary>
    public class PlayerFeedbackTests
    {
        string tempSaves;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempSaves = Path.Combine(Path.GetTempPath(), "rtt_test_saves_feedback");
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

        static EnemyBase Nearest(string id, Vector2 near)
        {
            EnemyBase best = null;
            foreach (var e in EnemyBase.All)
            {
                if (e.enemyId != id || e.IsDead || !e.gameObject.activeInHierarchy) continue;
                // away from the bosses, which Tự Động leaves alone
                if (BossBase.All.Exists(b => b != null && Vector2.Distance(b.Home, e.transform.position) < 20f)) continue;
                if (best == null || Vector2.Distance(e.transform.position, near) < Vector2.Distance(best.transform.position, near))
                    best = e;
            }
            return best;
        }

        [UnityTest]
        public IEnumerator AClickedBigEnemyIsStruckFromTheEdgeOfItsBody()
        {
            var hero = Players.Local;
            hero.health.invulnerable = true;
            var bear = BossBase.Find("bear");
            Assert.NotNull(bear);
            Vector2 c = bear.transform.position;
            Vector2 beside = c + Vector2.right * 3f;
            float reach = PlayerController.Reach(beside, bear.health);
            Assert.Less(reach, 3f - 0.4f, "reach is to the edge of its body, not its middle");
            Assert.Greater(reach, 0f);

            // somewhere with a clear line to it (the arena has boulders)
            Vector2 from = c + Vector2.right * 5f;
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f;
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5f;
                if (!Util.LineBlocked(p, c + (p - c).normalized * 2f)) { from = p; break; }
            }
            hero.motor.Teleport(from);
            yield return GameSmokeTests.GameSeconds(0.2f);
            float hp = bear.health.hp;
            hero.Attack(bear.health);   // what a left click on it does
            float until = Time.time + 10f;
            while (Time.time < until && bear.health.hp >= hp) yield return null;
            Assert.Less(bear.health.hp, hp, $"the hero walked up to it and struck it (hero at {hero.transform.position}, " +
                                             $"reach {PlayerController.Reach(hero.transform.position, bear.health):0.00}, bear at {bear.transform.position})");
        }

        [UnityTest]
        public IEnumerator TuDongHuntsAroundWhereItWasSwitchedOn()
        {
            var hero = Players.Local;
            hero.health.invulnerable = true;
            var slime = Nearest("slime", hero.transform.position);
            Assert.NotNull(slime);
            Vector2 start = (Vector2)slime.transform.position + Vector2.left * 3f;
            hero.motor.Teleport(start);
            yield return GameSmokeTests.GameSeconds(0.2f);
            hero.auto.Set(hero, true);
            Assert.IsTrue(hero.auto.On);
            Assert.AreEqual(start.x, hero.auto.Anchor.x, 0.2f, "its hunting ground is where it was switched on");
            float until = Time.time + 12f;
            while (Time.time < until && !slime.IsDead) yield return null;
            Assert.IsTrue(slime.IsDead, "it went after the slime and struck it down on its own");
            Assert.Less(Vector2.Distance(hero.transform.position, hero.auto.Anchor), AutoHunt.Leash + 1f, "it stays near its ground");
            hero.health.invulnerable = false;
            hero.health.Kill();
            yield return null;
            Assert.IsFalse(hero.auto.On, "a fall switches it off");
        }

        [UnityTest]
        public IEnumerator AFallenBossLeavesAChestThatBurstsOpen()
        {
            var hero = Players.Local;
            hero.health.invulnerable = true;
            var bear = BossBase.Find("bear");
            Assert.NotNull(bear);
            hero.motor.Teleport(bear.Home + Vector2.left * 20f);
            yield return GameSmokeTests.GameSeconds(0.2f);
            var hit = DamageInfo.Make(999999f, Team.Player, hero.gameObject, bear.transform.position, Vector2.up);
            hit.pure = true;
            bear.health.TakeDamage(hit);
            Assert.IsTrue(bear.health.IsDead);

            TreasureChest chest = null;
            float deadline = Time.realtimeSinceStartup + 6f;
            while (chest == null && Time.realtimeSinceStartup < deadline)
            {
                chest = Object.FindAnyObjectByType<TreasureChest>();
                yield return null;
            }
            Assert.NotNull(chest, "a chest appears");
            Assert.Less(Vector2.Distance(chest.transform.position, bear.Home), 1.5f, "in the middle of the arena");
            Assert.IsFalse(chest.Opened, "it waits for its hero");
            Assert.Greater(chest.Contents.Count, 0, "with the boss's loot in it");

            int gold = hero.inventory.gold;
            int drops = Object.FindObjectsByType<LootPickup>(FindObjectsInactive.Exclude).Length;
            hero.motor.Teleport(chest.transform.position);
            float until = Time.time + 3f;
            while (Time.time < until && !chest.Opened) yield return null;
            Assert.IsTrue(chest.Opened, "walking up to it opens it");
            yield return GameSmokeTests.GameSeconds(1.5f);
            int dropsNow = Object.FindObjectsByType<LootPickup>(FindObjectsInactive.Exclude).Length;
            Assert.IsTrue(hero.inventory.gold > gold || dropsNow > drops, "the loot burst out (coins already in the purse)");
        }
    }
}

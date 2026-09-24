using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// Online phase 0 (Docs/KeHoach-Online.md): the game no longer assumes a single hero. Plays the
    /// real scenes with a second hero next to the local one: each keeps their own stats, bag,
    /// quests, bestiary and save sections; kills credit everyone who hurt the enemy; enemies and
    /// the boss go after whoever angered them most; loot goes to the nearest hero; and in a shared
    /// world time effects stay on this screen.
    /// </summary>
    public class OnlinePrepTests
    {
        string tempSaves;
        PlayerController me, other;

        /// <summary>An empty spot outside the map (no colliders, no camps).</summary>
        static readonly Vector2 Away = new Vector2(500f, 500f);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempSaves = Path.Combine(Path.GetTempPath(), "rtt_test_saves_online");
            if (Directory.Exists(tempSaves)) Directory.Delete(tempSaves, true);
            SaveManager.FolderOverride = tempSaves;
            Health.SpreadRoll = () => 0.5f;
            GameSession.Mode = SessionMode.Offline;
            EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
            yield return new EnterPlayMode();
            yield return GameSmokeTests.Frames(3);
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((SceneLoader.I == null || SceneLoader.I.Busy) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsNotNull(ZoneRoot.Current, "start zone loaded");
            yield return GameSmokeTests.Frames(2);
            if (HUD.I != null && HUD.I.help != null) HUD.I.help.Close();

            me = Players.Local;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFactory.CharFolder + "/Player.prefab");
            var go = Object.Instantiate(prefab, (Vector2)me.transform.position + Vector2.right * 2f, Quaternion.identity);
            go.name = "Hero 2";
            other = go.GetComponent<PlayerController>();
            yield return GameSmokeTests.Frames(2);   // its Start: quests auto-start
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameSession.Mode = SessionMode.Offline;
            yield return new ExitPlayMode();
            SaveManager.FolderOverride = null;
            Health.SpreadRoll = () => Random.value;
            if (Directory.Exists(tempSaves)) Directory.Delete(tempSaves, true);
        }

        static DamageInfo Hit(PlayerController from, Health target, float amount)
        {
            var d = DamageInfo.Make(amount, Team.Player, from.gameObject, target.transform.position, Vector2.up);
            d.pure = true;   // exact amounts
            return d;
        }

        static IEnumerator Until(System.Func<bool> done, float gameSeconds)
        {
            float end = Time.time + gameSeconds;
            while (!done() && Time.time < end) yield return null;
        }

        [UnityTest]
        public IEnumerator ASecondHeroJoinsButTheScreenStaysWithTheFirst()
        {
            Assert.AreEqual(2, Players.All.Count);
            Assert.AreEqual(GameManager.I.localPlayer, me, "the Core scene's hero is the local one");
            Assert.IsTrue(me.IsLocal);
            Assert.IsFalse(other.IsLocal);
            Assert.AreEqual(me.transform, CameraRig.I.target, "the camera follows the local hero");
            Assert.AreNotSame(me.inventory, other.inventory, "a bag each");
            Assert.AreNotSame(me.quests, other.quests, "a quest log each");
            Assert.AreNotSame(me.bestiary, other.bestiary, "a bestiary each");
            Assert.AreEqual(me.inventory.gold, other.inventory.gold, "both start with the kit of the prefab");
            Assert.AreEqual(4, other.inventory.Count("potion_red"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EachHeroKeepsTheirOwnProgress()
        {
            Assert.AreEqual(QuestStatus.Active, other.quests.Status("talk_chief"), "auto-started for the new hero too");
            int gold = me.inventory.gold;
            other.inventory.gold += 500;
            Assert.AreEqual(gold, me.inventory.gold);

            int levelUps = 0;
            GameEvents.LevelUp += _ => levelUps++;
            other.stats.AddXp(60);
            Assert.AreEqual(2, other.stats.level);
            Assert.AreEqual(1, me.stats.level);
            Assert.AreEqual(0, levelUps, "another hero's level-up is not on this screen's HUD");
            me.stats.AddXp(60);
            Assert.AreEqual(1, levelUps);

            yield return GameSmokeTests.TalkThrough("chief");
            Assert.AreEqual(QuestStatus.Done, me.quests.Status("talk_chief"), "the local hero talked to the chief");
            Assert.AreEqual(QuestStatus.Active, other.quests.Status("talk_chief"), "the other did not");
            var chief = NPC.All.Find(n => n.npcId == "chief");
            Assert.IsFalse(DialogueDirector.I.Talk(chief, other), "another hero's conversation runs on their own screen");
        }

        [UnityTest]
        public IEnumerator KillsCreditEveryoneWhoHurtTheEnemy()
        {
            var slime = EnemyBase.All.Find(e => !e.IsDead && e.enemyId == "slime");
            Assert.NotNull(slime, "a Slime Rêu in the zone");
            int myXp = me.stats.xp + me.stats.level * 100000;
            int otherXp = other.stats.xp + other.stats.level * 100000;
            slime.health.TakeDamage(Hit(other, slime.health, 1f));
            CollectionAssert.AreEqual(new[] { other }, slime.health.Attackers);
            slime.health.Kill();
            yield return null;
            Assert.Greater(other.stats.xp + other.stats.level * 100000, otherXp, "the hero who hurt it gets XP");
            Assert.AreEqual(myXp, me.stats.xp + me.stats.level * 100000, "the one who did not, none");
            Assert.AreEqual(1, other.quests.KillCount("slime"));
            Assert.AreEqual(0, me.quests.KillCount("slime"));
            Assert.AreEqual(1, other.bestiary.Find("slime").kills);
            Assert.IsNull(me.bestiary.Find("slime"));

            // a kill nobody took part in (a script, a cheat) counts for every hero
            var another = EnemyBase.All.Find(e => !e.IsDead && e.enemyId == "slime");
            Assert.NotNull(another);
            another.health.Kill();
            yield return null;
            Assert.AreEqual(2, other.quests.KillCount("slime"));
            Assert.AreEqual(1, me.quests.KillCount("slime"));
        }

        [UnityTest]
        public IEnumerator EnemiesGoAfterWhoeverHurtThemMost()
        {
            var slime = EnemyBase.All.Find(e => !e.IsDead && e.enemyId == "slime");
            Assert.NotNull(slime);
            me.health.invulnerable = other.health.invulnerable = true;
            Vector2 at = slime.transform.position;
            me.motor.Teleport(at + Vector2.left * 3f);
            other.motor.Teleport(at + Vector2.right * 3f);
            yield return GameSmokeTests.Frames(2);

            slime.health.TakeDamage(Hit(other, slime.health, 5f));
            yield return Until(() => slime.Target == other, 2f);
            Assert.AreEqual(other, slime.Target, "chases the hero who hit it");

            slime.health.TakeDamage(Hit(me, slime.health, 20f));
            yield return Until(() => slime.Target == me, 3f);
            Assert.AreEqual(me, slime.Target, "turns on the hero who hurt it more");
        }

        [UnityTest]
        public IEnumerator TheBossFightsTheAngriestHeroAndResetsOnlyWhenAllAreGone()
        {
            var boss = BossBase.Find("bear");
            var spot = (Vector2)GameManager.I.bossSpot.position;
            me.health.invulnerable = other.health.invulnerable = true;
            me.motor.Teleport(spot + new Vector2(-2f, -4f));
            other.motor.Teleport(spot + new Vector2(2f, -4f));
            boss.health.TakeDamage(Hit(other, boss.health, 50f));
            yield return Until(() => boss.Target == other, 5f);
            Assert.AreEqual(other, boss.Target, "woken by the second hero, fights them");

            boss.health.TakeDamage(Hit(me, boss.health, 200f));
            yield return Until(() => boss.Target == me, 5f);
            Assert.AreEqual(me, boss.Target, "turns on the hero who hurt it more");

            me.motor.Teleport(Away);
            yield return Until(() => boss.Target == other, 5f);
            Assert.AreEqual(other, boss.Target, "the local hero left: back to the other");
            Assert.IsTrue(boss.Engaged, "still fighting while a hero is in the arena");

            other.motor.Teleport(Away + Vector2.right * 3f);
            yield return Until(() => !boss.Engaged, 5f);
            Assert.IsFalse(boss.Engaged, "everyone left: the fight resets");
            Assert.AreEqual(boss.health.maxHp, boss.health.hp);
            Assert.AreEqual(0, boss.health.Attackers.Count, "and forgets who hurt it");
        }

        [UnityTest]
        public IEnumerator TheBossGrowsWithTheHeroesInTheFight()
        {
            var boss = BossBase.Find("bear");
            var spot = (Vector2)GameManager.I.bossSpot.position;
            float baseHp = boss.maxHp;
            float two = BossBear.MaxHpFor(baseHp, 2, boss.hpPerExtraHero);
            me.health.invulnerable = other.health.invulnerable = true;
            other.motor.Teleport(Away);
            me.motor.Teleport(spot + new Vector2(-2f, -4f));
            yield return Until(() => boss.Engaged, 5f);
            Assert.IsTrue(boss.Engaged, "one hero wakes it");
            yield return GameSmokeTests.GameSeconds(0.8f);
            Assert.AreEqual(baseHp, boss.health.maxHp, 0.01f, "alone: the bear of the offline game");

            boss.health.TakeDamage(Hit(me, boss.health, baseHp * 0.25f));
            float share = boss.health.Fraction;
            other.motor.Teleport(spot + new Vector2(2f, -4f));
            yield return Until(() => boss.health.maxHp > baseHp, 3f);
            Assert.AreEqual(two, boss.health.maxHp, 0.5f, "a second hero joins the fight: more health");
            Assert.AreEqual(share, boss.health.Fraction, 0.01f, "with the same share of it left");

            other.motor.Teleport(Away);
            yield return GameSmokeTests.GameSeconds(1f);
            Assert.AreEqual(two, boss.health.maxHp, 0.5f, "a hero leaving does not shrink it mid-fight");

            me.motor.Teleport(Away + Vector2.left * 3f);
            yield return Until(() => !boss.Engaged, 5f);
            Assert.IsFalse(boss.Engaged);
            Assert.AreEqual(baseHp, boss.health.maxHp, 0.01f, "the fight reset: back to its own health");
        }

        [UnityTest]
        public IEnumerator LootGoesToTheNearestHero()
        {
            other.motor.Teleport(Away);
            yield return GameSmokeTests.Frames(2);
            int mine = me.inventory.Count("apple"), theirs = other.inventory.Count("apple");
            Loot.Drop("apple", 1, Away + Vector2.up * 0.3f);
            yield return Until(() => other.inventory.Count("apple") > theirs, 3f);
            Assert.AreEqual(theirs + 1, other.inventory.Count("apple"), "into the bag of the hero next to it");
            Assert.AreEqual(mine, me.inventory.Count("apple"));
        }

        [UnityTest]
        public IEnumerator EachHeroSavesOnTheirOwn()
        {
            me.inventory.gold = 111;
            other.inventory.gold = 999;
            other.stats.SetState(5, 7, new[] { 1, 1, 1, 1 }, 0, 0, 0);
            yield return null;

            var mine = SaveManager.CaptureCharacter(me);
            var theirs = SaveManager.CaptureCharacter(other);
            var keys = new List<string>();
            foreach (var s in theirs) keys.Add(s.key);
            CollectionAssert.IsSubsetOf(new[] { "player", "inventory", "quests", "bestiary" }, keys);
            CollectionAssert.DoesNotContain(keys, "dialogue", "the Yarn variables are the local hero's");
            var world = new List<string>();
            foreach (var s in SaveManager.CaptureWorld()) world.Add(s.key);
            CollectionAssert.Contains(world, "time");
            CollectionAssert.DoesNotContain(world, "inventory", "no hero's bag in the world's sections");

            SaveManager.RestoreCharacter(me, theirs);
            SaveManager.RestoreCharacter(other, mine);
            Assert.AreEqual(999, me.inventory.gold);
            Assert.AreEqual(5, me.stats.level);
            Assert.AreEqual(111, other.inventory.gold);
            Assert.AreEqual(1, other.stats.level);

            // offline, a slot holds the world and the local hero
            Assert.IsTrue(SaveManager.I.Save(1));
            var f = SaveManager.Read(1);
            StringAssert.Contains("999", f.Get("inventory"));
            Assert.NotNull(f.Get("time"));
            Assert.AreEqual(5, f.level);
        }

        [UnityTest]
        public IEnumerator AHeroActsOnItsIntent()
        {
            other.motor.Teleport(Away);
            yield return GameSmokeTests.Frames(2);
            float x = other.transform.position.x;
            other.SetIntent(new PlayerIntent { move = Vector2.right });
            yield return GameSmokeTests.GameSeconds(0.5f);
            Assert.Greater(other.transform.position.x, x + 1f, "walks while the intent says so");
            other.SetIntent(new PlayerIntent());

            int red = other.inventory.Count("potion_red");
            int casts = 0;
            other.skills.SkillCast += slot => { if (slot == 1) casts++; };
            other.SetIntent(new PlayerIntent { potionPresses = 1, skillPresses = 1 << 1, aim = Away + Vector2.right * 4f });
            yield return GameSmokeTests.Frames(4);
            Assert.AreEqual(red - 1, other.inventory.Count("potion_red"), "a press acts once");
            Assert.AreEqual(1, casts, "Cầu Lửa cast once");
        }

        [UnityTest]
        public IEnumerator AnotherHeroFallingDoesNotShowTheDeathScreen()
        {
            int downed = 0;
            GameEvents.PlayerDowned += _ => downed++;
            other.health.Kill();
            yield return GameSmokeTests.RealSeconds(1.5f);
            Assert.AreNotEqual(GameState.Dead, GameManager.I.State);
            Assert.AreEqual(0, downed);
            yield return GameSmokeTests.RealSeconds(4.2f);
            Assert.IsFalse(other.IsDead, "gets up at the spawn");
            Assert.Less(Vector2.Distance(other.transform.position, GameManager.I.respawnPoint.position), 0.1f);
        }

        [UnityTest]
        public IEnumerator InASharedWorldTimeRunsOn()
        {
            GameSession.Mode = SessionMode.Host;
            var sk = me.skills;
            var pd = me.perfectDodge;
            me.energy = 10f;
            Vector2 aim = (Vector2)me.transform.position + Vector2.right * 3f;
            Assert.IsTrue(sk.TryCast(7, aim), "Lướt");
            me.health.TakeDamage(DamageInfo.Make(20, Team.Enemy, null, me.transform.position, Vector2.left));
            Assert.AreEqual(1, pd.Count, "still a perfect dodge");
            Assert.IsTrue(pd.BonusReady, "with its bonus");
            yield return GameSmokeTests.Frames(2);
            Assert.AreEqual(1f, Time.timeScale, 1e-4f, "but no slow motion for everyone");

            var slime = EnemyBase.All.Find(e => !e.IsDead);
            var hit = Hit(me, slime.health, 1f);
            hit.hitStop = 0.3f;
            Combat.OnHitFeedback(slime.health, hit);
            yield return GameSmokeTests.Frames(1);
            Assert.AreEqual(1f, Time.timeScale, 1e-4f, "a hit-stop does not stop the world");
            Assert.IsTrue(slime.anim.Held, "the one that was hit holds its pose");

            GameManager.I.SetMenu(true, true);
            yield return GameSmokeTests.Frames(2);
            Assert.AreEqual(1f, Time.timeScale, 1e-4f, "a menu does not pause the world");
            GameManager.I.SetMenu(false, false);
        }
    }

    /// <summary>Online phase 0 pieces that need no scene.</summary>
    public class OnlinePrepUnitTests
    {
        [Test]
        public void ThreatTablePicksTheHeroWithTheMost()
        {
            var a = new GameObject("a").AddComponent<PlayerController>();
            var b = new GameObject("b").AddComponent<PlayerController>();
            try
            {
                var t = new ThreatTable();
                Assert.IsNull(t.Top());
                t.Add(a, 5f);
                t.Add(b, 3f);
                Assert.AreEqual(a, t.Top());
                t.Add(b, 4f);
                Assert.AreEqual(b, t.Top(), "7 beats 5");
                Assert.AreEqual(a, t.Top(p => p != b), "out of range: dropped");
                Assert.AreEqual(0f, t.Of(b), "and forgotten");
                t.Add(null, 99f);
                Assert.AreEqual(1, t.Count, "no threat for nobody");
                Object.DestroyImmediate(a.gameObject);
                Assert.IsNull(t.Top(), "gone heroes drop out");
                Assert.AreEqual(0, t.Count);
            }
            finally
            {
                if (a != null) Object.DestroyImmediate(a.gameObject);
                Object.DestroyImmediate(b.gameObject);
            }
        }

        [Test]
        public void KillsCreditTheirHeroesOrEveryone()
        {
            var a = new GameObject("a").AddComponent<PlayerController>();
            var b = new GameObject("b").AddComponent<PlayerController>();
            try
            {
                var scripted = new KillInfo { id = "slime" };
                Assert.IsTrue(scripted.Credits(a) && scripted.Credits(b), "no attackers: everyone");
                var fought = new KillInfo { id = "slime", credited = new List<PlayerController> { a } };
                Assert.IsTrue(fought.Credits(a));
                Assert.IsFalse(fought.Credits(b));
            }
            finally
            {
                Object.DestroyImmediate(a.gameObject);
                Object.DestroyImmediate(b.gameObject);
            }
        }

        [Test]
        public void AnIntentsPressesHappenOnce()
        {
            var i = new PlayerIntent { move = Vector2.right, aim = Vector2.one, skillPresses = 0b101, potionPresses = 0b10, basicAttack = true };
            Assert.IsTrue(i.SkillPressed(0) && !i.SkillPressed(1) && i.SkillPressed(2));
            Assert.IsTrue(i.PotionPressed(1));
            var next = i.Held;
            Assert.AreEqual(Vector2.right, next.move, "keeps walking");
            Assert.AreEqual(Vector2.one, next.aim);
            Assert.AreEqual(0, next.skillPresses);
            Assert.AreEqual(0, next.potionPresses);
            Assert.IsFalse(next.basicAttack);
        }
    }
}

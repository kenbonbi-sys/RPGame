using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
            // Core loads the start zone next to itself
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((SceneLoader.I == null || SceneLoader.I.Busy) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsNotNull(ZoneRoot.Current, "start zone loaded");
            yield return Frames(2);
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

        /// <summary>Waits <paramref name="n"/> game frames (not editor updates; see TalkThrough).</summary>
        static IEnumerator Frames(int n)
        {
            int target = Time.frameCount + n;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.frameCount < target && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>
        /// Talks to an NPC and clicks through the whole conversation, picking <paramref name="choice"/>.
        /// The guard is real time: here each yield is one editor update, and many of those can pass
        /// within a single (frame-rate capped) game frame.
        /// </summary>
        static IEnumerator TalkThrough(string npcId, int choice = 0)
        {
            var npc = NPC.All.Find(n => n.npcId == npcId);
            Assert.NotNull(npc, "npc " + npcId);
            Assert.IsTrue(DialogueDirector.I.Talk(npc), "conversation starts");
            float deadline = Time.realtimeSinceStartup + 15f;
            while (DialogueDirector.I.IsRunning)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    var ui = DialogueUI.I;
                    Assert.Fail($"conversation with {npcId} did not end: runner running={DialogueDirector.I.Runner.IsDialogueRunning}, " +
                                $"ui open={ui.IsOpen}, options={ui.ShowingOptions}, advance={ui.AdvanceRequested}, chosen={ui.ChosenOption}, " +
                                $"text='{ui.bodyText.text}'");
                }
                if (DialogueUI.I.ShowingOptions) DialogueUI.I.Choose(choice);
                else DialogueUI.I.DebugAdvance();
                yield return null;
            }
            yield return null;
        }

        /// <summary>Kills one live enemy with the given id (spawners keep the counts going).</summary>
        static void KillEnemy(string id)
        {
            foreach (var e in Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Exclude))
            {
                if (e.IsDead || e.enemyId != id) continue;
                e.health.Kill();
                return;
            }
            // not enough live ones: report the kill the way an enemy would
            GameEvents.RaiseEnemyKilled(new KillInfo { id = id, name = id, level = 2, rank = EnemyRank.Normal });
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
            yield return TalkThrough("chief");                      // talk_chief done, clear_forest active
            KillEnemy("slime");                                      // 1/4 slimes
            QuestSystem.I.SetFlag("test_flag");
            PlayerStats.I.SetState(3, 42, new[] { 1, 0, 2, 3 }, 0, 2, 2);
            Inventory.I.gold = 777;
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
            // Core reloads, loads the saved zone, then applies the save
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((SaveManager.HasPendingLoad || SceneLoader.I == null || SceneLoader.I.Busy) && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return Frames(2);

            var p = GameManager.I.player;
            Assert.AreEqual(3, PlayerStats.I.level);
            Assert.AreEqual(42, PlayerStats.I.xp);
            Assert.AreEqual(3, PlayerStats.I.Allocated(CoreStat.Vitality));
            Assert.AreEqual(777, Inventory.I.gold);
            Assert.AreEqual(QuestStatus.Done, QuestSystem.I.Status("talk_chief"));
            Assert.AreEqual(QuestStatus.Active, QuestSystem.I.Status("clear_forest"));
            Assert.AreEqual(1, QuestSystem.I.Progress("clear_forest", 0));
            Assert.IsTrue(QuestSystem.I.HasFlag("test_flag"));
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
        public IEnumerator ChiefIntroFinishesFirstQuestAndOpensTheForest()
        {
            var q = QuestSystem.I;
            Assert.AreEqual(QuestStatus.Active, q.Status("talk_chief"), "auto-started at boot");
            Assert.AreEqual(QuestSystem.Marker.Exclaim, q.MarkerFor("chief"));
            Assert.AreEqual(QuestStatus.Locked, q.Status("mushrooms"));
            int red = Inventory.I.Count("potion_red");

            yield return TalkThrough("chief");

            Assert.AreEqual(QuestStatus.Done, q.Status("talk_chief"));
            Assert.AreEqual(QuestStatus.Active, q.Status("clear_forest"), "follow-up started");
            Assert.AreEqual(QuestStatus.Available, q.Status("mushrooms"), "side quest unlocked");
            Assert.AreEqual(QuestSystem.Marker.Exclaim, q.MarkerFor("girl"));
            Assert.AreEqual(red + 2, Inventory.I.Count("potion_red"), "reward");
            Assert.AreEqual(20, PlayerStats.I.xp);
            Assert.AreEqual("clear_forest", q.Tracked()[0].id);
        }

        [UnityTest]
        public IEnumerator MaiChoiceAcceptsAndDeliveryTurnsIn()
        {
            var q = QuestSystem.I;
            yield return TalkThrough("chief");
            yield return TalkThrough("girl", 1);                     // "Để sau nhé"
            Assert.AreEqual(QuestStatus.Available, q.Status("mushrooms"), "declining keeps it available");
            yield return TalkThrough("girl", 0);                     // "Được"
            Assert.AreEqual(QuestStatus.Active, q.Status("mushrooms"));

            var cap = GameManager.I.db.Item("shroom_cap");
            Inventory.I.Add(cap, 3);
            yield return null;
            Assert.AreEqual(QuestStatus.Ready, q.Status("mushrooms"));
            Assert.AreEqual(QuestSystem.Marker.Question, q.MarkerFor("girl"));

            int green = Inventory.I.Count("potion_green");
            yield return TalkThrough("girl");
            Assert.AreEqual(QuestStatus.Done, q.Status("mushrooms"));
            Assert.AreEqual(0, Inventory.I.Count("shroom_cap"), "delivered");
            Assert.AreEqual(green + 3, Inventory.I.Count("potion_green"));
        }

        [UnityTest]
        public IEnumerator KillsDriveTheMainQuestChain()
        {
            var q = QuestSystem.I;
            yield return TalkThrough("chief");
            // the bear dies early: slay_bear counts earlier kills
            GameEvents.RaiseEnemyKilled(new KillInfo { id = "bear", name = "Gấu Ma", level = 6, rank = EnemyRank.Boss });
            for (int i = 0; i < 4; i++) KillEnemy("slime");
            Assert.AreEqual(QuestStatus.Active, q.Status("clear_forest"));
            for (int i = 0; i < 2; i++) KillEnemy("shroom");
            yield return null;
            Assert.AreEqual(QuestStatus.Done, q.Status("clear_forest"), "no turn-in: completes by itself");
            Assert.AreEqual(QuestStatus.Ready, q.Status("slay_bear"), "bear already dead");
            Assert.AreEqual(QuestSystem.Marker.Question, q.MarkerFor("chief"));

            yield return TalkThrough("chief");
            Assert.AreEqual(QuestStatus.Done, q.Status("slay_bear"));
            Assert.IsTrue(q.HasFlag("forest_saved"));
        }

        [UnityTest]
        public IEnumerator EverySaveableRegistersItself()
        {
            var registered = new System.Collections.Generic.List<ISaveable>(SaveRegistry.Items);
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (mb is ISaveable s) Assert.IsTrue(registered.Contains(s), mb.GetType().Name + " is not in SaveRegistry");
            yield return null;
        }

        [UnityTest]
        public IEnumerator UiIsPooled()
        {
            var enemy = EnemyBase.All.Find(e => !e.IsDead);
            Assert.NotNull(enemy);
            var hero = GameManager.I.player.gameObject;
            enemy.health.TakeDamage(DamageInfo.Make(1, Team.Player, hero, enemy.transform.position, Vector2.up));
            yield return null;
            var layer = HUD.I.worldLayer;
            int plates = layer.GetComponentsInChildren<NameplateUI>(true).Length;
            int active = layer.GetComponentsInChildren<NameplateUI>(false).Length;
            enemy.health.Kill();                  // its nameplate goes back to the pool
            yield return null;
            Assert.AreEqual(plates, layer.GetComponentsInChildren<NameplateUI>(true).Length, "released, not destroyed");
            Assert.AreEqual(active - 1, layer.GetComponentsInChildren<NameplateUI>(false).Length, "switched off");
            GameEvents.RaiseBanner(BannerKind.Victory, "T", "S");
            GameEvents.RaiseSkillAnnounced(enemy.health, "Kỹ năng: thử");
            yield return null;
        }

        class TestLever : MonoBehaviour, IWorldState
        {
            public bool pulled;
            public string CaptureWorldState() => pulled ? "1" : "0";
            public void RestoreWorldState(string json) => pulled = json == "1";
        }

        [UnityTest]
        public IEnumerator WorldObjectsSaveByStableId()
        {
            var go = new GameObject("Lever");
            go.SetActive(false);
            go.AddComponent<WorldId>().SetId("lever_test");
            var lever = go.AddComponent<TestLever>();
            go.SetActive(true);
            Assert.AreEqual(go.GetComponent<WorldId>(), WorldId.Find("lever_test"));

            lever.pulled = true;
            Assert.IsTrue(SaveManager.I.Save(3));
            lever.pulled = false;
            new WorldStateStore().RestoreState(SaveManager.Read(3).Get("world"));
            Assert.IsTrue(lever.pulled, "restored from the save file");

            Object.Destroy(go);
            yield return Frames(2);
            Assert.IsTrue(WorldId.Find("lever_test") == null, "unregistered when destroyed");
        }

        [UnityTest]
        public IEnumerator KeyPressCastsThroughTheInputActions()
        {
            // the editor only routes keyboards to Play Mode while the Game view has focus
            // and drops keyboards while the (batchmode) application has no focus
            var focus = InputSystem.settings.editorInputBehaviorInPlayMode;
            var background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var kb = InputSystem.AddDevice<Keyboard>("TestKeyboard");
            try
            {
                int casts = 0;
                GameManager.I.player.skills.SkillCast += slot => { if (slot == 0) casts++; };
                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Q));
                yield return Frames(2);
                var skill1 = InputReader.Asset.FindAction("Gameplay/Skill1", true);
                string where = $"key down={kb.qKey.isPressed}, action pressed={skill1.IsPressed()}, enabled={skill1.enabled}, " +
                               $"state={GameManager.I.State}, updates={InputSystem.settings.updateMode}";
                InputSystem.QueueStateEvent(kb, new KeyboardState());
                yield return Frames(2);
                Assert.AreEqual(1, casts, "Q casts slot 0 once; " + where);
            }
            finally
            {
                InputSystem.RemoveDevice(kb);
                InputSystem.settings.editorInputBehaviorInPlayMode = focus;
                InputSystem.settings.backgroundBehavior = background;
            }
        }

        [UnityTest]
        public IEnumerator PortedAbilitiesHitLikeThePrototype()
        {
            var hero = GameManager.I.player;
            var db = GameManager.I.db;
            float Hit(string ability, System.Func<AbilityDef, HitSpec> pick)
            {
                var a = db.Ability(ability);
                var ctx = new AbilityContext { ability = a, caster = hero, level = 1, origin = hero.transform.position };
                return pick(a).Make(ctx, Vector2.zero, Vector2.right).amount;
            }
            // prototype numbers: Chém Gió 22, Cầu Lửa 46, Mũi Băng 30, Lôi Phạt 52, Bão Kiếm 11 per tick
            var slash = (ComboEffect)db.Ability("slash").effects[0];
            Assert.AreEqual(22f, Hit("slash", a => ((DamageEffect)slash.stages[0].effects[1]).hit), 0.2f);
            Assert.AreEqual(22f * 1.7f, Hit("slash", a => ((DamageEffect)slash.stages[2].effects[1]).hit), 0.3f);
            Assert.AreEqual(46f, Hit("fireball", a => ((ProjectileEffect)a.effects[1]).hit), 0.2f);
            Assert.AreEqual(30f, Hit("ice", a => ((DamageEffect)((LineEffect)a.effects[1]).each[1]).hit), 0.2f);
            Assert.AreEqual(52f, Hit("lightning", a => ((DamageEffect)((BurstEffect)a.effects[1]).each[1]).hit), 0.2f);
            Assert.AreEqual(11f, Hit("bladestorm", a => ((DamageEffect)((PulseEffect)a.effects[1]).each[0]).hit), 0.1f);
            Assert.NotNull(((ProjectileEffect)db.Ability("fireball").effects[1]).prefab, "fireball has its projectile");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ANewAbilityMadeOnlyFromData()
        {
            // what a designer does in the Inspector: a new AbilityDef with one Damage block
            var nova = ScriptableObject.CreateInstance<AbilityDef>();
            nova.id = "test_nova";
            nova.displayName = "Tân Tinh";
            nova.targeting = AbilityTargeting.Self;
            nova.cooldown = 1f;
            nova.effects.Add(new CueEffect { at = new Anchor(Anchor.From.Caster), vfx = "fire_explosion" });
            nova.effects.Add(new DamageEffect { at = new Anchor(Anchor.From.Caster), radius = 3f, hit = new HitSpec { power = 2f, type = DamageType.Fire } });

            var hero = GameManager.I.player;
            var enemy = EnemyBase.All.Find(e => !e.IsDead);
            Assert.NotNull(enemy);
            hero.motor.Teleport((Vector2)enemy.transform.position + Vector2.left);
            yield return Frames(2);
            float before = enemy.health.hp;
            hero.skills.slots[6] = nova;
            Assert.IsTrue(hero.skills.TryCast(6, enemy.transform.position));
            yield return Frames(1);
            Assert.Less(enemy.health.hp, before, "the data-only ability dealt damage");
            Assert.GreaterOrEqual(before - enemy.health.hp, 40f, "about 2 × Attack 24.5");
            Object.Destroy(nova);
        }

        [UnityTest]
        public IEnumerator ShieldBuffCutsDamageThenExpires()
        {
            var hero = GameManager.I.player;
            Assert.IsTrue(hero.skills.TryCast(5, hero.transform.position), "Khiên Thánh");
            yield return Frames(1);
            Assert.IsTrue(hero.HasBuff("shield"));
            Assert.AreEqual(0.4f, hero.health.damageTakenMultiplier, 1e-4f);
            Assert.IsTrue(hero.status.stunImmune);
            float hp = hero.health.hp;
            hero.health.TakeDamage(DamageInfo.Make(50, Team.Enemy, null, hero.transform.position, Vector2.up));
            Assert.Less(hp - hero.health.hp, 21f, "60% less damage");

            yield return GameSeconds(5.3f);
            Assert.IsFalse(hero.HasBuff("shield"));
            Assert.AreEqual(1f, hero.health.damageTakenMultiplier, 1e-4f);
            Assert.IsFalse(hero.status.stunImmune);
        }

        [UnityTest]
        public IEnumerator CoreAndZoneAreSeparateScenes()
        {
            Assert.AreEqual("Core", GameManager.I.gameObject.scene.name);
            Assert.AreEqual("Core", GameManager.I.player.gameObject.scene.name, "the hero lives in Core");
            var zone = ZoneRoot.Current;
            Assert.AreEqual("RungThiTham", zone.gameObject.scene.name);
            Assert.AreEqual(zone.gameObject.scene, UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "zone is the active scene");
            Assert.AreEqual(zone.gameObject.scene, BossBear.All[0].gameObject.scene, "the boss belongs to the zone");
            Assert.NotNull(GameManager.I.respawnPoint, "spots come from the zone");
            Assert.AreEqual(zone.bounds, CameraRig.I.worldBounds);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TravelReloadsTheZoneAtAnEntry()
        {
            VFX.Spawn("hit_spark", Vector3.zero, Quaternion.identity);   // makes the pool root
            yield return Frames(2);
            var zone = ZoneRoot.Current.def;
            SceneLoader.I.Travel(zone, "boss");
            Assert.IsTrue(SceneLoader.I.Busy);
            float deadline = Time.realtimeSinceStartup + 20f;
            while (SceneLoader.I.Busy && Time.realtimeSinceStartup < deadline) yield return null;
            yield return Frames(2);
            Assert.NotNull(ZoneRoot.Current);
            var boss = GameManager.I.bossSpot;
            Assert.Less(Vector2.Distance(GameManager.I.player.transform.position, boss.position), 0.1f, "entered at the boss spot");
            Assert.AreEqual(1, BossBear.All.Count, "old zone unloaded, new one loaded");
            Assert.IsNotNull(VFX.Spawn("hit_spark", Vector3.zero, Quaternion.identity), "pool still works after the swap");
        }

        [UnityTest]
        public IEnumerator EnemiesDissolveAndOutline()
        {
            var enemy = EnemyBase.All.Find(e => !e.IsDead);
            Assert.NotNull(enemy.style, "enemy prefab has a SpriteStyle");
            Assert.IsTrue(enemy.style.Supported, "body uses RPG/Sprite Lit FX");
            enemy.style.SetOutline(true, Color.red);
            yield return GameSeconds(0.3f);
            Assert.AreEqual(1f, enemy.style.OutlineAmount, 1e-3f);
            enemy.style.SetOutline(false, Color.red);
            enemy.health.Kill();
            yield return GameSeconds(1.2f + 0.8f);
            Assert.IsFalse(enemy.gameObject.activeSelf, "dissolved and despawned");
            Assert.AreEqual(1f, enemy.style.DissolveAmount, 1e-3f);
        }

        [UnityTest]
        public IEnumerator DebugConsoleCommands()
        {
            var c = DebugConsole.I;
            Assert.NotNull(c, "console on the game manager");
            c.Execute("level 4");
            Assert.AreEqual(4, PlayerStats.I.level);
            int red = Inventory.I.Count("potion_red");
            c.Execute("give potion_red 2");
            Assert.AreEqual(red + 2, Inventory.I.Count("potion_red"));
            c.Execute("tp boss");
            Assert.Less(Vector2.Distance(GameManager.I.player.transform.position, GameManager.I.bossSpot.position), 0.1f);
            c.Execute("tp spawn");
            c.Execute("quest talk_chief status");
            c.Execute("no_such_command");

            c.Execute("ttk");
            var enemy = EnemyBase.All.Find(e => !e.IsDead);
            var hero = GameManager.I.player.gameObject;
            enemy.health.TakeDamage(DamageInfo.Make(1, Team.Player, hero, enemy.transform.position, Vector2.up));
            yield return GameSeconds(0.2f);
            enemy.health.Kill();
            yield return null;
            c.Execute("ttk");   // prints the result
            c.Execute("hitbox");
            Assert.IsTrue(c.ShowHitboxes);
            c.Execute("hitbox");
            yield return null;
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

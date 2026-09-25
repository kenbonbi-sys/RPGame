using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// Hắc Phong, the rest of Thảo Nguyên Gió (T62), in the real world scene played offline: the
    /// abandoned fields' living scarecrows, the bandits' archers and blades, Bò Rừng Sắt and Thủ Lĩnh
    /// Hắc Phong on his windmill hill, and the quest road through them.
    /// </summary>
    public class HacPhongTests
    {
        string tempSaves;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempSaves = Path.Combine(Path.GetTempPath(), "rtt_test_saves_hacphong");
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
            Wind.Override = null;
            yield return new ExitPlayMode();
            SaveManager.FolderOverride = null;
            Health.SpreadRoll = () => Random.value;
            if (Directory.Exists(tempSaves)) Directory.Delete(tempSaves, true);
        }

        static Vector2 Spot(string id)
        {
            var t = ZoneRoot.Current.SpotOf(id);
            Assert.NotNull(t, id + " spot");
            return t.position;
        }

        static EnemyBase Nearest(string id, Vector2 near)
        {
            EnemyBase best = null;
            foreach (var e in EnemyBase.All)
                if (e.enemyId == id && !e.IsDead && (best == null ||
                    Vector2.Distance(e.transform.position, near) < Vector2.Distance(best.transform.position, near)))
                    best = e;
            return best;
        }

        /// <summary>Waits game seconds (WaitForSeconds does not wait in these tests).</summary>
        static IEnumerator Seconds(float s)
        {
            float end = Time.time + s;
            float deadline = Time.realtimeSinceStartup + s + 10f;
            while (Time.time < end)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "game time must advance while waiting");
                yield return null;
            }
        }

        static IEnumerator Until(System.Func<bool> done, float seconds)
        {
            float end = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds + 10f;
            while (!done() && Time.time < end && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static PlayerController Hero()
        {
            var hero = Players.Local;
            hero.stats.look = new HeroLook();
            CharacterChoice.Apply(hero, new HeroLook { cls = "fighter", race = "human", weapon = "sword" });
            return hero;
        }

        /// <summary>Only <paramref name="keep"/> (and bosses) stay awake: nothing else wanders into the test.</summary>
        static void Alone(params Component[] keep)
        {
            foreach (var e in EnemyBase.All.ToArray())
                if (System.Array.IndexOf(keep, e) < 0) e.gameObject.SetActive(false);
        }

        /// <summary>
        /// A way out of <paramref name="from"/> clear of everything solid (tents, banners, fences) for
        /// <paramref name="length"/> units and a body's width: a dash or an arrow flies it unhindered.
        /// </summary>
        static Vector2 OpenLane(Vector2 from, float length, Component self)
        {
            for (int k = 0; k < 16; k++)
            {
                Vector2 dir = Util.FromAngle(k * 22.5f);
                if (Clear(from, dir, length, self)) return dir;
            }
            Assert.Fail("no open lane around " + from);
            return Vector2.right;
        }

        /// <summary>Nothing solid but heroes and <paramref name="self"/> along a body-wide lane.</summary>
        static bool Clear(Vector2 from, Vector2 dir, float length, Component self)
        {
            var own = self != null ? self.GetComponentsInChildren<Collider2D>() : new Collider2D[0];
            var hits = new RaycastHit2D[8];
            var filter = new ContactFilter2D();
            filter.SetLayerMask(Layers.ObstacleMask);
            filter.useTriggers = false;
            int n = Physics2D.CircleCast(from + Vector2.up * 0.3f, 0.5f, dir, filter, hits, length);
            for (int i = 0; i < n; i++)
                if (System.Array.IndexOf(own, hits[i].collider) < 0 && hits[i].collider.GetComponentInParent<PlayerController>() == null) return false;
            return true;
        }

        static DamageInfo HeroBlow(Vector2 from, Vector2 at) =>
            DamageInfo.Make(100f, Team.Player, null, at, (at - from).normalized, DamageType.Physical, 0f);

        [UnityTest]
        public IEnumerator TheBanditsHoldTheWestOfTheSteppe()
        {
            foreach (var id in new[] { "fields", "ironbison", "hacphong", "windmillhill" }) Spot(id);
            Assert.NotNull(Waystone.Find("coixay"), "a waystone at the foot of the windmill hill");
            Assert.AreEqual(4, EnemyBase.All.FindAll(e => e.enemyId == "scarecrow").Count, "four living scarecrows in the fields");
            Assert.GreaterOrEqual(EnemyBase.All.FindAll(e => e.enemyId == "hp_archer").Count, 5, "archers enough for their bounty");
            Assert.GreaterOrEqual(EnemyBase.All.FindAll(e => e.enemyId == "hp_blade").Count, 5, "blades enough for theirs");
            var ravine = ZoneRoot.Current;
            foreach (var e in EnemyBase.All)
                if (e.enemyId == "hp_archer" || e.enemyId == "hp_blade")
                    Assert.Less(e.transform.position.x, 45f, e.name + " lives west of Khe Vực");
            var bison = BossBase.Find("ironbison") as BossIronBison;
            Assert.NotNull(bison, "Bò Rừng Sắt");
            Assert.AreEqual(EnemyRank.MiniBoss, bison.rank);
            var chief = BossBase.Find("blackwind") as BossBlackWind;
            Assert.NotNull(chief, "Thủ Lĩnh Hắc Phong");
            Assert.NotNull(chief.archers, "his archers wait below the hill");
            Assert.AreEqual(4, chief.archers.members.Count);
            Assert.AreEqual(0, chief.archers.Out, "not called yet");
            Assert.NotNull(chief.hill, "the hill's area");
            Assert.AreEqual("Đồi Cối Xay", chief.hill.zoneName);
            Assert.AreEqual(12f, chief.hill.windCycle, "on the hilltop the wind turns every 12 s");
            Assert.NotNull(Object.FindAnyObjectByType<Windmill>(), "the windmill on the hill");
            // the bandits are drawn as people
            var archer = Nearest("hp_archer", Spot("hacphong"));
            var dressed = archer.GetComponent<HeroLookEnemy>();
            Assert.NotNull(dressed);
            Assert.AreEqual("bow", dressed.look.weapon);
            Assert.AreNotEqual(dressed.look.ToJson(), new HeroLook().ToJson());
            Assert.IsTrue(archer.anim.set.name.EndsWith("_dressed"), "this screen dressed it in its look");
            var own = AssetFactory.Database.quests.Find(q => q.id == "hacphong_archers");
            Assert.NotNull(own);
            var first = GameManager.I.db.quests.Find(q => q.id == "steppe_scarecrows");
            Assert.IsTrue(first.requires.Exists(r => r != null && r.id == "steppe_bisons"), "after the bisons");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AScarecrowStandsStillThenWakes()
        {
            var hero = Hero();
            hero.health.invulnerable = true;
            Wind.Override = Vector2.zero;
            var sc = Nearest("scarecrow", Spot("fields")) as ScarecrowAI;
            Assert.NotNull(sc);
            Alone(sc);
            Vector2 at = sc.transform.position;
            // walking by at a distance: it only turns its head
            hero.motor.Teleport(at + new Vector2(-5f, 0f));
            yield return Seconds(0.6f);
            Assert.IsTrue(sc.Still, "it stands still while nobody comes close");
            Assert.IsTrue(sc.body.flipX, "its head follows the hero on its left");
            Assert.AreEqual("still", sc.anim.Current);
            // the tell: the straw scarecrows sway in the wind, the living one does not
            GameObject straw = null;
            foreach (Transform t in ZoneRoot.Current.obstacles)
                if (t.name.StartsWith("scarecrow")) { straw = t.gameObject; break; }
            Assert.NotNull(straw, "straw scarecrows stand in the fields");
            Assert.AreEqual("RPG/Sprite Lit Wind", straw.transform.Find("Sprite").GetComponent<SpriteRenderer>().sharedMaterial.shader.name, "the straw ones sway");
            Assert.AreNotEqual("RPG/Sprite Lit Wind", sc.body.sharedMaterial.shader.name, "the living one does not");
            // struck while it pretends: a deeper blow
            Assert.AreEqual(sc.surpriseBonus, sc.health.Guard(HeroBlow(hero.transform.position, at)), 0.001f, "Bất ngờ!");
            // too close: it wakes
            hero.motor.Teleport(at + new Vector2(-1.8f, 0f));
            yield return Until(() => !sc.Still, 2f);
            Assert.IsFalse(sc.Still, "it wakes when a hero comes close");
            Assert.AreEqual(1f, sc.health.Guard(HeroBlow(hero.transform.position, at)), 0.001f, "awake, a blow is a blow");
            yield return Seconds(1.5f);
            Assert.AreNotEqual("still", sc.anim.Current, "it fights");
        }

        [UnityTest]
        public IEnumerator AScarecrowHopsOntoItsMark()
        {
            var hero = Hero();
            hero.health.hp = hero.health.maxHp = 5000f;
            Wind.Override = Vector2.zero;
            var sc = Nearest("scarecrow", Spot("fields")) as ScarecrowAI;
            Alone(sc);
            Vector2 post = sc.transform.position;
            Vector2 away = OpenLane(post, 5f, sc);
            hero.motor.Teleport(post + away * 4f);
            yield return Seconds(0.3f);
            bool hop = false, spin = false;
            hero.health.Damaged += (d, amount) =>
            {
                if (d.skillName == "Nhảy Vồ") hop = true;
                if (d.skillName == "Liềm Xoay") spin = true;
            };
            sc.DebugAttack(hero, true);
            float top = 0f;
            float until = Time.time + 3f;
            while (Time.time < until && !hop)
            {
                top = Mathf.Max(top, sc.lift.localPosition.y);
                yield return null;
            }
            Assert.IsTrue(hop, "Nhảy Vồ lands on its mark");
            yield return Seconds(0.5f);
            Assert.AreEqual(0f, sc.lift.localPosition.y, 0.001f, "down on the ground again");
            hero.motor.Teleport((Vector2)sc.transform.position + away * 1.2f);
            yield return null;
            sc.DebugAttack(hero, false);
            yield return Until(() => spin, 3f);
            Assert.IsTrue(spin, "Liềm Xoay around itself");
            Assert.Greater(top, 0.5f, "it hopped high on its lift (every screen draws that)");
        }

        [UnityTest]
        public IEnumerator AnArcherReadsTheWind()
        {
            // the aim into the wind carries the shot onto its mark
            Wind.Override = new Vector2(1f, 0f);
            var camp = Spot("hacphong");
            Assert.Greater(Wind.At(camp).x, 0.9f);
            Vector2 from = camp, to = camp + new Vector2(0f, 8f);
            Vector2 dir = EnemyShots.IntoTheWind(from, to, 13f);
            Vector2 flight = dir * 13f + Wind.At(from) * Wind.ShotDrift;
            Assert.Less(Vector2.Angle(flight, to - from), 0.5f, "blown back onto the line");
            Assert.Less(dir.x, -0.1f, "it aims upwind");

            var hero = Hero();
            hero.health.hp = hero.health.maxHp = 5000f;
            hero.motor.windResponse = 0f;   // the hero stands firm: only the arrow feels the wind
            var archer = Nearest("hp_archer", camp) as BanditArcherAI;
            Assert.NotNull(archer);
            Alone(archer);
            Vector2 a = archer.transform.position;
            // across the wind, in a lane clear of the camp's tents and banners
            Vector2 lane = Clear(a, Vector2.up, 6.5f, archer) ? Vector2.up : Clear(a, Vector2.down, 6.5f, archer) ? Vector2.down : Vector2.zero;
            if (lane == Vector2.zero)
            {
                // take the archer out to open grass first
                for (int r = 6; r <= 20 && lane == Vector2.zero; r += 2)
                    for (int k = 0; k < 12 && lane == Vector2.zero; k++)
                    {
                        var spot = Spot("hacphong") + Util.FromAngle(k * 30f) * r;
                        if (ZoneRoot.Current.IsWall(spot) || ZoneRoot.Current.IsWall(spot + Vector2.up * 6f) || !Clear(spot, Vector2.up, 6.5f, archer)) continue;
                        a = spot;
                        lane = Vector2.up;
                    }
                Assert.AreNotEqual(Vector2.zero, lane, "open grass near the camp");
                archer.GetComponent<CharacterMotor>().Teleport(a);
            }
            hero.motor.Teleport(a + lane * 6f);
            yield return Seconds(0.3f);
            hero.Autopilot = true;
            float before = hero.health.hp;
            bool arrow = false;
            hero.health.Damaged += (d, amount) => { if (d.skillName == "Mũi Tên Đón Gió") arrow = true; };
            archer.DebugShoot(hero);
            yield return Until(() => arrow, 3f);
            hero.Autopilot = false;
            Assert.IsTrue(arrow, "the arrow shot into a crosswind still hits");
            Assert.Less(hero.health.hp, before);

            // too close: it springs back
            Wind.Override = Vector2.zero;
            yield return Seconds(1f);
            Vector2 was = archer.transform.position;
            hero.motor.Teleport(was + new Vector2(1.5f, 0f));
            yield return null;
            archer.DebugLeap(hero);
            yield return Seconds(0.5f);
            Assert.Greater(Vector2.Distance(archer.transform.position, hero.transform.position), 3.2f, "it sprang out of reach");
        }

        [UnityTest]
        public IEnumerator ABladeDashesThenStandsOpen()
        {
            var hero = Hero();
            hero.health.hp = hero.health.maxHp = 5000f;
            Wind.Override = Vector2.zero;
            var blade = Nearest("hp_blade", Spot("hacphong")) as BanditBladeAI;
            Assert.NotNull(blade);
            Alone(blade);
            Vector2 b = blade.transform.position;
            Vector2 side = OpenLane(b, 7f, blade);
            hero.motor.Teleport(b + side * 4.2f);
            yield return Seconds(0.3f);
            int dash = 0, slashes = 0;
            hero.health.Damaged += (d, amount) =>
            {
                if (d.skillName == "Lướt Chém") dash++;
                if (d.skillName == "Chém Liên Hoàn") slashes++;
            };
            blade.DebugCombo(hero);
            yield return Until(() => blade.Open, 4f);
            Assert.AreEqual(1, dash, "the dash cuts through once");
            Assert.GreaterOrEqual(slashes, 1, "then the combo");
            Assert.IsTrue(blade.Open, "then it stands open");
            Assert.AreEqual(blade.recoverBonus, blade.health.Guard(HeroBlow(hero.transform.position, blade.transform.position)), 0.001f, "Hở sườn!");
            yield return Seconds(blade.recoverSeconds + 0.2f);
            Assert.IsFalse(blade.Open);
            Assert.AreEqual(1f, blade.health.Guard(HeroBlow(hero.transform.position, blade.transform.position)), 0.001f);
        }

        [UnityTest]
        public IEnumerator TheIronBisonRamsSandstoneAndItsPlatesSpringLoose()
        {
            var hero = Hero();
            hero.health.invulnerable = true;
            Wind.Override = Vector2.zero;
            var bison = BossBase.Find("ironbison") as BossIronBison;
            Assert.NotNull(bison);
            Alone();
            Vector2 c = bison.Home;
            // its iron front turns a blow aside, its flank does not
            bison.body.flipX = false;
            Vector2 at = bison.transform.position;
            Assert.AreEqual(bison.frontGuard, bison.health.Guard(HeroBlow(at + Vector2.right * 2f, at)), 0.001f, "Giáp sắt!");
            Assert.AreEqual(bison.flankBonus, bison.health.Guard(HeroBlow(at + Vector2.left * 2f, at)), 0.001f, "its rump");
            // between it and a block of sandstone
            hero.motor.Teleport(c + Util.FromAngle(30f) * 3.4f);
            yield return Seconds(0.4f);
            Assert.IsTrue(bison.DebugForce("charge"));
            yield return Until(() => bison.Dazed, 4f);
            Assert.IsTrue(bison.Dazed, "it rams the sandstone");
            Assert.IsTrue(bison.status.IsStunned, "and reels");
            Assert.AreEqual(bison.dazedBonus, bison.health.Guard(HeroBlow(at + Vector2.right * 2f, (Vector2)bison.transform.position)), 0.001f,
                            "Giáp bung!: every side is open");
        }

        [UnityTest]
        public IEnumerator TheChiefsHillHasItsOwnWindAndHisRageRaisesTheSand()
        {
            var hero = Hero();
            hero.health.invulnerable = true;
            var chief = BossBase.Find("blackwind") as BossBlackWind;
            Assert.NotNull(chief);
            Alone();
            Vector2 hill = chief.Home;
            Assert.AreEqual(Wind.CurrentFor(12f), Wind.At(hill), "the hill's wind turns on its own 12 s count");
            Assert.AreEqual(Wind.Current, Wind.At(hill + new Vector2(0f, -20f)), "below it, the steppe's");
            Assert.AreEqual(0f, ZoneArea.StormAt(hill), "no sand before his rage");
            hero.motor.Teleport(hill + new Vector2(0f, -4f));
            yield return Until(() => chief.Engaged, 4f);
            Assert.IsTrue(chief.Engaged, "he wakes for a hero on his hill");
            chief.health.hp = chief.health.maxHp * 0.45f;
            yield return Until(() => chief.Enraged, 8f);
            Assert.IsTrue(chief.Enraged);
            yield return Seconds(2.6f);
            Assert.Greater(ZoneArea.StormAt(hill), 0.8f, "Bão Cát");
            ZoneArea.MoodAt(hill, out var tint, out float mist, out _);
            Assert.Greater(mist, 0.7f, "the sand hides the hill");
            Assert.Less(tint.b, 0.8f, "in its colour");
            chief.health.Kill();
            yield return Seconds(2.5f);
            Assert.Less(ZoneArea.StormAt(hill), 0.2f, "the sand settles when he falls");
        }

        [UnityTest]
        public IEnumerator DodgingTheChiefsThirdRunThrowsHimOffBalance()
        {
            var hero = Hero();
            hero.health.hp = hero.health.maxHp = 50000f;
            Wind.Override = Vector2.zero;
            var chief = BossBase.Find("blackwind") as BossBlackWind;
            Alone();
            Vector2 hill = chief.Home;
            hero.motor.Teleport(hill + new Vector2(0f, -4f));
            yield return Until(() => chief.Engaged, 4f);
            yield return Seconds(2.5f);   // the intro
            // runs that land do not unbalance him
            int runs = 0;
            hero.health.Damaged += (d, amount) => { if (d.skillName != null && d.skillName.StartsWith("Lướt Gió")) runs++; };
            Assert.IsTrue(chief.DebugForce("dashes"));
            yield return Seconds(3.5f);
            Assert.GreaterOrEqual(runs, 2, "his runs cut through the hero");
            Assert.IsFalse(chief.OffBalance, "taking the runs does nothing to him");
            // the third run dodged (a Lướt's invulnerability turns it aside): he reels
            yield return Seconds(1f);
            hero.motor.Teleport(hill + new Vector2(0f, -4f));
            hero.health.invulnerable = true;
            Assert.IsTrue(chief.DebugForce("dashes"));
            yield return Until(() => chief.OffBalance, 5f);
            hero.health.invulnerable = false;
            Assert.IsTrue(chief.OffBalance, "Mất thăng bằng!");
            Assert.IsTrue(chief.status.IsStunned);
            var d2 = HeroBlow(hero.transform.position, chief.transform.position);
            Assert.AreEqual(chief.offBalanceBonus, chief.health.Guard(d2), 0.001f, "every blow bites deeper");
        }

        [UnityTest]
        public IEnumerator TheChiefCallsHisArchersAndSendsThemAwayForHisDuel()
        {
            var hero = Hero();
            hero.health.hp = hero.health.maxHp = 50000f;
            Wind.Override = Vector2.zero;
            var chief = BossBase.Find("blackwind") as BossBlackWind;
            Alone();
            hero.motor.Teleport(chief.Home + new Vector2(0f, -4f));
            yield return Until(() => chief.Engaged, 4f);
            yield return Seconds(2.5f);
            Assert.IsTrue(chief.DebugForce("archers"));
            yield return Until(() => chief.archers.Out >= 2, 3f);
            Assert.AreEqual(2, chief.archers.Out, "two archers come up the hill");
            Assert.IsTrue(chief.DebugForce("duel"));
            yield return Seconds(0.3f);
            Assert.AreEqual(0, chief.archers.Out, "they withdraw for the duel");
            Assert.IsTrue(chief.Dueling);
            Assert.AreEqual(hero, chief.Challenged);
        }

        [UnityTest]
        public IEnumerator AWhirlwindThrowsWhoeverItRunsOverAndGoesOn()
        {
            var hero = Hero();
            hero.health.hp = hero.health.maxHp = 5000f;
            Wind.Override = Vector2.zero;
            Alone();
            Vector2 at = Spot("windmillhill") + new Vector2(0f, -3f);
            hero.motor.Teleport(at);
            yield return Seconds(0.3f);
            float before = hero.health.hp;
            var pr = EnemyShots.Tornado(null, at + Vector2.left * 3f, Vector2.right, 30f, 4f, 3f, 0.7f, "Lốc Xoáy");
            Assert.NotNull(pr, "the whirlwind's prefab");
            Assert.IsTrue(pr.pierce);
            yield return Until(() => hero.health.hp < before, 2f);
            Assert.Less(hero.health.hp, before, "it runs the hero over");
            Assert.IsTrue(hero.status.IsStunned, "and throws them");
            yield return Seconds(0.6f);
            Assert.IsTrue(pr.gameObject.activeInHierarchy, "it goes on after them");
            Assert.Greater(pr.transform.position.x, at.x + 0.5f, "on past where the hero stood");
            // the wind drifts it like any shot
            Wind.Override = new Vector2(0f, 1f);
            float y0 = pr.transform.position.y;
            yield return Seconds(0.5f);
            Assert.Greater(pr.transform.position.y - y0, 0.6f, "the wind carries it");
        }
    }
}

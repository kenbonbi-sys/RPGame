using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// Sách Chiêu (T63) in the real world scene played offline: the Băng, Lôi and Ám schools have
    /// six spells each with a Tuyệt kỹ, a book and a boss that keeps it; a Bí Kíp is read only by
    /// the classes whose D&amp;D list has the spell; a learned spell takes a bar slot (W E R A S, or
    /// D for a Tuyệt kỹ) out of a fight, keeps its cooldown when moved, and is saved with the
    /// hero; and each new spell does what it says on dummies off the map.
    /// </summary>
    public class SpellbookTests
    {
        string tempSaves;
        static readonly Vector2 Arena = new Vector2(500f, 520f);   // an empty spot outside the map

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempSaves = Path.Combine(Path.GetTempPath(), "rtt_test_saves_spellbook");
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

        static PlayerController Hero(string cls)
        {
            var hero = Players.Local;
            hero.stats.look = new HeroLook();
            CharacterChoice.Apply(hero, new HeroLook { cls = cls, race = "human", weapon = GameManager.I.db.Class(cls).DefaultWeapon });
            return hero;
        }

        /// <summary>Knows these spells (as if their books were read), nothing on the bar yet.</summary>
        static void Knows(PlayerController hero, params string[] ids)
        {
            var look = hero.stats.look.Clone();
            look.spells = ids;
            look.bar = new string[0];
            hero.stats.SetLook(look);
        }

        /// <summary>Puts a known spell straight on a slot (skipping the out-of-combat rule).</summary>
        static void OnBar(PlayerController hero, int slot, string id)
        {
            var look = hero.stats.look.Clone();
            var bar = new string[Spellbook.UltimateSlot];
            for (int i = 0; i < bar.Length; i++) bar[i] = "";
            bar[slot - 1] = id;
            look.bar = bar;
            hero.stats.SetLook(look);
            Assert.AreEqual(id, hero.skills.slots[slot].id, $"{id} is on slot {slot}");
        }

        /// <summary>Enemies turned into still dummies with a lot of health, in a row east of the arena.</summary>
        static List<EnemyBase> Dummies(int n)
        {
            var list = EnemyBase.All.FindAll(e => !e.IsDead && !e.health.invulnerable && e.enemyId == "slime");
            if (list.Count < n) list = EnemyBase.All.FindAll(e => !e.IsDead && !e.health.invulnerable && e.motor != null);
            Assert.GreaterOrEqual(list.Count, n, "enough enemies to stand in");
            list = list.GetRange(0, n);
            for (int i = 0; i < n; i++)
            {
                var e = list[i];
                e.enabled = false;   // no thinking: they stand still
                e.health.maxHp = e.health.hp = 100000f;
            }
            Line(list);
            return list;
        }

        /// <summary>Back in their row.</summary>
        static void Line(List<EnemyBase> dummies)
        {
            for (int i = 0; i < dummies.Count; i++) dummies[i].motor.Teleport(Arena + new Vector2(3f + i * 1.8f, (i % 2) * 0.4f));
        }

        static void Heal(List<EnemyBase> dummies)
        {
            foreach (var e in dummies)
            {
                e.health.hp = e.health.maxHp;
                e.status.Cleanse();
            }
        }

        static bool Hurt(EnemyBase e) => e.health.hp < e.health.maxHp - 0.5f;

        static IEnumerator Cast(PlayerController hero, int slot, Vector2 aim, float wait)
        {
            hero.motor.Teleport(Arena);
            hero.energy = hero.maxEnergy;
            hero.skills.ResetCooldowns();
            yield return GameSmokeTests.GameSeconds(0.05f);
            Assert.IsTrue(hero.skills.TryCast(slot, aim), $"{hero.skills.slots[slot].id} casts");
            yield return GameSmokeTests.GameSeconds(wait);
        }

        // ================================================================== the schools
        [UnityTest]
        public IEnumerator TheThreeSchoolsHaveSixSpellsABookAndAKeeperEach()
        {
            var db = GameManager.I.db;
            Assert.AreEqual(18, Spellbook.All.Length, "eighteen spells (plan §06)");
            foreach (Spellbook.School school in System.Enum.GetValues(typeof(Spellbook.School)))
            {
                var of = System.Array.FindAll(Spellbook.All, e => e.school == school);
                Assert.AreEqual(6, of.Length, $"six spells of {school}");
                Assert.AreEqual(1, System.Array.FindAll(of, e => Spellbook.IsUltimate(e.id)).Length, $"one Tuyệt kỹ of {school}");
            }
            foreach (var e in Spellbook.All)
            {
                var a = db.Ability(e.id);
                Assert.NotNull(a, e.id);
                Assert.NotNull(a.icon, e.id + " has an icon");
                Assert.IsTrue(a.effects.Count > 0, e.id + " does something");
                var tome = db.Item(e.TomeId);
                Assert.NotNull(tome, e.TomeId);
                Assert.AreEqual(ItemKind.Tome, tome.kind);
                Assert.NotNull(tome.icon, e.TomeId + " has an icon");
                Assert.AreSame(e, Spellbook.OfTome(tome));
                foreach (var c in e.classes) Assert.NotNull(db.Class(c), $"{e.id}: class {c}");
                var loot = new List<LootEntry>();
                Spellbook.AddBossBooks(BossId(e.from), loot);
                Assert.IsTrue(loot.Exists(l => l.itemId == e.TomeId), $"{e.from} keeps {e.TomeId}");
            }
            // every class finds at least three spells of its own list
            foreach (var cls in db.classes)
            {
                int n = System.Array.FindAll(Spellbook.All, e => Spellbook.ClassMay(cls.id, e)).Length;
                Assert.GreaterOrEqual(n, 3, cls.id + " can learn a few");
            }
            Assert.IsFalse(Spellbook.ClassMay("fighter", Spellbook.Find("chainlightning")), "no Xích Lôi for a fighter");
            Assert.IsTrue(Spellbook.ClassMay("sorcerer", Spellbook.Find("chainlightning")), "a sorcerer's Chain Lightning");
            yield return null;
        }

        static string BossId(string name)
        {
            switch (name)
            {
                case "Cóc Tía": return "toadking";
                case "Xà Mẫu": return "snake";
                case "Golem Pha Lê Cổ": return "crystalgolem";
                case "Nhện Chúa Pha Lê": return "spiderqueen";
                case "Bò Rừng Sắt": return "ironbison";
                default: return "blackwind";
            }
        }

        // ================================================================== learning and the bar
        [UnityTest]
        public IEnumerator ABookIsReadOnlyByTheClassesThatHaveItsSpell()
        {
            var hero = Hero("fighter");
            var db = GameManager.I.db;
            var bag = hero.inventory;
            var chain = db.Item("tome_chainlightning");
            var armor = db.Item("tome_frostarmor");
            // a boss's chest rolls only books this hero may still learn
            var table = new List<LootEntry>
            {
                new LootEntry { itemId = chain.id, chance = 1f },
                new LootEntry { itemId = armor.id, chance = 1f },
            };
            var rolled = Loot.RollList(table, hero);
            Assert.AreEqual(1, rolled.Count, "no Xích Lôi for a fighter");
            Assert.AreEqual(armor, rolled[0].item);

            bag.Add(chain, 1, false);
            bag.Add(armor, 2, false);

            Assert.IsFalse(hero.UseItem(chain), "a fighter cannot read Xích Lôi");
            Assert.AreEqual(1, bag.Count(chain), "the book stays in the bag");
            Assert.IsFalse(Spellbook.Knows(hero.stats.look, "chainlightning"));

            Assert.IsTrue(hero.UseItem(armor), "Giáp Sương is on a fighter's list");
            Assert.IsTrue(Spellbook.Knows(hero.stats.look, "frostarmor"));
            Assert.AreEqual(1, bag.Count(armor), "one book read");
            Assert.IsFalse(hero.UseItem(armor), "learned once");
            Assert.AreEqual(1, bag.Count(armor), "the second book is kept");
            Assert.AreEqual(0, Loot.RollList(table, hero).Count, "nor a book already learned");
            Assert.AreEqual("parry", hero.skills.slots[4].id, "learning alone changes nothing on the bar");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ALearnedSpellTakesASlotOutOfAFightAndKeepsItsCooldown()
        {
            var hero = Hero("wizard");
            var kit = hero.stats.Class.kit;
            foreach (var id in new[] { "iceprison", "iceage", "chainlightning" })
                Assert.IsNull(Spellbook.Learn(hero, Spellbook.Find(id)), id);
            Assert.IsNotNull(Spellbook.Learn(hero, Spellbook.Find("shadowknives")), "no Ám Tiễn for a wizard");
            hero.motor.Teleport(Arena);
            yield return GameSmokeTests.Frames(2);

            Assert.IsNull(Spellbook.Set(hero, 1, "iceprison"));
            Assert.AreEqual("iceprison", hero.skills.slots[1].id, "W is Ngục Băng now");
            Assert.IsNotNull(Spellbook.Set(hero, 1, "iceage"), "a Tuyệt kỹ does not go on W");
            Assert.IsNotNull(Spellbook.Set(hero, 6, "iceprison"), "D is for a Tuyệt kỹ");
            Assert.IsNotNull(Spellbook.Set(hero, 2, "blizzard"), "not learned");
            Assert.IsNull(Spellbook.Set(hero, 6, "iceage"));
            Assert.AreEqual("iceage", hero.skills.slots[6].id);
            Assert.IsNull(Spellbook.Set(hero, 2, "iceprison"), "moved to E");
            Assert.AreEqual("iceprison", hero.skills.slots[2].id);
            Assert.AreEqual(kit[1], hero.skills.slots[1].id, "W is the class's own again");

            // the cooldown goes with the spell
            hero.energy = hero.maxEnergy;
            hero.skills.ResetCooldowns();
            Assert.IsTrue(hero.skills.TryCast(6, Arena));
            yield return GameSmokeTests.GameSeconds(0.8f);
            Assert.Greater(hero.skills.Remaining(6), 15f, "Kỷ Băng Hà cools down");
            Assert.IsNull(Spellbook.Set(hero, 6, ""), "D back to the class");
            Assert.AreEqual(kit[6], hero.skills.slots[6].id);
            Assert.AreEqual(0f, hero.skills.Remaining(6), "the class's Tuyệt kỹ is ready");
            Assert.IsNull(Spellbook.Set(hero, 6, "iceage"));
            Assert.Greater(hero.skills.Remaining(6), 15f, "and Kỷ Băng Hà is not ready again for being moved");

            // not in a fight
            var hit = DamageInfo.Make(1f, Team.Enemy, null, Arena, Vector2.up, DamageType.Physical);
            hit.pure = true;
            hero.health.TakeDamage(hit);
            Assert.IsNotNull(Spellbook.Set(hero, 1, "chainlightning"), "not while fighting");
            Assert.AreEqual(kit[1], hero.skills.slots[1].id);
            yield return GameSmokeTests.GameSeconds(4.2f);
            Assert.IsNull(Spellbook.Set(hero, 1, "chainlightning"), "a few seconds later it may");

            // it is part of who the hero is: saved, copied deep, not part of the drawing
            var look = hero.stats.look;
            var back = HeroLook.FromJson(JsonUtility.ToJson(look));
            CollectionAssert.AreEqual(look.spells, back.spells);
            CollectionAssert.AreEqual(look.bar, back.bar);
            var copy = look.Clone();
            copy.spells[0] = "x";
            Assert.AreNotEqual("x", look.spells[0], "a clone does not share the list");
            var plain = look.Clone();
            plain.spells = new string[0];
            plain.bar = new string[0];
            Assert.AreEqual(plain.ArtKey(), look.ArtKey(), "spells do not change the drawing");
            var restyle = look.Clone();
            restyle.hair = (look.hair + 1) % 4;
            restyle.spells = new string[0];
            Assert.IsTrue(CharacterChoice.Apply(hero, restyle));
            Assert.IsTrue(Spellbook.Knows(hero.stats.look, "iceage"), "a new haircut does not unlearn anything");
            Assert.AreEqual("iceage", hero.skills.slots[6].id, "nor empty the bar");

            // the window shows it all
            var ui = SpellbookUI.I;
            Assert.NotNull(ui, "Sách Chiêu is on the HUD");
            yield return GameSmokeTests.GameSeconds(4.2f);
            ui.Show();
            yield return GameSmokeTests.Frames(3);
            Assert.IsTrue(ui.IsOpen);
            Assert.GreaterOrEqual(ui.GetComponentsInChildren<UiHover>(true).Length, 24, "six slots and eighteen spells");
            ui.DebugPick("chainlightning");
            yield return null;
            ui.Close();
            yield return null;
            Assert.IsFalse(ui.IsOpen);
        }

        // ================================================================== the spells
        [UnityTest]
        public IEnumerator TheIceSpellsChillFreezeAndArmor()
        {
            var hero = Hero("wizard");
            hero.health.invulnerable = true;
            Knows(hero, "frostarrows", "frostarmor", "iceprison", "blizzard", "iceage");
            var d = Dummies(3);
            Vector2 first = d[0].transform.position;

            OnBar(hero, 1, "frostarrows");
            yield return Cast(hero, 1, first, 0.9f);
            Assert.IsTrue(Hurt(d[0]), "Băng Tiễn hits");
            Assert.IsTrue(d[0].status.ChillStacks > 0 || d[0].status.IsFrozen, "and chills");

            Heal(d);
            OnBar(hero, 1, "iceprison");
            yield return Cast(hero, 1, first, 0.8f);
            Assert.IsTrue(d[0].status.IsFrozen, "Ngục Băng freezes");
            Assert.IsTrue(Hurt(d[0]));

            Heal(d);
            OnBar(hero, 1, "blizzard");
            yield return Cast(hero, 1, (Vector2)d[1].transform.position, 1.3f);
            Assert.IsTrue(Hurt(d[1]), "Bão Tuyết hurts");
            Assert.IsTrue(d[1].status.ChillStacks > 0 || d[1].status.IsFrozen, "and chills");
            yield return GameSmokeTests.GameSeconds(4f);

            Heal(d);
            OnBar(hero, 6, "iceage");
            // right after the blast: they were frozen a few times already, so each freeze is shorter (diminishing crowd control)
            yield return Cast(hero, 6, Arena, 0.35f);
            foreach (var e in d)
            {
                Assert.IsTrue(Hurt(e), "Kỷ Băng Hà reaches everyone within 7");
                Assert.IsTrue(e.status.IsFrozen, "and freezes them");
            }
            yield return GameSmokeTests.GameSeconds(0.5f);   // the cast's lock

            Heal(d);
            OnBar(hero, 1, "frostarmor");
            yield return Cast(hero, 1, Arena, 0.3f);
            Assert.IsTrue(hero.ChillAttackers > 0, "Giáp Sương is on");
            d[0].motor.Teleport(Arena + Vector2.right * 1.2f);
            hero.health.invulnerable = false;
            var bite = DamageInfo.Make(5f, Team.Enemy, d[0].gameObject, Arena, Vector2.left, DamageType.Physical);
            hero.health.TakeDamage(bite);
            hero.health.invulnerable = true;
            Assert.IsTrue(d[0].status.ChillStacks > 0, "who strikes the armor is chilled");
        }

        [UnityTest]
        public IEnumerator TheLightningSpellsLeapStepAndCharge()
        {
            var hero = Hero("wizard");
            hero.health.invulnerable = true;
            Knows(hero, "chainlightning", "thunderstep", "balllightning", "stormfield", "thunderstorm");
            var d = Dummies(3);

            OnBar(hero, 1, "chainlightning");
            yield return Cast(hero, 1, (Vector2)d[0].transform.position, 0.6f);
            foreach (var e in d)
            {
                Assert.IsTrue(Hurt(e), "Xích Lôi leaps to every one of them");
                Assert.GreaterOrEqual(e.status.ChargeStacks, 1, "each gets Tích Điện");
            }

            Heal(d);
            OnBar(hero, 1, "balllightning");
            yield return Cast(hero, 1, (Vector2)d[1].transform.position, 3f);
            Assert.IsTrue(Hurt(d[0]) && Hurt(d[1]), "Lôi Cầu zaps what it passes");

            Heal(d);
            OnBar(hero, 1, "stormfield");
            hero.motor.Teleport(Arena);
            foreach (var e in d) e.motor.Teleport(Arena + Vector2.right * 1.5f);
            yield return GameSmokeTests.Frames(2);
            hero.energy = hero.maxEnergy;
            hero.skills.ResetCooldowns();
            Assert.IsTrue(hero.skills.TryCast(1, Arena));
            yield return GameSmokeTests.GameSeconds(0.7f);
            Assert.IsTrue(Hurt(d[0]), "Điện Trường shocks");
            Assert.Less(d[0].status.SpeedMultiplier, 1f, "and slows");
            Assert.IsTrue(hero.buffs.Exists(b => b.spec != null && b.spec.id == "stormfield"), "the caster runs faster");
            yield return GameSmokeTests.GameSeconds(4.6f);

            Line(d);
            Heal(d);
            OnBar(hero, 6, "thunderstorm");
            yield return Cast(hero, 6, (Vector2)d[1].transform.position, 2.6f);
            Assert.IsTrue(Hurt(d[1]), "Cửu Thiên Lôi strikes");

            OnBar(hero, 1, "thunderstep");
            yield return Cast(hero, 1, Arena + Vector2.up * 10f, 0.4f);
            Assert.Greater(((Vector2)hero.transform.position - Arena).y, 4f, "Thiểm Bộ blinks toward the aim");
        }

        [UnityTest]
        public IEnumerator TheDarkSpellsCurseSiphonCloneAndEclipse()
        {
            var hero = Hero("warlock");
            hero.health.invulnerable = true;
            Knows(hero, "shadowknives", "curse", "shadowclone", "soulsiphon", "eclipse");
            var d = Dummies(3);
            Vector2 first = d[0].transform.position;

            OnBar(hero, 1, "shadowknives");
            yield return Cast(hero, 1, first, 0.6f);
            Assert.IsTrue(Hurt(d[0]), "Ám Tiễn hits");

            Heal(d);
            OnBar(hero, 1, "curse");
            yield return Cast(hero, 1, first, 0.4f);
            Assert.IsTrue(d[0].status.IsCursed, "Lời Nguyền curses");

            Heal(d);
            OnBar(hero, 1, "soulsiphon");
            hero.health.hp = hero.health.maxHp * 0.4f;
            float before = hero.health.hp;
            yield return Cast(hero, 1, first, 2.3f);
            Assert.IsTrue(Hurt(d[0]), "Hút Hồn drains");
            Assert.Greater(hero.health.hp, before, "and heals the caster");

            Heal(d);
            OnBar(hero, 1, "shadowclone");
            yield return Cast(hero, 1, first, 2f);
            Assert.IsTrue(Hurt(d[0]), "Phân Thân cuts the foe beside it");

            OnBar(hero, 6, "eclipse");
            float crit = hero.stats.Stats.Get(StatId.CritChance);
            yield return Cast(hero, 6, Arena, 0.4f);
            Assert.AreEqual(crit + 0.5f, hero.stats.Stats.Get(StatId.CritChance), 0.01f, "Nhật Thực: +50% crit");
            Heal(d);
            yield return GameSmokeTests.GameSeconds(6.2f);
            Assert.AreEqual(crit, hero.stats.Stats.Get(StatId.CritChance), 0.01f, "for six seconds");
            Assert.IsTrue(Hurt(d[0]), "then the darkness bursts");
        }

        [UnityTest]
        public IEnumerator LightningBrandChargesTheBasicAttack()
        {
            var hero = Hero("rogue");
            hero.health.invulnerable = true;
            Knows(hero, "lightningbrand");
            var d = Dummies(1);
            OnBar(hero, 1, "lightningbrand");
            yield return Cast(hero, 1, Arena, 0.3f);
            Assert.Greater(hero.ImbuePower, 0f, "Lôi Ấn is on");
            hero.motor.Teleport((Vector2)d[0].transform.position + Vector2.left * 1f);
            hero.energy = hero.maxEnergy;
            hero.skills.ResetCooldowns();
            yield return GameSmokeTests.GameSeconds(0.05f);
            Assert.IsTrue(hero.skills.TryCast(0, d[0].transform.position), "the basic attack");
            yield return GameSmokeTests.GameSeconds(0.6f);
            Assert.IsTrue(Hurt(d[0]));
            Assert.GreaterOrEqual(d[0].status.ChargeStacks, 1, "a charged weapon adds Tích Điện");
        }
    }
}

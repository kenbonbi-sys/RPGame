using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// Players' second feedback (25/09/2026), in the real world scene played offline: gear is put
    /// on from the bag and adds its bonuses, a worn piece comes back to the bag, worn gear survives
    /// a save, the smith makes gear from monsters' drops, the bag holds 48 and scrolls in the one
    /// Nhân Vật window, food is eaten from the bag, and a Đá Truyền Tống says what to do.
    /// </summary>
    public class GearTests
    {
        string tempSaves;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            tempSaves = Path.Combine(Path.GetTempPath(), "rtt_test_saves_gear");
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

        static PlayerController Fighter()
        {
            var hero = Players.Local;
            hero.stats.look = new HeroLook();
            CharacterChoice.Apply(hero, new HeroLook { cls = "fighter", race = "human", weapon = "sword" });
            return hero;
        }

        [UnityTest]
        public IEnumerator GearIsPutOnFromTheBagAndAddsItsBonuses()
        {
            var hero = Fighter();
            var db = GameManager.I.db;
            var bag = hero.inventory;
            var shield = db.Item("shield");
            var sword = db.Item("sword");
            Assert.AreEqual(EquipSlot.Offhand, shield.slot, "Khiên Gỗ goes in the off hand");
            Assert.AreEqual(EquipSlot.Offhand, sword.slot, "so does a second sword");
            bag.Add(shield, 1, false);
            if (bag.Count(sword) == 0) bag.Add(sword, 1, false);
            float armor = hero.stats.Stats.Get(StatId.Armor);
            float speed = hero.stats.Stats.Get(StatId.AttackSpeed);

            Assert.IsNull(bag.Equip(shield));
            Assert.AreEqual(shield, bag.Worn(EquipSlot.Offhand));
            Assert.AreEqual(0, bag.Count(shield), "out of the bag while worn");
            Assert.AreEqual(armor + 3f, hero.stats.Stats.Get(StatId.Armor), 0.01f, "+3 Giáp");
            Assert.AreEqual(hero.stats.Stats.Get(StatId.Armor), hero.health.armor, 0.01f, "the body takes it");

            Assert.IsNull(bag.Equip(sword), "the other piece of that slot swaps in");
            Assert.AreEqual(sword, bag.Worn(EquipSlot.Offhand));
            Assert.AreEqual(1, bag.Count(shield), "the shield is back in the bag");
            Assert.AreEqual(armor, hero.stats.Stats.Get(StatId.Armor), 0.01f);
            Assert.AreEqual(speed + 0.04f, hero.stats.Stats.Get(StatId.AttackSpeed), 0.001f);

            Assert.IsNull(bag.Unequip(EquipSlot.Offhand));
            Assert.IsNull(bag.Worn(EquipSlot.Offhand));
            Assert.AreEqual(speed, hero.stats.Stats.Get(StatId.AttackSpeed), 0.001f);
            Assert.NotNull(bag.Equip(db.Item("gel")), "slime gel is not worn");
            Assert.NotNull(bag.Unequip(EquipSlot.Head), "nothing on the head");

            // the village chief's ring: +1 to every ability score
            var ring = db.Item("ring");
            bag.Add(ring, 1, false);
            int cha = hero.stats.Attribute(CoreStat.Charisma);
            Assert.IsNull(bag.Equip(ring));
            Assert.AreEqual(cha + 1, hero.stats.Attribute(CoreStat.Charisma));
            yield return null;
        }

        [UnityTest]
        public IEnumerator WornGearSurvivesASave()
        {
            var hero = Fighter();
            var db = GameManager.I.db;
            var bag = hero.inventory;
            bag.Add(db.Item("helm_leather"), 1, false);
            bag.Add(db.Item("boots_toad"), 1, false);
            Assert.IsNull(bag.Equip(db.Item("helm_leather")));
            Assert.IsNull(bag.Equip(db.Item("boots_toad")));
            float hp = hero.stats.Stats.Get(StatId.MaxHp);
            string json = bag.CaptureState();

            bag.Unequip(EquipSlot.Head);
            bag.Unequip(EquipSlot.Feet);
            bag.RestoreState(json);
            Assert.AreEqual("helm_leather", bag.Worn(EquipSlot.Head).id);
            Assert.AreEqual("boots_toad", bag.Worn(EquipSlot.Feet).id);
            Assert.AreEqual(0, bag.Count("helm_leather"), "not both worn and in the bag");
            Assert.AreEqual(hp, hero.stats.Stats.Get(StatId.MaxHp), 0.01f, "their bonuses are back");

            // an older save without gear leaves every slot empty
            bag.RestoreState("{\"gold\":5,\"stacks\":[]}");
            foreach (var slot in Gear.Slots) Assert.IsNull(bag.Worn(slot));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheSmithMakesGearFromMonstersDrops()
        {
            var hero = Fighter();
            var db = GameManager.I.db;
            var bag = hero.inventory;
            var helm = db.Item("helm_leather");
            Assert.IsTrue(helm.Craftable);
            Assert.Contains(helm, Forge.Recipes(), "on the forge's Chế Tạo list");
            foreach (var r in Forge.Recipes())
            {
                Assert.IsTrue(r.IsGear, r.id);
                Assert.NotNull(r.icon, r.id + " has an icon");
                foreach (var part in r.craftItems) Assert.NotNull(db.Item(part.id), $"{r.id} needs {part.id}, a real item");
            }
            var smith = NPC.All.Find(n => n.forge);
            hero.motor.Teleport((Vector2)smith.transform.position + Vector2.left * 8f);
            yield return null;
            Assert.NotNull(Forge.Craft(hero, "helm_leather"), "only next to the smith");
            hero.motor.Teleport((Vector2)smith.transform.position + Vector2.left * 1.2f);
            yield return null;
            bag.gold = 100;
            bag.Add(db.Item("gel"), 6, false);
            Assert.NotNull(Forge.Craft(hero, "helm_leather"), "no mushroom caps yet");
            bag.Add(db.Item("shroom_cap"), 2, false);
            Assert.IsNull(Forge.Craft(hero, "helm_leather"));
            Assert.AreEqual(1, bag.Count(helm));
            Assert.AreEqual(40, bag.gold);
            Assert.AreEqual(0, bag.Count("gel"));
            Assert.NotNull(Forge.Craft(hero, "ring"), "the chief's ring is not the smith's to make");
            bag.gold = 1000;
            bag.Add(db.Item("gel"), 4, false);
            bag.Add(db.Item("herb"), 3, false);
            Assert.IsNull(Forge.Apply(hero, Forge.Action.Craft, 0, "boots_leather"), "asked the way the network asks");
            Assert.AreEqual(1, bag.Count("boots_leather"));
        }

        [UnityTest]
        public IEnumerator OneWindowHoldsTheSheetTheGearAndAScrollingBag()
        {
            var hero = Fighter();
            var db = GameManager.I.db;
            var bag = hero.inventory;
            Assert.GreaterOrEqual(bag.capacity, 48, "a bigger bag");
            // fill it with one of everything, then some
            foreach (var it in db.items)
                if (it != null && it.kind != ItemKind.Currency && bag.stacks.Count < bag.capacity) bag.Add(it, 1, false);
            var panel = HUD.I.heroPanel;
            Assert.NotNull(panel, "Nhân Vật is on the HUD");
            panel.Show();
            yield return GameSmokeTests.Frames(3);
            Assert.IsTrue(panel.IsOpen);
            var scroll = panel.GetComponentInChildren<ScrollRect>();
            Assert.NotNull(scroll, "the bag scrolls");
            Canvas.ForceUpdateCanvases();
            Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height + 20f, "more rows than fit: the wheel moves them");
            panel.DebugScroll(0f);
            yield return null;
            Assert.Less(scroll.verticalNormalizedPosition, 0.05f, "down to the last row");
            panel.Close();
            yield return null;
            Assert.IsFalse(panel.IsOpen);
        }

        [UnityTest]
        public IEnumerator FoodIsEatenFromTheBag()
        {
            var hero = Fighter();
            var db = GameManager.I.db;
            var bread = db.Item("bread");
            hero.inventory.Add(bread, 2, false);
            hero.health.hp = 20f;
            Assert.IsTrue(hero.UseItem(bread));
            Assert.AreEqual(35f, hero.health.hp, 0.5f, "bread heals 15");
            Assert.AreEqual(1, hero.inventory.Count(bread));
            Assert.IsFalse(hero.UseItem(db.Item("gel")), "gel is not food");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AWaystoneSaysWhatToDo()
        {
            var hero = Fighter();
            hero.health.invulnerable = true;
            var stone = Waystone.Find("village");
            Assert.NotNull(stone);
            hero.motor.Teleport((Vector2)stone.transform.position + Vector2.left * 30f);
            yield return GameSmokeTests.Frames(3);
            Assert.IsNull(stone.Prompt, "nothing from afar");
            hero.motor.Teleport(stone.Arrival);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (stone.Prompt != "[F] Dịch chuyển" && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual("[F] Dịch chuyển", stone.Prompt, "woken and standing at it: F travels");
        }
    }
}

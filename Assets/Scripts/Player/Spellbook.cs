using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Sách Chiêu (plan §06, T63): the spells of the Băng, Lôi and Ám schools. Each is learned once
    /// from its Bí Kíp (a book the region's bosses drop) and only by the classes that have it on
    /// their D&D spell list (Chain Lightning for a sorcerer, not a fighter), then put on the skill
    /// bar in place of one of the class's own skills: W E R A S take ordinary spells, D a Tuyệt kỹ.
    /// Changing the bar waits until the hero is out of a fight. What a hero knows and has on the
    /// bar lives in their <see cref="HeroLook"/> (<see cref="HeroLook.spells"/>, <see cref="HeroLook.bar"/>):
    /// saved with the character, checked where the rules run, sent to every screen.
    /// </summary>
    public static class Spellbook
    {
        public enum School { Ice, Lightning, Dark }

        public class Entry
        {
            public string id;
            public School school;
            /// <summary>Classes whose spell list has it (<see cref="ClassDef"/> ids).</summary>
            public string[] classes;
            /// <summary>Where its Bí Kíp is found, for the tooltip and the Sách Chiêu.</summary>
            public string from;

            public string TomeId => TomePrefix + id;
        }

        public const string TomePrefix = "tome_";
        /// <summary>Slots of the bar a spell may take: W E R A S (1–5) ordinary spells, D (6) a Tuyệt kỹ.</summary>
        public const int FirstSlot = 1, UltimateSlot = 6;

        /// <summary>The eighteen spells, six a school (plan §06), in the order the Sách Chiêu lists them.</summary>
        public static readonly Entry[] All =
        {
            // Băng: Đầm Lầy Sương Mù's bosses keep their books
            new Entry { id = "ice", school = School.Ice, classes = new[] { "sorcerer", "wizard", "druid" }, from = "Cóc Tía" },
            new Entry { id = "frostarrows", school = School.Ice, classes = new[] { "sorcerer", "wizard", "ranger" }, from = "Cóc Tía" },
            new Entry { id = "frostarmor", school = School.Ice, classes = new[] { "warlock", "wizard", "sorcerer", "barbarian", "fighter" }, from = "Cóc Tía" },
            new Entry { id = "iceprison", school = School.Ice, classes = new[] { "druid", "sorcerer", "wizard" }, from = "Xà Mẫu" },
            new Entry { id = "blizzard", school = School.Ice, classes = new[] { "druid", "sorcerer", "wizard" }, from = "Xà Mẫu" },
            new Entry { id = "iceage", school = School.Ice, classes = new[] { "druid", "sorcerer", "wizard" }, from = "Xà Mẫu" },
            // Lôi: Hang Pha Lê's
            new Entry { id = "chainlightning", school = School.Lightning, classes = new[] { "sorcerer", "wizard", "druid", "ranger" }, from = "Golem Pha Lê Cổ" },
            new Entry { id = "thunderstep", school = School.Lightning, classes = new[] { "sorcerer", "warlock", "wizard", "fighter" }, from = "Golem Pha Lê Cổ" },
            new Entry { id = "lightningbrand", school = School.Lightning,
                        classes = new[] { "fighter", "ranger", "paladin", "rogue", "barbarian", "monk", "bard" }, from = "Golem Pha Lê Cổ" },
            new Entry { id = "balllightning", school = School.Lightning, classes = new[] { "sorcerer", "wizard", "warlock" }, from = "Nhện Chúa Pha Lê" },
            new Entry { id = "stormfield", school = School.Lightning, classes = new[] { "druid", "sorcerer", "wizard", "cleric", "barbarian", "paladin" }, from = "Nhện Chúa Pha Lê" },
            new Entry { id = "thunderstorm", school = School.Lightning, classes = new[] { "druid", "sorcerer", "wizard", "cleric" }, from = "Nhện Chúa Pha Lê" },
            // Ám: Thảo Nguyên Gió's
            new Entry { id = "shadowknives", school = School.Dark, classes = new[] { "rogue", "warlock", "monk" }, from = "Bò Rừng Sắt" },
            new Entry { id = "curse", school = School.Dark, classes = new[] { "bard", "cleric", "wizard", "warlock", "paladin" }, from = "Bò Rừng Sắt" },
            new Entry { id = "shadowstep", school = School.Dark, classes = new[] { "monk", "warlock", "bard" }, from = "Bò Rừng Sắt" },
            new Entry { id = "shadowclone", school = School.Dark, classes = new[] { "rogue", "bard", "warlock", "wizard", "sorcerer" }, from = "Thủ Lĩnh Hắc Phong" },
            new Entry { id = "soulsiphon", school = School.Dark, classes = new[] { "warlock", "wizard", "sorcerer" }, from = "Thủ Lĩnh Hắc Phong" },
            new Entry { id = "eclipse", school = School.Dark, classes = new[] { "rogue", "warlock", "monk" }, from = "Thủ Lĩnh Hắc Phong" },
        };

        public static string SchoolName(School s) => s == School.Ice ? "Băng" : s == School.Lightning ? "Lôi" : "Ám";

        public static Color SchoolColor(School s) =>
            s == School.Ice ? new Color(0.55f, 0.85f, 1f) : s == School.Lightning ? new Color(0.75f, 0.7f, 1f) : new Color(0.8f, 0.45f, 0.95f);

        public static Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var e in All)
                if (e.id == id) return e;
            return null;
        }

        /// <summary>The spell a Bí Kíp teaches (null for any other item).</summary>
        public static Entry OfTome(ItemDef item) =>
            item != null && item.id != null && item.id.StartsWith(TomePrefix) ? Find(item.id.Substring(TomePrefix.Length)) : null;

        public static bool ClassMay(string cls, Entry e) => e != null && !string.IsNullOrEmpty(cls) && Array.IndexOf(e.classes, cls) >= 0;

        public static bool Knows(HeroLook look, string id) => look != null && look.spells != null && Array.IndexOf(look.spells, id) >= 0;

        /// <summary>"Pháp Sư, Thuật Sĩ" — who may learn it.</summary>
        public static string ClassNames(Entry e)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            var names = new List<string>();
            foreach (var c in e.classes)
            {
                var def = db != null ? db.Class(c) : null;
                names.Add(def != null ? def.displayName : c);
            }
            return string.Join(", ", names);
        }

        static AbilityDef Ability(string id)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            return db != null ? db.Ability(id) : null;
        }

        public static bool IsUltimate(string id)
        {
            var a = Ability(id);
            return a != null && a.HasTag(AbilityTags.Ultimate);
        }

        /// <summary>The slot a spell goes to by default: D for a Tuyệt kỹ, W for anything else.</summary>
        public static int DefaultSlot(string id) => IsUltimate(id) ? UltimateSlot : FirstSlot;

        // ------------------------------------------------------------------ learning
        /// <summary>Why <paramref name="hero"/> cannot learn <paramref name="e"/> now (null: they can).</summary>
        public static string WhyNotLearn(PlayerController hero, Entry e)
        {
            if (e == null) return "Không có chiêu này.";
            if (hero == null || hero.stats == null || !hero.stats.HasClass) return "Hãy chọn lớp nhân vật trước.";
            if (!ClassMay(hero.stats.look.cls, e)) return $"Lớp {hero.stats.Class.displayName} không học được chiêu này.";
            if (Knows(hero.stats.look, e.id)) return "Đã học chiêu này rồi.";
            if (Ability(e.id) == null) return "Không có chiêu này.";
            return null;
        }

        /// <summary>
        /// Learns the spell of a Bí Kíp (where the rules run; the book is the caller's to take).
        /// A spell new to the bar goes on it straight away when the slot is still the class's own.
        /// Null when done, else why not.
        /// </summary>
        public static string Learn(PlayerController hero, Entry e)
        {
            string why = WhyNotLearn(hero, e);
            if (why != null) return why;
            var look = hero.stats.look.Clone();
            var list = new List<string>(look.spells ?? new string[0]) { e.id };
            look.spells = list.ToArray();
            hero.stats.SetLook(look);
            var a = Ability(e.id);
            NetCues.VfxOn("quest_complete", hero, 0.8f, 0.4f);
            NetCues.Sound("sfx_levelup", 0.8f, 0f, hero.transform.position);
            Notify.Log(hero, $"Học được chiêu {a.displayName} (hệ {SchoolName(e.school)})! Mở Sách Chiêu (K) để đưa lên thanh kỹ năng.", Palette.Xp);
            return null;
        }

        // ------------------------------------------------------------------ the bar
        /// <summary>The spell on bar slot <paramref name="slot"/> (1–6), or "" for the class's own.</summary>
        public static string OnBar(HeroLook look, int slot)
        {
            if (look == null || look.bar == null || slot < FirstSlot || slot > UltimateSlot) return "";
            int i = slot - FirstSlot;
            return i < look.bar.Length && look.bar[i] != null ? look.bar[i] : "";
        }

        /// <summary>Whether a spell may sit in a slot: D takes a Tuyệt kỹ, W E R A S anything else.</summary>
        public static bool Fits(string id, int slot) =>
            slot >= FirstSlot && slot <= UltimateSlot && IsUltimate(id) == (slot == UltimateSlot);

        /// <summary>The learned spell a bar slot shows, when it may (null: the class's own).</summary>
        public static AbilityDef BarAbility(HeroLook look, int slot)
        {
            string id = OnBar(look, slot);
            if (id.Length == 0 || !Knows(look, id) || !ClassMay(look.cls, Find(id)) || !Fits(id, slot)) return null;
            return Ability(id);
        }

        /// <summary>A hero hurt lately or hunted by an enemy (where the rules run it knows who hunts whom).</summary>
        public static bool InCombat(PlayerController hero)
        {
            if (hero == null || hero.health == null) return false;
            if (Time.time - hero.health.LastDamageTime < 4f) return true;
            if (!GameSession.IsAuthority) return false;
            foreach (var e in EnemyBase.All)
                if (e != null && e.Busy && e.Target == hero) return true;
            foreach (var b in BossBase.All)
                if (b != null && b.Engaged && b.Target == hero) return true;
            return false;
        }

        /// <summary>Why bar slot <paramref name="slot"/> cannot take <paramref name="id"/> ("" : the class's own) now (null: it can).</summary>
        public static string WhyNotSet(PlayerController hero, int slot, string id)
        {
            if (hero == null || hero.stats == null || !hero.stats.HasClass) return "Hãy chọn lớp nhân vật trước.";
            if (slot < FirstSlot || slot > UltimateSlot) return "Ô này không đổi được.";
            if (InCombat(hero)) return "Đang giao chiến: ra khỏi trận rồi hãy đổi chiêu.";
            if (string.IsNullOrEmpty(id)) return null;
            var e = Find(id);
            if (e == null || !Knows(hero.stats.look, id)) return "Chưa học chiêu này.";
            if (!ClassMay(hero.stats.look.cls, e)) return "Lớp này không dùng được chiêu này.";
            if (!Fits(id, slot)) return IsUltimate(id) ? "Tuyệt kỹ chỉ đặt được vào ô D." : "Ô D chỉ dành cho Tuyệt kỹ.";
            return null;
        }

        /// <summary>
        /// Puts <paramref name="id"/> on bar slot <paramref name="slot"/> (or gives the slot back to the
        /// class: ""), where the rules run; a spell already elsewhere on the bar leaves its old slot.
        /// Null when done, else why not (the hero is told).
        /// </summary>
        public static string Set(PlayerController hero, int slot, string id)
        {
            id = id ?? "";
            string why = WhyNotSet(hero, slot, id);
            if (why != null)
            {
                if (hero != null && hero.health != null) Notify.WorldText(hero, why, hero.health.HeadPosition + Vector3.up * 0.4f, new Color(1f, 0.6f, 0.5f));
                return why;
            }
            var look = hero.stats.look.Clone();
            var bar = new string[UltimateSlot];
            for (int i = 0; i < bar.Length; i++) bar[i] = look.bar != null && i < look.bar.Length && look.bar[i] != null ? look.bar[i] : "";
            if (id.Length > 0)
                for (int i = 0; i < bar.Length; i++)
                    if (bar[i] == id) bar[i] = "";
            bar[slot - FirstSlot] = id;
            look.bar = bar;
            hero.stats.SetLook(look);
            NetCues.Sound("sfx_ui_click", 0.7f, 0.05f, hero.transform.position);
            return null;
        }

        /// <summary>This screen's hero asks for a bar change: offline done here, online the server is asked.</summary>
        public static void Ask(int slot, string id)
        {
            var hero = Players.Local;
            if (hero == null) return;
            if (!GameSession.IsAuthority)
            {
                OnlineSession.Ask(new ActRequest { kind = ActKind.SetSkill, value = slot, text = id ?? "" });
                return;
            }
            Set(hero, slot, id);
        }

        // ------------------------------------------------------------------ where the books are found
        /// <summary>Bí Kíp a boss or mini-boss keeps (its fall's chest rolls them for each hero with the kill).</summary>
        public static void AddBossBooks(string bossId, List<LootEntry> loot)
        {
            if (loot == null) return;
            foreach (var e in All)
            {
                if (BossOf(e.from) != bossId) continue;
                string tome = e.TomeId;
                if (loot.Exists(l => l != null && l.itemId == tome)) continue;
                loot.Add(new LootEntry { itemId = tome, chance = IsUltimateId(e.id) ? 0.12f : 0.22f, min = 1, max = 1 });
            }
        }

        /// <summary>
        /// Whether a loot table's item is rolled for <paramref name="owner"/> (offline: the hero
        /// playing): a Bí Kíp only for a hero who may still learn its spell, anything else always.
        /// </summary>
        public static bool Wanted(string itemId, PlayerController owner)
        {
            if (string.IsNullOrEmpty(itemId) || !itemId.StartsWith(TomePrefix)) return true;
            var e = Find(itemId.Substring(TomePrefix.Length));
            var hero = owner != null ? owner : Players.Local;
            if (e == null || hero == null || hero.stats == null) return true;
            return hero.stats.HasClass && ClassMay(hero.stats.look.cls, e) && !Knows(hero.stats.look, e.id);
        }

        /// <summary>The boss id of a book's keeper (the names the Sách Chiêu shows).</summary>
        static string BossOf(string name)
        {
            switch (name)
            {
                case "Cóc Tía": return "toadking";
                case "Xà Mẫu": return "snake";
                case "Golem Pha Lê Cổ": return "crystalgolem";
                case "Nhện Chúa Pha Lê": return "spiderqueen";
                case "Bò Rừng Sắt": return "ironbison";
                case "Thủ Lĩnh Hắc Phong": return "blackwind";
                default: return null;
            }
        }

        // the three Tuyệt kỹ are rarer (the table can be read before the database is: no Ability lookup)
        static bool IsUltimateId(string id) => id == "iceage" || id == "thunderstorm" || id == "eclipse";
    }
}

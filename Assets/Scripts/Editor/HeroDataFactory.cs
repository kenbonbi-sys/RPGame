using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>
    /// The peoples and classes of the character creator (D&D 5e, Player's Handbook 2014): nine
    /// <see cref="RaceDef"/>s with their ability score increases and traits, twelve
    /// <see cref="ClassDef"/>s with their hit die, standard array, saving throws, casting ability,
    /// armor, weapons and skill bar. Descriptions are this game's own words. Authoring mode: an
    /// existing asset is left as it is.
    /// </summary>
    public static class HeroDataFactory
    {
        const string RaceFolder = AssetFactory.DataFolder + "/Races";
        const string ClassFolder = AssetFactory.DataFolder + "/Classes";

        static Color C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        static Color[] Cs(params string[] hex)
        {
            var r = new Color[hex.Length];
            for (int i = 0; i < hex.Length; i++) r[i] = C(hex[i]);
            return r;
        }

        static readonly Color[] HumanSkins = Cs("#ffe2c6", "#f2c49c", "#dea676", "#c28356", "#98603e", "#6a432e");

        // STR INT DEX CON WIS CHA (CoreStat order)
        static int[] B(int str = 0, int intel = 0, int dex = 0, int con = 0, int wis = 0, int cha = 0) => new[] { str, intel, dex, con, wis, cha };

        static T Make<T>(string folder, string id, System.Action<T> fill) where T : ScriptableObject
        {
            EditorUtil.EnsureFolder(folder);
            string path = $"{folder}/{id}.asset";
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (EditorUtil.Keep(a)) return a;
            EditorUtil.Written++;
            var fresh = ScriptableObject.CreateInstance<T>();
            if (a == null)
            {
                a = fresh;
                AssetDatabase.CreateAsset(a, path);
            }
            else
            {
                EditorUtility.CopySerialized(fresh, a);
                Object.DestroyImmediate(fresh);
            }
            fill(a);
            EditorUtility.SetDirty(a);
            return a;
        }

        public static List<RaceDef> CreateRaces()
        {
            RaceDef R(string id, System.Action<RaceDef> fill) => Make<RaceDef>(RaceFolder, id, r =>
            {
                r.id = id;
                r.skins = HumanSkins;
                fill(r);
            });
            return new List<RaceDef>
            {
                R("human", r =>
                {
                    r.displayName = "Con Người";
                    r.englishName = "Human";
                    r.description = "Tuổi đời ngắn nên sống vội và học nhanh. Đi đâu cũng thấy họ, làm gì cũng được.";
                    r.bonus = B(1, 1, 1, 1, 1, 1);
                    r.xpBonus = 0.1f;
                    r.traits = "Đa Tài: nhận thêm 10% kinh nghiệm.";
                }),
                R("elf", r =>
                {
                    r.displayName = "Tiên Tộc";
                    r.englishName = "High Elf";
                    r.description = "Dân rừng già sống hàng trăm năm, mắt tinh, tay khéo, thuộc phép từ bé.";
                    r.bonus = B(intel: 1, dex: 2);
                    r.darkvision = true;
                    r.controlShorter = 0.3f;
                    r.pointedEars = true;
                    r.skins = Cs("#fff2e2", "#f6dcc4", "#e6c4a2", "#c8a080", "#9c7a64", "#dcdcf0");
                    r.traits = "Nhìn Trong Tối: ánh sáng của mình rọi xa hơn trong hang.\nDòng Máu Tiên: Choáng, Trói và Nguyền ngắn hơn 30%.";
                }),
                R("dwarf", r =>
                {
                    r.displayName = "Người Lùn";
                    r.englishName = "Hill Dwarf";
                    r.description = "Thấp, chắc như đá núi, bền bỉ và cứng đầu. Uống rượu mạnh như uống nước.";
                    r.bonus = B(con: 2, wis: 1);
                    r.darkvision = true;
                    r.poisonShorter = 0.5f;
                    r.resist.poison = 0.3f;
                    r.hpPerLevel = 1.8f;
                    r.speedMultiplier = 0.93f;
                    r.build = BodyBuild.Stout;
                    r.beards = true;
                    r.skins = Cs("#f6caa8", "#e2aa88", "#ca8a68", "#a26a4a", "#7c4e36");
                    r.traits = "Nhìn Trong Tối.\nKiên Cường: kháng Độc 30%, Độc ngắn hơn một nửa.\nDẻo Dai: thêm máu mỗi cấp.\nChân ngắn: đi chậm hơn một chút.";
                }),
                R("halfling", r =>
                {
                    r.displayName = "Người Tí Hon";
                    r.englishName = "Lightfoot Halfling";
                    r.description = "Nhỏ bé, vui vẻ và may mắn đến khó tin. Lẻn qua đâu cũng không ai để ý.";
                    r.bonus = B(dex: 2, cha: 1);
                    r.luckyDodge = 0.1f;
                    r.controlShorter = 0.3f;
                    r.speedMultiplier = 0.93f;
                    r.build = BodyBuild.Small;
                    r.traits = "May Mắn: 10% đòn đánh trúng mình bị hụt.\nGan Dạ: Choáng, Trói và Nguyền ngắn hơn 30%.\nChân ngắn: đi chậm hơn một chút.";
                }),
                R("dragonborn", r =>
                {
                    r.displayName = "Long Duệ";
                    r.englishName = "Dragonborn";
                    r.description = "Hậu duệ của rồng: vảy cứng, dáng cao, máu rồng chảy trong người.";
                    r.bonus = B(str: 2, cha: 1);
                    r.draconic = true;
                    r.dragonHead = true;
                    r.tail = true;
                    r.skins = Cs("#b8322c", "#d8a830", "#3a6cc8", "#a07a40", "#e0e4ec", "#a8b0c0", "#3a8a4a", "#3a3448");
                    r.traits = "Huyết Thống Rồng: màu vảy là tổ tiên rồng, kháng 50% hệ của nó (Đỏ, Vàng Kim: Lửa; Lam, Đồng Thiếc: Lôi; Trắng, Bạc: Băng; Lục, Đen: Độc).";
                }),
                R("gnome", r =>
                {
                    r.displayName = "Thần Lùn";
                    r.englishName = "Rock Gnome";
                    r.description = "Nhỏ con, tò mò vô tận, đầu óc đầy máy móc và phép thuật.";
                    r.bonus = B(intel: 2, con: 1);
                    r.darkvision = true;
                    r.resist.fire = r.resist.ice = r.resist.lightning = r.resist.poison = r.resist.holy = r.resist.dark = 0.1f;
                    r.build = BodyBuild.Small;
                    r.pointedEars = true;
                    r.traits = "Nhìn Trong Tối.\nMưu Trí: kháng 10% mọi hệ phép.";
                }),
                R("halfelf", r =>
                {
                    r.displayName = "Bán Tiên";
                    r.englishName = "Half-Elf";
                    r.description = "Nửa người nửa tiên, ở đâu cũng được đón mà chẳng thuộc hẳn về đâu. Miệng lưỡi khéo.";
                    r.bonus = B(dex: 1, con: 1, cha: 2);
                    r.darkvision = true;
                    r.controlShorter = 0.3f;
                    r.pointedEars = true;
                    r.traits = "Nhìn Trong Tối.\nDòng Máu Tiên: Choáng, Trói và Nguyền ngắn hơn 30%.";
                }),
                R("halforc", r =>
                {
                    r.displayName = "Bán Orc";
                    r.englishName = "Half-Orc";
                    r.description = "Mang sức vóc của tộc orc: to khỏe, dữ dằn, gục rồi vẫn đứng dậy.";
                    r.bonus = B(str: 2, con: 1);
                    r.darkvision = true;
                    r.relentlessCooldown = 90f;
                    r.savageCrit = 0.3f;
                    r.tusks = true;
                    r.skins = Cs("#a2b482", "#8aa070", "#788e62", "#8e9888", "#b0a880", "#6a7a58");
                    r.traits = "Nhìn Trong Tối.\nKiên Trì Bất Khuất: đòn chí tử chỉ để lại 1 máu, mỗi 90 giây một lần.\nĐòn Tàn Bạo: chí mạng mạnh hơn.";
                }),
                R("tiefling", r =>
                {
                    r.displayName = "Quỷ Duệ";
                    r.englishName = "Tiefling";
                    r.description = "Mang dòng máu quỷ từ xa xưa: sừng, đuôi, mắt rực, và ngọn lửa địa ngục trong tay.";
                    r.bonus = B(intel: 1, cha: 2);
                    r.darkvision = true;
                    r.resist.fire = 0.5f;
                    r.rebukeChance = 0.2f;
                    r.horns = true;
                    r.tail = true;
                    r.skins = Cs("#c8505a", "#a83e5c", "#8a4a9a", "#6a70b8", "#d8a890", "#9a5a5a");
                    r.traits = "Nhìn Trong Tối.\nKháng Lửa Địa Ngục: kháng Lửa 50%.\nTrả Đòn Địa Ngục: 20% làm kẻ đánh mình bốc cháy.";
                }),
            };
        }

        public static List<ClassDef> CreateClasses()
        {
            var made = MakeClasses();
            // classes made before their emblems were drawn get them now
            foreach (var c in made)
                if (c != null && c.emblem == null)
                {
                    c.emblem = ArtImporter.S("cls_" + c.id);
                    if (c.emblem != null) EditorUtility.SetDirty(c);
                }
            return made;
        }

        static List<ClassDef> MakeClasses()
        {
            ClassDef K(string id, System.Action<ClassDef> fill) => Make<ClassDef>(ClassFolder, id, c =>
            {
                c.id = id;
                c.outfit = id;
                c.emblem = ArtImporter.S("cls_" + id);
                fill(c);
            });
            var S = CoreStat.Strength;
            var D = CoreStat.Dexterity;
            var N = CoreStat.Constitution;
            var I = CoreStat.Intelligence;
            var W = CoreStat.Wisdom;
            var H = CoreStat.Charisma;
            return new List<ClassDef>
            {
                K("barbarian", c =>
                {
                    c.displayName = "Cuồng Chiến Binh";
                    c.englishName = "Barbarian";
                    c.role = "CHIẾN BINH CUỒNG NỘ";
                    c.description = "Chiến binh hoang dã sống bằng cơn thịnh nộ nguyên thủy: càng đau càng đánh mạnh.";
                    c.complexity = Complexity.Low;
                    c.hitDie = 12;
                    c.primary = new[] { S };
                    c.arrayOrder = new[] { S, N, D, W, H, I };
                    c.savingThrows = new[] { S, N };
                    c.casting = S;
                    c.armor = ArmorKind.Unarmored;
                    c.weapons = new[] { "axe", "sword", "mace", "spear" };
                    c.kit = new[] { "", "rage", "slam", "axethrow", "warcry", "endure", "bladestorm" };
                    c.cloth = C("#74513a");
                    c.color = C("#e07a2a");
                }),
                K("bard", c =>
                {
                    c.displayName = "Thi Sĩ";
                    c.englishName = "Bard";
                    c.role = "NGHỆ SĨ CỔ VŨ";
                    c.description = "Dùng lời ca và tiếng đàn để cổ vũ đồng đội, chữa lành, chế nhạo và mê hoặc kẻ thù.";
                    c.complexity = Complexity.High;
                    c.hitDie = 8;
                    c.primary = new[] { H };
                    c.arrayOrder = new[] { H, D, N, W, I, S };
                    c.savingThrows = new[] { D, H };
                    c.casting = H;
                    c.armor = ArmorKind.Light;
                    c.weapons = new[] { "rapier", "lute", "dagger" };
                    c.kit = new[] { "notes", "soundwave", "mockery", "anthem", "song", "misty", "hypnotic" };
                    c.cloth = C("#c8508a");
                    c.color = C("#d0409a");
                }),
                K("cleric", c =>
                {
                    c.displayName = "Tu Sĩ";
                    c.englishName = "Cleric";
                    c.role = "TU SĨ THẦN THÁNH";
                    c.description = "Mượn sức mạnh của thần linh: chữa lành đồng đội, che chở họ và giáng ánh sáng lên kẻ ác.";
                    c.complexity = Complexity.Average;
                    c.hitDie = 8;
                    c.primary = new[] { W };
                    c.arrayOrder = new[] { W, N, S, H, D, I };
                    c.savingThrows = new[] { W, H };
                    c.casting = W;
                    c.armor = ArmorKind.Medium;
                    c.weapons = new[] { "mace", "staff" };
                    c.kit = new[] { "sacredflame", "guidingbolt", "spiritguard", "lightpillar", "heal", "shield", "radiance" };
                    c.cloth = C("#d8dce8");
                    c.color = C("#d0a030");
                }),
                K("druid", c =>
                {
                    c.displayName = "Tế Sư Rừng Xanh";
                    c.englishName = "Druid";
                    c.role = "TƯ TẾ THIÊN NHIÊN";
                    c.description = "Người giữ rừng: gọi rễ cây trói địch, gọi sấm sét, gai nhọn và chữa lành bằng sức sống muôn loài.";
                    c.complexity = Complexity.High;
                    c.hitDie = 8;
                    c.primary = new[] { W };
                    c.arrayOrder = new[] { W, N, D, I, H, S };
                    c.savingThrows = new[] { I, W };
                    c.casting = W;
                    c.armor = ArmorKind.Medium;
                    c.weapons = new[] { "staff", "scimitar" };
                    c.kit = new[] { "thornwhip", "entangle", "thunderwave", "lightning", "heal", "barkskin", "thornstorm" };
                    c.cloth = C("#2e8a36");
                    c.color = C("#50a040");
                }),
                K("fighter", c =>
                {
                    c.displayName = "Chiến Binh";
                    c.englishName = "Fighter";
                    c.role = "BẬC THẦY VŨ KHÍ";
                    c.description = "Thạo mọi loại vũ khí và giáp trụ. Xông pha, trụ vững, và không bao giờ bỏ cuộc.";
                    c.complexity = Complexity.Low;
                    c.hitDie = 10;
                    c.primary = new[] { S, D };
                    c.arrayOrder = new[] { S, N, D, W, H, I };
                    c.savingThrows = new[] { S, N };
                    c.casting = S;
                    c.armor = ArmorKind.Heavy;
                    c.weapons = new[] { "sword", "axe", "mace", "spear", "rapier", "bow" };
                    c.kit = new[] { "", "charge", "whirl", "secondwind", "parry", "surge", "bladestorm" };
                    c.cloth = C("#b52a34");
                    c.color = C("#b04a30");
                }),
                K("monk", c =>
                {
                    c.displayName = "Võ Tăng";
                    c.englishName = "Monk";
                    c.role = "VÕ SƯ";
                    c.description = "Rèn thân và tâm tới mức nắm đấm nhanh như gió, cú đá làm choáng, bước chân nhẹ như mây.";
                    c.complexity = Complexity.High;
                    c.hitDie = 8;
                    c.primary = new[] { D, W };
                    c.arrayOrder = new[] { D, W, N, S, I, H };
                    c.savingThrows = new[] { S, D };
                    c.casting = W;
                    c.armor = ArmorKind.Unarmored;
                    c.weapons = new[] { "fist", "quarterstaff" };
                    c.kit = new[] { "", "flyingkick", "stunstrike", "flurryblows", "patience", "windstep", "palm" };
                    c.cloth = C("#d2641e");
                    c.color = C("#20a0a0");
                }),
                K("paladin", c =>
                {
                    c.displayName = "Hiệp Sĩ Thánh";
                    c.englishName = "Paladin";
                    c.role = "CHIẾN BINH MỘ ĐẠO";
                    c.description = "Chiến binh đã thề nguyện: giáp nặng, đòn trừng phạt thần thánh, bàn tay chữa lành.";
                    c.complexity = Complexity.Average;
                    c.hitDie = 10;
                    c.primary = new[] { S, H };
                    c.arrayOrder = new[] { S, H, N, W, D, I };
                    c.savingThrows = new[] { W, H };
                    c.casting = H;
                    c.armor = ArmorKind.Heavy;
                    c.weapons = new[] { "sword", "mace", "axe", "spear" };
                    c.kit = new[] { "", "smite", "layonhands", "auraprotect", "shield", "holycharge", "heavenfall" };
                    c.cloth = C("#2c58b0");
                    c.color = C("#b8c4d8");
                }),
                K("ranger", c =>
                {
                    c.displayName = "Du Hiệp";
                    c.englishName = "Ranger";
                    c.role = "CHIẾN BINH HOANG DÃ";
                    c.description = "Thợ săn nơi hoang dã: tên bay như mưa, bẫy gai, dấu săn, và luôn giữ khoảng cách.";
                    c.complexity = Complexity.Average;
                    c.hitDie = 10;
                    c.primary = new[] { D, W };
                    c.arrayOrder = new[] { D, W, N, S, I, H };
                    c.savingThrows = new[] { S, D };
                    c.casting = W;
                    c.armor = ArmorKind.Medium;
                    c.weapons = new[] { "bow", "scimitar", "dagger" };
                    c.kit = new[] { "", "volley", "piercing", "trap", "huntermark", "disengage", "multishot" };
                    c.cloth = C("#3a6a3a");
                    c.color = C("#6a8a3a");
                }),
                K("rogue", c =>
                {
                    c.displayName = "Đạo Tặc";
                    c.englishName = "Rogue";
                    c.role = "CAO THỦ KHÉO LÉO";
                    c.description = "Nhanh tay, lẹ mắt, đánh vào chỗ yếu: dao ném, nhát chí mạng, bom khói và bóng tối.";
                    c.complexity = Complexity.Low;
                    c.hitDie = 8;
                    c.primary = new[] { D };
                    c.arrayOrder = new[] { D, N, I, W, H, S };
                    c.savingThrows = new[] { D, I };
                    c.casting = D;
                    c.armor = ArmorKind.Light;
                    c.weapons = new[] { "dagger", "rapier", "scimitar", "bow" };
                    c.kit = new[] { "", "throwknife", "deathstrike", "smokebomb", "evasion", "shadowstep", "bladefan" };
                    c.cloth = C("#6a1e2e");
                    c.color = C("#3a4a8a");
                }),
                K("sorcerer", c =>
                {
                    c.displayName = "Thuật Sĩ";
                    c.englishName = "Sorcerer";
                    c.role = "PHÉP THUẬT BẨM SINH";
                    c.description = "Sinh ra đã mang ma lực trong máu: lửa, băng, sấm và hỗn loạn tuôn ra theo cảm xúc.";
                    c.complexity = Complexity.High;
                    c.hitDie = 6;
                    c.primary = new[] { H };
                    c.arrayOrder = new[] { H, N, D, I, W, S };
                    c.savingThrows = new[] { N, H };
                    c.casting = H;
                    c.armor = ArmorKind.None;
                    c.weapons = new[] { "orb", "wand", "staff" };
                    c.kit = new[] { "firebolt", "fireball", "chaosbolt", "lightning", "arcaneshield", "misty", "meteor" };
                    c.cloth = C("#b52a34");
                    c.color = C("#e05030");
                }),
                K("warlock", c =>
                {
                    c.displayName = "Khế Ước Sư";
                    c.englishName = "Warlock";
                    c.role = "PHÙ THỦY DỊ GIỚI";
                    c.description = "Đổi lấy sức mạnh bằng khế ước với thực thể bên kia bức màn: tia hắc ám, lời nguyền, xúc tu bóng tối.";
                    c.complexity = Complexity.High;
                    c.hitDie = 8;
                    c.primary = new[] { H };
                    c.arrayOrder = new[] { H, N, D, W, I, S };
                    c.savingThrows = new[] { W, H };
                    c.casting = H;
                    c.armor = ArmorKind.Light;
                    c.weapons = new[] { "tome", "wand", "orb" };
                    c.kit = new[] { "eldritch", "hex", "tentacles", "drain", "darkarmor", "misty", "voidstorm" };
                    c.cloth = C("#6232a0");
                    c.color = C("#9a2a4a");
                }),
                K("wizard", c =>
                {
                    c.displayName = "Pháp Sư";
                    c.englishName = "Wizard";
                    c.role = "HỌC GIẢ PHÉP THUẬT";
                    c.description = "Học phép từ sách vở suốt đời: cầu lửa, mũi băng, sấm sét, lá chắn và cả hố đen.";
                    c.complexity = Complexity.Average;
                    c.hitDie = 6;
                    c.primary = new[] { I };
                    c.arrayOrder = new[] { I, N, D, W, H, S };
                    c.savingThrows = new[] { I, W };
                    c.casting = I;
                    c.armor = ArmorKind.None;
                    c.weapons = new[] { "staff", "wand", "tome" };
                    c.kit = new[] { "missile", "fireball", "ice", "lightning", "arcaneshield", "misty", "blackhole" };
                    c.cloth = C("#2c58b0");
                    c.color = C("#7a4ad0");
                }),
            };
        }
    }
}

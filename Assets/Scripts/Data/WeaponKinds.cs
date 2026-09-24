using UnityEngine;

namespace RPG
{
    /// <summary>
    /// A kind of weapon a hero carries (D&D's weapon table, as this game plays it): the ability
    /// that strikes with it (Sức Mạnh; Khéo Léo for finesse and ranged ones; the casting ability
    /// for a focus), its attack, and the basic attack it gives the Q key. A spellcaster's focus
    /// (staff, wand, orb, tome, lute) leaves Q to the class's own cantrip.
    /// </summary>
    public sealed class WeaponKind
    {
        public string id;
        public string name;
        /// <summary>D&D finesse: strikes with the better of Sức Mạnh and Khéo Léo.</summary>
        public bool finesse;
        /// <summary>A bow: Khéo Léo.</summary>
        public bool ranged;
        /// <summary>A spellcasting focus: the class's casting ability and its cantrip on Q.</summary>
        public bool focus;
        /// <summary>Its attack before upgrades (the old Kiếm Sắt: 20).</summary>
        public float attack = 20f;
        /// <summary>The basic attack it gives Q (an ability id); empty for a focus.</summary>
        public string basic;
        public string description;
    }

    /// <summary>Every weapon kind (see <see cref="WeaponKind"/>).</summary>
    public static class WeaponKinds
    {
        public static readonly WeaponKind[] All =
        {
            new WeaponKind { id = "sword", name = "Kiếm Dài", attack = 20f, basic = "slash", description = "Chém vòng cung, đòn thứ ba cực mạnh. Đánh bằng Sức Mạnh." },
            new WeaponKind { id = "axe", name = "Rìu Lớn", attack = 23f, basic = "cleave", description = "Chậm mà nặng, bổ rộng nửa vòng, hất lùi. Đánh bằng Sức Mạnh." },
            new WeaponKind { id = "mace", name = "Chùy", attack = 21f, basic = "smash", description = "Nện mạnh, đòn cuối làm choáng. Đánh bằng Sức Mạnh." },
            new WeaponKind { id = "spear", name = "Giáo", attack = 20f, basic = "thrust", description = "Đâm xa và hẹp, giữ quái ở ngoài tầm. Đánh bằng Sức Mạnh." },
            new WeaponKind { id = "rapier", name = "Kiếm Mảnh", finesse = true, attack = 19f, basic = "thrust", description = "Đâm nhanh và xa. Đánh bằng Khéo Léo (hoặc Sức Mạnh nếu cao hơn)." },
            new WeaponKind { id = "scimitar", name = "Mã Tấu", finesse = true, attack = 18f, basic = "slash", description = "Chém vòng cung nhẹ tay. Đánh bằng Khéo Léo (hoặc Sức Mạnh)." },
            new WeaponKind { id = "dagger", name = "Dao Găm", finesse = true, attack = 16f, basic = "stab", description = "Đâm rất nhanh, dễ chí mạng. Đánh bằng Khéo Léo (hoặc Sức Mạnh)." },
            new WeaponKind { id = "fist", name = "Quyền Thủ", finesse = true, attack = 17f, basic = "flurry", description = "Hai cú đấm liền, đòn cuối đá bay. Đánh bằng Khéo Léo." },
            new WeaponKind { id = "quarterstaff", name = "Côn Gỗ", finesse = true, attack = 19f, basic = "smash", description = "Côn của võ tăng: quật mạnh, đòn cuối làm choáng. Đánh bằng Khéo Léo." },
            new WeaponKind { id = "bow", name = "Cung Dài", ranged = true, attack = 19f, basic = "arrow", description = "Bắn tên từ xa. Đánh bằng Khéo Léo." },
            new WeaponKind { id = "staff", name = "Trượng Phép", focus = true, attack = 20f, description = "Vật dẫn phép: phím Q là phép nhỏ của lớp nhân vật." },
            new WeaponKind { id = "wand", name = "Đũa Phép", focus = true, attack = 19f, description = "Vật dẫn phép nhẹ: phím Q là phép nhỏ của lớp nhân vật." },
            new WeaponKind { id = "orb", name = "Ngọc Phép", focus = true, attack = 20f, description = "Viên ngọc chứa ma lực bẩm sinh: phím Q là phép nhỏ của lớp nhân vật." },
            new WeaponKind { id = "tome", name = "Sách Phép", focus = true, attack = 20f, description = "Cuốn sách của khế ước hay học thuật: phím Q là phép nhỏ của lớp nhân vật." },
            new WeaponKind { id = "lute", name = "Đàn Luýt", focus = true, attack = 19f, description = "Cây đàn của thi sĩ: phím Q là khúc nhạc tấn công." },
        };

        public static WeaponKind Get(string id)
        {
            foreach (var w in All)
                if (w.id == id) return w;
            return null;
        }

        /// <summary>Attack gained per upgrade level at the forge (+8% each).</summary>
        public const float AttackPerUpgrade = 0.08f;
        public const int MaxUpgrade = 10;

        public static float AttackOf(WeaponKind w, int upgrade) =>
            (w != null ? w.attack : 20f) * (1f + AttackPerUpgrade * Mathf.Clamp(upgrade, 0, MaxUpgrade));
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Lò Rèn, the village smith's (Thợ Rèn): a hero's weapon is theirs to shape. Change it for
    /// another their class carries (free), temper it from +0 to +10 (each level +8% attack, a
    /// glow from +7) with gold and what the monsters of each region drop — the forest's for the
    /// first levels, the bear's claw for +3, the swamp's, Cóc Tía's crown and Xà Mẫu's scales, the
    /// cave's — and give it another metal, the rarer ones once it is tempered enough. Applied where
    /// the world's rules run (offline, the server), only next to the smith.
    /// </summary>
    public static class Forge
    {
        /// <summary>How close to the smith a hero must stand.</summary>
        public const float Reach = 3.5f;

        public struct Cost
        {
            public int gold;
            public (string id, int count)[] items;
            /// <summary>The weapon level it needs (metals).</summary>
            public int level;
        }

        /// <summary>What tempering to <paramref name="level"/> costs.</summary>
        public static Cost UpgradeCost(int level)
        {
            switch (level)
            {
                case 1: return new Cost { gold = 40, items = new[] { ("gel", 3) } };
                case 2: return new Cost { gold = 70, items = new[] { ("shroom_cap", 3) } };
                case 3: return new Cost { gold = 150, items = new[] { ("claw", 1) } };
                case 4: return new Cost { gold = 200, items = new[] { ("toad_skin", 4) } };
                case 5: return new Cost { gold = 260, items = new[] { ("leech_tooth", 3), ("mud_core", 2) } };
                case 6: return new Cost { gold = 340, items = new[] { ("toad_crown", 1) } };
                case 7: return new Cost { gold = 430, items = new[] { ("snake_scale", 2) } };
                case 8: return new Cost { gold = 540, items = new[] { ("crystal_shard", 6), ("bat_wing", 3) } };
                case 9: return new Cost { gold = 660, items = new[] { ("spider_silk", 3), ("golem_core", 3) } };
                default: return new Cost { gold = 800, items = new[] { ("snake_fang", 1), ("crystal_shard", 10) } };
            }
        }

        /// <summary>What a metal costs (the plain ones nothing) and the weapon level it needs.</summary>
        public static Cost MetalCost(int metal)
        {
            switch (metal)
            {
                case 2: return new Cost { gold = 100, level = 3, items = new (string, int)[0] };                     // Vàng
                case 4: return new Cost { gold = 200, level = 8, items = new[] { ("crystal_shard", 5) } };         // Pha Lê
                case 5: return new Cost { gold = 150, level = 6, items = new[] { ("poison_gland", 2) } };          // Ngọc Lục
                case 6: return new Cost { gold = 300, level = 9, items = new[] { ("gem_red", 1) } };               // Huyết Thạch
                default: return new Cost { gold = 0, level = 0, items = new (string, int)[0] };                    // Thép, Đồng, Hắc Thiết
            }
        }

        /// <summary>The smith, if <paramref name="hero"/> stands close enough.</summary>
        public static NPC SmithNear(PlayerController hero)
        {
            if (hero == null) return null;
            foreach (var n in NPC.All)
                if (n != null && n.forge && Vector2.Distance(n.transform.position, hero.transform.position) <= Reach) return n;
            return null;
        }

        /// <summary>Why the hero cannot pay <paramref name="c"/> (null: they can).</summary>
        public static string Missing(PlayerController hero, Cost c)
        {
            var inv = hero.inventory;
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (inv == null || db == null) return "Không có túi đồ.";
            if (hero.stats.look.upgrade < c.level) return $"Vũ khí phải đạt +{c.level}.";
            if (inv.gold < c.gold) return $"Thiếu vàng ({inv.gold}/{c.gold}).";
            foreach (var (id, count) in c.items)
            {
                var item = db.Item(id);
                int have = item != null ? inv.Count(item) : 0;
                if (have < count) return $"Thiếu {(item != null ? item.displayName : id)} ({have}/{count}).";
            }
            return null;
        }

        static void Pay(PlayerController hero, Cost c)
        {
            var db = GameManager.I.db;
            hero.inventory.gold -= c.gold;
            foreach (var (id, count) in c.items) hero.inventory.Remove(db.Item(id), count);
            hero.inventory.NotifyChanged();
        }

        /// <summary>Tempers the weapon one level (the rules' side); null when done, else why not.</summary>
        public static string Upgrade(PlayerController hero)
        {
            if (hero == null || hero.stats == null || !hero.stats.HasClass) return "Chưa có lớp nhân vật.";
            if (SmithNear(hero) == null) return "Phải đứng cạnh Thợ Rèn.";
            int now = hero.stats.look.upgrade;
            if (now >= WeaponKinds.MaxUpgrade) return "Vũ khí đã đạt tối đa.";
            var cost = UpgradeCost(now + 1);
            string why = Missing(hero, cost);
            if (why != null) return why;
            Pay(hero, cost);
            var look = hero.stats.look.Clone();
            look.upgrade = now + 1;
            hero.stats.SetLook(look);
            NetCues.Vfx("chest_open", (Vector2)hero.transform.position + Vector2.up * 0.6f, 0f, 0.8f);
            NetCues.Sound("sfx_levelup", 0.8f, 0f, hero.transform.position);
            Notify.Log(hero, $"Rèn thành công: {hero.stats.Weapon.name} +{look.upgrade}!", Palette.Xp);
            return null;
        }

        /// <summary>Gives the weapon another metal; null when done, else why not.</summary>
        public static string ChangeMetal(PlayerController hero, int metal)
        {
            if (hero == null || hero.stats == null || !hero.stats.HasClass) return "Chưa có lớp nhân vật.";
            if (SmithNear(hero) == null) return "Phải đứng cạnh Thợ Rèn.";
            if (metal < 0 || metal >= HeroLook.Metals.Length || metal == hero.stats.look.metal) return "Không đổi gì.";
            var cost = MetalCost(metal);
            string why = Missing(hero, cost);
            if (why != null) return why;
            Pay(hero, cost);
            var look = hero.stats.look.Clone();
            look.metal = metal;
            hero.stats.SetLook(look);
            NetCues.Sound("sfx_hit_heavy", 0.7f, 0.05f, hero.transform.position);
            return null;
        }

        /// <summary>Takes another weapon of the hero's class (free, the forge level stays); null when done.</summary>
        public static string ChangeWeapon(PlayerController hero, string weapon)
        {
            if (hero == null || hero.stats == null || !hero.stats.HasClass) return "Chưa có lớp nhân vật.";
            if (SmithNear(hero) == null) return "Phải đứng cạnh Thợ Rèn.";
            if (!hero.stats.Class.Allows(weapon) || weapon == hero.stats.Weapon.id) return "Không đổi gì.";
            var look = hero.stats.look.Clone();
            look.weapon = weapon;
            hero.stats.SetLook(look);
            NetCues.Sound("sfx_ui_open", 0.7f, 0f, hero.transform.position);
            return null;
        }

        public enum Action : byte { Upgrade = 1, Metal = 2, Weapon = 3 }

        /// <summary>This screen's hero asks the forge (offline done here, online the server is asked).</summary>
        public static string Ask(Action what, int value = 0, string text = null)
        {
            var hero = Players.Local;
            if (hero == null) return "Không có nhân vật.";
            if (!GameSession.IsAuthority)
            {
                OnlineSession.Ask(new ActRequest { kind = ActKind.Forge, id = (int)what, value = value, text = text });
                return null;
            }
            return Apply(hero, what, value, text);
        }

        /// <summary>The rules' side of a forge request (offline, the server).</summary>
        public static string Apply(PlayerController hero, Action what, int value, string text)
        {
            string why = what == Action.Upgrade ? Upgrade(hero) : what == Action.Metal ? ChangeMetal(hero, value) : ChangeWeapon(hero, text);
            if (why != null && hero != null) Notify.WorldText(hero, why, hero.health.HeadPosition + Vector3.up * 0.4f, new Color(1f, 0.6f, 0.5f));
            return why;
        }
    }
}

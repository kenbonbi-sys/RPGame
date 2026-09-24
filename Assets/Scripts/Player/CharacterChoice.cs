using UnityEngine;

namespace RPG
{
    /// <summary>
    /// The character creator's choice, applied where the world's rules run (offline, the server):
    /// a people and a class are chosen once, looks and the weapon (among the class's) any time;
    /// the forge's level and the rarer metals stay the forge's. Numbers out of range are brought
    /// back in, so a client cannot ask for more than the creator offers.
    /// </summary>
    public static class CharacterChoice
    {
        /// <summary>Metals the creator offers; the others come from the forge.</summary>
        public static readonly int[] StarterMetals = { 0, 1, 3 };

        /// <summary>Makes the choice this hero's; false when it is refused (another class once one is chosen).</summary>
        public static bool Apply(PlayerController hero, HeroLook look)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db == null || hero == null || hero.stats == null || look == null) return false;
            var cls = db.Class(look.cls);
            var race = db.Race(look.race);
            if (cls == null || race == null) return false;
            var now = hero.stats.look;
            bool first = !hero.stats.HasClass;
            if (!first && (now.cls != look.cls || now.race != look.race)) return false;
            var chosen = look.Clone();
            chosen.upgrade = now.upgrade;
            if (!cls.Allows(chosen.weapon)) chosen.weapon = cls.DefaultWeapon;
            chosen.skin = Mathf.Clamp(chosen.skin, 0, Mathf.Max(0, (race.skins != null ? race.skins.Length : 1) - 1));
            chosen.hair = Mathf.Clamp(chosen.hair, 0, HeroLook.HairStyles.Length - 1);
            chosen.hairColor = Mathf.Clamp(chosen.hairColor, 0, HeroLook.HairColors.Length - 1);
            chosen.beard = Mathf.Clamp(chosen.beard, 0, HeroLook.Beards.Length - 1);
            chosen.eyes = Mathf.Clamp(chosen.eyes, 0, HeroLook.EyeColors.Length - 1);
            chosen.cloth = Mathf.Clamp(chosen.cloth, -1, HeroLook.Cloths.Length - 1);
            if (chosen.metal != now.metal && System.Array.IndexOf(StarterMetals, chosen.metal) < 0) chosen.metal = now.metal;
            hero.stats.SetLook(chosen);
            if (first)
            {
                hero.health.hp = hero.health.maxHp;
                hero.energy = hero.maxEnergy;
                Notify.Log(hero, $"Bạn là {race.displayName} {cls.displayName}. Chúc phiêu lưu vui vẻ!", Palette.Xp);
            }
            return true;
        }

        /// <summary>The creator's confirm on this screen: offline it is applied here, online the server is asked.</summary>
        public static void Choose(HeroLook look)
        {
            var hero = Players.Local;
            if (hero == null || look == null) return;
            if (!GameSession.IsAuthority)
            {
                OnlineSession.Ask(new ActRequest { kind = ActKind.ChooseLook, text = look.ToJson() });
                return;
            }
            Apply(hero, look);
        }
    }
}

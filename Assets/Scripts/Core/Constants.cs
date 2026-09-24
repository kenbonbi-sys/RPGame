using UnityEngine;

namespace RPG
{
    /// <summary>Physics layers used by the game (set up by Tools/RPG/Setup Project).</summary>
    public static class Layers
    {
        public const int Player = 8;
        public const int Enemy = 9;
        public const int Obstacle = 10;
        public const int Projectile = 11;
        public const int Pickup = 12;
        public const int NPC = 13;

        public const int PlayerMask = 1 << Player;
        public const int EnemyMask = 1 << Enemy;
        public const int ObstacleMask = 1 << Obstacle;
        public const int NPCMask = 1 << NPC;

        /// <summary>Everything that can carry a Health component and be hit.</summary>
        public const int HittableMask = PlayerMask | EnemyMask | ObstacleMask;
    }

    /// <summary>Sorting layers, back to front.</summary>
    public static class SortingLayerNames
    {
        public const string Ground = "Ground";
        public const string Decal = "Decal";
        public const string Default = "Default";
        public const string VFX = "VFX";
        public const string Top = "Top";

        public static readonly string[] All = { Ground, Decal, Default, VFX, Top };
    }

    public enum Team
    {
        Player,
        Enemy,
        Neutral
    }

    public enum DamageType
    {
        Physical,
        Fire,
        Ice,
        Lightning,
        Poison,
        Holy
    }

    /// <summary>Colours shared by UI and VFX so everything stays in one palette.</summary>
    public static class Palette
    {
        public static readonly Color Damage = new Color(1f, 1f, 1f);
        public static readonly Color Crit = new Color(1f, 0.83f, 0.25f);
        public static readonly Color PlayerHurt = new Color(1f, 0.36f, 0.33f);
        public static readonly Color Heal = new Color(0.45f, 1f, 0.45f);
        public static readonly Color Energy = new Color(0.45f, 0.75f, 1f);
        public static readonly Color Status = new Color(1f, 0.72f, 0.3f);
        public static readonly Color Gold = new Color(1f, 0.85f, 0.35f);
        public static readonly Color Telegraph = new Color(1f, 0.32f, 0.18f);
        public static readonly Color Fire = new Color(1f, 0.55f, 0.15f);
        public static readonly Color Ice = new Color(0.55f, 0.9f, 1f);
        public static readonly Color Lightning = new Color(0.8f, 0.7f, 1f);
        public static readonly Color Poison = new Color(0.6f, 0.95f, 0.3f);
        public static readonly Color Holy = new Color(1f, 0.9f, 0.5f);
        public static readonly Color Dark = new Color(0.66f, 0.42f, 0.9f);
        public static readonly Color LogInfo = new Color(0.92f, 0.9f, 0.86f);
        public static readonly Color LogLoot = new Color(0.75f, 0.95f, 0.6f);
        public static readonly Color LogQuest = new Color(1f, 0.85f, 0.4f);
        public static readonly Color LogBestiary = new Color(0.85f, 0.8f, 1f);
        public static readonly Color Xp = new Color(0.78f, 0.62f, 1f);

        public static Color ForType(DamageType t)
        {
            switch (t)
            {
                case DamageType.Fire: return Fire;
                case DamageType.Ice: return Ice;
                case DamageType.Lightning: return Lightning;
                case DamageType.Poison: return Poison;
                case DamageType.Holy: return Holy;
                default: return Damage;
            }
        }
    }
}

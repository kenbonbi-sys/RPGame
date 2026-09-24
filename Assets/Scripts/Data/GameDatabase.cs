using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>All items, abilities, quests and dialogue of the game, plus shared sprites used at runtime.</summary>
    [CreateAssetMenu(menuName = "RPG/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        public List<ItemDef> items = new List<ItemDef>();
        public List<AbilityDef> abilities = new List<AbilityDef>();
        public ProgressionConfig progression;
        public CombatConfig combat;
        public List<QuestDef> quests = new List<QuestDef>();
        public List<ZoneDef> zones = new List<ZoneDef>();
        public ZoneDef startZone;
        [Tooltip("Compiled Yarn project with every NPC's dialogue (Assets/Dialogue).")]
        public Yarn.Unity.YarnProject dialogue;

        [Header("Shared sprites")]
        public Sprite shadowSprite;
        public Sprite whiteSprite;
        public Sprite starIcon;
        public Sprite skullIcon;
        public Sprite questExclaim;
        public Sprite questQuestion;
        public Sprite glowSprite;
        public Sprite beamSprite;
        public Texture2D cursorDefault;
        public Texture2D cursorAttack;

        [Header("Status icons")]
        public Sprite stunIcon;
        public Sprite slowIcon;
        public Sprite burnIcon;
        public Sprite shieldIcon;
        public Sprite regenIcon;

        [Header("Materials")]
        public Material spriteLit;
        [Tooltip("Lit sprite with dissolve + outline (characters).")]
        public Material spriteLitFX;
        public Material spriteUnlit;
        public Material additive;
        public Material silhouette;

        [Header("Prefabs")]
        public GameObject lootPrefab;
        public GameObject boulderPrefab;
        public GameObject telegraphPrefab;
        public GameObject rockProjectilePrefab;
        public GameObject fireballPrefab;
        public GameObject sporePrefab;

        [Header("Online (Docs/KeHoach-Online.md)")]
        [Tooltip("The hero spawned for every player of an online session (Prefabs/Characters/NetHero).")]
        public GameObject netHeroPrefab;
        [Tooltip("FishNet's NetworkManager with its transport and network prefab list (Prefabs/Net/NetworkManager).")]
        public GameObject networkManagerPrefab;

        Dictionary<string, ItemDef> itemMap;

        public ItemDef Item(string id)
        {
            if (itemMap == null)
            {
                itemMap = new Dictionary<string, ItemDef>();
                foreach (var i in items)
                    if (i != null) itemMap[i.id] = i;
            }
            itemMap.TryGetValue(id, out var item);
            return item;
        }

        public ZoneDef Zone(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var z in zones)
                if (z != null && z.id == id) return z;
            return null;
        }

        public AbilityDef Ability(string id)
        {
            foreach (var a in abilities)
                if (a != null && a.id == id) return a;
            return null;
        }
    }
}

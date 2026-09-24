using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>All items and skills of the game, plus shared sprites used at runtime.</summary>
    [CreateAssetMenu(menuName = "RPG/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        public List<ItemDef> items = new List<ItemDef>();
        public List<SkillDef> skills = new List<SkillDef>();

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

        public SkillDef Skill(string id)
        {
            foreach (var s in skills)
                if (s != null && s.id == id) return s;
            return null;
        }
    }
}

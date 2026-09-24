using UnityEngine;

namespace RPG
{
    /// <summary>Root of the in-game UI. Holds every widget and spawns world-anchored labels.</summary>
    public class HUD : MonoBehaviour
    {
        public static HUD I { get; private set; }

        public Canvas canvas;
        public OrbUI hpOrb;
        public OrbUI energyOrb;
        public BossBarUI bossBar;
        public SkillBarUI skillBar;
        public PotionBarUI potionBar;
        public MinimapUI minimap;
        public QuestTrackerUI quest;
        public CombatLogUI log;
        public BuffBarUI buffs;
        public BannerUI banner;
        public FloatingTextManager floating;
        public TooltipUI tooltip;
        public InventoryUI inventory;
        public HelpPanelUI help;
        public PauseMenuUI pause;
        public DeathScreenUI death;
        public JournalUI journal;

        [Header("World-anchored UI")]
        public RectTransform worldLayer;
        public NameplateUI nameplatePrefab;
        public SkillBannerUI skillBannerPrefab;

        void Awake() => I = this;

        public NameplateUI CreateNameplate(Transform target, string label, bool star, Color barColor, Health health, float height)
        {
            if (nameplatePrefab == null || worldLayer == null) return null;
            var p = Instantiate(nameplatePrefab, worldLayer);
            p.gameObject.SetActive(true);
            p.Bind(target, label, star, barColor, health, height);
            return p;
        }

        public void ShowSkillBanner(Health owner, string text)
        {
            if (skillBannerPrefab == null || worldLayer == null || owner == null) return;
            var b = Instantiate(skillBannerPrefab, worldLayer);
            b.gameObject.SetActive(true);
            b.Show(owner, text);
        }
    }
}

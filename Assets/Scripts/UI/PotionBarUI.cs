using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>Potion slots 1-2-3 with stack counts and a shared cooldown.</summary>
    public class PotionBarUI : MonoBehaviour
    {
        public Image[] icons = new Image[3];
        public TextMeshProUGUI[] counts = new TextMeshProUGUI[3];
        public Image[] cooldowns = new Image[3];
        public Button bagButton;

        void Start()
        {
            if (bagButton != null)
                bagButton.onClick.AddListener(() => { if (HUD.I != null && HUD.I.inventory != null) HUD.I.inventory.Toggle(); });
        }

        void Update()
        {
            var p = GameManager.I != null ? GameManager.I.player : null;
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (p == null || db == null || Inventory.I == null) return;
            float cd = p.potionCooldown > 0 ? p.PotionReadyIn / p.potionCooldown : 0f;
            for (int i = 0; i < 3 && i < p.potionIds.Length; i++)
            {
                var item = db.Item(p.potionIds[i]);
                int n = Inventory.I.Count(item);
                if (icons[i] != null)
                {
                    icons[i].sprite = item != null ? item.icon : null;
                    icons[i].color = n > 0 ? Color.white : new Color(1, 1, 1, 0.3f);
                }
                if (counts[i] != null) counts[i].text = n > 0 ? n.ToString() : "";
                if (cooldowns[i] != null) cooldowns[i].fillAmount = cd;
            }
        }
    }
}

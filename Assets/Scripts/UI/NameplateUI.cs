using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>World-anchored name + optional HP bar ("★ Tảng Đá Lớn", enemy names, NPC names).</summary>
    public class NameplateUI : MonoBehaviour
    {
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI promptText;
        public Image star;
        public RectTransform bar;
        public Image barFill;
        public Image barChip;
        public CanvasGroup group;

        Transform target;
        Health health;
        float height;
        float chip = 1f;
        bool alwaysShowBar;
        RectTransform layer;

        public void Bind(Transform t, string label, bool showStar, Color barColor, Health h, float heightAbove)
        {
            target = t;
            health = h;
            height = heightAbove;
            layer = (RectTransform)transform.parent;
            if (nameText != null)
            {
                nameText.gameObject.SetActive(true);   // pooled: a previous owner may have hidden it
                nameText.text = label;
                nameText.color = showStar ? new Color(1f, 0.86f, 0.45f)
                    : h != null ? new Color(1f, 0.78f, 0.72f) : new Color(0.96f, 0.93f, 0.86f);
            }
            if (star != null) star.gameObject.SetActive(showStar);
            alwaysShowBar = showStar;
            if (bar != null) bar.gameObject.SetActive(h != null && alwaysShowBar);
            if (barFill != null) barFill.color = barColor;
            if (promptText != null) promptText.gameObject.SetActive(false);
            chip = h != null ? h.Fraction : 1f;
            LateUpdate();
        }

        /// <summary>Gives the plate back to the pool (its owner died or went away).</summary>
        public void Release()
        {
            target = null;
            health = null;
            Pool.Release(gameObject, true);
        }

        /// <summary>Changes the name shown (a player's name arrives after their hero).</summary>
        public void SetLabel(string label)
        {
            if (nameText != null) nameText.text = label;
        }

        public void SetPrompt(string s)
        {
            if (promptText == null) return;
            bool on = !string.IsNullOrEmpty(s);
            if (promptText.gameObject.activeSelf != on) promptText.gameObject.SetActive(on);
            if (on) promptText.text = s;
        }

        void LateUpdate()
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                if (group != null) group.alpha = 0;
                if (target == null) Release();
                return;
            }
            bool visible = UIUtil.WorldToLayer(target.position + Vector3.up * height, layer, out Vector2 local);
            if (group != null) group.alpha = visible ? 1f : 0f;
            ((RectTransform)transform).anchoredPosition = local;
            if (health != null && bar != null)
            {
                bool recentlyHit = Time.time - health.LastDamageTime < 5f;
                bool show = !health.IsDead && (alwaysShowBar || recentlyHit);
                if (bar.gameObject.activeSelf != show) bar.gameObject.SetActive(show);
                if (nameText != null && !alwaysShowBar) nameText.gameObject.SetActive(show);
                float f = health.Fraction;
                if (barFill != null) barFill.fillAmount = f;
                chip = Mathf.MoveTowards(chip, f, Time.deltaTime * 0.8f);
                if (chip < f) chip = f;
                if (barChip != null) barChip.fillAmount = chip;
            }
        }
    }
}

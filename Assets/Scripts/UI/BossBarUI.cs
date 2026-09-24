using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>Top-of-screen boss health bar: "☠ Gấu Ma Rừng Già · Cấp 6 ☠" with a delayed damage chip.</summary>
    public class BossBarUI : MonoBehaviour
    {
        public CanvasGroup group;
        public Image fill;
        public Image chip;
        public TextMeshProUGUI title;
        public TextMeshProUGUI hpText;
        public RectTransform shakeRoot;

        Health target;
        float chipValue = 1f;
        float chipDelay;
        float lastHp;
        float shake;
        float hideAt = -1;
        float alpha, alphaGoal;

        void Awake()
        {
            UIUtil.SetAlpha(group, 0);
        }

        public void Show(Health h, string bossName, int level)
        {
            target = h;
            if (title != null) title.text = $"{bossName} · Cấp {level}";
            chipValue = h.Fraction;
            lastHp = h.hp;
            hideAt = -1;
            alphaGoal = 1;
        }

        public void Hide(float delay = 0f)
        {
            hideAt = Time.unscaledTime + delay;
        }

        void Update()
        {
            if (hideAt > 0 && Time.unscaledTime >= hideAt)
            {
                alphaGoal = 0;
                hideAt = -1;
            }
            alpha = Mathf.MoveTowards(alpha, alphaGoal, Time.unscaledDeltaTime * 3f);
            UIUtil.SetAlpha(group, alpha);
            if (target == null) return;

            float f = target.Fraction;
            if (target.hp < lastHp - 0.5f)
            {
                chipDelay = 0.45f;
                shake = Mathf.Min(1f, shake + (lastHp - target.hp) / 120f);
            }
            lastHp = target.hp;
            if (fill != null) fill.fillAmount = f;
            chipDelay -= Time.unscaledDeltaTime;
            if (chipDelay <= 0) chipValue = Mathf.MoveTowards(chipValue, f, Time.unscaledDeltaTime * 0.6f);
            if (chipValue < f) chipValue = f;
            if (chip != null) chip.fillAmount = chipValue;
            if (hpText != null) hpText.text = $"{Mathf.CeilToInt(target.hp)} / {Mathf.RoundToInt(target.maxHp)}";

            shake = Mathf.MoveTowards(shake, 0, Time.unscaledDeltaTime * 3f);
            if (shakeRoot != null)
                shakeRoot.anchoredPosition = shake > 0.01f ? Random.insideUnitCircle * (shake * 5f) : Vector2.zero;
        }
    }
}

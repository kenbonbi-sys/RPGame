using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// Top-of-screen boss health bar: "☠ Gấu Ma Rừng Già · Cấp 6 ☠" with a delayed damage chip,
    /// and the Thanh Trấn Áp (poise) strip under it.
    /// </summary>
    public class BossBarUI : MonoBehaviour
    {
        public CanvasGroup group;
        public Image fill;
        public Image chip;
        public TextMeshProUGUI title;
        public TextMeshProUGUI hpText;
        public RectTransform shakeRoot;

        [Header("Thanh Trấn Áp")]
        public GameObject poiseRoot;
        public Image poiseFill;
        public TextMeshProUGUI poiseLabel;

        Health target;
        Poise poise;
        float poiseShown;
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

        void OnEnable()
        {
            GameEvents.BossEngaged += Show;
            GameEvents.BossDisengaged += Hide;
        }

        void OnDisable()
        {
            GameEvents.BossEngaged -= Show;
            GameEvents.BossDisengaged -= Hide;
        }

        public void Show(Health h, string bossName, int level)
        {
            target = h;
            poise = h != null ? h.GetComponent<Poise>() : null;
            poiseShown = 0f;
            if (poiseRoot != null) poiseRoot.SetActive(poise != null);
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

            UpdatePoise();

            shake = Mathf.MoveTowards(shake, 0, Time.unscaledDeltaTime * 3f);
            if (shakeRoot != null)
                shakeRoot.anchoredPosition = shake > 0.01f ? Random.insideUnitCircle * (shake * 5f) : Vector2.zero;
        }

        void UpdatePoise()
        {
            if (poise == null || poiseFill == null) return;
            bool broken = poise.IsBroken;
            float goal = broken ? Mathf.Clamp01(poise.BrokenRemaining / Mathf.Max(0.01f, poise.breakStun)) : poise.Fraction;
            poiseShown = broken ? goal : Mathf.MoveTowards(poiseShown, goal, Time.unscaledDeltaTime * 2.5f);
            poiseFill.fillAmount = poiseShown;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f);
            poiseFill.color = broken ? Color.Lerp(new Color(1f, 0.55f, 0.2f), Color.white, pulse)
                                     : Color.Lerp(new Color(0.95f, 0.8f, 0.4f), new Color(1f, 0.95f, 0.75f), poiseShown);
            if (poiseLabel != null)
            {
                poiseLabel.text = broken ? "VỠ TRẤN ÁP!" : "Trấn Áp";
                poiseLabel.color = broken ? new Color(1f, 0.75f, 0.35f) : new Color(0.8f, 0.76f, 0.7f, 0.85f);
            }
        }
    }
}

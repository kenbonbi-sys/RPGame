using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>Thin XP bar along the bottom edge with "Cấp N · xp/next XP" and an unspent-points hint.</summary>
    public class XpBarUI : MonoBehaviour
    {
        public Image fill;
        public Image flash;
        public TextMeshProUGUI label;
        public TextMeshProUGUI pointsHint;

        float shown;
        float flashAlpha;
        int lastLevel = -1;

        void OnEnable() => GameEvents.LevelUp += OnLevelUp;
        void OnDisable() => GameEvents.LevelUp -= OnLevelUp;

        void OnLevelUp(int level)
        {
            flashAlpha = 1f;
            shown = 0f;
        }

        void Update()
        {
            var s = PlayerStats.I;
            if (s == null) return;
            int next = s.XpToNext;
            float frac = next > 0 ? Mathf.Clamp01((float)s.xp / next) : 1f;
            if (lastLevel < 0) shown = frac;
            lastLevel = s.level;
            shown = Mathf.MoveTowards(shown, frac, Time.unscaledDeltaTime * 1.5f);
            if (fill != null) fill.fillAmount = shown;
            if (label != null)
                label.text = s.IsMaxLevel ? $"Cấp {s.level}  ·  Tối đa" : $"Cấp {s.level}  ·  {s.xp} / {next} XP";
            if (pointsHint != null)
            {
                bool any = s.statPoints > 0;
                pointsHint.enabled = any;
                if (any)
                {
                    pointsHint.text = $"+{s.statPoints} điểm chỉ số  [C]";
                    pointsHint.alpha = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
                }
            }
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.unscaledDeltaTime * 1.2f);
            if (flash != null) flash.color = new Color(1f, 0.9f, 1f, flashAlpha);
        }
    }
}

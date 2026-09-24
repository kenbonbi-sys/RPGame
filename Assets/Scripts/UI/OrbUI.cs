using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>Diablo-style liquid orb (HP or Energy) with a moving surface line.</summary>
    public class OrbUI : MonoBehaviour
    {
        public enum Kind { Health, Energy }

        public Kind kind;
        public Image fill;
        public RectTransform surface;
        public Image surfaceImage;
        public RectTransform liquidArea;
        public TextMeshProUGUI label;
        public Image lowPulse;
        public string prefix = "Máu";

        float shown = 1f;
        float lastValue = -1;
        float bump;

        void Update()
        {
            var p = Players.Local;
            if (p == null) return;
            float cur, max;
            if (kind == Kind.Health)
            {
                cur = p.health.hp;
                max = p.health.maxHp;
            }
            else
            {
                cur = p.energy;
                max = p.maxEnergy;
            }
            float frac = max > 0 ? Mathf.Clamp01(cur / max) : 0;
            if (lastValue >= 0 && cur < lastValue - 0.5f) bump = 1f;
            lastValue = cur;
            shown = Mathf.Lerp(shown, frac, Time.unscaledDeltaTime * 8f);
            if (fill != null) fill.fillAmount = shown;

            if (surface != null && liquidArea != null)
            {
                float h = liquidArea.rect.height;
                surface.anchoredPosition = new Vector2(Mathf.Sin(Time.unscaledTime * 1.7f) * 3f, -h * 0.5f + h * shown);
                if (surfaceImage != null) surfaceImage.enabled = shown > 0.02f && shown < 0.985f;
            }
            if (label != null) label.text = $"{prefix} <b>{Mathf.CeilToInt(cur)}</b>/{Mathf.RoundToInt(max)}";

            bump = Mathf.MoveTowards(bump, 0, Time.unscaledDeltaTime * 4f);
            transform.localScale = Vector3.one * (1f + bump * 0.04f);
            if (lowPulse != null)
            {
                bool low = kind == Kind.Health && frac < 0.3f;
                float a = low ? 0.35f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
                lowPulse.color = new Color(1f, 0.2f, 0.2f, a);
            }
        }
    }
}

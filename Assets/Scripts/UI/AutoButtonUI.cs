using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>The Tự Động button above the skill bar (same as T): lit and breathing while the hero hunts on its own.</summary>
    public class AutoButtonUI : MonoBehaviour
    {
        public Button button;
        public Image glow;
        public TextMeshProUGUI label;

        static readonly Color Off = new Color(0.78f, 0.74f, 0.66f);
        static readonly Color On = new Color(0.55f, 0.9f, 1f);

        void Awake()
        {
            if (button != null) button.onClick.AddListener(Toggle);
        }

        static void Toggle()
        {
            var pc = Players.Local;
            if (pc == null || pc.IsDead || pc.Puppet) return;
            pc.auto.Set(pc, !pc.auto.On);
        }

        void Update()
        {
            var pc = Players.Local;
            bool on = pc != null && pc.auto.On;
            if (glow != null)
            {
                glow.enabled = on;
                if (on) glow.color = new Color(On.r, On.g, On.b, 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f));
            }
            if (label != null) label.color = on ? On : Off;
        }
    }
}

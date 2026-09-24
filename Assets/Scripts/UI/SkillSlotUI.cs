using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>One skill slot: icon, radial cooldown, seconds counter, key label, tooltip.</summary>
    public class SkillSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public int slot;
        public Image icon;
        public Image cooldownMask;
        public Image highlight;
        public Image readyFlash;
        public TextMeshProUGUI keyLabel;
        public TextMeshProUGUI cdText;
        public TextMeshProUGUI costText;

        float punch;
        float flash;
        bool wasOnCooldown;
        bool hovered;

        void OnEnable()
        {
            InputReader.BindingsChanged += RefreshKey;
            RefreshKey();
        }

        void OnDisable() => InputReader.BindingsChanged -= RefreshKey;

        void RefreshKey()
        {
            if (keyLabel != null) keyLabel.text = InputReader.SkillLabel(slot);
        }

        public void OnCast()
        {
            punch = 1f;
        }

        public void OnFailed()
        {
            punch = 0.5f;
            flash = -1f;
        }

        void Update()
        {
            var p = GameManager.I != null ? GameManager.I.player : null;
            if (p == null || p.skills == null) return;
            var s = p.skills.slots[slot];
            if (icon != null)
            {
                icon.enabled = s != null && s.icon != null;
                if (s != null) icon.sprite = s.icon;
            }
            if (s == null)
            {
                if (cooldownMask != null) cooldownMask.fillAmount = 0;
                if (cdText != null) cdText.text = "";
                return;
            }
            float frac = p.skills.Fraction(slot);
            float rem = p.skills.Remaining(slot);
            bool onCd = rem > 0.01f;
            if (cooldownMask != null) cooldownMask.fillAmount = frac;
            if (cdText != null) cdText.text = onCd ? Util.FormatSeconds(rem) : "";
            bool afford = p.skills.CanAfford(slot);
            if (icon != null) icon.color = !afford && !onCd ? new Color(0.45f, 0.55f, 0.85f) : Color.white;
            if (costText != null) costText.text = s.energyCost > 0 ? Mathf.RoundToInt(s.energyCost).ToString() : "";

            if (wasOnCooldown && !onCd) flash = 1f;
            wasOnCooldown = onCd;

            punch = Mathf.MoveTowards(punch, 0, Time.unscaledDeltaTime * 5f);
            transform.localScale = Vector3.one * (1f - 0.1f * Mathf.Sin(punch * Mathf.PI));
            flash = Mathf.MoveTowards(flash, 0, Time.unscaledDeltaTime * 3f);
            if (readyFlash != null)
            {
                readyFlash.color = flash >= 0 ? new Color(1f, 0.95f, 0.7f, flash * 0.7f) : new Color(1f, 0.2f, 0.2f, -flash * 0.6f);
            }
            if (highlight != null) highlight.enabled = hovered || InputReader.SkillHeld(slot);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            hovered = true;
            var p = GameManager.I != null ? GameManager.I.player : null;
            var s = p != null ? p.skills.slots[slot] : null;
            if (s != null && HUD.I != null && HUD.I.tooltip != null)
                HUD.I.tooltip.Show($"{s.displayName}  <color=#b8b0c8><size=80%>[{InputReader.SkillLabel(slot)}]</size></color>", s.Tooltip(), new Color(1f, 0.88f, 0.55f));
        }

        public void OnPointerExit(PointerEventData e)
        {
            hovered = false;
            if (HUD.I != null && HUD.I.tooltip != null) HUD.I.tooltip.Hide();
        }
    }
}

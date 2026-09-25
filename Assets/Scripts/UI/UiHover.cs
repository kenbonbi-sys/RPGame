using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// A tooltip and clicks for a widget of the panels that build themselves (the character
    /// screen, the forge): what to show is asked when the pointer comes, so it is always current.
    /// </summary>
    public class UiHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Func<(string title, string body, Color color)> tip;
        public Action<PointerEventData.InputButton> click;
        public Image highlight;

        bool over;

        public void OnPointerEnter(PointerEventData e)
        {
            over = true;
            if (highlight != null) highlight.enabled = true;
            ShowTip();
        }

        public void OnPointerExit(PointerEventData e)
        {
            over = false;
            if (highlight != null) highlight.enabled = false;
            if (HUD.I != null && HUD.I.tooltip != null) HUD.I.tooltip.Hide();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (click == null) return;
            click(e.button);
            if (over) ShowTip();   // what it holds may have changed
        }

        void ShowTip()
        {
            if (tip == null || HUD.I == null || HUD.I.tooltip == null) return;
            var t = tip();
            if (string.IsNullOrEmpty(t.title) && string.IsNullOrEmpty(t.body)) HUD.I.tooltip.Hide();
            else HUD.I.tooltip.Show(t.title, t.body, t.color);
        }

        void OnDisable()
        {
            if (over && HUD.I != null && HUD.I.tooltip != null) HUD.I.tooltip.Hide();
            over = false;
            if (highlight != null) highlight.enabled = false;
        }
    }
}

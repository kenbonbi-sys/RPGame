using UnityEngine;

namespace RPG
{
    /// <summary>Base for toggleable full panels (bag, help, journal, pause) with a fade + scale.</summary>
    public class UIPanel : MonoBehaviour
    {
        public CanvasGroup group;
        public RectTransform window;
        public bool pausesGame;
        public bool blocksGameplay = true;

        public bool IsOpen { get; private set; }
        float a;

        protected virtual void Awake()
        {
            if (group != null)
            {
                group.alpha = 0;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        public virtual void Show()
        {
            if (IsOpen) return;
            IsOpen = true;
            if (group != null)
            {
                group.blocksRaycasts = true;
                group.interactable = true;
            }
            if (blocksGameplay && GameManager.I != null) GameManager.I.SetMenu(true, pausesGame);
            AudioManager.Play("sfx_ui_open", 0.5f);
            OnShow();
        }

        public virtual void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (group != null)
            {
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            if (blocksGameplay && GameManager.I != null) GameManager.I.SetMenu(false, false);
            if (HUD.I != null && HUD.I.tooltip != null) HUD.I.tooltip.Hide();
            AudioManager.Play("sfx_ui_close", 0.4f);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Show();
        }

        protected virtual void OnShow() { }

        protected virtual void Update()
        {
            a = Mathf.MoveTowards(a, IsOpen ? 1 : 0, Time.unscaledDeltaTime * 7f);
            if (group != null) group.alpha = a;
            if (window != null) window.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, Util.EaseOutCubic(a));
        }
    }
}

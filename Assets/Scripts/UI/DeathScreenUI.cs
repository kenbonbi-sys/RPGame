using TMPro;
using UnityEngine;

namespace RPG
{
    public class DeathScreenUI : MonoBehaviour
    {
        public CanvasGroup group;
        public TextMeshProUGUI title;
        public TextMeshProUGUI countdown;

        float until;
        bool showing;

        void Awake() => UIUtil.SetAlpha(group, 0);

        void OnEnable()
        {
            GameEvents.PlayerDowned += Show;
            GameEvents.PlayerRespawned += Hide;
        }

        void OnDisable()
        {
            GameEvents.PlayerDowned -= Show;
            GameEvents.PlayerRespawned -= Hide;
        }

        public void Show(float seconds)
        {
            until = Time.unscaledTime + seconds;
            showing = true;
        }

        public void Hide() => showing = false;

        void Update()
        {
            float a = Mathf.MoveTowards(group != null ? group.alpha : 0, showing ? 1 : 0, Time.unscaledDeltaTime * 2f);
            UIUtil.SetAlpha(group, a);
            if (title != null) title.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, a);
            if (showing && countdown != null)
                countdown.text = $"Hồi sinh sau {Mathf.Max(0, Mathf.CeilToInt(until - Time.unscaledTime))}...";
        }
    }
}

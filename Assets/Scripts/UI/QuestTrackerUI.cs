using TMPro;
using UnityEngine;

namespace RPG
{
    /// <summary>"Nói chuyện với Trưởng Làng (+1 · Tab)" — the focused objective under the minimap.</summary>
    public class QuestTrackerUI : MonoBehaviour
    {
        public TextMeshProUGUI text;
        public TextMeshProUGUI titleText;
        float refresh;
        float punch;
        string last;

        void OnEnable() => GameEvents.QuestChanged += Changed;
        void OnDisable() => GameEvents.QuestChanged -= Changed;

        void Changed() => refresh = 0;

        void Update()
        {
            refresh -= Time.unscaledDeltaTime;
            if (refresh <= 0)
            {
                refresh = 0.4f;
                Refresh();
            }
            punch = Mathf.MoveTowards(punch, 0, Time.unscaledDeltaTime * 3f);
            transform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(punch * Mathf.PI));
        }

        void Refresh()
        {
            var me = Players.Local;
            var q = me != null ? me.quests : null;
            if (q == null || text == null) return;
            var list = q.Tracked();
            string s;
            string title = "";
            if (list.Count == 0) s = "<color=#9a9aa8>Không có nhiệm vụ</color>";
            else
            {
                var e = list[Mathf.Clamp(q.focus, 0, list.Count - 1)];
                string icon = e.main ? "<color=#ffd24a>◆</color> " : "<color=#9ad0ff>◇</color> ";
                s = icon + e.objective;
                if (list.Count > 1) s += $"  <color=#b8b0c8>(+{list.Count - 1} · Tab)</color>";
                title = e.title;
            }
            if (s != last)
            {
                if (last != null) punch = 1f;
                last = s;
                text.text = s;
                if (titleText != null) titleText.text = title;
            }
        }
    }
}

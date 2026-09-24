using System.Collections;
using TMPro;
using UnityEngine;

namespace RPG
{
    /// <summary>Big centred titles: zone names, boss intro, quest updates, victory.</summary>
    public class BannerUI : MonoBehaviour
    {
        [Header("Zone / boss title")]
        public CanvasGroup titleGroup;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI subtitleText;

        [Header("Quest toast")]
        public CanvasGroup questGroup;
        public TextMeshProUGUI questHeader;
        public TextMeshProUGUI questTitle;

        [Header("Victory")]
        public CanvasGroup victoryGroup;
        public TextMeshProUGUI victoryText;
        public TextMeshProUGUI victorySub;

        Coroutine titleCo, questCo, victoryCo;

        void Awake()
        {
            UIUtil.SetAlpha(titleGroup, 0);
            UIUtil.SetAlpha(questGroup, 0);
            UIUtil.SetAlpha(victoryGroup, 0);
        }

        void OnEnable() => GameEvents.ZoneEntered += ShowZone;
        void OnDisable() => GameEvents.ZoneEntered -= ShowZone;

        public void ShowZone(string zone) => ShowTitle(zone, "— Khu vực —", new Color(0.95f, 0.92f, 0.82f));

        public void ShowTitle(string title, string sub, Color color)
        {
            if (titleText != null)
            {
                titleText.text = title;
                titleText.color = color;
            }
            if (subtitleText != null) subtitleText.text = sub;
            if (titleCo != null) StopCoroutine(titleCo);
            titleCo = StartCoroutine(FadeRoutine(titleGroup, titleText != null ? titleText.rectTransform : null, 0.5f, 2.2f, 0.9f));
        }

        public void ShowQuest(string header, string title)
        {
            if (questHeader != null) questHeader.text = header;
            if (questTitle != null) questTitle.text = title;
            if (questCo != null) StopCoroutine(questCo);
            questCo = StartCoroutine(FadeRoutine(questGroup, questGroup != null ? (RectTransform)questGroup.transform : null, 0.3f, 2.6f, 0.7f));
        }

        public void ShowVictory(string title, string sub)
        {
            if (victoryText != null) victoryText.text = title;
            if (victorySub != null) victorySub.text = sub;
            if (victoryCo != null) StopCoroutine(victoryCo);
            victoryCo = StartCoroutine(FadeRoutine(victoryGroup, victoryText != null ? victoryText.rectTransform : null, 0.4f, 3.5f, 1.2f, 1.6f));
        }

        IEnumerator FadeRoutine(CanvasGroup g, RectTransform punch, float fadeIn, float hold, float fadeOut, float punchScale = 1.25f)
        {
            if (g == null) yield break;
            float t = 0;
            while (t < fadeIn)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / fadeIn);
                UIUtil.SetAlpha(g, k);
                if (punch != null) punch.localScale = Vector3.one * Mathf.Lerp(punchScale, 1f, Util.EaseOutBack(k));
                yield return null;
            }
            if (punch != null) punch.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(hold);
            t = 0;
            while (t < fadeOut)
            {
                t += Time.unscaledDeltaTime;
                UIUtil.SetAlpha(g, 1f - Mathf.Clamp01(t / fadeOut));
                yield return null;
            }
            UIUtil.SetAlpha(g, 0);
        }
    }
}

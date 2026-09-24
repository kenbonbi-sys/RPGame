using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// Dialogue box with portrait, speaker name, typewriter text and up to 3 choices.
    /// A view only: <see cref="DialogueDirector"/> feeds it one line (or one set of choices)
    /// at a time and waits for <see cref="AdvanceRequested"/> / <see cref="ChosenOption"/>.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        public static DialogueUI I { get; private set; }

        public CanvasGroup group;
        public RectTransform box;
        public Image portrait;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI bodyText;
        public TextMeshProUGUI hintText;
        public float charsPerSecond = 45f;

        [Header("Choices")]
        public RectTransform optionsRoot;
        public Button[] optionButtons = new Button[3];
        public TextMeshProUGUI[] optionLabels = new TextMeshProUGUI[3];

        public bool IsOpen => open;
        /// <summary>The player asked for the next line (after the current one was fully shown).</summary>
        public bool AdvanceRequested { get; private set; }
        /// <summary>Index of the picked choice, -1 while choosing.</summary>
        public int ChosenOption { get; private set; } = -1;
        public bool ShowingOptions => optionCount > 0;

        NPC speaker;
        bool open;
        float openTime;
        float lineTime;
        float shownChars;
        int totalChars;
        int lastBlip;
        int optionCount;
        Vector2 boxHome;

        void Awake()
        {
            I = this;
            UIUtil.SetAlpha(group, 0);
            if (box != null) boxHome = box.anchoredPosition;
            for (int i = 0; i < optionButtons.Length; i++)
            {
                int k = i;
                if (optionButtons[i] != null) optionButtons[i].onClick.AddListener(() => Choose(k));
            }
            HideOptions();
        }

        // ------------------------------------------------------------------ API
        public void Begin(NPC npc)
        {
            speaker = npc;
            open = true;
            openTime = Time.unscaledTime;
            AdvanceRequested = false;
            HideOptions();
            if (bodyText != null) bodyText.text = "";
            if (GameManager.I != null) GameManager.I.SetDialogue(true);
            AudioManager.Play("sfx_ui_open", 0.5f);
        }

        public void ShowLine(string speakerName, string richText)
        {
            HideOptions();
            AdvanceRequested = false;
            lineTime = Time.unscaledTime;
            if (nameText != null) nameText.text = speakerName;
            if (portrait != null)
            {
                // the NPC's portrait for their own lines; other speakers (the hero, a narrator) have none yet
                bool own = speaker != null && (string.IsNullOrEmpty(speakerName) || speakerName == speaker.displayName);
                portrait.sprite = own ? speaker.portrait : null;
                portrait.enabled = portrait.sprite != null;
            }
            if (bodyText != null)
            {
                bodyText.text = richText;
                bodyText.ForceMeshUpdate();
                totalChars = bodyText.textInfo.characterCount;
                bodyText.maxVisibleCharacters = 0;
            }
            shownChars = 0;
            lastBlip = 0;
        }

        public void ShowOptions(IList<string> labels)
        {
            ChosenOption = -1;
            optionCount = Mathf.Min(labels.Count, optionButtons.Length);
            // the question stays fully visible above the choices
            shownChars = totalChars;
            if (bodyText != null) bodyText.maxVisibleCharacters = totalChars;
            if (optionsRoot != null) optionsRoot.gameObject.SetActive(optionCount > 0);
            for (int i = 0; i < optionButtons.Length; i++)
            {
                bool on = i < optionCount;
                if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(on);
                if (on && optionLabels[i] != null) optionLabels[i].text = $"<color=#ffe07a>{i + 1}.</color> {labels[i]}";
            }
            lineTime = Time.unscaledTime;
        }

        public void HideOptions()
        {
            optionCount = 0;
            if (optionsRoot != null) optionsRoot.gameObject.SetActive(false);
        }

        public void End()
        {
            open = false;
            speaker = null;
            HideOptions();
            if (GameManager.I != null) GameManager.I.SetDialogue(false);
            AudioManager.Play("sfx_ui_close", 0.4f);
        }

        /// <summary>Test / AutoShot helper: finish the typewriter and request the next line.</summary>
        public void DebugAdvance()
        {
            shownChars = totalChars;
            if (bodyText != null) bodyText.maxVisibleCharacters = totalChars;
            AdvanceRequested = true;
        }

        public void Choose(int index)
        {
            if (index < 0 || index >= optionCount) return;
            ChosenOption = index;
            AudioManager.Play("sfx_ui_click", 0.6f);
        }

        /// <summary>Ends the whole conversation immediately (tests / skip button).</summary>
        public void SkipAll()
        {
            if (DialogueDirector.I != null) DialogueDirector.I.Skip();
        }

        // ------------------------------------------------------------------ frame
        void Update()
        {
            float target = open ? 1f : 0f;
            if (group != null)
            {
                float a = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 6f);
                UIUtil.SetAlpha(group, a);
                if (box != null) box.anchoredPosition = boxHome + new Vector2(0, Mathf.Lerp(-30f, 0f, Util.EaseOutCubic(a)));
            }
            if (!open) return;

            if (shownChars < totalChars)
            {
                shownChars += charsPerSecond * Time.unscaledDeltaTime;
                int n = Mathf.Min(totalChars, Mathf.FloorToInt(shownChars));
                if (bodyText != null) bodyText.maxVisibleCharacters = n;
                if (n - lastBlip >= 3)
                {
                    lastBlip = n;
                    AudioManager.Play("sfx_dialogue_blip", 0.25f, 0.12f, null, 0.04f);
                }
            }
            bool done = shownChars >= totalChars;
            if (hintText != null)
            {
                hintText.alpha = done ? 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 5f) : 0.25f;
                hintText.text = ShowingOptions ? "► Chọn [1–3] hoặc bấm chuột" : "► [F / Space / Click] tiếp tục";
            }

            // ignore the key press that opened the conversation or the choice that led here
            if (Time.unscaledTime - openTime < 0.15f || Time.unscaledTime - lineTime < 0.08f) return;
            if (ShowingOptions)
            {
                for (int i = 0; i < optionCount; i++)
                    if (InputReader.OptionPressed(i)) Choose(i);
                return;
            }
            if (InputReader.Advance) Advance();
        }

        void Advance()
        {
            if (shownChars < totalChars)
            {
                shownChars = totalChars;
                if (bodyText != null) bodyText.maxVisibleCharacters = totalChars;
                return;
            }
            if (AdvanceRequested) return;
            AdvanceRequested = true;
            AudioManager.Play("sfx_ui_click", 0.4f);
        }
    }
}

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>Dialogue box with portrait, speaker name and typewriter text.</summary>
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

        List<DialogueLine> lines;
        int index;
        float shownChars;
        int totalChars;
        Action onDone;
        NPC speaker;
        bool open;
        float openTime;
        int lastBlip;

        public bool IsOpen => open;
        Vector2 boxHome;

        void Awake()
        {
            I = this;
            UIUtil.SetAlpha(group, 0);
            if (box != null) boxHome = box.anchoredPosition;
        }

        public void Open(NPC npc, List<DialogueLine> dialogue, Action done)
        {
            speaker = npc;
            lines = dialogue;
            onDone = done;
            index = 0;
            open = true;
            openTime = Time.unscaledTime;
            if (portrait != null)
            {
                portrait.sprite = npc != null ? npc.portrait : null;
                portrait.enabled = portrait.sprite != null;
            }
            if (GameManager.I != null) GameManager.I.SetDialogue(true);
            AudioManager.Play("sfx_ui_open", 0.5f);
            ShowLine();
        }

        void ShowLine()
        {
            var l = lines[index];
            if (nameText != null) nameText.text = l.speaker;
            if (bodyText != null)
            {
                bodyText.text = l.text;
                bodyText.ForceMeshUpdate();
                totalChars = bodyText.textInfo.characterCount;
                bodyText.maxVisibleCharacters = 0;
            }
            shownChars = 0;
            lastBlip = 0;
        }

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
            if (hintText != null)
            {
                bool done = shownChars >= totalChars;
                hintText.alpha = done ? 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 5f) : 0.25f;
                hintText.text = index < lines.Count - 1 ? "► [F / Space / Click] tiếp tục" : "► [F / Space / Click] kết thúc";
            }
            if (Time.unscaledTime - openTime > 0.15f && InputReader.Advance) Advance();
        }

        void Advance()
        {
            if (shownChars < totalChars)
            {
                shownChars = totalChars;
                if (bodyText != null) bodyText.maxVisibleCharacters = totalChars;
                return;
            }
            index++;
            AudioManager.Play("sfx_ui_click", 0.4f);
            if (index >= lines.Count) Close();
            else ShowLine();
        }

        /// <summary>Finishes the whole conversation immediately (tests / skip button).</summary>
        public void SkipAll()
        {
            if (!open) return;
            Close();
        }

        void Close()
        {
            open = false;
            if (GameManager.I != null) GameManager.I.SetDialogue(false);
            var cb = onDone;
            onDone = null;
            AudioManager.Play("sfx_ui_close", 0.4f);
            cb?.Invoke();
        }
    }
}

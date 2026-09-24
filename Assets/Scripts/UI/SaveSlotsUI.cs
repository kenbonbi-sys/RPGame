using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// Save / load window: the autosave row plus 3 manual slots with level, zone, date and play time.
    /// Saving over a used slot needs a second click.
    /// </summary>
    public class SaveSlotsUI : UIPanel
    {
        public TextMeshProUGUI title;
        public Button[] rows = new Button[SaveManager.SlotCount + 1];
        public TextMeshProUGUI[] labels = new TextMeshProUGUI[SaveManager.SlotCount + 1];

        bool saveMode;
        int confirmSlot = -1;
        float confirmUntil;

        void Start()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                int slot = i;
                if (rows[i] != null) rows[i].onClick.AddListener(() => Click(slot));
            }
        }

        public void Open(bool save)
        {
            saveMode = save;
            confirmSlot = -1;
            if (IsOpen) Refresh();
            else Show();
        }

        protected override void OnShow() => Refresh();

        protected override void Update()
        {
            base.Update();
            if (confirmSlot >= 0 && Time.unscaledTime > confirmUntil)
            {
                confirmSlot = -1;
                Refresh();
            }
        }

        void Click(int slot)
        {
            var sm = SaveManager.I;
            if (sm == null) return;
            if (saveMode)
            {
                if (slot == SaveManager.AutoSlot) return;
                if (SaveManager.Exists(slot) && confirmSlot != slot)
                {
                    confirmSlot = slot;
                    confirmUntil = Time.unscaledTime + 3f;
                    AudioManager.Play("sfx_ui_click", 0.5f);
                    Refresh();
                    return;
                }
                confirmSlot = -1;
                if (sm.Save(slot))
                {
                    GameEvents.RaiseLog($"Đã lưu vào ô {slot}.", Palette.LogQuest);
                    AudioManager.Play("sfx_quest", 0.6f, 0f);
                }
                Refresh();
            }
            else if (SaveManager.Exists(slot))
            {
                sm.Load(slot);
            }
        }

        void Refresh()
        {
            if (title != null) title.text = saveMode ? "Lưu Game" : "Tải Game";
            for (int i = 0; i < rows.Length; i++)
            {
                var f = SaveManager.Read(i);
                string head = i == SaveManager.AutoSlot ? "Tự động lưu" : $"Ô {i}";
                string text;
                if (confirmSlot == i) text = $"<color=#ffb070>{head}  ·  Bấm lần nữa để ghi đè</color>";
                else if (f == null) text = $"{head}\n<size=80%><color=#9a93a8>— Trống —</color></size>";
                else
                {
                    string zone = string.IsNullOrEmpty(f.zone) ? "" : $"  ·  {f.zone}";
                    text = $"{head}  ·  <color=#ffe07a>Cấp {f.level}</color>{zone}\n" +
                           $"<size=80%><color=#9a93a8>{FormatDate(f.savedAt)}   ·   Đã chơi {FormatTime(f.playTime)}</color></size>";
                }
                if (labels[i] != null) labels[i].text = text;
                if (rows[i] != null)
                    rows[i].interactable = saveMode ? i != SaveManager.AutoSlot : f != null;
            }
        }

        static string FormatDate(string iso)
        {
            return DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d)
                ? d.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                : "";
        }

        static string FormatTime(float seconds)
        {
            var t = TimeSpan.FromSeconds(Mathf.Max(0, seconds));
            return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }
    }
}

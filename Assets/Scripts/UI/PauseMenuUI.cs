using TMPro;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// The Esc menu. Online the world does not pause, the server saves the character by itself,
    /// and the load button takes the player back to the title screen instead.
    /// </summary>
    public class PauseMenuUI : UIPanel
    {
        public Button resumeButton;
        public Button helpButton;
        public Button saveButton;
        public Button loadButton;
        public Button quitButton;

        void Start()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(Close);
            if (helpButton != null) helpButton.onClick.AddListener(() =>
            {
                Close();
                if (HUD.I != null && HUD.I.help != null) HUD.I.help.Show();
            });
            if (GameSession.Online)
            {
                Label(saveButton, "Đã tự lưu");
                Label(loadButton, "Về màn hình chính");
                if (saveButton != null) saveButton.onClick.AddListener(() =>
                {
                    Close();
                    GameEvents.RaiseLog("Máy chủ tự lưu nhân vật của bạn: khi lên cấp, hạ boss, mỗi 30 giây và khi thoát.", Palette.LogInfo);
                });
                if (loadButton != null) loadButton.onClick.AddListener(() => OnlineSession.Leave());
            }
            else
            {
                if (saveButton != null) saveButton.onClick.AddListener(() => OpenSlots(true));
                if (loadButton != null) loadButton.onClick.AddListener(() => OpenSlots(false));
            }
            if (quitButton != null) quitButton.onClick.AddListener(() => GameManager.I.QuitGame());
        }

        static void Label(Button b, string text)
        {
            var t = b != null ? b.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (t != null) t.text = text;
        }

        void OpenSlots(bool save)
        {
            Close();
            if (HUD.I != null && HUD.I.saves != null) HUD.I.saves.Open(save);
        }
    }
}

using UnityEngine.UI;

namespace RPG
{
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
            if (saveButton != null) saveButton.onClick.AddListener(() => OpenSlots(true));
            if (loadButton != null) loadButton.onClick.AddListener(() => OpenSlots(false));
            if (quitButton != null) quitButton.onClick.AddListener(() => GameManager.I.QuitGame());
        }

        void OpenSlots(bool save)
        {
            Close();
            if (HUD.I != null && HUD.I.saves != null) HUD.I.saves.Open(save);
        }
    }
}

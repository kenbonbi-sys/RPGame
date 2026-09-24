using UnityEngine.UI;

namespace RPG
{
    public class PauseMenuUI : UIPanel
    {
        public Button resumeButton;
        public Button helpButton;
        public Button quitButton;

        void Start()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(Close);
            if (helpButton != null) helpButton.onClick.AddListener(() =>
            {
                Close();
                if (HUD.I != null && HUD.I.help != null) HUD.I.help.Show();
            });
            if (quitButton != null) quitButton.onClick.AddListener(() => GameManager.I.QuitGame());
        }
    }
}

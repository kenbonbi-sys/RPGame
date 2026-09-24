using UnityEngine;
using UnityEngine.InputSystem;

namespace RPG
{
    /// <summary>
    /// Online chat (Docs/KeHoach-Online.md, phase 4): Enter opens a line at the bottom of the
    /// screen, Enter sends it (to everyone, or a command such as /n for the party or /w for a
    /// whisper: <see cref="ChatCommands"/>), Esc closes it. Lines show in the log. While typing,
    /// the hero does not act on the keys.
    /// </summary>
    public class ChatInput : MonoBehaviour
    {
        public static ChatInput I { get; private set; }

        public bool IsOpen { get; private set; }

        string text = "";

        void Awake() => I = this;

        void OnDestroy()
        {
            if (I == this) I = null;
        }
        bool focus;

        void Update()
        {
            if (!GameSession.Online || !GameSession.HasScreen) return;
            var kb = Keyboard.current;
            if (kb == null || IsOpen) return;
            var gm = GameManager.I;
            bool console = DebugConsole.I != null && DebugConsole.I.IsOpen;
            if (!console && gm != null && gm.State == GameState.Playing && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                Open();
        }

        void Open()
        {
            IsOpen = true;
            focus = true;
            text = "";
            if (GameManager.I != null) GameManager.I.SetMenu(true, false);
        }

        void Close()
        {
            IsOpen = false;
            if (GameManager.I != null) GameManager.I.SetMenu(false, false);
        }

        void OnGUI()
        {
            if (!IsOpen) return;
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    string line = text.Trim();
                    Close();
                    if (line.Length > 0) ChatCommands.Run(line);
                    e.Use();
                    return;
                }
                if (e.keyCode == KeyCode.Escape)
                {
                    Close();
                    e.Use();
                    return;
                }
            }
            float h = Mathf.Max(28f, Screen.height / 30f);
            var area = new Rect(Screen.width * 0.02f, Screen.height * 0.58f, Screen.width * 0.36f, h);
            GUI.color = new Color(0, 0, 0, 0.75f);
            GUI.DrawTexture(new Rect(area.x - 6, area.y - 6, area.width + 12, area.height + 12), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(h * 0.55f) };
            GUI.SetNextControlName("ChatLine");
            text = GUI.TextField(area, text, 120, style);
            if (focus)
            {
                GUI.FocusControl("ChatLine");
                focus = false;
            }
        }
    }
}

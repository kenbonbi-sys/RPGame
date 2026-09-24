using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// The title screen (scene Title, the game's first): "Vào thế giới" finds the online world by
    /// itself (<see cref="ServerDiscovery"/>) and joins it — its first channel with room — with the
    /// character this machine remembers, or asks for a name and password first (a new name makes a new character);
    /// "Chơi một mình" starts the offline game. When a session ended badly it says why, and after a
    /// lost connection it joins again by itself. Started with -server, -host, -client, -autoshot or
    /// -netsmoke the game goes straight on. The widgets are made here from the references the
    /// scene builder gives it (Tools/RPG, TitleBuilder).
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        [Header("Look")]
        public TMP_FontAsset font;
        public Material fontOutline;
        public Sprite windowSprite;
        public Sprite buttonSprite;
        public Sprite dividerSprite;
        public Sprite whiteSprite;
        [Tooltip("Optional picture behind everything.")]
        public Sprite backdrop;

        static readonly Color Gold = new Color(1f, 0.86f, 0.45f);
        static readonly Color Cream = new Color(0.96f, 0.93f, 0.86f);
        static readonly Color Muted = new Color(0.72f, 0.68f, 0.78f);
        static readonly Color Good = new Color(0.55f, 0.95f, 0.55f);
        static readonly Color Bad = new Color(1f, 0.55f, 0.45f);

        /// <summary>Seconds a search listens for answers.</summary>
        const float SearchSeconds = 0.9f;
        /// <summary>Seconds between searches while the title is open (player counts stay fresh).</summary>
        const float SearchEvery = 12f;
        /// <summary>After a lost connection, seconds before joining again by itself.</summary>
        const float RejoinSeconds = 5f;

        TextMeshProUGUI serverText, characterText, messageText;
        Button switchButton;
        TMP_InputField customField;
        GameObject loginWindow, menuWindow;
        TMP_InputField nameField, passwordField;
        Toggle rememberToggle;
        TextMeshProUGUI loginMessage;

        readonly List<ServerDiscovery.Found> found = new List<ServerDiscovery.Found>();
        bool searching, joining;
        float nextSearch;
        Coroutine rejoin;
        bool wantJoin;

        void Awake()
        {
            // started for a server, a host, a direct join or an automated run: no title
            OnlineSession.ReadCommandLine();
            if (GameSession.Mode != SessionMode.Offline || AutoShot.Active || NetSmoke.Active || LoadBot.Active || BackdropShot.Active ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-creatorshot") >= 0)
            {
                SceneManager.LoadScene(ZoneRoot.CoreScene);
                enabled = false;
                return;
            }
            Build();
        }

        void Start()
        {
            if (!enabled) return;
            AudioListener.volume = 1f;
            ShowCharacter();
            var last = LoginInfo.LastResult;
            string error = LoginInfo.LastError;
            LoginInfo.LastError = null;
            LoginInfo.LastResult = null;
            if (last.HasValue && last.Value.code != LoginCode.Ok)
            {
                // refused at the door: the login window says why
                OpenLogin(last.Value.message, last.Value.code == LoginCode.NoSuchCharacter);
            }
            else if (!string.IsNullOrEmpty(error))
            {
                SetMessage(error, Bad);
                if (LoginInfo.RememberedName != null) rejoin = StartCoroutine(RejoinSoon());
            }
            StartSearch();
            string shot = ArgAfter("-titleshot");
            if (shot != null) StartCoroutine(Shot(shot, ArgAfter("-titleshotLogin") != null));
        }

        static string ArgAfter(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : i >= 0 ? "" : null;
        }

        /// <summary>-titleshot &lt;file.png&gt; [-titleshotLogin 1]: a picture of the title screen (or its login window), then quit (checking the look of a build).</summary>
        IEnumerator Shot(string path, bool login)
        {
            if (login) OpenLogin(null, false);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit(0);
        }

        // ================================================================== searching and joining
        void StartSearch()
        {
            if (searching) return;
            StartCoroutine(Search());
        }

        IEnumerator Search()
        {
            searching = true;
            if (found.Count == 0) SetServer("Đang tìm máy chủ…", Muted);
            string custom = customField != null ? customField.text.Trim() : "";
            if (custom != ServerList.Custom) ServerList.Custom = custom;
            yield return ServerDiscovery.Search(ServerList.Addresses(), OnlineSession.DefaultPort, SearchSeconds, found);
            searching = false;
            nextSearch = Time.unscaledTime + SearchEvery;
            var best = Best();
            if (best == null) SetServer("Không tìm thấy máy chủ. Kiểm tra mạng (LAN / Radmin VPN) rồi bấm Tìm lại.", Bad);
            else if (!best.Compatible)
                SetServer(best.version > NetProtocol.Version ? $"{best.name}: máy chủ mới hơn game này. Hãy tải bản mới." : $"{best.name}: máy chủ đang chạy bản cũ.", Bad);
            else SetServer($"● {best.name}{ChannelLabel(best)}  ·  {best.players}/{best.max} người  ·  {Mathf.RoundToInt(best.ping * 1000f)} ms{OtherChannels(best)}", Good);
            if (wantJoin)
            {
                wantJoin = false;
                Join();
            }
        }

        /// <summary>The server to join: a compatible one with room, the first channel of the fastest world (friends meet there).</summary>
        ServerDiscovery.Found Best()
        {
            ServerDiscovery.Found any = null, best = null;
            foreach (var f in found)
            {
                if (any == null) any = f;
                if (!f.Compatible || f.Full) continue;
                if (best == null || f.address == best.address && f.channel < best.channel) best = f;
            }
            return best ?? any;
        }

        static string ChannelLabel(ServerDiscovery.Found f) => f.channel > 0 ? $" · Kênh {f.channel}" : "";

        /// <summary>The other channels of the same world, when it has more than one: "Kênh 2: 3/20".</summary>
        string OtherChannels(ServerDiscovery.Found best)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var f in found)
                if (f != best && f.address == best.address && f.channel > 0 && f.Compatible)
                    sb.Append($"  ·  Kênh {f.channel}: {f.players}/{f.max}");
            return sb.ToString();
        }

        void OnJoinClicked()
        {
            StopRejoin();
            if (LoginInfo.RememberedName == null || !LoginInfo.UseRemembered())
            {
                OpenLogin(null, false);
                return;
            }
            Join();
        }

        /// <summary>Joins the best server found with the login in <see cref="LoginInfo"/>.</summary>
        void Join()
        {
            if (joining) return;
            if (searching)
            {
                wantJoin = true;
                return;
            }
            var best = Best();
            if (best == null)
            {
                SetMessage("Chưa tìm thấy máy chủ. Đang tìm lại…", Bad);
                wantJoin = true;
                StartSearch();
                return;
            }
            if (!best.Compatible)
            {
                SetMessage("Game này và máy chủ khác phiên bản.", Bad);
                return;
            }
            if (best.Full)
            {
                SetMessage($"Máy chủ đã đủ {best.max} người. Hãy thử lại sau.", Bad);
                return;
            }
            joining = true;
            SetMessage($"Đang vào {best.name} với nhân vật \"{LoginInfo.Name}\"…", Gold);
            GameSession.Mode = SessionMode.Client;
            OnlineSession.Address = best.address;
            OnlineSession.Port = best.port;
            OnlineSession.Channel = Mathf.Max(1, best.channel);
            SceneManager.LoadScene(ZoneRoot.CoreScene);
        }

        IEnumerator RejoinSoon()
        {
            for (float left = RejoinSeconds; left > 0f; left -= Time.unscaledDeltaTime)
            {
                SetMessage($"{messageBase} Tự vào lại sau {Mathf.CeilToInt(left)} giây… (bấm Chơi một mình hoặc Esc để hủy)", Bad, false);
                yield return null;
            }
            rejoin = null;
            if (LoginInfo.UseRemembered()) Join();
        }

        void StopRejoin()
        {
            if (rejoin == null) return;
            StopCoroutine(rejoin);
            rejoin = null;
            SetMessage(messageBase, Bad, false);
        }

        void Update()
        {
            if (!searching && !joining && Time.unscaledTime >= nextSearch) StartSearch();
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (loginWindow != null && loginWindow.activeSelf) CloseLogin();
                else StopRejoin();
            }
            if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && loginWindow != null && loginWindow.activeSelf)
                SubmitLogin(false);
        }

        // ================================================================== login window
        void OpenLogin(string message, bool offerCreate)
        {
            loginWindow.SetActive(true);
            menuWindow.SetActive(false);
            nameField.text = !string.IsNullOrEmpty(LoginInfo.Name) ? LoginInfo.Name : LoginInfo.RememberedName ?? "";
            passwordField.text = "";
            rememberToggle.isOn = true;
            SetLoginMessage(message ?? "Nhập tên nhân vật và mật khẩu. Tên mới sẽ tạo nhân vật mới.", message != null ? Bad : Muted);
            if (offerCreate) SetLoginMessage(message + " Bấm \"Tạo nhân vật mới\" để tạo.", Bad);
            (string.IsNullOrEmpty(nameField.text) ? nameField : passwordField).ActivateInputField();
        }

        void CloseLogin()
        {
            loginWindow.SetActive(false);
            menuWindow.SetActive(true);
        }

        void SubmitLogin(bool create)
        {
            string name = LoginCrypto.NormalizeName(nameField.text);
            string password = passwordField.text;
            string bad = LoginCrypto.CheckName(name) ?? LoginCrypto.CheckPassword(password);
            if (bad != null)
            {
                SetLoginMessage(bad, Bad);
                return;
            }
            LoginInfo.Name = name;
            LoginInfo.Password = password;
            LoginInfo.Mode = create ? LoginMode.Register : LoginMode.Login;
            LoginInfo.Remember = rememberToggle.isOn;
            CloseLogin();
            Join();
        }

        void ShowCharacter()
        {
            string n = LoginInfo.RememberedName;
            characterText.text = n != null ? $"Nhân vật: <color=#ffe07a>{n}</color>" : "Chưa đăng nhập trên máy này";
            switchButton.gameObject.SetActive(n != null);
        }

        void SwitchCharacter()
        {
            LoginInfo.Forget();
            LoginInfo.Name = "";
            ShowCharacter();
            OpenLogin(null, false);
        }

        // ================================================================== messages
        string messageBase = "";

        void SetMessage(string text, Color color, bool remember = true)
        {
            if (remember) messageBase = text;
            messageText.text = text;
            messageText.color = color;
        }

        void SetServer(string text, Color color)
        {
            serverText.text = text;
            serverText.color = color;
        }

        void SetLoginMessage(string text, Color color)
        {
            loginMessage.text = text;
            loginMessage.color = color;
        }

        // ================================================================== widgets
        void Build()
        {
            var canvasGo = new GameObject("TitleCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            var bg = Stretch(root, "Backdrop");
            var bgImg = Img(bg, backdrop != null ? backdrop : whiteSprite, backdrop != null ? Color.white : new Color(0.05f, 0.09f, 0.07f));
            bgImg.preserveAspect = false;
            Img(Stretch(root, "Shade"), whiteSprite, new Color(0.02f, 0.03f, 0.05f, backdrop != null ? 0.6f : 0.25f));

            var title = Txt(root, "Title", "Rừng Thì Thầm", 104, Gold, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0, -190), new Vector2(1400, 140));
            title.fontStyle = FontStyles.Bold;
            Txt(root, "Subtitle", "Thế giới online  ·  đánh quái và boss cùng bạn bè", 30, Cream, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0, -282), new Vector2(1400, 44));

            // main window
            var win = Rect(root, "Menu", new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(680, 560));
            menuWindow = win.gameObject;
            Frame(win);
            serverText = Txt(win, "Server", "Đang tìm máy chủ…", 24, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0, -52), new Vector2(620, 60));
            characterText = Txt(win, "Character", "", 24, Cream, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(-60, -108), new Vector2(460, 36));
            switchButton = Btn(win, "Switch", "Đổi", new Vector2(0.5f, 1f), new Vector2(210, -108), new Vector2(110, 40), 20, SwitchCharacter);
            if (dividerSprite != null) Img(Rect(win, "Divider", new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(420, 14)), dividerSprite, Color.white);
            Btn(win, "Join", "Vào thế giới", new Vector2(0.5f, 1f), new Vector2(0, -205), new Vector2(460, 78), 34, OnJoinClicked)
                .GetComponentInChildren<TextMeshProUGUI>().color = Gold;
            Btn(win, "Offline", "Chơi một mình", new Vector2(0.5f, 1f), new Vector2(0, -295), new Vector2(460, 64), 28, PlayOffline);
            Btn(win, "Quit", "Thoát", new Vector2(0.5f, 1f), new Vector2(0, -372), new Vector2(460, 58), 26, Quit);
            Txt(win, "CustomLabel", "Máy chủ khác:", 20, Muted, TextAlignmentOptions.MidlineLeft, new Vector2(0.5f, 0f), new Vector2(-230, 58), new Vector2(160, 40));
            customField = Input(win, "CustomServer", "để trống: tự tìm", false, new Vector2(0.5f, 0f), new Vector2(20, 58), new Vector2(300, 46), 40);
            customField.text = ServerList.Custom;
            Btn(win, "Search", "Tìm lại", new Vector2(0.5f, 0f), new Vector2(240, 58), new Vector2(130, 46), 20, () => { StopRejoin(); StartSearch(); });

            messageText = Txt(root, "Message", "", 24, Cream, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(1500, 80));
            Txt(root, "Version", $"Phiên bản {Application.version} · giao thức {NetProtocol.Version}", 18, Muted, TextAlignmentOptions.BottomRight,
                              new Vector2(1f, 0f), new Vector2(-230, 30), new Vector2(420, 30));

            // login window
            var dim = Stretch(root, "Login");
            loginWindow = dim.gameObject;
            Img(dim, whiteSprite, new Color(0, 0, 0, 0.55f)).raycastTarget = true;
            var lw = Rect(dim, "Window", new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(700, 620));
            Frame(lw);
            var lt = Txt(lw, "Title", "Vào thế giới", 40, Gold, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0, -48), new Vector2(640, 56));
            lt.fontStyle = FontStyles.Bold;
            Txt(lw, "NameLabel", "Tên nhân vật", 22, Muted, TextAlignmentOptions.MidlineLeft, new Vector2(0.5f, 1f), new Vector2(-70, -120), new Vector2(420, 30));
            nameField = Input(lw, "Name", "3–16 chữ, ví dụ: Khang", false, new Vector2(0.5f, 1f), new Vector2(0, -164), new Vector2(560, 56), LoginCrypto.NameMax);
            Txt(lw, "PasswordLabel", "Mật khẩu", 22, Muted, TextAlignmentOptions.MidlineLeft, new Vector2(0.5f, 1f), new Vector2(-70, -222), new Vector2(420, 30));
            passwordField = Input(lw, "Password", "ít nhất 4 ký tự", true, new Vector2(0.5f, 1f), new Vector2(0, -266), new Vector2(560, 56), 64);
            rememberToggle = Toggle(lw, "Remember", "Nhớ đăng nhập trên máy này", new Vector2(0.5f, 1f), new Vector2(0, -322));
            loginMessage = Txt(lw, "Message", "", 21, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0, -382), new Vector2(620, 70));
            Btn(lw, "Login", "Vào game", new Vector2(0.5f, 0f), new Vector2(-150, 130), new Vector2(270, 66), 28, () => SubmitLogin(false))
                .GetComponentInChildren<TextMeshProUGUI>().color = Gold;
            Btn(lw, "Create", "Tạo nhân vật mới", new Vector2(0.5f, 0f), new Vector2(150, 130), new Vector2(270, 66), 24, () => SubmitLogin(true));
            Btn(lw, "Back", "Quay lại", new Vector2(0.5f, 0f), new Vector2(0, 52), new Vector2(220, 52), 22, CloseLogin);
            loginWindow.SetActive(false);
        }

        void PlayOffline()
        {
            StopRejoin();
            GameSession.Mode = SessionMode.Offline;
            SceneManager.LoadScene(ZoneRoot.CoreScene);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ primitives
        static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>A window's wooden frame over a dark plate, so nothing behind shows through its middle.</summary>
        void Frame(RectTransform window)
        {
            var back = Stretch(window, "Backing");
            back.offsetMin = new Vector2(8, 8);
            back.offsetMax = new Vector2(-8, -8);
            Img(back, whiteSprite, new Color(0.09f, 0.07f, 0.11f, 0.94f));
            Img(Stretch(window, "Frame"), windowSprite, Color.white, Image.Type.Sliced);
        }

        static Image Img(RectTransform rt, Sprite sprite, Color color, Image.Type type = Image.Type.Simple)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sprite != null ? type : Image.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        TextMeshProUGUI Txt(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align,
                            Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var rt = Rect(parent, name, anchor, pos, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            if (fontOutline != null) t.fontSharedMaterial = fontOutline;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.richText = true;
            t.raycastTarget = false;
            return t;
        }

        Button Btn(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, Action onClick)
        {
            var rt = Rect(parent, name, anchor, pos, size);
            var img = Img(rt, buttonSprite, Color.white, Image.Type.Sliced);
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cols = b.colors;
            cols.highlightedColor = new Color(1f, 0.92f, 0.7f);
            cols.pressedColor = new Color(0.8f, 0.75f, 0.6f);
            b.colors = cols;
            var t = Txt(rt, "Label", label, fontSize, Cream, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            b.onClick.AddListener(() =>
            {
                AudioManager.Play("sfx_ui_click", 0.6f);
                onClick();
            });
            return b;
        }

        TMP_InputField Input(Transform parent, string name, string placeholder, bool password, Vector2 anchor, Vector2 pos, Vector2 size, int limit)
        {
            var rt = Rect(parent, name, anchor, pos, size);
            rt.gameObject.SetActive(false);   // wired up before it wakes
            var bg = Img(rt, buttonSprite, new Color(0.75f, 0.7f, 0.8f), Image.Type.Sliced);
            bg.raycastTarget = true;
            var area = Stretch(rt, "Text Area");
            area.offsetMin = new Vector2(16, 6);
            area.offsetMax = new Vector2(-16, -6);
            area.gameObject.AddComponent<RectMask2D>();
            var ph = Txt(area, "Placeholder", placeholder, 22, new Color(0.6f, 0.58f, 0.66f), TextAlignmentOptions.MidlineLeft, new Vector2(0.5f, 0.5f), Vector2.zero, area.rect.size);
            ph.rectTransform.anchorMin = Vector2.zero;
            ph.rectTransform.anchorMax = Vector2.one;
            ph.rectTransform.offsetMin = ph.rectTransform.offsetMax = Vector2.zero;
            ph.fontStyle = FontStyles.Italic;
            ph.fontSharedMaterial = font != null ? font.material : ph.fontSharedMaterial;
            var text = Txt(area, "Text", "", 24, Cream, TextAlignmentOptions.MidlineLeft, new Vector2(0.5f, 0.5f), Vector2.zero, area.rect.size);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var field = rt.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = ph;
            field.targetGraphic = bg;
            field.characterLimit = limit;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            if (password) field.asteriskChar = '•';
            field.caretColor = Gold;
            field.selectionColor = new Color(1f, 0.86f, 0.45f, 0.35f);
            rt.gameObject.SetActive(true);
            return field;
        }

        Toggle Toggle(Transform parent, string name, string label, Vector2 anchor, Vector2 pos)
        {
            var rt = Rect(parent, name, anchor, pos, new Vector2(560, 40));
            var box = Rect(rt, "Box", new Vector2(0f, 0.5f), new Vector2(20, 0), new Vector2(32, 32));
            var bg = Img(box, buttonSprite, Color.white, Image.Type.Sliced);
            bg.raycastTarget = true;
            var mark = Img(Rect(box, "Check", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18)), whiteSprite, Gold);
            var t = Txt(rt, "Label", label, 22, Cream, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0.5f), new Vector2(290, 0), new Vector2(480, 40));
            var toggle = rt.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = bg;
            toggle.graphic = mark;
            toggle.isOn = true;
            return toggle;
        }
    }
}

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
    /// -netsmoke the game goes straight on. The look: the village at dusk behind the game's name on
    /// the left, and on the right a dark green menu in a gold frame (the character, the server
    /// found, the two ways in, other servers folded away), drawn with <see cref="MenuKit"/>.
    /// The widgets are made here from the font and picture the scene builder gives it (Tools/RPG, TitleBuilder).
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        [Header("Look")]
        public TMP_FontAsset font;
        [Tooltip("The font's material with an outline and a shadow (words over the picture, the game's name).")]
        public Material fontOutline;
        [Tooltip("Optional picture behind everything.")]
        public Sprite backdrop;

        /// <summary>Seconds a search listens for answers.</summary>
        const float SearchSeconds = 0.9f;
        /// <summary>Seconds between searches while the title is open (player counts stay fresh).</summary>
        const float SearchEvery = 12f;
        /// <summary>After a lost connection, seconds before joining again by itself.</summary>
        const float RejoinSeconds = 5f;

        // where the backdrop's campfire lands (1920×1080 layout) and the game's name above it
        const float LogoX = 716f;
        /// <summary>The backdrop is shown a quarter larger (its pixels stay even), its top on the screen's top.</summary>
        const float BackdropScale = 1.25f;
        /// <summary>The campfire on the 1920×1080 backdrop (the village at dusk, Debug/BackdropShot.cs).</summary>
        static readonly Vector2 CampfireOnBackdrop = new Vector2(882f, 333f);

        const float PanelX = 1180f, PanelY = 110f, PanelW = 660f, PanelH = 860f, OtherH = 84f;

        MenuKit kit;
        RectTransform stage, loginStage, panel, otherArea;
        Image otherChevron;
        TextMeshProUGUI serverTitle, serverDetail, pingText, characterName, switchLabel, messageText;
        Image serverDot, signalIcon, messageBack;
        TMP_InputField customField;
        GameObject loginWindow;
        TMP_InputField nameField, passwordField;
        Toggle rememberToggle;
        TextMeshProUGUI loginMessage;
        Material logoMaterial;
        bool otherOpen;

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

        void OnDestroy()
        {
            if (logoMaterial != null) Destroy(logoMaterial);
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
                SetMessage(error, MenuKit.Red);
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
            if (found.Count == 0) ShowServer("Đang tìm máy chủ…", "LAN · Radmin VPN · servers.txt", MenuKit.Muted);
            string custom = customField != null ? customField.text.Trim() : "";
            if (custom != ServerList.Custom) ServerList.Custom = custom;
            yield return ServerDiscovery.Search(ServerList.Addresses(), OnlineSession.DefaultPort, SearchSeconds, found);
            searching = false;
            nextSearch = Time.unscaledTime + SearchEvery;
            var best = Best();
            if (best == null)
                ShowServer("Không tìm thấy máy chủ", "Kiểm tra mạng (LAN / Radmin VPN) hoặc nhập địa chỉ ở Máy chủ khác.", MenuKit.Red);
            else if (!best.Compatible)
                ShowServer(best.name, best.version > NetProtocol.Version ? "Máy chủ mới hơn game này. Hãy tải bản mới." : "Máy chủ đang chạy bản cũ.", MenuKit.Red);
            else
                ShowServer($"{best.name}{ChannelLabel(best)}", $"{best.players}/{best.max} người{OtherChannels(best)}", MenuKit.Green,
                           $"{Mathf.RoundToInt(best.ping * 1000f)} ms");
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
                SetMessage("Chưa tìm thấy máy chủ. Đang tìm lại…", MenuKit.Red);
                wantJoin = true;
                StartSearch();
                return;
            }
            if (!best.Compatible)
            {
                SetMessage("Game này và máy chủ khác phiên bản.", MenuKit.Red);
                return;
            }
            if (best.Full)
            {
                SetMessage($"Máy chủ đã đủ {best.max} người. Hãy thử lại sau.", MenuKit.Red);
                return;
            }
            joining = true;
            SetMessage($"Đang vào {best.name} với nhân vật \"{LoginInfo.Name}\"…", MenuKit.GoldText);
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
                SetMessage($"{messageBase} Tự vào lại sau {Mathf.CeilToInt(left)} giây… (bấm Chơi một mình hoặc Esc để hủy)", MenuKit.Red, false);
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
            SetMessage(messageBase, MenuKit.Red, false);
        }

        void Update()
        {
            if (stage != null) MenuKit.FitStage(stage);
            if (loginStage != null) MenuKit.FitStage(loginStage);
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
            panel.gameObject.SetActive(false);
            nameField.text = !string.IsNullOrEmpty(LoginInfo.Name) ? LoginInfo.Name : LoginInfo.RememberedName ?? "";
            passwordField.text = "";
            rememberToggle.isOn = true;
            SetLoginMessage(message ?? "Nhập tên nhân vật và mật khẩu. Tên mới sẽ tạo nhân vật mới.", message != null ? MenuKit.Red : MenuKit.Muted);
            if (offerCreate) SetLoginMessage(message + " Bấm \"Tạo nhân vật mới\" để tạo.", MenuKit.Red);
            (string.IsNullOrEmpty(nameField.text) ? nameField : passwordField).ActivateInputField();
        }

        void CloseLogin()
        {
            loginWindow.SetActive(false);
            panel.gameObject.SetActive(true);
        }

        void SubmitLogin(bool create)
        {
            string name = LoginCrypto.NormalizeName(nameField.text);
            string password = passwordField.text;
            string bad = LoginCrypto.CheckName(name) ?? LoginCrypto.CheckPassword(password);
            if (bad != null)
            {
                SetLoginMessage(bad, MenuKit.Red);
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
            characterName.text = n ?? "Chưa đăng nhập";
            characterName.color = n != null ? MenuKit.Cream : MenuKit.Muted;
            switchLabel.text = n != null ? "Đổi" : "Đăng nhập";
        }

        void SwitchCharacter()
        {
            if (LoginInfo.RememberedName != null)
            {
                LoginInfo.Forget();
                LoginInfo.Name = "";
                ShowCharacter();
            }
            OpenLogin(null, false);
        }

        // ================================================================== messages
        string messageBase = "";

        void SetMessage(string text, Color color, bool remember = true)
        {
            if (remember) messageBase = text;
            messageText.text = text;
            messageText.color = color;
            messageBack.enabled = !string.IsNullOrEmpty(text);
        }

        void ShowServer(string title, string detail, Color color, string ping = null)
        {
            serverTitle.text = title;
            serverTitle.color = color;
            serverDetail.text = detail;
            serverDot.color = color;
            pingText.text = ping ?? "";
            signalIcon.enabled = ping != null;
            signalIcon.color = color;
        }

        void SetLoginMessage(string text, Color color)
        {
            loginMessage.text = text;
            loginMessage.color = color;
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

        /// <summary>Unfolds or folds "Máy chủ khác" (an address to try, and search again); the menu grows both ways to make room.</summary>
        void SetOther(bool open, bool focus = false)
        {
            otherOpen = open;
            otherArea.gameObject.SetActive(open);
            otherChevron.rectTransform.localScale = new Vector3(1f, open ? -1f : 1f, 1f);
            float grow = open ? OtherH : 0f;
            panel.anchoredPosition = new Vector2(PanelX, -(PanelY - grow / 2f));
            panel.sizeDelta = new Vector2(PanelW, PanelH + grow);
            if (open && focus) customField.ActivateInputField();
        }

        // ================================================================== widgets
        void Build()
        {
            kit = new MenuKit(font, fontOutline);
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

            BuildBackdrop(root);
            stage = MenuKit.Stage(root, "Stage");
            BuildLogo(stage);
            BuildMenu(stage);

            // what is happening (joining, a lost connection): under the game's name
            var msg = MenuKit.Box(stage, "Message", LogoX - 520f, 968f, 1040f, 64f);
            messageBack = MenuKit.Art(msg, "round_fill", new Color(0.03f, 0.05f, 0.04f, 0.82f));
            messageText = kit.Text(msg, "Text", "", 22, MenuKit.Cream, TextAlignmentOptions.Center, 20f, 0f, 1000f, 64f);
            SetMessage("", MenuKit.Cream);

            BuildLogin(root);
        }

        void BuildBackdrop(RectTransform root)
        {
            MenuKit.Solid(MenuKit.Fill(root, "Page"), MenuKit.C("#0a1510"));
            if (backdrop != null)
            {
                // larger than the screen and shifted, so the campfire sits under the game's name
                var go = new GameObject("Backdrop", typeof(RectTransform));
                var bg = (RectTransform)go.transform;
                bg.SetParent(root, false);
                bg.anchorMin = bg.anchorMax = new Vector2(0.5f, 1f);
                bg.pivot = new Vector2(0.5f, 1f);
                var size = new Vector2(backdrop.rect.width, backdrop.rect.height) * (1920f / Mathf.Max(1f, backdrop.rect.width)) * BackdropScale;
                bg.sizeDelta = size;
                float fireX = CampfireOnBackdrop.x * BackdropScale - size.x / 2f;
                bg.anchoredPosition = new Vector2(LogoX - 960f - fireX, 0f);
                MenuKit.Img(bg, backdrop, Color.white).preserveAspect = false;
            }
            MenuKit.Solid(MenuKit.Fill(root, "Shade"), new Color(0.01f, 0.03f, 0.02f, 0.22f));
        }

        void BuildLogo(RectTransform s)
        {
            // a soft dark cloud so the name reads over the picture
            MenuKit.Art(MenuKit.Box(s, "LogoShade", LogoX - 520f, -30f, 1040f, 440f), "glow", new Color(0f, 0.02f, 0.01f, 0.62f));

            MenuKit.Icon(s, "leaves", LogoX + 4f, 44f, 57f, 39f, Color.white);
            MenuKit.Icon(s, "sparkle_small", LogoX - 58f, 50f, 15f, 15f, Color.white);
            MenuKit.Icon(s, "sparkle_small", LogoX + 62f, 36f, 15f, 15f, Color.white);

            if (fontOutline != null)
            {
                logoMaterial = new Material(fontOutline) { name = "Logo (runtime)" };
                logoMaterial.SetColor("_OutlineColor", MenuKit.C("#3a2006"));
                logoMaterial.SetFloat("_OutlineWidth", 0.3f);
                logoMaterial.SetFloat("_FaceDilate", 0.3f);
                logoMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.75f));
                logoMaterial.SetFloat("_UnderlayOffsetX", 0.25f);
                logoMaterial.SetFloat("_UnderlayOffsetY", -0.9f);
                logoMaterial.SetFloat("_UnderlaySoftness", 0.2f);
            }
            var gradient = new VertexGradient(MenuKit.C("#fff2b8"), MenuKit.C("#fff2b8"), MenuKit.C("#e8a93a"), MenuKit.C("#e8a93a"));
            TextMeshProUGUI Word(string text, float y)
            {
                var t = kit.Text(s, "Title", text, 112, Color.white, TextAlignmentOptions.Center, LogoX - 460f, y, 920f, 120f, true);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                if (logoMaterial != null) t.fontSharedMaterial = logoMaterial;
                t.enableVertexGradient = true;
                t.colorGradient = gradient;
                return t;
            }
            Word("Rừng", 72f);
            Word("Thì Thầm", 190f);
            MenuKit.Icon(s, "sparkle", LogoX - 186f, 132f, 27f, 27f, Color.white);
            MenuKit.Icon(s, "sparkle", LogoX + 186f, 132f, 27f, 27f, Color.white);

            MenuKit.Divider(s, LogoX - 240f, 322f, 480f);
            const string tag = "Cùng bạn bè khám phá khu rừng.";
            var sub = kit.Text(s, "Subtitle", tag, 25, MenuKit.Cream, TextAlignmentOptions.Center, LogoX - 400f, 334f, 800f, 40f, false, true);
            sub.textWrappingMode = TextWrappingModes.NoWrap;
            float half = sub.GetPreferredValues(tag).x / 2f + 24f;
            MenuKit.Icon(s, "diamond", LogoX - half, 354f, 12f, 12f, MenuKit.Gold);
            MenuKit.Icon(s, "diamond", LogoX + half, 354f, 12f, 12f, MenuKit.Gold);
        }

        void BuildMenu(RectTransform s)
        {
            panel = MenuKit.Box(s, "Menu", PanelX, PanelY, PanelW, PanelH);
            MenuKit.GoldFrame(panel, MenuKit.WithAlpha(MenuKit.Panel, 0.97f));
            const float L = 56f, W = PanelW - 2 * L;

            MenuKit.Icon(panel, "check", L + 9f, 70f, 18f, 14f, MenuKit.Green);
            var ready = kit.Text(panel, "Ready", "SẴN SÀNG KHÁM PHÁ", 16, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, L + 28f, 58f, 400f, 24f, true);
            ready.characterSpacing = 6f;
            kit.Text(panel, "Heading", "Bắt đầu hành trình", 44, MenuKit.Cream, TextAlignmentOptions.MidlineLeft, L, 86f, W, 62f, true);

            // the character this machine remembers
            var card = MenuKit.Box(panel, "Character", L, 164f, W, 110f);
            MenuKit.Card(card, MenuKit.CardFill, MenuKit.CardLine);
            var face = MenuKit.Box(card, "Portrait", 14f, 12f, 86f, 86f);
            MenuKit.Card(face, MenuKit.C("#0a1311"), MenuKit.GoldDim);
            MenuKit.Icon(face, "hero_bust", 43f, 45f, 65f, 70f, Color.white);
            kit.Text(card, "Label", "Nhân vật", 17, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, 120f, 22f, 280f, 26f);
            characterName = MenuKit.Fit(kit.Text(card, "Name", "", 27, MenuKit.Cream, TextAlignmentOptions.MidlineLeft, 120f, 48f, 250f, 40f, true), 16);
            var sw = kit.DarkButton(card, "Switch", "Đổi", W - 16f - 150f, 32f, 150f, 46f, 18, SwitchCharacter, "pencil", 18f, 18f);
            switchLabel = sw.GetComponentInChildren<TextMeshProUGUI>();

            // the server found
            var server = MenuKit.Box(panel, "Server", L, 292f, W, 108f);
            MenuKit.Card(server, MenuKit.C("#0e1f19"), MenuKit.C("#23402f"));
            serverDot = MenuKit.Icon(server, "dot", 32f, 36f, 15f, 15f, MenuKit.Muted);
            serverTitle = MenuKit.Fit(kit.Text(server, "Title", "", 21, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, 54f, 18f, W - 180f, 36f, true), 14);
            serverDetail = kit.Text(server, "Detail", "", 16, MenuKit.Muted, TextAlignmentOptions.TopLeft, 54f, 56f, W - 84f, 46f);
            signalIcon = MenuKit.Icon(server, "signal", W - 104f, 36f, 22f, 16f, MenuKit.Green);
            pingText = kit.Text(server, "Ping", "", 17, MenuKit.Muted, TextAlignmentOptions.MidlineRight, W - 96f, 20f, 76f, 32f);

            kit.GoldButton(panel, "Join", "Vào thế giới", L, 424f, W, 84f, 30, OnJoinClicked);
            kit.DarkButton(panel, "Offline", "Chơi một mình", L, 526f, W, 72f, 26, PlayOffline);
            MenuKit.Divider(panel, L + 40f, 630f, W - 80f, true, 0.5f);

            // other servers, folded away
            var other = MenuKit.Box(panel, "Other", L, 656f, W, 66f);
            MenuKit.Card(other, MenuKit.CardFill, MenuKit.CardLine);
            MenuKit.Icon(other, "server_list", 34f, 33f, 22f, 24f, MenuKit.Muted);
            kit.Text(other, "Label", "Máy chủ khác", 20, MenuKit.Cream, TextAlignmentOptions.MidlineLeft, 66f, 0f, 360f, 66f);
            otherChevron = MenuKit.Icon(other, "chevron_down", W - 36f, 33f, 18f, 11f, MenuKit.Muted);
            MenuKit.Clickable(other, () => SetOther(!otherOpen, true));

            otherArea = MenuKit.Box(panel, "OtherArea", L, 736f, W, 60f);
            customField = Input(otherArea, "CustomServer", "Địa chỉ (để trống: tự tìm)", false, 0f, 0f, W - 150f, 60f, 40);
            customField.text = ServerList.Custom;
            kit.DarkButton(otherArea, "Search", "Tìm lại", W - 138f, 0f, 138f, 60f, 19, () => { StopRejoin(); StartSearch(); });

            // the foot of the menu follows its bottom edge
            var foot = new GameObject("Footer", typeof(RectTransform));
            var fr = (RectTransform)foot.transform;
            fr.SetParent(panel, false);
            fr.anchorMin = new Vector2(0f, 0f);
            fr.anchorMax = new Vector2(1f, 0f);
            fr.pivot = new Vector2(0.5f, 0f);
            fr.sizeDelta = new Vector2(0f, 84f);
            fr.anchoredPosition = Vector2.zero;
            MenuKit.Line(fr, L, 4f, W, 1f, MenuKit.WithAlpha(MenuKit.Faint, 0.8f));
            var quit = MenuKit.Box(fr, "Quit", L - 10f, 18f, 190f, 44f);
            kit.Text(quit, "Label", "Thoát trò chơi", 18, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, 10f, 0f, 180f, 44f);
            MenuKit.Clickable(quit, Quit);
            kit.Text(fr, "Version", $"Phiên bản {Application.version} · giao thức {NetProtocol.Version}", 15, MenuKit.Faint, TextAlignmentOptions.MidlineRight,
                     L + W - 320f, 18f, 320f, 44f);

            SetOther(!string.IsNullOrEmpty(ServerList.Custom));
        }

        void BuildLogin(RectTransform root)
        {
            var dim = MenuKit.Fill(root, "Login");
            loginWindow = dim.gameObject;
            MenuKit.Solid(dim, new Color(0.01f, 0.02f, 0.02f, 0.62f)).raycastTarget = true;
            loginStage = MenuKit.Stage(dim, "Stage");
            const float W = 700f, H = 650f, L = 70f;
            var lw = MenuKit.Box(loginStage, "Window", (1920f - W) / 2f, (1080f - H) / 2f, W, H);
            MenuKit.GoldFrame(lw, MenuKit.WithAlpha(MenuKit.Panel, 0.98f));
            var small = kit.Text(lw, "Small", "ĐĂNG NHẬP", 16, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, L, 50f, 400f, 24f, true);
            small.characterSpacing = 6f;
            kit.Text(lw, "Title", "Vào thế giới", 40, MenuKit.Cream, TextAlignmentOptions.MidlineLeft, L, 76f, W - 2 * L, 56f, true);
            kit.Text(lw, "NameLabel", "Tên nhân vật", 18, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, L, 150f, 400f, 30f);
            nameField = Input(lw, "Name", "3–16 chữ, ví dụ: Khang", false, L, 182f, W - 2 * L, 60f, LoginCrypto.NameMax);
            kit.Text(lw, "PasswordLabel", "Mật khẩu", 18, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, L, 256f, 400f, 30f);
            passwordField = Input(lw, "Password", "ít nhất 4 ký tự", true, L, 288f, W - 2 * L, 60f, 64);
            rememberToggle = Toggle(lw, "Remember", "Nhớ đăng nhập trên máy này", L, 368f);
            loginMessage = kit.Text(lw, "Message", "", 18, MenuKit.Muted, TextAlignmentOptions.TopLeft, L, 414f, W - 2 * L, 60f);
            kit.GoldButton(lw, "Login", "Vào game", L, 488f, 272f, 70f, 26, () => SubmitLogin(false));
            kit.DarkButton(lw, "Create", "Tạo nhân vật mới", W - L - 272f, 488f, 272f, 70f, 22, () => SubmitLogin(true));
            kit.DarkButton(lw, "Back", "Quay lại", (W - 200f) / 2f, 576f, 200f, 46f, 18, CloseLogin, "chevron_left", 9f, 15f);
            loginWindow.SetActive(false);
        }

        // ------------------------------------------------------------------ fields
        TMP_InputField Input(Transform parent, string name, string placeholder, bool password, float x, float y, float w, float h, int limit)
        {
            var rt = MenuKit.Box(parent, name, x, y, w, h);
            rt.gameObject.SetActive(false);   // wired up before it wakes
            var bg = MenuKit.Card(rt, MenuKit.C("#0a1210"), MenuKit.C("#3a4a44"));
            bg.raycastTarget = true;
            var area = MenuKit.Fill(rt, "Text Area", 0f);
            area.offsetMin = new Vector2(18f, 6f);
            area.offsetMax = new Vector2(-18f, -6f);
            area.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI Stretched(string n, string text, float size, Color c)
            {
                var t = kit.Text(area, n, text, size, c, TextAlignmentOptions.MidlineLeft, 0f, 0f, 10f, 10f);
                t.rectTransform.anchorMin = Vector2.zero;
                t.rectTransform.anchorMax = Vector2.one;
                t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                return t;
            }
            var ph = Stretched("Placeholder", placeholder, 20, MenuKit.Faint);
            ph.fontStyle = FontStyles.Italic;
            var text = Stretched("Text", "", 22, MenuKit.Cream);
            var field = rt.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = ph;
            field.targetGraphic = bg;
            field.characterLimit = limit;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            if (password) field.asteriskChar = '•';
            field.caretColor = MenuKit.GoldText;
            field.selectionColor = MenuKit.WithAlpha(MenuKit.Gold, 0.35f);
            var cb = field.colors;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            field.colors = cb;
            rt.gameObject.SetActive(true);
            return field;
        }

        Toggle Toggle(Transform parent, string name, string label, float x, float y)
        {
            var rt = MenuKit.Box(parent, name, x, y, 560f, 34f);
            var box = MenuKit.Box(rt, "Box", 0f, 0f, 34f, 34f);
            var bg = MenuKit.Card(box, MenuKit.C("#0a1210"), MenuKit.GoldDim);
            bg.raycastTarget = true;
            var mark = MenuKit.Icon(box, "check", 17f, 17f, 20f, 15f, MenuKit.GoldText);
            kit.Text(rt, "Label", label, 19, MenuKit.Cream, TextAlignmentOptions.MidlineLeft, 48f, 0f, 500f, 34f);
            var toggle = rt.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = bg;
            toggle.graphic = mark;
            toggle.isOn = true;
            return toggle;
        }
    }
}

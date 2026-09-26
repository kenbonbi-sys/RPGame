using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// The character creator (Tạo Nhân Vật), after D&D Beyond's: choose a class from its cards
    /// (filtered by how hard it is to play; a second click on the chosen card opens its details
    /// and seven skills), then a people, then the looks and the weapon, then check it all. On the
    /// right the hero walks, swings and casts on a stone pedestal with the ability scores the
    /// choice makes (the standard array placed by the class, the people's increases, D&D
    /// modifiers and saving throws). It opens by itself for a hero with no class yet — a new one,
    /// or an older one after classes came — and from the character sheet to change the looks
    /// later (people and class stay). Offline the choice is applied at once, online the server is
    /// asked (<see cref="CharacterChoice"/>). Built at run time in the green-and-gold look of
    /// <see cref="MenuKit"/>, on a 1920×1080 stage.
    /// </summary>
    public class CharacterCreatorUI : UIPanel
    {
        public TMP_FontAsset font;
        public Material fontOutline;

        public enum Mode { New, Looks }

        public static CharacterCreatorUI I { get; private set; }
        /// <summary>Automated runs (tests, tours, bots) keep a hero with no class instead of opening the creator.</summary>
        public static bool Suppressed;

        // the preview panel on the right of every step
        const float PvX = 1320f, PvY = 140f, PvW = 552f, PvH = 800f;
        // the left part of the page
        const float Left = 58f, LeftW = 1224f;
        const float FootY = 952f;

        static readonly string[] Keys = { "Q", "W", "E", "R", "A", "S", "D" };
        static readonly string[] StepNames = { "Lớp", "Chủng tộc", "Ngoại hình", "Hoàn tất" };

        Mode mode;
        int step;
        Complexity? filter;
        HeroLook look = new HeroLook();
        bool built, askedOnce;
        float previewT;
        int previewDir, previewPose;

        MenuKit kit;
        RectTransform root, stage, details;
        Image previewImage, detailsImage;

        static GameDatabase Db => GameManager.I != null ? GameManager.I.db : null;

        protected override void Awake()
        {
            base.Awake();
            I = this;
            blocksGameplay = true;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        /// <summary>Opens the creator for the hero on this screen.</summary>
        public void Open(Mode m)
        {
            var hero = Players.Local;
            if (hero == null || hero.stats == null || Db == null || Db.classes.Count == 0) return;
            if (!built) Build();
            mode = m;
            look = hero.stats.look.Clone();
            if (!look.HasClass)
            {
                var first = Db.classes[0];
                look.cls = first.id;
                look.race = Db.races.Count > 0 ? Db.races[0].id : "";
                look.weapon = first.DefaultWeapon;
                look.hair = 1;
                look.hairColor = 2;
            }
            step = m == Mode.Looks ? 2 : 0;
            Show();
            Refresh();
        }

        static bool Automated => Suppressed || Application.isBatchMode || AutoShot.Active || NetSmoke.Active || LoadBot.Active || BackdropShot.Active;

        /// <summary>-creatorshot &lt;folder&gt;: pictures of every step of the creator (a class picked, a people, looks) and of the class details, then quit.</summary>
        static string ShotFolder
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args, "-creatorshot");
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            }
        }

        System.Collections.IEnumerator Start()
        {
            string folder = ShotFolder;
            if (folder == null) yield break;
            System.IO.Directory.CreateDirectory(folder);
            float deadline = Time.realtimeSinceStartup + 30f;
            while ((Players.Local == null || GameManager.I == null || GameManager.I.State != GameState.Playing) && Time.realtimeSinceStartup < deadline) yield return null;
            if (HUD.I != null && HUD.I.help != null) HUD.I.help.Close();
            yield return new WaitForSecondsRealtime(0.5f);
            Open(Mode.New);
            string[] steps = { "1_class", "2_race", "3_looks", "4_summary" };
            for (int s = 0; s < 4; s++)
            {
                if (s == 0) { look.cls = "wizard"; look.weapon = "staff"; }
                if (s == 1) { look.race = "dwarf"; look.beard = 2; }
                if (s == 2) { look.hair = 3; look.hairColor = 6; }
                step = s;
                Refresh();
                yield return new WaitForSecondsRealtime(0.8f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, $"creator_{steps[s]}.png"));
                yield return new WaitForSecondsRealtime(0.4f);
            }
            step = 0;
            Refresh();
            OpenDetails(Db.Class(look.cls));
            yield return new WaitForSecondsRealtime(0.8f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, "creator_5_details.png"));
            yield return new WaitForSecondsRealtime(0.4f);
            Application.Quit(0);
        }

        protected override void Update()
        {
            base.Update();
            var hero = Players.Local;
            var gm = GameManager.I;
            // a hero with no class yet: the creator opens by itself once it can
            // online: once the server's sheet is here (it comes before "Ready"), or a hero with a class would see it too
            bool sheetHere = GameSession.IsAuthority || (OnlineSession.I != null && OnlineSession.I.InWorld);
            if (!IsOpen && !askedOnce && hero != null && hero.stats != null && !hero.stats.HasClass && !Automated && sheetHere &&
                gm != null && gm.State == GameState.Playing && (SceneLoader.I == null || !SceneLoader.I.Busy))
            {
                askedOnce = true;
                Open(Mode.New);
            }
            if (hero != null && hero.stats != null && hero.stats.HasClass) askedOnce = false;
            if (!IsOpen) return;
            if (stage != null) MenuKit.FitStage(stage);
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && details != null) CloseDetails();
            AnimatePreview();
        }

        // ================================================================== frame of every step
        void Build()
        {
            built = true;
            kit = new MenuKit(font, fontOutline);
            root = (RectTransform)window;
            if (root == null) root = (RectTransform)transform;
            MenuKit.Solid(MenuKit.Fill(root, "Backdrop"), MenuKit.Page).raycastTarget = true;
            stage = MenuKit.Stage(root, "Stage");
        }

        void GoTo(int s)
        {
            if (mode == Mode.Looks && s < 2) return;
            step = Mathf.Clamp(s, 0, 3);
            Refresh();
        }

        void Next()
        {
            if (step < 3)
            {
                GoTo(step + 1);
                return;
            }
            CharacterChoice.Choose(look);
            AudioManager.Play("sfx_quest", 0.8f);
            Close();
        }

        void Refresh()
        {
            if (stage == null) return;
            for (int i = stage.childCount - 1; i >= 0; i--) Destroy(stage.GetChild(i).gameObject);
            previewImage = detailsImage = null;
            details = null;
            var db = Db;
            if (db == null || db.Class(look.cls) == null || db.Race(look.race) == null) return;
            BuildHeader();
            BuildFooter();
            switch (step)
            {
                case 0: BuildClasses(); break;
                case 1: BuildRaces(); break;
                case 2: BuildLooks(); break;
                default: BuildSummary(); break;
            }
            BuildPreview();
        }

        void BuildHeader()
        {
            string small = mode == Mode.New ? "TẠO NHÂN VẬT" : "ĐỔI NGOẠI HÌNH";
            MenuKit.Icon(stage, "diamond", Left + 8f, 38f, 9f, 9f, MenuKit.GoldDim);
            var label = kit.Text(stage, "Small", small, 15, MenuKit.C("#b8964e"), TextAlignmentOptions.MidlineLeft, Left + 22f, 26f, 300f, 24f, true);
            label.characterSpacing = 4f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            float lx = Left + 22f + label.GetPreferredValues(small).x + 14f;
            if (lx < 400f) MenuKit.Line(stage, lx, 38f, 400f - lx, 1f, MenuKit.WithAlpha(MenuKit.Gold, 0.45f));

            string title, sub;
            switch (step)
            {
                case 0: title = "Chọn lớp nhân vật"; sub = "Chọn phong cách chiến đấu của bạn."; break;
                case 1: title = "Chọn chủng tộc"; sub = "Mỗi chủng tộc mang một lợi thế riêng."; break;
                case 2: title = "Ngoại hình & vũ khí"; sub = "Tạo diện mạo cho hành trình của bạn."; break;
                default:
                    title = mode == Mode.New ? "Sẵn sàng lên đường?" : "Xem lại ngoại hình";
                    sub = mode == Mode.New ? "Kiểm tra nhân vật trước khi bắt đầu." : "Kiểm tra trước khi lưu.";
                    break;
            }
            kit.Text(stage, "Title", title, 50, MenuKit.Cream, TextAlignmentOptions.MidlineLeft, Left + 14f, 50f, 1000f, 66f, true);
            kit.Text(stage, "Subtitle", sub, 21, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, Left + 16f, 114f, 1000f, 34f);

            // the steps, top right
            const float pw = 180f, gap = 16f, py = 40f, ph = 52f;
            float x0 = 1864f - 4 * pw - 3 * gap;
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                float x = x0 + i * (pw + gap);
                bool current = i == step;
                bool locked = mode == Mode.Looks && i < 2;
                bool done = i < step || locked;
                var pill = MenuKit.Box(stage, "Step" + i, x, py, pw, ph);
                MenuKit.Card(pill, current ? MenuKit.C("#1c1a10") : MenuKit.CardFill,
                             current ? MenuKit.Gold : done ? MenuKit.C("#3c4a40") : MenuKit.CardLine, current);
                float textW = done ? pw - 34f : pw;
                var t = kit.Text(pill, "Label", $"{i + 1}   {StepNames[i]}", 17, current ? MenuKit.GoldText : done ? MenuKit.Cream : MenuKit.Muted,
                                 TextAlignmentOptions.Center, 0f, 0f, textW, ph, current);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                if (done) MenuKit.Icon(pill, "check", pw - 30f, ph / 2f, 18f, 14f, MenuKit.GoldText);
                if (!locked && !current) MenuKit.Clickable(pill, () => GoTo(k));
                if (i < 3) MenuKit.Icon(stage, "diamond", x + pw + gap / 2f, py + ph / 2f, 8f, 8f, MenuKit.GoldDim);
            }
        }

        void BuildFooter()
        {
            // a band across the whole screen, however wide
            var band = MenuKit.Box(stage, "FootBand", -2000f, FootY, 5920f, 1080f - FootY + 400f);
            MenuKit.Solid(band, MenuKit.Band);
            MenuKit.Line(stage, -2000f, FootY, 5920f, 1f, MenuKit.C("#1f2c29"));

            int first = mode == Mode.Looks ? 2 : 0;
            if (step > first)
                kit.DarkButton(stage, "Back", "Quay lại", Left, 980f, 250f, 70f, 22, () => GoTo(step - 1), "chevron_left", 11f, 18f);
            else if (mode == Mode.Looks)
                kit.DarkButton(stage, "Cancel", "Hủy", Left, 980f, 250f, 70f, 22, Close, "close", 14f, 14f);
            else
            {
                MenuKit.Icon(stage, "info", Left + 12f, 1015f, 22f, 22f, MenuKit.Muted);
                kit.Text(stage, "Hint", "Bấm lần nữa vào lớp đang chọn để xem chi tiết và kỹ năng.", 17, MenuKit.Muted,
                         TextAlignmentOptions.MidlineLeft, Left + 34f, 998f, 900f, 34f);
            }

            string note = null;
            if (step == 2) note = mode == Mode.New ? "Bạn có thể đổi ngoại hình và vũ khí sau." : "Lớp và chủng tộc giữ nguyên; chỉ ngoại hình và vũ khí đổi.";
            if (step == 3 && mode == Mode.New) note = "Lớp và chủng tộc chỉ chọn một lần.\nNgoại hình và vũ khí đổi lại được ở bảng Nhân Vật (C).";
            if (note != null)
                kit.Text(stage, "Note", note, 16, MenuKit.Muted, TextAlignmentOptions.Center, 560f, 982f, 800f, 66f);

            string next = step < 3 ? "Tiếp tục" : mode == Mode.New ? "Bắt đầu phiêu lưu" : "Lưu ngoại hình";
            kit.GoldButton(stage, "Next", next, 1864f - 360f, 978f, 360f, 74f, 25, Next);
        }

        // ================================================================== step 1: classes
        void BuildClasses()
        {
            var db = Db;
            kit.Text(stage, "FilterLabel", "Độ khó", 19, MenuKit.Cream, TextAlignmentOptions.MidlineLeft, Left + 4f, 186f, 120f, 42f, true);
            string[] names = { "Tất cả", "Dễ", "Vừa", "Khó" };
            for (int i = 0; i < 4; i++)
            {
                Complexity? f = i == 0 ? (Complexity?)null : (Complexity)(i - 1);
                bool on = filter == f;
                var pill = MenuKit.Box(stage, "Filter" + i, 184f + i * 142f, 186f, 128f, 42f);
                if (on)
                {
                    MenuKit.Art(pill, "round_grad", MenuKit.ButtonGold);
                    MenuKit.Art(MenuKit.Fill(pill, "Rim"), "round_line", MenuKit.C("#b07c26"));
                }
                else MenuKit.Card(pill, MenuKit.CardFill, MenuKit.CardLine);
                kit.Text(pill, "Label", names[i], 18, on ? MenuKit.Ink : MenuKit.Muted, TextAlignmentOptions.Center, 0f, 0f, 128f, 42f, on);
                if (!on) MenuKit.Clickable(pill, () => { filter = f; Refresh(); });
            }
            int shown = 0;
            foreach (var cls in db.classes)
            {
                if (cls == null || (filter.HasValue && cls.complexity != filter.Value)) continue;
                int col = shown % 3, row = shown / 3;
                ClassCard(cls, Left + col * 412f, 250f + row * 174f, 398f, 158f);
                shown++;
            }
        }

        void ClassCard(ClassDef cls, float x, float y, float w, float h)
        {
            bool chosen = look.cls == cls.id;
            var card = MenuKit.Box(stage, "Class_" + cls.id, x, y, w, h);
            Chosen(card, chosen, w);
            // a breath of the class's colour behind its emblem and portrait
            MenuKit.Art(MenuKit.Box(card, "Tint", 0f, 0f, 250f, h), "glow", MenuKit.WithAlpha(cls.color, chosen ? 0.2f : 0.13f));
            MenuKit.Icon(card, cls.emblem, 38f, h / 2f, 40f, 40f);
            var portraitLook = look.Clone();
            portraitLook.cls = cls.id;
            portraitLook.weapon = cls.DefaultWeapon;
            portraitLook.cloth = -1;
            MenuKit.Icon(card, HeroArt.Portrait(portraitLook), 116f, h / 2f, 96f, 96f);
            MenuKit.Fit(kit.Text(card, "Name", cls.displayName, 23, MenuKit.Cream, TextAlignmentOptions.MidlineLeft, 172f, 30f, w - 172f - 40f, 36f, true), 15);
            string tag = !string.IsNullOrEmpty(cls.tagline) ? cls.tagline : cls.role;
            MenuKit.Fit(kit.Text(card, "Tag", tag, 16, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, 172f, 66f, w - 172f - 16f, 28f), 12);
            kit.Badge(card, ClassDef.ComplexityName(cls.complexity), DifficultyColor(cls.complexity), 172f, 104f, 66f, 26f, 15);
            MenuKit.Clickable(card, () =>
            {
                if (look.cls == cls.id)
                {
                    OpenDetails(cls);
                    return;
                }
                look.cls = cls.id;
                if (!cls.Allows(look.weapon)) look.weapon = cls.DefaultWeapon;
                look.cloth = -1;
                Refresh();
            });
        }

        /// <summary>A card's face: gold rim and a check in the corner when it is the one chosen.</summary>
        static void Chosen(RectTransform card, bool chosen, float w)
        {
            MenuKit.Card(card, chosen ? MenuKit.CardChosen : MenuKit.CardFill, chosen ? MenuKit.Gold : MenuKit.CardLine, chosen);
            if (!chosen) return;
            var tab = MenuKit.Box(card, "Check", w - 34f, 0f, 34f, 32f);
            MenuKit.Art(tab, "round_fill", MenuKit.Gold);
            MenuKit.Icon(tab, "check", 17f, 16f, 18f, 14f, MenuKit.Ink);
        }

        static Color DifficultyColor(Complexity c) => c == Complexity.Low ? MenuKit.Easy : c == Complexity.Average ? MenuKit.Average : MenuKit.Hard;

        // ------------------------------------------------------------------ class details
        void OpenDetails(ClassDef cls)
        {
            if (cls == null) return;
            CloseDetails();
            details = MenuKit.Box(stage, "Details", -2000f, -1000f, 5920f, 3080f);
            MenuKit.Solid(details, new Color(0.01f, 0.02f, 0.02f, 0.72f)).raycastTarget = true;
            const float W = 1560f, H = 960f;
            var w = MenuKit.Box(details, "Window", 2000f + 180f, 1000f + 60f, W, H);
            MenuKit.GoldFrame(w, MenuKit.WithAlpha(MenuKit.Panel, 0.99f));

            // title
            var small = kit.Text(w, "Small", "CHI TIẾT LỚP", 15, MenuKit.C("#b8964e"), TextAlignmentOptions.Center, 0f, 24f, W, 24f, true);
            small.characterSpacing = 4f;
            MenuKit.Line(w, W / 2f - 190f, 36f, 90f, 1f, MenuKit.WithAlpha(MenuKit.Gold, 0.5f));
            MenuKit.Line(w, W / 2f + 100f, 36f, 90f, 1f, MenuKit.WithAlpha(MenuKit.Gold, 0.5f));
            kit.Text(w, "Name", cls.displayName, 42, MenuKit.Cream, TextAlignmentOptions.Center, 0f, 50f, W, 54f, true);
            kit.Text(w, "English", cls.englishName, 17, MenuKit.Muted, TextAlignmentOptions.Center, 0f, 104f, W, 24f);
            var close = MenuKit.Box(w, "Close", W - 76f, 26f, 46f, 46f);
            MenuKit.Card(close, MenuKit.C("#0f1917"), MenuKit.GoldDim);
            MenuKit.Icon(close, "close", 23f, 23f, 16f, 16f, MenuKit.GoldText);
            MenuKit.Clickable(close, CloseDetails);
            MenuKit.Line(w, 30f, 140f, W - 60f, 1f, MenuKit.C("#24322e"));

            // left: the hero, how hard, what it is
            MenuKit.Art(MenuKit.Box(w, "Dungeon", 44f, 150f, 432f, 304f), "dungeon", Color.white);
            MenuKit.Art(MenuKit.Box(w, "Pedestal", 170f, 350f, 180f, 81f), "pedestal", Color.white);
            var heroLook = look.Clone();
            if (heroLook.cls != cls.id)
            {
                heroLook.cls = cls.id;
                heroLook.weapon = cls.DefaultWeapon;
            }
            detailsImage = MenuKit.Img(MenuKit.CenterPivot(MenuKit.Box(w, "Hero", 164f, 199f, 192f, 192f)), HeroArt.Portrait(heroLook), Color.white);
            detailsImage.preserveAspect = true;
            kit.Text(w, "DifficultyLabel", "Độ khó", 18, MenuKit.Cream, TextAlignmentOptions.MidlineRight, 44f, 462f, 196f, 30f, true);
            kit.Badge(w, ClassDef.ComplexityName(cls.complexity), DifficultyColor(cls.complexity), 252f, 464f, 70f, 28f, 15);
            kit.Text(w, "Desc", cls.description, 16, MenuKit.Muted, TextAlignmentOptions.Top, 44f, 506f, 432f, 70f);
            MenuKit.Divider(w, 44f, 592f, 432f);
            var rows = new List<(string icon, string label, string value)>
            {
                ("die_gold", "Xúc xắc máu", "d" + cls.hitDie),
                ("armor", "Giáp", ArmorName(cls.armor)),
                ("fist", "Chỉ số chính", Join(cls.primary)),
                ("book", "Phép dùng", CoreStats.Name(cls.casting)),
                ("shield", "Kháng", Join(cls.savingThrows)),
                ("swords", "Vũ khí", WeaponList(cls)),
            };
            for (int i = 0; i < rows.Count; i++)
            {
                float cy = 624f + i * 46f;
                MenuKit.Icon(w, rows[i].icon, 62f, cy, 26f, 26f, Color.white);
                kit.Text(w, "Label", rows[i].label, 17, MenuKit.GoldText, TextAlignmentOptions.MidlineLeft, 86f, cy - 15f, 150f, 30f);
                kit.Text(w, "Value", rows[i].value, 17, MenuKit.Cream, TextAlignmentOptions.TopLeft, 240f, cy - 11f, 236f, 50f);
            }
            MenuKit.Line(w, 504f, 160f, 1f, 730f, MenuKit.C("#24322e"));

            // right: the seven skills
            var head = kit.Text(w, "Skills", "Kỹ năng", 26, MenuKit.Cream, TextAlignmentOptions.MidlineLeft, 534f, 150f, 200f, 40f, true);
            head.textWrappingMode = TextWrappingModes.NoWrap;
            kit.Text(w, "Count", "7 kỹ năng", 16, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, 534f + head.GetPreferredValues("Kỹ năng").x + 14f, 152f, 200f, 40f);
            for (int i = 0; i < 7; i++)
            {
                float top = 200f + i * 98f, cy = top + 49f;
                var a = Skill(cls, i, cls.DefaultWeapon);
                kit.KeyCap(w, Keys[i], 562f, cy, 48f);
                var icon = MenuKit.Box(w, "SkillIcon", 606f, cy - 36f, 72f, 72f);
                MenuKit.Card(icon, MenuKit.C("#0c1413"), MenuKit.CardLine);
                if (a != null && a.icon != null) MenuKit.Icon(icon, a.icon, 36f, 36f, 64f, 64f);
                kit.Text(w, "SkillName", a != null ? a.displayName : "(sắp có)", 20, a != null ? MenuKit.Cream : MenuKit.Faint,
                         TextAlignmentOptions.BottomLeft, 698f, top + 8f, 820f, 32f, true);
                if (a != null)
                    kit.Text(w, "SkillDesc", MenuKit.GoldNumbers(a.description), 16, MenuKit.Muted, TextAlignmentOptions.TopLeft, 698f, top + 42f, 820f, 54f);
                if (i < 6) MenuKit.Line(w, 534f, top + 97f, W - 534f - 40f, 1f, MenuKit.C("#1c2926"));
            }

            MenuKit.Line(w, 30f, 898f, W - 60f, 1f, MenuKit.C("#24322e"));
            kit.DarkButton(w, "Done", "Đóng", W - 44f - 200f, 908f, 200f, 42f, 18, CloseDetails);
            AudioManager.Play("sfx_ui_open", 0.5f);
        }

        void CloseDetails()
        {
            if (details == null) return;
            Destroy(details.gameObject);
            details = null;
            detailsImage = null;
        }

        /// <summary>The skill on key <paramref name="i"/> (Q W E R A S D) of a class carrying that weapon: Q is the weapon's own attack unless it is a focus.</summary>
        static AbilityDef Skill(ClassDef cls, int i, string weapon)
        {
            string id = cls.kit != null && i < cls.kit.Length ? cls.kit[i] : "";
            var wk = WeaponKinds.Get(weapon);
            if (i == 0 && wk != null && !wk.focus) id = wk.basic;
            return Db != null ? Db.Ability(id) : null;
        }

        static string WeaponList(ClassDef cls)
        {
            var sb = new StringBuilder();
            foreach (var id in cls.weapons)
            {
                var wk = WeaponKinds.Get(id);
                sb.Append(sb.Length > 0 ? ", " : "").Append(wk != null ? wk.name : id);
            }
            return sb.ToString();
        }

        static string ArmorName(ArmorKind a)
        {
            switch (a)
            {
                case ArmorKind.Light: return "Nhẹ";
                case ArmorKind.Medium: return "Vừa";
                case ArmorKind.Heavy: return "Nặng";
                case ArmorKind.Unarmored: return "Không giáp (thân thể rèn luyện)";
                default: return "Áo choàng";
            }
        }

        static string Join(CoreStat[] a)
        {
            var sb = new StringBuilder();
            foreach (var x in a) sb.Append(sb.Length > 0 ? ", " : "").Append(CoreStats.Name(x));
            return sb.ToString();
        }

        // ================================================================== step 2: peoples
        void BuildRaces()
        {
            int i = 0;
            foreach (var race in Db.races)
            {
                if (race == null) continue;
                int col = i % 3, row = i / 3;
                RaceCard(race, Left + col * 418f, 190f + row * 236f, 404f, 220f);
                i++;
            }
        }

        void RaceCard(RaceDef race, float x, float y, float w, float h)
        {
            bool chosen = look.race == race.id;
            var card = MenuKit.Box(stage, "Race_" + race.id, x, y, w, h);
            Chosen(card, chosen, w);
            var portraitLook = look.Clone();
            portraitLook.race = race.id;
            portraitLook.skin = 0;
            portraitLook.beard = race.beards ? 2 : 0;
            MenuKit.Icon(card, HeroArt.Portrait(portraitLook), 88f, h / 2f + 4f, 128f, 128f);
            MenuKit.Fit(kit.Text(card, "Name", race.displayName, 24, MenuKit.Cream, TextAlignmentOptions.MidlineLeft, 170f, 30f, w - 170f - 40f, 36f, true), 16);
            kit.Text(card, "English", race.englishName, 16, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, 170f, 66f, w - 186f, 26f);
            var lines = BonusLines(race);
            for (int l = 0; l < lines.Count && l < 2; l++)
                MenuKit.Fit(kit.Text(card, "Bonus", lines[l], 19, MenuKit.Cream, TextAlignmentOptions.MidlineLeft, 170f, 112f + l * 34f, w - 186f, 32f), 13);
            MenuKit.Clickable(card, () =>
            {
                if (look.race == race.id) return;
                look.race = race.id;
                look.skin = 0;
                look.beard = race.beards ? 2 : 0;
                if (race.dragonHead) look.hairColor = 6;
                Refresh();
            });
        }

        /// <summary>A people's increases as the cards show them: "+2 Khéo Léo" a line, the smaller ones together when there are three.</summary>
        static List<string> BonusLines(RaceDef race)
        {
            string gold = MenuKit.Hex(MenuKit.GoldText);
            var parts = new List<(int b, string text)>();
            bool allOne = true;
            foreach (var a in CoreStats.SheetOrder)
            {
                int b = race.Bonus(a);
                allOne &= b == 1;
                if (b != 0) parts.Add((b, $"<color={gold}><b>{(b > 0 ? "+" : "")}{b}</b></color> {CoreStats.Name(a)}"));
            }
            if (allOne) return new List<string> { $"<color={gold}><b>+1</b></color> mọi chỉ số" };
            parts.Sort((p, q) => q.b.CompareTo(p.b));
            var lines = new List<string>();
            if (parts.Count <= 2)
                foreach (var p in parts) lines.Add(p.text);
            else
            {
                lines.Add(parts[0].text);
                var rest = new StringBuilder();
                for (int i = 1; i < parts.Count; i++) rest.Append(i > 1 ? "  ·  " : "").Append(parts[i].text);
                lines.Add(rest.ToString());
            }
            return lines;
        }

        /// <summary>A people's traits, one per line of <see cref="RaceDef.traits"/> ("Tên: what it does").</summary>
        static List<(string name, string desc)> Traits(RaceDef race)
        {
            var list = new List<(string, string)>();
            if (string.IsNullOrEmpty(race.traits)) return list;
            foreach (var raw in race.traits.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                int c = line.IndexOf(':');
                if (c > 0)
                {
                    string desc = line.Substring(c + 1).Trim();
                    if (desc.Length > 0) desc = char.ToUpperInvariant(desc[0]) + desc.Substring(1);
                    list.Add((line.Substring(0, c).Trim(), desc));
                }
                else list.Add((line.TrimEnd('.'), ""));
            }
            return list;
        }

        // ================================================================== step 3: looks
        void BuildLooks()
        {
            var db = Db;
            var race = db.Race(look.race);
            var cls = db.Class(look.cls);
            bool dragon = race.dragonHead;
            int skins = race.skins != null && race.skins.Length > 0 ? race.skins.Length : 1;
            const float cw = 600f, gap = 24f, rh = 72f;
            float rx = Left + cw + gap;

            float y = 196f;
            kit.Section(stage, "Diện mạo", Left, y, LeftW);
            y += 28f;
            SkinCell(race, skins, Left, y, cw, 96f);
            Option(dragon ? "Màu mào" : "Màu mắt", rx, y, cw, 96f,
                   dragon ? HeroLook.HairColors[look.hairColor].name : HeroLook.EyeColors[look.eyes].name,
                   d =>
                   {
                       if (dragon) look.hairColor = Wrap(look.hairColor + d, HeroLook.HairColors.Length);
                       else look.eyes = Wrap(look.eyes + d, HeroLook.EyeColors.Length);
                   },
                   dragon ? HeroLook.HairColors[look.hairColor].color : HeroLook.EyeColors[look.eyes].color);
            y += 96f + 32f;

            kit.Section(stage, "Tóc & trang phục", Left, y, LeftW);
            y += 28f;
            var cells = new List<Action<float, float>>();
            if (!dragon)
            {
                cells.Add((cx, cy) => Option("Kiểu tóc", cx, cy, cw, rh, HeroLook.HairStyles[look.hair],
                                             d => look.hair = Wrap(look.hair + d, HeroLook.HairStyles.Length)));
                cells.Add((cx, cy) => Option("Màu tóc", cx, cy, cw, rh, HeroLook.HairColors[look.hairColor].name,
                                             d => look.hairColor = Wrap(look.hairColor + d, HeroLook.HairColors.Length), HeroLook.HairColors[look.hairColor].color));
                cells.Add((cx, cy) => Option("Râu", cx, cy, cw, rh, HeroLook.Beards[look.beard],
                                             d => look.beard = Wrap(look.beard + d, HeroLook.Beards.Length)));
            }
            else
                cells.Add((cx, cy) => Option("Màu mắt", cx, cy, cw, rh, HeroLook.EyeColors[look.eyes].name,
                                             d => look.eyes = Wrap(look.eyes + d, HeroLook.EyeColors.Length), HeroLook.EyeColors[look.eyes].color));
            cells.Add((cx, cy) => Option("Màu trang phục", cx, cy, cw, rh, look.cloth < 0 ? "Mặc định của lớp" : HeroLook.Cloths[look.cloth].name,
                                         d => look.cloth = Wrap(look.cloth + 1 + d, HeroLook.Cloths.Length + 1) - 1,
                                         look.cloth < 0 ? cls.cloth : HeroLook.Cloths[look.cloth].color));
            if (cls.id == "wizard" || cls.id == "ranger" || cls.id == "rogue" || cls.id == "warlock")
                cells.Add((cx, cy) => Option(cls.id == "wizard" ? "Mũ phù thủy" : "Mũ trùm", cx, cy, cw, rh, look.bareHead ? "Bỏ" : "Đội",
                                             d => look.bareHead = !look.bareHead));
            for (int i = 0; i < cells.Count; i++)
                cells[i](i % 2 == 0 ? Left : rx, y + (i / 2) * (rh + 12f));
            y += ((cells.Count + 1) / 2) * (rh + 12f) + 20f;

            kit.Section(stage, "Vũ khí", Left, y, LeftW);
            y += 28f;
            var weapons = cls.weapons;
            var wk = WeaponKinds.Get(look.weapon);
            Option("Loại vũ khí", Left, y, cw, rh, wk != null ? wk.name : look.weapon,
                   d =>
                   {
                       int i = Array.IndexOf(weapons, look.weapon);
                       look.weapon = weapons[Wrap((i < 0 ? 0 : i) + d, weapons.Length)];
                   }, null, HeroArt.WeaponIcon(look));
            var metals = CharacterChoice.StarterMetals;
            Option("Kim loại vũ khí", rx, y, cw, rh, HeroLook.Metals[look.metal].name,
                   d =>
                   {
                       int i = Array.IndexOf(metals, look.metal);
                       look.metal = metals[Wrap((i < 0 ? 0 : i) + d, metals.Length)];
                   }, HeroLook.Metals[look.metal].color);
            y += rh + 10f;
            if (wk != null)
                kit.Text(stage, "WeaponHint", wk.description, 16, MenuKit.Muted, TextAlignmentOptions.TopLeft, Left + 6f, y, LeftW, 28f);
            y += 40f;

            kit.DarkButton(stage, "Random", "Ngẫu nhiên", Left, y, 260f, 58f, 20, () =>
            {
                var rnd = new System.Random();
                look.skin = rnd.Next(skins);
                look.hair = rnd.Next(HeroLook.HairStyles.Length);
                look.hairColor = rnd.Next(HeroLook.HairColors.Length);
                look.beard = race.beards ? rnd.Next(1, 3) : rnd.Next(4) == 0 ? 1 : 0;
                look.eyes = rnd.Next(HeroLook.EyeColors.Length);
                look.cloth = rnd.Next(-1, HeroLook.Cloths.Length);
                look.metal = metals[rnd.Next(metals.Length)];
                Refresh();
            }, "die", 30f, 30f);
        }

        /// <summary>The skin (or scale) colours as swatches to click; dragons name their ancestry.</summary>
        void SkinCell(RaceDef race, int skins, float x, float y, float w, float h)
        {
            var cell = MenuKit.Box(stage, "Opt_Skin", x, y, w, h);
            MenuKit.Card(cell, MenuKit.CardFill, MenuKit.CardLine);
            bool dragon = race.dragonHead;
            kit.Text(cell, "Label", dragon ? "Màu vảy" : "Màu da", 17, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, 18f, 0f, 150f, h);
            string tone = dragon
                ? HeroLook.DraconicNames[Mathf.Clamp(look.skin, 0, HeroLook.DraconicNames.Length - 1)] + " · kháng " + TypeName(look.DraconicElement)
                : $"Tông {look.skin + 1}/{skins}";
            kit.Text(cell, "Tone", tone, 16, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, 170f, 10f, w - 186f, 24f);
            for (int i = 0; i < skins && race.skins != null && i < race.skins.Length; i++)
            {
                int k = i;
                bool on = look.skin == i;
                var sw = MenuKit.Box(cell, "Swatch" + i, 170f + i * 48f, 42f, 38f, 38f);
                MenuKit.Card(sw, race.skins[i], on ? MenuKit.Cream : MenuKit.C("#05090a"), on);
                if (!on) MenuKit.Clickable(sw, () => { look.skin = k; Refresh(); });
            }
        }

        /// <summary>One choice of the looks: its name, a swatch or a picture when it has one, the value, and ‹ › to step through.</summary>
        void Option(string label, float x, float y, float w, float h, string value, Action<int> change, Color? swatch = null, Sprite picture = null)
        {
            var cell = MenuKit.Box(stage, "Opt_" + label, x, y, w, h);
            MenuKit.Card(cell, MenuKit.CardFill, MenuKit.CardLine);
            kit.Text(cell, "Label", label, 17, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, 18f, 0f, 150f, h);
            float vx = 170f;
            if (swatch.HasValue)
            {
                var sw = MenuKit.Box(cell, "Swatch", vx, (h - 36f) / 2f, 36f, 36f);
                MenuKit.Card(sw, swatch.Value, MenuKit.C("#05090a"));
                vx += 50f;
            }
            if (picture != null)
            {
                MenuKit.Icon(cell, picture, vx + 22f, h / 2f, 44f, 44f);
                vx += 56f;
            }
            MenuKit.Fit(kit.Text(cell, "Value", value, 19, MenuKit.Cream, TextAlignmentOptions.MidlineLeft, vx, 0f, w - vx - 132f, h, true), 13);
            MenuKit.ArrowButton(cell, "Prev", false, w - 122f, (h - 44f) / 2f, 50f, 44f, () => { change(-1); Refresh(); });
            MenuKit.ArrowButton(cell, "Next", true, w - 64f, (h - 44f) / 2f, 50f, 44f, () => { change(1); Refresh(); });
        }

        static int Wrap(int i, int n) => n <= 0 ? 0 : ((i % n) + n) % n;

        static string TypeName(DamageType t)
        {
            switch (t)
            {
                case DamageType.Fire: return "Lửa";
                case DamageType.Ice: return "Băng";
                case DamageType.Lightning: return "Lôi";
                case DamageType.Poison: return "Độc";
                case DamageType.Holy: return "Thánh";
                case DamageType.Dark: return "Ám";
                default: return "Vật lý";
            }
        }

        // ================================================================== step 4: summary
        void BuildSummary()
        {
            var db = Db;
            var cls = db.Class(look.cls);
            var race = db.Race(look.race);

            string who = $"{race.displayName} · {cls.displayName}";
            var name = kit.Text(stage, "Who", who, 34, MenuKit.Cream, TextAlignmentOptions.MidlineLeft, Left + 14f, 170f, 1000f, 50f, true);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            kit.Text(stage, "WhoEnglish", $"{race.englishName} {cls.englishName}", 17, MenuKit.Muted, TextAlignmentOptions.MidlineLeft,
                     Left + 14f + name.GetPreferredValues(who).x + 18f, 176f, 400f, 44f);

            // the six scores
            kit.Section(stage, "Chỉ số nhân vật", Left, 250f, LeftW);
            var order = CoreStats.SheetOrder;
            for (int i = 0; i < order.Length; i++)
            {
                var a = order[i];
                int score = cls.ArrayScore(a) + race.Bonus(a);
                float bw = 388f, bh = 80f, bx = Left + 14f + (i % 3) * 404f, by = 276f + (i / 3) * 92f;
                var box = MenuKit.Box(stage, "Score_" + CoreStats.Short(a), bx, by, bw, bh);
                MenuKit.Card(box, MenuKit.CardFill, MenuKit.CardLine);
                if (cls.Saves(a)) MenuKit.Icon(box, "star", 34f, 28f, 22f, 20f, Color.white);
                kit.Text(box, "Name", CoreStats.Name(a), 16, MenuKit.Muted, TextAlignmentOptions.Center, 0f, 8f, bw, 24f);
                kit.Text(box, "Score", score.ToString(), 32, MenuKit.GoldText, TextAlignmentOptions.MidlineRight, 0f, 32f, bw / 2f + 6f, 42f, true);
                kit.Text(box, "Mod", $"({CoreStats.ModifierText(score)})", 19, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, bw / 2f + 18f, 32f, 120f, 42f);
            }
            string bonusNote = race.BonusText() == "+1 mọi chỉ số"
                ? $"+1 {race.displayName} đã được tính vào chỉ số."
                : $"{race.BonusText()} của {race.displayName} đã được tính vào chỉ số.";
            kit.Text(stage, "BonusNote", bonusNote, 15, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, Left + 16f, 460f, 900f, 26f);
            MenuKit.Icon(stage, "star", Left + LeftW - 72f, 473f, 18f, 16f, Color.white);
            kit.Text(stage, "SaveLegend", "Kháng", 15, MenuKit.GoldText, TextAlignmentOptions.MidlineLeft, Left + LeftW - 58f, 460f, 80f, 26f, true);

            // the skills: Q E A D on the left, W R S on the right
            kit.Section(stage, "Kỹ năng", Left, 506f, LeftW);
            for (int i = 0; i < 7; i++)
            {
                int col = i % 2, row = i / 2;
                float cx = Left + 36f + col * 600f, cy = 552f + row * 52f;
                var ab = Skill(cls, i, look.weapon);
                kit.KeyCap(stage, Keys[i], cx, cy, 40f);
                if (ab != null && ab.icon != null) MenuKit.Icon(stage, ab.icon, cx + 50f, cy, 40f, 40f);
                kit.Text(stage, "Skill", ab != null ? ab.displayName : "(sắp có)", 19, ab != null ? MenuKit.Cream : MenuKit.Faint,
                         TextAlignmentOptions.MidlineLeft, cx + 84f, cy - 18f, 480f, 36f, true);
            }

            // the people's traits
            kit.Section(stage, "Đặc tính chủng tộc", Left, 762f, LeftW);
            var traits = Traits(race);
            for (int i = 0; i < traits.Count && i < 4; i++)
            {
                float tx = Left + 14f + (i % 2) * 612f, ty = 786f + (i / 2) * 76f;
                MenuKit.Icon(stage, "sparkle", tx + 16f, ty + 18f, 27f, 27f, Color.white);
                kit.Text(stage, "Trait", traits[i].name, 19, MenuKit.Cream, TextAlignmentOptions.MidlineLeft, tx + 42f, ty + 2f, 560f, 30f, true);
                if (traits[i].desc.Length > 0)
                    kit.Text(stage, "TraitDesc", traits[i].desc, 15, MenuKit.Muted, TextAlignmentOptions.TopLeft, tx + 42f, ty + 32f, 560f, 44f);
            }
        }

        // ================================================================== preview
        static readonly string[] PoseClips = { "walk", "idle", "attack", "cast" };
        static readonly string[] DirNames = { "down", "side", "up", "side" };

        void BuildPreview()
        {
            var db = Db;
            var cls = db.Class(look.cls);
            var race = db.Race(look.race);
            var pv = MenuKit.Box(stage, "Preview", PvX, PvY, PvW, PvH);
            MenuKit.GoldFrame(pv, MenuKit.WithAlpha(MenuKit.Panel, 0.98f));

            string small = step == 0 ? race.displayName.ToUpperInvariant() : step == 2 ? "XEM TRƯỚC" : "NHÂN VẬT CỦA BẠN";
            var s = kit.Text(pv, "Small", small, 14, MenuKit.C("#b8964e"), TextAlignmentOptions.Center, 0f, 26f, PvW, 22f, true);
            s.characterSpacing = 3f;
            MenuKit.Fit(kit.Text(pv, "Name", step == 0 ? cls.displayName : race.displayName, 36, MenuKit.GoldText, TextAlignmentOptions.Center,
                                 24f, 46f, PvW - 48f, 50f, true), 22);
            if (step > 0) kit.Text(pv, "Sub", cls.displayName, 19, MenuKit.Muted, TextAlignmentOptions.Center, 0f, 94f, PvW, 28f);

            // the hero on its pedestal
            if (step == 0) MenuKit.Art(MenuKit.Box(pv, "Dungeon", 60f, 92f, 432f, 304f), "dungeon", Color.white);
            else MenuKit.Art(MenuKit.Box(pv, "Glow", 56f, 110f, 440f, 320f), "glow", new Color(0.25f, 0.55f, 0.45f, 0.28f));
            MenuKit.Art(MenuKit.Box(pv, "Pedestal", 186f, 290f, 180f, 81f), "pedestal", Color.white);
            previewImage = MenuKit.Img(MenuKit.CenterPivot(MenuKit.Box(pv, "Hero", 180f, 141f, 192f, 192f)), HeroArt.Portrait(look), Color.white);
            previewImage.preserveAspect = true;
            MenuKit.ArrowButton(pv, "Left", false, 114f, 384f, 52f, 46f, () => { previewDir = (previewDir + 3) % 4; });
            kit.DarkButton(pv, "Pose", "Đổi dáng", 184f, 384f, 184f, 46f, 17, () => { previewPose = (previewPose + 1) % 4; previewT = 0f; });
            MenuKit.ArrowButton(pv, "Right", true, 386f, 384f, 52f, 46f, () => { previewDir = (previewDir + 1) % 4; });

            switch (step)
            {
                case 0:
                    kit.Text(pv, "Desc", cls.description, 16, MenuKit.C("#b9c0bb"), TextAlignmentOptions.Top, 40f, 446f, PvW - 80f, 66f);
                    MenuKit.Divider(pv, 40f, 526f, PvW - 80f);
                    Scores(pv, cls, race, 546f);
                    MenuKit.Divider(pv, 40f, 664f, PvW - 80f, false);
                    Vitals(pv, cls, race, 696f);
                    WeaponLine(pv, 740f);
                    break;
                case 1:
                    MenuKit.Divider(pv, 40f, 446f, PvW - 80f, false);
                    kit.Text(pv, "EdgeLabel", "Lợi thế chủng tộc", 15, MenuKit.Muted, TextAlignmentOptions.Center, 0f, 456f, PvW, 24f);
                    var bonus = BonusLines(race);
                    MenuKit.Fit(kit.Text(pv, "Edge", string.Join("  ·  ", bonus), 22, MenuKit.Cream, TextAlignmentOptions.Center, 30f, 480f, PvW - 60f, 34f, true), 14);
                    var traits = Traits(race);
                    if (traits.Count > 0)
                        kit.Text(pv, "Trait", traits[0].desc.Length > 0 ? $"<b>{traits[0].name}</b>: {traits[0].desc}" : traits[0].name, 15, MenuKit.Muted,
                                 TextAlignmentOptions.Top, 40f, 516f, PvW - 80f, 44f);
                    MenuKit.Divider(pv, 40f, 572f, PvW - 80f);
                    Scores(pv, cls, race, 588f);
                    MenuKit.Divider(pv, 40f, 704f, PvW - 80f, false);
                    Vitals(pv, cls, race, 728f);
                    WeaponLine(pv, 764f);
                    break;
                case 2:
                    MenuKit.Divider(pv, 40f, 446f, PvW - 80f, false);
                    MenuKit.Icon(pv, "diamond", PvW / 2f - 92f, 472f, 8f, 8f, MenuKit.Gold);
                    MenuKit.Icon(pv, "diamond", PvW / 2f + 92f, 472f, 8f, 8f, MenuKit.Gold);
                    kit.Text(pv, "ScoresLabel", "Chỉ số nhân vật", 16, MenuKit.GoldText, TextAlignmentOptions.Center, 0f, 460f, PvW, 24f, true);
                    Scores(pv, cls, race, 494f);
                    MenuKit.Divider(pv, 40f, 612f, PvW - 80f, false);
                    Vitals(pv, cls, race, 646f);
                    WeaponLine(pv, 690f);
                    break;
                default:
                    MenuKit.Divider(pv, 40f, 446f, PvW - 80f);
                    Numbers(cls, race, out float hp, out float energy);
                    var w = WeaponKinds.Get(look.weapon);
                    TableRow(pv, 0, "heart", "Máu", hp.ToString("0"), $"d{cls.hitDie}");
                    TableRow(pv, 1, "drop", "Năng lượng", energy.ToString("0"), null);
                    TableRow(pv, 2, null, "Vũ khí", w != null ? w.name : look.weapon, null);
                    break;
            }
        }

        /// <summary>A hero's health and energy at level 1.</summary>
        static void Numbers(ClassDef cls, RaceDef race, out float hp, out float energy)
        {
            var c = ProgressionConfig.Current;
            float con = cls.ArrayScore(CoreStat.Constitution) + race.Bonus(CoreStat.Constitution);
            float cast = cls.ArrayScore(cls.casting) + race.Bonus(cls.casting);
            hp = Mathf.Round(c.MaxHp(1, cls.HitDieAverage, con) + race.hpPerLevel);
            energy = Mathf.Round(c.MaxEnergy(1, cast));
        }

        static string StatIcon(CoreStat a)
        {
            switch (a)
            {
                case CoreStat.Strength: return "fist";
                case CoreStat.Dexterity: return "hourglass";
                case CoreStat.Constitution: return "heart_gold";
                case CoreStat.Intelligence: return "book";
                case CoreStat.Wisdom: return "sparkle";
                default: return "crown";
            }
        }

        /// <summary>The six scores in two columns: STR CON WIS, then DEX INT CHA (a gold star: a saving throw of the class).</summary>
        void Scores(RectTransform pv, ClassDef cls, RaceDef race, float y)
        {
            CoreStat[] leftCol = { CoreStat.Strength, CoreStat.Constitution, CoreStat.Wisdom };
            CoreStat[] rightCol = { CoreStat.Dexterity, CoreStat.Intelligence, CoreStat.Charisma };
            for (int col = 0; col < 2; col++)
                for (int r = 0; r < 3; r++)
                {
                    var a = col == 0 ? leftCol[r] : rightCol[r];
                    int score = cls.ArrayScore(a) + race.Bonus(a);
                    float ox = 44f + col * 248f, cy = y + 16f + r * 34f;
                    MenuKit.Icon(pv, StatIcon(a), ox + 10f, cy, 22f, 22f, Color.white);
                    kit.Text(pv, "Stat", CoreStats.Short(a), 17, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, ox + 32f, cy - 15f, 54f, 30f, true);
                    kit.Text(pv, "Score", score.ToString(), 19, MenuKit.Cream, TextAlignmentOptions.MidlineRight, ox + 84f, cy - 15f, 36f, 30f, true);
                    kit.Text(pv, "Mod", $"({CoreStats.ModifierText(score)})", 16, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, ox + 128f, cy - 15f, 60f, 30f);
                    if (cls.Saves(a)) MenuKit.Icon(pv, "star", ox + 196f, cy, 16f, 15f, Color.white);
                }
            MenuKit.Line(pv, PvW / 2f, y + 2f, 1f, 98f, MenuKit.C("#2a3834"));
        }

        /// <summary>Health, energy and the hit die in one row.</summary>
        void Vitals(RectTransform pv, ClassDef cls, RaceDef race, float cy)
        {
            Numbers(cls, race, out float hp, out float energy);
            string cream = MenuKit.Hex(MenuKit.Cream);
            MenuKit.Icon(pv, "heart", 56f, cy, 22f, 20f, Color.white);
            kit.Text(pv, "Hp", $"Máu <color={cream}><b>{hp:0}</b></color>", 17, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, 74f, cy - 15f, 150f, 30f);
            MenuKit.Icon(pv, "drop", 222f, cy, 16f, 22f, Color.white);
            kit.Text(pv, "Energy", $"Năng lượng <color={cream}><b>{energy:0}</b></color>", 17, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, 238f, cy - 15f, 190f, 30f);
            MenuKit.Icon(pv, "die", 424f, cy, 22f, 22f, Color.white);
            kit.Text(pv, "Die", $"d{cls.hitDie}", 17, MenuKit.Cream, TextAlignmentOptions.MidlineLeft, 442f, cy - 15f, 70f, 30f, true);
        }

        /// <summary>The weapon, centred, with its picture.</summary>
        void WeaponLine(RectTransform pv, float cy)
        {
            var w = WeaponKinds.Get(look.weapon);
            string text = $"Vũ khí: <color={MenuKit.Hex(MenuKit.Cream)}>{(w != null ? w.name : look.weapon)}</color>";
            var t = kit.Text(pv, "Weapon", text, 17, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, 0f, cy - 15f, 400f, 30f);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            float tw = t.GetPreferredValues(text).x;
            float x = (PvW - tw - 34f) / 2f;
            t.rectTransform.anchoredPosition = new Vector2(x + 34f, -(cy - 15f));
            MenuKit.Icon(pv, HeroArt.WeaponIcon(look), x + 12f, cy, 26f, 26f);
        }

        /// <summary>A row of the summary's table in the preview: an icon, a name, the value, a note on the right.</summary>
        void TableRow(RectTransform pv, int i, string icon, string label, string value, string note)
        {
            float top = 468f + i * 64f, cy = top + 30f;
            if (icon != null) MenuKit.Icon(pv, icon, 62f, cy, 24f, 24f, Color.white);
            else MenuKit.Icon(pv, HeroArt.WeaponIcon(look), 62f, cy, 30f, 30f);
            kit.Text(pv, "Label", label, 18, MenuKit.Muted, TextAlignmentOptions.MidlineLeft, 92f, top, 170f, 60f);
            MenuKit.Fit(kit.Text(pv, "Value", value, 21, MenuKit.Cream, TextAlignmentOptions.MidlineLeft, 262f, top, 170f, 60f, true), 14);
            if (note != null)
            {
                MenuKit.Icon(pv, "die", 424f, cy, 22f, 22f, Color.white);
                kit.Text(pv, "Note", note, 18, MenuKit.Cream, TextAlignmentOptions.MidlineLeft, 442f, top, 80f, 60f, true);
            }
            MenuKit.Line(pv, 40f, top + 62f, PvW - 80f, 1f, MenuKit.C("#22302c"));
        }

        void AnimatePreview()
        {
            if (previewImage == null || !look.HasClass) return;
            var set = HeroArt.SetFor(look);
            if (set == null) return;
            string dir = DirNames[previewDir];
            var clip = set.Get(PoseClips[previewPose] + "_" + dir);
            if (clip == null || clip.frames.Length == 0) return;
            previewT += Time.unscaledDeltaTime;
            int f = Mathf.FloorToInt(previewT * Mathf.Max(1f, clip.fps)) % clip.frames.Length;
            // facing left: the side clips mirrored
            var flip = new Vector3(previewDir == 3 ? -1f : 1f, 1f, 1f);
            previewImage.sprite = clip.frames[f];
            previewImage.rectTransform.localScale = flip;
            if (detailsImage != null)
            {
                detailsImage.sprite = clip.frames[f];
                detailsImage.rectTransform.localScale = flip;
            }
        }
    }
}

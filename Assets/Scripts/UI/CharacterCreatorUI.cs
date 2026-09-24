using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// The character creator (Tạo Nhân Vật), after D&D Beyond's: choose a class from its cards
    /// (filtered by how hard it is to play), then a people, then the looks and the weapon, and see
    /// the hero walk, swing and cast on the right with the ability scores the choice makes (the
    /// standard array placed by the class, the people's increases, D&D modifiers and saving
    /// throws). It opens by itself for a hero with no class yet — a new one, or an older one after
    /// classes came — and from the character sheet to change the looks later (people and class
    /// stay). Offline the choice is applied at once, online the server is asked
    /// (<see cref="CharacterChoice"/>). Built at run time from the HUD's font and sprites.
    /// </summary>
    public class CharacterCreatorUI : UIPanel
    {
        public TMP_FontAsset font;
        public Material fontOutline;
        public Sprite windowSprite, buttonSprite, dividerSprite, whiteSprite, slotSprite;

        public enum Mode { New, Looks }

        public static CharacterCreatorUI I { get; private set; }
        /// <summary>Automated runs (tests, tours, bots) keep a hero with no class instead of opening the creator.</summary>
        public static bool Suppressed;

        static readonly Color Gold = new Color(1f, 0.86f, 0.45f);
        static readonly Color Cream = new Color(0.96f, 0.93f, 0.86f);
        static readonly Color Muted = new Color(0.66f, 0.62f, 0.72f);
        static readonly Color Panel = new Color(0.09f, 0.07f, 0.11f, 0.96f);
        static readonly Color Card = new Color(0.13f, 0.11f, 0.16f, 1f);

        Mode mode;
        int step;
        Complexity? filter;
        HeroLook look = new HeroLook();
        bool built, askedOnce;
        float previewT;
        int previewDir, previewPose;

        RectTransform root, content;
        readonly Button[] tabs = new Button[4];
        TextMeshProUGUI title, subtitle, previewName, previewScores, previewInfo;
        Image previewImage;
        Button backButton, nextButton;
        TextMeshProUGUI nextLabel;

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

        /// <summary>-creatorshot &lt;folder&gt;: pictures of every step of the creator (a class picked, a people, looks), then quit.</summary>
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
            AnimatePreview();
        }

        // ================================================================== layout
        void Build()
        {
            built = true;
            root = (RectTransform)window;
            if (root == null) root = (RectTransform)transform;
            var bg = Stretch(root, "Backdrop");
            Img(bg, whiteSprite, new Color(0.035f, 0.025f, 0.05f, 1f)).raycastTarget = true;

            title = Txt(root, "Title", "TẠO NHÂN VẬT", 22, Muted, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(48 + 350, -28 - 15), new Vector2(700, 30));
            subtitle = Txt(root, "Subtitle", "Chọn lớp nhân vật.", 52, Cream, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(48 + 450, -58 - 35), new Vector2(900, 70));
            subtitle.fontStyle = FontStyles.Bold;
            string[] names = { "1  Lớp", "2  Chủng Tộc", "3  Ngoại Hình", "4  Hoàn Tất" };
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                tabs[i] = Btn(root, "Tab" + i, names[i], new Vector2(0, 1), new Vector2(1000 + i * 190, -60), new Vector2(180, 52), 21, () => GoTo(k));
            }

            content = Rect(root, "Content", new Vector2(0, 1), new Vector2(48 + 670, -150 - 400), new Vector2(1340, 800));

            // the preview on the right
            var pv = Rect(root, "Preview", new Vector2(1, 1), new Vector2(-280, -150 - 400), new Vector2(480, 800));
            Frame(pv);
            previewName = Txt(pv, "Name", "", 26, Gold, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(440, 36));
            previewName.fontStyle = FontStyles.Bold;
            var imgRt = Rect(pv, "Hero", new Vector2(0.5f, 1), new Vector2(0, -220), new Vector2(288, 288));
            previewImage = Img(imgRt, null, Color.white);
            previewImage.preserveAspect = true;
            Btn(pv, "Left", "<", new Vector2(0.5f, 1), new Vector2(-150, -390), new Vector2(56, 44), 22, () => { previewDir = (previewDir + 3) % 4; });
            Btn(pv, "Pose", "Đổi dáng", new Vector2(0.5f, 1), new Vector2(0, -390), new Vector2(200, 44), 20, () => { previewPose = (previewPose + 1) % 4; previewT = 0f; });
            Btn(pv, "Right", ">", new Vector2(0.5f, 1), new Vector2(150, -390), new Vector2(56, 44), 22, () => { previewDir = (previewDir + 1) % 4; });
            previewScores = Txt(pv, "Scores", "", 20, Cream, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1), new Vector2(0, -530), new Vector2(420, 220), false);
            previewInfo = Txt(pv, "Info", "", 17, Muted, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1), new Vector2(0, -700), new Vector2(420, 130), false);

            backButton = Btn(root, "Back", "Quay lại", new Vector2(0, 0), new Vector2(160, 52), new Vector2(220, 58), 22, () => GoTo(step - 1));
            nextButton = Btn(root, "Next", "Tiếp tục", new Vector2(1, 0), new Vector2(-280, 52), new Vector2(420, 64), 26, Next);
            nextLabel = nextButton.GetComponentInChildren<TextMeshProUGUI>();
            nextLabel.color = Gold;
        }

        void GoTo(int s)
        {
            if (mode == Mode.Looks && s < 2) return;
            step = Mathf.Clamp(s, 0, 3);
            AudioManager.Play("sfx_ui_click", 0.6f);
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
            for (int i = 0; i < 4; i++)
            {
                bool locked = mode == Mode.Looks && i < 2;
                tabs[i].interactable = !locked;
                var t = tabs[i].GetComponentInChildren<TextMeshProUGUI>();
                t.color = i == step ? Gold : locked ? new Color(0.4f, 0.37f, 0.44f) : Cream;
            }
            backButton.gameObject.SetActive(step > (mode == Mode.Looks ? 2 : 0));
            nextLabel.text = step < 3 ? "Tiếp tục" : mode == Mode.New ? "Bắt đầu phiêu lưu" : "Lưu ngoại hình";
            title.text = mode == Mode.New ? "TẠO NHÂN VẬT" : "ĐỔI NGOẠI HÌNH";
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            switch (step)
            {
                case 0: subtitle.text = "Chọn lớp nhân vật."; BuildClasses(); break;
                case 1: subtitle.text = "Chọn chủng tộc."; BuildRaces(); break;
                case 2: subtitle.text = "Ngoại hình và vũ khí."; BuildLooks(); break;
                default: subtitle.text = "Sẵn sàng lên đường?"; BuildSummary(); break;
            }
            RefreshPreview();
        }

        // ================================================================== step 1: classes
        void BuildClasses()
        {
            var db = Db;
            string[] fnames = { "TẤT CẢ", "DỄ", "VỪA", "KHÓ" };
            Txt(content, "FilterLabel", "ĐỘ KHÓ:", 18, Muted, TextAlignmentOptions.MidlineLeft, new Vector2(0, 1), new Vector2(60, -22), new Vector2(120, 30));
            for (int i = 0; i < 4; i++)
            {
                Complexity? f = i == 0 ? (Complexity?)null : (Complexity)(i - 1);
                var b = Btn(content, "Filter" + i, fnames[i], new Vector2(0, 1), new Vector2(180 + i * 120, -22), new Vector2(110, 38), 17, () => { filter = f; Refresh(); });
                b.GetComponentInChildren<TextMeshProUGUI>().color = filter == f ? Gold : Cream;
            }
            int shown = 0;
            foreach (var cls in db.classes)
            {
                if (cls == null || (filter.HasValue && cls.complexity != filter.Value)) continue;
                int col = shown % 4, row = shown / 4;
                ClassCard(cls, new Vector2(165 + col * 336, -150 - row * 258));
                shown++;
            }
        }

        void ClassCard(ClassDef cls, Vector2 pos)
        {
            bool chosen = look.cls == cls.id;
            var card = Rect(content, "Class_" + cls.id, new Vector2(0, 1), pos, new Vector2(324, 246));
            Img(card, whiteSprite, Card).raycastTarget = true;
            var band = Rect(card, "Band", new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(316, 8));
            Img(band, whiteSprite, cls.color);
            Border(card, chosen ? Gold : new Color(cls.color.r, cls.color.g, cls.color.b, 0.6f), chosen ? 3 : 2);
            // emblem, portrait
            var em = Rect(card, "Emblem", new Vector2(0, 1), new Vector2(34, -38), new Vector2(46, 46));
            Img(em, whiteSprite, cls.color * 0.85f);
            if (cls.emblem != null) Img(Rect(em, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40)), cls.emblem, Color.white).preserveAspect = true;
            else Txt(em, "Letter", cls.displayName.Substring(0, 1), 28, Color.white, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46));
            var portraitLook = look.Clone();
            portraitLook.cls = cls.id;
            portraitLook.weapon = cls.DefaultWeapon;
            portraitLook.cloth = -1;
            var pimg = Img(Rect(card, "Portrait", new Vector2(1, 1), new Vector2(-62, -66), new Vector2(112, 112)), HeroArt.Portrait(portraitLook), Color.white);
            pimg.preserveAspect = true;
            var name = Txt(card, "Name", cls.displayName, 29, Cream, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(122, -70), new Vector2(220, 40));
            name.fontStyle = FontStyles.Bold;
            Fit(name, 18, 29);
            Txt(card, "English", cls.englishName + "  ·  d" + cls.hitDie + "  ·  " + ClassDef.ComplexityName(cls.complexity), 14, Muted, TextAlignmentOptions.TopLeft,
                new Vector2(0, 1), new Vector2(122, -100), new Vector2(220, 20), false);
            Pill(card, cls.role, cls.color, new Vector2(12, -128));
            Pill(card, cls.PrimaryText(), cls.color, new Vector2(12, -156));
            var desc = Txt(card, "Desc", cls.description, 14, new Color(0.84f, 0.8f, 0.86f), TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(162, -193), new Vector2(300, 38), false);
            desc.overflowMode = TextOverflowModes.Ellipsis;
            Btn(card, "More", "XEM THÊM", new Vector2(1, 0), new Vector2(-186, 22), new Vector2(118, 32), 14, () => Details(cls));
            var pick = Btn(card, "Pick", chosen ? "ĐÃ CHỌN" : "CHỌN", new Vector2(1, 0), new Vector2(-62, 22), new Vector2(110, 32), 15, () =>
            {
                look.cls = cls.id;
                if (!cls.Allows(look.weapon)) look.weapon = cls.DefaultWeapon;
                look.cloth = -1;
                Refresh();
            });
            pick.GetComponentInChildren<TextMeshProUGUI>().color = chosen ? Gold : Cream;
        }

        void Pill(RectTransform parent, string text, Color c, Vector2 pos)
        {
            var t = Txt(parent, "Pill", text, 13, Cream, TextAlignmentOptions.Center, new Vector2(0, 1), Vector2.zero, new Vector2(10, 22), false);
            t.fontStyle = FontStyles.Bold;
            float w = t.GetPreferredValues(text, 400, 22).x + 20;
            var rt = t.rectTransform;
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(w, 22);
            var back = Rect(parent, "PillBack", new Vector2(0, 1), Vector2.zero, new Vector2(w, 22));
            back.pivot = new Vector2(0, 0.5f);
            back.anchoredPosition = pos;
            back.SetSiblingIndex(rt.GetSiblingIndex());
            Img(back, whiteSprite, new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 0.9f));
            Border(back, c, 1);
        }

        void Details(ClassDef cls)
        {
            var dim = Stretch(content, "Details");
            Img(dim, whiteSprite, new Color(0, 0, 0, 0.7f)).raycastTarget = true;
            var w = Rect(dim, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 680));
            Frame(w);
            var t = Txt(w, "Title", cls.displayName + "  <size=20><color=#9a93a8>" + cls.englishName + "</color></size>", 38, Gold, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(760, 50));
            t.fontStyle = FontStyles.Bold;
            var sb = new StringBuilder();
            sb.Append(cls.description).Append("\n\n");
            sb.Append($"<color=#ffe07a>Xúc xắc máu</color>  d{cls.hitDie}   ·   <color=#ffe07a>Giáp</color>  {ArmorName(cls.armor)}   ·   <color=#ffe07a>Độ khó</color>  {ClassDef.ComplexityName(cls.complexity)}\n");
            sb.Append($"<color=#ffe07a>Chỉ số chính</color>  {Join(cls.primary)}   ·   <color=#ffe07a>Phép dùng</color>  {CoreStats.Name(cls.casting)}\n");
            sb.Append($"<color=#ffe07a>Kháng (saving throw)</color>  {Join(cls.savingThrows)}\n");
            sb.Append("<color=#ffe07a>Vũ khí</color>  ");
            for (int i = 0; i < cls.weapons.Length; i++)
            {
                var wk = WeaponKinds.Get(cls.weapons[i]);
                sb.Append(i > 0 ? ", " : "").Append(wk != null ? wk.name : cls.weapons[i]);
            }
            sb.Append("\n\n<color=#ffe07a>Kỹ năng</color>\n");
            string[] keys = { "Q", "W", "E", "R", "A", "S", "D" };
            var weapon = WeaponKinds.Get(cls.DefaultWeapon);
            for (int i = 0; i < 7; i++)
            {
                string id = cls.kit != null && i < cls.kit.Length ? cls.kit[i] : "";
                if (i == 0 && weapon != null && !weapon.focus) id = weapon.basic;
                var a = Db.Ability(id);
                sb.Append($"<color=#86c4f5>{keys[i]}</color>  ").Append(a != null ? $"<b>{a.displayName}</b> — {FirstLine(a.description)}" : "<color=#7a7384>(sắp có)</color>").Append('\n');
            }
            Txt(w, "Body", sb.ToString(), 18, Cream, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1), new Vector2(0, -330), new Vector2(740, 520), false);
            Btn(w, "Close", "Đóng", new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(200, 50), 22, () => Destroy(dim.gameObject));
        }

        static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int i = s.IndexOf('.');
            return i > 0 && i < 110 ? s.Substring(0, i + 1) : s.Length > 110 ? s.Substring(0, 110) + "…" : s;
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
            var db = Db;
            int i = 0;
            foreach (var race in db.races)
            {
                if (race == null) continue;
                int col = i % 3, row = i / 3;
                RaceCard(race, new Vector2(218 + col * 452, -128 - row * 262));
                i++;
            }
        }

        void RaceCard(RaceDef race, Vector2 pos)
        {
            bool chosen = look.race == race.id;
            var card = Rect(content, "Race_" + race.id, new Vector2(0, 1), pos, new Vector2(436, 250));
            Img(card, whiteSprite, Card).raycastTarget = true;
            Border(card, chosen ? Gold : new Color(0.35f, 0.3f, 0.4f), chosen ? 3 : 1);
            var portraitLook = look.Clone();
            portraitLook.race = race.id;
            portraitLook.skin = 0;
            portraitLook.beard = race.beards ? 2 : 0;
            Img(Rect(card, "Portrait", new Vector2(0, 1), new Vector2(66, -76), new Vector2(112, 112)), HeroArt.Portrait(portraitLook), Color.white).preserveAspect = true;
            var name = Txt(card, "Name", race.displayName, 28, Cream, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(262, -34), new Vector2(290, 38));
            name.fontStyle = FontStyles.Bold;
            Txt(card, "English", race.englishName, 15, Muted, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(262, -64), new Vector2(290, 22), false);
            Fit(Txt(card, "Bonus", race.BonusText(), 17, Gold, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(262, -92), new Vector2(290, 26), false), 12, 17);
            var rd = Txt(card, "Desc", race.description, 14, new Color(0.84f, 0.8f, 0.86f), TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(262, -134), new Vector2(290, 52), false);
            rd.overflowMode = TextOverflowModes.Ellipsis;
            Txt(card, "Traits", race.traits, 13, Muted, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(218, -196), new Vector2(410, 70), false);
            var pick = Btn(card, "Pick", chosen ? "ĐÃ CHỌN" : "CHỌN", new Vector2(1, 0), new Vector2(-62, 20), new Vector2(110, 32), 15, () =>
            {
                look.race = race.id;
                look.skin = 0;
                look.beard = race.beards ? 2 : 0;
                if (race.dragonHead) look.hairColor = 6;
                Refresh();
            });
            pick.GetComponentInChildren<TextMeshProUGUI>().color = chosen ? Gold : Cream;
        }

        // ================================================================== step 3: looks
        void BuildLooks()
        {
            var db = Db;
            var race = db.Race(look.race);
            var cls = db.Class(look.cls);
            bool dragon = race != null && race.dragonHead;
            var rows = new List<(string label, Func<string> value, Action<int> step, Func<Color?> swatch)>();
            int skins = race != null && race.skins != null ? race.skins.Length : 1;
            rows.Add((dragon ? "Màu vảy" : "Màu da",
                () => dragon ? HeroLook.DraconicNames[Mathf.Clamp(look.skin, 0, HeroLook.DraconicNames.Length - 1)] + " · kháng " + TypeName(look.DraconicElement) : $"Tông {look.skin + 1}/{skins}",
                d => look.skin = Wrap(look.skin + d, skins),
                () => race != null && race.skins.Length > 0 ? race.skins[Mathf.Clamp(look.skin, 0, race.skins.Length - 1)] : (Color?)null));
            if (!dragon)
            {
                rows.Add(("Kiểu tóc", () => HeroLook.HairStyles[look.hair], d => look.hair = Wrap(look.hair + d, HeroLook.HairStyles.Length), () => null));
                rows.Add(("Màu tóc", () => HeroLook.HairColors[look.hairColor].name, d => look.hairColor = Wrap(look.hairColor + d, HeroLook.HairColors.Length),
                    () => HeroLook.HairColors[look.hairColor].color));
                rows.Add(("Râu", () => HeroLook.Beards[look.beard], d => look.beard = Wrap(look.beard + d, HeroLook.Beards.Length), () => null));
            }
            else
                rows.Add(("Màu mào", () => HeroLook.HairColors[look.hairColor].name, d => look.hairColor = Wrap(look.hairColor + d, HeroLook.HairColors.Length),
                    () => HeroLook.HairColors[look.hairColor].color));
            rows.Add(("Màu mắt", () => HeroLook.EyeColors[look.eyes].name, d => look.eyes = Wrap(look.eyes + d, HeroLook.EyeColors.Length), () => HeroLook.EyeColors[look.eyes].color));
            rows.Add(("Màu trang phục", () => look.cloth < 0 ? "Mặc định của lớp" : HeroLook.Cloths[look.cloth].name,
                d => look.cloth = Wrap(look.cloth + 1 + d, HeroLook.Cloths.Length + 1) - 1,
                () => look.cloth < 0 ? (cls != null ? cls.cloth : (Color?)null) : HeroLook.Cloths[look.cloth].color));
            if (cls != null && (cls.id == "wizard" || cls.id == "ranger" || cls.id == "rogue" || cls.id == "warlock"))
                rows.Add((cls.id == "wizard" ? "Mũ phù thủy" : "Mũ trùm", () => look.bareHead ? "Bỏ" : "Đội", d => look.bareHead = !look.bareHead, () => null));
            if (cls != null)
            {
                var weapons = cls.weapons;
                rows.Add(("Vũ khí", () => { var w = WeaponKinds.Get(look.weapon); return w != null ? w.name : look.weapon; },
                    d =>
                    {
                        int i = Array.IndexOf(weapons, look.weapon);
                        look.weapon = weapons[Wrap((i < 0 ? 0 : i) + d, weapons.Length)];
                    }, () => null));
            }
            var metals = CharacterChoice.StarterMetals;
            rows.Add(("Kim loại vũ khí", () => HeroLook.Metals[look.metal].name,
                d =>
                {
                    int i = Array.IndexOf(metals, look.metal);
                    look.metal = metals[Wrap((i < 0 ? 0 : i) + d, metals.Length)];
                }, () => HeroLook.Metals[look.metal].color));

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                int col = i % 2, row = i / 2;
                OptionRow(r.label, r.value, r.step, r.swatch, new Vector2(335 + col * 670, -40 - row * 88));
            }
            var weaponKind = WeaponKinds.Get(look.weapon);
            if (weaponKind != null)
                Txt(content, "WeaponHint", $"<color=#ffe07a>{weaponKind.name}</color>: {weaponKind.description}", 17, Cream, TextAlignmentOptions.TopLeft,
                    new Vector2(0, 1), new Vector2(670, -40 - ((rows.Count + 1) / 2) * 88 - 10), new Vector2(1260, 50), false);
            Btn(content, "Random", "Ngẫu nhiên", new Vector2(0, 0), new Vector2(160, 40), new Vector2(260, 50), 20, () =>
            {
                var rnd = new System.Random();
                look.skin = rnd.Next(skins);
                look.hair = rnd.Next(HeroLook.HairStyles.Length);
                look.hairColor = rnd.Next(HeroLook.HairColors.Length);
                look.beard = race != null && race.beards ? rnd.Next(1, 3) : rnd.Next(4) == 0 ? 1 : 0;
                look.eyes = rnd.Next(HeroLook.EyeColors.Length);
                look.cloth = rnd.Next(-1, HeroLook.Cloths.Length);
                look.metal = metals[rnd.Next(metals.Length)];
                Refresh();
            });
        }

        void OptionRow(string label, Func<string> value, Action<int> step, Func<Color?> swatch, Vector2 pos)
        {
            var row = Rect(content, "Opt_" + label, new Vector2(0, 1), pos, new Vector2(640, 76));
            Img(row, whiteSprite, Card);
            Txt(row, "Label", label, 20, Muted, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f), new Vector2(110, 0), new Vector2(200, 40), false);
            var val = Txt(row, "Value", value(), 22, Cream, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(90, 0), new Vector2(250, 40));
            Fit(val, 14, 22);
            var sw = swatch();
            if (sw.HasValue)
            {
                var s = Rect(row, "Swatch", new Vector2(0.5f, 0.5f), new Vector2(-70, 0), new Vector2(30, 30));
                Img(s, whiteSprite, sw.Value);
                Border(s, new Color(0, 0, 0, 0.8f), 2);
            }
            Btn(row, "Prev", "<", new Vector2(1, 0.5f), new Vector2(-104, 0), new Vector2(48, 44), 20, () => { step(-1); Refresh(); });
            Btn(row, "Next", ">", new Vector2(1, 0.5f), new Vector2(-42, 0), new Vector2(48, 44), 20, () => { step(1); Refresh(); });
        }

        static int Wrap(int i, int n) => n <= 0 ? 0 : ((i % n) + n) % n;

        /// <summary>One line that shrinks to fit its box.</summary>
        static TextMeshProUGUI Fit(TextMeshProUGUI t, float min, float max)
        {
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
            return t;
        }

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
            if (cls == null || race == null) return;
            var sb = new StringBuilder();
            sb.Append($"<size=34><b>{race.displayName} {cls.displayName}</b></size>   <color=#9a93a8>{race.englishName} {cls.englishName}</color>\n\n");
            sb.Append("<color=#ffe07a>Chỉ số (mảng chuẩn D&D 15 14 13 12 10 8 theo lớp, cộng chủng tộc)</color>\n");
            foreach (var a in CoreStats.SheetOrder)
            {
                int score = cls.ArrayScore(a) + race.Bonus(a);
                string star = cls.Saves(a) ? "  <color=#ffe07a>★ kháng</color>" : "";
                string bonus = race.Bonus(a) != 0 ? $"  <color=#86c4f5>(+{race.Bonus(a)} {race.displayName})</color>" : "";
                sb.Append($"   {CoreStats.Name(a),-11} <b>{score}</b>  ({CoreStats.ModifierText(score)}){bonus}{star}\n");
            }
            sb.Append("\n<color=#ffe07a>Kỹ năng</color>\n");
            string[] keys = { "Q", "W", "E", "R", "A", "S", "D" };
            var weapon = WeaponKinds.Get(look.weapon);
            for (int i = 0; i < 7; i++)
            {
                string id = cls.kit != null && i < cls.kit.Length ? cls.kit[i] : "";
                if (i == 0 && weapon != null && !weapon.focus) id = weapon.basic;
                var ab = db.Ability(id);
                sb.Append($"   <color=#86c4f5>{keys[i]}</color> {(ab != null ? ab.displayName : "(sắp có)")}");
                if (i % 2 == 1 || i == 6) sb.Append('\n');
                else sb.Append("      ");
            }
            sb.Append("\n<color=#ffe07a>Đặc tính chủng tộc</color>\n").Append(race.traits);
            Txt(content, "Summary", sb.ToString(), 20, Cream, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(660, -380), new Vector2(1260, 760), false);
            if (mode == Mode.New)
                Txt(content, "Note", "Lớp và chủng tộc chọn một lần; ngoại hình và vũ khí đổi được sau ở bảng Nhân Vật (C).", 17, Muted,
                    TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(1200, 30), false);
        }

        // ================================================================== preview
        static readonly string[] PoseClips = { "walk", "idle", "attack", "cast" };
        static readonly string[] DirNames = { "down", "side", "up", "side" };

        void RefreshPreview()
        {
            var db = Db;
            var cls = db.Class(look.cls);
            var race = db.Race(look.race);
            if (cls == null || race == null)
            {
                previewName.text = "";
                previewScores.text = "";
                previewInfo.text = "";
                return;
            }
            previewName.text = $"{race.displayName} · {cls.displayName}";
            var c = ProgressionConfig.Current;
            var sb = new StringBuilder();
            foreach (var a in CoreStats.SheetOrder)
            {
                int score = cls.ArrayScore(a) + race.Bonus(a);
                string star = cls.Saves(a) ? "<color=#ffe07a>★</color>" : " ";
                sb.Append($"{star} {CoreStats.Short(a)}  <b>{score}</b>  ({CoreStats.ModifierText(score)})");
                sb.Append(a == CoreStat.Dexterity || a == CoreStat.Intelligence || a == CoreStat.Charisma ? "\n" : "<pos=50%>");
            }
            previewScores.text = sb.ToString();
            float con = cls.ArrayScore(CoreStat.Constitution) + race.Bonus(CoreStat.Constitution);
            float cast = cls.ArrayScore(cls.casting) + race.Bonus(cls.casting);
            float hp = Mathf.Round(c.MaxHp(1, cls.HitDieAverage, con) + race.hpPerLevel);
            float energy = Mathf.Round(c.MaxEnergy(1, cast));
            var w = WeaponKinds.Get(look.weapon);
            previewInfo.text = $"Máu <b>{hp}</b>  ·  Năng lượng <b>{energy}</b>  ·  d{cls.hitDie}\n" +
                               $"Vũ khí: {(w != null ? w.name : "")}\n{cls.role}";
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
            previewImage.sprite = clip.frames[f];
            // facing left: the side clips mirrored
            previewImage.rectTransform.localScale = new Vector3(previewDir == 3 ? -1f : 1f, 1f, 1f);
        }

        // ================================================================== primitives (as TitleScreen's)
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

        void Frame(RectTransform window)
        {
            var back = Stretch(window, "Backing");
            back.offsetMin = new Vector2(8, 8);
            back.offsetMax = new Vector2(-8, -8);
            Img(back, whiteSprite, Panel);
            if (windowSprite != null) Img(Stretch(window, "Frame"), windowSprite, Color.white, Image.Type.Sliced);
        }

        void Border(RectTransform rt, Color c, float width)
        {
            void Edge(string n, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
            {
                var e = new GameObject(n, typeof(RectTransform));
                var t = (RectTransform)e.transform;
                t.SetParent(rt, false);
                t.anchorMin = min;
                t.anchorMax = max;
                t.offsetMin = offMin;
                t.offsetMax = offMax;
                Img(t, whiteSprite, c);
            }
            Edge("Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -width), Vector2.zero);
            Edge("Bottom", new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, width));
            Edge("Left", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(width, 0));
            Edge("Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-width, 0), Vector2.zero);
        }

        static Image Img(RectTransform rt, Sprite sprite, Color color, Image.Type type = Image.Type.Simple)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = type;
            img.raycastTarget = false;
            return img;
        }

        TextMeshProUGUI Txt(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align, Vector2 anchor, Vector2 pos,
                            Vector2 box, bool outline = true)
        {
            var rt = Rect(parent, name, anchor, pos, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            if (outline && fontOutline != null) t.fontSharedMaterial = fontOutline;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        Button Btn(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, Action onClick)
        {
            var rt = Rect(parent, name, anchor, pos, size);
            var img = Img(rt, buttonSprite != null ? buttonSprite : whiteSprite, buttonSprite != null ? Color.white : new Color(0.25f, 0.2f, 0.3f),
                          buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple);
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.highlightedColor = new Color(1f, 0.95f, 0.8f);
            cb.pressedColor = new Color(0.8f, 0.75f, 0.65f);
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var t = Txt(rt, "Label", label, fontSize, Cream, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 1), size);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return b;
        }
    }
}

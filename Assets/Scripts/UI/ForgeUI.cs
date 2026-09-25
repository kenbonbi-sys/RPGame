using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// Lò Rèn (the smith's window, <see cref="Forge"/>), laid out like a crafting screen: four
    /// tabs on top (Nâng Cấp, Kim Loại, Vũ Khí, Chế Tạo) each saying what it is for; on the left
    /// the list of that tab (the ten tempering levels, the metals, the class's weapons, the gear
    /// the smith makes) with what state each is in; in the middle the pick — the hero holding
    /// the weapon as it would be, or the piece of gear — and its number now → after; on the right
    /// what it asks for, what the hero has of it and which monsters drop what is missing; and one
    /// big button at the bottom that says what it will do, or why it cannot yet. Closes when the
    /// hero walks away from the smith.
    /// </summary>
    public class ForgeUI : UIPanel
    {
        public TMP_FontAsset font;
        public Material fontOutline;
        public Sprite windowSprite, buttonSprite, whiteSprite;
        [Tooltip("Optional: the list's cells, the hover light, the floor glow (the HUD's sprites).")]
        public Sprite slotSprite, highlightSprite, glowSprite, dividerSprite;

        public static ForgeUI I { get; private set; }

        const float W = 1640f, H = 900f;
        const float ListX = 40f, ListW = 440f, MidX = 500f, MidW = 640f, ReqX = 1160f, ReqW = 440f, PanelTop = 214f;

        static readonly string[] TabNames = { "Nâng Cấp", "Kim Loại", "Vũ Khí", "Chế Tạo" };
        static readonly string[] TabHelp =
        {
            "Rèn vũ khí mạnh hơn, từ +0 tới +10: mỗi cấp +8% công, từ +7 vũ khí phát sáng. Mỗi cấp cần vàng và đồ quái rơi ở một vùng.",
            "Đổi màu kim loại của vũ khí. Kim loại quý mở khi vũ khí được rèn đủ cao.",
            "Đổi sang vũ khí khác mà lớp nhân vật dùng được. Miễn phí, giữ nguyên cấp rèn và kim loại.",
            "Chế tạo trang bị (mũ, giáp, giày, tay phụ, nhẫn) từ đồ quái rơi. Làm xong thì mặc ở bảng Nhân Vật (B).",
        };

        UiKit kit;
        RectTransform body, listContent, reqRoot;
        ScrollRect listScroll;
        Image preview, previewIcon, previewGlow;
        TextMeshProUGUI helpText, nameText, kindText, nowLabel, nowValue, nextLabel, nextValue, arrow, detailText, statusText, actionLabel, reasonText;
        Button actionButton;
        readonly Button[] tabs = new Button[4];
        readonly List<Row> rows = new List<Row>();
        readonly List<GameObject> reqRows = new List<GameObject>();
        readonly Dictionary<string, string> sources = new Dictionary<string, string>();
        int tab, pick = -1;
        float animT;
        bool built, dirty;
        PlayerController watched;
        HeroLook previewLook;
        ItemDef previewItem;

        class Row
        {
            public GameObject go;
            public Image bg, icon, swatch, frame;
            public TextMeshProUGUI name, sub, badge;
        }

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

        public void Open()
        {
            var p = Players.Local;
            if (p == null || p.stats == null) return;
            if (!p.stats.HasClass)
            {
                Notify.WorldText(p, "Hãy chọn lớp nhân vật trước (bảng Nhân Vật, C).", p.health.HeadPosition + Vector3.up * 0.4f, UiKit.Bad);
                return;
            }
            if (!built) Build();
            FindSources();
            Show();
            watched = p;
            p.stats.LookChanged += MarkDirty;
            if (p.inventory != null) p.inventory.Changed += MarkDirty;
            pick = -1;
            SetTab(tab);
        }

        public override void Close()
        {
            if (watched != null)
            {
                watched.stats.LookChanged -= MarkDirty;
                if (watched.inventory != null) watched.inventory.Changed -= MarkDirty;
                watched = null;
            }
            base.Close();
        }

        void MarkDirty() => dirty = true;

        /// <summary>Opens a tab and picks a row of it (-1: the tab's natural pick); for automated tours.</summary>
        public void DebugTab(int t, int row = -1)
        {
            SetTab(t);
            if (row >= 0) Pick(Mathf.Min(row, rows.Count - 1));
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;
            var p = Players.Local;
            if (p == null || Forge.SmithNear(p) == null)
            {
                Close();
                return;
            }
            Fit();
            if (dirty)
            {
                dirty = false;
                FillList();
                ShowPick();
            }
            Animate();
        }

        void Fit()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null || body == null) return;
            var r = ((RectTransform)canvas.transform).rect;
            float s = Mathf.Min(1f, (r.width - 24f) / W, (r.height - 16f) / H);
            body.localScale = new Vector3(s, s, 1f);
        }

        // ================================================================== layout
        static RectTransform Box(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = UiKit.Rect(parent, name, new Vector2(0, 1), Vector2.zero, new Vector2(w, h));
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            return rt;
        }

        TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align, float x, float y, float w, float h, bool outline = false)
        {
            var t = kit.Txt(parent, name, text, size, color, align, new Vector2(0, 1), Vector2.zero, new Vector2(w, h), outline);
            t.rectTransform.pivot = new Vector2(0, 1);
            t.rectTransform.anchoredPosition = new Vector2(x, -y);
            return t;
        }

        Image Panel(RectTransform rt, Color tint)
        {
            var img = UiKit.Img(rt, buttonSprite != null ? buttonSprite : whiteSprite, tint, buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple);
            return img;
        }

        void Heading(string text, float x, float y, float w)
        {
            var t = Text(body, "H_" + text, text, 19, UiKit.Gold, TextAlignmentOptions.BottomLeft, x, y, w, 26);
            t.characterSpacing = 4f;
            t.fontStyle = FontStyles.Bold;
            UiKit.Img(Box(body, "Rule_" + text, x, y + 28, w, 2), whiteSprite, new Color(1f, 0.86f, 0.45f, 0.28f));
        }

        void Build()
        {
            built = true;
            kit = new UiKit(font, fontOutline, windowSprite, buttonSprite, whiteSprite);
            var root = (RectTransform)window;
            var dim = UiKit.Stretch(root, "Dim");
            UiKit.Img(dim, whiteSprite, new Color(0, 0, 0, 0.6f)).raycastTarget = true;
            body = UiKit.Rect(root, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));
            kit.Frame(body);
            UiKit.Img(UiKit.Stretch(body, "Raycast"), whiteSprite, Color.clear).raycastTarget = true;
            var t = kit.Txt(body, "Title", "Lò Rèn", 40, UiKit.Gold, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(900, 50));
            t.fontStyle = FontStyles.Bold;
            kit.Txt(body, "Sub", "Thợ Rèn làng Lá Xanh", 18, UiKit.Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(900, 26), false);
            kit.Btn(body, "Close", "✗", new Vector2(1, 1), new Vector2(-58, -48), new Vector2(52, 48), 26, Close);

            // tabs: numbered, the open one lit
            for (int i = 0; i < tabs.Length; i++)
            {
                int k = i;
                var b = kit.Btn(body, "Tab" + i, $"{i + 1}  {TabNames[i]}", new Vector2(0, 1), Vector2.zero, new Vector2(230, 50), 22, () =>
                {
                    AudioManager.Play("sfx_ui_click", 0.6f);
                    pick = -1;
                    SetTab(k);
                });
                var brt = (RectTransform)b.transform;
                brt.pivot = new Vector2(0, 1);
                brt.anchoredPosition = new Vector2(ListX + i * 244f, -110f);
                tabs[i] = b;
            }
            helpText = Text(body, "Help", "", 18, UiKit.Muted, TextAlignmentOptions.MidlineLeft, ListX, 168, W - 2 * ListX, 30);

            // the list (left)
            var view = Box(body, "List", ListX, PanelTop, ListW, H - PanelTop - 120);
            UiKit.Img(view, whiteSprite, new Color(0.03f, 0.02f, 0.05f, 0.45f)).raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            listContent = UiKit.Rect(view, "Rows", new Vector2(0, 1), Vector2.zero, Vector2.zero);
            listContent.anchorMin = new Vector2(0, 1);
            listContent.anchorMax = new Vector2(1, 1);
            listContent.pivot = new Vector2(0.5f, 1);
            listContent.sizeDelta = Vector2.zero;
            var vl = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(8, 8, 8, 8);
            vl.spacing = 6;
            vl.childControlWidth = true;
            vl.childControlHeight = false;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            listScroll = view.gameObject.AddComponent<ScrollRect>();
            listScroll.content = listContent;
            listScroll.viewport = view;
            listScroll.horizontal = false;
            listScroll.movementType = ScrollRect.MovementType.Clamped;
            listScroll.inertia = false;
            listScroll.scrollSensitivity = 45f;

            // the pick (middle)
            var mid = Box(body, "Pick", MidX, PanelTop, MidW, H - PanelTop - 120);
            Panel(mid, new Color(0.55f, 0.5f, 0.62f));
            previewGlow = UiKit.Img(Box(body, "Glow", MidX + MidW / 2 - 200, PanelTop + 20, 400, 300), glowSprite != null ? glowSprite : whiteSprite,
                                    new Color(1f, 0.7f, 0.35f, glowSprite != null ? 0.3f : 0.05f));
            preview = UiKit.Img(Box(body, "Hero", MidX + MidW / 2 - 130, PanelTop + 24, 260, 260), null, Color.white);
            preview.preserveAspect = true;
            previewIcon = UiKit.Img(Box(body, "Item", MidX + MidW / 2 - 90, PanelTop + 64, 180, 180), null, Color.white);
            previewIcon.preserveAspect = true;
            nameText = Text(body, "Name", "", 32, UiKit.Gold, TextAlignmentOptions.Center, MidX, PanelTop + 296, MidW, 42, true);
            nameText.fontStyle = FontStyles.Bold;
            kindText = Text(body, "Kind", "", 18, UiKit.Muted, TextAlignmentOptions.Center, MidX, PanelTop + 338, MidW, 26);
            // now → after, big, like the reference's "100 → 105"
            nowLabel = Text(body, "NowLabel", "HIỆN TẠI", 16, UiKit.Muted, TextAlignmentOptions.Center, MidX + 60, PanelTop + 378, 220, 22);
            nextLabel = Text(body, "NextLabel", "SAU KHI RÈN", 16, UiKit.Muted, TextAlignmentOptions.Center, MidX + MidW - 280, PanelTop + 378, 220, 22);
            nowValue = Text(body, "Now", "", 46, UiKit.Cream, TextAlignmentOptions.Center, MidX + 60, PanelTop + 400, 220, 58, true);
            nowValue.fontStyle = FontStyles.Bold;
            nextValue = Text(body, "Next", "", 46, UiKit.Good, TextAlignmentOptions.Center, MidX + MidW - 280, PanelTop + 400, 220, 58, true);
            nextValue.fontStyle = FontStyles.Bold;
            arrow = Text(body, "Arrow", "→", 44, UiKit.Gold, TextAlignmentOptions.Center, MidX + MidW / 2 - 40, PanelTop + 400, 80, 58, true);
            detailText = Text(body, "Detail", "", 18, UiKit.Cream, TextAlignmentOptions.Top, MidX + 30, PanelTop + 470, MidW - 60, H - 120 - PanelTop - 480);

            // what it asks for (right)
            Heading("YÊU CẦU", ReqX, PanelTop - 6, ReqW);
            reqRoot = Box(body, "Requirements", ReqX, PanelTop + 32, ReqW, H - PanelTop - 200);
            statusText = Text(body, "Status", "", 20, UiKit.Good, TextAlignmentOptions.TopLeft, ReqX, H - 156, ReqW, 40);

            // the one button
            actionButton = kit.Btn(body, "Action", "", new Vector2(0, 1), Vector2.zero, new Vector2(420, 66), 28, DoAction);
            var art = (RectTransform)actionButton.transform;
            art.pivot = new Vector2(0.5f, 1);
            art.anchoredPosition = new Vector2(MidX + MidW / 2, -(H - 104));
            actionLabel = actionButton.GetComponentInChildren<TextMeshProUGUI>();
            actionLabel.fontStyle = FontStyles.Bold;
            reasonText = Text(body, "Reason", "", 17, UiKit.Bad, TextAlignmentOptions.MidlineLeft, MidX + MidW / 2 + 224, H - 100, 440, 58);
            Text(body, "Hint", "Bấm một dòng bên trái để xem  ·  Esc hoặc đi xa Thợ Rèn để đóng", 16, UiKit.Muted, TextAlignmentOptions.MidlineLeft, ListX, H - 88, ListW, 44);
        }

        // ================================================================== tabs and rows
        void SetTab(int t)
        {
            tab = Mathf.Clamp(t, 0, tabs.Length - 1);
            for (int i = 0; i < tabs.Length; i++)
            {
                var label = tabs[i].GetComponentInChildren<TextMeshProUGUI>();
                label.color = i == tab ? UiKit.Gold : UiKit.Cream;
                tabs[i].GetComponent<Image>().color = i == tab ? new Color(1f, 0.92f, 0.7f) : new Color(0.62f, 0.58f, 0.68f);
            }
            helpText.text = TabHelp[tab];
            FillList();
            if (listScroll != null) listScroll.verticalNormalizedPosition = 1f;
            ShowPick();
        }

        Row RowAt(int i)
        {
            while (rows.Count <= i)
            {
                var r = new Row();
                var rt = UiKit.Rect(listContent, "Row", new Vector2(0, 1), Vector2.zero, new Vector2(ListW - 16, 66));
                var le = rt.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 66;
                r.go = rt.gameObject;
                r.bg = Panel(rt, new Color(0.5f, 0.46f, 0.56f));
                r.bg.raycastTarget = true;
                var cell = UiKit.Rect(rt, "Cell", new Vector2(0, 0.5f), new Vector2(38, 0), new Vector2(54, 54));
                UiKit.Img(cell, slotSprite != null ? slotSprite : whiteSprite, slotSprite != null ? Color.white : new Color(0.1f, 0.08f, 0.12f),
                          slotSprite != null ? Image.Type.Sliced : Image.Type.Simple);
                r.swatch = UiKit.Img(UiKit.Rect(cell, "Swatch", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30)), whiteSprite, Color.clear);
                r.icon = UiKit.Img(UiKit.Rect(cell, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42, 42)), null, Color.white);
                r.icon.preserveAspect = true;
                r.name = kit.Txt(rt, "Name", "", 21, UiKit.Cream, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f), new Vector2(76 + 112, 11), new Vector2(224, 26), false);
                r.name.textWrappingMode = TextWrappingModes.NoWrap;
                r.name.overflowMode = TextOverflowModes.Ellipsis;
                r.sub = kit.Txt(rt, "Sub", "", 16, UiKit.Muted, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f), new Vector2(76 + 112, -13), new Vector2(224, 22), false);
                r.sub.textWrappingMode = TextWrappingModes.NoWrap;
                r.sub.overflowMode = TextOverflowModes.Ellipsis;
                r.badge = kit.Txt(rt, "Badge", "", 16, UiKit.Good, TextAlignmentOptions.MidlineRight, new Vector2(1, 0.5f), new Vector2(-66, 0), new Vector2(116, 40), false);
                r.frame = UiKit.Img(UiKit.Stretch(rt, "Picked"), highlightSprite != null ? highlightSprite : whiteSprite,
                                    highlightSprite != null ? UiKit.Gold : new Color(1f, 0.86f, 0.45f, 0.2f), highlightSprite != null ? Image.Type.Sliced : Image.Type.Simple);
                r.frame.enabled = false;
                int k = rows.Count;
                var hover = r.go.AddComponent<UiHover>();
                hover.click = b =>
                {
                    AudioManager.Play("sfx_ui_click", 0.5f);
                    Pick(k);
                };
                rows.Add(r);
            }
            var row = rows[i];
            row.go.SetActive(true);
            row.icon.enabled = true;
            row.swatch.color = Color.clear;
            return row;
        }

        void Pick(int i)
        {
            pick = i;
            for (int r = 0; r < rows.Count; r++) rows[r].frame.enabled = r == pick && rows[r].go.activeSelf;
            ShowPick();
        }

        /// <summary>Fills the left list for the tab, and picks the row that matters most if none is picked.</summary>
        void FillList()
        {
            var p = Players.Local;
            if (p == null || p.stats == null || !p.stats.HasClass) return;
            var s = p.stats;
            int n = 0, natural = 0;
            switch (tab)
            {
                case 0:
                {
                    int lv = s.look.upgrade;
                    for (int l = 1; l <= WeaponKinds.MaxUpgrade; l++)
                    {
                        var r = RowAt(n++);
                        var look = s.look.Clone();
                        look.upgrade = l;
                        r.icon.sprite = HeroArt.WeaponIcon(look);
                        r.name.text = $"{s.Weapon.name} +{l}";
                        r.sub.text = $"Công vũ khí {WeaponKinds.AttackOf(s.Weapon, l):0.#}" + (l == 7 ? " · phát sáng" : "");
                        bool done = l <= lv, next = l == lv + 1;
                        r.badge.text = done ? "✓ Đã rèn" : next ? (Forge.Missing(p, Forge.UpgradeCost(l)) == null ? "Rèn được" : "Tiếp theo") : "";
                        r.badge.color = done ? UiKit.Muted : Forge.Missing(p, Forge.UpgradeCost(l)) == null ? UiKit.Good : UiKit.Gold;
                        r.name.color = done ? UiKit.Muted : UiKit.Cream;
                        if (next) natural = l - 1;
                    }
                    if (lv >= WeaponKinds.MaxUpgrade) natural = WeaponKinds.MaxUpgrade - 1;
                    break;
                }
                case 1:
                    for (int m = 0; m < HeroLook.Metals.Length; m++)
                    {
                        var r = RowAt(n++);
                        var look = s.look.Clone();
                        look.metal = m;
                        r.icon.sprite = HeroArt.WeaponIcon(look);
                        var cost = Forge.MetalCost(m);
                        r.name.text = HeroLook.Metals[m].name;
                        r.sub.text = cost.level > 0 ? $"Cần vũ khí +{cost.level}" : "Mở sẵn";
                        bool on = m == s.look.metal;
                        bool locked = s.look.upgrade < cost.level;
                        r.badge.text = on ? "Đang dùng" : locked ? "Khóa" : Forge.Missing(p, cost) == null ? "Đổi được" : "";
                        r.badge.color = on ? UiKit.Gold : locked ? UiKit.Bad : UiKit.Good;
                        r.name.color = locked ? UiKit.Muted : UiKit.Cream;
                        if (on) natural = m;
                    }
                    break;
                case 2:
                {
                    var weapons = s.Class.weapons;
                    for (int i = 0; i < weapons.Length; i++)
                    {
                        var wk = WeaponKinds.Get(weapons[i]);
                        var r = RowAt(n++);
                        var look = s.look.Clone();
                        look.weapon = weapons[i];
                        r.icon.sprite = HeroArt.WeaponIcon(look);
                        if (r.icon.sprite == null) r.icon.sprite = s.Class.emblem;
                        r.name.text = wk != null ? wk.name : weapons[i];
                        r.sub.text = wk != null ? $"Công {wk.attack:0} · {(wk.focus ? "vật dẫn phép" : wk.ranged ? "đánh xa" : wk.finesse ? "vũ khí khéo" : "vũ khí nặng")}" : "";
                        bool on = weapons[i] == s.Weapon.id;
                        r.badge.text = on ? "Đang cầm" : "Miễn phí";
                        r.badge.color = on ? UiKit.Gold : UiKit.Good;
                        r.name.color = UiKit.Cream;
                        if (on) natural = i;
                    }
                    break;
                }
                default:
                {
                    var recipes = Forge.Recipes();
                    int firstCan = -1;
                    for (int i = 0; i < recipes.Count; i++)
                    {
                        var it = recipes[i];
                        var r = RowAt(n++);
                        r.icon.sprite = it.icon;
                        r.name.text = it.displayName;
                        r.name.color = it.RarityColor;
                        r.sub.text = $"{Gear.SlotName(it.slot)} · {it.BonusText(", ")}";
                        bool worn = p.inventory != null && p.inventory.Worn(it.slot) == it;
                        bool have = p.inventory != null && p.inventory.Count(it) > 0;
                        bool can = Forge.Missing(p, Forge.CraftCost(it)) == null;
                        r.badge.text = worn ? "Đang mặc" : have ? "Có trong túi" : can ? "Làm được" : "";
                        r.badge.color = worn || have ? UiKit.Gold : UiKit.Good;
                        if (can && !worn && !have && firstCan < 0) firstCan = i;
                    }
                    natural = firstCan >= 0 ? firstCan : 0;
                    break;
                }
            }
            for (int i = n; i < rows.Count; i++) rows[i].go.SetActive(false);
            if (pick < 0 || pick >= n) pick = natural;
            for (int r = 0; r < rows.Count; r++) rows[r].frame.enabled = r == pick && r < n;
        }

        // ================================================================== the pick
        void ShowPick()
        {
            var p = Players.Local;
            if (p == null || p.stats == null || !p.stats.HasClass || body == null) return;
            var s = p.stats;
            var w = s.Weapon;
            previewLook = null;
            previewItem = null;
            var cost = new Forge.Cost { items = new (string, int)[0] };
            string action = "", why = null, detail = "";
            bool free = false;
            nowLabel.text = "HIỆN TẠI";
            nextLabel.text = "SAU KHI RÈN";
            switch (tab)
            {
                case 0:
                {
                    int lv = s.look.upgrade, sel = Mathf.Clamp(pick + 1, 1, WeaponKinds.MaxUpgrade);
                    previewLook = s.look.Clone();
                    previewLook.upgrade = sel;
                    nameText.text = $"{w.name} +{sel}";
                    kindText.text = $"Vũ khí · {HeroLook.Metals[Mathf.Clamp(s.look.metal, 0, HeroLook.Metals.Length - 1)].name}" + (sel >= 7 ? " · phát sáng" : "");
                    nowLabel.text = $"CÔNG VŨ KHÍ +{lv}";
                    nextLabel.text = sel > lv ? $"SAU KHI RÈN +{sel}" : $"+{sel}";
                    SetNumbers(WeaponKinds.AttackOf(w, lv), WeaponKinds.AttackOf(w, sel), sel > lv);
                    cost = Forge.UpgradeCost(sel);
                    if (lv >= WeaponKinds.MaxUpgrade) why = "Vũ khí đã được rèn tới +10.";
                    else if (sel <= lv) why = $"Đã rèn qua +{sel}.";
                    else if (sel > lv + 1) why = $"Hãy rèn +{lv + 1} trước.";
                    else why = Forge.Missing(p, cost);
                    action = sel > lv ? $"RÈN LÊN +{sel}" : "ĐÃ RÈN";
                    detail = $"Mỗi cấp rèn +8% công của vũ khí. Công hiện tại của nhân vật: <b>{s.Stats.Get(w.focus ? StatId.MagicAttack : StatId.PhysicalAttack):0.#}</b>.";
                    break;
                }
                case 1:
                {
                    int m = Mathf.Clamp(pick, 0, HeroLook.Metals.Length - 1);
                    previewLook = s.look.Clone();
                    previewLook.metal = m;
                    nameText.text = $"{w.name} · {HeroLook.Metals[m].name}";
                    kindText.text = "Kim loại";
                    nowLabel.text = "ĐANG DÙNG";
                    nextLabel.text = "ĐỔI SANG";
                    SetWords(HeroLook.Metals[Mathf.Clamp(s.look.metal, 0, HeroLook.Metals.Length - 1)].name, HeroLook.Metals[m].name);
                    cost = Forge.MetalCost(m);
                    why = m == s.look.metal ? "Vũ khí đang dùng kim loại này." : Forge.Missing(p, cost);
                    action = $"ĐỔI SANG {HeroLook.Metals[m].name.ToUpperInvariant()}";
                    detail = "Kim loại chỉ đổi màu và vẻ ngoài của vũ khí; công giữ nguyên.";
                    if (cost.level > 0) detail += $"\nCần vũ khí rèn tới <b>+{cost.level}</b> (hiện tại +{s.look.upgrade}).";
                    break;
                }
                case 2:
                {
                    var weapons = s.Class.weapons;
                    string id = weapons[Mathf.Clamp(pick, 0, weapons.Length - 1)];
                    var wk = WeaponKinds.Get(id);
                    previewLook = s.look.Clone();
                    previewLook.weapon = id;
                    nameText.text = wk != null ? wk.name : id;
                    kindText.text = $"Vũ khí của {s.Class.displayName}";
                    nowLabel.text = w.name.ToUpperInvariant();
                    nextLabel.text = (wk != null ? wk.name : id).ToUpperInvariant();
                    SetNumbers(WeaponKinds.AttackOf(w, s.look.upgrade), WeaponKinds.AttackOf(wk, s.look.upgrade), id != w.id);
                    free = true;
                    why = id == w.id ? "Đang cầm vũ khí này." : null;
                    action = $"CẦM {(wk != null ? wk.name : id).ToUpperInvariant()}";
                    detail = wk != null ? wk.description : "";
                    break;
                }
                default:
                {
                    var recipes = Forge.Recipes();
                    if (recipes.Count == 0) break;
                    var it = recipes[Mathf.Clamp(pick, 0, recipes.Count - 1)];
                    previewItem = it;
                    nameText.text = it.displayName;
                    nameText.color = it.RarityColor;
                    kindText.text = $"{it.RarityName} · {it.KindName}";
                    var worn = p.inventory != null ? p.inventory.Worn(it.slot) : null;
                    nowLabel.text = "ĐANG MẶC";
                    nextLabel.text = "MÓN NÀY";
                    SetWords(worn != null ? worn.BonusText("\n") : "—", it.BonusText("\n"), 20);
                    cost = Forge.CraftCost(it);
                    why = Forge.Missing(p, cost);
                    if (why == null && p.inventory != null && !p.inventory.HasRoomFor(it)) why = "Túi đầy.";
                    action = "CHẾ TẠO";
                    detail = it.description + (worn != null ? $"\n<color=#b8b0c8>Đang mặc: {worn.displayName}</color>" : "");
                    break;
                }
            }
            if (tab != 3) nameText.color = UiKit.Gold;
            detailText.text = detail;
            preview.enabled = previewLook != null;
            previewIcon.enabled = previewItem != null;
            if (previewItem != null) previewIcon.sprite = previewItem.icon;
            ShowCost(p, cost, free);
            actionLabel.text = action;
            bool can = why == null && !string.IsNullOrEmpty(action);
            actionButton.interactable = can;
            actionLabel.color = can ? UiKit.Gold : UiKit.Muted;
            reasonText.text = why ?? "";
            statusText.text = why == null ? "<color=#8cf08c>✓ Đủ rồi, bấm nút bên dưới.</color>" : "";
        }

        void SetNumbers(float now, float next, bool change)
        {
            nowValue.fontSize = nextValue.fontSize = 46;
            nowValue.text = $"{now:0.#}";
            nextValue.text = change ? $"{next:0.#}" : "—";
            nextValue.color = !change ? UiKit.Muted : next > now ? UiKit.Good : next < now ? UiKit.Bad : UiKit.Cream;
            arrow.enabled = true;
        }

        void SetWords(string now, string next, float size = 30)
        {
            nowValue.fontSize = nextValue.fontSize = size;
            nowValue.text = now;
            nextValue.text = next;
            nextValue.color = UiKit.Good;
            arrow.enabled = true;
        }

        /// <summary>The right column: gold, then each item with its icon, have/need and where it drops.</summary>
        void ShowCost(PlayerController p, Forge.Cost cost, bool free)
        {
            foreach (var go in reqRows) Destroy(go);
            reqRows.Clear();
            var db = GameManager.I.db;
            float y = 0;
            if (free)
            {
                var t = kit.Txt(reqRoot, "Free", "Miễn phí.", 22, UiKit.Good, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(ReqW / 2, -20), new Vector2(ReqW, 40), false);
                reqRows.Add(t.gameObject);
                return;
            }
            if (cost.level > 0) ReqRow(null, $"Vũ khí rèn tới +{cost.level}", p.stats.look.upgrade, cost.level, null, ref y);
            if (cost.gold > 0) ReqRow(db.Item("gold"), "Vàng", p.inventory.gold, cost.gold, null, ref y);
            foreach (var (id, n) in cost.items)
            {
                var item = db.Item(id);
                int have = item != null ? p.inventory.Count(item) : 0;
                sources.TryGetValue(id, out var from);
                ReqRow(item, item != null ? item.displayName : id, have, n, have < n ? from : null, ref y);
            }
            if (cost.gold <= 0 && cost.items.Length == 0 && cost.level <= 0)
            {
                var t = kit.Txt(reqRoot, "Nothing", "Không cần gì thêm.", 20, UiKit.Muted, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(ReqW / 2, -20), new Vector2(ReqW, 40), false);
                reqRows.Add(t.gameObject);
            }
        }

        void ReqRow(ItemDef item, string name, int have, int need, string from, ref float y)
        {
            float h = from != null ? 78 : 60;
            var rt = Box(reqRoot, "Req", 0, y, ReqW, h - 6);
            reqRows.Add(rt.gameObject);
            var bg = Panel(rt, new Color(0.5f, 0.46f, 0.56f));
            bg.raycastTarget = true;
            var cell = UiKit.Rect(rt, "Cell", new Vector2(0, 1), new Vector2(32, -27), new Vector2(46, 46));
            UiKit.Img(cell, slotSprite != null ? slotSprite : whiteSprite, slotSprite != null ? Color.white : new Color(0.1f, 0.08f, 0.12f),
                      slotSprite != null ? Image.Type.Sliced : Image.Type.Simple);
            if (item != null && item.icon != null)
                UiKit.Img(UiKit.Rect(cell, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36, 36)), item.icon, Color.white).preserveAspect = true;
            else kit.Txt(cell, "Plus", "+", 26, UiKit.Gold, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46));
            bool ok = have >= need;
            var n = kit.Txt(rt, "Name", name, 20, ok ? UiKit.Cream : UiKit.Bad, TextAlignmentOptions.MidlineLeft, new Vector2(0, 1), new Vector2(66 + 120, -27), new Vector2(240, 28), false);
            n.textWrappingMode = TextWrappingModes.NoWrap;
            kit.Txt(rt, "Count", $"<color={(ok ? ItemTips.Good : ItemTips.Bad)}>{have}</color> / {need}", 22, UiKit.Cream, TextAlignmentOptions.MidlineRight,
                new Vector2(1, 1), new Vector2(-70, -27), new Vector2(120, 28), false);
            if (from != null)
            {
                var f = kit.Txt(rt, "From", "Rơi từ: " + from, 15, UiKit.Muted, TextAlignmentOptions.MidlineLeft, new Vector2(0, 1), new Vector2(66 + 175, -56), new Vector2(350, 22), false);
                f.textWrappingMode = TextWrappingModes.NoWrap;
                f.overflowMode = TextOverflowModes.Ellipsis;
            }
            if (item != null)
            {
                var hover = rt.gameObject.AddComponent<UiHover>();
                hover.tip = () => ItemTips.For(item, Players.Local);
            }
            y += h;
        }

        /// <summary>Which monsters of the world drop what (the list's "Rơi từ").</summary>
        void FindSources()
        {
            sources.Clear();
            var names = new Dictionary<string, List<string>>();
            void Note(List<LootEntry> loot, string who)
            {
                if (loot == null || string.IsNullOrEmpty(who)) return;
                foreach (var e in loot)
                {
                    if (e == null || string.IsNullOrEmpty(e.itemId)) continue;
                    if (!names.TryGetValue(e.itemId, out var list)) names[e.itemId] = list = new List<string>();
                    if (!list.Contains(who)) list.Add(who);
                }
            }
            foreach (var e in EnemyBase.All)
                if (e != null) Note(e.loot, e.displayName);
            foreach (var b in BossBase.All)
                if (b != null) Note(b.loot, b.displayName);
            foreach (var kv in names)
            {
                var l = kv.Value;
                sources[kv.Key] = l.Count > 3 ? string.Join(", ", l.GetRange(0, 3)) + "…" : string.Join(", ", l);
            }
        }

        void Animate()
        {
            if (previewLook == null || preview == null) return;
            var set = HeroArt.SetFor(previewLook);
            if (set == null) return;
            var idle = set.Get("idle_down");
            var attack = set.Get("attack_down");
            animT += Time.unscaledDeltaTime;
            float t = animT % 2.4f;
            if (attack != null && attack.frames.Length > 0 && t > 1.6f)
                preview.sprite = attack.frames[Mathf.Min(attack.frames.Length - 1, Mathf.FloorToInt((t - 1.6f) * 6f))];
            else if (idle != null && idle.frames.Length > 0)
                preview.sprite = idle.frames[Mathf.FloorToInt(t * 3f) % idle.frames.Length];
        }

        void DoAction()
        {
            var p = Players.Local;
            if (p == null || p.stats == null) return;
            switch (tab)
            {
                case 0:
                    Forge.Ask(Forge.Action.Upgrade);
                    break;
                case 1:
                    Forge.Ask(Forge.Action.Metal, Mathf.Clamp(pick, 0, HeroLook.Metals.Length - 1));
                    break;
                case 2:
                {
                    var weapons = p.stats.Class.weapons;
                    Forge.Ask(Forge.Action.Weapon, 0, weapons[Mathf.Clamp(pick, 0, weapons.Length - 1)]);
                    break;
                }
                default:
                {
                    var recipes = Forge.Recipes();
                    if (recipes.Count > 0) Forge.Ask(Forge.Action.Craft, 0, recipes[Mathf.Clamp(pick, 0, recipes.Count - 1)].id);
                    break;
                }
            }
            // after a tempering the next level is the natural pick
            if (tab == 0) pick = -1;
        }
    }
}

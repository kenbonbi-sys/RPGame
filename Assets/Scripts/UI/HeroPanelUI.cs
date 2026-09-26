using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// "Nhân Vật" (B, I or C: one window for all three, like Minecraft's): the character sheet,
    /// the hero with their gear and the bag side by side.
    /// <list type="bullet">
    /// <item>Left, laid out like D&amp;D Beyond's sheet: the six ability scores as boxes (the
    /// modifier big, the score under it, + to spend a point), the saving throws with the class's
    /// proficient ones marked, and the combat numbers the scores make.</item>
    /// <item>Middle, after Kingshot's hero screen: the hero standing between their gear slots
    /// (Mũ, Giáp, Giày on the left; the forge's weapon, Tay Phụ, Nhẫn on the right), their health,
    /// armor, attack and energy under them, and their people's traits.</item>
    /// <item>Right: the bag, scrolled with the wheel or the bar, filtered by sort. Right click
    /// uses food and potions; a click on gear puts it on, a click on a worn piece takes it off.</item>
    /// </list>
    /// Built at run time from the HUD's font and sprites, for the hero on this screen; scaled
    /// down to fit narrower screens.
    /// </summary>
    public class HeroPanelUI : UIPanel
    {
        public TMP_FontAsset font;
        public Material fontOutline;
        public Sprite windowSprite, panelSprite, slotSprite, highlightSprite, dividerSprite, whiteSprite, glowSprite, portraitSprite;

        public static HeroPanelUI I { get; private set; }

        public const float W = 1720f, H = 960f;
        const float Top = 150f;
        const int Columns = 6;
        const float Cell = 80f, Gap = 8f;

        static readonly Color BoxColor = new Color(0.12f, 0.1f, 0.15f, 1f);
        static readonly Color RuleColor = new Color(1f, 0.86f, 0.45f, 0.28f);
        static readonly Color XpColor = new Color(0.66f, 0.45f, 1f);
        static readonly string[] FilterNames = { "Tất cả", "Trang bị", "Tiêu hao", "Nguyên liệu" };

        /// <summary>What each ability does in this game (the boxes' tooltips).</summary>
        static readonly string[] AbilityUse =
        {
            "Đòn của vũ khí nặng (kiếm, rìu, chùy, giáo) · Trấn Áp mạnh hơn.",
            "Vũ khí khéo và cung · chí mạng · tốc đánh · Lướt hồi nhanh.",
            "+10 máu mỗi điểm · giáp.",
            "Phép của Pháp Sư · giảm hồi chiêu.",
            "Phép của Tu Sĩ, Tế Sư, Du Hiệp, Võ Tăng · hồi máu · kháng hệ.",
            "Phép của Thi Sĩ, Thuật Sĩ, Khế Ước Sư, Hiệp Sĩ Thánh · vàng nhặt thêm.",
        };

        static readonly string[] CombatNames =
        {
            "Công vật lý", "Công phép", "Chí mạng", "Đòn chí mạng", "Tốc đánh", "Giảm hồi chiêu",
            "Kháng hệ", "Giáp chặn", "Hồi máu", "Hồi Lướt", "Trấn Áp", "Vàng nhặt thêm",
        };

        class GearSlot
        {
            public EquipSlot slot;   // None: the weapon
            public Image icon, ghost;
            public TextMeshProUGUI badge;
        }

        class BagCell
        {
            public GameObject go;
            public Image icon, rarity;
            public TextMeshProUGUI count;
            public Inventory.Stack stack;
        }

        UiKit kit;
        bool built;
        RectTransform body, grid;
        Image portrait, xpFill, hero;
        TextMeshProUGUI identity, subIdentity, xpText, pointsText, profText, traitsText, countText, goldText;
        readonly TextMeshProUGUI[] abilityMod = new TextMeshProUGUI[CoreStats.Count];
        readonly TextMeshProUGUI[] abilityScore = new TextMeshProUGUI[CoreStats.Count];
        readonly Button[] plus = new Button[CoreStats.Count];
        readonly Image[] saveDots = new Image[CoreStats.Count];
        readonly TextMeshProUGUI[] saveValues = new TextMeshProUGUI[CoreStats.Count];
        readonly TextMeshProUGUI[] combatValues = new TextMeshProUGUI[12];
        readonly TextMeshProUGUI[] summaryValues = new TextMeshProUGUI[4];
        readonly List<GearSlot> gear = new List<GearSlot>();
        readonly List<BagCell> cells = new List<BagCell>();
        readonly Button[] filterButtons = new Button[4];
        ScrollRect scroll;
        int filter;
        float animT;
        bool dirty;

        PlayerController bound;

        static PlayerController Me => Players.Local;

        protected override void Awake()
        {
            base.Awake();
            I = this;
            blocksGameplay = true;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            Bind(null);
        }

        public override void Show()
        {
            if (!built) Build();
            base.Show();
        }

        protected override void OnShow()
        {
            Fit();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
            Refresh();
        }

        protected override void Update()
        {
            base.Update();
            if (Me != bound) Bind(Me);
            if (!IsOpen || !built) return;
            Fit();
            if (dirty) Refresh();
            AnimateHero();
        }

        void Bind(PlayerController p)
        {
            if (bound != null)
            {
                if (bound.stats != null)
                {
                    bound.stats.Changed -= MarkDirty;
                    bound.stats.LookChanged -= MarkDirty;
                }
                if (bound.inventory != null) bound.inventory.Changed -= MarkDirty;
            }
            bound = p;
            if (bound != null)
            {
                if (bound.stats != null)
                {
                    bound.stats.Changed += MarkDirty;
                    bound.stats.LookChanged += MarkDirty;
                }
                if (bound.inventory != null) bound.inventory.Changed += MarkDirty;
            }
            dirty = true;
        }

        void MarkDirty() => dirty = true;

        /// <summary>Scrolls the bag (1: the top, 0: the bottom); for automated tours.</summary>
        public void DebugScroll(float at)
        {
            Refresh();
            Canvas.ForceUpdateCanvases();
            if (scroll != null) scroll.verticalNormalizedPosition = at;
        }

        /// <summary>Shows one sort of the bag (0 all, 1 gear, 2 consumables, 3 materials); for automated tours.</summary>
        public void DebugFilter(int f)
        {
            filter = Mathf.Clamp(f, 0, FilterNames.Length - 1);
            Refresh();
        }

        /// <summary>Narrower screens (the canvas matches height): the window shrinks to fit.</summary>
        void Fit()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null || body == null) return;
            var r = ((RectTransform)canvas.transform).rect;
            float s = Mathf.Min(1f, (r.width - 24f) / W, (r.height - 16f) / H);
            body.localScale = new Vector3(s, s, 1f);
        }

        // ================================================================== layout helpers
        /// <summary>A rectangle placed from the window's top-left corner (x right, y down).</summary>
        static RectTransform Box(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = UiKit.Rect(parent, name, new Vector2(0, 1), Vector2.zero, new Vector2(w, h));
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            return rt;
        }

        TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align,
                             float x, float y, float w, float h, bool outline = false)
        {
            var t = kit.Txt(parent, name, text, size, color, align, new Vector2(0, 1), Vector2.zero, new Vector2(w, h), outline);
            t.rectTransform.pivot = new Vector2(0, 1);
            t.rectTransform.anchoredPosition = new Vector2(x, -y);
            return t;
        }

        Image Panel(RectTransform rt, Color? tint = null)
        {
            var img = UiKit.Img(rt, panelSprite != null ? panelSprite : whiteSprite, tint ?? Color.white,
                                panelSprite != null ? Image.Type.Sliced : Image.Type.Simple);
            return img;
        }

        /// <summary>A section title in small capitals with a thin rule under it (D&amp;D Beyond's headings).</summary>
        TextMeshProUGUI Heading(string text, float x, float y, float w)
        {
            var t = Text(body, "H_" + text, text, 19, UiKit.Gold, TextAlignmentOptions.BottomLeft, x, y, w, 26);
            t.characterSpacing = 4f;
            t.fontStyle = FontStyles.Bold;
            UiKit.Img(Box(body, "Rule_" + text, x, y + 28, w, 2), whiteSprite, RuleColor);
            return t;
        }

        UiHover Hover(GameObject go, Image highlight = null)
        {
            var h = go.GetComponent<UiHover>();
            if (h == null) h = go.AddComponent<UiHover>();
            h.highlight = highlight;
            return h;
        }

        // ================================================================== build
        void Build()
        {
            built = true;
            kit = new UiKit(font, fontOutline, windowSprite, panelSprite, whiteSprite);
            var root = (RectTransform)window;
            if (root == null) root = (RectTransform)transform;
            var dim = UiKit.Stretch(root, "Dim");
            UiKit.Img(dim, whiteSprite, new Color(0, 0, 0, 0.55f)).raycastTarget = true;
            body = UiKit.Rect(root, "Window", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));
            kit.Frame(body);
            UiKit.Img(UiKit.Stretch(body, "Raycast"), whiteSprite, Color.clear).raycastTarget = true;

            var title = kit.Txt(body, "Title", "Nhân Vật", 40, UiKit.Gold, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(600, 50));
            title.fontStyle = FontStyles.Bold;
            if (dividerSprite != null) UiKit.Img(UiKit.Rect(body, "Divider", new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(420, 14)), dividerSprite, Color.white);
            kit.Btn(body, "Close", "✗", new Vector2(1, 1), new Vector2(-58, -48), new Vector2(52, 48), 26, Close);

            BuildHeader();
            BuildSheet(36f);
            BuildGear(600f);
            BuildBag(1144f);

            Text(body, "Hint", "<color=#ffe07a>B</color> / <color=#ffe07a>C</color> / Esc: đóng   ·   Túi đồ: chuột phải để dùng, bấm vào trang bị để mặc   ·   Bấm ô trang bị trên người để tháo",
                17, UiKit.Muted, TextAlignmentOptions.Center, 40, H - 48, W - 80, 26);
        }

        void BuildHeader()
        {
            var pf = Box(body, "PortraitFrame", 40, 96, 76, 76);
            UiKit.Img(pf, portraitSprite != null ? portraitSprite : slotSprite, Color.white, Image.Type.Sliced);
            portrait = UiKit.Img(UiKit.Rect(pf, "Portrait", new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(64, 64)), null, Color.white);
            portrait.preserveAspect = true;
            identity = Text(body, "Identity", "", 25, UiKit.Cream, TextAlignmentOptions.TopLeft, 132, 98, 900, 34);
            identity.textWrappingMode = TextWrappingModes.NoWrap;
            subIdentity = Text(body, "SubIdentity", "", 18, UiKit.Muted, TextAlignmentOptions.TopLeft, 132, 134, 900, 26);
            subIdentity.textWrappingMode = TextWrappingModes.NoWrap;
            // the XP bar over the bag's column
            xpText = Text(body, "XpText", "", 18, new Color(0.86f, 0.8f, 1f), TextAlignmentOptions.BottomRight, 1144, 100, 540, 26);
            var track = Box(body, "XpTrack", 1144, 130, 540, 16);
            UiKit.Img(track, whiteSprite, new Color(0.05f, 0.03f, 0.07f, 1f));
            var fillRt = UiKit.Stretch(track, "Fill");
            fillRt.offsetMin = new Vector2(2, 2);
            fillRt.offsetMax = new Vector2(-2, -2);
            xpFill = UiKit.Img(fillRt, whiteSprite, XpColor, Image.Type.Filled);
            xpFill.fillMethod = Image.FillMethod.Horizontal;
            UiKit.Img(Box(body, "HeaderRule", 36, Top + 26, W - 72, 2), whiteSprite, new Color(1f, 1f, 1f, 0.08f));
        }

        // ------------------------------------------------------------------ the sheet (left)
        void BuildSheet(float x)
        {
            const float w = 540f;
            float y = Top + 40;
            Heading("CHỈ SỐ", x, y, w);
            pointsText = Text(body, "Points", "", 18, UiKit.Muted, TextAlignmentOptions.BottomRight, x + 200, y, w - 200, 26);
            y += 42;
            const float bw = 170f, bh = 132f, gap = 15f;
            for (int i = 0; i < CoreStats.Count; i++)
            {
                int k = i;
                var a = CoreStats.SheetOrder[i];
                float bx = x + (i % 3) * (bw + gap), by = y + (i / 3) * (bh + 12f);
                var box = Box(body, "Ability_" + CoreStats.Short(a), bx, by, bw, bh);
                var bg = Panel(box, new Color(0.85f, 0.8f, 0.9f));
                bg.raycastTarget = true;
                var hl = UiKit.Img(UiKit.Stretch(box, "Highlight"), highlightSprite != null ? highlightSprite : whiteSprite,
                                   highlightSprite != null ? Color.white : new Color(1, 1, 1, 0.1f), highlightSprite != null ? Image.Type.Sliced : Image.Type.Simple);
                hl.enabled = false;
                var label = kit.Txt(box, "Name", CoreStats.Name(a).ToUpperInvariant(), 16, UiKit.Muted, TextAlignmentOptions.Center,
                                    new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(bw - 16, 22), false);
                label.characterSpacing = 2f;
                abilityMod[i] = kit.Txt(box, "Mod", "+0", 50, UiKit.Cream, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(bw, 60));
                abilityMod[i].fontStyle = FontStyles.Bold;
                var oval = UiKit.Rect(box, "Score", new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(72, 28));
                UiKit.Img(oval, whiteSprite, new Color(0.04f, 0.03f, 0.06f, 0.9f));
                abilityScore[i] = kit.Txt(oval, "Value", "10", 21, UiKit.Gold, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(72, 28), false);
                plus[i] = kit.Btn(box, "Plus", "+", new Vector2(1, 0), new Vector2(-26, 24), new Vector2(38, 36), 28, () =>
                {
                    var me = Me;
                    if (me != null && me.stats != null) me.stats.Spend(CoreStats.SheetOrder[k]);
                });
                plus[i].GetComponentInChildren<TextMeshProUGUI>().color = UiKit.Good;
                Hover(box.gameObject, hl).tip = () => AbilityTip(k);
            }
            y += 2 * bh + 12f + 20f;

            Heading("KHÁNG  ·  SAVING THROW", x, y, w);
            profText = Text(body, "Proficiency", "", 18, UiKit.Muted, TextAlignmentOptions.BottomRight, x + 280, y, w - 280, 26);
            y += 38;
            for (int i = 0; i < CoreStats.Count; i++)
            {
                int k = i;
                var a = CoreStats.SheetOrder[i];
                float rx = x + (i % 2) * (w / 2f + 6f), ry = y + (i / 2) * 30f;
                var row = Box(body, "Save_" + CoreStats.Short(a), rx, ry, w / 2f - 6f, 28);
                UiKit.Img(row, whiteSprite, new Color(0.05f, 0.03f, 0.07f, 0.35f)).raycastTarget = true;
                saveDots[i] = UiKit.Img(UiKit.Rect(row, "Dot", new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(12, 12)), whiteSprite, UiKit.Muted);
                kit.Txt(row, "Name", CoreStats.Name(a), 19, UiKit.Cream, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f), new Vector2(34 + 90, 0), new Vector2(180, 28), false);
                saveValues[i] = kit.Txt(row, "Value", "+0", 20, UiKit.Cream, TextAlignmentOptions.MidlineRight, new Vector2(1, 0.5f), new Vector2(-46, 0), new Vector2(80, 28), false);
                Hover(row.gameObject).tip = () => SaveTip(k);
            }
            y += 3 * 30f + 14f;

            Heading("CHIẾN ĐẤU", x, y, w);
            y += 38;
            for (int i = 0; i < combatValues.Length; i++)
            {
                float rx = x + (i % 2) * (w / 2f + 6f), ry = y + (i / 2) * 27f;
                var row = Box(body, "Combat_" + i, rx, ry, w / 2f - 6f, 26);
                kit.Txt(row, "Name", CombatNames[i], 18, UiKit.Muted, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f), new Vector2(4 + 100, 0), new Vector2(200, 26), false);
                combatValues[i] = kit.Txt(row, "Value", "", 19, UiKit.Cream, TextAlignmentOptions.MidlineRight, new Vector2(1, 0.5f), new Vector2(-66, 0), new Vector2(124, 26), false);
            }
        }

        // ------------------------------------------------------------------ the hero and their gear (middle)
        void BuildGear(float x)
        {
            const float w = 520f;
            float y = Top + 40;
            Heading("TRANG BỊ", x, y, w);
            y += 44;
            float cx = x + w / 2f;
            // the stage: a warm light on the floor, the hero on it
            var floor = Box(body, "Floor", cx - 180, y + 250, 360, 110);
            UiKit.Img(floor, glowSprite != null ? glowSprite : whiteSprite, new Color(1f, 0.78f, 0.4f, glowSprite != null ? 0.35f : 0.08f));
            var halo = Box(body, "Halo", cx - 170, y + 10, 340, 340);
            UiKit.Img(halo, glowSprite != null ? glowSprite : whiteSprite, new Color(0.6f, 0.5f, 1f, glowSprite != null ? 0.12f : 0.03f));
            hero = UiKit.Img(Box(body, "Hero", cx - 150, y + 20, 300, 300), null, Color.white);
            hero.preserveAspect = true;

            EquipSlot[] left = { EquipSlot.Head, EquipSlot.Body, EquipSlot.Feet };
            EquipSlot[] right = { EquipSlot.None, EquipSlot.Offhand, EquipSlot.Ring };
            for (int i = 0; i < 3; i++)
            {
                GearCell(left[i], x + 6, y + 6 + i * 122);
                GearCell(right[i], x + w - 6 - 96, y + 6 + i * 122);
            }
            y += 374;

            // D&D Beyond's boxes under the portrait: health, armor, attack, energy
            string[] names = { "MÁU", "GIÁP", "CÔNG", "NĂNG LƯỢNG" };
            const float bw = 121f, gap = 12f;
            for (int i = 0; i < 4; i++)
            {
                var box = Box(body, "Summary_" + i, x + i * (bw + gap), y, bw, 84);
                Panel(box, new Color(0.85f, 0.8f, 0.9f));
                kit.Txt(box, "Name", names[i], 15, UiKit.Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(bw, 20), false).characterSpacing = 2f;
                summaryValues[i] = kit.Txt(box, "Value", "", 27, UiKit.Cream, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(bw, 36));
                summaryValues[i].fontStyle = FontStyles.Bold;
                summaryValues[i].textWrappingMode = TextWrappingModes.NoWrap;
            }
            y += 100;

            Heading("ĐẶC ĐIỂM", x, y, w);
            var looks = kit.Btn(body, "Looks", "Ngoại hình", new Vector2(0, 1), Vector2.zero, new Vector2(150, 34), 17, () =>
            {
                var me = Me;
                if (CharacterCreatorUI.I == null || me == null || me.stats == null) return;
                Close();
                CharacterCreatorUI.I.Open(me.stats.HasClass ? CharacterCreatorUI.Mode.Looks : CharacterCreatorUI.Mode.New);
            });
            var lrt = (RectTransform)looks.transform;
            lrt.pivot = new Vector2(1, 1);
            lrt.anchoredPosition = new Vector2(x + w, -(y - 8));
            traitsText = Text(body, "Traits", "", 17, UiKit.Cream, TextAlignmentOptions.TopLeft, x, y + 38, w, H - 70 - (y + 38));
            traitsText.overflowMode = TextOverflowModes.Ellipsis;
        }

        void GearCell(EquipSlot slot, float x, float y)
        {
            var g = new GearSlot { slot = slot };
            var rt = Box(body, "Gear_" + (slot == EquipSlot.None ? "Weapon" : slot.ToString()), x, y, 96, 96);
            UiKit.Img(rt, slotSprite != null ? slotSprite : whiteSprite, Color.white, slotSprite != null ? Image.Type.Sliced : Image.Type.Simple).raycastTarget = true;
            g.ghost = UiKit.Img(UiKit.Rect(rt, "Ghost", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64)), null, new Color(0.55f, 0.5f, 0.62f, 0.22f));
            g.ghost.preserveAspect = true;
            g.icon = UiKit.Img(UiKit.Rect(rt, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70)), null, Color.white);
            g.icon.preserveAspect = true;
            g.badge = kit.Txt(rt, "Badge", "", 18, UiKit.Gold, TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(-30, -16), new Vector2(56, 24));
            var hl = UiKit.Img(UiKit.Stretch(rt, "Highlight"), highlightSprite != null ? highlightSprite : whiteSprite,
                               highlightSprite != null ? Color.white : new Color(1, 1, 1, 0.1f), highlightSprite != null ? Image.Type.Sliced : Image.Type.Simple);
            hl.enabled = false;
            var label = kit.Txt(body, "GearLabel_" + slot, slot == EquipSlot.None ? "Vũ Khí" : Gear.SlotName(slot), 16, UiKit.Muted, TextAlignmentOptions.Center,
                                new Vector2(0, 1), Vector2.zero, new Vector2(110, 22), false);
            label.rectTransform.pivot = new Vector2(0.5f, 1);
            label.rectTransform.anchoredPosition = new Vector2(x + 48, -(y + 98));
            var hover = Hover(rt.gameObject, hl);
            hover.tip = () => GearTip(g);
            hover.click = b =>
            {
                var me = Me;
                if (g.slot == EquipSlot.None || me == null || me.inventory == null || me.inventory.Worn(g.slot) == null) return;
                me.inventory.AskEquip(null, g.slot);
            };
            gear.Add(g);
        }

        // ------------------------------------------------------------------ the bag (right)
        void BuildBag(float x)
        {
            const float w = 540f;
            float y = Top + 40;
            Heading("TÚI ĐỒ", x, y, w);
            countText = Text(body, "Count", "", 18, UiKit.Muted, TextAlignmentOptions.BottomRight, x + 200, y, w - 200, 26);
            y += 42;
            float fw = (w - 3 * 8f) / 4f;
            for (int i = 0; i < filterButtons.Length; i++)
            {
                int k = i;
                var b = kit.Btn(body, "Filter_" + i, FilterNames[i], new Vector2(0, 1), Vector2.zero, new Vector2(fw, 36), 17, () =>
                {
                    filter = k;
                    AudioManager.Play("sfx_ui_click", 0.5f);
                    if (scroll != null) scroll.verticalNormalizedPosition = 1f;
                    Refresh();
                });
                var brt = (RectTransform)b.transform;
                brt.pivot = new Vector2(0, 1);
                brt.anchoredPosition = new Vector2(x + i * (fw + 8f), -y);
                filterButtons[i] = b;
            }
            y += 48;

            // the scrolled grid: the wheel, a drag or the bar
            float viewH = H - 120 - y;
            var view = Box(body, "BagView", x, y, w - 18, viewH);
            UiKit.Img(view, whiteSprite, new Color(0.03f, 0.02f, 0.05f, 0.45f)).raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            grid = UiKit.Rect(view, "Grid", new Vector2(0, 1), Vector2.zero, Vector2.zero);
            grid.anchorMin = new Vector2(0, 1);
            grid.anchorMax = new Vector2(1, 1);
            grid.pivot = new Vector2(0.5f, 1);
            grid.anchoredPosition = Vector2.zero;
            grid.sizeDelta = Vector2.zero;
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(Cell, Cell);
            gl.spacing = new Vector2(Gap, Gap);
            gl.padding = new RectOffset(4, 4, 4, 4);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = Columns;
            gl.childAlignment = TextAnchor.UpperLeft;
            var fit = grid.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var barRt = Box(body, "BagScrollbar", x + w - 12, y, 12, viewH);
            UiKit.Img(barRt, whiteSprite, new Color(0.03f, 0.02f, 0.05f, 0.8f)).raycastTarget = true;
            var area = UiKit.Stretch(barRt, "Area");
            var handle = UiKit.Stretch(area, "Handle");
            var handleImg = UiKit.Img(handle, whiteSprite, new Color(1f, 0.86f, 0.45f, 0.75f));
            handleImg.raycastTarget = true;
            var bar = barRt.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = handleImg;
            bar.direction = Scrollbar.Direction.BottomToTop;

            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = grid;
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 45f;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            y += viewH + 12;
            var coin = Box(body, "Coin", x, y, 30, 30);
            var db = GameManager.I != null ? GameManager.I.db : null;
            var coinItem = db != null ? db.Item("gold") : null;
            var ci = UiKit.Img(coin, coinItem != null ? coinItem.icon : whiteSprite, coinItem != null ? Color.white : UiKit.Gold);
            ci.preserveAspect = true;
            goldText = Text(body, "Gold", "", 23, UiKit.Cream, TextAlignmentOptions.MidlineLeft, x + 38, y, 300, 30);
            Text(body, "BagHint", "Chuột phải: dùng · Bấm đồ: mặc", 16, UiKit.Muted, TextAlignmentOptions.MidlineRight, x + 220, y, w - 220, 30);
        }

        BagCell NewCell()
        {
            var c = new BagCell();
            var rt = UiKit.Rect(grid, "Cell", new Vector2(0, 1), Vector2.zero, new Vector2(Cell, Cell));
            c.go = rt.gameObject;
            UiKit.Img(rt, slotSprite != null ? slotSprite : whiteSprite, Color.white, slotSprite != null ? Image.Type.Sliced : Image.Type.Simple).raycastTarget = true;
            c.rarity = UiKit.Img(UiKit.Rect(rt, "Rarity", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Cell - 8, Cell - 8)), glowSprite != null ? glowSprite : whiteSprite, Color.clear);
            c.icon = UiKit.Img(UiKit.Rect(rt, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Cell - 20, Cell - 20)), null, Color.white);
            c.icon.preserveAspect = true;
            c.count = kit.Txt(rt, "Count", "", 19, Color.white, TextAlignmentOptions.BottomRight, new Vector2(0.5f, 0.5f), new Vector2(-4, 4), new Vector2(Cell - 8, Cell - 8));
            var hl = UiKit.Img(UiKit.Stretch(rt, "Highlight"), highlightSprite != null ? highlightSprite : whiteSprite,
                               highlightSprite != null ? Color.white : new Color(1, 1, 1, 0.1f), highlightSprite != null ? Image.Type.Sliced : Image.Type.Simple);
            hl.enabled = false;
            var hover = Hover(c.go, hl);
            hover.tip = () => CellTip(c);
            hover.click = b => ClickCell(c, b);
            cells.Add(c);
            return c;
        }

        // ================================================================== refresh
        void Refresh()
        {
            dirty = false;
            var me = Me;
            if (!built || me == null || me.stats == null) return;
            var s = me.stats;
            var st = s.Stats;
            var cls = s.Class;
            var race = s.Race;
            var bag = me.inventory;

            // header
            portrait.sprite = s.HasClass ? HeroArt.Portrait(s.look) : IdleFrame(me);
            string people = race != null ? race.displayName : "";
            identity.text = cls != null
                ? $"<color=#ffe07a>Cấp {s.level}</color>   {people} · {cls.displayName} <color=#9a93a8>(d{cls.hitDie})</color>"
                : $"<color=#ffe07a>Cấp {s.level}</color>   Chưa chọn lớp nhân vật";
            var w = s.Weapon;
            string metal = HeroLook.Metals[Mathf.Clamp(s.look.metal, 0, HeroLook.Metals.Length - 1)].name;
            subIdentity.text = cls != null
                ? $"Vũ khí: {w.name} +{s.look.upgrade} · {metal}   ·   Giáp của lớp: {ArmorName(cls.armor)}   ·   Phép dùng {CoreStats.Name(cls.casting)}"
                : "Bấm \"Ngoại hình\" để chọn chủng tộc và lớp.";
            xpText.text = s.IsMaxLevel ? "Cấp tối đa" : $"{s.xp} / {s.XpToNext} XP   ·   còn {s.XpToNext - s.xp} tới cấp {s.level + 1}";
            xpFill.fillAmount = s.IsMaxLevel ? 1f : Mathf.Clamp01(s.xp / (float)Mathf.Max(1, s.XpToNext));

            // ability scores
            int pb = Proficiency(s.level);
            for (int i = 0; i < CoreStats.Count; i++)
            {
                var a = CoreStats.SheetOrder[i];
                int score = s.Attribute(a);
                abilityMod[i].text = CoreStats.ModifierText(score);
                abilityScore[i].text = score.ToString();
                plus[i].gameObject.SetActive(s.statPoints > 0);
                bool prof = cls != null && cls.Saves(a);
                saveDots[i].color = prof ? UiKit.Gold : new Color(0.3f, 0.27f, 0.35f);
                int save = CoreStats.Modifier(score) + (prof ? pb : 0);
                saveValues[i].text = (save >= 0 ? "+" : "") + save;
                saveValues[i].color = prof ? UiKit.Gold : UiKit.Cream;
            }
            pointsText.text = s.statPoints > 0
                ? $"<color=#ffe07a>Còn {s.statPoints} điểm</color> · bấm <color=#8cf08c>+</color> để cộng"
                : $"Mỗi cấp +{s.Config.statPointsPerLevel} điểm";
            profText.text = $"Thành thạo <color=#ffe07a>+{pb}</color>";

            // combat numbers
            var c = s.Config;
            float armor = st.Get(StatId.Armor);
            string[] v =
            {
                $"{st.Get(StatId.PhysicalAttack):0.#}",
                $"{st.Get(StatId.MagicAttack):0.#}",
                $"{Mathf.Min(c.critMax, st.Get(StatId.CritChance)) * 100f:0.#}%",
                $"×{s.CritMultiplier:0.##}",
                $"+{st.Get(StatId.AttackSpeed) * 100f:0.#}%",
                $"{st.Get(StatId.CooldownReduction) * 100f:0.#}%",
                $"{Mathf.Min(c.resistMax, st.Get(StatId.ElementalResist)) * 100f:0.#}%",
                $"{c.ArmorReduction(armor, s.level) * 100f:0.#}%",
                $"×{s.HealingPower:0.##}",
                $"−{st.Get(StatId.DashCooldownReduction) * 100f:0.#}%",
                $"×{s.PoiseMultiplier:0.##}",
                $"+{s.GoldFind * 100f:0.#}%",
            };
            for (int i = 0; i < combatValues.Length; i++) combatValues[i].text = v[i];

            // the hero's summary
            var h = me.health;
            summaryValues[0].text = h != null ? $"{Mathf.Round(h.hp)}<size=60%><color=#9a93a8>/{Mathf.Round(h.maxHp)}</color></size>" : "";
            summaryValues[1].text = $"{armor:0.#}";
            summaryValues[2].text = $"{st.Get(w != null && w.focus ? StatId.MagicAttack : StatId.PhysicalAttack):0}";
            summaryValues[3].text = $"{Mathf.Round(me.maxEnergy)}";

            // traits
            var tb = new StringBuilder();
            if (race != null)
            {
                tb.Append($"<color=#ffe07a>{race.displayName}</color>");
                if (!string.IsNullOrEmpty(race.traits)) tb.Append("  ").Append(race.traits.Replace("\n", " · "));
            }
            if (cls != null)
            {
                if (tb.Length > 0) tb.Append("\n");
                tb.Append($"<color=#ffe07a>{cls.displayName}</color>  vũ khí: ");
                for (int i = 0; i < cls.weapons.Length; i++)
                {
                    var wk = WeaponKinds.Get(cls.weapons[i]);
                    if (i > 0) tb.Append(", ");
                    tb.Append(wk != null ? wk.name : cls.weapons[i]);
                }
                tb.Append($" · kháng {CoreStats.Name(cls.savingThrows[0])}, {CoreStats.Name(cls.savingThrows[Mathf.Min(1, cls.savingThrows.Length - 1)])}");
            }
            traitsText.text = tb.ToString();

            // gear
            foreach (var g in gear)
            {
                if (g.slot == EquipSlot.None)
                {
                    var icon = s.HasClass ? HeroArt.WeaponIcon(s.look) : null;
                    g.icon.sprite = icon != null ? icon : cls != null ? cls.emblem : null;
                    g.icon.enabled = g.icon.sprite != null;
                    g.ghost.enabled = false;
                    g.badge.text = s.look.upgrade > 0 ? "+" + s.look.upgrade : "";
                    continue;
                }
                var worn = bag != null ? bag.Worn(g.slot) : null;
                g.icon.sprite = worn != null ? worn.icon : null;
                g.icon.enabled = worn != null;
                g.ghost.sprite = GhostOf(g.slot);
                g.ghost.enabled = worn == null && g.ghost.sprite != null;
                g.badge.text = "";
            }

            // the bag
            RefreshBag(bag);
            for (int i = 0; i < filterButtons.Length; i++)
                filterButtons[i].GetComponentInChildren<TextMeshProUGUI>().color = i == filter ? UiKit.Gold : UiKit.Cream;
        }

        void RefreshBag(Inventory bag)
        {
            if (bag == null) return;
            var shown = new List<Inventory.Stack>();
            foreach (var st in bag.stacks)
                if (st != null && st.item != null && Passes(st.item)) shown.Add(st);
            // every slot of the bag in "Tất cả" (the empty ones too); only the matching items otherwise, rounded up to a row
            int n = filter == 0 ? Mathf.Max(bag.capacity, shown.Count) : Mathf.Max(Columns * 4, Mathf.CeilToInt(shown.Count / (float)Columns) * Columns);
            while (cells.Count < n) NewCell();
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                c.go.SetActive(i < n);
                var st = i < shown.Count ? shown[i] : null;
                c.stack = st;
                bool has = st != null;
                c.icon.enabled = has;
                if (has) c.icon.sprite = st.item.icon;
                c.count.text = has && st.count > 1 ? st.count.ToString() : "";
                c.rarity.enabled = has && st.item.rarity > ItemRarity.Common;
                if (has) c.rarity.color = st.item.RarityColor.WithAlpha(0.35f);
            }
            countText.text = $"{bag.stacks.Count}/{bag.capacity} ô";
            countText.color = bag.stacks.Count >= bag.capacity ? UiKit.Bad : UiKit.Muted;
            goldText.text = $"<color=#ffd84a>{bag.gold}</color> vàng";
        }

        bool Passes(ItemDef it)
        {
            switch (filter)
            {
                case 1: return it.IsGear || it.kind == ItemKind.Equipment;
                case 2: return it.kind == ItemKind.Consumable || it.kind == ItemKind.Tome;
                case 3: return it.kind == ItemKind.Material || it.kind == ItemKind.Quest;
                default: return true;
            }
        }

        void AnimateHero()
        {
            var me = Me;
            var set = me != null && me.anim != null ? me.anim.set : null;
            if (set == null || hero == null) return;
            var idle = set.Get("idle_down");
            var attack = set.Get("attack_down");
            animT += Time.unscaledDeltaTime;
            float cycle = 4f;
            float t = animT % cycle;
            if (attack != null && attack.frames.Length > 0 && t > cycle - 0.5f)
            {
                int f = Mathf.Min(attack.frames.Length - 1, Mathf.FloorToInt((t - (cycle - 0.5f)) * attack.frames.Length / 0.5f));
                hero.sprite = attack.frames[f];
            }
            else if (idle != null && idle.frames.Length > 0) hero.sprite = idle.frames[Mathf.FloorToInt(t * Mathf.Max(1f, idle.fps)) % idle.frames.Length];
        }

        static Sprite IdleFrame(PlayerController me)
        {
            var set = me != null && me.anim != null ? me.anim.set : null;
            var clip = set != null ? set.Get("idle_down") : null;
            return clip != null && clip.frames.Length > 0 ? clip.frames[0] : null;
        }

        /// <summary>A dim picture of what a slot takes (the first piece of that slot in the database).</summary>
        static Sprite GhostOf(EquipSlot slot)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db == null) return null;
            foreach (var it in db.items)
                if (it != null && it.slot == slot && it.icon != null) return it.icon;
            return null;
        }

        /// <summary>D&amp;D's proficiency bonus: +2 at level 1, one more every four levels.</summary>
        public static int Proficiency(int level) => 2 + Mathf.Max(0, level - 1) / 4;

        static string ArmorName(ArmorKind k)
        {
            switch (k)
            {
                case ArmorKind.Light: return "nhẹ";
                case ArmorKind.Medium: return "vừa";
                case ArmorKind.Heavy: return "nặng";
                case ArmorKind.Unarmored: return "không giáp (thân thể)";
                default: return "áo choàng";
            }
        }

        // ================================================================== tooltips and clicks
        (string, string, Color) AbilityTip(int i)
        {
            var s = Me != null ? Me.stats : null;
            if (s == null) return (null, null, Color.white);
            var a = CoreStats.SheetOrder[i];
            int score = s.Attribute(a), spent = s.Allocated(a), baseScore = s.BaseScore(a);
            int gearBonus = score - baseScore - spent;
            var sb = new StringBuilder();
            sb.Append(AbilityUse[i]);
            sb.Append($"\n\nĐiểm: <b>{score}</b>  (hệ số {CoreStats.ModifierText(score)})");
            sb.Append($"\n<color=#b8b0c8><size=85%>Gốc {baseScore} (lớp + chủng tộc)");
            if (spent > 0) sb.Append($" · đã cộng +{spent}");
            if (gearBonus != 0) sb.Append($" · trang bị {(gearBonus > 0 ? "+" : "")}{gearBonus}");
            sb.Append("</size></color>");
            if (s.statPoints > 0) sb.Append("\n\n<color=#9ad06a>Bấm + để cộng 1 điểm.</color>");
            return ($"{CoreStats.Name(a)} ({CoreStats.Short(a)})", sb.ToString(), UiKit.Gold);
        }

        (string, string, Color) SaveTip(int i)
        {
            var s = Me != null ? Me.stats : null;
            if (s == null) return (null, null, Color.white);
            var a = CoreStats.SheetOrder[i];
            var cls = s.Class;
            bool prof = cls != null && cls.Saves(a);
            string body = prof
                ? $"Lớp {cls.displayName} thành thạo kháng này: cộng thêm +{Proficiency(s.level)}."
                : "Lớp nhân vật không thành thạo kháng này.";
            if (prof && a == CoreStat.Wisdom) body += "\nChoáng, trói, làm chậm ngắn đi 20%.";
            if (prof && a == CoreStat.Constitution) body += "\nĐộc ngắn đi 25%.";
            return ($"Kháng {CoreStats.Name(a)}", body, UiKit.Gold);
        }

        (string, string, Color) GearTip(GearSlot g)
        {
            var me = Me;
            if (me == null || me.stats == null) return (null, null, Color.white);
            if (g.slot == EquipSlot.None)
            {
                var s = me.stats;
                var w = s.Weapon;
                string metal = HeroLook.Metals[Mathf.Clamp(s.look.metal, 0, HeroLook.Metals.Length - 1)].name;
                float atk = s.Stats.Get(w.focus ? StatId.MagicAttack : StatId.PhysicalAttack);
                string body = $"<color=#b8b0c8><size=85%>Vũ khí · {metal}</size></color>\n{w.description}\n\n" +
                              $"Công của vũ khí: <b>{WeaponKinds.AttackOf(w, s.look.upgrade):0.#}</b> (+{s.look.upgrade * WeaponKinds.AttackPerUpgrade * 100f:0}% từ rèn)\n" +
                              $"{(w.focus ? "Công phép" : "Công vật lý")}: <b>{atk:0.#}</b>\n\n<color=#9ad06a>Rèn, đổi kim loại và đổi vũ khí ở Lò Rèn (Thợ Rèn, làng Lá Xanh).</color>";
                return ($"{w.name} +{s.look.upgrade}", body, UiKit.Gold);
            }
            var worn = me.inventory != null ? me.inventory.Worn(g.slot) : null;
            if (worn == null)
                return ($"{Gear.SlotName(g.slot)} (trống)", "Chưa mặc gì.\n\n<color=#9ad06a>Bấm vào món trang bị trong túi để mặc. Thợ Rèn chế tạo trang bị từ đồ quái rơi (Lò Rèn, tab Chế Tạo).</color>", UiKit.Muted);
            return ItemTips.For(worn, me, "Bấm để tháo ra (cất vào túi).");
        }

        (string, string, Color) CellTip(BagCell c)
        {
            if (c.stack == null || c.stack.item == null) return (null, null, Color.white);
            var it = c.stack.item;
            string hint = it.IsGear ? "Bấm để mặc vào." : it.kind == ItemKind.Consumable ? "Chuột phải để dùng." : null;
            return ItemTips.For(it, Me, hint, c.stack.count);
        }

        void ClickCell(BagCell c, PointerEventData.InputButton b)
        {
            var me = Me;
            if (me == null || c.stack == null || c.stack.item == null) return;
            var it = c.stack.item;
            if (it.IsGear)
            {
                me.inventory.AskEquip(it);
                return;
            }
            if ((it.kind == ItemKind.Consumable || it.kind == ItemKind.Tome) && b == PointerEventData.InputButton.Right) me.UseItem(it);
        }
    }
}

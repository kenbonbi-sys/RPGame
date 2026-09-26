using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// Sách Chiêu (K, <see cref="Spellbook"/>): the hero's skill bar on top — W E R A S D, each with
    /// the skill on it and whether it is the class's own or a learned spell — and below it the
    /// three schools, six spells each, saying which are learned, which are on the bar, which the
    /// class cannot learn and which boss keeps the book. Click a learned spell, then a slot that
    /// takes it (they light up); right-click a slot to give it back to the class. Changing the
    /// bar waits until the hero is out of a fight, which the window says.
    /// </summary>
    public class SpellbookUI : UIPanel
    {
        public TMP_FontAsset font;
        public Material fontOutline;
        public Sprite windowSprite, buttonSprite, whiteSprite;
        [Tooltip("Optional: the cells, the picked frame (the HUD's sprites).")]
        public Sprite slotSprite, highlightSprite;

        public static SpellbookUI I { get; private set; }

        const float W = 1500f, H = 880f;
        const float BarY = 150f, Cell = 104f, CellGap = 30f, ColY = 404f, ColGap = 20f, Side = 40f;

        UiKit kit;
        RectTransform body;
        TextMeshProUGUI classText, statusText;
        readonly Slot[] bar = new Slot[Spellbook.UltimateSlot + 1];
        readonly List<Row> rows = new List<Row>();
        string picked;
        bool built, dirty;
        float pulse;
        PlayerController watched;

        class Slot
        {
            public int index;
            public Image icon, frame, glow;
            public TextMeshProUGUI key, name, sub;
        }

        class Row
        {
            public Spellbook.Entry entry;
            public Image bg, icon, frame;
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

        public override void Show()
        {
            var p = Players.Local;
            if (p == null || p.stats == null) return;
            if (!p.stats.HasClass)
            {
                Notify.WorldText(p, "Hãy chọn lớp nhân vật trước (bảng Nhân Vật, C).", p.health.HeadPosition + Vector3.up * 0.4f, UiKit.Bad);
                return;
            }
            if (!built) Build();
            base.Show();
            watched = p;
            p.stats.LookChanged += MarkDirty;
            picked = null;
            Refresh();
        }

        public override void Close()
        {
            if (watched != null)
            {
                watched.stats.LookChanged -= MarkDirty;
                watched = null;
            }
            picked = null;
            base.Close();
        }

        void MarkDirty() => dirty = true;

        /// <summary>Picks a spell as a click on its row would (for automated tours and tests).</summary>
        public void DebugPick(string id)
        {
            picked = id;
            Refresh();
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;
            if (Players.Local == null || Players.Local != watched)
            {
                Close();
                return;
            }
            Fit();
            if (dirty)
            {
                dirty = false;
                Refresh();
            }
            pulse += Time.unscaledDeltaTime;
            float glow = 0.35f + 0.3f * Mathf.Sin(pulse * 6f);
            for (int i = Spellbook.FirstSlot; i <= Spellbook.UltimateSlot; i++)
                if (bar[i] != null && bar[i].glow.enabled) bar[i].glow.color = new Color(1f, 0.86f, 0.45f, glow);
            ShowStatus();
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

        Image Panel(RectTransform rt, Color tint) =>
            UiKit.Img(rt, buttonSprite != null ? buttonSprite : whiteSprite, tint, buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple);

        Image CellImage(RectTransform rt) =>
            UiKit.Img(rt, slotSprite != null ? slotSprite : whiteSprite, slotSprite != null ? Color.white : new Color(0.1f, 0.08f, 0.12f),
                      slotSprite != null ? Image.Type.Sliced : Image.Type.Simple);

        Image Highlight(RectTransform rt, Color c)
        {
            var img = UiKit.Img(UiKit.Stretch(rt, "Frame"), highlightSprite != null ? highlightSprite : whiteSprite,
                                highlightSprite != null ? c : new Color(c.r, c.g, c.b, 0.25f), highlightSprite != null ? Image.Type.Sliced : Image.Type.Simple);
            img.enabled = false;
            return img;
        }

        void Heading(string text, Color color, float x, float y, float w)
        {
            var t = Text(body, "H_" + text, text, 19, color, TextAlignmentOptions.BottomLeft, x, y, w, 26);
            t.characterSpacing = 4f;
            t.fontStyle = FontStyles.Bold;
            UiKit.Img(Box(body, "Rule_" + text, x, y + 28, w, 2), whiteSprite, new Color(color.r, color.g, color.b, 0.3f));
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
            var t = kit.Txt(body, "Title", "Sách Chiêu", 40, UiKit.Gold, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(900, 50));
            t.fontStyle = FontStyles.Bold;
            classText = kit.Txt(body, "Sub", "", 18, UiKit.Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(1200, 26), false);
            kit.Btn(body, "Close", "✗", new Vector2(1, 1), new Vector2(-58, -48), new Vector2(52, 48), 26, Close);

            // the bar: W E R A S D
            Heading("THANH KỸ NĂNG", UiKit.Gold, Side, BarY - 44, W - 2 * Side);
            int n = Spellbook.UltimateSlot - Spellbook.FirstSlot + 1;
            float x0 = (W - (n * Cell + (n - 1) * CellGap)) / 2f;
            for (int i = Spellbook.FirstSlot; i <= Spellbook.UltimateSlot; i++)
            {
                var s = new Slot { index = i };
                float x = x0 + (i - Spellbook.FirstSlot) * (Cell + CellGap);
                var cell = Box(body, "Slot" + i, x, BarY, Cell, Cell);
                CellImage(cell).raycastTarget = true;
                s.glow = Highlight(cell, UiKit.Gold);
                s.icon = UiKit.Img(UiKit.Rect(cell, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Cell - 26, Cell - 26)), null, Color.white);
                s.icon.preserveAspect = true;
                s.frame = Highlight(cell, Color.white);
                s.key = kit.Txt(cell, "Key", "", 20, UiKit.Gold, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(26, -18), new Vector2(40, 28));
                s.key.fontStyle = FontStyles.Bold;
                s.name = Text(body, "Name" + i, "", 17, UiKit.Cream, TextAlignmentOptions.Top, x - CellGap / 2, BarY + Cell + 6, Cell + CellGap, 24);
                s.name.textWrappingMode = TextWrappingModes.NoWrap;
                s.name.overflowMode = TextOverflowModes.Ellipsis;
                s.sub = Text(body, "Sub" + i, "", 14, UiKit.Muted, TextAlignmentOptions.Top, x - CellGap / 2, BarY + Cell + 30, Cell + CellGap, 20);
                var hover = cell.gameObject.AddComponent<UiHover>();
                int k = i;
                hover.tip = () => SlotTip(k);
                hover.click = b => ClickSlot(k, b);
                bar[i] = s;
            }
            statusText = Text(body, "Status", "", 19, UiKit.Muted, TextAlignmentOptions.Center, Side, BarY + Cell + 62, W - 2 * Side, 30);

            // the three schools
            float colW = (W - 2 * Side - 2 * ColGap) / 3f;
            var schools = new[] { Spellbook.School.Ice, Spellbook.School.Lightning, Spellbook.School.Dark };
            for (int c = 0; c < schools.Length; c++)
            {
                float cx = Side + c * (colW + ColGap);
                var sc = schools[c];
                Heading("HỆ " + Spellbook.SchoolName(sc).ToUpperInvariant(), Spellbook.SchoolColor(sc), cx, ColY - 36, colW);
                int r = 0;
                foreach (var e in Spellbook.All)
                    if (e.school == sc) rows.Add(MakeRow(e, cx, ColY + r++ * 68f, colW));
            }
            Text(body, "Hint", "Bấm một chiêu đã học rồi bấm ô sáng trên thanh để đặt  ·  Chuột phải một ô để trả về chiêu của lớp  ·  " +
                 "Bí Kíp rơi từ boss các vùng, chuột phải trong túi để học  ·  K hoặc Esc để đóng",
                 16, UiKit.Muted, TextAlignmentOptions.Center, Side, H - 58, W - 2 * Side, 40);
        }

        Row MakeRow(Spellbook.Entry e, float x, float y, float w)
        {
            var r = new Row { entry = e };
            var rt = Box(body, "Row_" + e.id, x, y, w, 62);
            r.bg = Panel(rt, new Color(0.5f, 0.46f, 0.56f));
            r.bg.raycastTarget = true;
            var cell = UiKit.Rect(rt, "Cell", new Vector2(0, 0.5f), new Vector2(34, 0), new Vector2(50, 50));
            CellImage(cell);
            r.icon = UiKit.Img(UiKit.Rect(cell, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40)), null, Color.white);
            r.icon.preserveAspect = true;
            r.name = kit.Txt(rt, "Name", "", 20, UiKit.Cream, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f), new Vector2(70 + 105, 11), new Vector2(210, 26), false);
            r.name.textWrappingMode = TextWrappingModes.NoWrap;
            r.name.overflowMode = TextOverflowModes.Ellipsis;
            r.sub = kit.Txt(rt, "Sub", "", 14, UiKit.Muted, TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f), new Vector2(70 + 115, -13), new Vector2(230, 22), false);
            r.sub.textWrappingMode = TextWrappingModes.NoWrap;
            r.sub.overflowMode = TextOverflowModes.Ellipsis;
            r.badge = kit.Txt(rt, "Badge", "", 16, UiKit.Good, TextAlignmentOptions.MidlineRight, new Vector2(1, 0.5f), new Vector2(-64, 0), new Vector2(112, 40), false);
            r.frame = Highlight(rt, Spellbook.SchoolColor(e.school));
            var hover = rt.gameObject.AddComponent<UiHover>();
            hover.tip = () => SpellTip(e);
            hover.click = b => ClickSpell(e, b);
            return r;
        }

        // ================================================================== state
        static AbilityDef Ability(string id)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            return db != null && !string.IsNullOrEmpty(id) ? db.Ability(id) : null;
        }

        /// <summary>The class's own skill of a slot (what a right-click gives back).</summary>
        static AbilityDef ClassSkill(PlayerController p, int slot)
        {
            var kitIds = p.stats.Class.kit;
            return kitIds != null && slot < kitIds.Length ? Ability(kitIds[slot]) : null;
        }

        void Refresh()
        {
            var p = Players.Local;
            if (p == null || p.stats == null || !p.stats.HasClass || body == null) return;
            var look = p.stats.look;
            if (picked != null && !CanPick(p, Spellbook.Find(picked))) picked = null;
            classText.text = $"{p.stats.Class.displayName}  ·  đã học {Learned(look)}/{Spellbook.All.Length} chiêu hệ Băng, Lôi, Ám";
            for (int i = Spellbook.FirstSlot; i <= Spellbook.UltimateSlot; i++)
            {
                var s = bar[i];
                var a = p.skills != null ? p.skills.slots[i] : null;
                var spell = Spellbook.BarAbility(look, i);
                s.icon.sprite = a != null ? a.icon : null;
                s.icon.enabled = a != null && a.icon != null;
                s.key.text = InputReader.SkillLabel(i);
                s.name.text = a != null ? a.displayName : "—";
                var e = spell != null ? Spellbook.Find(spell.id) : null;
                s.sub.text = e != null ? "Hệ " + Spellbook.SchoolName(e.school) : i == Spellbook.UltimateSlot ? "Tuyệt kỹ của lớp" : "Chiêu của lớp";
                s.sub.color = e != null ? Spellbook.SchoolColor(e.school) : UiKit.Muted;
                s.frame.enabled = e != null;
                if (e != null) s.frame.color = Spellbook.SchoolColor(e.school);
                s.glow.enabled = picked != null && Spellbook.Fits(picked, i) && Spellbook.OnBar(look, i) != picked;
            }
            foreach (var r in rows)
            {
                var e = r.entry;
                var a = Ability(e.id);
                bool knows = Spellbook.Knows(look, e.id);
                bool may = Spellbook.ClassMay(look.cls, e);
                int on = SlotOf(look, e.id);
                r.icon.sprite = a != null ? a.icon : null;
                r.icon.enabled = r.icon.sprite != null;
                r.icon.color = knows && may ? Color.white : new Color(0.4f, 0.38f, 0.45f, 0.8f);
                r.name.text = a != null ? a.displayName : e.id;
                r.name.color = knows && may ? UiKit.Cream : UiKit.Muted;
                bool ult = a != null && a.HasTag(AbilityTags.Ultimate);
                r.sub.text = (ult ? "<color=#ffe07a>Tuyệt kỹ</color> · " : "") + "Bí Kíp: " + e.from;
                if (!may) { r.badge.text = "Lớp khác"; r.badge.color = new Color(0.62f, 0.5f, 0.5f); }
                else if (on > 0) { r.badge.text = "Ô " + InputReader.SkillLabel(on); r.badge.color = UiKit.Gold; }
                else if (knows) { r.badge.text = "Đã học"; r.badge.color = UiKit.Good; }
                else { r.badge.text = "Chưa học"; r.badge.color = UiKit.Muted; }
                r.frame.enabled = picked == e.id;
                r.bg.color = picked == e.id ? new Color(0.72f, 0.66f, 0.8f) : knows && may ? new Color(0.55f, 0.5f, 0.62f) : new Color(0.36f, 0.33f, 0.4f);
            }
        }

        static int Learned(HeroLook look)
        {
            int n = 0;
            foreach (var e in Spellbook.All)
                if (Spellbook.Knows(look, e.id)) n++;
            return n;
        }

        static int SlotOf(HeroLook look, string id)
        {
            for (int i = Spellbook.FirstSlot; i <= Spellbook.UltimateSlot; i++)
                if (Spellbook.OnBar(look, i) == id) return i;
            return 0;
        }

        static bool CanPick(PlayerController p, Spellbook.Entry e) =>
            e != null && Spellbook.Knows(p.stats.look, e.id) && Spellbook.ClassMay(p.stats.look.cls, e);

        void ShowStatus()
        {
            var p = Players.Local;
            if (p == null || statusText == null) return;
            if (Spellbook.InCombat(p))
            {
                statusText.text = "Đang giao chiến: ra khỏi trận rồi hãy đổi chiêu.";
                statusText.color = UiKit.Bad;
            }
            else if (picked != null)
            {
                var a = Ability(picked);
                string name = a != null ? a.displayName : picked;
                statusText.text = Spellbook.IsUltimate(picked)
                    ? $"Bấm ô {InputReader.SkillLabel(Spellbook.UltimateSlot)} để đặt Tuyệt kỹ {name}."
                    : $"Bấm một ô sáng ({Keys()}) để đặt {name}.";
                statusText.color = UiKit.Gold;
            }
            else if (Learned(p.stats.look) == 0)
            {
                statusText.text = "Chưa học chiêu nào: Bí Kíp rơi từ boss các vùng (Cóc Tía, Xà Mẫu, Golem Pha Lê Cổ...).";
                statusText.color = UiKit.Muted;
            }
            else
            {
                statusText.text = "Bấm một chiêu đã học để đặt lên thanh.";
                statusText.color = UiKit.Muted;
            }
        }

        static string Keys()
        {
            var keys = new List<string>();
            for (int i = Spellbook.FirstSlot; i < Spellbook.UltimateSlot; i++) keys.Add(InputReader.SkillLabel(i));
            return string.Join(" ", keys);
        }

        // ================================================================== clicks
        void ClickSpell(Spellbook.Entry e, PointerEventData.InputButton b)
        {
            var p = Players.Local;
            if (p == null) return;
            int on = SlotOf(p.stats.look, e.id);
            if (b == PointerEventData.InputButton.Right)
            {
                if (on > 0) Put(on, "");
                return;
            }
            if (!CanPick(p, e))
            {
                string why = Spellbook.WhyNotLearn(p, e);
                if (Spellbook.Knows(p.stats.look, e.id)) why = "Lớp này không dùng được chiêu này.";
                else if (why == null) why = $"Chưa học: tìm Bí Kíp {Ability(e.id)?.displayName} ở {e.from}.";
                Say(why);
                return;
            }
            AudioManager.Play("sfx_ui_click", 0.5f);
            picked = picked == e.id ? null : e.id;
            Refresh();
        }

        void ClickSlot(int slot, PointerEventData.InputButton b)
        {
            var p = Players.Local;
            if (p == null) return;
            if (b == PointerEventData.InputButton.Right)
            {
                if (Spellbook.OnBar(p.stats.look, slot).Length > 0) Put(slot, "");
                return;
            }
            if (picked == null)
            {
                // a slot holding a spell picks it up, to move it elsewhere
                string on = Spellbook.OnBar(p.stats.look, slot);
                if (on.Length > 0 && CanPick(p, Spellbook.Find(on)))
                {
                    AudioManager.Play("sfx_ui_click", 0.5f);
                    picked = on;
                    Refresh();
                }
                return;
            }
            if (!Spellbook.Fits(picked, slot))
            {
                Say(Spellbook.IsUltimate(picked) ? "Tuyệt kỹ chỉ đặt được vào ô D." : "Ô D chỉ dành cho Tuyệt kỹ.");
                return;
            }
            string id = picked;
            picked = null;
            Put(slot, id);
        }

        /// <summary>Asks for the change (checked here first so a refusal shows at once).</summary>
        void Put(int slot, string id)
        {
            var p = Players.Local;
            string why = Spellbook.WhyNotSet(p, slot, id);
            if (why != null)
            {
                Say(why);
                return;
            }
            Spellbook.Ask(slot, id);
            Refresh();
        }

        void Say(string why)
        {
            AudioManager.Play("sfx_ui_close", 0.4f);
            statusText.text = why;
            statusText.color = UiKit.Bad;
        }

        // ================================================================== tooltips
        (string, string, Color) SlotTip(int slot)
        {
            var p = Players.Local;
            var a = p != null && p.skills != null ? p.skills.slots[slot] : null;
            if (a == null) return (null, null, Color.white);
            string body = a.Tooltip();
            if (Spellbook.OnBar(p.stats.look, slot).Length > 0)
            {
                var own = ClassSkill(p, slot);
                body += $"\n<color=#b8b0c8><size=85%>Chuột phải: trả về {(own != null ? own.displayName : "chiêu của lớp")}.</size></color>";
            }
            return ($"{a.displayName}  <color=#b8b0c8><size=80%>[{InputReader.SkillLabel(slot)}]</size></color>", body, new Color(1f, 0.88f, 0.55f));
        }

        (string, string, Color) SpellTip(Spellbook.Entry e)
        {
            var a = Ability(e.id);
            if (a == null) return (null, null, Color.white);
            var p = Players.Local;
            string body = a.Tooltip() + $"\n<color=#b8b0c8><size=85%>Hệ {Spellbook.SchoolName(e.school)} · Bí Kíp rơi từ {e.from}\nLớp học được: {Spellbook.ClassNames(e)}</size></color>";
            if (p != null && !Spellbook.ClassMay(p.stats.look.cls, e)) body += $"\n<color=#ff8c73>Lớp {p.stats.Class.displayName} không học được.</color>";
            return (a.displayName, body, Spellbook.SchoolColor(e.school));
        }
    }
}

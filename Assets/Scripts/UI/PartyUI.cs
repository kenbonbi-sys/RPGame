using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// Tổ đội on screen (online phase 4, Docs/KeHoach-Online.md): the members top left with their
    /// level and health (the leader marked ◆, a fallen member greyed), and an invitation with its
    /// answer (Y / N, or the buttons). Made at run time on the HUD, in the HUD's own font, when
    /// playing online; it only shows what <see cref="PartyState"/> holds.
    /// </summary>
    public class PartyUI : MonoBehaviour
    {
        static readonly Color Gold = new Color(1f, 0.86f, 0.45f);
        static readonly Color Cream = new Color(0.96f, 0.93f, 0.86f);
        static readonly Color Muted = new Color(0.72f, 0.68f, 0.78f);
        static readonly Color Plate = new Color(0.07f, 0.06f, 0.1f, 0.72f);
        static readonly Color HpRed = new Color(0.86f, 0.16f, 0.2f);
        static readonly Color Fallen = new Color(0.45f, 0.43f, 0.5f);

        const float RowHeight = 56f;

        class Row
        {
            public GameObject go;
            public TextMeshProUGUI name, level;
            public RectTransform fill;
            public Image fillImage;
        }

        TMP_FontAsset font;
        Material material;
        RectTransform panel;
        readonly Row[] rows = new Row[Parties.MaxMembers];
        GameObject prompt;
        TextMeshProUGUI promptText;
        float nextRefresh;

        /// <summary>The panel of this HUD, made on first use.</summary>
        public static PartyUI Ensure(HUD hud)
        {
            if (hud == null || hud.canvas == null) return null;
            var ui = hud.GetComponentInChildren<PartyUI>(true);
            if (ui != null) return ui;
            var go = new GameObject("Party", typeof(RectTransform));
            go.transform.SetParent(hud.canvas.transform, false);
            ui = go.AddComponent<PartyUI>();
            ui.Build(hud);
            return ui;
        }

        void OnEnable() => PartyState.Changed += Refresh;
        void OnDisable() => PartyState.Changed -= Refresh;

        // ================================================================== showing
        void Update()
        {
            if (Time.unscaledTime >= nextRefresh) Refresh();
            bool invited = PartyState.InvitedBy != null;
            if (prompt.activeSelf != invited)
            {
                prompt.SetActive(invited);
                if (invited) promptText.text = $"<color=#ffe07a>{PartyState.InvitedBy}</color> mời bạn vào tổ đội.";
            }
            if (!invited) return;
            var kb = Keyboard.current;
            var gm = GameManager.I;
            bool typing = ChatInput.I != null && ChatInput.I.IsOpen || DebugConsole.I != null && DebugConsole.I.IsOpen;
            if (kb == null || typing || gm == null || gm.State != GameState.Playing) return;
            if (kb.yKey.wasPressedThisFrame) PartyState.Answer(true);
            else if (kb.nKey.wasPressedThisFrame) PartyState.Answer(false);
        }

        void Refresh()
        {
            nextRefresh = Time.unscaledTime + 0.2f;
            if (panel == null) return;
            var names = PartyState.Names;
            panel.gameObject.SetActive(names.Length > 0);
            if (names.Length == 0) return;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, 40f + RowHeight * names.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                bool on = i < names.Length;
                r.go.SetActive(on);
                if (!on) continue;
                var hero = PartyState.HeroOf(i);
                bool down = hero != null && hero.IsDead;
                r.name.text = (PartyState.IsLeader(names[i]) ? "<color=#ffd24a>◆</color> " : "") + names[i];
                r.name.color = down ? Fallen : Cream;
                r.level.text = hero != null ? $"Cấp {hero.Level}" : "";
                float hp = hero != null && hero.health != null ? hero.health.Fraction : 0f;
                r.fill.anchorMax = new Vector2(Mathf.Clamp01(hp), 1f);
                r.fillImage.color = down ? Fallen : HpRed;
            }
        }

        // ================================================================== widgets
        void Build(HUD hud)
        {
            var sample = hud.quest != null ? hud.quest.text : hud.GetComponentInChildren<TextMeshProUGUI>(true);
            if (sample != null)
            {
                font = sample.font;
                material = sample.fontSharedMaterial;
            }
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            // under the key hints of the top-left corner
            panel = Rect(root, "Members", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -52f), new Vector2(320f, 40f));
            Img(Stretch(panel, "Plate"), Plate);
            Txt(panel, "Title", "Tổ đội", 20, Muted, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -4f), new Vector2(200f, 30f));
            for (int i = 0; i < rows.Length; i++)
            {
                var row = Rect(panel, "Member" + (i + 1), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -36f - i * RowHeight), new Vector2(296f, RowHeight - 4f));
                var r = new Row { go = row.gameObject };
                // boxes taller than a line: an ellipsis box shows nothing when its line does not fit
                r.name = Txt(row, "Name", "", 22, Cream, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(210f, 32f));
                r.level = Txt(row, "Level", "", 18, Muted, TextAlignmentOptions.MidlineRight, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(84f, 32f));
                var track = Rect(row, "Track", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 5f), new Vector2(296f, 11f));
                Img(track, new Color(0.08f, 0.04f, 0.07f, 0.95f));
                r.fill = Stretch(track, "Fill");
                r.fill.offsetMin = new Vector2(1f, 1f);
                r.fill.offsetMax = new Vector2(-1f, -1f);
                r.fillImage = Img(r.fill, HpRed);
                rows[i] = r;
            }
            panel.gameObject.SetActive(false);

            var p = Rect(root, "Invitation", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(600f, 130f));
            prompt = p.gameObject;
            Img(Stretch(p, "Plate"), new Color(0.07f, 0.06f, 0.1f, 0.9f)).raycastTarget = true;
            promptText = Txt(p, "Text", "", 25, Cream, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(560f, 40f));
            Btn(p, "Yes", "Vào tổ đội (Y)", new Vector2(-130f, 30f), () => PartyState.Answer(true)).color = Gold;
            Btn(p, "No", "Từ chối (N)", new Vector2(130f, 30f), () => PartyState.Answer(false));
            prompt.SetActive(false);
        }

        static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform Stretch(Transform parent, string name)
        {
            var rt = Rect(parent, name, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static Image Img(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        TextMeshProUGUI Txt(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align,
                            Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box)
        {
            var rt = Rect(parent, name, anchor, pivot, pos, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            if (material != null) t.fontSharedMaterial = material;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.richText = true;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>A button; returns its label.</summary>
        TextMeshProUGUI Btn(Transform parent, string name, string label, Vector2 pos, Action onClick)
        {
            var rt = Rect(parent, name, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), pos, new Vector2(230f, 46f));
            var img = Img(rt, new Color(0.24f, 0.2f, 0.3f, 1f));
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f);
            b.colors = colors;
            b.onClick.AddListener(() =>
            {
                AudioManager.Play("sfx_ui_click", 0.6f);
                onClick();
            });
            var t = Txt(rt, "Label", label, 22, Cream, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230f, 46f));
            return t;
        }
    }
}

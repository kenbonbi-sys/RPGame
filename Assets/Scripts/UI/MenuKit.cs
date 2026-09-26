using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// Widgets of the green-and-gold menus (the title screen and the character creator): cards
    /// with rounded corners, gold and dark buttons, the gold frame, section titles, dividers,
    /// badges and key caps, drawn with <see cref="MenuArt"/>. Positions are in the 1920×1080
    /// layout, measured from the top-left corner of the parent (y grows downwards).
    /// </summary>
    public sealed class MenuKit
    {
        public readonly TMP_FontAsset font;
        readonly Material shadowed;

        public static readonly Color Page = C("#0b1211");
        public static readonly Color Band = C("#0d1615");
        public static readonly Color Panel = C("#0d1816");
        public static readonly Color CardFill = C("#111b19");
        public static readonly Color CardLine = C("#25332f");
        public static readonly Color CardChosen = C("#241e12");
        public static readonly Color Gold = C("#e3b75a");
        public static readonly Color GoldText = C("#f3cf72");
        public static readonly Color GoldDim = C("#8c6d32");
        public static readonly Color ButtonGold = C("#f5c352");
        public static readonly Color Ink = C("#2a1a06");
        public static readonly Color Cream = C("#eee7d5");
        public static readonly Color Muted = C("#8e9993");
        public static readonly Color Faint = C("#56625d");
        public static readonly Color Green = C("#6eeb9a");
        public static readonly Color Red = C("#ef6b5f");
        public static readonly Color Easy = C("#62c46f");
        public static readonly Color Average = C("#e2b64a");
        public static readonly Color Hard = C("#e2625a");

        /// <param name="shadowed">The font's material with an outline and a shadow, for words over a picture.</param>
        public MenuKit(TMP_FontAsset font, Material shadowed)
        {
            this.font = font;
            this.shadowed = shadowed;
        }

        public static Color C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        /// <summary>"#rrggbb", for rich text.</summary>
        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // ================================================================== boxes and pictures
        /// <summary>A box at (x, y) from its parent's top-left corner, w×h.</summary>
        public static RectTransform Box(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Fills its parent, less an inset on every side.</summary>
        public static RectTransform Fill(Transform parent, string name, float inset = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Img(RectTransform rt, Sprite sprite, Color color, bool sliced = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sliced && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A plain rectangle of colour.</summary>
        public static Image Solid(RectTransform rt, Color color) => Img(rt, null, color);

        /// <summary>One of <see cref="MenuArt"/>'s pictures on a box (sliced when it is a frame or a face).</summary>
        public static Image Art(RectTransform rt, string picture, Color color) => Img(rt, MenuArt.Get(picture), color, MenuArt.Sliced(picture));

        /// <summary>Puts a box's pivot in its middle without moving it (so it can be flipped or turned in place).</summary>
        public static RectTransform CenterPivot(RectTransform rt)
        {
            var size = rt.sizeDelta;
            var pos = rt.anchoredPosition;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos + new Vector2(size.x / 2f, -size.y / 2f);
            return rt;
        }

        /// <summary>A picture of <see cref="MenuArt"/> centred on (cx, cy), fitted into w×h (its pivot in the middle).</summary>
        public static Image Icon(Transform parent, string picture, float cx, float cy, float w, float h, Color color)
        {
            var img = Art(CenterPivot(Box(parent, "Icon_" + picture, cx - w / 2f, cy - h / 2f, w, h)), picture, color);
            img.preserveAspect = true;
            return img;
        }

        /// <summary>Any sprite centred on (cx, cy), fitted into w×h (hidden while there is none).</summary>
        public static Image Icon(Transform parent, Sprite sprite, float cx, float cy, float w, float h)
        {
            var img = Img(CenterPivot(Box(parent, "Icon", cx - w / 2f, cy - h / 2f, w, h)), sprite, Color.white);
            img.preserveAspect = true;
            img.enabled = sprite != null;
            return img;
        }

        /// <summary>A card: a rounded face and its rim.</summary>
        public static Image Card(RectTransform rt, Color fill, Color line, bool thick = false)
        {
            var face = Art(rt, "round_fill", fill);
            Art(Fill(rt, "Rim"), thick ? "round_line2" : "round_line", line);
            return face;
        }

        /// <summary>The gold frame with corner ornaments over a dark backing.</summary>
        public static void GoldFrame(RectTransform rt, Color back)
        {
            Solid(Fill(rt, "Backing", 8f), back);
            Art(Fill(rt, "Frame"), "frame_gold", Color.white);
        }

        /// <summary>A thin gold line fading out at both ends (y is its middle), with a small diamond at the centre when asked.</summary>
        public static void Divider(Transform parent, float x, float y, float w, bool diamond = true, float alpha = 0.75f)
        {
            Art(Box(parent, "Divider", x, y - 1f, w, 2f), "fade_band", WithAlpha(Gold, alpha));
            if (diamond) Icon(parent, "diamond", x + w / 2f, y, 12f, 12f, Gold);
        }

        /// <summary>A plain line (y is its top).</summary>
        public static void Line(Transform parent, float x, float y, float w, float h, Color c) => Solid(Box(parent, "Line", x, y, w, h), c);

        // ================================================================== words
        public TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align,
                                    float x, float y, float w, float h, bool bold = false, bool shadow = false)
        {
            var rt = Box(parent, name, x, y, w, h);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            if (shadow && shadowed != null) t.fontSharedMaterial = shadowed;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>One line that shrinks to fit its box.</summary>
        public static TextMeshProUGUI Fit(TextMeshProUGUI t, float min)
        {
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = t.fontSize;
            return t;
        }

        /// <summary>A section's title: a gold diamond, the words, and a line running on to x + w. y is its middle.</summary>
        public TextMeshProUGUI Section(Transform parent, string text, float x, float y, float w, float size = 22f)
        {
            Icon(parent, "diamond_outline", x + 8f, y, 15f, 15f, Gold);
            var t = Text(parent, "Section", text, size, GoldText, TextAlignmentOptions.MidlineLeft, x + 28f, y - 18f, w - 28f, 36f, true);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            float tw = t.GetPreferredValues(text).x;
            float lx = x + 28f + tw + 16f;
            if (x + w - 8f > lx)
            {
                Line(parent, lx, y - 1f, x + w - 8f - lx, 2f, WithAlpha(Gold, 0.45f));
                Icon(parent, "diamond", x + w - 4f, y, 8f, 8f, Gold);
            }
            return t;
        }

        /// <summary>Numbers, shares and seconds in gold ("8 giây", "30%").</summary>
        public static string GoldNumbers(string s) =>
            string.IsNullOrEmpty(s) ? "" : Regex.Replace(s, @"\d+(?:[.,]\d+)?\s?(?:%|giây|ô)?", m => $"<color={Hex(GoldText)}>{m.Value}</color>");

        // ================================================================== buttons
        /// <summary>
        /// Makes a box clickable: a faint light over it while the pointer is there, darker while
        /// pressed, dimmed when it cannot be used.
        /// </summary>
        public static Button Clickable(RectTransform rt, Action onClick, bool sound = true)
        {
            var over = Art(Fill(rt, "Hover"), "round_fill", Color.white);
            over.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = over;
            var cb = b.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 0f);
            cb.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
            cb.pressedColor = new Color(0f, 0f, 0f, 0.2f);
            cb.selectedColor = new Color(1f, 1f, 1f, 0f);
            cb.disabledColor = new Color(0f, 0f, 0f, 0.4f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null)
                b.onClick.AddListener(() =>
                {
                    if (sound) AudioManager.Play("sfx_ui_click", 0.6f);
                    onClick();
                });
            return b;
        }

        /// <summary>The gold call to action ("Vào thế giới", "Tiếp tục"): dark bold words and a chevron.</summary>
        public Button GoldButton(Transform parent, string name, string label, float x, float y, float w, float h, float size, Action onClick, bool chevron = true)
        {
            var rt = Box(parent, name, x, y, w, h);
            Art(rt, "round_grad", ButtonGold);
            Line(rt, 8f, 4f, w - 16f, 2f, new Color(1f, 0.96f, 0.78f, 0.6f));
            Art(Fill(rt, "Rim"), "round_line2", C("#b07c26"));
            var t = Text(rt, "Label", label, size, Ink, TextAlignmentOptions.Center, 0f, 0f, w, h, true);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            if (chevron) Icon(rt, "chevron_right", w - 36f, h / 2f, 12f, 20f, Ink);
            return Clickable(rt, onClick);
        }

        /// <summary>A dark button with a gold rim ("Chơi một mình", "Quay lại"), an icon before the words when given.</summary>
        public Button DarkButton(Transform parent, string name, string label, float x, float y, float w, float h, float size, Action onClick,
                                 string icon = null, float iconW = 12f, float iconH = 20f)
        {
            var rt = Box(parent, name, x, y, w, h);
            Card(rt, C("#0f1a18"), C("#a9843c"));
            float shift = icon != null ? 20f : 0f;
            var t = Text(rt, "Label", label, size, Cream, TextAlignmentOptions.Center, shift, 0f, w - shift, h, true);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            if (icon != null) Icon(rt, icon, 34f, h / 2f, iconW, iconH, icon.StartsWith("chevron") ? GoldText : Color.white);
            return Clickable(rt, onClick);
        }

        /// <summary>A small square button with a chevron (the preview's turn buttons, the looks' arrows).</summary>
        public static Button ArrowButton(Transform parent, string name, bool right, float x, float y, float w, float h, Action onClick)
        {
            var rt = Box(parent, name, x, y, w, h);
            Card(rt, C("#0f1917"), GoldDim);
            Icon(rt, right ? "chevron_right" : "chevron_left", w / 2f, h / 2f, 10f, 17f, GoldText);
            return Clickable(rt, onClick);
        }

        /// <summary>A pill with a word in it (the difficulty of a class).</summary>
        public TextMeshProUGUI Badge(Transform parent, string text, Color c, float x, float y, float w, float h, float size)
        {
            var rt = Box(parent, "Badge", x, y, w, h);
            Card(rt, new Color(c.r * 0.22f, c.g * 0.22f, c.b * 0.22f, 0.95f), WithAlpha(c, 0.85f));
            return Text(rt, "Label", text, size, Color.Lerp(c, Color.white, 0.2f), TextAlignmentOptions.Center, 0f, 0f, w, h, true);
        }

        /// <summary>A key cap centred on (cx, cy).</summary>
        public void KeyCap(Transform parent, string key, float cx, float cy, float size)
        {
            var rt = Box(parent, "Key_" + key, cx - size / 2f, cy - size / 2f, size, size);
            Card(rt, C("#0c1413"), C("#77827c"));
            Text(rt, "Label", key, size * 0.42f, Cream, TextAlignmentOptions.Center, 0f, 0f, size, size, true);
        }

        // ================================================================== the stage
        /// <summary>
        /// Keeps a 1920×1080 layout whole on any screen: scaled down (never up) to fit the area
        /// it sits in, centred. The canvas already matches the screen's height, so this only
        /// matters on screens narrower than 16:9.
        /// </summary>
        public static void FitStage(RectTransform stage)
        {
            var area = stage.parent as RectTransform;
            if (area == null) return;
            var r = area.rect;
            float s = Mathf.Min(1f, Mathf.Min(r.width / 1920f, r.height / 1080f));
            if (s <= 0f) return;
            stage.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>A 1920×1080 stage in the middle of its parent (see <see cref="FitStage"/>).</summary>
        public static RectTransform Stage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(1920f, 1080f);
            return rt;
        }
    }
}

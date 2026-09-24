using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// Widgets for panels that build themselves at run time (the forge): rectangles, images,
    /// text, buttons and frames in the HUD's font and sprites.
    /// </summary>
    public sealed class UiKit
    {
        public readonly TMP_FontAsset font;
        public readonly Material outline;
        public readonly Sprite window, button, white;

        public static readonly Color Gold = new Color(1f, 0.86f, 0.45f);
        public static readonly Color Cream = new Color(0.96f, 0.93f, 0.86f);
        public static readonly Color Muted = new Color(0.66f, 0.62f, 0.72f);
        public static readonly Color Good = new Color(0.55f, 0.95f, 0.55f);
        public static readonly Color Bad = new Color(1f, 0.55f, 0.45f);

        public UiKit(TMP_FontAsset font, Material outline, Sprite window, Sprite button, Sprite white)
        {
            this.font = font;
            this.outline = outline;
            this.window = window;
            this.button = button;
            this.white = white;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
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

        public static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Img(RectTransform rt, Sprite sprite, Color color, Image.Type type = Image.Type.Simple)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = type;
            img.raycastTarget = false;
            return img;
        }

        public void Frame(RectTransform w)
        {
            var back = Stretch(w, "Backing");
            back.offsetMin = new Vector2(8, 8);
            back.offsetMax = new Vector2(-8, -8);
            Img(back, white, new Color(0.09f, 0.07f, 0.11f, 0.97f));
            if (window != null) Img(Stretch(w, "Frame"), window, Color.white, Image.Type.Sliced);
        }

        public TextMeshProUGUI Txt(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align,
                                   Vector2 anchor, Vector2 pos, Vector2 box, bool useOutline = true)
        {
            var rt = Rect(parent, name, anchor, pos, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            if (useOutline && outline != null) t.fontSharedMaterial = outline;
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

        public Button Btn(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, Action onClick)
        {
            var rt = Rect(parent, name, anchor, pos, size);
            var img = Img(rt, button != null ? button : white, button != null ? Color.white : new Color(0.25f, 0.2f, 0.3f),
                          button != null ? Image.Type.Sliced : Image.Type.Simple);
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

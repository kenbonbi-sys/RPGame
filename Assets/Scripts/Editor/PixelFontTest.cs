using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace RPG.EditorTools
{
    /// <summary>
    /// T19: Vietnamese in pixel fonts at 8 and 12 px, through TextMeshPro. The font files come from
    /// Tools/FontGen/make_pixel_fonts.py (Galmuri, OFL, subset to Latin + Vietnamese). Tools/RPG/Pixel Font Test
    /// makes TMP font assets for Galmuri7 (8 px) and Galmuri11 (12 px) — raster, one texel per font
    /// pixel, point filtered, the Vietnamese alphabet baked in — and opens a scene with game text at
    /// ×1, ×2 and ×3 next to the current Inter font. The game UI keeps Inter until the style guide
    /// (T18) picks its fonts. Docs/FontPixelTiengViet.md has the comparison.
    /// </summary>
    public static class PixelFontTest
    {
        public const string ScenePath = "Assets/Scenes/Tools/PixelFontTest.unity";

        public struct PixelFont
        {
            public string file, asset;
            public int px;   // font size at which one font pixel is one screen pixel
        }

        public static readonly PixelFont[] Fonts =
        {
            new PixelFont { file = "Assets/Fonts/Galmuri7.ttf", asset = "Assets/Fonts/Galmuri7 Pixel.asset", px = 8 },
            new PixelFont { file = "Assets/Fonts/Galmuri11.ttf", asset = "Assets/Fonts/Galmuri11 Pixel.asset", px = 12 },
        };

        static readonly string[] Lines =
        {
            "Bách Khoa Trùm: ghi lại Gấu Ma Rừng Già",
            "Kỹ năng: Dậm Đất · Chụp Quăng · Cuồng Nộ!",
            "Nhiệm vụ: Tìm Mèo Mướp (+1 · Tab)",
            "Trưởng Làng: Rừng đang thì thầm, cháu nghe thấy không?",
            "RỪNG THÌ THẦM · ẤN ẨN ẪN ẬN · ỄỂỆ ỖỔỘ ỞỠỢ ỪỬỮỰ",
            "Hồi chiêu 3.5s · +15% năng lượng · Cấp 12 · 1 280 XP",
        };

        [MenuItem("Tools/RPG/Pixel Font Test", priority = 43)]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var fonts = new List<(TMP_FontAsset asset, int px)>();
            foreach (var f in Fonts)
            {
                var fa = FontAsset(f);
                if (fa != null) fonts.Add((fa, f.px));
            }
            BuildScene(fonts);
        }

        /// <summary>The raster TMP font asset of a pixel font, created the first time.</summary>
        public static TMP_FontAsset FontAsset(PixelFont f)
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(f.asset);
            if (fa != null) return fa;
            var font = AssetDatabase.LoadAssetAtPath<Font>(f.file);
            if (font == null)
            {
                Debug.LogWarning($"[Font] {f.file} is missing: run python Tools/FontGen/make_pixel_fonts.py");
                return null;
            }
            fa = TMP_FontAsset.CreateFontAsset(font, f.px, 1, GlyphRenderMode.RASTER_HINTED, 256, 256, AtlasPopulationMode.Dynamic, true);
            if (fa == null)
            {
                Debug.LogWarning("[Font] TextMeshPro could not load " + f.file);
                return null;
            }
            fa.name = Path.GetFileNameWithoutExtension(f.asset);
            AssetDatabase.CreateAsset(fa, f.asset);
            if (fa.material != null)
            {
                fa.material.name = fa.name + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }
            fa.TryAddCharacters(AssetFactory.VietnameseCharset(), out string missing);
            for (int i = 0; i < fa.atlasTextures.Length; i++)
            {
                var tex = fa.atlasTextures[i];
                if (tex == null) continue;
                tex.name = $"{fa.name} Atlas {i}";
                tex.filterMode = FilterMode.Point;   // crisp pixels at every whole-number scale
                if (!AssetDatabase.Contains(tex)) AssetDatabase.AddObjectToAsset(tex, fa);
            }
            fa.atlasPopulationMode = AtlasPopulationMode.Static;   // everything is baked in: no atlas changes at runtime
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(missing)) Debug.Log($"[Font] {fa.name} has no glyph for: {missing} (falls back)");
            return fa;
        }

        static void BuildScene(List<(TMP_FontAsset asset, int px)> fonts)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.106f, 0.094f, 0.133f);

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;   // 1 canvas unit = 1 screen pixel
            scaler.scaleFactor = 1f;

            var column = new GameObject("Samples", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            column.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)column.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -24f);
            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = column.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var cream = new Color(0.95f, 0.91f, 0.82f);
            var muted = new Color(0.6f, 0.56f, 0.68f);
            void Text(TMP_FontAsset font, float size, string text, Color color)
            {
                var go = new GameObject($"{font.name} {size}", typeof(RectTransform));
                go.transform.SetParent(column.transform, false);
                var t = go.AddComponent<TextMeshProUGUI>();
                t.font = font;
                t.fontSize = size;
                t.color = color;
                t.richText = false;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.text = text;
            }
            string all = string.Join("\n", Lines);
            foreach (var (fa, px) in fonts)
            {
                Text(fa, px * 2, $"{fa.name}: {px} px · ×1, ×2 (cỡ nhỏ nhất trên màn 1080p theo mục 14), ×3", muted);
                Text(fa, px, Lines[0] + " · " + Lines[4], cream);
                Text(fa, px * 2, all, cream);
                Text(fa, px * 3, Lines[1], cream);
            }
            var inter = AssetFactory.Font;
            if (inter != null)
            {
                Text(inter, 16, "Inter SDF 16 px: font hiện tại của UI, để so sánh", muted);
                Text(inter, 16, all, cream);
            }

            EditorUtil.EnsureFolder("Assets/Scenes/Tools");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}

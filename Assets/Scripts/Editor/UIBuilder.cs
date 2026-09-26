using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RPG.EditorTools
{
    /// <summary>
    /// Builds the whole HUD as a real Canvas hierarchy in the scene (editable in the Inspector),
    /// laid out after the reference: boss bar top-centre, minimap + zone + time top-right,
    /// quest tracker under it, log on the left, HP/Energy orbs and skill/potion bars at the bottom.
    /// </summary>
    public static class UIBuilder
    {
        static TMP_FontAsset font;
        static Material outline, shadow;

        static readonly Color Gold = new Color(1f, 0.86f, 0.45f);
        static readonly Color Cream = new Color(0.96f, 0.93f, 0.86f);
        static readonly Color Muted = new Color(0.72f, 0.68f, 0.78f);
        static readonly Color HpRed = new Color(0.86f, 0.16f, 0.2f);
        static readonly Color EnergyBlue = new Color(0.2f, 0.55f, 1f);

        public class Refs
        {
            public HUD hud;
            public Image flash;
            public MinimapUI minimap;
            public CanvasGroup loading;
            public TextMeshProUGUI loadingTitle;
        }

        // ================================================================== primitives
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

        static RectTransform Stretch(Transform parent, string name, float inset = 0)
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

        static Image Img(RectTransform rt, string sprite, Color color, Image.Type type = Image.Type.Simple, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = string.IsNullOrEmpty(sprite) ? null : ArtImporter.S(sprite);
            img.color = color;
            img.type = type;
            img.raycastTarget = raycast;
            if (type == Image.Type.Sliced) img.fillCenter = true;
            return img;
        }

        static Image Img(Transform parent, string name, string sprite, Color color, Vector2 anchor, Vector2 pos, Vector2 size,
                         Image.Type type = Image.Type.Simple, bool raycast = false)
        {
            var rt = Rect(parent, name, anchor, new Vector2(0.5f, 0.5f), pos, size);
            return Img(rt, sprite, color, type, raycast);
        }

        static TextMeshProUGUI Txt(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align,
                                   Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box, bool useOutline = true)
        {
            var rt = Rect(parent, name, anchor, pivot, pos, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            if (useOutline && outline != null) t.fontSharedMaterial = outline;
            else if (shadow != null) t.fontSharedMaterial = shadow;
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

        static CanvasGroup Group(GameObject go, bool interactable = false)
        {
            var g = go.AddComponent<CanvasGroup>();
            g.interactable = interactable;
            g.blocksRaycasts = interactable;
            return g;
        }

        static readonly Vector2 C = new Vector2(0.5f, 0.5f);

        // ================================================================== build
        public static Refs Build(MinimapUI.MarkerKind unused = MinimapUI.MarkerKind.Enemy)
        {
            font = AssetFactory.Font;
            outline = AssetFactory.FontOutline;
            shadow = AssetFactory.FontShadow;
            var refs = new Refs();

            // event system (Input System UI module)
            var esGo = new GameObject("EventSystem", typeof(EventSystem));
            var module = esGo.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();

            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = 100;
            canvasGo.AddComponent<GraphicRaycaster>();
            var hud = canvasGo.AddComponent<HUD>();
            hud.canvas = canvas;
            refs.hud = hud;
            var root = canvasGo.transform;

            // full-screen flash sits under every widget so the HUD stays readable
            var flash = Stretch(root, "ScreenFlash");
            refs.flash = Img(flash, "white", new Color(1, 1, 1, 0));

            // layers
            hud.worldLayer = Stretch(root, "WorldLayer");
            var floatLayer = Stretch(root, "FloatingText");
            var fm = floatLayer.gameObject.AddComponent<FloatingTextManager>();
            fm.layer = floatLayer;
            fm.prefab = BuildFloatingTemplate(floatLayer);
            hud.floating = fm;

            hud.nameplatePrefab = BuildNameplateTemplate(hud.worldLayer);
            hud.skillBannerPrefab = BuildSkillBannerTemplate(hud.worldLayer);

            BuildBossBar(root, hud);
            BuildMinimap(root, hud, refs);
            BuildQuest(root, hud);
            BuildLog(root, hud);
            hud.hpOrb = BuildOrb(root, "HPOrb", OrbUI.Kind.Health, "Máu", HpRed, new Vector2(0, 0), new Vector2(150, 150), true);
            hud.energyOrb = BuildOrb(root, "EnergyOrb", OrbUI.Kind.Energy, "Năng lượng", EnergyBlue, new Vector2(1, 0), new Vector2(-150, 150), false);
            BuildPotionBar(root, hud);
            BuildSkillBar(root, hud);
            hud.xpBar = BuildXpBar(root);
            BuildBuffBar(root, hud);
            BuildBanner(root, hud);
            BuildDialogue(root);
            hud.heroPanel = BuildHeroPanel(root);
            hud.journal = BuildJournal(root);
            hud.help = BuildHelp(root);
            hud.pause = BuildPause(root);
            hud.saves = BuildSaves(root);
            hud.death = BuildDeath(root);
            hud.creator = BuildCreator(root);
            BuildForge(root);
            BuildSpellbook(root);
            hud.tooltip = BuildTooltip(root);
            BuildHint(root);
            BuildLoading(root, refs);
            return refs;
        }

        // ================================================================== templates
        static FloatingText BuildFloatingTemplate(Transform layer)
        {
            var t = Txt(layer, "FloatingTextTemplate", "123", 34, Color.white, TextAlignmentOptions.Center, C, C, Vector2.zero, new Vector2(260, 60));
            t.fontStyle = FontStyles.Bold;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var ft = t.gameObject.AddComponent<FloatingText>();
            ft.text = t;
            t.gameObject.SetActive(false);
            return ft;
        }

        static NameplateUI BuildNameplateTemplate(Transform layer)
        {
            var rt = Rect(layer, "NameplateTemplate", C, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(240, 60));
            var g = Group(rt.gameObject);
            var np = rt.gameObject.AddComponent<NameplateUI>();
            np.group = g;
            var row = Rect(rt, "NameRow", C, C, new Vector2(0, 16), new Vector2(240, 28));
            np.star = Img(row, "Star", "icon_star", Color.white, C, new Vector2(-70, 0), new Vector2(21, 21));
            np.nameText = Txt(row, "Name", "Tảng Đá Lớn", 21, Gold, TextAlignmentOptions.Center, C, C, Vector2.zero, new Vector2(240, 28));
            np.nameText.textWrappingMode = TextWrappingModes.NoWrap;
            var bar = Rect(rt, "Bar", C, C, new Vector2(0, -2), new Vector2(96, 11));
            Img(bar, "bar_track", Color.white, Image.Type.Sliced);
            var chip = Stretch(bar, "Chip", 2);
            np.barChip = Img(chip, "white", new Color(1f, 0.95f, 0.8f, 0.9f), Image.Type.Filled);
            np.barChip.fillMethod = Image.FillMethod.Horizontal;
            var fill = Stretch(bar, "Fill", 2);
            np.barFill = Img(fill, "bar_fill", new Color(1f, 0.7f, 0.25f), Image.Type.Filled);
            np.barFill.fillMethod = Image.FillMethod.Horizontal;
            np.bar = bar;
            // what the hero can do here, on a dark pill that fits the words, growing up from over the name
            var prompt = Rect(rt, "Prompt", C, new Vector2(0.5f, 0f), new Vector2(0, 32), new Vector2(200, 32));
            Img(prompt, "white", new Color(0.05f, 0.03f, 0.08f, 0.8f));
            var fit = prompt.gameObject.AddComponent<HorizontalLayoutGroup>();
            fit.padding = new RectOffset(12, 12, 3, 4);
            fit.childAlignment = TextAnchor.MiddleCenter;
            fit.childControlWidth = true;
            fit.childControlHeight = true;
            var pfit = prompt.gameObject.AddComponent<ContentSizeFitter>();
            pfit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            pfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            np.promptText = Txt(prompt, "Text", "[F] Nói chuyện", 21, new Color(1f, 0.93f, 0.55f), TextAlignmentOptions.Center, C, C, Vector2.zero, new Vector2(240, 28));
            np.promptText.textWrappingMode = TextWrappingModes.NoWrap;
            np.promptRoot = prompt.gameObject;
            rt.gameObject.SetActive(false);
            return np;
        }

        static SkillBannerUI BuildSkillBannerTemplate(Transform layer)
        {
            var rt = Rect(layer, "SkillBannerTemplate", C, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(300, 46));
            var g = Group(rt.gameObject);
            Img(rt, "frame_panel", new Color(1f, 1f, 1f, 0.95f), Image.Type.Sliced);
            var fit = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            fit.padding = new RectOffset(22, 22, 6, 8);
            fit.childAlignment = TextAnchor.MiddleCenter;
            fit.childControlWidth = true;
            fit.childControlHeight = true;
            var csf = rt.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var t = Txt(rt, "Text", "Kỹ năng: Dậm Đất", 25, new Color(0.9f, 0.95f, 1f), TextAlignmentOptions.Center, C, C, Vector2.zero, new Vector2(280, 34));
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var sb = rt.gameObject.AddComponent<SkillBannerUI>();
            sb.text = t;
            sb.group = g;
            rt.gameObject.SetActive(false);
            return sb;
        }

        // ================================================================== boss bar
        static void BuildBossBar(Transform root, HUD hud)
        {
            var rt = Rect(root, "BossBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(820, 110));
            var g = Group(rt.gameObject);
            var ui = rt.gameObject.AddComponent<BossBarUI>();
            ui.group = g;
            var shake = Rect(rt, "Shake", C, C, Vector2.zero, new Vector2(820, 110));
            ui.shakeRoot = shake;
            // title row with skulls
            ui.title = Txt(shake, "Title", "Gấu Ma Rừng Già · Cấp 6", 30, new Color(1f, 0.82f, 0.4f), TextAlignmentOptions.Center, C, C, new Vector2(0, 26), new Vector2(700, 40));
            ui.title.textWrappingMode = TextWrappingModes.NoWrap;
            Img(shake, "SkullL", "icon_skull", Color.white, C, new Vector2(-230, 26), new Vector2(30, 24));
            Img(shake, "SkullR", "icon_skull", Color.white, C, new Vector2(230, 26), new Vector2(30, 24));
            // frame + bar
            var frame = Rect(shake, "Frame", C, C, new Vector2(0, -20), new Vector2(820, 66));
            var track = Rect(frame, "Track", C, C, Vector2.zero, new Vector2(700, 26));
            Img(track, "white", new Color(0.08f, 0.04f, 0.07f, 1f));
            var chip = Stretch(track, "Chip", 0);
            ui.chip = Img(chip, "white", new Color(1f, 0.55f, 0.42f), Image.Type.Filled);
            ui.chip.fillMethod = Image.FillMethod.Horizontal;
            var fill = Stretch(track, "Fill", 0);
            ui.fill = Img(fill, "bar_fill", new Color(0.86f, 0.12f, 0.16f), Image.Type.Filled);
            ui.fill.fillMethod = Image.FillMethod.Horizontal;
            Img(frame, "frame_boss", Color.white, Image.Type.Sliced);
            ui.hpText = Txt(frame, "HP", "6825 / 6825", 22, Color.white, TextAlignmentOptions.Center, C, C, new Vector2(0, 1), new Vector2(400, 30));
            ui.hpText.textWrappingMode = TextWrappingModes.NoWrap;
            // Thanh Trấn Áp under the frame
            var poise = Rect(shake, "Poise", C, C, new Vector2(0, -64), new Vector2(560, 22));
            ui.poiseRoot = poise.gameObject;
            var ptrack = Rect(poise, "Track", C, C, new Vector2(40, 0), new Vector2(460, 9));
            Img(ptrack, "white", new Color(0.08f, 0.05f, 0.06f, 0.9f));
            var pfill = Stretch(ptrack, "Fill", 1);
            ui.poiseFill = Img(pfill, "white", new Color(0.95f, 0.8f, 0.4f), Image.Type.Filled);
            ui.poiseFill.fillMethod = Image.FillMethod.Horizontal;
            ui.poiseFill.fillAmount = 0;
            ui.poiseLabel = Txt(poise, "Label", "Trấn Áp", 17, new Color(0.8f, 0.76f, 0.7f), TextAlignmentOptions.MidlineRight,
                                C, new Vector2(1f, 0.5f), new Vector2(-196, 0), new Vector2(170, 22));
            ui.poiseLabel.textWrappingMode = TextWrappingModes.NoWrap;
            hud.bossBar = ui;
        }

        // ================================================================== minimap
        static void BuildMinimap(Transform root, HUD hud, Refs refs)
        {
            var rt = Rect(root, "Minimap", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -20), new Vector2(344, 268));
            Img(rt, "frame_wood", Color.white, Image.Type.Sliced);
            var mm = rt.gameObject.AddComponent<MinimapUI>();
            var mapBox = Rect(rt, "MapBox", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(300, 150));
            Img(mapBox, "white", new Color(0.04f, 0.06f, 0.05f));
            var mask = mapBox.gameObject.AddComponent<RectMask2D>();
            var mapRt = Stretch(mapBox, "Map", 0);
            var raw = mapRt.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            mm.map = raw;
            var markers = Stretch(mapBox, "Markers", 0);
            mm.markerRoot = markers;
            var pm = Img(markers, "Player", "icon_arrow", Color.white, C, Vector2.zero, new Vector2(20, 18));
            mm.playerMarker = pm.rectTransform;
            // inner frame lines
            Img(rt, "Divider", "divider", Color.white, new Vector2(0.5f, 1f), new Vector2(0, -186), new Vector2(260, 12));
            mm.zoneLabel = Txt(rt, "Zone", "Làng Lá Xanh", 27, Cream, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -192), new Vector2(320, 36));
            mm.zoneLabel.textWrappingMode = TextWrappingModes.NoWrap;
            var timeRow = Rect(rt, "Time", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -228), new Vector2(300, 28));
            mm.timeIcon = Img(timeRow, "Icon", "icon_sun", Color.white, C, new Vector2(-70, 0), new Vector2(24, 24));
            mm.timeLabel = Txt(timeRow, "Label", "Ngày  24%", 21, new Color(1f, 0.92f, 0.65f), TextAlignmentOptions.Left, C, new Vector2(0f, 0.5f), new Vector2(-52, 0), new Vector2(200, 28));
            mm.sunIcon = ArtImporter.S("icon_sun");
            mm.moonIcon = ArtImporter.S("icon_moon");
            mm.dotSprite = ArtImporter.S("white");
            mm.bossSprite = ArtImporter.S("icon_skull");
            mm.npcSprite = ArtImporter.S("quest_excl");
            mm.objectiveSprite = ArtImporter.S("icon_star");
            mm.worldSize = new Vector2Int(WorldBuilder.W, WorldBuilder.H);
            mm.viewTiles = new Vector2(44, 22);
            hud.minimap = mm;
            refs.minimap = mm;
        }

        static void BuildQuest(Transform root, HUD hud)
        {
            var rt = Rect(root, "QuestTracker", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-26, -296), new Vector2(620, 70));
            var q = rt.gameObject.AddComponent<QuestTrackerUI>();
            q.titleText = Txt(rt, "Title", "Lời Nhờ Của Trưởng Làng", 19, Muted, TextAlignmentOptions.Right, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(620, 26));
            q.text = Txt(rt, "Objective", "◆ Nói chuyện với Trưởng Làng  (+1 · Tab)", 23, Cream, TextAlignmentOptions.Right, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -24), new Vector2(620, 60));
            hud.quest = q;
        }

        // ================================================================== log
        static void BuildLog(Transform root, HUD hud)
        {
            var rt = Rect(root, "CombatLog", new Vector2(0, 0), new Vector2(0, 0), new Vector2(18, 300), new Vector2(620, 280));
            var log = rt.gameObject.AddComponent<CombatLogUI>();
            var vl = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childAlignment = TextAnchor.LowerLeft;
            vl.spacing = 4;
            vl.childControlHeight = true;
            vl.childControlWidth = false;
            vl.childForceExpandHeight = false;
            vl.childForceExpandWidth = false;
            log.container = rt;

            var line = Rect(rt, "LogLineTemplate", C, C, Vector2.zero, new Vector2(560, 36));
            var bg = line.gameObject.AddComponent<Image>();
            bg.sprite = ArtImporter.S("white");
            bg.color = new Color(0.05f, 0.03f, 0.07f, 0.55f);
            bg.raycastTarget = false;
            var lv = line.gameObject.AddComponent<VerticalLayoutGroup>();
            lv.padding = new RectOffset(12, 12, 5, 5);
            lv.childControlWidth = true;
            lv.childControlHeight = true;
            lv.childForceExpandWidth = true;
            lv.childForceExpandHeight = false;
            var g = Group(line.gameObject);
            var t = Txt(line, "Text", "• Bách Khoa Trùm: ghi lại kỹ năng \"Dậm Đất\" của Gấu Ma Rừng Già.", 20, Cream,
                        TextAlignmentOptions.MidlineLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(536, 26), false);
            t.textWrappingMode = TextWrappingModes.Normal;
            var ll = line.gameObject.AddComponent<LogLineUI>();
            ll.text = t;
            ll.group = g;
            line.gameObject.SetActive(false);
            log.linePrefab = ll;
            hud.log = log;
        }

        // ================================================================== orbs
        static OrbUI BuildOrb(Transform root, string name, OrbUI.Kind kind, string prefix, Color liquid, Vector2 anchor, Vector2 pos, bool left)
        {
            var rt = Rect(root, name, anchor, C, pos, new Vector2(240, 240));
            var orb = rt.gameObject.AddComponent<OrbUI>();
            orb.kind = kind;
            orb.prefix = prefix;
            Img(rt, "Back", "orb_back", Color.white, C, Vector2.zero, new Vector2(180, 180));
            var maskRt = Rect(rt, "Liquid", C, C, Vector2.zero, new Vector2(180, 180));
            var maskImg = Img(maskRt, "orb_back", Color.white);
            var mask = maskRt.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var fill = Img(maskRt, "Fill", "orb_liquid", liquid, C, Vector2.zero, new Vector2(180, 180), Image.Type.Filled);
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            var surf = Img(maskRt, "Surface", "orb_surface", Color.Lerp(liquid, Color.white, 0.55f), C, Vector2.zero, new Vector2(190, 9));
            orb.fill = fill;
            orb.surface = surf.rectTransform;
            orb.surfaceImage = surf;
            orb.liquidArea = maskRt;
            Img(rt, "Glass", "orb_glass", Color.white, C, Vector2.zero, new Vector2(180, 180));
            orb.lowPulse = Img(rt, "LowPulse", "glow", new Color(1f, 0.2f, 0.2f, 0f), C, Vector2.zero, new Vector2(230, 230));
            Img(rt, "Frame", "orb_frame", Color.white, C, Vector2.zero, new Vector2(240, 240));
            var label = Txt(rt, "Label", $"{prefix} 149/149", 24, Cream, left ? TextAlignmentOptions.Left : TextAlignmentOptions.Right,
                            C, new Vector2(left ? 0f : 1f, 0.5f), new Vector2(left ? -84 : 84, 136), new Vector2(320, 34));
            label.textWrappingMode = TextWrappingModes.NoWrap;
            orb.label = label;
            return orb;
        }

        // ================================================================== bars
        static RectTransform Slot(Transform parent, string name, Vector2 anchor, Vector2 pos, float size, out Image icon, out Image cd, string key)
        {
            var rt = Rect(parent, name, anchor, C, pos, new Vector2(size, size));
            Img(rt, "slot", Color.white, Image.Type.Sliced, true);
            icon = Img(rt, "Icon", null, Color.white, C, Vector2.zero, new Vector2(size - 18, size - 18));
            icon.preserveAspect = true;
            cd = Img(rt, "Cooldown", "white", new Color(0.02f, 0.01f, 0.05f, 0.72f), C, Vector2.zero, new Vector2(size - 14, size - 14), Image.Type.Filled);
            cd.fillMethod = Image.FillMethod.Radial360;
            cd.fillOrigin = (int)Image.Origin360.Top;
            cd.fillClockwise = false;
            cd.fillAmount = 0;
            if (!string.IsNullOrEmpty(key))
            {
                float w = key.Length > 2 ? 58 : 28;
                var badge = Img(rt, "KeyBadge", "key_badge", Color.white, new Vector2(0.5f, 0f), new Vector2(0, -2), new Vector2(w, 24), Image.Type.Sliced);
                var kt = Txt(badge.transform, "Key", key, 16, Cream, TextAlignmentOptions.Center, C, C, new Vector2(0, 1), new Vector2(w, 24), false);
                kt.textWrappingMode = TextWrappingModes.NoWrap;
            }
            return rt;
        }

        static void BuildPotionBar(Transform root, HUD hud)
        {
            var rt = Rect(root, "PotionBar", new Vector2(0, 0), new Vector2(0, 0), new Vector2(270, 22), new Vector2(330, 74));
            var pb = rt.gameObject.AddComponent<PotionBarUI>();
            for (int i = 0; i < 3; i++)
            {
                var s = Slot(rt, "Potion" + (i + 1), new Vector2(0, 0), new Vector2(37 + i * 74, 37), 68, out var icon, out var cd, (i + 1).ToString());
                pb.icons[i] = icon;
                pb.cooldowns[i] = cd;
                pb.counts[i] = Txt(s, "Count", "4", 20, Color.white, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(7, -3), new Vector2(40, 26));
            }
            var bag = Slot(rt, "Bag", new Vector2(0, 0), new Vector2(37 + 3 * 74 + 8, 37), 68, out var bagIcon, out var bagCd, "B");
            bagIcon.sprite = ArtImporter.S("pelt");
            bagIcon.color = new Color(0.9f, 0.85f, 0.75f);
            bagCd.enabled = false;
            var btn = bag.gameObject.AddComponent<Button>();
            btn.targetGraphic = bag.GetComponent<Image>();
            pb.bagButton = btn;
            hud.potionBar = pb;
        }

        static void BuildSkillBar(Transform root, HUD hud)
        {
            var rt = Rect(root, "SkillBar", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-270, 22), new Vector2(456, 156));
            var bar = rt.gameObject.AddComponent<SkillBarUI>();
            const float size = 70, gap = 7;
            for (int i = 0; i < 8; i++)
            {
                Vector2 pos;
                if (i < 6) pos = new Vector2(-(5 - i) * (size + gap) - size / 2, size / 2);
                else pos = new Vector2(-(7 - i) * (size + gap) - size / 2, size + 14 + size / 2);
                string key = InputReader.SkillLabel(i);
                var s = Slot(rt, "Skill_" + key, new Vector2(1, 0), pos, size, out var icon, out var cd, key);
                var ui = s.gameObject.AddComponent<SkillSlotUI>();
                ui.slot = i;
                ui.icon = icon;
                ui.cooldownMask = cd;
                ui.keyLabel = s.Find("KeyBadge/Key").GetComponent<TextMeshProUGUI>();
                ui.readyFlash = Img(s, "ReadyFlash", "white", new Color(1, 1, 1, 0), C, Vector2.zero, new Vector2(size - 14, size - 14));
                ui.highlight = Img(s, "Highlight", "slot_highlight", Color.white, C, Vector2.zero, new Vector2(size, size), Image.Type.Sliced);
                ui.highlight.enabled = false;
                ui.cdText = Txt(s, "CD", "", 22, Color.white, TextAlignmentOptions.Center, C, C, new Vector2(0, 2), new Vector2(size, 30));
                ui.cdText.fontStyle = FontStyles.Bold;
                ui.costText = Txt(s, "Cost", "", 15, new Color(0.55f, 0.8f, 1f), TextAlignmentOptions.TopRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-6, -3), new Vector2(40, 20));
                bar.slots[i] = ui;
            }
            // Tự Động (T): on the upper row, left of D
            var autoSlot = Slot(rt, "Auto", new Vector2(1, 0), new Vector2(-2 * (size + gap) - size / 2, size + 14 + size / 2), size, out var autoIcon, out var autoCd, "T");
            autoIcon.enabled = false;
            autoCd.enabled = false;
            var au = autoSlot.gameObject.AddComponent<AutoButtonUI>();
            au.glow = Img(autoSlot, "Glow", "slot_highlight", Color.white, C, Vector2.zero, new Vector2(size, size), Image.Type.Sliced);
            au.glow.enabled = false;
            au.label = Txt(autoSlot, "Label", "TỰ\nĐỘNG", 15, Cream, TextAlignmentOptions.Center, C, C, new Vector2(0, 5), new Vector2(size, 44));
            au.label.fontStyle = FontStyles.Bold;
            au.button = autoSlot.gameObject.AddComponent<Button>();
            au.button.targetGraphic = autoSlot.GetComponent<Image>();
            hud.skillBar = bar;
        }

        static void BuildBuffBar(Transform root, HUD hud)
        {
            var rt = Rect(root, "BuffBar", new Vector2(0, 0), new Vector2(0, 0), new Vector2(272, 108), new Vector2(360, 44));
            var hl = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 6;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = false;
            hl.childControlHeight = false;
            var bb = rt.gameObject.AddComponent<BuffBarUI>();
            bb.container = rt;
            var tmpl = Rect(rt, "BuffTemplate", C, C, Vector2.zero, new Vector2(40, 40));
            Img(tmpl, "st_shield", Color.white);
            var fill = Img(tmpl, "fill", "white", new Color(0, 0, 0, 0.55f), C, Vector2.zero, new Vector2(40, 40), Image.Type.Filled);
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            Txt(tmpl, "Time", "5", 17, Color.white, TextAlignmentOptions.BottomRight, C, C, new Vector2(4, -6), new Vector2(40, 40));
            tmpl.gameObject.SetActive(false);
            bb.iconPrefab = tmpl.gameObject;
            hud.buffs = bb;
        }

        // ================================================================== banners
        static void BuildBanner(Transform root, HUD hud)
        {
            var rt = Stretch(root, "Banners");
            var b = rt.gameObject.AddComponent<BannerUI>();
            var title = Rect(rt, "ZoneTitle", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -170), new Vector2(1200, 140));
            b.titleGroup = Group(title.gameObject);
            b.titleText = Txt(title, "Title", "Rừng Thì Thầm", 60, Cream, TextAlignmentOptions.Center, C, C, new Vector2(0, 14), new Vector2(1200, 80));
            b.titleText.textWrappingMode = TextWrappingModes.NoWrap;
            Img(title, "Divider", "divider", Color.white, C, new Vector2(0, -30), new Vector2(420, 15));
            b.subtitleText = Txt(title, "Sub", "— Khu vực —", 23, Muted, TextAlignmentOptions.Center, C, C, new Vector2(0, -56), new Vector2(800, 34));

            var quest = Rect(rt, "QuestToast", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22, -372), new Vector2(440, 86));
            b.questGroup = Group(quest.gameObject);
            Img(quest, "frame_panel", Color.white, Image.Type.Sliced);
            b.questHeader = Txt(quest, "Header", "Nhiệm vụ mới", 19, Muted, TextAlignmentOptions.Center, C, C, new Vector2(0, 18), new Vector2(400, 26));
            b.questTitle = Txt(quest, "Title", "Dọn Dẹp Rừng Thì Thầm", 28, Gold, TextAlignmentOptions.Center, C, C, new Vector2(0, -12), new Vector2(420, 38));

            var vic = Rect(rt, "Victory", C, C, new Vector2(0, 170), new Vector2(1400, 220));
            b.victoryGroup = Group(vic.gameObject);
            var vg = Img(vic, "Glow", "glow", new Color(1f, 0.8f, 0.3f, 0.35f), C, Vector2.zero, new Vector2(1100, 260));
            b.victoryText = Txt(vic, "Title", "CHIẾN THẮNG!", 96, new Color(1f, 0.85f, 0.35f), TextAlignmentOptions.Center, C, C, new Vector2(0, 20), new Vector2(1400, 120));
            b.victoryText.fontStyle = FontStyles.Bold;
            b.victoryText.textWrappingMode = TextWrappingModes.NoWrap;
            b.victorySub = Txt(vic, "Sub", "Đã đánh bại Gấu Ma Rừng Già", 30, Cream, TextAlignmentOptions.Center, C, C, new Vector2(0, -58), new Vector2(1200, 44));
            hud.banner = b;
        }

        // ================================================================== dialogue
        static void BuildDialogue(Transform root)
        {
            var rt = Stretch(root, "Dialogue");
            var g = Group(rt.gameObject, true);
            var ui = rt.gameObject.AddComponent<DialogueUI>();
            ui.group = g;
            var box = Rect(rt, "Box", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 190), new Vector2(1040, 220));
            ui.box = box;
            Img(box, "frame_wood", Color.white, Image.Type.Sliced, true);
            var pf = Img(box, "PortraitFrame", "portrait_frame", Color.white, new Vector2(0, 0.5f), new Vector2(104, 0), new Vector2(162, 162), Image.Type.Sliced);
            ui.portrait = Img(pf.transform, "Portrait", null, Color.white, C, new Vector2(0, 0), new Vector2(150, 150));
            ui.portrait.preserveAspect = true;
            ui.nameText = Txt(box, "Name", "Trưởng Làng", 30, Gold, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(206, -20), new Vector2(780, 40));
            ui.bodyText = Txt(box, "Body", "...", 26, Cream, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(206, -64), new Vector2(800, 110), false);
            ui.hintText = Txt(box, "Hint", "► [F / Space / Click] tiếp tục", 18, Muted, TextAlignmentOptions.BottomRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 16), new Vector2(500, 26), false);

            // choices stack above the box's right side: 1 on top
            var opts = Rect(box, "Options", new Vector2(1, 1), new Vector2(1, 0), new Vector2(-10, 10), new Vector2(600, 3 * 58));
            ui.optionsRoot = opts;
            for (int i = 0; i < 3; i++)
            {
                var b = Btn(opts, "Option_" + (i + 1), "", new Vector2(0.5f, 1f), new Vector2(0, -29 - i * 58), new Vector2(600, 52), 23);
                var label = b.GetComponentInChildren<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.margin = new Vector4(20, 0, 20, 0);
                ui.optionButtons[i] = b;
                ui.optionLabels[i] = label;
            }
            opts.gameObject.SetActive(false);
        }

        static Button Btn(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize = 26)
        {
            var rt = Rect(parent, name, anchor, C, pos, size);
            var img = Img(rt, "frame_panel", Color.white, Image.Type.Sliced, true);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cols = b.colors;
            cols.highlightedColor = new Color(1f, 0.92f, 0.7f);
            cols.pressedColor = new Color(0.8f, 0.75f, 0.6f);
            b.colors = cols;
            Txt(rt, "Label", label, fontSize, Cream, TextAlignmentOptions.Center, C, C, Vector2.zero, size);
            return b;
        }

        // ================================================================== xp
        static XpBarUI BuildXpBar(Transform root)
        {
            // a thin strip along the bottom edge, between the two orbs and under the slot bars
            var go = new GameObject("XpBar", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(272, 2);
            rt.offsetMax = new Vector2(-272, 11);
            var ui = go.AddComponent<XpBarUI>();
            Img(rt, "white", new Color(0.06f, 0.03f, 0.08f, 0.85f));
            var fill = Stretch(rt, "Fill", 1);
            ui.fill = Img(fill, "white", new Color(0.66f, 0.45f, 1f), Image.Type.Filled);
            ui.fill.fillMethod = Image.FillMethod.Horizontal;
            ui.flash = Img(Stretch(rt, "Flash", 0), "white", new Color(1, 1, 1, 0));
            ui.label = Txt(root, "XpLabel", "Cấp 1  ·  0 / 50 XP", 19, new Color(0.86f, 0.8f, 1f), TextAlignmentOptions.Center,
                           new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(420, 26));
            ui.label.textWrappingMode = TextWrappingModes.NoWrap;
            ui.pointsHint = Txt(root, "XpPoints", "+3 điểm chỉ số  [C]", 18, Gold, TextAlignmentOptions.Center,
                                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(420, 26));
            ui.pointsHint.textWrappingMode = TextWrappingModes.NoWrap;
            ui.pointsHint.enabled = false;
            return ui;
        }

        // ================================================================== panels
        static RectTransform Window(Transform root, string name, Vector2 size, Vector2 pos, out CanvasGroup g, string title, bool wood = true)
        {
            var full = Stretch(root, name);
            g = Group(full.gameObject, true);
            var dim = full.gameObject.AddComponent<Image>();
            dim.sprite = ArtImporter.S("white");
            dim.color = new Color(0, 0, 0, 0.35f);
            dim.raycastTarget = true;
            var w = Rect(full, "Window", C, C, pos, size);
            Img(w, wood ? "frame_wood" : "frame_panel", Color.white, Image.Type.Sliced, true);
            var t = Txt(w, "Title", title, 36, Gold, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(size.x - 40, 48));
            t.fontStyle = FontStyles.Bold;
            Img(w, "Divider", "divider", Color.white, new Vector2(0.5f, 1f), new Vector2(0, -78), new Vector2(size.x * 0.6f, 14));
            return w;
        }

        /// <summary>Nhân Vật (B / I / C): the sheet, the hero's gear and the bag, a full-screen panel that builds its own widgets (<see cref="HeroPanelUI"/>).</summary>
        static HeroPanelUI BuildHeroPanel(Transform root)
        {
            var rt = Stretch(root, "HeroPanel");
            var g = Group(rt.gameObject, true);
            var ui = rt.gameObject.AddComponent<HeroPanelUI>();
            ui.group = g;
            ui.window = rt;
            ui.font = font;
            ui.fontOutline = outline;
            ui.windowSprite = ArtImporter.S("frame_wood");
            ui.panelSprite = ArtImporter.S("frame_panel");
            ui.slotSprite = ArtImporter.S("slot");
            ui.highlightSprite = ArtImporter.S("slot_highlight");
            ui.dividerSprite = ArtImporter.S("divider");
            ui.whiteSprite = ArtImporter.S("white");
            ui.glowSprite = ArtImporter.S("glow");
            ui.portraitSprite = ArtImporter.S("portrait_frame");
            return ui;
        }

        static JournalUI BuildJournal(Transform root)
        {
            var w = Window(root, "Journal", new Vector2(640, 600), new Vector2(-360, 30), out var g, "Bách Khoa Trùm");
            var ui = w.parent.gameObject.AddComponent<JournalUI>();
            ui.group = g;
            ui.window = w;
            ui.body = Txt(w, "Body", "", 23, Cream, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -104), new Vector2(560, 440), false);
            Txt(w, "Hint", "[J] Đóng", 18, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(480, 26), false);
            return ui;
        }

        /// <summary>The character creator: a full-screen panel that builds its own widgets (<see cref="CharacterCreatorUI"/>).</summary>
        static CharacterCreatorUI BuildCreator(Transform root)
        {
            var rt = Stretch(root, "CharacterCreator");
            var g = Group(rt.gameObject, true);
            var ui = rt.gameObject.AddComponent<CharacterCreatorUI>();
            ui.group = g;
            ui.window = rt;
            ui.font = font;
            ui.fontOutline = outline;
            ui.windowSprite = ArtImporter.S("frame_wood");
            ui.buttonSprite = ArtImporter.S("frame_panel");
            ui.dividerSprite = ArtImporter.S("divider");
            ui.whiteSprite = ArtImporter.S("white");
            ui.slotSprite = ArtImporter.S("slot");
            return ui;
        }

        /// <summary>Lò Rèn: a full-screen panel that builds its own widgets (<see cref="ForgeUI"/>).</summary>
        static ForgeUI BuildForge(Transform root)
        {
            var rt = Stretch(root, "Forge");
            var g = Group(rt.gameObject, true);
            var ui = rt.gameObject.AddComponent<ForgeUI>();
            ui.group = g;
            ui.window = rt;
            ui.font = font;
            ui.fontOutline = outline;
            ui.windowSprite = ArtImporter.S("frame_wood");
            ui.buttonSprite = ArtImporter.S("frame_panel");
            ui.whiteSprite = ArtImporter.S("white");
            ui.slotSprite = ArtImporter.S("slot");
            ui.highlightSprite = ArtImporter.S("slot_highlight");
            ui.glowSprite = ArtImporter.S("glow");
            ui.dividerSprite = ArtImporter.S("divider");
            return ui;
        }

        /// <summary>Sách Chiêu (K): a full-screen panel that builds its own widgets (<see cref="SpellbookUI"/>).</summary>
        static SpellbookUI BuildSpellbook(Transform root)
        {
            var rt = Stretch(root, "Spellbook");
            var g = Group(rt.gameObject, true);
            var ui = rt.gameObject.AddComponent<SpellbookUI>();
            ui.group = g;
            ui.window = rt;
            ui.font = font;
            ui.fontOutline = outline;
            ui.windowSprite = ArtImporter.S("frame_wood");
            ui.buttonSprite = ArtImporter.S("frame_panel");
            ui.whiteSprite = ArtImporter.S("white");
            ui.slotSprite = ArtImporter.S("slot");
            ui.highlightSprite = ArtImporter.S("slot_highlight");
            return ui;
        }

        static HelpPanelUI BuildHelp(Transform root)
        {
            var w = Window(root, "Help", new Vector2(1040, 640), Vector2.zero, out var g, "Hướng Dẫn");
            var ui = w.parent.gameObject.AddComponent<HelpPanelUI>();
            ui.group = g;
            ui.window = w;
            const string K = "<color=#ffe07a>";
            const string E = "</color>";
            string left =
                $"{K}Di chuyển{E}\n  Chuột phải (bấm hoặc giữ), hoặc phím mũi tên\n\n" +
                $"{K}Tấn công{E}\n  Chuột trái: bấm vào quái để tới đánh,\n  giữ để chém về phía chuột · Q: Chém Gió\n\n" +
                $"{K}Kỹ năng{E}\n  Q W E R A S D: kỹ năng của lớp nhân vật\n  (di chuột lên ô kỹ năng để xem)\n  Space: Lướt (bất tử trong chốc lát)\n\n" +
                $"{K}Bình thuốc{E}\n  1 Máu · 2 Năng lượng · 3 Thảo mộc";
            string right =
                $"{K}Tương tác{E}\n  F: Nói chuyện · Lò Rèn · Đá Truyền Tống\n  B / C: Nhân vật, trang bị và túi đồ\n  J: Bách Khoa Trùm · Tab: Đổi nhiệm vụ\n  K: Sách Chiêu (chiêu học từ Bí Kíp)\n  T: Tự động đánh quái · M: Bản đồ\n  F1: Hướng dẫn · Esc: Tạm dừng\n\n" +
                $"{K}Mẹo chiến đấu{E}\n  Vòng đỏ dưới đất = đòn sắp đánh.\n  Lướt (Space) ra ngoài vòng!\n\n" +
                $"{K}Phím thử nghiệm{E}\n  F5 hồi đầy · F6 đổi giờ · F7 tới Boss\n  F8 về làng · F9 hạ quái gần";
            Txt(w, "Left", left, 22, Cream, TextAlignmentOptions.TopLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -104), new Vector2(460, 460), false);
            Txt(w, "Right", right, 22, Cream, TextAlignmentOptions.TopLeft, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-50, -104), new Vector2(460, 460), false)
                .rectTransform.pivot = new Vector2(1, 1);
            Txt(w, "Hint", "Nhấn phím bất kỳ để bắt đầu", 20, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(600, 30), false);
            return ui;
        }

        static PauseMenuUI BuildPause(Transform root)
        {
            var w = Window(root, "Pause", new Vector2(440, 560), Vector2.zero, out var g, "Tạm Dừng");
            var ui = w.parent.gameObject.AddComponent<PauseMenuUI>();
            ui.group = g;
            ui.window = w;
            ui.pausesGame = true;
            Button PauseBtn(string label, float y) => Btn(w, "Btn_" + label, label, C, new Vector2(0, y), new Vector2(300, 60));
            ui.resumeButton = PauseBtn("Tiếp tục", 118);
            ui.saveButton = PauseBtn("Lưu game", 44);
            ui.loadButton = PauseBtn("Tải game", -30);
            ui.helpButton = PauseBtn("Hướng dẫn", -104);
            ui.quitButton = PauseBtn("Thoát game", -178);
            return ui;
        }

        static SaveSlotsUI BuildSaves(Transform root)
        {
            var w = Window(root, "Saves", new Vector2(640, 560), Vector2.zero, out var g, "Lưu Game");
            var ui = w.parent.gameObject.AddComponent<SaveSlotsUI>();
            ui.group = g;
            ui.window = w;
            ui.pausesGame = true;
            ui.title = w.Find("Title").GetComponent<TextMeshProUGUI>();
            for (int i = 0; i <= SaveManager.SlotCount; i++)
            {
                var b = Btn(w, "Slot_" + i, "", new Vector2(0.5f, 1f), new Vector2(0, -140 - i * 94), new Vector2(560, 84), 23);
                var label = b.GetComponentInChildren<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.margin = new Vector4(22, 0, 22, 0);
                label.lineSpacing = -8;
                ui.rows[i] = b;
                ui.labels[i] = label;
            }
            Txt(w, "Hint", "[Esc] Đóng  ·  File lưu cũ được giữ lại dạng .bak", 17, Muted, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(560, 24), false);
            return ui;
        }

        static DeathScreenUI BuildDeath(Transform root)
        {
            var rt = Stretch(root, "DeathScreen");
            var g = Group(rt.gameObject);
            Img(rt, "white", new Color(0.18f, 0.0f, 0.02f, 0.62f));
            var ui = rt.gameObject.AddComponent<DeathScreenUI>();
            ui.group = g;
            ui.title = Txt(rt, "Title", "BẠN ĐÃ GỤC NGÃ", 84, new Color(1f, 0.35f, 0.3f), TextAlignmentOptions.Center, C, C, new Vector2(0, 40), new Vector2(1400, 120));
            ui.title.fontStyle = FontStyles.Bold;
            ui.countdown = Txt(rt, "Countdown", "Hồi sinh sau 4...", 30, Cream, TextAlignmentOptions.Center, C, C, new Vector2(0, -50), new Vector2(800, 50));
            return ui;
        }

        static TooltipUI BuildTooltip(Transform root)
        {
            var rt = Rect(root, "Tooltip", new Vector2(0.5f, 0.5f), new Vector2(0, 0), Vector2.zero, new Vector2(400, 160));
            var g = Group(rt.gameObject);
            // a solid backing under the frame: tooltips open over busy windows (the bag, the forge)
            var back = Stretch(rt, "Backing", 5);
            Img(back, "white", new Color(0.07f, 0.05f, 0.09f, 0.97f));
            back.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Img(rt, "frame_panel", Color.white, Image.Type.Sliced);
            var vl = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(22, 22, 18, 20);
            vl.spacing = 6;
            vl.childControlHeight = true;
            vl.childControlWidth = true;
            vl.childForceExpandHeight = false;
            var csf = rt.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var ui = rt.gameObject.AddComponent<TooltipUI>();
            ui.rect = rt;
            ui.group = g;
            ui.title = Txt(rt, "Title", "Tên", 26, Gold, TextAlignmentOptions.TopLeft, C, C, Vector2.zero, new Vector2(356, 34));
            ui.body = Txt(rt, "Body", "Mô tả", 20, Cream, TextAlignmentOptions.TopLeft, C, C, Vector2.zero, new Vector2(356, 80), false);
            return ui;
        }

        /// <summary>Black screen with the zone name, shown while the SceneLoader swaps zones (on top of everything).</summary>
        static void BuildLoading(Transform root, Refs refs)
        {
            var rt = Stretch(root, "Loading");
            var g = Group(rt.gameObject, true);   // interactable: it blocks clicks while visible
            Img(rt, "white", new Color(0.03f, 0.03f, 0.05f, 1f), Image.Type.Simple, true);
            refs.loadingTitle = Txt(rt, "Title", "Rừng Thì Thầm", 54, Cream, TextAlignmentOptions.Center, C, C, new Vector2(0, 20), new Vector2(1200, 80));
            Img(rt, "Divider", "divider", Color.white, C, new Vector2(0, -26), new Vector2(360, 14));
            Txt(rt, "Sub", "Đang tải…", 22, Muted, TextAlignmentOptions.Center, C, C, new Vector2(0, -60), new Vector2(600, 34), false);
            refs.loading = g;
        }

        static void BuildHint(Transform root)
        {
            Txt(root, "HelpHint", "F1: Hướng dẫn   ·   B / C: Nhân vật & túi đồ   ·   J: Bách Khoa Trùm   ·   M: Bản đồ", 17, new Color(0.8f, 0.78f, 0.85f, 0.7f),
                TextAlignmentOptions.Left, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -14), new Vector2(700, 26), false);
        }
    }
}

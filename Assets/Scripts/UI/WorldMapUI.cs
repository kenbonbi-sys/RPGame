using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// Bản đồ thế giới (M, plan T49): the whole map with the hero, the bosses and the Đá Truyền Tống
    /// the hero has woken. Standing at a woken stone (F at the stone opens the map too), clicking
    /// another woken stone travels there (<see cref="WaystoneLog.RequestTravel"/>). Made at run
    /// time on the HUD, in the HUD's own font; it draws the minimap's texture whole.
    /// </summary>
    public class WorldMapUI : UIPanel
    {
        public static WorldMapUI I { get; private set; }

        static readonly Color Gold = new Color(1f, 0.86f, 0.45f);
        static readonly Color Cream = new Color(0.96f, 0.93f, 0.86f);
        static readonly Color Muted = new Color(0.72f, 0.68f, 0.78f);
        static readonly Color Plate = new Color(0.07f, 0.06f, 0.1f, 0.94f);
        static readonly Color Here = new Color(0.45f, 1f, 0.95f);

        const float MapWidth = 1560f, MapHeight = 520f;

        class StoneMark
        {
            public Waystone stone;
            public RectTransform rt;
            public Image icon;
            public TextMeshProUGUI label;
            public Button button;
        }

        TMP_FontAsset font;
        Material material;
        RawImage map;
        RectTransform mapRect, frame, heroMark;
        TextMeshProUGUI hint, place;
        readonly List<StoneMark> stones = new List<StoneMark>();
        readonly List<(BossBase boss, RectTransform rt)> bosses = new List<(BossBase, RectTransform)>();
        float nextRefresh;

        /// <summary>The map of this HUD, made on first use.</summary>
        public static WorldMapUI Ensure(HUD hud)
        {
            if (hud == null || hud.canvas == null) return null;
            var ui = hud.GetComponentInChildren<WorldMapUI>(true);
            if (ui != null) return ui;
            var go = new GameObject("WorldMap", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(hud.canvas.transform, false);
            go.SetActive(false);   // Awake after the parts exist
            ui = go.AddComponent<WorldMapUI>();
            ui.Build(hud);
            go.SetActive(true);
            return ui;
        }

        protected override void Awake()
        {
            I = this;
            base.Awake();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        protected override void OnShow()
        {
            Layout();
            Refresh();
        }

        protected override void Update()
        {
            base.Update();
            if (IsOpen && Time.unscaledTime >= nextRefresh) Refresh();
        }

        // ================================================================== showing
        /// <summary>Fits the map to the zone's shape and makes a mark for every stone and boss.</summary>
        void Layout()
        {
            var size = MinimapUI.WorldSize;
            float aspect = size.y > 0 ? size.x / (float)size.y : 3f;
            float w = MapWidth, h = MapWidth / aspect;
            if (h > MapHeight)
            {
                h = MapHeight;
                w = MapHeight * aspect;
            }
            mapRect.sizeDelta = new Vector2(w, h);
            frame.sizeDelta = new Vector2(w + 6f, h + 6f);
            map.texture = MinimapUI.MapTexture;
            if (heroMark.GetComponent<Image>().sprite == null) heroMark.GetComponent<Image>().sprite = MinimapUI.PlayerSprite;

            foreach (var m in stones)
                if (m.rt != null) Destroy(m.rt.gameObject);
            stones.Clear();
            foreach (var w0 in Waystone.All)
            {
                if (w0 == null) continue;
                var stone = w0;
                var rt = Rect(mapRect, "Stone " + stone.stoneId, new Vector2(0f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(36f, 56f));
                var icon = Img(rt, Color.white);
                icon.sprite = stone.lit != null ? stone.lit : stone.dark;
                icon.preserveAspect = true;
                icon.raycastTarget = true;
                var b = rt.gameObject.AddComponent<Button>();
                b.targetGraphic = icon;
                var colors = b.colors;
                colors.highlightedColor = new Color(1.4f, 1.4f, 1.2f);
                colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                b.colors = colors;
                b.onClick.AddListener(() => Travel(stone));
                var label = Txt(rt, "Name", stone.displayName, 20, Cream, TextAlignmentOptions.Center,
                                new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(260f, 28f));
                stones.Add(new StoneMark { stone = stone, rt = rt, icon = icon, label = label, button = b });
            }

            foreach (var bm in bosses)
                if (bm.rt != null) Destroy(bm.rt.gameObject);
            bosses.Clear();
            foreach (var boss in BossBase.All)
            {
                if (boss == null) continue;
                var rt = Rect(mapRect, "Boss " + boss.bossId, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
                var img = Img(rt, boss.rank == EnemyRank.Boss ? new Color(1f, 0.4f, 0.35f) : new Color(1f, 0.7f, 0.4f));
                img.sprite = MinimapUI.BossSprite;
                bosses.Add((boss, rt));
            }
            heroMark.SetAsLastSibling();
        }

        void Refresh()
        {
            nextRefresh = Time.unscaledTime + 0.15f;
            var me = Players.Local;
            var log = me != null ? me.waystones : null;
            var size = MinimapUI.WorldSize;
            Vector2 ToMap(Vector2 world) => new Vector2(world.x / Mathf.Max(1, size.x) * mapRect.sizeDelta.x,
                                                        world.y / Mathf.Max(1, size.y) * mapRect.sizeDelta.y);
            if (map.texture == null) map.texture = MinimapUI.MapTexture;
            if (me != null)
            {
                heroMark.gameObject.SetActive(true);
                heroMark.anchoredPosition = ToMap(me.transform.position);
                heroMark.localRotation = Quaternion.Euler(0, 0, Util.Angle(me.motor.Facing) - 90f);
            }
            else heroMark.gameObject.SetActive(false);

            var at = me != null ? Waystone.At(me.transform.position) : null;
            bool canTravel = at != null && log != null && log.Knows(at.stoneId) && !me.IsDead;
            int known = 0;
            foreach (var m in stones)
            {
                if (m.stone == null)
                {
                    m.rt.gameObject.SetActive(false);
                    continue;
                }
                bool woke = log != null && log.Knows(m.stone.stoneId);
                m.rt.gameObject.SetActive(woke);   // a stone shows up once found
                if (!woke) continue;
                known++;
                m.rt.anchoredPosition = ToMap(m.stone.transform.position);
                bool here = m.stone == at;
                bool last = log.Last == m.stone.stoneId;
                m.button.interactable = canTravel && !here;
                m.label.text = m.stone.displayName + (here ? " · bạn ở đây" : last ? " · hồi sinh" : "");
                m.label.color = here ? Here : canTravel ? Gold : Cream;
            }
            foreach (var (boss, rt) in bosses)
            {
                bool show = boss != null && boss.gameObject.activeInHierarchy && !boss.health.IsDead;
                rt.gameObject.SetActive(show);
                if (show) rt.anchoredPosition = ToMap(boss.Home);
            }
            var area = ZoneArea.Current;
            place.text = area != null ? area.zoneName : "";
            if (canTravel) hint.text = "Bấm vào một Đá Truyền Tống để dịch chuyển tới đó.   <color=#b8b0c8>M / Esc: đóng</color>";
            else if (known == 0) hint.text = "Chưa đánh thức Đá Truyền Tống nào: đi tới gần một viên đá để đánh thức nó.   <color=#b8b0c8>M / Esc: đóng</color>";
            else hint.text = "Đứng cạnh một Đá Truyền Tống đã đánh thức (bấm F) để dịch chuyển.   <color=#b8b0c8>M / Esc: đóng</color>";
        }

        void Travel(Waystone stone)
        {
            var me = Players.Local;
            if (me == null || me.waystones == null || stone == null) return;
            AudioManager.Play("sfx_ui_click", 0.6f);
            Close();
            me.waystones.RequestTravel(stone.stoneId);
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
            group = GetComponent<CanvasGroup>();
            pausesGame = true;   // offline only: an online world keeps going
            blocksGameplay = true;
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            Img(Stretch(root, "Dim"), new Color(0f, 0f, 0f, 0.55f)).raycastTarget = true;

            window = Rect(root, "Window", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MapWidth + 60f, MapHeight + 170f));
            Img(Stretch(window, "Plate"), Plate).raycastTarget = true;
            Txt(window, "Title", "Bản Đồ Thế Giới", 34, Gold, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(700f, 48f));
            place = Txt(window, "Place", "", 22, Muted, TextAlignmentOptions.Center,
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(700f, 30f));
            frame = Rect(window, "Frame", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(MapWidth + 6f, MapHeight + 6f));
            Img(frame, new Color(0.35f, 0.3f, 0.42f, 1f));
            mapRect = Rect(window, "Map", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(MapWidth, MapHeight));
            map = mapRect.gameObject.AddComponent<RawImage>();
            map.raycastTarget = false;
            heroMark = Rect(mapRect, "Hero", Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            Img(heroMark, Here);
            hint = Txt(window, "Hint", "", 22, Cream, TextAlignmentOptions.Center,
                       new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(MapWidth, 32f));
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
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            t.raycastTarget = false;
            return t;
        }
    }
}

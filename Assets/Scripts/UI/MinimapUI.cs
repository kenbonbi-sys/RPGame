using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace RPG
{
    /// <summary>
    /// Top-right minimap. The map texture is generated from the tilemaps at start;
    /// markers (player, NPCs, enemies, boss, quest objective) are UI images.
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        public enum MarkerKind { Enemy, NPC, Boss, Objective }

        [Header("Map")]
        public RawImage map;
        public RectTransform markerRoot;
        public RectTransform playerMarker;
        public Tilemap ground, tallGrass, dirt;
        public Transform obstaclesRoot;
        public Vector2Int worldSize = new Vector2Int(100, 64);
        public Vector2 viewTiles = new Vector2(44, 22);

        [Header("Labels")]
        public TextMeshProUGUI zoneLabel;
        public TextMeshProUGUI timeLabel;
        public Image timeIcon;
        public Sprite sunIcon, moonIcon;

        [Header("Marker sprites")]
        public Sprite dotSprite;
        public Sprite bossSprite;
        public Sprite npcSprite;
        public Sprite objectiveSprite;

        static readonly List<(Transform t, MarkerKind k)> Pending = new List<(Transform, MarkerKind)>();
        static MinimapUI inst;
        readonly List<(Transform t, MarkerKind k, RectTransform icon)> markers = new List<(Transform, MarkerKind, RectTransform)>();
        RectTransform objectiveMarker;
        Texture2D tex;

        public static void Register(Transform t, MarkerKind kind)
        {
            if (inst != null) inst.Add(t, kind);
            else Pending.Add((t, kind));
        }

        void Awake()
        {
            inst = this;
            foreach (var p in Pending) Add(p.t, p.k);
            Pending.Clear();
            GameEvents.ZoneEntered += OnZone;
        }

        void OnDestroy()
        {
            if (inst == this) inst = null;
            GameEvents.ZoneEntered -= OnZone;
        }

        void Start()
        {
            if (tex == null) BuildTexture();
            if (objectiveMarker == null) objectiveMarker = MakeIcon(objectiveSprite, new Color(1f, 0.85f, 0.3f), 18);
        }

        /// <summary>Draws the map of a newly loaded zone.</summary>
        public void SetWorld(ZoneRoot zone)
        {
            ground = zone.ground;
            tallGrass = zone.tallGrass;
            dirt = zone.dirt;
            obstaclesRoot = zone.obstacles;
            worldSize = new Vector2Int(Mathf.CeilToInt(zone.bounds.xMax), Mathf.CeilToInt(zone.bounds.yMax));
            if (tex != null) Destroy(tex);
            BuildTexture();
        }

        void OnZone(string zone)
        {
            if (zoneLabel != null) zoneLabel.text = zone;
        }

        void Add(Transform t, MarkerKind kind)
        {
            if (t == null || markerRoot == null) return;
            Sprite s = kind == MarkerKind.Boss ? bossSprite : kind == MarkerKind.NPC ? npcSprite : dotSprite;
            Color c = kind == MarkerKind.Enemy ? new Color(1f, 0.35f, 0.3f) : kind == MarkerKind.NPC ? new Color(1f, 0.9f, 0.4f) : Color.white;
            float size = kind == MarkerKind.Boss ? 22 : kind == MarkerKind.NPC ? 14 : 8;
            markers.Add((t, kind, MakeIcon(s, c, size)));
        }

        RectTransform MakeIcon(Sprite s, Color c, float size)
        {
            var go = new GameObject("marker", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(markerRoot, false);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.sprite = s;
            img.color = c;
            img.raycastTarget = false;
            return rt;
        }

        void BuildTexture()
        {
            int w = worldSize.x, h = worldSize.y;
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[w * h];
            Color32 grass = new Color32(74, 110, 58, 255);
            Color32 tall = new Color32(38, 70, 44, 255);
            Color32 dirtC = new Color32(150, 118, 80, 255);
            Color32 edge = new Color32(20, 30, 24, 255);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector3Int(x, y, 0);
                    Color32 c = edge;
                    if (ground != null && ground.HasTile(p)) c = grass;
                    if (tallGrass != null && tallGrass.HasTile(p)) c = tall;
                    if (dirt != null && dirt.HasTile(p)) c = dirtC;
                    cols[y * w + x] = c;
                }
            // obstacles (trees, rocks) as dark pixels
            if (obstaclesRoot != null)
            {
                foreach (Transform t in obstaclesRoot)
                {
                    int x = Mathf.FloorToInt(t.position.x), y = Mathf.FloorToInt(t.position.y);
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    bool tree = t.name.StartsWith("pine") || t.name.StartsWith("oak");
                    cols[y * w + x] = tree ? new Color32(22, 48, 34, 255) : new Color32(110, 104, 118, 255);
                    if (tree && y + 1 < h) cols[(y + 1) * w + x] = new Color32(28, 58, 40, 255);
                }
            }
            tex.SetPixels32(cols);
            tex.Apply();
            if (map != null) map.texture = tex;
        }

        void LateUpdate()
        {
            var p = Players.Local;
            if (p == null || map == null) return;
            Vector2 pp = p.transform.position;
            float vx = viewTiles.x, vy = viewTiles.y;
            float ox = Mathf.Clamp(pp.x - vx * 0.5f, 0, worldSize.x - vx);
            float oy = Mathf.Clamp(pp.y - vy * 0.5f, 0, worldSize.y - vy);
            map.uvRect = new Rect(ox / worldSize.x, oy / worldSize.y, vx / worldSize.x, vy / worldSize.y);
            var r = map.rectTransform.rect;

            Vector2 ToUI(Vector2 world) => new Vector2((world.x - ox) / vx * r.width + r.xMin, (world.y - oy) / vy * r.height + r.yMin);
            bool Inside(Vector2 ui) => ui.x > r.xMin && ui.x < r.xMax && ui.y > r.yMin && ui.y < r.yMax;

            if (playerMarker != null)
            {
                playerMarker.anchoredPosition = ToUI(pp);
                var f = p.motor.Facing;
                playerMarker.localRotation = Quaternion.Euler(0, 0, Util.Angle(f) - 90f);
            }
            for (int i = markers.Count - 1; i >= 0; i--)
            {
                var m = markers[i];
                if (m.t == null)
                {
                    if (m.icon != null) Destroy(m.icon.gameObject);
                    markers.RemoveAt(i);
                    continue;
                }
                var e = m.k == MarkerKind.Enemy ? m.t.GetComponent<EnemyBase>() : null;
                bool alive = m.t.gameObject.activeInHierarchy && (e == null || !e.IsDead);
                Vector2 ui = ToUI(m.t.position);
                m.icon.gameObject.SetActive(alive && Inside(ui));
                m.icon.anchoredPosition = ui;
            }
            if (objectiveMarker != null && p.quests != null)
            {
                var obj = p.quests.ObjectivePosition();
                if (obj.HasValue)
                {
                    Vector2 ui = ToUI(obj.Value);
                    // clamp to the edge so it works as a direction hint
                    ui.x = Mathf.Clamp(ui.x, r.xMin + 8, r.xMax - 8);
                    ui.y = Mathf.Clamp(ui.y, r.yMin + 8, r.yMax - 8);
                    objectiveMarker.gameObject.SetActive(true);
                    objectiveMarker.anchoredPosition = ui;
                    objectiveMarker.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f));
                }
                else objectiveMarker.gameObject.SetActive(false);
            }

            var dn = DayNightCycle.I;
            if (dn != null && timeLabel != null)
            {
                dn.GetPhase(out string label, out float prog, out bool isDay);
                timeLabel.text = $"{label}  <color=#c8c0d0>{Mathf.RoundToInt(prog * 100)}%</color>";
                if (timeIcon != null) timeIcon.sprite = isDay ? sunIcon : moonIcon;
            }
        }
    }
}

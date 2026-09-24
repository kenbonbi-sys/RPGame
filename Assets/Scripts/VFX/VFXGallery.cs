using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RPG
{
    /// <summary>
    /// VFX Gallery (plan §13, T21): the effects of the VFX library on a grid, one page at a time,
    /// replayable, each labelled with its peak particle count and Light2D count against the budget
    /// of the VFXLibrary (150 particles, 1 Light2D; a Tuyệt kỹ gets twice that).
    /// Open it with Tools/RPG/VFX Gallery. Keys: 1–9 play one cell · Space the whole page ·
    /// ←/→ page · B bloom · L loop · M measure everything (plays every page, then logs the budget
    /// report) · Tab the table of all effects. Clicking a label replays that effect too.
    /// </summary>
    public class VFXGallery : MonoBehaviour
    {
        public VFXLibrary library;
        [Tooltip("Post-processing volume whose Bloom B switches on and off.")]
        public Volume volume;
        public Camera cam;
        public int columns = 3;
        public int rows = 3;
        public float spacing = 4.5f;
        [Tooltip("Seconds between replays when looping, and per page when measuring.")]
        public float interval = 2.5f;
        public bool loop = true;

        class Cell
        {
            public VFXLibrary.Entry entry;
            public GameObject instance;
            public ParticleSystem[] systems;
            public int peak = -1;   // most particles alive at once so far; -1 = never played
            public int lights;
        }

        readonly List<Cell> cells = new List<Cell>();
        int page;
        float nextPlay;
        bool measuring;
        bool showTable;
        Bloom bloom;
        Vector2 tableScroll;
        GUIStyle labelStyle, textStyle;

        int PerPage => Mathf.Max(1, columns * rows);
        public int PageCount => Mathf.Max(1, (cells.Count + PerPage - 1) / PerPage);
        public int Page => page;
        public int EffectCount => cells.Count;

        void Start()
        {
            if (library == null)
            {
                Debug.LogWarning("[VFX Gallery] No VFX library assigned.");
                enabled = false;
                return;
            }
            foreach (var e in library.entries)
                if (e != null && e.prefab != null) cells.Add(new Cell { entry = e, lights = Lights(e.prefab) });
            if (volume != null) volume.profile.TryGet(out bloom);   // .profile is a runtime copy: the asset is not changed
            if (cam == null) cam = Camera.main;
            if (cam != null) cam.orthographicSize = rows * spacing * 0.5f + 1.2f;
            ShowPage(0);
        }

        /// <summary>World position of a slot (0 = top left) of the current page, centred on the gallery.</summary>
        public Vector3 SlotPosition(int slot)
        {
            int c = slot % columns, r = slot / columns;
            return transform.position + new Vector3((c - (columns - 1) * 0.5f) * spacing, ((rows - 1) * 0.5f - r) * spacing, 0f);
        }

        public void ShowPage(int p)
        {
            foreach (var c in cells) Stop(c);
            page = (p % PageCount + PageCount) % PageCount;
            PlayPage();
        }

        public void PlayPage()
        {
            for (int slot = 0; slot < PerPage; slot++) PlaySlot(slot);
            nextPlay = Time.time + interval;
        }

        public void PlaySlot(int slot)
        {
            int i = page * PerPage + slot;
            if (slot < 0 || slot >= PerPage || i >= cells.Count) return;
            var c = cells[i];
            Stop(c);
            c.instance = Pool.Get(c.entry.prefab, SlotPosition(slot), Quaternion.identity, transform);
            var fx = c.instance.GetComponent<PooledFX>();
            if (fx != null) fx.Persistent = true;   // the gallery decides when it goes: replay or page turn
            c.systems = c.instance.GetComponentsInChildren<ParticleSystem>(true);
            if (c.peak < 0) c.peak = 0;
        }

        static void Stop(Cell c)
        {
            if (c.instance != null && c.instance.activeSelf) Pool.Release(c.instance);
            c.instance = null;
            c.systems = null;
        }

        /// <summary>Plays every page in turn to measure every effect, then logs <see cref="Report"/>.</summary>
        public void MeasureAll()
        {
            foreach (var c in cells) c.peak = -1;
            measuring = true;
            ShowPage(0);
        }

        public void ToggleBloom()
        {
            if (bloom != null) bloom.active = !bloom.active;
        }

        void Update()
        {
            int first = page * PerPage;
            for (int i = first; i < cells.Count && i < first + PerPage; i++)
            {
                var c = cells[i];
                if (c.systems == null) continue;
                int n = 0;
                foreach (var ps in c.systems)
                    if (ps != null) n += ps.particleCount;
                if (n > c.peak) c.peak = n;
            }
            if (Time.time < nextPlay) return;
            if (measuring)
            {
                if (page + 1 < PageCount) ShowPage(page + 1);
                else
                {
                    measuring = false;
                    nextPlay = Time.time + interval;
                    Debug.Log(Report());
                }
            }
            else if (loop) PlayPage();
        }

        bool Over(Cell c) => c.peak > library.ParticleLimit(c.entry.id) || c.lights > library.LightLimit(c.entry.id);

        /// <summary>Every effect with its peak particles and Light2D count against its budget.</summary>
        public string Report()
        {
            int over = 0, measured = 0;
            foreach (var c in cells)
            {
                if (c.peak >= 0) measured++;
                if (Over(c)) over++;
            }
            var sb = new StringBuilder($"[VFX Gallery] {over} of {measured} measured effects over budget " +
                                       $"(particles {library.particleBudget}, Light2D {library.lightBudget}; Tuyệt kỹ ×2)");
            foreach (var c in cells)
            {
                string id = c.entry.id;
                sb.Append($"\n  {(Over(c) ? "✗" : "✓")} {id}: particles {(c.peak < 0 ? "—" : c.peak.ToString())}/{library.ParticleLimit(id)}" +
                          $" · Light2D {c.lights}/{library.LightLimit(id)}");
            }
            return sb.ToString();
        }

        /// <summary>Light2D count of an effect prefab or instance.</summary>
        public static int Lights(GameObject fx) => fx != null ? fx.GetComponentsInChildren<Light2D>(true).Length : 0;

        // ------------------------------------------------------------------ on-screen
        void OnGUI()
        {
            if (library == null) return;
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.UpperCenter };
                textStyle = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
            }
            labelStyle.fontSize = textStyle.fontSize = Mathf.Max(11, Screen.height / 60);
            HandleKeys(Event.current);

            if (cam != null)
            {
                int first = page * PerPage;
                for (int slot = 0; slot < PerPage && first + slot < cells.Count; slot++)
                {
                    Vector3 sp = cam.WorldToScreenPoint(SlotPosition(slot) + Vector3.down * spacing * 0.38f);
                    var r = new Rect(sp.x - 120f, Screen.height - sp.y, 240f, labelStyle.fontSize * 3.4f);
                    if (GUI.Button(r, CellText(cells[first + slot], slot), labelStyle)) PlaySlot(slot);
                }
            }

            string bloomState = bloom == null ? "không có" : bloom.active ? "bật" : "tắt";
            string head = $"<b>VFX Gallery</b> · trang {page + 1}/{PageCount} · {cells.Count} hiệu ứng   " +
                          $"<color=#9a93a8>1–9 phát · Space cả trang · ←/→ trang · B Bloom ({bloomState}) · L lặp ({(loop ? "bật" : "tắt")}) · M đo tất cả · Tab bảng</color>";
            if (measuring) head += $"   <color=#ffe07a>đang đo… trang {page + 1}/{PageCount}</color>";
            GUI.Label(new Rect(10f, 8f, Screen.width - 20f, textStyle.fontSize * 2.6f), head, textStyle);

            if (!showTable) return;
            var box = new Rect(Screen.width * 0.56f, textStyle.fontSize * 3f, Screen.width * 0.42f, Screen.height - textStyle.fontSize * 4f);
            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.BeginArea(box);
            tableScroll = GUILayout.BeginScrollView(tableScroll);
            foreach (var c in cells) GUILayout.Label(TableRow(c), textStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        string Numbers(Cell c)
        {
            string id = c.entry.id;
            int pl = library.ParticleLimit(id), ll = library.LightLimit(id);
            string p = c.peak < 0 ? "—" : c.peak.ToString();
            return $"<color={(c.peak > pl ? "#ff6b5a" : "#cfe8d0")}>hạt {p}/{pl}</color> · " +
                   $"<color={(c.lights > ll ? "#ff6b5a" : "#cfe8d0")}>đèn {c.lights}/{ll}</color>";
        }

        string CellText(Cell c, int slot) =>
            $"<b>{slot + 1}. {c.entry.id}</b>{(library.IsUltimate(c.entry.id) ? " <color=#ffe07a>Tuyệt kỹ</color>" : "")}\n{Numbers(c)}";

        string TableRow(Cell c) => $"{(Over(c) ? "<color=#ff6b5a>✗</color>" : "✓")} {c.entry.id}   {Numbers(c)}";

        void HandleKeys(Event e)
        {
            if (e == null || e.type != EventType.KeyDown) return;
            var k = e.keyCode;
            if (k >= KeyCode.Alpha1 && k <= KeyCode.Alpha9) PlaySlot(k - KeyCode.Alpha1);
            else if (k >= KeyCode.Keypad1 && k <= KeyCode.Keypad9) PlaySlot(k - KeyCode.Keypad1);
            else if (k == KeyCode.Space) PlayPage();
            else if (k == KeyCode.RightArrow || k == KeyCode.PageDown) ShowPage(page + 1);
            else if (k == KeyCode.LeftArrow || k == KeyCode.PageUp) ShowPage(page - 1);
            else if (k == KeyCode.B) ToggleBloom();
            else if (k == KeyCode.L) loop = !loop;
            else if (k == KeyCode.M) MeasureAll();
            else if (k == KeyCode.Tab) showTable = !showTable;
            else return;
            e.Use();
        }
    }
}

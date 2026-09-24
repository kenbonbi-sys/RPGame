using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPG
{
    /// <summary>
    /// Developer console (plan T13), opened with ` (backquote). Cheats, teleporting between spots
    /// and zones, quests, saves, collider/hit-area display and time-to-kill measurement.
    /// Other systems can add commands with <see cref="Register"/>. Off when DevCheats is disabled.
    /// </summary>
    public class DebugConsole : MonoBehaviour
    {
        public static DebugConsole I { get; private set; }

        public bool IsOpen { get; private set; }
        public bool ShowHitboxes { get; private set; }

        class Command
        {
            public string help;
            public Action<string[]> run;
        }

        static readonly Dictionary<string, Command> Commands = new Dictionary<string, Command>();
        readonly List<string> output = new List<string>();
        readonly List<string> history = new List<string>();
        int historyIndex;
        string input = "";
        Vector2 scroll;
        bool focusField;

        // time to kill
        bool measuringTtk;
        readonly Dictionary<Health, float> firstHit = new Dictionary<Health, float>();
        readonly Dictionary<string, List<float>> ttk = new Dictionary<string, List<float>>();

        // hit areas drawn for a moment when "hitbox" is on
        struct Area
        {
            public Vector2 center, dir;
            public float radius, angle, until;
        }

        static readonly List<Area> Areas = new List<Area>();
        Material lineMat;

        /// <summary>Adds a console command. <paramref name="run"/> gets the words after the name.</summary>
        public static void Register(string name, string help, Action<string[]> run) =>
            Commands[name.ToLowerInvariant()] = new Command { help = help, run = run };

        /// <summary>Called by Combat when damage areas are used (drawn only while hitboxes are shown).</summary>
        public static void RecordArea(Vector2 center, float radius, Vector2 dir = default, float angle = 360f)
        {
            if (I == null || !I.ShowHitboxes) return;
            Areas.Add(new Area { center = center, radius = radius, dir = dir, angle = angle, until = Time.time + 0.25f });
        }

        void Awake()
        {
            I = this;
            RegisterDefaults();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            if (lineMat != null) Destroy(lineMat);
        }

        void OnEnable()
        {
            GameEvents.Damaged += OnDamaged;
            GameEvents.Died += OnDied;
            RenderPipelineManager.endCameraRendering += DrawHitboxes;
        }

        void OnDisable()
        {
            GameEvents.Damaged -= OnDamaged;
            GameEvents.Died -= OnDied;
            RenderPipelineManager.endCameraRendering -= DrawHitboxes;
        }

        bool Allowed
        {
            get
            {
                var cheats = GetComponent<DevCheats>();
                return cheats == null || cheats.enableCheats;
            }
        }

        void Update()
        {
            if (InputReader.ToggleConsole && Allowed) Toggle();
            Areas.RemoveAll(a => a.until < Time.time);
        }

        public void Toggle()
        {
            IsOpen = !IsOpen;
            focusField = IsOpen;
            if (GameManager.I != null) GameManager.I.SetMenu(IsOpen, false);
            if (IsOpen && output.Count == 0) Print("Gõ \"help\" để xem lệnh.");
        }

        public void Print(string line)
        {
            if (printOverride != null)
            {
                printOverride(line);
                return;
            }
            output.Add(line);
            if (output.Count > 200) output.RemoveAt(0);
            scroll.y = float.MaxValue;
        }

        /// <summary>
        /// Commands a player's machine runs itself online; every other one changes the world, so it
        /// goes to the server, which runs it for game masters only (Docs/KeHoach-Online.md).
        /// </summary>
        static readonly HashSet<string> OwnCommands = new HashSet<string> { "help", "clear", "net", "leave", "hitbox", "ttk", "stats", "host", "join", "players", "say" };

        /// <summary>Runs one command line (also used by tests).</summary>
        public void Execute(string line)
        {
            line = line.Trim();
            if (line.Length == 0) return;
            Print("> " + line);
            history.Add(line);
            historyIndex = history.Count;
            var words = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string name = words[0].ToLowerInvariant();
            if (!Commands.TryGetValue(name, out var cmd))
            {
                Print($"Không có lệnh \"{words[0]}\".");
                return;
            }
            if (GameSession.Mode == SessionMode.Client && printOverride == null && !OwnCommands.Contains(name))
            {
                OnlineSession.Ask(new ActRequest { kind = ActKind.Console, text = line });
                return;
            }
            try { cmd.run(words.Skip(1).ToArray()); }
            catch (Exception e) { Print("Lỗi: " + e.Message); }
        }

        // ------------------------------------------------------------------ run for another player (server)
        static PlayerController heroOverride;
        static Action<string> printOverride;

        /// <summary>Server: runs a game master's command line on their own hero; the output goes back to them.</summary>
        public static void RunFor(PlayerController hero, string line, Action<string> output)
        {
            if (I == null || hero == null) return;
            heroOverride = hero;
            printOverride = output;
            try { I.Execute(line); }
            finally
            {
                heroOverride = null;
                printOverride = null;
            }
        }

        /// <summary>Moves a hero and, when it is played on this screen, the camera with it.</summary>
        static void Move(PlayerController p, Vector2 to)
        {
            if (GameSession.Serving && p.Puppet) ServerPlayers.Teleport(p, to);
            else p.motor.Teleport(to);
            if (p.IsLocal && CameraRig.I != null) CameraRig.I.SnapToTarget();
        }

        // ------------------------------------------------------------------ commands
        /// <summary>The hero cheats act on: this screen's, or the game master's a server runs a command for.</summary>
        static PlayerController Hero => heroOverride != null ? heroOverride : Players.Local;

        /// <summary>The live enemy or boss closest to the hero.</summary>
        static Health NearestEnemy()
        {
            var hero = Hero;
            if (hero == null) return null;
            Vector2 p = hero.transform.position;
            Health best = null;
            float bestD = float.MaxValue;
            void Consider(Health h)
            {
                if (h == null || h.IsDead) return;
                float d = ((Vector2)h.transform.position - p).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = h;
                }
            }
            foreach (var e in EnemyBase.All) Consider(e.health);
            foreach (var b in BossBear.All) Consider(b.health);
            return best;
        }

        static int Int(string[] a, int i, int fallback) =>
            a.Length > i && int.TryParse(a[i], out int v) ? v : fallback;

        static float Float(string[] a, int i, float fallback) =>
            a.Length > i && float.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;

        void RegisterDefaults()
        {
            Register("help", "danh sách lệnh", a =>
            {
                foreach (var kv in Commands.OrderBy(k => k.Key)) Print($"  {kv.Key} — {kv.Value.help}");
            });
            Register("clear", "xóa màn hình", a => output.Clear());
            Register("heal", "hồi đầy máu, năng lượng, hồi chiêu", a =>
            {
                var p = Hero;
                p.health.Heal(99999, false);
                p.energy = p.maxEnergy;
                p.skills.ResetCooldowns();
                Print("Đã hồi đầy.");
            });
            Register("god", "bật/tắt bất tử", a =>
            {
                var h = Hero.health;
                h.invulnerable = !h.invulnerable;
                Print("Bất tử: " + (h.invulnerable ? "bật" : "tắt"));
            });
            Register("xp", "xp <n> — cộng kinh nghiệm", a => Hero.stats.AddXp(Int(a, 0, 100)));
            Register("level", "level <n> — đặt cấp", a =>
            {
                var s = Hero.stats;
                int target = Mathf.Clamp(Int(a, 0, s.level + 1), 1, s.Config.maxLevel);
                while (s.level < target) s.AddXp(s.XpToNext - s.xp);
                Print($"Cấp {s.level}.");
            });
            Register("gold", "gold <n> — cộng vàng", a =>
            {
                Hero.inventory.gold += Int(a, 0, 100);
                Print($"Vàng: {Hero.inventory.gold}");
            });
            Register("give", "give <item_id> [n] — thêm vật phẩm", a =>
            {
                var item = a.Length > 0 ? GameManager.I.db.Item(a[0]) : null;
                if (item == null)
                {
                    Print("Vật phẩm: " + string.Join(", ", GameManager.I.db.items.Where(i => i != null).Select(i => i.id)));
                    return;
                }
                Hero.inventory.Add(item, Int(a, 1, 1));
            });
            Register("kill", "kill [bán kính] — hạ quái quanh mình (mặc định 12)", a =>
            {
                float r = Float(a, 0, 12f);
                int n = 0;
                foreach (var e in EnemyBase.All.ToArray())
                    if (!e.IsDead && Vector2.Distance(e.transform.position, Hero.transform.position) <= r) { e.health.Kill(); n++; }
                // the boss only for a wide sweep (kill 20)
                foreach (var b in BossBear.All)
                    if (b != null && b.gameObject.activeInHierarchy && !b.health.IsDead && r >= 20f &&
                        Vector2.Distance(b.transform.position, Hero.transform.position) <= r) { b.health.Kill(); n++; }
                Print($"Đã hạ {n} quái.");
            });
            Register("tp", "tp <spot> — tới một điểm của vùng (spawn, forest, boss…)", a =>
            {
                var zone = ZoneRoot.Current;
                var spot = a.Length > 0 && zone != null ? zone.SpotOf(a[0]) : null;
                if (spot == null)
                {
                    Print("Điểm: " + (zone != null ? string.Join(", ", zone.spots.Select(s => s.id)) : "(chưa có vùng)"));
                    return;
                }
                Move(Hero, spot.position);
            });
            Register("zone", "zone <id> [điểm] — chuyển vùng", a =>
            {
                if (GameSession.Online)
                {
                    Print("Thế giới online hiện có một vùng.");
                    return;
                }
                var zone = a.Length > 0 ? GameManager.I.db.Zone(a[0]) : null;
                if (zone == null)
                {
                    Print("Vùng: " + string.Join(", ", GameManager.I.db.zones.Where(z => z != null).Select(z => z.id)));
                    return;
                }
                Toggle();
                SceneLoader.I.Travel(zone, a.Length > 1 ? a[1] : null);
            });
            Register("time", "time <0..1> — giờ trong ngày (0.5 = trưa)", a => DayNightCycle.I.time = Mathf.Repeat(Float(a, 0, 0.5f), 1f));
            Register("quest", "quest <id> start|complete|status", a =>
            {
                var q = Hero.quests;
                if (a.Length < 1)
                {
                    foreach (var s in GameManager.I.db.quests.Where(d => d != null)) Print($"  {s.id}: {q.Status(s.id)}");
                    return;
                }
                string op = a.Length > 1 ? a[1] : "status";
                if (op == "start") q.StartQuest(a[0]);
                else if (op == "complete") q.CompleteQuest(a[0]);
                Print($"{a[0]}: {q.Status(a[0])}");
            });
            Register("flag", "flag <tên> — đặt cờ nhiệm vụ", a =>
            {
                if (a.Length > 0) Hero.quests.SetFlag(a[0]);
            });
            Register("save", "save <1-3> — lưu (online: máy chủ lưu mọi nhân vật ngay)", a =>
            {
                if (GameSession.Serving && ServerPlayers.I != null)
                {
                    ServerPlayers.I.SaveAll("console");
                    Print($"Máy chủ đã lưu {ServerPlayers.I.Count} nhân vật.");
                    return;
                }
                Print(SaveManager.I.Save(Int(a, 0, 1)) ? "Đã lưu." : "Không lưu được.");
            });
            Register("load", "load <0-3> — tải (0 = tự động lưu)", a => SaveManager.I.Load(Int(a, 0, 1)));
            Register("host", "host [cổng] — mở thế giới online trên máy này (người khác vào bằng join)", a =>
                OnlineSession.Reboot(SessionMode.Host, null, (ushort)Mathf.Clamp(Int(a, 0, 0), 0, 65535)));
            Register("join", "join <địa chỉ> [cổng] — vào thế giới online của máy khác (dùng tên đăng nhập đã nhớ)", a =>
            {
                if (a.Length == 0)
                {
                    Print("Cần địa chỉ máy chủ, ví dụ: join 192.168.1.5");
                    return;
                }
                if (!LoginInfo.Ready && !LoginInfo.UseRemembered())
                {
                    Print("Chưa đăng nhập trên máy này: vào từ màn hình chính (Chơi online) trước.");
                    return;
                }
                OnlineSession.Reboot(SessionMode.Client, a[0], (ushort)Mathf.Clamp(Int(a, 1, 0), 0, 65535));
            });
            Register("leave", "leave — rời thế giới online, về màn hình chính", a => OnlineSession.Leave());
            Register("net", "trạng thái online", a => Print(OnlineSession.Status()));
            Register("players", "danh sách người trong thế giới online", a => Print(OnlineSession.PlayerList()));
            Register("say", "say <lời> — nói với mọi người trong thế giới (hoặc bấm Enter)", a =>
            {
                if (a.Length > 0) OnlineSession.Say(string.Join(" ", a));
            });
            Register("hitbox", "bật/tắt hiện collider và vùng sát thương", a =>
            {
                ShowHitboxes = !ShowHitboxes;
                Print("Hitbox: " + (ShowHitboxes ? "bật" : "tắt"));
            });
            Register("status", "status <bong|lanh|dien|doc|choang|troi|cham|nguyen|phanxet|sach> [số] [me] — trạng thái lên quái gần nhất (me: bản thân)", a =>
            {
                bool me = a.Contains("me");
                var target = me ? Hero.health : NearestEnemy();
                var s = target != null ? target.Status : null;
                if (s == null || a.Length == 0)
                {
                    Print(s == null ? "Không có mục tiêu." : "Trạng thái: bong lanh dien doc choang troi cham nguyen phanxet sach");
                    return;
                }
                Team from = me ? Team.Enemy : Team.Player;
                int n = Int(a, 1, 1);
                float sec = Float(a, 1, 0f);
                switch (a[0])
                {
                    case "bong": s.Burn(n, Hero.Attack(DamageType.Fire) * CombatConfig.Current.burnShare, from); break;
                    case "lanh": s.Chill(n); break;
                    case "dien": s.Charge(n, Hero.Attack(DamageType.Lightning), from, me ? null : Hero.gameObject); break;
                    case "doc": s.Poison(n, from); break;
                    case "choang": s.Stun(sec > 0 ? sec : 1.5f); break;
                    case "troi": s.Root(sec > 0 ? sec : 2f); break;
                    case "cham": s.Slow(0.3f, sec > 0 ? sec : 3f); break;
                    case "nguyen": s.Curse(sec > 0 ? sec : 8f); break;
                    case "phanxet": s.Judge(sec > 0 ? sec : 5f); break;
                    case "sach": s.Cleanse(); break;
                    default: Print("Không rõ trạng thái: " + a[0]); return;
                }
                Print($"{target.displayName}: {a[0]}");
            });
            Register("ttk", "bắt đầu / dừng đo thời gian hạ quái (TTK)", a => ToggleTtk());
            Register("stats", "FPS, số quái, số đối tượng trong pool", a =>
                Print($"FPS {1f / Mathf.Max(0.0001f, Time.smoothDeltaTime):0} · quái {EnemyBase.All.Count} · pool rảnh {Pool.FreeCount}"));
        }

        // ------------------------------------------------------------------ time to kill
        void ToggleTtk()
        {
            measuringTtk = !measuringTtk;
            if (measuringTtk)
            {
                firstHit.Clear();
                ttk.Clear();
                Print("Đo TTK: đánh quái rồi gõ \"ttk\" lần nữa để xem kết quả.");
                return;
            }
            Print("TTK (từ đòn đầu tới khi chết) · mục tiêu: quái thường 1.5–4 s, Tinh Anh 8–15 s, boss 2–4 phút");
            foreach (var kv in ttk)
                Print($"  {kv.Key}: trung bình {kv.Value.Average():0.0} s · nhanh nhất {kv.Value.Min():0.0} s · {kv.Value.Count} lần");
            if (ttk.Count == 0) Print("  (chưa hạ được gì)");
        }

        void OnDamaged(Health h, DamageInfo d, float amount)
        {
            if (measuringTtk && d.sourceTeam == Team.Player && h.team == Team.Enemy && !firstHit.ContainsKey(h))
                firstHit[h] = Time.time;
        }

        void OnDied(Health h)
        {
            if (!measuringTtk || !firstHit.TryGetValue(h, out float t0)) return;
            firstHit.Remove(h);
            string key = string.IsNullOrEmpty(h.displayName) ? h.name : h.displayName;
            if (!ttk.TryGetValue(key, out var list)) ttk[key] = list = new List<float>();
            list.Add(Time.time - t0);
        }

        // ------------------------------------------------------------------ hitboxes
        void DrawHitboxes(ScriptableRenderContext ctx, Camera cam)
        {
            if (!ShowHitboxes || cam != CameraRig.MainCam) return;
            if (lineMat == null)
            {
                var sh = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
                if (sh == null) return;
                lineMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }
            lineMat.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;
            GL.Begin(GL.LINES);
            foreach (var c in FindColliders())
            {
                GL.Color(c.gameObject.layer == Layers.Player ? Color.cyan : c.gameObject.layer == Layers.Enemy ? Color.red : Color.yellow);
                var b = c.bounds;
                if (c is CircleCollider2D circle) Circle(b.center, circle.radius * Mathf.Abs(c.transform.lossyScale.x), 360f, Vector2.right);
                else Rect(b.min, b.max);
            }
            GL.Color(new Color(1f, 0.5f, 0f));
            foreach (var a in Areas) Circle(a.center, a.radius, a.angle, a.dir == Vector2.zero ? Vector2.right : a.dir);
            GL.End();
            GL.PopMatrix();
        }

        static IEnumerable<Collider2D> FindColliders()
        {
            var hero = Hero;
            if (hero != null)
                foreach (var c in hero.GetComponentsInChildren<Collider2D>()) yield return c;
            foreach (var e in EnemyBase.All)
                foreach (var c in e.GetComponentsInChildren<Collider2D>()) yield return c;
            foreach (var b in BossBear.All)
                if (b != null && b.gameObject.activeInHierarchy)
                    foreach (var c in b.GetComponentsInChildren<Collider2D>()) yield return c;
        }

        static void Circle(Vector2 c, float r, float angle, Vector2 dir)
        {
            const int seg = 28;
            float a0 = Util.Angle(dir) - angle * 0.5f;
            Vector2 prev = c + Util.FromAngle(a0) * r;
            if (angle < 360f) Line(c, prev);
            for (int i = 1; i <= seg; i++)
            {
                Vector2 p = c + Util.FromAngle(a0 + angle * i / seg) * r;
                Line(prev, p);
                prev = p;
            }
            if (angle < 360f) Line(prev, c);
        }

        static void Rect(Vector2 min, Vector2 max)
        {
            Line(min, new Vector2(max.x, min.y));
            Line(new Vector2(max.x, min.y), max);
            Line(max, new Vector2(min.x, max.y));
            Line(new Vector2(min.x, max.y), min);
        }

        static void Line(Vector2 a, Vector2 b)
        {
            GL.Vertex3(a.x, a.y, 0);
            GL.Vertex3(b.x, b.y, 0);
        }

        // ------------------------------------------------------------------ UI (IMGUI: a developer tool)
        void OnGUI()
        {
            if (!IsOpen) return;
            float w = Screen.width, h = Screen.height * 0.42f;
            GUI.color = new Color(0, 0, 0, 0.82f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height / 55f), richText = true, wordWrap = true };
            GUILayout.BeginArea(new Rect(10, 8, w - 20, h - 16));
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var line in output) GUILayout.Label(line, style);
            GUILayout.EndScrollView();
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    Execute(input);
                    input = "";
                    e.Use();
                }
                else if (e.keyCode == KeyCode.UpArrow && history.Count > 0)
                {
                    historyIndex = Mathf.Max(0, historyIndex - 1);
                    input = history[historyIndex];
                    e.Use();
                }
                else if (e.keyCode == KeyCode.DownArrow && history.Count > 0)
                {
                    historyIndex = Mathf.Min(history.Count, historyIndex + 1);
                    input = historyIndex < history.Count ? history[historyIndex] : "";
                    e.Use();
                }
                else if (e.keyCode == KeyCode.BackQuote || e.character == '`')
                {
                    e.Use();   // the toggle key never lands in the text field
                }
            }
            GUI.SetNextControlName("ConsoleInput");
            input = GUILayout.TextField(input, new GUIStyle(GUI.skin.textField) { fontSize = style.fontSize });
            if (focusField)
            {
                GUI.FocusControl("ConsoleInput");
                focusField = false;
            }
            GUILayout.EndArea();
        }
    }
}

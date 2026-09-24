using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace RPG
{
    public enum GameState
    {
        Playing,
        Menu,
        Dialogue,
        Cinematic,
        Dead
    }

    /// <summary>Boots the game scene, owns global references and the game state.</summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        public GameDatabase db;
        public VFXLibrary vfx;
        /// <summary>
        /// The hero placed in the Core scene: the one this machine controls when playing offline.
        /// It only seeds <see cref="Players.Local"/>; code asks <see cref="Players"/> for heroes.
        /// </summary>
        [FormerlySerializedAs("player")] public PlayerController localPlayer;

        [Header("Start")]
        public bool showHelpOnStart = true;

        // places of the current zone (its ZoneRoot spots)
        public Transform respawnPoint => Spot("spawn");
        public Transform forestSpot => Spot("forest");
        public Transform bossSpot => Spot("boss");

        public GameState State
        {
            get
            {
                if (dead) return GameState.Dead;
                if (cinematic) return GameState.Cinematic;
                if (dialogue) return GameState.Dialogue;
                if (menu) return GameState.Menu;
                return GameState.Playing;
            }
        }

        bool dead, cinematic, dialogue, menu;
        bool attackCursor;

        void Awake()
        {
            I = this;
            GameEvents.Reset();
            Players.Reset();
            ZoneArea.ForgetAll();
            OnlineSession.ReadCommandLine();
            if (localPlayer != null)
            {
                // online, the server spawns a hero for every player (OnlineSession)
                if (GameSession.Mode == SessionMode.Offline) Players.SetLocal(localPlayer);
                else localPlayer.gameObject.SetActive(false);
            }
            Pool.ClearAll();
            SetupPhysics();
            Pool.SetHome(gameObject.scene);   // pooled objects outlive zone changes
            InputReader.Enable();
            Application.targetFrameRate = 120;
            // scenes built before these systems existed
            if (GetComponent<SaveManager>() == null) gameObject.AddComponent<SaveManager>();
            if (GetComponent<DialogueDirector>() == null) gameObject.AddComponent<DialogueDirector>();
            if (GetComponent<SceneLoader>() == null) gameObject.AddComponent<SceneLoader>();
            if (GetComponent<DebugConsole>() == null) gameObject.AddComponent<DebugConsole>();
            if (GetComponent<OnlineSession>() == null) gameObject.AddComponent<OnlineSession>();
            if (NetSmoke.Active && GetComponent<NetSmoke>() == null) gameObject.AddComponent<NetSmoke>();
            if (LoadBot.Active && GetComponent<LoadBot>() == null) gameObject.AddComponent<LoadBot>();
            if (BackdropShot.Active && GetComponent<BackdropShot>() == null) gameObject.AddComponent<BackdropShot>();
            if (GameSession.Online && GameSession.HasScreen && GetComponent<ChatInput>() == null) gameObject.AddComponent<ChatInput>();
            if (GameSession.Mode == SessionMode.Server) WithoutScreen();
        }

        /// <summary>
        /// A zone server has no screen: the HUD, the camera's post-processing, ambient particles
        /// and sound would only cost it time. Switched off before they wake up.
        /// </summary>
        void WithoutScreen()
        {
            foreach (var hud in FindObjectsByType<HUD>(FindObjectsInactive.Include)) hud.gameObject.SetActive(false);
            foreach (var v in FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include)) v.enabled = false;
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsInactive.Include)) l.enabled = false;
            var ambient = GameObject.Find("[Ambient]");
            if (ambient != null) ambient.SetActive(false);
            AudioListener.volume = 0f;
        }

        static void SetupPhysics()
        {
            for (int i = 0; i < 32; i++)
            {
                Physics2D.IgnoreLayerCollision(Layers.Projectile, i, true);
                Physics2D.IgnoreLayerCollision(Layers.Pickup, i, true);
            }
            Physics2D.IgnoreLayerCollision(Layers.Player, Layers.Player, true);   // heroes walk through each other
        }

        void Start()
        {
            SetCursor(false);
            if (GameSession.HasScreen && HUD.I != null) WorldMapUI.Ensure(HUD.I);
            if (SaveManager.HasPendingLoad) return;   // loading a save: no welcome
            bool screen = GameSession.Mode != SessionMode.Server && !AutoShot.Active && !NetSmoke.Active && !LoadBot.Active && !BackdropShot.Active;
            if (showHelpOnStart && screen && HUD.I != null && HUD.I.help != null) HUD.I.help.Show();
            GameEvents.RaiseLog("Chào mừng đến Làng Lá Xanh! Bấm F1 để xem hướng dẫn.", Palette.LogQuest);
        }

        void Update()
        {
            foreach (var p in Players.All) ZoneArea.Tick(p);
            HandleHotkeys();
            UpdateCursor();
        }

        void HandleHotkeys()
        {
            var hud = HUD.I;
            if (hud == null || dead) return;
            if (DebugConsole.I != null && DebugConsole.I.IsOpen)
            {
                if (InputReader.Cancel) DebugConsole.I.Toggle();
                return;   // typing in the console is not gameplay
            }
            if (ChatInput.I != null && ChatInput.I.IsOpen) return;   // nor typing a chat line
            var map = WorldMapUI.I;
            if (InputReader.Cancel)
            {
                if (map != null && map.IsOpen) map.Close();
                else if (hud.inventory != null && hud.inventory.IsOpen) hud.inventory.Close();
                else if (hud.journal != null && hud.journal.IsOpen) hud.journal.Close();
                else if (hud.character != null && hud.character.IsOpen) hud.character.Close();
                else if (hud.saves != null && hud.saves.IsOpen) hud.saves.Close();
                else if (hud.help != null && hud.help.IsOpen) hud.help.Close();
                else if (!dialogue && hud.pause != null) hud.pause.Toggle();
            }
            if (dialogue || cinematic) return;
            if (InputReader.ToggleHelp && hud.help != null) hud.help.Toggle();
            if (InputReader.ToggleBag && hud.inventory != null) hud.inventory.Toggle();
            if (InputReader.ToggleJournal && hud.journal != null) hud.journal.Toggle();
            if (InputReader.ToggleCharacter && hud.character != null) hud.character.Toggle();
            var me = Players.Local;
            if (InputReader.ToggleQuest && me != null && me.quests != null) me.quests.CycleFocus();
            if (map != null && me != null)
            {
                if (InputReader.ToggleMap) map.Toggle();
                // F at a Đá Truyền Tống (and no one to talk to): the map, ready to travel
                else if (InputReader.Interact && !map.IsOpen && !me.IsDead && Waystone.At(me.transform.position, 0f) != null &&
                         NPC.Nearest(me.transform.position, me.interactRadius) == null) map.Show();
            }
        }

        static readonly Color EnemyOutline = new Color(1.8f, 0.45f, 0.35f, 1f);
        static readonly Color NpcOutline = new Color(1.6f, 1.35f, 0.55f, 1f);
        SpriteStyle hovered;

        /// <summary>Attack cursor over enemies; a 1 px outline on whatever enemy or NPC is under the mouse.</summary>
        void UpdateCursor()
        {
            bool overEnemy = false;
            SpriteStyle style = null;
            Color outline = NpcOutline;
            if (State == GameState.Playing && !InputReader.PointerOverUI)
            {
                var c = Physics2D.OverlapCircle(InputReader.MouseWorld, 0.4f, Layers.EnemyMask | Layers.NPCMask);
                if (c != null)
                {
                    var h = c.GetComponentInParent<Health>();
                    overEnemy = h != null && h.team == Team.Enemy && !h.IsDead;
                    if (overEnemy) outline = EnemyOutline;
                    if (overEnemy || c.GetComponentInParent<NPC>() != null) style = c.GetComponentInParent<SpriteStyle>();
                }
            }
            if (overEnemy != attackCursor) SetCursor(overEnemy);
            if (style != hovered)
            {
                if (hovered != null) hovered.SetOutline(false, outline);
                hovered = style;
                if (hovered != null) hovered.SetOutline(true, outline);
            }
        }

        void SetCursor(bool attack)
        {
            attackCursor = attack;
            if (db == null) return;
            var tex = attack ? db.cursorAttack : db.cursorDefault;
            if (tex != null) Cursor.SetCursor(tex, attack ? new Vector2(3, 3) : Vector2.zero, CursorMode.Auto);
        }

        public void SetDialogue(bool on) => dialogue = on;

        /// <summary>A named place of the current zone (spawn, forest, boss…), for quests, respawn and tools.</summary>
        public Transform Spot(string id)
        {
            var zone = ZoneRoot.Current;
            return zone != null ? zone.SpotOf(id) : null;
        }
        public void SetCinematic(bool on) => cinematic = on;

        /// <summary>A menu opens or closes. Only a game that owns its clock (offline) stops time for it.</summary>
        public void SetMenu(bool on, bool pauseTime)
        {
            menu = on;
            TimeFX.Paused = on && pauseTime && GameSession.OwnsTime;
        }

        /// <summary>Seconds from a hero's fall to getting up again.</summary>
        public const float RespawnSeconds = 5.2f;

        /// <summary>
        /// A hero fell: they get up at the zone's spawn a few seconds later. The local one also sees
        /// the death screen. Online the server decides, and tells a player when their hero fell
        /// and got up (<see cref="LocalDowned"/>, <see cref="LocalRespawned"/>).
        /// </summary>
        public void OnPlayerDied(PlayerController p)
        {
            if (p == null || !GameSession.IsAuthority) return;
            if (p.IsLocal)
            {
                if (dead) return;
                dead = true;
            }
            else if (GameSession.Serving) ServerPlayers.SendTo(p, new ControlMsg { kind = ControlKind.Downed, value = RespawnSeconds });
            StartCoroutine(RespawnRoutine(p, p.IsLocal));
        }

        IEnumerator RespawnRoutine(PlayerController p, bool local)
        {
            yield return new WaitForSecondsRealtime(1.2f);
            if (local) GameEvents.RaisePlayerDowned(4f);
            yield return new WaitForSecondsRealtime(RespawnSeconds - 1.2f);
            if (p != null && RespawnPointFor(p, out Vector2 at))
            {
                p.Respawn(at);
                if (local && CameraRig.I != null) CameraRig.I.SnapToTarget();
                if (!local && GameSession.Serving) ServerPlayers.SendTo(p, new ControlMsg { kind = ControlKind.Respawn, pos = at });
            }
            if (!local) yield break;
            GameEvents.RaisePlayerRespawned();
            dead = false;
            GameEvents.RaiseLog($"Bạn đã hồi sinh tại {RespawnPlaceName(p)}.", Palette.LogInfo);
        }

        /// <summary>The name of where a hero gets up ("Đá Truyền Tống …" or the village).</summary>
        public static string RespawnPlaceName(PlayerController p)
        {
            var stone = p != null && p.waystones != null ? Waystone.Find(p.waystones.Last) : null;
            return stone != null ? "Đá Truyền Tống " + stone.displayName : "Làng Lá Xanh";
        }

        /// <summary>
        /// Where a fallen hero gets up: at the Đá Truyền Tống they touched last, else at the zone's
        /// spawn. False when the zone has neither.
        /// </summary>
        public bool RespawnPointFor(PlayerController p, out Vector2 at)
        {
            var stone = p != null && p.waystones != null ? Waystone.Find(p.waystones.Last) : null;
            if (stone != null)
            {
                at = stone.Arrival;
                return true;
            }
            var spawn = respawnPoint;
            at = spawn != null ? (Vector2)spawn.position : Vector2.zero;
            return spawn != null;
        }

        /// <summary>A player's machine online: the server says this screen's hero fell.</summary>
        public void LocalDowned(float respawnIn)
        {
            if (dead) return;
            dead = true;
            StartCoroutine(DownedScreen(respawnIn));
        }

        IEnumerator DownedScreen(float respawnIn)
        {
            yield return new WaitForSecondsRealtime(1.2f);
            if (dead) GameEvents.RaisePlayerDowned(Mathf.Max(0f, respawnIn - 1.2f));
        }

        /// <summary>A player's machine online: the server got this screen's hero up again.</summary>
        public void LocalRespawned()
        {
            if (CameraRig.I != null) CameraRig.I.SnapToTarget();
            if (!dead) return;
            GameEvents.RaisePlayerRespawned();
            dead = false;
            GameEvents.RaiseLog($"Bạn đã hồi sinh tại {RespawnPlaceName(Players.Local)}.", Palette.LogInfo);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

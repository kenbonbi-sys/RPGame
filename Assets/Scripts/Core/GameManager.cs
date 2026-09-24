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
            ZoneArea.Reset();
            if (localPlayer != null) Players.SetLocal(localPlayer);
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
        }

        static void SetupPhysics()
        {
            for (int i = 0; i < 32; i++)
            {
                Physics2D.IgnoreLayerCollision(Layers.Projectile, i, true);
                Physics2D.IgnoreLayerCollision(Layers.Pickup, i, true);
            }
        }

        void Start()
        {
            SetCursor(false);
            if (SaveManager.HasPendingLoad) return;   // loading a save: no welcome
            if (showHelpOnStart && HUD.I != null && HUD.I.help != null && !AutoShot.Active) HUD.I.help.Show();
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
            if (InputReader.Cancel)
            {
                if (hud.inventory != null && hud.inventory.IsOpen) hud.inventory.Close();
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

        /// <summary>A hero fell: they get up at the zone's spawn a few seconds later. The local one also sees the death screen.</summary>
        public void OnPlayerDied(PlayerController p)
        {
            if (p == null) return;
            if (p.IsLocal)
            {
                if (dead) return;
                dead = true;
            }
            StartCoroutine(RespawnRoutine(p, p.IsLocal));
        }

        IEnumerator RespawnRoutine(PlayerController p, bool local)
        {
            yield return new WaitForSecondsRealtime(1.2f);
            if (local) GameEvents.RaisePlayerDowned(4f);
            yield return new WaitForSecondsRealtime(4f);
            if (p != null && respawnPoint != null)
            {
                p.Respawn(respawnPoint.position);
                if (local && CameraRig.I != null) CameraRig.I.SnapToTarget();
            }
            if (!local) yield break;
            GameEvents.RaisePlayerRespawned();
            dead = false;
            GameEvents.RaiseLog("Bạn đã hồi sinh tại Làng Lá Xanh.", Palette.LogInfo);
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

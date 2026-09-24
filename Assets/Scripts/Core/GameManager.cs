using System.Collections;
using UnityEngine;

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
        public PlayerController player;

        [Header("World points")]
        public Transform respawnPoint;
        public Transform chiefSpot;
        public Transform girlSpot;
        public Transform forestSpot;
        public Transform bossSpot;

        [Header("Start")]
        public string startMusic = "music_forest";
        public string startAmbience = "amb_forest";
        public bool showHelpOnStart = true;

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
            Bestiary.Reset();
            Pool.ClearAll();
            SetupPhysics();
            Application.targetFrameRate = 120;
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
            AudioManager.PlayMusic(startMusic, 2f);
            AudioManager.PlayAmbience(startAmbience);
            if (showHelpOnStart && HUD.I != null && HUD.I.help != null && !AutoShot.Active) HUD.I.help.Show();
            GameEvents.RaiseLog("Chào mừng đến Làng Lá Xanh! Bấm F1 để xem hướng dẫn.", Palette.LogQuest);
        }

        void Update()
        {
            if (player != null) ZoneArea.Tick(player.transform.position);
            HandleHotkeys();
            UpdateCursor();
        }

        void HandleHotkeys()
        {
            var hud = HUD.I;
            if (hud == null || dead) return;
            if (InputReader.Cancel)
            {
                if (hud.inventory != null && hud.inventory.IsOpen) hud.inventory.Close();
                else if (hud.journal != null && hud.journal.IsOpen) hud.journal.Close();
                else if (hud.help != null && hud.help.IsOpen) hud.help.Close();
                else if (!dialogue && hud.pause != null) hud.pause.Toggle();
            }
            if (dialogue || cinematic) return;
            if (InputReader.ToggleHelp && hud.help != null) hud.help.Toggle();
            if (InputReader.ToggleBag && hud.inventory != null) hud.inventory.Toggle();
            if (InputReader.ToggleJournal && hud.journal != null) hud.journal.Toggle();
            if (InputReader.ToggleQuest && QuestSystem.I != null) QuestSystem.I.CycleFocus();
        }

        void UpdateCursor()
        {
            bool overEnemy = false;
            if (State == GameState.Playing && !InputReader.PointerOverUI)
            {
                var c = Physics2D.OverlapCircle(InputReader.MouseWorld, 0.4f, Layers.EnemyMask);
                overEnemy = c != null;
            }
            if (overEnemy != attackCursor) SetCursor(overEnemy);
        }

        void SetCursor(bool attack)
        {
            attackCursor = attack;
            if (db == null) return;
            var tex = attack ? db.cursorAttack : db.cursorDefault;
            if (tex != null) Cursor.SetCursor(tex, attack ? new Vector2(3, 3) : Vector2.zero, CursorMode.Auto);
        }

        public void SetDialogue(bool on) => dialogue = on;
        public void SetCinematic(bool on) => cinematic = on;

        public void SetMenu(bool on, bool pauseTime)
        {
            menu = on;
            TimeFX.Paused = on && pauseTime;
        }

        public void OnPlayerDied()
        {
            if (dead) return;
            dead = true;
            StartCoroutine(RespawnRoutine());
        }

        IEnumerator RespawnRoutine()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            if (HUD.I != null && HUD.I.death != null) HUD.I.death.Show(4f);
            yield return new WaitForSecondsRealtime(4f);
            if (player != null && respawnPoint != null)
            {
                player.Respawn(respawnPoint.position);
                if (CameraRig.I != null) CameraRig.I.SnapToTarget();
            }
            if (HUD.I != null && HUD.I.death != null) HUD.I.death.Hide();
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

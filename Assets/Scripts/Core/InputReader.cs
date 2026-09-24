using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPG
{
    /// <summary>
    /// The only place gameplay reads input from. Backed by the Input System actions of
    /// <see cref="GameControls"/>, so keys can be remapped (binding overrides saved in
    /// PlayerPrefs) and a gamepad can be added without touching gameplay code.
    /// </summary>
    public static class InputReader
    {
        const string OverridesKey = "rtt.controls.overrides";

        static InputActionAsset asset;
        static InputAction[] skills, potions, choices, cheats;
        static InputAction move, point, primary, secondary, interact;
        static InputAction cancel, help, bag, character, journal, questCycle, advance, console, map;

        static InputReader() => Build();

        /// <summary>Raised after keys were remapped or reset (key labels refresh).</summary>
        public static event System.Action BindingsChanged;

        /// <summary>The live actions (read them, remap them, save the overrides).</summary>
        public static InputActionAsset Asset
        {
            get
            {
                Build();
                return asset;
            }
        }

        static void Build()
        {
            if (asset != null) return;
            asset = GameControls.Create();
            asset.hideFlags = HideFlags.HideAndDontSave;   // only referenced from here: keep it through scene loads
            LoadBindingOverrides();
            InputAction A(string map, string name) => asset.FindActionMap(map).FindAction(name, true);
            InputAction[] All(string map, string[] names)
            {
                var r = new InputAction[names.Length];
                for (int i = 0; i < names.Length; i++) r[i] = A(map, names[i]);
                return r;
            }
            skills = All(GameControls.Gameplay, GameControls.SkillActions);
            potions = All(GameControls.Gameplay, GameControls.PotionActions);
            choices = All(GameControls.Menus, GameControls.ChoiceActions);
            cheats = All(GameControls.Debug, GameControls.CheatActions);
            move = A(GameControls.Gameplay, "Move");
            point = A(GameControls.Gameplay, "Point");
            primary = A(GameControls.Gameplay, "Primary");
            secondary = A(GameControls.Gameplay, "Secondary");
            interact = A(GameControls.Gameplay, "Interact");
            cancel = A(GameControls.Menus, "Cancel");
            help = A(GameControls.Menus, "Help");
            bag = A(GameControls.Menus, "Bag");
            character = A(GameControls.Menus, "Character");
            journal = A(GameControls.Menus, "Journal");
            questCycle = A(GameControls.Menus, "QuestCycle");
            map = A(GameControls.Menus, "Map");
            advance = A(GameControls.Menus, "Advance");
            console = A(GameControls.Debug, "Console");
        }

        /// <summary>Switches every action on (the game manager calls this at start-up).</summary>
        public static void Enable()
        {
            Build();
            if (!asset.enabled) asset.Enable();
        }

        static bool Down(InputAction a)
        {
            if (!Application.isPlaying) return false;
            Enable();
            return a.WasPressedThisFrame();
        }

        static bool Hold(InputAction a)
        {
            if (!Application.isPlaying) return false;
            Enable();
            return a.IsPressed();
        }

        // ------------------------------------------------------------------ remapping
        public static void SaveBindingOverrides()
        {
            PlayerPrefs.SetString(OverridesKey, Asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
            BindingsChanged?.Invoke();
        }

        static void LoadBindingOverrides()
        {
            string json = PlayerPrefs.GetString(OverridesKey, "");
            if (string.IsNullOrEmpty(json)) return;
            try { asset.LoadBindingOverridesFromJson(json); }
            catch (System.Exception e) { Debug.LogWarning("[Input] Ignoring broken key overrides: " + e.Message); }
        }

        public static void ResetBindings()
        {
            Asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(OverridesKey);
            BindingsChanged?.Invoke();
        }

        /// <summary>Key label for a skill slot as currently bound ("Q", "Space").</summary>
        public static string SkillLabel(int slot)
        {
            Build();
            return slot >= 0 && slot < skills.Length ? skills[slot].GetBindingDisplayString() : "";
        }

        // ------------------------------------------------------------------ gameplay
        public static Vector2 ArrowMove
        {
            get
            {
                if (!Application.isPlaying) return Vector2.zero;
                Enable();
                var v = move.ReadValue<Vector2>();
                return v.sqrMagnitude > 1 ? v.normalized : v;
            }
        }

        public static Vector2 MouseScreen
        {
            get
            {
                if (!Application.isPlaying) return Vector2.zero;
                Enable();
                return point.ReadValue<Vector2>();
            }
        }

        public static Vector2 MouseWorld
        {
            get
            {
                var cam = CameraRig.MainCam;
                if (cam == null) return Vector2.zero;
                Vector3 p = MouseScreen;
                p.z = -cam.transform.position.z;
                return cam.ScreenToWorldPoint(p);
            }
        }

        public static bool MoveHeld => Hold(secondary) || (Hold(primary) && !PointerOverUI);
        public static bool LeftPressed => Down(primary);
        public static bool LeftHeld => Hold(primary);
        public static bool RightPressed => Down(secondary);

        public static bool SkillPressed(int slot) => Down(skills[slot]);
        public static bool SkillHeld(int slot) => Hold(skills[slot]);
        public static bool PotionPressed(int slot) => Down(potions[slot]);
        /// <summary>Dialogue choice 1–3.</summary>
        public static bool OptionPressed(int index) => index >= 0 && index < choices.Length && Down(choices[index]);
        /// <summary>Developer cheats F5–F9 (heal, time, boss, village, kill).</summary>
        public static bool Cheat(int index) => index >= 0 && index < cheats.Length && Down(cheats[index]);

        public static bool Interact => Down(interact);
        public static bool ToggleBag => Down(bag);
        public static bool ToggleQuest => Down(questCycle);
        public static bool ToggleHelp => Down(help);
        public static bool ToggleJournal => Down(journal);
        public static bool ToggleCharacter => Down(character);
        public static bool ToggleMap => Down(map);
        public static bool Cancel => Down(cancel);
        public static bool Advance => Down(advance);
        public static bool ToggleConsole => Down(console);

        /// <summary>"Press any key" screens: any key or mouse button, whatever the bindings are.</summary>
        public static bool AnyKeyOrClick
        {
            get
            {
                var kb = Keyboard.current;
                var ms = Mouse.current;
                if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
                return ms != null && (ms.leftButton.wasPressedThisFrame || ms.rightButton.wasPressedThisFrame);
            }
        }

        public static bool PointerOverUI
        {
            get
            {
                var es = EventSystem.current;
                return es != null && es.IsPointerOverGameObject();
            }
        }
    }
}

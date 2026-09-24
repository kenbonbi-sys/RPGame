using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPG
{
    /// <summary>
    /// Thin wrapper around the Input System devices so gameplay code never touches
    /// Keyboard/Mouse directly (easy to rebind or add gamepad later).
    /// Layout follows the reference game: skills on Q W E R A S D + Space,
    /// potions on 1 2 3, movement with the mouse (hold) or the arrow keys.
    /// </summary>
    public static class InputReader
    {
        public static readonly Key[] SkillKeys = { Key.Q, Key.W, Key.E, Key.R, Key.A, Key.S, Key.D, Key.Space };
        public static readonly string[] SkillKeyLabels = { "Q", "W", "E", "R", "A", "S", "D", "Space" };
        public static readonly Key[] PotionKeys = { Key.Digit1, Key.Digit2, Key.Digit3 };

        static Keyboard Kb => Keyboard.current;
        static Mouse Ms => Mouse.current;

        public static Vector2 ArrowMove
        {
            get
            {
                if (Kb == null) return Vector2.zero;
                var v = Vector2.zero;
                if (Kb.leftArrowKey.isPressed) v.x -= 1;
                if (Kb.rightArrowKey.isPressed) v.x += 1;
                if (Kb.upArrowKey.isPressed) v.y += 1;
                if (Kb.downArrowKey.isPressed) v.y -= 1;
                return v.sqrMagnitude > 1 ? v.normalized : v;
            }
        }

        public static Vector2 MouseScreen => Ms != null ? Ms.position.ReadValue() : Vector2.zero;

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

        public static bool MoveHeld => Ms != null && (Ms.rightButton.isPressed || (Ms.leftButton.isPressed && !PointerOverUI));
        public static bool LeftPressed => Ms != null && Ms.leftButton.wasPressedThisFrame;
        public static bool LeftHeld => Ms != null && Ms.leftButton.isPressed;
        public static bool RightPressed => Ms != null && Ms.rightButton.wasPressedThisFrame;

        public static bool SkillPressed(int slot) => Kb != null && Kb[SkillKeys[slot]].wasPressedThisFrame;
        public static bool SkillHeld(int slot) => Kb != null && Kb[SkillKeys[slot]].isPressed;
        public static bool PotionPressed(int slot) => Kb != null && Kb[PotionKeys[slot]].wasPressedThisFrame;
        /// <summary>Dialogue choice 1–3 (same keys as the potions; potions are off while talking).</summary>
        public static bool OptionPressed(int index) => index >= 0 && index < PotionKeys.Length && PotionPressed(index);

        public static bool Pressed(Key k) => Kb != null && Kb[k].wasPressedThisFrame;
        public static bool Held(Key k) => Kb != null && Kb[k].isPressed;

        public static bool Interact => Pressed(Key.F);
        public static bool ToggleBag => Pressed(Key.B) || Pressed(Key.I);
        public static bool ToggleQuest => Pressed(Key.Tab);
        public static bool ToggleHelp => Pressed(Key.F1) || Pressed(Key.H);
        public static bool ToggleJournal => Pressed(Key.J);
        public static bool ToggleCharacter => Pressed(Key.C);
        public static bool Cancel => Pressed(Key.Escape);
        public static bool Advance => Pressed(Key.F) || Pressed(Key.Space) || Pressed(Key.Enter) || LeftPressed;

        public static bool AnyKeyOrClick
        {
            get
            {
                if (Kb != null && Kb.anyKey.wasPressedThisFrame) return true;
                return Ms != null && (Ms.leftButton.wasPressedThisFrame || Ms.rightButton.wasPressedThisFrame);
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

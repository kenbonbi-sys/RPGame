using UnityEngine;
using UnityEngine.InputSystem;

namespace RPG
{
    /// <summary>
    /// Every binding of the game as Input System actions (plan T17), defined once here.
    /// Layout of the reference game: skills on Q W E R A S D + Space, potions 1 2 3, walking with
    /// the right mouse button or the arrow keys, fighting with the left one, T for Tự Động. Players' remaps are stored as binding overrides
    /// (see <see cref="InputReader.SaveBindingOverrides"/>), never by changing these defaults.
    /// </summary>
    public static class GameControls
    {
        public const string Scheme = "Keyboard&Mouse";

        public const string Gameplay = "Gameplay";
        public const string Menus = "Menus";
        public const string Debug = "Debug";

        public static readonly string[] SkillActions = { "Skill1", "Skill2", "Skill3", "Skill4", "Skill5", "Skill6", "Skill7", "Dash" };
        public static readonly string[] PotionActions = { "Potion1", "Potion2", "Potion3" };
        public static readonly string[] ChoiceActions = { "Choice1", "Choice2", "Choice3" };
        public static readonly string[] CheatActions = { "CheatHeal", "CheatTime", "CheatBoss", "CheatVillage", "CheatKill" };

        public static InputActionAsset Create()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "GameControls";
            asset.AddControlScheme(Scheme).WithRequiredDevice("<Keyboard>").WithRequiredDevice("<Mouse>");

            var play = asset.AddActionMap(Gameplay);
            string[] skillKeys = { "q", "w", "e", "r", "a", "s", "d", "space" };
            for (int i = 0; i < SkillActions.Length; i++) Button(play, SkillActions[i], "<Keyboard>/" + skillKeys[i]);
            for (int i = 0; i < PotionActions.Length; i++) Button(play, PotionActions[i], $"<Keyboard>/{i + 1}");
            Button(play, "Interact", "<Keyboard>/f");
            Button(play, "Auto", "<Keyboard>/t");
            var move = play.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow", Scheme)
                .With("Down", "<Keyboard>/downArrow", Scheme)
                .With("Left", "<Keyboard>/leftArrow", Scheme)
                .With("Right", "<Keyboard>/rightArrow", Scheme);
            play.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position", expectedControlLayout: "Vector2");
            Button(play, "Primary", "<Mouse>/leftButton");
            Button(play, "Secondary", "<Mouse>/rightButton");

            var menus = asset.AddActionMap(Menus);
            Button(menus, "Cancel", "<Keyboard>/escape");
            Button(menus, "Help", "<Keyboard>/f1", "<Keyboard>/h");
            Button(menus, "Bag", "<Keyboard>/b", "<Keyboard>/i");
            Button(menus, "Character", "<Keyboard>/c");
            Button(menus, "Journal", "<Keyboard>/j");
            Button(menus, "QuestCycle", "<Keyboard>/tab");
            Button(menus, "Map", "<Keyboard>/m");
            Button(menus, "Spellbook", "<Keyboard>/k");
            Button(menus, "Advance", "<Keyboard>/f", "<Keyboard>/space", "<Keyboard>/enter", "<Mouse>/leftButton");
            for (int i = 0; i < ChoiceActions.Length; i++) Button(menus, ChoiceActions[i], $"<Keyboard>/{i + 1}");

            var debug = asset.AddActionMap(Debug);
            for (int i = 0; i < CheatActions.Length; i++) Button(debug, CheatActions[i], $"<Keyboard>/f{i + 5}");
            Button(debug, "Console", "<Keyboard>/backquote");
            return asset;
        }

        static void Button(InputActionMap map, string name, params string[] paths)
        {
            var a = map.AddAction(name, InputActionType.Button);
            foreach (var p in paths) a.AddBinding(p, groups: Scheme);
        }
    }
}

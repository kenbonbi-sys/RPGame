using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace RPG.EditorTools
{
    /// <summary>
    /// The title screen scene (Assets/Scenes/Title.unity, the build's first scene): a camera, an
    /// event system and a <see cref="TitleScreen"/> given the game's font, frames and, when
    /// Assets/Art/UI/title_backdrop.png exists, a picture behind the menu. The widgets themselves
    /// are made by TitleScreen when it starts.
    /// </summary>
    public static class TitleBuilder
    {
        public const string ScenePath = "Assets/Scenes/Title.unity";
        public const string BackdropPath = "Assets/Art/UI/title_backdrop.png";

        [MenuItem("Tools/RPG/Steps/8. Title Scene", priority = 108)]
        public static void BuildMenu()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            SceneBuilder.UpdateBuildSettings();
        }

        /// <summary>Builds the title scene (overwrites it); leaves it open.</summary>
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.07f, 0.05f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();

            var esGo = new GameObject("EventSystem", typeof(EventSystem));
            esGo.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var go = new GameObject("[Title]");
            var title = go.AddComponent<TitleScreen>();
            title.font = AssetFactory.Font;
            title.fontOutline = AssetFactory.FontOutline;
            title.windowSprite = ArtImporter.S("frame_wood");
            title.buttonSprite = ArtImporter.S("frame_panel");
            title.dividerSprite = ArtImporter.S("divider");
            title.whiteSprite = ArtImporter.S("white");
            title.backdrop = Backdrop();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtil.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorUtil.Written++;
            Debug.Log("[RPG] Title scene → " + ScenePath);
        }

        /// <summary>
        /// The picture behind the menu (a dusk shot of Làng Lá Xanh taken by the game:
        /// RungThiTham.exe -backdropshot, see Debug/BackdropShot.cs), imported as a crisp sprite.
        /// </summary>
        static Sprite Backdrop()
        {
            var importer = AssetImporter.GetAtPath(BackdropPath) as TextureImporter;
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Point)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(BackdropPath);
        }
    }
}

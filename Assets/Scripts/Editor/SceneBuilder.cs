using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RPG.EditorTools
{
    /// <summary>
    /// Tools/RPG/Build Everything — imports the art, creates data/VFX/prefabs and assembles
    /// Assets/Scenes/Game.unity (camera, lights, post-processing, managers, world, HUD).
    ///
    /// Authoring mode (default): only assets that are missing get created; existing prefabs,
    /// materials, data and the scene are left exactly as they are, so hand edits survive.
    /// Tools/RPG/Force Rebuild Everything regenerates all of it (the old behaviour).
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("Tools/RPG/Build Everything (create missing only)", priority = 20)]
        public static void BuildEverything()
        {
            EditorUtil.ResetStats();
            RunSteps();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) BuildScene();
            else
            {
                EditorUtil.Kept++;
                Debug.Log("[RPG] Scene kept: " + ScenePath + " (Steps/6 or Force Rebuild regenerates it).");
            }
            Debug.Log($"[RPG] Build Everything done ({EditorUtil.Stats}) → " + ScenePath);
        }

        [MenuItem("Tools/RPG/Force Rebuild Everything (overwrite)", priority = 21)]
        public static void ForceRebuildEverything()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Force Rebuild Everything",
                    "Regenerate every generated asset and the Game scene?\n\nHand edits to generated prefabs, VFX, " +
                    "materials, items, skills and the scene will be lost.", "Overwrite", "Cancel"))
                return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorUtil.ResetStats();
            EditorUtil.Forced(() =>
            {
                RunSteps();
                BuildScene();
            });
            Debug.Log($"[RPG] Force Rebuild Everything done ({EditorUtil.Stats}) → " + ScenePath);
        }

        static void RunSteps()
        {
            ProjectSetup.EnsureSortingLayers();
            ProjectSetup.EnsureLayers();
            ArtImporter.ImportAll();
            ArtImporter.ResetCache();
            AssetFactory.CreateAll();
            VFXFactory.BuildAll();
            PrefabFactory.BuildAll();
            AssetFactory.LinkLateReferences();
        }

        [MenuItem("Tools/RPG/Steps/6. Rebuild Scene Only (keeps prefabs)", priority = 106)]
        public static void RebuildSceneOnly()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            PrefabFactory.LoadExisting();
            BuildScene();
            Debug.Log("[RPG] Scene rebuilt → " + ScenePath);
        }

        static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var db = AssetFactory.Database;
            var vfx = VFXFactory.Library;

            // ---------------------------------------------------------------- managers
            var game = new GameObject("[Game]");
            var gm = game.AddComponent<GameManager>();
            gm.db = db;
            gm.vfx = vfx;
            var audio = game.AddComponent<AudioManager>();
            audio.library = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/Data/AudioLibrary.asset");
            game.AddComponent<TimeFX>();
            game.AddComponent<QuestSystem>();
            game.AddComponent<SaveManager>();
            var inv = game.AddComponent<Inventory>();
            inv.gold = 25;
            inv.stacks.Add(new Inventory.Stack { item = db.Item("potion_red"), count = 4 });
            inv.stacks.Add(new Inventory.Stack { item = db.Item("potion_blue"), count = 4 });
            inv.stacks.Add(new Inventory.Stack { item = db.Item("potion_green"), count = 2 });
            inv.stacks.Add(new Inventory.Stack { item = db.Item("sword"), count = 1 });
            inv.stacks.Add(new Inventory.Stack { item = db.Item("apple"), count = 3 });
            game.AddComponent<DevCheats>();
            game.AddComponent<AutoShot>();
            var screenFx = game.AddComponent<ScreenFX>();

            // ---------------------------------------------------------------- lighting
            var lightGo = new GameObject("Global Light 2D");
            var global = lightGo.AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = 1f;
            global.color = Color.white;
            global.targetSortingLayers = SortingLayer.layers.Select(l => l.id).ToArray();
            var dn = lightGo.AddComponent<DayNightCycle>();
            dn.globalLight = global;
            dn.time = 0.32f;
            dn.dayLength = 360f;

            // ---------------------------------------------------------------- post processing
            Volume Vol(string name, string profile, float weight, float priority)
            {
                var go = new GameObject(name);
                var v = go.AddComponent<Volume>();
                v.isGlobal = true;
                v.sharedProfile = AssetFactory.LoadProfile(profile);
                v.weight = weight;
                v.priority = priority;
                return v;
            }
            Vol("Global Volume", "GameVolume", 1f, 0f);
            screenFx.impactVolume = Vol("Impact Volume", "ImpactVolume", 0f, 5f);
            screenFx.dangerVolume = Vol("Danger Volume", "DangerVolume", 0f, 6f);

            // ---------------------------------------------------------------- world
            var world = new GameObject("World");
            var res = WorldBuilder.Build(world.transform);
            var player = res.player.GetComponent<PlayerController>();
            gm.player = player;
            gm.respawnPoint = res.playerSpawn;
            gm.chiefSpot = res.chief;
            gm.girlSpot = res.girl;
            gm.forestSpot = res.forestSpot;
            gm.bossSpot = res.bossSpot;

            // ---------------------------------------------------------------- camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8.4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.1f, 0.08f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            camGo.transform.position = new Vector3(res.playerSpawn.position.x, res.playerSpawn.position.y, -10);
            camGo.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            var rig = camGo.AddComponent<CameraRig>();
            rig.target = res.player.transform;
            rig.worldBounds = new Rect(0, 0, WorldBuilder.W, WorldBuilder.H);

            // ---------------------------------------------------------------- ambient
            var ambGo = new GameObject("[Ambient]");
            VFXFactory.BuildAmbient(ambGo);

            // ---------------------------------------------------------------- UI
            var ui = UIBuilder.Build();
            screenFx.flashImage = ui.flash;
            ui.minimap.ground = res.ground;
            ui.minimap.tallGrass = res.tall;
            ui.minimap.dirt = res.dirt;
            ui.minimap.obstaclesRoot = res.props;

            EditorUtility.SetDirty(gm);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtil.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }
    }
}

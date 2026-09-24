using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RPG.EditorTools
{
    /// <summary>
    /// Tools/RPG/Build Everything — imports the art, creates data/VFX/prefabs and assembles the
    /// scenes: Assets/Scenes/Core.unity (managers, hero, camera, light, post-processing, HUD —
    /// always loaded) and one scene per zone in Assets/Scenes/Zones (terrain, props, NPCs,
    /// enemies, boss, zone areas and a ZoneRoot), loaded next to Core by the SceneLoader.
    ///
    /// Authoring mode (default): only assets and scenes that are missing get created; existing
    /// prefabs, materials, data and scenes are left exactly as they are, so hand edits survive.
    /// Tools/RPG/Force Rebuild Everything regenerates all of it (the old behaviour).
    /// </summary>
    public static class SceneBuilder
    {
        public const string CoreScenePath = "Assets/Scenes/Core.unity";
        public const string ZoneFolder = "Assets/Scenes/Zones";
        /// <summary>The scene to open to play the game.</summary>
        public const string ScenePath = CoreScenePath;

        public static string ZoneScenePath(ZoneDef zone) => $"{ZoneFolder}/{zone.sceneName}.unity";

        /// <summary>Core first (build index 0), then every zone of the database.</summary>
        public static string[] AllScenePaths()
        {
            var list = new List<string> { CoreScenePath };
            var db = AssetFactory.Database;
            if (db != null)
                foreach (var z in db.zones)
                    if (z != null && !string.IsNullOrEmpty(z.sceneName)) list.Add(ZoneScenePath(z));
            return list.ToArray();
        }

        [MenuItem("Tools/RPG/Build Everything (create missing only)", priority = 20)]
        public static void BuildEverything()
        {
            EditorUtil.ResetStats();
            RunSteps();
            BuildScenes(false);
            Debug.Log($"[RPG] Build Everything done ({EditorUtil.Stats}) → " + string.Join(", ", AllScenePaths()));
        }

        [MenuItem("Tools/RPG/Force Rebuild Everything (overwrite)", priority = 21)]
        public static void ForceRebuildEverything()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Force Rebuild Everything",
                    "Regenerate every generated asset and scene?\n\nHand edits to generated prefabs, VFX, " +
                    "materials, items, abilities, quests and the scenes will be lost.", "Overwrite", "Cancel"))
                return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorUtil.ResetStats();
            EditorUtil.Forced(() =>
            {
                RunSteps();
                BuildScenes(true);
            });
            Debug.Log($"[RPG] Force Rebuild Everything done ({EditorUtil.Stats})");
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

        [MenuItem("Tools/RPG/Steps/6. Rebuild Scenes (keeps prefabs)", priority = 106)]
        public static void RebuildSceneOnly()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            PrefabFactory.LoadExisting();
            BuildScenes(true);
            Debug.Log("[RPG] Scenes rebuilt → " + string.Join(", ", AllScenePaths()));
        }

        /// <summary>Builds the missing scenes (or all of them when <paramref name="all"/>), then the build settings.</summary>
        static void BuildScenes(bool all)
        {
            // the VFX Gallery tool scene first, so Core is the scene left open at the end
            if (all || AssetDatabase.LoadAssetAtPath<SceneAsset>(VFXGalleryBuilder.ScenePath) == null) VFXGalleryBuilder.Build();
            else EditorUtil.Kept++;
            var db = AssetFactory.Database;
            foreach (var zone in db.zones)
            {
                if (zone == null) continue;
                string path = ZoneScenePath(zone);
                if (all || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) BuildZone(zone, path);
                else EditorUtil.Kept++;
            }
            if (all || AssetDatabase.LoadAssetAtPath<SceneAsset>(CoreScenePath) == null) BuildCore();
            else EditorUtil.Kept++;
            EditorBuildSettings.scenes = AllScenePaths().Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            AssetDatabase.SaveAssets();
        }

        // ================================================================== zone
        static void BuildZone(ZoneDef zone, string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var world = new GameObject("World");
            var root = world.AddComponent<ZoneRoot>();
            var res = WorldBuilder.Build(world.transform);
            root.def = zone;
            root.bounds = new Rect(0, 0, WorldBuilder.W, WorldBuilder.H);
            void Spot(string id, Transform t)
            {
                if (t != null) root.spots.Add(new ZoneRoot.Spot { id = id, point = t });
            }
            Spot("spawn", res.playerSpawn);
            Spot("forest", res.forestSpot);
            Spot("boss", res.bossSpot);
            Spot("chief", res.chief);
            Spot("girl", res.girl);
            root.ground = res.ground;
            root.tallGrass = res.tall;
            root.dirt = res.dirt;
            root.obstacles = res.props;
            EditorUtility.SetDirty(root);
            EditorUtil.EnsureFolder(ZoneFolder);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            EditorUtil.Written++;
        }

        // ================================================================== core
        static void BuildCore()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var db = AssetFactory.Database;
            var vfx = VFXFactory.Library;
            Vector3 spawn = new Vector3(18.5f, 15.2f, 0);   // only a preview position; the SceneLoader places the hero

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
            game.AddComponent<DialogueDirector>();
            var loader = game.AddComponent<SceneLoader>();
            loader.startZone = db.startZone;
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

            // ---------------------------------------------------------------- hero
            var playerGo = (GameObject)PrefabUtility.InstantiatePrefab(PrefabFactory.Player);
            playerGo.name = "Player";
            playerGo.transform.position = spawn;
            gm.player = playerGo.GetComponent<PlayerController>();

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
            camGo.transform.position = new Vector3(spawn.x, spawn.y, -10);
            camGo.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;
            var rig = camGo.AddComponent<CameraRig>();
            rig.target = playerGo.transform;
            rig.worldBounds = new Rect(0, 0, WorldBuilder.W, WorldBuilder.H);   // replaced by each zone's bounds

            // ---------------------------------------------------------------- ambient
            var ambGo = new GameObject("[Ambient]");
            VFXFactory.BuildAmbient(ambGo);

            // ---------------------------------------------------------------- UI
            var ui = UIBuilder.Build();
            screenFx.flashImage = ui.flash;
            loader.fade = ui.loading;
            loader.fadeTitle = ui.loadingTitle;

            EditorUtility.SetDirty(gm);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtil.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, CoreScenePath);
            EditorUtil.Written++;
        }
    }
}

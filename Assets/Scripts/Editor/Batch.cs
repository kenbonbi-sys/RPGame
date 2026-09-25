using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>
    /// Entry points for command-line (batchmode) builds:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RPG.EditorTools.Batch.BuildAll
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RPG.EditorTools.Batch.ForceBuildAll
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RPG.EditorTools.Batch.RebuildScene
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RPG.EditorTools.Batch.RebuildCore
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RPG.EditorTools.Batch.RebuildZones
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RPG.EditorTools.Batch.MoveHeroState
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RPG.EditorTools.Batch.BuildOnline
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RPG.EditorTools.Batch.BuildTitle
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RPG.EditorTools.Batch.BuildPlayer
    /// </summary>
    public static class Batch
    {
        /// <summary>Project settings + TextMeshPro resources (run once).</summary>
        public static void Setup()
        {
            ProjectSetup.Run();
            Debug.Log("[RPG] Batch.Setup done");
        }

        /// <summary>Art import, data, VFX, prefabs and the game scene — creates missing assets only.</summary>
        public static void BuildAll()
        {
            SceneBuilder.BuildEverything();
        }

        /// <summary>Same as BuildAll but overwrites every generated asset and the scene.</summary>
        public static void ForceBuildAll()
        {
            SceneBuilder.ForceRebuildEverything();
        }

        /// <summary>Regenerates only the Game scene from the prefabs on disk.</summary>
        public static void RebuildScene()
        {
            SceneBuilder.RebuildSceneOnly();
        }

        /// <summary>Regenerates only the Core scene (the HUD lives there) from the prefabs on disk; the zones and the title stay.</summary>
        public static void RebuildCore()
        {
            SceneBuilder.RebuildCoreOnly();
        }

        /// <summary>Regenerates only the zone scenes (the world map) from the prefabs on disk; Core and the title stay.</summary>
        public static void RebuildZones()
        {
            SceneBuilder.RebuildZonesOnly();
        }

        /// <summary>
        /// Art import, data, VFX and prefabs (creating only what is missing, like Build Everything),
        /// then the zone scenes regenerated: new regions of the map without touching Core or the title.
        /// </summary>
        public static void GrowWorld()
        {
            SceneBuilder.BuildAssetsOnly();
            SceneBuilder.RebuildZonesOnly();
            Debug.Log($"[RPG] Batch.GrowWorld done ({EditorUtil.Stats})");
        }

        /// <summary>Builds the NetHero prefab and links the network prefabs into the GameDatabase (online phase 1).</summary>
        public static void BuildOnline()
        {
            PrefabFactory.BuildOnline();
            Debug.Log("[RPG] Batch.BuildOnline done");
        }

        /// <summary>Builds the title screen scene and puts it first in the build (online phase 2).</summary>
        public static void BuildTitle()
        {
            TitleBuilder.Build();
            SceneBuilder.UpdateBuildSettings();
            Debug.Log("[RPG] Batch.BuildTitle done");
        }

        /// <summary>Moves the bag and the quest log of an older Core scene onto the Player prefab (online phase 0).</summary>
        public static void MoveHeroState()
        {
            SceneBuilder.MoveHeroStateOntoPlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[RPG] Batch.MoveHeroState done");
        }

        /// <summary>Builds Builds/Windows/RungThiTham.exe; from the command line, -buildPath "D:\out\RungThiTham.exe" builds elsewhere.</summary>
        [MenuItem("Tools/RPG/Build Windows Player", priority = 40)]
        public static void BuildPlayer()
        {
            var args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-buildPath");
            var opts = new BuildPlayerOptions
            {
                scenes = SceneBuilder.AllScenePaths(),
                locationPathName = at >= 0 && at + 1 < args.Length ? args[at + 1] : "Builds/Windows/RungThiTham.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[RPG] Player build: {s.result}, {s.totalSize / (1024 * 1024)} MB, {s.totalTime.TotalSeconds:0}s → {opts.locationPathName}");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}

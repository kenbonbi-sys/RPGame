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
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RPG.EditorTools.Batch.MoveHeroState
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RPG.EditorTools.Batch.BuildOnline
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

        /// <summary>Builds the NetHero prefab and links the network prefabs into the GameDatabase (online phase 1).</summary>
        public static void BuildOnline()
        {
            PrefabFactory.BuildOnline();
            Debug.Log("[RPG] Batch.BuildOnline done");
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

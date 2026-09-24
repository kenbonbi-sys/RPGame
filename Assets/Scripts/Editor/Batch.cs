using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>
    /// Entry points for command-line (batchmode) builds:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RPG.EditorTools.Batch.BuildAll
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

        /// <summary>Art import, data, VFX, prefabs and the game scene.</summary>
        public static void BuildAll()
        {
            SceneBuilder.BuildEverything();
        }

        [MenuItem("Tools/RPG/Build Windows Player", priority = 40)]
        public static void BuildPlayer()
        {
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.ScenePath },
                locationPathName = "Builds/Windows/RungThiTham.exe",
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

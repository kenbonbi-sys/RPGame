using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RPG.EditorTools
{
    /// <summary>
    /// VFX Gallery (plan T21). Tools/RPG/VFX Gallery builds Assets/Scenes/Tools/VFXGallery.unity when
    /// it is missing (Force Rebuild Everything remakes it), opens it and presses Play. Tools/RPG/VFX Budget Report measures every effect
    /// without Play Mode (particles simulated in the editor) and logs it against the budget of the
    /// VFXLibrary. The gallery scene is a tool: it is not in the build settings.
    /// </summary>
    public static class VFXGalleryBuilder
    {
        public const string ScenePath = "Assets/Scenes/Tools/VFXGallery.unity";

        [MenuItem("Tools/RPG/VFX Gallery", priority = 41)]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Build();
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
        }

        /// <summary>Camera, a dim global light, the game's post-processing and the gallery.</summary>
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.None;

            var lightGo = new GameObject("Global Light 2D");
            var global = lightGo.AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = 0.5f;   // dusk, so each effect's own Light2D shows
            global.color = Color.white;
            global.targetSortingLayers = SortingLayer.layers.Select(l => l.id).ToArray();

            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetFactory.LoadProfile("GameVolume");

            var gallery = new GameObject("VFX Gallery").AddComponent<VFXGallery>();
            gallery.library = VFXFactory.Library;
            gallery.volume = volume;
            gallery.cam = cam;
            cam.orthographicSize = gallery.rows * gallery.spacing * 0.5f + 1.2f;

            EditorUtil.EnsureFolder("Assets/Scenes/Tools");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorUtil.Written++;
        }

        // ------------------------------------------------------------------ budget report
        public struct Row
        {
            public string id;
            public int peak, particleLimit, lights, lightLimit;
            public bool Over => peak > particleLimit || lights > lightLimit;
        }

        [MenuItem("Tools/RPG/VFX Budget Report", priority = 42)]
        public static void LogReport()
        {
            var lib = VFXFactory.Library;
            if (lib == null)
            {
                Debug.LogWarning("[VFX] No VFX library at Assets/Data/VFXLibrary.asset");
                return;
            }
            var rows = MeasureAll();
            var sb = new StringBuilder($"[VFX] Budget report: {rows.Count(r => r.Over)} of {rows.Count} effects over budget " +
                                       $"(particles {lib.particleBudget}, Light2D {lib.lightBudget}; Tuyệt kỹ ×2)");
            foreach (var r in rows.OrderByDescending(r => r.Over).ThenBy(r => r.id))
                sb.Append($"\n  {(r.Over ? "✗" : "✓")} {r.id}: particles {r.peak}/{r.particleLimit} · Light2D {r.lights}/{r.lightLimit}");
            Debug.Log(sb.ToString());
        }

        /// <summary>Every effect of the library with its simulated particle peak and Light2D count.</summary>
        public static List<Row> MeasureAll()
        {
            var rows = new List<Row>();
            var lib = VFXFactory.Library;
            if (lib == null) return rows;
            foreach (var e in lib.entries)
            {
                if (e == null || e.prefab == null) continue;
                rows.Add(new Row
                {
                    id = e.id,
                    peak = SimulatePeak(e.prefab),
                    particleLimit = lib.ParticleLimit(e.id),
                    lights = VFXGallery.Lights(e.prefab),
                    lightLimit = lib.LightLimit(e.id)
                });
            }
            return rows;
        }

        /// <summary>
        /// Most particles alive at once while the effect plays: its particle systems are simulated
        /// in steps over its lifetime (or 3 s for effects that stay until released). No Play Mode.
        /// </summary>
        public static int SimulatePeak(GameObject prefab, float step = 1f / 30f)
        {
            var pooled = prefab.GetComponent<PooledFX>();
            float seconds = pooled != null && pooled.lifetime > 0f ? Mathf.Min(pooled.lifetime, 5f) : 3f;
            var go = Object.Instantiate(prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var all = go.GetComponentsInChildren<ParticleSystem>(true);
                // simulate from the top systems only; they drive their child systems and sub-emitters
                var tops = all.Where(ps => ps.transform.parent == null || ps.transform.parent.GetComponentInParent<ParticleSystem>() == null).ToArray();
                int peak = 0;
                bool restart = true;
                for (float t = 0f; t < seconds; t += step)
                {
                    foreach (var ps in tops) ps.Simulate(step, true, restart, true);
                    restart = false;
                    int n = 0;
                    foreach (var ps in all) n += ps.particleCount;
                    if (n > peak) peak = n;
                }
                return peak;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}

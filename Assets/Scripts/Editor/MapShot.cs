using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RPG.EditorTools
{
    /// <summary>
    /// Renders the whole world map from above into PNGs (terrain, props and actors as placed), to
    /// check a generated map without playing it:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RPG.EditorTools.MapShot.Batch -mapShotDir "C:\shots"
    /// Writes map_full.png (the whole map, 4 px per tile) and map_part_N.png (slices at full
    /// sprite resolution, 16 px per tile). Opens the first zone scene without saving it.
    /// </summary>
    public static class MapShot
    {
        const int SlicePixels = 1600;

        [MenuItem("Tools/RPG/Map Screenshot", priority = 45)]
        public static void FromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Render(Path.Combine(Application.dataPath, "..", "Builds", "MapShot"));
        }

        public static void Batch()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-mapShotDir");
            string dir = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(Application.dataPath, "..", "Builds", "MapShot");
            Render(dir);
        }

        static void Render(string dir)
        {
            var db = AssetFactory.Database;
            if (db == null || db.zones.Count == 0 || db.zones[0] == null) return;
            var scene = EditorSceneManager.OpenScene(SceneBuilder.ZoneScenePath(db.zones[0]), OpenSceneMode.Single);
            var root = Object.FindAnyObjectByType<ZoneRoot>();
            Rect b = root != null ? root.bounds : new Rect(0, 0, WorldBuilder.W, WorldBuilder.H);
            Directory.CreateDirectory(dir);

            var lightGo = new GameObject("MapShotLight");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = Color.white;
            var camGo = new GameObject("MapShotCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.1f, 0.08f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            // the whole map, small
            Save(cam, new Rect(b.xMin, b.yMin, b.width, b.height), Mathf.RoundToInt(b.width * 4), Mathf.RoundToInt(b.height * 4),
                 Path.Combine(dir, "map_full.png"));
            // slices at full resolution
            float sliceWidth = SlicePixels / 16f;
            int n = 0;
            for (float x = b.xMin; x < b.xMax; x += sliceWidth)
            {
                float w = Mathf.Min(sliceWidth, b.xMax - x);
                Save(cam, new Rect(x, b.yMin, w, b.height), Mathf.RoundToInt(w * 16), Mathf.RoundToInt(b.height * 16),
                     Path.Combine(dir, $"map_part_{n++}.png"));
            }
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);
            Debug.Log($"[RPG] Map shots in {dir}");
        }

        static void Save(Camera cam, Rect area, int width, int height, string path)
        {
            cam.orthographicSize = area.height / 2f;
            cam.aspect = area.width / area.height;
            cam.transform.position = new Vector3(area.center.x, area.center.y, -10f);
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}

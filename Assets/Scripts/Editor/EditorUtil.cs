using System.IO;
using UnityEditor;
using UnityEngine;

namespace RPG.EditorTools
{
    public static class EditorUtil
    {
        // ------------------------------------------------------------------ authoring mode
        /// <summary>
        /// False (authoring mode, the default): generators only create assets that are missing and
        /// leave existing ones untouched, so hand edits survive. True only inside <see cref="Forced"/>
        /// (Tools/RPG/Force Rebuild Everything), where every generated asset is written again.
        /// </summary>
        public static bool Overwrite { get; private set; }

        /// <summary>Assets written / left alone since the last <see cref="ResetStats"/>.</summary>
        public static int Written, Kept;

        public static void ResetStats() => Written = Kept = 0;

        public static string Stats => $"{Written} written, {Kept} kept";

        /// <summary>Runs a build step with overwriting enabled.</summary>
        public static void Forced(System.Action step)
        {
            bool prev = Overwrite;
            Overwrite = true;
            try { step(); }
            finally { Overwrite = prev; }
        }

        /// <summary>True when <paramref name="existing"/> must be left alone (authoring mode).</summary>
        public static bool Keep(Object existing)
        {
            if (existing == null || Overwrite) return false;
            Kept++;
            return true;
        }

        /// <summary>Sets a reference unless authoring mode keeps a value that is already there.</summary>
        public static void Assign<T>(ref T field, T value) where T : Object
        {
            if (field == null || Overwrite) field = value;
        }

        public static void EnsureFolder(string path)
        {
            path = path.Replace("\\", "/").TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>
        /// Creates an asset at path. An existing asset is kept in authoring mode, or overwritten
        /// in place (same GUID) when forced.
        /// </summary>
        public static T SaveAsset<T>(T obj, string path) where T : Object
        {
            EnsureFolder(Path.GetDirectoryName(path));
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (Keep(existing)) return existing;
            Written++;
            if (existing != null && existing != obj)
            {
                EditorUtility.CopySerialized(obj, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            if (existing == null) AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        /// <summary>
        /// Saves a built GameObject as a prefab. In authoring mode an existing prefab wins: the
        /// freshly built object is discarded and the prefab on disk is returned unchanged.
        /// </summary>
        public static GameObject SavePrefab(GameObject go, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (Keep(existing))
            {
                Object.DestroyImmediate(go);
                return existing;
            }
            Written++;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        public static GameObject Child(GameObject parent, string name, Vector3 localPos = default)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            return go;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        public static AnimationCurve Curve(params float[] kv)
        {
            var c = new AnimationCurve();
            for (int i = 0; i + 1 < kv.Length; i += 2) c.AddKey(new Keyframe(kv[i], kv[i + 1]));
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0);
            return c;
        }

        public static Gradient Grad(params (float t, Color c)[] keys)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[keys.Length];
            var ak = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                ck[i] = new GradientColorKey(keys[i].c, keys[i].t);
                ak[i] = new GradientAlphaKey(keys[i].c.a, keys[i].t);
            }
            g.SetKeys(ck, ak);
            return g;
        }

        public static Color Hex(string hex, float a = 1f)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c);
            c.a = a;
            return c;
        }
    }
}

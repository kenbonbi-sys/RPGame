using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>
    /// Applies Assets/Art/art_manifest.json (written by Tools/ArtGen/build_all.py):
    /// texture import settings, sprite slicing with pivots/borders and SpriteAnimSet assets.
    /// Sprite IDs are deterministic so re-importing keeps prefab references intact.
    /// </summary>
    public static class ArtImporter
    {
        const string ManifestPath = "Assets/Art/art_manifest.json";
        public const string AnimFolder = "Assets/Data/Anims";

        [Serializable]
        class SpriteEntry
        {
            public string name;
            public int x, y, w, h;
            public float[] pivot;
            public int[] border;
        }

        [Serializable]
        class TextureEntry
        {
            public string path;
            public string filter;
            public float ppu;
            public string kind;
            public List<SpriteEntry> sprites;
        }

        [Serializable]
        class AnimEntry
        {
            public string set;
            public string name;
            public List<string> frames;
            public float fps;
            public bool loop;
        }

        [Serializable]
        class Manifest
        {
            public List<TextureEntry> textures;
            public List<AnimEntry> anims;
        }

        static Dictionary<string, Sprite> _sprites;

        [MenuItem("Tools/RPG/Steps/2. Import Art (slice sprites + anims)", priority = 102)]
        public static void ImportAll()
        {
            var m = Load();
            if (m == null) return;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var t in m.textures) ConfigureTexture(t);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            _sprites = null;
            BuildAnimSets(m);
            ConfigureCursor();
            AssetDatabase.SaveAssets();
            Debug.Log($"[RPG] Imported {m.textures.Count} textures, {m.anims.Count} animations.");
        }

        static Manifest Load()
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError("[RPG] Missing " + ManifestPath + " — run Tools/ArtGen/build_all.py first.");
                return null;
            }
            return JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
        }

        static void ConfigureTexture(TextureEntry t)
        {
            var ti = AssetImporter.GetAtPath(t.path) as TextureImporter;
            if (ti == null)
            {
                Debug.LogWarning("[RPG] Not a texture: " + t.path);
                return;
            }
            ti.textureType = TextureImporterType.Sprite;
            bool multiple = t.sprites.Count > 1;
            ti.spriteImportMode = multiple ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            ti.spritePixelsPerUnit = t.ppu;
            ti.filterMode = t.filter == "point" ? FilterMode.Point : FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = 4096;

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 1;
            settings.spriteGenerateFallbackPhysicsShape = false;
            if (!multiple)
            {
                var s = t.sprites[0];
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(s.pivot[0], s.pivot[1]);
                settings.spriteBorder = Border(s);
            }
            ti.SetTextureSettings(settings);

            if (multiple)
            {
                var factory = new SpriteDataProviderFactories();
                factory.Init();
                var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
                dp.InitSpriteEditorDataProvider();
                var rects = new List<SpriteRect>();
                foreach (var s in t.sprites)
                {
                    rects.Add(new SpriteRect
                    {
                        name = s.name,
                        rect = new Rect(s.x, s.y, s.w, s.h),
                        alignment = SpriteAlignment.Custom,
                        pivot = new Vector2(s.pivot[0], s.pivot[1]),
                        border = Border(s),
                        spriteID = StableId(t.path, s.name)
                    });
                }
                dp.SetSpriteRects(rects.ToArray());
                var names = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
                if (names != null)
                    names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
                dp.Apply();
            }
            ti.SaveAndReimport();
        }

        static Vector4 Border(SpriteEntry s)
        {
            if (s.border == null || s.border.Length < 4) return Vector4.zero;
            return new Vector4(s.border[0], s.border[1], s.border[2], s.border[3]);
        }

        static GUID StableId(string path, string name)
        {
            var h = Hash128.Compute(path + "::" + name).ToString();
            return new GUID(h);
        }

        /// <summary>All sprites of the Art folder by name (built lazily).</summary>
        public static Dictionary<string, Sprite> Sprites
        {
            get
            {
                if (_sprites != null) return _sprites;
                _sprites = new Dictionary<string, Sprite>();
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art" }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                        if (o is Sprite sp && !_sprites.ContainsKey(sp.name)) _sprites[sp.name] = sp;
                }
                return _sprites;
            }
        }

        public static Sprite S(string name)
        {
            if (Sprites.TryGetValue(name, out var s)) return s;
            Debug.LogWarning("[RPG] Missing sprite: " + name);
            return null;
        }

        public static void ResetCache() => _sprites = null;

        static void BuildAnimSets(Manifest m)
        {
            EditorUtil.EnsureFolder(AnimFolder);
            foreach (var group in m.anims.GroupBy(a => a.set))
            {
                string path = $"{AnimFolder}/{group.Key}.asset";
                var set = AssetDatabase.LoadAssetAtPath<SpriteAnimSet>(path);
                if (set == null)
                {
                    set = ScriptableObject.CreateInstance<SpriteAnimSet>();
                    AssetDatabase.CreateAsset(set, path);
                }
                set.clips.Clear();
                foreach (var a in group)
                {
                    set.clips.Add(new SpriteAnimSet.Clip
                    {
                        name = a.name,
                        fps = a.fps,
                        loop = a.loop,
                        frames = a.frames.Select(S).Where(x => x != null).ToArray()
                    });
                }
                EditorUtility.SetDirty(set);
            }
        }

        public static SpriteAnimSet AnimSet(string set) => AssetDatabase.LoadAssetAtPath<SpriteAnimSet>($"{AnimFolder}/{set}.asset");

        static void ConfigureCursor()
        {
            foreach (var p in new[] { "Assets/Art/UI/cursor.png", "Assets/Art/UI/cursor_attack.png" })
            {
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Cursor;
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.isReadable = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.alphaIsTransparency = true;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.SaveAndReimport();
            }
        }
    }
}

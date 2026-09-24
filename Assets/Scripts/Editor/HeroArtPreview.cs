using System.IO;
using UnityEditor;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>
    /// A picture of what <see cref="HeroArt"/> draws, for checking the paper doll outside the game:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod RPG.EditorTools.HeroArtPreview.Batch -previewOut C:\out\heroes.png
    /// One row per class (a different people on each), in a few poses, four times the size.
    /// </summary>
    public static class HeroArtPreview
    {
        static readonly (string clip, int frame)[] Poses =
        {
            ("idle_down", 0), ("walk_down", 0), ("idle_side", 0), ("walk_side", 2), ("attack_side", 1), ("attack_down", 1),
            ("cast_down", 0), ("idle_up", 0), ("hurt_side", 0), ("dead", 0),
        };

        [MenuItem("Tools/RPG/Hero Art Preview", priority = 60)]
        public static void Menu() => Write("Builds/hero_preview.png");

        public static void Batch()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-previewOut");
            Write(i >= 0 && i + 1 < args.Length ? args[i + 1] : "Builds/hero_preview.png");
        }

        static void Write(string path)
        {
            var db = AssetFactory.Database;
            HeroArt.DatabaseOverride = db;
            const int scale = 4, cell = HeroArt.FW * scale;
            int rows = db.classes.Count + 3;
            var sheet = new Texture2D(Poses.Length * cell, rows * cell, TextureFormat.RGBA32, false);
            var bg = new Color32[sheet.width * sheet.height];
            for (int k = 0; k < bg.Length; k++) bg[k] = ((k / sheet.width / cell + k % sheet.width / cell) % 2 == 0) ? new Color32(58, 70, 58, 255) : new Color32(66, 80, 66, 255);
            sheet.SetPixels32(bg);
            for (int r = 0; r < rows; r++)
            {
                HeroLook look;
                if (r < db.classes.Count)
                {
                    var cls = db.classes[r];
                    var race = db.races[r % db.races.Count];
                    look = new HeroLook { cls = cls.id, race = race.id, weapon = cls.DefaultWeapon, hair = r % 6, hairColor = (r * 5) % 12, skin = r % 4, eyes = r % 8, beard = race.beards ? 2 : 0, metal = r % 7, upgrade = r == 3 ? 8 : 0 };
                }
                else
                {
                    // extra rows: other weapons and a bare head
                    var cls = db.Class(r == db.classes.Count ? "fighter" : r == db.classes.Count + 1 ? "rogue" : "wizard");
                    string weapon = r == db.classes.Count ? "axe" : r == db.classes.Count + 1 ? "bow" : "tome";
                    look = new HeroLook { cls = cls.id, race = r == db.classes.Count ? "halforc" : r == db.classes.Count + 1 ? "halfling" : "tiefling", weapon = weapon, hair = 3, hairColor = 6, bareHead = true, cloth = r % 12 };
                }
                var set = HeroArt.Build(look, null);
                for (int c = 0; c < Poses.Length; c++)
                {
                    var clip = set.Get(Poses[c].clip);
                    if (clip == null || clip.frames.Length == 0) continue;
                    var sp = clip.frames[Mathf.Min(Poses[c].frame, clip.frames.Length - 1)];
                    var tex = sp.texture;
                    var rect = sp.rect;
                    for (int y = 0; y < HeroArt.FH; y++)
                        for (int x = 0; x < HeroArt.FW; x++)
                        {
                            var px = tex.GetPixel((int)rect.x + x, (int)rect.y + y);
                            if (px.a <= 0f) continue;
                            for (int sy = 0; sy < scale; sy++)
                                for (int sx = 0; sx < scale; sx++)
                                    sheet.SetPixel(c * cell + x * scale + sx, (rows - 1 - r) * cell + y * scale + sy, px);
                        }
                }
            }
            sheet.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Debug.Log($"[RPG] Hero art preview → {path}");
            HeroArt.DatabaseOverride = null;
        }
    }
}

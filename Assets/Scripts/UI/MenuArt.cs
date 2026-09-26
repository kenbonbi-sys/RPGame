using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// The small pictures of the green-and-gold menus (the title screen and the character
    /// creator): rounded card faces, a gold frame with corner ornaments, the stone pedestal and
    /// the dungeon alcove behind the hero, and icons (stats, health, energy, dice, chevrons…).
    /// They are drawn in the game the first time they are asked for (see MenuArtDraw.cs), like
    /// <see cref="HeroArt"/>'s heroes, so the menus need no imported art and no scene rebuild.
    /// </summary>
    public static partial class MenuArt
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>The sprite of that name (see the list in MenuArtDraw.cs), or null for an unknown name.</summary>
        public static Sprite Get(string name)
        {
            if (Cache.TryGetValue(name, out var s) && s != null) return s;
            if (!Defs.TryGetValue(name, out var d))
            {
                Debug.LogWarning("[MenuArt] no picture called " + name);
                return null;
            }
            var c = d.draw();
            var buf = new Color32[c.w * c.h];
            for (int y = 0; y < c.h; y++)
                for (int x = 0; x < c.w; x++)
                    buf[(c.h - 1 - y) * c.w + x] = c.px[y * c.w + x];
            var tex = new Texture2D(c.w, c.h, TextureFormat.RGBA32, false)
            {
                filterMode = d.smooth ? FilterMode.Bilinear : FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "menu_" + name,
                hideFlags = HideFlags.DontSave
            };
            tex.SetPixels32(buf);
            tex.Apply(false, true);
            // a canvas counts 100 units per sprite unit: one pixel spans `scale` units of the 1920×1080 layout
            float b = d.border;
            s = Sprite.Create(tex, new Rect(0, 0, c.w, c.h), new Vector2(0.5f, 0.5f), 100f / d.scale, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            s.name = tex.name;
            s.hideFlags = HideFlags.DontSave;
            Cache[name] = s;
            return s;
        }

        /// <summary>True when the picture is cut into nine (a frame or a card face that stretches).</summary>
        public static bool Sliced(string name) => Defs.TryGetValue(name, out var d) && d.border > 0;
    }
}

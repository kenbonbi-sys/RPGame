using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Draws a hero's sprite sheet in the game from their <see cref="HeroLook"/>: the paper doll of
    /// Tools/ArtGen/gen_player.py (the same parts, poses and frames, ported), dressed as the look
    /// says — their people (skin or scales, pointed ears, horns, tusks, a tail, a dragon's head,
    /// a stout or small build), hair and beard, the outfit of their class in the chosen colour
    /// (robes, capes, hoods, a wizard's hat, armor) and their weapon in the chosen metal, in hand
    /// at rest and swung in the attack. Every screen draws the heroes it shows, so any look costs
    /// a few bytes on the network; the server draws nothing. Sheets are cached by look.
    /// </summary>
    public static class HeroArt
    {
        public const int FW = 32, FH = 32;
        const int FeetY = 29, CX = 16;
        const float Ppu = 16f;

        /// <summary>The database to read peoples and classes from outside Play Mode (editor tools).</summary>
        public static GameDatabase DatabaseOverride;

        static GameDatabase Db => GameManager.I != null && GameManager.I.db != null ? GameManager.I.db : DatabaseOverride;

        /// <summary>The prefab's own animation set (clip names, speeds and order the drawn sets copy).</summary>
        public static SpriteAnimSet Template { get; private set; }

        static readonly Dictionary<string, SpriteAnimSet> Cache = new Dictionary<string, SpriteAnimSet>();

        // ================================================================== applying
        /// <summary>Dresses a hero's sprite in its look (a hero with no class yet keeps the prefab's).</summary>
        public static void ApplyTo(PlayerController pc)
        {
            if (pc == null || pc.anim == null) return;
            if (Template == null && pc.anim.set != null && !Cache.ContainsValue(pc.anim.set)) Template = pc.anim.set;
            if (!GameSession.HasScreen || pc.stats == null) return;
            var set = pc.stats.look.HasClass ? SetFor(pc.stats.look) : Template;
            if (set == null || set == pc.anim.set) return;
            string clip = pc.anim.Current;
            pc.anim.set = set;
            if (!string.IsNullOrEmpty(clip) && set.Has(clip)) pc.anim.Play(clip, true);
        }

        /// <summary>The drawn animation set of a look (cached).</summary>
        public static SpriteAnimSet SetFor(HeroLook look)
        {
            string key = look.ArtKey();
            if (Cache.TryGetValue(key, out var set) && set != null) return set;
            set = Build(look, Template);
            Cache[key] = set;
            if (Cache.Count > 64) Trim(key);
            return set;
        }

        static void Trim(string keep)
        {
            var drop = new List<string>();
            foreach (var kv in Cache)
                if (kv.Key != keep && drop.Count < 32) drop.Add(kv.Key);
            foreach (var k in drop) Cache.Remove(k);
        }

        /// <summary>One still picture of a look facing the viewer (the creator's cards).</summary>
        public static Sprite Portrait(HeroLook look)
        {
            var set = SetFor(look);
            var clip = set != null ? set.Get("idle_down") : null;
            return clip != null && clip.frames.Length > 0 ? clip.frames[0] : null;
        }

        static readonly Dictionary<string, Sprite> WeaponIcons = new Dictionary<string, Sprite>();

        /// <summary>
        /// The look's weapon alone as a small square icon, in its metal and glowing from +7, cropped
        /// and outlined like the item icons (the weapon slot of the character screen, the forge).
        /// Null for bare fists.
        /// </summary>
        public static Sprite WeaponIcon(HeroLook look)
        {
            if (look == null) return null;
            string key = $"{look.cls}|{look.weapon}|{look.metal}|{(look.upgrade >= 7 ? 1 : 0)}";
            if (WeaponIcons.TryGetValue(key, out var cached)) return cached;
            const int S = 24;
            var drawn = new Doll(look).WeaponAlone(S);
            int x0 = S, y0 = S, x1 = -1, y1 = -1;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    if (drawn.Opaque(x, y))
                    {
                        x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y);
                        x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y);
                    }
            Sprite sprite = null;
            if (x1 >= 0)
            {
                // cropped to a square around the weapon, then a dark rim: it fills its slot
                int n = Mathf.Max(8, Mathf.Max(x1 - x0 + 1, y1 - y0 + 1) + 2);
                int ox = (n - (x1 - x0 + 1)) / 2 - x0, oy = (n - (y1 - y0 + 1)) / 2 - y0;
                var cv = new Px(n, n);
                cv.Blit(drawn, ox, oy);
                var rim = new Color32(30, 18, 22, 255);
                var buf = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        var c = cv.Get(x, y);
                        if (c.a == 0 && (cv.Opaque(x - 1, y) || cv.Opaque(x + 1, y) || cv.Opaque(x, y - 1) || cv.Opaque(x, y + 1))) c = rim;
                        buf[(n - 1 - y) * n + x] = c;
                    }
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "weapon_" + look.weapon
                };
                tex.SetPixels32(buf);
                tex.Apply();
                sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
                sprite.name = tex.name;
            }
            WeaponIcons[key] = sprite;
            return sprite;
        }

        // ================================================================== the sheet
        static readonly (string name, float fps, bool loop)[] Clips =
        {
            ("idle_down", 3, true), ("idle_up", 3, true), ("idle_side", 3, true),
            ("walk_down", 10, true), ("walk_up", 10, true), ("walk_side", 10, true),
            ("attack_down", 16, false), ("attack_up", 16, false), ("attack_side", 16, false),
            ("cast_down", 8, true), ("cast_up", 8, true), ("cast_side", 8, true),
            ("hurt_down", 8, false), ("hurt_up", 8, false), ("hurt_side", 8, false),
            ("dash_down", 8, false), ("dash_up", 8, false), ("dash_side", 8, false), ("dead", 8, false),
        };

        /// <summary>Draws every frame of a look into one texture and wraps them as an animation set.</summary>
        public static SpriteAnimSet Build(HeroLook look, SpriteAnimSet template)
        {
            var doll = new Doll(look);
            var frames = doll.All();
            int cols = 4, rows = 0;
            var order = new List<string>();
            if (template != null)
                foreach (var c in template.clips)
                    if (c != null) order.Add(c.name);
            if (order.Count == 0)
                foreach (var c in Clips) order.Add(c.name);
            foreach (var n in order) rows += 1;
            var tex = new Texture2D(cols * FW, rows * FH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "hero_" + look.race + "_" + look.cls
            };
            var buf = new Color32[tex.width * tex.height];
            var set = ScriptableObject.CreateInstance<SpriteAnimSet>();
            set.name = tex.name;
            set.hideFlags = HideFlags.DontSave;
            for (int r = 0; r < order.Count; r++)
            {
                string name = order[r];
                frames.TryGetValue(name, out var list);
                var sprites = new List<Sprite>();
                if (list != null)
                    for (int f = 0; f < list.Count && f < cols; f++)
                    {
                        var px = list[f];
                        int ox = f * FW, oy = (rows - 1 - r) * FH;
                        for (int y = 0; y < FH; y++)
                            for (int x = 0; x < FW; x++)
                                buf[(oy + FH - 1 - y) * tex.width + ox + x] = px.Get(x, y);
                        var s = Sprite.Create(tex, new Rect(ox, oy, FW, FH), new Vector2(0.5f, (FH - 30f) / FH), Ppu, 0, SpriteMeshType.FullRect);
                        s.name = $"{tex.name}_{name}_{f}";
                        sprites.Add(s);
                    }
                var tc = template != null ? template.Get(name) : null;
                var def = Array.Find(Clips, c => c.name == name);
                set.clips.Add(new SpriteAnimSet.Clip
                {
                    name = name,
                    frames = sprites.ToArray(),
                    fps = tc != null ? tc.fps : def.fps > 0 ? def.fps : 8f,
                    loop = tc != null ? tc.loop : def.loop
                });
            }
            tex.SetPixels32(buf);
            tex.Apply(false, false);
            return set;
        }

        // ================================================================== pixels
        static Color32 Hex(string h)
        {
            ColorUtility.TryParseHtmlString(h, out var c);
            return c;
        }

        static readonly Color32 Outline = Hex("#1c1420");
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        /// <summary>A small RGBA canvas, top-left origin like the Python one.</summary>
        sealed class Px
        {
            public readonly int w, h;
            public readonly Color32[] a;

            public Px(int w, int h)
            {
                this.w = w;
                this.h = h;
                a = new Color32[w * h];
            }

            public bool In(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;

            public void Set(int x, int y, Color32 c)
            {
                if (c.a == 0 || !In(x, y)) return;
                a[y * w + x] = c;
            }

            public Color32 Get(int x, int y) => In(x, y) ? a[y * w + x] : Clear;

            public bool Opaque(int x, int y) => In(x, y) && a[y * w + x].a > 0;

            public void Blit(Px o, int ox, int oy)
            {
                for (int y = 0; y < o.h; y++)
                    for (int x = 0; x < o.w; x++)
                    {
                        var c = o.a[y * o.w + x];
                        if (c.a > 0) Set(ox + x, oy + y, c);
                    }
            }

            public void Outline(Color32 c)
            {
                var mask = new bool[a.Length];
                for (int i = 0; i < a.Length; i++) mask[i] = a[i].a > 0;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        if (mask[y * w + x]) continue;
                        bool near = (x > 0 && mask[y * w + x - 1]) || (x < w - 1 && mask[y * w + x + 1]) ||
                                    (y > 0 && mask[(y - 1) * w + x]) || (y < h - 1 && mask[(y + 1) * w + x]);
                        if (near) a[y * w + x] = c;
                    }
            }

            /// <summary>Turned a quarter to the left (numpy's rot90), then moved down (the fallen pose).</summary>
            public Px Fallen(int down)
            {
                var r = new Px(h, w);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        r.Set(y, w - 1 - x, a[y * w + x]);
                var s = new Px(r.w, r.h);
                s.Blit(r, 0, down);
                return s;
            }
        }

        /// <summary>Dark to light shades around a colour, shadows a little cooler and lights a little warmer.</summary>
        static Color32[] Ramp(Color c, int n)
        {
            Color.RGBToHSV(c, out float hue, out float sat, out float val);
            var r = new Color32[n];
            for (int i = 0; i < n; i++)
            {
                float t = n > 1 ? i / (float)(n - 1) : 0.5f;   // 0 dark … 1 light
                float v = Mathf.Clamp01(val * Mathf.Lerp(0.45f, 1.3f, t) + (t > 0.8f ? 0.06f : 0f));
                float s = Mathf.Clamp01(sat * Mathf.Lerp(1.15f, 0.7f, t));
                float hh = Mathf.Repeat(hue + Mathf.Lerp(0.025f, -0.02f, t), 1f);
                r[i] = Color.HSVToRGB(hh, s, v);
            }
            return r;
        }

        static Color32 Mix(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);

        // ================================================================== outfits
        enum Wear { Cloth, Skin, Metal, Leather, Fixed }

        /// <summary>How a class dresses: which colour goes where and what it wears on top.</summary>
        sealed class Outfit
        {
            public Wear main = Wear.Cloth, second = Wear.Fixed, sleeves = Wear.Cloth, pants = Wear.Fixed;
            public Color mainFixed = new Color(0.55f, 0.55f, 0.6f), secondFixed = new Color(0.72f, 0.51f, 0.11f), pantsFixed = new Color(0.17f, 0.16f, 0.28f);
            /// <summary>Which of main / second / pants / cape the player's chosen colour paints.</summary>
            public string clothOn = "main";
            public bool robe, cape, hood, hat, fur, pauldrons, tabard, sash, crown, bareForearms, scarf;
            public Color capeFixed = new Color(0.85f, 0.86f, 0.9f);
            public Color32 emblem;
            public bool hasEmblem;
        }

        static Outfit OutfitOf(string id)
        {
            switch (id)
            {
                case "barbarian":
                    return new Outfit { main = Wear.Skin, sleeves = Wear.Skin, second = Wear.Leather, pants = Wear.Cloth, clothOn = "pants", fur = true };
                case "bard":
                    return new Outfit { main = Wear.Cloth, second = Wear.Fixed, secondFixed = new Color(0.9f, 0.72f, 0.2f), cape = true, clothOn = "main" };
                case "cleric":
                    return new Outfit { main = Wear.Metal, sleeves = Wear.Metal, second = Wear.Cloth, clothOn = "second", tabard = true, hasEmblem = true, emblem = Hex("#ffe07a") };
                case "druid":
                    return new Outfit { main = Wear.Cloth, second = Wear.Leather, robe = true, crown = true, clothOn = "main" };
                case "fighter":
                    return new Outfit { main = Wear.Metal, sleeves = Wear.Metal, second = Wear.Cloth, clothOn = "second", tabard = true, pauldrons = true };
                case "monk":
                    return new Outfit { main = Wear.Cloth, second = Wear.Fixed, secondFixed = new Color(0.3f, 0.2f, 0.14f), pants = Wear.Cloth, clothOn = "main", sash = true, bareForearms = true };
                case "paladin":
                    return new Outfit { main = Wear.Metal, sleeves = Wear.Metal, second = Wear.Cloth, clothOn = "second", tabard = true, pauldrons = true, cape = true, hasEmblem = true, emblem = Hex("#ffe07a") };
                case "ranger":
                    return new Outfit { main = Wear.Leather, sleeves = Wear.Leather, second = Wear.Cloth, clothOn = "second", hood = true, cape = true };
                case "rogue":
                    return new Outfit { main = Wear.Fixed, mainFixed = new Color(0.2f, 0.19f, 0.25f), sleeves = Wear.Fixed, second = Wear.Cloth, clothOn = "second", hood = true, scarf = true };
                case "sorcerer":
                    return new Outfit { main = Wear.Cloth, second = Wear.Fixed, secondFixed = new Color(0.95f, 0.75f, 0.25f), robe = true, clothOn = "main" };
                case "warlock":
                    return new Outfit { main = Wear.Cloth, second = Wear.Fixed, secondFixed = new Color(0.13f, 0.11f, 0.16f), robe = true, hood = true, clothOn = "main" };
                case "wizard":
                    return new Outfit { main = Wear.Cloth, second = Wear.Fixed, secondFixed = new Color(0.9f, 0.75f, 0.3f), robe = true, hat = true, clothOn = "main" };
                default:
                    // no class yet: the old red-scarfed look
                    return new Outfit { main = Wear.Fixed, mainFixed = new Color(0.78f, 0.8f, 0.88f), second = Wear.Fixed, secondFixed = new Color(0.71f, 0.16f, 0.2f), sleeves = Wear.Fixed };
            }
        }

        // ================================================================== ASCII parts (gen_player.py)
        static readonly string[] FaceDown =
        {
            "...sSSSSs...", "..sSSLLSSs..", ".sSSLLLSSSs.", ".sSSSSSSSSs.", ".sSSSSSSSSs.", ".sSSSSSSSSs.",
            ".sSeSSSSeSs.", ".sSeSSSSeSs.", ".sbSSooSSbs.", "..sSSSSSSs..", "...sSSSSs...",
        };
        static readonly string[] FaceDownBlink =
        {
            "...sSSSSs...", "..sSSLLSSs..", ".sSSLLLSSSs.", ".sSSSSSSSSs.", ".sSSSSSSSSs.", ".sSSSSSSSSs.",
            ".sSSSSSSSSs.", ".sseSSSSess.", ".sbSSooSSbs.", "..sSSSSSSs..", "...sSSSSs...",
        };
        static readonly string[] FaceDownHurt =
        {
            "...sSSSSs...", "..sSSLLSSs..", ".sSSLLLSSSs.", ".sSSSSSSSSs.", ".sSSSSSSSSs.", ".sSSSSSSSSs.",
            ".sSeSSSSeSs.", ".seSSSSSSes.", ".sbSSooSSbs.", "..sSSSSSSs..", "...sSSSSs...",
        };
        static readonly string[] FaceUp =
        {
            "...sSSSSs...", "..sSSSSSSs..", ".sSSSSSSSSs.", ".sSSSSSSSSs.", ".sSSSSSSSSs.", ".sSSSSSSSSs.",
            ".sSSSSSSSSs.", ".ssSSSSSSss.", "..sssSSsss..", "...ssqRss...", "............",
        };
        static readonly string[] FaceSide =
        {
            "...sSSSSs...", ".sSSSSSSSSs.", "sSSSSSSLSSSs", "sSSSSSSSSSSS", "sSSSSSSSSSSS", "sSSSSSSSSSSS",
            "sSSSSSSSSSeS", "ssSSSsLSSSeS", ".sSSSsSSSbSS", "..sSSsSSSSs.", "....ssssss..",
        };
        static readonly string[] FaceSideHurt =
        {
            "...sSSSSs...", ".sSSSSSSSSs.", "sSSSSSSLSSSs", "sSSSSSSSSSSS", "sSSSSSSSSSSS", "sSSSSSSSSSSS",
            "sSSSSSSSSSSS", "ssSSSsLSSeeS", ".sSSSsSSSbSS", "..sSSsSSSSs.", "....ssssss..",
        };

        // the dragon heads of Long Duệ (h: horn, 1–3: frill in the hair colour, o: nostril)
        static readonly string[] DragonDown =
        {
            "...sSSSSs...", "..sSLLLLSs..", ".sSSSLLSSSs.", "3sSeSSSSeSs3", "23SSSSSSSS32", ".2sSLLLLSs2.",
            "..sSLLLLSs..", "..sSLLLLSs..", "..sSoLLoSs..", "...sSSSSs...", "....ssss....",
        };
        static readonly string[] DragonDownHurt =
        {
            "...sSSSSs...", "..sSLLLLSs..", ".sSSSLLSSSs.", "3sSSSSSSSSs3", "23eSSSSSSe32", ".2sSLLLLSs2.",
            "..sSLLLLSs..", "..sSLLLLSs..", "..sSoLLoSs..", "...soooos...", "....ssss....",
        };
        static readonly string[] DragonUp =
        {
            "..sSSSSSSs..", ".sSS1221SSs.", ".sSS2332SSs.", "sSSSS23SSSSs", "sSSSS23SSSSs", "sSSSSS2SSSSs",
            ".sSSSSSSSSs.", "..sSSSSSSs..", "...sSSSSs...", "...ssqRss...", "............",
        };
        static readonly string[] DragonSide =
        {
            ".3sSSSs.....", "32sSLLSSs...", "2sSSSSSeSSs.", "sSSSSSSSSSSs", "sSSSSSSSSSSo", "sSSSSSSSSSs.",
            "sSSSSSssss..", ".sSSSSs.....", ".sSSSSs.....", "..sSSs......", "............",
        };
        static readonly string[] DragonSideHurt =
        {
            ".3sSSSs.....", "32sSLLSSs...", "2sSSSSSeeSs.", "sSSSSSSSSSSs", "sSSSSSSSSSSo", "sSSSSSSooSs.",
            "sSSSSSssss..", ".sSSSSs.....", ".sSSSSs.....", "..sSSs......", "............",
        };

        /// <summary>A hair style's overlay for one direction; <c>dy</c> is where its first row sits against the head's.</summary>
        struct Overlay
        {
            public int dy;
            public string[] rows;
            public Overlay(int dy, params string[] rows)
            {
                this.dy = dy;
                this.rows = rows;
            }
        }

        // hair: [style][direction 0 down, 1 up, 2 side]
        static readonly Overlay[][] Hair =
        {
            // Ngắn
            new[]
            {
                new Overlay(0, "...344443...", "..34554433..", ".3445543332.", ".3344433332.", ".2332333232.", ".2..3..3..2.", ".2........2."),
                new Overlay(0, "...344443...", "..34554433..", ".3445543332.", ".3344433332.", ".2333333332.", ".2233333322.", ".1222222221.", "..11111111.."),
                new Overlay(0, "...344443...", ".3345544333.", "334455433333", "233444333332", "2333333323..", "233333......", "2233........", ".12........."),
            },
            // Dài (the prototype's)
            new[]
            {
                new Overlay(0, "...344443...", ".3345544333.", "334455433332", "233444333322", "233233332332", "23..3..3..32", "23........32", "21........12", "21........12", "11........11", "..1......1.."),
                new Overlay(0, "...344443...", ".3345544333.", "334455433332", "233444333322", "233333333322", "223333333322", "222333333222", "212233332212", "211222222112", "111......111", "..11222211.."),
                new Overlay(0, "...344443...", ".3345544333.", "334455433333", "233444333333", "233333332333", "233333......", "23333.......", "22332.......", "21221.......", "11211.......", ".111........"),
            },
            // Đuôi Ngựa
            new[]
            {
                new Overlay(0, "...344443...", "..34554433..", ".3445543332.", ".3344433332.", ".2332333232.", ".2..3..3..2.", ".2........2."),
                new Overlay(-1, "....5445....", "...344443...", "..34554433..", ".3445543332.", ".3344433332.", ".2333333332.", ".2233443322.", ".1222332221.", "....2332....", "....2332....", ".....22.....", ".....21....."),
                new Overlay(0, "...344443...", ".3345544333.", "334455433333", "233444333332", "2333333323..", "533333......", "43.3........", "32..........", "32..........", "21..........", "1..........."),
            },
            // Búi Tó
            new[]
            {
                new Overlay(-3, "....3443....", "...345543..", "....2332....", "...344443...", "..34554433..", ".3445543332.", ".3344433332.", ".2332333232.", ".2..3..3..2.", ".2........2."),
                new Overlay(-3, "....3443....", "...345543..", "....2332....", "...344443...", "..34554433..", ".3445543332.", ".3344433332.", ".2333333332.", ".2233333322.", ".1222222221.", "..11111111.."),
                new Overlay(-3, "..3443......", ".345543.....", "..2332......", "...344443...", ".3345544333.", "334455433333", "233444333332", "2333333323..", "233333......", "2233........", ".12........."),
            },
            // Dựng
            new[]
            {
                new Overlay(-2, "..4..5..4...", ".343.4.343..", "..345544333.", ".3445543332.", ".3344433332.", ".2332333232.", ".2..3..3..2."),
                new Overlay(-2, "..4..5..4...", ".343.4.343..", "..345544333.", ".3445543332.", ".3344433332.", ".2333333332.", ".2233333322.", ".1222222221."),
                new Overlay(-2, "...4..5.....", "..343.43....", ".33455443...", "334455433333", "233444333332", "2333333323..", "233333......", "2233........"),
            },
            // Tết Đôi
            new[]
            {
                new Overlay(0, "...344443...", ".3345544333.", "334455433332", "233444333322", "233233332332", "23..3..3..32", "23........32", "32........23", "23........32", "32........23", "23........32", "32........23", "2..........2"),
                new Overlay(0, "...344443...", ".3345544333.", "334455433332", "233444333322", "233333333322", "223333333322", "222333333222", "32.2333332.23", "23.......32.", "32........23", "23........32", "32........23", "2..........2"),
                new Overlay(0, "...344443...", ".3345544333.", "334455433333", "233444333333", "233333332333", "233333......", "23333.......", "32332.......", "23.........", "32.........", "23.........", "32.........", "2.........."),
            },
            // Trọc
            new[] { new Overlay(0), new Overlay(0), new Overlay(0) },
        };

        static readonly Overlay[][] BeardArt =
        {
            new[] { new Overlay(0), new Overlay(0), new Overlay(0) },
            new[]
            {
                new Overlay(8, "..2.......2.", "..23333332..", "...233332...", "....2332...."),
                new Overlay(0),
                new Overlay(8, ".........2..", "......23332.", ".......2332.", "........22.."),
            },
            new[]
            {
                new Overlay(7, ".2........2.", ".23......32.", "..23333332..", "..34333343..", "...333333...", "...233332...", "....2332....", "....2332....", ".....22....."),
                new Overlay(0),
                new Overlay(7, "..........2.", ".......2332.", "......23332.", "......33343.", ".......3332.", ".......2332.", "........22..", "........2..."),
            },
        };

        // hoods (H: the hood, h: its shadow) and the wizard's hat (g: its gold band)
        static readonly Overlay[] HoodArt =
        {
            new Overlay(-1, "...hHHHHh...", "..hHHHHHHh..", ".hHHHHHHHHh.", "hHHHHHHHHHHh", "hHH......HHh", "hH........Hh", "hH........Hh", "hH........Hh", "hHh......hHh", ".hH......Hh.", ".hHh....hHh."),
            new Overlay(-1, "...hHHHHh...", "..hHHHHHHh..", ".hHHHHHHHHh.", "hHHHHHHHHHHh", "hHHHHHHHHHHh", "hHHHHHHHHHHh", "hHHHHHHHHHHh", "hHHHHHHHHHHh", "hHHHHHHHHHHh", ".hHHHHHHHHh.", "..hhhhhhhh.."),
            new Overlay(-1, "...hHHHHh...", ".hHHHHHHHHh.", "hHHHHHHHHHh.", "hHHHHHHH....", "hHHHHHH.....", "hHHHHH......", "hHHHHH......", "hHHHH.......", "hHHHH.......", ".hHHh.......", "..hh........"),
        };
        static readonly Overlay[] HatArt =
        {
            new Overlay(-8, "......hH....", ".....hHH....", ".....hHHH...", "....hHHHH...", "....hHHHHH..", "...hHHHHHH..", "...hHHHHHHH.", "..ggggggggg.", "hHHHHHHHHHHHh"),
            new Overlay(-8, "......hH....", ".....hHH....", ".....hHHH...", "....hHHHH...", "....hHHHHH..", "...hHHHHHH..", "...hHHHHHHH.", "..ggggggggg.", "hHHHHHHHHHHHh"),
            new Overlay(-8, "....Hh......", "....HHh.....", "...HHHh.....", "...HHHHh....", "..HHHHHh....", "..HHHHHHh...", ".HHHHHHHh...", ".ggggggggg..", "HHHHHHHHHHHHh"),
        };

        static readonly string[] TorsoDown = { "rRqRRqRr", "CrRRRRrc", "CWWrRCCc", "CWWWCCcc", "tttTTttt", "CCCCCccc", ".cCCCcc." };
        static readonly string[] TorsoUp = { "rRRRRRRr", "CrRRRRrc", "CWWWCCCc", "CWWCCCcc", "tttttttt", "CCCCCccc", ".cCCCcc." };
        static readonly string[] TorsoSide = { ".rRqRr", "rRRRCc", "WWCCCc", "WWCCcc", "ttTttt", "CCCCcc", ".CCcc." };
        static readonly string[] ArmL = { "Aa", "Aa", "aa", "aa", "SS", "ss" };
        static readonly string[] ArmR = { "aa", "aa", "aa", "aa", "SS", "ss" };
        static readonly string[] ArmSide = { "Aa", "Aa", "Aa", "aa", "SS", "sS" };
        static readonly string[] ArmBack = { "aa", "aa", "aa", "aa", "ss", "ss" };
        static readonly string[] Leg = { "PP", "Pp", "NN", "nn" };
        static readonly string[] LegBack = { "pp", "pp", "nn", "nn" };

        // ================================================================== the doll
        /// <summary>One look's paper doll: its palette and its drawing of every pose.</summary>
        sealed class Doll
        {
            readonly HeroLook look;
            readonly RaceDef race;
            readonly Outfit outfit;
            readonly WeaponKind weapon;
            readonly Dictionary<char, Color32> pal = new Dictionary<char, Color32>();
            readonly int lift;
            readonly bool stout, dragon, bareHead;
            readonly Color32[] metal, wood, glow, capeRamp, hoodRamp, hornRamp, tailRamp;
            readonly int hairStyle;

            public Doll(HeroLook look)
            {
                this.look = look;
                var db = Db;
                race = db != null ? db.Race(look.race) : null;
                var cls = db != null ? db.Class(look.cls) : null;
                outfit = OutfitOf(cls != null ? cls.outfit : look.cls);
                weapon = WeaponKinds.Get(look.weapon) ?? WeaponKinds.Get(cls != null ? cls.DefaultWeapon : "sword");
                var build = race != null ? race.build : BodyBuild.Normal;
                lift = build == BodyBuild.Small ? 3 : build == BodyBuild.Stout ? 2 : 0;
                stout = build == BodyBuild.Stout;
                dragon = race != null && race.dragonHead;
                hairStyle = dragon ? HeroLook.Bald : Mathf.Clamp(look.hair, 0, HeroLook.HairStyles.Length - 1);
                bareHead = look.bareHead;

                // people: skin and features
                Color skinBase = race != null && race.skins != null && race.skins.Length > 0
                    ? race.skins[Mathf.Clamp(look.skin, 0, race.skins.Length - 1)]
                    : new Color(0.93f, 0.69f, 0.53f);
                var skin = Ramp(skinBase, 4);
                pal['s'] = skin[1];
                pal['S'] = skin[2];
                pal['L'] = skin[3];
                pal['b'] = Mix(skin[2], Hex("#f08a7a"), dragon ? 0f : 0.55f);
                var hair = Ramp(HeroLook.Pick(HeroLook.HairColors, look.hairColor), 5);
                for (int i = 0; i < 5; i++) pal[(char)('1' + i)] = hair[i];
                pal['e'] = HeroLook.Pick(HeroLook.EyeColors, look.eyes);
                pal['o'] = dragon ? skin[0] : Hex("#241826");
                pal['w'] = Hex("#fffbe8");

                // clothes
                Color chosen = look.cloth >= 0 ? HeroLook.Pick(HeroLook.Cloths, look.cloth) : cls != null ? cls.cloth : outfit.mainFixed;
                var leather = Ramp(Hex("#6c4028"), 5);
                metal = Ramp(HeroLook.Pick(HeroLook.Metals, look.metal), 6);
                var armor = Ramp(new Color(0.6f, 0.63f, 0.72f), 5);
                Color32[] RampOf(Wear w, Color fixedColor, string slot)
                {
                    if (outfit.clothOn == slot) return Ramp(chosen, 5);
                    switch (w)
                    {
                        case Wear.Skin: return new[] { skin[0], skin[1], skin[2], skin[3], skin[3] };
                        case Wear.Metal: return armor;
                        case Wear.Leather: return leather;
                        default: return Ramp(fixedColor, 5);
                    }
                }
                var main = RampOf(outfit.main, outfit.mainFixed, "main");
                var second = RampOf(outfit.second, outfit.secondFixed, "second");
                var pants = RampOf(outfit.pants, outfit.pantsFixed, "pants");
                var sleeves = outfit.sleeves == outfit.main ? main : RampOf(outfit.sleeves, outfit.mainFixed, "sleeves");
                pal['c'] = main[1];
                pal['C'] = main[2];
                pal['W'] = main[3];
                pal['r'] = second[1];
                pal['R'] = second[2];
                pal['q'] = second[3];
                pal['a'] = sleeves[1];
                pal['A'] = sleeves[3];
                pal['t'] = outfit.sash ? second[1] : leather[1];
                pal['T'] = outfit.sash ? second[2] : Hex("#e8b634");
                pal['p'] = pants[1];
                pal['P'] = pants[2];
                pal['n'] = leather[0];
                pal['N'] = leather[2];
                pal['k'] = leather[2];
                pal['g'] = Hex("#b8821e");
                pal['G'] = Hex("#ffe07a");
                capeRamp = outfit.clothOn == "cape" ? Ramp(chosen, 5) : Ramp(outfit.capeFixed, 5);
                if (outfit.cape && outfit.clothOn != "cape" && (outfit.main == Wear.Metal || outfit.main == Wear.Leather)) capeRamp = second;
                hoodRamp = outfit.hood ? (outfit.clothOn == "main" ? main : second) : main;
                pal['h'] = hoodRamp[0];
                pal['H'] = outfit.hat ? main[2] : hoodRamp[2];
                wood = Ramp(Hex("#855433"), 5);
                glow = Ramp(HeroLook.Pick(HeroLook.Metals, look.metal), 5);
                hornRamp = Ramp(race != null && race.id == "tiefling" ? Hex("#4a3a44") : Hex("#d8ccbc"), 4);
                tailRamp = Ramp(skinBase, 4);
            }

            Px Part(string[] rows)
            {
                int w = 0;
                foreach (var r in rows) w = Mathf.Max(w, r.Length);
                var p = new Px(Mathf.Max(1, w), Mathf.Max(1, rows.Length));
                for (int y = 0; y < rows.Length; y++)
                    for (int x = 0; x < rows[y].Length; x++)
                    {
                        char ch = rows[y][x];
                        if (ch == '.' || ch == ' ') continue;
                        if (pal.TryGetValue(ch, out var c)) p.Set(x, y, c);
                    }
                return p;
            }

            void Over(Px cv, Overlay o, int x, int y)
            {
                if (o.rows == null || o.rows.Length == 0) return;
                cv.Blit(Part(o.rows), x, y + o.dy);
            }

            // ------------------------------------------------------------ heads
            void Head(Px cv, string dir, int hx, int hy, string mode)
            {
                int d = dir == "down" ? 0 : dir == "up" ? 1 : 2;
                bool hidesHair = !bareHead && (outfit.hood || outfit.hat);
                // behind the head: horns reach back, a ponytail hangs
                string[] face;
                if (dragon)
                    face = d == 0 ? (mode == "hurt" ? DragonDownHurt : DragonDown) : d == 1 ? DragonUp : (mode == "hurt" ? DragonSideHurt : DragonSide);
                else
                    face = d == 0 ? (mode == "hurt" ? FaceDownHurt : mode == "blink" ? FaceDownBlink : FaceDown) : d == 1 ? FaceUp : (mode == "hurt" ? FaceSideHurt : FaceSide);
                if (dragon || (race != null && race.horns)) Horns(cv, d, hx, hy);
                cv.Blit(Part(face), hx, hy);
                if (race != null && race.tusks && d != 1) Tusks(cv, d, hx, hy);
                if (!dragon && !hidesHair) Over(cv, Hair[hairStyle][d], hx, hy);
                if (!dragon && look.beard > 0) Over(cv, BeardArt[Mathf.Clamp(look.beard, 0, BeardArt.Length - 1)][d], hx, hy);
                if (race != null && race.pointedEars && !dragon) Ears(cv, d, hx, hy, hidesHair);
                if (!bareHead && outfit.hood) Over(cv, HoodArt[d], hx, hy);
                if (!bareHead && outfit.hat) Over(cv, HatArt[d], hx, hy);
                if (outfit.crown && !hidesHair) Crown(cv, d, hx, hy);
                if (outfit.scarf && !bareHead && d != 1) Scarf(cv, d, hx, hy);
            }

            // two shades of horn, curving up and back from the top of the head
            static readonly (int x, int y, int shade)[] HornFront = { (3, 0, 1), (2, 0, 0), (2, -1, 1), (1, -1, 0), (1, -2, 1), (0, -2, 0), (0, -3, 1) };
            static readonly (int x, int y, int shade)[] HornSide = { (5, 0, 1), (4, 0, 0), (4, -1, 1), (3, -1, 0), (2, -2, 1), (1, -2, 0), (0, -3, 1) };

            void Horns(Px cv, int d, int hx, int hy)
            {
                var c0 = hornRamp[1];
                var c1 = hornRamp[3];
                if (d == 2)
                {
                    foreach (var (x, y, sh) in HornSide) cv.Set(hx + x, hy + y, sh == 1 ? c1 : c0);
                    return;
                }
                foreach (var (x, y, sh) in HornFront)
                {
                    cv.Set(hx + x, hy + y, sh == 1 ? c1 : c0);
                    cv.Set(hx + 11 - x, hy + y, sh == 1 ? c0 : c1);
                }
            }

            void Ears(Px cv, int d, int hx, int hy, bool hooded)
            {
                var s = pal['S'];
                var l = pal['L'];
                if (d == 2)
                {
                    if (hooded) return;
                    cv.Set(hx + 4, hy + 6, s); cv.Set(hx + 3, hy + 5, s); cv.Set(hx + 2, hy + 4, l);
                    return;
                }
                cv.Set(hx, hy + 6, s); cv.Set(hx - 1, hy + 5, s); cv.Set(hx - 2, hy + 4, l);
                cv.Set(hx + 11, hy + 6, s); cv.Set(hx + 12, hy + 5, s); cv.Set(hx + 13, hy + 4, l);
            }

            void Tusks(Px cv, int d, int hx, int hy)
            {
                var w = pal['w'];
                if (d == 2) { cv.Set(hx + 9, hy + 9, w); return; }
                cv.Set(hx + 4, hy + 8, w);
                cv.Set(hx + 7, hy + 8, w);
            }

            void Crown(Px cv, int d, int hx, int hy)
            {
                var leaf = Ramp(Hex("#50b848"), 4);
                int[] xs = d == 2 ? new[] { 3, 5, 7, 9 } : new[] { 2, 4, 7, 9 };
                foreach (int x in xs)
                {
                    cv.Set(hx + x, hy + 1, leaf[2]);
                    cv.Set(hx + x, hy, leaf[3]);
                }
                for (int x = 2; x <= 9; x++) cv.Set(hx + x, hy + 2, leaf[1]);
            }

            void Scarf(Px cv, int d, int hx, int hy)
            {
                var r = pal['r'];
                var R = pal['R'];
                if (d == 2)
                {
                    for (int x = 6; x <= 11; x++) cv.Set(hx + x, hy + 8, R);
                    for (int x = 6; x <= 10; x++) cv.Set(hx + x, hy + 9, r);
                    return;
                }
                for (int x = 2; x <= 9; x++) cv.Set(hx + x, hy + 8, R);
                for (int x = 2; x <= 9; x++) cv.Set(hx + x, hy + 9, r);
            }

            // ------------------------------------------------------------ body extras
            void Tail(Px cv, string dir, int bodyY, int lean)
            {
                if (race == null || !race.tail) return;
                bool thick = dragon;
                var c = tailRamp;
                int x0 = CX + 2 + lean, y0 = bodyY + 5;
                if (dir == "side")
                {
                    // out behind (to the left), curling
                    for (int k = 0; k < 7; k++)
                    {
                        int x = CX - 3 - k + lean, y = y0 + 1 + (k > 3 ? k - 3 : 0) / 2;
                        cv.Set(x, y, c[2]);
                        if (thick) cv.Set(x, y + 1, c[1]);
                    }
                    if (!thick) { cv.Set(CX - 10 + lean, y0 + 2, c[1]); cv.Set(CX - 10 + lean, y0 + 1, c[3]); }
                    return;
                }
                if (dir == "up")
                {
                    for (int k = 0; k < 7; k++)
                    {
                        int x = CX + (k > 2 ? k - 2 : 0), y = y0 + 1 + k;
                        cv.Set(x, y, c[2]);
                        if (thick) cv.Set(x - 1, y, c[1]);
                    }
                    return;
                }
                // facing the viewer: the tip shows beside the legs
                for (int k = 0; k < 4; k++)
                {
                    cv.Set(x0 + 3 + k / 2, y0 + 3 + k, c[1]);
                    if (thick) cv.Set(x0 + 2 + k / 2, y0 + 3 + k, c[1]);
                }
                if (!thick) cv.Set(x0 + 5, y0 + 7, c[3]);
            }

            void Cape(Px cv, string dir, int bodyY, int lean, int sway)
            {
                if (!outfit.cape) return;
                var c = capeRamp;
                int bottom = FeetY - 2;
                if (dir == "down")
                {
                    for (int y = bodyY + 1; y <= bottom; y++)
                    {
                        cv.Set(CX - 6 + lean, y, c[1]);
                        cv.Set(CX + 5 + lean, y, c[0]);
                    }
                    return;
                }
                if (dir == "up")
                {
                    for (int y = bodyY; y <= bottom; y++)
                    {
                        int half = 4 + (y - bodyY) / 5;
                        for (int x = CX - half; x < CX + half; x++)
                            cv.Set(x + (y > bodyY + 8 ? sway : 0), y, x < CX - half + 2 ? c[3] : x > CX + half - 3 ? c[1] : c[2]);
                    }
                    for (int x = CX - 5; x < CX + 5; x++) cv.Set(x + sway, bottom, c[0]);
                    return;
                }
                for (int y = bodyY + 1; y <= bottom; y++)
                {
                    int back = 1 + (y - bodyY) / 4;
                    for (int x = CX - 3 - back; x <= CX - 2; x++)
                        cv.Set(x + lean - (y > bodyY + 7 ? sway : 0), y, x == CX - 3 - back ? c[1] : c[2]);
                }
            }

            void Robe(Px cv, string dir, int bodyY, int lean, int sway)
            {
                if (!outfit.robe) return;
                var m = new[] { pal['c'], pal['C'], pal['W'] };
                var trim = pal['R'];
                int top = bodyY + 5, bottom = FeetY - 1;
                if (dir == "side")
                {
                    for (int y = top; y <= bottom; y++)
                    {
                        int back = (y - top) / 3;
                        for (int x = CX - 3 - back; x <= CX + 2; x++)
                            cv.Set(x + lean + (y > top + 4 ? sway : 0), y, y == bottom ? trim : x <= CX - 2 - back ? m[0] : x >= CX + 1 ? m[2] : m[1]);
                    }
                    return;
                }
                for (int y = top; y <= bottom; y++)
                {
                    int half = 4 + (y - top) / 3;
                    for (int x = CX - half; x < CX + half; x++)
                    {
                        Color32 col = y == bottom ? trim : x < CX - half + 2 ? m[2] : x >= CX + half - 2 ? m[0] : m[1];
                        cv.Set(x + lean + (y > top + 4 ? sway : 0), y, col);
                    }
                }
                if (dir == "down")
                    for (int y = top + 1; y < bottom; y++) cv.Set(CX + lean + (y > top + 4 ? sway : 0), y, trim);
            }

            void Tabard(Px cv, string dir, int bodyY, int lean)
            {
                if (!outfit.tabard || dir == "up") return;
                var r = pal['R'];
                var rq = pal['r'];
                if (dir == "side")
                {
                    for (int y = bodyY + 1; y <= bodyY + 8; y++) cv.Set(CX + 1 + lean, y, y > bodyY + 6 ? rq : r);
                    return;
                }
                for (int y = bodyY + 1; y <= bodyY + 8; y++)
                {
                    cv.Set(CX - 1 + lean, y, y > bodyY + 6 ? rq : r);
                    cv.Set(CX + lean, y, rq);
                }
                if (outfit.hasEmblem)
                {
                    cv.Set(CX - 1 + lean, bodyY + 2, outfit.emblem);
                    cv.Set(CX + lean, bodyY + 2, outfit.emblem);
                    cv.Set(CX - 1 + lean, bodyY + 3, outfit.emblem);
                }
            }

            void Pauldrons(Px cv, string dir, int bodyY, int lean, (int, int)[] arms)
            {
                if (!outfit.pauldrons) return;
                var hi = pal['G'];
                var m = Ramp(new Color(0.75f, 0.78f, 0.86f), 4);
                if (dir == "side")
                {
                    int x = CX - 1 + arms[0].Item1 + lean, y = bodyY + arms[0].Item2;
                    cv.Set(x, y, m[3]); cv.Set(x + 1, y, m[2]); cv.Set(x - 1, y + 1, m[2]); cv.Set(x, y + 1, m[1]); cv.Set(x + 1, y + 1, hi);
                    return;
                }
                int lx = CX - 7 + arms[0].Item1 + lean, rx = CX + 4 + arms[1].Item1 + lean;
                for (int k = 0; k < 3; k++)
                {
                    cv.Set(lx + k, bodyY + arms[0].Item2, m[3 - k / 2]);
                    cv.Set(rx + k, bodyY + arms[1].Item2, m[2 - k / 2]);
                    cv.Set(lx + k, bodyY + 1 + arms[0].Item2, m[1]);
                    cv.Set(rx + k, bodyY + 1 + arms[1].Item2, m[1]);
                }
            }

            void Fur(Px cv, string dir, int bodyY, int lean)
            {
                if (!outfit.fur) return;
                var f = Ramp(Hex("#784c30"), 4);
                int w = dir == "side" ? 7 : 10;
                int x0 = dir == "side" ? CX - 4 + lean : CX - 5 + lean;
                for (int x = 0; x < w; x++)
                {
                    cv.Set(x0 + x, bodyY, (x % 2 == 0) ? f[3] : f[2]);
                    cv.Set(x0 + x, bodyY + 1, (x % 3 == 0) ? f[1] : f[2]);
                }
            }

            // ------------------------------------------------------------ weapons
            Color32 M(int i) => metal[Mathf.Clamp(i, 0, metal.Length - 1)];

            void Line(Px cv, float x0, float y0, float dx, float dy, int from, int to, Func<int, Color32> col)
            {
                for (int k = from; k <= to; k++) cv.Set(Mathf.RoundToInt(x0 + dx * k), Mathf.RoundToInt(y0 + dy * k), col(k));
            }

            /// <summary>The weapon with its grip at (hx, hy) pointing along <paramref name="angleDeg"/> (0: right, 90: down).</summary>
            void Weapon(Px cv, int hx, int hy, float angleDeg, bool swinging)
            {
                if (weapon == null) return;
                float a = angleDeg * Mathf.Deg2Rad;
                float dx = Mathf.Cos(a), dy = Mathf.Sin(a);
                float px = -dy, py = dx;
                var edge = M(5);
                var blade = M(3);
                var dark = M(1);
                bool glowing = look.upgrade >= 7;
                Color32 G(Color32 c) => glowing ? Mix(c, glow[4], 0.35f) : c;
                switch (weapon.id)
                {
                    case "sword":
                    case "rapier":
                    case "scimitar":
                    {
                        int len = weapon.id == "rapier" ? 11 : weapon.id == "scimitar" ? 8 : 9;
                        Line(cv, hx, hy, dx, dy, -2, -1, k => pal['k']);
                        for (int k = -1; k <= 1; k++)
                            cv.Set(Mathf.RoundToInt(hx + dx + px * k * 1.2f), Mathf.RoundToInt(hy + dy + py * k * 1.2f), k != 1 ? pal['G'] : pal['g']);
                        for (int k = 2; k < len + 2; k++)
                        {
                            float curve = weapon.id == "scimitar" ? (k - 2) * (k - 2) * 0.06f : 0f;
                            float x = hx + dx * k + px * curve, y = hy + dy * k + py * curve;
                            cv.Set(Mathf.RoundToInt(x), Mathf.RoundToInt(y), G(blade));
                            if (weapon.id != "rapier")
                                cv.Set(Mathf.RoundToInt(x + px * 0.8f), Mathf.RoundToInt(y + py * 0.8f), G(k < len ? edge : blade));
                        }
                        cv.Set(Mathf.RoundToInt(hx + dx * (len + 2)), Mathf.RoundToInt(hy + dy * (len + 2)), G(edge));
                        break;
                    }
                    case "dagger":
                    {
                        Line(cv, hx, hy, dx, dy, -1, -1, k => pal['k']);
                        cv.Set(Mathf.RoundToInt(hx + px), Mathf.RoundToInt(hy + py), pal['G']);
                        cv.Set(Mathf.RoundToInt(hx - px), Mathf.RoundToInt(hy - py), pal['g']);
                        Line(cv, hx, hy, dx, dy, 1, 5, k => G(k == 5 ? edge : blade));
                        break;
                    }
                    case "axe":
                    {
                        Line(cv, hx, hy, dx, dy, -2, 8, k => wood[k % 2 == 0 ? 2 : 3]);
                        for (int k = 5; k <= 8; k++)
                            for (int s = 1; s <= 3; s++)
                            {
                                float w = s - (k == 5 || k == 8 ? 1 : 0);
                                if (w <= 0) continue;
                                cv.Set(Mathf.RoundToInt(hx + dx * k + px * w), Mathf.RoundToInt(hy + dy * k + py * w), G(s == 3 ? edge : s == 2 ? blade : dark));
                            }
                        break;
                    }
                    case "mace":
                    case "quarterstaff":
                    {
                        if (weapon.id == "quarterstaff")
                        {
                            Line(cv, hx, hy, dx, dy, -6, 9, k => wood[(k + 10) % 3 == 0 ? 2 : 3]);
                            cv.Set(Mathf.RoundToInt(hx + dx * 9), Mathf.RoundToInt(hy + dy * 9), M(3));
                            cv.Set(Mathf.RoundToInt(hx - dx * 6), Mathf.RoundToInt(hy - dy * 6), M(3));
                            break;
                        }
                        Line(cv, hx, hy, dx, dy, -2, 5, k => wood[3]);
                        for (int ox = -1; ox <= 1; ox++)
                            for (int oy = -1; oy <= 1; oy++)
                                cv.Set(Mathf.RoundToInt(hx + dx * 7) + ox, Mathf.RoundToInt(hy + dy * 7) + oy, G(ox + oy < 0 ? edge : blade));
                        cv.Set(Mathf.RoundToInt(hx + dx * 9), Mathf.RoundToInt(hy + dy * 9), G(edge));
                        cv.Set(Mathf.RoundToInt(hx + dx * 7 + px * 2), Mathf.RoundToInt(hy + dy * 7 + py * 2), G(dark));
                        cv.Set(Mathf.RoundToInt(hx + dx * 7 - px * 2), Mathf.RoundToInt(hy + dy * 7 - py * 2), G(dark));
                        break;
                    }
                    case "spear":
                    {
                        Line(cv, hx, hy, dx, dy, -5, 10, k => wood[k % 2 == 0 ? 2 : 3]);
                        Line(cv, hx, hy, dx, dy, 11, 13, k => G(k == 13 ? edge : blade));
                        cv.Set(Mathf.RoundToInt(hx + dx * 11 + px), Mathf.RoundToInt(hy + dy * 11 + py), G(dark));
                        cv.Set(Mathf.RoundToInt(hx + dx * 11 - px), Mathf.RoundToInt(hy + dy * 11 - py), G(dark));
                        break;
                    }
                    case "bow":
                    {
                        // the limbs bend back from the grip, the string runs straight
                        for (int k = -6; k <= 6; k++)
                        {
                            float bend = (36f - k * k) / 36f * 2.2f;
                            cv.Set(Mathf.RoundToInt(hx + px * k + dx * bend), Mathf.RoundToInt(hy + py * k + dy * bend), wood[Mathf.Abs(k) < 2 ? 1 : 3]);
                        }
                        float pull = swinging ? -1.5f : 0f;
                        for (int k = -5; k <= 5; k++)
                        {
                            float sx = hx + px * k + dx * (pull * (1f - Mathf.Abs(k) / 6f)), sy = hy + py * k + dy * (pull * (1f - Mathf.Abs(k) / 6f));
                            if (!cv.Opaque(Mathf.RoundToInt(sx), Mathf.RoundToInt(sy))) cv.Set(Mathf.RoundToInt(sx), Mathf.RoundToInt(sy), Hex("#e8e2d0"));
                        }
                        break;
                    }
                    case "staff":
                    {
                        Line(cv, hx, hy, dx, dy, -4, 9, k => wood[k % 3 == 0 ? 1 : 2]);
                        int ox = Mathf.RoundToInt(hx + dx * 11), oy = Mathf.RoundToInt(hy + dy * 11);
                        for (int i = -1; i <= 1; i++)
                            for (int j = -1; j <= 1; j++)
                                if (Mathf.Abs(i) + Mathf.Abs(j) < 2) cv.Set(ox + i, oy + j, i + j < 0 ? glow[4] : glow[2]);
                        cv.Set(ox, oy, Hex("#ffffff"));
                        break;
                    }
                    case "wand":
                    {
                        Line(cv, hx, hy, dx, dy, -1, 4, k => wood[1]);
                        cv.Set(Mathf.RoundToInt(hx + dx * 5), Mathf.RoundToInt(hy + dy * 5), glow[4]);
                        cv.Set(Mathf.RoundToInt(hx + dx * 5 + px), Mathf.RoundToInt(hy + dy * 5 + py), glow[2]);
                        break;
                    }
                    case "orb":
                    {
                        int ox = Mathf.RoundToInt(hx + dx * 3), oy = Mathf.RoundToInt(hy + dy * 3);
                        for (int i = -2; i <= 1; i++)
                            for (int j = -2; j <= 1; j++)
                            {
                                if ((i == -2 || i == 1) && (j == -2 || j == 1)) continue;
                                cv.Set(ox + i, oy + j, i + j < -1 ? glow[4] : i + j > 0 ? glow[1] : glow[2]);
                            }
                        cv.Set(ox - 1, oy - 1, Hex("#ffffff"));
                        break;
                    }
                    case "tome":
                    {
                        var cover = Ramp(HeroLook.Pick(HeroLook.Metals, look.metal) * 0.7f, 4);
                        for (int i = -2; i <= 1; i++)
                            for (int j = -2; j <= 2; j++)
                                cv.Set(hx + i, hy + j, i == -2 ? cover[1] : j == 2 || i == 1 ? pal['w'] : cover[2]);
                        cv.Set(hx - 1, hy, pal['G']);
                        break;
                    }
                    case "lute":
                    {
                        Line(cv, hx, hy, dx, dy, 2, 7, k => wood[1]);
                        cv.Set(Mathf.RoundToInt(hx + dx * 8), Mathf.RoundToInt(hy + dy * 8), wood[0]);
                        for (int i = -2; i <= 2; i++)
                            for (int j = -2; j <= 2; j++)
                                if (i * i + j * j <= 5) cv.Set(Mathf.RoundToInt(hx - dx * 1) + i, Mathf.RoundToInt(hy - dy * 1) + j, i * i + j * j <= 1 ? wood[0] : wood[3]);
                        break;
                    }
                    // fist: the hands are the weapon
                }
            }

            /// <summary>How the weapon rests in hand when not swung: long ones upright, blades down.</summary>
            float RestAngle(string dir)
            {
                if (weapon == null) return 90f;
                switch (weapon.id)
                {
                    case "spear":
                    case "staff":
                    case "quarterstaff":
                        return -85f;
                    case "bow":
                        return dir == "side" ? 0f : 180f;
                    case "orb":
                    case "tome":
                        return dir == "side" ? 0f : -90f;
                    case "lute":
                        return dir == "side" ? -40f : -60f;
                    default:
                        return dir == "side" ? 70f : dir == "up" ? 100f : 80f;
                }
            }

            // ------------------------------------------------------------ one pose
            Px Compose(string direction, (int, int) legs = default, int bob = 0, (int, int)[] arms = null, string head = "normal", int lean = 0,
                       (int, float)? sword = null, string armPose = null, bool blink = false, bool rest = true, int sway = 0)
            {
                arms = arms ?? new[] { (0, 0), (0, 0) };
                var cv = new Px(FW, FH);
                int topY = FeetY - 21 + bob + lift;
                int bodyY = topY + 11;
                int legY = FeetY - 3 + lift;
                string[] leg = Trim(Leg, lift), legBack = Trim(LegBack, lift);
                string mode = head == "hurt" ? "hurt" : blink ? "blink" : "normal";
                (int x, int y)[] hand;
                (int, float)? weaponAt = sword;
                if (weaponAt == null && rest && weapon != null && weapon.id != "fist")
                {
                    // a bow rests in the left hand, everything else in the right
                    int hi = direction == "down" ? (weapon.id == "bow" ? 0 : 1) : 0;
                    weaponAt = (hi, RestAngle(direction));
                }

                if (direction == "down")
                {
                    Cape(cv, direction, bodyY, lean, sway);
                    Tail(cv, direction, bodyY, lean);
                    cv.Blit(Part(leg), CX - 3 + lean, legY - legs.Item1);
                    cv.Blit(Part(leg), CX + 1 + lean, legY - legs.Item2);
                    Robe(cv, direction, bodyY, lean, sway);
                    if (armPose != "cast")
                    {
                        cv.Blit(Part(ArmL), CX - 6 + arms[0].Item1 + lean, bodyY + 1 + arms[0].Item2);
                        cv.Blit(Part(ArmR), CX + 4 + arms[1].Item1 + lean, bodyY + 1 + arms[1].Item2);
                    }
                    if (stout)
                    {
                        cv.Blit(Part(TorsoDown), CX - 5 + lean, bodyY);
                        cv.Blit(Part(TorsoDown), CX - 3 + lean, bodyY);
                    }
                    cv.Blit(Part(TorsoDown), CX - 4 + lean, bodyY);
                    Fur(cv, direction, bodyY, lean);
                    Tabard(cv, direction, bodyY, lean);
                    Pauldrons(cv, direction, bodyY, lean, arms);
                    Head(cv, direction, CX - 6 + lean, topY, mode);
                    if (armPose == "cast")
                    {
                        cv.Blit(Part(new[] { "Aa", "aa", "SS" }), CX - 5, bodyY + 1);
                        cv.Blit(Part(new[] { "aa", "aa", "SS" }), CX + 3, bodyY + 1);
                        hand = new[] { (CX - 4, bodyY + 3), (CX + 4, bodyY + 3) };
                    }
                    else
                        hand = new[] { (CX - 5 + arms[0].Item1 + lean, bodyY + 6 + arms[0].Item2), (CX + 5 + arms[1].Item1 + lean, bodyY + 6 + arms[1].Item2) };
                }
                else if (direction == "up")
                {
                    Tail(cv, direction, bodyY, lean);
                    cv.Blit(Part(legBack), CX - 3 + lean, legY - legs.Item1);
                    cv.Blit(Part(legBack), CX + 1 + lean, legY - legs.Item2);
                    Robe(cv, direction, bodyY, lean, sway);
                    hand = new[] { (CX - 5 + arms[0].Item1, bodyY + 6 + arms[0].Item2), (CX + 5 + arms[1].Item1, bodyY + 6 + arms[1].Item2) };
                    // the weapon behind the body when facing away
                    if (weaponAt != null)
                    {
                        var w = weaponAt.Value;
                        Weapon(cv, hand[w.Item1].x, hand[w.Item1].y, w.Item2, sword != null);
                        weaponAt = null;
                    }
                    cv.Blit(Part(ArmR), CX - 6 + arms[0].Item1, bodyY + 1 + arms[0].Item2);
                    cv.Blit(Part(ArmL), CX + 4 + arms[1].Item1, bodyY + 1 + arms[1].Item2);
                    if (stout)
                    {
                        cv.Blit(Part(TorsoUp), CX - 5, bodyY);
                        cv.Blit(Part(TorsoUp), CX - 3, bodyY);
                    }
                    cv.Blit(Part(TorsoUp), CX - 4, bodyY);
                    Fur(cv, direction, bodyY, lean);
                    Pauldrons(cv, direction, bodyY, lean, arms);
                    Cape(cv, direction, bodyY, lean, sway);
                    Head(cv, direction, CX - 6, topY, mode);
                    if (armPose == "cast")
                    {
                        cv.Blit(Part(new[] { "SS" }), CX - 5, bodyY);
                        cv.Blit(Part(new[] { "SS" }), CX + 3, bodyY);
                    }
                }
                else
                {
                    Cape(cv, direction, bodyY, lean, sway);
                    Tail(cv, direction, bodyY, lean);
                    cv.Blit(Part(legBack), CX - 2 - legs.Item2 + lean, legY - (legs.Item2 != 0 ? 1 : 0));
                    cv.Blit(Part(ArmBack), CX - 3 - arms[1].Item1 + lean, bodyY + 1 + arms[1].Item2);
                    if (stout) cv.Blit(Part(TorsoSide), CX - 4 + lean, bodyY);
                    cv.Blit(Part(TorsoSide), CX - 3 + lean, bodyY);
                    cv.Blit(Part(leg), CX - 1 + legs.Item1 + lean, legY - (legs.Item1 < 0 ? 1 : 0));
                    Robe(cv, direction, bodyY, lean, sway);
                    Fur(cv, direction, bodyY, lean);
                    Tabard(cv, direction, bodyY, lean);
                    Head(cv, direction, CX - 6 + lean, topY, mode);
                    if (armPose == "cast")
                    {
                        cv.Blit(Part(new[] { "AaaSS", "aaaSs" }), CX, bodyY + 2);
                        hand = new[] { (CX + 4, bodyY + 2), (CX + 4, bodyY + 2) };
                    }
                    else
                    {
                        cv.Blit(Part(ArmSide), CX - 1 + arms[0].Item1 + lean, bodyY + 1 + arms[0].Item2);
                        hand = new[] { (CX + arms[0].Item1 + lean, bodyY + 6 + arms[0].Item2), (CX + arms[0].Item1 + lean, bodyY + 6 + arms[0].Item2) };
                    }
                    Pauldrons(cv, direction, bodyY, lean, arms);
                }
                if (weaponAt != null)
                {
                    var w = weaponAt.Value;
                    var h = hand[Mathf.Clamp(w.Item1, 0, hand.Length - 1)];
                    Weapon(cv, h.x, h.y, w.Item2, sword != null);
                }
                cv.Outline(Outline);
                return cv;
            }

            static string[] Trim(string[] rows, int drop)
            {
                if (drop <= 0) return rows;
                drop = Mathf.Min(drop, rows.Length - 1);
                var r = new string[rows.Length - drop];
                Array.Copy(rows, drop, r, 0, r.Length);
                return r;
            }

            /// <summary>The attack's three frames per direction: the weapon's own swing (a bow draws and looses, a focus points).</summary>
            (int, float)[] Swing(string dir)
            {
                bool bow = weapon != null && (weapon.id == "bow" || weapon.focus);
                bool thrust = weapon != null && (weapon.id == "spear" || weapon.id == "rapier" || weapon.id == "dagger");
                if (dir == "down")
                    return bow ? new[] { (1, 90f), (1, 90f), (1, 90f) }
                        : thrust ? new[] { (1, 60f), (1, 90f), (1, 95f) }
                        : new[] { (1, -60f), (1, 110f), (1, 160f) };
                if (dir == "up")
                    return bow ? new[] { (1, -90f), (1, -90f), (1, -90f) }
                        : thrust ? new[] { (1, -60f), (1, -90f), (1, -95f) }
                        : new[] { (1, -30f), (1, -100f), (1, -150f) };
                return bow ? new[] { (0, 0f), (0, 0f), (0, 0f) }
                    : thrust ? new[] { (0, -20f), (0, 0f), (0, 5f) }
                    : new[] { (0, -120f), (0, -10f), (0, 50f) };
            }

            /// <summary>The weapon alone, pointing up and to the right (the weapon slot's icon).</summary>
            public Px WeaponAlone(int size)
            {
                var cv = new Px(size, size);
                Weapon(cv, size / 2 - 4, size / 2 + 4, -45f, false);
                return cv;
            }

            public Dictionary<string, List<Px>> All()
            {
                var A = new Dictionary<string, List<Px>>();
                foreach (var d in new[] { "down", "up", "side" })
                {
                    A["idle_" + d] = new List<Px> { Compose(d), Compose(d, bob: 1, blink: d == "down") };
                    if (d == "side")
                        A["walk_" + d] = new List<Px>
                        {
                            Compose(d, legs: (2, 2), arms: new[] { (1, 0), (1, 0) }, sway: 1),
                            Compose(d, bob: -1),
                            Compose(d, legs: (-2, -2), arms: new[] { (-1, 0), (-1, 0) }, sway: -1),
                            Compose(d, bob: -1),
                        };
                    else
                        A["walk_" + d] = new List<Px>
                        {
                            Compose(d, legs: (1, 0), arms: new[] { (0, 1), (0, -1) }, sway: 1),
                            Compose(d, bob: -1),
                            Compose(d, legs: (0, 1), arms: new[] { (0, -1), (0, 1) }, sway: -1),
                            Compose(d, bob: -1),
                        };
                }
                var sd = Swing("down");
                var su = Swing("up");
                var ss = Swing("side");
                bool bow = weapon != null && (weapon.id == "bow" || weapon.focus);
                A["attack_down"] = new List<Px>
                {
                    Compose("down", arms: new[] { (0, 0), bow ? (-1, -2) : (1, -3) }, sword: sd[0]),
                    Compose("down", arms: new[] { (0, 0), (-1, 0) }, sword: sd[1], bob: 1),
                    Compose("down", arms: new[] { (0, 0), bow ? (-1, 0) : (-3, 1) }, sword: sd[2], bob: 1),
                };
                A["attack_up"] = new List<Px>
                {
                    Compose("up", arms: new[] { (0, 0), (0, -2) }, sword: su[0]),
                    Compose("up", arms: new[] { (0, 0), (-2, -3) }, sword: su[1]),
                    Compose("up", arms: new[] { (0, 0), (-4, -2) }, sword: su[2]),
                };
                A["attack_side"] = new List<Px>
                {
                    Compose("side", arms: new[] { bow ? (2, -2) : (-2, -3), (0, 0) }, sword: ss[0]),
                    Compose("side", arms: new[] { (2, -1), (0, 0) }, sword: ss[1], lean: 1),
                    Compose("side", arms: new[] { (2, 1), (0, 0) }, sword: ss[2], lean: 1),
                };
                foreach (var d in new[] { "down", "up", "side" })
                {
                    A["cast_" + d] = new List<Px> { Compose(d, armPose: "cast"), Compose(d, armPose: "cast", bob: 1) };
                    A["hurt_" + d] = new List<Px> { Compose(d, head: "hurt", bob: 1) };
                }
                A["dash_down"] = new List<Px> { Compose("down", legs: (2, 0), bob: -1, sway: 1) };
                A["dash_up"] = new List<Px> { Compose("up", legs: (0, 2), bob: -1, sway: 1) };
                A["dash_side"] = new List<Px> { Compose("side", legs: (3, 3), lean: 2, bob: 1, arms: new[] { (-2, 0), (2, 0) }, sway: 2) };
                A["dead"] = new List<Px> { Compose("down", head: "hurt", rest: false).Fallen(6) };
                return A;
            }
        }
    }
}

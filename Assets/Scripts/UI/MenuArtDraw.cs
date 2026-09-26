using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// The pictures of <see cref="MenuArt"/>, drawn pixel by pixel: nothing here touches a
    /// texture, so the drawing can be checked outside Unity. Row 0 is the top row.
    /// </summary>
    public static partial class MenuArt
    {
        /// <summary>A picture being drawn (row 0 at the top).</summary>
        public sealed class Canvas
        {
            public readonly int w, h;
            public readonly Color32[] px;

            public Canvas(int w, int h)
            {
                this.w = w;
                this.h = h;
                px = new Color32[w * h];
            }

            public bool In(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;
            public Color32 Get(int x, int y) => In(x, y) ? px[y * w + x] : default;
            public bool Opaque(int x, int y) => In(x, y) && px[y * w + x].a > 0;

            public void Set(int x, int y, Color32 c)
            {
                if (In(x, y)) px[y * w + x] = c;
            }

            /// <summary>Paints over what is there, by the colour's alpha.</summary>
            public void Put(int x, int y, Color32 c)
            {
                if (!In(x, y) || c.a == 0) return;
                var b = px[y * w + x];
                if (c.a == 255 || b.a == 0)
                {
                    px[y * w + x] = c;
                    return;
                }
                float a = c.a / 255f, ba = b.a / 255f, oa = a + ba * (1f - a);
                byte Ch(byte s, byte d) => (byte)Math.Round((s * a + d * ba * (1f - a)) / oa);
                px[y * w + x] = new Color32(Ch(c.r, b.r), Ch(c.g, b.g), Ch(c.b, b.b), (byte)Math.Round(oa * 255f));
            }

            public void Rect(int x0, int y0, int rw, int rh, Color32 c)
            {
                for (int y = y0; y < y0 + rh; y++)
                    for (int x = x0; x < x0 + rw; x++)
                        Put(x, y, c);
            }

            public void Blit(Canvas o, int ox, int oy)
            {
                for (int y = 0; y < o.h; y++)
                    for (int x = 0; x < o.w; x++)
                        Put(ox + x, oy + y, o.px[y * o.w + x]);
            }

            /// <summary>A one-pixel rim around everything drawn.</summary>
            public Canvas Outline(Color32 c)
            {
                var mask = new bool[px.Length];
                for (int i = 0; i < px.Length; i++) mask[i] = px[i].a > 0;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        if (mask[y * w + x]) continue;
                        bool near = (x > 0 && mask[y * w + x - 1]) || (x < w - 1 && mask[y * w + x + 1]) ||
                                    (y > 0 && mask[(y - 1) * w + x]) || (y < h - 1 && mask[(y + 1) * w + x]);
                        if (near) px[y * w + x] = c;
                    }
                return this;
            }

            /// <summary>The same picture with a transparent margin (room for an outline).</summary>
            public Canvas Pad(int m)
            {
                var r = new Canvas(w + 2 * m, h + 2 * m);
                r.Blit(this, m, m);
                return r;
            }

            public Canvas MirrorX()
            {
                var r = new Canvas(w, h);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        r.px[y * w + (w - 1 - x)] = px[y * w + x];
                return r;
            }
        }

        // ================================================================== colours
        static Color32 Hex(string hex, byte a = 255)
        {
            int v = Convert.ToInt32(hex.TrimStart('#'), 16);
            return new Color32((byte)(v >> 16), (byte)(v >> 8), (byte)v, a);
        }

        static Color32 Mix(Color32 a, Color32 b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            byte L(byte x, byte y) => (byte)Math.Round(x + (y - x) * t);
            return new Color32(L(a.r, b.r), L(a.g, b.g), L(a.b, b.b), L(a.a, b.a));
        }

        static Color32 Alpha(Color32 c, float a) => new Color32(c.r, c.g, c.b, (byte)Math.Round(c.a * Math.Max(0f, Math.Min(1f, a))));

        static readonly Color32 Ink = Hex("#1a1008");
        static readonly Color32 White = Hex("#ffffff");

        /// <summary>The letters of the small icons below.</summary>
        static readonly Dictionary<char, Color32> Pal = new Dictionary<char, Color32>
        {
            { 'W', Hex("#ffffff") }, { 'w', Hex("#d6d6de") }, { 'k', Hex("#8a8a96") },
            { 'G', Hex("#ffe08a") }, { 'g', Hex("#e3b451") }, { 'd', Hex("#9c6d2a") }, { 'e', Hex("#5e3f16") },
            { 'R', Hex("#ff9a8a") }, { 'r', Hex("#e04848") }, { 'q', Hex("#9a2430") },
            { 'B', Hex("#b0e4ff") }, { 'b', Hex("#3f9ff0") }, { 'n', Hex("#235ca8") },
            { 'L', Hex("#b4f08a") }, { 'l', Hex("#5cc458") }, { 'm', Hex("#2c7a3c") },
            { 'P', Hex("#e0d6ff") }, { 'p', Hex("#a090ec") }, { 'v', Hex("#5a48a8") },
            { 'C', Hex("#f2e8d2") }, { 'S', Hex("#f6c9a0") }, { 'N', Hex("#b07a48") }, { 'u', Hex("#6e4628") },
            { 'F', Hex("#fff0a0") }, { 'f', Hex("#ffa030") }, { 'h', Hex("#d85020") },
            { 'o', Ink },
        };

        /// <summary>A small picture from rows of letters (see <see cref="Pal"/>; '.' is empty).</summary>
        static Canvas Ascii(params string[] rows)
        {
            int w = 0;
            foreach (var r in rows) w = Math.Max(w, r.Length);
            var c = new Canvas(w, rows.Length);
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    if (Pal.TryGetValue(rows[y][x], out var col)) c.Set(x, y, col);
            return c;
        }

        /// <summary>An icon in colour: padded and ringed in dark ink so it reads on any panel.</summary>
        static Canvas Inked(Color32 ink, params string[] rows) => Ascii(rows).Pad(1).Outline(ink);

        // ================================================================== panels and cards
        /// <summary>Rounded corners: how many pixels each corner row leaves out, from the edge in.</summary>
        static readonly int[] Corner = { 2, 1 };

        static bool InRound(int x, int y, int w, int h)
        {
            int fx = Math.Min(x, w - 1 - x), fy = Math.Min(y, h - 1 - y);
            return fy >= Corner.Length || fx >= Corner[fy];
        }

        /// <summary>A white rounded rectangle, tinted by the image (card and button faces).</summary>
        static Canvas RoundFill()
        {
            var c = new Canvas(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    if (InRound(x, y, 16, 16)) c.Set(x, y, White);
            return c;
        }

        /// <summary>The rim of <see cref="RoundFill"/>, <paramref name="width"/> pixels thick.</summary>
        static Canvas RoundLine(int width)
        {
            var c = new Canvas(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    if (!InRound(x, y, 16, 16)) continue;
                    // on the rim: something outside within `width` steps (no diagonals, so corners stay thin)
                    bool rim = false;
                    for (int dy = -width; dy <= width && !rim; dy++)
                        for (int dx = -width; dx <= width && !rim; dx++)
                        {
                            if (Math.Abs(dx) + Math.Abs(dy) > width) continue;
                            int nx = x + dx, ny = y + dy;
                            rim = nx < 0 || ny < 0 || nx >= 16 || ny >= 16 || !InRound(nx, ny, 16, 16);
                        }
                    if (rim) c.Set(x, y, White);
                }
            return c;
        }

        /// <summary>A rounded face lit from above: white at the top to grey at the bottom, tinted by the image (the gold buttons).</summary>
        static Canvas RoundGradient()
        {
            var c = new Canvas(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    if (!InRound(x, y, 16, 16)) continue;
                    float v = y == 1 ? 1f : 1f - 0.2f * y / 15f;
                    if (y == 0) v = 0.96f;
                    byte b = (byte)Math.Round(255 * v);
                    c.Set(x, y, new Color32(b, b, b, 255));
                }
            return c;
        }

        /// <summary>
        /// A thin gold frame with an ornament in each corner (the preview, the title's menu, the
        /// class details), 40×40 and sliced 16 from each side; the middle is empty.
        /// </summary>
        static Canvas FrameGold()
        {
            const int S = 40;
            var bright = Hex("#f6d98a");
            var gold = Hex("#caa052");
            var dim = Hex("#7a5d2a", 170);
            var deep = Hex("#4a3818");
            var c = new Canvas(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int fx = Math.Min(x, S - 1 - x), fy = Math.Min(y, S - 1 - y);
                    Color32 col = default;
                    // the two lines along every side
                    if ((fy == 4 && fx >= 4) || (fx == 4 && fy >= 4)) col = gold;
                    else if ((fy == 6 && fx >= 6) || (fx == 6 && fy >= 6)) col = dim;
                    // the corner: a bracket outside the lines, joined to them, and a diamond knob
                    bool bracket = (fy == 1 && fx >= 5 && fx <= 12) || (fx == 12 && fy >= 1 && fy <= 3) ||
                                   (fx == 1 && fy >= 5 && fy <= 12) || (fy == 12 && fx >= 1 && fx <= 3);
                    if (bracket) col = bright;
                    int dd = Math.Abs(fx - 2) + Math.Abs(fy - 2);
                    if (dd <= 2) col = dd == 0 ? deep : dd == 1 ? bright : gold;
                    if (col.a > 0) c.Set(x, y, col);
                }
            return c;
        }

        // ================================================================== the preview's stage
        /// <summary>A round stone pedestal the hero stands on (60×27).</summary>
        static Canvas Pedestal()
        {
            const int W = 60, H = 27;
            const float cx = 30f, rx = 28.5f, ry = 7.2f, top = 8.5f, bottom = 18.5f;
            var c = new Canvas(W, H);
            var light = Hex("#8a8478");
            var mid = Hex("#5e5950");
            var dark = Hex("#3c3832");
            var deep = Hex("#221f1b");
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f - cx) / rx;
                    float ty = (y + 0.5f - top) / ry, by = (y + 0.5f - bottom) / ry;
                    bool onTop = dx * dx + ty * ty <= 1f;
                    bool onSide = Math.Abs(dx) <= 1f && (y + 0.5f >= top && y + 0.5f <= bottom || dx * dx + by * by <= 1f);
                    if (!onTop && !onSide) continue;
                    float lit = 0.5f - dx * 0.35f;   // light from the upper left
                    Color32 col;
                    if (onTop)
                    {
                        float r = (float)Math.Sqrt(dx * dx + ty * ty);
                        col = r > 0.93f ? Mix(mid, light, lit) : r > 0.74f && r < 0.8f ? Mix(dark, mid, lit) : Mix(Mix(mid, light, 0.55f), light, lit);
                        // flagstones on the top
                        double ang = Math.Atan2(ty, dx);
                        if (r < 0.74f && r > 0.2f && Math.Abs(Math.Sin(ang * 3)) < 0.08) col = Mix(col, dark, 0.6f);
                    }
                    else
                    {
                        // the side: bricks on a cylinder
                        double u = Math.Asin(Math.Max(-1f, Math.Min(1f, dx)));
                        int row = y + 0.5f < (top + bottom) / 2f + 0.5f ? 0 : 1;
                        double seam = (u / Math.PI * 7.0 + (row == 1 ? 0.5 : 0.0)) % 1.0;
                        if (seam < 0) seam += 1.0;
                        col = Mix(deep, mid, lit * 1.1f);
                        if (seam < 0.07 || Math.Abs(y + 0.5f - ((top + bottom) / 2f + 0.5f)) < 0.5f) col = Mix(col, deep, 0.7f);
                        if (dx * dx + by * by > 0.8f && y + 0.5f > bottom) col = Mix(col, deep, 0.5f);
                    }
                    c.Set(x, y, col);
                }
            return c.Outline(Hex("#141210"));
        }

        /// <summary>
        /// A dungeon alcove behind the hero on the class step and in the class details: a stone
        /// wall, an arch, red banners and two torches whose light warms the stones. The edges fade
        /// out into the panel (108×76).
        /// </summary>
        static Canvas Dungeon()
        {
            const int W = 108, H = 76;
            var c = new Canvas(W, H);
            var mortar = Hex("#15120f");
            var stoneA = Hex("#2e2923");
            var stoneB = Hex("#37312a");
            var inner = Hex("#0f0c0c");
            var rim = Hex("#4d453b");
            const float ax = 54f, ay = 38f, ar = 22f;
            const int floorY = 60;
            // wall and arch
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color32 col;
                    if (y >= floorY)
                    {
                        // floor: flat stones in rows, darker towards the viewer
                        int row = (y - floorY) / 4;
                        bool seam = (y - floorY) % 4 == 3 || (x + row * 5) % 13 == 0;
                        col = seam ? mortar : Mix(stoneB, stoneA, (y - floorY) / 16f);
                    }
                    else
                    {
                        int by = y / 6, bx = (x + (by % 2 == 1 ? 6 : 0)) / 12;
                        bool seam = y % 6 == 5 || (x + (by % 2 == 1 ? 6 : 0)) % 12 == 11;
                        col = seam ? mortar : ((bx * 7 + by * 3) % 5 < 2 ? stoneA : stoneB);
                    }
                    // the alcove
                    float dxa = x + 0.5f - ax, dya = y + 0.5f - ay;
                    bool inArch = Math.Abs(dxa) < ar && (dya > 0 || dxa * dxa + dya * dya < ar * ar) && y < floorY + 2;
                    bool onRim = !inArch && Math.Abs(dxa) < ar + 3 && (dya > 0 ? y < floorY : dxa * dxa + dya * dya < (ar + 3) * (ar + 3));
                    if (inArch) col = Mix(inner, stoneA, Math.Max(0f, (y - 20f) / 60f) * 0.5f);
                    else if (onRim)
                    {
                        double ang = Math.Atan2(dya, dxa);
                        bool rimSeam = dya < 0 ? Math.Abs(Math.Sin(ang * 6)) < 0.12 : y % 7 == 6;
                        col = rimSeam ? mortar : rim;
                    }
                    c.Set(x, y, col);
                }
            // banners
            foreach (int bx0 in new[] { 8, 88 })
            {
                var red = Hex("#7c1d22");
                var redLit = Hex("#a02a2e");
                var trim = Hex("#c9a052");
                for (int x = bx0 - 1; x < bx0 + 13; x++) c.Set(x, 6, Hex("#5a3a1c"));
                for (int y = 7; y < 40; y++)
                    for (int x = bx0; x < bx0 + 12; x++)
                    {
                        int cut = y - 33;   // the swallow tail at the bottom
                        if (cut > 0 && Math.Abs(x - (bx0 + 5.5f)) < cut) continue;
                        Color32 col = x < bx0 + 3 ? redLit : red;
                        if (x == bx0 || x == bx0 + 11) col = Hex("#4e1014");
                        if (y == 9 || y == 30) col = trim;
                        c.Set(x, y, col);
                    }
                // an emblem
                for (int y = 16; y < 24; y++)
                    for (int x = bx0 + 3; x < bx0 + 9; x++)
                        if (Math.Abs(x - (bx0 + 5.5f)) + Math.Abs(y - 19.5f) < 3.6f) c.Set(x, y, trim);
            }
            // torches and their light
            foreach (int tx in new[] { 28, 79 })
            {
                const int ty = 30;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float d = (float)Math.Sqrt((x - tx) * (x - tx) + (y - ty) * (y - ty) * 1.2f);
                        float glow = Math.Max(0f, 1f - d / 26f);
                        if (glow <= 0f) continue;
                        c.Put(x, y, Alpha(Hex("#ff9a3c"), glow * glow * 0.55f));
                    }
                c.Rect(tx - 1, ty + 1, 3, 6, Hex("#3a2414"));
                c.Rect(tx - 2, ty, 5, 2, Hex("#6e4628"));
                string[] flame = { "..F..", ".FFf.", ".FfFf", "fFFfh", ".ffh.", "..h.." };
                for (int y = 0; y < flame.Length; y++)
                    for (int x = 0; x < flame[y].Length; x++)
                        if (Pal.TryGetValue(flame[y][x], out var fc)) c.Set(tx - 2 + x, ty - 6 + y, fc);
            }
            // fade into the panel at the sides and the top
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float e = Math.Min(Math.Min(x, W - 1 - x) / 14f, Math.Min(y / 10f, (H - 1 - y) / 6f));
                    float a = Math.Max(0f, Math.Min(1f, e));
                    var p = c.px[y * W + x];
                    c.px[y * W + x] = Alpha(p, a * a * (3f - 2f * a));
                }
            return c;
        }

        /// <summary>A soft round glow, white in the middle and clear at the rim (tinted; drawn smooth, not in pixels).</summary>
        static Canvas Glow()
        {
            const int S = 64;
            var c = new Canvas(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = (float)Math.Sqrt((x + 0.5f - S / 2f) * (x + 0.5f - S / 2f) + (y + 0.5f - S / 2f) * (y + 0.5f - S / 2f)) / (S / 2f);
                    float a = Math.Max(0f, 1f - d);
                    c.Set(x, y, new Color32(255, 255, 255, (byte)Math.Round(255 * a * a)));
                }
            return c;
        }

        /// <summary>A soft band, clear at both ends (the title's shade behind the menu, dividers).</summary>
        static Canvas FadeBand()
        {
            const int S = 64;
            var c = new Canvas(S, 4);
            for (int x = 0; x < S; x++)
            {
                float t = Math.Abs(x + 0.5f - S / 2f) / (S / 2f);
                byte a = (byte)Math.Round(255 * Math.Max(0f, 1f - t * t));
                for (int y = 0; y < 4; y++) c.Set(x, y, new Color32(255, 255, 255, a));
            }
            return c;
        }

        // ================================================================== icons
        static Canvas Leaves() => Inked(Hex("#0e240e"),
            "..........LLLL...",
            ".LLL.....LLlllL..",
            "LllmL...LLllllmL.",
            "LlllmL..LllllllmL",
            ".LlllmL.Llllllmm.",
            "..LlllmLllllmmm..",
            "...LLlmmllllmm...",
            ".....Lmmlmmm.....",
            ".......mm........",
            ".......mm........",
            ".......mm........");

        static Canvas Sparkle() => Ascii(
            "....G....",
            "....G....",
            "...GgG...",
            "..GgWgG..",
            "GGgWWWgGG",
            "..GgWgG..",
            "...GgG...",
            "....G....",
            "....G....");

        static Canvas SparkleSmall() => Ascii(
            "..G..",
            ".GgG.",
            "GgWgG",
            ".GgG.",
            "..G..");

        static Canvas Star() => Inked(Hex("#3a2208"),
            "....G....",
            "...GGG...",
            "GGGGWGGGG",
            ".GGgggGG.",
            "..Ggggg..",
            "..GgdgG..",
            ".Ggd.dgG.",
            ".Gd...dG.");

        static Canvas Heart() => Inked(Hex("#2a0c10"),
            ".RR...rr.",
            "RWRr.rrrq",
            "RRrrrrrrq",
            "rrrrrrrrq",
            ".rrrrrrq.",
            "..rrrrq..",
            "...rrq...",
            "....q....");

        static Canvas HeartGold() => Inked(Hex("#2a1808"),
            ".GG...gg.",
            "GWGg.gggd",
            "GGggggggd",
            "ggggggggd",
            ".ggggggd.",
            "..ggggd..",
            "...ggd...",
            "....d....");

        static Canvas Drop() => Inked(Hex("#0a1830"),
            "...B...",
            "...B...",
            "..BBb..",
            "..Bbb..",
            ".BWbbb.",
            ".BWbbbn",
            "BWbbbbn",
            "Bbbbbbn",
            ".bbbbn.",
            "..nnn..");

        static Canvas Die() => Inked(Hex("#1a1430"),
            ".PPPPPPP.",
            "PPPPPPPPp",
            "PPvPPPPPp",
            "PPPPPPPPp",
            "PPPPvPPPp",
            "PPPPPPPPp",
            "PPPPPPvPp",
            "PPPPPPPPp",
            ".ppppppp.");

        static Canvas DieGold() => Inked(Hex("#2a1808"),
            ".GGGGGGG.",
            "GGGGGGGGg",
            "GGeGGGGGg",
            "GGGGGGGGg",
            "GGGGeGGGg",
            "GGGGGGGGg",
            "GGGGGGeGg",
            "GGGGGGGGg",
            ".ggggggg.");

        static Canvas Fist() => Inked(Hex("#2a1808"),
            ".GGGGGG..",
            "GgGgGgGG.",
            "GgGgGgGgG",
            "GGGGGGGgG",
            ".GgggggGd",
            ".Gggggggd",
            ".Gggggggd",
            "..Gggggd.",
            "..Gggggd.");

        static Canvas Hourglass() => Inked(Hex("#2a1808"),
            "GGGGGGGG",
            ".dGGGGd.",
            ".dggggd.",
            "..dggd..",
            "...gd...",
            "...dg...",
            "..dGGd..",
            ".dGggGd.",
            ".dggggd.",
            "GGGGGGGG");

        static Canvas Book() => Inked(Hex("#2a1808"),
            "dGGG..GGGd",
            "GCCCGGCCCG",
            "GCkCGGCkCG",
            "GCCCGGCCCG",
            "GCkCGGCkCG",
            "GCCCGGCCCG",
            "GGGGGGGGGG",
            ".dddddddd.");

        static Canvas Crown() => Inked(Hex("#2a1808"),
            "G...GG...G",
            "GG.GggG.GG",
            "GgGgggggGg",
            "GggggggggG",
            "GgRgggBggG",
            "GggggggggG",
            "GGGGGGGGGG",
            ".dddddddd.");

        static Canvas Swords() => Inked(Hex("#2a1808"),
            "G........G",
            ".G......G.",
            "..G....G..",
            "...G..G...",
            "....GG....",
            "....GG....",
            "...G..G...",
            ".dG....Gd.",
            "..d....d..",
            ".d......d.");

        static Canvas Shield() => Inked(Hex("#2a1808"),
            "GGGGGGGGG",
            "GgggGgggd",
            "GgggGgggd",
            "GGGGGGGGd",
            "GgggGgggd",
            ".GggGggd.",
            ".GggGggd.",
            "..GgGgd..",
            "...GGd...",
            "....d....");

        static Canvas Armor() => Inked(Hex("#2a1808"),
            ".GG....GG.",
            "GggG..Gggd",
            "GgggGGgggd",
            "Gggggggggd",
            ".Gggggggd.",
            ".GgggggGd.",
            ".Gggggggd.",
            ".Gggggggd.",
            "..dddddd..");

        /// <summary>A hero's head and shoulders (the title's character card: the title knows the name, not the look).</summary>
        static Canvas HeroBust() => Inked(Hex("#1c1420"),
            "...uuuuuuu...",
            "..uNNNNNNNu..",
            ".uNNNNNNNNNu.",
            ".uNNNuuuNNNu.",
            ".uNuSSSSSuNu.",
            ".uuSSSSSSSuu.",
            "..uSoSSSoSu..",
            "..uSSSSSSSu..",
            "...SSSRRSS...",
            "....SSSSS....",
            "..bbbSSSbbb..",
            ".bbbBbbbBbbb.",
            ".bBbbbbbbbBb.",
            ".bbbbnnnbbbb.");

        // white icons, tinted by the image
        static Canvas Check() => Ascii(
            ".......W",
            "......WW",
            "W....WW.",
            "WW..WW..",
            ".WWWW...",
            "..WW....");

        static Canvas ChevronRight() => Ascii(
            "WW...",
            ".WW..",
            "..WW.",
            "...WW",
            "...WW",
            "..WW.",
            ".WW..",
            "WW...");

        static Canvas ChevronDown() => Ascii(
            "W......W",
            "WW....WW",
            ".WW..WW.",
            "..WWWW..",
            "...WW...");

        static Canvas Close() => Ascii(
            "WW....WW",
            "WWW..WWW",
            ".WWWWWW.",
            "..WWWW..",
            "..WWWW..",
            ".WWWWWW.",
            "WWW..WWW",
            "WW....WW");

        static Canvas Pencil() => Ascii(
            "......WW.",
            ".....WkWW",
            "....WkWW.",
            "...WkWW..",
            "..WkWW...",
            ".WkWW....",
            "WWWW.....",
            "WWW......",
            "W........");

        static Canvas ServerList() => Ascii(
            "WWWWWWWWWW",
            "WkWkWWWWWW",
            "WWWWWWWWWW",
            "..........",
            "WWWWWWWWWW",
            "WkWkWWWWWW",
            "WWWWWWWWWW",
            "..........",
            "WWWWWWWWWW",
            "WkWkWWWWWW",
            "WWWWWWWWWW");

        static Canvas Info() => Ascii(
            "..WWWWW..",
            ".W.....W.",
            "W...W...W",
            "W.......W",
            "W...W...W",
            "W...W...W",
            "W...W...W",
            ".W.....W.",
            "..WWWWW..");

        static Canvas Signal() => Ascii(
            ".........WW",
            ".........WW",
            "......WW.WW",
            "......WW.WW",
            "...WW.WW.WW",
            "...WW.WW.WW",
            "WW.WW.WW.WW",
            "WW.WW.WW.WW");

        static Canvas Dot() => Ascii(
            ".WWWW.",
            "WWWWWW",
            "WWWWWW",
            "WWWWWW",
            "WWWWWW",
            ".WWWW.");

        static Canvas DiamondOutline() => Ascii(
            "...W...",
            "..W.W..",
            ".W...W.",
            "W..W..W",
            ".W...W.",
            "..W.W..",
            "...W...");

        static Canvas Diamond() => Ascii(
            "..W..",
            ".WWW.",
            "WWWWW",
            ".WWW.",
            "..W..");

        // ================================================================== the list
        /// <summary>How one picture is drawn and cut: its 9-slice border and UI units per pixel.</summary>
        struct Def
        {
            public Func<Canvas> draw;
            public int border;
            public float scale;
            public bool smooth;
        }

        static readonly Dictionary<string, Def> Defs = new Dictionary<string, Def>
        {
            { "round_fill", new Def { draw = RoundFill, border = 5, scale = 2f } },
            { "round_line", new Def { draw = () => RoundLine(1), border = 5, scale = 2f } },
            { "round_line2", new Def { draw = () => RoundLine(2), border = 5, scale = 2f } },
            { "round_grad", new Def { draw = RoundGradient, border = 5, scale = 2f } },
            { "frame_gold", new Def { draw = FrameGold, border = 16, scale = 2f } },
            { "pedestal", new Def { draw = Pedestal, scale = 3f } },
            { "dungeon", new Def { draw = Dungeon, scale = 4f } },
            { "glow", new Def { draw = Glow, scale = 4f, smooth = true } },
            { "fade_band", new Def { draw = FadeBand, scale = 4f, smooth = true } },
            { "leaves", new Def { draw = Leaves, scale = 4f } },
            { "sparkle", new Def { draw = Sparkle, scale = 3f } },
            { "sparkle_small", new Def { draw = SparkleSmall, scale = 3f } },
            { "star", new Def { draw = Star, scale = 3f } },
            { "heart", new Def { draw = Heart, scale = 3f } },
            { "heart_gold", new Def { draw = HeartGold, scale = 3f } },
            { "drop", new Def { draw = Drop, scale = 3f } },
            { "die", new Def { draw = Die, scale = 3f } },
            { "die_gold", new Def { draw = DieGold, scale = 3f } },
            { "fist", new Def { draw = Fist, scale = 3f } },
            { "hourglass", new Def { draw = Hourglass, scale = 3f } },
            { "book", new Def { draw = Book, scale = 3f } },
            { "crown", new Def { draw = Crown, scale = 3f } },
            { "swords", new Def { draw = Swords, scale = 3f } },
            { "shield", new Def { draw = Shield, scale = 3f } },
            { "armor", new Def { draw = Armor, scale = 3f } },
            { "hero_bust", new Def { draw = HeroBust, scale = 5f } },
            { "check", new Def { draw = Check, scale = 3f } },
            { "chevron_right", new Def { draw = ChevronRight, scale = 3f } },
            { "chevron_left", new Def { draw = () => ChevronRight().MirrorX(), scale = 3f } },
            { "chevron_down", new Def { draw = ChevronDown, scale = 3f } },
            { "close", new Def { draw = Close, scale = 3f } },
            { "pencil", new Def { draw = Pencil, scale = 3f } },
            { "server_list", new Def { draw = ServerList, scale = 3f } },
            { "info", new Def { draw = Info, scale = 3f } },
            { "signal", new Def { draw = Signal, scale = 3f } },
            { "dot", new Def { draw = Dot, scale = 3f } },
            { "diamond_outline", new Def { draw = DiamondOutline, scale = 3f } },
            { "diamond", new Def { draw = Diamond, scale = 3f } },
        };

        /// <summary>Every picture's name (the preview tool draws them all).</summary>
        public static IEnumerable<string> Names => Defs.Keys;

        /// <summary>Draws one picture by name (null for an unknown name).</summary>
        public static Canvas Draw(string name) => Defs.TryGetValue(name, out var d) ? d.draw() : null;
    }
}

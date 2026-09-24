"""
pixelkit - tiny pixel-art toolkit used by the art generators.

Everything is drawn on numpy RGBA arrays (H, W, 4) so we can do per-pixel
logic (shading, outlines, dithering) and then save to PNG with PIL.
"""
from __future__ import annotations

import math
import random
from typing import Callable, Iterable, Sequence

import numpy as np
from PIL import Image

# ----------------------------------------------------------------------------
# Colors
# ----------------------------------------------------------------------------

Color = tuple  # (r, g, b, a)


def hx(h: str, a: int = 255) -> Color:
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def mix(c1: Color, c2: Color, t: float) -> Color:
    return tuple(int(round(c1[i] + (c2[i] - c1[i]) * t)) for i in range(4))


def with_alpha(c: Color, a: int) -> Color:
    return (c[0], c[1], c[2], a)


def ramp(*hexes: str) -> list:
    return [hx(h) for h in hexes]


# A hand-tuned palette: warm, slightly desaturated, forest-y.
OUTLINE = hx("#1c1420")
OUTLINE_SOFT = hx("#2e1f2c")

P = {
    # ground
    "grass": ramp("#1f3a24", "#2b4d2b", "#3a6333", "#4d7a3b", "#669447", "#86ad5a"),
    "dgrass": ramp("#15291d", "#1c3424", "#24422b", "#2e5232", "#3a6338"),
    "dirt": ramp("#3e2a22", "#5a3d2b", "#74513a", "#8f6a4a", "#a8845e", "#c2a078"),
    "stone": ramp("#23222e", "#383848", "#50526a", "#6c7088", "#8e94ab", "#b4bbcf"),
    "moss": ramp("#233d1e", "#35592a", "#4f7d35", "#74a346"),
    "wood": ramp("#2c1a14", "#48291c", "#643c26", "#855433", "#a56f45", "#c48d5c"),
    "leaf": ramp("#122a1c", "#1a3a24", "#24502d", "#306638", "#417f43", "#5c9a4f"),
    "pine": ramp("#0e2220", "#153230", "#1d4438", "#285840", "#366e4a", "#4a8656"),
    "autumn": ramp("#5a1d14", "#8a2e1a", "#b84a24", "#de7236", "#f59f52"),
    # characters
    "skin": ramp("#8a4a3a", "#c47a5a", "#eeb088", "#ffd6b4"),
    "hair_red": ramp("#5a1628", "#8f2238", "#c23a45", "#e8605a", "#ff8f78"),
    "cloth": ramp("#5a5a78", "#8e92b0", "#c8cde0", "#eef1fa"),
    "red": ramp("#4a0e1a", "#7c1a26", "#b52a34", "#e04a44", "#ff7a62"),
    "blue": ramp("#101a44", "#1c3278", "#2c58b0", "#4a8ae0", "#86c4f5", "#c8ecff"),
    "gold": ramp("#4a2e10", "#7c5214", "#b8821e", "#e8b634", "#ffe07a", "#fff4c4"),
    "purple": ramp("#22123a", "#3c1f66", "#6232a0", "#9454cc", "#c48af0", "#ecc8ff"),
    "green": ramp("#10301a", "#1e5a28", "#2e8a36", "#50b848", "#8ae06a", "#c8ff9a"),
    "ice": ramp("#10304e", "#1e5a8a", "#3a8ec4", "#6ec4ec", "#b0ecff", "#f0ffff"),
    "fire": ramp("#4a0c06", "#8e1c0a", "#d2400e", "#f5781a", "#ffb43a", "#ffe680", "#fffbe0"),
    "pants": ramp("#1c1a30", "#2c2a48", "#403e64", "#57557e"),
    "leather": ramp("#2e1a14", "#4a2c1e", "#6c4028", "#8e5a36", "#b07a4c"),
    "metal": ramp("#2a2e40", "#4a5068", "#737a96", "#a4acc6", "#d6dcee", "#ffffff"),
    "fur": ramp("#0f0f18", "#1a1a26", "#262636", "#34344a", "#46465e", "#5c5c78"),
    "fur_brown": ramp("#2a1812", "#40261a", "#5a3824", "#784c30", "#96643e"),
    "bone": ramp("#6a5a50", "#a8988a", "#d8ccbc", "#f6efe2"),
    "white": ramp("#8a8aa0", "#c0c0d4", "#e8e8f4", "#ffffff"),
    "pink_ui": ramp("#26121e", "#3e1e30", "#5e3048", "#86506a", "#b07a92", "#d8a8bc"),
    "ui_dark": ramp("#0e0b12", "#17121c", "#221a28", "#302434", "#443444"),
    "slime": ramp("#123a1a", "#1e5e26", "#34883a", "#5cb450", "#94dc76", "#d4ffb0"),
    "shroom_red": ramp("#4a0c18", "#7e1624", "#b42a30", "#e0503e", "#ff8866"),
    "cream": ramp("#6a5040", "#a88a6a", "#dcc49a", "#f6e8c4", "#fffaea"),
}

# ----------------------------------------------------------------------------
# Bayer dithering
# ----------------------------------------------------------------------------

BAYER4 = np.array(
    [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], dtype=np.float32
) / 16.0


def bayer(x: int, y: int) -> float:
    return float(BAYER4[y & 3, x & 3])


# ----------------------------------------------------------------------------
# Canvas
# ----------------------------------------------------------------------------


class Canvas:
    def __init__(self, w: int, h: int):
        self.w, self.h = w, h
        self.a = np.zeros((h, w, 4), dtype=np.uint8)

    # -- basic ops -----------------------------------------------------------
    def copy(self) -> "Canvas":
        c = Canvas(self.w, self.h)
        c.a = self.a.copy()
        return c

    def inb(self, x: int, y: int) -> bool:
        return 0 <= x < self.w and 0 <= y < self.h

    def px(self, x: int, y: int, c: Color | None):
        if c is None:
            return
        x, y = int(x), int(y)
        if not self.inb(x, y):
            return
        if len(c) == 3:
            c = (c[0], c[1], c[2], 255)
        if c[3] >= 255:
            self.a[y, x] = c
        elif c[3] > 0:
            # alpha blend over existing
            dst = self.a[y, x].astype(np.float32)
            sa = c[3] / 255.0
            da = dst[3] / 255.0
            oa = sa + da * (1 - sa)
            if oa <= 0:
                return
            rgb = (np.array(c[:3], np.float32) * sa + dst[:3] * da * (1 - sa)) / oa
            self.a[y, x] = (*[int(v) for v in rgb], int(oa * 255))

    def get(self, x: int, y: int) -> Color:
        if not self.inb(x, y):
            return (0, 0, 0, 0)
        return tuple(int(v) for v in self.a[y, x])

    def opaque(self, x: int, y: int) -> bool:
        return self.inb(x, y) and self.a[y, x, 3] > 0

    def clear(self, x: int, y: int):
        if self.inb(x, y):
            self.a[y, x] = (0, 0, 0, 0)

    def rect(self, x0, y0, w, h, c):
        for y in range(y0, y0 + h):
            for x in range(x0, x0 + w):
                self.px(x, y, c)

    def hline(self, x0, x1, y, c):
        for x in range(min(x0, x1), max(x0, x1) + 1):
            self.px(x, y, c)

    def vline(self, x, y0, y1, c):
        for y in range(min(y0, y1), max(y0, y1) + 1):
            self.px(x, y, c)

    def line(self, x0, y0, x1, y1, c, width: int = 1):
        x0, y0, x1, y1 = int(round(x0)), int(round(y0)), int(round(x1)), int(round(y1))
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx = 1 if x0 < x1 else -1
        sy = 1 if y0 < y1 else -1
        err = dx + dy
        while True:
            if width <= 1:
                self.px(x0, y0, c)
            else:
                r = width // 2
                for oy in range(-r, width - r):
                    for ox in range(-r, width - r):
                        self.px(x0 + ox, y0 + oy, c)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def ellipse(self, cx, cy, rx, ry, c):
        for y in range(int(cy - ry - 1), int(cy + ry + 2)):
            for x in range(int(cx - rx - 1), int(cx + rx + 2)):
                dx = (x + 0.5 - cx) / max(rx, 0.01)
                dy = (y + 0.5 - cy) / max(ry, 0.01)
                if dx * dx + dy * dy <= 1.0:
                    self.px(x, y, c)

    def blit(self, other: "Canvas", ox: int, oy: int, flip_x: bool = False):
        for y in range(other.h):
            for x in range(other.w):
                sx = other.w - 1 - x if flip_x else x
                c = other.get(sx, y)
                if c[3] > 0:
                    self.px(ox + x, oy + y, c)

    def flipped(self) -> "Canvas":
        c = Canvas(self.w, self.h)
        c.a = self.a[:, ::-1].copy()
        return c

    def shifted(self, dx: int, dy: int) -> "Canvas":
        c = Canvas(self.w, self.h)
        c.blit(self, dx, dy)
        return c

    def mask(self) -> np.ndarray:
        return self.a[:, :, 3] > 0

    # -- stylistic passes ------------------------------------------------------
    def outline(self, c: Color = OUTLINE, diagonal: bool = False, only_below=False):
        """Adds a 1px outline around all opaque pixels."""
        m = self.mask()
        out = np.zeros_like(m)
        h, w = m.shape
        offs = [(1, 0), (-1, 0), (0, 1), (0, -1)]
        if diagonal:
            offs += [(1, 1), (-1, 1), (1, -1), (-1, -1)]
        for dx, dy in offs:
            shifted = np.zeros_like(m)
            ys = slice(max(dy, 0), h + min(dy, 0))
            yd = slice(max(-dy, 0), h + min(-dy, 0))
            xs = slice(max(dx, 0), w + min(dx, 0))
            xd = slice(max(-dx, 0), w + min(-dx, 0))
            shifted[ys, xs] = m[yd, xd]
            out |= shifted
        out &= ~m
        ys, xs = np.nonzero(out)
        for y, x in zip(ys, xs):
            self.a[y, x] = c
        return self

    def inner_outline(self, c: Color):
        """Recolors the outermost ring of opaque pixels."""
        m = self.mask()
        h, w = m.shape
        edge = np.zeros_like(m)
        for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
            nb = np.zeros_like(m)
            ys = slice(max(dy, 0), h + min(dy, 0))
            yd = slice(max(-dy, 0), h + min(-dy, 0))
            xs = slice(max(dx, 0), w + min(dx, 0))
            xd = slice(max(-dx, 0), w + min(-dx, 0))
            nb[ys, xs] = m[yd, xd]
            edge |= m & ~nb
        ys, xs = np.nonzero(edge)
        for y, x in zip(ys, xs):
            self.a[y, x] = c
        return self

    def recolor(self, src: Color, dst: Color):
        m = np.all(self.a == np.array(src, np.uint8), axis=2)
        self.a[m] = dst
        return self

    def tint(self, c: Color, t: float):
        m = self.mask()
        rgb = self.a[:, :, :3].astype(np.float32)
        rgb = rgb + (np.array(c[:3], np.float32) - rgb) * t
        self.a[:, :, :3][m] = rgb[m].astype(np.uint8)
        return self

    def silhouette(self, c: Color) -> "Canvas":
        s = self.copy()
        m = s.mask()
        s.a[m] = c
        return s

    def to_image(self) -> Image.Image:
        return Image.fromarray(self.a, "RGBA")

    def save(self, path: str):
        self.to_image().save(path)


# ----------------------------------------------------------------------------
# Shaded primitives
# ----------------------------------------------------------------------------

LIGHT = np.array([-0.55, -0.65, 0.52])
LIGHT = LIGHT / np.linalg.norm(LIGHT)


def shade_index(intensity: float, n: int, x: int, y: int, dither: float = 0.0, bias: float = 0.0) -> int:
    """Maps a light intensity (-1..1) to a ramp index, optionally dithered."""
    t = (intensity + 1.0) * 0.5 + bias
    t = max(0.0, min(0.999, t))
    f = t * n
    i = int(f)
    frac = f - i
    if dither > 0 and i < n - 1:
        # dither only in the transition band
        if frac > 1.0 - dither and bayer(x, y) < (frac - (1.0 - dither)) / dither:
            i += 1
    return max(0, min(n - 1, i))


def shaded_ellipse(cv: Canvas, cx, cy, rx, ry, rmp, dither=0.35, bias=0.0, light=LIGHT,
                   clip: Callable[[int, int], bool] | None = None, flat=0.0):
    """Draws an ellipsoid-shaded blob using the ramp (dark->light)."""
    n = len(rmp)
    for y in range(int(cy - ry - 1), int(cy + ry + 2)):
        for x in range(int(cx - rx - 1), int(cx + rx + 2)):
            dx = (x + 0.5 - cx) / max(rx, 0.01)
            dy = (y + 0.5 - cy) / max(ry, 0.01)
            d = dx * dx + dy * dy
            if d > 1.0:
                continue
            if clip is not None and not clip(x, y):
                continue
            nz = math.sqrt(max(0.0, 1.0 - d))
            nx, ny = dx * (1 - flat), dy * (1 - flat)
            inten = nx * light[0] + ny * light[1] + nz * light[2]
            cv.px(x, y, rmp[shade_index(inten, n, x, y, dither, bias)])


def shaded_poly(cv: Canvas, pts: Sequence[tuple], rmp, grad_dir=(0.0, -1.0), dither=0.3, bias=0.0):
    """Fills a polygon with a linear gradient along grad_dir using the ramp."""
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    x0, x1, y0, y1 = int(min(xs)), int(max(xs)) + 1, int(min(ys)), int(max(ys)) + 1
    gx, gy = grad_dir
    projs = [p[0] * gx + p[1] * gy for p in pts]
    pmin, pmax = min(projs), max(projs)
    n = len(rmp)
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if point_in_poly(x + 0.5, y + 0.5, pts):
                t = ((x + 0.5) * gx + (y + 0.5) * gy - pmin) / max(pmax - pmin, 0.001)
                cv.px(x, y, rmp[shade_index(t * 2 - 1, n, x, y, dither, bias)])


def point_in_poly(x, y, pts) -> bool:
    inside = False
    j = len(pts) - 1
    for i in range(len(pts)):
        xi, yi = pts[i]
        xj, yj = pts[j]
        if ((yi > y) != (yj > y)) and (x < (xj - xi) * (y - yi) / (yj - yi + 1e-9) + xi):
            inside = not inside
        j = i
    return inside


# ----------------------------------------------------------------------------
# ASCII sprites
# ----------------------------------------------------------------------------


def from_ascii(rows: Sequence[str], cmap: dict, w: int | None = None) -> Canvas:
    rows = [r for r in rows]
    h = len(rows)
    w = w or max(len(r) for r in rows)
    cv = Canvas(w, h)
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch in (".", " "):
                continue
            c = cmap.get(ch)
            if c is None:
                raise KeyError(f"no color for '{ch}' in row {y}: {r}")
            cv.px(x, y, c)
    return cv


# ----------------------------------------------------------------------------
# Noise
# ----------------------------------------------------------------------------


class TileNoise:
    """Value noise that tiles with a given period (in pixels)."""

    def __init__(self, period: int, cells: int, seed: int):
        rnd = random.Random(seed)
        self.period = period
        self.cells = cells
        self.g = [[rnd.random() for _ in range(cells)] for _ in range(cells)]

    def __call__(self, x: float, y: float) -> float:
        fx = (x / self.period) * self.cells
        fy = (y / self.period) * self.cells
        ix, iy = int(math.floor(fx)), int(math.floor(fy))
        tx, ty = fx - ix, fy - iy
        tx = tx * tx * (3 - 2 * tx)
        ty = ty * ty * (3 - 2 * ty)
        c = self.cells
        a = self.g[iy % c][ix % c]
        b = self.g[iy % c][(ix + 1) % c]
        d = self.g[(iy + 1) % c][ix % c]
        e = self.g[(iy + 1) % c][(ix + 1) % c]
        return (a * (1 - tx) + b * tx) * (1 - ty) + (d * (1 - tx) + e * tx) * ty


# ----------------------------------------------------------------------------
# Sheets
# ----------------------------------------------------------------------------


def pack_row(frames: Sequence[Canvas]) -> Canvas:
    w = sum(f.w for f in frames)
    h = max(f.h for f in frames)
    out = Canvas(w, h)
    x = 0
    for f in frames:
        out.blit(f, x, 0)
        x += f.w
    return out


def pack_grid(frames: Sequence[Canvas], cols: int) -> Canvas:
    fw = max(f.w for f in frames)
    fh = max(f.h for f in frames)
    rows = (len(frames) + cols - 1) // cols
    out = Canvas(fw * cols, fh * rows)
    for i, f in enumerate(frames):
        out.blit(f, (i % cols) * fw, (i // cols) * fh)
    return out


def preview(img: Image.Image, scale: int, path: str, bg=(60, 70, 60, 255)):
    big = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
    base = Image.new("RGBA", big.size, bg)
    base.alpha_composite(big)
    base.save(path)


def soft_disc(size: int, falloff: float = 2.0, color=(255, 255, 255)) -> Image.Image:
    """Smooth radial gradient (for glows); returned as PIL image."""
    a = np.zeros((size, size, 4), np.uint8)
    c = (size - 1) / 2.0
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - c, y - c) / (size / 2.0)
            v = max(0.0, 1.0 - d)
            v = v ** falloff
            a[y, x] = (*color, int(255 * v))
    return Image.fromarray(a, "RGBA")


def pack_shelf(items, width: int = 256, pad: int = 1):
    """Packs [(name, Canvas, pivot)] into rows. Returns (sheet, [(name, x, y, w, h, px, py)])
    where (px, py) is the pivot measured from the rect's top-left."""
    items = sorted(items, key=lambda it: -it[1].h)
    x = y = 0
    row_h = 0
    placed = []
    for name, cv, piv in items:
        if x + cv.w > width:
            x = 0
            y += row_h + pad
            row_h = 0
        placed.append((name, cv, piv, x, y))
        x += cv.w + pad
        row_h = max(row_h, cv.h)
    total_h = y + row_h
    h = 1
    while h < total_h:
        h *= 2
    sheet = Canvas(width, h)
    rects = []
    for name, cv, piv, px, py in placed:
        sheet.blit(cv, px, py)
        rects.append((name, px, py, cv.w, cv.h, piv[0], piv[1]))
    return sheet, rects

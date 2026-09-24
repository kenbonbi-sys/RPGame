"""
VFX textures.
  smooth/*  : soft gradients (bilinear filtering in Unity) for glows, rings, smoke
  pixel/*   : crisp pixel textures & flipbooks (point filtering)
Grayscale textures are meant to be tinted by particle/sprite colors.
"""
import math
import random

import numpy as np
from PIL import Image, ImageFilter

from pixelkit import Canvas, P, OUTLINE, hx, mix, shaded_ellipse, point_in_poly, shade_index


def hash01(x, y, s=0):
    h = (x * 374761393 + y * 668265263 + s * 2147483647) & 0xFFFFFFFF
    h = (h ^ (h >> 13)) * 1274126177 & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0


def np_img(a):
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")


def radial(size, fn):
    a = np.zeros((size, size, 4), np.float32)
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - c, y - c) / (size / 2)
            v = fn(d, x, y)
            a[y, x] = (255, 255, 255, 255 * max(0.0, min(1.0, v)))
    return np_img(a)


# ============================================================================
# Smooth
# ============================================================================

def glow():
    return radial(64, lambda d, x, y: max(0, 1 - d) ** 2.2)


def glow_hard():
    return radial(64, lambda d, x, y: max(0, 1 - d) ** 1.2 * 0.6 + (1 if d < 0.18 else max(0, 1 - (d - 0.18) * 6)) * 0.4)


def ring(width=0.08, radius=0.86):
    def f(d, x, y):
        return math.exp(-((d - radius) / width) ** 2)
    return radial(128, f)


def ring_thick():
    def f(d, x, y):
        v = math.exp(-((d - 0.78) / 0.16) ** 2)
        return v
    return radial(128, f)


def telegraph_fill():
    """Filled disc with a brighter rim; used for AoE warnings."""
    def f(d, x, y):
        if d > 1:
            return 0
        edge = 1 - min(1, (1 - d) / 0.03)
        return 0.35 + 0.25 * d ** 3 + edge * 0.4
    return radial(128, f)


def telegraph_ring():
    def f(d, x, y):
        if d > 1:
            return 0
        return 1.0 if d > 0.93 else 0.0
    img = radial(128, f)
    return img.filter(ImageFilter.GaussianBlur(0.6))


def cone(angle_deg=90, size=128):
    W, H = size, size // 2
    a = np.zeros((H, W, 4), np.float32)
    ax, ay = W / 2, H  # apex at the bottom-centre, cone points up
    half = math.radians(angle_deg / 2)
    for y in range(H):
        for x in range(W):
            dx, dy = x + 0.5 - ax, ay - (y + 0.5)
            r = math.hypot(dx, dy) / H
            ang = math.atan2(dx, dy)
            if r > 1 or abs(ang) > half:
                continue
            edge = max(1 - min(1, (1 - r) / 0.04), 1 - min(1, (half - abs(ang)) / 0.05))
            v = 0.35 + 0.2 * r ** 2 + edge * 0.45
            a[y, x] = (255, 255, 255, 255 * v)
    return np_img(a)


def rect_telegraph():
    W, H = 32, 32
    a = np.zeros((H, W, 4), np.float32)
    for y in range(H):
        for x in range(W):
            e = min(x, y, W - 1 - x, H - 1 - y)
            v = 0.85 if e < 2 else 0.4
            a[y, x] = (255, 255, 255, 255 * v)
    return np_img(a)


def streak():
    W, H = 64, 16
    a = np.zeros((H, W, 4), np.float32)
    for y in range(H):
        for x in range(W):
            u = (x + 0.5) / W
            v = abs((y + 0.5) / H - 0.5) * 2
            val = (math.sin(u * math.pi) ** 0.6) * max(0, 1 - v) ** 2
            a[y, x] = (255, 255, 255, 255 * val)
    return np_img(a)


def smoke():
    rnd = random.Random(5)
    S = 64
    a = np.zeros((S, S, 4), np.float32)
    blobs = [(rnd.uniform(20, 44), rnd.uniform(20, 44), rnd.uniform(9, 16)) for _ in range(9)]
    for y in range(S):
        for x in range(S):
            v = 0
            for (bx, by, r) in blobs:
                d = math.hypot(x - bx, y - by) / r
                v += max(0, 1 - d) ** 1.5
            d0 = math.hypot(x - 31.5, y - 31.5) / 32
            v = min(1, v * 0.8) * max(0, 1 - d0) ** 0.7
            a[y, x] = (255, 255, 255, 255 * v)
    return np_img(a)


def beam():
    W, H = 16, 64
    a = np.zeros((H, W, 4), np.float32)
    for y in range(H):
        for x in range(W):
            u = abs((x + 0.5) / W - 0.5) * 2
            t = (y + 0.5) / H  # 0 top .. 1 bottom
            val = max(0, 1 - u) ** 1.5 * (t ** 1.4)
            a[y, x] = (255, 255, 255, 255 * val)
    return np_img(a)


def shield_bubble():
    S = 64
    a = np.zeros((S, S, 4), np.float32)
    c = (S - 1) / 2
    for y in range(S):
        for x in range(S):
            d = math.hypot(x - c, y - c) / (S / 2)
            if d > 1:
                continue
            rim = d ** 6
            hl = 0
            d2 = math.hypot(x - (c - 10), y - (c - 12)) / (S / 2)
            if d2 < 0.28:
                hl = (1 - d2 / 0.28) ** 2 * 0.8
            v = min(1, 0.12 + rim * 0.95 + hl)
            a[y, x] = (255, 255, 255, 255 * v)
    return np_img(a)


def shadow():
    W, H = 32, 12
    a = np.zeros((H, W, 4), np.float32)
    for y in range(H):
        for x in range(W):
            dx = (x + 0.5 - W / 2) / (W / 2)
            dy = (y + 0.5 - H / 2) / (H / 2)
            d = math.hypot(dx, dy)
            if d <= 1:
                v = 0.55 * min(1, (1 - d) * 3)
                a[y, x] = (10, 8, 16, 255 * v)
    return np_img(a)


# ============================================================================
# Pixel
# ============================================================================

def px_square():
    cv = Canvas(4, 4)
    cv.rect(0, 0, 4, 4, (255, 255, 255, 255))
    return cv


def spark4():
    cv = Canvas(9, 9)
    W = (255, 255, 255, 255)
    G = (200, 200, 200, 255)
    for k in range(-4, 5):
        c = W if abs(k) < 2 else G
        cv.px(4 + k, 4, c)
        cv.px(4, 4 + k, c)
    cv.px(3, 3, G); cv.px(5, 3, G); cv.px(3, 5, G); cv.px(5, 5, G)
    return cv


def plus():
    cv = Canvas(7, 7)
    for k in range(7):
        cv.px(3, k, (255, 255, 255, 255))
        cv.px(k, 3, (255, 255, 255, 255))
    cv.px(2, 3, (255, 255, 255, 255))
    for k in range(2, 5):
        cv.px(k, 2, (255, 255, 255, 255)); cv.px(k, 4, (255, 255, 255, 255))
    return cv


def leaf(tone):
    cv = Canvas(6, 5)
    rows = ["..ab..", ".abbc.", "abbbc.", ".bcc..", "..c..."]
    cmap = {"a": tone[4], "b": tone[3], "c": tone[1]}
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch in cmap:
                cv.px(x, y, cmap[ch])
    return cv


def slash_frames(n=5, S=48):
    """Crescent slash sweeping ~150 degrees. Grayscale."""
    frames = []
    for i in range(n):
        cv = Canvas(S, S)
        t = (i + 1) / n
        start = -80
        end = start + 170 * min(1, t * 1.25)
        fade = max(0, (t - 0.55) / 0.45)
        cx, cy = S / 2, S / 2
        R = S * 0.42
        for a10 in range(int(start * 10), int(end * 10), 8):
            a = math.radians(a10 / 10)
            p = (a10 / 10 - start) / max(1, end - start)  # 0 tail .. 1 head
            thick = 8.5 * math.sin(p * math.pi) ** 0.8 * (1 - fade * 0.7) + 0.8
            for k in range(int(thick) + 1):
                r = R - k
                x, y = cx + math.cos(a) * r, cy + math.sin(a) * r
                if k < thick * 0.35:
                    col = (255, 255, 255, 255)
                elif k < thick * 0.7:
                    col = (205, 205, 205, 235)
                else:
                    col = (150, 150, 150, 170)
                if fade > 0 and p < fade:
                    continue
                cv.px(int(x), int(y), col)
        frames.append(cv)
    return frames


def claw_frames(n=4, S=48):
    frames = []
    for i in range(n):
        cv = Canvas(S, S)
        t = (i + 1) / n
        for j in range(3):
            ox = (j - 1) * 9
            for k in range(0, int(34 * min(1, t * 1.4))):
                p = k / 34
                x = S / 2 + ox + math.sin(p * 2.2) * 7 - 3
                y = 7 + k
                w = 2.6 * math.sin(p * math.pi) + 0.5
                if t > 0.75 and p < (t - 0.75) * 4:
                    continue
                for dx in range(-int(w), int(w) + 1):
                    c = (255, 255, 255, 255) if abs(dx) < w * 0.5 else (190, 190, 190, 220)
                    cv.px(int(x + dx), int(y), c)
        frames.append(cv)
    return frames


def fire_frames(n=8, S=16):
    f = P["fire"]
    frames = []
    for i in range(n):
        cv = Canvas(S, S)
        t = i / (n - 1)
        r = 6.5 * (1 - t * 0.55)
        cy = S / 2 + 1 - t * 3
        for y in range(S):
            for x in range(S):
                dx = (x + 0.5 - S / 2) / r
                dy = (y + 0.5 - cy) / (r * 1.25)
                d = math.hypot(dx, dy)
                if d > 1:
                    continue
                heat = (1 - d) * (1 - t * 0.9) + (0.15 if dy > 0 else 0)
                if heat < 0.08 and (x + y + i) % 2:
                    continue
                if t > 0.7:
                    col = mix(f[1], (60, 50, 50, 255), (t - 0.7) / 0.3)
                else:
                    idx = int(min(1, heat * 1.5) * (len(f) - 1))
                    col = f[max(1, idx)]
                cv.px(x, y, col)
        frames.append(cv)
    return frames


def puff_frames(n=4, S=16, tone=None):
    frames = []
    rnd = random.Random(3)
    for i in range(n):
        cv = Canvas(S, S)
        t = i / (n - 1)
        blobs = [(-2, 0, 3.5), (2, -1, 3.2), (0, 2, 3.0), (-3, 2, 2.4), (3, 2, 2.4)]
        for (bx, by, br) in blobs:
            r = br * (0.7 + t * 0.7)
            cx, cy = S / 2 + bx * (1 + t * 0.6), S / 2 + by * (1 + t * 0.6) - t * 2
            for y in range(S):
                for x in range(S):
                    d = math.hypot(x + 0.5 - cx, y + 0.5 - cy) / r
                    if d <= 1:
                        if t > 0.4 and hash01(x, y, i) < (t - 0.4) * 0.9:
                            continue
                        v = 255 if d < 0.55 else 200
                        if tone:
                            cv.px(x, y, tone[2] if d < 0.55 else tone[1])
                        else:
                            cv.px(x, y, (v, v, v, 255))
        frames.append(cv)
    return frames


def fireball_frames(n=4, S=16):
    f = P["fire"]
    frames = []
    for i in range(n):
        cv = Canvas(S, S)
        for y in range(S):
            for x in range(S):
                a = math.atan2(y + 0.5 - S / 2, x + 0.5 - S / 2)
                wob = 1 + 0.12 * math.sin(a * 5 + i * 1.6)
                d = math.hypot(x + 0.5 - S / 2, y + 0.5 - S / 2) / (6.5 * wob)
                if d > 1:
                    continue
                idx = int((1 - d) * 6.5)
                cv.px(x, y, f[min(len(f) - 1, max(2, idx))])
        frames.append(cv)
    return frames


def ice_spike_frames(n=6):
    W, H = 24, 40
    ic = P["ice"]
    frames = []
    for i in range(n):
        cv = Canvas(W, H)
        grow = min(1, (i + 1) / 3)
        crack = max(0, i - 3)
        spikes = [(12, 34, 5), (6, 20, 3), (18, 24, 3)]
        for (cx, h, w) in spikes:
            hh = h * grow
            if hh < 1:
                continue
            base = H - 3
            pts = [(cx - w, base), (cx + w, base), (cx + 0.5, base - hh)]
            for y in range(H):
                for x in range(W):
                    if point_in_poly(x + 0.5, y + 0.5, pts):
                        if crack and (x * 7 + y * 3) % (6 - crack) == 0:
                            continue
                        c = ic[4] if x < cx else ic[2]
                        if x == int(cx) and y < base - 2:
                            c = ic[5]
                        cv.px(x, y, c)
        # frost on the ground
        for x in range(2, W - 2):
            if (x + i) % 3:
                cv.px(x, H - 3, ic[3])
                cv.px(x, H - 2, ic[1])
        cv.outline(hx("#0a1a2e"))
        frames.append(cv)
    return frames


def ice_shard():
    cv = Canvas(6, 6)
    ic = P["ice"]
    for (x, y, c) in [(2, 0, 5), (3, 1, 4), (2, 1, 5), (1, 2, 4), (2, 2, 4), (3, 2, 3), (2, 3, 3), (3, 3, 2), (2, 4, 2)]:
        cv.px(x, y, ic[c])
    return cv


def bolt_impact_frames(n=4, S=32):
    frames = []
    rnd = random.Random(9)
    for i in range(n):
        cv = Canvas(S, S)
        t = i / (n - 1)
        for k in range(7):
            a = k / 7 * math.tau + rnd.uniform(-0.3, 0.3)
            L = S * 0.45 * (0.6 + t * 0.4)
            x, y = S / 2, S / 2
            steps = 6
            for s in range(steps):
                nx = S / 2 + math.cos(a) * L * (s + 1) / steps + rnd.uniform(-2, 2)
                ny = S / 2 + math.sin(a) * L * (s + 1) / steps + rnd.uniform(-2, 2)
                if s / steps >= t * 0.6:
                    cv.line(x, y, nx, ny, (255, 255, 255, 255) if s < 3 else (200, 200, 255, 255))
                x, y = nx, ny
        if t < 0.5:
            cv.ellipse(S / 2, S / 2, 4 - t * 4, 4 - t * 4, (255, 255, 255, 255))
        frames.append(cv)
    return frames


def hit_frames(n=4, S=24):
    frames = []
    for i in range(n):
        cv = Canvas(S, S)
        t = i / (n - 1)
        c = S / 2
        for k in range(8):
            a = k / 8 * math.tau + (0.39 if k % 2 else 0)
            L0 = 2 + t * 6
            L1 = (9 if k % 2 == 0 else 6) + t * 3
            for s in np.linspace(L0, L1, 12):
                cv.px(int(c + math.cos(a) * s), int(c + math.sin(a) * s), (255, 255, 255, 255))
        if i == 0:
            cv.ellipse(c, c, 3, 3, (255, 255, 255, 255))
        frames.append(cv)
    return frames


def explosion_frames(n=7, S=48):
    f = P["fire"]
    frames = []
    rnd = random.Random(12)
    blobs = [(rnd.uniform(-8, 8), rnd.uniform(-8, 8), rnd.uniform(5, 10)) for _ in range(9)]
    for i in range(n):
        cv = Canvas(S, S)
        t = i / (n - 1)
        for (bx, by, br) in blobs:
            r = br * (0.4 + t * 0.9)
            cx, cy = S / 2 + bx * (0.5 + t), S / 2 + by * (0.5 + t) - t * 4
            for y in range(S):
                for x in range(S):
                    d = math.hypot(x + 0.5 - cx, y + 0.5 - cy) / r
                    if d > 1:
                        continue
                    if t > 0.55:
                        # smoke phase with holes
                        if hash01(x, y, i) < (t - 0.55) * 1.1:
                            continue
                        col = mix((90, 70, 70, 255), (40, 34, 40, 255), t) if d > 0.45 else mix(f[2], (70, 50, 50, 255), t)
                    else:
                        heat = (1 - d) * (1 - t)
                        idx = int(min(1, heat * 2.2) * (len(f) - 1))
                        col = f[max(2, idx)]
                    cv.px(x, y, col)
        frames.append(cv)
    return frames


def magic_circle(S=64):
    cv = Canvas(S, S)
    c = S / 2
    W = (255, 255, 255, 255)
    G = (190, 190, 190, 255)
    for r, col in [(30, W), (27, G), (18, W)]:
        for a in range(0, 3600, 7):
            rad = math.radians(a / 10)
            cv.px(int(c + math.cos(rad) * r), int(c + math.sin(rad) * r), col)
    # runes between rings
    rnd = random.Random(21)
    for k in range(12):
        a = k / 12 * math.tau
        rx, ry = c + math.cos(a) * 22.5, c + math.sin(a) * 22.5
        glyph = rnd.choice([[(0, -1), (0, 0), (0, 1), (1, -1)], [(-1, 0), (0, 0), (1, 0), (0, 1)],
                            [(-1, -1), (0, 0), (1, 1), (1, -1)], [(0, -1), (-1, 1), (1, 1), (0, 0)]])
        for (gx, gy) in glyph:
            cv.px(int(rx + gx), int(ry + gy), W)
    # hexagram
    pts = [(c + math.cos(k / 6 * math.tau - math.pi / 2) * 18, c + math.sin(k / 6 * math.tau - math.pi / 2) * 18) for k in range(6)]
    for k in range(6):
        a, b = pts[k], pts[(k + 2) % 6]
        cv.line(a[0], a[1], b[0], b[1], G)
    return cv


def crack_decal():
    W, H = 64, 40
    cv = Canvas(W, H)
    rnd = random.Random(33)
    D = hx("#1a120e")
    M = hx("#3a2a20")
    cx, cy = W / 2, H / 2
    for k in range(9):
        a = k / 9 * math.tau + rnd.uniform(-0.2, 0.2)
        x, y = cx, cy
        L = rnd.uniform(14, 28)
        steps = 7
        for s in range(steps):
            nx = x + math.cos(a) * L / steps + rnd.uniform(-1.5, 1.5)
            ny = y + math.sin(a) * L / steps * 0.62 + rnd.uniform(-1, 1)
            cv.line(x, y, nx, ny, D if s < 4 else M, 2 if s < 2 else 1)
            if rnd.random() < 0.25:
                b = a + rnd.choice([-0.8, 0.8])
                cv.line(nx, ny, nx + math.cos(b) * 5, ny + math.sin(b) * 3, M)
            x, y = nx, ny
    # central crater
    cv.ellipse(cx, cy, 7, 4, D)
    cv.ellipse(cx, cy - 1, 5, 2.5, hx("#241a14"))
    return cv


def debris(seed):
    cv = Canvas(5, 5)
    s = P["stone"]
    rnd = random.Random(seed)
    for y in range(5):
        for x in range(5):
            if abs(x - 2) + abs(y - 2) <= 2 + (rnd.random() < 0.3):
                cv.px(x, y, s[4] if y < 2 else s[2])
    return cv


def proj_arrow():
    """An arrow flying right: shaft, steel head, fletching."""
    cv = Canvas(16, 5)
    wood = hx("#a56f45")
    for x in range(3, 13):
        cv.px(x, 2, wood)
    for (x, y) in ((13, 1), (13, 2), (13, 3), (14, 2), (15, 2)):
        cv.px(x, y, hx("#d6dcee") if y == 2 else hx("#8e94ab"))
    for (x, y) in ((0, 0), (1, 1), (2, 1), (0, 4), (1, 3), (2, 3), (1, 2), (2, 2)):
        cv.px(x, y, hx("#e8e2d0") if y != 2 else hx("#b04a3a"))
    return cv


def proj_knife():
    """A thrown knife pointing right."""
    cv = Canvas(10, 4)
    for x in range(0, 3):
        cv.px(x, 1, hx("#48291c"))
        cv.px(x, 2, hx("#643c26"))
    cv.px(3, 0, hx("#e8b634"))
    cv.px(3, 3, hx("#b8821e"))
    for x in range(3, 10):
        cv.px(x, 1, hx("#ffffff") if x < 9 else hx("#d6dcee"))
        cv.px(x, 2, hx("#a4acc6") if x < 9 else None)
    return cv


def proj_axe():
    """A throwing axe (it spins in flight)."""
    cv = Canvas(12, 12)
    for k in range(0, 10):
        cv.px(2 + k, 10 - k, hx("#855433"))
    for (x, y) in ((7, 1), (8, 1), (9, 1), (6, 2), (7, 2), (8, 2), (9, 2), (10, 2), (7, 3), (8, 3), (9, 3), (10, 3), (11, 3), (8, 4), (9, 4), (10, 4), (11, 4)):
        cv.px(x, y, hx("#d6dcee") if x + y > 12 else hx("#a4acc6"))
    cv.outline(hx("#1c1420"))
    return cv


def proj_note():
    """A musical note (a bard's attack)."""
    cv = Canvas(8, 10)
    for y in range(0, 7):
        cv.px(5, y, hx("#ffffff"))
    cv.px(6, 0, hx("#ffffff"))
    cv.px(7, 1, hx("#ffffff"))
    cv.px(6, 1, hx("#ffffff"))
    for (x, y) in ((2, 6), (3, 6), (4, 6), (1, 7), (2, 7), (3, 7), (4, 7), (5, 7), (2, 8), (3, 8), (4, 8)):
        cv.px(x, y, hx("#ffffff"))
    return cv


def build():
    smooth = {
        "glow": glow(), "glow_hard": glow_hard(), "ring": ring(), "ring_thick": ring_thick(),
        "tele_fill": telegraph_fill(), "tele_ring": telegraph_ring(), "tele_cone": cone(90),
        "tele_rect": rect_telegraph(), "streak": streak(), "smoke": smoke(), "beam": beam(),
        "bubble": shield_bubble(), "shadow": shadow(),
    }
    pixel_single = [
        ("px_square", px_square()), ("spark4", spark4()), ("plus", plus()),
        ("leaf_green", leaf(P["leaf"])), ("leaf_autumn", leaf(P["autumn"])),
        ("ice_shard", ice_shard()), ("magic_circle", magic_circle()), ("crack", crack_decal()),
        ("debris_0", debris(1)), ("debris_1", debris(2)),
        ("proj_arrow", proj_arrow()), ("proj_knife", proj_knife()), ("proj_axe", proj_axe()), ("proj_note", proj_note()),
    ]
    flipbooks = {
        "slash": slash_frames(), "claw": claw_frames(), "fire": fire_frames(),
        "smoke_px": puff_frames(), "dust": puff_frames(tone=[hx("#5a4838"), hx("#8a7358"), hx("#b09a7a")]),
        "fireball": fireball_frames(), "ice_spike": ice_spike_frames(), "bolt_hit": bolt_impact_frames(),
        "hit": hit_frames(), "explosion": explosion_frames(),
        "poison": puff_frames(tone=[hx("#2a4a1a"), hx("#5a8a2a"), hx("#9ad04a")]),
    }
    return smooth, pixel_single, flipbooks

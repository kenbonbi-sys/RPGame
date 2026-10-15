"""Signed-distance sculpting for the Godot creatures.

Shapes are built from primitives (balls, ellipsoids, round cones, boxes, tubes along a path) joined
with smooth unions, carved with smooth subtraction, and pushed in and out by noise (lumps, layered
fur clumps). A surface-nets mesher turns a shape into a smooth mesh: vertices are pulled onto the
surface, normals come from the field, and vertex colours get baked ambient occlusion (dark in
creases) and cavity (light on ridges), the hand-painted look. `write_glb` saves meshes for Godot.

Every node has d(p) -> distances and dc(p) -> (distances, colours) for points p of shape (N, 3).
Colours are sRGB triples in 0..1.
"""
import json
import math
import struct

import numpy as np

F = np.float64


def vec(*a):
    return np.array(a, dtype=F)


def normalize(v):
    v = np.asarray(v, dtype=F)
    return v / np.linalg.norm(v)


def axis_angle(axis, angle):
    x, y, z = normalize(axis)
    c, s = math.cos(angle), math.sin(angle)
    t = 1 - c
    return np.array([[t * x * x + c, t * x * y - s * z, t * x * z + s * y],
                     [t * x * y + s * z, t * y * y + c, t * y * z - s * x],
                     [t * x * z - s * y, t * y * z + s * x, t * z * z + c]], dtype=F)


def euler(x=0.0, y=0.0, z=0.0):
    """Godot's Basis.from_euler order (Y, then X, then Z)."""
    return axis_angle((0, 1, 0), y) @ axis_angle((1, 0, 0), x) @ axis_angle((0, 0, 1), z)


def basis_y(up, hint=(0, 0, 1)):
    """A rotation whose local +Y points along `up`."""
    y = normalize(up)
    h = np.asarray(hint, dtype=F)
    if abs(np.dot(h, y)) > 0.95:
        h = np.array([1.0, 0, 0]) if abs(y[0]) < 0.9 else np.array([0, 0, 1.0])
    x = normalize(np.cross(y, h))
    z = np.cross(x, y)
    return np.stack([x, y, z], axis=1)


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def mix(a, b, t):
    return a + (b - a) * t


# ------------------------------------------------------------------ noise
def _hash(ix, iy, iz, seed):
    h = (ix.astype(np.int64) * 73856093) ^ (iy.astype(np.int64) * 19349663) ^ (iz.astype(np.int64) * 83492791) ^ (seed * 2654435761)
    h = h.astype(np.uint64)
    h ^= h >> np.uint64(13)
    h *= np.uint64(0x5bd1e995)
    h ^= h >> np.uint64(15)
    h *= np.uint64(0x27d4eb2d)
    h ^= h >> np.uint64(16)
    return (h & np.uint64(0xFFFFFF)).astype(F) / float(0x1000000)


def vnoise(p, seed=0):
    """Smooth value noise in 0..1."""
    pi = np.floor(p)
    f = p - pi
    w = f * f * (3 - 2 * f)
    i, j, k = pi[:, 0], pi[:, 1], pi[:, 2]
    out = 0.0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                wx = w[:, 0] if dx else 1 - w[:, 0]
                wy = w[:, 1] if dy else 1 - w[:, 1]
                wz = w[:, 2] if dz else 1 - w[:, 2]
                out = out + wx * wy * wz * _hash(i + dx, j + dy, k + dz, seed)
    return out


def fbm(p, octaves=3, seed=0, gain=0.5):
    out = 0.0
    amp = 1.0
    total = 0.0
    q = p
    for o in range(octaves):
        out = out + amp * vnoise(q, seed + o * 17)
        total += amp
        amp *= gain
        q = q * 2.03 + 3.1
    return out / total


def clumps(p, size, flow, stretch=3.0, seed=0, jitter=0.8):
    """Layered fur locks: overlapping teardrops, long along `flow`, wide at the root, tapering
    to a point, highest just before the tip where each lock ends in a lip over the next one,
    with a couple of strand grooves along it. Returns a height 0..1 per point (the top lock
    wins, so they overlap like shingles)."""
    flow = normalize(flow)
    b = basis_y(flow)
    q = (p @ b) / size
    q[:, 1] /= stretch
    pi = np.floor(q)
    best = np.zeros(len(p))
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            for dz in (-1, 0, 1):
                c = pi + vec(dx, dy, dz)
                hsh = _hash(c[:, 0], c[:, 1], c[:, 2], seed)
                bits = (hsh * float(0x1000000)).astype(np.int64)
                j = np.stack([(bits & 0xFF), (bits >> 8) & 0xFF, (bits >> 16) & 0xFF], axis=1) / 255.0
                o = q - (c + 0.5 + (j - 0.5) * jitter)
                along = o[:, 1] / 1.1                     # -1 root .. +1 tip
                width = 0.78 * (1.0 - 0.75 * smoothstep(-0.6, 1.0, along))
                r = np.sqrt(o[:, 0] ** 2 + o[:, 2] ** 2) / np.maximum(width, 1e-3)
                body = np.clip(1.0 - r * r, 0.0, 1.0)
                rise = smoothstep(-1.1, 0.7, along) * (1.0 - smoothstep(0.72, 1.0, along))
                strands = 1.0 - 0.14 * np.abs(np.sin(r * 5.5))      # lines of equal r run along the lock
                v = np.sqrt(body) * rise * strands * (0.75 + 0.25 * j[:, 0])
                best = np.maximum(best, v)
    return best


# ------------------------------------------------------------------ nodes
class Node:
    def d(self, p):
        raise NotImplementedError

    def dc(self, p):
        raise NotImplementedError


def _color(color, p):
    if callable(color):
        return np.asarray(color(p), dtype=F).reshape(len(p), 3)
    return np.broadcast_to(np.asarray(color, dtype=F), (len(p), 3)).copy()


class Prim(Node):
    def __init__(self, color=(0.8, 0.8, 0.8)):
        self.color = color

    def dc(self, p):
        return self.d(p), _color(self.color, p)


class Sphere(Prim):
    def __init__(self, r, color=(0.8, 0.8, 0.8)):
        super().__init__(color)
        self.r = r

    def d(self, p):
        return np.linalg.norm(p, axis=1) - self.r


class Ellipsoid(Prim):
    def __init__(self, radii, color=(0.8, 0.8, 0.8)):
        super().__init__(color)
        self.radii = np.asarray(radii, dtype=F)

    def d(self, p):
        k0 = np.linalg.norm(p / self.radii, axis=1)
        k1 = np.linalg.norm(p / (self.radii * self.radii), axis=1)
        return k0 * (k0 - 1.0) / np.maximum(k1, 1e-9)


class RoundCone(Prim):
    """A capsule from a (radius ra) to b (radius rb)."""

    def __init__(self, a, b, ra, rb=None, color=(0.8, 0.8, 0.8)):
        super().__init__(color)
        self.a = np.asarray(a, dtype=F)
        self.b = np.asarray(b, dtype=F)
        self.ra = ra
        self.rb = ra if rb is None else rb

    def d(self, p):
        return _round_cone(p, self.a, self.b, self.ra, self.rb)


def _round_cone(p, a, b, r1, r2):
    ba = b - a
    l2 = float(ba @ ba)
    rr = r1 - r2
    a2 = l2 - rr * rr
    il2 = 1.0 / l2
    pa = p - a
    y = pa @ ba
    z = y - l2
    xv = pa * l2 - np.outer(y, ba)
    x2 = np.einsum("ij,ij->i", xv, xv)
    y2 = y * y * l2
    z2 = z * z * l2
    k = math.copysign(1.0, rr) * rr * rr * x2 if rr != 0 else np.zeros_like(x2)
    d_end = np.sqrt(x2 + z2) * il2 - r2
    d_start = np.sqrt(x2 + y2) * il2 - r1
    d_mid = (np.sqrt(np.maximum(x2 * a2 * il2, 0.0)) + y * rr) * il2 - r1
    return np.where(np.sign(z) * a2 * z2 > k, d_end, np.where(np.sign(y) * a2 * y2 < k, d_start, d_mid))


class Box(Prim):
    def __init__(self, size, rounding=0.0, color=(0.8, 0.8, 0.8)):
        super().__init__(color)
        self.half = np.asarray(size, dtype=F) * 0.5
        self.r = rounding

    def d(self, p):
        q = np.abs(p) - self.half + self.r
        out = np.linalg.norm(np.maximum(q, 0.0), axis=1)
        return out + np.minimum(q.max(axis=1), 0.0) - self.r


class Torus(Prim):
    """A ring in the XZ plane."""

    def __init__(self, big, small, color=(0.8, 0.8, 0.8)):
        super().__init__(color)
        self.big = big
        self.small = small

    def d(self, p):
        q = np.stack([np.hypot(p[:, 0], p[:, 2]) - self.big, p[:, 1]], axis=1)
        return np.linalg.norm(q, axis=1) - self.small


class Plane(Prim):
    """Everything below the plane (opposite `normal`, `offset` along it) is inside."""

    def __init__(self, normal, offset=0.0, color=(0.8, 0.8, 0.8)):
        super().__init__(color)
        self.n = normalize(normal)
        self.o = offset

    def d(self, p):
        return p @ self.n - self.o


class Tube(Node):
    """Round cones through a list of points with a radius at each. `color` may be a function of
    t, 0 at the first point and 1 at the last."""

    def __init__(self, points, radii, color=(0.8, 0.8, 0.8)):
        self.pts = [np.asarray(q, dtype=F) for q in points]
        self.radii = list(radii)
        self.color = color

    def _seg(self, p):
        ds = []
        for i in range(len(self.pts) - 1):
            ds.append(_round_cone(p, self.pts[i], self.pts[i + 1], self.radii[i], self.radii[i + 1]))
        return np.stack(ds, axis=1)

    def d(self, p):
        return self._seg(p).min(axis=1)

    def dc(self, p):
        ds = self._seg(p)
        idx = ds.argmin(axis=1)
        d = ds[np.arange(len(p)), idx]
        if not callable(self.color):
            return d, _color(self.color, p)
        a = np.stack(self.pts[:-1])[idx]
        b = np.stack(self.pts[1:])[idx]
        ab = b - a
        u = np.clip(np.einsum("ij,ij->i", p - a, ab) / np.einsum("ij,ij->i", ab, ab), 0, 1)
        t = (idx + u) / (len(self.pts) - 1)
        return d, np.asarray(self.color(t), dtype=F).reshape(len(p), 3)


def bezier(p0, p1, p2, n=8):
    """Points along a quadratic curve, for bent tubes (horns, claws, antlers)."""
    p0, p1, p2 = (np.asarray(q, dtype=F) for q in (p0, p1, p2))
    return [(1 - t) ** 2 * p0 + 2 * (1 - t) * t * p1 + t * t * p2 for t in np.linspace(0, 1, n)]


def taper(r0, r1, n, power=1.0):
    return [r0 + (r1 - r0) * (i / (n - 1)) ** power for i in range(n)]


class Xf(Node):
    """Moves, turns and uniformly scales a child: the child is built in its own frame."""

    def __init__(self, child, pos=(0, 0, 0), basis=None, scale=1.0):
        self.child = child
        self.pos = np.asarray(pos, dtype=F)
        self.basis = np.eye(3) if basis is None else np.asarray(basis, dtype=F)
        self.s = scale

    def _local(self, p):
        return ((p - self.pos) @ self.basis) / self.s

    def d(self, p):
        return self.child.d(self._local(p)) * self.s

    def dc(self, p):
        d, c = self.child.dc(self._local(p))
        return d * self.s, c


class MirrorX(Node):
    """The child on both sides: build it at +x."""

    def __init__(self, child):
        self.child = child

    def _m(self, p):
        q = p.copy()
        q[:, 0] = np.abs(q[:, 0])
        return q

    def d(self, p):
        return self.child.d(self._m(p))

    def dc(self, p):
        return self.child.dc(self._m(p))


def _smin(a, b, k):
    if k <= 0:
        h = (a < b).astype(F)
        return np.minimum(a, b), h
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return mix(b, a, h) - k * h * (1 - h), h


class Union(Node):
    """Smooth union (k = blend radius; 0 is a hard union). Colours blend across the seam."""

    def __init__(self, *children, k=0.0):
        self.children = [c for c in children if c is not None]
        self.k = k

    def d(self, p):
        d = self.children[0].d(p)
        for c in self.children[1:]:
            d, _ = _smin(d, c.d(p), self.k)
        return d

    def dc(self, p):
        d, col = self.children[0].dc(p)
        for c in self.children[1:]:
            d2, c2 = c.dc(p)
            d, h = _smin(d, d2, self.k)
            col = mix(c2, col, h[:, None])
        return d, col


class Sub(Node):
    """Carves `b` out of `a`; the cut is coloured `inner` if given."""

    def __init__(self, a, b, k=0.0, inner=None):
        self.a, self.b, self.k, self.inner = a, b, k, inner

    def d(self, p):
        d, _ = _smin(-self.a.d(p), self.b.d(p), self.k)
        return -d

    def dc(self, p):
        da, ca = self.a.dc(p)
        db = self.b.d(p)
        d, h = _smin(-da, db, self.k)
        if self.inner is not None:
            ca = mix(_color(self.inner, p), ca, h[:, None])
        return -d, ca


class Intersect(Node):
    def __init__(self, a, b, k=0.0):
        self.a, self.b, self.k = a, b, k

    def d(self, p):
        d, _ = _smin(-self.a.d(p), -self.b.d(p), self.k)
        return -d

    def dc(self, p):
        da, ca = self.a.dc(p)
        d, _ = _smin(-da, -self.b.d(p), self.k)
        return -d, ca


class Displace(Node):
    """Adds fn(p) to the distance near the surface (negative pushes out). `band` must cover the
    largest push; far points keep the child's distance, which is all the mesher needs there."""

    def __init__(self, child, fn, band, paint=None):
        self.child, self.fn, self.band, self.paint = child, fn, band, paint

    def d(self, p):
        d = self.child.d(p)
        near = np.abs(d) < self.band
        if near.any():
            d = d.copy()
            d[near] += self.fn(p[near])
        return d

    def dc(self, p):
        d, c = self.child.dc(p)
        near = np.abs(d) < self.band
        if near.any():
            d = d.copy()
            d[near] += self.fn(p[near])
        if self.paint is not None:
            c = self.paint(p, c)
        return d, c


class Paint(Node):
    """Changes the colours of a child: fn(p, colours) -> colours."""

    def __init__(self, child, fn):
        self.child, self.fn = child, fn

    def d(self, p):
        return self.child.d(p)

    def dc(self, p):
        d, c = self.child.dc(p)
        return d, self.fn(p, c)


# ------------------------------------------------------------------ meshing
def _eval_grid(node, lo, n, h, chunk=1 << 19):
    nx, ny, nz = n
    out = np.empty((nx, ny, nz), dtype=F)
    ys = lo[1] + np.arange(ny) * h
    zs = lo[2] + np.arange(nz) * h
    yy, zz = np.meshgrid(ys, zs, indexing="ij")
    plane = np.stack([np.zeros(yy.size), yy.ravel(), zz.ravel()], axis=1)
    step = max(1, chunk // plane.shape[0])
    for i0 in range(0, nx, step):
        i1 = min(nx, i0 + step)
        pts = np.concatenate([plane + vec(lo[0] + i * h, 0, 0) for i in range(i0, i1)])
        out[i0:i1] = node.d(pts).reshape(i1 - i0, ny, nz)
    return out


def grad(node, p, eps):
    e = np.eye(3) * eps
    g = np.stack([node.d(p + e[i]) - node.d(p - e[i]) for i in range(3)], axis=1)
    return g / (2 * eps)


def polygonize(node, lo, hi, h, project=3):
    """Surface nets: one vertex per cell the surface crosses, one quad per crossed grid edge."""
    lo = np.asarray(lo, dtype=F)
    hi = np.asarray(hi, dtype=F)
    n = tuple(int(math.ceil((hi[i] - lo[i]) / h)) + 1 for i in range(3))
    g = _eval_grid(node, lo, n, h)
    nx, ny, nz = n
    acc = np.zeros((nx - 1, ny - 1, nz - 1, 3), dtype=np.float32)
    cnt = np.zeros((nx - 1, ny - 1, nz - 1), dtype=np.float32)
    inside = g < 0
    edges = []
    for axis in range(3):
        sl_a = [slice(None)] * 3
        sl_b = [slice(None)] * 3
        sl_a[axis] = slice(0, -1)
        sl_b[axis] = slice(1, None)
        ga, gb = g[tuple(sl_a)], g[tuple(sl_b)]
        cross = inside[tuple(sl_a)] != inside[tuple(sl_b)]
        t = np.where(cross, ga / np.where(cross, ga - gb, 1.0), 0.0).astype(np.float32)
        shape = cross.shape
        idx = np.indices(shape, dtype=np.float32)
        pos = np.stack([idx[0], idx[1], idx[2]], axis=-1)
        pos[..., axis] += t
        pos *= cross[..., None]
        others = [a for a in range(3) if a != axis]
        for d1 in (0, 1):
            for d2 in (0, 1):
                src = [slice(None)] * 3
                dst = [slice(None)] * 3
                src[axis] = slice(None)
                dst[axis] = slice(None)
                for oa, dd in zip(others, (d1, d2)):
                    m = n[oa] - 1
                    src[oa] = slice(dd, dd + m)
                    dst[oa] = slice(0, m)
                acc[tuple(dst)] += pos[tuple(src)]
                cnt[tuple(dst)] += cross[tuple(src)]
        edges.append((axis, cross, inside[tuple(sl_a)]))
    active = cnt > 0
    vid = np.full(active.shape, -1, dtype=np.int64)
    nv = int(active.sum())
    vid[active] = np.arange(nv)
    verts = (acc[active] / cnt[active][:, None]).astype(F) * h + lo
    quads = []
    for axis, cross, inside_a in edges:
        others = [a for a in range(3) if a != axis]
        sel = [slice(None)] * 3
        for oa in others:
            sel[oa] = slice(1, n[oa] - 1)
        c = cross[tuple(sel)]
        ins = inside_a[tuple(sel)][c]
        ii = np.nonzero(c)
        ii = [ii[0].copy(), ii[1].copy(), ii[2].copy()]
        for oa in others:
            ii[oa] += 1
        u, v = others
        if axis == 1:
            u, v = 2, 0      # keep (u, v, axis) right handed

        def cell(du, dv):
            q = [ii[0].copy(), ii[1].copy(), ii[2].copy()]
            q[u] -= du
            q[v] -= dv
            return vid[q[0], q[1], q[2]]

        a, b, cc, d = cell(1, 1), cell(0, 1), cell(0, 0), cell(1, 0)
        quad = np.stack([a, b, cc, d], axis=1)
        flip = ~ins
        quad[flip] = quad[flip][:, ::-1]
        quads.append(quad)
    quads = np.concatenate(quads)
    quads = quads[(quads >= 0).all(axis=1)]
    # pull the vertices onto the surface
    for _ in range(project):
        dist = node.d(verts)
        gr = grad(node, verts, h * 0.25)
        gl2 = np.maximum(np.einsum("ij,ij->i", gr, gr), 1e-9)
        stepv = gr * (dist / gl2)[:, None]
        sl = np.linalg.norm(stepv, axis=1)
        stepv *= (np.minimum(sl, h * 0.75) / np.maximum(sl, 1e-12))[:, None]
        verts = verts - stepv
    # split each quad along its shorter diagonal
    pa, pb, pc, pd = (verts[quads[:, i]] for i in range(4))
    short_ac = np.linalg.norm(pa - pc, axis=1) <= np.linalg.norm(pb - pd, axis=1)
    t1 = np.where(short_ac[:, None], quads[:, [0, 1, 2]], quads[:, [0, 1, 3]])
    t2 = np.where(short_ac[:, None], quads[:, [0, 2, 3]], quads[:, [1, 2, 3]])
    tris = np.concatenate([t1, t2])
    # drop degenerate triangles
    tris = tris[(tris[:, 0] != tris[:, 1]) & (tris[:, 1] != tris[:, 2]) & (tris[:, 0] != tris[:, 2])]
    return verts, tris


def finish(node, verts, tris, h, ao=0.6, ao_dist=None, cavity=0.25, cavity_r=None, shade=None):
    """Normals from the field, colours with baked occlusion and cavity, outward winding."""
    normals = grad(node, verts, h * 0.3)
    normals /= np.maximum(np.linalg.norm(normals, axis=1), 1e-9)[:, None]
    _, col = node.dc(verts)
    if ao > 0:
        ao_dist = ao_dist or h * 8
        occ = np.zeros(len(verts))
        w = 1.0
        for i in range(1, 6):
            r = ao_dist * i / 5.0
            dd = node.d(verts + normals * r)
            occ += w * np.clip(r - dd, 0, None) / ao_dist
            w *= 0.6
        a = np.clip(1.0 - occ * 1.6, 0.0, 1.0)
        col = col * (1.0 - ao + ao * a)[:, None]
    if cavity > 0:
        r = cavity_r or h * 2.5
        lap = -6.0 * node.d(verts)
        e = np.eye(3) * r
        for i in range(3):
            lap = lap + node.d(verts + e[i]) + node.d(verts - e[i])
        lap = lap / (r * r)
        k = np.clip(lap * r * 0.5, -1.0, 1.0)
        col = col * (1.0 + cavity * k)[:, None]
    if shade is not None:
        col = shade(verts, normals, col)
    col = np.clip(col, 0.0, 1.0)
    # outward winding (counter-clockwise seen from outside, as glTF wants)
    fa, fb, fc = verts[tris[:, 0]], verts[tris[:, 1]], verts[tris[:, 2]]
    fn = np.cross(fb - fa, fc - fa)
    nn = normals[tris[:, 0]] + normals[tris[:, 1]] + normals[tris[:, 2]]
    wrong = np.einsum("ij,ij->i", fn, nn) < 0
    tris = tris.copy()
    tris[wrong] = tris[wrong][:, [0, 2, 1]]
    return normals, col, tris


def build(name, node, lo, hi, h, **kw):
    project = kw.pop("project", 3)
    verts, tris = polygonize(node, lo, hi, h, project)
    normals, col, tris = finish(node, verts, tris, h, **kw)
    print("  %-14s %7d verts %7d tris" % (name, len(verts), len(tris)))
    return {"name": name, "verts": verts, "normals": normals, "colors": col, "tris": tris}


# ------------------------------------------------------------------ glTF
def write_glb(path, meshes):
    """One node per mesh (identity transform), positions/normals float, colours as bytes."""
    blob = bytearray()
    views, accessors, gl_meshes, nodes = [], [], [], []

    def add_view(data, target):
        while len(blob) % 4:
            blob.append(0)
        off = len(blob)
        blob.extend(data)
        views.append({"buffer": 0, "byteOffset": off, "byteLength": len(data), "target": target})
        return len(views) - 1

    for m in meshes:
        v = np.asarray(m["verts"], dtype=np.float32)
        nrm = np.asarray(m["normals"], dtype=np.float32)
        col = np.clip(np.round(np.asarray(m["colors"]) * 255), 0, 255).astype(np.uint8)
        col4 = np.concatenate([col, np.full((len(col), 1), 255, np.uint8)], axis=1)
        idx = np.asarray(m["tris"], dtype=np.uint32).ravel()
        bv = add_view(v.tobytes(), 34962)
        accessors.append({"bufferView": bv, "componentType": 5126, "count": len(v), "type": "VEC3",
                          "min": v.min(axis=0).tolist(), "max": v.max(axis=0).tolist()})
        a_pos = len(accessors) - 1
        bv = add_view(nrm.tobytes(), 34962)
        accessors.append({"bufferView": bv, "componentType": 5126, "count": len(nrm), "type": "VEC3"})
        a_nrm = len(accessors) - 1
        bv = add_view(col4.tobytes(), 34962)
        accessors.append({"bufferView": bv, "componentType": 5121, "normalized": True, "count": len(col4), "type": "VEC4"})
        a_col = len(accessors) - 1
        bv = add_view(idx.tobytes(), 34963)
        accessors.append({"bufferView": bv, "componentType": 5125, "count": len(idx), "type": "SCALAR"})
        a_idx = len(accessors) - 1
        gl_meshes.append({"name": m["name"], "primitives": [{"attributes": {"POSITION": a_pos, "NORMAL": a_nrm, "COLOR_0": a_col},
                                                              "indices": a_idx, "material": 0}]})
        nodes.append({"name": m["name"], "mesh": len(gl_meshes) - 1})
    while len(blob) % 4:
        blob.append(0)
    doc = {
        "asset": {"version": "2.0", "generator": "Tools/Sculpt"},
        "scene": 0,
        "scenes": [{"nodes": list(range(len(nodes)))}],
        "nodes": nodes,
        "meshes": gl_meshes,
        "materials": [{"name": "VertexColour", "pbrMetallicRoughness": {"baseColorFactor": [1, 1, 1, 1], "metallicFactor": 0.0,
                                                                         "roughnessFactor": 0.8}}],
        "accessors": accessors,
        "bufferViews": views,
        "buffers": [{"byteLength": len(blob)}],
    }
    js = json.dumps(doc, separators=(",", ":")).encode()
    while len(js) % 4:
        js += b" "
    with open(path, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(blob)))
        f.write(struct.pack("<II", len(js), 0x4E4F534A))
        f.write(js)
        f.write(struct.pack("<II", len(blob), 0x004E4942))
        f.write(bytes(blob))


def hit(node, origin, direction, far=20.0):
    """Where a ray first meets the surface (sphere tracing), for placing features on a body."""
    o = np.asarray(origin, dtype=F)
    dv = normalize(direction)
    t = 0.0
    for _ in range(400):
        p = o + dv * t
        d = float(node.d(p[None, :])[0])
        if d < 1e-4:
            return p
        t += d * 0.7
        if t > far:
            break
    return o + dv * t


class Offset(Node):
    """The child grown by r (shrunk when r < 0)."""

    def __init__(self, child, r):
        self.child, self.r = child, r

    def d(self, p):
        return self.child.d(p) - self.r

    def dc(self, p):
        d, c = self.child.dc(p)
        return d - self.r, c


class Field(Node):
    """A shape from a plain function of position (regions to cut with)."""

    def __init__(self, fn, color=(1.0, 1.0, 1.0)):
        self.fn, self.color = fn, color

    def d(self, p):
        return self.fn(p)

    def dc(self, p):
        return self.fn(p), _color(self.color, p)


def shell(base, outer, inner):
    """A skin between `inner` and `outer` above the surface of `base` (armour that hugs a body)."""
    return Sub(Offset(base, outer), Offset(base, inner))

"""The ground of Hang Gấu, the first colossus's den: a forest clearing where the fight happens, the
land rising into wooded hills around it. Written as a glTF mesh (imported by import_assets.py):

    python Tools/Unreal/make_terrain.py

Writes Unreal/SourceArt/Terrain/HangGau_Ground.glb. In Unreal space (cm): the hero starts at
(0, 0), the colossus stands at (3500, 0) and keeps within 38 m of it; the clearing is a circle of
CLEARING cm around CENTRE, nearly flat; beyond it the ground rolls up into hills to the map's edge.
Vertex colour R marks bare earth (worn paths across the clearing, patches), G the hills' rock
(steep ground); the ground material paints by them. Heights are smooth noise, made with a fixed
seed so the map is the same every time.
"""
import json
import os
import struct

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Unreal", "SourceArt", "Terrain")
SIZE = 50000.0          # cm across
CELLS = 256
CENTRE = np.array([1750.0, 0.0])
CLEARING = 7000.0
rng = np.random.default_rng(2617)


def smooth_noise(n, cells, octaves):
    """Sum of upsampled random grids (value noise), roughly -1..1."""
    out = np.zeros((n, n))
    amp = 1.0
    total = 0.0
    for o in range(octaves):
        k = cells * (2 ** o)
        grid = rng.standard_normal((k + 3, k + 3))
        xs = np.linspace(1.0, k + 1.0, n)
        i = np.floor(xs).astype(int)
        f = xs - i
        f = f * f * (3.0 - 2.0 * f)
        rows = grid[i, :] * (1 - f)[:, None] + grid[i + 1, :] * f[:, None]
        layer = rows[:, i] * (1 - f)[None, :] + rows[:, i + 1] * f[None, :]
        out += layer * amp
        total += amp
        amp *= 0.5
    return out / total


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


n = CELLS + 1
axis = np.linspace(-SIZE / 2, SIZE / 2, n)
X, Y = np.meshgrid(axis + CENTRE[0], axis + CENTRE[1], indexing="ij")
d = np.hypot(X - CENTRE[0], Y - CENTRE[1])

rolling = smooth_noise(n, 6, 4)
hills = smooth_noise(n, 3, 3)
rise = smoothstep(CLEARING, CLEARING + 5000.0, d)
height = 60.0 * rolling                                   # the clearing: a gentle roll, under a metre
height += rise * (900.0 + 2200.0 * (0.5 + 0.5 * hills))   # hills of 9 to 31 m beyond it
height += rise * 350.0 * smooth_noise(n, 14, 2)           # lumps on the slopes
# the colossus's den stays flat enough to fight on
height *= 1.0 - 0.85 * (1.0 - smoothstep(CLEARING * 0.6, CLEARING, d))

# bare earth: a worn path from where the hero comes in to the den, and patches; rock on the steep
path_dir = np.array([1.0, 0.0])
along = (X - 0.0) * path_dir[0] + (Y - 0.0) * path_dir[1]
across = np.abs(Y - 300.0 * np.sin(X / 2500.0))
path = (1.0 - smoothstep(250.0, 600.0, across)) * smoothstep(-2000.0, 0.0, along) * (1.0 - smoothstep(5500.0, 6500.0, along))
patches = smoothstep(0.3, 0.6, smooth_noise(n, 10, 2)) * (1.0 - smoothstep(CLEARING * 0.45, CLEARING * 0.8, d))
den = 1.0 - smoothstep(900.0, 2800.0, np.hypot(X - 3500.0, Y))
# mostly grass, as in Dauntless's clearings: the path and the den are worn, not bare
earth = np.clip(path * 0.75 + patches * 0.6 + den * 0.4, 0.0, 1.0)
gy, gx = np.gradient(height, axis[1] - axis[0])
slope = np.hypot(gx, gy)
rock = smoothstep(0.45, 0.8, slope)

# Unreal (X, Y, Z) cm -> glTF (x, y, z) m as the importer turns glTF (x, y, z) into Unreal (x, z, y)
pos = np.stack([X / 100.0, height / 100.0, Y / 100.0], axis=-1).reshape(-1, 3).astype(np.float32)
normal_ue = np.stack([-gx, -gy, np.ones_like(height)], axis=-1)
normal_ue /= np.linalg.norm(normal_ue, axis=-1, keepdims=True)
nrm = np.stack([normal_ue[..., 0], normal_ue[..., 2], normal_ue[..., 1]], axis=-1).reshape(-1, 3).astype(np.float32)
uv = np.stack([X / 1000.0, Y / 1000.0], axis=-1).reshape(-1, 2).astype(np.float32)
col = np.stack([earth, rock, np.zeros_like(earth), np.ones_like(earth)], axis=-1).reshape(-1, 4)
col = np.clip(col * 255.0 + 0.5, 0, 255).astype(np.uint8)

idx = np.arange(n * n).reshape(n, n)
a, b, c, e = idx[:-1, :-1], idx[1:, :-1], idx[1:, 1:], idx[:-1, 1:]
tris = np.stack([np.stack([a, e, c], -1), np.stack([a, c, b], -1)], axis=-2).reshape(-1, 3).astype(np.uint32)

blobs = [pos.tobytes(), nrm.tobytes(), uv.tobytes(), col.tobytes(), tris.tobytes()]
offsets = []
binary = b""
for blob in blobs:
    offsets.append(len(binary))
    binary += blob + b"\0" * ((4 - len(blob) % 4) % 4)
views = [{"buffer": 0, "byteOffset": o, "byteLength": len(bl)} for o, bl in zip(offsets, blobs)]
views[-1]["target"] = 34963
for v in views[:-1]:
    v["target"] = 34962
gltf = {
    "asset": {"version": "2.0", "generator": "Tools/Unreal/make_terrain.py"},
    "scenes": [{"nodes": [0]}],
    "nodes": [{"name": "HangGau_Ground", "mesh": 0}],
    "meshes": [{"name": "HangGau_Ground", "primitives": [{"attributes": {"POSITION": 0, "NORMAL": 1, "TEXCOORD_0": 2, "COLOR_0": 3},
                                                          "indices": 4, "material": 0}]}],
    # a named slot, so the imported mesh has one for import_assets.py to put M_Ground in
    "materials": [{"name": "Ground"}],
    "accessors": [
        {"bufferView": 0, "componentType": 5126, "count": len(pos), "type": "VEC3",
         "min": pos.min(axis=0).tolist(), "max": pos.max(axis=0).tolist()},
        {"bufferView": 1, "componentType": 5126, "count": len(nrm), "type": "VEC3"},
        {"bufferView": 2, "componentType": 5126, "count": len(uv), "type": "VEC2"},
        {"bufferView": 3, "componentType": 5121, "normalized": True, "count": len(col), "type": "VEC4"},
        {"bufferView": 4, "componentType": 5125, "count": tris.size, "type": "SCALAR"},
    ],
    "bufferViews": views,
    "buffers": [{"byteLength": len(binary)}],
}
text = json.dumps(gltf).encode("utf-8")
text += b" " * ((4 - len(text) % 4) % 4)
os.makedirs(OUT, exist_ok=True)
path = os.path.join(OUT, "HangGau_Ground.glb")
with open(path, "wb") as fh:
    fh.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(text) + 8 + len(binary)))
    fh.write(struct.pack("<II", len(text), 0x4E4F534A) + text)
    fh.write(struct.pack("<II", len(binary), 0x004E4942) + binary)
print("wrote %s: %d vertices, %d triangles, heights %.0f..%.0f cm, clearing at the den %.0f cm" % (
    path, len(pos), len(tris), height.min(), height.max(), height[np.unravel_index(np.argmin(np.hypot(X - 3500.0, Y)), X.shape)]))

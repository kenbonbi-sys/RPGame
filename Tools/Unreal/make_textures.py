"""Detail textures for the sculpted creatures, so they carry the fine surface detail Paragon heroes
have (the sculpts are vertex-coloured shapes with no texture of their own):

    python Tools/Unreal/make_textures.py

Writes tileable 1024 px PNGs to Unreal/SourceArt/Creatures (imported by import_assets.py):
- T_FibreNormal: fur strands and bark fibres, streaks along V (normal map);
- T_LumpNormal: moss and lichen lumps (normal map);
- T_CreatureDetail: R the fibres' cavities, G a broad blotch for colour variation, B the lumps.
Everything is filtered noise made in the frequency domain, so it tiles without seams.
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Unreal", "SourceArt", "Creatures")
N = 1024
rng = np.random.default_rng(1717)
fy = np.fft.fftfreq(N)[:, None]
fx = np.fft.fftfreq(N)[None, :]


def filtered(sigma_x, sigma_y):
    """White noise blurred by a Gaussian of `sigma_*` pixels (periodic), scaled to 0..1."""
    spectrum = np.fft.fft2(rng.standard_normal((N, N)))
    kernel = np.exp(-2.0 * np.pi ** 2 * ((fx * sigma_x) ** 2 + (fy * sigma_y) ** 2))
    field = np.real(np.fft.ifft2(spectrum * kernel))
    field -= field.min()
    return field / max(field.max(), 1e-9)


def normal_map(height, strength):
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5 * strength
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5 * strength
    n = np.stack([-dx, -dy, np.ones_like(height)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return np.clip((n * 0.5 + 0.5) * 255.0 + 0.5, 0, 255).astype(np.uint8)


def save(name, array):
    Image.fromarray(array).save(os.path.join(OUT, name + ".png"), optimize=True)
    print("wrote", name)


os.makedirs(OUT, exist_ok=True)

# fibres: long thin streaks at three sizes, sharpened so strands stand apart
fibres = 0.55 * filtered(1.1, 26.0) + 0.3 * filtered(2.2, 48.0) + 0.15 * filtered(4.0, 90.0)
fibres = np.clip((fibres - 0.3) / 0.45, 0.0, 1.0) ** 1.6
save("T_FibreNormal", normal_map(fibres, 28.0))

# lumps: round knobs of moss and lichen
lumps = 0.6 * filtered(5.0, 5.0) + 0.4 * filtered(14.0, 14.0)
lumps = np.clip((lumps - 0.25) / 0.5, 0.0, 1.0) ** 1.3
save("T_LumpNormal", normal_map(lumps, 22.0))

blotch = filtered(70.0, 70.0)
detail = np.stack([fibres, blotch, lumps], axis=-1)
save("T_CreatureDetail", np.clip(detail * 255.0 + 0.5, 0, 255).astype(np.uint8))

"""Bake Wildstone's procedural VFX textures into Assets/Wildstone/Art/VFX/ (PNG only; Unity writes .meta files).

    & "C:/Users/tyler/Main Storage/ai_skelly_tool/.venv/Scripts/python.exe" -I Tools/VFX/make_vfx_textures.py

Provenance: T_VfxStreak, T_VfxBlade, T_VfxStrip, T_VfxEnergy, T_VfxMist_16x4, T_VfxFlame, T_VfxGlow, T_VfxBolt and
T_VfxDebris_4x4 are ported unchanged (same algorithms and seeds) from the user's read-only
C:/Users/tyler/Main Storage/vibegame1/Tools/vfx/make_vfx_textures.py at repository HEAD
2cb6e5760bccc0733f34b367be2f18c90b802766 (file last changed in 76002b418cee61988f5970d8c548a24288cd9927). Its
.meta writer, contact sheet and output paths are dropped. T_VfxHex, T_VfxRing, T_VfxScorch, T_VfxSpark and
T_VfxShockwave are new for Wildstone. Everything is procedural NumPy/SciPy with fixed seeds; no imagery is read.

Single-channel (L) textures are LINEAR masks read from .r, values <= 1:
  T_VfxStreak      256x64    spark / speed-line streak: hot head at u=0, tapering tail to u=1
  T_VfxBlade       256x64    crescent swipe
  T_VfxStrip        32x64    uniform soft line profile (rings, beam core)
  T_VfxEnergy      512x64    u-tileable energy filaments
  T_VfxMist_16x4  4096x1024  flipbook: 16 columns (frames, 256 px) x 4 rows (wisp variants)
  T_VfxFlame       512x128   u-tileable flame tongues
  T_VfxGlow         64x64    gaussian line faded at both ends
  T_VfxBolt         32x64    crisp core in a wide halo
  T_VfxDebris_4x4  256x256   atlas: 4x4 frames of 64 px (6 chips, 4 dust, 4 droplets, 2 glints)
  T_VfxHex         512x512   u/v-tileable hex-grid line mask (7 x 8 cells; hexes ~1% anisotropic for exact tiling)
  T_VfxRing        256x256   soft radial ring, zero at the border
  T_VfxSpark       128x32    short hot streak spark: head at u=0
  T_VfxFlare       256x256   round billboard glow: gaussian core + faint 4-point star, 0 before the border
RGB(A) textures:
  T_VfxScorch      512x512   RGBA sRGB colour + linear alpha: dark soot decal, irregular soft edge
  T_VfxShockwave   256x256   RGB linear: R,G ring distortion normal (0.5 = flat), B ring intensity
"""
import hashlib
import os

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter, map_coordinates, shift

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Wildstone", "Art", "VFX")


# ----------------------------------------------------------------------------- helpers (ported)

def grid(w, h):
    """u in [0,1] along x, y in [-1,1] across (row 0 = v 0)."""
    u = (np.arange(w) + 0.5) / w
    y = ((np.arange(h) + 0.5) / h) * 2.0 - 1.0
    return np.meshgrid(u, y)


def profile(y, half_width, core_frac=0.38, halo=0.30):
    """A hot flattened-gaussian core with a soft halo, zero at |y| = 1."""
    w = np.maximum(half_width, 1e-4)
    core = np.exp(-(y / (w * core_frac * 1.25)) ** 4)
    glow = np.exp(-(y / w) ** 2 * 1.6) * (halo + 0.12)
    edge = np.clip(1.0 - np.abs(y), 0.0, 1.0) ** 0.5
    return np.maximum(core, glow) * edge


def periodic_walk(w, knots, amp, rng):
    """A jagged, u-periodic path: random knots joined by straight segments."""
    k = rng.normal(0, amp, knots)
    xs = np.arange(knots + 1) / knots
    ks = np.concatenate([k, k[:1]])
    return np.interp((np.arange(w) + 0.5) / w, xs, ks)


# ----------------------------------------------------------------------------- ported textures

def streak():
    U, Y = grid(256, 64)
    head = np.exp(-((U - 0.05) / 0.05) ** 2)
    body = (1.0 - U) ** 1.4 * np.exp(-U * 0.6)
    along = np.maximum(head, 0.9 * body) * np.clip(U / 0.02, 0, 1)
    half = 0.52 - 0.36 * U ** 0.7
    return along * profile(Y, half, core_frac=0.40, halo=0.30)


def blade():
    U, Y = grid(256, 64)
    rise = np.clip(U / 0.30, 0, 1) ** 0.8
    fall = np.clip((1.0 - U) / 0.70, 0, 1) ** 1.3
    along = np.sin(np.pi * 0.5 * rise) * fall
    half = 0.12 + 0.50 * along
    return along * profile(Y, half, core_frac=0.40, halo=0.32)


def strip():
    U, Y = grid(32, 64)
    return profile(Y, 0.62, core_frac=0.36, halo=0.34)


def energy():
    w, h = 512, 64
    U, Y = grid(w, h)
    rng = np.random.default_rng(77)
    out = np.zeros((h, w))
    core = periodic_walk(w, 48, 0.07, rng) + periodic_walk(w, 11, 0.06, rng)
    out = np.maximum(out, np.exp(-((Y - core) / 0.075) ** 2))
    for i in range(3):
        path = periodic_walk(w, 36, 0.10, rng) + periodic_walk(w, 7, 0.22, rng)
        path = np.clip(path, -0.55, 0.55)
        gate = np.clip(0.35 + periodic_walk(w, 14, 0.8, rng), 0.0, 1.0)
        out = np.maximum(out, np.exp(-((Y - path) / 0.045) ** 2) * gate * (0.85 - 0.15 * i))
    out = np.maximum(out, np.exp(-(Y / 0.40) ** 2) * 0.30)
    return out * np.clip(1.0 - np.abs(Y) ** 2, 0, 1)


MIST_FS, MIST_FRAMES, MIST_VARIANTS = 256, 16, 4


def mist_puff(seed):
    """One wisp advected through divergence-free curl noise; 16 of 64 simulated steps, centroid-centred."""
    fs = MIST_FS
    rng = np.random.default_rng(seed)
    ys, xs = np.meshgrid(np.arange(fs, dtype=float), np.arange(fs, dtype=float), indexing="ij")
    cx = xs / (fs - 1) * 2 - 1
    cy = ys / (fs - 1) * 2 - 1

    def smooth_noise(sigma):
        g = gaussian_filter(rng.standard_normal((fs, fs)), sigma, mode="wrap")
        return g / (np.abs(g).max() + 1e-9)

    ang = rng.uniform(0, np.pi)
    ex, ey = rng.uniform(0.85, 1.2), rng.uniform(0.75, 1.05)
    rx = (cx * np.cos(ang) + cy * np.sin(ang)) / ex
    ry = (-cx * np.sin(ang) + cy * np.cos(ang)) / ey
    rad = np.sqrt(rx ** 2 + ry ** 2)
    detail = 0.55 * smooth_noise(10) + 0.30 * smooth_noise(4) + 0.15 * smooth_noise(1.6)
    detail = (detail - detail.min()) / (detail.max() - detail.min())
    r0 = rad + rng.uniform(0.25, 0.38) * smooth_noise(rng.uniform(16, 26))
    fall = np.clip(1.0 - r0 / 0.52, 0, 1)
    dens = fall ** 1.2 * (0.25 + 0.75 * detail ** 1.4)
    psi_a, psi_b = smooth_noise(rng.uniform(20, 30)), smooth_noise(rng.uniform(7, 11))
    psi_c, psi_d = smooth_noise(rng.uniform(20, 30)), smooth_noise(rng.uniform(7, 11))
    steps, sim = 3, 64
    edge = np.clip((1.0 - np.maximum(np.abs(cx), np.abs(cy))) / 0.10, 0, 1)
    keep = {round(i * (sim - 1) / (MIST_FRAMES - 1)) for i in range(MIST_FRAMES)}
    frames = []
    for f in range(sim):
        t = f / (sim - 1)
        if f in keep:
            frames.append(dens.copy())
        k = 0.5 - 0.5 * np.cos(np.pi * t)
        psi = (1 - k) * (psi_a + psi_b * 0.45) + k * (psi_c + psi_d * 0.45)
        gy, gx = np.gradient(psi)
        vx, vy = gy, -gx
        vmax = np.abs(np.stack([vx, vy])).max() + 1e-9
        speed = 0.75 + 0.95 * t
        vx = vx / vmax * speed + cx * 0.10
        vy = vy / vmax * speed + cy * 0.10
        for _ in range(steps):
            dens = map_coordinates(dens, [ys - vy, xs - vx], order=1, mode="constant")
            dens = gaussian_filter(dens, 0.35)
    out = []
    for i, img in enumerate(frames):
        t = i / (MIST_FRAMES - 1)
        m = img.sum() + 1e-9
        my, mx = (img * ys).sum() / m, (img * xs).sum() / m
        img = shift(img, ((fs - 1) / 2 - my, (fs - 1) / 2 - mx), order=1, mode="constant")
        img = img * edge
        out.append(np.clip(img / np.percentile(img, 99.7) * (0.95 - 0.2 * t), 0, 1))
    return out


def mist_flipbook():
    fs = MIST_FS
    sheet = np.zeros((fs * MIST_VARIANTS, fs * MIST_FRAMES))
    for v, seed in enumerate([1234, 5821, 9043, 31337]):
        for f, img in enumerate(mist_puff(seed)):
            sheet[v * fs:(v + 1) * fs, f * fs:(f + 1) * fs] = img
    return sheet


def flame():
    w, h = 512, 128
    rng = np.random.default_rng(4242)
    u = (np.arange(w) + 0.5) / w
    y = ((np.arange(h) + 0.5) / h) * 2 - 1
    U, Y = np.meshgrid(u, y)

    def pnoise(sx, sy):
        g = gaussian_filter(rng.standard_normal((h, w)), (sy, sx), mode=("reflect", "wrap"))
        return (g - g.min()) / (g.max() - g.min())

    def tongues1d(count):
        acc = np.zeros(w)
        for _ in range(count):
            c = rng.random()
            wd = rng.uniform(0.02, 0.055)
            hgt = rng.uniform(0.35, 1.0) ** 1.3
            dd = np.abs(np.mod(u - c + 0.5, 1.0) - 0.5)
            acc = np.maximum(acc, hgt * np.exp(-(dd / wd) ** 1.4))
        low = gaussian_filter(rng.standard_normal(w), 14.0, mode="wrap")
        low = (low - low.min()) / (low.max() - low.min())
        return 0.18 + 0.14 * low + 0.68 * acc

    out = np.zeros((h, w))
    for side in (1, -1):
        d = np.clip(Y * side, 0, None)
        warp = (pnoise(8, 14) - 0.5) * 0.22 * d + 0.12 * d
        uu = np.mod(U + warp, 1.0)
        ht = np.interp(uu.ravel(), u, tongues1d(12), period=1.0).reshape(uu.shape)
        k = np.clip(1.0 - d / ht, 0, 1)
        turb = pnoise(6, 9)
        holes = np.clip((pnoise(5, 7) - 0.28) / 0.3, 0, 1)
        tongue = np.clip(k * 1.5, 0, 1) ** 0.8 * (0.3 + 0.7 * k) * (0.5 + 0.5 * turb)
        tongue *= 1.0 - (1.0 - holes) * np.clip(d / ht, 0, 1) * 0.8
        out = np.maximum(out, np.where(Y * side >= 0, tongue, 0))
    out = np.maximum(out, np.exp(-(Y / 0.17) ** 2) * (0.5 + 0.5 * pnoise(10, 4)))
    out = gaussian_filter(out, (1.2, 1.5), mode=("reflect", "wrap"))
    out *= np.clip(1.0 - np.abs(Y) ** 6, 0, 1)
    return np.clip(out / out.max(), 0, 1)


def glow():
    U, Y = grid(64, 64)
    ends = np.clip(np.minimum(U, 1.0 - U) / 0.14, 0, 1)
    ends = ends * ends * (3 - 2 * ends)
    return np.exp(-(Y / 0.42) ** 2) * np.clip(1.0 - np.abs(Y), 0, 1) ** 0.5 * ends


def bolt():
    U, Y = grid(32, 64)
    return profile(Y, 0.92, core_frac=0.13, halo=0.30)


def debris_atlas():
    fs = 64
    rng = np.random.default_rng(99)
    ys, xs = np.meshgrid((np.arange(fs) + 0.5) / fs * 2 - 1, (np.arange(fs) + 0.5) / fs * 2 - 1, indexing="ij")
    ang = np.arctan2(ys, xs)
    rad = np.sqrt(xs ** 2 + ys ** 2)
    frames = []
    for i in range(6):   # grit chips: intersections of random half-planes, lit edge
        k = rng.integers(4, 7)
        size = 0.30 + 0.22 * rng.random()
        sd = np.full((fs, fs), -1e9)
        for j in range(k):
            a = (j + rng.random() * 0.7) / k * 2 * np.pi
            d = size * (0.7 + 0.6 * rng.random()) * (1.0 + 0.5 * (j == 0))
            sd = np.maximum(sd, xs * np.cos(a) + ys * np.sin(a) - d)
        inside = np.clip(-sd * fs * 0.6, 0, 1)
        edge = np.clip(1 - np.abs(sd) * fs * 0.35, 0, 1) * inside
        lit = np.clip(0.55 + 0.35 * (-xs - ys) / max(size, 0.2), 0.35, 1.0)
        frames.append(np.maximum(np.maximum(inside * lit * 0.8, edge), gaussian_filter(inside, 2.5) * 0.30))
    for i in range(4):   # dust specks
        nz = gaussian_filter(rng.random((fs, fs)), 3.0)
        nz = (nz - nz.min()) / (nz.max() - nz.min())
        frames.append(np.clip(1 - rad / (0.75 + 0.1 * i), 0, 1) ** 1.6 * (0.45 + 0.55 * nz))
    for i in range(4):   # droplets
        r0 = 0.40 + 0.05 * i
        body = np.clip((r0 - rad) * fs * 0.4, 0, 1)
        rim = np.exp(-((rad - r0 * 0.86) / 0.07) ** 2) * body
        hi = np.exp(-(((xs + 0.14) / 0.10) ** 2 + ((ys + 0.16) / 0.08) ** 2))
        frames.append(np.maximum(np.maximum(body * 0.30 + rim * 0.6, hi), gaussian_filter(body, 4.0) * 0.25))
    for i in range(2):   # glint stars
        a = ang - (0.0 if i == 0 else np.pi / 4)
        spike = np.exp(-(np.abs(np.sin(2 * a)) * rad / 0.035) ** 2) * np.clip(1 - rad, 0, 1) ** 1.5
        frames.append(np.clip(np.maximum(spike, np.exp(-(rad / 0.16) ** 2)), 0, 1))
    sheet = np.zeros((fs * 4, fs * 4))
    for f, img in enumerate(frames):
        img = img.copy()
        img[:2, :] = img[-2:, :] = 0
        img[:, :2] = img[:, -2:] = 0
        r, c = divmod(f, 4)
        sheet[r * fs:(r + 1) * fs, c * fs:(c + 1) * fs] = np.clip(img, 0, 1)
    return sheet


# ----------------------------------------------------------------------------- new for Wildstone

def hex_grid(size=512, cols=7, double_rows=4, line=0.055):
    """Pointy-top hex lattice edges. The texture spans exactly `cols` horizontal and `double_rows` vertical
    lattice periods, so it tiles in u and v; 7/4 vs sqrt(3) leaves the hexes 1% wide."""
    px = (np.arange(size) + 0.5) / size
    X, Yv = np.meshgrid(px * cols * np.sqrt(3), px * double_rows * 3.0)   # circumradius 1 lattice units
    period = np.array([np.sqrt(3), 3.0])
    best = np.full(X.shape, np.inf)
    qx, qy = np.zeros_like(X), np.zeros_like(X)
    for ox, oy in ((0.0, 0.0), (np.sqrt(3) / 2, 1.5)):   # the two rectangular sub-lattices of a hex lattice
        dx = np.mod(X - ox + period[0] / 2, period[0]) - period[0] / 2
        dy = np.mod(Yv - oy + period[1] / 2, period[1]) - period[1] / 2
        d = dx * dx + dy * dy
        closer = d < best
        best = np.where(closer, d, best)
        qx, qy = np.where(closer, dx, qx), np.where(closer, dy, qy)
    r = np.maximum(np.abs(qx), np.maximum(np.abs(qx * .5 + qy * np.sqrt(3) / 2), np.abs(qx * .5 - qy * np.sqrt(3) / 2)))
    edge = np.sqrt(3) / 2 - r                                   # distance to the cell edge (apothem - hex radius)
    aa = cols * np.sqrt(3) / size                               # one pixel in lattice units
    return np.clip((line - edge) / aa + 0.5, 0, 1)


def radial(size):
    c = (np.arange(size) + 0.5) / size * 2 - 1
    X, Y = np.meshgrid(c, c)
    return X, Y, np.sqrt(X * X + Y * Y)


def ring():
    _, _, r = radial(256)
    return np.exp(-((r - 0.72) / 0.09) ** 2) * np.clip((1 - r) / 0.08, 0, 1)


def spark():
    U, Y = grid(128, 32)
    along = np.maximum(np.exp(-((U - 0.06) / 0.06) ** 2), (1 - U) ** 2.4 * 0.85) * np.clip(U / 0.03, 0, 1)
    return along * profile(Y, 0.55 - 0.40 * U ** 0.6, core_frac=0.45, halo=0.22)


def flare():
    """Round billboard glow: soft gaussian core plus a faint 4-point star, reaching 0 before the border."""
    X, Y, r = radial(256)
    core = np.exp(-(r / 0.22) ** 2)
    star = 0.35 * (np.exp(-(Y / 0.018) ** 2) + np.exp(-(X / 0.018) ** 2)) * np.clip(1 - r, 0, 1) ** 2
    fade = np.clip((0.95 - r) / 0.25, 0, 1)
    return np.maximum(core, star) * fade * fade * (3 - 2 * fade)


def scorch():
    """Dark soot decal: an irregular noise-bitten disc, densest at the centre, with faint radial streaks."""
    size = 512
    rng = np.random.default_rng(31415)
    X, Y, r = radial(size)
    ang = np.arctan2(Y, X)

    def noise(sigma):
        g = gaussian_filter(rng.standard_normal((size, size)), sigma, mode="wrap")
        return (g - g.min()) / (g.max() - g.min())
    rim = 0.62 + 0.20 * (noise(22) - 0.5) + 0.10 * (noise(7) - 0.5)
    streaks = 0.5 + 0.5 * np.cos(ang * 23 + 3 * noise(30)) * np.cos(ang * 9 + 1.7)
    reach = rim + 0.045 * streaks * np.clip((r - 0.3) / 0.4, 0, 1)
    alpha = np.clip(1.25 * (1 - r / reach), 0, 1) ** 0.6 * (0.82 + 0.18 * noise(3))
    alpha *= np.clip((1 - r) / 0.06, 0, 1)
    soot = 0.035 + 0.06 * noise(5) + 0.04 * np.clip(r / reach, 0, 1)   # sRGB-ish dark values, warm toward the rim
    rgb = np.stack([soot * 1.10, soot * 0.98, soot * 0.88], -1)
    return np.concatenate([rgb, alpha[..., None]], -1)


def shockwave():
    """Ring height h(r); R,G encode the in-plane gradient normal (0.5 = flat), B the ring intensity."""
    X, Y, r = radial(256)
    r0, w = 0.68, 0.10
    h = np.exp(-((r - r0) / w) ** 2) * np.clip((1 - r) / 0.08, 0, 1)
    slope = -2 * (r - r0) / (w * w) * h * w * 0.45                      # dh/dr scaled to roughly [-1, 1]
    nx, ny = -slope * X / np.maximum(r, 1e-6), -slope * Y / np.maximum(r, 1e-6)
    return np.stack([np.clip(nx * 0.5 + 0.5, 0, 1), np.clip(ny * 0.5 + 0.5, 0, 1), h], -1)


TEXTURES = [
    ("T_VfxStreak", streak), ("T_VfxBlade", blade), ("T_VfxStrip", strip), ("T_VfxEnergy", energy),
    ("T_VfxDebris_4x4", debris_atlas), ("T_VfxMist_16x4", mist_flipbook), ("T_VfxFlame", flame),
    ("T_VfxGlow", glow), ("T_VfxBolt", bolt),
    ("T_VfxHex", hex_grid), ("T_VfxRing", ring), ("T_VfxSpark", spark),
    ("T_VfxScorch", scorch), ("T_VfxShockwave", shockwave), ("T_VfxFlare", flare),
]


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, fn in TEXTURES:
        arr = np.clip(fn(), 0.0, 1.0)
        mode = "L" if arr.ndim == 2 else ("RGB" if arr.shape[2] == 3 else "RGBA")
        img = Image.fromarray((arr * 255.0 + 0.5).astype(np.uint8), mode=mode)
        path = os.path.join(OUT, name + ".png")
        img.save(path, optimize=True)
        digest = hashlib.sha256(open(path, "rb").read()).hexdigest()
        print("%-16s %4dx%-4d %-4s %8.1f KB  sha256 %s" % (name, img.width, img.height, mode, os.path.getsize(path) / 1024, digest))
    # Tiling check: a 2x2 repeat sampled directly equals four copies of the texture.
    tile, big = hex_grid(), hex_grid(1024, 14, 8)
    for oy in (0, 512):
        for ox in (0, 512):
            assert np.abs(big[oy:oy + 512, ox:ox + 512] - tile).max() < 1e-6, ("hex tiling", ox, oy)
    glow = flare()
    assert glow.max() > .99 and max(glow[0].max(), glow[-1].max(), glow[:, 0].max(), glow[:, -1].max()) == 0, "flare edge"


if __name__ == "__main__":
    main()

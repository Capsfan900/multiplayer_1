"""Wildstone armored soldier: Soldier.fbx (third person, 3 LODs, team marks) and SoldierArms.fbx (first person).

Run with the isolated ai_skelly_tool Python. Never import torch and bpy in one process (forge rule): `concept`
imports only the forge's SDXL/TripoSR stages; every other mode imports only bpy.

    $PY = "C:\\Users\\tyler\\Main Storage\\ai_skelly_tool\\.venv\\Scripts\\python.exe"
    & $PY -B Tools/Assets/build_soldier.py concept  --output-root .  # GPU: forge SDXL front/back views + TripoSR mesh
    & $PY -B Tools/Assets/build_soldier.py model    --output-root .  # Soldier.blend + Assets/Wildstone/Art/Soldier.fbx
    & $PY -B Tools/Assets/build_soldier.py arms     --output-root .  # SoldierArms.blend + SoldierArms.fbx
    & $PY -B Tools/Assets/build_soldier.py verify   --output-root .  # clean-scene FBX reimport assertions
    & $PY -B Tools/Assets/build_soldier.py counts   --output-root .  # triangle counts only (tuning)
    & $PY -B Tools/Assets/build_soldier.py previews --output-root .  # ArtSource/Previews/Soldier*.png
    & $PY -B Tools/Assets/build_soldier.py all      --output-root .  # model+arms+verify+previews+manifest (no GPU)

Contract: Tools/Assets/soldier_skeleton.py (bone table, sockets, to_blender). Geometry is authored in Unity metres
(right +X, up +Y, forward +Z, feet y=0) and converted once by to_blender(); exports need NO import mirror.
Concept/TripoSR outputs (ArtSource/Characters/Forge) are visual references only, never exported to Assets/.
Provenance: Tube/superellipse/dome/stations/skin/slab/shell/tube-sweep helpers and the gauntlet palm, back lames,
knuckle guard, digit and lame shapes are ported from vibegame1 Tools/weapon_models/build_art.py and
build_weapons.py (read-only, commit 2cb6e57), adapted from its hilt-wrapped fist to the skeleton's straight
T-pose fingers with one rigid piece per phalanx. Everything else is original.
"""
import argparse
import functools
import hashlib
import json
import math
import subprocess
import sys
from pathlib import Path

import numpy as np

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import soldier_skeleton as sk  # noqa: E402

FORGE = Path(r"C:\Users\tyler\Main Storage\ai_skelly_tool")
VIBE = Path(r"C:\Users\tyler\Main Storage\vibegame1")
B = sk.to_blender
bpy = bmesh = Vector = Matrix = None

ARMOR, SUIT, TRIM, VISOR, ACCENT, MARK, GLOW = ("SoldierArmor", "SoldierSuit", "SoldierTrim", "SoldierVisor",
                                                "SoldierAccent", "SoldierMark", "SoldierGlow")
# sRGB base colour, metallic, roughness. Unity remaps by name; these are sensible defaults.
MATERIALS = {ARMOR: ((.80, .79, .75), 0, .40), SUIT: ((.12, .13, .14), 0, .80), TRIM: ((.25, .26, .28), .5, .36),
             VISOR: ((.03, .035, .045), .3, .06), ACCENT: ((.10, .70, .85), 0, .45), MARK: ((.95, .95, .92), 0, .5),
             GLOW: ((.08, .08, .08), 0, .4)}
# sRGB; kept equal to SoldierRig.TeamAccents (Unity material Color inputs are sRGB).
TEAMS = {1: (.16, .73, .78), 2: (.98, .92, .50), 3: (.40, .055, .10), 4: (.43, .25, .60)}
MATERIALS[ACCENT] = (TEAMS[1], 0, .45)
DIGITS = ("Index", "Middle", "Ring", "Little", "Thumb")
SEGS = ("Proximal", "Intermediate", "Distal")
TRI_LIMITS = {"Soldier_LOD0": (18000, 22000), "Soldier_LOD1": (7000, 9000), "Soldier_LOD2": (2500, 3500),
              "SoldierArms": (8000, 12000)}


def U(p):
    """Blender -> Unity (inverse of to_blender)."""
    return (-p[0], p[2], -p[1])


def load_bpy():
    global bpy, bmesh, Vector, Matrix
    import bpy as _bpy
    import bmesh as _bmesh
    from mathutils import Matrix as _M, Vector as _V
    bpy, bmesh, Vector, Matrix = _bpy, _bmesh, _V, _M


# ------------------------------------------------------------------ level of detail state
LODS = [dict(k=.6, bev=(.003, 1), fine=True, mid=True),
        dict(k=.55, bev=None, fine=False, mid=True),
        dict(k=.2, bev=None, fine=False, mid=False)]
ARMS_Q = dict(k=.72, bev=(.003, 1), fine=True, mid=True, round=.012)   # first person: denser, rounded thick plates
# Gauntlet detail: first-person arms 'full'; body LOD0 'mid' (segmented rigid phalanges, fewer facets);
# LOD1/LOD2 'low' (one tube per digit, each ring on its phalanx bone).
HAND = {"full": dict(nv=8, kd=2, lame=(3, 5), cop=True, palm=16, lames=3),
        "mid": dict(nv=6, kd=1, lame=None, cop=False, palm=12, lames=2),
        "low": dict(nv=6, kd=0, lame=None, cop=False, palm=10, lames=1)}
Q = dict(LODS[0])


def set_lod(q):
    Q.clear(); Q.update(LODS[q] if isinstance(q, int) else q)


def nn(base, lo=2):
    return max(lo, int(round(base * Q["k"])))


def bev(th):
    """Hardened chamfer; first-person plates at least Q['round'] thick get a rounded 2-segment bevel."""
    if not Q["bev"] or th < .004:
        return None
    if th >= Q.get("round", 1):
        return (round(min(.0035, th * .3), 4), 2)
    w, seg = Q["bev"]
    return (round(min(w, th * .3), 4), seg)


# ------------------------------------------------------------------ surface kernel (ported from vibegame1 build_art.py)
def unit(v):
    v = np.asarray(v, float); n = np.linalg.norm(v)
    return v / n if n > 1e-12 else v


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


def ramp(x, centre, half):
    return smooth((x - centre + half) / (2 * half))


def edge(v, centre, span):
    return min(1.0, abs(v - centre) / span)


def superellipse(a, b, v, p=2.0):
    c, s = abs(math.cos(v)), abs(math.sin(v))
    return (max(c / a, 1e-9) ** p + max(s / b, 1e-9) ** p) ** (-1 / p)


def dome_factor(u, u0, u1, floor=.14):
    if u < u0: return max(floor, math.sqrt(max(0, 1 - ((u0 - u) / max(u0, 1e-6)) ** 2)))
    if u > u1: return max(floor, math.sqrt(max(0, 1 - ((u - u1) / max(1 - u1, 1e-6)) ** 2)))
    return 1.0


def stations(u0, u1, n, dome0=0.0, dome1=0.0, k=4):
    body = list(np.linspace(u0 + dome0, u1 - dome1, n))
    head = [u0 + dome0 * (1 - math.cos(a)) for a in np.linspace(0, math.pi / 2, k + 1)[:-1]] if dome0 else []
    tail = [u1 - dome1 * (1 - math.cos(a)) for a in np.linspace(math.pi / 2, 0, k + 1)[1:]] if dome1 else []
    return head + body + tail


def ridge(t, width=.10, centre=.5):
    return math.exp(-((t - centre) / width) ** 2)


def pillow(h, e=.4, extra=None):
    return lambda s, t: h * max(0.0, math.sin(math.pi * s) * math.sin(math.pi * t)) ** e + (extra(s, t) if extra else 0)


class Tube:
    """Stations along an axis with frames (T tangent, D from `ref`, L = T x D); v = 0 is D.
    at(u, v, extra) = axis(u) + (radius(u, v) + extra) * (cos v D + sin v L); u is arc length 0..1."""

    def __init__(self, pts, ref, radius=None):
        self.p = np.asarray(pts, float)
        seg = np.linalg.norm(np.diff(self.p, axis=0), axis=1)
        self.length = seg.sum()
        self.u = np.r_[0, np.cumsum(seg)] / self.length
        t = np.gradient(self.p, self.u, axis=0)
        self.t = t / np.linalg.norm(t, axis=1)[:, None]
        r = np.asarray(ref, float)
        self.d = np.array([unit(r - r.dot(tt) * tt) for tt in self.t])
        self.radius = radius

    def frame(self, u):
        c = np.array([np.interp(u, self.u, self.p[:, k]) for k in range(3)])
        t = unit([np.interp(u, self.u, self.t[:, k]) for k in range(3)])
        d = np.array([np.interp(u, self.u, self.d[:, k]) for k in range(3)])
        d = unit(d - d.dot(t) * t)
        return c, t, d, np.cross(t, d)

    def at(self, u, v, extra=0.0):
        c, _, d, l = self.frame(u)
        return c + (self.radius(u, v) + extra) * (math.cos(v) * d + math.sin(v) * l)

    def coord(self, u, k):
        return float(np.interp(u, self.u, self.p[:, k]))

    def u_of(self, k, value):
        c = self.p[:, k]
        return float(np.interp(value, c[::-1], self.u[::-1]) if c[-1] < c[0] else np.interp(value, c, self.u))


class Part:
    """Unity-space geometry with one material and per-vertex bone weights (a bone name = rigid)."""

    def __init__(self, verts, faces, mat, bone, bevel=None, hard=True):
        self.v = np.asarray(verts, float).reshape(-1, 3)
        self.f = [tuple(int(i) for i in f) for f in faces]
        self.mat, self.bevel, self.hard = mat, bevel, hard
        self.w = [finalize(w) for w in bone] if isinstance(bone, list) else [{bone: 1.0}] * len(self.v)


def finalize(w):
    items = sorted(((b, x) for b, x in w.items() if x > .01), key=lambda i: -i[1])[:4]
    s = sum(x for _, x in items)
    return {b: x / s for b, x in items}


def swap(name):
    return "Left" + name[5:] if name.startswith("Right") else name


def mirror(p):
    q = Part(p.v * [-1, 1, 1], [tuple(reversed(f)) for f in p.f], p.mat, "x", p.bevel, p.hard)
    q.w = [{swap(b): x for b, x in w.items()} for w in p.w]
    return q


def chain(x, bones, centres, half):
    halves = half if isinstance(half, (list, tuple)) else [half] * len(centres)
    ts = [ramp(x, c, h) for c, h in zip(centres, halves)]
    vals = [1 - ts[0]] + [ts[i] - ts[i + 1] for i in range(len(ts) - 1)] + [ts[-1]]
    return {b: v for b, v in zip(bones, vals) if v > 1e-4}


def skin(tube, us, nv, extra=0.0):
    vs = np.linspace(0, math.tau, nv, endpoint=False)
    rings = [[tube.at(u, v, extra) for v in vs] for u in us]
    verts = [p for r in rings for p in r]; m = len(rings)
    faces = [(j * nv + i, j * nv + (i + 1) % nv, (j + 1) * nv + (i + 1) % nv, (j + 1) * nv + i)
             for j in range(m - 1) for i in range(nv)]
    for j in (0, m - 1):
        verts.append(np.mean(rings[j], axis=0)); k = len(verts) - 1
        faces += [(j * nv + i, j * nv + (i + 1) % nv, k) for i in range(nv)]
    return verts, faces


def slab(outer, inner, closed=False):
    nu, nv = len(outer), len(outer[0])
    verts = [p for r in outer for p in r] + [p for r in inner for p in r]
    O = lambda i, j: i * nv + j
    I = lambda i, j: nu * nv + i * nv + j
    jr = range(nv) if closed else range(nv - 1)
    faces = []
    for i in range(nu - 1):
        for j in jr:
            j2 = (j + 1) % nv
            faces += [(O(i, j), O(i, j2), O(i + 1, j2), O(i + 1, j)), (I(i, j), I(i + 1, j), I(i + 1, j2), I(i, j2))]
    for i in (0, nu - 1):
        for j in jr:
            faces.append((O(i, j), I(i, j), I(i, (j + 1) % nv), O(i, (j + 1) % nv)))
    if not closed:
        for j in (0, nv - 1):
            for i in range(nu - 1):
                faces.append((O(i, j), O(i + 1, j), I(i + 1, j), I(i, j)))
    return verts, faces


def grid_slab(point, nu, nv, th, lift=None, closed=False):
    lift = lift or (lambda s, t: 0.0)
    ss = np.linspace(0, 1, nu)
    ts = [j / (nv if closed else nv - 1) for j in range(nv)]
    outer = [[point(s, t, th + lift(s, t)) for t in ts] for s in ss]
    inner = [[point(s, t, lift(s, t)) for t in ts] for s in ss]
    return slab(outer, inner, closed)


def _fn(x):
    return x if callable(x) else (lambda _: x)


def shell(tube, u0, u1, v0, v1, gap, th, mat, bone, nu=6, nv=10, lift=None, closed=False, hard=True):
    """A plate hugging `tube`: `gap` off the skin, `th` thick, raised by lift(s, t). The u edges may be functions of
    the angle v (contoured outlines: U-shaped pecs, a V brow, pointed shins); the v edges may be functions of s."""
    if closed:
        v1 = v0 + math.tau
    U0, U1, V0, V1 = map(_fn, (u0, u1, v0, v1))

    def pt(s, t, o):
        v = V0(s) + (V1(s) - V0(s)) * t
        a, b = U0(v), U1(v)
        return tube.at(a + (b - a) * s, v, gap + o)
    return Part(*grid_slab(pt, nn(nu), nn(nv, 3), th, lift, closed), mat, bone, bev(th), hard)


def cap(centre, axis, up, R, a0, a1, b0, b1, th, mat, bone, nu=6, nv=12, lift=None):
    """Ellipsoidal band plate about `centre`: R = (along axis, across); polar a0..a1 deg, azimuth b0..b1 deg."""
    Ra, Rp = R if isinstance(R, tuple) else (R, R)
    c, ax = np.asarray(centre, float), unit(axis)
    upv = unit(np.asarray(up, float) - np.dot(up, ax) * ax); side = np.cross(ax, upv)

    def pt(s, t, o):
        a, b = math.radians(a0 + (a1 - a0) * s), math.radians(b0 + (b1 - b0) * t)
        return c + (Ra + o) * math.cos(a) * ax + (Rp + o) * math.sin(a) * (math.cos(b) * upv + math.sin(b) * side)
    return Part(*grid_slab(pt, nn(nu), nn(nv, 3), th, lift), mat, bone, bev(th))


def loft(rings):
    n = len(rings[0]); verts = [p for r in rings for p in r]
    faces = [tuple(reversed(range(n))), tuple(range((len(rings) - 1) * n, len(rings) * n))]
    for j in range(len(rings) - 1):
        faces += [(j * n + i, j * n + (i + 1) % n, (j + 1) * n + (i + 1) % n, (j + 1) * n + i) for i in range(n)]
    return verts, faces


def tube_geo(points, radius, sides):
    """Swept round section with parallel-transported frames (vibegame1 build_weapons.tube)."""
    pts = np.asarray(points, float); rings, side = [], None
    for i, p in enumerate(pts):
        t = unit(pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)])
        if side is not None:
            side = side - side.dot(t) * t
        if side is None or np.linalg.norm(side) < .2:
            side = np.cross(t, [0, 1, 0])
            if np.linalg.norm(side) < .3:
                side = np.cross(t, [1, 0, 0])
        side = unit(side); other = unit(np.cross(t, side))
        rings.append([p + radius * (math.cos(a) * side + math.sin(a) * other)
                      for a in np.linspace(0, math.tau, sides, endpoint=False)])
    return loft(rings)


def sweep(points, radius, mat, bone, sides=12):
    return Part(*tube_geo(points, radius, nn(sides, 6)), mat, bone, bev(2 * radius))


def seam(tube, u0, u1, v, weight, n=12):
    """A raised undersuit seam along constant v, soft-weighted like the suit under it."""
    verts, faces = tube_geo([tube.at(u, v, .0004) for u in np.linspace(u0, u1, n)], .0022, 4)
    return Part(verts, faces, SUIT, [weight(p) for p in verts], hard=False)


def fan_prism(outer, inner, oc, ic):
    n = len(outer); verts = list(outer) + list(inner) + [oc, ic]
    faces = [(i, (i + 1) % n, 2 * n) for i in range(n)] + [(n + (i + 1) % n, n + i, 2 * n + 1) for i in range(n)]
    faces += [(i, n + i, n + (i + 1) % n, (i + 1) % n) for i in range(n)]
    return verts, faces


def sg(x):
    return math.copysign(1.0, x)


def rbox(centre, half, mat, bone, n=16, p=6.0, frame=None):
    """Rounded box extruded along local z (flat front/back faces); frame = local (x, y, z) axes in Unity space."""
    hx, hy, hz = half
    ex, ey, ez = (np.asarray(a, float) for a in (frame or ((1, 0, 0), (0, 1, 0), (0, 0, 1))))
    c = np.asarray(centre, float)

    def ring(z):
        return [c + hx * sg(math.cos(a)) * abs(math.cos(a)) ** (2 / p) * ex
                + hy * sg(math.sin(a)) * abs(math.sin(a)) ** (2 / p) * ey + z * ez
                for a in np.linspace(0, math.tau, nn(n, 8), endpoint=False)]
    return Part(*loft([ring(-hz), ring(hz)]), mat, bone, bev(2 * min(half)))


def xcyl(stations_, cy, cz, mat, bone, sides=16):
    """Lofted cylinder along Unity X: stations (x, r)."""
    rings = [[(x, cy + r * math.cos(a), cz + r * math.sin(a)) for a in np.linspace(0, math.tau, nn(sides, 6), endpoint=False)]
             for x, r in stations_]
    return Part(*loft(rings), mat, bone, bev(min(r for _, r in stations_)))


def washer(x0, x1, ri, ro, cy, cz, mat, bone, n=20):
    """Flat ring facing +X (an annulus extruded x0..x1)."""
    ring = lambda r, x: [(x, cy + r * math.cos(a), cz + r * math.sin(a)) for a in np.linspace(0, math.tau, nn(n, 8), endpoint=False)]
    return Part(*slab([ring(ri, x1), ring(ro, x1)], [ring(ri, x0), ring(ro, x0)], closed=True), mat, bone, bev(x1 - x0))


def project(tube, u, x, extra, back=False):
    """Surface point (front or back half of a vertical tube whose v=90 deg is +X) at Unity x."""
    lo, hi = (math.pi / 2, 3 * math.pi / 2) if back else (-math.pi / 2, math.pi / 2)
    sign = -1 if back else 1
    for _ in range(40):
        mid = (lo + hi) / 2
        if sign * (tube.at(u, mid, extra)[0] - x) < 0: lo = mid
        else: hi = mid
    return tube.at(u, (lo + hi) / 2, extra)


def resample(outline, k):
    pts = []
    for i, a in enumerate(outline):
        b = outline[(i + 1) % len(outline)]
        pts += [(a[0] + (b[0] - a[0]) * j / k, a[1] + (b[1] - a[1]) * j / k) for j in range(k)]
    return pts


def surface_prism(tube, uy, centre, outline, e0, e1, mat, bone, back=False):
    x0, y0 = centre
    get = lambda ox, oy, e: project(tube, uy(y0 + oy), x0 + ox, e, back)
    return Part(*fan_prism([get(ox, oy, e1) for ox, oy in outline], [get(ox, oy, e0) for ox, oy in outline],
                           get(0, 0, e1), get(0, 0, e0)), mat, bone)


def rounded_rect(w, h, r, n=4):
    pts = []
    for cx, cy, a0 in ((w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180),
                       (w / 2 - r, -h / 2 + r, 270)):
        pts += [(cx + r * math.cos(math.radians(a0 + 90 * i / n)), cy + r * math.sin(math.radians(a0 + 90 * i / n)))
                for i in range(n + 1)]
    return pts


def symbol(team, size):
    if team == 1:
        return resample([(0, .577 * size), (-.5 * size, -.289 * size), (.5 * size, -.289 * size)], 6)
    if team == 2:
        h = .37 * size
        return resample([(-h, -h), (h, -h), (h, h), (-h, h)], 5)
    if team == 3:
        h = .50 * size
        return resample([(0, h), (.78 * h, 0), (0, -h), (-.78 * h, 0)], 5)
    r = .41 * size
    return [(r * math.cos(a), r * math.sin(a)) for a in np.linspace(0, math.tau, 24, endpoint=False)]


# ------------------------------------------------------------------ body geometry (Unity metres; right side mirrored)
TORSO = [(.84, .120, .080, .092), (.89, .158, .098, .112), (.96, .168, .104, .118), (1.03, .158, .100, .108),
         (1.10, .148, .097, .098), (1.18, .156, .106, .099), (1.26, .174, .118, .104), (1.34, .186, .124, .108),
         (1.41, .186, .118, .107), (1.46, .168, .100, .098), (1.50, .118, .074, .074), (1.53, .072, .058, .058)]
HELM = [(1.540, .086, .096, .084), (1.560, .099, .110, .098), (1.590, .109, .121, .109), (1.630, .115, .127, .116),
        (1.670, .117, .128, .119), (1.700, .1165, .127, .119)]
LEG = [(1.00, .085, .085, .095), (.93, .092, .090, .100), (.84, .088, .084, .094), (.72, .080, .074, .084),
       (.62, .070, .064, .070), (.54, .060, .060, .058), (.48, .056, .056, .060), (.38, .054, .050, .066),
       (.27, .048, .044, .056), (.16, .040, .038, .042), (.06, .038, .036, .040)]
ARM = [(.10, .064, .066), (.17, .060, .062), (.24, .054, .056), (.33, .050, .050), (.43, .044, .044),
       (.47, .044, .043), (.54, .045, .042), (.63, .039, .036), (.70, .032, .030), (.735, .030, .028)]
BADGE = (0, 1.35)                 # chest badge centre (x, y) on the torso surface
COMMS = (.1195, 1.640, -.015)     # helmet comms disc: face x, centre y, centre z (right side; mirrored)
FING = {"Index": (.0205, .0188), "Middle": (.0215, .0194), "Ring": (.0205, .0186), "Little": (.0180, .0168),
        "Thumb": (.0215, .0196)}   # vibegame1 FINGERS width/thickness (real glove scale)
rad = math.radians


def table_interp(rows, y):
    ys = [r[0] for r in rows]
    if ys[0] > ys[-1]:
        rows = rows[::-1]; ys = ys[::-1]
    return [float(np.interp(y, ys, [r[k] for r in rows])) for k in range(1, len(rows[0]))]


def torso_tube():
    y0, y1 = TORSO[0][0], TORSO[-1][0]
    t = Tube([(0, y, -.01) for y in np.linspace(y0, y1, 6)], (0, 0, 1))

    def radius(u, v):
        w, f, b = table_interp(TORSO, y0 + (y1 - y0) * u)
        return superellipse(f if math.cos(v) > 0 else b, w, v, 2.25)
    t.radius = radius
    t.uy = lambda y: (y - y0) / (y1 - y0)
    return t


def helmet_tube():
    y0, y1 = 1.54, 1.786
    t = Tube([(0, y0, .012), (0, y1, .0)], (0, 0, 1))

    def radius(u, v):
        y = y0 + (y1 - y0) * u
        if y <= 1.70:
            w, f, b = table_interp(HELM, y)
        else:
            s = math.sqrt(max(0.0, 1 - ((y - 1.70) / .087) ** 2))
            w, f, b = (x * s for x in HELM[-1][1:])
        return superellipse(f if math.cos(v) > 0 else b, w, v, 2.15)
    t.radius = radius
    t.uy = lambda y: (y - y0) / (y1 - y0)
    return t


def torso_weight(p):
    x, y = p[0], p[1]
    w = chain(y, ["Hips", "Spine", "Chest", "UpperChest", "Neck"], [1.06, 1.21, 1.35, 1.505], .045)
    side, ax = ("Right" if x > 0 else "Left"), abs(x)
    if ax > .02:
        for bone, k in ((side + "Shoulder", .7 * ramp(ax, .12, .04) * ramp(y, 1.40, .04)),
                        (side + "UpperLeg", .6 * ramp(-y, -.915, .04) * ramp(ax, .075, .03))):
            if k > 1e-3:
                w = {b: v * (1 - k) for b, v in w.items()}; w[bone] = w.get(bone, 0) + k
    return w


def pouch(T, v_deg, y, half):
    """Belt pouch (Hips): dark fabric body with a graphite flap, facing out of the belt at angle v."""
    v = rad(v_deg)
    n, t, up = np.array([math.sin(v), 0, math.cos(v)]), np.array([math.cos(v), 0, -math.sin(v)]), np.array([0, 1.0, 0])
    w, h, d = half; base = T.at(T.uy(y), v, .023); frame = (t, up, n)
    return [rbox(base + n * d, (w, h, d), SUIT, "Hips", 12, 5, frame),
            rbox(base + n * (d + .003) + up * (h * .6), (w * 1.1, h * .45, d + .002), TRIM, "Hips", 12, 5, frame)]


def backpack():
    """Power pack (UpperChest): graphite core, ivory caps, side canisters, vents, accent strip and glow rails."""
    c = [rbox((0, 1.335, -.170), (.098, .108, .030), TRIM, "UpperChest"),
         rbox((0, 1.446, -.170), (.104, .012, .034), ARMOR, "UpperChest", 12),
         rbox((0, 1.224, -.170), (.100, .010, .032), ARMOR, "UpperChest", 12),
         rbox((0, 1.428, -.2005), (.080, .0055, .003), ACCENT, "UpperChest", 8)]
    s = [sweep([(.118, 1.245, -.172), (.118, 1.425, -.172)], .020, ARMOR, "UpperChest")]
    if Q["mid"]:
        s += [sweep([(.118, y, -.172), (.118, y + .012, -.172)], .0228, TRIM, "UpperChest") for y in (1.262, 1.396)]
        s.append(rbox((.088, 1.33, -.2005), (.004, .065, .003), GLOW, "UpperChest", 8))
    if Q["fine"]:
        c += [rbox((0, y, -.2015), (.06, .003, .0025), SUIT, "UpperChest", 8) for y in (1.240, 1.250)]
    return c, s


def torso_parts(T):
    """(centre parts, right-side parts to mirror) of the trunk, all on trunk bones."""
    uy, c, s = T.uy, [], []
    v, f = skin(T, np.linspace(0, 1, nn(26, 8)), nn(28, 10))
    c.append(Part(v, f, SUIT, [torso_weight(p) for p in v], hard=False))
    N = Tube([(0, 1.45, -.01), (0, 1.64, .006)], (0, 0, 1),
             lambda u, v_: superellipse(.057 - .006 * u, .062 - .010 * u, v_, 2.2))
    v, f = skin(N, np.linspace(0, 1, nn(8, 3)), nn(16, 8))
    c.append(Part(v, f, SUIT, [chain(p[1], ["UpperChest", "Neck", "Head"], [1.49, 1.585], .025) for p in v], hard=False))
    # Chest: a contoured cuirass. Pectoral plates bulge over each pec and overhang a U-shaped lower edge; a sternum
    # keel rises between the rib plates; the team badge stands proud on the centre line; accent yoke and collar above.
    pec = lambda s_, t: .003 + .017 * math.sin(math.pi * min(1.0, .15 + s_)) ** .7 * math.exp(-((t - .3) / .32) ** 2)
    s.append(shell(T, lambda a: uy(1.235 + .05 * edge(a, 0, rad(100)) ** 1.5), lambda a: uy(1.432 - .016 * edge(a, 0, rad(100))),
                   rad(4), rad(100), .013, .012, ARMOR, "UpperChest", 10, 14, pec))
    s.append(shell(T, lambda a: uy(1.418 - .016 * edge(a, 0, rad(84))), uy(1.47), rad(4), rad(84), .026, .009, ACCENT,
                   "UpperChest", 3, 8, pillow(.002)))
    s.append(shell(T, lambda a: uy(1.165 + .02 * edge(a, 0, rad(104))), lambda a: uy(1.272 + .035 * edge(a, 0, rad(104))),
                   rad(6), rad(104), .004, .008, ARMOR, "Chest", 4, 10, pillow(.006, .5)))
    s.append(shell(T, uy(1.18), uy(1.30), rad(92), rad(120), .013, .009, ACCENT, "Chest", 4, 4))
    s.append(shell(T, uy(1.40), uy(1.505), rad(58), rad(124), .030, .008, TRIM, "UpperChest", 3, 7, pillow(.002)))
    c.append(shell(T, uy(1.17), uy(1.30), rad(-9), rad(9), .006, .010, ARMOR, "Chest", 4, 5,
                   lambda s_, t: .006 + .008 * ridge(t, .3)))
    c.append(shell(T, uy(1.462), uy(1.53), 0, 0, .014, .012, TRIM, "UpperChest", 3, 28, closed=True))
    c.append(surface_prism(T, uy, BADGE, rounded_rect(.18, .17, .024), .020, .050, TRIM, "UpperChest"))
    # Abdomen: three rows of dark segmented plates (split on the centre line) between ivory flanks; kidney plate behind.
    for y0, y1 in ((1.122, 1.162), (1.077, 1.117), (1.032, 1.072)) if Q["mid"] else ((1.032, 1.162),):
        s.append(shell(T, uy(y0), uy(y1), rad(5), rad(40), .007, .009, TRIM, "Spine", 5, 7, pillow(.006, .5)))
    s.append(shell(T, uy(1.045), uy(1.165), rad(46), rad(80), .005, .008, ARMOR, "Spine", 4, 5, pillow(.004, .5)))
    c.append(shell(T, uy(1.035), uy(1.135), rad(120), rad(240), .006, .009, TRIM, "Spine", 3, 8))
    # Pelvis: belt, cod and seat plates, pouches.
    c.append(shell(T, uy(.955), uy(1.025), 0, 0, .010, .014, TRIM, "Hips", 3, 28, closed=True))
    c.append(shell(T, uy(.85), uy(.95), rad(-26), rad(26), .010, .010, TRIM, "Hips", 4, 6, pillow(.004)))
    c.append(shell(T, uy(.87), uy(.95), rad(146), rad(214), .010, .010, TRIM, "Hips", 3, 6, pillow(.004)))
    # Back: contoured plates, lower back, power pack carrying the back symbol.
    s.append(shell(T, lambda a: uy(1.25 + .035 * edge(a, math.pi, rad(84))), uy(1.45), rad(96), rad(178), .013, .012,
                   ARMOR, "UpperChest", 6, 9, pillow(.008, .45)))
    s.append(shell(T, uy(1.14), lambda a: uy(1.265 + .035 * edge(a, math.pi, rad(80))), rad(100), rad(178), .004, .008,
                   ARMOR, "Chest", 4, 7, pillow(.004)))
    bc, bs = backpack(); c += bc; s += bs
    if Q["mid"]:
        c.append(surface_prism(T, uy, (0, .99), rounded_rect(.075, .05, .01), .022, .030, ACCENT, "Hips"))
        s.append(shell(T, uy(1.20), uy(1.29), rad(104.5), rad(107.5), .018, .007, GLOW, "Chest", 3, 2))
        s += pouch(T, 86, .99, (.026, .030, .013)) + pouch(T, 136, .99, (.024, .028, .012))
    if Q["fine"]:
        c.append(surface_prism(T, uy, (0, 1.24), rounded_rect(.02, .012, .004), .028, .036, GLOW, "Chest"))
        s += [seam(T, uy(1.03), uy(1.24), rad(a), torso_weight) for a in (86, 130)]
    return c, s


def helmet_parts(H):
    uy, c, s = H.uy, [], []
    ys = [1.54, 1.55, 1.565, 1.585, 1.605, 1.625, 1.645, 1.665, 1.685, 1.70, 1.715, 1.73, 1.745, 1.758, 1.768,
          1.776, 1.782, 1.786]
    ys = ys if Q["fine"] else ys[::2] + [ys[-1]] if Q["mid"] else [1.54, 1.585, 1.645, 1.70, 1.745, 1.776, 1.786]
    c.append(Part(*skin(H, [uy(y) for y in ys], nn(28, 10)), ARMOR, "Head"))
    # Wraparound faceted visor: tall and V-keeled in the centre, narrowing round to the comms housings (12 deg facets).
    vtop = lambda a: 1.704 - .014 * max(0.0, 1 - abs(a) / rad(60))
    vbot = lambda a: 1.612 + .030 * (abs(a) / rad(85)) ** 1.5
    c.append(shell(H, lambda a: uy(vbot(a)), lambda a: uy(vtop(a)), rad(-78), rad(78), .002, .006, VISOR, "Head", 4, 15,
                   lambda s_, t: .003 + .010 * (1 - abs(2 * t - 1)) + .003 * math.sin(math.pi * s_), hard=12))
    # Brow ridge overhanging the visor, deepest over the centre.
    c.append(shell(H, lambda a: uy(vtop(a) - .006), lambda a: uy(1.742 - .008 * edge(a, 0, rad(105))), rad(-105), rad(105),
                   .004, .010, ARMOR, "Head", 5, 20,
                   lambda s_, t: .003 + .015 * (1 - s_) ** 2 * (.45 + .55 * (1 - abs(2 * t - 1))) + .003 * ridge(t, .05)))
    # Graphite V mouth guard under the visor, flanked by flared ivory cheek guards.
    c.append(shell(H, uy(1.548), lambda a: uy(vbot(a) + .004), rad(-32), rad(32), .004, .010, TRIM, "Head", 4, 6,
                   lambda s_, t: .009 * (1 - abs(2 * t - 1)) + .004 * s_, hard=20))
    s.append(shell(H, uy(1.552), lambda a: uy(vbot(a) + .004), rad(24), rad(72), .004, .010, ARMOR, "Head", 5, 8,
                   lambda s_, t: .002 + .012 * (1 - t) ** 2 + .005 * (1 - s_) ** 2))
    c.append(shell(H, uy(1.545), uy(1.60), rad(125), rad(235), .004, .008, TRIM, "Head", 3, 8,
                   lambda s_, t: .006 * (1 - s_) ** 2))
    # Crest: an accent ribbon over the crown, brow to nape (reads front, back and above).
    top = np.array([0, 1.70, .006])
    path = [H.at(uy(y), 0) for y in (1.722, 1.735, 1.748, 1.76, 1.77, 1.778, 1.783, 1.786)]
    path += [H.at(uy(y), math.pi) for y in (1.786, 1.783, 1.778, 1.77, 1.76, 1.748, 1.735, 1.72, 1.705, 1.69)]
    if not Q["mid"]:
        path = path[::2] + [path[-1]]
    rings = []
    for p in path:
        n = unit(p - top); x = np.array([1.0, 0, 0]) * .015
        rings.append([p - x - n * .003, p + x - n * .003, p + x + n * .004, p - x + n * .004])
    c.append(Part(*loft(rings), ACCENT, "Head", bev(.007)))
    # Comms housings: a recessed graphite disc (carrying the side team mark) inside a raised ivory rim, set into the shell.
    hx, hy, hz = COMMS
    s.append(xcyl([(.100, .037), (hx, .037)], hy, hz, TRIM, "Head"))
    if Q["mid"]:
        s.append(washer(.106, hx + .0075, .036, .046, hy, hz, ARMOR, "Head"))
        s.append(shell(H, uy(1.66), uy(1.685), rad(120), rad(134), .002, .004, GLOW, "Head", 2, 3))
    return c, s


def arm_tube(x0):
    t = Tube([(x0, 1.47, -.01), (.735, 1.47, -.01)], (0, 1, 0))

    def radius(u, v):
        w, h = table_interp(ARM, x0 + (.735 - x0) * u)
        return superellipse(h, w, v, 2.2)
    t.radius = radius
    t.ux = lambda x: (x - x0) / (.735 - x0)
    return t


def digit_segment(bone, a, b, wd, tk, k, h):
    """One phalanx: a rigid rounded segment whose ends are domes centred on its joints (so curling never gaps),
    plated on the back like vibegame1's digit()/digit_lames()/knuckle_plate()."""
    taper = .95 ** k
    r, hw = tk / 2 * taper, wd * .46 * taper
    d = unit(b - a)
    tube = Tube([a - d * r, b + d * r], (0, 1, 0)); d0 = r / tube.length
    tube.radius = lambda u, v: superellipse(r * (1 if math.cos(v) > 0 else .9), hw, v, 2.3) * dome_factor(u, d0, 1 - d0, .12)
    parts = [Part(*skin(tube, stations(0, 1, 2, d0, d0, h["kd"]), h["nv"]), SUIT, bone, hard=False)]
    if h["lame"]:
        nu, nv = h["lame"]
        lame = lambda s, t: .0007 * ridge(t, .14) + .0010 * s ** 3 + .0004 * (1 - s)
        parts.append(Part(*grid_slab(lambda s, t, o: tube.at(d0 + .05 + (1 - 2 * d0 - .08) * s, rad(-76 + 152 * t), .0009 + o),
                                     nu, nv, .0018, lame), TRIM, bone))
    if h["cop"] and k == 0:
        cop = lambda s, t: .0024 * math.sin(math.pi * s) * (.4 + .6 * ridge(t, .22))
        parts.append(Part(*grid_slab(lambda s, t, o: tube.at(max(.0, d0 - .14) + .30 * s, rad(-68 + 136 * t), .0014 + o),
                                     3, 6, .0018, cop), ARMOR, bone))
    return parts


def gauntlet(mode="full"):
    """Right gauntlet on the skeleton's straight T-pose hand (palm -Y, thumb +Z)."""
    h = HAND[mode]
    J = {n: (np.array(hd, float), np.array(tl, float)) for n, _, hd, tl in sk.bones()}
    wrist, knuck = np.array((.700, 1.467, -.008)), np.array((.800, 1.4665, -.0085))
    palm = Tube([wrist + (knuck - wrist) * f for f in np.linspace(-.05, 1.0, 9)], (0, 1, 0))

    def palm_r(u, v):
        half_w = np.interp(u, [0, .35, .8, 1], [.030, .037, .044, .044])
        half_t = np.interp(u, [0, .3, 1], [.0165, .0175, .0150])
        back = math.cos(v) > 0
        return superellipse(half_t * (1.0 if back else .92), half_w, v, 2.8 if back else 3.4) * dome_factor(u, .06, .93, .3)
    palm.radius = palm_r
    sides = h["palm"] if Q["mid"] else 6
    parts = [Part(*skin(palm, stations(0, 1, max(3, h["palm"] // 2), .06, .07, 2 if mode == "full" else 1), sides),
                  SUIT, "RightHand", hard=False)]
    lames = {3: ((.16, .42), (.38, .64), (.60, .84)), 2: ((.16, .50), (.46, .84)), 1: ((.16, .84),)}[h["lames"]]
    for a, b in (lames if Q["mid"] else ()):
        parts.append(shell(palm, a, b, -1.25, 1.25, .0015, .0026, ARMOR, "RightHand", 4, 10,
                           lambda s, t: .0014 * ridge(t, .16) + .0012 * s ** 3))
    parts.append(shell(palm, .80, .97, -1.35, 1.35, .002, .003, TRIM, "RightHand", 5, 12,
                       lambda s, t: .004 * math.sin(math.pi * s) ** .7))
    for digit in DIGITS:
        wd, tk = FING[digit]
        if mode == "low":   # one tube per digit, each ring on its phalanx bone
            names = [f"Right{digit}{s}" for s in SEGS]
            pts = [J[n][0] for n in names] + [J[names[-1]][1]]
            tube = Tube(pts, (0, 1, 0), lambda u, v, tk=tk, wd=wd: superellipse(tk / 2, wd * .46, v, 2.2))
            us = list(tube.u)
            sides = h["nv"] if Q["mid"] else 4
            verts, faces = skin(tube, us, sides)
            weights = [{names[min(2, i // sides)]: 1.0} for i in range(sides * len(us))] + [{names[0]: 1.0}, {names[2]: 1.0}]
            parts.append(Part(verts, faces, SUIT, weights, hard=False))
            continue
        for k, seg in enumerate(SEGS):
            bone = f"Right{digit}{seg}"
            parts += digit_segment(bone, *J[bone], wd, tk, k, h)
    return parts


def arm_parts(fp=False, hand="full"):
    x0 = .16 if fp else .10
    A = arm_tube(x0); ux = A.ux
    bones = ((["RightUpperArm", "RightLowerArm", "RightHand"], [.45, .712], [.035, .012]) if fp else
             (["RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand"], [.165, .45, .712], [.04, .035, .012]))
    weight = lambda p: chain(p[0], *bones)
    v, f = skin(A, np.linspace(0, 1, nn(30 if fp else 26, 7)), nn(16, 6))
    parts = [Part(v, f, SUIT, [weight(p) for p in v], hard=False)]
    if not fp:   # domed ellipsoid pauldron in team accent, a graphite rim over its lower edge, ivory lame under it
        centre = (.175, 1.472, -.01)
        parts.append(cap(centre, (1, 0, 0), (0, 1, 0), (.098, .084), 46, 120, -132, 132, .012, ACCENT, "RightUpperArm",
                         7, 16, pillow(.006, .5)))
        parts.append(cap(centre, (1, 0, 0), (0, 1, 0), (.108, .094), 44, 52, -134, 134, .005, TRIM, "RightUpperArm", 2, 16))
        parts.append(shell(A, ux(.212), ux(.272), rad(-112), rad(112), .004, .008, ARMOR, "RightUpperArm", 3, 10,
                           pillow(.003)))
    if Q["mid"]:
        parts.append(shell(A, ux(.262), ux(.33), rad(-108), rad(108), .006, .008, ARMOR, "RightUpperArm", 3, 10,
                           pillow(.003)))
    parts.append(shell(A, ux(.32), ux(.415), rad(-100), rad(100), .006, .010, ARMOR, "RightUpperArm", 4, 10,
                       pillow(.005, .5)))
    parts.append(shell(A, ux(.41), ux(.495), rad(-170), rad(-10), .008, .010, TRIM, "RightLowerArm", 5, 9,
                       pillow(.012, .5)))
    # Forearm: two overlapping bracer plates (the wrist plate over the elbow plate), accent stripe, glow rail, cuff.
    parts.append(shell(A, ux(.495), ux(.595), rad(-128), rad(128), .006, .010, ARMOR, "RightLowerArm", 4, 13,
                       pillow(.004, .5)))
    parts.append(shell(A, ux(.585), ux(.69), rad(-132), rad(132), .010, .010, ARMOR, "RightLowerArm", 5, 13,
                       pillow(.004, .5, extra=lambda s, t: .005 * s ** 3)))
    parts.append(shell(A, ux(.60), ux(.675), rad(-13), rad(13), .022, .005, ACCENT, "RightLowerArm", 4, 3))
    parts.append(shell(A, ux(.69), ux(.718), 0, 0, .010, .007, TRIM, "RightLowerArm", 3, 20,
                       lambda s, t: .003 * s, closed=True))
    if Q["mid"]:
        parts.append(shell(A, ux(.51), ux(.58), rad(64), rad(70), .0155, .004, GLOW, "RightLowerArm", 3, 2))
    if Q["fine"]:
        parts.append(seam(A, ux(.20), ux(.68), math.pi, weight))
    return parts + gauntlet(hand)


def leg_parts():
    L = Tube([(.092, 1.00, 0), (.095, .95, 0), (.095, .52, .005), (.095, .09, -.02), (.095, .06, -.021)], (0, 0, 1))

    def radius(u, v):
        w, f, b = table_interp(LEG, L.coord(u, 1))
        return superellipse(f if math.cos(v) > 0 else b, w, v, 2.2)
    L.radius = radius
    uy = lambda y: L.u_of(1, y)
    weight = lambda p: chain(-p[1], ["Hips", "RightUpperLeg", "RightLowerLeg", "RightFoot"], [-.925, -.52, -.10],
                             [.045, .035, .025])
    v, f = skin(L, np.linspace(0, 1, nn(34, 9)), nn(16, 6))
    parts = [Part(v, f, SUIT, [weight(p) for p in v], hard=False)]
    # On this tube v = -90 deg is the outer side (+X), +90 the inner side.
    parts.append(shell(L, lambda a: uy(.875 + .025 * max(0.0, -a) / rad(58)), lambda a: uy(.60 + .025 * edge(a, rad(4), rad(62)) ** 2),
                       rad(-58), rad(66), .008, .011, ARMOR, "RightUpperLeg", 7, 10, pillow(.008, .45)))
    parts.append(shell(L, uy(.945), lambda a: uy(.66 + .04 * edge(a, rad(-99), rad(39)) ** 2), rad(-138), rad(-60), .021, .010,
                       ACCENT, "RightUpperLeg", 6, 7, pillow(.005)))
    parts.append(shell(L, lambda a: uy(.595 - .03 * edge(a, 0, rad(70)) ** 2), lambda a: uy(.455 + .03 * edge(a, 0, rad(70)) ** 2),
                       rad(-70), rad(70), .016, .012, ARMOR, "RightLowerLeg", 6, 9, pillow(.016, .55)))
    parts.append(shell(L, lambda a: uy(.468 - .07 * edge(a, 0, rad(104)) ** 1.4), uy(.165), rad(-104), rad(104), .004, .010,
                       ARMOR, "RightLowerLeg", 8, 11,
                       pillow(.005, extra=lambda s, t: .004 * ridge(t, .08) + .006 * s ** 3)))
    parts.append(shell(L, uy(.165), uy(.10), 0, 0, .010, .010, TRIM, "RightLowerLeg", 3, 18, closed=True))
    parts.append(shell(L, uy(.885), uy(.635), rad(128), rad(232), .006, .009, ARMOR, "RightUpperLeg", 5, 7, pillow(.005)))
    parts.append(shell(L, uy(.43), uy(.21), rad(122), rad(238), .006, .009, ARMOR, "RightLowerLeg", 5, 8, pillow(.006, .5)))
    if Q["mid"]:
        parts.append(shell(L, uy(.36), uy(.24), rad(-97), rad(-92), .0135, .005, GLOW, "RightLowerLeg", 3, 2))
    if Q["fine"]:
        parts += [seam(L, uy(.90), uy(.58), rad(a), weight) for a in (150, 210)]
        parts.append(seam(L, uy(.86), uy(.20), rad(90), weight, 16))
    # Boot: graphite shoe on Foot, ivory toe cap on Toes, heel guard, dark soles; superellipse sections along Z.
    def boot(rows, bottom, mat, bone, p=3.0, n=16):
        rings = []
        for z, hw, top in rows:
            hh, yc = (top - bottom) / 2, (top + bottom) / 2
            rings.append([(.096 + hw * sg(math.cos(a)) * abs(math.cos(a)) ** (2 / p),
                           yc + hh * sg(math.sin(a)) * abs(math.sin(a)) ** (2 / p), z)
                          for a in np.linspace(0, math.tau, nn(n, 6), endpoint=False)])
        return Part(*loft(rings), mat, bone, bev(.02))
    parts.append(boot([(-.088, .030, .070), (-.080, .042, .095), (-.055, .050, .112), (-.02, .052, .118),
                       (.03, .053, .105), (.08, .054, .085), (.125, .052, .068)], .022, TRIM, "RightFoot"))
    parts.append(boot([(.108, .056, .075), (.14, .055, .067), (.175, .050, .057), (.20, .041, .046),
                       (.214, .027, .035)], .020, ARMOR, "RightToes"))
    parts.append(boot([(-.092, .034, .024), (-.084, .046, .024), (.03, .057, .024), (.122, .056, .024)], .001, SUIT,
                      "RightFoot", 6.0))
    parts.append(boot([(.118, .058, .024), (.16, .056, .024), (.20, .046, .024), (.219, .029, .024)], .001, SUIT,
                      "RightToes", 6.0))
    parts.append(rbox((.096, .062, -.092), (.030, .030, .008), ARMOR, "RightFoot", 12))
    return parts


def body_parts(hand):
    T, H = torso_tube(), helmet_tube()
    c, s = torso_parts(T)
    hc, hs = helmet_parts(H)
    side = s + hs + arm_parts(hand=hand) + leg_parts()
    return c + hc + side + [mirror(p) for p in side]


def team_marks(team):
    T = torso_tube()
    def fitted(w, h):
        o = np.asarray(symbol(team, 1)); low, high = o.min(axis=0), o.max(axis=0)
        return (o - (low + high) / 2) * min(w / (high[0] - low[0]), h / (high[1] - low[1]))
    # Fill the existing graphite plates, remaining inside their rounded edges; 1 mm proud.
    front = surface_prism(T, T.uy, BADGE, fitted(.162, .146), .0502, .051, MARK, "UpperChest")
    zf, yb, outline = -.2005, 1.335, fitted(.154, .174)
    back = Part(*fan_prism([(x, yb + y, zf - .0008) for x, y in outline], [(x, yb + y, zf) for x, y in outline],
                           (0, yb, zf - .0008), (0, yb, zf)), MARK, "UpperChest")
    hx, hy, hz = COMMS
    o = symbol(team, .055)   # viewed from +X, image right is -Z; every symbol is left-right symmetric
    helm = Part(*fan_prism([(hx + .003, hy + y, hz - x) for x, y in o], [(hx - .001, hy + y, hz - x) for x, y in o],
                           (hx + .003, hy, hz), (hx - .001, hy, hz)), MARK, "Head")
    # Clip decals against the actual bevelled pauldron facets: no curved fan can cut through the shell.
    # Everything stays inside each team's existing mesh, preserving Unity's exact-name activation.
    plate = cap((.175, 1.472, -.01), (1, 0, 0), (0, 1, 0), (.098, .084),
                46, 120, -132, 132, .012, ACCENT, "RightUpperArm", 7, 16, pillow(.006, .5))
    projection = build_object("_MarkProjection", [plate], {m.name: m for m in bpy.data.materials})
    mesh = projection.data; mesh.calc_loop_triangles()
    facets = []
    for tri in mesh.loop_triangles:
        pts = np.array([U(mesh.vertices[i].co) for i in tri.vertices])
        # Coordinate conversion is a reflection; orient by the actual Blender face normal.
        normal = np.asarray(U(tri.normal))
        if normal[1] > .25:
            facets.append((pts, normal))
    bpy.data.objects.remove(projection, do_unlink=True); bpy.data.meshes.remove(mesh)

    def shoulder(outline, material, offset):
        assert 0 < offset < .005
        outline = np.asarray(outline)
        area = sum(outline[i, 0]*outline[(i+1) % len(outline), 1] - outline[i, 1]*outline[(i+1) % len(outline), 0]
                   for i in range(len(outline)))
        if area < 0:
            outline = outline[::-1]
        def xy(p):
            return np.array((-p[2] - .01, .19 - p[0]))
        verts, faces = [], []
        for triangle, normal in facets:
            polygon = list(triangle)
            for i, a in enumerate(outline):
                b = outline[(i+1) % len(outline)]; edge = b-a
                def distance(p):
                    d = xy(p)-a
                    return edge[0]*d[1] - edge[1]*d[0]
                clipped = []
                for j, p in enumerate(polygon):
                    q = polygon[(j+1) % len(polygon)]; dp, dq = distance(p), distance(q)
                    if dp >= -1e-10:
                        clipped.append(p)
                    if (dp >= 0) != (dq >= 0):
                        clipped.append(p + (q-p) * dp/(dp-dq))
                polygon = clipped
                if len(polygon) < 3:
                    break
            if len(polygon) >= 3:
                base = len(verts); verts += [p + offset * normal for p in polygon]
                faces += [(base, base+j, base+j+1) for j in range(1, len(polygon)-1)]
        assert faces, (team, material)
        return Part(verts, faces, material, "RightUpperArm", hard=False)

    backing = shoulder(rounded_rect(.142, .108, .012), VISOR, .0006)
    shoulder_mark = shoulder(fitted(.126, .094), MARK, .0012)
    pieces = [front, back, helm, mirror(helm), backing, mirror(backing), shoulder_mark, mirror(shoulder_mark)]
    if team == 4:
        # A hollow circle keeps the circle/square distinction after minification to a handful of pixels.
        hole = np.asarray(symbol(4, .075))
        pieces.append(surface_prism(T, T.uy, BADGE, hole, .0511, .0518, VISOR, "UpperChest"))
        pieces.append(Part(*fan_prism([(x, yb+y, zf-.0016) for x, y in hole],
                                     [(x, yb+y, zf-.0009) for x, y in hole],
                                     (0, yb, zf-.0016), (0, yb, zf-.0009)), VISOR, "UpperChest"))
        ring_hole = shoulder(symbol(4, .052), VISOR, .0018)
        pieces += [ring_hole, mirror(ring_hole)]
    return pieces


# ------------------------------------------------------------------ grip fit (pure numpy; preview evidence + manifest)
GRIP_R, GRIP_SPAN = .0175, (-.075, .045)   # 3.5 cm grip along the knuckle line, about the hand socket


def rotation(axis, deg):
    a = unit(axis); c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    K = np.array([[0, -a[2], a[1]], [a[2], 0, -a[0]], [-a[1], a[0], 0]])
    return np.eye(3) * c + (1 - c) * np.outer(a, a) + s * K


def seg_dist(p1, q1, p2, q2):
    d1, d2, r = q1 - p1, q2 - p2, p1 - p2
    a, e, f, c, b = d1 @ d1, d2 @ d2, d2 @ r, d1 @ r, d1 @ d2
    den = a * e - b * b
    s = float(np.clip((b * f - c * e) / den, 0, 1)) if den > 1e-12 else 0.0
    t = (b * s + f) / e
    if t < 0: t, s = 0.0, float(np.clip(-c / a, 0, 1))
    elif t > 1: t, s = 1.0, float(np.clip((b - c) / a, 0, 1))
    return float(np.linalg.norm(p1 + d1 * s - p2 - d2 * t))


def finger_hinge(side, digit):
    """h = cross(segmentDir, palmNormal -Y) at bind; +deg about h curls toward the palm. Left mirrors the direction."""
    d = unit(np.subtract(sk._THUMB[1], sk._THUMB[0])) if digit == "Thumb" else np.array([1.0, 0, 0])
    if side == "Left":
        d = d * [-1, 1, 1]
    return tuple(float(x) for x in np.cross(d, (0, -1, 0)))


# Thumb's extra axes on ThumbProximal (no new bone): spread swings it forward in the palm plane (toward the fingers),
# opposition rolls it under the palm. Unity world axes for +deg; the left hand mirrors (reflection negates the angle).
THUMB_AXES = {"Right": ((0.0, 1.0, 0.0), (1.0, 0.0, 0.0)), "Left": ((0.0, -1.0, 0.0), (1.0, 0.0, 0.0))}


def thumb_rotation(side, spread, oppose):
    a, b = THUMB_AXES[side]
    return rotation(b, oppose) @ rotation(a, spread)


@functools.lru_cache(maxsize=1)
def grip_solve(margin=.0008):
    """Right hand round a 3.5 cm grip on WeaponHand_R's axis: greedy curl per phalanx (0.5 deg steps) until its capsule
    would touch the grip. The thumb also searches spread x opposition (5 deg steps), must clear the curled index
    finger, and keeps the pose hugging the grip closest. Returns angles and minimum clearance (mm); palm not checked."""
    J = {n: (np.array(h, float), np.array(t, float)) for n, _, h, t in sk.bones()}
    c = np.array({s[0]: s for s in sk.SOCKETS}["WeaponHand_R"][2], float)
    g0, g1 = c + [0, 0, GRIP_SPAN[0]], c + [0, 0, GRIP_SPAN[1]]

    def points(digit, angles, r0):
        names, hinge = [f"Right{digit}{s}" for s in SEGS], finger_hinge("Right", digit)
        pts, phi = [J[names[0]][0]], 0.0
        for k, n in enumerate(names):
            phi += angles[k] if k < len(angles) else 0.0
            pts.append(pts[-1] + r0 @ rotation(hinge, phi) @ (J[n][1] - J[n][0]))
        return pts

    def fit(digit, r0=np.eye(3), avoid=()):
        tk = FING[digit][1]
        limits = (40, 60, 70) if digit == "Thumb" else (100, 105, 90)

        def gaps(angles):
            pts = points(digit, angles, r0)
            grip = [seg_dist(pts[k], pts[k + 1], g0, g1) - GRIP_R - tk / 2 * .95 ** k for k in range(3)]
            near = [min([grip[k]] + [seg_dist(pts[k], pts[k + 1], a, b) - ra - tk / 2 * .95 ** k for a, b, ra in avoid])
                    for k in range(3)]
            return grip, near
        if min(gaps([])[1]) < margin:
            return None
        angles = []
        for k in range(3):
            best = 0.0
            for th in np.arange(0, limits[k] + .01, .5):
                if min(gaps(angles + [th])[1][k:]) < margin:
                    break
                best = float(th)
            angles.append(best)
        grip, near = gaps(angles)
        return {"deg": angles, "clearance_mm": round(min(near) * 1000, 2), "wrap_mm": round(sum(grip) * 1000, 1)}
    out = {d: fit(d) for d in DIGITS[:4]}
    ip = points("Index", out["Index"]["deg"], np.eye(3))
    avoid = [(ip[k], ip[k + 1], FING["Index"][1] / 2 * .95 ** k) for k in range(3)]
    best = None
    for spread in range(0, 50, 5):
        for oppose in range(0, 95, 5):
            r = fit("Thumb", thumb_rotation("Right", spread, oppose), avoid)
            if r and (best is None or r["wrap_mm"] < best["wrap_mm"]):
                best = dict(r, spread_deg=spread, opposition_deg=oppose)
    out["Thumb"] = best
    return out


# ------------------------------------------------------------------ Blender assembly
def clean():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0


def srgb(c):
    return tuple(x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c)


def materials():
    mats = {}
    for name, (col, metal, rough) in MATERIALS.items():
        m = bpy.data.materials.new(name); m.use_nodes = True
        bsdf = m.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = (*srgb(col), 1); m.diffuse_color = (*srgb(col), 1)
        bsdf.inputs["Metallic"].default_value = metal; bsdf.inputs["Roughness"].default_value = rough
        if name == GLOW:
            bsdf.inputs["Emission Color"].default_value = (*srgb(TEAMS[1]), 1)
            bsdf.inputs["Emission Strength"].default_value = 4.0
        mats[name] = m
    return mats


def dedupe(p):
    _, first, inv = np.unique(np.round(p.v, 7), axis=0, return_index=True, return_inverse=True)
    inv = np.asarray(inv).reshape(-1)
    faces = []
    for f in p.f:
        g = [int(inv[i]) for i in f]
        h = [x for i, x in enumerate(g) if x != g[i - 1]]
        if len(h) >= 3 and len(set(h)) == len(h):
            faces.append(h)
    return p.v[first], faces, [p.w[i] for i in first]


def build_object(name, parts, mats):
    groups = {}
    for p in parts:
        groups.setdefault((p.bevel, p.hard), []).append(p)
    objs = []
    for (bevel, hard), ps in groups.items():
        verts, faces, fmat, weights, order = [], [], [], [], []
        for p in ps:
            v, f, w = dedupe(p)
            base = len(verts)
            verts += [B(x) for x in v]; faces += [tuple(i + base for i in face) for face in f]; weights += w
            if p.mat not in order:
                order.append(p.mat)
            fmat += [order.index(p.mat)] * len(f)
        me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces)
        for m in order:
            me.materials.append(mats[m])
        me.polygons.foreach_set("material_index", fmat); me.update()
        bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
        me.shade_smooth()
        if hard:
            me.set_sharp_from_angle(angle=math.radians(35 if hard is True else hard))
        obj = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(obj)
        by_bone = {}
        for i, w in enumerate(weights):
            for bone, x in w.items():
                by_bone.setdefault(bone, {}).setdefault(round(x, 6), []).append(i)
        for bone, by in sorted(by_bone.items()):
            g = obj.vertex_groups.new(name=bone)
            for x, idx in by.items():
                g.add(idx, x, "REPLACE")
        if bevel:
            mod = obj.modifiers.new("Bevel", "BEVEL")
            mod.width, mod.segments = bevel
            mod.limit_method = "ANGLE"; mod.angle_limit = math.radians(35); mod.harden_normals = True
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.modifier_apply(modifier=mod.name)
        objs.append(obj)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    obj = objs[0]; obj.name = name; obj.data.name = name
    return obj


def make_rig(name, table):
    data = bpy.data.armatures.new(name); rig = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(rig)
    bpy.ops.object.select_all(action="DESELECT"); rig.select_set(True); bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    for bone, parent, head, tail in table:
        eb = data.edit_bones.new(bone); eb.head = B(head); eb.tail = B(tail); eb.use_connect = False
        d = np.subtract(tail, head)
        eb.align_roll(Vector(B((0, 0, 1) if abs(d[1]) > .7 * np.linalg.norm(d) else (0, 1, 0))))
    for bone, parent, _, _ in table:
        if parent:
            data.edit_bones[bone].parent = data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def bind(obj, rig):
    obj.parent = rig
    obj.modifiers.new("Armature", "ARMATURE").object = rig
    return obj


def socket(rig, name, bone, pos, fwd, up):
    """Empty whose Unity-local +Z = fwd and +Y = up. The FBX roundtrip keeps Blender-local axes as Unity-local
    axes (measured in Unity), so local Z = B(fwd) and local Y = B(up); X completes a proper rotation."""
    f, u = Vector(B(unit(fwd))), Vector(B(unit(up)))
    rot = Matrix([u.cross(f), u, f]).transposed()
    e = bpy.data.objects.new(name, None); bpy.context.collection.objects.link(e)
    e.empty_display_type = "ARROWS"; e.empty_display_size = .05
    e.parent = rig; e.parent_type = "BONE"; e.parent_bone = bone
    bpy.context.view_layer.update()
    e.matrix_world = Matrix.Translation(Vector(B(pos))) @ rot.to_4x4()
    return e


def export(path, objects):
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", add_leaf_bones=False, bake_anim=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", path_mode="AUTO")


def save(path):
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(path), compress=True)


def build_body(root):
    clean(); mats = materials()
    rig = make_rig("SoldierRig", sk.bones())
    objs = [rig]
    for q in range(3):
        set_lod(q)
        objs.append(bind(build_object(f"Soldier_LOD{q}", body_parts("mid" if q == 0 else "low"), mats), rig))
    set_lod(0)
    for team in range(1, 5):
        objs.append(bind(build_object(f"TeamMark{team}", team_marks(team), mats), rig))
    objs += [socket(rig, *s) for s in sk.SOCKETS]
    save(root / "ArtSource" / "Characters" / "Soldier.blend")
    export(root / "Assets" / "Wildstone" / "Art" / "Soldier.fbx", objs)


def arms_table():
    table = [("ArmsRoot", None, (0, 0, 0), (0, .1, 0))]
    for bone, parent, head, tail in sk.bones():
        side = "Right" if bone.startswith("Right") else "Left" if bone.startswith("Left") else None
        rest = bone[len(side):] if side else ""
        if rest in ("UpperArm", "LowerArm", "Hand") or rest.startswith(DIGITS):
            table.append((bone, "ArmsRoot" if rest == "UpperArm" else parent, head, tail))
    return table


def arm_sockets():
    by = {s[0]: s for s in sk.SOCKETS}
    return [(grip, *by[src][1:]) for grip, src in (("Grip_R", "WeaponHand_R"), ("Grip_L", "WeaponHand_L"))]


def build_arms(root):
    clean(); mats = materials(); set_lod(ARMS_Q)
    rig = make_rig("SoldierArmsRig", arms_table())
    side = arm_parts(fp=True)
    objs = [rig, bind(build_object("SoldierArms", side + [mirror(p) for p in side], mats), rig)]
    objs += [socket(rig, *s) for s in arm_sockets()]
    save(root / "ArtSource" / "Characters" / "SoldierArms.blend")
    export(root / "Assets" / "Wildstone" / "Art" / "SoldierArms.fbx", objs)


def counts():
    out = {}
    for q in range(3):
        set_lod(q); clean()
        out[f"Soldier_LOD{q}"] = tris(build_object("c", body_parts("mid" if q == 0 else "low"), materials()))
    set_lod(ARMS_Q); clean(); side = arm_parts(fp=True)
    out["SoldierArms"] = tris(build_object("a", side + [mirror(p) for p in side], materials()))
    return out


# ------------------------------------------------------------------ verification (clean-scene reimport)
def tris(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def verify(path, kind):
    clean()
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    bpy.context.view_layer.update()
    objs = {o.name: o for o in bpy.data.objects}
    rigs = [o for o in objs.values() if o.type == "ARMATURE"]
    assert len(rigs) == 1, rigs
    rig = rigs[0]
    skel = {b[0]: b for b in sk.bones()}
    table = {b[0]: b for b in (sk.bones() if kind == "body" else arms_table())}
    names = {b.name for b in rig.data.bones}
    assert rig.name == ("SoldierRig" if kind == "body" else "SoldierArmsRig"), rig.name
    assert names == set(table), sorted(names ^ set(table))
    assert names - {"ArmsRoot"} <= set(skel)
    for b in rig.data.bones:
        head = U(rig.matrix_world @ b.head_local)
        assert np.allclose(head, table[b.name][2], atol=1e-4), (b.name, head, table[b.name][2])
        assert (b.parent.name if b.parent else None) == table[b.name][1], (b.name, b.parent)
    right_x = U(rig.matrix_world @ rig.data.bones["RightHand"].head_local)[0]
    assert right_x > 0, right_x
    for name, bone, pos, fwd, up in (sk.SOCKETS if kind == "body" else arm_sockets()):
        o = objs[name]
        assert o.parent == rig and o.parent_type == "BONE" and o.parent_bone == bone, (name, o.parent, o.parent_type, o.parent_bone)
        m = o.matrix_world
        assert np.allclose(U(m.translation), pos, atol=1e-4), (name, U(m.translation))
        m3 = m.to_3x3().normalized()
        assert np.allclose(U(m3 @ Vector((0, 0, 1))), unit(fwd), atol=1e-3), (name, "forward")
        assert np.allclose(U(m3 @ Vector((0, 1, 0))), unit(up), atol=1e-3), (name, "up")
        if bone.endswith("Hand"):   # barrel along the metacarpals, weapon top toward the index knuckle
            side = bone[:-4]
            knuckles = unit(np.subtract(skel[side + "IndexProximal"][2], skel[side + "LittleProximal"][2]))
            meta = unit(np.subtract(skel[bone][3], skel[bone][2]))
            assert np.dot(unit(up), knuckles) > .9 and np.dot(unit(fwd), meta) > .9, (name, "grip must follow the knuckle line")
    meshes = [o for o in objs.values() if o.type == "MESH"]
    contract = set(MATERIALS)
    for o in meshes:
        groups = {g.index: g.name for g in o.vertex_groups}
        bad = []
        for v in o.data.vertices:
            ws = [(groups[g.group], g.weight) for g in v.groups if g.weight > 0]
            x = U(o.matrix_world @ v.co)[0]
            if (not ws or abs(sum(w for _, w in ws) - 1) > .001 or any(n not in names for n, _ in ws)
                    or (x > .02 and any(n.startswith("Left") for n, _ in ws))
                    or (x < -.02 and any(n.startswith("Right") for n, _ in ws))):
                bad.append(v.index)
        assert not bad, (o.name, len(bad), bad[:8])
        assert {m.name for m in o.data.materials} <= contract, (o.name, [m.name for m in o.data.materials])
    out = {"fbx": path.name, "bones": len(names), "meshes": {o.name: tris(o) for o in sorted(meshes, key=lambda o: o.name)}}
    for name, (lo, hi) in TRI_LIMITS.items():
        if name in objs:
            assert lo <= tris(objs[name]) <= hi, (name, tris(objs[name]))
    if kind == "body":
        lods = [objs[f"Soldier_LOD{q}"] for q in range(3)]
        marks = [objs[f"TeamMark{t}"] for t in range(1, 5)]
        assert all(set(m.vertex_groups.keys()) == {"UpperChest", "Head", "RightUpperArm", "LeftUpperArm"} for m in marks)
        used = {m.name for o in lods[:1] + marks for m in o.data.materials}
        assert used == contract, used
        pts = np.array([U(o.matrix_world @ v.co) for o in lods + marks for v in o.data.vertices])
    else:
        pts = np.array([U(objs["SoldierArms"].matrix_world @ v.co) for v in objs["SoldierArms"].data.vertices])
    low, high = pts.min(axis=0), pts.max(axis=0)
    if kind == "body":
        assert abs(high[1] - sk.HEIGHT) <= .005 and 0 <= low[1] < .02, (low, high)
    out.update(bounds_min=[round(float(x), 4) for x in low], bounds_max=[round(float(x), 4) for x in high])
    return out


# ------------------------------------------------------------------ previews
def rotate(rig, bone, axis, deg):
    """Rotate a posed bone about a Unity world axis through its head (Unity right-hand-rule angle)."""
    pb = rig.pose.bones[bone]; m = pb.matrix.copy(); h = m.to_translation()
    # to_blender is a reflection, so a Unity rotation of +deg is a Blender rotation of -deg about B(axis).
    r = Matrix.Translation(h) @ Matrix.Rotation(-math.radians(deg), 4, Vector(B(unit(axis)))) @ Matrix.Translation(-h)
    pb.matrix = r @ m
    bpy.context.view_layer.update()


def pose(rig, steps):
    for bone, axis, deg in steps:
        rotate(rig, bone, axis, deg)


def rest(rig):
    for pb in rig.pose.bones:
        pb.matrix_basis.identity()
    bpy.context.view_layer.update()


FLEX, KNEE, LEAN = (-1, 0, 0), (1, 0, 0), (1, 0, 0)
IDLE = [("RightUpperArm", (0, 0, -1), 70), ("LeftUpperArm", (0, 0, 1), 70), ("RightLowerArm", FLEX, 14),
        ("LeftLowerArm", FLEX, 14), ("RightUpperLeg", (0, 0, 1), 3), ("LeftUpperLeg", (0, 0, -1), 3)]
POSES = {   # Unity world axes; FLEX moves a hanging limb forward, KNEE folds the shin back.
    "Run": [("RightUpperLeg", FLEX, 35), ("LeftUpperLeg", FLEX, -35), ("RightLowerLeg", KNEE, 25),
            ("LeftLowerLeg", KNEE, 70), ("Spine", LEAN, 8), ("RightUpperArm", (0, 0, -1), 72),
            ("LeftUpperArm", (0, 0, 1), 72), ("RightUpperArm", FLEX, -30), ("LeftUpperArm", FLEX, 30),
            ("RightLowerArm", FLEX, 60), ("LeftLowerArm", FLEX, 60)],
    "Slide": [("RightUpperLeg", FLEX, 85), ("RightLowerLeg", KNEE, 100), ("LeftUpperLeg", FLEX, 70),
              ("LeftLowerLeg", KNEE, 15), ("Spine", LEAN, -22), ("RightUpperArm", (0, 0, -1), 55),
              ("LeftUpperArm", (0, 0, 1), 55), ("RightLowerArm", FLEX, 40), ("LeftLowerArm", FLEX, 40)],
    "Aim": [("RightUpperArm", (0, -1, 0), 80), ("RightLowerArm", (0, -1, 0), 60),
            ("LeftUpperArm", (0, 1, 0), 80), ("LeftLowerArm", (0, 1, 0), 60)],
    "CrouchLand": [("RightUpperLeg", FLEX, 100), ("LeftUpperLeg", FLEX, 100), ("RightLowerLeg", KNEE, 110),
                   ("LeftLowerLeg", KNEE, 110), ("Spine", LEAN, 20), ("RightUpperArm", (0, 0, -1), 60),
                   ("LeftUpperArm", (0, 0, 1), 60), ("RightUpperArm", FLEX, 30), ("LeftUpperArm", FLEX, 30),
                   ("RightLowerArm", FLEX, 50), ("LeftLowerArm", FLEX, 50)],
}


def studio(scene, engine="BLENDER_EEVEE_NEXT"):
    scene.render.engine = engine
    if engine == "BLENDER_EEVEE_NEXT":
        scene.eevee.taa_render_samples = 48
    else:
        scene.display.shading.light = "STUDIO"; scene.display.shading.color_type = "MATERIAL"
        scene.display.shading.show_shadows = True; scene.display.shading.show_cavity = True
    scene.view_settings.view_transform = "AgX"
    world = bpy.data.worlds.new("Daylight"); scene.world = world; world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (*srgb((.70, .72, .75)), 1); bg.inputs["Strength"].default_value = .55
    for name, energy, rot, angle in (("Sun", 4.2, (54, 0, 58), 4), ("Rim", 1.6, (62, 0, 205), 10)):
        data = bpy.data.lights.new(name, "SUN"); data.energy = energy; data.angle = math.radians(angle)
        o = bpy.data.objects.new(name, data); scene.collection.objects.link(o)
        o.rotation_euler = tuple(math.radians(a) for a in rot)
    mat = bpy.data.materials.new("Floor"); mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*srgb((.78, .77, .74)), 1)
    me = bpy.data.meshes.new("Floor"); me.from_pydata([(-6, -6, 0), (6, -6, 0), (6, 6, 0), (-6, 6, 0)], [], [(0, 1, 2, 3)])
    me.materials.append(mat); floor = bpy.data.objects.new("Floor", me); scene.collection.objects.link(floor)


def camera(scene, eye, target, lens=70, ortho=None):
    data = bpy.data.cameras.new("Cam"); data.sensor_fit = "VERTICAL"; data.lens = lens
    if ortho:
        data.type = "ORTHO"; data.ortho_scale = ortho
    cam = bpy.data.objects.new("Cam", data); scene.collection.objects.link(cam)
    cam.location = Vector(B(eye))
    cam.rotation_euler = (Vector(B(target)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    return cam


def render(scene, path, res):
    scene.render.resolution_x, scene.render.resolution_y = res; scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"; scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def set_team(team, colour=None):
    col = (*srgb(colour or TEAMS[team]), 1)
    bpy.data.materials[ACCENT].node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = col
    bpy.data.materials[GLOW].node_tree.nodes["Principled BSDF"].inputs["Emission Color"].default_value = col
    for t in range(1, 5):
        bpy.data.objects[f"TeamMark{t}"].hide_render = t != team


def show_lod(q):
    for i in range(3):
        bpy.data.objects[f"Soldier_LOD{i}"].hide_render = i != q
    for t in range(1, 5):
        if q:
            bpy.data.objects[f"TeamMark{t}"].hide_render = True


def previews(root, scratch=None):
    from PIL import Image, ImageOps, ImageDraw
    out = root / "ArtSource" / "Previews"; out.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(root / "ArtSource" / "Characters" / "Soldier.blend"))
    scene = bpy.context.scene
    try:
        studio(scene)
        camera(scene, (0, 1, 6), (0, .9, 0)); render(scene, out / "_probe.png", (64, 64))
    except Exception as e:   # Eevee needs a GPU context; Workbench always works headless
        print("Eevee unavailable, using Workbench:", e)
        studio(scene, "BLENDER_WORKBENCH")
    (out / "_probe.png").unlink(missing_ok=True)
    rig = bpy.data.objects["SoldierRig"]
    views = {"Front": ((0, 1.0, 6.4), (0, .9, 0)), "Back": ((0, 1.0, -6.4), (0, .9, 0)),
             "ThreeQuarter": ((4.5, 1.6, 4.5), (0, .9, 0))}
    written = []

    def shot(name, eye, target=(0, .9, 0), res=(600, 900), **kw):
        camera(scene, eye, target, **kw); p = out / name; render(scene, p, res); written.append(p)
    show_lod(0); set_team(1)
    shot("Soldier_Bind.png", *views["Front"])
    pose(rig, IDLE)
    for team in range(1, 5):
        set_team(team)
        for view, (eye, target) in views.items():
            shot(f"Soldier_Team{team}_{view}.png", eye, target)
    set_team(1)
    shot("Soldier_Detail_Torso.png", (1.2, 1.45, 1.9), (0, 1.3, 0), (700, 700), lens=50)
    shot("Soldier_Detail_Helmet.png", (.45, 1.70, .65), (0, 1.645, .02), (700, 700), lens=60)
    for q in (1, 2):
        show_lod(q); shot(f"Soldier_LOD{q}.png", *views["ThreeQuarter"])
    show_lod(0); rest(rig)
    for name, steps in POSES.items():
        pose(rig, steps)
        shot(f"Soldier_Pose_{name}.png", (4.2, 1.5, 4.2), (0, .85, 0))
        shot(f"Soldier_Pose_{name}Side.png", (6.2, 1.1, 0), (0, .85, 0))
        rest(rig)
    # Readability at 30 m: a 1.8 m figure is ~62 px tall on a 1080p, 60-degree-vertical view.
    # Columns (front, side, back each): team colour, its greyscale, greyscale with the accent neutralised (symbols only).
    pose(rig, IDLE); tiles = []
    for team in range(1, 5):
        row = []
        for colour in (TEAMS[team], (.45, .45, .45)):
            set_team(team, colour)
            for eye in ((0, 1.0, 30), (30, 1.0, 0), (0, 1.0, -30)):
                camera(scene, eye, (0, .9, 0), ortho=1.8 * 80 / 62)
                p = out / "_tile.png"; render(scene, p, (80, 80)); row.append(Image.open(p).convert("RGB").copy())
        tiles.append(row)
    (out / "_tile.png").unlink(missing_ok=True)
    grey = [[ImageOps.grayscale(im) for im in row] for row in tiles]
    sheet = Image.new("RGB", (80 * 9, 80 * 4))
    for r in range(4):
        for c in range(3):
            sheet.paste(tiles[r][c], (c * 80, r * 80))
            sheet.paste(grey[r][c].convert("RGB"), ((c + 3) * 80, r * 80))
            sheet.paste(grey[r][c + 3].convert("RGB"), ((c + 6) * 80, r * 80))
    p = out / "Soldier_Readability30m.png"; sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST).save(p); written.append(p)
    # Separation: fewest pixels (of 6400) differing by more than 24 grey levels between any two teams, per view.
    sep = {"metric": "80x80 native tiles, 62px figure; count absolute difference >24/255 display grey levels",
           "pairs": {}, "accent_linear_luminance": {str(t): round(float(np.dot(srgb(c), (.2126, .7152, .0722))), 4)
                                                      for t, c in TEAMS.items()}}
    for name, off in (("palette_grey", 0), ("symbol_only_grey", 3)):
        for j, view in enumerate(("front", "side", "back")):
            a = [np.asarray(grey[t][off + j], int) for t in range(4)]
            pairs = {f"{x+1}-{y+1}": int((abs(a[x] - a[y]) > 24).sum()) for x in range(4) for y in range(x + 1, 4)}
            sep["pairs"][f"{name}_{view}"] = pairs
            sep[f"{name}_{view}"] = min(pairs.values())
    # Labelled contact sheet retains nearest-neighbour enlargement of the actual measured pixels.
    labelled = Image.new("RGB", (sheet.width * 3 + 70, sheet.height * 3 + 48), "#20252b")
    labelled.paste(sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST), (70, 48))
    draw = ImageDraw.Draw(labelled)
    for col, title in enumerate(("Colour front", "Colour side", "Colour back", "Grey front", "Grey side", "Grey back",
                                 "Neutral front", "Neutral side", "Neutral back")):
        draw.text((74 + col * 240, 16), title, fill="white")
    for row in range(4):
        draw.text((5, 140 + row * 240), f"Team {row+1}", fill="white")
    labelled.save(p)
    # Machado full-severity matrices, applied in linear sRGB before display encoding.
    # https://github.com/colour-science/colour/blob/develop/colour/blindness/datasets/machado2010.py
    cvd = {"Protanopia": ((.152286, 1.052583, -.204868), (.114503, .786281, .099216), (-.003882, -.048116, 1.051998)),
           "Deuteranopia": ((.367322, .860646, -.227968), (.280085, .672501, .047413), (-.011820, .042940, .968881)),
           "Tritanopia": ((1.255528, -.076749, -.178779), (-.078411, .930809, .147602), (.004733, .691367, .303900))}
    cvd_sheet = Image.new("RGB", labelled.size, "#20252b"); cvd_draw = ImageDraw.Draw(cvd_sheet)
    sep["cvd_model"] = "Machado 2009/2010 severity 1.0, linear sRGB; count >24 difference in any simulated display RGB channel, plus conservative greyscale counts; not human recognition evidence"
    for group, (name, matrix) in enumerate(cvd.items()):
        simulated, simulated_rgb = [], []
        for t in range(4):
            row, rgb_row = [], []
            cvd_draw.text((5, 140 + t*240), f"Team {t+1}", fill="white")
            for j, view in enumerate(("front", "side", "back")):
                rgb = np.asarray(tiles[t][j], float)/255
                linear = np.where(rgb <= .04045, rgb/12.92, ((rgb+.055)/1.055)**2.4)
                transformed = np.clip(linear @ np.asarray(matrix).T, 0, 1)
                encoded = np.where(transformed <= .0031308, transformed*12.92, 1.055*transformed**(1/2.4)-.055)
                im = Image.fromarray(np.round(encoded*255).astype(np.uint8))
                cvd_sheet.paste(im.resize((240, 240), Image.NEAREST), (70+(group*3+j)*240, 48+t*240))
                row.append(np.asarray(ImageOps.grayscale(im), int))
                rgb_row.append(np.asarray(im, int))
                cvd_draw.text((74+(group*3+j)*240, 16), f"{name} {view}", fill="white")
            simulated.append(row)
            simulated_rgb.append(rgb_row)
        for j, view in enumerate(("front", "side", "back")):
            pairs = {f"{x+1}-{y+1}": int((abs(simulated[x][j]-simulated[y][j]) > 24).sum())
                     for x in range(4) for y in range(x+1, 4)}
            sep["pairs"][f"{name.lower()}_grey_{view}"] = pairs
            sep[f"{name.lower()}_grey_{view}"] = min(pairs.values())
            rgb_pairs = {f"{x+1}-{y+1}": int((np.max(abs(simulated_rgb[x][j]-simulated_rgb[y][j]), axis=2) > 24).sum())
                         for x in range(4) for y in range(x+1, 4)}
            sep["pairs"][f"{name.lower()}_rgb_{view}"] = rgb_pairs
            sep[f"{name.lower()}_rgb_{view}"] = min(rgb_pairs.values())
    p = out / "Soldier_Readability30m_CVD.png"; cvd_sheet.save(p); written.append(p)
    # Regression floors catch a return to the old near-zero separation; recognition still needs gameplay review.
    for view in ("front", "side", "back"):
        assert sep[f"palette_grey_{view}"] >= 20, sep
        assert sep[f"symbol_only_grey_{view}"] >= 3, sep
        for name in cvd:
            assert sep[f"{name.lower()}_rgb_{view}"] >= 20, sep
    # First-person arms: bind pose, then the right hand curled (grip_solve) round a 3.5 cm grip on WeaponHand_R's axis.
    bpy.ops.wm.open_mainfile(filepath=str(root / "ArtSource" / "Characters" / "SoldierArms.blend"))
    scene = bpy.context.scene; studio(scene); bpy.data.objects["Floor"].hide_render = True
    rig = bpy.data.objects["SoldierArmsRig"]
    shot("SoldierArms_Bind.png", (.55, 1.75, .55), (.62, 1.46, -.01), (900, 700), lens=35)
    fit = grip_solve()
    th = fit["Thumb"]
    rotate(rig, "RightThumbProximal", THUMB_AXES["Right"][0], th["spread_deg"])
    rotate(rig, "RightThumbProximal", THUMB_AXES["Right"][1], th["opposition_deg"])
    for digit, r in fit.items():
        hinge = finger_hinge("Right", digit)
        if digit == "Thumb":
            hinge = thumb_rotation("Right", th["spread_deg"], th["opposition_deg"]) @ hinge
        for seg, deg in zip(SEGS, r["deg"]):
            rotate(rig, f"Right{digit}{seg}", hinge, deg)
    c = np.array({s[0]: s for s in sk.SOCKETS}["WeaponHand_R"][2], float)
    mats = {m.name: m for m in bpy.data.materials}
    build_object("GripProxy", [Part(*tube_geo([c + [0, 0, GRIP_SPAN[0]], c + [0, 0, GRIP_SPAN[1]]], GRIP_R, 24), ACCENT, "x")], mats)
    shot("SoldierArms_Grip.png", (.62, 1.30, .28), (.78, 1.44, -.02), (900, 700), lens=40)
    shot("SoldierArms_GripEnd.png", (.785, 1.44, -.45), (.785, 1.44, -.01), (900, 700), lens=60)   # little-finger end
    print("grip fit:", json.dumps(fit))
    if scratch:
        scratch.mkdir(parents=True, exist_ok=True)
        groups = {"sheet_teams": [out / f"Soldier_Team{t}_{v}.png" for t in range(1, 5) for v in views],
                  "sheet_poses": [p for p in written if "Pose" in p.name or "LOD" in p.name or "Bind" in p.name],
                  "sheet_arms": [p for p in written if p.name.startswith("SoldierArms")] + [out / "Soldier_Readability30m.png"]}
        for name, files in groups.items():
            ims = [Image.open(f).convert("RGB") for f in files]
            cols = 6 if name == "sheet_teams" else 4 if name == "sheet_poses" else 2
            w, h = 300, 450 if name != "sheet_arms" else 350
            grid = Image.new("RGB", (cols * w, -(-len(ims) // cols) * h), (255, 255, 255))
            for i, im in enumerate(ims):
                im.thumbnail((w, h)); grid.paste(im, ((i % cols) * w, (i // cols) * h))
            grid.save(scratch / f"{name}.png")
    return [str(p.relative_to(root)) for p in written], sep


# ------------------------------------------------------------------ concept (forge, GPU) and manifest
CONCEPT_DESC = ("sleek armored sci-fi soldier, closed helmet with dark glossy visor, clean ivory hard-surface armor "
                "plates over charcoal undersuit, graphite trim, small cyan accent panels, athletic human proportions, "
                "minimalist")


def gpu_free_mib():
    out = subprocess.run(["nvidia-smi", "--query-gpu=memory.free", "--format=csv,noheader,nounits"],
                         capture_output=True, text=True, check=True).stdout
    return int(out.split()[0])


def concept(root, seeds, force=False):
    out = root / "ArtSource" / "Characters" / "Forge" / "soldier_concept"
    free = gpu_free_mib()
    if free < 8000 and not force:
        raise SystemExit(f"GPU has only {free} MiB free; another process holds VRAM. Not killing it: "
                         "close it (or pass --force) and rerun `concept`.")
    sys.path.insert(0, str(FORGE))
    from stages import concept as forge_concept, image_to_3d, preprocess
    record = {"forge_entry": "stages.concept.build (SDXL + OpenPose ControlNet + IP-Adapter), "
                             "stages.preprocess.run + stages.image_to_3d.run (TripoSR, MIT)",
              "description": CONCEPT_DESC, "positive_template": forge_concept.POSITIVE,
              "negative": forge_concept.NEGATIVE, "view_hints": forge_concept.VIEW_HINT, "runs": []}
    for seed in seeds:
        views = forge_concept.build(CONCEPT_DESC, out / f"seed{seed}", seed=seed, steps=30)
        record["runs"].append({"seed": seed, "steps": 30, "views": {k: str(Path(v).relative_to(root)) for k, v in views.items()}})
    forge_concept.unload()
    front = out / f"seed{seeds[0]}" / "view_front.png"
    cut = out / "triposr" / "soldier_ref.cut.png"; cut.parent.mkdir(parents=True, exist_ok=True)
    preprocess.run(front, cut, lineart="off")
    obj = image_to_3d.run(cut.with_suffix(".rgb.png"), cut.parent / "soldier_ref.raw.obj", resolution=256, yaw=90.0)
    record["triposr"] = {"input": str(front.relative_to(root)), "mesh": str(Path(obj).relative_to(root)),
                         "note": "proportion reference only; normalised to 2 m by the forge; never exported"}
    (out / "concept.json").write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(record, indent=2))


def git(repo, *args):
    return subprocess.run(["git", "--no-optional-locks", "-C", str(repo), *args], capture_output=True, text=True).stdout


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def manifest(root, results, preview_files):
    chars, art = root / "ArtSource" / "Characters", root / "Assets" / "Wildstone" / "Art"
    concept_json = chars / "Forge" / "soldier_concept" / "concept.json"
    previous = chars / "soldier-export-manifest.json"
    baseline = json.loads(previous.read_text()).get("readability_baseline") if previous.is_file() else None
    data = {
        "generator": "Tools/Assets/build_soldier.py all", "blender": bpy.app.version_string,
        "build_identity": {"command": '"C:\\Users\\tyler\\Main Storage\\ai_skelly_tool\\.venv\\Scripts\\python.exe" -B Tools/Assets/build_soldier.py all --output-root .',
                           "head": git(root, "rev-parse", "HEAD").strip(),
                           "generator_sha256": sha(root / "Tools/Assets/build_soldier.py"),
                           "limits": "Blender export/reimport and static studio previews only; Unity import, moving readability and human CVD acceptance remain with integration lane"},
        "axis": "Unity (x,y,z) -> Blender (-x,-z,y) via soldier_skeleton.to_blender; FBX -Z forward, Y up, "
                "FBX_SCALE_ALL, no leaf bones, primary Y / secondary X; NO import mirror",
        "skeleton": "Tools/Assets/soldier_skeleton.py", "skeleton_sha256": sha(root / "Tools/Assets/soldier_skeleton.py"),
        "sockets": {"table": [list(map(lambda x: list(x) if isinstance(x, tuple) else x, s)) for s in sk.SOCKETS],
                    "hand_convention": "forward = wrist->knuckles (barrel along the metacarpals); up = thumb/index side "
                                       "(weapon top); the grip (weapon -Y) runs through the fist along the knuckle line; "
                                       "Grip_R/Grip_L in SoldierArms.fbx copy WeaponHand_R/L"},
        "forge": {"path": str(FORGE), "commit": git(FORGE, "rev-parse", "HEAD").strip(),
                  "branch": git(FORGE, "branch", "--show-current").strip(),
                  "dirty_entries": len(git(FORGE, "status", "--porcelain").splitlines()),
                  "concept": json.loads(concept_json.read_text()) if concept_json.is_file() else "not generated",
                  "rig_helpers_used": "none; skinning is deterministic per region (rigid plates, axial chain blends)"},
        "vibegame1": {"path": str(VIBE), "commit": git(VIBE, "rev-parse", "HEAD").strip(),
                      "ported_from": "Tools/weapon_models/build_art.py: Tube, superellipse, dome_factor, stations, "
                                     "skin, slab, shell, digit(), digit_lames(), knuckle_plate(), palm_tube(), "
                                     "back-of-hand lames, knuckle guard; build_weapons.py tube()/loft()"},
        "hand_rig_notes": {
            "bind": "fingers straight along the skeleton (right hand +X, palm -Y, thumb +Z); knuckles at bone heads",
            "pieces": "every phalanx is a separate rigid piece (100% its bone) with domes centred on both joints",
            "handpose_port": "vibegame1 HandPose derives hinges from a bent-fist bind (cross of adjacent segments) "
                             "and treats rest = fist. Here the bind is straight, so that cross product is zero: the "
                             "port must use explicit hinges and treat curl 0 = bind (open), curl 1 = fist.",
            "hinge_world_at_bind": {f"{side}{grp}": [round(x, 4) for x in finger_hinge(side, d)]
                                    for side in ("Right", "Left") for grp, d in (("Fingers", "Index"), ("Thumb", "Thumb"))},
            "thumb_axes_world": {side: {"spread": list(a), "opposition": list(b)} for side, (a, b) in THUMB_AXES.items()},
            "hinge_rule": "h = cross(segmentDir, palmNormal -Y) at bind; Quaternion.AngleAxis(+deg, h) in world space "
                          "curls toward the palm. Thumb (no extra bone; ThumbProximal's other degrees of freedom): rotate "
                          "ThumbProximal by AngleAxis(+spread, spreadAxis) then AngleAxis(+opposition, oppositionAxis) "
                          "(world, at bind), then curl every thumb segment about R * h",
            "grip_preview": {"grip_diameter_m": 2 * GRIP_R, "grip_axis": "Unity Z through WeaponHand_R",
                             "fit_right_hand": grip_solve()},
            "tips": "no tip bones (52-bone contract); distal tip = bone tail in soldier_skeleton.py"},
        "team_readability": {"accent_srgb": TEAMS, "mark_srgb": MATERIALS[MARK][0],
                             "surface_decals": "Chest/back fill existing plates; both pauldrons use ivory symbols on near-black backing, 0.6/1.2 mm offset (circle hole 1.8 mm); existing TeamMark1..4 meshes only",
                             "unity_integration": "SoldierBuilder must map SoldierMark to neutral ivory, not accent-tinted emissive; Unity validation belongs to integration lane"},
        "readability_baseline": baseline,
        "results": results, "previews": preview_files,
        "files": {str(p.relative_to(root)).replace("\\", "/"): sha(p) for p in
                  (chars / "Soldier.blend", chars / "SoldierArms.blend", art / "Soldier.fbx", art / "SoldierArms.fbx")},
    }
    (chars / "soldier-export-manifest.json").write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    return data


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=["concept", "model", "arms", "verify", "counts", "previews", "all"])
    parser.add_argument("--output-root", required=True, type=Path)
    parser.add_argument("--seeds", default="7,21", help="concept seeds")
    parser.add_argument("--force", action="store_true", help="concept: run even with little free VRAM")
    parser.add_argument("--scratch", type=Path, default=None, help="previews: also write review contact sheets here")
    args = parser.parse_args()
    root = args.output_root.resolve()
    assert root == Path(__file__).resolve().parents[2] and (root / "AGENTS.md").is_file(), root
    if args.mode == "concept":
        concept(root, [int(s) for s in args.seeds.split(",")], args.force)
        raise SystemExit(0)
    load_bpy()
    if args.mode == "counts":
        print(json.dumps(counts(), indent=2)); print("grip fit:", json.dumps(grip_solve()))
        raise SystemExit(0)
    art = root / "Assets" / "Wildstone" / "Art"
    results = {}
    if args.mode in ("model", "all"):
        build_body(root)
    if args.mode in ("arms", "all"):
        build_arms(root)
    if args.mode in ("model", "verify", "all"):
        results["body"] = verify(art / "Soldier.fbx", "body")
    if args.mode in ("arms", "verify", "all"):
        results["arms"] = verify(art / "SoldierArms.fbx", "arms")
    print(json.dumps(results, indent=2))
    if args.mode in ("previews", "all"):
        files, sep = previews(root, args.scratch)
        print("\n".join(files)); print("readability 30 m:", json.dumps(sep))
        if args.mode == "all":
            manifest(root, dict(results, readability_30m=sep), files)
            print("manifest written")

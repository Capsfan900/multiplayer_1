"""Wildstone soldier animation set: declare, bake, verify and preview every clip.

Run with the isolated ai_skelly_tool bpy Python (docs/ASSET-PIPELINE.md):
    <ai_skelly_tool>/.venv/Scripts/python.exe -B Tools/Assets/soldier_clips.py build --output-root .
Modes: build (bake .blend + FBX + clips sidecar + manifest), verify (re-import the FBX and gate
every clip, plus rest compatibility with Soldier.fbx), previews (contact sheets).

Hand-authored and license-clean: poses are declared in Unity character space (right +X, up +Y,
forward +Z, metres, feet at y=0) as pelvis/torso/head orientations, foot and weapon targets, and
solved per 30 fps frame with analytic two-bone IK onto soldier_skeleton. Each clip is its own
Blender action, exported as FBX take "SoldierRig|<Clip>". Forge (ai_skelly_tool/stages/anim.py)
curve helpers drive the periodic channels, its clinical gait curves shape the swing-foot arcs and
its C1 quaternion spline interpolates key poses; tests/test_anim_quality.py gates the result.
"""
import argparse
import bisect
import hashlib
import json
import math
import subprocess
import sys
from pathlib import Path

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
FORGE = Path(r"C:\Users\tyler\Main Storage\ai_skelly_tool")      # read-only; imported by path
for _p in (FORGE / "tests", FORGE, HERE):
    if str(_p) not in sys.path:
        sys.path.insert(0, str(_p))

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import soldier_skeleton as sk  # noqa: E402
from stages import anim as forge  # noqa: E402

FPS = 30
RIG, PROXY = "SoldierRig", "AnimProxy"
TABLE = sk.bones()
REST = {n: (Vector(h), Vector(t)) for n, _, h, t in TABLE}
PARENT = {n: p for n, p, _, _ in TABLE}
ORDER = [row[0] for row in TABLE]
SIDES = ("Left", "Right")
X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
I3 = Matrix.Identity(3)
TO_BL = Matrix(((-1, 0, 0), (0, 0, -1), (0, 1, 0)))    # sk.to_blender as a (reflection) matrix


def seg(n):
    return REST[n][1] - REST[n][0]


THIGH, SHIN = seg("LeftUpperLeg").length, seg("LeftLowerLeg").length
UPPER, FORE = seg("RightUpperArm").length, seg("RightLowerArm").length
SOCK = {name: Vector(p) for name, _, p, _, _ in sk.SOCKETS}
ANK_Y = REST["LeftFoot"][0].y
# Sole contact points: heel and ball relative to the Foot head, tip relative to the Toes head.
HEEL, BALL = Vector((0, -ANK_Y, -.055)), Vector((0, -ANK_Y, .14))
TIP = Vector((0, -REST["LeftToes"][0].y, .095))
# Heel-up roll pivots midway between the ball pad and the toe joint, so neither the pad nor the
# flat toes drag more than ~1 cm as the heel rises (a pure pad pivot drags the toe tip ~2 cm).
BALL_PIVOT = BALL + Vector((0, .5 * REST["LeftToes"][0].y, 0))


# --- rotation helpers (Unity axes: +pitch leans forward, +yaw turns right, +roll tips left) ---
def rx(d):
    return Matrix.Rotation(math.radians(d), 3, "X")


def ry(d):
    return Matrix.Rotation(math.radians(d), 3, "Y")


def rz(d):
    return Matrix.Rotation(math.radians(d), 3, "Z")


def rot(p=0.0, y=0.0, r=0.0):
    return ry(y) @ rx(p) @ rz(r)


def foot_rot(up=0.0, yaw=0.0, roll=0.0):
    """Foot orientation: `up` lifts the toes (negative = heel up)."""
    return ry(yaw) @ rx(-up) @ rz(roll)


def slerp(a, b, t):
    qa, qb = a.to_quaternion(), b.to_quaternion()
    if qa.dot(qb) < 0:
        qb.negate()
    return qa.slerp(qb, t).to_matrix()


def frame(d, s):
    d = d.normalized()
    s = (s - d * d.dot(s)).normalized()
    return Matrix((d, s, d.cross(s))).transposed()


def smooth(u):
    u = min(1.0, max(0.0, u))
    return u * u * (3 - 2 * u)


def bump(u, w):
    """Unit initial slope, zero value at 0 and w, flat after w: a local tangent fix."""
    return u * (1 - u / w) ** 2 if 0 <= u < w else 0.0


def wv(amp, freq=1.0, peak=0.0):
    """Scalar cosine via forge._sway, crest at cycle fraction `peak`."""
    f = forge._sway(amp, axis=0, freq=freq, phase=0.25 - freq * peak)
    return lambda t: f(t)[0]


def S(*terms):
    """Sum of constants and scalar cycle functions."""
    return lambda t: sum(x(t) if callable(x) else x for x in terms)


def yaw_wave(amp, phase=0.0):
    f = forge._yaw(amp, phase)        # forge: cosine on the yaw channel, peak with the lead leg
    return lambda t: f(t)[1]


def tri(p=0.0, y=0.0, r=0.0):
    parts = [v if callable(v) else (lambda t, v=v: v) for v in (p, y, r)]
    return lambda t: tuple(f(t) for f in parts)


# --- interpolation ---------------------------------------------------------
def track(ts, vals, secant_ends=False):
    """C1 monotone cubic through keys (Blender auto-clamped style): flat at extremes."""
    scalar = isinstance(vals[0], (int, float))
    rows = [(float(v),) if scalar else tuple(v) for v in vals]
    n, dims = len(ts), len(rows[0])

    def slope(k, j):
        if n < 2:
            return 0.0
        if k == 0 or k == n - 1:
            if not secant_ends:
                return 0.0
            a, b = (0, 1) if k == 0 else (n - 2, n - 1)
            return (rows[b][j] - rows[a][j]) / (ts[b] - ts[a])
        a = (rows[k][j] - rows[k - 1][j]) / (ts[k] - ts[k - 1])
        b = (rows[k + 1][j] - rows[k][j]) / (ts[k + 1] - ts[k])
        if a * b <= 0:
            return 0.0
        m = (rows[k + 1][j] - rows[k - 1][j]) / (ts[k + 1] - ts[k - 1])
        return math.copysign(min(abs(m), 3 * abs(a), 3 * abs(b)), m)

    ms = [[slope(k, j) for j in range(dims)] for k in range(n)]

    def ev(t):
        if t <= ts[0]:
            out = rows[0]
        elif t >= ts[-1]:
            out = rows[-1]
        else:
            i = bisect.bisect_right(ts, t) - 1
            h = ts[i + 1] - ts[i]
            u = (t - ts[i]) / h
            h00, h10 = 2 * u ** 3 - 3 * u * u + 1, u ** 3 - 2 * u * u + u
            h01, h11 = -2 * u ** 3 + 3 * u * u, u ** 3 - u * u
            out = tuple(h00 * rows[i][j] + h10 * h * ms[i][j] + h01 * rows[i + 1][j] + h11 * h * ms[i + 1][j]
                        for j in range(dims))
        return out[0] if scalar else Vector(out)
    return ev


def rot_track(ts, mats):
    """forge._declared: C1 quaternion spline, flat at the ends and at reversals."""
    keys, prev = [], None
    for t, m in zip(ts, mats):
        e = m.to_euler("XYZ") if prev is None else m.to_euler("XYZ", prev)
        prev = e
        keys.append((t, tuple(math.degrees(a) for a in e)))
    return lambda t: forge._declared(keys, t)


# Overlapping action, in frames (the forge OVERLAP idea, applied to interior keys only):
# legs and pelvis lead, the weapon and head follow, the fingers settle last.
OVERLAP = {"hip": -1, "hipR": -1, "ankL": -1, "ankR": -1, "footL": -1, "footR": -1, "toeL": -1, "toeR": -1,
           "kneeL": -1, "kneeR": -1, "headR": 2, "hpR": 1, "hrR": 1, "hpL": 1, "hrL": 2, "elbL": 1, "elbR": 1,
           "fingL": 2, "fingR": 2}


def keyed(keys, overlap=True):
    """Key poses [(seconds, channels)] -> pose(t); every key must carry every channel."""
    ts = [float(t) for t, _ in keys]
    tracks = {}
    for ch in keys[0][1]:
        vals = [k[1][ch] for k in keys]
        tt = ts
        shift = OVERLAP.get(ch, 0) / FPS if overlap else 0.0
        if shift:
            moved = [ts[0]] + [t + shift for t in ts[1:-1]] + [ts[-1]]
            if all(a < b - 1e-6 for a, b in zip(moved, moved[1:])):
                tt = moved
        tracks[ch] = rot_track(tt, vals) if isinstance(vals[0], Matrix) else track(tt, vals)
    return lambda t: {ch: f(t) for ch, f in tracks.items()}


# --- solver ------------------------------------------------------------------
def _rest_frames():
    out = {}
    for side in SIDES:
        for upper, lower, pole in (("UpperLeg", "LowerLeg", Z), ("UpperArm", "LowerArm", -Z)):
            fu = frame(seg(side + upper), pole)
            n, dl = Vector(fu.col[2]), seg(side + lower)
            out[side + upper], out[side + lower] = fu, frame(dl, n.cross(dl))
    return out


RFRAME = _rest_frames()
FINGERS, SEGS = ("Index", "Middle", "Ring", "Little"), ("Proximal", "Intermediate", "Distal")
CURL, THUMB = (75.0, 95.0, 60.0), (25.0, 40.0, 50.0)
PALM = Vector((0, -1, 0))
# (index, middle, ring, little, thumb curl, thumb opposition deg, spread deg)
HANDS = {
    "trigger": (.22, .92, .96, 1.0, .55, 38, 0),    # pistol grip, index along the trigger
    "fist": (.95, .97, 1.0, 1.0, .6, 45, 0),
    "cup": (.42, .52, .58, .64, .22, 22, 4),        # support hand under a fore-end
    "wrap": (.72, .78, .82, .86, .45, 28, 0),       # support hand around the firing hand
    "relaxed": (.25, .32, .40, .48, .15, 12, 6),
    "open": (.04, .05, .07, .09, .0, 0, 12),
    "ball": (.55, .60, .66, .72, .45, 50, 8),       # grenade or magazine
}


def two_bone(root, target, a, b, pole):
    d = target - root
    dist = min(max(d.length, abs(a - b) + 1e-4), .9995 * (a + b))
    axis = d.normalized()
    x = (a * a + dist * dist - b * b) / (2 * dist)
    h = math.sqrt(max(a * a - x * x, 0.0))
    bend = (pole - axis * axis.dot(pole)).normalized()
    return root + axis * x + bend * h, root + axis * dist, bend


def clav_user(side, v):
    prot, elev = v[0], v[1]
    return ry(-prot) @ rz(elev) if side == "Right" else ry(prot) @ rz(-elev)


def solve(c, info=None):
    """Channels -> world rotation delta from the T-pose for every bone (Unity axes)."""
    D, P = {}, {}

    def at(n):
        if n not in P:
            p = PARENT[n]
            P[n] = c["hip"].copy() if p is None else at(p) + D[p] @ (REST[n][0] - REST[p][0])
        return P[n]
    hips, chest, head = c["hipR"], c["chestR"], c["headR"]
    D["Hips"] = hips
    D["Spine"], D["Chest"], D["UpperChest"] = slerp(hips, chest, .35), slerp(hips, chest, .7), chest
    D["Neck"], D["Head"] = slerp(chest, head, .5), head
    uc, att = at("UpperChest"), c["attach"]
    for side in SIDES:
        k = side[0]
        hj = at(side + "UpperLeg")
        knee, ank, bend = two_bone(hj, c["ank" + k], THIGH, SHIN, c["knee" + k])
        ft = frame(knee - hj, bend)
        n, ds = Vector(ft.col[2]), ank - knee
        D[side + "UpperLeg"] = ft @ RFRAME[side + "UpperLeg"].transposed()
        D[side + "LowerLeg"] = frame(ds, n.cross(ds)) @ RFRAME[side + "LowerLeg"].transposed()
        D[side + "Foot"] = c["foot" + k]
        D[side + "Toes"] = c["foot" + k] @ rx(-c["toe" + k])
        # Hands: chest-attached when attach=1 (they ride the torso), world-locked at 0.
        hand_r = slerp(c["hr" + k], chest @ c["hr" + k], att)
        hp = c["hp" + k]
        hand_p = hp.lerp(uc + chest @ (hp - REST["UpperChest"][0]), att)
        pole = c["elb" + k].lerp(chest @ c["elb" + k], att)
        wrist = hand_p - hand_r @ (SOCK["WeaponHand_" + k] - REST[side + "Hand"][0])
        sh = side + "Shoulder"
        base = chest @ clav_user(side, c["clav" + k])
        ua0 = at(sh) + base @ (REST[side + "UpperArm"][0] - REST[sh][0])
        cdir, need = base @ seg(sh), wrist - ua0
        w = smooth((need.length / (UPPER + FORE) - .55) / .4)
        axis = cdir.cross(need)
        D[sh] = base
        if w > 0 and axis.length > 1e-6:      # clavicle follows a long reach
            D[sh] = Matrix.Rotation(min(cdir.angle(need) * .25, math.radians(16)) * w, 3, axis) @ base
        s = at(sh) + D[sh] @ (REST[side + "UpperArm"][0] - REST[sh][0])
        elbow, wr, bend = two_bone(s, wrist, UPPER, FORE, pole)
        fu = frame(elbow - s, bend)
        n, df = Vector(fu.col[2]), wr - elbow
        D[side + "UpperArm"] = fu @ RFRAME[side + "UpperArm"].transposed()
        la0 = frame(df, n.cross(df)) @ RFRAME[side + "LowerArm"].transposed()
        # Pronation lives in the forearm; the wrist only flexes and deviates.
        q, a = (la0.transposed() @ hand_r).to_quaternion(), seg(side + "LowerArm").normalized()
        tw = 2 * math.atan2(q.x * a.x + q.y * a.y + q.z * a.z, q.w)
        tw = (tw + math.pi) % (2 * math.pi) - math.pi
        D[side + "LowerArm"] = la0 @ Matrix.Rotation(tw, 3, a)
        D[side + "Hand"] = hand_r
        if info is not None:
            info.setdefault("reach", []).append((c["ank" + k] - hj).length / (THIGH + SHIN))
            info.setdefault("ankle_err", []).append((ank - c["ank" + k]).length)
            info.setdefault("wrist_err", []).append((wr - wrist).length)
            info.setdefault("pronation", []).append(abs(math.degrees(tw)))
            info.setdefault("twist", []).append(math.degrees(tw))
        _fingers(D, side, c["fing" + k])
    return D


def _fingers(D, side, f):
    hand = D[side + "Hand"]
    for digit, curl, sp in zip(FINGERS, f[:4], (1, 0, -1, -2)):
        parent = hand
        for i, s in enumerate(SEGS):
            name = side + digit + s
            d = seg(name).normalized()
            r = Matrix.Rotation(math.radians(curl * CURL[i]), 3, d.cross(PALM).normalized())
            if i == 0 and sp:
                r = r @ Matrix.Rotation(math.radians(f[6] * sp), 3, d.cross(Z).normalized())
            D[name] = parent @ r
            parent = D[name]
    parent = hand @ rx(f[5])
    for i, s in enumerate(SEGS):
        name = side + "Thumb" + s
        d = seg(name).normalized()
        D[name] = parent @ Matrix.Rotation(math.radians(f[4] * THUMB[i]), 3, d.cross(PALM).normalized())
        parent = D[name]


def fk(D, hip):
    P = {}
    for n in ORDER:
        p = PARENT[n]
        P[n] = hip.copy() if p is None else P[p] + D[p] @ (REST[n][0] - REST[p][0])
    return P


def soles(D, P):
    return {side: [P[side + "Foot"] + D[side + "Foot"] @ HEEL, P[side + "Foot"] + D[side + "Foot"] @ BALL,
                   P[side + "Toes"] + D[side + "Toes"] @ TIP] for side in SIDES}


def eye(c):
    D = solve(c)
    P = fk(D, c["hip"])
    return P["Head"] + D["Head"] @ (SOCK["HeadSocket"] - REST["Head"][0])


# --- pose building blocks ------------------------------------------------------
STANCE = {"L": (-.13, .07, -10.0), "R": (.15, -.09, 16.0)}       # x, z, yaw: slightly bladed
SQUARE = {"L": (-.12, .02, -6.0), "R": (.13, -.03, 8.0)}


def body(hip=(.01, .955, -.01), pelvis=(2, 5, 0), chest=(5, -2, 0), head=(-1, -2, 0)):
    return {"hip": Vector(hip), "hipR": rot(*pelvis), "chestR": rot(*chest),
            "headR": head if isinstance(head, Matrix) else rot(*head)}


def planted(feet=STANCE, up=None, out=.15):
    """Feet on the floor; `up` = {side: pitch} rolls a foot about its ball (<0) or heel (>0)."""
    c = {}
    for k, (x, z, yaw) in feet.items():
        p = (up or {}).get(k, 0.0)
        A, R = pivot_foot(Vector((x, ANK_Y, z)), yaw, p)
        c.update({"ank" + k: A, "foot" + k: R, "toe" + k: max(0.0, -p),
                  "knee" + k: (ry(yaw * .5) @ Vector((out * (-1 if k == "L" else 1), 0, 1))).normalized()})
    return c


def pivot_foot(flat, yaw, p):
    Ry, R = ry(yaw), foot_rot(p, yaw)
    piv = HEEL if p >= 0 else BALL_PIVOT
    return flat + Ry @ piv - R @ piv, R


def air_legs(c, **legs):
    """Airborne legs authored like FK: (hip flex, knee flex, abduction, plantarflex) per side."""
    out = {}
    for side in SIDES:
        k = side[0]
        flex, kn, abd, plant = legs[k]
        sg = -1 if k == "L" else 1
        hj = c["hip"] + c["hipR"] @ (REST[side + "UpperLeg"][0] - REST["Hips"][0])
        base = c["hipR"] @ rz(sg * abd)
        thigh, shin = base @ rx(-flex), base @ rx(-(flex - kn))
        out["ank" + k] = hj + thigh @ Vector((0, -THIGH, 0)) + shin @ Vector((0, -SHIN, 0))
        out["knee" + k] = thigh @ Z
        out["foot" + k] = shin @ rx(plant)
        out["toe" + k] = 0.0
    return out


def gun(down=0.0, yaw=0.0, cant=0.0):
    """Weapon orientation (+Z muzzle, +Y top). `down` dips the muzzle, `cant` rolls the top left."""
    return rot(down, yaw, cant)


def _grip_to_hand():
    """Hand T-pose axes -> weapon axes from the WeaponHand_R socket: forward -> muzzle (+Z), up -> top (+Y)."""
    f, u = next((Vector(f), Vector(u)) for n, _, _, f, u in sk.SOCKETS if n == "WeaponHand_R")
    return Matrix((u.cross(f), u, f))


GRIP_TO_HAND = _grip_to_hand()
RIFLE_FORE = Vector((0, .075, .36))   # support-palm contact under the bore, at the fore-grip (~0.36 m ahead)
SIGHT = .17                  # sight line above the grip anchor (bore +0.12, optic +0.05)


def left_hand(fwd, back):
    """Left hand from its wrist->knuckle direction and back-of-hand direction."""
    f = fwd.normalized()
    b = (back - f * f.dot(back)).normalized()
    return Matrix((-f, b, (-f).cross(b))).transposed()


def arms(hpR, hrR, hpL, hrL, fingR, fingL, elbR=(.6, -.75, -.25), elbL=(-.35, -.9, .1),
         clavR=(0, 0), clavL=(0, 0), attach=1.0):
    return {"hpR": Vector(hpR), "hrR": hrR, "hpL": Vector(hpL), "hrL": hrL,
            "fingR": Vector(HANDS[fingR] if isinstance(fingR, str) else fingR),
            "fingL": Vector(HANDS[fingL] if isinstance(fingL, str) else fingL),
            "elbR": Vector(elbR).normalized(), "elbL": Vector(elbL).normalized(),
            "clavR": Vector((*clavR, 0)), "clavL": Vector((*clavL, 0)), "attach": float(attach)}


def rifle_left(G, W):
    return G + W @ RIFLE_FORE, left_hand(W @ Vector((.5, .05, .85)), W @ Vector((-.6, -.8, .1)))


def rifle(G, W, left=None, fingL="cup", **kw):
    G = Vector(G)
    hpL, hrL = left or rifle_left(G, W)
    return arms(G, W @ GRIP_TO_HAND, hpL, hrL, "trigger", fingL, **kw)


def pistol(G, W, **kw):
    G = Vector(G)
    kw.setdefault("elbR", (.45, -.85, -.15))
    kw.setdefault("elbL", (-.45, -.85, -.15))
    return arms(G, W @ GRIP_TO_HAND, G + W @ Vector((-.045, -.02, -.01)),
                left_hand(W @ Vector((.3, -.55, .78)), W @ Vector((-.95, .15, .1))), "trigger", "wrap", **kw)


def sword(G, W, L=(-.19, 1.08, .17), **kw):
    kw.setdefault("elbR", (.7, -.7, -.2))
    kw.setdefault("elbL", (-.6, -.8, -.1))
    return arms(G, W @ GRIP_TO_HAND, L, left_hand(Vector((.35, -.2, 1)), Vector((-.6, .7, 0))), "fist", "relaxed", **kw)


# Carry presets angle the muzzle across the body: with this rig's arm (shoulder->palm 0.61 m) a square
# rifle puts the fore-grip out of the support hand's reach.
LOW = ((.05, 1.20, .17), gun(23, -32, -8))       # low ready, stock in the shoulder pocket
READY = ((.07, 1.18, .23), gun(6, -24, -2))      # rifle near level at the chest, ready to raise
HOLD_PISTOL = ((.03, 1.22, .30), gun(10, -2, 0))
HOLD_SWORD = ((.22, 1.10, .22), gun(-45, -15, -15))


def low_ready(dG=(0, 0, 0), dW=(0, 0, 0), preset=LOW, **kw):
    G, W = preset
    return rifle(Vector(G) + Vector(dG), rot(*dW) @ W, **kw)


def idle_pose(**over):
    c = {**body(), **planted(), **low_ready()}
    c.update(over)
    return c


def merge(*parts):
    out = {}
    for p in parts:
        out.update(p)
    return out


def lowest(c):
    D = solve(c)
    P = fk(D, c["hip"])
    return min(min(p.y for p in P.values()), min(p.y for ps in soles(D, P).values() for p in ps))


def lift(*keys, margin=.01):
    """Airborne keys: raise them together (no added bob) until nothing hangs below the floor."""
    dy = max(0.0, margin - min(lowest(k) for k in keys))
    up = Vector((0, dy, 0))
    for k in {id(k): k for k in keys}.values():
        for ch in ("hip", "ankL", "ankR") + (("hpL", "hpR") if k["attach"] < 1 else ()):
            k[ch] = k[ch] + up
    return keys


# --- locomotion ------------------------------------------------------------------
# Swing-foot arcs come from the forge's clinical gait curves (Perry & Burnfield shapes) as FK on
# this leg: (hip, knee, ankle curve, toe-off fraction, hip-flexion scale, knee scale).
SWING_SRC = {
    "walk": (forge.WALK_HIP, forge.WALK_KNEE, forge.WALK_ANKLE, .60, 1.0, 1.0),
    "run": (forge.RUN_HIP, forge.RUN_KNEE, forge.RUN_ANKLE, .45, 1.0, 1.0),
    "sprint": (forge.RUN_HIP, forge.RUN_KNEE, forge.RUN_ANKLE, .45, 1.25, 1.18),
}


def forge_leg(src, t):
    hip, knee, ankle, _, flex_k, knee_k = SWING_SRC[src]
    f = -hip(t)[0]
    f = f * flex_k if f > 0 else f
    kn = knee(t)[0] * knee_k
    rf, rs = math.radians(f), math.radians(f - kn)
    return (THIGH * math.sin(rf) + SHIN * math.sin(rs), -(THIGH * math.cos(rf) + SHIN * math.cos(rs)),
            f - kn - ankle(t)[0])


def bounce(beta, td, rise, frames):
    """Hip height over a run cycle: ballistic flight, C1 spring dip in stance (one per step)."""
    T = frames / FPS
    tc, tf = beta * T, (.5 - beta) * T
    v0 = 4 * rise / tf

    def f(phi):
        h = (phi % .5) * T
        if h < tc:
            return td - v0 * tc / math.pi * math.sin(math.pi * h / tc)
        x = h - tc
        return td + v0 * x - v0 * x * x / tf
    return f


TOE_REL = REST["LeftToes"][0] - REST["LeftFoot"][0]
# Swing clearance of the lowest sole point (m) over the swing fraction; the ankle height is derived
# from it, so toes never dip through the floor whatever the foot pitch.
CLEAR = {"walk": [(0, 0), (.15, .03), (.4, .045), (.7, .035), (.9, .018), (1, 0)],
         "side": [(0, 0), (.3, .055), (.7, .045), (1, 0)],
         "run": [(0, 0), (.15, .08), (.35, .22), (.6, .18), (.85, .06), (1, 0)],
         "back": [(0, 0), (.08, .06), (.35, .22), (.6, .18), (.85, .06), (1, 0)],     # quick toe clearance at lift-off
         "sprint": [(0, 0), (.12, .12), (.3, .38), (.5, .36), (.7, .22), (.88, .07), (1, 0)]}


def sole_depth(R, toe):
    return -min((R @ HEEL).y, (R @ BALL).y, (R @ TOE_REL + R @ rx(-toe) @ TIP).y)


def gait(name, frames, duty, travel, sweep, center, lanes, yaws, pitch, src, hip, pelvis, chest, head,
         carry, clear=None, lift=1.0, bulge=None, knees=None, reverse=False, offsets=None, tilt=None,
         windows=(.3, .3)):
    """In-place cycle: exact ground-locked stance (heel/ball roll) + forge-shaped C1 swing."""
    T = frames / FPS
    d = Vector(travel).normalized()
    offsets = offsets or {"Left": 0.0, "Right": .5}
    pitch_f = track([s for s, _ in pitch], [p for _, p in pitch], secant_ends=True)
    keys = CLEAR[clear or src]
    clear_f = track([u for u, _ in keys], [h * lift for _, h in keys])
    toff = SWING_SRC[src][3]

    def stance(side, s):
        lane = Vector(lanes[side])
        flat = Vector((lane.x, ANK_Y, lane.z)) + d * (center + sweep * (.5 - s))
        p = pitch_f(s)
        A, R = pivot_foot(flat, yaws[side], p)
        return A, p, max(0.0, -p)

    def shape(u):
        uu = 1 - u if reverse else u
        return forge_leg(src, toff + uu * (1 - toff))

    def make_swing(side):
        e, k = 1e-3, (1 - duty) / duty
        A0, p0, t0 = stance(side, 1.0)
        A1, p1, t1 = stance(side, 0.0)
        Ab, pb, _ = stance(side, 1 - e)
        Af, pf, _ = stance(side, e)
        m0, mp0 = (A0 - Ab) / e * k, (p0 - pb) / e * k
        m1, mp1 = (Af - A1) / e * k, (pf - p1) / e * k
        m0.y = m1.y = 0.0
        f0, _, q0 = shape(0.0)
        f1 = shape(1.0)[0]
        chord = A1 - A0
        chord.y = 0.0
        bul = Vector((bulge or {}).get(side, (0, 0, 0)))

        def base(u):
            f, _, q = shape(u)       # forge clinical arc: forward progress and foot pitch
            return A0 + chord * ((f - f0) / (f1 - f0)) + bul * math.sin(math.pi * u), p0 + (q - q0)
        B1, Q1 = base(1.0)
        Bs, Qs = base(e)
        Be, Qe = base(1 - e)
        dm0, dmp0 = m0 - (Bs - A0) / e, mp0 - (Qs - p0) / e
        dm1, dmp1 = m1 - (B1 - Be) / e, mp1 - (Q1 - Qe) / e
        dm0.y = dm1.y = 0.0
        w0, w1 = windows

        def at(u):
            B, Q = base(u)
            h = smooth(u)
            pos = B + (A1 - B1) * h + dm0 * bump(u, w0) - dm1 * bump(1 - u, w1)
            p = Q + (p1 - Q1) * h + dmp0 * bump(u, w0) - dmp1 * bump(1 - u, w1)
            toe = t0 * (1 - smooth(u / .3)) + t1 * smooth((u - .7) / .3)
            R = foot_rot(p, yaws[side])
            pos.y = ANK_Y - (ANK_Y - sole_depth(R, toe)) + clear_f(u)     # lowest sole point rides `clear`
            return pos, R, toe
        return at
    swings = {side: make_swing(side) for side in SIDES}
    knees = knees or {"Left": (-.12, 0, 1), "Right": (.12, 0, 1)}

    def pose(t):
        phi = (t / T) % 1.0
        pel = pelvis(phi)
        c = {"hip": hip(phi), "hipR": rot(*pel), "chestR": rot(*chest(phi)), "headR": rot(*head(phi))}
        for side in SIDES:
            k = side[0]
            psi = (phi - offsets[side]) % 1.0
            if psi < duty:
                A, p, toe = stance(side, psi / duty)
                R = foot_rot(p, yaws[side])
            else:
                A, R, toe = swings[side]((psi - duty) / (1 - duty))
            c.update({"ank" + k: A, "foot" + k: R, "toe" + k: toe,
                      "knee" + k: (ry(pel[1] * .7) @ Vector(knees[side])).normalized()})
        c.update(carry(phi))
        if tilt:
            roll, pivot = tilt
            R = rz(roll)
            for key in ("hip", "ankL", "ankR"):
                c[key] = pivot + R @ (c[key] - pivot)
            for key in ("hipR", "chestR", "footL", "footR"):
                c[key] = R @ c[key]
            for key in ("kneeL", "kneeR"):
                c[key] = R @ c[key]
            c["headR"] = rz(roll * .45) @ c["headR"]      # the head rights itself toward level
        return c
    events = sorted((round(offsets[s] * T, 4), "Foot" + s[0]) for s in SIDES)
    return {"name": name, "frames": frames, "loop": True, "mask": "full", "additive": False,
            "events": events, "pose": pose, "travel": d, "design_speed": sweep / (duty * T),
            "contact": tilt, "stride": sweep / (duty * T) * T}


def carry_low(bob=.010, sway=1.5, nod=1.0, lag=.17, preset=LOW, dG=(0, 0, 0)):
    """Rifle riding the chest with a lagging vertical bob and a yaw sway (overlapping action)."""
    b, s, n = wv(bob, 2, lag), wv(sway, 1, lag - .07), wv(nod, 2, lag + .03)
    return lambda phi: low_ready(dG=Vector(dG) + Vector((0, b(phi), 0)), dW=(n(phi), s(phi), 0), preset=preset)


def mirrored(fn_tuple):
    """Mirror a (pitch, yaw, roll) cycle across the body's sagittal plane."""
    return lambda phi: (lambda v: (v[0], -v[1], -v[2]))(fn_tuple(phi))


def walk_f():
    return gait("WalkF", 28, .60, (0, 0, 1), .92, -.10,
                {"Left": (-.09, 0, 0), "Right": (.09, 0, 0)}, {"Left": -7, "Right": 7},
                [(0, 18), (.14, 0), (.55, 0), (.8, -14), (1, -48)], "walk",
                hip=lambda p: Vector((wv(-.022, 1, .3)(p), .915 + wv(.02, 2, .3)(p), 0)),
                pelvis=tri(S(3, wv(1.2, 2, .05)), yaw_wave(6), wv(-3, 1, .3)),
                chest=tri(S(6, wv(.8, 2, .12)), yaw_wave(-1.8), wv(1.0, 1, .35)),
                head=tri(S(2, wv(.5, 2, .17)), yaw_wave(.5), 0),
                carry=carry_low(bob=.008, sway=1.2),
                bulge={"Left": (-.015, 0, 0), "Right": (.015, 0, 0)})


def walk_b():
    return gait("WalkB", 28, .62, (0, 0, -1), .76, .04,
                {"Left": (-.09, 0, 0), "Right": (.09, 0, 0)}, {"Left": -6, "Right": 6},
                [(0, -22), (.2, 0), (.65, 0), (1, 16)], "walk", reverse=True,
                hip=lambda p: Vector((wv(-.018, 1, .3)(p), .91 + wv(.016, 2, .3)(p), 0)),
                pelvis=tri(S(1, wv(1, 2, .05)), yaw_wave(-4), wv(-2.5, 1, .3)),
                chest=tri(S(3, wv(.6, 2, .12)), yaw_wave(1.2), wv(.8, 1, .35)),
                head=tri(S(1, wv(.4, 2, .17)), 0, 0),
                carry=carry_low(bob=.007, sway=1.0))


def walk_side(right=True):
    s = 1 if right else -1
    lanes = {"Left": (-.20, 0, .03), "Right": (.20, 0, -.03)}
    return gait("WalkR" if right else "WalkL", 16, .5, (s, 0, 0), .28, 0, lanes, {"Left": -10, "Right": 10},
                [(0, -6), (.3, 0), (.75, 0), (1, -30)], "walk", clear="side",
                hip=lambda p: Vector((0, .925 + wv(-.010, 2, .0)(p), 0)),
                pelvis=tri(S(2, wv(.8, 2, .1)), s * 5, wv(-2 * s, 1, .25)),
                chest=tri(S(5, wv(.5, 2, .15)), -s * 2, wv(1 * s, 1, .3)),
                head=tri(1, -s * 1, 0),
                carry=carry_low(bob=.006, sway=.8),
                offsets={"Left": .5, "Right": 0.0} if right else {"Left": 0.0, "Right": .5})


def run_f(name="RunF", tilt=None, lanes_x=.06):
    beta = .139
    return gait(name, 20, beta, (0, 0, 1), .74, -.10,
                {"Left": (-lanes_x, 0, 0), "Right": (lanes_x, 0, 0)}, {"Left": -4, "Right": 4},
                [(0, -8), (.4, -2), (.7, -12), (1, -45)], "sprint",
                hip=lambda p, b=bounce(beta, .915, .045, 20): Vector((wv(-.012, 1, beta / 2)(p), b(p), 0)),
                pelvis=tri(S(9, wv(2, 2, beta / 2)), yaw_wave(9), wv(-4, 1, beta / 2)),
                chest=tri(S(15, wv(1.5, 2, beta / 2 + .08)), yaw_wave(-3), wv(1.5, 1, beta / 2 + .1)),
                head=tri(S(4, wv(1, 2, beta / 2 + .12)), yaw_wave(.8), 0),
                carry=carry_low(bob=.016, sway=2.5, nod=1.5, lag=beta / 2 + .15, dG=(0, .02, -.02)),
                bulge={"Left": (-.02, 0, 0), "Right": (.02, 0, 0)}, tilt=tilt, windows=(.3, .13))


# Back and side runs bound (short contact, long flight) on slightly lower hips, so their step matches RunF's and an
# 11 m/s backpedal or strafe cycles at the forward run's ~4.2 steps/s instead of ~6 (SoldierRig.LocomotionScale).
def run_b():
    beta = .12
    return gait("RunB", 20, beta, (0, 0, -1), .62, -.04,
                {"Left": (-.075, 0, 0), "Right": (.075, 0, 0)}, {"Left": -5, "Right": 5},
                [(0, -18), (.4, -6), (1, -22)], "run", reverse=True, lift=.8, clear="back",
                hip=lambda p, b=bounce(beta, .86, .035, 20): Vector((wv(-.01, 1, beta / 2)(p), b(p), 0)),
                pelvis=tri(S(4, wv(1.5, 2, beta / 2)), yaw_wave(-5), wv(-3, 1, beta / 2)),
                chest=tri(S(7, wv(1, 2, beta / 2 + .08)), yaw_wave(1.5), wv(1, 1, beta / 2 + .1)),
                head=tri(S(1, wv(.8, 2, beta / 2 + .12)), 0, 0),
                carry=carry_low(bob=.012, sway=1.8, lag=beta / 2 + .15), windows=(.15, .3))


def run_side(right=True):
    beta, s = .105, (1 if right else -1)
    lead, trail = ("Right", "Left") if right else ("Left", "Right")
    lanes = {lead: (.03 * s, 0, -.06), trail: (-.03 * s, 0, .06)}
    bulge = {trail: (0, 0, .10), lead: (0, 0, -.08)}         # trail leg crosses over in front
    return gait("RunR" if right else "RunL", 20, beta, (s, 0, 0), .56, 0, lanes,
                {lead: 22 * s, trail: 26 * s}, [(0, -10), (.4, -3), (1, -35)], "run", lift=.8,
                hip=lambda p, b=bounce(beta, .845, .04, 20): Vector((0, b(p), 0)),
                pelvis=tri(S(5, wv(1.5, 2, beta / 2)), S(14 * s, wv(3, 1, 0)), -4 * s),
                chest=tri(S(9, wv(1, 2, beta / 2 + .08)), S(-2 * s, wv(-1, 1, 0)), S(-3 * s, wv(1, 1, .1))),
                head=tri(2, -s, 0),
                carry=carry_low(bob=.014, sway=2, lag=beta / 2 + .15),
                bulge=bulge, knees={lead: (.35 * s, 0, 1), trail: (.35 * s, 0, 1)},
                offsets={lead: 0.0, trail: .5}, windows=(.3, .15))


# --- one-shot and held clips ---------------------------------------------------------
def clip(name, frames, pose, loop=False, mask="full", additive=False, events=()):
    return {"name": name, "frames": frames, "loop": loop, "mask": mask, "additive": additive,
            "events": sorted(events), "pose": pose, "travel": None, "design_speed": 0.0, "contact": None}


def cycle(frames, fn):
    """Loop from a function of cycle fraction; frame N reproduces frame 0 exactly."""
    T = frames / FPS
    return lambda t: fn((t / T) % 1.0)


def idle():
    def fn(p):
        shift, breath = wv(1, 1, 0)(p), wv(1, 2, .1)(p)            # weight shift once, two breaths
        c = merge(body(hip=(.01 + .022 * shift, .955 - .006 * (1 - abs(shift)) + .002 * breath, -.01),
                       pelvis=(2 + .6 * breath, 5 + wv(1.2, 1, .6)(p), 1.6 * shift),
                       chest=(5 - .9 * breath, -2 + wv(1.0, 1, .7)(p), -.8 * shift),
                       head=(-1 + wv(1.2, 1, .35)(p) + wv(.4, 3, .2)(p), -2 + wv(3.5, 1, .55)(p), wv(.8, 1, .4)(p))),
                  planted(), low_ready(dG=(0, .005 * breath, 0), dW=(wv(.8, 2, .2)(p), wv(.7, 1, .8)(p), 0)))
        c["clavL"] = c["clavR"] = Vector((0, 1.2 * breath, 0))
        return c
    return clip("Idle", 108, cycle(108, fn), loop=True)


def hold(name, frames, arm_fn, bod=None, feet=STANCE):
    def fn(p):
        breath = wv(1, 2, .1)(p)
        b = bod or {}
        c = merge(body(hip=b.get("hip", (.01, .955 - .002 * breath, -.01)), pelvis=b.get("pelvis", (2, 5, 0)),
                       chest=tuple(a + d for a, d in zip(b.get("chest", (5, -2, 0)), (-.7 * breath, wv(.5, 1, .3)(p), 0))),
                       head=b.get("head", (-1, -2, 0))),
                  planted(feet), arm_fn(p, breath))
        return c
    return clip(name, frames, cycle(frames, fn), loop=True, mask="upper")


def aim_rifle_pose(breath=0.0, p=0.0, dG=(0, 0, 0), dW=(0, 0, 0)):
    bod = merge(body(hip=(.02, .95, -.02), pelvis=(3, 12, 0), chest=(7, 24, 0), head=(4, 0, -9)), planted())
    e = eye(merge(bod, low_ready()))
    G = Vector((e.x + .03, e.y - SIGHT, e.z + .12)) + Vector(dG)
    return rifle(G + Vector((wv(.002, 1, .3)(p), .003 * breath, 0)), rot(*dW) @ gun(wv(.25, 1, .1)(p), wv(.3, 1, .6)(p)),
                 attach=0.0, elbR=(.9, -.4, -.1), elbL=(-.2, -1, .05), clavL=(10, 0), clavR=(4, 6))


def aim_pistol_pose(breath=0.0, p=0.0):
    bod = merge(body(hip=(.01, .95, -.01), pelvis=(3, 2, 0), chest=(7, 0, 0), head=(3, 0, 0)), planted(SQUARE))
    e = eye(merge(bod, low_ready()))
    G = Vector((e.x, e.y - .10 + .003 * breath, e.z + .42))
    return pistol(G, gun(wv(.3, 1, .1)(p), wv(.3, 1, .6)(p)), attach=0.0, clavL=(12, 2), clavR=(12, 2),
                  elbR=(.6, -.8, 0), elbL=(-.6, -.8, 0))


def jump_start():
    k0 = idle_pose(**body(hip=(.01, .95, -.01), pelvis=(3, 4, 0), chest=(6, -2, 0), head=(0, -1, 0)), **planted(SQUARE))
    k1 = merge(k0, body(hip=(.01, .82, -.06), pelvis=(14, 4, 0), chest=(24, -2, 0), head=(-6, -1, 0)),
               low_ready(dG=(0, -.03, .02)))
    k2 = merge(k0, body(hip=(.01, 1.035, .03), pelvis=(-3, 2, 0), chest=(2, -1, 0), head=(-4, 0, 0)),
               planted(SQUARE, up={"L": -38, "R": -38}), low_ready(dG=(0, .04, -.01), dW=(-6, 0, 0)))
    return clip("JumpStart", 8, keyed([(0, k0), (.1, k1), (8 / FPS, k2)]), events=[(.25, "Takeoff")])


def fall_pose(p=0.0, amp=1.0):
    c = merge(body(hip=(0, 1.075 + .008 * wv(1, 1, .2)(p), 0), pelvis=(8 + 2 * wv(1, 1, .1)(p), 3 * wv(1, 1, .3)(p), 2 * wv(1, 1, .6)(p)),
                   chest=(10 + 1.5 * wv(1, 1, .2)(p), -2 * wv(1, 1, .35)(p), -1.5 * wv(1, 1, .7)(p)),
                   head=(-3 + wv(1, 1, .3)(p), 0, 0)),
              low_ready(preset=READY, dG=(0, .02 + .012 * wv(1, 1, .3)(p), -.02), dW=(-3 + 2 * wv(1, 1, .4)(p), 0, 0)))
    c.update(air_legs(c, L=(38 + 9 * amp * wv(1, 1, 0)(p), 62 + 12 * amp * wv(1, 1, .08)(p), 8, 25),
                      R=(18 + 9 * amp * wv(1, 1, .5)(p), 42 + 12 * amp * wv(1, 1, .58)(p), 6, 20)))
    return c


def land():
    k0 = merge(body(hip=(.01, .985, 0), pelvis=(6, 2, 0), chest=(8, -1, 0), head=(-2, 0, 0)),
               planted(SQUARE, up={"L": -14, "R": -10}), low_ready(preset=READY, dG=(0, .02, -.02)))
    k1 = merge(k0, body(hip=(.01, .90, -.02), pelvis=(10, 3, 0), chest=(14, -1, 0), head=(-4, 0, 0)), planted(SQUARE))
    k2 = merge(k1, body(hip=(.01, .79, -.06), pelvis=(17, 3, 0), chest=(27, -2, 0), head=(-9, -1, 0)),
               low_ready(dG=(0, -.04, .01), dW=(8, 0, 0)))
    k3 = idle_pose(**planted(SQUARE))
    return clip("Land", 9, keyed([(0, k0), (2 / FPS, k1), (4 / FPS, k2), (9 / FPS, k3)]), events=[(0.0, "Land")])


def dash():
    def legs(c, l, r):
        c.update(air_legs(c, L=l, R=r))
        return c
    b0 = merge(body(hip=(0, .93, 0), pelvis=(8, 0, 0), chest=(12, 0, 0), head=(2, 0, 0)), low_ready(preset=READY))
    k0 = legs(b0, (20, 35, 6, 10), (5, 25, 6, 10))
    k1 = legs(merge(b0, body(hip=(0, .89, -.02), pelvis=(14, 0, 0), chest=(20, 0, 0), head=(0, 0, 0))),
              (28, 50, 6, 10), (-5, 30, 6, 15))
    k2 = legs(merge(b0, body(hip=(0, .92, .08), pelvis=(22, 0, 0), chest=(34, 0, 0), head=(4, 0, 0)),
                    low_ready(preset=READY, dG=(-.02, .05, -.05), dW=(-8, 0, 0))), (70, 95, 8, 10), (-25, 15, 6, 30))
    k3 = legs(merge(b0, body(hip=(0, .93, .03), pelvis=(10, 0, 0), chest=(14, 0, 0), head=(2, 0, 0))),
              (35, 60, 6, 15), (-5, 40, 6, 20))
    lift(k0, k1, k2, k3)
    return clip("Dash", 9, keyed([(0, k0), (2 / FPS, k1), (4 / FPS, k2), (9 / FPS, k3)]))


def slide_pose(p=0.0, amp=1.0):
    j = amp * (wv(.004, 8, 0)(p) + wv(.003, 5, .3)(p))
    c = merge(body(hip=(0, .43 + j, .16), pelvis=(-24, 6 + amp * wv(1, 1, .2)(p), amp * wv(1.2, 1, 0)(p)),
                   chest=(-22 + amp * wv(.8, 2, .1)(p), 4, amp * wv(1.5, 1, .1)(p)), head=(6, -2, 0)),
              low_ready(preset=((.10, 1.24, .28), gun(8, -30, -10)), elbR=(.8, -.5, -.3), elbL=(-.3, -1, .2)))
    hj_r = c["hip"] + c["hipR"] @ (REST["RightUpperLeg"][0] - REST["Hips"][0])
    heel = Vector((.13, 0, hj_r.z + .80))
    A, R = pivot_foot(heel - HEEL, 4, 30)
    c.update({"ankR": A, "footR": R, "toeR": 0.0, "kneeR": Vector((.15, 1, .3)).normalized(),
              "ankL": Vector((.03, .10, .58)), "footL": foot_rot(10, 75, -55), "toeL": 0.0,
              "kneeL": Vector((-1, .35, .35)).normalized()})
    return c


def slide_enter():
    k0 = merge(body(hip=(0, .92, 0), pelvis=(8, 0, 0), chest=(13, 0, 0), head=(3, 0, 0)), low_ready(preset=READY))
    k0.update(air_legs(k0, L=(25, 40, 6, 10), R=(-10, 30, 6, 20)))
    s = slide_pose()
    k1 = merge(s, body(hip=(0, .60, .14), pelvis=(-14, 4, 0), chest=(-12, 3, 0), head=(4, -1, 0)))
    k1.update(air_legs(k1, L=(60, 110, 20, 10), R=(80, 5, 4, -25)))
    lift(k0)
    lift(k1)
    return clip("SlideEnter", 6, keyed([(0, k0), (3 / FPS, k1), (6 / FPS, s)]))


def wall_run(left=True):
    """Lean 30 deg off the wall and slide the cycle 0.285 m toward it, so the boots meet the wall at the 0.4 m
    capsule radius and the head stays near the capsule. The pivot is the point whose pure roll lands that shift,
    so contact_space still undoes the tilt exactly."""
    roll, shift = (-30, -.285) if left else (30, .285)
    a, b = 1 - math.cos(math.radians(roll)), math.sin(math.radians(roll))
    pivot = Vector((a * shift, b * shift, 0)) / (a * a + b * b)
    return run_f("WallRunL" if left else "WallRunR", tilt=(roll, pivot), lanes_x=.07)


def wall_jump():
    def legs(c, l, r):
        c.update(air_legs(c, L=l, R=r))
        return c
    b = merge(body(hip=(0, .80, -.04), pelvis=(16, 0, 0), chest=(22, 0, 0), head=(-6, 0, 0)), low_ready(preset=READY))
    k0 = legs(b, (70, 100, 6, 0), (60, 95, 6, 0))
    k1 = legs(merge(b, body(hip=(0, .98, .05), pelvis=(-4, 0, 0), chest=(4, 0, 0), head=(-6, 0, 0)),
                    low_ready(preset=READY, dG=(0, .04, -.02), dW=(-6, 0, 0))), (-10, 10, 6, 40), (-5, 15, 6, 35))
    k2 = legs(merge(b, body(hip=(0, 1.0, .04), pelvis=(4, 0, 0), chest=(8, 0, 0), head=(-3, 0, 0))),
              (-15, 35, 8, 30), (65, 90, 6, 10))
    lift(k0, k1, k2)
    k3 = fall_pose()
    return clip("WallJump", 10, keyed([(0, k0), (3 / FPS, k1), (6 / FPS, k2), (10 / FPS, k3)]), events=[(.1, "Push")])


def free_arms(R, L, hrR, hrL, fingR="relaxed", fingL="relaxed", elbR=(.5, -.8, -.3), elbL=(-.5, -.8, -.3)):
    return arms(R, hrR, L, hrL, fingR, fingL, elbR=elbR, elbL=elbL)


def death_front():
    k0 = idle_pose()
    k1 = merge(k0, body(hip=(.01, .95, -.06), pelvis=(-6, 6, 0), chest=(-16, 4, 3), head=(-28, 6, 0)),
               low_ready(dG=(0, .08, -.02), dW=(-25, 8, 0)))
    k2 = merge(k1, body(hip=(0, .70, -.18), pelvis=(-12, 8, -4), chest=(-24, 6, 4), head=(-10, 10, 0)),
               free_arms((.30, 1.02, .14), (-.42, 1.28, .10), gun(55, 10, 0) @ GRIP_TO_HAND,
                         left_hand(Vector((-.3, .4, .5)), Vector((-.5, -.2, -.6))), "trigger", "open"))
    k3 = merge(k2, body(hip=(0, .38, -.42), pelvis=(-55, 10, -6), chest=(-70, 8, 6), head=(-55, 10, 0)),
               free_arms((.38, 1.12, .02), (-.48, 1.40, .0), gun(40, 30, 0) @ GRIP_TO_HAND,
                         left_hand(Vector((-.4, .6, .2)), Vector((-.4, -.3, -.8))), "relaxed", "open"))
    k3.update({"ankL": Vector((-.14, .10, .10)), "ankR": Vector((.17, .12, -.02)),
               "kneeL": Vector((0, 1, .4)).normalized(), "kneeR": Vector((.2, 1, .4)).normalized()})
    k4 = merge(k3, body(hip=(0, .14, -.60), pelvis=(-88, 10, -4), chest=(-96, 6, 4), head=rx(-100) @ ry(15)),
               free_arms((.50, 1.30, -.06), (-.55, 1.42, -.06), gun(20, 60, 70) @ GRIP_TO_HAND,
                         left_hand(Vector((-.6, .5, 0)), Vector((0, 0, -1))), "relaxed", "relaxed"))
    k4.update({"ankL": Vector((-.18, .12, .24)), "ankR": Vector((.20, .26, .08)),
               "footL": foot_rot(60, -20), "footR": foot_rot(20, 10)})
    k5 = merge(k4, body(hip=(0, .17, -.60), pelvis=(-86, 10, -4), chest=(-90, 6, 4), head=rx(-84) @ ry(22)))
    k6 = merge(k4, body(hip=(0, .125, -.60), pelvis=(-90, 12, -3), chest=(-92, 6, 3), head=rx(-95) @ ry(32)),
               free_arms((.52, 1.36, -.09), (-.50, 1.48, -.06), gun(10, 70, 85) @ GRIP_TO_HAND,
                         left_hand(Vector((-.7, .6, 0)), Vector((0, 0, -1))), "relaxed", "relaxed"))
    k6.update({"ankL": Vector((-.20, .10, .28)), "ankR": Vector((.22, .10, -.02)),
               "footL": foot_rot(80, -30), "footR": foot_rot(0, 12), "kneeR": Vector((.25, 1, .2)).normalized(),
               "toeL": 0.0, "toeR": 0.0})
    keys = [(0, k0), (3 / FPS, k1), (10 / FPS, k2), (18 / FPS, k3), (24 / FPS, k4), (27 / FPS, k5), (40 / FPS, k6)]
    return clip("DeathF", 40, keyed(keys))


def tucked(ankles, pitch):
    """Feet standing on curled toes (heel up `pitch`) with the ankle at the given x, z."""
    out = {}
    for k, (x, z, yaw) in ankles.items():
        A, R = pivot_foot(Vector((x, ANK_Y, z)), yaw, pitch)
        A, R = pivot_foot(Vector((2 * x - A.x, ANK_Y, 2 * z - A.z)), yaw, pitch)
        out.update({"ank" + k: A, "foot" + k: R, "toe" + k: 5 - pitch})       # tips just clear the floor
    return out


def death_back():
    k0 = idle_pose()
    k1 = merge(k0, body(hip=(.01, .95, .07), pelvis=(8, -4, 0), chest=(-6, -3, 0), head=(-20, 0, 0)),
               low_ready(dG=(0, .03, .03), dW=(10, 0, 0)))
    k2 = merge(k1, body(hip=(0, .64, .12), pelvis=(25, -6, 0), chest=(40, -6, 0), head=(18, -8, 0)),
               planted(STANCE, up={"L": -25, "R": -30}),
               free_arms((.28, 1.05, .25), (-.30, 1.08, .28), gun(20, 0, 0) @ GRIP_TO_HAND,
                         left_hand(Vector((.2, -.6, .7)), Vector((-.6, .2, .3))), "trigger", "relaxed"))
    k2.update({"kneeL": Vector((-.1, -.3, 1)).normalized(), "kneeR": Vector((.1, -.3, 1)).normalized()})
    k3 = merge(k2, body(hip=(0, .54, .06), pelvis=(30, -6, 2), chest=(62, -5, 0), head=(40, -10, 0)),
               free_arms((.30, 1.25, .45), (-.30, 1.25, .48), gun(-25, 10, 0) @ GRIP_TO_HAND,
                         left_hand(Vector((0, -.7, .7)), Vector((-.3, .5, .6))), "relaxed", "open"))
    k3.update(tucked({"L": (-.14, -.38, -8), "R": (.16, -.36, 8)}, -70))
    k4 = merge(k3, body(hip=(0, .25, .22), pelvis=(72, -6, 4), chest=(94, -4, 0), head=rx(80) @ ry(-50)),
               free_arms((.42, 1.36, .14), (-.40, 1.30, .08), gun(-75, 40, -60) @ GRIP_TO_HAND,
                         left_hand(Vector((0, .2, 1)), Vector((0, 0, -1))), "relaxed", "relaxed"))
    k4.update(tucked({"L": (-.15, -.62, -8), "R": (.18, -.58, 8)}, -75))
    k4.update({"kneeL": Vector((-.1, -1, .2)).normalized(), "kneeR": Vector((.3, -1, .2)).normalized()})
    k5 = merge(k4, body(hip=(0, .27, .24), pelvis=(76, -6, 4), chest=(90, -4, 0), head=rx(76) @ ry(-58)))
    k6 = merge(k4, body(hip=(0, .13, .26), pelvis=(88, -5, 3), chest=(92, -3, 0), head=rx(90) @ ry(-70)),
               free_arms((.46, 1.32, .05), (-.42, 1.12, .03), gun(-85, 50, -80) @ GRIP_TO_HAND,
                         left_hand(Vector((0, .1, 1)), Vector((0, 0, -1))), "relaxed", "relaxed"))
    k6.update(tucked({"L": (-.17, -.66, -5), "R": (.24, -.60, 25)}, -75))
    k6.update({"kneeL": Vector((-.1, -1, .1)).normalized(), "kneeR": Vector((.6, -1, .1)).normalized()})
    keys = [(0, k0), (3 / FPS, k1), (10 / FPS, k2), (16 / FPS, k3), (24 / FPS, k4), (27 / FPS, k5), (40 / FPS, k6)]
    return clip("DeathB", 40, keyed(keys))


# --- upper-body actions (full skeleton keyed; legs hold the idle stance) ---------------------
def upper(name, frames, keys, additive=False, events=()):
    return {**clip(name, frames, keyed(keys), mask="upper", additive=additive, events=events)}


def ready_pose(**over):
    c = merge(body(), planted(), low_ready(preset=READY))
    c.update(over)
    return c


def mag_hand(G, W, drop=0.0):
    return G + W @ Vector((0, -.06 - drop, .11)), left_hand(W @ Vector((.15, .2, .95)), W @ Vector((-.95, -.1, .1)))


def reload():
    G0, W0 = Vector(READY[0]), READY[1]
    G1, W1 = G0 + Vector((-.03, .03, -.03)), rot(-8, 0, 18) @ W0
    G2, W2 = G1 + Vector((0, .01, 0)), rot(-6, 0, 14) @ W0

    def k(G, W, left, fing, head=(-1, -2, 0), chest=(5, -2, 0)):
        c = merge(body(chest=chest, head=head), planted(), rifle(G, W, left=left, fingL=fing))
        return c
    pouch = (Vector((-.17, .92, .10)), left_hand(Vector((.1, -1, .2)), Vector((-.9, 0, -.3))))
    keys = [
        (0, k(G0, W0, None, "cup")),
        (.12, k(G1, W1, mag_hand(G1, W1), "ball", head=(12, -2, 0), chest=(7, -2, 0))),
        (.30, k(G1, W1, (Vector((-.06, .96, .22)), left_hand(Vector((.1, -.6, .8)), Vector((-.9, .1, .1)))), "ball",
                head=(18, -4, 0), chest=(8, -2, 0))),
        (.45, k(G1, W1, pouch, "ball", head=(16, -6, 0), chest=(8, -3, 0))),
        (.55, k(G1, W1, pouch, "ball", head=(16, -6, 0), chest=(8, -3, 0))),
        (.63, k(G1, W1, mag_hand(G1, W1, .07), "ball", head=(18, -2, 0), chest=(8, -2, 0))),
        (.70, k(G1, W1, mag_hand(G1, W1), "ball", head=(18, -2, 0), chest=(8, -2, 0))),
        (.78, k(G2, W2, mag_hand(G2, W2, -.015), "cup", head=(14, -2, 0), chest=(7, -2, 0))),
        (.95, k(G0 + Vector((0, .01, 0)), rot(-2, 0, 3) @ W0, None, "cup", head=(2, -2, 0))),
        (1.2, k(G0, W0, None, "cup")),
    ]
    return upper("Reload", 36, keys, events=[(.30, "MagOut"), (.70, "ReloadInsert")])


def throw():
    G0, W0 = Vector(READY[0]), READY[1]
    Gd, Wd = G0 + Vector((.03, -.09, -.02)), rot(25, 15, 0) @ W0

    def k(L, hrL, fing, chest=(5, -2, 0), pelvis=(2, 5, 0), G=Gd, W=Wd, elbL=(-.6, -.6, -.4)):
        return merge(body(pelvis=pelvis, chest=chest, head=(-1, -2, 0)), planted(),
                     rifle(G, W, left=(Vector(L), hrL), fingL=fing, elbL=elbL))
    grip = left_hand(Vector((.2, .3, .9)), Vector((-.9, .3, 0)))
    keys = [
        (0, merge(ready_pose())),
        (.08, k((-.10, 1.25, .16), grip, "ball")),
        (.125, k((-.46, 1.38, .02), left_hand(Vector((-.3, .6, -.3)), Vector((-.9, -.2, -.3))), "ball",
                 chest=(0, -10, 0), pelvis=(1, 1, 0), elbL=(-.5, -.8, -.3))),
        (.17, k((-.30, 1.72, -.18), left_hand(Vector((.1, .9, -.2)), Vector((-.9, 0, -.3))), "ball",
                chest=(-6, -18, 0), pelvis=(0, -2, 0), elbL=(-.6, .2, -.7))),
        (.20, k((-.18, 1.70, .26), left_hand(Vector((.1, .6, .8)), Vector((-.6, .4, -.5))), "ball",
                chest=(2, -6, 0), pelvis=(1, 1, 0), elbL=(-.7, .2, -.6))),
        (.27, k((-.02, 1.22, .45), left_hand(Vector((.3, -.6, .7)), Vector((-.4, .7, .4))), "open",
                chest=(14, 15, 0), pelvis=(4, 9, 0), elbL=(-.6, -.6, -.2))),
        (.467, ready_pose()),
    ]
    return upper("Throw", 14, keys, events=[(.20, "Release")])


def sword_swing():
    G0, W0 = Vector(HOLD_SWORD[0]), HOLD_SWORD[1]

    def k(G, W, L, chest, pelvis, head=(-1, -2, 0)):
        return merge(body(pelvis=pelvis, chest=chest, head=head), planted(), sword(G, W, L=L))
    keys = [
        (0, k(G0, W0, (-.19, 1.08, .17), (5, -2, 0), (2, 5, 0))),
        (.12, k((.50, 1.56, -.06), gun(-30, 120, 30), (-.15, 1.15, .25), (2, 22, 2), (2, 12, 0), (-2, -6, 0))),
        (.185, k((.04, 1.30, .52), gun(-5, -20, -60), (-.20, 1.10, .10), (7, 0, 0), (3, 2, 0), (0, -2, 0))),
        (.25, k((-.36, 1.18, .38), gun(30, -70, -60), (-.25, 1.05, -.05), (8, -25, -2), (3, -8, 0), (0, 2, 0))),
        (.40, k(G0, W0, (-.19, 1.08, .17), (5, -2, 0), (2, 5, 0))),
    ]
    return upper("SwordSwing", 12, keys, events=[(.12, "SlashStart"), (.185, "Slash"), (.25, "SlashEnd")])


def equip():
    k0 = merge(body(), planted(), arms((.25, .98, .06), gun(75, 10, 0) @ GRIP_TO_HAND, (-.22, .93, .06),
                                       left_hand(Vector((0, -1, .2)), Vector((-1, 0, 0))), "trigger", "relaxed",
                                       elbR=(.3, -.5, -.8), elbL=(-.3, -.5, -.8)))
    G1, W1 = Vector((.17, 1.08, .20)), gun(30, 0, 0)
    k1 = merge(body(), planted(), rifle(G1, W1, left=((-.08, 1.02, .30), left_hand(Vector((.5, .1, .8)), Vector((-.6, -.8, 0)))),
                                        fingL="relaxed"))
    return upper("Equip", 9, [(0, k0), (.15, k1), (.30, ready_pose())])


def additive(name, frames, peaks, events=()):
    """Additive layer: frame 0 is the READY reference; `peaks` = [(t, offsets)] returning to zero."""
    def k(o):
        G = Vector(READY[0]) + Vector(o.get("dG", (0, 0, 0)))
        W = rot(*o.get("dW", (0, 0, 0))) @ READY[1]
        b = body(hip=tuple(a + d for a, d in zip((.01, .955, -.01), o.get("hip", (0, 0, 0)))),
                 pelvis=tuple(a + d for a, d in zip((2, 5, 0), o.get("pelvis", (0, 0, 0)))),
                 chest=tuple(a + d for a, d in zip((5, -2, 0), o.get("chest", (0, 0, 0)))),
                 head=tuple(a + d for a, d in zip((-1, -2, 0), o.get("head", (0, 0, 0)))))
        c = merge(b, planted(), rifle(G, W, clavR=o.get("clavR", (0, 0)), clavL=o.get("clavL", (0, 0))))
        return c
    keys = [(0, k({}))] + [(t, k(o)) for t, o in peaks] + [(frames / FPS, k({}))]
    return {**clip(name, frames, keyed(keys, overlap=False), mask="upper", additive=True, events=events)}


def clips():
    out = [idle(), walk_f(), walk_b(), walk_side(False), walk_side(True), run_f(), run_b(), run_side(False), run_side(True),
           jump_start(), clip("Fall", 36, cycle(36, fall_pose), loop=True), land(), dash(), slide_enter(),
           clip("Slide", 30, cycle(30, slide_pose), loop=True), wall_run(True), wall_run(False), wall_jump(),
           death_front(), death_back(),
           hold("HoldRifle", 72, lambda p, b: low_ready(preset=READY, dG=(0, .004 * b, 0), dW=(wv(.6, 1, .2)(p), wv(.5, 1, .7)(p), 0))),
           hold("HoldPistol", 72, lambda p, b: pistol(Vector(HOLD_PISTOL[0]) + Vector((0, .004 * b, 0)),
                                                       rot(wv(.6, 1, .2)(p), wv(.5, 1, .7)(p), 0) @ HOLD_PISTOL[1])),
           hold("HoldSword", 72, lambda p, b: sword(Vector(HOLD_SWORD[0]) + Vector((0, .005 * b, 0)),
                                                     rot(wv(1.2, 1, .2)(p), wv(1, 1, .7)(p), 0) @ HOLD_SWORD[1])),
           hold("AimRifle", 60, lambda p, b: aim_rifle_pose(b, p), bod={"hip": (.02, .95, -.02), "pelvis": (3, 12, 0),
                                                                          "chest": (7, 24, 0), "head": (4, 0, -9)}),
           hold("AimPistol", 60, lambda p, b: aim_pistol_pose(b, p), bod={"hip": (.01, .95, -.01), "pelvis": (3, 2, 0),
                                                                           "chest": (7, 0, 0), "head": (3, 0, 0)},
                feet=SQUARE),
           reload(), throw(), sword_swing(), equip(),
           additive("Recoil", 6, [(1 / FPS, {"dG": (0, .012, -.035), "dW": (-5, 1, 0), "chest": (-2.5, 0, 0),
                                             "head": (-2, 0, 0), "clavR": (-3, 1)}),
                                  (2.5 / FPS, {"dG": (0, .006, -.016), "dW": (-2.2, .4, 0), "chest": (-1, 0, 0),
                                               "head": (-.8, 0, 0), "clavR": (-1.3, .4)}),
                                  (4.5 / FPS, {})]),
           additive("HitF", 9, [(2 / FPS, {"chest": (-9, 2, 0), "head": (-12, 3, 0), "pelvis": (-3, 0, 0),
                                           "dG": (0, .02, -.04), "dW": (-6, 2, 0), "clavL": (0, 6), "clavR": (0, 6)}),
                                (4.5 / FPS, {"chest": (2, 0, 0), "head": (3, 0, 0), "pelvis": (1, 0, 0)})]),
           additive("HitB", 9, [(2 / FPS, {"chest": (10, -2, 0), "head": (8, -2, 0), "pelvis": (4, 0, 0),
                                           "hip": (0, 0, .02), "dG": (0, -.02, .03), "dW": (5, 0, 0),
                                           "clavL": (0, 5), "clavR": (0, 5)}),
                                (4.5 / FPS, {"chest": (-2, 0, 0), "head": (-2, 0, 0), "pelvis": (-1, 0, 0)})])]
    names = [c["name"] for c in out]
    assert len(names) == len(set(names)) == len(CLIP_NAMES) and set(names) == set(CLIP_NAMES), names
    return out


CLIP_NAMES = ("Idle", "WalkF", "WalkB", "WalkL", "WalkR", "RunF", "RunB", "RunL", "RunR", "JumpStart", "Fall", "Land",
              "Dash", "SlideEnter", "Slide", "WallRunL", "WallRunR", "WallJump", "DeathF", "DeathB", "HoldRifle",
              "HoldPistol", "HoldSword", "AimRifle", "AimPistol", "Reload", "Throw", "SwordSwing", "Equip", "Recoil",
              "HitF", "HitB")
LOCOMOTION = ("WalkF", "WalkB", "WalkL", "WalkR", "RunF", "RunB", "RunL", "RunR", "WallRunL", "WallRunR")
PLANTED = ("Idle", "Land", "HoldRifle", "HoldPistol", "HoldSword", "AimRifle", "AimPistol", "Reload", "Throw",
           "SwordSwing", "Equip", "Recoil", "HitF", "HitB")


# --- sampling and gates (FK model; verify also checks the FBX matches it) -------------------
def sample(c_):
    rows = []
    for f in range(c_["frames"] + 1):
        t = (f % c_["frames"] if c_["loop"] else f) / FPS
        ch, info = c_["pose"](t), {}
        D = solve(ch, info)
        rows.append((D, fk(D, ch["hip"]), info))
    return rows


def contact_space(c_, v):
    if not c_["contact"]:
        return v
    roll, pivot = c_["contact"]
    return pivot + rz(-roll) @ (v - pivot)


def measure(c_, rows):
    """Natural speed from planted sole travel, plus slip/floor/IK gates."""
    pts = []
    for D, P, _ in rows:
        sol = soles(D, P)
        pts.append({(s, i): contact_space(c_, p) for s in SIDES for i, p in enumerate(sol[s])})
    keys = list(pts[0])
    d = c_["travel"]
    vel = [-(pts[f][k] - pts[f - 1][k]).dot(d) * FPS for k in keys for f in range(1, len(pts))
           if pts[f][k].y < .01 and pts[f - 1][k].y < .01] if d is not None else []
    vel.sort()
    v = vel[len(vel) // 2] if vel else 0.0
    slip = 0.0
    if d is not None or c_["name"] in PLANTED:
        for k in keys:
            run = []
            for f, row in enumerate(pts + [None]):
                if row is not None and row[k].y < .01:
                    g = row[k] + (d * v * f / FPS if d is not None else Vector())
                    run.append(Vector((g.x, g.z)))
                    continue
                if len(run) > 1:
                    slip = max(slip, max((p - run[0]).length for p in run))
                run = []
    floor = min(min(p.y for p in row.values()) for row in pts)
    body_floor = min(min(contact_space(c_, P[n]).y for n in ORDER) for _, P, _ in rows)
    hips = min(contact_space(c_, P["Hips"]).y for _, P, _ in rows)
    info = [i for _, _, i in rows]
    return {"speed": round(v, 3), "slip": slip, "floor": min(floor, body_floor), "hips": hips,
            "reach": max(max(i["reach"]) for i in info), "ankle_err": max(max(i["ankle_err"]) for i in info),
            "wrist_err": max(max(i["wrist_err"]) for i in info), "pronation": max(max(i["pronation"]) for i in info)}


def seam_deg(rows):
    a, b = rows[0][0], rows[-1][0]
    return max(math.degrees(a[n].to_quaternion().rotation_difference(b[n].to_quaternion()).angle) for n in ORDER)


def gates(c_, m, rows):
    bad = []
    if c_["frames"] < 6:
        bad.append("fewer than 6 frames")
    if any(not all(math.isfinite(x) for x in v) for _, P, _ in rows for v in P.values()):
        bad.append("NaN")
    if c_["loop"] and seam_deg(rows) >= .5:
        bad.append(f"seam {seam_deg(rows):.2f} deg")
    if m["floor"] < -.01:
        bad.append(f"below floor {m['floor']:.3f}")
    if m["hips"] < .25 and not c_["name"].startswith(("Slide", "Death")):
        bad.append(f"hips {m['hips']:.3f}")
    if (c_["travel"] is not None or c_["name"] in PLANTED) and m["slip"] >= .02:
        bad.append(f"slip {m['slip'] * 100:.1f} cm")
    if c_["name"] in LOCOMOTION + PLANTED and m["ankle_err"] > .01:
        bad.append(f"ankle IK miss {m['ankle_err'] * 100:.1f} cm")
    return bad


# --- Blender build ---------------------------------------------------------------------
def B(p):
    return Vector(sk.to_blender(tuple(p)))


def make_rig():
    """Same construction and bone rolls as build_soldier.make_rig, so rest matrices match Soldier.fbx."""
    data = bpy.data.armatures.new(RIG)
    rig = bpy.data.objects.new(RIG, data)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, parent, head, tail in TABLE:
        eb = data.edit_bones.new(name)
        eb.head, eb.tail, eb.use_connect = B(head), B(tail), False
        d = Vector(tail) - Vector(head)
        eb.align_roll(B((0, 0, 1) if abs(d.y) > .7 * d.length else (0, 1, 0)))
    for name, parent, _, _ in TABLE:
        if parent:
            data.edit_bones[name].parent = data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return rig


def _box(verts, faces, corners):
    base = len(verts)
    verts.extend(corners)
    for f in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
        faces.append(tuple(base + i for i in f))


def _along(a, b, w, t):
    ax = (b - a).normalized()
    side = ax.cross(Z if abs(ax.dot(Z)) < .9 else X).normalized()
    up = side.cross(ax)
    return [p + side * sx * w + up * sy * t for p in (a, b) for sx in (-1, 1) for sy in (-1, 1)]


def _aabb(c, h):
    c, h = Vector(c), Vector(h)
    return [c + Vector((sx * h.x, sy * h.y, sz * h.z)) for sz in (-1, 1) for sx in (-1, 1) for sy in (-1, 1)]


def proxy(rig):
    """Rigid low-poly stand-in (one bone per part) for previews; left limbs blue, right orange."""
    verts, faces, groups = [], [], []

    def add(bone, corners):
        start = len(verts)
        _box(verts, faces, corners)
        groups.append((bone, range(start, len(verts))))
    torso = {"Hips": ((0, .94, 0), (.16, .09, .11)), "Spine": ((0, 1.15, 0), (.14, .08, .10)),
             "Chest": ((0, 1.29, 0), (.16, .08, .11)), "UpperChest": ((0, 1.42, -.01), (.19, .07, .115)),
             "Head": ((0, 1.685, .01), (.095, .11, .105))}
    for bone, (c, h) in torso.items():
        add(bone, _aabb(c, h))
    add("UpperChest", _aabb((0, 1.38, .125), (.07, .04, .015)))      # chest plate: shows facing
    add("Head", _aabb((0, 1.67, .115), (.07, .022, .015)))           # visor
    for name, *_ in TABLE:
        a, b = REST[name]
        if name.endswith(("Foot", "Toes")):
            x = a.x
            add(name, _aabb((x, .045, .03), (.05, .045, .105)) if name.endswith("Foot") else _aabb((x, .022, .175), (.048, .022, .045)))
        elif name.endswith("Hand"):
            add(name, _along(a, b + (b - a) * .1, .04, .018))
        elif any(name.endswith(k) for k in ("Neck", "Shoulder", "UpperArm", "LowerArm", "UpperLeg", "LowerLeg")):
            w = {"Neck": .045, "Shoulder": .05, "UpperArm": .05, "LowerArm": .042, "UpperLeg": .07, "LowerLeg": .052}
            add(name, _along(a, b, *(2 * [next(v for k, v in w.items() if name.endswith(k))])))
        elif any(d in name for d in FINGERS + ("Thumb",)):
            add(name, _along(a, b, .009, .008))
    mesh = bpy.data.meshes.new(PROXY)
    mesh.from_pydata([tuple(B(v)) for v in verts], [], [tuple(reversed(f)) for f in faces])
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(PROXY, mesh)
    bpy.context.collection.objects.link(obj)
    colors = {"Left": (.25, .45, .85), "Right": (.92, .52, .2), "": (.74, .74, .7)}
    for key, col in colors.items():
        mat = bpy.data.materials.new("Anim" + (key or "Centre"))
        mat.diffuse_color = (*col, 1)
        mesh.materials.append(mat)
    face_bone = []
    for bone, rng in groups:
        g = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
        g.add(list(rng), 1.0, "REPLACE")
        face_bone += [bone] * 6
    for poly, bone in zip(mesh.polygons, face_bone):
        poly.material_index = 0 if bone.startswith("Left") else 1 if bone.startswith("Right") else 2
    obj.parent = rig
    obj.modifiers.new("Armature", "ARMATURE").object = rig
    return obj


def bake(rig, all_clips):
    rest = {b.name: b.matrix_local.to_3x3() for b in rig.data.bones}
    rig.animation_data_create()
    for c_ in all_clips:
        act = bpy.data.actions.new(c_["name"])
        act.use_fake_user = True
        n = c_["frames"]
        quats = {b: [] for b in ORDER}
        loc = []
        for f in range(n + 1):
            t = (f % n if c_["loop"] else f) / FPS
            ch = c_["pose"](t)
            D = solve(ch)
            Db = {b: TO_BL @ D[b] @ TO_BL.transposed() for b in ORDER}
            for b in ORDER:
                p = PARENT[b]
                L = rest[b].transposed() @ (Db[p].transposed() if p else I3) @ Db[b] @ rest[b]
                q = L.to_quaternion()
                if quats[b] and quats[b][-1].dot(q) < 0:
                    q.negate()
                quats[b].append(q)
            loc.append(rest["Hips"].transposed() @ (TO_BL @ (ch["hip"] - REST["Hips"][0])))
        for b in ORDER:
            path = f'pose.bones["{b}"].rotation_quaternion'
            for i in range(4):
                _curve(act, path, i, b, [q[i] for q in quats[b]])
        for i in range(3):
            _curve(act, 'pose.bones["Hips"].location', i, "Hips", [v[i] for v in loc])
        act.use_frame_range = True
        act.frame_start, act.frame_end = 0, n
    rig.animation_data.action = bpy.data.actions["Idle"]


def _curve(act, path, i, group, values):
    fc = act.fcurves.new(path, index=i, action_group=group)
    fc.keyframe_points.add(len(values))
    fc.keyframe_points.foreach_set("co", [x for f, v in enumerate(values) for x in (f, v)])
    for kp in fc.keyframe_points:
        kp.interpolation = "LINEAR"
    fc.update()


def export(path, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0, path_mode="AUTO")


def paths(root):
    return {"blend": root / "ArtSource" / "Characters" / "SoldierAnimations.blend",
            "fbx": root / "Assets" / "Wildstone" / "Art" / "SoldierAnimations.fbx",
            "sidecar": root / "Assets" / "Wildstone" / "Art" / "SoldierAnimations.clips.json",
            "manifest": root / "ArtSource" / "Characters" / "soldier-anim-manifest.json",
            "previews": root / "ArtSource" / "Previews" / "Anim",
            "soldier": root / "Assets" / "Wildstone" / "Art" / "Soldier.fbx"}


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def git(*args):
    try:
        return subprocess.run(["git", "--no-optional-locks", "-C", str(FORGE), *args], capture_output=True,
                              text=True, check=True).stdout
    except (OSError, subprocess.CalledProcessError):
        return ""


REUSED = {
    "stages/anim.py": ["_sway (every periodic channel via wv)", "_yaw (pelvis/torso/head counter-rotation)",
                       "WALK_HIP/WALK_KNEE/WALK_ANKLE and RUN_HIP/RUN_KNEE/RUN_ANKLE clinical gait curves "
                       "(swing-foot arc and foot pitch as FK on this leg)",
                       "_declared (C1 quaternion spline for key-pose rotation channels)",
                       "OVERLAP idea (interior-key shift per channel)", "twist (arm twist report via the test)"],
    "tests/test_anim_quality.py": ["_pose", "_hidden_roll", "_loop_hitch", "arm_twist", "twist_issues",
                                   "SEAM_TOL", "JERK_TOL", "HITCH_TOL", "HIDDEN_ROLL_TOL"],
    "tests/asset_checks.py": ["loop_seam_distance", "max_point_distance"],
}
WRITTEN = ["bake(): forge bake() is one-timeline/forge-rig specific (A62/T90 arm references, local-Euler "
           "tables); here every clip is its own action keyed per frame as quaternions from world deltas",
           "two-bone IK with pole, clavicle reach, forearm pronation split (after forge _plant_feet's solver math)",
           "exact ground-locked stance with heel/ball roll; swing = forge FK arc with C1 endpoint/tangent fixes",
           "ballistic run hip bounce, finger curl presets, weapon-socket hand targets, tilted wall-run contact frame",
           "speed measurement from planted sole points (forge _stride_travel idea, in-place)"]


def build(root):
    P = paths(root)
    all_clips = clips()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS
    rig = make_rig()
    prox = proxy(rig)
    bake(rig, all_clips)
    for p in (P["blend"], P["fbx"]):
        p.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(P["blend"]), compress=True)
    export(P["fbx"], [rig, prox])
    table, failures = [], {}
    for c_ in all_clips:
        rows = sample(c_)
        m = measure(c_, rows)
        bad = gates(c_, m, rows)
        if bad:
            failures[c_["name"]] = bad
        table.append({"name": c_["name"], "loop": c_["loop"], "duration": round(c_["frames"] / FPS, 6),
                      "frames": c_["frames"], "speed": m["speed"] if c_["travel"] is not None else 0.0,
                      "mask": c_["mask"], "additive": c_["additive"],
                      "events": [{"time": round(t, 4), "name": n} for t, n in c_["events"]],
                      "_m": m, "_stride": c_.get("stride")})
    sidecar = [{k: v for k, v in row.items() if not k.startswith("_") and k != "frames"} for row in table]
    P["sidecar"].write_text(json.dumps({"clips": sidecar}, indent=1) + "\n", encoding="utf-8")
    print_table(table)
    status = git("status", "--porcelain")
    manifest = {
        "generator": "Tools/Assets/soldier_clips.py", "generator_sha256": sha(__file__),
        "skeleton_sha256": sha(HERE / "soldier_skeleton.py"), "blender": bpy.app.version_string, "fps": FPS,
        "forge": {"path": str(FORGE), "commit": git("rev-parse", "HEAD").strip(),
                  "dirty_entries": len([ln for ln in status.splitlines() if ln.strip()]),
                  "files": {f: sha(FORGE / f) for f in REUSED}, "reused": REUSED, "written_here": WRITTEN},
        "provenance": "Original procedural keyframes; forge curve helpers are the user's own tooling; no mocap, "
                      "no generated motion, no third-party animation data",
        "export": {"axis_forward": "-Z", "axis_up": "Y", "apply_scale_options": "FBX_SCALE_ALL", "add_leaf_bones": False,
                   "primary_bone_axis": "Y", "secondary_bone_axis": "X", "bake_anim_use_all_actions": True,
                   "bake_anim_simplify_factor": 0, "take_names": "SoldierRig|<Clip>"},
        "clips": [{**{k: v for k, v in row.items() if not k.startswith("_")},
                   "stride_m": round(row["_stride"], 3) if row["_stride"] else None,
                   "slip_cm": round(row["_m"]["slip"] * 100, 2), "min_floor_m": round(row["_m"]["floor"], 4),
                   "min_hips_m": round(row["_m"]["hips"], 3), "max_leg_reach": round(row["_m"]["reach"], 3),
                   "max_forearm_pronation_deg": round(row["_m"]["pronation"], 1)} for row in table],
        "hashes": {"blend": sha(P["blend"]), "fbx": sha(P["fbx"]), "sidecar": sha(P["sidecar"])},
        "gate_failures": failures,
    }
    P["manifest"].write_text(json.dumps(manifest, indent=1) + "\n", encoding="utf-8")
    if failures:
        print("GATE FAILURES:", json.dumps(failures, indent=1))
    return not failures


def print_table(table):
    print(f"{'clip':11s} {'f':>4s} {'dur':>6s} loop {'speed':>6s} {'stride':>6s} {'slip':>5s} {'floor':>6s} "
          f"{'hips':>5s} {'reach':>5s} {'pron':>5s} mask")
    for r in table:
        m = r["_m"]
        print(f"{r['name']:11s} {r['frames']:4d} {r['duration']:6.3f} {str(r['loop'])[0]:>4s} {r['speed']:6.2f} "
              f"{(r['_stride'] or 0):6.2f} {m['slip'] * 100:5.2f} {m['floor']:6.3f} {m['hips']:5.2f} "
              f"{m['reach']:5.3f} {m['pronation']:5.0f} {r['mask']}{' add' if r['additive'] else ''}")


# --- verify: re-import the FBX ----------------------------------------------------------
def verify(root):
    import test_anim_quality as tq
    from asset_checks import loop_seam_distance, max_point_distance
    P = paths(root)
    side = json.loads(P["sidecar"].read_text(encoding="utf-8"))["clips"]
    by_name = {c["name"]: c for c in clips()}
    problems = []
    names = [r["name"] for r in side]
    if tuple(names) != CLIP_NAMES:
        problems.append(f"sidecar clips {names}")
    for r in side:
        assert set(r) == {"name", "loop", "duration", "speed", "mask", "additive", "events"}, r
        if r["mask"] not in ("full", "upper", "lower") or not all(0 <= e["time"] <= r["duration"] for e in r["events"]):
            problems.append(f"{r['name']}: bad sidecar row")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS
    bpy.ops.import_scene.fbx(filepath=str(P["fbx"]), anim_offset=0.0)
    rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    acts = {a.name.split("|")[-1]: a for a in bpy.data.actions}
    if set(acts) != set(CLIP_NAMES):
        problems.append(f"FBX takes {sorted(acts)}")
    scene = bpy.context.scene
    print(f"{'clip':11s} {'frames':>6s} {'fbx-fk mm':>9s} {'seam deg':>8s} {'jerk':>5s} {'hitch':>5s} {'roll':>5s} twist(LA/UA/Hand max)")
    for r in side:
        name = r["name"]
        c_, act = by_name[name], acts.get(name)
        if act is None:
            continue
        rig.animation_data.action = act
        lo, hi = act.frame_range
        n = round(r["duration"] * FPS)
        if round(lo) != 0 or round(hi) != n or n < 6 or r["loop"] != c_["loop"]:
            problems.append(f"{name}: range {lo}-{hi} vs {n} frames")
        rows = sample(c_)
        m = measure(c_, rows)
        if c_["travel"] is not None and abs(m["speed"] - r["speed"]) > .01:
            problems.append(f"{name}: speed {m['speed']} vs sidecar {r['speed']}")
        problems += [f"{name}: {b}" for b in gates(c_, m, rows)]
        worst, samples = 0.0, []
        for f in range(n + 1):
            scene.frame_set(f)
            for b in ("Hips", "LeftFoot", "RightToes", "Head", "LeftHand", "RightIndexDistal", "LeftThumbDistal"):
                got = rig.matrix_world @ rig.pose.bones[b].head
                worst = max(worst, (Vector((-got.x, got.z, -got.y)) - rows[f][1][b]).length)
            samples.append(tq._pose(rig, scene, f))
        if worst > .002:
            problems.append(f"{name}: FBX differs from the solved pose by {worst * 1000:.1f} mm")
        frames = [s[0] for s in samples]
        quat = [s[1] for s in samples]
        seam = max(math.degrees(2 * math.acos(min(1, abs(a.dot(b))))) for a, b in zip(quat[0], quat[-1]))
        if r["loop"] and (seam >= .5 or loop_seam_distance(frames) > tq.SEAM_TOL):
            problems.append(f"{name}: FBX loop seam {seam:.2f} deg")
        steps = [max_point_distance(frames[i], frames[i - 1]) for i in range(1, len(frames))]
        mean = sum(steps) / len(steps)
        acc = [abs(steps[i] - steps[i - 1]) for i in range(1, len(steps))]
        jerk = max(acc) / mean if mean >= .002 and acc else 0.0
        hitch = tq._loop_hitch(frames)[0] if r["loop"] and mean >= .002 else 0.0
        roll = tq._hidden_roll(frames, quat, rig)[0]
        twists = tq.arm_twist(samples)
        tw = [max(v for k_, v in twists.items() if k_.endswith(part)) for part in ("LowerArm", "UpperArm", "Hand")]
        if jerk > tq.JERK_TOL:
            problems.append(f"{name}: jerk {jerk:.1f}x")
        if hitch > tq.HITCH_TOL:
            problems.append(f"{name}: loop hitch {hitch:.1f}x")
        if roll > tq.HIDDEN_ROLL_TOL:
            problems.append(f"{name}: hidden roll {roll:.0f} deg")
        print(f"{name:11s} {n:6d} {worst * 1000:9.2f} {seam:8.3f} {jerk:5.1f} {hitch:5.1f} {roll:5.0f} "
              f"{tw[0]:.0f}/{tw[1]:.0f}/{tw[2]:.0f}")
    problems += soldier_compat(P["soldier"], rig)
    print("\n" + ("VERIFY OK" if not problems else "VERIFY FAILED:\n  " + "\n  ".join(problems)))
    return not problems


def soldier_compat(path, rig):
    """Soldier.fbx (model lane) must carry the same skeleton: names, parents, rest matrices."""
    if not path.is_file():
        return [f"missing {path.name}"]
    ours = {b.name: (b.parent.name if b.parent else None, b.matrix_local.copy()) for b in rig.data.bones}
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    theirs = next(o for o in set(bpy.data.objects) - before if o.type == "ARMATURE")
    out, worst = [], 0.0
    for b in theirs.data.bones:
        if b.name in ours:
            if (b.parent.name if b.parent else None) != ours[b.name][0]:
                out.append(f"Soldier.fbx parent mismatch {b.name}")
            worst = max(worst, max(abs(x - y) for r1, r2 in zip(b.matrix_local, ours[b.name][1]) for x, y in zip(r1, r2)))
    missing = set(ours) - {b.name for b in theirs.data.bones}
    if missing:
        out.append(f"Soldier.fbx lacks {sorted(missing)}")
    print(f"Soldier.fbx rest compatibility: {len(ours) - len(missing)}/{len(ours)} bones, max rest-matrix delta {worst:.2e}")
    if worst > 1e-3:
        out.append(f"Soldier.fbx rest matrices differ by {worst:.3g}")
    return out


# --- previews ----------------------------------------------------------------------------
def weapon_mesh(kind):
    """Proxy weapon in weapon space (+Z muzzle, +Y top, grip anchor at the origin)."""
    if kind == "sword":
        parts = [((0, 0, .01), (.02, .02, .09)), ((0, 0, .53), (.012, .006, .42)), ((0, 0, .11), (.05, .015, .012))]
    elif kind == "pistol":
        parts = [((0, .09, .07), (.016, .025, .12)), ((0, 0, -.005), (.014, .05, .02))]
    else:
        parts = [((0, .12, .12), (.028, .035, .20)), ((0, .12, .48), (.012, .012, .18)), ((0, .09, -.17), (.022, .04, .12)),
                 ((0, .0, -.01), (.015, .055, .02)), ((0, .04, .08), (.016, .05, .025)), ((0, .17, .05), (.012, .015, .06)),
                 ((0, .085, .30), (.025, .02, .09))]
    verts, faces = [], []
    for c, h in parts:
        _box(verts, faces, _aabb(c, h))
    return verts, faces


def previews(root, only=None):
    P = paths(root)
    out_dir = P["previews"]
    out_dir.mkdir(parents=True, exist_ok=True)
    kinds = {"HoldPistol": "pistol", "AimPistol": "pistol", "HoldSword": "sword", "SwordSwing": "sword"}
    for c_ in clips():
        if only and c_["name"] not in only:
            continue
        bpy.ops.wm.open_mainfile(filepath=str(P["blend"]))
        scene = bpy.context.scene
        rig, prox = bpy.data.objects[RIG], bpy.data.objects[PROXY]
        rig.animation_data.action = bpy.data.actions[c_["name"]]
        # weapon proxy parented to the right hand at the WeaponHand_R socket
        wv_, wf = weapon_mesh(kinds.get(c_["name"], "rifle"))
        sock = SOCK["WeaponHand_R"]
        verts = [tuple(B(sock + GRIP_TO_HAND.transposed() @ Vector(v))) for v in wv_]
        mesh = bpy.data.meshes.new("PreviewWeapon")
        mesh.from_pydata(verts, [], [tuple(reversed(f)) for f in wf])
        wobj = bpy.data.objects.new("PreviewWeapon", mesh)
        scene.collection.objects.link(wobj)
        mat = bpy.data.materials.new("Gun")
        mat.diffuse_color = (.12, .12, .14, 1)
        mesh.materials.append(mat)
        scene.frame_set(0)
        bpy.context.view_layer.update()
        rest_bone = rig.data.bones["RightHand"]
        wobj.parent, wobj.parent_type, wobj.parent_bone = rig, "BONE", "RightHand"
        wobj.matrix_parent_inverse = (rig.matrix_world @ rest_bone.matrix_local @ Matrix.Translation((0, rest_bone.length, 0))).inverted()
        n = c_["frames"]
        count = 10
        frames = sorted({round(i * n / count) for i in range(count)} if c_["loop"] else {round(i * n / (count - 1)) for i in range(count)})
        snaps = []
        for f in frames:
            scene.frame_set(f)
            dg = bpy.context.evaluated_depsgraph_get()
            for obj in (prox, wobj):
                me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
                snaps.append((f, me, obj.matrix_world.copy()))
        prox.hide_render = wobj.hide_render = rig.hide_render = True
        spacing = 1.1 if not c_["name"].startswith(("Death", "Slide")) else 2.0
        for row, turn in ((0, 0.0), (1, math.radians(55))):
            for i, f in enumerate(frames):
                for (ff, me, mw) in snaps:
                    if ff != f:
                        continue
                    o = bpy.data.objects.new("snap", me)
                    scene.collection.objects.link(o)
                    o.matrix_world = Matrix.Translation((0, i * spacing, -row * 2.3)) @ Matrix.Rotation(turn, 4, "Z") @ mw
            floor = bpy.data.meshes.new("floor")
            fl = [(-.7, -1.0, -.02), (-.7, len(frames) * spacing, -.02), (-.7, len(frames) * spacing, 0), (-.7, -1.0, 0)]
            floor.from_pydata([(x, y, z - row * 2.3) for x, y, z in fl], [], [(0, 1, 2, 3)])
            fo = bpy.data.objects.new("floor", floor)
            fm = bpy.data.materials.new("floor")
            fm.diffuse_color = (.35, .37, .4, 1)
            floor.materials.append(fm)
            scene.collection.objects.link(fo)
            for i, f in enumerate(frames):
                txt = bpy.data.curves.new("t", "FONT")
                txt.body = str(f)
                txt.size = .16
                to = bpy.data.objects.new("t", txt)
                scene.collection.objects.link(to)
                to.location = (.9, i * spacing - .1, -row * 2.3 + 1.95)
                to.rotation_euler = (math.pi / 2, 0, math.pi / 2)
        cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
        scene.collection.objects.link(cam)
        width = len(frames) * spacing + .6
        cam.data.type, cam.data.ortho_scale = "ORTHO", width
        cam.location = (12, width / 2 - 1.0 + .3, -.15)
        cam.rotation_euler = (math.pi / 2, 0, math.pi / 2)
        scene.camera = cam
        r = scene.render
        r.engine = "BLENDER_WORKBENCH"
        r.resolution_x, r.resolution_y = 2000, int(2000 * 4.7 / width)
        scene.display.shading.light, scene.display.shading.color_type = "STUDIO", "MATERIAL"
        scene.display.shading.show_shadows = False
        scene.display.shading.show_cavity = True
        r.image_settings.file_format = "PNG"
        r.filepath = str(out_dir / (c_["name"] + ".png"))
        bpy.ops.render.render(write_still=True)
        print(r.filepath)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=["build", "verify", "previews"])
    parser.add_argument("--output-root", required=True, type=Path)
    parser.add_argument("--clips", help="previews only: comma-separated clip names")
    args = parser.parse_args()
    root = args.output_root.resolve()
    assert root == Path(__file__).resolve().parents[2] and (root / "AGENTS.md").is_file(), root
    if args.mode == "build":
        ok = build(root)
    elif args.mode == "verify":
        ok = verify(root)
    else:
        previews(root, set(args.clips.split(",")) if args.clips else None)
        ok = True
    sys.exit(0 if ok else 1)

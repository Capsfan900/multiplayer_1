"""Shared Wildstone soldier skeleton: the contract between the model and animation builds.

Coordinates are Unity metres in a T-pose bind: right +X, up +Y, forward +Z, feet at y=0.
Bone names are Unity HumanBodyBones names so Humanoid auto-mapping is exact.
Soldier exports do NOT use the legacy (X,-Z,Y) + import-mirror convention of
build_assets.py: a mirrored root breaks Humanoid avatars. Use to_blender() instead,
whose FBX handedness flip lands the right hand on Unity +X with no import fix.
Pure Python: importable from bpy scripts and plain tooling alike.
"""

HEIGHT = 1.787   # helmet top; matches the existing human and the 1.8 m capsule
EYE = 1.65       # authoritative standing eye (capsule 1.8 - 0.15)

# (name, parent, head, tail) for the centre line; sides are generated below.
_CENTRE = [
    ("Hips", None, (0, .98, 0), (0, 1.08, 0)),
    ("Spine", "Hips", (0, 1.08, 0), (0, 1.22, -.005)),
    ("Chest", "Spine", (0, 1.22, -.005), (0, 1.36, -.01)),
    ("UpperChest", "Chest", (0, 1.36, -.01), (0, 1.49, -.01)),
    ("Neck", "UpperChest", (0, 1.49, -.01), (0, 1.58, .005)),
    ("Head", "Neck", (0, 1.58, .005), (0, HEIGHT, .005)),
]

# Right side (+X); the left side mirrors X.
_SIDE = [
    ("Shoulder", "UpperChest", (.03, 1.46, -.01), (.17, 1.47, -.01)),
    ("UpperArm", "Shoulder", (.17, 1.47, -.01), (.45, 1.47, -.01)),
    ("LowerArm", "UpperArm", (.45, 1.47, -.01), (.71, 1.47, -.01)),
    ("Hand", "LowerArm", (.71, 1.47, -.01), (.80, 1.47, -.01)),
    ("UpperLeg", "Hips", (.095, .95, 0), (.095, .52, .005)),
    ("LowerLeg", "UpperLeg", (.095, .52, .005), (.095, .09, -.02)),
    ("Foot", "LowerLeg", (.095, .09, -.02), (.095, .03, .12)),
    ("Toes", "Foot", (.095, .03, .12), (.095, .02, .20)),
]

# Finger chains, right hand; palm faces down (-Y) in the T-pose with the thumb forward (+Z).
# (digit, knuckle, [segment lengths]); segments run along +X except the thumb.
_FINGERS = [
    ("Index", (.80, 1.470, .020), (.045, .026, .022)),
    ("Middle", (.80, 1.470, .000), (.049, .030, .024)),
    ("Ring", (.795, 1.467, -.020), (.045, .028, .022)),
    ("Little", (.785, 1.462, -.037), (.036, .021, .018)),
]
_THUMB = [(.725, 1.455, .020), (.755, 1.450, .052), (.778, 1.447, .072), (.797, 1.445, .088)]
_SEGMENTS = ("Proximal", "Intermediate", "Distal")

# Sockets: (name, parent bone, position, forward, up). Forward runs wrist->knuckles (the barrel
# follows the metacarpals); up is the thumb/index side (the weapon top), so an authored weapon's
# grip (+Z barrel, +Y up, handle along -Y) runs through the fist along the knuckle line.
# The hand position is the axis of a 3.5 cm grip touching the palm (build_soldier.grip_solve).
SOCKETS = [
    ("WeaponHand_R", "RightHand", (.775, 1.434, -.01), (1, 0, 0), (0, 0, 1)),
    ("WeaponHand_L", "LeftHand", (-.775, 1.434, -.01), (-1, 0, 0), (0, 0, 1)),
    ("HeadSocket", "Head", (0, EYE, .09), (0, 0, 1), (0, 1, 0)),
]


def _mirror(p):
    return (-p[0], p[1], p[2])


def bones():
    """Ordered (name, parent, head, tail) in Unity coordinates; parents precede children."""
    out = list(_CENTRE)
    for side, flip in (("Left", _mirror), ("Right", lambda p: p)):
        for name, parent, head, tail in _SIDE:
            parent_name = parent if parent in ("UpperChest", "Hips") else side + parent
            out.append((side + name, parent_name, flip(head), flip(tail)))
        hand = side + "Hand"
        for digit, knuckle, lengths in _FINGERS:
            start, parent_name = knuckle, hand
            for segment, length in zip(_SEGMENTS, lengths):
                end = (start[0] + length, start[1], start[2])
                name = f"{side}{digit}{segment}"
                out.append((name, parent_name, flip(start), flip(end)))
                start, parent_name = end, name
        parent_name = hand
        for segment, (start, end) in zip(_SEGMENTS, zip(_THUMB, _THUMB[1:])):
            name = f"{side}Thumb{segment}"
            out.append((name, parent_name, flip(start), flip(end)))
            parent_name = name
    return out


def to_blender(p):
    """Unity (x, y, z) -> Blender (-x, -z, y). FBX export: forward -Z, up Y, no import mirror."""
    x, y, z = p
    return (-x, -z, y)


HUMANOID_REQUIRED = ("Hips", "Spine", "Head", "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                     "RightUpperLeg", "RightLowerLeg", "RightFoot", "LeftUpperArm", "LeftLowerArm",
                     "LeftHand", "RightUpperArm", "RightLowerArm", "RightHand")

if __name__ == "__main__":
    table = bones()
    names = [b[0] for b in table]
    assert len(names) == len(set(names)), "duplicate bone"
    seen = set()
    for name, parent, _, _ in table:
        assert parent is None or parent in seen, (name, parent)
        seen.add(name)
    assert set(HUMANOID_REQUIRED) <= seen
    assert all(s[1] in seen for s in SOCKETS)
    right = dict((b[0], b) for b in table)["RightHand"]
    assert right[2][0] > 0 and to_blender(right[2])[0] < 0, "right hand must be Unity +X"
    print(f"{len(table)} bones OK")

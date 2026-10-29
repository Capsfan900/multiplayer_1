"""Wildstone editable assets. Run with the isolated ai_skelly_tool bpy Python.

All authored coordinates are Unity metres (right X, up Y, forward Z).
Blender coordinates are (X, -Z, Y); FBX export uses forward -Z, up Y.
Only --output-root may receive generated files. Existing Unity .meta files remain.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path

import bpy
from mathutils import Euler, Vector


def xyz(point):
    x, y, z = point
    return x, -z, y


def clean():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for material in list(bpy.data.materials):
        bpy.data.materials.remove(material)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)


def material(name, color):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = 0.82
    return mat


def empty(name, point, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    if parent:
        obj.parent = parent
    obj.location = xyz(point)
    return obj


def box(name, center, size, mat, parent=None, soft=True):
    bpy.ops.mesh.primitive_cube_add(size=1, location=xyz(center))
    obj = bpy.context.object
    obj.name = name
    obj.scale = xyz(size)
    obj.scale = tuple(abs(v) for v in obj.scale)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    if soft:
        bevel = obj.modifiers.new("SoftEdge", "BEVEL")
        bevel.width = min(min(size) * .12, .012)
        bevel.segments = 2
        normal = obj.modifiers.new("FaceNormals", "WEIGHTED_NORMAL")
        normal.keep_sharp = True
    if parent:
        world = obj.matrix_world.copy()
        obj.parent = parent
        obj.matrix_world = world
    return obj


def ellipsoid(name, center, radii, mat, parent=None, segments=12, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=xyz(center))
    obj = bpy.context.object
    obj.name = name
    obj.scale = (radii[0], radii[2], radii[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    if parent:
        world = obj.matrix_world.copy()
        obj.parent = parent
        obj.matrix_world = world
    return obj


def tapered_box(name, stations, mat, parent=None):
    """Four-corner cross sections (z, center_y, width, height) in Unity metres."""
    vertices = []
    for z, cy, width, height in stations:
        vertices.extend(xyz((x * width / 2, cy + y * height / 2, z))
                        for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1)))
    count = len(stations)
    faces = [(3, 2, 1, 0), tuple(4 * (count - 1) + i for i in range(4))]
    for section in range(count - 1):
        a, b = 4 * section, 4 * (section + 1)
        faces += [(a + i, a + (i + 1) % 4, b + (i + 1) % 4, b + i) for i in range(4)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("SoftEdge", "BEVEL")
    bevel.width = .008
    bevel.segments = 2
    obj.modifiers.new("FaceNormals", "WEIGHTED_NORMAL")
    if parent:
        obj.parent = parent
    return obj


def arc_ring(name, center, outer, inner, mat, start, sweep, parent=None, depth=.045, steps=12):
    x, y, z = center
    vertices = []
    for offset in (-depth / 2, depth / 2):
        for radius in (outer, inner):
            for i in range(steps + 1):
                angle = start + sweep * i / steps
                vertices.append(xyz((x + math.cos(angle) * radius, y + math.sin(angle) * radius, z + offset)))
    stride = steps + 1
    faces = []
    for row, other in ((0, 1), (0, 2), (1, 3), (2, 3)):
        for i in range(steps):
            faces.append((row * stride + i, row * stride + i + 1, other * stride + i + 1, other * stride + i))
    faces += [(0, stride, 3 * stride, 2 * stride),
              (steps, 2 * stride + steps, 3 * stride + steps, stride + steps)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("SoftEdge", "BEVEL")
    bevel.width = .005
    bevel.segments = 2
    obj.modifiers.new("FaceNormals", "WEIGHTED_NORMAL")
    if parent:
        obj.parent = parent
    return obj


def cylinder(name, a, b, radius, mat, parent=None, sides=12, radius_end=None):
    radius_end = radius if radius_end is None else radius_end
    start, end = Vector(xyz(a)), Vector(xyz(b))
    direction = end - start
    assert direction.length > 0
    tangent = direction.normalized()
    side = tangent.cross(Vector((0, 0, 1)))
    if side.length < 0.1:
        side = tangent.cross(Vector((1, 0, 0)))
    side.normalize()
    up = tangent.cross(side).normalized()
    vertices = []
    for center, r in ((start, radius), (end, radius_end)):
        for i in range(sides):
            angle = 2 * math.pi * i / sides
            vertices.append(tuple(center + r * (math.cos(angle) * side + math.sin(angle) * up)))
    faces = [tuple(reversed(range(sides))), tuple(range(sides, 2 * sides))]
    faces += [(i, (i + 1) % sides, (i + 1) % sides + sides, i + sides) for i in range(sides)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    if parent:
        world = obj.matrix_world.copy()
        obj.parent = parent
        obj.matrix_world = world
    return obj


def rounded_limb(name, a, b, radius_start, radius_end, mat, sides=12):
    start, end = Vector(xyz(a)), Vector(xyz(b))
    axis = (end - start).normalized()
    right = axis.cross(Vector((0, 0, 1)))
    if right.length < .1:
        right = axis.cross(Vector((1, 0, 0)))
    right.normalize()
    outward = axis.cross(right).normalized()
    rows = ((-.07, radius_start * .05), (-.04, radius_start * .65), (.02, radius_start),
            (.48, (radius_start + radius_end) * .52), (.98, radius_end),
            (1.04, radius_end * .65), (1.07, radius_end * .05))
    vertices = []
    for t, radius in rows:
        center = start.lerp(end, t)
        for i in range(sides):
            angle = math.tau * i / sides
            vertices.append(tuple(center + radius * (math.cos(angle) * right + math.sin(angle) * outward)))
    faces = [tuple(reversed(range(sides))), tuple(range((len(rows) - 1) * sides, len(rows) * sides))]
    for row in range(len(rows) - 1):
        for i in range(sides):
            j = (i + 1) % sides
            faces.append((row * sides + i, row * sides + j, (row + 1) * sides + j, (row + 1) * sides + i))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def tailored_body(mat):
    # One continuous hip/waist/chest shape keeps the target envelope legible.
    sections = ((.77, .125, .095), (.88, .175, .12), (1.02, .17, .115),
                (1.12, .165, .11), (1.25, .205, .13), (1.37, .225, .125),
                (1.46, .16, .09))
    sides = 16
    vertices = []
    for y, width, depth in sections:
        for i in range(sides):
            angle = math.tau * i / sides
            vertices.append(xyz((math.cos(angle) * width, y, math.sin(angle) * depth)))
    faces = [tuple(reversed(range(sides))), tuple(range((len(sections) - 1) * sides, len(sections) * sides))]
    for row in range(len(sections) - 1):
        for i in range(sides):
            j = (i + 1) % sides
            faces.append((row * sides + i, row * sides + j, (row + 1) * sides + j, (row + 1) * sides + i))
    mesh = bpy.data.meshes.new("TailoredSuit")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("TailoredSuit", mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def bored_cup(name, center, mat_outer, mat_inner, parent=None, sides=24):
    x, y, _ = center
    rings = ((.29, .15), (.54, .19), (.54, .15), (.40, .09))
    vertices = [xyz((x + math.cos(math.tau * i / sides) * radius,
                     y + math.sin(math.tau * i / sides) * radius, z))
                for z, radius in rings for i in range(sides)]
    faces = []
    for row in range(3):
        for i in range(sides):
            j = (i + 1) % sides
            faces.append((row * sides + i, row * sides + j, (row + 1) * sides + j, (row + 1) * sides + i))
    faces.append(tuple(range(3 * sides, 4 * sides)))  # recessed inner back, not a front cap
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat_outer)
    obj.data.materials.append(mat_inner)
    for polygon in obj.data.polygons:
        if polygon.index >= 2 * sides:
            polygon.material_index = 1
    if parent:
        obj.parent = parent
    return obj


def ring(name, center, outer, inner, mat, axis="z", parent=None, sides=20, depth=.018):
    assert outer > inner > 0
    x, y, z = center
    def point(angle, radius, offset):
        u, v = math.cos(angle) * radius, math.sin(angle) * radius
        return (x + u, y + v, z + offset) if axis == "z" else (x + u, y + offset, z + v)
    vertices = []
    for offset in (-depth / 2, depth / 2):
        for radius in (outer, inner):
            for i in range(sides):
                vertices.append(xyz(point(2 * math.pi * i / sides, radius, offset)))
    faces = []
    for row, other in ((0, 1), (0, 2), (1, 3), (2, 3)):
        for i in range(sides):
            j = (i + 1) % sides
            faces.append((row * sides + i, row * sides + j, other * sides + j, other * sides + i))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    if parent:
        world = obj.matrix_world.copy()
        obj.parent = parent
        obj.matrix_world = world
    return obj


def save_asset(root, name, objects, animated=False):
    source = root / "ArtSource" / (name + ".blend")
    exported = root / "Assets" / "Wildstone" / "Art" / (name + ".fbx")
    source.parent.mkdir(parents=True, exist_ok=True)
    exported.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(source), compress=True)
    export(exported, objects, animated)
    return exported


def export(path, objects, animated=False):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
        bake_anim=animated, bake_anim_use_all_actions=animated,
        bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0,
        primary_bone_axis="Y", secondary_bone_axis="X", path_mode="AUTO",
    )


def fixture(root):
    clean()
    ivory = material("Ivory", (0.74, 0.72, 0.67))
    coal = material("Graphite", (0.10, 0.11, 0.12))
    origin = empty("AxisFixture", (0, 0, 0))
    bar = box("OneMetreBar", (0.5, 0.10, 0.0), (1.0, 0.2, 0.1), ivory, origin, soft=False)
    tab = box("PositiveZTab", (0.82, 0.25, 0.10), (0.12, 0.10, 0.12), coal, origin, soft=False)
    grip = empty("Grip", (0.12, 0.10, -0.05), origin)
    muzzle = empty("Muzzle", (0.95, 0.10, 0.05), origin)
    source = root / "ArtSource" / "AxisFixture.blend"
    exported = root / "Assets" / "Wildstone" / "Art" / "AxisFixture.fbx"
    source.parent.mkdir(parents=True, exist_ok=True)
    exported.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(source), compress=True)
    export(exported, [origin, bar, tab, grip, muzzle])
    # Reimport confirms the FBX contains geometry and named anchors. Unity must
    # separately confirm its importer sees the expected metre, axis and handedness.
    clean()
    bpy.ops.import_scene.fbx(filepath=str(exported), use_anim=False)
    names = {obj.name for obj in bpy.data.objects}
    assert {"OneMetreBar", "PositiveZTab", "Grip", "Muzzle"} <= names, names
    assert len(bpy.data.objects["OneMetreBar"].data.vertices) == 8
    bpy.context.view_layer.update()
    for name, expected in (("Grip", (.12, .10, -.05)), ("Muzzle", (.95, .10, .05))):
        actual = from_xyz(bpy.data.objects[name].matrix_world.translation)
        assert all(abs(a - b) < 1e-4 for a, b in zip(actual, expected)), (name, actual)
    limits = mesh_bounds([bpy.data.objects[name] for name in ("OneMetreBar", "PositiveZTab")])
    for actual, expected in zip((*limits[0], *limits[1]), (0, 0, -.05, 1, .30, .16)):
        assert abs(actual - expected) < 1e-4, limits
    result = {
        "source": str(source), "fbx": str(exported),
        "expected_unity_bounds": {"min": [0, 0, -0.05], "max": [1, 0.30, 0.16]},
        "expected_unity_grip": [0.12, 0.10, -0.05],
        "expected_unity_muzzle": [0.95, 0.10, 0.05],
        "expected_unity_positive_z_tab_center": [0.82, 0.25, 0.10],
        "reimport_names": sorted(names),
    }
    print(json.dumps(result, indent=2))


def from_xyz(point):
    return point.x, point.z, -point.y


def mesh_bounds(objects):
    points = [from_xyz(obj.matrix_world @ vertex.co) for obj in objects if obj.type == "MESH" for vertex in obj.data.vertices]
    assert points, "No exported mesh vertices"
    return tuple(min(p[i] for p in points) for i in range(3)), tuple(max(p[i] for p in points) for i in range(3))


def palette():
    return {
        "ivory": material("MatteIvory", (.76, .73, .66)),
        "graphite": material("Graphite", (.105, .115, .125)),
        "mid": material("SoftGrey", (.32, .34, .35)),
        "cyan": material("SignalCyan", (.13, .68, .74)),
        "amber": material("SignalAmber", (.95, .43, .12)),
        "coral": material("SignalCoral", (.84, .30, .24)),
        "violet": material("SignalViolet", (.55, .39, .80)),
        "suit": material("MatteCharcoalSuit", (.19, .21, .22)),
        "shade": material("DeepCharcoal", (.105, .12, .13)),
        "head": material("HeadValue", (.28, .30, .31)),
        "mark": material("TeamMark", (.70, .70, .66)),
    }


def weapon(root, name):
    clean()
    m = palette()
    base = empty(name, (0, 0, 0))
    parts = [base]
    def add(obj):
        parts.append(obj)
        return obj
    def B(label, center, size, key):
        return add(box(label, center, size, m[key], base))
    def C(label, a, b, r, key, end=None):
        return add(cylinder(label, a, b, r, m[key], base, radius_end=end))
    def E(label, center, radii, key):
        return add(ellipsoid(label, center, radii, m[key], base))
    def R(label, center, outer, inner, key, axis="z", depth=.018):
        return add(ring(label, center, outer, inner, m[key], axis, base, depth=depth))
    def T(label, stations, key):
        return add(tapered_box(label, stations, m[key], base))
    if name == "LineDriver":
        T("Receiver", [(-.29, .08, .14, .15), (-.15, .10, .19, .20), (.07, .10, .17, .16)], "graphite")
        T("UpperBridge", [(-.16, .19, .14, .05), (.10, .21, .20, .05), (.36, .18, .16, .035)], "ivory")
        for side in (-1, 1):
            rail = T("SplitRail", [(-.04, .13, .07, .075), (.31, .15, .055, .055), (.64, .15, .035, .035)], "ivory")
            rail.location.x = side * .095
            B("RailSignal", (side * .065, .142, .34), (.009, .012, .36), "cyan")
        C("RearCell", (0, .13, -.30), (0, .13, -.18), .078, "mid")
        T("StockTop", [(-.51, .11, .14, .045), (-.35, .08, .13, .05), (-.21, .08, .15, .06)], "ivory")
        B("StockHeel", (0, -.005, -.50), (.18, .18, .045), "graphite")
        muzzle = (0, .13, .65)
    elif name == "PulseLobber":
        B("Receiver", (0, .07, -.10), (.20, .20, .33), "graphite")
        E("OrbChamber", (0, .17, .13), (.22, .21, .22), "amber")
        R("ChamberRearBand", (0, .17, -.035), .225, .185, "ivory", depth=.055)
        R("ChamberFrontBand", (0, .17, .29), .225, .185, "ivory", depth=.055)
        add(bored_cup("Bell", (0, .16, 0), m["ivory"], m["graphite"], base))
        R("DarkMuzzle", (0, .16, .54), .19, .15, "graphite", depth=.012)
        R("RecessedEmitter", (0, .16, .405), .067, .053, "amber", depth=.009)
        for side in (-1, 1):
            B("ChamberCradle", (side * .19, .055, .13), (.065, .055, .28), "ivory")
        muzzle = (0, .16, .55)
    elif name == "ReboundDisc":
        T("RearShell", [(-.39, .08, .20, .12), (-.24, .09, .22, .14), (-.02, .11, .15, .10)], "ivory")
        B("Neck", (0, .11, .02), (.17, .11, .23), "graphite")
        C("DiscCore", (0, .145, .31), (0, .17, .31), .22, "graphite")
        R("DiscSignal", (0, .174, .31), .19, .15, "coral", axis="y", depth=.009)
        for side in (-1, 1):
            fork = T("Fork", [(.09, .12, .065, .08), (.36, .12, .06, .07), (.58, .12, .045, .055)], "graphite")
            fork.location.x = side * .21
            B("ForkLip", (side * .21, .12, .56), (.07, .08, .055), "mid")
        muzzle = (0, .16, .58)
    elif name == "PushLens":
        T("Receiver", [(-.34, .08, .16, .13), (-.18, .08, .22, .17), (.02, .11, .16, .12)], "ivory")
        C("Neck", (0, .14, -.03), (0, .14, .18), .11, "graphite")
        R("OpenLensFrame", (0, .16, .39), .25, .205, "graphite", depth=.075)
        R("InnerField", (0, .16, .39), .208, .197, "violet", depth=.012)
        for side in (-1, 1):
            add(arc_ring("LensGuard", (0, .16, .39), .278, .226, m["ivory"],
                         -.52 if side == 1 else math.pi - .52, 1.04, base, depth=.085))
            C("FieldPost", (side * .19, .36, .39), (side * .19, .16, .39), .018, "violet")
        muzzle = (0, .16, .40)
    elif name == "Grenade":
        E("Core", (0, 0, .08), (.10, .10, .11), "graphite")
        R("GripBand", (0, 0, .08), .104, .089, "ivory", depth=.044)
        B("FuseTab", (0, .105, .08), (.065, .026, .13), "amber")
        muzzle = (0, 0, .19)
    elif name == "StunGun":
        B("Receiver", (0, .065, -.07), (.17, .13, .32), "ivory")
        for side in (-1, 1):
            C("Prong", (side * .07, .10, .08), (side * .07, .10, .37), .032, "graphite", .023)
            E("ProngTip", (side * .07, .10, .37), (.031, .031, .028), "cyan")
        B("WarningBar", (0, .14, -.07), (.12, .012, .12), "cyan")
        muzzle = (0, .10, .40)
    elif name == "LaserSword":
        C("GripBody", (0, 0, -.16), (0, 0, .12), .047, "graphite")
        R("Guard", (0, 0, .12), .092, .048, "ivory", depth=.035)
        C("BladeCore", (0, 0, .15), (0, 0, .91), .025, "cyan", .008)
        C("BladeSpine", (0, 0, .15), (0, 0, .88), .012, "ivory", .004)
        muzzle = (0, 0, .92)
    elif name == "LaserPistol":
        T("PistolSlide", [(-.27, .10, .13, .13), (.02, .115, .15, .14), (.31, .11, .12, .11)], "ivory")
        B("LowerFrame", (0, .005, -.055), (.12, .09, .34), "graphite")
        C("PistolEmitter", (0, .11, .25), (0, .11, .37), .047, "graphite")
        R("EmitterSignal", (0, .11, .375), .044, .025, "cyan", depth=.014)
        B("RearSight", (0, .205, -.20), (.095, .035, .035), "graphite")
        B("FrontSight", (0, .19, .27), (.027, .025, .035), "cyan")
        B("PowerCell", (0, -.20, -.18), (.07, .12, .07), "mid")
        muzzle = (0, .11, .39)
    elif name == "LaserShotgun":
        T("ShotgunReceiver", [(-.40, .11, .25, .16), (-.13, .12, .29, .18), (.18, .12, .25, .16)], "ivory")
        for side in (-1, 1):
            C("ShotgunBarrel", (side * .10, .12, .10), (side * .10, .12, .76), .073, "graphite")
            R("ShotgunEmitter", (side * .10, .12, .77), .074, .048, "amber", depth=.024)
        B("PumpForeend", (0, -.015, .32), (.28, .10, .28), "mid")
        B("ForeendSignal", (0, -.07, .34), (.13, .012, .15), "amber")
        T("ShoulderStock", [(-.67, .035, .20, .15), (-.48, .07, .16, .10), (-.36, .10, .17, .10)], "graphite")
        B("StockPad", (0, .035, -.68), (.23, .20, .05), "ivory")
        muzzle = (0, .12, .80)
    elif name == "LaserSniper":
        T("SniperReceiver", [(-.42, .10, .15, .13), (-.10, .11, .17, .16), (.24, .11, .14, .13)], "ivory")
        C("PrecisionBarrel", (0, .11, .19), (0, .11, 1.00), .043, "graphite")
        C("MuzzleBrake", (0, .11, .96), (0, .11, 1.10), .066, "mid")
        R("PrecisionEmitter", (0, .11, 1.105), .065, .033, "violet", depth=.014)
        B("ScopeMount", (0, .23, -.02), (.07, .09, .27), "graphite")
        C("ScopeTube", (0, .32, -.27), (0, .32, .29), .062, "graphite")
        R("ScopeFront", (0, .32, .30), .078, .050, "ivory", depth=.045)
        C("ScopeLens", (0, .32, .313), (0, .32, .319), .046, "violet")
        B("ScopeTurret", (0, .40, .01), (.09, .07, .09), "mid")
        T("PrecisionStock", [(-.71, .05, .17, .16), (-.48, .08, .11, .09), (-.34, .10, .14, .10)], "graphite")
        B("SniperMagazine", (0, -.06, -.02), (.10, .22, .12), "graphite")
        muzzle = (0, .11, 1.12)
    elif name == "BlinkPickup":
        R("BlinkOuter", (0, .15, 0), .15, .105, "ivory", axis="y", depth=.045)
        R("BlinkInner", (0, .18, 0), .092, .080, "violet", axis="y", depth=.01)
        for side in (-1, 1):
            B("BlinkNotch", (side * .14, .17, 0), (.035, .05, .052), "graphite")
        muzzle = (0, .15, .15)
    elif name == "ShieldPickup":
        E("ShieldBody", (0, .15, 0), (.14, .05, .16), "ivory")
        B("ShieldKeel", (0, .19, -.025), (.055, .035, .24), "graphite")
        R("ShieldStatus", (0, .207, 0), .082, .065, "cyan", axis="y", depth=.009)
        muzzle = (0, .15, .16)
    else:
        raise ValueError(name)
    if name not in ("BlinkPickup", "ShieldPickup", "Grenade"):
        B("PalmGrip", (0, -.095, -.18), (.09, .25, .09), "graphite")
        B("GripHeel", (0, -.225, -.205), (.105, .045, .10), "ivory")
    add(empty("Grip", (0, -.12, -.18) if name not in ("BlinkPickup", "ShieldPickup", "Grenade") else (0, 0, 0), base))
    add(empty("Muzzle", muzzle, base))
    if name not in ("BlinkPickup", "ShieldPickup"):
        add(empty("LeftHand", (-.075, -.045, -.02), base))
    return save_asset(root, name, parts)


def skinned(obj, rig, bone):
    group = obj.vertex_groups.new(name=bone)
    group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    world = obj.matrix_world.copy()
    obj.parent = rig
    obj.matrix_world = world
    modifier = obj.modifiers.new("Skin", "ARMATURE")
    modifier.object = rig
    return obj


def skin_body(obj, rig):
    pelvis = obj.vertex_groups.new(name="pelvis")
    spine = obj.vertex_groups.new(name="spine")
    for vertex in obj.data.vertices:
        y = from_xyz(vertex.co)[1]
        spine_weight = max(0, min(1, (y - .99) / .17))
        if spine_weight < 1:
            pelvis.add([vertex.index], 1 - spine_weight, "REPLACE")
        if spine_weight > 0:
            spine.add([vertex.index], spine_weight, "REPLACE")
    world = obj.matrix_world.copy()
    obj.parent = rig
    obj.matrix_world = world
    obj.modifiers.new("Skin", "ARMATURE").object = rig
    return obj


def human(root):
    clean()
    m = palette()
    bones = {
        "pelvis": ((0, .83, 0), (0, 1.04, 0), None),
        "spine": ((0, 1.04, 0), (0, 1.43, 0), "pelvis"),
        "neck": ((0, 1.43, 0), (0, 1.56, 0), "spine"),
        "head": ((0, 1.56, 0), (0, 1.76, 0), "neck"),
        "upper_arm.L": ((-.22, 1.39, 0), (-.39, 1.16, 0), "spine"),
        "forearm.L": ((-.39, 1.16, 0), (-.49, .96, 0), "upper_arm.L"),
        "hand.L": ((-.49, .96, 0), (-.51, .86, 0), "forearm.L"),
        "upper_arm.R": ((.22, 1.39, 0), (.39, 1.16, 0), "spine"),
        "forearm.R": ((.39, 1.16, 0), (.49, .96, 0), "upper_arm.R"),
        "hand.R": ((.49, .96, 0), (.51, .86, 0), "forearm.R"),
        "thigh.L": ((-.105, .84, 0), (-.115, .48, 0), "pelvis"),
        "shin.L": ((-.115, .48, 0), (-.115, .10, 0), "thigh.L"),
        "foot.L": ((-.115, .10, 0), (-.115, .045, .16), "shin.L"),
        "thigh.R": ((.105, .84, 0), (.115, .48, 0), "pelvis"),
        "shin.R": ((.115, .48, 0), (.115, .10, 0), "thigh.R"),
        "foot.R": ((.115, .10, 0), (.115, .045, .16), "shin.R"),
    }
    arm_data = bpy.data.armatures.new("WildstoneHumanRig")
    arm = bpy.data.objects.new("WildstoneHuman", arm_data)
    bpy.context.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, (head, tail, _) in bones.items():
        bone = arm_data.edit_bones.new(name)
        bone.head = xyz(head)
        bone.tail = xyz(tail)
    for name, (_, _, parent) in bones.items():
        if parent:
            arm_data.edit_bones[name].parent = arm_data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    objects = [arm]
    def skin(obj, bone):
        objects.append(skinned(obj, arm, bone))
    objects.append(skin_body(tailored_body(m["suit"]), arm))
    skin(rounded_limb("Neck", (0, 1.43, 0), (0, 1.56, 0), .063, .057, m["shade"]), "neck")
    skin(ellipsoid("Head", (0, 1.655, 0), (.112, .132, .108), m["head"]), "head")
    skin(box("FaceFacingStripe", (0, 1.655, .107), (.10, .034, .013), m["shade"]), "head")
    for side, label in ((-1, "L"), (1, "R")):
        def mirror(point): return (side * point[0], point[1], point[2])
        skin(ellipsoid("Shoulder" + label, mirror((.235, 1.36, 0)), (.074, .072, .085), m["suit"]), "upper_arm." + label)
        skin(rounded_limb("UpperArm" + label, mirror((.255, 1.37, 0)), mirror((.39, 1.15, 0)), .078, .064, m["suit"]), "upper_arm." + label)
        skin(rounded_limb("Forearm" + label, mirror((.39, 1.16, 0)), mirror((.49, .96, 0)), .068, .050, m["suit"]), "forearm." + label)
        skin(ellipsoid("Hand" + label, mirror((.50, .91, .01)), (.052, .072, .042), m["shade"]), "hand." + label)
        skin(rounded_limb("Thigh" + label, mirror((.105, .85, 0)), mirror((.115, .47, 0)), .105, .080, m["suit"]), "thigh." + label)
        skin(rounded_limb("Shin" + label, mirror((.115, .48, 0)), mirror((.115, .10, 0)), .081, .056, m["suit"]), "shin." + label)
        skin(ellipsoid("Foot" + label, mirror((.115, .065, .075)), (.079, .06, .14), m["shade"]), "foot." + label)
    # Four separate high-contrast symbols. Runtime selects exactly one TeamNN set.
    for team in range(1, 5):
        for face in (-1, 1):
            z = face * .135
            prefix = f"Team{team:02d}_{'Front' if face == 1 else 'Back'}"
            if team == 1:  # one long upright
                marks = [box(prefix, (0, 1.33, z), (.047, .135, .012), m["mark"])]
            elif team == 2:  # twin bars
                marks = [box(prefix + str(i), (i * .038, 1.33, z), (.034, .135, .012), m["mark"]) for i in (-1, 1)]
            elif team == 3:  # three bars, rising profile
                marks = [box(prefix + str(i), (i * .045, 1.33, z), (.032, .08 + (i + 1) * .033, .012), m["mark"]) for i in (-1, 0, 1)]
            else:  # open circle plus stem
                marks = [ring(prefix + "Ring", (0, 1.35, z), .065, .047, m["mark"], depth=.012),
                         box(prefix + "Stem", (0, 1.26, z), (.028, .08, .012), m["mark"])]
            for mark in marks:
                skin(mark, "spine")
    # Sockets follow the appropriate animated bones.
    for name, bone, point in (("WeaponHand_R", "hand.R", (.50, .94, .045)), ("WeaponHand_L", "hand.L", (-.50, .94, .045))):
        obj = empty(name, point)
        bpy.context.view_layer.update()
        world = obj.matrix_world.copy()
        obj.parent = arm
        obj.parent_type = "BONE"
        obj.parent_bone = bone
        obj.matrix_world = world
        objects.append(obj)
    animations(arm)
    # One editable master; four FBX variants each contain one front/back mark.
    source = root / "ArtSource" / "WildstoneHuman.blend"
    source.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(source), compress=True)
    variants = []
    for team in range(1, 5):
        name = "WildstoneHuman" if team == 1 else f"WildstoneHumanTeam{team:02d}"
        path = root / "Assets" / "Wildstone" / "Art" / (name + ".fbx")
        path.parent.mkdir(parents=True, exist_ok=True)
        selected = [obj for obj in objects if not obj.name.startswith("Team") or obj.name.startswith(f"Team{team:02d}_")]
        export(path, selected, animated=True)
        variants.append(path)
    return variants


def animations(arm):
    # Pose values are small rotations in the armature's Blender coordinate frame.
    clips = {
        "Idle": [(1, {}), (25, {})],
        "Run": [(1, {"thigh.L": .48, "thigh.R": -.48, "upper_arm.L": -.22, "upper_arm.R": .22}),
                (9, {"thigh.L": -.48, "thigh.R": .48, "upper_arm.L": .22, "upper_arm.R": -.22}),
                (17, {"thigh.L": .48, "thigh.R": -.48, "upper_arm.L": -.22, "upper_arm.R": .22})],
        "Strafe": [(1, {"thigh.L": .22, "thigh.R": -.22, "spine": .08}),
                   (9, {"thigh.L": -.22, "thigh.R": .22, "spine": -.08}),
                   (17, {"thigh.L": .22, "thigh.R": -.22, "spine": .08})],
        "Jump": [(1, {}), (9, {"thigh.L": .30, "thigh.R": .30, "shin.L": -.45, "shin.R": -.45}), (17, {})],
        "Slide": [(1, {}), (8, {"thigh.L": .75, "thigh.R": .75, "shin.L": -.9, "shin.R": -.9, "spine": .45}), (22, {})],
        "Aim": [(1, {"upper_arm.L": -1.2, "upper_arm.R": -1.2}),
                (25, {"upper_arm.L": -1.2, "upper_arm.R": -1.2})],
        "Death": [(1, {}), (20, {"spine": 1.15, "head": -.32, "upper_arm.L": .6, "upper_arm.R": -.6})],
    }
    arm.animation_data_create()
    for clip, frames in clips.items():
        action = bpy.data.actions.new(clip)
        action.use_fake_user = True
        arm.animation_data.action = action
        for frame, values in frames:
            for bone in arm.pose.bones:
                bone.rotation_mode = "QUATERNION"
                bone.rotation_quaternion = Euler((values.get(bone.name, 0), 0, 0)).to_quaternion()
            bpy.context.view_layer.update()
            if clip == "Aim":
                for name, direction in (("upper_arm.L", (-.03, -.26, -.04)), ("upper_arm.R", (.03, -.26, -.04)),
                                        ("forearm.L", (.02, -.22, .01)), ("forearm.R", (-.02, -.22, .01))):
                    bone = arm.pose.bones[name]
                    rest = (bone.bone.tail_local - bone.bone.head_local).normalized()
                    parent = bone.parent
                    parent_delta = parent.matrix.to_quaternion() @ parent.bone.matrix_local.to_quaternion().inverted()
                    basis = bone.bone.matrix_local.to_quaternion()
                    bone.rotation_quaternion = basis.inverted() @ parent_delta.inverted() @ rest.rotation_difference(Vector(direction)) @ basis
                    bpy.context.view_layer.update()
            for bone in arm.pose.bones:
                bone.keyframe_insert(data_path="rotation_quaternion", frame=frame, group=bone.name)
        assert action.frame_range[1] >= frames[-1][0]
    arm.animation_data.action = bpy.data.actions["Idle"]
    bpy.context.scene.frame_set(-1)
    for bone in arm.pose.bones:
        bone.rotation_quaternion = (1, 0, 0, 0)


def hands(root):
    clean()
    m = palette()
    base = empty("FirstPersonHands", (0, 0, 0))
    objects = [base]
    for side, label in ((-1, "L"), (1, "R")):
        objects.append(cylinder("Sleeve" + label, (side * .20, -.17, -.38), (side * .12, -.12, -.18), .085, m["mid"], base, radius_end=.06))
        objects.append(ellipsoid("Palm" + label, (side * .10, -.11, -.15), (.068, .044, .08), m["graphite"], base))
        for i in range(4):
            objects.append(cylinder("Finger" + label + str(i), (side * (.055 + i * .028), -.10, -.11),
                                    (side * (.055 + i * .028), -.11, -.015), .015, m["graphite"], base, sides=8))
        objects.append(empty("Grip_" + label, (side * .10, -.11, -.07), base))
    return save_asset(root, "FirstPersonHands", objects)


def verify_asset(path, kind):
    clean()
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=kind == "human")
    bpy.context.view_layer.update()
    objects = list(bpy.data.objects)
    meshes = [obj for obj in objects if obj.type == "MESH"]
    low, high = mesh_bounds(meshes)
    extents = [high[i] - low[i] for i in range(3)]
    assert all(.01 < extent < 2.1 for extent in extents), (path.name, low, high)
    names = {obj.name for obj in objects}
    triangles = sum(sum(len(poly.vertices) - 2 for poly in obj.data.polygons) for obj in meshes)
    assert triangles >= 12, (path.name, triangles)
    if kind == "weapon":
        assert {"Grip", "Muzzle"} <= names, (path.name, names)
        grip = from_xyz(bpy.data.objects["Grip"].matrix_world.translation)
        muzzle = from_xyz(bpy.data.objects["Muzzle"].matrix_world.translation)
        assert muzzle[2] > grip[2] + .1, (path.name, grip, muzzle)
        for anchor in ("Grip", "Muzzle"):
            forward = bpy.data.objects[anchor].matrix_world.to_quaternion() @ Vector((0, -1, 0))
            assert forward.normalized().dot(Vector((0, -1, 0))) > .99, (path.name, anchor, forward)
        assert len({mat.name for obj in meshes for mat in obj.data.materials}) >= 2
        distinctive = {
            "LineDriver": ("SplitRail", "SplitRail.001"),
            "PulseLobber": ("OrbChamber", "Bell"),
            "ReboundDisc": ("DiscCore", "Fork"),
            "PushLens": ("OpenLensFrame", "LensGuard"),
            "Grenade": ("Core", "FuseTab"),
            "StunGun": ("Prong", "ProngTip"),
            "LaserSword": ("BladeCore", "Guard"),
            "BlinkPickup": ("BlinkOuter", "BlinkNotch"),
            "ShieldPickup": ("ShieldBody", "ShieldStatus"),
            "LaserPistol": ("PistolSlide", "PistolEmitter"),
            "LaserShotgun": ("ShotgunBarrel", "PumpForeend"),
            "LaserSniper": ("PrecisionBarrel", "ScopeTube"),
        }
        assert all(any(name.startswith(piece) for name in names) for piece in distinctive[path.stem]), (path.name, names)
    elif kind == "human":
        rigs = [obj for obj in objects if obj.type == "ARMATURE"]
        assert len(rigs) == 1, (path.name, rigs)
        assert len(rigs[0].data.bones) >= 16
        assert {"WeaponHand_R", "WeaponHand_L"} <= names
        assert 1.74 < high[1] < 1.85 and 0 <= low[1] < .02, (low, high)
        unweighted = []
        for obj in meshes:
            for vertex in obj.data.vertices:
                if not vertex.groups or abs(sum(group.weight for group in vertex.groups) - 1) > .001:
                    unweighted.append((obj.name, vertex.index))
        assert not unweighted, (path.name, unweighted[:10])
        team = 1 if path.stem == "WildstoneHuman" else int(path.stem[-2:])
        for candidate in range(1, 5):
            front = any(name.startswith(f"Team{candidate:02d}_Front") for name in names)
            back = any(name.startswith(f"Team{candidate:02d}_Back") for name in names)
            assert front == back == (candidate == team), (path.name, candidate, front, back)
        clips = {action.name.split("|")[-1] for action in bpy.data.actions}
        assert {"Idle", "Run", "Strafe", "Jump", "Slide", "Aim", "Death"} <= clips, clips
        for action in bpy.data.actions:
            assert action.frame_range[1] - action.frame_range[0] >= 8, action.name
        rig = rigs[0]
        rig.animation_data_create()
        aim = next(action for action in bpy.data.actions if action.name.endswith("|Aim"))
        rig.animation_data.action = aim
        bpy.context.scene.frame_set(int(aim.frame_range[0]))
        bpy.context.view_layer.update()
        left = from_xyz(rig.pose.bones["hand.L"].tail)
        right = from_xyz(rig.pose.bones["hand.R"].tail)
        assert left[2] > .15 and right[2] > .15, (left, right)
    else:
        assert {"Grip_L", "Grip_R"} <= names
    return {"asset": path.name, "bounds_min": low, "bounds_max": high, "triangles": triangles,
            "mesh_count": len(meshes), "actions": sorted(action.name for action in bpy.data.actions)}


def build_laser_kit(root):
    exports = []
    for name in ("LaserPistol", "LaserShotgun", "LaserSniper"):
        path = weapon(root, name)
        result = verify_asset(path, "weapon")
        result["source_sha256"] = hashlib.sha256((root / "ArtSource" / (name + ".blend")).read_bytes()).hexdigest()
        result["fbx_sha256"] = hashlib.sha256(path.read_bytes()).hexdigest()
        exports.append(result)
    manifest = {"generator": "Tools/Assets/build_assets.py laser-kit", "blender": bpy.app.version_string,
                "provenance": "Original procedural laser pistol, twin-barrel shotgun and scoped sniper; no copied meshes or textures",
                "axis": "Unity (X,Y,Z) -> Blender (X,-Z,Y); FBX -Z forward, Y up; metres", "exports": exports}
    (root / "ArtSource" / "laser-kit-export-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(manifest, indent=2))


def build_all(root):
    output = []
    for name in ("LineDriver", "PulseLobber", "ReboundDisc", "PushLens", "Grenade", "StunGun", "LaserSword", "BlinkPickup", "ShieldPickup", "LaserPistol", "LaserShotgun", "LaserSniper"):
        path = weapon(root, name)
        output.append(verify_asset(path, "weapon"))
    for path in human(root):
        output.append(verify_asset(path, "human"))
    path = hands(root)
    output.append(verify_asset(path, "hands"))
    manifest = {
        "generator": "Tools/Assets/build_assets.py", "blender": bpy.app.version_string,
        "axis": "Unity (X,Y,Z) -> Blender (X,-Z,Y); FBX forward -Z up Y; metres",
        "provenance": "Original procedural geometry; approved local C3.1 and W01-W04 simple-v2 concept images as visual references; no copied source meshes or textures",
        "exports": [{"name": entry["asset"],
                     "source_sha256": hashlib.sha256((root / "ArtSource" / ("WildstoneHuman.blend" if entry["asset"].startswith("WildstoneHumanTeam") else Path(entry["asset"]).stem + ".blend")).read_bytes()).hexdigest(),
                     "fbx_sha256": hashlib.sha256((root / "Assets" / "Wildstone" / "Art" / entry["asset"]).read_bytes()).hexdigest(),
                     "triangles": entry["triangles"], "bounds_min": entry["bounds_min"], "bounds_max": entry["bounds_max"]}
                    for entry in output],
    }
    (root / "ArtSource" / "export-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(output, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=["fixture", "human", "all", "laser-kit"])
    parser.add_argument("--output-root", required=True, type=Path)
    args = parser.parse_args()
    root = args.output_root.resolve()
    assert root == Path(__file__).resolve().parents[2] and (root / "AGENTS.md").is_file(), root
    if args.mode == "fixture":
        fixture(root)
    elif args.mode == "human":
        print(json.dumps([verify_asset(path, "human") for path in human(root)], indent=2))
    elif args.mode == "laser-kit":
        build_laser_kit(root)
    else:
        build_all(root)

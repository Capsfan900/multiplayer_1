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
import bmesh  # available only after bpy initialises
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


# Weapon-kit geometry (2026-10-29). Technique adapted, not copied, from the read-only vibegame1
# Tools/weapon_models/build_laser_kit.py (inset, loft_z, side_slab, lathe_axis) and build_weapons.py
# (loft) at 2cb6e5760bccc0733f34b367be2f18c90b802766: chamfered lofted sections, side-profile slabs and
# lathed emitters. Rewritten for this file's Unity-metre convention; no source meshes or textures.
def polymesh(name, verts, faces, mat, parent=None, bevel=0.0, segments=2, recalc=True):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([xyz(v) for v in verts], [], faces)
    mesh.validate()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    if recalc:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4], ngon_method="BEAUTY")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    soften(obj, bevel, segments)
    if parent:
        obj.parent = parent
    return obj


def soften(obj, bevel, segments=2):
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    if bevel:
        obj.data.set_sharp_from_angle(angle=math.pi)  # clear marks; the bevel and weighted normals shade
        mod = obj.modifiers.new("SoftEdge", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = "ANGLE"
        mod.angle_limit = math.radians(30)
        obj.modifiers.new("FaceNormals", "WEIGHTED_NORMAL").keep_sharp = True
    else:
        obj.data.set_sharp_from_angle(angle=math.radians(30))


def join(objects, name):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = objects[0]
    obj.name = obj.data.name = name
    return obj


def loft(name, rings, mat, parent=None, bevel=0.0, segments=2):
    n = len(rings[0])
    faces = [(j * n + i, j * n + (i + 1) % n, (j + 1) * n + (i + 1) % n, (j + 1) * n + i)
             for j in range(len(rings) - 1) for i in range(n)]
    faces += [tuple(reversed(range(n))), tuple(range((len(rings) - 1) * n, len(rings) * n))]
    return polymesh(name, [p for r in rings for p in r], faces, mat, parent, bevel, segments)


def rect(hw, y0, y1, c, top=None, x=0.0):
    """Chamfered (x, y) section, CCW from bottom left; `top` narrows the upper edge."""
    t = hw if top is None else top
    return [(x - hw + c, y0), (x + hw - c, y0), (x + hw, y0 + c), (x + t, y1 - c),
            (x + t - c, y1), (x - t + c, y1), (x - t, y1 - c), (x - hw, y0 + c)]


def superellipse(a, b, t, p=2.6):
    c, s = math.cos(t), math.sin(t)
    return a * math.copysign(abs(c) ** (2 / p), c), b * math.copysign(abs(s) ** (2 / p), s)


def inset(poly, d):
    """A closed 2D polygon moved d inward with mitred corners, either winding."""
    area = sum(poly[i - 1][0] * poly[i][1] - poly[i][0] * poly[i - 1][1] for i in range(len(poly)))
    s = 1.0 if area > 0 else -1.0
    out = []
    for i in range(len(poly)):
        (ax, ay), (bx, by), (cx, cy) = poly[i - 1], poly[i], poly[(i + 1) % len(poly)]
        l1, l2 = math.hypot(bx - ax, by - ay), math.hypot(cx - bx, cy - by)
        n1 = (-s * (by - ay) / l1, s * (bx - ax) / l1)
        n2 = (-s * (cy - by) / l2, s * (cx - bx) / l2)
        mx, my = n1[0] + n2[0], n1[1] + n2[1]
        ml = math.hypot(mx, my)
        mx, my = mx / ml, my / ml
        k = d / max(mx * n1[0] + my * n1[1], .4)
        out.append((bx + mx * k, by + my * k))
    return out


def side_slab(name, prof, x0, x1, mat, parent=None, ch=0.0, bevel=0.0):
    """A side profile [(z, y)] extruded across X, both side faces chamfered by `ch`."""
    layers = [(x0, inset(prof, ch)), (x0 + ch, prof), (x1 - ch, prof), (x1, inset(prof, ch))] if ch else [(x0, prof), (x1, prof)]
    return loft(name, [[(x, y, z) for z, y in pr] for x, pr in layers], mat, parent, bevel)


def lathe(name, profile, mat, centre=(0, 0, 0), axis="z", sides=24, phase=0.0, closed=False, parent=None, bevel=0.0):
    """Rings [(h, r)] around an axis through `centre`; `closed` revolves a closed profile loop (a hollow ring)."""
    cx, cy, cz = centre
    def point(h, r, a):
        u, v = r * math.cos(a), r * math.sin(a)
        return {"z": (cx + u, cy + v, cz + h), "y": (cx + u, cy + h, cz + v), "x": (cx + h, cy + u, cz + v)}[axis]
    rings = [[point(h, r, phase + math.tau * i / sides) for i in range(sides)] for h, r in profile]
    if not closed:
        return loft(name, rings, mat, parent, bevel)
    n, count = sides, len(rings)
    faces = [(j * n + i, j * n + (i + 1) % n, (j + 1) % count * n + (i + 1) % n, (j + 1) % count * n + i)
             for j in range(count) for i in range(n)]
    return polymesh(name, [p for r in rings for p in r], faces, mat, parent, bevel)


def frame_z(name, z0, z1, outer, inner, mat, parent=None, bevel=0.0):
    """An open rectangular hood: matching outer/inner (x, y) loops between z0 and z1."""
    n = len(outer)
    verts = [(x, y, z) for loop in (outer, inner) for z in (z0, z1) for x, y in loop]
    faces = []
    for i in range(n):
        j = (i + 1) % n
        faces += [(i, j, n + j, n + i), (2 * n + i, 3 * n + i, 3 * n + j, 2 * n + j),
                  (n + i, n + j, 3 * n + j, 3 * n + i), (i, 2 * n + i, 2 * n + j, j)]
    return polymesh(name, verts, faces, mat, parent, bevel)


def slab(name, outer, inner, mat):
    """A plate between two matching (nu, nv) point grids."""
    nu, nv = len(outer), len(outer[0])
    verts = [p for row in outer for p in row] + [p for row in inner for p in row]
    o = lambda i, j: i * nv + j
    q = lambda i, j: nu * nv + i * nv + j
    faces = []
    for i in range(nu - 1):
        for j in range(nv - 1):
            faces += [(o(i, j), o(i, j + 1), o(i + 1, j + 1), o(i + 1, j)), (q(i, j), q(i + 1, j), q(i + 1, j + 1), q(i, j + 1))]
    for i in (0, nu - 1):
        faces += [(o(i, j), q(i, j), q(i, j + 1), o(i, j + 1)) for j in range(nv - 1)]
    for j in (0, nv - 1):
        faces += [(o(i, j), o(i + 1, j), q(i + 1, j), q(i, j)) for i in range(nu - 1)]
    return polymesh(name, verts, faces, mat)


def disk(name, points, mat, parent=None):
    """One flat polygon; CCW (x right, y up) points give a +Z (weapon-forward) normal."""
    obj = polymesh(name, points, [tuple(range(len(points)))], mat, parent, recalc=False)
    for polygon in obj.data.polygons:
        polygon.use_smooth = False
    return obj


def optic_glass_material():
    mat = material("OpticGlass", (.035, .05, .055))
    mat.diffuse_color = (.035, .05, .055, .55)
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = .12
    bsdf.inputs["Alpha"].default_value = .55
    return mat


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
        "mint": material("SignalMint", (.30, .86, .50)),
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
    # Weapon-kit closures: L lofts (x, y) sections along z, Y lofts (x, z) sections along y.
    def L(label, stations, key, bevel=.004):
        return add(loft(label, [[(x, y, z) for x, y in sec] for z, sec in stations], m[key], base, bevel))
    def Y(label, stations, key, bevel=.004):
        return add(loft(label, [[(x, y, z) for x, z in sec] for y, sec in stations], m[key], base, bevel))
    def Q(label, profile, key, centre, axis="z", sides=24, octagon=False, closed=False, bevel=0.0):
        return add(lathe(label, profile, m[key], centre, axis, 8 if octagon else sides,
                         math.pi / 8 if octagon else 0.0, closed, base, bevel))
    def S(label, prof, x0, x1, key, ch=0.0, bevel=0.0):
        return add(side_slab(label, prof, x0, x1, m[key], base, ch, bevel))
    def follow(obj, target):
        bpy.context.view_layer.update()
        world = obj.matrix_world.copy()
        obj.parent = bpy.data.objects[target]
        obj.matrix_world = world
        return obj
    # Signal names still remap in Unity; node emission also makes Blender review honest.
    if name in (*WEAPON_KIT, *WEAPON_SPECIALS):
        for key in ("cyan", "amber", "coral", "violet"):
            shader = m[key].node_tree.nodes.get("Principled BSDF")
            shader.inputs["Emission Color"].default_value = m[key].diffuse_color
            shader.inputs["Emission Strength"].default_value = 3.0
    sight = None
    if name in ("LineDriver", "LaserSMG"):
        m["glass"] = optic_glass_material()
    if name == "LineDriver":
        # AR-class starter: graphite receiver, ivory upper bridge, open split rails around a cyan charge path.
        L("Receiver", [(-.31, rect(.054, .035, .17, .014)), (-.24, rect(.062, .02, .19, .016)),
                       (.04, rect(.062, .02, .19, .016)), (.13, rect(.046, .06, .172, .012))], "graphite", .005)
        L("UpperBridge", [(-.27, rect(.066, .168, .222, .012)), (-.02, rect(.072, .168, .236, .014, .062)),
                          (.22, rect(.066, .162, .226, .012, .058)), (.42, rect(.036, .158, .196, .008))], "ivory", .005)
        for side in (-1, 1):
            # Constant inner faces keep the charge slit straight; outer faces taper toward the tips.
            L("SplitRail", [(z, rect(hw, y0, y1, c, x=side * (.058 + hw))) for z, hw, y0, y1, c in
                            ((-.04, .032, .07, .175, .010), (.30, .027, .09, .166, .009),
                             (.60, .019, .106, .152, .007), (.665, .011, .114, .144, .004))], "ivory", .004)
            B("RailSignal", (side * .0572, .128, .36), (.004, .010, .40), "cyan")
            B("RailTip", (side * .076, .152, .585), (.016, .006, .05), "cyan")
        L("Handguard", [(.03, rect(.05, .03, .1, .012)), (.30, rect(.045, .06, .1, .01)), (.36, rect(.03, .075, .1, .008))], "graphite")
        B("RailBridge", (0, .088, .30), (.13, .018, .045), "graphite")
        Q("ChargePath", [(.12, .0055), (.60, .0055)], "cyan", (0, .13, 0), sides=8)
        B("NodeStrut", (0, .13, .612), (.122, .010, .014), "graphite")
        Q("EmitterNode", [(.585, .012), (.595, .02), (.625, .02), (.64, .013)], "graphite", (0, .13, 0), sides=16, bevel=.002)
        Q("EmitterLens", [(.636, .010), (.646, .007)], "cyan", (0, .13, 0), sides=16)
        Q("RearCell", [(-.38, .048), (-.37, .062), (-.24, .062), (-.23, .05)], "mid", (0, .115, 0))
        Q("CellSignal", [(-.336, .060), (-.334, .0645), (-.322, .0645), (-.32, .060)], "cyan", (0, .115, 0))
        L("StockTop", [(-.36, rect(.026, .10, .15, .008)), (-.50, rect(.024, .085, .13, .008)),
                       (-.565, rect(.024, .08, .125, .008))], "ivory", .003)
        L("StockStrut", [(-.26, rect(.022, .0, .045, .007)), (-.565, rect(.022, -.065, -.02, .007))], "graphite", .003)
        L("StockHeel", [(-.59, rect(.03, -.08, .135, .01)), (-.56, rect(.03, -.08, .135, .01))], "graphite", .004)
        # Holo sight: open rectangular hood with the reticle glass near its front.
        L("HoloSightBase", [(-.18, rect(.034, .224, .262, .008)), (-.06, rect(.034, .224, .262, .008))], "mid", .003)
        add(frame_z("HoloSightFrame", -.17, -.08, rect(.054, .254, .36, .018), rect(.041, .266, .346, .011), m["graphite"], base, .002))
        add(disk("OpticGlass", [(x, y, -.086) for x, y in rect(.039, .268, .344, .009)], m["glass"], base))
        # Broad top/right details remain legible from the rear at viewmodel scale .6.
        for side in (-1, 1):
            S("ReceiverInset", [(-.235,.055), (.05,.055), (.09,.105), (.025,.159), (-.235,.159)],
              *sorted((side*.062,side*.067)), "mid", .002)
            S("ReceiverPlate", [(-.215,.065), (-.045,.065), (.035,.115), (-.035,.147), (-.215,.147)],
              *sorted((side*.067,side*.070)), "graphite", .002)
            B("ReceiverStatus", (side*.071,.123,-.13), (.003,.013,.135), "cyan")
            L("RailInset", [(z,rect(.004,y-.017,y+.017,.002,x=side*x))
                            for z,x,y in ((.035,.121,.124),(.28,.110,.129),(.49,.093,.13))], "graphite", 0)
            B("TopConduit", (side*.040,.239,.04), (.024,.008,.32), "cyan")
        B("TopRailBed", (0,.24,.08), (.029,.012,.44), "graphite")
        for z in (-.12,-.055,.01,.075,.14,.205,.27):
            add(box("RailNotch", (0,.25,z), (.048,.012,.026), m["mid"], base, soft=False))
        # An exposed rear-facing cell and an actual annular emitter face.
        Q("RearCellDisplay", [(-.385,.032),(-.382,.045),(-.374,.045)], "cyan", (0,.115,0), sides=16)
        R("EmitterRing", (0,.13,.647), .016,.009,"cyan", depth=.008)
        B("RearPlate", (0,.197,-.274), (.070,.035,.012), "graphite")
        for z in (.20,.34,.48):
            Q("ChargeCoil", [(z,.022),(z+.018,.022),(z+.018,.015),(z,.015)],
              "cyan", (0,.13,0), sides=16, closed=True)
        for z in (-.20,-.13,-.06):
            B("Capacitor", (.073,.099,z), (.008,.030,.044), "cyan")
            Q("PanelBolt", [(.073,.007),(.078,.007)], "mid", (0,.150,z), axis="x", sides=8)
        sight = (0, .306, -.20)
        muzzle = (0, .13, .65)
    elif name == "PulseLobber":
        # Stubby launcher: exposed orange chamber held between six curved ivory ribs.
        L("Receiver", [(-.35, rect(.065, .015, .16, .018)), (-.24, rect(.09, -.01, .20, .02)),
                       (.12, rect(.085, .01, .17, .02)), (.26, rect(.055, .04, .13, .015))], "graphite", .006)
        Q("RearBreech", [(-.28, .075), (-.25, .13), (-.16, .15), (-.12, .145)], "ivory", (0, .17, 0))
        add(ellipsoid("OrbChamber", (0, .17, .10), (.177, .177, .205), m["amber"], base, segments=24, rings=14))
        for z in (-.08, .26):
            Q("ChamberBand", [(z-.025, .151), (z-.02, .19), (z+.02, .19), (z+.025, .151)],
              "graphite", (0, .17, 0), closed=True)
        for i in range(6):
            a = math.tau * i / 6
            rings = []
            for z, r in ((-.13, .14), (-.06, .19), (.10, .205), (.25, .19), (.32, .15)):
                rings.append([(rad * math.cos(a+d), .17 + rad * math.sin(a+d), z)
                              for rad, d in ((r, -.11), (r+.018, -.11), (r+.018, .11), (r, .11))])
            add(loft("ChamberRib", rings, m["ivory"], base, .003))
        Q("Bell", [(.29, .14), (.35, .15), (.49, .19), (.535, .185), (.535, .145), (.48, .14), (.34, .09)],
          "ivory", (0, .17, 0), closed=True)
        Q("DarkMuzzle", [(.51, .185), (.55, .185), (.55, .148), (.51, .148)], "graphite", (0, .17, 0), closed=True)
        R("RecessedEmitter", (0, .17, .37), .09, .066, "amber", depth=.018)
        for side in (-1, 1):
            S("Cradle", [(-.22, .01), (.31, .01), (.37, -.035), (.28, -.075), (-.24, -.055)],
              *sorted((side*.075, side*.10)), "ivory", .005)
            B("ChargeIndicator", (side*.092, .09, -.21), (.008, .025, .075), "amber")
        B("ForeRest", (0, -.065, .20), (.14, .04, .17), "graphite")
        Q("BreechEnergy", [(-.235,.133),(-.207,.133),(-.207,.122),(-.235,.122)],
          "amber", (0,.17,0), closed=True)
        for side in (-1,1):
            B("PlasmaFeed", (side*.104,.089,-.17), (.018,.038,.11), "amber")
            Q("CradleLatch", [(.10,.013),(.12,.013)], "mid", (0,.04,-.15), axis="x", sides=8)
        R("BellEnergyLip", (0,.17,.548), .176,.157,"amber",depth=.009)
        muzzle = (0, .16, .55)
    elif name == "ReboundDisc":
        # Broad horizontal disc visibly nested inside swept launch forks.
        L("RearShell", [(-.39, rect(.06, .035, .16, .015)), (-.28, rect(.105, .015, .195, .02)),
                        (-.04, rect(.10, .035, .18, .018)), (.10, rect(.07, .065, .145, .012))], "ivory", .005)
        Q("DiscCore", [(-.04, .11), (-.03, .18), (-.012, .212), (.012, .212), (.023, .18)],
          "graphite", (0, .145, .31), axis="y", sides=32)
        Q("DiscSignal", [(.014, .15), (.014, .195), (.026, .195), (.030, .15)],
          "coral", (0, .145, .31), axis="y", sides=32, closed=True)
        Q("DiscHub", [(.023, .07), (.033, .07), (.047, .04)], "mid", (0, .145, .31), axis="y")
        R("HubSignal", (0, .194, .31), .042, .024, "coral", axis="y", depth=.005)
        for side in (-1, 1):
            L("Fork", [(z, rect(w, y0, y1, .008, x=side*x)) for z, x, w, y0, y1 in
                       ((-.06, .09, .04, .04, .15), (.13, .23, .04, .055, .145),
                        (.41, .24, .034, .07, .14), (.59, .205, .022, .09, .145))], "ivory", .003)
            B("ForkRail", (side*.242, .147, .30), (.027, .014, .30), "graphite")
            B("ForkSignal", (side*.242, .156, .30), (.012, .006, .25), "coral")
            Q("ForkRoller", [(-.018, .037), (.018, .037)], "mid", (side*.211, .117, .515), axis="y", sides=16)
            B("RearSignal", (side*.102, .112, -.17), (.005, .018, .12), "coral")
        L("UnderRail", [(-.08, rect(.05, .005, .05, .01)), (.32, rect(.06, .015, .055, .01))], "graphite")
        follow(Q("DiscRimLight", [(-.016,.213),(-.016,.219),(.015,.219),(.015,.213)],
                 "coral", (0,.145,.31), axis="y", sides=32, closed=True), "DiscSignal")
        for side in (-1,1):
            B("ForkEnergyEdge", (side*.242,.156,.30), (.022,.012,.30), "coral")
            S("DiscHousingPlate", [(-.28,.06),(-.06,.06),(.02,.10),(-.06,.153),(-.28,.153)],
              *sorted((side*.102,side*.109)), "graphite", .003)
            B("DiscCapacitor", (side*.110,.115,-.17), (.007,.030,.12), "coral")
        muzzle = (0, .16, .58)
    elif name == "PushLens":
        L("Receiver", [(-.34, rect(.055, .025, .135, .016)), (-.23, rect(.09, .005, .18, .02)),
                       (-.03, rect(.085, .025, .17, .018)), (.12, rect(.045, .065, .135, .012))], "ivory", .005)
        Q("Neck", [(-.05, .065), (.08, .08), (.19, .06)], "graphite", (0, .14, 0), sides=16)
        Q("OpenLensFrame", [(.29, .226), (.32, .253), (.42, .253), (.45, .235), (.45, .201), (.32, .201)],
          "graphite", (0, .16, 0), sides=32, closed=True)
        Q("InnerField", [(.322, .202), (.322, .191), (.43, .191), (.43, .202)],
          "violet", (0, .16, 0), sides=32, closed=True)
        R("FieldLip", (0, .16, .447), .23, .211, "mid", depth=.009)
        for side in (-1, 1):
            add(arc_ring("LensGuard", (0, .16, .37), .278, .25, m["ivory"],
                         -.78 if side == 1 else math.pi-.78, 1.56, base, depth=.10, steps=10))
            L("LensYoke", [(z, rect(.024, y-.035, y+.035, .008, x=side*x))
                           for z,x,y in ((-.02,.07,.12), (.17,.19,.12), (.33,.25,.16))], "ivory", .003)
            B("RearStatus", (side*.088, .11, -.17), (.006, .015, .12), "violet")
        for i in range(4):
            a = math.pi/4 + i*math.pi/2
            x,y = .223*math.cos(a), .16+.223*math.sin(a)
            Q("FieldCoil", [(.27,.026), (.32,.032), (.40,.032), (.46,.025)], "mid", (x,y,0), sides=12)
            Q("CoilSignal", [(.448,.020), (.466,.020)], "violet", (x,y,0), sides=12)
            follow(add(arc_ring("FieldSegment", (0,.16,.303), .247,.218,m["violet"],
                                i*math.pi/2+.16,1.20,base,depth=.016,steps=8)), "InnerField")
        for side in (-1,1):
            B("FieldCapacitor", (side*.093,.095,-.17), (.014,.038,.12), "violet")
            Q("FieldLatch", [(.095,.012),(.105,.012)], "mid", (0,.145,-.22), axis="x", sides=8)
        muzzle = (0, .16, .47)
    elif name == "Grenade":
        # Segmented frag: ivory shell plates over a graphite core, a soft-grey grip band at the
        # equator, and an amber fuse lever and light at the +Z (Muzzle) end.
        centre, radii = (0, 0, .085), (.084, .084, .098)
        def surface(theta, phi, extra):
            return (centre[0] + (radii[0] + extra) * math.sin(phi) * math.cos(theta),
                    centre[1] + (radii[1] + extra) * math.sin(phi) * math.sin(theta),
                    centre[2] + (radii[2] + extra) * math.cos(phi))
        add(ellipsoid("Core", centre, radii, m["graphite"], base, segments=16, rings=10))
        plates = []
        for k in range(6):
            t0 = math.pi / 2 + k * math.tau / 6 + .075
            ts = [t0 + (math.tau / 6 - .15) * i / 2 for i in range(3)]
            for p0, p1 in ((.52, 1.29), (1.85, 2.70)):
                ps = [p0 + (p1 - p0) * i / 2 for i in range(3)]
                grid = lambda extra: [[surface(t, p, extra) for t in ts] for p in ps]
                plates.append(slab("Shell", grid(.008), grid(.0015), m["ivory"]))
        shell = join(plates, "Shell")
        soften(shell, .0015, segments=1)
        shell.parent = base
        add(shell)
        Q("GripBand", [(.062, .078), (.063, .089), (.067, .093), (.080, .093), (.082, .090), (.088, .090),
                       (.090, .093), (.103, .093), (.107, .089), (.108, .078)], "mid", (0, 0, 0), sides=24, closed=True)
        Q("FuseCap", [(.158, .030), (.172, .039), (.190, .039), (.196, .035), (.199, .027)], "graphite", (0, 0, 0))
        Q("FuseLight", [(.192, .024), (.200, .024), (.205, .019), (.208, .011), (.209, .003)], "amber", (0, 0, 0))
        # Spoon lever riding the plate gap on the +Y side, from the fuse cap down to the grip band.
        tab = []
        for i in range(7):
            phi = .42 + (1.27 - .42) * i / 6
            normal = Vector((0, math.sin(phi), math.cos(phi)))
            p = Vector(surface(math.pi / 2, phi, .011))
            tab.append([tuple(p + Vector((u, 0, 0)) + normal * v) for u, v in rect(.0135, -.0035, .0035, .002)])
        add(loft("FuseTab", tab, m["amber"], base))
        Q("LiveSeam", [(.079,.090),(.079,.095),(.091,.095),(.091,.090)],
          "amber", (0,0,0), sides=24, closed=True)
        Q("FuseHalo", [(.177,.039),(.190,.039),(.190,.033),(.177,.033)],
          "amber", (0,0,0), sides=16, closed=True)
        muzzle = (0, 0, .209)
    elif name == "StunGun":
        L("Receiver", [(-.28, rect(.05, .025, .145, .014)), (-.21, rect(.078, .01, .17, .018)),
                       (.015, rect(.078, .025, .17, .018)), (.11, rect(.058, .045, .14, .014))], "ivory", .004)
        L("ProngBridge", [(.045, rect(.095, .05, .15, .016)), (.125, rect(.095, .06, .145, .016))], "graphite")
        for side in (-1, 1):
            Q("Prong", [(.10,.036), (.14,.036), (.19,.025), (.32,.022), (.35,.026)],
              "graphite", (side*.072,.103,0), sides=16)
            Q("ProngTip", [(.34,.024), (.35,.030), (.375,.030), (.392,.015)],
              "cyan", (side*.072,.103,0), sides=16)
            for z in (.16,.20,.24):
                Q("Insulator", [(z,.029), (z+.015,.029)], "mid", (side*.072,.103,0), sides=16)
            S("FlankPanel", [(-.24,.055), (-.20,.14), (-.04,.14), (.035,.09), (.015,.05)],
              *sorted((side*.077, side*.084)), "graphite", .003)
            B("ChargeRail", (side*.085,.10,-.11), (.004,.012,.14), "cyan")
        L("WarningBar", [(-.20, rect(.026,.172,.185,.004)), (-.06,rect(.026,.172,.185,.004)),
                         (-.025,rect(.015,.166,.18,.003))], "cyan", .001)
        Q("CellCap", [(-.30,.035), (-.29,.048), (-.265,.048)], "mid", (0,.095,0), sides=16)
        for side in (-1,1):
            for z in (.158,.208,.258,.303):
                Q("ProngCoil", [(z,.030),(z+.018,.030),(z+.018,.024),(z,.024)],
                  "cyan", (side*.072,.103,0), sides=16, closed=True)
            B("StunCapacitor", (side*.087,.094,-.14), (.010,.035,.12), "cyan")
            Q("SafetyBolt", [(.085,.009),(.095,.009)], "mid", (0,.13,-.23), axis="x", sides=8)
        Q("RearChargeRing", [(-.297,.042),(-.28,.042),(-.28,.035),(-.297,.035)],
          "cyan", (0,.095,0), sides=16, closed=True)
        muzzle = (0, .10, .40)
    elif name == "LaserSword":
        # Downward palm grip, swept crossguard and a broad, pointed energy blade.
        L("HiltBody", [(-.26,rect(.045,-.035,.055,.012)), (-.12,rect(.052,-.035,.07,.014)),
                       (.07,rect(.035,-.025,.06,.01)), (.14,rect(.042,-.02,.065,.012))], "graphite", .004)
        Q("Pommel", [(-.28,.035), (-.27,.055), (-.235,.055), (-.225,.042)], "mid", (0,.01,0), sides=16)
        blade_sections = ((.15,.036,.025),(.25,.045,.029),(.88,.033,.022),(1.14,.017,.014),(1.205,.002,.002))
        L("BladeCore", [(z,[(-w,.020),(0,.020-h),(w,.020),(0,.020+h)])
                        for z,w,h in blade_sections], "cyan", .0015)
        # An open four-edge envelope leaves the core visible with the same opaque Unity Signal material.
        shell_parts = []
        for angle in (0,math.pi/2,math.pi,3*math.pi/2):
            rings = []
            for z,w,h in ((.16,.044,.033),(.25,.053,.037),(.88,.041,.030),(1.14,.024,.021),(1.225,.002,.002)):
                x,y = w*math.cos(angle), .020+h*math.sin(angle)
                rings.append([(x+dx,y+dy,z) for dx,dy in ((-.001,-.001),(.001,-.001),(.001,.001),(-.001,.001))])
            shell_parts.append(loft("GlowEdge", rings,m["cyan"],base))
        add(join(shell_parts,"BladeGlowShell"))
        follow(parts[-1],"BladeCore")
        for side in (-1,1):
            L("Guard", [(z,rect(w,y-.022,y+.022,.006,x=side*x))
                        for z,x,w,y in ((.085,.035,.028,.012), (.10,.095,.045,.005), (.17,.14,.025,.025))], "ivory", .003)
            L("EmitterJaw", [(.09,rect(.014,-.005,.055,.004,x=side*.047)),
                             (.27,rect(.010,.004,.045,.003,x=side*.051))], "ivory", .002)
            B("HiltSignal", (side*.052,.035,-.11), (.005,.012,.16), "cyan")
        Q("EmitterCollar", [(.09,.040), (.10,.059), (.145,.059), (.16,.045)], "mid", (0,.015,0), sides=16)
        for z in (-.21,-.16,-.11,-.06):
            Q("HiltBand", [(z,.047), (z+.012,.047)], "ivory", (0,.01,0), sides=16)
        Q("MachinedEmitter", [(.095,.058),(.105,.066),(.14,.066),(.165,.051),(.165,.036),(.11,.036)],
          "graphite", (0,.015,0), closed=True, sides=24)
        Q("EmitterEnergy", [(.146,.055),(.16,.055),(.16,.044),(.146,.044)],
          "cyan", (0,.015,0), closed=True, sides=24)
        for y in (-.045,-.075,-.105,-.135,-.165,-.195):
            z=-.18+(y+.12)*.21
            Y("GripRib", [(y-.006,[(px,z+pz) for px,pz in
                                   (superellipse(.043,.059,math.tau*i/16) for i in range(16))]),
                          (y+.006,[(px,z+pz) for px,pz in
                                   (superellipse(.043,.059,math.tau*i/16) for i in range(16))])], "mid", .001)
        muzzle = (0, 0, .92)
    elif name == "LaserPistol":
        # Slab-sided slide with a narrow top deck, octagonal emitter with an exposed coil, iron sights.
        def slide(hw, y0, y1, top, shoulder, c=.006):
            return [(-hw + c, y0), (hw - c, y0), (hw, y0 + c), (hw, shoulder), (top, y1), (-top, y1), (-hw, shoulder), (-hw, y0 + c)]
        L("PistolSlide", [(-.262, slide(.058, .113, .181, .038, .152)), (.205, slide(.058, .113, .181, .038, .152)),
                          (.25, slide(.052, .113, .174, .032, .148))], "ivory", .004)
        L("SlideChassis", [(-.26,rect(.055,.071,.113,.006)), (.22,rect(.055,.071,.113,.006)),
                           (.249,rect(.047,.077,.113,.005))], "graphite", .002)
        L("SlideRear", [(-.268, rect(.05, .08, .17, .01, .032)), (-.258, rect(.05, .08, .17, .01, .032))], "graphite", .002)
        for side in (-1, 1):
            B("SlideSignal", (side * .0585, .118, -.04), (.003, .007, .26), "cyan")
        S("LowerFrame", [(-.25, .074), (.235, .074), (.25, .062), (.25, .03), (.225, .016), (-.02, .016),
                         (-.06, .028), (-.13, .036), (-.20, .036), (-.25, .05)], -.05, .05, "graphite", .004, .002)
        Q("PistolEmitter", [(.235, .044), (.245, .05), (.296, .05), (.302, .045)], "graphite", (0, .112, 0), octagon=True, bevel=.003)
        Q("EmitterCoil", [(.300, .040), (.303, .045), (.322, .045), (.325, .040)], "cyan", (0, .112, 0), octagon=True)
        Q("EmitterCrown", [(.323, .045), (.329, .052), (.364, .052), (.372, .046), (.372, .030)], "graphite", (0, .112, 0), octagon=True, bevel=.003)
        R("EmitterSignal", (0, .112, .372), .044, .026, "cyan", depth=.010)
        Q("EmitterLens", [(.358, .026), (.366, .026)], "cyan", (0, .112, 0), sides=16)
        # Iron sights: the front post top and the rear ear tops share the sight line (y .225).
        S("RearSight", [(-.248, .176), (-.21, .176), (-.21, .198), (-.248, .198)], -.036, .036, "graphite", .003)
        for side in (-1, 1):
            S("RearSight", [(-.248, .196), (-.21, .196), (-.214, .225), (-.244, .225)], *sorted((side * .0105, side * .034)), "graphite", .002)
        S("FrontSight", [(.19, .176), (.226, .176), (.222, .225), (.204, .225)], -.0055, .0055, "cyan", .0015)
        B("PowerCell", (0, -.252, -.207), (.076, .016, .105), "mid")
        for side in (-1,1):
            S("SlideInset", [(-.19,.123),(-.08,.123),(-.045,.151),(-.19,.151)],
              *sorted((side*.056,side*.060)), "graphite", .002)
            B("SlideStatus", (side*.0605,.135,-.13), (.002,.011,.10), "cyan")
            for z in (-.23,-.211,-.192):
                add(box("SlideSerration", (side*.057,.145,z), (.006,.042,.008), m["mid"], base, soft=False))
        # Split deck plates reveal a cyan coil window without obstructing the iron sight line.
        B("TopCellWell", (0,.183,.045), (.055,.008,.23), "graphite")
        B("TopCell", (0,.188,.045), (.042,.006,.205), "cyan")
        for z in (-.04,.045,.13):
            add(box("CellBridge", (0,.193,z), (.054,.007,.009), m["mid"], base, soft=False))
        B("RearChargeDisplay", (0,.116,-.270), (.032,.018,.006), "cyan")
        B("CellHeelSignal", (0,-.253,-.261), (.042,.008,.003), "cyan")
        for z in (.264,.282,.299):
            Q("EmitterWinding", [(z,.048),(z+.009,.048),(z+.009,.043),(z,.043)],
              "cyan", (0,.112,0), octagon=True, closed=True)
        for z in (-.15,-.04):
            Q("FrameBolt", [(.054,.008),(.061,.008)], "mid", (0,.088,z), axis="x", sides=8)
        # Surface inserts must follow the runtime's PistolSlide recoil mesh.
        bpy.context.view_layer.update()
        for obj in parts:
            if obj.name.startswith(("SlideInset", "SlideStatus", "SlideSerration", "SlideSignal",
                                    "TopCell", "CellBridge", "RearChargeDisplay")):
                world = obj.matrix_world.copy()
                obj.parent = bpy.data.objects["PistolSlide"]
                obj.matrix_world = world
        sight = (0, .225, -.278)
        muzzle = (0, .112, .39)
    elif name == "LaserShotgun":
        L("ShotgunReceiver", [(-.40,rect(.080,.035,.195,.018)), (-.28,rect(.105,.01,.22,.022)),
                              (.05,rect(.105,.025,.21,.022)), (.18,rect(.085,.06,.18,.016))], "ivory", .006)
        for side in (-1, 1):
            Q("ShotgunBarrel", [(.08,.060), (.20,.060), (.23,.052), (.66,.052), (.70,.062), (.77,.062), (.78,.052)],
              "graphite", (side*.085,.13,0), sides=20)
            Q("BarrelSleeve", [(.14,.063), (.17,.069), (.48,.069), (.52,.058)], "mid", (side*.085,.13,0), sides=20)
            Q("ShotgunEmitter", [(.765,.055), (.789,.055), (.789,.037), (.765,.037)],
              "amber", (side*.085,.13,0), sides=20, closed=True)
            Q("Bore", [(.75,.035), (.775,.035)], "graphite", (side*.085,.13,0), sides=16)
            S("ReceiverPanel", [(-.34,.08), (-.29,.18), (-.08,.18), (.03,.12), (-.02,.07)],
              *sorted((side*.104,side*.112)), "graphite", .004)
            B("ReceiverSignal", (side*.113,.135,-.17), (.004,.012,.19), "coral")
        # Join all pump pieces so the existing viewmodel moves the complete assembly.
        pump_start = len(parts)
        L("PumpShell", [(.17,rect(.12,-.075,.055,.018)), (.22,rect(.14,-.08,.06,.02)),
                        (.41,rect(.14,-.08,.06,.02)), (.46,rect(.11,-.06,.05,.015))], "graphite", .004)
        for z in (.23,.28,.33,.38):
            L("PumpRib", [(z,rect(.145,-.085,.035,.015)), (z+.014,rect(.145,-.085,.035,.015))], "mid", .002)
        for side in (-1,1):
            B("PumpSignal", (side*.145,-.025,.32), (.005,.018,.17), "amber")
        # Apply bevels before joining to retain each piece's intended soft edge.
        pump_parts = parts[pump_start:]
        for obj in pump_parts:
            bpy.context.view_layer.objects.active = obj
            for mod in list(obj.modifiers):
                bpy.ops.object.modifier_apply(modifier=mod.name)
        del parts[pump_start:]
        add(join(pump_parts, "PumpForeend"))
        L("ShoulderStock", [(-.67,rect(.07,-.065,.13,.016)), (-.58,rect(.065,-.035,.15,.016)),
                            (-.44,rect(.04,.035,.14,.012)), (-.36,rect(.045,.05,.15,.012))], "graphite", .005)
        L("CheekRest", [(-.62,rect(.05,.11,.16,.01)), (-.49,rect(.045,.13,.17,.01))], "ivory", .003)
        L("StockPad", [(-.70,rect(.08,-.075,.14,.018)), (-.67,rect(.08,-.075,.14,.018))], "mid", .004)
        B("RibSight", (0,.207,.29), (.025,.016,.50), "ivory")
        B("FrontBead", (0,.224,.65), (.015,.018,.025), "amber")
        for side in (-1,1):
            for z in (.55,.61,.67):
                Q("BarrelWinding", [(z,.057),(z+.016,.057),(z+.016,.052),(z,.052)],
                  "amber", (side*.085,.13,0), sides=20, closed=True)
            B("ShotgunCapacitor", (side*.081,.212,-.17), (.030,.012,.16), "amber")
            Q("BreechBolt", [(.112,.010),(.12,.010)], "mid", (0,.09,-.27), axis="x", sides=8)
        muzzle = (0, .12, .80)
    elif name == "LaserSniper":
        # Long ivory receiver, octagonal handguard, stepped barrel and a full scope on the y .31 axis.
        L("SniperReceiver", [(-.40, rect(.056, .04, .17, .014)), (-.30, rect(.062, .03, .19, .016, .052)),
                             (.18, rect(.062, .03, .19, .016, .052)), (.27, rect(.05, .055, .17, .012))], "graphite", .002)
        L("SniperUpperShell", [(-.39,rect(.05,.135,.174,.010)),(-.29,rect(.054,.145,.194,.011)),
                              (.15,rect(.054,.145,.194,.011)),(.25,rect(.043,.135,.176,.010))], "ivory", .002)
        S("SniperChassis", [(-.36, .05), (.30, .05), (.30, .01), (.27, -.005), (-.12, -.005), (-.15, .02), (-.36, .02)],
          -.052, .052, "graphite", .004, .002)
        B("SniperRail", (0, .196, -.04), (.046, .014, .44), "graphite")
        Q("SniperHandguard", [(.24, .05), (.26, .058), (.58, .058), (.62, .048)], "graphite", (0, .11, 0), octagon=True)
        for side in (-1, 1):
            B("SniperSignal", (side * .0545, .11, .42), (.004, .014, .24), "cyan")
        Q("PrecisionBarrel", [(.24, .036), (.60, .036), (.63, .031), (.96, .029)], "graphite", (0, .11, 0), sides=20)
        Q("BarrelCollar", [(.66, .031), (.668, .037), (.702, .037), (.71, .031)], "mid", (0, .11, 0), sides=20)
        Q("MuzzleBrake", [(.95, .05), (.96, .058), (1.085, .058), (1.10, .052), (1.10, .035)], "mid", (0, .11, 0), octagon=True)
        for side in (-1, 1):
            for z in (.995, 1.03, 1.065):
                B("BrakePort", (side * .055, .11, z), (.008, .04, .016), "graphite")
        R("PrecisionEmitter", (0, .11, 1.103), .05, .028, "cyan", depth=.010)
        Q("ScopeTube", [(-.20, .033), (.15, .033), (.19, .040), (.25, .058), (.30, .060)], "graphite", (0, .31, 0))
        Q("ScopeFront", [(.295, .058), (.298, .066), (.322, .066), (.327, .060), (.327, .050), (.31, .050)], "ivory", (0, .31, 0), closed=True)
        Q("ScopeLens", [(.312, .051), (.316, .051)], "cyan", (0, .31, 0))
        Q("ScopeEyepiece", [(-.315, .040), (-.312, .047), (-.27, .047), (-.235, .039), (-.195, .0335)], "ivory", (0, .31, 0))
        Q("ScopeEyeLens", [(-.3165, .034), (-.3155, .034)], "graphite", (0, .31, 0))
        Q("ScopeTurret", [(.033, .022), (.07, .022), (.073, .018)], "mid", (0, .31, 0), axis="y", sides=16)
        Q("ScopeTurret", [(.033, .020), (.062, .020), (.065, .016)], "mid", (0, .31, 0), axis="x", sides=16)
        for z in (-.13, .09):
            Q("ScopeMount", [(z - .015, .033), (z - .015, .042), (z + .015, .042), (z + .015, .033)], "graphite", (0, .31, 0), closed=True)
            B("ScopeMount", (0, .236, z), (.036, .072, .03), "graphite")
        L("PrecisionStock", [(-.40, rect(.05, .02, .17, .014)), (-.48, rect(.045, .0, .15, .012)),
                             (-.66, rect(.045, -.03, .15, .012)), (-.73, rect(.048, -.06, .16, .014))], "graphite", 0)
        L("SniperCheekRest", [(-.47, rect(.035, .14, .185, .012)), (-.64, rect(.035, .14, .185, .012))], "ivory", .004)
        L("SniperButtPad", [(-.745, rect(.05, -.065, .165, .012)), (-.725, rect(.05, -.065, .165, .012))], "mid", .004)
        Y("SniperMagazine", [(.03, rect(.036, .03, .13, .01)), (-.14, rect(.034, .05, .145, .01))], "graphite")
        for side in (-1, 1):
            B("MagazineSignal", (side * .0352, -.06, .09), (.003, .08, .012), "cyan")
        for side in (-1,1):
            S("SniperSidePlate", [(-.29,.058),(.12,.058),(.19,.10),(.13,.132),(-.29,.132)],
              *sorted((side*.061,side*.065)), "mid", .001)
            S("CellWindow", [(-.265,.073),(-.08,.073),(-.045,.114),(-.265,.114)],
              *sorted((side*.065,side*.067)), "graphite")
            B("ExposedCell", (side*.067,.094,-.175), (.002,.032,.19), "cyan")
            for z in (-.25,-.19,-.13):
                add(box("CellRetainer", (side*.067,.095,z), (.003,.041,.009), m["graphite"], base, soft=False))
        for z in (-.21,-.15,-.09,-.03,.03,.09,.15):
            add(box("SniperRailTooth", (0,.209,z), (.052,.012,.022), m["mid"], base, soft=False))
        for z in (.30,.36,.42,.48,.54):
            Q("CoolingFin", [(z,.058),(z+.012,.058)], "mid", (0,.11,0), octagon=True)
        B("ScopeTopSignal", (0,.345,-.035), (.022,.007,.15), "cyan")
        R("ScopeRearStatus", (0,.31,-.317), .039,.032,"cyan",depth=.003)
        B("BreechDisplay", (0,.117,-.403), (.034,.018,.007), "cyan")
        for z in (.73,.79,.85):
            Q("PrecisionCoil", [(z,.045),(z+.018,.045),(z+.018,.032),(z,.032)],
              "cyan", (0,.11,0), sides=20, closed=True)
        for z in (-.26,.10):
            Q("ReceiverLatch", [(.064,.009),(.068,.009)], "mid", (0,.12,z), axis="x", sides=8)
        sight = (0, .31, -.345)
        muzzle = (0, .11, 1.12)
    elif name == "LaserSMG":
        # Compact coral-accented SMG: tall short receiver, stubby vented emitter, vertical fore-grip,
        # retracted two-rod stock and an enclosed red dot. Coral is unused on the other laser-kit guns.
        L("SMGReceiver", [(-.30, rect(.05, .07, .17, .014)), (-.24, rect(.056, .065, .19, .016, .048)),
                          (.06, rect(.056, .065, .19, .016, .048)), (.13, rect(.046, .08, .175, .012))], "graphite", .003)
        L("SMGUpperShell", [(-.29,rect(.043,.143,.177,.010)),(-.23,rect(.049,.15,.194,.012)),
                           (.045,rect(.049,.15,.194,.012)),(.12,rect(.04,.14,.18,.01))], "ivory", .003)
        S("SMGReceiverLower", [(-.27, .075), (.15, .075), (.16, .06), (.16, .025), (.13, .0), (-.08, .0),
                               (-.11, .025), (-.24, .025), (-.27, .045)], -.05, .05, "graphite", .004, .002)
        B("SMGRail", (0, .197, -.08), (.042, .014, .30), "graphite")
        B("SMGChargingHandle", (0, .176, -.285), (.07, .022, .03), "mid")
        for side in (-1, 1):
            B("SMGSignal", (side * .0572, .128, -.07), (.003, .008, .16), "coral")
        Q("SMGEmitter", [(.12, .044), (.13, .05), (.19, .05), (.195, .046)], "graphite", (0, .112, 0), octagon=True, bevel=.003)
        Q("SMGEmitterVent", [(.193, .040), (.196, .046), (.214, .046), (.217, .040)], "coral", (0, .112, 0), octagon=True)
        Q("SMGEmitterShroud", [(.215, .046), (.22, .05), (.27, .05), (.28, .045), (.28, .028)], "graphite", (0, .112, 0), octagon=True, bevel=.003)
        Q("SMGEmitterLens", [(.268, .028), (.274, .028)], "coral", (0, .112, 0), sides=16)
        Y("SMGForegrip", [(.005, rect(.021, .045, .095, .008)), (-.06, rect(.02, .05, .098, .008)),
                          (-.115, rect(.022, .056, .104, .009))], "graphite")
        Y("SMGForegripCap", [(-.112, rect(.025, .052, .108, .009)), (-.13, rect(.025, .054, .11, .009))], "ivory", .003)
        for side in (-1, 1):
            Q("SMGStock", [(-.445, .0075), (-.27, .0075)], "graphite", (side * .03, .118, 0), sides=10)
        L("SMGStockPad", [(-.462, rect(.042, .02, .172, .012)), (-.44, rect(.042, .02, .172, .012))], "ivory", .004)
        # Enclosed red dot on the rail: hollow housing, reticle glass near the front of the bore.
        L("RedDotMount", [(-.135, rect(.022, .2, .232, .006)), (-.085, rect(.022, .2, .232, .006))], "mid", .002)
        Q("RedDotHousing", [(-.14, .025), (-.14, .031), (-.136, .034), (-.084, .034), (-.08, .031), (-.08, .025)],
          "graphite", (0, .258, 0), closed=True)
        Q("RedDotDial", [(.032, .009), (.042, .009), (.044, .007)], "mid", (0, .258, -.11), axis="x", sides=12)
        add(disk("OpticGlass", [(.0235 * math.cos(math.tau * i / 24), .258 + .0235 * math.sin(math.tau * i / 24), -.086)
                                for i in range(24)], m["glass"], base))
        for side in (-1,1):
            S("SMGFlankPlate", [(-.23,.08),(.045,.08),(.09,.113),(.04,.144),(-.23,.144)],
              *sorted((side*.055,side*.059)), "mid", .001)
            S("SMGCellWindow", [(-.21,.091),(-.08,.091),(-.045,.126),(-.21,.126)],
              *sorted((side*.059,side*.060)), "graphite")
            B("SMGCell", (side*.060,.111,-.145), (.002,.032,.16), "coral")
            for z in (-.19,-.135,-.08):
                add(box("SMGCellRib", (side*.060,.11,z), (.002,.039,.008), m["graphite"], base, soft=False))
        for z in (-.22,-.17,-.02,.03):
            add(box("SMGRailTooth", (0,.210,z), (.048,.012,.021), m["mid"], base, soft=False))
        B("SMGTopSignal", (.030,.197,-.075), (.020,.007,.24), "coral")
        B("SMGRearDisplay", (0,.123,-.303), (.034,.026,.006), "coral")
        for z in (.015,.045,.075):
            add(box("SMGHeatVent", (.058,.139,z), (.005,.012,.012), m["graphite"], base, soft=False))
        R("SMGMuzzleRing", (0,.112,.278), .040,.029,"coral",depth=.004)
        for z in (.14,.163,.186):
            Q("SMGCoil", [(z,.051),(z+.010,.051),(z+.010,.044),(z,.044)],
              "coral", (0,.112,0), octagon=True, closed=True)
        for z in (-.23,.025):
            Q("SMGLatch", [(.055,.008),(.060,.008)], "mid", (0,.08,z), axis="x", sides=8)
        sight = (0, .258, -.17)
        muzzle = (0, .112, .29)
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
    elif name == "HealthPickup":
        # Ivory canister with graphite end caps; a mint plus on the top and both faces reads as health while it spins.
        B("HealthCanister", (0, .15, 0), (.24, .16, .20), "ivory")
        for side in (-1, 1):
            B("HealthCap", (side * .13, .15, 0), (.025, .175, .215), "graphite")
            B("HealthCross", (0, .15, side * .1015), (.11, .034, .006), "mint")
            B("HealthCross", (0, .15, side * .1015), (.034, .11, .006), "mint")
        B("HealthCross", (0, .2315, 0), (.13, .006, .04), "mint")
        B("HealthCross", (0, .2315, 0), (.04, .006, .13), "mint")
        muzzle = (0, .15, .16)
    else:
        raise ValueError(name)
    if sight or name in WEAPON_SPECIALS:
        # Raked kit grip through the shared Grip socket (0, -.12, -.18), with guard and trigger.
        def zc(y):
            return -.18 + (y + .12) * .21
        def section(y, a, b, ts):
            return [(px, y, zc(y) + pz) for px, pz in (superellipse(a, b, t) for t in ts)]
        full = [math.tau * i / 20 for i in range(20)]
        add(loft("PalmGrip", [section(y, a, b, full) for y, a, b in
                              ((.035, .034, .046), (-.02, .036, .05), (-.09, .038, .053), (-.16, .039, .055), (-.212, .038, .054))],
                 m["graphite"], base, .004))
        back = [math.pi + .45 + (math.pi - .9) * i / 10 for i in range(11)]
        add(loft("GripPanel", [section(y, a + .0055, b + .0055, back) + section(y, a + .0005, b + .0005, back[::-1])
                               for y, a, b in ((-.01, .036, .05), (-.09, .038, .053), (-.195, .039, .055))],
                 m["ivory"], base))
        add(loft("GripHeel", [section(y, .038 + g, .054 + g, full) for y, g in ((-.205, .002), (-.214, .007), (-.236, .007), (-.245, .002))],
                 m["ivory"], base))
        guard = [(-.135, -.038), (-.10, -.047), (-.05, -.047), (-.026, -.038), (-.014, -.012), (-.010, .03)]
        def band(path, t):
            left, right = [], []
            for i, (z, y) in enumerate(path):
                (z0, y0), (z1, y1) = path[max(i - 1, 0)], path[min(i + 1, len(path) - 1)]
                length = math.hypot(z1 - z0, y1 - y0)
                nz, ny = -(y1 - y0) / length * t / 2, (z1 - z0) / length * t / 2
                left.append((z + nz, y + ny))
                right.append((z - nz, y - ny))
            return left + right[::-1]
        S("TriggerGuard", band(guard, .011), -.009, .009, "graphite", .002)
        S("Trigger", [(-.086, .03), (-.07, .03), (-.067, .005), (-.071, -.017), (-.079, -.022), (-.083, -.015), (-.081, .005)],
          -.006, .006, "mid", .0015)
    elif name not in ("BlinkPickup", "ShieldPickup", "HealthPickup", "Grenade"):
        B("PalmGrip", (0, -.095, -.18), (.09, .25, .09), "graphite")
        B("GripHeel", (0, -.225, -.205), (.105, .045, .10), "ivory")
    add(empty("Grip", (0, -.12, -.18) if name not in ("BlinkPickup", "ShieldPickup", "HealthPickup", "Grenade") else (0, 0, 0), base))
    add(empty("Muzzle", muzzle, base))
    if sight:
        add(empty("Sight", sight, base))
    if name not in ("BlinkPickup", "ShieldPickup", "HealthPickup"):
        support = {"LaserShotgun": (-.10,-.05,.32), "PulseLobber": (-.075,-.065,.20),
                   "ReboundDisc": (-.055,.015,.12), "PushLens": (-.06,.04,.07),
                   "StunGun": (-.06,.035,.015), "LaserSword": (0,-.21,-.20)}
        add(empty("LeftHand", support.get(name, (-.075, -.045, -.02)), base))
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


# Parts whose centre (FrontSight: top-centre) must lie on the Sight forward line within 2 mm.
SIGHT_TARGETS = {"LineDriver": ("OpticGlass",), "LaserSMG": ("OpticGlass",),
                 "LaserPistol": ("FrontSight",), "LaserSniper": ("ScopeEyepiece", "ScopeLens")}
WEAPON_KIT = ("LineDriver", "LaserPistol", "LaserSniper", "LaserSMG", "Grenade")
WEAPON_SPECIALS = {"LaserShotgun": "PumpForeend", "PulseLobber": "OrbChamber",
                   "ReboundDisc": "DiscSignal", "PushLens": "InnerField",
                   "StunGun": "WarningBar", "LaserSword": "BladeCore"}
PALETTE_PREFIXES = ("MatteIvory", "Graphite", "SoftGrey", "SignalCyan", "SignalAmber", "SignalCoral",
                    "SignalViolet", "MatteCharcoalSuit", "DeepCharcoal", "HeadValue", "TeamMark", "OpticGlass")


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
        optics = SIGHT_TARGETS.get(path.stem, ())
        for anchor in ("Grip", "Muzzle") + (("Sight",) if optics else ()):
            assert anchor in names, (path.name, anchor)
            forward = bpy.data.objects[anchor].matrix_world.to_quaternion() @ Vector((0, -1, 0))
            assert forward.normalized().dot(Vector((0, -1, 0))) > .99, (path.name, anchor, forward)
        assert len({mat.name for obj in meshes for mat in obj.data.materials}) >= 2
        if path.stem in WEAPON_SPECIALS:
            moving = WEAPON_SPECIALS[path.stem]
            assert moving in names and bpy.data.objects[moving].type == "MESH", (path.name, moving)
            assert 1500 <= triangles <= 9000, (path.name, triangles)
            assert "LeftHand" in names, path.name
            for anchor in ("Grip", "Muzzle", "LeftHand"):
                obj = bpy.data.objects[anchor]
                assert obj.type == "EMPTY" and obj.rotation_euler.to_matrix().is_identity, (path.name, anchor)
            for obj in meshes:
                assert all(mat.name.startswith(PALETTE_PREFIXES) for mat in obj.data.materials), (path.name, obj.name)
        sight_line = {}
        if optics:
            sight = from_xyz(bpy.data.objects["Sight"].matrix_world.translation)
            sight_line["sight"] = [round(v, 5) for v in sight]
            for part in optics:
                low_p, high_p = mesh_bounds([bpy.data.objects[part]])
                # Front sight: top-centre of the post; optics and scope parts: bounds centre (on axis).
                point = [(low_p[i] + high_p[i]) / 2 for i in range(3)]
                if part == "FrontSight":
                    point[1] = high_p[1]
                miss = math.hypot(point[0] - sight[0], point[1] - sight[1])
                assert miss < .002 and point[2] > sight[2] + .02, (path.name, part, sight, point, miss)
                sight_line[part] = [round(v, 5) for v in point]
            if "OpticGlass" in optics:
                glass = bpy.data.objects["OpticGlass"]
                assert [mat.name for mat in glass.data.materials] == ["OpticGlass"], glass.data.materials[:]
                for polygon in glass.data.polygons:
                    normal = from_xyz(glass.matrix_world.to_3x3() @ polygon.normal)
                    assert normal[2] > .99, (path.name, normal)
        distinctive = {
            "LaserSMG": ("SMGReceiver", "SMGEmitter", "RedDotHousing", "OpticGlass"),
            "LineDriver": ("SplitRail", "SplitRail.001", "HoloSightFrame", "OpticGlass"),
            "PulseLobber": ("OrbChamber", "Bell"),
            "ReboundDisc": ("DiscCore", "Fork"),
            "PushLens": ("OpenLensFrame", "LensGuard"),
            "Grenade": ("Core", "FuseTab", "Shell", "GripBand", "FuseLight"),
            "StunGun": ("Prong", "ProngTip"),
            "LaserSword": ("BladeCore", "Guard"),
            "BlinkPickup": ("BlinkOuter", "BlinkNotch"),
            "ShieldPickup": ("ShieldBody", "ShieldStatus"),
            "HealthPickup": ("HealthCanister", "HealthCross"),
            "LaserPistol": ("PistolSlide", "PistolEmitter", "FrontSight", "RearSight"),
            "LaserShotgun": ("ShotgunBarrel", "PumpForeend"),
            "LaserSniper": ("PrecisionBarrel", "ScopeTube", "ScopeEyepiece"),
        }
        assert all(any(name.startswith(piece) for name in names) for piece in distinctive[path.stem]), (path.name, names)
        if path.stem in WEAPON_KIT or path.stem in WEAPON_SPECIALS:
            assert triangles <= (3000 if path.stem == "Grenade" else 9000), (path.name, triangles)
            assert all(mat.name.startswith(PALETTE_PREFIXES) for obj in meshes for mat in obj.data.materials)
            for anchor in ("Grip", "Muzzle", "LeftHand") + (("Sight",) if optics else ()):
                assert anchor in names, (path.name, anchor)
                obj = bpy.data.objects[anchor]
                assert obj.type == "EMPTY" and obj.rotation_euler.to_matrix().is_identity, (path.name, anchor)
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
    result = {"asset": path.name, "bounds_min": low, "bounds_max": high, "triangles": triangles,
              "mesh_count": len(meshes), "actions": sorted(action.name for action in bpy.data.actions)}
    if kind == "weapon" and sight_line:
        result["sight_line"] = sight_line
    if kind == "weapon" and path.stem in WEAPON_SPECIALS:
        result["moving_part"] = WEAPON_SPECIALS[path.stem]
        result["sockets"] = {n: [round(v, 5) for v in from_xyz(bpy.data.objects[n].matrix_world.translation)]
                             for n in ("Grip", "Muzzle", "LeftHand")}
        result["materials"] = sorted({mat.name for obj in meshes for mat in obj.data.materials})
        result["verify"] = "PASS: bounds, triangles, palette, identity sockets, muzzle ahead, moving part"
    if kind == "weapon" and path.stem in (*WEAPON_KIT, *WEAPON_SPECIALS):
        result["sockets"] = {n: list(from_xyz(bpy.data.objects[n].matrix_world.translation))
                             for n in ("Grip", "Muzzle", "LeftHand", "Sight") if n in names}
        result["materials"] = sorted({mat.name for obj in meshes for mat in obj.data.materials})
        result["signal_surface_m2"] = round(sum(poly.area for obj in meshes for poly in obj.data.polygons
                                                if obj.data.materials[poly.material_index].name.startswith("Signal")), 6)
    return result


def build_weapon_energy(root):
    """Surgical eleven-weapon pass, with live pre-export socket/extent/name comparisons."""
    entries = []
    for name in (*WEAPON_KIT, *WEAPON_SPECIALS):
        path = root / "Assets/Wildstone/Art" / (name + ".fbx")
        # Read the previous FBX directly: it may predate the new triangle budgets.
        clean()
        bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
        bpy.context.view_layer.update()
        before_names = {obj.name for obj in bpy.data.objects}
        before_sockets = {n: list(from_xyz(bpy.data.objects[n].matrix_world.translation))
                          for n in ("Grip", "Muzzle", "LeftHand", "Sight") if n in before_names}
        low, high = mesh_bounds(list(bpy.data.objects))
        before_extents = [high[i] - low[i] for i in range(3)]
        result = verify_asset(weapon(root, name), "weapon")
        assert before_names <= {obj.name for obj in bpy.data.objects}, (name, "Lost object names")
        assert before_sockets == result["sockets"], (name, before_sockets, result["sockets"])
        ratios = [(result["bounds_max"][i] - result["bounds_min"][i]) / before_extents[i] for i in range(3)]
        assert all(.95 <= ratio <= (1.40 if name == "LaserSword" and i == 2 else 1.05)
                   for i, ratio in enumerate(ratios)), (name, ratios)
        result.update(source_sha256=hashlib.sha256((root / "ArtSource" / (name + ".blend")).read_bytes()).hexdigest(),
                      fbx_sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                      verify="PASS: exact sockets, identity frames, bounds, triangle budget, palette, legacy names, optics")
        entries.append(result)
        print(name, result["triangles"], result["verify"])
        for socket, point in before_sockets.items():
            print(" ", socket, "before", point, "after", result["sockets"][socket])
    manifest = {"generator": "Tools/Assets/build_assets.py weapon-energy", "blender": bpy.app.version_string,
                "axis": "Unity (X,Y,Z) -> Blender (X,-Z,Y); FBX -Z forward, Y up; FBX_SCALE_ALL; metres",
                "provenance": "Original procedural energy cells, coils, field segments, blade and machined details; existing audited kit helpers; no downloaded/generated meshes or textures",
                "exports": entries}
    # Refresh matching entries only, leaving humans, hands, pickups and historical fields intact.
    for path in (root / "ArtSource").glob("*export-manifest*.json"):
        if path.name == "weapon-energy-export-manifest.json":
            continue
        data = json.loads(path.read_text(encoding="utf-8"))
        changed = False
        for entry in data.get("exports", []):
            name = Path(entry.get("asset", entry.get("name", ""))).stem
            match = next((e for e in entries if Path(e["asset"]).stem == name), None)
            if match:
                entry.update({k: match[k] for k in entry.keys() & match.keys()})
                changed = True
        if changed:
            data["latest_weapon_pass"] = "weapon-energy; see weapon-energy-export-manifest.json for current evidence"
            path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    (root / "ArtSource/weapon-energy-export-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


def preview_weapon_energy(root):
    """Lit Eevee + glare review, with full-weapon and close rear-right-above views."""
    from PIL import Image, ImageDraw, ImageFont
    names = (*WEAPON_KIT, *WEAPON_SPECIALS)
    previews = root / "ArtSource/Previews"
    (previews / "FirstPerson").mkdir(parents=True, exist_ok=True)
    for name in names:
        bpy.ops.wm.open_mainfile(filepath=str(root / "ArtSource" / (name + ".blend")))
        scene = bpy.context.scene
        scene.render.engine = "BLENDER_EEVEE_NEXT"
        scene.eevee.taa_render_samples = 32
        scene.render.image_settings.file_format = "PNG"
        scene.render.resolution_percentage = 100
        scene.view_settings.view_transform = "AgX"
        scene.world = bpy.data.worlds.new("ReviewWorld")
        scene.world.use_nodes = True
        scene.world.node_tree.nodes["Background"].inputs[0].default_value = (.035,.044,.055,1)
        scene.world.node_tree.nodes["Background"].inputs[1].default_value = .5
        scene.use_nodes = True
        tree = scene.node_tree
        tree.nodes.clear()
        layers = tree.nodes.new("CompositorNodeRLayers")
        glow = tree.nodes.new("CompositorNodeGlare")
        glow.glare_type = "FOG_GLOW"
        glow.quality = "HIGH"
        glow.threshold = 1.05
        glow.size = 7
        tree.links.new(layers.outputs["Image"], glow.inputs["Image"])
        tree.links.new(glow.outputs["Image"], tree.nodes.new("CompositorNodeComposite").inputs["Image"])
        bpy.context.view_layer.update()
        low, high = mesh_bounds(list(bpy.data.objects))
        target = Vector(xyz(tuple((a+b)/2 for a,b in zip(low,high))))
        span = max(b-a for a,b in zip(low,high))
        scale = max(span, .45)
        for direction, energy, size in (((2,1,3),120,2),((-2,-1,1),45,1.5),((0,-3,2),75,1.5)):
            bpy.ops.object.light_add(type="AREA", location=target+Vector(direction)*scale)
            light = bpy.context.object
            light.data.energy = energy*scale*scale
            light.data.shape = "DISK"
            light.data.size = size*scale
            light.rotation_euler = (target-light.location).to_track_quat("-Z","Y").to_euler()
        bpy.ops.object.camera_add()
        camera = bpy.context.object
        scene.camera = camera
        for close in (False, True):
            if close:
                direction = Vector((.80,1.9,.95)).normalized()
                orientation = (-direction).to_track_quat("-Z","Y")
                right, up = orientation @ Vector((1,0,0)), orientation @ Vector((0,1,0))
                points = [obj.matrix_world @ v.co - target for obj in bpy.data.objects
                          if obj.type == "MESH" for v in obj.data.vertices]
                # Fit the perspective frustum rather than cropping tall pistol grips or wide projector rings.
                tan_x, tan_y = 36 / (2*42), 36 / (2*42) * .7
                distance = max(p.dot(direction) + 1.13*max(abs(p.dot(right))/tan_x,abs(p.dot(up))/tan_y)
                               for p in points)
                eye = target + direction*distance
            else:
                eye = target + Vector((2.9,-3.8,2.2))*scale
            camera.location = eye
            camera.rotation_euler = (target-eye).to_track_quat("-Z","Y").to_euler()
            camera.data.type = "PERSP" if close else "ORTHO"
            camera.data.lens = 42
            camera.data.ortho_scale = max(span*1.35,.30)
            scene.render.resolution_x = 1000 if close else 900
            scene.render.resolution_y = 700 if close else 900
            scene.render.filepath = str(previews / ("FirstPerson" if close else "") / (name+".png"))
            bpy.ops.render.render(write_still=True)
        print(name, "full and first-person-angle previews rendered")
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf",21)
    for close in (False,True):
        tile_h = 310 if close else 430
        sheet = Image.new("RGB", (1600,3*tile_h), (18,22,28))
        draw = ImageDraw.Draw(sheet)
        for i,name in enumerate(names):
            x,y = (i%4)*400, (i//4)*tile_h
            with Image.open(previews / ("FirstPerson" if close else "") / (name+".png")) as im:
                sheet.paste(im.convert("RGB").resize((400,tile_h-30),Image.Resampling.LANCZOS),(x,y))
            draw.text((x+12,y+tile_h-30),name,fill=(230,232,235),font=font)
        path = previews / ("FirstPerson/WeaponSpecials-ContactSheet.png" if close else "WeaponSpecials-ContactSheet.png")
        sheet.save(path)
        print(path)


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


def build_weapon_kit(root):
    """Rebuild only the five kit weapons in place; .meta files and every other asset stay untouched."""
    exports = []
    for name in WEAPON_KIT:
        path = weapon(root, name)
        result = verify_asset(path, "weapon")
        exports.append({"name": name, "source": f"ArtSource/{name}.blend", "fbx": f"Assets/Wildstone/Art/{name}.fbx",
                        "source_sha256": hashlib.sha256((root / "ArtSource" / (name + ".blend")).read_bytes()).hexdigest(),
                        "fbx_sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                        **{key: result[key] for key in ("triangles", "mesh_count", "bounds_min", "bounds_max")},
                        **({"sight_line": result["sight_line"]} if "sight_line" in result else {})})
    manifest = {
        "generator": "Tools/Assets/build_assets.py weapon-kit", "blender": bpy.app.version_string,
        "provenance": ("Original procedural LineDriver (holo sight), LaserPistol (iron sights), LaserSniper (scope), "
                       "LaserSMG (red dot) and segmented Grenade. Lofted-section, side-slab and lathe techniques adapted "
                       "from read-only vibegame1 Tools/weapon_models/build_laser_kit.py and build_weapons.py at "
                       "2cb6e5760bccc0733f34b367be2f18c90b802766; no copied meshes, textures or third-party assets."),
        "axis": "Unity (X,Y,Z) -> Blender (X,-Z,Y); FBX -Z forward, Y up; metres; Unity import adds an X reflection",
        "sockets": "Grip, Muzzle, LeftHand on every kit weapon; Sight (forward = weapon +Z) on the four guns",
        "materials": "Palette materials plus OpticGlass (LineDriver, LaserSMG OpticGlass objects only)",
        "exports": exports,
    }
    (root / "ArtSource" / "weapon-kit-export-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(manifest, indent=2))


def build_weapon_specials(root):
    """Only six special weapons; update their existing manifest entries without regenerating the kit."""
    exports = []
    for name in WEAPON_SPECIALS:
        path = weapon(root, name)
        result = verify_asset(path, "weapon")
        result["source_sha256"] = hashlib.sha256((root / "ArtSource" / (name + ".blend")).read_bytes()).hexdigest()
        result["fbx_sha256"] = hashlib.sha256(path.read_bytes()).hexdigest()
        exports.append(result)
    manifest = {"generator": "Tools/Assets/build_assets.py weapon-specials", "blender": bpy.app.version_string,
                "axis": "Unity (X,Y,Z) -> Blender (X,-Z,Y); FBX -Z forward, Y up; FBX_SCALE_ALL; metres",
                "provenance": "Original procedural special weapons; same audited loft, slab and lathe techniques as weapon-kit; no copied meshes or textures",
                "exports": exports}
    (root / "ArtSource" / "weapon-specials-export-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    # Historical manifests also contain these assets: replace only matching entries.
    for filename in ("export-manifest.json", "laser-kit-export-manifest.json"):
        path = root / "ArtSource" / filename
        data = json.loads(path.read_text(encoding="utf-8"))
        for entry in data["exports"]:
            match = next((e for e in exports if e["asset"] == entry.get("asset", entry.get("name"))), None)
            if match:
                entry.update({k: match[k] for k in entry.keys() & match.keys()})
        path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(manifest, indent=2))


def preview_weapon_specials(root):
    """Reuse the kit's Workbench framing; compose existing kit previews without overwriting them."""
    import runpy
    import sys
    from PIL import Image, ImageDraw, ImageFont
    original_argv = sys.argv[:]
    try:
        for name in WEAPON_SPECIALS:
            sys.argv = ["render_previews.py", name]
            runpy.run_path(str(root / "Tools/Assets/render_previews.py"), run_name="__main__")
    finally:
        sys.argv = original_argv
    names = (*WEAPON_KIT, *WEAPON_SPECIALS)
    sheet = Image.new("RGB", (1600, 1290), (28, 31, 35))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 21)
    for index, name in enumerate(names):
        x, y = (index % 4) * 400, (index // 4) * 430
        with Image.open(root / "ArtSource/Previews" / (name + ".png")) as preview:
            sheet.paste(preview.convert("RGB").resize((400, 400), Image.Resampling.LANCZOS), (x, y))
        draw.text((x+14, y+399), name, fill=(230, 232, 235), font=font)
    path = root / "ArtSource/Previews/WeaponSpecials-ContactSheet.png"
    sheet.save(path)
    print(path)


def build_health_pickup(root):
    """Only the health pack pickup; no other model is rebuilt."""
    path = weapon(root, "HealthPickup")
    result = verify_asset(path, "weapon")
    result["source_sha256"] = hashlib.sha256((root / "ArtSource" / "HealthPickup.blend").read_bytes()).hexdigest()
    result["fbx_sha256"] = hashlib.sha256(path.read_bytes()).hexdigest()
    manifest = {"generator": "Tools/Assets/build_assets.py health-pickup", "blender": bpy.app.version_string,
                "axis": "Unity (X,Y,Z) -> Blender (X,-Z,Y); FBX -Z forward, Y up; FBX_SCALE_ALL; metres",
                "provenance": "Original procedural pickup from the existing box helper; no copied meshes or textures",
                "exports": [result]}
    (root / "ArtSource" / "health-pickup-export-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(manifest, indent=2))


def build_all(root):
    output = []
    for name in ("LineDriver", "PulseLobber", "ReboundDisc", "PushLens", "Grenade", "StunGun", "LaserSword", "BlinkPickup", "ShieldPickup", "HealthPickup", "LaserPistol", "LaserShotgun", "LaserSniper", "LaserSMG"):
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
    parser.add_argument("mode", choices=["fixture", "human", "all", "laser-kit", "weapon-kit", "weapon-specials", "weapon-specials-previews", "weapon-energy", "weapon-energy-previews", "health-pickup"])
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
    elif args.mode == "weapon-kit":
        build_weapon_kit(root)
    elif args.mode == "weapon-specials":
        build_weapon_specials(root)
    elif args.mode == "weapon-specials-previews":
        preview_weapon_specials(root)
    elif args.mode == "weapon-energy":
        build_weapon_energy(root)
    elif args.mode == "weapon-energy-previews":
        preview_weapon_energy(root)
    elif args.mode == "health-pickup":
        build_health_pickup(root)
    else:
        build_all(root)

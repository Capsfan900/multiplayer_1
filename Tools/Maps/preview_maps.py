"""Blender previews and geometry lint for the exported Wildstone arena definitions.

The JSON is emitted from ArenaLayouts.CreateAll() by managed code. This script
does not alter it. Lint is geometric evidence, not a Unity collider/physics test.

Run with the ai_skelly_tool Python that includes bpy, e.g.:
  python -B Tools/Maps/preview_maps.py lint
  python -B Tools/Maps/preview_maps.py build --map atrium
"""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource" / "Maps" / "layouts.json"
DEST = ROOT / "ArtSource" / "Maps"
PREVIEWS = DEST / "Previews"
PALETTE = ((0.79, 0.77, 0.71), (0.52, 0.55, 0.55), (0.16, 0.19, 0.20))


def xyz(value):
    return (float(value["x"]), float(value["y"]), float(value["z"]))


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def matmul(a, b):
    return tuple(tuple(sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)) for i in range(3))


def rotate_matrix(euler):
    """Unity Quaternion.Euler: extrinsic Z, then X, then Y (Ry * Rx * Rz)."""
    x, y, z = (math.radians(v) for v in xyz(euler))
    cx, sx, cy, sy, cz, sz = math.cos(x), math.sin(x), math.cos(y), math.sin(y), math.cos(z), math.sin(z)
    rx = ((1, 0, 0), (0, cx, -sx), (0, sx, cx))
    ry = ((cy, 0, sy), (0, 1, 0), (-sy, 0, cy))
    rz = ((cz, -sz, 0), (sz, cz, 0), (0, 0, 1))
    return matmul(matmul(ry, rx), rz)


def rotated(matrix, v):
    return tuple(dot(row, v) for row in matrix)


def unity_to_blender(v):
    return (v[0], -v[2], v[1])


class Box:
    def __init__(self, piece):
        self.name = piece["Name"]
        self.position = xyz(piece["Position"])
        self.size = xyz(piece["Size"])
        self.half = tuple(v / 2 for v in self.size)
        self.rotation = rotate_matrix(piece["Euler"])
        self.tone = int(piece["Tone"])
        corners = [self.world((x*self.half[0], y*self.half[1], z*self.half[2]))
                   for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)]
        self.minimum = tuple(min(p[i] for p in corners) for i in range(3))
        self.maximum = tuple(max(p[i] for p in corners) for i in range(3))

    def local(self, world):
        delta = tuple(world[i] - self.position[i] for i in range(3))
        return tuple(sum(self.rotation[j][i] * delta[j] for j in range(3)) for i in range(3))

    def vector_local(self, v):
        return tuple(sum(self.rotation[j][i] * v[j] for j in range(3)) for i in range(3))

    def world(self, local):
        v = rotated(self.rotation, local)
        return tuple(v[i] + self.position[i] for i in range(3))

    def closest_distance(self, point):
        p = self.local(point)
        return math.sqrt(sum(max(abs(p[i]) - self.half[i], 0) ** 2 for i in range(3)))

    def top_height(self, x, z):
        """Vertical ray intersection with oriented positive local Y face."""
        if not (self.minimum[0]-1e-5 <= x <= self.maximum[0]+1e-5 and
                self.minimum[2]-1e-5 <= z <= self.maximum[2]+1e-5):
            return None
        normal_y = self.rotation[1][1]
        if normal_y < 0.7:
            return None
        top_origin = self.world((0, self.half[1], 0))
        normal = tuple(self.rotation[i][1] for i in range(3))
        height = top_origin[1] - (normal[0] * (x - top_origin[0]) + normal[2] * (z - top_origin[2])) / normal_y
        loc = self.local((x, height, z))
        if abs(loc[0]) <= self.half[0] + 1e-5 and abs(loc[2]) <= self.half[2] + 1e-5:
            return height
        return None

    def blocks_capsule(self, feet):
        """Exact segment-to-OBB distance for ArenaValidation's .38 m capsule."""
        radius = 0.38
        start = (feet[0], feet[1]+0.43, feet[2])
        end = (feet[0], feet[1]+1.43, feet[2])
        if any(end[i]+radius < self.minimum[i] or start[i]-radius > self.maximum[i]
               for i in range(3)):
            return False
        a, b = self.local(start), self.local(end)
        delta = tuple(b[i]-a[i] for i in range(3))
        cuts = [0.0, 1.0]
        for i in range(3):
            if abs(delta[i]) > 1e-9:
                cuts.extend(t for face in (-self.half[i], self.half[i])
                            if 0 < (t := (face-a[i])/delta[i]) < 1)
        cuts.sort()
        # Between face crossings, squared distance to the box is one quadratic.
        for low, high in zip(cuts, cuts[1:]):
            middle = (low+high)/2
            terms = [(a[i]-math.copysign(self.half[i], a[i]+middle*delta[i]), delta[i])
                     for i in range(3) if abs(a[i]+middle*delta[i]) > self.half[i]]
            slope = sum(d*d for _, d in terms)
            t = min(high, max(low, -sum(p*d for p, d in terms)/slope)) if slope else low
            if sum((p+t*d)**2 for p, d in terms) <= radius*radius:
                return True
        return False

    def segment_intersection(self, start, end):
        a, b = self.local(start), self.local(end)
        d = tuple(b[i] - a[i] for i in range(3))
        lo, hi = 0.0, 1.0
        for i in range(3):
            if abs(d[i]) < 1e-9:
                if abs(a[i]) > self.half[i]:
                    return False
                continue
            t0, t1 = (-self.half[i] - a[i]) / d[i], (self.half[i] - a[i]) / d[i]
            lo, hi = max(lo, min(t0, t1)), min(hi, max(t0, t1))
            if lo > hi:
                return False
        return hi > 0.01 and lo < 0.99


def support_height(boxes, point):
    # Match the native downward ray's origin/range; clearance uses the hit height.
    x, y, z = point
    return max((h for b in boxes if (h := b.top_height(x, z)) is not None
                and y-1.4 <= h <= y+1.4), default=None)


def clearance(boxes, point, tolerance):
    height = support_height(boxes, point)
    if height is None or abs(height-point[1]) > tolerance:
        return False, []
    feet = (point[0], height, point[2])
    return True, [b.name for b in boxes if b.blocks_capsule(feet)]


def lint(arena):
    boxes = [Box(p) for p in arena["Pieces"]]
    issues = []
    route = [xyz(p) for p in arena["ZoneRoute"]]
    route_failures = []
    route_obstructions = []
    offsets = [(0, 0)] + [(radius * math.cos(a * math.pi / 12), radius * math.sin(a * math.pi / 12))
                         for radius in (2, 4) for a in range(24)]
    for segment in range(len(route)):
        start, end = route[segment], route[(segment + 1) % len(route)]
        length = math.dist(start, end)
        samples = max(1, math.ceil(length * 2))
        for step in range(samples + 1):
            t = step / samples
            center = tuple(start[i] * (1 - t) + end[i] * t for i in range(3))
            # A 4 m zone disk, checked at centre, half radius and its perimeter.
            unsupported = []
            blocked = []
            for index, (dx, dz) in enumerate(offsets):
                point = (center[0]+dx, center[1], center[2]+dz)
                supported, obstacles = clearance(boxes, point, .2 if index == 0 else 1.25)
                if not supported:
                    unsupported.append((point[0], point[2]))
                elif obstacles:
                    blocked.append((point, obstacles))
            if unsupported:
                route_failures.append((segment, center, unsupported[0], len(unsupported)))
            if blocked:
                route_obstructions.append((segment, center, blocked[0]))
    grouped = {}
    for seg, center, point, count in route_failures:
        entry = grouped.setdefault(seg, [0, center, point, 0])
        entry[0] += 1
        entry[3] = max(entry[3], count)
    for seg, (count, center, point, worst) in grouped.items():
        issues.append(f"zone segment {seg}->{(seg+1)%len(route)}: {count} sampled centres have unsupported 4 m disk; example centre {tuple(round(v,2) for v in center)}, footprint xz {tuple(round(v,2) for v in point)}, worst {worst}/49 disk samples")
    obstructed_segments = {}
    for seg, center, obstruction in route_obstructions:
        entry = obstructed_segments.setdefault(seg, [0, center, obstruction])
        entry[0] += 1
    for seg, (count, center, (point, obstacles)) in obstructed_segments.items():
        issues.append(f"zone segment {seg}->{(seg+1)%len(route)}: {count} sampled centres have capsule clearance blocked; example centre {tuple(round(v,2) for v in center)}, footprint {tuple(round(v,2) for v in point)} by {obstacles}")

    spawns = [[p["Position"] for p in arena["Spawns"] if p["Team"] == team] for team in range(4)]
    for team_index, team in enumerate(spawns):
        for spawn_index, spawn in enumerate(team):
            p = xyz(spawn)
            supported, blockers = clearance(boxes, p, .2)
            if not supported:
                issues.append(f"team {team_index} spawn {spawn_index} at {p}: no floor at feet")
            if blockers:
                issues.append(f"team {team_index} spawn {spawn_index} at {p}: capsule clearance blocked by {blockers}")
    for pickup in arena["Pickups"]:
        p = xyz(pickup["Position"])
        supported, blockers = clearance(boxes, (p[0], p[1]-.45, p[2]), .2)
        if not supported:
            issues.append(f"pickup {pickup['Item']} at {p}: no floor 0.45 m below")
        if blockers:
            issues.append(f"pickup {pickup['Item']} at {p}: clearance blocked by {blockers}")

    # Eye rays from opening team starts, excluding floor slabs below eye height.
    los = []
    for i in range(len(spawns)):
        for j in range(i + 1, len(spawns)):
            for si, spawn_a in enumerate(spawns[i]):
                for sj, spawn_b in enumerate(spawns[j]):
                    a, b = xyz(spawn_a), xyz(spawn_b)
                    eye_a, eye_b = (a[0], a[1] + 1.6, a[2]), (b[0], b[1] + 1.6, b[2])
                    if not any(box.segment_intersection(eye_a, eye_b) for box in boxes):
                        los.append((i, si, j, sj))
    if los:
        issues.append(f"{len(los)} unobstructed opposing spawn eye rays (of {sum(len(spawns[i])*len(spawns[j]) for i in range(len(spawns)) for j in range(i+1,len(spawns)))}); examples {los[:8]}")
    return issues, len(route_failures), len(los)


def make_material(bpy, name, rgb, metallic=0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*rgb, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = 0.83
    bsdf.inputs["Metallic"].default_value = metallic
    return mat


def add_box(bpy, box, materials, collection):
    local = [(-1,-1,-1), (1,-1,-1), (1,1,-1), (-1,1,-1), (-1,-1,1), (1,-1,1), (1,1,1), (-1,1,1)]
    verts = [unity_to_blender(box.world(tuple(local[i][j]*box.half[j] for j in range(3)))) for i in range(8)]
    faces = [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
    mesh = bpy.data.meshes.new(box.name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(box.name, mesh)
    collection.objects.link(obj)
    obj.data.materials.append(materials[box.tone])
    bevel = obj.modifiers.new("LevelMesh chamfer preview", "BEVEL")
    bevel.width = 0.04 if min(box.size) < 0.5 else 0.06
    bevel.segments = 2
    bevel.affect = 'EDGES'
    obj.modifiers.new("Weighted normals", "WEIGHTED_NORMAL")
    return obj


def add_line(bpy, name, points, material, collection, thickness=0.09, closed=False):
    curve = bpy.data.curves.new(name, 'CURVE')
    curve.dimensions = '3D'
    curve.resolution_u = 1
    curve.bevel_depth = thickness
    curve.bevel_resolution = 2
    spline = curve.splines.new('POLY')
    spline.points.add(len(points)-1)
    for target, point in zip(spline.points, points):
        target.co = (*unity_to_blender(point), 1)
    spline.use_cyclic_u = closed
    obj = bpy.data.objects.new(name, curve)
    collection.objects.link(obj)
    curve.materials.append(material)


def add_marker(bpy, name, pos, mat, collection, radius=0.45):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=1, location=unity_to_blender(pos))
    obj = bpy.context.object
    obj.name = name
    for old in tuple(obj.users_collection):
        old.objects.unlink(obj)
    collection.objects.link(obj)
    obj.scale = (radius, radius, 0.08)
    obj.data.materials.append(mat)


def aim_camera(bpy, cam, origin, target):
    from mathutils import Vector
    cam.location = unity_to_blender(origin)
    direction = Vector(unity_to_blender(target)) - cam.location
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()


def render_scene(bpy, path, marker_collection, camera, marker_visibility, resolution):
    marker_collection.hide_render = not marker_visibility
    bpy.context.scene.camera = camera
    bpy.context.scene.render.resolution_x, bpy.context.scene.render.resolution_y = resolution
    bpy.context.scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def build(arena, render=True):
    import bpy
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 16
    scene.cycles.use_denoising = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.look = 'AgX - Medium High Contrast'
    scene.world.color = (0.68, 0.72, 0.77)
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get('Background')
    bg.inputs['Color'].default_value = (0.72, 0.78, 0.83, 1)
    bg.inputs['Strength'].default_value = 0.55
    architecture = bpy.data.collections.new('Authored layout geometry')
    scene.collection.children.link(architecture)
    markers = bpy.data.collections.new('Preview markers - not structural')
    scene.collection.children.link(markers)
    materials = [make_material(bpy, name, rgb) for name, rgb in zip(('Ivory', 'Ash', 'Charcoal'), PALETTE)]
    for piece in arena['Pieces']:
        add_box(bpy, Box(piece), materials, architecture)

    route_mat = make_material(bpy, 'Zone route marker', (0.20, 0.74, 0.75))
    spawn_colors = ((0.9,0.3,0.22),(0.28,0.55,0.88),(0.9,0.67,0.2),(0.6,0.38,0.8))
    spawn_mats = [make_material(bpy, f'Team {i} preview marker', color) for i,color in enumerate(spawn_colors)]
    pickup_mat = make_material(bpy, 'Pickup preview marker', (0.89, 0.86, 0.57))
    add_line(bpy, 'Closed zone route', [(p['x'],p['y']+0.16,p['z']) for p in arena['ZoneRoute']], route_mat, markers, closed=True)
    spawn_groups = [[p['Position'] for p in arena['Spawns'] if p['Team'] == team] for team in range(4)]
    for team, group in enumerate(spawn_groups):
        for index, point in enumerate(group):
            pos = xyz(point)
            add_marker(bpy, f'Team {team} spawn {index}', (pos[0],pos[1]+0.1,pos[2]), spawn_mats[team], markers, radius=0.65)
    for point in arena['Pickups']:
        pos = xyz(point['Position'])
        add_marker(bpy, f"{point['Item']} pickup", (pos[0],pos[1]+0.1,pos[2]), pickup_mat, markers, radius=0.30)

    # A large soft key and cool fill follow the accepted C3.1 daylight intent.
    for name, location, energy, color, size in (
        ('Soft daylight', (20, 45, 45), 5700, (1.0, .93, .84), 42),
        ('Sky fill', (-35, 20, -20), 1600, (.77, .86, 1.0), 40),
    ):
        data = bpy.data.lights.new(name, 'AREA')
        data.energy, data.color, data.shape, data.size = energy, color, 'DISK', size
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        aim_camera(bpy, obj, location, (0,0,0))

    minimum, maximum = xyz(arena['Minimum']), xyz(arena['Maximum'])
    span = max(maximum[0]-minimum[0], maximum[2]-minimum[2])
    overview_data = bpy.data.cameras.new('Isometric camera')
    overview_data.type = 'ORTHO'
    overview_data.ortho_scale = span * 1.53
    overview = bpy.data.objects.new('Isometric camera', overview_data)
    scene.collection.objects.link(overview)
    aim_camera(bpy, overview, (span*.72,span*.85,-span*.9), (0,1.5,0))
    view_specs = {
        'atrium': ((-6,1.65,-24),(4,3.7,10)),
        'split-gallery': ((30,1.65,8),(0,-2,14)),
        'terrace-steps': ((20,1.65,-26),(12,-2,2)),
    }
    eye, target = view_specs[arena['Id']]
    eye_data = bpy.data.cameras.new('Human height camera')
    eye_data.type = 'PERSP'
    eye_data.lens = 24
    eye_cam = bpy.data.objects.new('Human height camera', eye_data)
    scene.collection.objects.link(eye_cam)
    aim_camera(bpy, eye_cam, eye, target)
    scene.camera = overview
    bpy.context.preferences.filepaths.save_version = 0
    DEST.mkdir(parents=True, exist_ok=True)
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    markers.hide_render = True
    bpy.ops.wm.save_as_mainfile(filepath=str(DEST / f"{arena['Id']}.blend"))
    if render:
        render_scene(bpy, PREVIEWS / f"{arena['Id']}-isometric.png", markers, overview, False, (1100,800))
        render_scene(bpy, PREVIEWS / f"{arena['Id']}-route.png", markers, overview, True, (1100,800))
        render_scene(bpy, PREVIEWS / f"{arena['Id']}-human-height.png", markers, eye_cam, False, (1100,700))


def verify(arena):
    """Read the saved .blend and check its actual unmodified authored mesh vertices."""
    import bpy
    path = DEST / f"{arena['Id']}.blend"
    bpy.ops.wm.open_mainfile(filepath=str(path))
    for piece in arena['Pieces']:
        box = Box(piece)
        obj = bpy.data.objects.get(box.name)
        if obj is None or obj.type != 'MESH' or len(obj.data.vertices) != 8:
            raise AssertionError(f"{path.name}: missing or non-box mesh {box.name}")
        actual = [tuple(v.co) for v in obj.data.vertices]
        expected = [unity_to_blender(box.world((sx*box.half[0],sy*box.half[1],sz*box.half[2])))
                    for sx,sy,sz in ((-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1))]
        error = max(math.dist(a, e) for a, e in zip(actual, expected))
        if error > 0.0001:
            raise AssertionError(f"{path.name}: {box.name} vertex mismatch {error:.6f} m")
        bevel = next((m for m in obj.modifiers if m.type == 'BEVEL'), None)
        width = 0.04 if min(box.size) < 0.5 else 0.06
        if bevel is None or abs(bevel.width - width) > 1e-6 or len(obj.data.materials) != 1:
            raise AssertionError(f"{path.name}: {box.name} bevel/material mismatch")
    print(f"  VERIFIED: {path.name}: {len(arena['Pieces'])} saved meshes, 8 Unity-converted corners each within 0.1 mm, bevel/material")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=('lint','build','verify'))
    parser.add_argument('--map', choices=('atrium','split-gallery','terrace-steps'))
    parser.add_argument('--no-render', action='store_true')
    args = parser.parse_args()
    arenas = json.loads(SOURCE.read_text(encoding='utf-8'))
    selected = [a for a in arenas if args.map is None or a['Id']==args.map]
    if not selected:
        parser.error('Map not in exported layouts.json')
    failed = False
    for arena in selected:
        issues, unsupported, clear_los = lint(arena)
        print(f"{arena['Id']}: {len(arena['Pieces'])} boxes, {len(arena['ZoneRoute'])} zone nodes, {unsupported} unsupported sampled centres, {clear_los} clear opposing spawn rays")
        for issue in issues:
            print('  LINT:', issue)
        failed |= bool(issues)
    if failed:
        return 1
    for arena in selected:
        if args.action == 'build':
            build(arena, render=not args.no_render)
            print('  SAVED:', DEST / f"{arena['Id']}.blend")
        elif args.action == 'verify':
            verify(arena)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())

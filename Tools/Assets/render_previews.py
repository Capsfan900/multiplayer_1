"""Render local Workbench review stills from editable Wildstone .blend files."""
import argparse
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument("name")
parser.add_argument("clip", nargs="?")
parser.add_argument("frame", nargs="?", type=int)
parser.add_argument("--team", type=int, choices=range(1, 5), default=1)
parser.add_argument("--view", choices=("front", "back"), default="front")
args = parser.parse_args()
name = args.name
source = ROOT / "ArtSource" / (name + ".blend")
assert source.is_file() and source.parent == ROOT / "ArtSource"
bpy.ops.wm.open_mainfile(filepath=str(source))
suffix = ""
if args.clip:
    assert args.frame is not None
    rig = bpy.data.objects[name]
    rig.animation_data.action = bpy.data.actions[args.clip]
    bpy.context.scene.frame_set(args.frame)
    suffix = f"_{args.clip}{args.frame}"
if name == "WildstoneHuman":
    for obj in bpy.data.objects:
        if obj.name.startswith("Team"):
            obj.hide_render = not obj.name.startswith(f"Team{args.team:02d}_")
    if args.team != 1 or args.view != "front":
        suffix += f"_Team{args.team}_{args.view.capitalize()}"
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.render.resolution_x = 900
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
human = name == "WildstoneHuman"
points = [obj.matrix_world @ vertex.co for obj in bpy.data.objects if obj.type == "MESH" for vertex in obj.data.vertices]
low = Vector(tuple(min(point[i] for point in points) for i in range(3)))
high = Vector(tuple(max(point[i] for point in points) for i in range(3)))
target = (low + high) / 2
span = max(high - low)
direction = Vector((2.9, -3.8, 2.2)) if args.view == "front" else Vector((-2.9, 3.8, 2.2))
eye = target + direction * max(span, .5)
bpy.ops.object.camera_add(location=eye)
camera = bpy.context.object
camera.rotation_euler = (target - eye).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = max(span * 1.45, .48)
scene.camera = camera
output = ROOT / "ArtSource" / "Previews" / (name + suffix + ".png")
output.parent.mkdir(parents=True, exist_ok=True)
scene.render.filepath = str(output)
bpy.ops.render.render(write_still=True)
print(output)

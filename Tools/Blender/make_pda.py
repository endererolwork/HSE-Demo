"""
Builds the clinical PDA (rugged handheld with a barcode/QR scanner on the top edge) and exports it to Unity.

Run headless:
  blender --background --factory-startup --python Tools/Blender/make_pda.py -- <project_root> [preview.png]

Modelling frame (metres): screen faces +Z, scanner window on the +Y end, X is the device width.
Object names are used by the Unity prefab builder to assign materials, so keep them stable.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PROJECT = argv[0] if argv else os.getcwd()
PREVIEW = argv[1] if len(argv) > 1 else None
OUT_DIR = os.path.join(PROJECT, "Assets", "HSE", "Models", "PDA")
os.makedirs(OUT_DIR, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------------------------------------------------------- materials (preview + names for Unity)

def mat(name, rgb, rough=0.4, metal=0.0, emit=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    if emit:
        bsdf.inputs["Emission Color"].default_value = (*emit, 1)
        bsdf.inputs["Emission Strength"].default_value = 1.0
    return m

M = {
    "White": mat("PDA_White", (0.92, 0.93, 0.94), 0.35),
    "Blue": mat("PDA_Blue", (0.10, 0.25, 0.62), 0.55),
    "Glass": mat("PDA_Glass", (0.01, 0.012, 0.016), 0.05),
    "ScanRed": mat("PDA_ScanRed", (0.35, 0.02, 0.03), 0.05, emit=(0.25, 0.0, 0.0)),
    "Orange": mat("PDA_Orange", (0.95, 0.45, 0.08), 0.45),
    "Dark": mat("PDA_Dark", (0.08, 0.085, 0.09), 0.5),
    "Gold": mat("PDA_Gold", (0.85, 0.65, 0.25), 0.3, 1.0),
    "Gray": mat("PDA_Gray", (0.55, 0.57, 0.6), 0.5),
}

# ---------------------------------------------------------------- geometry helpers

def rounded_rect(w, l, r, seg=10):
    pts = []
    r = min(r, w / 2 - 1e-5, l / 2 - 1e-5)
    corners = [(w / 2 - r, l / 2 - r, 0), (-w / 2 + r, l / 2 - r, 90), (-w / 2 + r, -l / 2 + r, 180), (w / 2 - r, -l / 2 + r, 270)]
    for cx, cy, a0 in corners:
        for i in range(seg + 1):
            a = math.radians(a0 + 90 * i / seg)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def prism(name, w, l, t, r, center=(0, 0, 0), material=None, bevel=0.0012, seg=10, axis="Z"):
    """Rounded-rectangle slab. axis is the slab normal: Z (lying flat), X or Y (standing on a side)."""
    bm = bmesh.new()
    pts = rounded_rect(w, l, r, seg)
    bottom = [bm.verts.new((x, y, -t / 2)) for x, y in pts]
    top = [bm.verts.new((x, y, t / 2)) for x, y in pts]
    bm.faces.new(list(reversed(bottom)))
    bm.faces.new(top)
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bottom[i], bottom[j], top[j], top[i]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    scene.collection.objects.link(ob)
    if axis == "X":
        ob.rotation_euler = (0, math.radians(90), 0)
    elif axis == "Y":
        ob.rotation_euler = (math.radians(90), 0, 0)
    ob.location = center
    if material:
        me.materials.append(material)
    if bevel > 0:
        mod = ob.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 3
        mod.limit_method = "ANGLE"
        mod.angle_limit = math.radians(40)
        mod.harden_normals = True
    return ob

# ---------------------------------------------------------------- the device

W, L, T = 0.078, 0.168, 0.014           # white shell
BW, BL, BT = 0.0845, 0.1745, 0.0105     # blue rubber bumper

body = prism("Body", W, L, T, 0.011, (0, 0, 0), M["White"], bevel=0.0025, seg=12)
bumper = prism("Bumper", BW, BL, BT, 0.0145, (0, 0, -0.0012), M["Blue"], bevel=0.003, seg=12)

screen_center_y = -0.004
prism("Screen", 0.068, 0.128, 0.0008, 0.0045, (0, screen_center_y, T / 2 + 0.0002), M["Glass"], bevel=0.0003)

# Scanner head on the +Y end: raised hood plus a dark red exit window facing +Y.
prism("ScannerHood", 0.056, 0.008, 0.0112, 0.003, (0, BL / 2 - 0.002, -0.0012), M["Dark"], bevel=0.0015, axis="Z")
prism("ScanWindow", 0.036, 0.0065, 0.0015, 0.002, (0, BL / 2 + 0.0024, -0.0012), M["ScanRed"], bevel=0.0004, axis="Y")

# Front details.
prism("Speaker", 0.014, 0.0018, 0.0008, 0.0009, (0, 0.0715, T / 2 + 0.0001), M["Dark"], bevel=0)
prism("FrontCamera", 0.003, 0.003, 0.0008, 0.0015, (0.012, 0.0715, T / 2 + 0.0001), M["Dark"], bevel=0)
prism("StatusLED", 0.003, 0.0016, 0.0008, 0.0008, (-0.014, 0.0715, T / 2 + 0.0001), M["ScanRed"], bevel=0)
prism("HomeKey", 0.012, 0.0035, 0.0008, 0.0017, (0, -0.0765, T / 2 + 0.0001), M["Gray"], bevel=0)

# Side scan triggers (orange) and volume keys.
for side, sx in (("L", -1), ("R", 1)):
    prism("ScanButton_" + side, 0.0055, 0.022, 0.003, 0.0025, (sx * (BW / 2 + 0.0006), 0.012, 0.0005), M["Orange"], bevel=0.0008, axis="X")
prism("VolumeKey", 0.0045, 0.016, 0.0025, 0.002, (-(BW / 2 + 0.0004), 0.045, 0.0005), M["Blue"], bevel=0.0007, axis="X")
prism("PowerKey", 0.0045, 0.009, 0.0025, 0.002, ((BW / 2 + 0.0004), 0.045, 0.0005), M["Gray"], bevel=0.0007, axis="X")

# Bottom end: charging contacts and lanyard loop.
for i, x in enumerate((-0.006, -0.002, 0.002, 0.006)):
    prism("Contact_%d" % i, 0.0022, 0.0035, 0.0006, 0.0005, (x, -(BL / 2) - 0.0001, -0.002), M["Gold"], bevel=0, axis="Y")

# Back: battery cover, camera island, grip pads.
prism("BatteryCover", 0.062, 0.11, 0.0016, 0.006, (0, -0.012, -T / 2 - 0.0006), M["Gray"], bevel=0.0005)
prism("CameraIsland", 0.022, 0.012, 0.0016, 0.004, (0, 0.06, -T / 2 - 0.0006), M["Dark"], bevel=0.0005)
prism("CameraLens", 0.006, 0.006, 0.0008, 0.003, (-0.004, 0.06, -T / 2 - 0.0016), M["Glass"], bevel=0)
prism("Flash", 0.004, 0.004, 0.0008, 0.002, (0.005, 0.06, -T / 2 - 0.0016), M["White"], bevel=0)

# Empties Unity uses: where the laser starts and where the UI canvas sits.
laser = bpy.data.objects.new("LaserOrigin", None)
laser.location = (0, BL / 2 + 0.0035, -0.0012)
scene.collection.objects.link(laser)
ui = bpy.data.objects.new("ScreenAnchor", None)
ui.location = (0, screen_center_y, T / 2 + 0.0007)
scene.collection.objects.link(ui)

# ---------------------------------------------------------------- export

fbx = os.path.join(OUT_DIR, "PDA.fbx")
bpy.ops.export_scene.fbx(
    filepath=fbx,
    use_selection=False,
    object_types={"MESH", "EMPTY"},
    use_mesh_modifiers=True,
    mesh_smooth_type="OFF",
    apply_scale_options="FBX_SCALE_UNITS",
    bake_space_transform=True,
    axis_forward="-Z",
    axis_up="Y",
    add_leaf_bones=False,
)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROJECT, "Tools", "Blender", "PDA.blend"))
print("[PDA] exported", fbx)

# ---------------------------------------------------------------- optional preview render

if PREVIEW:
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.85, 0.88, 0.9, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
    scene.world = world

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 70
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = (0.22, 0.3, 0.22)
    direction = Vector((0, 0.005, 0)) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam

    for loc, energy in (((0.3, -0.2, 0.5), 12), ((-0.4, 0.3, 0.3), 5)):
        ld = bpy.data.lights.new("Key", "AREA")
        ld.energy = energy
        ld.size = 0.4
        lo = bpy.data.objects.new("Key", ld)
        lo.location = loc
        lo.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        scene.collection.objects.link(lo)

    # Fake screen content so the preview reads as a device.
    bpy.data.objects["Screen"].data.materials[0] = mat("PreviewScreen", (0.05, 0.25, 0.5), 0.1, emit=(0.15, 0.45, 0.85))

    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    scene.render.filepath = PREVIEW
    bpy.ops.render.render(write_still=True)
    print("[PDA] preview", PREVIEW)

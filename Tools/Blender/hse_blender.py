"""
Shared helpers for the HSE Blender model scripts.

Conventions (read by Assets/HSE/Editor/ModelLibrary.cs):
  * Object names end in "__<MaterialKey>", e.g. "Headboard__WoodLight". Unity assigns its own URP material with that key.
  * Empties "Marker_Forward" and "Marker_Up" sit one metre from the origin along the model's front and up axes,
    so Unity can orient the model no matter how the FBX axes were converted.
  * Modelling frame: Z up, metres, origin on the floor at the model's footprint centre.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

PREVIEW_COLORS = {
    "PlasticWhite": (0.92, 0.92, 0.91), "PlasticGray": (0.5, 0.52, 0.55), "DarkMetal": (0.18, 0.19, 0.2),
    "Metal": (0.75, 0.76, 0.78), "WoodLight": (0.62, 0.45, 0.3), "Mattress": (0.2, 0.32, 0.45),
    "Sheet": (0.95, 0.95, 0.96), "Blanket": (0.25, 0.48, 0.52), "Black": (0.03, 0.03, 0.03),
    "Screen": (0.01, 0.015, 0.02), "Accent": (0.1, 0.35, 0.7), "Yellow": (0.95, 0.75, 0.05),
    "GreenO2": (0.05, 0.5, 0.2), "Red": (0.7, 0.08, 0.08), "UpholsteryBlue": (0.08, 0.16, 0.32),
    "UpholsteryTeal": (0.1, 0.34, 0.36), "UpholsteryTealLight": (0.18, 0.45, 0.45), "Rubber": (0.06, 0.06, 0.065),
    "MonitorBody": (0.15, 0.16, 0.18), "EmissiveGreen": (0.2, 1.0, 0.3), "ScreenBlue": (0.03, 0.12, 0.28),
}
_mats = {}


def args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    project = argv[0] if argv else os.getcwd()
    preview = argv[1] if len(argv) > 1 else None
    return project, preview


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mats.clear()


def material(key):
    if key not in _mats:
        m = bpy.data.materials.new(key)
        m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = (*PREVIEW_COLORS.get(key, (0.6, 0.6, 0.6)), 1)
        b.inputs["Roughness"].default_value = 0.45
        _mats[key] = m
    return _mats[key]


def _finish(ob, key, bevel, seg=3, angle=40):
    ob.name = ob.name if "__" in ob.name else ob.name + "__" + key
    ob.data.materials.clear()
    ob.data.materials.append(material(key))
    for p in ob.data.polygons:
        p.use_smooth = True
    if bevel > 0:
        mod = ob.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = seg
        mod.limit_method = "ANGLE"
        mod.angle_limit = math.radians(angle)
        mod.harden_normals = True
    return ob


def _link(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def rounded_rect(w, l, r, seg=8):
    r = max(1e-4, min(r, w / 2 - 1e-4, l / 2 - 1e-4))
    pts = []
    for cx, cy, a0 in ((w / 2 - r, l / 2 - r, 0), (-w / 2 + r, l / 2 - r, 90), (-w / 2 + r, -l / 2 + r, 180), (w / 2 - r, -l / 2 + r, 270)):
        for i in range(seg + 1):
            a = math.radians(a0 + 90 * i / seg)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def slab(name, key, w, l, t, r, loc=(0, 0, 0), rot=(0, 0, 0), bevel=0.004, seg=8, bevel_seg=3, parent=None):
    """Rounded-rectangle slab: w along X, l along Y, t along Z (before rotation, degrees)."""
    bm = bmesh.new()
    pts = rounded_rect(w, l, r, seg)
    bot = [bm.verts.new((x, y, -t / 2)) for x, y in pts]
    top = [bm.verts.new((x, y, t / 2)) for x, y in pts]
    bm.faces.new(list(reversed(bot)))
    bm.faces.new(top)
    for i in range(len(pts)):
        j = (i + 1) % len(pts)
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    ob = _link(name, bm)
    ob.location = loc
    ob.rotation_euler = tuple(math.radians(a) for a in rot)
    if parent:
        ob.parent = parent
    return _finish(ob, key, bevel, bevel_seg)


def frame(name, key, w, l, t, r, border, loc=(0, 0, 0), rot=(0, 0, 0), bevel=0.003, seg=8, parent=None):
    """Rounded-rectangle ring (a panel with a rounded window cut out)."""
    bm = bmesh.new()
    outer = rounded_rect(w, l, r, seg)
    inner = rounded_rect(w - 2 * border, l - 2 * border, max(0.002, r - border), seg)
    ob_ = [bm.verts.new((x, y, -t / 2)) for x, y in outer]
    ot = [bm.verts.new((x, y, t / 2)) for x, y in outer]
    ib = [bm.verts.new((x, y, -t / 2)) for x, y in inner]
    it = [bm.verts.new((x, y, t / 2)) for x, y in inner]
    n = len(outer)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((ot[i], ot[j], it[j], it[i]))
        bm.faces.new((ob_[j], ob_[i], ib[i], ib[j]))
        bm.faces.new((ob_[i], ob_[j], ot[j], ot[i]))
        bm.faces.new((ib[j], ib[i], it[i], it[j]))
    ob = _link(name, bm)
    ob.location = loc
    ob.rotation_euler = tuple(math.radians(a) for a in rot)
    if parent:
        ob.parent = parent
    return _finish(ob, key, bevel)


def cylinder(name, key, radius, depth, loc=(0, 0, 0), rot=(0, 0, 0), bevel=0.002, verts=24, parent=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=tuple(math.radians(a) for a in rot))
    ob = bpy.context.active_object
    ob.name = name
    if parent:
        ob.parent = parent
    return _finish(ob, key, bevel, 2, 50)


def tube(name, key, a, b, radius, verts=16, parent=None):
    """Cylinder between two points."""
    a, b = Vector(a), Vector(b)
    d = b - a
    ob = cylinder(name, key, radius, d.length, loc=(a + b) / 2, bevel=0, verts=verts, parent=parent)
    ob.rotation_mode = "QUATERNION"
    ob.rotation_quaternion = d.to_track_quat("Z", "Y")
    return ob


def soft_box(name, key, size, loc=(0, 0, 0), rot=(0, 0, 0), levels=2, parent=None):
    """Pillow/cushion: subdivided box."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=tuple(math.radians(a) for a in rot))
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if parent:
        ob.parent = parent
    _finish(ob, key, 0)
    sub = ob.modifiers.new("Subsurf", "SUBSURF")
    sub.levels = levels
    sub.render_levels = levels
    return ob


def empty(name, loc, parent=None):
    e = bpy.data.objects.new(name, None)
    e.location = loc
    if parent:
        e.parent = parent
    bpy.context.scene.collection.objects.link(e)
    return e


def markers(forward=(0, 1, 0), up=(0, 0, 1)):
    empty("Marker_Forward", forward)
    empty("Marker_Up", up)


def unwrap_all(cube_size=1.0):
    """Box-project UVs in object space so 1 UV unit = cube_size metres; Unity tiles textures per metre."""
    for ob in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
        bpy.ops.object.select_all(action="DESELECT")
        bpy.context.view_layer.objects.active = ob
        ob.select_set(True)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.cube_project(cube_size=cube_size, correct_aspect=False, clip_to_bounds=False, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode="OBJECT")


def export(project, rel_dir, name):
    unwrap_all()
    out_dir = os.path.join(project, "Assets", "HSE", "Models", rel_dir)
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=False, object_types={"MESH", "EMPTY"}, use_mesh_modifiers=True,
        mesh_smooth_type="OFF", apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True,
        axis_forward="-Z", axis_up="Y", add_leaf_bones=False)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(project, "Tools", "Blender", name + ".blend"))
    print("[HSE] exported", path)


def preview(path, target=(0, 0, 0.5), cam_loc=(2.2, -2.4, 1.8), lens=45, res=900):
    if not path:
        return
    scene = bpy.context.scene
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.8, 0.82, 0.85, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.7
    scene.world = world
    cd = bpy.data.cameras.new("Cam")
    cd.lens = lens
    cam = bpy.data.objects.new("Cam", cd)
    scene.collection.objects.link(cam)
    cam.location = cam_loc
    cam.rotation_euler = (Vector(target) - Vector(cam_loc)).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    for loc, e in (((3, -2, 4), 400), ((-3, 2, 3), 150)):
        ld = bpy.data.lights.new("L", "AREA")
        ld.energy = e
        ld.size = 2
        lo = bpy.data.objects.new("L", ld)
        lo.location = loc
        lo.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        scene.collection.objects.link(lo)
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x = res
    scene.render.resolution_y = res
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("[HSE] preview", path)

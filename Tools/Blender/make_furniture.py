"""
Room furniture and equipment, one FBX each, in the same frames the Unity room builder uses
(+Y is the item's front in Blender = +Z in Unity; origin on the floor unless noted).
Run: blender --background --factory-startup --python Tools/Blender/make_furniture.py -- <project_root> [preview_dir]
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hse_blender as h  # noqa: E402

project, preview_dir = h.args()


def caster(x, y, z_top, key="Rubber", r=0.025):
    h.cylinder("CasterStem", "Metal", 0.008, 0.02, (x, y, z_top - 0.01), bevel=0.001)
    h.slab("CasterFork", "DarkMetal", 0.03, 0.035, 0.025, 0.008, (x, y, z_top - 0.03), bevel=0.003)
    h.cylinder("CasterWheel", key, r, 0.022, (x, y, r), rot=(0, 90, 0), bevel=0.004)


def star_base(spokes, length, z, key="DarkMetal", wheel_r=0.025):
    for i in range(spokes):
        a = 2 * math.pi * i / spokes
        dx, dy = math.sin(a), math.cos(a)
        s = h.slab("Spoke", key, 0.035, length, 0.03, 0.012, (dx * length / 2, dy * length / 2, z), rot=(0, 0, -math.degrees(a)), bevel=0.006)
        caster(dx * length, dy * length, z - 0.01, r=wheel_r)


def finish(name, rel, preview_target=(0, 0, 0.5), cam=(1.4, -1.6, 1.3), lens=45):
    h.markers()
    h.export(project, rel, name)
    if preview_dir:
        h.preview(os.path.join(preview_dir, name + ".png"), preview_target, cam, lens, 600)


# ---------------------------------------------------------------- nurse stool (front +Y)
h.reset()
star_base(5, 0.27, 0.065)
h.cylinder("Hub", "DarkMetal", 0.04, 0.05, (0, 0, 0.07), bevel=0.006)
h.cylinder("GasLift", "Metal", 0.022, 0.3, (0, 0, 0.25), bevel=0.002)
h.cylinder("GasLiftCover", "DarkMetal", 0.032, 0.14, (0, 0, 0.16), bevel=0.004)
h.slab("Mechanism", "DarkMetal", 0.18, 0.16, 0.04, 0.03, (0, 0, 0.465), bevel=0.006)
h.cylinder("SeatShell", "DarkMetal", 0.225, 0.025, (0, 0, 0.495), verts=48, bevel=0.008)
h.cylinder("SeatCushion", "UpholsteryBlue", 0.22, 0.07, (0, 0, 0.54), verts=48, bevel=0.03)
h.slab("BackPost", "DarkMetal", 0.04, 0.03, 0.3, 0.01, (0, -0.2, 0.66), rot=(-8, 0, 0), bevel=0.006)
h.soft_box("BackPad", "UpholsteryBlue", (0.36, 0.06, 0.17), (0, -0.225, 0.83), rot=(-8, 0, 0))
h.slab("HeightLever", "Black", 0.1, 0.018, 0.012, 0.006, (0.11, 0.04, 0.46), bevel=0.003)
h.bpy.ops.mesh.primitive_torus_add(major_radius=0.19, minor_radius=0.007, major_segments=48, minor_segments=10, location=(0, 0, 0.3))
ring = h.bpy.context.active_object
ring.name = "FootRing"
h._finish(ring, "Metal", 0)
for i in range(3):
    a = math.radians(90 + i * 120)
    h.tube("FootRingStrut", "Metal", (0, 0, 0.3), (math.cos(a) * 0.19, math.sin(a) * 0.19, 0.3), 0.006)
finish("NurseStool", "Furniture", (0, 0, 0.45), (1.1, -1.3, 1.1))

# ---------------------------------------------------------------- visitor armchair (front +Y)
h.reset()
for sx in (-1, 1):
    for sy in (-1, 1):
        h.tube("Leg", "WoodLight", (sx * 0.28, sy * 0.24, 0.0), (sx * 0.29, sy * 0.25, 0.24), 0.02, verts=16)
h.slab("SeatBase", "UpholsteryTeal", 0.68, 0.62, 0.14, 0.06, (0, 0, 0.3), bevel=0.03, bevel_seg=4)
h.slab("SeatCushion", "UpholsteryTealLight", 0.56, 0.54, 0.1, 0.05, (0, 0.03, 0.41), bevel=0.035, bevel_seg=5)
h.slab("Back", "UpholsteryTeal", 0.68, 0.14, 0.6, 0.06, (0, -0.27, 0.66), rot=(-10, 0, 0), bevel=0.035, bevel_seg=4)
h.slab("BackCushion", "UpholsteryTealLight", 0.54, 0.1, 0.42, 0.05, (0, -0.19, 0.68), rot=(-10, 0, 0), bevel=0.035, bevel_seg=5)
for sx in (-1, 1):
    h.slab("Arm", "UpholsteryTeal", 0.09, 0.62, 0.24, 0.035, (sx * 0.335, 0, 0.47), bevel=0.025, bevel_seg=4)
    h.slab("ArmCap", "WoodLight", 0.1, 0.6, 0.025, 0.03, (sx * 0.335, 0.01, 0.6), bevel=0.008)
finish("VisitorArmchair", "Furniture", (0, 0, 0.45), (1.3, 1.5, 1.2))

# ---------------------------------------------------------------- IV pole with infusion pump (front +Y, pump faces -Y)
h.reset()
star_base(5, 0.25, 0.05, key="Metal", wheel_r=0.022)
h.cylinder("Hub", "Metal", 0.035, 0.06, (0, 0, 0.06), bevel=0.004)
h.cylinder("Pole", "Metal", 0.0125, 1.85, (0, 0, 1.0), bevel=0.001, verts=20)
h.cylinder("PoleClamp", "PlasticGray", 0.02, 0.04, (0, 0, 1.4), bevel=0.003)
for a in (0, 90, 180, 270):
    r = math.radians(a)
    tip = (math.cos(r) * 0.15, math.sin(r) * 0.15, 1.95)
    h.tube("HookArm", "Metal", (0, 0, 1.93), tip, 0.005)
    h.tube("HookTip", "Metal", tip, (tip[0], tip[1], tip[2] - 0.03), 0.004)
h.cylinder("PoleCap", "PlasticGray", 0.018, 0.03, (0, 0, 1.94), bevel=0.004)
h.soft_box("IVBag", "IVBag", (0.12, 0.035, 0.2), (0.15, 0, 1.81))
h.slab("IVBagLabel", "Sheet", 0.07, 0.07, 0.002, 0.004, (0.15, -0.019, 1.82), rot=(90, 0, 0), bevel=0)
h.tube("BagPort", "PlasticWhite", (0.15, 0, 1.71), (0.15, 0, 1.69), 0.006)
h.cylinder("DripChamber", "IVBag", 0.012, 0.06, (0.15, 0, 1.655), bevel=0.002)
h.tube("Line1", "IVBag", (0.15, 0, 1.625), (0.07, -0.08, 1.3), 0.0025)
h.tube("Line2", "IVBag", (0.07, -0.08, 1.3), (0.03, -0.1, 1.11), 0.0025)
h.slab("PumpBody", "PlasticWhite", 0.15, 0.11, 0.19, 0.02, (0, -0.06, 1.2), bevel=0.01)
h.slab("PumpBezel", "MonitorBody", 0.11, 0.06, 0.004, 0.008, (0, -0.116, 1.24), rot=(90, 0, 0), bevel=0)
for i in range(6):
    h.slab("PumpKey", "PlasticGray", 0.028, 0.018, 0.005, 0.004, (-0.035 + (i % 3) * 0.035, -0.116, 1.165 + (i // 3) * 0.028), rot=(90, 0, 0), bevel=0)
h.slab("PumpDoor", "PlasticGray", 0.03, 0.09, 0.16, 0.008, (0.075, -0.06, 1.2), bevel=0.004)
h.slab("PumpClamp", "DarkMetal", 0.05, 0.05, 0.04, 0.01, (0, -0.0, 1.2), bevel=0.004)
finish("IVPole", "Equipment", (0, 0, 1.0), (1.6, -2.0, 1.6), 35)

# ---------------------------------------------------------------- vitals monitor housing (origin = body centre, screen faces -Y)
h.reset()
h.slab("Body", "MonitorBody", 0.4, 0.31, 0.07, 0.025, (0, 0, 0), rot=(90, 0, 0), bevel=0.012)
h.frame("Bezel", "Black", 0.375, 0.275, 0.006, 0.014, 0.02, (0, -0.036, 0.008), rot=(90, 0, 0), bevel=0.002)
h.frame("Handle", "MonitorBody", 0.22, 0.07, 0.035, 0.03, 0.018, (0, 0, 0.175), rot=(90, 0, 0), bevel=0.006)
h.cylinder("Knob", "PlasticGray", 0.016, 0.012, (0.17, -0.04, -0.12), rot=(90, 0, 0), bevel=0.003)
h.slab("LightBar", "EmissiveGreen", 0.05, 0.006, 0.004, 0.002, (-0.15, -0.037, 0.148), rot=(90, 0, 0), bevel=0)
for i in range(6):
    h.slab("Vent", "Black", 0.2, 0.006, 0.004, 0.002, (0, 0.036, -0.08 + i * 0.03), rot=(90, 0, 0), bevel=0)
h.slab("Mount", "DarkMetal", 0.12, 0.08, 0.04, 0.01, (0, 0.05, 0), rot=(90, 0, 0), bevel=0.004)
finish("VitalsMonitor", "Equipment", (0, 0, 0), (0.5, -0.9, 0.3), 50)

# ---------------------------------------------------------------- procedure trolley (front +Y towards the bed)
h.reset()
for sx in (-1, 1):
    for sy in (-1, 1):
        h.cylinder("Post", "Metal", 0.0125, 0.86, (sx * 0.34, sy * 0.21, 0.5), bevel=0.001, verts=20)
        caster(sx * 0.34, sy * 0.21, 0.07, r=0.035)
for name, z, key in (("TopShelf", 0.92, "PlasticWhite"), ("MidShelf", 0.5, "PlasticGray"), ("LowShelf", 0.15, "PlasticGray")):
    h.slab(name, key, 0.74, 0.48, 0.03, 0.04, (0, 0, z), bevel=0.008)
    h.frame(name + "Lip", key, 0.74, 0.48, 0.03, 0.04, 0.012, (0, 0, z + 0.025), bevel=0.004)
h.slab("DrawerUnit", "PlasticWhite", 0.4, 0.42, 0.36, 0.02, (0.14, 0, 0.72), bevel=0.008)
for i, key in enumerate(("Accent", "Yellow", "GreenO2")):
    z = 0.84 - i * 0.115
    h.slab("DrawerFront", key, 0.37, 0.1, 0.012, 0.012, (0.14, 0.212, z), rot=(90, 0, 0), bevel=0.004)
    h.tube("DrawerPull", "Metal", (0.08, 0.225, z + 0.02), (0.2, 0.225, z + 0.02), 0.005)
h.tube("PushHandle", "Metal", (-0.3, -0.29, 1.0), (0.3, -0.29, 1.0), 0.013)
for sx in (-1, 1):
    h.tube("HandleBracket", "Metal", (sx * 0.3, -0.29, 1.0), (sx * 0.3, -0.24, 0.94), 0.01)
h.slab("BinHolder", "PlasticGray", 0.2, 0.012, 0.18, 0.01, (-0.2, -0.25, 0.75), rot=(0, 0, 0), bevel=0.003)
finish("ProcedureTrolley", "Equipment", (0, 0, 0.55), (1.4, 1.6, 1.4))

# ---------------------------------------------------------------- bedside cabinet (front faces -X towards the bed)
h.reset()
h.slab("Body", "WoodLight", 0.45, 0.45, 0.72, 0.02, (0, 0, 0.38), bevel=0.008)
h.slab("Plinth", "DarkMetal", 0.41, 0.41, 0.03, 0.02, (0, 0, 0.015), bevel=0.003)
h.slab("Top", "PlasticWhite", 0.48, 0.48, 0.025, 0.03, (0, 0, 0.752), bevel=0.008)
h.slab("Drawer", "PlasticWhite", 0.01, 0.41, 0.17, 0.012, (-0.228, 0, 0.6), bevel=0.003)
h.slab("Door", "PlasticWhite", 0.01, 0.41, 0.4, 0.012, (-0.228, 0, 0.27), bevel=0.003)
h.tube("DrawerPull", "Metal", (-0.24, -0.08, 0.62), (-0.24, 0.08, 0.62), 0.006)
h.tube("DoorPull", "Metal", (-0.24, 0.15, 0.32), (-0.24, 0.15, 0.44), 0.006)
for sx in (-1, 1):
    for sy in (-1, 1):
        h.cylinder("Glide", "Rubber", 0.012, 0.01, (sx * 0.19, sy * 0.19, 0.005), bevel=0.002)
finish("BedsideCabinet", "Furniture", (0, 0, 0.4), (-1.2, -1.2, 1.1))

# ---------------------------------------------------------------- overbed table (top cantilevers towards -X over the bed)
h.reset()
h.slab("Base", "DarkMetal", 0.12, 0.62, 0.045, 0.02, (0, 0, 0.05), bevel=0.008)
for sy in (-1, 1):
    caster(0, sy * 0.27, 0.03, r=0.025)
h.slab("Column", "PlasticGray", 0.06, 0.06, 0.82, 0.015, (0, 0, 0.48), bevel=0.004)
h.slab("ColumnSleeve", "PlasticWhite", 0.075, 0.075, 0.3, 0.018, (0, 0, 0.3), bevel=0.004)
h.slab("Arm", "PlasticGray", 0.4, 0.06, 0.04, 0.015, (-0.17, 0, 0.885), bevel=0.004)
h.slab("Top", "WoodLight", 0.86, 0.42, 0.028, 0.05, (-0.36, 0, 0.92), bevel=0.008)
h.frame("TopEdge", "PlasticWhite", 0.87, 0.43, 0.03, 0.055, 0.012, (-0.36, 0, 0.92), bevel=0.005)
finish("OverbedTable", "Furniture", (-0.3, 0, 0.6), (1.2, -1.4, 1.4))

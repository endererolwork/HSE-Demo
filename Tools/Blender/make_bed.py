"""
Electric hospital bed with mattress, bedding and split side rails.
Run: blender --background --factory-startup --python Tools/Blender/make_bed.py -- <project_root> [preview.png]

Frame: X width, +Y towards the headboard, Z up. Dimensions match the patient figure the Unity room builder adds:
foot mattress top at 0.58 m, head section hinged at y=0.30, z=0.50 and raised 20 degrees.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hse_blender as h  # noqa: E402

project, preview = h.args()
h.reset()

# ---------------------------------------------------------------- chassis and wheels
h.slab("Chassis", "PlasticWhite", 0.62, 1.62, 0.08, 0.06, (0, 0, 0.13), bevel=0.02)
h.slab("ChassisUnder", "DarkMetal", 0.58, 1.56, 0.03, 0.05, (0, 0, 0.085), bevel=0.005)
for sx in (-1, 1):
    for sy in (-1, 1):
        x, y = sx * 0.29, sy * 0.73
        h.cylinder("CasterStem", "Metal", 0.016, 0.05, (x, y, 0.105), bevel=0.002)
        h.slab("CasterFork", "DarkMetal", 0.05, 0.06, 0.035, 0.012, (x, y, 0.075), bevel=0.004)
        h.cylinder("Wheel", "Rubber", 0.05, 0.034, (x, y, 0.05), rot=(0, 90, 0), bevel=0.006)
        h.cylinder("Hub", "PlasticGray", 0.025, 0.038, (x, y, 0.05), rot=(0, 90, 0), bevel=0.002)
for sy in (-1, 1):
    h.slab("BrakePedal", "Red", 0.1, 0.05, 0.016, 0.01, (0.18, sy * 0.83, 0.07), bevel=0.004)
    h.slab("SteerPedal", "GreenO2", 0.1, 0.05, 0.016, 0.01, (-0.18, sy * 0.83, 0.07), bevel=0.004)

for sy in (-1, 1):
    h.slab("LiftColumn", "PlasticWhite", 0.18, 0.14, 0.22, 0.04, (0, sy * 0.5, 0.28), bevel=0.01)
    for i in range(4):
        h.slab("Bellows", "PlasticGray", 0.19, 0.15, 0.012, 0.045, (0, sy * 0.5, 0.2 + i * 0.05), bevel=0.004)

# ---------------------------------------------------------------- deck
h.frame("DeckFrame", "PlasticGray", 0.96, 2.04, 0.05, 0.07, 0.045, (0, 0, 0.4), bevel=0.012)
h.slab("DeckPanel", "DarkMetal", 0.9, 1.98, 0.02, 0.05, (0, 0, 0.405), bevel=0.003)
for sx in (-1, 1):
    for sy in (-1, 1):
        h.cylinder("Bumper", "Rubber", 0.035, 0.08, (sx * 0.47, sy * 1.0, 0.4), bevel=0.01)
        h.cylinder("IVSocket", "Metal", 0.012, 0.06, (sx * 0.44, sy * 1.03, 0.45), bevel=0.002)

# ---------------------------------------------------------------- mattress and bedding
h.slab("Mattress_Foot", "Mattress", 0.9, 1.3, 0.16, 0.06, (0, -0.35, 0.5), bevel=0.035, bevel_seg=4)
h.slab("Sheet_Foot", "Sheet", 0.925, 1.33, 0.012, 0.06, (0, -0.35, 0.585), bevel=0.005)
for sx in (-1, 1):
    h.slab("SheetDrape", "Sheet", 0.012, 1.3, 0.13, 0.004, (sx * 0.463, -0.35, 0.525), bevel=0.003)

pivot = h.empty("HeadSectionPivot", (0, 0.3, 0.5))
pivot.rotation_euler = (h.math.radians(20), 0, 0)
h.slab("Mattress_Head", "Mattress", 0.9, 0.75, 0.16, 0.06, (0, 0.375, 0), bevel=0.035, bevel_seg=4, parent=pivot)
h.slab("Sheet_Head", "Sheet", 0.925, 0.77, 0.012, 0.06, (0, 0.375, 0.085), bevel=0.005, parent=pivot)
h.soft_box("Pillow", "Sheet", (0.62, 0.34, 0.12), (0, 0.58, 0.145), parent=pivot)

h.slab("Blanket", "Blanket", 0.96, 0.9, 0.05, 0.03, (0, -0.6, 0.615), bevel=0.022, bevel_seg=4)
for sx in (-1, 1):
    h.slab("BlanketDrape", "Blanket", 0.014, 0.9, 0.26, 0.006, (sx * 0.478, -0.6, 0.5), bevel=0.002)
    h.soft_box("LegBump", "Blanket", (0.17, 0.85, 0.08), (sx * 0.11, -0.58, 0.64))
    h.soft_box("FootBump", "Blanket", (0.14, 0.16, 0.12), (sx * 0.12, -0.92, 0.68))
h.soft_box("BlanketFold", "Sheet", (0.97, 0.17, 0.045), (0, -0.155, 0.645))
h.slab("BlanketFootDrape", "Blanket", 0.96, 0.014, 0.2, 0.006, (0, -1.048, 0.53), bevel=0.002)

# ---------------------------------------------------------------- head and foot boards
h.slab("Headboard", "PlasticWhite", 1.0, 0.58, 0.05, 0.1, (0, 1.075, 0.75), rot=(90, 0, 0), bevel=0.014)
h.slab("HeadboardInset", "WoodLight", 0.78, 0.36, 0.012, 0.06, (0, 1.047, 0.72), rot=(90, 0, 0), bevel=0.004)
h.slab("HeadboardGrip", "PlasticGray", 0.32, 0.04, 0.056, 0.018, (0, 1.075, 0.975), bevel=0.004)
h.slab("Footboard", "PlasticWhite", 1.0, 0.44, 0.05, 0.09, (0, -1.075, 0.65), rot=(90, 0, 0), bevel=0.014)
h.slab("FootboardInset", "WoodLight", 0.78, 0.24, 0.012, 0.06, (0, -1.047, 0.63), rot=(90, 0, 0), bevel=0.004)
h.slab("ControlPanel", "MonitorBody", 0.34, 0.13, 0.012, 0.02, (0, -1.103, 0.72), rot=(90, 0, 0), bevel=0.003)
h.slab("ControlScreen", "ScreenBlue", 0.14, 0.07, 0.004, 0.006, (-0.07, -1.11, 0.725), rot=(90, 0, 0), bevel=0)
for i in range(4):
    h.slab("ControlKey", "PlasticGray", 0.026, 0.026, 0.005, 0.006, (0.04 + (i % 2) * 0.035, -1.11, 0.705 + (i // 2) * 0.035), rot=(90, 0, 0), bevel=0)
h.slab("NameCardHolder", "Sheet", 0.14, 0.08, 0.006, 0.01, (0.3, -1.103, 0.74), rot=(90, 0, 0), bevel=0.002)
for sx in (-1, 1):
    h.tube("HeadPost", "Metal", (sx * 0.4, 1.06, 0.42), (sx * 0.4, 1.06, 0.5), 0.014)
    h.tube("FootPost", "Metal", (sx * 0.4, -1.06, 0.42), (sx * 0.4, -1.06, 0.45), 0.014)

# ---------------------------------------------------------------- side rails (A = +X, B = -X)
for side, sx in (("A", 1), ("B", -1)):
    h.frame("SideRail_Head_" + side, "PlasticWhite", 0.76, 0.27, 0.03, 0.07, 0.04, (sx * 0.495, 0.55, 0.78), rot=(90, 0, 90), bevel=0.008)
    h.slab("SideRail_Head_" + side + "_Arm", "DarkMetal", 0.025, 0.12, 0.2, 0.01, (sx * 0.475, 0.55, 0.6), bevel=0.003)
    h.frame("SideRail_Foot_" + side, "PlasticWhite", 0.62, 0.23, 0.03, 0.06, 0.035, (sx * 0.495, -0.52, 0.72), rot=(90, 0, 90), bevel=0.008)
    h.slab("SideRail_Foot_" + side + "_Arm", "DarkMetal", 0.025, 0.1, 0.17, 0.01, (sx * 0.475, -0.52, 0.56), bevel=0.003)
    h.slab("RailKeypad_" + side, "MonitorBody", 0.006, 0.12, 0.07, 0.01, (sx * 0.513, 0.62, 0.8), bevel=0.002)

h.slab("Pendant", "MonitorBody", 0.06, 0.03, 0.14, 0.012, (-0.53, -0.4, 0.6), bevel=0.006)
h.tube("PendantCord", "Black", (-0.53, -0.4, 0.67), (-0.5, -0.45, 0.76), 0.003)

h.markers(forward=(0, 1, 0), up=(0, 0, 1))
h.export(project, "Bed", "HospitalBed")
h.preview(preview, target=(0, 0, 0.55), cam_loc=(2.1, -2.3, 1.7), lens=40)

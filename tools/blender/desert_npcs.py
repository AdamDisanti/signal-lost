# The desert revamp's new NPCs (docs/desert-revamp.md): Doris (an alien ostrich at Mirage Springs), Marshal Mo (a meerkat
# race marshal at the Dustbowl Speedway) and Old Gus (a deaf tortoise prospector in Rattlesnake Canyon).
# Run inside Blender (Blender MCP: exec(open(path).read()), or blender --background --python tools/blender/desert_npcs.py).
# Built like Cavendish: low poly, flat shaded, and split into parts that move (Body, Head, ArmL/ArmR or WingL/WingR), each
# object's origin at its pivot, so Unity can animate them by code. Z is up and every NPC faces -Y (Blender's front view).
# One FBX per NPC into Assets/Prefabs/NPC/Desert/Models, and the .blend into art/desert-npcs.
import bpy, math, os, sys, importlib
from mathutils import Vector, Matrix

REPO = os.environ.get("SIGNAL_LOST_REPO", r"C:\Users\Lawson\signal-lost")
sys.path.insert(0, os.path.join(REPO, "tools", "blender"))
import lowkit
importlib.reload(lowkit)
from lowkit import Kit, material, srgb

MODELS = os.path.join(REPO, "signal-lost", "Assets", "Prefabs", "NPC", "Desert", "Models")
ART = os.path.join(REPO, "art", "desert-npcs")

scene = bpy.data.scenes.get("DesertNpcs") or bpy.data.scenes.new("DesertNpcs")
for o in list(scene.objects):
    bpy.data.objects.remove(o, do_unlink=True)
if bpy.context.window is not None:
    bpy.context.window.scene = scene

M = {
    # Doris
    "DorisFeather": material("DorisFeather", srgb(176, 140, 204)),
    "DorisFeatherDark": material("DorisFeatherDark", srgb(122, 92, 156)),
    "DorisPlume": material("DorisPlume", srgb(246, 240, 236)),
    "DorisLeg": material("DorisLeg", srgb(232, 150, 120)),
    "DorisBeak": material("DorisBeak", srgb(246, 168, 62)),
    "DorisTowel": material("DorisTowel", srgb(246, 246, 240)),
    "DorisStripe": material("DorisStripe", srgb(62, 172, 162)),
    "DorisLens": material("DorisLens", srgb(30, 30, 44), rough=0.15),
    "DorisFrame": material("DorisFrame", srgb(240, 92, 152)),
    # Mo
    "MoFur": material("MoFur", srgb(206, 160, 102)),
    "MoFurLight": material("MoFurLight", srgb(236, 206, 152)),
    "MoFurDark": material("MoFurDark", srgb(120, 86, 56)),
    "MoPatch": material("MoPatch", srgb(70, 50, 40)),
    "MoNose": material("MoNose", srgb(40, 30, 30)),
    "MoVest": material("MoVest", srgb(255, 122, 22)),
    "MoStripe": material("MoStripe", srgb(232, 232, 222), emit=srgb(120, 120, 110)),
    "MoCapBrim": material("MoCapBrim", srgb(40, 40, 48)),
    "MoHorn": material("MoHorn", srgb(242, 202, 42), rough=0.4),
    "MoPole": material("MoPole", srgb(92, 92, 98), metal=0.5, rough=0.4),
    "MoFlagWhite": material("MoFlagWhite", srgb(240, 240, 236)),
    "MoFlagBlack": material("MoFlagBlack", srgb(26, 26, 30)),
    # Gus
    "GusSkin": material("GusSkin", srgb(150, 160, 108)),
    "GusSkinDark": material("GusSkinDark", srgb(108, 118, 78)),
    "GusShell": material("GusShell", srgb(120, 90, 56)),
    "GusPlate": material("GusPlate", srgb(166, 126, 76)),
    "GusRim": material("GusRim", srgb(88, 64, 40)),
    "GusPlastron": material("GusPlastron", srgb(212, 192, 132)),
    "GusBeard": material("GusBeard", srgb(242, 242, 236)),
    "GusHat": material("GusHat", srgb(152, 106, 62)),
    "GusHatBand": material("GusHatBand", srgb(90, 60, 36)),
    "GusBrass": material("GusBrass", srgb(204, 162, 62), metal=0.6, rough=0.35),
    "GusRod": material("GusRod", srgb(132, 96, 60)),
    # shared
    "Eye": material("NpcEye", srgb(16, 14, 14), rough=0.15),
    "Shine": material("NpcShine", srgb(255, 255, 255), emit=srgb(255, 255, 255)),
}

parts = {}

def part(npc, name, pivot):
    k = Kit(M)
    parts.setdefault(npc, []).append((name, k, pivot))
    return k

def eyes(k, centre_l, radius, mirror=True):
    for sx in ((1, -1) if mirror else (1,)):
        c = Vector((centre_l[0] * sx, centre_l[1], centre_l[2]))
        k.ball(c, radius, "Eye", subdiv=1)
        k.ball(c + Vector((0.3 * radius * sx, -0.75 * radius, 0.35 * radius)), radius * 0.32, "Shine", subdiv=0)

# ================================================================ Doris
def doris():
    body = part("Doris", "DorisBody", (0, 0, 0))
    for sx in (1, -1):
        body.tube([(0.18 * sx, 0.06, 1.2), (0.2 * sx, 0.2, 0.64), (0.2 * sx, 0.02, 0.12)], [0.1, 0.07, 0.05], 6, "DorisLeg")
        for toe in (-0.12, 0.1):
            body.tube([(0.2 * sx, 0.02, 0.06), (0.2 * sx + toe, -0.24, 0.03)], [0.045, 0.02], 5, "DorisLeg")
    body.ball((0, 0.06, 1.46), 0.56, "DorisFeather", subdiv=2, squash=(0.82, 1.18, 0.74))
    body.ball((0, 0.0, 1.3), 0.4, "DorisFeatherDark", subdiv=1, squash=(0.8, 1.1, 0.55))
    for i in range(6):
        a = -0.6 + i * 0.24
        body.blade([(0.0 + a * 0.3, 0.6, 1.55), (a * 0.45, 0.85, 1.85), (a * 0.7, 0.95, 2.15)], [0.05, 0.12, 0.04], 0.015, "DorisPlume", normal_hint=(0, -1, 0.3))
    for sx, nm in ((1, "DorisWingL"), (-1, "DorisWingR")):
        w = part("Doris", nm, (0.42 * sx, -0.05, 1.58))
        w.blade([(0.42 * sx, -0.05, 1.58), (0.55 * sx, 0.2, 1.48), (0.56 * sx, 0.5, 1.3)], [0.14, 0.18, 0.06], 0.03, "DorisFeatherDark", normal_hint=(sx, 0, 0))
        for f in range(3):
            w.blade([(0.5 * sx, 0.25 + f * 0.1, 1.42 - f * 0.04), (0.6 * sx, 0.45 + f * 0.1, 1.2 - f * 0.04)], [0.06, 0.02], 0.02, "DorisPlume", normal_hint=(sx, 0, 0))
    head = part("Doris", "DorisHead", (0, -0.36, 1.72))
    head.tube([(0, -0.36, 1.66), (0, -0.46, 2.0), (0, -0.5, 2.36)], [0.12, 0.1, 0.085], 7, "DorisFeather")
    head.ball((0, -0.55, 2.46), 0.17, "DorisFeather", subdiv=2, squash=(0.9, 1.1, 0.95))
    head.lathe((0, -0.66, 2.42), [(0.07, 0.0), (0.06, 0.08), (0.02, 0.2), (0.001, 0.24)], 6, lambda i: "DorisBeak", axis=(0, -1, -0.15))
    eyes(head, (0.11, -0.64, 2.5), 0.045)
    for sx in (1, -1):
        head.lathe((0.095 * sx, -0.69, 2.52), [(0.0, 0.0), (0.075, 0.0), (0.075, 0.02), (0.0, 0.02)], 8, lambda i: "DorisLens", axis=(0, -1, 0))
        head.tube([(0.095 * sx + 0.07 * sx, -0.69, 2.53), (0.16 * sx, -0.55, 2.55)], [0.012, 0.012], 4, "DorisFrame")
    head.box((0, -0.7, 2.53), (0.05, 0.015, 0.015), "DorisFrame")
    head.ball((0, -0.52, 2.6), 0.21, "DorisTowel", subdiv=1, squash=(1.0, 1.05, 0.68))
    head.lathe((0, -0.52, 2.56), [(0.215, 0.0), (0.22, 0.04), (0.215, 0.08)], 10, lambda i: "DorisStripe")
    head.lathe((0, -0.5, 2.72), [(0.08, 0.0), (0.06, 0.08), (0.02, 0.14)], 6, lambda i: "DorisTowel", axis=(0, 0.3, 1))

# ================================================================ Marshal Mo
def mo():
    body = part("Mo", "MoBody", (0, 0, 0))
    for sx in (1, -1):
        body.ball((0.11 * sx, -0.08, 0.05), 0.07, "MoFurDark", subdiv=1, squash=(0.9, 1.4, 0.6))
        body.tube([(0.11 * sx, 0.0, 0.08), (0.12 * sx, 0.0, 0.25)], [0.06, 0.07], 6, "MoFur")
    body.lathe((0, 0, 0.16), [(0.12, 0.0), (0.21, 0.14), (0.22, 0.32), (0.19, 0.5), (0.15, 0.64), (0.11, 0.72)], 9, lambda i: "MoFur")
    body.ball((0, -0.13, 0.4), 0.15, "MoFurLight", subdiv=1, squash=(0.95, 0.45, 1.5))
    body.lathe((0, 0, 0.44), [(0.215, 0.0), (0.205, 0.12), (0.175, 0.26), (0.16, 0.3)], 9, lambda i: "MoVest")
    body.lathe((0, 0, 0.58), [(0.19, 0.0), (0.19, 0.035)], 9, lambda i: "MoStripe")
    body.tube([(0, 0.17, 0.22), (0, 0.38, 0.12), (0, 0.56, 0.04), (0, 0.66, 0.03)], [0.06, 0.045, 0.03, 0.015], 6, "MoFur")
    body.ball((0, 0.64, 0.03), 0.03, "MoFurDark", subdiv=0)
    head = part("Mo", "MoHead", (0, -0.02, 0.86))
    head.tube([(0, 0.0, 0.84), (0, -0.03, 0.95)], [0.08, 0.08], 7, "MoFur")
    head.ball((0, -0.04, 1.02), 0.14, "MoFur", subdiv=2, squash=(1.0, 1.1, 1.0))
    head.lathe((0, -0.14, 1.0), [(0.075, 0.0), (0.055, 0.08), (0.025, 0.15)], 7, lambda i: "MoFurLight", axis=(0, -1, -0.1))
    head.ball((0, -0.29, 0.985), 0.03, "MoNose", subdiv=1)
    for sx in (1, -1):
        head.ball((0.065 * sx, -0.13, 1.06), 0.05, "MoPatch", subdiv=1, squash=(1.0, 0.5, 1.2))
        head.ball((0.12 * sx, 0.0, 1.08), 0.04, "MoFurDark", subdiv=0, squash=(0.6, 1.0, 1.0))
    eyes(head, (0.065, -0.16, 1.065), 0.022)
    head.lathe((0, -0.02, 1.1), [(0.13, 0.0), (0.12, 0.05), (0.08, 0.09), (0.001, 0.1)], 9, lambda i: "MoVest")
    head.box((0, -0.15, 1.105), (0.2, 0.12, 0.015), "MoCapBrim")
    arm_l = part("Mo", "MoArmL", (0.15, -0.04, 0.74))
    arm_l.tube([(0.15, -0.04, 0.74), (0.19, -0.12, 0.62), (0.2, -0.18, 0.55)], [0.04, 0.035, 0.03], 6, "MoFur")
    arm_l.ball((0.2, -0.19, 0.54), 0.035, "MoFurDark", subdiv=0)
    arm_l.tube([(0.2, -0.2, 0.42), (0.2, -0.2, 1.36)], [0.012, 0.012], 5, "MoPole")
    for col in range(3):
        for row in range(2):
            key = "MoFlagBlack" if (col + row) % 2 == 0 else "MoFlagWhite"
            arm_l.box((0.2 + 0.065 + col * 0.12, -0.2, 1.3 - row * 0.11), (0.12, 0.01, 0.11), key)
    arm_r = part("Mo", "MoArmR", (-0.15, -0.04, 0.74))
    arm_r.tube([(-0.15, -0.04, 0.74), (-0.19, -0.14, 0.66), (-0.2, -0.2, 0.62)], [0.04, 0.035, 0.03], 6, "MoFur")
    arm_r.lathe((-0.2, -0.22, 0.64), [(0.04, 0.0), (0.05, 0.06), (0.09, 0.2), (0.11, 0.26), (0.1, 0.27)], 9, lambda i: "MoHorn", axis=(0, -1, 0.15))
    arm_r.tube([(-0.2, -0.17, 0.62), (-0.2, -0.17, 0.55)], [0.02, 0.02], 5, "MoPole")

# ================================================================ Old Gus
def gus():
    body = part("Gus", "GusBody", (0, 0, 0))
    for sx in (1, -1):
        body.tube([(0.2 * sx, 0.02, 0.06), (0.21 * sx, 0.02, 0.38)], [0.12, 0.11], 7, "GusSkin")
        body.ball((0.2 * sx, -0.05, 0.05), 0.12, "GusSkinDark", subdiv=1, squash=(1.0, 1.3, 0.5))
    body.ball((0, -0.06, 0.74), 0.38, "GusPlastron", subdiv=2, squash=(0.95, 0.6, 1.15))
    body.lathe((0, 0.02, 0.76), [(0.5, 0.0), (0.52, 0.06), (0.47, 0.18), (0.36, 0.3), (0.2, 0.4), (0.001, 0.44)], 9,
               lambda i: ("GusRim", "GusPlate", "GusShell", "GusPlate", "GusShell")[i], axis=(0, 1, 0.08), phase=0.17)
    body.tube([(0, 0.4, 0.32), (0, 0.5, 0.22)], [0.05, 0.01], 5, "GusSkinDark")
    head = part("Gus", "GusHead", (0, -0.16, 1.1))
    head.tube([(0, -0.12, 1.04), (0, -0.24, 1.16), (0, -0.3, 1.22)], [0.1, 0.095, 0.09], 7, "GusSkin")
    head.ball((0, -0.37, 1.28), 0.15, "GusSkin", subdiv=2, squash=(0.95, 1.2, 0.95))
    eyes(head, (0.075, -0.48, 1.32), 0.025)
    for sx in (1, -1):
        head.blade([(0.03 * sx, -0.5, 1.37), (0.11 * sx, -0.5, 1.39), (0.17 * sx, -0.46, 1.38)], [0.02, 0.03, 0.01], 0.012, "GusBeard", normal_hint=(0, -1, 0))
    for b in range(5):
        x = -0.08 + b * 0.04
        head.blade([(x, -0.48, 1.22), (x * 1.2, -0.5, 1.08), (x * 0.8, -0.47, 0.9)], [0.03, 0.04, 0.008], 0.012, "GusBeard", normal_hint=(0, -1, 0))
    head.lathe((0, -0.36, 1.39), [(0.001, 0.0), (0.32, 0.0), (0.33, 0.03), (0.16, 0.035), (0.16, 0.06), (0.15, 0.14), (0.12, 0.2), (0.001, 0.2)], 10,
               lambda i: "GusHatBand" if i == 3 else "GusHat")
    arm_l = part("Gus", "GusArmL", (0.33, -0.06, 0.96))
    arm_l.tube([(0.33, -0.06, 0.96), (0.38, -0.2, 0.84), (0.38, -0.3, 0.74)], [0.07, 0.06, 0.055], 6, "GusSkin")
    arm_l.ball((0.38, -0.32, 0.72), 0.06, "GusSkinDark", subdiv=0)
    arm_l.tube([(0.38, -0.32, 0.72), (0.38, -0.62, 0.66), (0.38, -0.82, 0.58)], [0.018, 0.016, 0.014], 5, "GusRod")
    for sx in (1, -1):
        arm_l.tube([(0.38, -0.32, 0.72), (0.38 + 0.12 * sx, -0.22, 0.74)], [0.016, 0.014], 5, "GusRod")
    arm_r = part("Gus", "GusArmR", (-0.33, -0.06, 0.96))
    arm_r.tube([(-0.33, -0.06, 0.96), (-0.36, -0.2, 1.06), (-0.27, -0.3, 1.18)], [0.07, 0.06, 0.055], 6, "GusSkin")
    arm_r.ball((-0.26, -0.31, 1.19), 0.06, "GusSkinDark", subdiv=0)
    arm_r.lathe((-0.15, -0.36, 1.3), [(0.02, 0.0), (0.03, 0.1), (0.06, 0.24), (0.12, 0.33), (0.13, 0.35)], 9, lambda i: "GusBrass", axis=(-1, -0.1, 0.25))

doris()
mo()
gus()

os.makedirs(MODELS, exist_ok=True)
os.makedirs(ART, exist_ok=True)
summary = {}
offset = {"Doris": -1.6, "Mo": 0.0, "Gus": 1.4}
for npc, items in parts.items():
    objs = [k.finish(name, scene, pivot) for name, k, pivot in items]
    for other in scene.objects:
        other.select_set(other in objs)
    with bpy.context.temp_override(selected_objects=objs, active_object=objs[0]):
        bpy.ops.export_scene.fbx(filepath=os.path.join(MODELS, "Desert" + npc + ".fbx"), use_selection=True, object_types={'MESH'},
                                 apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                                 mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
    for o in objs:
        o.location.x += offset[npc]
    summary[npc] = {"parts": [o.name for o in objs], "tris": sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs),
                    "height": round(max((o.matrix_world @ Vector(c)).z for o in objs for c in o.bound_box), 2)}
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ART, "DesertNpcs.blend"), copy=True)
print("DESERT_NPCS", summary)

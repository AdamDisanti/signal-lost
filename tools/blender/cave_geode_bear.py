# Grumble, the Geode Bear: a Crystal Caves mini-boss (docs/geode-bear.md). A big, round, fluffy cave bear in cool
# grey-brown fur with a paler muzzle and belly and small sleepy eyes. Years of sleep grew a crest of cyan and purple
# geode crystals on its hump, and the ship's power cell is strapped to its lower back with scrap straps (the weak
# spot). About 4.3 m long and 2.5 m tall on all fours.
# Built with the same kit as the other bosses (bosskit.py); run tools/blender/cave_bosses.py -- bear.
import math
from mathutils import Vector
from bosskit import Boss, srgb, sym, mirror, X, Y, Z

# Body sections along its length: (y, centre height, width, height). It faces -Y.
BODY = [(1.8, 1.45, 1.0, 0.95), (1.45, 1.5, 1.6, 1.4), (0.9, 1.55, 1.85, 1.55), (0.2, 1.62, 1.95, 1.62),
        (-0.4, 1.65, 1.95, 1.7), (-0.95, 1.55, 1.7, 1.45), (-1.3, 1.5, 1.25, 1.1)]


def top(y):
    """Height of the top of the back at y."""
    for a, b in zip(BODY, BODY[1:]):
        if b[0] <= y <= a[0]:
            t = (a[0] - y) / (a[0] - b[0])
            return (a[1] + a[3] / 2) * (1 - t) + (b[1] + b[3] / 2) * t
    return BODY[-1][1]


def build():
    b = Boss("GeodeBear")
    b.material("Fur", srgb(122, 104, 94), rough=0.95)
    b.material("FurDark", srgb(90, 76, 72), rough=0.95)
    b.material("Belly", srgb(172, 156, 142), rough=0.95)
    b.material("Muzzle", srgb(190, 172, 152), rough=0.9)
    b.material("Nose", srgb(34, 30, 34), rough=0.4)
    b.material("Eye", srgb(18, 16, 22), rough=0.3)
    b.material("EarInner", srgb(70, 58, 60), rough=0.9)
    b.material("Mouth", srgb(96, 44, 52), rough=0.6)
    b.material("Nail", srgb(222, 206, 160), rough=0.5)
    b.material("Geode", srgb(128, 122, 134), rough=0.9)
    # The crest: the Unity builder swaps these for the cave's own crystal materials (cyan and purple).
    b.material("CrystalCyan", srgb(60, 200, 230), rough=0.2, emit=srgb(51, 230, 255), strength=2.5)
    b.material("CrystalPurple", srgb(160, 90, 230), rough=0.2, emit=srgb(170, 100, 255), strength=2.5)
    b.material("Metal", srgb(150, 150, 156), rough=0.4, metal=0.75)
    b.material("MetalDark", srgb(70, 72, 78), rough=0.55, metal=0.7)
    b.material("Strap", srgb(78, 64, 52), rough=0.8)
    b.material("CellGlow", srgb(120, 255, 150), emit=srgb(110, 255, 140), strength=3.0)
    b.material("Signal", srgb(255, 90, 60), emit=srgb(255, 80, 50), strength=4.0)

    # ------------------------------------------------------------ skeleton (faces -Y, +X is its left)
    b.bone("Root", (0, 0, 0))
    b.bone("Pivot", (0, 0.2, 1.4), "Root")
    b.bone("Hips", (0, 1.1, 1.45), "Pivot")
    b.bone("Spine", (0, 0.2, 1.6), "Hips")
    b.bone("Chest", (0, -0.7, 1.6), "Spine")
    b.bone("Neck", (0, -1.3, 1.55), "Chest")
    b.bone("Head", (0, -1.65, 1.6), "Neck")
    b.bone("Jaw", (0, -2.0, 1.3), "Head")
    b.bone("Lids", (0, -1.97, 1.82), "Head")      # not animated: the script lowers them in phase 3
    b.bone("Crest", (0, -0.35, 2.45), "Spine")    # socket: the geode back's hitbox
    b.bone("Cell", (0, 1.0, 2.42), "Hips")        # socket: the power cell (weak spot, ship part)
    for side, s in (("L", 1), ("R", -1)):
        b.bone("UpperArm" + side, (0.6 * s, -0.85, 1.3), "Chest")
        b.bone("Forearm" + side, (0.62 * s, -0.9, 0.65), "UpperArm" + side)
        b.bone("Hand" + side, (0.62 * s, -0.95, 0.15), "Forearm" + side)
        b.bone("Thigh" + side, (0.55 * s, 1.2, 1.2), "Hips")
        b.bone("Shin" + side, (0.55 * s, 1.3, 0.6), "Thigh" + side)
        b.bone("Foot" + side, (0.55 * s, 1.25, 0.15), "Shin" + side)

    body_chain = ["Hips", "Spine", "Chest"]

    # ------------------------------------------------------------ body: round, a big shoulder hump, pale belly
    def body_mat(c, k, i):
        if k < 0 or k >= len(BODY) - 1:
            return "Fur"
        zc = (BODY[k][1] + BODY[k + 1][1]) / 2
        hh = (BODY[k][3] + BODY[k + 1][3]) / 4
        if c.z < zc - hh * 0.45:
            return "Belly"
        return "FurDark" if c.z > zc + hh * 0.75 else "Fur"
    b.loft("Body", [((0, y, z), w, h) for y, z, w, h in BODY], X, Z, 12, 2.2, body_mat, jitter=0.04, chain=body_chain)
    b.blob("Tail", (0, 1.85, 1.62), (0.16, 0.14, 0.15), "FurDark", bone="Hips")
    # Fluff: soft tufts on the flanks, the neck ruff and the cheeks, so it reads fluffy in low poly.
    for i, y in enumerate([1.3, 0.85, 0.35, -0.15, -0.65]):
        for k, (dx, dz, tilt) in enumerate([(0.88, 0.0, 1.2), (-0.88, 0.0, -1.2), (0.7, -0.4, 1.0), (-0.7, -0.4, -1.0)]):
            zc = top(y) - 0.85
            b.spike("Tuft%d_%d" % (i, k), (dx, y + 0.05 * (k % 2), zc + dz), (tilt, 0.25, -0.5), 0.26, 0.16,
                    "FurDark" if (i + k) % 2 else "Fur", n=3, chain=body_chain)
    for k, (dx, dz) in enumerate([(0.55, 0.35), (-0.55, 0.35), (0.62, -0.05), (-0.62, -0.05), (0.0, 0.55)]):
        b.spike("Ruff%d" % k, (dx, -1.25, 1.5 + dz), (dx, 0.6, 0.2 if dz > 0 else -0.4), 0.3, 0.17, "Fur", n=3,
                bone="Neck")

    # ------------------------------------------------------------ head: broad skull, short pale muzzle, sleepy eyes
    head = [((0, -1.3, 1.52), 1.2, 1.05), ((0, -1.6, 1.58), 1.08, 0.98), ((0, -1.9, 1.6), 0.95, 0.85),
            ((0, -2.12, 1.52), 0.7, 0.62)]
    b.loft("Skull", head, X, Z, 10, 2.2, lambda c, k, i: "Muzzle" if c.z < 1.3 else "Fur", jitter=0.03, bone="Head")
    muzzle = [((0, -2.1, 1.44), 0.62, 0.5), ((0, -2.38, 1.4), 0.44, 0.38), ((0, -2.52, 1.39), 0.3, 0.27)]
    b.loft("MuzzleM", muzzle, X, Z, 8, 2.2, "Muzzle", bone="Head")
    b.blob("Nose", (0, -2.56, 1.47), (0.14, 0.08, 0.1), "Nose", bone="Head")
    b.blob("MouthIn", (0, -2.28, 1.24), (0.2, 0.2, 0.06), "Mouth", bone="Head")
    b.loft("JawM", [((0, -2.02, 1.2), 0.5, 0.16), ((0, -2.42, 1.22), 0.3, 0.12)], X, Z, 8, 2.0, "Muzzle", bone="Jaw")
    for s in (1, -1):
        b.blob("Eye%d" % s, (0.3 * s, -1.98, 1.76), (0.06, 0.04, 0.05), "Eye", bone="Head")
        # Heavy lids half over the eyes: sleepy. Phase 3 lowers them further.
        b.box("Lid%d" % s, (0.3 * s, -2.0, 1.83), (0.17, 0.07, 0.06), "FurDark", rot=(0, math.radians(-8 * s), 0),
              bone="Lids")
        b.blob("Ear%d" % s, (0.38 * s, -1.58, 2.06), (0.16, 0.09, 0.16), "Fur", bone="Head")
        b.blob("EarIn%d" % s, (0.38 * s, -1.66, 2.06), (0.1, 0.03, 0.1), "EarInner", bone="Head")
        b.spike("Fang%d" % s, (0.1 * s, -2.42, 1.27), (0, -0.1, -1), 0.08, 0.03, "Nail", n=3, bone="Head")

    # ------------------------------------------------------------ the geode crest on its hump
    clusters = [(0.0, -0.9, 0), (0.0, -0.45, 0), (0.0, 0.0, 0), (0.0, 0.35, 0), (0.45, -0.65, 1), (-0.45, -0.65, 1),
                (0.5, -0.15, 1), (-0.5, -0.15, 1), (0.42, 0.3, 1), (-0.42, 0.3, 1)]
    for n, (x, y, flank) in enumerate(clusters):
        z = top(y) - (0.16 if flank else 0.02)
        lean = Vector((x * 1.2, 0, 1)).normalized()
        b.blob("GeodeRock%d" % n, (x, y, z), (0.26, 0.24, 0.13), "Geode", jitter=0.12, chain=body_chain)
        for k in range(4 if not flank else 3):
            a = 2 * math.pi * k / (4 if not flank else 3) + n
            off = Vector((math.cos(a), math.sin(a), 0)) * 0.1
            d = (lean + Vector((math.cos(a), math.sin(a), 0)) * 0.35).normalized()
            length = (0.62 if not flank else 0.45) * (1.0 - 0.18 * k) * (1.1 if n % 3 == 0 else 1.0)
            mat = "CrystalCyan" if (n + k) % 2 == 0 else "CrystalPurple"
            b.spike("Geode%d_%d" % (n, k), Vector((x, y, z + 0.04)) + off, d, length, 0.11, mat, n=6, chain=body_chain)

    # ------------------------------------------------------------ the ship's power cell, strapped on with scrap
    cy, cz = 1.0, 2.42
    b.tube("CellCapL", (0.42, cy, cz), (0.27, cy, cz), 0.25, 0.25, "MetalDark", n=8, bone="Cell")
    b.tube("CellCapR", (-0.27, cy, cz), (-0.42, cy, cz), 0.25, 0.25, "MetalDark", n=8, bone="Cell")
    b.tube("CellCore", (0.27, cy, cz), (-0.27, cy, cz), 0.21, 0.21, "CellGlow", n=8, bone="Cell")
    for x in (0.14, -0.14):
        b.tube("CellRib%d" % int(x * 100), (x + 0.025, cy, cz), (x - 0.025, cy, cz), 0.225, 0.225, "Metal", n=8, bone="Cell")
    b.box("CellBase", (0, cy, cz - 0.17), (0.75, 0.42, 0.08), "Metal", bone="Cell")
    b.blob("CellLight", (0.34, cy - 0.16, cz + 0.2), (0.05, 0.05, 0.05), "Signal", subdiv=0, bone="Cell")
    for s in (1, -1):
        b.path("Wire%d" % s, [(0.42 * s, cy, cz + 0.05), (0.58 * s, cy + 0.15, cz + 0.12), (0.56 * s, cy + 0.32, cz - 0.08)],
               0.025, "MetalDark", n=4, bone="Cell")
        for sy in (-0.15, 0.15):
            b.path("Strap%d%d" % (s, int(sy * 100)), [(0.3 * s, cy + sy, cz - 0.12), (0.72 * s, cy + sy, top(cy) - 0.2),
                                                     (0.97 * s, cy + sy, 1.6), (0.9 * s, cy + sy, 1.05)],
                   0.04, "Strap", n=4, chain=body_chain)

    # ------------------------------------------------------------ legs: thick pillars, big paws, cream claws
    for side, s in (("L", 1), ("R", -1)):
        b.blob("Shoulder" + side, (0.6 * s, -0.85, 1.3), (0.45, 0.55, 0.6), "Fur", jitter=0.05, bone="UpperArm" + side)
        b.path("UpperArmM" + side, [(0.6 * s, -0.85, 1.25), (0.62 * s, -0.9, 0.65)], [0.36, 0.3], "Fur", n=7,
               bone="UpperArm" + side)
        b.path("ForearmM" + side, [(0.62 * s, -0.9, 0.65), (0.62 * s, -0.95, 0.15)], [0.3, 0.27], "FurDark", n=7,
               bone="Forearm" + side)
        b.blob("Paw" + side, (0.62 * s, -1.05, 0.1), (0.3, 0.38, 0.12), "FurDark", bone="Hand" + side)
        b.blob("Haunch" + side, (0.55 * s, 1.2, 1.2), (0.5, 0.65, 0.6), "Fur", jitter=0.05, bone="Thigh" + side)
        b.path("ThighM" + side, [(0.55 * s, 1.2, 1.15), (0.55 * s, 1.3, 0.6)], [0.38, 0.3], "Fur", n=7, bone="Thigh" + side)
        b.path("ShinM" + side, [(0.55 * s, 1.3, 0.6), (0.55 * s, 1.25, 0.15)], [0.28, 0.25], "FurDark", n=7,
               bone="Shin" + side)
        b.blob("FootM" + side, (0.55 * s, 1.12, 0.1), (0.28, 0.4, 0.11), "FurDark", bone="Foot" + side)
        for t, dx in enumerate((-0.16, -0.05, 0.05, 0.16)):
            b.spike("Claw%s%d" % (side, t), (0.62 * s + dx, -1.38, 0.1), (dx, -1, -0.35), 0.16, 0.04, "Nail", n=3,
                    bone="Hand" + side)
            b.spike("Toe%s%d" % (side, t), (0.55 * s + dx, 0.75, 0.08), (dx, -1, -0.3), 0.1, 0.035, "Nail", n=3,
                    bone="Foot" + side)
    return b


def animate(b):
    names = ["Root", "Pivot", "Hips", "Spine", "Chest", "Neck", "Head", "Jaw", "Crest", "Cell"]
    b.base = {k: (0, 0, 0) for k in names}
    b.base.update(sym({k + "L": (0, 0, 0) for k in ["UpperArm", "Forearm", "Hand", "Thigh", "Shin", "Foot"]}))
    b.base["Root@"] = (0, 0, 0)

    def M(*poses):
        out = {}
        for q in poses:
            out.update(q)
        return out

    def gait(t, swing, lift, pairs):
        """Four legs. pairs: phase offsets (LF, RF, LH, RH). -pitch swings a leg forward; the lower leg folds as it lifts."""
        out = {}
        for (leg, side), ph in zip((("F", "L"), ("F", "R"), ("H", "L"), ("H", "R")), pairs):
            p = t + ph
            up = max(0.0, math.cos(p))
            if leg == "F":
                out["UpperArm" + side] = (-swing * math.sin(p), 0, 0)
                out["Forearm" + side] = (lift * up, 0, 0)
                out["Hand" + side] = (-lift * 0.6 * up, 0, 0)
            else:
                out["Thigh" + side] = (-swing * 0.9 * math.sin(p), 0, 0)
                out["Shin" + side] = (lift * up, 0, 0)
                out["Foot" + side] = (-lift * 0.7 * up, 0, 0)
        return out

    keys = []
    for f in range(0, 81, 10):
        t = 2 * math.pi * f / 80
        keys.append((f, {"Spine": (1.5 * math.sin(t), 0, 0), "Chest": (-1.5 * math.sin(t), 0, 0),
                         "Neck": (2 * math.sin(t + 1), 4 * math.sin(t * 0.5), 0), "Head": (2 * math.sin(t + 2), 0, 3 * math.sin(t * 0.5)),
                         "Jaw": (2 * max(0.0, math.sin(t)), 0, 0), "Root@": (0, 0.02 * math.sin(t), 0)}))
    b.clip("Idle", keys, loop=True)

    keys = []
    for f in range(0, 41, 5):
        t = 2 * math.pi * f / 40
        pose = {"Root@": (0, 0.04 * abs(math.sin(2 * t)), 0), "Pivot": (0, 2 * math.sin(t), 3 * math.sin(t)),
                "Neck": (3 * math.sin(2 * t), -3 * math.sin(t), 0), "Head": (-2 * math.sin(2 * t), 0, 0)}
        pose.update(gait(t, 18, 30, (0, math.pi, math.pi, 0)))
        keys.append((f, pose))
    b.clip("Walk", keys, loop=True)

    keys = []
    for f in range(0, 25, 3):
        t = 2 * math.pi * f / 24
        pose = {"Root@": (0, 0.12 * abs(math.sin(t)), 0), "Hips": (6 * math.sin(t), 0, 0), "Chest": (-4 * math.sin(t), 0, 0),
                "Neck": (5 * math.sin(t + 1), 0, 0)}
        pose.update(gait(t, 32, 45, (0, 0.5, math.pi, math.pi + 0.5)))
        keys.append((f, pose))
    b.clip("Run", keys, loop=True)

    # Rearing onto the hind legs: the front tips up about the hips, the thighs counter-turn to keep the legs planted.
    def rear(angle):
        return {"Hips": (-angle, 0, 0), "ThighL": (angle, 0, 0), "ThighR": (angle, 0, 0)}

    # Roar: rears up, arms flung wide, jaw open.
    wide = sym({"UpperArmL": (-30, 0, 38), "ForearmL": (-20, 0, 0), "HandL": (-15, 0, 0)})
    reared = M(rear(55), wide, {"Neck": (-8, 0, 0), "Head": (-20, 0, 0), "Jaw": (10, 0, 0)})
    roar = M(reared, {"Head": (-28, 0, 0), "Jaw": (38, 0, 0)})
    b.clip("Roar", [(0, {}), (16, reared), (24, roar), (36, M(roar, {"Head": (-28, 10, 8)})),
                    (48, M(roar, {"Head": (-26, -10, -8)})), (70, {})], events={"roar": 22})

    # Paw swipe (right paw): shifts back and raises it high (the warning), then rakes it across the front.
    raise_r = M(rear(14), {"Root@": (-0.1, 0.05, -0.1), "Chest": (0, -12, 0), "Neck": (0, 8, 0),
                           "UpperArmR": (-85, 0, -28), "ForearmR": (-30, 0, 0), "HandR": (-20, 0, 0)})
    strike_r = M(rear(6), {"Root@": (0.1, 0, 0.35), "Chest": (6, 18, 0), "Neck": (4, -10, 0),
                           "UpperArmR": (-50, 35, 35), "ForearmR": (-10, 0, 0), "HandR": (10, 0, 0)})
    b.clip("SwipeR", [(0, {}), (14, raise_r), (17, raise_r), (21, strike_r), (25, strike_r), (42, {})],
           events={"move": 14, "hit": 21})
    b.clip("SwipeL", [(0, {}), (14, mirror(raise_r)), (17, mirror(raise_r)), (21, mirror(strike_r)),
                      (25, mirror(strike_r)), (42, {})], events={"move": 14, "hit": 21})

    # Boulder lob: digs both paws under a rock, rips it up, rears with it overhead, and hurls it.
    dig = {"Root@": (0, -0.12, 0.15), "Hips": (10, 0, 0), "Neck": (14, 0, 0), "Head": (10, 0, 0)}
    dig.update(sym({"UpperArmL": (-24, 0, -6), "ForearmL": (34, 0, 0), "HandL": (20, 0, 0)}))
    rip = M(rear(20), sym({"UpperArmL": (-60, 0, -4), "ForearmL": (-10, 0, 0)}), {"Root@": (0, 0.05, 0)})
    overhead = M(rear(36), sym({"UpperArmL": (-128, 0, -6), "ForearmL": (-25, 0, 0), "HandL": (-15, 0, 0)}),
                 {"Neck": (-10, 0, 0), "Head": (-14, 0, 0), "Chest": (-6, 0, 0)})
    throw = M(rear(12), sym({"UpperArmL": (-55, 0, -4), "ForearmL": (0, 0, 0), "HandL": (15, 0, 0)}),
              {"Root@": (0, 0, 0.4), "Chest": (14, 0, 0), "Neck": (8, 0, 0)})
    b.clip("Lob", [(0, {}), (12, dig), (18, rip), (30, overhead), (34, M(overhead, {"Chest": (-12, 0, 0)})), (38, throw),
                   (44, throw), (60, {})], events={"rip": 18, "throw": 38})

    # Big yawn: sits back, stretches a paw, head up, jaw wide (the dust cone), then sways dizzily (the opening).
    sit = M(rear(30), {"Root@": (0, -0.18, -0.2), "Neck": (-14, 0, 0), "Head": (-26, 0, 0), "Jaw": (20, 0, 0),
                       "UpperArmL": (-95, 0, 22), "ForearmL": (-15, 0, 0)})
    gape = M(sit, {"Jaw": (42, 0, 0), "Head": (-30, 0, 0)})
    shut = M(sit, {"Jaw": (2, 0, 0), "Head": (-10, 0, 0), "UpperArmL": (-30, 0, 10)})
    sway_a = {"Neck": (4, 16, 0), "Head": (6, 10, 18), "Pivot": (0, 0, 5), "Root@": (0, -0.05, 0)}
    sway_b = {"Neck": (4, -16, 0), "Head": (6, -10, -18), "Pivot": (0, 0, -5), "Root@": (0, -0.05, 0)}
    b.clip("Yawn", [(0, {}), (14, sit), (20, gape), (34, gape), (42, shut), (52, sway_a), (64, sway_b), (76, sway_a),
                    (90, {})], events={"yawn_start": 20, "yawn_end": 40, "recovered": 82})

    # Belly flop: crouches, launches, flies belly-down with all four legs spread, lands flat, gets up.
    crouch = M(sym({"ThighL": (-18, 0, 0), "ShinL": (30, 0, 0), "ForearmL": (25, 0, 0), "UpperArmL": (10, 0, 0)}),
               {"Root@": (0, -0.3, 0), "Neck": (10, 0, 0)})
    launch = M(rear(22), sym({"UpperArmL": (-45, 0, 40)}), {"Root@": (0, 0.2, 0), "Head": (-10, 0, 0)})
    spread = M(sym({"UpperArmL": (-15, 0, 72), "ForearmL": (-10, 0, 0), "ThighL": (20, 0, 62), "ShinL": (10, 0, 0)}),
               {"Neck": (-12, 0, 0), "Head": (-10, 0, 0), "Jaw": (18, 0, 0)})
    flat = M(spread, {"Root@": (0, -0.72, 0), "Jaw": (6, 0, 0), "Neck": (6, 0, 0)})
    b.clip("BellyFlop", [(0, {}), (12, crouch), (18, launch), (26, spread), (38, spread), (40, flat), (56, flat),
                         (64, M(flat, {"Head": (0, 16, 14)})), (70, M(flat, {"Head": (0, -16, -14)})), (88, {})],
           events={"launch": 18, "land": 40, "recovered": 74})

    # Bear-hug lunge: rears with arms wide (the warning), charges upright, and wraps its arms shut.
    hug_wide = M(rear(55), sym({"UpperArmL": (-50, 0, 70), "ForearmL": (-20, 0, 0)}), {"Head": (-10, 0, 0), "Jaw": (16, 0, 0)})
    charge = M(rear(40), sym({"UpperArmL": (-55, 0, 62), "ForearmL": (-20, 0, 0)}), {"Root@": (0, 0, 0.25), "Head": (-4, 0, 0)})
    closed = M(rear(45), sym({"UpperArmL": (-78, 0, -16), "ForearmL": (-42, 0, 0), "HandL": (-10, 0, 0)}),
               {"Root@": (0, 0, 0.35), "Head": (-6, 0, 0)})
    b.clip("HugLunge", [(0, {}), (14, hug_wide), (20, hug_wide), (24, charge), (32, charge), (36, closed), (42, closed),
                        (60, {})], events={"charge": 20, "catch": 34, "catch_end": 42})
    squeeze = M(closed, sym({"UpperArmL": (-78, 0, -26), "ForearmL": (-50, 0, 0)}), {"Chest": (8, 0, 0), "Jaw": (14, 0, 0)})
    fling = M(rear(35), sym({"UpperArmL": (-95, 0, 60)}), {"Chest": (-6, 28, 0), "Head": (-12, 0, 0)})
    b.clip("HugSqueeze", [(0, closed), (6, closed), (10, squeeze), (16, closed), (22, squeeze), (28, closed), (34, squeeze),
                          (42, closed), (48, fling), (54, fling), (72, {})],
           events={"squeeze": [10, 22, 34], "throw": 48})

    # Hibernate: lies down and curls round, head tucked toward its tail; snores; jolts awake, dazed.
    lie = M(sym({"UpperArmL": (-62, 0, 0), "ForearmL": (10, 0, 0), "ThighL": (-60, 0, 0), "ShinL": (85, 0, 0),
                 "FootL": (-20, 0, 0)}), {"Root@": (0, -0.4, 0)})
    curled = M(lie, {"Root@": (0, -0.72, 0), "Spine": (0, 14, 0), "Chest": (0, 18, 0), "Neck": (30, 28, 0),
                     "Head": (22, 20, 0), "Jaw": (0, 0, 0)})
    b.clip("CurlUp", [(0, {}), (14, lie), (28, curled), (34, curled)], events={"curl": 28})
    keys = []
    for f in range(0, 61, 10):
        t = 2 * math.pi * f / 60
        keys.append((f, M(curled, {"Root@": (0, -0.72 + 0.03 * math.sin(t), 0), "Spine": (2 * math.sin(t), 14, 0),
                                   "Chest": (2 * math.sin(t), 18, 0), "Jaw": (6 * max(0.0, math.sin(t)), 0, 0)})))
    b.clip("Hibernate", keys, loop=True)
    jolt = M(lie, {"Root@": (0, -0.3, 0), "Neck": (-20, 0, 0), "Head": (-24, 0, 0), "Jaw": (24, 0, 0)})
    daze_a = {"Neck": (6, 14, 0), "Head": (8, 8, 18), "Pivot": (0, 0, 4)}
    daze_b = {"Neck": (6, -14, 0), "Head": (8, -8, -18), "Pivot": (0, 0, -4)}
    b.clip("WakeUp", [(0, curled), (6, jolt), (16, {}), (24, daze_a), (34, daze_b), (44, daze_a), (52, daze_b), (64, {})],
           events={"up": 16, "recovered": 54})

    hit = M(rear(12), {"Root@": (0, 0, -0.3), "Neck": (-10, 14, 0), "Head": (-14, 12, 10), "Jaw": (18, 0, 0)})
    b.clip("Stagger", [(0, {}), (6, hit), (22, M(hit, {"Head": (-6, 8, 6)})), (40, {})])
    dead = M(sym({"UpperArmL": (-20, 0, 25), "ThighL": (-10, 0, 20)}),
             {"Root@": (0, -0.45, 0), "Pivot": (0, 0, 86), "Neck": (10, 10, 0), "Head": (10, 18, 0), "Jaw": (14, 0, 0)})
    b.clip("Death", [(0, {}), (8, hit), (24, M(hit, {"Root@": (0, -0.2, 0), "Pivot": (0, 0, 30)})), (42, dead), (70, dead)])
    return b


def shots(b, folder):
    front, look = (-5.5, -9.0, 3.4), (0, -0.3, 1.4)
    side = (9.5, -1.0, 3.0)
    return b.previews(folder, [
        ("bear_front", None, 0, front, look),
        ("bear_side", None, 0, side, (0, 0.2, 1.3)),
        ("bear_back", None, 0, (4.5, 8.0, 4.5), (0, 0.4, 1.8)),
        ("bear_face", None, 0, (-1.6, -5.0, 2.0), (0, -2.0, 1.6)),
        ("bear_roar", "Roar", 26, front, (0, -0.3, 2.2)),
        ("bear_swipe_raise", "SwipeR", 16, front, look),
        ("bear_swipe_hit", "SwipeR", 22, front, look),
        ("bear_lob_overhead", "Lob", 32, side, (0, 0, 2.2)),
        ("bear_yawn", "Yawn", 28, front, (0, -0.3, 1.8)),
        ("bear_flop_air", "BellyFlop", 30, front, look),
        ("bear_flop_flat", "BellyFlop", 48, side, (0, 0.2, 0.8)),
        ("bear_hug_wide", "HugLunge", 18, front, (0, -0.3, 2.2)),
        ("bear_hug_closed", "HugLunge", 38, side, (0, -0.3, 2.2)),
        ("bear_curled", "Hibernate", 0, (5.0, 6.0, 5.0), (0, 0, 0.9)),
        ("bear_wake", "WakeUp", 6, front, (0, -0.3, 1.0)),
        ("bear_death", "Death", 60, front, (0, 0, 0.8)),
    ])

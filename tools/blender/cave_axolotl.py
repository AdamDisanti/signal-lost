# The Cave Axolotl: the Crystal Caves stage boss (docs/cave-axolotl.md). A huge, pale peach-pink axolotl with a
# wide flat head, a little smile, black bead eyes, three feathery coral gills on each side (its weak spot), short
# star-toed legs and a long, flattened tail with a fin. Its back and tail are crusted with cave crystals: the Unity
# builder swaps the crystal material for the cave's own, and the boss recolours them each time it hides.
# About 7.5 m long including the tail. Built with the desert bosses' kit (bosskit.py); run tools/blender/cave_bosses.py.
import math
from mathutils import Vector
from bosskit import Boss, srgb, sym, mirror, X, Y, Z


def crystal(b, name, base, direction, length, radius, **skin):
    """A cave crystal: a hexagonal prism with a pointed tip, like the level's crystal batches."""
    base, d = Vector(base), Vector(direction).normalized()
    mid = base + d * (length * 0.6)
    b.tube(name + "P", base - d * 0.08, mid, radius, radius * 0.95, "Crystal", n=6, power=2.0, **skin)
    b.spike(name + "T", mid, d, length * 0.4, radius * 0.95, "Crystal", n=6, **skin)


def build():
    b = Boss("CaveAxolotl")
    b.material("Skin", srgb(238, 188, 168), rough=0.8)
    b.material("SkinTop", srgb(226, 170, 150), rough=0.8)
    b.material("Belly", srgb(247, 212, 194), rough=0.85)
    b.material("Gill", srgb(226, 122, 120), rough=0.7)
    b.material("GillFringe", srgb(204, 94, 102), rough=0.7)
    b.material("Eye", srgb(16, 14, 18), rough=0.15)
    b.material("Mouth", srgb(120, 58, 66), rough=0.7)
    # Its crystals: the Unity builder swaps this for one of the cave's own crystal materials.
    b.material("Crystal", srgb(230, 110, 190), rough=0.2, emit=srgb(255, 110, 200), strength=2.5)

    # ------------------------------------------------------------ skeleton (faces -Y, +X is its left)
    b.bone("Root", (0, 0, 0))
    b.bone("Pivot", (0, 0.3, 1.0), "Root")
    b.bone("Hips", (0, 1.1, 1.0), "Pivot")
    b.bone("Spine", (0, 0.2, 1.05), "Hips")
    b.bone("Chest", (0, -0.8, 1.1), "Spine")
    b.bone("Head", (0, -1.45, 1.15), "Chest")
    b.bone("Jaw", (0, -1.7, 0.85), "Head")
    for side, s in (("L", 1), ("R", -1)):
        b.bone("Gill" + side, (0.85 * s, -1.5, 1.45), "Head")
        b.bone("UpperArm" + side, (0.7 * s, -0.85, 0.8), "Chest")
        b.bone("Forearm" + side, (1.15 * s, -1.0, 0.45), "UpperArm" + side)
        b.bone("Hand" + side, (1.3 * s, -1.1, 0.12), "Forearm" + side)
        b.bone("Thigh" + side, (0.65 * s, 1.05, 0.8), "Hips")
        b.bone("Shin" + side, (1.1 * s, 1.2, 0.45), "Thigh" + side)
        b.bone("Foot" + side, (1.25 * s, 1.3, 0.12), "Shin" + side)
    tail = [(0, 1.8, 0.95), (0, 2.5, 0.9), (0, 3.2, 0.85), (0, 3.9, 0.8)]
    prev = "Hips"
    for i, p in enumerate(tail):
        prev = b.bone("Tail%d" % (i + 1), p, prev)
    b.bone("TailTip", (0, 4.6, 0.75), prev)

    body_chain = ["Hips", "Spine", "Chest"]
    # ------------------------------------------------------------ body: long and soft, a paler belly
    body = [(-1.2, 1.12, 1.7, 1.15), (-0.7, 1.12, 1.85, 1.25), (0.0, 1.1, 1.95, 1.3), (0.7, 1.05, 1.85, 1.2),
            (1.3, 1.0, 1.5, 1.05), (1.8, 0.96, 1.0, 0.85)]

    def body_mat(c, k, i):
        if k < 0 or k >= len(body) - 1:
            return "Skin"
        zc = (body[k][1] + body[k + 1][1]) / 2
        if c.z < zc - 0.18:
            return "Belly"
        return "SkinTop" if c.z > zc + 0.4 else "Skin"
    b.loft("Body", [((0, y, z), w, h) for y, z, w, h in body], X, Z, 12, 2.2, body_mat, jitter=0.03, chain=body_chain)

    # ------------------------------------------------------------ head: wide and flat, a smile, bead eyes
    head = [((0, -1.15, 1.15), 1.9, 1.25), ((0, -1.6, 1.15), 2.15, 1.28), ((0, -2.1, 1.1), 2.1, 1.12),
            ((0, -2.5, 1.05), 1.8, 0.88), ((0, -2.72, 1.0), 1.3, 0.56)]
    b.loft("Skull", head, X, Z, 12, 2.4, lambda c, k, i: "Belly" if c.z < 0.85 else ("SkinTop" if c.z > 1.55 else "Skin"),
           jitter=0.025, bone="Head")
    b.blob("JawM", (0, -2.15, 0.74), (0.86, 0.55, 0.2), "Belly", bone="Jaw")
    smile = []
    for k in range(7):
        x = -0.72 + 0.24 * k
        smile.append((x, -2.62 + 0.32 * (x / 0.72) ** 2, 0.88 + 0.06 * (x / 0.72) ** 2))
    b.path("Smile", smile, 0.035, "Mouth", n=4, bone="Head")
    for s in (1, -1):
        b.blob("Eye%d" % s, (0.74 * s, -2.32, 1.4), (0.17, 0.17, 0.18), "Eye", bone="Head")
        b.blob("Nostril%d" % s, (0.22 * s, -2.76, 1.08), (0.04, 0.03, 0.03), "Mouth", subdiv=0, bone="Head")

    # ------------------------------------------------------------ gills: three feathery stalks a side (weak spot)
    for side, s in (("L", 1), ("R", -1)):
        for k, (dz, d, length) in enumerate([(0.42, (1.0, 0.35, 0.95), 1.05), (0.12, (1.0, 0.5, 0.3), 0.95),
                                             (-0.18, (1.0, 0.55, -0.25), 0.8)]):
            base = Vector((0.88 * s, -1.42 + 0.06 * k, 1.28 + dz))
            dv = Vector((d[0] * s, d[1], d[2])).normalized()
            b.spike("Gill%s%d" % (side, k), base, dv, length, 0.16, "Gill", n=5, bone="Gill" + side)
            back = Vector((0.15 * s, 1, -0.25)).normalized()
            for f in range(3):
                at = base + dv * (length * (0.3 + 0.22 * f))
                b.spike("Fringe%s%d%d" % (side, k, f), at, (back + dv * 0.3).normalized(), 0.32 - 0.06 * f, 0.06,
                        "GillFringe", n=3, bone="Gill" + side)

    # ------------------------------------------------------------ legs: short, sprawled, star toes
    for side, s in (("L", 1), ("R", -1)):
        for limb, (a, bb, c, parent), front in (("Arm", ((0.7 * s, -0.85, 0.85), (1.15 * s, -1.0, 0.45), (1.3 * s, -1.1, 0.12),
                                                 ("UpperArm", "Forearm", "Hand")), True),
                                                ("Leg", ((0.65 * s, 1.05, 0.85), (1.1 * s, 1.2, 0.45), (1.25 * s, 1.3, 0.12),
                                                 ("Thigh", "Shin", "Foot")), False)):
            b.blob(limb + "Top" + side, a, (0.4, 0.42, 0.36), "Skin", jitter=0.03, bone=parent[0] + side)
            b.path(limb + "Upper" + side, [a, bb], [0.26, 0.22], "Skin", n=6, bone=parent[0] + side)
            b.path(limb + "Lower" + side, [bb, c], [0.22, 0.18], "Skin", n=6, bone=parent[1] + side)
            foot = Vector(c) + Vector((0.12 * s, -0.08 if front else 0.05, -0.06))
            b.blob(limb + "Palm" + side, foot, (0.26, 0.26, 0.08), "Skin", bone=parent[2] + side)
            for t in range(4):
                ang = math.radians(-70 + 46 * t) if front else math.radians(-40 + 46 * t)
                d = Vector((math.cos(ang) * s, -math.sin(ang), -0.05)).normalized()
                b.spike(limb + "Toe%s%d" % (side, t), foot + d * 0.12, d, 0.36, 0.07, "Skin", n=4, bone=parent[2] + side)

    # ------------------------------------------------------------ tail: long and flattened, a fin along the top
    tchain = ["Hips", "Tail1", "Tail2", "Tail3", "Tail4", "TailTip"]
    b.path("Tail", [(0, 1.55, 0.98), (0, 2.3, 0.92), (0, 3.0, 0.86), (0, 3.7, 0.8), (0, 4.4, 0.75), (0, 5.05, 0.72)],
           [(0.55, 0.5), (0.42, 0.48), (0.32, 0.44), (0.22, 0.38), (0.12, 0.3), (0.03, 0.14)], "Skin", n=8, chain=tchain)
    # The fin runs only past the crystals, so it never hides them.
    b.path("TailFin", [(0, 3.95, 1.0), (0, 4.5, 0.98), (0, 5.15, 0.78)], [(0.03, 0.06), (0.035, 0.2), (0.02, 0.06)],
           "SkinTop", n=4, chain=tchain[3:])
    b.path("TailFinLow", [(0, 2.8, 0.42), (0, 3.6, 0.42), (0, 4.4, 0.5), (0, 5.05, 0.64)],
           [(0.03, 0.04), (0.04, 0.12), (0.035, 0.12), (0.02, 0.04)], "Belly", n=4, chain=tchain[2:])

    # ------------------------------------------------------------ crystals: the back, the crown and the tail
    rows = [(-0.95, 1.72, 0.85), (-0.45, 1.77, 1.0), (0.1, 1.77, 1.1), (0.65, 1.67, 0.95), (1.15, 1.55, 0.75)]
    for i, (y, z, h) in enumerate(rows):
        bone = "Chest" if y < -0.5 else "Spine" if y < 0.6 else "Hips"
        crystal(b, "Back%d" % i, (0.04 * (i % 2), y, z - 0.08), (0.08 * (1 if i % 2 else -1), 0.1, 1), h, 0.15, bone=bone)
        for s in (1, -1):
            crystal(b, "Back%d_%d" % (i, s), (0.32 * s, y + 0.12, z - 0.16), (0.55 * s, 0.15, 1), h * 0.62, 0.11, bone=bone)
    for s in (1, -1):
        crystal(b, "Crown%d" % s, (0.38 * s, -1.25, 1.66), (0.35 * s, 0.25, 1), 0.45, 0.09, bone="Head")
        crystal(b, "Flank%d" % s, (0.85 * s, -0.15, 1.3), (1, 0.2, 0.7), 0.5, 0.1, bone="Spine")
        crystal(b, "Hip%d" % s, (0.72 * s, 0.85, 1.3), (1, 0.4, 0.8), 0.42, 0.09, bone="Hips")
    # The tail's crystals: they're what the swipe hits with.
    for i, (y, z, bone) in enumerate([(2.0, 1.36, "Tail1"), (2.7, 1.3, "Tail2"), (3.4, 1.2, "Tail3"), (4.05, 1.06, "Tail4")]):
        crystal(b, "TailC%d" % i, (0, y, z - 0.2), (0, 0.45, 1), 0.85 - 0.1 * i, 0.13 - 0.015 * i, bone=bone)
        for s in (1, -1):
            crystal(b, "TailS%d_%d" % (i, s), (0.14 * s, y + 0.2, z - 0.3), (0.8 * s, 0.4, 0.6), 0.55 - 0.07 * i,
                    0.09, bone=bone)
    return b


def animate(b):
    names = ["Root", "Pivot", "Hips", "Spine", "Chest", "Head", "Jaw", "Tail1", "Tail2", "Tail3", "Tail4", "TailTip"]
    b.base = {k: (0, 0, 0) for k in names}
    b.base.update(sym({k + "L": (0, 0, 0) for k in ["Gill", "UpperArm", "Forearm", "Hand", "Thigh", "Shin", "Foot"]}))
    b.base["Root@"] = (0, 0, 0)

    def M(*poses):
        out = {}
        for q in poses:
            out.update(q)
        return out

    def tail(t, amp, lift=0.0):
        return {"Tail%d" % (i + 1): (lift + 1.5 * math.sin(t + i), amp * math.sin(t - i * 0.7), 0) for i in range(4)}

    def gills(t, amp, flare=0.0):
        return {"GillL": (-flare * 0.4, 0, amp * math.sin(t) + flare), "GillR": (-flare * 0.4, 0, -amp * math.sin(t) - flare)}

    def gait(t, sweep, lift):
        """Salamander walk: diagonal legs step together while the body swings side to side."""
        out = {}
        for side, s, ph in (("L", 1, 0.0), ("R", -1, math.pi)):
            p = t + ph
            out["UpperArm" + side] = (0, s * sweep * math.sin(p), s * lift * max(0.0, math.cos(p)))
            out["Hand" + side] = (0, s * -0.5 * sweep * math.sin(p), 0)
            out["Thigh" + side] = (0, s * sweep * math.sin(p + math.pi), s * lift * max(0.0, math.cos(p + math.pi)))
            out["Foot" + side] = (0, s * -0.5 * sweep * math.sin(p + math.pi), 0)
        return out

    keys = []
    for f in range(0, 61, 10):
        t = 2 * math.pi * f / 60
        pose = {"Chest": (1.2 * math.sin(t), 0, 0), "Head": (1.5 * math.sin(t + 1), 4 * math.sin(t * 0.5), 0),
                "Jaw": (2 * max(0.0, math.sin(t)), 0, 0), "Root@": (0, 0.03 * math.sin(t), 0)}
        pose.update(tail(t, 7))
        pose.update(gills(t * 2, 8))
        keys.append((f, pose))
    b.clip("Idle", keys, loop=True)

    keys = []
    for f in range(0, 41, 5):
        t = 2 * math.pi * f / 40
        pose = {"Root@": (0, 0.04 * abs(math.sin(2 * t)), 0), "Pivot": (0, 7 * math.sin(t), 0),
                "Chest": (0, -9 * math.sin(t), 0), "Head": (0, -5 * math.sin(t), 0)}
        pose.update(gait(t, 26, 14))
        pose.update(tail(t + 1.2, 14))
        pose.update(gills(t * 2, 6))
        keys.append((f, pose))
    b.clip("Walk", keys, loop=True)

    keys = []
    for f in range(0, 25, 3):
        t = 2 * math.pi * f / 24
        pose = {"Root@": (0, 0.1 * abs(math.sin(t)), 0), "Pivot": (2, 11 * math.sin(t), 0),
                "Chest": (0, -13 * math.sin(t), 0), "Head": (-4, -7 * math.sin(t), 0)}
        pose.update(gait(t, 38, 22))
        pose.update(tail(t + 1.2, 20, 4))
        pose.update(gills(t * 2, 4, 6))
        keys.append((f, pose))
    b.clip("Run", keys, loop=True)

    # Roar: rears its head up, mouth wide, gills flared.
    rear = M(gills(0, 0, 30), {"Root@": (0, 0.25, 0.1), "Pivot": (-12, 0, 0), "Chest": (-10, 0, 0), "Head": (-22, 0, 0),
                               "Jaw": (28, 0, 0), "Tail1": (10, 0, 0), "Tail2": (6, 0, 0),
                               "UpperArmL": (0, 0, -8), "UpperArmR": (0, 0, 8)})
    b.clip("Roar", [(0, {}), (14, rear), (20, M(rear, {"Head": (-28, 0, 0), "Jaw": (34, 0, 0)})),
                    (36, M(rear, {"Head": (-26, 12, 6), "Jaw": (34, 0, 0)})),
                    (50, M(rear, {"Head": (-24, -12, -6), "Jaw": (30, 0, 0)})), (70, {})], events={"roar": 20})

    # Tail swipe: turns away and coils the crystal tail high (the warning: the crystals flare), holds, then spins
    # right round so the crystal tail scythes a full circle.
    coil = {"Root@": (0, -0.1, 0), "Pivot": (0, 42, 0), "Chest": (0, 18, 0), "Head": (0, 16, 0),
            "Tail1": (14, 22, 0), "Tail2": (10, 16, 0), "Tail3": (8, 10, 0), "Tail4": (6, 6, 0)}
    keys = [(0, {}), (16, coil), (24, M(coil, {"Pivot": (0, 52, 0)}))]
    for f, yaw in ((27, -30), (30, -120), (33, -210), (36, -290), (39, -345)):
        pose = {"Root@": (0, 0.08, 0), "Pivot": (0, yaw, 0), "Chest": (0, -10, 0), "Head": (0, -12, 0),
                "Tail1": (-4, -22, 0), "Tail2": (-3, -14, 0), "Tail3": (-2, -10, 0), "Tail4": (0, -6, 0)}
        pose.update(sym({"UpperArmL": (0, 0, 20)}))
        keys.append((f, pose))
    keys += [(46, {"Pivot": (0, -360, 0), "Chest": (0, -5, 0)}), (64, {"Pivot": (0, -360, 0)})]
    b.clip("TailSwipe", keys, events={"hit_start": 26, "hit_end": 39})

    # Roll: it tucks its head, curls its tail under and draws its legs in: a crystal-studded ball. The code spins
    # the Pivot while it rolls.
    ball = M(sym({"UpperArmL": (-30, 30, -40), "ForearmL": (-30, 0, -30), "ThighL": (30, -30, -40), "ShinL": (30, 0, -30)}),
             gills(0, 0, -20),
             {"Root@": (0, 0.6, 0.3), "Pivot": (30, 0, 0), "Chest": (55, 0, 0), "Head": (70, 0, 0), "Jaw": (0, 0, 0),
              "Hips": (-45, 0, 0), "Tail1": (-55, 0, 0), "Tail2": (-58, 0, 0), "Tail3": (-60, 0, 0), "Tail4": (-55, 0, 0),
              "TailTip": (-40, 0, 0)})
    b.clip("RollStart", [(0, {}), (8, M({"Root@": (0, -0.12, 0), "Pivot": (6, 0, 0)}, gills(0, 0, 16))),
                         (22, ball), (28, ball)], events={"curled": 22})
    b.clip("Roll", [(0, ball), (10, M(ball, {"Root@": (0, 0.66, 0.3)})), (20, ball)], loop=True)
    b.clip("RollEnd", [(0, ball), (14, M({"Root@": (0, 0.1, 0)}, gills(0, 0, 10))), (24, {})])
    # Crash: bounces off whatever it hit, flops down splayed and dizzy, gills flared and exposed: the opening.
    flop = M(sym({"UpperArmL": (0, 0, 28), "ThighL": (0, 0, 28)}), gills(0, 0, 32),
             {"Root@": (0, -0.32, -0.2), "Pivot": (6, 0, 0), "Chest": (8, 0, 0), "Head": (14, 0, 0), "Jaw": (14, 0, 0)})
    keys = [(0, ball), (6, M(ball, {"Root@": (0, 0.9, -0.6), "Pivot": (-10, 0, 0)})), (14, flop)]
    for f in range(22, 71, 12):
        keys.append((f, M(flop, {"Head": (14, 18 if (f // 12) % 2 else -18, 14 if (f // 12) % 2 else -14)})))
    keys += [(84, {})]
    b.clip("RollCrash", keys, events={"down": 14, "recovered": 74})

    # Shard ring: arches its back high so its crystals stand up and glow (the warning), then snaps down hunched,
    # firing the shards off its back in a ring.
    arch = M(gills(0, 0, 18), {"Root@": (0, 0.3, 0), "Pivot": (-8, 0, 0), "Spine": (-8, 0, 0), "Chest": (-14, 0, 0),
                               "Head": (-16, 0, 0), "Jaw": (10, 0, 0), "Tail1": (14, 0, 0), "Tail2": (10, 0, 0),
                               "UpperArmL": (0, 0, -10), "UpperArmR": (0, 0, 10)})
    snap = M(gills(0, 0, 26), {"Root@": (0, -0.18, 0), "Pivot": (6, 0, 0), "Spine": (10, 0, 0), "Chest": (12, 0, 0),
                               "Head": (10, 0, 0), "Jaw": (20, 0, 0), "Tail1": (-8, 0, 0)})
    b.clip("ShardRing", [(0, {}), (18, arch), (28, M(arch, {"Root@": (0, 0.36, 0)})), (31, snap), (40, snap), (60, {})],
           events={"fire": 31})

    # Burrow: digs nose-first with its front feet and wriggles under.
    dig1 = M(sym({"UpperArmL": (0, -30, -10)}), {"UpperArmL": (0, 40, -20), "Root@": (0, -0.3, 0.2), "Pivot": (20, 8, 0),
                                                 "Head": (16, 0, 0), "Tail1": (0, 18, 0)})
    dig2 = M(mirror(dig1), {"Root@": (0, -0.7, 0.35), "Pivot": (28, -8, 0)})
    dive = M(gills(0, 0, -22), {"Root@": (0, -2.3, 0.8), "Pivot": (35, 0, 0), "Head": (18, 0, 0), "Tail1": (18, 0, 0),
                                "Tail2": (12, 0, 0), "UpperArmL": (0, -40, 30), "UpperArmR": (0, 40, -30)})
    b.clip("BurrowIn", [(0, {}), (8, dig1), (14, dig2), (20, M(dig1, {"Root@": (0, -1.0, 0.5), "Pivot": (32, 8, 0)})),
                        (26, M(dig2, {"Root@": (0, -1.5, 0.6), "Pivot": (34, -8, 0)})), (34, dive), (40, dive)],
           events={"under": 34})
    # Burst out: up out of its crystal, head high and gills wide, then a heavy landing and a shake.
    below = {"Root@": (0, -2.5, 0), "Pivot": (-30, 0, 0), "UpperArmL": (0, -40, 30), "UpperArmR": (0, 40, -30)}
    burst = M(gills(0, 0, 32), {"Root@": (0, 0.6, 0), "Pivot": (-26, 0, 0), "Chest": (-8, 0, 0), "Head": (-20, 0, 0),
                                "Jaw": (26, 0, 0), "Tail1": (16, 0, 0)})
    land = {"Root@": (0, -0.15, 0.1), "Pivot": (8, 0, 0), "Chest": (6, 0, 0), "Head": (8, 0, 0)}
    shake_a = M(land, gills(0, 0, 20), {"Head": (8, 14, 16)})
    shake_b = M(land, gills(0, 0, 20), {"Head": (8, -14, -16)})
    b.clip("BurrowOut", [(0, below), (8, burst), (12, burst), (20, land), (28, shake_a), (36, shake_b), (44, shake_a),
                         (62, {})], events={"erupt": 8, "recovered": 50})

    hit = M(gills(0, 0, -14), {"Root@": (0, 0, 0.3), "Pivot": (-8, 8, 6), "Chest": (-6, 10, 0), "Head": (-12, 14, 10)})
    b.clip("Stagger", [(0, {}), (6, hit), (22, M(hit, {"Head": (-6, 8, 6)})), (40, {})])
    dead = M(gills(0, 0, -30), {"Root@": (0, 0.2, 0), "Pivot": (0, 0, 80), "Head": (8, 16, 0), "Jaw": (18, 0, 0),
                                "UpperArmL": (0, 0, 40), "ThighL": (0, 0, 30), "Tail1": (0, 14, 0), "Tail2": (0, 10, 0)})
    b.clip("Death", [(0, {}), (8, hit), (22, M(hit, {"Root@": (0, 0.1, 0.4)})), (40, dead), (70, dead)])
    return b


def shots(b, folder):
    front, look = (-6.5, -10.0, 4.0), (0, -0.6, 1.2)
    side = (11.0, 0.4, 3.2)
    return b.previews(folder, [
        ("axolotl_front", None, 0, front, look),
        ("axolotl_side", None, 0, side, (0, 0.8, 1.0)),
        ("axolotl_back", None, 0, (5.0, 10.0, 5.0), (0, 1.0, 1.2)),
        ("axolotl_top", None, 0, (0.5, 0.6, 13.0), (0, 0.6, 0.5)),
        ("axolotl_roar", "Roar", 24, front, (0, -0.6, 1.6)),
        ("axolotl_walk", "Walk", 10, front, look),
        ("axolotl_swipe_coil", "TailSwipe", 22, (-8.0, 7.0, 5.0), look),
        ("axolotl_swipe_spin", "TailSwipe", 33, (-8.0, -7.0, 6.0), look),
        ("axolotl_ball", "RollStart", 24, side, (0, 0.2, 1.4)),
        ("axolotl_ball_front", "RollStart", 24, front, (0, 0.2, 1.4)),
        ("axolotl_crash", "RollCrash", 30, front, (0, -0.4, 0.8)),
        ("axolotl_arch", "ShardRing", 26, side, (0, 0.4, 1.6)),
        ("axolotl_dig", "BurrowIn", 22, side, (0, -0.4, 0.6)),
        ("axolotl_burst", "BurrowOut", 10, front, (0, -0.2, 2.0)),
        ("axolotl_stagger", "Stagger", 8, front, look),
        ("axolotl_death", "Death", 60, front, (0, 0, 0.8)),
    ])

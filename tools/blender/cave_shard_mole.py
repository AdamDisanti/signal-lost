# The Shard Mole: the Crystal Caves mini-boss (docs/shard-mole.md). A blind, hulking cave mole of dark slate fur
# with huge pink digging hands tipped in glowing cave-crystal claws, a pink nose, closed eyes, and a short tail ending
# in a crystal blade. The ship's antenna array is wired into its back: it hears through it. About 4.5 m long.
# Built with the same kit as the desert bosses (bosskit.py); run tools/blender/cave_bosses.py.
import math
from mathutils import Vector
from bosskit import Boss, srgb, sym, mirror, X, Y, Z


def build():
    b = Boss("ShardMole")
    b.material("Fur", srgb(48, 50, 66), rough=0.95)
    b.material("FurDark", srgb(34, 35, 48), rough=0.95)
    b.material("Belly", srgb(72, 74, 92), rough=0.95)
    b.material("Skin", srgb(214, 140, 132), rough=0.7)
    b.material("Nose", srgb(232, 128, 140), rough=0.45)
    b.material("Nail", srgb(222, 206, 160), rough=0.5)
    b.material("EyeSlit", srgb(26, 24, 30), rough=0.6)
    # The claws and the tail blade: the Unity builder swaps this for the cave's own crystal material (cyan).
    b.material("Crystal", srgb(60, 200, 230), rough=0.2, emit=srgb(51, 230, 255), strength=2.5)
    b.material("Metal", srgb(150, 150, 156), rough=0.4, metal=0.75)
    b.material("MetalDark", srgb(70, 72, 78), rough=0.55, metal=0.7)
    b.material("Signal", srgb(255, 90, 60), emit=srgb(255, 80, 50), strength=4.0)

    # ------------------------------------------------------------ skeleton (faces -Y, +X is its left)
    b.bone("Root", (0, 0, 0))
    b.bone("Pivot", (0, 0.2, 1.1), "Root")
    b.bone("Hips", (0, 0.8, 1.05), "Pivot")
    b.bone("Spine", (0, 0.0, 1.35), "Hips")
    b.bone("Chest", (0, -0.7, 1.55), "Spine")
    b.bone("Head", (0, -1.25, 1.6), "Chest")
    b.bone("Snout", (0, -1.75, 1.45), "Head")
    b.bone("Antenna", (0, 0.1, 2.3), "Spine")
    for side, s in (("L", 1), ("R", -1)):
        b.bone("UpperArm" + side, (0.8 * s, -0.85, 1.35), "Chest")
        b.bone("Forearm" + side, (1.25 * s, -1.05, 0.95), "UpperArm" + side)
        b.bone("Hand" + side, (1.45 * s, -1.35, 0.55), "Forearm" + side)
        b.bone("Thigh" + side, (0.55 * s, 0.75, 0.75), "Hips")
        b.bone("Foot" + side, (0.65 * s, 0.95, 0.18), "Thigh" + side)
    tail = [(0, 1.45, 0.9), (0, 1.95, 0.72), (0, 2.45, 0.58)]
    prev = "Hips"
    for i, p in enumerate(tail):
        prev = b.bone("Tail%d" % (i + 1), p, prev)
    b.bone("TailTip", (0, 2.85, 0.5), prev)
    b.bone("BladeTip", (0, 3.55, 0.56), "TailTip")   # socket: where the blade's point is (the swoop's hit)

    body_chain = ["Hips", "Spine", "Chest"]
    # ------------------------------------------------------------ body: a fat pear, darker on the back
    body = [(1.5, 0.95, 0.9, 0.75), (1.15, 1.02, 1.85, 1.45), (0.55, 1.12, 2.3, 1.85), (-0.1, 1.28, 2.36, 1.95),
            (-0.7, 1.45, 2.1, 1.75), (-1.15, 1.56, 1.55, 1.35)]

    def body_mat(c, k, i):
        if k < 0 or k >= len(body) - 1:
            return "Fur"
        zc = (body[k][1] + body[k + 1][1]) / 2
        hh = (body[k][3] + body[k + 1][3]) / 4
        if c.z < zc - hh * 0.5:
            return "Belly"
        return "FurDark" if c.z > zc + hh * 0.7 else "Fur"
    b.loft("Body", [((0, y, z), w, h) for y, z, w, h in body], X, Z, 12, 2.0, body_mat, jitter=0.05, chain=body_chain)
    # Fur: rough tufts along the back and flanks, so the low-poly body reads as shaggy.
    for i, y in enumerate([1.15, 0.8, 0.45, 0.1, -0.25, -0.6, -0.9]):
        for k, (dx, dz, tilt) in enumerate([(0.0, 0.95, 0), (0.45, 0.82, 0.6), (-0.45, 0.82, -0.6), (0.85, 0.45, 1.1),
                                           (-0.85, 0.45, -1.1)]):
            zc = 1.0 + 0.5 * (1.15 - y) / 2.05
            scale = 1.0 if abs(y) < 0.9 else 0.75
            base = (dx * scale, y + 0.04 * (k % 2), zc + dz * scale * 0.95)
            b.spike("Tuft%d_%d" % (i, k), base, (tilt * 0.5, 1.0, 0.3), 0.3, 0.15, "FurDark" if k % 2 else "Fur",
                    n=3, chain=body_chain)

    # ------------------------------------------------------------ head: no neck, a long tapering snout, closed eyes
    head = [((0, -1.0, 1.6), 1.35, 1.15), ((0, -1.35, 1.58), 1.08, 0.92), ((0, -1.62, 1.5), 0.72, 0.6),
            ((0, -1.85, 1.44), 0.42, 0.36), ((0, -1.98, 1.42), 0.26, 0.24)]
    b.loft("Skull", head[:3], X, Z, 10, 2.2, lambda c, k, i: "Belly" if c.z < 1.3 else "Fur", jitter=0.04, bone="Head")
    b.loft("SnoutM", head[2:], X, Z, 8, 2.2, "Fur", bone="Snout")
    b.blob("Nose", (0, -2.04, 1.43), (0.17, 0.11, 0.14), "Nose", bone="Snout")
    for s in (1, -1):
        b.blob("Nostril%d" % s, (0.06 * s, -2.14, 1.44), (0.035, 0.02, 0.04), "EyeSlit", bone="Snout")
        b.box("EyeSlit%d" % s, (0.36 * s, -1.42, 1.78), (0.16, 0.06, 0.025), "EyeSlit", rot=(0, math.radians(-14 * s), 0),
              bone="Head")
        for w in range(3):
            b.spike("Whisker%d%d" % (s, w), (0.14 * s, -1.9, 1.4 + 0.04 * w), (s, -0.25, -0.12 + 0.12 * w), 0.5, 0.012,
                    "Nail", n=3, bone="Snout")
    b.blob("Mouth", (0, -1.8, 1.28), (0.16, 0.08, 0.04), "EyeSlit", bone="Snout")
    for s in (1, -1):
        b.spike("Tooth%d" % s, (0.05 * s, -1.86, 1.27), (0, -0.2, -1), 0.09, 0.03, "Nail", n=3, bone="Snout")

    # ------------------------------------------------------------ the antenna array, wired into its back
    b.box("AntennaMount", (0, 0.1, 2.25), (0.55, 0.45, 0.18), "MetalDark", bone="Antenna")
    b.box("AntennaBase", (0, 0.1, 2.38), (0.32, 0.3, 0.14), "Metal", taper=0.8, bone="Antenna")
    dish_axis = Vector((0, 0.35, 1)).normalized()
    c0 = Vector((0, 0.15, 2.5))
    u = X
    v = dish_axis.cross(u)
    b.loft("Dish", [(c0, 0.12, 0.12, u, v), (c0 + dish_axis * 0.12, 0.5, 0.5, u, v), (c0 + dish_axis * 0.2, 0.82, 0.82, u, v)],
           u, v, 10, 2.0, "Metal", cap_start=True, cap_end=False, bone="Antenna")
    b.tube("DishFeed", c0 + dish_axis * 0.1, c0 + dish_axis * 0.55, 0.025, 0.02, "MetalDark", n=4, bone="Antenna")
    b.blob("DishLight", c0 + dish_axis * 0.57, (0.05, 0.05, 0.05), "Signal", subdiv=0, bone="Antenna")
    for k, (x, y, h) in enumerate([(0.2, -0.05, 0.85), (-0.2, -0.02, 0.7), (0.05, 0.3, 1.0)]):
        b.tube("Rod%d" % k, (x, y, 2.4), (x * 1.3, y + 0.05, 2.4 + h), 0.03, 0.015, "Metal", n=4, bone="Antenna")
        b.blob("RodLight%d" % k, (x * 1.3, y + 0.05, 2.42 + h), (0.045, 0.045, 0.045), "Signal", subdiv=0, bone="Antenna")
    for s in (1, -1):
        b.path("Cable%d" % s, [(0.22 * s, 0.2, 2.25), (0.5 * s, 0.35, 2.1), (0.75 * s, 0.55, 1.75)], 0.04, "MetalDark",
               n=4, chain=body_chain)

    # ------------------------------------------------------------ arms: short and thick, huge palm-out hands
    for side, s in (("L", 1), ("R", -1)):
        sh, el, wr = (0.8 * s, -0.85, 1.35), (1.25 * s, -1.05, 0.95), (1.45 * s, -1.35, 0.55)
        b.blob("Shoulder" + side, (0.85 * s, -0.85, 1.3), (0.45, 0.5, 0.5), "Fur", jitter=0.05, bone="UpperArm" + side)
        b.path("UpperArmM" + side, [sh, el], [0.36, 0.3], "Fur", n=7, bone="UpperArm" + side)
        b.path("ForearmM" + side, [el, wr], [0.3, 0.26], "Fur", n=7, bone="Forearm" + side)
        palm = Vector((1.6 * s, -1.55, 0.45))
        b.blob("Palm" + side, palm, (0.42, 0.18, 0.4), "Skin", rot=(math.radians(10), 0, math.radians(30 * s)),
               bone="Hand" + side)
        for f in range(5):
            a = -0.55 + 0.32 * f
            d = Vector((math.sin(a) * s, -math.cos(a), -0.35)).normalized()
            base = palm + Vector((d.x, d.y, 0)) * 0.3 + Vector((0, 0, -0.03))
            knuckle = base + d * 0.16
            b.tube("Finger%s%d" % (side, f), base, knuckle, 0.08, 0.07, "Skin", n=5, bone="Hand" + side)
            b.spike("Claw%s%d" % (side, f), knuckle, d, 0.62 if 0 < f < 4 else 0.48, 0.08, "Crystal", n=5,
                    bend=(0, 0, -0.18), bone="Hand" + side)
        # the thumb, on the inside
        thumb = palm + Vector((-0.25 * s, -0.1, 0.1))
        b.spike("Thumb" + side, thumb, (-0.5 * s, -0.7, 0.1), 0.38, 0.08, "Crystal", n=5, bone="Hand" + side)

        hip, ankle = (0.55 * s, 0.75, 0.75), (0.65 * s, 0.95, 0.18)
        b.blob("Haunch" + side, (0.6 * s, 0.85, 0.75), (0.45, 0.55, 0.45), "Fur", jitter=0.05, bone="Thigh" + side)
        b.path("Leg" + side, [hip, ankle], [0.25, 0.2], "Fur", n=6, bone="Thigh" + side)
        b.blob("FootM" + side, (0.68 * s, 0.75, 0.12), (0.24, 0.36, 0.11), "Skin", bone="Foot" + side)
        for t, dx in enumerate((-0.14, -0.05, 0.05, 0.14)):
            b.spike("Toe%s%d" % (side, t), (0.68 * s + dx, 0.44, 0.1), (dx, -1, -0.2), 0.16, 0.035, "Nail", n=3,
                    bone="Foot" + side)

    # ------------------------------------------------------------ tail: short and pink, a crystal blade at the end
    tchain = ["Hips", "Tail1", "Tail2", "Tail3", "TailTip"]
    b.path("Tail", [(0, 1.3, 0.95)] + tail + [(0, 2.85, 0.5)], [0.26, 0.2, 0.16, 0.13, 0.1], "Skin", n=6, chain=tchain)
    b.blob("BladeSocket", (0, 2.85, 0.5), (0.14, 0.14, 0.14), "Skin", bone="TailTip")
    b.spike("Blade", (0, 2.82, 0.5), (0, 1, 0.08), 0.75, 0.2, "Crystal", n=4, bone="TailTip")
    for s in (1, -1):
        b.spike("BladeFin%d" % s, (0.04 * s, 2.9, 0.52), (0.55 * s, 1, 0.35), 0.38, 0.09, "Crystal", n=4, bone="TailTip")
    return b


def animate(b):
    names = ["Root", "Pivot", "Hips", "Spine", "Chest", "Head", "Snout", "Antenna", "Tail1", "Tail2", "Tail3", "TailTip"]
    b.base = {k: (0, 0, 0) for k in names}
    b.base.update(sym({k + "L": (0, 0, 0) for k in ["UpperArm", "Forearm", "Hand", "Thigh", "Foot"]}))
    b.base["Root@"] = (0, 0, 0)

    def M(*poses):
        out = {}
        for q in poses:
            out.update(q)
        return out

    def tail(t, amp, lift=0.0):
        return {"Tail%d" % (i + 1): (lift + 2 * math.sin(t + i), amp * math.sin(t - i * 0.8), 0) for i in range(3)}

    def paddle(t, sweep, lift):
        """Mole gait: the hands paddle back alternately (+yaw swings a left hand back), the hind legs shuffle opposite."""
        out = {}
        for side, s, ph in (("L", 1, 0.0), ("R", -1, math.pi)):
            p = t + ph
            out["UpperArm" + side] = (-6 * math.sin(p), s * sweep * math.sin(p), s * lift * max(0.0, math.cos(p)))
            out["Hand" + side] = (0, s * -0.4 * sweep * math.sin(p), 0)
            out["Thigh" + side] = (sweep * 0.9 * math.sin(p + math.pi), 0, 0)
            out["Foot" + side] = (-sweep * 0.5 * math.sin(p + math.pi), 0, 0)
        return out

    keys = []
    for f in range(0, 61, 10):
        t = 2 * math.pi * f / 60
        pose = {"Chest": (1.5 * math.sin(t), 0, 0), "Head": (2 * math.sin(t + 1), 3 * math.sin(t * 0.5), 0),
                "Snout": (-4 * max(0.0, math.sin(t * 3)), 0, 0), "Antenna": (0, 10 * math.sin(t), 0),
                "Root@": (0, 0.03 * math.sin(t), 0)}
        pose.update(tail(t, 6))
        keys.append((f, pose))
    b.clip("Idle", keys, loop=True)

    keys = []
    for f in range(0, 41, 5):
        t = 2 * math.pi * f / 40
        pose = {"Root@": (0, 0.05 * abs(math.sin(2 * t)), 0), "Pivot": (0, 4 * math.sin(t), 3 * math.sin(t)),
                "Chest": (0, -4 * math.sin(t), 0), "Head": (0, 3 * math.sin(t), 0)}
        pose.update(paddle(t, 28, 16))
        pose.update(tail(t, 10))
        keys.append((f, pose))
    b.clip("Walk", keys, loop=True)

    keys = []
    for f in range(0, 25, 3):
        t = 2 * math.pi * f / 24
        pose = {"Root@": (0, -0.06 + 0.14 * abs(math.sin(t)), 0), "Pivot": (6 + 4 * math.sin(t), 0, 0),
                "Head": (-6, 0, 0), "Snout": (-6, 0, 0)}
        pose.update(paddle(t, 40, 24))
        pose.update(tail(t, 8, 6))
        keys.append((f, pose))
    b.clip("Run", keys, loop=True)

    # Roar: rears up with its arms flung wide (the reference's spread pose), snout up.
    spread = sym({"UpperArmL": (-15, -10, 28), "ForearmL": (0, 0, 12), "HandL": (-15, 0, 10)})
    rear = M(spread, {"Root@": (0, 0.35, 0.3), "Pivot": (-28, 0, 0), "Chest": (-8, 0, 0), "Head": (-14, 0, 0),
                      "Snout": (-18, 0, 0), "Tail1": (14, 0, 0), "Tail2": (8, 0, 0), "ThighL": (20, 0, 0),
                      "ThighR": (20, 0, 0), "FootL": (-20, 0, 0), "FootR": (-20, 0, 0)})
    roar = M(rear, {"Head": (-24, 0, 0), "Snout": (-26, 0, 0)})
    b.clip("Roar", [(0, {}), (14, rear), (22, roar), (36, M(roar, {"Head": (-24, 10, 6)})),
                    (50, M(roar, {"Head": (-22, -10, -6)})), (70, {})], events={"roar": 20})

    # Listen: frozen low, the head cocked one way, then the other, the snout twitching and the antenna dish sweeping.
    low = sym({"UpperArmL": (8, 10, -12), "HandL": (10, 0, 0)})
    low.update({"Root@": (0, -0.12, 0), "Pivot": (6, 0, 0), "Chest": (4, 0, 0)})
    cock_l = M(low, {"Head": (6, 18, 24), "Snout": (-4, 0, 0), "Antenna": (-10, 45, 0)})
    cock_r = M(low, {"Head": (6, -18, -24), "Snout": (-4, 0, 0), "Antenna": (-10, -45, 0)})
    sniff = lambda p: M(p, {"Snout": (-12, 0, 0)})
    b.clip("Listen", [(0, low), (14, cock_l), (26, sniff(cock_l)), (30, cock_l), (40, cock_l), (56, cock_r),
                      (68, sniff(cock_r)), (72, cock_r), (82, cock_r), (96, low)], loop=True)
    # Heard something: the head snaps up toward the sound.
    b.clip("Startle", [(0, low), (5, M(low, {"Root@": (0, 0.15, 0), "Head": (-14, 0, 0), "Snout": (-16, 0, 0),
                                              "Antenna": (-20, 0, 0)})), (20, {})], events={"heard": 5})

    # Claw swipe (right hand): draws it out and back, then rakes across the front to the left.
    raise_r = {"Root@": (-0.15, 0.1, -0.1), "Pivot": (-6, -10, -4), "Chest": (-4, -16, 0), "Head": (0, 10, 0),
               "UpperArmR": (-25, -45, -50), "ForearmR": (-20, 0, -10), "HandR": (-15, 0, 0)}
    strike_r = {"Root@": (0.1, 0, 0.45), "Pivot": (6, 14, 4), "Chest": (6, 22, 0), "Head": (4, -8, 0),
                "UpperArmR": (-45, 75, -12), "ForearmR": (-10, 20, 0), "HandR": (10, 15, 0)}
    b.clip("SwipeR", [(0, {}), (12, raise_r), (14, raise_r), (18, strike_r), (22, strike_r), (38, {})],
           events={"move": 12, "hit": 18})
    b.clip("SwipeL", [(0, {}), (12, mirror(raise_r)), (14, mirror(raise_r)), (18, mirror(strike_r)),
                      (22, mirror(strike_r)), (38, {})], events={"move": 12, "hit": 18})

    # Claw flurry (phase 2): right, left, then both hands raised high and HELD (the delay that catches an early
    # dodge) before a two-handed rake down the middle.
    both_up = M(rear, {"Root@": (0, 0.45, 0.1), "UpperArmL": (-70, -20, 40), "UpperArmR": (-70, 20, -40),
                       "ForearmL": (-40, 0, 0), "ForearmR": (-40, 0, 0)})
    both_down = {"Root@": (0, -0.2, 0.9), "Pivot": (16, 0, 0), "Chest": (8, 0, 0), "Head": (6, 0, 0),
                 "UpperArmL": (-30, 30, 10), "UpperArmR": (-30, -30, -10), "HandL": (20, 0, 0), "HandR": (20, 0, 0),
                 "Tail1": (-8, 0, 0)}
    b.clip("ClawFlurry", [(0, {}), (10, raise_r), (14, strike_r), (18, strike_r), (26, mirror(raise_r)),
                          (30, mirror(strike_r)), (34, mirror(strike_r)), (44, both_up),
                          (54, M(both_up, {"Root@": (0, 0.5, 0.05)})), (58, both_down), (64, both_down), (84, {})],
           events={"move": [10, 26, 54], "hit": [14, 30, 58]})

    # Tail swoop: crouches and coils with the blade raised (the warning), then spins right round so the crystal
    # blade scythes a full circle at shin height.
    coil = {"Root@": (0, -0.2, 0), "Pivot": (6, 40, 0), "Chest": (0, 20, 0), "Head": (0, 14, 0),
            "Tail1": (22, 18, 0), "Tail2": (16, 10, 0), "Tail3": (10, 6, 0), "TailTip": (6, 0, 0)}
    keys = [(0, {}), (16, coil), (22, M(coil, {"Pivot": (6, 48, 0)}))]
    for f, yaw in ((25, -40), (28, -130), (31, -220), (34, -300), (37, -350)):
        pose = {"Root@": (0, 0.1, 0), "Pivot": (0, yaw, 0), "Chest": (0, -12, 0), "Head": (0, -14, 0),
                "Tail1": (-6, -20, 0), "Tail2": (-4, -12, 0), "Tail3": (-2, -8, 0), "TailTip": (0, -4, 0)}
        pose.update(sym({"UpperArmL": (-20, 0, 30)}))
        keys.append((f, pose))
    keys += [(44, {"Pivot": (0, -360, 0), "Chest": (0, -6, 0)}), (62, {"Pivot": (0, -360, 0)})]
    b.clip("TailSwoop", keys, events={"hit_start": 24, "hit_end": 37})

    # Burrow: claws dig in turn and it dives snout-first into the floor.
    dig1 = M(sym({"UpperArmL": (-40, 10, -10)}), {"UpperArmL": (-50, 30, -5), "UpperArmR": (10, -20, 10),
                                                  "Root@": (0, -0.3, 0.2), "Pivot": (24, 0, 0), "Head": (14, 0, 0)})
    dig2 = M(mirror(dig1), {"Root@": (0, -0.6, 0.35), "Pivot": (32, 0, 0)})
    dive = {"Root@": (0, -2.4, 0.9), "Pivot": (45, 0, 0), "Head": (18, 0, 0), "Tail1": (20, 0, 0), "Tail2": (14, 0, 0),
            "UpperArmL": (-70, 0, 10), "UpperArmR": (-70, 0, -10)}
    b.clip("BurrowIn", [(0, {}), (8, dig1), (14, dig2), (20, M(dig1, {"Root@": (0, -0.9, 0.5), "Pivot": (36, 0, 0)})),
                        (26, M(dig2, {"Root@": (0, -1.4, 0.6), "Pivot": (40, 0, 0)})), (34, dive), (40, dive)],
           events={"under": 34})
    # Burst out: from below, up into the air with arms flung wide, then a heavy landing and a dazed head shake
    # (the opening).
    below = {"Root@": (0, -2.6, 0), "Pivot": (-30, 0, 0), "UpperArmL": (-80, 0, 10), "UpperArmR": (-80, 0, -10)}
    burst = M(spread, {"Root@": (0, 0.7, 0), "Pivot": (-30, 0, 0), "Head": (-20, 0, 0), "Snout": (-20, 0, 0),
                       "Tail1": (20, 0, 0)})
    land = {"Root@": (0, -0.2, 0.1), "Pivot": (12, 0, 0), "Chest": (6, 0, 0), "Head": (10, 0, 0),
            "UpperArmL": (-10, 20, -20), "UpperArmR": (-10, -20, 20)}
    daze_a = M(land, {"Head": (10, 16, 20), "Pivot": (8, 0, 5)})
    daze_b = M(land, {"Head": (10, -16, -20), "Pivot": (8, 0, -5)})
    b.clip("BurrowOut", [(0, below), (8, burst), (12, burst), (20, land), (30, daze_a), (40, daze_b), (50, daze_a),
                         (66, {})], events={"erupt": 8, "recovered": 56})

    # Ceiling shards (phase 3): rears high with both claws up, then slams them into the floor: the quake shakes
    # crystals loose from the ceiling.
    high = M(both_up, {"Root@": (0, 0.6, 0.0), "Pivot": (-34, 0, 0), "Head": (-20, 0, 0), "Snout": (-20, 0, 0)})
    slam = M(both_down, {"Root@": (0, -0.35, 0.6), "Pivot": (22, 0, 0), "HandL": (30, 0, 0), "HandR": (30, 0, 0)})
    b.clip("Quake", [(0, {}), (18, high), (30, M(high, {"Root@": (0, 0.7, -0.05)})), (36, slam), (44, slam),
                     (52, M(slam, {"Head": (0, 8, 0)})), (72, {})], events={"slam": 36})

    hit = {"Root@": (0, 0, 0.3), "Pivot": (-10, 8, 8), "Chest": (-6, 10, 0), "Head": (-14, 16, 12)}
    b.clip("Stagger", [(0, {}), (6, hit), (22, M(hit, {"Head": (-6, 8, 6)})), (40, {})])
    dead = {"Root@": (0, 0.1, 0), "Pivot": (0, 0, 85), "Head": (10, 20, 0), "UpperArmL": (-30, 0, 40),
            "UpperArmR": (-20, 0, 10), "ThighL": (-20, 0, 10), "Tail1": (0, 14, 0)}
    b.clip("Death", [(0, {}), (8, hit), (22, M(hit, {"Root@": (0, 0.1, 0.4)})), (40, dead), (70, dead)])
    return b


def shots(b, folder):
    front, look = (-5.0, -8.5, 3.2), (0, -0.4, 1.2)
    side = (8.5, -2.5, 2.8)
    return b.previews(folder, [
        ("mole_front", None, 0, front, look),
        ("mole_side", None, 0, (9.0, 0.3, 2.4), (0, 0.4, 1.0)),
        ("mole_back", None, 0, (4.0, 7.5, 3.5), (0, 0.5, 1.4)),
        ("mole_roar", "Roar", 24, front, (0, 0, 1.6)),
        ("mole_listen_l", "Listen", 30, front, look),
        ("mole_listen_r", "Listen", 72, front, look),
        ("mole_swipe_raise", "SwipeR", 13, front, look),
        ("mole_swipe_hit", "SwipeR", 19, front, look),
        ("mole_flurry_hold", "ClawFlurry", 50, side, (0, -0.5, 1.8)),
        ("mole_flurry_rake", "ClawFlurry", 59, side, look),
        ("mole_tail_coil", "TailSwoop", 20, (-6.0, 6.0, 4.0), look),
        ("mole_tail_spin", "TailSwoop", 30, (-6.0, -6.0, 5.0), look),
        ("mole_dig", "BurrowIn", 22, side, (0, -0.4, 0.6)),
        ("mole_burst", "BurrowOut", 10, front, (0, 0, 1.8)),
        ("mole_quake", "Quake", 38, side, look),
        ("mole_death", "Death", 60, front, (0, 0, 0.8)),
    ])

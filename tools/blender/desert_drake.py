# Cindermaw, the Ash Drake: a long, low, wingless ground dragon of charcoal plates with molten cracks, the ship's
# rocket thruster fused into its spine between the shoulders, a horned crest and a club tail. About 10 m long.
# See docs/desert-bosses.md.
import math
from mathutils import Vector
from bosskit import Boss, srgb, sym, mirror, X, Y, Z


def build():
    b = Boss("Cindermaw")
    b.material("Hide", srgb(52, 46, 46), rough=0.9)
    b.material("Plate", srgb(34, 31, 33), rough=0.75)
    b.material("Belly", srgb(96, 84, 78), rough=0.9)
    b.material("Ember", srgb(255, 120, 30), emit=srgb(255, 110, 25), strength=3.0)
    b.material("Horn", srgb(24, 22, 24), rough=0.35)
    b.material("Metal", srgb(120, 116, 112), rough=0.45, metal=0.75)
    b.material("MetalDark", srgb(66, 58, 54), rough=0.55, metal=0.7)
    b.material("Fire", srgb(255, 190, 80), emit=srgb(255, 150, 40), strength=5.0)
    b.material("Teeth", srgb(226, 214, 190), rough=0.5)
    b.material("Maw", srgb(90, 26, 18), rough=0.8)
    b.material("Eye", srgb(255, 170, 40), emit=srgb(255, 170, 40), strength=5.0)

    # ------------------------------------------------------------ skeleton
    b.bone("Root", (0, 0, 0))
    b.bone("Pivot", (0, 0.2, 1.9), "Root")
    b.bone("Hips", (0, 1.4, 1.9), "Pivot")
    b.bone("Spine", (0, 0.0, 2.15), "Hips")
    b.bone("Chest", (0, -1.3, 2.2), "Spine")
    b.bone("Neck1", (0, -2.1, 2.35), "Chest")
    b.bone("Neck2", (0, -2.75, 2.5), "Neck1")
    b.bone("Head", (0, -3.3, 2.6), "Neck2")
    b.bone("Jaw", (0, -3.35, 2.3), "Head")
    b.bone("Mouth", (0, -4.75, 2.35), "Head")          # socket: the flame breath
    b.bone("Nozzle", (0, 0.75, 3.95), "Spine")         # socket: the thruster's exhaust
    tail = [(0, 2.6, 1.85), (0, 3.6, 1.6), (0, 4.5, 1.32), (0, 5.3, 1.05)]
    prev = "Hips"
    for i, p in enumerate(tail):
        prev = b.bone("Tail%d" % (i + 1), p, prev)
    b.bone("Club", (0, 6.0, 0.88), prev)
    for side, s in (("L", 1), ("R", -1)):
        b.bone("UpperArm" + side, (1.0 * s, -1.4, 1.95), "Chest")
        b.bone("Forearm" + side, (1.35 * s, -1.55, 1.05), "UpperArm" + side)
        b.bone("Hand" + side, (1.4 * s, -1.75, 0.3), "Forearm" + side)
        b.bone("Thigh" + side, (1.0 * s, 1.4, 1.85), "Hips")
        b.bone("Shin" + side, (1.25 * s, 0.8, 0.95), "Thigh" + side)
        b.bone("Foot" + side, (1.25 * s, 1.35, 0.3), "Shin" + side)

    body_chain = ["Hips", "Spine", "Chest"]
    # ------------------------------------------------------------ body: dark plates over a molten core
    body = [(2.7, 1.85, 1.2, 1.1), (1.9, 1.95, 2.0, 1.6), (1.0, 2.1, 2.45, 1.85), (0.0, 2.2, 2.55, 1.95),
            (-0.9, 2.25, 2.45, 1.9), (-1.6, 2.3, 2.05, 1.7), (-2.15, 2.35, 1.35, 1.25)]
    centres = {round(y, 2): z for y, z, w, h in body}

    def body_mat(c, k, i):
        if k < 0 or k >= len(body) - 1:
            return "Hide"
        zc = (body[k][1] + body[k + 1][1]) / 2
        hh = (body[k][3] + body[k + 1][3]) / 4
        if c.z > zc + hh * 0.55:
            return "Ember"          # seen only through the gaps between the back plates
        return "Belly" if c.z < zc - hh * 0.55 else "Hide"
    b.loft("Body", [((0, y, z), w, h) for y, z, w, h in body], X, Z, 12, 2.0, body_mat, chain=body_chain)
    for i, y in enumerate([2.35, 1.8, 1.25, 0.7, 0.15, -0.4, -0.95, -1.5, -1.95]):
        # interpolate the body's top at y
        zc, hh, ww = 0, 0, 0
        for a, c in zip(body, body[1:]):
            if c[0] <= y <= a[0]:
                t = (a[0] - y) / (a[0] - c[0])
                zc, ww, hh = a[1] + (c[1] - a[1]) * t, a[2] + (c[2] - a[2]) * t, a[3] + (c[3] - a[3]) * t
        top = zc + hh / 2
        if -0.7 < y < 0.5:
            # the thruster sits here: plates only at the sides
            pass
        else:
            b.box("PlateC%d" % i, (0, y, top + 0.02), (ww * 0.38, 0.46, 0.24), "Plate", rot=(math.radians(-8), 0, 0),
                  taper=0.75, chain=body_chain)
            b.spike("Ridge%d" % i, (0, y + 0.05, top + 0.12), (0, 0.5, 1), 0.55 + 0.15 * (i % 2), 0.13, "Horn",
                    bend=(0, 0.25, 0), chain=body_chain)
        for s in (1, -1):
            b.box("PlateS%d%d" % (i, s), (ww * 0.3 * s, y, top - hh * 0.14), (ww * 0.34, 0.46, 0.22), "Plate",
                  rot=(math.radians(-8), math.radians(32 * s), 0), taper=0.8, chain=body_chain)
            b.box("PlateF%d%d" % (i, s), (ww * 0.47 * s, y, zc + hh * 0.08), (0.22, 0.48, hh * 0.42), "Plate",
                  rot=(0, math.radians(8 * s), 0), taper=0.85, chain=body_chain)

    # ------------------------------------------------------------ the thruster, fused between the shoulders
    axis = Vector((0, 0.72, 0.69)).normalized()
    base = Vector((0, -0.45, 2.9))
    u = X
    v = axis.cross(u)
    secs = [(base, 1.2, 1.2), (base + axis * 0.35, 1.0, 1.0), (base + axis * 0.75, 0.66, 0.66),
            (base + axis * 1.15, 1.05, 1.05), (base + axis * 1.6, 1.6, 1.6), (base + axis * 1.75, 1.66, 1.66)]
    b.loft("ThrusterBell", [(c, w, h, u, v) for c, w, h in secs], u, v, 12, 2.0,
           lambda c, k, i: "Fire" if k == len(secs) - 1 else ("MetalDark" if k in (1, 4) else "Metal"),
           cap_start=False, cap_end=True, chain=["Spine"])
    b.loft("ThrusterFlesh", [(base + axis * -0.2, 1.7, 1.5, u, v), (base + axis * 0.25, 1.4, 1.3, u, v),
                             (base + axis * 0.55, 0.85, 0.85, u, v)], u, v, 9, 2.0, "Hide", jitter=0.08,
           chain=["Spine"])
    for k in range(6):
        a = 2 * math.pi * k / 6 + 0.3
        d = u * math.cos(a) + v * math.sin(a)
        b.box("ThrusterFin%d" % k, base + axis * 1.1 + d * 0.5, (0.08, 0.5, 0.35), "MetalDark",
              rot=(math.atan2(axis.z, axis.y) - math.pi / 2, 0, a), chain=["Spine"])
    for s in (1, -1):
        b.path("FuelPipe%d" % s, [(0.45 * s, -0.4, 3.0), (0.9 * s, -0.7, 2.75), (1.15 * s, -1.1, 2.3)], 0.08, "Metal",
               n=5, chain=body_chain)
        b.box("Bracket%d" % s, (0.75 * s, -0.25, 3.0), (0.5, 0.3, 0.12), "MetalDark", rot=(0, math.radians(-30 * s), 0),
              chain=body_chain)
    b.blob("ExhaustGlow", base + axis * 1.6, (0.6, 0.6, 0.12), "Fire", rot=(math.atan2(axis.y, axis.z), 0, 0),
           chain=["Spine"])

    # ------------------------------------------------------------ neck and head
    neck_chain = ["Chest", "Neck1", "Neck2", "Head"]
    neck = [(0, -1.7, 2.33), (0, -2.15, 2.38), (0, -2.75, 2.52), (0, -3.2, 2.6)]
    b.path("Neck", neck, [(0.9, 0.82), (0.8, 0.72), (0.68, 0.62), (0.6, 0.56)], "Hide", n=9, power=2.2, chain=neck_chain)
    for i, (x, y, z) in enumerate(neck[1:]):
        b.box("NeckPlate%d" % i, (0, y, z + 0.55 - 0.05 * i), (0.6, 0.42, 0.2), "Plate", taper=0.7, chain=neck_chain)
        b.spike("NeckRidge%d" % i, (0, y + 0.05, z + 0.62 - 0.05 * i), (0, 0.6, 1), 0.45, 0.1, "Horn", chain=neck_chain)
    skull = [((0, -2.95, 2.68), 1.0, 0.9), ((0, -3.55, 2.7), 1.02, 0.76), ((0, -4.15, 2.6), 0.8, 0.54),
             ((0, -4.7, 2.5), 0.58, 0.38)]
    b.loft("Skull", skull, X, Z, 8, 2.8,
           lambda c, k, i: "Maw" if 0 <= k < len(skull) - 1 and c.z < skull[k][0][2] - 0.2 else "Hide", bone="Head")
    b.box("Brow", (0, -3.45, 3.03), (0.95, 0.75, 0.2), "Plate", rot=(math.radians(8), 0, 0), taper=0.7, bone="Head")
    b.box("Snout", (0, -4.15, 2.84), (0.62, 0.9, 0.16), "Plate", rot=(math.radians(6), 0, 0), taper=0.7, bone="Head")
    for s in (1, -1):
        b.spike("Horn%d" % s, (0.36 * s, -3.05, 3.0), (0.3 * s, 1, 0.45), 1.6, 0.2, "Horn", segments=2,
                bend=(0.1 * s, 0, -0.45), bone="Head")
        b.spike("HornB%d" % s, (0.5 * s, -3.2, 2.62), (0.7 * s, 1, 0.1), 1.0, 0.13, "Horn", segments=1, bone="Head")
        b.spike("HornC%d" % s, (0.2 * s, -2.9, 3.05), (0.1 * s, 1, 0.8), 0.7, 0.1, "Horn", bone="Head")
        b.blob("Eye%d" % s, (0.43 * s, -3.62, 2.86), (0.09, 0.16, 0.07), "Eye", bone="Head")
        for t in range(4):
            b.spike("Tooth%d%d" % (s, t), (0.3 * s - 0.04 * t * s, -3.9 - 0.2 * t, 2.42), (0, -0.1, -1), 0.17, 0.05,
                    "Teeth", n=3, bone="Head")
    b.spike("NoseHorn", (0, -4.45, 2.78), (0, -0.3, 1), 0.5, 0.11, "Horn", bone="Head")
    jaw = [((0, -3.1, 2.3), 0.9, 0.42), ((0, -3.9, 2.18), 0.8, 0.34), ((0, -4.6, 2.2), 0.52, 0.22)]
    b.loft("Jaw", jaw, X, Z, 7, 2.4,
           lambda c, k, i: "Maw" if 0 <= k < len(jaw) - 1 and c.z > jaw[k][0][2] + 0.08 else "Hide", bone="Jaw")
    for s in (1, -1):
        for t in range(3):
            b.spike("LowTooth%d%d" % (s, t), (0.26 * s - 0.04 * t * s, -3.85 - 0.22 * t, 2.32), (0, 0, 1), 0.14, 0.045,
                    "Teeth", n=3, bone="Jaw")
        b.spike("JawSpike%d" % s, (0.42 * s, -3.2, 2.15), (0.6 * s, 0.6, -0.4), 0.45, 0.08, "Horn", bone="Jaw")
    b.blob("MouthGlow", (0, -3.6, 2.42), (0.25, 0.5, 0.1), "Fire", bone="Head")

    # ------------------------------------------------------------ tail and club
    tchain = ["Hips", "Tail1", "Tail2", "Tail3", "Tail4", "Club"]
    tpts = [(0, 2.4, 1.88)] + tail + [(0, 5.9, 0.9)]
    b.path("Tail", tpts, [0.62, 0.56, 0.46, 0.37, 0.3, 0.26], "Hide", n=8, power=2.2, chain=tchain)
    for i in range(1, len(tpts) - 1):
        x, y, z = tpts[i]
        r = [0.56, 0.46, 0.37, 0.3][i - 1]
        b.box("TailPlate%d" % i, (0, y, z + r * 0.85), (r * 1.2, 0.5, 0.18), "Plate", taper=0.7, chain=tchain)
        b.spike("TailRidge%d" % i, (0, y, z + r * 0.95), (0, 0.6, 1), 0.4, 0.09, "Horn", chain=tchain)
    b.blob("ClubRock", (0, 6.05, 0.86), (0.62, 0.78, 0.52), "Plate", subdiv=1, jitter=0.12, bone="Club")
    b.blob("ClubCore", (0, 6.05, 0.86), (0.66, 0.6, 0.4), "Ember", subdiv=1, bone="Club")
    for k, d in enumerate([(1, 0.3, 0.3), (-1, 0.3, 0.3), (0.5, 0.4, 1), (-0.5, 0.4, 1), (0.7, 0.3, -0.6),
                           (-0.7, 0.3, -0.6), (0, 1, 0.2)]):
        b.spike("ClubSpike%d" % k, (0, 6.05, 0.86), d, 1.0 if k == 6 else 0.85, 0.15, "Horn", bone="Club")

    # ------------------------------------------------------------ legs
    for side, s in (("L", 1), ("R", -1)):
        sh, el, wr, toe = (1.0 * s, -1.4, 1.95), (1.35 * s, -1.55, 1.05), (1.4 * s, -1.75, 0.3), (1.45 * s, -2.2, 0.16)
        b.blob("Shoulder" + side, (1.0 * s, -1.35, 1.75), (0.6, 0.75, 0.75), "Hide", bone="UpperArm" + side)
        b.box("ShoulderPlate" + side, (1.15 * s, -1.35, 2.3), (0.6, 0.8, 0.22), "Plate",
              rot=(0, math.radians(30 * s), 0), taper=0.8, bone="UpperArm" + side)
        b.path("UpperArmM" + side, [sh, el], [0.46, 0.36], "Hide", n=7, bone="UpperArm" + side)
        b.path("ForearmM" + side, [el, wr], [0.34, 0.26], "Hide", n=7, bone="Forearm" + side)
        b.box("ForearmPlate" + side, (1.45 * s, -1.6, 0.75), (0.2, 0.5, 0.7), "Plate", taper=0.8, bone="Forearm" + side)
        b.path("HandM" + side, [wr, toe], [0.3, 0.24], "Hide", n=6, bone="Hand" + side)
        for t, dx in enumerate((-0.2, 0, 0.2)):
            b.spike("Claw%s%d" % (side, t), (toe[0] + dx * s, toe[1] - 0.05, 0.16), (dx * s, -1, -0.4), 0.45, 0.08,
                    "Horn", bend=(0, 0, -0.3), bone="Hand" + side)
        hip, knee, ankle, htoe = (1.0 * s, 1.4, 1.85), (1.25 * s, 0.8, 0.95), (1.25 * s, 1.35, 0.3), (1.3 * s, 0.85, 0.15)
        b.blob("Haunch" + side, (1.05 * s, 1.25, 1.6), (0.65, 0.95, 0.8), "Hide", rot=(math.radians(25), 0, 0),
               bone="Thigh" + side)
        b.box("HaunchPlate" + side, (1.2 * s, 1.25, 2.1), (0.6, 0.9, 0.22), "Plate", rot=(0, math.radians(30 * s), 0),
              taper=0.8, bone="Thigh" + side)
        b.path("ThighM" + side, [hip, knee], [0.46, 0.34], "Hide", n=7, bone="Thigh" + side)
        b.path("ShinM" + side, [knee, ankle], [0.3, 0.22], "Hide", n=7, bone="Shin" + side)
        b.path("FootM" + side, [ankle, htoe], [0.26, 0.24], "Hide", n=6, bone="Foot" + side)
        for t, dx in enumerate((-0.2, 0, 0.2)):
            b.spike("HClaw%s%d" % (side, t), (htoe[0] + dx * s, htoe[1] - 0.05, 0.15), (dx * s, -1, -0.4), 0.4, 0.08,
                    "Horn", bend=(0, 0, -0.3), bone="Foot" + side)
    return b


def animate(b):
    names = ["Root", "Pivot", "Hips", "Spine", "Chest", "Neck1", "Neck2", "Head", "Jaw", "Tail1", "Tail2", "Tail3",
             "Tail4", "Club"]
    b.base = {k: (0, 0, 0) for k in names}
    b.base.update(sym({k + "L": (0, 0, 0) for k in ["UpperArm", "Forearm", "Hand", "Thigh", "Shin", "Foot"]}))
    b.base["Root@"] = (0, 0, 0)

    def tail_wave(t, amp, pitch=1.5):
        return {"Tail%d" % (i + 1): (pitch * math.sin(t + i * 0.5), amp * math.sin(t - i * 0.7), 0) for i in range(4)}

    def legs(t, stride, lift, pair_offset=math.pi):
        """Diagonal gait: left front with right hind. Positive pitch tips the leg's lower end back."""
        out = {}
        for name, phase in (("L", 0.0), ("R", pair_offset)):
            fp = t + phase              # front leg on this side
            hp = t + phase + pair_offset  # hind leg on this side, opposite
            out["UpperArm" + name] = (-stride * math.sin(fp), 0, 0)
            out["Forearm" + name] = (lift * max(0, math.cos(fp)), 0, 0)
            out["Hand" + name] = (stride * 0.6 * math.sin(fp) - lift * 0.5 * max(0, math.cos(fp)), 0, 0)
            out["Thigh" + name] = (-stride * math.sin(hp), 0, 0)
            out["Shin" + name] = (-lift * max(0, math.cos(hp)), 0, 0)
            out["Foot" + name] = (stride * 0.6 * math.sin(hp) + lift * 0.4 * max(0, math.cos(hp)), 0, 0)
        return out

    keys = []
    for f in range(0, 61, 10):
        t = 2 * math.pi * f / 60
        pose = {"Chest": (1.2 * math.sin(t), 0, 0), "Neck1": (-2 * math.sin(t), 3 * math.sin(t), 0),
                "Head": (2 * math.sin(t + 1), -4 * math.sin(t), 0), "Jaw": (-2 - 2 * max(0, math.sin(t)), 0, 0),
                "Root@": (0, 0.03 * math.sin(t), 0)}
        pose.update(tail_wave(t, 5))
        keys.append((f, pose))
    b.clip("Idle", keys, loop=True)

    keys = []
    for f in range(0, 41, 5):
        t = 2 * math.pi * f / 40
        pose = {"Root@": (0, 0.06 * abs(math.sin(2 * t)), 0), "Hips": (0, 3 * math.sin(t), 2 * math.sin(t)),
                "Chest": (0, -3 * math.sin(t), -2 * math.sin(t)), "Neck1": (0, 3 * math.sin(t), 0),
                "Head": (0, -4 * math.sin(t), 0)}
        pose.update(legs(t, 20, 26))
        pose.update(tail_wave(t, 8))
        keys.append((f, pose))
    b.clip("Walk", keys, loop=True)

    keys = []
    for f in range(0, 19, 3):
        t = 2 * math.pi * f / 18
        pose = {"Root@": (0, -0.1 + 0.2 * abs(math.sin(t)), 0), "Pivot": (4 * math.sin(t), 0, 0), "Chest": (6, 0, 0),
                "Neck1": (10, 0, 0), "Head": (-10, 0, 0), "Jaw": (-8, 0, 0)}
        pose.update(legs(t, 34, 40, pair_offset=math.pi * 0.85))
        pose.update(tail_wave(t, 6, 4))
        keys.append((f, pose))
    b.clip("Run", keys, loop=True)

    rear = {"Root@": (0, 0.5, 0.6), "Pivot": (-24, 0, 0), "Chest": (-6, 0, 0), "Neck1": (6, 0, 0), "Head": (10, 0, 0),
            "ThighL": (26, 0, 0), "ThighR": (26, 0, 0), "ShinL": (-12, 0, 0), "ShinR": (-12, 0, 0),
            "FootL": (-14, 0, 0), "FootR": (-14, 0, 0),
            "UpperArmL": (-40, 0, 10), "UpperArmR": (-40, 0, -10), "ForearmL": (50, 0, 0), "ForearmR": (50, 0, 0),
            "Tail1": (16, 0, 0), "Tail2": (6, 0, 0)}
    roar = dict(rear)
    roar.update({"Neck1": (-12, 0, 0), "Neck2": (-12, 0, 0), "Head": (-16, 0, 0), "Jaw": (-40, 0, 0)})
    b.clip("Roar", [(0, {}), (14, rear), (22, roar), (40, dict(roar, Head=(-20, 8, 6))), (52, dict(roar, Head=(-18, -8, -6))),
                    (70, {})], events={"roar": 20})

    # Claw combo: right claw, left claw, then a lunging bite.
    def claw(side_pose):
        return side_pose
    raise_r = {"Root@": (0.25, 0.2, -0.1), "Pivot": (-8, 6, -6), "Chest": (-6, -8, 4), "Neck1": (0, 10, 0),
               "UpperArmR": (-60, 30, -25), "ForearmR": (70, 0, 0), "HandR": (20, 0, 0)}
    strike_r = {"Root@": (-0.2, 0, 0.8), "Pivot": (6, -10, 4), "Chest": (8, 12, -4), "Neck1": (6, -8, 0),
                "UpperArmR": (30, -30, -10), "ForearmR": (-10, 0, 0), "HandR": (-20, 0, 0), "Jaw": (-14, 0, 0)}
    raise_l, strike_l = mirror(raise_r), mirror(strike_r)
    biteback = {"Root@": (0, 0, -0.2), "Neck1": (-18, 0, 0), "Neck2": (-10, 0, 0), "Head": (12, 0, 0), "Jaw": (-30, 0, 0),
                "Pivot": (-4, 0, 0)}
    bite = {"Root@": (0, -0.1, 1.2), "Pivot": (8, 0, 0), "Neck1": (22, 0, 0), "Neck2": (10, 0, 0), "Head": (-4, 0, 0),
            "Jaw": (0, 0, 0), "UpperArmL": (-20, 0, 0), "UpperArmR": (-20, 0, 0)}
    b.clip("ClawCombo", [(0, {}), (11, raise_r), (16, strike_r), (21, dict(strike_r, Jaw=(-4, 0, 0))), (31, raise_l),
                         (36, strike_l), (42, dict(strike_l, Jaw=(-4, 0, 0))), (52, biteback), (58, bite), (62, bite),
                         (82, {})], events={"hit": [16, 36, 58], "move": [12, 32, 54]})

    # Tail club spin: winds one way, then whips all the way round.
    coil = {"Pivot": (0, 35, 0), "Chest": (0, 20, 0), "Neck1": (0, 18, 0), "Head": (0, 10, 0)}
    coil.update({"Tail%d" % i: (0, 14, 0) for i in range(1, 5)})
    keys = [(0, {}), (16, coil), (20, dict(coil, Pivot=(0, 42, 0)))]
    for f, yaw in ((24, -60), (28, -170), (32, -270), (36, -330)):
        pose = {"Pivot": (0, yaw, 0), "Chest": (0, -14, 0), "Neck1": (0, -16, 0), "Root@": (0, 0.15, 0)}
        pose.update({"Tail%d" % i: (-4, -12 - 3 * i, 0) for i in range(1, 5)})
        keys.append((f, pose))
    keys += [(42, {"Pivot": (0, -360, 0), "Chest": (0, -6, 0)}), (60, {"Pivot": (0, -360, 0)})]
    b.clip("TailSpin", keys, events={"hit_start": 22, "hit_end": 36})

    # Thruster lunge: crouches with the thruster pointing back (the wind-up holds the last pose), charges (a fast
    # loop), then skids out and shakes its head (the opening).
    crouch = {"Root@": (0, -0.45, 0.2), "Pivot": (10, 0, 0), "Chest": (6, 0, 0), "Neck1": (4, 0, 0), "Head": (-6, 0, 0),
              "Jaw": (-10, 0, 0), "ThighL": (26, 0, 0), "ThighR": (26, 0, 0), "ShinL": (-28, 0, 0), "ShinR": (-28, 0, 0),
              "FootL": (14, 0, 0), "FootR": (14, 0, 0), "UpperArmL": (-20, 0, 0), "UpperArmR": (-20, 0, 0),
              "ForearmL": (30, 0, 0), "ForearmR": (30, 0, 0)}
    crouch.update({"Tail%d" % i: (-6, 0, 0) for i in range(1, 5)})
    b.clip("LungeWindup", [(0, {}), (14, crouch), (24, dict(crouch, Pivot=(12, 0, 0))), (30, dict(crouch, Pivot=(11, 0, 0)))],
           events={})
    charge = {"Root@": (0, -0.2, 0), "Pivot": (8, 0, 0), "Neck1": (14, 0, 0), "Head": (-14, 0, 0), "Jaw": (-26, 0, 0),
              "UpperArmL": (-50, 0, 0), "UpperArmR": (-50, 0, 0), "ForearmL": (40, 0, 0), "ForearmR": (40, 0, 0),
              "ThighL": (40, 0, 0), "ThighR": (40, 0, 0), "ShinL": (-20, 0, 0), "ShinR": (-20, 0, 0)}
    charge.update({"Tail%d" % i: (-3, 0, 0) for i in range(1, 5)})
    b.clip("Charge", [(0, charge), (4, dict(charge, Pivot=(10, 0, 3))), (8, dict(charge, Pivot=(10, 0, -3))), (12, charge)],
           loop=True)
    skid = {"Root@": (0.3, -0.1, 0), "Pivot": (-6, -24, 12), "Chest": (0, 10, 0), "Neck1": (0, 24, 0), "Head": (0, 20, 14),
            "Jaw": (-20, 0, 0), "UpperArmL": (30, 0, 20), "ForearmL": (-20, 0, 0), "ThighR": (-30, 0, -10)}
    skid.update({"Tail%d" % i: (0, -14, 0) for i in range(1, 5)})
    dizzy_a = {"Neck1": (10, 14, 0), "Head": (14, 10, 18), "Jaw": (-14, 0, 0), "Pivot": (4, 0, 4)}
    dizzy_b = {"Neck1": (10, -14, 0), "Head": (14, -10, -18), "Jaw": (-14, 0, 0), "Pivot": (4, 0, -4)}
    b.clip("Skid", [(0, charge), (8, skid), (18, dict(skid, Pivot=(-2, -10, 4))), (30, dizzy_a), (44, dizzy_b),
                    (58, dizzy_a), (76, {})], events={"recovered": 66})

    # Flame breath: rears its head, draws breath (the mouth glows), then sweeps a cone of fire left to right.
    inhale = {"Root@": (0, 0.2, -0.3), "Pivot": (-8, 0, 0), "Chest": (-6, 0, 0), "Neck1": (-16, 30, 0), "Neck2": (-10, 10, 0),
              "Head": (-10, 10, 0), "Jaw": (-8, 0, 0)}
    blast = {"Root@": (0, 0, 0.1), "Pivot": (4, 0, 0), "Neck1": (10, 0, 0), "Neck2": (4, 0, 0), "Head": (-6, 0, 0),
             "Jaw": (-34, 0, 0)}
    b.clip("Breath", [(0, {}), (18, inhale), (24, dict(inhale, Neck1=(-18, 34, 0))), (30, dict(blast, Neck1=(10, 32, 0))),
                      (46, dict(blast, Neck1=(10, 0, 0))), (62, dict(blast, Neck1=(10, -32, 0))),
                      (68, dict(blast, Neck1=(8, -34, 0), Jaw=(-20, 0, 0))), (86, {})],
           events={"breath_start": 28, "breath_end": 66})

    # Smoke vent: arches its back so the thruster points up, and belches smoke.
    arch = {"Root@": (0, -0.2, 0), "Pivot": (10, 0, 0), "Spine": (-12, 0, 0), "Chest": (8, 0, 0), "Neck1": (16, 0, 0),
            "Head": (6, 0, 0), "Jaw": (-16, 0, 0)}
    b.clip("Vent", [(0, {}), (14, arch), (18, dict(arch, Spine=(-16, 0, 0))), (34, dict(arch, Spine=(-14, 0, 0))), (50, {})],
           events={"smoke": 16})

    # Rear slam: rises on its hind legs, hangs, and crashes down: the fire ring rolls out from here.
    high = dict(rear)
    high.update({"Root@": (0, 1.3, 0.3), "Pivot": (-38, 0, 0), "UpperArmL": (-70, 0, 20), "UpperArmR": (-70, 0, -20),
                 "ForearmL": (40, 0, 0), "ForearmR": (40, 0, 0), "Head": (14, 0, 0), "Jaw": (-30, 0, 0)})
    slam = {"Root@": (0, -0.4, 1.0), "Pivot": (10, 0, 0), "Chest": (8, 0, 0), "Neck1": (14, 0, 0), "Head": (4, 0, 0),
            "Jaw": (-20, 0, 0), "UpperArmL": (24, 0, 10), "UpperArmR": (24, 0, -10), "ForearmL": (-10, 0, 0),
            "ForearmR": (-10, 0, 0), "Tail1": (-10, 0, 0)}
    b.clip("RearSlam", [(0, {}), (18, high), (30, dict(high, **{"Root@": (0, 1.45, 0.25)})), (36, slam),
                        (44, dict(slam, **{"Root@": (0, -0.3, 1.0)})), (64, {})], events={"hit": 36, "move": 31})

    # Overheat: sagging, panting, venting (a loop).
    sag = {"Root@": (0, -0.4, 0), "Pivot": (6, 0, 0), "Neck1": (16, 0, 0), "Neck2": (10, 0, 0), "Head": (10, 0, 0),
           "Jaw": (-18, 0, 0), "ThighL": (14, 0, 0), "ThighR": (14, 0, 0), "ShinL": (-18, 0, 0), "ShinR": (-18, 0, 0)}
    b.clip("Overheat", [(0, sag), (10, dict(sag, Chest=(4, 0, 0), Jaw=(-26, 0, 0))), (20, sag)], loop=True)

    hit = {"Root@": (0, 0, -0.4), "Pivot": (-8, 6, 6), "Chest": (-8, 10, 0), "Neck1": (-16, 20, 0), "Head": (-8, 16, 10),
           "Jaw": (-24, 0, 0)}
    b.clip("Stagger", [(0, {}), (6, hit), (22, dict(hit, Neck1=(-8, 10, 0))), (40, {})])
    dead = {"Root@": (0, 0.2, 0), "Pivot": (0, 0, -80), "Neck1": (10, 26, 0), "Neck2": (10, 16, 0), "Head": (6, 14, 0),
            "Jaw": (-26, 0, 0), "UpperArmL": (-30, 0, 20), "ThighL": (-30, 0, 10)}
    dead.update({"Tail%d" % i: (0, 10, 0) for i in range(1, 5)})
    b.clip("Death", [(0, {}), (8, hit), (22, dict(hit, **{"Root@": (0, 0.2, -0.6)})), (40, dead), (70, dead)])

    # ---------- the overhaul (11-10, after Lawson's first look): more dynamic moves ----------
    def M(*poses):
        out = {}
        for q in poses:
            out.update(q)
        return out

    # Raking claws: right, left, then it rears with both claws high, holds (the delay) and crashes them down.
    both_up = M(rear, {"Root@": (0, 0.9, 0.2), "Pivot": (-30, 0, 0), "UpperArmL": (-80, 0, 20), "UpperArmR": (-80, 0, -20),
                       "ForearmL": (60, 0, 0), "ForearmR": (60, 0, 0), "Neck1": (-6, 0, 0), "Head": (10, 0, 0), "Jaw": (-34, 0, 0)})
    both_down = {"Root@": (0, -0.35, 1.3), "Pivot": (12, 0, 0), "Chest": (8, 0, 0), "Neck1": (16, 0, 0), "Head": (6, 0, 0),
                 "Jaw": (-18, 0, 0), "UpperArmL": (34, 0, 10), "UpperArmR": (34, 0, -10), "ForearmL": (-14, 0, 0),
                 "ForearmR": (-14, 0, 0), "HandL": (-10, 0, 0), "HandR": (-10, 0, 0), "Tail1": (-10, 0, 0)}
    b.clip("RakeCombo", [(0, {}), (11, raise_r), (16, strike_r), (21, strike_r), (30, raise_l), (36, strike_l), (41, strike_l),
                         (52, both_up), (60, M(both_up, {"Root@": (0, 1.05, 0.1)})), (66, both_down), (72, both_down),
                         (92, {})], events={"hit": [16, 36, 66], "move": [12, 32, 62]})

    # Thruster pounce: a deep crouch with the thruster lit, a rocket leap (the code flies it), claws out in the air,
    # and a crushing landing.
    pcrouch = M(crouch, {"Root@": (0, -0.6, -0.2), "Pivot": (14, 0, 0), "Head": (-12, 0, 0), "Jaw": (-16, 0, 0)})
    b.clip("PounceStart", [(0, {}), (12, pcrouch), (16, M(pcrouch, {"Root@": (0, -0.7, -0.3)})), (20, M(pcrouch, {"Pivot": (-10, 0, 0)}))],
           events={"launch": 17})
    flying = {"Pivot": (-12, 0, 0), "Neck1": (4, 0, 0), "Head": (-8, 0, 0), "Jaw": (-36, 0, 0),
              "UpperArmL": (-70, 0, 20), "UpperArmR": (-70, 0, -20), "ForearmL": (40, 0, 0), "ForearmR": (40, 0, 0),
              "ThighL": (-40, 0, 0), "ThighR": (-40, 0, 0), "ShinL": (30, 0, 0), "ShinR": (30, 0, 0)}
    flying.update({"Tail%d" % i: (-6, 0, 0) for i in range(1, 5)})
    b.clip("PounceAir", [(0, flying), (6, M(flying, {"Pivot": (-8, 0, 3)})), (12, M(flying, {"Pivot": (-8, 0, -3)})), (18, flying)],
           loop=True)
    landing = M(both_down, {"Root@": (0, -0.5, 0.2), "Pivot": (16, 0, 0)})
    b.clip("PounceLand", [(0, flying), (4, landing), (12, landing), (36, {})], events={"hit": 4})

    # Tail whip: lashes the club one way, then whips it back the other (two hits; jump them).
    w1 = {"Pivot": (0, -55, 0), "Chest": (0, 18, 0), "Neck1": (0, 16, 0)}
    w1.update({"Tail%d" % i: (-4, -24, 0) for i in range(1, 5)})
    w2 = {"Pivot": (0, 50, 0), "Chest": (0, -18, 0), "Neck1": (0, -16, 0)}
    w2.update({"Tail%d" % i: (-4, 24, 0) for i in range(1, 5)})
    wind = {"Pivot": (0, 25, 0), "Chest": (0, -10, 0)}
    wind.update({"Tail%d" % i: (0, 16, 0) for i in range(1, 5)})
    b.clip("TailWhip", [(0, {}), (10, wind), (18, w1), (24, M(w1, {"Pivot": (0, -62, 0)})), (32, M(w1, {"Pivot": (0, -45, 0)})),
                        (40, w2), (46, M(w2, {"Pivot": (0, 58, 0)})), (64, {})],
           events={"hit_start": [14, 34], "hit_end": [22, 44]})

    # Fire lob: rears its head and spits three molten globs in high arcs.
    lob_back = {"Root@": (0, 0.1, -0.2), "Pivot": (-6, 0, 0), "Neck1": (-24, 0, 0), "Neck2": (-14, 0, 0), "Head": (-10, 0, 0),
                "Jaw": (-10, 0, 0)}
    lob = {"Root@": (0, 0.1, 0.1), "Pivot": (-2, 0, 0), "Neck1": (-4, 0, 0), "Neck2": (-20, 0, 0), "Head": (-20, 0, 0),
           "Jaw": (-40, 0, 0)}
    b.clip("FireLob", [(0, {}), (14, lob_back), (20, lob), (26, M(lob_back, {"Neck1": (-24, 12, 0)})), (32, M(lob, {"Neck1": (-4, 12, 0)})),
                       (38, M(lob_back, {"Neck1": (-24, -12, 0)})), (44, M(lob, {"Neck1": (-4, -12, 0)})), (62, {})],
           events={"spit": [20, 32, 44]})

    # Afterburner dash: a thruster-kicked sidestep (the code moves it). DashL goes to its left.
    lean = {"Root@": (0.2, -0.15, 0), "Pivot": (0, 0, 16), "Chest": (0, 0, 6), "Neck1": (0, -14, 0), "Head": (0, -10, 0),
            "UpperArmL": (0, 0, 24), "ThighL": (0, 0, 20), "UpperArmR": (0, 0, -10), "ThighR": (0, 0, -10)}
    lean.update({"Tail%d" % i: (0, -10, 0) for i in range(1, 5)})
    seq = [(0, {}), (4, lean), (10, M(lean, {"Pivot": (0, 0, 10)})), (18, {})]
    b.clip("DashL", seq, events={"go": 3})
    b.clip("DashR", [(f, mirror(q)) for f, q in seq], events={"go": 3})

    # Skid sweep: the end of a charge, sliding sideways while the club tail scythes round.
    sweep = M(skid, {"Pivot": (-6, -60, 14)})
    sweep.update({"Tail%d" % i: (-2, -30, 0) for i in range(1, 5)})
    b.clip("SkidSweep", [(0, charge), (6, M(skid, {"Pivot": (-6, -20, 10)})), (14, sweep), (20, M(sweep, {"Pivot": (-4, -80, 8)})),
                         (36, dizzy_a), (52, {})], events={"hit_start": 6, "hit_end": 18})

    # Supernova: rears high as the thruster goes white-hot and shudders (a loop while it charges), then slams down
    # into a blast.
    nova = M(high, {"Root@": (0, 1.2, 0.2), "Pivot": (-36, 0, 0), "Spine": (-10, 0, 0), "Neck1": (-20, 0, 0),
                    "Head": (-24, 0, 0), "Jaw": (-44, 0, 0)})
    b.clip("SupernovaCharge", [(0, nova), (5, M(nova, {"Pivot": (-35, 2, 2)})), (10, M(nova, {"Pivot": (-37, -2, -2)})), (15, nova)],
           loop=True)
    b.clip("SupernovaBlast", [(0, nova), (6, M(slam, {"Root@": (0, -0.5, 0.4)})), (20, M(sag, {"Jaw": (-30, 0, 0)})), (40, sag)],
           events={"blast": 6})
    return b


def shots(b, folder):
    cam_front, look = (-9.0, -14.0, 6.0), (0, 0, 1.8)
    side = (14.0, -6.0, 4.5)
    return b.previews(folder, [
        ("drake_front", None, 0, cam_front, look),
        ("drake_side", None, 0, (16.0, 0.5, 4.0), (0, 1.0, 1.6)),
        ("drake_rake_up", "RakeCombo", 58, side, (0, -1, 2.5)),
        ("drake_rake_slam", "RakeCombo", 67, side, (0, -1, 1.5)),
        ("drake_pounce_crouch", "PounceStart", 15, side, look),
        ("drake_pounce_air", "PounceAir", 0, side, (0, 0, 2.2)),
        ("drake_pounce_land", "PounceLand", 6, side, look),
        ("drake_whip1", "TailWhip", 20, (-8.0, -14.0, 9.0), look),
        ("drake_whip2", "TailWhip", 42, (-8.0, -14.0, 9.0), look),
        ("drake_lob", "FireLob", 20, side, (0, -1, 2.5)),
        ("drake_dash", "DashL", 6, cam_front, look),
        ("drake_skidsweep", "SkidSweep", 16, (-8.0, -14.0, 9.0), look),
        ("drake_nova", "SupernovaCharge", 4, side, (0, 0, 3.0)),
        ("drake_breath", "Breath", 46, cam_front, look),
        ("drake_crouch", "LungeWindup", 28, side, look),
        ("drake_death", "Death", 60, cam_front, (0, 0, 1.0)),
    ])

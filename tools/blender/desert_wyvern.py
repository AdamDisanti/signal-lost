# Briarwing, the Thorn Wyvern: a long-necked, two-legged wyvern on its folded wing-arms, overgrown with thornwood,
# with the ship's hull panels lashed into its torn wings. About 14 m across the wings. See docs/desert-bosses.md.
import math
from mathutils import Vector
from bosskit import Boss, srgb, sym, mirror, X, Y, Z


def build():
    b = Boss("Briarwing")
    b.material("Hide", srgb(70, 54, 44), rough=0.85)
    b.material("Belly", srgb(132, 108, 80), rough=0.85)
    b.material("Scute", srgb(96, 104, 82), rough=0.8)
    b.material("Thorn", srgb(176, 182, 140), rough=0.6, emit=srgb(120, 255, 90), strength=0.0)
    b.material("Membrane", srgb(150, 92, 56), rough=0.9)
    b.material("MembraneDark", srgb(104, 62, 42), rough=0.9)
    b.material("Hull", srgb(176, 182, 188), rough=0.35, metal=0.7)
    b.material("Lash", srgb(46, 36, 28), rough=0.9)
    b.material("Claw", srgb(36, 32, 30), rough=0.5)
    b.material("Teeth", srgb(230, 220, 192), rough=0.5)
    b.material("Maw", srgb(70, 22, 26), rough=0.8)
    b.material("Eye", srgb(140, 255, 100), emit=srgb(140, 255, 100), strength=4.0)
    b.material("Glow", srgb(120, 255, 90), emit=srgb(120, 255, 90), strength=3.0)

    # ------------------------------------------------------------ skeleton
    b.bone("Root", (0, 0, 0))
    b.bone("Pivot", (0, 0.2, 2.8), "Root")          # spins and rolls turn about the body's middle
    b.bone("Hips", (0, 0.9, 2.7), "Pivot")
    b.bone("Spine", (0, -0.3, 2.95), "Hips")
    b.bone("Chest", (0, -1.3, 3.15), "Spine")
    b.bone("Neck1", (0, -1.9, 3.65), "Chest")
    b.bone("Neck2", (0, -2.4, 4.2), "Neck1")
    b.bone("Neck3", (0, -2.8, 4.75), "Neck2")
    b.bone("Head", (0, -3.05, 5.1), "Neck3")
    b.bone("Jaw", (0, -3.25, 4.87), "Head")
    b.bone("Mouth", (0, -4.65, 4.9), "Head")          # socket: thorns and roars come from here
    tail = [(0, 2.0, 2.65), (0, 3.2, 2.35), (0, 4.4, 2.0), (0, 5.6, 1.68), (0, 6.7, 1.45)]
    prev = "Hips"
    for i, p in enumerate(tail):
        prev = b.bone("Tail%d" % (i + 1), p, prev)
    for side, s in (("L", 1), ("R", -1)):
        b.bone("Thigh" + side, (0.85 * s, 1.0, 2.5), "Hips")
        b.bone("Shin" + side, (1.05 * s, 0.2, 1.45), "Thigh" + side)
        b.bone("Foot" + side, (1.05 * s, 1.0, 0.5), "Shin" + side)
        b.bone("WingArm" + side, (1.0 * s, -1.2, 3.25), "Chest")
        b.bone("WingFore" + side, (2.2 * s, -0.4, 3.7), "WingArm" + side)
        b.bone("WingHand" + side, (2.5 * s, -2.4, 0.45), "WingFore" + side)
        for f in (1, 2, 3):
            b.bone("Finger%d%s" % (f, side), (2.5 * s, -2.4, 0.45), "WingHand" + side)

    # ------------------------------------------------------------ body
    body = [(2.3, 2.62, 1.1, 1.1), (1.5, 2.72, 1.8, 1.7), (0.6, 2.86, 2.15, 2.0), (-0.4, 3.0, 2.15, 2.1),
            (-1.2, 3.12, 1.9, 2.05), (-1.75, 3.3, 1.4, 1.6), (-2.05, 3.5, 0.95, 1.1)]
    b.loft("Body", [((0, y, z), w, h) for y, z, w, h in body], X, Z, 10, 2.2,
           lambda c, k, i: "Belly" if c.z < 2.45 + 0.05 * (1 - c.y) else "Hide", chain=["Hips", "Spine", "Chest"])
    # scutes: overlapping plates down the back
    for i, y in enumerate([1.9, 1.3, 0.7, 0.1, -0.5, -1.1, -1.6]):
        zt = 2.62 + (2.0 - y) * 0.12 + 0.9
        b.box("Scute%d" % i, (0, y, zt), (0.9 - 0.04 * abs(i - 3), 0.7, 0.18), "Scute", rot=(math.radians(-12), 0, 0),
              taper=0.7, chain=["Hips", "Spine", "Chest"])
        b.spike("BackThorn%d" % i, (0, y, zt + 0.05), (0, 0.55, 1), 0.9 + 0.35 * (i % 2) + 0.15 * (3 - abs(i - 3)), 0.17,
                "Thorn", bend=(0, 0.35, 0), chain=["Hips", "Spine", "Chest"])
        for s in (1, -1):
            b.spike("FlankThorn%d%d" % (i, s), (0.62 * s, y + 0.1, zt - 0.45), (0.9 * s, 0.35, 0.6), 0.45, 0.09, "Thorn",
                    chain=["Hips", "Spine", "Chest"])

    # ------------------------------------------------------------ neck and head
    neck = [(0, -1.5, 3.25), (0, -1.95, 3.7), (0, -2.4, 4.22), (0, -2.8, 4.75), (0, -3.05, 5.08)]
    b.path("Neck", neck, [(0.9, 0.95), (0.78, 0.84), (0.66, 0.72), (0.58, 0.62), (0.52, 0.55)], "Hide", n=8, power=2.3,
           chain=["Chest", "Neck1", "Neck2", "Neck3", "Head"])
    for i, (x, y, z) in enumerate(neck[1:]):
        b.spike("NeckThorn%d" % i, (0, y + 0.1, z + 0.45 - 0.05 * i), (0, 0.8, 0.8), 0.75 - 0.08 * i, 0.12, "Thorn",
                bend=(0, 0.3, -0.1), chain=["Chest", "Neck1", "Neck2", "Neck3", "Head"])
        for s in (1, -1):
            b.spike("NeckSide%d%d" % (i, s), (0.36 * s, y + 0.05, z + 0.12), (0.8 * s, 0.5, 0.4), 0.32, 0.06, "Thorn",
                    chain=["Chest", "Neck1", "Neck2", "Neck3", "Head"])
    # skull: a long wedge with a heavy brow, scaled up about the head joint
    HS = 1.3
    HEAD = Vector((0, -3.05, 5.1))
    hp = lambda p: HEAD + (Vector(p) - HEAD) * HS
    skull = [((0, -2.85, 5.18), 0.78, 0.72), ((0, -3.2, 5.22), 0.84, 0.66), ((0, -3.65, 5.12), 0.7, 0.5),
             ((0, -4.1, 5.0), 0.52, 0.36), ((0, -4.5, 4.92), 0.34, 0.24)]
    b.loft("Skull", [(hp(c), w * HS, h * HS) for c, w, h in skull], X, Z, 8, 2.6,
           lambda c, k, i: "Maw" if 0 <= k < len(skull) - 1 and c.z < hp(skull[k][0]).z - 0.18 else "Hide", bone="Head")
    for s in (1, -1):
        b.box("Brow%d" % s, hp((0.34 * s, -3.4, 5.4)), (0.16, 0.62, 0.12), "Hide",
              rot=(math.radians(14), 0, math.radians(-16 * s)), taper=0.6, bone="Head")
        for t in range(3):
            b.spike("BrowThorn%d%d" % (s, t), hp((0.36 * s, -3.6 + 0.2 * t, 5.43)), (0.5 * s, 0.7, 0.7),
                    0.3 + 0.1 * t, 0.06, "Thorn", bone="Head")
        b.blob("Eye%d" % s, hp((0.37 * s, -3.5, 5.28)), (0.1, 0.16, 0.09), "Eye", bone="Head")
        b.spike("Horn%d" % s, hp((0.3 * s, -2.95, 5.45)), (0.35 * s, 1, 0.55), 2.0, 0.2, "Thorn", segments=2,
                bend=(0, 0.1, -0.5), bone="Head")
        b.spike("HornSmall%d" % s, hp((0.42 * s, -3.15, 5.35)), (1 * s, 0.6, 0.3), 0.8, 0.1, "Thorn", bone="Head")
        b.spike("CheekThorn%d" % s, hp((0.38 * s, -3.0, 4.98)), (1 * s, 0.7, -0.2), 0.65, 0.1, "Thorn", bone="Head")
        for t in range(4):   # upper teeth
            b.spike("Tooth%d%d" % (s, t), hp((0.24 * s - 0.04 * t * s, -3.75 - 0.18 * t, 4.95 - 0.02 * t)), (0, -0.1, -1),
                    0.16, 0.045, "Teeth", n=3, bone="Head")
    b.spike("NoseThorn", hp((0, -4.3, 5.03)), (0, -0.4, 1), 0.38, 0.07, "Thorn", bone="Head")
    jaw = [((0, -3.0, 4.86), 0.62, 0.32), ((0, -3.6, 4.8), 0.56, 0.26), ((0, -4.1, 4.78), 0.4, 0.2),
           ((0, -4.45, 4.78), 0.26, 0.14)]
    b.loft("Jaw", [(hp(c), w * HS, h * HS) for c, w, h in jaw], X, Z, 6, 2.2,
           lambda c, k, i: "Maw" if 0 <= k < len(jaw) - 1 and c.z > hp(jaw[k][0]).z + 0.08 else "Hide", bone="Jaw")
    for s in (1, -1):
        for t in range(3):
            b.spike("LowTooth%d%d" % (s, t), hp((0.2 * s - 0.04 * t * s, -3.7 - 0.2 * t, 4.88)), (0, 0, 1), 0.13, 0.04,
                    "Teeth", n=3, bone="Jaw")
        b.spike("ChinThorn%d" % s, hp((0.2 * s, -3.4, 4.68)), (0.4 * s, 0.3, -1), 0.35, 0.06, "Thorn", bone="Jaw")
    b.blob("ThroatGlow", hp((0, -3.35, 4.98)), (0.18, 0.35, 0.1), "Glow", bone="Head")

    # ------------------------------------------------------------ tail
    tpts = [(0, 1.9, 2.66)] + tail + [(0, 7.6, 1.3)]
    b.path("Tail", tpts, [0.62, 0.55, 0.45, 0.35, 0.26, 0.18, 0.12], "Hide", n=7, power=2.2,
           chain=["Hips", "Tail1", "Tail2", "Tail3", "Tail4", "Tail5"])
    tchain = ["Hips", "Tail1", "Tail2", "Tail3", "Tail4", "Tail5"]
    for i in range(1, len(tpts) - 1):
        x, y, z = tpts[i]
        r = [0.55, 0.45, 0.35, 0.26, 0.18][i - 1]
        b.spike("TailThorn%d" % i, (0, y, z + r * 0.9), (0, 0.7, 1), 0.7 - 0.08 * i, 0.12 - 0.012 * i, "Thorn",
                bend=(0, 0.3, 0), chain=tchain)
        for s in (1, -1):
            b.spike("TailSide%d%d" % (i, s), (r * 0.85 * s, y + 0.2, z + 0.1), (1 * s, 0.45, 0.3), 0.45 - 0.04 * i, 0.07,
                    "Thorn", chain=tchain)
    for k, d in enumerate([(0, 1, 0.2), (0.8, 0.6, 0.5), (-0.8, 0.6, 0.5), (0.7, 0.7, -0.4), (-0.7, 0.7, -0.4), (0, 0.5, 1)]):
        b.spike("TailClub%d" % k, (0, 7.45, 1.32), d, 0.9 if k == 0 else 0.6, 0.11, "Thorn", bone="Tail5")

    # ------------------------------------------------------------ legs and wings, each side
    for side, s in (("L", 1), ("R", -1)):
        hip, knee, ankle, toe = (0.85 * s, 1.0, 2.5), (1.05 * s, 0.2, 1.45), (1.05 * s, 1.0, 0.5), (1.08 * s, 0.55, 0.14)
        b.blob("Thigh" + side, (0.95 * s, 0.75, 2.15), (0.5, 0.85, 0.8), "Hide", rot=(math.radians(35), 0, 0),
               bone="Thigh" + side)
        b.path("ThighBone" + side, [hip, knee], [0.42, 0.3], "Hide", n=6, bone="Thigh" + side)
        b.path("Shin" + side, [knee, ankle], [0.27, 0.18], "Hide", n=6, bone="Shin" + side)
        b.spike("KneeThorn" + side, (1.08 * s, 0.05, 1.5), (0.2 * s, -1, 0.3), 0.5, 0.09, "Thorn", bone="Shin" + side)
        b.path("Foot" + side, [ankle, toe], [0.18, 0.2], "Hide", n=6, bone="Foot" + side)
        for t, dx in enumerate((-0.18, 0, 0.18)):
            b.spike("Talon%s%d" % (side, t), (toe[0] + dx * s, toe[1] - 0.05, 0.14), (dx * s, -1, -0.35), 0.45, 0.08,
                    "Claw", bend=(0, 0, -0.4), bone="Foot" + side)
        b.spike("Spur" + side, (ankle[0], ankle[1] + 0.1, 0.45), (0, 1, -0.3), 0.35, 0.06, "Claw", bone="Foot" + side)

        sh, el, wr = Vector((1.0 * s, -1.2, 3.25)), Vector((2.2 * s, -0.4, 3.7)), Vector((2.5 * s, -2.4, 0.45))
        b.path("WingArm" + side, [sh, el], [0.42, 0.28], "Hide", n=6, bone="WingArm" + side)
        b.path("WingFore" + side, [el, wr], [0.26, 0.16], "Hide", n=6, bone="WingFore" + side)
        b.blob("Wrist" + side, wr + Vector((0, 0, 0.1)), (0.24, 0.26, 0.22), "Hide", bone="WingHand" + side)
        b.spike("ElbowThorn" + side, el + Vector((0, 0.1, 0.2)), (0.3 * s, 0.6, 1), 0.9, 0.12, "Thorn",
                bend=(0, 0.4, 0), bone="WingFore" + side)
        b.spike("ThumbClaw" + side, wr + Vector((0, -0.15, 0.05)), (0.1 * s, -1, -0.25), 0.75, 0.1, "Claw",
                bend=(0, 0, -0.4), bone="WingHand" + side)
        tips = [Vector((3.0 * s, 0.4, 4.9)), Vector((3.9 * s, 2.3, 3.7)), Vector((3.3 * s, 3.6, 1.9))]
        for f, tip in enumerate(tips):
            name = "Finger%d%s" % (f + 1, side)
            b.path(name, [wr + Vector((0, 0, 0.15)), tip], [0.11, 0.035], "Hide", n=5, bone=name)
            for k in (0.35, 0.65):
                p = wr.lerp(tip, k)
                b.spike("FingerThorn%d%s%d" % (f, side, int(k * 10)), p, (0.4 * s, 0.6, 0.6), 0.35, 0.05, "Thorn", bone=name)
        # membrane: wrist, the three finger tips with a ragged trailing edge, and the flank
        hipside, chestside = Vector((0.85 * s, 1.2, 2.75)), Vector((1.0 * s, -0.9, 3.2))
        w, f1, f2, f3 = wr + Vector((0, 0, 0.2)), tips[0], tips[1], tips[2]
        m12 = f1.lerp(f2, 0.5).lerp(w, 0.28)
        m23 = f2.lerp(f3, 0.5).lerp(w, 0.3)
        m3h = f3.lerp(hipside, 0.5).lerp(w, 0.22)
        rag = [f1.lerp(m12, 0.55) + Vector((0, 0, -0.2)), f2.lerp(m23, 0.5) + Vector((0, 0.25, -0.15))]
        verts = [w, f1, m12, f2, m23, f3, m3h, hipside, chestside, el, rag[0], rag[1]]
        H, F1, F2, F3 = "WingHand" + side, "Finger1" + side, "Finger2" + side, "Finger3" + side
        weights = [[(H, 1)], [(F1, 1)], [(F1, 0.5), (F2, 0.5)], [(F2, 1)], [(F2, 0.5), (F3, 0.5)], [(F3, 1)],
                   [(F3, 0.5), ("Hips", 0.5)], [("Hips", 1)], [("Chest", 1)], [("WingFore" + side, 1)],
                   [(F1, 0.75), (F2, 0.25)], [(F2, 0.75), (F3, 0.25)]]
        faces = [(0, 1, 10), (0, 10, 2), (0, 2, 3), (0, 3, 11), (0, 11, 4), (0, 4, 5), (0, 5, 6), (0, 6, 7), (0, 7, 8),
                 (0, 8, 9), (0, 9, 1)]
        if s < 0:
            faces = [tuple(reversed(f)) for f in faces]
        b.sheet("Membrane" + side, verts, faces, "Membrane", weights)
        # the ship's hull panels lashed into the wing
        for k, (u, v, size) in enumerate([(0.35, 0.3, (0.9, 0.06, 1.3)), (0.55, 0.45, (1.1, 0.06, 0.8))]):
            centre = w.lerp(f1.lerp(f2, v), u + 0.1) + Vector((0, 0, 0))
            normal = (f1 - w).cross(f2 - w).normalized()
            off = normal * 0.05
            yaw = math.atan2((f2 - f1).y, (f2 - f1).x)
            mixw = [(H, 0.4), (F1, 0.3), (F2, 0.3)]
            b.box("Panel%s%d" % (side, k), centre + off, size, "Hull", rot=(0, math.radians(-30 * s), yaw), mix=mixw)
            b.box("Lash%s%d" % (side, k), centre + off * 1.6, (size[0] * 1.1, 0.08, 0.12), "Lash",
                  rot=(0, math.radians(-30 * s), yaw + 0.5), mix=mixw)
    return b


# ------------------------------------------------------------ animation
STANCE = {}


def animate(b):
    b.base = sym({"ThighL": (0, 0, 0), "ShinL": (0, 0, 0), "FootL": (0, 0, 0), "WingArmL": (0, 0, 0),
                  "WingForeL": (0, 0, 0), "WingHandL": (0, 0, 0), "Finger1L": (0, 0, 0), "Finger2L": (0, 0, 0),
                  "Finger3L": (0, 0, 0)})
    b.base.update({k: (0, 0, 0) for k in ["Hips", "Spine", "Chest", "Neck1", "Neck2", "Neck3", "Head", "Jaw",
                                          "Tail1", "Tail2", "Tail3", "Tail4", "Tail5", "Root", "Pivot"]})
    b.base["Root@"] = (0, 0, 0)

    def tail_wave(t, amp, phase=0.0):
        return {"Tail%d" % (i + 1): (2 * math.sin(t + i * 0.5 + phase), amp * math.sin(t + phase - i * 0.6), 0) for i in range(5)}

    # Idle: breathing, a slow neck sway, tail swaying.
    keys = []
    for f in range(0, 61, 10):
        t = 2 * math.pi * f / 60
        pose = {"Chest": (1.5 * math.sin(t), 0, 0), "Neck1": (-2 * math.sin(t), 3 * math.sin(t / 1), 0),
                "Neck2": (2 * math.sin(t + 0.6), 2 * math.sin(t + 0.4), 0), "Head": (2 * math.sin(t + 1), -3 * math.sin(t), 0),
                "Jaw": (-3 - 3 * max(0, math.sin(t)), 0, 0), "Root@": (0, 0.03 * math.sin(t), 0)}
        pose.update(tail_wave(t, 6))
        pose.update(sym({"Finger2L": (0, 2 * math.sin(t), 0)}))
        keys.append((f, pose))
    b.clip("Idle", keys, loop=True)

    # Walk: a heavy knuckle-walk. Feet and wing-hands alternate (left foot with right hand). One cycle = 40 frames.
    keys = []
    for f in range(0, 41, 5):
        t = 2 * math.pi * f / 40
        st, ct = math.sin(t), math.cos(t)
        pose = {"Root@": (0.06 * st, 0.08 * abs(math.sin(t)), 0), "Hips": (0, 3 * st, 4 * st),
                "Chest": (0, -4 * st, -3 * st), "Neck1": (2 * abs(st), 4 * st, 0), "Head": (-2 * abs(st), -5 * st, 0),
                "ThighL": (-24 * st, 0, 0), "ShinL": (10 * max(0, ct), 0, 0), "FootL": (18 * st - 8 * max(0, ct), 0, 0),
                "ThighR": (24 * st, 0, 0), "ShinR": (10 * max(0, -ct), 0, 0), "FootR": (-18 * st - 8 * max(0, -ct), 0, 0),
                "WingArmL": (0, 0, 0), "WingForeL": (16 * st, 0, 6 * max(0, ct)), "WingHandL": (-16 * st, 0, 0),
                "WingForeR": (-16 * st, 0, -6 * max(0, -ct)), "WingHandR": (16 * st, 0, 0)}
        pose.update(tail_wave(t, 9))
        keys.append((f, pose))
    b.clip("Walk", keys, loop=True)

    # Run: the same gait, faster and lower (24 frames).
    keys = []
    for f in range(0, 25, 3):
        t = 2 * math.pi * f / 24
        st, ct = math.sin(t), math.cos(t)
        pose = {"Root@": (0, -0.15 + 0.18 * abs(math.sin(t)), 0), "Hips": (4, 4 * st, 5 * st), "Chest": (6, -5 * st, -4 * st),
                "Neck1": (12, 5 * st, 0), "Neck2": (-4, 0, 0), "Head": (-8, -4 * st, 0),
                "ThighL": (-36 * st, 0, 0), "ShinL": (18 * max(0, ct), 0, 0), "FootL": (26 * st, 0, 0),
                "ThighR": (36 * st, 0, 0), "ShinR": (18 * max(0, -ct), 0, 0), "FootR": (-26 * st, 0, 0),
                "WingForeL": (26 * st, 0, 8 * max(0, ct)), "WingHandL": (-24 * st, 0, 0),
                "WingForeR": (-26 * st, 0, -8 * max(0, -ct)), "WingHandR": (24 * st, 0, 0)}
        pose.update(tail_wave(t, 12))
        keys.append((f, pose))
    b.clip("Run", keys, loop=True)

    spread = sym({"WingArmL": (-10, 10, 55), "WingForeL": (-20, 0, 40), "WingHandL": (40, 0, 30),
                  "Finger1L": (10, 0, -5), "Finger2L": (0, 15, 0), "Finger3L": (-10, 25, 5)})
    rear = {"Root@": (0, 0.5, 0.5), "Hips": (-18, 0, 0), "Spine": (-8, 0, 0), "Chest": (-6, 0, 0),
            "Neck1": (14, 0, 0), "Neck2": (6, 0, 0), "Head": (12, 0, 0), "Tail1": (18, 0, 0), "Tail2": (6, 0, 0),
            "ThighL": (26, 0, 0), "ThighR": (26, 0, 0), "ShinL": (-10, 0, 0), "ShinR": (-10, 0, 0),
            "FootL": (-14, 0, 0), "FootR": (-14, 0, 0)}

    # Roar: rears up, wings flared, head thrown up, jaw wide.
    roar = dict(rear, **spread)
    roar.update({"Neck1": (-10, 0, 0), "Neck2": (-14, 0, 0), "Neck3": (-10, 0, 0), "Head": (-18, 0, 0), "Jaw": (-38, 0, 0)})
    b.clip("Roar", [(0, {}), (14, rear), (22, roar), (40, dict(roar, Head=(-22, 6, 4))), (50, dict(roar, Head=(-20, -6, -4))),
                    (66, {})], events={"roar": 20})

    # Swipe (left wing-claw): rears onto the right side, raises the left arm, sweeps it across in front.
    wind = {"Root@": (-0.3, 0.3, 0), "Hips": (-8, 10, -6), "Chest": (-6, 12, -10), "Neck1": (0, -14, 0), "Head": (0, -10, 0),
            "WingArmL": (-50, 20, 40), "WingForeL": (-40, 0, 10), "WingHandL": (-30, 0, 0), "Finger1L": (0, 0, -10)}
    strike = {"Root@": (0.4, 0.05, 0.9), "Hips": (2, -14, 6), "Chest": (8, -22, 10), "Neck1": (8, 10, 0), "Head": (4, 10, 0),
              "WingArmL": (20, -50, 10), "WingForeL": (20, -20, -10), "WingHandL": (30, 0, 0), "Jaw": (-15, 0, 0)}
    b.clip("SwipeL", [(0, {}), (10, wind), (13, dict(wind, WingArmL=(-56, 24, 44))), (17, strike), (22, dict(strike, Jaw=(-5, 0, 0))),
                      (34, {})], events={"hit": 16, "move": 13})
    b.clip("SwipeR", [(f, mirror(p)) for f, p in [(0, {}), (10, wind), (13, dict(wind, WingArmL=(-56, 24, 44))), (17, strike),
                                                   (22, dict(strike, Jaw=(-5, 0, 0))), (34, {})]], events={"hit": 16, "move": 13})

    # Slam (the third, delayed hit): rears high with both arms up, hangs there, then crashes both down.
    up = dict(rear)
    up.update(sym({"WingArmL": (-70, 10, 30), "WingForeL": (-50, 0, 10), "WingHandL": (-30, 0, 0)}))
    up.update({"Neck1": (-6, 0, 0), "Head": (-10, 0, 0), "Jaw": (-25, 0, 0), "Root@": (0, 1.0, 0.4)})
    down = {"Root@": (0, -0.35, 1.4), "Hips": (16, 0, 0), "Spine": (6, 0, 0), "Chest": (10, 0, 0), "Neck1": (18, 0, 0),
            "Head": (10, 0, 0), "Jaw": (-10, 0, 0), "Tail1": (-14, 0, 0), "Tail2": (-6, 0, 0)}
    down.update(sym({"WingArmL": (30, 0, 0), "WingForeL": (30, 0, 0), "WingHandL": (20, 0, 0), "ThighL": (-20, 0, 0),
                     "ShinL": (20, 0, 0)}))
    b.clip("Slam", [(0, {}), (14, up), (26, dict(up, **{"Root@": (0, 1.15, 0.3)})),
                    (31, down), (40, dict(down, **{"Root@": (0, -0.25, 1.4)})), (56, {})], events={"hit": 31, "move": 27})

    # Tail lash: turns its body left, coils the tail, then whips it round through ~220 degrees behind it.
    coil = {"Root": (0, -30, 0), "Hips": (0, -10, 0), "Chest": (0, 25, 0), "Neck1": (0, 20, 0), "Head": (0, 10, 0)}
    coil.update({"Tail%d" % i: (0, 18, 0) for i in range(1, 6)})
    whip = {"Root": (0, 70, 0), "Hips": (0, 25, -6), "Chest": (0, -30, 4), "Neck1": (0, -20, 0), "Head": (0, -10, 0)}
    whip.update({"Tail%d" % i: (2, -22 - 4 * i, 0) for i in range(1, 6)})
    follow = dict(whip)
    follow.update({"Root": (0, 100, 0)})
    follow.update({"Tail%d" % i: (0, -10, 0) for i in range(1, 6)})
    b.clip("TailLash", [(0, {}), (14, coil), (18, dict(coil, Root=(0, -36, 0))), (25, whip), (30, follow), (52, {})],
           events={"hit_start": 20, "hit_end": 28})

    # Lunge: pulls its head back and down, then shoots forward with a snapping bite.
    pull = {"Root@": (0, -0.2, -0.4), "Hips": (6, 0, 0), "Chest": (-8, 0, 0), "Neck1": (-20, 0, 0), "Neck2": (-18, 0, 0),
            "Neck3": (10, 0, 0), "Head": (14, 0, 0), "Jaw": (-10, 0, 0),
            "ThighL": (14, 0, 0), "ThighR": (14, 0, 0), "ShinL": (-10, 0, 0), "ShinR": (-10, 0, 0)}
    shoot = {"Root@": (0, 0.1, 0.6), "Hips": (-4, 0, 0), "Chest": (10, 0, 0), "Neck1": (24, 0, 0), "Neck2": (18, 0, 0),
             "Neck3": (8, 0, 0), "Head": (-6, 0, 0), "Jaw": (-40, 0, 0), "ThighL": (-30, 0, 0), "ThighR": (-24, 0, 0)}
    shoot.update(sym({"WingForeL": (20, 0, 0), "WingHandL": (-20, 0, 0)}))
    bite = dict(shoot, Jaw=(0, 0, 0), Head=(6, 0, 0))
    b.clip("Lunge", [(0, {}), (16, pull), (19, dict(pull, Neck1=(-24, 0, 0))), (24, shoot), (27, bite), (34, bite),
                     (50, {})], events={"move_start": 19, "move_end": 27, "hit": 27})

    # Barrage: rears up with wings spread, the thorns flare, then three volleys snapped out of its spread wings.
    flare = dict(rear, **spread)
    flare.update({"Neck1": (-4, 0, 0), "Head": (6, 0, 0), "Jaw": (-30, 0, 0)})
    snap = dict(flare)
    snap.update(sym({"WingArmL": (-4, 18, 60), "WingForeL": (-10, 0, 50), "Finger2L": (0, 25, 0)}))
    snap.update({"Chest": (2, 0, 0), "Head": (14, 0, 0), "Jaw": (-40, 0, 0)})
    keys = [(0, {}), (20, flare), (30, flare)]
    for v in (36, 54, 72):
        keys += [(v - 4, dict(flare, Chest=(-10, 0, 0))), (v, snap), (v + 8, flare)]
    keys += [(96, {})]
    b.clip("Barrage", keys, events={"volley": [36, 54, 72], "flare": 18})

    # Hop: crouches and springs up (the code lifts it); Glide: wings out in the air; SpinDive: a corkscrew with wings
    # wrapped tight; SpinRecover: lands tangled, thrashes and rights itself.
    crouch = {"Root@": (0, -0.6, 0), "Hips": (14, 0, 0), "Chest": (10, 0, 0), "Neck1": (10, 0, 0),
              "ThighL": (30, 0, 0), "ThighR": (30, 0, 0), "ShinL": (-34, 0, 0), "ShinR": (-34, 0, 0),
              "FootL": (20, 0, 0), "FootR": (20, 0, 0)}
    crouch.update(sym({"WingArmL": (-20, 10, 40), "WingForeL": (-20, 0, 30)}))
    air = dict(spread)
    air.update({"Hips": (-4, 0, 0), "ThighL": (-30, 0, 0), "ThighR": (-30, 0, 0), "ShinL": (40, 0, 0), "ShinR": (40, 0, 0),
                "Tail1": (-8, 0, 0), "Tail2": (-6, 0, 0)})
    b.clip("Hop", [(0, {}), (10, crouch), (14, dict(crouch, **{"Root@": (0, -0.7, 0)})), (18, air), (24, air)],
           events={"launch": 15})
    flap_up = dict(air)
    flap_up.update(sym({"WingArmL": (-20, 10, 80), "WingForeL": (-20, 0, 50), "Finger2L": (0, 20, 0)}))
    flap_down = dict(air)
    flap_down.update(sym({"WingArmL": (10, 10, 10), "WingForeL": (0, 0, 20), "Finger2L": (0, 30, 0)}))
    b.clip("Hover", [(0, flap_up), (9, flap_down), (18, flap_up)], loop=True, events={"flap": 9})
    tuck = {"Neck1": (24, 0, 0), "Neck2": (10, 0, 0), "Head": (6, 0, 0), "Jaw": (-25, 0, 0),
            "ThighL": (-40, 0, 0), "ThighR": (-40, 0, 0), "ShinL": (50, 0, 0), "ShinR": (50, 0, 0)}
    tuck.update(sym({"WingArmL": (-30, -10, -20), "WingForeL": (-10, 0, -10), "Finger1L": (0, -10, 0),
                     "Finger2L": (0, -20, 0), "Finger3L": (0, -25, 0)}))
    tuck.update({"Tail%d" % i: (-4, 0, 0) for i in range(1, 6)})
    keys = []
    for k in range(7):
        keys.append((k * 5, dict(tuck, Pivot=(0, 0, 120 * k))))
    b.clip("SpinDive", keys, loop=True)
    tangled = {"Root@": (0.3, -0.4, 0), "Pivot": (0, 0, 24), "Hips": (0, 0, 10), "Neck1": (30, 20, 0), "Neck2": (14, 0, 0),
               "Head": (10, 0, 20), "Jaw": (-20, 0, 0), "ThighL": (-20, 0, 0), "ShinL": (30, 0, 0)}
    tangled.update(sym({"WingArmL": (10, -20, 30), "WingForeL": (20, 0, 20)}))
    thrash = dict(tangled, Pivot=(0, 0, -12), Head=(-10, -20, -10), Neck1=(10, -20, 0))
    b.clip("SpinRecover", [(0, tangled), (16, thrash), (30, tangled), (46, thrash), (62, dict(thrash, Pivot=(0, 0, 0))),
                           (80, {})], events={"recovered": 66})

    # Takeoff (for the dive bomb): two deep wing beats lifting it high; Plunge: wings folded, head down; Impact: buried
    # nose-first, stuck, then wrenches free.
    b.clip("Takeoff", [(0, {}), (10, crouch), (16, flap_up), (24, flap_down), (32, flap_up), (40, flap_down), (46, flap_up)],
           events={"launch": 15})
    plunge = dict(tuck)
    plunge.update({"Root": (60, 0, 0), "Neck1": (20, 0, 0), "Head": (10, 0, 0)})
    b.clip("Plunge", [(0, flap_up), (8, plunge), (14, plunge)], events={})
    stuck = {"Root@": (0, -0.9, 0.6), "Root": (28, 0, 0), "Neck1": (34, 0, 0), "Neck2": (24, 0, 0), "Head": (20, 0, 0),
             "Hips": (-10, 0, 0), "Tail1": (-20, 0, 0), "Tail2": (-10, 0, 0), "ThighL": (-30, 0, 0), "ThighR": (-30, 0, 0)}
    stuck.update(sym({"WingArmL": (10, -10, 60), "WingForeL": (10, 0, 50), "WingHandL": (30, 0, 0)}))
    pull_free = dict(stuck, Root=(12, 0, 0), Neck1=(10, 0, 0), Head=(-20, 0, 0), Jaw=(-30, 0, 0))
    b.clip("Impact", [(0, stuck), (20, dict(stuck, Root=(30, 0, 4))), (36, dict(stuck, Root=(30, 0, -4))), (56, pull_free),
                      (72, {})], events={"hit": 0, "free": 58})

    # Stagger: rocks back, head thrown aside. Death: crumples onto its side, wing over itself.
    hit = {"Root@": (0, 0, -0.5), "Hips": (-8, 0, 6), "Chest": (-14, 10, 8), "Neck1": (-20, 24, 0), "Head": (-10, 20, 10),
           "Jaw": (-25, 0, 0)}
    b.clip("Stagger", [(0, {}), (6, hit), (22, dict(hit, Neck1=(-10, 10, 0))), (40, {})])
    dead = {"Root@": (0, 0.45, 0), "Root": (0, 0, -78), "Hips": (0, 0, -6), "Neck1": (10, 30, 0), "Neck2": (14, 20, 0),
            "Head": (10, 18, 0), "Jaw": (-28, 0, 0)}
    dead.update({"Tail%d" % i: (0, 10, 0) for i in range(1, 6)})
    dead.update(sym({"WingArmL": (20, 0, 50), "WingForeL": (10, 0, 30)}))
    b.clip("Death", [(0, {}), (8, hit), (20, dict(hit, **{"Root@": (0, 0.2, -0.6)})), (40, dead), (70, dead)])
    return b


def shots(b, folder):
    return b.previews(folder, [
        ("wyvern_front", None, 0, (-9.0, -15.0, 6.0), (0, 0, 2.8)),
        ("wyvern_side", None, 0, (17.0, 0.5, 4.5), (0, 1.5, 2.6)),
        ("wyvern_back", None, 0, (-8.0, 14.0, 7.5), (0, 0, 2.8)),
        ("wyvern_face", None, 0, (-2.6, -8.5, 6.2), (0, -3.6, 5.0)),
        ("wyvern_roar", "Roar", 26, (-11.0, -14.0, 6.0), (0, 0, 3.2)),
        ("wyvern_swipe", "SwipeL", 16, (-11.0, -14.0, 6.0), (0, 0, 3.0)),
        ("wyvern_slam_up", "Slam", 22, (-11.0, -14.0, 6.0), (0, 0, 3.4)),
        ("wyvern_barrage", "Barrage", 36, (-11.0, -14.0, 6.0), (0, 0, 3.4)),
        ("wyvern_lunge", "Lunge", 26, (14.0, -6.0, 5.0), (0, -1, 3.0)),
        ("wyvern_tail", "TailLash", 25, (-8.0, 15.0, 9.0), (0, 2, 2.5)),
        ("wyvern_walk", "Walk", 10, (14.0, -8.0, 5.0), (0, 0, 2.6)),
        ("wyvern_hover", "Hover", 0, (-11.0, -14.0, 6.0), (0, 0, 3.4)),
        ("wyvern_spin", "SpinDive", 7, (14.0, -8.0, 5.0), (0, 0, 2.6)),
        ("wyvern_impact", "Impact", 10, (14.0, -8.0, 5.0), (0, 0, 2.0)),
        ("wyvern_death", "Death", 60, (-11.0, -14.0, 6.0), (0, 0, 1.6)),
    ])

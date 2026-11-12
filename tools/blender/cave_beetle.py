# The Beetles: the Crystal Caves mini-boss band (docs/cave-beetles.md). One model for all four (Bug Lennon, Paul
# Arthropodney, George Harrison and Scarab Starr); Unity swaps the shell, belly and crystal colours per beetle.
# The same look as before: a round glossy shell with crystal growths, big friendly eyes, six stubby legs, a microphone
# pack strapped to its back and a headset mic. No antennae or mandibles. About 2.3 m long, 1.3 m to the top of the shell.
# Built with the desert bosses' kit (bosskit.py); run tools/blender/cave_bosses.py -- beetle.
import math
from bosskit import Boss, srgb, sym, X, Y, Z

LEGS = (("F", -0.4), ("M", 0.02), ("B", 0.42))
# The shell's sections along its length: (y, centre height, width, height).
SHELL = [(1.0, 0.74, 0.5, 0.4), (0.82, 0.72, 1.25, 0.8), (0.45, 0.72, 1.62, 1.05), (0.05, 0.73, 1.68, 1.12),
         (-0.35, 0.72, 1.6, 1.05), (-0.6, 0.7, 1.38, 0.9)]


def shell_top(y):
    for (y0, z0, _, h0), (y1, z1, _, h1) in zip(SHELL, SHELL[1:]):
        if y1 <= y <= y0:
            t = (y - y0) / (y1 - y0)
            return z0 + (z1 - z0) * t + (h0 + (h1 - h0) * t) / 2
    return SHELL[-1][1] + SHELL[-1][3] / 2


def build():
    b = Boss("CaveBeetle")
    b.material("Shell", srgb(199, 36, 31), rough=0.18)          # swapped per beetle in Unity
    b.material("Belly", srgb(110, 40, 36), rough=0.65)          # swapped per beetle in Unity
    b.material("Crystal", srgb(230, 120, 120), rough=0.15, emit=srgb(255, 140, 140), strength=2.0)  # swapped per beetle
    b.material("Seam", srgb(30, 28, 26), rough=0.5)
    b.material("EyeWhite", srgb(242, 237, 219), rough=0.15)
    b.material("Pupil", srgb(8, 8, 10), rough=0.1)
    b.material("Leg", srgb(31, 28, 26), rough=0.55)
    b.material("Pack", srgb(41, 43, 48), rough=0.45, metal=0.6)
    b.material("Strap", srgb(26, 26, 28), rough=0.75)
    b.material("Foam", srgb(15, 15, 18), rough=0.95)
    b.material("MicLight", srgb(50, 255, 90), emit=srgb(50, 255, 90), strength=3.0)  # driven by the script in Unity

    # ------------------------------------------------------------ skeleton (faces -Y, +X is its left)
    b.bone("Root", (0, 0, 0))
    b.bone("Pivot", (0, 0.4, 0.45), "Root")      # near the back legs: it rears up and flips over about here
    b.bone("Body", (0, 0.05, 0.72), "Pivot")
    b.bone("Head", (0, -0.85, 0.62), "Body")
    b.bone("MicPack", (0, 0.3, 1.33), "Body")     # also where the signal amplifier rides (the ship part)
    for name, y in LEGS:
        for side, s in (("L", 1), ("R", -1)):
            b.bone("Leg" + name + side, (0.5 * s, y, 0.34), "Body")

    # ------------------------------------------------------------ shell, glossy on top, the belly underneath
    def shell_mat(c, k, i):
        if k < 0 or k >= len(SHELL) - 1:
            return "Shell"
        return "Belly" if c.z < (SHELL[k][1] + SHELL[k + 1][1]) / 2 - 0.12 else "Shell"
    b.loft("Carapace", [((0, y, z), w, h) for y, z, w, h in SHELL], X, Z, 14, 2.0, shell_mat, bone="Body")
    b.path("Seam", [(0, y, shell_top(y) + 0.008) for y in (-0.55, -0.3, 0.0, 0.3, 0.6, 0.85, 0.97)], 0.025, "Seam",
           n=4, bone="Body")
    b.blob("Pronotum", (0, -0.72, 0.7), (0.6, 0.32, 0.4), "Shell", subdiv=2, bone="Body")

    # ------------------------------------------------------------ head: big friendly eyes, no antennae or mandibles
    b.blob("HeadM", (0, -1.0, 0.62), (0.42, 0.34, 0.32), "Shell", subdiv=2, bone="Head")
    for s in (1, -1):
        b.blob("Eye%d" % s, (0.2 * s, -1.2, 0.76), (0.17, 0.12, 0.15), "EyeWhite", subdiv=2, bone="Head")
        b.blob("Pupil%d" % s, (0.21 * s, -1.31, 0.77), (0.075, 0.03, 0.08), "Pupil", subdiv=1, bone="Head")
        b.blob("Glint%d" % s, (0.18 * s, -1.335, 0.81), (0.022, 0.01, 0.022), "EyeWhite", subdiv=0, bone="Head")

    # ------------------------------------------------------------ crystal growths on the shoulders and the back
    for s in (1, -1):
        for base, d, big in (((0.55 * s, -0.45, 1.02), (0.5 * s, -0.2, 1.0), 1.0),
                             ((0.38 * s, 0.62, 1.1), (0.3 * s, 0.6, 1.0), 0.75)):
            for k, (dx, dy, length, radius) in enumerate(((0, 0, 0.36, 0.09), (0.12, 0.08, 0.26, 0.07), (-0.08, 0.12, 0.2, 0.06))):
                p = (base[0] + dx * s * big, base[1] + dy * big, base[2] - 0.02 * k)
                dd = (d[0] + dx * s * 2, d[1] + dy * 2, d[2])
                b.spike("Crystal%d_%d_%d" % (s, int(big * 4), k), p, dd, length * big, radius * big, "Crystal", n=5, bone="Body")

    # ------------------------------------------------------------ the microphone pack and its straps
    b.box("Pack", (0, 0.3, 1.33), (0.42, 0.55, 0.22), "Pack", rot=(math.radians(-6), 0, 0), bone="MicPack")
    b.box("PackFace", (0, 0.3, 1.45), (0.3, 0.42, 0.04), "Strap", rot=(math.radians(-6), 0, 0), bone="MicPack")
    b.blob("MicLightM", (0.12, 0.12, 1.48), (0.045, 0.045, 0.035), "MicLight", subdiv=1, bone="MicPack")
    for k in range(3):
        b.box("Grille%d" % k, (-0.07, 0.36 + 0.07 * k, 1.475), (0.13, 0.025, 0.012), "Pack", bone="MicPack")
    b.tube("Aerial", (-0.12, 0.5, 1.42), (-0.2, 0.75, 1.85), 0.018, 0.012, "Strap", n=4, bone="MicPack")
    b.blob("AerialTip", (-0.2, 0.75, 1.86), (0.035, 0.035, 0.035), "Strap", subdiv=0, bone="MicPack")
    strap = [(0.21, 0.3, 1.32), (0.6, 0.3, 1.16), (0.84, 0.3, 0.8), (0.78, 0.3, 0.45), (0.45, 0.3, 0.24), (0.0, 0.3, 0.2)]
    b.path("Strap", strap + [(-x, y, z) for x, y, z in reversed(strap[:-1])], 0.03, "Strap", n=4, bone="Body")

    # ------------------------------------------------------------ headset: a band over the head, a boom mic to the mouth
    b.path("Headband", [(0.41 * math.cos(math.radians(a)), -1.0, 0.6 + 0.38 * math.sin(math.radians(a))) for a in range(0, 181, 20)],
           0.022, "Strap", n=4, bone="Head")
    for s in (1, -1):
        b.blob("EarCup%d" % s, (0.42 * s, -1.0, 0.6), (0.06, 0.1, 0.1), "Pack", subdiv=1, bone="Head")
    b.path("Boom", [(0.44, -1.0, 0.56), (0.42, -1.15, 0.46), (0.3, -1.28, 0.42), (0.15, -1.36, 0.44)], 0.015, "Strap", n=4,
           bone="Head")
    b.blob("MicFoam", (0.12, -1.38, 0.45), (0.06, 0.06, 0.06), "Foam", subdiv=1, bone="Head")

    # ------------------------------------------------------------ six short, stubby legs, mostly under the shell
    for name, y in LEGS:
        for side, s in (("L", 1), ("R", -1)):
            foot_y = y + {"F": -0.05, "M": 0.0, "B": 0.05}[name]
            b.tube("Leg%s%sM" % (name, side), (0.5 * s, y, 0.34), (0.66 * s, foot_y, 0.06), 0.1, 0.08, "Leg", n=5,
                   bone="Leg" + name + side)
            b.blob("Foot%s%s" % (name, side), (0.67 * s, foot_y, 0.05), (0.1, 0.12, 0.06), "Leg", subdiv=1, bone="Leg" + name + side)
    return b


def animate(b):
    legs = ["Leg" + n + s for n, _ in LEGS for s in "LR"]
    b.base = {k: (0, 0, 0) for k in ["Root", "Pivot", "Body", "Head", "MicPack"] + legs}
    b.base["Root@"] = (0, 0, 0)

    def M(*poses):
        out = {}
        for q in poses:
            out.update(q)
        return out

    def gait(t, swing, lift):
        """Tripod gait: front-left, middle-right and back-left step together, then the other three. +yaw swings a
        foot back; the foot lifts on the way forward."""
        out = {}
        for name, _ in LEGS:
            for side, s in (("L", 1), ("R", -1)):
                group = (name == "M") != (side == "L")
                p = t + (0.0 if group else math.pi)
                out["Leg" + name + side] = (0, s * swing * math.sin(p), s * lift * max(0.0, math.cos(p)))
        return out

    def front_legs(pitch, wave=0.0, t=0.0):
        return {"LegFL": (pitch + wave * math.sin(t), 0, 10), "LegFR": (pitch - wave * math.sin(t), 0, -10)}

    # Idle: nods along to a beat only it can hear; the aerial wobbles.
    keys = []
    for f in range(0, 61, 5):
        t = 2 * math.pi * f / 60
        keys.append((f, {"Head": (7 * abs(math.sin(2 * t)), 0, 0), "Body": (1.5 * math.sin(2 * t), 0, 0),
                         "MicPack": (0, 5 * math.sin(t), 0), "Root@": (0, 0.025 * abs(math.sin(2 * t)), 0)}))
    b.clip("Idle", keys, loop=True)

    keys = []
    for f in range(0, 41, 5):
        t = 2 * math.pi * f / 40
        pose = {"Pivot": (0, 2 * math.sin(t), 2 * math.sin(2 * t)), "Root@": (0, 0.04 * abs(math.sin(2 * t)), 0),
                "Head": (3 * math.sin(2 * t), 0, 0), "MicPack": (0, 4 * math.sin(t), 0)}
        pose.update(gait(t, 22, 14))
        keys.append((f, pose))
    b.clip("Walk", keys, loop=True)

    keys = []
    for f in range(0, 21, 2):
        t = 2 * math.pi * f / 20
        pose = {"Pivot": (6, 0, 3 * math.sin(2 * t)), "Root@": (0, 0.06 * abs(math.sin(2 * t)), 0), "Head": (6, 0, 0)}
        pose.update(gait(t, 32, 20))
        keys.append((f, pose))
    b.clip("Run", keys, loop=True)

    # Roar (the fight starting, and every new phase): rears up on its back legs, head back, front legs waving.
    rear = M({"Pivot": (-34, 0, 0), "Root@": (0, 0.1, 0), "Head": (-18, 0, 0), "LegML": (-25, 0, 0), "LegMR": (-25, 0, 0),
              "LegBL": (30, 0, 0), "LegBR": (30, 0, 0)}, front_legs(-50))
    b.clip("Roar", [(0, {}), (14, rear), (22, M(rear, front_legs(-60, 20, 0))), (30, M(rear, {"Head": (-22, 12, 8)}, front_legs(-60, 20, 2))),
                    (40, M(rear, {"Head": (-22, -12, -8)}, front_legs(-60, 20, 4))), (50, M(rear, front_legs(-55))), (70, {})],
           events={"roar": 22})

    # Head-butt: rears back (the warning), then lunges head first.
    wind = {"Pivot": (-14, 0, 0), "Root@": (0, 0, -0.15), "Head": (-14, 0, 0), "LegFL": (-20, 0, 0), "LegFR": (-20, 0, 0)}
    lunge = {"Pivot": (10, 0, 0), "Root@": (0, -0.03, 0.55), "Head": (18, 0, 0), "LegBL": (25, 0, 0), "LegBR": (25, 0, 0)}
    b.clip("Headbutt", [(0, {}), (16, wind), (20, lunge), (23, lunge), (36, {})], events={"move": 16, "hit": 20})

    # Brace (box-in warning): reared up on its back legs, rocking, front legs paddling.
    keys = []
    for f in range(0, 13, 3):
        t = 2 * math.pi * f / 12
        keys.append((f, M({"Pivot": (-26 - 3 * math.sin(t), 0, 0), "Root@": (0, 0.08, 0), "Head": (-8, 0, 0),
                           "LegBL": (26, 0, 0), "LegBR": (26, 0, 0)}, front_legs(-45, 18, t))))
    b.clip("Brace", keys, loop=True)

    # Charge (box-in): head down, legs going flat out.
    keys = []
    for f in range(0, 13, 2):
        t = 2 * math.pi * f / 12
        pose = {"Pivot": (8, 0, 0), "Head": (16, 0, 0), "Root@": (0, -0.04 + 0.04 * abs(math.sin(2 * t)), 0)}
        pose.update(gait(t, 36, 22))
        keys.append((f, pose))
    b.clip("Charge", keys, loop=True)

    # Flipped onto its back (a box-in crash or a missed stage dive): the belly is the weak spot.
    flail = {k: (40 * (1 if i % 2 else -1), 0, 0) for i, k in enumerate(legs)}
    back = {"Pivot": (0, 0, 180), "Root@": (0, 0.5, 0)}
    b.clip("FlipOver", [(0, {}), (5, M({"Pivot": (0, 0, 70), "Root@": (0, 0.55, 0), "Head": (-10, 10, 0)}, flail)),
                        (10, M({"Pivot": (0, 0, 150), "Root@": (0, 0.65, 0)}, flail)), (13, M(back, {"Root@": (0, 0.42, 0)})),
                        (17, M(back, {"Root@": (0, 0.56, 0)})), (22, back)], events={"flipped": 13})
    keys = []
    for f in range(0, 31, 3):
        t = 2 * math.pi * f / 30
        pose = {"Pivot": (0, 0, 180 + 6 * math.sin(t)), "Root@": (0, 0.5, 0), "Head": (0, 12 * math.sin(t), 0)}
        for i, k in enumerate(legs):
            pose[k] = (30 * math.sin(2 * t + i * 1.3), 0, 0)
        keys.append((f, pose))
    b.clip("OnBack", keys, loop=True)
    b.clip("FlipUp", [(0, back), (8, M(back, {"Pivot": (0, 0, 196)})), (14, M(back, {"Pivot": (0, 0, 164)})),
                      (20, M({"Pivot": (0, 0, 90), "Root@": (0, 0.75, 0)}, flail)), (26, {"Pivot": (0, 0, -8), "Root@": (0, -0.05, 0)}),
                      (30, {"Pivot": (0, 0, 4)}), (36, {})], events={"up": 24})

    # Drum stomp (phase 2): rears high (the warning), holds, then slams down: a shockwave of crystals rolls out.
    high = M({"Pivot": (-46, 0, 0), "Root@": (0, 0.25, -0.1), "Head": (-20, 0, 0), "LegML": (-30, 0, 0), "LegMR": (-30, 0, 0),
              "LegBL": (34, 0, 0), "LegBR": (34, 0, 0)}, front_legs(-60))
    slam = {"Pivot": (10, 0, 0), "Root@": (0, -0.06, 0.15), "Head": (12, 0, 0), "LegFL": (20, 0, 18), "LegFR": (20, 0, -18)}
    b.clip("Stomp", [(0, {}), (24, high), (30, M(high, {"Pivot": (-48, 0, 0), "Root@": (0, 0.3, -0.1)})), (34, slam), (40, slam),
                     (56, {})], events={"slam": 34})

    # Stage dive (phase 3): crouches, leaps (the script flies it), spreads out like a starfish and belly-flops.
    spread = {k: (0, 0, (35 if k.endswith("L") else -35)) for k in legs}
    b.clip("Dive", [(0, {}), (14, M({"Root@": (0, -0.18, 0), "Pivot": (6, 0, 0)}, {k: (0, 0, (-15 if k.endswith("L") else 15)) for k in legs})),
                    (18, M({"Pivot": (-8, 0, 0), "Root@": (0, 0.1, 0), "Head": (-15, 0, 0)}, spread)),
                    (36, M({"Pivot": (6, 0, 0), "Head": (-10, 0, 0)}, {k: (0, 0, (45 if k.endswith("L") else -45)) for k in legs})),
                    (40, M({"Root@": (0, -0.12, 0)}, spread)), (46, M({"Root@": (0, -0.06, 0)}, spread)), (64, {})],
           events={"launch": 14, "land": 40})

    hit = {"Pivot": (-10, 8, 10), "Head": (-14, 12, 10), "Root@": (0, 0, 0.25)}
    b.clip("Stagger", [(0, {}), (6, hit), (22, M(hit, {"Head": (-6, 6, 4)})), (40, {})])
    curled = {k: (40 * (1 if i % 2 else -1), 0, 0) for i, k in enumerate(legs)}
    b.clip("Death", [(0, {}), (8, hit), (22, {"Pivot": (0, 0, 60), "Root@": (0, 0.3, 0)}), (36, M(back, curled)), (70, M(back, curled))])
    return b


def shots(b, folder):
    front, look = (-3.2, -4.8, 2.0), (0, -0.2, 0.7)
    side = (5.0, -0.6, 1.6)
    return b.previews(folder, [
        ("beetle_front", None, 0, front, look),
        ("beetle_side", None, 0, side, (0, 0, 0.7)),
        ("beetle_back", None, 0, (2.6, 4.2, 2.6), (0, 0.2, 0.9)),
        ("beetle_roar", "Roar", 24, front, (0, 0, 1.1)),
        ("beetle_headbutt_wind", "Headbutt", 16, side, look),
        ("beetle_headbutt_hit", "Headbutt", 20, side, look),
        ("beetle_brace", "Brace", 0, front, (0, 0, 1.0)),
        ("beetle_charge", "Charge", 4, side, look),
        ("beetle_flip", "FlipOver", 8, front, (0, 0, 1.0)),
        ("beetle_on_back", "OnBack", 6, (-3.0, -3.6, 3.0), (0, 0, 0.6)),
        ("beetle_stomp_high", "Stomp", 28, side, (0, 0, 1.2)),
        ("beetle_stomp_slam", "Stomp", 35, side, look),
        ("beetle_dive_air", "Dive", 30, front, (0, 0, 0.9)),
        ("beetle_dive_land", "Dive", 41, front, look),
        ("beetle_walk", "Walk", 10, front, look),
        ("beetle_death", "Death", 60, front, (0, 0, 0.6)),
    ])

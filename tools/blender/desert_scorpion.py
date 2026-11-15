# Pitchclaw, the Oilstone Scorpion (rework 11-10, after Lawson's first look): a huge scorpion entirely armoured in
# angular rock slabs, with black fuel flowing in the cracks between them (pulses run outward from the ship's fuel
# tank sunk in its back, like the energy on the Core Worm), two massive serrated pincers, and a far longer,
# twelve-segment tail it can whip, stab over its head, or reach out with to snare the player. About 9 m long plus a
# 10 m tail. See docs/desert-bosses.md.
import math
from mathutils import Vector
from bosskit import Boss, srgb, sym, mirror, X, Y, Z

TAIL_SEGMENTS = 12
TAIL_LENGTH = 10.0
TAIL_BASE = Vector((0, 2.75, 1.7))


def tail_curve():
    """The tail's rest arch: back from the abdomen, up, and curling forward over the body (heading in degrees, 0 =
    straight back, 90 = up, 180 = forward). Returns the joint points and each segment's heading."""
    pts = [TAIL_BASE.copy()]
    heads = []
    seg = TAIL_LENGTH / TAIL_SEGMENTS
    phi = math.radians(28)
    for i in range(TAIL_SEGMENTS):
        k = 0.17 + 0.33 * (i / (TAIL_SEGMENTS - 1)) ** 1.3       # tightening curl toward the tip
        phi_mid = phi + k * seg * 0.5
        heads.append(math.degrees(phi_mid))
        p = pts[-1] + Vector((0, math.cos(phi_mid), math.sin(phi_mid))) * seg
        pts.append(p)
        phi += k * seg
    return pts, heads


TAIL_PTS, TAIL_REST = tail_curve()


def build():
    b = Boss("Pitchclaw")
    b.material("Rock", srgb(172, 122, 80), rough=0.95)
    b.material("RockDark", srgb(122, 84, 58), rough=0.95)
    b.material("Basalt", srgb(70, 60, 58), rough=0.85)
    b.material("Shard", srgb(150, 104, 70), rough=0.7)
    # The cracks: in Unity this becomes the flowing-oil shader; here it glows so the previews show the veins.
    b.material("Oil", srgb(18, 14, 14), rough=0.08, metal=0.2, emit=srgb(255, 140, 40), strength=1.2)
    b.material("Obsidian", srgb(22, 18, 26), rough=0.15)
    b.material("Tank", srgb(214, 210, 200), rough=0.45, metal=0.5)
    b.material("Stripe", srgb(234, 110, 30), rough=0.5, metal=0.3)
    b.material("Eye", srgb(255, 180, 60), emit=srgb(255, 170, 50), strength=4.0)
    b.material("Drip", srgb(30, 20, 14), rough=0.05, emit=srgb(255, 140, 40), strength=0.6)

    # ------------------------------------------------------------ skeleton
    b.bone("Root", (0, 0, 0))
    b.bone("Pivot", (0, 0.3, 1.5), "Root")
    b.bone("Body", (0, -0.7, 1.5), "Pivot")
    b.bone("Abdomen", (0, 0.9, 1.55), "Pivot")
    b.bone("Mouth", (0, -2.75, 1.25), "Body")           # socket: the fuel spew
    prev = "Abdomen"
    for i in range(TAIL_SEGMENTS):
        prev = b.bone("Tail%d" % (i + 1), TAIL_PTS[i], prev)
    b.bone("Stinger", TAIL_PTS[-1], prev)
    tip_dir = (TAIL_PTS[-1] - TAIL_PTS[-2]).normalized()
    sting_tip = TAIL_PTS[-1] + tip_dir * 0.4 + Vector((0, -0.9, -1.25))
    b.bone("StingTip", sting_tip, "Stinger")            # socket: the point (stabs, the snare's hook, the fuel rain)
    for side, s in (("L", 1), ("R", -1)):
        b.bone("Mand" + side, (0.32 * s, -2.45, 1.3), "Body")
        b.bone("Arm" + side, (1.0 * s, -1.9, 1.55), "Body")
        b.bone("Fore" + side, (2.05 * s, -2.6, 1.7), "Arm" + side)
        b.bone("Claw" + side, (2.0 * s, -3.6, 1.6), "Fore" + side)
        b.bone("Finger" + side, (1.55 * s, -4.55, 1.6), "Claw" + side)
        for i, (y, spread) in enumerate([(-1.25, -0.55), (-0.45, -0.15), (0.35, 0.25), (1.15, 0.65)]):
            b.bone("Leg%dA%s" % (i + 1, side), (0.95 * s, y, 1.35), "Body" if i < 2 else "Abdomen")
            b.bone("Leg%dB%s" % (i + 1, side), (2.3 * s, y + spread, 2.15), "Leg%dA%s" % (i + 1, side))

    body_chain = ["Body", "Abdomen"]
    tank_centre = Vector((0, 1.45, 2.35))

    # ------------------------------------------------------------ body: oil core flowing out from the tank, rock shell
    core = [(-2.55, 1.35, 1.0, 0.7), (-2.1, 1.45, 2.0, 1.1), (-1.0, 1.55, 2.5, 1.25), (0.2, 1.58, 2.5, 1.25),
            (1.3, 1.6, 2.35, 1.2), (2.3, 1.65, 1.8, 1.0), (3.0, 1.72, 1.0, 0.75)]
    front = [c for c in core if c[0] <= 1.45]
    back = [c for c in core if c[0] >= 1.3]
    b.loft("CoreFront", [((0, y, z), w, h) for y, z, w, h in reversed(front)], X, Z, 12, 2.2, "Oil",
           chain=body_chain, blend=0.5)
    b.loft("CoreBack", [((0, y, z), w, h) for y, z, w, h in back], X, Z, 12, 2.2, "Oil", chain=body_chain, blend=0.5)

    def tank_window(pos, normal):
        return (pos - tank_centre).length < 0.95 and normal.z > 0.3

    b.shell("BodyShell", [(0, y, z) for y, z, w, h in core], [w / 2 for y, z, w, h in core], cell=0.95, thickness=0.17, gap=0.07,
            squash=0.52, skip=tank_window, chain=body_chain, blend=0.5)
    # jagged shards along the back, like a ridge of broken stone
    for i, y in enumerate([-1.9, -1.35, -0.8, -0.2, 0.35, 2.05, 2.6]):
        h = 0.55 + 0.25 * ((i * 7) % 3) / 2
        b.spike("BackShard%d" % i, (0, y, 2.15), (0, 0.35, 1), h, 0.2, "Shard", n=4, chain=body_chain, blend=0.5)
        for s in (1, -1):
            b.spike("SideShard%d%d" % (i, s), (0.95 * s, y + 0.1, 1.85), (0.9 * s, 0.2, 0.6), h * 0.7, 0.14, "Shard", n=4,
                    chain=body_chain, blend=0.5)
    # the fuel tank in its window, rock lipping its edges
    b.loft("FuelTank", [((0, y, 2.35), 1.05, 1.05) for y in (0.75, 0.85, 1.25, 1.4, 1.85, 2.0, 2.3)], X, Z, 10, 2.0,
           lambda c, k, i: "Stripe" if k in (1, 4) else "Tank", chain=body_chain, blend=0.5)
    for s in (1, -1):
        b.blob("TankRock%d" % s, (0.6 * s, 1.5, 2.15), (0.4, 1.0, 0.4), "RockDark", subdiv=1, jitter=0.15, bone="Abdomen")
        b.path("TankPipe%d" % s, [(0.25 * s, 0.7, 2.45), (0.6 * s, 0.3, 2.25), (0.8 * s, -0.1, 1.95)], 0.07, "Tank", n=5,
               chain=body_chain, blend=0.5)
    # face: a heavy brow of stone over clusters of amber eyes, mandibles
    for s in (1, -1):
        b.blob("Brow%d" % s, (0.45 * s, -2.25, 1.98), (0.42, 0.36, 0.25), "Basalt", subdiv=1, jitter=0.12, bone="Body")
        for k, (dx, dy, dz, r) in enumerate([(0.18, -2.4, 1.9, 0.09), (0.34, -2.36, 1.84, 0.07), (0.6, -2.2, 1.8, 0.06),
                                             (0.12, -2.2, 2.05, 0.08)]):
            b.blob("Eye%d%d" % (s, k), (dx * s, dy, dz), (r, r, r), "Eye", subdiv=1, bone="Body")
        mand = "Mand" + ("L" if s > 0 else "R")
        b.spike("Mandible%d" % s, (0.32 * s, -2.45, 1.3), (-0.25 * s, -1, -0.2), 0.8, 0.17, "Basalt", n=5,
                bend=(-0.3 * s, 0, 0), bone=mand)
        b.spike("MandFang%d" % s, (0.22 * s, -2.95, 1.25), (-0.4 * s, -0.3, -1), 0.32, 0.06, "Obsidian", bone=mand)
    b.blob("MawOil", (0, -2.6, 1.25), (0.38, 0.22, 0.2), "Oil", bone="Body")

    # ------------------------------------------------------------ the long tail
    tchain = ["Abdomen"] + ["Tail%d" % (i + 1) for i in range(TAIL_SEGMENTS)] + ["Stinger"]
    radii = [0.55 - 0.3 * (i / TAIL_SEGMENTS) for i in range(TAIL_SEGMENTS + 1)]
    pts = [Vector((0, 2.4, 1.68))] + TAIL_PTS
    b.path("TailCore", pts, [radii[0]] + radii, "Oil", n=8, chain=tchain, blend=0.3, uv_start=3.0)
    for i in range(TAIL_SEGMENTS):
        p, q = TAIL_PTS[i], TAIL_PTS[i + 1]
        bone = "Tail%d" % (i + 1)
        r = radii[i] * 1.12
        # one ring of armour per segment, a crack of oil showing at each joint
        b.shell("TailRing%d" % (i + 1), [p.lerp(q, 0.05), p.lerp(q, 0.95)], [r, r * 0.93], cell=2 * math.pi * r / 4.2,
                thickness=0.13, gap=0.09, taper=0.86, bone=bone)
        d = (q - p).normalized()
        top = d.cross(X).normalized()
        if top.z < 0:
            top = -top
        b.spike("TailShard%d" % (i + 1), p.lerp(q, 0.5) + top * r, top + d * 0.35, 0.5 - 0.02 * i, 0.12, "Shard", n=4,
                bone=bone)
    end = TAIL_PTS[-1]
    b.path("BulbCore", [end, end + tip_dir * 0.7], [0.5, 0.42], "Oil", n=8, bone="Stinger", uv_start=13.0)
    b.shell("BulbShell", [end, end + tip_dir * 0.75], [0.62, 0.5], cell=0.6, thickness=0.13, gap=0.08, bone="Stinger")
    sting_base = end + tip_dir * 0.6
    b.spike("Sting", sting_base, (sting_tip - sting_base).normalized(), (sting_tip - sting_base).length * 1.05, 0.28,
            "Obsidian", n=6, segments=2, bend=tuple(tip_dir * 0.35), bone="Stinger")
    b.blob("StingDrip", sting_tip, (0.07, 0.07, 0.12), "Drip", subdiv=1, bone="Stinger")

    # ------------------------------------------------------------ pincers
    for side, s in (("L", 1), ("R", -1)):
        sh, el, wr = Vector((1.0 * s, -1.9, 1.55)), Vector((2.05 * s, -2.6, 1.7)), Vector((2.0 * s, -3.6, 1.6))
        b.path("ArmCore" + side, [sh, el], [0.32, 0.3], "Oil", n=6, bone="Arm" + side, uv_start=2.0)
        b.shell("ArmShell" + side, [sh, el], [0.34, 0.32], cell=0.55, thickness=0.12, gap=0.08, bone="Arm" + side)
        b.path("ForeCore" + side, [el, wr], [0.34, 0.38], "Oil", n=6, bone="Fore" + side, uv_start=3.2)
        b.shell("ForeShell" + side, [el, wr], [0.38, 0.42], cell=0.6, thickness=0.13, gap=0.08, bone="Fore" + side)
        b.spike("ForeShard" + side, el + Vector((0.25 * s, 0, 0.35)), (0.4 * s, 0.2, 1), 0.6, 0.14, "Shard", n=4,
                bone="Fore" + side)
        palm_a, palm_b = Vector((1.95 * s, -3.45, 1.6)), Vector((1.95 * s, -4.85, 1.58))
        b.path("PalmCore" + side, [palm_a, palm_a.lerp(palm_b, 0.5), palm_b], [0.5, 0.72, 0.5], "Oil", n=8,
               bone="Claw" + side, uv_start=4.3)
        b.shell("PalmShell" + side, [palm_a, palm_a.lerp(palm_b, 0.5), palm_b], [0.55, 0.78, 0.52], cell=0.7,
                thickness=0.15, gap=0.07, squash=0.8, bone="Claw" + side)
        for k in range(3):
            b.spike("PalmShard%s%d" % (side, k), palm_a.lerp(palm_b, 0.2 + 0.3 * k) + Vector((0.45 * s, 0, 0.45)),
                    (0.5 * s, 0.1, 1), 0.45, 0.13, "Shard", n=4, bone="Claw" + side)
        fixed_base, fixed_tip = Vector((2.15 * s, -4.75, 1.6)), Vector((1.85 * s, -6.25, 1.5))
        b.spike("FixedFinger" + side, fixed_base, fixed_tip - fixed_base, (fixed_tip - fixed_base).length, 0.38, "Basalt",
                n=5, segments=2, bend=(-0.25 * s, 0, 0), bone="Claw" + side)
        mv_base, mv_tip = Vector((1.55 * s, -4.6, 1.6)), Vector((1.55 * s, -5.95, 1.55))
        b.spike("MovingFinger" + side, mv_base, mv_tip - mv_base, (mv_tip - mv_base).length, 0.32, "RockDark", n=5,
                segments=2, bend=(0.3 * s, 0, 0), bone="Finger" + side)
        for k in range(4):
            t = 0.2 + 0.18 * k
            b.spike("Serration%s%d" % (side, k), fixed_base.lerp(fixed_tip, t) + Vector((-0.18 * s, 0, 0)),
                    (-1 * s, -0.3, 0), 0.27, 0.06, "Obsidian", n=3, bone="Claw" + side)
            b.spike("SerrationM%s%d" % (side, k), mv_base.lerp(mv_tip, t) + Vector((0.14 * s, 0, 0)), (1 * s, -0.3, 0),
                    0.22, 0.05, "Obsidian", n=3, bone="Finger" + side)
        # legs: oil core, slabbed, a stone knee
        for i, (y, spread) in enumerate([(-1.25, -0.55), (-0.45, -0.15), (0.35, 0.25), (1.15, 0.65)]):
            a = Vector((0.95 * s, y, 1.35))
            k = Vector((2.3 * s, y + spread, 2.15))
            f = Vector((3.2 * s, y + spread * 1.6, 0.0))
            mid = k.lerp(f, 0.45) + Vector((0.12 * s, 0, 0.1))
            A, B = "Leg%dA%s" % (i + 1, side), "Leg%dB%s" % (i + 1, side)
            b.path("LegCoreA%d%s" % (i, side), [a, k], [0.2, 0.17], "Oil", n=6, bone=A, uv_start=1.5)
            b.shell("LegShellA%d%s" % (i, side), [a, k], [0.24, 0.21], cell=0.5, thickness=0.09, gap=0.08, bone=A)
            b.blob("LegKnee%d%s" % (i, side), k, (0.3, 0.3, 0.3), "Basalt", subdiv=1, jitter=0.15, bone=A)
            b.path("LegCoreB%d%s" % (i, side), [k, mid, f + Vector((0, 0, 0.2))], [0.17, 0.13, 0.07], "Oil", n=6, bone=B,
                   uv_start=2.9)
            b.shell("LegShellB%d%s" % (i, side), [k, mid, f + Vector((0, 0, 0.35))], [0.2, 0.16, 0.08], cell=0.45,
                    thickness=0.08, gap=0.08, bone=B)
            b.spike("LegTip%d%s" % (i, side), f + Vector((0, 0, 0.25)), (0.1 * s, 0, -1), 0.3, 0.07, "Obsidian", n=4, bone=B)
    return b


# ------------------------------------------------------------ animation
def tail_shape(headings, yaw=0.0, yaw_spread=3, roll=0.0):
    """Pitch for each tail bone so segment i points along headings[i] (degrees: 0 back, 90 up, 180 forward), plus a
    yaw swing shared over the first few segments (+ swings the tail to the creature's left)."""
    out = {}
    prev_t, prev_r = 0.0, 0.0
    for i in range(TAIL_SEGMENTS):
        t, r = headings[i], TAIL_REST[i]
        dy = yaw / yaw_spread if i < yaw_spread else 0.0
        out["Tail%d" % (i + 1)] = ((t - prev_t) - (r - prev_r), dy, roll / TAIL_SEGMENTS)
        prev_t, prev_r = t, r
    return out


def profile(start, end, power=1.0, tip=None):
    """Headings easing from start to end along the tail; tip (optional) overrides the last two."""
    h = [start + (end - start) * (i / (TAIL_SEGMENTS - 1)) ** power for i in range(TAIL_SEGMENTS)]
    if tip is not None:
        h[-2] = (h[-3] + tip) / 2
        h[-1] = tip
    return h


REST = list(TAIL_REST)
COCKED = profile(72, 200, 1.6, tip=218)                 # coiled tight and high, ready to strike
STAB = profile(80, 190, 0.45, tip=232)         # thrown over the head, the point driven down in front
STAB_HIGH = profile(75, 205, 0.6, tip=215)     # pulled back between quick stabs
SNARE = profile(105, 184, 0.22, tip=192)        # flung out straight ahead at chest height
LOW = profile(12, 4, 1.0)                      # straight out behind, low (for sweeps and spins)
UP = profile(84, 92, 1.0)                      # straight up (the fuel rain)


def tail(headings, yaw=0.0, wave=0.0, phase=0.0):
    pose = tail_shape(headings, yaw)
    if wave:
        for i in range(TAIL_SEGMENTS):
            p, y, r = pose["Tail%d" % (i + 1)]
            pose["Tail%d" % (i + 1)] = (p + wave * math.sin(phase - i * 0.55), y + wave * 0.6 * math.cos(phase - i * 0.5), r)
    return pose


def animate(b):
    names = ["Root", "Pivot", "Body", "Abdomen", "Stinger"] + ["Tail%d" % (i + 1) for i in range(TAIL_SEGMENTS)]
    b.base = {k: (0, 0, 0) for k in names}
    sides = ["Mand", "Arm", "Fore", "Claw", "Finger"] + ["Leg%d%s" % (i, ab) for i in range(1, 5) for ab in "AB"]
    b.base.update(sym({k + "L": (0, 0, 0) for k in sides}))
    b.base["Root@"] = (0, 0, 0)

    def gait(t, swing, lift):
        out = {}
        for i in range(1, 5):
            for side, sgn in (("L", 1), ("R", -1)):
                phase = t + (0 if (i % 2 == 1) == (side == "L") else math.pi)
                fwd, up = math.sin(phase), max(0.0, math.cos(phase))
                out["Leg%dA%s" % (i, side)] = (0, -swing * fwd * sgn, lift * up * sgn)
                out["Leg%dB%s" % (i, side)] = (0, 0, -lift * 0.6 * up * sgn)
        return out

    def M(*poses):
        out = {}
        for p in poses:
            out.update(p)
        return out

    # Idle: claws flex, mandibles work, the long tail sways like a cobra.
    keys = []
    for f in range(0, 61, 10):
        t = 2 * math.pi * f / 60
        pose = {"Root@": (0, 0.03 * math.sin(t), 0), "Body": (1 * math.sin(t), 0, 0),
                "ArmL": (0, 3 * math.sin(t), 0), "ArmR": (0, -3 * math.sin(t + 1), 0),
                "FingerL": (0, 12 + 10 * math.sin(t), 0), "FingerR": (0, -12 - 10 * math.sin(t + 1.4), 0),
                "MandL": (0, 6 * math.sin(2 * t), 0), "MandR": (0, -6 * math.sin(2 * t), 0)}
        pose.update(tail(REST, 6 * math.sin(t), wave=2.5, phase=t))
        pose["Stinger"] = (5 * math.sin(t + 1), 0, 0)
        keys.append((f, pose))
    b.clip("Idle", keys, loop=True)

    for name, length, swing, lift, bob in (("Walk", 32, 16, 18, 0.05), ("Run", 16, 22, 24, 0.1)):
        keys = []
        step = length // 8
        for f in range(0, length + 1, step):
            t = 2 * math.pi * f / length
            pose = {"Root@": (0, bob * abs(math.sin(2 * t)), 0), "Pivot": (0, 2 * math.sin(t), 1.5 * math.sin(2 * t)),
                    "ArmL": (0, -6, 0), "ArmR": (0, 6, 0), "ForeL": (-4, 0, 0), "ForeR": (-4, 0, 0)}
            pose.update(gait(t, swing, lift))
            pose.update(tail(REST, 4 * math.sin(t), wave=2, phase=2 * t))
            keys.append((f, pose))
        b.clip(name, keys, loop=True)

    raise_claws = sym({"ArmL": (-20, 25, 35), "ForeL": (-40, 0, 10), "ClawL": (-30, 0, 0), "FingerL": (0, 34, 0)})
    roar = M({"Root@": (0, 0.45, 0.2), "Pivot": (-16, 0, 0), "Body": (-6, 0, 0), "MandL": (0, 30, 0), "MandR": (0, -30, 0)},
             raise_claws, sym({"Leg1AL": (0, 0, 20), "Leg2AL": (0, 0, 10)}), tail(COCKED))
    b.clip("Roar", [(0, {}), (16, roar), (24, M(roar, {"Pivot": (-18, 0, 0)})), (44, M(roar, tail(COCKED, 14))),
                    (56, M(roar, tail(COCKED, -14))), (74, {})], events={"roar": 22})

    # ---- pincers
    draw = {"Root@": (-0.1, 0, -0.3), "Pivot": (0, -8, 0), "ArmL": (-6, 40, 6), "ForeL": (0, 30, 0), "ClawL": (0, 10, 0),
            "FingerL": (0, 40, 0), "Body": (0, -6, 0)}
    snap = {"Root@": (0.1, 0, 0.9), "Pivot": (4, 10, 0), "ArmL": (6, -30, 0), "ForeL": (4, -30, 0), "ClawL": (0, -10, 0),
            "FingerL": (0, -4, 0), "Body": (2, 8, 0)}
    seq = [(0, {}), (12, draw), (15, M(draw, {"FingerL": (0, 44, 0)})), (19, snap), (24, snap), (40, {})]
    b.clip("SnapL", seq, events={"hit": 19, "move": 15})
    b.clip("SnapR", [(f, mirror(p)) for f, p in seq], events={"hit": 19, "move": 15})

    # Pincer combo: left snap, right snap, a held-back left (the delay), then a scissor of both claws across the front.
    scissor_open = M({"Root@": (0, 0.2, -0.2), "Pivot": (-8, 0, 0)},
                     sym({"ArmL": (-10, 55, 25), "ForeL": (-15, 35, 0), "FingerL": (0, 46, 0)}))
    scissor = M({"Root@": (0, 0, 1.0), "Pivot": (6, 0, 0)},
                sym({"ArmL": (6, -48, 0), "ForeL": (4, -38, 0), "ClawL": (0, -12, 0), "FingerL": (0, -4, 0)}))
    draw_long = M(draw, {"ArmL": (-12, 52, 12), "Pivot": (-4, -14, 0), "Root@": (-0.2, 0.1, -0.5)})
    b.clip("PincerCombo", [(0, {}), (10, draw), (16, snap), (22, snap), (30, mirror(draw)), (36, mirror(snap)),
                           (42, mirror(snap)), (50, draw_long), (58, M(draw_long, {"FingerL": (0, 48, 0)})), (62, snap),
                           (66, snap), (76, scissor_open), (84, scissor), (90, scissor), (110, {})],
           events={"hit": [16, 36, 62, 84], "move": [12, 32, 58, 80]})

    # ---- the tail
    # Tail lash: winds the tail round to its right, then sweeps it low and wide round its left side (jumpable).
    b.clip("TailLash", [(0, {}), (14, M({"Pivot": (0, 18, 0)}, tail(LOW, 75))), (18, M({"Pivot": (0, 22, 0)}, tail(LOW, 82))),
                        (24, M({"Pivot": (0, -10, 0)}, tail(LOW, 0))), (30, M({"Pivot": (0, -24, 0)}, tail(LOW, -85))),
                        (36, M({"Pivot": (0, -26, 0)}, tail(LOW, -95))), (56, {})],
           events={"hit_start": 20, "hit_end": 33})

    # Stab: cocks the tail tight, holds, then throws it over its head and drives the point in. Stuck a moment.
    cock = M({"Pivot": (-6, 0, 0), "Root@": (0, 0.1, -0.3)}, tail(COCKED))
    stab = M({"Pivot": (18, 0, 0), "Body": (6, 0, 0), "Root@": (0, -0.25, 0.6)}, tail(STAB))
    b.clip("Stab", [(0, {}), (16, cock), (26, M(cock, {"Stinger": (-14, 0, 0)})), (30, stab), (36, M(stab, {"Stinger": (4, 4, 0)})),
                    (42, M(stab, {"Stinger": (4, -4, 0)})), (48, M(stab, {"Stinger": (4, 4, 0)})), (66, {})],
           events={"hit": 30, "free": 52})

    # Stinger barrage: the tail hangs over its head and hammers three fast stabs, tracking.
    hover = M({"Pivot": (6, 0, 0), "Root@": (0, 0, 0.2)}, tail(STAB_HIGH))
    keys = [(0, {}), (14, cock), (20, hover)]
    for k, f in enumerate((24, 40, 56)):
        keys += [(f - 3, M(hover, tail(STAB_HIGH, 0))), (f, stab), (f + 6, hover if k < 2 else stab)]
    keys += [(70, M(stab, {"Stinger": (4, 5, 0)})), (90, {})]
    b.clip("StingerBarrage", keys, events={"hit": [24, 40, 56], "free": 74})

    # Tail snare: draws the tail back low, then flings it straight out ahead to hook the player, and hauls it back.
    reach = M({"Pivot": (8, 0, 0), "Root@": (0, -0.1, 0.4)}, tail(SNARE))
    b.clip("TailSnare", [(0, {}), (14, M({"Pivot": (-8, 0, 0)}, tail(profile(40, 140, 0.8)))), (20, M({"Pivot": (-10, 0, 0)}, tail(profile(30, 120, 0.8)))),
                         (25, reach), (30, M(reach, {"Stinger": (10, 0, 0)})), (44, cock), (62, {})],
           events={"hit": 25, "pull_end": 44})
    # Snare crush: the tail holds the player up in front while both claws crush, then flings them away.
    held = M({"Pivot": (-10, 0, 0), "Root@": (0, 0.3, 0.3)}, tail(profile(85, 175, 0.4, tip=150)),
             sym({"ArmL": (-25, 30, 20), "ForeL": (-35, 10, 0), "FingerL": (0, 42, 0)}))
    crush = M(held, sym({"ArmL": (-28, -20, 12), "ForeL": (-40, -25, 0), "FingerL": (0, -4, 0)}))
    fling = M({"Pivot": (-4, -30, 0)}, tail(profile(60, 120, 0.8), -60))
    b.clip("SnareCrush", [(0, cock), (12, held), (20, crush), (27, held), (34, crush), (42, held), (50, fling), (72, {})],
           events={"crush": [20, 34], "throw": 50})

    # Fuel spew: mandibles flare and the head end lowers; the stream sweeps from its right to its left.
    spew = M({"Body": (8, 0, 0), "MandL": (0, 40, 0), "MandR": (0, -40, 0), "Pivot": (4, 0, 0),
              "ArmL": (0, 20, 15), "ArmR": (0, -20, -15)}, tail(COCKED))
    b.clip("Spew", [(0, {}), (18, M(spew, {"Body": (-6, 0, 0)})), (24, M(spew, {"Pivot": (4, -30, 0)})),
                    (50, M(spew, {"Pivot": (4, 0, 0)})), (76, M(spew, {"Pivot": (4, 30, 0)})), (96, {})],
           events={"spew_start": 24, "spew_end": 76})

    # Fuel rain: the tail points straight up and sprays fuel high into the air to rain down round it.
    rain = M({"Pivot": (-8, 0, 0), "Root@": (0, 0.15, 0)}, tail(UP))
    b.clip("FuelRain", [(0, {}), (16, rain), (22, M(rain, {"Stinger": (-30, 0, 0)})), (36, M(rain, tail(UP, 18))),
                        (50, M(rain, tail(UP, -18))), (54, rain), (72, {})], events={"rain_start": 22, "rain_end": 52})

    # Spark scrape: both claws dragged along the ground outward from the front, throwing sparks.
    low = M({"Pivot": (8, 0, 0), "Root@": (0, -0.2, 0)}, sym({"ArmL": (30, -30, -14), "ForeL": (25, 0, 0), "ClawL": (20, 0, 0)}))
    wide = M(low, sym({"ArmL": (30, 40, -18), "ForeL": (24, 20, 0), "ClawL": (20, 0, 0)}))
    b.clip("Scrape", [(0, {}), (14, low), (20, low), (44, wide), (50, wide), (66, {})],
           events={"sparks_start": 20, "sparks_end": 46})

    # Burrow: claws dig in and it sinks into the sand; erupt: bursts up claws-first and crashes down.
    dig = M({"Pivot": (14, 0, 0)}, sym({"ArmL": (34, -10, -20), "ForeL": (30, 0, 0), "ClawL": (20, 0, 0)}),
            sym({"Leg1AL": (0, 0, 20), "Leg2AL": (0, 0, 20)}))
    b.clip("BurrowIn", [(0, {}), (10, dig), (18, M(dig, {"Root@": (0, -0.6, 0.2)})),
                        (30, M(dig, {"Root@": (0, -2.0, 0.6)}, tail(LOW))), (40, M(dig, {"Root@": (0, -3.2, 0.8)}, tail(LOW)))],
           events={"under": 36})
    burst = M({"Root@": (0, 0.8, 0), "Pivot": (-24, 0, 0)}, raise_claws, tail(COCKED))
    b.clip("BurrowOut", [(0, M(dig, {"Root@": (0, -3.2, 0)}, tail(LOW))), (6, M(burst, {"Root@": (0, -0.6, 0)})), (10, burst),
                         (18, M(burst, {"Root@": (0, 0.9, 0)})), (24, M({"Root@": (0, -0.3, 0.6), "Pivot": (10, 0, 0)},
                                                                       sym({"ArmL": (25, -10, -10), "ForeL": (20, 0, 0)}))),
                         (50, {})], events={"erupt": 6, "slam": 24})

    # Leap crush: crouches, springs (the code throws it), claws high in the air, lands with both claws.
    crouch = M({"Root@": (0, -0.5, 0), "Pivot": (8, 0, 0)}, sym({"Leg1AL": (0, 0, -16), "Leg2AL": (0, 0, -16),
                                                                 "Leg3AL": (0, 0, -16), "Leg4AL": (0, 0, -16)}))
    air = M({"Pivot": (-14, 0, 0)}, raise_claws, sym({"Leg1AL": (0, 0, 25), "Leg2AL": (0, 0, 25), "Leg3AL": (0, 0, 25),
                                                      "Leg4AL": (0, 0, 25)}), tail(COCKED))
    land = M({"Root@": (0, -0.35, 0.6), "Pivot": (12, 0, 0), "Body": (6, 0, 0)},
             sym({"ArmL": (28, -12, -10), "ForeL": (22, 0, 0), "ClawL": (12, 0, 0), "FingerL": (0, -2, 0)}))
    b.clip("LeapCrush", [(0, {}), (12, crouch), (15, M(crouch, {"Root@": (0, -0.6, 0)})), (18, air), (30, air), (33, land),
                         (40, land), (58, {})], events={"launch": 16, "land": 33})

    # Inferno spin: the tail straightened out low behind, it spins on the spot twice, sweeping everything round it.
    keys = [(0, {}), (12, M({"Pivot": (0, 30, 0), "Root@": (0, 0.1, 0)}, tail(LOW, 40)))]
    for k, yaw in enumerate((-90, -210, -330, -450, -570, -690)):
        keys.append((18 + k * 7, M({"Pivot": (0, yaw, 0), "Root@": (0, 0.15, 0)}, tail(LOW, -30))))
    keys += [(66, M({"Pivot": (0, -720, 0)}, tail(LOW, 0))), (86, {"Pivot": (0, -720, 0)})]
    b.clip("InfernoSpin", keys, events={"hit_start": 16, "hit_end": 60})

    hit = M({"Root@": (0, 0.1, -0.5), "Pivot": (-10, 8, 6), "Body": (-6, 0, 0), "MandL": (0, 30, 0), "MandR": (0, -30, 0)},
            sym({"ArmL": (-10, 20, 20), "FingerL": (0, 30, 0)}), tail(REST, 20, wave=6, phase=1))
    b.clip("Stagger", [(0, {}), (6, hit), (24, M(hit, {"Pivot": (-4, 4, 2)})), (42, {})])
    dead = M({"Root@": (0, -0.75, 0), "Pivot": (4, 0, 4), "Body": (6, 0, 0)},
             sym({"ArmL": (20, 20, -20), "ForeL": (20, 0, 0), "FingerL": (0, 30, 0)}),
             sym({"Leg%dAL" % i: (0, 0, 30) for i in range(1, 5)}), sym({"Leg%dBL" % i: (0, 0, -70) for i in range(1, 5)}),
             tail(LOW, 30))
    b.clip("Death", [(0, {}), (8, hit), (20, M(hit, {"Root@": (0, 0.3, -0.5)})), (40, dead), (70, dead)])
    return b


def shots(b, folder):
    cam, look = (-11.0, -16.0, 8.0), (0, 0, 2.2)
    side = (18.0, 0.0, 5.5)
    return b.previews(folder, [
        ("scorp_front", None, 0, cam, look),
        ("scorp_side", None, 0, side, (0, 1.5, 3.0)),
        ("scorp_back", None, 0, (-8.0, 16.0, 9.0), (0, 1, 3.0)),
        ("scorp_face", None, 0, (-2.5, -9.5, 3.5), (0, -3.0, 1.6)),
        ("scorp_roar", "Roar", 26, cam, look),
        ("scorp_combo", "PincerCombo", 84, cam, look),
        ("scorp_lash", "TailLash", 30, (-6.0, 18.0, 10.0), (0, 2, 1.5)),
        ("scorp_stab_cock", "Stab", 24, side, (0, 1, 3.5)),
        ("scorp_stab", "Stab", 31, side, (0, -1, 2.0)),
        ("scorp_barrage", "StingerBarrage", 40, side, (0, -1, 2.5)),
        ("scorp_snare", "TailSnare", 25, side, (0, -3, 2.0)),
        ("scorp_snare_crush", "SnareCrush", 20, cam, look),
        ("scorp_rain", "FuelRain", 36, side, (0, 1, 4.0)),
        ("scorp_leap", "LeapCrush", 24, side, (0, 0, 2.5)),
        ("scorp_spin", "InfernoSpin", 32, (-8.0, -14.0, 10.0), look),
        ("scorp_burrow", "BurrowOut", 12, cam, look),
    ])

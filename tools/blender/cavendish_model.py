"""Cavendish (Nicole, 11-08): a capybara on all fours in a spacesuit, described as low-poly primitives.

Running it (1) renders preview images and (2) writes the part table into the Unity builder
(CavendishModelBuilder.cs), so the preview and the game always match.
Unity coordinates: x right, y up, z forward (his nose points +Z), metres, feet on y = 0.
"""
import math
import sys
import numpy as np

# ---------------------------------------------------------------- maths

def quat_from_matrix(m):
    t = m[0, 0] + m[1, 1] + m[2, 2]
    if t > 0:
        s = math.sqrt(t + 1.0) * 2
        w = 0.25 * s; x = (m[2, 1] - m[1, 2]) / s; y = (m[0, 2] - m[2, 0]) / s; z = (m[1, 0] - m[0, 1]) / s
    elif m[0, 0] > m[1, 1] and m[0, 0] > m[2, 2]:
        s = math.sqrt(1.0 + m[0, 0] - m[1, 1] - m[2, 2]) * 2
        w = (m[2, 1] - m[1, 2]) / s; x = 0.25 * s; y = (m[0, 1] + m[1, 0]) / s; z = (m[0, 2] + m[2, 0]) / s
    elif m[1, 1] > m[2, 2]:
        s = math.sqrt(1.0 + m[1, 1] - m[0, 0] - m[2, 2]) * 2
        w = (m[0, 2] - m[2, 0]) / s; x = (m[0, 1] + m[1, 0]) / s; y = 0.25 * s; z = (m[1, 2] + m[2, 1]) / s
    else:
        s = math.sqrt(1.0 + m[2, 2] - m[0, 0] - m[1, 1]) * 2
        w = (m[1, 0] - m[0, 1]) / s; x = (m[0, 2] + m[2, 0]) / s; y = (m[1, 2] + m[2, 1]) / s; z = 0.25 * s
    return np.array([x, y, z, w])


def mat_from_quat(q):
    x, y, z, w = q
    return np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def euler(x=0.0, y=0.0, z=0.0):
    """Unity's Quaternion.Euler(x, y, z): Z first, then X, then Y (degrees)."""
    rx, ry, rz = (math.radians(a) for a in (x, y, z))
    X = np.array([[1, 0, 0], [0, math.cos(rx), -math.sin(rx)], [0, math.sin(rx), math.cos(rx)]])
    Y = np.array([[math.cos(ry), 0, math.sin(ry)], [0, 1, 0], [-math.sin(ry), 0, math.cos(ry)]])
    Z = np.array([[math.cos(rz), -math.sin(rz), 0], [math.sin(rz), math.cos(rz), 0], [0, 0, 1]])
    return quat_from_matrix(Y @ X @ Z)


def look_y(direction):
    """Rotation that turns +Y onto direction."""
    d = np.asarray(direction, float); d /= np.linalg.norm(d)
    a = np.array([1.0, 0, 0]) if abs(d[0]) < 0.9 else np.array([0, 0, 1.0])
    x = np.cross(d, a); x /= np.linalg.norm(x)
    z = np.cross(x, d)
    return quat_from_matrix(np.column_stack([x, d, z]))


# ---------------------------------------------------------------- the model

# Groups are empty pivots under "Body" (the legs swing and tuck about their hips).
GROUPS = {
    'Front Right Leg': ((0.235, 0.50, 0.40), euler()),
    'Front Left Leg': ((-0.235, 0.50, 0.40), euler()),
    'Hind Right Leg': ((0.255, 0.52, -0.42), euler()),
    'Hind Left Leg': ((-0.255, 0.52, -0.42), euler()),
    'Raised helmet frame': ((0.0, 1.04, 0.80), euler()),
}

PARTS = []  # (name, group, prim, pos, quat, scale, material)


def part(name, prim, pos, scale, mat, rot=None, group='Body'):
    PARTS.append((name, group, prim, tuple(float(v) for v in pos), tuple(rot if rot is not None else euler()),
                  tuple(float(v) for v in scale), mat))


def sphere(name, pos, radii, mat, rot=None, group='Body', fine=False):
    part(name, 'Sphere2' if fine else 'Sphere1', pos, radii, mat, rot, group)


def box(name, pos, size, mat, rot=None, group='Body'):
    part(name, 'Cube', pos, size, mat, rot, group)


def cyl(name, pos, radius, length, mat, rot=None, group='Body', sides=10):
    part(name, f'Prism{sides}', pos, (radius, length, radius), mat, rot, group)


def rod(name, a, b, radius, mat, group='Body', sides=6):
    a, b = np.asarray(a, float), np.asarray(b, float)
    part(name, f'Prism{sides}', (a + b) / 2, (radius, np.linalg.norm(b - a), radius), mat, look_y(b - a), group)


def ring(name, pos, major, minor_ratio, mat, rot=None, group='Body', squash=(1.0, 1.0)):
    prim = 'TorusThin' if minor_ratio < 0.09 else 'Torus'
    part(name, prim, pos, (major * squash[0], major, major * squash[1]), mat, rot, group)


def build():
    # --- Suit body: a capybara's barrel, rump a touch higher than the shoulders.
    sphere('Pressure suit', (0, 0.64, -0.05), (0.43, 0.38, 0.66), 'SuitIvory', fine=True)
    sphere('Suit rump', (0, 0.68, -0.38), (0.40, 0.38, 0.38), 'SuitIvory', fine=True)
    sphere('Suit chest', (0, 0.62, 0.30), (0.37, 0.34, 0.37), 'SuitIvory', fine=True)
    ring('Waist seal', (0, 0.67, -0.10), 0.43, 0.11, 'SuitTeal', euler(90, 0, 0), squash=(1.03, 0.93))
    # Suit seams down each flank and a padded belly panel.
    for s in (-1, 1):
        sphere('Shoulder patch', (s * 0.31, 0.72, 0.36), (0.13, 0.15, 0.15), 'SuitTeal')
        sphere('Hip patch', (s * 0.32, 0.74, -0.44), (0.12, 0.14, 0.14), 'SuitTeal')
    box('Belly panel', (0, 0.285, -0.05), (0.28, 0.04, 0.58), 'SuitTeal')
    # Crew patch on his right flank (orange with a teal badge).
    box('Corps patch', (0.425, 0.76, 0.06), (0.03, 0.15, 0.20), 'SafetyOrange', euler(0, 0, -8))
    box('Corps badge', (0.44, 0.765, 0.06), (0.012, 0.08, 0.10), 'SuitTeal', euler(0, 0, -8))

    # --- Chest panel under the neck.
    box('Chest panel housing', (0, 0.58, 0.69), (0.26, 0.15, 0.05), 'Graphite', euler(-12, 0, 0))
    box('Chest screen', (-0.045, 0.59, 0.717), (0.12, 0.07, 0.012), 'SuitTeal', euler(-12, 0, 0))
    sphere('Panel button', (0.065, 0.575, 0.716), (0.018, 0.018, 0.01), 'SignalMint')
    sphere('Panel button', (0.105, 0.575, 0.716), (0.018, 0.018, 0.01), 'SafetyOrange')

    # --- Life support pack on his back, two tanks along it, hoses to the collar.
    box('Life support pack', (0, 1.03, -0.22), (0.40, 0.12, 0.46), 'Graphite')
    box('Pack cover', (0, 1.095, -0.22), (0.32, 0.02, 0.36), 'SuitTeal')
    for s in (-1, 1):
        cyl('Oxygen tank', (s * 0.12, 1.15, -0.23), 0.055, 0.42, 'Steel', euler(90, 0, 0))
        cyl('Tank cap', (s * 0.12, 1.15, -0.005), 0.038, 0.035, 'Graphite', euler(90, 0, 0))
        pts = [(s * 0.17, 1.04, 0.01), (s * 0.25, 1.00, 0.22), (s * 0.26, 0.90, 0.40)]
        for a, b in zip(pts, pts[1:]):
            rod('Breathing hose', a, b, 0.028, 'SafetyOrange')

    # --- Collar where the fishbowl helmet seats, tilted toward the helmet's centre.
    axis = np.array([0, 0.55, 0.83]); axis /= np.linalg.norm(axis)
    collar = np.array(GROUPS['Raised helmet frame'][0]) - axis * 0.36
    cyl('Helmet collar', collar, 0.29, 0.07, 'Graphite', look_y(axis), sides=16)
    ring('Collar rim', collar + axis * 0.035, 0.29, 0.11, 'SafetyOrange', look_y(axis))

    # --- Capybara head: boxy skull, big blunt muzzle, eyes high on the sides, small round ears.
    sphere('Capybara head', (0, 1.04, 0.68), (0.25, 0.25, 0.29), 'Fur')
    sphere('Brow', (0, 1.13, 0.80), (0.21, 0.11, 0.19), 'Fur')
    sphere('Long blunt muzzle', (0, 0.96, 0.94), (0.185, 0.17, 0.22), 'Muzzle')
    sphere('Cheek', (0.13, 0.95, 0.80), (0.10, 0.11, 0.12), 'Muzzle')
    sphere('Cheek', (-0.13, 0.95, 0.80), (0.10, 0.11, 0.12), 'Muzzle')
    box('Nose pad', (0, 1.005, 1.145), (0.18, 0.09, 0.03), 'EarInner', euler(-8, 0, 0))
    for s in (-1, 1):
        sphere('Nostril', (s * 0.052, 1.01, 1.162), (0.022, 0.014, 0.01), 'Eyes')
    box('Mouth line', (0, 0.88, 1.13), (0.11, 0.012, 0.012), 'EarInner')
    box('Chin', (0, 0.83, 1.03), (0.13, 0.03, 0.13), 'Muzzle')
    # Eyes: Outfits looks for "Eye" and "Eye.001" (the Blender model's names) to fit his sunglasses.
    for s, name in ((1, 'Eye'), (-1, 'Eye.001')):
        sphere(name, (s * 0.19, 1.125, 0.83), (0.04, 0.046, 0.042), 'Eyes')
        sphere('Eye glint', (s * 0.205, 1.14, 0.86), (0.012, 0.012, 0.008), 'Highlight')
        sphere('Small round ear', (s * 0.16, 1.27, 0.58), (0.07, 0.064, 0.042), 'Fur')
        sphere('Ear inset', (s * 0.16, 1.27, 0.606), (0.044, 0.04, 0.012), 'EarInner')

    # --- Fishbowl helmet: clear glass, an ivory rim round the face, hinges and a lamp on top.
    sphere('Helmet glass', (0, 0, 0), (0.43, 0.43, 0.43), 'HelmetGlass', group='Raised helmet frame', fine=True)
    box('Helmet lamp', (0, 0.435, -0.05), (0.12, 0.07, 0.12), 'Graphite', group='Raised helmet frame')
    box('Lamp lens', (0, 0.435, 0.014), (0.08, 0.035, 0.01), 'SignalMint', group='Raised helmet frame')

    # --- Legs: suit sleeve, teal cuff, bare capybara foot with dark toes.
    for leg in ('Front Right Leg', 'Front Left Leg', 'Hind Right Leg', 'Hind Left Leg'):
        hind = leg.startswith('Hind')
        hip_y = GROUPS[leg][0][1]
        top, ankle = -0.02, 0.15 - hip_y      # group-local heights
        cyl('Suit sleeve', (0, (top + ankle) / 2, 0), 0.115, top - ankle, 'SuitIvory', group=leg, sides=12)
        sphere('Leg top', (0, -0.02, 0), (0.15, 0.10, 0.15), 'SuitIvory', group=leg)
        cyl('Ankle cuff', (0, ankle, 0), 0.122, 0.06, 'SuitTeal', group=leg, sides=12)
        foot_z = 0.035 if not hind else 0.0
        sphere('Foot', (0, 0.06 - hip_y, foot_z), (0.11, 0.065, 0.14), 'Fur', group=leg)
        toes = (-0.055, 0.0, 0.055) if hind else (-0.06, -0.02, 0.02, 0.06)
        for tx in toes:
            sphere('Toe', (tx, 0.03 - hip_y, foot_z + 0.12), (0.028, 0.03, 0.036), 'EarInner', group=leg)


build()

COLORS = {
    'SuitIvory': '#D9DFDB', 'SuitTeal': '#287F86', 'SafetyOrange': '#E68435', 'Graphite': '#29343D',
    'Steel': '#8CABB6', 'Fur': '#9B704A', 'Muzzle': '#B98B5E', 'EarInner': '#67452F', 'Eyes': '#111A21',
    'Highlight': '#F4F5E4', 'SignalMint': '#8CE7C6', 'HelmetGlass': '#CFEFFF',
}

# ---------------------------------------------------------------- primitives (same algorithms as the C# builder)

def icosphere(sub):
    t = (1 + 5 ** 0.5) / 2
    v = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
         (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    v = [np.array(p) / np.linalg.norm(p) for p in v]
    f = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6),
         (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7),
         (9, 8, 1)]
    for _ in range(sub):
        cache, nf = {}, []
        def mid(i, j):
            k = (min(i, j), max(i, j))
            if k not in cache:
                p = (v[i] + v[j]) / 2; v.append(p / np.linalg.norm(p)); cache[k] = len(v) - 1
            return cache[k]
        for a, b, c in f:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
            nf += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        f = nf
    return [np.array([v[a], v[b], v[c]]) for a, b, c in f]


def cube():
    tris = []
    for axis in range(3):
        for s in (-0.5, 0.5):
            u, w = [(axis + 1) % 3, (axis + 2) % 3]
            corners = []
            for a, b in ((-0.5, -0.5), (0.5, -0.5), (0.5, 0.5), (-0.5, 0.5)):
                p = np.zeros(3); p[axis] = s; p[u] = a; p[w] = b; corners.append(p)
            tris += [np.array([corners[0], corners[1], corners[2]]), np.array([corners[0], corners[2], corners[3]])]
    return tris


def prism(sides):
    tris = []
    for i in range(sides):
        a0, a1 = 2 * math.pi * i / sides, 2 * math.pi * (i + 1) / sides
        p0, p1 = np.array([math.cos(a0), 0, math.sin(a0)]), np.array([math.cos(a1), 0, math.sin(a1)])
        top, bot = np.array([0, 0.5, 0]), np.array([0, -0.5, 0])
        tris += [np.array([p0 + bot, p1 + bot, p1 + top]), np.array([p0 + bot, p1 + top, p0 + top]),
                 np.array([top, p0 + top, p1 + top]), np.array([bot, p1 + bot, p0 + bot])]
    return tris


def torus(minor, major_n=16, minor_n=6):
    tris = []
    def pt(i, j):
        u, v = 2 * math.pi * i / major_n, 2 * math.pi * j / minor_n
        r = 1 + minor * math.cos(v)
        return np.array([r * math.cos(u), minor * math.sin(v), r * math.sin(u)])
    for i in range(major_n):
        for j in range(minor_n):
            a, b, c, d = pt(i, j), pt(i + 1, j), pt(i + 1, j + 1), pt(i, j + 1)
            tris += [np.array([a, b, c]), np.array([a, c, d])]
    return tris


PRIMS = {'Sphere1': icosphere(1), 'Sphere2': icosphere(2), 'Cube': cube(), 'Prism6': prism(6), 'Prism10': prism(10),
         'Prism12': prism(12), 'Prism16': prism(16), 'Torus': torus(0.11), 'TorusThin': torus(0.07)}


def world_triangles(pose='stand'):
    group_m = {}
    for g, (pos, q) in GROUPS.items():
        R = mat_from_quat(q)
        if pose == 'loaf' and 'Leg' in g:
            front = g.startswith('Front')
            R = R @ mat_from_quat(euler(-75 if front else 0, 0, 0)) @ np.diag([1, 0.7 if front else 0.2, 1])
        group_m[g] = (np.array(pos), R)
    body_off = np.array([0, -0.24, 0]) if pose == 'loaf' else np.zeros(3)
    out = []
    for name, group, prim, pos, q, scale, mat in PARTS:
        M = mat_from_quat(q) @ np.diag(scale)
        for tri in PRIMS[prim]:
            p = tri @ M.T + np.array(pos)
            if group != 'Body':
                gp, gR = group_m[group]
                p = p @ gR.T + gp
            out.append((p + body_off, mat))
    return out


def render(path):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    from mpl_toolkits.mplot3d.art3d import Poly3DCollection
    light = np.array([0.4, 0.8, 0.5]); light /= np.linalg.norm(light)
    views = [('stand', 18, 35, 'Front three-quarter'), ('stand', 5, 0, 'Side'), ('stand', 8, 90, 'Front'),
             ('stand', 30, -140, 'Rear three-quarter'), ('loaf', 18, 35, 'Seated (loaf)')]
    fig = plt.figure(figsize=(25, 5.5))
    for k, (pose, elev, azim, title) in enumerate(views):
        ax = fig.add_subplot(1, len(views), k + 1, projection='3d')
        tris, cols = [], []
        for p, mat in world_triangles(pose):
            n = np.cross(p[1] - p[0], p[2] - p[0]); ln = np.linalg.norm(n)
            if ln < 1e-12: continue
            n /= ln
            shade = 0.45 + 0.55 * abs(n @ light)
            rgb = np.array(matplotlib.colors.to_rgb(COLORS[mat])) * shade
            alpha = 0.12 if mat == 'HelmetGlass' else 1.0
            tris.append(p[:, [0, 2, 1]]); cols.append((*np.clip(rgb, 0, 1), alpha))
        ax.add_collection3d(Poly3DCollection(tris, facecolors=cols, edgecolors=(0, 0, 0, 0.08), linewidths=0.2))
        ax.set_xlim(-0.8, 0.8); ax.set_ylim(-0.8, 1.2); ax.set_zlim(0, 1.5)
        ax.set_box_aspect((1.6, 2.0, 1.5)); ax.dist = 6; ax.view_init(elev, azim); ax.set_axis_off(); ax.set_title(title)
    plt.tight_layout(); plt.savefig(path, dpi=80)


# ---------------------------------------------------------------- C# part table

def csharp_table():
    lines = []
    for g, (pos, q) in GROUPS.items():
        lines.append(f'            new Group("{g}", V({pos[0]:.4f}f, {pos[1]:.4f}f, {pos[2]:.4f}f)),')
    groups = '\n'.join(lines)
    lines = []
    for name, group, prim, pos, q, scale, mat in PARTS:
        lines.append(f'            new Part("{name}", "{group}", Prim.{prim}, V({pos[0]:.4f}f, {pos[1]:.4f}f, {pos[2]:.4f}f), '
                     f'Q({q[0]:.5f}f, {q[1]:.5f}f, {q[2]:.5f}f, {q[3]:.5f}f), V({scale[0]:.4f}f, {scale[1]:.4f}f, {scale[2]:.4f}f), "{mat}"),')
    return groups, '\n'.join(lines)


def write_builder(path):
    """Replaces the part table in CavendishModelBuilder.cs (between // <generated> and // </generated>)."""
    groups, parts = csharp_table()
    text = open(path, encoding='utf-8').read()
    start = text.index('        // <generated>\n') + len('        // <generated>\n')
    end = text.index('        // </generated>')
    table = ('        private static readonly Group[] Groups =\n        {\n' + groups + '\n        };\n\n'
             '        private static readonly Part[] Parts =\n        {\n' + parts + '\n        };\n')
    open(path, 'w', encoding='utf-8', newline='\n').write(text[:start] + table + text[end:])


if __name__ == '__main__':
    # python3 cavendish_model.py PREVIEW.png [--builder signal-lost/Assets/Editor/CavendishModelBuilder.cs]
    out = sys.argv[1] if len(sys.argv) > 1 else 'preview.png'
    if '--builder' in sys.argv:
        write_builder(sys.argv[sys.argv.index('--builder') + 1])
    render(out)
    tris = world_triangles()
    pts = np.concatenate([p for p, _ in tris])
    print('parts', len(PARTS), 'bounds min', pts.min(0).round(3), 'max', pts.max(0).round(3))

# Mudlands buildings (docs/mudlands.md, "Buildings rebuilt in Blender"): every house, hall, shed and stand of the
# Mudlands as closed low-poly solids, flat shaded, in Christian's swamp look (husk walls with pale ribs, giant mushroom
# caps with gills underneath and glowing spots, leaf shingles, root stilts, round mint windows).
#
# Run inside Blender (Blender MCP: exec(open(path).read()), or blender --background --python tools/blender/mudlands_buildings.py).
# Builds every model in its own scene "MudlandsBuildings", checks it, exports one FBX per model for Unity and saves the
# .blend. Geometry is written in Unity's axes (x right, y up, z forward = the building's front) and turned into
# Blender's (Z up, front facing -Y) on the way out, so the numbers match MudModels.cs and the level builder.
#
# Every part is a closed solid. The checks (printed, and the run fails if one finds something):
#   open    - a part with an edge that doesn't have exactly two faces (a hole you could see through)
#   coplanar- two parts with faces in the same plane, facing the same way, overlapping (z-fighting)
#   floating- a part that touches nothing on a chain down to the ground (y <= 0)
# Material names are the keys MudlandsBuilder maps onto its own URP materials (MB_*); MB_Cap is coloured per variant.
import bpy, bmesh, math, os
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

REPO = os.environ.get("SIGNAL_LOST_REPO", r"C:\Users\Lawson\signal-lost\.claude\worktrees\mudlands-buildings-5cacb8")
MODELS = os.path.join(REPO, "signal-lost", "Assets", "Prefabs", "Enviornment objects", "Mudlands", "Models")
ART = os.path.join(REPO, "art", "mudlands-buildings")
ONLY = os.environ.get("MUD_BUILDINGS_ONLY", "")  # comma list of model names to rebuild, empty for all
EXPORT = os.environ.get("MUD_BUILDINGS_EXPORT", "1") == "1"
TOLERANCE = 0.02  # faces closer than 2 cm in the same plane count as coplanar (24-bit depth can flicker at 1 cm far off)

# ---------------------------------------------------------------- scene and materials
scene = bpy.data.scenes.get("MudlandsBuildings") or bpy.data.scenes.new("MudlandsBuildings")
for o in list(scene.objects):
    bpy.data.objects.remove(o, do_unlink=True)
if bpy.context.window is not None:
    bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0

def linear(c):
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)

# Unity colours (sRGB, from MudModels.cs / MudPalette) and glow. Preview only: Unity assigns its own materials by name.
COLOURS = {
    "MB_Husk": ((0.56, 0.5, 0.62), 0), "MB_HuskDark": ((0.36, 0.3, 0.44), 0), "MB_Rib": ((0.84, 0.8, 0.7), 0),
    "MB_Gill": ((0.86, 0.7, 0.78), 0), "MB_Cap": ((0.2, 0.5, 0.52), 0), "MB_Spot": ((0.75, 1.0, 0.88), 1.6),
    "MB_Window": ((0.5, 1.0, 0.75), 2.2), "MB_Wood": ((0.52, 0.45, 0.58), 0), "MB_DarkWood": ((0.32, 0.26, 0.38), 0),
    "MB_GreyWood": ((0.62, 0.6, 0.68), 0), "MB_Bark": ((0.28, 0.22, 0.32), 0), "MB_Leaf": ((0.16, 0.4, 0.42), 0),
    "MB_LeafDark": ((0.12, 0.27, 0.36), 0), "MB_Moss": ((0.52, 0.4, 0.64), 0), "MB_Orange": ((0.93, 0.45, 0.12), 0),
    "MB_Stone": ((0.45, 0.48, 0.47), 0), "MB_Metal": ((0.55, 0.57, 0.6), 0), "MB_DarkMetal": ((0.2, 0.21, 0.23), 0),
    "MB_Black": ((0.06, 0.06, 0.07), 0), "MB_Hull": ((0.8, 0.79, 0.74), 0), "MB_HullDark": ((0.4, 0.43, 0.47), 0),
    "MB_HullStripe": ((0.93, 0.45, 0.12), 0), "MB_Teal": ((0.3, 0.95, 0.85), 2.0), "MB_ShipGlow": ((0.3, 0.95, 0.85), 1.5),
    "MB_Red": ((0.8, 0.2, 0.15), 0), "MB_Cream": ((0.93, 0.86, 0.68), 0), "MB_Rust": ((0.55, 0.27, 0.13), 0),
    "MB_Banana": ((1.0, 0.86, 0.25), 0), "MB_Awning": ((0.85, 0.15, 0.1), 0), "MB_Lacquer": ((0.45, 0.2, 0.12), 0),
    "MB_WitchHat": ((0.36, 0.2, 0.46), 0), "MB_Gourd": ((0.86, 0.55, 0.22), 0), "MB_Rubber": ((0.11, 0.11, 0.12), 0),
}

def material(name):
    rgb, glow = COLOURS[name]
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    lin = linear(rgb)
    p.inputs["Base Color"].default_value = (*lin, 1.0)
    p.inputs["Roughness"].default_value = 0.85
    if glow:
        p.inputs["Emission Color"].default_value = (*lin, 1.0)
        p.inputs["Emission Strength"].default_value = glow
    m.diffuse_color = (*lin, 1.0)
    return m

# ---------------------------------------------------------------- helpers (Unity axes)
UP = Vector((0, 1, 0))

def V(x, y, z):
    return Vector((x, y, z))

def rot(axis, deg):
    return Matrix.Rotation(math.radians(deg), 3, axis)

def ring2d(points, origin, u, v):
    return [origin + u * a + v * b for a, b in points]

def octagon(r, n=8, phase=22.5):
    return [(r * math.cos(math.radians(phase + i * 360 / n)), r * math.sin(math.radians(phase + i * 360 / n))) for i in range(n)]

def arch(hw, h, bottom, segs=7):
    """A round-topped door outline (counter-clockwise, u across, v up); the arch springs at h - hw."""
    pts = [(-hw, bottom), (hw, bottom)]
    spring = h - hw
    for i in range(segs + 1):
        t = math.pi * i / segs
        pts.append((hw * math.cos(t), spring + hw * math.sin(t)))
    return pts

def lerp(a, b, t):
    return a + (b - a) * t

class Frame:
    """A local frame on a surface: u across, v up along it, w out of it."""
    def __init__(self, origin, w, up=UP):
        self.o = Vector(origin)
        self.w = Vector(w).normalized()
        u = Vector(up).cross(self.w)
        if u.length < 1e-6:
            u = Vector((1, 0, 0))
        self.u = u.normalized()
        self.v = self.w.cross(self.u).normalized()

    def at(self, a, b, c=0.0):
        return self.o + self.u * a + self.v * b + self.w * c

# ---------------------------------------------------------------- the kit
class Kit:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.keys = []
        self.parts = []  # (label, [faces])

    def _mi(self, key):
        if key not in self.keys:
            self.keys.append(key)
        return self.keys.index(key)

    def begin(self, label):
        self.cur = []
        self.parts.append((label, self.cur))

    def face(self, verts, key):
        f = self.bm.faces.new(verts)
        f.material_index = self._mi(key)
        self.cur.append(f)
        return f

    def loft(self, rings, keys, label, cap0=True, cap1=True, closed=False, cap_keys=None):
        """Rings of points (equal counts, or 1 for a point on the axis). keys: one key or one per band."""
        self.begin(label)
        vr = [[self.bm.verts.new(p) for p in r] for r in rings]
        n = max(len(r) for r in rings)
        bands = len(vr) if closed else len(vr) - 1
        for i in range(bands):
            a, b = vr[i], vr[(i + 1) % len(vr)]
            k = keys[i] if isinstance(keys, list) else keys
            if len(a) == 1 and len(b) == 1:
                continue
            for j in range(n):
                j1 = (j + 1) % n
                if len(a) == 1:
                    self.face([a[0], b[j], b[j1]], k)
                elif len(b) == 1:
                    self.face([a[j], a[j1], b[0]], k)
                else:
                    self.face([a[j], a[j1], b[j1], b[j]], k)
        if not closed:
            k0 = cap_keys[0] if cap_keys else (keys[0] if isinstance(keys, list) else keys)
            k1 = cap_keys[1] if cap_keys else (keys[-1] if isinstance(keys, list) else keys)
            if cap0 and len(vr[0]) > 2:
                self.face(list(reversed(vr[0])), k0)
            if cap1 and len(vr[-1]) > 2:
                self.face(vr[-1], k1)
        return self.cur

    # --- solids
    def box(self, centre, size, key, label="box", R=None):
        R = R or Matrix.Identity(3)
        c, (sx, sy, sz) = Vector(centre), size
        def ring(y):
            return [c + R @ V(x * sx / 2, y * sy / 2, z * sz / 2) for x, z in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        return self.loft([ring(-1), ring(1)], key, label)

    def slab(self, a, b, half_width, thick, key, label="slab", side=None):
        """A board from point a to point b (centre line of its top face), half_width across, thick down."""
        a, b = Vector(a), Vector(b)
        t = (b - a).normalized()
        s = (side or t.cross(UP)).normalized()
        if s.length < 1e-6:
            s = Vector((1, 0, 0))
        n = s.cross(t).normalized()
        if n.y < 0:
            n = -n
        def ring(p):
            return [p + s * half_width, p - s * half_width, p - s * half_width - n * thick, p + s * half_width - n * thick]
        return self.loft([ring(a), ring(b)], key, label)

    def prism(self, frame, outline, w0, w1, key, label="prism", cap_keys=None):
        bot = [frame.at(a, b, w0) for a, b in outline]
        top = [frame.at(a, b, w1) for a, b in outline]
        return self.loft([bot, top], key, label, cap_keys=cap_keys)

    def annulus(self, frame, outer, inner, w0, w1, key, label="frame"):
        rings = [[frame.at(a, b, w1) for a, b in outer], [frame.at(a, b, w0) for a, b in outer],
                 [frame.at(a, b, w0) for a, b in inner], [frame.at(a, b, w1) for a, b in inner]]
        return self.loft(rings, key, label, closed=True)

    def lathe(self, centre, profile, sides, keys, label="lathe", phase=0.0, R=None, cap_keys=None):
        """profile: (radius, height) pairs; a radius under 2 mm is a point on the axis."""
        R = R or Matrix.Identity(3)
        c = Vector(centre)
        rings = []
        for r, y in profile:
            if r < 0.002:
                rings.append([c + R @ V(0, y, 0)])
            else:
                rings.append([c + R @ V(r * math.cos(math.radians(phase + i * 360 / sides)), y,
                                         r * math.sin(math.radians(phase + i * 360 / sides))) for i in range(sides)])
        return self.loft(rings, keys, label, cap_keys=cap_keys)

    def tube(self, path, radii, sides, key, label="tube", cap_keys=None):
        path = [Vector(p) for p in path]
        if not isinstance(radii, (list, tuple)):
            radii = [radii] * len(path)
        rings, u = [], None
        for i, p in enumerate(path):
            t = (path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)]).normalized()
            if u is None:
                u = t.cross(UP if abs(t.y) < 0.95 else Vector((1, 0, 0))).normalized()
            else:
                u = (u - t * u.dot(t)).normalized()
            w = t.cross(u).normalized()
            rings.append([p + (u * math.cos(2 * math.pi * s / sides) + w * math.sin(2 * math.pi * s / sides)) * radii[i] for s in range(sides)])
        return self.loft(rings, key, label, cap_keys=cap_keys)

    def blade(self, path, widths, thick, key, normal_hint, label="blade"):
        """A thin closed blade (leaf, sail, moss strand) along a path; widths are half-widths; normal_hint is its face."""
        path = [Vector(p) for p in path]
        rings = []
        for i, p in enumerate(path):
            t = (path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)]).normalized()
            s = t.cross(Vector(normal_hint)).normalized()
            n = s.cross(t).normalized()
            if n.dot(Vector(normal_hint)) < 0:
                n = -n
            w = max(widths[i], 0.006)
            rings.append([p + s * w + n * thick, p - s * w + n * thick, p - s * w - n * thick, p + s * w - n * thick])
        return self.loft(rings, key, label)

    def ball(self, centre, radii, key, label="ball", subdiv=1, R=None):
        self.begin(label)
        R = R or Matrix.Identity(3)
        if not isinstance(radii, (list, tuple, Vector)):
            radii = (radii, radii, radii)
        ret = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=1.0)
        for v in ret["verts"]:
            v.co = Vector(centre) + R @ V(v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2])
        k = self._mi(key)
        for f in {f for v in ret["verts"] for f in v.link_faces}:
            f.material_index = k
            self.cur.append(f)
        return self.cur

    # --- queries while building
    def tree(self, labels):
        faces = [f for lab, fs in self.parts if lab in labels for f in fs]
        verts = list({v for f in faces for v in f.verts})
        index = {v: i for i, v in enumerate(verts)}
        return BVHTree.FromPolygons([v.co.copy() for v in verts], [[index[v] for v in f.verts] for f in faces])

    def under(self, labels, x, z, y_from=0.5):
        """Height of the first surface of these parts straight up from (x, y_from, z)."""
        hit = self.tree(labels).ray_cast(V(x, y_from, z), UP)
        if hit[0] is None:
            raise RuntimeError(f"{self.name}: nothing above ({x:.2f}, {z:.2f}) in {labels}")
        return hit[0].y

# ---------------------------------------------------------------- checks
def tri_area2(pts):
    a = 0.0
    for i in range(len(pts)):
        x0, y0 = pts[i]
        x1, y1 = pts[(i + 1) % len(pts)]
        a += x0 * y1 - x1 * y0
    return abs(a) * 0.5

def clip(subject, clipper):
    """Sutherland-Hodgman: convex polygon subject clipped by convex polygon clipper (both 2D, CCW)."""
    def inside(p, a, b):
        return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0]) >= -1e-9
    def inter(p, q, a, b):
        x1, y1, x2, y2 = p[0], p[1], q[0], q[1]
        x3, y3, x4, y4 = a[0], a[1], b[0], b[1]
        den = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4)
        if abs(den) < 1e-12:
            return q
        t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / den
        return (x1 + t * (x2 - x1), y1 + t * (y2 - y1))
    out = subject
    for i in range(len(clipper)):
        a, b = clipper[i], clipper[(i + 1) % len(clipper)]
        inp, out = out, []
        if not inp:
            break
        for j in range(len(inp)):
            p, q = inp[j], inp[(j + 1) % len(inp)]
            if inside(q, a, b):
                if not inside(p, a, b):
                    out.append(inter(p, q, a, b))
                out.append(q)
            elif inside(p, a, b):
                out.append(inter(p, q, a, b))
    return out

def ccw(pts):
    a = sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))
    return pts if a > 0 else list(reversed(pts))

def check(kit):
    bm = kit.bm
    bm.verts.ensure_lookup_table()
    part_of = {}
    for i, (lab, faces) in enumerate(kit.parts):
        for f in faces:
            part_of[f] = i
    problems = []
    # open edges
    for i, (lab, faces) in enumerate(kit.parts):
        edges = {e for f in faces for e in f.edges}
        bad = [e for e in edges if len(e.link_faces) != 2]
        if bad:
            problems.append(f"open: {lab}#{i} has {len(bad)} edges without two faces")
    # coplanar, same facing, overlapping faces of different parts
    tris = []
    for loops in bm.calc_loop_triangles():
        f = loops[0].face
        pts = [l.vert.co.copy() for l in loops]
        n = (pts[1] - pts[0]).cross(pts[2] - pts[0])
        if n.length < 1e-9:
            continue
        n.normalize()
        tris.append((part_of[f], pts, n, n.dot(pts[0])))
    buckets = {}
    for idx, (p, pts, n, d) in enumerate(tris):
        key = (round(n.x * 10), round(n.y * 10), round(n.z * 10), round(d * 50))
        buckets.setdefault(key, []).append(idx)
    seen = {}
    for idx, (p, pts, n, d) in enumerate(tris):
        k0 = (round(n.x * 10), round(n.y * 10), round(n.z * 10), round(d * 50))
        u = n.orthogonal().normalized()
        w = n.cross(u)
        A = ccw([(q.dot(u), q.dot(w)) for q in pts])
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    for dd in (-1, 0, 1):
                        for jdx in buckets.get((k0[0] + dx, k0[1] + dy, k0[2] + dz, k0[3] + dd), ()):
                            if jdx <= idx:
                                continue
                            q, qpts, m, e = tris[jdx]
                            if q == p or n.dot(m) < 0.9994:
                                continue
                            gaps = [n.dot(v) - d for v in qpts]
                            if max(abs(g) for g in gaps) > TOLERANCE:
                                continue
                            if min(gaps) < -0.002 and max(gaps) > 0.002:
                                continue  # the faces cross: an intersection line, not two faces lying in one plane
                            B = ccw([(v.dot(u), v.dot(w)) for v in qpts])
                            area = tri_area2(clip(A, B)) if len(clip(A, B)) >= 3 else 0.0
                            if area > 2e-4:
                                pair = tuple(sorted((p, q)))
                                seen[pair] = seen.get(pair, 0.0) + area
    for (p, q), area in seen.items():
        problems.append(f"coplanar: {kit.parts[p][0]}#{p} and {kit.parts[q][0]}#{q} overlap {area * 1e4:.0f} cm2")
    # floating parts
    trees, boxes = [], []
    for lab, faces in kit.parts:
        verts = list({v for f in faces for v in f.verts})
        index = {v: i for i, v in enumerate(verts)}
        trees.append((BVHTree.FromPolygons([v.co.copy() for v in verts], [[index[v] for v in f.verts] for f in faces]), [v.co.copy() for v in verts]))
        lo = Vector((min(v.co.x for v in verts), min(v.co.y for v in verts), min(v.co.z for v in verts)))
        hi = Vector((max(v.co.x for v in verts), max(v.co.y for v in verts), max(v.co.z for v in verts)))
        boxes.append((lo, hi))
    def inside(point, tree):
        hits, origin, direction = 0, point.copy(), Vector((0.5773, 0.5774, 0.5772))
        for _ in range(64):
            loc, nrm, i, dist = tree.ray_cast(origin, direction)
            if loc is None:
                break
            hits += 1
            origin = loc + direction * 1e-4
        return hits % 2 == 1
    n = len(kit.parts)
    links = [set() for _ in range(n)]
    for i in range(n):
        for j in range(i + 1, n):
            (lo1, hi1), (lo2, hi2) = boxes[i], boxes[j]
            if any(lo1[a] > hi2[a] + 0.004 or lo2[a] > hi1[a] + 0.004 for a in range(3)):
                continue
            ti, vi = trees[i]
            tj, vj = trees[j]
            touch = bool(ti.overlap(tj))
            if not touch:
                touch = any(tj.find_nearest(v)[3] is not None and tj.find_nearest(v)[3] < 0.004 for v in vi) or \
                        any(ti.find_nearest(v)[3] is not None and ti.find_nearest(v)[3] < 0.004 for v in vj)
            if not touch:
                touch = inside(vi[0], tj) or inside(vj[0], ti)
            if touch:
                links[i].add(j)
                links[j].add(i)
    grounded = [i for i in range(n) if boxes[i][0].y <= 0.001]
    reached, stack = set(grounded), list(grounded)
    while stack:
        i = stack.pop()
        for j in links[i]:
            if j not in reached:
                reached.add(j)
                stack.append(j)
    for i in range(n):
        if i not in reached:
            problems.append(f"floating: {kit.parts[i][0]}#{i} (lowest {boxes[i][0].y:.2f})")
    return problems

# ---------------------------------------------------------------- finishing: to Blender axes, one object
def finish(kit):
    for lab, faces in kit.parts:
        bmesh.ops.recalc_face_normals(kit.bm, faces=faces)  # outward, per closed part
    problems = check(kit)
    for v in kit.bm.verts:
        x, y, z = v.co
        v.co = Vector((-x, -z, y))  # Unity (x, y, z) -> Blender; a mirror, so...
    bmesh.ops.reverse_faces(kit.bm, faces=list(kit.bm.faces))  # ...turn the faces back outward
    me = bpy.data.meshes.get(kit.name)
    if me is not None:
        bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new(kit.name)
    kit.bm.to_mesh(me)
    tris = sum(len(f.verts) - 2 for f in kit.bm.faces)
    kit.bm.free()
    for k in kit.keys:
        me.materials.append(material(k))
    for poly in me.polygons:
        poly.use_smooth = False
    ob = bpy.data.objects.new(kit.name, me)
    scene.collection.objects.link(ob)
    return ob, problems, tris

# ---------------------------------------------------------------- shared swamp pieces
def wall_facet(r0, y0, r1, y1, sides, phase, a_mid, y, centre=V(0, 0, 0)):
    """A frame on the middle of a lathe wall's facet (straight band r0@y0 .. r1@y1) at height y, facing out."""
    half = math.pi / sides
    t = (y - y0) / (y1 - y0)
    d = V(math.cos(math.radians(a_mid)), 0, math.sin(math.radians(a_mid)))
    p = Vector(centre) + d * (lerp(r0, r1, t) * math.cos(half)) + UP * y
    s = d * ((r1 - r0) * math.cos(half)) + UP * (y1 - y0)
    across = V(-d.z, 0, d.x)
    n = s.cross(across).normalized()
    if n.dot(d) < 0:
        n = -n
    return Frame(p, n, s.normalized())

def window(k, frame, r, label="window"):
    k.annulus(frame, octagon(r * 1.3), octagon(r * 0.85), -0.06, 0.09, "MB_Rib", label + " frame")
    k.prism(frame, octagon(r * 0.95), -0.11, 0.035, "MB_Window", label + " pane")

def door(k, frame, hw, h, label="door", knob=True, bottom=-0.35):
    k.annulus(frame, arch(hw + 0.12, h + 0.12, bottom), arch(hw, h, bottom + 0.15), -0.07, 0.08, "MB_Rib", label + " frame")
    k.prism(frame, arch(hw + 0.05, h + 0.05, bottom + 0.1), -0.12, 0.03, "MB_DarkWood", label + " leaf")
    if knob:
        k.ball(frame.at(hw * 0.55, h * 0.42, 0.05), 0.05, "MB_Rib", label + " knob", subdiv=1)

def cap_profile(R, H):
    return [(0.0, 0.1), (0.45 * R, -0.06), (0.9 * R, -0.22), (R, 0.05), (0.88 * R, 0.42 * H), (0.6 * R, 0.78 * H), (0.28 * R, 0.97 * H), (0.0, H)]

def cap(k, centre, R, H, sides=12, label="cap", key="MB_Cap", phase=0.0, R3=None, spots=10, profile=None):
    """A giant mushroom cap: gills underneath (closed solid), glowing spots on top along its ridges."""
    prof = profile or cap_profile(R, H)
    k.lathe(centre, prof, sides, ["MB_Gill", "MB_Gill", key, key, key, key, key], label, phase=phase, R=R3)
    R3 = R3 or Matrix.Identity(3)
    dome = prof[3:]
    for i in range(spots):
        outer = i % 2 == 0
        r = (0.74 if outer else 0.44) * R
        for j in range(len(dome) - 1):
            (ra, ya), (rb, yb) = dome[j], dome[j + 1]
            if rb <= r <= ra:
                y = lerp(ya, yb, (ra - r) / (ra - rb))
                dr, dy = ra - rb, ya - yb  # inner to outer
                break
        a = math.radians(phase + round(i * sides / spots) * 360 / sides)  # on a ridge of the faceted cap
        d = V(math.cos(a), 0, math.sin(a))
        nrm = (d * dy + UP * dr).normalized()
        nrm = R3 @ nrm
        at = Vector(centre) + R3 @ (d * r + UP * y)
        q = UP.rotation_difference(nrm).to_matrix()
        k.ball(at, (R * 0.075, R * 0.034, R * 0.075), "MB_Spot", label + " spot", subdiv=1, R=q)

def ribs(k, centre, r0, y0, r1, y1, sides, phase, radius=0.07, skip=()):
    for i in range(sides):
        if i in skip:
            continue
        a = math.radians(phase + i * 360 / sides)
        d = V(math.cos(a), 0, math.sin(a))
        c = Vector(centre)
        k.tube([c + d * (r0 + radius * 0.4) + UP * y0, c + d * (lerp(r0, r1, 0.5) + radius * 0.9) + UP * lerp(y0, y1, 0.5),
                c + d * (r1 + radius * 0.4) + UP * y1], radius, 5, "MB_Rib", "rib")

def moss(k, top, length, sway=V(0.05, 0, 0.05)):
    for i in range(3):
        t = Vector(top) + V(i * 0.14 - 0.14, 0, (i % 2) * 0.09)
        L = length - i * 0.25
        k.blade([t + UP * 0.08, t - UP * L * 0.5 + sway * 0.5, t - UP * L + sway], [0.06, 0.05, 0.025], 0.012, "MB_Moss", V(0, 0, 1), "moss")

def shelf_fungi(k, frame, size, key="MB_Cap"):
    for i in range(3):
        c = frame.at((i - 1) * size * 0.9, (i % 2) * size * 0.7, 0)
        r = size * (1 - 0.18 * i)
        q = Matrix((frame.u, frame.w, frame.v)).transposed()  # local x across, y out of the wall, z up
        k.ball(c, (r, r * 0.75, r * 0.22), key, "fungus", subdiv=1, R=q)
        k.ball(c - frame.v * r * 0.07 + frame.w * r * 0.06, (r * 0.92, r * 0.7, r * 0.12), "MB_Spot", "fungus rim", subdiv=1, R=q)

def shingles(k, e0, e1, r0, r1, rows, across, label="shingle", lift=0.08, keys=("MB_Leaf", "MB_LeafDark")):
    """Leaf shingles over a roof plane (eave e0..e1, ridge r0..r1), rows overlapping like tiles. Each leaf's lower end
    stands 8 cm proud and its upper end tucks 5 cm into the roof under the next row, so where rows overlap they are at
    least 6 cm apart; leaves in a row sit side by side without overlapping (no two leaves ever share a plane)."""
    e0, e1, r0, r1 = map(Vector, (e0, e1, r0, r1))
    n = (r0 - e0).cross(e1 - e0).normalized()
    if n.y < 0:
        n = -n
    span = (e1 - e0).length
    width = span / across * 0.48
    for row in range(rows):
        t0 = row / rows - (0.06 if row == 0 else 0.0)
        t1 = min(1.0, (row + 1.4) / rows)
        cols = across + (row % 2)
        for c in range(cols):
            u = (c + (0.5 if row % 2 == 0 else 0.0)) / across
            w = width
            if row % 2 == 1 and c in (0, across):  # the offset rows end in half leaves at the roof's edges
                u = 0.25 / across if c == 0 else 1.0 - 0.25 / across
                w = width * 0.5
            eave, ridge = e0.lerp(e1, u), r0.lerp(r1, u)
            stagger = 0.02 * ((row + c) % 3) / rows  # leaf ends don't line up either (their end faces would share planes)
            low = eave.lerp(ridge, t0 + stagger) + n * lift
            high = eave.lerp(ridge, t1 if t1 < 1.0 else 1.0 - stagger) + n * -0.05
            mid = low.lerp(high, 0.45) + n * 0.012
            k.blade([low, mid, high], [w * 0.75, w, w * 0.55], 0.02, keys[(row + c) % 2], n, label)

# ---------------------------------------------------------------- buildings
def hut_wall(k, centre, r0, r1, top, sides, phase):
    k.lathe(centre, [(r0, -0.5), (r1, top)], sides, "MB_Husk", "wall", phase=phase)

def mud_cabin(name, chapel=False):
    """The Flats cabin (and, with chapel=True, the sunken chapel): a round husk hut under a mushroom cap."""
    k = Kit(name)
    sides, phase = 10, 90 - 18
    R, H, y0 = 2.9, (2.2 if chapel else 1.5), 2.45
    cap(k, V(0, y0, 0), R, H, 12, "cap", phase=0)
    r0, r1 = 2.25, 2.05
    top = k.under(["cap"], r1, 0) + 0.15
    hut_wall(k, V(0, 0, 0), r0, r1, top, sides, phase)
    ribs(k, V(0, 0, 0), r0, -0.3, r1, top - 0.05, sides, phase)
    door(k, wall_facet(r0, -0.5, r1, top, sides, phase, 90, 0.0), 0.48, 1.9)
    k.box(V(0, -0.02, r0 + 0.25), (1.3, 0.22, 0.6), "MB_Stone", "doorstep")
    for deg in (54, 126, 198, 342):
        window(k, wall_facet(r0, -0.5, r1, top, sides, phase, deg, 1.45), 0.3)
    # the chimney: a curled stalk with a glowing seed
    path = [V(0.7, 3.5, -0.6), V(0.9, 4.4, -0.8), V(1.3, 4.9, -0.6), V(1.55, 4.8, -0.2)]
    if chapel:
        path = [V(0.9, 3.6, -0.7), V(1.1, 4.7, -0.9), V(1.5, 5.2, -0.7), V(1.75, 5.1, -0.3)]
    k.tube(path, [0.12, 0.1, 0.09, 0.08], 5, "MB_Rib", "chimney")
    k.ball(path[-1] + V(0.05, -0.06, 0.12), 0.2, "MB_Spot", "chimney seed", subdiv=1)
    if chapel:
        # A little bell turret on the crown: husk walls with four glowing slits, its own cap, and a gourd bell.
        apex = y0 + H
        k.lathe(V(0, 0, 0), [(0.55, apex - 0.6), (0.48, apex + 0.75)], 6, "MB_Husk", "turret", phase=0)
        for a in (30, 90, 150, 210, 270, 330):
            f = wall_facet(0.55, apex - 0.6, 0.48, apex + 0.75, 6, 0, a, apex + 0.3)
            k.prism(f, [(-0.08, -0.25), (0.08, -0.25), (0.08, 0.2), (0.0, 0.3), (-0.08, 0.2)], -0.08, 0.025, "MB_Window", "turret slit")
        cap(k, V(0, apex + 0.6, 0), 0.85, 0.9, 8, "turret cap", phase=0, spots=0)
        tip = apex + 1.48
        k.tube([V(0, tip - 0.2, 0), V(0, tip + 0.4, 0), V(0.25, tip + 0.75, 0), V(0.55, tip + 0.8, 0)], [0.14, 0.1, 0.07, 0.04], 6, "MB_Rib", "spire")
        k.tube([V(0.53, tip + 0.82, 0), V(0.55, tip + 0.5, 0)], 0.02, 4, "MB_Rib", "bell cord")
        k.lathe(V(0.55, tip + 0.18, 0), [(0.0, 0.0), (0.16, 0.02), (0.13, 0.18), (0.07, 0.34), (0.0, 0.36)], 8, "MB_Gourd", "bell")
    return k

def mud_stilt_shack(name):
    k = Kit(name)
    lift, floor_top, sides, phase = 1.8, 1.9, 8, 90 - 22.5
    c = V(0, 0, -0.1)
    R, H, y0 = 2.45, 1.25, 4.25  # apex at 5.5: the bridge shack's hex lantern stands there
    cap(k, c + UP * y0, R, H, 12, "cap", phase=15)
    r0, r1 = 1.75, 1.55
    top = k.under(["cap"], c.x + r1, c.z) + 0.15
    k.lathe(c, [(r0, floor_top - 0.08), (r1, top)], sides, "MB_Husk", "wall", phase=phase)
    ribs(k, c, r0, floor_top - 0.05, r1, top - 0.05, sides, phase, skip=())
    wy0 = floor_top - 0.08
    door(k, wall_facet(r0, wy0, r1, top, sides, phase, 90, floor_top, c), 0.42, 1.8, bottom=-0.16)
    for deg, y, r in ((135, 1.5, 0.27), (45, 1.55, 0.25), (225, 1.4, 0.26), (0, 1.5, 0.24)):
        window(k, wall_facet(r0, wy0, r1, top, sides, phase, deg, floor_top + y, c), r)
    # the floor and porch on joists and twisted root stilts
    k.box(V(0, lift, 0.4), (3.8, 0.2, 4.6), "MB_Wood", "floor")
    for z in (-1.5, 1.3):
        k.box(V(0, lift - 0.17, z), (3.5, 0.2, 0.26), "MB_DarkWood", "joist")
    for p in (V(-1.4, 0, -1.5), V(1.4, 0, -1.5), V(-1.4, 0, 1.3), V(1.4, 0, 1.3), V(0, 0, 2.6)):
        k.tube([V(p.x * 1.35, -1.5, p.z * 1.2), V(p.x * 1.25, 0.3, p.z * 1.12), V(p.x * 1.08, 1.1, p.z * 1.03), V(p.x, lift, p.z)], [0.2, 0.17, 0.15, 0.14], 6, "MB_Bark", "stilt")
    # porch rail (a gap where the ladder comes up)
    for x, z in ((-1.75, 2.55), (-0.95, 2.55), (0.15, 2.55), (1.75, 2.55), (-1.75, 1.35), (1.75, 1.35)):
        k.tube([V(x, lift + 0.05, z), V(x, lift + 1.1, z)], 0.07, 5, "MB_Bark", "rail post")
    for a, b, r in ((V(-1.75, 2.75, 2.55), V(-0.95, 2.75, 2.55), 0.035), (V(0.15, 2.75, 2.55), V(1.75, 2.75, 2.55), 0.035),
                    (V(-1.75, 2.62, 2.55), V(-1.75, 2.62, 1.35), 0.03), (V(1.75, 2.62, 2.55), V(1.75, 2.62, 1.35), 0.03)):
        k.tube([a + (b - a).normalized() * -0.1, b + (b - a).normalized() * 0.1], r, 6, "MB_Wood", "rail")
    # a vine ladder from the porch to the ground
    for x in (-0.75, -0.05):
        k.tube([V(x, -0.15, 3.2), V(x, lift - 0.02, 2.64)], 0.05, 5, "MB_Bark", "ladder rail")
    for i in range(5):
        y = 0.3 + i * 0.32
        z = lerp(3.2, 2.64, (y + 0.15) / (lift + 0.13))
        k.tube([V(-0.75, y, z), V(-0.05, y, z)], 0.03, 4, "MB_Wood", "rung")
    # hanging moss under the brim, and a curled chimney stalk
    for i in range(4):
        a = math.radians(i * 90 + 30)
        p = c + V(math.cos(a) * 2.0, 0, math.sin(a) * 2.0)
        moss(k, V(p.x, k.under(["cap"], p.x, p.z, 3.0) + 0.06, p.z), 0.9 + 0.2 * i)
    path = [V(-0.6, 5.0, -0.7), V(-0.8, 5.65, -0.9), V(-1.15, 5.95, -0.75), V(-1.35, 5.85, -0.4)]
    k.tube(path, [0.1, 0.08, 0.07, 0.06], 5, "MB_Rib", "chimney")
    k.ball(path[-1] + V(-0.04, -0.05, 0.1), 0.15, "MB_Spot", "chimney seed")
    return k

def mud_driving_school(name):
    k = Kit(name)
    w, d, h = 5.9, 6.4, 4.4
    k.box(V(0, (h - 0.4) / 2, 0), (w, h + 0.4, d), "MB_Husk", "walls")
    k.box(V(0, 4.455, 0), (w + 0.4, 0.25, d + 0.4), "MB_HuskDark", "roof deck")  # top 4.58: the possums sit here
    for sx in (-1, 1):
        for sz in (-1, 1):
            p = V(sx * w / 2, 0, sz * d / 2)
            k.lathe(p, [(0.62, -0.1), (0.6, 0.0), (0.46, 1.2), (0.38, 4.5), (0.5, 4.8), (0.24, 5.0), (0.0, 5.05)], 8, "MB_Rib", "pillar")
            k.ball(p + UP * 5.08, 0.22, "MB_Spot", "pillar bulb")
    # ribs between the windows (none across a window or the door)
    def wall_rib(p0, out):
        out = Vector(out)
        k.tube([p0 + out * 0.03 + UP * -0.3, p0 + out * 0.16 + UP * (h / 2), p0 + out * 0.03 + UP * (h + 0.02)], 0.09, 5, "MB_Rib", "rib")
    for sx in (-1, 1):
        for z in (-2.45, 0.0, 2.45):
            wall_rib(V(sx * w / 2, 0, z), V(sx, 0, 0))
    for x in (-1.8, -0.6, 0.6):
        wall_rib(V(x, 0, -d / 2), V(0, 0, -1))
    front = lambda x, y: Frame(V(x, y, d / 2), V(0, 0, 1))
    door(k, front(0, 0), 0.72, 2.35)
    k.box(V(0, -0.02, d / 2 + 0.35), (2.0, 0.24, 0.7), "MB_Stone", "doorstep")
    for x in (-1.85, 1.85):
        window(k, front(x, 2.5), 0.55)
    for sx in (-1, 1):
        for z in (-1.3, 1.3):
            window(k, Frame(V(sx * w / 2, 2.4, z), V(sx, 0, 0)), 0.5)
    window(k, Frame(V(1.9, 2.4, -d / 2), V(0, 0, -1)), 0.45)
    shelf_fungi(k, Frame(V(-w / 2, 3.55, 1.3), V(-1, 0, 0)), 0.38, "MB_Cap")
    shelf_fungi(k, Frame(V(w / 2, 1.0, -1.2), V(1, 0, 0)), 0.34, "MB_Cap")
    shelf_fungi(k, Frame(V(-1.2, 1.1, -d / 2), V(0, 0, -1)), 0.3, "MB_Cap")
    # curled fronds round the deck (not along the front, where the sign is)
    def frond(base, lean, length, key):
        lean = Vector(lean).normalized()
        mid = base + lean * length * 0.6
        tip = mid + (lean * 0.5 + V(0, -0.35, 0)) * length * 0.45
        side = lean.cross(UP).normalized()
        k.blade([base + UP * -0.12, mid, tip], [0.35, 0.38, 0.12], 0.025, key, side.cross(lean).normalized() * -1, "frond")
    for i in range(5):
        z = -d / 2 + (i + 0.5) * d / 5
        for sx in (-1, 1):
            frond(V(sx * (w / 2 + 0.05), 4.58, z), V(sx * 0.45, 0.75, 0), 1.25, "MB_Leaf" if i % 2 == 0 else "MB_LeafDark")
    for i in range(4):
        frond(V(-w / 2 + (i + 0.5) * w / 4, 4.58, -d / 2 - 0.05), V(0, 0.75, -0.45), 1.4, "MB_Leaf" if i % 2 == 0 else "MB_LeafDark")
    # the sign on the front edge of the deck (MudProp writes on it at z 3.29)
    k.box(V(0, 4.98, 3.2), (5.4, 1.0, 0.1), "MB_Rib", "sign")
    k.box(V(0, 4.98, 3.16), (5.6, 1.15, 0.08), "MB_Orange", "sign backing")
    return k

def mud_kiosk(name):
    k = Kit(name)
    k.box(V(0, 0.775, 0), (1.0, 1.65, 0.8), "MB_HullDark", "console")
    k.box(V(0, 0.3, 0), (1.06, 0.12, 0.86), "MB_HullStripe", "stripe")
    k.box(V(0, 1.25, 0.40), (0.7, 0.5, 0.12), "MB_Teal", "screen")
    k.box(V(0, 1.25, 0.40), (0.8, 0.6, 0.06), "MB_DarkMetal", "bezel")
    k.box(V(0, 0.7, 0.42), (0.2, 0.1, 0.06), "MB_Red", "button")
    k.box(V(0, 1.62, 0.05), (1.2, 0.1, 1.0), "MB_Rust", "hood")
    for x in (-0.4, 0.4):
        k.tube([V(x, 1.6, -0.035), V(x, 2.3, -0.035)], 0.035, 5, "MB_DarkMetal", "sign post")
    k.box(V(0, 2.15, 0), (0.9, 0.45, 0.06), "MB_Cream", "sign")  # MudProp writes "TEST" at z 0.05
    for x in (-0.38, 0.38):
        for y in (0.45, 1.5):
            k.ball(V(x, y, 0.4), 0.025, "MB_Metal", "rivet", subdiv=0)
    return k

def mud_sawmill(name):
    k = Kit(name)
    R, H, y0 = 5.2, 1.8, 4.3
    cap(k, V(0, y0, 0), R, H, 12, "cap")
    for p in (V(-4, 0, -3), V(4, 0, -3), V(-4, 0, 3), V(4, 0, 3)):
        top = p * 0.86
        ty = k.under(["cap"], top.x, top.z, 2.0) + 0.2
        k.tube([p + UP * -0.2, p * 0.96 + V(0.1, 2.0, 0), V(top.x, ty, top.z)], [0.28, 0.24, 0.22], 6, "MB_Bark", "stalk")
    k.box(V(0, 0.575, 0), (6.0, 1.25, 1.6), "MB_Wood", "bench")
    k.box(V(0, 1.22, 0), (6.1, 0.06, 1.7), "MB_GreyWood", "bench top")
    teeth = []
    for i in range(32):
        a = math.radians(i * 360 / 32)
        r = 1.1 if i % 2 == 0 else 1.0
        teeth.append((r * math.cos(a), r * math.sin(a)))
    k.prism(Frame(V(0, 1.15, 0), V(0, 0, 1)), teeth, -0.03, 0.03, "MB_Metal", "saw blade")
    k.prism(Frame(V(0, 1.15, 0), V(0, 0, 1)), octagon(0.18), -0.08, 0.08, "MB_DarkMetal", "saw hub")
    for i, z in enumerate((-2.55, -1.85, -1.15)):
        k.tube([V(-3, 0.33, z), V(3, 0.33, z)], 0.35, 7, "MB_Bark", "log", cap_keys=("MB_Wood", "MB_Wood"))
    for z in (-2.2, -1.5):
        k.tube([V(-2.8, 0.83, z), V(2.8, 0.83, z)], 0.35, 7, "MB_Bark", "log", cap_keys=("MB_Wood", "MB_Wood"))
    return k

def mud_outhouse(name):
    k = Kit(name)
    cap(k, V(0, 2.3, 0), 1.05, 0.6, 10, "cap", phase=18, spots=6)
    k.box(V(0, 1.0, 0), (1.2, 2.6, 1.2), "MB_Wood", "walls")
    for sx in (-1, 1):
        for sz in (-1, 1):
            k.box(V(sx * 0.6, 0.95, sz * 0.6), (0.14, 2.58, 0.14), "MB_DarkWood", "corner post")
    k.box(V(0, 1.0, 0.6), (0.8, 1.9, 0.06), "MB_GreyWood", "door")
    f = Frame(V(0, 1.75, 0.63), V(0, 0, 1))
    moon = [(0.09 * math.cos(math.radians(a)), 0.12 * math.sin(math.radians(a))) for a in range(-80, 260, 34)]
    k.prism(f, moon, -0.02, 0.04, "MB_Black", "moon")
    k.ball(V(0.28, 1.0, 0.65), 0.04, "MB_Rib", "handle", subdiv=0)
    return k

def mud_witch_hut(name):
    k = Kit(name)
    hat_c, tilt = V(0.2, 3.05, 0), rot('Z', -8)
    prof = [(0.0, 0.15), (1.4, -0.02), (2.65, -0.2), (3.05, 0.05), (1.9, 0.9), (1.1, 2.2), (0.55, 3.5), (0.0, 4.0)]
    cap(k, hat_c, 3.05, 4.0, 9, "hat", key="MB_WitchHat", R3=tilt, spots=7, profile=prof)
    lean = math.tan(math.radians(3))  # the whole hut leans 3 degrees to the right; its walls stay flat
    half = 1.8
    def ring(y, top=False):
        pts = []
        for x, z in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            px, pz = x * half + y * lean, z * half
            py = (k.under(["hat"], px, pz, 1.0) + 0.14) if top else y
            if top:
                px = x * half + py * lean
            pts.append(V(px, py, pz))
        return pts
    top_ring = ring(0, True)
    mid = min(p.y for p in top_ring) - 0.25
    k.loft([ring(-0.4), ring(mid), top_ring], "MB_HuskDark", "walls")
    up = V(lean, 1, 0).normalized()
    for x in (-1.45, 1.6):
        k.tube([V(x - 0.3 * lean, -0.3, half + 0.02), V(x + 1.6 * lean, 1.6, half + 0.1), V(x + (mid - 0.1) * lean, mid - 0.1, half + 0.02)], 0.07, 4, "MB_Rib", "rib")
    for x in (-1.35, -0.45, 0.45, 1.35):
        k.tube([V(x - 0.3 * lean, -0.3, -half - 0.02), V(x + 1.6 * lean, 1.6, -half - 0.1), V(x + (mid - 0.1) * lean, mid - 0.1, -half - 0.02)], 0.07, 4, "MB_Rib", "rib")
    door(k, Frame(V(-0.2, 0, half), V(0, 0, 1), up), 0.5, 1.9)
    k.box(V(-0.2, -0.02, half + 0.25), (1.2, 0.22, 0.55), "MB_Stone", "doorstep")
    window(k, Frame(V(0.95 + 2.2 * lean, 2.2, half), V(0, 0, 1), up), 0.32)
    window(k, Frame(V(half + 1.8 * lean, 1.8, -0.3), V(1, -lean, 0).normalized()), 0.3)
    # the floppy tip of the hat
    apex = hat_c + tilt @ V(0, 4.0, 0)
    t0 = hat_c + tilt @ V(0, 3.4, 0)
    path = [t0, apex + V(0.05, 0.25, 0), apex + V(0.4, 0.75, 0), apex + V(1.0, 0.85, 0), apex + V(1.5, 0.45, 0)]
    k.tube(path, [0.42, 0.3, 0.2, 0.12, 0.05], 7, "MB_WitchHat", "hat tip")
    k.ball(path[-1] + V(0.03, -0.06, 0), 0.13, "MB_Spot", "hat tip glow")
    return k

def mud_roadhouse(name):
    """The Greasy Spoon: a long husk hall with a pitched leaf-shingle roof, a porch under a leaf awning and its sign on
    the front gable (MudProp writes the neon words at z 5.73)."""
    k = Kit(name)
    s = 2.2
    w, d, h = 4.2 * s, 4.6 * s, 2.6 * s
    eave_x, eave_y, ridge_y = w * 0.62, h, h + 1.6 * s
    slope = (ridge_y - eave_y) / eave_x
    thick = 0.3
    zr0, zr1 = -d / 2 - 0.45, d / 2 + 0.44
    under = lambda x: eave_y - thick / math.cos(math.atan(slope)) + slope * (eave_x - abs(x))
    # body: one closed prism, walls and gables together, its top tucked under the roof
    wall_top = under(w / 2) + 0.12
    outline = [(-w / 2, -0.4), (w / 2, -0.4), (w / 2, wall_top), (0.0, under(0) + 0.12), (-w / 2, wall_top)]
    k.prism(Frame(V(0, 0, 0), V(0, 0, 1)), outline, -d / 2, d / 2, "MB_Husk", "walls")
    for sx in (-1, 1):
        sec = [(sx * eave_x, eave_y), (0.0, ridge_y), (0.0, ridge_y - thick / math.cos(math.atan(slope))), (sx * eave_x, eave_y - thick / math.cos(math.atan(slope)))]
        if sx > 0:
            sec = list(reversed(sec))
        k.prism(Frame(V(0, 0, 0), V(0, 0, 1)), sec, zr0, zr1, "MB_HuskDark", "roof")
        shingles(k, V(sx * (eave_x + 0.05), eave_y, zr0 + 0.05), V(sx * (eave_x + 0.05), eave_y, zr1 - 0.05),
                 V(0, ridge_y, zr0 + 0.05), V(0, ridge_y, zr1 - 0.05), 5, 6)
    k.tube([V(0, ridge_y + 0.02, zr0 - 0.1), V(0, ridge_y + 0.02, zr1 + 0.1)], 0.42, 6, "MB_LeafDark", "ridge")
    for i in range(5):
        k.ball(V(0, ridge_y + 0.42, lerp(zr0, zr1, (i + 0.5) / 5)), 0.27, "MB_Spot", "ridge bulb")
    # ribs between the openings, from the ground up under the eaves
    def rib(x, z, out):
        out = Vector(out)
        top = under(x if out.x == 0 else w / 2) + 0.12
        p = V(x, 0, z)
        k.tube([p + out * 0.06 + UP * -0.3, p + out * 0.26 + UP * (top / 2), p + out * 0.06 + UP * top], 0.17, 5, "MB_Rib", "rib")
    for sx in (-1, 1):
        for z in (-3.4, -1.65, 1.65, 3.4):
            rib(sx * w / 2, z, V(sx, 0, 0))
    for x in (-3.1, -1.55, 0.0, 1.55, 3.1):
        rib(x, -d / 2, V(0, 0, -1))
    for x in (-4.15, 4.15):
        rib(x, d / 2, V(0, 0, 1))
    front = lambda x, y: Frame(V(x, y, d / 2), V(0, 0, 1))
    door(k, front(0, 0), 1.1, 3.1)
    for x in (-2.75, 2.75):
        window(k, front(x, 2.97), 0.79)
    for sx in (-1, 1):
        window(k, Frame(V(sx * w / 2, 2.97, 0), V(sx, 0, 0)), 0.79)
    shelf_fungi(k, Frame(V(-w / 2, 4.5, -2.5), V(-1, 0, 0)), 0.45, "MB_Cap")
    shelf_fungi(k, Frame(V(w / 2, 1.3, 2.5), V(1, 0, 0)), 0.42, "MB_Cap")
    shelf_fungi(k, Frame(V(-2.75, 4.75, d / 2), V(0, 0, 1)), 0.32, "MB_Cap")
    # the porch: a deck on a plinth, posts and a leaf awning
    k.box(V(0, 0.16, d / 2 + 0.515), (11.0, 0.52, 1.11), "MB_Wood", "porch")
    az0, az1, ay0, ay1, at = d / 2 - 0.15, 6.4, 4.45, 3.85, 0.16  # above the window frames (top 3.98)
    k.slab(V(0, ay0, az0), V(0, ay1, az1), 5.75, at, "MB_LeafDark", "awning", side=V(1, 0, 0))
    ae = lerp(ay0, ay1, (az1 - 0.05 - az0) / (az1 - az0))
    ar = lerp(ay0, ay1, 0.2 / (az1 - az0))
    shingles(k, V(-5.7, ae, az1 - 0.05), V(5.7, ae, az1 - 0.05), V(-5.7, ar, az0 + 0.2), V(5.7, ar, az0 + 0.2), 2, 5, "awning leaf")
    for x in (-5.0, -1.6, 1.6, 5.0):
        z = 5.6
        top = lerp(ay0, ay1, (z - az0) / (az1 - az0)) - at + 0.06
        k.tube([V(x, 0.3, z), V(x + 0.1, 1.8, z + 0.05), V(x, top, z)], [0.15, 0.13, 0.13], 6, "MB_Bark", "porch post")
        k.ball(V(x, 2.85, z + 0.13), 0.14, "MB_Spot", "post bulb")
    # the sign board, hung off the gable on three ribs
    k.box(V(0, 6.6, 5.62), (7.0, 1.4, 0.15), "MB_Black", "sign")
    for x in (-2.8, 0.0, 2.8):
        k.box(V(x, 6.6, (d / 2 + 5.6) / 2 - 0.02), (0.22, 0.22, 5.6 - d / 2 + 0.12), "MB_Rib", "sign bracket")
    return k

def mud_windmill(name):
    k = Kit(name)
    b, t, H = 1.06, 0.42, 9.0
    sq = lambda half, y: [V(-half, y, -half), V(half, y, -half), V(half, y, half), V(-half, y, half)]
    half = lambda y: lerp(b, t, y / H)
    k.loft([sq(half(-0.4), -0.4), sq(half(H - 0.05), H - 0.05)], "MB_GreyWood", "tower")
    for x, z in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        k.tube([V(x * half(-0.3), -0.3, z * half(-0.3)), V(x * half(H - 0.2), H - 0.2, z * half(H - 0.2))], 0.09, 5, "MB_DarkWood", "corner batten")
    for y in (2.2, 4.6, 7.0):
        k.loft([sq(half(y - 0.09) + 0.035, y - 0.09), sq(half(y + 0.09) + 0.035, y + 0.09)], "MB_DarkWood", "band")
    k.box(V(0, H - 0.07, 0), (1.3, 0.2, 1.3), "MB_DarkWood", "top deck")  # top 9.03: the hex lantern stands here
    # a door in the front face
    slope_n = V(0, (b - t) / H, 1).normalized()
    door(k, Frame(V(0, 0, b), slope_n, V(0, 1, -(b - t) / H).normalized()), 0.38, 1.6)
    # hub, shaft, sails (one hanging broken) and a tail vane
    hy = 8.4
    k.tube([V(0, hy, 0.0), V(0, hy, 1.0)], 0.12, 6, "MB_DarkWood", "shaft")
    k.ball(V(0, hy, 0.95), (0.42, 0.42, 0.5), "MB_HuskDark", "hub")
    for i in range(4):
        ang = i * 90 + 20 + (50 if i == 2 else 0)
        r = rot('Z', ang)
        hub = V(0, hy, 1.25 + (0.12 if i == 2 else 0.0))  # the broken sail hangs in front of the others
        tip = hub + r @ V(0, 3.1, 0)
        k.tube([hub + r @ V(0, -0.1, -0.15), tip], 0.05, 4, "MB_Rib", "spoke")
        k.blade([hub + r @ V(0.3, 0.55, 0), hub + r @ V(0.33, 1.8, 0), tip + r @ V(0.3, -0.1, 0)], [0.27, 0.32, 0.26], 0.03,
                "MB_Leaf" if i % 2 == 0 else "MB_LeafDark", V(0, 0, 1), "sail")
    k.tube([V(0, hy + 0.1, -0.2), V(0, hy + 0.25, -2.2)], 0.06, 5, "MB_DarkWood", "vane arm")
    k.blade([V(0, hy + 0.0, -1.4), V(0, hy + 0.35, -1.9), V(0, hy + 0.5, -2.5)], [0.2, 0.42, 0.3], 0.025, "MB_LeafDark", V(1, 0, 0), "vane")
    return k

def mud_grandstand(name):
    k = Kit(name)
    for i in range(4):
        y = 0.36 + i * 0.5
        k.box(V(0, (y - 0.3) / 2, -i * 0.8), (10.0, y + 0.3, 0.8), "MB_DarkWood", "step")
        k.box(V(0, y + 0.02, -i * 0.8 + 0.12), (10.1, 0.08, 0.46), "MB_Wood", "seat")
    rr = rot('X', -10)
    roof_c = V(0, 3.4, -2.6)
    k.box(roof_c, (10.4, 0.15, 1.8), "MB_Awning", "roof", R=rr)
    for i in range(6):
        x = -5 + i * 2
        top = k.under(["roof"], x, -2.6, 2.0) + 0.06
        k.tube([V(x, 1.7, -2.6), V(x, top, -2.6)], 0.1, 6, "MB_DarkWood", "post")
    k.tube([V(-5.05, 2.85, -2.6), V(5.05, 2.85, -2.6)], 0.06, 5, "MB_DarkWood", "back rail")
    return k

def mud_banana_stand(name):
    k = Kit(name)
    k.box(V(0, 0.475, 0), (2.4, 1.05, 1.0), "MB_Wood", "counter")
    k.box(V(0, 1.0, 0), (2.5, 0.06, 1.1), "MB_GreyWood", "counter top")
    k.box(V(0, 0.7, 0), (2.46, 0.15, 1.06), "MB_Banana", "stripe")
    rr = rot('X', -12)
    k.box(V(0, 2.45, -0.2), (2.6, 0.1, 1.4), "MB_Banana", "canopy", R=rr)
    for x in (-1.1, 1.1):
        top = k.under(["canopy"], x, -0.4, 1.5) + 0.05
        k.tube([V(x, 0.95, -0.4), V(x, top, -0.4)], 0.06, 5, "MB_DarkWood", "post")
        btop = k.under(["canopy"], x, 0.25, 1.5) + 0.04
        k.tube([V(x, 1.75, -0.4), V(x, btop, 0.25)], 0.04, 4, "MB_DarkWood", "brace")
    for bx in (-0.7, 0.0, 0.7):
        stem = V(bx, 1.25, -0.05)
        k.tube([stem + V(0, -0.2, 0), stem + V(0, 0.08, 0)], 0.035, 5, "MB_DarkWood", "banana stem")
        for j in range(4):
            a = math.radians(-50 + j * 33)
            d = V(math.sin(a), 0, math.cos(a))
            k.tube([stem + V(0, -0.02, 0), stem + d * 0.12 + V(0, -0.1, 0), stem + d * 0.26 + V(0, -0.2, 0), stem + d * 0.36 + V(0, -0.15, 0)],
                   [0.03, 0.045, 0.04, 0.02], 5, "MB_Banana", "banana")
    return k

def mud_dojo_gate(name):
    k = Kit(name)
    for x in (-2.2, 2.2):
        k.lathe(V(x, 0, 0), [(0.38, -0.1), (0.36, 0.35), (0.0, 0.42)], 8, "MB_Stone", "plinth")
        k.lathe(V(x, 0, 0), [(0.24, -0.3), (0.2, 4.05), (0.0, 4.06)], 8, "MB_Lacquer", "post")
    k.box(V(0, 3.3, 0), (4.8, 0.25, 0.3), "MB_Lacquer", "tie beam")
    k.box(V(0, 3.92, 0), (5.4, 0.16, 0.42), "MB_Lacquer", "lower lintel")
    path = [V(-3.1, 4.38, 0), V(-1.6, 4.12, 0), V(0, 4.08, 0), V(1.6, 4.12, 0), V(3.1, 4.38, 0)]
    k.blade(path, [0.26] * 5, 0.17, "MB_DarkWood", V(0, 1, 0), "top beam")
    k.box(V(0, 3.62, 0), (0.3, 0.5, 0.2), "MB_Lacquer", "strut")
    k.box(V(0, 3.6, 0.17), (0.6, 0.42, 0.12), "MB_DarkWood", "plaque")
    return k

def mud_gas_pump(name):
    k = Kit(name)
    k.box(V(0, 0.05, 0), (0.9, 0.2, 0.7), "MB_Stone", "plinth")
    k.box(V(0, 0.95, 0), (0.7, 1.7, 0.5), "MB_Hull", "cabinet")
    k.box(V(0, 1.45, 0), (0.74, 0.12, 0.54), "MB_HullStripe", "stripe")
    k.box(V(0, 1.15, 0.255), (0.45, 0.25, 0.04), "MB_ShipGlow", "display")
    for x in (-0.3, 0.3):
        for y in (0.3, 1.7):
            k.ball(V(x, y, 0.25), 0.025, "MB_Metal", "rivet", subdiv=0)
    k.ball(V(0, 1.92, 0), (0.3, 0.2, 0.12), "MB_ShipGlow", "running light")
    k.box(V(0.39, 1.1, 0.1), (0.1, 0.22, 0.14), "MB_DarkMetal", "holster")
    k.tube([V(0.3, 1.3, -0.1), V(0.5, 1.3, -0.1), V(0.62, 0.6, 0.05), V(0.55, 0.4, 0.2), V(0.42, 1.04, 0.12)], 0.04, 5, "MB_Black", "hose")
    return k

BUILDERS = [
    ("MudStiltShack", mud_stilt_shack),
    ("MudCabin", lambda n: mud_cabin(n)),
    ("MudChapel", lambda n: mud_cabin(n, chapel=True)),
    ("MudDrivingSchool", mud_driving_school),
    ("MudKiosk", mud_kiosk),
    ("MudSawmill", mud_sawmill),
    ("MudOuthouse", mud_outhouse),
    ("MudWitchHut", mud_witch_hut),
    ("MudRoadhouse", mud_roadhouse),
    ("MudWindmill", mud_windmill),
    ("MudGrandstand", mud_grandstand),
    ("MudBananaStand", mud_banana_stand),
    ("MudDojoGate", mud_dojo_gate),
    ("MudGasPump", mud_gas_pump),
]

# ---------------------------------------------------------------- build, check, export
os.makedirs(MODELS, exist_ok=True)
os.makedirs(ART, exist_ok=True)
wanted = {s.strip() for s in ONLY.split(",") if s.strip()}
report, failed, x = [], [], 0.0
for name, make in BUILDERS:
    if wanted and name not in wanted:
        continue
    ob, problems, tris = finish(make(name))
    dims = ob.dimensions.copy()
    ob.location = (x + dims.x * 0.5, 0, 0)
    x += dims.x + 4.0
    report.append((name, [round(c, 2) for c in dims], tris, problems))
    if problems:
        failed.append(name)
        continue
    if EXPORT:
        saved = ob.location.copy()
        ob.location = (0, 0, 0)
        with bpy.context.temp_override(selected_objects=[ob], active_object=ob):
            bpy.ops.export_scene.fbx(filepath=os.path.join(MODELS, name + ".fbx"), use_selection=True, object_types={'MESH'},
                                     apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                                     mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
        ob.location = saved

if EXPORT and not wanted:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ART, "MudlandsBuildings.blend"), copy=True)
for name, dims, tris, problems in report:
    print("MUD_BUILDING", name, dims, tris, "OK" if not problems else problems[:12])
print("MUD_BUILDINGS_FAILED", failed)

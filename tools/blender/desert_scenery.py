# Desert revamp scenery (docs/desert-revamp.md): rock formations, canyon walls and desert trees, low poly and flat shaded.
# Run inside Blender (Blender MCP: exec(open(path).read()), or blender --background --python tools/blender/desert_scenery.py).
# Builds every prop in its own scene "DesertScenery", exports one FBX per prop for Unity, saves the .blend and a contact
# sheet. Units are metres; Z is up; each prop's origin is the middle of its footprint at ground level.
# Material names are the keys DesertDunesBuilder maps onto its own Unity materials (DS_*), so colours are set in Unity.
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix

REPO = os.environ.get("SIGNAL_LOST_REPO", r"C:\Users\Lawson\signal-lost")
MODELS = os.path.join(REPO, "signal-lost", "Assets", "Prefabs", "Enviornment objects", "DesertDunes", "Models")
ART = os.path.join(REPO, "art", "desert-scenery")
ONLY = os.environ.get("DESERT_SCENERY_ONLY", "")  # comma list of prop names to rebuild, empty for all

# ---------------------------------------------------------------- scene
scene = bpy.data.scenes.get("DesertScenery") or bpy.data.scenes.new("DesertScenery")
for o in list(scene.objects):
    bpy.data.objects.remove(o, do_unlink=True)
if bpy.context.window is not None:
    bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0

def srgb(r, g, b):
    f = lambda c: c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (f(r / 255), f(g / 255), f(b / 255))

def material(name, rgb, rough=0.85, emit=None):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = (*rgb, 1.0)
    p.inputs["Roughness"].default_value = rough
    if emit is not None:
        p.inputs["Emission Color"].default_value = (*emit, 1.0)
        p.inputs["Emission Strength"].default_value = 2.0
    m.diffuse_color = (*rgb, 1.0)
    return m

# Preview colours only: Unity assigns its own materials by these names.
M = {
    "DS_RockLight": material("DS_RockLight", srgb(196, 133, 92)),
    "DS_RockMid":   material("DS_RockMid",   srgb(176, 107, 74)),
    "DS_RockDark":  material("DS_RockDark",  srgb(133, 84, 64)),
    "DS_RockBand":  material("DS_RockBand",  srgb(214, 168, 120)),
    "DS_Bark":      material("DS_Bark",      srgb(122, 92, 66)),
    "DS_BarkPale":  material("DS_BarkPale",  srgb(170, 150, 140)),
    "DS_Frond":     material("DS_Frond",     srgb(62, 160, 132)),
    "DS_FrondDark": material("DS_FrondDark", srgb(40, 118, 104)),
    "DS_Pod":       material("DS_Pod",       srgb(245, 118, 158)),
    "DS_DeadWood":  material("DS_DeadWood",  srgb(168, 148, 122)),
    "DS_Spike":     material("DS_Spike",     srgb(110, 170, 150)),
    "DS_Twig":      material("DS_Twig",      srgb(112, 86, 60)),
    "DS_Egg":       material("DS_Egg",       srgb(236, 226, 198)),
    "DS_Speckle":   material("DS_Speckle",   srgb(120, 96, 70)),
    "DS_PanIron":   material("DS_PanIron",   srgb(52, 50, 54), rough=0.4),
    "DS_Omelet":    material("DS_Omelet",    srgb(246, 206, 90)),
}

# ---------------------------------------------------------------- mesh kit
class Kit:
    """Collects faces into one bmesh; each face carries a material key."""
    def __init__(self):
        self.bm = bmesh.new()
        self.keys = []

    def mat_index(self, key):
        if key not in self.keys:
            self.keys.append(key)
        return self.keys.index(key)

    def face(self, pts, key):
        vs = [self.bm.verts.new(p) for p in pts]
        f = self.bm.faces.new(vs)
        f.material_index = self.mat_index(key)
        return f

    def loft(self, rings, key_of, cap_bottom=True, cap_top=True):
        """rings: lists of points (same count, counter-clockwise seen from above). key_of(i) gives ring band i's material."""
        n = len(rings[0])
        verts = [[self.bm.verts.new(p) for p in ring] for ring in rings]
        for i in range(len(rings) - 1):
            k = self.mat_index(key_of(i))
            for j in range(n):
                a, b = verts[i][j], verts[i][(j + 1) % n]
                c, d = verts[i + 1][(j + 1) % n], verts[i + 1][j]
                f = self.bm.faces.new((a, b, c, d))
                f.material_index = k
        if cap_bottom:
            f = self.bm.faces.new(list(reversed(verts[0])))
            f.material_index = self.mat_index(key_of(0))
        if cap_top:
            f = self.bm.faces.new(verts[-1])
            f.material_index = self.mat_index(key_of(len(rings) - 2))
        return verts

    def prism(self, outline, z0, z1, key_side, key_top=None, key_bottom=None):
        """Extrudes a counter-clockwise outline (x, y) from z0 to z1."""
        bot = [Vector((x, y, z0)) for x, y in outline]
        top = [Vector((x, y, z1)) for x, y in outline]
        self.loft([bot, top], lambda i: key_side, cap_bottom=False, cap_top=False)
        f = self.face(list(reversed(bot)), key_bottom or key_side)
        f = self.face(top, key_top or key_side)

    def ball(self, centre, radius, key, subdiv=1, squash=(1, 1, 1), jitter=0.0, rng=None):
        ret = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=radius)
        k = self.mat_index(key)
        for v in ret["verts"]:
            j = 1.0 + (rng.uniform(-jitter, jitter) if rng and jitter else 0.0)
            v.co = Vector((v.co.x * squash[0] * j, v.co.y * squash[1] * j, v.co.z * squash[2] * j)) + Vector(centre)
        for f in {f for v in ret["verts"] for f in v.link_faces}:
            f.material_index = k
        return ret["verts"]

    def tube(self, path, radii, sides, key, rng=None, jitter=0.0, cap=True):
        """A tapered tube along a list of points (any direction)."""
        rings = []
        for i, p in enumerate(path):
            p = Vector(p)
            t = (Vector(path[min(i + 1, len(path) - 1)]) - Vector(path[max(i - 1, 0)])).normalized()
            up = Vector((0, 0, 1)) if abs(t.z) < 0.95 else Vector((1, 0, 0))
            u = t.cross(up).normalized()
            w = t.cross(u).normalized()
            ring = []
            for s in range(sides):
                a = 2 * math.pi * s / sides
                r = radii[i] * (1.0 + (rng.uniform(-jitter, jitter) if rng and jitter else 0.0))
                ring.append(p + (u * math.cos(a) + w * math.sin(a)) * r)
            rings.append(ring)
        return self.loft(rings, lambda i: key, cap_bottom=cap, cap_top=cap)

    def leaf(self, path, widths, thick, key_top, key_bottom, droop_axis):
        """A thin blade along a path: widths per point, sideways along droop_axis x tangent."""
        top, bot = [], []
        for i, p in enumerate(path):
            p = Vector(p)
            t = (Vector(path[min(i + 1, len(path) - 1)]) - Vector(path[max(i - 1, 0)])).normalized()
            side = t.cross(Vector(droop_axis)).normalized()
            normal = side.cross(t).normalized()
            w = widths[i]
            top.append((p + side * w + normal * thick, p - side * w + normal * thick))
            bot.append((p + side * w - normal * thick, p - side * w - normal * thick))
        for i in range(len(path) - 1):
            (a, b), (c, d) = top[i], top[i + 1]
            self.face([a, b, d, c], key_top)
            (a2, b2), (c2, d2) = bot[i], bot[i + 1]
            self.face([a2, c2, d2, b2], key_bottom)
            self.face([a, c, c2, a2], key_bottom)
            self.face([b, b2, d2, d], key_bottom)

    def finish(self, name):
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=0.0005)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.get(name)
        if me is not None:
            bpy.data.meshes.remove(me)
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        for k in self.keys:
            me.materials.append(M[k])
        for poly in me.polygons:
            poly.use_smooth = False
        ob = bpy.data.objects.new(name, me)
        scene.collection.objects.link(ob)
        return ob

def ring(radius, sides, z, rng, jitter=0.12, squash=1.0, offset=(0, 0), phase=0.0):
    pts = []
    for s in range(sides):
        a = 2 * math.pi * s / sides + phase
        r = radius * (1.0 + rng.uniform(-jitter, jitter))
        pts.append(Vector((offset[0] + math.cos(a) * r, offset[1] + math.sin(a) * r * squash, z)))
    return pts

STRATA = ["DS_RockMid", "DS_RockLight", "DS_RockDark", "DS_RockBand"]

# ---------------------------------------------------------------- rock formations
def hoodoo(name, seed, height):
    """A wind-carved pillar: soft banded layers pinching in and out, and a wider dark caprock on top."""
    rng = random.Random(seed)
    k = Kit()
    sides = 8
    rings, keys = [], []
    z = -1.0
    base = rng.uniform(2.4, 3.2)
    layers = rng.randint(5, 7)
    for i in range(layers + 1):
        t = i / layers
        pinch = 0.55 + 0.45 * abs(math.sin(t * math.pi * rng.uniform(1.6, 2.4) + seed))
        r = base * (1.0 - 0.45 * t) * pinch
        r = max(r, base * 0.38)
        rings.append(ring(r, sides, z, rng, 0.1, offset=(rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2)), phase=0.2 * i))
        keys.append(STRATA[(i + seed) % 3])
        z += (height - 1.0) / layers
    k.loft(rings, lambda i: keys[i])
    cap_r = base * rng.uniform(0.75, 0.95)
    top = rings[-1][0].z + 0.3  # the caprock sits on the pillar's last layer
    capring0 = ring(cap_r, sides, top - 0.4, rng, 0.15)
    capring1 = ring(cap_r * 1.05, sides, top + 0.6, rng, 0.15)
    capring2 = ring(cap_r * 0.7, sides, top + 1.3, rng, 0.2)
    k.loft([capring0, capring1, capring2], lambda i: "DS_RockDark")
    return k.finish(name)

def balanced_rock(name, seed):
    rng = random.Random(seed)
    k = Kit()
    rings = [ring(2.2, 7, -1.0, rng), ring(1.6, 7, 2.0, rng), ring(0.9, 7, 4.5, rng), ring(0.7, 7, 5.6, rng)]
    k.loft(rings, lambda i: STRATA[i % 3])
    k.ball((0.4, 0.2, 7.6), 2.9, "DS_RockLight", subdiv=1, squash=(1.25, 1.0, 0.8), jitter=0.12, rng=rng)
    return k.finish(name)

def big_arch(name, seed):
    """A natural bridge about 28 m across: its top is nearly flat over the middle 16 m (the walkway, about 3.4 m wide,
    10.5 m up) so it can span Rattlesnake Canyon level with the plateau either side; its legs plunge to the floor."""
    rng = random.Random(seed)
    k = Kit()
    span, rise = 14.0, 10.5
    steps = 26
    rings = []
    for i in range(steps + 1):
        t = i / steps
        a = math.pi * (1 - t)
        x = math.cos(a) * span
        z = math.sin(a) ** 0.35 * rise - 2.0 * (1 - math.sin(a))
        legs = 1.0 + 1.3 * (1 - math.sin(a)) ** 2
        half_w = 1.7 * legs        # across the arch (walkway width at the top)
        thick = 1.6 * legs         # vertical thickness of the band
        # Tangent along the arch in XZ, and its normal.
        da = -math.pi / steps
        dx = -math.sin(a) * span * da
        dz = math.cos(a) * rise * da
        tn = Vector((dx, 0, dz)).normalized()
        nn = Vector((-tn.z, 0, tn.x))
        if nn.z < 0:
            nn = -nn
        c = Vector((x, 0, z))
        j = lambda: rng.uniform(-0.18, 0.18) if 0 < i < steps else 0.0
        # Top surface stays flat across, so the walkway is level; the underside is rough.
        rings.append([
            c + nn * thick * 0.5 + Vector((0, -half_w, 0)),
            c + nn * thick * 0.5 + Vector((0, half_w, 0)),
            c + nn * (thick * 0.1 + j()) + Vector((0, half_w * 1.12, 0)),
            c - nn * (thick * 0.5 + abs(j())) + Vector((0, half_w * 0.9, 0)),
            c - nn * (thick * 0.55 + abs(j())) + Vector((0, 0, 0)),
            c - nn * (thick * 0.5 + abs(j())) + Vector((0, -half_w * 0.9, 0)),
            c + nn * (thick * 0.1 + j()) + Vector((0, -half_w * 1.12, 0)),
        ])
    # The rings were built going "inward" for some faces; loft wants consistent order, normals are recalculated later.
    k.loft(rings, lambda i: STRATA[(i // 3) % 4])
    # Footing boulders where the legs meet the ground.
    for sx in (-1, 1):
        for n in range(3):
            k.ball((sx * (span + rng.uniform(-1.5, 2.5)), rng.uniform(-3.5, 3.5), rng.uniform(-0.6, 0.6)),
                   rng.uniform(1.2, 2.2), "DS_RockDark", subdiv=1, squash=(1.2, 1.0, 0.7), jitter=0.15, rng=rng)
    return k.finish(name)

def canyon_wall(name, seed, length=16.0, height=14.0, depth=5.0):
    """A layered cliff slab: stacked strata, each with its own jagged front edge, so the face steps in ledges.
    The front faces -Y; the slab runs along X from -length/2 to length/2."""
    rng = random.Random(seed)
    k = Kit()
    layers = rng.randint(5, 6)
    z = -2.0
    for i in range(layers):
        lz = (height + 2.0) / layers * rng.uniform(0.8, 1.2)
        inset = rng.uniform(-0.4, 0.9) + i * 0.25
        pts = 7
        front = []
        for p in range(pts + 1):
            x = -length / 2 + length * p / pts
            front.append((x + rng.uniform(-0.4, 0.4) * (0 < p < pts), -depth / 2 + inset + rng.uniform(-0.6, 0.6)))
        back = [(length / 2 + 0.3, depth / 2), (-length / 2 - 0.3, depth / 2)]
        outline = front + back
        # Counter-clockwise from above: front goes +X along -Y side, back returns along +Y side.
        z1 = min(z + lz, height) if i == layers - 1 else z + lz
        if i == layers - 1:
            z1 = height
        k.prism(outline, z, z1, STRATA[(i + seed) % 4], key_top="DS_RockLight")
        z = z1
    return k.finish(name)

def butte(name, seed, radius, height):
    """An eroded butte: stacked strata, each with its own wobbly outline stepping in, a flat cap and a talus skirt.
    About 2*radius across and height tall; the skirt runs out to 1.35*radius at ground level."""
    rng = random.Random(seed)
    k = Kit()
    sides = 16
    phase = [rng.uniform(0, 6.28) for _ in range(4)]
    def outline(r, wobble, salt):
        pts = []
        for s in range(sides):
            a = 2 * math.pi * s / sides
            w = 1 + wobble * (0.55 * math.sin(2 * a + phase[0] + salt) + 0.3 * math.sin(3 * a + phase[1]) + 0.15 * math.sin(5 * a + phase[2] + salt * 2))
            pts.append((math.cos(a) * r * w, math.sin(a) * r * w * 0.85))
        return pts
    # Talus skirt: a low cone of scree from 1.28 r at the ground up to the cliff's foot, sharing the first layer's bottom
    # ring, and every layer starts exactly on the ring the ledge below it ends on: one closed shell, no slits (11-22).
    z = height * 0.12
    r = radius
    base = outline(radius, 0.24, 0.0)
    skirt0 = [Vector((x, y, -0.6)) for x, y in outline(radius * 1.28, 0.24, 0.0)]
    k.loft([skirt0, [Vector((x, y, z)) for x, y in base]], lambda i: "DS_RockMid", cap_bottom=True, cap_top=False)
    layers = rng.randint(5, 7)
    for i in range(layers):
        lz = (height - z) / (layers - i) * rng.uniform(0.75, 1.25) if i < layers - 1 else height - z
        # Near-vertical walls: each layer only bulges or pinches a little; the outline is the same lumpy shape all the way up.
        r_next = r * rng.uniform(0.95, 1.03)
        bot = [Vector((x, y, z)) for x, y in base]
        top = [Vector((x, y, z + lz)) for x, y in outline(r_next, 0.24, i * 0.15 + 0.05)]
        k.loft([bot, top], lambda j, i=i: STRATA[(i + seed) % 4], cap_bottom=False, cap_top=False)
        # The ledge on top of this layer, stepping in to the next.
        r_in = r_next * (rng.uniform(0.9, 0.94) if rng.random() < 0.3 else rng.uniform(0.97, 1.0))
        base = outline(r_in, 0.24, i * 0.15 + 0.08)
        k.loft([top, [Vector((x, y, z + lz)) for x, y in base]], lambda j: "DS_RockLight", cap_bottom=False, cap_top=(i == layers - 1))
        z += lz
        r = r_in
    return k.finish(name)

def rock_fin(name, seed):
    """A tall thin blade of rock, like a wall fragment: a canyon wall with little depth."""
    return canyon_wall(name, seed, length=12.0, height=11.0, depth=2.6)

# ---------------------------------------------------------------- trees
def palm(name, seed, height):
    """An alien palm: a curving ringed trunk, a crown of drooping teal fronds and pink seed pods."""
    rng = random.Random(seed)
    k = Kit()
    lean = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)).normalized() * rng.uniform(0.8, 1.8)
    segs = 9
    path, radii = [], []
    for i in range(segs + 1):
        t = i / segs
        p = Vector((0, 0, -0.4 + t * height)) + lean * (t * t)
        path.append(p)
        radii.append((0.42 - 0.18 * t) * (1.12 if i % 2 == 0 else 0.96))
    k.tube(path, radii, 6, "DS_Bark", rng=rng, jitter=0.05)
    top = path[-1]
    fronds = rng.randint(7, 9)
    for f in range(fronds):
        a = 2 * math.pi * f / fronds + rng.uniform(-0.2, 0.2)
        out = Vector((math.cos(a), math.sin(a), 0))
        length = rng.uniform(3.4, 4.4)
        pts, widths = [], []
        for s in range(7):
            t = s / 6
            p = top + out * (length * t) + Vector((0, 0, 0.9 * math.sin(t * math.pi * 0.7) - 1.9 * t * t))
            pts.append(p)
            widths.append(0.08 + 0.55 * math.sin(t * math.pi) ** 0.8)
        k.leaf(pts, widths, 0.04, "DS_Frond", "DS_FrondDark", droop_axis=(0, 0, 1))
    for n in range(rng.randint(3, 5)):
        a = rng.uniform(0, 2 * math.pi)
        k.ball(top + Vector((math.cos(a) * 0.45, math.sin(a) * 0.45, -0.35 - rng.uniform(0, 0.3))), 0.22, "DS_Pod", subdiv=1)
    return k.finish(name)

def spindle_tree(name, seed):
    """A Joshua-like spindle tree: a short pale trunk forking into crooked arms, each tipped with a spiky teal tuft."""
    rng = random.Random(seed)
    k = Kit()
    trunk_h = rng.uniform(2.0, 2.8)
    k.tube([(0, 0, -0.3), (0, 0, trunk_h * 0.5), (0.1, 0, trunk_h)], [0.38, 0.32, 0.28], 6, "DS_BarkPale", rng=rng, jitter=0.06)
    tips = []
    def branch(start, direction, length, radius, depth):
        mid = start + direction * length * 0.5 + Vector((rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), 0.2))
        end = start + direction * length
        k.tube([start, mid, end], [radius, radius * 0.85, radius * 0.7], 5, "DS_BarkPale", rng=rng, jitter=0.05)
        if depth == 0:
            tips.append(end)
            return
        for b in range(2):
            a = rng.uniform(0, 2 * math.pi)
            d = (direction * 0.6 + Vector((math.cos(a), math.sin(a), 0.9)) * 0.6).normalized()
            branch(end, d, length * rng.uniform(0.6, 0.8), radius * 0.7, depth - 1)
    for b in range(rng.randint(2, 3)):
        a = 2 * math.pi * b / 3 + rng.uniform(-0.4, 0.4)
        branch(Vector((0.1, 0, trunk_h)), Vector((math.cos(a) * 0.7, math.sin(a) * 0.7, 0.75)).normalized(), rng.uniform(1.6, 2.2), 0.2, 1)
    for tip in tips:
        spikes = 10
        for s in range(spikes):
            a = 2 * math.pi * s / spikes + rng.uniform(-0.2, 0.2)
            up = rng.uniform(-0.4, 1.0)
            d = Vector((math.cos(a), math.sin(a), up)).normalized()
            base = tip + d * 0.12
            k.tube([base, base + d * rng.uniform(0.55, 0.85)], [0.09, 0.01], 3, "DS_Spike", cap=True)
        k.ball(tip, 0.22, "DS_FrondDark", subdiv=0)
    return k.finish(name)

def dead_tree(name, seed):
    """A dead thorn tree: a twisted bleached trunk with crooked bare branches and thorns."""
    rng = random.Random(seed)
    k = Kit()
    def branch(start, direction, length, radius, depth):
        pts = [start]
        d = direction
        for s in range(3):
            d = (d + Vector((rng.uniform(-0.35, 0.35), rng.uniform(-0.35, 0.35), rng.uniform(-0.1, 0.25)))).normalized()
            pts.append(pts[-1] + d * length / 3)
        k.tube(pts, [radius, radius * 0.8, radius * 0.55, radius * 0.25], 5, "DS_DeadWood", rng=rng, jitter=0.06)
        for s in range(1, 3):
            a = rng.uniform(0, 2 * math.pi)
            t = Vector((math.cos(a), math.sin(a), rng.uniform(-0.2, 0.5))).normalized()
            k.tube([pts[s], pts[s] + t * 0.35], [radius * 0.25, 0.005], 3, "DS_Twig")
        if depth > 0:
            for b in range(rng.randint(2, 3)):
                a = rng.uniform(0, 2 * math.pi)
                nd = (d + Vector((math.cos(a), math.sin(a), 0.3)) * 0.9).normalized()
                branch(pts[rng.randint(2, 3)], nd, length * 0.62, radius * 0.6, depth - 1)
    branch(Vector((0, 0, -0.3)), Vector((rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2), 1)).normalized(), rng.uniform(3.2, 4.2), 0.32, 2)
    return k.finish(name)

# ---------------------------------------------------------------- quest bits for act 1
def nest(name, seed):
    """A big nest of sticks and ship scrap; the egg sits in its hollow at about z 0.35."""
    rng = random.Random(seed)
    k = Kit()
    for n in range(26):
        a = 2 * math.pi * n / 26 + rng.uniform(-0.1, 0.1)
        r = rng.uniform(0.85, 1.15)
        z = rng.uniform(0.05, 0.45)
        p = Vector((math.cos(a) * r, math.sin(a) * r, z))
        t = Vector((-math.sin(a), math.cos(a), rng.uniform(-0.25, 0.25))).normalized()
        k.tube([p - t * 0.6, p + t * 0.6], [0.07, 0.05], 4, "DS_Twig" if n % 3 else "DS_DeadWood")
    rings = [ring(0.95, 9, 0.0, rng, 0.05), ring(1.15, 9, 0.18, rng, 0.05), ring(0.6, 9, 0.12, rng, 0.05)]
    k.loft(rings, lambda i: "DS_Twig", cap_top=True, cap_bottom=True)
    return k.finish(name)

def egg(name, seed):
    """A big speckled egg, 0.55 m tall, origin at its bottom."""
    rng = random.Random(seed)
    k = Kit()
    rings = []
    for i in range(9):
        t = i / 8
        z = 0.55 * t
        r = 0.2 * math.sin(math.pi * t) ** 0.7 * (1.0 - 0.18 * t) + 0.001
        rings.append(ring(r, 10, z, rng, 0.0, phase=0.3 * i))
    verts = k.loft(rings, lambda i: "DS_Egg", cap_bottom=True, cap_top=True)
    sp = k.mat_index("DS_Speckle")
    faces = list(k.bm.faces)
    for f in rng.sample(faces, max(4, len(faces) // 6)):
        f.material_index = sp
    return k.finish(name)

def frying_pan(name, seed):
    """Cavendish's camp frying pan, 0.56 m across, with a long handle along +X; origin under the pan's middle."""
    rng = random.Random(seed)
    k = Kit()
    rings = [ring(0.2, 12, 0.0, rng, 0.0), ring(0.24, 12, 0.02, rng, 0.0), ring(0.28, 12, 0.09, rng, 0.0),
             ring(0.25, 12, 0.09, rng, 0.0), ring(0.2, 12, 0.025, rng, 0.0)]
    k.loft(rings, lambda i: "DS_PanIron", cap_bottom=True, cap_top=True)
    k.tube([(0.27, 0, 0.07), (0.45, 0, 0.11), (0.7, 0, 0.14)], [0.025, 0.022, 0.025], 5, "DS_PanIron")
    k.tube([(0.5, 0, 0.12), (0.72, 0, 0.145)], [0.035, 0.035], 6, "DS_Twig")
    # An omelet-yellow disc in the pan.
    k.loft([ring(0.19, 12, 0.03, rng, 0.04), ring(0.17, 12, 0.055, rng, 0.04)], lambda i: "DS_Omelet", cap_bottom=False, cap_top=True)
    return k.finish(name)

# ---------------------------------------------------------------- build, export, preview
BUILDERS = [
    ("DesertHoodooA", lambda n: hoodoo(n, 11, 13.0)),
    ("DesertHoodooB", lambda n: hoodoo(n, 23, 10.0)),
    ("DesertHoodooC", lambda n: hoodoo(n, 37, 16.0)),
    ("DesertBalancedRock", lambda n: balanced_rock(n, 5)),
    ("DesertBigArch", lambda n: big_arch(n, 7)),
    ("DesertButteA", lambda n: butte(n, 41, 9.0, 13.0)),
    ("DesertButteB", lambda n: butte(n, 53, 7.0, 16.0)),
    ("DesertButteC", lambda n: butte(n, 67, 11.0, 10.0)),
    ("DesertRockFin", lambda n: rock_fin(n, 71)),
    ("DesertPalmA", lambda n: palm(n, 3, 7.5)),
    ("DesertPalmB", lambda n: palm(n, 9, 9.0)),
    ("DesertSpindleTreeA", lambda n: spindle_tree(n, 13)),
    ("DesertSpindleTreeB", lambda n: spindle_tree(n, 29)),
    ("DesertDeadTreeA", lambda n: dead_tree(n, 17)),
    ("DesertDeadTreeB", lambda n: dead_tree(n, 31)),
    ("DesertNest", lambda n: nest(n, 19)),
    ("DesertEgg", lambda n: egg(n, 21)),
    ("DesertFryingPan", lambda n: frying_pan(n, 23)),
]

os.makedirs(MODELS, exist_ok=True)
os.makedirs(ART, exist_ok=True)
wanted = {s.strip() for s in ONLY.split(",") if s.strip()}
built = []
x = 0.0
for name, make in BUILDERS:
    ob = make(name)
    dims = ob.dimensions.copy()
    ob.location = (x + dims.x * 0.5, 0, 0)
    x += dims.x + 4.0
    built.append(ob)
    if wanted and name not in wanted:
        continue
    for other in scene.objects:
        other.select_set(other == ob)
    saved = ob.location.copy()
    ob.location = (0, 0, 0)
    with bpy.context.temp_override(selected_objects=[ob], active_object=ob):
        bpy.ops.export_scene.fbx(filepath=os.path.join(MODELS, name + ".fbx"), use_selection=True, object_types={'MESH'},
                                 apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                                 mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
    ob.location = saved

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ART, "DesertScenery.blend"), copy=True)
print("DESERT_SCENERY", [(o.name, [round(c, 1) for c in o.dimensions],
                          sum(len(p.vertices) - 2 for p in o.data.polygons)) for o in built])

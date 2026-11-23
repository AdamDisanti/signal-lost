# Shared low-poly, flat-shaded mesh kit for the desert revamp's Blender scripts (desert_npcs.py).
# Each Kit collects faces into one bmesh, every face tagged with a material key from the given material dict.
import bpy, bmesh, math
from mathutils import Vector


def srgb(r, g, b):
    f = lambda c: c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (f(r / 255), f(g / 255), f(b / 255))


def material(name, rgb, rough=0.8, metal=0.0, emit=None):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = (*rgb, 1.0)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    if emit is not None:
        p.inputs["Emission Color"].default_value = (*emit, 1.0)
        p.inputs["Emission Strength"].default_value = 2.0
    m.diffuse_color = (*rgb, 1.0)
    return m


class Kit:
    def __init__(self, mats):
        self.mats = mats
        self.bm = bmesh.new()
        self.keys = []

    def mi(self, key):
        if key not in self.keys:
            self.keys.append(key)
        return self.keys.index(key)

    def face(self, pts, key):
        f = self.bm.faces.new([self.bm.verts.new(Vector(p)) for p in pts])
        f.material_index = self.mi(key)
        return f

    def loft(self, rings, key_of, cap_bottom=True, cap_top=True):
        n = len(rings[0])
        verts = [[self.bm.verts.new(Vector(p)) for p in r] for r in rings]
        for i in range(len(rings) - 1):
            k = self.mi(key_of(i))
            for j in range(n):
                f = self.bm.faces.new((verts[i][j], verts[i][(j + 1) % n], verts[i + 1][(j + 1) % n], verts[i + 1][j]))
                f.material_index = k
        if cap_bottom:
            self.bm.faces.new(list(reversed(verts[0]))).material_index = self.mi(key_of(0))
        if cap_top:
            self.bm.faces.new(verts[-1]).material_index = self.mi(key_of(len(rings) - 2))
        return verts

    def lathe(self, centre, profile, sides, key_of, axis=None, phase=0.0):
        """Rings round an axis (default +Z) through centre: profile is [(radius, height), ...] from bottom to top."""
        c = Vector(centre)
        up = Vector(axis).normalized() if axis else Vector((0, 0, 1))
        side = Vector((1, 0, 0)) if abs(up.x) < 0.9 else Vector((0, 1, 0))
        u = up.cross(side).normalized()
        w = up.cross(u).normalized()
        rings = []
        for r, h in profile:
            ring = []
            for s in range(sides):
                a = 2 * math.pi * s / sides + phase
                ring.append(c + up * h + (u * math.cos(a) + w * math.sin(a)) * max(r, 0.0005))
            rings.append(ring)
        return self.loft(rings, key_of)

    def ball(self, centre, radius, key, subdiv=1, squash=(1, 1, 1), rot=None):
        ret = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=radius)
        k = self.mi(key)
        for v in ret["verts"]:
            co = Vector((v.co.x * squash[0], v.co.y * squash[1], v.co.z * squash[2]))
            if rot is not None:
                co = rot @ co
            v.co = co + Vector(centre)
        for f in {f for v in ret["verts"] for f in v.link_faces}:
            f.material_index = k
        return ret["verts"]

    def tube(self, path, radii, sides, key, cap=True):
        rings = []
        for i, p in enumerate(path):
            p = Vector(p)
            t = (Vector(path[min(i + 1, len(path) - 1)]) - Vector(path[max(i - 1, 0)])).normalized()
            ref = Vector((0, 0, 1)) if abs(t.z) < 0.95 else Vector((1, 0, 0))
            u = t.cross(ref).normalized()
            w = t.cross(u).normalized()
            rings.append([p + (u * math.cos(2 * math.pi * s / sides) + w * math.sin(2 * math.pi * s / sides)) * radii[i] for s in range(sides)])
        return self.loft(rings, lambda i: key, cap_bottom=cap, cap_top=cap)

    def box(self, centre, size, key, rot=None):
        c = Vector(centre)
        hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
        corners = [Vector((x, y, z)) for z in (-hz, hz) for y in (-hy, hy) for x in (-hx, hx)]
        if rot is not None:
            corners = [rot @ v for v in corners]
        v = [c + q for q in corners]
        for idx in ((0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)):
            self.face([v[i] for i in idx], key)

    def blade(self, path, widths, thick, key, normal_hint=(0, 0, 1)):
        """A thin flat strip along a path (feathers, flags, beards)."""
        hint = Vector(normal_hint)
        top, bot = [], []
        for i, p in enumerate(path):
            p = Vector(p)
            t = (Vector(path[min(i + 1, len(path) - 1)]) - Vector(path[max(i - 1, 0)])).normalized()
            side = t.cross(hint).normalized()
            n = side.cross(t).normalized()
            top.append((p + side * widths[i] + n * thick, p - side * widths[i] + n * thick))
            bot.append((p + side * widths[i] - n * thick, p - side * widths[i] - n * thick))
        for i in range(len(path) - 1):
            (a, b), (c, d) = top[i], top[i + 1]
            (a2, b2), (c2, d2) = bot[i], bot[i + 1]
            self.face([a, b, d, c], key)
            self.face([a2, c2, d2, b2], key)
            self.face([a, c, c2, a2], key)
            self.face([b, b2, d2, d], key)

    def finish(self, name, scene, pivot=(0, 0, 0)):
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=0.0003)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        old = bpy.data.meshes.get(name)
        if old is not None:
            bpy.data.meshes.remove(old)
        me = bpy.data.meshes.new(name)
        pv = Vector(pivot)
        for v in self.bm.verts:
            v.co -= pv
        self.bm.to_mesh(me)
        self.bm.free()
        for k in self.keys:
            me.materials.append(self.mats[k])
        for poly in me.polygons:
            poly.use_smooth = False
        ob = bpy.data.objects.new(name, me)
        ob.location = pv
        scene.collection.objects.link(ob)
        return ob

# Shared kit for the desert bosses (tools/blender/desert_*.py): low-poly flat-shaded parts, one skinned mesh on a
# simple skeleton, and key-pose animation clips. Run the boss scripts inside Blender (Blender MCP, or
# blender --background --python tools/blender/desert_bosses.py).
#
# Conventions:
# - Metres, Z up, the creature faces -Y (Blender's front view), standing on Z = 0.
# - Every bone points straight up (+Z) with no roll, so each bone's local axes match the world's at rest:
#   rotation (pitch, yaw, roll) in degrees is about X (+ tips the front down), Z/up (+ turns to the creature's left,
#   which is +X) and the forward axis (+ rolls the left side up). Joints pivot at the bone's head.
# - Root location offsets are (left, up, forward) in metres.
# - Parts are skinned rigidly to one bone, or smoothly along a chain of bones (necks, tails, bodies).
import bpy, bmesh, math, os, json, random
from mathutils import Vector, Matrix

X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
FPS = 30


def srgb(r, g, b):
    f = lambda c: c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (f(r / 255), f(g / 255), f(b / 255))


class Boss:
    def __init__(self, name):
        self.name = name
        scene = bpy.data.scenes.get(name)
        if scene is not None:
            for o in list(scene.objects):
                bpy.data.objects.remove(o, do_unlink=True)
        else:
            scene = bpy.data.scenes.new(name)
        if bpy.context.window is not None:
            bpy.context.window.scene = scene
        scene.render.fps = FPS
        self.scene = scene
        self.mats = {}
        self.bones = {}        # name -> (head, parent)
        self.order = []
        self.parts = []        # objects with vertex groups
        self.clips = []        # (name, frames, loop, events)
        self.base = {}
        self.rng = random.Random(name)

    # ------------------------------------------------------------ materials (Unity rebuilds them by name)
    def material(self, key, rgb, rough=0.75, metal=0.0, emit=None, strength=2.5):
        name = self.name + key
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        p = m.node_tree.nodes.get("Principled BSDF")
        p.inputs["Base Color"].default_value = (*rgb, 1.0)
        p.inputs["Roughness"].default_value = rough
        p.inputs["Metallic"].default_value = metal
        if emit is not None:
            p.inputs["Emission Color"].default_value = (*emit, 1.0)
            p.inputs["Emission Strength"].default_value = strength
        else:
            p.inputs["Emission Strength"].default_value = 0.0
        m.diffuse_color = (*rgb, 1.0)
        self.mats[key] = m
        return m

    # ------------------------------------------------------------ skeleton
    def bone(self, name, head, parent=None):
        self.bones[name] = (Vector(head), parent)
        self.order.append(name)
        return name

    def mirror_bones(self, names_and_heads, parent_of):
        """names_and_heads: [(base name, head with +X = left)]; adds L (+X) and R (-X) copies."""
        for side, s in (("L", 1), ("R", -1)):
            for base, head in names_and_heads:
                h = Vector(head)
                h.x *= s
                par = parent_of(base)
                if par is not None and par in [b for b, _ in names_and_heads]:
                    par = par + side
                self.bone(base + side, h, par)

    # ------------------------------------------------------------ mesh helpers
    def _object(self, name, bm, keys):
        me = bpy.data.meshes.new(name)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(me)
        bm.free()
        for k in keys:
            me.materials.append(self.mats[k])
        for poly in me.polygons:
            poly.use_smooth = False
        ob = bpy.data.objects.new(name, me)
        self.scene.collection.objects.link(ob)
        return ob

    def _skin(self, ob, bone=None, chain=None, blend=0.35, weights=None, mix=None):
        """Rigid to one bone, smooth along a chain of joints, per-vertex weights [(bone, w), ...], or one mix of
        bones for every vertex."""
        me = ob.data
        if mix is not None:
            weights = [mix] * len(me.vertices)
        groups = {}
        def group(b):
            if b not in groups:
                groups[b] = ob.vertex_groups.new(name=b)
            return groups[b]
        if weights is not None:
            for v, ws in zip(me.vertices, weights):
                for b, w in ws:
                    group(b).add([v.index], w, 'REPLACE')
        elif chain is None:
            group(bone).add([v.index for v in me.vertices], 1.0, 'REPLACE')
        else:
            pts = [self.bones[b][0] for b in chain]
            # arc length of each joint along the polyline
            arc = [0.0]
            for i in range(1, len(pts)):
                arc.append(arc[-1] + (pts[i] - pts[i - 1]).length)
            n = len(pts)
            segs = [(pts[i + 1] - pts[i]).length for i in range(n - 1)]
            for v in me.vertices:
                p = ob.matrix_world @ v.co
                # arc-length position of the nearest point on the joint polyline (extended past both ends)
                best, s = 1e9, 0.0
                for i in range(n - 1):
                    a, ab = pts[i], pts[i + 1] - pts[i]
                    t = (p - a).dot(ab) / ab.length_squared
                    t = max(-10.0 if i == 0 else 0.0, min(10.0 if i == n - 2 else 1.0, t))
                    d = (a + ab * t - p).length
                    if d < best:
                        best, s = d, arc[i] + t * segs[i]
                k = 0
                while k < n - 1 and s >= arc[k + 1]:
                    k += 1
                ws = {chain[k]: 1.0}
                # blend across the joint nearest this point
                for j in (k, k + 1):
                    if j < 1 or j > n - 1:
                        continue
                    width = max(0.05, blend * min(segs[j - 1], segs[j] if j < n - 1 else segs[j - 1]))
                    x = (s - arc[j]) / width
                    if -1 < x < 1:
                        t = (x + 1) / 2
                        t = t * t * (3 - 2 * t)
                        ws = {chain[j - 1]: 1 - t, chain[j]: t}
                for b, w in ws.items():
                    if w > 0.001:
                        group(b).add([v.index], w, 'REPLACE')
        self.parts.append(ob)
        return ob

    def ring(self, bm, centre, u, v, w, h, n, power, phase=0.5, jitter=0.0):
        verts = []
        for i in range(n):
            t = 2 * math.pi * (i + phase) / n
            c, s = math.cos(t), math.sin(t)
            x = math.copysign(abs(c) ** (2 / power), c) * w / 2
            y = math.copysign(abs(s) ** (2 / power), s) * h / 2
            j = 1 + (self.rng.uniform(-jitter, jitter) if jitter else 0)
            verts.append(bm.verts.new(centre + (u * x + v * y) * j))
        return verts

    def loft(self, name, sections, u, v, n, power, mat, cap_start=True, cap_end=True, jitter=0.0, uv_start=0.0,
             uv_reverse=False, **skin):
        """sections: (centre, width along u, height along v) or (centre, w, h, u, v) to twist a section.
        mat: a material key, or f(face centre, band index, face index around) -> key.
        UVs: u is the distance along the loft in metres from uv_start (counting down if uv_reverse), v goes round it;
        the flowing-oil shader runs its pulses along u."""
        bm = bmesh.new()
        keys = []
        rings = []
        arc = [0.0]
        for k, sec in enumerate(sections):
            c, w, h = Vector(sec[0]), sec[1], sec[2]
            uu, vv = (Vector(sec[3]), Vector(sec[4])) if len(sec) > 3 else (Vector(u), Vector(v))
            rings.append(self.ring(bm, c, uu, vv, w, h, n, power, jitter=jitter))
            if k > 0:
                arc.append(arc[-1] + (Vector(sec[0]) - Vector(sections[k - 1][0])).length)
        total = arc[-1]
        ucoord = [uv_start + ((total - a) if uv_reverse else a) for a in arc]
        uv_layer = bm.loops.layers.uv.new("UVMap")
        faces = []
        for k in range(len(rings) - 1):
            a, b = rings[k], rings[k + 1]
            for i in range(n):
                j = (i + 1) % n
                f = bm.faces.new((a[i], a[j], b[j], b[i]))
                for loop, (kk, ii) in zip(f.loops, ((k, i), (k, i + 1), (k + 1, i + 1), (k + 1, i))):
                    loop[uv_layer].uv = (ucoord[kk], ii / n)
                faces.append((f, k, i))
        if cap_start:
            f = bm.faces.new(list(reversed(rings[0])))
            for loop in f.loops:
                loop[uv_layer].uv = (ucoord[0], 0.5)
            faces.append((f, -1, -1))
        if cap_end:
            f = bm.faces.new(rings[-1])
            for loop in f.loops:
                loop[uv_layer].uv = (ucoord[-1], 0.5)
            faces.append((f, len(rings) - 1, -1))
        for f, k, i in faces:
            key = mat if isinstance(mat, str) else mat(f.calc_center_median(), k, i)
            if key not in keys:
                keys.append(key)
            f.material_index = keys.index(key)
        return self._skin(self._object(name, bm, keys), **skin)

    def blob(self, name, centre, radii, mat, subdiv=1, rot=(0, 0, 0), jitter=0.0, **skin):
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
        if jitter:
            for v in bm.verts:
                v.co *= 1 + self.rng.uniform(-jitter, jitter)
        m = Matrix.Translation(Vector(centre)) @ Matrix.Rotation(rot[2], 4, 'Z') @ Matrix.Rotation(rot[1], 4, 'Y') @ \
            Matrix.Rotation(rot[0], 4, 'X') @ Matrix.Diagonal((*radii, 1))
        bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
        return self._skin(self._object(name, bm, [mat]), **skin)

    def box(self, name, centre, size, mat, rot=(0, 0, 0), taper=1.0, **skin):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        if taper != 1.0:
            for v in bm.verts:
                if v.co.z > 0:
                    v.co.x *= taper
                    v.co.y *= taper
        m = Matrix.Translation(Vector(centre)) @ Matrix.Rotation(rot[2], 4, 'Z') @ Matrix.Rotation(rot[1], 4, 'Y') @ \
            Matrix.Rotation(rot[0], 4, 'X') @ Matrix.Diagonal((*size, 1))
        bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
        return self._skin(self._object(name, bm, [mat]), **skin)

    def spike(self, name, base, direction, length, radius, mat, n=4, bend=None, segments=1, **skin):
        """A cone (n sides) from base along direction; bend curves it toward that vector along its length."""
        base, d = Vector(base), Vector(direction).normalized()
        u = d.orthogonal().normalized()
        v = d.cross(u)
        bm = bmesh.new()
        rings = []
        pts = []
        for s in range(segments + 1):
            t = s / (segments + 1)
            off = d * (length * t)
            if bend is not None:
                off += Vector(bend) * (length * t * t)
            pts.append(base + off)
            r = radius * (1 - t)
            rings.append([bm.verts.new(base + off + (u * math.cos(2 * math.pi * i / n) + v * math.sin(2 * math.pi * i / n)) * r)
                          for i in range(n)])
        tip_off = d * length + (Vector(bend) * length if bend is not None else Vector())
        tip = bm.verts.new(base + tip_off)
        for k in range(len(rings) - 1):
            a, b = rings[k], rings[k + 1]
            for i in range(n):
                j = (i + 1) % n
                bm.faces.new((a[i], a[j], b[j], b[i]))
        last = rings[-1]
        for i in range(n):
            bm.faces.new((last[i], last[(i + 1) % n], tip))
        bm.faces.new(list(reversed(rings[0])))
        return self._skin(self._object(name, bm, [mat]), **skin)

    def tube(self, name, a, b, ra, rb, mat, n=6, power=2.0, **skin):
        a, b = Vector(a), Vector(b)
        axis = (b - a).normalized()
        u = axis.orthogonal().normalized()
        v = axis.cross(u)
        return self.loft(name, [(a, ra * 2, ra * 2), (b, rb * 2, rb * 2)], u, v, n, power, mat, **skin)

    def path(self, name, points, radii, mat, n=6, power=2.0, **skin):
        """A tube through points with a radius per point (rounded square-ish sections facing along the path)."""
        pts = [Vector(p) for p in points]
        secs = []
        for i, p in enumerate(pts):
            d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
            u = d.cross(Z if abs(d.dot(Z)) < 0.95 else Y).normalized()
            v = u.cross(d).normalized()
            r = radii[i] if isinstance(radii, (list, tuple)) else radii
            rw, rh = (r if isinstance(r, (int, float)) else r[0]), (r if isinstance(r, (int, float)) else r[1])
            secs.append((p, rw * 2, rh * 2, u, v))
        return self.loft(name, secs, X, Z, n, power, mat, **skin)

    def shell(self, name, points, radii, mats=("Rock", "RockDark", "Basalt"), weights=(5, 3, 2), cell=0.6, gap=0.13,
              thickness=0.2, lift=0.05, skip=None, taper=0.78, squash=1.0, **skin):
        """Covers a tube (through points, a radius per point) with angular rock slabs, leaving thin cracks between
        them so whatever is underneath (the flowing oil) shows through. Rows are staggered like cobbles.
        skip(position, outward normal) -> True leaves a slab out (the fuel tank's window, the belly)."""
        pts = [Vector(p) for p in points]
        rad = list(radii) if isinstance(radii, (list, tuple)) else [radii] * len(pts)
        arc = [0.0]
        for i in range(1, len(pts)):
            arc.append(arc[-1] + (pts[i] - pts[i - 1]).length)
        total = arc[-1]
        bm = bmesh.new()
        keys = list(mats)
        rows = max(1, int(round(total / cell)))
        step = total / rows
        for row in range(rows):
            s = (row + 0.5) * step
            k = 0
            while k < len(arc) - 2 and arc[k + 1] < s:
                k += 1
            t = (s - arc[k]) / max(1e-6, arc[k + 1] - arc[k])
            c = pts[k].lerp(pts[k + 1], t)
            d = (pts[k + 1] - pts[k]).normalized()
            r = rad[k] + (rad[k + 1] - rad[k]) * t
            u = d.cross(Z if abs(d.dot(Z)) < 0.95 else Y).normalized()
            v = u.cross(d).normalized()
            around = max(4, int(round(2 * math.pi * r / cell)))
            offset = (row % 2) * 0.5
            for i in range(around):
                a = 2 * math.pi * (i + offset + self.rng.uniform(-0.08, 0.08)) / around
                normal = (u * math.cos(a) + v * math.sin(a) * squash).normalized()
                pos = c + (u * math.cos(a) + v * math.sin(a) * squash) * r + normal * (thickness * 0.35 + lift)
                if skip is not None and skip(pos, normal):
                    continue
                side = d.cross(normal).normalized()
                outer = r + thickness * 0.35 + lift
                w = 2 * math.pi * outer / around * (1 - gap) * self.rng.uniform(0.95, 1.08)
                l = step * (1 - gap) * self.rng.uniform(0.94, 1.06)
                h = thickness * self.rng.uniform(0.75, 1.3)
                ret = bmesh.ops.create_cube(bm, size=1.0)
                verts = list(ret["verts"])
                for vt in verts:
                    if vt.co.z > 0:
                        vt.co.x *= taper * self.rng.uniform(0.9, 1.05)
                        vt.co.y *= taper * self.rng.uniform(0.9, 1.05)
                        vt.co.z *= self.rng.uniform(0.85, 1.25)
                # a faceted top like cut stone: the top face pokes up to an off-centre point
                top = [f for f in {f for vt in verts for f in vt.link_faces} if all(v.co.z > 0 for v in f.verts)]
                if top:
                    poked = bmesh.ops.poke(bm, faces=top, offset=self.rng.uniform(0.15, 0.55))
                    for vt in poked["verts"]:
                        vt.co.x += self.rng.uniform(-0.15, 0.15)
                        vt.co.y += self.rng.uniform(-0.15, 0.15)
                    verts = list({v for f in poked["faces"] for v in f.verts} | set(verts))
                basis = Matrix((side, d, normal)).transposed().to_4x4()
                tilt = Matrix.Rotation(self.rng.uniform(-0.05, 0.05), 4, 'X') @ Matrix.Rotation(self.rng.uniform(-0.05, 0.05), 4, 'Y') @                     Matrix.Rotation(self.rng.uniform(-0.18, 0.18), 4, 'Z')
                m = Matrix.Translation(pos) @ basis @ tilt @ Matrix.Diagonal((w, l, h, 1))
                bmesh.ops.transform(bm, matrix=m, verts=verts)
                pick = self.rng.choices(range(len(keys)), weights=weights)[0]
                for f in {f for vt in verts for f in vt.link_faces}:
                    f.material_index = pick
        if not bm.verts:
            bm.free()
            return None
        return self._skin(self._object(name, bm, keys), **skin)

    def sheet(self, name, verts, faces, mat, weights, two_sided=True):
        """A membrane: verts (positions), faces (index tuples), weights [(bone, w), ...] per vertex."""
        bm = bmesh.new()
        vs = [bm.verts.new(Vector(p)) for p in verts]
        ws = list(weights)
        for f in faces:
            bm.faces.new([vs[i] for i in f])
        if two_sided:
            back = [bm.verts.new(Vector(p)) for p in verts]
            for f in faces:
                bm.faces.new([back[i] for i in reversed(f)])
            ws = ws + ws
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)   # keep the face winding as given: both sides exist
        bm.free()
        me.materials.append(self.mats[mat])
        for poly in me.polygons:
            poly.use_smooth = False
        ob = bpy.data.objects.new(name, me)
        self.scene.collection.objects.link(ob)
        return self._skin(ob, weights=ws)

    # ------------------------------------------------------------ build: armature + one skinned mesh
    def build(self):
        arm_data = bpy.data.armatures.new(self.name + "Armature")
        arm = bpy.data.objects.new(self.name + "Rig", arm_data)
        self.scene.collection.objects.link(arm)
        view_layer = self.scene.view_layers[0]
        view_layer.objects.active = arm
        with bpy.context.temp_override(scene=self.scene, view_layer=view_layer, active_object=arm, object=arm):
            bpy.ops.object.mode_set(mode='EDIT')
            for name in self.order:
                head, parent = self.bones[name]
                eb = arm_data.edit_bones.new(name)
                eb.head = head
                eb.tail = head + Z * 0.25
                eb.roll = 0.0
                if parent is not None:
                    eb.parent = arm_data.edit_bones[parent]
                    eb.use_connect = False
            bpy.ops.object.mode_set(mode='OBJECT')
        self.arm = arm
        # one mesh
        obs = self.parts
        with bpy.context.temp_override(scene=self.scene, view_layer=view_layer, active_object=obs[0],
                                       selected_editable_objects=obs, selected_objects=obs, object=obs[0]):
            bpy.ops.object.join()
        mesh = obs[0]
        mesh.name = mesh.data.name = self.name + "Mesh"
        mesh.parent = arm
        mod = mesh.modifiers.new("Armature", 'ARMATURE')
        mod.object = arm
        # every vertex must have a weight
        unweighted = sum(1 for v in mesh.data.vertices if not v.groups)
        self.mesh = mesh
        for pb in arm.pose.bones:
            pb.rotation_mode = 'XYZ'
        return unweighted

    # ------------------------------------------------------------ animation
    def clip(self, name, keys, loop=False, events=None):
        """keys: [(frame, {bone: (pitch, yaw, roll)} and optionally "Root@": (left, up, forward))]. Each key is
        the boss's base pose with its overrides; every bone named anywhere in the clip is keyed on every key."""
        self.clips.append((name, keys, loop, events or {}))

    def bake_clips(self):
        arm = self.arm
        ad = arm.animation_data or arm.animation_data_create()
        table = {}
        for name, keys, loop, events in self.clips:
            # One action per clip, named "<Boss>_<Clip>" so bosses built in the same Blender session never clash
            # (Blender would rename a second "Idle" to "Idle.001"). Unity strips the prefix.
            full_name = self.name + "_" + name
            old = bpy.data.actions.get(full_name)
            if old is not None:
                bpy.data.actions.remove(old)
            act = bpy.data.actions.new(full_name)
            act.use_fake_user = True
            slot = None
            try:
                slot = act.slots.new(id_type='OBJECT', name=arm.name)
            except Exception:
                pass
            ad.action = act
            if slot is not None:
                try:
                    ad.action_slot = slot
                except Exception:
                    pass
            bones = {k for k in self.base if k != "Root@"}
            for _, pose in keys:
                bones |= {b for b in pose if b != "Root@"}
            use_root_loc = any("Root@" in pose for _, pose in keys)
            for frame, pose in keys:
                full = dict(self.base)
                full.update(pose)
                for b in bones:
                    pb = arm.pose.bones[b]
                    p, yw, r = full.get(b, (0, 0, 0))
                    # local axes of an up-pointing bone: X = world X, Y = world Z (up), Z = world -Y (forward)
                    pb.rotation_euler = (math.radians(p), math.radians(yw), math.radians(r))
                    pb.keyframe_insert("rotation_euler", frame=frame, group=b)
                if use_root_loc:
                    pb = arm.pose.bones["Root"]
                    l, up, fw = full.get("Root@", (0, 0, 0))
                    pb.location = (l, up, fw)
                    pb.keyframe_insert("location", frame=frame, group="Root")
            frames = max(f for f, _ in keys)
            table[name] = {"length": round(frames / FPS, 4), "loop": loop,
                           "events": {k: round(v / FPS, 4) if not isinstance(v, list) else [round(x / FPS, 4) for x in v]
                                      for k, v in events.items()}}
            for fc in _fcurves(act):
                for kp in fc.keyframe_points:
                    kp.interpolation = 'BEZIER'
                    kp.handle_left_type = kp.handle_right_type = 'AUTO_CLAMPED'
                if loop:
                    try:
                        fc.modifiers.new('CYCLES')
                    except Exception:
                        pass
        ad.action = None
        for pb in arm.pose.bones:
            pb.rotation_euler = (0, 0, 0)
            pb.location = (0, 0, 0)
        self.table = table
        return table

    def pose_at(self, clip, frame):
        """Poses the rig at a frame of a clip (for preview renders)."""
        act = bpy.data.actions[self.name + "_" + clip]
        ad = self.arm.animation_data
        ad.action = act
        try:
            ad.action_slot = act.slots[0]
        except Exception:
            pass
        self.scene.frame_set(int(frame))

    # ------------------------------------------------------------ export + previews
    def export(self, fbx, blend, table_path):
        os.makedirs(os.path.dirname(fbx), exist_ok=True)
        os.makedirs(os.path.dirname(blend), exist_ok=True)
        self.arm.animation_data.action = None
        view_layer = self.scene.view_layers[0]
        for ob in self.scene.objects:
            ob.select_set(ob in (self.arm, self.mesh))
        with bpy.context.temp_override(scene=self.scene, view_layer=view_layer, selected_objects=[self.arm, self.mesh],
                                       active_object=self.arm):
            bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={'ARMATURE', 'MESH'},
                                     apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                                     bake_space_transform=False, mesh_smooth_type='FACE', add_leaf_bones=False,
                                     primary_bone_axis='Y', secondary_bone_axis='X', use_armature_deform_only=False,
                                     bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
                                     bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True,
                                     bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode='STRIP')
        # Unity's JsonUtility reads lists, not dictionaries: clips and their events are lists of named entries.
        clips = []
        for name, info in self.table.items():
            events = [{"name": k, "times": v if isinstance(v, list) else [v]} for k, v in sorted(info["events"].items())]
            clips.append({"name": name, "length": info["length"], "loop": info["loop"], "events": events})
        mats = []
        for key, m in self.mats.items():
            p = m.node_tree.nodes.get("Principled BSDF")
            col = list(p.inputs["Base Color"].default_value)[:3]
            em = list(p.inputs["Emission Color"].default_value)[:3]
            mats.append({"name": m.name, "key": key, "color": [round(c, 4) for c in col],
                         "emission": [round(c, 4) for c in em],
                         "emissionStrength": round(p.inputs["Emission Strength"].default_value, 3),
                         "metallic": round(p.inputs["Metallic"].default_value, 3),
                         "smoothness": round(1 - p.inputs["Roughness"].default_value, 3)})
        with open(table_path, "w") as f:
            json.dump({"boss": self.name, "colorSpace": "linear", "clips": clips, "materials": mats}, f, indent=1)
        bpy.ops.wm.save_as_mainfile(filepath=blend, copy=True)

    def previews(self, folder, shots, size=(1100, 800), camera=None):
        """shots: [(file name, clip or None, frame, camera location, look at)]."""
        scene = self.scene
        os.makedirs(folder, exist_ok=True)
        cam_data = bpy.data.cameras.get("BossPreviewCam") or bpy.data.cameras.new("BossPreviewCam")
        cam_data.lens = 35
        cam = bpy.data.objects.new("PreviewCamera", cam_data)
        scene.collection.objects.link(cam)
        scene.camera = cam
        sun_data = bpy.data.lights.get("BossPreviewSun") or bpy.data.lights.new("BossPreviewSun", 'SUN')
        sun_data.energy = 3.2
        sun = bpy.data.objects.new("PreviewSun", sun_data)
        sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
        scene.collection.objects.link(sun)
        fill_data = bpy.data.lights.get("BossPreviewFill") or bpy.data.lights.new("BossPreviewFill", 'SUN')
        fill_data.energy = 0.8
        fill = bpy.data.objects.new("PreviewFill", fill_data)
        fill.rotation_euler = (math.radians(60), 0, math.radians(150))
        scene.collection.objects.link(fill)
        ground_me = bpy.data.meshes.new("PreviewGround")
        bm = bmesh.new()
        bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=40)
        bm.to_mesh(ground_me)
        bm.free()
        gm = bpy.data.materials.get("PreviewSand") or bpy.data.materials.new("PreviewSand")
        gm.use_nodes = True
        gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*srgb(214, 178, 122), 1)
        ground_me.materials.append(gm)
        ground = bpy.data.objects.new("PreviewGround", ground_me)
        scene.collection.objects.link(ground)
        world = bpy.data.worlds.get("BossWorld") or bpy.data.worlds.new("BossWorld")
        world.use_nodes = True
        world.node_tree.nodes["Background"].inputs["Color"].default_value = (*srgb(170, 196, 220), 1)
        world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
        scene.world = world
        for engine in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT', 'BLENDER_WORKBENCH'):
            try:
                scene.render.engine = engine
                break
            except TypeError:
                continue
        scene.render.resolution_x, scene.render.resolution_y = size
        scene.render.film_transparent = False
        out = []
        for fname, clip, frame, loc, at in shots:
            if clip is None:
                self.arm.animation_data.action = None
                for pb in self.arm.pose.bones:
                    pb.rotation_euler = (0, 0, 0)
                    pb.location = (0, 0, 0)
                scene.frame_set(0)
            else:
                self.pose_at(clip, frame)
            cam.location = Vector(loc)
            cam.rotation_euler = (Vector(at) - cam.location).to_track_quat('-Z', 'Y').to_euler()
            scene.render.filepath = os.path.join(folder, fname + ".png")
            bpy.ops.render.render(write_still=True, scene=scene.name)
            out.append(scene.render.filepath)
        for o in (cam, sun, fill, ground):
            bpy.data.objects.remove(o, do_unlink=True)
        self.arm.animation_data.action = None
        return out

    def stats(self):
        return {"triangles": sum(len(p.vertices) - 2 for p in self.mesh.data.polygons),
                "bones": len(self.order), "clips": len(self.clips),
                "size": [round(c, 2) for c in self.mesh.dimensions]}


def _fcurves(act):
    """F-curves of an action (layered actions in Blender 4.4+, plain ones before)."""
    try:
        return list(act.fcurves)
    except Exception:
        pass
    out = []
    try:
        for layer in act.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    out.extend(bag.fcurves)
    except Exception:
        pass
    return out


def lerp_pose(a, b, t):
    out = {}
    for k in set(a) | set(b):
        va, vb = a.get(k, (0, 0, 0)), b.get(k, (0, 0, 0))
        out[k] = tuple(x + (y - x) * t for x, y in zip(va, vb))
    return out


def merge(*poses):
    out = {}
    for p in poses:
        out.update(p)
    return out


def sym(pose):
    """Adds the right-side (R) mirror of every left-side (L) bone in a pose: same pitch, yaw and roll negated."""
    out = dict(pose)
    for k, v in pose.items():
        if k.endswith("L") and k[:-1] + "R" not in pose:
            out[k[:-1] + "R"] = (v[0], -v[1], -v[2])
    return out


def mirror(pose):
    """Swaps left and right: for the mirrored version of a one-sided move (a left swipe into a right one)."""
    out = {}
    for k, v in pose.items():
        if k == "Root@":
            out[k] = (-v[0], v[1], v[2])
            continue
        name = k[:-1] + ("R" if k.endswith("L") else "L") if k[-1] in "LR" and k[:-1] else k
        out[name] = (v[0], -v[1], -v[2])
    return out


def contact_sheet(paths, out, cols=4, scale=2):
    """Tiles preview renders into one image (downscaled by `scale`), for reviewing many poses at once."""
    import numpy as np
    tiles = []
    for p in paths:
        img = bpy.data.images.load(p, check_existing=False)
        w, h = img.size
        px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::scale, ::scale]
        tiles.append(px)
        bpy.data.images.remove(img)
    th, tw = tiles[0].shape[:2]
    rows = (len(tiles) + cols - 1) // cols
    sheet = np.ones((rows * th, cols * tw, 4), dtype=np.float32)
    for i, t in enumerate(tiles):
        r, c = i // cols, i % cols
        y0 = (rows - 1 - r) * th   # Blender images start at the bottom row
        sheet[y0:y0 + th, c * tw:c * tw + tw] = t[:th, :tw]
    img = bpy.data.images.new("ContactSheet", cols * tw, rows * th)
    img.pixels = sheet.ravel()
    img.filepath_raw = out
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)
    return out

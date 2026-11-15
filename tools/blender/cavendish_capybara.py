# Cavendish as a normal four-legged capybara in a space suit, low poly and flat shaded.
# Run inside Blender (Blender MCP, or: blender --background --python tools/blender/cavendish_capybara.py).
# Builds the model in its own scene "Cavendish", saves the .blend and a preview, and exports the FBX for Unity.
# Units are metres; Z is up and the capybara faces -Y (Blender's front view).
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

REPO = os.environ.get("SIGNAL_LOST_REPO", r"C:\Users\Lawson\signal-lost")
FBX = os.path.join(REPO, "signal-lost", "Assets", "Prefabs", "NPC", "Cavendish", "Models", "CavendishCapybara.fbx")
BLEND = os.path.join(REPO, "art", "cavendish-capybara", "CavendishCapybara.blend")
PREVIEW = os.path.join(REPO, "art", "cavendish-capybara", "CavendishCapybara.png")

# ---------------------------------------------------------------- scene
scene = bpy.data.scenes.get("Cavendish")
if scene is not None:
    for o in list(scene.objects):
        bpy.data.objects.remove(o, do_unlink=True)
else:
    scene = bpy.data.scenes.new("Cavendish")
if bpy.context.window is not None:
    bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0

# ---------------------------------------------------------------- materials (names match the Unity materials)
def material(name, rgb, rough=0.7, metal=0.0, alpha=1.0, emit=None):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = (*rgb, 1.0)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    p.inputs["Alpha"].default_value = alpha
    if emit is not None:
        p.inputs["Emission Color"].default_value = (*emit, 1.0)
        p.inputs["Emission Strength"].default_value = 3.0
    if alpha < 1.0:
        try:
            m.surface_render_method = 'BLENDED'
        except Exception:
            pass
    m.diffuse_color = (*rgb, alpha)
    return m

def srgb(r, g, b):
    f = lambda c: c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (f(r / 255), f(g / 255), f(b / 255))

M = {
    "Fur":     material("CavFur",     srgb(158, 98, 52)),
    "FurDark": material("CavFurDark", srgb(96, 70, 52)),
    "Muzzle":  material("CavMuzzle",  srgb(124, 88, 62)),
    "Nose":    material("CavNose",    srgb(38, 34, 34), rough=0.5),
    "Eye":     material("CavEye",     srgb(14, 12, 12), rough=0.15),
    "Suit":    material("CavSuit",    srgb(232, 232, 226)),
    "Accent":  material("CavAccent",  srgb(240, 118, 28)),
    "Metal":   material("CavMetal",   srgb(122, 128, 136), rough=0.4, metal=0.6),
    "Boot":    material("CavBoot",    srgb(52, 52, 58)),
    "Patch":   material("CavPatch",   srgb(48, 92, 170)),
    "Shine":   material("CavShine",   srgb(255, 255, 255), rough=0.2, emit=srgb(255, 255, 255)),
    "Light":   material("CavLight",   srgb(255, 70, 50), emit=srgb(255, 70, 50)),
    "Glass":   material("CavHelmetGlass", srgb(205, 230, 245), rough=0.05, alpha=0.18),
}

# ---------------------------------------------------------------- mesh helpers
# Every part belongs to a group; each group becomes one object in Unity, pivoted where it moves from (the head turns
# at the neck, ears twitch at their base, eyes blink about their middle).
parts = []
GROUP = "Body"
PIVOTS = {}

def new_object(name, bm, mats):
    me = bpy.data.meshes.new(name)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(M[m])
    for poly in me.polygons:
        poly.use_smooth = False
    ob = bpy.data.objects.new(name, me)
    scene.collection.objects.link(ob)
    return ob

def ring(bm, centre, u, v, w, h, n, power, phase=0.5):
    verts = []
    for i in range(n):
        t = 2 * math.pi * (i + phase) / n
        c, s = math.cos(t), math.sin(t)
        x = math.copysign(abs(c) ** (2 / power), c) * w / 2
        y = math.copysign(abs(s) ** (2 / power), s) * h / 2
        verts.append(bm.verts.new(centre + u * x + v * y))
    return verts

def loft(name, sections, u, v, n, power, material_of, cap_start=True, cap_end=True):
    """sections: (centre, width along u, height along v). material_of(face centre, band index) -> material key."""
    bm = bmesh.new()
    keys = []
    rings = [ring(bm, Vector(c), Vector(u), Vector(v), w, h, n, power) for c, w, h in sections]
    faces = []
    for k in range(len(rings) - 1):
        a, b = rings[k], rings[k + 1]
        for i in range(n):
            j = (i + 1) % n
            f = bm.faces.new((a[i], a[j], b[j], b[i]))
            faces.append((f, k))
    if cap_start:
        faces.append((bm.faces.new(rings[0]), -1))
    if cap_end:
        faces.append((bm.faces.new(rings[-1]), len(rings) - 1))
    for f, k in faces:
        key = material_of(f.calc_center_median(), k)
        if key not in keys:
            keys.append(key)
        f.material_index = keys.index(key)
    ob = new_object(name, bm, keys)
    parts.append((GROUP, ob))
    return ob

def blob(name, centre, radii, key, subdiv=1, rot=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    m = Matrix.Translation(centre) @ Matrix.Rotation(rot[2], 4, 'Z') @ Matrix.Rotation(rot[1], 4, 'Y') @ \
        Matrix.Rotation(rot[0], 4, 'X') @ Matrix.Diagonal((*radii, 1))
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    ob = new_object(name, bm, [key])
    parts.append((GROUP, ob))
    return ob

def box(name, centre, size, key, bevel=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(centre) @ Matrix.Diagonal((*size, 1)), verts=bm.verts)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1, affect='EDGES')
    ob = new_object(name, bm, [key])
    parts.append((GROUP, ob))
    return ob

def tube(name, a, b, radius, key, n=8, cap=True):
    a, b = Vector(a), Vector(b)
    axis = (b - a).normalized()
    u = axis.orthogonal().normalized()
    v = axis.cross(u)
    return loft(name, [(a, radius * 2, radius * 2), (b, radius * 2, radius * 2)], u, v, n, 2.0,
                lambda c, k: key, cap, cap)

def torus(name, centre, axis, major, minor, key, seg=14, side=6):
    centre, axis = Vector(centre), Vector(axis).normalized()
    u = axis.orthogonal().normalized()
    v = axis.cross(u)
    bm = bmesh.new()
    rings = []
    for i in range(seg):
        t = 2 * math.pi * i / seg
        radial = u * math.cos(t) + v * math.sin(t)
        rc = centre + radial * major
        rings.append([bm.verts.new(rc + radial * (minor * math.cos(2 * math.pi * j / side)) +
                                   axis * (minor * math.sin(2 * math.pi * j / side))) for j in range(side)])
    for i in range(seg):
        a, b = rings[i], rings[(i + 1) % seg]
        for j in range(side):
            k = (j + 1) % side
            bm.faces.new((a[j], a[k], b[k], b[j]))
    ob = new_object(name, bm, [key])
    parts.append((GROUP, ob))
    return ob

X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))

# ---------------------------------------------------------------- body: a capybara's barrel, rump a little higher than the shoulders
body = [(-0.66, 0.74, 0.30, 0.34), (-0.58, 0.70, 0.58, 0.64), (-0.38, 0.66, 0.72, 0.74), (-0.14, 0.66, 0.76, 0.76),
        (-0.02, 0.66, 0.76, 0.76), (0.28, 0.69, 0.80, 0.82), (0.54, 0.71, 0.72, 0.74), (0.70, 0.72, 0.48, 0.52),
        (0.78, 0.72, 0.18, 0.22)]
loft("Body", [((0, y, z), w, h) for y, z, w, h in body], X, Z, 12, 2.3,
     lambda c, k: "Accent" if k == 3 else "Suit")   # an orange band round the middle

# ---------------------------------------------------------------- legs: short and sturdy, thick thighs behind; grey knee joints and boots
def leg(name, x, y_top, y_foot, sections):
    secs = []
    for z, w in sections:
        f = max(0.0, (sections[1][0] - z) / (sections[1][0] - sections[-1][0]))
        secs.append(((x, y_top + (y_foot - y_top) * f, z), w, w * 1.08))
    knee = [i for i, (z, w) in enumerate(sections) if abs(z - 0.26) < 0.001]
    loft(name, secs, X, Y, 8, 2.2, lambda c, k: "Metal" if knee and k == knee[0] else "Suit", False, True)
    yf = y_foot
    loft(name + "Boot", [((x, yf, 0.16), 0.17, 0.19), ((x, yf - 0.02, 0.06), 0.20, 0.25), ((x, yf - 0.02, 0.0), 0.20, 0.25)],
         X, Y, 8, 3.0, lambda c, k: "Boot")

for side in (-1, 1):
    leg("FrontLeg" + ("L" if side < 0 else "R"), 0.18 * side, -0.40, -0.43, [(0.72, 0.24), (0.40, 0.22), (0.26, 0.18), (0.20, 0.17), (0.12, 0.15)])
    leg("BackLeg" + ("L" if side < 0 else "R"), 0.19 * side, 0.42, 0.52, [(0.80, 0.30), (0.46, 0.27), (0.36, 0.22), (0.26, 0.19), (0.20, 0.17), (0.12, 0.15)])

# ---------------------------------------------------------------- head: big, tall and blunt like a brick, the top sloping down to the nose
GROUP = "Head"
PIVOTS["Head"] = Vector((0, -0.62, 0.92))
head = [(-0.56, 0.98, 0.30, 0.38), (-0.64, 1.00, 0.40, 0.50), (-0.78, 0.98, 0.41, 0.50), (-0.92, 0.94, 0.37, 0.46),
        (-1.04, 0.90, 0.32, 0.42), (-1.12, 0.88, 0.28, 0.36), (-1.15, 0.88, 0.20, 0.26)]
def head_material(c, k):
    if c.y < -1.0 and (c.z < 0.82 or c.y < -1.14):
        return "Muzzle"
    if c.z > 1.15 and c.y > -0.86:
        return "FurDark"
    return "Fur"
loft("Head", [((0, y, z), w, h) for y, z, w, h in head], X, Z, 12, 3.2, head_material)
for side in (-1, 1):
    s = "L" if side < 0 else "R"
    eye = Vector((0.196 * side, -0.82, 1.11))
    GROUP = "Eye" + s
    PIVOTS[GROUP] = eye
    blob("Eye" + s, eye, (0.03, 0.058, 0.046), "Eye", subdiv=2)
    # the shine sits on the open part of the eye, below the lid
    blob("Shine" + s, eye + Vector((0.029 * side, -0.02, -0.008)), (0.007, 0.011, 0.01), "Shine")
    GROUP = "Ear" + s
    PIVOTS[GROUP] = Vector((0.145 * side, -0.63, 1.235))
    blob("Ear" + s, Vector((0.145 * side, -0.63, 1.275)), (0.055, 0.03, 0.068), "FurDark", rot=(0.2, -0.35 * side, 0))
    blob("EarInner" + s, Vector((0.147 * side, -0.655, 1.28)), (0.035, 0.012, 0.045), "Nose", rot=(0.2, -0.35 * side, 0))
    GROUP = "Head"
    # a relaxed upper lid over the top of each eye: the calm, content capybara look
    blob("Lid" + s, eye + Vector((0.003 * side, 0, 0.032)), (0.033, 0.062, 0.022), "FurDark", rot=(0.12 * side, 0, 0))
    for i, (up, out) in enumerate(((0.03, 0.10), (0.0, 0.11), (-0.03, 0.10))):
        root = Vector((0.115 * side, -1.10, 0.92 + up))
        tube("Whisker" + s + str(i), root, root + Vector((out * side, -0.03, up * 0.6)), 0.0035, "Eye", n=4)
    blob("Nostril" + s, Vector((0.05 * side, -1.168, 0.995)), (0.024, 0.01, 0.015), "Eye")
blob("NosePad", Vector((0, -1.135, 0.985)), (0.115, 0.035, 0.07), "Nose")
# a small smile: the split upper lip, curling up at both ends
smile = [(0.0, -1.158, 0.808), (0.04, -1.155, 0.812), (0.075, -1.143, 0.826), (0.098, -1.125, 0.845)]
for side in (-1, 1):
    for i in range(len(smile) - 1):
        a, b = smile[i], smile[i + 1]
        tube("Smile" + ("L" if side < 0 else "R") + str(i), (a[0] * side, a[1], a[2]), (b[0] * side, b[1], b[2]), 0.0075, "Eye", n=4)
GROUP = "Body"

# ---------------------------------------------------------------- suit details
collar_centre = Vector((0, -0.60, 0.90))
neck = Vector((0, -0.90, 0.44)).normalized()          # points from the body into the helmet
backpack = box("Backpack", Vector((0, 0.10, 1.11)), (0.46, 0.56, 0.20), "Metal", bevel=0.03)
for side in (-1, 1):
    tube("Tank" + ("L" if side < 0 else "R"), (0.12 * side, -0.14, 1.27), (0.12 * side, 0.34, 1.27), 0.075, "Accent")
tube("Antenna", (0.17, 0.34, 1.19), (0.17, 0.40, 1.55), 0.012, "Metal", n=6)
GROUP = "AntennaLight"
PIVOTS[GROUP] = Vector((0.17, 0.40, 1.57))
blob("AntennaLight", Vector((0.17, 0.40, 1.57)), (0.032, 0.032, 0.032), "Light")
GROUP = "Body"
box("ChestPanel", Vector((0, -0.64, 0.58)), (0.22, 0.05, 0.14), "Metal", bevel=0.015)
blob("ChestLight", Vector((0.05, -0.67, 0.60)), (0.02, 0.012, 0.02), "Light")
for side in (-1, 1):
    tube("Patch" + ("L" if side < 0 else "R"), (0.365 * side, -0.32, 0.80), (0.385 * side, -0.32, 0.80), 0.075, "Patch", n=8)

# ---------------------------------------------------------------- helmet: a clear faceted bubble, open where it meets the collar
bm = bmesh.new()
bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=1.0)
bmesh.ops.transform(bm, matrix=Matrix.Translation((0, -0.84, 0.99)) @ Matrix.Diagonal((0.37, 0.48, 0.45, 1)), verts=bm.verts)
bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces), plane_co=collar_centre,
                       plane_no=neck, clear_inner=True)
rim = [v for v in bm.verts if v.is_boundary]
rim_radius = sum(((v.co - collar_centre) - neck * (v.co - collar_centre).dot(neck)).length for v in rim) / max(1, len(rim))
helmet = new_object("Helmet", bm, ["Glass"])
torus("Collar", collar_centre, neck, rim_radius, 0.04, "Metal", seg=16, side=6)

# ---------------------------------------------------------------- one object per group, its origin at the group's pivot
names = {"Body": "CavendishBody", "Head": "CavendishHead", "AntennaLight": "CavendishAntennaLight"}
groups = {}
for g, ob in parts:
    groups.setdefault(g, []).append(ob)
objects = []
for g, obs in groups.items():
    for ob in scene.objects:
        ob.select_set(False)
    for ob in obs:
        ob.select_set(True)
    with bpy.context.temp_override(active_object=obs[0], selected_editable_objects=obs, selected_objects=obs):
        bpy.ops.object.join()
    ob = obs[0]
    ob.name = ob.data.name = names.get(g, "Cavendish" + g)
    pivot = PIVOTS.get(g, Vector((0, 0, 0)))
    ob.data.transform(Matrix.Translation(-pivot))
    ob.location = pivot
    objects.append(ob)
model = next(o for o in objects if o.name == "CavendishBody")
helmet.name = helmet.data.name = "CavendishHelmet"
objects.append(helmet)
# ---------------------------------------------------------------- export for Unity, save, preview
os.makedirs(os.path.dirname(FBX), exist_ok=True)
os.makedirs(os.path.dirname(BLEND), exist_ok=True)
for ob in scene.objects:
    ob.select_set(ob in objects)
with bpy.context.temp_override(selected_objects=objects, active_object=model):
    bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE',
                             add_leaf_bones=False, bake_anim=False, path_mode='STRIP')

cam_data = bpy.data.cameras.new("PreviewCamera")
cam_data.lens = 50
cam = bpy.data.objects.new("PreviewCamera", cam_data)
scene.collection.objects.link(cam)
cam.location = (-2.5, -3.0, 1.55)
cam.rotation_euler = (Vector((0, -0.15, 0.65)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam
sun_data = bpy.data.lights.new("PreviewSun", 'SUN')
sun_data.energy = 3.5
sun = bpy.data.objects.new("PreviewSun", sun_data)
sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
scene.collection.objects.link(sun)
world = bpy.data.worlds.get("CavendishWorld") or bpy.data.worlds.new("CavendishWorld")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.42, 0.5, 0.6, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9
scene.world = world
for engine in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT', 'BLENDER_WORKBENCH'):
    try:
        scene.render.engine = engine
        break
    except TypeError:
        continue
scene.render.resolution_x, scene.render.resolution_y = 1200, 900
scene.render.film_transparent = False
scene.render.filepath = PREVIEW
bpy.ops.render.render(write_still=True, scene=scene.name)
bpy.ops.wm.save_as_mainfile(filepath=BLEND, copy=True)

result = {
    "triangles": sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objects),
    "objects": [o.name for o in objects],
    "size": [round(c, 2) for c in model.dimensions],
    "rim_radius": round(rim_radius, 3),
    "fbx": FBX, "preview": PREVIEW, "engine": scene.render.engine,
}
print("CAVENDISH_RESULT", result)

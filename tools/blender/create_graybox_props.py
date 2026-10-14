"""Original low-poly Signal Lost props; no downloaded meshes, textures, or fonts.
Run with Blender --background --python this_file -- --output PATH [--render].
Output contains editable Blender sources and a Unity import payload. Does not edit scenes.
"""
import argparse
import json
import math
from pathlib import Path
import random
import sys

import bpy
from mathutils import Vector

parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--render', action='store_true')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
source_dir = args.output / 'Blender'
model_dir = args.output / 'Models'
source_dir.mkdir(parents=True, exist_ok=True)
model_dir.mkdir(parents=True, exist_ok=True)

# Blender coordinates: Z up, characters face -Y. Metric dimensions, applied transforms.
PALETTE = {
    'SuitIvory': ('D9DFDB', 0.05, 0.65),
    'SuitTeal': ('287F86', 0.25, 0.45),
    'SafetyOrange': ('E68435', 0.10, 0.55),
    'Graphite': ('29343D', 0.25, 0.65),
    'Steel': ('8CABB6', 0.65, 0.32),
    'Fur': ('9B704A', 0.0, 0.95),
    'Muzzle': ('B98B5E', 0.0, 0.95),
    'EarInner': ('67452F', 0.0, 0.95),
    'Eyes': ('111A21', 0.0, 0.28),
    'Highlight': ('F4F5E4', 0.0, 0.35),
    'Charcoal': ('30302F', 0.0, 1.0),
    'Ash': ('56534C', 0.0, 1.0),
    'Scorch': ('181C1E', 0.0, 1.0),
    'Rust': ('795743', 0.05, 0.95),
    'Basin': ('414744', 0.0, 0.95),
    'SignalMint': ('8CE7C6', 0.10, 0.40),
}
materials = {}


def rgb(hex_value):
    values = [int(hex_value[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in values)


def reset():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for mat in list(bpy.data.materials):
        bpy.data.materials.remove(mat)
    materials.clear()
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1
    for name, (color, metal, roughness) in PALETTE.items():
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = (*rgb(color), 1)
        mat.use_nodes = True
        shader = mat.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Base Color'].default_value = (*rgb(color), 1)
        shader.inputs['Metallic'].default_value = metal
        shader.inputs['Roughness'].default_value = roughness
        materials[name] = mat


def finish(obj, name, material):
    obj.name = name
    obj.data.materials.append(materials[material])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def box(name, pos, size, mat, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    obj = bpy.context.object
    obj.scale = size
    finish(obj, name, mat)
    if bevel:
        mod = obj.modifiers.new('Soft manufactured edges', 'BEVEL')
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def sphere(name, pos, scale, mat, segments=16, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1, location=pos)
    obj = bpy.context.object
    obj.scale = scale
    return finish(obj, name, mat)


def cylinder(name, pos, radius, depth, mat, vertices=16, direction=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=pos)
    obj = finish(bpy.context.object, name, mat)
    if direction:
        obj.rotation_euler = Vector(direction).to_track_quat('Z', 'Y').to_euler()
    return obj


def rod(name, start, end, radius, mat, vertices=12):
    a, b = Vector(start), Vector(end)
    return cylinder(name, (a + b) / 2, radius, (b - a).length, mat, vertices, b - a)


def ring(name, pos, major, minor, mat, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_segments=24, minor_segments=6,
                                   location=pos, major_radius=major, minor_radius=minor,
                                   rotation=rotation)
    return finish(bpy.context.object, name, mat)


def mesh_object(name, vertices, faces, mat):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(materials[mat])
    return obj


def capybara():
    # Short, stocky astronaut with a long, blunt capybara muzzle and small high ears.
    for side in (-1, 1):
        x = side * 0.24
        box('Boot', (x, -0.11, 0.115), (0.35, 0.53, 0.23), 'Graphite', 0.07)
        box('Boot toe', (x, -0.31, 0.14), (0.31, 0.10, 0.14), 'Steel', 0.03)
        sphere('Suit leg', (x, 0, 0.43), (0.18, 0.21, 0.34), 'SuitIvory')
        cylinder('Ankle seal', (x, 0, 0.28), 0.18, 0.08, 'SuitTeal')
    sphere('Pressure suit', (0, 0.035, 0.97), (0.51, 0.35, 0.59), 'SuitIvory', 20, 10)
    belt = ring('Waist seal', (0, 0.02, 0.72), 0.40, 0.055, 'SuitTeal')
    belt.scale.y = 0.76
    box('Life support pack', (0, 0.37, 1.12), (0.64, 0.27, 0.63), 'Graphite', 0.055)
    box('Pack cover', (0, 0.53, 1.12), (0.51, 0.10, 0.46), 'SuitTeal', 0.025)
    for side in (-1, 1):
        cylinder('Oxygen tank', (side * 0.25, 0.46, 1.13), 0.105, 0.49, 'Steel')
        start = (side * 0.44, 0.015, 1.23)
        end = (side * 0.64, -0.07, 0.83)
        rod('Suit sleeve', start, end, 0.155, 'SuitIvory')
        sphere('Shoulder patch', start, (0.17, 0.18, 0.19), 'SuitTeal')
        cylinder('Wrist seal', (side * 0.65, -0.075, 0.79), 0.14, 0.09, 'SafetyOrange')
        sphere('Glove', (side * 0.65, -0.09, 0.68), (0.14, 0.16, 0.16), 'Graphite')
    box('Chest panel housing', (0, -0.305, 1.075), (0.43, 0.10, 0.28), 'Graphite', 0.025)
    box('Chest screen', (-0.065, -0.366, 1.10), (0.21, 0.015, 0.12), 'SuitTeal', 0.008)
    for x, mat in ((0.085, 'SignalMint'), (0.15, 'SafetyOrange')):
        sphere('Panel button', (x, -0.374, 1.04), (0.025, 0.013, 0.025), mat, 8, 4)
    # An open helmet frame, not opaque glass obscuring the animal's face.
    cylinder('Helmet collar', (0, 0, 1.43), 0.39, 0.12, 'Graphite', 24)
    ring('Collar rim', (0, 0, 1.49), 0.375, 0.045, 'SafetyOrange')
    sphere('Capybara head', (0, -0.035, 1.76), (0.385, 0.36, 0.35), 'Fur', 20, 10)
    box('Long blunt muzzle', (0, -0.365, 1.68), (0.61, 0.53, 0.37), 'Muzzle', 0.115)
    for side in (-1, 1):
        sphere('Small round ear', (side * 0.27, 0.005, 2.065), (0.105, 0.085, 0.12), 'Fur')
        sphere('Ear inset', (side * 0.27, -0.061, 2.075), (0.060, 0.024, 0.071), 'EarInner')
        sphere('Eye', (side * 0.303, -0.277, 1.88), (0.053, 0.035, 0.047), 'Eyes')
        sphere('Eye glint', (side * 0.302 - 0.008, -0.310, 1.893), (0.011, 0.009, 0.011), 'Highlight', 8, 4)
        sphere('Nostril', (side * 0.18, -0.626, 1.77), (0.047, 0.018, 0.026), 'EarInner', 12, 6)
        cylinder('Helmet hinge', (side * 0.43, 0.03, 1.78), 0.13, 0.10, 'SuitTeal', direction=(1, 0, 0))
    box('Mouth line', (0, -0.631, 1.57), (0.32, 0.009, 0.015), 'EarInner', 0.004)
    # Rear arch and top lamp suggest an EVA helmet with the visor raised.
    helmet = ring('Raised helmet frame', (0, 0.09, 1.79), 0.44, 0.045,
                  'SuitIvory', (math.pi / 2, 0, 0))
    helmet.scale.z = 1.03
    box('Helmet lamp', (0, 0.06, 2.24), (0.17, 0.16, 0.085), 'Graphite', 0.015)
    box('Lamp lens', (0, -0.027, 2.24), (0.12, 0.018, 0.044), 'SignalMint', 0.006)
    for side in (-1, 1):
        points = [(side * 0.30, -0.27, 1.38), (side * 0.44, -0.29, 1.22),
                  (side * 0.44, -0.28, 0.99), (side * 0.30, -0.26, 0.88)]
        for a, b in zip(points, points[1:]):
            rod('Breathing hose', a, b, 0.035, 'SafetyOrange', 8)


def burnt_rock():
    rng = random.Random(18)
    count = 18
    variation = [rng.uniform(0.92, 1.08) for _ in range(count)]
    # Rings ascend outside, cross the rim, then descend into a genuine empty basin.
    specs = [(1.03, 0.0), (1.24, 0.28), (1.12, 0.76), (0.91, 1.01),
             (0.73, 0.91), (0.58, 0.64)]
    verts = []
    for j, (radius, z) in enumerate(specs):
        for i in range(count):
            a = 2 * math.pi * i / count
            height = z + (rng.uniform(-0.055, 0.055) if j in (1, 2, 3, 4) else 0)
            verts.append((radius * variation[i] * math.cos(a),
                          radius * variation[i] * 0.82 * math.sin(a), height))
    faces = []
    bands = []
    for band in range(len(specs) - 1):
        for i in range(count):
            a, b = band * count + i, band * count + (i + 1) % count
            c, d = b + count, a + count
            faces.extend([(a, b, c), (a, c, d)])
            bands.extend([band, band])
    faces.append(tuple(reversed(range(count))))
    bands.append(-1)
    faces.append(tuple(5 * count + i for i in range(count)))
    bands.append(5)
    obj = mesh_object('Recessed burnt rock', verts, faces, 'Charcoal')
    for name in ('Ash', 'Scorch', 'Rust', 'Basin'):
        obj.data.materials.append(materials[name])
    for poly, band in zip(obj.data.polygons, bands):
        poly.material_index = (4 if band == 5 else 2 if band in (3, 4)
                               else rng.choices([0, 1, 2, 3], [6, 2, 3, 1])[0])
    # Tiny angular chips embedded at the outside base, not in the pickup space.
    for i in range(7):
        a = i * 2 * math.pi / 7 + 0.2
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1,
            location=(1.04 * math.cos(a), 0.84 * math.sin(a), 0.09))
        chip = bpy.context.object
        chip.scale = (0.17, 0.13, 0.09)
        finish(chip, 'Charred fragment', 'Scorch' if i % 2 else 'Ash')


def flask():
    box('Rounded flask body', (0, 0, 0.13), (0.18, 0.075, 0.26), 'Steel', 0.024)
    box('Leather grip', (0, 0, 0.135), (0.185, 0.080, 0.13), 'EarInner', 0.012)
    box('Orange label', (0, -0.044, 0.135), (0.105, 0.008, 0.085), 'SafetyOrange', 0.005)
    # Geometric bottle emblem, no font or brand dependencies.
    box('Label bottle', (0, -0.050, 0.132), (0.029, 0.006, 0.043), 'SuitIvory', 0.004)
    box('Label bottle neck', (0, -0.050, 0.161), (0.013, 0.006, 0.018), 'SuitIvory', 0.002)
    cylinder('Flask neck', (0.037, 0, 0.274), 0.025, 0.040, 'Graphite')
    cylinder('Screw cap', (0.037, 0, 0.301), 0.034, 0.035, 'Steel')
    for i in range(12):
        angle = 2 * math.pi * i / 12
        cylinder('Cap knurl', (0.037 + 0.032 * math.cos(angle), 0.032 * math.sin(angle), 0.301),
                 0.003, 0.024, 'Graphite', 6)
    box('Cap retaining hinge', (0.080, 0, 0.265), (0.027, 0.019, 0.062), 'Steel', 0.005)
    box('Cap retaining arm', (0.063, 0, 0.291), (0.053, 0.019, 0.012), 'Steel', 0.003)


def gate():
    # Opening: X +/-1.4; lintel begins at Z 2.8. No threshold or hidden closed door.
    for side in (-1, 1):
        x = side * 1.70
        box('Gate upright', (x, 0, 1.4), (0.60, 0.65, 2.8), 'Graphite', 0.055)
        box('Foot', (x, 0, 0.12), (0.86, 0.90, 0.24), 'Steel', 0.03)
        box('Outer casing', (side * 1.91, 0, 1.4), (0.20, 0.72, 2.65), 'SuitIvory', 0.025)
        box('Front inset', (x, -0.345, 1.57), (0.37, 0.055, 1.85), 'SuitTeal', 0.025)
        box('Passage light', (side * 1.435, -0.355, 1.5), (0.04, 0.045, 2.3), 'SignalMint', 0.008)
        for z in (0.40, 0.62, 0.84):
            stripe = box('Warning stripe', (x, -0.384, z), (0.35, 0.025, 0.10), 'SafetyOrange', 0.008)
            stripe.rotation_euler.y = side * 0.26
        for z in (0.16, 2.63):
            cylinder('Frame bolt', (x, -0.376, z), 0.044, 0.04, 'Steel', 8, (0, -1, 0))
    box('Lintel', (0, 0, 3.00), (4.10, 0.70, 0.40), 'Graphite', 0.065)
    box('Lintel cover', (0, -0.365, 3.01), (3.55, 0.07, 0.27), 'SuitIvory', 0.022)
    box('Exit sign housing', (0, -0.421, 3.02), (1.14, 0.10, 0.35), 'SuitTeal', 0.025)
    # Up-arrow indicates onward travel; model front is -Y, passage follows Y.
    box('Exit arrow stem', (0, -0.478, 2.98), (0.055, 0.02, 0.13), 'SignalMint', 0.005)
    verts = [(-0.12, -0.49, 3.015), (0.12, -0.49, 3.015), (0, -0.49, 3.125),
             (-0.12, -0.47, 3.015), (0.12, -0.47, 3.015), (0, -0.47, 3.125)]
    mesh_object('Exit arrow head', verts, [(0, 1, 2), (5, 4, 3), (0, 3, 4, 1),
                                           (1, 4, 5, 2), (2, 5, 3, 0)], 'SignalMint')
    box('Access panel', (1.70, -0.408, 1.44), (0.26, 0.10, 0.29), 'Graphite', 0.022)
    box('Access panel display', (1.70, -0.465, 1.47), (0.16, 0.018, 0.15), 'SignalMint', 0.01)


def validate_and_export(name):
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if not meshes:
        raise RuntimeError('Empty prop ' + name)
    # Recalculate normals on every closed component before FBX export.
    import bmesh
    for obj in meshes:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        if any(not edge.is_manifold for edge in bm.edges):
            raise RuntimeError('Non-manifold component: ' + obj.name)
        bm.to_mesh(obj.data)
        bm.free()
    bpy.ops.object.select_all(action='SELECT')
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    # Preserve separate editable parts in .blend; export meshes only (no scene camera).
    bpy.context.scene['asset_name'] = name
    bpy.context.scene['usage'] = 'Static graybox prop; meters; Blender Z up, front -Y.'
    bpy.ops.wm.save_as_mainfile(filepath=str(source_dir / (name + '.blend')))
    bpy.ops.export_scene.fbx(filepath=str(model_dir / (name + '.fbx')),
        use_selection=True, object_types={'MESH'}, add_leaf_bones=False,
        bake_anim=False, axis_forward='-Z', axis_up='Y',
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True)
    for obj in meshes:
        obj.data.calc_loop_triangles()
    return {'name': name, 'meshCount': len(meshes),
            'triangles': sum(len(o.data.loop_triangles) for o in meshes)}


def render_preview(name):
    scene = bpy.context.scene
    meshes = [o for o in scene.objects if o.type == 'MESH']
    corners = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    lo = Vector(tuple(min(p[i] for p in corners) for i in range(3)))
    hi = Vector(tuple(max(p[i] for p in corners) for i in range(3)))
    center = (lo + hi) / 2
    extent = max(hi - lo)
    bpy.ops.object.camera_add(location=center + Vector((1.5, -3.2, 1.6)) * extent)
    camera = bpy.context.object
    camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = extent * 1.4
    scene.camera = camera
    for pos, energy, size in [((1, -2, 4), 450, 3), ((-3, -1, 2), 250, 3), ((0, 3, 3), 500, 2)]:
        bpy.ops.object.light_add(type='AREA', location=center + Vector(pos) * extent)
        light = bpy.context.object
        light.data.energy = energy * extent * extent
        light.data.shape = 'DISK'
        light.data.size = size * extent
        light.rotation_euler = (center - light.location).to_track_quat('-Z', 'Y').to_euler()
    scene.world.color = (0.16, 0.16, 0.16)
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.render.resolution_x = 800
    scene.render.resolution_y = 800
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.film_transparent = False
    scene.render.filepath = str(args.output / (name + '.png'))
    bpy.ops.render.render(write_still=True)


stats = []
for name, make in [('CapybaraAstronaut', capybara), ('BurntRockBasin', burnt_rock),
                   ('AlcoholFlask', flask), ('GrayboxExitGate', gate)]:
    reset()
    make()
    stats.append(validate_and_export(name))
    if args.render:
        render_preview(name)

(args.output / 'palette.json').write_text(json.dumps({'materials': [
    {'name': name, 'hex': color, 'metallic': metal, 'smoothness': 1 - roughness}
    for name, (color, metal, roughness) in PALETTE.items()]}, indent=2))
(args.output / 'mesh-report.json').write_text(json.dumps(stats, indent=2))
print('GRAYBOX_PROPS_EXPORTED', json.dumps(stats))

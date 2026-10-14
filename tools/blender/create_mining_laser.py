"""Original static low-poly mining-laser model for Signal Lost.
Blender --background --python this_file -- --output <scratch-directory>
Sources stay outside Unity Assets; exported model is Blender Z up / front -Y.
"""
import argparse
import json
import math
from pathlib import Path
import sys
# These modules are supplied by Blender's embedded Python, not the host interpreter.
import bpy  # pyright: ignore[reportMissingImports]
import bmesh  # pyright: ignore[reportMissingImports]
from mathutils import Vector  # pyright: ignore[reportMissingImports]

parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
args.output.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.context.scene.unit_settings.system = 'METRIC'
bpy.context.scene.unit_settings.scale_length = 1
palette = {
    'LaserGraphite': ('29343D', 0.35, 0.32),
    'LaserSteel': ('819CA5', 0.7, 0.27),
    'LaserOrange': ('DF883C', 0.2, 0.45),
    'LaserTeal': ('246C79', 0.3, 0.4),
    'LaserLens': ('80E3D8', 0.15, 0.18),
    'LaserRubber': ('151E24', 0.0, 0.9),
}
materials = {}
for name, (color, metallic, roughness) in palette.items():
    rgb = [int(color[i:i+2], 16) / 255 for i in (0, 2, 4)]
    linear = [v / 12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in rgb]
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*linear, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*linear, 1)
    shader.inputs['Metallic'].default_value = metallic
    shader.inputs['Roughness'].default_value = roughness
    materials[name] = mat


def finish(obj, name, mat):
    obj.name = name
    obj.data.materials.append(materials[mat])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def box(name, pos, size, mat, bevel=0.008):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    obj = bpy.context.object
    obj.scale = size
    finish(obj, name, mat)
    if bevel:
        modifier = obj.modifiers.new('Machined edges', 'BEVEL')
        modifier.width = bevel
        modifier.segments = 2
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj


def barrel(name, pos, radius, length, mat, axis=(0, -1, 0), count=16):
    bpy.ops.mesh.primitive_cylinder_add(vertices=count, radius=radius, depth=length, location=pos)
    obj = finish(bpy.context.object, name, mat)
    obj.rotation_euler = Vector(axis).to_track_quat('Z', 'Y').to_euler()
    return obj


# Compact industrial tool, 0.86m long. Ground-level pivot in its upright orientation.
box('Main laser housing', (0, -0.025, 0.31), (0.24, 0.43, 0.18), 'LaserOrange', 0.026)
box('Top armored rail', (0, -0.015, 0.415), (0.16, 0.36, 0.05), 'LaserGraphite')
box('Rear power pack', (0, 0.23, 0.305), (0.21, 0.15, 0.17), 'LaserGraphite', 0.016)
box('Power cartridge', (0, 0.25, 0.205), (0.15, 0.10, 0.08), 'LaserTeal')
box('Rubber shoulder pad', (0, 0.315, 0.31), (0.205, 0.025, 0.15), 'LaserRubber')
grip = box('Angled hand grip', (0, 0.11, 0.105), (0.095, 0.115, 0.21), 'LaserRubber', 0.014)
grip.rotation_euler.x = -0.20
for z in (0.05, 0.085, 0.12, 0.155):
    box('Grip rib', (0, 0.048, z), (0.10, 0.018, 0.015), 'LaserGraphite', 0.004)
box('Trigger guard bottom', (0, 0.01, 0.125), (0.065, 0.12, 0.025), 'LaserSteel', 0.005)
box('Trigger guard front', (0, -0.045, 0.18), (0.065, 0.025, 0.11), 'LaserSteel', 0.005)
box('Trigger', (0, 0.045, 0.21), (0.025, 0.024, 0.07), 'LaserTeal', 0.004)
barrel('Emitter neck', (0, -0.30, 0.32), 0.070, 0.17, 'LaserSteel')
barrel('Emitter shroud', (0, -0.405, 0.32), 0.102, 0.11, 'LaserGraphite')
barrel('Lens surround', (0, -0.467, 0.32), 0.093, 0.025, 'LaserSteel')
barrel('Mining laser lens', (0, -0.484, 0.32), 0.070, 0.012, 'LaserLens')
for i in range(4):
    a = i * math.pi / 2
    box('Cooling fin', (0.09 * math.cos(a), -0.305, 0.32 + 0.09 * math.sin(a)),
        (0.035 if i % 2 == 0 else 0.13, 0.10, 0.13 if i % 2 == 0 else 0.035), 'LaserTeal', 0.004)
for side in (-1, 1):
    box('Side inspection panel', (side * 0.126, 0.01, 0.31), (0.025, 0.26, 0.11), 'LaserGraphite')
    for y in (-0.075, -0.02, 0.035, 0.09):
        box('Side vent', (side * 0.143, y, 0.31), (0.012, 0.022, 0.075), 'LaserSteel', 0.003)
    barrel('Housing fastener', (side * 0.145, -0.13, 0.365), 0.017, 0.012, 'LaserSteel', (1, 0, 0), 8)
box('Charge status housing', (0, 0.07, 0.449), (0.085, 0.13, 0.02), 'LaserTeal', 0.004)
for y in (0.025, 0.07, 0.115):
    box('Charge indicator', (0, y, 0.463), (0.062, 0.022, 0.009), 'LaserLens', 0.003)

meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
lowest = min((o.matrix_world @ Vector(c)).z for o in meshes for c in o.bound_box)
for obj in meshes:
    obj.location.z -= lowest
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    if any(not e.is_manifold for e in bm.edges):
        raise RuntimeError('Non-manifold component ' + obj.name)
    bm.to_mesh(obj.data)
    bm.free()
bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
bpy.ops.wm.save_as_mainfile(filepath=str(args.output / 'MiningLaser.blend'))
bpy.ops.export_scene.fbx(filepath=str(args.output / 'MiningLaser.fbx'), use_selection=True,
    object_types={'MESH'}, axis_forward='-Z', axis_up='Y', bake_space_transform=True,
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', bake_anim=False, add_leaf_bones=False)
(args.output / 'MiningLaserPalette.json').write_text(json.dumps({'materials': [
    {'name': n, 'hex': c, 'metallic': m, 'smoothness': 1-r} for n, (c, m, r) in palette.items()]}, indent=2))
points = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
center = Vector(tuple((max(p[i] for p in points)+min(p[i] for p in points))/2 for i in range(3)))
bpy.ops.object.camera_add(location=center + Vector((1.2, -1.5, 1.0)))
camera = bpy.context.object
camera.rotation_euler = (center-camera.location).to_track_quat('-Z', 'Y').to_euler()
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 1.25
scene = bpy.context.scene
scene.camera = camera
for pos, power in [((1,-2,3), 300), ((-2,0,2), 250), ((0,2,1), 200)]:
    bpy.ops.object.light_add(type='AREA', location=pos)
    light = bpy.context.object
    light.data.energy = power
    light.data.size = 2
    light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
scene.world.color=(.15,.15,.15)
scene.render.engine = 'CYCLES'
scene.cycles.samples = 24
scene.render.resolution_x = 900
scene.render.resolution_y = 650
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = str(args.output / 'MiningLaser.png')
bpy.ops.render.render(write_still=True)
print('MINING_LASER_EXPORTED', len(meshes), 'closed components')

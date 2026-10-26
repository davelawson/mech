"""Prepare the articulated runtime Awesome, without modifying its source GLB.

Run with Blender 4.2.3:
  blender --background --factory-startup --python scripts/prepare-awesome.py

All region coordinates below use the source's height-normalized, game-facing
frame (+Y up, -Z forward). Blender stores that frame as (x, -z, y).
"""
from collections import defaultdict
import hashlib
import json
import struct
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "generated/Awesome.glb"
OUTPUT = ROOT / "Assets/Models/Awesome.glb"
HEIGHT = 2.8
ARMOR_TRIANGLES = 88000


def game(point):
    return Vector((point.x, point.z, -point.y))


def blender(point):
    return Vector((point[0], -point[2], point[1]))


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
source = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
bpy.context.view_layer.objects.active = source
source.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
original_triangles = len(source.data.polygons)
points = [game(v.co) for v in source.data.vertices]
bottom = min(p.y for p in points)
height = max(p.y for p in points) - bottom
# The authored front is +Z; rotate 180 degrees so the cannon stays on +X.
for vertex, point in zip(source.data.vertices, points):
    vertex.co = blender((-point.x / height, (point.y - bottom) / height, -point.z / height))

# Weld geometry while retaining per-corner UV seams; collapse interpolates UVs.
bm = bmesh.new()
bm.from_mesh(source.data)
bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.000001)
bm.to_mesh(source.data)
bm.free()

paint = source.data.materials[0]
paint.name = "AwesomeSourcePaint"
interior = bpy.data.materials.new("AwesomeJointInterior")
interior.use_nodes = True
shader = interior.node_tree.nodes.get("Principled BSDF")
shader.inputs["Base Color"].default_value = (0.045, 0.055, 0.05, 1)
shader.inputs["Metallic"].default_value = 0.35
shader.inputs["Roughness"].default_value = 0.85
source.data.materials.append(interior)

# Bisect first, then classify: faces cannot straddle a joint or damage region.
# The diagonal lower-arm seam follows the taper below the broad shoulder.
planes = [(Vector((0, 1, 0)), h) for h in (0.105, 0.30, 0.36, 0.54, 0.75)]
planes += [(Vector((1, 0, 0)), x) for x in (0, -0.075, 0.075, -0.09, 0.09, -0.16, 0.16)]
planes += [(Vector((sign, 0.18, 0)), 0.365) for sign in (-1, 1)]
bm = bmesh.new()
bm.from_mesh(source.data)
for normal, offset in planes:
    bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                          plane_co=blender(normal * (offset / normal.length_squared)),
                          plane_no=blender(normal), dist=0.0000001)
bmesh.ops.triangulate(bm, faces=list(bm.faces))
bm.faces.ensure_lookup_table()
bm.faces.index_update()


def region(point):
    x, h, z = game(point)
    side = "Right" if x > 0 else "Left"
    arm = (h >= 0.75 and abs(x) > 0.16) or (0.36 <= h < 0.75 and abs(x) + 0.18 * h > 0.365)
    if arm:
        return side + "Arm", ("PpcEmitter" if side == "Right" and x > 0.31 and 0.55 < h < 0.635 and z < -0.35
                               else "Fist" if side == "Left" and h < 0.48 else "ArmShell")
    if h < 0.36 or (h < 0.54 and abs(x) > 0.075):
        return side + "Leg", "Foot" if h < 0.105 else "Shin" if h < 0.30 else "Thigh"
    if h >= 0.75 and abs(x) < 0.09:
        return "Head", "CockpitAndDish"
    if h >= 0.54 and abs(x) > 0.09:
        return side + "Torso", ("PpcEmitter" if (abs(x) - 0.143) ** 2 + (h - 0.697) ** 2 < 0.038 ** 2 and z < -0.043 else "TorsoShell")
    return "CenterTorso", "CoreAndPelvis"


groups = defaultdict(list)
for face in bm.faces:
    groups[region(face.calc_center_median())].append(face.index)
# A face integer layer survives copies without relying on changed face indices.
part_layer = bm.faces.layers.int.new("part")
keys = sorted(groups)
for part_id, key in enumerate(keys):
    for index in groups[key]:
        bm.faces[index][part_layer] = part_id

roots = {}
for location in ("Head", "CenterTorso", "LeftTorso", "RightTorso", "LeftArm", "RightArm", "LeftLeg", "RightLeg"):
    root = bpy.data.objects.new(location, None)
    bpy.context.collection.objects.link(root)
    roots[location] = root

counts = {}
for part_id, (location, name) in enumerate(keys):
    part = bm.copy()
    layer = part.faces.layers.int["part"]
    bmesh.ops.delete(part, geom=[f for f in part.faces if f[layer] != part_id], context="FACES")
    boundary = [edge for edge in part.edges if edge.is_boundary and any(
        all(abs(normal.dot(game(v.co)) - offset) < 0.000002 for v in edge.verts)
        for normal, offset in planes)]
    if boundary:
        caps = bmesh.ops.holes_fill(part, edges=boundary, sides=0)["faces"]
        for face in caps:
            face.material_index = 1
            face.smooth = False
    bmesh.ops.triangulate(part, faces=list(part.faces))
    bmesh.ops.recalc_face_normals(part, faces=list(part.faces))
    for vertex in part.verts:
        vertex.co *= HEIGHT
    # The generated feet are very slightly uneven. Ground each sole independently.
    if name == "Foot":
        sole = min(v.co.z for v in part.verts)
        for vertex in part.verts:
            vertex.co.z -= sole
    mesh = bpy.data.meshes.new(location + name)
    part.to_mesh(mesh)
    part.free()
    mesh.materials.clear()
    mesh.materials.append(paint)
    mesh.materials.append(interior)
    center = sum((v.co for v in mesh.vertices), Vector()) / len(mesh.vertices)
    for vertex in mesh.vertices:
        vertex.co -= center
    obj = bpy.data.objects.new(location + "_" + name, mesh)
    obj.location = center
    bpy.context.collection.objects.link(obj)
    obj.parent = roots[location]
    counts[location + "/" + name] = len(mesh.polygons)
bm.free()
bpy.data.objects.remove(source, do_unlink=True)

# Simplify after cutting so redundant cut/cap tessellation consumes no budget.
# The modifier retains per-corner texture coordinates and each rigid part.
ratio = min(1.0, ARMOR_TRIANGLES / sum(counts.values()))
for location, root in roots.items():
    for obj in root.children:
        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new("Runtime silhouette simplification", "DECIMATE")
        modifier.ratio = ratio
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        counts[location + "/" + obj.name.removeprefix(location + "_")] = len(obj.data.polygons)

# Dark joints overlap the cut interiors without changing the outer armor shape.
# Coordinates match the movement profile, expressed in normalized source height.
for sign in (-1, 1):
    side = "Left" if sign < 0 else "Right"
    for name, location, center, radii in (
        ("Hip", side + "Leg", (sign * 0.17, 0.47, 0.02), (0.067, 0.065, 0.070)),
        ("KneeInterior", side + "Leg", (sign * 0.205, 0.30, 0.005), (0.066, 0.058, 0.068)),
        ("Ankle", side + "Leg", (sign * 0.20, 0.105, 0.05), (0.065, 0.045, 0.060)),
        ("ShoulderInterior", side + "Arm", (sign * 0.23, 0.78, 0.07), (0.07, 0.07, 0.07)),
    ):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8,
            location=blender(Vector(center) * HEIGHT))
        joint = bpy.context.object
        joint.name = location + "_" + name
        joint.scale = blender((radii[0] * HEIGHT, radii[1] * HEIGHT, radii[2] * HEIGHT))
        # blender() negates the game Z component; sphere scale must stay positive.
        joint.scale = Vector((abs(joint.scale.x), abs(joint.scale.y), abs(joint.scale.z)))
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        joint.data.materials.append(interior)
        for face in joint.data.polygons:
            face.use_smooth = True
        joint.parent = roots[location]
        counts[location + "/" + name] = sum(len(p.vertices) - 2 for p in joint.data.polygons)

triangles = sum(counts.values())
assert 60000 <= triangles <= 100000, triangles
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.export_scene.gltf(filepath=str(OUTPUT), export_format="GLB", export_yup=True,
                          export_animations=False, export_cameras=False, export_lights=False)
# glTF export discards degenerate faces. Record actual exported indices, matching
# the count Godot renders rather than Blender's pre-export polygon count.
encoded = OUTPUT.read_bytes()
json_length = struct.unpack_from("<I", encoded, 12)[0]
gltf = json.loads(encoded[20:20 + json_length])
counts = {}
for node in gltf["nodes"]:
    if "mesh" in node:
        location, name = node["name"].split("_", 1)
        counts[location + "/" + name] = sum(
            gltf["accessors"][primitive["indices"]]["count"] // 3
            for primitive in gltf["meshes"][node["mesh"]]["primitives"])
triangles = sum(counts.values())
assert 60000 <= triangles <= 100000, triangles
manifest = {
    "source": "generated/Awesome.glb",
    "source_sha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
    "tool": "Blender " + bpy.app.version_string,
    "height": HEIGHT,
    "source_triangles": original_triangles,
    "runtime_triangles": triangles,
    "parts": counts,
}
OUTPUT.with_suffix(".json").write_text(json.dumps(manifest, indent=2) + "\n")
print(json.dumps(manifest, indent=2))

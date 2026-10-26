"""Assemble MW5's stock CPLT-C1 as rigid, textured damage groups.

Run: python3 scripts/prepare-catapult.py (requires Pillow).
Inputs are untouched UE Viewer exports in generated/mw5-catapult.
No Blender or Unreal installation is required to rebuild the runtime GLB.
"""
from collections import defaultdict
import hashlib
import json
import math
from pathlib import Path
import sys

sys.dont_write_bytecode = True
from mech_asset_tools import Asset as GltfAsset, Glb as GltfDocument, paint as build_paint, rotate

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "generated/mw5-catapult"
OUTPUT = ROOT / "Assets/Models/Catapult.glb"
HEIGHT = 3.16 / 1.225  # Factory applies the unchanged 65-ton scale.
LOCATIONS = ("Head", "CenterTorso", "LeftTorso", "RightTorso", "LeftArm", "RightArm", "LeftLeg", "RightLeg")


class Asset(GltfAsset):
    def __init__(self, relative):
        super().__init__(SOURCE / relative)


class Glb(GltfDocument):
    def __init__(self):
        super().__init__("Mech Catapult preparation")

    def save(self):
        super().save(OUTPUT)


def paint(glb, section):
    return build_paint(glb, SOURCE, "Catapult", section)


def main():
    body = Asset("Body/Catapult_SKM.gltf")
    attachment_data = json.loads((SOURCE / "attachments.json").read_text())
    glb = Glb()
    armor, weapons = paint(glb, "Body"), paint(glb, "Variant")
    for name, color, metallic, rough in [("CatapultAmberCanopy", (0.93, .58, .12, 1), .35, .25),
                                         ("CatapultCockpitFrame", (.18, .22, .17, 1), .35, .65)]:
        glb.g["materials"].append({"name": name, "pbrMetallicRoughness": {"baseColorFactor": list(color), "metallicFactor": metallic, "roughnessFactor": rough}})
    parts = defaultdict(list)
    markers = []

    def add(location, name, asset, primitive, attributes, indices, material, position=lambda p: p, direction=lambda p: p):
        parts[(location, name)].append((attributes, indices, material, position, direction))

    for primitive, attributes, indices in body.primitives():
        grouped = defaultdict(list)
        joints, weights = attributes["JOINTS_0"], attributes["WEIGHTS_0"]
        for start in range(0, len(indices), 3):
            triangle = indices[start:start + 3]
            owners = {joints[i][0] for i in triangle}
            assert len(owners) == 1 and all(abs(weights[i][0] - 1) < 1e-5 for i in triangle), "Expected rigid source faces"
            bone = body.g["nodes"][body.g["skins"][0]["joints"][owners.pop()]]["name"]
            side = "Left" if "Left" in bone else "Right"
            if bone.startswith(("Thigh", "Hip", "Calf", "Foot", "Toe", "Talon")):
                owner = side + "Leg"
                name = "Thigh" if bone.startswith("Thigh") else "Hip" if bone.startswith("Hip") else "Shin" if bone.startswith("Calf") else "Foot"
            elif bone.startswith("Upperarm"):
                owner, name = side + "Arm", "LauncherSupport"
            elif bone.startswith("Torso_Left") or bone.startswith("Torso_Right"):
                owner, name = side + "Torso", "TorsoShell"
            else:
                owner, name = "CenterTorso", "CoreAndPelvis"
            grouped[(owner, name)].extend(triangle)
        for (owner, name), triangles in grouped.items():
            add(owner, name, body, primitive, attributes, triangles, armor)

    canopy = Asset("Body/CPLT_TP_Cockpit_DM0_STM.gltf")
    for primitive, attributes, indices in canopy.primitives():
        # Frame/glass section colors are authored here and verified in the rendered preview.
        material = 3 if primitive["material"] == attachment_data["cockpit_frame_section"] else 2
        add("Head", "CockpitFrame" if material == 3 else "AmberCanopy", canopy, primitive, attributes, indices, material,
            lambda p: body.world("Cockpit", p), lambda p: body.world("Cockpit", p, True))

    for slot in attachment_data["weapons"]:
        asset = Asset("Weapons/" + slot["mesh"] + ".gltf")
        bone, q, t = slot["bone"], slot["rotation"], slot["translation"]
        def place(p, bone=bone, q=q, t=t):
            return body.world(bone, tuple(a + b for a, b in zip(rotate(q, p), t)))
        def direct(p, bone=bone, q=q):
            return body.world(bone, rotate(q, p), True)
        location = slot["location"]
        for primitive, attributes, indices in asset.primitives():
            add(location, slot["name"], asset, primitive, attributes, indices, weapons, place, direct)
        if slot["name"].startswith("Lrm15"):
            fires = [name for name in asset.names if name and name.startswith("Fire")]
            assert len(fires) == 15
            for i, fire in enumerate(fires):
                markers.append((location, f"MissileTube{i:02}", place(asset.world(fire))))

    all_points = [position(a["POSITION"][i]) for entries in parts.values()
                  for a, indices, _, position, _ in entries for i in set(indices)]
    bottom = min(p[1] for p in all_points)
    scale = HEIGHT / (max(p[1] for p in all_points) - bottom)
    def game(p):
        return (p[2] * scale, (p[1] - bottom) * scale, -p[0] * scale)
    def normal(p):
        length = math.sqrt(sum(v * v for v in p))
        return (p[2] / length, p[1] / length, -p[0] / length)
    roots = {name: glb.node(name) for name in LOCATIONS}
    counts = {}
    for (location, name), entries in sorted(parts.items()):
        primitives = []
        points = [game(position(a["POSITION"][i])) for a, indices, _, position, _ in entries for i in set(indices)]
        center = tuple((min(p[k] for p in points) + max(p[k] for p in points)) / 2 for k in range(3))
        sole = min(p[1] for p in points) if name == "Foot" else 0
        for attributes, indices, material, position, direction in entries:
            used = sorted(set(indices))
            remap = {old: new for new, old in enumerate(used)}
            vertices = [tuple(v - c - (sole if k == 1 else 0) for k, (v, c) in enumerate(zip(game(position(attributes["POSITION"][i])), center))) for i in used]
            normals = [normal(direction(attributes["NORMAL"][i])) for i in used]
            uv = [attributes["TEXCOORD_0"][i] for i in used]
            prim = {"attributes": {"POSITION": glb.accessor(vertices, "VEC3"), "NORMAL": glb.accessor(normals, "VEC3"), "TEXCOORD_0": glb.accessor(uv, "VEC2")},
                    "indices": glb.accessor([(remap[i],) for i in indices], "SCALAR", True), "material": material}
            primitives.append(prim)
        mesh = len(glb.g["meshes"])
        glb.g["meshes"].append({"name": location + "_" + name, "primitives": primitives})
        glb.node(location + "_" + name, roots[location], mesh=mesh, translation=list(center))
        counts[location + "/" + name] = sum(len(e[1]) // 3 for e in entries)
    for location, name, point in markers:
        glb.node(location + "_" + name, roots[location], translation=list(game(point)))
    glb.save()
    def pivot(name):
        return [round(v, 7) for v in game(body.world(name))]
    manifest = {"source": str(SOURCE.relative_to(ROOT)), "source_files": {
        str(p.relative_to(SOURCE)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(SOURCE.rglob("*")) if p.is_file()},
        "runtime_triangles": sum(counts.values()), "render_meshes": len(parts), "parts": counts,
        "height": HEIGHT, "scale": scale, "bottom": bottom,
        "rig": {"waist": pivot("Torso_Twist")[1], "hip": pivot("Thigh_Right"), "knee": pivot("Calf_Right"), "ankle": pivot("Foot0_Right"), "shoulder": pivot("Upperarm_Right")},
        "weapons": attachment_data["weapons"], "missile_tubes": len(markers),
        "materials": "Olive palette baked from MW5 default/wear masks; source UVs and normal detail preserved. Amber exterior canopy.",
        "tool": "scripts/prepare-catapult.py; Python + Pillow"}
    OUTPUT.with_suffix(".json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(json.dumps({k: v for k, v in manifest.items() if k != "source_files"}, indent=2))


if __name__ == "__main__":
    main()

"""Shared rigid MW5 preparation for external body, exterior cockpit and stock weapons."""
import argparse
from collections import defaultdict
import hashlib
import json
import math
from pathlib import Path

from mech_asset_tools import Asset, Glb, paint

ROOT = Path(__file__).resolve().parents[1]
LOCATIONS = ("Head", "CenterTorso", "LeftTorso", "RightTorso", "LeftArm", "RightArm", "LeftLeg", "RightLeg")


def region(bone):
    side = "Left" if "Left" in bone else "Right"
    if any(part in bone for part in ("Thigh", "Hip", "Calf", "Foot", "Toe", "Talon")):
        part = "Thigh" if "Thigh" in bone else "Hip" if "Hip" in bone else "Shin" if "Calf" in bone else "Foot"
        return side + "Leg", part
    if bone.startswith(("Clavicle", "Upperarm", "Forearm", "Elbow", "Hand")):
        return side + "Arm", "ForearmAndFist" if bone.startswith(("Forearm", "Hand")) else "ShoulderAndUpperarm"
    if bone.startswith(("Torso_Left", "Torso_Right")):
        return side + "Torso", "TorsoShell"
    if bone.startswith(("Torso_Center", "Torso_Twist", "Pelvis", "Flap_")):
        return "CenterTorso", "CoreAndPelvis"
    raise ValueError("Unassigned source bone: " + bone)


def prepare(chassis, prefix, displayed_height, model_scale, weapons, script_name, missile_tubes, evidence):
    """Validate rigid weights, assemble source attachments, bake paint and record provenance."""
    default_source = Path.home() / f"Models/MW5Mercenaries/Game/Objects/Mechs/{chassis}/Model"
    output = ROOT / f"Assets/Models/{chassis}.glb"
    height = displayed_height / model_scale
    parser = argparse.ArgumentParser(description=f"Prepare MW5 {chassis} from external UE Viewer exports (Python 3 + Pillow).")
    parser.add_argument("--source", type=Path, default=default_source)
    args = parser.parse_args()
    source = args.source.expanduser().resolve()
    if source.name == "Body":
        source = source.parent
    inputs = set()

    def load(relative):
        asset = Asset(source / relative)
        inputs.add(asset.path)
        inputs.update(asset.path.parent / b["uri"] for b in asset.g["buffers"])
        return asset

    body = load(f"Body/{chassis}_SKM.gltf")
    glb = Glb(f"Mech {chassis} preparation")
    armor = paint(glb, source, chassis, "Body")
    weapon_paint = paint(glb, source, chassis, "Variant")
    for section in ("Body", "Variant"):
        for suffix in ("Default_MSK", "Wear_MSK", "NRM"):
            inputs.add(source / f"Body/Materials/Textures/{chassis}_{section}_{suffix}.png")
    glb.g["materials"].extend([
        {"name": chassis + "AmberCanopy", "pbrMetallicRoughness": {"baseColorFactor": [.93, .58, .12, 1], "metallicFactor": .35, "roughnessFactor": .25}},
        {"name": chassis + "CockpitFrame", "pbrMetallicRoughness": {"baseColorFactor": [.18, .22, .17, 1], "metallicFactor": .35, "roughnessFactor": .65}},
    ])
    parts = defaultdict(list)
    markers = []
    for _, attributes, indices in body.primitives():
        grouped = defaultdict(list)
        joints, weights = attributes["JOINTS_0"], attributes["WEIGHTS_0"]
        for start in range(0, len(indices), 3):
            triangle = indices[start:start + 3]
            owners = {joints[i][0] for i in triangle}
            assert len(owners) == 1 and all(abs(weights[i][0] - 1) < 1e-5 for i in triangle), "Expected rigid source faces"
            bone = body.g["nodes"][body.g["skins"][0]["joints"][owners.pop()]]["name"]
            grouped[region(bone)].extend(triangle)
        for key, triangles in grouped.items():
            parts[key].append((attributes, triangles, armor, lambda p: p, lambda p: p))

    canopy = load(f"Body/{prefix}_TP_Cockpit_DM0_STM.gltf")
    for primitive, attributes, indices in canopy.primitives():
        # The small first section is the exterior glazing; verify in rendered captures.
        glass = primitive["material"] == 0
        key = ("Head", "AmberCanopy" if glass else "CockpitFrame")
        parts[key].append((attributes, indices, 2 if glass else 3,
                           lambda p: body.world("Cockpit", p), lambda p: body.world("Cockpit", p, True)))

    for location, name, bone, mesh in weapons:
        asset = load("Weapons/" + mesh + ".gltf")
        def position(p, bone=bone):
            return body.world(bone, p)
        def direction(p, bone=bone):
            return body.world(bone, p, True)
        for _, attributes, indices in asset.primitives():
            parts[(location, name)].append((attributes, indices, weapon_paint, position, direction))
        fires = sorted(name for name in asset.names if name.startswith("Fire"))
        if name.startswith("Srm"):
            assert len(fires) == missile_tubes
            markers.extend((location, f"MissileTube{i:02}", position(asset.world(fire))) for i, fire in enumerate(fires))
        elif name == "CannonHousing":
            assert fires == ["Fire00"]
            markers.append((location, "CannonMuzzle", position(asset.world("Fire00"))))
        elif chassis != "Wolverine":
            assert fires == ["Fire00"]
            markers.append((location, "WeaponMuzzle_" + name, position(asset.world("Fire00"))))

    all_points = [position(a["POSITION"][i]) for entries in parts.values()
                  for a, indices, _, position, _ in entries for i in set(indices)]
    bottom = min(p[1] for p in all_points)
    scale = height / (max(p[1] for p in all_points) - bottom)

    def game(p):
        return (p[2] * scale, (p[1] - bottom) * scale, -p[0] * scale)

    def normal(p):
        length = math.sqrt(sum(v * v for v in p))
        return (p[2] / length, p[1] / length, -p[0] / length)

    roots = {name: glb.node(name) for name in LOCATIONS}
    counts = {}
    for (location, name), entries in sorted(parts.items()):
        points = [game(position(a["POSITION"][i])) for a, indices, _, position, _ in entries for i in set(indices)]
        center = tuple((min(p[k] for p in points) + max(p[k] for p in points)) / 2 for k in range(3))
        sole = min(p[1] for p in points) if name == "Foot" else 0
        primitives = []
        for attributes, indices, material, position, direction in entries:
            used = sorted(set(indices))
            remap = {old: new for new, old in enumerate(used)}
            vertices = [tuple(v - c - (sole if k == 1 else 0)
                              for k, (v, c) in enumerate(zip(game(position(attributes["POSITION"][i])), center))) for i in used]
            normals = [normal(direction(attributes["NORMAL"][i])) for i in used]
            uv = [attributes["TEXCOORD_0"][i] for i in used]
            primitives.append({"attributes": {"POSITION": glb.accessor(vertices, "VEC3"),
                                                "NORMAL": glb.accessor(normals, "VEC3"), "TEXCOORD_0": glb.accessor(uv, "VEC2")},
                               "indices": glb.accessor([(remap[i],) for i in indices], "SCALAR", True), "material": material})
        mesh_index = len(glb.g["meshes"])
        glb.g["meshes"].append({"name": location + "_" + name, "primitives": primitives})
        glb.node(location + "_" + name, roots[location], mesh=mesh_index, translation=list(center))
        counts[location + "/" + name] = sum(len(e[1]) // 3 for e in entries)
    for location, name, point in markers:
        glb.node(location + "_" + name, roots[location], translation=list(game(point)))
    glb.save(output)

    def pivot(name):
        return [round(v, 7) for v in game(body.world(name))]

    manifest = {
        "source": str(source), "source_files": {str(p.relative_to(source)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(inputs)},
        "runtime_triangles": sum(counts.values()), "render_meshes": len(parts), "parts": counts,
        "height": height, "displayed_height": displayed_height, "scale": scale, "bottom": bottom,
        "rig": {"waist": pivot("Torso_Twist")[1], "hip": pivot("Thigh_Right"), "knee": pivot("Calf_Right"),
                "ankle": pivot("Foot0_Right"), "shoulder": pivot("Upperarm_Right")},
        "weapons": [{"location": owner, "name": name, "bone": bone, "mesh": mesh, "local_transform": "identity"}
                    for owner, name, bone, mesh in weapons],
        "cockpit": {"mesh": f"{prefix}_TP_Cockpit_DM0_STM", "bone": "Cockpit", "glass_section": 0},
        "missile_tubes": missile_tubes,
        "attachment_evidence": evidence,
        "materials": "Olive palette baked from default/wear masks; source UVs and normal detail retained; amber exterior canopy.",
        "tool": f"scripts/{script_name} + scripts/mech_asset_tools.py; Python + Pillow",
    }
    output.with_suffix(".json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(json.dumps({k: v for k, v in manifest.items() if k != "source_files"}, indent=2))


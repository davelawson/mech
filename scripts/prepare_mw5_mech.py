"""Shared rigid MW5 preparation for external body, exterior cockpit and stock weapons."""
from collections import Counter, defaultdict
import hashlib
import json
import math
from pathlib import Path
import re

from mech_asset_tools import Asset, Glb, paint

ROOT = Path(__file__).resolve().parents[1]
LOCATIONS = ("Head", "CenterTorso", "LeftTorso", "RightTorso", "LeftArm", "RightArm", "LeftLeg", "RightLeg")


def region(bone):
    if bone.startswith("Cockpit"):
        return "Head", "CockpitBody"
    side = "Left" if "Left" in bone else "Right"
    if any(part in bone for part in ("Thigh", "Hip", "Calf", "Knee", "Foot", "Toe", "Talon")):
        part = "Thigh" if "Thigh" in bone else "Hip" if "Hip" in bone else "Shin" if "Calf" in bone or "Knee" in bone else "Foot"
        return side + "Leg", part
    if bone.startswith(("Clavicle", "Upperarm", "Forearm", "Elbow", "Hand")):
        return side + "Arm", "ForearmAndFist" if bone.startswith(("Forearm", "Hand")) else "ShoulderAndUpperarm"
    if bone.startswith(("Torso_Left", "Torso_Right")):
        return side + "Torso", "TorsoShell"
    if bone.startswith(("Torso_Center", "Torso_Twist", "Torso_Pitch", "Pelvis", "Flap_")):
        return "CenterTorso", "CoreAndPelvis"
    raise ValueError("Unassigned source bone: " + bone)


CLASSES = {"B": "Ballistic", "E": "Energy", "M": "Missile", "A": "Ams"}
# Library visuals that game weapon kinds can resolve to (WeaponMounts.cs); other MW5 tech stays out of the build.
GAME_VISUALS = re.compile(r"Laser|PPC|Flamer|MG|AC(2|5|10|20)|Missile\d+|Blank")


def weapon_file(prefix, mesh):
    """Split `Weapon_Mech_<PREFIX>_<Bone>_<Class><H|B><n>_<Visual>_SKM` into hardpoint parts; None for other files."""
    match = re.fullmatch(rf"Weapon_Mech_{prefix}_(?P<bone>.+)_(?P<cls>[A-Z])(?P<slot>[HB])(?P<n>\d+)_(?P<visual>[A-Za-z0-9]+)_SKM", mesh)
    return match and match.groupdict()


def hardpoint_location(bone):
    if bone in ("Torso_Left", "Torso_Right", "Torso_Center"):
        return bone.removeprefix("Torso_") + "Torso"
    if bone == "Head":
        return "Head"
    if bone.startswith(("Forearm_", "Upperarm_", "Clavicle_", "Arm_")):
        return ("Left" if bone.endswith("Left") else "Right") + "Arm"
    raise ValueError("Unassigned hardpoint bone: " + bone)


def hardpoint_bone(bone):
    # Torso and head sockets ride the torso pitch; Shadow Hawk's `Arm_Right` hardpoints use the forearm.
    if bone.startswith(("Torso_", "Head")):
        return "Torso_Pitch"
    return "Forearm_" + bone.removeprefix("Arm_") if bone.startswith("Arm_") else bone


def default_source(chassis, source_tree="Game"):
    return Path.home() / f"Models/MW5Mercenaries/{source_tree}/Objects/Mechs/{chassis}/Model"


def prepare(chassis, prefix, displayed_height, model_scale, weapons, missile_tubes, evidence, source, glass_section=0,
            hardpoint_overrides=None):
    """Validate rigid weights, assemble source attachments, bake paint and record provenance.
    Also writes `<Chassis>Weapons.glb`: every exported weapon visual for every hardpoint, in the same model space."""
    output = ROOT / f"Assets/Models/{chassis}.glb"
    library_output = ROOT / f"Assets/Models/{chassis}Weapons.glb"
    height = displayed_height / model_scale
    source = Path(source).expanduser().resolve()
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
    seam_faces = 0
    weighted_faces = 0
    for _, attributes, indices in body.primitives():
        grouped = defaultdict(list)
        joints, weights = attributes["JOINTS_0"], attributes["WEIGHTS_0"]
        for start in range(0, len(indices), 3):
            triangle = indices[start:start + 3]
            bones = [body.g["nodes"][body.g["skins"][0]["joints"][joints[i][0]]]["name"] for i in triangle]
            regions = [region(bone) for bone in bones]
            # Preserve each complete face; the predominant source region owns seam faces.
            if len(set(regions)) > 1:
                seam_faces += 1
            if any(abs(weights[i][0] - 1) >= 1e-5 for i in triangle):
                weighted_faces += 1
            grouped[Counter(regions).most_common(1)[0][0]].extend(triangle)
        for key, triangles in grouped.items():
            parts[key].append((attributes, triangles, armor, lambda p: p, lambda p: p))

    canopy = load(f"Body/{prefix}_TP_Cockpit_DM0_STM.gltf")
    # Some exports omit the unweighted Cockpit joint; Torso_Head has the same source transform.
    cockpit_bone = "Cockpit" if "Cockpit" in body.names else "Torso_Head"
    for primitive, attributes, indices in canopy.primitives():
        # Cockpit material order varies by chassis; verify glazing in rendered captures.
        glass = primitive["material"] == glass_section
        key = ("Head", "AmberCanopy" if glass else "CockpitFrame")
        parts[key].append((attributes, indices, 2 if glass else 3,
                           lambda p: body.world(cockpit_bone, p), lambda p: body.world(cockpit_bone, p, True)))

    def weapon(mesh, bone):
        """A weapon mesh's primitives and sorted firing positions, attached at its socket bone with identity locals."""
        asset = load("Weapons/" + mesh + ".gltf")
        def position(p, bone=bone):
            return body.world(bone, p)
        def direction(p, bone=bone):
            return body.world(bone, p, True)
        entries = [(attributes, indices, weapon_paint, position, direction) for _, attributes, indices in asset.primitives()]
        fires = [position(asset.world(fire)) for fire in sorted(name for name in asset.names if name.startswith("Fire"))]
        return entries, fires

    part_markers = {}
    for location, name, bone, mesh in weapons:
        entries, fires = weapon(mesh, bone)
        parts[(location, name)].extend(entries)
        first_marker = len(markers)
        if name.startswith("Weapon"):
            # Batch imports preserve every source firing position with weapon-specific names.
            markers.extend((location, f"WeaponMuzzle_{name}_{i:02}", point) for i, point in enumerate(fires))
        elif name.startswith("Lrm"):
            assert len(fires) == int(name.removeprefix("Lrm").removesuffix("Housing"))
            markers.extend((location, f"LrmTube{i:02}", point) for i, point in enumerate(fires))
        elif name.startswith("Srm"):
            assert len(fires) == missile_tubes
            markers.extend((location, f"MissileTube{i:02}", point) for i, point in enumerate(fires))
        elif name == "CannonHousing":
            assert len(fires) == 1
            markers.append((location, "CannonMuzzle", fires[0]))
        elif chassis != "Wolverine":
            assert len(fires) == 1
            markers.append((location, "WeaponMuzzle_" + name, fires[0]))
        part_markers.setdefault((location, name), []).extend(marker for _, marker, _ in markers[first_marker:])

    # Every exported visual, grouped by hardpoint. `<X>B<n>` blank covers close the matching `<X>H<n>` hardpoint.
    overrides = hardpoint_overrides or {}
    stock = {}
    for location, name, bone, mesh in weapons:
        parsed = weapon_file(prefix, mesh)
        hardpoint = f"{parsed['bone']}_{parsed['cls']}H{parsed['n']}"
        stock[hardpoint] = {"location": location, "bone": bone, "visual": parsed["visual"], "part": name,
                            "markers": part_markers[(location, name)]}
    hardpoints = {}
    for file in sorted((source / "Weapons").glob(f"Weapon_Mech_{prefix}_*_SKM.gltf")):
        parsed = weapon_file(prefix, file.stem)
        # `Hero_` hardpoints belong to MW5 hero-variant bodies, not the prepared stock body.
        if parsed is None or not GAME_VISUALS.fullmatch(parsed["visual"]) or parsed["bone"].startswith("Hero"):
            continue
        hardpoint = f"{parsed['bone']}_{parsed['cls']}H{parsed['n']}"
        hardpoints.setdefault(hardpoint, {"bone": parsed["bone"], "cls": parsed["cls"], "files": {}})
        hardpoints[hardpoint]["files"]["Blank" if parsed["slot"] == "B" else parsed["visual"]] = file.stem
    # Covers whose hardpoint has no weapon visuals (generic GB covers, unmatched slots) are dropped.
    hardpoints = {k: v for k, v in hardpoints.items() if v["cls"] in CLASSES and set(v["files"]) - {"Blank"}}

    all_points = [position(a["POSITION"][i]) for entries in parts.values()
                  for a, indices, _, position, _ in entries for i in set(indices)]
    bottom = min(p[1] for p in all_points)
    scale = height / (max(p[1] for p in all_points) - bottom)

    def game(p):
        return (p[2] * scale, (p[1] - bottom) * scale, -p[0] * scale)

    def normal(p):
        length = math.sqrt(sum(v * v for v in p))
        if length == 0:  # a few degenerate source vertices (e.g. Locust's LRM 20 racks)
            return (0, 1, 0)
        return (p[2] / length, p[1] / length, -p[0] / length)

    def emit(target, parent, name, mesh_name, entries, sole_name=None):
        points = [game(position(a["POSITION"][i])) for a, indices, _, position, _ in entries for i in set(indices)]
        center = tuple((min(p[k] for p in points) + max(p[k] for p in points)) / 2 for k in range(3))
        sole = min(p[1] for p in points) if sole_name == "Foot" else 0
        primitives = []
        for attributes, indices, material, position, direction in entries:
            used = sorted(set(indices))
            remap = {old: new for new, old in enumerate(used)}
            vertices = [tuple(v - c - (sole if k == 1 else 0)
                              for k, (v, c) in enumerate(zip(game(position(attributes["POSITION"][i])), center))) for i in used]
            normals = [normal(direction(attributes["NORMAL"][i])) for i in used]
            uv = [attributes["TEXCOORD_0"][i] for i in used]
            primitives.append({"attributes": {"POSITION": target.accessor(vertices, "VEC3"),
                                                "NORMAL": target.accessor(normals, "VEC3"), "TEXCOORD_0": target.accessor(uv, "VEC2")},
                               "indices": target.accessor([(remap[i],) for i in indices], "SCALAR", True), "material": material})
        mesh_index = len(target.g["meshes"])
        target.g["meshes"].append({"name": mesh_name, "primitives": primitives})
        target.node(name, parent, mesh=mesh_index, translation=list(center))
        return sum(len(e[1]) // 3 for e in entries)

    roots = {name: glb.node(name) for name in LOCATIONS}
    counts = {}
    for (location, name), entries in sorted(parts.items()):
        counts[location + "/" + name] = emit(glb, roots[location], location + "_" + name, location + "_" + name, entries, name)
    for location, name, point in markers:
        glb.node(location + "_" + name, roots[location], translation=list(game(point)))
    glb.save(output)

    # The library shares the main model's space and is painted at runtime with its `<Chassis>VariantOlive` material.
    library = Glb(f"Mech {chassis} weapon library")
    library.g["materials"].append({"name": f"{chassis}VariantOlive", "pbrMetallicRoughness": {"baseColorFactor": [.47, .47, .29, 1]}})
    library.g.pop("samplers")
    library.g.pop("textures")
    library.g.pop("images")
    library_record = {}
    for hardpoint, info in sorted(hardpoints.items()):
        override = overrides.get(hardpoint, {})
        location = override.get("location") or stock.get(hardpoint, {}).get("location") or hardpoint_location(info["bone"])
        bone = override.get("bone") or stock.get(hardpoint, {}).get("bone") or hardpoint_bone(info["bone"])
        node = library.node(hardpoint)
        visuals = {}
        for visual, mesh in sorted(info["files"].items()):
            entries, fires = weapon(mesh, bone)
            entries = [(a, i, 0, position, direction) for a, i, _, position, direction in entries]
            # Godot makes imported node names unique scene-wide, so every name carries its hardpoint.
            visuals[visual] = {"triangles": emit(library, node, f"{hardpoint}_{visual}", f"{hardpoint}_{visual}", entries),
                               "markers": len(fires)}
            for i, point in enumerate(fires):
                library.node(f"{hardpoint}_{visual}_Fire{i:02}", node, translation=list(game(point)))
        entry = {"class": CLASSES[info["cls"]], "location": location, "bone": bone, "visuals": visuals}
        if hardpoint in stock:
            entry.update({key: stock[hardpoint][key] for key in ("visual", "part", "markers")})
            entry["stock"] = entry.pop("visual")
        library_record[hardpoint] = entry
    missing = set(stock) - set(library_record)
    if missing:
        raise ValueError(f"{chassis} stock hardpoints missing from the weapon exports: {sorted(missing)}")
    library.save(library_output)

    def pivot(name):
        return [round(v, 7) for v in game(body.world(name))]

    manifest = {
        "source": str(source), "source_files": {str(p.relative_to(source)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(inputs)},
        "runtime_triangles": sum(counts.values()), "render_meshes": len(parts), "parts": counts,
        "cross_region_faces": seam_faces, "weighted_faces": weighted_faces,
        "height": height, "displayed_height": displayed_height, "scale": scale, "bottom": bottom,
        "rig": {"waist": pivot("Torso_Twist")[1], "hip": pivot("Thigh_Right"), "knee": pivot("Calf_Right"),
                "ankle": pivot("Foot0_Right"), "shoulder": pivot("Upperarm_Right")},
        "weapons": [{"location": owner, "name": name, "bone": bone, "mesh": mesh, "local_transform": "identity"}
                    for owner, name, bone, mesh in weapons],
        "hardpoints": library_record,
        "library_bytes": library_output.stat().st_size,
        "cockpit": {"mesh": f"{prefix}_TP_Cockpit_DM0_STM", "bone": cockpit_bone, "glass_section": glass_section},
        "missile_tubes": missile_tubes,
        "attachment_evidence": evidence,
        "materials": "Olive palette baked from default/wear masks; source UVs and normal detail retained; amber exterior canopy.",
        "tool": "scripts/prepare-mech.py + scripts/mech_asset_tools.py; Python + Pillow",
    }
    output.with_suffix(".json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(json.dumps({k: v for k, v in manifest.items() if k not in ("source_files", "hardpoints")}, indent=2))


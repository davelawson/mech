"""Assemble MW5's stock CPLT-C1 as rigid, textured damage groups.

Run: python3 scripts/prepare-catapult.py (requires Pillow).
Inputs are untouched UE Viewer exports in generated/mw5-catapult.
No Blender or Unreal installation is required to rebuild the runtime GLB.
"""
from collections import defaultdict
import hashlib
import io
import json
import math
from pathlib import Path
import struct

from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "generated/mw5-catapult"
OUTPUT = ROOT / "Assets/Models/Catapult.glb"
HEIGHT = 3.16 / 1.225  # Factory applies the unchanged 65-ton scale.
LOCATIONS = ("Head", "CenterTorso", "LeftTorso", "RightTorso", "LeftArm", "RightArm", "LeftLeg", "RightLeg")


def rotate(q, p):
    x, y, z, w = q
    length = math.sqrt(sum(v * v for v in q))
    x, y, z, w = (v / length for v in (x, y, z, w))
    a, b, c = p
    tx, ty, tz = 2 * (y * c - z * b), 2 * (z * a - x * c), 2 * (x * b - y * a)
    return (a + w * tx + y * tz - z * ty,
            b + w * ty + z * tx - x * tz,
            c + w * tz + x * ty - y * tx)


class Asset:
    def __init__(self, relative):
        self.path = SOURCE / relative
        self.g = json.loads(self.path.read_text())
        self.buffers = [(self.path.parent / b["uri"]).read_bytes() for b in self.g["buffers"]]
        self.parents = {c: i for i, n in enumerate(self.g["nodes"]) for c in n.get("children", [])}
        self.names = {n.get("name"): i for i, n in enumerate(self.g["nodes"])}

    def data(self, i):
        a = self.g["accessors"][i]
        v = self.g["bufferViews"][a["bufferView"]]
        size = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[a["type"]]
        fmt = {5121: "B", 5123: "H", 5125: "I", 5126: "f"}[a["componentType"]] * size
        offset = v.get("byteOffset", 0) + a.get("byteOffset", 0)
        stride = v.get("byteStride", struct.calcsize("<" + fmt))
        values = [struct.unpack_from("<" + fmt, self.buffers[v["buffer"]], offset + j * stride)
                  for j in range(a["count"])]
        if a.get("normalized"):
            divisor = {5121: 255, 5123: 65535}[a["componentType"]]
            return [tuple(c / divisor for c in value) for value in values]
        return values

    def world(self, bone, point=(0, 0, 0), direction=False):
        i = self.names[bone] if isinstance(bone, str) else bone
        node = self.g["nodes"][i]
        p = rotate(node.get("rotation", (0, 0, 0, 1)), point)
        if not direction:
            p = tuple(a + b for a, b in zip(p, node.get("translation", (0, 0, 0))))
        return self.world(self.parents[i], p, direction) if i in self.parents else p

    def primitives(self):
        for mesh in self.g["meshes"]:
            for primitive in mesh["primitives"]:
                yield primitive, {name: self.data(a) for name, a in primitive["attributes"].items()}, [v[0] for v in self.data(primitive["indices"])]


class Glb:
    def __init__(self):
        self.binary = bytearray()
        self.g = {"asset": {"version": "2.0", "generator": "Mech Catapult preparation"},
                  "scene": 0, "scenes": [{"nodes": []}], "nodes": [], "meshes": [],
                  "materials": [], "textures": [], "images": [], "samplers": [{"magFilter": 9729, "minFilter": 9987, "wrapS": 10497, "wrapT": 10497}],
                  "bufferViews": [], "accessors": []}

    def view(self, raw):
        self.binary.extend(b"\0" * (-len(self.binary) % 4))
        result = len(self.g["bufferViews"])
        self.g["bufferViews"].append({"buffer": 0, "byteOffset": len(self.binary), "byteLength": len(raw)})
        self.binary.extend(raw)
        return result

    def accessor(self, values, kind, indices=False):
        fmt, component = ("I", 5125) if indices else ("f", 5126)
        raw = struct.pack("<" + fmt * sum(len(v) for v in values), *(c for v in values for c in v))
        result = len(self.g["accessors"])
        a = {"bufferView": self.view(raw), "componentType": component, "type": kind, "count": len(values)}
        if kind == "VEC3":
            a["min"] = [min(v[i] for v in values) for i in range(3)]
            a["max"] = [max(v[i] for v in values) for i in range(3)]
        self.g["accessors"].append(a)
        return result

    def texture(self, image):
        stream = io.BytesIO()
        image.save(stream, format="PNG")
        i = len(self.g["images"])
        self.g["images"].append({"bufferView": self.view(stream.getvalue()), "mimeType": "image/png"})
        self.g["textures"].append({"source": i, "sampler": 0})
        return {"index": i}

    def node(self, name, parent=None, **properties):
        i = len(self.g["nodes"])
        self.g["nodes"].append({"name": name, **properties})
        if parent is None:
            self.g["scenes"][0]["nodes"].append(i)
        else:
            self.g["nodes"][parent].setdefault("children", []).append(i)
        return i

    def save(self):
        self.binary.extend(b"\0" * (-len(self.binary) % 4))
        self.g["buffers"] = [{"byteLength": len(self.binary)}]
        encoded = json.dumps(self.g, separators=(",", ":")).encode()
        encoded += b" " * (-len(encoded) % 4)
        raw = struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(encoded) + 8 + len(self.binary))
        raw += struct.pack("<II", len(encoded), 0x4E4F534A) + encoded
        raw += struct.pack("<II", len(self.binary), 0x004E4942) + self.binary
        OUTPUT.parent.mkdir(parents=True, exist_ok=True)
        OUTPUT.write_bytes(raw)


def paint(glb, section):
    folder = SOURCE / "Body/Materials/Textures"
    mask = Image.open(folder / f"Catapult_{section}_Default_MSK.png").convert("RGB")
    r, g, b = mask.split()
    # Source mask channels select paint regions; black is unpainted machinery.
    albedo = Image.composite(Image.new("RGB", mask.size, "#777749"), Image.new("RGB", mask.size, "#303935"), r)
    albedo = Image.composite(Image.new("RGB", mask.size, "#909063"), albedo, g)
    albedo = Image.composite(Image.new("RGB", mask.size, "#56605a"), albedo, b)
    wear = Image.open(folder / f"Catapult_{section}_Wear_MSK.png").convert("RGB").getchannel("B").point(lambda p: int(p * .25))
    albedo = Image.composite(Image.new("RGB", mask.size, "#b1a477"), albedo, wear)
    normal = Image.open(folder / f"Catapult_{section}_NRM.png").convert("RGB")
    nr, ng, nb = normal.split()
    # Unreal normal maps use DirectX's green-channel convention; glTF uses OpenGL.
    normal = Image.merge("RGB", (nr, ImageOps.invert(ng), nb))
    material = {"name": f"Catapult{section}Olive", "pbrMetallicRoughness": {
        "baseColorTexture": glb.texture(albedo), "metallicFactor": .28, "roughnessFactor": .78},
        "normalTexture": {**glb.texture(normal), "scale": .7}}
    glb.g["materials"].append(material)
    return len(glb.g["materials"]) - 1


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

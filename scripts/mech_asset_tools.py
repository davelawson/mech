"""glTF input, rigid mesh output, and olive materials shared by MW5 preparation scripts."""
import io
import json
import math
from pathlib import Path
import struct

from PIL import Image, ImageOps


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
    def __init__(self, path):
        self.path = Path(path)
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
    def __init__(self, generator="Mech asset preparation"):
        self.binary = bytearray()
        self.g = {"asset": {"version": "2.0", "generator": generator},
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

    def save(self, output):
        self.binary.extend(b"\0" * (-len(self.binary) % 4))
        self.g["buffers"] = [{"byteLength": len(self.binary)}]
        encoded = json.dumps(self.g, separators=(",", ":")).encode()
        encoded += b" " * (-len(encoded) % 4)
        raw = struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(encoded) + 8 + len(self.binary))
        raw += struct.pack("<II", len(encoded), 0x4E4F534A) + encoded
        raw += struct.pack("<II", len(self.binary), 0x004E4942) + self.binary
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_bytes(raw)


def paint(glb, source, chassis, section):
    folder = source / "Body/Materials/Textures"
    mask = Image.open(folder / f"{chassis}_{section}_Default_MSK.png").convert("RGB")
    r, g, b = mask.split()
    # Source mask channels select paint regions; black is unpainted machinery.
    albedo = Image.composite(Image.new("RGB", mask.size, "#777749"), Image.new("RGB", mask.size, "#303935"), r)
    albedo = Image.composite(Image.new("RGB", mask.size, "#909063"), albedo, g)
    albedo = Image.composite(Image.new("RGB", mask.size, "#56605a"), albedo, b)
    wear = Image.open(folder / f"{chassis}_{section}_Wear_MSK.png").convert("RGB").getchannel("B").point(lambda p: int(p * .25))
    albedo = Image.composite(Image.new("RGB", mask.size, "#b1a477"), albedo, wear)
    normal = Image.open(folder / f"{chassis}_{section}_NRM.png").convert("RGB")
    nr, ng, nb = normal.split()
    # Unreal normal maps use DirectX's green-channel convention; glTF uses OpenGL.
    normal = Image.merge("RGB", (nr, ImageOps.invert(ng), nb))
    material = {"name": f"{chassis}{section}Olive", "pbrMetallicRoughness": {
        "baseColorTexture": glb.texture(albedo), "metallicFactor": .28, "roughnessFactor": .78},
        "normalTexture": {**glb.texture(normal), "scale": .7}}
    glb.g["materials"].append(material)
    return len(glb.g["materials"]) - 1


#!/usr/bin/env python3
"""Render Vintage Story cuboid shapes and Flywheel runtime geometry from fixed views."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

Vec3 = tuple[float, float, float]


@dataclass
class Face:
    vertices: list[Vec3]
    material: str
    element: str


VIEWS: dict[str, tuple[Vec3, Vec3]] = {
    "front": ((1, 0, 0), (0, 1, 0)),
    "back": ((-1, 0, 0), (0, 1, 0)),
    "right": ((0, 0, 1), (0, 1, 0)),
    "left": ((0, 0, -1), (0, 1, 0)),
    "top": ((0, 1, 0), (0, 0, -1)),
    "bottom": ((0, -1, 0), (0, 0, 1)),
    "isometric": ((1, 0.78, 1), (0, 1, 0)),
}

FACE_INDICES = {
    "north": (0, 2, 3, 1),
    "east": (1, 3, 7, 5),
    "south": (4, 5, 7, 6),
    "west": (0, 4, 6, 2),
    "up": (2, 6, 7, 3),
    "down": (0, 1, 5, 4),
}

FALLBACK_COLORS = {
    "wood": (139, 94, 52),
    "wheel": (113, 119, 122),
    "metal": (126, 132, 135),
    "bearing": (79, 82, 81),
    "chalk": (148, 38, 38),
    "stone": (111, 105, 96),
}


def add(a: Vec3, b: Vec3) -> Vec3:
    return tuple(a[index] + b[index] for index in range(3))  # type: ignore[return-value]


def sub(a: Vec3, b: Vec3) -> Vec3:
    return tuple(a[index] - b[index] for index in range(3))  # type: ignore[return-value]


def mul(vector: Vec3, scalar: float) -> Vec3:
    return tuple(value * scalar for value in vector)  # type: ignore[return-value]


def dot(a: Vec3, b: Vec3) -> float:
    return sum(a[index] * b[index] for index in range(3))


def cross(a: Vec3, b: Vec3) -> Vec3:
    return (
        a[1] * b[2] - a[2] * b[1],
        a[2] * b[0] - a[0] * b[2],
        a[0] * b[1] - a[1] * b[0],
    )


def normalize(vector: Vec3) -> Vec3:
    magnitude = math.sqrt(dot(vector, vector))
    if magnitude == 0:
        raise ValueError("Cannot normalize a zero vector.")
    return mul(vector, 1 / magnitude)


def rotate(point: Vec3, origin: Vec3, angles: Vec3) -> Vec3:
    x, y, z = sub(point, origin)
    rx, ry, rz = (math.radians(value) for value in angles)
    y, z = y * math.cos(rx) - z * math.sin(rx), y * math.sin(rx) + z * math.cos(rx)
    x, z = x * math.cos(ry) + z * math.sin(ry), -x * math.sin(ry) + z * math.cos(ry)
    x, y = x * math.cos(rz) - y * math.sin(rz), x * math.sin(rz) + y * math.cos(rz)
    return add((x, y, z), origin)


def cuboid(start: Vec3, end: Vec3) -> list[Vec3]:
    x0, y0, z0 = start
    x1, y1, z1 = end
    return [
        (x0, y0, z0), (x1, y0, z0), (x0, y1, z0), (x1, y1, z0),
        (x0, y0, z1), (x1, y0, z1), (x0, y1, z1), (x1, y1, z1),
    ]


def load_shape(path: Path) -> tuple[list[Face], dict[str, str]]:
    data = json.loads(path.read_text(encoding="utf-8"))
    faces: list[Face] = []

    def visit(elements: list[dict], inherited_angles: Vec3 = (0, 0, 0)) -> None:
        for element in elements:
            vertices = cuboid(tuple(element["from"]), tuple(element["to"]))
            angles = (
                inherited_angles[0] + float(element.get("rotationX", 0)),
                inherited_angles[1] + float(element.get("rotationY", 0)),
                inherited_angles[2] + float(element.get("rotationZ", 0)),
            )
            if angles != (0, 0, 0):
                origin = tuple(element.get("rotationOrigin", (8, 8, 8)))
                vertices = [rotate(vertex, origin, angles) for vertex in vertices]
            for direction, definition in element.get("faces", {}).items():
                indices = FACE_INDICES.get(direction)
                if indices:
                    faces.append(Face(
                        [vertices[index] for index in indices],
                        str(definition.get("texture", "#missing")).lstrip("#"),
                        element.get("name", "unnamed"),
                    ))
            visit(element.get("children", []), angles)

    visit(data.get("elements", []))
    return faces, dict(data.get("textures", {}))


def constants(path: Path) -> dict[str, float]:
    matches = re.findall(
        r"internal const float\s+(\w+)\s*=\s*([0-9.]+)f?;",
        path.read_text(encoding="utf-8"),
    )
    return {name: float(value) for name, value in matches}


def add_annulus(
    faces: list[Face],
    min_x: float,
    max_x: float,
    inner_radius: float,
    outer_radius: float,
    material: str,
    element: str,
    segments: int = 72,
) -> None:
    def vertex(x: float, radius: float, angle: float) -> Vec3:
        return (x, 8 + radius * math.sin(angle), 8 + radius * math.cos(angle))

    for segment in range(segments):
        a0 = math.tau * segment / segments
        a1 = math.tau * (segment + 1) / segments
        faces.extend([
            Face([vertex(max_x, inner_radius, a0), vertex(max_x, inner_radius, a1),
                  vertex(max_x, outer_radius, a1), vertex(max_x, outer_radius, a0)], material, element),
            Face([vertex(min_x, inner_radius, a0), vertex(min_x, outer_radius, a0),
                  vertex(min_x, outer_radius, a1), vertex(min_x, inner_radius, a1)], material, element),
            Face([vertex(min_x, outer_radius, a0), vertex(max_x, outer_radius, a0),
                  vertex(max_x, outer_radius, a1), vertex(min_x, outer_radius, a1)], material, element),
        ])
        if inner_radius:
            faces.append(Face(
                [vertex(max_x, inner_radius, a0), vertex(min_x, inner_radius, a0),
                 vertex(min_x, inner_radius, a1), vertex(max_x, inner_radius, a1)],
                material,
                element,
            ))


def add_rotated_cuboid(
    faces: list[Face],
    start: Vec3,
    end: Vec3,
    material: str,
    element: str,
    rotation_x: float,
) -> None:
    vertices = cuboid(start, end)
    if rotation_x:
        vertices = [rotate(vertex, (8, 8, 8), (rotation_x, 0, 0)) for vertex in vertices]
    for indices in FACE_INDICES.values():
        faces.append(Face([vertices[index] for index in indices], material, element))


def load_flywheel(path: Path, size: str) -> list[Face]:
    values = constants(path)
    prefix = "Compact" if size == "compact" else ""

    def value(name: str) -> float:
        return values[f"{prefix}{name}"] * 16

    faces: list[Face] = []
    wheel_radius = value("WheelOuterRadius")
    wheel_inner = value("CoupledInnerRadius")
    wheel_half = value("WheelHalfThickness")
    wheel_min, wheel_max = 8 - wheel_half, 8 + wheel_half
    if size == "compact":
        add_annulus(faces, wheel_min, wheel_max, wheel_inner, wheel_radius, "wheel", "RuntimeWheel")
    else:
        spoke_half = value("SpokeHalfWidth")
        spoke_inner = value("HubOuterRadius") * 0.92
        spoke_outer = value("FelloeInnerRadius") + 0.02 * 16
        for index in range(8):
            add_rotated_cuboid(
                faces,
                (wheel_min, 8 - spoke_half, 8 + spoke_inner),
                (wheel_max, 8 + spoke_half, 8 + spoke_outer),
                "wood",
                f"RuntimeWoodSpoke{index}",
                index * 45,
            )
        add_annulus(
            faces, wheel_min, wheel_max,
            value("FelloeInnerRadius"), value("FelloeOuterRadius"),
            "wood", "RuntimeWoodFelloe",
        )
        add_annulus(
            faces, wheel_min, wheel_max,
            value("TyreInnerRadius"), wheel_radius,
            "wheel", "RuntimeOuterTyre",
        )
    add_annulus(
        faces, 8 - value("BearingHalfThickness"), 8 + value("BearingHalfThickness"),
        value("ShaftClearanceRadius"), value("BearingOuterRadius"), "bearing", "BearingCollar", 48,
    )
    add_annulus(
        faces, 8 - value("HubHalfThickness"), 8 + value("HubHalfThickness"),
        value("BearingOuterRadius"), value("HubOuterRadius"), "metal", "Hub",
    )
    plate_thickness = value("CouplingPlateThickness")
    plate_radius = value("CouplingPlateOuterRadius")
    shaft_radius = value("ShaftClearanceRadius")
    add_annulus(faces, wheel_max + 0.08, wheel_max + 0.08 + plate_thickness,
                shaft_radius, plate_radius, "metal", "FrontCouplingPlate")
    add_annulus(faces, wheel_min - 0.08 - plate_thickness, wheel_min - 0.08,
                shaft_radius, plate_radius, "metal", "BackCouplingPlate")

    marker_raise = 0.006 * 16
    marker_half = (0.025 if size == "compact" else 0.04) * 16
    marker_outer = wheel_radius + marker_raise * 2
    for x in (wheel_min - marker_raise, wheel_max + marker_raise):
        faces.append(Face([
            (x, 8 - marker_half, 8 + wheel_radius * 0.18),
            (x, 8 + marker_half, 8 + wheel_radius * 0.18),
            (x, 8 + marker_half, 8 + marker_outer),
            (x, 8 - marker_half, 8 + marker_outer),
        ], "chalk", "RegistrationMarkFace"))
    radius = wheel_radius + marker_raise
    half_angle = marker_half / radius

    def rim(x: float, angle: float) -> Vec3:
        return (x, 8 + radius * math.sin(angle), 8 + radius * math.cos(angle))

    faces.append(Face([
        rim(wheel_min - marker_raise * 2, -half_angle),
        rim(wheel_max + marker_raise * 2, -half_angle),
        rim(wheel_max + marker_raise * 2, half_angle),
        rim(wheel_min - marker_raise * 2, half_angle),
    ], "chalk", "RegistrationMarkRim"))
    return faces


def resolve_texture(location: str, roots: list[Path]) -> Path | None:
    domain, relative = location.split(":", 1) if ":" in location else ("game", location)
    relative = relative.removeprefix("textures/")
    if not relative.endswith(".png"):
        relative += ".png"
    for root in roots:
        candidate = root / domain / "textures" / relative
        if candidate.exists():
            return candidate
    return None


def average_color(path: Path | None, material: str, location: str = "") -> tuple[int, int, int]:
    if path is None:
        if "/wood/" in location:
            return FALLBACK_COLORS["wood"]
        if "/stone/" in location:
            return FALLBACK_COLORS["stone"]
        return FALLBACK_COLORS.get(material, (155, 155, 155))
    return tuple(Image.open(path).convert("RGB").resize((1, 1)).getpixel((0, 0)))


def render(faces: list[Face], colors: dict[str, tuple[int, int, int]], view_name: str, output: Path, size: int) -> None:
    view, nominal_up = VIEWS[view_name]
    view = normalize(view)
    right = normalize(cross(nominal_up, view))
    up = normalize(cross(view, right))
    vertices = [vertex for face in faces for vertex in face.vertices]
    center = tuple(
        (min(vertex[index] for vertex in vertices) + max(vertex[index] for vertex in vertices)) / 2
        for index in range(3)
    )
    projected = [(dot(sub(vertex, center), right), dot(sub(vertex, center), up)) for vertex in vertices]
    scale = size * 0.78 / max(
        max(abs(point[0]) for point in projected) * 2,
        max(abs(point[1]) for point in projected) * 2,
        1,
    )
    image = Image.new("RGB", (size, size), (28, 31, 34))
    draw = ImageDraw.Draw(image)
    light = normalize((0.55, 0.8, 0.45))
    ordered = sorted(faces, key=lambda face: sum(dot(sub(v, center), view) for v in face.vertices) / len(face.vertices))
    for face in ordered:
        normal = normalize(cross(sub(face.vertices[1], face.vertices[0]), sub(face.vertices[2], face.vertices[0])))
        if dot(normal, view) <= 0.001:
            continue
        brightness = 0.48 + 0.52 * max(0, dot(normal, light))
        base = colors.get(face.material, (155, 155, 155))
        fill = tuple(round(channel * brightness) for channel in base)
        points = [
            (size / 2 + dot(sub(v, center), right) * scale, size / 2 - dot(sub(v, center), up) * scale)
            for v in face.vertices
        ]
        draw.polygon(points, fill=fill)
        draw.line(points + [points[0]], fill=(16, 18, 20), width=max(1, size // 420))
    draw.rounded_rectangle((12, 12, 126, 42), radius=7, fill=(8, 10, 12), outline=(87, 94, 99))
    draw.text((22, 20), view_name.upper(), fill=(235, 238, 240), font=ImageFont.load_default())
    image.save(output)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--assets-root", type=Path, action="append", default=[])
    parser.add_argument("--size", type=int, default=720)
    args = parser.parse_args()

    manifest_path = args.manifest.resolve()
    base = manifest_path.parent
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    output = args.output_dir.resolve()
    output.mkdir(parents=True, exist_ok=True)
    roots = [path.resolve() for path in args.assets_root]
    installed_assets = Path(r"D:\Games\Vintagestory\assets")
    if installed_assets.exists() and installed_assets not in roots:
        roots.append(installed_assets)

    faces: list[Face] = []
    textures: dict[str, str] = {}
    inputs = [manifest_path]
    for relative in manifest.get("shapes", []):
        path = (base / relative).resolve()
        shape_faces, shape_textures = load_shape(path)
        faces.extend(shape_faces)
        textures.update(shape_textures)
        inputs.append(path)
    if "proceduralFlywheel" in manifest:
        spec = manifest["proceduralFlywheel"]
        path = (base / spec["dimensionsSource"]).resolve()
        faces.extend(load_flywheel(path, spec["size"]))
        inputs.append(path)
    textures.update(manifest.get("textures", {}))

    resolved = {}
    colors = {}
    for material in sorted({face.material for face in faces}):
        texture = resolve_texture(textures.get(material, ""), roots) if textures.get(material) else None
        resolved[material] = str(texture) if texture else None
        colors[material] = average_color(texture, material, textures.get(material, ""))

    images = []
    for view_name in VIEWS:
        path = output / f"{view_name}.png"
        render(faces, colors, view_name, path, args.size)
        images.append(path)
    sheet = Image.new("RGB", (args.size * 4, args.size * 2), (20, 22, 24))
    for index, path in enumerate(images):
        sheet.paste(Image.open(path), ((index % 4) * args.size, (index // 4) * args.size))
    sheet.save(output / "contact-sheet.png")

    vertices = [vertex for face in faces for vertex in face.vertices]
    metadata = {
        "name": manifest.get("name", manifest_path.stem),
        "inputs": [{"path": str(path), "sha256": sha256(path)} for path in inputs],
        "faceCount": len(faces),
        "boundsModelUnits": {
            "min": [min(vertex[index] for vertex in vertices) for index in range(3)],
            "max": [max(vertex[index] for vertex in vertices) for index in range(3)],
        },
        "resolvedTextures": resolved,
        "unresolvedTextures": sorted(key for key, path in resolved.items() if path is None),
        "views": list(VIEWS),
    }
    (output / "render-metadata.json").write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
    print(f"Rendered {metadata['name']} to {output}")


if __name__ == "__main__":
    main()

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

from PIL import Image, ImageDraw, ImageEnhance, ImageFont

Vec2 = tuple[float, float]
Vec3 = tuple[float, float, float]


@dataclass
class Face:
    vertices: list[Vec3]
    material: str
    element: str
    uvs: list[Vec2] | None = None


VIEWS: dict[str, tuple[Vec3, Vec3]] = {
    "front": ((1, 0, 0), (0, 1, 0)),
    "back": ((-1, 0, 0), (0, 1, 0)),
    "right": ((0, 0, 1), (0, 1, 0)),
    "left": ((0, 0, -1), (0, 1, 0)),
    "top": ((0, 1, 0), (0, 0, -1)),
    "bottom": ((0, -1, 0), (0, 0, 1)),
    "isometric": ((1, 0.78, 1), (0, 1, 0)),
    "isometric-opposite": ((-1, 0.78, -1), (0, 1, 0)),
}

RENDER_MODES = ("wireframe", "material", "textured")

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


def face_uvs(
    definition: dict,
    texture_width: float,
    texture_height: float,
    direction: str,
    start: Vec3,
    end: Vec3,
) -> list[Vec2]:
    spans = sub(end, start)
    automatic_size = {
        "north": (spans[0], spans[1]),
        "south": (spans[0], spans[1]),
        "east": (spans[2], spans[1]),
        "west": (spans[2], spans[1]),
        "up": (spans[0], spans[2]),
        "down": (spans[0], spans[2]),
    }.get(direction, (texture_width, texture_height))
    raw = definition.get("uv", (0, 0, automatic_size[0], automatic_size[1]))
    u0, v0, u1, v1 = (float(value) for value in raw)
    result = [
        (u0 / texture_width, v1 / texture_height),
        (u0 / texture_width, v0 / texture_height),
        (u1 / texture_width, v0 / texture_height),
        (u1 / texture_width, v1 / texture_height),
    ]
    quarter_turns = int(definition.get("rotation", 0)) // 90
    if quarter_turns:
        quarter_turns %= 4
        result = result[-quarter_turns:] + result[:-quarter_turns]
    return result


def load_shape(path: Path) -> tuple[list[Face], dict[str, str]]:
    data = json.loads(path.read_text(encoding="utf-8"))
    faces: list[Face] = []
    texture_width = float(data.get("textureWidth", 16))
    texture_height = float(data.get("textureHeight", 16))

    def visit(elements: list[dict], inherited_angles: Vec3 = (0, 0, 0)) -> None:
        for element in elements:
            start = tuple(element["from"])
            end = tuple(element["to"])
            vertices = cuboid(start, end)
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
                        face_uvs(definition, texture_width, texture_height, direction, start, end),
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

    def disc_uv(radius: float, angle: float) -> Vec2:
        return (
            0.5 + 0.5 * radius * math.sin(angle) / outer_radius,
            0.5 - 0.5 * radius * math.cos(angle) / outer_radius,
        )

    for segment in range(segments):
        a0 = math.tau * segment / segments
        a1 = math.tau * (segment + 1) / segments
        t0 = segment / segments
        t1 = (segment + 1) / segments
        faces.extend([
            Face([vertex(max_x, inner_radius, a0), vertex(max_x, inner_radius, a1),
                  vertex(max_x, outer_radius, a1), vertex(max_x, outer_radius, a0)],
                 material, element,
                 [disc_uv(inner_radius, a0), disc_uv(inner_radius, a1),
                  disc_uv(outer_radius, a1), disc_uv(outer_radius, a0)]),
            Face([vertex(min_x, inner_radius, a0), vertex(min_x, outer_radius, a0),
                  vertex(min_x, outer_radius, a1), vertex(min_x, inner_radius, a1)],
                 material, element,
                 [disc_uv(inner_radius, a0), disc_uv(outer_radius, a0),
                  disc_uv(outer_radius, a1), disc_uv(inner_radius, a1)]),
            Face([vertex(min_x, outer_radius, a0), vertex(max_x, outer_radius, a0),
                  vertex(max_x, outer_radius, a1), vertex(min_x, outer_radius, a1)],
                 material, element, [(t0, 1), (t0, 0), (t1, 0), (t1, 1)]),
        ])
        if inner_radius:
            faces.append(Face(
                [vertex(max_x, inner_radius, a0), vertex(min_x, inner_radius, a0),
                 vertex(min_x, inner_radius, a1), vertex(max_x, inner_radius, a1)],
                material,
                element,
                [(t0, 0), (t0, 1), (t1, 1), (t1, 0)],
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
    default_uvs = [(0, 1), (0, 0), (1, 0), (1, 1)]
    for indices in FACE_INDICES.values():
        faces.append(Face([vertices[index] for index in indices], material, element, default_uvs))


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
    front_mark = [
        (wheel_max + marker_raise, 8 - marker_half, 8 + wheel_radius * 0.18),
        (wheel_max + marker_raise, 8 + marker_half, 8 + wheel_radius * 0.18),
        (wheel_max + marker_raise, 8 + marker_half, 8 + marker_outer),
        (wheel_max + marker_raise, 8 - marker_half, 8 + marker_outer),
    ]
    back_mark = [
        (wheel_min - marker_raise, 8 - marker_half, 8 + wheel_radius * 0.18),
        (wheel_min - marker_raise, 8 - marker_half, 8 + marker_outer),
        (wheel_min - marker_raise, 8 + marker_half, 8 + marker_outer),
        (wheel_min - marker_raise, 8 + marker_half, 8 + wheel_radius * 0.18),
    ]
    marker_uvs = [(0, 1), (0, 0), (1, 0), (1, 1)]
    faces.append(Face(front_mark, "chalk", "RegistrationMarkFaceFront", marker_uvs))
    faces.append(Face(back_mark, "chalk", "RegistrationMarkFaceBack", marker_uvs))
    radius = wheel_radius + marker_raise
    half_angle = marker_half / radius

    def rim(x: float, angle: float) -> Vec3:
        return (x, 8 + radius * math.sin(angle), 8 + radius * math.cos(angle))

    faces.append(Face([
        rim(wheel_min - marker_raise * 2, -half_angle),
        rim(wheel_max + marker_raise * 2, -half_angle),
        rim(wheel_max + marker_raise * 2, half_angle),
        rim(wheel_min - marker_raise * 2, half_angle),
    ], "chalk", "RegistrationMarkRim", marker_uvs))
    return faces


def resolve_texture(location: str, roots: list[Path]) -> Path | None:
    if not location:
        return None
    domain, relative = location.split(":", 1) if ":" in location else ("game", location)
    relative = relative.removeprefix("textures/")
    if not relative.endswith(".png"):
        relative += ".png"
    for root in roots:
        candidates = [
            root / domain / "textures" / relative,
            root / "textures" / relative,
        ]
        if domain == "game":
            candidates.extend(root / pack / "textures" / relative for pack in ("game", "survival", "creative"))
        for candidate in candidates:
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


def projection(
    faces: list[Face],
    view_name: str,
    size: int,
) -> tuple[Vec3, Vec3, Vec3, Vec3, float]:
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
    return view, right, up, center, scale


def screen_point(vertex: Vec3, center: Vec3, right: Vec3, up: Vec3, size: int, scale: float) -> Vec2:
    return (
        size / 2 + dot(sub(vertex, center), right) * scale,
        size / 2 - dot(sub(vertex, center), up) * scale,
    )


def affine_texture_coefficients(points: list[Vec2], uvs: list[Vec2], texture: Image.Image) -> tuple[float, ...] | None:
    p0, p1, p2 = points[:3]
    uv0, uv1, uv2 = uvs[:3]
    dx1, dy1 = p1[0] - p0[0], p1[1] - p0[1]
    dx2, dy2 = p2[0] - p0[0], p2[1] - p0[1]
    determinant = dx1 * dy2 - dx2 * dy1
    if abs(determinant) < 1e-8:
        return None

    def coefficients(values: tuple[float, float, float], extent: int) -> tuple[float, float, float]:
        q0, q1, q2 = (value * max(1, extent - 1) for value in values)
        dq1, dq2 = q1 - q0, q2 - q0
        a = (dq1 * dy2 - dq2 * dy1) / determinant
        b = (dx1 * dq2 - dx2 * dq1) / determinant
        c = q0 - a * p0[0] - b * p0[1]
        return a, b, c

    u = coefficients((uv0[0], uv1[0], uv2[0]), texture.width)
    v = coefficients((uv0[1], uv1[1], uv2[1]), texture.height)
    return u + v


def draw_textured_face(
    image: Image.Image,
    points: list[Vec2],
    face: Face,
    texture: Image.Image,
    brightness: float,
    line_width: int,
) -> None:
    coefficients = affine_texture_coefficients(
        points,
        face.uvs or [(0, 1), (0, 0), (1, 0), (1, 1)],
        texture,
    )
    if coefficients is None:
        return

    left = max(0, math.floor(min(point[0] for point in points)))
    top = max(0, math.floor(min(point[1] for point in points)))
    right = min(image.width, math.ceil(max(point[0] for point in points)) + 1)
    bottom = min(image.height, math.ceil(max(point[1] for point in points)) + 1)
    if right <= left or bottom <= top:
        return

    a, b, c, d, e, f = coefficients
    sampled = texture.transform(
        (right - left, bottom - top),
        Image.Transform.AFFINE,
        (a, b, c + a * left + b * top, d, e, f + d * left + e * top),
        resample=Image.Resampling.NEAREST,
    ).convert("RGB")
    if brightness != 1:
        sampled = ImageEnhance.Brightness(sampled).enhance(brightness)
    local_points = [(point[0] - left, point[1] - top) for point in points]
    mask = Image.new("L", sampled.size, 0)
    ImageDraw.Draw(mask).polygon(local_points, fill=255)
    image.paste(sampled, (left, top), mask)
    ImageDraw.Draw(image).line(points + [points[0]], fill=(18, 20, 22), width=line_width)


def render(
    faces: list[Face],
    colors: dict[str, tuple[int, int, int]],
    textures: dict[str, Image.Image | None],
    view_name: str,
    mode: str,
    output: Path,
    size: int,
) -> None:
    view, right, up, center, scale = projection(faces, view_name, size)
    image = Image.new("RGB", (size, size), (28, 31, 34))
    draw = ImageDraw.Draw(image)
    line_width = max(1, size // 420)
    light = normalize(add(mul(view, 0.72), add(mul(up, 0.62), mul(right, -0.28))))
    ordered = sorted(faces, key=lambda face: sum(dot(sub(v, center), view) for v in face.vertices) / len(face.vertices))

    if mode == "wireframe":
        for face in ordered:
            normal = normalize(cross(sub(face.vertices[1], face.vertices[0]), sub(face.vertices[2], face.vertices[0])))
            facing = dot(normal, view)
            points = [screen_point(vertex, center, right, up, size, scale) for vertex in face.vertices]
            edge = (188, 205, 214) if facing > 0.001 else (61, 70, 76)
            draw.line(points + [points[0]], fill=edge, width=line_width)
    else:
        for face in ordered:
            normal = normalize(cross(sub(face.vertices[1], face.vertices[0]), sub(face.vertices[2], face.vertices[0])))
            if dot(normal, view) <= 0.001:
                continue
            points = [screen_point(vertex, center, right, up, size, scale) for vertex in face.vertices]
            if mode == "textured" and textures.get(face.material) is not None:
                brightness = 0.58 + 0.42 * max(0, dot(normal, light))
                draw_textured_face(
                    image,
                    points,
                    face,
                    textures[face.material],  # type: ignore[arg-type]
                    brightness,
                    line_width,
                )
                continue
            base = colors.get(face.material, (155, 155, 155))
            fill = base if mode == "material" else tuple(round(channel * 0.72) for channel in base)
            draw.polygon(points, fill=fill)
            draw.line(points + [points[0]], fill=(16, 18, 20), width=line_width)

    label = f"{mode.upper()} / {view_name.upper()}"
    label_width = max(164, 12 + len(label) * 7)
    draw.rounded_rectangle((12, 12, label_width, 42), radius=7, fill=(8, 10, 12), outline=(87, 94, 99))
    draw.text((22, 20), label, fill=(235, 238, 240), font=ImageFont.load_default())
    image.save(output)


def contact_sheet(paths: list[Path], output: Path, columns: int, rows: int, size: int) -> None:
    sheet = Image.new("RGB", (size * columns, size * rows), (20, 22, 24))
    for index, path in enumerate(paths):
        sheet.paste(Image.open(path), ((index % columns) * size, (index // columns) * size))
    sheet.save(output)


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

    resolved: dict[str, str | None] = {}
    colors: dict[str, tuple[int, int, int]] = {}
    texture_images: dict[str, Image.Image | None] = {}
    for material in sorted({face.material for face in faces}):
        texture = resolve_texture(textures.get(material, ""), roots) if textures.get(material) else None
        resolved[material] = str(texture) if texture else None
        colors[material] = average_color(texture, material, textures.get(material, ""))
        texture_images[material] = Image.open(texture).convert("RGB") if texture else None

    all_images: list[Path] = []
    for mode in RENDER_MODES:
        mode_output = output / mode
        mode_output.mkdir(parents=True, exist_ok=True)
        mode_images = []
        for view_name in VIEWS:
            path = mode_output / f"{view_name}.png"
            render(faces, colors, texture_images, view_name, mode, path, args.size)
            mode_images.append(path)
            all_images.append(path)
        contact_sheet(mode_images, mode_output / "contact-sheet.png", 4, 2, args.size)
    contact_sheet(all_images, output / "contact-sheet.png", len(VIEWS), len(RENDER_MODES), args.size)

    vertices = [vertex for face in faces for vertex in face.vertices]
    metadata = {
        "name": manifest.get("name", manifest_path.stem),
        "representation": manifest.get("representation", "placed"),
        "inputs": [{"path": str(path), "sha256": sha256(path)} for path in inputs],
        "faceCount": len(faces),
        "boundsModelUnits": {
            "min": [min(vertex[index] for vertex in vertices) for index in range(3)],
            "max": [max(vertex[index] for vertex in vertices) for index in range(3)],
        },
        "resolvedTextures": resolved,
        "unresolvedTextures": sorted(key for key, path in resolved.items() if path is None),
        "views": list(VIEWS),
        "renderModes": list(RENDER_MODES),
        "renderedImageCount": len(all_images),
    }
    (output / "render-metadata.json").write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
    print(f"Rendered {metadata['name']} to {output}")


if __name__ == "__main__":
    main()

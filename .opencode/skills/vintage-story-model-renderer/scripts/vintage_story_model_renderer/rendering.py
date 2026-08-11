"""Texture resolution, camera projection, and deterministic software rasterization."""

from __future__ import annotations

import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from .core import (
    FALLBACK_COLORS,
    VIEWS,
    Face,
    Vec2,
    Vec3,
    add,
    cross,
    dot,
    face_normal,
    mul,
    normalize,
    sub,
)


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


def average_color(
    path: Path | None,
    material: str,
    location: str = "",
) -> tuple[int, int, int]:
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
    return projection_for_view(faces, view, nominal_up, size)


def projection_for_view(
    faces: list[Face],
    view: Vec3,
    nominal_up: Vec3,
    size: int,
    center_override: Vec3 | None = None,
    scale_override: float | None = None,
) -> tuple[Vec3, Vec3, Vec3, Vec3, float]:
    view = normalize(view)
    right = normalize(cross(nominal_up, view))
    up = normalize(cross(view, right))
    vertices = [vertex for face in faces for vertex in face.vertices]
    center = center_override or tuple(
        (min(vertex[index] for vertex in vertices) + max(vertex[index] for vertex in vertices)) / 2
        for index in range(3)
    )
    projected = [
        (dot(sub(vertex, center), right), dot(sub(vertex, center), up))
        for vertex in vertices
    ]
    scale = scale_override or (
        size * 0.78 / max(
            max(abs(point[0]) for point in projected) * 2,
            max(abs(point[1]) for point in projected) * 2,
            1,
        )
    )
    return view, right, up, center, scale


def rotate_view_around_y(view: Vec3, turns: float) -> Vec3:
    angle = turns * math.tau
    cosine = math.cos(angle)
    sine = math.sin(angle)
    return (
        view[0] * cosine + view[2] * sine,
        view[1],
        -view[0] * sine + view[2] * cosine,
    )


def fixed_animation_projections(
    frame_faces: list[list[Face]],
    views: list[Vec3],
    size: int,
) -> list[tuple[Vec3, Vec3, Vec3, Vec3, float]]:
    all_vertices = [
        vertex
        for faces in frame_faces
        for face in faces
        for vertex in face.vertices
    ]
    center = tuple(
        (min(vertex[index] for vertex in all_vertices) + max(vertex[index] for vertex in all_vertices)) / 2
        for index in range(3)
    )
    bases = []
    maximum_span = 1.0
    for faces, view in zip(frame_faces, views):
        normalized_view = normalize(view)
        right = normalize(cross((0, 1, 0), normalized_view))
        up = normalize(cross(normalized_view, right))
        vertices = [vertex for face in faces for vertex in face.vertices]
        projected = [
            (dot(sub(vertex, center), right), dot(sub(vertex, center), up))
            for vertex in vertices
        ]
        maximum_span = max(
            maximum_span,
            max(abs(point[0]) for point in projected) * 2,
            max(abs(point[1]) for point in projected) * 2,
        )
        bases.append((normalized_view, right, up))
    scale = size * 0.78 / maximum_span
    return [
        (view, right, up, center, scale)
        for view, right, up in bases
    ]


def screen_point(
    vertex: Vec3,
    center: Vec3,
    right: Vec3,
    up: Vec3,
    size: int,
    scale: float,
) -> Vec2:
    return (
        size / 2 + dot(sub(vertex, center), right) * scale,
        size / 2 - dot(sub(vertex, center), up) * scale,
    )


def rasterize_triangle(
    pixels: np.ndarray,
    depths: np.ndarray,
    points: list[Vec2],
    vertex_depths: list[float],
    fill: tuple[int, int, int],
    texture: Image.Image | None = None,
    uvs: list[Vec2] | None = None,
    brightness: float = 1,
    opacity: float = 1,
) -> None:
    min_x = max(0, math.floor(min(point[0] for point in points)))
    min_y = max(0, math.floor(min(point[1] for point in points)))
    max_x = min(pixels.shape[1] - 1, math.ceil(max(point[0] for point in points)))
    max_y = min(pixels.shape[0] - 1, math.ceil(max(point[1] for point in points)))
    if min_x > max_x or min_y > max_y:
        return

    x0, y0 = points[0]
    x1, y1 = points[1]
    x2, y2 = points[2]
    denominator = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
    if abs(denominator) < 1e-8:
        return

    grid_y, grid_x = np.mgrid[min_y:max_y + 1, min_x:max_x + 1]
    sample_x = grid_x + 0.5
    sample_y = grid_y + 0.5
    weight0 = ((y1 - y2) * (sample_x - x2) + (x2 - x1) * (sample_y - y2)) / denominator
    weight1 = ((y2 - y0) * (sample_x - x2) + (x0 - x2) * (sample_y - y2)) / denominator
    weight2 = 1 - weight0 - weight1
    inside = (weight0 >= -1e-9) & (weight1 >= -1e-9) & (weight2 >= -1e-9)
    interpolated_depth = (
        weight0 * vertex_depths[0]
        + weight1 * vertex_depths[1]
        + weight2 * vertex_depths[2]
    )
    current_depth = depths[min_y:max_y + 1, min_x:max_x + 1]
    visible = inside & (interpolated_depth > current_depth + 1e-9)
    if not np.any(visible):
        return

    target = pixels[min_y:max_y + 1, min_x:max_x + 1]
    if texture is not None and uvs is not None:
        texture_pixels = np.asarray(texture.convert("RGB"))
        interpolated_u = weight0 * uvs[0][0] + weight1 * uvs[1][0] + weight2 * uvs[2][0]
        interpolated_v = weight0 * uvs[0][1] + weight1 * uvs[1][1] + weight2 * uvs[2][1]
        texture_x = np.clip(
            np.rint(interpolated_u * max(1, texture.width - 1)).astype(int),
            0,
            texture.width - 1,
        )
        texture_y = np.clip(
            np.rint(interpolated_v * max(1, texture.height - 1)).astype(int),
            0,
            texture.height - 1,
        )
        sampled = texture_pixels[texture_y, texture_x]
        if brightness != 1:
            sampled = np.clip(np.rint(sampled * brightness), 0, 255).astype(np.uint8)
        if opacity < 1:
            blended = np.clip(
                np.rint(target * (1 - opacity) + sampled * opacity),
                0,
                255,
            ).astype(np.uint8)
            target[visible] = blended[visible]
        else:
            target[visible] = sampled[visible]
    else:
        if opacity < 1:
            blended_fill = np.clip(
                np.rint(target * (1 - opacity) + np.asarray(fill) * opacity),
                0,
                255,
            ).astype(np.uint8)
            target[visible] = blended_fill[visible]
        else:
            target[visible] = fill
    current_depth[visible] = interpolated_depth[visible]


def rasterize_edge(
    pixels: np.ndarray,
    depths: np.ndarray,
    start: Vec2,
    end: Vec2,
    start_depth: float,
    end_depth: float,
    color: tuple[int, int, int],
    width: int,
) -> None:
    steps = max(1, math.ceil(max(abs(end[0] - start[0]), abs(end[1] - start[1]))))
    for step in range(steps + 1):
        amount = step / steps
        x = round(start[0] + (end[0] - start[0]) * amount)
        y = round(start[1] + (end[1] - start[1]) * amount)
        depth = start_depth + (end_depth - start_depth) * amount
        for offset_y in range(-(width // 2), width - width // 2):
            for offset_x in range(-(width // 2), width - width // 2):
                pixel_x = x + offset_x
                pixel_y = y + offset_y
                if (
                    0 <= pixel_x < pixels.shape[1]
                    and 0 <= pixel_y < pixels.shape[0]
                    and depth >= depths[pixel_y, pixel_x] - 1e-6
                ):
                    pixels[pixel_y, pixel_x] = color


def render(
    faces: list[Face],
    colors: dict[str, tuple[int, int, int]],
    textures: dict[str, Image.Image | None],
    view_name: str,
    mode: str,
    output: Path,
    size: int,
    projection_override: tuple[Vec3, Vec3, Vec3, Vec3, float] | None = None,
    label_override: str | None = None,
) -> None:
    view, right, up, center, scale = projection_override or projection(faces, view_name, size)
    image = Image.new("RGB", (size, size), (28, 31, 34))
    draw = ImageDraw.Draw(image)
    line_width = max(1, size // 420)
    light = normalize(add(mul(view, 0.72), add(mul(up, 0.62), mul(right, -0.28))))
    ordered = sorted(
        faces,
        key=lambda face: sum(dot(sub(vertex, center), view) for vertex in face.vertices) / len(face.vertices),
    )

    if mode == "wireframe":
        for face in ordered:
            normal = normalize(cross(
                sub(face.vertices[1], face.vertices[0]),
                sub(face.vertices[2], face.vertices[0]),
            ))
            facing = dot(normal, view)
            points = [screen_point(vertex, center, right, up, size, scale) for vertex in face.vertices]
            edge = (188, 205, 214) if facing > 0.001 else (61, 70, 76)
            draw.line(points + [points[0]], fill=edge, width=line_width)
    else:
        pixels = np.asarray(image).copy()
        depths = np.full((size, size), -np.inf)
        visible_faces = []
        for face in faces:
            normal = face_normal(face)
            if dot(normal, view) <= 0.001:
                continue
            points = [screen_point(vertex, center, right, up, size, scale) for vertex in face.vertices]
            vertex_depths = [dot(sub(vertex, center), view) for vertex in face.vertices]
            texture = textures.get(face.material) if mode == "textured" else None
            brightness = 0.58 + 0.42 * max(0, dot(normal, light))
            base = colors.get(face.material, (155, 155, 155))
            fill = base if mode == "material" else tuple(round(channel * 0.72) for channel in base)
            opacity = 0.28 if face.source == "representation-reference" else 1
            face_uv_coordinates = face.uvs or [(0, 1), (0, 0), (1, 0), (1, 1)]
            for index in range(1, len(points) - 1):
                triangle = [points[0], points[index], points[index + 1]]
                triangle_depths = [vertex_depths[0], vertex_depths[index], vertex_depths[index + 1]]
                triangle_uvs = [
                    face_uv_coordinates[0],
                    face_uv_coordinates[index],
                    face_uv_coordinates[index + 1],
                ]
                rasterize_triangle(
                    pixels,
                    depths,
                    triangle,
                    triangle_depths,
                    fill,
                    texture,
                    triangle_uvs,
                    brightness,
                    opacity,
                )
            visible_faces.append((points, vertex_depths, face.source == "representation-reference"))

        for points, vertex_depths, is_reference in visible_faces:
            for index, start in enumerate(points):
                end_index = (index + 1) % len(points)
                rasterize_edge(
                    pixels,
                    depths,
                    start,
                    points[end_index],
                    vertex_depths[index],
                    vertex_depths[end_index],
                    (122, 157, 176) if is_reference else (16, 18, 20),
                    line_width,
                )
        image = Image.fromarray(pixels)
        draw = ImageDraw.Draw(image)

    label = label_override or f"{mode.upper()} / {view_name.upper()}"
    label_width = max(164, 12 + len(label) * 7)
    draw.rounded_rectangle(
        (12, 12, label_width, 42),
        radius=7,
        fill=(8, 10, 12),
        outline=(87, 94, 99),
    )
    draw.text((22, 20), label, fill=(235, 238, 240), font=ImageFont.load_default())
    image.save(output)

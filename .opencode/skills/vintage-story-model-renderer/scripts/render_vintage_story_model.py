#!/usr/bin/env python3
"""Render Vintage Story cuboid shapes and Flywheel runtime geometry from fixed views."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import subprocess
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

Vec2 = tuple[float, float]
Vec3 = tuple[float, float, float]


@dataclass
class Face:
    vertices: list[Vec3]
    material: str
    element: str
    uvs: list[Vec2] | None = None
    surface: str = ""
    source: str = ""


@dataclass(frozen=True)
class CoplanarOverlap:
    first_element: str
    first_surface: str
    second_element: str
    second_surface: str
    overlap_area: float
    plane_distance: float


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


def face_normal(face: Face) -> Vec3:
    origin = face.vertices[0]
    for index in range(1, len(face.vertices) - 1):
        normal = cross(
            sub(face.vertices[index], origin),
            sub(face.vertices[index + 1], origin),
        )
        if dot(normal, normal) > 1e-16:
            return normalize(normal)
    raise ValueError(f"Face {face.element}/{face.surface} is degenerate.")


def signed_polygon_area(points: list[Vec2]) -> float:
    return sum(
        points[index][0] * points[(index + 1) % len(points)][1]
        - points[(index + 1) % len(points)][0] * points[index][1]
        for index in range(len(points))
    ) / 2


def cross_2d(a: Vec2, b: Vec2) -> float:
    return a[0] * b[1] - a[1] * b[0]


def intersect_lines(segment_start: Vec2, segment_end: Vec2, clip_start: Vec2, clip_end: Vec2) -> Vec2:
    segment = (
        segment_end[0] - segment_start[0],
        segment_end[1] - segment_start[1],
    )
    clip = (clip_end[0] - clip_start[0], clip_end[1] - clip_start[1])
    denominator = cross_2d(segment, clip)
    if abs(denominator) < 1e-12:
        return segment_end
    offset = (
        clip_start[0] - segment_start[0],
        clip_start[1] - segment_start[1],
    )
    amount = cross_2d(offset, clip) / denominator
    return (
        segment_start[0] + amount * segment[0],
        segment_start[1] + amount * segment[1],
    )


def convex_intersection_area(subject: list[Vec2], clip: list[Vec2], tolerance: float = 1e-9) -> float:
    result = subject
    orientation = 1 if signed_polygon_area(clip) >= 0 else -1
    for index, clip_start in enumerate(clip):
        clip_end = clip[(index + 1) % len(clip)]
        source = result
        result = []
        if not source:
            return 0

        def inside(point: Vec2) -> bool:
            edge = (clip_end[0] - clip_start[0], clip_end[1] - clip_start[1])
            relative = (point[0] - clip_start[0], point[1] - clip_start[1])
            return orientation * cross_2d(edge, relative) >= -tolerance

        previous = source[-1]
        previous_inside = inside(previous)
        for current in source:
            current_inside = inside(current)
            if current_inside != previous_inside:
                result.append(intersect_lines(previous, current, clip_start, clip_end))
            if current_inside:
                result.append(current)
            previous = current
            previous_inside = current_inside
    return abs(signed_polygon_area(result)) if len(result) >= 3 else 0


def projected_face(face: Face, normal: Vec3) -> tuple[list[Vec2], float]:
    dropped_axis = max(range(3), key=lambda axis: abs(normal[axis]))
    retained = [axis for axis in range(3) if axis != dropped_axis]
    return (
        [(vertex[retained[0]], vertex[retained[1]]) for vertex in face.vertices],
        abs(normal[dropped_axis]),
    )


def find_coplanar_overlaps(
    faces: list[Face],
    plane_tolerance: float = 1e-5,
    normal_tolerance: float = 1e-6,
    area_tolerance: float = 1e-4,
) -> list[CoplanarOverlap]:
    overlaps: list[CoplanarOverlap] = []
    for first_index, first in enumerate(faces):
        first_normal = face_normal(first)
        for second in faces[first_index + 1:]:
            if first.element == second.element and first.source == second.source:
                continue
            second_normal = face_normal(second)
            if dot(first_normal, second_normal) < 1 - normal_tolerance:
                continue
            distances = [
                abs(dot(first_normal, sub(vertex, first.vertices[0])))
                for vertex in second.vertices
            ]
            plane_distance = max(distances)
            if plane_distance > plane_tolerance:
                continue
            first_2d, projection_scale = projected_face(first, first_normal)
            second_2d, _ = projected_face(second, first_normal)
            overlap_area = convex_intersection_area(first_2d, second_2d) / projection_scale
            if overlap_area <= area_tolerance:
                continue
            overlaps.append(CoplanarOverlap(
                first.element,
                first.surface,
                second.element,
                second.surface,
                overlap_area,
                plane_distance,
            ))
    return sorted(
        overlaps,
        key=lambda overlap: (
            overlap.first_element,
            overlap.first_surface,
            overlap.second_element,
            overlap.second_surface,
        ),
    )


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


def lerp(left: float, right: float, amount: float) -> float:
    return left + (right - left) * amount


def sample_animation_pose(data: dict, animation_code: str, frame: float) -> dict[str, dict[str, Vec3]]:
    animation = next(
        (
            candidate
            for candidate in data.get("animations", [])
            if candidate.get("code") == animation_code or candidate.get("name") == animation_code
        ),
        None,
    )
    if animation is None:
        raise ValueError(f"Animation '{animation_code}' was not found in the shape.")

    quantity = int(animation["quantityframes"])
    if quantity <= 0:
        raise ValueError(f"Animation '{animation_code}' has an invalid quantityframes value.")
    frame %= quantity
    keyframes = sorted(animation.get("keyframes", []), key=lambda keyframe: int(keyframe["frame"]))
    channels = {
        "offset": ("offsetX", "offsetY", "offsetZ"),
        "rotation": ("rotationX", "rotationY", "rotationZ"),
        "stretch": ("stretchX", "stretchY", "stretchZ"),
        "origin": ("originX", "originY", "originZ"),
    }
    defaults = {
        "offset": (0.0, 0.0, 0.0),
        "rotation": (0.0, 0.0, 0.0),
        "stretch": (1.0, 1.0, 1.0),
        "origin": (0.0, 0.0, 0.0),
    }
    element_names = {
        name
        for keyframe in keyframes
        for name in keyframe.get("elements", {})
    }
    poses: dict[str, dict[str, Vec3]] = {}
    for name in element_names:
        pose: dict[str, Vec3] = {}
        for channel, properties in channels.items():
            keyed = []
            for keyframe in keyframes:
                definition = keyframe.get("elements", {}).get(name)
                if definition is not None and any(prop in definition for prop in properties):
                    keyed.append((
                        int(keyframe["frame"]),
                        tuple(float(definition.get(prop, defaults[channel][index])) for index, prop in enumerate(properties)),
                    ))
            if not keyed:
                continue

            right_index = next((index for index, (at, _) in enumerate(keyed) if at > frame), 0)
            right_frame, right_value = keyed[right_index]
            left_frame, left_value = keyed[(right_index - 1) % len(keyed)]
            if len(keyed) == 1:
                amount = 0.0
            elif right_frame <= left_frame:
                distance = right_frame + quantity - left_frame
                amount = ((frame - left_frame) % quantity) / distance
            else:
                amount = (frame - left_frame) / (right_frame - left_frame)
            pose[channel] = tuple(
                lerp(left_value[index], right_value[index], amount)
                for index in range(3)
            )  # type: ignore[assignment]
        poses[name] = pose
    return poses


def load_shape(
    path: Path,
    animation_code: str | None = None,
    animation_frame: float = 0,
) -> tuple[list[Face], dict[str, str]]:
    data = json.loads(path.read_text(encoding="utf-8"))
    faces: list[Face] = []
    texture_width = float(data.get("textureWidth", 16))
    texture_height = float(data.get("textureHeight", 16))
    poses = sample_animation_pose(data, animation_code, animation_frame) if animation_code else {}

    def identity(point: Vec3) -> Vec3:
        return point

    def visit(elements: list[dict], parent_transform=identity) -> None:
        for element in elements:
            start = tuple(element["from"])
            end = tuple(element["to"])
            vertices = cuboid(start, end)
            pose = poses.get(element.get("name", ""), {})
            angles = (
                float(element.get("rotationX", 0)),
                float(element.get("rotationY", 0)),
                float(element.get("rotationZ", 0)),
            )
            origin = tuple(element.get("rotationOrigin", (8, 8, 8)))
            if angles != (0, 0, 0):
                vertices = [rotate(vertex, origin, angles) for vertex in vertices]
            animation_origin = pose.get("origin", origin)
            animation_angles = pose.get("rotation", (0.0, 0.0, 0.0))
            animation_stretch = pose.get("stretch", (1.0, 1.0, 1.0))
            animation_offset = pose.get("offset", (0.0, 0.0, 0.0))
            if animation_stretch != (1.0, 1.0, 1.0):
                vertices = [
                    add(
                        tuple(
                            (vertex[index] - animation_origin[index]) * animation_stretch[index]
                            for index in range(3)
                        ),
                        animation_origin,
                    )
                    for vertex in vertices
                ]
            if animation_angles != (0.0, 0.0, 0.0):
                vertices = [rotate(vertex, animation_origin, animation_angles) for vertex in vertices]
            if animation_offset != (0.0, 0.0, 0.0):
                vertices = [add(vertex, animation_offset) for vertex in vertices]
            vertices = [parent_transform(vertex) for vertex in vertices]
            for direction, definition in element.get("faces", {}).items():
                indices = FACE_INDICES.get(direction)
                if indices and definition.get("enabled", True):
                    faces.append(Face(
                        [vertices[index] for index in indices],
                        str(definition.get("texture", "#missing")).lstrip("#"),
                        element.get("name", "unnamed"),
                        face_uvs(definition, texture_width, texture_height, direction, start, end),
                        direction,
                        str(path),
                    ))

            def child_transform(
                point: Vec3,
                *,
                start=start,
                origin=origin,
                angles=angles,
                animation_origin=animation_origin,
                animation_angles=animation_angles,
                animation_stretch=animation_stretch,
                animation_offset=animation_offset,
            ) -> Vec3:
                point_in_parent = add(point, start)
                if angles != (0, 0, 0):
                    point_in_parent = rotate(point_in_parent, origin, angles)
                if animation_stretch != (1.0, 1.0, 1.0):
                    point_in_parent = add(
                        tuple(
                            (point_in_parent[index] - animation_origin[index]) * animation_stretch[index]
                            for index in range(3)
                        ),
                        animation_origin,
                    )
                if animation_angles != (0.0, 0.0, 0.0):
                    point_in_parent = rotate(point_in_parent, animation_origin, animation_angles)
                if animation_offset != (0.0, 0.0, 0.0):
                    point_in_parent = add(point_in_parent, animation_offset)
                return parent_transform(point_in_parent)

            visit(element.get("children", []), child_transform)

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
        spoke_inset = value("SpokeDepthInset")
        spoke_inner = value("HubOuterRadius") * 0.92
        spoke_outer = value("FelloeInnerRadius") + 0.02 * 16
        for index in range(8):
            add_rotated_cuboid(
                faces,
                (wheel_min + spoke_inset, 8 - spoke_half, 8 + spoke_inner),
                (wheel_max - spoke_inset, 8 + spoke_half, 8 + spoke_outer),
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
    marker_raise = 0.006 * 16
    plate_gap = min(marker_raise * 2, plate_thickness * 0.4)
    add_annulus(faces, wheel_max + plate_gap, wheel_max + plate_gap + plate_thickness,
                shaft_radius, plate_radius, "metal", "FrontCouplingPlate")
    add_annulus(faces, wheel_min - plate_gap - plate_thickness, wheel_min - plate_gap,
                shaft_radius, plate_radius, "metal", "BackCouplingPlate")

    marker_half = (0.025 if size == "compact" else 0.04) * 16
    marker_outer = wheel_radius + marker_raise * 2
    marker_uvs = [(0, 1), (0, 0), (1, 0), (1, 1)]

    def mark_face(x: float, inner: float, outer: float, front: bool, element: str) -> Face:
        if front:
            vertices = [
                (x, 8 - marker_half, 8 + inner),
                (x, 8 + marker_half, 8 + inner),
                (x, 8 + marker_half, 8 + outer),
                (x, 8 - marker_half, 8 + outer),
            ]
        else:
            vertices = [
                (x, 8 - marker_half, 8 + inner),
                (x, 8 - marker_half, 8 + outer),
                (x, 8 + marker_half, 8 + outer),
                (x, 8 + marker_half, 8 + inner),
            ]
        return Face(vertices, "chalk", element, marker_uvs)

    start_radius = wheel_radius * 0.18
    bearing_radius = value("BearingOuterRadius")
    bearing_front = 8 + value("BearingHalfThickness") + marker_raise
    bearing_back = 8 - value("BearingHalfThickness") - marker_raise
    plate_front = wheel_max + plate_gap + plate_thickness + marker_raise
    plate_back = wheel_min - plate_gap - plate_thickness - marker_raise

    faces.extend([
        mark_face(bearing_front, start_radius, bearing_radius, True, "RegistrationMarkBearingFront"),
        mark_face(plate_front, bearing_radius, plate_radius, True, "RegistrationMarkPlateFront"),
        mark_face(wheel_max + marker_raise, plate_radius, marker_outer, True, "RegistrationMarkFaceFront"),
        mark_face(bearing_back, start_radius, bearing_radius, False, "RegistrationMarkBearingBack"),
        mark_face(plate_back, bearing_radius, plate_radius, False, "RegistrationMarkPlateBack"),
        mark_face(wheel_min - marker_raise, plate_radius, marker_outer, False, "RegistrationMarkFaceBack"),
    ])
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
    projected = [(dot(sub(vertex, center), right), dot(sub(vertex, center), up)) for vertex in vertices]
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


def animation_sample_positions(quantity: int, output_fps: int, source_fps: int) -> list[float]:
    samples_per_cycle = round(quantity * output_fps / source_fps)
    if samples_per_cycle <= 0:
        raise ValueError("Animation sampling produced no output frames.")
    return [
        sample * source_fps / output_fps
        for sample in range(samples_per_cycle)
    ]


def turntable_frame_count(fps: int, duration_seconds: float) -> int:
    total_frames = round(fps * duration_seconds)
    if total_frames <= 0:
        raise ValueError("Turntable sampling produced no output frames.")
    return total_frames


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


def screen_point(vertex: Vec3, center: Vec3, right: Vec3, up: Vec3, size: int, scale: float) -> Vec2:
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
        target[visible] = sampled[visible]
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
    ordered = sorted(faces, key=lambda face: sum(dot(sub(v, center), view) for v in face.vertices) / len(face.vertices))

    if mode == "wireframe":
        for face in ordered:
            normal = normalize(cross(sub(face.vertices[1], face.vertices[0]), sub(face.vertices[2], face.vertices[0])))
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
            face_uvs = face.uvs or [(0, 1), (0, 0), (1, 0), (1, 1)]
            for index in range(1, len(points) - 1):
                triangle = [points[0], points[index], points[index + 1]]
                triangle_depths = [vertex_depths[0], vertex_depths[index], vertex_depths[index + 1]]
                triangle_uvs = [face_uvs[0], face_uvs[index], face_uvs[index + 1]]
                rasterize_triangle(
                    pixels,
                    depths,
                    triangle,
                    triangle_depths,
                    fill,
                    texture,
                    triangle_uvs,
                    brightness,
                )
            visible_faces.append((points, vertex_depths))

        for points, vertex_depths in visible_faces:
            for index, start in enumerate(points):
                end_index = (index + 1) % len(points)
                rasterize_edge(
                    pixels,
                    depths,
                    start,
                    points[end_index],
                    vertex_depths[index],
                    vertex_depths[end_index],
                    (16, 18, 20),
                    line_width,
                )
        image = Image.fromarray(pixels)
        draw = ImageDraw.Draw(image)

    label = label_override or f"{mode.upper()} / {view_name.upper()}"
    label_width = max(164, 12 + len(label) * 7)
    draw.rounded_rectangle((12, 12, label_width, 42), radius=7, fill=(8, 10, 12), outline=(87, 94, 99))
    draw.text((22, 20), label, fill=(235, 238, 240), font=ImageFont.load_default())
    image.save(output)


def render_animation(
    shape_path: Path,
    animation_code: str,
    colors: dict[str, tuple[int, int, int]],
    textures: dict[str, Image.Image | None],
    view_name: str,
    output: Path,
    size: int,
    fps: int,
    source_fps: int,
    cycles: int,
    orbit: bool,
) -> dict:
    data = json.loads(shape_path.read_text(encoding="utf-8"))
    animation = next(
        (
            candidate
            for candidate in data.get("animations", [])
            if candidate.get("code") == animation_code or candidate.get("name") == animation_code
        ),
        None,
    )
    if animation is None:
        raise ValueError(f"Animation '{animation_code}' was not found in {shape_path}.")
    quantity = int(animation["quantityframes"])
    source_positions = animation_sample_positions(quantity, fps, source_fps)
    sampled_cycle_faces = [
        load_shape(shape_path, animation_code, source_position)[0]
        for source_position in source_positions
    ]
    samples_per_cycle = len(sampled_cycle_faces)
    total_frames = samples_per_cycle * cycles
    if orbit:
        frame_faces = [
            sampled_cycle_faces[frame % samples_per_cycle]
            for frame in range(total_frames)
        ]
        base_view = VIEWS[view_name][0]
        views = [
            rotate_view_around_y(base_view, frame / total_frames)
            for frame in range(total_frames)
        ]
    else:
        frame_faces = sampled_cycle_faces
        views = [VIEWS[view_name][0]] * samples_per_cycle
    projections = fixed_animation_projections(frame_faces, views, size)
    frame_directory = output.parent / f"{output.stem}-frames"
    frame_directory.mkdir(parents=True, exist_ok=True)
    for frame, (faces, frame_projection) in enumerate(zip(frame_faces, projections)):
        camera_label = f"ORBIT {360 * frame / total_frames:06.2f} DEG" if orbit else view_name.upper()
        source_position = source_positions[frame % samples_per_cycle]
        render(
            faces,
            colors,
            textures,
            view_name,
            "textured",
            frame_directory / f"{frame:04d}.png",
            size,
            frame_projection,
            f"TEXTURED / {camera_label} / {animation_code.upper()} / {source_position:05.2f}",
        )

    ffmpeg_command = [
        "ffmpeg",
        "-hide_banner",
        "-loglevel", "error",
        "-y",
    ]
    if not orbit:
        ffmpeg_command.extend(["-stream_loop", str(max(0, cycles - 1))])
    ffmpeg_command.extend([
        "-framerate", str(fps),
        "-i", str(frame_directory / "%04d.png"),
        "-frames:v", str(total_frames),
        "-c:v", "libx264",
        "-pix_fmt", "yuv420p",
        "-movflags", "+faststart",
        str(output),
    ])
    subprocess.run(ffmpeg_command, check=True)
    return {
        "animation": animation_code,
        "sourceFrameCount": quantity,
        "sourceFramesPerSecond": source_fps,
        "cycles": cycles,
        "videoFrameCount": total_frames,
        "framesPerSecond": fps,
        "durationSeconds": total_frames / fps,
        "view": view_name,
        "cameraMotion": "orbit-360" if orbit else "fixed",
        "cameraRevolutions": 1 if orbit else 0,
        "output": str(output),
        "sha256": sha256(output),
    }


def render_turntable(
    faces: list[Face],
    colors: dict[str, tuple[int, int, int]],
    textures: dict[str, Image.Image | None],
    view_name: str,
    output: Path,
    size: int,
    fps: int,
    duration_seconds: float,
) -> dict:
    total_frames = turntable_frame_count(fps, duration_seconds)
    frame_faces = [faces] * total_frames
    base_view = VIEWS[view_name][0]
    views = [
        rotate_view_around_y(base_view, frame / total_frames)
        for frame in range(total_frames)
    ]
    projections = fixed_animation_projections(frame_faces, views, size)
    frame_directory = output.parent / f"{output.stem}-frames"
    frame_directory.mkdir(parents=True, exist_ok=True)
    for frame, frame_projection in enumerate(projections):
        angle = 360 * frame / total_frames
        render(
            faces,
            colors,
            textures,
            view_name,
            "textured",
            frame_directory / f"{frame:04d}.png",
            size,
            frame_projection,
            f"TEXTURED / ORBIT {angle:06.2f} DEG / STATIC",
        )

    subprocess.run([
        "ffmpeg",
        "-hide_banner",
        "-loglevel", "error",
        "-y",
        "-framerate", str(fps),
        "-i", str(frame_directory / "%04d.png"),
        "-frames:v", str(total_frames),
        "-c:v", "libx264",
        "-pix_fmt", "yuv420p",
        "-movflags", "+faststart",
        str(output),
    ], check=True)
    return {
        "pose": "authored-rest-pose",
        "videoFrameCount": total_frames,
        "framesPerSecond": fps,
        "durationSeconds": duration_seconds,
        "view": view_name,
        "cameraMotion": "orbit-360",
        "cameraRevolutions": 1,
        "output": str(output),
        "sha256": sha256(output),
    }


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
    parser.add_argument("--fail-on-coplanar-overlap", action="store_true")
    parser.add_argument("--animation")
    parser.add_argument("--animation-output", type=Path)
    parser.add_argument("--animation-view", choices=VIEWS, default="isometric")
    parser.add_argument("--animation-fps", type=int, default=30)
    parser.add_argument("--animation-source-fps", type=int, default=30)
    parser.add_argument("--animation-cycles", type=int, default=3)
    parser.add_argument("--animation-orbit", action="store_true")
    parser.add_argument("--turntable-output", type=Path)
    parser.add_argument("--turntable-view", choices=VIEWS, default="isometric")
    parser.add_argument("--turntable-fps", type=int, default=60)
    parser.add_argument("--turntable-seconds", type=float, default=12)
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
    overlaps = find_coplanar_overlaps(faces)

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

    animation_metadata = None
    if args.animation:
        shape_specs = manifest.get("shapes", [])
        if len(shape_specs) != 1 or "proceduralFlywheel" in manifest:
            raise ValueError("Animation rendering currently requires a manifest with exactly one JSON shape.")
        if args.animation_fps <= 0 or args.animation_source_fps <= 0 or args.animation_cycles <= 0:
            raise ValueError("Animation output FPS, source FPS, and cycles must be positive.")
        animation_output = (
            args.animation_output.resolve()
            if args.animation_output
            else output / f"{args.animation}.mp4"
        )
        animation_output.parent.mkdir(parents=True, exist_ok=True)
        animation_metadata = render_animation(
            (base / shape_specs[0]).resolve(),
            args.animation,
            colors,
            texture_images,
            args.animation_view,
            animation_output,
            args.size,
            args.animation_fps,
            args.animation_source_fps,
            args.animation_cycles,
            args.animation_orbit,
        )

    turntable_metadata = None
    if args.turntable_output:
        if args.turntable_fps <= 0 or args.turntable_seconds <= 0:
            raise ValueError("Turntable FPS and duration must be positive.")
        turntable_output = args.turntable_output.resolve()
        turntable_output.parent.mkdir(parents=True, exist_ok=True)
        turntable_metadata = render_turntable(
            faces,
            colors,
            texture_images,
            args.turntable_view,
            turntable_output,
            args.size,
            args.turntable_fps,
            args.turntable_seconds,
        )

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
        "coplanarOverlapCount": len(overlaps),
        "coplanarOverlaps": [
            {
                "first": {
                    "element": overlap.first_element,
                    "surface": overlap.first_surface,
                },
                "second": {
                    "element": overlap.second_element,
                    "surface": overlap.second_surface,
                },
                "overlapAreaModelUnitsSquared": overlap.overlap_area,
                "planeDistanceModelUnits": overlap.plane_distance,
            }
            for overlap in overlaps
        ],
        "animationVideo": animation_metadata,
        "turntableVideo": turntable_metadata,
    }
    (output / "render-metadata.json").write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
    if overlaps:
        print(f"Detected {len(overlaps)} same-facing coplanar overlap(s).")
        for overlap in overlaps:
            print(
                f"  {overlap.first_element}/{overlap.first_surface} overlaps "
                f"{overlap.second_element}/{overlap.second_surface} by "
                f"{overlap.overlap_area:.6f} model-unit^2"
            )
    print(f"Rendered {metadata['name']} to {output}")
    if overlaps and args.fail_on_coplanar_overlap:
        raise SystemExit(2)


if __name__ == "__main__":
    main()

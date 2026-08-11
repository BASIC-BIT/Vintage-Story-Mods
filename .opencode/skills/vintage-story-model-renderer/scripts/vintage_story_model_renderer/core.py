"""Shared geometry types, vector math, and model audits."""

from __future__ import annotations

import math
from dataclasses import dataclass

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
    "reference-hand": (177, 132, 101),
    "reference-cuff": (63, 78, 88),
    "reference-axis-z": (55, 132, 218),
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


def intersect_lines(
    segment_start: Vec2,
    segment_end: Vec2,
    clip_start: Vec2,
    clip_end: Vec2,
) -> Vec2:
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


def convex_intersection_area(
    subject: list[Vec2],
    clip: list[Vec2],
    tolerance: float = 1e-9,
) -> float:
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

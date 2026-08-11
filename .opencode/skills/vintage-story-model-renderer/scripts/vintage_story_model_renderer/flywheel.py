"""Flywheel Power's procedural runtime geometry adapter."""

from __future__ import annotations

import math
import re
from pathlib import Path

from .core import FACE_INDICES, Face, Vec2, Vec3, cuboid, rotate


def constants(path: Path) -> dict[str, float]:
    matches = re.findall(
        r"internal const (?:float|int)\s+(\w+)\s*=\s*([0-9.]+)f?;",
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
            Face(
                [
                    vertex(max_x, inner_radius, a0),
                    vertex(max_x, inner_radius, a1),
                    vertex(max_x, outer_radius, a1),
                    vertex(max_x, outer_radius, a0),
                ],
                material,
                element,
                [
                    disc_uv(inner_radius, a0),
                    disc_uv(inner_radius, a1),
                    disc_uv(outer_radius, a1),
                    disc_uv(outer_radius, a0),
                ],
            ),
            Face(
                [
                    vertex(min_x, inner_radius, a0),
                    vertex(min_x, outer_radius, a0),
                    vertex(min_x, outer_radius, a1),
                    vertex(min_x, inner_radius, a1),
                ],
                material,
                element,
                [
                    disc_uv(inner_radius, a0),
                    disc_uv(outer_radius, a0),
                    disc_uv(outer_radius, a1),
                    disc_uv(inner_radius, a1),
                ],
            ),
            Face(
                [
                    vertex(min_x, outer_radius, a0),
                    vertex(max_x, outer_radius, a0),
                    vertex(max_x, outer_radius, a1),
                    vertex(min_x, outer_radius, a1),
                ],
                material,
                element,
                [(t0, 1), (t0, 0), (t1, 0), (t1, 1)],
            ),
        ])
        if inner_radius:
            faces.append(Face(
                [
                    vertex(max_x, inner_radius, a0),
                    vertex(min_x, inner_radius, a0),
                    vertex(min_x, inner_radius, a1),
                    vertex(max_x, inner_radius, a1),
                ],
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
        spoke_count = int(values["SpokeCount"])
        if spoke_count <= 0:
            raise ValueError("SpokeCount must be positive.")
        spoke_half = value("SpokeHalfWidth")
        spoke_inset = value("SpokeDepthInset")
        spoke_inner = value("HubOuterRadius") * 0.92
        spoke_outer = value("FelloeInnerRadius") + 0.02 * 16
        for index in range(spoke_count):
            add_rotated_cuboid(
                faces,
                (wheel_min + spoke_inset, 8 - spoke_half, 8 + spoke_inner),
                (wheel_max - spoke_inset, 8 + spoke_half, 8 + spoke_outer),
                "wood",
                f"RuntimeWoodSpoke{index}",
                index * 360 / spoke_count,
            )
        add_annulus(
            faces,
            wheel_min,
            wheel_max,
            value("FelloeInnerRadius"),
            value("FelloeOuterRadius"),
            "wood",
            "RuntimeWoodFelloe",
        )
        add_annulus(
            faces,
            wheel_min,
            wheel_max,
            value("TyreInnerRadius"),
            wheel_radius,
            "wheel",
            "RuntimeOuterTyre",
        )
    add_annulus(
        faces,
        8 - value("BearingHalfThickness"),
        8 + value("BearingHalfThickness"),
        value("ShaftClearanceRadius"),
        value("BearingOuterRadius"),
        "bearing",
        "BearingCollar",
        48,
    )
    add_annulus(
        faces,
        8 - value("HubHalfThickness"),
        8 + value("HubHalfThickness"),
        value("BearingOuterRadius"),
        value("HubOuterRadius"),
        "metal",
        "Hub",
    )
    plate_thickness = value("CouplingPlateThickness")
    plate_radius = value("CouplingPlateOuterRadius")
    shaft_radius = value("ShaftClearanceRadius")
    marker_raise = 0.006 * 16
    plate_gap = min(marker_raise * 2, plate_thickness * 0.4)
    add_annulus(
        faces,
        wheel_max + plate_gap,
        wheel_max + plate_gap + plate_thickness,
        shaft_radius,
        plate_radius,
        "metal",
        "FrontCouplingPlate",
    )
    add_annulus(
        faces,
        wheel_min - plate_gap - plate_thickness,
        wheel_min - plate_gap,
        shaft_radius,
        plate_radius,
        "metal",
        "BackCouplingPlate",
    )

    marker_half = (0.025 if size == "compact" else 0.04) * 16
    marker_outer = wheel_radius + marker_raise * 2
    texture_units = 0.72 * 16

    def planar_uvs(vertices: list[Vec3]) -> list[tuple[float, float]]:
        tile_y = math.floor(min(vertex[1] for vertex in vertices) / texture_units)
        tile_z = math.floor(min(vertex[2] for vertex in vertices) / texture_units)
        return [
            (vertex[1] / texture_units - tile_y, vertex[2] / texture_units - tile_z)
            for vertex in vertices
        ]

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
        return Face(vertices, "chalk", element, planar_uvs(vertices))

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

    rim_vertices = [
        rim(wheel_min - marker_raise * 2, -half_angle),
        rim(wheel_max + marker_raise * 2, -half_angle),
        rim(wheel_max + marker_raise * 2, half_angle),
        rim(wheel_min - marker_raise * 2, half_angle),
    ]
    rim_u = 2 * marker_half / texture_units
    rim_v = (2 * wheel_half + marker_raise * 4) / texture_units
    faces.append(Face(
        rim_vertices,
        "chalk",
        "RegistrationMarkRim",
        [(0, 0), (0, rim_v), (rim_u, rim_v), (rim_u, 0)],
    ))
    return faces

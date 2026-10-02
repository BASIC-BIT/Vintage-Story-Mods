"""Flywheel Power's procedural runtime geometry adapter."""

from __future__ import annotations

import math
import re
from pathlib import Path

from .core import FACE_INDICES, Face, Vec2, Vec3, cuboid, rotate


def constants(path: Path) -> dict[str, float]:
    matches = re.findall(
        r"(?:internal|private) const (?:float|int)\s+(\w+)\s*=\s*([0-9.]+)f?;",
        path.read_text(encoding="utf-8"),
    )
    return {name: float(value) for name, value in matches}


def planar_uvs(vertices: list[Vec3], texture_units: float) -> list[Vec2]:
    tile_y = math.floor(min(vertex[1] for vertex in vertices) / texture_units)
    tile_z = math.floor(min(vertex[2] for vertex in vertices) / texture_units)
    return [
        (vertex[1] / texture_units - tile_y, vertex[2] / texture_units - tile_z)
        for vertex in vertices
    ]


def add_annulus(
    faces: list[Face],
    min_x: float,
    max_x: float,
    inner_radius: float,
    outer_radius: float,
    material: str,
    element: str,
    segments: int,
    radial_steps: int,
    texture_units: float,
) -> None:
    def vertex(x: float, radius: float, angle: float) -> Vec3:
        return (x, 8 + radius * math.sin(angle), 8 + radius * math.cos(angle))

    def add_disc_face(x: float, r0: float, r1: float, a0: float, a1: float) -> None:
        vertices = [
            vertex(x, r0, a0),
            vertex(x, r1, a0),
            vertex(x, r1, a1),
            vertex(x, r0, a1),
        ]
        uvs = planar_uvs(vertices, texture_units)
        order = (0, 3, 2, 1)
        vertices = [vertices[index] for index in order]
        uvs = [uvs[index] for index in order]
        faces.append(Face(vertices, material, element, uvs))

    radius_span = outer_radius - inner_radius
    for radial in range(radial_steps):
        r0 = inner_radius + radius_span * radial / radial_steps
        r1 = inner_radius + radius_span * (radial + 1) / radial_steps
        for segment in range(segments):
            a0 = math.tau * segment / segments
            a1 = math.tau * (segment + 1) / segments
            add_disc_face(max_x, r0, r1, a0, a1)
            add_disc_face(min_x, r0, r1, a1, a0)

    def add_radius_side(radius: float, start_x: float, end_x: float) -> None:
        axial_steps = max(1, math.ceil(abs(end_x - start_x) / texture_units))
        angular_steps = max(1, math.ceil(math.tau * radius / texture_units))
        max_segment_angle = math.tau / segments
        for axial in range(axial_steps):
            x0 = start_x + (end_x - start_x) * axial / axial_steps
            x1 = start_x + (end_x - start_x) * (axial + 1) / axial_steps
            v1 = abs(x1 - x0) / texture_units
            for angular in range(angular_steps):
                cell_a0 = math.tau * angular / angular_steps
                cell_a1 = math.tau * (angular + 1) / angular_steps
                sub_segments = max(1, math.ceil((cell_a1 - cell_a0) / max_segment_angle))
                for sub in range(sub_segments):
                    u0 = sub / sub_segments
                    u1 = (sub + 1) / sub_segments
                    a0 = cell_a0 + (cell_a1 - cell_a0) * u0
                    a1 = cell_a0 + (cell_a1 - cell_a0) * u1
                    vertices = [
                        vertex(x0, radius, a0),
                        vertex(x1, radius, a0),
                        vertex(x1, radius, a1),
                        vertex(x0, radius, a1),
                    ]
                    uvs = [(u0, 0), (u0, v1), (u1, v1), (u1, 0)]
                    faces.append(Face(vertices, material, element, uvs))

    add_radius_side(outer_radius, min_x, max_x)
    if inner_radius:
        add_radius_side(inner_radius, max_x, min_x)


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


def add_spoke(
    faces: list[Face],
    min_x: float,
    max_x: float,
    inner_radius: float,
    outer_radius: float,
    half_width: float,
    material: str,
    element: str,
    angle: float,
) -> None:
    """Mirror FlywheelMechBlockRenderer.AddSpoke geometry and authored UVs."""
    radial_y = math.sin(angle)
    radial_z = math.cos(angle)
    tangent_y = math.cos(angle)
    tangent_z = -math.sin(angle)

    def vertex(x: float, radius: float, tangent: float, uv: Vec2) -> tuple[Vec3, Vec2]:
        return (
            (
                x,
                8 + radius * radial_y + tangent * tangent_y,
                8 + radius * radial_z + tangent * tangent_z,
            ),
            uv,
        )

    f_inner_left = vertex(max_x, inner_radius, -half_width, (0, 0))
    f_inner_right = vertex(max_x, inner_radius, half_width, (1, 0))
    f_outer_right = vertex(max_x, outer_radius, half_width, (1, 1))
    f_outer_left = vertex(max_x, outer_radius, -half_width, (0, 1))
    b_inner_left = vertex(min_x, inner_radius, -half_width, (0, 0))
    b_inner_right = vertex(min_x, inner_radius, half_width, (1, 0))
    b_outer_right = vertex(min_x, outer_radius, half_width, (1, 1))
    b_outer_left = vertex(min_x, outer_radius, -half_width, (0, 1))

    def face(surface: str, *corners: tuple[Vec3, Vec2]) -> None:
        faces.append(Face(
            [corner[0] for corner in corners],
            material,
            element,
            [corner[1] for corner in corners],
            surface=surface,
        ))

    face("front", f_inner_left, f_inner_right, f_outer_right, f_outer_left)
    face("back", b_inner_left, b_outer_left, b_outer_right, b_inner_right)
    face("tangent-positive", f_inner_right, b_inner_right, b_outer_right, f_outer_right)
    face("tangent-negative", f_inner_left, f_outer_left, b_outer_left, b_inner_left)
    face("outer", f_outer_left, f_outer_right, b_outer_right, b_outer_left)
    face("inner", f_inner_left, b_inner_left, b_inner_right, f_inner_right)


def load_flywheel(path: Path, size: str) -> list[Face]:
    values = constants(path)
    renderer_values = constants(path.with_name("FlywheelMechBlockRenderer.cs"))
    prefix = "Compact" if size == "compact" else ""

    def value(name: str) -> float:
        return values[f"{prefix}{name}"] * 16

    faces: list[Face] = []
    wheel_radius = value("WheelOuterRadius")
    wheel_inner = value("CoupledInnerRadius")
    wheel_half = value("WheelHalfThickness")
    wheel_min, wheel_max = 8 - wheel_half, 8 + wheel_half
    wheel_segments = int(renderer_values["WheelSegments"])
    wheel_radial_steps = int(renderer_values["WheelRadialSteps"])
    texture_units = renderer_values["TextureMeters"] * 16
    if size == "compact":
        add_annulus(
            faces, wheel_min, wheel_max, wheel_inner, wheel_radius,
            "wheel", "RuntimeWheel", wheel_segments, wheel_radial_steps, texture_units,
        )
    else:
        spoke_count = int(values["SpokeCount"])
        if spoke_count <= 0:
            raise ValueError("SpokeCount must be positive.")
        spoke_half = value("SpokeHalfWidth")
        spoke_inset = value("SpokeDepthInset")
        spoke_inner = value("HubOuterRadius") * 0.92
        spoke_outer = value("FelloeInnerRadius") + 0.02 * 16
        for index in range(spoke_count):
            add_spoke(
                faces,
                wheel_min + spoke_inset,
                wheel_max - spoke_inset,
                spoke_inner,
                spoke_outer,
                spoke_half,
                "wood",
                f"RuntimeWoodSpoke{index}",
                math.tau * index / spoke_count,
            )
        add_annulus(
            faces,
            wheel_min,
            wheel_max,
            value("FelloeInnerRadius"),
            value("FelloeOuterRadius"),
            "wood",
            "RuntimeWoodFelloe",
            wheel_segments,
            2,
            texture_units,
        )
        add_annulus(
            faces,
            wheel_min,
            wheel_max,
            value("TyreInnerRadius"),
            wheel_radius,
            "wheel",
            "RuntimeOuterTyre",
            wheel_segments,
            2,
            texture_units,
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
        2,
        texture_units,
    )
    add_annulus(
        faces,
        8 - value("HubHalfThickness"),
        8 + value("HubHalfThickness"),
        value("BearingOuterRadius"),
        value("HubOuterRadius"),
        "metal",
        "Hub",
        wheel_segments,
        2,
        texture_units,
    )
    plate_thickness = value("CouplingPlateThickness")
    plate_radius = value("CouplingPlateOuterRadius")
    shaft_radius = value("ShaftClearanceRadius")
    marker_raise = renderer_values["ChalkRaise"] * 16
    plate_gap = min(marker_raise * 2, plate_thickness * 0.4)
    add_annulus(
        faces,
        wheel_max + plate_gap,
        wheel_max + plate_gap + plate_thickness,
        shaft_radius,
        plate_radius,
        "metal",
        "FrontCouplingPlate",
        wheel_segments,
        3,
        texture_units,
    )
    add_annulus(
        faces,
        wheel_min - plate_gap - plate_thickness,
        wheel_min - plate_gap,
        shaft_radius,
        plate_radius,
        "metal",
        "BackCouplingPlate",
        wheel_segments,
        3,
        texture_units,
    )

    marker_half = (0.025 if size == "compact" else 0.04) * 16
    marker_outer = wheel_radius + renderer_values["ChalkEdgeOverlap"] * 16

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
        return Face(vertices, "chalk", element, planar_uvs(vertices, texture_units))

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

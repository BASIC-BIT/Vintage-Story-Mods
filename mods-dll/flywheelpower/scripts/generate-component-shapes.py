#!/usr/bin/env python3
"""Generate distinct cuboid inventory and held models for staged flywheel parts."""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[1]
SHAPE_ROOT = PROJECT_ROOT / "assets" / "flywheelpower" / "shapes" / "item"
CENTER = 8.0
RING_SEGMENTS = 16
DEPTH_STEP = 0.01

FACE_PLANES = {
    "north": (0, 1),
    "east": (2, 1),
    "south": (0, 1),
    "west": (2, 1),
    "up": (0, 2),
    "down": (0, 2),
}


def cuboid(
    name: str,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    material: str,
    rotation_x: float | None = None,
) -> dict:
    spans = tuple(end[index] - start[index] for index in range(3))
    faces = {
        direction: {
            "texture": f"#{material}",
            "uv": [
                0.0,
                0.0,
                round(min(16.0, spans[axes[0]]), 4),
                round(min(16.0, spans[axes[1]]), 4),
            ],
        }
        for direction, axes in FACE_PLANES.items()
    }
    element = {
        "name": name,
        "from": [round(value, 4) for value in start],
        "to": [round(value, 4) for value in end],
        "faces": faces,
    }
    if rotation_x is not None:
        element["rotationOrigin"] = [CENTER, CENTER, CENTER]
        element["rotationX"] = round(rotation_x, 4)
    return element


def ring(
    name: str,
    material: str,
    min_x: float,
    max_x: float,
    inner_radius: float,
    outer_radius: float,
    layer: int,
) -> list[dict]:
    half_tangent = outer_radius * math.tan(math.pi / RING_SEGMENTS) + 0.025
    return [
        cuboid(
            f"{name}{segment}",
            (
                min_x + (segment - 7.5) * DEPTH_STEP + layer * 0.0005,
                CENTER - half_tangent,
                CENTER + inner_radius,
            ),
            (
                max_x + (segment - 7.5) * DEPTH_STEP + layer * 0.0005,
                CENTER + half_tangent,
                CENTER + outer_radius,
            ),
            material,
            segment * 22.5 + layer * 0.05,
        )
        for segment in range(RING_SEGMENTS)
    ]


def spokes(
    name: str,
    count: int,
    material: str,
    min_x: float,
    max_x: float,
    inner_radius: float,
    outer_radius: float,
    half_width: float,
) -> list[dict]:
    return [
        cuboid(
            f"{name}{index}",
            (min_x, CENTER - half_width, CENTER + inner_radius),
            (max_x, CENTER + half_width, CENTER + outer_radius),
            material,
            index * 360 / count,
        )
        for index in range(count)
    ]


def bearing_fitting() -> list[dict]:
    # One forged saddle fitting. Small depth offsets prevent coplanar faces at joints.
    return [
        cuboid("LeftFoot", (2.5, 3.4, 6.40), (5.4, 5.2, 9.60), "metal"),
        cuboid("RightFoot", (10.6, 3.4, 6.42), (13.5, 5.2, 9.62), "metal"),
        cuboid("LeftCheek", (3.6, 4.8, 6.44), (5.2, 10.0, 9.64), "metal"),
        cuboid("RightCheek", (10.8, 4.8, 6.46), (12.4, 10.0, 9.66), "metal"),
        cuboid("Crown", (4.8, 8.9, 6.48), (11.2, 11.0, 9.68), "metal"),
    ]


def bearing_set(compact: bool) -> list[dict]:
    if compact:
        axle_min, axle_max = 2.5, 13.5
        outer_radius = 3.45
        metal_min, metal_max = 5.2, 10.8
        plate_depth = 0.42
    else:
        axle_min, axle_max = 1.5, 14.5
        outer_radius = 4.35
        metal_min, metal_max = 4.25, 11.75
        plate_depth = 0.48

    elements = [cuboid("WoodenAxle", (axle_min, 6.65, 6.65), (axle_max, 9.35, 9.35), "wood")]
    elements.extend(ring("BearingLiner", "bearing", metal_min - 0.2, metal_max + 0.2, 1.95, 2.35, 0))
    elements.extend(ring("HubBody", "metal", metal_min, metal_max, 2.45, outer_radius, 1))
    elements.extend(ring("FrontRetainer", "metal", metal_max + 0.12, metal_max + 0.12 + plate_depth, 1.95, outer_radius + 0.35, 2))
    elements.extend(ring("BackRetainer", "metal", metal_min - 0.12 - plate_depth, metal_min - 0.12, 1.95, outer_radius + 0.35, 3))
    return elements


def timber_web(compact: bool) -> list[dict]:
    if compact:
        elements = ring("InnerBoss", "wood", 6.0, 10.0, 1.35, 2.35, 0)
        elements.extend(spokes("Spoke", 4, "wood", 6.2, 9.8, 2.15, 5.35, 0.72))
        elements.extend(ring("OuterFelloe", "wood", 5.9, 10.1, 5.1, 6.15, 1))
        return elements

    elements = ring("InnerBoss", "wood", 6.15, 9.85, 1.55, 2.65, 0)
    elements.extend(spokes("Spoke", 8, "wood", 6.3, 9.7, 2.4, 5.75, 0.5))
    elements.extend(ring("OuterFelloe", "wood", 6.0, 10.0, 5.5, 6.65, 1))
    return elements


def rim_blank(compact: bool) -> list[dict]:
    if compact:
        return ring("CompactWheelBlank", "rim", 5.8, 10.2, 2.3, 6.45, 0)
    return ring("CurvedTyre", "rim", 6.85, 9.15, 5.55, 7.0, 0)


def shape_text(textures: dict[str, str], elements: list[dict]) -> str:
    payload = {
        "editor": {"allAngles": False, "entityTextureMode": False},
        "textureWidth": 16,
        "textureHeight": 16,
        "textures": textures,
        "elements": elements,
    }
    return json.dumps(payload, indent=2, separators=(",", ": ")) + "\n"


SHAPES = {
    "bearing-fitting.json": (
        {"metal": "game:block/metal/sheet/iron1"},
        bearing_fitting,
    ),
    "flywheel-bearing-full.json": (
        {
            "metal": "game:block/metal/ingot/iron",
            "bearing": "game:block/metal/tarnished/iron-riveted1",
            "wood": "game:block/wood/planks/generic",
        },
        lambda: bearing_set(compact=False),
    ),
    "flywheel-bearing-compact.json": (
        {
            "metal": "game:block/metal/ingot/copper",
            "bearing": "game:block/metal/tarnished/iron-riveted1",
            "wood": "game:block/wood/planks/generic",
        },
        lambda: bearing_set(compact=True),
    ),
    "flywheel-web-full.json": (
        {"wood": "game:block/wood/planks/generic"},
        lambda: timber_web(compact=False),
    ),
    "flywheel-rim-full.json": (
        {"rim": "game:block/metal/ingot/iron"},
        lambda: rim_blank(compact=False),
    ),
    "flywheel-rim-compact.json": (
        {"rim": "game:block/stone/rock/granite1"},
        lambda: rim_blank(compact=True),
    ),
}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="Fail if committed component shapes are stale.")
    args = parser.parse_args()
    SHAPE_ROOT.mkdir(parents=True, exist_ok=True)

    for file_name, (textures, factory) in SHAPES.items():
        path = SHAPE_ROOT / file_name
        expected = shape_text(textures, factory())
        if args.check:
            if not path.exists() or path.read_text(encoding="utf-8") != expected:
                raise SystemExit(f"Generated component shape is stale: {path}")
        else:
            path.write_text(expected, encoding="utf-8")

    action = "Verified" if args.check else "Generated"
    print(f"{action} {len(SHAPES)} staged-component inventory and held shapes")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Generate cuboid-based inventory/held flywheel shapes from runtime dimensions."""

from __future__ import annotations

import argparse
import json
import math
import re
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[1]
DIMENSIONS_SOURCE = PROJECT_ROOT / "src" / "FlywheelModelDimensions.cs"
SHAPE_ROOT = PROJECT_ROOT / "assets" / "flywheelpower" / "shapes" / "block"
CENTER = 8.0
RING_SEGMENTS = 16

FACE_PLANES = {
    "north": (0, 1),
    "east": (2, 1),
    "south": (0, 1),
    "west": (2, 1),
    "up": (0, 2),
    "down": (0, 2),
}


def dimensions() -> dict[str, float]:
    source = DIMENSIONS_SOURCE.read_text(encoding="utf-8")
    matches = re.findall(
        r"internal const (?:float|int)\s+(\w+)\s*=\s*([0-9.]+)f?;",
        source,
    )
    return {name: float(value) for name, value in matches}


def cuboid_element(
    name: str,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    material: str,
    rotation_x: float | None = None,
) -> dict:
    spans = tuple(end[index] - start[index] for index in range(3))
    faces = {}
    for direction, axes in FACE_PLANES.items():
        width = min(16.0, spans[axes[0]])
        height = min(16.0, spans[axes[1]])
        faces[direction] = {
            "texture": f"#{material}",
            "uv": [0.0, 0.0, round(width, 4), round(height, 4)],
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


def ring_elements(
    name: str,
    material: str,
    min_x: float,
    max_x: float,
    inner_radius: float,
    outer_radius: float,
    segments: int = RING_SEGMENTS,
) -> list[dict]:
    midpoint = (inner_radius + outer_radius) / 2
    half_tangent = outer_radius * math.tan(math.pi / segments) + 0.025
    return [
        cuboid_element(
            f"{name}{segment}",
            (min_x, CENTER - half_tangent, CENTER + inner_radius),
            (max_x, CENTER + half_tangent, CENTER + outer_radius),
            material,
            360 * segment / segments,
        )
        for segment in range(segments)
    ]


def spoke_elements(values: dict[str, float], min_x: float, max_x: float) -> list[dict]:
    inner = values["HubOuterRadius"] * 16 * 0.92
    outer = (values["FelloeInnerRadius"] + 0.02) * 16
    half_width = values["SpokeHalfWidth"] * 16
    count = int(values["SpokeCount"])
    return [
        cuboid_element(
            f"WoodSpoke{spoke}",
            (min_x, CENTER - half_width, CENTER + inner),
            (max_x, CENTER + half_width, CENTER + outer),
            "wood",
            360 * spoke / count,
        )
        for spoke in range(count)
    ]


def registration_mark(
    wheel_min: float,
    wheel_max: float,
    radius: float,
    half_width: float,
) -> list[dict]:
    raise_amount = 0.006 * 16
    overlap = 0.012 * 16
    start_radius = radius * 0.18
    return [
        cuboid_element(
            "ChalkLineFront",
            (wheel_max + raise_amount, CENTER - half_width, CENTER + start_radius),
            (wheel_max + raise_amount * 2, CENTER + half_width, CENTER + radius + overlap),
            "chalk",
        ),
        cuboid_element(
            "ChalkLineBack",
            (wheel_min - raise_amount * 2, CENTER - half_width, CENTER + start_radius),
            (wheel_min - raise_amount, CENTER + half_width, CENTER + radius + overlap),
            "chalk",
        ),
        cuboid_element(
            "ChalkLineRim",
            (wheel_min - overlap, CENTER - half_width, CENTER + radius + raise_amount),
            (wheel_max + overlap, CENTER + half_width, CENTER + radius + raise_amount * 2),
            "chalk",
        ),
    ]


def assembly_elements(values: dict[str, float], compact: bool) -> list[dict]:
    prefix = "Compact" if compact else ""

    def value(name: str) -> float:
        return values[f"{prefix}{name}"] * 16

    wheel_radius = value("WheelOuterRadius")
    wheel_half = value("WheelHalfThickness")
    wheel_min, wheel_max = CENTER - wheel_half, CENTER + wheel_half
    elements: list[dict] = []

    if compact:
        elements.extend(
            ring_elements(
                "CompactWheel",
                "wheel",
                wheel_min,
                wheel_max,
                value("CoupledInnerRadius"),
                wheel_radius,
            )
        )
    else:
        elements.extend(spoke_elements(values, wheel_min, wheel_max))
        elements.extend(
            ring_elements(
                "WoodFelloe",
                "wood",
                wheel_min,
                wheel_max,
                value("FelloeInnerRadius"),
                value("FelloeOuterRadius"),
            )
        )
        elements.extend(
            ring_elements(
                "OuterTyre",
                "wheel",
                wheel_min,
                wheel_max,
                value("TyreInnerRadius"),
                wheel_radius,
            )
        )

    shaft_radius = value("ShaftClearanceRadius")
    elements.extend(
        ring_elements(
            "BearingCollar",
            "bearing",
            CENTER - value("BearingHalfThickness"),
            CENTER + value("BearingHalfThickness"),
            shaft_radius,
            value("BearingOuterRadius"),
        )
    )
    elements.extend(
        ring_elements(
            "Hub",
            "metal",
            CENTER - value("HubHalfThickness"),
            CENTER + value("HubHalfThickness"),
            value("BearingOuterRadius"),
            value("HubOuterRadius"),
        )
    )

    plate_thickness = value("CouplingPlateThickness")
    plate_gap = min(0.006 * 16 * 2, plate_thickness * 0.4)
    plate_radius = value("CouplingPlateOuterRadius")
    elements.extend(
        ring_elements(
            "FrontCouplingPlate",
            "metal",
            wheel_max + plate_gap,
            wheel_max + plate_gap + plate_thickness,
            shaft_radius,
            plate_radius,
        )
    )
    elements.extend(
        ring_elements(
            "BackCouplingPlate",
            "metal",
            wheel_min - plate_gap - plate_thickness,
            wheel_min - plate_gap,
            shaft_radius,
            plate_radius,
        )
    )
    chalk_half_width = (0.025 if compact else 0.04) * 16
    elements.extend(registration_mark(wheel_min, wheel_max, wheel_radius, chalk_half_width))
    return elements


def shape_text(elements: list[dict], compact: bool) -> str:
    textures = {
        "wheel": "game:block/metal/ingot/iron",
        "metal": "game:block/metal/ingot/iron",
        "bearing": "game:block/metal/tarnished/iron-riveted1",
        "chalk": "game:block/cloth/wool/red1",
    }
    if not compact:
        textures["wood"] = "game:block/wood/planks/generic"
    lines = [
        "{",
        '  "editor": {"allAngles":false,"entityTextureMode":false},',
        '  "textureWidth": 16,',
        '  "textureHeight": 16,',
        f'  "textures": {json.dumps(textures, separators=(",", ":"))},',
        '  "elements": [',
    ]
    lines.extend(
        f"    {json.dumps(element, separators=(',', ':'))}{',' if index < len(elements) - 1 else ''}"
        for index, element in enumerate(elements)
    )
    lines.extend(["  ]", "}"])
    return "\n".join(lines) + "\n"


def write_shape(path: Path, elements: list[dict], compact: bool, check: bool) -> None:
    expected = shape_text(elements, compact)
    if check:
        if not path.exists() or path.read_text(encoding="utf-8") != expected:
            raise SystemExit(f"Generated preview shape is stale: {path}")
        return
    path.write_text(expected, encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="Fail if generated shapes differ from committed source.")
    args = parser.parse_args()
    values = dimensions()
    write_shape(
        SHAPE_ROOT / "flywheel-wheel-coupled.json",
        assembly_elements(values, compact=False),
        compact=False,
        check=args.check,
    )
    write_shape(
        SHAPE_ROOT / "compact-flywheel-wheel-coupled.json",
        assembly_elements(values, compact=True),
        compact=True,
        check=args.check,
    )
    action = "Verified" if args.check else "Generated"
    print(f"{action} full and compact inventory/held flywheel shapes from FlywheelModelDimensions.cs")


if __name__ == "__main__":
    main()

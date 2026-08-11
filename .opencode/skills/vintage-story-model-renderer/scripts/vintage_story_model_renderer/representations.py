"""Definition-backed collectible transforms and neutral representation references."""

from __future__ import annotations

import fnmatch
import math
import re
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

from .core import FACE_INDICES, Face, Vec3, cuboid
from .jsonio import load_vintage_story_json


TRANSFORM_PROPERTIES = {
    "guiTransform",
    "groundTransform",
    "fpHandTransform",
    "tpHandTransform",
    "tpOffHandTransform",
}


@dataclass(frozen=True)
class CollectibleTransform:
    property: str
    translation: Vec3
    rotation: Vec3
    origin: Vec3
    scale: Vec3
    rotate: bool
    units_per_block: float
    variant_code: str | None = None
    resolution: str = "direct"

    def metadata(self) -> dict:
        result = asdict(self)
        result["unitsPerBlock"] = result.pop("units_per_block")
        result["variantCode"] = result.pop("variant_code")
        return result


def _vector(definition: dict, key: str, default: Vec3) -> Vec3:
    value = definition.get(key, {})
    return tuple(float(value.get(axis, default[index])) for index, axis in enumerate("xyz"))  # type: ignore[return-value]


def _matches_variant(pattern: str, code: str) -> bool:
    if pattern.startswith("@"):
        return re.fullmatch(pattern[1:], code) is not None
    return fnmatch.fnmatchcase(code, pattern)


def _merge_objects(base: dict[str, Any], override: dict[str, Any]) -> dict[str, Any]:
    merged = dict(base)
    for key, value in override.items():
        if isinstance(merged.get(key), dict) and isinstance(value, dict):
            merged[key] = _merge_objects(merged[key], value)
        else:
            merged[key] = value
    return merged


def resolve_collectible_property(
    collectible: dict[str, Any],
    property_name: str,
    variant_code: str | None = None,
) -> tuple[Any | None, str | None]:
    """Resolve one top-level property using RegistryObjectType.solveByType order.

    Vintage Story checks the variant map in authored order, uses the first wildcard
    match, and merges object values into an existing direct property.
    """

    direct = collectible.get(property_name)
    if variant_code is None:
        return direct, "direct" if property_name in collectible else None

    by_type_name = f"{property_name}ByType"
    by_type = collectible.get(by_type_name, {})
    if not isinstance(by_type, dict):
        raise ValueError(f"Collectible property {by_type_name} must be an object.")
    for pattern, value in by_type.items():
        if _matches_variant(pattern, variant_code):
            if isinstance(direct, dict) and isinstance(value, dict):
                value = _merge_objects(direct, value)
            return value, f"{by_type_name}:{pattern}"
    return direct, "direct" if property_name in collectible else None


def load_collectible_transform(
    definition_path: Path,
    property_name: str,
    units_per_block: float = 16.0,
    variant_code: str | None = None,
) -> CollectibleTransform:
    if property_name not in TRANSFORM_PROPERTIES:
        raise ValueError(f"Unsupported collectible transform property: {property_name}")
    if units_per_block <= 0:
        raise ValueError("Collectible transform unitsPerBlock must be positive.")

    collectible = load_vintage_story_json(definition_path)
    definition, resolution = resolve_collectible_property(collectible, property_name, variant_code)
    if definition is None:
        raise ValueError(
            f"Collectible definition {definition_path} does not define {property_name}"
            f" for variant {variant_code or '(unspecified)'}; "
            "implicit game defaults are not inferred."
        )
    uniform_scale = float(definition.get("scale", 1.0))
    scale = _vector(definition, "scaleXYZ", (uniform_scale, uniform_scale, uniform_scale))
    return CollectibleTransform(
        property=property_name,
        translation=_vector(definition, "translation", (0.0, 0.0, 0.0)),
        rotation=_vector(definition, "rotation", (0.0, 0.0, 0.0)),
        origin=_vector(definition, "origin", (0.5, 0.5, 0.5)),
        scale=scale,
        rotate=bool(definition.get("rotate", True)),
        units_per_block=float(units_per_block),
        variant_code=variant_code,
        resolution=resolution or "direct",
    )


def transform_point(point: Vec3, transform: CollectibleTransform) -> Vec3:
    """Apply ModelTransformNoDefaults.AsMatrix semantics in model units.

    Vintage Story builds T(translation) T(origin) Rx Ry Rz S T(-origin).
    With column vectors the point operations therefore run from right to left.
    """

    units = transform.units_per_block
    origin = tuple(value * units for value in transform.origin)
    translation = tuple(value * units for value in transform.translation)
    x, y, z = (point[index] - origin[index] for index in range(3))
    x, y, z = x * transform.scale[0], y * transform.scale[1], z * transform.scale[2]

    rz = math.radians(transform.rotation[2])
    x, y = x * math.cos(rz) - y * math.sin(rz), x * math.sin(rz) + y * math.cos(rz)
    ry = math.radians(transform.rotation[1])
    x, z = x * math.cos(ry) + z * math.sin(ry), -x * math.sin(ry) + z * math.cos(ry)
    rx = math.radians(transform.rotation[0])
    y, z = y * math.cos(rx) - z * math.sin(rx), y * math.sin(rx) + z * math.cos(rx)

    return tuple(
        value + origin[index] + translation[index]
        for index, value in enumerate((x, y, z))
    )  # type: ignore[return-value]


def transform_faces(faces: list[Face], transform: CollectibleTransform) -> list[Face]:
    return [
        Face(
            [transform_point(vertex, transform) for vertex in face.vertices],
            face.material,
            face.element,
            face.uvs,
            face.surface,
            face.source,
        )
        for face in faces
    ]


def _cuboid_faces(start: Vec3, end: Vec3, material: str, element: str) -> list[Face]:
    vertices = cuboid(start, end)
    return [
        Face(
            [vertices[index] for index in indices],
            material,
            element,
            surface=direction,
            source="representation-reference",
        )
        for direction, indices in FACE_INDICES.items()
    ]


def grip_reference_faces(transform: CollectibleTransform) -> list[Face]:
    """Build a neutral palm/cuff proxy around the transformed collectible pivot.

    This is deliberately not a Seraph hand or runtime attachment simulation. It gives
    transform reviews a stable size, orientation, and grip-point reference.
    """

    units = transform.units_per_block
    pivot = transform_point(tuple(value * units for value in transform.origin), transform)

    def around(minimum: Vec3, maximum: Vec3, material: str, element: str) -> list[Face]:
        return _cuboid_faces(
            tuple(pivot[index] + minimum[index] for index in range(3)),
            tuple(pivot[index] + maximum[index] for index in range(3)),
            material,
            element,
        )

    return [
        *around((-1.8, -0.7, -1.15), (1.8, 0.7, 1.15), "reference-hand", "grip-proxy-palm"),
        *around((-0.9, -4.0, -0.85), (0.9, -0.7, 0.85), "reference-cuff", "grip-proxy-cuff"),
        *around((-0.13, -0.13, -2.4), (0.13, 0.13, 2.4), "reference-axis-z", "grip-axis-z"),
    ]

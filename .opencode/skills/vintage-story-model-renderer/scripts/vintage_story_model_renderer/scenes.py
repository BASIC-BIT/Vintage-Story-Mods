"""Seraph scene composition for definition-backed held-item evidence."""

from __future__ import annotations

import math
from pathlib import Path

from .core import Face, Vec3
from .representations import CollectibleTransform
from .shapes import AttachmentPose, load_shape_scene


def resolve_shape_asset(location: str, roots: list[Path]) -> Path | None:
    domain, relative = location.split(":", 1) if ":" in location else ("game", location)
    relative = relative.removeprefix("shapes/")
    if not relative.endswith(".json"):
        relative += ".json"
    for root in roots:
        for candidate in (root / domain / "shapes" / relative, root / "shapes" / relative):
            if candidate.exists():
                return candidate
    return None


def select_attachment(attachments: list[AttachmentPose], code: str) -> AttachmentPose:
    matches = [attachment for attachment in attachments if attachment.code == code]
    if len(matches) != 1:
        raise ValueError(f"Expected exactly one attachment point named {code}, found {len(matches)}.")
    return matches[0]


def _rotate_model_matrix_order(point: Vec3, angles: Vec3) -> Vec3:
    """Apply Rz, then Ry, then Rx, matching the engine's chained matrix calls."""

    x, y, z = point
    rz = math.radians(angles[2])
    x, y = x * math.cos(rz) - y * math.sin(rz), x * math.sin(rz) + y * math.cos(rz)
    ry = math.radians(angles[1])
    x, z = x * math.cos(ry) + z * math.sin(ry), -x * math.sin(ry) + z * math.cos(ry)
    rx = math.radians(angles[0])
    y, z = y * math.cos(rx) - z * math.sin(rx), y * math.sin(rx) + z * math.cos(rx)
    return x, y, z


def compose_held_point(
    point: Vec3,
    transform: CollectibleTransform,
    attachment: AttachmentPose,
) -> Vec3:
    """Reproduce ItemFishingPole.LoadHeldItemModelMatrix in model units.

    The decompiled game chain is Anim * T(origin) * S *
    T(attachment/16 + translation) * Rx * Ry * Rz * T(-origin).
    """

    units = transform.units_per_block
    point_blocks = tuple(value / units for value in point)
    relative = tuple(point_blocks[index] - transform.origin[index] for index in range(3))
    combined_rotation = tuple(
        attachment.rotation[index] + transform.rotation[index]
        for index in range(3)
    )
    rotated = _rotate_model_matrix_order(relative, combined_rotation)
    translated = tuple(
        rotated[index] + attachment.position[index] / units + transform.translation[index]
        for index in range(3)
    )
    scaled = tuple(translated[index] * transform.scale[index] for index in range(3))
    local_model_units = tuple(
        (scaled[index] + transform.origin[index]) * units
        for index in range(3)
    )
    return attachment.transform(local_model_units)


def compose_held_faces(
    faces: list[Face],
    transform: CollectibleTransform,
    attachment: AttachmentPose,
) -> list[Face]:
    return [
        Face(
            [compose_held_point(vertex, transform, attachment) for vertex in face.vertices],
            face.material,
            face.element,
            face.uvs,
            face.surface,
            face.source,
            face.texture_key,
        )
        for face in faces
    ]


def load_seraph_held_frame(
    seraph_shape: Path,
    item_faces: list[Face],
    transform: CollectibleTransform,
    attachment_code: str,
    animation_code: str,
    animation_frame: float,
) -> tuple[list[Face], dict[str, str], AttachmentPose, int]:
    seraph_faces, seraph_textures, attachments = load_shape_scene(
        seraph_shape,
        animation_code,
        animation_frame,
    )
    attachment = select_attachment(attachments, attachment_code)
    held_faces = compose_held_faces(item_faces, transform, attachment)
    return seraph_faces + held_faces, seraph_textures, attachment, len(seraph_faces)

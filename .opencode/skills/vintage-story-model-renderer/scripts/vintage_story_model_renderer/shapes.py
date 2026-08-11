"""Vintage Story cuboid-shape and authored-animation loading."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Callable

from .core import FACE_INDICES, Face, Vec2, Vec3, add, cuboid, rotate
from .jsonio import load_vintage_story_json


@dataclass(frozen=True)
class AttachmentPose:
    code: str
    element: str
    element_path: str
    position: Vec3
    rotation: Vec3
    transform: Callable[[Vec3], Vec3]


def face_uvs(
    definition: dict,
    texture_width: float,
    texture_height: float,
    direction: str,
    start: Vec3,
    end: Vec3,
) -> list[Vec2]:
    spans = tuple(end[index] - start[index] for index in range(3))
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


def sample_animation_pose(
    data: dict,
    animation_code: str,
    frame: float,
) -> dict[str, dict[str, Vec3]]:
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
                        tuple(
                            float(definition.get(prop, defaults[channel][index]))
                            for index, prop in enumerate(properties)
                        ),
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


def resolve_internal_step_parents(elements: list[dict]) -> list[dict]:
    """Attach same-shape stepParentName elements to their named parent element."""

    entries: list[tuple[dict, list[dict]]] = []

    def collect(children: list[dict]) -> None:
        for element in list(children):
            entries.append((element, children))
            collect(element.get("children", []))

    collect(elements)
    by_name: dict[str, list[dict]] = {}
    for element, _ in entries:
        by_name.setdefault(str(element.get("name", "")), []).append(element)

    for element, current_parent in entries:
        parent_name = element.get("stepParentName")
        if not parent_name:
            continue
        matches = by_name.get(str(parent_name), [])
        if len(matches) != 1:
            raise ValueError(
                f"stepParentName '{parent_name}' for {element.get('name')} resolved to {len(matches)} elements."
            )
        target_children = matches[0].setdefault("children", [])
        if current_parent is target_children:
            continue
        current_parent.remove(element)
        target_children.append(element)
    return elements


def load_shape(
    path: Path,
    animation_code: str | None = None,
    animation_frame: float = 0,
) -> tuple[list[Face], dict[str, str]]:
    faces, textures, _ = load_shape_scene(path, animation_code, animation_frame)
    return faces, textures


def load_shape_scene(
    path: Path,
    animation_code: str | None = None,
    animation_frame: float = 0,
) -> tuple[list[Face], dict[str, str], list[AttachmentPose]]:
    data = load_vintage_story_json(path)
    faces: list[Face] = []
    attachments: list[AttachmentPose] = []
    texture_width = float(data.get("textureWidth", 16))
    texture_height = float(data.get("textureHeight", 16))
    poses = sample_animation_pose(data, animation_code, animation_frame) if animation_code else {}

    def identity(point: Vec3) -> Vec3:
        return point

    def visit(elements: list[dict], parent_transform=identity, parent_path: str = "") -> None:
        for element in elements:
            element_name = element.get("name", "unnamed")
            element_path = f"{parent_path}/{element_name}" if parent_path else element_name
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
                        element_name,
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

            for definition in element.get("attachmentpoints", []):
                attachments.append(AttachmentPose(
                    str(definition["code"]),
                    element_name,
                    element_path,
                    tuple(float(definition.get(f"pos{axis}", 0)) for axis in "XYZ"),  # type: ignore[arg-type]
                    tuple(float(definition.get(f"rotation{axis}", 0)) for axis in "XYZ"),  # type: ignore[arg-type]
                    child_transform,
                ))

            visit(element.get("children", []), child_transform, element_path)

    visit(resolve_internal_step_parents(data.get("elements", [])))
    return faces, dict(data.get("textures", {})), attachments

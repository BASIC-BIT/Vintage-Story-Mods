"""Command-line orchestration for deterministic model evidence."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image

from .core import RENDER_MODES, VIEWS, Face, find_coplanar_overlaps
from .flywheel import load_flywheel
from .jsonio import load_vintage_story_json
from .representations import (
    grip_reference_faces,
    load_collectible_transform,
    resolve_collectible_property,
    transform_faces,
)
from .rendering import average_color, render, resolve_texture
from .scenes import load_seraph_held_frame, resolve_shape_asset
from .shapes import load_shape
from .video import (
    contact_sheet,
    render_animation,
    render_seraph_held_animation,
    render_turntable,
    sha256,
)


def build_parser() -> argparse.ArgumentParser:
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
    return parser


def main(argv: list[str] | None = None) -> None:
    args = build_parser().parse_args(argv)

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
    item_faces = list(faces)

    collectible_transform = None
    reference_faces: list[Face] = []
    held_scene_metadata = None
    seraph_shape_path = None
    seraph_animation = None
    seraph_attachment = None
    seraph_face_count = 0
    if "seraphHeldScene" in manifest and "collectibleTransform" in manifest:
        raise ValueError("Use either seraphHeldScene or collectibleTransform, not both.")
    if "seraphHeldScene" in manifest:
        spec = manifest["seraphHeldScene"]
        definition_path = (base / spec["collectibleDefinition"]).resolve()
        transform_property = spec.get("transformProperty", "tpHandTransform")
        variant_code = spec.get("variantCode")
        collectible_transform = load_collectible_transform(
            definition_path,
            transform_property,
            float(spec.get("unitsPerBlock", 16)),
            variant_code,
        )
        seraph_shape_path = resolve_shape_asset(
            spec.get("seraphShape", "game:entity/humanoid/seraph-hairless"),
            roots,
        )
        if seraph_shape_path is None:
            raise ValueError(f"Could not resolve Seraph shape {spec.get('seraphShape')} from the asset roots.")
        collectible_definition = load_vintage_story_json(definition_path)
        seraph_animation = spec.get("animation")
        animation_source = "manifest"
        if not seraph_animation:
            seraph_animation, animation_resolution = resolve_collectible_property(
                collectible_definition,
                "heldRightTpIdleAnimation",
                variant_code,
            )
            if seraph_animation:
                animation_source = f"collectible-definition:{animation_resolution}"
        if not seraph_animation:
            seraph_animation, animation_resolution = resolve_collectible_property(
                collectible_definition,
                "heldTpIdleAnimation",
                variant_code,
            )
            if seraph_animation:
                animation_source = f"collectible-definition:{animation_resolution}"
        if not seraph_animation:
            seraph_animation = "idle1"
            animation_source = "player-default-idle"
        seraph_attachment = spec.get("attachment", "RightHand")
        animation_frame = float(spec.get("animationFrame", 0))
        faces, seraph_textures, attachment_pose, seraph_face_count = load_seraph_held_frame(
            seraph_shape_path,
            item_faces,
            collectible_transform,
            seraph_attachment,
            seraph_animation,
            animation_frame,
        )
        textures.update(seraph_textures)
        if "seraphTexture" in spec:
            textures["seraph"] = spec["seraphTexture"]
        inputs.extend([definition_path, seraph_shape_path])
        held_scene_metadata = {
            "type": "seraph-held-item",
            "attachment": seraph_attachment,
            "attachmentElement": attachment_pose.element,
            "attachmentElementPath": attachment_pose.element_path,
            "attachmentPosition": attachment_pose.position,
            "attachmentRotation": attachment_pose.rotation,
            "animation": seraph_animation,
            "animationFrame": animation_frame,
            "animationSelection": animation_source,
            "holdMode": (
                "authored-two-hand-pose"
                if seraph_animation.startswith("holdbothhands")
                else "single-hand-attachment"
            ),
            "variantCode": variant_code,
            "matrixParity": "ItemFishingPole.LoadHeldItemModelMatrix",
            "runtimeParity": "single-animation geometry and attachment matrix",
            "limitations": [
                "No runtime animation blending or easing.",
                "No wearable or skinnable-part shape composition.",
                "Two-hand poses align the support hand through authored animation, not a second item constraint.",
                "No first-person camera, shader, or arm-only render pass.",
            ],
        }
    if "collectibleTransform" in manifest:
        spec = manifest["collectibleTransform"]
        definition_path = (base / spec["definition"]).resolve()
        collectible_transform = load_collectible_transform(
            definition_path,
            spec["property"],
            float(spec.get("unitsPerBlock", 16)),
            spec.get("variantCode"),
        )
        faces = transform_faces(faces, collectible_transform)
        inputs.append(definition_path)
        if spec.get("reference") == "grip-proxy":
            if collectible_transform.property not in {"fpHandTransform", "tpHandTransform", "tpOffHandTransform"}:
                raise ValueError("grip-proxy is only valid for first- or third-person hand transforms.")
            reference_faces = grip_reference_faces(collectible_transform)
        elif spec.get("reference") not in {None, "none"}:
            raise ValueError(f"Unsupported collectible transform reference: {spec['reference']}")

    render_faces = faces + reference_faces

    resolved: dict[str, str | None] = {}
    colors: dict[str, tuple[int, int, int]] = {}
    texture_images: dict[str, Image.Image | None] = {}
    for material in sorted({face.material for face in render_faces}):
        if material.startswith("reference-"):
            resolved[material] = "builtin:representation-reference-color"
            colors[material] = average_color(None, material)
            texture_images[material] = None
            continue
        texture = resolve_texture(textures.get(material, ""), roots) if textures.get(material) else None
        resolved[material] = str(texture) if texture else None
        colors[material] = average_color(texture, material, textures.get(material, ""))
        texture_images[material] = Image.open(texture).convert("RGBA") if texture else None

    all_images: list[Path] = []
    for mode in RENDER_MODES:
        mode_output = output / mode
        mode_output.mkdir(parents=True, exist_ok=True)
        mode_images = []
        for view_name in VIEWS:
            path = mode_output / f"{view_name}.png"
            label = None
            if held_scene_metadata:
                label = (
                    f"{mode.upper()} / {view_name.upper()} / SERAPH "
                    f"{seraph_animation} / {seraph_attachment}"
                )
            elif collectible_transform:
                label = f"{mode.upper()} / {view_name.upper()} / {collectible_transform.property}"
            render(render_faces, colors, texture_images, view_name, mode, path, args.size, label_override=label)
            mode_images.append(path)
            all_images.append(path)
        contact_sheet(mode_images, mode_output / "contact-sheet.png", 4, 2, args.size)
    contact_sheet(all_images, output / "contact-sheet.png", len(VIEWS), len(RENDER_MODES), args.size)

    animation_metadata = None
    if args.animation:
        if args.animation_fps <= 0 or args.animation_source_fps <= 0 or args.animation_cycles <= 0:
            raise ValueError("Animation output FPS, source FPS, and cycles must be positive.")
        animation_output = (
            args.animation_output.resolve()
            if args.animation_output
            else output / f"{args.animation}.mp4"
        )
        animation_output.parent.mkdir(parents=True, exist_ok=True)
        if held_scene_metadata:
            animation_metadata = render_seraph_held_animation(
                seraph_shape_path,
                item_faces,
                collectible_transform,
                seraph_attachment,
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
        else:
            shape_specs = manifest.get("shapes", [])
            if len(shape_specs) != 1 or "proceduralFlywheel" in manifest:
                raise ValueError("Animation rendering currently requires a manifest with exactly one JSON shape.")
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
        "itemFaceCount": len(item_faces),
        "seraphFaceCount": seraph_face_count,
        "referenceFaceCount": len(reference_faces),
        "collectibleTransform": collectible_transform.metadata() if collectible_transform else None,
        "heldScene": held_scene_metadata,
        "representationReference": (
            {
                "type": "neutral-grip-proxy",
                "runtimeParity": False,
                "limitation": "Not a Seraph hand, animation, or attachment-anchor simulation.",
            }
            if reference_faces
            else None
        ),
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
    (output / "render-metadata.json").write_text(
        json.dumps(metadata, indent=2) + "\n",
        encoding="utf-8",
    )
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

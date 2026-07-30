"""Command-line orchestration for deterministic model evidence."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image

from .core import RENDER_MODES, VIEWS, Face, find_coplanar_overlaps
from .flywheel import load_flywheel
from .rendering import average_color, render, resolve_texture
from .shapes import load_shape
from .video import contact_sheet, render_animation, render_turntable, sha256


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


def main() -> None:
    args = build_parser().parse_args()

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

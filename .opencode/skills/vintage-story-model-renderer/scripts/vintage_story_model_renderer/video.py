"""Animation sampling, camera turntables, and video encoding."""

from __future__ import annotations

import hashlib
import subprocess
from pathlib import Path

from PIL import Image

from .core import VIEWS, Face, Vec3
from .jsonio import load_vintage_story_json
from .representations import CollectibleTransform, transform_faces
from .rendering import fixed_animation_projections, orbit_view, render
from .scenes import load_seraph_held_frame
from .shapes import load_shape


def animation_sample_positions(
    quantity: int,
    output_fps: int,
    source_fps: int,
) -> list[float]:
    samples_per_cycle = round(quantity * output_fps / source_fps)
    if samples_per_cycle <= 0:
        raise ValueError("Animation sampling produced no output frames.")
    return [
        sample * source_fps / output_fps
        for sample in range(samples_per_cycle)
    ]


def turntable_frame_count(fps: int, duration_seconds: float) -> int:
    total_frames = round(fps * duration_seconds)
    if total_frames <= 0:
        raise ValueError("Turntable sampling produced no output frames.")
    return total_frames


def prepare_frame_directory(output: Path) -> Path:
    frame_directory = output.parent / f"{output.stem}-frames"
    frame_directory.mkdir(parents=True, exist_ok=True)
    for existing in frame_directory.glob("*.png"):
        if existing.stem.isdecimal():
            existing.unlink()
    return frame_directory


def render_animation(
    shape_path: Path,
    animation_code: str,
    colors: dict[str, tuple[int, int, int]],
    textures: dict[str, Image.Image | None],
    view_name: str,
    output: Path,
    size: int,
    fps: int,
    source_fps: int,
    cycles: int,
    orbit: bool,
    transform: CollectibleTransform | None = None,
) -> dict:
    data = load_vintage_story_json(shape_path)
    animation = next(
        (
            candidate
            for candidate in data.get("animations", [])
            if candidate.get("code") == animation_code or candidate.get("name") == animation_code
        ),
        None,
    )
    if animation is None:
        raise ValueError(f"Animation '{animation_code}' was not found in {shape_path}.")
    quantity = int(animation["quantityframes"])
    source_positions = animation_sample_positions(quantity, fps, source_fps)
    sampled_cycle_faces = []
    for source_position in source_positions:
        frame = load_shape(shape_path, animation_code, source_position)[0]
        sampled_cycle_faces.append(transform_faces(frame, transform) if transform else frame)
    samples_per_cycle = len(sampled_cycle_faces)
    total_frames = samples_per_cycle * cycles
    if orbit:
        frame_faces = [
            sampled_cycle_faces[frame % samples_per_cycle]
            for frame in range(total_frames)
        ]
        base_view = VIEWS[view_name][0]
        views = [
            orbit_view(base_view, frame / total_frames)
            for frame in range(total_frames)
        ]
    else:
        frame_faces = sampled_cycle_faces
        views = [VIEWS[view_name][0]] * samples_per_cycle
    projections = fixed_animation_projections(
        frame_faces,
        views,
        size,
        [VIEWS[view_name][1]] * len(views),
    )
    frame_directory = prepare_frame_directory(output)
    for frame, (faces, frame_projection) in enumerate(zip(frame_faces, projections)):
        camera_label = f"ORBIT {360 * frame / total_frames:06.2f} DEG" if orbit else view_name.upper()
        source_position = source_positions[frame % samples_per_cycle]
        render(
            faces,
            colors,
            textures,
            view_name,
            "textured",
            frame_directory / f"{frame:04d}.png",
            size,
            frame_projection,
            f"TEXTURED / {camera_label} / {animation_code.upper()} / {source_position:05.2f}",
        )

    ffmpeg_command = [
        "ffmpeg",
        "-hide_banner",
        "-loglevel",
        "error",
        "-y",
    ]
    if not orbit:
        ffmpeg_command.extend(["-stream_loop", str(max(0, cycles - 1))])
    ffmpeg_command.extend([
        "-framerate",
        str(fps),
        "-i",
        str(frame_directory / "%04d.png"),
        "-frames:v",
        str(total_frames),
        "-c:v",
        "libx264",
        "-pix_fmt",
        "yuv420p",
        "-movflags",
        "+faststart",
        str(output),
    ])
    subprocess.run(ffmpeg_command, check=True)
    return {
        "animation": animation_code,
        "sourceFrameCount": quantity,
        "sourceFramesPerSecond": source_fps,
        "cycles": cycles,
        "videoFrameCount": total_frames,
        "framesPerSecond": fps,
        "durationSeconds": total_frames / fps,
        "view": view_name,
        "cameraMotion": "orbit-360" if orbit else "fixed",
        "cameraRevolutions": 1 if orbit else 0,
        "output": str(output),
        "sha256": sha256(output),
    }


def render_seraph_held_animation(
    seraph_shape: Path,
    item_faces: list[Face],
    transform: CollectibleTransform,
    attachment_code: str,
    animation_code: str,
    colors: dict[str, tuple[int, int, int]],
    textures: dict[str, Image.Image | None],
    view_name: str,
    output: Path,
    size: int,
    fps: int,
    source_fps: int,
    cycles: int,
    orbit: bool,
) -> dict:
    data = load_vintage_story_json(seraph_shape)
    animation = next(
        (
            candidate
            for candidate in data.get("animations", [])
            if candidate.get("code") == animation_code or candidate.get("name") == animation_code
        ),
        None,
    )
    if animation is None:
        raise ValueError(f"Animation '{animation_code}' was not found in {seraph_shape}.")
    quantity = int(animation["quantityframes"])
    source_positions = animation_sample_positions(quantity, fps, source_fps)
    sampled_cycle_faces = [
        load_seraph_held_frame(
            seraph_shape,
            item_faces,
            transform,
            attachment_code,
            animation_code,
            source_position,
        )[0]
        for source_position in source_positions
    ]
    samples_per_cycle = len(sampled_cycle_faces)
    total_frames = samples_per_cycle * cycles
    frame_faces = [
        sampled_cycle_faces[frame % samples_per_cycle]
        for frame in range(total_frames if orbit else samples_per_cycle)
    ]
    base_view = VIEWS[view_name][0]
    views = (
        [orbit_view(base_view, frame / total_frames) for frame in range(total_frames)]
        if orbit
        else [base_view] * samples_per_cycle
    )
    projections = fixed_animation_projections(
        frame_faces,
        views,
        size,
        [VIEWS[view_name][1]] * len(views),
    )
    frame_directory = prepare_frame_directory(output)
    for frame, (faces, frame_projection) in enumerate(zip(frame_faces, projections)):
        camera_label = f"ORBIT {360 * frame / total_frames:06.2f} DEG" if orbit else view_name.upper()
        source_position = source_positions[frame % samples_per_cycle]
        render(
            faces,
            colors,
            textures,
            view_name,
            "textured",
            frame_directory / f"{frame:04d}.png",
            size,
            frame_projection,
            f"TEXTURED / {camera_label} / SERAPH {animation_code.upper()} / {source_position:05.2f}",
        )

    ffmpeg_command = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y"]
    if not orbit:
        ffmpeg_command.extend(["-stream_loop", str(max(0, cycles - 1))])
    ffmpeg_command.extend([
        "-framerate", str(fps),
        "-i", str(frame_directory / "%04d.png"),
        "-frames:v", str(total_frames),
        "-c:v", "libx264",
        "-pix_fmt", "yuv420p",
        "-movflags", "+faststart",
        str(output),
    ])
    subprocess.run(ffmpeg_command, check=True)
    return {
        "animation": animation_code,
        "attachment": attachment_code,
        "sourceFrameCount": quantity,
        "sourceFramesPerSecond": source_fps,
        "cycles": cycles,
        "videoFrameCount": total_frames,
        "framesPerSecond": fps,
        "durationSeconds": total_frames / fps,
        "view": view_name,
        "cameraMotion": "orbit-360" if orbit else "fixed",
        "cameraRevolutions": 1 if orbit else 0,
        "scene": "seraph-held-item",
        "output": str(output),
        "sha256": sha256(output),
    }


def render_turntable(
    faces: list[Face],
    colors: dict[str, tuple[int, int, int]],
    textures: dict[str, Image.Image | None],
    view_name: str,
    output: Path,
    size: int,
    fps: int,
    duration_seconds: float,
) -> dict:
    total_frames = turntable_frame_count(fps, duration_seconds)
    frame_faces = [faces] * total_frames
    base_view = VIEWS[view_name][0]
    views = [
        orbit_view(base_view, frame / total_frames)
        for frame in range(total_frames)
    ]
    projections = fixed_animation_projections(
        frame_faces,
        views,
        size,
        [VIEWS[view_name][1]] * len(views),
    )
    frame_directory = prepare_frame_directory(output)
    for frame, frame_projection in enumerate(projections):
        angle = 360 * frame / total_frames
        render(
            faces,
            colors,
            textures,
            view_name,
            "textured",
            frame_directory / f"{frame:04d}.png",
            size,
            frame_projection,
            f"TEXTURED / ORBIT {angle:06.2f} DEG / STATIC",
        )

    subprocess.run([
        "ffmpeg",
        "-hide_banner",
        "-loglevel",
        "error",
        "-y",
        "-framerate",
        str(fps),
        "-i",
        str(frame_directory / "%04d.png"),
        "-frames:v",
        str(total_frames),
        "-c:v",
        "libx264",
        "-pix_fmt",
        "yuv420p",
        "-movflags",
        "+faststart",
        str(output),
    ], check=True)
    return {
        "pose": "authored-rest-pose",
        "videoFrameCount": total_frames,
        "framesPerSecond": fps,
        "durationSeconds": duration_seconds,
        "view": view_name,
        "cameraMotion": "orbit-360",
        "cameraRevolutions": 1,
        "output": str(output),
        "sha256": sha256(output),
    }


def contact_sheet(
    paths: list[Path],
    output: Path,
    columns: int,
    rows: int,
    size: int,
) -> None:
    sheet = Image.new("RGB", (size * columns, size * rows), (20, 22, 24))
    for index, path in enumerate(paths):
        sheet.paste(Image.open(path), ((index % columns) * size, (index // columns) * size))
    sheet.save(output)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()

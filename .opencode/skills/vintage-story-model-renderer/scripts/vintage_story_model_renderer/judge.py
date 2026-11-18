"""Opt-in LLM review of deterministic model-renderer evidence."""

from __future__ import annotations

import argparse
import base64
import json
import mimetypes
import os
import urllib.error
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .video import sha256


DEFAULT_MODEL = "gemini-3.1-pro-preview"
MAX_INLINE_BYTES = 90 * 1024 * 1024
OBJECTIVE_CATEGORIES = {
    "missing-face",
    "clipping",
    "z-fighting",
    "gap",
    "floating-geometry",
    "uv",
    "camera-motion",
    "animation-loop",
    "frozen-video",
}
VALID_SEVERITIES = {"info", "warning", "error"}


@dataclass(frozen=True)
class MediaArtifact:
    path: Path
    mime_type: str
    sha256: str
    byte_count: int

    def manifest_entry(self) -> dict[str, Any]:
        return {
            "path": str(self.path),
            "mimeType": self.mime_type,
            "sha256": self.sha256,
            "byteCount": self.byte_count,
        }


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Prepare or execute an advisory LLM review of renderer evidence."
    )
    parser.add_argument("--evidence-dir", type=Path, required=True)
    parser.add_argument("--video", type=Path)
    parser.add_argument("--rubric", type=Path)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--model", default=DEFAULT_MODEL)
    parser.add_argument(
        "--execute",
        action="store_true",
        help="Send evidence to Gemini. Without this flag, only an offline request manifest is written.",
    )
    return parser


def media_artifact(path: Path) -> MediaArtifact:
    path = path.resolve()
    if not path.is_file():
        raise ValueError(f"Media artifact not found: {path}")
    mime_type = mimetypes.guess_type(path.name)[0]
    if mime_type not in {"image/png", "image/jpeg", "video/mp4"}:
        raise ValueError(f"Unsupported judge media type for {path}: {mime_type}")
    size = path.stat().st_size
    if size > MAX_INLINE_BYTES:
        raise ValueError(
            f"{path.name} is {size} bytes; inline judging is limited to "
            f"{MAX_INLINE_BYTES} bytes. Use a shorter/bounded clip."
        )
    return MediaArtifact(path, mime_type, sha256(path), size)


def default_rubric_path() -> Path:
    return Path(__file__).resolve().parents[2] / "rubrics" / "model-visual-review.md"


def build_prompt(
    metadata: dict[str, Any],
    rubric: str,
    media: list[MediaArtifact],
) -> str:
    media_lines = "\n".join(
        f"- {artifact.path.name}: {artifact.mime_type}, sha256={artifact.sha256}"
        for artifact in media
    )
    deterministic = {
        "name": metadata.get("name"),
        "representation": metadata.get("representation"),
        "faceCount": metadata.get("faceCount"),
        "boundsModelUnits": metadata.get("boundsModelUnits"),
        "renderedImageCount": metadata.get("renderedImageCount"),
        "coplanarOverlapCount": metadata.get("coplanarOverlapCount"),
        "unresolvedTextures": metadata.get("unresolvedTextures"),
    }
    return f"""You are an adversarial visual reviewer for a deterministic Vintage Story model render.
Review only the supplied contact sheet and optional video. Do not claim in-game approval, runtime registration,
lighting parity, collision correctness, or human taste approval. Distinguish a likely source-model defect from a likely
renderer artifact. Cite the exact view name or video timestamp for every finding. A still-image defect that is not visible
must not be invented from the rubric.

First perform a neutral inventory of what is visibly present without referring to the expected result. Then apply the
rubric. Treat the deterministic metadata as provenance and coverage, not as proof that the image looks correct.

Media:
{media_lines}

Deterministic metadata:
{json.dumps(deterministic, indent=2)}

Rubric:
{rubric.strip()}

Return strict JSON only:
{{
  "neutralDescription": "what is visibly present, without success criteria",
  "findings": [
    {{
      "category": "missing-face|clipping|z-fighting|gap|floating-geometry|uv|camera-motion|animation-loop|frozen-video|proportion|construction|other",
      "severity": "info|warning|error",
      "source": "model|renderer|uncertain",
      "evidence": "specific view name or MM:SS timestamp and visible observation",
      "recommendation": "smallest useful follow-up"
    }}
  ],
  "verdict": "pass|needs-review|fail",
  "summary": "short bounded conclusion"
}}

Use fail for a clear objective visual defect, needs-review for ambiguity or subjective construction/taste, and pass only
when no warning/error finding remains. Never silently reinterpret a warning or error as pass."""


def prepare_request(
    evidence_dir: Path,
    video: Path | None,
    rubric_path: Path | None,
    model: str,
) -> tuple[dict[str, Any], list[MediaArtifact], str]:
    evidence_dir = evidence_dir.resolve()
    metadata_path = evidence_dir / "render-metadata.json"
    contact_sheet_path = evidence_dir / "contact-sheet.png"
    if not metadata_path.is_file():
        raise ValueError(f"Renderer metadata not found: {metadata_path}")
    metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
    if metadata.get("renderedImageCount") != 24:
        raise ValueError("Visual judging requires the complete 24-image renderer evidence set.")
    rubric_path = (rubric_path or default_rubric_path()).resolve()
    if not rubric_path.is_file():
        raise ValueError(f"Visual review rubric not found: {rubric_path}")
    media = [media_artifact(contact_sheet_path)]
    if video:
        media.append(media_artifact(video))
    prompt = build_prompt(
        metadata,
        rubric_path.read_text(encoding="utf-8"),
        media,
    )
    request = {
        "schemaVersion": 1,
        "provider": "gemini",
        "model": model,
        "executed": False,
        "advisoryOnly": True,
        "evidenceDirectory": str(evidence_dir),
        "metadata": {
            "path": str(metadata_path),
            "sha256": sha256(metadata_path),
        },
        "rubric": {
            "path": str(rubric_path),
            "sha256": sha256(rubric_path),
        },
        "media": [artifact.manifest_entry() for artifact in media],
        "prompt": prompt,
    }
    return request, media, prompt


def parse_judgment(text: str) -> dict[str, Any]:
    try:
        judgment = json.loads(text)
    except json.JSONDecodeError as exc:
        raise ValueError(f"Gemini returned invalid JSON: {exc}") from exc
    if not isinstance(judgment.get("neutralDescription"), str):
        raise ValueError("Judgment is missing neutralDescription.")
    findings = judgment.get("findings")
    if not isinstance(findings, list):
        raise ValueError("Judgment findings must be an array.")
    normalized_findings = []
    for index, finding in enumerate(findings):
        if not isinstance(finding, dict):
            raise ValueError(f"Finding {index} is not an object.")
        severity = str(finding.get("severity", "")).lower()
        if severity not in VALID_SEVERITIES:
            raise ValueError(f"Finding {index} has invalid severity: {severity}")
        for key in ("category", "source", "evidence", "recommendation"):
            if not isinstance(finding.get(key), str) or not finding[key].strip():
                raise ValueError(f"Finding {index} is missing {key}.")
        normalized_findings.append({**finding, "severity": severity})
    judgment["findings"] = normalized_findings
    severities = {finding["severity"] for finding in normalized_findings}
    if "error" in severities:
        judgment["verdict"] = "fail"
    elif "warning" in severities:
        judgment["verdict"] = "needs-review"
    else:
        judgment["verdict"] = "pass"
    judgment["objectiveDefectCount"] = sum(
        1
        for finding in normalized_findings
        if finding["severity"] == "error"
        and finding["category"] in OBJECTIVE_CATEGORIES
    )
    return judgment


def execute_gemini(
    model: str,
    media: list[MediaArtifact],
    prompt: str,
    api_key: str,
) -> dict[str, Any]:
    parts: list[dict[str, Any]] = []
    for artifact in media:
        parts.append(
            {
                "inline_data": {
                    "mime_type": artifact.mime_type,
                    "data": base64.b64encode(artifact.path.read_bytes()).decode("ascii"),
                }
            }
        )
    parts.append({"text": prompt})
    payload = {
        "contents": [{"role": "user", "parts": parts}],
        "generationConfig": {
            "temperature": 0.1,
            "responseMimeType": "application/json",
        },
    }
    request = urllib.request.Request(
        f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent",
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "Content-Type": "application/json",
            "x-goog-api-key": api_key,
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=300) as response:
            body = json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")[:600]
        raise RuntimeError(f"Gemini HTTP {exc.code}: {detail}") from exc
    candidates = body.get("candidates", [])
    if not candidates:
        raise RuntimeError("Gemini returned no candidates.")
    text = "".join(
        part.get("text", "")
        for part in candidates[0].get("content", {}).get("parts", [])
    )
    return parse_judgment(text)


def main(argv: list[str] | None = None) -> None:
    args = build_parser().parse_args(argv)
    request, media, prompt = prepare_request(
        args.evidence_dir,
        args.video,
        args.rubric,
        args.model,
    )
    if args.execute:
        api_key = os.environ.get("GEMINI_API_KEY", "").strip()
        if not api_key:
            raise SystemExit("--execute requires GEMINI_API_KEY in the process environment.")
        request["executed"] = True
        request["judgment"] = execute_gemini(args.model, media, prompt, api_key)
    args.out.resolve().parent.mkdir(parents=True, exist_ok=True)
    args.out.resolve().write_text(json.dumps(request, indent=2) + "\n", encoding="utf-8")
    print(
        f"{'Executed' if args.execute else 'Prepared'} advisory visual review: "
        f"{args.out.resolve()}"
    )
    if args.execute and request["judgment"]["verdict"] != "pass":
        raise SystemExit(2)

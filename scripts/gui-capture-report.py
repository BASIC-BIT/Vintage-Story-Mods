#!/usr/bin/env python3
"""Compare explicitly reviewed GUI captures. No rendering or baseline promotion.

Each input directory owns captures.json (schema 1, sourceTreeHash, coverage,
captures: [{scenario, file, manifest}]). manifest is a relative JSON filename
or an inline object. See validate_capture for required provenance fields.
Exit 0: compatible evidence; 1: missing/stale evidence; 2: invalid invocation.
Added/changed/removed images are review findings, never automatic approvals.
"""
import argparse
from collections import Counter
import hashlib
import html
import json
import math
from pathlib import Path
import re
import struct
import sys
import zlib

PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
MAX_DIMENSION = 4096
MAX_PNG_BYTES = 128 * 1024 * 1024
ENVIRONMENT_FIELDS = ("viewport", "locale", "scale", "fonts", "gameBinaries", "gameAssets", "nativeGuide")
SCENE_FIELDS = ("scale", "previewTime", "fixture", "coverage", "omissions")
COVERAGE = ("layout-only", "native-client")


def chunk(kind, data):
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))


def encode_png(width, height, pixels):
    rows = b"".join(b"\0" + pixels[y * width * 4:(y + 1) * width * 4] for y in range(height))
    return PNG_SIGNATURE + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b"")


def decode_png(path):
    """Bounded 8-bit, noninterlaced RGB/RGBA PNG, including all five row filters."""
    if path.stat().st_size > MAX_PNG_BYTES:
        raise ValueError("PNG exceeds size limit")
    data = path.read_bytes()
    if not data.startswith(PNG_SIGNATURE):
        raise ValueError("Invalid PNG signature")
    offset, header, compressed, ended = 8, None, bytearray(), False
    seen_palette, image_started, image_ended = False, False, False
    while offset < len(data):
        if offset + 12 > len(data):
            raise ValueError("Truncated PNG chunk")
        size = struct.unpack_from(">I", data, offset)[0]
        kind = data[offset + 4:offset + 8]
        if not re.fullmatch(rb"[A-Za-z]{4}", kind) or kind[2] & 32:
            raise ValueError("Invalid PNG chunk name")
        end = offset + size + 12
        if end > len(data):
            raise ValueError("Truncated PNG data")
        payload = data[offset + 8:end - 4]
        if zlib.crc32(kind + payload) != struct.unpack_from(">I", data, end - 4)[0]:
            raise ValueError("PNG CRC mismatch")
        if header is None and kind != b"IHDR":
            raise ValueError("PNG must start with IHDR")
        if kind == b"IHDR":
            if header is not None or size != 13:
                raise ValueError("Invalid PNG header")
            header = struct.unpack(">IIBBBBB", payload)
            width, height, depth, color, compression, filtering, interlace = header
            if not (0 < width <= MAX_DIMENSION and 0 < height <= MAX_DIMENSION):
                raise ValueError("PNG dimensions must be 1..4096 per axis")
            if depth != 8 or color not in (2, 6) or (compression, filtering, interlace) != (0, 0, 0):
                raise ValueError("PNG must be noninterlaced 8-bit RGB or RGBA")
        elif kind == b"IDAT":
            if image_ended:
                raise ValueError("Nonconsecutive PNG image data")
            image_started = True
            compressed.extend(payload)
        elif kind == b"IEND":
            if size or end != len(data):
                raise ValueError("Invalid PNG end")
            ended = True
            break
        elif kind == b"PLTE":
            if seen_palette or image_started or not 0 < size <= 768 or size % 3:
                raise ValueError("Invalid PNG palette")
            seen_palette = True
        elif kind == b"tRNS" or not kind[0] & 32:
            raise ValueError("Unsupported PNG chunk " + repr(kind))
        if image_started and kind != b"IDAT":
            image_ended = True
        offset = end
    if not ended or not compressed:
        raise ValueError("Missing PNG image data/end")
    channels = 4 if color == 6 else 3
    stride = width * channels
    expected = height * (stride + 1)
    inflater = zlib.decompressobj()
    raw = inflater.decompress(compressed, expected + 1)
    if len(raw) != expected or not inflater.eof or inflater.unused_data or inflater.unconsumed_tail:
        raise ValueError("Invalid or oversized PNG pixel stream")
    pixels = bytearray(width * height * 4)
    previous = bytearray(stride)
    for y in range(height):
        start = y * (stride + 1)
        mode = raw[start]
        if mode > 4:
            raise ValueError("Invalid PNG filter")
        row = bytearray(raw[start + 1:start + 1 + stride])
        for x in range(stride):
            left = row[x - channels] if x >= channels else 0
            above = previous[x]
            corner = previous[x - channels] if x >= channels else 0
            if mode == 1:
                predictor = left
            elif mode == 2:
                predictor = above
            elif mode == 3:
                predictor = (left + above) // 2
            elif mode == 4:
                p = left + above - corner
                distances = (abs(p - left), abs(p - above), abs(p - corner))
                predictor = (left, above, corner)[distances.index(min(distances))]
            else:
                predictor = 0
            row[x] = (row[x] + predictor) & 255
        for x in range(width):
            target = (y * width + x) * 4
            pixels[target:target + 4] = row[x * channels:(x + 1) * channels] + (b"\xff" if channels == 3 else b"")
        previous = row
    return width, height, bytes(pixels)


def compare_pixels(before, after):
    width, height = max(before[0], after[0]), max(before[1], after[1])
    pixels = bytearray(width * height * 4)
    changed = 0
    for y in range(height):
        for x in range(width):
            if (x >= before[0] or y >= before[1]) and (x >= after[0] or y >= after[1]):
                continue
            present = x < before[0] and x < after[0] and y < before[1] and y < after[1]
            left = before[2][(y * before[0] + x) * 4:(y * before[0] + x + 1) * 4] if x < before[0] and y < before[1] else b""
            right = after[2][(y * after[0] + x) * 4:(y * after[0] + x + 1) * 4] if x < after[0] and y < after[1] else b""
            if not present or left != right:
                changed += 1
                color = b"\xff\0\xff\xff"
            else:
                gray = sum(right[:3]) // 6
                color = bytes((gray, gray, gray, 255))
            pixels[(y * width + x) * 4:(y * width + x + 1) * 4] = color
    return changed, encode_png(width, height, pixels)


def finite_float(value):
    number = float(value)
    if not math.isfinite(number):
        raise ValueError("Nonfinite JSON number")
    return number


def read_json(path):
    if path.stat().st_size > 4 * 1024 * 1024:
        raise ValueError("Manifest exceeds size limit")
    return json.loads(path.read_text(encoding="utf-8"), parse_float=finite_float, parse_constant=lambda value: (_ for _ in ()).throw(ValueError("Nonfinite JSON value")))


def input_path(root, value):
    if not isinstance(value, str) or not value or Path(value).is_absolute() or "\\" in value or ":" in value:
        raise ValueError("Capture paths must be relative POSIX paths")
    path = (root / value).resolve()
    if path == root or not path.is_relative_to(root):
        raise ValueError("Capture path escapes input directory: " + value)
    return path


def load_index(root):
    index = read_json(root / "captures.json")
    if not isinstance(index, dict) or index.get("schema") != 1 or not isinstance(index.get("captures"), list):
        raise ValueError("Expected captures.json schema 1")
    if len(index["captures"]) > 256:
        raise ValueError("Capture count exceeds 256")
    captures = {}
    for entry in index["captures"]:
        if not isinstance(entry, dict) or not isinstance(entry.get("scenario"), str) or not re.fullmatch(r"[A-Za-z0-9_./:-]{1,128}", entry["scenario"]):
            raise ValueError("Each capture requires a bounded ASCII scenario identifier")
        if entry["scenario"] in captures:
            raise ValueError("Duplicate scenario: " + entry["scenario"])
        input_path(root, entry.get("file"))
        if not isinstance(entry.get("manifest"), dict):
            input_path(root, entry.get("manifest"))
        captures[entry["scenario"]] = entry
    return index, captures


def validate_capture(root, index, entry, expected_hash):
    result = {"manifest": None, "image": None, "missing": [], "stale": []}
    try:
        manifest = entry["manifest"] if isinstance(entry["manifest"], dict) else read_json(input_path(root, entry["manifest"]))
        if not isinstance(manifest, dict):
            raise ValueError("Capture manifest must be an object")
        result["manifest"] = manifest
    except (OSError, ValueError) as error:
        result["missing"].append("manifest: " + str(error))
        manifest = {}
    for label, value in (("index", index.get("sourceTreeHash")), ("capture", manifest.get("sourceTreeHash"))):
        if not isinstance(value, str) or value.lower() != expected_hash:
            result["stale"].append(label + " sourceTreeHash missing or does not match expected source")
    if manifest.get("scenario") != entry["scenario"]:
        result["stale"].append("manifest scenario mismatch")
    if manifest.get("coverage") not in COVERAGE or manifest.get("coverage") != index.get("coverage"):
        result["stale"].append("coverage missing or inconsistent with index")
    for field in ("width", "height"):
        if type(manifest.get(field)) is not int or not 0 < manifest[field] <= MAX_DIMENSION:
            result["stale"].append("invalid " + field)
    if type(manifest.get("scale")) not in (int, float) or not math.isfinite(manifest["scale"]) or manifest["scale"] <= 0:
        result["stale"].append("invalid scale")
    if type(manifest.get("previewTime")) not in (int, float) or not math.isfinite(manifest["previewTime"]) or manifest["previewTime"] < 0:
        result["stale"].append("invalid previewTime")
    if "fixture" not in manifest or not isinstance(manifest.get("omissions"), list):
        result["stale"].append("fixture or omissions missing")
    if manifest.get("coverage") == "layout-only" and not manifest.get("omissions"):
        result["stale"].append("layout-only capture must state omissions")
    if not isinstance(manifest.get("environment"), dict) or not manifest["environment"]:
        result["stale"].append("full environment provenance missing")
    environment = manifest.get("environmentIdentity")
    if not isinstance(environment, dict):
        environment = {}
    for field in ENVIRONMENT_FIELDS:
        if field not in environment or environment[field] in (None, "", [], {}):
            result["stale"].append("environmentIdentity." + field + " missing")
    viewport = environment.get("viewport")
    if not isinstance(viewport, dict) or any(type(viewport.get(axis)) is not int or not 0 < viewport[axis] <= MAX_DIMENSION for axis in ("width", "height")):
        result["stale"].append("invalid environmentIdentity.viewport")
    if not isinstance(environment.get("locale"), str) or not environment["locale"].strip():
        result["stale"].append("invalid environmentIdentity.locale")
    fonts = environment.get("fonts")
    if not isinstance(fonts, dict) or any(not isinstance(fonts.get(field), str) or not fonts[field].strip() for field in ("standard", "decorative")):
        result["stale"].append("invalid environmentIdentity.fonts")
        fonts = {}
    for field, entries, name_field in (("gameBinaries", environment.get("gameBinaries"), "name"), ("gameAssets", environment.get("gameAssets"), "path"), ("fonts.fontFiles", fonts.get("fontFiles"), "path")):
        if not isinstance(entries, list) or not entries or any(not isinstance(entry, dict) or not isinstance(entry.get(name_field), str) or not entry[name_field].strip() or not isinstance(entry.get("sha256"), str) or not re.fullmatch(r"[0-9a-fA-F]{64}", entry["sha256"]) for entry in entries):
            result["stale"].append("invalid environmentIdentity." + field)
    if type(environment.get("scale")) not in (int, float) or environment.get("scale") != manifest.get("scale"):
        result["stale"].append("environmentIdentity.scale mismatch")
    guide = environment.get("nativeGuide")
    if not isinstance(guide, dict) or type(guide.get("rendered")) is not bool or guide.get("rendered") != (manifest.get("coverage") == "native-client"):
        result["stale"].append("environmentIdentity.nativeGuide coverage mismatch")
        guide = {}
    if not isinstance(guide.get("scope"), str) or not guide["scope"].strip() or not isinstance(guide.get("omissions"), list) or any(not isinstance(item, str) or not item.strip() for item in guide["omissions"]):
        result["stale"].append("invalid environmentIdentity.nativeGuide scope")
    if guide.get("rendered"):
        for field, name_field in (("loadedAssemblies", "name"), ("assets", "path")):
            entries = guide.get(field)
            if not isinstance(entries, list) or not entries or any(not isinstance(item, dict) or not isinstance(item.get(name_field), str) or not item[name_field].strip() or not isinstance(item.get("sha256"), str) or not re.fullmatch(r"[0-9a-fA-F]{64}", item["sha256"]) for item in entries):
                result["stale"].append("invalid environmentIdentity.nativeGuide." + field)
        guide_assets = guide.get("assets")
        if isinstance(guide_assets, list) and any(type(item.get("patched")) is not bool for item in guide_assets if isinstance(item, dict)):
            result["stale"].append("invalid environmentIdentity.nativeGuide asset patch provenance")
    elif guide and (guide.get("loadedAssemblies") != [] or guide.get("assets") != [] or not guide.get("omissions")):
        result["stale"].append("layout-only nativeGuide must state its omission")
    try:
        result["image"] = decode_png(input_path(root, entry["file"]))
        if result["image"][:2] != (manifest.get("width"), manifest.get("height")):
            result["stale"].append("image dimensions do not match manifest")
    except (OSError, ValueError, zlib.error) as error:
        result["missing"].append("image: " + str(error))
    return result


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def make_report(baseline, current, output, baseline_hash, current_hash):
    baseline, current, output = (Path(value).resolve() for value in (baseline, current, output))
    if baseline == current:
        raise ValueError("Baseline and current directories must differ")
    for source in (baseline, current):
        if output.is_relative_to(source) or source.is_relative_to(output):
            raise ValueError("Output must be separate from input directories")
    if output.exists() and (not output.is_dir() or any(output.iterdir())):
        raise ValueError("Output directory must be empty")
    before_index, before = load_index(baseline)
    after_index, after = load_index(current)
    index_reasons = []
    for label, index, expected in (("baseline", before_index, baseline_hash), ("current", after_index, current_hash)):
        value = index.get("sourceTreeHash")
        if not isinstance(value, str) or value.lower() != expected:
            index_reasons.append(label + " index sourceTreeHash missing or stale")
        if index.get("coverage") not in COVERAGE:
            index_reasons.append(label + " index coverage missing or invalid")
    output.mkdir(parents=True, exist_ok=True)
    rows = []
    for scenario in sorted(before.keys() | after.keys()):
        row = {"scenario": scenario, "status": "unchanged", "reasons": list(index_reasons), "artifacts": {}}
        name = re.sub(r"[^A-Za-z0-9_-]", "-", scenario)[:48] + "-" + hashlib.sha256(scenario.encode()).hexdigest()[:12]
        folder = output / "captures" / name
        folder.mkdir(parents=True)
        sides = {}
        for side, root, index, entries, expected in (("before", baseline, before_index, before, baseline_hash), ("after", current, after_index, after, current_hash)):
            if scenario not in entries:
                continue
            capture = validate_capture(root, index, entries[scenario], expected)
            sides[side] = capture
            row["reasons"].extend(side + ": " + reason for reason in capture["missing"] + capture["stale"])
            if capture["manifest"] is not None:
                path = folder / (side + ".json")
                write_json(path, capture["manifest"])
                row["artifacts"][side + "Manifest"] = path.relative_to(output).as_posix()
            if capture["image"] is not None:
                path = folder / (side + ".png")
                path.write_bytes(encode_png(*capture["image"]))
                row["artifacts"][side] = path.relative_to(output).as_posix()
        if any(side["missing"] for side in sides.values()):
            row["status"] = "missing"
        elif index_reasons or any(side["stale"] for side in sides.values()):
            row["status"] = "stale"
        elif "before" not in sides:
            row["status"] = "added"
        elif "after" not in sides:
            row["status"] = "removed"
        else:
            left, right = sides["before"]["manifest"], sides["after"]["manifest"]
            for field in SCENE_FIELDS:
                if left[field] != right[field]:
                    row["reasons"].append(field + " mismatch")
            for field in ENVIRONMENT_FIELDS:
                if left["environmentIdentity"][field] != right["environmentIdentity"][field]:
                    row["reasons"].append("environmentIdentity." + field + " mismatch")
            if row["reasons"]:
                row["status"] = "stale"
            else:
                changed, diff = compare_pixels(sides["before"]["image"], sides["after"]["image"])
                path = folder / "diff.png"
                path.write_bytes(diff)
                row["artifacts"]["diff"] = path.relative_to(output).as_posix()
                row["changedPixels"] = changed
                row["status"] = "changed" if changed else "unchanged"
        rows.append(row)
    counts = dict(Counter(row["status"] for row in rows))
    valid = bool(rows) and not index_reasons and not any(counts.get(status, 0) for status in ("missing", "stale"))
    report = {"schema": 1, "baselineSourceTreeHash": baseline_hash, "currentSourceTreeHash": current_hash,
              "baselineCoverage": before_index.get("coverage"), "currentCoverage": after_index.get("coverage"),
              "evidenceValid": valid, "indexReasons": index_reasons, "counts": counts, "captures": rows}
    write_json(output / "report.json", report)
    render_reports(output, report)
    return report


def render_reports(output, report):
    intro = ("GUI capture comparison. Reference provenance is recorded; this report does not approve a baseline. "
             "Pixel differences require review; this report does not approve captures or certify gameplay. "
             "Layout-only evidence does not certify native character or animation appearance.")
    markdown = ["<!-- gui-capture-report -->", "[AGENT]", "", intro, "",
                "Evidence: **" + ("compatible" if report["evidenceValid"] else "missing or stale") + "**.", "",
                "Baseline source: `" + report["baselineSourceTreeHash"] + "`. Current source: `" + report["currentSourceTreeHash"] + "`.", "",
                "| Scenario | Status | Changed pixels | Artifacts |", "| --- | --- | ---: | --- |"]
    sections, details = [], []
    for row in report["captures"]:
        scenario = html.escape(row["scenario"])
        links = " ".join("[" + key + "](" + path + ")" for key, path in row["artifacts"].items())
        markdown.append("| " + scenario.replace("|", "&#124;").replace("\n", " ").replace("\r", " ") + " | " + row["status"] + " | " + str(row.get("changedPixels", "")) + " | " + links + " |")
        images = "".join('<figure><figcaption>' + side + '</figcaption><a href="' + row["artifacts"][side] + '"><img alt="' + side + '" src="' + row["artifacts"][side] + '"></a></figure>' for side in ("before", "after", "diff") if side in row["artifacts"])
        reasons = "<ul>" + "".join("<li>" + html.escape(reason) + "</li>" for reason in row["reasons"]) + "</ul>" if row["reasons"] else ""
        sections.append("<section><h2>" + scenario + ": " + row["status"] + "</h2>" + reasons + '<div class="images">' + images + "</div></section>")
        if row["reasons"]:
            details.extend(["", "<details><summary>" + scenario + " provenance</summary>", "", *["- " + html.escape(reason).replace("\n", " ").replace("\r", " ") for reason in row["reasons"]], "", "</details>"])
    markdown.extend(details)
    if not report["captures"]:
        markdown.extend(["", "No captures were supplied; evidence is missing."])
    (output / "report.md").write_text("\n".join(markdown) + "\n", encoding="utf-8")
    (output / "report.html").write_text('<!doctype html><html lang="en"><meta charset="utf-8"><title>GUI capture comparison</title><style>body{font:16px sans-serif;max-width:1400px;margin:2em auto;padding:0 1em}.images{display:flex;flex-wrap:wrap;gap:1em}figure{margin:0;flex:1;min-width:240px}img{max-width:100%;height:auto}section{border-top:1px solid #aaa;margin-top:2em}</style><h1>GUI capture comparison</h1><p>' + html.escape(intro) + '</p><p>Evidence: ' + ("compatible" if report["evidenceValid"] else "missing or stale") + '</p><p>Baseline: <code>' + report["baselineSourceTreeHash"] + '</code><br>Current: <code>' + report["currentSourceTreeHash"] + '</code></p>' + "".join(sections) + "</html>", encoding="utf-8")


def source_hash(value):
    if not re.fullmatch(r"[0-9a-fA-F]{64}", value):
        raise argparse.ArgumentTypeError("Source identity must be a SHA-256 hex digest")
    return value.lower()


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline", required=True)
    parser.add_argument("--current", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--baseline-source-hash", required=True, type=source_hash)
    parser.add_argument("--current-source-hash", required=True, type=source_hash)
    args = parser.parse_args(argv)
    try:
        report = make_report(args.baseline, args.current, args.output, args.baseline_source_hash, args.current_source_hash)
    except (OSError, ValueError, zlib.error) as error:
        print("Capture report failed: " + str(error), file=sys.stderr)
        return 2
    print(json.dumps({"evidenceValid": report["evidenceValid"], "counts": report["counts"]}))
    return 0 if report["evidenceValid"] else 1


if __name__ == "__main__":
    sys.exit(main())

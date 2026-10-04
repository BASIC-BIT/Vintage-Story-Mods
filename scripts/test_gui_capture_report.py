#!/usr/bin/env python3
"""Run with python -m unittest discover -s scripts -p test_gui_capture_report.py."""
import importlib.util
import json
from pathlib import Path
import struct
import tempfile
import unittest
import zlib

spec = importlib.util.spec_from_file_location("gui_capture_report", Path(__file__).with_name("gui-capture-report.py"))
reporter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reporter)
BASE_HASH, CURRENT_HASH = "a" * 64, "b" * 64


def png_chunk(kind, data):
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))


def filtered_png(channels, mode):
    """Independent known-pixel fixture exercises row and left/above predictors."""
    rows = [bytes((40, 80, 120, 200, 150, 90, 30, 250)) if channels == 4 else bytes((40, 80, 120, 150, 90, 30)),
            bytes((200, 140, 70, 10, 20, 40, 60, 80)) if channels == 4 else bytes((200, 140, 70, 20, 40, 60))]
    filtered = bytearray()
    for y, row in enumerate(rows):
        filtered.append(mode)
        for x, value in enumerate(row):
            a = row[x - channels] if x >= channels else 0
            b = rows[y - 1][x] if y else 0
            c = rows[y - 1][x - channels] if y and x >= channels else 0
            if mode == 4:
                p = a + b - c
                distances = (abs(p - a), abs(p - b), abs(p - c))
                predictor = (a, b, c)[distances.index(min(distances))]
            else:
                predictor = (0, a, b, (a + b) // 2)[mode]
            filtered.append((value - predictor) & 255)
    expected = b"".join(row if channels == 4 else b"".join(row[x:x + 3] + b"\xff" for x in (0, 3)) for row in rows)
    data = reporter.PNG_SIGNATURE + png_chunk(b"IHDR", struct.pack(">IIBBBBB", 2, 2, 8, 6 if channels == 4 else 2, 0, 0, 0)) + png_chunk(b"IDAT", zlib.compress(filtered)) + png_chunk(b"IEND", b"")
    return data, expected


class CaptureReportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.before, self.after, self.output = (self.root / name for name in ("before", "after", "report"))
        self.before.mkdir()
        self.after.mkdir()
        self.addCleanup(self.temp.cleanup)

    def capture(self, root, scenario="chat", pixels=b"\x28\x50\x78\xff", width=1, height=1):
        source = BASE_HASH if root == self.before else CURRENT_HASH
        manifest = {"scenario": scenario, "width": width, "height": height, "scale": 1, "previewTime": 0,
                    "fixture": {"draft": "default"}, "coverage": "layout-only", "omissions": ["native character"],
                    "sourceTreeHash": source, "environment": {"modBinary": source},
                    "environmentIdentity": {"viewport": {"width": 1600, "height": 1000}, "locale": "en",
                                            "scale": 1, "fonts": {"standard": "Test Font", "decorative": "Test Font", "fontFiles": [{"path": "font.ttf", "sha256": "c" * 64}]},
                                            "gameBinaries": [{"name": "API", "sha256": "d" * 64}], "gameAssets": [{"path": "gui.png", "sha256": "e" * 64}]}}
        (root / (scenario + ".png")).write_bytes(reporter.encode_png(width, height, pixels))
        reporter.write_json(root / (scenario + ".json"), manifest)
        index_path = root / "captures.json"
        index = reporter.read_json(index_path) if index_path.exists() else {"schema": 1, "sourceTreeHash": source, "coverage": "layout-only", "captures": []}
        index["captures"].append({"scenario": scenario, "file": scenario + ".png", "manifest": scenario + ".json"})
        reporter.write_json(index_path, index)
        return manifest

    def change_manifest(self, root, update):
        path = root / "chat.json"
        manifest = reporter.read_json(path)
        update(manifest)
        reporter.write_json(path, manifest)

    def run_report(self):
        return reporter.make_report(self.before, self.after, self.output, BASE_HASH, CURRENT_HASH)

    def test_all_png_filters_rgb_and_rgba(self):
        path = self.root / "fixture.png"
        for channels in (3, 4):
            for mode in range(5):
                with self.subTest(channels=channels, mode=mode):
                    data, expected = filtered_png(channels, mode)
                    path.write_bytes(data)
                    self.assertEqual(reporter.decode_png(path), (2, 2, expected))

    def test_crc_dimensions_and_decompression_bounds(self):
        path = self.root / "bad.png"
        data, _ = filtered_png(4, 0)
        damaged = bytearray(data)
        damaged[-1] ^= 1
        oversized = reporter.PNG_SIGNATURE + png_chunk(b"IHDR", struct.pack(">IIBBBBB", 4097, 1, 8, 6, 0, 0, 0)) + png_chunk(b"IDAT", zlib.compress(b"\0" * 8)) + png_chunk(b"IEND", b"")
        bomb = reporter.PNG_SIGNATURE + png_chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 6, 0, 0, 0)) + png_chunk(b"IDAT", zlib.compress(b"\0" * 100000)) + png_chunk(b"IEND", b"")
        for content in (damaged, oversized, bomb, data[:-1]):
            path.write_bytes(content)
            with self.assertRaises(ValueError):
                reporter.decode_png(path)

    def test_decoded_pixels_not_png_encoding(self):
        self.capture(self.before, pixels=bytes((40, 80, 120, 200, 150, 90, 30, 250, 200, 140, 70, 10, 20, 40, 60, 80)), width=2, height=2)
        self.capture(self.after, pixels=bytes((40, 80, 120, 200, 150, 90, 30, 250, 200, 140, 70, 10, 20, 40, 60, 80)), width=2, height=2)
        (self.after / "chat.png").write_bytes(filtered_png(4, 4)[0])
        inputs = {path: path.read_bytes() for root in (self.before, self.after) for path in root.iterdir()}
        report = self.run_report()
        self.assertTrue(report["evidenceValid"])
        self.assertEqual(report["counts"], {"unchanged": 1})
        self.assertEqual(report["captures"][0]["changedPixels"], 0)
        self.assertTrue((self.output / "report.html").exists())
        self.assertTrue((self.output / "report.md").read_text().startswith("<!-- gui-capture-report -->\n[AGENT]"))
        self.assertTrue(all(path.read_bytes() == data for path, data in inputs.items()))

    def test_alpha_changes_are_magenta_and_missing_edges_count(self):
        self.capture(self.before)
        self.capture(self.after, pixels=b"\x28\x50\x78\x00" + b"\0\0\0\0", width=2)
        report = self.run_report()
        row = report["captures"][0]
        self.assertEqual((row["status"], row["changedPixels"]), ("changed", 2))
        self.assertEqual(reporter.decode_png(self.output / row["artifacts"]["diff"]), (2, 1, b"\xff\0\xff\xff" * 2))

    def test_added_and_removed(self):
        self.capture(self.before, "removed")
        self.capture(self.after, "added")
        report = self.run_report()
        self.assertEqual(report["counts"], {"added": 1, "removed": 1})
        self.assertTrue(report["evidenceValid"])
        self.assertFalse(any("diff" in row["artifacts"] for row in report["captures"]))

    def test_missing_image_and_manifest_are_missing(self):
        for filename in ("chat.png", "chat.json"):
            with self.subTest(filename=filename):
                self.capture(self.before)
                self.capture(self.after)
                (self.after / filename).unlink()
                report = self.run_report()
                self.assertEqual(report["counts"], {"missing": 1})
                self.assertFalse(report["evidenceValid"])
                self.reset_case()

    def reset_case(self):
        # Only removes this test's fixed disposable directories.
        import shutil
        for root in (self.before, self.after, self.output):
            if root.exists():
                shutil.rmtree(root)
        self.before.mkdir()
        self.after.mkdir()

    def test_native_missing_source_hash_is_stale(self):
        self.capture(self.before)
        self.capture(self.after)
        for root in (self.before, self.after):
            self.change_manifest(root, lambda manifest: manifest.update(coverage="native-client", omissions=[]))
            index = reporter.read_json(root / "captures.json")
            index["coverage"] = "native-client"
            reporter.write_json(root / "captures.json", index)
        self.change_manifest(self.after, lambda manifest: manifest.pop("sourceTreeHash"))
        report = self.run_report()
        self.assertEqual(report["counts"], {"stale": 1})
        self.assertFalse(report["evidenceValid"])
        self.assertNotIn("diff", report["captures"][0]["artifacts"])

    def test_environment_and_fixture_mismatches_are_stale(self):
        updates = [lambda manifest, field=field: manifest["environmentIdentity"].update({field: "different"}) for field in reporter.ENVIRONMENT_FIELDS]
        updates += [lambda manifest: manifest.update(previewTime=1), lambda manifest: manifest.update(fixture={"draft": "changed"}), lambda manifest: manifest["environmentIdentity"].pop("fonts")]
        for update in updates:
            self.capture(self.before)
            self.capture(self.after)
            self.change_manifest(self.after, update)
            self.assertEqual(self.run_report()["counts"], {"stale": 1})
            self.reset_case()

    def test_index_source_mismatch_is_stale(self):
        self.capture(self.before)
        self.capture(self.after)
        index = reporter.read_json(self.after / "captures.json")
        index["sourceTreeHash"] = BASE_HASH
        reporter.write_json(self.after / "captures.json", index)
        self.assertEqual(self.run_report()["counts"], {"stale": 1})

    def test_unsafe_paths_and_overlapping_output_rejected_before_writing(self):
        self.capture(self.before)
        self.capture(self.after)
        for output in (self.before, self.before / "nested", self.root):
            with self.assertRaises(ValueError):
                reporter.make_report(self.before, self.after, output, BASE_HASH, CURRENT_HASH)
        for path in ("../outside.png", str(self.root / "outside.png"), "a\\b.png", "C:/outside.png"):
            with self.assertRaises(ValueError):
                reporter.input_path(self.before, path)
        index = reporter.read_json(self.after / "captures.json")
        index["captures"][0]["file"] = "../outside.png"
        reporter.write_json(self.after / "captures.json", index)
        with self.assertRaises(ValueError):
            self.run_report()
        self.assertFalse(self.output.exists())

    def test_output_is_not_reused_and_duplicate_scenarios_rejected(self):
        self.capture(self.before)
        self.capture(self.after)
        self.run_report()
        with self.assertRaises(ValueError):
            self.run_report()
        self.capture(self.after)
        with self.assertRaises(ValueError):
            reporter.load_index(self.after)

    def test_cli_exit_codes_and_invalid_hash(self):
        self.capture(self.before)
        self.capture(self.after)
        args = ["--baseline", str(self.before), "--current", str(self.after), "--output", str(self.output),
                "--baseline-source-hash", BASE_HASH, "--current-source-hash", CURRENT_HASH]
        self.change_manifest(self.after, lambda manifest: manifest.update(sourceTreeHash=BASE_HASH))
        self.assertEqual(reporter.main(args), 1)
        self.assertEqual(reporter.main(args), 2)
        with self.assertRaises(SystemExit) as error:
            reporter.main(args[:-1] + ["bad"])
        self.assertEqual(error.exception.code, 2)


if __name__ == "__main__":
    unittest.main()

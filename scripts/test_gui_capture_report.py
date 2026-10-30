#!/usr/bin/env python3
"""Run with python -m unittest discover -s scripts -p test_gui_capture_report.py."""
import importlib.util
import json
from pathlib import Path
import struct
import shutil
import subprocess
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
                                            "gameBinaries": [{"name": "API", "sha256": "d" * 64}], "gameAssets": [{"path": "gui.png", "sha256": "e" * 64}],
                                            "nativeGuide": {"rendered": False, "scope": "Layout only", "loadedAssemblies": [], "assets": [], "omissions": ["Native character"]}}}
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

    def software_seraph(self, root, actor=True):
        self.capture(root)
        def software(manifest):
            manifest.update(coverage="standalone-gui-and-seraph", omissions=["Live world rendering and GPU shader calibration are not exercised."])
            manifest["environmentIdentity"]["seraphPreview"] = {
                "renderer": "software", "geometry": "native-game-shapes", "animation": "native-client-animator"}
            manifest["environmentIdentity"]["nativeGuide"].update(
                scope="Native client renderer not exercised in standalone composition.",
                omissions=["Native client rendering and GPU shaders are not exercised."])
            manifest["environment"].update(commands=[], seraphs=[], assets=[])
            if actor:
                manifest["environment"].update(
                    seraphs=[{"identity": "Pip", "vertices": 24, "indices": 36, "textures": 1}],
                    commands=[{"op": "seraph", "identity": "Pip", "animation": "idle", "time": 0.75,
                               "yaw": -1.27, "opacity": 1, "width": 100, "height": 200}],
                    assets=[{"path": "assets/game/shapes/entity/humanoid/seraph.json", "sha256": "f" * 64}])
        self.change_manifest(root, software)
        index = reporter.read_json(root / "captures.json")
        index["coverage"] = "standalone-gui-and-seraph"
        reporter.write_json(root / "captures.json", index)

    def test_software_seraph_coverage_preserves_models_assets_and_poses_without_claiming_native_gpu(self):
        for actor in (False, True):
            with self.subTest(actor=actor):
                for root in (self.before, self.after):
                    self.software_seraph(root, actor)
                report = self.run_report()
                self.assertTrue(report["evidenceValid"])
                self.assertEqual(report["counts"], {"unchanged": 1})
                self.assertEqual(report["currentCoverage"], "standalone-gui-and-seraph")
                saved = reporter.read_json(self.output / report["captures"][0]["artifacts"]["afterManifest"])
                self.assertFalse(saved["environmentIdentity"]["nativeGuide"]["rendered"])
                self.assertEqual(len(saved["environment"]["seraphs"]), int(actor))
                for filename in ("report.md", "report.html"):
                    text = (self.output / filename).read_text()
                    self.assertIn("standalone-gui-and-seraph", text)
                    self.assertIn("native client GPU", text)
                self.reset_case()

    def test_software_seraph_provenance_is_required_when_an_actor_is_drawn(self):
        updates = [
            lambda manifest: manifest["environmentIdentity"]["seraphPreview"].update(renderer="native"),
            lambda manifest: manifest["environmentIdentity"]["nativeGuide"].update(rendered=True),
            lambda manifest: manifest.update(omissions=[]),
            lambda manifest: manifest["environment"].update(assets=[]),
            lambda manifest: manifest["environment"]["assets"][0].update(sha256="invalid"),
            lambda manifest: manifest["environment"].update(seraphs=[]),
            lambda manifest: manifest["environment"]["seraphs"][0].update(vertices=0),
            lambda manifest: manifest["environment"]["commands"][0].update(identity="unknown"),
            lambda manifest: manifest["environment"]["commands"][0].update(animation=""),
            lambda manifest: manifest["environment"]["commands"][0].update(time="invalid"),
            lambda manifest: manifest["environment"]["commands"][0].update(opacity=2),
        ]
        for update in updates:
            with self.subTest(update=update):
                for root in (self.before, self.after):
                    self.software_seraph(root)
                self.change_manifest(self.after, update)
                self.assertEqual(self.run_report()["counts"], {"stale": 1})
                self.reset_case()

    def test_software_seraph_asset_changes_are_stale_but_pose_changes_are_scene_output(self):
        for root in (self.before, self.after):
            self.software_seraph(root)
        self.change_manifest(self.after, lambda manifest: manifest["environment"]["assets"][0].update(sha256="e" * 64))
        self.assertEqual(self.run_report()["counts"], {"stale": 1})
        self.reset_case()
        for root in (self.before, self.after):
            self.software_seraph(root)
        self.change_manifest(self.after, lambda manifest: manifest["environment"]["commands"][0].update(yaw=0.5))
        self.assertTrue(self.run_report()["evidenceValid"])

    def test_layout_and_software_seraph_coverage_are_not_comparable(self):
        self.capture(self.before)
        self.software_seraph(self.after)
        report = self.run_report()
        self.assertEqual(report["counts"], {"stale": 1})
        self.assertIn("coverage mismatch", report["captures"][0]["reasons"])

    def test_software_seraph_assets_compare_across_checkouts_without_losing_identity(self):
        for case in ("same", "hash", "domain", "unknown", "duplicate"):
            with self.subTest(case=case):
                self.reset_case()
                for root in (self.before, self.after):
                    self.software_seraph(root)
                    path = str(root / "mods-dll/thebasics/assets/thebasics/config/setup-guide.json")
                    self.change_manifest(root, lambda manifest, path=path: manifest["environment"]["assets"].append(
                        {"path": path, "sha256": "a" * 64}))
                if case == "hash":
                    self.change_manifest(self.after, lambda manifest: manifest["environment"]["assets"][1].update(sha256="b" * 64))
                elif case == "domain":
                    self.change_manifest(self.after, lambda manifest: manifest["environment"]["assets"][1].update(
                        path=str(self.after / "mods-dll/thebasics/assets/other/config/setup-guide.json")))
                elif case == "unknown":
                    self.change_manifest(self.after, lambda manifest: manifest["environment"]["assets"][1].update(
                        path=str(self.after / "other/assets/thebasics/config/setup-guide.json")))
                elif case == "duplicate":
                    self.change_manifest(self.after, lambda manifest: manifest["environment"]["assets"].append(
                        {"path": "C:/other/mods-dll/thebasics/assets/thebasics/config/setup-guide.json", "sha256": "a" * 64}))
                report = self.run_report()
                self.assertEqual(report["counts"], {"unchanged" if case == "same" else "stale": 1})
                self.assertEqual(report["evidenceValid"], case == "same")

    @unittest.skipUnless(shutil.which("pwsh"), "PowerShell required for collector aggregation check")
    def test_collector_externalizes_222_expanded_receipts_within_report_size_bound(self):
        collector = Path(__file__).with_name("collect-gui-captures.ps1").resolve()
        aggregate = self.root / "aggregate.ps1"
        # Execute the production aggregation statements without downloads, builds, or rendering.
        aggregate.write_text(r'''param([string]$Collector, [string]$CaptureRoot, [string]$SourceHash)
$ErrorActionPreference = 'Stop'
$ast = [Management.Automation.Language.Parser]::ParseFile($Collector, [ref]$null, [ref]$null)
$entryLoop = @($ast.FindAll({ param($node)
    $node -is [Management.Automation.Language.ForEachStatementAst] -and
        $node.Variable.VariablePath.UserPath -eq 'entry' -and $node.Condition.Extent.Text -eq '$index.captures'
}, $true))
$indexWriter = @($ast.FindAll({ param($node)
    $node -is [Management.Automation.Language.PipelineAst] -and
        $node.Extent.Text.StartsWith('@{ schema = 1; sourceTreeHash = $sourceHash;')
}, $true))
if ($entryLoop.Count -ne 1 -or $indexWriter.Count -ne 1) { throw 'Expected one capture loop and index writer.' }
$captures = [Collections.Generic.List[object]]::new()
$coverage = 'layout-only'
$hasScenes = $true
$directory = $CaptureRoot
foreach ($scale in @('1', '1.25')) {
    $scaleDirectory = Join-Path $directory ('scale-' + $scale)
    $index = Get-Content -LiteralPath (Join-Path $scaleDirectory 'captures.json') -Raw | ConvertFrom-Json
    & ([scriptblock]::Create($entryLoop[0].Extent.Text))
}
& ([scriptblock]::Create($indexWriter[0].Extent.Text))
''', encoding="utf-8")
        bound = 4 * 1024 * 1024
        for root, source in ((self.before, BASE_HASH), (self.after, CURRENT_HASH)):
            template = self.capture(root)
            commands = [{"op": "texture", "id": number, "x": number % 80, "y": number // 80,
                         "width": 40, "height": 22, "z": 50, "color": [1, 1, 1, 1]}
                        for number in range(400)]
            for scale in (1, 1.25):
                directory = root / ("scale-" + str(scale))
                directory.mkdir()
                entries = []
                for number in range(111):
                    scene = "wizard-scene-" + str(number)
                    manifest = dict(template, scenario=scene, scale=scale,
                                    environment=dict(template["environment"], commands=commands),
                                    environmentIdentity=dict(template["environmentIdentity"], scale=scale))
                    (directory / (scene + ".png")).write_bytes((root / "chat.png").read_bytes())
                    reporter.write_json(directory / (scene + ".json"), manifest)
                    entries.append({"scenario": scene, "file": scene + ".png", "manifest": scene + ".json"})
                reporter.write_json(directory / "captures.json", {"schema": 1, "sourceTreeHash": source,
                                    "coverage": "layout-only", "captures": entries})
            subprocess.run(["pwsh", "-NoProfile", "-File", str(aggregate), str(collector), str(root), source], check=True)
            self.assertLess((root / "captures.json").stat().st_size, bound)
            index, entries = reporter.load_index(root)
            self.assertEqual(len(entries), 222)
            for entry in entries.values():
                self.assertIsInstance(entry["manifest"], str)
                self.assertNotIn("\\", entry["manifest"])
                manifest = reporter.read_json(root / entry["manifest"])
                self.assertEqual(manifest["scenario"], entry["scenario"])
                self.assertEqual(manifest["sceneId"], entry["scenario"].split("/")[1])
                self.assertEqual(manifest["environment"]["commands"], commands)
                original = reporter.read_json(root / entry["manifest"].replace(".report.json", ".json"))
                self.assertEqual(original["scenario"], manifest["sceneId"])
                self.assertNotIn("sceneId", original)
        report = self.run_report()
        self.assertTrue(report["evidenceValid"])
        self.assertEqual(report["counts"], {"unchanged": 222})
        # The prior inline index repeats the same complete receipts and exceeds the existing guard.
        index = reporter.read_json(self.after / "captures.json")
        for entry in index["captures"]:
            entry["manifest"] = reporter.read_json(self.after / entry["manifest"])
        reporter.write_json(self.after / "captures.json", index)
        self.assertGreater((self.after / "captures.json").stat().st_size, bound)
        with self.assertRaisesRegex(ValueError, "Manifest exceeds size limit"):
            reporter.load_index(self.after)

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

    def test_native_dependencies_are_required_and_changes_make_comparison_stale(self):
        for mutation in (lambda guide: guide["assets"][0].update(sha256="f" * 64),
                         lambda guide: guide["loadedAssemblies"][0].update(sha256="f" * 64),
                         lambda guide: guide["loadedAssemblies"].clear(),
                         lambda guide: guide.update(assets=None),
                         lambda guide: guide["assets"][0].pop("patched"),
                         lambda guide: guide.update(rendered=False)):
            with self.subTest(mutation=mutation):
                for root in (self.before, self.after):
                    self.capture(root)
                    def native(manifest):
                        manifest.update(coverage="native-client", omissions=[])
                        manifest["environmentIdentity"]["nativeGuide"] = {
                            "rendered": True, "scope": "Loaded native guide assets", "omissions": ["Runtime object changes"],
                            "loadedAssemblies": [{"name": "VSSurvivalMod", "sha256": "c" * 64}],
                            "assets": [{"path": "game:entities/player.json", "sha256": "d" * 64, "patched": False}]}
                    self.change_manifest(root, native)
                    index = reporter.read_json(root / "captures.json")
                    index["coverage"] = "native-client"
                    reporter.write_json(root / "captures.json", index)
                self.change_manifest(self.after, lambda manifest: mutation(manifest["environmentIdentity"]["nativeGuide"]))
                report = self.run_report()
                self.assertFalse(report["evidenceValid"])
                self.assertEqual(report["counts"], {"stale": 1})
                shutil.rmtree(self.output)
                for root in (self.before, self.after):
                    (root / "captures.json").unlink()

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

    def test_untrusted_scenario_labels_and_capture_count_are_bounded(self):
        self.capture(self.after)
        index = reporter.read_json(self.after / "captures.json")
        for scenario in ("[click](https://example.com)", "@everyone", "x" * 129):
            index["captures"][0]["scenario"] = scenario
            reporter.write_json(self.after / "captures.json", index)
            with self.assertRaises(ValueError):
                reporter.load_index(self.after)
        index["captures"] = [dict(index["captures"][0], scenario="scene-" + str(number)) for number in range(257)]
        reporter.write_json(self.after / "captures.json", index)
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

    @unittest.skipUnless(shutil.which("pwsh"), "PowerShell required for source identity check")
    def test_source_identity_ignores_checkout_eol_but_retains_source_and_binary_changes(self):
        script = Path(__file__).with_name("gui-source-identity.ps1")
        source = self.root / "mods-dll" / "thebasics" / "src" / "test.cs"
        image = self.root / "mods-dll" / "thebasics" / "assets" / "test.png"
        source.parent.mkdir(parents=True)
        image.parent.mkdir(parents=True)
        image.write_bytes(b"\r\n\x00")

        def identity():
            return subprocess.check_output(["pwsh", "-NoProfile", "-File", str(script), "-Repo", str(self.root)], text=True).strip()

        source.write_bytes(b"class C {}\n")
        original = identity()
        source.write_bytes(b"class C {}\r\n")
        self.assertEqual(original, identity())
        source.write_bytes(b"class C { int X; }\n")
        self.assertNotEqual(original, identity())
        source.write_bytes(b"class C {}\n")
        image.write_bytes(b"\n\x00")
        self.assertNotEqual(original, identity())


if __name__ == "__main__":
    unittest.main()

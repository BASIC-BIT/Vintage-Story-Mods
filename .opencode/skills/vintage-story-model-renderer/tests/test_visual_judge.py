import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image


SCRIPTS = Path(__file__).parents[1] / "scripts"
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))

from vintage_story_model_renderer.judge import parse_judgment, prepare_request


class VisualJudgeRequestTests(unittest.TestCase):
    def make_evidence(self, root: Path) -> Path:
        evidence = root / "evidence"
        evidence.mkdir()
        Image.new("RGB", (8, 8), (64, 96, 128)).save(evidence / "contact-sheet.png")
        (evidence / "render-metadata.json").write_text(
            json.dumps(
                {
                    "name": "fixture",
                    "representation": "placed",
                    "faceCount": 6,
                    "boundsModelUnits": {"min": [0, 0, 0], "max": [1, 1, 1]},
                    "renderedImageCount": 24,
                    "coplanarOverlapCount": 0,
                    "unresolvedTextures": [],
                }
            ),
            encoding="utf-8",
        )
        return evidence

    def test_prepare_request_is_offline_and_hash_bound(self):
        with tempfile.TemporaryDirectory() as temporary:
            evidence = self.make_evidence(Path(temporary))
            request, media, prompt = prepare_request(
                evidence,
                video=None,
                rubric_path=None,
                model="test-model",
            )

        self.assertFalse(request["executed"])
        self.assertTrue(request["advisoryOnly"])
        self.assertEqual("test-model", request["model"])
        self.assertEqual(1, len(media))
        self.assertEqual(64, len(request["metadata"]["sha256"]))
        self.assertEqual(64, len(request["rubric"]["sha256"]))
        self.assertEqual(64, len(request["media"][0]["sha256"]))
        self.assertIn("neutral inventory", prompt)
        self.assertNotIn("GEMINI_API_KEY", json.dumps(request))

    def test_prepare_request_rejects_incomplete_view_set(self):
        with tempfile.TemporaryDirectory() as temporary:
            evidence = self.make_evidence(Path(temporary))
            metadata_path = evidence / "render-metadata.json"
            metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
            metadata["renderedImageCount"] = 23
            metadata_path.write_text(json.dumps(metadata), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "complete 24-image"):
                prepare_request(evidence, None, None, "test-model")

    def test_package_directory_is_directly_executable(self):
        result = subprocess.run(
            [sys.executable, str(SCRIPTS / "vintage_story_model_renderer"), "--help"],
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("--manifest", result.stdout)

    def test_package_directory_dispatches_judge(self):
        result = subprocess.run(
            [
                sys.executable,
                str(SCRIPTS / "vintage_story_model_renderer"),
                "judge",
                "--help",
            ],
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("--execute", result.stdout)


class VisualJudgeParsingTests(unittest.TestCase):
    def test_error_finding_forces_fail_and_counts_objective_defect(self):
        judgment = parse_judgment(
            json.dumps(
                {
                    "neutralDescription": "A wheel with a visibly open side.",
                    "findings": [
                        {
                            "category": "missing-face",
                            "severity": "error",
                            "source": "model",
                            "evidence": "isometric: the far cylinder cap is absent",
                            "recommendation": "inspect cap winding",
                        }
                    ],
                    "verdict": "pass",
                    "summary": "Incorrect optimistic model verdict.",
                }
            )
        )

        self.assertEqual("fail", judgment["verdict"])
        self.assertEqual(1, judgment["objectiveDefectCount"])

    def test_warning_forces_human_review(self):
        judgment = parse_judgment(
            json.dumps(
                {
                    "neutralDescription": "A framed wheel.",
                    "findings": [
                        {
                            "category": "construction",
                            "severity": "warning",
                            "source": "uncertain",
                            "evidence": "isometric-opposite: brace joint appears unusually thin",
                            "recommendation": "ask for human construction review",
                        }
                    ],
                    "verdict": "pass",
                    "summary": "Ambiguous construction.",
                }
            )
        )

        self.assertEqual("needs-review", judgment["verdict"])
        self.assertEqual(0, judgment["objectiveDefectCount"])


if __name__ == "__main__":
    unittest.main()

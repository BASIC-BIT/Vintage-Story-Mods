import json
import sys
import tempfile
import unittest
from unittest import mock
from pathlib import Path

import numpy as np
from PIL import Image

SCRIPTS = Path(__file__).parents[1] / "scripts"
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))

import vintage_story_model_renderer as renderer


class CollectibleTransformTests(unittest.TestCase):
    def test_definition_loader_accepts_vintage_story_relaxed_json(self):
        with tempfile.TemporaryDirectory() as directory:
            definition = Path(directory) / "block.json"
            definition.write_text("""
                {
                  // Asset files commonly use unquoted keys and trailing commas.
                  fpHandTransform: {
                    translation: { x: 0.25, y: 0, z: 0, },
                    scale: 2,
                  },
                }
            """, encoding="utf-8")

            transform = renderer.load_collectible_transform(definition, "fpHandTransform")

            self.assertEqual((0.25, 0, 0), transform.translation)
            self.assertEqual((2, 2, 2), transform.scale)

    def test_definition_transform_uses_vintage_story_matrix_order_and_block_units(self):
        with tempfile.TemporaryDirectory() as directory:
            definition = Path(directory) / "item.json"
            definition.write_text(json.dumps({
                "tpHandTransform": {
                    "translation": {"x": 1, "y": 2, "z": 3},
                    "rotation": {"x": 90, "y": 90, "z": 90},
                    "origin": {"x": 0, "y": 0, "z": 0},
                    "scaleXYZ": {"x": 2, "y": 3, "z": 4},
                }
            }), encoding="utf-8")

            transform = renderer.load_collectible_transform(definition, "tpHandTransform")
            transformed = renderer.transform_point((1, 0, 0), transform)

            for actual, expected in zip(transformed, (16, 32, 50)):
                self.assertAlmostEqual(expected, actual)

    def test_origin_and_translation_are_converted_from_blocks_to_model_units(self):
        with tempfile.TemporaryDirectory() as directory:
            definition = Path(directory) / "item.json"
            definition.write_text(json.dumps({
                "fpHandTransform": {
                    "translation": {"x": 0.25, "y": 0, "z": 0},
                    "origin": {"x": 0.5, "y": 0.5, "z": 0.5},
                    "scale": 2,
                }
            }), encoding="utf-8")

            transform = renderer.load_collectible_transform(definition, "fpHandTransform")

            self.assertEqual((2, 2, 2), transform.scale)
            self.assertEqual((12, 8, 8), renderer.transform_point((8, 8, 8), transform))

    def test_variant_transform_uses_first_matching_by_type_entry_and_merges_direct_value(self):
        with tempfile.TemporaryDirectory() as directory:
            definition = Path(directory) / "item.json"
            definition.write_text(json.dumps({
                "tpHandTransform": {
                    "translation": {"x": -1, "y": -2, "z": -3},
                    "rotation": {"x": 10, "y": 20, "z": 30},
                    "scale": 0.4,
                },
                "tpHandTransformByType": {
                    "flywheel-full-*": {
                        "translation": {"x": -0.5},
                        "scale": 0.8,
                    },
                    "*": {"scale": 9},
                },
            }), encoding="utf-8")

            transform = renderer.load_collectible_transform(
                definition,
                "tpHandTransform",
                variant_code="flywheel-full-steel",
            )

            self.assertEqual((-0.5, -2, -3), transform.translation)
            self.assertEqual((10, 20, 30), transform.rotation)
            self.assertEqual((0.8, 0.8, 0.8), transform.scale)
            self.assertEqual("flywheel-full-steel", transform.variant_code)
            self.assertEqual("tpHandTransformByType:flywheel-full-*", transform.resolution)

    def test_variant_scalar_property_supports_engine_regex_patterns(self):
        value, resolution = renderer.resolve_collectible_property(
            {
                "heldTpIdleAnimationByType": {
                    "@flywheel-(rim|web)-full.*": "holdbothhandslarge",
                    "*": "idle1",
                },
            },
            "heldTpIdleAnimation",
            "flywheel-rim-full-steel",
        )

        self.assertEqual("holdbothhandslarge", value)
        self.assertEqual(
            "heldTpIdleAnimationByType:@flywheel-(rim|web)-full.*",
            resolution,
        )

    def test_transform_preserves_face_identity_and_uvs(self):
        transform = renderer.CollectibleTransform(
            "tpHandTransform",
            (0, 0, 0),
            (0, 0, 0),
            (0, 0, 0),
            (2, 2, 2),
            True,
            16,
        )
        source = renderer.Face(
            [(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)],
            "metal",
            "plate",
            [(0, 0), (1, 0), (1, 1), (0, 1)],
            "north",
            "fixture",
        )

        transformed = renderer.transform_faces([source], transform)[0]

        self.assertEqual("metal", transformed.material)
        self.assertEqual("plate", transformed.element)
        self.assertEqual(source.uvs, transformed.uvs)
        self.assertEqual((2, 2, 0), transformed.vertices[2])

    def test_shape_animation_applies_the_selected_collectible_transform(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            shape = root / "shape.json"
            shape.write_text(json.dumps({
                "elements": [{
                    "name": "cube",
                    "from": [0, 0, 0],
                    "to": [1, 1, 1],
                    "faces": {"north": {"texture": "#surface"}},
                }],
                "textures": {"surface": "fixture:surface"},
                "animations": [{
                    "code": "idle",
                    "quantityframes": 1,
                    "keyframes": [],
                }],
            }), encoding="utf-8")
            transform = renderer.CollectibleTransform(
                "groundTransform",
                (1, 0, 0),
                (0, 0, 0),
                (0, 0, 0),
                (1, 1, 1),
                True,
                16,
            )

            with (
                mock.patch("vintage_story_model_renderer.video.render") as render_call,
                mock.patch("vintage_story_model_renderer.video.subprocess.run"),
                mock.patch("vintage_story_model_renderer.video.sha256", return_value="HASH"),
            ):
                renderer.render_animation(
                    shape,
                    "idle",
                    {},
                    {},
                    "front",
                    root / "animation.mp4",
                    64,
                    1,
                    1,
                    1,
                    False,
                    transform,
                )

            rendered_faces = render_call.call_args.args[0]
            self.assertEqual(16, min(vertex[0] for face in rendered_faces for vertex in face.vertices))
            self.assertTrue(all(face.texture_key for face in rendered_faces))

    def test_grip_proxy_is_explicit_reference_geometry_centered_on_transformed_pivot(self):
        transform = renderer.CollectibleTransform(
            "tpHandTransform",
            (1, 0, 0),
            (0, 0, 0),
            (0.5, 0.5, 0.5),
            (1, 1, 1),
            True,
            16,
        )

        faces = renderer.grip_reference_faces(transform)

        self.assertEqual(18, len(faces))
        self.assertTrue(all(face.source == "representation-reference" for face in faces))
        palm_vertices = [vertex for face in faces if face.element == "grip-proxy-palm" for vertex in face.vertices]
        self.assertAlmostEqual(24, (min(vertex[0] for vertex in palm_vertices) + max(vertex[0] for vertex in palm_vertices)) / 2)

    def test_cli_records_definition_hash_and_reference_boundary(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            shape = root / "shape.json"
            shape.write_text(json.dumps({
                "elements": [{
                    "name": "cube",
                    "from": [0, 0, 0],
                    "to": [16, 16, 16],
                    "faces": {
                        direction: {"texture": "#material"}
                        for direction in renderer.FACE_INDICES
                    },
                }],
                "textures": {"material": "game:block/missing"},
            }), encoding="utf-8")
            definition = root / "item.json"
            definition.write_text(json.dumps({
                "tpHandTransform": {
                    "translation": {"x": -1, "y": -1, "z": -1},
                    "rotation": {"x": 0, "y": -62, "z": 18},
                    "scale": 0.42,
                }
            }), encoding="utf-8")
            manifest = root / "manifest.json"
            manifest.write_text(json.dumps({
                "name": "held-fixture",
                "shapes": ["shape.json"],
                "collectibleTransform": {
                    "definition": "item.json",
                    "property": "tpHandTransform",
                    "reference": "grip-proxy",
                },
            }), encoding="utf-8")
            output = root / "output"

            renderer.main([
                "--manifest", str(manifest),
                "--output-dir", str(output),
                "--size", "64",
            ])
            metadata = json.loads((output / "render-metadata.json").read_text(encoding="utf-8"))

            self.assertEqual(24, metadata["renderedImageCount"])
            self.assertEqual(18, metadata["referenceFaceCount"])
            self.assertFalse(metadata["representationReference"]["runtimeParity"])
            self.assertEqual(["material"], metadata["unresolvedTextures"])
            self.assertEqual(
                "builtin:representation-reference-color",
                metadata["resolvedTextures"]["reference-hand"],
            )
            self.assertEqual(str(definition.resolve()), metadata["inputs"][-1]["path"])
            self.assertEqual(renderer.sha256(definition), metadata["inputs"][-1]["sha256"])

    def test_reference_opacity_blends_without_hiding_existing_geometry(self):
        pixels = np.full((3, 3, 3), 10, dtype=np.uint8)
        depths = np.full((3, 3), -np.inf)

        renderer.rasterize_triangle(
            pixels,
            depths,
            [(0, 0), (2, 0), (0, 2)],
            [1, 1, 1],
            (100, 100, 100),
            opacity=0.5,
        )

        self.assertEqual([55, 55, 55], pixels[0, 0].tolist())


class SeraphHeldSceneTests(unittest.TestCase):
    def test_same_shape_step_parent_element_is_composed_under_named_parent(self):
        with tempfile.TemporaryDirectory() as directory:
            shape = Path(directory) / "shape.json"
            shape.write_text(json.dumps({
                "elements": [
                    {
                        "name": "Head",
                        "from": [10, 0, 0],
                        "to": [11, 1, 1],
                        "faces": {},
                    },
                    {
                        "name": "eyesroot",
                        "stepParentName": "Head",
                        "from": [2, 0, 0],
                        "to": [3, 1, 1],
                        "faces": {"north": {"texture": "#skin"}},
                    },
                ],
            }), encoding="utf-8")

            faces, _ = renderer.load_shape(shape)

            self.assertEqual(1, len(faces))
            self.assertEqual(12, min(vertex[0] for vertex in faces[0].vertices))
            self.assertEqual(13, max(vertex[0] for vertex in faces[0].vertices))

    def test_shape_loader_tracks_attachment_through_nested_animated_elements(self):
        with tempfile.TemporaryDirectory() as directory:
            shape = Path(directory) / "seraph.json"
            shape.write_text(json.dumps({
                "elements": [{
                    "name": "root",
                    "from": [10, 0, 0],
                    "to": [10, 0, 0],
                    "faces": {},
                    "children": [{
                        "name": "ItemAnchor",
                        "from": [2, 0, 0],
                        "to": [2, 0, 0],
                        "faces": {},
                        "attachmentpoints": [{
                            "code": "RightHand",
                            "posX": 1,
                            "posY": 0,
                            "posZ": 0,
                            "rotationY": -180,
                        }],
                    }],
                }],
                "animations": [{
                    "code": "idle",
                    "quantityframes": 1,
                    "keyframes": [{
                        "frame": 0,
                        "elements": {"root": {"offsetX": 1}},
                    }],
                }],
            }), encoding="utf-8")

            _, _, attachments = renderer.load_shape_scene(shape, "idle", 0)
            attachment = renderer.select_attachment(attachments, "RightHand")

            self.assertEqual("root/ItemAnchor", attachment.element_path)
            self.assertEqual((0, -180, 0), attachment.rotation)
            self.assertEqual((14, 0, 0), attachment.transform(attachment.position))

    def test_held_composition_matches_engine_chain_including_scaled_translation(self):
        attachment = renderer.AttachmentPose(
            "RightHand",
            "ItemAnchor",
            "root/ItemAnchor",
            (16, 0, 0),
            (0, 0, 0),
            lambda point: point,
        )
        transform = renderer.CollectibleTransform(
            "tpHandTransform",
            (1, 0, 0),
            (0, 0, 0),
            (0, 0, 0),
            (2, 2, 2),
            True,
            16,
        )

        self.assertEqual((96, 0, 0), renderer.compose_held_point((16, 0, 0), transform, attachment))

    def test_shape_asset_resolution_honors_domain_and_json_suffix(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            shape = root / "game" / "shapes" / "entity" / "seraph.json"
            shape.parent.mkdir(parents=True)
            shape.write_text("{}", encoding="utf-8")

            self.assertEqual(
                shape,
                renderer.resolve_shape_asset("game:entity/seraph", [root]),
            )

    def test_cli_records_full_seraph_attachment_and_default_animation_boundary(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            assets = root / "assets"
            seraph = assets / "fixture" / "shapes" / "entity" / "seraph.json"
            seraph.parent.mkdir(parents=True)
            seraph_texture = assets / "fixture" / "textures" / "entity" / "seraph.png"
            seraph_texture.parent.mkdir(parents=True)
            Image.new("RGBA", (1, 1), (12, 34, 56, 255)).save(seraph_texture)
            seraph.write_text(json.dumps({
                "textures": {"seraph": "fixture:entity/ignored"},
                "elements": [{
                    "name": "root",
                    "from": [0, 0, 0],
                    "to": [2, 4, 2],
                    "faces": {"north": {"texture": "#seraph"}},
                    "children": [{
                        "name": "ItemAnchor",
                        "from": [2, 2, 0],
                        "to": [2, 2, 0],
                        "faces": {},
                        "attachmentpoints": [{
                            "code": "RightHand",
                            "posX": 0,
                            "posY": 0,
                            "posZ": 0,
                            "rotationY": -180,
                        }],
                    }],
                }],
                "animations": [{
                    "code": "idle1",
                    "quantityframes": 1,
                    "keyframes": [],
                }],
            }), encoding="utf-8")
            item_shape = root / "item-shape.json"
            item_shape.write_text(json.dumps({
                "elements": [{
                    "name": "item",
                    "from": [0, 0, 0],
                    "to": [1, 1, 1],
                    "faces": {"north": {"texture": "#metal"}},
                }],
                "textures": {"metal": "fixture:item/metal"},
            }), encoding="utf-8")
            definition = root / "item.json"
            definition.write_text(json.dumps({
                "tpHandTransform": {
                    "translation": {"x": 0, "y": 0, "z": 0},
                    "rotation": {"x": 0, "y": 0, "z": 0},
                    "scale": 1,
                },
            }), encoding="utf-8")
            manifest = root / "manifest.json"
            manifest.write_text(json.dumps({
                "name": "seraph-held-fixture",
                "shapes": ["item-shape.json"],
                "seraphHeldScene": {
                    "collectibleDefinition": "item.json",
                    "transformProperty": "tpHandTransform",
                    "seraphShape": "fixture:entity/seraph",
                    "seraphTexture": "fixture:entity/seraph",
                    "attachment": "RightHand",
                },
            }), encoding="utf-8")
            output = root / "output"

            renderer.main([
                "--manifest", str(manifest),
                "--output-dir", str(output),
                "--assets-root", str(assets),
                "--size", "64",
            ])
            metadata = json.loads((output / "render-metadata.json").read_text(encoding="utf-8"))

            self.assertEqual(24, metadata["renderedImageCount"])
            self.assertEqual("seraph-held-item", metadata["heldScene"]["type"])
            self.assertEqual("root/ItemAnchor", metadata["heldScene"]["attachmentElementPath"])
            self.assertEqual("idle1", metadata["heldScene"]["animation"])
            self.assertEqual("player-default-idle", metadata["heldScene"]["animationSelection"])
            self.assertEqual(str(seraph_texture), metadata["resolvedTextures"]["seraph"])
            self.assertEqual("single-animation geometry and attachment matrix", metadata["heldScene"]["runtimeParity"])
            self.assertGreater(metadata["seraphFaceCount"], 0)
            self.assertEqual(1, metadata["itemFaceCount"])
            provenance = {entry["path"]: entry["sha256"] for entry in metadata["inputs"]}
            self.assertIn(str(definition.resolve()), provenance)
            self.assertIn(str(seraph.resolve()), provenance)
            self.assertEqual(renderer.sha256(seraph_texture), provenance[str(seraph_texture)])

    def test_cli_resolves_variant_specific_two_hand_pose(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            collectible = {
                "heldTpIdleAnimationByType": {
                    "fixture-full-*": "holdbothhandslarge",
                },
            }

            animation, resolution = renderer.resolve_collectible_property(
                collectible,
                "heldTpIdleAnimation",
                "fixture-full-steel",
            )

            self.assertEqual("holdbothhandslarge", animation)
            self.assertEqual(
                "heldTpIdleAnimationByType:fixture-full-*",
                resolution,
            )


if __name__ == "__main__":
    unittest.main()

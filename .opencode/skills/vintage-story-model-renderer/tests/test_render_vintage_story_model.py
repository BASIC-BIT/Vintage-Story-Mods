import json
import sys
import tempfile
import unittest
from pathlib import Path

import numpy as np
from PIL import Image


SCRIPTS = Path(__file__).parents[1] / "scripts"
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))

import vintage_story_model_renderer as renderer


class PackageBoundaryTests(unittest.TestCase):
    def test_public_api_exports_focused_package_modules(self):
        self.assertEqual("vintage_story_model_renderer.cli", renderer.main.__module__)
        self.assertEqual("vintage_story_model_renderer.shapes", renderer.load_shape.__module__)
        self.assertEqual("vintage_story_model_renderer.rendering", renderer.render.__module__)
        self.assertEqual("vintage_story_model_renderer.video", renderer.render_animation.__module__)
        self.assertEqual("vintage_story_model_renderer.flywheel", renderer.load_flywheel.__module__)


class AnnulusWindingTests(unittest.TestCase):
    def test_near_and_far_caps_face_their_respective_views(self):
        faces = []
        renderer.add_annulus(
            faces,
            min_x=7.0,
            max_x=9.0,
            inner_radius=2.0,
            outer_radius=4.0,
            material="wheel",
            element="test",
            segments=8,
            radial_steps=1,
            texture_units=100,
        )

        near_cap = faces[0]
        far_cap = faces[1]
        near_normal = renderer.normalize(
            renderer.cross(
                renderer.sub(near_cap.vertices[1], near_cap.vertices[0]),
                renderer.sub(near_cap.vertices[2], near_cap.vertices[0]),
            )
        )
        far_normal = renderer.normalize(
            renderer.cross(
                renderer.sub(far_cap.vertices[1], far_cap.vertices[0]),
                renderer.sub(far_cap.vertices[2], far_cap.vertices[0]),
            )
        )

        self.assertGreater(renderer.dot(near_normal, (1, 0, 0)), 0.999)
        self.assertGreater(renderer.dot(far_normal, (-1, 0, 0)), 0.999)

    def test_outer_and_inner_rims_face_opposite_radial_directions(self):
        faces = []
        renderer.add_annulus(
            faces,
            min_x=7.0,
            max_x=9.0,
            inner_radius=2.0,
            outer_radius=4.0,
            material="wheel",
            element="test",
            segments=8,
            radial_steps=1,
            texture_units=100,
        )

        outer_rim = faces[16]
        inner_rim = faces[24]

        def normal(face):
            return renderer.normalize(
                renderer.cross(
                    renderer.sub(face.vertices[1], face.vertices[0]),
                    renderer.sub(face.vertices[2], face.vertices[0]),
                )
            )

        self.assertGreater(renderer.dot(normal(outer_rim), (0, 0, 1)), 0.7)
        self.assertGreater(renderer.dot(normal(inner_rim), (0, 0, -1)), 0.7)


class CuboidWindingTests(unittest.TestCase):
    def test_all_six_named_faces_use_the_correct_plane_and_outward_normal(self):
        vertices = renderer.cuboid((1, 2, 3), (5, 7, 11))
        expected = {
            "north": ((2, 3), (0, 0, -1)),
            "east": ((0, 5), (1, 0, 0)),
            "south": ((2, 11), (0, 0, 1)),
            "west": ((0, 1), (-1, 0, 0)),
            "up": ((1, 7), (0, 1, 0)),
            "down": ((1, 2), (0, -1, 0)),
        }

        for direction, (plane, outward) in expected.items():
            with self.subTest(direction=direction):
                face = [vertices[index] for index in renderer.FACE_INDICES[direction]]
                axis, coordinate = plane
                self.assertTrue(all(vertex[axis] == coordinate for vertex in face))
                normal = renderer.normalize(
                    renderer.cross(
                        renderer.sub(face[1], face[0]),
                        renderer.sub(face[2], face[0]),
                    )
                )
                self.assertGreater(renderer.dot(normal, outward), 0.999)


class RenderMatrixTests(unittest.TestCase):
    def test_three_modes_cover_six_profiles_and_opposing_isometrics(self):
        self.assertEqual(
            [
                "front",
                "back",
                "right",
                "left",
                "top",
                "bottom",
                "isometric",
                "isometric-opposite",
            ],
            list(renderer.VIEWS),
        )
        self.assertEqual(("wireframe", "material", "textured"), renderer.RENDER_MODES)
        self.assertEqual(24, len(renderer.VIEWS) * len(renderer.RENDER_MODES))

    def test_game_domain_texture_resolves_from_survival_content_pack(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            texture = root / "survival" / "textures" / "block" / "test.png"
            texture.parent.mkdir(parents=True)
            Image.new("RGB", (2, 2), (1, 2, 3)).save(texture)

            self.assertEqual(texture, renderer.resolve_texture("game:block/test", [root]))

    def test_orbit_view_completes_one_seamless_revolution(self):
        start = (1, 0.78, 1)
        quarter = renderer.orbit_view(start, 0.25)
        complete = renderer.orbit_view(start, 1)
        views = [
            renderer.normalize(renderer.orbit_view(start, frame / 120))
            for frame in range(120)
        ]

        self.assertAlmostEqual(1, quarter[0])
        self.assertAlmostEqual(0.78, quarter[1])
        self.assertAlmostEqual(-1, quarter[2])
        for actual, expected in zip(complete, start):
            self.assertAlmostEqual(expected, actual)
        self.assertAlmostEqual(
            renderer.dot(views[0], views[1]),
            renderer.dot(views[-1], views[0]),
        )

    def test_top_and_bottom_orbits_move_off_the_poles_and_close_the_loop(self):
        for view_name in ("top", "bottom"):
            with self.subTest(view_name=view_name):
                start = renderer.VIEWS[view_name][0]
                quarter = renderer.orbit_view(start, 0.25)
                complete = renderer.orbit_view(start, 1)

                self.assertNotEqual(start, quarter)
                self.assertAlmostEqual(0, quarter[1])
                for actual, expected in zip(complete, start):
                    self.assertAlmostEqual(expected, actual)

    def test_multiple_shapes_preserve_same_named_material_texture_bindings(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            assets = root / "assets"
            red = assets / "fixture" / "textures" / "red.png"
            blue = assets / "fixture" / "textures" / "blue.png"
            red.parent.mkdir(parents=True)
            Image.new("RGBA", (1, 1), (255, 0, 0, 255)).save(red)
            Image.new("RGBA", (1, 1), (0, 0, 255, 255)).save(blue)

            for name, offset in (("red", 0), ("blue", 2)):
                (root / f"{name}.json").write_text(json.dumps({
                    "textures": {"surface": f"fixture:{name}"},
                    "elements": [{
                        "name": name,
                        "from": [offset, 0, 0],
                        "to": [offset + 1, 1, 1],
                        "faces": {"north": {"texture": "#surface"}},
                    }],
                }), encoding="utf-8")
            manifest = root / "manifest.json"
            manifest.write_text(json.dumps({
                "name": "texture-binding-fixture",
                "shapes": ["red.json", "blue.json"],
            }), encoding="utf-8")
            output = root / "output"

            renderer.main([
                "--manifest", str(manifest),
                "--output-dir", str(output),
                "--assets-root", str(assets),
                "--size", "64",
            ])
            metadata = json.loads((output / "render-metadata.json").read_text(encoding="utf-8"))

            self.assertEqual(set(metadata["resolvedTextures"].values()), {str(red), str(blue)})
            self.assertEqual([], metadata["unresolvedTextures"])
            provenance = {entry["path"]: entry["sha256"] for entry in metadata["inputs"]}
            self.assertEqual(renderer.sha256(red), provenance[str(red)])
            self.assertEqual(renderer.sha256(blue), provenance[str(blue)])

    def test_animation_sampling_interpolates_source_frames_at_higher_output_rate(self):
        positions = renderer.animation_sample_positions(30, 60, 30)

        self.assertEqual(60, len(positions))
        self.assertEqual([0, 0.5, 1, 1.5], positions[:4])
        self.assertEqual(29.5, positions[-1])

    def test_static_turntable_frame_count_preserves_requested_duration(self):
        self.assertEqual(720, renderer.turntable_frame_count(60, 12))

    def test_frame_directory_removes_only_stale_numbered_pngs(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "walk.mp4"
            frames = output.parent / "walk-frames"
            frames.mkdir()
            (frames / "0000.png").write_bytes(b"old")
            (frames / "0099.png").write_bytes(b"old")
            (frames / "notes.png").write_bytes(b"keep")

            self.assertEqual(frames, renderer.prepare_frame_directory(output))
            self.assertFalse((frames / "0000.png").exists())
            self.assertFalse((frames / "0099.png").exists())
            self.assertTrue((frames / "notes.png").exists())


class HierarchicalShapeTests(unittest.TestCase):
    @staticmethod
    def load_shape(data, animation_code=None, animation_frame=0):
        with tempfile.TemporaryDirectory() as directory:
            shape = Path(directory) / "shape.json"
            shape.write_text(json.dumps(data), encoding="utf-8")
            return renderer.load_shape(shape, animation_code, animation_frame)

    def test_child_coordinates_are_relative_to_parent_from(self):
        faces, _ = self.load_shape({
            "elements": [{
                "name": "parent",
                "from": [10, 20, 30],
                "to": [12, 22, 32],
                "faces": {},
                "children": [{
                    "name": "child",
                    "from": [1, 2, 3],
                    "to": [2, 3, 4],
                    "faces": {"north": {"texture": "#skin"}},
                }],
            }],
        })

        vertices = faces[0].vertices
        self.assertEqual((11, 22, 33), vertices[0])
        self.assertEqual((12, 23, 33), vertices[2])

    def test_parent_rotation_is_composed_around_parent_origin(self):
        faces, _ = self.load_shape({
            "elements": [{
                "name": "parent",
                "from": [10, 0, 0],
                "to": [12, 2, 2],
                "rotationOrigin": [10, 0, 0],
                "rotationY": 90,
                "faces": {},
                "children": [{
                    "name": "child",
                    "from": [1, 0, 0],
                    "to": [2, 1, 1],
                    "faces": {"north": {"texture": "#skin"}},
                }],
            }],
        })

        vertices = faces[0].vertices
        self.assertAlmostEqual(10, vertices[0][0])
        self.assertAlmostEqual(0, vertices[0][1])
        self.assertAlmostEqual(-1, vertices[0][2])
        self.assertAlmostEqual(10, vertices[3][0])
        self.assertAlmostEqual(-2, vertices[3][2])

    def test_disabled_faces_are_not_rendered(self):
        faces, _ = self.load_shape({
            "elements": [{
                "name": "element",
                "from": [0, 0, 0],
                "to": [1, 1, 1],
                "faces": {
                    "north": {"texture": "#skin", "enabled": False},
                    "south": {"texture": "#skin"},
                },
            }],
        })

        self.assertEqual(["south"], [face.surface for face in faces])

    def test_animation_pose_interpolates_and_wraps_channels_independently(self):
        data = {
            "animations": [{
                "code": "walk",
                "quantityframes": 20,
                "keyframes": [
                    {"frame": 0, "elements": {"Body": {
                        "offsetX": 0, "offsetY": 0, "offsetZ": 0,
                        "rotationX": 0, "rotationY": 0, "rotationZ": 0,
                    }}},
                    {"frame": 10, "elements": {"Body": {
                        "offsetX": 10, "offsetY": 0, "offsetZ": 0,
                    }}},
                ],
            }],
        }

        midpoint = renderer.sample_animation_pose(data, "walk", 5)["Body"]
        wrapped = renderer.sample_animation_pose(data, "walk", 15)["Body"]

        self.assertEqual((5, 0, 0), midpoint["offset"])
        self.assertEqual((5, 0, 0), wrapped["offset"])
        self.assertEqual((0, 0, 0), midpoint.get("rotation", (0, 0, 0)))

    def test_animation_pose_honors_per_axis_shortest_rotation(self):
        data = {
            "animations": [{
                "code": "turn",
                "quantityframes": 20,
                "keyframes": [
                    {"frame": 0, "elements": {"Body": {
                        "rotationX": 350,
                        "rotationY": 350,
                        "rotShortestDistanceX": True,
                    }}},
                    {"frame": 10, "elements": {"Body": {
                        "rotationX": 10,
                        "rotationY": 10,
                        "rotShortestDistanceX": True,
                    }}},
                ],
            }],
        }

        midpoint = renderer.sample_animation_pose(data, "turn", 5)["Body"]["rotation"]
        wrapped = renderer.sample_animation_pose(data, "turn", 15)["Body"]["rotation"]

        self.assertEqual((360, 180, 0), midpoint)
        self.assertEqual((0, 180, 0), wrapped)

    def test_animation_pose_interpolates_axes_on_their_own_keyframes(self):
        data = {
            "animations": [{
                "code": "independent-axes",
                "quantityframes": 20,
                "keyframes": [
                    {"frame": 0, "elements": {"Body": {"rotationX": 0}}},
                    {"frame": 5, "elements": {"Body": {"rotationY": 100}}},
                    {"frame": 10, "elements": {"Body": {"rotationX": 10}}},
                    {"frame": 15, "elements": {"Body": {"rotationY": 200}}},
                ],
            }],
        }

        rotation = renderer.sample_animation_pose(
            data,
            "independent-axes",
            7.5,
        )["Body"]["rotation"]

        self.assertEqual((7.5, 125, 0), rotation)

    def test_animated_parent_rotation_moves_child(self):
        faces, _ = self.load_shape({
            "elements": [{
                "name": "parent",
                "from": [0, 0, 0],
                "to": [1, 1, 1],
                "rotationOrigin": [5, 0, 0],
                "faces": {},
                "children": [{
                    "name": "child",
                    "from": [6, 0, 0],
                    "to": [7, 1, 1],
                    "faces": {"north": {"texture": "#skin"}},
                }],
            }],
            "animations": [{
                "code": "walk",
                "quantityframes": 2,
                "keyframes": [
                    {"frame": 0, "elements": {"parent": {
                        "rotationX": 0, "rotationY": 0, "rotationZ": 90,
                    }}},
                ],
            }],
        }, "walk", 0)

        self.assertAlmostEqual(5, faces[0].vertices[0][0])
        self.assertAlmostEqual(1, faces[0].vertices[0][1])


class DepthBufferTests(unittest.TestCase):
    def assert_crossing_depth_faces_render_per_pixel(self, view_name):
        view = renderer.normalize(renderer.VIEWS[view_name][0])
        nominal_up = renderer.VIEWS[view_name][1]
        right = renderer.normalize(renderer.cross(nominal_up, view))
        up = renderer.normalize(renderer.cross(view, right))

        def point(depth, horizontal, vertical):
            return renderer.add(
                renderer.mul(view, depth),
                renderer.add(
                    renderer.mul(right, horizontal),
                    renderer.mul(up, vertical),
                ),
            )

        sloped = renderer.Face(
            [
                point(0, 0, 0),
                point(0, 1, 0),
                point(10, 0, 1),
            ],
            "sloped",
            "sloped",
        )
        level = renderer.Face(
            [
                point(4, 0, 0),
                point(4, 1, 0),
                point(4, 0, 1),
            ],
            "level",
            "level",
        )

        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / f"{view_name}.png"
            renderer.render(
                [sloped, level],
                {"sloped": (220, 20, 20), "level": (20, 220, 20)},
                {},
                view_name,
                "material",
                output,
                200,
            )
            image = Image.open(output).convert("RGB")
            _, projected_right, projected_up, center, scale = renderer.projection(
                [sloped, level],
                view_name,
                200,
            )

            def sample(horizontal, vertical):
                screen = renderer.screen_point(
                    point(0, horizontal, vertical),
                    center,
                    projected_right,
                    projected_up,
                    200,
                    scale,
                )
                return image.getpixel((round(screen[0]), round(screen[1])))

            self.assertEqual((220, 20, 20), sample(0.1, 0.8))
            self.assertEqual((20, 220, 20), sample(0.1, 0.1))

    def test_crossing_depth_faces_render_correctly_from_front(self):
        self.assert_crossing_depth_faces_render_per_pixel("front")

    def test_crossing_depth_faces_render_correctly_from_isometric(self):
        self.assert_crossing_depth_faces_render_per_pixel("isometric")

    def test_crossing_depth_faces_render_correctly_from_opposing_isometric(self):
        self.assert_crossing_depth_faces_render_per_pixel("isometric-opposite")

    def test_fully_transparent_texture_does_not_write_color_or_depth(self):
        pixels = np.full((4, 4, 3), (10, 20, 30), dtype=np.uint8)
        depths = np.full((4, 4), -np.inf)
        texture = Image.new("RGBA", (1, 1), (255, 0, 0, 0))

        renderer.rasterize_triangle(
            pixels,
            depths,
            [(0, 0), (4, 0), (0, 4)],
            [1, 1, 1],
            (255, 0, 0),
            texture,
            [(0, 0), (0, 0), (0, 0)],
        )

        self.assertTrue(np.all(pixels == (10, 20, 30)))
        self.assertTrue(np.all(np.isneginf(depths)))

    def test_fully_transparent_textured_face_does_not_draw_an_outline(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "transparent.png"
            face = renderer.Face(
                [(1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)],
                "surface",
                "transparent",
            )
            renderer.render(
                [face],
                {"surface": (255, 0, 0)},
                {"surface": Image.new("RGBA", (1, 1), (255, 0, 0, 0))},
                "front",
                "textured",
                output,
                64,
            )

            pixels = np.asarray(Image.open(output))
            self.assertTrue(np.all(pixels[45:, :, :] == (28, 31, 34)))

    def test_partial_alpha_composites_over_opaque_geometry_regardless_of_face_order(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "translucent.png"
            rear = renderer.Face(
                [(0, 0, 0), (0, 1, 0), (0, 1, 1), (0, 0, 1)],
                "rear",
                "rear",
            )
            front = renderer.Face(
                [(1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)],
                "front",
                "front",
            )
            renderer.render(
                [front, rear],
                {"front": (255, 0, 0), "rear": (0, 255, 0)},
                {
                    "front": Image.new("RGBA", (1, 1), (255, 0, 0, 128)),
                    "rear": Image.new("RGBA", (1, 1), (0, 255, 0, 255)),
                },
                "front",
                "textured",
                output,
                128,
            )

            pixels = np.asarray(Image.open(output))[45:, :, :]
            blended = pixels[(pixels[..., 0] > 50) & (pixels[..., 1] > 50)]
            self.assertGreater(len(blended), 0)


class AnimationProjectionTests(unittest.TestCase):
    def test_top_and_bottom_views_use_a_nonparallel_up_vector(self):
        face = renderer.Face(
            [(0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)],
            "wood",
            "test",
        )

        for view_name in ("top", "bottom"):
            with self.subTest(view_name=view_name):
                projection = renderer.fixed_animation_projections(
                    [[face]],
                    [renderer.VIEWS[view_name][0]],
                    100,
                )[0]
                view, right, up, _, _ = projection
                self.assertAlmostEqual(0, renderer.dot(view, right))
                self.assertAlmostEqual(0, renderer.dot(view, up))

    def test_polar_orbit_camera_basis_stays_continuous_through_the_poles(self):
        face = renderer.Face(
            [(0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)],
            "wood",
            "test",
        )
        frame_count = 120
        for view_name in ("top", "bottom"):
            with self.subTest(view_name=view_name):
                views = [
                    renderer.orbit_view(renderer.VIEWS[view_name][0], frame / frame_count)
                    for frame in range(frame_count)
                ]
                projections = renderer.fixed_animation_projections(
                    [[face]] * frame_count,
                    views,
                    100,
                    [renderer.VIEWS[view_name][1]] * frame_count,
                )
                rights = [projection[1] for projection in projections]
                ups = [projection[2] for projection in projections]

                for index in range(frame_count):
                    following = (index + 1) % frame_count
                    self.assertGreater(renderer.dot(rights[index], rights[following]), 0.99)
                    self.assertGreater(renderer.dot(ups[index], ups[following]), 0.99)


class UvTests(unittest.TestCase):
    def test_missing_cuboid_uv_uses_face_dimensions(self):
        uvs = renderer.face_uvs(
            {},
            texture_width=16,
            texture_height=16,
            direction="north",
            start=(1, 2, 3),
            end=(5, 8, 11),
        )

        self.assertEqual([(0, 0.375), (0, 0), (0.25, 0), (0.25, 0.375)], uvs)

    def test_uv_corners_follow_each_cuboid_faces_vertex_order(self):
        expected_forward = [(0, 0.25), (0, 0), (0.5, 0), (0.5, 0.25)]
        expected_reversed = [(0, 0.25), (0.5, 0.25), (0.5, 0), (0, 0)]

        for direction in ("north", "east", "up"):
            with self.subTest(direction=direction):
                self.assertEqual(
                    expected_forward,
                    renderer.face_uvs(
                        {"uv": [0, 0, 8, 4]},
                        16,
                        16,
                        direction,
                        (0, 0, 0),
                        (2, 3, 5),
                    ),
                )

        for direction in ("south", "west", "down"):
            with self.subTest(direction=direction):
                self.assertEqual(
                    expected_reversed,
                    renderer.face_uvs(
                        {"uv": [0, 0, 8, 4]},
                        16,
                        16,
                        direction,
                        (0, 0, 0),
                        (2, 3, 5),
                    ),
                )

    def test_face_rotation_cycles_uv_corners(self):
        unrotated = renderer.face_uvs(
            {"uv": [0, 0, 8, 4]},
            16,
            16,
            "north",
            (0, 0, 0),
            (1, 1, 1),
        )
        rotated = renderer.face_uvs(
            {"uv": [0, 0, 8, 4], "rotation": 90},
            16,
            16,
            "north",
            (0, 0, 0),
            (1, 1, 1),
        )

        self.assertEqual(unrotated[-1:] + unrotated[:-1], rotated)


class RegistrationMarkWindingTests(unittest.TestCase):
    def test_procedural_spokes_follow_the_authored_integer_count(self):
        root = Path(__file__).parents[4]
        dimensions = root / "mods-dll" / "flywheelpower" / "src" / "FlywheelModelDimensions.cs"
        with tempfile.TemporaryDirectory() as directory:
            changed = Path(directory) / "FlywheelModelDimensions.cs"
            renderer_source = dimensions.with_name("FlywheelMechBlockRenderer.cs")
            changed.write_text(
                dimensions.read_text(encoding="utf-8").replace(
                    "internal const int SpokeCount = 8;",
                    "internal const int SpokeCount = 6;",
                ),
                encoding="utf-8",
            )
            changed.with_name("FlywheelMechBlockRenderer.cs").write_text(
                renderer_source.read_text(encoding="utf-8"),
                encoding="utf-8",
            )
            faces = renderer.load_flywheel(changed, "full")

        spoke_elements = {
            face.element
            for face in faces
            if face.element.startswith("RuntimeWoodSpoke")
        }
        self.assertEqual(6, len(spoke_elements))

    def test_procedural_disc_matches_runtime_radial_cells_and_planar_uvs(self):
        root = Path(__file__).parents[4]
        dimensions = root / "mods-dll" / "flywheelpower" / "src" / "FlywheelModelDimensions.cs"
        faces = renderer.load_flywheel(dimensions, "compact")
        values = renderer.constants(dimensions)
        runtime_values = renderer.constants(dimensions.with_name("FlywheelMechBlockRenderer.cs"))
        wheel_max = 8 + values["CompactWheelHalfThickness"] * 16
        texture_units = runtime_values["TextureMeters"] * 16
        front = [
            face
            for face in faces
            if face.element == "RuntimeWheel"
            and all(abs(vertex[0] - wheel_max) < 1e-9 for vertex in face.vertices)
        ]

        self.assertEqual(
            int(runtime_values["WheelSegments"] * runtime_values["WheelRadialSteps"]),
            len(front),
        )
        for face in front:
            expected = renderer.planar_uvs(face.vertices, texture_units)
            for actual_uv, expected_uv in zip(face.uvs, expected):
                self.assertAlmostEqual(expected_uv[0], actual_uv[0])
                self.assertAlmostEqual(expected_uv[1], actual_uv[1])

    def test_front_and_back_marks_face_opposite_directions(self):
        root = Path(__file__).parents[4]
        dimensions = root / "mods-dll" / "flywheelpower" / "src" / "FlywheelModelDimensions.cs"
        faces = renderer.load_flywheel(dimensions, "full")
        front = next(face for face in faces if face.element == "RegistrationMarkFaceFront")
        back = next(face for face in faces if face.element == "RegistrationMarkFaceBack")

        def normal(face):
            return renderer.normalize(
                renderer.cross(
                    renderer.sub(face.vertices[1], face.vertices[0]),
                    renderer.sub(face.vertices[2], face.vertices[0]),
                )
            )

        self.assertGreater(renderer.dot(normal(front), (1, 0, 0)), 0.999)
        self.assertGreater(renderer.dot(normal(back), (-1, 0, 0)), 0.999)

    def test_compact_and_full_marks_wrap_the_corner_with_physical_scale_uvs(self):
        root = Path(__file__).parents[4]
        dimensions = root / "mods-dll" / "flywheelpower" / "src" / "FlywheelModelDimensions.cs"
        expected = {
            "compact": (0.05 / 0.72, (0.32 + 0.024) / 0.72),
            "full": (0.08 / 0.72, (0.1875 + 0.024) / 0.72),
        }

        for size, (expected_u, expected_v) in expected.items():
            with self.subTest(size=size):
                faces = renderer.load_flywheel(dimensions, size)
                front = next(face for face in faces if face.element == "RegistrationMarkFaceFront")
                back = next(face for face in faces if face.element == "RegistrationMarkFaceBack")
                rim = next(face for face in faces if face.element == "RegistrationMarkRim")

                self.assertGreaterEqual(max(vertex[2] for vertex in front.vertices), max(vertex[2] for vertex in rim.vertices))
                self.assertGreaterEqual(max(vertex[2] for vertex in back.vertices), max(vertex[2] for vertex in rim.vertices))
                self.assertLessEqual(min(vertex[0] for vertex in rim.vertices), max(vertex[0] for vertex in back.vertices))
                self.assertGreaterEqual(max(vertex[0] for vertex in rim.vertices), min(vertex[0] for vertex in front.vertices))

                self.assertAlmostEqual(expected_u, max(uv[0] for uv in rim.uvs) - min(uv[0] for uv in rim.uvs))
                self.assertAlmostEqual(expected_v, max(uv[1] for uv in rim.uvs) - min(uv[1] for uv in rim.uvs))
                self.assertAlmostEqual(expected_u, max(uv[0] for uv in front.uvs) - min(uv[0] for uv in front.uvs))


class CoplanarOverlapTests(unittest.TestCase):
    @staticmethod
    def face(
        element,
        x0,
        y0,
        x1,
        y1,
        z=0,
        reverse=False,
    ):
        vertices = [(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)]
        if reverse:
            vertices.reverse()
        return renderer.Face(vertices, "wood", element, surface="north", source="test")

    def test_detects_positive_area_overlap_between_same_facing_coplanar_faces(self):
        overlaps = renderer.find_coplanar_overlaps([
            self.face("first", 0, 0, 2, 2),
            self.face("second", 1, 1, 3, 3),
        ])

        self.assertEqual(1, len(overlaps))
        self.assertAlmostEqual(1, overlaps[0].overlap_area)

    def test_detects_overlap_between_primitives_that_share_an_element_name(self):
        overlaps = renderer.find_coplanar_overlaps([
            self.face("duplicate", 0, 0, 2, 2),
            self.face("duplicate", 1, 1, 3, 3),
        ])

        self.assertEqual(1, len(overlaps))
        self.assertAlmostEqual(1, overlaps[0].overlap_area)

    def test_shared_edge_does_not_count_as_overlap(self):
        overlaps = renderer.find_coplanar_overlaps([
            self.face("first", 0, 0, 1, 1),
            self.face("second", 1, 0, 2, 1),
        ])

        self.assertEqual([], overlaps)

    def test_opposite_facing_internal_joint_does_not_count_as_z_fighting(self):
        overlaps = renderer.find_coplanar_overlaps([
            self.face("first", 0, 0, 2, 2),
            self.face("second", 1, 1, 3, 3, reverse=True),
        ])

        self.assertEqual([], overlaps)

    def test_small_plane_offset_clears_overlap(self):
        overlaps = renderer.find_coplanar_overlaps([
            self.face("first", 0, 0, 2, 2),
            self.face("second", 1, 1, 3, 3, z=0.125),
        ])

        self.assertEqual([], overlaps)

    def test_authored_flywheel_brace_offsets_clear_coplanar_overlaps(self):
        root = Path(__file__).parents[4]
        shape = (
            root
            / "mods-dll"
            / "flywheelpower"
            / "assets"
            / "flywheelpower"
            / "shapes"
            / "block"
            / "flywheel-frame-horizontal.json"
        )
        faces, _ = renderer.load_shape(shape)
        pairs = {
            frozenset((overlap.first_element, overlap.second_element))
            for overlap in renderer.find_coplanar_overlaps(faces)
        }

        self.assertNotIn(frozenset(("LeftFrontBrace", "LeftRearBrace")), pairs)
        self.assertNotIn(frozenset(("RightFrontBrace", "RightRearBrace")), pairs)
        self.assertEqual(set(), pairs)


if __name__ == "__main__":
    unittest.main()

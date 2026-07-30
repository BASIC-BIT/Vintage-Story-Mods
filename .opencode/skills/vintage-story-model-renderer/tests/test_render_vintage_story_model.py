import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image


SCRIPT = Path(__file__).parents[1] / "scripts" / "render_vintage_story_model.py"
SPEC = importlib.util.spec_from_file_location("render_vintage_story_model", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
renderer = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = renderer
SPEC.loader.exec_module(renderer)


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
        )

        outer_rim = faces[2]
        inner_rim = faces[3]

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

import importlib.util
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

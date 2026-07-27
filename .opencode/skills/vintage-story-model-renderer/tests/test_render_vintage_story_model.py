import importlib.util
import sys
import unittest
from pathlib import Path


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


if __name__ == "__main__":
    unittest.main()

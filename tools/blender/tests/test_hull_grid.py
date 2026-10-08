"""Blender-free tests for aetheria_ships/hull_grid.py, the arithmetic behind New Ship, Rasterise and the Grid object.

    python -m unittest discover -s tools/blender/tests

hull_grid is loaded by path because the package __init__ imports bpy. Frame: nose toward -Y, starboard toward -X, cell
(x, y) centred on grid_origin + (-2x, -2y) metres.
"""

import importlib.util
import unittest
from pathlib import Path

_spec = importlib.util.spec_from_file_location(
    "hull_grid", Path(__file__).resolve().parents[1] / "aetheria_ships" / "hull_grid.py")
hull_grid = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(hull_grid)


def rect(x0, y0, x1, y1):
    return [((x0, y0), (x1, y0), (x1, y1)), ((x0, y0), (x1, y1), (x0, y1))]


def occupied(cells, height):
    return {(i // height, i % height) for i, cell in enumerate(cells) if cell}


class CellMappingTests(unittest.TestCase):
    ORIGIN = (3.0, -5.0)

    def test_every_cell_of_a_5x3_grid_maps_back_to_itself(self):
        for x in range(5):
            for y in range(3):
                centre = hull_grid.cell_centre(self.ORIGIN, x, y)
                self.assertEqual(hull_grid.cell_at(self.ORIGIN, centre), (x, y))

    def test_cell_1_0_is_two_metres_toward_minus_x_and_0_1_toward_minus_y(self):
        origin = hull_grid.cell_centre(self.ORIGIN, 0, 0)
        self.assertEqual(origin, self.ORIGIN)
        self.assertEqual(hull_grid.cell_centre(self.ORIGIN, 1, 0), (1.0, -5.0))
        self.assertEqual(hull_grid.cell_centre(self.ORIGIN, 0, 1), (3.0, -7.0))
        self.assertEqual(hull_grid.cell_centre(self.ORIGIN, 4, 2), (-5.0, -9.0))

    def test_cell_at_takes_the_nearest_centre_and_the_lower_cell_on_a_tie(self):
        near = lambda dx, dy: hull_grid.cell_at(self.ORIGIN, (self.ORIGIN[0] - 2 + dx, self.ORIGIN[1] - 4 + dy))
        self.assertEqual(near(0.9, -0.9), (1, 2))
        self.assertEqual(near(-0.9, 0.9), (1, 2))
        self.assertEqual(near(1.1, 0), (0, 2))
        self.assertEqual(near(-1.1, 0), (2, 2))
        self.assertEqual(near(0, 1.1), (1, 1))
        self.assertEqual(near(0, -1.1), (1, 3))
        self.assertEqual(near(1.0, 1.0), (0, 1))
        self.assertEqual(near(-1.0, -1.0), (1, 2))


class RasteriseTests(unittest.TestCase):
    def test_eight_of_sixteen_samples_occupy_a_cell_and_seven_do_not(self):
        wing = rect(-2, -1, 0, 1)  # cell (1, 0) in full; fixes the grid at 2 x 1 cells about the origin
        eight = rect(0, -1, 2, 0)
        seven = rect(0, -1, 2, -0.5) + rect(0.5, -0.5, 2, 0)
        for triangles, expected in ((eight, True), (seven, False)):
            width, height, cells, origin = hull_grid.rasterise(wing + triangles)
            self.assertEqual((width, height, origin), (2, 1, (1.0, 0.0)))
            self.assertEqual(cells, [expected, True])

    def test_a_sample_inside_two_triangles_counts_once(self):
        seven = rect(0, -1, 2, -0.5) + rect(0.5, -0.5, 2, 0)
        _, _, cells, _ = hull_grid.rasterise(rect(-2, -1, 0, 1) + seven + seven)
        self.assertEqual(cells, [False, True])

    def test_an_l_shape_gives_the_cells_of_the_l(self):
        width, height, cells, origin = hull_grid.rasterise(rect(-2, -2, 2, 0) + rect(0, 0, 2, 2))
        self.assertEqual((width, height, origin), (2, 2, (1.0, 1.0)))
        self.assertEqual(occupied(cells, height), {(0, 0), (0, 1), (1, 1)})
        self.assertEqual(cells, [True, True, False, True])

    def test_winding_does_not_matter(self):
        flipped = [(a, c, b) for a, b, c in rect(-2, -2, 2, 0) + rect(0, 0, 2, 2)]
        self.assertEqual(hull_grid.rasterise(flipped)[2], [True, True, False, True])

    def test_the_grid_is_ceil_of_the_extent_over_two_metres_on_each_axis(self):
        for extent, cells_across in ((1.0, 1), (2.0, 1), (2.01, 2), (4.0, 2), (4.0000001, 2), (4.0005, 2), (4.002, 3), (4.01, 3), (5.0, 3), (20.0, 10)):
            width, height, _, _ = hull_grid.rasterise(rect(0, 0, extent, 6.5))
            self.assertEqual((width, height), (cells_across, 4), extent)

    def test_the_grid_is_centred_on_the_silhouette(self):
        width, height, cells, origin = hull_grid.rasterise(rect(10, 20, 13, 22))
        self.assertEqual((width, height), (2, 1))
        self.assertEqual(origin, (12.5, 21.0))
        self.assertEqual(cells, [True, True])

    def test_cell_centres_from_the_origin_lie_in_the_cells_they_name(self):
        width, height, cells, origin = hull_grid.rasterise(rect(-2, -2, 2, 0) + rect(0, 0, 2, 2))
        for x in range(width):
            for y in range(height):
                cx, cy = hull_grid.cell_centre(origin, x, y)
                self.assertEqual(hull_grid.cell_at(origin, (cx, cy)), (x, y))
                inside = any(hull_grid._inside(tri, cx, cy) for tri in rect(-2, -2, 2, 0) + rect(0, 0, 2, 2))
                self.assertEqual(inside, cells[x * height + y])

    def test_samples_per_axis_and_cell_size_are_parameters(self):
        triangles = rect(-1, -1, 1, 0)
        self.assertEqual(hull_grid.rasterise(triangles, samples=1)[2], [True])
        self.assertEqual(hull_grid.rasterise(triangles, cell_size=1.0)[:2], (2, 1))

    def test_thirty_two_cells_are_accepted_and_thirty_three_is_refused_naming_it(self):
        width, height, cells, _ = hull_grid.rasterise(rect(0, 0, 2, 64))
        self.assertEqual((width, height, len(cells), all(cells)), (1, 32, 32, True))
        with self.assertRaises(ValueError) as caught:
            hull_grid.rasterise(rect(0, 0, 2, 66))
        self.assertIn("33", str(caught.exception))
        with self.assertRaises(ValueError) as caught:
            hull_grid.rasterise(rect(0, 0, 66, 2))
        self.assertIn("33", str(caught.exception))

    def test_a_silhouette_with_no_area_is_refused(self):
        with self.assertRaises(ValueError):
            hull_grid.rasterise([])
        with self.assertRaises(ValueError):
            hull_grid.rasterise([((0, 0), (1, 1), (2, 2))])

    def test_a_degenerate_triangle_does_not_widen_the_grid(self):
        _, _, cells, _ = hull_grid.rasterise(rect(0, 0, 2, 2) + [((50, 50), (60, 60), (70, 70))])
        self.assertEqual(cells, [True])


if __name__ == "__main__":
    unittest.main()

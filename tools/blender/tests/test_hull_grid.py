"""Blender-free tests for aetheria_ships/hull_grid.py, the arithmetic behind New Ship, Rasterise and the Grid object.

    python -m unittest discover -s tools/blender/tests

hull_grid is loaded by path because the package __init__ imports bpy. Frame: nose toward -Y, starboard toward -X, cell
(x, y) centred on grid_origin + (-2x, -2y) metres.
"""

import importlib.util
import unittest
from pathlib import Path

# The dotted name is the module's path from the repo root, which mutmut keys its mutants by.
_spec = importlib.util.spec_from_file_location(
    "tools.blender.aetheria_ships.hull_grid", Path(__file__).resolve().parents[1] / "aetheria_ships" / "hull_grid.py")
hull_grid = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(hull_grid)


def rect(x0, y0, x1, y1):
    return [((x0, y0), (x1, y0), (x1, y1)), ((x0, y0), (x1, y1), (x0, y1))]


CORNER = [((1.99, 0.98), (2.0, 0.98), (2.0, 1.0))]  # holds the bounding box at x 2 without covering a sample


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


class InsideTests(unittest.TestCase):
    TRIANGLE = ((0, 0), (1, 0), (0, 1))

    def test_inside_points_are_inside_for_both_windings(self):
        for triangle in (self.TRIANGLE, self.TRIANGLE[::-1]):
            for point in ((0.25, 0.25), (0.5, 0.25), (0.1, 0.8)):
                self.assertTrue(hull_grid._inside(triangle, *point), (triangle, point))

    def test_points_across_each_edge_are_outside_for_both_windings(self):
        outside = ((0.5, -0.25), (0.75, 0.75), (-0.25, 0.5), (-0.5, -0.5), (1.5, 0.0), (0.0, 1.5), (2.0, 2.0))
        for triangle in (self.TRIANGLE, self.TRIANGLE[::-1]):
            for point in outside:
                self.assertFalse(hull_grid._inside(triangle, *point), (triangle, point))

    def test_edges_and_corners_are_inside_for_both_windings(self):
        on_edge = ((0.5, 0.0), (0.0, 0.5), (0.5, 0.5), (0.0, 0.0), (1.0, 0.0), (0.0, 1.0))
        for triangle in (self.TRIANGLE, self.TRIANGLE[::-1]):
            for point in on_edge:
                self.assertTrue(hull_grid._inside(triangle, *point), (triangle, point))


class RasteriseTests(unittest.TestCase):
    def test_eight_of_sixteen_samples_occupy_a_cell_and_seven_do_not(self):
        wing = rect(-2, -1, 0, 1)  # cell (1, 0) in full; fixes the grid at 2 x 1 cells about the origin
        eight = rect(0, -1, 2, 0)
        seven = rect(0, -1, 2, -0.5) + rect(0.5, -0.5, 2, 0)
        for triangles, expected in ((eight, True), (seven, False)):
            width, height, cells, origin = hull_grid.rasterise(wing + triangles)
            self.assertEqual((width, height, origin), (2, 1, (1.0, 0.0)))
            self.assertEqual(cells, [expected, True])

    def test_samples_sit_at_quarter_points_of_the_cell_on_each_axis(self):
        wing = rect(-2, -1, 0, 1)
        # Cell (0, 0) spans x 0..2, y -1..1; its sample rows are y = 0.75, 0.25, -0.25, -0.75 and columns x = 1.75 .. 0.25.
        rows = hull_grid.rasterise(wing + rect(0, -0.3, 2, 0.3))[2]  # y = 0.25 and -0.25: 8 samples
        columns = hull_grid.rasterise(wing + rect(0.3, -1, 1.0, 1) + CORNER)[2]  # x = 0.75 only: 4 samples
        self.assertEqual((rows, columns), ([True, True], [False, True]))

    def test_a_sample_on_a_triangle_bound_counts(self):
        wing = rect(-2, -1, 0, 1)
        self.assertEqual(hull_grid.rasterise(wing + rect(0, 0.25, 2, 0.75))[2], [True, True])  # rows 0.75 and 0.25
        self.assertEqual(hull_grid.rasterise(wing + rect(0.25, -1, 0.75, 1) + CORNER)[2], [True, True])  # columns 0.75 and 0.25

    def test_a_single_triangle_covers_only_its_own_half_of_its_bounding_box(self):
        width, height, cells, _ = hull_grid.rasterise([((-2, -2), (2, -2), (-2, 2))])
        self.assertEqual((width, height), (2, 2))
        self.assertEqual(cells, [False, True, True, True])

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
        self.assertEqual(str(caught.exception), "The hull needs 33 cells long; a ship grid holds at most 32")
        with self.assertRaises(ValueError) as caught:
            hull_grid.rasterise(rect(0, 0, 66, 2))
        self.assertEqual(str(caught.exception), "The hull needs 33 cells wide; a ship grid holds at most 32")

    def test_a_silhouette_with_no_area_is_refused(self):
        for triangles in ([], [((0, 0), (1, 1), (2, 2))]):
            with self.assertRaises(ValueError) as caught:
                hull_grid.rasterise(triangles)
            self.assertEqual(str(caught.exception), "The hull has no triangle with area to rasterise")

    def test_a_degenerate_triangle_does_not_widen_the_grid(self):
        for line in (((50, 50), (60, 60), (70, 70)), ((51, 50), (53, 51), (55, 52)), ((50, 51), (51, 53), (52, 55))):
            _, _, cells, _ = hull_grid.rasterise(rect(0, 0, 2, 2) + [line])
            self.assertEqual(cells, [True], line)


if __name__ == "__main__":
    unittest.main()

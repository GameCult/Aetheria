"""The ship grid's arithmetic: where a cell is, and which cells a hull's top-down silhouette occupies. Imports without bpy.

Frame: the Ship Root frame in metres, nose toward -Y and starboard toward -X (map M7). Cell (x, y) is centred on its
integer coordinate, so its centre is grid_origin + (-CELL_SIZE x, -CELL_SIZE y), where grid_origin is cell (0, 0)'s
centre (the Aetheria.Entity.ToWorldPoint rule: schematic +x is starboard, +y is forward). grid_origin is a stored fact
written only by Rasterise; every lookup below takes it as an argument.
"""

import math

CELL_SIZE = 2.0  # metres per cell, Settings.SchematicCellSize
MAX_CELLS = 32  # per axis, the layout panel's limit
SAMPLES = 4  # per axis per cell: 4 x 4 coverage samples
_EDGE = 1e-9  # slack for float noise on a sample bound, in sample steps
_TOLERANCE = 1e-3  # metres: mesh coordinates are float32, so an extent a millimetre over a cell boundary is on it


def cell_centre(grid_origin, x, y):
    """The Ship Root frame position of cell (x, y)'s centre."""
    return (grid_origin[0] - CELL_SIZE * x, grid_origin[1] - CELL_SIZE * y)


def cell_at(grid_origin, point):
    """The cell whose centre is nearest to point; a point midway between two centres belongs to the lower cell."""
    return (math.ceil((grid_origin[0] - point[0]) / CELL_SIZE - 0.5),
            math.ceil((grid_origin[1] - point[1]) / CELL_SIZE - 0.5))


def _cells_across(extent, cell_size):
    return max(1, math.ceil((extent - _TOLERANCE) / cell_size))


def _inside(triangle, px, py):
    (ax, ay), (bx, by), (cx, cy) = triangle
    d1 = (bx - ax) * (py - ay) - (by - ay) * (px - ax)
    d2 = (cx - bx) * (py - by) - (cy - by) * (px - bx)
    d3 = (ax - cx) * (py - cy) - (ay - cy) * (px - cx)
    return not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0))


def rasterise(triangles_xy, cell_size=CELL_SIZE, samples=SAMPLES):
    """Occupied cells of a top-down silhouette given as triangles of (x, y) points in the Ship Root frame.

    The grid is ceil(extent / cell_size) cells on each axis, centred on the silhouette's bounding box. Each cell is
    tested at samples x samples points; it is occupied when at least half of them fall in some triangle. A sample
    inside several triangles counts once, and each triangle is tested only at the samples inside its bounding box.
    Returns (width, height, cells, grid_origin): cells are column-major, cells[x * height + y], and grid_origin is cell
    (0, 0)'s centre."""
    triangles = [tri for tri in triangles_xy if
                 (tri[1][0] - tri[0][0]) * (tri[2][1] - tri[0][1]) != (tri[1][1] - tri[0][1]) * (tri[2][0] - tri[0][0])]
    if not triangles:
        raise ValueError("The hull has no triangle with area to rasterise")
    xs = [p[0] for tri in triangles for p in tri]
    ys = [p[1] for tri in triangles for p in tri]
    width = _cells_across(max(xs) - min(xs), cell_size)
    height = _cells_across(max(ys) - min(ys), cell_size)
    for axis, count in (("wide", width), ("long", height)):
        if count > MAX_CELLS:
            raise ValueError(f"The hull needs {count} cells {axis}; a ship grid holds at most {MAX_CELLS}")
    # Cell (0, 0)'s centre: the far (+X, +Y) corner of the grid, half a cell in.
    origin = ((min(xs) + max(xs)) / 2 + (width - 1) * cell_size / 2, (min(ys) + max(ys)) / 2 + (height - 1) * cell_size / 2)
    step = cell_size / samples
    # Sample (a, b) sits at origin + cell_size/2 - (a + 1/2) step on each axis, so it falls in cell (a // samples, b // samples).
    top_x, top_y = origin[0] + cell_size / 2, origin[1] + cell_size / 2

    def span(low, high, top, count):
        first = math.ceil((top - high) / step - 0.5 - _EDGE)
        last = math.floor((top - low) / step - 0.5 + _EDGE)
        return range(max(first, 0), min(last, count * samples - 1) + 1)

    hits = set()
    for tri in triangles:
        txs = [p[0] for p in tri]
        tys = [p[1] for p in tri]
        for a in span(min(txs), max(txs), top_x, width):
            px = top_x - (a + 0.5) * step
            for b in span(min(tys), max(tys), top_y, height):
                if (a, b) not in hits and _inside(tri, px, top_y - (b + 0.5) * step):
                    hits.add((a, b))
    counts = {}
    for a, b in hits:
        key = (a // samples, b // samples)
        counts[key] = counts.get(key, 0) + 1
    cells = [2 * counts.get((x, y), 0) >= samples * samples for x in range(width) for y in range(height)]
    return width, height, cells, origin

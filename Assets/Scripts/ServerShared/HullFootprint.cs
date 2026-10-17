/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using System.Linq;
using CultMath;

// A hardpoint's schematic footprint: its Shape laid over the hull's cells at its Position, in hull orientation.
// The footprint is authored for gameplay and never derived from the model or the Blender add-on
// (schematic-and-model-are-separate-spaces). This is the one reading of footprint cells in hull space and the one
// edit rule, so any host (the Studio drawer now, a Thing host later) edits footprints the same way.
public static class HullFootprint
{
    public static IEnumerable<int2> Cells(HardpointData hardpoint) =>
        hardpoint.Shape.Coordinates.Select(c => c + hardpoint.Position);

    // Toggles one hull cell of the footprint at `position`. A cell outside the footprint grows it toward any edge;
    // a cell inside is removed and empty border rows and columns are trimmed; every other cell keeps its hull
    // coordinate. Removing the last cell is refused (false, outputs are the inputs' values). Builds a new Shape and
    // never edits the one it is given.
    public static bool Toggle(Shape footprint, int2 position, int2 hullCell, out Shape shape, out int2 newPosition)
    {
        var cells = Cells(footprint, position);
        if (!cells.Remove(hullCell)) cells.Add(hullCell);
        else if (cells.Count == 0)
        {
            shape = footprint;
            newPosition = position;
            return false;
        }

        var min = cells.Aggregate((a, b) => new int2(math.min(a.x, b.x), math.min(a.y, b.y)));
        var max = cells.Aggregate((a, b) => new int2(math.max(a.x, b.x), math.max(a.y, b.y)));
        shape = new Shape(max.x - min.x + 1, max.y - min.y + 1);
        foreach (var cell in cells) shape[cell - min] = true;
        newPosition = min;
        return true;
    }

    private static HashSet<int2> Cells(Shape footprint, int2 position) =>
        new HashSet<int2>(footprint.Coordinates.Select(c => c + position));
}

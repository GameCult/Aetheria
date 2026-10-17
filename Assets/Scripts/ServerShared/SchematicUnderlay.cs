/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using System.Linq;
using CultMath;

// A mod hull's line-art schematic laid under its cell grids. The one rule for where the lines land: the line art's
// top-down bounding box is stretched, per axis, onto the hull Shape's cell rect [0, Width] x [0, Height] as the hull
// drawer sets it, never onto the occupied cells (underlay-fits-hull-bounds). Display only: nothing is derived from the
// model into the schematic or back (schematic-and-model-are-separate-spaces).
public static class SchematicUnderlay
{
    // Points are Blender Z-up XYZ in the Ship Root frame, nose toward -Y and starboard toward -X, so the schematic's
    // x (starboard right) is -X and its y (forward up) is -Y. Output is in hull cell units, index-aligned with `lines`.
    public static float2[][] Fit(IReadOnlyList<ShipPolyline> lines, Shape hull)
    {
        if (lines == null || lines.Count == 0 || hull == null) return new float2[0][];
        var top = lines.Select(line => Enumerable.Range(0, (line?.Points?.Length ?? 0) / 3)
            .Select(i => new float2(-line.Points[i * 3], -line.Points[i * 3 + 1])).ToArray()).ToArray();
        var all = top.SelectMany(line => line).ToArray();
        if (all.Length == 0) return top;

        var minX = all.Min(p => p.x);
        var maxX = all.Max(p => p.x);
        var minY = all.Min(p => p.y);
        var maxY = all.Max(p => p.y);
        return top.Select(line => line.Select(p => new float2(
            Stretch(p.x, minX, maxX, hull.Width), Stretch(p.y, minY, maxY, hull.Height))).ToArray()).ToArray();
    }

    // min lands on 0 and max on `size`; an axis with no extent sits at the midpoint.
    private static float Stretch(float value, float min, float max, int size) =>
        max > min ? (value - min) / (max - min) * size : size / 2f;
}

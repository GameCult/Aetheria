/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using CultMath;

/// <summary>
/// Turns intent (which way to go, which way to turn) into every thruster's throttle, whatever the thrusters' actual
/// strength and position. One per ship. A column is one thruster's effect per unit throttle: x starboard acceleration,
/// y forward acceleration, z clockwise yaw rate in rad/s. Throttles are bounded to [0,1].
///
/// One <see cref="BoundedLeastSquares"/> solve per call over the n throttles and four error variables (the unwanted or
/// missing translation on each half-axis, paid for linearly). Yaw is a heavily weighted row, so a hold is exact and a
/// turn is served first; the linear translation cost means the actuator with the least translation per unit of turn
/// serves first and the next joins only as it saturates. Nothing classifies a column as attitude or drive: the onset
/// at which a second actuator joins a turn falls out of the columns.
/// </summary>
public sealed class ThrustAllocator
{
    const float TranslationWeight = 100f;
    const float YawWeight = 1000f;
    const float ErrorCostWeight = .3f;
    const float ErrorCostOffset = 10f;
    const float Ridge = .1f;
    const float ErrorMax = 2f;
    const double KktRelativeTolerance = 1e-12;
    const int ErrorVariables = 4;
    const int FixedRows = 3 + ErrorVariables;

    int _columns = -1;
    float[] _a, _b, _lo, _hi, _x, _workspace;

    /// <summary>
    /// Solves the throttles for the intent. <paramref name="forwardFloor"/> is the throttle lock: every thruster whose
    /// column pushes forward is bounded below at full, the stick is ignored (forward is the only translation asked, so
    /// nothing fires against the floored drives), and the turn is served by what remains.
    /// </summary>
    public void Allocate(ReadOnlySpan<float3> columns, float2 move, float turn, Span<float> throttle, bool forwardFloor = false)
    {
        var n = columns.Length;
        if (n != _columns) Resize(n);
        for (var i = 0; i < n; i++) throttle[i] = 0;
        if (forwardFloor) move = new float2(0, 1);
        for (var i = 0; i < n; i++) _lo[i] = forwardFloor && columns[i].y > 0 ? 1f : 0f;

        // Nothing asked, nothing fired: that is a fact about the intent, decided before the solve. The solver's warm
        // start is left as the last solved point, which is where the next non-zero call resumes.
        if (Intent(move, turn, 0) == 0 && Intent(move, turn, 1) == 0 && Intent(move, turn, 2) == 0) return;

        var vars = n + ErrorVariables;
        var rows = FixedRows + n;

        // Rows 0 and 1 are the translation axes (starboard, forward), row 2 the yaw.
        for (var axis = 0; axis < 3; axis++)
        {
            float plus = 0, minus = 0;
            for (var i = 0; i < n; i++)
            {
                var c = Component(columns[i], axis);
                if (c > 0) plus += c;
                else if (c < 0) minus -= c;
            }
            var extreme = Math.Max(plus, minus);
            var intent = Intent(move, turn, axis);
            var weight = axis == 2 ? YawWeight : TranslationWeight;
            // Intent is a fraction of the half-axis extreme it points into.
            var demand = intent >= 0 ? intent * plus : intent * minus;
            var scale = extreme > 0 ? weight / extreme : 0f;
            var row = axis * vars;
            for (var i = 0; i < n; i++) _a[row + i] = Component(columns[i], axis) * scale;
            _b[axis] = demand * scale;
            if (axis < 2)
            {
                var errorWeight = extreme > 0 ? weight : 0f;
                _a[row + n + 2 * axis] = -errorWeight;
                _a[row + n + 2 * axis + 1] = errorWeight;
            }
        }

        // A linear cost on each error variable, as w*e = -w*kappa, and a ridge on each throttle.
        for (var k = 0; k < ErrorVariables; k++)
        {
            _a[(3 + k) * vars + n + k] = ErrorCostWeight;
            _b[3 + k] = -ErrorCostWeight * ErrorCostOffset;
        }
        for (var i = 0; i < n; i++) _a[(FixedRows + i) * vars + i] = Ridge;

        var status = BoundedLeastSquares.Solve(rows, vars, _a, _b, _lo, _hi, _x, _workspace, out _, KktRelativeTolerance);
        if (status == BoundedLeastSquaresStatus.InvalidInput) return;
        for (var i = 0; i < n; i++) throttle[i] = _x[i];
    }

    static float Intent(float2 move, float turn, int axis) =>
        Math.Min(Math.Max(axis == 0 ? move.x : axis == 1 ? move.y : turn, -1f), 1f);

    static float Component(float3 c, int axis) => axis == 0 ? c.x : axis == 1 ? c.y : c.z;

    void Resize(int n)
    {
        _columns = n;
        var vars = n + ErrorVariables;
        var rows = FixedRows + n;
        _a = new float[rows * vars];
        _b = new float[rows];
        _lo = new float[vars];
        _hi = new float[vars];
        _x = new float[vars];
        _workspace = new float[BoundedLeastSquares.WorkspaceLength(vars)];
        for (var i = 0; i < vars; i++) _hi[i] = i < n ? 1f : ErrorMax;
    }
}

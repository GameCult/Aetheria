/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using static CultMath.math;

// The one owner of the flight camera's zoom (rulings tactical-view-is-zoom-out, tactical-view-in-runs-slow-time).
// Nothing stores an "in tactical" state: the tactical view is how far out the camera is. Zoom is the only state;
// distance, blend and the time rate are derived from it, each frame, by whoever draws or steps.
public sealed class TacticalView
{
    private readonly float _zoomStep, _blendStart, _blendEnd, _timeScale, _fitPerRadius;
    private float _zoneRadius;

    public TacticalView(float zoomStep, float blendStart, float blendEnd, float timeScale, float fitPerRadius)
    {
        _zoomStep = zoomStep;
        _blendStart = blendStart;
        _blendEnd = blendEnd;
        _timeScale = timeScale;
        _fitPerRadius = fitPerRadius;
    }

    // The multiple of the framed distance the camera sits at, >= 1.
    public float Zoom { get; private set; } = 1;

    // The farthest the camera goes: the whole zone in view.
    public float MaxDistance => _zoneRadius * _fitPerRadius;

    // Each notch multiplies Zoom by ZoomStep (negative notches zoom in). Never inside the hull's framing, never past
    // the whole zone.
    public void Step(float notches, float baseDistance)
    {
        var ceiling = MaxDistance > baseDistance && baseDistance > 0 ? MaxDistance / baseDistance : 1f;
        Zoom = clamp(Zoom * pow(_zoomStep, notches), 1f, ceiling);
    }

    // Back to the hull's own framing, in the zone the camera is now in.
    public void Reset(float zoneRadius)
    {
        _zoneRadius = zoneRadius;
        Zoom = 1;
    }

    public float Distance(float baseDistance) => min(baseDistance * Zoom, max(MaxDistance, baseDistance));

    // 0 at or below the band's near edge, 1 at or above its far edge, smooth in the logarithm of distance between.
    public float Blend(float distance)
    {
        var t = saturate((log(max(distance, 1e-6f)) - log(_blendStart)) / (log(_blendEnd) - log(_blendStart)));
        return t * t * (3 - 2 * t);
    }

    // Real seconds feed the sim clock at this rate: 1 in flight, TacticalTimeScale fully zoomed out.
    public float TimeRate(float blend) => pow(_timeScale, saturate(blend));
}

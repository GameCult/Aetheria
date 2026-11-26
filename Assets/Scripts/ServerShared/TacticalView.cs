/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using CultMath;
using static CultMath.math;

// The one owner of the view (rulings tactical-view-distinct-mode, tactical-view-in-runs-slow-time). One scalar, the camera
// distance in log space, is the view: flight owns [ln base, ln BlendStart], the tactical view owns [ln BlendEnd, ln max],
// and the band between is a detent the player scrolls through with intent. Scroll moves a commanded position; an eased
// position follows it on a critically damped spring and is what the camera is. Nothing stores a mode: Tactical is derived
// from the commanded position against the band's crest, and Blend, TimeRate and the rig weight are derived from the
// eased distance, by whoever draws or steps.
// Every feel constant (notch step, detent resistance and relaxation, ease time, the band's edges) is a placeholder the
// operator tunes by eye (GameSettings).
public sealed class TacticalView
{
    private readonly float _zoomStep, _blendStart, _blendEnd, _timeScale, _fitPerRadius;
    private readonly float _detentResistance, _detentRelaxTime, _easeTime;
    private readonly float _lnStart, _lnEnd, _crest;
    private float _lnBase, _lnMax, _zoneRadius;

    public TacticalView(float zoomStep, float blendStart, float blendEnd, float timeScale, float fitPerRadius,
        float detentResistance, float detentRelaxTime, float easeTime)
    {
        _zoomStep = zoomStep;
        _blendStart = blendStart;
        _blendEnd = blendEnd;
        _timeScale = timeScale;
        _fitPerRadius = fitPerRadius;
        _detentResistance = detentResistance;
        _detentRelaxTime = detentRelaxTime;
        _easeTime = easeTime;
        _lnStart = log(blendStart);
        _lnEnd = log(blendEnd);
        _crest = (_lnStart + _lnEnd) / 2;
    }

    // Commanded and eased positions and the ease velocity, in ln metres; the grid point the tactical camera looks at.
    public float Commanded { get; private set; }
    private float _eased, _velocity;
    private float2 _ship, _pan;

    // The grid point the camera looks at: the ship in flight, the panned grid point in tactical, eased between by the
    // rig's own weight (the blend of the eased distance), never by the commanded flip.
    public float2 Target => _ship + (_pan - _ship) * Blend(Distance);

    // Derived, never stored: the commanded position is past the detent's crest.
    public bool Tactical => Commanded > _crest;

    // What the camera is: the eased position.
    public float Distance => exp(_eased);

    // Back to the hull's own framing over the ship, in the zone the camera is now in.
    public void Reset(float zoneRadius, float baseDistance, float2 shipXZ)
    {
        _lnBase = log(baseDistance);
        _lnMax = max(log(max(zoneRadius * _fitPerRadius, _blendEnd)), _lnBase);
        _zoneRadius = zoneRadius;
        Commanded = _eased = _lnBase;
        _velocity = 0;
        _ship = _pan = shipXZ;
    }

    // Each notch (positive zooms out) moves the commanded position by ln ZoomStep. Inside a view it moves freely. A notch
    // that carries it deeper into the band moves it DetentResistance times as far there; one that carries it past the
    // crest commits, and the position jumps to the band's far edge. Symmetric from the tactical side.
    public void Scroll(float notches)
    {
        var step = log(_zoomStep);
        var count = (int) abs(notches);
        var sign = notches < 0 ? -1f : 1f;
        for (var i = 0; i < count; i++) Notch(sign * step);
    }

    private void Notch(float delta)
    {
        // Depth is how far into the band from the edge on the commanded side; negative is out in that view's free range.
        var side = Tactical ? -1f : 1f;
        var edge = Tactical ? _lnEnd : _lnStart;
        var depth = side * (Commanded - edge);
        var deeper = side * delta;
        if (deeper <= 0)
        {
            Commanded = clamp(Commanded + delta, _lnBase, _lnMax);
            return;
        }
        var free = min(deeper, max(-depth, 0f));
        var newDepth = depth + free + (deeper - free) * _detentResistance;
        if (newDepth > _crest - _lnStart)
        {
            Commanded = Tactical ? _lnStart : _lnEnd;
            return;
        }
        Commanded = clamp(edge + side * newDepth, _lnBase, _lnMax);
    }

    // Real seconds: the band's relaxation toward the edge on the commanded side of the crest (it never crosses the crest),
    // then the critically damped ease of the eased position toward the commanded one.
    public void Tick(float realSeconds)
    {
        if (realSeconds <= 0) return;
        if (Commanded > _lnStart && Commanded < _lnEnd)
        {
            var edge = Tactical ? _lnEnd : _lnStart;
            Commanded = edge + (Commanded - edge) * exp(-realSeconds / _detentRelaxTime);
        }
        // Exact solution of x'' = -2w(x' + w(x - c)) over dt: no overshoot from rest, stable at any dt.
        var w = 2 / _easeTime;
        var offset = _eased - Commanded;
        var carried = _velocity + w * offset;
        var decay = exp(-w * realSeconds);
        _eased = Commanded + (offset + carried * realSeconds) * decay;
        _velocity = (_velocity - w * carried * realSeconds) * decay;
    }

    // The ship's grid position, every frame. The panned point rides the ship while the eased camera is in flight (to within
    // float noise of the band's near edge, a weight of about a millionth), so a pan starts from the hull; once the camera
    // leaves flight it stays where the player put it, and the eased weight carries Target back to the ship on the way home.
    public void Track(float2 shipXZ)
    {
        _ship = shipXZ;
        if (!Tactical && Distance <= _blendStart * 1.001f) _pan = shipXZ;
    }

    // Move the target over the grid by a ground delta, never off the zone.
    public void Pan(float2 groundDelta)
    {
        if (!Tactical) return;
        var moved = _pan + groundDelta;
        var reach = length(moved);
        _pan = reach > _zoneRadius && reach > 0 ? moved * (_zoneRadius / reach) : moved;
    }

    // 0 at or below the band's near edge, 1 at or above its far edge, smooth in the logarithm of distance between.
    public float Blend(float distance)
    {
        var t = saturate((log(max(distance, 1e-6f)) - log(_blendStart)) / (log(_blendEnd) - log(_blendStart)));
        return t * t * (3 - 2 * t);
    }

    // Real seconds feed the sim clock at this rate: 1 in flight, TacticalTimeScale fully zoomed out.
    public float TimeRate(float blend) => pow(_timeScale, saturate(blend));
}

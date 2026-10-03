/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using GameCult.Caching;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;

// Mining index: one belt's rocks as targets. Derived from the belt's data, the zone's settings and zone time when the
// zone builds the belt, and never saved.
//
// Rocks are grouped into bands of neighbouring orbit distance. Within a band they are sorted by where they stood on
// their orbits (in turns, as AsteroidBelt.Pose measures them) at the band's key time. A rock's turn at a later time is
// its key plus elapsed / period, and a band's periods lie close together, so the rocks near a point lie in one arc of
// keys, widened by elapsed time times the band's rate spread. A band sheared past ShearLimit is re-sorted at the
// current time, at most RekeyBudget bands per search, so no single search pays for the whole belt; one that is not
// re-sorted is still searched correctly through its wider arc. Each band is split into segments of consecutive keys,
// and each segment's part of the arc is a region, bounded by the annular sector it can occupy.
public sealed class BeltTargets : ITargetProvider
{
    private const int RocksPerBand = 256;
    private const int RocksPerSegment = 32;
    private const double ShearLimit = 1.0 / 64;
    private const int RekeyBudget = 8;

    private readonly Zone _zone;
    private readonly CultRecordKey _field;
    private readonly AsteroidBeltData _data;
    private readonly double[] _periods;
    private readonly Band[] _bands;
    // No rock of the belt, whole or worn, is wider than this (AsteroidSize's bound over every argument).
    private readonly float _largestRadius;

    private sealed class Band
    {
        public float Inner, Outer;              // the least and greatest orbit distance among the band's rocks
        public double SlowestRate, FastestRate; // turns per second: 1 / period
        public double KeyTime;
        public int[] Rocks;
        public double[] Keys;                   // each rock's turn at KeyTime, ascending, in [0, 1)
    }

    public BeltTargets(Zone zone, CultRecordKey field, AsteroidBeltData data, PlanetSettings settings, double time)
    {
        _zone = zone;
        _field = field;
        _data = data;
        var size = settings.AsteroidSize;
        _largestRadius = size.Exponent > 0 ? max(abs(size.Minimum), abs(size.Maximum)) : float.PositiveInfinity;

        var count = data.Asteroids.Length;
        _periods = new double[count];
        var order = new int[count];
        var distances = new float[count];
        for (var i = 0; i < count; i++)
        {
            order[i] = i;
            distances[i] = data.Asteroids[i].Distance;
            // Pose divides by the float period the curve returns; the keys must use the same one.
            _periods[i] = settings.OrbitPeriod.Evaluate(distances[i]);
        }
        Array.Sort(distances, order);
        _bands = new Band[(count + RocksPerBand - 1) / RocksPerBand];
        for (var b = 0; b < _bands.Length; b++)
        {
            var start = b * RocksPerBand;
            var length = Math.Min(RocksPerBand, count - start);
            var band = new Band
            {
                Inner = distances[start], Outer = distances[start + length - 1],
                SlowestRate = double.PositiveInfinity, FastestRate = double.NegativeInfinity,
                Rocks = new int[length], Keys = new double[length]
            };
            Array.Copy(order, start, band.Rocks, 0, length);
            foreach (var rock in band.Rocks)
            {
                var rate = 1.0 / _periods[rock];
                band.SlowestRate = Math.Min(band.SlowestRate, rate);
                band.FastestRate = Math.Max(band.FastestRate, rate);
            }
            Rekey(band, time);
            _bands[b] = band;
        }
    }

    // Diagnostic only: how many bands have been re-sorted since the belt was built, so a test can pin the budget.
    public long Rekeys { get; private set; }

    private void Rekey(Band band, double time)
    {
        Rekeys++;
        band.KeyTime = time;
        for (var j = 0; j < band.Rocks.Length; j++)
            band.Keys[j] = Turn(time / _periods[band.Rocks[j]] + _data.Asteroids[band.Rocks[j]].Phase);
        Array.Sort(band.Keys, band.Rocks);
    }

    public void Regions(in TargetSearch search, List<TargetRegion> into)
    {
        if (_bands.Length == 0) return;
        var time = _zone.ExactTime;
        var centre = _zone.GetOrbitPosition(_zone.Orbits[_data.Orbit.Key].Data.Parent.Key);
        var offset = search.Position - centre;
        var distance = length(offset);
        var turn = Turn(atan2(offset.y, offset.x) / (2 * Math.PI));
        // Slack covers float rounding in Pose and in these bounds; it only ever widens a region.
        var slack = 1e-4f * (_bands[_bands.Length - 1].Outer + distance) + 1e-3f;
        var reach = search.Reach + slack;
        var half = distance <= reach ? 1.0 : Math.Asin(reach / distance) / (2 * Math.PI) + 1e-6;
        var budget = RekeyBudget;

        // The first band whose outer edge reaches the search.
        int lo = 0, hi = _bands.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (_bands[mid].Outer < distance - reach) lo = mid + 1;
            else hi = mid;
        }
        for (var b = lo; b < _bands.Length && _bands[b].Inner <= distance + reach; b++)
        {
            var band = _bands[b];
            if (Math.Abs(time - band.KeyTime) * (band.FastestRate - band.SlowestRate) > ShearLimit && budget > 0)
            {
                Rekey(band, time);
                budget--;
            }
            Drift(band, time, out var leastDrift, out var mostDrift);
            var width = 2 * half + (mostDrift - leastDrift);
            if (!(width < 1))
            {
                AddSegments(search, b, 0, band.Keys.Length, centre, distance, turn, slack, into);
                continue;
            }
            // Keys whose rock can now lie within half a window of the search's own turn.
            var from = Turn(turn - half - mostDrift);
            var to = from + width;
            AddSegments(search, b, LowerBound(band.Keys, from), UpperBound(band.Keys, Math.Min(to, 1.0)), centre, distance, turn, slack, into);
            if (to > 1) AddSegments(search, b, 0, UpperBound(band.Keys, to - 1), centre, distance, turn, slack, into);
        }
    }

    public void Candidates(in TargetSearch search, in TargetRegion region, List<TargetCandidate> into)
    {
        var band = _bands[region.Group];
        for (var j = region.Start; j < region.End; j++)
        {
            var chunk = new ChunkId(_field, band.Rocks[j]);
            if (_zone.ChunkExists(chunk)) into.Add(new TargetCandidate(chunk, _zone.ChunkPose(_field, chunk.Index).xy));
        }
    }

    // The keys [start, end) of one band, one region per segment they cross, each bounded by the sector its rocks can
    // occupy now: the band's distances, and the turns of its first and last key widened by the band's drift.
    private void AddSegments(in TargetSearch search, int b, int start, int end, float2 centre, float distance, double turn, float slack, List<TargetRegion> into)
    {
        var band = _bands[b];
        Drift(band, _zone.ExactTime, out var leastDrift, out var mostDrift);
        var crossSection = _zone.FieldCrossSection(_data);
        for (var s = start; s < end; s = (s / RocksPerSegment + 1) * RocksPerSegment)
        {
            var e = Math.Min(end, (s / RocksPerSegment + 1) * RocksPerSegment);
            var sector = new Sector
            {
                Inner = band.Inner, Outer = band.Outer,
                From = band.Keys[s] + leastDrift - 1e-6,
                Width = band.Keys[e - 1] - band.Keys[s] + (mostDrift - leastDrift) + 2e-6
            };
            sector.Range(distance, turn, out var nearest, out var farthest);
            nearest = max(0f, nearest - slack);
            if (nearest > search.Reach) continue;
            sector.Bearing(search.Position - centre, out var bearing, out var bearingHalf);
            var light = 0f;
            foreach (var sun in _zone.Suns())
            {
                var fromSun = sun.Orbit.Position - centre;
                sector.Range(length(fromSun), Turn(atan2(fromSun.y, fromSun.x) / (2 * Math.PI)), out var sunNearest, out _);
                light += Zone.SunLight(sun, max(0f, sunNearest - slack));
            }
            var cells = _largestRadius / _zone.SchematicCellSize;
            into.Add(new TargetRegion
            {
                Provider = this,
                Group = b, Start = s, End = e,
                Nearest = nearest,
                Farthest = farthest + slack,
                BearingCentre = bearing,
                BearingHalfWidth = min((float) PI, bearingHalf + slack / max(distance, 1e-3f) + 1e-5f),
                MaxVisibility = crossSection * PI * cells * cells * light * 1.0001f
            });
        }
    }

    private static void Drift(Band band, double time, out double least, out double most)
    {
        var elapsed = time - band.KeyTime;
        least = Math.Min(elapsed * band.SlowestRate, elapsed * band.FastestRate);
        most = Math.Max(elapsed * band.SlowestRate, elapsed * band.FastestRate);
    }

    private static double Turn(double turns) => turns - Math.Floor(turns);

    private static int LowerBound(double[] keys, double value)
    {
        int lo = 0, hi = keys.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (keys[mid] < value) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private static int UpperBound(double[] keys, double value)
    {
        int lo = 0, hi = keys.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (keys[mid] <= value) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    // An annular sector about the belt centre: distances [Inner, Outer], turns [From, From + Width] (a Width of 1 or
    // more is the whole annulus).
    private struct Sector
    {
        public float Inner, Outer;
        public double From, Width;

        // The nearest and farthest a point `distance` from the centre at `turn` can be from any point of the sector.
        public void Range(float distance, double turn, out float nearest, out float farthest)
        {
            double offAngle, farAngle;
            if (Width >= 1)
            {
                offAngle = 0;
                farAngle = Math.PI;
            }
            else
            {
                var into = Turn(turn - From);
                offAngle = into <= Width ? 0 : Math.Min(into - Width, 1 - into) * 2 * Math.PI;
                // The sector point farthest in angle: the antipode if the sector holds it, else the farther end.
                farAngle = Turn(turn + .5 - From) <= Width
                    ? Math.PI
                    : Math.Max(Separation(turn, From), Separation(turn, From + Width)) * 2 * Math.PI;
            }
            if (offAngle == 0)
                nearest = max(max(Inner - distance, distance - Outer), 0f);
            else
            {
                // On the nearer edge ray: the closest point of the segment [Inner, Outer] along it.
                var along = clamp(distance * (float) Math.Cos(offAngle), Inner, Outer);
                nearest = Apart(distance, along, offAngle);
            }
            farthest = max(Apart(distance, Inner, farAngle), Apart(distance, Outer, farAngle));
        }

        // A circle that holds the sector, seen from `from` (relative to the belt centre): the bearing to its centre
        // and the half-width of the bearings it covers (pi when `from` is inside it).
        public void Bearing(float2 from, out float centre, out float halfWidth)
        {
            float2 middle;
            float radius;
            if (Width >= .5)
            {
                middle = float2.zero;
                radius = Outer;
            }
            else
            {
                var middleTurn = (From + Width / 2) * 2 * Math.PI;
                var halfAngle = Width * Math.PI;
                var middleRadius = (Inner + Outer) / 2;
                middle = middleRadius * float2((float) Math.Cos(middleTurn), (float) Math.Sin(middleTurn));
                radius = max(Apart(middleRadius, Inner, halfAngle), Apart(middleRadius, Outer, halfAngle));
            }
            var toMiddle = middle - from;
            var apart = length(toMiddle);
            centre = atan2(toMiddle.y, toMiddle.x);
            halfWidth = apart <= radius ? (float) PI : asin(radius / apart);
        }

        // How far apart two points are at distances a and b from the centre, `angle` radians apart about it.
        private static float Apart(float a, float b, double angle) =>
            (float) Math.Sqrt(Math.Max(0, (double) a * a + (double) b * b - 2.0 * a * b * Math.Cos(angle)));

        // The angle in turns between two turns, in [0, .5].
        private static double Separation(double a, double b)
        {
            var d = Turn(a - b);
            return Math.Min(d, 1 - d);
        }
    }
}

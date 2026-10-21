using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CultMath;
using GameCult.Caching;
using static CultMath.math;
using float2 = CultMath.float2;

// A digest of what a zone's generation and build decide, so a test can pin it to a value computed at an earlier commit:
// every orbit's (Distance, Phase, Period, Reach, Support); every body's (type, Mass, rail Distance) and each belt rock's
// (Distance, Phase, Size, RotationSpeed, Period); every orbital entity's (type, position, rail Distance and Phase) and any other
// entity's type alone (a ship packed at the origin is scattered from the clock-seeded item stream, Zone.cs:276); the pack radius;
// GetHeight on a 9x9 grid over the pack at t 0 and 777.7; and, when asked, PopulatedRadius. Cache keys and names are left
// out; they are minted, not decided.
public static class ZoneFingerprint
{
    public static string Of(IEnumerable<Zone> zones, Func<CultRecordKey, bool> skipOrbit, Func<BodyData, bool> skipBody, bool populatedRadius)
    {
        var text = new StringBuilder();
        foreach (var zone in zones) text.Append(Describe(zone, skipOrbit, skipBody, populatedRadius)).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }

    private static string Describe(Zone z, Func<CultRecordKey, bool> skipOrbit, Func<BodyData, bool> skipBody, bool populatedRadius)
    {
        string F(double v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        var orbits = z.Orbits.Where(o => !skipOrbit(o.Key))
            .Select(o => $"{F(o.Value.Data.Distance)},{F(o.Value.Data.Phase)},{F(o.Value.Period)},{F(z.Wells.Reach(o.Key))},{F(z.Wells.Support(o.Key))}")
            .OrderBy(x => x, StringComparer.Ordinal);
        var bodies = z.Planets.Where(b => !skipBody(b.Value))
            .Select(b => $"{b.Value.GetType().Name},{F(b.Value.Mass)},{F(z.Orbits[b.Value.Orbit.Key].Data.Distance)}," +
                         (b.Value is AsteroidBeltData belt ? string.Join("|", belt.Asteroids.Select((r, i) =>
                             $"{F(r.Distance)}/{F(r.Phase)}/{F(r.Size)}/{F(r.RotationSpeed)}/{F(z.AsteroidBelts[b.Key].Periods[i])}")) : ""))
            .OrderBy(x => x, StringComparer.Ordinal);
        var entities = z.Entities.Select(e => e is OrbitalEntity o
                ? $"{e.GetType().Name},{F(e.Position.x)},{F(e.Position.y)},{F(e.Position.z)},{F(z.Orbits[o.OrbitData].Data.Distance)},{F(z.Orbits[o.OrbitData].Data.Phase)}"
                : e.GetType().Name)
            .OrderBy(x => x, StringComparer.Ordinal);
        var r = z.Pack.Radius;
        var heights = new List<string>();
        foreach (var t in new[] { 0.0, 777.7 })
            for (var i = -4; i <= 4; i++)
                for (var j = -4; j <= 4; j++)
                    heights.Add(F(z.GetHeight(float2(i * r / 4f, j * r / 4f), t)));
        return string.Join(";", orbits) + "#" + string.Join(";", bodies) + "#" + string.Join(";", entities) + "#" + F(r) + "#" +
               string.Join(",", heights) + (populatedRadius ? "#" + F(z.PopulatedRadius) : "");
    }
}

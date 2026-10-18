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
// every orbit's (Distance, Phase, Period, Reach, Support), every body's (type, Mass, rail Distance, belt rocks), the entity
// count and GetHeight at three points round each non-belt body. Cache keys and names are left out; they are minted, not decided.
public static class ZoneFingerprint
{
    public static string Of(IEnumerable<Zone> zones, Func<CultRecordKey, bool> skipOrbit, Func<BodyData, bool> skipBody)
    {
        var text = new StringBuilder();
        foreach (var zone in zones) text.Append(Describe(zone, skipOrbit, skipBody)).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }

    private static string Describe(Zone z, Func<CultRecordKey, bool> skipOrbit, Func<BodyData, bool> skipBody)
    {
        string F(float v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        var orbits = z.Orbits.Where(o => !skipOrbit(o.Key))
            .Select(o => $"{F(o.Value.Data.Distance)},{F(o.Value.Data.Phase)},{F(o.Value.Period)},{F(z.Wells.Reach(o.Key))},{F(z.Wells.Support(o.Key))}")
            .OrderBy(x => x, StringComparer.Ordinal);
        var bodies = z.Planets.Values.Where(b => !skipBody(b))
            .Select(b => $"{b.GetType().Name},{F(b.Mass)},{F(z.Orbits[b.Orbit.Key].Data.Distance)}," +
                         (b is AsteroidBeltData belt ? string.Join("|", belt.Asteroids.Select(r => $"{F(r.Distance)}/{F(r.Phase)}/{F(r.Size)}")) : ""))
            .OrderBy(x => x, StringComparer.Ordinal);
        var heights = z.Planets.Values.Where(b => !(b is AsteroidBeltData)).Select(b => z.Orbits[b.Orbit.Key].Position)
            .SelectMany(p => new[] { float2(0, 0), float2(37, 0), float2(0, -211) }.Select(o => F(z.GetHeight(p + o))));
        return string.Join(";", orbits) + "#" + string.Join(";", bodies) + "#" + z.Entities.Count() + "#" + string.Join(",", heights);
    }
}

/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;

// The demo's pressure curve (aetheria-release, cut demo-pressure-curve): how many hostiles, of what hull tier, a zone of a
// galaxy with a gate antagonist (Galaxy.Antagonist) holds, as a function of its depth toward the gate. The single owner of these numbers;
// ZoneGenerator asks and draws. Pure: no Unity, no cache, no randomness, so the same persisted galaxy yields the same zone.
public static class DemoPressure
{
    // Depth at or below which a zone carries no hostiles. Depth is e / (e + x), the share of the way from the entrance to the
    // gate a zone sits at, so a quarter covers the entrance, its neighbours, and on a short galaxy the first jump out.
    public const float GraceDepth = 0.25f;

    // Hostiles at the gate (its ring, besides the boss), the most the curve ever asks for.
    public const int MaxHostiles = 6;

    // Tier is depth raised to this power: above 1 keeps heavy hulls rare until the last stretch.
    public const float TierExponent = 2f;

    public readonly struct Pressure
    {
        public readonly int Hostiles;
        // 0..1: the antagonist's ship hulls up to this quantile of cost may be drawn.
        public readonly float Tier;
        public Pressure(int hostiles, float tier) { Hostiles = hostiles; Tier = tier; }
    }

    // e / (e + x) for e = jumps from the entrance and x = jumps to the exit: 0 only at the entrance, 1 only at the gate, from
    // the persisted Entrance and Exit alone. On a shortest entrance-exit path it is the share of the way travelled; a zone
    // behind the entrance rises slowly with its distance from it rather than sitting at 0.
    public static float Depth(Galaxy galaxy, GalaxyZone zone)
    {
        float e = galaxy.Entrance.Distance[zone], x = galaxy.Exit.Distance[zone];
        return e + x == 0 ? 0f : e / (e + x);
    }

    // The entrance and this many jumps around it are quiet whatever the galaxy's depth scale, so a short galaxy's first
    // neighbours are no deeper than the grace.
    public const int GraceJumps = 1;

    // No antagonist ship, station or turret holds a zone in grace.
    public static bool InGrace(Galaxy galaxy, GalaxyZone zone) =>
        galaxy.Entrance.Distance[zone] <= GraceJumps || Depth(galaxy, zone) <= GraceDepth;

    // What the zone holds: the curve at its depth, none in grace.
    public static Pressure Of(Galaxy galaxy, GalaxyZone zone) =>
        InGrace(galaxy, zone) ? new Pressure(0, At(Depth(galaxy, zone)).Tier) : At(Depth(galaxy, zone));

    public static Pressure At(float depth)
    {
        depth = Math.Clamp(depth, 0f, 1f);
        var ramp = Math.Clamp((depth - GraceDepth) / (1f - GraceDepth), 0f, 1f);
        var smooth = ramp * ramp * (3f - 2f * ramp);
        return new Pressure((int)MathF.Round(MaxHostiles * smooth), MathF.Pow(depth, TierExponent));
    }

    // The dearest hull price the tier admits: the price at that quantile of the hulls offered, cheapest first. Never below the
    // cheapest hull offered, so the draw is never empty while any hull is offered.
    public static int PriceCeiling(int[] offeredPrices, float tier)
    {
        var sorted = offeredPrices.OrderBy(price => price).ToArray();
        return sorted[(int)MathF.Round(Math.Clamp(tier, 0f, 1f) * (sorted.Length - 1))];
    }
}

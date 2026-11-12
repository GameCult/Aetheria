/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;

// The demo's pressure curve (aetheria-release, cut demo-pressure-curve): how many hostiles, of what hull tier, a zone of a
// prelude galaxy with a boss zone holds, as a function of its depth toward the boss zone. The single owner of these numbers;
// ZoneGenerator asks and draws. Pure: no Unity, no cache, no randomness, so the same persisted galaxy yields the same zone.
public static class DemoPressure
{
    // Depth at or below which a zone carries no hostiles. Depth is the share of the entrance-to-boss way already travelled,
    // so a quarter covers the entrance, its neighbours, and on a short galaxy the first jump out.
    public const float GraceDepth = 0.25f;

    // Hostiles at the boss zone (its ring, besides the boss), the most the curve ever asks for.
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

    // 0 at the entrance, 1 at the boss zone, from the persisted Entrance and Exit alone. A zone farther from the exit than the
    // entrance is, is 0.
    public static float Depth(Galaxy galaxy, GalaxyZone zone)
    {
        var way = galaxy.Exit.Distance[galaxy.Entrance];
        if (way <= 0) return 0f;
        return Math.Clamp(1f - (float)galaxy.Exit.Distance[zone] / way, 0f, 1f);
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

    // The faction that raids: the owner of the boss zone nearest the entrance (a prelude with several boss zones raids with
    // that one), or null where this is no prelude with a boss zone.
    public static Faction Antagonist(Galaxy galaxy) =>
        !galaxy.IsPrelude || galaxy.BossZones.Count == 0 ? null
            : galaxy.BossZones.MinBy(boss => galaxy.Entrance.Distance[boss.Value]).Key;

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

/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;

// assembly-from-maker [apply] (aetheria-release, cut assembly-from-maker; rulings quality-lean-assembly-hard-ceiling,
// quality-min-tolerance-binned-assembly): seeds each maker's final assembly from the products it sells. A faction with no
// Assembly that manufactures at least one product with roles gets Mean = the mean of those products' role means plus Margin
// (a maker assembles a little above its parts, capped at 1) and StandardDeviation = the mean of their standard deviations,
// both rounded to 2 dp. It writes only an Assembly that is null, so a value tuned in Studio survives every replay, and it
// never writes a product's Assembly. The derivation reads the products, so it replays last, after every command that adds
// products or factions. Dry run unless apply.
public static class AssemblyFromMakerCommand
{
    // How far above its part mean a maker's final assembly sits: the tolerance margin of the ceiling.
    public const float Margin = .1f;

    // The assembly a maker's roled products imply: the mean over every role of every product, so a product with more roles
    // weighs more. Null when there is no role to average.
    public static AssemblyDistribution Derive(IEnumerable<FactionProductData> products)
    {
        var roles = products.SelectMany(p => p.Roles ?? new List<ProductRole>()).ToArray();
        if (roles.Length == 0) return null;
        return new AssemblyDistribution
        {
            Mean = (float) Math.Round(Math.Min(1, roles.Average(r => r.Mean) + Margin), 2),
            StandardDeviation = (float) Math.Round(roles.Average(r => r.StandardDeviation), 2)
        };
    }

    public static int Run(CultCache cache, bool apply, TextWriter output)
    {
        var byMaker = cache.GetAll<FactionProductData>()
            .GroupBy(p => p.Manufacturer.Key)
            .Select(g => (Maker: g.Key, Derived: Derive(g)))
            .Where(m => m.Derived != null)
            .ToDictionary(m => m.Maker, m => m.Derived);

        var changed = new List<Faction>();
        foreach (var faction in cache.GetAll<Faction>().OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            if (faction.Assembly != null || !byMaker.TryGetValue(cache.RefOf(faction).Key, out var derived)) continue;
            output.WriteLine($"{faction.Name}: assembly -> {derived.Mean:0.##}±{derived.StandardDeviation:0.##}");
            faction.Assembly = derived;
            changed.Add(faction);
        }

        if (changed.Count == 0)
        {
            output.WriteLine("No changes.");
            return 0;
        }

        if (!apply)
        {
            output.WriteLine($"\nDry run. Pass \"apply\" to write {changed.Count} factions.");
            return 0;
        }

        foreach (var faction in changed) CultRecordRefs.Validate(faction);
        cache.Commit(batch =>
        {
            foreach (var faction in changed) batch.Upsert(typeof(Faction), faction, cache.RefOf(faction).Key);
        });
        output.WriteLine($"\nWrote {changed.Count} factions in Aetheria.cc");
        return 0;
    }
}

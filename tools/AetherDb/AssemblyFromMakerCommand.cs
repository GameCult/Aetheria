/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;

// assembly-from-maker [apply] (aetheria-release, cut assembly-from-maker; rulings quality-is-assembly-over-supply-chain,
// quality-lean-assembly-hard-ceiling): derives each maker's final-assembly distribution from the products it sells. A faction
// that manufactures at least one product with roles gets AssemblyMean = the mean of those products' role means and
// AssemblyDeviation = the mean of their standard deviations, rounded to 2 dp; every other faction gets the field defaults.
// The derivation reads the products, so it replays last, after every command that adds products or factions. A faction that
// already holds the derived values is not rewritten, so a rerun writes nothing. Dry run unless apply.
public static class AssemblyFromMakerCommand
{
    // The distribution a maker's roled products imply: the mean over every role of every product, so a product with more
    // roles weighs more. Null when there is no role to average.
    public static (float Mean, float Deviation)? Derive(IEnumerable<FactionProductData> products)
    {
        var roles = products.SelectMany(p => p.Roles ?? new List<ProductRole>()).ToArray();
        if (roles.Length == 0) return null;
        return ((float) Math.Round(roles.Average(r => r.Mean), 2), (float) Math.Round(roles.Average(r => r.StandardDeviation), 2));
    }

    public static int Run(CultCache cache, bool apply, TextWriter output)
    {
        var defaults = new Faction();
        var byMaker = cache.GetAll<FactionProductData>()
            .GroupBy(p => p.Manufacturer.Key)
            .Select(g => (Maker: g.Key, Derived: Derive(g)))
            .Where(m => m.Derived != null)
            .ToDictionary(m => m.Maker, m => m.Derived.Value);

        var changed = new List<Faction>();
        foreach (var faction in cache.GetAll<Faction>().OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            var (mean, deviation) = byMaker.TryGetValue(cache.RefOf(faction).Key, out var derived)
                ? derived
                : (defaults.AssemblyMean, defaults.AssemblyDeviation);
            if (faction.AssemblyMean == mean && faction.AssemblyDeviation == deviation) continue;
            output.WriteLine($"{faction.Name}: assembly {faction.AssemblyMean:0.##}±{faction.AssemblyDeviation:0.##} -> {mean:0.##}±{deviation:0.##}");
            faction.AssemblyMean = mean;
            faction.AssemblyDeviation = deviation;
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

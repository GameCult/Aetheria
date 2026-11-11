/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Caching;

// station-reactor-spread [apply] (aetheria-release follow-up scenarios-batch4-minors): the per-manufacturer station reactors
// (f458206c7) were authored as scaled copies of each maker's largest reactor, but their products were authored with a bare
// ProductRole { Role } for each role, which is the .5 / .15 default, so they lost the source product's quality spread. This
// gives each station product the Mean and StandardDeviation the source product holds for the same role. It writes only a role
// still at the default pair, so a spread tuned in Studio survives every replay, and a second apply has nothing to do. Replays
// before assembly-from-maker (which derives from role means) when that command has an unauthored maker to fill. Dry run
// unless apply.
public static class StationReactorSpreadCommand
{
    // The role pair an unauthored ProductRole carries (ProductRole's field defaults).
    private static readonly ProductRole Unauthored = new ProductRole();

    // (station product, source product, maker short name): the source is the maker's product of the reactor the station
    // design was scaled from.
    public static readonly (string Station, string Source, string Maker)[] Rows =
    {
        ("Steadfast Station", "Steadfast", "R&D"),
        ("Manhattan Station", "Manhattan", "Zhestokost"),
        ("Notorious Station", "Notorious", "Lucent"),
        ("Vulcan Station", "Vulcan", "AU"),
        ("MoveOnPro Station", "MoveOnPro", "NiteLife"),
    };

    public static int Run(CultCache cache, bool apply, TextWriter output, (string Station, string Source, string Maker)[] rows = null)
    {
        var products = cache.GetAll<FactionProductData>().ToArray();
        FactionProductData Find(string name, string maker)
        {
            var found = products.Where(p => p.Name == name && cache.Get(p.Manufacturer)?.ShortName == maker).ToArray();
            if (found.Length != 1) throw new InvalidOperationException($"Station reactor row \"{name}\" by {maker}: {found.Length} products match.");
            return found[0];
        }

        var changed = new List<FactionProductData>();
        foreach (var (stationName, sourceName, maker) in rows ?? Rows)
        {
            var station = Find(stationName, maker);
            var source = Find(sourceName, maker);
            foreach (var role in station.Roles)
            {
                var from = source.Roles.SingleOrDefault(r => r.Role == role.Role)
                    ?? throw new InvalidOperationException($"\"{sourceName}\" holds no \"{role.Role}\" role to copy to \"{stationName}\".");
                if (role.Mean != Unauthored.Mean || role.StandardDeviation != Unauthored.StandardDeviation) continue;
                if (from.Mean == role.Mean && from.StandardDeviation == role.StandardDeviation) continue;
                output.WriteLine($"{stationName} ({maker}) {role.Role}: {role.Mean:0.###}±{role.StandardDeviation:0.###} -> {from.Mean:0.###}±{from.StandardDeviation:0.###}");
                role.Mean = from.Mean;
                role.StandardDeviation = from.StandardDeviation;
                if (!changed.Contains(station)) changed.Add(station);
            }
        }

        if (changed.Count == 0)
        {
            output.WriteLine("No changes.");
            return 0;
        }

        if (!apply)
        {
            output.WriteLine($"\nDry run. Pass \"apply\" to write {changed.Count} products.");
            return 0;
        }

        foreach (var product in changed) CultRecordRefs.Validate(product);
        cache.Commit(batch =>
        {
            foreach (var product in changed) batch.Upsert(typeof(FactionProductData), product, cache.RefOf(product).Key);
        });
        output.WriteLine($"\nWrote {changed.Count} products in Aetheria.cc");
        return 0;
    }
}

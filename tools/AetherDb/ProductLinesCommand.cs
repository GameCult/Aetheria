/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GameCult.Caching;

// product-lines <line> [apply] (aetheria-release, cut product-lines-weapons; rulings catalog-grows-generic-designs-branded-products,
// maker-design-tiers-allowed, miss-terris-belongs-to-emily-r3, pirates-record-content): authors the branded products of one
// product line into GameData/Aetheria.cc. A line is a table of (design, product, maker, text); each row becomes one
// FactionProductData with one ProductRole per role of its design, at the maker's profile in Program.MakerProfiles, so the table
// carries no quality numbers. A row already in the catalog (same name, design and maker) is skipped, so the same command is the
// first apply, the second apply and the replay onto a merged catalog. A maker that already sells the design is not refused: a
// tier is allowed. Dry run unless passed "apply".
public static class ProductLinesCommand
{
    internal sealed record Row(string Design, string Name, string Maker, string Description);

    private static readonly Row[] Weapons =
    {
        new("Light Machine Gun", "Rattler", "AU", "Cheap, loud, and there when the rocks start moving."),
        new("Slug Rifle", "SM-6 Marksman", "R&D", "Standardised, certified, guaranteed to hit the target.*"),
        new("Long Slug Rifle", "Longshot", "Finch", "Light, quiet and dead accurate, when it feels like it."),
        new("Autocannon", "Workhorse", "AU", "Feeds from anything, fires at anything."),
        new("Cluster Cannon", "everything at once", "DME", "why aim/ when you can be everywhere"),
        new("Charged Scattergun", "Grapeshot", "Zhestokost", "The old answer to boarders, sized for the void."),
        new("Light Laser", "Sparrow", "Finch", "Barely registers on the scale, or on their sensors."),
        new("Light Laser", "Spotlight", "Lucent", "Make sure they know who got them."),
        new("Cell Blaster", "Strobe", "Lucent", "Fast, bright, and very hard to ignore."),
        new("Long Laser", "Merlin", "Finch", "Hovers at the edge of range, then stoops."),
        new("Heavy Beam Laser", "Afterglow", "NiteLife", "Keeps glowing long after the fight is over."),
        new("Charged Blaster", "Encore", "Lucent", "One more, for the fans."),
        new("Charged Scatter Blaster", "nobody leaves early", "DME", "doors are locked/ lights are low/ stay for the finale"),
        new("Lightning Projector", "Static Shock", "NiteLife", "A jolt for every ship in reach."),
        new("Guided Missile Launcher", "Homer", "Finch", "Finds its way, mostly."),
        new("Long-Range Missile Rack", "Long Haul", "AU", "Delivers to the far side of the zone."),
        new("Short-Range Missile Rack", "Short Fuse", "AU", "For problems already too close."),
        new("Swarm Rocket Pod", "Confetti", "Lucent", "Every launch is a celebration."),
        new("Incendiary Rocket Pod", "Trebuchet", "Zhestokost", "Siege fire for a column that does not stop."),
        new("Mine Launcher", "Tripwire", "Zhestokost", "Closes the road behind the column."),
    };

    // Every line the command knows, by the word that follows "product-lines".
    private static readonly Dictionary<string, Row[]> Lines = new() { ["weapons"] = Weapons };

    // A line's rows, for the catalog tests that check the shipped records against the table the command authors from.
    internal static IReadOnlyList<Row> Table(string line) => Lines[line];

    // A product's record key is derived from its name, so the same table lands the same records every time it is applied: the
    // replay at a merge reproduces the branch's blob, and nothing that orders products by key reads a different order after it.
    private static CultRecordKey KeyOf(Row row) =>
        new(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("product-line:" + row.Name)).AsSpan(0, 16)).ToString("N"));

    public static int Run(string[] args, string root = null)
    {
        var apply = args.Contains("apply");
        var line = args.FirstOrDefault(a => a != "apply");
        if (line == null || !Lines.TryGetValue(line, out var rows))
        {
            Console.WriteLine($"usage: product-lines {string.Join("|", Lines.Keys)} [apply]");
            return 1;
        }

        var db = AetherDb.Open(catalogWritable: apply, root: root);
        var designs = db.Cache.GetAll<CraftedItemData>().ToArray();
        var factions = db.Cache.GetAll<Faction>().ToArray();
        var products = db.Cache.GetAll<FactionProductData>().ToArray();

        // Resolve every row before anything is written: a missing or ambiguous design or maker, a name that belongs to another
        // product or to a design, and a design with no roles each stop the command with the catalog untouched.
        var resolved = new List<(Row Row, CraftedItemData Design, Faction Maker, bool Present)>();
        foreach (var row in rows)
        {
            var design = designs.Where(d => d.Name == row.Design).ToArray();
            if (design.Length != 1) throw new InvalidOperationException($"Expected exactly one design named \"{row.Design}\", found {design.Length}.");
            var maker = factions.Where(f => f.ShortName == row.Maker).ToArray();
            if (maker.Length != 1) throw new InvalidOperationException($"Expected exactly one faction with short name \"{row.Maker}\", found {maker.Length}.");
            if (!Program.MakerProfiles.ContainsKey(row.Maker)) throw new InvalidOperationException($"No maker profile for \"{row.Maker}\".");
            if (design[0].Roles.Count == 0) throw new InvalidOperationException($"The design \"{row.Design}\" has no roles.");

            var designKey = db.Cache.RefOf(design[0]).Key;
            var makerKey = db.Cache.RefOf(maker[0]).Key;
            var named = products.Where(p => string.Equals(p.Name, row.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
            var present = named.Length == 1 && named[0].Name == row.Name && named[0].Design.Key.Equals(designKey) && named[0].Manufacturer.Key.Equals(makerKey);
            if (named.Length != 0 && !present) throw new InvalidOperationException($"The product name \"{row.Name}\" already belongs to another product.");
            if (!present && designs.Any(d => string.Equals(d.Name, row.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"The product name \"{row.Name}\" is the name of a design.");
            resolved.Add((row, design[0], maker[0], present));
        }

        var missing = resolved.Where(r => !r.Present).ToArray();
        if (missing.Length == 0)
        {
            Console.WriteLine($"The {line} product line is already authored; nothing to do.");
            return 0;
        }

        Console.WriteLine($"Product line {line}:");
        foreach (var (row, design, _, present) in resolved)
        {
            var (mean, dev) = Program.MakerProfiles[row.Maker];
            Console.WriteLine($"  {(present ? "present" : "new    ")} {row.Design} -> \"{row.Name}\" by {row.Maker}, mean {mean:0.##} dev {dev:0.##} on [{string.Join(", ", design.Roles.Select(r => r.Name))}]: {row.Description}");
        }

        if (!apply)
        {
            Console.WriteLine($"\nDry run. Pass \"apply\" to author {missing.Length} products.");
            return 0;
        }

        // ExpectUnchanged lands the batch onto the file as it is: every other record keeps the bytes it has, where a plain
        // commit would re-encode the whole catalog through today's types.
        if (!db.Cache.Commit(batch =>
        {
            batch.ExpectUnchanged();
            foreach (var (row, design, maker, _) in missing)
            {
                var (mean, dev) = Program.MakerProfiles[row.Maker];
                batch.Upsert(typeof(FactionProductData), new FactionProductData
                {
                    Name = row.Name,
                    Description = row.Description,
                    Design = db.Cache.RefOf(design),
                    Manufacturer = db.Cache.RefOf(maker),
                    Roles = design.Roles.Select(r => new ProductRole { Role = r.Name, Mean = mean, StandardDeviation = dev }).ToList(),
                }, KeyOf(row));
            }
        }))
            throw new InvalidOperationException("The catalog changed while the product line was being authored; nothing was written. Run it again.");
        Console.WriteLine($"\nAuthored {missing.Length} products in Aetheria.cc");
        return 0;
    }
}

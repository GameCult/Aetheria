/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GameCult.Caching;

// product-lines <line> [apply] (aetheria-release, cuts product-lines-weapons and gear-lines; lines: weapons, gear; rulings catalog-grows-generic-designs-branded-products,
// maker-design-tiers-allowed, miss-terris-belongs-to-emily-r3, pirates-record-content): authors the branded products of one
// product line into GameData/Aetheria.cc. A line is a table of (design, product, maker, text); each row becomes one
// FactionProductData with one ProductRole per role of its design, at the maker's profile in Program.MakerProfiles, so the table
// carries no quality numbers. A row already in the catalog (same name, design and maker) is skipped, so the same command is the
// first apply, the second apply and the replay onto a merged catalog. A maker that already sells the design is not refused: a
// tier is allowed. Dry run unless passed "apply".
// product-lines tiers [apply] (cut product-tiers; rulings maker-design-tiers-allowed, bin-share-by-floor): authors the bins of
// those lines. A tier row is (line product name, bin name, floor, text); the bin gets Design and Manufacturer from its line,
// Bin = { Line, Floor }, and no Roles or Assembly of its own: the mint derives both from the line.
public static class ProductLinesCommand
{
    internal sealed record Row(string Design, string Name, string Maker, string Description);
    internal sealed record TierRow(string Line, string Name, float Floor, string Description);

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

    // The Aya Collective, Megiddo and Orbital Forge (cut minor-power-factions): ordinary makers, applied after minor-power-factions.
    private static readonly Row[] MinorPowers =
    {
        new("Point Defense Gun", "Common Guard", "Aya", "Standard mount, removable feed box, a barrel you can swap at the bench. Drawings included."),
        new("Point Defense Laser", "Porch Light", "Aya", "Keeps the dark off the doorstep. The manual is longer than the warranty, on purpose."),
        new("Flak Gun", "Canopy", "Aya", "Shade for the whole convoy. Every part replaceable by the people it covers."),
        new("Point Defense Gun", "Watchman", "Megiddo", "Holds the line it is given. Every round accounted for."),
        new("Point Defense Laser", "Vigil", "Megiddo", "It does not sleep while the boundary stands."),
        new("Flak Gun", "Perimeter", "Megiddo", "Nothing crosses that we have not seen."),
        new("Light Machine Gun", "Pattern 12", "Forge", "Drawings ship with the gun. Build the next one yourself."),
        new("Autocannon", "Pattern 30", "Forge", "Standard bore, standard feed, standard anywhere a Belt yard can cut steel."),
        new("Slug Rifle", "Pattern 6", "Forge", "Licensed or not, it fits the same mount."),
        new("Point Defense Gun", "Pattern 2", "Forge", "Close-in defence to an open standard. No license check, ever."),
    };

    private static readonly Row[] Gear =
    {
        new("Adaptive Radiator", "Puffin", "Finch", "Fluffs up in the cold, slicks down in the heat. Mostly on cue."),
        new("Radiator", "Shade Sail", "Lightsail", "Keeps the hold cool on the long run, wherever the run goes."),
        new("Long Radiator", "Smokestack", "Zhestokost", "The heat goes up the stack. The column keeps marching."),
        new("Heat Pump", "ChillOut+-", "NiteLife", "Cool down between sets."),
        new("Cryogenic Heat Pump", "CryoVoid", "Adrasteia", "One half of the Santulanum Duality Thermal Dispersion System. The other half is sold separately."),
        new("Compact Reactor", "Hestia", "AU", "A small fire that keeps the whole house warm."),
        new("Compact Reactor", "Night Shift", "Lightsail", "Steady power for the hauls nobody else wants."),
        new("Heavy Reactor", "HR-9 Bulwark", "R&D", "Rated for continuous output under all certified conditions.*"),
        new("High-Yield Reactor", "PowerHour 2.0", "NiteLife", "Maximum output, for as long as it lasts."),
        new("Ring Reactor", "Milk Run", "Lightsail", "Regular, reliable, and on schedule every time."),
        new("High-Thrust Drive", "Battering Ram", "Zhestokost", "Straight ahead, through whatever is there. The brakes do, in fact, work."),
        new("Burn Drive", "Headliner", "Lucent", "Top of the bill and the brightest exhaust in the zone."),
        new("Rugged Drive", "Rough Road", "Lightsail", "Gets the freight through, whatever the road is made of."),
        new("Racing Drive", "Prokope", "Finch", "67% fatal and 100% fun."),
        new("Sensor Array", "Ranger", "AU", "Practically haunted by all the ghosts it picks up."),
        new("Capacitor", "Powder Keg", "Zhestokost", "A broadside's worth of charge, all of it at once."),
        new("Cargo Bay", "Pithos", "AU", "Holds everything. Do not open in transit."),
        new("Cargo Bay", "SafeStuff", "R&D", "Safe for valuables. Excludes pets, children, cupcakes and friendship."),
        new("Small Cargo Bay", "Amphora", "AU", "A jar for the long road."),
        new("Medium Drive", "Stagecoach", "AU", "Steady miles, every day of the week."),
        new("Large Drive", "Juggernaut", "Zhestokost", "Moves the column. Stopping it is someone else's problem."),
        new("Industrial Thermostatic Heater", "WarmUp+-", "NiteLife", "Room temperature, whatever the room."),
    };

    // The bins of the shipped lines. The names and text are drafts the operator may rename.
    private static readonly TierRow[] Tiers =
    {
        new("Sparrow", "Goldcrest", .67f, "Only the Sparrows that sang on the test bench. The rest still fly."),
        new("Spotlight", "Spotlight Premiere", 0f, "Hand-picked for opening night."),
        new("Rattler", "Rattler Select", 0f, "Pick of the batch. Still loud."),
        new("SM-6 Marksman", "SM-6C Marksman Certified", 0f, "Individually certified to published tolerance.*"),
        new("PowerHour 2.0", "PowerHour 2.0 VIP", .67f, "Skip the line. Same party, better parts."),
        new("Juggernaut", "Juggernaut Guards", 0f, "The column's best drives go to the front of the column."),
    };

    internal static IReadOnlyList<TierRow> TierTable() => Tiers;

    // Every line the command knows, by the word that follows "product-lines".
    private static readonly Dictionary<string, Row[]> Lines = new() { ["weapons"] = Weapons, ["minor-powers"] = MinorPowers, ["gear"] = Gear };

    // A line's rows, for the catalog tests that check the shipped records against the table the command authors from.
    internal static IReadOnlyList<Row> Table(string line) => Lines[line];

    // A product's record key is derived from its name, so the same table lands the same records every time it is applied: the
    // replay at a merge reproduces the branch's blob, and nothing that orders products by key reads a different order after it.
    private static CultRecordKey KeyOf(Row row) => KeyOf(row.Name);

    private static CultRecordKey KeyOf(string name) =>
        new(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("product-line:" + name)).AsSpan(0, 16)).ToString("N"));

    public static int Run(string[] args, string root = null)
    {
        var apply = args.Contains("apply");
        var line = args.FirstOrDefault(a => a != "apply");
        if (line == "tiers") return RunTiers(Tiers, apply, root);
        if (line == null || !Lines.TryGetValue(line, out var rows))
        {
            Console.WriteLine($"usage: product-lines {string.Join("|", Lines.Keys)}|tiers [apply]");
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
            // A cargo bay carries no roles (ruling catalog-breadth-roles-and-components); its product carries none either.
            if (design[0] is not CargoBayData && (design[0].Roles?.Count ?? 0) == 0) throw new InvalidOperationException($"The design \"{row.Design}\" has no roles.");

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
            Console.WriteLine($"  {(present ? "present" : "new    ")} {row.Design} -> \"{row.Name}\" by {row.Maker}, mean {mean:0.##} dev {dev:0.##} on [{string.Join(", ", (design.Roles ?? new()).Select(r => r.Name))}]: {row.Description}");
        }

        if (!apply)
        {
            Console.WriteLine($"\nDry run. Pass \"apply\" to author {missing.Length} products.");
            return 0;
        }

        // The batch lands onto the file as it is: every other record keeps the bytes it has, where a plain commit would
        // re-encode the whole catalog through today's types. What it expects is that each key it authors is still absent, the
        // one condition the command's own validation rests on. ExpectUnchanged would compare the file's schema ids with the
        // loaded ones and refuse any catalog that holds a record written on an older schema, whatever the batch is.
        if (!db.Cache.Commit(batch =>
        {
            foreach (var (row, _, _, _) in missing) batch.Expect(KeyOf(row), null);
            foreach (var (row, design, maker, _) in missing)
            {
                var (mean, dev) = Program.MakerProfiles[row.Maker];
                batch.Upsert(typeof(FactionProductData), new FactionProductData
                {
                    Name = row.Name,
                    Description = row.Description,
                    Design = db.Cache.RefOf(design),
                    Manufacturer = db.Cache.RefOf(maker),
                    Roles = (design.Roles ?? new()).Select(r => new ProductRole { Role = r.Name, Mean = mean, StandardDeviation = dev }).ToList(),
                }, KeyOf(row));
            }
        }))
            throw new InvalidOperationException("The catalog changed while the product line was being authored; nothing was written. Run it again.");
        Console.WriteLine($"\nAuthored {missing.Length} products in Aetheria.cc");
        return 0;
    }

    // The tiers line has Run's shape: resolve every row first, so a refusal leaves the catalog untouched, then land the missing
    // bins in one batch that expects each of their keys absent. A bin already in the catalog (this line, this floor) is skipped,
    // so the same command is the first apply and the replay onto a merged catalog.
    internal static int RunTiers(IReadOnlyList<TierRow> tiers, bool apply, string root)
    {
        var db = AetherDb.Open(catalogWritable: apply, root: root);
        var designs = db.Cache.GetAll<CraftedItemData>().ToArray();
        var products = db.Cache.GetAll<FactionProductData>().ToArray();

        var resolved = new List<(TierRow Row, FactionProductData Line, bool Present)>();
        for (var i = 0; i < tiers.Count; i++)
        {
            var row = tiers[i];
            var lines = products.Where(p => p.Name == row.Line).ToArray();
            if (lines.Length != 1) throw new InvalidOperationException($"Tier row {i}: expected exactly one line product, found {lines.Length}.");
            var parent = lines[0];
            if (parent.Bin != null) throw new InvalidOperationException($"Tier row {i}: the line is itself a bin.");
            if (!float.IsFinite(row.Floor) || row.Floor < 0) throw new InvalidOperationException($"Tier row {i}: the floor is not a finite number of at least 0.");

            var named = products.Where(p => string.Equals(p.Name, row.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
            var present = named.Length == 1 && named[0].Name == row.Name && named[0].Bin != null &&
                named[0].Bin.Line.Key.Equals(db.Cache.RefOf(parent).Key) && named[0].Bin.Floor == row.Floor;
            if (named.Length != 0 && !present) throw new InvalidOperationException($"Tier row {i}: the bin name already belongs to another product.");
            if (!present && designs.Any(d => string.Equals(d.Name, row.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Tier row {i}: the bin name is the name of a design.");
            resolved.Add((row, parent, present));
        }

        var missing = resolved.Where(r => !r.Present).ToArray();
        Console.WriteLine("Product tiers:");
        foreach (var (row, parent, present) in resolved)
            Console.WriteLine($"  {(present ? "present" : "new    ")} {row.Line} -> \"{row.Name}\" by {db.Cache.Get(parent.Manufacturer)?.ShortName}, floor {row.Floor:0.##}: {row.Description}");
        if (missing.Length == 0)
        {
            Console.WriteLine("\nThe tiers are already authored; nothing to do.");
            return 0;
        }

        if (!apply)
        {
            Console.WriteLine($"\nDry run. Pass \"apply\" to author {missing.Length} bins.");
            return 0;
        }

        if (!db.Cache.Commit(batch =>
        {
            foreach (var (row, _, _) in missing) batch.Expect(KeyOf(row.Name), null);
            foreach (var (row, parent, _) in missing)
                batch.Upsert(typeof(FactionProductData), new FactionProductData
                {
                    Name = row.Name,
                    Description = row.Description,
                    Design = parent.Design,
                    Manufacturer = parent.Manufacturer,
                    Bin = new ProductBin { Line = db.Cache.RefOf(parent), Floor = row.Floor },
                }, KeyOf(row.Name));
        }))
            throw new InvalidOperationException("The catalog changed while the tiers were being authored; nothing was written. Run it again.");
        Console.WriteLine($"\nAuthored {missing.Length} bins in Aetheria.cc");
        return 0;
    }
}

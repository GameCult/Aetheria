/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MessagePack;
using CultMath;
using Random = CultMath.Random;

// Commands over the game database, run with: dotnet run --project tools/AetherDb -- <command>
public static class Program
{
    public static int Main(string[] args)
    {
        var command = args.FirstOrDefault() ?? "help";
        switch (command)
        {
            case "doctor": return Doctor();
            case "census": return Census();
            case "station-fit": return StationFit();
            case "hardpoint-fit": return HardpointFit();
            case "loadout": return Loadout(args.Skip(1).FirstOrDefault());
            case "save": return Save();
            case "factions": return Factions();
            case "clear-boss-hulls": return ClearBossHulls(args.Contains("apply"));
            case "settings": return Settings();
            case "settings-dump": return SettingsDump();
            case "migrate-products": return MigrateProducts(args.Contains("apply"));
            default:
                Console.WriteLine("commands: doctor, census, station-fit, migrate-products [apply]");
                return 1;
        }
    }

    // Every entry in the database deserializes, and what types it holds.
    private static int Doctor()
    {
        var db = AetherDb.Open();
        var bytes = File.ReadAllBytes(Path.Combine(db.Root, "GameData", "AetherDB.msgpack"));
        var entries = MessagePackSerializer.Deserialize<DatabaseEntry[]>(bytes);
        var nulls = entries.Select((e, i) => (e, i)).Where(x => x.e == null).Select(x => x.i).ToArray();
        Console.WriteLine($"{bytes.Length} bytes, {entries.Length} entries, {db.Cache.AllEntries.Count()} through the cache");
        Console.WriteLine(nulls.Length == 0 ? "no entries failed to deserialize" : $"FAILED at indices: {string.Join(", ", nulls)}");
        foreach (var group in entries.Where(e => e != null).GroupBy(e => e.GetType().Name).OrderBy(g => g.Key))
            Console.WriteLine($"  {group.Key}: {group.Count()}");
        return nulls.Length;
    }

    // Designs by kind and manufacturer, designs no product sells, and products with authored role quality.
    private static int Census()
    {
        var db = AetherDb.Open();
        var items = db.Cache.GetAll<EquippableItemData>().ToArray();
        var products = db.Cache.GetAll<FactionProductData>().ToArray();

        var kinds = new Dictionary<string, List<string>>();
        foreach (var item in items)
        {
            var kind = item is HullData hull ? $"Hull/{hull.HullType}"
                : item is DockingBayData ? "DockingBay"
                : item is CargoBayData ? "CargoBay"
                : item is WeaponItemData weapon ? $"Weapon/{weapon.HardpointType}"
                : item.HardpointType.ToString();
            if (!kinds.TryGetValue(kind, out var makers)) kinds[kind] = makers = new List<string>();
            makers.Add(db.Cache.Get<Faction>(item.Manufacturer)?.ShortName ?? "(none)");
        }

        Console.WriteLine($"{items.Length} designs, {products.Length} products\n");
        foreach (var kind in kinds.OrderBy(k => k.Key))
            Console.WriteLine($"{kind.Key,-22} {kind.Value.Count,3}  " + string.Join(", ",
                kind.Value.GroupBy(m => m).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}:{g.Count()}")));

        var orphans = items.Where(i => !products.Any(p => p.Design == i.ID)).ToArray();
        Console.WriteLine($"\n{orphans.Length} designs no product sells, so they cannot spawn:");
        foreach (var item in orphans) Console.WriteLine($"  {item.Name}");

        var authored = products.Where(p => p.Roles != null && p.Roles.Count > 0).ToArray();
        Console.WriteLine($"\n{authored.Length} products carry role quality:");
        foreach (var product in authored)
            Console.WriteLine($"  {product.Name,-28} by {db.Cache.Get<Faction>(product.Manufacturer)?.ShortName ?? "(none)",-12} " +
                string.Join(", ", product.Roles.Select(r => $"{r.Role} {r.Mean:0.00}±{r.StandardDeviation:0.00}")));
        if (authored.Length == 0) Console.WriteLine("  none yet: add roles to a design, then set each product's means");
        return 0;
    }

    // Some docking bay fits every station hull, which LoadoutGenerator.GenerateStationLoadout requires.
    // Models the rule the game applies: an interior cell is usable when no gear occupies it, empty hardpoint
    // cells included. Reports both the empty hull and the case where every hardpoint is already filled, because
    // generation equips hardpoint gear first and only the first of those is guaranteed.
    private static int StationFit()
    {
        var db = AetherDb.Open();
        var bays = db.Cache.GetAll<DockingBayData>().ToArray();
        var failures = 0;
        foreach (var hull in db.Cache.GetAll<HullData>().Where(h => h.HullType == HullType.Station))
        {
            var empty = new Shape(hull.Shape.Width, hull.Shape.Height);
            foreach (var v in hull.InteriorCells.Coordinates) empty[v] = true;

            var hardpointsFilled = new Shape(hull.Shape.Width, hull.Shape.Height);
            foreach (var v in hull.InteriorCells.Coordinates) hardpointsFilled[v] = true;
            foreach (var hardpoint in hull.Hardpoints)
                foreach (var v in hardpoint.Shape.Coordinates)
                    hardpointsFilled[hardpoint.Position + v] = false;

            var onEmpty = bays.Where(b => b.Shape.FitsWithin(empty, out _, out _)).Select(b => b.Name).ToArray();
            var onFilled = bays.Where(b => b.Shape.FitsWithin(hardpointsFilled, out _, out _)).Select(b => b.Name).ToArray();
            if (onEmpty.Length == 0) failures++;
            Console.WriteLine($"{hull.Name}: empty hull fits [{string.Join(", ", onEmpty)}], every hardpoint filled fits [{string.Join(", ", onFilled)}]");
            if (onEmpty.Length > 0 && onFilled.Length == 0)
                Console.WriteLine("    depends on hardpoint gear leaving cells free; generation can still fail here");
        }
        return failures;
    }

    // Every hardpoint on every hull, and which designs can fill it: LoadoutGenerator requires a matching
    // HardpointType, a shape that fits the hardpoint, and exactly as many cells as the hardpoint has. A
    // hardpoint no product can fill is gear the game cannot place.
    private static int HardpointFit()
    {
        var db = AetherDb.Open();
        var gear = db.Cache.GetAll<GearData>().ToArray();
        var products = db.Cache.GetAll<FactionProductData>().ToArray();
        var unfillable = 0;
        foreach (var hull in db.Cache.GetAll<HullData>().OrderBy(h => h.HullType).ThenBy(h => h.Name))
        {
            Console.WriteLine($"\n{hull.Name} ({hull.HullType}):");
            foreach (var hardpoint in hull.Hardpoints.OrderByDescending(h => h.Shape.Coordinates.Length))
            {
                var cells = hardpoint.Shape.Coordinates.Length;
                var matches = gear.Where(g =>
                    g.HardpointType == hardpoint.Type &&
                    g.Shape.FitsWithin(hardpoint.Shape, hardpoint.Rotation, out _) &&
                    g.Shape.Coordinates.Length == cells).ToArray();
                var sold = matches.Where(m => products.Any(p => p.Design == m.ID)).ToArray();
                if (sold.Length == 0) unfillable++;
                Console.WriteLine($"  {hardpoint.Type,-14} {cells,2} cells: {matches.Length} designs match, {sold.Length} sold" +
                    (matches.Length == 0 ? "   <- no design of that size" : sold.Length == 0 ? "   <- designs exist but no product sells them" : ""));
                foreach (var match in matches)
                    Console.WriteLine($"      {match.Name,-28} {match.Shape.Coordinates.Length} cells" +
                        (products.Any(p => p.Design == match.ID) ? "" : "  (unsold)"));
            }
        }
        Console.WriteLine($"\n{unfillable} hardpoints no product can fill");
        return unfillable;
    }

    // The parsed shape of the settings asset, for checking where sequence items actually landed.
    private static int SettingsDump()
    {
        var authored = AuthoredSettings.Load(AetherDb.Open().Root);
        authored.Dump("TutorialGenerationSettings", 2);
        Console.WriteLine();
        authored.Dump("GameplaySettings", 2);
        return 0;
    }

    // What the authored settings asset actually yields, so a fixture using it can be trusted before it reports
    // anything about galaxies. Prints the values that matter to generation and every field it could not place.
    private static int Settings()
    {
        var db = AetherDb.Open();
        var authored = AuthoredSettings.Load(db.Root);

        var tutorial = authored.Read<TutorialGenerationSettings>("TutorialGenerationSettings");
        var background = authored.Read<SectorBackgroundSettings>("TutorialBackgroundSettings");
        var names = authored.Read<NameGeneratorSettings>("NameGeneratorSettings");
        var zones = authored.Read<ZoneGenerationSettings>("ZoneSettings");
        var gameplay = authored.Read<GameplaySettings>("GameplaySettings");

        Console.WriteLine($"tutorial: protagonist {tutorial.ProtagonistFaction}, antagonist {tutorial.AntagonistFaction}, " +
            $"buffer {tutorial.BufferFaction}, quest {tutorial.QuestFaction}, " +
            $"neutrals [{string.Join(", ", tutorial.NeutralFactions ?? new string[0])}], " +
            $"{tutorial.ZoneCount} zones, link density {tutorial.LinkDensity}");
        Console.WriteLine($"background: frequency {background.NoiseFrequency}, amplitude {background.NoiseAmplitude}, " +
            $"cloud exponent {background.CloudExponent}, density at centre {background.CloudDensity(new float2(.5f)):0.000}");
        Console.WriteLine($"names: order {names.NameGeneratorOrder}, length {names.NameGeneratorMinLength}-{names.NameGeneratorMaxLength}");
        Console.WriteLine($"zones: sun mass {zones.SunMass}, planet mass {zones.PlanetMass}, satellite passes {zones.SatellitePasses}, " +
            $"belt probability {zones.BeltProbability}");
        Console.WriteLine($"  zone radius curve {zones.ZoneRadius?.Minimum}-{zones.ZoneRadius?.Maximum} exponent {zones.ZoneRadius?.Exponent}");
        Console.WriteLine($"  sub zone count {zones.SubZoneCount?.Minimum}-{zones.SubZoneCount?.Maximum} exponent {zones.SubZoneCount?.Exponent}");
        Console.WriteLine($"gameplay: weapon groups {gameplay.WeaponGroupCount}, shutdown performance {gameplay.DefaultEntitySettings?.ShutdownPerformance}");
        Console.WriteLine($"  tiers: {string.Join(", ", (gameplay.Tiers ?? new RarityTier[0]).Select(t => $"{t.Name} q{t.Quality} r{t.Rarity}"))}");
        Console.WriteLine($"  price modifier {gameplay.QualityPriceModifier?.Minimum}-{gameplay.QualityPriceModifier?.Maximum} exponent {gameplay.QualityPriceModifier?.Exponent}");

        // Assert counts, not non-null: an empty array reads as populated and would make every number a fixture
        // reports downstream a fiction. These are the authored values as of the asset read above.
        var missing = new List<string>();
        if (zones.ZoneRadius == null || zones.ZoneRadius.Maximum <= 0) missing.Add("ZoneSettings.ZoneRadius");
        if (zones.SubZoneCount == null || zones.SubZoneCount.Maximum <= 0) missing.Add("ZoneSettings.SubZoneCount");
        if (zones.PlanetSafetyRadius == null || zones.PlanetSafetyRadius.Multiplier <= 0) missing.Add("ZoneSettings.PlanetSafetyRadius");
        if (gameplay.Tiers == null || gameplay.Tiers.Length != 5) missing.Add($"GameplaySettings.Tiers (expected 5, got {gameplay.Tiers?.Length ?? 0})");
        else if (gameplay.Tiers.Any(t => string.IsNullOrEmpty(t.Name) || t.Quality <= 0)) missing.Add("GameplaySettings.Tiers (a tier parsed without a name or quality)");
        if (gameplay.DefaultEntitySettings == null) missing.Add("GameplaySettings.DefaultEntitySettings");
        if (gameplay.QualityPriceModifier == null || gameplay.QualityPriceModifier.Maximum <= 0) missing.Add("GameplaySettings.QualityPriceModifier");
        if (tutorial.NeutralFactions == null || tutorial.NeutralFactions.Length != 2) missing.Add($"TutorialGenerationSettings.NeutralFactions (expected 2, got {tutorial.NeutralFactions?.Length ?? 0})");
        if (string.IsNullOrEmpty(tutorial.ProtagonistFaction)) missing.Add("TutorialGenerationSettings.ProtagonistFaction");

        Console.WriteLine($"\n{authored.Unplaced.Count} fields in the asset had nowhere to go");
        foreach (var field in authored.Unplaced.Take(20)) Console.WriteLine($"  {field}");
        Console.WriteLine($"{missing.Count} settings generation needs came back unset");
        foreach (var field in missing) Console.WriteLine($"  {field}");
        return missing.Count;
    }

    // Clears boss hull links that resolve to nothing. Galaxy.PlaceFactionsMain gives a boss zone to every faction
    // carrying a BossHull, so a dangling link claims a chokepoint that can never spawn a boss. Dry run unless
    // passed "apply".
    private static int ClearBossHulls(bool apply)
    {
        var db = AetherDb.Open();
        var dangling = db.Cache.GetAll<Faction>()
            .Where(f => f.BossHull != Guid.Empty && db.Cache.Get<HullData>(f.BossHull) == null)
            .OrderBy(f => f.Name)
            .ToArray();

        Console.WriteLine($"{dangling.Length} factions point at a boss hull that does not exist:");
        foreach (var faction in dangling)
        {
            Console.WriteLine($"  {faction.Name,-26} {faction.BossHull}");
            if (apply) faction.BossHull = Guid.Empty;
        }

        if (apply && dangling.Length > 0)
        {
            db.Save();
            Console.WriteLine($"\nCleared {dangling.Length} boss hull links in AetherDB.msgpack");
        }
        else if (!apply && dangling.Length > 0) Console.WriteLine("\nDry run. Pass \"apply\" to clear them.");
        return 0;
    }

    // Each faction's generation-critical links, read through the loaded cache so the multi-file NameFile store
    // counts. Galaxy.GenerateNames dereferences the geoname file without a guard, so a dangling link there stops
    // galaxy generation outright.
    private static int Factions()
    {
        // Name files are not loaded: reporting a geoname link needs only the link's target id, and loading that
        // store rewrites it. A dangling geoname therefore reads as "not loaded" here rather than as DANGLING.
        var db = AetherDb.Open();
        var products = db.Cache.GetAll<FactionProductData>().ToArray();
        Console.WriteLine($"{"faction",-26} {"short",-13} {"geonames",-22} {"boss hull",-14} {"influence",-9} products");
        var broken = 0;
        foreach (var faction in db.Cache.GetAll<Faction>().OrderBy(f => f.Name))
        {
            var geonames = faction.GeonameFile == Guid.Empty ? "UNSET" : "set";
            var boss = faction.BossHull == Guid.Empty
                ? "none"
                : db.Cache.Get<HullData>(faction.BossHull)?.Name ?? "DANGLING";
            if (geonames == "UNSET" || boss == "DANGLING") broken++;
            Console.WriteLine($"{faction.Name,-26} {faction.ShortName,-13} {geonames,-22} {boss,-14} {faction.InfluenceDistance,-9} " +
                products.Count(p => p.Manufacturer == faction.ID));
        }
        Console.WriteLine($"\n{broken} factions carry a link that would break generation");
        return broken;
    }

    // What the saved run actually holds per zone. A zone carrying orbits but no orbital entities was packed by a
    // generation run that failed partway, and is reused on revisit rather than regenerated, so a fixed generator
    // does not repopulate it.
    private static int Save()
    {
        var db = AetherDb.Open();
        var path = Path.Combine(db.Root, "GameData", "PlayerSettings.msgpack");
        if (!File.Exists(path))
        {
            Console.WriteLine($"no save at {path}");
            return 0;
        }

        PlayerSettings settings;
        try
        {
            settings = MessagePackSerializer.Deserialize<PlayerSettings>(File.ReadAllBytes(path));
        }
        catch (Exception e)
        {
            Console.WriteLine($"save does not deserialize: {e.GetType().Name}: {e.Message}");
            return 1;
        }

        var run = settings.SavedRun;
        if (run?.Zones == null)
        {
            Console.WriteLine($"save holds no run (player {settings.Name}, tutorial passed: {settings.TutorialPassed})");
            return 0;
        }

        Console.WriteLine($"player {settings.Name}, {run.Zones.Length} zones, current zone {run.CurrentZone}\n");
        var suspect = 0;
        for (var i = 0; i < run.Zones.Length; i++)
        {
            var zone = run.Zones[i];
            if (zone.Contents == null) continue;
            var stations = zone.Contents.Entities.Count(e => e is OrbitalEntityPack);
            var ships = zone.Contents.Entities.Count(e => e is ShipPack);
            var owner = zone.Owner >= 0 && zone.Owner < run.Factions.Length
                ? db.Cache.Get<Faction>(run.Factions[zone.Owner])?.ShortName ?? "(unknown)"
                : "(none)";
            var packedEmpty = zone.Contents.Orbits.Count > 0 && stations == 0;
            if (packedEmpty) suspect++;
            Console.WriteLine($"  [{i,3}] {zone.Name,-18} owner {owner,-12} orbits {zone.Contents.Orbits.Count,3}  " +
                $"stations {stations,2}  ships {ships,2}{(packedEmpty ? "   <- packed with orbits but no stations" : "")}");
        }

        Console.WriteLine($"\n{suspect} visited zones packed with orbits but no stations");
        return 0;
    }

    // Runs the real LoadoutGenerator against every hull, so a generation failure reproduces here instead of on a
    // flight to a populated sector. No galaxy, so this covers placement, roles and products but NOT availability
    // filtering or distance weighting, which need a real galaxy. Seeded: a failure repeats.
    private static int Loadout(string seedArgument)
    {
        var db = AetherDb.Open();
        var seed = uint.TryParse(seedArgument, out var parsed) ? parsed : 1u;
        var settings = new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp()
        };
        var log = new List<string>();
        var itemManager = new ItemManager(db.Cache, settings, log.Add);
        var failures = 0;

        foreach (var hull in db.Cache.GetAll<HullData>().OrderBy(h => h.HullType).ThenBy(h => h.Name))
        {
            var random = new Random(seed);
            var generator = new LoadoutGenerator(ref random, itemManager, null, null, null, .5f);
            log.Clear();
            string outcome;
            try
            {
                var pack = hull.HullType switch
                {
                    HullType.Ship => generator.GenerateShipLoadout(candidate => candidate.ID == hull.ID),
                    HullType.Turret => generator.GenerateTurretLoadout(),
                    _ => generator.GenerateStationLoadout()
                };
                outcome = pack == null ? "NO LOADOUT (nothing suitable found)" : "ok";
                if (pack == null) failures++;
            }
            catch (Exception e)
            {
                outcome = $"THREW {e.GetType().Name}: {e.Message}";
                failures++;
            }

            Console.WriteLine($"{hull.Name,-12} ({hull.HullType,-7}) {outcome}");
            foreach (var line in log.Distinct()) Console.WriteLine($"      {line}");
        }

        Console.WriteLine($"\n{failures} hulls failed to generate a loadout (seed {seed})");
        return failures;
    }

    // Mints a product for every design that still carries a manufacturer, copying its name and description.
    private static int MigrateProducts(bool apply)
    {
        var db = AetherDb.Open();
        var products = db.Cache.GetAll<FactionProductData>().ToList();
        var designs = db.Cache.GetAll<CraftedItemData>()
            .Where(d => d.Manufacturer != Guid.Empty)
            .Where(d => !products.Any(p => p.Design == d.ID && p.Manufacturer == d.Manufacturer))
            .ToArray();

        Console.WriteLine($"{products.Count} products exist; {designs.Length} designs need one\n");
        foreach (var design in designs)
        {
            var product = new FactionProductData
            {
                Name = design.Name,
                Description = design.Description,
                Design = design.ID,
                Manufacturer = design.Manufacturer,
                // A design authored before roles existed has none; its product gets rows when roles are added
                Roles = (design.Roles ?? new List<ItemRole>()).Select(r => new ProductRole { Role = r.Name }).ToList()
            };
            Console.WriteLine($"  {product.Name,-28} by {db.Cache.Get<Faction>(product.Manufacturer)?.ShortName ?? "(unknown)"}");
            if (apply) db.Cache.Add(product);
        }

        if (apply && designs.Length > 0)
        {
            db.Save();
            Console.WriteLine($"\nSaved {designs.Length} products to AetherDB.msgpack");
        }
        else if (!apply) Console.WriteLine("\nDry run. Pass \"apply\" to write these to the database.");
        return 0;
    }
}

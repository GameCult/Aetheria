/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CultMath;
using GameCult.Caching;
using Random = CultMath.Random;

// Commands over the game database, run with: dotnet run --project tools/AetherDb -- <command>
public static class Program
{
    public static int Main(string[] args)
    {
        var command = args.FirstOrDefault() ?? "help";
        switch (command)
        {
            case "census": return Census();
            case "station-fit": return StationFit();
            case "hardpoint-fit": return HardpointFit();
            case "loadout": return Loadout(args.Skip(1).FirstOrDefault());
            case "save": return Save();
            case "factions": return Factions();
            case "dangling": return Dangling(args.Skip(1).ToArray());
            case "settings": return Settings();
            case "settings-dump": return SettingsDump();
            case "shield-migrate": return ShieldMigrate(args.Contains("apply"));
            case "brownout-migrate": return BrownoutMigrate(args.Contains("apply"));
            default:
                Console.WriteLine("commands: census, factions, station-fit, hardpoint-fit, loadout [seed], save, settings, settings-dump, dangling [clear <Type.Member>]... [apply], shield-migrate [apply], brownout-migrate [apply]");
                return 1;
        }
    }

    // Designs by kind and manufacturer, designs no product sells, and products with authored role quality.
    private static int Census()
    {
        var db = AetherDb.Open();
        var items = db.Cache.GetAll<EquippableItemData>().ToArray();
        var products = db.Cache.GetAll<FactionProductData>()
            .OrderBy(p => db.Cache.RefOf(p).Key.Value, StringComparer.Ordinal).ToArray();

        // The manufacturer no longer lives on the design; a runtime item picks one via Brand()'s tie-break, but
        // census must not invent that same attribution for a design with several sellers. Instead it lists every
        // distinct maker that sells the design (via products), in record-key order. A design no product sells,
        // or that only unset-manufacturer products sell, shows "(none)".
        string MakerOf(EquippableItemData item)
        {
            var design = db.Cache.RefOf(item).Key;
            var makers = products
                .Where(p => p.Design.Key.Equals(design) && p.Manufacturer.IsSet())
                .Select(p => p.Manufacturer.Key)
                .Distinct()
                .OrderBy(k => k.Value, StringComparer.Ordinal)
                .Select(k => db.Cache.Get<Faction>(k)?.ShortName ?? "(none)")
                .ToArray();
            return makers.Length == 0 ? "(none)" : string.Join(", ", makers);
        }

        var kinds = new Dictionary<string, List<string>>();
        foreach (var item in items)
        {
            var kind = item is HullData hull ? $"Hull/{hull.HullType}"
                : item is DockingBayData ? "DockingBay"
                : item is CargoBayData ? "CargoBay"
                : item is WeaponItemData weapon ? $"Weapon/{weapon.HardpointType}"
                : item.HardpointType.ToString();
            if (!kinds.TryGetValue(kind, out var makers)) kinds[kind] = makers = new List<string>();
            makers.Add(MakerOf(item));
        }

        Console.WriteLine($"{items.Length} designs, {products.Length} products\n");
        foreach (var kind in kinds.OrderBy(k => k.Key))
            Console.WriteLine($"{kind.Key,-22} {kind.Value.Count,3}  " + string.Join(", ",
                kind.Value.GroupBy(m => m).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}:{g.Count()}")));

        var sold = SoldDesigns(products);
        var orphans = items.Where(i => !sold.Contains(db.Cache.RefOf(i).Key)).ToArray();
        Console.WriteLine($"\n{orphans.Length} designs no product sells, so they cannot spawn:");
        foreach (var item in orphans) Console.WriteLine($"  {item.Name}");

        var authored = products.Where(p => p.Roles != null && p.Roles.Count > 0).ToArray();
        Console.WriteLine($"\n{authored.Length} products carry role quality:");
        foreach (var product in authored)
            Console.WriteLine($"  {product.Name,-28} by {db.Cache.Get(product.Manufacturer)?.ShortName ?? "(none)",-12} " +
                string.Join(", ", product.Roles.Select(r => $"{r.Role} {r.Mean:0.00}±{r.StandardDeviation:0.00}")));
        if (authored.Length == 0) Console.WriteLine("  none yet: add roles to a design, then set each product's means");

        // Brand() picks the first (maker, design) product in record-key order; more than one means the choice is
        // arbitrary rather than authored, which the rule until segment bands exist forbids.
        var duplicateBrands = products
            .Where(p => p.Manufacturer.IsSet())
            .GroupBy(p => (Maker: p.Manufacturer.Key, Design: p.Design.Key))
            .Where(g => g.Count() > 1)
            .ToArray();
        Console.WriteLine($"\n{duplicateBrands.Length} (maker, design) pairs with more than one product:");
        foreach (var group in duplicateBrands)
        {
            var maker = db.Cache.Get<Faction>(group.Key.Maker);
            var design = db.Cache.Get<CraftedItemData>(group.Key.Design);
            Console.WriteLine($"  {maker?.ShortName ?? "(unknown)"} / {design?.Name ?? group.Key.Design.Value}: " +
                string.Join(", ", group.Select(p => p.Name)));
        }
        return 0;
    }

    private static HashSet<GameCult.Caching.CultRecordKey> SoldDesigns(IEnumerable<FactionProductData> products) =>
        new HashSet<GameCult.Caching.CultRecordKey>(products.Select(p => p.Design.Key));

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
        var sold = SoldDesigns(db.Cache.GetAll<FactionProductData>());
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
                var soldMatches = matches.Where(m => sold.Contains(db.Cache.RefOf(m).Key)).ToArray();
                if (soldMatches.Length == 0) unfillable++;
                Console.WriteLine($"  {hardpoint.Type,-14} {cells,2} cells: {matches.Length} designs match, {soldMatches.Length} sold" +
                    (matches.Length == 0 ? "   <- no design of that size" : soldMatches.Length == 0 ? "   <- designs exist but no product sells them" : ""));
                foreach (var match in matches)
                    Console.WriteLine($"      {match.Name,-28} {match.Shape.Coordinates.Length} cells" +
                        (sold.Contains(db.Cache.RefOf(match).Key) ? "" : "  (unsold)"));
            }
        }
        Console.WriteLine($"\n{unfillable} hardpoints no product can fill");
        return unfillable;
    }

    // The parsed shape of the settings asset, for checking where sequence items actually landed.
    private static int SettingsDump()
    {
        var authored = AuthoredSettings.Load(AetherDb.FindRoot());
        authored.Dump("TutorialGenerationSettings", 2);
        Console.WriteLine();
        authored.Dump("GameplaySettings", 2);
        return 0;
    }

    // What the authored settings asset actually yields, so a fixture using it can be trusted before it reports
    // anything about galaxies. Prints the values that matter to generation and every field it could not place.
    private static int Settings()
    {
        var authored = AuthoredSettings.Load(AetherDb.FindRoot());

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

    // Every CultRecordRef in the catalog that resolves to nothing, as "<record> <DeclaringType.Member> -> <key>".
    // Each "clear <DeclaringType.Member>" argument unsets that member's dangling refs, or removes that dictionary's
    // dangling keys (Faction.BossHull clears boss hulls that would claim a chokepoint no boss can spawn in). Dry
    // run unless passed "apply", which alone opens the catalog writable and lands every changed record in one commit.
    private static int Dangling(string[] args)
    {
        var apply = args.Contains("apply");
        var clear = new HashSet<string>(args.Select((arg, i) => (arg, i))
            .Where(p => p.arg == "clear" && p.i + 1 < args.Length)
            .Select(p => args[p.i + 1]));
        var db = AetherDb.Open(catalogWritable: apply);

        var changed = new List<(object Document, CultRecordKey Key)>();
        var found = 0;
        foreach (var stored in db.Cache.AllStoredDocuments.OrderBy(s => NameOf(s.Document) ?? s.Key.Value, StringComparer.Ordinal))
        {
            var name = NameOf(stored.Document) ?? stored.Key.Value;
            var cleared = 0;
            foreach (var dangling in DanglingRefs(db.Cache, stored.Document, new HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance)))
            {
                found++;
                var member = $"{dangling.Field.DeclaringType.Name}.{dangling.Field.Name}";
                var clearing = clear.Contains(member);
                Console.WriteLine($"{name} {member} -> {dangling.Key}{(clearing ? "   (cleared)" : "")}");
                if (!clearing) continue;
                dangling.Clear();
                cleared++;
            }
            if (cleared > 0) changed.Add((stored.Document, stored.Key));
        }

        Console.WriteLine($"\n{found} dangling refs");
        if (changed.Count == 0) return 0;
        if (!apply)
        {
            Console.WriteLine($"Dry run. Pass \"apply\" to land {changed.Count} changed records.");
            return 0;
        }

        // F7 (docs/stats-and-power-cut.md, Soul pass 2026-09-19): this repair must preserve the record's existing
        // key, which the validating CultRecordRefs.Upsert extension does not support (see AetheriaStores.cs's own
        // Validate comment) -- so it calls the same validation directly, right before the identity-preserving
        // write, instead of writing raw and unvalidated the way this line used to.
        foreach (var (document, _) in changed) CultRecordRefs.Validate(document);
        db.Cache.Commit(batch =>
        {
            foreach (var (document, key) in changed) batch.Upsert(document.GetType(), document, key);
        });
        Console.WriteLine($"Landed {changed.Count} changed records in Aetheria.cc");
        return 0;
    }

    private sealed record DanglingRef(FieldInfo Field, CultRecordKey Key, Action Clear);

    // Walks fields by reflection: plain refs, list and array elements, ref-keyed dictionary keys, and nested
    // [MessagePackObject] and [Union] values. Clearing a list element unsets it; clearing a dictionary key removes it.
    private static IEnumerable<DanglingRef> DanglingRefs(CultCache cache, object value, HashSet<object> seen)
    {
        if (value == null || !seen.Add(value)) yield break;
        foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var owner = value;
            var member = field.GetValue(owner);
            if (member == null) continue;
            if (member is ICultRecordRef reference)
            {
                if (Dangles(cache, reference))
                    yield return new DanglingRef(field, reference.Key, () => field.SetValue(owner, Activator.CreateInstance(field.FieldType)));
            }
            else if (member is IDictionary dictionary)
            {
                foreach (var key in dictionary.Keys.Cast<object>().ToArray())
                {
                    if (key is ICultRecordRef keyRef && Dangles(cache, keyRef))
                        yield return new DanglingRef(field, keyRef.Key, () => dictionary.Remove(key));
                    else foreach (var nested in DanglingRefs(cache, key, seen)) yield return nested;
                    if (dictionary.Contains(key))
                        foreach (var nested in DanglingRefs(cache, dictionary[key], seen)) yield return nested;
                }
            }
            else if (member is IList list && !(member is Array { Rank: > 1 }))
            {
                for (var i = 0; i < list.Count; i++)
                {
                    var index = i;
                    if (list[i] is ICultRecordRef elementRef)
                    {
                        if (Dangles(cache, elementRef))
                            yield return new DanglingRef(field, elementRef.Key, () => list[index] = Activator.CreateInstance(elementRef.GetType()));
                    }
                    else if (list[i] != null && IsSerializedObject(list[i].GetType()))
                        foreach (var nested in DanglingRefs(cache, list[i], seen)) yield return nested;
                }
            }
            else if (IsSerializedObject(member.GetType()))
                foreach (var nested in DanglingRefs(cache, member, seen)) yield return nested;
        }
    }

    private static string NameOf(object document) => document.GetType()
        .GetFields(BindingFlags.Instance | BindingFlags.Public)
        .FirstOrDefault(f => f.IsDefined(typeof(CultNameAttribute), true))?.GetValue(document) as string;

    private static bool Dangles(CultCache cache, ICultRecordRef reference) =>
        reference.Key.IsSet() && cache.Get(reference.Key) == null;

    private static bool IsSerializedObject(Type type)
    {
        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            if (t.IsDefined(typeof(MessagePack.MessagePackObjectAttribute), false) || t.IsDefined(typeof(MessagePack.UnionAttribute), false))
                return true;
        return false;
    }

    // Each faction's generation-critical links. Galaxy.GenerateNames dereferences the geoname file without a
    // guard, so an unset link there stops galaxy generation outright.
    private static int Factions()
    {
        var db = AetherDb.Open();
        var products = db.Cache.GetAll<FactionProductData>().ToArray();
        Console.WriteLine($"{"faction",-26} {"short",-13} {"geonames",-22} {"boss hull",-14} {"influence",-9} products");
        var broken = 0;
        foreach (var faction in db.Cache.GetAll<Faction>().OrderBy(f => f.Name))
        {
            var key = db.Cache.RefOf(faction).Key;
            var geonames = faction.GeonameFile.IsSet() ? "set" : "UNSET";
            var boss = !faction.BossHull.IsSet()
                ? "none"
                : db.Cache.Get(faction.BossHull)?.Name ?? "DANGLING";
            if (geonames == "UNSET" || boss == "DANGLING") broken++;
            Console.WriteLine($"{faction.Name,-26} {faction.ShortName,-13} {geonames,-22} {boss,-14} {faction.InfluenceDistance,-9} " +
                products.Count(p => p.Manufacturer.Key.Equals(key)));
        }
        Console.WriteLine($"\n{broken} factions carry a link that would break generation");
        return broken;
    }

    // What the saved run actually holds per zone. A zone carrying orbits but no orbital entities was packed by a
    // generation run that failed partway, and is reused on revisit rather than regenerated, so a fixed generator
    // does not repopulate it.
    private static int Save()
    {
        var path = Path.Combine(AetherDb.FindRoot(), "GameData", "run.cc");
        if (!File.Exists(path))
        {
            Console.WriteLine($"no save at {path}");
            return 0;
        }

        AetherDb db;
        try
        {
            db = AetherDb.Open(withRun: true);
        }
        catch (Exception e)
        {
            Console.WriteLine($"save does not open: {e.GetType().Name}: {e.Message}");
            return 1;
        }

        var run = db.Cache.GetGlobal<SavedGame>();
        if (run?.Zones == null)
        {
            Console.WriteLine("run store holds no SavedGame");
            return 0;
        }

        Console.WriteLine($"{run.Zones.Length} zones, current zone {run.CurrentZone}\n");
        var suspect = 0;
        var crafted = new List<CraftedItemInstance>();
        for (var i = 0; i < run.Zones.Length; i++)
        {
            var zone = db.Cache.Get(run.Zones[i]);
            if (zone?.Contents == null) continue;
            var stations = zone.Contents.Entities.Count(e => e is OrbitalEntityPack);
            var ships = zone.Contents.Entities.Count(e => e is ShipPack);
            var owner = zone.Owner >= 0 && zone.Owner < run.Factions.Length
                ? db.Cache.Get(run.Factions[zone.Owner])?.ShortName ?? "(unknown)"
                : "(none)";
            var packedEmpty = zone.Contents.Orbits.Count > 0 && stations == 0;
            if (packedEmpty) suspect++;
            crafted.AddRange(zone.Contents.Entities.SelectMany(EntitySerializer.Items).OfType<CraftedItemInstance>());
            Console.WriteLine($"  [{i,3}] {zone.Name,-18} owner {owner,-12} orbits {zone.Contents.Orbits.Count,3}  " +
                $"stations {stations,2}  ships {ships,2}{(packedEmpty ? "   <- packed with orbits but no stations" : "")}");
        }

        Console.WriteLine($"\n{suspect} visited zones packed with orbits but no stations");

        // The only headless check of a real played save: does every crafted instance still resolve its lot, and
        // does its Data still equal the lot's Design (the one invariant CreateInstance(int) is meant to hold)?
        var lots = RunSave.Lots(db.Cache);
        var absent = 0;
        var mismatched = 0;
        foreach (var item in crafted)
        {
            if (!lots.Lots.TryGetValue(item.Lot, out var lot)) { absent++; continue; }
            if (!lot.Design.Key.Equals(item.Data.Key)) mismatched++;
        }
        Console.WriteLine($"\n{lots.Lots.Count} lots, {crafted.Count} crafted instances, " +
            $"{absent} instances whose lot is absent, {mismatched} instances whose Data differs from Lot.Design");
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
        var itemManager = new ItemManager(db.Cache, new ProvenanceLedger(), settings, log.Add);
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
                    HullType.Ship => generator.GenerateShipLoadout(candidate => candidate == hull),
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

    // Operator ruling, 2026-09-19 (docs/stats-and-power-cut.md, shield reserve ruling block): ShieldData now
    // authors Capacity/RefillDuration/RestoreDuration directly instead of deriving the reserve from EnergyUsage
    // at runtime. Every existing shield design's PerformanceStat default (Min = Max = 0) reads as an authored
    // zero reserve post-migration -- CanTakeHit would break on the very first hit -- so this command must land
    // in the same commit as the schema change. Dry run unless passed "apply", which alone opens the catalog
    // writable; every record must derive cleanly or nothing is written (TEMP: prints the derivation table for
    // operator review either way).
    private static int ShieldMigrate(bool apply)
    {
        var db = AetherDb.Open(catalogWritable: apply);

        // ShieldData nests inside either host's Behaviors list (GearData: an equipped item with a hardpoint
        // Shape to size from; ConsumableItemData: a consumable effect with no Shape -- cells reads 0 there, and
        // the derivation below floors it to 1). Both are scanned because the schema change applies to every
        // ShieldData record, not just equipped ones.
        var hosts = db.Cache.GetAll<GearData>().Select(g => (Name: g.Name, Cells: g.Shape.Coordinates.Length, Document: (object) g, Key: db.Cache.RefOf(g).Key, Behaviors: g.Behaviors))
            .Concat(db.Cache.GetAll<ConsumableItemData>().Select(c => (Name: c.Name, Cells: 0, Document: (object) c, Key: db.Cache.RefOf(c).Key, Behaviors: c.Behaviors)));
        var shields = hosts
            .Select(h => (h.Name, h.Cells, h.Document, h.Key, Shield: h.Behaviors.OfType<ShieldData>().FirstOrDefault()))
            .Where(p => p.Shield != null)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToArray();

        Console.WriteLine($"{shields.Length} shield designs\n");
        Console.WriteLine($"{"design",-24} {"cells",5} {"energy",7} {"capacity",9} {"refill",7} {"restore",8}  derived from");
        var changed = new List<(object Document, CultRecordKey Key)>();
        var alreadyAuthored = 0;
        foreach (var (name, cells, document, key, shield) in shields)
        {
            if (shield.Capacity.Max > 0f || shield.RefillDuration.Max > 0f || shield.RestoreDuration.Max > 0f)
            {
                alreadyAuthored++;
                Console.WriteLine($"{name,-24} {cells,5} {shield.EnergyUsage.Max,7:0.##} {shield.Capacity.Max,9:0.##} " +
                    $"{shield.RefillDuration.Max,7:0.##} {shield.RestoreDuration.Max,8:0.##}  (already authored, left alone)");
                continue;
            }

            // EnergyUsage is a per-damage-point cost multiplier, not an energy quantity of its own -- Cut 4's
            // mistake was treating it as a reserve size. A reserve needs to be sized in damage points, so this
            // divides back out: a shield with EnergyUsage 1 costs 1 energy per point of damage, so a Capacity of
            // (BaseAbsorption * EnergyUsage) buys BaseAbsorption points of raw damage before breaking, scaling
            // with the item's own cost multiplier the same way the old runtime-derived Capacity did (Capacity =
            // EnergyUsage) but sized for a real fight instead of one hit. BaseAbsorption = 10 raw damage points,
            // then scaled by the item's own cell count (larger reserved capacitors in a bigger hull slot) with a
            // floor of 1 cell so a consumable-hosted shield (no Shape, Cells = 0) or a malformed 0-cell fixture
            // does not zero the result.
            const float baseAbsorption = 10f;
            var energyUsage = shield.EnergyUsage.Max;
            var capacity = baseAbsorption * Math.Max(1f, energyUsage) * Math.Max(1, cells);

            // Refill (holding) is the fast lever; restore (broken) is the punish window and is authored several
            // times slower so breaking a shield costs real time regardless of how it broke. 2s/12s are flat
            // starting points (no other authored duration on ShieldData to scale from), not derived from any
            // per-design number -- flagged for operator tuning against real combat pacing, same footing Cut 4
            // flagged its own guess on.
            const float refillDuration = 2f;
            const float restoreDuration = 12f;

            shield.Capacity = new PerformanceStat { Min = capacity, Max = capacity };
            shield.RefillDuration = new PerformanceStat { Min = refillDuration, Max = refillDuration };
            shield.RestoreDuration = new PerformanceStat { Min = restoreDuration, Max = restoreDuration };

            Console.WriteLine($"{name,-24} {cells,5} {energyUsage,7:0.##} {capacity,9:0.##} " +
                $"{refillDuration,7:0.##} {restoreDuration,8:0.##}  {baseAbsorption:0.#} x max(1,energy) x cells; flat durations");
            changed.Add((document, key));
        }

        Console.WriteLine($"\n{changed.Count} designs migrated, {alreadyAuthored} already authored (left alone)");
        if (changed.Count == 0) return 0;
        if (!apply)
        {
            Console.WriteLine($"Dry run. Pass \"apply\" to land {changed.Count} changed records.");
            return 0;
        }

        // F7 (docs/stats-and-power-cut.md, Soul pass 2026-09-19): this repair must preserve the record's existing
        // key, which the validating CultRecordRefs.Upsert extension does not support (see AetheriaStores.cs's own
        // Validate comment) -- so it calls the same validation directly, right before the identity-preserving
        // write, instead of writing raw and unvalidated the way this line used to.
        foreach (var (document, _) in changed) CultRecordRefs.Validate(document);
        db.Cache.Commit(batch =>
        {
            foreach (var (document, key) in changed) batch.Upsert(document.GetType(), document, key);
        });
        Console.WriteLine($"Landed {changed.Count} changed records in Aetheria.cc");
        return 0;
    }

    // Operator ruling, docs/stats-and-power-target.md: "Continuous consumers brown out through a power supply
    // curve on their performance stats." The Cut 7 code change (PowerBus.cs's five continuous-consumer gates)
    // only has anything to curve if some shipped stat actually declares a PowerSupply term -- otherwise every
    // one of those stats keeps answering PowerSupplyFactor's identity (Entity.cs), and removing the gates makes
    // a partial grant read exactly like a full one instead of a reduced one. This authors the term, once, on the
    // one performance stat each of the four curve-eligible behaviours (EnergyDraw has none -- see EnergyDraw.cs)
    // actually reads for its continuous effect: ThrusterData.Thrust, AetherDriveData.Torque,
    // RadiatorData.PumpedHeat, ConstantWeaponData.Damage. None of the four is a power-request field
    // (StatValidation.PowerRequestFields), so authoring the term here does not trip
    // ValidateNoPowerSupplyOnRequest. Exponent 1 (linear) is the gentle default the ruling calls for -- half
    // supply reads as half performance, not a cliff -- and is left for the operator to steepen per design later.
    // Dry run unless passed "apply"; every record must derive cleanly or nothing is written, same contract as
    // ShieldMigrate.
    private static int BrownoutMigrate(bool apply)
    {
        var db = AetherDb.Open(catalogWritable: apply);
        const float gentleExponent = 1f;

        // (behaviour type, target performance-stat field, short label) -- deliberately NOT the request field
        // (StatValidation.PowerRequestFields already forbids a PowerSupply term there for each of these types).
        var targets = new (Type BehaviorType, string Field, string Label)[]
        {
            (typeof(ThrusterData), nameof(ThrusterData.Thrust), "thrust"),
            (typeof(AetherDriveData), nameof(AetherDriveData.Torque), "torque"),
            (typeof(RadiatorData), nameof(RadiatorData.PumpedHeat), "pumpedHeat"),
            (typeof(ConstantWeaponData), nameof(WeaponData.Damage), "damage"),
        };

        var hosts = db.Cache.GetAll<GearData>().Select(g => (Name: g.Name, Document: (object) g, Key: db.Cache.RefOf(g).Key, Behaviors: g.Behaviors))
            .Concat(db.Cache.GetAll<ConsumableItemData>().Select(c => (Name: c.Name, Document: (object) c, Key: db.Cache.RefOf(c).Key, Behaviors: c.Behaviors)));

        Console.WriteLine($"{"design",-24} {"behaviour",-14} {"stat",-10} {"before",8} {"after",8}  exponent");
        var changed = new List<(object Document, CultRecordKey Key)>();
        var alreadyAuthored = 0;
        var failures = 0;
        foreach (var (name, document, key, behaviors) in hosts.OrderBy(h => h.Name, StringComparer.Ordinal))
        {
            foreach (var behavior in behaviors)
            {
                foreach (var (behaviorType, field, label) in targets)
                {
                    if (!behaviorType.IsInstanceOfType(behavior)) continue;
                    PerformanceStat stat;
                    try
                    {
                        stat = behaviorType.GetField(field)?.GetValue(behavior) as PerformanceStat
                            ?? throw new InvalidOperationException($"{behaviorType.Name}.{field} is not a PerformanceStat field");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"FAILED reading {name} {behaviorType.Name}.{field}: {e.Message}");
                        failures++;
                        continue;
                    }

                    if (stat.Terms.Any(t => t.Source == StatSource.PowerSupply))
                    {
                        alreadyAuthored++;
                        Console.WriteLine($"{name,-24} {behaviorType.Name,-14} {label,-10} {"(already authored, left alone)",-17}");
                        continue;
                    }

                    var before = stat.Terms.Count;
                    stat.Terms.Add(new StatTerm { Source = StatSource.PowerSupply, Exponent = gentleExponent });
                    Console.WriteLine($"{name,-24} {behaviorType.Name,-14} {label,-10} {before,8} {stat.Terms.Count,8}  {gentleExponent:0.##}");
                    changed.Add((document, key));
                }
            }
        }

        Console.WriteLine($"\n{changed.Count} stats authored a PowerSupply term, {alreadyAuthored} already authored (left alone), {failures} failures");
        if (failures > 0)
        {
            Console.WriteLine("Refusing to write: at least one record failed to derive cleanly.");
            return 1;
        }
        if (changed.Count == 0) return 0;
        if (!apply)
        {
            Console.WriteLine($"Dry run. Pass \"apply\" to land {changed.Count} changed records.");
            return 0;
        }

        // F7 (docs/stats-and-power-cut.md, Soul pass 2026-09-19): this repair must preserve the record's existing
        // key, which the validating CultRecordRefs.Upsert extension does not support (see AetheriaStores.cs's own
        // Validate comment) -- so it calls the same validation directly, right before the identity-preserving
        // write, instead of writing raw and unvalidated the way this line used to.
        foreach (var (document, _) in changed) CultRecordRefs.Validate(document);
        db.Cache.Commit(batch =>
        {
            foreach (var (document, key) in changed) batch.Upsert(document.GetType(), document, key);
        });
        Console.WriteLine($"Landed {changed.Count} changed records in Aetheria.cc");
        return 0;
    }
}

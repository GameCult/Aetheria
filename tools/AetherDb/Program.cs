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
using GameCult.Caching.MessagePack;
using MessagePack;
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
            case "loadout": return Loadout(args.Skip(1).ToArray());
            case "save": return Save();
            case "factions": return Factions();
            case "dangling": return Dangling(args.Skip(1).ToArray());
            case "settings": return Settings();
            case "settings-dump": return SettingsDump();
            case "shield-migrate": return ShieldMigrate(args.Contains("apply"));
            case "brownout-migrate": return BrownoutMigrate(args.Contains("apply"));
            case "roles-migrate": return RolesMigrate(args.Contains("apply"));
            case "firing-arc-migrate": return FiringArcMigrate(args.Contains("apply"));
            case "range-falloff": return RangeFalloff(args.Skip(1).ToArray());
            case "targeting-catalog": return TargetingCatalog(args.Contains("apply"));
            case "targeting-catalog-6c": return TargetingCatalog6c(args.Contains("apply"));
            case "targeting-catalog-6d": return TargetingCatalog6d(args.Contains("apply"));
            case "ship-authoring": return ShipAuthoringCommands.Run(args.Skip(1).ToArray());
            case "field-kinds": return FieldKindsCatalog(args.Contains("apply"));
            case "pd-gear": return PdGearCatalog(args.Contains("apply"));
            case "mine-launcher": return MineLauncherCatalog(args.Contains("apply"));
            case "smoke-hull": return SmokeHullCatalog(args.Contains("apply"));
            case "smoke-rounds": return SmokeRoundsCatalog(args.Contains("apply"));
            case "roles-backfill": return RolesBackfill(args.Contains("apply"));
            case "consumables-first-set": return ConsumablesFirstSetCatalog(args.Contains("apply"));
            case "launch-suppressor": return LaunchSuppressorCatalog(args.Contains("apply"));
            case "pirates-faction":
                Console.OutputEncoding = new System.Text.UTF8Encoding(false);
                return PiratesFaction(args.Contains("apply"));
            case "verse-grammar": return VerseGrammarCommand.Run(args.Contains("apply"));
            case "sensor-stat-set": return SensorStatSet(args.Contains("apply"));
            case "generic-designs": return GenericDesigns(args.Contains("apply"));
            case "inspection-scanner": return InspectionScannerCatalog(args.Contains("apply"));
            case "product-lines": return ProductLinesCommand.Run(args.Skip(1).ToArray());
            case "doctrine-catalog": return DoctrineCatalog.Run(AetherDb.Open(catalogWritable: args.Contains("apply")).Cache, args.Contains("apply"), Console.Out);
            default:
                Console.WriteLine("commands: census, factions, station-fit, hardpoint-fit, loadout [seed], save, settings, settings-dump, dangling [clear <Type.Member>]... [apply], shield-migrate [apply], brownout-migrate [apply], roles-migrate [apply], firing-arc-migrate [apply], range-falloff [apply] [--effective <fraction>] [--end <multiplier>], targeting-catalog [apply], targeting-catalog-6c [apply], targeting-catalog-6d [apply], ship-authoring create|inspect|validate|compose, field-kinds [apply], mine-launcher [apply], smoke-hull [apply], smoke-rounds [apply], roles-backfill [apply], pd-gear [apply], consumables-first-set [apply], launch-suppressor [apply], pirates-faction [apply], verse-grammar [apply], generic-designs [apply], sensor-stat-set [apply], inspection-scanner [apply], product-lines weapons|gear [apply], doctrine-catalog [apply]");
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

        // The manufacturer no longer lives on the design; a runtime item's brand is the product its lot records, but
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

        // Cut 7 (docs/stats-and-power-cut.md): "adding a role to a shipped design changes nothing for existing
        // lots... but a newly minted lot from a product with no spread for that role rolls new ProductRole() --
        // mean .5 ... The census is where that is caught, and the two must land together." A design's roles and
        // a selling product's role spread can drift independently (a role added after the product was authored,
        // or a product added after roles landed), so every sold, role-bearing design is checked against every
        // product that actually sells it, not only the ones this run's roles-migrate happened to touch.
        var roleGaps = 0;
        foreach (var item in items.Where(i => i.Roles != null && i.Roles.Count > 0))
        {
            var design = db.Cache.RefOf(item).Key;
            var sellers = products.Where(p => p.Design.Key.Equals(design)).ToArray();
            foreach (var product in sellers)
            {
                var covered = new HashSet<string>((product.Roles ?? new List<ProductRole>()).Select(r => r.Role));
                var missing = item.Roles.Select(r => r.Name).Where(r => !covered.Contains(r)).ToArray();
                if (missing.Length == 0) continue;
                roleGaps++;
                Console.WriteLine($"  GAP: {product.Name} sells {item.Name} but authors no spread for: {string.Join(", ", missing)}" +
                    " -- a newly minted lot rolls the ProductRole default (mean .5) for these, not this maker's own quality");
            }
        }
        Console.WriteLine($"\n{roleGaps} (product, role) pairs where a sold, role-bearing design's product authors no matching spread");
        return 0;
    }

    // Cut 7 (docs/stats-and-power-cut.md): "products author quality, designs author roles." Zero designs
    // declared a role and zero products authored role quality before this command; every field->role table below
    // is the one place that content is decided, so the operator reviews a legible table instead of hand-picking
    // through 51 catalog records. Field names are matched against the concrete behaviour Type they appear on, not
    // by name alone, so "Range" on a launcher's guidance system and "Range" on a mining tool never collide.
    //
    // A stat is only worth pointing at a role if it actually varies with Quality *and* the value moves (Min !=
    // Max) -- Cut 1's flat placeholder stats (min == max, a bare "Heat^1" term the migration used as a harmless
    // non-empty Terms list) carry no observable role-vs-workmanship difference, so authoring a role there would
    // be authoring fiction. Fields the maps below do not mention keep generic quality; the report says why.
    private static readonly Dictionary<string, string> BallisticWeaponRoles = new Dictionary<string, string>
    {
        ["Damage"] = "barrel", ["Range"] = "barrel", ["EffectiveRange"] = "barrel", ["Velocity"] = "barrel", ["Penetration"] = "barrel",
        ["Cooldown"] = "feed mechanism", ["Spread"] = "feed mechanism", ["Heat"] = "feed mechanism",
        ["Visibility"] = "feed mechanism", ["Energy"] = "feed mechanism", ["Count"] = "feed mechanism",
    };

    private static readonly Dictionary<string, string> EnergyWeaponRoles = new Dictionary<string, string>
    {
        ["Damage"] = "focusing array", ["Range"] = "focusing array", ["EffectiveRange"] = "focusing array", ["Velocity"] = "focusing array",
        ["Penetration"] = "focusing array", ["DamageSpread"] = "focusing array",
        ["Energy"] = "power coupling", ["Heat"] = "power coupling", ["ChargeTime"] = "power coupling",
        ["ChargeEnergy"] = "power coupling", ["ChargeHeat"] = "power coupling", ["Cooldown"] = "power coupling",
        ["Spread"] = "power coupling", ["Visibility"] = "power coupling",
    };

    private static readonly Dictionary<string, string> LauncherRoles = new Dictionary<string, string>
    {
        ["Damage"] = "warhead", ["Penetration"] = "warhead", ["DamageSpread"] = "warhead",
        ["LockSpeed"] = "guidance system", ["SensorImpact"] = "guidance system", ["LockAngle"] = "guidance system",
        ["DirectionImpact"] = "guidance system", ["Decay"] = "guidance system", ["Range"] = "guidance system",
        ["MinRange"] = "guidance system",
        ["Cooldown"] = "guidance system", ["Spread"] = "guidance system", ["Visibility"] = "guidance system",
        ["Energy"] = "guidance system", ["Heat"] = "guidance system",
        ["Velocity"] = "thruster",
        // The munition thruster part's own stats. A part's fields are keyed by type and field (RoleKey), because Range
        // and Visibility also name the seeker's and plume's flat stats, which this map must not range.
        ["MunitionThrusterData.Acceleration"] = "thruster", ["MunitionThrusterData.Lateral"] = "thruster",
        ["MunitionThrusterData.MaxSpeed"] = "thruster",
    };

    // How a role map names a behaviour's stat: a munition part's fields by type and field (several parts share a field name
    // with the launcher's own stats), every other behaviour's by field.
    private static string RoleKey(BehaviorData behavior, string field) =>
        behavior is MunitionPartData ? $"{behavior.GetType().Name}.{field}" : field;

    // Warhead and dispenser: a mine layer is a launcher without guidance or thrust, so its stats split between the
    // charge it lays and the mechanism that lays it. ArmingDelay, FuseDelay and Lifetime are timings, not stats.
    private static readonly Dictionary<string, string> MineLayerRoles = new Dictionary<string, string>
    {
        ["Damage"] = "warhead", ["Penetration"] = "warhead", ["DamageSpread"] = "warhead",
        ["Range"] = "dispenser", ["Cooldown"] = "dispenser", ["Spread"] = "dispenser", ["Velocity"] = "dispenser",
        ["Energy"] = "dispenser", ["Heat"] = "dispenser", ["Visibility"] = "dispenser",
    };

    private static readonly Dictionary<string, string> RadiatorRoles = new Dictionary<string, string>
    {
        ["Emissivity"] = "fins", ["WasteHeat"] = "fins",
        ["PumpedHeat"] = "pump", ["EnergyUsage"] = "pump",
        ["ThermalMass"] = "reservoir",
    };

    private static readonly Dictionary<string, string> ReactorRoles = new Dictionary<string, string>
    {
        ["Charge"] = "core", ["Efficiency"] = "core", ["Modifier"] = "core",
        ["OverloadEfficiency"] = "regulator", ["ThrottlingFactor"] = "regulator",
    };

    private static readonly Dictionary<string, string> SensorRoles = new Dictionary<string, string>
    {
        ["Sensitivity"] = "receiver",
        ["PingBoost"] = "emitter", ["PingEnergy"] = "emitter", ["PingRange"] = "emitter",
        ["PingCooldown"] = "emitter", ["InterceptFactor"] = "emission-discipline",
    };

    private static readonly Dictionary<string, string> ThrusterRoles = new Dictionary<string, string>
    {
        ["Thrust"] = "nozzle", ["Visibility"] = "nozzle",
        ["Heat"] = "injector", ["EnergyUsage"] = "injector",
    };

    private static readonly Dictionary<string, string> HullShipRoles = new Dictionary<string, string>
    {
        ["CrossSection"] = "plating",
    };

    // Tool designs are not a shared kind the way a laser or a radiator is -- each is its own bespoke gadget, so
    // its role map is keyed by design name rather than by behaviour field, same as the census already treats
    // "Tool" as a catch-all HardpointType. A design with no entry here carries no field with a natural role
    // (Assembly Line, Deep Ore Extractor, Refinery, Shipyard, Surface Ore Extractor carry no PerformanceStat
    // behaviour at all in the current catalog).
    private static readonly Dictionary<string, Dictionary<string, string>> ToolRolesByDesign =
        new Dictionary<string, Dictionary<string, string>>
        {
            ["Industrial Thermostatic Heater"] = new Dictionary<string, string>
            {
                ["EnergyDraw"] = "heating element", ["Heat"] = "heating element",
            },
            ["Capacitor"] = new Dictionary<string, string>
            {
                ["Capacity"] = "storage cell", ["Efficiency"] = "converter",
            },
            ["Passive Array"] = new Dictionary<string, string> { ["Multiple"] = "receiver" },
            ["Inspection Scanner"] = new Dictionary<string, string>
            {
                ["Range"] = "scan array", ["ScanTime"] = "scan array", ["InterceptFactor"] = EmissionDiscipline,
            },
        };

    // One Mean / StandardDeviation per maker, applied to every role of a design the maker sells (catalog-growth
    // addenda, "Maker profiles"); the Pirates are middling and inconsistent (pirates-record). Read by ProductLinesCommand too.
    internal static readonly Dictionary<string, (float Mean, float Dev)> MakerProfiles = new Dictionary<string, (float Mean, float Dev)>
    {
        ["Zhestokost"] = (.50f, .08f), ["AU"] = (.50f, .12f), ["Lightsail"] = (.55f, .10f), ["R&D"] = (.60f, .15f),
        ["Lucent"] = (.60f, .15f), ["NiteLife"] = (.55f, .12f), ["Finch"] = (.65f, .20f), ["Alakrita"] = (.60f, .14f),
        ["DME"] = (.55f, .20f), ["Adrasteia"] = (.60f, .10f),
    };

    private static readonly (float Mean, float Dev) PirateProfile = (.45f, .22f);

    private static string KindOf(EquippableItemData item) =>
        item is HullData hull ? $"Hull/{hull.HullType}"
        : item is DockingBayData ? "DockingBay"
        : item is CargoBayData ? "CargoBay"
        : item is WeaponItemData weapon ? $"Weapon/{weapon.HardpointType}"
        : item.HardpointType.ToString();

    // The field->role map for one design, or null when its kind carries no per-part behaviour stat at all
    // (CargoBay, DockingBay, ControlModule, Hull/Station, Hull/Turret -- none of their behaviours read
    // StatSource.Quality today, so there is no natural role to author and the report says so per design).
    private static Dictionary<string, string> RoleMapFor(EquippableItemData item, string kind) =>
        item.Behaviors != null && item.Behaviors.OfType<MineLayerData>().Any() ? MineLayerRoles : kind switch
    {
        "Weapon/Ballistic" => BallisticWeaponRoles,
        "Weapon/Energy" => EnergyWeaponRoles,
        "Weapon/Launcher" => LauncherRoles,
        "Radiator" => RadiatorRoles,
        "Reactor" => ReactorRoles,
        "Sensors" => SensorRoles,
        "Thruster" => ThrusterRoles,
        _ when kind == "Hull/Ship" => HullShipRoles,
        "Tool" => ToolRolesByDesign.TryGetValue(item.Name, out var byName) ? byName : null,
        _ => null,
    };

    // The field->role map roles-backfill would apply to this design, or null; the catalog tests ask the tool, so they keep no
    // copy of the kinds or the maps.
    public static IReadOnlyDictionary<string, string> RoleMapOf(EquippableItemData item) => RoleMapFor(item, KindOf(item));

    // Whether CB-R1 maps this design's stats to roles.
    public static bool HasRoleMap(EquippableItemData item) => RoleMapOf(item) != null;

    private sealed record RoleAuthoring(
        EquippableItemData Item, string Kind, List<(string Behavior, string Field, string Role)> StatsAssigned,
        List<string> RolesDeclared, string SkipReason);

    // The content pass itself: declares roles on designs, points each design's own StatTerm.Role at the role that
    // governs it, and authors matching ProductRole quality on every product that sells a now-rolegetting design.
    // Dry run unless passed "apply", which alone opens the catalog writable; every record must derive cleanly or
    // nothing is written, the same all-or-nothing contract ShieldMigrate and BrownoutMigrate already use. Prints
    // the operator review table (grouped by item kind) either way.
    private static int RolesMigrate(bool apply)
    {
        var db = AetherDb.Open(catalogWritable: apply);
        var items = db.Cache.GetAll<EquippableItemData>().ToArray();
        var products = db.Cache.GetAll<FactionProductData>().ToArray();
        var changedDesigns = new List<(object Document, CultRecordKey Key)>();
        var changedProducts = new List<(object Document, CultRecordKey Key)>();
        var report = new List<RoleAuthoring>();

        foreach (var item in items.OrderBy(i => KindOf(i)).ThenBy(i => i.Name, StringComparer.Ordinal))
        {
            var kind = KindOf(item);
            var map = RoleMapFor(item, kind);
            if (map == null)
            {
                report.Add(new RoleAuthoring(item, kind, new List<(string, string, string)>(), new List<string>(),
                    "kind carries no behaviour reading StatSource.Quality; generic quality stays"));
                continue;
            }

            var rolesUsed = new SortedSet<string>(StringComparer.Ordinal);
            var assigned = new List<(string Behavior, string Field, string Role)>();
            foreach (var behavior in item.Behaviors ?? new List<BehaviorData>())
            {
                if (behavior == null) continue;
                foreach (var field in behavior.GetType().GetFields().Where(f => f.FieldType == typeof(PerformanceStat)))
                {
                    if (!(field.GetValue(behavior) is PerformanceStat stat) || stat.Terms == null) continue;
                    if (stat.Min == stat.Max) continue; // flat: no observable role-vs-workmanship difference
                    var qualityTerm = stat.Terms.FirstOrDefault(t => t.Source == StatSource.Quality);
                    if (qualityTerm == null || !string.IsNullOrEmpty(qualityTerm.Role)) continue;
                    if (!map.TryGetValue(RoleKey(behavior, field.Name), out var role)) continue;
                    qualityTerm.Role = role;
                    rolesUsed.Add(role);
                    assigned.Add((behavior.GetType().Name, field.Name, role));
                }
            }

            if (rolesUsed.Count == 0)
            {
                report.Add(new RoleAuthoring(item, kind, assigned, new List<string>(),
                    "no quality-bearing, non-flat stat matched this kind's role map"));
                continue;
            }

            item.Roles = rolesUsed.Select(r => new ItemRole { Name = r }).ToList();
            report.Add(new RoleAuthoring(item, kind, assigned, item.Roles.Select(r => r.Name).ToList(), null));
            changedDesigns.Add((item, db.Cache.RefOf(item).Key));
        }

        // Products author quality for every role their design now declares. A design's Roles may also include
        // roles authored on an earlier pass (none exist yet, but the loop is idempotent either way): every sold
        // product is checked against the full current Roles list, not just what changed this run.
        var productAuthoring = new List<(FactionProductData Product, string Design, List<(string Role, float Mean, float Dev, string Basis)> Added)>();
        var byDesign = items.ToDictionary(i => db.Cache.RefOf(i).Key, i => i);
        foreach (var product in products.OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (!byDesign.TryGetValue(product.Design.Key, out var design) || design.Roles == null || design.Roles.Count == 0)
                continue;
            var existing = new HashSet<string>((product.Roles ?? new List<ProductRole>()).Select(r => r.Role));
            var added = new List<(string, float, float, string)>();
            foreach (var role in design.Roles)
            {
                if (existing.Contains(role.Name)) continue;
                var (mean, dev, basis) = AuthorProductRole(db, product, design.Name, role.Name);
                product.Roles.Add(new ProductRole { Role = role.Name, Mean = mean, StandardDeviation = dev });
                added.Add((role.Name, mean, dev, basis));
            }
            if (added.Count == 0) continue;
            productAuthoring.Add((product, design.Name, added));
            changedProducts.Add((product, db.Cache.RefOf(product).Key));
        }

        PrintRolesReport(report, productAuthoring);

        var totalDesigns = report.Count(r => r.SkipReason == null);
        var totalStats = report.Sum(r => r.StatsAssigned.Count);
        Console.WriteLine($"\n{totalDesigns} designs authored roles ({report.Count - totalDesigns} left at generic quality), " +
            $"{totalStats} stats pointed at a role, {changedProducts.Count} products authored role quality.");

        if (changedDesigns.Count == 0 && changedProducts.Count == 0) return 0;
        if (!apply)
        {
            Console.WriteLine($"\nDry run. Pass \"apply\" to land {changedDesigns.Count} design(s) and {changedProducts.Count} product(s).");
            return 0;
        }

        db.Cache.Commit(batch =>
        {
            foreach (var (document, key) in changedDesigns) batch.Upsert(document.GetType(), document, key);
            foreach (var (document, key) in changedProducts) batch.Upsert(document.GetType(), document, key);
        });
        Console.WriteLine($"Landed {changedDesigns.Count} design(s) and {changedProducts.Count} product(s) in Aetheria.cc");
        return 0;
    }

    // A stable (process-independent) hash: string.GetHashCode() is randomized per process, which would make a dry
    // run and the later "apply" run author different numbers for the same product. Deterministic FNV-1a instead.
    private static uint StableHash(string s)
    {
        unchecked
        {
            var h = 2166136261u;
            foreach (var c in s) { h ^= c; h *= 16777619u; }
            return h;
        }
    }

    // Alakrita's Arctica is the one product the lore already speaks for (docs/item-provenance-target.md: "Arctica
    // is Alakrita branding, by the name I presume they're optimizing for heat efficiency"): its radiating fins get
    // a real premium and tight quality control, everything else on it stays near the design-wide default. Every
    // other product has no established brand identity yet, so its means and deviations are derived deterministically
    // from (product name, role) -- reproducible across a dry run and the later apply, and varied enough that two
    // products of one design will not coincide by construction -- and are named as generic in the report rather
    // than presented as lore. The operator review (O7) is exactly where a real identity replaces a generic one.
    private static (float Mean, float Dev, string Basis) AuthorProductRole(AetherDb db, FactionProductData product, string designName, string role)
    {
        var maker = db.Cache.Get(product.Manufacturer)?.Name;
        if (designName == "Adaptive Radiator" && maker == "Alakrita" && role == "fins")
            return (.78f, .07f, "lore: Alakrita brands on heat efficiency (item-provenance-target.md)");

        var hash = StableHash($"{product.Name}|{role}");
        var mean = .45f + (hash % 1000) / 1000f * .3f; // [.45, .75)
        var dev = .08f + (hash / 1000 % 1000) / 1000f * .1f; // [.08, .18)
        return (mean, dev, "generic: no established brand identity, needs operator review");
    }

    private static void PrintRolesReport(List<RoleAuthoring> report,
        List<(FactionProductData Product, string Design, List<(string Role, float Mean, float Dev, string Basis)> Added)> productAuthoring)
    {
        Console.WriteLine("=== Cut 7: roles authored, by item kind ===");
        foreach (var kindGroup in report.GroupBy(r => r.Kind).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"\n-- {kindGroup.Key} --");
            foreach (var r in kindGroup.OrderBy(r => r.Item.Name, StringComparer.Ordinal))
            {
                if (r.SkipReason != null)
                {
                    Console.WriteLine($"  {r.Item.Name,-28} (none)  -- {r.SkipReason}");
                    continue;
                }
                Console.WriteLine($"  {r.Item.Name,-28} roles: {string.Join(", ", r.RolesDeclared)}");
                foreach (var (behavior, field, role) in r.StatsAssigned)
                    Console.WriteLine($"      {behavior}.{field,-18} -> {role}");
            }
        }

        Console.WriteLine("\n=== Cut 7: product role quality authored ===");
        foreach (var (product, design, added) in productAuthoring.OrderBy(p => p.Product.Name, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {product.Name,-28} ({design})");
            foreach (var (role, mean, dev, basis) in added)
                Console.WriteLine($"      {role,-18} mean {mean:0.00} dev {dev:0.00}  {basis}");
        }
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
                var matches = gear.Where(hardpoint.Takes).ToArray();
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
        Console.WriteLine($"{"faction",-26} {"short",-13} {"geonames",-22} {"boss hull",-14} {"influence",-9} {"doctrine",-10} products");
        var broken = 0;
        foreach (var faction in db.Cache.GetAll<Faction>().OrderBy(f => f.Name))
        {
            var key = db.Cache.RefOf(faction).Key;
            var geonames = faction.GeonameFile.IsSet() ? "set" : "UNSET";
            var boss = !faction.BossHull.IsSet()
                ? "none"
                : db.Cache.Get(faction.BossHull)?.Name ?? "DANGLING";
            if (geonames == "UNSET" || boss == "DANGLING") broken++;
            Console.WriteLine($"{faction.Name,-26} {faction.ShortName,-13} {geonames,-22} {boss,-14} {faction.InfluenceDistance,-9} {(faction.Doctrine == null ? "none" : faction.Doctrine.EngageOn.ToString()),-10} " +
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
    // flight to a populated sector. No galaxy, so this covers placement, roles and products but NOT
    // availability filtering or distance weighting, which need a real galaxy. Seeded: a failure repeats.
    private static int Loadout(string[] args)
    {
        var seed = uint.TryParse(args.FirstOrDefault(), out var parsed) ? parsed : 1u;
        var db = AetherDb.Open();
        var settings = new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp()
        };

        var log = new List<string>();
        var itemManager = new ItemManager(db.Cache, new ProvenanceLedger(), settings, log.Add);
        var failures = 0;
        var targetingGaps = 0;

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
                if (pack != null && log.Any(l => l.Contains("targeting system available", StringComparison.OrdinalIgnoreCase))) targetingGaps++;
            }
            catch (Exception e)
            {
                outcome = $"THREW {e.GetType().Name}: {e.Message}";
                failures++;
            }

            Console.WriteLine($"{hull.Name,-12} ({hull.HullType,-7}) {outcome}");
            foreach (var line in log.Distinct()) Console.WriteLine($"      {line}");
        }

        Console.WriteLine($"\n{failures} hulls failed to generate a loadout (seed {seed}), {targetingGaps} armed hulls generated with no targeting system (fired unaided)");
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
    // one performance stat each of the three curve-eligible behaviours (EnergyDraw has none -- see EnergyDraw.cs)
    // actually reads for its continuous effect: ThrusterData.Thrust,
    // RadiatorData.PumpedHeat, ConstantWeaponData.Damage. One of the three (RadiatorData.PumpedHeat)
    // is ALSO a power-request field (StatValidation.PowerRequestFields) -- at the time
    // this tool authored the shipped catalog's curves that briefly made that record illegal under F6's
    // now-deleted static check (StatValidation.ValidateNoPowerSupplyOnRequest); the nominal-request ruling
    // (docs/stats-and-power-cut.md, operator ruling 2026-09-19, see ItemData.cs's PowerRequestFields comment)
    // resolved that by having PowerRequest read request fields nominally instead of forbidding the term, so
    // authoring it here on a request field is legal again. Exponent 1 (linear) is the gentle default the ruling
    // calls for -- half supply reads as half performance, not a cliff -- and is left for the operator to steepen
    // per design later. Dry run unless passed "apply"; every record must derive cleanly or nothing is written,
    // same contract as ShieldMigrate.
    private static int BrownoutMigrate(bool apply)
    {
        var db = AetherDb.Open(catalogWritable: apply);
        const float gentleExponent = 1f;

        // (behaviour type, target performance-stat field, short label) -- one of these is also a request field
        // (StatValidation.PowerRequestFields); that is legal under the nominal-request ruling above.
        var targets = new (Type BehaviorType, string Field, string Label)[]
        {
            (typeof(ThrusterData), nameof(ThrusterData.Thrust), "thrust"),
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

    // Cut 1 (docs/fire-control-cut.md, "Catalog" under Cut 1 / Q1): a turret hardpoint authors
    // FiringArc: 360 -- no new type for what a number already says (R6 already gives every other hardpoint
    // the GameplaySettings.FiringArc default via 0). Scoped to the Turret hull's own hardpoints, the only
    // ones the cut names; other hulls' hardpoints are left at 0 (default arc) until a design pass says
    // otherwise. Dry run unless passed "apply", same contract as ShieldMigrate/BrownoutMigrate.
    private static int FiringArcMigrate(bool apply)
    {
        var db = AetherDb.Open(catalogWritable: apply);

        var hulls = db.Cache.GetAll<HullData>().Where(h => h.Name == "Turret").ToArray();
        if (hulls.Length == 0)
        {
            Console.WriteLine("No hull named \"Turret\" found.");
            return 1;
        }

        Console.WriteLine($"{"hull",-16} {"hardpoint",-10} {"position",-10} {"was",5} {"now",5}");
        var changed = new List<(object Document, CultRecordKey Key)>();
        foreach (var hull in hulls)
        {
            var key = db.Cache.RefOf(hull).Key;
            var touchedThisHull = false;
            foreach (var hardpoint in hull.Hardpoints)
            {
                if (hardpoint.Type != HardpointType.Ballistic) continue;
                var position = $"({hardpoint.Position.x},{hardpoint.Position.y})";
                var was = hardpoint.FiringArc;
                if (was >= 360f)
                {
                    Console.WriteLine($"{hull.Name,-16} {hardpoint.Type,-10} {position,-10} {was,5:0} {was,5:0}  (already authored, left alone)");
                    continue;
                }
                hardpoint.FiringArc = 360f;
                Console.WriteLine($"{hull.Name,-16} {hardpoint.Type,-10} {position,-10} {was,5:0} {360,5:0}");
                touchedThisHull = true;
            }
            if (touchedThisHull) changed.Add((hull, key));
        }

        Console.WriteLine($"\n{changed.Count} hull records migrated");
        if (changed.Count == 0) return 0;
        if (!apply)
        {
            Console.WriteLine($"Dry run. Pass \"apply\" to land {changed.Count} changed records.");
            return 0;
        }

        foreach (var (document, _) in changed) CultRecordRefs.Validate(document);
        db.Cache.Commit(batch =>
        {
            foreach (var (document, key) in changed) batch.Upsert(document.GetType(), document, key);
        });
        Console.WriteLine($"Landed {changed.Count} changed records in Aetheria.cc");
        return 0;
    }

    // Damage falloff (aetheria-release, damage-curve-fate-ruled): every gun and beam authors an effective range at a
    // fraction of its Range (Min and Max scaled, Terms copied so the effective range wears and heats as the range does)
    // and a damage multiplier at maximum range. Guided weapons, lock weapons and mine layers keep the unauthored
    // defaults (no falloff). The command upserts every weapon design, which is also what drops the retired damage curve
    // (key 7) from the stored records. Idempotent: the values are functions of Range alone. Dry run unless "apply".
    private static int RangeFalloff(string[] args)
    {
        var apply = args.Contains("apply");
        float Option(string name, float fallback)
        {
            var at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? float.Parse(args[at + 1], System.Globalization.CultureInfo.InvariantCulture) : fallback;
        }
        var fraction = Option("--effective", .5f);
        var end = Option("--end", 0f);
        var db = AetherDb.Open(catalogWritable: apply);
        // A record is rewritten only when what it would store differs from what is stored: that is what drops the retired
        // key 7 from a record the command leaves otherwise unchanged, and what makes a second apply change no byte.
        var persisted = CultDocumentMessagePackSerialization.DeserializeSnapshot(File.ReadAllBytes(Path.Combine(db.Root, "GameData", "Aetheria.cc")))
            .Records.ToDictionary(r => r.Key, r => r.Payload);

        Console.WriteLine($"{"weapon",-28} {"behaviour",-20} {"range",9} {"effective",9} {"end",5}");
        var changed = new List<(object Document, CultRecordKey Key)>();
        foreach (var item in db.Cache.GetAll<WeaponItemData>().OrderBy(i => i.Name, StringComparer.Ordinal))
        {
            var key = db.Cache.RefOf(item).Key;
            foreach (var weapon in item.Behaviors.OfType<WeaponData>())
            {
                var falls = !(weapon is LockWeaponData || weapon is GuidedWeaponData || weapon is MineLayerData);
                if (falls)
                {
                    weapon.EffectiveRange = new PerformanceStat
                    {
                        Min = weapon.Range.Min * fraction, Max = weapon.Range.Max * fraction,
                        Terms = weapon.Range.Terms.Select(t => new StatTerm { Source = t.Source, Exponent = t.Exponent, Role = t.Role }).ToList()
                    };
                    weapon.DamageAtMaxRange = end;
                }
                Console.WriteLine($"{item.Name,-28} {weapon.GetType().Name,-20} {weapon.Range.Max,9:0.##} {(falls ? weapon.EffectiveRange.Max : float.PositiveInfinity),9:0.##} {weapon.DamageAtMaxRange,5:0.##}");
            }
            if (!persisted[key.Value].SequenceEqual(CultDocumentMessagePackSerialization.SerializeUntyped(item, item.GetType(), db.Cache.Registry)))
                changed.Add((item, key));
        }

        Console.WriteLine($"\n{changed.Count} weapon records to rewrite");
        if (changed.Count == 0) return 0;
        if (!apply)
        {
            Console.WriteLine($"Dry run. Pass \"apply\" to land {changed.Count} records.");
            return 0;
        }
        foreach (var (document, _) in changed) CultRecordRefs.Validate(document);
        db.Cache.Commit(batch =>
        {
            foreach (var (document, key) in changed) batch.Upsert(document.GetType(), document, key);
        });
        Console.WriteLine($"Landed {changed.Count} records in Aetheria.cc");
        return 0;
    }

    // Fire control Cut 6a (docs/fire-control-cut.md, Cut 2's "Catalog:" bullet). Cut 2 shipped the behaviour and
    // never shipped the data: no targeting-system design exists, so tools/AetherDb loadout throws
    // InvalidLoadoutException ("No compatible targeting system found for entity!") for every armed hull. This
    // authors the two designs Cut 2 asked for (1-cell and 2-cell, each with a NiteLife product -- the one
    // manufacturer that already sells the capacitor, so IsAvailable finds one without falling back to "any
    // manufacturer") plus one product each for the four designs nothing currently sells (plight, pswarm, Core
    // Power, Large Drive), matching each to the manufacturer its own flavour text or the roster already implies.
    // Dry run unless passed "apply", same contract as the other *-migrate commands.
    private static int TargetingCatalog(bool apply)
    {
        var db = AetherDb.Open(catalogWritable: apply);

        Faction FactionByShortName(string shortName)
        {
            var faction = db.Cache.GetAll<Faction>().FirstOrDefault(f => f.ShortName == shortName);
            if (faction == null) throw new InvalidOperationException($"No faction with short name \"{shortName}\".");
            return faction;
        }

        EquippableItemData DesignByName(string name)
        {
            var design = db.Cache.GetAll<EquippableItemData>().FirstOrDefault(i => i.Name == name);
            if (design == null) throw new InvalidOperationException($"No design named \"{name}\".");
            return design;
        }

        PerformanceStat Flat(float value) => new PerformanceStat { Min = value, Max = value, Terms = new List<StatTerm>() };

        PerformanceStat Rolled(float min, float max, string role) => new PerformanceStat
        {
            Min = min,
            Max = max,
            Terms = new List<StatTerm>
            {
                new StatTerm { Source = StatSource.Heat, Exponent = 0.0625f },
                new StatTerm { Source = StatSource.Durability, Exponent = 0.25f },
                new StatTerm { Source = StatSource.Quality, Exponent = 2f, Role = role },
            },
        };

        // Both roles are new vocabulary (docs/stats-power-cut7-roles.md's convention is one vocabulary per new
        // behaviour kind, same as content-batch-one's shield "emitter/reservoir/regulator"): "processor" for the
        // aim-quality half (Accuracy, Precision), "array" for the sensor/tracking-hardware half (Tracking).
        // Resolution carries no role and no terms -- it is authored as a flat info-level threshold per design
        // (see the design-time note below), not a stat that wear or manufacturing quality moves.
        GearData BuildTargetingDesign(string name, string description, int cells, int durability, float mass,
            int price, float minTemp, float maxTemp, float optimal, float plateau,
            (float min, float max) accuracy, float resolution, (float min, float max) precision,
            (float min, float max) tracking, float energyDraw, float heat)
        {
            var shape = new Shape();
            if (cells > 1) shape.Width = cells;
            return new GearData
            {
                Name = name,
                Description = description,
                Hardpoint = HardpointType.Tool,
                Shape = shape,
                Mass = mass,
                Price = price,
                Durability = durability,
                MinimumTemperature = minTemp,
                MaximumTemperature = maxTemp,
                ThermalResilience = 1,
                OptimalTemperature = optimal,
                PlateauWidth = plateau,
                SpecificHeat = 1,
                Conductivity = 1,
                Roles = new List<ItemRole> { new ItemRole { Name = "processor" }, new ItemRole { Name = "array" } },
                Behaviors = new List<BehaviorData>
                {
                    new TargetingSystemData
                    {
                        Accuracy = Rolled(accuracy.min, accuracy.max, "processor"),
                        Resolution = Flat(resolution),
                        Precision = Rolled(precision.min, precision.max, "processor"),
                        Tracking = Rolled(tracking.min, tracking.max, "array"),
                    },
                    new EnergyDrawData { EnergyDraw = Flat(energyDraw), PerSecond = true },
                    new HeatData { Heat = Flat(heat), PerSecond = true },
                },
            };
        }

        // Sibling argued: PotaT+- (the capacitor, the only other bare Tool gadget in the catalog) is 1x1,
        // Durability 50, Mass 75, Price 75000, heat response 200-500/276.5/36. GameplaySettings.UnaidedAccuracy
        // is .05 (AetherDb settings) and GameplaySettings.UnaidedTracking will be 10 once Cut 5 lands (not yet on
        // this branch) -- both designs clear it by 50% or more. Resolution is placed against the live thresholds
        // this branch reads from AetherDb settings: TargetDetectionInfoThreshold .1,
        // TargetGearInfoThreshold .8. FireControl.HitProbability reads
        // pSensor = saturate(unlerp(TargetDetectionInfoThreshold, Resolution, info)), so a HIGHER Resolution
        // needs MORE gathered info before sensor state stops limiting hits -- it is a cost, not a bonus. The
        // 1-cell system is placed low (.3, just above the detection floor) so it reaches its own modest ceiling
        // off a cheap scan; the 2-cell system is placed high (.75, near the gear-info tier) so its much higher
        // ceiling is only fully reachable after sustained scanning. That is an authored reading of "the 1-cell
        // design lower than the 2-cell", not a re-derivation of the spec, and the report flags it for review.
        var targetingComputer = BuildTargetingDesign(
            "Targeting Computer",
            "Resolves sensor return into a firing solution for the weapons it feeds.",
            cells: 1, durability: 50, mass: 60, price: 60000,
            minTemp: 200, maxTemp: 500, optimal: 276.5f, plateau: 36,
            accuracy: (.45f, .6f), resolution: .3f, precision: (.05f, .2f), tracking: (15f, 25f),
            energyDraw: 2f, heat: 15f);

        var fireControlArray = BuildTargetingDesign(
            "Fire Control Array",
            "A larger fire-control computer: a dedicated tracking array pairs with deeper prediction, at higher mass and price.",
            cells: 2, durability: 60, mass: 110, price: 150000,
            minTemp: 220, maxTemp: 480, optimal: 270f, plateau: 28,
            accuracy: (.65f, .85f), resolution: .75f, precision: (.35f, .55f), tracking: (30f, 45f),
            energyDraw: 4f, heat: 25f);

        var newDesigns = new (GearData Design, string ProductName, Faction Maker, string ProductDescription, (string Role, float Mean, float Dev)[] Roles)[]
        {
            (targetingComputer, "LockOn", FactionByShortName("NiteLife"),
                "Keeps the lights on your target.",
                new[] { ("processor", .55f, .15f), ("array", .5f, .16f) }),
            (fireControlArray, "LockOn Pro", FactionByShortName("NiteLife"),
                "Same promise, sharper lock.",
                new[] { ("processor", .72f, .12f), ("array", .68f, .13f) }),
        };

        // Part 3: one product each for the four designs nothing currently sells. Manufacturer assignment is the
        // brief's own (fire-control-cut.md Cut 6a): plight -> Lucent Media, pswarm -> Aeronautics Unlimited,
        // Core Power -> Rossum & Douglas, Large Drive -> Lightsail Express. Role coverage matches each design's
        // own already-declared Roles (read from the catalog, not invented): plight [power coupling, focusing
        // array], pswarm [guidance system, thruster, warhead], Core Power [core, regulator], Large Drive [] (it
        // declares no roles today, so its product declares none either -- adding roles to an existing design is
        // outside this cut's brief).
        var existingDesigns = new (string DesignName, string ProductName, Faction Maker, string ProductDescription, (string Role, float Mean, float Dev)[] Roles)[]
        {
            ("Lightning Projector", "DragOnBreath", FactionByShortName("Lucent"),
                "Chain lightning has your enemies chain smoking while you enjoy a relaxing DragOnBreath vapepop, now available in 17! flavours (manufactured by DragOnBreath, a subsidary of Lucent Media)",
                new[] { ("focusing array", .7f, .13f), ("power coupling", .55f, .15f) }),
            ("Swarm Rocket Pod", "Leonid", FactionByShortName("AU"),
                "A rain of pain that falls mainly on space planes.",
                new[] { ("guidance system", .65f, .14f), ("thruster", .63f, .15f), ("warhead", .7f, .13f) }),
            ("Compact Reactor", "Steadfast", FactionByShortName("R&D"),
                "It gets the job done, but that's all. Rated for continuous operation. \"Continuous\" does not imply \"uninterrupted.\"",
                new[] { ("core", .5f, .12f), ("regulator", .5f, .12f) }),
            ("Large Drive", "True North", FactionByShortName("Lightsail"),
                "Gets your cargo there. Getting you there is your problem.",
                Array.Empty<(string, float, float)>()),
        };

        Console.WriteLine("New designs:");
        foreach (var (design, productName, maker, _, _) in newDesigns)
            Console.WriteLine($"  {design.Name} ({design.Shape.Width}x{design.Shape.Height}, {design.Durability} durability, {design.Mass}kg, {design.Price}c) -> product \"{productName}\" by {maker.ShortName}");
        Console.WriteLine("\nNew products for existing unsold designs:");
        foreach (var (designName, productName, maker, _, roles) in existingDesigns)
            Console.WriteLine($"  {designName} -> \"{productName}\" by {maker.ShortName}, roles [{string.Join(", ", roles.Select(r => r.Role))}]");

        if (!apply)
        {
            Console.WriteLine($"\nDry run. Pass \"apply\" to land {newDesigns.Length} new designs and {newDesigns.Length + existingDesigns.Length} new products.");
            return 0;
        }

        // Validate before staging, same as ShieldMigrate/BrownoutMigrate/RolesMigrate/FiringArcMigrate above:
        // CultRecordRefs.Validate is the body AetheriaStores.Open and cache.Upsert<T> both run, but a raw
        // batch.Upsert(Type, document, key) -- needed here because a product must land in the same batch as the
        // design it references, before that design has a minted key of its own -- does not call it.
        foreach (var (design, _, _, _, _) in newDesigns) CultRecordRefs.Validate(design);

        db.Cache.Commit(batch =>
        {
            var designKeys = new Dictionary<string, CultRecordKey>();
            foreach (var (design, _, _, _, _) in newDesigns)
                designKeys[design.Name] = batch.Upsert(typeof(GearData), design);

            foreach (var (design, productName, maker, description, roles) in newDesigns)
            {
                var product = new FactionProductData
                {
                    Name = productName,
                    Description = description,
                    Design = new CultRecordRef<CraftedItemData>(designKeys[design.Name]),
                    Manufacturer = db.Cache.RefOf(maker),
                    Roles = roles.Select(r => new ProductRole { Role = r.Role, Mean = r.Mean, StandardDeviation = r.Dev }).ToList(),
                };
                batch.Upsert(typeof(FactionProductData), product);
            }

            foreach (var (designName, productName, maker, description, roles) in existingDesigns)
            {
                var design = DesignByName(designName);
                var product = new FactionProductData
                {
                    Name = productName,
                    Description = description,
                    Design = new CultRecordRef<CraftedItemData>(db.Cache.RefOf(design).Key),
                    Manufacturer = db.Cache.RefOf(maker),
                    Roles = roles.Select(r => new ProductRole { Role = r.Role, Mean = r.Mean, StandardDeviation = r.Dev }).ToList(),
                };
                batch.Upsert(typeof(FactionProductData), product);
            }
        });

        Console.WriteLine($"\nLanded {newDesigns.Length} new designs and {newDesigns.Length + existingDesigns.Length} new products in Aetheria.cc");
        return 0;
    }

    // Fire control Cut 6c (docs/fire-control-cut.md, "the formula gets fixed, and the single point of
    // failure"). Two parts:
    //
    // 6c.1 (operator ruling 2026-09-19): FireControl.HitProbability now takes Resolution's reciprocal to
    // derive the sensor-limiting info ceiling, so a bigger authored number is a bigger benefit, matching
    // every other stat on this behaviour. Cut 6a's two designs were authored against the old (ceiling-read-
    // directly) reading -- Targeting Computer at .3, Fire Control Array at .75 -- which is why the premium
    // design scored strictly worse at low info. Re-authored on the new scale: 1-cell 2 (ceiling
    // .1 + .9/2 = .55), 2-cell 4 (ceiling .1 + .9/4 = .325). Both clear the unaided ceiling of 1 by a wide
    // margin (lower is better under the new reading), and the 2-cell design is unambiguously ahead of the
    // 1-cell one at every info level, which was the whole point of the fix.
    //
    // 6c.2: NiteLife Energy was the only seller of both designs -- "at least the manufacturers that sell the
    // capacitor" (Cut 2's letter) rather than the thematic sellers the roster actually names for targeting
    // hardware (Cut 2's intent). Finch Cybernetics (passive sensors) and Lucent Media (active sensors, lasers)
    // both get a product per design, so a galaxy that does not draw NiteLife still has two other chances to
    // sell one. Brand names and flavour text are each manufacturer's own; the design stays dry and carries no
    // manufacturer field (standing text policy). Role coverage matches the designs' own already-declared Roles
    // (processor, array), same convention TargetingCatalog's newDesigns used above.
    //
    // Dry run unless passed "apply", same contract as the other *-migrate commands.
    private static int TargetingCatalog6c(bool apply)
    {
        var db = AetherDb.Open(catalogWritable: apply);

        Faction FactionByShortName(string shortName)
        {
            var faction = db.Cache.GetAll<Faction>().FirstOrDefault(f => f.ShortName == shortName);
            if (faction == null) throw new InvalidOperationException($"No faction with short name \"{shortName}\".");
            return faction;
        }

        GearData DesignByName(string name)
        {
            var design = db.Cache.GetAll<GearData>().FirstOrDefault(i => i.Name == name);
            if (design == null) throw new InvalidOperationException($"No design named \"{name}\".");
            return design;
        }

        var targetingComputer = DesignByName("Targeting Computer");
        var fireControlArray = DesignByName("Fire Control Array");

        var resolutionChanges = new (GearData Design, float OldResolution, float NewResolution)[]
        {
            (targetingComputer, targetingComputer.Behaviors.OfType<TargetingSystemData>().Single().Resolution.Max, 2f),
            (fireControlArray, fireControlArray.Behaviors.OfType<TargetingSystemData>().Single().Resolution.Max, 4f),
        };

        var newProducts = new (GearData Design, string ProductName, Faction Maker, string ProductDescription, (string Role, float Mean, float Dev)[] Roles)[]
        {
            (targetingComputer, "Panopticon", FactionByShortName("Finch"),
                "Sees everything, judges nothing. Well, mostly nothing.",
                new[] { ("processor", .5f, .13f), ("array", .58f, .12f) }),
            (fireControlArray, "Panopticon Prime", FactionByShortName("Finch"),
                "The upgrade nobody at Finch will admit they needed until they had it.",
                new[] { ("processor", .68f, .11f), ("array", .75f, .10f) }),
            (targetingComputer, "ClapBack", FactionByShortName("Lucent"),
                "Your target won't clock it until the killcam.",
                new[] { ("processor", .6f, .16f), ("array", .48f, .15f) }),
            (fireControlArray, "ClapBack Ultra", FactionByShortName("Lucent"),
                "Now with main-character energy built in.",
                new[] { ("processor", .78f, .14f), ("array", .65f, .13f) }),
        };

        Console.WriteLine("Resolution re-authored to the reciprocal-reading scale (6c.1):");
        foreach (var (design, oldResolution, newResolution) in resolutionChanges)
            Console.WriteLine($"  {design.Name,-20} {oldResolution,6:0.##} -> {newResolution,4:0.##}");

        Console.WriteLine("\nNew products (6c.2, thematic sellers alongside NiteLife):");
        foreach (var (design, productName, maker, _, roles) in newProducts)
            Console.WriteLine($"  {design.Name} -> \"{productName}\" by {maker.ShortName}, roles [{string.Join(", ", roles.Select(r => r.Role))}]");

        if (!apply)
        {
            Console.WriteLine($"\nDry run. Pass \"apply\" to land {resolutionChanges.Length} Resolution changes and {newProducts.Length} new products.");
            return 0;
        }

        foreach (var (design, _, newResolution) in resolutionChanges)
        {
            var behavior = design.Behaviors.OfType<TargetingSystemData>().Single();
            behavior.Resolution = new PerformanceStat { Min = newResolution, Max = newResolution, Terms = new List<StatTerm>() };
        }

        // Validate before staging, same contract as ShieldMigrate/TargetingCatalog above.
        foreach (var (design, _, _) in resolutionChanges) CultRecordRefs.Validate(design);

        db.Cache.Commit(batch =>
        {
            foreach (var (design, _, _) in resolutionChanges)
                batch.Upsert(typeof(GearData), design, db.Cache.RefOf(design).Key);

            foreach (var (design, productName, maker, description, roles) in newProducts)
            {
                var product = new FactionProductData
                {
                    Name = productName,
                    Description = description,
                    Design = new CultRecordRef<CraftedItemData>(db.Cache.RefOf(design).Key),
                    Manufacturer = db.Cache.RefOf(maker),
                    Roles = roles.Select(r => new ProductRole { Role = r.Role, Mean = r.Mean, StandardDeviation = r.Dev }).ToList(),
                };
                batch.Upsert(typeof(FactionProductData), product);
            }
        });

        Console.WriteLine($"\nLanded {resolutionChanges.Length} Resolution changes and {newProducts.Length} new products in Aetheria.cc");
        return 0;
    }

    // Fire control Cut 6d (docs/fire-control-cut.md, "where a hit lands is a dart throw, not a coin flip").
    //
    // Precision stops being a probability (the old coin flip's "chance the hit lands exactly on the aimed
    // item") and becomes a grouping tightness on FireControl.Sigma's own scale (sigma = 1/Precision, in hull-
    // schematic cell units -- the same units Shape.CenterOfMass and GearOccupancy already use). The two
    // designs' authored ranges (Cut 6a: 1-cell .05-.2, 2-cell .35-.55) were probabilities under the old rule
    // and are meaningless as written under the new one -- re-authored here on the sigma-reciprocal scale
    // instead, same convention as 6c.1's Resolution fix.
    //
    // Both ranges are chosen against real hull scale (GameData/Aetheria.cc's own hulls: Longinus 6x17,
    // Zenith 12x12, Turret 8x8) and the unaided floor this cut adds alongside them
    // (GameplaySettings.UnaidedPrecision, .3 -> sigma 3.33 cells): a design that did not clear the unaided
    // floor by a wide margin would make the required Tool item pointless to equip.
    //
    // 1-cell Targeting Computer: .5-.8 (sigma 2.0-1.25 cells) -- tight enough that aiming at a named subsystem
    // meaningfully concentrates fire on it and its immediate neighbours, well past the unaided floor.
    // 2-cell Fire Control Array: .9-1.3 (sigma 1.11-0.77 cells) -- strictly tighter than the 1-cell design at
    // every point along both authored ranges, the same "premium item unambiguously ahead" shape 6c.1 restored
    // for Resolution.
    //
    // Dry run unless passed "apply", same contract as the other *-migrate commands.
    private static int TargetingCatalog6d(bool apply)
    {
        var db = AetherDb.Open(catalogWritable: apply);

        GearData DesignByName(string name)
        {
            var design = db.Cache.GetAll<GearData>().FirstOrDefault(i => i.Name == name);
            if (design == null) throw new InvalidOperationException($"No design named \"{name}\".");
            return design;
        }

        var targetingComputer = DesignByName("Targeting Computer");
        var fireControlArray = DesignByName("Fire Control Array");

        var precisionChanges = new (GearData Design, (float Min, float Max) Old, (float Min, float Max) New)[]
        {
            (targetingComputer,
                (targetingComputer.Behaviors.OfType<TargetingSystemData>().Single().Precision.Min,
                 targetingComputer.Behaviors.OfType<TargetingSystemData>().Single().Precision.Max),
                (.5f, .8f)),
            (fireControlArray,
                (fireControlArray.Behaviors.OfType<TargetingSystemData>().Single().Precision.Min,
                 fireControlArray.Behaviors.OfType<TargetingSystemData>().Single().Precision.Max),
                (.9f, 1.3f)),
        };

        Console.WriteLine("Precision re-authored from a coin-flip probability to a grouping-tightness scale (Cut 6d):");
        foreach (var (design, old, @new) in precisionChanges)
            Console.WriteLine($"  {design.Name,-20} {old.Min,4:0.##}-{old.Max,-4:0.##} -> {@new.Min,4:0.##}-{@new.Max,-4:0.##}");

        if (!apply)
        {
            Console.WriteLine($"\nDry run. Pass \"apply\" to land {precisionChanges.Length} Precision changes.");
            return 0;
        }

        foreach (var (design, _, @new) in precisionChanges)
        {
            var behavior = design.Behaviors.OfType<TargetingSystemData>().Single();
            var terms = behavior.Precision.Terms;
            behavior.Precision = new PerformanceStat { Min = @new.Min, Max = @new.Max, Terms = terms };
        }

        // Validate before staging, same contract as ShieldMigrate/TargetingCatalog/TargetingCatalog6c above.
        foreach (var (design, _, _) in precisionChanges) CultRecordRefs.Validate(design);

        db.Cache.Commit(batch =>
        {
            foreach (var (design, _, _) in precisionChanges)
                batch.Upsert(typeof(GearData), design, db.Cache.RefOf(design).Key);
        });

        Console.WriteLine($"\nLanded {precisionChanges.Length} Precision changes in Aetheria.cc");
        return 0;
    }

    // Mining Cut 3 (docs/mining-cut-refresh.md): the first field kind. A chunk is detected by reflected light, and
    // its reflectivity comes from its belt's kind, so a catalog with no kind leaves every belt dark. This authors
    // the one kind Cut 3 needs, "Asteroid"; Cut 7 authors the rest. CrossSection is reflectivity per schematic
    // cell of chunk area: 5 sits inside the shipped hulls' 2-15 per cell (Longinus, Djinni), so a mid-size rock
    // (3-6 units across, 7-28 cells) reflects like a small ship. Both numbers are for the operator to review.
    private static int FieldKindsCatalog(bool apply)
    {
        var db = AetherDb.Open(catalogWritable: apply);
        var kinds = db.Cache.GetAll<FieldKindData>().ToArray();
        foreach (var kind in kinds)
            Console.WriteLine($"  {kind.Name,-12} cross-section {kind.CrossSection,5:0.##} per cell, weight {kind.GenerationWeight:0.##}");

        if (kinds.Any(kind => kind.Name == "Asteroid"))
        {
            Console.WriteLine("\"Asteroid\" is already authored; nothing to do.");
            return 0;
        }

        var asteroid = new FieldKindData { Name = "Asteroid", CrossSection = 5f, GenerationWeight = 1f };
        Console.WriteLine($"Adds \"Asteroid\": cross-section {asteroid.CrossSection} per cell, weight {asteroid.GenerationWeight}.");
        if (!apply)
        {
            Console.WriteLine("\nDry run. Pass \"apply\" to author it.");
            return 0;
        }

        CultRecordRefs.Validate(asteroid);
        db.Cache.Commit(batch => batch.Upsert(typeof(FieldKindData), asteroid));
        Console.WriteLine("\nAuthored \"Asteroid\" in Aetheria.cc");
        return 0;
    }

    // Aetheria release, cut pd-gear (ruling catalog-grows-generic-designs-branded-products): the catalog's first
    // point-defence class guns. Nothing in data marks a gun as PD (ruling pd-who-engages); these are PD by their
    // stats: a hose of light rounds, a quick light beam, and a small pellet volley. Each design is a deep copy of a
    // template weapon (so roles, effect, sound and temperatures are inherited) with only the stats below overridden;
    // each carries several products, and branding lives in the products alone. Dry run unless passed "apply".
    private static int PdGearCatalog(bool apply)
    {
        var db = AetherDb.Open(catalogWritable: apply);
        var weapons = db.Cache.GetAll<WeaponItemData>().ToArray();

        var designNames = new[] { "Point Defense Gun", "Point Defense Laser", "Flak Gun" };
        var already = designNames.Count(name => weapons.Any(w => w.Name == name));
        if (already == designNames.Length)
        {
            Console.WriteLine("Point-defence gear is already authored; nothing to do.");
            return 0;
        }
        if (already != 0) throw new InvalidOperationException("Point-defence gear is partly authored; the catalog needs a human.");

        WeaponItemData Template(string name) =>
            weapons.FirstOrDefault(w => w.Name == name) ?? throw new InvalidOperationException($"No weapon design named \"{name}\".");
        Faction Maker(string shortName) =>
            db.Cache.GetAll<Faction>().FirstOrDefault(f => f.ShortName == shortName) ?? throw new InvalidOperationException($"No faction with short name \"{shortName}\".");

        var ammoRef = Template("Autocannon").Behaviors.OfType<InstantWeaponData>().Single().AmmoType;
        var ammunition = db.Cache.Get<SimpleCommodityData>(ammoRef.Key) ?? throw new InvalidOperationException("Autocannon's ammunition commodity is not in the catalog.");

        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(WeaponItemData).Assembly);
        WeaponItemData Copy(WeaponItemData template, string name, string description, float mass, int price, float durability, int width, int height, WeaponModifiers modifiers)
        {
            var design = MessagePackSerializer.Deserialize<WeaponItemData>(MessagePackSerializer.Serialize(template, options), options);
            design.Name = name;
            design.Description = description;
            design.Mass = mass;
            design.Price = price;
            design.Durability = durability;
            design.Shape = new Shape(width, height);
            for (var x = 0; x < width; x++)
                for (var y = 0; y < height; y++) design.Shape.Cells[x, y] = true;
            design.WeaponModifiers = modifiers;
            return design;
        }

        // The stat keeps the template's terms (so quality still moves it as it moves the template); only Min and Max are authored.
        static void Set(PerformanceStat stat, float min, float max) { stat.Min = min; stat.Max = max; }
        static void Track(WeaponData data, float rate) => data.Tracking = new PerformanceStat { Min = rate, Max = rate, Terms = new List<StatTerm>() };

        var gun = Copy(Template("Light Machine Gun"), designNames[0], "Close-in defence gun: a hose of light rounds.", 40f, 80000, 60f, 1, 1, WeaponModifiers.RapidFire);
        var gunData = gun.Behaviors.OfType<InstantWeaponData>().Single();
        Set(gunData.Damage, 1.5f, 4f); Set(gunData.Range, 300f, 550f); Set(gunData.MinRange, 0f, 0f);
        Set(gunData.Cooldown, .06f, .025f); Set(gunData.Count, 1f, 1f); Set(gunData.Spread, 1f, .3f);
        Set(gunData.Velocity, 1100f, 1600f); Set(gunData.Energy, .4f, .2f); Set(gunData.Heat, 40f, 15f); Set(gunData.Visibility, 400f, 150f);
        gunData.AmmoType = db.Cache.RefOf(ammunition); gunData.MagazineSize = 250; gunData.ReloadTime = 3f;
        Track(gunData, 40f);

        var laser = Copy(Template("Light Laser"), designNames[1], "Close-in defence beam: quick, light, instant.", 30f, 110000, 30f, 1, 1, Template("Light Laser").WeaponModifiers);
        var laserData = laser.Behaviors.OfType<InstantWeaponData>().Single();
        Set(laserData.Damage, 5f, 12f); Set(laserData.Range, 400f, 750f); Set(laserData.MinRange, 0f, 0f);
        Set(laserData.Cooldown, .2f, .1f); Set(laserData.Count, 1f, 1f); Set(laserData.Spread, .05f, 0f);
        Set(laserData.Velocity, 0f, 0f); Set(laserData.Energy, 6f, 10f); Set(laserData.Heat, 300f, 150f); Set(laserData.Visibility, 300f, 100f);
        Track(laserData, 40f);

        var flak = Copy(Template("Cluster Cannon"), designNames[2], "Small flak gun: a short-range pellet volley.", 120f, 180000, 80f, 1, 2, WeaponModifiers.Cluster);
        var flakData = flak.Behaviors.OfType<InstantWeaponData>().Single();
        Set(flakData.Damage, 30f, 70f); Set(flakData.Count, 6f, 8f); Set(flakData.Range, 350f, 800f); Set(flakData.MinRange, 0f, 0f);
        Set(flakData.Cooldown, .6f, .35f); Set(flakData.Spread, 4f, 2f); Set(flakData.Velocity, 900f, 1300f);
        Set(flakData.Energy, 1f, .5f); Set(flakData.Heat, 300f, 150f); Set(flakData.Visibility, 800f, 300f);
        flakData.AmmoType = db.Cache.RefOf(ammunition); flakData.MagazineSize = 40; flakData.ReloadTime = 4f;
        Track(flakData, 20f);

        var products = new (WeaponItemData Design, string Name, string Maker, string Description)[]
        {
            (gun, "Waykeeper", "Lightsail", "Freight first, questions later. Keeps a hauler's paint unscratched through a full salvo."),
            (gun, "Rampart", "Zhestokost", "A column does not dodge. It does not need to."),
            (gun, "Swatter", "AU", "Keeps the rocks and the rockets off your back while you work."),
            (gun, "Catch-All", "R&D", "Guaranteed to stop the missile!* *Missile defined as whatever it stops."),
            (laser, "Glare", "Lucent", "Every missile gets its moment in the spotlight. A short one."),
            (laser, "Nightlight", "NiteLife", "Lights out for anything with a warhead. Shares the bus with your blasters, so mind the meter."),
            (laser, "Wren", "Finch", "Small, quiet and quicker than it looks, most days."),
            (laser, "Gnat", "Alakrita", "Weighs less than your conscience and keeps the fast pass fast."),
            (flak, "MFer", "Zhestokost", "Our shrapnel turns your enemies into scrapnel."),
            (flak, "Hailstorm", "AU", "A wall of steel for crews who would rather not count missiles."),
            (flak, "sky full of teeth", "DME", "light it up/ shake it loose/ nothing gets through tonight"),
        };
        foreach (var product in products) Maker(product.Maker);

        void Describe(WeaponItemData design)
        {
            var data = design.Behaviors.OfType<InstantWeaponData>().Single();
            Console.WriteLine($"  {design.Name,-20} {design.HardpointType,-9} {design.Shape.Width}x{design.Shape.Height}  mass {design.Mass:0.##}  price {design.Price}  durability {design.Durability:0.##}  modifiers {design.WeaponModifiers}");
            Console.WriteLine($"      damage {data.Damage.Min:0.##}-{data.Damage.Max:0.##}  count {data.Count.Min:0.##}-{data.Count.Max:0.##}  range {data.Range.Min:0.##}-{data.Range.Max:0.##}  cooldown {data.Cooldown.Min:0.###}-{data.Cooldown.Max:0.###}  spread {data.Spread.Min:0.##}-{data.Spread.Max:0.##}  velocity {data.Velocity.Min:0.##}-{data.Velocity.Max:0.##}");
            Console.WriteLine($"      energy {data.Energy.Min:0.##}-{data.Energy.Max:0.##}  heat {data.Heat.Min:0.##}-{data.Heat.Max:0.##}  visibility {data.Visibility.Min:0.##}-{data.Visibility.Max:0.##}  ammo {(data.AmmoType.IsSet() ? ammunition.Name : "none")}  magazine {data.MagazineSize}  reload {data.ReloadTime:0.##}  tracking {data.Tracking.Min:0.##}  roles [{string.Join(", ", design.Roles.Select(r => r.Name))}]");
            Console.WriteLine($"      \"{design.Description}\"");
        }

        Console.WriteLine("New designs:");
        foreach (var design in new[] { gun, laser, flak }) Describe(design);
        Console.WriteLine("\nNew products:");
        foreach (var (design, name, maker, description) in products)
        {
            var (mean, dev) = MakerProfiles[maker];
            Console.WriteLine($"  {design.Name} -> \"{name}\" by {maker}, every role mean {mean:0.##} dev {dev:0.##}: {description}");
        }

        if (!apply)
        {
            Console.WriteLine($"\nDry run. Pass \"apply\" to author 3 designs and {products.Length} products.");
            return 0;
        }

        foreach (var design in new[] { gun, laser, flak }) CultRecordRefs.Validate(design);
        db.Cache.Commit(batch =>
        {
            var keys = new Dictionary<WeaponItemData, CultRecordKey>();
            foreach (var design in new[] { gun, laser, flak }) keys[design] = batch.Upsert(typeof(WeaponItemData), design);
            foreach (var (design, name, maker, description) in products)
            {
                var (mean, dev) = MakerProfiles[maker];
                batch.Upsert(typeof(FactionProductData), new FactionProductData
                {
                    Name = name,
                    Description = description,
                    Design = new CultRecordRef<CraftedItemData>(keys[design]),
                    Manufacturer = db.Cache.RefOf(Maker(maker)),
                    Roles = design.Roles.Select(r => new ProductRole { Role = r.Name, Mean = mean, StandardDeviation = dev }).ToList(),
                });
            }
        });
        Console.WriteLine($"\nAuthored 3 designs and {products.Length} products in Aetheria.cc");
        return 0;
    }

    private const string EmissionDiscipline = "emission-discipline";

    // The intercept factor alpha is how far others hear a sensor against how far it sees: a quality-ranged stat from
    // .5 (loud) to 2 (quiet) whose midpoint quality .5 reads 1, so lerp(Min, Max, q^e) with e = log2(3). Brand flavour
    // lives in the product's emission-discipline quality (ruling sensor-stat-set): Lucent is low, Finch and Sol high,
    // every other maker middling. A maker with no profile of its own (Miss Terri's, the Pirates) keeps the deviation its
    // product already authored for the emitter role.
    private static float DisciplineMean(Faction maker) =>
        maker.ShortName == "Lucent" ? .2f : maker.ShortName == "Finch" || maker.ShortName == "Sol" || maker.Name == "Sol" ? .8f : .5f;

    private static float MakerDeviation(Faction maker, FactionProductData product) =>
        MakerProfiles.TryGetValue(maker.ShortName ?? "", out var profile) ? profile.Dev
        : product.Roles.FirstOrDefault(r => r.Role == "emitter")?.StandardDeviation ?? throw new InvalidOperationException($"Product \"{product.Name}\" has no emitter spread to take a deviation from.");

    // aetheria-release cut sensor-catalog (rulings sensor-stat-set, catalog-grows-generic-designs-branded-products): every
    // SensorData design's InterceptFactor becomes a quality-ranged stat reading the emission-discipline role, every
    // product of such a design authors that role, and the Passive Array (a generic listener gear, ListenerData) is
    // authored with two products from the stealth makers. Prints the sensor census first. Dry run unless passed
    // "apply"; idempotent, so a second apply writes nothing and a catalog another cut changed is rerun, never merged.
    private static int SensorStatSet(bool apply)
    {
        var db = AetherDb.Open(catalogWritable: apply);
        var designs = db.Cache.GetAll<EquippableItemData>().ToArray();
        var products = db.Cache.GetAll<FactionProductData>().ToArray();
        Faction MakerOf(FactionProductData p) => db.Cache.Get(p.Manufacturer);
        Faction Maker(string shortName) =>
            db.Cache.GetAll<Faction>().FirstOrDefault(f => f.ShortName == shortName) ?? throw new InvalidOperationException($"No faction with short name \"{shortName}\".");

        var sensors = designs.Where(d => d.Behaviors != null && d.Behaviors.OfType<SensorData>().Any()).OrderBy(d => d.Name, StringComparer.Ordinal).ToArray();
        var listeners = designs.Where(d => d.Behaviors != null && d.Behaviors.OfType<ListenerData>().Any()).OrderBy(d => d.Name, StringComparer.Ordinal).ToArray();
        Console.WriteLine($"Census: {sensors.Length} sensor designs, {listeners.Length} listener designs.");
        foreach (var design in sensors.Concat(listeners))
        {
            var key = db.Cache.RefOf(design).Key;
            Console.WriteLine($"  {design.Name} [{design.HardpointType}] roles [{string.Join(", ", (design.Roles ?? new List<ItemRole>()).Select(r => r.Name))}]");
            foreach (var product in products.Where(p => p.Design.Key.Equals(key)).OrderBy(p => p.Name, StringComparer.Ordinal))
                Console.WriteLine($"      {product.Name} by {MakerOf(product)?.ShortName ?? "(none)"}: " + string.Join(", ", (product.Roles ?? new List<ProductRole>()).Select(r => $"{r.Role} {r.Mean:0.00}±{r.StandardDeviation:0.00}")));
        }
        foreach (var consumable in db.Cache.GetAll<ConsumableItemData>().Where(c => c.Behaviors != null && c.Behaviors.OfType<SensorData>().Any()).OrderBy(c => c.Name, StringComparer.Ordinal))
            Console.WriteLine($"  consumable {consumable.Name} carries SensorData; consumables are not catalog sensor designs and keep their alpha");

        var changedDesigns = new List<(object Document, CultRecordKey Key)>();
        var changedProducts = new List<(object Document, CultRecordKey Key)>();
        var exponent = MathF.Log2(3f);
        foreach (var design in sensors)
        {
            var key = db.Cache.RefOf(design).Key;
            var designChanged = false;
            foreach (var sensor in design.Behaviors.OfType<SensorData>())
            {
                var stat = sensor.InterceptFactor;
                var current = stat != null && stat.Min == .5f && stat.Max == 2f && stat.Terms != null && stat.Terms.Count == 1
                    && stat.Terms[0].Source == StatSource.Quality && stat.Terms[0].Role == EmissionDiscipline && stat.Terms[0].Exponent == exponent;
                if (current) continue;
                sensor.InterceptFactor = new PerformanceStat { Min = .5f, Max = 2f, Terms = new List<StatTerm> { new StatTerm { Source = StatSource.Quality, Exponent = exponent, Role = EmissionDiscipline } } };
                designChanged = true;
            }
            design.Roles ??= new List<ItemRole>();
            if (!design.Roles.Any(r => r.Name == EmissionDiscipline)) { design.Roles.Add(new ItemRole { Name = EmissionDiscipline }); designChanged = true; }
            if (designChanged) changedDesigns.Add((design, key));
            foreach (var product in products.Where(p => p.Design.Key.Equals(key)))
            {
                var maker = MakerOf(product) ?? throw new InvalidOperationException($"Product \"{product.Name}\" has no maker.");
                product.Roles ??= new List<ProductRole>();
                var mean = DisciplineMean(maker);
                var dev = MakerDeviation(maker, product);
                var existing = product.Roles.FirstOrDefault(r => r.Role == EmissionDiscipline);
                if (existing != null && existing.Mean == mean && existing.StandardDeviation == dev) continue;
                if (existing != null) { existing.Mean = mean; existing.StandardDeviation = dev; }
                else product.Roles.Add(new ProductRole { Role = EmissionDiscipline, Mean = mean, StandardDeviation = dev });
                changedProducts.Add((product, db.Cache.RefOf(product).Key));
            }
        }

        // The catalog sells two sensor designs, one branded by DME and one by Miss Terri's, and no Lucent or Finch sensor at all,
        // so the brand flavour has nothing to vary across: each design gains a loud Lucent and a quiet Finch product
        // (ruling catalog-grows-generic-designs-branded-products). Every role of the design is authored at the maker's profile,
        // emission-discipline at the maker's flavour.
        var sensorProducts = new (string Design, string Name, string Maker, string Description)[]
        {
            ("Sensor Array", "Floodlight", "Lucent", "Sees everything and tells everything. Lucent never saw the point of subtle."),
            ("Sensor Array", "Pinion", "Finch", "A listening ear with a whisper of a voice. Finch sensors are heard last and heard least."),
            ("Compact Sensor", "Beacon", "Lucent", "If it can see you, you can see it. That is called fairness."),
            ("Compact Sensor", "Kestrel", "Finch", "Hangs in the dark and looks down. The prey rarely notices."),
        };
        var newProducts = new List<(EquippableItemData Design, string Name, Faction Maker, string Description)>();
        foreach (var (designName, name, makerName, description) in sensorProducts)
        {
            if (products.Any(p => p.Name == name)) continue;
            var design = sensors.Single(s => s.Name == designName);
            newProducts.Add((design, name, Maker(makerName), description));
        }

        // The Passive Array takes its thermal envelope and sound from a sensor gear, then becomes a Tool listener.
        GearData array = null;
        var arrayProducts = new List<(string Name, string Maker, string Description)>();
        if (listeners.Length == 0)
        {
            var template = sensors.OfType<GearData>().FirstOrDefault() ?? throw new InvalidOperationException("No sensor gear to take the Passive Array's thermal envelope from.");
            var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(GearData).Assembly);
            array = MessagePackSerializer.Deserialize<GearData>(MessagePackSerializer.Serialize(template, options), options);
            array.Name = "Passive Array";
            array.Description = "Passive listening gear. Hears every emitter at a multiple of its own reach, giving a bearing only and no track. Emits nothing.";
            array.Hardpoint = HardpointType.Tool;
            array.Mass = 20f; array.Price = 60000; array.Durability = 30f;
            array.Shape = new Shape(1, 1);
            array.Shape.Cells[0, 0] = true;
            array.Behaviors = new List<BehaviorData>
            {
                new EnergyDrawData { EnergyDraw = new PerformanceStat { Min = 10f, Max = 10f, Terms = new List<StatTerm>() }, PerSecond = true },
                new ListenerData { Multiple = new PerformanceStat { Min = 2f, Max = 3f, Terms = new List<StatTerm> { new StatTerm { Source = StatSource.Quality, Exponent = 1f, Role = "receiver" } } } },
            };
            array.Roles = new List<ItemRole> { new ItemRole { Name = "receiver" } };
            arrayProducts.Add(("Murmur", "Finch", "Quiet as its name, and about as easy to notice. Hears a shout from the next sector over."));
            arrayProducts.Add(("Wallflower", "NiteLife", "Stands in the corner of your hull and takes notes on everyone's business."));
            foreach (var (name, maker, description) in arrayProducts) Maker(maker);
            Console.WriteLine($"\nNew design: Passive Array [Tool] 1x1, EnergyDraw 10/s, Multiple 2-3 reading receiver; products {string.Join(", ", arrayProducts.Select(p => $"{p.Name} by {p.Maker}"))}");
        }

        Console.WriteLine($"\nIntercept factor: {changedDesigns.Count} designs and {changedProducts.Count} products to change, {newProducts.Count} sensor products to add; Passive Array {(array == null ? "already authored" : "to author")}.");
        if (changedDesigns.Count == 0 && changedProducts.Count == 0 && newProducts.Count == 0 && array == null)
        {
            Console.WriteLine("Sensor stats are already authored; nothing to do.");
            return 0;
        }
        if (!apply)
        {
            Console.WriteLine("Dry run. Pass \"apply\" to write them.");
            return 0;
        }
        if (array != null) CultRecordRefs.Validate(array);
        db.Cache.Commit(batch =>
        {
            foreach (var (document, key) in changedDesigns) batch.Upsert(document.GetType(), document, key);
            foreach (var (document, key) in changedProducts) batch.Upsert(document.GetType(), document, key);
            foreach (var (design, name, maker, description) in newProducts)
            {
                var (profileMean, profileDev) = MakerProfiles[maker.ShortName];
                batch.Upsert(typeof(FactionProductData), new FactionProductData
                {
                    Name = name, Description = description,
                    Design = new CultRecordRef<CraftedItemData>(db.Cache.RefOf(design).Key),
                    Manufacturer = db.Cache.RefOf(maker),
                    Roles = design.Roles.Select(r => r.Name == EmissionDiscipline
                        ? new ProductRole { Role = r.Name, Mean = DisciplineMean(maker), StandardDeviation = profileDev }
                        : new ProductRole { Role = r.Name, Mean = profileMean, StandardDeviation = profileDev }).ToList(),
                });
            }
            if (array == null) return;
            var arrayKey = batch.Upsert(typeof(GearData), array);
            foreach (var (name, maker, description) in arrayProducts)
            {
                var (mean, dev) = MakerProfiles[maker];
                batch.Upsert(typeof(FactionProductData), new FactionProductData
                {
                    Name = name, Description = description,
                    Design = new CultRecordRef<CraftedItemData>(arrayKey),
                    Manufacturer = db.Cache.RefOf(Maker(maker)),
                    Roles = array.Roles.Select(r => new ProductRole { Role = r.Name, Mean = mean, StandardDeviation = dev }).ToList(),
                });
            }
        });
        Console.WriteLine($"Landed {changedDesigns.Count} designs, {changedProducts.Count} products changed and {newProducts.Count} added{(array == null ? "" : ", the Passive Array and its products")} in Aetheria.cc");
        return 0;
    }

    // aetheria-release cut scenarios-smoke (rulings weapon-feel-to-smoke-cut-test-hull, test-hull-both-flanks): the
    // Smoke Test Hull, a copy of the Longinus hull that no product sells, so no generated ship, station or shop offers
    // it and only a scenario fits it. It adds what no shipped ship hull has, where the Longinus runs out of cells: two
    // rows of cells past the bow carrying a 2x2 Energy hardpoint (ChargeBlast SG) and a 2x2 Ballistic hardpoint
    // (Autocannon), and a 2x2 side mount on each flank, a Clockwise Ballistic one on the starboard and a
    // CounterClockwise Energy one on the port, which need two columns of margin on each side: every Longinus cell and
    // hardpoint moves right by two. Their gear is unchanged. Its prefab is the
    // Longinus's, which has no barrel for the new hardpoints: they fire from the hull centre. Dry run unless passed
    // "apply".
    public static int SmokeHullCatalog(bool apply, string root = null)
    {
        const string name = "Smoke Test Hull";
        var db = AetherDb.Open(catalogWritable: apply, root: root);
        if (db.Cache.GetAll<HullData>().Any(h => h.Name == name))
        {
            Console.WriteLine($"\"{name}\" is already authored; nothing to do.");
            return 0;
        }

        var longinus = db.Cache.GetAll<HullData>().Single(h => h.Name == "Longinus");
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(HullData).Assembly);
        var hull = MessagePackSerializer.Deserialize<HullData>(MessagePackSerializer.Serialize(longinus, options), options);
        hull.Name = name;
        hull.Description = "The Longinus with two forward 2x2 hardpoints and a side mount on each flank, for scenarios. No faction builds it.";

        // Two cells of margin on each side (every Longinus cell and hardpoint moves right by Margin), two rows of cells past
        // the bow over the width of the tail's neighbours, and a 2x2 block of cells on each flank at the wide section.
        const int Margin = 2;
        var width = longinus.Shape.Width + 2 * Margin;
        var bow = longinus.Shape.Height;
        var grown = new Shape(width, bow + 2);
        for (var x = 0; x < longinus.Shape.Width; x++)
            for (var y = 0; y < bow; y++) grown.Cells[x + Margin, y] = longinus.Shape.Cells[x, y];
        for (var x = Margin + 1; x <= Margin + 4; x++)
            for (var y = bow; y < grown.Height; y++) grown.Cells[x, y] = true;
        foreach (var x in new[] { 0, 1, width - 2, width - 1 })
            for (var y = 4; y <= 5; y++) grown.Cells[x, y] = true;
        hull.Shape = grown;
        foreach (var existing in hull.Hardpoints) existing.Position = CultMath.math.int2(existing.Position.x + Margin, existing.Position.y);

        static Shape Cells(int side)
        {
            var shape = new Shape(side, side);
            for (var x = 0; x < side; x++)
                for (var y = 0; y < side; y++) shape.Cells[x, y] = true;
            return shape;
        }
        hull.Hardpoints.Add(new HardpointData { Type = HardpointType.Energy, Position = CultMath.math.int2(Margin + 1, bow), Shape = Cells(2), Rotation = ItemRotation.None, Armor = 20f });
        hull.Hardpoints.Add(new HardpointData { Type = HardpointType.Ballistic, Position = CultMath.math.int2(Margin + 3, bow), Shape = Cells(2), Rotation = ItemRotation.None, Armor = 20f });
        hull.Hardpoints.Add(new HardpointData { Type = HardpointType.Ballistic, Position = CultMath.math.int2(width - 2, 4), Shape = Cells(2), Rotation = ItemRotation.Clockwise, Armor = 20f });
        hull.Hardpoints.Add(new HardpointData { Type = HardpointType.Energy, Position = CultMath.math.int2(0, 4), Shape = Cells(2), Rotation = ItemRotation.CounterClockwise, Armor = 20f });

        Console.WriteLine($"Adds \"{name}\": a Longinus copy grown from {longinus.Shape.Width}x{longinus.Shape.Height} to {grown.Width}x{grown.Height}, no product.");
        foreach (var hardpoint in hull.Hardpoints.Skip(longinus.Hardpoints.Count))
            Console.WriteLine($"  {hardpoint.Type,-9} {hardpoint.Shape.Width}x{hardpoint.Shape.Height} at {hardpoint.Position.x},{hardpoint.Position.y} rotation {hardpoint.Rotation}");
        if (!apply)
        {
            Console.WriteLine("\nDry run. Pass \"apply\" to author it.");
            return 0;
        }

        CultRecordRefs.Validate(hull);
        db.Cache.Commit(batch => batch.Upsert(typeof(HullData), hull));
        Console.WriteLine($"\nAuthored \"{name}\" in Aetheria.cc");
        return 0;
    }

    // aetheria-release cut scenarios-smoke-rounds (docs/scenarios-cut.md 5.3): the five smoke weapons the Fused Rounds, Refused
    // Rounds and AI Fused Discipline arenas fit. None has a product, so no generated ship, station or shop offers one and only a
    // scenario fits them. Smoke Proximity, Smoke Delayed and Smoke Refused are Spectra clones (energy, 1x2) with the operator's
    // fuse values; Smoke Refused Beam is a constant weapon and Smoke Refused Charge a charged one cloned from ChargeBlast SG,
    // each fused with a Range below its BlastRadius, so every refused weapon kind has a design. The rounds that fly are slow
    // (Velocity 150) so a scenario can kill the target while one is in the air. Dry run unless passed "apply".
    public static int SmokeRoundsCatalog(bool apply, string root = null)
    {
        var db = AetherDb.Open(catalogWritable: apply, root: root);
        var weapons = db.Cache.GetAll<WeaponItemData>().ToArray();
        var names = new[] { "Smoke Proximity", "Smoke Delayed", "Smoke Refused", "Smoke Refused Beam", "Smoke Refused Charge" };
        var already = names.Count(name => weapons.Any(w => w.Name == name));
        if (already == names.Length)
        {
            Console.WriteLine("The smoke rounds are already authored; nothing to do.");
            return 0;
        }
        if (already != 0) throw new InvalidOperationException("The smoke rounds are partly authored; the catalog needs a human.");

        WeaponItemData Template(string name) =>
            weapons.FirstOrDefault(w => w.Name == name) ?? throw new InvalidOperationException($"No weapon design named \"{name}\".");
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(WeaponItemData).Assembly);
        WeaponItemData Copy(string template, string name, string description, WeaponFuse fuse, float blast)
        {
            var source = Template(template);
            var design = MessagePackSerializer.Deserialize<WeaponItemData>(MessagePackSerializer.Serialize(source, options), options);
            design.Name = name;
            design.Description = description;
            design.Fuse = fuse;
            design.BlastRadius = blast;
            return design;
        }
        static PerformanceStat Fixed(float value) => new PerformanceStat { Min = value, Max = value, Terms = new List<StatTerm>() };
        // A refused weapon keeps its template's ratio of effective range to range, so range-falloff leaves it as authored.
        static void Shorten(WeaponData weapon)
        {
            weapon.EffectiveRange = Fixed(20f * weapon.EffectiveRange.Max / weapon.Range.Max);
            weapon.Range = Fixed(20f);
        }

        var proximity = Copy("Light Laser", names[0], "A Light Laser that bursts by proximity, blast 8. For scenarios; no one builds it.", WeaponFuse.Proximity, 8f);
        var delayed = Copy("Light Laser", names[1], "A Light Laser that bursts after two cells of metal, blast 4. For scenarios; no one builds it.", WeaponFuse.Delayed, 4f);
        var refused = Copy("Light Laser", names[2], "A Light Laser whose blast is wider than its reach, so it never fires. For scenarios; no one builds it.", WeaponFuse.Proximity, 30f);
        var charge = Copy("Charged Scatter Blaster", names[4], "A Charged Scatter Blaster whose blast is wider than its reach, so it never charges. For scenarios; no one builds it.", WeaponFuse.Proximity, 30f);
        var beam = Copy("Light Laser", names[3], "A constant beam whose blast is wider than its reach, so it never starts. For scenarios; no one builds it.", WeaponFuse.Proximity, 30f);

        // The Smoke Test Hull carries one 2x2 reactor. A Spectra's 20-40 energy a shot at its cadence is more than that reactor makes,
        // so two of them overload it, it overheats and shuts down within a second, and nothing aboard fires again (Fused Rounds,
        // stepped, before this). The two rounds a scenario must fly draw like an Autocannon instead.
        foreach (var design in new[] { proximity, delayed })
        {
            var data = design.Behaviors.OfType<InstantWeaponData>().Single();
            data.Velocity = Fixed(150f);
            data.Energy = Fixed(4f);
        }
        delayed.Behaviors.OfType<InstantWeaponData>().Single().Penetration = Fixed(2f);
        Shorten(refused.Behaviors.OfType<InstantWeaponData>().Single());
        Shorten(charge.Behaviors.OfType<ChargedWeaponData>().Single());

        // The beam keeps the Spectra's gear (shape, hardpoint, thermal envelope, roles) and swaps its behaviour for a constant one.
        var instant = beam.Behaviors.OfType<InstantWeaponData>().Single();
        var constant = new ConstantWeaponData
        {
            DamageType = instant.DamageType, Damage = instant.Damage, Penetration = instant.Penetration, DamageSpread = instant.DamageSpread,
            MinRange = instant.MinRange, Range = Fixed(20f), EffectiveRange = Fixed(20f * instant.EffectiveRange.Max / instant.Range.Max), DamageAtMaxRange = instant.DamageAtMaxRange, EffectPrefab = instant.EffectPrefab,
            Energy = instant.Energy, Heat = instant.Heat, Visibility = instant.Visibility, AmmoType = instant.AmmoType,
            MagazineSize = instant.MagazineSize, ReloadTime = instant.ReloadTime, Spread = instant.Spread, Velocity = instant.Velocity,
            Tracking = instant.Tracking
        };
        beam.Behaviors.Remove(instant);
        beam.Behaviors.Add(constant);

        var designs = new[] { proximity, delayed, refused, beam, charge };
        Console.WriteLine("Adds 5 weapon designs, no products:");
        foreach (var design in designs)
        {
            var data = design.Behaviors.OfType<WeaponData>().Single();
            Console.WriteLine($"  {design.Name,-20} {design.Hardpoint,-8} {design.Shape.Width}x{design.Shape.Height} {data.GetType().Name,-18} fuse {design.Fuse} blast {design.BlastRadius} range {data.Range.Min}-{data.Range.Max} velocity {data.Velocity.Min}");
        }
        if (!apply)
        {
            Console.WriteLine("\nDry run. Pass \"apply\" to author them.");
            return 0;
        }

        foreach (var design in designs) CultRecordRefs.Validate(design);
        db.Cache.Commit(batch =>
        {
            foreach (var design in designs) batch.Upsert(typeof(WeaponItemData), design);
        });
        Console.WriteLine($"\nAuthored {designs.Length} designs in Aetheria.cc");
        return 0;
    }

    // sim-mines-presenter: the Mine Launcher item, so a ship can be fitted with the sim's mine layer. Its numbers
    // are the 2021 record's (GameData/Legacy/AetherDB.2021-03-05.msgpack, recovered in 8c71a637: mass 75, price
    // 125000, damage 50, range 2000, energy 15, heat 500, visibility 10, magazine 6, reload 5, spread 5, velocity
    // 50, cooldown 2, thermal limits 173.15..473.15 K, kelvin like every shipped item); BlastRadius 25, the delays
    // 2 and 2 and the 30 s lifetime are the retired prefab's. The optimum and plateau are the 2021 record's
    // sibling weapon's (it carries 173.15..473.15 K too), because that record sets neither. An already authored
    // Mine Launcher has its thermal envelope brought to these values. Dry run unless passed "apply".
    private static int MineLauncherCatalog(bool apply)
    {
        const string name = "Mine Launcher";
        var db = AetherDb.Open(catalogWritable: apply);
        const float minimumTemperature = 173.15f, maximumTemperature = 473.15f, optimalTemperature = 249.65f, plateauWidth = 36f;
        if (db.Cache.GetAll<GearData>().SingleOrDefault(gear => gear.Name == name) is { } authored)
        {
            if (authored is not WeaponItemData existing) throw new InvalidOperationException($"\"{name}\" exists and is not a weapon.");
            if (existing.MinimumTemperature == minimumTemperature && existing.MaximumTemperature == maximumTemperature &&
                existing.OptimalTemperature == optimalTemperature && existing.PlateauWidth == plateauWidth)
            {
                Console.WriteLine($"\"{name}\" is already authored; nothing to do.");
                return 0;
            }
            Console.WriteLine($"\"{name}\" thermal envelope {existing.MinimumTemperature}..{existing.MaximumTemperature} (optimum {existing.OptimalTemperature}, plateau {existing.PlateauWidth}) -> {minimumTemperature}..{maximumTemperature} K (optimum {optimalTemperature}, plateau {plateauWidth}).");
            if (!apply)
            {
                Console.WriteLine("\nDry run. Pass \"apply\" to correct it.");
                return 0;
            }
            existing.MinimumTemperature = minimumTemperature;
            existing.MaximumTemperature = maximumTemperature;
            existing.OptimalTemperature = optimalTemperature;
            existing.PlateauWidth = plateauWidth;
            CultRecordRefs.Validate(existing);
            db.Cache.Commit(batch => batch.Upsert(typeof(GearData), existing, db.Cache.RefOf(existing).Key));
            Console.WriteLine($"\nCorrected \"{name}\" in Aetheria.cc");
            return 0;
        }

        PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };
        var launcher = new WeaponItemData
        {
            Name = name,
            Hardpoint = HardpointType.Launcher,
            Shape = new Shape(),
            WeaponType = WeaponType.Mine,
            WeaponRange = WeaponRange.Medium,
            WeaponCaliber = WeaponCaliber.Medium,
            Fuse = WeaponFuse.Proximity,
            BlastRadius = 25f,
            Mass = 75f,
            Price = 125000,
            Durability = 100f,
            MinimumTemperature = minimumTemperature,
            MaximumTemperature = maximumTemperature,
            OptimalTemperature = optimalTemperature,
            PlateauWidth = plateauWidth,
            Behaviors =
            {
                new MineLayerData
                {
                    DamageType = DamageType.Kinetic,
                    Damage = Constant(50f),
                    Range = Constant(2000f),
                    MinRange = Constant(0f),
                    Energy = Constant(15f),
                    Heat = Constant(500f),
                    Visibility = Constant(10f),
                    MagazineSize = 6,
                    ReloadTime = 5f,
                    Spread = Constant(5f),
                    Velocity = Constant(50f),
                    Count = Constant(1f),
                    Cooldown = Constant(2f),
                    ArmingDelay = 2f,
                    FuseDelay = 2f,
                    Lifetime = 30f
                }
            }
        };
        Console.WriteLine($"Adds \"{name}\": Launcher, Mine, Proximity fuse, blast radius 25, one MineLayerData (arming 2, fuse 2, lifetime 30).");
        if (!apply)
        {
            Console.WriteLine("\nDry run. Pass \"apply\" to author it.");
            return 0;
        }

        // A design no product sells cannot spawn, and the shipped catalog holds none: sell it as the first
        // shipped launcher is sold, by the same maker.
        var launcherProducts = db.Cache.GetAll<FactionProductData>()
            .Where(p => db.Cache.Get(p.Design) is WeaponItemData weapon && weapon.Hardpoint == HardpointType.Launcher)
            .OrderBy(p => db.Cache.RefOf(p).Key.Value, StringComparer.Ordinal).ToArray();
        if (launcherProducts.Length == 0) throw new InvalidOperationException("No shipped launcher product names a maker for the Mine Launcher.");
        var maker = launcherProducts[0].Manufacturer;
        Console.WriteLine($"Sold by {db.Cache.Get(maker)?.ShortName ?? "(none)"}, as the first shipped launcher product is.");

        CultRecordRefs.Validate(launcher);
        db.Cache.Commit(batch =>
        {
            var design = batch.Upsert(typeof(GearData), launcher);
            batch.Upsert(typeof(FactionProductData), new FactionProductData
            {
                Name = name,
                Description = "Lays a drifting proximity mine that arms after two seconds.",
                Design = new CultRecordRef<CraftedItemData>(design),
                Manufacturer = maker
            });
        });
        Console.WriteLine($"\nAuthored \"{name}\" in Aetheria.cc");
        return 0;
    }

    // Aetheria release, cut roles-backfill (rule CB-R1): designs that shipped with flat stats and no roles get the
    // roles their kind's map names, and every seller authors a ProductRole per role. Each mapped stat becomes a range
    // of value x (0.8, 1.2) that rises with quality for a benefit and falls for a cost, read through one Quality term
    // naming its role (exponent: the mode of the kind's existing designs on that field, 1 when there is none; an
    // existing Quality term keeps its exponent). Zero stats stay flat. A mapped stat that already varies keeps its range
    // and only has its unnamed Quality term pointed at the role. Small Drive joined the set because it declares roles that
    // no non-flat stat reads; DeathCluster, Flak Gun, GT 3K, plight and the two MoveOnPros because each had a non-flat stat
    // whose Quality term named no role (CB-R1: each non-flat stat reads exactly one declared role). Idempotent: a design with
    // no flat mapped stat and no unnamed one is left alone, a product with a role is left alone. Dry run unless passed "apply".
    private static readonly string[] BackfillDesigns = { "Autocannon", "Long-Range Missile Rack", "Short-Range Missile Rack", "Mine Launcher", "Large Drive", "Small Drive",
        "Cluster Cannon", "Flak Gun", "Guided Missile Launcher", "Lightning Projector", "Muon Fusion Reactor", "Muon Fusion Station Reactor" };

    // Stats the backfill names but never ranges: a shot count is whole, and which way a launcher's MinRange should fall is
    // each design's own call (GT 3K authors it as a cost already).
    private static readonly HashSet<string> RoleOnlyStats = new HashSet<string>(StringComparer.Ordinal) { "Count", "MinRange" };

    private static readonly HashSet<string> CostStats = new HashSet<string>(StringComparer.Ordinal)
    {
        "Cooldown", "Spread", "Heat", "Visibility", "Energy", "EnergyUsage", "ChargeTime", "ChargeEnergy", "ChargeHeat",
        "SensorImpact", "DirectionImpact", "Decay",
    };

    private static IEnumerable<PerformanceStat> StatsOf(EquippableItemData item) =>
        (item.Behaviors ?? new List<BehaviorData>()).Where(b => b != null)
            .SelectMany(b => b.GetType().GetFields().Where(f => f.FieldType == typeof(PerformanceStat)).Select(f => f.GetValue(b) as PerformanceStat))
            .Where(s => s?.Terms != null);

    public static int RolesBackfill(bool apply, string root = null)
    {
        var db = AetherDb.Open(catalogWritable: apply, root: root);
        var items = db.Cache.GetAll<EquippableItemData>().ToArray();
        var products = db.Cache.GetAll<FactionProductData>().ToArray();
        var targets = BackfillDesigns.Select(name => items.Where(i => i.Name == name).ToArray())
            .Select((found, i) => found.Length == 1 ? found[0] : throw new InvalidOperationException(
                $"Expected exactly one design named \"{BackfillDesigns[i]}\", found {found.Length}.")).ToArray();

        // The precedent is read once, before any target changes: the named Quality exponents of every design that declares
        // roles, so a design the command names later never leaves the pool another target draws from, and a design that
        // starts with no named term contributes nothing.
        var precedent = items.Where(i => i.Roles != null && i.Roles.Count > 0)
            .SelectMany(i => (i.Behaviors ?? new List<BehaviorData>()).Where(b => b != null)
                .SelectMany(b => b.GetType().GetFields().Where(f => f.FieldType == typeof(PerformanceStat)).Select(f => (Kind: KindOf(i), Field: f.Name, Stat: f.GetValue(b) as PerformanceStat))))
            .Where(x => x.Stat?.Terms != null && x.Stat.Min != x.Stat.Max)
            .SelectMany(x => x.Stat.Terms.Where(t => t.Source == StatSource.Quality && !string.IsNullOrEmpty(t.Role)).Select(t => (x.Kind, x.Field, t.Exponent)))
            .ToArray();

        float ExponentFor(string kind, string field)
        {
            var modes = precedent.Where(p => p.Kind == kind && p.Field == field).Select(p => p.Exponent)
                .GroupBy(e => e).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g => g.Key).ToArray();
            return modes.Length > 0 ? modes[0] : 1f;
        }

        var changedDesigns = new List<(EquippableItemData Document, CultRecordKey Key)>();
        foreach (var design in targets)
        {
            var kind = KindOf(design);
            var map = RoleMapFor(design, kind) ?? throw new InvalidOperationException($"\"{design.Name}\" has no role map.");
            var lines = new List<string>();
            foreach (var behavior in design.Behaviors.Where(b => b != null))
                foreach (var field in behavior.GetType().GetFields().Where(f => f.FieldType == typeof(PerformanceStat)))
                {
                    var stat = field.GetValue(behavior) as PerformanceStat;
                    if (stat == null) continue;
                    if (!map.TryGetValue(RoleKey(behavior, field.Name), out var role)) continue;
                    stat.Terms ??= new List<StatTerm>();
                    var quality = stat.Terms.FirstOrDefault(t => t.Source == StatSource.Quality);
                    var value = stat.Min;
                    if (stat.Min == stat.Max)
                    {
                        if (RoleOnlyStats.Contains(field.Name)) continue; // a count or a per-design bound: never ranged
                        if (stat.Min == 0f || float.IsInfinity(stat.Min)) continue;
                        (stat.Min, stat.Max) = CostStats.Contains(field.Name) ? (value * 1.2f, value * .8f) : (value * .8f, value * 1.2f);
                        quality ??= new StatTerm { Source = StatSource.Quality, Exponent = ExponentFor(kind, field.Name) };
                        if (!stat.Terms.Contains(quality)) stat.Terms.Add(quality);
                    }
                    else if (quality == null || !string.IsNullOrEmpty(quality.Role)) continue; // already named, or not quality-driven
                    quality.Role = role;
                    lines.Add($"      {behavior.GetType().Name}.{field.Name,-16} {value:0.####} -> {stat.Min:0.####}..{stat.Max:0.####}  {role} ^{quality.Exponent:0.##}");
                }
            if (lines.Count == 0) continue;
            design.Roles = StatsOf(design).Where(s => s.Min != s.Max)
                .SelectMany(s => s.Terms.Where(t => t.Source == StatSource.Quality && !string.IsNullOrEmpty(t.Role)).Select(t => t.Role))
                .Distinct().OrderBy(r => r, StringComparer.Ordinal).Select(r => new ItemRole { Name = r }).ToList();
            Console.WriteLine($"  {design.Name}  ({kind})  roles [{string.Join(", ", design.Roles.Select(r => r.Name))}]");
            foreach (var line in lines) Console.WriteLine(line);
            changedDesigns.Add((design, db.Cache.RefOf(design).Key));
        }

        var changedProducts = new List<(FactionProductData Document, CultRecordKey Key)>();
        foreach (var design in targets)
            foreach (var product in products.Where(p => p.Design.Key.Equals(db.Cache.RefOf(design).Key)).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                var maker = db.Cache.Get(product.Manufacturer);
                var profile = maker?.Name == "Pirates" ? PirateProfile
                    : maker != null && MakerProfiles.TryGetValue(maker.ShortName, out var found) ? found
                    : throw new InvalidOperationException($"No maker profile for the seller of \"{design.Name}\" ({maker?.ShortName ?? "no maker"}).");
                product.Roles ??= new List<ProductRole>();
                var missing = design.Roles.Select(r => r.Name).Where(r => product.Roles.All(p => p.Role != r)).ToArray();
                if (missing.Length == 0) continue;
                foreach (var role in missing)
                    product.Roles.Add(new ProductRole { Role = role, Mean = profile.Mean, StandardDeviation = profile.Dev });
                Console.WriteLine($"  {product.Name,-28} ({design.Name}, {maker.ShortName}) {string.Join(", ", missing)} at {profile.Mean:0.00}/{profile.Dev:0.00}");
                changedProducts.Add((product, db.Cache.RefOf(product).Key));
            }

        if (changedDesigns.Count == 0 && changedProducts.Count == 0)
        {
            Console.WriteLine("Roles backfill is already authored; nothing to do.");
            return 0;
        }
        Console.WriteLine($"\n{changedDesigns.Count} design(s) and {changedProducts.Count} product(s) to author.");
        if (!apply)
        {
            Console.WriteLine("Dry run. Pass \"apply\" to land them.");
            return 0;
        }
        foreach (var (document, _) in changedDesigns) CultRecordRefs.Validate(document);
        foreach (var (document, _) in changedProducts) CultRecordRefs.Validate(document);
        db.Cache.Commit(batch =>
        {
            foreach (var (document, key) in changedDesigns) batch.Upsert(document is WeaponItemData ? typeof(WeaponItemData) : typeof(GearData), document, key);
            foreach (var (document, key) in changedProducts) batch.Upsert(typeof(FactionProductData), document, key);
        });
        Console.WriteLine($"Landed {changedDesigns.Count} design(s) and {changedProducts.Count} product(s) in Aetheria.cc");
        return 0;
    }

    // Aetheria release, cut pirates-record (rulings pirates-record-content, catalog-grows-generic-designs-branded-products):
    // the Pirates faction and eight rebrands of other makers' designs, named in emoji. The Pirates own no design: their range
    // is what they took, so every product points at a design someone else also makes. Each product carries one ProductRole per
    // role its design declares, middling and inconsistent (mean .45, sd .22, against makers' .15). Names are stored as the emoji
    // only, written here as \U escapes so no editor or shell mangles them; the dry run prints them raw and as code points.
    //
    // Dry run unless passed "apply", same contract as the other *-migrate commands.
    public static int PiratesFaction(bool apply, string root = null)
    {
        var db = AetherDb.Open(catalogWritable: apply, root: root);

        var existing = db.Cache.GetAll<Faction>().FirstOrDefault(f => f.Name == "Pirates");
        if (existing != null)
        {
            Console.WriteLine("A faction named \"Pirates\" already exists; nothing to do.");
            return 0;
        }

        CraftedItemData DesignByName(string name)
        {
            var matches = db.Cache.GetAll<CraftedItemData>().Where(i => i.Name == name).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException($"Expected exactly one design named \"{name}\", found {matches.Length}.");
            return matches[0];
        }

        string CodePoints(string text) => string.Join(" ", text.EnumerateRunes().Select(r => $"U+{r.Value:X4}"));

        var nameFile = db.Cache.GetAll<NameFile>().FirstOrDefault(n => n.Name == "pleiades");
        if (nameFile == null) throw new InvalidOperationException("No name file named \"pleiades\".");

        var others = db.Cache.GetAll<Faction>().ToArray();
        var pirates = new Faction
        {
            Name = "Pirates",
            ShortName = "Pirates",
            Description = "A loose coalition of crews and hidden docks moving people and goods outside official control. No command, no common law: possession has a maintenance schedule.",
            PrimaryColor = new float3(.42f, .40f, .37f),
            SecondaryColor = new float3(1f, .45f, .05f),
            GeonameFile = db.Cache.RefOf(nameFile),
            InfluenceDistance = 3,
        };
        foreach (var other in others) pirates.Allegiance[db.Cache.RefOf(other)] = 1f;

        var rebrands = new (string Design, string Name, string Description)[]
        {
            ("Autocannon", "\U0001FAF3\U0001F52B", "Serial number filed off. Still shoots."),
            ("Long Slug Rifle", "\U0001FAA6\U0001F920", "The previous owner had no further use for it."),
            ("Cell Blaster", "\U0001F9FE\U0001F6AB", "Warranty void where prohibited, which is everywhere."),
            ("Slug Rifle", "\U0001F3A8\U0001F648", "The logo underneath is somebody else's problem."),
            ("Incendiary Rocket Pod", "\U0001F525", "Turns out we did start the fire after all."),
            ("Targeting Computer", "\U0001F440\U0001F3AF", "Somebody else's eyes. They still work."),
            ("Small Drive", "\U0001F511\U0001F3C3\U0001F4A8", "Hotwired. Do not ask about the ignition."),
            ("Cargo Bay", "\U0001F4E6\U0001F92B", "Contents not as declared."),
        };

        var products = rebrands.Select(r =>
        {
            var design = DesignByName(r.Design);
            return (Design: design, r.Name, r.Description,
                Roles: (design.Roles ?? new List<ItemRole>()).Select(role => new ProductRole { Role = role.Name, Mean = PirateProfile.Mean, StandardDeviation = PirateProfile.Dev }).ToList());
        }).ToArray();

        Console.WriteLine($"New faction \"{pirates.Name}\": influence {pirates.InfluenceDistance}, names \"{nameFile.Name}\", allegiance 1.0 to {pirates.Allegiance.Count} factions.");
        Console.WriteLine($"  colours: primary ({pirates.PrimaryColor.x:0.00}, {pirates.PrimaryColor.y:0.00}, {pirates.PrimaryColor.z:0.00}) primer grey, secondary ({pirates.SecondaryColor.x:0.00}, {pirates.SecondaryColor.y:0.00}, {pirates.SecondaryColor.z:0.00}) hazard orange");
        Console.WriteLine($"  {pirates.Description}");
        Console.WriteLine($"\nNew products ({products.Length}):");
        foreach (var (design, name, description, roles) in products)
        {
            var makers = db.Cache.GetAll<FactionProductData>()
                .Where(p => p.Design.Key.Equals(db.Cache.RefOf(design).Key))
                .Select(p => db.Cache.Get(p.Manufacturer)?.ShortName).OrderBy(n => n, StringComparer.Ordinal);
            Console.WriteLine($"  {name}  [{CodePoints(name)}]");
            Console.WriteLine($"    design {design.GetType().Name} \"{design.Name}\" (made by {string.Join(", ", makers)}), roles [{string.Join(", ", roles.Select(r => r.Role))}]");
            Console.WriteLine($"    \"{description}\"");
        }

        CultRecordRefs.Validate(pirates);
        foreach (var (design, name, description, roles) in products)
            CultRecordRefs.Validate(new FactionProductData
            {
                Name = name, Description = description, Roles = roles,
                Design = new CultRecordRef<CraftedItemData>(db.Cache.RefOf(design).Key),
                Manufacturer = db.Cache.RefOf(others[0]),
            });

        if (!apply)
        {
            Console.WriteLine($"\nDry run. Pass \"apply\" to land the Pirates faction and {products.Length} products.");
            return 0;
        }

        db.Cache.Commit(batch =>
        {
            var piratesKey = batch.Upsert(typeof(Faction), pirates);
            foreach (var (design, name, description, roles) in products)
                batch.Upsert(typeof(FactionProductData), new FactionProductData
                {
                    Name = name, Description = description, Roles = roles,
                    Design = new CultRecordRef<CraftedItemData>(db.Cache.RefOf(design).Key),
                    Manufacturer = new CultRecordRef<Faction>(piratesKey),
                });
        });

        Console.WriteLine($"\nLanded the Pirates faction and {products.Length} products in Aetheria.cc");
        return 0;
    }

    // aetheria-release cut consumables-first-set (rulings consumables-scarce, overdrive-keep-steering,
    // vapour-cloud-obscuration): the first two consumables, scarce and strong, each with the roles its effect stats
    // read and two products from distinct makers. Specific heat and conductivity come from the Targeting Computer,
    // the catalog's 1x1 tool; mass, price, shape and every effect are authored here. The vent's cloud encloses the
    // largest hull (the Djinni) at its minimum radius: half the hull's diagonal in zone units, times 1.5. Dry run
    // unless passed "apply".
    public static int ConsumablesFirstSetCatalog(bool apply, string root = null)
    {
        var db = AetherDb.Open(catalogWritable: apply, root: root);
        var designNames = new[] { "Thruster Overdrive", "Coolant Vent" };
        var already = designNames.Count(name => db.Cache.GetAll<ConsumableItemData>().Any(c => c.Name == name));
        if (already == designNames.Length)
        {
            Console.WriteLine("The first consumables are already authored; nothing to do.");
            return 0;
        }
        if (already != 0) throw new InvalidOperationException("The first consumables are partly authored; the catalog needs a human.");

        var template = db.Cache.GetAll<GearData>().Single(g => g.Name == "Targeting Computer");
        var djinni = db.Cache.GetAll<HullData>().Single(h => h.Name == "Djinni");
        var cellSize = AuthoredSettings.Load(db.Root).Read<GameplaySettings>("GameplaySettings").SchematicCellSize;
        var halfDiagonal = .5f * cellSize * MathF.Sqrt(djinni.Shape.Width * djinni.Shape.Width + djinni.Shape.Height * djinni.Shape.Height);
        var radiusMin = 1.5f * halfDiagonal;

        Faction Maker(string shortName) =>
            db.Cache.GetAll<Faction>().FirstOrDefault(f => f.ShortName == shortName) ?? throw new InvalidOperationException($"No faction with short name \"{shortName}\".");

        // Min applies at quality 0 of the role and Max at quality 1, so a rising benefit is authored Min < Max and a
        // falling cost Min > Max, in the order the design tables give.
        PerformanceStat Rolled(float min, float max, string role) => new PerformanceStat
        {
            Min = min,
            Max = max,
            Terms = new List<StatTerm> { new StatTerm { Source = StatSource.Quality, Exponent = 1f, Role = role } },
        };
        StatModifierData Multiply(string behavior, string stat, float min, float max, string role) => new StatModifierData
        {
            Stat = new StatReference { Target = behavior, Stat = stat },
            Modifier = Rolled(min, max, role),
            Type = StatModifierType.Multiplier,
        };
        ConsumableItemData Design(string name, string description, float mass, int price, float duration, string[] roles, params BehaviorData[] behaviors) => new ConsumableItemData
        {
            Name = name,
            Description = description,
            Mass = mass,
            Price = price,
            Duration = duration,
            Stackable = false,
            Shape = new Shape(),
            SpecificHeat = template.SpecificHeat,
            Conductivity = template.Conductivity,
            Roles = roles.Select(r => new ItemRole { Name = r }).ToList(),
            Behaviors = behaviors.ToList(),
        };

        var overdrive = Design(designNames[0], "Opens every drive wide and holds it there. The ship is thrown forward on a bright, hot plume while the pilot keeps the wheel.",
            10f, 18000, 6f, new[] { "propellant", "regulator" },
            new ThrottleLockData(),
            Multiply(nameof(ThrusterData), nameof(ThrusterData.Thrust), 3f, 4.5f, "propellant"),
            Multiply(nameof(VelocityLimitData), nameof(VelocityLimitData.TopSpeed), 2f, 3f, "propellant"),
            Multiply(nameof(ThrusterData), nameof(ThrusterData.Heat), 5f, 3f, "regulator"),
            Multiply(nameof(ThrusterData), nameof(ThrusterData.Visibility), 5f, 3f, "regulator"));
        var vent = Design(designNames[1], "Dumps the coolant loop into space as a spreading bank of vapour. Sensors lose whoever is inside it, and the radiators pay for the loss.",
            15f, 12000, 15f, new[] { "nozzle", "coolant" },
            new VapourDumpData { Radius = Rolled(radiusMin, 1.5f * radiusMin, "nozzle"), Opacity = Rolled(.90f, .98f, "nozzle"), Lifetime = Rolled(14f, 20f, "coolant") },
            Multiply(nameof(RadiatorData), nameof(RadiatorData.Emissivity), .3f, .5f, "coolant"));

        var products = new (ConsumableItemData Design, string Name, string Maker, string Description)[]
        {
            (overdrive, "send it", "DME", "no brakes/ no regrets/ one good burn"),
            (overdrive, "Updraft", "Finch", "Light as a feather and, for six seconds, about as fast as one in a gale."),
            (vent, "Smoke Machine", "NiteLife", "Fills the room. Nobody can say who came in with you."),
            (vent, "Morning Fog", "Lightsail", "Rolls in thick, lifts slowly, and leaves the bills with the other fellow."),
        };
        foreach (var product in products) Maker(product.Maker);

        Console.WriteLine($"Vent radius Min {radiusMin:0.##} (Djinni {djinni.Shape.Width}x{djinni.Shape.Height} cells at {cellSize:0.##} units: half diagonal {halfDiagonal:0.##}, x1.5), Max {1.5f * radiusMin:0.##}");
        Console.WriteLine("New designs:");
        foreach (var design in new[] { overdrive, vent })
        {
            Console.WriteLine($"  {design.Name,-20} {design.Shape.Width}x{design.Shape.Height}  mass {design.Mass:0.##}  price {design.Price}  stackable {design.Stackable}  duration {design.Duration:0.##}  roles [{string.Join(", ", design.Roles.Select(r => r.Name))}]");
            foreach (var behavior in design.Behaviors)
            {
                switch (behavior)
                {
                    case StatModifierData m:
                        Console.WriteLine($"      {m.Type} {m.Stat.Target}.{m.Stat.Stat} {m.Modifier.Min:0.##}-{m.Modifier.Max:0.##} ({m.Modifier.Terms.Single().Role})");
                        break;
                    case VapourDumpData d:
                        Console.WriteLine($"      VapourDump radius {d.Radius.Min:0.##}-{d.Radius.Max:0.##} ({d.Radius.Terms.Single().Role}), opacity {d.Opacity.Min:0.##}-{d.Opacity.Max:0.##} ({d.Opacity.Terms.Single().Role}), lifetime {d.Lifetime.Min:0.##}-{d.Lifetime.Max:0.##} ({d.Lifetime.Terms.Single().Role})");
                        break;
                    default:
                        Console.WriteLine($"      {behavior.GetType().Name}");
                        break;
                }
            }
            Console.WriteLine($"      \"{design.Description}\"");
        }
        Console.WriteLine("\nNew products:");
        foreach (var (design, name, maker, description) in products)
        {
            var (mean, dev) = MakerProfiles[maker];
            Console.WriteLine($"  {design.Name} -> \"{name}\" by {maker}, every role mean {mean:0.##} dev {dev:0.##}: {description}");
        }

        if (!apply)
        {
            Console.WriteLine($"\nDry run. Pass \"apply\" to author 2 designs and {products.Length} products.");
            return 0;
        }

        foreach (var design in new[] { overdrive, vent }) CultRecordRefs.Validate(design);
        db.Cache.Commit(batch =>
        {
            var keys = new Dictionary<ConsumableItemData, CultRecordKey>();
            foreach (var design in new[] { overdrive, vent }) keys[design] = batch.Upsert(typeof(ConsumableItemData), design);
            foreach (var (design, name, maker, description) in products)
            {
                var (mean, dev) = MakerProfiles[maker];
                batch.Upsert(typeof(FactionProductData), new FactionProductData
                {
                    Name = name,
                    Description = description,
                    Design = new CultRecordRef<CraftedItemData>(keys[design]),
                    Manufacturer = db.Cache.RefOf(Maker(maker)),
                    Roles = design.Roles.Select(r => new ProductRole { Role = r.Name, Mean = mean, StandardDeviation = dev }).ToList(),
                });
            }
        });
        Console.WriteLine($"\nAuthored 2 designs and {products.Length} products in Aetheria.cc");
        return 0;
    }

    // generic-designs: the rename, brand and product-text tables (map: "Generic design names", "Content: brands, flavour and
    // functional text"). Public so the command test reads the same rows the command writes.
    public const string AuthoringNote = " (new text)";

    public static readonly (string Old, string Generic, string Functional)[] Renames =
    {
        ("ClearPath", "Light Machine Gun", "Light ballistic hose: many small rounds at short range."),
        ("6k Shooter", "Slug Rifle", "Single heavy slugs, fired as fast as the trigger."),
        ("Earp", "Long Slug Rifle", "A lighter slug rifle with the longest ballistic reach."),
        ("DeathCluster", "Cluster Cannon", "Medium shotgun: a heavy volley of pellets."),
        ("pretty pretty bang bang", "Charged Scattergun", "Ballistic shotgun that charges for a heavier volley."),
        ("Spectra", "Light Laser", "The lightest beam: low damage, quick cycle, long reach."),
        ("FastBlast+-", "Cell Blaster", "Rapid energy bursts fed by cells that cut its reactor draw."),
        ("ColdFire", "Long Laser", "Heavy small beam: slow, hard-hitting, long reach."),
        ("CShot RainbowLite Lazer", "Heavy Beam Laser", "The heaviest beam: nine cells, enormous damage and reach."),
        ("ChargeBlast+-", "Charged Blaster", "One charged knockout shot."),
        ("ChargeBlast SG", "Charged Scatter Blaster", "Charged energy shotgun."),
        ("plight", "Lightning Projector", "Charged chain lightning."),
        ("GT 3K", "Guided Missile Launcher", "Fires guided missiles."),
        ("LRMM72", "Long-Range Missile Rack", "Large missile rack for long engagements."),
        ("SRMM72", "Short-Range Missile Rack", "Extra-large rack of short-range missiles."),
        ("pswarm", "Swarm Rocket Pod", "Dumbfire rockets in a swarm."),
        ("scorched void policy", "Incendiary Rocket Pod", "Incendiary dumbfire rockets."),
        ("Arctica", "Adaptive Radiator", "Radiator whose output scales widely with conditions."),
        ("Iapyx", "Radiator", "Standard two-cell radiator."),
        ("Skiron", "Long Radiator", "Four-cell radiator with a large surface."),
        ("OK Disperser", "Heat Pump", "Moves heat to where it can be shed."),
        ("cold like my heart", "Cryogenic Heat Pump", "Heavy heat pump that drives temperatures far down."),
        ("Core Power", "Compact Reactor", "Small reactor."),
        ("Manhattan", "Heavy Reactor", "Large three-by-three reactor."),
        ("Notorious", "High-Yield Reactor", "Large reactor with a wide output range."),
        ("Vulcan", "Ring Reactor", "Ring-shaped reactor, four by four."),
        ("MoveOnPro", "Muon Fusion Reactor", "Compact fusion reactor that draws a reservoir."),
        ("Core Power Station Reactor", "Station Reactor", "A reactor sized for a station's core."),
        ("Manhattan Station Reactor", "Heavy Station Reactor", "A heavy reactor sized for a station's core."),
        ("Notorious Station Reactor", "High-Yield Station Reactor", "A high-yield reactor sized for a station's core."),
        ("Vulcan Station Reactor", "Ring Station Reactor", "A ring reactor sized for a station's core."),
        ("MoveOnPro Station Reactor", "Muon Fusion Station Reactor", "A muon fusion reactor sized for a station's core."),
        ("RevvITup 2.0", "High-Thrust Drive", "Large drive: huge thrust, little torque."),
        ("deep space burnout", "Burn Drive", "Large drive that runs very hot."),
        ("Talaria", "Rugged Drive", "Two-cell working drive."),
        ("Victoire", "Racing Drive", "Feather-light two-cell drive with extreme response."),
        ("The Bat", "Sensor Array", "Two-by-two long-range sensor array."),
        ("not if i see you first", "Compact Sensor", "One-cell sensor."),
        ("PotaT+-", "Capacitor", "Stores charge for burst draw."),
        ("SafeStuff II", "Secure Cargo Bay", "Three-by-three secured hold."),
        ("Store-All Enterprise Edition", "Large Cargo Bay", "Six-by-six hold."),
        ("Store-All Plus", "Cargo Bay", "Two-by-two hold."),
        ("Store-All Plus Mini", "Small Cargo Bay", "One-cell hold."),
    };

    public static readonly (string Design, string Maker, string OldProduct, string Brand, string Text, string DesignText)[] Brands =
    {
        ("Autocannon", "Zhestokost", "Autocannon", "Hammerfall",
            "The rounds fall steadily, and they do not stop until the work is done.",
            "Belt-fed cannon: a steady stream of rounds at long reach."),
        ("Cockpit 2x2", "Lightsail", "Cockpit 2x2", "Wheelhouse",
            "Every Lightsail captain's favourite chair. The view comes standard.",
            "Two-by-two crew cockpit."),
        ("Industrial Thermostatic Heater", "Lightsail", "Industrial Thermostatic Heater", "Hearth",
            "A warm hold, a warm crew, and a warm welcome at the cold end of the run.",
            "Turns reactor power into heat to keep the hull warm."),
        ("Medium Drive", "Lightsail", "Medium Drive", "Steady Haul",
            "Keeps you going and going and going...",
            "Mid-sized working drive."),
        ("Small Drive", "Lightsail", "Small Drive", "Short Hop",
            "Dock to dock, and not a metre further than the manifest says.",
            "Small working drive."),
        ("Turret Control Module", "AU", "Turret Control Module", "Watchtower",
            "Point it at the dark and get back to work. It keeps watch.",
            "Control module for a station turret."),
        ("Medium Docking Bay", "AU", "Medium Docking Bay", "Open Berth",
            "Room for a friend, a customer or a tug. We don't ask which.",
            "Docking bay for mid-sized ships."),
        ("Mine Launcher", "DME", "Mine Launcher", "parting gift",
            "we were never here/ but we left you something/ it's waiting where we were",
            "Lays a drifting proximity mine that arms after two seconds."),
    };

    public static readonly (string Product, string Maker, string Text)[] ProductTexts =
    {
        ("Victoire", "Alakrita",
            "The most responsive thruster on the market, for the most irresponsible of racing pilots."),
        ("DragOnBreath", "Lucent",
            "Chain lightning has your enemies chain smoking while you enjoy a relaxing DragOnBreath vapepop, now available in 17! flavours (manufactured by DragOnBreath, a subsidary of Lucent Media)"),
        ("Steadfast", "R&D",
            "It gets the job done, but that's all. Rated for continuous operation. \"Continuous\" does not imply \"uninterrupted.\""),
        ("Steadfast Station", "R&D",
            "Rated for continuous operation at station scale. The fine print scales too."),
        ("Manhattan Station", "Zhestokost",
            "A fortress needs a furnace. This is the furnace."),
        ("Notorious Station", "Lucent",
            "The biggest reaction in the system, and everyone docked here will hear about it."),
        ("Vulcan Station", "AU",
            "Keeps the lights on and the forges hot."),
        ("MoveOnPro Station", "NiteLife",
            "The club never closes. Neither does the reactor."),
        ("LRMM72", "R&D",
            "Engages targets at up to seventy-two kilometres!* *In a vacuum, against a cooperative target."),
        ("SRMM72", "DME",
            "seventy-two reasons/ to keep your distance/ all of them leaving at once"),
        ("Iapyx", "AU",
            "Two cells of honest fin. Bolt it on and forget it's there."),
        ("Skiron", "AU",
            "When the job runs hot, run a bigger fin."),
        ("Talaria", "AU",
            "Takes a knock, keeps pushing. Built for crews who work for a living."),
        ("RevvITup 2.0", "NiteLife",
            "Floor it. The bass drops when you do."),
        ("PotaT+-", "NiteLife",
            "Holds the charge until the drop, then lets it all go at once."),
        ("not if i see you first", "DME",
            "eyes in the dark/ you were seen before you looked/ say goodnight"),
        ("Space Tractor", "Ewan Hart",
            "It's a tractor. In space. Trap your enemies in gravity wells. Haul thousands of tons of hay through space!"),
    };

    // Designs whose text was flavour or named a brand, rewritten as tech-node text without a rename (ruling designs-are-tech-nodes).
    // The Large Drive's old line ships as Steady Haul's text; the Tractor Beam's moves to its product, Space Tractor (ProductTexts).
    public static readonly (string Design, string Text)[] DesignTexts =
    {
        ("Large Drive", "Large working drive."),
        ("Tractor Beam", "Holds a target in a gravity well and hauls it in."),
        ("Smoke Proximity", "A Light Laser that bursts by proximity, blast 8. For scenarios; no one builds it."),
        ("Smoke Delayed", "A Light Laser that bursts after two cells of metal, blast 4. For scenarios; no one builds it."),
        ("Smoke Refused", "A Light Laser whose blast is wider than its reach, so it never fires. For scenarios; no one builds it."),
        ("Smoke Refused Charge", "A Charged Scatter Blaster whose blast is wider than its reach, so it never charges. For scenarios; no one builds it."),
    };

    // generic-designs: a design is named and described like a node on a tech tree, and the brand lives in the product it
    // sells (rulings catalog-grows-generic-designs-branded-products, kitchen-sink-content, designs-are-tech-nodes). A
    // Renames row renames the design found under its old name and gives it the functional text; a Brands row renames the
    // maker's same-named product and sets its text and the design's functional text; ProductTexts sets the rest; every
    // shipped product text ending in the authoring note loses it. Dry run unless passed "apply".
    public static int GenericDesigns(bool apply, string root = null)
    {
        var db = AetherDb.Open(catalogWritable: apply, root: root);
        var designs = db.Cache.GetAll<EquippableItemData>().ToArray();
        var products = db.Cache.GetAll<FactionProductData>().ToArray();
        string MakerOf(FactionProductData product) => db.Cache.Get(product.Manufacturer)?.ShortName;
        var changed = new List<(object Document, CultRecordKey Key)>();
        var report = new List<string>();

        void Write(object document, string what, string before, string after)
        {
            if (!changed.Any(c => ReferenceEquals(c.Document, document))) changed.Add((document, db.Cache.RefOf(document).Key));
            report.Add($"{what}: \"{before}\" -> \"{after}\"");
        }

        foreach (var (old, generic, functional) in Renames)
        {
            var found = designs.Where(d => d.Name == old).ToArray();
            var held = designs.Where(d => d.Name == generic).ToArray();
            if (found.Length == 0 && held.Length == 0) throw new InvalidOperationException($"Rename row \"{old}\" -> \"{generic}\": no design carries either name.");
            if (found.Length == 0) continue;
            if (held.Length != 0) throw new InvalidOperationException($"Rename row \"{old}\" -> \"{generic}\": the generic name is already held by another design.");
            if (found.Length != 1) throw new InvalidOperationException($"Rename row \"{old}\" -> \"{generic}\": {found.Length} designs carry the old name.");
            var design = found[0];
            Write(design, "design name", design.Name, generic);
            design.Name = generic;
            if (design.Description != functional) Write(design, $"design \"{generic}\" text", design.Description, functional);
            design.Description = functional;
        }

        foreach (var (designName, maker, oldProduct, brand, text, designText) in Brands)
        {
            var found = designs.Where(d => d.Name == designName).ToArray();
            if (found.Length != 1) throw new InvalidOperationException($"Brand row \"{brand}\": {found.Length} designs are named \"{designName}\".");
            var design = found[0];
            var key = db.Cache.RefOf(design).Key;
            var sold = products.Where(p => p.Design.Key.Equals(key) && MakerOf(p) == maker).ToArray();
            var product = sold.FirstOrDefault(p => p.Name == brand) ?? sold.FirstOrDefault(p => p.Name == oldProduct);
            if (product == null) throw new InvalidOperationException($"Brand row \"{brand}\": {maker} sells no product of \"{designName}\" under either name.");
            if (product.Name == brand) continue;
            if (products.Any(p => !ReferenceEquals(p, product) && p.Name == brand)) throw new InvalidOperationException($"Brand row \"{brand}\": the brand is already held by another product.");
            Write(product, $"product of \"{designName}\" by {maker}", product.Name, brand);
            product.Name = brand;
            Write(product, $"product \"{brand}\" text", product.Description, text);
            product.Description = text;
            if (design.Description != designText) Write(design, $"design \"{designName}\" text", design.Description, designText);
            design.Description = designText;
        }

        foreach (var (designName, text) in DesignTexts)
        {
            var found = designs.Where(d => d.Name == designName).ToArray();
            if (found.Length != 1) throw new InvalidOperationException($"Design text row \"{designName}\": {found.Length} designs carry the name.");
            if (found[0].Description == text) continue;
            Write(found[0], $"design \"{designName}\" text", found[0].Description, text);
            found[0].Description = text;
        }

        foreach (var (name, maker, text) in ProductTexts)
        {
            var found = products.Where(p => p.Name == name && MakerOf(p) == maker).ToArray();
            if (found.Length != 1) throw new InvalidOperationException($"Product text row \"{name}\" by {maker}: {found.Length} products match.");
            if (found[0].Description == text) continue;
            Write(found[0], $"product \"{name}\" text", found[0].Description, text);
            found[0].Description = text;
        }

        foreach (var product in products.Where(p => p.Description != null && p.Description.EndsWith(AuthoringNote, StringComparison.Ordinal)))
        {
            var stripped = product.Description.Substring(0, product.Description.Length - AuthoringNote.Length);
            Write(product, $"product \"{product.Name}\" authoring note stripped", product.Description, stripped);
            product.Description = stripped;
        }

        if (changed.Count == 0)
        {
            Console.WriteLine("Already applied; nothing to do.");
            return 0;
        }
        foreach (var line in report) Console.WriteLine(line);
        if (!apply)
        {
            Console.WriteLine($"\nDry run. Pass \"apply\" to write {changed.Count} records.");
            return 0;
        }
        foreach (var (document, _) in changed) CultRecordRefs.Validate(document);
        // ExpectUnchanged lands the batch onto the file as it is: every record the command did not author keeps the bytes it has,
        // where a plain commit would re-encode the whole catalog through today's types.
        if (!db.Cache.Commit(batch =>
        {
            batch.ExpectUnchanged();
            foreach (var (document, key) in changed) batch.Upsert(document.GetType(), document, key);
        }))
            throw new InvalidOperationException("The catalog changed while the generic designs were being authored; nothing was written. Run it again.");
        Console.WriteLine($"\nWrote {changed.Count} records to Aetheria.cc");
        return 0;
    }

    // aetheria-ew cut ew-hide-launch-catalog (rulings ew-suppressor-strength-ruled, ew-suppressor-makers-ruled,
    // ew-mild-suppressor-makers-defaulted): two launch suppressors on the Tool hardpoint, one helper for both. The strong
    // Launch Suppressor hides a launch from its own target and gives out after an opening volley; the mild Launch Baffle
    // only dims the flash for distant observers and runs through a long fight. Every figure is one Quality term, linear in
    // its role (baffles for the Factor, heat sink for the heat), so quality moves the part and nothing else does. Thermal
    // constants and temperatures come from the Targeting Computer, the catalog's 1x1 tool, as the consumables do. Dry run
    // unless passed "apply".
    public static int LaunchSuppressorCatalog(bool apply, string root = null)
    {
        var db = AetherDb.Open(catalogWritable: apply, root: root);
        var designNames = new[] { "Launch Suppressor", "Launch Baffle" };
        var template = db.Cache.GetAll<GearData>().Single(g => g.Name == "Targeting Computer");

        Faction Maker(string shortName) =>
            db.Cache.GetAll<Faction>().FirstOrDefault(f => f.ShortName == shortName) ?? throw new InvalidOperationException($"No faction with short name \"{shortName}\".");
        PerformanceStat Rolled(float min, float max, string role) => new PerformanceStat
        {
            Min = min,
            Max = max,
            Terms = new List<StatTerm> { new StatTerm { Source = StatSource.Quality, Exponent = 1f, Role = role } },
        };
        // Min applies at quality 0 of the role and Max at quality 1: a falling Factor and falling heat per unit are Min > Max.
        GearData Design(string name, string description, int price, (float Min, float Max) factor, (float Min, float Max) heatPerUnit, (float Min, float Max) rated)
        {
            var design = new GearData
            {
                Name = name,
                Description = description,
                Hardpoint = HardpointType.Tool,
                Mass = 60f,
                Price = price,
                Durability = 50f,
                Shape = new Shape(1, 1),
                SpecificHeat = template.SpecificHeat,
                Conductivity = template.Conductivity,
                MinimumTemperature = template.MinimumTemperature,
                MaximumTemperature = template.MaximumTemperature,
                OptimalTemperature = template.OptimalTemperature,
                PlateauWidth = template.PlateauWidth,
                ThermalResilience = template.ThermalResilience,
                Roles = new[] { "baffles", "heat sink" }.Select(r => new ItemRole { Name = r }).ToList(),
                Behaviors = new List<BehaviorData>
                {
                    new LaunchSuppressorData
                    {
                        Factor = Rolled(factor.Min, factor.Max, "baffles"),
                        HeatPerUnit = Rolled(heatPerUnit.Min, heatPerUnit.Max, "heat sink"),
                        RatedTemperature = Rolled(rated.Min, rated.Max, "heat sink"),
                    },
                },
            };
            design.Shape.Cells[0, 0] = true;
            return design;
        }

        var strong = Design(designNames[0], "Baffles and a heat sink remove nearly all of a weapon's launch flash, hiding the shot from its target. The removed light is dissipated as heat in the device's own cell; above its rated temperature it passes the full flash.",
            40000, (.08f, .03f), (1.4f, .8f), (310f, 330f));
        var mild = Design(designNames[1], "Baffles dim a weapon's launch flash to a fraction and a heat sink absorbs the difference. The target still sees the shot; distant observers do not. The sink sustains the effect through a long fight.",
            25000, (.4f, .2f), (.8f, .5f), (310f, 330f));
        var designs = new[] { strong, mild };

        var products = new (GearData Design, string Name, string Maker, string Description)[]
        {
            (strong, "Velvet Rope", "NiteLife", "Keeps the flash on the inside. Nobody sees who you let out."),
            (strong, "Hush", "Finch", "Small, quiet, and the first shot is a secret. The second, probably."),
            (strong, "lights out", "DME", "no flash/ no name/ nobody saw a thing"),
            (strong, "Plausible Deniability", "R&D", "Hides 100% of launches!* *Launches defined as the ones nobody saw."),
            (mild, "Low Profile", "Lightsail", "Quiet on the manifest, quiet on the scope. A hauler nobody notices is a hauler that gets paid."),
            (mild, "Fast Pass Dim", "Alakrita", "Takes the glare off every launch and keeps the schedule. Anyone close enough to mind was already your problem."),
        };
        foreach (var product in products) Maker(product.Maker);

        // Authored when both designs exist and every product is present on its own design; untouched when nothing is
        // present; anything between needs a human, a product name that belongs to an unrelated item included.
        var existing = designNames.Select(name => db.Cache.GetAll<GearData>().SingleOrDefault(g => g.Name == name)).ToArray();
        var productNames = products.Select(p => p.Name).ToHashSet();
        var presentProducts = db.Cache.GetAll<FactionProductData>().Where(p => productNames.Contains(p.Name)).ToArray();
        if (existing.All(d => d != null) && presentProducts.Length == products.Length
            && products.All(p => presentProducts.Any(x => x.Name == p.Name && x.Design.Key.Equals(db.Cache.RefOf(existing[p.Design == strong ? 0 : 1]).Key))))
        {
            Console.WriteLine("The launch suppressors are already authored; nothing to do.");
            return 0;
        }
        if (existing.Any(d => d != null) || presentProducts.Length != 0)
            throw new InvalidOperationException("The launch suppressors are partly authored; the catalog needs a human.");

        Console.WriteLine("New designs:");
        foreach (var design in designs)
        {
            var data = design.Behaviors.OfType<LaunchSuppressorData>().Single();
            Console.WriteLine($"  {design.Name,-18} {design.Shape.Width}x{design.Shape.Height} {design.Hardpoint}  mass {design.Mass:0.##}  price {design.Price}  durability {design.Durability:0.##}  roles [{string.Join(", ", design.Roles.Select(r => r.Name))}]");
            foreach (var (label, stat) in new[] { ("Factor", data.Factor), ("HeatPerUnit", data.HeatPerUnit), ("RatedTemperature", data.RatedTemperature) })
                Console.WriteLine($"      {label} {stat.Min:0.##}-{stat.Max:0.##} ({stat.Terms.Single().Role})");
            Console.WriteLine($"      \"{design.Description}\"");
        }
        Console.WriteLine("\nNew products:");
        foreach (var (design, name, maker, description) in products)
        {
            var (mean, dev) = MakerProfiles[maker];
            Console.WriteLine($"  {design.Name} -> \"{name}\" by {maker}, every role mean {mean:0.##} dev {dev:0.##}: {description}");
        }

        if (!apply)
        {
            Console.WriteLine($"\nDry run. Pass \"apply\" to author 2 designs and {products.Length} products.");
            return 0;
        }

        foreach (var design in designs) CultRecordRefs.Validate(design);
        // ExpectUnchanged lands the batch onto the file as it is: every other record keeps the bytes it has, where a plain
        // commit would re-encode the whole catalog through today's types.
        if (!db.Cache.Commit(batch =>
        {
            batch.ExpectUnchanged();
            var keys = new Dictionary<GearData, CultRecordKey>();
            foreach (var design in designs) keys[design] = batch.Upsert(typeof(GearData), design);
            foreach (var (design, name, maker, description) in products)
            {
                var (mean, dev) = MakerProfiles[maker];
                batch.Upsert(typeof(FactionProductData), new FactionProductData
                {
                    Name = name,
                    Description = description,
                    Design = new CultRecordRef<CraftedItemData>(keys[design]),
                    Manufacturer = db.Cache.RefOf(Maker(maker)),
                    Roles = design.Roles.Select(r => new ProductRole { Role = r.Name, Mean = mean, StandardDeviation = dev }).ToList(),
                });
            }
        }))
            throw new InvalidOperationException("The catalog changed while the launch suppressors were being authored; nothing was written. Run it again.");
        Console.WriteLine($"\nAuthored 2 designs and {products.Length} products in Aetheria.cc");
        return 0;
    }

    // aetheria-release cut scanner-catalog (rulings catalog-grows-generic-designs-branded-products, designs-are-tech-nodes,
    // catalog-breadth-roles-and-components, sensor-stat-set, scan-held-beam, miss-terris-belongs-to-emily-r3): the catalog sells
    // an Inspection Scanner, one generic Tool design carrying one ScannerData, and three products from makers whose
    // emission discipline is quiet (Finch), middling (AU) and loud (Lucent). Range and ScanTime are one linear Quality term
    // each on the scan array; the intercept factor is the sensor curve on emission-discipline, so the brand's voice lives in
    // that role's mean (DisciplineMean) as it does for sensors. Thermal fields come from the Targeting Computer, the catalog's
    // 1x1 tool. Dry run unless passed "apply"; a catalog that is neither untouched nor fully authored is refused.
    public static int InspectionScannerCatalog(bool apply, string root = null)
    {
        var db = AetherDb.Open(catalogWritable: apply, root: root);
        const string designName = "Inspection Scanner";
        const string scanArray = "scan array";
        var template = db.Cache.GetAll<GearData>().Single(g => g.Name == "Targeting Computer");

        Faction Maker(string shortName) =>
            db.Cache.GetAll<Faction>().FirstOrDefault(f => f.ShortName == shortName) ?? throw new InvalidOperationException($"No faction with short name \"{shortName}\".");
        PerformanceStat Rolled(float min, float max, string role, float exponent = 1f) => new PerformanceStat
        {
            Min = min,
            Max = max,
            Terms = new List<StatTerm> { new StatTerm { Source = StatSource.Quality, Exponent = exponent, Role = role } },
        };

        var design = new GearData
        {
            Name = designName,
            Description = "Held on a tracked ship within its range for the scan time, it reveals every item fitted to that ship, the quiet ones included. The scanned ship is warned and the scan is heard at the scanner's range over its intercept factor.",
            Hardpoint = HardpointType.Tool,
            Mass = 40f,
            Price = 90000,
            Durability = 40f,
            Shape = new Shape(1, 1),
            SpecificHeat = template.SpecificHeat,
            Conductivity = template.Conductivity,
            MinimumTemperature = template.MinimumTemperature,
            MaximumTemperature = template.MaximumTemperature,
            OptimalTemperature = template.OptimalTemperature,
            PlateauWidth = template.PlateauWidth,
            ThermalResilience = template.ThermalResilience,
            Roles = new[] { scanArray, EmissionDiscipline }.Select(r => new ItemRole { Name = r }).ToList(),
            Behaviors = new List<BehaviorData>
            {
                new ScannerData
                {
                    // Min applies at quality 0 of the role and Max at quality 1: a falling scan time is Min > Max.
                    Range = Rolled(350f, 550f, scanArray),
                    ScanTime = Rolled(10f, 6f, scanArray),
                    InterceptFactor = Rolled(.5f, 2f, EmissionDiscipline, MathF.Log2(3f)),
                },
            },
        };
        design.Shape.Cells[0, 0] = true;

        var products = new (string Name, string Maker, string Description)[]
        {
            ("Inkling", "Finch", "Reads every item on a hull and barely clears its throat doing it. The other pilot gets a feeling, not a warning."),
            ("Assay", "AU", "Surveys a ship the way a crew surveys a seam: steadily, thoroughly, at a volume the neighbours learn to ignore."),
            ("Full Disclosure", "Lucent", "Everything fitted to the other ship, on the record, and every ear in range hears you ask."),
        };
        foreach (var product in products) Maker(product.Maker);

        // Authored when the design exists and all three products are present on it; untouched when nothing is present;
        // anything between needs a human, a product name that belongs to an unrelated item included.
        var existing = db.Cache.GetAll<GearData>().SingleOrDefault(g => g.Name == designName);
        var productNames = products.Select(p => p.Name).ToHashSet();
        var presentProducts = db.Cache.GetAll<FactionProductData>().Where(p => productNames.Contains(p.Name)).ToArray();
        if (existing != null && presentProducts.Length == products.Length
            && products.All(p => presentProducts.Any(x => x.Name == p.Name && x.Design.Key.Equals(db.Cache.RefOf(existing).Key))))
        {
            Console.WriteLine("The inspection scanner is already authored; nothing to do.");
            return 0;
        }
        if (existing != null || presentProducts.Length != 0)
            throw new InvalidOperationException("The inspection scanner is partly authored; the catalog needs a human.");

        var data = design.Behaviors.OfType<ScannerData>().Single();
        Console.WriteLine("New design:");
        Console.WriteLine($"  {design.Name} {design.Shape.Width}x{design.Shape.Height} {design.Hardpoint}  mass {design.Mass:0.##}  price {design.Price}  durability {design.Durability:0.##}  roles [{string.Join(", ", design.Roles.Select(r => r.Name))}]");
        foreach (var (label, stat) in new[] { ("Range", data.Range), ("ScanTime", data.ScanTime), ("InterceptFactor", data.InterceptFactor) })
            Console.WriteLine($"      {label} {stat.Min:0.##}-{stat.Max:0.##} ({stat.Terms.Single().Role}, exponent {stat.Terms.Single().Exponent:0.####})");
        Console.WriteLine($"      \"{design.Description}\"");
        Console.WriteLine("\nNew products:");
        foreach (var (name, maker, description) in products)
        {
            var (mean, dev) = MakerProfiles[maker];
            Console.WriteLine($"  {design.Name} -> \"{name}\" by {maker}, {scanArray} mean {mean:0.##} dev {dev:0.##}, {EmissionDiscipline} mean {DisciplineMean(Maker(maker)):0.##} dev {dev:0.##}: {description}");
        }

        if (!apply)
        {
            Console.WriteLine($"\nDry run. Pass \"apply\" to author 1 design and {products.Length} products.");
            return 0;
        }

        CultRecordRefs.Validate(design);
        // ExpectUnchanged lands the batch onto the file as it is: every other record keeps the bytes it has, where a plain
        // commit would re-encode the whole catalog through today's types.
        if (!db.Cache.Commit(batch =>
        {
            batch.ExpectUnchanged();
            var key = batch.Upsert(typeof(GearData), design);
            foreach (var (name, maker, description) in products)
            {
                var (mean, dev) = MakerProfiles[maker];
                batch.Upsert(typeof(FactionProductData), new FactionProductData
                {
                    Name = name,
                    Description = description,
                    Design = new CultRecordRef<CraftedItemData>(key),
                    Manufacturer = db.Cache.RefOf(Maker(maker)),
                    Roles = design.Roles.Select(r => new ProductRole
                    {
                        Role = r.Name,
                        Mean = r.Name == EmissionDiscipline ? DisciplineMean(Maker(maker)) : mean,
                        StandardDeviation = dev,
                    }).ToList(),
                });
            }
        }))
            throw new InvalidOperationException("The catalog changed while the inspection scanner was being authored; nothing was written. Run it again.");
        Console.WriteLine($"\nAuthored 1 design and {products.Length} products in Aetheria.cc");
        return 0;
    }

}

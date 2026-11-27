using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GameCult.Caching;

// Puts shields on sale (campaign aetheria-release, cut hardpoint-fit-1; rulings catalog-grows-generic-designs-branded-products,
// designs-are-tech-nodes, hardpoint-sizes-are-footprint-palette, gear-ladder-wide-by-rare-inputs): two generic Shield designs,
// a Light Shield Projector (1x1) and a Heavy Shield Projector (2x2), each sold by two makers. Loose fit lets the light one
// serve the starter's 1x1 mount and the heavy one fill the Djinni's and the Nakovalnya's 2x2 mounts. Every stat is one linear
// Quality term on its ShieldRoles role (Min at quality 0, Max at quality 1); prices and stats are starting knobs, sim-tuned later.
// Recipes wait on aetheria-living-world:follow_up:design-bills-of-materials. Every record is keyed by its name, so the same
// apply replays byte-identically at a merge. Dry run unless apply; a catalog that is neither untouched nor fully authored is refused.
public static class ShieldCatalog
{
    public const string Light = "Light Shield Projector";
    public const string Heavy = "Heavy Shield Projector";
    private const string Emitter = "emitter", Reservoir = "reservoir", Regulator = "regulator";

    private sealed record Design(string Name, string Description, int Size, float Mass, float Durability, int Price,
        (float Min, float Max) Efficiency, (float Min, float Max) EnergyUsage, (float Min, float Max) Capacity,
        (float Min, float Max) RefillDuration, (float Min, float Max) RestoreDuration);

    private static readonly Design[] Designs =
    {
        new(Light, "A compact emitter and reservoir that wraps a hull in a field it spends energy to hold. Small enough for any mount.",
            1, 120f, 100f, 3000, (.05f, .15f), (8f, 4f), (60f, 180f), (8f, 4f), (24f, 12f)),
        new(Heavy, "A broad-aperture projector with a deep reservoir: it soaks heavy fire, and takes its time coming back when overwhelmed.",
            2, 420f, 250f, 34000, (.05f, .2f), (8f, 3f), (180f, 520f), (12f, 5f), (34f, 16f)),
    };

    private static readonly (string Name, string Design, string Maker, string Description)[] Products =
    {
        ("Umbrella", Light, "AU", "Shade for one hull: a small field, held steadily, and no one aboard has to think about it."),
        ("Awning", Light, "Aya", "Draws out over the bow like cloth on a pole. It will not stop a siege and was never meant to."),
        ("Migdal", Heavy, "Megiddo", "The tower the fleet stands behind. Megiddo sells it as a promise and builds it to keep one."),
        ("Redoubt", Heavy, "Zhestokost", "A wall of field, thrown up crudely and sold cheaply to those who expect to be shot at."),
    };

    // A record's key is derived from its name: the same table lands the same records every time it is applied.
    private static CultRecordKey KeyOf(string name) =>
        new(new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("shield-catalog:" + name)).AsSpan(0, 16)).ToString("N"));

    private static PerformanceStat Rolled((float Min, float Max) range, string role) => new PerformanceStat
    {
        Min = range.Min,
        Max = range.Max,
        Terms = new List<StatTerm> { new StatTerm { Source = StatSource.Quality, Exponent = 1f, Role = role } },
    };

    private static GearData Build(Design row)
    {
        var design = new GearData
        {
            Name = row.Name,
            Description = row.Description,
            Hardpoint = HardpointType.Shield,
            Mass = row.Mass,
            Price = row.Price,
            Durability = row.Durability,
            Shape = new Shape(row.Size, row.Size),
            // Thermal fields from content-batch-one 4a.
            SpecificHeat = 2f,
            Conductivity = 4f,
            ThermalResilience = 8f,
            MinimumTemperature = 173.15f,
            MaximumTemperature = 573.15f,
            OptimalTemperature = 258.15f,
            PlateauWidth = 84f,
            Roles = new[] { Emitter, Reservoir, Regulator }.Select(r => new ItemRole { Name = r }).ToList(),
            Behaviors = new List<BehaviorData>
            {
                new ShieldData
                {
                    Efficiency = Rolled(row.Efficiency, Emitter),
                    EnergyUsage = Rolled(row.EnergyUsage, Regulator),
                    Capacity = Rolled(row.Capacity, Reservoir),
                    RefillDuration = Rolled(row.RefillDuration, Regulator),
                    RestoreDuration = Rolled(row.RestoreDuration, Regulator),
                },
            },
        };
        for (var x = 0; x < row.Size; x++)
            for (var y = 0; y < row.Size; y++)
                design.Shape.Cells[x, y] = true;
        return design;
    }

    public static int Run(CultCache cache, bool apply, TextWriter output)
    {
        Faction Maker(string shortName) =>
            cache.GetAll<Faction>().FirstOrDefault(f => f.ShortName == shortName) ?? throw new InvalidOperationException($"No faction with short name \"{shortName}\".");
        foreach (var product in Products) Maker(product.Maker);

        // Authored when every design and product is present under its key; untouched when none is; anything between needs a human.
        var keys = Designs.Select(d => KeyOf(d.Name)).Concat(Products.Select(p => KeyOf(p.Name))).ToArray();
        var held = cache.GetAll<EquippableItemData>().Select(d => cache.RefOf(d).Key)
            .Concat(cache.GetAll<FactionProductData>().Select(p => cache.RefOf(p).Key)).ToHashSet();
        var present = keys.Count(held.Contains);
        var names = Designs.Select(d => d.Name).Concat(Products.Select(p => p.Name)).ToHashSet();
        var strangers = cache.GetAll<EquippableItemData>().Select(d => d.Name).Concat(cache.GetAll<FactionProductData>().Select(p => p.Name))
            .Count(names.Contains);
        if (present == keys.Length)
        {
            output.WriteLine("The shield designs are already authored; nothing to do.");
            return 0;
        }
        if (present != 0 || strangers != 0)
            throw new InvalidOperationException("The shield designs are partly authored, or their names belong to other items; the catalog needs a human.");

        var designs = Designs.Select(d => (Row: d, Gear: Build(d))).ToArray();
        output.WriteLine("New designs:");
        foreach (var (_, gear) in designs)
        {
            var data = gear.Behaviors.OfType<ShieldData>().Single();
            output.WriteLine($"  {gear.Name} {gear.Shape.Width}x{gear.Shape.Height} {gear.Hardpoint}  mass {gear.Mass:0.##}  price {gear.Price}  durability {gear.Durability:0.##}  roles [{string.Join(", ", gear.Roles.Select(r => r.Name))}]");
            foreach (var (label, stat) in new[] { ("Efficiency", data.Efficiency), ("EnergyUsage", data.EnergyUsage), ("Capacity", data.Capacity), ("RefillDuration", data.RefillDuration), ("RestoreDuration", data.RestoreDuration) })
                output.WriteLine($"      {label} {stat.Min:0.##}-{stat.Max:0.##} ({stat.Terms.Single().Role})");
        }
        output.WriteLine("\nNew products:");
        foreach (var (name, design, maker, description) in Products)
        {
            var (mean, dev) = Program.MakerProfiles[maker];
            output.WriteLine($"  {design} -> \"{name}\" by {maker}, mean {mean:0.##} dev {dev:0.##}: {description}");
        }
        if (!apply)
        {
            output.WriteLine($"\nDry run. Pass \"apply\" to author {Designs.Length} designs and {Products.Length} products.");
            return 0;
        }

        foreach (var (_, gear) in designs) CultRecordRefs.Validate(gear);
        // The batch lands onto the file as it is, expecting every key it authors to be absent (ProductLinesCommand's contract).
        if (!cache.Commit(batch =>
        {
            foreach (var key in keys) batch.Expect(key, null);
            foreach (var (row, gear) in designs) batch.Upsert(typeof(GearData), gear, KeyOf(row.Name));
            foreach (var (name, design, maker, description) in Products)
            {
                var (mean, dev) = Program.MakerProfiles[maker];
                batch.Upsert(typeof(FactionProductData), new FactionProductData
                {
                    Name = name,
                    Description = description,
                    Design = new CultRecordRef<CraftedItemData>(KeyOf(design)),
                    Manufacturer = cache.RefOf(Maker(maker)),
                    Roles = new[] { Emitter, Reservoir, Regulator }.Select(r => new ProductRole { Role = r, Mean = mean, StandardDeviation = dev }).ToList(),
                }, KeyOf(name));
            }
        }))
            throw new InvalidOperationException("The catalog changed while the shields were being authored; nothing was written. Run it again.");
        output.WriteLine($"\nAuthored {Designs.Length} designs and {Products.Length} products in Aetheria.cc");
        return 0;
    }
}

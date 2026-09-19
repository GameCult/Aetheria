using System;
using System.IO;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// Cut 7 (docs/stats-and-power-cut.md): designs declare roles, stats name the role they read, and products author
// per-role quality. Two rules pinned here that StatsReadTheLot (LoadoutTests.cs) and CreateLotFillsRolesFromProductSpread
// (LoadoutTests.cs) do not already cover: a stat naming a role its design lacks is refused at Upsert, and two
// products of one design with different role means resolve to different values through the same stat.
public sealed class RoleAuthoringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-roles-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public RoleAuthoringTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    private GameplaySettings Settings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp()
    };

    // The rule the ruling names directly: "validation already refuses a stat naming a role its design lacks."
    // A term reading StatSource.Quality with Role "ghost" on a design that declares no roles at all must not
    // reach disk -- silently falling back to Lot.Quality (Lot.QualityForRole's own behaviour for an unmatched
    // role) would hide the authoring mistake instead of naming it.
    [Fact]
    public void UpsertRefusesAStatNamingARoleItsDesignLacks()
    {
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        var ghost = new GearData
        {
            Name = "Ghost Lamp", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 10,
            Behaviors =
            {
                new EnergyDrawData
                {
                    EnergyDraw = new PerformanceStat
                    {
                        Min = 0, Max = 1,
                        Terms = { new StatTerm { Source = StatSource.Quality, Exponent = 1, Role = "ghost" } }
                    }
                }
            }
        };
        var ex = Assert.Throws<InvalidOperationException>(() => cache.Upsert(ghost));
        Assert.Contains("ghost", ex.Message);
    }

    // The positive control for the same rule: a stat naming a role the design DOES declare must not throw --
    // otherwise UpsertRefusesAStatNamingARoleItsDesignLacks could be passing because Upsert refuses every
    // role-bearing stat, not because it distinguishes declared from undeclared.
    [Fact]
    public void UpsertAcceptsAStatNamingADeclaredRole()
    {
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        var lensed = new GearData
        {
            Name = "Lensed Lamp", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 10,
            Roles = { new ItemRole { Name = "lens" } },
            Behaviors =
            {
                new EnergyDrawData
                {
                    EnergyDraw = new PerformanceStat
                    {
                        Min = 0, Max = 1,
                        Terms = { new StatTerm { Source = StatSource.Quality, Exponent = 1, Role = "lens" } }
                    }
                }
            }
        };
        cache.Upsert(lensed); // must not throw
    }

    // F4/Cut 7: "a market segment is a second product with a role raised" -- two products of the same design,
    // differing only in one role's Mean, must mint lots that resolve a role-termed stat to different values.
    // Zero StandardDeviation on both keeps the roll deterministic, so this cannot pass by coincidence.
    [Fact]
    public void TwoProductsOfOneDesignWithDifferentRoleMeansResolveDifferentValues()
    {
        using var cache = AetheriaStores.Open(Catalog, catalogWritable: true);
        var makerRef = cache.Upsert(new Faction { Name = "Maker", ShortName = "MKR" });
        var lensGear = cache.Upsert(new GearData
        {
            Name = "Lensed", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 10,
            Roles = { new ItemRole { Name = "lens" } }
        });
        var cheapProduct = cache.Upsert(new FactionProductData
        {
            Name = "Lensed Economy", Design = new CultRecordRef<CraftedItemData>(lensGear.Key), Manufacturer = makerRef,
            Roles = { new ProductRole { Role = "lens", Mean = .3f, StandardDeviation = 0 } }
        });
        var premiumProduct = cache.Upsert(new FactionProductData
        {
            Name = "Lensed Premium", Design = new CultRecordRef<CraftedItemData>(lensGear.Key), Manufacturer = makerRef,
            Roles = { new ProductRole { Role = "lens", Mean = .9f, StandardDeviation = 0 } }
        });
        cache.FlushAsync().Wait();

        var items = new ItemManager(cache, new ProvenanceLedger(), Settings(), _ => { });
        var cheapLot = items.CreateLot(cache.GetAll<FactionProductData>().Single(p => p.Name == "Lensed Economy"));
        var premiumLot = items.CreateLot(cache.GetAll<FactionProductData>().Single(p => p.Name == "Lensed Premium"));
        var cheapInstance = (EquippableItem) items.CreateInstance(cheapLot);
        var premiumInstance = (EquippableItem) items.CreateInstance(premiumLot);

        var stat = new PerformanceStat { Min = 0, Max = 1, Terms = { new StatTerm { Source = StatSource.Quality, Exponent = 1, Role = "lens" } } };
        var cheapValue = items.Evaluate(stat, cheapInstance);
        var premiumValue = items.Evaluate(stat, premiumInstance);

        Assert.Equal(.3f, cheapValue, 3);
        Assert.Equal(.9f, premiumValue, 3);
        Assert.NotEqual(cheapValue, premiumValue);
    }
}

/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using Xunit;
using static CultMath.math;
using float3 = CultMath.float3;
using int2 = CultMath.int2;
using Random = CultMath.Random;

// Cut 7 (docs/fire-control-cut.md): 7.1, the fifth mutation Soul found survived 197 green tests --
// FireControl.Fire freezing the raw TargetItem.Value instead of the reveal-re-checked ResolvedTargetItem.
// Builds its own shooter/target fixture, the same shape FireAuthorityTests uses for Cut 3, rather than
// reusing that file's private Build helper (the established convention in this campaign's test files).
public sealed class FireControlCut7Tests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-firecontrolcut7-" + Guid.NewGuid().ToString("N"));
    private string Catalog => Path.Combine(_root, "Aetheria.cc");

    public FireControlCut7Tests() => Directory.CreateDirectory(_root);

    private readonly List<CultCache> _openCaches = new List<CultCache>();

    public void Dispose()
    {
        foreach (var c in _openCaches) c.Dispose();
        Directory.Delete(_root, true);
    }

    // Detection well below the armor tier, which is well below the gear tier, so an aimed interior item can
    // decay out of its own reveal while the weapon itself (index 0, the armor tier) and general detection
    // both stay satisfied -- the shot still fires, only the aim point should be lost.
    private static GameplaySettings TestSettings() => new GameplaySettings
    {
        DefaultEntitySettings = new EntitySettings(),
        Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
        QualityPriceModifier = new ExponentialLerp(),
        TargetDetectionInfoThreshold = .1f,
        TargetArmorInfoThreshold = .3f,
        TargetGearInfoThreshold = .9f,
        FiringArc = 170,
        CommitHorizon = .5f,
        SchematicCellSize = 1f,
        UnaidedAccuracy = .05f,
        AgentMinHitProbability = 0f
    };

    private static PerformanceStat Constant(float v) => new PerformanceStat { Min = v, Max = v };

    private sealed class Engagement
    {
        public ItemManager Items;
        public Zone Zone;
        public Ship Shooter;
        public Ship Target;
        public EquippedItem WeaponItem;
        public InstantWeapon Weapon;
    }

    // A trimmed copy of FireAuthorityTests.Build: one Sensors-hardpoint weapon plus a Tool-slot targeting
    // system on the shooter, a target ship with a matching weapon (index 0 in IsRevealed's ranking) and,
    // through beforeActivate, a second Tool item to aim at (index 1 -- the gear tier).
    private Engagement Build(GameplaySettings settings, Action<ItemManager, Ship, Ship> beforeActivate)
    {
        var hullShape = new Shape(5, 5);
        foreach (var cell in hullShape.AllCoordinates) hullShape[cell] = true;
        var hullData = new HullData
        {
            Name = "Hull", HullType = HullType.Ship, Shape = hullShape, Durability = 1000, Mass = 1000,
            Hardpoints = { new HardpointData { Type = HardpointType.Sensors, Position = new int2(0, 0), Shape = new Shape() } }
        };

        var cache = AetheriaStores.Open(Catalog + Guid.NewGuid().ToString("N"), catalogWritable: true);
        _openCaches.Add(cache);
        cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
        cache.Upsert(hullData);
        cache.Upsert(new GearData
        {
            Name = "Gun", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new InstantWeaponData
            {
                Damage = Constant(50), Range = Constant(1000), MinRange = Constant(0),
                Velocity = Constant(0), Spread = Constant(0), DamageSpread = Constant(0),
                Penetration = Constant(0), Count = Constant(1), BurstTime = Constant(0), Cooldown = Constant(1000),
                DamageCurve = new BezierCurve { Keys = new[] { float4(0, 1, 0, 0), float4(1, 1, 0, 0) } }
            } }
        });
        cache.Upsert(new GearData
        {
            Name = "Targeting", Hardpoint = HardpointType.Tool, Shape = new Shape(), Durability = 1,
            MinimumTemperature = -1000, MaximumTemperature = 1000, OptimalTemperature = 0, PlateauWidth = 2000,
            Behaviors = { new TargetingSystemData
            {
                Accuracy = Constant(1), Resolution = Constant(1), Precision = Constant(1), Tracking = Constant(1000)
            } }
        });
        cache.FlushAsync().Wait();

        var ledger = new ProvenanceLedger();
        for (var i = 1; i <= 10; i++) ledger.Lots[i] = new Lot { Origin = new Attributed(), Quality = 1 };
        var items = new ItemManager(cache, ledger, settings, _ => { });
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);

        var hullRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<HullData>("Hull"));
        var shooterHull = new EquippableItem { Data = hullRef, Durability = 1000, Lot = 1 };
        var shooter = new Ship(items, zone, shooterHull, new EntitySettings());

        var gunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var gun = new EquippableItem { Data = gunRef, Durability = 1, Lot = 2 };
        Assert.True(shooter.TryEquip(gun, new int2(0, 0)));
        var weaponItem = shooter.Equipment.Single(x => x.EquippableItem == gun);
        var weapon = (InstantWeapon) weaponItem.Behaviors.Single(b => b is Weapon);

        var targetingRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Targeting"));
        var targeting = new EquippableItem { Data = targetingRef, Durability = 1, Lot = 3 };
        Assert.True(shooter.TryFindSpace(targeting, out var tpos));
        Assert.True(shooter.TryEquip(targeting, tpos));

        var targetHull = new EquippableItem { Data = hullRef, Durability = 1000, Lot = 4 };
        var target = new Ship(items, zone, targetHull, new EntitySettings());
        var targetGunRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Gun"));
        var targetGun = new EquippableItem { Data = targetGunRef, Durability = 1, Lot = 5 };
        Assert.True(target.TryEquip(targetGun, new int2(0, 0)));

        beforeActivate?.Invoke(items, shooter, target);

        zone.Entities.Add(shooter);
        zone.Entities.Add(target);
        shooter.Activate();
        target.Activate();

        shooter.Position = float3.zero;
        target.Position = float3(0, 0, 100);
        shooter.Target.Value = target;
        shooter.SetIff(target, true);

        zone.Update(0f); // warm-up: resolves weapon/targeting stats before Fire reads them

        return new Engagement { Items = items, Zone = zone, Shooter = shooter, Target = target, WeaponItem = weaponItem, Weapon = weapon };
    }

    // 7.1 (Soul's fifth survivor): FireControl.Fire must freeze source.ResolvedTargetItem, not the raw
    // source.TargetItem.Value -- the aim point is re-checked against reveal at the instant the trigger is
    // pulled, same as every other read of it. Mutation: read TargetItem.Value directly. Must die.
    //
    // The target's non-hull equipment ranks the hardpoint weapon first (armor tier, index 0 of 2) and the
    // interior "Targeting" item last (gear tier, index 1 of 2, i.e. exactly TargetGearInfoThreshold). Info is
    // dropped to a level between the two tiers: the weapon stays revealed (so the shot still fires, PBase >
    // 0) but the aimed item's own tier is no longer met -- the reveal re-check, not a global blackout, is
    // what must drop the aim point.
    [Fact]
    public void AimedItemDropsOutWhenRevealDecaysBeforeFire()
    {
        EquippableItem aimedGear = null;
        var settings = TestSettings();
        var e = Build(settings, (items, shooter, target) =>
        {
            var targetingRef = items.ItemData.RefOf<ItemData>(items.ItemData.GetByName<GearData>("Targeting"));
            aimedGear = new EquippableItem { Data = targetingRef, Durability = 1, Lot = 6 };
            Assert.True(target.TryFindSpace(aimedGear, out var pos));
            Assert.True(target.TryEquip(aimedGear, pos));
        });
        var aimedItem = e.Target.Equipment.Single(x => x.EquippableItem == aimedGear);

        // Fully reveal and select the interior item as the aim point.
        e.Shooter.EntityInfoGathered[e.Target] = 1f;
        Assert.True(e.Shooter.TrySelectTargetItem(aimedItem));
        Assert.Same(aimedItem, e.Shooter.ResolvedTargetItem);

        // Decay to halfway between the armor and gear tiers: the weapon (armor tier) stays revealed, the
        // aimed item (gear tier) does not. The raw field still holds the item -- nothing clears it -- but the
        // read accessor must now return null.
        e.Shooter.EntityInfoGathered[e.Target] = (settings.TargetArmorInfoThreshold + settings.TargetGearInfoThreshold) / 2f;
        Assert.Same(aimedItem, e.Shooter.TargetItem.Value); // raw field: untouched by decay
        Assert.Null(e.Shooter.ResolvedTargetItem); // read path: re-checked, and drops it

        var shotId = FireControl.Fire(e.Weapon, e.WeaponItem, e.Shooter);
        var shot = e.Zone.PendingShots.Single(s => s.ShotId == shotId);

        Assert.True(shot.PBase > 0f); // the shot is still worth taking -- this isn't a blackout, just a lost aim point
        Assert.Null(shot.Aimed); // the frozen snapshot must not carry an aim point reveal no longer supports
    }

    // 7.3/7.4 (docs/fire-control-cut.md): the campaign's only assertion that the shipped catalog is
    // readable by the shipped code at all, not only by a synthetic fixture. Cut 2 shipped a targeting-system
    // requirement with no data to satisfy it and every green synthetic-fixture suite missed it; merging Cuts
    // 6b and 6c separately shipped a non-nullable field no existing record carried, and neither cut's own
    // synthetic-fixture tests ever opened real data, so AetheriaStores.Open throwing outright on the shipped
    // catalog (fixed at c896489b) passed every gate until an operator opened it by hand. Any exception here
    // -- deserialization or generation -- is a failure; this is not scoped to "did a design exist."
    [Fact]
    public void ShippedCatalogOpensAndGeneratesAnArmedHull()
    {
        var gameData = Path.Combine(FindRepoRoot(), "GameData", "Aetheria.cc");
        using var cache = OpenReadOnlyRealCatalog(gameData);

        // Force every EquippableItemData -- WeaponItemData included, the type the broken merge could not
        // read -- to actually be enumerated. AetheriaStores.Open already deserializes every record eagerly
        // at attach time (CultCache.PullAll), so this line is what turns that into a promise this test
        // enforces rather than an accident of the current backing-store implementation: a future lazy-read
        // change that skipped WeaponItemData specifically would still be caught here.
        var equippable = cache.GetAll<EquippableItemData>().ToList();
        Assert.NotEmpty(equippable);
        Assert.Contains(equippable, e => e is WeaponItemData);

        var hull = cache.GetByName<HullData>("LonginusX");
        Assert.NotNull(hull);

        var settings = new GameplaySettings
        {
            DefaultEntitySettings = new EntitySettings(),
            Tiers = new[] { new RarityTier { Name = "Common", Quality = .5f, Rarity = 0, Color = new float3(1, 1, 1) } },
            QualityPriceModifier = new ExponentialLerp()
        };
        var log = new List<string>();
        var items = new ItemManager(cache, new ProvenanceLedger(), settings, log.Add);
        var random = new Random(1u);
        var generator = new LoadoutGenerator(ref random, items, null, null, null, .5f);
        var pack = generator.GenerateShipLoadout(candidate => candidate == hull);

        Assert.NotNull(pack); // Cut 2's own mutation (drop the targeting designs' products): this goes null
        Assert.Contains(pack.Equipment, e => (cache.Get(e.item.Data) as GearData)?.Behaviors.Any(b => b is TargetingSystemData) ?? false);
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Aetheria.Shared", "Aetheria.Shared.csproj")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Run from inside the Aetheria repository -- built test output was not under a recognizable repo root.");
    }

    // AetheriaStores.Open (Assets/Scripts/ServerShared/AetheriaStores.cs) uses `new CultCache()`, whose
    // default registry (CultDocumentRegistry.Shared) auto-discovers every [CultDocument] type loaded
    // ANYWHERE in the current process by scanning the whole AppDomain -- including this very test
    // assembly's own TestCatalogGlobal (AetheriaStoresTests.cs: [CultGlobal], routed to the catalog through
    // PersonalityAttribute). The shipped catalog naturally carries no record of a test-only type it has
    // never heard of, so opening it through the shared, process-wide registry throws "no
    // aetheria.tests.catalogglobal record" -- a same-process, cross-assembly artifact of this test suite,
    // not something the real game process (which never loads TestCatalogGlobal) can ever hit.
    //
    // This reproduces AetheriaStores.Open's own composition and validation exactly, scoped to a registry
    // built only from the shipped assembly's own [CultDocument] types (CultDocumentRegistry.ForTypes does
    // not re-scan the AppDomain the way the default constructor's Refresh() does), so this test still opens
    // the catalog the same way the game does -- same backing store, same missing-global check, same R-heat
    // validation loop -- without the test assembly's own fixture type poisoning it.
    private static CultCache OpenReadOnlyRealCatalog(string catalogPath)
    {
        var registry = CultDocumentRegistry.ForTypes(typeof(ItemData).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.GetCustomAttribute<CultDocumentAttribute>() != null));
        var cache = new CultCache(registry);
        try
        {
            cache.AddBackingStore(new SingleFileMessagePackBackingStore(catalogPath, true), AetheriaStores.CatalogTypes);
            var missing = cache.Registry.AllDescriptors.FirstOrDefault(descriptor =>
                descriptor.IsGlobal &&
                AetheriaStores.CatalogTypes.Any(home => home.IsAssignableFrom(descriptor.DocumentType)) &&
                cache.AllStoredDocuments.All(stored => stored.Descriptor != descriptor));
            if (missing != null)
                throw new InvalidOperationException($"Catalog {catalogPath} has no {missing.SchemaName} record; catalog globals are authored, never invented.");
            foreach (var data in cache.GetAll<EquippableItemData>())
            {
                StatValidation.ValidateHeatResponse(data);
                StatValidation.ValidateStatModifiers(data.Name, data.Behaviors);
                StatValidation.ValidateRoleUsage(data.Name, data.Roles, data.Behaviors);
            }
            foreach (var data in cache.GetAll<ConsumableItemData>())
            {
                StatValidation.ValidateStatModifiers(data.Name, data.Behaviors);
                StatValidation.ValidateRoleUsage(data.Name, data.Roles, data.Behaviors);
            }
            return cache;
        }
        catch
        {
            cache.Dispose();
            throw;
        }
    }
}

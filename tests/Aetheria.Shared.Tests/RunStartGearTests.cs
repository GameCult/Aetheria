/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using Xunit;

// Gear does not need its maker present: LoadoutGenerator.IsAvailable reaches by allegiance alone, and an absent
// maker only weighs less (operator, 2026-10-06: a faction need not be present in the galaxy for its gear to be
// available). Every galaxy reaches the same way, the prelude included.
public sealed partial class RunStartTests
{
    // A maker the galaxy does not carry, with one product made from a real design, and a generator for a fresh
    // faction whose allegiance names exactly the makers given (the absent one among them, or not).
    private (FactionProductData product, Faction absent) AbsentMaker(string name)
    {
        var absent = new Faction { Name = name, ShortName = name };
        _cache.Upsert(absent);
        var design = _cache.GetAll<FactionProductData>().First(p => p.Manufacturer.IsSet()).Design;
        var product = new FactionProductData { Name = name + " Product", Design = design, Manufacturer = _cache.RefOf(absent) };
        _cache.Upsert(product);
        return (product, absent);
    }

    private LoadoutGenerator GeneratorFor(Galaxy galaxy, string name, params Faction[] allies)
    {
        var faction = new Faction { Name = name, ShortName = name };
        _cache.Upsert(faction);
        foreach (var ally in allies) faction.Allegiance[_cache.RefOf(ally)] = 1;
        return new LoadoutGenerator(ref _items.Random, _items, galaxy, galaxy.Entrance, faction, .5f);
    }

    [Fact]
    public void AnAbsentMakersGearIsAvailableThroughAllegiance()
    {
        var galaxy = MainSectorGalaxy();
        var (product, absent) = AbsentMaker("AbsentAlly");
        Assert.DoesNotContain(galaxy.Factions, f => f == absent);
        Assert.True(GeneratorFor(galaxy, "Friend", absent).IsAvailable(product));
    }

    [Fact]
    public void AnAbsentMakerOutsideAllegianceIsNot()
    {
        var galaxy = MainSectorGalaxy();
        var (product, _) = AbsentMaker("AbsentStranger");
        Assert.False(GeneratorFor(galaxy, "Loner").IsAvailable(product));
    }

    [Fact]
    public void AFactionlessGeneratorReachesEveryMaker()
    {
        var galaxy = MainSectorGalaxy();
        AbsentMaker("AbsentToAll");
        var generator = new LoadoutGenerator(ref _items.Random, _items, galaxy, galaxy.Entrance, null, .5f);
        var products = _cache.GetAll<FactionProductData>().Where(p => p.Manufacturer.IsSet()).ToArray();
        Assert.NotEmpty(products);
        Assert.All(products, product => Assert.True(generator.IsAvailable(product), product.Name));
    }

    [Fact]
    public void AnAbsentMakerWeighsAsTheFarthest()
    {
        var galaxy = MainSectorGalaxy();
        var (product, absent) = AbsentMaker("AbsentFar");
        var zone = galaxy.Entrance;
        var farthest = galaxy.Factions
            .Where(f => galaxy.HomeZones.TryGetValue(f, out var home) && zone.Distance.ContainsKey(home))
            .OrderByDescending(f => zone.Distance[galaxy.HomeZones[f]]).First();
        var generator = GeneratorFor(galaxy, "Trader", absent, farthest);
        var absentWeight = generator.ManufacturerPreference(product.Manufacturer);
        var farthestWeight = generator.ManufacturerPreference(_cache.RefOf(farthest));
        Assert.True(absentWeight > 0, "an absent ally is reachable");
        Assert.True(absentWeight < farthestWeight, $"absent {absentWeight} must weigh below the farthest present maker {farthestWeight}");
        Assert.Equal(1f / (2 + zone.Distance.Values.Max()), absentWeight);
    }

    [Fact]
    public void APreludeNoLongerOffersEverything()
    {
        Assert.True(_galaxy.IsPrelude);
        var (absentProduct, _) = AbsentMaker("AbsentToPrelude");
        var generator = GeneratorFor(_galaxy, "PreludeLoner");
        Assert.False(generator.IsAvailable(absentProduct));
        var present = _cache.GetAll<FactionProductData>().Where(p => p.Manufacturer.IsSet() &&
            _galaxy.Factions.Any(f => _cache.RefOf(f).Key.Equals(p.Manufacturer.Key))).ToArray();
        Assert.NotEmpty(present);
        Assert.All(present, product => Assert.False(generator.IsAvailable(product), product.Name));
    }
}

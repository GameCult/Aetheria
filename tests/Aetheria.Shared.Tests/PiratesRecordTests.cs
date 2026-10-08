/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using Xunit;

// The Pirates record (aetheria-release, cut pirates-record): the shipped catalog's faction of rebrands, named in emoji.
public sealed partial class RunStartTests
{
    private Faction ShippedPirates() => _cache.GetAll<Faction>().Single(faction => faction.Name == "Pirates");

    private FactionProductData[] PiratesProducts()
    {
        var pirates = _cache.RefOf(ShippedPirates());
        return _cache.GetAll<FactionProductData>().Where(product => product.Manufacturer.Equals(pirates)).ToArray();
    }

    // The demo's allied faction exists as catalog data with eight generatable rebrands of other makers' designs.
    [Fact]
    public void PiratesAreInTheShippedCatalog()
    {
        var pirates = ShippedPirates();
        var others = _cache.GetAll<Faction>().Where(faction => faction != pirates).Select(faction => _cache.RefOf(faction)).ToArray();
        Assert.NotEmpty(others);
        Assert.All(others, other => Assert.True(pirates.Allegiance.ContainsKey(other)));
        Assert.NotNull(_cache.Get(pirates.GeonameFile));

        var products = PiratesProducts();
        Assert.Equal(8, products.Length);
        var hulls = _cache.GetAll<HullData>().ToArray();
        foreach (var product in products)
        {
            var design = _cache.Get(product.Design) as EquippableItemData;
            Assert.NotNull(design);
            Assert.True(LoadoutGenerator.HasHome(design, hulls), $"{design.Name} has no home");
            Assert.True(design.Price > 0, $"{design.Name} has no price");
            Assert.Contains(_cache.GetAll<FactionProductData>(), other =>
                other.Design.Key.Equals(product.Design.Key) && !other.Manufacturer.Equals(product.Manufacturer));
        }
    }

    // The Pirates' products are named in emoji and nothing else: no ASCII letter or digit survives in a name.
    [Fact]
    public void PiratesProductsAreNamedInEmoji()
    {
        foreach (var product in PiratesProducts())
        {
            Assert.False(string.IsNullOrEmpty(product.Name));
            Assert.DoesNotContain(product.Name, character => character < 128 && char.IsLetterOrDigit(character));
            Assert.Contains(product.Name.EnumerateRunes(), rune => rune.Value >= 0x1F000);
        }
    }

    // Brand finds a lot's product by (maker, design), so a maker may sell one product per design.
    [Fact]
    public void NoMakerSellsTwoProductsOfOneDesign()
    {
        var twice = _cache.GetAll<FactionProductData>()
            .GroupBy(product => (Maker: product.Manufacturer.Key.Value, Design: product.Design.Key.Value))
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(", ", group.Select(product => product.Name)))
            .ToArray();
        Assert.Empty(twice);
    }
}

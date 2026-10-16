using System;
using System.Collections.Generic;
using System.IO;
using CultMath;
using GameCult.Caching;
using Xunit;

// Items are titled by their product (ruling catalog-grows-generic-designs-branded-products): ItemManager.Title reads
// Brand, so the lot's own product names the unit (a maker may sell several products of one design), and nothing
// else carries a name.
public sealed class ItemTitleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aetheria-itemtitle-" + Guid.NewGuid().ToString("N"));
    private readonly CultCache _cache;
    private readonly ItemManager _items;
    private readonly FactionProductData _panopticon, _zulu, _clapBack, _spiceBlend;
    private readonly CultRecordRef<Faction> _finch;
    private readonly CultRecordRef<Faction> _stranger;
    private readonly CompoundCommodityData _spice;
    private readonly GearData _array;

    public ItemTitleTests()
    {
        Directory.CreateDirectory(_root);
        _cache = AetheriaStores.Open(Path.Combine(_root, "Aetheria.cc"), catalogWritable: true);
        _cache.Upsert(new VerseGrammar { Revision = 1 });
        _array = new GearData { Name = "Array", Hardpoint = HardpointType.Sensors, Shape = new Shape(), Price = 10, Durability = 5 };
        _cache.Upsert(_array);
        _cache.Upsert(new SimpleCommodityData { Name = "Ore", Shape = new Shape(), Price = 7, MaxStack = 100 });
        var finch = _finch = _cache.Upsert(new Faction { Name = "Finch", ShortName = "FIN" });
        var lucent = _cache.Upsert(new Faction { Name = "Lucent", ShortName = "LUC" });
        _stranger = _cache.Upsert(new Faction { Name = "Stranger", ShortName = "STR" });
        _spice = new CompoundCommodityData { Name = "Spice", Shape = new Shape(), Price = 3 };
        _cache.Upsert(_spice);
        var design = _cache.RefOf<CraftedItemData>(_cache.GetByName<GearData>("Array"));
        _panopticon = new FactionProductData { Name = "Panopticon Prime", Design = design, Manufacturer = finch };
        // Finch's second Array product, keyed to sort after the first: a key-order lookup would hand its units the first.
        _zulu = new FactionProductData { Name = "Zulu Prime", Design = design, Manufacturer = finch };
        _cache.Commit(batch => batch.Upsert(typeof(FactionProductData), _zulu, new CultRecordKey("array-finch-zulu")));
        _clapBack = new FactionProductData { Name = "ClapBack Ultra", Design = design, Manufacturer = lucent };
        _cache.Commit(batch => batch.Upsert(typeof(FactionProductData), _panopticon, new CultRecordKey("array-finch")));
        _cache.Commit(batch => batch.Upsert(typeof(FactionProductData), _clapBack, new CultRecordKey("array-lucent")));
        _spiceBlend = new FactionProductData { Name = "Finch Reserve", Design = _cache.RefOf<CraftedItemData>(_spice), Manufacturer = finch };
        _cache.Commit(batch => batch.Upsert(typeof(FactionProductData), _spiceBlend, new CultRecordKey("spice-finch")));
        _items = new ItemManager(_cache, new ProvenanceLedger(), RunSaveTests.TestSettings(), _ => { });
    }

    public void Dispose()
    {
        _cache.Dispose();
        Directory.Delete(_root, true);
    }

    [Fact]
    public void A_product_unit_is_titled_by_its_product()
    {
        var unit = _items.CreateInstance(_items.CreateLot(_panopticon));
        Assert.Equal("Panopticon Prime", _items.Title(unit));
        Assert.NotEqual("Panopticon Prime", _items.GetData(unit).Name);
    }

    [Fact]
    public void Two_makers_title_their_own_units()
    {
        var finch = _items.CreateInstance(_items.CreateLot(_panopticon));
        var lucent = _items.CreateInstance(_items.CreateLot(_clapBack));
        Assert.Equal(_items.GetData(finch).Name, _items.GetData(lucent).Name);
        Assert.Equal("Panopticon Prime", _items.Title(finch));
        Assert.Equal("ClapBack Ultra", _items.Title(lucent));
    }

    [Fact]
    public void A_makers_second_product_titles_and_brands_as_itself()
    {
        var zulu = (CraftedItemInstance) _items.CreateInstance(_items.CreateLot(_zulu));
        Assert.Equal("Zulu Prime", _items.Title(zulu));
        var (maker, product) = _items.Brand(zulu);
        Assert.Equal("Finch", maker.Name);
        Assert.Equal("Zulu Prime", product.Name);

        var panopticon = _items.CreateInstance(_items.CreateLot(_panopticon));
        Assert.Equal("Panopticon Prime", _items.Title(panopticon));
    }

    [Fact]
    public void A_lot_without_a_product_brands_its_maker_only()
    {
        var unit = (CraftedItemInstance) _items.CreateInstance(_items.CreateLot(_array, _finch, .5f));
        var (maker, product) = _items.Brand(unit);
        Assert.Equal("Finch", maker.Name);
        Assert.Null(product);
        Assert.Equal("Array", _items.Title(unit));
    }

    [Fact]
    public void A_lot_whose_product_left_the_catalog_brands_its_maker_only()
    {
        var lot = _items.CreateLot(_panopticon);
        _items.Lots[lot].Product = new CultRecordRef<FactionProductData>(new CultRecordKey("removed-product"));
        var unit = (CraftedItemInstance) _items.CreateInstance(lot);
        var (maker, product) = _items.Brand(unit);
        Assert.Equal("Finch", maker.Name);
        Assert.Null(product);
        Assert.Equal("Array", _items.Title(unit));
    }

    [Fact]
    public void Unbranded_units_are_titled_by_design()
    {
        var given = _items.CreateInstance(_items.CreateLot(_array, default, .5f));
        Assert.Equal("Array", _items.Title(given));

        var extractedLot = _items.Lots.Add(new Lot
        {
            Design = _cache.RefOf<ItemData>(_array), Origin = new Extracted(), Quality = .5f, Roles = new List<RoleFill>()
        });
        Assert.Equal("Array", _items.Title(_items.CreateInstance(extractedLot)));

        var ore = new SimpleCommodity { Data = _cache.RefOf<ItemData>(_cache.GetByName<SimpleCommodityData>("Ore")), Quantity = 3 };
        Assert.Equal("Ore", _items.Title(ore));
    }

    [Fact]
    public void A_maker_without_a_product_of_the_design_is_titled_by_design()
    {
        var unit = _items.CreateInstance(_items.CreateLot(_array, _stranger, .5f));
        Assert.Equal("Stranger", _items.Brand(unit).Maker.Name);
        Assert.Equal("Array", _items.Title(unit));
    }

    [Fact]
    public void A_crafted_commodity_with_a_product_is_titled_by_its_product()
    {
        var unit = _items.CreateInstance(_items.CreateLot(_spiceBlend));
        Assert.IsType<CompoundCommodity>(unit);
        Assert.Equal("Finch Reserve", _items.Title(unit));
    }
}

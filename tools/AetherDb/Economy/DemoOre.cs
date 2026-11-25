using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;

// Interim ore sheet (ruling mining-demo-ore-minimal-yields-now): the Asteroid field kind yields Regolith, Nickel-Iron and Gold, and those
// three can be held (a 1x1 shape, a mass) and are sold (a price). The economy ores batch may rewrite the yields by element id.
// Prices are ruling regolith-demo-retune (it superseded mining-demo-ore-prices-gear-scale), gear-scale so the mine, sell, repair loop closes: a retune
// is an edit here and a rerun. When the ores batch lands with the 100x gear rescale its prices take over (aetheria-living-world ruling
// ore-batch-keeps-prices-gear-rescaled) and this sheet retires.
// Ruling regolith-demo-retune (rock-becomes-regolith): Rock becomes Regolith at price 1 and New Metals becomes Nickel-Iron; yield shifts toward
// Nickel-Iron so a full hold sells for about what it did (expected 900.5 a unit against 920). The names and descriptions are the ores batch's.
// Munitions and MunitionsFeed never rewrite these three commodities (MunitionsFeed finds the two renamed ores by the keys above), so rerunning them does not clobber this sheet.
public sealed class DemoOre : EconomyBatch
{
    private const int RegolithPrice = 1;
    private const int NickelIronPrice = 1000;
    private const int GoldPrice = 5000;

    // The two renamed records are found by key, so a rerun converges after the rename (the ores batch finds them the same way).
    public static readonly CultRecordKey RegolithKey = new CultRecordKey("043997a8-0661-4206-ab8a-10215ecf4efc");
    public static readonly CultRecordKey NickelIronKey = new CultRecordKey("00b5b3b2-2fc5-4adb-a1d7-c1a35d19afd3");

    // Authored element ids (the cache takes a hand-written id only as 12 lowercase hex): asteroid regolith, nickel-iron, gold. The ids keep
    // their first names; the ores batch rewrites yields by element id.
    private const string RockId = "a57e0c000001";
    private const string NewMetalsId = "a57e0c000002";
    private const string GoldId = "a57e0c000003";

    public override void Author(EconomyAuthor author)
    {
        Rename(author, RegolithKey, "Regolith", "Crushed silicate rubble and dust, the bulk of every belt: what is left when the ore is picked out, and the aggregate cast habitats and berms are made of.", RegolithPrice, null);
        Rename(author, NickelIronKey, "Nickel-Iron", "Native nickel-iron, kamacite and taenite, cut from metallic asteroids: about nine parts iron to one of nickel, with a trace of cobalt and platinum.", NickelIronPrice, 2);
        author.Edit<SimpleCommodityData>("Gold", o => { o.Shape = new Shape(); o.Price = GoldPrice; });

        var kinds = author.Catalog.GetAll<FieldKindData>().Where(k => k.Name == "Asteroid").ToArray();
        if (kinds.Length != 1) throw new InvalidOperationException($"{kinds.Length} field kinds are named 'Asteroid'; the ore sheet needs exactly one.");
        var asteroid = kinds[0];
        asteroid.Yields = new List<FieldYield>
        {
            Yield(author, "Regolith", 5, RockId),
            Yield(author, "Nickel-Iron", 4, NewMetalsId),
            Yield(author, "Gold", 1, GoldId)
        };
        author.Put(asteroid, author.Catalog.RefOf(asteroid).Key);
    }

    private static void Rename(EconomyAuthor author, CultRecordKey key, string name, string description, int price, float? mass)
    {
        var record = author.Catalog.Get<SimpleCommodityData>(key) ?? throw new InvalidOperationException($"The catalog holds no simple commodity {key.Value}.");
        record.Name = name;
        record.Description = description;
        record.Price = price;
        record.Shape = new Shape();
        if (mass.HasValue) record.Mass = mass.Value;
        author.Put(record, key);
    }

    private static FieldYield Yield(EconomyAuthor author, string name, float abundance, string id)
    {
        var item = author.Catalog.GetAll<SimpleCommodityData>().Single(i => i.Name == name);
        return new FieldYield { Commodity = new CultRecordRef<SimpleCommodityData>(author.Catalog.RefOf(item).Key), Abundance = abundance, Regional = false, Id = id };
    }
}

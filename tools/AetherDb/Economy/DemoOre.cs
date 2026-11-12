using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;

// Interim ore sheet (ruling mining-demo-ore-minimal-yields-now): the Asteroid field kind yields Rock, New Metals and Gold, and those
// three can be held (a 1x1 shape, a mass) and are sold (a price). The economy ores batch may rewrite the yields by element id.
// Prices are ruling mining-demo-ore-prices-gear-scale, gear-scale so the mine, sell, repair loop closes: a retune is an edit here and a rerun.
// Munitions and MunitionsFeed never rewrite these three commodities, so rerunning them does not clobber this sheet.
public sealed class DemoOre : EconomyBatch
{
    private const int RockPrice = 200;
    private const int NewMetalsPrice = 1000;
    private const int GoldPrice = 5000;

    // Authored element ids (the cache takes a hand-written id only as 12 lowercase hex): asteroid-rock, asteroid-new-metals, asteroid-gold.
    private const string RockId = "a57e0c000001";
    private const string NewMetalsId = "a57e0c000002";
    private const string GoldId = "a57e0c000003";

    public override void Author(EconomyAuthor author)
    {
        author.Edit<SimpleCommodityData>("Rock", o => { o.Shape = new Shape(); o.Price = RockPrice; });
        author.Edit<SimpleCommodityData>("New Metals", o => { o.Shape = new Shape(); o.Price = NewMetalsPrice; o.Mass = 2; });
        author.Edit<SimpleCommodityData>("Gold", o => { o.Shape = new Shape(); o.Price = GoldPrice; });

        var kinds = author.Catalog.GetAll<FieldKindData>().Where(k => k.Name == "Asteroid").ToArray();
        if (kinds.Length != 1) throw new InvalidOperationException($"{kinds.Length} field kinds are named 'Asteroid'; the ore sheet needs exactly one.");
        var asteroid = kinds[0];
        asteroid.Yields = new List<FieldYield>
        {
            Yield(author, "Rock", 6, RockId),
            Yield(author, "New Metals", 3, NewMetalsId),
            Yield(author, "Gold", 1, GoldId)
        };
        author.Put(asteroid, author.Catalog.RefOf(asteroid).Key);
    }

    private static FieldYield Yield(EconomyAuthor author, string name, float abundance, string id)
    {
        var item = author.Catalog.GetAll<SimpleCommodityData>().Single(i => i.Name == name);
        return new FieldYield { Commodity = new CultRecordRef<SimpleCommodityData>(author.Catalog.RefOf(item).Key), Abundance = abundance, Regional = false, Id = id };
    }
}

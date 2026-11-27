using System.Linq;
using GameCult.Caching;

// One-shot (aetheria-living-world, cut commodity-shapes): gives every commodity stored with no Shape a one-cell Shape, put back by its
// concrete type. Writes only where Shape is null, so a rerun writes nothing. Deleted once applied.
public sealed class CommodityShapes : EconomyBatch
{
    public override void Author(EconomyAuthor author)
    {
        var catalog = author.Catalog;
        foreach (var item in catalog.GetAll<ItemData>().Where(Recipes.IsCommodity).Where(item => item.Shape == null).ToArray())
        {
            item.Shape = new Shape();
            var key = catalog.RefOf(item).Key;
            if (item is SimpleCommodityData simple) author.Put(simple, key);
            else author.Put((CompoundCommodityData) item, key);
        }
    }
}

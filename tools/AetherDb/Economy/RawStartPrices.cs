using System;
using GameCult.Caching;

// One-shot: gives each raw good still at Price 0 a start price, its labour at the going wage over its output count (at least 1).
// Price 0 on a raw good is unset, not free; after this the wage reads only authored raw prices, and author's reprice writes only made
// goods, which never enter the wage. Applied, then deleted.
public sealed class RawStartPrices : EconomyBatch
{
    public override void Author(EconomyAuthor author)
    {
        var wage = EconomyBalance.Wage(author.Catalog);
        foreach (var recipe in author.Catalog.GetAll<RecipeData>())
        {
            var item = author.Catalog.Get(recipe.Output);
            if (item == null || !Recipes.IsCommodity(item) || !Recipes.IsRaw(author.Catalog, recipe) || item.Price != 0) continue;
            author.SetPrice(recipe.Output.Key.Value, Math.Max(1, (int) Math.Round(wage * recipe.Workforce * recipe.Cycle / recipe.OutputCount, MidpointRounding.AwayFromZero)));
        }
    }
}

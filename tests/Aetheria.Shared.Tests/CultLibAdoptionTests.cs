using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;

// CultLib 2.0 refuses a union subtype no arm names (it used to write nil). These keep that refusal a build-time failure.
public sealed class CultLibAdoptionTests
{
    [Fact]
    public void EveryConcreteSubtypeOfAUnionBaseIsAnArmOfIt()
    {
        var types = typeof(BehaviorData).Assembly.GetTypes();
        var missing = new System.Collections.Generic.List<string>();
        foreach (var root in types)
        {
            var arms = root.GetCustomAttributes<UnionAttribute>(false).Select(u => u.SubType).ToHashSet();
            if (arms.Count == 0) continue;
            foreach (var subtype in types.Where(t => t != root && !t.IsAbstract && !t.IsInterface && root.IsAssignableFrom(t)))
                if (!arms.Contains(subtype)) missing.Add($"{subtype.Name} is not an arm of {root.Name}");
        }
        Assert.Empty(missing);
    }

    [Fact]
    public void NoShippedGearHoldsANullBehavior()
    {
        using var cache = new CultCache(TestCatalog.Registry());
        cache.AddBackingStore(new SingleFileMessagePackBackingStore(TestCatalog.Repo, true), AetheriaStores.CatalogTypes);
        var gear = cache.GetAll<GearData>().ToArray();
        Assert.NotEmpty(gear);
        var bad = gear.Where(g => g.Behaviors != null && g.Behaviors.Any(b => b == null)).Select(g => g.Name).ToArray();
        Assert.Empty(bad);
    }
}

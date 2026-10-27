using System;
using System.Linq;
using GameCult.Caching;

// TEMPORARY probe, removed before the final WIP commit.
public sealed class FactionProbe : EconomyBatch
{
    public override void Author(EconomyAuthor author)
    {
        var c = author.Catalog;
        var products = c.GetAll<FactionProductData>().ToArray();
        foreach (var f in c.GetAll<Faction>().OrderBy(f => f.Name))
        {
            var kinds = StationKinds.KindsOf(c, f).Select(k => c.Get(k)?.Name ?? k.Key.Value);
            var field = StationKinds.FieldableKindsOf(c, f).Select(k => c.Get(k)?.Name ?? k.Key.Value);
            var key = c.RefOf(f).Key;
            var hulls = products.Where(p => p.Manufacturer.Key.Equals(key)).Select(p => c.Get(p.Design) as HullData).Where(h => h != null && h.HullType == HullType.Station)
                .Select(h => $"{h.Name}[{(h.Kind.IsSet() ? c.Get(h.Kind)?.Name : "-")}]");
            Console.WriteLine($"PROBE faction '{f.Name}' infl={f.InfluenceDistance} products={products.Count(p => p.Manufacturer.Key.Equals(key))} stationHulls=({string.Join(", ", hulls)}) kindsOf=({string.Join(", ", kinds)}) fieldable=({string.Join(", ", field)})");
        }
        foreach (var k in c.GetAll<StationKindData>().OrderBy(k => k.Name))
            Console.WriteLine($"PROBE kind '{k.Name}' standIns=({string.Join(", ", k.StandIns.Select(s => (c.Get(s.Faction)?.Name ?? "?") + "->" + (c.Get(s.Kind)?.Name ?? "?")))})");
        foreach (var h in c.GetAll<HullData>().Where(h => h.HullType == HullType.Station).OrderBy(h => h.Name))
            Console.WriteLine($"PROBE hull '{h.Name}' kind={(h.Kind.IsSet() ? c.Get(h.Kind)?.Name : "-")} makers=({string.Join(", ", products.Where(p => p.Design.Key.Equals(c.RefOf(h).Key)).Select(p => c.Get(p.Manufacturer)?.Name))})");
    }
}

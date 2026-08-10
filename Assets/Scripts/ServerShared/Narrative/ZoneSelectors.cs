using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Random = Unity.Mathematics.Random;

public abstract class ZoneSelector
{
    public abstract GalaxyZone SelectZone(List<GalaxyZone> candidates);
}

public abstract class OrderedZoneSelector : ZoneSelector
{
    public bool Flip = false;
    
    protected abstract Func<GalaxyZone, IComparable> Comparison { get; }

    public override GalaxyZone SelectZone(List<GalaxyZone> candidates)
    {
        var sorted = candidates.OrderBy(Comparison);
        return Flip ? sorted.Last() : sorted.First();
    }
}

public class DistanceSelector : OrderedZoneSelector
{
    private GalaxyZone Target { get; }
    
    
    public DistanceSelector(string[] args, IZoneResolver zoneResolver)
    {
        Target = zoneResolver.ResolveZone(args[0].Trim());
    }

    protected override Func<GalaxyZone, IComparable> Comparison => zone => Target?.Distance[zone] ?? 0;
}

public class RandomSelector : ZoneSelector
{
    private Random Random { get; }
    
    public RandomSelector(ref Random random)
    {
        Random = random;
    }
    
    public override GalaxyZone SelectZone(List<GalaxyZone> candidates)
    {
        return candidates[Random.NextInt(candidates.Count)];
    }
}
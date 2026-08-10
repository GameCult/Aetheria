using System;
using System.Collections.Generic;
using System.Linq;

public interface IZoneResolver
{
    public GalaxyZone ResolveZone(string path);
}

public interface IFactionResolver
{
    public Faction ResolveFaction(string name);
}

public abstract class ZoneConstraint
{
    public bool Flip = false;
    
    public bool Test(GalaxyZone zone)
    {
        return Flip ^ TestZone(zone);
    }
    
    protected abstract bool TestZone(GalaxyZone zone);
}

public class FactionPresenceConstraint : ZoneConstraint
{
    private Faction TargetFaction { get; }
    public FactionPresenceConstraint(string[] args, IFactionResolver resolver)
    {
        TargetFaction = resolver.ResolveFaction(args[0]);
    }

    protected override bool TestZone(GalaxyZone zone)
    {
        return zone.Factions.Any(f=>f.ID == TargetFaction?.ID);
    }
}

public class FactionOwnerConstraint : ZoneConstraint
{
    private Faction TargetFaction { get; }
    
    public FactionOwnerConstraint(string[] args, IFactionResolver resolver)
    {
        TargetFaction = resolver.ResolveFaction(args[0]);
    }

    protected override bool TestZone(GalaxyZone zone)
    {
        return zone.Owner.ID == TargetFaction?.ID;
    }
}

public class DistanceConstraint : ZoneConstraint
{
    private GalaxyZone _targetZone;
    private Predicate<int> _test;
    
    public DistanceConstraint(string[] args, IZoneResolver zoneResolver)
    {
        if (args.Length == 3)
        {
            _targetZone = zoneResolver.ResolveZone(args[0].Trim());
            if (!int.TryParse(args[2], out var v)) _test = _ => false;
            else
            {
                var trimmed = args[1].Trim();
                _test = trimmed switch
                {
                    "<" => i => i < v,
                    ">" => i => i > v,
                    "=" => i => i == v,
                    _ => _ => false
                };
            }
        }
        else
        {
            _test = _ => false;
        }
    }

    protected override bool TestZone(GalaxyZone zone)
    {
        return _targetZone != null && _test(zone.Distance[_targetZone]);
    }
}
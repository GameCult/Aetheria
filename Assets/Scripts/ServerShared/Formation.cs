using System.Collections.Generic;
using System.Linq;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;

// A formation is a compiled group of ships a summon places into a running zone (ruling summons-follow-faction-logic): a
// script, a stable id and the faction it is cast as. It declares; ZoneStage.Summon is the one admission, and the stance of
// what arrives is the faction rule's and its grudges', so a formation writes none. The IFL palette calls the same Summon.
public abstract class Formation
{
    // The formation's stable identity: kebab-case, unique across Formations, never shown to the player. Name is display only.
    public abstract string Id { get; }

    public abstract string Name { get; }

    // A catalog faction the formation's ships are fitted for and belong to, resolved through Galaxy.ResolveFaction. Null is
    // factionless.
    public virtual string Faction => null;

    // Places the formation with the stage's verbs, around a point and facing a direction. The faction is the resolved one, null
    // when the formation is factionless; only a Generated fit carries it.
    public abstract void Place(ZoneStage stage, Faction faction, float2 at, float2 facing);

    // Summons the formation into a running zone, or returns null with every failure listed and the zone, item stream and
    // lot ledger unchanged. A faction the catalog does not hold refuses the summon: it would otherwise arrive factionless.
    public IReadOnlyList<Entity> Summon(ItemManager items, Zone zone, float2 at, float2 facing, List<string> failures)
    {
        Faction faction = null;
        if (Faction != null)
        {
            faction = zone.Galaxy.ResolveFaction(Faction);
            if (faction == null)
            {
                failures.Add($"formation {Id}: no faction named {Faction}");
                return null;
            }
        }
        return ZoneStage.Summon(items, zone, stage => Place(stage, faction, at, facing), failures);
    }
}

// Every formation, in the order the console lists them.
public static class Formations
{
    public static readonly Formation[] All =
    {
        new Squad("sparring-target", "Sparring Target", null, 1),
        new Squad("zhestokost-pair", "Zhestokost Pair", "Zhe", 2),
        new Squad("pirates-pair", "Pirates Pair", "Pirates", 2),
        new Squad("lucent-pair", "Lucent Media Pair", "Luc", 2),
        new Squad("aeronautics-pair", "Aeronautics Unlimited Pair", "Aero", 2)
    };

    public static Formation ById(string id) => All.FirstOrDefault(formation => formation.Id == id);

    // Piloted generated ships of any hull, side by side across the line of facing, 60 m apart, centred on the point.
    // Faction names are the demo cast's (DemoTerminus.Cast).
    private sealed class Squad : Formation
    {
        private const float Spacing = 60f;
        private readonly string _faction;
        private readonly int _ships;

        public Squad(string id, string name, string faction, int ships)
        {
            Id = id;
            Name = name;
            _faction = faction;
            _ships = ships;
        }

        public override string Id { get; }
        public override string Name { get; }
        public override string Faction => _faction;

        public override void Place(ZoneStage stage, Faction faction, float2 at, float2 facing)
        {
            var forward = lengthsq(facing) > 0 ? normalize(facing) : float2(0, 1);
            var across = float2(-forward.y, forward.x);
            for (var ship = 0; ship < _ships; ship++)
                stage.Place(stage.Generated("", faction), at + across * ((ship - (_ships - 1) / 2f) * Spacing), forward, piloted: true);
        }
    }
}

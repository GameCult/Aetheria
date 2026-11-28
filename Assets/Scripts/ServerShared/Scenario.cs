using System;
using System.Collections.Generic;

// A scenario is one way to set up a new run (docs/scenarios-cut.md, R): a script, compiled with the game, that says
// which galaxy to generate and what the run starts with. New Game lists them (Scenarios). RunStart runs one: it
// generates the galaxy through GalaxyStage, the arena under Ambient, and admits what Stage placed through a
// ScenarioStage. A scenario only declares; once staged, everything it placed is ordinary run state.
public abstract class Scenario
{
    public abstract string Name { get; }

    // The scenario's stable identity: kebab-case, unique across Scenarios, never shown to the player, recorded by
    // matches and replays. Name is display only.
    public abstract string Id { get; }

    // The conditions and what to verify in them, shown under the scenario's button.
    public abstract string Brief { get; }

    // Zero draws the seed from the clock, so each launch is a new galaxy. Anything else fixes the galaxy, its
    // background, its arena and every fit, each item's maker and workmanship included, so every launch has the same
    // layout.
    public virtual uint Seed => 0;

    // False: the arena keeps its planets, orbits and stations but generates no ship or turret, so nothing wanders into
    // the scenario's conditions.
    public virtual bool Ambient => true;

    // False: the scenario is a simulation. It starts through RunSave.Beside, so the saved run stays in the store and
    // stays resumable; it is never captured or committed, and its records are discarded when it ends. The one
    // declaration: nothing tests a scenario's type to decide saving.
    public virtual bool Saved => true;

    // The stations the arena's zone always gets, in slot order after its story stations, whatever its seed rolls
    // (ZoneGenerator). Empty: its stations are rolled like any other zone's.
    public virtual IReadOnlyList<EntranceStationOrder> EntranceStations => EntranceStationOrder.Any;

    // Which galaxy the run is in: stage.Main() or stage.Cast(cast).
    public abstract Galaxy Generate(GalaxyStage stage);

    // What the run starts with: exactly one stage.Player, and anything else the scenario places.
    public abstract void Stage(ScenarioStage stage);
}

// A run-start placement's stance toward the player. Hostile is written as an override both ways; Neutral writes nothing,
// so the faction rule and grudges decide.
public enum ScenarioStance
{
    Neutral,
    Hostile
}

// One station a scenario's entrance zone must hold. Faction is a cast name Galaxy.ResolveFaction resolves (null: the zone's
// structure faction); Kind is a station-kind id, StationKindData.KeyOf (null: StationKinds.Choose picks one).
public sealed class EntranceStationOrder
{
    public string Faction { get; }
    public string Kind { get; }

    public EntranceStationOrder(string faction, string kind) { Faction = faction; Kind = kind; }

    // One default order: the zone's structure faction, a chosen kind.
    public static readonly IReadOnlyList<EntranceStationOrder> Any = new[] { new EntranceStationOrder(null, null) };
    public static readonly IReadOnlyList<EntranceStationOrder> None = Array.Empty<EntranceStationOrder>();
}

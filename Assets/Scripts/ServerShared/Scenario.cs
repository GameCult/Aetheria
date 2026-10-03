// A scenario is one way to set up a new run (docs/scenarios-cut.md, R): a script, compiled with the game, that says
// which galaxy to generate and what the run starts with. New Game lists them (Scenarios). RunStart runs one: it
// generates the galaxy through GalaxyStage, the arena under Ambient, and admits what Stage placed through a
// ScenarioStage. A scenario only declares; once staged, everything it placed is ordinary run state.
public abstract class Scenario
{
    public abstract string Name { get; }

    // The conditions and what to verify in them, shown under the scenario's button.
    public abstract string Brief { get; }

    // Zero draws the seed from the clock, so each launch is a new galaxy. Anything else fixes the galaxy, its
    // background and its arena, so every launch has the same layout.
    public virtual uint Seed => 0;

    // False: the arena keeps its planets, orbits and stations but generates no ship or turret, so nothing wanders into
    // the scenario's conditions.
    public virtual bool Ambient => true;

    // Which galaxy the run is in: stage.Main() or stage.Prelude().
    public abstract Galaxy Generate(GalaxyStage stage);

    // What the run starts with: exactly one stage.Player, and anything else the scenario places.
    public abstract void Stage(ScenarioStage stage);
}

public enum ScenarioStance
{
    Neutral,
    Hostile
}

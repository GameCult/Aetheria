using System.Collections.Generic;
using GameCult.Caching;
using MessagePack;
using CultMath;

// A scenario is an arena whose conditions are present at spawn (docs/scenarios-cut.md): the player's ship and every
// other entity already fitted and placed. Authored catalog data, read at runtime; RunStart stages it into a new run.
// It sets conditions and nothing else: once staged, its entities are ordinary run state.
[CultDocument("aetheria.scenario", "1"), MessagePackObject]
public class Scenario
{
    [CultName, Key(0)] public string Name;
    [Key(1)] public string Brief;                  // the conditions in a line or two, shown in the menu
    [Key(2)] public uint Seed;                     // the arena galaxy derives from it, so every launch has one layout
    [Key(3)] public bool Ambient;                  // false: planets, orbits and stations, but no generated ship or turret
    [Key(4)] public ScenarioShip Player;
    [Key(5)] public List<ScenarioEntity> Entities = new List<ScenarioEntity>();
}

// A preset placed in the arena. A ship hull stages a Ship; a turret hull an orbital entity with no orbit, which stays
// where it is put.
[MessagePackObject]
public class ScenarioShip
{
    [Key(0)] public CultRecordRef<Loadout> Loadout;
    [Key(1)] public float2 Position;               // zone xz
    [Key(2)] public float2 Direction;              // zero keeps the entity's default heading
    [Key(3)] public List<CultRecordRef<EquippableItemData>> Cargo = new List<CultRecordRef<EquippableItemData>>(); // first cargo bay
}

[MessagePackObject]
public class ScenarioEntity : ScenarioShip
{
    [Key(4)] public ScenarioStance Stance;         // applied both ways between this entity and the player
    [Key(5)] public bool Piloted;                  // a Minion flies it; otherwise it has no agent and stays put
}

public enum ScenarioStance
{
    Neutral,
    Hostile
}

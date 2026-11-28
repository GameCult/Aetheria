using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using static CultMath.math;
using static ItemRotation;
using float2 = CultMath.float2;

// The demo: one gate, one region and one boss, with a fixed cast. The Pirates are the protagonist's ostensible
// employer, Zhestokost the antagonist whose home is the boss zone and the exit, Lucent Media and Aeronautics Unlimited
// the neutrals. A galaxy of that cast at half influence; the player, who is not a Pirate, flies the authored Longinus of
// Starter, the same fit on every seed (ruling demo-start-hull-longinus). Its entrance holds two stations on every seed: Aeronautics Unlimited's Zenith trade hub and the Pirates' refinery.
public sealed class DemoTerminus : Scenario
{
    public static readonly GalaxyCast Cast = new GalaxyCast
    {
        ProtagonistFaction = "Pirates",
        AntagonistFaction = "Zhe",
        BufferFaction = "Luc",
        NeutralFactions = new[] { "Aero" },
        QuestFaction = null,
        ZoneCount = 64,
        LinkDensity = .5f,
        InfluenceScale = .5f
    };

    // Zenith (ruling demo-zenith-ore-depot) and the Pirates' processing station (ruling demo-processing-station-buys-ore),
    // named from the cast so the cast and the orders cannot drift.
    public override IReadOnlyList<EntranceStationOrder> EntranceStations { get; } = new[]
    {
        new EntranceStationOrder(Cast.NeutralFactions[0], "trade-hub"),
        new EntranceStationOrder(Cast.ProtagonistFaction, "processing")
    };

    public override string Name => "Terminus (Demo)";
    public override string Id => "demo-terminus";
    public override string Brief => "One gate, one region, one boss. Allied: the Pirates. Opposed: Zhestokost. " +
                                    "Neutral: Lucent Media and Aeronautics Unlimited.";

    // The quality of every lot of the starter: a sim-tuned knob (economy-numbers-are-sim-tuned-knobs), repair scales with it.
    public const float StarterQuality = .5f;

    // The demo player's ship (ruling demo-start-hull-longinus, read by economy-is-never-faked: an authored fit, no random draw):
    // the Longinus with its Light Lasers on the two Energy mounts, since a Cell Blaster there is ammunition-fed and the starter
    // mines with its lasers. Everything else is the design the generator gives every seed, by product, at the generator's cells.
    public static ScenarioFit Starter(ScenarioStage stage) => stage.Fit("Longinus", StarterQuality,
        ("Wheelhouse", int2(2, 6), None), // Cockpit 2x2
        ("True North", int2(1, 0), Reversed), // Large Drive
        ("True North", int2(3, 0), Reversed), // Large Drive
        ("Hestia", int2(2, 4), None), // Compact Reactor
        ("Homer", int2(0, 5), None), // Guided Missile Launcher
        ("Homer", int2(5, 5), None), // Guided Missile Launcher
        ("Stagecoach", int2(2, 14), CounterClockwise), // Medium Drive
        ("Stagecoach", int2(3, 14), Clockwise), // Medium Drive
        ("Spotlight", int2(1, 8), None), // Light Laser
        ("Spotlight", int2(4, 8), None), // Light Laser
        ("Iapyx", int2(2, 2), CounterClockwise), // Radiator
        ("Iapyx", int2(3, 2), Clockwise), // Radiator
        ("not if i see you first", int2(3, 10), None), // Compact Sensor
        ("PotaT+-", int2(1, 5), None), // Capacitor
        ("👀🎯", int2(1, 6), None), // Targeting Computer
        ("Space Tractor", int2(1, 7), None), // Tractor Beam
        ("Pithos", int2(2, 8), None)); // Cargo Bay

    public override Galaxy Generate(GalaxyStage stage)
    {
        var galaxy = stage.Cast(Cast);
        galaxy.PlaceGate(galaxy.ResolveFaction(Cast.AntagonistFaction));
        return galaxy;
    }

    public override void Stage(ScenarioStage stage)
    {
        var galaxy = stage.Galaxy;
        var pirates = galaxy.ResolveFaction(Cast.ProtagonistFaction);
        galaxy.FactionRelationships[pirates] = FactionRelationship.Friendly;
        galaxy.FactionRelationships[galaxy.ResolveFaction(Cast.AntagonistFaction)] = FactionRelationship.Hated;
        galaxy.FactionRelationships[galaxy.ResolveFaction(Cast.BufferFaction)] = FactionRelationship.Neutral;
        foreach (var neutral in Cast.NeutralFactions)
            galaxy.FactionRelationships[galaxy.ResolveFaction(neutral)] = FactionRelationship.Neutral;
        stage.Player(Starter(stage), float2.zero);

        // Zenith's silo buys the ores the refinery's gear draws (ruling demo-zenith-ore-depot), by a standing order to the
        // refinery, and a hauler of Zenith's faction carries it (ore-haul-run).
        var zenith = stage.Station("trade-hub");
        var refinery = stage.Station("processing");
        if (refinery == null) return;
        var items = refinery.ItemManager;
        var ores = StationWants.Of(refinery).Where(good => FieldKinds.IsOre(items.ItemData, items.ItemData.Get(new CultRecordRef<ItemData>(good))))
            .OrderBy(good => good.Value, System.StringComparer.Ordinal).Select(good => new CultRecordRef<ItemData>(good));
        stage.HaulOrder("demo-terminus-ore", zenith, refinery, ores, "scenario:demo-terminus");
        // Interim scaffolding (economy-is-never-faked): the hauler's draw is deleted when an Aeronautics Unlimited shipyard in the
        // region's chain (demo-chain-spans-the-region) builds it.
        stage.Hauler(stage.Generated(null, zenith.Faction), "demo-terminus-ore", stage.Arena.GetOrbitPosition(zenith.OrbitData));
    }
}

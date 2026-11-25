using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using float2 = CultMath.float2;
using static CultMath.math;

// Formations (compiled groups a summon places, with a stable id and a cast faction) into a running arena.
public sealed partial class RunStartTests
{
    private sealed class FormationOf : Formation
    {
        private readonly string _faction;
        public FormationOf(string faction) => _faction = faction;
        public override string Id => "test-formation";
        public override string Name => "Test Formation";
        public override string Faction => _faction;
        public override void Place(ZoneStage stage, Faction faction, float2 at, float2 facing) =>
            stage.Place(stage.Generated("", faction), at, facing, piloted: true);
    }

    // The cast and size each formation is authored with: a literal, so a miscast formation cannot vouch for itself.
    private static readonly (string Id, string Faction, int Ships)[] Authored =
    {
        ("sparring-target", null, 1),
        ("zhestokost-pair", "Zhestokost", 2),
        ("pirates-pair", "Pirates", 2),
        ("lucent-pair", "Lucent Media", 2),
        ("aeronautics-pair", "Aeronautics Unlimited", 2)
    };

    // Every formation summons into a running zone: no failure, each placed entity is a member with one agent and carries the
    // faction the formation is authored for (a literal, resolved through the catalog as the game does), the ships stand 60 m
    // apart centred on the summon point. Kills: a formation that drops or miscasts its faction, places nothing, or collapses
    // its spacing.
    [Fact]
    public void EveryFormationSummons()
    {
        Assert.Equal(Authored.Select(a => a.Id), Formations.All.Select(formation => formation.Id));
        foreach (var formation in Formations.All)
        {
            var (_, factionName, ships) = Authored.Single(a => a.Id == formation.Id);
            var zone = Arena(true);
            Stage(zone, new Scripted(true, stage => stage.Player(stage.Bare("Djinni"), float2(0, 0))));
            for (var tick = 0; tick < 5; tick++) zone.Update(.1f);

            var failures = new List<string>();
            var summoned = formation.Summon(_items, zone, float2(400, 0), float2(-1, 0), failures);

            Assert.True(failures.Count == 0, $"{formation.Id}: {string.Join("; ", failures)}");
            Assert.NotNull(summoned);
            Assert.NotEmpty(summoned);
            var expected = factionName == null ? null : _galaxy.ResolveFaction(factionName);
            if (factionName != null)
            {
                Assert.NotNull(expected);
                Assert.Equal(factionName, expected.Name);
            }
            Assert.Equal(ships, summoned.Count);
            foreach (var entity in summoned)
            {
                SafeAssert.In(zone, entity, $"{formation.Id}: the summoned ship is admitted");
                Assert.Equal(1, AgentsOf(zone, entity));
                Assert.Same(expected, entity.Faction);
            }
            if (ships == 2)
            {
                var a = summoned[0].Position.xz;
                var b = summoned[1].Position.xz;
                Assert.Equal(60f, length(a - b), 1e-3f);
                Assert.Equal(400f, (a.x + b.x) / 2, 1e-3f);
                Assert.Equal(0f, (a.y + b.y) / 2, 1e-3f);
            }
        }
    }

    // Ids are the stable handle: unique, kebab-case, and found by ById. Kills: duplicate or display-text ids.
    [Fact]
    public void FormationIdsAreStable()
    {
        var ids = Formations.All.Select(formation => formation.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        foreach (var formation in Formations.All)
        {
            Assert.Matches(new Regex("^[a-z0-9]+(-[a-z0-9]+)*$"), formation.Id);
            Assert.Same(formation, Formations.ById(formation.Id));
        }
        Assert.Null(Formations.ById("no-such-formation"));
    }

    // A faction the catalog does not hold refuses the summon rather than arriving factionless. Kills: resolving to null and
    // summoning factionless ships.
    [Fact]
    public void AnUnknownFormationFactionSummonsNothing()
    {
        var zone = Arena(true);
        Stage(zone, new Scripted(true, stage => stage.Player(stage.Bare("Djinni"), float2(0, 0))));
        var entities = zone.Entities.ToList();
        var agents = zone.Agents.ToList();
        var failures = new List<string>();

        Assert.Null(new FormationOf("No Such Faction").Summon(_items, zone, float2(400, 0), float2(-1, 0), failures));

        Assert.Contains(failures, failure => failure.Contains("No Such Faction"));
        SafeAssert.Unchanged(zone, entities, agents);
    }
}

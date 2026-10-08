using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CultMath;
using GameCult.Caching;
using UniRx;
using Xunit;

// The flight (Agents/Flight.cs) is the only writer of its members' Target and Task. These tests drive it on the
// IffAndCombatTests world. Perception is the sensors' output (VisibleEnemies, EntityInfoGathered), which
// IffAndCombatTests covers separately, so the tests set it directly; a grudge is a real IFF override.
public sealed class FlightTests : IDisposable
{
    private readonly IffAndCombatTests _world = new IffAndCombatTests();
    private readonly ItemManager _items;
    private readonly Zone _zone;

    public FlightTests() => (_items, _zone) = _world.BuildWorld();

    public void Dispose() => _world.Dispose();

    private static readonly Faction Hostile = new Faction { Name = "Hostile" };

    private static Faction WithDoctrine(EngageOn on, float grace = 0, string hail = null, float comply = 0) => new Faction
    {
        Name = "Escort",
        Doctrine = new FactionDoctrine
        {
            EngageOn = on, Grace = grace, Hail = hail, ComplySpeed = comply,
            Combatant = new RoleDoctrine { RangeExponent = .25f, MinHitProbability = .2f }
        }
    };

    private Ship Pilot(Faction faction, float x = 0)
    {
        var ship = _world.NewShip(_items, _zone, faction, piloted: true);
        ship.Position = new float3(x, 0, 0);
        return ship;
    }

    private Ship Track(float x = 100)
    {
        var track = _world.NewShip(_items, _zone, Hostile);
        track.Position = new float3(x, 0, 0);
        return track;
    }

    private static void See(Ship member, Entity track)
    {
        member.EntityInfoGathered[track] = 1;
        member.VisibleEnemies.Add(track);
    }

    private void Tick(float dt)
    {
        foreach (var flight in _zone.Flights.ToArray()) flight.Update(dt);
    }

    [Fact]
    public void ADefaultDoctrineEngagesTheVisibleEnemyAsMinionDid()
    {
        var member = Pilot(new Faction { Name = "Plain" });
        var track = Track();
        See(member, track);

        Tick(.1f);
        Assert.Same(track, member.Target.Value.Entity);

        member.VisibleEnemies.Remove(track);
        Tick(.1f);
        Assert.True(member.Target.Value.IsNone);
        Assert.IsType<PatrolOrbitsTask>(_zone.Agents.Single().Task);
    }

    [Fact]
    public void OnlyTheFlightWritesTheTarget()
    {
        var member = Pilot(new Faction { Name = "Plain" });
        See(member, Track());

        Assert.True(member.Target.Value.IsNone);
        Tick(.1f);
        Assert.False(member.Target.Value.IsNone);
    }

    [Fact]
    public void IdentifiedWaitsForTheGearTier()
    {
        _items.GameplaySettings.TargetGearInfoThreshold = .8f;
        var faction = WithDoctrine(EngageOn.Identified);
        var a = Pilot(faction);
        var b = Pilot(faction, 10);
        var track = Track();
        See(a, track);
        See(b, track);
        a.EntityInfoGathered[track] = .7f;
        b.EntityInfoGathered[track] = .7f;

        Tick(.1f);
        Assert.True(a.Target.Value.IsNone);
        Assert.True(b.Target.Value.IsNone);

        b.EntityInfoGathered[track] = .9f;
        Tick(.1f);
        Assert.Same(track, a.Target.Value.Entity);
        Assert.Same(track, b.Target.Value.Entity);
    }

    [Fact]
    public void ProvokedEngagesOnlyTheGrudgedTrack()
    {
        var member = Pilot(WithDoctrine(EngageOn.Provoked));
        var calm = Track(80);
        var grudged = Track(120);
        See(member, calm);
        See(member, grudged);
        member.SetIff(grudged, true);

        Tick(.1f);

        Assert.Same(grudged, member.Target.Value.Entity);
    }

    [Fact]
    public void NeverIgnoresEvenAGrudge()
    {
        var member = Pilot(WithDoctrine(EngageOn.Never));
        var track = Track();
        See(member, track);
        member.SetIff(track, true);

        Tick(.1f);

        Assert.True(member.Target.Value.IsNone);
    }

    [Fact]
    public void GraceHailsOnceFromTheNearestThenEngages()
    {
        var faction = WithDoctrine(EngageOn.Detection, grace: 5, hail: "Hold.");
        var near = Pilot(faction, 90);
        var far = Pilot(faction, 0);
        var track = Track(100);
        See(near, track);
        See(far, track);

        Tick(.1f);

        Assert.True(near.Messages.ContainsKey("Hold."));
        Assert.False(far.Messages.ContainsKey("Hold."));
        Assert.True(near.Target.Value.IsNone && far.Target.Value.IsNone);
        Assert.All(_zone.Agents, a => Assert.IsType<FollowTask>(a.Task));

        Tick(4.8f);
        Assert.True(near.Target.Value.IsNone && far.Target.Value.IsNone);

        Tick(.4f);
        Assert.Same(track, near.Target.Value.Entity);
        Assert.Same(track, far.Target.Value.Entity);
    }

    [Fact]
    public void ASlowTrackComplies()
    {
        var faction = WithDoctrine(EngageOn.Detection, grace: 5, hail: "Hold.", comply: 10);
        var member = Pilot(faction);
        var track = Track();
        See(member, track);

        Tick(.1f);
        Tick(5.1f);
        Assert.True(member.Target.Value.IsNone);
        Assert.IsType<FollowTask>(_zone.Agents.Single().Task);

        member.Messages.Clear();
        track.Velocity = new float2(20, 0);
        Tick(.1f);
        Assert.Same(track, member.Target.Value.Entity);
        Assert.Empty(member.Messages);
    }

    [Fact]
    public void AGrudgeSkipsTheGrace()
    {
        var member = Pilot(WithDoctrine(EngageOn.Detection, grace: 5, hail: "Hold."));
        var track = Track();
        See(member, track);

        Tick(.1f);
        Assert.True(member.Target.Value.IsNone);

        member.SetIff(track, true);
        Tick(.1f);
        Assert.Same(track, member.Target.Value.Entity);
    }

    [Fact]
    public void FollowClosesWhenFarAndOpensWhenNear()
    {
        var far = Pilot(null);
        var near = Pilot(null);
        var anchor = Track();

        float TurnAfterFollowing(Ship ship, float3 anchorAt)
        {
            anchor.Position = anchorAt;
            var minion = new Minion(ship, FactionDoctrine.Default(_items.GameplaySettings).Combatant)
                { Task = new FollowTask { Anchor = anchor, Standoff = 100 } };
            minion.Update(.1f);
            minion.Update(.1f);
            return ship.Turn;
        }

        // The hull faces +z. Anchor 300 ahead: the desired velocity is dead ahead, no turn. Anchor 20 ahead: inside the
        // standoff, the desired velocity is astern, and the pilot turns to it.
        Assert.Equal(0, TurnAfterFollowing(far, new float3(0, 0, 300)), 3);
        Assert.NotEqual(0, TurnAfterFollowing(near, new float3(0, 0, 20)), 3);
    }

    [Fact]
    public void ATaskChangeLeavesThePatrolSubstate()
    {
        var ship = Pilot(null);
        var anchor = Track();
        var minion = new Minion(ship, FactionDoctrine.Default(_items.GameplaySettings).Combatant)
            { Task = new PatrolOrbitsTask { Circuit = new CultRecordKey[1] } };
        for (var i = 0; i < 4; i++) minion.Update(.1f);
        Assert.IsType<MoveToOrbitState>(CurrentState(minion));

        minion.Task = new FollowTask { Anchor = anchor, Standoff = 50 };
        minion.Update(.1f);

        Assert.IsType<FollowState>(CurrentState(minion));
    }

    private static BaseState CurrentState(Agent agent) =>
        (BaseState) typeof(Agent).GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(agent);

    [Fact]
    public void RemovalTakesTheAgentAndTheFlightMember()
    {
        var faction = new Faction { Name = "Plain" };
        var dies = Pilot(faction);
        var leaves = Pilot(faction, 10);
        var stays = Pilot(faction, 20);
        var player = _world.NewShip(_items, _zone, faction, isPlayerShip: true);
        var flight = _zone.Flights.Single();
        Assert.Equal(3, flight.Count);

        dies.Hull.Durability = 0;
        dies.HullDamage.OnNext(1f);
        _zone.Entities.Remove(leaves);

        Assert.Equal(new[] { stays }, _zone.Agents.Select(a => a.Ship));
        Assert.Equal(1, flight.Count);
        Assert.DoesNotContain(_zone.Agents, a => a.Ship == player);
    }

    private static string TempCatalog() => Path.Combine(Path.GetTempPath(), "aetheria-doctrine-" + Guid.NewGuid().ToString("N") + ".cc");

    [Fact]
    public void TheDoctrineRoundTrips()
    {
        var path = TempCatalog();
        try
        {
            using (var cache = AetheriaStores.Open(path, catalogWritable: true))
            {
                cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
                cache.Upsert(new Faction
                {
                    Name = "Armed",
                    Doctrine = new FactionDoctrine
                    {
                        EngageOn = EngageOn.Trespass, Grace = 6, Hail = "Hold.", ComplySpeed = 10,
                        Combatant = new RoleDoctrine { RangeExponent = .05f, MinHitProbability = .15f, HoldStandoff = 40 }
                    }
                });
                cache.Upsert(new Faction { Name = "Bare" });
                cache.FlushAsync().Wait();
            }

            using (var cache = AetheriaStores.Open(path))
            {
                var armed = cache.GetAll<Faction>().Single(f => f.Name == "Armed").Doctrine;
                Assert.Equal(EngageOn.Trespass, armed.EngageOn);
                Assert.Equal(6f, armed.Grace);
                Assert.Equal("Hold.", armed.Hail);
                Assert.Equal(10f, armed.ComplySpeed);
                Assert.Equal(.05f, armed.Combatant.RangeExponent);
                Assert.Equal(.15f, armed.Combatant.MinHitProbability);
                Assert.Equal(40f, armed.Combatant.HoldStandoff);
                Assert.Null(cache.GetAll<Faction>().Single(f => f.Name == "Bare").Doctrine);
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TheCatalogCommandNamesAMissingFaction()
    {
        var path = TempCatalog();
        try
        {
            using (var cache = AetheriaStores.Open(path, catalogWritable: true))
            {
                cache.Upsert(new TestCatalogGlobal { Name = "Temperament" });
                foreach (var name in new[] { "Zhestokost", "Lucent Media", "Aeronautics Unlimited" }) cache.Upsert(new Faction { Name = name });
                cache.FlushAsync().Wait();
            }

            var output = new StringWriter();
            using (var cache = AetheriaStores.Open(path, catalogWritable: true))
            {
                DoctrineCatalog.Run(cache, apply: true, output);
                cache.FlushAsync().Wait();
            }

            Assert.Contains("Pirates: no Faction record, doctrine not written", output.ToString());
            using (var cache = AetheriaStores.Open(path, catalogWritable: true))
            {
                Assert.All(cache.GetAll<Faction>(), f => Assert.NotNull(f.Doctrine));
                Assert.Equal(EngageOn.Trespass, cache.GetAll<Faction>().Single(f => f.Name == "Zhestokost").Doctrine.EngageOn);
                cache.Upsert(new Faction { Name = "Pirates" });
                DoctrineCatalog.Run(cache, apply: true, new StringWriter());
                Assert.Equal(8f, cache.GetAll<Faction>().Single(f => f.Name == "Pirates").Doctrine.Grace);
            }
        }
        finally { File.Delete(path); }
    }
}

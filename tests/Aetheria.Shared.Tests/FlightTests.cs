using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CultMath;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using UniRx;
using Xunit;
using static CultMath.math;

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

    private Ship Pilot(Faction faction, float x = 0, float z = 0)
    {
        var ship = _world.NewShip(_items, _zone, faction, piloted: true);
        ship.Position = new float3(x, 0, z);
        return ship;
    }

    private Ship Track(float x = 100, float z = 0)
    {
        var track = _world.NewShip(_items, _zone, Hostile);
        track.Position = new float3(x, 0, z);
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
        a.EntityInfoGathered[track] = MathF.BitDecrement(.8f); // just under the threshold
        b.EntityInfoGathered[track] = MathF.BitDecrement(.8f);

        Tick(.1f);
        Assert.True(a.Target.Value.IsNone);
        Assert.True(b.Target.Value.IsNone);

        b.EntityInfoGathered[track] = .8f; // exactly the threshold
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

    public static System.Collections.Generic.IEnumerable<object[]> BuildOrders() =>
        from order in new[] { "NTFM", "MFTN", "TMNF", "FNMT", "NMTF", "TFMN" }
        select new object[] { order };

    // The track is off the origin. N is the nearest member by distance, on the diagonal; T is nearer than N in x alone, F
    // nearer in z alone, and M nearest by the sum of the axis distances (60 against N's 80), all farther in distance.
    // Built in several orders: the speaker is the nearest by distance, not first or last built, nor the nearest on one
    // axis or by the sum of components. Kills: the hail from the first or last member, or from the nearest by x, by z
    // or by the sum of components.
    [Theory]
    [MemberData(nameof(BuildOrders))]
    public void GraceHailsOnceFromTheNearestThenEngages(string order)
    {
        var faction = WithDoctrine(EngageOn.Detection, grace: 5, hail: "Hold.");
        var (tx, tz) = (300f, -200f);
        var spots = new System.Collections.Generic.Dictionary<char, (float x, float z)>
            { ['N'] = (tx + 40, tz + 40), ['T'] = (tx + 5, tz + 120), ['F'] = (tx - 200, tz + 10), ['M'] = (tx + 60, tz) };
        var ships = order.ToDictionary(c => c, c => Pilot(faction, spots[c].x, spots[c].z));
        var track = Track(tx, tz);
        foreach (var ship in ships.Values) See(ship, track);

        Tick(.1f);

        Assert.True(ships['N'].Messages.ContainsKey("Hold."));
        Assert.False(ships['T'].Messages.ContainsKey("Hold."));
        Assert.False(ships['F'].Messages.ContainsKey("Hold."));
        Assert.False(ships['M'].Messages.ContainsKey("Hold."));
        Assert.All(ships.Values, m => Assert.True(m.Target.Value.IsNone));
        Assert.All(_zone.Agents, a => Assert.IsType<FollowTask>(a.Task));

        Tick(4.8f);
        Assert.All(ships.Values, m => Assert.True(m.Target.Value.IsNone));

        Tick(.4f);
        Assert.All(ships.Values, m => Assert.Same(track, m.Target.Value.Entity));
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

    // Grace 3 against ComplySpeed 10: a track at 5 is slower than ComplySpeed (complied) though faster than the Grace
    // number, and one at 12 is faster than both. Kills: either comparison reading Grace for ComplySpeed.
    // The speed is the length of the velocity: along x, along y, and on the diagonal at the same speed. Kills: either
    // comparison reading one component.
    [Theory]
    [InlineData(5f, false, 1f, 0f)]
    [InlineData(12f, true, 1f, 0f)]
    [InlineData(5f, false, 0f, 1f)]
    [InlineData(12f, true, 0f, 1f)]
    [InlineData(5f, false, .6f, .8f)]
    [InlineData(12f, true, .6f, .8f)]
    [InlineData(8f, false, .7071f, .7071f)]  // 11.3 by the sum of components, 8 by length
    [InlineData(12f, true, .7071f, .7071f)]  // 8.5 by the larger component, 12 by length
    public void ComplianceIsJudgedOnComplySpeedAndNotOnGrace(float speed, bool engaged, float dx, float dy)
    {
        var member = Pilot(WithDoctrine(EngageOn.Detection, grace: 3, hail: "Hold.", comply: 10));
        var track = Track();
        track.Velocity = new float2(dx, dy) * speed;
        See(member, track);

        Tick(.5f);
        Tick(3.5f);
        Assert.Equal(engaged, !member.Target.Value.IsNone);

        // Once complied the same speed stays complied; once engaged it stays engaged.
        Tick(.1f);
        Assert.Equal(engaged, !member.Target.Value.IsNone);
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
        var far = Pilot(null, -200, 50);
        var near = Pilot(null, -200, 50);
        var anchor = Track();

        float TurnAfterFollowing(Ship ship, float3 anchorAt)
        {
            anchor.Position = ship.Position + anchorAt;
            var minion = new Minion(ship, FactionDoctrine.Default(_items.GameplaySettings).Combatant)
                { Task = new FollowTask { Anchor = anchor, Standoff = 100 } };
            minion.Update(.1f);
            minion.Update(.1f);
            return ship.Turn;
        }

        // The hull faces +z, and the pilots are off the origin so the anchor's offset from the ship counts, not its
        // coordinates. Anchor 300 ahead: the desired velocity is dead ahead, no turn. Anchor 20 ahead: inside the
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

    [Fact]
    public void APatrollingPilotStaysOnPatrol()
    {
        var ship = Pilot(null);
        var minion = new Minion(ship, FactionDoctrine.Default(_items.GameplaySettings).Combatant)
            { Task = new PatrolOrbitsTask { Circuit = new CultRecordKey[1] } };

        for (var i = 0; i < 30; i++)
        {
            minion.Update(.1f);
            if (i > 0) Assert.IsNotType<BaseState>(CurrentState(minion));
        }
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
        Assert.Equal(new[] { leaves, stays }, _zone.Agents.Select(a => a.Ship));
        Assert.Equal(2, flight.Count);

        _zone.Remove(leaves);
        Assert.Equal(new[] { stays }, _zone.Agents.Select(a => a.Ship));
        Assert.Equal(1, flight.Count);
        Assert.True(flight.Contains(_zone.Agents.Single()));
        Assert.DoesNotContain(_zone.Agents, a => a.Ship == player);
    }

    private static void Forbid(Ship track) => track.PresencePermitted = Observable.Return(false).ToReadOnlyReactiveProperty();

    [Fact]
    public void TrespassEngagesOnlyWhatTheZoneDoesNotPermit()
    {
        var member = Pilot(WithDoctrine(EngageOn.Trespass));
        var welcome = Track(80);
        var trespasser = Track(120);
        See(member, welcome);
        See(member, trespasser);
        welcome.PresencePermitted = Observable.Return(true).ToReadOnlyReactiveProperty();
        Forbid(trespasser);

        Tick(.1f);

        Assert.Same(trespasser, member.Target.Value.Entity);
    }

    [Fact]
    public void ADetectionDoctrineEngagesWhateverIsVisibleWithoutAGrudge()
    {
        var member = Pilot(WithDoctrine(EngageOn.Detection));
        var track = Track();
        See(member, track);

        Tick(.1f);

        Assert.Same(track, member.Target.Value.Entity);
        Assert.Null(member.IffOverride(track));
    }

    [Fact]
    public void EachFactionFliesAFlightOfItsOwnAndUnaffiliatedShipsShareOne()
    {
        Pilot(new Faction { Name = "One" });
        Pilot(new Faction { Name = "Two" });
        Pilot(null);
        Pilot(null);

        Assert.Equal(new[] { 1, 1, 2 }, _zone.Flights.Select(f => f.Count).OrderBy(c => c));
    }

    [Fact]
    public void TheZoneTickRunsTheFlightsBeforeTheAgents()
    {
        var member = Pilot(new Faction { Name = "Plain" });
        var track = Track();
        See(member, track);

        _zone.Update(.1f);

        Assert.Same(track, member.Target.Value.Entity);
    }

    [Fact]
    public void ADoctrineWithoutACombatantFightsWithTheDefaultRole()
    {
        var faction = new Faction { Name = "Sparse", Doctrine = new FactionDoctrine { EngageOn = EngageOn.Detection } };
        Pilot(faction);

        var combatant = _zone.Flights.Single().Combatant;

        Assert.Equal(_items.GameplaySettings.AgentRangeExponent, combatant.RangeExponent);
        Assert.Equal(_items.GameplaySettings.AgentMinHitProbability, combatant.MinHitProbability);
    }

    // The picture is the union of the members' senses while overrides are per member: a flightmate engages a track
    // that a member has made friendly (a truce), and that member neither targets it nor follows its bearing. A truce is
    // no grudge, in either order of building. Kills: the member's own friendly override ignored when it holds a bearing,
    // or read as a grudge.
    [Theory]
    [InlineData(EngageOn.Detection, true)]
    [InlineData(EngageOn.Detection, false)]
    [InlineData(EngageOn.Provoked, true)]
    [InlineData(EngageOn.Provoked, false)]
    public void ATrackAMemberHasMadeFriendlyIsNeitherTargetedNorFollowedByIt(EngageOn on, bool friendlyBuiltFirst)
    {
        var faction = WithDoctrine(on);
        Ship friendly, sees;
        if (friendlyBuiltFirst) { friendly = Pilot(faction); sees = Pilot(faction, 10); }
        else { sees = Pilot(faction, 10); friendly = Pilot(faction); }
        var track = Track();
        friendly.SetIff(track, false);
        See(sees, track);

        Tick(.1f);

        Assert.Same(on == EngageOn.Detection ? track : null, sees.Target.Value.Entity);
        Assert.True(friendly.Target.Value.IsNone);
        Assert.IsType<PatrolOrbitsTask>(_zone.Agents.Single(a => a.Ship == friendly).Task);
    }

    [Fact]
    public void AnyMembersGrudgeEngagesTheWholeFlight()
    {
        var faction = WithDoctrine(EngageOn.Provoked);
        var angry = Pilot(faction);
        var calm = Pilot(faction, 10);
        var track = Track();
        See(angry, track);
        See(calm, track);
        angry.SetIff(track, true);

        Tick(.1f);

        Assert.Same(track, angry.Target.Value.Entity);
        Assert.Same(track, calm.Target.Value.Entity);
    }

    [Fact]
    public void TheGraceEndsExactlyWhenItIsUp()
    {
        var member = Pilot(WithDoctrine(EngageOn.Detection, grace: 5, hail: "Hold."));
        See(member, Track());

        Tick(.5f); // hailing starts at 0.5 s
        Tick(4.5f);
        Assert.True(member.Target.Value.IsNone, "4.5 s into a 5 s grace");
        Tick(.5f);
        Assert.False(member.Target.Value.IsNone, "5.0 s in");
    }

    [Fact]
    public void NothingComplyingMeansTheGraceEndsInEngagement()
    {
        var member = Pilot(WithDoctrine(EngageOn.Detection, grace: 1, hail: "Hold.", comply: 0));
        See(member, Track());

        Tick(.5f);
        Tick(1f);

        Assert.False(member.Target.Value.IsNone);
    }

    [Fact]
    public void ATrackExactlyAtComplySpeedDoesNotComply()
    {
        var member = Pilot(WithDoctrine(EngageOn.Detection, grace: 1, hail: "Hold.", comply: 10));
        var track = Track();
        track.Velocity = new float2(10, 0);
        See(member, track);

        Tick(.5f);
        Tick(1f);

        Assert.Same(track, member.Target.Value.Entity);
    }

    [Fact]
    public void ACompliantTrackAtExactlyComplySpeedIsEngaged()
    {
        var member = Pilot(WithDoctrine(EngageOn.Detection, grace: 1, hail: "Hold.", comply: 10));
        var track = Track();
        See(member, track);

        Tick(.5f);
        Tick(1f);
        Assert.True(member.Target.Value.IsNone, "complied while still");

        track.Velocity = new float2(10, 0);
        Tick(.1f);
        Assert.Same(track, member.Target.Value.Entity);
    }

    // The member is off the origin and its nearer track is the one farther from the origin: a pick by coordinates alone
    // takes the other. Kills: Flight's nearest pick reading the track's position without the member's.
    [Fact]
    public void AMemberKeepsItsTargetWhileThatTrackStaysEngaged()
    {
        var member = Pilot(new Faction { Name = "Plain" }, 1000);
        var first = Track(950);
        var second = Track(100);
        See(member, first);
        See(member, second);

        Tick(.1f);
        Assert.Same(first, member.Target.Value.Entity); // the nearer

        first.Position = new float3(-500, 0, 0);
        second.Position = new float3(990, 0, 0);
        Tick(.1f);
        Assert.Same(first, member.Target.Value.Entity); // kept, though the other is nearer now
    }

    // Three engaged tracks, each nearest by a different measure from the member: A by the sum of components (100), C by
    // length (99.2), B by x alone (60). The member picks the nearest by length. Kills: the pick by the sum of components
    // or by x alone.
    [Fact]
    public void AMemberPicksTheNearestEngagedTrackByLength()
    {
        var member = Pilot(new Faction { Name = "Plain" }, 1000, 500);
        var a = Track(1100, 500);
        var b = Track(1060, 590);
        var c = Track(1075, 565);
        See(member, a);
        See(member, b);
        See(member, c);

        Tick(.1f);

        Assert.Same(c, member.Target.Value.Entity);
    }

    [Fact]
    public void HoldersCloseOnTheNearestHeldTrackAndKeepItWhileItIsHeld()
    {
        var member = Pilot(WithDoctrine(EngageOn.Detection, grace: 5, hail: "Hold."), 1000);
        var near = Track(950);
        var far = Track(100);
        See(member, near);
        See(member, far);

        Tick(.1f);
        Assert.Same(near, ((FollowTask) _zone.Agents.Single().Task).Anchor);

        near.Position = new float3(-900, 0, 0);
        far.Position = new float3(990, 0, 0);
        Tick(.1f);
        Assert.Same(near, ((FollowTask) _zone.Agents.Single().Task).Anchor);
    }

    // The held anchor leaves the picture: the member re-anchors on what is still held. Kills: the anchor rewritten only
    // when it is unchanged (`==` for `!=`), which leaves the member following a track that is gone.
    [Fact]
    public void AHeldAnchorThatLeavesThePictureIsReplacedByTheRemainingTrack()
    {
        var member = Pilot(WithDoctrine(EngageOn.Detection, grace: 5, hail: "Hold."), 1000);
        var near = Track(950);
        var far = Track(100);
        See(member, near);
        See(member, far);
        Tick(.1f);
        Assert.Same(near, ((FollowTask) _zone.Agents.Single().Task).Anchor);

        member.VisibleEnemies.Remove(near);
        Tick(.1f);

        Assert.Same(far, ((FollowTask) _zone.Agents.Single().Task).Anchor);
    }

    [Fact]
    public void ATrackThatLeavesForgetsItsPhaseAndTheMemberResumesItsPatrol()
    {
        var member = Pilot(WithDoctrine(EngageOn.Detection, grace: 5, hail: "Hold."));
        var agent = _zone.Agents.Single();
        var patrol = agent.Task;
        var track = Track();
        See(member, track);

        Tick(.1f);
        Assert.IsType<FollowTask>(agent.Task);

        member.VisibleEnemies.Remove(track);
        Tick(.1f);
        Assert.Same(patrol, agent.Task);
        Assert.Equal(Flight.Phase.None, _zone.Flights.Single().PhaseOf(track));

        member.Messages.Clear();
        See(member, track);
        Tick(.1f);
        Assert.True(member.Messages.ContainsKey("Hold."), "a returning track is hailed again");
    }

    [Fact]
    public void AnAuthoredStandoffIsTheHoldDistance()
    {
        var faction = WithDoctrine(EngageOn.Detection, grace: 5);
        faction.Doctrine.Combatant.HoldStandoff = 40;
        var member = Pilot(faction);
        See(member, Track());

        Tick(.1f);

        Assert.Equal(40f, ((FollowTask) _zone.Agents.Single().Task).Standoff);
    }

    [Fact]
    public void FollowTurnsTowardTheAnchorWhenFarAndAwayFromItWhenNear()
    {
        var anchor = Track();

        float Turn(float anchorX)
        {
            var ship = Pilot(null, -200, 50);
            anchor.Position = ship.Position + new float3(anchorX, 0, 0);
            var minion = new Minion(ship, FactionDoctrine.Default(_items.GameplaySettings).Combatant)
                { Task = new FollowTask { Anchor = anchor, Standoff = 100 } };
            minion.Update(.1f);
            minion.Update(.1f);
            return ship.Turn;
        }

        var far = Turn(300);
        var near = Turn(20);

        Assert.NotEqual(0f, far);
        Assert.NotEqual(0f, near);
        Assert.Equal(sign(far), -sign(near));
    }

    [Fact]
    public void ATaskChangeBackToPatrolLeavesTheFollowState()
    {
        var ship = Pilot(null);
        var anchor = Track();
        var minion = new Minion(ship, FactionDoctrine.Default(_items.GameplaySettings).Combatant)
            { Task = new FollowTask { Anchor = anchor, Standoff = 50 } };
        minion.Update(.1f);
        minion.Update(.1f);
        Assert.IsType<FollowState>(CurrentState(minion));

        minion.Task = new PatrolOrbitsTask { Circuit = new CultRecordKey[1] };
        minion.Update(.1f);
        Assert.IsType<PatrolOrbitsState>(CurrentState(minion));

        minion.Task = new FollowTask { Anchor = anchor, Standoff = 50 };
        minion.Update(.1f);
        minion.Update(.1f);
        minion.Task = null;
        minion.Update(.1f);
        minion.Update(.1f);
        Assert.IsNotType<FollowState>(CurrentState(minion));
    }

    public static System.Collections.Generic.IEnumerable<object[]> UnseenGeometries() =>
        from distance in new[] { 90f, 900f }
        from blindIndex in new[] { 0, 1, 2 }
        from graced in new[] { false, true }
        from right in new[] { true, false }
        from blindAt in new[] { new[] { -300f, 250f }, new[] { 300f, -250f } }
        select new object[] { distance, blindIndex, graced, right, blindAt[0], blindAt[1] };

    // A member that cannot see the engaged track steers on it as a bearing, wherever it is (off the origin, on both axes),
    // at any index in the flight, with or without a hail first (the grace ends in a grudge), left or right of the member.
    // Kills: the unseen rule holding the track only near, only for one member index, only with no grace, only on one
    // side, or only for a member at the origin or below some coordinate.
    [Theory]
    [MemberData(nameof(UnseenGeometries))]
    public void AMemberThatCannotSeeTheEngagedTrackClosesOnItsBearing(float distance, int blindIndex, bool graced, bool right, float x, float z)
    {
        var faction = WithDoctrine(EngageOn.Detection, grace: graced ? 5 : 0, hail: "Hold.");
        var members = new Ship[3];
        var sightedOffsets = new[] { 400f, -450f };
        for (int i = 0, s = 0; i < 3; i++)
            members[i] = i == blindIndex ? Pilot(faction, x, z) : Pilot(faction, x, z + sightedOffsets[s++]);
        var blind = members[blindIndex];
        var sighted = members.Where(m => m != blind).ToArray();
        var track = Track(x + (right ? distance : -distance), z + 60);
        foreach (var m in sighted) See(m, track);
        if (graced)
        {
            Tick(.1f); // hailed, not yet engaged
            sighted[0].SetIff(track, true);
        }

        Tick(.1f);

        Assert.All(sighted, m => Assert.Same(track, m.Target.Value.Entity));
        Assert.True(blind.Target.Value.IsNone);
        var task = Assert.IsType<FollowTask>(_zone.Agents.Single(a => a.Ship == blind).Task);
        Assert.Same(track, task.Anchor);
    }

    // The flight's engaged track outranks a hailed one a blind member does see, though that one is nearer. Both sit
    // either side of a blind member off the origin. Kills: the anchor picked by distance alone, or kept once something
    // outranks it.
    [Fact]
    public void ABlindMemberAnchorsOnTheEngagedTrackOverANearerHailingOne()
    {
        var faction = WithDoctrine(EngageOn.Detection, grace: 5, hail: "Hold.");
        var blind = Pilot(faction, -300, 250);
        var sighted = Pilot(faction, -300, 650);
        var engaged = Track(600, 250);
        var hailing = Track(-250, 250);
        See(sighted, engaged);
        See(blind, hailing);
        Tick(.1f);
        var task = Assert.IsType<FollowTask>(_zone.Agents.Single(a => a.Ship == blind).Task);
        Assert.Same(hailing, task.Anchor); // nothing outranks it yet: the nearest held

        sighted.SetIff(engaged, true);
        Tick(.1f);

        Assert.Same(engaged, sighted.Target.Value.Entity);
        task = Assert.IsType<FollowTask>(_zone.Agents.Single(a => a.Ship == blind).Task);
        Assert.Same(engaged, task.Anchor);
    }

    // Hail then engage is the production path of every graced doctrine: the member goes from FollowState into combat,
    // and out of it again while the Task is a FollowTask on another held track. Kills: the combat interrupt covering
    // the patrol sub-states only, or the exit reading a patrol Task only.
    [Fact]
    public void CombatIsEnteredFromFollowAndLeftWhileAnotherTrackIsHeld()
    {
        var member = Pilot(WithDoctrine(EngageOn.Detection, grace: 5, hail: "Hold."));
        var minion = (Minion) _zone.Agents.Single();
        var hot = Track(300);
        var held = Track(-300);
        See(member, hot);
        See(member, held);
        Tick(.1f);
        UpdateUntil(minion, () => CurrentState(minion) is FollowState);

        member.SetIff(hot, true);
        Tick(.1f);
        Assert.Same(hot, member.Target.Value.Entity);
        UpdateUntil(minion, () => CurrentState(minion) is CombatState);

        // The grudge listed it a second time (the hostility refresh adds it again), so every listing goes.
        while (member.VisibleEnemies.Remove(hot)) { }
        Tick(.1f);
        Assert.True(member.Target.Value.IsNone);
        Assert.IsType<FollowTask>(minion.Task);
        UpdateUntil(minion, () => !(CurrentState(minion) is CombatState));
        UpdateUntil(minion, () => CurrentState(minion) is FollowState);
    }

    private Minion PatrollingMinion(Ship ship)
    {
        // Far from the (unresolved) circuit point, so MoveToOrbit does not hand back to patrol on arrival every tick.
        ship.Position = new float3(500, 0, 0);
        var minion = new Minion(ship, FactionDoctrine.Default(_items.GameplaySettings).Combatant)
            { Task = new PatrolOrbitsTask { Circuit = new CultRecordKey[1] } };
        for (var i = 0; i < 4; i++) minion.Update(.1f);
        Assert.IsType<MoveToOrbitState>(CurrentState(minion));
        return minion;
    }

    // Patrol's own sub-states may hand over to one another first, so a transition out of patrol is looked for within a
    // few updates; none of them ever reaches combat unless the interrupt covers the patrol sub-states.
    private static void UpdateUntil(Minion minion, Func<bool> done)
    {
        for (var i = 0; i < 3 && !done(); i++) minion.Update(.1f);
        Assert.True(done(), "pilot is in " + CurrentState(minion).GetType().Name);
    }

    [Fact]
    public void ATargetPullsAPatrollingPilotIntoCombatAndLosingItReturnsToPatrol()
    {
        var ship = Pilot(null);
        var minion = PatrollingMinion(ship);
        var track = Track();
        See(ship, track);

        ship.SetTarget(new TargetRef(track));
        UpdateUntil(minion, () => CurrentState(minion) is CombatState);

        ship.SetTarget(TargetRef.None);
        UpdateUntil(minion, () => !(CurrentState(minion) is CombatState));
        UpdateUntil(minion, () => CurrentState(minion) is PatrolOrbitsState || CurrentState(minion) is MoveToOrbitState);
    }

    [Fact]
    public void CombatHoldsWhileTheTargetStands()
    {
        var ship = Pilot(null);
        var minion = PatrollingMinion(ship);
        var track = Track();
        See(ship, track);
        ship.SetTarget(new TargetRef(track));
        UpdateUntil(minion, () => CurrentState(minion) is CombatState);

        for (var i = 0; i < 5; i++)
        {
            minion.Update(.1f);
            Assert.IsType<CombatState>(CurrentState(minion));
        }
    }

    [Fact]
    public void ATaskThatIsNeitherPatrolNorFollowSendsAPatrollingPilotToTheRoot()
    {
        var ship = Pilot(null);
        var minion = PatrollingMinion(ship);

        minion.Task = null;
        minion.Update(.1f);

        Assert.IsType<BaseState>(CurrentState(minion));
    }

    // A complied track that moves off at ComplySpeed or more is engaged, whichever way it moves: the diagonal's x
    // component alone is under the speed. Kills: the Complied exit reading one component.
    [Theory]
    [InlineData(1f, 0f)]
    [InlineData(0f, 1f)]
    [InlineData(.6f, .8f)]
    public void ACompliedTrackSpeedingOffOnAnyAxisIsEngaged(float dx, float dy)
    {
        var member = Pilot(WithDoctrine(EngageOn.Detection, grace: 1, hail: "Hold.", comply: 10));
        var track = Track();
        See(member, track);
        Tick(.5f);
        Tick(1f);
        Assert.True(member.Target.Value.IsNone, "complied while still");

        track.Velocity = new float2(dx, dy) * 12f;
        Tick(.1f);

        Assert.Same(track, member.Target.Value.Entity);
    }

    // A track with no presence value is permitted: a Trespass doctrine leaves it alone.
    [Fact]
    public void TrespassLeavesATrackWithNoPresenceValueAlone()
    {
        var member = Pilot(WithDoctrine(EngageOn.Trespass));
        var track = Track();
        track.PresencePermitted = null;
        See(member, track);

        Tick(.1f);

        Assert.True(member.Target.Value.IsNone);
    }

    // A follower at a distance from an anchor; the hull faces across the line to the anchor, so a demanded velocity
    // along that line needs a turn. Returns the pilot's turn and thrust after two updates, and its top speed.
    private (float turn, float2 thrust, float topSpeed) Follow(float2 anchorVelocity, float distance, float2 facing)
    {
        var ship = Pilot(null, -200, 50);
        ship.Direction = facing;
        var anchor = Track();
        anchor.Position = ship.Position + new float3(0, 0, distance);
        anchor.Velocity = anchorVelocity;
        var minion = new Minion(ship, FactionDoctrine.Default(_items.GameplaySettings).Combatant)
            { Task = new FollowTask { Anchor = anchor, Standoff = 100 } };
        minion.Update(.1f);
        minion.Update(.1f);
        return (ship.Turn, ship.MovementDirection, minion.TopSpeed);
    }

    // At the standoff the pull is zero and the demanded velocity is the anchor's alone. Accelerate turns the hull only
    // for a velocity error above 20 and thrusts all ways above 1, so the anchor's velocity of 30 turns the hull on
    // either axis and one of 15 does not (it thrusts). Kills: the anchor's velocity dropped, halved, doubled, or
    // reduced to one component.
    [Theory]
    [InlineData(30f, 0f, 0f, 1f, true)]
    [InlineData(0f, 30f, 1f, 0f, true)]
    [InlineData(-30f, 0f, 0f, 1f, true)]
    [InlineData(15f, 0f, 0f, 1f, false)]
    [InlineData(0f, 15f, 1f, 0f, false)]
    public void AFollowerAtItsStandoffMatchesTheAnchorsVelocityAtFullScale(float vx, float vy, float fx, float fy, bool turns)
    {
        var (turn, thrust, _) = Follow(new float2(vx, vy), 100, new float2(fx, fy));

        Assert.Equal(turns, turn != 0f);
        if (!turns) Assert.NotEqual(new float2(0, 0), thrust);
    }

    // The pull toward a stationary anchor is the error over the standoff, as a fraction of the pilot's top speed: a
    // pull of 30 turns the hull and one of 15 does not. Kills: the pull halved or doubled.
    [Theory]
    [InlineData(30f, true)]
    [InlineData(15f, false)]
    public void AFollowersPullIsTheErrorOverTheStandoffAtTopSpeed(float wantedPull, bool turns)
    {
        var top = Follow(new float2(0, 0), 100, new float2(1, 0)).topSpeed;
        Assert.True(top >= 60f, "the fixture needs a top speed above the pull it asks for");
        var (turn, _, _) = Follow(new float2(0, 0), 100 + 100 * wantedPull / top, new float2(1, 0));

        Assert.Equal(turns, turn != 0f);
    }

    // Faction.Doctrine rides at wire slot 17; slot 16 is reserved for Relations. Kills: the key moved.
    [Fact]
    public void TheFactionDoctrineRidesAtWireSlot17()
    {
        var faction = new Faction { Name = "Armed", Doctrine = new FactionDoctrine { EngageOn = EngageOn.Trespass, Grace = 6f } };

        var slots = MessagePack.MessagePackSerializer.Deserialize<object[]>(MessagePack.MessagePackSerializer.Serialize(faction,
            CultDocumentMessagePackSerialization.OptionsFor(typeof(Faction).Assembly)));

        Assert.Equal(18, slots.Length);
        Assert.Null(slots[16]);
        var doctrine = Assert.IsType<object[]>(slots[17]);
        Assert.Equal(6f, Convert.ToSingle(doctrine[1]));
    }

    // A cast faction that already has a doctrine keeps it, whatever it holds: one that differs in its engagement and one
    // that differs only below it. Kills: the keep tested on the engagement alone, or on any one field.
    [Fact]
    public void TheCatalogKeepsATunedDoctrine()
    {
        var path = TempCatalog();
        FactionDoctrine Tuned(EngageOn on) => new FactionDoctrine
        {
            EngageOn = on, Grace = 99f, Hail = "Mine.", ComplySpeed = 3f,
            Combatant = new RoleDoctrine { RangeExponent = .7f, MinHitProbability = .9f, HoldStandoff = 5f }
        };
        try
        {
            using (var cache = AetheriaStores.Open(path, catalogWritable: true))
            {
                cache.Upsert(new VerseGrammar { Revision = 1 });
                cache.Upsert(new Faction { Name = "Zhestokost", Doctrine = Tuned(EngageOn.Never) });
                cache.Upsert(new Faction { Name = "Lucent Media", Doctrine = Tuned(EngageOn.Detection) });
                cache.FlushAsync().Wait();
            }

            var output = new StringWriter();
            using (var cache = AetheriaStores.Open(path, catalogWritable: true))
            {
                DoctrineCatalog.Run(cache, apply: true, output);
                cache.FlushAsync().Wait();
            }

            using (var cache = AetheriaStores.Open(path))
            {
                foreach (var (name, on) in new[] { ("Zhestokost", EngageOn.Never), ("Lucent Media", EngageOn.Detection) })
                {
                    var kept = cache.GetAll<Faction>().Single(f => f.Name == name).Doctrine;
                    Assert.Equal(on, kept.EngageOn);
                    Assert.Equal(99f, kept.Grace);
                    Assert.Equal("Mine.", kept.Hail);
                    Assert.Equal(3f, kept.ComplySpeed);
                    Assert.Equal(.7f, kept.Combatant.RangeExponent);
                    Assert.Equal(.9f, kept.Combatant.MinHitProbability);
                    Assert.Equal(5f, kept.Combatant.HoldStandoff);
                    Assert.Contains($"{name}: already has a doctrine, kept", output.ToString());
                }
            }
        }
        finally { File.Delete(path); }
    }

    // The catalog names a faction exactly: a prefix, a longer name or another case is another faction. Kills: the match
    // widened to StartsWith, Contains or a case-insensitive compare.
    [Fact]
    public void TheCatalogMatchesFactionNamesExactly()
    {
        var path = TempCatalog();
        try
        {
            var near = new[] { "Zhestokost Prime", "zhestokost", "The Pirates", "Pirate", "Lucent Media " };
            using (var cache = AetheriaStores.Open(path, catalogWritable: true))
            {
                cache.Upsert(new VerseGrammar { Revision = 1 });
                cache.Upsert(new Faction { Name = "Zhestokost" });
                foreach (var name in near) cache.Upsert(new Faction { Name = name });
                cache.FlushAsync().Wait();
            }

            using (var cache = AetheriaStores.Open(path, catalogWritable: true))
            {
                DoctrineCatalog.Run(cache, apply: true, new StringWriter());
                cache.FlushAsync().Wait();
            }

            using (var cache = AetheriaStores.Open(path))
            {
                var factions = cache.GetAll<Faction>().ToArray();
                Assert.NotNull(factions.Single(f => f.Name == "Zhestokost").Doctrine);
                Assert.All(factions.Where(f => near.Contains(f.Name)), f => Assert.Null(f.Doctrine));
            }
        }
        finally { File.Delete(path); }
    }

    // A rerun that finds nothing to author writes nothing: the file is the same bytes.
    [Fact]
    public void ReapplyingTheCatalogLeavesTheBytesAlone()
    {
        var path = TempCatalog();
        try
        {
            using (var cache = AetheriaStores.Open(path, catalogWritable: true))
            {
                cache.Upsert(new VerseGrammar { Revision = 1 });
                foreach (var name in new[] { "Zhestokost", "Lucent Media", "Aeronautics Unlimited", "Pirates" }) cache.Upsert(new Faction { Name = name });
                cache.FlushAsync().Wait();
            }

            void Apply()
            {
                using var cache = AetheriaStores.Open(path, catalogWritable: true);
                DoctrineCatalog.Run(cache, apply: true, new StringWriter());
                cache.FlushAsync().Wait();
            }

            Apply();
            var first = File.ReadAllBytes(path);
            System.Threading.Thread.Sleep(50); // a rewritten stamp would differ
            Apply();

            Assert.Equal(first, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
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
                cache.Upsert(new VerseGrammar { Revision = 1 });
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
    public void ADryRunOfTheCatalogCommandWritesNothing()
    {
        var path = TempCatalog();
        try
        {
            using (var cache = AetheriaStores.Open(path, catalogWritable: true))
            {
                cache.Upsert(new VerseGrammar { Revision = 1 });
                cache.Upsert(new Faction { Name = "Zhestokost" });
                cache.FlushAsync().Wait();
            }

            var before = File.ReadAllBytes(path);
            var output = new StringWriter();
            using (var cache = AetheriaStores.Open(path, catalogWritable: true))
            {
                DoctrineCatalog.Run(cache, apply: false, output);
                cache.FlushAsync().Wait();
            }

            Assert.Contains("Zhestokost: engage on", output.ToString());
            Assert.Equal(before, File.ReadAllBytes(path));
            using (var cache = AetheriaStores.Open(path))
                Assert.Null(cache.GetAll<Faction>().Single().Doctrine);
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
                cache.Upsert(new VerseGrammar { Revision = 1 });
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

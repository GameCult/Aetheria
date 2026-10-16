using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CultMath;
using GameCult.Caching;
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
        a.EntityInfoGathered[track] = .7f;
        b.EntityInfoGathered[track] = .7f;

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

    [Fact]
    public void GraceHailsOnceFromTheNearestThenEngages()
    {
        var faction = WithDoctrine(EngageOn.Detection, grace: 5, hail: "Hold.");
        var far = Pilot(faction, 0);
        var near = Pilot(faction, 90); // built second: the nearest, not the first built, speaks
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

    // Grace 3 against ComplySpeed 10: a track at 5 is slower than ComplySpeed (complied) though faster than the Grace
    // number, and one at 12 is faster than both. Kills: either comparison reading Grace for ComplySpeed.
    [Theory]
    [InlineData(5f, false)]
    [InlineData(12f, true)]
    public void ComplianceIsJudgedOnComplySpeedAndNotOnGrace(float speed, bool engaged)
    {
        var member = Pilot(WithDoctrine(EngageOn.Detection, grace: 3, hail: "Hold.", comply: 10));
        var track = Track();
        track.Velocity = new float2(speed, 0);
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
        from blindFirst in new[] { true, false }
        from graced in new[] { false, true }
        from right in new[] { true, false }
        select new object[] { distance, blindFirst, graced, right };

    // A member that cannot see the engaged track steers on it as a bearing, wherever it is, whoever was built first, with
    // or without a hail first (the grace ends in a grudge), left or right of the member. Kills: the unseen rule holding
    // the track only near, only for a non-first member, only with no grace, or only on one side.
    [Theory]
    [MemberData(nameof(UnseenGeometries))]
    public void AMemberThatCannotSeeTheEngagedTrackClosesOnItsBearing(float distance, bool blindFirst, bool graced, bool right)
    {
        var faction = WithDoctrine(EngageOn.Detection, grace: graced ? 5 : 0, hail: "Hold.");
        Ship blind, sighted;
        if (blindFirst) { blind = Pilot(faction, 0, 0); sighted = Pilot(faction, 0, 400); }
        else { sighted = Pilot(faction, 0, 400); blind = Pilot(faction, 0, 0); }
        var track = Track(right ? distance : -distance);
        See(sighted, track);
        if (graced)
        {
            Tick(.1f); // hailed, not yet engaged
            sighted.SetIff(track, true);
        }

        Tick(.1f);

        Assert.Same(track, sighted.Target.Value.Entity);
        Assert.True(blind.Target.Value.IsNone);
        var task = Assert.IsType<FollowTask>(_zone.Agents.Single(a => a.Ship == blind).Task);
        Assert.Same(track, task.Anchor);
    }

    // The flight's engaged track outranks a hailed one a blind member does see, though that one is nearer. Kills: the
    // anchor picked by distance alone, or kept once something outranks it.
    [Fact]
    public void ABlindMemberAnchorsOnTheEngagedTrackOverANearerHailingOne()
    {
        var faction = WithDoctrine(EngageOn.Detection, grace: 5, hail: "Hold.");
        var blind = Pilot(faction, 0, 0);
        var sighted = Pilot(faction, 0, 400);
        var engaged = Track(900);
        var hailing = Track(50);
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

        member.VisibleEnemies.Remove(hot);
        Tick(.1f);
        Assert.True(member.Target.Value.IsNone, "Target is hot " + ReferenceEquals(member.Target.Value.Entity, hot) + ", held " + _zone.Flights.Single().PhaseOf(held) + ", hot " + _zone.Flights.Single().PhaseOf(hot) + ", visible " + member.VisibleEnemies.Count);
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

using System;
using System.Linq;
using CultMath;
using GameCult.Caching;
using Xunit;

// A side's brain reaches other ships only through its PilotView (Agents/PilotView.cs): handles it issued, tracks only
// of what a member tracks, picture only of what a member's sensors hold hostile. Driven on the IffAndCombatTests world,
// like FlightTests; perception is set directly, a grudge is a real IFF override.
public sealed class PilotViewTests : IDisposable
{
    private readonly IffAndCombatTests _world = new IffAndCombatTests();
    private readonly ItemManager _items;
    private readonly Zone _zone;

    public PilotViewTests() => (_items, _zone) = _world.BuildWorld();

    public void Dispose() => _world.Dispose();

    private Ship Pilot(Faction faction, float x = 0, float z = 0)
    {
        var ship = _world.NewShip(_items, _zone, faction, piloted: true);
        ship.Position = new float3(x, 0, z);
        return ship;
    }

    private Ship Subject(float x = 100, float z = 0)
    {
        var ship = _world.NewShip(_items, _zone, new Faction { Name = "Other" });
        ship.Position = new float3(x, 0, z);
        return ship;
    }

    private static void See(Ship member, Entity subject)
    {
        member.EntityInfoGathered[subject] = 1;
        if (!member.VisibleEntities.Contains(subject)) member.VisibleEntities.Add(subject);
        member.VisibleEnemies.Add(subject);
    }

    private PilotView ViewOf(Ship ship) => _zone.Agents.Single(a => a.Ship == ship).View;

    private void Tick(float dt)
    {
        foreach (var flight in _zone.Flights.ToArray()) flight.Update(dt);
        foreach (var entity in _zone.Entities.ToArray()) PilotApplier.Apply(entity);
    }

    [Fact]
    public void ASideNeverListsItsOwnMember()
    {
        var a = Pilot(null, -200);
        var b = Pilot(null, 200);
        a.SetIff(b, true);
        b.SetIff(a, true);
        See(a, b);
        See(b, a);

        Assert.Equal(2, _zone.Flights.Count());
        Assert.Equal(new[] { PilotView.Handle(b) }, ViewOf(a).Picture());
        Assert.Equal(new[] { PilotView.Handle(a) }, ViewOf(b).Picture());
        for (var step = 0; step < 600; step++)
        {
            Tick(.1f);
            foreach (var member in new[] { a, b })
                Assert.NotEqual(PilotView.Handle(member), (_zone.Agents.Single(x => x.Ship == member).Task as FollowTask)?.Anchor);
        }
    }

    [Fact]
    public void AFactionsPictureLeavesOutItsOwnMembers()
    {
        var faction = new Faction { Name = "Squabblers" };
        var a = Pilot(faction, -200);
        var b = Pilot(faction, 200);
        var other = Subject();
        See(a, b);
        See(b, a);
        See(b, other);

        Assert.Equal(new[] { PilotView.Handle(other) }, ViewOf(a).Picture());
    }

    [Fact]
    public void APictureIsTheMembersEnemiesInMemberOrderEachOnce()
    {
        var faction = new Faction { Name = "Plain" };
        var a = Pilot(faction, -10);
        var b = Pilot(faction, 10);
        var x = Subject(100);
        var y = Subject(200);
        var z = Subject(300);
        See(a, x);
        See(a, y);
        See(b, y);
        See(b, z);

        Assert.Equal(new[] { x, y, z }.Select(PilotView.Handle), ViewOf(a).Picture());
    }

    [Fact]
    public void ATrackIsOnlyWhatTheMemberTracks()
    {
        var member = Pilot(null);
        var subject = Subject(120, 30);
        subject.Velocity = new float2(3, -4);
        subject.Direction = new float2(0, -1);
        var threshold = _items.GameplaySettings.TargetDetectionInfoThreshold;
        var view = ViewOf(member);
        var handle = PilotView.Handle(subject);

        member.EntityInfoGathered[subject] = threshold / 2;
        Assert.Null(view.TrackOf(member, handle));
        Assert.Equal(threshold / 2, view.Info(member, handle));

        member.EntityInfoGathered[subject] = threshold * 2;
        var track = view.TrackOf(member, handle);
        Assert.NotNull(track);
        Assert.Equal(subject.Position, track.Value.Position);
        Assert.Equal(subject.Velocity, track.Value.Velocity);
        Assert.Equal(subject.Direction, track.Value.Direction);
        Assert.Equal(threshold * 2, view.Info(member, handle));
    }

    [Fact]
    public void ASideTrackComesFromATrackingMember()
    {
        var faction = new Faction { Name = "Plain" };
        var a = Pilot(faction, -10);
        var b = Pilot(faction, 10);
        var x = Subject(100);
        var y = Subject(200);
        See(b, x);
        var view = ViewOf(a);

        Assert.Null(view.TrackOf(a, PilotView.Handle(x)));
        Assert.Equal(x.Position, view.SideTrack(PilotView.Handle(x)).Value.Position);
        Assert.Null(view.SideTrack(PilotView.Handle(y)));
    }

    [Fact]
    public void AViewAnswersIffOverridesAndTrespassPerMember()
    {
        var member = Pilot(null);
        var subject = Subject();
        var view = ViewOf(member);
        var handle = PilotView.Handle(subject);

        Assert.Null(view.IffOverride(member, handle));
        member.SetIff(subject, true);
        Assert.True(view.IffOverride(member, handle));
        member.SetIff(subject, false);
        Assert.False(view.IffOverride(member, handle));
        Assert.Equal(member.BelievesTrespassing(subject), view.BelievesTrespassing(member, handle));
    }

    [Fact]
    public void SeeingIsTheMembersOwnEnemyList()
    {
        var faction = new Faction { Name = "Plain" };
        var a = Pilot(faction, -10);
        var b = Pilot(faction, 10);
        var x = Subject();
        See(b, x);
        var view = ViewOf(a);

        Assert.True(view.Sees(b, PilotView.Handle(x)));
        Assert.False(view.Sees(a, PilotView.Handle(x)));
    }

    [Fact]
    public void ATargetIsThePendingDesignationElseTheAppliedTarget()
    {
        var member = Pilot(null);
        var x = Subject(100);
        var y = Subject(200);
        See(member, x);
        See(member, y);
        var view = ViewOf(member);

        Assert.Null(view.TargetOf(member));
        Assert.Null(view.AppliedTargetOf(member));

        view.Designate(member, PilotView.Handle(x));
        Assert.Equal(PilotView.Handle(x), view.TargetOf(member));
        Assert.Null(view.AppliedTargetOf(member));

        PilotApplier.Apply(member);
        Assert.Equal(PilotView.Handle(x), view.AppliedTargetOf(member));
        Assert.Equal(PilotView.Handle(x), view.TargetOf(member));

        view.Designate(member, PilotView.Handle(y));
        Assert.Equal(PilotView.Handle(y), view.TargetOf(member));
        Assert.Equal(PilotView.Handle(x), view.AppliedTargetOf(member));
    }

    [Fact]
    public void DesignatingTheAppliedTargetAgainWritesNothing()
    {
        var member = Pilot(null);
        var x = Subject(100);
        See(member, x);
        var view = ViewOf(member);
        view.Designate(member, PilotView.Handle(x));
        PilotApplier.Apply(member);

        view.Designate(member, PilotView.Handle(x));
        Assert.Null(member.Command.Designated);

        view.Designate(member, null);
        Assert.True(member.Command.Designated.Value.IsNone);
        PilotApplier.Apply(member);
        view.Designate(member, null);
        Assert.Null(member.Command.Designated);
    }

    [Fact]
    public void AHandleIsTheContactAcrossItsAbsence()
    {
        var member = Pilot(null);
        var x = Subject(100);
        var y = Subject(200);
        var view = ViewOf(member);
        See(member, x);
        var before = view.Picture().Single();

        member.VisibleEnemies.Remove(x);
        member.VisibleEntities.Remove(x);
        Assert.Empty(view.Picture());
        See(member, x);

        Assert.Equal(before, view.Picture().Single());
        Assert.Equal(PilotView.Handle(x), before);
        Assert.True(before == PilotView.Handle(x));
        Assert.Equal(PilotView.Handle(x).GetHashCode(), before.GetHashCode());
        Assert.NotEqual(PilotView.Handle(y), before);
        Assert.True(PilotView.Handle(y) != before);
    }

    [Fact]
    public void ACombatingPilotFliesOnlyOnATrackedTarget()
    {
        var member = Pilot(null);
        var subject = Subject(500, 300);
        member.SetTarget(subject);
        member.Position = new float3(500, 0, 0);
        var agent = new Minion(member, FactionDoctrine.Default(_items.GameplaySettings).Combatant)
            { Task = new PatrolOrbitsTask { Circuit = new CultRecordKey[1] } };

        for (var i = 0; i < 4; i++)
        {
            agent.Update(.1f);
            PilotApplier.Apply(member);
        }
        Assert.True((member.Command.Aim?.z ?? 0) < 100, "an untracked target is not flown on");

        See(member, subject);
        member.SetTarget(subject);
        for (var i = 0; i < 4; i++)
        {
            agent.Update(.1f);
            PilotApplier.Apply(member);
        }
        Assert.Equal(300f, member.Command.Aim.Value.z, 1);
    }
}

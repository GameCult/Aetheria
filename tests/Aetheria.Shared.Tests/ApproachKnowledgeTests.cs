using System.Linq;
using CultMath;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// A staged Approach states what its pilot already knows (ruling stage-states-pilot-knowledge): the anchor is a sensor
// track from the first step, and the pilot follows it only as long as the sensors keep it.
public sealed partial class RunStartTests
{
    private (Zone arena, Ship east, Ship west) StagedApproach(float westX = -450f)
    {
        Ship east = null, west = null;
        var (_, arena, _, failures) = Launch(new Scripted(false, stage =>
        {
            stage.Player(stage.Bare("Djinni"), float2(-50000, -50000));
            east = stage.Place(EvLonginus(stage), float2(450, 0), facing: float2(-1, 0), piloted: true) as Ship;
            west = stage.Place(EvLonginus(stage), float2(westX, 0), facing: float2(1, 0), piloted: true) as Ship;
            stage.Approach(east, west);
        }));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        return (arena, east, west);
    }

    [Fact]
    public void AStagedApproachFollowerClosesOnItsAnchorFromTheFirstStep()
    {
        var (arena, east, west) = StagedApproach();
        var view = arena.Agents.Single(a => a.Ship == east).View;
        var startGap = east.Position.x - west.Position.x;

        arena.Update(1f / 60f);
        arena.Update(1f / 60f);

        Assert.NotNull(view.TrackOf(east, PilotView.Handle(west)));
        Assert.True(east.Velocity.x < -.5f, $"the follower thrusts toward its anchor from the first steps (vx {east.Velocity.x})");
        for (var i = 0; i < 120; i++) arena.Update(1f / 60f);
        Assert.True(east.Position.x - west.Position.x < startGap - 20f, "the follower closed on its anchor");
    }

    // The anchor is staged 60 km off, far outside sensor reach: the staged knowledge decays through the sensors like any
    // track (the Sensor is the only writer after staging), and the follower stops once they have dropped it. Nothing
    // here writes the pilot's knowledge after the stage.
    [Fact]
    public void AStagedApproachFollowerStopsFollowingOnceTheSensorsLoseTheTrack()
    {
        var (arena, east, west) = StagedApproach(westX: -60000f);
        var view = arena.Agents.Single(a => a.Ship == east).View;
        var sentinel = new float3(0, 0, 12345);

        Assert.NotNull(view.TrackOf(east, PilotView.Handle(west)));
        for (var i = 0; i < 3; i++) arena.Update(1f / 60f);
        Assert.NotNull(view.TrackOf(east, PilotView.Handle(west)));
        east.Command.Aim = sentinel;
        arena.Update(1f / 60f);
        Assert.NotEqual(sentinel, east.Command.Aim.Value);

        var steps = 0;
        while (view.TrackOf(east, PilotView.Handle(west)) != null && steps++ < 1200) arena.Update(.1f);
        Assert.Null(view.TrackOf(east, PilotView.Handle(west)));

        east.Command.Aim = sentinel;
        arena.Update(.1f);
        Assert.Equal(sentinel, east.Command.Aim.Value);
    }
}

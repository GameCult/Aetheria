using System;
using System.Linq;
using System.Reflection;
using CultMath;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// Fire control is per shooter (ruling per-shooter-fire-control): a pilot flies, aims and fires on what its own sensors
// track, not on a track only a sidemate holds.
public sealed partial class RunStartTests
{
    // East and west are one side, one view; only west tracks x. East is designated on x: it must not aim or pull on west's
    // track (Combat reads TrackOf for the shooter, not SideTrack). The positive control has east track x as well, and it aims.
    // Kills: Combat flying on SideTrack.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMemberDesignatedOnAContactOnlyItsSidemateTracksNeitherAimsNorFires(bool eastTracks)
    {
        Ship east = null, west = null, x = null;
        var (_, arena, _, failures) = Launch(new Scripted(false, stage =>
        {
            stage.Player(stage.Bare("Djinni"), float2(-50000, -50000));
            east = stage.Place(EvLonginus(stage), float2(100, 0), facing: float2(-1, 0), piloted: true) as Ship;
            west = stage.Place(EvLonginus(stage), float2(-100, 0), facing: float2(1, 0), piloted: true) as Ship;
            x = stage.Place(EvLonginus(stage), float2(0, 300), facing: float2(0, -1)) as Ship;
        }));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        var known = (east.ItemManager.GameplaySettings.TargetDetectionInfoThreshold + 1f) / 2f;
        east.SetIff(x, true);
        west.SetIff(x, true);
        var side = new PilotView(east, west);
        var agent = arena.Agents.Single(a => a.Ship == east);
        agent.View = side;
        west.EntityInfoGathered[x] = known;
        if (eastTracks) east.EntityInfoGathered[x] = known;
        Assert.NotNull(side.SideTrack(PilotView.Handle(x)));
        Assert.Equal(eastTracks, side.TrackOf(east, PilotView.Handle(x)) != null);

        var sentinel = new float3(0, 0, -12345);
        object state = null;
        for (var i = 0; i < 4; i++)
        {
            east.Command.Designate(new TargetRef(x));
            east.Command.Aim = sentinel;
            agent.Update(1f / 60f);
            state = typeof(Agent).GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(agent);
        }
        Assert.IsType<CombatState>(state);
        var aimed = !east.Command.Aim.Value.Equals(sentinel);
        if (eastTracks) Assert.True(aimed, "a member that tracks the contact aims at it");
        else
        {
            Assert.False(aimed, $"a member flew its aim on a sidemate's track: {east.Command.Aim}");
            Assert.False(east.Weapons.Any(w => east.Command.Pulls(w)), "a member pulled a trigger on a sidemate's track");
        }
    }
}

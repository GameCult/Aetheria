using System.Linq;
using Xunit;
using static CultMath.math;

// The arena the operator flies to check the incoming-lock warning (map: Scenarios for operator checks, SC5 and SC14). The
// test launches the scenario the way New Game does and steps what it staged.
public sealed partial class RunStartTests
{
    // Lock On: the hostile starts piloted, beyond its sight of the player, and the player has no incoming paint. Stepped at
    // .1 s, it closes, finds the player and locks: a Track paint reaches the player bearing toward the hostile, and not at
    // once, because the hostile had to find the player first. The tick is the lock's, at seed 113 it is 83 (8.4 s).
    [Fact]
    public void LockOnSituation()
    {
        var (_, arena, staged, failures) = Launch(new LockOn(), Inputs(() => GalaxySeed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        var player = staged.Player;
        var hostile = Assert.Single(staged.Entities);
        Assert.True(player.IsHostileTo(hostile) && hostile.IsHostileTo(player), "the hostile is hostile both ways");
        Assert.Equal(1, AgentsOf(arena, hostile));

        arena.Update(0f);
        Assert.DoesNotContain(hostile, player.VisibleEnemies);
        Assert.DoesNotContain(player.IncomingPaints, paint => paint.Mode == PaintMode.Track);
        Assert.DoesNotContain(player, hostile.VisibleEnemies); // it starts without a track of the player

        var lockedAt = -1;
        var bearingDot = 0f;
        for (var tick = 0; tick < 600 && lockedAt < 0; tick++)
        {
            arena.Update(.1f);
            foreach (var paint in player.IncomingPaints.Where(paint => paint.Mode == PaintMode.Track))
            {
                lockedAt = tick;
                bearingDot = dot(normalize(paint.Bearing), normalize(hostile.Position.xz - player.Position.xz));
            }
        }
        Assert.True(lockedAt >= 0, "the hostile never locked the player within 600 ticks of .1 s");
        Assert.True(bearingDot >= cos(radians(5f)), "the warning points at the hostile");
        // Not at the staging: the hostile had to close and find the player first (a hostile staged inside sight locks within 10 ticks).
        Assert.InRange(lockedAt, 40, 200);
    }
}

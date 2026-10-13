using System.Collections.Generic;
using System.Linq;
using Xunit;
using static CultMath.math;

// The arena the operator flies to check the incoming-lock warning (map: Scenarios for operator checks, SC5 and SC14). The
// test launches the scenario the way New Game does and steps what it staged.
public sealed partial class RunStartTests
{
    // Lock On: the hostile starts piloted, facing the player, beyond its own sight of the player, and the player has no
    // incoming paint. Stepped at .1 s, it closes, finds the player and locks: a Track paint reaches the player bearing
    // toward the hostile, and its Strength rises from nothing as the lock tightens.
    [Fact]
    public void LockOnSituation()
    {
        var (_, arena, staged, failures) = Launch(new LockOn(), Inputs(() => GalaxySeed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        var player = staged.Player;
        var hostile = Assert.Single(staged.Entities);
        Assert.True(player.IsHostileTo(hostile) && hostile.IsHostileTo(player), "the hostile is hostile both ways");
        Assert.Equal(1, AgentsOf(arena, hostile));

        var toPlayer = normalize(player.Position.xz - hostile.Position.xz);
        Assert.True(dot(normalize(hostile.Direction), toPlayer) >= cos(radians(5f)), "the hostile faces the player");

        // The first real sensing tick: the hostile holds no track of the player, and the sim's own passive sensing, asked for
        // the player's signature at the staged place, settles below the detection threshold: the staging is out of its sight.
        arena.Update(.1f);
        Assert.DoesNotContain(player, hostile.VisibleEnemies);
        Assert.DoesNotContain(player.IncomingPaints, paint => paint.Mode == PaintMode.Track);
        Assert.True(player.Visibility > 0f, "the player shows a signature to be seen by");
        var threshold = player.ItemManager.GameplaySettings.TargetDetectionInfoThreshold;
        var settled = hostile.PassiveInfo(player.Position.xz, player.Visibility);
        Assert.True(settled <= threshold, $"the hostile would settle at {settled} on the player, above the detection threshold {threshold}: staged inside its sight");

        var lockedAt = -1;
        var bearingDot = 0f;
        var strengths = new List<float>();
        for (var tick = 0; tick < 600 && (lockedAt < 0 || tick < lockedAt + 40); tick++)
        {
            arena.Update(.1f);
            foreach (var paint in player.IncomingPaints.Where(paint => paint.Mode == PaintMode.Track))
            {
                if (lockedAt < 0)
                {
                    lockedAt = tick;
                    bearingDot = dot(normalize(paint.Bearing), toPlayer * -1f);
                }
                strengths.Add(paint.Strength);
            }
        }
        Assert.True(lockedAt >= 0, "the hostile never locked the player within 600 ticks of .1 s");
        Assert.True(bearingDot >= cos(radians(5f)), "the warning points at the hostile");
        Assert.True(strengths.First() < strengths.Last(), $"the lock tightens: Strength went {strengths.First()} to {strengths.Last()}");
        Assert.True(strengths.Zip(strengths.Skip(1), (a, b) => b >= a).All(rising => rising), "the lock never loosens while it holds");
    }
}

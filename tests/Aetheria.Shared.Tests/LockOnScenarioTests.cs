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
        Assert.False(hostile.VisibleEnemies.Any(x => ReferenceEquals(x, player)), "the player is out of sight");
        Assert.DoesNotContain(player.IncomingPaints, paint => paint.Mode == PaintMode.Track);
        Assert.True(player.Visibility > 0f, "the player shows a signature to be seen by");
        var threshold = player.ItemManager.GameplaySettings.TargetDetectionInfoThreshold;
        var settled = hostile.PassiveInfo(player.Position.xz, player.Visibility);
        Assert.True(settled <= threshold, $"the hostile would settle at {settled} on the player, above the detection threshold {threshold}: staged inside its sight");

        var (lockedAt, bearingError, stagedDrift, strengths) = StepToLock(arena, player, hostile, toPlayer);
        Assert.True(lockedAt >= 0, "the hostile never locked the player within 600 ticks of .1 s");
        Assert.True(bearingError <= StampedBearingLimit, $"the warning points at the hostile where it was when stamped, {bearingError} deg off (the hostile is {stagedDrift} deg off its staged line at the lock)");
        Assert.True(strengths.First() < strengths.Last(), $"the lock tightens: Strength went {strengths.First()} to {strengths.Last()}");
    }

    // Float error in the stamped pose and the normalizations, not a lag budget: the warning is exact for the tick it was stamped in.
    const float StampedBearingLimit = .05f;

    // The paint is stamped from the poses at the end of one tick and published at the start of the next (an entity's inbox
    // publishes a tick late, so the order of the entities does not matter), so the warning first read is measured against
    // the poses of the tick before: where the hostile was when the paint was stamped, not where it is now. Steps up to 600
    // ticks of .1 s, and 40 past the lock. lockedAt is -1 when it never locks.
    private static (int lockedAt, float bearingError, float stagedDrift, List<float> strengths) StepToLock(Zone arena, Ship player, Entity hostile, CultMath.float2 toPlayer)
    {
        var hostileThen = hostile.Position.xz;
        var playerThen = player.Position.xz;
        var lockedAt = -1;
        var bearingError = 0f;
        var stagedDrift = 0f;
        var strengths = new List<float>();
        for (var tick = 0; tick < 600 && (lockedAt < 0 || tick < lockedAt + 40); tick++)
        {
            arena.Update(.1f);
            foreach (var paint in player.IncomingPaints.Where(paint => paint.Mode == PaintMode.Track))
            {
                if (lockedAt < 0)
                {
                    lockedAt = tick;
                    var stamped = normalize(hostileThen - playerThen);
                    bearingError = degrees(acos(clamp(dot(normalize(paint.Bearing), stamped), -1f, 1f)));
                    stagedDrift = degrees(acos(clamp(dot(stamped, -toPlayer), -1f, 1f)));
                }
                strengths.Add(paint.Strength);
            }
            hostileThen = hostile.Position.xz;
            playerThen = player.Position.xz;
        }
        return (lockedAt, bearingError, stagedDrift, strengths);
    }

    // The staged inputs, each moved 1%: the lock and the warning's bearing still hold, so the scenario does not rest on the
    // exact numbers it was written with. Kills: a pass geometry that only the staged numbers reach.
    [Fact]
    public void LockOnHoldsUnderOnePercentOfEachStagedInput()
    {
        var at = LockOn.HostileAt;
        var variants = new (string what, LockOn scenario)[]
        {
            ("staged", new LockOn()),
            ("x +1%", new LockOn(float2(at.x * 1.01f, at.y), .6f)), ("x -1%", new LockOn(float2(at.x * .99f, at.y), .6f)),
            ("y +1%", new LockOn(float2(at.x, at.y * 1.01f), .6f)), ("y -1%", new LockOn(float2(at.x, at.y * .99f), .6f)),
            ("quality +1%", new LockOn(at, .6f * 1.01f)), ("quality -1%", new LockOn(at, .6f * .99f))
        };
        foreach (var (what, scenario) in variants)
        {
            var (_, arena, staged, failures) = Launch(scenario, Inputs(() => GalaxySeed));
            Assert.True(failures.Count == 0, $"{what}: {string.Join("; ", failures)}");
            var hostile = Assert.Single(staged.Entities);
            arena.Update(.1f);
            var (lockedAt, bearingError, _, _) = StepToLock(arena, staged.Player, hostile, normalize(staged.Player.Position.xz - hostile.Position.xz));
            Assert.True(lockedAt >= 0, $"{what}: the hostile never locked the player within 600 ticks");
            Assert.True(bearingError <= StampedBearingLimit, $"{what}: the warning is {bearingError} deg off the pose it was stamped from");
        }
    }
}

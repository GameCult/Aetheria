using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static CultMath.math;

public sealed partial class RunStartTests
{
    [Fact]
    public void ZProbeLockOn()
    {
        var log = new List<string>();
        var circuits = new List<int[]> { new[] { 3 } };
        for (var j = 0; j < 13; j++) if (j != 3) { circuits.Add(new[] { j, 3 }); circuits.Add(new[] { 3, j }); }
        foreach (var q in new[] { .2f, .3f, .4f, .5f, .7f, .8f, .9f, 1f }) foreach (var c in new[] { new[] { 3 } })
        {
            var (_, arena, staged, failures) = Launch(new LockOn(q), Inputs(() => GalaxySeed));
            var player = staged.Player;
            var hostile = staged.Entities.Single();
            var keys = arena.Orbits.Keys.ToList();
            ((PatrolOrbitsTask) arena.Agents.Single().Task).Circuit = c.Select(i => keys[i]).ToArray();
            var minD = 1e9f;
            var lockedAt = -1;
            var line = "";
            for (var tick = 0; tick < 600 && lockedAt < 0; tick++)
            {
                arena.Update(.1f);
                var d = length(hostile.Position.xz - player.Position.xz);
                minD = min(minD, d);
                foreach (var paint in player.IncomingPaints.Where(p => p.Mode == PaintMode.Track))
                {
                    lockedAt = tick;
                    var live = normalize(hostile.Position.xz - player.Position.xz);
                    var err = degrees(acos(clamp(dot(normalize(paint.Bearing), live), -1f, 1f)));
                    line = $"err={err} d={d} hostV={length(hostile.Velocity)} relV={length(hostile.Velocity - player.Velocity)}";
                    break;
                }
            }
            log.Add($"PROBE q={q} circuit [{string.Join(",", c)}] locked={lockedAt} minD={minD} {line}");
        }
        Assert.Fail(string.Join(Environment.NewLine, log));
    }
}

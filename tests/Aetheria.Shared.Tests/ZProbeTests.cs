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
        for (var which = -1; which < 13; which++)
        {
            var (_, arena, staged, failures) = Launch(new LockOn(), Inputs(() => GalaxySeed));
            var player = staged.Player;
            var hostile = staged.Entities.Single();
            var keys = arena.Orbits.Keys.ToList();
            var agent = arena.Agents.Single();
            var task = (PatrolOrbitsTask) agent.Task;
            if (which < 0)
                foreach (var b in arena.PlanetInstances)
                    log.Add($"PROBE body {arena.Planets[b.Key].Name} orbit idx {keys.IndexOf(b.Value.Orbit.Data == null ? default : keys.First(k => ReferenceEquals(arena.Orbits[k], b.Value.Orbit)))}");
            else task.Circuit = new[] { keys[which] };
            var minD = 1e9f;
            var lockedAt = -1;
            for (var tick = 0; tick < 600 && lockedAt < 0; tick++)
            {
                arena.Update(.1f);
                minD = min(minD, length(hostile.Position.xz - player.Position.xz));
                if (player.IncomingPaints.Any(p => p.Mode == PaintMode.Track)) lockedAt = tick;
            }
            log.Add($"PROBE only-orbit {which} start={arena.Orbits[keys[Math.Max(which, 0)]].Position} locked={lockedAt} minD={minD}");
        }
        Assert.Fail(string.Join(Environment.NewLine, log));
    }
}

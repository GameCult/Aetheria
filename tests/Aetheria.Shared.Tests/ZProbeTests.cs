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
        var (_, arena, staged, failures) = Launch(new LockOn(), Inputs(() => GalaxySeed));
        var player = staged.Player;
        var hostile = staged.Entities.Single(); var log = new List<string>();
        var keys = arena.Orbits.Keys.ToList();
        var agent = arena.Agents.Single();
        var circuit = ((PatrolOrbitsTask) agent.Task).Circuit;
        log.Add($"PROBE circuit idx={string.Join(",", circuit.Select(k => keys.IndexOf(k)))} orbits={keys.Count}");
        for (var i = 0; i < keys.Count; i++) log.Add($"PROBE orbit {i} at {arena.GetOrbitPosition(keys[i])}");
        var minD = 1e9f;
        for (var tick = 0; tick < 600; tick++)
        {
            arena.Update(.1f);
            var d = length(hostile.Position.xz - player.Position.xz);
            minD = min(minD, d);
            var locked = player.IncomingPaints.Any(p => p.Mode == PaintMode.Track);
            if (tick % 40 == 0 || locked && tick % 10 == 0)
                log.Add($"PROBE t={tick} d={d} host={hostile.Position.xz} orbit0={arena.Orbits[circuit[0]].Position} locked={locked} task={agent.Task?.GetType().Name} tgt={hostile is Ship s && s.Target.Value.Entity != null} vis={((Ship)hostile).VisibleEnemies.Count}");
        }
        log.Add($"PROBE minD={minD}");
        Assert.Fail("
" + string.Join("
", log));
    }
}

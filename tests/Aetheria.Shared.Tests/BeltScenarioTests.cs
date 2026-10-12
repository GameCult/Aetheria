using System.Collections.Generic;
using System.Linq;
using Xunit;
using static CultMath.math;

// The arena the operator flies to check mining target queries (map: Scenarios for operator checks, SC11 and SC14; ruling
// mining-merge-on-agent-proof). The test launches the scenario the way New Game does and reads what it staged.
public sealed partial class RunStartTests
{
    // The scan behind Belt's seed: seeds 1-256 of the demo cast's arena, no ambient traffic, the most asteroids in one belt
    // per seed. Seed 24 holds 615; seed 1 holds 299.
    private const int ScannedBeltAsteroids = 615;

    // Belt: the arena carries a belt of at least the scanned asteroid count, nothing else flies in it, the player stands
    // 100 m outside the ring on the side nearest the origin and faces along it, and the player's sensors see rocks within
    // mining reach from the first tick.
    [Fact]
    public void BeltConditions()
    {
        var (_, arena, staged, failures) = Launch(new Belt(), Inputs(() => GalaxySeed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        Assert.Empty(staged.Entities);
        Assert.Equal(new[] { staged.Player }, arena.Entities.OfType<Ship>());

        var belt = arena.Planets.Values.OfType<AsteroidBeltData>().OrderByDescending(candidate => candidate.Asteroids.Length).First();
        Assert.True(belt.Asteroids.Length >= ScannedBeltAsteroids, $"the biggest belt has {belt.Asteroids.Length} asteroids");

        var player = staged.Player;
        var orbit = arena.Orbits[belt.Orbit.Key].Data;
        var centre = arena.GetOrbitPosition(orbit.Parent.Key);
        var radial = player.Position.xz - centre;
        Assert.InRange(length(radial), orbit.Distance + 99f, orbit.Distance + 101f);
        Assert.True(length(player.Position.xz) < length(centre), "the player stands on the side of the ring nearest the origin");
        Assert.True(abs(dot(player.Direction, normalize(radial))) < .01f, "the player faces along the ring");

        var field = arena.Planets.Single(body => body.Value == belt).Key;
        var inReach = new List<ChunkId>();
        player.VisibleChunksInReach(inReach);
        Assert.NotEmpty(inReach);
        Assert.All(inReach, chunk => Assert.Equal(field, chunk.Field));
    }
}

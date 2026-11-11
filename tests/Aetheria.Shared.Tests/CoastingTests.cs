using System;
using CultMath;
using Xunit;
using static CultMath.math;
using float3 = CultMath.float3;
using Random = CultMath.Random;

// The consumer's side of aetheria-gravity:ruling:no-drag-below-top-speed: a ship launched below its hull's top speed and
// left alone keeps that speed, at the global thrust tuning the game ships with (GameplaySettings.ThrustScale 0.1) as well as
// at the fixtures' 1. Reads only the ship's velocity after the zone steps: not Drag, DragScale or any field of the fix.
// Kills: drag reintroduced below top speed, or any deceleration whose size depends on ThrustScale.
public sealed class CoastingTests
{
    private const float PlayThrustScale = .1f; // GameplaySettings.ThrustScale's default, the value play runs at

    private static float CoastedSpeed(float thrustScale, float launchSpeed, float seconds)
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var hull = cache.GetByName<HullData>("Longinus");
        var settings = RestoredHullsTests.Settings();
        settings.ThrustScale = thrustScale;
        var items = new ItemManager(1u, cache, new ProvenanceLedger(), settings, _ => { });
        items.Random = new Random(1u);
        var random = new Random(1);
        var pack = new LoadoutGenerator(ref random, items, null, null, null, .5f).GenerateShipLoadout(candidate => candidate == hull);
        Assert.NotNull(pack);
        var zone = new Zone(items, new PlanetSettings(), new ZonePack(), new GalaxyZone { Name = "Test", Owner = null }, null);
        var ship = (Ship) EntitySerializer.Unpack(items, zone, pack);
        zone.Admit(ship, piloted: false);
        ship.Position = float3.zero;
        zone.Update(0f);

        ship.MovementDirection = float2(0, 0);
        ship.Velocity = float2(0, launchSpeed);
        for (var i = 0; i < (int) (seconds * 60); i++) zone.Update(1f / 60f);
        return length(ship.Velocity);
    }

    [Theory]
    [InlineData(PlayThrustScale)]
    [InlineData(1f)]
    public void AShipLaunchedBelowTopSpeedKeepsItsSpeed(float thrustScale)
    {
        const float launch = 50f; // Longinus's top speed is 100
        var speed = CoastedSpeed(thrustScale, launch, 10f);
        Assert.True(speed >= launch - .01f, $"ThrustScale {thrustScale}: launched at {launch}, coasting 10 s left it at {speed}");
    }
}

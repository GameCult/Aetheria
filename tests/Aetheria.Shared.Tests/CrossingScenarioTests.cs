using System.Linq;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// The arenas the operator flies to check the evasion term and the articulated barrel (map: Scenarios for operator
// checks). Each test launches the scenario the way New Game does and reads what it staged, so a script that stops
// staging what its brief claims fails here.
public sealed partial class RunStartTests
{
    // Crossing: the demo's cast; projector and Cell Blaster fitted in separate weapon groups with the Fire Control Array
    // aboard; an unpiloted hostile Djinni, coasting at the Djinni's speed cap, that crosses the bow inside 400 m; and an unpiloted neutral one inside the Lightning Projector's
    // arc, short of its Range and beyond three quarters of it.
    [Fact]
    public void CrossingConditions()
    {
        var (galaxy, arena, staged, failures) = Launch(new Crossing(), Inputs(() => GalaxySeed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        Assert.Equal(DemoTerminus.Cast.ZoneCount, galaxy.Zones.Length);
        Assert.Equal(4, galaxy.HomeZones.Count);

        var player = staged.Player;
        var projector = player.Equipment.Single(item => item.Data.Name == "Lightning Projector");
        var small = player.Equipment.Single(item => item.Data.Name == "Cell Blaster");
        Assert.Contains(player.Equipment, item => item.Data.Name == "Fire Control Array");
        var groupOf = new System.Func<EquippedItem, int>(item => System.Array.FindIndex(player.WeaponGroups, group => group.items.Contains(item)));
        Assert.True(groupOf(projector) >= 0 && groupOf(small) >= 0, "both guns are in a weapon group");
        Assert.NotEqual(groupOf(projector), groupOf(small));

        Assert.Equal(2, staged.Entities.Count);
        var crosser = staged.Entities[0];
        var waiting = staged.Entities[1];
        Assert.IsType<Ship>(crosser);
        Assert.IsType<Ship>(waiting);
        Assert.Equal(("Djinni", "Djinni"), (_items.GetData(crosser.Hull).Name, _items.GetData(waiting.Hull).Name));
        Assert.Contains(crosser.Equipment, item => item.Behaviors.OfType<Weapon>().Any());

        Assert.True(player.IsHostileTo(crosser) && crosser.IsHostileTo(player), "the crosser is hostile both ways");
        Assert.Equal(0, AgentsOf(arena, crosser));
        var toCrosser = crosser.Position - player.Position;
        Assert.True(length(toCrosser) < 400f, "the crosser starts inside 400 m");
        Assert.True(toCrosser.z > 0, "the crosser starts ahead");
        Assert.True(abs(dot(crosser.Direction, player.Direction)) <= sin(radians(30f)) + 1e-4f,
            "the crosser faces within 30 degrees of perpendicular to the player's bow");
        // It is placed moving: toward the bow line at most of the Djinni's 50 m/s cap, so its run is its own and not only the
        // arena's pull on the player.
        var crosserVelocity = ((Ship) crosser).Velocity;
        Assert.True(crosserVelocity.x * toCrosser.x < 0, "the crosser starts moving toward the bow line");
        Assert.True(abs(crosserVelocity.x) >= 40f, "the crosser's speed toward the bow line is most of the Djinni's speed cap");
        Assert.True(length(crosserVelocity) >= 40f, "the crosser starts at most of the Djinni's speed cap");

        Assert.False(player.IsHostileTo(waiting) || waiting.IsHostileTo(player), "the waiting Djinni is neutral");
        Assert.Equal(0, AgentsOf(arena, waiting));
        var toWaiting = waiting.Position - player.Position;
        EvSettle(player); // a weapon's Range is read once the ship's stats have settled; the geometry above is as staged
        var range = projector.Behaviors.OfType<Weapon>().Single().Range;
        Assert.True(FireControl.InArc(projector, toWaiting), "the waiting Djinni is inside the Lightning Projector's arc");
        Assert.True(length(toWaiting) <= range, "the waiting Djinni is inside the Lightning Projector's Range");
        Assert.True(length(toWaiting) > .75f * range, "the waiting Djinni is beyond three quarters of the Lightning Projector's Range");

        // The crossing is what the sim does with that staging, read in the player's frame: the arena's gravity acts from the
        // first tick, so the t=0 facing proves nothing. Within CrossingSeconds the crosser's bearing sweeps from port to
        // starboard while it is ahead and inside CrossingMetres, and both guns bear on it. The pass is made by a coasting
        // player: the loop gives no input, and the Brief tells the operator to fly it that way.
        Assert.Equal(float2.zero, player.MovementDirection);
        var pass = FlyPass(arena, player, crosser);
        Assert.True(pass.Swept, $"the crosser's bearing never swept across the bow within {CrossingSeconds} s");
        Assert.True(pass.Ahead.y > 0, "the crosser passes the bow line ahead of the player, not astern");
        Assert.True(length(pass.Ahead) < CrossingMetres, "the crosser passes inside the stated distance");
        Assert.True(FireControl.InArc(small, float3(pass.Ahead.x, 0, pass.Ahead.y)), "Cell Blaster bears on the crosser as it crosses");
        Assert.True(dot(FireControl.MountDirection(small), float3(player.Direction.x, 0, player.Direction.y)) > 0, "Cell Blaster mounts forward");
        // At seed 112 with no input the pass is at about 3.6 s and 45 m; a seed, field or staging change that loses it moves these.
        Assert.InRange(pass.Seconds, CrossingEarliest, CrossingLatest);
        Assert.InRange(length(pass.Ahead), 30f, 60f);
    }

    // The unpiloted crosser's pass is ballistic, bent only by the field, so it does not hang on a lucky start: shifting where
    // the crosser starts by up to 10 m along either axis keeps the pass inside the test's time and distance windows, fast across
    // the line (relative speed at the bow line) and sweeping the player's view at a steady rate (angular rate seen from the player).
    // The shifts are applied to what Crossing stages, so the window is the scenario's own.
    [Fact]
    public void CrossingHoldsAcrossTheStartWindow()
    {
        var passes = new System.Collections.Generic.List<(float2 shift, CrossingPass pass)>();
        foreach (var along in new[] { true, false })
            for (var step = -4; step <= 4; step++)
            {
                var shift = along ? float2(step * 2.5f, 0) : float2(0, step * 2.5f);
                var (_, arena, staged, failures) = Launch(new Crossing(), Inputs(() => GalaxySeed));
                Assert.True(failures.Count == 0, string.Join("; ", failures));
                var crosser = staged.Entities[0];
                crosser.Position += float3(shift.x, 0, shift.y);
                passes.Add((shift, FlyPass(arena, staged.Player, crosser)));
            }
        foreach (var (shift, pass) in passes)
        {
            var at = $"crosser start shifted by ({shift.x}, {shift.y}) m";
            Assert.True(pass.Swept, $"{at}: never crosses the bow within {CrossingSeconds} s");
            Assert.True(pass.Ahead.y > 0 && length(pass.Ahead) < CrossingMetres, $"{at}: crosses astern or beyond {CrossingMetres} m");
            Assert.InRange(pass.Seconds, CrossingEarliest, CrossingLatest);
            Assert.InRange(length(pass.Ahead), 30f, 60f);
            Assert.InRange(pass.RelativeSpeed, 20f, 30f);
            Assert.InRange(pass.AngularRate, 0.3f, 0.7f);
        }

        // The shifts took effect: starting 20 m further out along the crosser's track delays the pass, and starting 20 m further
        // ahead moves it 20 m further ahead (a sweep that stages the same pass eighteen times proves nothing).
        var along = passes.Take(9).Select(entry => entry.pass).ToList();
        var across = passes.Skip(9).Select(entry => entry.pass).ToList();
        Assert.True(along[0].Seconds - along[8].Seconds > .5f, "a start 20 m further out delays the pass by more than half a second");
        Assert.InRange(across[8].Ahead.y - across[0].Ahead.y, 3f, 30f);
    }

    // Where the crosser's bearing from the player sweeps from port to starboard: when, where it is then (x port-to-starboard,
    // y ahead), its speed relative to the player and the angular rate of its bearing in radians per second.
    private readonly record struct CrossingPass(bool Swept, float Seconds, float2 Ahead, float RelativeSpeed, float AngularRate);

    private static CrossingPass FlyPass(Zone arena, Ship player, Entity crosser)
    {
        for (var tick = 0; tick < 60 * CrossingSeconds; tick++)
        {
            var before = crosser.Position - player.Position;
            arena.Update(1f / 60f);
            var after = crosser.Position - player.Position;
            if (before.x < 0 && after.x >= 0)
            {
                var at = float2(after.x, after.z);
                var relative = crosser.Velocity - player.Velocity;
                return new CrossingPass(true, (tick + 1) / 60f, at, length(relative), abs(at.x * relative.y - at.y * relative.x) / dot(at, at));
            }
        }

        return default;
    }

    // Place with no velocity leaves the entity at rest: the unpiloted waiting Djinni and both Shakedown targets start with
    // zero velocity, so their stated places are where they are.
    [Fact]
    public void PlacedWithoutVelocityStartsAtRest()
    {
        var (_, _, crossing, crossingFailures) = Launch(new Crossing(), Inputs(() => GalaxySeed));
        Assert.True(crossingFailures.Count == 0, string.Join("; ", crossingFailures));
        Assert.Equal(float2.zero, crossing.Entities[1].Velocity);
        var (_, _, shakedown, shakedownFailures) = Launch(new DjinniShakedown(), Inputs(() => GalaxySeed));
        Assert.True(shakedownFailures.Count == 0, string.Join("; ", shakedownFailures));
        Assert.All(shakedown.Entities, entity => Assert.Equal(float2.zero, entity.Velocity));
    }

    private const int CrossingSeconds = 6;
    private const float CrossingMetres = 150f;
    private const float CrossingEarliest = 3.0f, CrossingLatest = 4.2f;

    // Djinni Shakedown: the generated Djinni carries a gun on a Ballistic hardpoint (not a launcher); that mount bears on
    // Ahead and not on Abeam, and both targets are neutral.
    [Fact]
    public void DjinniShakedownTargets()
    {
        var (_, _, staged, failures) = Launch(new DjinniShakedown(), Inputs(() => GalaxySeed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        var player = staged.Player;
        var barrels = player.Equipment.Where(item => item.Hardpoint?.Type == HardpointType.Ballistic
                                                     && item.Behaviors.OfType<Weapon>().Any(weapon => !(weapon.WeaponData is LauncherData))).ToList();
        Assert.NotEmpty(barrels);

        Assert.Equal(2, staged.Entities.Count);
        var ahead = staged.Entities[0];
        var abeam = staged.Entities[1];
        var facing = player.Direction;
        var perpendicular = new float2(facing.y, -facing.x);
        Assert.Equal(400f, length(ahead.Position.xz), .01f);
        Assert.Equal(400f, length(abeam.Position.xz), .01f);
        Assert.Equal(1f, dot(normalize(ahead.Position.xz), facing), .001f);
        Assert.Equal(1f, abs(dot(normalize(abeam.Position.xz), perpendicular)), .001f);
        Assert.Equal(("Longinus", "Longinus"), (_items.GetData(ahead.Hull).Name, _items.GetData(abeam.Hull).Name));
        foreach (var barrel in barrels)
        {
            Assert.True(FireControl.InArc(barrel, ahead.Position - player.Position), $"{barrel.Data.Name} cannot bear on Ahead");
            Assert.False(FireControl.InArc(barrel, abeam.Position - player.Position), $"{barrel.Data.Name} bears on Abeam");
        }
        Assert.False(player.IsHostileTo(ahead) || ahead.IsHostileTo(player) || player.IsHostileTo(abeam) || abeam.IsHostileTo(player),
            "both targets are neutral");
    }
}

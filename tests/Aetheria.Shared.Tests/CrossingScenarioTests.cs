using System.Linq;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;

// The arenas the operator flies to check the evasion term and the articulated barrel (map: Scenarios for operator
// checks). Each test launches the scenario the way New Game does and reads what it staged, so a script that stops
// staging what its brief claims fails here.
public sealed partial class RunStartTests
{
    // Crossing: the demo's cast; plight and FastBlast+- fitted in separate weapon groups with the Fire Control Array
    // aboard; a piloted hostile Djinni that crosses the bow inside 400 m; and an unpiloted neutral one inside plight's
    // arc, short of its Range and beyond three quarters of it.
    [Fact]
    public void CrossingConditions()
    {
        var (galaxy, arena, staged, failures) = Launch(new Crossing(), Inputs(() => GalaxySeed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        Assert.Equal(DemoTerminus.Cast.ZoneCount, galaxy.Zones.Length);
        Assert.Equal(4, galaxy.HomeZones.Count);

        var player = staged.Player;
        var plight = player.Equipment.Single(item => item.Data.Name == "plight");
        var small = player.Equipment.Single(item => item.Data.Name == "FastBlast+-");
        Assert.Contains(player.Equipment, item => item.Data.Name == "Fire Control Array");
        var groupOf = new System.Func<EquippedItem, int>(item => System.Array.FindIndex(player.WeaponGroups, group => group.items.Contains(item)));
        Assert.True(groupOf(plight) >= 0 && groupOf(small) >= 0, "both guns are in a weapon group");
        Assert.NotEqual(groupOf(plight), groupOf(small));

        Assert.Equal(2, staged.Entities.Count);
        var crosser = staged.Entities[0];
        var waiting = staged.Entities[1];
        Assert.IsType<Ship>(crosser);
        Assert.IsType<Ship>(waiting);

        Assert.True(player.IsHostileTo(crosser) && crosser.IsHostileTo(player), "the crosser is hostile both ways");
        Assert.Equal(1, AgentsOf(arena, crosser));
        var toCrosser = crosser.Position - player.Position;
        Assert.True(length(toCrosser) < 400f, "the crosser starts inside 400 m");
        Assert.True(toCrosser.z > 0, "the crosser starts ahead");
        Assert.True(abs(dot(crosser.Direction, player.Direction)) <= sin(radians(30f)) + 1e-4f,
            "the crosser faces within 30 degrees of perpendicular to the player's bow");

        Assert.False(player.IsHostileTo(waiting) || waiting.IsHostileTo(player), "the waiting Djinni is neutral");
        Assert.Equal(0, AgentsOf(arena, waiting));
        var toWaiting = waiting.Position - player.Position;
        EvSettle(player); // a weapon's Range is read once the ship's stats have settled; the geometry above is as staged
        var range = plight.Behaviors.OfType<Weapon>().Single().Range;
        Assert.True(FireControl.InArc(plight, toWaiting), "the waiting Djinni is inside plight's arc");
        Assert.True(length(toWaiting) <= range, "the waiting Djinni is inside plight's Range");
        Assert.True(length(toWaiting) > .75f * range, "the waiting Djinni is beyond three quarters of plight's Range");
    }

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
        Assert.Equal((DjinniShakedown.Ahead, DjinniShakedown.Abeam), (ahead.Position.xz, abeam.Position.xz));
        foreach (var barrel in barrels)
        {
            Assert.True(FireControl.InArc(barrel, ahead.Position - player.Position), $"{barrel.Data.Name} cannot bear on Ahead");
            Assert.False(FireControl.InArc(barrel, abeam.Position - player.Position), $"{barrel.Data.Name} bears on Abeam");
        }
        Assert.False(player.IsHostileTo(ahead) || ahead.IsHostileTo(player) || player.IsHostileTo(abeam) || abeam.IsHostileTo(player),
            "both targets are neutral");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

// aetheria-release, cut scenarios-smoke: what the Smoke Test Hull scenarios stage, read from the staged run and the
// shipped catalog's hull.
public sealed partial class RunStartTests
{
    private static bool Bears(EquippedItem mount, Entity player, Entity target) => FireControl.InArc(mount, target.Position - player.Position);

    private static List<EquippedItem> WeaponMounts(Entity ship) =>
        ship.Equipment.Where(item => item.Behaviors.OfType<Weapon>().Any()).ToList();

    // Weapon Feel: the player flies the Smoke Test Hull with Charged Scatter Blaster on the 2x2 Energy hardpoint, Autocannon
    // on the 2x2 Ballistic hardpoint and a burst weapon; the bare hull ahead lies in every mount's arc.
    [Fact]
    public void WeaponFeelConditions()
    {
        var staged = Stage(Arena(false), new WeaponFeel());
        var player = staged.Player;
        player.GenerateWeaponGroups();
        Assert.Equal("Smoke Test Hull", _items.GetData(player.EquippedHull.EquippableItem).Name);

        EquippedItem Mount(string design) => Assert.Single(WeaponMounts(player), item => _items.GetData(item.EquippableItem).Name == design);
        var charge = Mount("Charged Scatter Blaster");
        var autocannon = Mount("Autocannon");
        Assert.Equal((HardpointType.Energy, 2, 2), (charge.Hardpoint.Type, charge.Hardpoint.Shape.Width, charge.Hardpoint.Shape.Height));
        Assert.Equal((HardpointType.Ballistic, 2, 2), (autocannon.Hardpoint.Type, autocannon.Hardpoint.Shape.Width, autocannon.Hardpoint.Shape.Height));

        var mounts = WeaponMounts(player);
        Assert.Contains(mounts, item => ((WeaponItemData)_items.GetData(item.EquippableItem)).WeaponModifiers.HasFlag(WeaponModifiers.RapidFire));
        foreach (var mount in mounts)
        {
            Assert.Contains(player.WeaponGroups, group => group.items.Contains(mount));
            Assert.True(Bears(mount, player, Assert.Single(staged.Entities)), $"{_items.GetData(mount.EquippableItem).Name} cannot bear on the hull ahead");
        }
    }

    // Arcs' flanks: each side mount bears on its own flank's hull and on nothing else, the forward mounts bear on the
    // bow and on neither flank, and the two side mounts sit in different weapon groups.
    [Fact]
    public void ArcsFlanks()
    {
        var staged = Stage(Arena(false), new Arcs());
        var player = staged.Player;
        player.GenerateWeaponGroups();
        var (bow, stern, port, starboard) = (staged.Entities[0], staged.Entities[1], staged.Entities[3], staged.Entities[4]);
        // The flank hulls lie abeam from the player's facing, read from the staged positions: within 5 degrees of the
        // perpendicular, starboard on the right of the facing and port on the left.
        var facing = player.Direction;
        var right = CultMath.math.float2(facing.y, -facing.x);
        float Abeam(Entity flank, CultMath.float2 side) => CultMath.math.dot(CultMath.math.normalize(flank.Position.xz - player.Position.xz), side);
        var abeam = CultMath.math.cos(CultMath.math.radians(5f));
        Assert.True(Abeam(starboard, right) >= abeam, "the starboard hull is not abeam to starboard");
        Assert.True(Abeam(port, -right) >= abeam, "the port hull is not abeam to port");

        var mounts = WeaponMounts(player);
        var forward = mounts.Where(item => item.EquippableItem.Rotation == ItemRotation.None).ToList();
        var starboardMounts = mounts.Where(item => item.EquippableItem.Rotation == ItemRotation.Clockwise).ToList();
        var portMounts = mounts.Where(item => item.EquippableItem.Rotation == ItemRotation.CounterClockwise).ToList();
        Assert.NotEmpty(forward);
        Assert.NotEmpty(starboardMounts);
        Assert.NotEmpty(portMounts);
        foreach (var mount in starboardMounts)
        {
            Assert.True(Bears(mount, player, starboard), "a starboard mount cannot bear on the starboard hull");
            Assert.False(Bears(mount, player, port) || Bears(mount, player, bow) || Bears(mount, player, stern), "a starboard mount bears off its flank");
        }
        foreach (var mount in portMounts)
        {
            Assert.True(Bears(mount, player, port), "a port mount cannot bear on the port hull");
            Assert.False(Bears(mount, player, starboard) || Bears(mount, player, bow) || Bears(mount, player, stern), "a port mount bears off its flank");
        }
        foreach (var mount in forward)
        {
            Assert.True(Bears(mount, player, bow), "a forward mount cannot bear on the bow hull");
            Assert.False(Bears(mount, player, port) || Bears(mount, player, starboard), "a forward mount bears on a flank");
        }

        int GroupOf(EquippedItem mount) => Array.FindIndex(player.WeaponGroups, group => group.items.Contains(mount));
        var starboardGroups = starboardMounts.Select(GroupOf).ToList();
        var portGroups = portMounts.Select(GroupOf).ToList();
        Assert.DoesNotContain(-1, starboardGroups.Concat(portGroups));
        Assert.Empty(starboardGroups.Intersect(portGroups));
    }
}

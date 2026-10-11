using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using int2 = CultMath.int2;

// The Pursuit scenario (aetheria-release cut scenarios-pursuit): a Mine Launcher fitted, consumables in the hold, and
// a hostile on the player's tail. Each test launches or stages through the production path, so what it observes is what
// the menu's button builds. The fixture is RunStartTests'.
public sealed partial class RunStartTests
{
    private IEnumerable<CraftedItemInstance> Held(Entity entity) => entity.CargoBays.SelectMany(bay => bay.Cargo.Keys).OfType<CraftedItemInstance>();

    private int HeldOf(Entity entity, string design) => Held(entity).Count(item => _items.GetData(item).Name == design);

    private static float2 Ground(Entity entity) => float2(entity.Position.x, entity.Position.z);

    // The arena the Pursuit button builds stages what the scenario claims: the cast is the demo's, the Mine Launcher is
    // fitted on a Launcher cell and sits in a weapon group, the hold has the consumables, and the pursuer is piloted,
    // hostile both ways, astern and facing the player inside the range a fresh vent cloud breaks a sight line.
    [Fact]
    public void PursuitStages()
    {
        var (galaxy, arena, staged, failures) = Launch(new Pursuit(), Inputs(() => GalaxySeed));
        Assert.Empty(failures);

        var cast = DemoTerminus.Cast;
        var named = new[] { cast.ProtagonistFaction, cast.AntagonistFaction, cast.BufferFaction }.Concat(cast.NeutralFactions);
        Assert.Equal(named.Select(galaxy.ResolveFaction), galaxy.Factions);
        Assert.Equal(cast.ZoneCount, galaxy.Zones.Length);

        var player = staged.Player;
        arena.Update(0); // weapon groups assemble on the first update
        var mines = player.Equipment.Single(item => _items.GetData(item.EquippableItem).Name == "Mine Launcher");
        Assert.Equal(HardpointType.Launcher, player.Hardpoints[mines.Position.x, mines.Position.y].Type);
        Assert.Contains(player.WeaponGroups, group => group.items.Contains(mines));

        Assert.Equal(3, HeldOf(player, "Coolant Vent"));
        Assert.Equal(2, HeldOf(player, "Thruster Overdrive"));
        Assert.All(Held(player), item => Assert.IsType<ConsumableItem>(item));

        var pursuer = Assert.Single(staged.Entities);
        Assert.Equal(1, AgentsOf(arena, pursuer));
        Assert.True(player.IsHostileTo(pursuer) && pursuer.IsHostileTo(player), "hostile both ways");
        var offset = Ground(pursuer) - Ground(player);
        Assert.True(dot(player.Direction, offset) < 0, "the pursuer is not astern");
        Assert.True(dot(pursuer.Direction, -offset) > 0, "the pursuer is not facing the player");
        // VapourCloudTests: a fresh vent breaks a lock at 400 and a close sensor burns through at 40.
        Assert.InRange(length(offset), 40f, 400f);
    }

    // Cargo names a consumable design as well as an equippable one; a name that is neither fails by name and admits
    // nothing.
    [Fact]
    public void ACargoConsumableIsStaged()
    {
        var oneCell = _cache.GetAll<GearData>()
            .Where(design => design.Shape.Coordinates.Length == 1)
            .Where(design => _cache.GetAll<FactionProductData>().Any(p => p.Design.Key.Equals(_cache.RefOf(design).Key)))
            .OrderBy(design => design.Name, StringComparer.Ordinal)
            .First();

        var player = Stage(Arena(true), new Scripted(true, stage =>
            stage.Cargo(stage.Player(stage.Generated("Djinni"), float2(0, 0)), "Coolant Vent", oneCell.Name))).Player;
        var vents = Held(player).Where(item => _items.GetData(item).Name == "Coolant Vent").ToList();
        Assert.IsType<ConsumableItem>(Assert.Single(vents));
        Assert.IsType<EquippableItem>(Assert.Single(Held(player), item => _items.GetData(item).Name == oneCell.Name));

        var zone = Arena(true);
        var failures = Refused(zone, new Scripted(true, stage =>
            stage.Cargo(stage.Player(stage.Generated("Djinni"), float2(0, 0)), "Coolant Vent", "No Such Thing")));
        Assert.Contains(failures, failure => failure.Contains("no design named No Such Thing"));
    }

    // A vent staged in the hold is the real consumable: activating it makes a cloud on the venter.
    [Fact]
    public void TheStagedVentMakesACloud()
    {
        var (_, arena, staged, failures) = Launch(new Pursuit(), Inputs(() => GalaxySeed));
        Assert.Empty(failures);
        var player = staged.Player;
        var vent = _items.ItemData.GetAll<ConsumableItemData>().Single(design => design.Name == "Coolant Vent");

        arena.Update(0);
        Assert.True(player.TryActivateConsumable(vent));
        arena.Update(.1f);

        var cloud = Assert.Single(arena.Clouds);
        Assert.True(length(cloud.Body.Position - Ground(player)) <= cloud.Radius, "the cloud is not on its venter");
        Assert.Equal(2, HeldOf(player, "Coolant Vent"));
    }
}

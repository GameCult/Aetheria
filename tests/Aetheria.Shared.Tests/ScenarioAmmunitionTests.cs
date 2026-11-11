using System.Collections.Generic;
using System.Linq;
using UniRx;
using Xunit;

// Scenarios that author a weapon's ammunition-fed crates in a hold (ruling ballistics-real-ammo): the staged hold is what the weapon reloads from, so a
// scenario whose check rests on a weapon firing past its first magazine states the crates it needs. Each test launches through the production path.
public sealed partial class RunStartTests
{
    // The Arcs turret fits two Light Machine Guns of a hundred rounds and a bay of Ammo: its hold holds what the scenario states, and across a minute
    // in range of the player it fires past both first magazines, each reload taking a crate. Kills: a turret fit without a bay or with no authored
    // crates (silent after one magazine), and Cargo failing to admit a commodity.
    [Fact]
    public void TheArcsTurretKeepsFiringPastItsFirstMagazine()
    {
        var (_, arena, staged, failures) = Launch(new Arcs(), Inputs(() => GalaxySeed));
        Assert.Empty(failures);
        var player = staged.Player;
        var turret = Assert.IsType<OrbitalEntity>(staged.Entities[2]);
        var guns = turret.Weapons.ToList();
        Assert.Equal(2, guns.Count);
        var ammo = guns[0].WeaponData.AmmoType.Key;
        Assert.Equal(Arcs.TurretAmmoCrates, turret.CountItemsInCargo(ammo));

        // The guns are Auto weapons: they fire on a hostile inside their range (400 m) with no pilot. The player starts 775 m off, so it flies up to 200 m.
        arena.Update(0);
        player.Position = turret.Position + new CultMath.float3(-200, 0, 0);
        var shots = 0;
        using var sub = arena.ShotCommitted.Subscribe(outcome => { if (ReferenceEquals(outcome.Source, turret)) shots++; });
        for (var tick = 0; tick < System.MathF.Round(60f / SimClock.GameStep); tick++) arena.Update(SimClock.GameStep);

        var firstMagazines = guns.Sum(gun => gun.WeaponData.MagazineSize);
        Assert.True(shots > firstMagazines, $"the turret fired {shots} rounds, no more than its two first magazines of {firstMagazines}");
        Assert.True(turret.CountItemsInCargo(ammo) < Arcs.TurretAmmoCrates, "the turret reloaded without taking a crate");
    }

    // The Pursuit player's Mine Launcher draws its magazine of six from crates of Mines it carries beside the consumables: it lays past six, and the
    // crate count falls by one per reload. Kills: authored cargo with no Mines (at most six mines), a reload that takes no crate.
    [Fact]
    public void ThePursuitPlayerLaysMoreThanOneMagazineOfMinesFromItsCrates()
    {
        var (_, arena, staged, failures) = Launch(new Pursuit(), Inputs(() => GalaxySeed));
        Assert.Empty(failures);
        var player = staged.Player;
        var launcher = player.Weapons.Single(weapon => weapon.WeaponData is MineLayerData);
        var mines = launcher.WeaponData.AmmoType.Key;
        Assert.Equal(Pursuit.MineCrates, player.CountItemsInCargo(mines));
        Assert.Equal(3, HeldOf(player, "Coolant Vent"));

        arena.Update(0);
        var laid = 0;
        using var sub = arena.Mines.ObserveAdd().Subscribe(_ => laid++);
        var reloaded = new List<int>();
        var last = Pursuit.MineCrates;
        for (var tick = 0; tick < System.MathF.Round(120f / SimClock.GameStep) && laid <= 18; tick++)
        {
            launcher.Activate();
            arena.Update(SimClock.GameStep);
            var crates = player.CountItemsInCargo(mines);
            if (crates != last) { reloaded.Add(last - crates); last = crates; }
        }

        Assert.True(laid > launcher.WeaponData.MagazineSize, $"the player laid {laid} mines, no more than one magazine of {launcher.WeaponData.MagazineSize}");
        Assert.NotEmpty(reloaded);
        Assert.All(reloaded, step => Assert.Equal(1, step));
    }
}

using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using Xunit;

// How much ammunition a staged scenario really spends over a run (ruling ballistics-real-ammo): the crates a scenario authors are pinned to
// this measurement, not to an estimate. A run is RunSeconds of continuous play: the player holds every ammunition-drawing weapon down with its
// hostile-or-flank hull standing in weapon range, an AI shooter fires on its own, and every hull keeps its armour so no shooter or target leaves
// the run early. The draw is counted in crates (one reload per crate) and each drawn crate is put back, so the count is not capped by the stock.
public sealed partial class RunStartTests
{
    // Ten minutes: longer than any session the Arcs and Pursuit briefs ask a tester for.
    public const float RunSeconds = 600f;

    // Crates each armed entity of the scenario draws over the run, keyed by (index in the stage's entities, -1 for the player; ammunition name),
    // and the crates each held when the run began.
    private (Dictionary<(int, string), int> drawn, Dictionary<(int, string), int> stocked) RunAmmunition(Scenario scenario, bool playerFires, bool standNearHostile)
    {
        var (_, arena, staged, failures) = Launch(scenario, Inputs(() => GalaxySeed));
        Assert.Empty(failures);
        arena.Update(0);
        var shooters = staged.Entities.Select((entity, index) => (entity, index)).Append((staged.Player, -1))
            .Where(s => s.Item1.Weapons.Any(weapon => weapon.WeaponData.AmmoType.IsSet())).ToList();
        var drawn = new Dictionary<(int, string), int>();
        var stocked = new Dictionary<(int, string), int>();
        var held = new Dictionary<(int, string), int>();
        string NameOf(CultRecordKey key) => _items.ItemData.Get<SimpleCommodityData>(key).Name;
        foreach (var (entity, index) in shooters)
            foreach (var key in entity.Weapons.Where(weapon => weapon.WeaponData.AmmoType.IsSet()).Select(weapon => weapon.WeaponData.AmmoType.Key).Distinct())
            {
                stocked[(index, NameOf(key))] = entity.CountItemsInCargo(key);
                // A shooter with no crate cannot reload at all, so a run could not show what it would draw: it is given one to start.
                if (stocked[(index, NameOf(key))] == 0)
                    entity.CargoBays.First().TryStore(new SimpleCommodity { Data = new CultRecordRef<ItemData>(key), Quantity = 1 });
                drawn[(index, NameOf(key))] = 0;
                held[(index, NameOf(key))] = entity.CountItemsInCargo(key);
            }
        var everyone = staged.Entities.Append(staged.Player).ToList();
        var player = staged.Player;
        var sound = everyone.SelectMany(entity => entity.Equipment).ToDictionary(item => item, item => item.EquippableItem.Durability);
        var hostile = standNearHostile ? staged.Entities.First(entity => player.IsHostileTo(entity)) : null;
        for (var tick = 0; tick < System.MathF.Round(RunSeconds / SimClock.GameStep); tick++)
        {
            foreach (var entity in everyone)
                for (var x = 0; x < entity.Armor.GetLength(0); x++)
                    for (var y = 0; y < entity.Armor.GetLength(1); y++)
                        entity.Armor[x, y] = entity.MaxArmor[x, y];
            foreach (var (item, durability) in sound) item.EquippableItem.Durability = durability;
            if (hostile != null) player.Position = hostile.Position + CultMath.math.float3(-200, 0, 0);
            if (playerFires)
                foreach (var weapon in player.Weapons.Where(weapon => weapon.WeaponData.AmmoType.IsSet())) weapon.Activate();
            arena.Update(SimClock.GameStep);
            if (false) System.IO.File.AppendAllText(System.Environment.GetEnvironmentVariable("TMPDIR") + "/dbg.txt", $"{scenario.Name} t={tick*SimClock.GameStep:F0} active={player.Active} me={player.Position} " + string.Join(",", player.Weapons.Select(w => w.WeaponData.AmmoType.IsSet() ? w.Ammo + "/" + w.Reserve : "e")) + "\n");
            foreach (var (entity, index) in shooters)
                foreach (var key in entity.Weapons.Where(weapon => weapon.WeaponData.AmmoType.IsSet()).Select(weapon => weapon.WeaponData.AmmoType.Key).Distinct())
                {
                    var slot = (index, NameOf(key));
                    var spent = held[slot] - entity.CountItemsInCargo(key);
                    if (spent <= 0) continue;
                    drawn[slot] += spent;
                    for (var crate = 0; crate < spent; crate++)
                        entity.CargoBays.First().TryStore(new SimpleCommodity { Data = new CultRecordRef<ItemData>(key), Quantity = 1 });
                }
        }
        return (drawn, stocked);
    }

    // The most crates each shooter draws in any of the modes, and what it was stocked with.
    private List<(int shooter, string ammo, int drawn, int stocked)> WorstRun(Scenario scenario, params (bool playerFires, bool standNearHostile)[] modes)
    {
        var worst = new Dictionary<(int, string), (int drawn, int stocked)>();
        foreach (var (fires, near) in modes)
        {
            var (drawn, stocked) = RunAmmunition(scenario, fires, near);
            foreach (var slot in drawn.Keys)
                worst[slot] = (System.Math.Max(drawn[slot], worst.TryGetValue(slot, out var seen) ? seen.drawn : 0), stocked[slot]);
        }
        return worst.Select(kv => (kv.Key.Item1, kv.Key.Item2, kv.Value.drawn, kv.Value.stocked)).ToList();
    }

    // Arcs, run to its end twice: the player holding every gun down, and the player standing in the hostile turret's range while the turret
    // shoots. Kills: a crate count nobody measured (the turret's 30 and the player's none were estimates), a scenario shooter stocked below
    // what a run draws so its guns go silent, and a harness that measures nothing.
    [Fact]
    public void ArcsStocksEveryShooterForARun()
    {
        var run = WorstRun(new Arcs(), (true, false), (false, true));
        Assert.Contains(run, r => r.shooter == 2 && r.ammo == "Ammo" && r.drawn > 0);
        Assert.Contains(run, r => r.shooter == -1 && r.ammo == "Ammo" && r.drawn > 0);
        Assert.All(run, r => Assert.True(r.stocked >= r.drawn, $"Arcs shooter {r.shooter} drew {r.drawn} {r.ammo} over a run and was stocked {r.stocked}"));
    }

    // Pursuit, the player holding every gun down and the pursuer shooting back, then the player idle. Kills: as above, for the Mine Launcher's
    // crates and the pursuer's.
    [Fact]
    public void PursuitStocksEveryShooterForARun()
    {
        var run = WorstRun(new Pursuit(), (true, false), (false, false));
        Assert.Contains(run, r => r.shooter == -1 && r.ammo == "Mines" && r.drawn > 0);
        Assert.Contains(run, r => r.shooter == 0 && r.ammo == "Ammo" && r.drawn > 0);
        Assert.All(run, r => Assert.True(r.stocked >= r.drawn, $"Pursuit shooter {r.shooter} drew {r.drawn} {r.ammo} over a run and was stocked {r.stocked}"));
    }
}

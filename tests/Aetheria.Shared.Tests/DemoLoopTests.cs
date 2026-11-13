/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Linq;
using System.Collections.Generic;
using CultMath;
using UniRx;
using Xunit;

// The demo loop on the shipped catalog (aetheria-release, mining 5a and the ore sheet): the player's own gun breaks a rock
// through the trigger, the ore lands in the hold, the Pirates station Zenith buys it, repairs the hull and sells a unit back.
public sealed partial class RunStartTests
{
    [Fact]
    public void DemoLoop_mine_sell_repair_buy()
    {
        var (galaxy, arena, staged, failures) = Launch(new DemoTerminus(), Inputs(() => 1));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        var player = staged.Player;
        var belt = arena.Planets.Single(body => body.Value is AsteroidBeltData);
        var rock = new ChunkId(belt.Key, 0);
        var laser = player.Equipment.Select(equipped => equipped.Behaviors.OfType<Weapon>().FirstOrDefault())
            .First(weapon => weapon != null && weapon.Item.EquippableItem != null && _items.GetData(weapon.Item.EquippableItem).Name == "Light Laser");

        // Once the ship's stats have settled.
        for (var tick = 0; tick < 50; tick++) arena.Update(.02f);
        // The belt drifts and the sun's well pulls, so the ship is held where it was put: forty units off the rock.
        void Hold()
        {
            var at = arena.ChunkPose(rock.Field, rock.Index).xy;
            player.Position = new float3(at.x, player.Position.y, at.y - 40f);
            player.Velocity = float2.zero;
            player.Direction = new float2(0, 1);
            player.Aim = new float3(0, 0, 1);
        }
        Hold();
        arena.Update(.02f);
        Hold();
        Assert.True(player.SetTarget(rock), "the rock is a target");
        var gains = new List<CargoGain>();
        arena.CargoGained.Subscribe(gains.Add);
        laser.Activate();
        for (var elapsed = 0f; elapsed < 20f && arena.ChunkExists(rock); elapsed += .02f) { Hold(); arena.Update(.02f); }
        var info = player.ChunkInfo(rock);
        Assert.False(arena.ChunkExists(rock), $"the laser broke the rock: designated {FireControl.Designated(laser, player, rock, out var range)} range {range} info {info} target {player.Target} pos {player.Position} rockpos {arena.ChunkPose(rock.Field, rock.Index)} hp {arena.Settings.AsteroidHitpoints.Evaluate(arena.AsteroidBelts[rock.Field].Data.Asteroids[0].Size)}");

        var ores = new[] { "Rock", "New Metals", "Gold" };
        var held = player.CargoBays.SelectMany(bay => bay.Cargo.Keys.OfType<SimpleCommodity>().Select(stack => (bay, stack)))
            .Where(h => ores.Contains(_items.GetData(h.stack).Name)).ToArray();
        Assert.NotEmpty(held);
        Assert.Empty(arena.FloatingItems);
        // The feed saw every unit that went into the hold and none float.
        Assert.NotEmpty(gains);
        Assert.All(gains, gain => Assert.Equal((GainKind.Mined, 0), (gain.Kind, gain.Adrift)));
        Assert.Equal(held.Sum(h => h.stack.Quantity), gains.Sum(gain => gain.Stored));

        var zenith = arena.Entities.Single(entity => entity.HullData.Name == "Zenith");
        var credits = 0;
        // Every mined stack goes in one sale: the market takes it all, whatever the full hold of Zenith has room for.
        var value = StationServices.OreValue(_items, player.CargoBays);
        Assert.True(value > 0);
        Assert.Equal(gains.Sum(gain => gain.Stored), StationServices.TrySellOre(player.CargoBays, zenith, ref credits));
        Assert.Equal(value, credits);
        Assert.DoesNotContain(player.CargoBays.SelectMany(bay => bay.Cargo.Keys.OfType<SimpleCommodity>()), c => ores.Contains(_items.GetData(c).Name));

        var cell = Enumerable.Range(0, player.MaxArmor.GetLength(0)).SelectMany(x => Enumerable.Range(0, player.MaxArmor.GetLength(1)).Select(y => (x, y)))
            .First(c => player.MaxArmor[c.x, c.y] > 0);
        player.Armor[cell.x, cell.y] = 0;
        var cost = StationServices.RepairCost(_items, player);
        Assert.True(cost > 0);
        var paid = credits;
        credits += cost;
        Assert.True(StationServices.TryRepair(player, ref credits));
        Assert.Equal(paid, credits);
        Assert.Equal(player.MaxArmor[cell.x, cell.y], player.Armor[cell.x, cell.y]);

        // Zenith's whole stock is one 16-cell reactor, which fits only an empty 4x4: the player jettisons what the hold
        // still carries, then buys it.
        var hold = player.CargoBays[0];
        foreach (var leftover in hold.Cargo.Keys.ToArray()) hold.Remove(leftover);
        var stock = zenith.CargoBays.SelectMany(bay => bay.Cargo.Keys.OfType<CraftedItemInstance>().Select(piece => (bay, piece))).Single();
        credits = StationServices.BuyPrice(_items, stock.piece);
        Assert.Equal(BuyResult.Bought, StationServices.TryBuy(_items, stock.piece, 1, ref credits,
            () => stock.bay.TryTransferItem(hold, stock.piece)));
        Assert.Equal(0, credits);
        Assert.Contains(stock.piece, hold.Cargo.Keys);
    }
}

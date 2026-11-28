using System;
/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Linq;
using System.Collections.Generic;
using CultMath;
using GameCult.Caching;
using UniRx;
using Xunit;

// The demo loop on the shipped catalog (aetheria-release, mining 5a): the player's own gun breaks a rock
// through the trigger, the ore lands in the hold, the Pirates' refinery buys it and repairs the hull, and Zenith, Aeronautics Unlimited's trade hub, sells a unit back.
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

        var ores = FieldKinds.Yield(((AsteroidBeltData) belt.Value).Kind, default, _cache).Select(yield => _cache.Get(yield.Commodity).Name).ToArray();
        var held = player.CargoBays.SelectMany(bay => bay.Cargo.Keys.OfType<SimpleCommodity>().Select(stack => (bay, stack)))
            .Where(h => ores.Contains(_items.GetData(h.stack).Name)).ToArray();
        Assert.NotEmpty(held);
        Assert.Empty(arena.FloatingItems);
        // The feed saw every unit that went into the hold and none float.
        Assert.NotEmpty(gains);
        Assert.All(gains, gain => Assert.Equal((GainKind.Mined, 0), (gain.Kind, gain.Adrift)));
        Assert.Equal(held.Sum(h => h.stack.Quantity), gains.Sum(gain => gain.Stored));

        var refinery = StationOfKind(arena, "processing");
        var zenith = StationOfKind(arena, "trade-hub");
        var credits = 0;
        // The refinery buys the mined ore as far as its bays hold it (ruling no-market-absorption): the units it took are paid for, and
        // what did not fit stays in the hold.
        var value = StationServices.OreValue(_items, player.CargoBays, refinery);
        Assert.True(value > 0);
        var mined = gains.Sum(gain => gain.Stored);
        var sale = StationServices.TrySellOre(player.CargoBays, refinery, ref credits);
        Assert.Equal(mined, sale.Offered);
        Assert.True(sale.Sold > 0, "the refinery has room for some of the ore");
        var left = player.CargoBays.SelectMany(bay => bay.Cargo.Keys.OfType<SimpleCommodity>()).Where(c => ores.Contains(_items.GetData(c).Name)).Sum(c => c.Quantity);
        Assert.Equal(mined - sale.Sold, left);
        if (left == 0) Assert.Equal(value, credits);
        else Assert.True(credits > 0 && credits < value);

        // Zenith's silo wants the ores of the haul order to the refinery: it buys what the hold still carries, and a fresh stack too.
        var zenithWants = StationWants.Of(zenith);
        Assert.NotEmpty(zenithWants);
        Assert.All(zenithWants, good => Assert.True(FieldKinds.IsOre(_cache, _cache.Get(new CultRecordRef<ItemData>(good)))));
        var silo = zenith.CargoBays.Single(bay => bay.Data is SiloData);
        var oreGood = zenithWants.OrderBy(good => good.Value, StringComparer.Ordinal).First();
        var fresh = new SimpleCommodity { Data = new CultRecordRef<ItemData>(oreGood), Quantity = 5 };
        Assert.True(player.CargoBays[0].TryStore(fresh));
        var atZenith = StationServices.TrySellPart(player.CargoBays[0], zenith, fresh, ref credits);
        Assert.Equal((5, SaleRefusal.None), (atZenith.Sold, atZenith.Refusal));
        Assert.Equal(5, silo.Count(oreGood));

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

        // Zenith's smallest piece may need an empty hold: the player jettisons what the hold still carries, then buys it.
        var hold = player.CargoBays[0];
        foreach (var leftover in hold.Cargo.Keys.ToArray()) hold.Remove(leftover);
        var stock = zenith.CargoBays.SelectMany(bay => bay.Cargo.Keys.OfType<CraftedItemInstance>().Select(piece => (bay, piece)))
            .MinBy(held => _items.GetData(held.piece).Shape.Cells.Cast<bool>().Count(occupied => occupied));
        credits = StationServices.BuyPrice(_items, stock.piece);
        Assert.Equal(BuyResult.Bought, StationServices.TryBuy(_items, stock.piece, 1, ref credits,
            () => stock.bay.TryTransferItem(hold, stock.piece, 1)));
        Assert.Equal(0, credits);
        Assert.Contains(stock.piece, hold.Cargo.Keys);
    }

    // What a hold of belt ore earns against what the loop spends it on, on the shipped catalog (aetheria-living-world, ruling
    // economy-numbers-are-sim-tuned-knobs: the prices are balance knobs tuned from simulation, so this pins only the scale). V is the
    // belt kind's hold-cell value: a unit is drawn by abundance, a cell takes MaxStack units, so a cell's value is
    // sum(abundance x price) / sum(abundance / MaxStack), over every yield of the kind. It is set against the repair of the player's hull
    // with all its armour lost and against the authored Price of the cheapest piece Zenith stocks (ruling demo-loop-reads-authored-price:
    // the rolled BuyPrice moves with any catalog change that shifts the stock draw, 47053 against 36806 for the same Ring Station Reactor
    // across design-bills, which is not what this test is about). Margin rule: the anchors are the readings the shipped catalog gives at
    // design-bills (V 282.149, repair 55404, cheapest authored price 15500; the price re-read at station-sale-by-wants as 2500, since generated stock stops at half the bay and the smaller, cheaper pieces then fit where the full bay refused them), and the band is 1/sqrt(10) to sqrt(10) of each, the
    // geometric midpoint between the shipped reading and the least mutant this test claims to catch (a divisor of ten on either side, a
    // ratio of 10 or 0.1): the shipped reading and that mutant each sit 3.16x from a bound.
    // The one station of a kind in the arena: the demo's two stations are told apart by what they are for, never by hull.
    private static OrbitalEntity StationOfKind(Zone arena, string kind) =>
        arena.Entities.OfType<OrbitalEntity>().Single(entity => entity.Kind.Key.Equals(StationKindData.KeyOf(kind)));

    private const double BaseRepairRatio = 0.0050925, BaseBuyRatio = 0.11286;
    private static readonly double Margin = Math.Sqrt(10);

    [Fact]
    public void DemoLoopIncomeToCostClosesAfterRescale()
    {
        var (_, arena, staged, failures) = Launch(new DemoTerminus(), Inputs(() => 1));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        var player = staged.Player;
        var belt = arena.Planets.Single(body => body.Value is AsteroidBeltData);
        var kind = _cache.Get(((AsteroidBeltData) belt.Value).Kind);
        var yields = kind.Yields.Select(y => (data: _cache.Get(y.Commodity), weight: (double) y.Abundance)).ToArray();
        var value = yields.Sum(y => y.weight * y.data.Price) / yields.Sum(y => y.weight / y.data.MaxStack);

        for (var x = 0; x < player.MaxArmor.GetLength(0); x++)
            for (var y = 0; y < player.MaxArmor.GetLength(1); y++)
                player.Armor[x, y] = 0;
        var repair = StationServices.RepairCost(_items, player);
        var zenith = StationOfKind(arena, "trade-hub");
        var cheapest = zenith.CargoBays.SelectMany(bay => bay.Cargo.Keys.OfType<CraftedItemInstance>()).Min(piece => _items.GetData(piece).Price);
        Assert.True(repair > 0 && cheapest > 0);

        var measured = $"belt {kind.Name}: V {value:0.###} per cell, repair {repair}, cheapest authored price {cheapest}, V/repair {value / repair:0.######}, V/buy {value / cheapest:0.######}";
        Assert.True(value / repair >= BaseRepairRatio / Margin && value / repair <= BaseRepairRatio * Margin, measured);
        Assert.True(value / cheapest >= BaseBuyRatio / Margin && value / cheapest <= BaseBuyRatio * Margin, measured);
    }
}

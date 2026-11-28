/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using Xunit;

// The demo's ore haul (aetheria-living-world, cut ore-haul-run; rulings transit-depots-buy-for-onward-haul, demo-zenith-ore-depot,
// no-market-absorption, comms-cells-are-zones, docking-is-enclosed-bays): a depot buys only into its silo, and a real hauler ship
// carries the silo's ore to the refinery under the standing haul order.
public sealed partial class RunStartTests
{
    // Finding zenith-keeps-buying-past-its-silo: with the silo full, a sale of order ore sells nothing and the stock bay does not
    // take it; the refinery still takes drawn ore into its one bay.
    [Fact]
    public void ZenithBuysOnlyIntoItsSilo()
    {
        var (_, arena, staged, failures) = Launch(new DemoTerminus(), Inputs(() => 1));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        var zenith = StationOfKind(arena, "trade-hub");
        var refinery = StationOfKind(arena, "processing");
        var player = staged.Player;
        var silo = zenith.CargoBays.Single(bay => bay.Data is SiloData);
        var stock = zenith.CargoBays.Single(bay => !(bay.Data is SiloData));
        var good = StationWants.Of(zenith).OrderBy(key => key.Value, StringComparer.Ordinal).First();
        Assert.Equal(new[] { silo }, StationWants.Bays(zenith, good));
        Assert.Empty(StationWants.Bays(zenith, new CultRecordKey("commodity:not-wanted")));

        while (silo.TryStore(new SimpleCommodity { Data = new CultRecordRef<ItemData>(good), Quantity = 10 })) { }
        var before = stock.Cargo.Select(held => (held.Key, held.Value)).ToArray();
        var hold = player.CargoBays[0];
        var offered = new SimpleCommodity { Data = new CultRecordRef<ItemData>(good), Quantity = 5 };
        Assert.True(hold.TryStore(offered));
        var credits = 0;
        var sale = StationServices.TrySellPart(hold, zenith, offered, ref credits);
        Assert.Equal((0, 5, SaleRefusal.NoRoom), (sale.Sold, sale.Offered, sale.Refusal));
        Assert.Equal(0, credits);
        Assert.Equal(5, hold.Count(good));
        Assert.Equal(before, stock.Cargo.Select(held => (held.Key, held.Value)).ToArray());
        Assert.Equal(0, stock.Count(good));

        var drawn = StationWants.Of(refinery).First(key => FieldKinds.IsOre(_cache, _cache.Get(new CultRecordRef<ItemData>(key))));
        Assert.Equal(refinery.CargoBays, StationWants.Bays(refinery, drawn));
        var ore = new SimpleCommodity { Data = new CultRecordRef<ItemData>(drawn), Quantity = 5 };
        Assert.True(hold.TryStore(ore));
        var taken = StationServices.TrySellPart(hold, refinery, ore, ref credits);
        Assert.Equal((5, SaleRefusal.None), (taken.Sold, taken.Refusal));
        Assert.True(credits > 0);
    }
}

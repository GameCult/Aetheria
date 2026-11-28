/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using UniRx;
using Xunit;
using float2 = CultMath.float2;

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

    private sealed class HaulWorld
    {
        public Zone Arena;
        public OrbitalEntity Zenith, Refinery;
        public Ship Player, Hauler;
        public EquippedCargoBay Silo;
        public HaulOrderData Order;
        public CultRecordKey Good;
        public RunStart.Staged Started;
    }

    // The demo entrance on a seed, with `silo` units of the order's ore sold into Zenith's silo.
    private HaulWorld HaulStage(uint seed = 1, int silo = 40)
    {
        var (_, arena, staged, failures) = Launch(new DemoTerminus(), Inputs(() => seed));
        Assert.True(failures.Count == 0, string.Join("; ", failures));
        var world = new HaulWorld
        {
            Arena = arena, Started = staged, Player = staged.Player,
            Zenith = StationOfKind(arena, "trade-hub"), Refinery = StationOfKind(arena, "processing"),
            Order = Assert.Single(_cache.GetAll<HaulOrderData>())
        };
        world.Hauler = Assert.Single(arena.Entities.OfType<Ship>(), ship => ship.Haul.Key.IsSet());
        world.Silo = world.Zenith.CargoBays.Single(bay => bay.Data is SiloData);
        world.Good = world.Order.Goods.Select(good => good.Key).OrderBy(key => key.Value, StringComparer.Ordinal).First();
        for (var left = silo; left > 0; left -= 10)
            Assert.True(world.Silo.TryStore(new SimpleCommodity { Data = new CultRecordRef<ItemData>(world.Good), Quantity = Math.Min(10, left) }));
        return world;
    }

    private static int Units(IEnumerable<EquippedCargoBay> bays, CultRecordKey good) => bays.Sum(bay => bay.Count(good));

    private static void Fly(Zone arena, float seconds, Action each = null)
    {
        for (var elapsed = 0f; elapsed < seconds; elapsed += .02f)
        {
            arena.Update(.02f);
            each?.Invoke();
        }
    }

    private static void Emptied(Ship ship)
    {
        foreach (var bay in ship.CargoBays)
            foreach (var stack in bay.Cargo.Keys.ToArray())
                bay.Remove(stack);
    }

    // The hauler's own pilot: the agent that flies it now (a dock call gives it a fresh one).
    private static Agent PilotOf(Zone arena, Ship ship) => arena.Agents.Single(agent => ReferenceEquals(agent.Ship, ship));

    // A real hauler carries Zenith's silo ore to the refinery under the standing order, docking to load and to unload; units of the
    // order's good are never minted (the refinery's facility only takes them), the silo falls and the refinery's bays gain.
    [Fact]
    public void AHaulerCarriesSiloOreToTheRefinery()
    {
        var world = HaulStage();
        var dockings = 0;
        world.Hauler.Docked.Subscribe(_ => dockings++);
        var start = Units(world.Zenith.CargoBays, world.Good) + Units(world.Refinery.CargoBays, world.Good);
        var refineryAtStart = Units(world.Refinery.CargoBays, world.Good);
        var refineryPeak = refineryAtStart;
        var stepped = 0;
        Fly(world.Arena, 240, () =>
        {
            var held = Units(world.Zenith.CargoBays, world.Good) + Units(world.Hauler.CargoBays, world.Good) + Units(world.Refinery.CargoBays, world.Good);
            Assert.True(held <= start, $"step {stepped}: {held} units of the good, {start} at the start: a unit was minted");
            refineryPeak = Math.Max(refineryPeak, Units(world.Refinery.CargoBays, world.Good));
            stepped++;
        });
        Assert.True(Units(new[] { world.Silo }, world.Good) < 40, "the silo gave up ore");
        Assert.True(refineryPeak > refineryAtStart, "the refinery's bays took delivered ore");
        Assert.True(dockings >= 2, $"the hauler docked {dockings} times: once to load and once to unload at least");
        Assert.Contains(world.Hauler, world.Arena.Entities);
        Assert.IsType<HaulingTask>(PilotOf(world.Arena, world.Hauler).Task);
    }

    // No field stores the leg: the order key is the task's only state, and the leg follows the hold as it is now.
    [Fact]
    public void TheLegIsTheHold()
    {
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly;
        Assert.Equal(new[] { "Order" }, typeof(HaulingTask).GetFields(flags).Select(field => field.Name));
        var world = HaulStage(silo: 0);
        var hold = world.Hauler.CargoBays[0];
        Assert.Equal(world.Order.From.Key, HaulRun.Leg(world.Hauler, world.Order));
        Assert.True(hold.TryStore(new SimpleCommodity { Data = new CultRecordRef<ItemData>(world.Good), Quantity = 5 }));
        Assert.Equal(world.Order.To.Key, HaulRun.Leg(world.Hauler, world.Order));

        // Steering follows it: from midway, a loaded hauler closes on the refinery and the same hauler with its hold hand-emptied turns for Zenith.
        float2 Toward(OrbitalEntity station) => CultMath.math.normalize(world.Arena.GetOrbitPosition(station.OrbitData) - world.Hauler.Position.xz);
        void Midway()
        {
            var between = (world.Arena.GetOrbitPosition(world.Zenith.OrbitData) + world.Arena.GetOrbitPosition(world.Refinery.OrbitData)) * .5f;
            world.Hauler.Position = new CultMath.float3(between.x, world.Hauler.Position.y, between.y);
            world.Hauler.Velocity = float2.zero;
        }
        Midway();
        Fly(world.Arena, 3);
        Assert.True(CultMath.math.dot(CultMath.math.normalize(world.Hauler.Velocity), Toward(world.Refinery)) > .5f, "a loaded hauler heads for the refinery");
        Emptied(world.Hauler);
        Midway();
        Fly(world.Arena, 3);
        Assert.True(CultMath.math.dot(CultMath.math.normalize(world.Hauler.Velocity), Toward(world.Zenith)) > .5f, "an emptied hauler heads for Zenith");
    }

    // A hauler docks only when the call would move a unit: with the silo empty it never docks and keeps its pilot; with the refinery's
    // bays full of something else, a loaded hauler keeps its cargo.
    [Fact]
    public void AHaulerDocksOnlyToMoveSomething()
    {
        var world = HaulStage(silo: 0);
        var dockings = 0;
        world.Hauler.Docked.Subscribe(_ => dockings++);
        var pilot = PilotOf(world.Arena, world.Hauler);
        Fly(world.Arena, 60);
        Assert.Equal(0, dockings);
        Assert.Same(pilot, PilotOf(world.Arena, world.Hauler));

        var wants = StationWants.Of(world.Refinery);
        var filler = _cache.GetAll<SimpleCommodityData>().First(data => !wants.Contains(_cache.RefOf(data).Key));
        foreach (var bay in world.Refinery.CargoBays)
            while (bay.TryStore(new SimpleCommodity { Data = new CultRecordRef<ItemData>(_cache.RefOf(filler).Key), Quantity = 1 })) { }
        Assert.Equal(0, Units(world.Refinery.CargoBays, world.Good));
        Assert.True(world.Hauler.CargoBays[0].TryStore(new SimpleCommodity { Data = new CultRecordRef<ItemData>(world.Good), Quantity = 5 }));
        Fly(world.Arena, 60);
        Assert.Equal(0, dockings);
        Assert.Equal(5, Units(world.Hauler.CargoBays, world.Good));
        Assert.Equal(0, Units(world.Refinery.CargoBays, world.Good));
    }

    // Delivery moves units and pays nobody: what the refinery gains is what the hauler loses, and a call that loads moves what the silo gives.
    [Fact]
    public void DeliveryPaysNobody()
    {
        var world = HaulStage(silo: 20);
        Emptied(world.Hauler);
        var credits = world.Started.Credits;
        HaulRun.Call(world.Hauler, world.Zenith);
        var carried = Units(world.Hauler.CargoBays, world.Good);
        Assert.True(carried > 0, "the call at the From station loaded the hauler");
        Assert.Equal(20 - carried, Units(new[] { world.Silo }, world.Good));
        Assert.Contains(world.Hauler, world.Arena.Entities);

        var before = Units(world.Refinery.CargoBays, world.Good);
        HaulRun.Call(world.Hauler, world.Refinery);
        var delivered = Units(world.Refinery.CargoBays, world.Good) - before;
        Assert.True(delivered > 0);
        Assert.Equal(carried - delivered, Units(world.Hauler.CargoBays, world.Good));
        Assert.Equal(credits, world.Started.Credits);
    }

    // Kill the loaded hauler: its stacks float, the stations are untouched, and nothing replaces it.
    [Fact]
    public void ALostHaulerLosesItsCargo()
    {
        var world = HaulStage(silo: 0);
        Assert.True(world.Hauler.CargoBays[0].TryStore(new SimpleCommodity { Data = new CultRecordRef<ItemData>(world.Good), Quantity = 7 }));
        var refinery = Units(world.Refinery.CargoBays, world.Good);
        world.Hauler.HeatstrokeDeath.OnNext(UniRx.Unit.Default);
        Assert.DoesNotContain(world.Hauler, world.Arena.Entities);
        Assert.Equal(7, world.Arena.FloatingItems.Where(floating => floating.Item is SimpleCommodity stack && stack.Data.Key.Equals(world.Good)).Sum(floating => ((SimpleCommodity) floating.Item).Quantity));
        Assert.Equal(0, Units(new[] { world.Silo }, world.Good));
        Assert.Equal(refinery, Units(world.Refinery.CargoBays, world.Good));
        Fly(world.Arena, 30);
        Assert.DoesNotContain(world.Arena.Entities.OfType<Ship>(), ship => ship.Haul.Key.IsSet());
    }

    // The order rides the pack: a restored ship carries it and gets the haul task at admission; a ship with no order patrols.
    [Fact]
    public void TheHaulSurvivesThePack()
    {
        var world = HaulStage(silo: 0);
        var pack = EntitySerializer.Pack(world.Hauler);
        Assert.Equal(HaulOrderData.KeyOf("demo-terminus-ore"), pack.Haul.Key);
        world.Hauler.HeatstrokeDeath.OnNext(UniRx.Unit.Default);
        var restored = (Ship) EntitySerializer.Unpack(_items, world.Arena, pack);
        Assert.Equal(pack.Haul.Key, restored.Haul.Key);
        world.Arena.Admit(restored, piloted: true);
        Assert.IsType<HaulingTask>(PilotOf(world.Arena, restored).Task);

        restored.Haul = default;
        var plain = (Ship) EntitySerializer.Unpack(_items, world.Arena, EntitySerializer.Pack(restored));
        Assert.Null(HaulRun.TaskOf(plain));
        world.Arena.Admit(plain, piloted: true);
        Assert.IsType<PatrolOrbitsTask>(PilotOf(world.Arena, plain).Task);
    }

    // A grudge on the hauler moves it off its leg; when the track is gone its task is the same HaulingTask object, and it flies on.
    [Fact]
    public void AHaulerFightsAndResumes()
    {
        var world = HaulStage(silo: 0);
        var pilot = PilotOf(world.Arena, world.Hauler);
        var task = pilot.Task;
        Assert.IsType<HaulingTask>(task);
        world.Player.Position = new CultMath.float3(world.Hauler.Position.x + 60, world.Hauler.Position.y, world.Hauler.Position.z);
        world.Hauler.EntityInfoGathered[world.Player] = (_items.GameplaySettings.TargetDetectionInfoThreshold + 1f) / 2f;
        world.Hauler.Provoke(world.Player);
        var left = false;
        Fly(world.Arena, 10, () => left |= !ReferenceEquals(pilot.Task, task) || pilot.View.TargetOf(world.Hauler) != null);
        Assert.True(left, "the grudge moved the hauler off its haul task");
        world.Player.HeatstrokeDeath.OnNext(UniRx.Unit.Default);
        Fly(world.Arena, 30);
        Assert.Same(task, pilot.Task);
        Assert.Null(pilot.View.TargetOf(world.Hauler));
    }
}

using System;
using System.Linq;
using CultMath;
using GameCult.Caching.MessagePack;
using MessagePack;
using Xunit;
using static CultMath.math;
using float3 = CultMath.float3;
using int2 = CultMath.int2;

// Electronic warfare, hide-launch (campaign aetheria-ew; invariants lie-is-own-emission and lies-cost-real-heat; ruling
// ew-imitation-availability-ruled makes this the hiding tier). A fitted suppressor thins its own ship launch flash inside
// Entity.SetSignature, the one writer of light, and pays for what it removed in its own cell. The fixtures drive that writer:
// real guns and beams fired inside Zone.Update for the reveal and the per-tick cost, and direct SetSignature calls (before any
// update, so no conduction blurs the cell) for the exact heat arithmetic. A gun Visibility is 5 and the observer reveals
// above .1 over its Eye, so a flash of 5 from 50 away is revealed (5 x 2 / (50 x .5) = .4) and a flash scaled by the
// Suppressor factor .1 is not (.04). Factor, HeatPerUnit and RatedTemperature (400) are authored in the constructor.
public sealed partial class ContactRevealTests
{
    private static EquippedItem SuppressorItem(Entity e, string dataName) =>
        e.Equipment.Single(i => i.Behaviors.Any(b => b is LaunchSuppressor) && i.Data.Name == dataName);

    private static EquippedItem BeamItem(Entity e) => e.Equipment.Single(i => i.Behaviors.Any(b => b is ConstantWeapon));

    private static int2 CellOf(EquippedItem item) => item.InsetShape.Coordinates.Single();

    private static float[,] Snapshot(Entity e) => (float[,]) e.Temperature.Clone();

    // Every cell of the hull except the named ones: the heat a suppressor pays must land in its own cell and nowhere else.
    private static void AssertOnlyCellsMoved(float[,] before, Entity e, params int2[] cells)
    {
        for (var x = 0; x < before.GetLength(0); x++)
        for (var y = 0; y < before.GetLength(1); y++)
            if (!cells.Contains(new int2(x, y)))
                Assert.Equal(before[x, y], e.Temperature[x, y]);
    }

    private static void AssertHeat(float expectedHeat, Entity e, int2 cell, float before)
    {
        var expected = expectedHeat / e.ThermalMass[cell.x, cell.y];
        var actual = e.Temperature[cell.x, cell.y] - before;
        Assert.InRange(actual, expected * .999f, expected * 1.001f);
    }

    // ---- ASuppressedLaunchIsNotRevealedWhileAControlLaunchIs ----

    [Fact]
    public void ASuppressedLaunchIsNotRevealedWhileAControlLaunchIs()
    {
        var control = Add(float3(30, 0, 40), new[] { "Gun" });
        var hidden = Add(float3(-30, 0, 40), new[] { "Gun" }, new[] { "Suppressor" });
        var o = Add(float3(0, 0, 0), new[] { "Eye" });

        Fire(control, Guns(control).Single());
        Fire(hidden, Guns(hidden).Single());

        var reveal = Assert.Single(o.RevealsOf(control));
        Assert.Equal(SignatureChannel.LaunchFlash, reveal.Channel);
        AssertBearing(.6f, .8f, reveal.Bearing);
        Assert.Empty(o.RevealsOf(hidden));
        var controlFlash = control.VisibilitySources[Guns(control).Single().Behaviors.Single(b => b is InstantWeapon)].Strength;
        var hiddenFlash = hidden.VisibilitySources[Guns(hidden).Single().Behaviors.Single(b => b is InstantWeapon)].Strength;
        Assert.Equal(5f, controlFlash, 3);
        Assert.Equal(.1f * controlFlash, hiddenFlash, 3); // Factor times the gun Visibility, whatever it is authored as
    }

    // ---- SuppressionHeatLandsInTheSuppressorsOwnCell ----

    [Fact]
    public void SuppressionHeatLandsInTheSuppressorsOwnCell()
    {
        var ship = Add(float3(0, 0, 0), null, new[] { "Suppressor" });
        var cell = CellOf(SuppressorItem(ship, "Suppressor"));
        var before = Snapshot(ship);

        ship.SetSignature(new object(), SignatureChannel.LaunchFlash, 5f, null);

        AssertHeat((5f - .5f) * 2f, ship, cell, before[cell.x, cell.y]); // removed 4.5 at 2 heat a unit
        AssertOnlyCellsMoved(before, ship, cell);
    }

    [Fact]
    public void ASuppressorThatRemovesNothingOrChargesNothingPaysNothing()
    {
        var free = Add(float3(0, 0, 0), null, new[] { "SuppressorCold" });
        var open = Add(float3(100, 0, 0), null, new[] { "SuppressorOpen" });
        var freeBefore = Snapshot(free);
        var openBefore = Snapshot(open);
        var source = new object();

        free.SetSignature(source, SignatureChannel.LaunchFlash, 5f, null);
        open.SetSignature(source, SignatureChannel.LaunchFlash, 5f, null);

        Assert.Equal(.5f, free.VisibilitySources[source].Strength, 3); // it still thins the flash...
        AssertOnlyCellsMoved(freeBefore, free);                        // ...at no cost with HeatPerUnit 0
        Assert.Equal(5f, open.VisibilitySources[source].Strength, 3);  // Factor 1 removes nothing
        AssertOnlyCellsMoved(openBefore, open);
    }

    [Fact]
    public void AFiredGunHeatsTheSuppressorsCellAboveItsFreeTwin()
    {
        var paying = Add(float3(30, 0, 40), new[] { "Gun" }, new[] { "Suppressor" });
        var twin = Add(float3(-30, 0, 40), new[] { "Gun" }, new[] { "SuppressorCold" });

        Fire(paying, Guns(paying).Single());
        Fire(twin, Guns(twin).Single());

        var p = CellOf(SuppressorItem(paying, "Suppressor"));
        var t = CellOf(SuppressorItem(twin, "SuppressorCold"));
        Assert.Equal(p, t); // the same fit puts them in the same cell
        Assert.True(paying.Temperature[p.x, p.y] > twin.Temperature[t.x, t.y]);
    }

    // ---- NothingIsSuppressedAboveRatedTemperature ----

    [Fact]
    public void NothingIsSuppressedAboveRatedTemperature()
    {
        var ship = Add(float3(0, 0, 0), null, new[] { "Suppressor" });
        var cell = CellOf(SuppressorItem(ship, "Suppressor"));
        var source = new object();
        ship.Temperature[cell.x, cell.y] = 600f; // past the rated 400
        var hot = Snapshot(ship);

        ship.SetSignature(source, SignatureChannel.LaunchFlash, 5f, null);

        Assert.Equal(5f, ship.VisibilitySources[source].Strength, 3); // the flash is as bright as a control
        AssertOnlyCellsMoved(hot, ship);                              // and the call paid nothing, in any cell
        Assert.Equal(600f, ship.Temperature[cell.x, cell.y]);

        ship.Temperature[cell.x, cell.y] = 300f; // cooled below
        ship.SetSignature(source, SignatureChannel.LaunchFlash, 5f, null);
        Assert.Equal(.5f, ship.VisibilitySources[source].Strength, 3);
        Assert.True(ship.Temperature[cell.x, cell.y] > 300f);
    }

    // ---- OnlyTheLaunchFlashChannelIsScaled ----

    [Fact]
    public void OnlyTheLaunchFlashChannelIsScaled()
    {
        var ship = Add(float3(0, 0, 0), null, new[] { "Suppressor" });
        var before = Snapshot(ship);
        var thermal = new object();
        var plume = new object();

        ship.SetSignature(thermal, SignatureChannel.Thermal, 5f, null);
        ship.SetSignature(plume, SignatureChannel.DrivePlume, 5f, null);

        Assert.Equal(5f, ship.VisibilitySources[thermal].Strength, 3);
        Assert.Equal(5f, ship.VisibilitySources[plume].Strength, 3);
        AssertOnlyCellsMoved(before, ship);
        Assert.Throws<ArgumentException>(() => ship.SetSignature(new object(), SignatureChannel.Emission, 5f, null));
    }

    // ---- AnUnpoweredOrSwitchedOffSuppressorDoesNothing ----

    [Fact]
    public void AnUnpoweredOrSwitchedOffSuppressorDoesNothing()
    {
        var ship = Add(float3(0, 0, 0), null, new[] { "Suppressor" });
        var item = SuppressorItem(ship, "Suppressor");
        var source = new object();
        item.Enabled.Value = false;
        Assert.False(item.Active.Value);
        var before = Snapshot(ship);

        ship.SetSignature(source, SignatureChannel.LaunchFlash, 5f, null);

        Assert.Equal(5f, ship.VisibilitySources[source].Strength, 3);
        AssertOnlyCellsMoved(before, ship);

        item.Enabled.Value = true;
        ship.SetSignature(source, SignatureChannel.LaunchFlash, 5f, null);
        Assert.Equal(.5f, ship.VisibilitySources[source].Strength, 3);
    }

    // ---- TwoSuppressorsScaleInEquipmentOrderAndEachPaysForItsOwnRemoval ----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoSuppressorsScaleInEquipmentOrderAndEachPaysForItsOwnRemoval(bool halfFirst)
    {
        var ship = Add(float3(0, 0, 0), null, halfFirst ? new[] { "SuppressorHalf", "SuppressorFifth" } : new[] { "SuppressorFifth", "SuppressorHalf" });
        var half = SuppressorItem(ship, "SuppressorHalf");
        var fifth = SuppressorItem(ship, "SuppressorFifth");
        var halfCell = CellOf(half);
        var fifthCell = CellOf(fifth);
        var before = Snapshot(ship);
        var source = new object();

        ship.SetSignature(source, SignatureChannel.LaunchFlash, 5f, null);

        Assert.Equal(5f * .5f * .2f, ship.VisibilitySources[source].Strength, 3);
        // The first in equipment order sees 5, the second sees what the first left: the removals, and so the heat, differ by order.
        var halfRemoved = halfFirst ? 5f * .5f : 5f * .2f * .5f; // factor .5 of what it is given
        var fifthRemoved = halfFirst ? 5f * .5f * .8f : 5f * .8f; // factor .2 of what it is given
        AssertHeat(halfRemoved * 1f, ship, halfCell, before[halfCell.x, halfCell.y]);
        AssertHeat(fifthRemoved * 10f, ship, fifthCell, before[fifthCell.x, fifthCell.y]);
        AssertOnlyCellsMoved(before, ship, halfCell, fifthCell);
    }

    // ---- ABeamPaysSuppressionHeatPerTick ----

    [Fact]
    public void ABeamPaysSuppressionHeatPerTick()
    {
        var paying = Add(float3(30, 0, 40), new[] { "Beam" }, new[] { "Targeting", "Reactor", "Suppressor" });
        var twin = Add(float3(-30, 0, 40), new[] { "Beam" }, new[] { "Targeting", "Reactor", "SuppressorCold" });
        var control = Add(float3(100, 0, 40), new[] { "Beam" }, new[] { "Targeting", "Reactor" });
        foreach (var ship in new[] { paying, twin, control })
        {
            ship.SetTarget(TargetRef.None);
            BeamItem(ship).Behaviors.Single(b => b is ConstantWeapon).Activate();
        }
        var p = CellOf(SuppressorItem(paying, "Suppressor"));
        var t = CellOf(SuppressorItem(twin, "SuppressorCold"));
        var excess = new float[3];
        for (var tick = 0; tick < 3; tick++)
        {
            Tick(.1f);
            excess[tick] = paying.Temperature[p.x, p.y] - twin.Temperature[t.x, t.y];
            var controlFlash = control.VisibilitySources[BeamItem(control).Behaviors.Single(b => b is ConstantWeapon)].Strength;
            Assert.Equal(5f, controlFlash, 3);
            Assert.Equal(.1f * controlFlash, paying.VisibilitySources[BeamItem(paying).Behaviors.Single(b => b is ConstantWeapon)].Strength, 3); // scaled on every tick write
        }
        Assert.True(excess[0] > 0f);
        Assert.True(excess[2] > excess[0]); // the cost is paid again on later ticks, not once
    }

    // ---- LaunchSuppressorDataRoundTripsAsUnionKey49 ----

    [Fact]
    public void LaunchSuppressorDataRoundTripsAsUnionKey49()
    {
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(ZonePack).Assembly);
        var data = new LaunchSuppressorData { Factor = Const(.25f), HeatPerUnit = Const(3f), RatedTemperature = Const(450f) };

        var bytes = MessagePackSerializer.Serialize<BehaviorData>(data, options);
        var reader = new MessagePackReader(bytes);
        Assert.Equal(2, reader.ReadArrayHeader());
        Assert.Equal(49, reader.ReadInt32()); // loot-2 holds 48; a key reused across branches would collide on the wire
        Assert.Equal(4, reader.ReadArrayHeader()); // Group is key 0, then the three stats

        var back = Assert.IsType<LaunchSuppressorData>(MessagePackSerializer.Deserialize<BehaviorData>(bytes, options));
        Assert.Equal(.25f, back.Factor.Min);
        Assert.Equal(3f, back.HeatPerUnit.Min);
        Assert.Equal(450f, back.RatedTemperature.Min);

        var unions = typeof(BehaviorData).GetCustomAttributes(typeof(UnionAttribute), false).Cast<UnionAttribute>().ToArray();
        Assert.Single(unions, u => u.Key == 49);
        Assert.Equal(typeof(MunitionFuelData), unions.Single(u => u.Key == 47).SubType);
        Assert.Equal(unions.Length, unions.Select(u => u.Key).Distinct().Count());
    }
}

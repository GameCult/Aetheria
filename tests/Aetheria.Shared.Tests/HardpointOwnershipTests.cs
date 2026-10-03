/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using Xunit;
using static CultMath.math;

// An equipped item knows the one hardpoint it occupies (EquippedItem.Hardpoint), decided once by the placement rule at
// equip. Its origin cell need not lie in that hardpoint: these are Soul's scenarios batch-4 P30 fixtures, where reading
// the hardpoint from the origin cell found nothing, or found a neighbour.
public sealed partial class RunStartTests
{
    // A 2x2 L whose origin cell (0, 0) is empty.
    private static Shape LShape()
    {
        var l = new Shape(2, 2);
        l[int2(1, 0)] = true;
        l[int2(0, 1)] = true;
        l[int2(1, 1)] = true;
        return l;
    }

    // A 2x1 design whose only cell is (1, 0): its origin lies one cell left of wherever it is placed.
    private static Shape EdgeShape()
    {
        var edge = new Shape(2, 1);
        edge[int2(1, 0)] = true;
        return edge;
    }

    private static HardpointData Mount(HardpointType type, int x, int y, Shape shape = null, float arc = 0,
        ItemRotation rotation = ItemRotation.None) =>
        new HardpointData { Type = type, Position = int2(x, y), Shape = shape ?? new Shape(), FiringArc = arc, Rotation = rotation };

    private GearData ScratchGear(string name, HardpointType type, Shape shape)
    {
        var design = ScratchGear(name, type, 1, 1);
        design.Shape = shape;
        return design;
    }

    private static bool InHardpoint(HardpointData hardpoint, int2 hullCell) => hardpoint.Shape[hullCell - hardpoint.Position];

    // P30 (a): an L gun in an L hardpoint sits at origin (1, 1), a cell outside the hardpoint, yet knows its hardpoint, and
    // FireControl reads that hardpoint's authored arc rather than the default.
    [Fact]
    public void AnLShapedGunInAnLHardpointKnowsItsHardpoint()
    {
        var hull = ScratchHull("P30 L Mount", 9, Mount(HardpointType.Ballistic, 1, 1, LShape(), arc: 360));
        var gun = ScratchGear("P30 L Gun", HardpointType.Ballistic, LShape());
        var entity = BareHull(hull);
        Assert.True(entity.TryEquip(Instance(gun)));
        var item = entity.Equipment.Single(i => i.Data == gun);

        Assert.False(InHardpoint(hull.Hardpoints[0], item.Position), $"the fixture's origin {item.Position} lies outside the hardpoint");
        Assert.Same(hull.Hardpoints[0], item.Hardpoint);
        Assert.NotEqual(360f, _items.GameplaySettings.FiringArc);
        Assert.Equal(360f, FireControl.ArcFor(item));
    }

    // P30 (c) shape: an edge gun whose origin cell lies in a radiator hardpoint reports its ballistic hardpoint.
    [Fact]
    public void AnItemWhoseOriginIsInAnotherHardpointKnowsItsOwn()
    {
        var radiator = Mount(HardpointType.Radiator, 0, 4);
        var ballistic = Mount(HardpointType.Ballistic, 1, 4, arc: 360);
        var hull = ScratchHull("P30 Neighbours", 9, radiator, ballistic);
        var gun = ScratchGear("P30 Edge Gun", HardpointType.Ballistic, EdgeShape());
        var entity = BareHull(hull);
        Assert.True(entity.TryEquip(Instance(gun), int2(0, 4)));
        var item = entity.Equipment.Single(i => i.Data == gun);

        Assert.True(InHardpoint(radiator, item.Position), "the fixture's origin lies in the radiator hardpoint");
        Assert.Same(ballistic, item.Hardpoint);
        Assert.Equal(360f, FireControl.ArcFor(item));
    }

    // P30 (b): for a design lacking its origin cell, TryFindSpace finds a placement wherever ItemFits accepts one, and the
    // origin it returns passes ItemFits and equips into that hardpoint. Unrotated, and turned so the item's extent swaps
    // axes (a counter-clockwise edge gun lies one cell below its origin).
    [Fact]
    public void TryFindSpaceFindsEveryPlacementTheFitRuleAccepts()
    {
        var gun = ScratchGear("P30 Edge Search Gun", HardpointType.Ballistic, EdgeShape());
        var cases = new[]
        {
            (hull: ScratchHull("P30 Edge Mount", 9, Mount(HardpointType.Ballistic, 0, 4)), fits: int2(-1, 4)),
            (hull: ScratchHull("P30 Turned Mount", 9, Mount(HardpointType.Ballistic, 4, 0, rotation: ItemRotation.CounterClockwise)), fits: int2(4, -1)),
        };
        foreach (var (hull, fits) in cases)
        {
            var entity = BareHull(hull);
            var unit = Instance(gun);
            Assert.True(entity.ItemFits(unit, fits), $"{hull.Name}: the fit rule accepts {fits}");
            Assert.True(entity.TryFindSpace(unit, out var found), $"{hull.Name}: TryFindSpace finds what ItemFits accepts");
            Assert.True(entity.ItemFits(unit, found), $"{hull.Name}: the found origin {found} passes ItemFits");
            Assert.True(entity.TryEquip(unit, found));
            Assert.Same(hull.Hardpoints[0], entity.Equipment.Single(i => i.Data == gun).Hardpoint);
        }
    }

    // A saved entity reloads each item into the same hardpoint, the L gun and the edge gun at a negative origin alike.
    [Fact]
    public void AnEquippedItemReloadsWithTheSameHardpoint()
    {
        var hull = ScratchHull("P30 Reload Mounts", 9, Mount(HardpointType.Ballistic, 1, 1, LShape()), Mount(HardpointType.Ballistic, 0, 6));
        var lGun = ScratchGear("P30 Reload L Gun", HardpointType.Ballistic, LShape());
        var edgeGun = ScratchGear("P30 Reload Edge Gun", HardpointType.Ballistic, EdgeShape());
        var entity = BareHull(hull);
        Assert.True(entity.TryEquip(Instance(lGun), int2(1, 1)));
        Assert.True(entity.TryEquip(Instance(edgeGun), int2(-1, 6)));

        var reloaded = EntitySerializer.Unpack(_items, null, EntitySerializer.Pack(entity));
        foreach (var design in new[] { lGun, edgeGun })
        {
            var before = hull.Hardpoints.IndexOf(entity.Equipment.Single(i => i.Data == design).Hardpoint);
            var after = reloaded.HullData.Hardpoints.IndexOf(reloaded.Equipment.Single(i => i.Data == design).Hardpoint);
            Assert.True(before >= 0, $"{design.Name} holds a hardpoint");
            Assert.Equal(before, after);
        }
    }

    // Tool gear occupies no hardpoint, even in a hardpoint's leftover cells; the hardpoint item beside it keeps its own.
    [Fact]
    public void ToolGearHasNoHardpoint()
    {
        var shape = new Shape(2, 1);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        var hull = ScratchHull("P30 Shared Mount", 9, Mount(HardpointType.Ballistic, 1, 1, shape));
        var gun = ScratchGear("P30 Small Gun", HardpointType.Ballistic, 1, 1);
        var tool = ScratchGear("P30 Tool", HardpointType.Tool, 1, 1);
        var entity = BareHull(hull);
        Assert.True(entity.TryEquip(Instance(gun), int2(1, 1)));
        Assert.True(entity.TryEquip(Instance(tool), int2(2, 1)), "tool gear in the hardpoint's leftover cell");

        Assert.Null(entity.Equipment.Single(i => i.Data == tool).Hardpoint);
        Assert.Same(hull.Hardpoints[0], entity.Equipment.Single(i => i.Data == gun).Hardpoint);
        Assert.Null(entity.EquippedHull.Hardpoint);
    }

    // The design's cells, turned to a rotation, moved so the lowest x and y are zero. Written here from the cells alone
    // (the game's turn directions, up to translation), so it is not Shape.Rotate checking itself.
    private static HashSet<(int x, int y)> Normalized(IEnumerable<int2> cells, ItemRotation rotation)
    {
        var turned = cells.Select(c => rotation switch
        {
            ItemRotation.Clockwise => (x: c.y, y: -c.x),
            ItemRotation.Reversed => (x: -c.x, y: -c.y),
            ItemRotation.CounterClockwise => (x: -c.y, y: c.x),
            _ => (x: c.x, y: c.y)
        }).ToList();
        if (turned.Count == 0) return new HashSet<(int, int)>();
        var minX = turned.Min(c => c.x);
        var minY = turned.Min(c => c.y);
        return turned.Select(c => (c.x - minX, c.y - minY)).ToHashSet();
    }

    // IsFilledBy against an independent oracle: a design fills a hardpoint when it is of the hardpoint's type and its
    // cells, turned to the hardpoint's rotation, are exactly the hardpoint's cells. Every catalog design against every
    // catalog hardpoint of its type, plus the L fixtures, whose rotations a rectangle-only catalog would not tell apart.
    [Fact]
    public void IsFilledByAgreesWithACellOracle()
    {
        var l = LShape();
        var hardpoints = _cache.GetAll<HullData>().SelectMany(hull => hull.Hardpoints).ToList();
        hardpoints.AddRange(Enum.GetValues(typeof(ItemRotation)).Cast<ItemRotation>()
            .Select(rotation => Mount(HardpointType.Ballistic, 0, 0, l, rotation: rotation)));
        var designs = _cache.GetAll<EquippableItemData>().ToList();
        designs.Add(new GearData { Name = "Oracle L", Hardpoint = HardpointType.Ballistic, Shape = l });
        designs.Add(new GearData { Name = "Oracle Edge", Hardpoint = HardpointType.Ballistic, Shape = EdgeShape() });

        int compared = 0, filled = 0;
        var mismatches = new List<string>();
        foreach (var hardpoint in hardpoints)
        foreach (var design in designs.Where(d => d.HardpointType == hardpoint.Type))
        {
            var oracle = Normalized(design.Shape.Coordinates, hardpoint.Rotation).SetEquals(Normalized(hardpoint.Shape.Coordinates, ItemRotation.None));
            compared++;
            if (oracle) filled++;
            if (hardpoint.IsFilledBy(design) != oracle && mismatches.Count < 10)
                mismatches.Add($"{design.Name} in a {hardpoint.Shape.Width}x{hardpoint.Shape.Height} {hardpoint.Type} hardpoint turned {hardpoint.Rotation}: oracle {oracle}");
        }
        Assert.True(mismatches.Count == 0, string.Join("; ", mismatches));
        Assert.True(filled > 0 && filled < compared, $"the oracle separates: {filled} of {compared} fill");
    }

    // Placement searches the hardpoint's last row and column: in a 3x3 hardpoint whose other cells hold tool gear, a
    // one-cell gun is placed in the bottom-right cell, and once it is, the search finds no space.
    [Fact]
    public void PlacementSearchesTheLastRow()
    {
        var mount = new Shape(3, 3);
        foreach (var cell in mount.AllCoordinates) mount[cell] = true;
        var hull = ScratchHull("P30 Crowded Mount", 9, Mount(HardpointType.Ballistic, 1, 1, mount));
        var gun = ScratchGear("P30 Corner Gun", HardpointType.Ballistic, 1, 1);
        var tool = ScratchGear("P30 Filler", HardpointType.Tool, 1, 1);
        var entity = BareHull(hull);
        var last = int2(3, 3);
        foreach (var cell in mount.Coordinates.Select(c => int2(1, 1) + c).Where(c => !c.Equals(last)))
            Assert.True(entity.TryEquip(Instance(tool), cell), $"filler at {cell}");

        var corner = Instance(gun);
        Assert.True(entity.TryFindSpace(corner, out var found));
        Assert.Equal(last, found);
        Assert.True(entity.TryEquip(corner, found));
        Assert.False(entity.TryFindSpace(Instance(gun), out _), "a full hardpoint offers no space");
    }

    // Billing does not depend on equip order: two heaters with different targets and draws, equipped in either order,
    // bill the bus the same at every step of a 60 s temperature ramp that crosses both targets.
    [Fact]
    public void ThermostatOrderDoesNotChangeBilling()
    {
        GearData Heater(string name, float target, float draw)
        {
            var design = ScratchGear(name, HardpointType.Tool, 1, 1);
            design.MinimumTemperature = 100;
            design.MaximumTemperature = 500;
            design.OptimalTemperature = 280;
            design.PlateauWidth = 200; // online at every temperature of the ramp
            design.Behaviors.Add(new ThermotoggleData { Group = 0, TargetTemperature = target });
            design.Behaviors.Add(new EnergyDrawData { Group = 0, EnergyDraw = new PerformanceStat { Min = draw, Max = draw }, PerSecond = true });
            return design;
        }
        var low = Heater("P30 Low Heater", 260, 10);
        var high = Heater("P30 High Heater", 320, 7);
        var hull = Hull("Djinni");
        var zone = Arena(new Scenario { Ambient = false });

        List<float> Bills(params GearData[] order)
        {
            var ship = BareHull(hull);
            ship.Zone = zone;
            foreach (var design in order) Assert.True(ship.TryEquip(Instance(design)), $"{design.Name} equips");
            zone.Admit(ship, piloted: false);
            for (var tick = 0; tick < 5; tick++) zone.Update(.1f);
            var bills = new List<float>();
            for (var tick = 0; tick < 600; tick++)
            {
                var temperature = 220 + 140 * tick / 599f;
                foreach (var v in ship.HullData.Shape.Coordinates) ship.Temperature[v.x, v.y] = temperature;
                ship.PowerBus.Step(.1f);
                bills.Add(ship.PowerBus.TotalDemand);
            }
            return bills;
        }
        var lowFirst = Bills(low, high);
        var highFirst = Bills(high, low);

        Assert.Equal(lowFirst, highFirst);
        Assert.True(lowFirst.Distinct().Count() >= 3, $"the ramp crosses both thermostats: bills {string.Join(", ", lowFirst.Distinct())}");
    }

    // The readers that bind muzzles, arcs, thruster emitters and radiators dereference item.Hardpoint: on generated
    // ships, stations and turrets every weapon, thruster and radiator holds a hardpoint containing all its cells, and
    // Tool gear and the hull hold none (Soul's scenarios-adopt probe S4, finding headless-reader-input).
    [Fact]
    public void GeneratedLoadoutsGiveEveryReaderItsHardpoint()
    {
        var sold = _cache.GetAll<FactionProductData>().Select(p => p.Design.Key).ToHashSet();
        var shipHulls = _cache.GetAll<HullData>()
            .Where(h => h.HullType == HullType.Ship && sold.Contains(_cache.RefOf(h).Key)).Select(h => h.Name).ToList();
        var generator = new LoadoutGenerator(ref _items.Random, _items, _galaxy, _galaxy.Entrance, _protagonist, .5f);
        var packs = shipHulls.SelectMany(name => Enumerable.Range(0, 2).Select(_ => generator.GenerateShipLoadout(h => h.Name == name)))
            .Concat(Enumerable.Range(0, 4).Select(_ => generator.GenerateStationLoadout()))
            .Concat(Enumerable.Range(0, 4).Select(_ => generator.GenerateTurretLoadout()))
            .ToList();
        var readers = 0;
        foreach (var pack in packs)
        {
            var entity = EntitySerializer.Unpack(_items, null, pack);
            Assert.Null(entity.EquippedHull.Hardpoint);
            foreach (var item in entity.Equipment.Where(i => i != entity.EquippedHull))
            {
                var label = $"{entity.HullData.Name}/{item.Data.Name}";
                if (item.Data.HardpointType == HardpointType.Tool) Assert.True(item.Hardpoint == null, $"{label}: Tool gear holds no hardpoint");
                if (!item.Behaviors.Any(b => b is Weapon || b is Thruster || b is Radiator)) continue;
                readers++;
                var hp = item.Hardpoint;
                Assert.True(hp != null, $"{label}: a reader item holds a hardpoint");
                var cells = item.Data.Shape.Coordinates.Select(c => item.Position + item.Data.Shape.Rotate(c, item.EquippableItem.Rotation));
                Assert.True(cells.All(c => hp.Shape[c - hp.Position]), $"{label}: its hardpoint contains every cell");
            }
        }
        Assert.True(readers > 0, "the generated loadouts carry weapons, thrusters or radiators");
    }
}

using System.Linq;
using CultMath;
using Xunit;

// Station facilities on typed mounts (aetheria-living-world, cut recipe-facilities; ruling station-facilities-on-typed-mounts): over
// the catalog the game boots, every offered station hull has the Facility rows to take the facility gear of every kind it serves,
// and keeps room for the docking bay and the heater it has always carried.
public sealed class StationFacilityRoomTests
{
    // StationKinds.FacilityRoomFaults over the booted catalog (shipped hulls and station packages): empty. A package merged without
    // its Facility row, or a kind given a facility some hull serving it cannot take, fails here by name.
    [Fact]
    public void EveryServedKindFitsItsHulls()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        Assert.Empty(StationKinds.FacilityRoomFaults(cache));
    }

    // Positive control: Zenith's Facility rows taken away (in memory, on a read-only catalog), the census names the hull, a kind it
    // serves and that kind's facility.
    [Fact]
    public void ARowlessHullIsNamed()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var zenith = Assert.Single(cache.GetAll<HullData>(), hull => hull.Name == "Zenith");
        Assert.Contains(zenith.Hardpoints, row => row.Type == HardpointType.Facility);
        zenith.Hardpoints.RemoveAll(row => row.Type == HardpointType.Facility);
        var faults = StationKinds.FacilityRoomFaults(cache);
        Assert.Contains(faults, line => line.Contains("Zenith") && line.Contains("Mining Station") && line.Contains("Extraction Rig"));
        Assert.All(faults, line => Assert.Contains("Zenith", line));
    }

    // A Facility row must not crowd out what a station has always carried: with every hardpoint's cells taken, each booted station
    // hull's interior still fits the Medium Docking Bay and then a heater.
    [Fact]
    public void EveryStationHullKeepsRoomForDockAndHeater()
    {
        using var cache = RestoredHullsTests.OpenCatalog();
        var bay = Assert.Single(cache.GetAll<DockingBayData>(), design => design.Name == "Medium Docking Bay");
        var heater = cache.GetAll<GearData>().Where(LoadoutGenerator.IsHeater)
            .OrderBy(design => design.Shape.Coordinates.Length).ThenBy(design => design.Name, System.StringComparer.Ordinal).First();
        var hulls = cache.GetAll<HullData>().Where(hull => hull.HullType == HullType.Station).ToArray();
        Assert.NotEmpty(hulls);
        foreach (var hull in hulls)
        {
            var free = new Shape(hull.Shape.Width, hull.Shape.Height);
            foreach (var cell in hull.InteriorCells.Coordinates) free[cell] = true;
            foreach (var row in hull.Hardpoints)
                foreach (var cell in row.Shape.Coordinates)
                {
                    var taken = row.Position + cell;
                    if (taken.x >= 0 && taken.y >= 0 && taken.x < free.Width && taken.y < free.Height) free[taken] = false;
                }
            foreach (var design in new ItemData[] { bay, heater })
            {
                Assert.True(design.Shape.FitsWithin(free, out var rotation, out var position), $"{hull.Name}: no room left for {design.Name} with every hardpoint's cells taken.");
                foreach (var cell in design.Shape.Coordinates) free[design.Shape.Rotate(cell, rotation) + position] = false;
            }
        }
    }
}

// Checks that some docking bay fits every station hull, which LoadoutGenerator.GenerateStationLoadout requires.
// Exits with the number of station hulls that cannot take any docking bay.
using System;
using System.Linq;

var db = AetherDb.Open();
var bays = db.Cache.GetAll<DockingBayData>().ToArray();
var failures = 0;
foreach (var hull in db.Cache.GetAll<HullData>().Where(h => h.HullType == HullType.Station))
{
    // Hardpoint gear is equipped before the docking bay, so the worst case leaves only the non-hardpoint interior free
    var worstCase = new Shape(hull.Shape.Width, hull.Shape.Height);
    foreach (var v in hull.InteriorCells.Coordinates) worstCase[v] = true;
    foreach (var hardpoint in hull.Hardpoints)
        foreach (var v in hardpoint.Shape.Coordinates)
            worstCase[hardpoint.Position + v] = false;

    var fitting = bays.Where(b => b.Shape.FitsWithin(worstCase, out _, out _)).Select(b => b.Name).ToArray();
    if (fitting.Length == 0) failures++;
    Console.WriteLine($"{hull.Name}: {(fitting.Length == 0 ? "NO docking bay fits" : string.Join(", ", fitting))}");
}
return failures;

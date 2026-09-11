// Counts equippable items by kind and manufacturer, to show how item variety is spread across the database.
using System;
using System.Linq;

var db = AetherDb.Open();
string Maker(Guid id) => db.Cache.Get<Faction>(id)?.ShortName ?? "(none)";
string Kind(EquippableItemData item) => item switch
{
    HullData hull => $"Hull/{hull.HullType}",
    DockingBayData _ => "DockingBay",
    CargoBayData _ => "CargoBay",
    WeaponItemData weapon => $"Weapon/{weapon.HardpointType}",
    _ => item.HardpointType.ToString()
};

var items = db.Cache.GetAll<EquippableItemData>().ToArray();
Console.WriteLine($"{items.Length} equippable items from {items.Select(i => i.Manufacturer).Distinct().Count()} manufacturers\n");
foreach (var kind in items.GroupBy(Kind).OrderBy(g => g.Key))
    Console.WriteLine($"{kind.Key,-22} {kind.Count(),3}  " +
        string.Join(", ", kind.GroupBy(i => Maker(i.Manufacturer)).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}:{g.Count()}")));

Console.WriteLine("\nSame kind and footprint from more than one manufacturer (candidate duplicates):");
foreach (var group in items.GroupBy(i => (Kind(i), i.Shape.Coordinates.Length)).Where(g => g.Select(i => i.Manufacturer).Distinct().Count() > 1))
    Console.WriteLine($"  {group.Key.Item1} size {group.Key.Length}: " + string.Join(" | ", group.Select(i => $"{i.Name} ({Maker(i.Manufacturer)})")));

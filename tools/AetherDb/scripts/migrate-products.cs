// Mints a faction product for every design that still carries a manufacturer, so branded variants exist for
// what the database already had. Copies the design's name and description as the brand's, and adds a default
// quality distribution row per role. Dry run unless passed "apply".
using System;
using System.Collections.Generic;
using System.Linq;

var db = AetherDb.Open();
var apply = args.Any(a => a == "apply");
var products = db.Cache.GetAll<FactionProductData>().ToList();
var designs = db.Cache.GetAll<CraftedItemData>()
    .Where(d => d.Manufacturer != Guid.Empty)
    .Where(d => !products.Any(p => p.Design == d.ID && p.Manufacturer == d.Manufacturer))
    .ToArray();

Console.WriteLine($"{products.Count} products exist; {designs.Length} designs need one\n");
foreach (var design in designs)
{
    var maker = db.Cache.Get<Faction>(design.Manufacturer);
    var product = new FactionProductData
    {
        Name = design.Name,
        Description = design.Description,
        Design = design.ID,
        Manufacturer = design.Manufacturer,
        // A design authored before roles existed has none; its product gets rows when roles are added to it.
        Roles = (design.Roles ?? new List<ItemRole>()).Select(r => new ProductRole { Role = r.Name }).ToList()
    };
    Console.WriteLine($"  {product.Name,-28} by {maker?.ShortName ?? "(unknown)",-12} roles: " +
        (product.Roles.Count == 0 ? "none" : string.Join(", ", product.Roles.Select(q => q.Role))));
    if (apply) db.Cache.Add(product);
}

if (apply && designs.Length > 0)
{
    db.Save();
    Console.WriteLine($"\nSaved {designs.Length} products to AetherDB.msgpack");
}
else if (!apply)
    Console.WriteLine("\nDry run. Pass \"apply\" to write these to the database.");

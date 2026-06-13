using Aetheria.State;
using Aetheria.State.Documents;

var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var statePath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : AetheriaStatePaths.ResolveDefaultStatePath(root);

await using var node = await AetheriaStateNode.OpenAsync(statePath, "aetheria-state-verify");

var ledger = await node.GetMigrationLedgerAsync()
    ?? throw new InvalidOperationException("Missing typed migration ledger.");
var quarantine = await node.GetLegacyCatalogQuarantineAsync()
    ?? throw new InvalidOperationException("Missing legacy catalog quarantine document.");

var items = node.Cache.GetAll<AetheriaItemDefinition>().ToArray();
var corporations = node.Cache.GetAll<AetheriaCorporation>().ToArray();
var nameFiles = node.Cache.GetAll<AetheriaNameFile>().ToArray();

RequireCount(ledger, "aetheria.item_definition.v1", items.Length);
RequireCount(ledger, "aetheria.corporation.v1", corporations.Length);
RequireCount(ledger, "aetheria.name_file.v1", nameFiles.Length);

if (items.Length == 0)
{
    throw new InvalidOperationException("Typed state has no item definitions.");
}

if (corporations.Length == 0)
{
    throw new InvalidOperationException("Typed state has no corporations.");
}

if (nameFiles.Length == 0)
{
    throw new InvalidOperationException("Typed state has no name files.");
}

var pricedItems = items.Count(item => item.Price > 0);
var manufacturedItems = items.Count(item => !string.IsNullOrWhiteSpace(item.ManufacturerLegacyId));
var shapedItems = items.Count(item => item.ShapeWidth > 0 && item.ShapeHeight > 0 && item.OccupiedCells > 0);
var describedCorporations = corporations.Count(corporation => !string.IsNullOrWhiteSpace(corporation.Description));
var corporationNameLinks = corporations.Count(corporation => !string.IsNullOrWhiteSpace(corporation.GeonameFileLegacyId));

if (pricedItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any prices.");
}

if (manufacturedItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any manufacturer legacy IDs.");
}

if (shapedItems == 0)
{
    throw new InvalidOperationException("Typed item definitions did not import any shape dimensions.");
}

if (describedCorporations == 0)
{
    throw new InvalidOperationException("Typed corporations did not import descriptions from legacy key 3.");
}

if (corporationNameLinks == 0)
{
    throw new InvalidOperationException("Typed corporations did not import geoname file legacy IDs.");
}

await RequireLegacyLookupAsync(
    items[0].LegacyId,
    () => node.GetItemDefinitionByLegacyIdAsync(items[0].LegacyId),
    "item definition");
await RequireLegacyLookupAsync(
    corporations[0].LegacyId,
    () => node.GetCorporationByLegacyIdAsync(corporations[0].LegacyId),
    "corporation");
await RequireLegacyLookupAsync(
    nameFiles[0].LegacyId,
    () => node.GetNameFileByLegacyIdAsync(nameFiles[0].LegacyId),
    "name file");

if (string.IsNullOrWhiteSpace(quarantine.CatalogFingerprint))
{
    throw new InvalidOperationException("Legacy catalog quarantine has no catalog fingerprint.");
}

Console.WriteLine($"Aetheria typed state verify passed: {statePath}");
Console.WriteLine($"Catalog fingerprint: {quarantine.CatalogFingerprint}");
Console.WriteLine($"Item definitions: {items.Length}");
Console.WriteLine($"Priced/manufactured/shaped items: {pricedItems}/{manufacturedItems}/{shapedItems}");
Console.WriteLine($"Corporations: {corporations.Length}");
Console.WriteLine($"Described/geoname-linked corporations: {describedCorporations}/{corporationNameLinks}");
Console.WriteLine($"Name files: {nameFiles.Length}");

static void RequireCount(AetheriaMigrationLedger ledger, string documentType, int actual)
{
    var expected = ledger.Counts.FirstOrDefault(count => count.DocumentType == documentType)?.Count;
    if (expected == null)
    {
        throw new InvalidOperationException($"Migration ledger is missing count for {documentType}.");
    }

    if (expected.Value != actual)
    {
        throw new InvalidOperationException(
            $"Migration ledger count mismatch for {documentType}: ledger={expected.Value}, actual={actual}.");
    }
}

static async Task RequireLegacyLookupAsync<T>(
    string legacyId,
    Func<Task<T?>> lookup,
    string label) where T : class
{
    if (string.IsNullOrWhiteSpace(legacyId))
    {
        throw new InvalidOperationException($"Cannot verify {label}: legacy id is empty.");
    }

    var value = await lookup().ConfigureAwait(false);
    if (value == null)
    {
        throw new InvalidOperationException($"Typed legacy-id lookup failed for {label} {legacyId}.");
    }
}

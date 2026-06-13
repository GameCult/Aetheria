using System.Security.Cryptography;
using Aetheria.State;
using Aetheria.State.Documents;
using Aetheria.State.Migration;

var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var statePath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : AetheriaStatePaths.ResolveDefaultStatePath(root);

var gameData = Path.Combine(root, "GameData");
var catalogPath = Path.Combine(gameData, "AetherDB.msgpack");
var nameFilesRoot = Path.Combine(gameData, "NameFile");
var capturedAtUtc = DateTimeOffset.UtcNow.ToString("O");

var catalog = CaptureFile(root, catalogPath);
var nameFiles = Directory.Exists(nameFilesRoot)
    ? Directory.EnumerateFiles(nameFilesRoot, "*.msgpack", SearchOption.TopDirectoryOnly)
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        .Select(path => CaptureFile(root, path))
        .ToArray()
    : [];

await using var node = await AetheriaStateNode.OpenAsync(statePath, "aetheria-legacy-catalog-import");

await node.PutLegacyCatalogQuarantineAsync(new AetheriaLegacyCatalogQuarantine
{
    RootPath = root,
    CapturedAtUtc = capturedAtUtc,
    CatalogFile = catalog.RelativePath,
    CatalogFingerprint = catalog.Fingerprint,
    CatalogBytes = catalog.Bytes,
    NameFiles = nameFiles,
    Notes =
    [
        "This document quarantines legacy catalog file facts only. It does not deserialize old DatabaseEntry payloads or grant old MessagePack files runtime state authority.",
        "AetherDB.msgpack and NameFile/*.msgpack remain migration inputs until a bounded catalog mapper emits typed Aetheria item/faction/name documents."
    ]
});

await node.PutMigrationLedgerAsync(new AetheriaMigrationLedger
{
    Source = LegacyMigrationBoundary.LegacyGameDataFile,
    SourceFingerprint = catalog.Fingerprint,
    LastMigrationAtUtc = capturedAtUtc,
    Counts =
    [
        new AetheriaMigrationCount
        {
            DocumentType = "aetheria.legacy_catalog_quarantine.v1",
            Count = 1
        },
        new AetheriaMigrationCount
        {
            DocumentType = "legacy.catalog_file",
            Count = catalog.Bytes > 0 ? 1 : 0
        },
        new AetheriaMigrationCount
        {
            DocumentType = "legacy.name_file",
            Count = nameFiles.Length
        }
    ],
    Notes =
    [
        "Legacy catalog quarantine captured file fingerprints into typed CultCache state without reading old DatabaseEntry payloads.",
        $"State path: {statePath}"
    ]
});

await node.FlushAsync();

Console.WriteLine($"Aetheria legacy catalog quarantine captured: {statePath}");
Console.WriteLine($"Catalog: {catalog.RelativePath} {catalog.Bytes} bytes {catalog.Fingerprint}");
Console.WriteLine($"Name files: {nameFiles.Length}");

static AetheriaLegacyCatalogFile CaptureFile(string root, string path)
{
    if (!File.Exists(path))
    {
        return new AetheriaLegacyCatalogFile
        {
            RelativePath = Path.GetRelativePath(root, path).Replace('\\', '/'),
            Fingerprint = "missing",
            Bytes = 0
        };
    }

    using var stream = File.OpenRead(path);
    var hash = SHA256.HashData(stream);
    return new AetheriaLegacyCatalogFile
    {
        RelativePath = Path.GetRelativePath(root, path).Replace('\\', '/'),
        Fingerprint = Convert.ToHexString(hash).ToLowerInvariant(),
        Bytes = stream.Length
    };
}

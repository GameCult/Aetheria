using System.Security.Cryptography;
using Aetheria.State;
using Aetheria.State.Documents;
using Aetheria.State.Migration;
using MessagePack;

var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var statePath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : AetheriaStatePaths.ResolveDefaultStatePath(root);
var sourceRoot = Path.GetRelativePath(Directory.GetCurrentDirectory(), root).Replace('\\', '/');
var outputStatePath = Path.GetRelativePath(root, statePath).Replace('\\', '/');

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
var entries = LegacyCatalogReader.Read(catalogPath);
var nameFileEntries = Directory.Exists(nameFilesRoot)
    ? Directory.EnumerateFiles(nameFilesRoot, "*.msgpack", SearchOption.TopDirectoryOnly)
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        .Select(LegacyCatalogReader.ReadSingle)
        .ToArray()
    : [];
var itemDefinitions = entries
    .Where(entry => entry.ItemDefinition != null)
    .Select(entry => entry.ItemDefinition!)
    .ToArray();
var corporations = entries
    .Where(entry => entry.Corporation != null)
    .Select(entry => entry.Corporation!)
    .ToArray();
var parsedNameFiles = entries.Concat(nameFileEntries)
    .Where(entry => entry.NameFile != null)
    .Select(entry => entry.NameFile!)
    .ToArray();

await using var node = await AetheriaStateNode.OpenAsync(statePath, "aetheria-legacy-catalog-import");

await node.PutLegacyCatalogQuarantineAsync(new AetheriaLegacyCatalogQuarantine
{
    RootPath = sourceRoot,
    CapturedAtUtc = capturedAtUtc,
    CatalogFile = catalog.RelativePath,
    CatalogFingerprint = catalog.Fingerprint,
    CatalogBytes = catalog.Bytes,
    NameFiles = nameFiles,
    Notes =
    [
        "This document quarantines legacy catalog file facts and records the bounded raw payload mapping pass. It does not grant old MessagePack files runtime state authority.",
        "AetherDB.msgpack and NameFile/*.msgpack remain migration inputs until Unity runtime reads typed Aetheria catalog documents directly."
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
        },
        new AetheriaMigrationCount
        {
            DocumentType = "aetheria.item_definition.v1",
            Count = itemDefinitions.Length
        },
        new AetheriaMigrationCount
        {
            DocumentType = "aetheria.corporation.v1",
            Count = corporations.Length
        },
        new AetheriaMigrationCount
        {
            DocumentType = "aetheria.name_file.v1",
            Count = parsedNameFiles.Length
        }
    ],
    LegacyCatalogEntries = entries.Select(entry => entry.Summary).ToArray(),
    Notes =
    [
        "Legacy catalog quarantine captured file fingerprints and mapped stable old MessagePack union payload fields into typed CultCache documents.",
        "The mapper intentionally reads only stable scalar/catalog fields. Runtime object graphs, behavior payloads, and Unity-specific shapes remain legacy until dedicated typed documents exist.",
        $"State path: {outputStatePath}"
    ]
});

foreach (var item in itemDefinitions)
{
    await node.PutLegacyItemDefinitionAsync(item);
}

foreach (var corporation in corporations)
{
    await node.PutLegacyCorporationAsync(corporation);
}

foreach (var nameFile in parsedNameFiles)
{
    await node.PutLegacyNameFileAsync(nameFile);
}

await node.PutCatalogSurfaceAsync(AetheriaCatalogSurfaceProjector.Build(node.ReadCatalogSnapshot(), capturedAtUtc));

await node.FlushAsync();

Console.WriteLine($"Aetheria legacy catalog mapped into typed state: {statePath}");
Console.WriteLine($"Catalog: {catalog.RelativePath} {catalog.Bytes} bytes {catalog.Fingerprint}");
Console.WriteLine($"Name files: {nameFiles.Length}");
Console.WriteLine($"Mapped items: {itemDefinitions.Length}");
Console.WriteLine($"Mapped factions: {corporations.Length}");
Console.WriteLine($"Mapped name files: {parsedNameFiles.Length}");

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

internal static class LegacyCatalogReader
{
    private static readonly Dictionary<int, string> UnionNames = new()
    {
        [0] = "SimpleCommodityData",
        [1] = "CompoundCommodityData",
        [2] = "GearData",
        [3] = "HullData",
        [8] = "GalaxyMapLayerData",
        [9] = "NameFile",
        [11] = "PlayerData",
        [13] = "Faction",
        [15] = "OrbitData",
        [16] = "BodyData",
        [17] = "PersonalityAttribute",
        [25] = "AsteroidBeltData",
        [26] = "GasGiantData",
        [27] = "SunData",
        [28] = "PlanetData",
        [29] = "CargoBayData",
        [30] = "DockingBayData",
        [31] = "WeaponItemData"
    };

    private static readonly HashSet<int> ItemUnionKeys = [0, 1, 2, 3, 29, 30, 31];

    public static IReadOnlyList<LegacyCatalogEntry> Read(string catalogPath)
    {
        if (!File.Exists(catalogPath))
        {
            return [];
        }

        var bytes = File.ReadAllBytes(catalogPath);
        var reader = new MessagePackReader(bytes);
        var count = reader.ReadArrayHeader();
        var entries = new List<LegacyCatalogEntry>(count);
        for (var i = 0; i < count; i++)
        {
            entries.Add(ReadEntry(ref reader));
        }

        return entries;
    }

    public static LegacyCatalogEntry ReadSingle(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var reader = new MessagePackReader(bytes);
        return ReadEntry(ref reader);
    }

    private static LegacyCatalogEntry ReadEntry(ref MessagePackReader reader)
    {
        var unionHeader = reader.ReadArrayHeader();
        if (unionHeader != 2)
        {
            throw new InvalidDataException($"Unexpected legacy catalog union header length: {unionHeader}.");
        }

        var unionKey = reader.ReadInt32();
        var unionName = UnionNames.TryGetValue(unionKey, out var knownName) ? knownName : $"union:{unionKey}";
        var payload = ReadArrayPayload(ref reader);
        var legacyId = GetGuid(payload, 0);
        var name = GetString(payload, 1);
        var description = GetString(payload, 2);
        var shape = ReadShape(payload, 5);
        var summary = new AetheriaLegacyCatalogEntrySummary
        {
            LegacyId = legacyId,
            UnionKey = unionKey,
            DocumentKind = unionName,
            Name = name,
            Note = "Mapped from stable legacy MessagePack array fields."
        };

        return new LegacyCatalogEntry(
            summary,
            ItemUnionKeys.Contains(unionKey)
                ? new AetheriaItemDefinition
                {
                    Name = name,
                    Category = unionName,
                    LegacyId = legacyId,
                    Description = description,
                    Mass = GetDouble(payload, 4),
                    Volume = shape.OccupiedCells,
                    ManufacturerLegacyId = GetOptionalGuid(payload, 3),
                    Price = GetInt(payload, 8),
                    ShapeWidth = shape.Width,
                    ShapeHeight = shape.Height,
                    OccupiedCells = shape.OccupiedCells,
                    Tags = [unionName, "legacy-catalog"]
                }
                : null,
            unionKey == 13
                ? new AetheriaCorporation
                {
                    Name = name,
                    LegacyId = legacyId,
                    ShortName = GetString(payload, 2),
                    Description = GetString(payload, 3),
                    GeonameFileLegacyId = GetOptionalGuid(payload, 9),
                    BossHullLegacyId = GetOptionalGuid(payload, 10),
                    InfluenceDistance = GetInt(payload, 11),
                    AllegianceCount = GetMapCount(payload, 12),
                    OverworldMusic = GetUInt(payload, 13),
                    CombatMusic = GetUInt(payload, 14),
                    BossMusic = GetUInt(payload, 15)
                }
                : null,
            unionKey == 9
                ? new AetheriaNameFile
                {
                    Name = name,
                    LegacyId = legacyId,
                    NameCount = GetArrayLength(payload, 2),
                    SampleNames = GetStringArraySample(payload, 2, 8)
                }
                : null);
    }

    private static Dictionary<int, object?> ReadArrayPayload(ref MessagePackReader reader)
    {
        var length = reader.ReadArrayHeader();
        var values = new Dictionary<int, object?>(length);
        for (var index = 0; index < length; index++)
        {
            values[index] = ReadValue(ref reader);
        }

        return values;
    }

    private static object? ReadValue(ref MessagePackReader reader)
    {
        if (reader.NextMessagePackType == MessagePackType.Nil)
        {
            reader.ReadNil();
            return null;
        }

        return reader.NextMessagePackType switch
        {
            MessagePackType.Integer => ReadInteger(ref reader),
            MessagePackType.Boolean => reader.ReadBoolean(),
            MessagePackType.Float => reader.ReadDouble(),
            MessagePackType.String => reader.ReadString(),
            MessagePackType.Binary => ReadBinary(ref reader),
            MessagePackType.Array => ReadNestedArray(ref reader),
            MessagePackType.Map => ReadNestedMap(ref reader),
            _ => SkipValue(ref reader)
        };
    }

    private static long ReadInteger(ref MessagePackReader reader)
    {
        return reader.NextCode >= MessagePackCode.MinNegativeFixInt || reader.NextCode == MessagePackCode.Int8 ||
               reader.NextCode == MessagePackCode.Int16 || reader.NextCode == MessagePackCode.Int32 ||
               reader.NextCode == MessagePackCode.Int64
            ? reader.ReadInt64()
            : (long) reader.ReadUInt64();
    }

    private static object?[] ReadNestedArray(ref MessagePackReader reader)
    {
        var length = reader.ReadArrayHeader();
        var values = new object?[length];
        for (var index = 0; index < length; index++)
        {
            values[index] = ReadValue(ref reader);
        }

        return values;
    }

    private static byte[]? ReadBinary(ref MessagePackReader reader)
    {
        var bytes = reader.ReadBytes();
        if (bytes == null)
        {
            return null;
        }

        var result = new byte[bytes.Value.Length];
        var offset = 0;
        foreach (var segment in bytes.Value)
        {
            segment.Span.CopyTo(result.AsSpan(offset));
            offset += segment.Length;
        }

        return result;
    }

    private static Dictionary<string, object?> ReadNestedMap(ref MessagePackReader reader)
    {
        var length = reader.ReadMapHeader();
        var values = new Dictionary<string, object?>(length);
        for (var index = 0; index < length; index++)
        {
            var key = ReadValue(ref reader);
            values[key?.ToString() ?? ""] = ReadValue(ref reader);
        }

        return values;
    }

    private static object? SkipValue(ref MessagePackReader reader)
    {
        reader.Skip();
        return null;
    }

    private static string GetGuid(IReadOnlyDictionary<int, object?> payload, int key)
    {
        return payload.TryGetValue(key, out var value) && value is byte[] bytes && bytes.Length == 16
            ? new Guid(bytes).ToString("D")
            : "";
    }

    private static string GetOptionalGuid(IReadOnlyDictionary<int, object?> payload, int key)
    {
        var legacyId = GetGuid(payload, key);
        return Guid.TryParse(legacyId, out var guid) && guid == Guid.Empty ? "" : legacyId;
    }

    private static string GetString(IReadOnlyDictionary<int, object?> payload, int key)
    {
        return payload.TryGetValue(key, out var value) ? value as string ?? "" : "";
    }

    private static double GetDouble(IReadOnlyDictionary<int, object?> payload, int key)
    {
        if (!payload.TryGetValue(key, out var value) || value == null)
        {
            return 0;
        }

        return value switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            long integerValue => integerValue,
            int integerValue => integerValue,
            _ => 0
        };
    }

    private static int GetInt(IReadOnlyDictionary<int, object?> payload, int key)
    {
        if (!payload.TryGetValue(key, out var value) || value == null)
        {
            return 0;
        }

        return value switch
        {
            long integerValue => checked((int) integerValue),
            int integerValue => integerValue,
            double doubleValue => checked((int) doubleValue),
            float floatValue => checked((int) floatValue),
            _ => 0
        };
    }

    private static uint GetUInt(IReadOnlyDictionary<int, object?> payload, int key)
    {
        if (!payload.TryGetValue(key, out var value) || value == null)
        {
            return 0;
        }

        return value switch
        {
            long integerValue when integerValue >= 0 => checked((uint) integerValue),
            int integerValue when integerValue >= 0 => checked((uint) integerValue),
            double doubleValue when doubleValue >= 0 => checked((uint) doubleValue),
            float floatValue when floatValue >= 0 => checked((uint) floatValue),
            _ => 0
        };
    }

    private static int GetMapCount(IReadOnlyDictionary<int, object?> payload, int key)
    {
        return payload.TryGetValue(key, out var value) && value is Dictionary<string, object?> map ? map.Count : 0;
    }

    private static int GetArrayLength(IReadOnlyDictionary<int, object?> payload, int key)
    {
        return payload.TryGetValue(key, out var value) && value is object?[] array ? array.Length : 0;
    }

    private static string[] GetStringArraySample(IReadOnlyDictionary<int, object?> payload, int key, int max)
    {
        return payload.TryGetValue(key, out var value) && value is object?[] array
            ? array.OfType<string>().Take(max).ToArray()
            : [];
    }

    private static ShapeFacts ReadShape(IReadOnlyDictionary<int, object?> payload, int key)
    {
        if (!payload.TryGetValue(key, out var value) || value == null)
        {
            return new ShapeFacts(0, 0, 0);
        }

        var matrix = UnwrapShapeMatrix(value);
        if (matrix is not object?[] columns)
        {
            return new ShapeFacts(0, 0, 0);
        }

        var width = columns.Length;
        var height = 0;
        var occupied = 0;
        foreach (var column in columns)
        {
            if (column is not object?[] cells)
            {
                if (column is bool occupiedCell)
                {
                    height = Math.Max(height, 1);
                    if (occupiedCell)
                    {
                        occupied++;
                    }
                }

                continue;
            }

            height = Math.Max(height, cells.Length);
            occupied += cells.Count(cell => cell is true);
        }

        return new ShapeFacts(width, height, occupied);
    }

    private static object? UnwrapShapeMatrix(object? value)
    {
        return value is object?[] shapeObject && shapeObject.Length == 1 ? shapeObject[0] : value;
    }
}

internal sealed record LegacyCatalogEntry(
    AetheriaLegacyCatalogEntrySummary Summary,
    AetheriaItemDefinition? ItemDefinition,
    AetheriaCorporation? Corporation,
    AetheriaNameFile? NameFile);

internal readonly record struct ShapeFacts(int Width, int Height, int OccupiedCells);

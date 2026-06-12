using Aetheria.State;
using Aetheria.State.Documents;
using Aetheria.State.Migration;
using GameCult.Caching;

var root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
var statePath = AetheriaStatePaths.ResolveDefaultStatePath(root);
var now = DateTimeOffset.UtcNow.ToString("O");
var itemKey = new CultRecordKey("item:smoke-aether-drive");

await using (var node = await AetheriaStateNode.OpenAsync(statePath, "aetheria-state-smoke"))
{
    await node.PutWorldAsync(new AetheriaWorldState
    {
        Name = "Aetheria",
        WorldId = "aetheria",
        SchemaEpoch = 1,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    });

    await node.PutItemDefinitionAsync(
        itemKey,
        new AetheriaItemDefinition
        {
            Name = "Smoke Aether Drive",
            Category = "ship-module",
            LegacyId = "smoke:aether-drive",
            Description = "Typed CultCache smoke document for the rebuild spine.",
            Mass = 12.5,
            Volume = 4.0,
            Tags = ["smoke", "state-spine"]
        });

    await node.PutMigrationLedgerAsync(new AetheriaMigrationLedger
    {
        Source = LegacyMigrationBoundary.LegacyGameDataFile,
        SourceFingerprint = "smoke",
        LastMigrationAtUtc = now,
        Counts =
        [
            new AetheriaMigrationCount
            {
                DocumentType = "aetheria.item_definition.v1",
                Count = 1
            }
        ],
        Notes = ["Smoke proves the new state owner can write, flush, reopen, and read without old JSON/Rethink authority."]
    });

    await node.FlushAsync();
}

await using (var reopened = await AetheriaStateNode.OpenAsync(statePath, "aetheria-state-smoke-reopen"))
{
    var world = await reopened.GetWorldAsync();
    var item = await reopened.GetItemDefinitionAsync(itemKey);

    if (world?.WorldId != "aetheria")
    {
        throw new InvalidOperationException("World state did not survive flush/reopen.");
    }

    if (item?.Name != "Smoke Aether Drive")
    {
        throw new InvalidOperationException("Item definition did not survive flush/reopen.");
    }
}

Console.WriteLine($"Aetheria typed state smoke passed: {statePath}");

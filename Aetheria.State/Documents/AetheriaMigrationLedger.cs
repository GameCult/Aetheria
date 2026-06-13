using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("aetheria.migration_ledger", "aetheria.migration_ledger.v1")]
[CultGlobal]
[MessagePackObject]
public sealed class AetheriaMigrationLedger
{
    [Key(0)]
    [CultName]
    public string Name { get; set; } = "Aetheria legacy migration ledger";

    [Key(1)]
    public string Source { get; set; } = "";

    [Key(2)]
    public string SourceFingerprint { get; set; } = "";

    [Key(3)]
    public string LastMigrationAtUtc { get; set; } = "";

    [Key(4)]
    public AetheriaMigrationCount[] Counts { get; set; } = [];

    [Key(5)]
    public string[] Notes { get; set; } = [];

    [Key(6)]
    public AetheriaLegacyCatalogEntrySummary[] LegacyCatalogEntries { get; set; } = [];
}

[MessagePackObject]
public sealed class AetheriaMigrationCount
{
    [Key(0)]
    public string DocumentType { get; set; } = "";

    [Key(1)]
    public int Count { get; set; }
}

[MessagePackObject]
public sealed class AetheriaLegacyCatalogEntrySummary
{
    [Key(0)]
    public string LegacyId { get; set; } = "";

    [Key(1)]
    public int UnionKey { get; set; }

    [Key(2)]
    public string DocumentKind { get; set; } = "";

    [Key(3)]
    public string Name { get; set; } = "";

    [Key(4)]
    public string Note { get; set; } = "";
}

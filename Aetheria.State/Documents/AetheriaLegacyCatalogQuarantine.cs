using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("aetheria.legacy_catalog_quarantine", "aetheria.legacy_catalog_quarantine.v1")]
[CultGlobal]
[MessagePackObject]
public sealed class AetheriaLegacyCatalogQuarantine
{
    [Key(0)]
    [CultName]
    public string Name { get; set; } = "Aetheria legacy catalog quarantine";

    [Key(1)]
    public string RootPath { get; set; } = "";

    [Key(2)]
    public string CapturedAtUtc { get; set; } = "";

    [Key(3)]
    public string CatalogFile { get; set; } = "";

    [Key(4)]
    public string CatalogFingerprint { get; set; } = "";

    [Key(5)]
    public long CatalogBytes { get; set; }

    [Key(6)]
    public AetheriaLegacyCatalogFile[] NameFiles { get; set; } = [];

    [Key(7)]
    public string[] Notes { get; set; } = [];

    [Key(8)]
    public AetheriaLegacyCatalogFile[] SupplementalCatalogFiles { get; set; } = [];
}

[MessagePackObject]
public sealed class AetheriaLegacyCatalogFile
{
    [Key(0)]
    public string RelativePath { get; set; } = "";

    [Key(1)]
    public string Fingerprint { get; set; } = "";

    [Key(2)]
    public long Bytes { get; set; }
}

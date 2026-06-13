using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("aetheria.corporation", "aetheria.corporation.v2")]
[MessagePackObject]
public sealed class AetheriaCorporation
{
    [Key(0)]
    [CultName]
    public string Name { get; set; } = "";

    [Key(1)]
    [CultIndex("legacyId")]
    public string LegacyId { get; set; } = "";

    [Key(2)]
    public string ShortName { get; set; } = "";

    [Key(3)]
    public string Description { get; set; } = "";

    [Key(4)]
    [CultIndex("geonameFileLegacyId")]
    public string GeonameFileLegacyId { get; set; } = "";

    [Key(5)]
    [CultIndex("bossHullLegacyId")]
    public string BossHullLegacyId { get; set; } = "";

    [Key(6)]
    public int InfluenceDistance { get; set; }

    [Key(7)]
    public int AllegianceCount { get; set; }

    [Key(8)]
    public uint OverworldMusic { get; set; }

    [Key(9)]
    public uint CombatMusic { get; set; }

    [Key(10)]
    public uint BossMusic { get; set; }

    [Key(11)]
    public AetheriaCorporationAllegiance[] Allegiances { get; set; } = [];
}

[MessagePackObject]
public sealed class AetheriaCorporationAllegiance
{
    [Key(0)]
    public string CorporationLegacyId { get; set; } = "";

    [Key(1)]
    public double Weight { get; set; }
}

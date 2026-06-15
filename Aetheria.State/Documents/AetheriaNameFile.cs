using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("aetheria.name_file", "aetheria.name_file.v2")]
[MessagePackObject]
public sealed class AetheriaNameFile
{
    [Key(0)]
    [CultName]
    public string Name { get; set; } = "";

    [Key(1)]
    [CultIndex("legacyId")]
    public string LegacyId { get; set; } = "";

    [IgnoreMember]
    public string NameFileKey => string.IsNullOrWhiteSpace(LegacyId)
        ? ""
        : $"aetheria.name_file:legacy:{LegacyId}";

    [Key(2)]
    public int NameCount { get; set; }

    [Key(3)]
    public string[] SampleNames { get; set; } = [];

    [Key(4)]
    public string[] Names { get; set; } = [];
}

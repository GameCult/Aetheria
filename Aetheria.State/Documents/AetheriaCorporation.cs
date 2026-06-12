using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("aetheria.corporation", "aetheria.corporation.v1")]
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
    public string Description { get; set; } = "";
}

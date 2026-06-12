using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("aetheria.item_definition", "aetheria.item_definition.v1")]
[MessagePackObject]
public sealed class AetheriaItemDefinition
{
    [Key(0)]
    [CultName]
    public string Name { get; set; } = "";

    [Key(1)]
    [CultIndex("category")]
    public string Category { get; set; } = "uncategorized";

    [Key(2)]
    [CultIndex("legacyId")]
    public string LegacyId { get; set; } = "";

    [Key(3)]
    public string Description { get; set; } = "";

    [Key(4)]
    public double Mass { get; set; }

    [Key(5)]
    public double Volume { get; set; }

    [Key(6)]
    public string[] Tags { get; set; } = [];
}

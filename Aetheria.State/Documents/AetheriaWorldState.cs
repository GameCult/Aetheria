using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("aetheria.world_state", "aetheria.world_state.v1")]
[CultGlobal]
[MessagePackObject]
public sealed class AetheriaWorldState
{
    [Key(0)]
    [CultName]
    public string Name { get; set; } = "Aetheria";

    [Key(1)]
    [CultIndex("worldId")]
    public string WorldId { get; set; } = "aetheria";

    [Key(2)]
    public int SchemaEpoch { get; set; } = 1;

    [Key(3)]
    public string CreatedAtUtc { get; set; } = "";

    [Key(4)]
    public string UpdatedAtUtc { get; set; } = "";
}

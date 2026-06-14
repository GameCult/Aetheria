using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("aetheria.runtime_session", "aetheria.runtime_session.v1")]
[MessagePackObject]
public sealed class AetheriaRuntimeSession
{
    [Key(0)]
    [CultName]
    public string RuntimeId { get; set; } = "";

    [Key(1)]
    [CultIndex("role")]
    public string Role { get; set; } = "local";

    [Key(2)]
    public string StartedAtUtc { get; set; } = "";

    [Key(3)]
    public string LastSeenAtUtc { get; set; } = "";

    [Key(4)]
    public string Status { get; set; } = "running";
}

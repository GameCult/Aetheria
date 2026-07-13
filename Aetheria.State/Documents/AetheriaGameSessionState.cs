using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("aetheria.game_session", "aetheria.game_session.v1")]
[CultGlobal]
[MessagePackObject]
public sealed class AetheriaGameSessionState
{
    public const string TerminusMode = "terminus";

    [Key(0)] public string Mode { get; set; } = TerminusMode;
    [Key(1)] public string SessionId { get; set; } = "";
    [Key(2)] public string RunId { get; set; } = "";
    [Key(3)] public string ControlledEntityKey { get; set; } = "";
    [Key(4)] public string EntrySurfaceId { get; set; } = "aetheria.pilot";
    [Key(5)] public string LastStartCommandId { get; set; } = "";
    [Key(6)] public string UpdatedAtUtc { get; set; } = "";
    [Key(7)] public double SimulationRate { get; set; }
}

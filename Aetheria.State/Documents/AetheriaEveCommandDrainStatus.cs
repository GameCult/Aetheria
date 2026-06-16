using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("aetheria.eve_command_drain_status", "aetheria.eve_command_drain_status.v1")]
[CultGlobal]
[MessagePackObject]
public sealed class AetheriaEveCommandDrainStatus
{
    [Key(0)]
    [CultName]
    public string Name { get; set; } = "Aetheria Eve command drain";

    [Key(1)]
    [CultIndex("runtimeId")]
    public string RuntimeId { get; set; } = "";

    [Key(2)]
    public string StatePath { get; set; } = "";

    [Key(3)]
    public string LastPollAtUtc { get; set; } = "";

    [Key(4)]
    public string LastAcceptedAtUtc { get; set; } = "";

    [Key(5)]
    public int PendingBeforeApply { get; set; }

    [Key(6)]
    public int CommandsAccepted { get; set; }

    [Key(7)]
    public int CommandsRejected { get; set; }

    [Key(8)]
    public int AppliedCatalogRefreshes { get; set; }

    [Key(9)]
    public int AppliedOperationsRefreshes { get; set; }

    [Key(10)]
    public int ConsecutiveFailures { get; set; }

    [Key(11)]
    public string LastError { get; set; } = "";

    [Key(12)]
    public string LastRejectedCommand { get; set; } = "";

    [Key(13)]
    public string LastRejectedReason { get; set; } = "";

    [Key(14)]
    public string Status { get; set; } = "ok";

    [Key(15)]
    public int AppliedPlayerSettingsCommands { get; set; }
}

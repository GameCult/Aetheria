using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("aetheria.runtime_commit_drain_status", "aetheria.runtime_commit_drain_status.v1")]
[CultGlobal]
[MessagePackObject]
public sealed class AetheriaRuntimeCommitDrainStatus
{
    [Key(0)]
    [CultName]
    public string Name { get; set; } = "Aetheria runtime commit drain";

    [Key(1)]
    [CultIndex("runtimeId")]
    public string RuntimeId { get; set; } = "";

    [Key(2)]
    public string StatePath { get; set; } = "";

    [Key(3)]
    public string LastPollAtUtc { get; set; } = "";

    [Key(4)]
    public string LastAppliedAtUtc { get; set; } = "";

    [Key(5)]
    public int PendingBeforeApply { get; set; }

    [Key(6)]
    public int CommandsApplied { get; set; }

    [Key(7)]
    public int AppliedPlayerSettings { get; set; }

    [Key(8)]
    public int AppliedLoadoutTemplates { get; set; }

    [Key(9)]
    public int AppliedRunCheckpoints { get; set; }

    [Key(10)]
    public int ConsecutiveFailures { get; set; }

    [Key(11)]
    public string LastError { get; set; } = "";

    [Key(12)]
    public string Status { get; set; } = "ok";
}

using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Daemon;

[CultDocument("gamecult.aetheria.ymir_persistence_marker", "gamecult.aetheria.ymir_persistence_marker.v1")]
[MessagePackObject]
public sealed class AetheriaYmirPersistenceMarkerDocument
{
    public const string RecordKey = "private:aetheria.ymir.persistence.v1";

    [Key(0)] public string FormatId { get; set; } = "gamecult.aetheria.ymir.persistence.v1";
    [Key(1)] public string ActivatedAtUtc { get; set; } = "";
}

[CultDocument("gamecult.aetheria.ymir_journal_chunk", "gamecult.aetheria.ymir_journal_chunk.v1")]
[MessagePackObject]
public sealed class AetheriaYmirJournalChunkDocument
{
    [Key(0)] public string RunId { get; set; } = "";
    [Key(1)] public int ZoneIndex { get; set; }
    [Key(2)] public string Channel { get; set; } = "";
    [Key(3)] public string SessionGeneration { get; set; } = "";
    [Key(4)] public long FirstEntryIndex { get; set; }
    [Key(5)] public int EntryCount { get; set; }
    [Key(6)] public byte[] Payload { get; set; } = [];
}

[CultDocument("gamecult.aetheria.ymir_resume", "gamecult.aetheria.ymir_resume.v1")]
[MessagePackObject]
public sealed class AetheriaYmirResumeDocument
{
    [Key(0)] public string RunId { get; set; } = "";
    [Key(1)] public int ZoneIndex { get; set; }
    [Key(2)] public long FrameId { get; set; }
    [Key(3)] public int SimulationStepIndex { get; set; }
    [Key(4)] public string WorldSessionGeneration { get; set; } = "";
    [Key(5)] public long WorldJournalEntryCount { get; set; }
    [Key(6)] public byte[] WorldDescriptorPayload { get; set; } = [];
    [Key(7)] public string PayloadSessionGeneration { get; set; } = "";
    [Key(8)] public long PayloadJournalEntryCount { get; set; }
    [Key(9)] public byte[] PayloadDescriptorPayload { get; set; } = [];
}

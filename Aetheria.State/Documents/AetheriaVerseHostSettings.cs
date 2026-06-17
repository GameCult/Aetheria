using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("aetheria.verse_host_settings", "aetheria.verse_host_settings.v1")]
[CultGlobal]
[MessagePackObject]
public sealed class AetheriaVerseHostSettings
{
    [Key(0)] public string Schema { get; set; } = "aetheria.verse_host_settings.v1";
    [Key(1)] public string ServiceId { get; set; } = "aetheria.runtime";
    [Key(2)] public string VerseId { get; set; } = "aetheria.local";
    [Key(3)] public string RootVerse { get; set; } = "asgard";
    [Key(4)] public string CanonicalService { get; set; } = "asgard.aetheria";
    [Key(5)] public string LocatedService { get; set; } = "asgard.local.aetheria";
    [Key(6)] public string CultMeshAddress { get; set; } = "asgard.local.aetheria/eve";
    [Key(7)] public string Title { get; set; } = "Aetheria";
    [Key(8)] public string Visibility { get; set; } = "private";
    [Key(9)] public string LastUpdatedAtUtc { get; set; } = "";
}

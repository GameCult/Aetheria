using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("aetheria.player_profile", "aetheria.player_profile.v1")]
[MessagePackObject]
public sealed class AetheriaPlayerProfile
{
    [Key(0)]
    [CultName]
    public string Username { get; set; } = "";

    [Key(1)]
    [CultIndex("legacyId")]
    public string LegacyId { get; set; } = "";

    [Key(2)]
    [CultIndex("email")]
    public string Email { get; set; } = "";

    [Key(3)]
    [CultReference(typeof(AetheriaCorporation))]
    public string CorporationKey { get; set; } = "";
}

using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("gamecult.eve.provider_advertisement", "gamecult.eve.provider_advertisement.v1")]
[CultGlobal]
[MessagePackObject]
public sealed class EveProviderAdvertisementState
{
    [Key(0)]
    [CultIndex("schema")]
    public string Schema { get; set; } = "gamecult.eve.provider_advertisement.v1";

    [Key(1)]
    [CultName]
    public string ProviderId { get; set; } = "";

    [Key(2)]
    public string ServiceId { get; set; } = "";

    [Key(3)]
    public string VerseId { get; set; } = "";

    [Key(4)]
    public string RootVerse { get; set; } = "asgard";

    [Key(5)]
    public string CanonicalService { get; set; } = "";

    [Key(6)]
    public string LocatedService { get; set; } = "";

    [Key(7)]
    public string CultMeshAddress { get; set; } = "";

    [Key(8)]
    public string Title { get; set; } = "";

    [Key(9)]
    public string Kind { get; set; } = "game.runtime";

    [Key(10)]
    public string UpdatedAtUtc { get; set; } = "";

    [Key(11)]
    public EveProviderFreshness Freshness { get; set; } = new();

    [Key(12)]
    public string[] Schemas { get; set; } = [];

    [Key(13)]
    public EveProviderWitness[] Witnesses { get; set; } = [];

    [Key(14)]
    public EveProviderSurfaceRef[] Surfaces { get; set; } = [];

    [Key(15)]
    public EveProviderCommandRef[] Commands { get; set; } = [];
}

[MessagePackObject]
public sealed class EveProviderFreshness
{
    [Key(0)]
    public string State { get; set; } = "fresh";

    [Key(1)]
    public string LastSeenAtUtc { get; set; } = "";

    [Key(2)]
    public int MaxAgeMs { get; set; } = 15000;
}

[MessagePackObject]
public sealed class EveProviderWitness
{
    [Key(0)]
    public string Kind { get; set; } = "";

    [Key(1)]
    public string Ref { get; set; } = "";

    [Key(2)]
    public string Summary { get; set; } = "";
}

[MessagePackObject]
public sealed class EveProviderSurfaceRef
{
    [Key(0)]
    public string Schema { get; set; } = "gamecult.eve.surface.v1";

    [Key(1)]
    public string SurfaceId { get; set; } = "";

    [Key(2)]
    public string Key { get; set; } = "";

    [Key(3)]
    public string Transport { get; set; } = "cultmesh";

    [Key(4)]
    public string Status { get; set; } = "available";
}

[MessagePackObject]
public sealed class EveProviderCommandRef
{
    [Key(0)]
    public string Command { get; set; } = "";

    [Key(1)]
    public string Transport { get; set; } = "cultmesh";

    [Key(2)]
    public string Summary { get; set; } = "";
}

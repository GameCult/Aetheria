using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("gamecult.eve.surface", "gamecult.eve.surface.v1")]
[MessagePackObject]
public sealed class EveSurfaceState
{
    [Key(0)]
    public string Type { get; set; } = "surface-state";

    [Key(1)]
    [CultIndex("schema")]
    public string Schema { get; set; } = "gamecult.eve.surface.v1";

    [Key(2)]
    [CultIndex("providerId")]
    public string ProviderId { get; set; } = "";

    [Key(3)]
    [CultIndex("providerKind")]
    public string ProviderKind { get; set; } = "";

    [Key(4)]
    [CultName]
    public string Title { get; set; } = "";

    [Key(5)]
    public long Version { get; set; }

    [Key(6)]
    public string UpdatedAtUtc { get; set; } = "";

    [Key(7)]
    public EveSurface Surface { get; set; } = new();

    [Key(8)]
    public EveCommandTemplate[] Commands { get; set; } = [];
}

[MessagePackObject]
public sealed class EveSurface
{
    [Key(0)]
    public string Id { get; set; } = "";

    [Key(1)]
    public EveSurfaceComponent Root { get; set; } = new();

    [Key(2)]
    public EveStyleToken[] Styles { get; set; } = [];
}

[MessagePackObject]
public sealed class EveSurfaceComponent
{
    [Key(0)]
    public string Id { get; set; } = "";

    [Key(1)]
    public string Kind { get; set; } = "";

    [Key(2)]
    public Dictionary<string, string> Props { get; set; } = new();

    [Key(3)]
    public EveSurfaceComponent[] Children { get; set; } = [];
}

[MessagePackObject]
public sealed class EveStyleToken
{
    [Key(0)]
    public string Name { get; set; } = "";

    [Key(1)]
    public string Value { get; set; } = "";
}

[MessagePackObject]
public sealed class EveCommandTemplate
{
    [Key(0)]
    public string Command { get; set; } = "";

    [Key(1)]
    public string Label { get; set; } = "";

    [Key(2)]
    public string Transport { get; set; } = "cultmesh";
}

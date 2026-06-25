using GameCult.Caching;
using MessagePack;

namespace Aetheria.State.Documents;

[CultDocument("aetheria.trade_value_policy", "aetheria.trade_value_policy.v1")]
[CultGlobal]
[MessagePackObject]
public sealed class AetheriaTradeValuePolicy
{
    public const string RecordKey = "global:aetheria.trade_value_policy.v1";

    [Key(0)] public string Schema { get; set; } = "aetheria.trade_value_policy.v1";
    [Key(1)] public string Name { get; set; } = "Aetheria trade value policy";
    [Key(2)] public AetheriaExponentialLerp QualityPriceModifier { get; set; } = new();
    [Key(3)] public AetheriaItemRarityTier[] Tiers { get; set; } = [];
    [Key(4)] public string UpdatedAtUtc { get; set; } = "";
}

[MessagePackObject]
public sealed class AetheriaExponentialLerp
{
    [Key(0)] public double Exponent { get; set; }
    [Key(1)] public double Minimum { get; set; }
    [Key(2)] public double Maximum { get; set; }
}

[MessagePackObject]
public sealed class AetheriaItemRarityTier
{
    [Key(0)] public string Name { get; set; } = "";
    [Key(1)] public double Quality { get; set; }
    [Key(2)] public double Red { get; set; }
    [Key(3)] public double Green { get; set; }
    [Key(4)] public double Blue { get; set; }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeTradeValuePolicySurfaceBuilder
    {
        public const string SurfaceId = "aetheria.tradeValuePolicy";

        public static AetheriaRuntimeSurfaceDocument BuildFromCatalog(
            AetheriaRuntimeCatalogSnapshot? catalog,
            long version = 1)
        {
            var settings = catalog?.TradeValueSettings ?? AetheriaRuntimeTradeValueSettings.Default;

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "game.design",
                title: "Aetheria Trade Value Policy",
                version: version,
                updatedAtUtc: DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                surface: new AetheriaRuntimeSurfaceTree(
                    SurfaceId,
                    Node(
                        "aetheria.tradeValuePolicy.root",
                        "surface",
                        Array.Empty<(string Key, string Value)>(),
                        BuildSummaryCard(settings),
                        BuildQualityCurveCard(settings.QualityPriceModifier),
                        BuildTierList(settings.Tiers)),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: BuildCommandTemplates());
        }

        private static AetheriaRuntimeSurfaceComponent BuildSummaryCard(
            AetheriaRuntimeTradeValueSettings settings)
        {
            return Node(
                "aetheria.tradeValuePolicy.summary",
                "card",
                new[] { ("title", "Trade Value Policy") },
                Metric(
                    "aetheria.tradeValuePolicy.summary.tiers",
                    "Rarity Tiers",
                    (settings?.Tiers.Count ?? 0).ToString(CultureInfo.InvariantCulture)),
                Metric(
                    "aetheria.tradeValuePolicy.summary.qualityModifier",
                    "Quality Price",
                    $"{Format(settings?.QualityPriceModifier.Minimum ?? 0)} -> {Format(settings?.QualityPriceModifier.Maximum ?? 0)}"),
                Text(
                    "aetheria.tradeValuePolicy.summary.note",
                    "Trade value is authored as typed policy state so inventory, trade, loadout pricing, and Starbridge station stock read the same curve."));
        }

        private static AetheriaRuntimeSurfaceComponent BuildQualityCurveCard(
            AetheriaRuntimeExponentialLerp curve)
        {
            return Node(
                "aetheria.tradeValuePolicy.quality",
                "card",
                new[] { ("title", "Quality Price Modifier") },
                NumberInput(
                    "aetheria.tradeValuePolicy.quality.minimum",
                    "Minimum",
                    curve.Minimum,
                    AetheriaRuntimeTradeValuePolicyCommands.SetQualityMinimum),
                NumberInput(
                    "aetheria.tradeValuePolicy.quality.maximum",
                    "Maximum",
                    curve.Maximum,
                    AetheriaRuntimeTradeValuePolicyCommands.SetQualityMaximum),
                NumberInput(
                    "aetheria.tradeValuePolicy.quality.exponent",
                    "Exponent",
                    curve.Exponent,
                    AetheriaRuntimeTradeValuePolicyCommands.SetQualityExponent),
                Row(
                    "aetheria.tradeValuePolicy.quality.samples",
                    Metric("aetheria.tradeValuePolicy.quality.sample.low", "Q 0.25", Format(curve.Evaluate(0.25))),
                    Metric("aetheria.tradeValuePolicy.quality.sample.mid", "Q 0.50", Format(curve.Evaluate(0.50))),
                    Metric("aetheria.tradeValuePolicy.quality.sample.high", "Q 0.75", Format(curve.Evaluate(0.75))),
                    Metric("aetheria.tradeValuePolicy.quality.sample.perfect", "Q 1.00", Format(curve.Evaluate(1.00)))));
        }

        private static AetheriaRuntimeSurfaceComponent BuildTierList(
            IReadOnlyList<AetheriaRuntimeItemRarityTier> tiers)
        {
            var rows = (tiers ?? Array.Empty<AetheriaRuntimeItemRarityTier>())
                .Select((tier, index) => TierRow(index, tier))
                .ToArray();

            return Node(
                "aetheria.tradeValuePolicy.tiers",
                "card",
                new[] { ("title", "Rarity Tiers") },
                rows.Length == 0
                    ? Text("aetheria.tradeValuePolicy.tiers.empty", "No rarity tiers are authored.")
                    : Row("aetheria.tradeValuePolicy.tiers.rows", rows));
        }

        private static AetheriaRuntimeSurfaceComponent TierRow(
            int index,
            AetheriaRuntimeItemRarityTier tier)
        {
            return Node(
                $"aetheria.tradeValuePolicy.tiers.{index}",
                "inspector.kv",
                new[]
                {
                    ("name", tier.Name),
                    ("quality", Format(tier.Quality)),
                    ("swatch", ToHex(tier.Red, tier.Green, tier.Blue))
                },
                NumberInput(
                    $"aetheria.tradeValuePolicy.tiers.{index}.quality",
                    "Quality",
                    tier.Quality,
                    AetheriaRuntimeTradeValuePolicyCommands.SetTierQuality,
                    ("tierIndex", index.ToString(CultureInfo.InvariantCulture))));
        }

        private static IReadOnlyList<AetheriaRuntimeSurfaceCommandTemplate> BuildCommandTemplates()
        {
            return new[]
            {
                new AetheriaRuntimeSurfaceCommandTemplate(AetheriaRuntimeTradeValuePolicyCommands.Refresh, "Refresh", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                new AetheriaRuntimeSurfaceCommandTemplate(AetheriaRuntimeTradeValuePolicyCommands.SetQualityMinimum, "Set Quality Minimum", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                new AetheriaRuntimeSurfaceCommandTemplate(AetheriaRuntimeTradeValuePolicyCommands.SetQualityMaximum, "Set Quality Maximum", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                new AetheriaRuntimeSurfaceCommandTemplate(AetheriaRuntimeTradeValuePolicyCommands.SetQualityExponent, "Set Quality Exponent", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                new AetheriaRuntimeSurfaceCommandTemplate(AetheriaRuntimeTradeValuePolicyCommands.SetTierQuality, "Set Tier Quality", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport)
            };
        }

        private static AetheriaRuntimeSurfaceComponent Metric(string id, string label, string value)
        {
            return Node(id, "metric", new[] { ("label", label), ("value", value ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent Text(string id, string value)
        {
            return Node(id, "text", new[] { ("value", value ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent Row(
            string id,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "row", Array.Empty<(string Key, string Value)>(), children);
        }

        private static AetheriaRuntimeSurfaceComponent NumberInput(
            string id,
            string label,
            double value,
            string command,
            params (string Key, string Value)[] extraProps)
        {
            var props = new List<(string Key, string Value)>
            {
                ("label", label ?? ""),
                ("value", Format(value)),
                ("command", command ?? ""),
                ("valueKind", "number")
            };
            if (extraProps != null)
            {
                props.AddRange(extraProps);
            }

            return Node(id, "control.text", props);
        }

        private static AetheriaRuntimeSurfaceComponent Node(
            string id,
            string kind,
            IEnumerable<(string Key, string Value)> props,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return new AetheriaRuntimeSurfaceComponent(
                id,
                kind,
                props.ToDictionary(prop => prop.Key, prop => prop.Value ?? "", StringComparer.Ordinal),
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>());
        }

        private static string Format(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string ToHex(double red, double green, double blue)
        {
            return "#" +
                ToByte(red).ToString("X2", CultureInfo.InvariantCulture) +
                ToByte(green).ToString("X2", CultureInfo.InvariantCulture) +
                ToByte(blue).ToString("X2", CultureInfo.InvariantCulture);
        }

        private static int ToByte(double value)
        {
            if (value <= 0)
                return 0;
            if (value >= 1)
                return 255;
            return (int)Math.Round(value * 255);
        }
    }

    public static class AetheriaRuntimeTradeValuePolicyCommands
    {
        public const string SurfaceId = AetheriaRuntimeTradeValuePolicySurfaceBuilder.SurfaceId;
        public const string Refresh = "aetheria.trade_value_policy.refresh";
        public const string SetQualityMinimum = "aetheria.trade_value_policy.quality.minimum.set";
        public const string SetQualityMaximum = "aetheria.trade_value_policy.quality.maximum.set";
        public const string SetQualityExponent = "aetheria.trade_value_policy.quality.exponent.set";
        public const string SetTierQuality = "aetheria.trade_value_policy.tier.quality.set";

        public static bool IsKnown(string command)
        {
            return command == Refresh ||
                command == SetQualityMinimum ||
                command == SetQualityMaximum ||
                command == SetQualityExponent ||
                command == SetTierQuality;
        }
    }
}

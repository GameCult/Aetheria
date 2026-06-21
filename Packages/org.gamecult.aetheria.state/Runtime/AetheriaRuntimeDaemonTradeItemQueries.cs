using System;
using System.Collections.Generic;
using System.Globalization;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public readonly struct AetheriaRuntimeExponentialLerp
    {
        public AetheriaRuntimeExponentialLerp(double exponent, double minimum, double maximum)
        {
            Exponent = exponent;
            Minimum = minimum;
            Maximum = maximum;
        }

        public double Exponent { get; }
        public double Minimum { get; }
        public double Maximum { get; }

        public double Evaluate(double value)
        {
            var t = value < 0 ? 0 : value > 1 ? 1 : value;
            return Minimum + Math.Pow(t, Exponent) * (Maximum - Minimum);
        }
    }

    public readonly struct AetheriaRuntimeItemRarityTier
    {
        public AetheriaRuntimeItemRarityTier(string name, double quality, double red, double green, double blue)
        {
            Name = name ?? "";
            Quality = quality;
            Red = red;
            Green = green;
            Blue = blue;
        }

        public string Name { get; }
        public double Quality { get; }
        public double Red { get; }
        public double Green { get; }
        public double Blue { get; }
    }

    public sealed class AetheriaRuntimeTradeValueSettings
    {
        public AetheriaRuntimeTradeValueSettings(
            AetheriaRuntimeExponentialLerp qualityPriceModifier,
            IReadOnlyList<AetheriaRuntimeItemRarityTier> tiers)
        {
            QualityPriceModifier = qualityPriceModifier;
            Tiers = tiers ?? Array.Empty<AetheriaRuntimeItemRarityTier>();
        }

        public AetheriaRuntimeExponentialLerp QualityPriceModifier { get; }
        public IReadOnlyList<AetheriaRuntimeItemRarityTier> Tiers { get; }
    }

    public readonly struct AetheriaRuntimeTradeItemProjection
    {
        public AetheriaRuntimeTradeItemProjection(
            int price,
            string tierName,
            string tierColorHex,
            int upgrades)
        {
            Price = price;
            TierName = tierName ?? "";
            TierColorHex = tierColorHex ?? "";
            Upgrades = upgrades;
        }

        public int Price { get; }
        public string TierName { get; }
        public string TierColorHex { get; }
        public int Upgrades { get; }
        public bool HasTier => !string.IsNullOrWhiteSpace(TierName);
    }

    public static class AetheriaRuntimeDaemonTradeItemQueries
    {
        public static AetheriaRuntimeTradeItemProjection ProjectTradeItem(
            AetheriaRuntimeCatalogItem? typedItem,
            AetheriaRuntimeLoadoutItemCommit? item,
            AetheriaRuntimeTradeValueSettings? settings)
        {
            if (typedItem == null)
                return default;

            var quality = item?.Quality;
            var price = quality.HasValue && settings != null
                ? (int)(settings.QualityPriceModifier.Evaluate(quality.Value) * typedItem.Price)
                : typedItem.Price;

            var tier = quality.HasValue && settings != null
                ? SelectTier(settings.Tiers, quality.Value)
                : null;
            var upgrades = tier.HasValue && quality.HasValue
                ? Upgrades(tier.Value, quality.Value)
                : 0;

            return new AetheriaRuntimeTradeItemProjection(
                price,
                tier?.Name ?? "",
                tier.HasValue ? ToHex(tier.Value) : "",
                upgrades);
        }

        public static AetheriaRuntimeLoadoutItemCommit CraftedItemCommit(
            string itemKey,
            double quality,
            double durability = 1)
        {
            return AetheriaRuntimeDaemonItemStatQueries.ItemCommit(
                itemKey,
                quality,
                durability);
        }

        private static AetheriaRuntimeItemRarityTier? SelectTier(
            IReadOnlyList<AetheriaRuntimeItemRarityTier> tiers,
            double quality)
        {
            if (tiers == null || tiers.Count == 0)
                return null;

            var tier = tiers[0];
            foreach (var candidate in tiers)
            {
                if (quality + .001 > candidate.Quality)
                    tier = candidate;
            }

            return tier;
        }

        private static int Upgrades(AetheriaRuntimeItemRarityTier tier, double quality)
        {
            return (int)((quality - tier.Quality) / .0499);
        }

        private static string ToHex(AetheriaRuntimeItemRarityTier tier)
        {
            return $"{ToByte(tier.Red):X2}{ToByte(tier.Green):X2}{ToByte(tier.Blue):X2}";
        }

        private static int ToByte(double value)
        {
            var scaled = Clamp01(value) * 255;
            return int.Parse(
                Math.Round(scaled).ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture);
        }

        private static double Clamp01(double value)
        {
            if (value < 0)
                return 0;
            return value > 1 ? 1 : value;
        }
    }
}

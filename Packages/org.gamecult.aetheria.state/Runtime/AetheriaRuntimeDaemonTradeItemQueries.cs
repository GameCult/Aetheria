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
        public static AetheriaRuntimeTradeValueSettings Default { get; } = new AetheriaRuntimeTradeValueSettings(
            new AetheriaRuntimeExponentialLerp(3, 0.25, 8),
            new[]
            {
                new AetheriaRuntimeItemRarityTier("Common", 0.3, 0.75, 0.75, 0.75),
                new AetheriaRuntimeItemRarityTier("Uncommon", 0.45, 0, 1, 0.5),
                new AetheriaRuntimeItemRarityTier("Rare", 0.6, 0, 0.6, 1),
                new AetheriaRuntimeItemRarityTier("Epic", 0.75, 0.75, 0, 1),
                new AetheriaRuntimeItemRarityTier("Legendary", 0.9, 1, 0.4, 0),
            });

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

    public readonly struct AetheriaRuntimeTradeItemValue
    {
        public AetheriaRuntimeTradeItemValue(
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
        public static AetheriaRuntimeTradeItemValue TradeItemValue(
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

            return new AetheriaRuntimeTradeItemValue(
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

        public static bool TryProjectLoadoutTemplatePrice(
            AetheriaRuntimeLoadoutTemplateSnapshot? template,
            AetheriaRuntimeCatalogSnapshot? catalog,
            AetheriaRuntimeTradeValueSettings? settings,
            out int price)
        {
            price = 0;
            return TryProjectEntityLoadoutPrice(template?.RootEntity, catalog, settings, out price);
        }

        private static bool TryProjectEntityLoadoutPrice(
            AetheriaRuntimeEntityLoadoutSnapshot? entity,
            AetheriaRuntimeCatalogSnapshot? catalog,
            AetheriaRuntimeTradeValueSettings? settings,
            out int price)
        {
            price = 0;
            if (entity == null || catalog == null)
                return false;

            if (!TryAddLoadoutItemPrice(entity.Hull, catalog, settings, ref price))
                return false;

            if (!TryAddSlotPrices(entity.Equipment, catalog, settings, ref price) ||
                !TryAddSlotPrices(entity.CargoBays, catalog, settings, ref price) ||
                !TryAddSlotPrices(entity.DockingBays, catalog, settings, ref price) ||
                !TryAddCargoPrices(entity.CargoContents, catalog, settings, ref price) ||
                !TryAddCargoPrices(entity.DockingBayContents, catalog, settings, ref price))
            {
                return false;
            }

            foreach (var child in entity.Children ?? Array.Empty<AetheriaRuntimeEntityLoadoutSnapshot>())
            {
                if (!TryProjectEntityLoadoutPrice(child, catalog, settings, out var childPrice))
                    return false;

                price += childPrice;
            }

            return true;
        }

        private static bool TryAddSlotPrices(
            IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot>? slots,
            AetheriaRuntimeCatalogSnapshot catalog,
            AetheriaRuntimeTradeValueSettings? settings,
            ref int price)
        {
            foreach (var slot in slots ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotSnapshot>())
            {
                if (!TryAddLoadoutItemPrice(slot?.Item, catalog, settings, ref price))
                    return false;
            }

            return true;
        }

        private static bool TryAddCargoPrices(
            IReadOnlyList<AetheriaRuntimeCargoBayLoadoutSnapshot>? bays,
            AetheriaRuntimeCatalogSnapshot catalog,
            AetheriaRuntimeTradeValueSettings? settings,
            ref int price)
        {
            foreach (var bay in bays ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutSnapshot>())
            {
                if (!TryAddSlotPrices(bay?.Items, catalog, settings, ref price))
                    return false;
            }

            return true;
        }

        private static bool TryAddLoadoutItemPrice(
            AetheriaRuntimeLoadoutItemSnapshot? item,
            AetheriaRuntimeCatalogSnapshot catalog,
            AetheriaRuntimeTradeValueSettings? settings,
            ref int price)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemKey))
                return false;

            var typedItem = catalog.FindItem(item.ItemKey);
            if (typedItem == null)
                return false;

            if (typedItem.Stackable)
            {
                price += typedItem.Price * Math.Max(1, item.Quantity);
                return true;
            }

            price += TradeItemValue(
                typedItem,
                CraftedItemCommit(item.ItemKey, item.Quality, item.Durability),
                settings).Price;
            return true;
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

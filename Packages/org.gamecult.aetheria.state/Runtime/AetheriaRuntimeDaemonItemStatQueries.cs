using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public readonly struct AetheriaRuntimeDaemonItemStatValue
    {
        public AetheriaRuntimeDaemonItemStatValue(
            string itemKey,
            string behaviorKind,
            int behaviorGroup,
            int fieldKey,
            string label,
            double value)
        {
            ItemKey = itemKey ?? "";
            BehaviorKind = behaviorKind ?? "";
            BehaviorGroup = behaviorGroup;
            FieldKey = fieldKey;
            Label = label ?? "";
            Value = value;
        }

        public string ItemKey { get; }
        public string BehaviorKind { get; }
        public int BehaviorGroup { get; }
        public int FieldKey { get; }
        public string Label { get; }
        public double Value { get; }
        public string ValueRef => AetheriaRuntimeDaemonItemStatQueries.ItemStatRef(
            ItemKey,
            BehaviorKind,
            BehaviorGroup,
            FieldKey);
    }

    public static class AetheriaRuntimeDaemonItemStatQueries
    {
        public const string StateRefPrefix = "aetheria.state/items";

        public static string ItemStatRef(
            string itemKey,
            string behaviorKind,
            int behaviorGroup,
            int fieldKey)
        {
            return $"{StateRefPrefix}/{Token(itemKey)}/behaviors/{Token(behaviorKind)}/{behaviorGroup}/stats/{fieldKey}";
        }

        public static AetheriaRuntimeStatRecipePreviewState ConditionsFor(
            AetheriaRuntimeLoadoutItemCommit? item,
            double heat = 0,
            double charge = 1,
            double ammo = 1,
            double range = 1,
            double integrity = 1,
            double pilotSkill = 0,
            double environment = 0)
        {
            return new AetheriaRuntimeStatRecipePreviewState(
                quality: Clamp01(item?.Quality ?? 1),
                durability: Clamp01(item?.Durability ?? 1),
                heat: Clamp01(heat),
                charge: Clamp01(charge),
                ammo: Clamp01(ammo),
                range: Clamp01(range),
                integrity: Clamp01(integrity),
                pilotSkill: Clamp01(pilotSkill),
                environment: Clamp01(environment));
        }

        public static double EvaluatePerformanceStat(
            AetheriaRuntimeBehaviorValue? value,
            AetheriaRuntimeLoadoutItemCommit? item,
            double heat = 0)
        {
            return EvaluatePerformanceStat(
                ReadPerformanceStat(value),
                ConditionsFor(item, heat));
        }

        public static double EvaluatePerformanceStat(
            AetheriaRuntimePerformanceStat? stat,
            AetheriaRuntimeStatRecipePreviewState conditions)
        {
            if (stat == null)
                return 0;

            conditions ??= AetheriaRuntimeStatRecipePreviewState.Default;
            if (stat.Recipe != null)
                return EvaluateRecipe(stat.Recipe, conditions);

            var heat = stat.HeatExponentMultiplier == 0
                ? 1
                : Math.Pow(Clamp01(conditions.Heat), stat.HeatExponentMultiplier);
            var durability = stat.DurabilityExponentMultiplier == 0
                ? 1
                : Math.Pow(Clamp01(conditions.Durability), stat.DurabilityExponentMultiplier);
            var quality = stat.QualityExponent == 0
                ? 1
                : Math.Pow(Clamp01(conditions.Quality), stat.QualityExponent);

            return Lerp(stat.Min, stat.Max, durability * quality * heat);
        }

        public static IReadOnlyList<AetheriaRuntimeDaemonItemStatValue> QueryPerformanceStats(
            AetheriaRuntimeCatalogItem? catalogItem,
            AetheriaRuntimeLoadoutItemCommit? item,
            double heat = 0)
        {
            if (catalogItem?.BehaviorPayloads == null || catalogItem.BehaviorPayloads.Count == 0)
                return Array.Empty<AetheriaRuntimeDaemonItemStatValue>();

            var conditions = ConditionsFor(item, heat);
            var stats = new List<AetheriaRuntimeDaemonItemStatValue>();
            foreach (var behavior in catalogItem.BehaviorPayloads)
            {
                var metadata = AetheriaRuntimeBehaviorMetadataCatalog.Get(behavior.Kind);
                foreach (var field in behavior.Fields ?? Array.Empty<AetheriaRuntimeBehaviorField>())
                {
                    var fieldMetadata = metadata?.DisplayFields.FirstOrDefault(candidate => candidate.Key == field.Key);
                    if (fieldMetadata?.ValueKind != AetheriaRuntimeBehaviorFieldValueKind.PerformanceStat)
                        continue;

                    stats.Add(new AetheriaRuntimeDaemonItemStatValue(
                        item?.ItemKey ?? catalogItem.ItemKey,
                        behavior.Kind,
                        behavior.Group,
                        field.Key,
                        fieldMetadata.Name,
                        EvaluatePerformanceStat(ReadPerformanceStat(field.Value), conditions)));
                }
            }

            return stats.Count == 0 ? Array.Empty<AetheriaRuntimeDaemonItemStatValue>() : stats;
        }

        public static AetheriaRuntimeLoadoutItemCommit ItemCommit(
            string itemKey,
            double quality,
            double durability,
            int quantity = 1,
            bool enabled = true,
            bool overrideShutdown = false)
        {
            return new AetheriaRuntimeLoadoutItemCommit
            {
                ItemKey = itemKey ?? "",
                Quality = quality,
                Durability = durability,
                Quantity = quantity,
                Enabled = enabled,
                OverrideShutdown = overrideShutdown
            };
        }

        private static AetheriaRuntimePerformanceStat ReadPerformanceStat(AetheriaRuntimeBehaviorValue? value)
        {
            return new AetheriaRuntimePerformanceStat(
                ChildNumber(value, 0),
                ChildNumber(value, 1),
                ChildNumber(value, 2),
                ChildNumber(value, 3),
                ChildNumber(value, 4),
                ReadStatRecipe(ChildValue(value, 5)));
        }

        private static AetheriaRuntimeStatRecipe? ReadStatRecipe(AetheriaRuntimeBehaviorValue? value)
        {
            if (value == null || value.Children.Count == 0)
                return null;

            var modifiers = ChildValue(value, 1)?.Children
                .Select(ReadStatRecipeModifier)
                .Where(modifier => modifier != null)
                .Cast<AetheriaRuntimeStatRecipeModifier>()
                .ToArray() ?? Array.Empty<AetheriaRuntimeStatRecipeModifier>();

            return new AetheriaRuntimeStatRecipe(ChildNumber(value, 0), modifiers);
        }

        private static AetheriaRuntimeStatRecipeModifier? ReadStatRecipeModifier(AetheriaRuntimeBehaviorValue? value)
        {
            if (value == null)
                return null;

            return new AetheriaRuntimeStatRecipeModifier(
                ChildString(value, 0),
                ChildString(value, 1),
                ChildNumber(value, 2),
                ReadCurveKeys(ChildValue(value, 3)),
                value.Children.Count <= 4 || ChildValue(value, 4)?.BoolValue == true);
        }

        private static IReadOnlyList<AetheriaRuntimeCurveKey> ReadCurveKeys(AetheriaRuntimeBehaviorValue? value)
        {
            if (value?.Children == null || value.Children.Count == 0)
                return Array.Empty<AetheriaRuntimeCurveKey>();

            return value.Children
                .Where(key => key.Children.Count >= 4)
                .Select(key => new AetheriaRuntimeCurveKey(
                    ChildNumber(key, 0),
                    ChildNumber(key, 1),
                    ChildNumber(key, 2),
                    ChildNumber(key, 3)))
                .ToArray();
        }

        private static double EvaluateRecipe(
            AetheriaRuntimeStatRecipe recipe,
            AetheriaRuntimeStatRecipePreviewState conditions)
        {
            var value = recipe.BaseValue;
            foreach (var modifier in recipe.Modifiers ?? Array.Empty<AetheriaRuntimeStatRecipeModifier>())
            {
                if (modifier == null || !modifier.Enabled || string.IsNullOrWhiteSpace(modifier.Condition))
                    continue;

                var sample = SampleCurve(modifier.CurveKeys, conditions.GetConditionValue(modifier.Condition));
                switch (modifier.Operation ?? "")
                {
                    case AetheriaRuntimeStatRecipeOperations.Multiply:
                        value *= 1 + ((modifier.Amount - 1) * sample);
                        break;
                    case AetheriaRuntimeStatRecipeOperations.Override:
                        value = Lerp(value, modifier.Amount, sample);
                        break;
                    default:
                        value += modifier.Amount * sample;
                        break;
                }
            }

            return value;
        }

        private static double SampleCurve(IReadOnlyList<AetheriaRuntimeCurveKey>? keys, double value)
        {
            if (keys == null || keys.Count == 0)
                return Clamp01(value);

            var ordered = keys.OrderBy(key => key.Time).ToArray();
            if (value <= ordered[0].Time)
                return Clamp01(ordered[0].Value);

            for (var index = 1; index < ordered.Length; index++)
            {
                var next = ordered[index];
                var previous = ordered[index - 1];
                if (value > next.Time)
                    continue;

                var span = next.Time - previous.Time;
                var t = span <= double.Epsilon ? 1 : Clamp01((value - previous.Time) / span);
                return Clamp01(Lerp(previous.Value, next.Value, t));
            }

            return Clamp01(ordered[ordered.Length - 1].Value);
        }

        private static double ChildNumber(AetheriaRuntimeBehaviorValue? value, int index)
        {
            return ChildValue(value, index)?.NumberValue ?? 0;
        }

        private static string ChildString(AetheriaRuntimeBehaviorValue? value, int index)
        {
            return ChildValue(value, index)?.StringValue ?? "";
        }

        private static AetheriaRuntimeBehaviorValue? ChildValue(AetheriaRuntimeBehaviorValue? value, int index)
        {
            return value != null && value.Children.Count > index ? value.Children[index] : null;
        }

        private static double Clamp01(double value)
        {
            if (value < 0)
                return 0;
            return value > 1 ? 1 : value;
        }

        private static double Lerp(double from, double to, double t)
        {
            return from + ((to - from) * t);
        }

        private static string Token(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "_"
                : value.Trim().Replace("/", "%2F");
        }
    }
}

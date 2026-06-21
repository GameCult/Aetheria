using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Eve.Surface;

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeCargoItemMetric
    {
        public AetheriaRuntimeCargoItemMetric(string id, string label, string value, string valueRef = "")
        {
            Id = id ?? "";
            Label = label ?? "";
            Value = value ?? "";
            ValueRef = valueRef ?? "";
        }

        public string Id { get; }
        public string Label { get; }
        public string Value { get; }
        public string ValueRef { get; }
    }

    public sealed class AetheriaRuntimeCargoItemSection
    {
        public AetheriaRuntimeCargoItemSection(
            string id,
            string title,
            IReadOnlyList<AetheriaRuntimeCargoItemMetric> metrics)
        {
            Id = id ?? "";
            Title = title ?? "";
            Metrics = metrics ?? Array.Empty<AetheriaRuntimeCargoItemMetric>();
        }

        public string Id { get; }
        public string Title { get; }
        public IReadOnlyList<AetheriaRuntimeCargoItemMetric> Metrics { get; }
    }

    public sealed class AetheriaRuntimeCargoItemDetailsSurfaceState
    {
        public AetheriaRuntimeCargoItemDetailsSurfaceState(
            string itemName,
            string description,
            string manufacturer,
            string mass,
            int price,
            int quantity,
            string tier,
            string durability,
            string thermalRange,
            IReadOnlyList<AetheriaRuntimeCargoItemSection> behaviorSections,
            string updatedAtUtc)
        {
            ItemName = itemName ?? "";
            Description = description ?? "";
            Manufacturer = manufacturer ?? "";
            Mass = mass ?? "";
            Price = price;
            Quantity = quantity;
            Tier = tier ?? "";
            Durability = durability ?? "";
            ThermalRange = thermalRange ?? "";
            BehaviorSections = behaviorSections ?? Array.Empty<AetheriaRuntimeCargoItemSection>();
            UpdatedAtUtc = updatedAtUtc ?? "";
        }

        public string ItemName { get; }
        public string Description { get; }
        public string Manufacturer { get; }
        public string Mass { get; }
        public int Price { get; }
        public int Quantity { get; }
        public string Tier { get; }
        public string Durability { get; }
        public string ThermalRange { get; }
        public IReadOnlyList<AetheriaRuntimeCargoItemSection> BehaviorSections { get; }
        public string UpdatedAtUtc { get; }
        public bool HasQuantity => Quantity > 0;
        public bool HasEquipmentStatus => !string.IsNullOrWhiteSpace(Tier) ||
                                          !string.IsNullOrWhiteSpace(Durability) ||
                                          !string.IsNullOrWhiteSpace(ThermalRange);
    }

    public sealed class AetheriaRuntimeCargoItemObservation
    {
        public AetheriaRuntimeCargoItemObservation(
            string itemKey,
            int quantity,
            bool isEquippable,
            double quality,
            double durability,
            double temperature,
            bool overrideShutdown)
        {
            ItemKey = itemKey ?? "";
            Quantity = quantity;
            IsEquippable = isEquippable;
            Quality = quality;
            Durability = durability;
            Temperature = temperature;
            OverrideShutdown = overrideShutdown;
        }

        public string ItemKey { get; }
        public int Quantity { get; }
        public bool IsEquippable { get; }
        public double Quality { get; }
        public double Durability { get; }
        public double Temperature { get; }
        public bool OverrideShutdown { get; }
    }

    public static class AetheriaRuntimeCargoItemDetailsSurfaceBuilder
    {
        public const string SurfaceId = "aetheria.inventory.cargo_item_details";
        public const string Close = "aetheria.inventory.cargo_item_details.close";

        public static AetheriaRuntimeCargoItemDetailsSurfaceState Project(
            AetheriaRuntimeCatalogItem typedItem,
            AetheriaRuntimeCargoItemObservation item,
            string manufacturer,
            string tier,
            Func<float, string> formatValue,
            Func<float, string> formatTemperature,
            DateTime updatedAtUtc = default(DateTime))
        {
            if (updatedAtUtc == default(DateTime))
                updatedAtUtc = DateTime.UtcNow;

            if (typedItem == null)
            {
                return new AetheriaRuntimeCargoItemDetailsSurfaceState(
                    "",
                    "",
                    manufacturer ?? "",
                    "",
                    0,
                    item?.Quantity ?? 0,
                    "",
                    "",
                    "",
                    Array.Empty<AetheriaRuntimeCargoItemSection>(),
                    updatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            }

            var quantity = item?.Quantity ?? 0;
            var durability = "";
            var thermalRange = "";
            var behaviorSections = Array.Empty<AetheriaRuntimeCargoItemSection>();

            if (item != null && item.IsEquippable)
            {
                durability = $"{(int)(item.Durability / MaxDurability(typedItem, item) * 100)}%";
                thermalRange = FormatTemperatureRange(typedItem, formatTemperature);
                behaviorSections = ProjectBehaviorSections(typedItem, item, formatValue, formatTemperature).ToArray();
            }

            return new AetheriaRuntimeCargoItemDetailsSurfaceState(
                typedItem.Name,
                typedItem.Description ?? "",
                manufacturer ?? "",
                FormatValue(Mass(typedItem, quantity), formatValue),
                typedItem.Price,
                quantity,
                tier ?? "",
                durability,
                thermalRange,
                behaviorSections,
                updatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        }

        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeCargoItemDetailsSurfaceState state,
            long version = 1)
        {
            state ??= new AetheriaRuntimeCargoItemDetailsSurfaceState(
                "",
                "",
                "",
                "",
                0,
                0,
                "",
                "",
                "",
                Array.Empty<AetheriaRuntimeCargoItemSection>(),
                "");

            var children = new List<AetheriaRuntimeSurfaceComponent>
            {
                Card(
                    $"{SurfaceId}.summary",
                    state.ItemName,
                    Text(
                        $"{SurfaceId}.description",
                        string.IsNullOrWhiteSpace(state.Description)
                            ? "No typed item description is available."
                            : state.Description),
                    Text(
                        $"{SurfaceId}.note",
                        "The observing client supplies the selected item; the shared runtime surface owns cargo-item inspection layout."),
                    Metric($"{SurfaceId}.manufacturer", "Manufacturer", state.Manufacturer),
                    Metric($"{SurfaceId}.mass", "Mass", state.Mass))
            };

            if (state.Price > 0)
            {
                children.Add(Card(
                    $"{SurfaceId}.market.card",
                    "Market",
                    Metric($"{SurfaceId}.price", "Price", state.Price.ToString("N0"))));
            }

            if (state.HasQuantity)
            {
                children.Add(Card(
                    $"{SurfaceId}.quantity.card",
                    "Quantity",
                    Metric($"{SurfaceId}.quantity", "Units", state.Quantity.ToString())));
            }

            if (state.HasEquipmentStatus)
            {
                children.Add(Card(
                    $"{SurfaceId}.status.card",
                    "Status",
                    Metric($"{SurfaceId}.tier", "Tier", state.Tier),
                    Metric($"{SurfaceId}.durability", "Durability", state.Durability),
                    Metric($"{SurfaceId}.temperature_range", "Thermal Range", state.ThermalRange)));
            }

            foreach (var section in state.BehaviorSections)
            {
                if (section?.Metrics == null || section.Metrics.Count == 0)
                    continue;

                children.Add(Card(
                    string.IsNullOrWhiteSpace(section.Id)
                        ? $"{SurfaceId}.behavior.{SafeId(section.Title)}"
                        : section.Id,
                    section.Title,
                    section.Metrics
                        .Select(metric => Metric(metric.Id, metric.Label, metric.Value, metric.ValueRef))
                        .ToArray()));
            }

            children.Add(ButtonRow(
                $"{SurfaceId}.actions",
                Button($"{SurfaceId}.close", "Close", Close)));

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "inventory.menu",
                title: "Inventory Cargo Item Details",
                version: version,
                updatedAtUtc: state.UpdatedAtUtc,
                surface: new AetheriaRuntimeSurfaceTree(
                    SurfaceId,
                    Node(
                        $"{SurfaceId}.root",
                        "surface",
                        Array.Empty<(string Key, string Value)>(),
                        children.ToArray()),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: new[]
                {
                    new AetheriaRuntimeSurfaceCommandTemplate(Close, "Close", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport)
                });
        }

        private static IEnumerable<AetheriaRuntimeCargoItemSection> ProjectBehaviorSections(
            AetheriaRuntimeCatalogItem typedItem,
            AetheriaRuntimeCargoItemObservation item,
            Func<float, string> formatValue,
            Func<float, string> formatTemperature)
        {
            foreach (var behavior in typedItem.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>())
            {
                if (string.Equals(behavior.Kind, AetheriaRuntimeBehaviorKinds.StatModifier, StringComparison.Ordinal))
                {
                    var statReference = ReadStatReference(FindTypedBehaviorField(behavior, 1)?.Value);
                    var modifier = ReadPerformanceStat(FindTypedBehaviorField(behavior, 2)?.Value);
                    var modifierType = ReadEnum(
                        FindTypedBehaviorField(behavior, 3)?.Value,
                        AetheriaRuntimeCargoItemStatModifierType.Constant);
                    yield return new AetheriaRuntimeCargoItemSection(
                        $"{SurfaceId}.behavior.{behavior.Kind}.stat_modifier",
                        "Stat Modifier",
                        new[]
                        {
                            new AetheriaRuntimeCargoItemMetric(
                                $"{SurfaceId}.behavior.{behavior.Kind}.target",
                                $"{SplitCamelCase(statReference.Target)}:{SplitCamelCase(statReference.Stat)}",
                                $"{(modifierType == AetheriaRuntimeCargoItemStatModifierType.Constant ? "+" : "x")}{FormatCurrentItemStat(modifier, item, formatValue)}",
                                AetheriaRuntimeDaemonItemStatQueries.ItemStatRef(
                                    item.ItemKey,
                                    behavior.Kind,
                                    behavior.Group,
                                    2))
                        });
                    continue;
                }

                var metadata = AetheriaRuntimeBehaviorMetadataCatalog.Get(behavior.Kind);
                if (metadata == null)
                    continue;

                var fields = metadata.DisplayFields
                    .Select(field => ProjectBehaviorMetric(behavior, field, item, formatValue, formatTemperature))
                    .Where(metric => metric != null)
                    .ToArray();

                if (fields.Length == 0)
                    continue;

                yield return new AetheriaRuntimeCargoItemSection(
                    $"{SurfaceId}.behavior.{behavior.Kind}",
                    FormatTypeName(behavior.Kind),
                    fields);
            }
        }

        private static AetheriaRuntimeCargoItemMetric ProjectBehaviorMetric(
            AetheriaRuntimeBehaviorPayload behavior,
            AetheriaRuntimeBehaviorFieldMetadata field,
            AetheriaRuntimeCargoItemObservation item,
            Func<float, string> formatValue,
            Func<float, string> formatTemperature)
        {
            var payloadField = FindTypedBehaviorField(behavior, field.Key);
            if (payloadField == null)
                return null;

            string value;
            switch (field.ValueKind)
            {
                case AetheriaRuntimeBehaviorFieldValueKind.Number:
                    value = FormatValue(payloadField.Value.NumberValue, formatValue);
                    break;
                case AetheriaRuntimeBehaviorFieldValueKind.Temperature:
                    value = FormatTemperature(payloadField.Value.NumberValue, formatTemperature);
                    break;
                case AetheriaRuntimeBehaviorFieldValueKind.Integer:
                    value = ((int)payloadField.Value.NumberValue).ToString(CultureInfo.InvariantCulture);
                    break;
                case AetheriaRuntimeBehaviorFieldValueKind.PerformanceStat:
                    value = FormatCurrentItemStat(payloadField.Value, item, formatValue);
                    break;
                default:
                    return null;
            }

            return new AetheriaRuntimeCargoItemMetric(
                $"{SurfaceId}.behavior.{behavior.Kind}.{field.Key}",
                SplitCamelCase(field.Name),
                value,
                field.ValueKind == AetheriaRuntimeBehaviorFieldValueKind.PerformanceStat
                    ? AetheriaRuntimeDaemonItemStatQueries.ItemStatRef(
                        item.ItemKey,
                        behavior.Kind,
                        behavior.Group,
                        field.Key)
                    : "");
        }

        private static string FormatCurrentItemStat(
            AetheriaRuntimePerformanceStat stat,
            AetheriaRuntimeCargoItemObservation item,
            Func<float, string> formatValue)
        {
            return FormatValue(
                AetheriaRuntimeDaemonItemStatQueries.EvaluatePerformanceStat(
                    ToBehaviorValue(stat),
                    ToLoadoutItem(item),
                    item?.Temperature ?? 0),
                formatValue);
        }

        private static string FormatCurrentItemStat(
            AetheriaRuntimeBehaviorValue stat,
            AetheriaRuntimeCargoItemObservation item,
            Func<float, string> formatValue)
        {
            return FormatValue(
                AetheriaRuntimeDaemonItemStatQueries.EvaluatePerformanceStat(
                    stat,
                    ToLoadoutItem(item),
                    item?.Temperature ?? 0),
                formatValue);
        }

        private static AetheriaRuntimeLoadoutItemCommit ToLoadoutItem(AetheriaRuntimeCargoItemObservation item)
        {
            return AetheriaRuntimeDaemonItemStatQueries.ItemCommit(
                item?.ItemKey ?? "",
                item?.Quality ?? 1,
                item?.Durability ?? 1,
                enabled: true,
                overrideShutdown: item != null && item.OverrideShutdown,
                temperature: item?.Temperature ?? 0);
        }

        private static double Mass(AetheriaRuntimeCatalogItem typedItem, int quantity)
        {
            if (typedItem == null)
                return 0;

            return quantity > 0
                ? typedItem.Mass * quantity
                : typedItem.Mass;
        }

        private static double MaxDurability(
            AetheriaRuntimeCatalogItem typedItem,
            AetheriaRuntimeCargoItemObservation item)
        {
            if (typedItem != null && typedItem.Durability > 0)
                return typedItem.Durability;

            return Math.Max(item?.Durability ?? 1, 1);
        }

        private static string FormatTemperatureRange(
            AetheriaRuntimeCatalogItem item,
            Func<float, string> formatTemperature)
        {
            if (item.MaximumTemperature > item.MinimumTemperature)
            {
                return
                    $"{FormatTemperature(item.MinimumTemperature, formatTemperature)} to " +
                    $"{FormatTemperature(item.MaximumTemperature, formatTemperature)}";
            }

            return "No typed thermal range";
        }

        private static AetheriaRuntimeBehaviorField FindTypedBehaviorField(
            AetheriaRuntimeBehaviorPayload behavior,
            int? key)
        {
            return key == null
                ? null
                : behavior?.Fields?.FirstOrDefault(field => field.Key == key.Value);
        }

        private static AetheriaRuntimePerformanceStat ReadPerformanceStat(AetheriaRuntimeBehaviorValue value)
        {
            return new AetheriaRuntimePerformanceStat(
                ChildNumber(value, 0),
                ChildNumber(value, 1),
                ChildNumber(value, 2),
                ChildNumber(value, 3),
                ChildNumber(value, 4),
                ReadStatRecipe(ChildValue(value, 5)));
        }

        private static AetheriaRuntimeStatRecipe ReadStatRecipe(AetheriaRuntimeBehaviorValue value)
        {
            if (value == null || value.Children.Count == 0)
                return null;

            var modifiers = ChildValue(value, 1)?.Children
                .Select(ReadStatRecipeModifier)
                .Where(modifier => modifier != null)
                .ToArray() ?? Array.Empty<AetheriaRuntimeStatRecipeModifier>();

            return new AetheriaRuntimeStatRecipe(ChildNumber(value, 0), modifiers);
        }

        private static AetheriaRuntimeStatRecipeModifier ReadStatRecipeModifier(AetheriaRuntimeBehaviorValue value)
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

        private static IReadOnlyList<AetheriaRuntimeCurveKey> ReadCurveKeys(AetheriaRuntimeBehaviorValue value)
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

        private static AetheriaRuntimeCargoItemStatReference ReadStatReference(AetheriaRuntimeBehaviorValue value)
        {
            return new AetheriaRuntimeCargoItemStatReference(
                ChildString(value, 1),
                ChildString(value, 2));
        }

        private static T ReadEnum<T>(AetheriaRuntimeBehaviorValue value, T fallback) where T : struct
        {
            if (!string.IsNullOrWhiteSpace(value?.StringValue) && Enum.TryParse(value.StringValue, true, out T parsed))
                return parsed;

            return value != null && Enum.IsDefined(typeof(T), (int)value.NumberValue)
                ? (T)Enum.ToObject(typeof(T), (int)value.NumberValue)
                : fallback;
        }

        private static AetheriaRuntimeBehaviorValue ToBehaviorValue(AetheriaRuntimePerformanceStat stat)
        {
            return new AetheriaRuntimeBehaviorValue(
                "performance-stat",
                "",
                0,
                false,
                "",
                "",
                new[]
                {
                    Number(stat?.Min ?? 0),
                    Number(stat?.Max ?? 0),
                    Number(stat?.HeatExponentMultiplier ?? 0),
                    Number(stat?.DurabilityExponentMultiplier ?? 0),
                    Number(stat?.QualityExponent ?? 0),
                    StatRecipeValue(stat?.Recipe)
                },
                EmptyMapEntries());
        }

        private static AetheriaRuntimeBehaviorValue StatRecipeValue(AetheriaRuntimeStatRecipe recipe)
        {
            if (recipe == null)
                return EmptyValue("stat-recipe");

            return new AetheriaRuntimeBehaviorValue(
                "stat-recipe",
                "",
                0,
                false,
                "",
                "",
                new[]
                {
                    Number(recipe.BaseValue),
                    new AetheriaRuntimeBehaviorValue(
                        "stat-recipe-modifiers",
                        "",
                        0,
                        false,
                        "",
                        "",
                        (recipe.Modifiers ?? Array.Empty<AetheriaRuntimeStatRecipeModifier>())
                            .Select(StatRecipeModifierValue)
                            .ToArray(),
                        EmptyMapEntries())
                },
                EmptyMapEntries());
        }

        private static AetheriaRuntimeBehaviorValue StatRecipeModifierValue(AetheriaRuntimeStatRecipeModifier modifier)
        {
            if (modifier == null)
                return EmptyValue("stat-recipe-modifier");

            return new AetheriaRuntimeBehaviorValue(
                "stat-recipe-modifier",
                "",
                0,
                false,
                "",
                "",
                new[]
                {
                    TextValue(modifier.Condition),
                    TextValue(modifier.Operation),
                    Number(modifier.Amount),
                    CurveValue(modifier.CurveKeys),
                    BoolValue(modifier.Enabled)
                },
                EmptyMapEntries());
        }

        private static AetheriaRuntimeBehaviorValue CurveValue(IReadOnlyList<AetheriaRuntimeCurveKey> keys)
        {
            return new AetheriaRuntimeBehaviorValue(
                "curve",
                "",
                0,
                false,
                "",
                "",
                (keys ?? Array.Empty<AetheriaRuntimeCurveKey>())
                    .Select(key => new AetheriaRuntimeBehaviorValue(
                        "curve-key",
                        "",
                        0,
                        false,
                        "",
                        "",
                        new[]
                        {
                            Number(key.Time),
                            Number(key.Value),
                            Number(key.InTangent),
                            Number(key.OutTangent)
                        },
                        EmptyMapEntries()))
                    .ToArray(),
                EmptyMapEntries());
        }

        private static AetheriaRuntimeBehaviorValue Number(double value)
        {
            return new AetheriaRuntimeBehaviorValue("", "", value, false, "", "", Array.Empty<AetheriaRuntimeBehaviorValue>(), EmptyMapEntries());
        }

        private static AetheriaRuntimeBehaviorValue TextValue(string value)
        {
            return new AetheriaRuntimeBehaviorValue("", value ?? "", 0, false, "", "", Array.Empty<AetheriaRuntimeBehaviorValue>(), EmptyMapEntries());
        }

        private static AetheriaRuntimeBehaviorValue BoolValue(bool value)
        {
            return new AetheriaRuntimeBehaviorValue("", "", 0, value, "", "", Array.Empty<AetheriaRuntimeBehaviorValue>(), EmptyMapEntries());
        }

        private static AetheriaRuntimeBehaviorValue EmptyValue(string kind)
        {
            return new AetheriaRuntimeBehaviorValue(kind ?? "", "", 0, false, "", "", Array.Empty<AetheriaRuntimeBehaviorValue>(), EmptyMapEntries());
        }

        private static IReadOnlyList<AetheriaRuntimeBehaviorMapEntry> EmptyMapEntries()
        {
            return Array.Empty<AetheriaRuntimeBehaviorMapEntry>();
        }

        private static double ChildNumber(AetheriaRuntimeBehaviorValue value, int index)
        {
            return value != null && value.Children.Count > index ? value.Children[index].NumberValue : 0;
        }

        private static string ChildString(AetheriaRuntimeBehaviorValue value, int index)
        {
            return value != null && value.Children.Count > index ? value.Children[index].StringValue ?? "" : "";
        }

        private static AetheriaRuntimeBehaviorValue ChildValue(AetheriaRuntimeBehaviorValue value, int index)
        {
            return value != null && value.Children.Count > index ? value.Children[index] : null;
        }

        private static string FormatValue(
            double value,
            Func<float, string> formatValue)
        {
            return formatValue == null
                ? value.ToString("0.###", CultureInfo.InvariantCulture)
                : formatValue((float)value);
        }

        private static string FormatTemperature(
            double value,
            Func<float, string> formatTemperature)
        {
            return formatTemperature == null
                ? value.ToString("0.###", CultureInfo.InvariantCulture)
                : formatTemperature((float)value);
        }

        private static string FormatTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return "";

            return SplitCamelCase(typeName.StartsWith("I", StringComparison.Ordinal) && typeName.Length > 1
                ? typeName.Substring(1)
                : typeName);
        }

        private static string SplitCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            return string.Concat(value.Select((character, index) =>
                index > 0 && char.IsUpper(character) ? " " + character : character.ToString()));
        }

        private static string SafeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "empty";

            return new string(value
                .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
                .ToArray()).Trim('-');
        }

        private static AetheriaRuntimeSurfaceComponent Card(
            string id,
            string title,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "card", new[] { ("title", title ?? "") }, children);
        }

        private static AetheriaRuntimeSurfaceComponent Metric(string id, string label, string value, string valueRef = "")
        {
            var props = new List<(string Key, string Value)>
            {
                ("label", label ?? ""),
                ("value", value ?? "")
            };

            if (!string.IsNullOrWhiteSpace(valueRef))
                props.Add(AetheriaRuntimeSurfaceStateRefs.ValueRef(valueRef));

            return Node(id, "metric", props);
        }

        private static AetheriaRuntimeSurfaceComponent Text(string id, string value)
        {
            return Node(id, "text", new[] { ("value", value ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent Button(string id, string label, string command)
        {
            return Node(id, "control.button", new[] { ("label", label ?? ""), ("command", command ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent ButtonRow(
            string id,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "row", Array.Empty<(string Key, string Value)>(), children);
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
    }

    public enum AetheriaRuntimeCargoItemDetailsCommandKind
    {
        Unknown = 0,
        Close = 1
    }

    internal enum AetheriaRuntimeCargoItemStatModifierType
    {
        Constant = 0,
        Multiplier = 1
    }

    internal readonly struct AetheriaRuntimeCargoItemStatReference
    {
        public AetheriaRuntimeCargoItemStatReference(string target, string stat)
        {
            Target = target ?? "";
            Stat = stat ?? "";
        }

        public string Target { get; }
        public string Stat { get; }
    }

    public readonly struct AetheriaRuntimeCargoItemDetailsCommand
    {
        public AetheriaRuntimeCargoItemDetailsCommand(AetheriaRuntimeCargoItemDetailsCommandKind kind)
        {
            Kind = kind;
        }

        public AetheriaRuntimeCargoItemDetailsCommandKind Kind { get; }
    }

    public static class AetheriaRuntimeCargoItemDetailsSurfaceCommands
    {
        public static bool TryRead(
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeCargoItemDetailsCommand command)
        {
            command = default;
            if (request == null ||
                !string.Equals(request.SurfaceId, AetheriaRuntimeCargoItemDetailsSurfaceBuilder.SurfaceId, StringComparison.Ordinal))
                return false;

            if (string.Equals(request.Command, AetheriaRuntimeCargoItemDetailsSurfaceBuilder.Close, StringComparison.Ordinal))
            {
                command = new AetheriaRuntimeCargoItemDetailsCommand(AetheriaRuntimeCargoItemDetailsCommandKind.Close);
                return true;
            }

            return false;
        }
    }
}

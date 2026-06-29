using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Eve.Surface;

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeTradeItemMetric
    {
        public AetheriaRuntimeTradeItemMetric(string id, string label, string value)
        {
            Id = id ?? "";
            Label = label ?? "";
            Value = value ?? "";
        }

        public string Id { get; }
        public string Label { get; }
        public string Value { get; }
    }

    public sealed class AetheriaRuntimeTradeItemSection
    {
        public AetheriaRuntimeTradeItemSection(
            string id,
            string title,
            IReadOnlyList<AetheriaRuntimeTradeItemMetric> metrics)
        {
            Id = id ?? "";
            Title = title ?? "";
            Metrics = metrics ?? Array.Empty<AetheriaRuntimeTradeItemMetric>();
        }

        public string Id { get; }
        public string Title { get; }
        public IReadOnlyList<AetheriaRuntimeTradeItemMetric> Metrics { get; }
    }

    public sealed class AetheriaRuntimeTradeItemDetailsSurfaceState
    {
        public AetheriaRuntimeTradeItemDetailsSurfaceState(
            string itemName,
            string description,
            string manufacturer,
            string mass,
            int price,
            string durability,
            string thermalRange,
            IReadOnlyList<AetheriaRuntimeTradeItemSection> behaviorSections,
            string updatedAtUtc)
        {
            ItemName = itemName ?? "";
            Description = description ?? "";
            Manufacturer = manufacturer ?? "";
            Mass = mass ?? "";
            Price = price;
            Durability = durability ?? "";
            ThermalRange = thermalRange ?? "";
            BehaviorSections = behaviorSections ?? Array.Empty<AetheriaRuntimeTradeItemSection>();
            UpdatedAtUtc = updatedAtUtc ?? "";
        }

        public string ItemName { get; }
        public string Description { get; }
        public string Manufacturer { get; }
        public string Mass { get; }
        public int Price { get; }
        public string Durability { get; }
        public string ThermalRange { get; }
        public IReadOnlyList<AetheriaRuntimeTradeItemSection> BehaviorSections { get; }
        public string UpdatedAtUtc { get; }
        public bool HasEquipmentStatus => !string.IsNullOrWhiteSpace(Durability) ||
                                          !string.IsNullOrWhiteSpace(ThermalRange) ||
                                          BehaviorSections.Count > 0;
    }

    public static class AetheriaRuntimeTradeItemDetailsSurfaceBuilder
    {
        public const string SurfaceId = "aetheria.trade.item_details";
        public const string Close = "aetheria.trade.item_details.close";

        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeCatalogItem item,
            string manufacturer,
            Func<float, string> formatValue,
            Func<float, string> formatTemperature,
            DateTime updatedAtUtc = default(DateTime),
            long version = 1)
        {
            return Build(
                ComposeState(
                    item,
                    manufacturer,
                    formatValue,
                    formatTemperature,
                    updatedAtUtc),
                version);
        }

        private static AetheriaRuntimeTradeItemDetailsSurfaceState ComposeState(
            AetheriaRuntimeCatalogItem item,
            string manufacturer,
            Func<float, string> formatValue,
            Func<float, string> formatTemperature,
            DateTime updatedAtUtc = default(DateTime))
        {
            if (updatedAtUtc == default(DateTime))
                updatedAtUtc = DateTime.UtcNow;

            if (item == null)
            {
                return new AetheriaRuntimeTradeItemDetailsSurfaceState(
                    "",
                    "",
                    manufacturer ?? "",
                    "",
                    0,
                    "",
                    "",
                    Array.Empty<AetheriaRuntimeTradeItemSection>(),
                    updatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            }

            var durability = "";
            var thermalRange = "";
            var behaviorSections = Array.Empty<AetheriaRuntimeTradeItemSection>();
            if (!string.IsNullOrWhiteSpace(item.HardpointType))
            {
                durability = FormatValue(item.Durability, formatValue);
                thermalRange = FormatTemperatureRange(item, formatTemperature);
                behaviorSections = ProjectBehaviorSections(item, formatValue, formatTemperature).ToArray();
            }

            return new AetheriaRuntimeTradeItemDetailsSurfaceState(
                item.Name,
                item.Description ?? "",
                manufacturer ?? "",
                FormatValue(item.Mass, formatValue),
                item.Price,
                durability,
                thermalRange,
                behaviorSections,
                updatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        }

        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeTradeItemDetailsSurfaceState state,
            long version = 1)
        {
            state ??= new AetheriaRuntimeTradeItemDetailsSurfaceState(
                "",
                "",
                "",
                "",
                0,
                "",
                "",
                Array.Empty<AetheriaRuntimeTradeItemSection>(),
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
                        "The observing client supplies the selected market row; the shared runtime surface owns trade item inspection layout."),
                    Metric($"{SurfaceId}.manufacturer", "Manufacturer", state.Manufacturer),
                    Metric($"{SurfaceId}.mass", "Mass", state.Mass),
                    Metric($"{SurfaceId}.price", "Price", state.Price.ToString("N0")))
            };

            if (state.HasEquipmentStatus)
            {
                children.Add(Card(
                    $"{SurfaceId}.durability.card",
                    "Durability",
                    Metric($"{SurfaceId}.durability", "Max Durability", state.Durability),
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
                        .Select(metric => Metric(metric.Id, metric.Label, metric.Value))
                        .ToArray()));
            }

            children.Add(ButtonRow(
                $"{SurfaceId}.actions",
                Button($"{SurfaceId}.close", "Close", Close)));

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "trade.menu",
                title: "Trade Item Details",
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

        private static IEnumerable<AetheriaRuntimeTradeItemSection> ProjectBehaviorSections(
            AetheriaRuntimeCatalogItem item,
            Func<float, string> formatValue,
            Func<float, string> formatTemperature)
        {
            foreach (var behavior in item.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>())
            {
                if (string.Equals(behavior.Kind, AetheriaRuntimeBehaviorKinds.StatModifier, StringComparison.Ordinal))
                {
                    var statReference = ReadStatReference(FindTypedBehaviorField(behavior, 1)?.Value);
                    var modifier = ReadPerformanceStat(FindTypedBehaviorField(behavior, 2)?.Value);
                    var modifierType = ReadEnum(
                        FindTypedBehaviorField(behavior, 3)?.Value,
                        AetheriaRuntimeTradeItemStatModifierType.Constant);
                    yield return new AetheriaRuntimeTradeItemSection(
                        $"{SurfaceId}.behavior.{behavior.Kind}.stat_modifier",
                        "Stat Modifier",
                        new[]
                        {
                            new AetheriaRuntimeTradeItemMetric(
                                $"{SurfaceId}.behavior.{behavior.Kind}.target",
                                $"{SplitCamelCase(statReference.Target)}:{SplitCamelCase(statReference.Stat)}",
                                $"{(modifierType == AetheriaRuntimeTradeItemStatModifierType.Constant ? "+" : "x")}{FormatValue(modifier.Min, formatValue)}")
                        });
                    continue;
                }

                var metadata = AetheriaRuntimeBehaviorMetadataCatalog.Get(behavior.Kind);
                if (metadata == null)
                    continue;

                var fields = metadata.DisplayFields
                    .Select(field => ProjectBehaviorMetric(behavior, field, formatValue, formatTemperature))
                    .Where(metric => metric != null)
                    .ToArray();

                if (fields.Length == 0)
                    continue;

                yield return new AetheriaRuntimeTradeItemSection(
                    $"{SurfaceId}.behavior.{behavior.Kind}",
                    FormatTypeName(behavior.Kind),
                    fields);
            }
        }

        private static AetheriaRuntimeTradeItemMetric ProjectBehaviorMetric(
            AetheriaRuntimeBehaviorPayload behavior,
            AetheriaRuntimeBehaviorFieldMetadata field,
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
                    value = FormatValue(
                        ReadPerformanceStat(payloadField.Value).Min,
                        formatValue);
                    break;
                default:
                    return null;
            }

            return new AetheriaRuntimeTradeItemMetric(
                $"{SurfaceId}.behavior.{behavior.Kind}.{field.Key}",
                SplitCamelCase(field.Name),
                value);
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

        private static AetheriaRuntimeTradeItemStatReference ReadStatReference(AetheriaRuntimeBehaviorValue value)
        {
            return new AetheriaRuntimeTradeItemStatReference(
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

        private static AetheriaRuntimeSurfaceComponent Metric(string id, string label, string value)
        {
            return Node(id, "metric", new[] { ("label", label ?? ""), ("value", value ?? "") });
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
                id ?? "",
                kind ?? "",
                (props ?? Array.Empty<(string Key, string Value)>())
                    .ToDictionary(prop => prop.Key, prop => prop.Value ?? "", StringComparer.Ordinal),
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>());
        }
    }

    public enum AetheriaRuntimeTradeItemDetailsCommandKind
    {
        Unknown = 0,
        Close = 1
    }

    internal enum AetheriaRuntimeTradeItemStatModifierType
    {
        Constant = 0,
        Multiplier = 1
    }

    internal readonly struct AetheriaRuntimeTradeItemStatReference
    {
        public AetheriaRuntimeTradeItemStatReference(string target, string stat)
        {
            Target = target ?? "";
            Stat = stat ?? "";
        }

        public string Target { get; }
        public string Stat { get; }
    }

    public readonly struct AetheriaRuntimeTradeItemDetailsCommand
    {
        public AetheriaRuntimeTradeItemDetailsCommand(AetheriaRuntimeTradeItemDetailsCommandKind kind)
        {
            Kind = kind;
        }

        public AetheriaRuntimeTradeItemDetailsCommandKind Kind { get; }
    }

    public static class AetheriaRuntimeTradeItemDetailsSurfaceCommands
    {
        public static bool TryRead(
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeTradeItemDetailsCommand command)
        {
            command = default;
            if (request == null ||
                !string.Equals(request.SurfaceId, AetheriaRuntimeTradeItemDetailsSurfaceBuilder.SurfaceId, StringComparison.Ordinal))
                return false;

            if (string.Equals(request.Operation?.OperationId, AetheriaRuntimeTradeItemDetailsSurfaceBuilder.Close, StringComparison.Ordinal))
            {
                command = new AetheriaRuntimeTradeItemDetailsCommand(AetheriaRuntimeTradeItemDetailsCommandKind.Close);
                return true;
            }

            return false;
        }
    }
}

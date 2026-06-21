using System;
using System.Collections.Generic;
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

            if (string.Equals(request.Command, AetheriaRuntimeTradeItemDetailsSurfaceBuilder.Close, StringComparison.Ordinal))
            {
                command = new AetheriaRuntimeTradeItemDetailsCommand(AetheriaRuntimeTradeItemDetailsCommandKind.Close);
                return true;
            }

            return false;
        }
    }
}

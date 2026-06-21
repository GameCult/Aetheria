using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Eve.Surface;

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeCargoItemMetric
    {
        public AetheriaRuntimeCargoItemMetric(string id, string label, string value)
        {
            Id = id ?? "";
            Label = label ?? "";
            Value = value ?? "";
        }

        public string Id { get; }
        public string Label { get; }
        public string Value { get; }
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

    public static class AetheriaRuntimeCargoItemDetailsSurfaceBuilder
    {
        public const string SurfaceId = "aetheria.inventory.cargo_item_details";
        public const string Close = "aetheria.inventory.cargo_item_details.close";

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
                        "Unity supplies the selected item; the shared runtime surface owns cargo-item inspection layout."),
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
                        .Select(metric => Metric(metric.Id, metric.Label, metric.Value))
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
                    new AetheriaRuntimeSurfaceCommandTemplate(Close, "Close", "unity-uitoolkit")
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

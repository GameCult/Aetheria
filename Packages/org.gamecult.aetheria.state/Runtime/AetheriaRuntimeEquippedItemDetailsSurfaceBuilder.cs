using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Eve.Surface;

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeEquippedItemMetric
    {
        public AetheriaRuntimeEquippedItemMetric(string id, string label, string value)
        {
            Id = id ?? "";
            Label = label ?? "";
            Value = value ?? "";
        }

        public string Id { get; }
        public string Label { get; }
        public string Value { get; }
    }

    public sealed class AetheriaRuntimeEquippedItemSection
    {
        public AetheriaRuntimeEquippedItemSection(
            string id,
            string title,
            IReadOnlyList<AetheriaRuntimeEquippedItemMetric> metrics)
        {
            Id = id ?? "";
            Title = title ?? "";
            Metrics = metrics ?? Array.Empty<AetheriaRuntimeEquippedItemMetric>();
        }

        public string Id { get; }
        public string Title { get; }
        public IReadOnlyList<AetheriaRuntimeEquippedItemMetric> Metrics { get; }
    }

    public sealed class AetheriaRuntimeEquippedItemControl
    {
        public AetheriaRuntimeEquippedItemControl(
            string id,
            string label,
            string command,
            IReadOnlyDictionary<string, string> payload)
        {
            Id = id ?? "";
            Label = label ?? "";
            Command = command ?? "";
            Payload = payload ?? EmptyPayload;
        }

        public string Id { get; }
        public string Label { get; }
        public string Command { get; }
        public IReadOnlyDictionary<string, string> Payload { get; }

        private static readonly IReadOnlyDictionary<string, string> EmptyPayload =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class AetheriaRuntimeEquippedItemTemperatureControl
    {
        public AetheriaRuntimeEquippedItemTemperatureControl(
            string id,
            string label,
            string value,
            IReadOnlyDictionary<string, string> payload)
        {
            Id = id ?? "";
            Label = label ?? "";
            Value = value ?? "";
            Payload = payload ?? EmptyPayload;
        }

        public string Id { get; }
        public string Label { get; }
        public string Value { get; }
        public IReadOnlyDictionary<string, string> Payload { get; }

        private static readonly IReadOnlyDictionary<string, string> EmptyPayload =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class AetheriaRuntimeEquippedItemActionBarSlot
    {
        public AetheriaRuntimeEquippedItemActionBarSlot(
            string id,
            string title,
            string currentBinding,
            IReadOnlyList<AetheriaRuntimeEquippedItemControl> controls)
        {
            Id = id ?? "";
            Title = title ?? "";
            CurrentBinding = currentBinding ?? "";
            Controls = controls ?? Array.Empty<AetheriaRuntimeEquippedItemControl>();
        }

        public string Id { get; }
        public string Title { get; }
        public string CurrentBinding { get; }
        public IReadOnlyList<AetheriaRuntimeEquippedItemControl> Controls { get; }
    }

    public sealed class AetheriaRuntimeEquippedItemDetailsSurfaceState
    {
        public AetheriaRuntimeEquippedItemDetailsSurfaceState(
            string itemName,
            string description,
            string manufacturer,
            string mass,
            string durability,
            string temperature,
            string thermalRange,
            string overrideShutdown,
            string overrideShutdownLabel,
            IReadOnlyList<AetheriaRuntimeEquippedItemTemperatureControl> temperatureControls,
            IReadOnlyList<AetheriaRuntimeEquippedItemSection> behaviorSections,
            IReadOnlyList<AetheriaRuntimeEquippedItemControl> weaponGroupControls,
            IReadOnlyList<AetheriaRuntimeEquippedItemActionBarSlot> actionBarSlots,
            string updatedAtUtc)
        {
            ItemName = itemName ?? "";
            Description = description ?? "";
            Manufacturer = manufacturer ?? "";
            Mass = mass ?? "";
            Durability = durability ?? "";
            Temperature = temperature ?? "";
            ThermalRange = thermalRange ?? "";
            OverrideShutdown = overrideShutdown ?? "";
            OverrideShutdownLabel = overrideShutdownLabel ?? "";
            TemperatureControls = temperatureControls ?? Array.Empty<AetheriaRuntimeEquippedItemTemperatureControl>();
            BehaviorSections = behaviorSections ?? Array.Empty<AetheriaRuntimeEquippedItemSection>();
            WeaponGroupControls = weaponGroupControls ?? Array.Empty<AetheriaRuntimeEquippedItemControl>();
            ActionBarSlots = actionBarSlots ?? Array.Empty<AetheriaRuntimeEquippedItemActionBarSlot>();
            UpdatedAtUtc = updatedAtUtc ?? "";
        }

        public string ItemName { get; }
        public string Description { get; }
        public string Manufacturer { get; }
        public string Mass { get; }
        public string Durability { get; }
        public string Temperature { get; }
        public string ThermalRange { get; }
        public string OverrideShutdown { get; }
        public string OverrideShutdownLabel { get; }
        public IReadOnlyList<AetheriaRuntimeEquippedItemTemperatureControl> TemperatureControls { get; }
        public IReadOnlyList<AetheriaRuntimeEquippedItemSection> BehaviorSections { get; }
        public IReadOnlyList<AetheriaRuntimeEquippedItemControl> WeaponGroupControls { get; }
        public IReadOnlyList<AetheriaRuntimeEquippedItemActionBarSlot> ActionBarSlots { get; }
        public string UpdatedAtUtc { get; }
        public bool HasWeaponControls => WeaponGroupControls.Count > 0;
    }

    public static class AetheriaRuntimeEquippedItemDetailsSurfaceBuilder
    {
        public const string SurfaceId = "aetheria.inventory.equipped_item_details";
        public const string Close = "aetheria.inventory.equipped_item_details.close";
        public const string ToggleOverrideShutdown = "aetheria.inventory.equipped_item_details.override_shutdown.toggle";
        public const string SetTargetTemperature = "aetheria.inventory.equipped_item_details.target_temperature.set";
        public const string ToggleWeaponGroup = "aetheria.inventory.equipped_item_details.weapon_group.toggle";
        public const string BindWeaponGroup = "aetheria.inventory.equipped_item_details.weapon_group.bind";
        public const string ClearActionBarBinding = "aetheria.inventory.equipped_item_details.action_bar.clear";

        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeEquippedItemDetailsSurfaceState state,
            long version = 1)
        {
            state ??= new AetheriaRuntimeEquippedItemDetailsSurfaceState(
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                Array.Empty<AetheriaRuntimeEquippedItemTemperatureControl>(),
                Array.Empty<AetheriaRuntimeEquippedItemSection>(),
                Array.Empty<AetheriaRuntimeEquippedItemControl>(),
                Array.Empty<AetheriaRuntimeEquippedItemActionBarSlot>(),
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
                        "The observing client projects the selected equipment; the shared runtime surface owns equipped-item inspection layout and commands."),
                    Metric($"{SurfaceId}.manufacturer", "Manufacturer", state.Manufacturer),
                    Metric($"{SurfaceId}.mass", "Mass", state.Mass)),
                Card(
                    $"{SurfaceId}.status.card",
                    "Status",
                    Metric($"{SurfaceId}.durability", "Durability", state.Durability),
                    Metric($"{SurfaceId}.temperature", "Temperature", state.Temperature),
                    Metric($"{SurfaceId}.thermal_range", "Thermal Range", state.ThermalRange))
            };

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

            children.Add(BuildControlsCard(state));

            if (state.HasWeaponControls)
            {
                children.Add(Card(
                    $"{SurfaceId}.weapon_groups.card",
                    "Weapon Groups",
                    Text(
                        $"{SurfaceId}.weapon_groups.note",
                        "Toggle membership directly; action-bar group binding is handled below."),
                    ButtonRow(
                        $"{SurfaceId}.weapon_groups.actions",
                        state.WeaponGroupControls.Select(Button).ToArray())));

                children.AddRange(state.ActionBarSlots.Select(BuildActionBarCard));
            }

            children.Add(ButtonRow(
                $"{SurfaceId}.actions",
                Button($"{SurfaceId}.close", "Close", Close)));

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "inventory.menu",
                title: "Inventory Equipped Item Details",
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
                    new AetheriaRuntimeSurfaceCommandTemplate(Close, "Close", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(ToggleOverrideShutdown, "Toggle Override Shutdown", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(SetTargetTemperature, "Set Target Temperature", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(ToggleWeaponGroup, "Toggle Weapon Group", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(BindWeaponGroup, "Bind Weapon Group", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport),
                    new AetheriaRuntimeSurfaceCommandTemplate(ClearActionBarBinding, "Clear Action Bar Binding", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport)
                });
        }

        public static IReadOnlyDictionary<string, string> Payload(params (string Key, string Value)[] values)
        {
            return (values ?? Array.Empty<(string Key, string Value)>())
                .ToDictionary(value => value.Key ?? "", value => value.Value ?? "", StringComparer.Ordinal);
        }

        private static AetheriaRuntimeSurfaceComponent BuildControlsCard(
            AetheriaRuntimeEquippedItemDetailsSurfaceState state)
        {
            var children = new List<AetheriaRuntimeSurfaceComponent>
            {
                Metric(
                    $"{SurfaceId}.controls.override_shutdown.metric",
                    "Override Shutdown",
                    state.OverrideShutdown),
                ButtonRow(
                    $"{SurfaceId}.controls.override_shutdown.actions",
                    Button(
                        $"{SurfaceId}.controls.override_shutdown.toggle",
                        string.IsNullOrWhiteSpace(state.OverrideShutdownLabel)
                            ? "Toggle Override"
                            : state.OverrideShutdownLabel,
                        ToggleOverrideShutdown))
            };

            children.AddRange(state.TemperatureControls.Select(control => Metric(
                $"{control.Id}.metric",
                control.Label,
                control.Value)));
            children.AddRange(state.TemperatureControls.Select(control => TextField(
                control.Id,
                control.Label,
                SetTargetTemperature,
                control.Value,
                control.Payload)));

            return Card($"{SurfaceId}.controls.card", "Controls", children.ToArray());
        }

        private static AetheriaRuntimeSurfaceComponent BuildActionBarCard(
            AetheriaRuntimeEquippedItemActionBarSlot slot)
        {
            return Card(
                slot.Id,
                slot.Title,
                Metric($"{slot.Id}.binding", "Current", slot.CurrentBinding),
                ButtonRow($"{slot.Id}.actions", slot.Controls.Select(Button).ToArray()));
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

        private static AetheriaRuntimeSurfaceComponent Button(AetheriaRuntimeEquippedItemControl control)
        {
            return CommandButton(control.Id, control.Label, control.Command, control.Payload);
        }

        private static AetheriaRuntimeSurfaceComponent Button(string id, string label, string command)
        {
            return Node(id, "control.button", new[] { ("label", label ?? ""), ("command", command ?? "") });
        }

        private static AetheriaRuntimeSurfaceComponent CommandButton(
            string id,
            string label,
            string command,
            IReadOnlyDictionary<string, string> payload)
        {
            var props = new List<(string Key, string Value)>
            {
                ("label", label ?? ""),
                ("command", command ?? "")
            };

            if (payload != null)
            {
                props.AddRange(payload.Select(entry => (entry.Key, entry.Value)));
            }

            return Node(id, "control.button", props);
        }

        private static AetheriaRuntimeSurfaceComponent TextField(
            string id,
            string label,
            string command,
            string value,
            IReadOnlyDictionary<string, string> payload)
        {
            var props = new List<(string Key, string Value)>
            {
                ("label", label ?? ""),
                ("command", command ?? ""),
                ("value", value ?? "")
            };

            if (payload != null)
            {
                props.AddRange(payload.Select(entry => (entry.Key, entry.Value)));
            }

            return Node(id, "control.text", props);
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

    public enum AetheriaRuntimeEquippedItemDetailsCommandKind
    {
        Unknown = 0,
        Close = 1,
        ToggleOverrideShutdown = 2,
        SetTargetTemperature = 3,
        ToggleWeaponGroup = 4,
        BindWeaponGroup = 5,
        ClearActionBarBinding = 6
    }

    public readonly struct AetheriaRuntimeEquippedItemDetailsCommand
    {
        public AetheriaRuntimeEquippedItemDetailsCommand(
            AetheriaRuntimeEquippedItemDetailsCommandKind kind,
            int behaviorIndex = -1,
            float targetTemperature = 0f,
            int groupIndex = -1,
            int slotIndex = -1)
        {
            Kind = kind;
            BehaviorIndex = behaviorIndex;
            TargetTemperature = targetTemperature;
            GroupIndex = groupIndex;
            SlotIndex = slotIndex;
        }

        public AetheriaRuntimeEquippedItemDetailsCommandKind Kind { get; }
        public int BehaviorIndex { get; }
        public float TargetTemperature { get; }
        public int GroupIndex { get; }
        public int SlotIndex { get; }
    }

    public static class AetheriaRuntimeEquippedItemDetailsSurfaceCommands
    {
        public static bool TryRead(
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeEquippedItemDetailsCommand command)
        {
            command = default;
            if (request == null ||
                !string.Equals(request.SurfaceId, AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.SurfaceId, StringComparison.Ordinal))
                return false;

            switch (request.Command ?? "")
            {
                case AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.Close:
                    command = new AetheriaRuntimeEquippedItemDetailsCommand(
                        AetheriaRuntimeEquippedItemDetailsCommandKind.Close);
                    return true;
                case AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.ToggleOverrideShutdown:
                    command = new AetheriaRuntimeEquippedItemDetailsCommand(
                        AetheriaRuntimeEquippedItemDetailsCommandKind.ToggleOverrideShutdown);
                    return true;
                case AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.SetTargetTemperature:
                    command = new AetheriaRuntimeEquippedItemDetailsCommand(
                        AetheriaRuntimeEquippedItemDetailsCommandKind.SetTargetTemperature,
                        behaviorIndex: ReadInt(request, "behaviorIndex", -1),
                        targetTemperature: ReadFloat(request, "value", 0f));
                    return true;
                case AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.ToggleWeaponGroup:
                    command = new AetheriaRuntimeEquippedItemDetailsCommand(
                        AetheriaRuntimeEquippedItemDetailsCommandKind.ToggleWeaponGroup,
                        groupIndex: ReadInt(request, "group", -1));
                    return true;
                case AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.BindWeaponGroup:
                    command = new AetheriaRuntimeEquippedItemDetailsCommand(
                        AetheriaRuntimeEquippedItemDetailsCommandKind.BindWeaponGroup,
                        groupIndex: ReadInt(request, "group", -1),
                        slotIndex: ReadInt(request, "slot", -1));
                    return true;
                case AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.ClearActionBarBinding:
                    command = new AetheriaRuntimeEquippedItemDetailsCommand(
                        AetheriaRuntimeEquippedItemDetailsCommandKind.ClearActionBarBinding,
                        slotIndex: ReadInt(request, "slot", -1));
                    return true;
                default:
                    return false;
            }
        }

        private static int ReadInt(EveSurfaceCommandRequest request, string key, int defaultValue)
        {
            return request.Payload != null &&
                   request.Payload.TryGetValue(key, out var raw) &&
                   int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : defaultValue;
        }

        private static float ReadFloat(EveSurfaceCommandRequest request, string key, float defaultValue)
        {
            return request.Payload != null &&
                   request.Payload.TryGetValue(key, out var raw) &&
                   float.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value)
                ? value
                : defaultValue;
        }
    }
}

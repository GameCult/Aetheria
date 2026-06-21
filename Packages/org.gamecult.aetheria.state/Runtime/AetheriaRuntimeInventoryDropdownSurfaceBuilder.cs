using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Eve.Surface;

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeInventoryDropdownOption
    {
        public AetheriaRuntimeInventoryDropdownOption(string id, string label, string command)
        {
            Id = id ?? "";
            Label = label ?? "";
            Command = command ?? "";
        }

        public string Id { get; }
        public string Label { get; }
        public string Command { get; }
        public bool IsCommand => !string.IsNullOrWhiteSpace(Command);
    }

    public sealed class AetheriaRuntimeInventoryDropdownGroup
    {
        public AetheriaRuntimeInventoryDropdownGroup(
            string id,
            string title,
            IReadOnlyList<AetheriaRuntimeInventoryDropdownOption> options)
        {
            Id = id ?? "";
            Title = title ?? "";
            Options = options ?? Array.Empty<AetheriaRuntimeInventoryDropdownOption>();
        }

        public string Id { get; }
        public string Title { get; }
        public IReadOnlyList<AetheriaRuntimeInventoryDropdownOption> Options { get; }
    }

    public sealed class AetheriaRuntimeInventoryDropdownSurfaceState
    {
        public AetheriaRuntimeInventoryDropdownSurfaceState(
            string currentView,
            IReadOnlyList<AetheriaRuntimeInventoryDropdownGroup> groups,
            string updatedAtUtc)
        {
            CurrentView = currentView ?? "";
            Groups = groups ?? Array.Empty<AetheriaRuntimeInventoryDropdownGroup>();
            UpdatedAtUtc = updatedAtUtc ?? "";
        }

        public string CurrentView { get; }
        public IReadOnlyList<AetheriaRuntimeInventoryDropdownGroup> Groups { get; }
        public string UpdatedAtUtc { get; }
    }

    public static class AetheriaRuntimeInventoryDropdownSurfaceBuilder
    {
        public const string SurfaceId = "aetheria.inventory.panel.dropdown";
        public const string Close = "aetheria.inventory.panel.dropdown.close";
        public const string SaveLoadout = "aetheria.inventory.panel.dropdown.save_loadout";
        public const string DockingBay = "aetheria.inventory.panel.dropdown.docking_bay";

        public static string EntityEquipmentCommand(int entityIndex)
        {
            return $"{SurfaceId}.entity_{entityIndex}.equipment";
        }

        public static string EntityBayCommand(int entityIndex, int bayIndex)
        {
            return $"{SurfaceId}.entity_{entityIndex}.bay_{bayIndex}";
        }

        public static string EntityCommand(int entityIndex)
        {
            return $"{SurfaceId}.entity_{entityIndex}";
        }

        public static string LoadoutCommand(int templateIndex)
        {
            return $"{SurfaceId}.loadout_{templateIndex}";
        }

        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimeInventoryDropdownSurfaceState state,
            long version = 1)
        {
            state ??= new AetheriaRuntimeInventoryDropdownSurfaceState(
                "",
                Array.Empty<AetheriaRuntimeInventoryDropdownGroup>(),
                "");

            var children = new List<AetheriaRuntimeSurfaceComponent>
            {
                Card(
                    $"{SurfaceId}.summary",
                    "Inventory Actions",
                    Metric(
                        $"{SurfaceId}.current",
                        "Current View",
                        string.IsNullOrWhiteSpace(state.CurrentView) ? "None" : state.CurrentView),
                    Text(
                        $"{SurfaceId}.note",
                        "Unity projects available inventory navigation; the shared runtime surface owns the dropdown contract."))
            };

            foreach (var group in state.Groups)
            {
                if (group?.Options == null || group.Options.Count == 0)
                    continue;

                children.Add(Card(
                    string.IsNullOrWhiteSpace(group.Id) ? $"{SurfaceId}.group.{SafeId(group.Title)}" : group.Id,
                    group.Title,
                    ButtonColumn(
                        $"{(string.IsNullOrWhiteSpace(group.Id) ? $"{SurfaceId}.group.{SafeId(group.Title)}" : group.Id)}.options",
                        group.Options.Select(ToOptionComponent).ToArray())));
            }

            children.Add(ButtonRow(
                $"{SurfaceId}.actions",
                Button($"{SurfaceId}.close", "Close", Close)));

            var commands = state.Groups
                .Where(group => group?.Options != null)
                .SelectMany(group => group.Options)
                .Where(option => option?.IsCommand == true)
                .Select(option => new AetheriaRuntimeSurfaceCommandTemplate(option.Command, option.Label, AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport))
                .Append(new AetheriaRuntimeSurfaceCommandTemplate(Close, "Close", AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport))
                .ToArray();

            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "inventory.panel",
                title: "Inventory Dropdown",
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
                commands: commands);
        }

        private static AetheriaRuntimeSurfaceComponent ToOptionComponent(AetheriaRuntimeInventoryDropdownOption option)
        {
            if (option.IsCommand)
                return Button(option.Id, option.Label, option.Command);

            return Text(option.Id, option.Label);
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

        private static AetheriaRuntimeSurfaceComponent ButtonColumn(
            string id,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "column", Array.Empty<(string Key, string Value)>(), children);
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

    public enum AetheriaRuntimeInventoryDropdownCommandKind
    {
        Unknown = 0,
        Close = 1,
        Select = 2
    }

    public readonly struct AetheriaRuntimeInventoryDropdownCommand
    {
        public AetheriaRuntimeInventoryDropdownCommand(
            AetheriaRuntimeInventoryDropdownCommandKind kind,
            string command)
        {
            Kind = kind;
            Command = command ?? "";
        }

        public AetheriaRuntimeInventoryDropdownCommandKind Kind { get; }
        public string Command { get; }
    }

    public static class AetheriaRuntimeInventoryDropdownSurfaceCommands
    {
        public static bool TryRead(
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeInventoryDropdownCommand command)
        {
            command = default;
            if (request == null ||
                !string.Equals(request.SurfaceId, AetheriaRuntimeInventoryDropdownSurfaceBuilder.SurfaceId, StringComparison.Ordinal))
                return false;

            var commandText = request.Command ?? "";
            if (string.Equals(commandText, AetheriaRuntimeInventoryDropdownSurfaceBuilder.Close, StringComparison.Ordinal))
            {
                command = new AetheriaRuntimeInventoryDropdownCommand(
                    AetheriaRuntimeInventoryDropdownCommandKind.Close,
                    commandText);
                return true;
            }

            if (string.IsNullOrWhiteSpace(commandText))
                return false;

            command = new AetheriaRuntimeInventoryDropdownCommand(
                AetheriaRuntimeInventoryDropdownCommandKind.Select,
                commandText);
            return true;
        }
    }
}

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

    public sealed class AetheriaRuntimeInventoryDropdownEntityOption
    {
        public AetheriaRuntimeInventoryDropdownEntityOption(
            int entityIndex,
            string entityKey,
            string name,
            bool isDisplayed,
            IReadOnlyList<AetheriaRuntimeInventoryDropdownBayOption> bays)
        {
            EntityIndex = entityIndex;
            EntityKey = entityKey ?? "";
            Name = name ?? "";
            IsDisplayed = isDisplayed;
            Bays = bays ?? Array.Empty<AetheriaRuntimeInventoryDropdownBayOption>();
        }

        public int EntityIndex { get; }
        public string EntityKey { get; }
        public string Name { get; }
        public bool IsDisplayed { get; }
        public IReadOnlyList<AetheriaRuntimeInventoryDropdownBayOption> Bays { get; }
    }

    public sealed class AetheriaRuntimeInventoryDropdownBayOption
    {
        public AetheriaRuntimeInventoryDropdownBayOption(int bayIndex, string label, bool isDisplayed)
        {
            BayIndex = bayIndex;
            Label = label ?? "";
            IsDisplayed = isDisplayed;
        }

        public int BayIndex { get; }
        public string Label { get; }
        public bool IsDisplayed { get; }
    }

    public sealed class AetheriaRuntimeInventoryDropdownLoadoutOption
    {
        public AetheriaRuntimeInventoryDropdownLoadoutOption(
            int templateIndex,
            string name,
            string priceLabel,
            bool canRestore)
        {
            TemplateIndex = templateIndex;
            Name = name ?? "";
            PriceLabel = priceLabel ?? "";
            CanRestore = canRestore;
        }

        public int TemplateIndex { get; }
        public string Name { get; }
        public string PriceLabel { get; }
        public bool CanRestore { get; }
    }

    public enum AetheriaRuntimeInventoryDropdownSelectionKind
    {
        Unknown = 0,
        EntityEquipment = 1,
        EntityBay = 2,
        Entity = 3,
        DockingBay = 4,
        SaveLoadout = 5,
        Loadout = 6
    }

    public readonly struct AetheriaRuntimeInventoryDropdownSelection
    {
        public AetheriaRuntimeInventoryDropdownSelection(
            AetheriaRuntimeInventoryDropdownSelectionKind kind,
            string command,
            string entityKey = "",
            int entityIndex = -1,
            int bayIndex = -1,
            int templateIndex = -1)
        {
            Kind = kind;
            Command = command ?? "";
            EntityKey = entityKey ?? "";
            EntityIndex = entityIndex;
            BayIndex = bayIndex;
            TemplateIndex = templateIndex;
        }

        public AetheriaRuntimeInventoryDropdownSelectionKind Kind { get; }
        public string Command { get; }
        public string EntityKey { get; }
        public int EntityIndex { get; }
        public int BayIndex { get; }
        public int TemplateIndex { get; }
    }

    public sealed class AetheriaRuntimeInventoryDropdownSurfaceModel
    {
        public AetheriaRuntimeInventoryDropdownSurfaceModel(
            AetheriaRuntimeInventoryDropdownSurfaceState state,
            IReadOnlyDictionary<string, AetheriaRuntimeInventoryDropdownSelection> selections)
        {
            State = state ?? new AetheriaRuntimeInventoryDropdownSurfaceState(
                "",
                Array.Empty<AetheriaRuntimeInventoryDropdownGroup>(),
                "");
            Selections = selections ?? new Dictionary<string, AetheriaRuntimeInventoryDropdownSelection>(StringComparer.Ordinal);
        }

        public AetheriaRuntimeInventoryDropdownSurfaceState State { get; }
        public IReadOnlyDictionary<string, AetheriaRuntimeInventoryDropdownSelection> Selections { get; }

        public bool TryResolve(
            string command,
            out AetheriaRuntimeInventoryDropdownSelection selection)
        {
            return Selections.TryGetValue(command ?? "", out selection);
        }
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

        public static AetheriaRuntimeInventoryDropdownSurfaceModel Compose(
            string currentView,
            IEnumerable<AetheriaRuntimeInventoryDropdownEntityOption> entities,
            bool hasDockingBay,
            string dockingBayLabel,
            bool dockingBayDisplayed,
            bool canSaveLoadout,
            IEnumerable<AetheriaRuntimeInventoryDropdownLoadoutOption> loadouts,
            string updatedAtUtc)
        {
            var groups = new List<AetheriaRuntimeInventoryDropdownGroup>();
            var selections = new Dictionary<string, AetheriaRuntimeInventoryDropdownSelection>(StringComparer.Ordinal);

            foreach (var entity in entities ?? Array.Empty<AetheriaRuntimeInventoryDropdownEntityOption>())
            {
                if (entity == null)
                    continue;

                var options = new List<AetheriaRuntimeInventoryDropdownOption>();
                var equipmentCommand = EntityEquipmentCommand(entity.EntityIndex);

                if (entity.Bays.Count > 0 && !entity.IsDisplayed)
                {
                    options.Add(new AetheriaRuntimeInventoryDropdownOption(
                        $"{SurfaceId}.entity_{entity.EntityIndex}.equipment",
                        "Equipment",
                        equipmentCommand));
                    selections[equipmentCommand] = new AetheriaRuntimeInventoryDropdownSelection(
                        AetheriaRuntimeInventoryDropdownSelectionKind.EntityEquipment,
                        equipmentCommand,
                        entityKey: entity.EntityKey,
                        entityIndex: entity.EntityIndex);
                }

                foreach (var bay in entity.Bays)
                {
                    if (bay == null || bay.IsDisplayed)
                        continue;

                    var bayCommand = EntityBayCommand(entity.EntityIndex, bay.BayIndex);
                    options.Add(new AetheriaRuntimeInventoryDropdownOption(
                        $"{SurfaceId}.entity_{entity.EntityIndex}.bay_{bay.BayIndex}",
                        string.IsNullOrWhiteSpace(bay.Label) ? $"Bay {bay.BayIndex + 1}" : bay.Label,
                        bayCommand));
                    selections[bayCommand] = new AetheriaRuntimeInventoryDropdownSelection(
                        AetheriaRuntimeInventoryDropdownSelectionKind.EntityBay,
                        bayCommand,
                        entityKey: entity.EntityKey,
                        entityIndex: entity.EntityIndex,
                        bayIndex: bay.BayIndex);
                }

                if (entity.Bays.Count == 0 && !entity.IsDisplayed)
                {
                    var entityCommand = EntityCommand(entity.EntityIndex);
                    options.Add(new AetheriaRuntimeInventoryDropdownOption(
                        $"{SurfaceId}.entity_{entity.EntityIndex}.select",
                        entity.Name,
                        entityCommand));
                    selections[entityCommand] = new AetheriaRuntimeInventoryDropdownSelection(
                        AetheriaRuntimeInventoryDropdownSelectionKind.Entity,
                        entityCommand,
                        entityKey: entity.EntityKey,
                        entityIndex: entity.EntityIndex);
                }

                if (options.Count > 0)
                {
                    groups.Add(new AetheriaRuntimeInventoryDropdownGroup(
                        $"{SurfaceId}.entity_{entity.EntityIndex}.card",
                        entity.Name,
                        options));
                }
            }

            if (hasDockingBay && !dockingBayDisplayed)
            {
                groups.Add(new AetheriaRuntimeInventoryDropdownGroup(
                    $"{SurfaceId}.dockingBay.card",
                    "Docking Bay",
                    new[]
                    {
                        new AetheriaRuntimeInventoryDropdownOption(
                            $"{SurfaceId}.dockingBay.select",
                            dockingBayLabel,
                            DockingBay)
                    }));
                selections[DockingBay] = new AetheriaRuntimeInventoryDropdownSelection(
                    AetheriaRuntimeInventoryDropdownSelectionKind.DockingBay,
                    DockingBay);
            }

            var loadoutOptions = new List<AetheriaRuntimeInventoryDropdownOption>();
            if (canSaveLoadout)
            {
                loadoutOptions.Add(new AetheriaRuntimeInventoryDropdownOption(
                    $"{SurfaceId}.loadouts.save",
                    "Save Loadout",
                    SaveLoadout));
                selections[SaveLoadout] = new AetheriaRuntimeInventoryDropdownSelection(
                    AetheriaRuntimeInventoryDropdownSelectionKind.SaveLoadout,
                    SaveLoadout);
            }

            foreach (var loadout in loadouts ?? Array.Empty<AetheriaRuntimeInventoryDropdownLoadoutOption>())
            {
                if (loadout == null)
                    continue;

                var loadoutCommand = LoadoutCommand(loadout.TemplateIndex);
                var priceSuffix = string.IsNullOrWhiteSpace(loadout.PriceLabel)
                    ? "unavailable"
                    : loadout.PriceLabel;
                loadoutOptions.Add(new AetheriaRuntimeInventoryDropdownOption(
                    $"{SurfaceId}.loadouts.{(loadout.CanRestore ? "restore" : "locked")}_{loadout.TemplateIndex}",
                    $"{loadout.Name} - {priceSuffix}{(loadout.CanRestore ? "" : " (unavailable)")}",
                    loadout.CanRestore ? loadoutCommand : ""));

                if (loadout.CanRestore)
                {
                    selections[loadoutCommand] = new AetheriaRuntimeInventoryDropdownSelection(
                        AetheriaRuntimeInventoryDropdownSelectionKind.Loadout,
                        loadoutCommand,
                        templateIndex: loadout.TemplateIndex);
                }
            }

            if (loadoutOptions.Count > 0)
            {
                groups.Add(new AetheriaRuntimeInventoryDropdownGroup(
                    $"{SurfaceId}.loadouts.card",
                    "Loadouts",
                    loadoutOptions));
            }

            return new AetheriaRuntimeInventoryDropdownSurfaceModel(
                new AetheriaRuntimeInventoryDropdownSurfaceState(
                    currentView,
                    groups,
                    updatedAtUtc),
                selections);
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
                        "The observing client lists available inventory navigation; the shared runtime surface owns the dropdown contract."))
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

            var commandText = request.Operation?.OperationId ?? "";
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

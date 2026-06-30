using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Eve.Surface;

namespace GameCult.Aetheria.State.Verse
{
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
                        equipmentCommand,
                        new AetheriaRuntimeInventoryDropdownSelection(
                            AetheriaRuntimeInventoryDropdownSelectionKind.EntityEquipment,
                            equipmentCommand,
                            entityKey: entity.EntityKey,
                            entityIndex: entity.EntityIndex)));
                }

                foreach (var bay in entity.Bays)
                {
                    if (bay == null || bay.IsDisplayed)
                        continue;

                    var bayCommand = EntityBayCommand(entity.EntityIndex, bay.BayIndex);
                    options.Add(new AetheriaRuntimeInventoryDropdownOption(
                        $"{SurfaceId}.entity_{entity.EntityIndex}.bay_{bay.BayIndex}",
                        string.IsNullOrWhiteSpace(bay.Label) ? $"Bay {bay.BayIndex + 1}" : bay.Label,
                        bayCommand,
                        new AetheriaRuntimeInventoryDropdownSelection(
                            AetheriaRuntimeInventoryDropdownSelectionKind.EntityBay,
                            bayCommand,
                            entityKey: entity.EntityKey,
                            entityIndex: entity.EntityIndex,
                            bayIndex: bay.BayIndex)));
                }

                if (entity.Bays.Count == 0 && !entity.IsDisplayed)
                {
                    var entityCommand = EntityCommand(entity.EntityIndex);
                    options.Add(new AetheriaRuntimeInventoryDropdownOption(
                        $"{SurfaceId}.entity_{entity.EntityIndex}.select",
                        entity.Name,
                        entityCommand,
                        new AetheriaRuntimeInventoryDropdownSelection(
                            AetheriaRuntimeInventoryDropdownSelectionKind.Entity,
                            entityCommand,
                            entityKey: entity.EntityKey,
                            entityIndex: entity.EntityIndex)));
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
                            DockingBay,
                            new AetheriaRuntimeInventoryDropdownSelection(
                                AetheriaRuntimeInventoryDropdownSelectionKind.DockingBay,
                                DockingBay))
                    }));
            }

            var loadoutOptions = new List<AetheriaRuntimeInventoryDropdownOption>();
            if (canSaveLoadout)
            {
                loadoutOptions.Add(new AetheriaRuntimeInventoryDropdownOption(
                    $"{SurfaceId}.loadouts.save",
                    "Save Loadout",
                    SaveLoadout,
                    new AetheriaRuntimeInventoryDropdownSelection(
                        AetheriaRuntimeInventoryDropdownSelectionKind.SaveLoadout,
                        SaveLoadout)));
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
                    loadout.CanRestore ? loadoutCommand : "",
                    loadout.CanRestore
                        ? new AetheriaRuntimeInventoryDropdownSelection(
                        AetheriaRuntimeInventoryDropdownSelectionKind.Loadout,
                        loadoutCommand,
                        templateIndex: loadout.TemplateIndex)
                        : default));
            }

            if (loadoutOptions.Count > 0)
            {
                groups.Add(new AetheriaRuntimeInventoryDropdownGroup(
                    $"{SurfaceId}.loadouts.card",
                    "Loadouts",
                    loadoutOptions));
            }

            return Build(currentView, groups, updatedAtUtc);
        }

        public static AetheriaRuntimeSurfaceDocument BuildFromDocuments(
            AetheriaRuntimeStationRefitDocument stationRefit,
            AetheriaRuntimeInventoryDropdownSurfaceRequest request,
            string updatedAtUtc)
        {
            request ??= new AetheriaRuntimeInventoryDropdownSurfaceRequest();
            stationRefit ??= new AetheriaRuntimeStationRefitDocument();
            var entities = (stationRefit.AvailableEntities ?? Array.Empty<AetheriaRuntimeStationRefitEntityOption>())
                .Select((entity, entityIndex) => new AetheriaRuntimeInventoryDropdownEntityOption(
                    entityIndex,
                    entity.EntityKey,
                    string.IsNullOrWhiteSpace(entity.DisplayName) ? $"Entity {entity.EntityIndex}" : entity.DisplayName,
                    IsDisplayedEntity(request, entity.EntityKey),
                    Enumerable.Range(0, Math.Max(entity.CargoBayCount, 0))
                        .Select(bayIndex => new AetheriaRuntimeInventoryDropdownBayOption(
                            bayIndex,
                            $"Bay {bayIndex + 1}",
                            IsDisplayedCargoBay(request, entity.EntityKey, bayIndex)))
                        .ToArray()))
                .ToArray();
            var loadouts = (stationRefit.LoadoutRestoreOptions ?? Array.Empty<AetheriaRuntimeStationLoadoutRestoreOption>())
                .Select((loadout, optionIndex) => new AetheriaRuntimeInventoryDropdownLoadoutOption(
                    optionIndex,
                    loadout.TemplateName,
                    loadout.CanRestore ? $"{loadout.Price:n0}" : "",
                    loadout.CanRestore))
                .ToArray();
            var currentDockingBay = (stationRefit.DockingBays ?? Array.Empty<AetheriaRuntimeStationDockingBayRow>())
                .FirstOrDefault(row => row != null && row.DockingBayIndex == stationRefit.DockingBayIndex);
            var hasDockingBay = currentDockingBay != null && stationRefit.IsDocked;

            return Build(
                request.CurrentView,
                entities,
                hasDockingBay,
                hasDockingBay ? $"Docking Bay {currentDockingBay.DockingBayIndex + 1}" : "Docking Bay",
                hasDockingBay &&
                IsDisplayedCargoBay(
                    request,
                    stationRefit.DockParentEntityKey,
                    currentDockingBay.DockingBayIndex),
                request.CanSaveLoadout,
                loadouts,
                updatedAtUtc);
        }

        private static AetheriaRuntimeSurfaceDocument Build(
            string currentView,
            IReadOnlyList<AetheriaRuntimeInventoryDropdownGroup> groups,
            string updatedAtUtc,
            long version = 1)
        {
            groups ??= Array.Empty<AetheriaRuntimeInventoryDropdownGroup>();

            var children = new List<AetheriaRuntimeSurfaceComponent>
            {
                Card(
                    $"{SurfaceId}.summary",
                    "Inventory Actions",
                    Metric(
                        $"{SurfaceId}.current",
                        "Current View",
                        string.IsNullOrWhiteSpace(currentView) ? "None" : currentView),
                    Text(
                        $"{SurfaceId}.note",
                        "The observing client lists available inventory navigation; the shared runtime surface owns the dropdown contract."))
            };

            foreach (var group in groups)
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

            var commands = groups
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
                updatedAtUtc: updatedAtUtc ?? "",
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
                return Button(option.Id, option.Label, option.Command, option.Selection);

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

        private static bool IsDisplayedEntity(
            AetheriaRuntimeInventoryDropdownSurfaceRequest request,
            string entityKey)
        {
            return request != null &&
                   !string.IsNullOrWhiteSpace(request.DisplayedEntityKey) &&
                   string.Equals(request.DisplayedEntityKey, entityKey ?? "", StringComparison.Ordinal);
        }

        private static bool IsDisplayedCargoBay(
            AetheriaRuntimeInventoryDropdownSurfaceRequest request,
            string entityKey,
            int cargoBayIndex)
        {
            return request != null &&
                   cargoBayIndex >= 0 &&
                   request.DisplayedCargoIndex == cargoBayIndex &&
                   !string.IsNullOrWhiteSpace(request.DisplayedCargoEntityKey) &&
                   string.Equals(request.DisplayedCargoEntityKey, entityKey ?? "", StringComparison.Ordinal);
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

        private static AetheriaRuntimeSurfaceComponent Button(
            string id,
            string label,
            string command,
            AetheriaRuntimeInventoryDropdownSelection selection = default)
        {
            var props = new List<(string Key, string Value)>
            {
                ("label", label ?? ""),
                ("command", command ?? "")
            };

            if (selection.Kind != AetheriaRuntimeInventoryDropdownSelectionKind.Unknown)
            {
                props.Add(("selectionKind", selection.Kind.ToString()));
                props.Add(("entityKey", selection.EntityKey ?? ""));
                props.Add(("entityIndex", selection.EntityIndex.ToString(CultureInfo.InvariantCulture)));
                props.Add(("bayIndex", selection.BayIndex.ToString(CultureInfo.InvariantCulture)));
                props.Add(("templateIndex", selection.TemplateIndex.ToString(CultureInfo.InvariantCulture)));
            }

            return Node(id, "control.button", props);
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

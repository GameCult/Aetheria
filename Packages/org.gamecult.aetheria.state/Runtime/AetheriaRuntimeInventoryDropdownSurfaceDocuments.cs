using System;
using System.Collections.Generic;
using GameCult.Caching;
using MessagePack;

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeInventoryDropdownOption
    {
        public AetheriaRuntimeInventoryDropdownOption(
            string id,
            string label,
            string command,
            AetheriaRuntimeInventoryDropdownSelection selection = default)
        {
            Id = id ?? "";
            Label = label ?? "";
            Command = command ?? "";
            Selection = selection;
        }

        public string Id { get; }
        public string Label { get; }
        public string Command { get; }
        public AetheriaRuntimeInventoryDropdownSelection Selection { get; }
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

    public sealed class AetheriaRuntimeInventoryDropdownSurfaceRequest
    {
        public string CurrentView { get; set; } = "";

        public string DisplayedEntityKey { get; set; } = "";

        public string DisplayedCargoEntityKey { get; set; } = "";

        public int DisplayedCargoIndex { get; set; } = -1;

        public bool CanSaveLoadout { get; set; }
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

    [MessagePackObject]
    public readonly struct AetheriaRuntimeInventoryDropdownSelection
    {
        [SerializationConstructor]
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

        [Key(0)]
        public AetheriaRuntimeInventoryDropdownSelectionKind Kind { get; }

        [Key(1)]
        public string Command { get; }

        [Key(2)]
        public string EntityKey { get; }

        [Key(3)]
        public int EntityIndex { get; }

        [Key(4)]
        public int BayIndex { get; }

        [Key(5)]
        public int TemplateIndex { get; }
    }

}

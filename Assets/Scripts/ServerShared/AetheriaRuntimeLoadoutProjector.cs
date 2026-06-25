using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using Unity.Mathematics;

public static class AetheriaRuntimeLoadoutProjector
{
    public static AetheriaRuntimeLoadoutTemplateCommit ProjectLoadoutTemplate(EntityConstructionBlueprint blueprint)
    {
        return new AetheriaRuntimeLoadoutTemplateCommit
        {
            Name = blueprint?.Name ?? "",
            OwnerPlayerKey = "global:aetheria.player_settings.v1",
            RootEntity = ProjectEntityLoadout(blueprint)
        };
    }

    public static AetheriaRuntimeLoadoutTemplateCommit ProjectLoadoutTemplate(Entity entity)
    {
        return new AetheriaRuntimeLoadoutTemplateCommit
        {
            Name = entity?.Name ?? "",
            OwnerPlayerKey = "global:aetheria.player_settings.v1",
            RootEntity = ProjectEntityLoadout(entity)
        };
    }

    private static AetheriaRuntimeEntityLoadoutCommit ProjectEntityLoadout(EntityConstructionBlueprint blueprint)
    {
        return new AetheriaRuntimeEntityLoadoutCommit
        {
            Name = blueprint?.Name ?? "",
            Kind = blueprint is ShipConstructionBlueprint ? "ship" : blueprint is OrbitalEntityConstructionBlueprint ? "orbital" : "entity",
            FactionKey = blueprint?.FactionKey ?? "",
            Hull = ProjectLoadoutItem(blueprint?.Hull),
            Equipment = ProjectSlots(blueprint?.Equipment),
            CargoBays = ProjectSlots(blueprint?.CargoBays),
            DockingBays = ProjectSlots(blueprint?.DockingBays),
            CargoContents = ProjectCargoBays(blueprint?.CargoContents),
            DockingBayContents = ProjectCargoBays(blueprint?.DockingBayContents),
            DockingBayAssignments = blueprint?.DockingBayAssignments ?? Array.Empty<int>(),
            WeaponGroups = blueprint?.WeaponGroups?.Select(group => (IReadOnlyList<int>)group).ToArray() ?? Array.Empty<IReadOnlyList<int>>(),
            Children = blueprint?.Children?.Select(ProjectEntityLoadout).ToArray() ?? Array.Empty<AetheriaRuntimeEntityLoadoutCommit>()
        };
    }

    private static AetheriaRuntimeEntityLoadoutCommit ProjectEntityLoadout(Entity entity)
    {
        return new AetheriaRuntimeEntityLoadoutCommit
        {
            Name = entity?.Name ?? "",
            Kind = entity is Ship ? "ship" : entity is OrbitalEntity ? "orbital" : "entity",
            FactionKey = entity?.Faction?.FactionKey ?? "",
            Hull = ProjectLoadoutItem(entity?.Hull),
            Equipment = ProjectSlots(entity?.Equipment),
            CargoBays = ProjectSlots(entity?.CargoBays),
            DockingBays = ProjectSlots(entity?.DockingBays),
            CargoContents = ProjectCargoBays(entity?.CargoBays),
            DockingBayContents = ProjectCargoBays(entity?.DockingBays),
            DockingBayAssignments = entity?.DockingBays?
                .Select(dockingBay => entity.Children.IndexOf(dockingBay.DockedShip))
                .ToArray() ?? Array.Empty<int>(),
            WeaponGroups = entity?.WeaponGroups?
                .Select(group => (IReadOnlyList<int>)group.items
                    .Select(item => entity.Equipment.IndexOf(item))
                    .ToArray())
                .ToArray() ?? Array.Empty<IReadOnlyList<int>>(),
            Children = entity?.Children?
                .Select(ProjectEntityLoadout)
                .ToArray() ?? Array.Empty<AetheriaRuntimeEntityLoadoutCommit>()
        };
    }

    private static AetheriaRuntimeLoadoutItemSlotCommit[] ProjectSlots((int2 position, EquippableItem item)[] slots)
    {
        return slots?
            .Select(slot => new AetheriaRuntimeLoadoutItemSlotCommit
            {
                X = slot.position.x,
                Y = slot.position.y,
                Item = ProjectLoadoutItem(slot.item)
            })
            .ToArray() ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
    }

    private static AetheriaRuntimeLoadoutItemSlotCommit[] ProjectSlots(IEnumerable<EquippedItem> slots)
    {
        return slots?
            .Select(slot => new AetheriaRuntimeLoadoutItemSlotCommit
            {
                X = slot.Position.x,
                Y = slot.Position.y,
                Item = ProjectLoadoutItem(slot.EquippableItem, slot.Temperature)
            })
            .ToArray() ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
    }

    private static AetheriaRuntimeCargoBayLoadoutCommit[] ProjectCargoBays((int2 position, ItemInstance item)[][] bays)
    {
        return bays?
            .Select(bay => new AetheriaRuntimeCargoBayLoadoutCommit
            {
                Items = bay?
                    .Select(slot => new AetheriaRuntimeLoadoutItemSlotCommit
                    {
                        X = slot.position.x,
                        Y = slot.position.y,
                        Item = ProjectLoadoutItem(slot.item)
                    })
                    .ToArray() ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()
            })
            .ToArray() ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
    }

    private static AetheriaRuntimeCargoBayLoadoutCommit[] ProjectCargoBays(IEnumerable<EquippedCargoBay> bays)
    {
        return bays?
            .Select(bay => new AetheriaRuntimeCargoBayLoadoutCommit
            {
                Items = bay.Cargo?
                    .Select(slot => new AetheriaRuntimeLoadoutItemSlotCommit
                    {
                        X = slot.Value.x,
                        Y = slot.Value.y,
                        Item = ProjectLoadoutItem(slot.Key)
                    })
                    .ToArray() ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()
            })
            .ToArray() ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
    }

    private static AetheriaRuntimeLoadoutItemCommit ProjectLoadoutItem(
        ItemInstance item,
        double temperature = 0)
    {
        if (item == null)
            return new AetheriaRuntimeLoadoutItemCommit();

        return new AetheriaRuntimeLoadoutItemCommit
        {
            ItemKey = item.ItemKey,
            Quality = item is CraftedItemInstance crafted ? crafted.Quality : 1.0,
            Durability = item is EquippableItem equippable ? equippable.Durability : 1.0,
            Quantity = item is SimpleCommodity commodity ? commodity.Quantity : 1,
            Enabled = true,
            OverrideShutdown = item is EquippableItem overrideable && overrideable.OverrideShutdown,
            Temperature = temperature
        };
    }
}

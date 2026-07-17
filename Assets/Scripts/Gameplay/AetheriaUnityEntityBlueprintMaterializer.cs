/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using Unity.Mathematics;

public sealed class AetheriaUnityEntityBlueprintMaterializer
{
    private readonly AetheriaUnityLoadoutItemFactory _loadoutItemFactory;

    public AetheriaUnityEntityBlueprintMaterializer(
        AetheriaUnityLoadoutItemFactory loadoutItemFactory)
    {
        _loadoutItemFactory = loadoutItemFactory ?? throw new ArgumentNullException(nameof(loadoutItemFactory));
    }

    public EntityConstructionBlueprint MaterializeTemplate(AetheriaRuntimeLoadoutTemplateSnapshot template)
    {
        if (template == null)
            return null;

        var blueprint = MaterializeLoadoutEntity(template.RootEntity);
        if (blueprint != null && string.IsNullOrWhiteSpace(blueprint.Name))
            blueprint.Name = template.Name;
        return blueprint;
    }

    public EntityConstructionBlueprint MaterializeLoadoutEntity(AetheriaRuntimeEntityLoadoutSnapshot entity)
    {
        if (entity == null)
            return null;

        var hull = CreateEquippableItem(entity.Hull);
        if (hull == null)
            return null;

        var blueprint = CreateBlueprint(entity.Kind);
        if (blueprint is ShipConstructionBlueprint shipBlueprint)
        {
            shipBlueprint.Direction = new float2(0, 1);
        }

        blueprint.Name = entity.Name;
        blueprint.FactionKey = entity.FactionKey ?? "";
        blueprint.Hull = hull;
        blueprint.Equipment = CreateEquippableSlots(entity.Equipment);
        blueprint.CargoBays = CreateEquippableSlots(entity.CargoBays);
        blueprint.DockingBays = CreateEquippableSlots(entity.DockingBays);
        blueprint.CargoContents = CreateCargoBayContents(entity.CargoContents);
        blueprint.DockingBayContents = CreateCargoBayContents(entity.DockingBayContents);
        blueprint.DockingBayAssignments = entity.DockingBayAssignments.ToArray();
        blueprint.WeaponGroups = entity.WeaponGroups.Select(group => group.ToArray()).ToArray();
        blueprint.Children = entity.Children
            .Select(MaterializeLoadoutEntity)
            .Where(child => child != null)
            .ToArray();
        return blueprint;
    }

    public EntityConstructionBlueprint MaterializeObservedEntity(
        AetheriaRuntimeEntitySnapshot entity,
        bool isCurrentEntity)
    {
        if (entity == null)
            return null;

        var hull = CreateEquippableItem(new AetheriaRuntimeLoadoutItemSnapshot(
            entity.HullItemKey,
            1,
            1,
            1,
            true,
            false));
        if (hull == null)
            return null;

        var blueprint = CreateBlueprint(entity.Kind);
        if (blueprint is ShipConstructionBlueprint shipBlueprint)
        {
            shipBlueprint.Position = new float3((float)entity.PositionX, (float)entity.PositionY, (float)entity.PositionZ);
            shipBlueprint.Direction = new float2((float)entity.DirectionX, (float)entity.DirectionY);
            shipBlueprint.IsPlayerShip = isCurrentEntity;
        }
        else if (blueprint is OrbitalEntityConstructionBlueprint orbitalBlueprint)
        {
            orbitalBlueprint.OrbitKey = entity.OrbitKey ?? "";
            orbitalBlueprint.SecurityLevel = (SecurityLevel)entity.SecurityLevel;
            orbitalBlueprint.SecurityRadius = (float)entity.SecurityRadius;
        }

        blueprint.Name = entity.Name ?? "";
        blueprint.FactionKey = entity.FactionKey ?? "";
        blueprint.Hull = hull;
        blueprint.Equipment = CreateEquippableSlots(entity.Equipment);
        blueprint.CargoBays = CreateEquippableSlots(entity.CargoBays);
        blueprint.DockingBays = CreateEquippableSlots(entity.DockingBays);
        blueprint.CargoContents = CreateCargoBayContents(entity.CargoContents);
        blueprint.DockingBayContents = CreateCargoBayContents(entity.DockingBayContents);
        blueprint.DockingBayAssignments = entity.DockingBayAssignments.ToArray();
        blueprint.WeaponGroups = entity.WeaponGroups.Select(group => group.ToArray()).ToArray();
        blueprint.Children = Array.Empty<EntityConstructionBlueprint>();
        return blueprint;
    }

    private static EntityConstructionBlueprint CreateBlueprint(string kind)
    {
        return string.Equals(kind, "orbital", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "station", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "turret", StringComparison.OrdinalIgnoreCase)
            ? new OrbitalEntityConstructionBlueprint()
            : new ShipConstructionBlueprint();
    }

    private (int2 position, EquippableItem item)[] CreateEquippableSlots(
        IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot> slots)
    {
        return (slots ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotSnapshot>())
            .Select(slot => (position: new int2(slot.X, slot.Y), item: CreateEquippableItem(slot.Item)))
            .Where(slot => slot.item != null)
            .ToArray();
    }

    private (int2 position, EquippableItem item)[] CreateEquippableSlots(
        IReadOnlyList<AetheriaRuntimeEntityItemSlotSnapshot> slots)
    {
        return (slots ?? Array.Empty<AetheriaRuntimeEntityItemSlotSnapshot>())
            .Select(slot => (position: new int2(slot.X, slot.Y), item: CreateEquippableItem(new AetheriaRuntimeLoadoutItemSnapshot(
                slot.ItemKey,
                slot.Quality,
                slot.Durability,
                slot.Quantity,
                slot.Enabled,
                slot.OverrideShutdown,
                slot.Temperature))))
            .Where(slot => slot.item != null)
            .ToArray();
    }

    private (int2 position, ItemInstance item)[][] CreateCargoBayContents(
        IReadOnlyList<AetheriaRuntimeCargoBayLoadoutSnapshot> bays)
    {
        return (bays ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutSnapshot>())
            .Select(bay => (bay.Items ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotSnapshot>())
                .Select(slot => (position: new int2(slot.X, slot.Y), item: _loadoutItemFactory.CreateLoadoutItem(slot.Item)))
                .Where(slot => slot.item != null)
                .ToArray())
            .ToArray();
    }

    private EquippableItem CreateEquippableItem(AetheriaRuntimeLoadoutItemSnapshot item)
    {
        var instance = _loadoutItemFactory.CreateLoadoutItem(item) as EquippableItem;
        if (instance != null && item.Durability > 0)
            instance.Durability = (float)item.Durability;
        return instance;
    }
}

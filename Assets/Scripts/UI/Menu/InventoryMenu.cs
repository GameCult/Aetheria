/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Aetheria.EveRuntime;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
using UnityEngine;
using UniRx;
using UniRx.Triggers;
using Unity.Mathematics;
using UnityEngine.UIElements;

public class InventoryMenu : MonoBehaviour
{
    public InventoryPanel[] InventoryPanels;
    public ActionGameManager GameManager;
    public ConfirmationDialog Dialog;
    // public ClickCatcher Background;

    private int2 _selectedPosition;
    private InventoryPanel _selectedPanel;
    private ItemInstance _selectedItem;
    private EquippedItem _selectedEquippedItem;
    private int2[] _selectedCells;
    private UIDocument _shipSettingsSurfaceDocument;
    private UIDocument _cargoItemDetailsSurfaceDocument;
    private UIDocument _equippedItemDetailsSurfaceDocument;
    private readonly AetheriaEveUnitySurfaceChrome _shipSettingsSurfaceChrome = PanelChrome(360f, 420f);
    private readonly AetheriaEveUnitySurfaceChrome _cargoItemDetailsSurfaceChrome = PanelChrome(420f, 520f);
    private readonly AetheriaEveUnitySurfaceChrome _equippedItemDetailsSurfaceChrome = PanelChrome(460f, 560f);
    // private List<IDisposable> _backgroundSubscriptions;

    // private ItemInstance _dragItem;
    // private Transform[] _dragCells;
    // private Vector2[] _dragOffsets;
    // private int2 _dragCellOffset;
    // private ItemRotation _originalRotation;
    // //private Shape _previousFakeOccupancy;
    // private Shape _originalOccupancy;
    // private EquippedItem _originalEquippedItem;
    // private InventoryPanel _originalPanel;
    //
    // private InventoryPanel _dragTargetPanel;
    // private int2 _dragTargetPosition;
    // private int2 _lastDragPosition;
    // private bool _dragTargetValid;
    // private bool _destroyItem;

    private void OnEnable()
    {
        // Background.gameObject.SetActive(true);

        // Background.OnEnter.Subscribe(enter =>
        // {
        //     _destroyItem = true;
        // });
        // BackgroundExited.OnPointerExitAsObservable().Subscribe(enter =>
        // {
        //     _destroyItem = false;
        // });
        var hasObservedCurrentEntity = GameManager.TryGetObservedCurrentEntity(out var currentEntity);
        var cargo = GameManager.TryGetObservedDockingBay(out var dockingBay)
            ? dockingBay
            : currentEntity?.CargoBays.FirstOrDefault();
        if (cargo!=null)
            InventoryPanels[0].Display(cargo);
        else InventoryPanels[0].Clear();
        if(hasObservedCurrentEntity)
            InventoryPanels[1].Display(currentEntity);
        else InventoryPanels[1].Clear();
    }

    private void OnDisable()
    {
        HideCurrentShipSettingsSurface();
        HideCargoItemDetailsSurface();
        HideEquippedItemDetailsSurface();
        // Background.gameObject.SetActive(false);
    }

    void Start()
    {
        foreach (var panel in InventoryPanels)
        {
            panel.OnClickAsObservable().Subscribe(e =>
            {
                if (e.data is InventoryCargoEventData cargoEvent)
                {
                    var item = cargoEvent.CargoBay.Occupancy[cargoEvent.Position.x, cargoEvent.Position.y];
                    if(item!=null)
                    {
                        if (e.clickCount == 2)
                        {
                            HideCargoItemDetailsSurface();
                            HideEquippedItemDetailsSurface();
                            ClearSelectedItemSelection();
                            var otherPanel = panel == InventoryPanels[0] ? InventoryPanels[1] : InventoryPanels[0];
                            RequestCargoItemTransfer(cargoEvent.CargoBay, otherPanel, item);
                            // TODO: SFX: Equip
                            // else
                            // TODO: SFX: Fail
                        }
                        else
                        {
                            HideCurrentShipSettingsSurface();
                            HideCargoItemDetailsSurface();
                            HideEquippedItemDetailsSurface();
                            ClearSelectedItemSelection();
                            _selectedPanel = panel;
                            _selectedPosition = cargoEvent.CargoBay.Cargo[item];
                            _selectedItem = item;
                            _selectedCells = GetSelectedCells(item, _selectedPosition);
                            ApplySelectedCellHighlight();
                            RenderCargoItemDetailsSurface(item);
                            // TODO: SFX: Success
                        }
                    }
                }
                else if (e.data is InventoryEntityEventData entityEvent)
                {
                    var item = entityEvent.Entity.GearOccupancy[entityEvent.Position.x, entityEvent.Position.y];
                    if (item != null)
                    {
                        if (e.clickCount == 2)
                        {
                            HideCargoItemDetailsSurface();
                            HideEquippedItemDetailsSurface();
                            ClearSelectedItemSelection();
                            var otherPanel = panel == InventoryPanels[0] ? InventoryPanels[1] : InventoryPanels[0];
                            RequestEquippedItemTransfer(entityEvent.Entity, item, otherPanel);
                            // TODO: SFX: Unequip
                            // else
                            // TODO: SFX: Fail
                        }
                        else
                        {
                            HideCurrentShipSettingsSurface();
                            HideCargoItemDetailsSurface();
                            ClearSelectedItemSelection();
                            _selectedPanel = panel;
                            _selectedPosition = item.Position;
                            _selectedItem = item.EquippableItem;
                            _selectedEquippedItem = item;
                            _selectedCells = GetSelectedCells(item.EquippableItem, _selectedPosition);
                            ApplySelectedCellHighlight();
                            RenderEquippedItemDetailsSurface(item);
                            // TODO: SFX: Success
                        }
                    }
                }
            });

            panel.OnBackgroundClick.Subscribe(data =>
            {
                if (!GameManager.TryGetObservedCurrentEntity(out var currentEntity)) return;

                RenderCurrentShipSettingsSurface(currentEntity);
            });
        }
    }

    private void RenderCurrentShipSettingsSurface(Entity entity)
    {
        if (entity == null)
            return;

        HideCargoItemDetailsSurface();
        HideEquippedItemDetailsSurface();
        ClearSelectedItemSelection();

        _shipSettingsSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _shipSettingsSurfaceDocument,
            "Aetheria Inventory Ship Settings Surface",
            AetheriaRuntimeShipSettingsSurfaceBuilder.Build(ProjectCurrentShipSettingsSurface(entity)),
            HandleCurrentShipSettingsSurfaceCommand,
            _shipSettingsSurfaceChrome);
    }

    private void HandleCurrentShipSettingsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (!GameManager.TryGetObservedCurrentEntity(out var entity))
        {
            HideCurrentShipSettingsSurface();
            return;
        }

        if (entity?.Settings == null)
        {
            HideCurrentShipSettingsSurface();
            return;
        }

        if (!AetheriaRuntimeShipSettingsSurfaceCommands.TryRead(request, out var command))
        {
            Debug.LogWarning($"Unknown inventory ship settings command: {request?.Command}");
            return;
        }

        switch (command.Kind)
        {
            case AetheriaRuntimeShipSettingsCommandKind.DecrementShutdownThreshold:
            case AetheriaRuntimeShipSettingsCommandKind.IncrementShutdownThreshold:
            case AetheriaRuntimeShipSettingsCommandKind.ResetShutdownThreshold:
                GameManager.RequestEntityShutdownPerformance(
                    entity,
                    AetheriaRuntimeShipSettingsSurfaceCommands.ResolveShutdownPerformance(
                        command.Kind,
                        entity.Settings.ShutdownPerformance,
                        GameManager.Settings.GameplaySettings.DefaultShutdownPerformance));
                return;
            case AetheriaRuntimeShipSettingsCommandKind.Close:
                HideCurrentShipSettingsSurface();
                return;
            default:
                Debug.LogWarning($"Unknown inventory ship settings command: {request?.Command}");
                return;
        }
    }

    private void HideCurrentShipSettingsSurface()
    {
        if (_shipSettingsSurfaceDocument == null)
            return;

        AetheriaEveUnitySurfaceHost.Hide(_shipSettingsSurfaceDocument);
    }

    private static AetheriaRuntimeShipSettingsSurfaceState ProjectCurrentShipSettingsSurface(Entity entity)
    {
        return AetheriaRuntimeShipSettingsSurfaceBuilder.Project(
            entity?.Name ?? "",
            entity?.Settings?.ShutdownPerformance ?? 0f,
            ActionGameManager.RuntimePlayerSettings.Format);
    }

    private void RenderCargoItemDetailsSurface(ItemInstance item)
    {
        var typedItem = FindTypedInventoryItem(item);
        if (item == null || typedItem == null)
            return;

        HideEquippedItemDetailsSurface();

        _cargoItemDetailsSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _cargoItemDetailsSurfaceDocument,
            "Aetheria Inventory Cargo Item Details Surface",
            AetheriaRuntimeCargoItemDetailsSurfaceBuilder.Build(ProjectCargoItemDetailsSurface(item, typedItem)),
            HandleCargoItemDetailsSurfaceCommand,
            _cargoItemDetailsSurfaceChrome,
            sortingOrder: 1001);
    }

    private void HandleCargoItemDetailsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (!AetheriaRuntimeCargoItemDetailsSurfaceCommands.TryRead(request, out var command))
        {
            Debug.LogWarning($"Unknown inventory cargo item details command: {request?.Command}");
            return;
        }

        if (command.Kind == AetheriaRuntimeCargoItemDetailsCommandKind.Close)
        {
            HideCargoItemDetailsSurface();
            ClearSelectedItemSelection();
            return;
        }

        Debug.LogWarning($"Unknown inventory cargo item details command: {request?.Command}");
    }

    private void HideCargoItemDetailsSurface()
    {
        if (_cargoItemDetailsSurfaceDocument == null)
            return;

        AetheriaEveUnitySurfaceHost.Hide(_cargoItemDetailsSurfaceDocument);
    }

    private void RenderEquippedItemDetailsSurface(EquippedItem item)
    {
        var typedItem = FindTypedInventoryItem(item?.EquippableItem);
        if (item?.EquippableItem == null || typedItem == null)
            return;

        _equippedItemDetailsSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _equippedItemDetailsSurfaceDocument,
            "Aetheria Inventory Equipped Item Details Surface",
            AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.Build(ProjectEquippedItemDetailsSurface(item, typedItem)),
            HandleEquippedItemDetailsSurfaceCommand,
            _equippedItemDetailsSurfaceChrome,
            sortingOrder: 1002);
    }

    private void HandleEquippedItemDetailsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        var item = _selectedEquippedItem;
        if (item?.EquippableItem == null)
        {
            HideEquippedItemDetailsSurface();
            ClearSelectedItemSelection();
            return;
        }

        if (!AetheriaRuntimeEquippedItemDetailsSurfaceCommands.TryRead(request, out var command))
        {
            Debug.LogWarning($"Unknown inventory equipped item details command: {request?.Command}");
            return;
        }

        switch (command.Kind)
        {
            case AetheriaRuntimeEquippedItemDetailsCommandKind.Close:
                HideEquippedItemDetailsSurface();
                ClearSelectedItemSelection();
                return;
            case AetheriaRuntimeEquippedItemDetailsCommandKind.ToggleOverrideShutdown:
                GameManager.RequestEquippedItemOverrideShutdown(item, !item.EquippableItem.OverrideShutdown);
                return;
            case AetheriaRuntimeEquippedItemDetailsCommandKind.SetTargetTemperature:
                if (command.BehaviorIndex >= 0 &&
                    command.BehaviorIndex < (item.Behaviors?.Length ?? 0) &&
                    item.Behaviors[command.BehaviorIndex] is Thermotoggle thermotoggle &&
                    thermotoggle.Adjustable &&
                    GameManager != null)
                {
                    GameManager.RequestThermotoggleTargetTemperature(thermotoggle, command.TargetTemperature);
                    return;
                }

                Debug.LogWarning("Unable to apply equipped-item target temperature command.");
                RenderEquippedItemDetailsSurface(item);
                return;
            case AetheriaRuntimeEquippedItemDetailsCommandKind.ToggleWeaponGroup:
                if (command.GroupIndex >= 0)
                {
                    var assigned = !IsWeaponGroupAssigned(item, command.GroupIndex);
                    GameManager.RequestWeaponGroupMembership(item, command.GroupIndex, assigned);
                    return;
                }

                Debug.LogWarning("Unable to submit equipped-item weapon group membership request.");
                return;
            case AetheriaRuntimeEquippedItemDetailsCommandKind.BindWeaponGroup:
                if (command.GroupIndex >= 0 &&
                    command.SlotIndex >= 0)
                {
                    GameManager.RequestWeaponGroupActionBarBinding(command.SlotIndex, command.GroupIndex);
                    return;
                }

                Debug.LogWarning("Unable to submit equipped-item action-bar binding request.");
                return;
            case AetheriaRuntimeEquippedItemDetailsCommandKind.ClearActionBarBinding:
                if (command.SlotIndex >= 0)
                {
                    GameManager.RequestClearActionBarBinding(command.SlotIndex);
                    return;
                }

                Debug.LogWarning("Unable to submit equipped-item action-bar clear request.");
                return;
            default:
                Debug.LogWarning($"Unknown inventory equipped item details command: {request?.Command}");
                return;
        }
    }

    private void HideEquippedItemDetailsSurface()
    {
        if (_equippedItemDetailsSurfaceDocument == null)
            return;

        AetheriaEveUnitySurfaceHost.Hide(_equippedItemDetailsSurfaceDocument);
    }

    private static AetheriaEveUnitySurfaceChrome PanelChrome(float width, float maxWidth)
    {
        return new AetheriaEveUnitySurfaceChrome
        {
            RootAlignItems = Align.FlexStart,
            RootJustifyContent = Justify.FlexStart,
            RootPaddingTop = 0f,
            Width = width,
            MinWidth = 0f,
            MaxWidth = maxWidth,
            PaddingLeft = 18f,
            PaddingRight = 18f,
            PaddingTop = 18f,
            PaddingBottom = 18f
        };
    }

    private AetheriaRuntimeEquippedItemDetailsSurfaceState ProjectEquippedItemDetailsSurface(
        EquippedItem item,
        AetheriaRuntimeCatalogItem typedItem)
    {
        var hasWeapon = item.GetBehavior<Weapon>() != null;

        return AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.Project(
            typedItem,
            ProjectEquippedItemObservation(item),
            BuildEquippedItemTitle(item, typedItem),
            ActionGameManager.RuntimeCatalog?.GetManufacturer(typedItem)?.Name ?? "GameCult",
            ActionGameManager.RuntimePlayerSettings.Format,
            ActionGameManager.RuntimePlayerSettings.FormatTemperature,
            ProjectEquippedItemTemperatureControls(item).ToArray(),
            hasWeapon ? ProjectEquippedItemWeaponGroupControls(item).ToArray() : Array.Empty<AetheriaRuntimeEquippedItemControl>(),
            hasWeapon ? ProjectEquippedItemActionBarSlots(item).ToArray() : Array.Empty<AetheriaRuntimeEquippedItemActionBarSlot>());
    }

    private static AetheriaRuntimeEquippedItemObservation ProjectEquippedItemObservation(EquippedItem item)
    {
        var equippableItem = item?.EquippableItem;
        return new AetheriaRuntimeEquippedItemObservation(
            equippableItem?.ItemKey ?? "",
            equippableItem?.Quality ?? 1,
            equippableItem?.Durability ?? 1,
            item?.Temperature ?? 0,
            equippableItem != null && equippableItem.OverrideShutdown);
    }

    private IEnumerable<AetheriaRuntimeEquippedItemTemperatureControl> ProjectEquippedItemTemperatureControls(
        EquippedItem item)
    {
        for (var i = 0; i < (item.Behaviors?.Length ?? 0); i++)
        {
            if (item.Behaviors[i] is not Thermotoggle thermotoggle || !thermotoggle.Adjustable)
                continue;

            yield return new AetheriaRuntimeEquippedItemTemperatureControl(
                $"{AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.SurfaceId}.controls.thermotoggle.{i}.input",
                $"Target Temperature {i + 1}",
                thermotoggle.TargetTemperature.ToString(CultureInfo.InvariantCulture),
                AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.Payload(
                    ("behaviorIndex", i.ToString(CultureInfo.InvariantCulture))));
        }
    }

    private IEnumerable<AetheriaRuntimeEquippedItemControl> ProjectEquippedItemWeaponGroupControls(
        EquippedItem item)
    {
        return Enumerable.Range(0, item.Entity?.WeaponGroups?.Length ?? 0)
            .Select(groupIndex => new AetheriaRuntimeEquippedItemControl(
                $"{AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.SurfaceId}.weapon_groups.{groupIndex}",
                IsWeaponGroupAssigned(item, groupIndex) ? $"G{groupIndex + 1} On" : $"G{groupIndex + 1} Off",
                AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.ToggleWeaponGroup,
                AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.Payload(
                    ("group", groupIndex.ToString(CultureInfo.InvariantCulture)))));
    }

    private IEnumerable<AetheriaRuntimeEquippedItemActionBarSlot> ProjectEquippedItemActionBarSlots(
        EquippedItem item)
    {
        for (var slotIndex = 0; slotIndex < GameManager.GetActionBarSlotCount(); slotIndex++)
        {
            var controls = Enumerable.Range(0, item.Entity?.WeaponGroups?.Length ?? 0)
                .Select(groupIndex => new AetheriaRuntimeEquippedItemControl(
                    $"{AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.SurfaceId}.action_bar.{slotIndex}.group.{groupIndex}",
                    $"G{groupIndex + 1}",
                    AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.BindWeaponGroup,
                    AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.Payload(
                        ("slot", slotIndex.ToString(CultureInfo.InvariantCulture)),
                        ("group", groupIndex.ToString(CultureInfo.InvariantCulture)))))
                .Concat(new[]
                {
                    new AetheriaRuntimeEquippedItemControl(
                        $"{AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.SurfaceId}.action_bar.{slotIndex}.clear",
                        "Clear",
                        AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.ClearActionBarBinding,
                        AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.Payload(
                            ("slot", slotIndex.ToString(CultureInfo.InvariantCulture))))
                })
                .ToArray();

            yield return new AetheriaRuntimeEquippedItemActionBarSlot(
                $"{AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.SurfaceId}.action_bar.{slotIndex}.card",
                $"Action Bar {GameManager.GetActionBarSlotLabel(slotIndex)}",
                GameManager.GetActionBarBindingLabel(slotIndex),
                controls);
        }
    }

    private AetheriaRuntimeCargoItemDetailsSurfaceState ProjectCargoItemDetailsSurface(
        ItemInstance item,
        AetheriaRuntimeCatalogItem typedItem)
    {
        return AetheriaRuntimeCargoItemDetailsSurfaceBuilder.Project(
            typedItem,
            ProjectCargoItemObservation(item),
            ActionGameManager.RuntimeCatalog?.GetManufacturer(typedItem)?.Name ?? "GameCult",
            item is EquippableItem equippableItem ? FormatItemTier(typedItem, equippableItem) : "",
            ActionGameManager.RuntimePlayerSettings.Format,
            ActionGameManager.RuntimePlayerSettings.FormatTemperature);
    }

    private static AetheriaRuntimeCargoItemObservation ProjectCargoItemObservation(ItemInstance item)
    {
        var equippableItem = item as EquippableItem;
        return new AetheriaRuntimeCargoItemObservation(
            item?.ItemKey ?? "",
            item is SimpleCommodity simpleCommodity ? simpleCommodity.Quantity : 0,
            equippableItem != null,
            equippableItem?.Quality ?? 1,
            equippableItem?.Durability ?? 1,
            equippableItem?.Temperature ?? 0,
            equippableItem != null && equippableItem.OverrideShutdown);
    }

    private string BuildEquippedItemTitle(EquippedItem item, AetheriaRuntimeCatalogItem typedItem)
    {
        return $"{typedItem?.Name ?? "Unknown Item"} ({FormatItemTier(typedItem, item.EquippableItem)})";
    }

    private string FormatItemTier(AetheriaRuntimeCatalogItem typedItem, EquippableItem item)
    {
        var tradeProjection = AetheriaRuntimeDaemonTradeItemQueries.ProjectTradeItem(
            typedItem,
            ToRuntimeLoadoutItem(item),
            GameManager.ObservedTradeValueSettings());
        return tradeProjection.HasTier
            ? $"{tradeProjection.TierName}{new string('+', tradeProjection.Upgrades)}"
            : "";
    }

    private static AetheriaRuntimeLoadoutItemCommit ToRuntimeLoadoutItem(EquippableItem item)
    {
        return AetheriaRuntimeDaemonItemStatQueries.ItemCommit(
            item?.ItemKey ?? "",
            item?.Quality ?? 1,
            item?.Durability ?? 1,
            enabled: true,
            overrideShutdown: item != null && item.OverrideShutdown);
    }

    private static bool IsWeaponGroupAssigned(EquippedItem item, int groupIndex)
    {
        return item?.Entity?.WeaponGroups != null &&
               groupIndex >= 0 &&
               groupIndex < item.Entity.WeaponGroups.Length &&
               item.Entity.WeaponGroups[groupIndex].items.Contains(item);
    }

    private void RequestCargoItemTransfer(EquippedCargoBay origin, InventoryPanel destination, ItemInstance item)
    {
        if (destination.DisplayedEntity != null && item is EquippableItem equippableItem)
        {
            GameManager.RequestCargoItemEquip(origin, destination.DisplayedEntity, equippableItem);
            return;
        }

        if (destination.DisplayedCargo != null)
            GameManager.RequestCargoItemTransfer(origin, destination.DisplayedCargo, item);
    }

    private void ClearSelectedItemSelection()
    {
        ClearSelectedCellHighlight();
        _selectedPanel = null;
        _selectedItem = null;
        _selectedEquippedItem = null;
        _selectedCells = null;
        _selectedPosition = default;
    }

    private void RequestEquippedItemTransfer(Entity origin, EquippedItem item, InventoryPanel destination)
    {
        if (destination.DisplayedCargo != null)
            GameManager.RequestEquippedItemStore(origin, item, destination.DisplayedCargo);
    }

    void Update()
    {
        // if (Keyboard.current.qKey.wasPressedThisFrame && _dragItem != null)
        // {
        //     _dragItem.Rotation = (ItemRotation) (((int) _dragItem.Rotation + 1) % 4);
        // }
        // if (Keyboard.current.eKey.wasPressedThisFrame && _dragItem != null)
        // {
        //     _dragItem.Rotation = (ItemRotation) (((int) _dragItem.Rotation + 3) % 4);
        // }
    }

    private void ClearSelectedCellHighlight()
    {
        if (_selectedPanel == null || _selectedCells == null) return;

        foreach (var v in _selectedCells)
        {
            if (_selectedPanel.CellInstances.ContainsKey(v))
                _selectedPanel.CellInstances[v].Icon.color = _selectedPanel.GetColor(v);
        }
    }

    private void ApplySelectedCellHighlight()
    {
        if (_selectedPanel == null || _selectedCells == null) return;

        foreach (var v in _selectedCells)
        {
            if (_selectedPanel.CellInstances.ContainsKey(v))
                _selectedPanel.CellInstances[v].Icon.color = _selectedPanel.GetColor(v, true);
        }
    }

    private int2[] GetSelectedCells(ItemInstance item, int2 position)
    {
        var typedItem = FindTypedInventoryItem(item);
        if (typedItem != null && typedItem.ShapeCells.Count > 0)
        {
            return typedItem.ShapeCells
                .Select(cell => RotateTypedShapeCell(cell, typedItem, item.Rotation) + position)
                .ToArray();
        }

        return Array.Empty<int2>();
    }

    private static AetheriaRuntimeCatalogItem FindTypedInventoryItem(ItemInstance item)
    {
        return ActionGameManager.RuntimeCatalog?.FindItem(item?.ItemKey ?? "");
    }

    private static int2 RotateTypedShapeCell(
        AetheriaRuntimeShapeCell cell,
        AetheriaRuntimeCatalogItem item,
        ItemRotation rotation)
    {
        return rotation switch
        {
            ItemRotation.Clockwise => new int2(cell.Y, item.ShapeWidth - 1 - cell.X),
            ItemRotation.Reversed => new int2(item.ShapeWidth - 1 - cell.X, item.ShapeHeight - 1 - cell.Y),
            ItemRotation.CounterClockwise => new int2(item.ShapeHeight - 1 - cell.Y, cell.X),
            _ => new int2(cell.X, cell.Y)
        };
    }

    private void OnDestroy()
    {
        if (_shipSettingsSurfaceDocument != null)
        {
            AetheriaEveUnitySurfaceHost.DestroyDocument(_shipSettingsSurfaceDocument);
            _shipSettingsSurfaceDocument = null;
        }

        if (_cargoItemDetailsSurfaceDocument != null)
        {
            AetheriaEveUnitySurfaceHost.DestroyDocument(_cargoItemDetailsSurfaceDocument);
            _cargoItemDetailsSurfaceDocument = null;
        }

        if (_equippedItemDetailsSurfaceDocument != null)
        {
            AetheriaEveUnitySurfaceHost.DestroyDocument(_equippedItemDetailsSurfaceDocument);
            _equippedItemDetailsSurfaceDocument = null;
        }
    }
}

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
using GameCult.Mesh;
using UnityEngine;
using UniRx;
using UniRx.Triggers;
using Unity.Mathematics;
using UnityEngine.UIElements;

public class InventoryMenu : MonoBehaviour
{
    public InventoryPanel[] InventoryPanels;
    public ConfirmationDialog Dialog;
    // public ClickCatcher Background;

    private int2 _selectedPosition;
    private InventoryPanel _selectedPanel;
    private ItemInstance _selectedItem;
    private EquippedItem _selectedEquippedItem;
    private int2[] _selectedCells;
    private AetheriaRuntimeCurrentEntityDocument _shipSettingsCurrentEntity;
    private UIDocument _shipSettingsSurfaceDocument;
    private UIDocument _cargoItemDetailsSurfaceDocument;
    private UIDocument _equippedItemDetailsSurfaceDocument;
    private CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> _catalog;
    private CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument> _playerSettings;
    private CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument> _currentEntity;
    private CultMeshReactiveDocument<AetheriaRuntimeStationRefitDocument> _stationRefit;
    private int _inventoryEntityIndex = -1;
    private CultMeshReactiveDocument<AetheriaRuntimeInventoryDocument> _inventory;
    private AetheriaUnityActionBarPresentation _actionBarPresentation;
    private AetheriaUnityObservedEntityIndex _observedEntityIndex;
    private AetheriaUnityObservedDockingIndex _observedDockingIndex;
    private readonly AetheriaEveUnitySurfaceChrome _shipSettingsSurfaceChrome = PanelChrome(360f, 420f);
    private readonly AetheriaEveUnitySurfaceChrome _cargoItemDetailsSurfaceChrome = PanelChrome(420f, 520f);
    private readonly AetheriaEveUnitySurfaceChrome _equippedItemDetailsSurfaceChrome = PanelChrome(460f, 560f);
    // private List<IDisposable> _backgroundSubscriptions;

    // private ItemInstance _dragItem;
    // private Transform[] _dragCells;
    // private Vector2[] _dragOffsets;

    public void SetDragSession(AetheriaUnityDragSession dragSession)
    {
        if (InventoryPanels == null)
            return;

        foreach (var panel in InventoryPanels)
            panel?.SetDragSession(dragSession);
    }

    public void SetActionBarPresentation(AetheriaUnityActionBarPresentation actionBarPresentation)
    {
        _actionBarPresentation = actionBarPresentation;
    }

    public void SetObservedEntityIndex(AetheriaUnityObservedEntityIndex observedEntityIndex)
    {
        if (!ReferenceEquals(_observedEntityIndex, observedEntityIndex))
        {
            _observedDockingIndex?.Dispose();
            _observedDockingIndex = null;
        }

        _observedEntityIndex = observedEntityIndex;
    }
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
        var hasCurrentEntity = TryResolveCurrentEntity(out var currentEntity);
        var cargo = TryResolveCurrentDockingBay(out var dockingBay)
            ? dockingBay
            : currentEntity?.CargoBays.FirstOrDefault();
        if (cargo!=null)
            InventoryPanels[0].Display(cargo);
        else InventoryPanels[0].Clear();
        if(hasCurrentEntity)
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
                if (!TryResolveCurrentEntityDocument(out var currentEntity)) return;

                RenderCurrentShipSettingsSurface(currentEntity);
            });
        }
    }

    private void RenderCurrentShipSettingsSurface(AetheriaRuntimeCurrentEntityDocument currentEntity)
    {
        if (currentEntity == null || string.IsNullOrWhiteSpace(currentEntity.EntityKey))
            return;

        HideCargoItemDetailsSurface();
        HideEquippedItemDetailsSurface();
        ClearSelectedItemSelection();
        _shipSettingsCurrentEntity = currentEntity;

        _shipSettingsSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _shipSettingsSurfaceDocument,
            "Aetheria Inventory Ship Settings Surface",
            AetheriaRuntimeShipSettingsSurfaceBuilder.Build(
                currentEntity?.Entity?.DisplayName ?? "",
                (float)(currentEntity?.ShutdownPerformance ?? 0),
                FormatValue),
            HandleCurrentShipSettingsSurfaceCommand,
            _shipSettingsSurfaceChrome);
    }

    private void HandleCurrentShipSettingsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        var currentEntity = _shipSettingsCurrentEntity;
        if (currentEntity == null ||
            string.IsNullOrWhiteSpace(currentEntity.EntityKey) ||
            !TryResolveCurrentEntityDocument(out var latestCurrentEntity) ||
            !string.Equals(latestCurrentEntity.EntityKey, currentEntity.EntityKey, StringComparison.Ordinal))
        {
            HideCurrentShipSettingsSurface();
            return;
        }

        _shipSettingsCurrentEntity = latestCurrentEntity;

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
                RequestEntityShutdownPerformance(
                    latestCurrentEntity.EntityKey,
                    AetheriaRuntimeShipSettingsSurfaceCommands.ResolveShutdownPerformance(
                        command.Kind,
                        (float)latestCurrentEntity.ShutdownPerformance,
                        ResolveDefaultShutdownPerformance()));
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
        _shipSettingsCurrentEntity = null;
        if (_shipSettingsSurfaceDocument == null)
            return;

        AetheriaEveUnitySurfaceHost.Hide(_shipSettingsSurfaceDocument);
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
            AetheriaRuntimeCargoItemDetailsSurfaceBuilder.Build(
                typedItem,
                ComposeCargoItemObservation(item),
                ResolveManufacturerName(typedItem),
                item is EquippableItem equippableItem ? FormatItemTier(typedItem, equippableItem) : "",
                FormatValue,
                FormatTemperature),
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
            AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.Build(
                typedItem,
                ComposeEquippedItemObservation(item),
                BuildEquippedItemTitle(item, typedItem),
                ResolveManufacturerName(typedItem),
                FormatValue,
                FormatTemperature,
                ComposeEquippedItemTemperatureControls(item).ToArray(),
                item.GetBehavior<Weapon>() != null
                    ? ComposeEquippedItemWeaponGroupControls(item).ToArray()
                    : Array.Empty<AetheriaRuntimeEquippedItemControl>()),
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
                RequestEquippedItemOverrideShutdown(item, !item.EquippableItem.OverrideShutdown);
                return;
            case AetheriaRuntimeEquippedItemDetailsCommandKind.SetTargetTemperature:
                if (command.BehaviorIndex >= 0 &&
                    command.BehaviorIndex < (item.Behaviors?.Length ?? 0) &&
                    item.Behaviors[command.BehaviorIndex] is Thermotoggle thermotoggle &&
                    thermotoggle.Adjustable)
                {
                    RequestThermotoggleTargetTemperature(item, command.BehaviorIndex, command.TargetTemperature);
                    return;
                }

                Debug.LogWarning("Unable to apply equipped-item target temperature command.");
                RenderEquippedItemDetailsSurface(item);
                return;
            case AetheriaRuntimeEquippedItemDetailsCommandKind.ToggleWeaponGroup:
                if (command.GroupIndex >= 0)
                {
                    var assigned = !IsWeaponGroupAssigned(item, command.GroupIndex);
                    RequestWeaponGroupMembership(item, command.GroupIndex, assigned);
                    return;
                }

                Debug.LogWarning("Unable to submit equipped-item weapon group membership request.");
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

    private static AetheriaRuntimeEquippedItemObservation ComposeEquippedItemObservation(EquippedItem item)
    {
        var equippableItem = item?.EquippableItem;
        return new AetheriaRuntimeEquippedItemObservation(
            equippableItem?.ItemKey ?? "",
            equippableItem?.Quality ?? 1,
            equippableItem?.Durability ?? 1,
            item?.Temperature ?? 0,
            equippableItem != null && equippableItem.OverrideShutdown);
    }

    private IEnumerable<AetheriaRuntimeEquippedItemTemperatureControl> ComposeEquippedItemTemperatureControls(
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

    private IEnumerable<AetheriaRuntimeEquippedItemControl> ComposeEquippedItemWeaponGroupControls(
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

    private static AetheriaRuntimeCargoItemObservation ComposeCargoItemObservation(ItemInstance item)
    {
        var equippableItem = item as EquippableItem;
        return new AetheriaRuntimeCargoItemObservation(
            item?.ItemKey ?? "",
            item is SimpleCommodity simpleCommodity ? simpleCommodity.Quantity : 0,
            equippableItem != null,
            equippableItem?.Quality ?? 1,
            equippableItem?.Durability ?? 1,
            0,
            equippableItem != null && equippableItem.OverrideShutdown);
    }

    private string BuildEquippedItemTitle(EquippedItem item, AetheriaRuntimeCatalogItem typedItem)
    {
        return $"{typedItem?.Name ?? "Unknown Item"} ({FormatItemTier(typedItem, item.EquippableItem)})";
    }

    private string FormatItemTier(AetheriaRuntimeCatalogItem typedItem, EquippableItem item)
    {
        var tradeValue = AetheriaRuntimeDaemonTradeItemQueries.TradeItemValue(
            typedItem,
            ToRuntimeLoadoutItem(item),
            ResolveCatalog()?.TradeValueSettings);
        return tradeValue.HasTier
            ? $"{tradeValue.TierName}{new string('+', tradeValue.Upgrades)}"
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
            RequestCargoItemEquip(origin, destination.DisplayedEntity, equippableItem);
            return;
        }

        if (destination.DisplayedCargo != null)
            RequestCargoItemTransfer(origin, destination.DisplayedCargo, item);
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
            RequestEquippedItemStore(item, destination.DisplayedCargo);
    }

    private void RequestCargoItemTransfer(
        EquippedCargoBay origin,
        EquippedCargoBay destination,
        ItemInstance item)
    {
        if (!TryResolveCargoBay(origin, out var originEntityKey, out var originCargoIndex) ||
            !TryResolveCargoBay(destination, out var destinationEntityKey, out var destinationCargoIndex) ||
            item == null ||
            string.IsNullOrWhiteSpace(item.ItemKey) ||
            !origin.Cargo.TryGetValue(item, out var originPosition) ||
            !TryValidateTypedCargoSlot(originEntityKey, originCargoIndex, item, originPosition, true))
        {
            return;
        }

        var quantity = item is SimpleCommodity commodity ? commodity.Quantity : 1;
        if (quantity <= 0)
            return;

        TrySubmitOperation(
            operations => operations.TransferCargoItem(
                originEntityKey,
                originCargoIndex,
                destinationEntityKey,
                destinationCargoIndex,
                item.ItemKey,
                quantity,
                originPosition.x,
                originPosition.y,
                default,
                default,
                false),
            "cargo transfer");
    }

    private void RequestCargoItemEquip(
        EquippedCargoBay origin,
        Entity destination,
        EquippableItem item)
    {
        if (!TryResolveCargoBay(origin, out var originEntityKey, out var originCargoIndex) ||
            !TryResolveEntityRecordKey(destination, out var destinationEntityKey) ||
            item == null ||
            string.IsNullOrWhiteSpace(item.ItemKey) ||
            !origin.Cargo.TryGetValue(item, out var originPosition) ||
            !TryValidateTypedCargoSlot(originEntityKey, originCargoIndex, item, originPosition, true))
        {
            return;
        }

        TrySubmitOperation(
            operations => operations.EquipItem(
                "cargo",
                originEntityKey,
                originCargoIndex,
                destinationEntityKey,
                item.ItemKey,
                originPosition.x,
                originPosition.y,
                default,
                default,
                false),
            "cargo equip");
    }

    private void RequestEquippedItemStore(
        EquippedItem item,
        EquippedCargoBay destination)
    {
        if (!TryResolveEquippedItem(item, out var originEntityKey, out var equipmentIndex) ||
            !TryResolveCargoBay(destination, out var destinationEntityKey, out var destinationCargoIndex) ||
            item?.EquippableItem == null ||
            !TryValidateTypedEquipmentSlot(originEntityKey, equipmentIndex, item))
        {
            return;
        }

        TrySubmitOperation(
            operations => operations.StoreItem(
                originEntityKey,
                equipmentIndex,
                destinationEntityKey,
                destinationCargoIndex,
                item.EquippableItem.ItemKey,
                default,
                default,
                false),
            "equipment store");
    }

    private void RequestEntityShutdownPerformance(string targetEntityKey, float shutdownPerformance)
    {
        if (string.IsNullOrWhiteSpace(targetEntityKey))
            return;

        TrySubmitOperation(
            operations => operations.SetShutdownPerformance(targetEntityKey, shutdownPerformance),
            "entity shutdown-performance");
    }

    private void RequestEquippedItemOverrideShutdown(EquippedItem item, bool enabled)
    {
        if (!TryResolveEquippedItem(item, out var targetEntityKey, out var equipmentIndex) ||
            item?.EquippableItem == null)
        {
            return;
        }

        TrySubmitOperation(
            operations => operations.SetItemOverrideShutdown(targetEntityKey, equipmentIndex, enabled),
            "equipped-item override-shutdown");
    }

    private void RequestThermotoggleTargetTemperature(
        EquippedItem item,
        int behaviorIndex,
        float targetTemperature)
    {
        if (!TryResolveEquippedItem(item, out var targetEntityKey, out var equipmentIndex) ||
            behaviorIndex < 0 ||
            behaviorIndex >= (item?.Behaviors?.Length ?? 0))
        {
            return;
        }

        TrySubmitOperation(
            operations => operations.SetThermotoggleTargetTemperature(
                targetEntityKey,
                equipmentIndex,
                behaviorIndex,
                targetTemperature),
            "thermotoggle target-temperature");
    }

    private void RequestWeaponGroupMembership(EquippedItem item, int groupIndex, bool assigned)
    {
        if (!TryResolveEquippedItem(item, out var targetEntityKey, out var equipmentIndex) ||
            groupIndex < 0)
        {
            return;
        }

        TrySubmitOperation(
            operations => operations.SetWeaponGroupMembership(
                targetEntityKey,
                equipmentIndex,
                groupIndex,
                assigned),
            "weapon-group membership");
    }

    private bool TryResolveEntityRecordKey(Entity entity, out string recordKey)
    {
        recordKey = "";
        return _observedEntityIndex != null &&
               _observedEntityIndex.TryResolveEntityRecordKey(entity, out recordKey);
    }

    private bool IsCurrentEntity(Entity entity)
    {
        if (entity == null ||
            !TryResolveEntityRecordKey(entity, out var entityKey) ||
            string.IsNullOrWhiteSpace(entityKey) ||
            !TryResolveCurrentEntityKey(out var currentEntityKey))
        {
            return false;
        }

        return string.Equals(entityKey, currentEntityKey, StringComparison.Ordinal);
    }

    private bool TryResolveCurrentDockingBayRow(out AetheriaRuntimeStationDockingBayRow dockingBay)
    {
        dockingBay = null;
        var refit = ResolveStationRefitDocument();
        if (refit?.IsDocked != true || refit.DockingBayIndex < 0)
            return false;

        dockingBay = (refit.DockingBays ?? Array.Empty<AetheriaRuntimeStationDockingBayRow>())
            .FirstOrDefault(row => row != null && row.DockingBayIndex == refit.DockingBayIndex);
        return dockingBay != null;
    }

    private bool TryResolveCurrentEntityKey(out string currentEntityKey)
    {
        currentEntityKey = ResolveCurrentEntity()?.EntityKey ?? "";
        return !string.IsNullOrWhiteSpace(currentEntityKey);
    }

    private bool TryResolveCurrentEntityDocument(out AetheriaRuntimeCurrentEntityDocument currentEntity)
    {
        currentEntity = ResolveCurrentEntity();
        if (currentEntity == null)
        {
            Debug.LogWarning("Failed to read Aetheria current entity for inventory ship settings.");
            return false;
        }

        return true;
    }

    private AetheriaRuntimeStationRefitDocument ResolveStationRefit()
    {
        return ResolveStationRefitDocument();
    }

    private bool TryResolveCurrentEntity(out Entity currentEntity)
    {
        currentEntity = null;
        return TryResolveObservedDockingIndex(out var dockingIndex) &&
               dockingIndex.TryResolveCurrentEntity(out currentEntity);
    }

    private bool TryResolveCurrentDockingBay(out EquippedCargoBay dockingBay)
    {
        dockingBay = null;
        if (!TryResolveObservedDockingIndex(out var dockingIndex) ||
            !dockingIndex.TryResolveCurrentDockingBay(out var resolvedDockingBay))
        {
            return false;
        }

        dockingBay = resolvedDockingBay;
        return dockingBay != null;
    }

    private bool TryResolveObservedDockingIndex(out AetheriaUnityObservedDockingIndex dockingIndex)
    {
        dockingIndex = null;
        if (_observedEntityIndex == null)
            return false;

        dockingIndex = _observedDockingIndex ??= new AetheriaUnityObservedDockingIndex(_observedEntityIndex);
        return true;
    }

    private bool TryResolveCargoBay(EquippedCargoBay cargoBay, out string entityKey, out int cargoIndex)
    {
        return TryResolveEquippedItem(cargoBay, out entityKey, out cargoIndex);
    }

    private bool TryResolveEquippedItem(EquippedItem item, out string entityKey, out int equipmentIndex)
    {
        entityKey = "";
        equipmentIndex = -1;
        var entity = item?.Entity;
        if (entity?.Equipment == null ||
            !TryResolveEntityRecordKey(entity, out entityKey))
        {
            return false;
        }

        equipmentIndex = entity.Equipment.IndexOf(item);
        return equipmentIndex >= 0;
    }

    private bool TryResolveTypedInventoryRows(
        string entityKey,
        out IReadOnlyList<AetheriaRuntimeRtsInventoryItem> equipment,
        out IReadOnlyList<AetheriaRuntimeRtsInventoryItem> cargo)
    {
        equipment = Array.Empty<AetheriaRuntimeRtsInventoryItem>();
        cargo = Array.Empty<AetheriaRuntimeRtsInventoryItem>();
        if (string.IsNullOrWhiteSpace(entityKey))
            return false;

        try
        {
            var current = ResolveCurrentEntity();
            if (current != null && string.Equals(current.EntityKey, entityKey, StringComparison.Ordinal))
            {
                equipment = current.Equipment ?? Array.Empty<AetheriaRuntimeRtsInventoryItem>();
                cargo = current.Cargo ?? Array.Empty<AetheriaRuntimeRtsInventoryItem>();
                return true;
            }

            var refit = ResolveStationRefitDocument();
            var entityIndex = -1;
            if (refit != null)
            {
                if (string.Equals(refit.DockParentEntityKey, entityKey, StringComparison.Ordinal))
                    entityIndex = refit.DockParentEntityIndex;
                else
                    entityIndex = (refit.AvailableEntities ?? Array.Empty<AetheriaRuntimeStationRefitEntityOption>())
                        .FirstOrDefault(option => string.Equals(option.EntityKey, entityKey, StringComparison.Ordinal))
                        ?.EntityIndex ?? -1;
            }

            if (entityIndex < 0)
                return false;

            var inventory = ResolveInventory(entityIndex);
            if (inventory == null || !string.Equals(inventory.EntityKey, entityKey, StringComparison.Ordinal))
                return false;

            equipment = inventory.Equipment ?? Array.Empty<AetheriaRuntimeRtsInventoryItem>();
            cargo = inventory.Cargo ?? Array.Empty<AetheriaRuntimeRtsInventoryItem>();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to validate Aetheria typed inventory document for {entityKey}: {ex.Message}");
            return false;
        }
    }

    private bool TryValidateTypedCargoSlot(
        string entityKey,
        int cargoIndex,
        ItemInstance item,
        int2 originPosition,
        bool hasOriginPosition)
    {
        if (item == null ||
            string.IsNullOrWhiteSpace(item.ItemKey) ||
            !TryResolveTypedInventoryRows(entityKey, out _, out var cargo))
        {
            return false;
        }

        return cargo.Any(row =>
            string.Equals(row.Source, "cargo", StringComparison.Ordinal) &&
            row.SourceIndex == cargoIndex &&
            string.Equals(row.ItemKey, item.ItemKey, StringComparison.Ordinal) &&
            (!hasOriginPosition || (row.X == originPosition.x && row.Y == originPosition.y)));
    }

    private bool TryValidateTypedEquipmentSlot(
        string entityKey,
        int equipmentIndex,
        EquippedItem item)
    {
        if (item?.EquippableItem == null ||
            string.IsNullOrWhiteSpace(item.EquippableItem.ItemKey) ||
            !TryResolveTypedInventoryRows(entityKey, out var equipment, out _))
        {
            return false;
        }

        return equipment.Any(row =>
            string.Equals(row.Source, "equipment", StringComparison.Ordinal) &&
            row.SourceIndex == equipmentIndex &&
            row.X == item.Position.x &&
            row.Y == item.Position.y &&
            string.Equals(row.ItemKey, item.EquippableItem.ItemKey, StringComparison.Ordinal));
    }

    private bool TrySubmitOperation(
        Action<AetheriaControl> submit,
        string label)
    {
        if (submit == null)
            return false;

        try
        {
            submit(AetheriaUnityRuntimeClientProvider.Control("unity-inventory-menu"));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon inventory menu {label} operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private void ClearClientCaches()
    {
        _catalog?.Dispose();
        _playerSettings?.Dispose();
        _currentEntity?.Dispose();
        _stationRefit?.Dispose();
        _observedDockingIndex?.Dispose();
        _inventory?.Dispose();
        _catalog = null;
        _playerSettings = null;
        _currentEntity = null;
        _stationRefit = null;
        _observedDockingIndex = null;
        _inventory = null;
        _inventoryEntityIndex = -1;
    }

    private AetheriaRuntimeCurrentEntityDocument ResolveCurrentEntity()
    {
        if (_currentEntity != null)
            return _currentEntity.Current;

        try
        {
            _currentEntity = AetheriaUnityRuntimeClientProvider
                .ReactiveCurrentEntity("unity-inventory-menu");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria current entity for inventory menu: {ex.Message}");
        }

        return _currentEntity?.Current;
    }

    private AetheriaRuntimeStationRefitDocument ResolveStationRefitDocument()
    {
        if (_stationRefit != null)
            return _stationRefit.Current;

        try
        {
            _stationRefit = AetheriaUnityRuntimeClientProvider
                .ReactiveStationRefit("unity-inventory-menu");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria station refit for inventory menu: {ex.Message}");
        }

        return _stationRefit?.Current;
    }

    private AetheriaRuntimeInventoryDocument ResolveInventory(int entityIndex)
    {
        if (_inventory != null && _inventoryEntityIndex == entityIndex)
            return _inventory.Current;

        try
        {
            var nextInventory = AetheriaUnityRuntimeClientProvider
                .RuntimeState("unity-inventory-menu")
                .Reactive<AetheriaRuntimeInventoryDocument>(entityIndex);
            _inventory?.Dispose();
            _inventoryEntityIndex = entityIndex;
            _inventory = nextInventory;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria typed inventory document for entity {entityIndex}: {ex.Message}");
        }

        return _inventory?.Current;
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

    private AetheriaRuntimeCatalogItem FindTypedInventoryItem(ItemInstance item)
    {
        return ResolveCatalog()?.FindItem(item, x => x.ItemKey);
    }

    private AetheriaRuntimeCatalogSnapshot ResolveCatalog()
    {
        if (_catalog != null)
            return _catalog.Current;

        try
        {
            _catalog = AetheriaUnityRuntimeClientProvider
                .Reactive<AetheriaRuntimeCatalogSnapshot>("unity-inventory-menu");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria runtime catalog for inventory menu: {ex.Message}");
        }

        return _catalog?.Current;
    }

    private AetheriaRuntimePlayerSettingsDocument ResolvePlayerSettings()
    {
        if (_playerSettings != null)
            return _playerSettings.Current;

        try
        {
            _playerSettings = AetheriaUnityRuntimeClientProvider
                .Reactive<AetheriaRuntimePlayerSettingsDocument>("unity-inventory-menu");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria player settings for inventory menu: {ex.Message}");
        }

        return _playerSettings?.Current;
    }

    private float ResolveDefaultShutdownPerformance()
    {
        var value = ResolvePlayerSettings()?.DefaultShutdownPerformance ?? 0.25;
        return (float)(value <= 0 ? 0.25 : value);
    }

    private string ResolveManufacturerName(AetheriaRuntimeCatalogItem item)
    {
        return ResolveCatalog()?.GetManufacturer(item)?.Name ?? "GameCult";
    }

    private string FormatValue(float value)
    {
        var settings = ResolvePlayerSettings();
        var significantDigits = settings?.SignificantDigits ?? 3;
        var magnitude = value == 0.0f ? 0 : (int)Math.Floor(Math.Log10(Math.Abs(value))) + 1;
        var digits = significantDigits - magnitude;
        if (digits < 0)
            digits = 0;

        var formatted = value.ToString($"N{digits}", CultureInfo.CurrentCulture);
        var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var decimalSeparator = Convert.ToChar(separator);
        return formatted.Contains(separator)
            ? formatted.TrimEnd('0').TrimEnd(decimalSeparator)
            : formatted;
    }

    private string FormatTemperature(float value)
    {
        var unit = ResolvePlayerSettings()?.TemperatureUnit ?? nameof(TemperatureUnit.Celsius);
        if (string.Equals(unit, nameof(TemperatureUnit.Kelvin), StringComparison.OrdinalIgnoreCase))
            return $"{FormatValue(value)} K";
        if (string.Equals(unit, nameof(TemperatureUnit.Fahrenheit), StringComparison.OrdinalIgnoreCase))
            return $"{FormatValue(value * (9f / 5) - 459.67f)} F";

        return $"{FormatValue(value - 273.15f)} C";
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
        ClearClientCaches();

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

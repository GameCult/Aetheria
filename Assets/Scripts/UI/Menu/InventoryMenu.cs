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
    private const float ShutdownThresholdStep = 0.05f;

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
        var cargo = GameManager.DockingBay ?? GameManager.CurrentEntity.CargoBays.FirstOrDefault();
        if (cargo!=null)
            InventoryPanels[0].Display(cargo);
        else InventoryPanels[0].Clear();
        if(GameManager.CurrentEntity != null)
            InventoryPanels[1].Display(GameManager.CurrentEntity);
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
                if (GameManager.CurrentEntity == null) return; // Only show ship settings if there's a ship, duh!

                RenderCurrentShipSettingsSurface(GameManager.CurrentEntity);
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
            AetheriaRuntimeShipSettingsSurfaceBuilder.Build(ProjectCurrentShipSettingsSurfaceState(entity)),
            HandleCurrentShipSettingsSurfaceCommand,
            _shipSettingsSurfaceChrome);
    }

    private void HandleCurrentShipSettingsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        var entity = GameManager.CurrentEntity;
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
                GameManager.RequestEntityShutdownPerformance(
                    entity,
                    Mathf.Clamp01(entity.Settings.ShutdownPerformance - ShutdownThresholdStep));
                RenderCurrentShipSettingsSurface(entity);
                return;
            case AetheriaRuntimeShipSettingsCommandKind.IncrementShutdownThreshold:
                GameManager.RequestEntityShutdownPerformance(
                    entity,
                    Mathf.Clamp01(entity.Settings.ShutdownPerformance + ShutdownThresholdStep));
                RenderCurrentShipSettingsSurface(entity);
                return;
            case AetheriaRuntimeShipSettingsCommandKind.ResetShutdownThreshold:
                GameManager.RequestEntityShutdownPerformance(
                    entity,
                    Mathf.Clamp01(GameManager.Settings.GameplaySettings.DefaultShutdownPerformance));
                RenderCurrentShipSettingsSurface(entity);
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

    private static AetheriaRuntimeShipSettingsSurfaceState ProjectCurrentShipSettingsSurfaceState(Entity entity)
    {
        return new AetheriaRuntimeShipSettingsSurfaceState(
            entity?.Name ?? "",
            entity == null
                ? ""
                : ActionGameManager.RuntimePlayerSettings.Format(entity.Settings.ShutdownPerformance),
            DateTime.UtcNow.ToString("O"));
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
            AetheriaRuntimeCargoItemDetailsSurfaceBuilder.Build(ProjectCargoItemDetailsSurfaceState(item, typedItem)),
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
            AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.Build(ProjectEquippedItemDetailsSurfaceState(item, typedItem)),
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
                RenderEquippedItemDetailsSurface(item);
                return;
            case AetheriaRuntimeEquippedItemDetailsCommandKind.SetTargetTemperature:
                if (command.BehaviorIndex >= 0 &&
                    command.BehaviorIndex < (item.Behaviors?.Length ?? 0) &&
                    item.Behaviors[command.BehaviorIndex] is Thermotoggle thermotoggle &&
                    thermotoggle.Adjustable &&
                    GameManager != null)
                {
                    GameManager.RequestThermotoggleTargetTemperature(thermotoggle, command.TargetTemperature);
                    RenderEquippedItemDetailsSurface(item);
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
                    command.SlotIndex >= 0 &&
                    GameManager.RequestWeaponGroupActionBarBinding(command.SlotIndex, command.GroupIndex))
                {
                    RenderEquippedItemDetailsSurface(item);
                    return;
                }

                Debug.LogWarning("Unable to bind equipped-item weapon group to action bar.");
                return;
            case AetheriaRuntimeEquippedItemDetailsCommandKind.ClearActionBarBinding:
                if (command.SlotIndex >= 0 &&
                    GameManager.RequestClearActionBarBinding(command.SlotIndex))
                {
                    RenderEquippedItemDetailsSurface(item);
                    return;
                }

                Debug.LogWarning("Unable to clear equipped-item action bar binding.");
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

    private AetheriaRuntimeEquippedItemDetailsSurfaceState ProjectEquippedItemDetailsSurfaceState(
        EquippedItem item,
        AetheriaRuntimeCatalogItem typedItem)
    {
        var durability = item.EquippableItem.Durability < .01f
            ? "Item Destroyed!"
            : $"{(int)(item.EquippableItem.Durability / GetMaxDurability(typedItem, item.EquippableItem) * 100)}%";
        var hasWeapon = item.GetBehavior<Weapon>() != null;

        return new AetheriaRuntimeEquippedItemDetailsSurfaceState(
            BuildEquippedItemTitle(item, typedItem),
            typedItem.Description ?? "",
            ActionGameManager.RuntimeCatalog?.GetManufacturer(typedItem)?.Name ?? "GameCult",
            ActionGameManager.RuntimePlayerSettings.Format((float)typedItem.Mass),
            durability,
            ActionGameManager.RuntimePlayerSettings.FormatTemperature(item.Temperature),
            FormatTemperatureRange(typedItem),
            item.EquippableItem.OverrideShutdown ? "Enabled" : "Disabled",
            item.EquippableItem.OverrideShutdown ? "Disable Override" : "Enable Override",
            ProjectEquippedItemTemperatureControls(item).ToArray(),
            ProjectEquippedItemBehaviorSections(typedItem, item.EquippableItem).ToArray(),
            hasWeapon ? ProjectEquippedItemWeaponGroupControls(item).ToArray() : Array.Empty<AetheriaRuntimeEquippedItemControl>(),
            hasWeapon ? ProjectEquippedItemActionBarSlots(item).ToArray() : Array.Empty<AetheriaRuntimeEquippedItemActionBarSlot>(),
            DateTime.UtcNow.ToString("O"));
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

    private AetheriaRuntimeCargoItemDetailsSurfaceState ProjectCargoItemDetailsSurfaceState(
        ItemInstance item,
        AetheriaRuntimeCatalogItem typedItem)
    {
        var quantity = item is SimpleCommodity simpleCommodity ? simpleCommodity.Quantity : 0;
        var tier = "";
        var durability = "";
        var thermalRange = "";
        var behaviorSections = Array.Empty<AetheriaRuntimeCargoItemSection>();

        if (item is EquippableItem equippableItem)
        {
            var (itemTier, upgrades) = GameManager.ItemManager.GetTier(equippableItem);
            tier = $"{itemTier.Name}{new string('+', upgrades)}";
            durability = $"{(int)(equippableItem.Durability / GetMaxDurability(typedItem, equippableItem) * 100)}%";
            thermalRange = FormatTemperatureRange(typedItem);
            behaviorSections = ProjectCargoItemBehaviorSections(typedItem, equippableItem).ToArray();
        }

        return new AetheriaRuntimeCargoItemDetailsSurfaceState(
            typedItem.Name,
            typedItem.Description ?? "",
            ActionGameManager.RuntimeCatalog?.GetManufacturer(typedItem)?.Name ?? "GameCult",
            ActionGameManager.RuntimePlayerSettings.Format(GetCargoItemMass(item, typedItem)),
            typedItem.Price,
            quantity,
            tier,
            durability,
            thermalRange,
            behaviorSections,
            DateTime.UtcNow.ToString("O"));
    }

    private IEnumerable<AetheriaRuntimeCargoItemSection> ProjectCargoItemBehaviorSections(
        AetheriaRuntimeCatalogItem typedItem,
        EquippableItem equippableItem)
    {
        foreach (var behavior in typedItem.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>())
        {
            if (string.Equals(behavior.Kind, AetheriaRuntimeBehaviorKinds.StatModifier, StringComparison.Ordinal))
            {
                var statReference = AetheriaRuntimeBehaviorValueReader.ReadStatReference(FindTypedBehaviorField(behavior, 1)?.Value);
                var modifier = AetheriaRuntimeBehaviorValueReader.ReadPerformanceStat(FindTypedBehaviorField(behavior, 2)?.Value);
                var modifierType = AetheriaRuntimeBehaviorValueReader.ReadEnum(
                    FindTypedBehaviorField(behavior, 3)?.Value,
                    StatModifierType.Constant);
                yield return new AetheriaRuntimeCargoItemSection(
                    $"{AetheriaRuntimeCargoItemDetailsSurfaceBuilder.SurfaceId}.behavior.{behavior.Kind}.stat_modifier",
                    "Stat Modifier",
                    new[]
                    {
                        new AetheriaRuntimeCargoItemMetric(
                            $"{AetheriaRuntimeCargoItemDetailsSurfaceBuilder.SurfaceId}.behavior.{behavior.Kind}.target",
                            $"{statReference.Target.SplitCamelCase()}:{statReference.Stat.SplitCamelCase()}",
                            $"{(modifierType == StatModifierType.Constant ? "+" : "x")}{ActionGameManager.RuntimePlayerSettings.Format(GameManager.ItemManager.Evaluate(modifier, equippableItem))}")
                    });
                continue;
            }

            var metadata = AetheriaRuntimeBehaviorMetadataCatalog.Get(behavior.Kind);
            if (metadata == null)
                continue;

            var fields = metadata.DisplayFields
                .Select(field => ProjectCargoItemBehaviorMetric(behavior, field, equippableItem))
                .Where(metric => metric != null)
                .ToArray();

            if (fields.Length == 0)
                continue;

            yield return new AetheriaRuntimeCargoItemSection(
                $"{AetheriaRuntimeCargoItemDetailsSurfaceBuilder.SurfaceId}.behavior.{behavior.Kind}",
                behavior.Kind.FormatTypeName(),
                fields);
        }
    }

    private AetheriaRuntimeCargoItemMetric ProjectCargoItemBehaviorMetric(
        AetheriaRuntimeBehaviorPayload behavior,
        AetheriaRuntimeBehaviorFieldMetadata field,
        EquippableItem equippableItem)
    {
        var payloadField = FindTypedBehaviorField(behavior, field.Key);
        if (payloadField == null)
            return null;

        string value;
        switch (field.ValueKind)
        {
            case AetheriaRuntimeBehaviorFieldValueKind.Number:
                value = ActionGameManager.RuntimePlayerSettings.Format((float)payloadField.Value.NumberValue);
                break;
            case AetheriaRuntimeBehaviorFieldValueKind.Temperature:
                value = ActionGameManager.RuntimePlayerSettings.FormatTemperature((float)payloadField.Value.NumberValue);
                break;
            case AetheriaRuntimeBehaviorFieldValueKind.Integer:
                value = ((int)payloadField.Value.NumberValue).ToString();
                break;
            case AetheriaRuntimeBehaviorFieldValueKind.PerformanceStat:
                value = ActionGameManager.RuntimePlayerSettings.Format(GameManager.ItemManager.Evaluate(AetheriaRuntimeBehaviorValueReader.ReadPerformanceStat(payloadField.Value), equippableItem));
                break;
            default:
                return null;
        }

        return new AetheriaRuntimeCargoItemMetric(
            $"{AetheriaRuntimeCargoItemDetailsSurfaceBuilder.SurfaceId}.behavior.{behavior.Kind}.{field.Key}",
            field.Name.SplitCamelCase(),
            value);
    }

    private IEnumerable<AetheriaRuntimeEquippedItemSection> ProjectEquippedItemBehaviorSections(
        AetheriaRuntimeCatalogItem typedItem,
        EquippableItem equippableItem)
    {
        foreach (var behavior in typedItem.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>())
        {
            if (string.Equals(behavior.Kind, AetheriaRuntimeBehaviorKinds.StatModifier, StringComparison.Ordinal))
            {
                var statReference = AetheriaRuntimeBehaviorValueReader.ReadStatReference(FindTypedBehaviorField(behavior, 1)?.Value);
                var modifier = AetheriaRuntimeBehaviorValueReader.ReadPerformanceStat(FindTypedBehaviorField(behavior, 2)?.Value);
                var modifierType = AetheriaRuntimeBehaviorValueReader.ReadEnum(
                    FindTypedBehaviorField(behavior, 3)?.Value,
                    StatModifierType.Constant);
                yield return new AetheriaRuntimeEquippedItemSection(
                    $"{AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.SurfaceId}.behavior.{behavior.Kind}.stat_modifier",
                    "Stat Modifier",
                    new[]
                    {
                        new AetheriaRuntimeEquippedItemMetric(
                            $"{AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.SurfaceId}.behavior.{behavior.Kind}.target",
                            $"{statReference.Target.SplitCamelCase()}:{statReference.Stat.SplitCamelCase()}",
                            $"{(modifierType == StatModifierType.Constant ? "+" : "x")}{ActionGameManager.RuntimePlayerSettings.Format(GameManager.ItemManager.Evaluate(modifier, equippableItem))}")
                    });
                continue;
            }

            var metadata = AetheriaRuntimeBehaviorMetadataCatalog.Get(behavior.Kind);
            if (metadata == null)
                continue;

            var fields = metadata.DisplayFields
                .Select(field => ProjectEquippedItemBehaviorMetric(behavior, field, equippableItem))
                .Where(metric => metric != null)
                .ToArray();

            if (fields.Length == 0)
                continue;

            yield return new AetheriaRuntimeEquippedItemSection(
                $"{AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.SurfaceId}.behavior.{behavior.Kind}",
                behavior.Kind.FormatTypeName(),
                fields);
        }
    }

    private AetheriaRuntimeEquippedItemMetric ProjectEquippedItemBehaviorMetric(
        AetheriaRuntimeBehaviorPayload behavior,
        AetheriaRuntimeBehaviorFieldMetadata field,
        EquippableItem equippableItem)
    {
        var payloadField = FindTypedBehaviorField(behavior, field.Key);
        if (payloadField == null)
            return null;

        string value;
        switch (field.ValueKind)
        {
            case AetheriaRuntimeBehaviorFieldValueKind.Number:
                value = ActionGameManager.RuntimePlayerSettings.Format((float)payloadField.Value.NumberValue);
                break;
            case AetheriaRuntimeBehaviorFieldValueKind.Temperature:
                value = ActionGameManager.RuntimePlayerSettings.FormatTemperature((float)payloadField.Value.NumberValue);
                break;
            case AetheriaRuntimeBehaviorFieldValueKind.Integer:
                value = ((int)payloadField.Value.NumberValue).ToString();
                break;
            case AetheriaRuntimeBehaviorFieldValueKind.PerformanceStat:
                value = ActionGameManager.RuntimePlayerSettings.Format(GameManager.ItemManager.Evaluate(AetheriaRuntimeBehaviorValueReader.ReadPerformanceStat(payloadField.Value), equippableItem));
                break;
            default:
                return null;
        }

        return new AetheriaRuntimeEquippedItemMetric(
            $"{AetheriaRuntimeEquippedItemDetailsSurfaceBuilder.SurfaceId}.behavior.{behavior.Kind}.{field.Key}",
            field.Name.SplitCamelCase(),
            value);
    }

private string BuildEquippedItemTitle(EquippedItem item, AetheriaRuntimeCatalogItem typedItem)
    {
        var (tier, upgrades) = GameManager.ItemManager.GetTier(item.EquippableItem);
        return $"{typedItem?.Name ?? "Unknown Item"} ({tier.Name}{new string('+', upgrades)})";
    }

    private static bool IsWeaponGroupAssigned(EquippedItem item, int groupIndex)
    {
        return item?.Entity?.WeaponGroups != null &&
               groupIndex >= 0 &&
               groupIndex < item.Entity.WeaponGroups.Length &&
               item.Entity.WeaponGroups[groupIndex].items.Contains(item);
    }

    private bool RequestCargoItemTransfer(EquippedCargoBay origin, InventoryPanel destination, ItemInstance item)
    {
        if (destination.DisplayedEntity != null && item is EquippableItem equippableItem)
            return GameManager.RequestCargoItemEquip(origin, destination.DisplayedEntity, equippableItem);

        if (destination.DisplayedCargo != null)
            return GameManager.RequestCargoItemTransfer(origin, destination.DisplayedCargo, item);

        return false;
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

    private bool RequestEquippedItemTransfer(Entity origin, EquippedItem item, InventoryPanel destination)
    {
        if (destination.DisplayedCargo != null)
            return GameManager.RequestEquippedItemStore(origin, item, destination.DisplayedCargo);

        return false;
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

    private static float GetCargoItemMass(ItemInstance item, AetheriaRuntimeCatalogItem typedItem)
    {
        if (typedItem == null)
            return 0f;

        return item is SimpleCommodity simpleCommodity
            ? (float)typedItem.Mass * simpleCommodity.Quantity
            : (float)typedItem.Mass;
    }

    private static float GetMaxDurability(AetheriaRuntimeCatalogItem typedItem, EquippableItem item)
    {
        if (typedItem != null && typedItem.Durability > 0)
            return (float)typedItem.Durability;

        return Math.Max(item?.Durability ?? 1f, 1f);
    }

    private static string FormatTemperatureRange(AetheriaRuntimeCatalogItem item)
    {
        if (item.MaximumTemperature > item.MinimumTemperature)
        {
            return
                $"{ActionGameManager.RuntimePlayerSettings.FormatTemperature((float)item.MinimumTemperature)} to " +
                $"{ActionGameManager.RuntimePlayerSettings.FormatTemperature((float)item.MaximumTemperature)}";
        }

        return "No typed thermal range";
    }

    private static AetheriaRuntimeBehaviorField FindTypedBehaviorField(AetheriaRuntimeBehaviorPayload behavior, int? key)
    {
        return key == null
            ? null
            : behavior.Fields.FirstOrDefault(field => field.Key == key.Value);
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

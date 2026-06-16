/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Unity;
using GameCult.Eve.Surface;
using GameCult.Eve.UnityUIToolkit;
using UnityEngine;
using UniRx;
using UniRx.Triggers;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class InventoryMenu : MonoBehaviour
{
    private const string ShipSettingsSurfaceType = "surface-state";
    private const string ShipSettingsSurfaceSchema = "gamecult.eve.surface.v1";
    private const string ShipSettingsSurfaceProviderId = "aetheria";
    private const string ShipSettingsSurfaceProviderKind = "inventory.menu";
    private const string ShipSettingsSurfaceId = "aetheria.inventory.current_ship_settings";
    private const string DecrementShutdownThresholdCommand = "aetheria.inventory.current_ship_settings.shutdown.decrement";
    private const string IncrementShutdownThresholdCommand = "aetheria.inventory.current_ship_settings.shutdown.increment";
    private const string ResetShutdownThresholdCommand = "aetheria.inventory.current_ship_settings.shutdown.reset";
    private const string CloseShipSettingsCommand = "aetheria.inventory.current_ship_settings.close";
    private const string CargoItemDetailsSurfaceId = "aetheria.inventory.cargo_item_details";
    private const string CloseCargoItemDetailsCommand = "aetheria.inventory.cargo_item_details.close";
    private const float ShutdownThresholdStep = 0.05f;

    public InventoryPanel[] InventoryPanels;
    public PropertiesPanel PropertiesPanel;
    public ActionGameManager GameManager;
    public RectTransform DragParent;
    public ConfirmationDialog Dialog;
    // public ClickCatcher Background;

    private int _currentPanel = -1;
    
    private int2 _selectedPosition;
    private InventoryPanel _selectedPanel;
    private ItemInstance _selectedItem;
    private int2[] _selectedCells;
    private UIDocument _shipSettingsSurfaceDocument;
    private UIDocument _cargoItemDetailsSurfaceDocument;
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
        // Background.gameObject.SetActive(false);
    }

    void Start()
    {
        PropertiesPanel.GameManager = GameManager;
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
                            ClearSelectedItemSelection();
                            var otherPanel = panel == InventoryPanels[0] ? InventoryPanels[1] : InventoryPanels[0];
                            if (CommitCargoItemTransfer(cargoEvent.CargoBay, otherPanel, item))
                            {
                                // TODO: SFX: Equip
                                panel.RefreshCells();
                                otherPanel.RefreshCells();
                            }
                            // else
                            // TODO: SFX: Fail
                        }
                        else
                        {
                            HideCurrentShipSettingsSurface();
                            HideCargoItemDetailsSurface();
                            PropertiesPanel.gameObject.SetActive(false);
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
                            ClearSelectedItemSelection();
                            var otherPanel = panel == InventoryPanels[0] ? InventoryPanels[1] : InventoryPanels[0];
                            if (CommitEquippedItemTransfer(entityEvent.Entity, item, otherPanel))
                            {
                                // TODO: SFX: Unequip
                                panel.RefreshCells();
                                otherPanel.RefreshCells();
                                // else
                                // TODO: SFX: Fail
                            }
                        }
                        else
                        {
                            HideCurrentShipSettingsSurface();
                            HideCargoItemDetailsSurface();
                            PropertiesPanel.gameObject.SetActive(true);
                            ClearSelectedItemSelection();

                            PropertiesPanel.Inspect(item);
                            _selectedPanel = panel;
                            _selectedPosition = item.Position;
                            _selectedItem = item.EquippableItem;
                            _selectedCells = GetSelectedCells(item.EquippableItem, _selectedPosition);
                            ApplySelectedCellHighlight();
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
        PropertiesPanel.gameObject.SetActive(false);
        ClearSelectedItemSelection();

        var document = ResolveShipSettingsSurfaceDocument();
        document.gameObject.SetActive(true);

        var root = document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1;
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.top = 0;
        root.style.right = 0;
        root.style.bottom = 0;
        root.style.alignItems = Align.FlexStart;
        root.style.justifyContent = Justify.FlexStart;
        root.pickingMode = PickingMode.Ignore;

        var shell = new VisualElement();
        shell.style.width = 360;
        shell.style.maxWidth = 420;
        shell.style.backgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.94f);
        shell.style.borderTopLeftRadius = 8;
        shell.style.borderTopRightRadius = 8;
        shell.style.borderBottomLeftRadius = 8;
        shell.style.borderBottomRightRadius = 8;
        shell.style.paddingLeft = 18;
        shell.style.paddingRight = 18;
        shell.style.paddingTop = 18;
        shell.style.paddingBottom = 18;
        shell.style.borderLeftWidth = 1;
        shell.style.borderRightWidth = 1;
        shell.style.borderTopWidth = 1;
        shell.style.borderBottomWidth = 1;
        shell.style.borderLeftColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderRightColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderTopColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderBottomColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.pickingMode = PickingMode.Position;
        root.Add(shell);

        var lowerer = new EveUiToolkitSurfaceLowerer();
        shell.Add(lowerer.Lower(BuildCurrentShipSettingsSurfaceDefinition(entity), HandleCurrentShipSettingsSurfaceCommand));
    }

    private void HandleCurrentShipSettingsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        var entity = GameManager.CurrentEntity;
        if (entity?.Settings == null)
        {
            HideCurrentShipSettingsSurface();
            return;
        }

        switch (request.Command)
        {
            case DecrementShutdownThresholdCommand:
                GameManager.CommitEntityShutdownPerformance(
                    entity,
                    Mathf.Clamp01(entity.Settings.ShutdownPerformance - ShutdownThresholdStep));
                RenderCurrentShipSettingsSurface(entity);
                return;
            case IncrementShutdownThresholdCommand:
                GameManager.CommitEntityShutdownPerformance(
                    entity,
                    Mathf.Clamp01(entity.Settings.ShutdownPerformance + ShutdownThresholdStep));
                RenderCurrentShipSettingsSurface(entity);
                return;
            case ResetShutdownThresholdCommand:
                GameManager.CommitEntityShutdownPerformance(
                    entity,
                    Mathf.Clamp01(GameManager.Settings.GameplaySettings.DefaultShutdownPerformance));
                RenderCurrentShipSettingsSurface(entity);
                return;
            case CloseShipSettingsCommand:
                HideCurrentShipSettingsSurface();
                return;
            default:
                Debug.LogWarning($"Unknown inventory ship settings command: {request.Command}");
                return;
        }
    }

    private void HideCurrentShipSettingsSurface()
    {
        if (_shipSettingsSurfaceDocument == null)
            return;

        _shipSettingsSurfaceDocument.rootVisualElement.Clear();
        _shipSettingsSurfaceDocument.gameObject.SetActive(false);
    }

    private void RenderCargoItemDetailsSurface(ItemInstance item)
    {
        var typedItem = FindTypedInventoryItem(item);
        if (item == null || typedItem == null)
            return;

        var document = ResolveCargoItemDetailsSurfaceDocument();
        document.gameObject.SetActive(true);

        var root = document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1;
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.top = 0;
        root.style.right = 0;
        root.style.bottom = 0;
        root.style.alignItems = Align.FlexStart;
        root.style.justifyContent = Justify.FlexStart;
        root.pickingMode = PickingMode.Ignore;

        var shell = new VisualElement();
        shell.style.width = 420;
        shell.style.maxWidth = 520;
        shell.style.backgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.94f);
        shell.style.borderTopLeftRadius = 8;
        shell.style.borderTopRightRadius = 8;
        shell.style.borderBottomLeftRadius = 8;
        shell.style.borderBottomRightRadius = 8;
        shell.style.paddingLeft = 18;
        shell.style.paddingRight = 18;
        shell.style.paddingTop = 18;
        shell.style.paddingBottom = 18;
        shell.style.borderLeftWidth = 1;
        shell.style.borderRightWidth = 1;
        shell.style.borderTopWidth = 1;
        shell.style.borderBottomWidth = 1;
        shell.style.borderLeftColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderRightColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderTopColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderBottomColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.pickingMode = PickingMode.Position;
        root.Add(shell);

        var lowerer = new EveUiToolkitSurfaceLowerer();
        shell.Add(lowerer.Lower(BuildCargoItemDetailsSurfaceDefinition(item, typedItem), HandleCargoItemDetailsSurfaceCommand));
    }

    private void HandleCargoItemDetailsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (string.Equals(request.Command, CloseCargoItemDetailsCommand, StringComparison.Ordinal))
        {
            HideCargoItemDetailsSurface();
            ClearSelectedItemSelection();
            return;
        }

        Debug.LogWarning($"Unknown inventory cargo item details command: {request.Command}");
    }

    private void HideCargoItemDetailsSurface()
    {
        if (_cargoItemDetailsSurfaceDocument == null)
            return;

        _cargoItemDetailsSurfaceDocument.rootVisualElement.Clear();
        _cargoItemDetailsSurfaceDocument.gameObject.SetActive(false);
    }

    private UIDocument ResolveCargoItemDetailsSurfaceDocument()
    {
        if (_cargoItemDetailsSurfaceDocument != null)
            return _cargoItemDetailsSurfaceDocument;

        var host = new GameObject("Aetheria Inventory Cargo Item Details Surface");
        host.transform.SetParent(transform, false);
        var document = host.AddComponent<UIDocument>();
        document.sortingOrder = 1001;
        host.SetActive(false);
        _cargoItemDetailsSurfaceDocument = document;
        return document;
    }

    private EveSurfaceDocument BuildCargoItemDetailsSurfaceDefinition(ItemInstance item, AetheriaRuntimeCatalogItem typedItem)
    {
        var children = new List<EveSurfaceComponent>
        {
            Card(
                $"{CargoItemDetailsSurfaceId}.summary",
                typedItem.Name,
                Text(
                    $"{CargoItemDetailsSurfaceId}.description",
                    typedItem.Description ?? "No typed item description is available."),
                Text(
                    $"{CargoItemDetailsSurfaceId}.note",
                    "InventoryMenu still owns cell selection. This surface replaces the old cargo-item PropertiesPanel shell."),
                Metric(
                    $"{CargoItemDetailsSurfaceId}.manufacturer",
                    "Manufacturer",
                    ActionGameManager.RuntimeCatalog?.GetManufacturer(typedItem)?.Name ?? "GameCult"),
                Metric(
                    $"{CargoItemDetailsSurfaceId}.mass",
                    "Mass",
                    ActionGameManager.RuntimePlayerSettings.Format(GetCargoItemMass(item, typedItem))))
        };

        if (typedItem.Price > 0)
        {
            children.Add(Card(
                $"{CargoItemDetailsSurfaceId}.market.card",
                "Market",
                Metric(
                    $"{CargoItemDetailsSurfaceId}.price",
                    "Price",
                    typedItem.Price.ToString("N0"))));
        }

        if (item is SimpleCommodity simpleCommodity)
        {
            children.Add(Card(
                $"{CargoItemDetailsSurfaceId}.quantity.card",
                "Quantity",
                Metric(
                    $"{CargoItemDetailsSurfaceId}.quantity",
                    "Units",
                    simpleCommodity.Quantity.ToString())));
        }

        if (item is EquippableItem equippableItem)
        {
            var (tier, upgrades) = GameManager.ItemManager.GetTier(equippableItem);
            children.Add(Card(
                $"{CargoItemDetailsSurfaceId}.status.card",
                "Status",
                Metric(
                    $"{CargoItemDetailsSurfaceId}.tier",
                    "Tier",
                    $"{tier.Name}{new string('+', upgrades)}"),
                Metric(
                    $"{CargoItemDetailsSurfaceId}.durability",
                    "Durability",
                    $"{(int)(equippableItem.Durability / GetMaxDurability(typedItem, equippableItem) * 100)}%"),
                Metric(
                    $"{CargoItemDetailsSurfaceId}.temperature_range",
                    "Thermal Range",
                    FormatTemperatureRange(typedItem))));

            foreach (var behaviorCard in BuildCargoItemBehaviorCards(typedItem, equippableItem))
            {
                children.Add(behaviorCard);
            }
        }

        children.Add(ButtonRow(
            $"{CargoItemDetailsSurfaceId}.actions",
            Button($"{CargoItemDetailsSurfaceId}.close", "Close", CloseCargoItemDetailsCommand)));

        return new EveSurfaceDocument(
            ShipSettingsSurfaceType,
            ShipSettingsSurfaceSchema,
            ShipSettingsSurfaceProviderId,
            ShipSettingsSurfaceProviderKind,
            "Inventory Cargo Item Details",
            version: 1,
            DateTime.UtcNow.ToString("O"),
            new EveSurfaceTree(
                CargoItemDetailsSurfaceId,
                Node(
                    $"{CargoItemDetailsSurfaceId}.root",
                    "surface",
                    Array.Empty<(string Key, string Value)>(),
                    children.ToArray()),
                Array.Empty<EveStyleToken>()),
            new[]
            {
                new EveCommandTemplate(CloseCargoItemDetailsCommand, "Close", "unity-uitoolkit")
            });
    }

    private IEnumerable<EveSurfaceComponent> BuildCargoItemBehaviorCards(
        AetheriaRuntimeCatalogItem typedItem,
        EquippableItem equippableItem)
    {
        foreach (var behavior in typedItem.BehaviorPayloads ?? Array.Empty<AetheriaRuntimeBehaviorPayload>())
        {
            if (string.Equals(behavior.Kind, AetheriaRuntimeBehaviorKinds.StatModifier, StringComparison.Ordinal))
            {
                var statReference = ReadTypedStatReference(FindTypedBehaviorField(behavior, 1)?.Value);
                var modifier = ReadTypedPerformanceStat(FindTypedBehaviorField(behavior, 2)?.Value);
                var modifierType = ReadTypedEnum(FindTypedBehaviorField(behavior, 3)?.Value, StatModifierType.Constant);
                yield return Card(
                    $"{CargoItemDetailsSurfaceId}.behavior.{behavior.Kind}.stat_modifier",
                    "Stat Modifier",
                    Metric(
                        $"{CargoItemDetailsSurfaceId}.behavior.{behavior.Kind}.target",
                        $"{statReference.target.SplitCamelCase()}:{statReference.stat.SplitCamelCase()}",
                        $"{(modifierType == StatModifierType.Constant ? "+" : "x")}{ActionGameManager.RuntimePlayerSettings.Format(GameManager.ItemManager.Evaluate(modifier, equippableItem))}"));
                continue;
            }

            var metadata = AetheriaRuntimeBehaviorMetadataCatalog.Get(behavior.Kind);
            if (metadata == null)
                continue;

            var fields = metadata.DisplayFields
                .Select(field => BuildCargoItemBehaviorMetric(behavior, field, equippableItem))
                .Where(metric => metric != null)
                .ToArray();

            if (fields.Length == 0)
                continue;

            yield return Card(
                $"{CargoItemDetailsSurfaceId}.behavior.{behavior.Kind}",
                behavior.Kind.FormatTypeName(),
                fields);
        }
    }

    private EveSurfaceComponent BuildCargoItemBehaviorMetric(
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
                value = ActionGameManager.RuntimePlayerSettings.Format(GameManager.ItemManager.Evaluate(ReadTypedPerformanceStat(payloadField.Value), equippableItem));
                break;
            default:
                return null;
        }

        return Metric(
            $"{CargoItemDetailsSurfaceId}.behavior.{behavior.Kind}.{field.Key}",
            field.Name.SplitCamelCase(),
            value);
    }

    private UIDocument ResolveShipSettingsSurfaceDocument()
    {
        if (_shipSettingsSurfaceDocument != null)
            return _shipSettingsSurfaceDocument;

        var host = new GameObject("Aetheria Inventory Ship Settings Surface");
        host.transform.SetParent(transform, false);
        var document = host.AddComponent<UIDocument>();
        document.sortingOrder = 1000;
        host.SetActive(false);
        _shipSettingsSurfaceDocument = document;
        return document;
    }

    private EveSurfaceDocument BuildCurrentShipSettingsSurfaceDefinition(Entity entity)
    {
        return new EveSurfaceDocument(
            ShipSettingsSurfaceType,
            ShipSettingsSurfaceSchema,
            ShipSettingsSurfaceProviderId,
            ShipSettingsSurfaceProviderKind,
            "Current Ship Settings",
            version: 1,
            DateTime.UtcNow.ToString("O"),
            new EveSurfaceTree(
                ShipSettingsSurfaceId,
                Node(
                    $"{ShipSettingsSurfaceId}.root",
                    "surface",
                    Array.Empty<(string Key, string Value)>(),
                    Card(
                        $"{ShipSettingsSurfaceId}.card",
                        entity.Name,
                        Metric(
                            $"{ShipSettingsSurfaceId}.shutdown.metric",
                            "Shutdown Threshold",
                            ActionGameManager.RuntimePlayerSettings.Format(entity.Settings.ShutdownPerformance)),
                        Text(
                            $"{ShipSettingsSurfaceId}.note",
                            "Gameplay still owns the mutation. This surface only lowers the current ship setting."),
                        ButtonRow(
                            $"{ShipSettingsSurfaceId}.shutdown.buttons",
                            Button($"{ShipSettingsSurfaceId}.shutdown.decrement", "Threshold -", DecrementShutdownThresholdCommand),
                            Button($"{ShipSettingsSurfaceId}.shutdown.increment", "Threshold +", IncrementShutdownThresholdCommand),
                            Button($"{ShipSettingsSurfaceId}.shutdown.reset", "Default", ResetShutdownThresholdCommand),
                            Button($"{ShipSettingsSurfaceId}.close", "Close", CloseShipSettingsCommand)))),
                Array.Empty<EveStyleToken>()),
            new[]
            {
                new EveCommandTemplate(DecrementShutdownThresholdCommand, "Threshold -", "unity-uitoolkit"),
                new EveCommandTemplate(IncrementShutdownThresholdCommand, "Threshold +", "unity-uitoolkit"),
                new EveCommandTemplate(ResetShutdownThresholdCommand, "Default", "unity-uitoolkit"),
                new EveCommandTemplate(CloseShipSettingsCommand, "Close", "unity-uitoolkit")
            });
    }

    private static EveSurfaceComponent Card(
        string id,
        string title,
        params EveSurfaceComponent[] children)
    {
        return Node(id, "card", new[] { ("title", title) }, children);
    }

    private static EveSurfaceComponent Metric(string id, string label, string value)
    {
        return Node(id, "metric", new[] { ("label", label), ("value", value) });
    }

    private static EveSurfaceComponent Text(string id, string value)
    {
        return Node(id, "text", new[] { ("value", value) });
    }

    private static EveSurfaceComponent Button(string id, string label, string command)
    {
        return Node(id, "control.button", new[] { ("label", label), ("command", command) });
    }

    private static EveSurfaceComponent ButtonRow(
        string id,
        params EveSurfaceComponent[] children)
    {
        return Node(id, "row", Array.Empty<(string Key, string Value)>(), children);
    }

    private static EveSurfaceComponent Node(
        string id,
        string kind,
        IEnumerable<(string Key, string Value)> props,
        params EveSurfaceComponent[] children)
    {
        return new EveSurfaceComponent(
            id,
            kind,
            props.ToDictionary(prop => prop.Key, prop => prop.Value, StringComparer.Ordinal),
            children ?? Array.Empty<EveSurfaceComponent>());
    }

    private bool CommitCargoItemTransfer(EquippedCargoBay origin, InventoryPanel destination, ItemInstance item)
    {
        if (destination.DisplayedEntity != null && item is EquippableItem equippableItem)
            return GameManager.CommitCargoItemEquip(origin, destination.DisplayedEntity, equippableItem);

        if (destination.DisplayedCargo != null)
            return GameManager.CommitCargoItemTransfer(origin, destination.DisplayedCargo, item);

        return false;
    }

    private void ClearSelectedItemSelection()
    {
        ClearSelectedCellHighlight();
        _selectedPanel = null;
        _selectedItem = null;
        _selectedCells = null;
        _selectedPosition = default;
    }

    private bool CommitEquippedItemTransfer(Entity origin, EquippedItem item, InventoryPanel destination)
    {
        if (destination.DisplayedCargo != null)
            return GameManager.CommitEquippedItemStore(origin, item, destination.DisplayedCargo);

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

    private static PerformanceStat ReadTypedPerformanceStat(AetheriaRuntimeBehaviorValue value)
    {
        return new PerformanceStat
        {
            Min = ReadTypedChildNumber(value, 0),
            Max = ReadTypedChildNumber(value, 1),
            HeatExponentMultiplier = ReadTypedChildNumber(value, 2),
            DurabilityExponentMultiplier = ReadTypedChildNumber(value, 3),
            QualityExponent = ReadTypedChildNumber(value, 4)
        };
    }

    private static (string target, string stat) ReadTypedStatReference(AetheriaRuntimeBehaviorValue value)
    {
        return (
            ReadTypedChildString(value, 1),
            ReadTypedChildString(value, 2));
    }

    private static float ReadTypedChildNumber(AetheriaRuntimeBehaviorValue value, int index)
    {
        return value != null && value.Children.Count > index
            ? (float)value.Children[index].NumberValue
            : 0f;
    }

    private static string ReadTypedChildString(AetheriaRuntimeBehaviorValue value, int index)
    {
        return value != null && value.Children.Count > index
            ? value.Children[index].StringValue ?? ""
            : "";
    }

    private static T ReadTypedEnum<T>(AetheriaRuntimeBehaviorValue value, T fallback) where T : struct
    {
        if (!string.IsNullOrWhiteSpace(value?.StringValue) && Enum.TryParse(value.StringValue, true, out T parsed))
        {
            return parsed;
        }

        return value != null && Enum.IsDefined(typeof(T), (int)value.NumberValue)
            ? (T)Enum.ToObject(typeof(T), (int)value.NumberValue)
            : fallback;
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
            Destroy(_shipSettingsSurfaceDocument.gameObject);
            _shipSettingsSurfaceDocument = null;
        }

        if (_cargoItemDetailsSurfaceDocument != null)
        {
            Destroy(_cargoItemDetailsSurfaceDocument.gameObject);
            _cargoItemDetailsSurfaceDocument = null;
        }
    }
}

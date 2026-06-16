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
                            PropertiesPanel.gameObject.SetActive(true);
                            ClearSelectedCellHighlight();
                            _selectedPanel = panel;
                            _selectedPosition = cargoEvent.Position;
                            PropertiesPanel.Inspect(item);
                            _selectedPanel = panel;
                            _selectedPosition = cargoEvent.CargoBay.Cargo[item];
                            _selectedItem = item;
                            _selectedCells = GetSelectedCells(item, _selectedPosition);
                            ApplySelectedCellHighlight();
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
                            PropertiesPanel.gameObject.SetActive(true);
                            ClearSelectedCellHighlight();

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

        PropertiesPanel.gameObject.SetActive(false);
        ClearSelectedCellHighlight();

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
    }
}

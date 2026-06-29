/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Aetheria.EveRuntime;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
using GameCult.Mesh;
using TMPro;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Experimental.Rendering;
using Unity.Mathematics;
using UnityEngine.Serialization;
using UIE = UnityEngine.UIElements;
using static Unity.Mathematics.math;
using int2 = Unity.Mathematics.int2;

public class InventoryPanel : MonoBehaviour, IPointerClickHandler
{
    public ConfirmationDialog Dialog;
    public RectTransform DragParent;
    public bool Flip;
    public GameSettings Settings;
    public TextMeshProUGUI Title;
    public TextMeshProUGUI MinTempLabel;
    public TextMeshProUGUI MaxTempLabel;
    public GameObject TemperatureRange;
    public Button Dropdown;
    public Button EditName;
    public Button Current;
    public Button Thermal;
    public GridLayoutGroup Grid;
    public Sprite[] NodeBackgroundTextures;
    public Sprite[] NodeTextures;
    public Sprite[] ThermalTextures;
    public float ThermalToggleRegionSize = .2f;
    public Prototype NodePrototype;
    public RawImage TemperatureDisplay;
    public Texture2D TemperatureColor;
    public ExponentialLerp TemperatureColorCurve;
    public ExponentialLerp TemperatureAlphaCurve;
    public bool FitToContent;
    public float CellHitPulseTime;
    public Color ToggleEnabledColor;
    public Color ToggleDisabledColor;
    public Color CellBackgroundColor = new Color(0, 0, 0, .75f);
    public float MinTempRange = 1;
    public float DoubleClickTime = .5f;
    public float HitDamageThreshold = 1;
    
    // Subject<InventoryEventData> _onBeginDrag;
    // Subject<InventoryEventData> _onDrag;
    // Subject<InventoryEventData> _onEndDrag;
    Subject<(InventoryEventData data, int clickCount)> _onClick;
    // Subject<InventoryEventData> _onPointerEnter;
    // Subject<InventoryEventData> _onPointerExit;

    private Entity _displayedEntity;
    private EquippedCargoBay _displayedCargo;
    private string _displayedEntityKey = "";
    private string _displayedCargoEntityKey = "";
    private int _displayedCargoIndex = -1;
    private Shape _displayedHullShape;
    private Shape _displayedHullInterior;
    private Texture2D _temperatureTexture;
    private RectTransform _firstRect;
    private AetheriaUnityDragSession _dragSession;
    private AetheriaUnityObservedEntityIndex _observedEntityIndex;
    private AetheriaUnityObservedDockingIndex _observedDockingIndex;

    public ItemInstance FakeItem;
    public Shape FakeOccupancy;
    public Shape IgnoreOccupancy;
    public List<GameObject> EmptyCells = new List<GameObject>();

    public void SetDragSession(AetheriaUnityDragSession dragSession)
    {
        _dragSession = dragSession;
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

    private AetheriaUnityDragSession DragSession => _dragSession ??= new AetheriaUnityDragSession();
    public Dictionary<int2, InventoryCell> CellInstances = new Dictionary<int2, InventoryCell>();
    
    private List<IDisposable> _subscriptions = new List<IDisposable>();
    private Dictionary<int2, int> _cellAnimationSequence = new Dictionary<int2, int>();
    private int _hitSequence = 0;
    private bool _thermal = false;
    private int _clickCount;
    private InventoryCell _clickCell;
    private float _clickTime;
    private string _clientStatePath = "";
    private CultMeshReactiveDocument<AetheriaRuntimeCatalogSnapshot> _catalog;
    private CultMeshReactiveDocument<AetheriaRuntimePlayerSettingsDocument> _playerSettings;
    private CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument> _currentEntity;
    private CultMeshReactiveDocument<AetheriaRuntimeStationRefitDocument> _stationRefit;
    private CultMeshReactiveDocument<AetheriaRuntimeDaemonFrameDocument> _loadoutFrame;
    private int _inventoryEntityIndex = -1;
    private CultMeshReactiveDocument<AetheriaRuntimeInventoryDocument> _inventory;
    private AetheriaRuntimeStationRefitEntityOption[] _dropdownStationRefitEntities =
        Array.Empty<AetheriaRuntimeStationRefitEntityOption>();
    private AetheriaRuntimeStationLoadoutRestoreOption[] _dropdownStationRefitLoadouts =
        Array.Empty<AetheriaRuntimeStationLoadoutRestoreOption>();
    private AetheriaRuntimeInventoryDropdownSurfaceModel _dropdownSurfaceModel;
    private readonly AetheriaEveUnitySurfaceChrome _dropdownSurfaceChrome = new AetheriaEveUnitySurfaceChrome
    {
        RootAlignItems = UIE.Align.FlexStart,
        RootJustifyContent = UIE.Justify.FlexStart,
        RootPaddingTop = 0f,
        Width = 420f,
        MinWidth = 0f,
        MaxWidth = 520f,
        PaddingLeft = 18f,
        PaddingRight = 18f,
        PaddingTop = 18f,
        PaddingBottom = 18f
    };
    private UIE.UIDocument _dropdownSurfaceDocument;

    private bool _hud;
    
    private int2[] _offsets = {
        int2(0, 1),
        int2(1, 0),
        int2(0, -1),
        int2(-1, 0),
        int2(1, 1),
        int2(1, -1),
        int2(-1, -1),
        int2(-1, 1)
    };

    public InventoryPanelTarget Target => 
        _displayedEntity != null ? InventoryPanelTarget.Equipment :
        _displayedCargo != null ? InventoryPanelTarget.Cargo : InventoryPanelTarget.None;
    public Entity DisplayedEntity => _displayedEntity;
    public EquippedCargoBay DisplayedCargo => _displayedCargo;

    private void Start()
    {
        if (Thermal)
        {
            Thermal.onClick.AddListener(() =>
            {
                HideDropdownSurface();
                Display(_displayedEntity, false, !_thermal);
            });
        }

        if (Current)
        {
            Current.onClick.AddListener(() =>
            {
                HideDropdownSurface();
                if (!IsCurrentEntity(_displayedEntity))
                {
                    if(_displayedEntity is Ship ship)
                    {
                        RequestDockedCurrentShip(ship);
                    }
                    else
                    {
                        Dialog.Clear();
                        Dialog.Title.text = "Can't select entity, you can only pilot a ship!";
                        Dialog.Show();
                        Dialog.MoveToCursor();
                    }
                }
            });
        }

        if (EditName)
        {
            EditName.onClick.AddListener(() =>
            {
                HideDropdownSurface();
                Dialog.Clear();
                var entityName = _displayedEntity.Name;
                Dialog.AddField("Name", () => entityName, s => entityName = s);
                Dialog.Show(() =>
                {
                    RequestEntityName(_displayedEntity, entityName);
                });
                Dialog.MoveToCursor();
            });
        }
        
        if(Dropdown)
        {
            Dropdown.onClick.AddListener(() =>
            {
                RenderDropdownSurface();
            });
        }
    }

    private void RenderDropdownSurface()
    {
        _dropdownSurfaceModel = ComposeDropdownSurface();

        _dropdownSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _dropdownSurfaceDocument,
            "Aetheria Inventory Dropdown Surface",
            AetheriaRuntimeInventoryDropdownSurfaceBuilder.Build(_dropdownSurfaceModel.State),
            HandleDropdownSurfaceCommand,
            _dropdownSurfaceChrome);
    }

    private void HandleDropdownSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (!AetheriaRuntimeInventoryDropdownSurfaceCommands.TryRead(request, out var command))
        {
            Debug.LogWarning($"Unknown inventory dropdown command: {request?.Command}");
            return;
        }

        if (command.Kind == AetheriaRuntimeInventoryDropdownCommandKind.Close)
        {
            HideDropdownSurface();
            return;
        }

        if (command.Kind == AetheriaRuntimeInventoryDropdownCommandKind.Select &&
            _dropdownSurfaceModel?.TryResolve(command.Command, out var selection) == true)
        {
            ExecuteDropdownSelection(selection);
            HideDropdownSurface();
            return;
        }

        Debug.LogWarning($"Unknown inventory dropdown command: {request?.Command}");
    }

    private void ExecuteDropdownSelection(AetheriaRuntimeInventoryDropdownSelection selection)
    {
        switch (selection.Kind)
        {
            case AetheriaRuntimeInventoryDropdownSelectionKind.EntityEquipment:
            case AetheriaRuntimeInventoryDropdownSelectionKind.Entity:
                if (TryResolveStationRefitEntity(selection.EntityKey, selection.EntityIndex, out var entity))
                {
                    Display(entity);
                }
                return;
            case AetheriaRuntimeInventoryDropdownSelectionKind.EntityBay:
                if (TryResolveStationRefitEntity(selection.EntityKey, selection.EntityIndex, out entity) &&
                    selection.BayIndex >= 0 &&
                    selection.BayIndex < entity.CargoBays.Count)
                {
                    Display(entity.CargoBays[selection.BayIndex]);
                }
                return;
            case AetheriaRuntimeInventoryDropdownSelectionKind.DockingBay:
                if (TryResolveCurrentDockingBay(out var dockingBay))
                {
                    Display(dockingBay);
                }
                return;
            case AetheriaRuntimeInventoryDropdownSelectionKind.SaveLoadout:
                if (_displayedEntity != null)
                {
                    RequestLoadoutTemplateSave(_displayedEntity);
                }
                return;
            case AetheriaRuntimeInventoryDropdownSelectionKind.Loadout:
                if (selection.TemplateIndex >= 0 && selection.TemplateIndex < _dropdownStationRefitLoadouts.Length)
                {
                    RequestRuntimeLoadoutRestore(_dropdownStationRefitLoadouts[selection.TemplateIndex]);
                }
                return;
        }
    }

    private void HideDropdownSurface()
    {
        if (_dropdownSurfaceDocument == null)
            return;

        AetheriaEveUnitySurfaceHost.Hide(_dropdownSurfaceDocument);
    }

    private AetheriaRuntimeInventoryDropdownSurfaceModel ComposeDropdownSurface()
    {
        var stationRefit = ResolveStationRefit();
        _dropdownStationRefitEntities = (stationRefit?.AvailableEntities ?? Array.Empty<AetheriaRuntimeStationRefitEntityOption>())
            .ToArray();
        _dropdownStationRefitLoadouts = (stationRefit?.LoadoutRestoreOptions ?? Array.Empty<AetheriaRuntimeStationLoadoutRestoreOption>())
            .ToArray();
        var entityOptions = _dropdownStationRefitEntities
            .Select((entity, entityIndex) => new AetheriaRuntimeInventoryDropdownEntityOption(
                entityIndex,
                entity.EntityKey,
                string.IsNullOrWhiteSpace(entity.DisplayName) ? $"Entity {entity.EntityIndex}" : entity.DisplayName,
                IsDisplayedEntityKey(entity.EntityKey),
                Enumerable.Range(0, Math.Max(entity.CargoBayCount, 0))
                    .Select(bayIndex => new AetheriaRuntimeInventoryDropdownBayOption(
                        bayIndex,
                        $"Bay {bayIndex + 1}",
                        IsDisplayedCargoBayKey(entity.EntityKey, bayIndex)))
                    .ToArray()))
            .ToArray();
        var loadoutOptions = _dropdownStationRefitLoadouts
            .Select((loadout, optionIndex) => new AetheriaRuntimeInventoryDropdownLoadoutOption(
                optionIndex,
                loadout.TemplateName,
                loadout.CanRestore ? $"{loadout.Price:n0}" : "",
                loadout.CanRestore))
            .ToArray();
        var hasDockingBay = TryResolveCurrentDockingBayRow(out var currentDockingBay);

        return AetheriaRuntimeInventoryDropdownSurfaceBuilder.Compose(
            Title?.text ?? "None",
            entityOptions,
            hasDockingBay,
            hasDockingBay
                ? $"Docking Bay {currentDockingBay.DockingBayIndex + 1}"
                : "Docking Bay",
            hasDockingBay &&
            IsDisplayedCargoBayKey(
                ResolveStationRefit()?.DockParentEntityKey ?? "",
                currentDockingBay.DockingBayIndex),
            _displayedEntity != null,
            loadoutOptions,
            DateTime.UtcNow.ToString("O"));
    }

private void Update()
    {
        if (_displayedEntity != null && TemperatureDisplay)
        {
            var tempRange = _displayedEntity.MaxTemp - _displayedEntity.MinTemp;
            var opacity = smoothstep(0, MinTempRange, tempRange);
            if(MinTempLabel)
            {
                MinTempLabel.text = FormatTemperature(_displayedEntity.MinTemp);
                MaxTempLabel.text = FormatTemperature(_displayedEntity.MaxTemp);
            }
            for(var x = 0; x < _temperatureTexture.width; x++)
            {
                for (var y = 0; y < _temperatureTexture.height; y++)
                {
                    if (_displayedHullShape[int2(x-1, y-1)])
                    {
                        var temp = (_displayedEntity.Temperature[x - 1, y - 1] - _displayedEntity.MinTemp) / (_displayedEntity.MaxTemp-_displayedEntity.MinTemp);
                        var color = TemperatureColor.GetPixelBilinear(TemperatureColorCurve.Evaluate(temp), 0);
                        color.a = TemperatureAlphaCurve.Evaluate(temp) * opacity;
                        _temperatureTexture.SetPixel(x,y,color);
                    }
                    else
                        _temperatureTexture.SetPixel(x,y,new Color(0,0,0,0));
                }
            }
            _temperatureTexture.Apply();
            TemperatureDisplay.rectTransform.anchoredPosition = _firstRect.anchoredPosition - Vector2.one * Grid.cellSize * 1.5f;
        }
    }

    private void OnDisable()
    {
        HideDropdownSurface();
        Clear();
    }
    

    public void Clear()
    {
        HideDropdownSurface();
        _displayedCargo = null;
        _displayedEntity = null;
        _displayedEntityKey = "";
        _displayedCargoEntityKey = "";
        _displayedCargoIndex = -1;
        _displayedHullShape = null;
        _displayedHullInterior = null;
        
        foreach(var empty in EmptyCells)
            Destroy(empty);
        EmptyCells.Clear();
        
        foreach(var node in CellInstances.Values)
            node.GetComponent<Prototype>().ReturnToPool();
        CellInstances.Clear();
        
        foreach(var s in _subscriptions)
            s.Dispose();
        
        _subscriptions.Clear();
        
        if (Current) Current.gameObject.SetActive(false);
        if (Thermal) Thermal.gameObject.SetActive(false);
        if (TemperatureRange) TemperatureRange.SetActive(false);
        if(TemperatureDisplay) TemperatureDisplay.gameObject.SetActive(false);
        if (EditName) EditName.gameObject.SetActive(false);

        if(Title)
            Title.text = "None";
    }

    public void Display(Entity entity, bool hud = false, bool thermal = false)
    {
        Clear();
        
        _subscriptions.Add(entity.Equipment.ObserveAdd().Subscribe(_ => RefreshCells()));
        _subscriptions.Add(entity.Equipment.ObserveRemove().Subscribe(_ => RefreshCells()));
        
        _thermal = thermal;
        _hud = hud;

        _displayedEntity = entity;
        _displayedCargo = null;
        _displayedCargoEntityKey = "";
        _displayedCargoIndex = -1;
        _displayedEntityKey = TryResolveEntityRecordKey(entity, out var displayedEntityKey)
            ? displayedEntityKey
            : "";
        _firstRect = null;
        
        if (TemperatureRange)
            TemperatureRange.SetActive(true);

        if(Title)
            Title.text = entity.Name;

        if (EditName)
        {
            EditName.gameObject.SetActive(true);
        }

        if (!TryResolveTypedHullGeometry(entity.Hull, out _displayedHullShape, out _displayedHullInterior))
        {
            Debug.LogError($"Cannot display inventory for {entity.Name}: missing typed hull geometry.");
            return;
        }
        
        if (FitToContent)
        {
            var gridRect = Grid.GetComponent<RectTransform>();
            var rect = gridRect.rect;
            Grid.cellSize = Vector2.one * (int) min(rect.width / (_displayedHullShape.Width + 1), rect.height / (_displayedHullShape.Height + 1));
        }
        
        if(TemperatureDisplay)
        {
            _temperatureTexture = new Texture2D(
                _displayedHullShape.Width + 2,
                _displayedHullShape.Height + 2,
                TextureFormat.RGBA32,
                false,
                false);
            TemperatureDisplay.gameObject.SetActive(true);
            TemperatureDisplay.texture = _temperatureTexture;
            var tempRect = TemperatureDisplay.rectTransform;
            tempRect.sizeDelta = Grid.cellSize * new Vector2(_displayedHullShape.Width + 2, _displayedHullShape.Height + 2);
        }
        
        if (Current)
        {
            Current.gameObject.SetActive(true);
            Current.targetGraphic.color =
                IsCurrentEntity(entity)
                    ? ToggleEnabledColor
                    : ToggleDisabledColor;
        }
        if (Thermal)
        {
            Thermal.gameObject.SetActive(true);
            Thermal.targetGraphic.color = thermal ? ToggleEnabledColor : ToggleDisabledColor;
        }
        
        Grid.constraintCount = _displayedHullShape.Width;
        foreach (var v in _displayedHullShape.AllCoordinates)
        {
            if (!_displayedHullShape[v])
            {
                var empty = new GameObject("Empty Node", typeof(RectTransform));
                empty.transform.SetParent(Grid.transform);
                if (!_firstRect)
                    _firstRect = empty.GetComponent<RectTransform>();
                EmptyCells.Add(empty);
            }
            else
            {
                var cell = NodePrototype.Instantiate<InventoryCell>();
                cell.Background.color = CellBackgroundColor;
                if (!_firstRect)
                    _firstRect = cell.GetComponent<RectTransform>();
                if (!thermal)
                {
                    if(cell.PointerClickTrigger)
                    {
                        cell.PointerClickTrigger.OnPointerClickAsObservable()
                            .Subscribe(data =>
                            {
                                if (cell != _clickCell || Time.time - _clickTime > DoubleClickTime) 
                                    _clickCount = 0;
                                _clickCell = cell;
                                _clickTime = Time.time;
                                _clickCount++;
                                _onClick?.OnNext((new InventoryEntityEventData(data, v, entity), _clickCount));
                            });
                        cell.BeginDragTrigger.OnBeginDragAsObservable()
                            .Subscribe(data =>
                            {
                                //Debug.Log("Entity Drag Start");
                                var item = entity.GearOccupancy[v.x, v.y];
                                if (item != null)
                                {
                                    var originalOccupancy = _displayedHullShape.Inset(GetItemShape(item.EquippableItem), item.Position, item.EquippableItem.Rotation);
                                    _dragCells = originalOccupancy.Coordinates
                                        .Select(v1 => Instantiate(CellInstances[v1], DragParent, true).transform).ToArray();
                                    foreach(var dragCell in _dragCells)
                                    {
                                        DestroyImmediate(dragCell.GetComponent<Prototype>());
                                        foreach (var component in dragCell.GetComponentsInChildren<ObservableTriggerBase>())
                                            component.enabled = false;
                                        foreach (var img in dragCell.GetComponentsInChildren<Image>())
                                            img.color = new Color(img.color.r, img.color.g, img.color.b, img.color.a * .5f);
                                        dragCell.GetComponentInChildren<Image>().raycastTarget = false;
                                    }
                                    _dragOffsets = _dragCells.Select(x => (Vector2) x.position - data.position).ToArray();
                                    IgnoreOccupancy = originalOccupancy;
                                    RefreshCells();
                                    _originalRotation = item.EquippableItem.Rotation;
                                    DragSession.Begin(new EquippedItemDragObject(item, entity, item.Position - v));
                                    //AkSoundEngine.PostEvent("Pickup", gameObject);
                                    // TODO: SFX: Pickup Item
                                }
                            });
                        cell.DragTrigger.OnDragAsObservable()
                            .Subscribe(data =>
                            {
                                for (var i = 0; i < _dragCells.Length; i++)
                                    _dragCells[i].position = new Vector3(
                                        data.position.x + _dragOffsets[i].x,
                                        data.position.y + _dragOffsets[i].y,
                                        _dragCells[i].position.z);
                            });
                        cell.EndDragTrigger.OnEndDragAsObservable()
                            .Subscribe(data =>
                            {
                                //Debug.Log("Entity Drag End");
                                IgnoreOccupancy = null;
                                var hadDragTarget = DragSession.End();
                                if (!hadDragTarget)
                                    entity.GearOccupancy[v.x, v.y].EquippableItem.Rotation = _originalRotation;
                                foreach(var dragObject in _dragCells)
                                    Destroy(dragObject.gameObject);
                                _dragCells = null;
                                RefreshCells();
                            });
                        cell.PointerEnterTrigger.OnPointerEnterAsObservable()
                            .Subscribe(data =>
                            {
                                //Debug.Log("Entity Pointer Enter");
                                if (!DragSession.TryGetDraggedItem(out var itemDragObject)) return;
                                var item = itemDragObject.Item;
                                if (!(item is EquippableItem equippableItem)) return;
                                var placementPosition = v + itemDragObject.OriginCellOffset;
                                //foreach (var cell in _dragCells) cell.gameObject.SetActive(false);
                                FakeItem = item;
                                FakeOccupancy = _displayedHullShape.Inset(GetItemShape(equippableItem), placementPosition, item.Rotation);
                                RefreshCells();
                                DragSession.RegisterTarget(drag =>
                                {
                                    //Debug.Log("Entity Drag Callback");
                                    FakeOccupancy = null;
                                    RequestDraggedItemToEntity(drag, entity, placementPosition);
                                    // TODO: SFX: Equip
                                });
                            });
                        cell.PointerExitTrigger.OnPointerExitAsObservable()
                            .Subscribe(data =>
                            {
                                if (!DragSession.TryGetDraggedItem(out _)) return;
                                FakeItem = null;
                                FakeOccupancy = null;
                                RefreshCells();
                                DragSession.UnregisterTarget();
                            });
                    }
                }
                else
                {
                    cell.PointerClickTrigger.OnPointerClickAsObservable().Subscribe(data =>
                    {
                        var rect = cell.GetComponent<RectTransform>();
                        var point = Rect.PointToNormalized(rect.rect, rect.InverseTransformPoint(data.position));
//                        Debug.Log($"Clicked at pos {data.position}, normalized {point}");
                        if (_displayedHullShape[int2(v.x - 1, v.y)] && point.x < ThermalToggleRegionSize)
                        {
                            RequestHullConductivityToggle(entity, int2(v.x - 1, v.y), 0);
                        }
                        if (_displayedHullShape[int2(v.x + 1, v.y)] && point.x > 1 - ThermalToggleRegionSize)
                        {
                            RequestHullConductivityToggle(entity, int2(v.x, v.y), 0);
                        }
                        if (_displayedHullShape[int2(v.x, v.y - 1)] && point.y < ThermalToggleRegionSize)
                        {
                            RequestHullConductivityToggle(entity, int2(v.x, v.y - 1), 1);
                        }
                        if (_displayedHullShape[int2(v.x, v.y + 1)] && point.y > 1 - ThermalToggleRegionSize)
                        {
                            RequestHullConductivityToggle(entity, int2(v.x, v.y), 1);
                        }
                    });
                }
                CellInstances.Add(v, cell);
            }
        }
        RefreshCells(CellInstances.Keys);
        
        _subscriptions.Add(entity.ArmorDamage.Subscribe(hit =>
        {
            var hitCells = new[] { hit.pos };
            StartCoroutine(Pulse(hitCells, HitType.Armor, _hitSequence++));
            RefreshCells(hitCells);
        }));
        
        _subscriptions.Add(entity.ItemDamage.Where(hit=>hit.damage > HitDamageThreshold).Subscribe(hit =>
        {
            var hitCells = hit.item.InsetShape.Coordinates;
            if(gameObject.activeInHierarchy)
                StartCoroutine(Pulse(hitCells, HitType.Armor, _hitSequence++));
            RefreshCells(hitCells);
        }));
    }

    private IEnumerator Pulse(int2[] cells, HitType hitType, int sequence)
    {
        foreach (var cell in cells)
        {
            if(_cellAnimationSequence.ContainsKey(cell))
                _cellAnimationSequence[cell] = max(_cellAnimationSequence[cell], sequence);
            else
                _cellAnimationSequence[cell] = sequence;
        }
        var hitColor = hitType switch
        {
            HitType.Armor => Settings.ArmorHitColor,
            HitType.Hardpoint => Settings.HardpointHitColor,
            HitType.Gear => Settings.GearHitColor,
            _ => Color.white
        };
        float startTime = Time.time;
        while (Time.time - startTime < CellHitPulseTime)
        {
            var lerp = (Time.time - startTime) / CellHitPulseTime;
            foreach(var cell in cells)
                if(_cellAnimationSequence[cell] == sequence)
                    if(CellInstances.ContainsKey(cell))
                        CellInstances[cell].Background.color = Color.Lerp(hitColor, CellBackgroundColor, lerp);
            yield return null;
        }
        foreach (var cell in cells)
            if(CellInstances.ContainsKey(cell))
                CellInstances[cell].Background.color = CellBackgroundColor;
    }

    public void Display(EquippedCargoBay cargo)
    {
        Clear();
        
        _subscriptions.Add(cargo.Cargo.ObserveAdd().Subscribe(_ => RefreshCells()));
        _subscriptions.Add(cargo.Cargo.ObserveRemove().Subscribe(_ => RefreshCells()));
        
        _displayedCargo = cargo;
        _displayedEntity = null;
        _displayedEntityKey = "";
        if (TryResolveCargoBay(cargo, out var displayedCargoEntityKey, out var displayedCargoIndex))
        {
            _displayedCargoEntityKey = displayedCargoEntityKey;
            _displayedCargoIndex = displayedCargoIndex;
        }
        else
        {
            _displayedCargoEntityKey = "";
            _displayedCargoIndex = -1;
        }

        if(Title)
            Title.text = cargo.Name;
        
        // FakeOccupancy = new Shape(cargo.InteriorShape.Width, cargo.InteriorShape.Height);
        // IgnoreOccupancy = new Shape(cargo.InteriorShape.Width, cargo.InteriorShape.Height);
        Grid.constraintCount = cargo.InteriorShape.Width;
        foreach (var v in cargo.InteriorShape.AllCoordinates)
        {
            if (!cargo.InteriorShape[v])
            {
                var empty = new GameObject("Empty Node", typeof(RectTransform));
                empty.transform.SetParent(Grid.transform);
                EmptyCells.Add(empty);
            }
            else
            {
                var cell = NodePrototype.Instantiate<InventoryCell>();
                cell.PointerClickTrigger.OnPointerClickAsObservable()
                    .Subscribe(data =>
                    {
                        if (cell != _clickCell || Time.time - _clickTime > DoubleClickTime) 
                            _clickCount = 0;
                        _clickCell = cell;
                        _clickTime = Time.time;
                        _clickCount++;
                        _onClick?.OnNext((new InventoryCargoEventData(data, v, cargo), _clickCount));
                    });
                cell.BeginDragTrigger.OnBeginDragAsObservable()
                    .Subscribe(data =>
                    {
                        //Debug.Log("Inventory Drag Start");
                        var item = cargo.Occupancy[v.x, v.y];
                        if (item != null)
                        {
                            var itemPosition = cargo.Cargo[item];
                            var originalOccupancy = cargo.InteriorShape.Inset(GetItemShape(item), itemPosition, item.Rotation);
                            _dragCells = originalOccupancy.Coordinates
                                .Select(v1 => Instantiate(CellInstances[v1], DragParent, true).transform).ToArray();
                            foreach(var dragCell in _dragCells)
                            {
                                DestroyImmediate(dragCell.GetComponent<Prototype>());
                                foreach (var component in dragCell.GetComponentsInChildren<ObservableTriggerBase>())
                                    component.enabled = false;
                                foreach (var img in dragCell.GetComponentsInChildren<Image>())
                                    img.color = new Color(img.color.r, img.color.g, img.color.b, img.color.a * .5f);
                                dragCell.GetComponentInChildren<Image>().raycastTarget = false;
                            }
                            _dragOffsets = _dragCells.Select(x => (Vector2) x.position - data.position).ToArray();
                            IgnoreOccupancy = originalOccupancy;
                            RefreshCells();
                            _originalRotation = item.Rotation;
                            DragSession.Begin(new ItemInstanceDragObject(item, cargo, cargo.Cargo[item] - v));
                            // TODO: SFX: Pickup
                        }
                    });
                cell.DragTrigger.OnDragAsObservable()
                    .Subscribe(data =>
                    {
                        for (var i = 0; i < _dragCells.Length; i++)
                            _dragCells[i].position = new Vector3(
                                data.position.x + _dragOffsets[i].x,
                                data.position.y + _dragOffsets[i].y,
                                _dragCells[i].position.z);
                    });
                cell.EndDragTrigger.OnEndDragAsObservable()
                    .Subscribe(data =>
                    {
                        //Debug.Log("Inventory Drag End");
                        IgnoreOccupancy = null;
                        DragSession.End();
                        foreach(var dragObject in _dragCells)
                            Destroy(dragObject.gameObject);
                        _dragCells = null;
                        RefreshCells();
                    });
                cell.PointerEnterTrigger.OnPointerEnterAsObservable()
                    .Subscribe(data =>
                    {
                        //Debug.Log("Inventory Pointer Enter");
                        if (!DragSession.TryGetDraggedItem(out var itemDragObject)) return;
                        var item = itemDragObject.Item;
                        var placementPosition = v + itemDragObject.OriginCellOffset;
                        //foreach (var cell in _dragCells) cell.gameObject.SetActive(false);
                        FakeItem = item;
                        FakeOccupancy = cargo.InteriorShape.Inset(GetItemShape(item), placementPosition, item.Rotation);
                        RefreshCells();
                        DragSession.RegisterTarget(drag =>
                        {
                            //Debug.Log("Inventory Drag Callback");
                            FakeOccupancy = null;
                            RequestDraggedItemToCargo(drag, cargo, placementPosition);
                            // TODO: SFX: Drop
                        });
                    });
                cell.PointerExitTrigger.OnPointerExitAsObservable()
                    .Subscribe(data =>
                    {
                        if (!DragSession.TryGetDraggedItem(out _)) return;
                        FakeItem = null;
                        FakeOccupancy = null;
                        RefreshCells();
                        DragSession.UnregisterTarget();
                    });
                
                CellInstances.Add(v, cell);
            }
        }
        RefreshCells();
    }

    public void RefreshCells()
    {
        RefreshCells(CellInstances.Keys);
    }

    public void RefreshCells(IEnumerable<int2> cells)
    {
        if (_displayedEntity != null)
        {
            foreach (var v in cells)
            {
                if(!CellInstances.ContainsKey(v)) continue;
                
                var spriteIndex = 0;
                var interior = _displayedHullInterior[v];
                var item = FakeOccupancy?[v]??false ? FakeItem : IgnoreOccupancy?[v]??false ? null : _displayedEntity.GearOccupancy[v.x, v.y]?.EquippableItem;
                var hardpoint = _displayedEntity.Hardpoints[v.x, v.y];

                bool HardpointMatch(int2 offset)
                {
                    var v2 = v + offset * (Flip ? -1 : 1);
                    return !(
                        _displayedHullShape[v2] &&
                        _displayedEntity.Hardpoints[v2.x, v2.y] == hardpoint &&
                        (FakeOccupancy?[v2]??false ? FakeItem : IgnoreOccupancy?[v2]??false ? null : _displayedEntity.GearOccupancy[v2.x, v2.y]?.EquippableItem) == item
                    );
                }

                bool NoHardpointMatch(int2 offset)
                {
                    var v2 = v + offset * (Flip ? -1 : 1);
                    return !(
                        _displayedHullShape[v2] && (
                            !interior && !_displayedHullInterior[v2] && _displayedEntity.Hardpoints[v2.x, v2.y] == null ||
                            interior && item != null && 
                            (FakeOccupancy?[v2]??false ? FakeItem : IgnoreOccupancy?[v2]??false ? null : _displayedEntity.GearOccupancy[v2.x, v2.y]?.EquippableItem) == item
                        )
                    );
                }

                if (hardpoint != null)
                {
                    for(int i = 0; i < 8; i++)
                        if (HardpointMatch(_offsets[i]))
                            spriteIndex += 1 << i;
                }
                else
                {
                    for(int i = 0; i < 8; i++)
                        if (NoHardpointMatch(_offsets[i]))
                            spriteIndex += 1 << i;
                }

                var bgSpriteIndex = spriteIndex;

                if (item != null)
                    spriteIndex += 1 << 8;

                if (_thermal)
                {
                    bool ThermalMatch(int2 offset)
                    {
                        if (!_displayedHullShape[v + offset]) return false;
                        var i = (offset.x, offset.y);
                        return i switch
                        {
                            (1, 0) => _displayedEntity.HullConductivity[v.x, v.y].x,
                            (-1, 0) => _displayedEntity.HullConductivity[v.x-1, v.y].x,
                            (0, 1) => _displayedEntity.HullConductivity[v.x, v.y].y,
                            (0, -1) => _displayedEntity.HullConductivity[v.x, v.y-1].y,
                            _ => false
                        };
                    }
                    spriteIndex = 0;
                    for(int i = 0; i < 4; i++)
                        if (ThermalMatch(_offsets[i]))
                            spriteIndex += 1 << i;
                }
            
                CellInstances[v].Background.sprite = NodeBackgroundTextures[bgSpriteIndex];
                CellInstances[v].Icon.sprite = _thermal ? ThermalTextures[spriteIndex] : NodeTextures[spriteIndex];
                CellInstances[v].Icon.color = GetColor(v);
            }
        }
        else if(_displayedCargo != null)
        {
            foreach (var v in cells)
            {
                if(!CellInstances.ContainsKey(v)) continue;
                
                var spriteIndex = 0;
                var item = FakeOccupancy?[v]??false ? FakeItem : IgnoreOccupancy?[v]??false ? null : _displayedCargo.Occupancy[v.x, v.y];

                bool ItemMatch(int2 offset)
                {
                    var v2 = v + offset;
                    return !_displayedCargo.InteriorShape[v2] || item == null ||
                           (FakeOccupancy?[v2]??false ? FakeItem : IgnoreOccupancy?[v2]??false ? null : _displayedCargo.Occupancy[v2.x, v2.y]) != item;
                }

                for(int i = 0; i < 8; i++)
                    if (ItemMatch(_offsets[i]))
                        spriteIndex += 1 << i;
                
                var bgSpriteIndex = spriteIndex;

                if (item != null)
                    spriteIndex += 1 << 8;
            
                CellInstances[v].Background.sprite = NodeBackgroundTextures[bgSpriteIndex];
                CellInstances[v].Icon.sprite = NodeTextures[spriteIndex];
                CellInstances[v].Icon.color = GetColor(v);
            }
        }
    }

    public Color GetColor(int2 position, bool highlight = false)
    {
        if (_displayedEntity != null)
        {
            var interior = _displayedHullInterior[position];
            var hardpoint = _displayedEntity.Hardpoints[position.x, position.y];
            var item = (FakeOccupancy?[position] ?? false ? FakeItem :
                IgnoreOccupancy?[position] ?? false ? null : _displayedEntity.GearOccupancy[position.x, position.y]?.EquippableItem) as EquippableItem;

            if (_hud)
            {
                if (_displayedEntity.Armor[position.x, position.y] > .01f)
                    return Settings.ArmorGradient.Evaluate(_displayedEntity.Armor[position.x, position.y] / _displayedEntity.MaxArmor[position.x, position.y]);

                if (item != null) return Settings.DurabilityGradient.Evaluate(item.Durability / GetMaxDurability(item));
                return float3(.25f).ToColor();
            }

            if (hardpoint == null)
            {
                if(!interior)
                    return Color.white;
                
                if (item == null)
                {
                    return float3(.25f).ToColor();
                }
                
                if(highlight)
                    return Color.white;
                
                return float3(.5f).ToColor();
            }

            var tint = hardpoint.TintColor;
            
            if (!highlight || item == null)
                tint *= .7071f;

            return tint.ToColor();
        }
        else
        {
            var item = FakeOccupancy?[position] ?? false ? FakeItem : IgnoreOccupancy?[position] ?? false ? null : _displayedCargo.Occupancy[position.x, position.y];
        
            if (item == null)
                return Color.white * .25f;

            var c = float3(1);
            if (FindTypedInventoryItem(item)?.TryGetHardpointType(out HardpointType typedHardpoint) == true)
                c = HardpointData.GetColor(typedHardpoint).ToUnityFloat3();
            
            if(!highlight)
                c *= .7071f;

            return c.ToColor();
        }
    }

    private float GetMaxDurability(ItemInstance item)
    {
        var typedItem = FindTypedInventoryItem(item);
        if (typedItem != null && typedItem.Durability > 0)
            return (float)typedItem.Durability;

        return item is EquippableItem equippable ? Math.Max(equippable.Durability, 1f) : 1f;
    }

    private Shape GetItemShape(ItemInstance item)
    {
        var typedItem = FindTypedInventoryItem(item);
        if (typedItem != null && typedItem.ShapeCells.Count > 0)
            return ToShape(typedItem.ShapeWidth, typedItem.ShapeHeight, typedItem.ShapeCells);

        return new Shape(1, 1);
    }

    private bool TryResolveTypedHullGeometry(ItemInstance hull, out Shape hullShape, out Shape interiorShape)
    {
        hullShape = null;
        interiorShape = null;
        var typedHull = FindTypedInventoryItem(hull);
        if (typedHull == null ||
            typedHull.ShapeCells.Count == 0 ||
            typedHull.InteriorShapeCells.Count == 0 ||
            typedHull.ShapeWidth <= 0 ||
            typedHull.ShapeHeight <= 0 ||
            typedHull.InteriorShapeWidth <= 0 ||
            typedHull.InteriorShapeHeight <= 0)
        {
            return false;
        }

        hullShape = ToShape(typedHull.ShapeWidth, typedHull.ShapeHeight, typedHull.ShapeCells);
        interiorShape = ToShape(typedHull.InteriorShapeWidth, typedHull.InteriorShapeHeight, typedHull.InteriorShapeCells);
        return true;
    }

    private static Shape ToShape(int width, int height, IReadOnlyList<AetheriaRuntimeShapeCell> cells)
    {
        var shape = new Shape(Math.Max(width, 1), Math.Max(height, 1));
        foreach (var cell in cells)
        {
            if (cell.X >= 0 && cell.Y >= 0 && cell.X < shape.Width && cell.Y < shape.Height)
                shape[int2(cell.X, cell.Y)] = true;
        }

        return shape;
    }

    private AetheriaRuntimeCatalogItem FindTypedInventoryItem(ItemInstance item)
    {
        return ResolveCatalog()?.FindItem(item, x => x.ItemKey);
    }

    private void RequestDockedCurrentShip(Ship ship)
    {
        if (ship == null || !TryResolveEntityRecordKey(ship, out var targetEntityKey))
            return;

        TrySubmitOperation(
            operations => operations.SetDockedCurrentShip(targetEntityKey),
            "docked current ship");
    }

    private void RequestEntityName(Entity entity, string name)
    {
        if (entity == null || !TryResolveEntityRecordKey(entity, out var targetEntityKey))
            return;

        TrySubmitOperation(
            operations => operations.SetEntityName(targetEntityKey, name ?? ""),
            "entity name");
    }

    private void RequestRuntimeLoadoutRestore(AetheriaRuntimeStationLoadoutRestoreOption loadout)
    {
        if (loadout == null ||
            !loadout.CanRestore ||
            string.IsNullOrWhiteSpace(loadout.TargetEntityKey) ||
            string.IsNullOrWhiteSpace(loadout.TemplateName))
        {
            return;
        }

        TrySubmitOperation(
            operations => operations.RestoreLoadout(loadout.TargetEntityKey, loadout.TemplateName, loadout.Price),
            "loadout restore");
    }

    private void RequestLoadoutTemplateSave(Entity entity)
    {
        if (entity == null || !TryResolveEntityRecordKey(entity, out var targetEntityKey))
            return;

        try
        {
            var client = ResolveClient();
            var loadout = CreateLoadoutTemplate(targetEntityKey);
            if (loadout?.RootEntity == null || string.IsNullOrWhiteSpace(loadout.RootEntity.Hull?.ItemKey ?? ""))
                return;

            var submitted = client
                .Ui.SaveLoadoutTemplateAsync(loadout, "unity-inventory")
                .GetAwaiter()
                .GetResult();

            Debug.Log($"Submitted Aetheria loadout template save Eve operation: {submitted.OperationId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send Aetheria loadout template save Eve command: {ex}");
        }
    }

    private void RequestHullConductivityToggle(Entity entity, int2 position, int axis)
    {
        if (entity == null || !TryResolveEntityRecordKey(entity, out var targetEntityKey))
            return;

        TrySubmitOperation(
            operations => operations.ToggleHullConductivity(targetEntityKey, position.x, position.y, axis),
            "hull conductivity");
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

    private bool TryResolveCurrentEntityKey(out string currentEntityKey)
    {
        currentEntityKey = ResolveCurrentEntity()?.EntityKey ?? "";
        return !string.IsNullOrWhiteSpace(currentEntityKey);
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

    private AetheriaRuntimeStationRefitDocument ResolveStationRefit()
    {
        return ResolveStationRefitDocument();
    }

    private bool TryResolveObservedDockingIndex(out AetheriaUnityObservedDockingIndex dockingIndex)
    {
        dockingIndex = null;
        if (_observedEntityIndex == null)
            return false;

        dockingIndex = _observedDockingIndex ??= new AetheriaUnityObservedDockingIndex(ResolveClient, _observedEntityIndex);
        return true;
    }

    private bool TryResolveStationRefitEntity(string entityKey, int optionIndex, out Entity entity)
    {
        entity = null;
        if (string.IsNullOrWhiteSpace(entityKey) &&
            optionIndex >= 0 &&
            optionIndex < (_dropdownStationRefitEntities?.Length ?? 0))
        {
            entityKey = _dropdownStationRefitEntities[optionIndex]?.EntityKey ?? "";
        }

        return TryResolveObservedAvailableEntityByKey(entityKey, out entity);
    }

    private bool TryResolveObservedAvailableEntityByKey(string entityKey, out Entity entity)
    {
        entity = null;
        if (_observedEntityIndex == null || string.IsNullOrWhiteSpace(entityKey))
            return false;

        return _observedEntityIndex.TryResolveEntityByRecordKey(entityKey, out entity);
    }

    private bool IsDisplayedEntityKey(string entityKey)
    {
        return _displayedEntity != null &&
               !string.IsNullOrWhiteSpace(_displayedEntityKey) &&
               string.Equals(_displayedEntityKey, entityKey, StringComparison.Ordinal);
    }

    private bool IsDisplayedCargoBayKey(string entityKey, int cargoBayIndex)
    {
        return _displayedCargo != null &&
               !string.IsNullOrWhiteSpace(_displayedCargoEntityKey) &&
               string.Equals(_displayedCargoEntityKey, entityKey, StringComparison.Ordinal) &&
               _displayedCargoIndex == cargoBayIndex;
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
            Debug.LogWarning($"Failed to validate Aetheria inventory projection for {entityKey}: {ex.Message}");
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

    private void RequestCargoItemTransfer(
        EquippedCargoBay origin,
        EquippedCargoBay destination,
        ItemInstance item,
        int2 destinationPosition,
        bool hasDestinationPosition)
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
                destinationPosition.x,
                destinationPosition.y,
                hasDestinationPosition),
            "cargo transfer");
    }

    private void RequestCargoItemEquip(
        EquippedCargoBay origin,
        Entity destination,
        EquippableItem item,
        int2 destinationPosition,
        bool hasDestinationPosition)
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
                destinationPosition.x,
                destinationPosition.y,
                hasDestinationPosition),
            "cargo equip");
    }

    private void RequestEquippedItemStore(
        EquippedItem item,
        EquippedCargoBay destination,
        int2 destinationPosition,
        bool hasDestinationPosition)
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
                destinationPosition.x,
                destinationPosition.y,
                hasDestinationPosition),
            "equipment store");
    }

    private void RequestEquippedItemEquip(
        EquippedItem item,
        Entity destination,
        int2 destinationPosition)
    {
        if (!TryResolveEquippedItem(item, out var originEntityKey, out var equipmentIndex) ||
            !TryResolveEntityRecordKey(destination, out var destinationEntityKey) ||
            item?.EquippableItem == null ||
            !TryValidateTypedEquipmentSlot(originEntityKey, equipmentIndex, item))
        {
            return;
        }

        TrySubmitOperation(
            operations => operations.EquipItem(
                "equipment",
                originEntityKey,
                equipmentIndex,
                destinationEntityKey,
                item.EquippableItem.ItemKey,
                item.Position.x,
                item.Position.y,
                destinationPosition.x,
                destinationPosition.y,
                true),
            "equipment equip");
    }

    private bool TrySubmitOperation(
        Action<AetheriaControl> submit,
        string label)
    {
        if (submit == null)
            return false;

        try
        {
            submit(ResolveClient().Control);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon inventory {label} operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private AetheriaClient ResolveClient()
    {
        var stateBoot = AetheriaRuntimeStateBoot.Inspect(AetheriaUnityRuntimePaths.GameDataDirectory);
        if (!string.Equals(_clientStatePath, stateBoot.StateFilePath, StringComparison.Ordinal))
        {
            _clientStatePath = stateBoot.StateFilePath;
            ClearClientCaches();
        }

        return AetheriaUnityRuntimeClientProvider.ResolveClient(stateBoot, "unity-inventory");
    }

    private void ClearClientCaches()
    {
        _catalog?.Dispose();
        _playerSettings?.Dispose();
        _currentEntity?.Dispose();
        _stationRefit?.Dispose();
        _loadoutFrame?.Dispose();
        _observedDockingIndex?.Dispose();
        _inventory?.Dispose();
        _catalog = null;
        _playerSettings = null;
        _currentEntity = null;
        _stationRefit = null;
        _loadoutFrame = null;
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
            _currentEntity = ResolveClient()
                .State.Reactive<AetheriaRuntimeCurrentEntityDocument>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria current entity for inventory panel: {ex.Message}");
        }

        return _currentEntity?.Current;
    }

    private AetheriaRuntimeStationRefitDocument ResolveStationRefitDocument()
    {
        if (_stationRefit != null)
            return _stationRefit.Current;

        try
        {
            _stationRefit = ResolveClient()
                .State.Reactive<AetheriaRuntimeStationRefitDocument>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria station refit for inventory panel: {ex.Message}");
        }

        return _stationRefit?.Current;
    }

    private AetheriaRuntimeLoadoutTemplateCommit CreateLoadoutTemplate(string targetEntityKey)
    {
        var frame = ResolveLoadoutFrame();
        return (frame?.Run ?? new AetheriaRuntimeRunCheckpointCommit())
            .CreateLoadoutTemplate(targetEntityKey ?? "");
    }

    private AetheriaRuntimeDaemonFrameDocument ResolveLoadoutFrame()
    {
        if (_loadoutFrame != null)
            return _loadoutFrame.Current;

        try
        {
            _loadoutFrame = ResolveClient()
                .State.Reactive<AetheriaRuntimeDaemonFrameDocument>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria daemon frame for loadout template save: {ex.Message}");
        }

        return _loadoutFrame?.Current;
    }

    private AetheriaRuntimeInventoryDocument ResolveInventory(int entityIndex)
    {
        if (_inventory != null && _inventoryEntityIndex == entityIndex)
            return _inventory.Current;

        try
        {
            var nextInventory = ResolveClient()
                .State.Reactive<AetheriaRuntimeInventoryDocument>(AetheriaClientState.Entity(entityIndex));
            _inventory?.Dispose();
            _inventoryEntityIndex = entityIndex;
            _inventory = nextInventory;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria inventory projection for entity {entityIndex}: {ex.Message}");
        }

        return _inventory?.Current;
    }

    private AetheriaRuntimeCatalogSnapshot ResolveCatalog()
    {
        if (_catalog != null)
            return _catalog.Current;

        try
        {
            _catalog = ResolveClient().State.Reactive<AetheriaRuntimeCatalogSnapshot>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria runtime catalog for inventory panel: {ex.Message}");
        }

        return _catalog?.Current;
    }

    private AetheriaRuntimePlayerSettingsDocument ResolvePlayerSettings()
    {
        if (_playerSettings != null)
            return _playerSettings.Current;

        try
        {
            _playerSettings = ResolveClient()
                .State.Reactive<AetheriaRuntimePlayerSettingsDocument>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to bind Aetheria player settings for inventory panel: {ex.Message}");
        }

        return _playerSettings?.Current;
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

    private void RequestDraggedItemToEntity(DragObject drag, Entity destination, int2 destinationPosition)
    {
        switch (drag)
        {
            case EquippedItemDragObject equippedItemDragObject:
                RequestEquippedItemEquip(
                    equippedItemDragObject.EquippedItem,
                    destination,
                    destinationPosition);
                return;
            case ItemInstanceDragObject itemInstanceDragObject when itemInstanceDragObject.Item is EquippableItem equippableItem:
                RequestCargoItemEquip(
                    itemInstanceDragObject.OriginInventory,
                    destination,
                    equippableItem,
                    destinationPosition,
                    true);
                return;
        }
    }

    private void RequestDraggedItemToCargo(DragObject drag, EquippedCargoBay destination, int2 destinationPosition)
    {
        switch (drag)
        {
            case EquippedItemDragObject equippedItemDragObject:
                RequestEquippedItemStore(
                    equippedItemDragObject.EquippedItem,
                    destination,
                    destinationPosition,
                    true);
                return;
            case ItemInstanceDragObject itemInstanceDragObject:
                RequestCargoItemTransfer(
                    itemInstanceDragObject.OriginInventory,
                    destination,
                    itemInstanceDragObject.Item,
                    destinationPosition,
                    true);
                return;
        }
    }

    private void OnDestroy()
    {
        ClearClientCaches();

        if (_dropdownSurfaceDocument != null)
        {
            AetheriaEveUnitySurfaceHost.DestroyDocument(_dropdownSurfaceDocument);
            _dropdownSurfaceDocument = null;
        }
    }

    public Subject<PointerEventData> OnBackgroundClick = new Subject<PointerEventData>();
    private Transform[] _dragCells;
    private Vector2[] _dragOffsets;
    private ItemRotation _originalRotation;

    public Subject<(InventoryEventData data, int clickCount)> OnClickAsObservable() => _onClick ?? (_onClick = new Subject<(InventoryEventData data, int clickCount)>());
    // public UniRx.IObservable<InventoryEventData> OnBeginDragAsObservable() => _onBeginDrag ?? (_onBeginDrag = new Subject<InventoryEventData>());
    // public UniRx.IObservable<InventoryEventData> OnDragAsObservable() => _onDrag ?? (_onDrag = new Subject<InventoryEventData>());
    // public UniRx.IObservable<InventoryEventData> OnEndDragAsObservable() => _onEndDrag ?? (_onEndDrag = new Subject<InventoryEventData>());
    // public UniRx.IObservable<InventoryEventData> OnPointerEnterAsObservable() => _onPointerEnter ?? (_onPointerEnter = new Subject<InventoryEventData>());
    // public UniRx.IObservable<InventoryEventData> OnPointerExitAsObservable() => _onPointerExit ?? (_onPointerExit = new Subject<InventoryEventData>());
    
    public void OnPointerClick(PointerEventData eventData)
    {
        OnBackgroundClick.OnNext(eventData);
    }
}

public abstract class InventoryEventData
{
    protected InventoryEventData(PointerEventData pointerEventData, int2 position)
    {
        PointerEventData = pointerEventData;
        Position = position;
    }

    public PointerEventData PointerEventData { get; }
    public int2 Position { get; }
}

public class InventoryEntityEventData : InventoryEventData
{
    public InventoryEntityEventData(PointerEventData pointerEventData, int2 position, Entity entity) : 
        base(pointerEventData, position)
    {
        Entity = entity;
    }

    public Entity Entity { get; }
}

public class InventoryCargoEventData : InventoryEventData
{
    public InventoryCargoEventData(PointerEventData pointerEventData, int2 position, EquippedCargoBay cargoBay) : 
        base(pointerEventData, position)
    {
        CargoBay = cargoBay;
    }

    public EquippedCargoBay CargoBay { get; }
}

public enum InventoryPanelTarget
{
    None,
    Cargo,
    Equipment
}

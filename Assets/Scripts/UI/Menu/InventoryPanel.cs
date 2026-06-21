/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.EveRuntime;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
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
    public ActionGameManager GameManager;
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
    private Shape _displayedHullShape;
    private Shape _displayedHullInterior;
    private Texture2D _temperatureTexture;
    private RectTransform _firstRect;

    public ItemInstance FakeItem;
    public Shape FakeOccupancy;
    public Shape IgnoreOccupancy;
    public List<GameObject> EmptyCells = new List<GameObject>();
    public Dictionary<int2, InventoryCell> CellInstances = new Dictionary<int2, InventoryCell>();
    
    private List<IDisposable> _subscriptions = new List<IDisposable>();
    private Dictionary<int2, int> _cellAnimationSequence = new Dictionary<int2, int>();
    private int _hitSequence = 0;
    private bool _thermal = false;
    private int _clickCount;
    private InventoryCell _clickCell;
    private float _clickTime;
    private readonly Dictionary<string, Action> _dropdownCommands = new Dictionary<string, Action>(StringComparer.Ordinal);
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
                if (_displayedEntity != GameManager.CurrentEntity)
                {
                    if(_displayedEntity is Ship ship)
                    {
                        GameManager.RequestDockedCurrentShip(ship);
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
                    GameManager.RequestEntityName(_displayedEntity, entityName);
                    Title.text = _displayedEntity.Name;
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

    private (string text, Action action, bool enabled) LoadoutOption(AetheriaRuntimeLoadoutTemplateSnapshot template)
    {
        var blueprint = GameManager.CreateEntityConstructionBlueprint(template);
        if (blueprint == null)
            return ($"{template.Name} - unavailable", () => { }, false);

        var price = blueprint.Price(GameManager.ItemManager);
        return ($"{template.Name} - {price:n0}", () =>
        {
            GameManager.RequestRuntimeLoadoutRestore(template);
        }, true);
    }

    private void RenderDropdownSurface()
    {
        BuildDropdownCommands();

        _dropdownSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _dropdownSurfaceDocument,
            "Aetheria Inventory Dropdown Surface",
            AetheriaRuntimeInventoryDropdownSurfaceBuilder.Build(ProjectDropdownSurfaceState()),
            HandleDropdownSurfaceCommand,
            _dropdownSurfaceChrome);
    }

    private void BuildDropdownCommands()
    {
        _dropdownCommands.Clear();

        foreach (var entityEntry in GameManager.AvailableEntities().Select((entity, entityIndex) => (entity, entityIndex)))
        {
            if (entityEntry.entity.CargoBays.Any())
            {
                if (entityEntry.entity != _displayedEntity)
                {
                    var equipmentCommand = AetheriaRuntimeInventoryDropdownSurfaceBuilder.EntityEquipmentCommand(entityEntry.entityIndex);
                    _dropdownCommands[equipmentCommand] = () => Display(entityEntry.entity);
                }

                foreach (var bayEntry in entityEntry.entity.CargoBays
                             .Where(bay => bay != _displayedCargo)
                             .Select((bay, bayIndex) => (bay, bayIndex)))
                {
                    var bayCommand = AetheriaRuntimeInventoryDropdownSurfaceBuilder.EntityBayCommand(
                        entityEntry.entityIndex,
                        bayEntry.bayIndex);
                    _dropdownCommands[bayCommand] = () => Display(bayEntry.bay);
                }
            }
            else if (entityEntry.entity != _displayedEntity)
            {
                var entityCommand = AetheriaRuntimeInventoryDropdownSurfaceBuilder.EntityCommand(entityEntry.entityIndex);
                _dropdownCommands[entityCommand] = () => Display(entityEntry.entity);
            }
        }

        if (GameManager.DockingBay != null && _displayedCargo != GameManager.DockingBay)
        {
            _dropdownCommands[AetheriaRuntimeInventoryDropdownSurfaceBuilder.DockingBay] = () => Display(GameManager.DockingBay);
        }

        if (_displayedEntity != null)
        {
            _dropdownCommands[AetheriaRuntimeInventoryDropdownSurfaceBuilder.SaveLoadout] = () =>
            {
                GameManager.RequestLoadoutTemplateSave(EntityConstructionBlueprintProjector.CaptureBlueprint(_displayedEntity));
            };
        }

        foreach (var loadoutEntry in GameManager.LoadoutTemplates.Select((template, templateIndex) => (template, templateIndex)))
        {
            var blueprint = GameManager.CreateEntityConstructionBlueprint(loadoutEntry.template);
            if (blueprint == null)
                continue;

            var restoreCommand = AetheriaRuntimeInventoryDropdownSurfaceBuilder.LoadoutCommand(loadoutEntry.templateIndex);
            _dropdownCommands[restoreCommand] = () =>
            {
                GameManager.RequestRuntimeLoadoutRestore(loadoutEntry.template);
            };
        }
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
            _dropdownCommands.TryGetValue(command.Command, out var action))
        {
            action();
            HideDropdownSurface();
            return;
        }

        Debug.LogWarning($"Unknown inventory dropdown command: {request?.Command}");
    }

    private void HideDropdownSurface()
    {
        if (_dropdownSurfaceDocument == null)
            return;

        AetheriaEveUnitySurfaceHost.Hide(_dropdownSurfaceDocument);
    }

    private AetheriaRuntimeInventoryDropdownSurfaceState ProjectDropdownSurfaceState()
    {
        var groups = new List<AetheriaRuntimeInventoryDropdownGroup>();

        foreach (var entityEntry in GameManager.AvailableEntities().Select((entity, entityIndex) => (entity, entityIndex)))
        {
            var options = new List<AetheriaRuntimeInventoryDropdownOption>();
            var equipmentCommand = AetheriaRuntimeInventoryDropdownSurfaceBuilder.EntityEquipmentCommand(entityEntry.entityIndex);
            if (_dropdownCommands.ContainsKey(equipmentCommand))
            {
                options.Add(new AetheriaRuntimeInventoryDropdownOption(
                    $"{AetheriaRuntimeInventoryDropdownSurfaceBuilder.SurfaceId}.entity_{entityEntry.entityIndex}.equipment",
                    "Equipment",
                    equipmentCommand));
            }

            foreach (var bayEntry in entityEntry.entity.CargoBays.Select((bay, bayIndex) => (bay, bayIndex)))
            {
                var bayCommand = AetheriaRuntimeInventoryDropdownSurfaceBuilder.EntityBayCommand(
                    entityEntry.entityIndex,
                    bayEntry.bayIndex);
                if (_dropdownCommands.ContainsKey(bayCommand))
                {
                    options.Add(new AetheriaRuntimeInventoryDropdownOption(
                        $"{AetheriaRuntimeInventoryDropdownSurfaceBuilder.SurfaceId}.entity_{entityEntry.entityIndex}.bay_{bayEntry.bayIndex}",
                        $"Bay {bayEntry.bayIndex + 1}",
                        bayCommand));
                }
            }

            var entityCommand = AetheriaRuntimeInventoryDropdownSurfaceBuilder.EntityCommand(entityEntry.entityIndex);
            if (_dropdownCommands.ContainsKey(entityCommand))
            {
                options.Add(new AetheriaRuntimeInventoryDropdownOption(
                    $"{AetheriaRuntimeInventoryDropdownSurfaceBuilder.SurfaceId}.entity_{entityEntry.entityIndex}.select",
                    entityEntry.entity.Name,
                    entityCommand));
            }

            if (options.Count > 0)
            {
                groups.Add(new AetheriaRuntimeInventoryDropdownGroup(
                    $"{AetheriaRuntimeInventoryDropdownSurfaceBuilder.SurfaceId}.entity_{entityEntry.entityIndex}.card",
                    entityEntry.entity.Name,
                    options));
            }
        }

        if (_dropdownCommands.ContainsKey(AetheriaRuntimeInventoryDropdownSurfaceBuilder.DockingBay))
        {
            groups.Add(new AetheriaRuntimeInventoryDropdownGroup(
                $"{AetheriaRuntimeInventoryDropdownSurfaceBuilder.SurfaceId}.dockingBay.card",
                "Docking Bay",
                new[]
                {
                    new AetheriaRuntimeInventoryDropdownOption(
                        $"{AetheriaRuntimeInventoryDropdownSurfaceBuilder.SurfaceId}.dockingBay.select",
                        GameManager.DockingBay.Name,
                        AetheriaRuntimeInventoryDropdownSurfaceBuilder.DockingBay)
                }));
        }

        var loadoutOptions = new List<AetheriaRuntimeInventoryDropdownOption>();
        if (_dropdownCommands.ContainsKey(AetheriaRuntimeInventoryDropdownSurfaceBuilder.SaveLoadout))
        {
            loadoutOptions.Add(new AetheriaRuntimeInventoryDropdownOption(
                $"{AetheriaRuntimeInventoryDropdownSurfaceBuilder.SurfaceId}.loadouts.save",
                "Save Loadout",
                AetheriaRuntimeInventoryDropdownSurfaceBuilder.SaveLoadout));
        }

        foreach (var loadoutEntry in GameManager.LoadoutTemplates.Select((template, templateIndex) => (template, templateIndex)))
        {
            var restoreCommand = AetheriaRuntimeInventoryDropdownSurfaceBuilder.LoadoutCommand(loadoutEntry.templateIndex);
            var blueprint = GameManager.CreateEntityConstructionBlueprint(loadoutEntry.template);
            if (blueprint == null)
            {
                loadoutOptions.Add(new AetheriaRuntimeInventoryDropdownOption(
                    $"{AetheriaRuntimeInventoryDropdownSurfaceBuilder.SurfaceId}.loadouts.unavailable_{loadoutEntry.templateIndex}",
                    $"{loadoutEntry.template.Name} - unavailable",
                    ""));
                continue;
            }

            var price = blueprint.Price(GameManager.ItemManager);
            if (_dropdownCommands.ContainsKey(restoreCommand))
            {
                loadoutOptions.Add(new AetheriaRuntimeInventoryDropdownOption(
                    $"{AetheriaRuntimeInventoryDropdownSurfaceBuilder.SurfaceId}.loadouts.restore_{loadoutEntry.templateIndex}",
                    $"{loadoutEntry.template.Name} - {price:n0}",
                    restoreCommand));
            }
            else
            {
                loadoutOptions.Add(new AetheriaRuntimeInventoryDropdownOption(
                    $"{AetheriaRuntimeInventoryDropdownSurfaceBuilder.SurfaceId}.loadouts.locked_{loadoutEntry.templateIndex}",
                    $"{loadoutEntry.template.Name} - {price:n0} (unavailable)",
                    ""));
            }
        }

        if (loadoutOptions.Count > 0)
        {
            groups.Add(new AetheriaRuntimeInventoryDropdownGroup(
                $"{AetheriaRuntimeInventoryDropdownSurfaceBuilder.SurfaceId}.loadouts.card",
                "Loadouts",
                loadoutOptions));
        }

        return new AetheriaRuntimeInventoryDropdownSurfaceState(
            Title?.text ?? "None",
            groups,
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
                MinTempLabel.text = ActionGameManager.RuntimePlayerSettings.FormatTemperature(_displayedEntity.MinTemp);
                MaxTempLabel.text = ActionGameManager.RuntimePlayerSettings.FormatTemperature(_displayedEntity.MaxTemp);
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
            Current.targetGraphic.color = entity == GameManager.CurrentEntity ? ToggleEnabledColor : ToggleDisabledColor;
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
                                    GameManager.BeginDrag(new EquippedItemDragObject(item, entity, item.Position - v));
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
                                if (!GameManager.EndDrag())
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
                                if (!(GameManager.DragObject is ItemDragObject itemDragObject)) return;
                                var item = itemDragObject.Item;
                                if (!(item is EquippableItem equippableItem)) return;
                                var placementPosition = v + itemDragObject.OriginCellOffset;
                                //foreach (var cell in _dragCells) cell.gameObject.SetActive(false);
                                FakeItem = item;
                                FakeOccupancy = _displayedHullShape.Inset(GetItemShape(equippableItem), placementPosition, item.Rotation);
                                RefreshCells();
                                GameManager.RegisterDragTarget(drag =>
                                {
                                    //Debug.Log("Entity Drag Callback");
                                    FakeOccupancy = null;
                                    var submitted = RequestDraggedItemToEntity(drag, entity, placementPosition);
                                    if (!submitted)
                                        ShowUnableToSubmitItemMoveRequestDialog();
                                    // TODO: SFX: Equip
                                    return submitted;
                                });
                            });
                        cell.PointerExitTrigger.OnPointerExitAsObservable()
                            .Subscribe(data =>
                            {
                                if (!(GameManager.DragObject is ItemDragObject)) return;
                                FakeItem = null;
                                FakeOccupancy = null;
                                RefreshCells();
                                GameManager.UnregisterDragTarget();
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
                            if (GameManager.RequestHullConductivityToggle(entity, int2(v.x - 1, v.y), 0))
                                RefreshCells(new []{v,int2(v.x - 1, v.y)});
                        }
                        if (_displayedHullShape[int2(v.x + 1, v.y)] && point.x > 1 - ThermalToggleRegionSize)
                        {
                            if (GameManager.RequestHullConductivityToggle(entity, int2(v.x, v.y), 0))
                                RefreshCells(new []{v,int2(v.x + 1, v.y)});
                        }
                        if (_displayedHullShape[int2(v.x, v.y - 1)] && point.y < ThermalToggleRegionSize)
                        {
                            if (GameManager.RequestHullConductivityToggle(entity, int2(v.x, v.y - 1), 1))
                                RefreshCells(new []{v,int2(v.x, v.y - 1)});
                        }
                        if (_displayedHullShape[int2(v.x, v.y + 1)] && point.y > 1 - ThermalToggleRegionSize)
                        {
                            if (GameManager.RequestHullConductivityToggle(entity, int2(v.x, v.y), 1))
                                RefreshCells(new []{v,int2(v.x, v.y + 1)});
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
                            GameManager.BeginDrag(new ItemInstanceDragObject(item, cargo, cargo.Cargo[item] - v));
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
                        GameManager.EndDrag();
                        foreach(var dragObject in _dragCells)
                            Destroy(dragObject.gameObject);
                        _dragCells = null;
                        RefreshCells();
                    });
                cell.PointerEnterTrigger.OnPointerEnterAsObservable()
                    .Subscribe(data =>
                    {
                        //Debug.Log("Inventory Pointer Enter");
                        if (!(GameManager.DragObject is ItemDragObject itemDragObject)) return;
                        var item = itemDragObject.Item;
                        var placementPosition = v + itemDragObject.OriginCellOffset;
                        //foreach (var cell in _dragCells) cell.gameObject.SetActive(false);
                        FakeItem = item;
                        FakeOccupancy = cargo.InteriorShape.Inset(GetItemShape(item), placementPosition, item.Rotation);
                        RefreshCells();
                        GameManager.RegisterDragTarget(drag =>
                        {
                            //Debug.Log("Inventory Drag Callback");
                            FakeOccupancy = null;
                            var submitted = RequestDraggedItemToCargo(drag, cargo, placementPosition);
                            if (!submitted)
                                ShowUnableToSubmitItemMoveRequestDialog();
                            // TODO: SFX: Drop
                            return submitted;
                        });
                    });
                cell.PointerExitTrigger.OnPointerExitAsObservable()
                    .Subscribe(data =>
                    {
                        if (!(GameManager.DragObject is ItemDragObject)) return;
                        FakeItem = null;
                        FakeOccupancy = null;
                        RefreshCells();
                        GameManager.UnregisterDragTarget();
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
            if (TryGetTypedHardpointType(item, out var typedHardpoint))
                c = HardpointData.GetColor(typedHardpoint).ToUnityFloat3();
            
            if(!highlight)
                c *= .7071f;

            return c.ToColor();
        }
    }

    private static bool TryGetTypedHardpointType(ItemInstance item, out HardpointType hardpointType)
    {
        hardpointType = HardpointType.Hull;
        var typedItem = FindTypedInventoryItem(item);
        return typedItem != null &&
               !string.IsNullOrWhiteSpace(typedItem.HardpointType) &&
               Enum.TryParse(typedItem.HardpointType, true, out hardpointType);
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

    private static bool TryResolveTypedHullGeometry(ItemInstance hull, out Shape hullShape, out Shape interiorShape)
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

    private static AetheriaRuntimeCatalogItem FindTypedInventoryItem(ItemInstance item)
    {
        return ActionGameManager.RuntimeCatalog?.FindItem(item?.ItemKey ?? "");
    }

    private bool RequestDraggedItemToEntity(DragObject drag, Entity destination, int2 destinationPosition)
    {
        switch (drag)
        {
            case EquippedItemDragObject equippedItemDragObject:
                return GameManager.RequestEquippedItemEquip(
                    equippedItemDragObject.OriginEntity,
                    equippedItemDragObject.EquippedItem,
                    destination,
                    destinationPosition);
            case ItemInstanceDragObject itemInstanceDragObject when itemInstanceDragObject.Item is EquippableItem equippableItem:
                return GameManager.RequestCargoItemEquip(
                    itemInstanceDragObject.OriginInventory,
                    destination,
                    equippableItem,
                    destinationPosition);
            default:
                return false;
        }
    }

    private bool RequestDraggedItemToCargo(DragObject drag, EquippedCargoBay destination, int2 destinationPosition)
    {
        switch (drag)
        {
            case EquippedItemDragObject equippedItemDragObject:
                return GameManager.RequestEquippedItemStore(
                    equippedItemDragObject.OriginEntity,
                    equippedItemDragObject.EquippedItem,
                    destination,
                    destinationPosition);
            case ItemInstanceDragObject itemInstanceDragObject:
                return GameManager.RequestCargoItemTransfer(
                    itemInstanceDragObject.OriginInventory,
                    destination,
                    itemInstanceDragObject.Item,
                    destinationPosition);
            default:
                return false;
        }
    }

    private void ShowUnableToSubmitItemMoveRequestDialog()
    {
        Dialog.Clear();
        Dialog.Title.text = "Unable to submit item move request!";
        Dialog.Show();
        Dialog.MoveToCursor();
    }

    private void OnDestroy()
    {
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

/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.EveRuntime;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
using GameCult.Mesh;
using TMPro;
using UniRx;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UIE = UnityEngine.UIElements;

public class InventoryPanel : MonoBehaviour, IPointerClickHandler
{
    public ConfirmationDialog Dialog;
    public RectTransform DragParent;
    public bool Flip;
    public GameSettings Settings;
    public TextMeshProUGUI Title;
    public Button Dropdown;
    public Button EditName;
    public Button Current;
    public Button Thermal;
    public Color ToggleEnabledColor;
    public Color ToggleDisabledColor;

    private Entity _displayedEntity;
    private EquippedCargoBay _displayedCargo;
    private string _displayedEntityKey = "";
    private string _displayedCargoEntityKey = "";
    private int _displayedCargoIndex = -1;
    private bool _thermal;
    private bool _hud;
    private AetheriaUnityDragSession _dragSession;
    private AetheriaUnityPresentationEntityIndex _presentationEntityIndex;
    private AetheriaClientState _runtimeState;
    private AetheriaRuntimeSurfaceDocument _inventoryPanelSurface;
    private UIE.UIDocument _inventoryPanelSurfaceDocument;
    private AetheriaRuntimeSurfaceDocument _dropdownSurface;
    private UIE.UIDocument _dropdownSurfaceDocument;
    private readonly AetheriaEveUnitySurfaceChrome _inventoryPanelSurfaceChrome = new AetheriaEveUnitySurfaceChrome
    {
        RootAlignItems = UIE.Align.FlexStart,
        RootJustifyContent = UIE.Justify.FlexStart,
        RootPaddingTop = 0f,
        Width = 520f,
        MinWidth = 0f,
        MaxWidth = 720f,
        PaddingLeft = 16f,
        PaddingRight = 16f,
        PaddingTop = 14f,
        PaddingBottom = 14f
    };

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

    public readonly Dictionary<int2, InventoryCell> CellInstances = new Dictionary<int2, InventoryCell>();
    public Subject<PointerEventData> OnBackgroundClick = new Subject<PointerEventData>();

    public InventoryPanelTarget Target =>
        _displayedEntity != null ? InventoryPanelTarget.Equipment :
        _displayedCargo != null ? InventoryPanelTarget.Cargo : InventoryPanelTarget.None;

    private AetheriaUnityDragSession DragSession => _dragSession ??= new AetheriaUnityDragSession();

    private AetheriaClientState RuntimeState =>
        _runtimeState ??= AetheriaUnityRuntimeClientProvider.RuntimeState("unity-inventory");

    public void SetDragSession(AetheriaUnityDragSession dragSession)
    {
        _dragSession = dragSession;
    }

    public void SetPresentationEntityIndex(AetheriaUnityPresentationEntityIndex presentationEntityIndex)
    {
        _presentationEntityIndex = presentationEntityIndex;
    }

    private void Awake()
    {
        if (Dropdown)
            Dropdown.onClick.AddListener(RenderDropdownSurface);
        if (Thermal)
            Thermal.onClick.AddListener(() =>
            {
                if (_displayedEntity != null)
                    Display(_displayedEntity, _hud, !_thermal);
            });
        if (Current)
            Current.onClick.AddListener(() =>
            {
                if (_displayedEntity is Ship ship && !IsCurrentEntity(_displayedEntity))
                    RequestDockedCurrentShip(ship);
            });
        if (EditName)
            EditName.onClick.AddListener(() => ShowRenameDialog(_displayedEntity));
    }

    public void Clear()
    {
        HideDropdownSurface();
        HideInventoryPanelSurface();
        _displayedEntity = null;
        _displayedCargo = null;
        _displayedEntityKey = "";
        _displayedCargoEntityKey = "";
        _displayedCargoIndex = -1;
        _thermal = false;
        _hud = false;
        CellInstances.Clear();
        if (Title)
            Title.text = "None";
    }

    public void Display(Entity entity, bool hud = false, bool thermal = false)
    {
        _displayedEntity = entity;
        _displayedCargo = null;
        _displayedCargoEntityKey = "";
        _displayedCargoIndex = -1;
        _displayedEntityKey = TryGetRecordKeyForPresentationEntity(entity, out var entityKey)
            ? entityKey
            : "";
        _hud = hud;
        _thermal = thermal;
        if (Title)
            Title.text = entity?.Name ?? "";
        RenderInventoryPanelSurface();
    }

    public void Display(EquippedCargoBay cargo)
    {
        _displayedEntity = null;
        _displayedEntityKey = "";
        _displayedCargo = cargo;
        _displayedCargoEntityKey = "";
        _displayedCargoIndex = -1;
        _thermal = false;
        _hud = false;
        if (TryResolveCargoBay(cargo, out var entityKey, out var cargoIndex))
        {
            _displayedCargoEntityKey = entityKey;
            _displayedCargoIndex = cargoIndex;
        }
        if (Title)
            Title.text = cargo?.Name ?? "Cargo";
        RenderInventoryPanelSurface();
    }

    public void RefreshCells()
    {
        RenderInventoryPanelSurface();
    }

    public void RefreshCells(IEnumerable<int2> cells)
    {
        RenderInventoryPanelSurface();
    }

    public Color GetColor(int2 position, bool selected = false)
    {
        return selected ? ToggleEnabledColor : Color.white;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnBackgroundClick.OnNext(eventData);
    }

    private void RenderInventoryPanelSurface()
    {
        _inventoryPanelSurface = ResolveInventoryPanelSurface();
        if (_inventoryPanelSurface == null)
        {
            HideInventoryPanelSurface();
            return;
        }

        _inventoryPanelSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _inventoryPanelSurfaceDocument,
            "Aetheria Inventory Panel Surface",
            _inventoryPanelSurface,
            HandleInventoryPanelSurfaceCommand,
            _inventoryPanelSurfaceChrome,
            embeddedDocumentResolver: ResolveEmbeddedInventorySurface,
            sortingOrder: 990);
    }

    private void RenderDropdownSurface()
    {
        _dropdownSurface = ResolveDropdownSurface();
        if (_dropdownSurface == null)
        {
            HideDropdownSurface();
            return;
        }

        _dropdownSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _dropdownSurfaceDocument,
            "Aetheria Inventory Dropdown Surface",
            _dropdownSurface,
            HandleDropdownSurfaceCommand,
            _dropdownSurfaceChrome,
            sortingOrder: 1000);
    }

    private AetheriaRuntimeSurfaceDocument ResolveEmbeddedInventorySurface(EveEmbeddedDocumentSlot slot)
    {
        if (slot == null ||
            !string.Equals(slot.SlotId, AetheriaRuntimeInventoryPanelSurfaceBuilder.DropdownSlotId, StringComparison.Ordinal))
        {
            return null;
        }

        return ResolveDropdownSurface();
    }

    private void HandleInventoryPanelSurfaceCommand(EveSurfaceCommandRequest request)
    {
        switch (request?.Command ?? "")
        {
            case AetheriaRuntimeInventoryPanelSurfaceBuilder.OpenNavigation:
                RenderDropdownSurface();
                return;
            case AetheriaRuntimeInventoryPanelSurfaceBuilder.ToggleThermal:
                if (_displayedEntity != null)
                    Display(_displayedEntity, _hud, !_thermal);
                return;
            case AetheriaRuntimeInventoryPanelSurfaceBuilder.SetCurrent:
                if (_displayedEntity is Ship ship && !IsCurrentEntity(_displayedEntity))
                    RequestDockedCurrentShip(ship);
                return;
            case AetheriaRuntimeInventoryPanelSurfaceBuilder.EditName:
                ShowRenameDialog(_displayedEntity);
                return;
            default:
                Debug.LogWarning($"Unknown inventory panel command: {request?.Command}");
                return;
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
            TryResolveDropdownSelection(request, command.Command, out var selection))
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
                if (TryResolveStationRefitEntity(selection.EntityKey, out var entity))
                    Display(entity);
                return;
            case AetheriaRuntimeInventoryDropdownSelectionKind.EntityBay:
                if (TryResolveStationRefitEntity(selection.EntityKey, out entity) &&
                    selection.BayIndex >= 0 &&
                    selection.BayIndex < entity.CargoBays.Count)
                {
                    Display(entity.CargoBays[selection.BayIndex]);
                }
                return;
            case AetheriaRuntimeInventoryDropdownSelectionKind.DockingBay:
                if (TryResolveCurrentDockingBay(out var dockingBay))
                    Display(dockingBay);
                return;
            case AetheriaRuntimeInventoryDropdownSelectionKind.SaveLoadout:
                if (!string.IsNullOrWhiteSpace(_displayedEntityKey))
                    RequestLoadoutTemplateSave(_displayedEntityKey);
                return;
            case AetheriaRuntimeInventoryDropdownSelectionKind.Loadout:
                if (TryResolveStationRefitLoadout(selection.TemplateIndex, out var loadout))
                    RequestRuntimeLoadoutRestore(loadout);
                return;
        }
    }

    private static bool TryResolveDropdownSelection(
        EveSurfaceCommandRequest request,
        string command,
        out AetheriaRuntimeInventoryDropdownSelection selection)
    {
        selection = default;
        if (request?.Payload == null)
            return false;

        var selectionKindText = PayloadValue(request, "selectionKind");
        if (!Enum.TryParse(selectionKindText, out AetheriaRuntimeInventoryDropdownSelectionKind selectionKind) ||
            selectionKind == AetheriaRuntimeInventoryDropdownSelectionKind.Unknown)
        {
            return false;
        }

        selection = new AetheriaRuntimeInventoryDropdownSelection(
            selectionKind,
            command,
            PayloadValue(request, "entityKey"),
            ParsePayloadInt(request, "entityIndex"),
            ParsePayloadInt(request, "bayIndex"),
            ParsePayloadInt(request, "templateIndex"));
        return true;
    }

    private static string PayloadValue(EveSurfaceCommandRequest request, string key)
    {
        return request?.Payload?.GetString(key, "") ?? "";
    }

    private static int ParsePayloadInt(EveSurfaceCommandRequest request, string key)
    {
        return request?.Payload?.GetInt32(key, -1) ?? -1;
    }

    private void HideDropdownSurface()
    {
        if (_dropdownSurfaceDocument != null)
            AetheriaEveUnitySurfaceHost.Hide(_dropdownSurfaceDocument);
    }

    private void HideInventoryPanelSurface()
    {
        if (_inventoryPanelSurfaceDocument != null)
            AetheriaEveUnitySurfaceHost.Hide(_inventoryPanelSurfaceDocument);
    }

    private void ShowRenameDialog(Entity entity)
    {
        if (entity == null || Dialog == null)
            return;

        Dialog.Clear();
        var entityName = entity.Name;
        Dialog.AddField("Name", () => entityName, s => entityName = s);
        Dialog.Show(() => RequestEntityName(entity, entityName));
        Dialog.MoveToCursor();
    }

    private AetheriaRuntimeSurfaceDocument ResolveInventoryPanelSurface()
    {
        try
        {
            return RuntimeState.InventoryPanelSurface(BuildInventoryPanelSurfaceRequest()).Latest();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read Aetheria inventory panel surface from local Verse state: {ex.Message}");
            return null;
        }
    }

    private AetheriaRuntimeSurfaceDocument ResolveDropdownSurface()
    {
        try
        {
            return RuntimeState.InventoryDropdownSurface(BuildDropdownSurfaceRequest()).Latest();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read Aetheria inventory dropdown surface from local Verse state: {ex.Message}");
            return null;
        }
    }

    private AetheriaRuntimeInventoryPanelSurfaceRequest BuildInventoryPanelSurfaceRequest()
    {
        var request = new AetheriaRuntimeInventoryPanelSurfaceRequest
        {
            ViewTitle = Title?.text ?? "",
            DisplayedEntityKey = _displayedEntityKey,
            DisplayedCargoEntityKey = _displayedCargoEntityKey,
            DisplayedCargoIndex = _displayedCargoIndex,
            ThermalView = _thermal,
            HudView = _hud
        };

        if (_displayedEntity != null)
            request.DisplayedEntityIndex = _displayedEntity.DaemonEntityIndex;
        if (_displayedCargo != null && TryResolveDisplayedCargoEntityIndex(out var displayedCargoEntityIndex))
            request.DisplayedCargoEntityIndex = displayedCargoEntityIndex;

        if (DragSession.TryGetDraggedItem(out var itemDragObject))
        {
            request.HasDragSession = true;
            request.DragItemKey = itemDragObject.Item?.ItemKey ?? "";
            request.DragOriginOffsetX = itemDragObject.OriginCellOffset.x;
            request.DragOriginOffsetY = itemDragObject.OriginCellOffset.y;
            request.DragRotation = itemDragObject.Item?.Rotation.ToString() ?? "";
        }

        return request;
    }

    private AetheriaRuntimeInventoryDropdownSurfaceRequest BuildDropdownSurfaceRequest()
    {
        return new AetheriaRuntimeInventoryDropdownSurfaceRequest
        {
            CurrentView = Title?.text ?? "",
            DisplayedEntityKey = _displayedEntityKey,
            DisplayedCargoEntityKey = _displayedCargoEntityKey,
            DisplayedCargoIndex = _displayedCargoIndex,
            CanSaveLoadout = !string.IsNullOrWhiteSpace(_displayedEntityKey)
        };
    }

    private bool TryResolveDisplayedCargoEntityIndex(out int entityIndex)
    {
        entityIndex = -1;
        if (string.IsNullOrWhiteSpace(_displayedCargoEntityKey))
            return false;

        var refit = StationRefitSnapshot();
        if (refit == null)
            return false;

        if (string.Equals(refit.DockParentEntityKey, _displayedCargoEntityKey, StringComparison.Ordinal))
            entityIndex = refit.DockParentEntityIndex;
        else
            entityIndex = (refit.AvailableEntities ?? Array.Empty<AetheriaRuntimeStationRefitEntityOption>())
                .FirstOrDefault(option => string.Equals(option?.EntityKey ?? "", _displayedCargoEntityKey, StringComparison.Ordinal))
                ?.EntityIndex ?? -1;

        return entityIndex >= 0;
    }

    private bool TryGetRecordKeyForPresentationEntity(Entity entity, out string recordKey)
    {
        recordKey = "";
        return entity != null &&
               _presentationEntityIndex != null &&
               _presentationEntityIndex.TryGetRecordKeyForPresentationEntity(entity, out recordKey);
    }

    private bool IsCurrentEntity(Entity entity)
    {
        return entity != null &&
               TryGetRecordKeyForPresentationEntity(entity, out var entityKey) &&
               string.Equals(entityKey, CurrentEntitySnapshot()?.EntityKey ?? "", StringComparison.Ordinal);
    }

    private bool TryResolveStationRefitEntity(string entityKey, out Entity entity)
    {
        entity = null;
        return _presentationEntityIndex != null &&
               !string.IsNullOrWhiteSpace(entityKey) &&
               _presentationEntityIndex.TryGetPresentationEntityByRecordKey(entityKey, out entity);
    }

    private bool TryResolveCurrentDockingBay(out EquippedDockingBay dockingBay)
    {
        dockingBay = null;
        var refit = StationRefitSnapshot();
        return refit?.IsDocked == true &&
               refit.DockingBayIndex >= 0 &&
               !string.IsNullOrWhiteSpace(refit.DockParentEntityKey) &&
               _presentationEntityIndex != null &&
               _presentationEntityIndex.TryGetPresentationDockingBayByRecordKey(
                   refit.DockParentEntityKey,
                   refit.DockingBayIndex,
                   out dockingBay) &&
               dockingBay != null;
    }

    private bool TryResolveStationRefitLoadout(
        int templateIndex,
        out AetheriaRuntimeStationLoadoutRestoreOption loadout)
    {
        loadout = null;
        if (templateIndex < 0)
            return false;

        loadout = (StationRefitSnapshot()?.LoadoutRestoreOptions ?? Array.Empty<AetheriaRuntimeStationLoadoutRestoreOption>())
            .FirstOrDefault(option => option?.TemplateIndex == templateIndex);
        return loadout != null;
    }

    private bool TryResolveCargoBay(EquippedCargoBay cargoBay, out string entityKey, out int cargoIndex)
    {
        entityKey = "";
        cargoIndex = -1;
        var entity = cargoBay?.Entity;
        if (entity?.CargoBays == null ||
            !TryGetRecordKeyForPresentationEntity(entity, out entityKey))
        {
            return false;
        }

        cargoIndex = entity.CargoBays.IndexOf(cargoBay);
        return cargoIndex >= 0;
    }

    private void RequestDockedCurrentShip(Ship ship)
    {
        if (ship == null || !TryGetRecordKeyForPresentationEntity(ship, out var targetEntityKey))
            return;

        TrySubmitOperation(
            operations => operations.SetDockedCurrentShip(targetEntityKey),
            "docked current ship");
    }

    private void RequestEntityName(Entity entity, string name)
    {
        if (entity == null || !TryGetRecordKeyForPresentationEntity(entity, out var targetEntityKey))
            return;

        TrySubmitOperation(
            operations => operations.SetEntityName(targetEntityKey, name ?? ""),
            "entity rename");
    }

    private void RequestRuntimeLoadoutRestore(AetheriaRuntimeStationLoadoutRestoreOption loadout)
    {
        if (loadout == null ||
            string.IsNullOrWhiteSpace(loadout.TargetEntityKey) ||
            string.IsNullOrWhiteSpace(loadout.TemplateName))
        {
            return;
        }

        TrySubmitOperation(
            operations => operations.RestoreLoadout(loadout.TargetEntityKey, loadout.TemplateName, loadout.Price),
            "loadout restore");
    }

    private void RequestLoadoutTemplateSave(string targetEntityKey)
    {
        if (string.IsNullOrWhiteSpace(targetEntityKey))
            return;

        try
        {
            var loadout = RuntimeState.DaemonFrame.Latest().Run.CreateLoadoutTemplate(targetEntityKey ?? "");
            AetheriaUnityRuntimeClientProvider
                .Ui("unity-inventory")
                .SaveLoadoutTemplateAsync(loadout, "unity-inventory")
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to save Aetheria loadout template: {ex.Message}");
        }
    }

    private bool TrySubmitOperation(Action<AetheriaControl> submit, string label)
    {
        if (submit == null)
            return false;

        try
        {
            submit(AetheriaUnityRuntimeClientProvider.Control("unity-inventory"));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon inventory {label} operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private AetheriaRuntimeCurrentEntityDocument CurrentEntitySnapshot()
    {
        try
        {
            return RuntimeState.CurrentEntity.Latest();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read Aetheria current entity for inventory panel: {ex.Message}");
            return null;
        }
    }

    private AetheriaRuntimeStationRefitDocument StationRefitSnapshot()
    {
        try
        {
            return RuntimeState.StationRefit.Latest();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read Aetheria station refit for inventory panel: {ex.Message}");
            return null;
        }
    }

    private void OnDestroy()
    {
        if (_inventoryPanelSurfaceDocument != null)
        {
            AetheriaEveUnitySurfaceHost.DestroyDocument(_inventoryPanelSurfaceDocument);
            _inventoryPanelSurfaceDocument = null;
        }

        if (_dropdownSurfaceDocument != null)
        {
            AetheriaEveUnitySurfaceHost.DestroyDocument(_dropdownSurfaceDocument);
            _dropdownSurfaceDocument = null;
        }
    }
}

public enum InventoryPanelTarget
{
    None,
    Equipment,
    Cargo
}

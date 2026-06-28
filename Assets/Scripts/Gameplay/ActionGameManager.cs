/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using Cinemachine;
using GameCult.Aetheria.State.Verse;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Serialization;
using UnityEngine.UI;
using float2 = Unity.Mathematics.float2;
using float3 = Unity.Mathematics.float3;

public class ActionGameManager : MonoBehaviour
{
    private AetheriaDaemonObserver _daemonObserver;
    private AetheriaUnityPilotCommandSender _pilotCommands;
    private AetheriaUnityObservedDockingIndex _observedDocking;
    private AetheriaUnityObservedEntityProjector _observedEntityProjector;
    private AetheriaUnityCurrentEntityBinder _currentEntityBinder;
    private AetheriaUnityObservedZoneContextProjector _observedZoneContextProjector;
    private AetheriaUnityPilotFrameAdapter _pilotFrameAdapter;
    private AetheriaUnityPilotOperationAdapter _pilotOperationAdapter;
    private AetheriaUnityObservedTargetQuery _observedTargetQuery;
    private AetheriaUnityObservedFrameApplier _observedFrameApplier;
    private AetheriaUnityEntityConstructionBlueprintProjector _entityConstructionBlueprintProjector;
    private AetheriaUnityActionBarBindingAdapter _actionBarBindingAdapter;
    private AetheriaUnityMenuShell _menuShell;
    private AetheriaUnityGameplayInputShell _gameplayInputShell;
    private AetheriaUnityCockpitHudShell _cockpitHudShell;
    private AetheriaUnityGameplayLoopShell _gameplayLoopShell;
    private AetheriaUnityGameplayBootShell _gameplayBootShell;
    private AetheriaUnityGameplaySceneWiring _sceneWiring;
    private AetheriaUnityPilotCommandSender PilotCommands =>
        _pilotCommands ??= new AetheriaUnityPilotCommandSender(ResolveCurrentRuntimeClient, () => Time.unscaledTime);
    private AetheriaUnityObservedDockingIndex ObservedDocking =>
        _observedDocking ??= new AetheriaUnityObservedDockingIndex(
            () => ResolveDaemonObserver()?.Client,
            _observedEntityIndex);
    private AetheriaUnityObservedEntityProjector ObservedEntityProjector =>
        _observedEntityProjector ??= new AetheriaUnityObservedEntityProjector(
            _observedEntityIndex,
            ItemManager,
            EntityConstructionBlueprintProjector.ProjectObservedEntity,
            _loadoutItemFactory.CreateLoadoutItem,
            Debug.LogWarning);
    private AetheriaUnityObservedZoneContextProjector ObservedZoneContextProjector =>
        _observedZoneContextProjector ??= new AetheriaUnityObservedZoneContextProjector(
            ItemManager,
            Settings.PlanetSettings,
            () => ObservedGalaxy,
            Debug.LogWarning,
            PlayMusic);
    private AetheriaUnityCurrentEntityBinder CurrentEntityBinder =>
        _currentEntityBinder ??= new AetheriaUnityCurrentEntityBinder
        {
            ZoneRenderer = ZoneRenderer,
            DeathPost = DeathPost,
            GameplayUI = GameplayUI,
            ObservedDocking = ObservedDocking,
            CurrentEntityPresentation = _currentEntityPresentation,
            TargetPresentation = _targetPresentation,
            GetCurrentEntity = () => CurrentEntity,
            SetCurrentEntity = entity => CurrentEntity = entity,
            GetViewDirection = () => _viewDirection,
            SetViewDirection = direction => _viewDirection = direction,
            ResolveZoneRender = () => ResolveDaemonObserver()?.LastObservedState?.ZoneRender,
            ApplyActionBarBindings = ActionBarBindings.ApplyBindings,
            EnablePlayerInput = EnablePlayerInput,
            DisablePlayerInput = DisablePlayerInput,
            PlayMusic = PlayMusic,
            UpdateTargetPanel = target => CockpitHudShell.UpdateTarget(target, CurrentEntity)
        };
    private AetheriaUnityPilotFrameAdapter PilotFrameAdapter =>
        _pilotFrameAdapter ??= new AetheriaUnityPilotFrameAdapter(direction => _viewDirection = direction)
        {
            ZoneRenderer = ZoneRenderer,
            Input = Input,
            TargetPresentation = _targetPresentation,
            PilotCommands = PilotCommands,
            HeatstrokePost = HeatstrokePost,
            SevereHeatstrokePost = SevereHeatstrokePost,
            Sensitivity = Sensitivity
        };
    private AetheriaUnityPilotOperationAdapter PilotOperationAdapter =>
        _pilotOperationAdapter ??= new AetheriaUnityPilotOperationAdapter(
            () => PilotCommands,
            _observedEntityIndex,
            () => _viewDirection,
            () => CurrentEntity);
    private AetheriaUnityObservedTargetQuery ObservedTargetQuery =>
        _observedTargetQuery ??= new AetheriaUnityObservedTargetQuery(
            () => ResolveDaemonObserver()?.Client,
            _observedEntityIndex);
    private AetheriaUnityObservedFrameApplier ObservedFrameApplier =>
        _observedFrameApplier ??= new AetheriaUnityObservedFrameApplier(
            ResolveDaemonObserver,
            ResolveObservedGalaxyZone,
            () => Zone,
            zone => Zone = zone,
            _observedEntityIndex,
            ObservedEntityProjector,
            ObservedZoneContextProjector,
            () => ZoneRenderer,
            () => CurrentEntity,
            entity => CurrentEntityBinder.RestoreBinding(entity),
            Debug.LogWarning);
    private AetheriaUnityEntityConstructionBlueprintProjector EntityConstructionBlueprintProjector =>
        _entityConstructionBlueprintProjector ??= new AetheriaUnityEntityConstructionBlueprintProjector(_loadoutItemFactory);
    private AetheriaUnityActionBarBindingAdapter ActionBarBindings =>
        _actionBarBindingAdapter ??= new AetheriaUnityActionBarBindingAdapter(_actionBarPresentation);
    private AetheriaUnityMenuShell MenuShell =>
        _menuShell ??= new AetheriaUnityMenuShell
        {
            MainMenu = MainMenu,
            HelpScreen = HelpScreen,
            InputDisplayLayout = InputDisplayLayout,
            UiRoot = UiRoot,
            Menu = Menu,
            GameplayUI = GameplayUI,
            ResolveCurrentEntity = () => CurrentEntity,
            IsCurrentEntityUndocked = IsCurrentEntityObservedUndocked,
            ResolveObservedTarget = entity => ObservedTargetQuery.GetObservedTarget(entity),
            SetPaused = paused => GameplayLoopShell.Paused = paused,
            EnablePlayerInput = EnablePlayerInput,
            DisablePlayerInput = DisablePlayerInput,
            UpdatePlayerPanel = () => CockpitHudShell.UpdatePlayer(CurrentEntity),
            UpdateTargetPanel = target => CockpitHudShell.UpdateTarget(target, CurrentEntity)
        };
    private AetheriaUnityGameplayInputShell GameplayInputShell =>
        _gameplayInputShell ??= new AetheriaUnityGameplayInputShell
        {
            RuntimePlayerSettings = RuntimePlayerSettings,
            InputDisplayLayout = InputDisplayLayout,
            Dialog = Dialog,
            MainMenu = MainMenu,
            Menu = Menu,
            MenuMap = MenuMap,
            GameplayUI = GameplayUI,
            ActionBar = ActionBar,
            ActionBarSlot = ActionBarSlot,
            ZoneRenderer = ZoneRenderer,
            MenuShell = MenuShell,
            DragSession = _dragSession,
            ActionBarPresentation = _actionBarPresentation,
            PilotOperationAdapter = PilotOperationAdapter,
            ResolveCurrentEntity = () => CurrentEntity,
            SetInput = input => Input = input
        };
    private AetheriaUnityCockpitHudShell CockpitHudShell =>
        _cockpitHudShell ??= new AetheriaUnityCockpitHudShell
        {
            ShipPanel = ShipPanel,
            TargetShipPanel = TargetShipPanel,
            SchematicDisplay = SchematicDisplay,
            TargetSchematicDisplay = TargetSchematicDisplay,
            TargetIndicator = TargetIndicator
        };
    private AetheriaUnityGameplayLoopShell GameplayLoopShell =>
        _gameplayLoopShell ??= new AetheriaUnityGameplayLoopShell
        {
            ResolveCurrentEntity = () => CurrentEntity,
            IsCurrentEntityUndocked = IsCurrentEntityObservedUndocked,
            ApplyLatestZoneRender = () => ObservedFrameApplier.ApplyLatestZoneRender(),
            CurrentEntityPresentation = _currentEntityPresentation,
            PilotFrameAdapter = PilotFrameAdapter,
            TargetPresentation = _targetPresentation
        };
    private AetheriaUnityGameplayBootShell GameplayBootShell =>
        _gameplayBootShell ??= new AetheriaUnityGameplayBootShell
        {
            Settings = Settings,
            ZoneRenderer = ZoneRenderer,
            CockpitHudShell = CockpitHudShell,
            TargetSpottedBlinkFrequency = TargetSpottedBlinkFrequency,
            TargetSpottedBlinkOffset = TargetSpottedBlinkOffset,
            Log = Debug.Log
        };
    private AetheriaUnityGameplaySceneWiring SceneWiring =>
        _sceneWiring ??= new AetheriaUnityGameplaySceneWiring
        {
            ZoneRenderer = ZoneRenderer,
            DockCamera = DockCamera,
            FollowCamera = FollowCamera,
            Menu = Menu,
            TradeMenu = TradeMenu,
            GameplayUI = GameplayUI,
            Inventory = Inventory,
            ShipPanel = ShipPanel,
            TargetShipPanel = TargetShipPanel,
            SchematicDisplay = SchematicDisplay,
            TargetSchematicDisplay = TargetSchematicDisplay,
            Crosshairs = Crosshairs,
            LockIndicator = LockIndicator,
            HitMarker = HitMarker,
            HitMarkerDuration = HitMarkerDuration,
            TargetShieldsBackground = TargetShieldsBackground,
            TargetShieldsIcon = TargetShieldsIcon,
            ShieldColor = ShieldColor,
            NoShieldColor = NoShieldColor,
            ShieldIcon = ShieldIcon,
            NoShieldIcon = NoShieldIcon,
            HostileTargetIndicator = HostileTargetIndicator,
            FriendlyTargetIndicator = FriendlyTargetIndicator,
            ViewDot = ViewDot,
            TargetIndicator = TargetIndicator,
            TargetHitpointsFill = TargetHitpointsFill,
            TargetVisibilityFill = TargetVisibilityFill,
            VisibilityToTargetFill = VisibilityToTargetFill,
            TargetShieldsFill = TargetShieldsFill,
            MainMenu = MainMenu
        };
    private readonly AetheriaUnityObservedEntityIndex _observedEntityIndex = new AetheriaUnityObservedEntityIndex();
    private static RuntimePlayerSettings RuntimePlayerSettings
        => AetheriaUnityRuntimeClientProvider.PlayerSettings;

    public GameSettings Settings;
    //public string StarterShipTemplate = "Longinus";
    public float2 Sensitivity;
    public float TargetSpottedBlinkFrequency = 20;
    public float TargetSpottedBlinkOffset = -.25f;

    [Header("Postprocessing")]
    public PostProcessVolume DeathPost;
    public PostProcessVolume HeatstrokePost;
    public PostProcessVolume SevereHeatstrokePost;

    [Header("Scene Links")]
    public GameObject UiRoot;
    public GameObject HelpScreen;
    public InputDisplayLayout InputDisplayLayout;
    public Transform ActionBar;
    public ActionBarSlot ActionBarSlot;
    public ZoneRenderer ZoneRenderer;
    public CinemachineVirtualCamera DockCamera;
    public CinemachineVirtualCamera FollowCamera;

    [Header("Menu UI")]
    public MainMenu MainMenu;
    public MenuPanel Menu;
    public MapRenderer MenuMap;
    public TradeMenu TradeMenu;
    public InventoryMenu Inventory;
    public InventoryPanel ShipPanel;
    public InventoryPanel TargetShipPanel;
    public ConfirmationDialog Dialog;

    [Header("Gameplay UI")]
    public CanvasGroup GameplayUI;
    public Prototype HostileTargetIndicator;
    public Prototype FriendlyTargetIndicator;
    public PlaceUIElementWorldspace ViewDot;
    public Prototype LockIndicator;
    public PlaceUIElementWorldspace[] Crosshairs;
    public GameObject HitMarker;
    public float HitMarkerDuration;
    public SchematicDisplay SchematicDisplay;
    public SchematicDisplay TargetSchematicDisplay;

    [Header("Target Indicator")]
    public PlaceUIElementWorldspace TargetIndicator;
    public Image TargetHitpointsFill;
    public Image TargetVisibilityFill;
    public Image VisibilityToTargetFill;
    public Image TargetShieldsBackground;
    public Image TargetShieldsFill;
    public Image TargetShieldsIcon;
    public Color ShieldColor;
    public Color NoShieldColor;
    public Sprite ShieldIcon;
    public Sprite NoShieldIcon;

    //public PlayerInput Input;

    // private CinemachineFramingTransposer _transposer;
    // private CinemachineComposer _composer;

    private Entity _currentEntity;

    // private ShipInput _shipInput;
    private float3 _viewDirection;
    private readonly AetheriaUnityCurrentEntityPresentation _currentEntityPresentation = new AetheriaUnityCurrentEntityPresentation();
    private readonly AetheriaUnityTargetPresentation _targetPresentation = new AetheriaUnityTargetPresentation();
    public AetheriaInput Input { get; private set; }

    private Entity CurrentEntity
    {
        get => _currentEntity;
        set => _currentEntity = value;
    }

    private ItemManager ItemManager { get; set; }
    private AetheriaUnityLoadoutItemFactory _loadoutItemFactory;
    private Galaxy ObservedGalaxy { get; set; }
    private Zone Zone { get; set; }

    private readonly AetheriaUnityDragSession _dragSession = new AetheriaUnityDragSession();
    private readonly AetheriaUnityActionBarPresentation _actionBarPresentation = new AetheriaUnityActionBarPresentation();

    private AetheriaClient ResolveActionBarClient()
    {
        return ResolveCurrentRuntimeClient();
    }

    private static AetheriaClient ResolveCurrentRuntimeClient()
    {
        return AetheriaUnityRuntimeClientProvider.CurrentClientForStateFile(
            AetheriaUnityRuntimePaths.RuntimeStateFilePath);
    }

    private void OnDisable()
    {
        _gameplayInputShell?.Dispose();
        _observedTargetQuery?.Dispose();
        _observedDocking?.Dispose();
        _observedTargetQuery = null;
        _observedDocking = null;
        AetheriaUnityRuntimeClientProvider.Dispose();
    }

    void Start()
    {
        var boot = GameplayBootShell.Boot();
        ItemManager = boot.ItemManager;
        _loadoutItemFactory = boot.LoadoutItemFactory;
        ObservedGalaxy = boot.ObservedGalaxy;
        SceneWiring.ConfigureCurrentEntityPresentation(_currentEntityPresentation, boot.RuntimeCatalog);
        SceneWiring.ConfigureTargetPresentation(
            _targetPresentation,
            boot.RuntimeCatalog,
            _observedEntityIndex,
            ObservedTargetQuery,
            () => ResolveDaemonObserver()?.Client);
        SceneWiring.ConfigureInventoryDragSession(_dragSession);
        SceneWiring.ConfigureActionBarPresentation(
            _actionBarPresentation,
            GameplayInputShell,
            boot.RuntimeCatalog,
            Settings,
            () => CurrentEntity,
            ResolveActionBarClient);
        SceneWiring.ConfigureRuntimeInputScreenShell(MenuShell);
        SceneWiring.ConfigureObservedEntityIndex(_observedEntityIndex);

        // TODO: Process Stories

        GameplayInputShell.Bootstrap();

        StartGame();
    }

    private void EnablePlayerInput()
    {
        GameplayInputShell.EnablePlayerInput();
    }

    private void DisablePlayerInput()
    {
        GameplayInputShell.DisablePlayerInput();
    }

    private void StartGame()
    {
        ObservedFrameApplier.ApplyLatestZoneRender();
    }

    private bool IsCurrentEntityObservedUndocked()
    {
        return ObservedDocking.IsEntityUndocked(CurrentEntity);
    }

    private GalaxyZone ResolveObservedGalaxyZone(int daemonZoneIndex)
    {
        if (daemonZoneIndex < 0 || ObservedGalaxy?.Zones == null)
            return null;

        foreach (var zone in ObservedGalaxy.Zones)
        {
            if (zone != null && zone.ZoneIndex == daemonZoneIndex)
                return zone;
        }

        return null;
    }

    // public void ToggleEditMode()
    // {
    //     _editMode = !_editMode;
    //     FollowCamera.gameObject.SetActive(!_editMode);
    //     TopDownCamera.gameObject.SetActive(_editMode);
    // }

    public void PlayMusic(MusicType type)
    {
        // TODO: SFX: Music
    }

    void Update()
    {
        GameplayLoopShell.Tick(Time.deltaTime, Time.time);
    }

    private AetheriaDaemonObserver ResolveDaemonObserver()
    {
        if (_daemonObserver != null)
        {
            return _daemonObserver;
        }

        _daemonObserver = FindAnyObjectByType<AetheriaDaemonObserver>();
        return _daemonObserver;
    }

    private void LateUpdate()
    {
        GameplayLoopShell.LateTick();
    }
}

/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cinemachine;
using GameCult.Aetheria.State.Verse;
using Ink;
using Ink.Runtime;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using static Unity.Mathematics.math;
using bool2 = CultMath.bool2;
using float2 = Unity.Mathematics.float2;
using float3 = Unity.Mathematics.float3;
using float4 = Unity.Mathematics.float4;
using int2 = Unity.Mathematics.int2;
using Regex = System.Text.RegularExpressions.Regex;
using Random = UnityEngine.Random;

public class ActionGameManager : MonoBehaviour
{
    private static readonly HashSet<string> ArticulatedWeaponBehaviorKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "GuidedWeapon",
        "InstantWeapon",
        "ConstantWeapon",
        "ChargedWeapon",
        "AutoWeapon"
    };

    private static float Saturate(float value) => Mathf.Clamp01(value);

    private static float Unlerp(float from, float to, float value) => (value - from) / (to - from);

    private const float DaemonMoveCommandIntervalSeconds = 0.05f;
    private const float DaemonMoveCommandChangeThreshold = 0.001f;
    private const float DaemonLookCommandIntervalSeconds = 0.02f;
    private const float DaemonLookCommandChangeThreshold = 0.0001f;
    private const float DaemonTractorCommandIntervalSeconds = 0.05f;
    private const float DaemonTractorCommandChangeThreshold = 0.001f;

    // Always check for null if accessing from anywhere that might not be in-game (e.g. menu UI)
    public static ActionGameManager Instance { get; private set; }
    private AetheriaDaemonObserver _daemonObserver;
    private Vector2 _lastSentDaemonMoveVector;
    private Vector3 _lastSentDaemonLookDirection;
    private float _lastSentDaemonTractorPower;
    private float _nextDaemonMoveCommandTime;
    private float _nextDaemonLookCommandTime;
    private float _nextDaemonTractorCommandTime;
    private bool _hasSentDaemonMoveVector;
    private bool _hasSentDaemonLookDirection;
    private bool _hasSentDaemonTractorPower;
    private long _lastAppliedAuthoritativeDaemonFrameId = -1;
    private string _lastAppliedAuthoritativeDaemonFramePath = "";
    private string _lastAppliedAuthoritativeDaemonRunId = "";
    private int _lastAppliedAuthoritativeDaemonZoneIndex = -1;
    private readonly Dictionary<string, Entity> _authoritativeDaemonEntities = new Dictionary<string, Entity>(StringComparer.Ordinal);
    private static DirectoryInfo _gameDataDirectory;
    public static DirectoryInfo GameDataDirectory
    {
        get => _gameDataDirectory ??= new DirectoryInfo(Application.dataPath).Parent.CreateSubdirectory("GameData");
    }

    private static string _runtimeStateFilePath;
    public static string RuntimeStateFilePath =>
        _runtimeStateFilePath ??= AetheriaRuntimeStateBoot.Inspect(GameDataDirectory).StateFilePath;

    private static RuntimePlayerSettings _runtimePlayerSettings;
    public static RuntimePlayerSettings RuntimePlayerSettings
    {
        get
        {
            return _runtimePlayerSettings ??= LoadRuntimePlayerSettings();
        }
    }

    public static void RequestRuntimeInputBindingOverride(string actionName, int bindingIndex, string inputSystemPath)
    {
        SendRuntimeInputSettingsCommand(
            AetheriaRuntimeEveCommandKind.SetBindingOverride,
            new AetheriaRuntimeInputSettingsCommandBody
            {
                ActionName = actionName ?? "",
                BindingIndex = bindingIndex,
                InputSystemPath = inputSystemPath ?? ""
            },
            "unity-input-screen",
            "input binding override");
    }

    public static void RequestRuntimeActionBarInput(string inputSystemPath, bool enabled)
    {
        SendRuntimeInputSettingsCommand(
            AetheriaRuntimeEveCommandKind.SetActionBarEnabled,
            new AetheriaRuntimeInputSettingsCommandBody
            {
                InputSystemPath = inputSystemPath ?? "",
                Enabled = enabled
            },
            "unity-input-screen",
            "action-bar input");
    }

    private static void SendRuntimeInputSettingsCommand(
        AetheriaRuntimeEveCommandKind command,
        AetheriaRuntimeInputSettingsCommandBody body,
        string clientId,
        string label)
    {
        try
        {
            if (!AetheriaRuntimeEveCommands.TrySendInputSettingsCommand(
                    RuntimeStateFilePath,
                    command,
                    body,
                    clientId,
                    out var submitted,
                    out var error))
            {
                Debug.LogError($"Failed to submit Aetheria {label} Eve command: {error}");
                return;
            }

            Debug.Log($"Submitted Aetheria {label} Eve command: {submitted!.CommandId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send Aetheria {label} Eve command: {ex}");
        }
    }

    private static RuntimePlayerSettings CreateDefaultRuntimePlayerSettings()
    {
        var settings = new RuntimePlayerSettings();
        settings.Name = Environment.UserName;
        settings.InputSettings.SetActionBarInputEnabled("<Keyboard>/leftShift", true);
        settings.InputSettings.SetActionBarInputEnabled("<Mouse>/leftButton", true);
        settings.InputSettings.SetActionBarInputEnabled("<Mouse>/rightButton", true);
        settings.InputSettings.SetActionBarInputEnabled("<Mouse>/middleButton", true);
        for (int i = 1; i < 6; i++) settings.InputSettings.SetActionBarInputEnabled($"<Keyboard>/{i}", true);
        return settings;
    }

    private static RuntimePlayerSettings LoadRuntimePlayerSettings()
    {
        var settings = CreateDefaultRuntimePlayerSettings();
        try
        {
            var stored = AetheriaRuntimeStateReader.ReadPlayerSettings(RuntimeStateFilePath);
            if (stored == null)
                return settings;

            ApplyRuntimePlayerSettings(settings, stored);
            Debug.Log("Loaded Aetheria Verse player settings from typed state.");
        }
        catch (FileNotFoundException)
        {
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load Aetheria Verse player settings from typed state; using defaults: {ex}");
        }

        return settings;
    }

    private static void ApplyRuntimePlayerSettings(
        RuntimePlayerSettings settings,
        AetheriaRuntimePlayerSettingsSnapshot stored)
    {
        if (!string.IsNullOrWhiteSpace(stored.PlayerName))
            settings.Name = stored.PlayerName;

        settings.TutorialPassed = stored.TutorialPassed;

        settings.HashedStoryFiles.Clear();
        foreach (var storyFileHash in stored.StoryFileHashes)
        {
            if (!string.IsNullOrWhiteSpace(storyFileHash.StoryPath))
                settings.HashedStoryFiles[storyFileHash.StoryPath] = storyFileHash.Hash;
        }

        if (Enum.TryParse(stored.TemperatureUnit, out TemperatureUnit temperatureUnit))
            settings.GameplaySettings.TemperatureUnit = temperatureUnit;
        settings.GameplaySettings.SignificantDigits = Math.Max(0, stored.SignificantDigits);

        if (Enum.TryParse(stored.NebulaQuality, out Quality nebulaQuality))
            settings.GraphicsSettings.NebulaQuality = nebulaQuality;
        settings.GraphicsSettings.ShowAsteroidsInMinimap = stored.ShowAsteroidsInMinimap;

        settings.InputSettings.InputActionMap.Clear();
        foreach (var binding in stored.BindingOverrides)
        {
            if (string.IsNullOrWhiteSpace(binding.ActionName) || string.IsNullOrWhiteSpace(binding.BindingPath) || binding.BindingIndex < 0)
                continue;

            settings.InputSettings.SetBindingOverride(binding.ActionName, binding.BindingIndex, binding.BindingPath);
        }

        settings.InputSettings.ActionBarInputs.Clear();
        foreach (var input in stored.ActionBarInputs)
        {
            if (!string.IsNullOrWhiteSpace(input))
                settings.InputSettings.SetActionBarInputEnabled(input, true);
        }
    }

    private void LoadRuntimeLoadoutTemplates(string stateFilePath)
    {
        try
        {
            var loadouts = AetheriaRuntimeStateReader.ReadLoadoutTemplates(stateFilePath);
            LoadoutTemplates.Clear();
            LoadoutTemplates.AddRange(loadouts);

            if (LoadoutTemplates.Count > 0)
                Debug.Log($"Loaded {LoadoutTemplates.Count} Aetheria Verse loadout templates from typed state.");
        }
        catch (FileNotFoundException)
        {
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load Aetheria Verse loadout templates from typed state: {ex}");
        }
    }

    public EntityConstructionBlueprint CreateEntityConstructionBlueprint(AetheriaRuntimeLoadoutTemplateSnapshot template)
    {
        var blueprint = CreateEntityConstructionBlueprint(template.RootEntity);
        if (blueprint != null && string.IsNullOrWhiteSpace(blueprint.Name))
            blueprint.Name = template.Name;
        return blueprint;
    }

    private EntityConstructionBlueprint CreateEntityConstructionBlueprint(AetheriaRuntimeEntityLoadoutSnapshot entity)
    {
        var hull = CreateEquippableLoadoutItem(entity.Hull);
        if (hull == null)
            return null;

        EntityConstructionBlueprint blueprint = string.Equals(entity.Kind, "orbital", StringComparison.OrdinalIgnoreCase)
            ? new OrbitalEntityConstructionBlueprint()
            : new ShipConstructionBlueprint
            {
                Direction = new float2(0, 1)
            };

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
            .Select(CreateEntityConstructionBlueprint)
            .Where(child => child != null)
            .ToArray();
        return blueprint;
    }

    private EntityConstructionBlueprint CreateEntityConstructionBlueprint(AetheriaRuntimeEntitySnapshot entity, bool isCurrentEntity)
    {
        if (entity == null)
            return null;

        var hull = CreateEquippableLoadoutItem(new AetheriaRuntimeLoadoutItemSnapshot(
            entity.HullItemKey,
            1,
            1,
            1,
            true,
            false));
        if (hull == null)
            return null;

        EntityConstructionBlueprint blueprint = string.Equals(entity.Kind, "orbital", StringComparison.OrdinalIgnoreCase)
            ? new OrbitalEntityConstructionBlueprint()
            : new ShipConstructionBlueprint
            {
                Position = new float3((float)entity.PositionX, (float)entity.PositionY, (float)entity.PositionZ),
                Direction = new float2((float)entity.DirectionX, (float)entity.DirectionY),
                IsPlayerShip = isCurrentEntity
            };

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

    private (int2 position, EquippableItem item)[] CreateEquippableSlots(
        IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot> slots)
    {
        return slots
            .Select(slot => (position: new int2(slot.X, slot.Y), item: CreateEquippableLoadoutItem(slot.Item)))
            .Where(slot => slot.item != null)
            .ToArray();
    }

    private (int2 position, EquippableItem item)[] CreateEquippableSlots(
        IReadOnlyList<AetheriaRuntimeEntityItemSlotSnapshot> slots)
    {
        return slots
            .Select(slot => (position: new int2(slot.X, slot.Y), item: CreateEquippableLoadoutItem(new AetheriaRuntimeLoadoutItemSnapshot(
                slot.ItemKey,
                slot.Quality,
                slot.Durability,
                slot.Quantity,
                slot.Enabled,
                slot.OverrideShutdown))))
            .Where(slot => slot.item != null)
            .ToArray();
    }

    private (int2 position, ItemInstance item)[][] CreateCargoBayContents(
        IReadOnlyList<AetheriaRuntimeCargoBayLoadoutSnapshot> bays)
    {
        return bays
            .Select(bay => bay.Items
                .Select(slot => (position: new int2(slot.X, slot.Y), item: CreateLoadoutItem(slot.Item)))
                .Where(slot => slot.item != null)
                .ToArray())
            .ToArray();
    }

    private EquippableItem CreateEquippableLoadoutItem(AetheriaRuntimeLoadoutItemSnapshot item)
    {
        var instance = CreateLoadoutItem(item) as EquippableItem;
        if (instance != null && item.Durability > 0)
            instance.Durability = (float)item.Durability;
        return instance;
    }

    private ItemInstance CreateLoadoutItem(AetheriaRuntimeLoadoutItemSnapshot item)
    {
        var typedItem = RuntimeCatalog?.FindItem(item.ItemKey);
        if (typedItem == null)
            return null;

        if (typedItem.Stackable)
            return ItemManager.CreateSimpleCommodityInstance(typedItem, Math.Max(1, item.Quantity));

        var instance = ItemManager.CreateCraftedInstance(typedItem, (float)item.Quality);
        if (instance is EquippableItem equippable)
        {
            if (item.Durability > 0)
                equippable.Durability = (float)item.Durability;
        }
        return instance;
    }

    public static Galaxy ObservedGalaxy;
    public static bool IsTutorial;
    public static AetheriaRuntimeCatalogSnapshot RuntimeCatalog { get; private set; }

    public GameSettings Settings;
    //public string StarterShipTemplate = "Longinus";
    public float2 Sensitivity;
    public int Credits = 15000000;
    public float TargetSpottedBlinkFrequency = 20;
    public float TargetSpottedBlinkOffset = -.25f;

    [Header("Postprocessing")]
    public float DeathPostTransitionTime;
    public PostProcessVolume DeathPost;
    public PostProcessVolume HeatstrokePost;
    public PostProcessVolume HypothermiaPost;
    public PostProcessVolume SevereHeatstrokePost;
    public PostProcessVolume SevereHypothermiaPost;

    [Header("Scene Links")]
    public GameObject UiRoot;
    public GameObject HelpScreen;
    public InputDisplayLayout InputDisplayLayout;
    public Transform ActionBar;
    public ActionBarSlot ActionBarSlot;
    public ZoneRenderer ZoneRenderer;
    public CinemachineVirtualCamera DockCamera;
    public CinemachineVirtualCamera FollowCamera;
    public CinemachineVirtualCamera WormholeCamera;
    //public SectorRenderer SectorRenderer;
    public SectorMap SectorMap;

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
    public EventLog EventLog;
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

    private bool _paused;
    private float _time;
    private int _zoomLevelIndex;
    private Entity _currentEntity;

    // private ShipInput _shipInput;
    private float2 _entityYawPitch;
    private float3 _viewDirection;
    private (HardpointData[] hardpoints, Transform[] barrels, PlaceUIElementWorldspace crosshair)[] _articulationGroups;
    private (LockWeapon targetLock, PlaceUIElementWorldspace indicator, Rotate spin)[] _lockingIndicators;
    private Dictionary<Entity, VisibleTargetIndicator> _visibleHostileIndicators = new Dictionary<Entity, VisibleTargetIndicator>();
    private Dictionary<Entity, VisibleTargetIndicator> _visibleFriendlyIndicators = new Dictionary<Entity, VisibleTargetIndicator>();
    private List<IDisposable> _shipSubscriptions = new List<IDisposable>();
    private List<IDisposable> _targetSubscriptions = new List<IDisposable>();
    private float _severeHeatstrokePhase;
    private bool _uiHidden;
    private bool _menuShown;
    private List<ActionBarSlot> _actionBarSlots = new List<ActionBarSlot>();
    private List<InputAction> _actionBarActions = new List<InputAction>();
    private float _hitMarkerTime;

    public AetheriaInput Input { get; private set; }
    public EquippedDockingBay DockingBay { get; private set; }
    public Entity DockedEntity { get; private set; }
    //if better place, please move
    public Entity TowingStation { get; private set; }

    public ZoneEnvironment CurrentEnvironment
    {
        get
        {
            return Settings.DefaultEnvironment;
        }
    }

    public Entity CurrentEntity
    {
        get => _currentEntity;
        set => _currentEntity = value;
    }

    public ItemManager ItemManager { get; private set; }
    public Zone Zone { get; private set; }
    public List<AetheriaRuntimeLoadoutTemplateSnapshot> LoadoutTemplates { get; } = new List<AetheriaRuntimeLoadoutTemplateSnapshot>();

    private readonly (float2 direction, string name)[] _directions = {
        (new float2(0, 1), "Front"),
        (new float2(1, 0), "Right"),
        (new float2(-1, 0), "Left"),
        (new float2(0, -1), "Rear")
    };

    public DragObject DragObject { get; private set; }
    private Action<DragObject> _endDragCallback;

    private List<Story> _stories = new List<Story>();

    public EntitySettings NewEntitySettings
    {
        get => Settings.GameplaySettings.DefaultEntitySettings.Copy();
    }

    private AetheriaRuntimeLoadoutTemplateCommit ProjectLoadoutTemplate(EntityConstructionBlueprint blueprint)
    {
        return new AetheriaRuntimeLoadoutTemplateCommit
        {
            Name = blueprint.Name ?? "",
            OwnerPlayerKey = $"global:aetheria.player_settings.v1",
            RootEntity = ProjectEntityLoadout(blueprint)
        };
    }

    private AetheriaRuntimeEntityLoadoutCommit ProjectEntityLoadout(EntityConstructionBlueprint blueprint)
    {
        return new AetheriaRuntimeEntityLoadoutCommit
        {
            Name = blueprint.Name ?? "",
            Kind = blueprint is ShipConstructionBlueprint ? "ship" : blueprint is OrbitalEntityConstructionBlueprint ? "orbital" : "entity",
            FactionKey = blueprint.FactionKey ?? "",
            Hull = ProjectLoadoutItem(blueprint.Hull),
            Equipment = ProjectSlots(blueprint.Equipment),
            CargoBays = ProjectSlots(blueprint.CargoBays),
            DockingBays = ProjectSlots(blueprint.DockingBays),
            CargoContents = ProjectCargoBays(blueprint.CargoContents),
            DockingBayContents = ProjectCargoBays(blueprint.DockingBayContents),
            DockingBayAssignments = blueprint.DockingBayAssignments ?? Array.Empty<int>(),
            WeaponGroups = blueprint.WeaponGroups?.Select(group => (IReadOnlyList<int>)group).ToArray() ?? Array.Empty<IReadOnlyList<int>>(),
            Children = blueprint.Children?.Select(ProjectEntityLoadout).ToArray() ?? Array.Empty<AetheriaRuntimeEntityLoadoutCommit>()
        };
    }

    private static AetheriaRuntimeLoadoutItemSlotCommit[] ProjectSlots((int2 position, EquippableItem item)[] slots)
    {
        return slots?
            .Select(slot => new AetheriaRuntimeLoadoutItemSlotCommit
            {
                X = slot.position.x,
                Y = slot.position.y,
                Item = ProjectLoadoutItem(slot.item)
            })
            .ToArray() ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
    }

    private static AetheriaRuntimeCargoBayLoadoutCommit[] ProjectCargoBays((int2 position, ItemInstance item)[][] bays)
    {
        return bays?
            .Select(bay => new AetheriaRuntimeCargoBayLoadoutCommit
            {
                Items = bay?
                    .Select(slot => new AetheriaRuntimeLoadoutItemSlotCommit
                    {
                        X = slot.position.x,
                        Y = slot.position.y,
                        Item = ProjectLoadoutItem(slot.item)
                    })
                    .ToArray() ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()
            })
            .ToArray() ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
    }

    private static AetheriaRuntimeLoadoutItemCommit ProjectLoadoutItem(ItemInstance item)
    {
        if (item == null)
            return new AetheriaRuntimeLoadoutItemCommit();

        return new AetheriaRuntimeLoadoutItemCommit
        {
            ItemKey = item.ItemKey,
            Quality = item is CraftedItemInstance crafted ? crafted.Quality : 1.0,
            Durability = item is EquippableItem equippable ? equippable.Durability : 1.0,
            Quantity = item is SimpleCommodity commodity ? commodity.Quantity : 1,
            Enabled = true,
            OverrideShutdown = item is EquippableItem overrideable && overrideable.OverrideShutdown
        };
    }

    private static AetheriaRuntimeActionBarBindingCommit ToActionBarBindingCommit(
        AetheriaRuntimeActionBarBindingSnapshot binding)
    {
        return binding == null
            ? null
            : new AetheriaRuntimeActionBarBindingCommit
            {
                ControlPath = binding.ControlPath ?? "",
                Kind = binding.Kind ?? "",
                EquipmentIndex = binding.EquipmentIndex,
                BehaviorIndex = binding.BehaviorIndex,
                WeaponGroup = binding.WeaponGroup,
                ItemKey = binding.TargetKey ?? ""
            };
    }

    private static AetheriaRuntimeActionBarBindingSnapshot ToActionBarBindingSnapshot(
        AetheriaRuntimeActionBarBindingCommit binding)
    {
        return binding == null
            ? null
            : new AetheriaRuntimeActionBarBindingSnapshot(
                binding.ControlPath ?? "",
                binding.Kind ?? "",
                binding.ItemKey ?? "",
                binding.EquipmentIndex,
                binding.BehaviorIndex,
                binding.WeaponGroup);
    }

    private void RequestActionBarBinding(ActionBarSlot slot, DragObject dragAction)
    {
        if (slot == null || dragAction == null || CurrentEntity == null)
            return;

        var bindingCommit = CreateActionBarBindingCommit(slot, CurrentEntity, dragAction);
        if (bindingCommit == null)
            return;

        TryRequestDaemonActionBarBinding(bindingCommit);
    }

    public int GetActionBarSlotCount()
    {
        return _actionBarSlots?.Count ?? 0;
    }

    public string GetActionBarSlotLabel(int slotIndex)
    {
        var slot = ResolveActionBarSlot(slotIndex);
        return slot == null ? "" : GetActionBarSlotLabel(slot);
    }

    public string GetActionBarBindingLabel(int slotIndex)
    {
        var slot = ResolveActionBarSlot(slotIndex);
        return slot?.Binding switch
        {
            ActionBarWeaponGroupBinding weaponGroup => $"G{weaponGroup.Group + 1}",
            ActionBarConsumableBinding consumable => consumable.Target?.Name ?? "Consumable",
            ActionBarGearBinding gear => RuntimeCatalog?.FindItem(gear.Item?.EquippableItem?.ItemKey ?? "")?.Name ?? "Gear",
            _ => "Empty"
        };
    }

    public void RequestWeaponGroupActionBarBinding(int slotIndex, int groupIndex)
    {
        var slot = ResolveActionBarSlot(slotIndex);
        if (slot == null)
        {
            return;
        }

        var binding = new AetheriaRuntimeActionBarBindingCommit
        {
            ControlPath = slot.ControlPath ?? "",
            Kind = "weapon_group",
            WeaponGroup = groupIndex
        };
        TryRequestDaemonActionBarBinding(binding);
    }

    public void RequestClearActionBarBinding(int slotIndex)
    {
        var slot = ResolveActionBarSlot(slotIndex);
        if (slot == null)
            return;

        TryRequestDaemonActionBarBindingClear(slot);
    }

    private void RestoreActionBarBindingsFromTypedRun(
        IReadOnlyList<AetheriaRuntimeActionBarBindingSnapshot> bindings)
    {
        var restoredBindings = bindings?
            .Select(ToActionBarBindingCommit)
            .Where(binding => binding != null)
            .ToArray();
        ApplyActionBarBindings(restoredBindings);
    }

    private void ApplyActionBarBindings(IReadOnlyList<AetheriaRuntimeActionBarBindingCommit> bindings)
    {
        if (_actionBarSlots == null || _actionBarSlots.Count == 0)
            return;

        foreach (var slot in _actionBarSlots)
            slot.Binding = null;

        if (CurrentEntity == null)
            return;

        if (bindings == null || bindings.Count == 0)
            return;

        var slotsByControlPath = _actionBarSlots
            .Where(slot => !string.IsNullOrWhiteSpace(slot?.ControlPath))
            .GroupBy(slot => slot.ControlPath, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var binding in bindings)
        {
            if (binding == null ||
                string.IsNullOrWhiteSpace(binding.ControlPath) ||
                !slotsByControlPath.TryGetValue(binding.ControlPath, out var slot))
            {
                continue;
            }

            var slotBinding = CreateActionBarBinding(slot, CurrentEntity, binding);
            if (slotBinding != null)
                slot.Binding = slotBinding;
        }
    }

    private ActionBarBinding CreateActionBarBinding(ActionBarSlot slot, Entity entity, DragObject dragAction)
    {
        var binding = CreateActionBarBindingCommit(slot, entity, dragAction);
        return binding == null ? null : CreateActionBarBinding(slot, entity, binding);
    }

    private AetheriaRuntimeActionBarBindingCommit CreateActionBarBindingCommit(
        ActionBarSlot slot,
        Entity entity,
        DragObject dragAction)
    {
        if (slot == null || entity == null || dragAction == null)
            return null;

        switch (dragAction)
        {
            case EquippedItemDragObject equippedItemDragAction:
                var equippedItem = equippedItemDragAction.EquippedItem;
                var trigger = equippedItem?.GetBehavior<IActivatedBehavior>();
                var equipmentIndex = equippedItem == null ? -1 : entity.Equipment.IndexOf(equippedItem);
                var behaviorIndex = equippedItem?.Behaviors == null ? -1 : Array.IndexOf(equippedItem.Behaviors, trigger);
                return trigger == null || equipmentIndex < 0 || behaviorIndex < 0
                    ? null
                    : new AetheriaRuntimeActionBarBindingCommit
                    {
                        ControlPath = slot.ControlPath ?? "",
                        Kind = "gear",
                        ItemKey = equippedItem.EquippableItem?.ItemKey ?? "",
                        EquipmentIndex = equipmentIndex,
                        BehaviorIndex = behaviorIndex
                    };
            case ItemInstanceDragObject itemInstanceDragAction:
                var consumable = FindTypedActionBarConsumable(itemInstanceDragAction.Item);
                return consumable == null
                    ? null
                    : new AetheriaRuntimeActionBarBindingCommit
                    {
                        ControlPath = slot.ControlPath ?? "",
                        Kind = "consumable",
                        ItemKey = consumable.ItemKey ?? ""
                    };
            default:
                return null;
        }
    }

    private ActionBarBinding CreateActionBarBinding(
        ActionBarSlot slot,
        Entity entity,
        AetheriaRuntimeActionBarBindingCommit binding)
    {
        if (slot == null || entity == null || binding == null)
            return null;

        switch (binding.Kind)
        {
            case "consumable":
                var consumable = RuntimeCatalog?.FindItem(binding.ItemKey ?? "");
                return consumable != null &&
                       string.Equals(consumable.Category, AetheriaRuntimeItemCategories.Consumable, StringComparison.Ordinal)
                    ? new ActionBarConsumableBinding(entity, slot, consumable)
                    : null;
            case "gear":
                if (binding.EquipmentIndex < 0 || binding.EquipmentIndex >= entity.Equipment.Count)
                    return null;

                var equippedItem = entity.Equipment[binding.EquipmentIndex];
                var behaviors = equippedItem?.Behaviors;
                if (equippedItem == null ||
                    !string.Equals(equippedItem.EquippableItem?.ItemKey ?? "", binding.ItemKey ?? "", StringComparison.Ordinal) ||
                    behaviors == null ||
                    binding.BehaviorIndex < 0 ||
                    binding.BehaviorIndex >= behaviors.Length)
                {
                    return null;
                }

                if (!(behaviors[binding.BehaviorIndex] is IActivatedBehavior activatedBehavior))
                    return null;

                return new ActionBarGearBinding(entity, slot, equippedItem, activatedBehavior);
            case "weapon_group":
                return entity.WeaponGroups != null &&
                       binding.WeaponGroup >= 0 &&
                       binding.WeaponGroup < entity.WeaponGroups.Length
                    ? new ActionBarWeaponGroupBinding(entity, slot, binding.WeaponGroup)
                    : null;
            default:
                return null;
        }
    }

    private ActionBarSlot ResolveActionBarSlot(int slotIndex)
    {
        return _actionBarSlots != null &&
               slotIndex >= 0 &&
               slotIndex < _actionBarSlots.Count
            ? _actionBarSlots[slotIndex]
            : null;
    }

    private static string GetActionBarSlotLabel(ActionBarSlot slot)
    {
        var controlPath = slot?.ControlPath ?? "";
        if (string.IsNullOrWhiteSpace(controlPath))
            return "Action Bar";

        var slashIndex = controlPath.LastIndexOf('/');
        return slashIndex >= 0 && slashIndex < controlPath.Length - 1
            ? controlPath.Substring(slashIndex + 1)
            : controlPath;
    }

    private AetheriaRuntimeCatalogItem FindTypedActionBarConsumable(ItemInstance item)
    {
        var typedItem = FindTypedActionBarItem(item);
        return typedItem != null &&
               string.Equals(typedItem.Category, AetheriaRuntimeItemCategories.Consumable, StringComparison.Ordinal)
            ? typedItem
            : null;
    }

    private AetheriaRuntimeCatalogItem FindTypedActionBarItem(ItemInstance item)
    {
        return RuntimeCatalog?.FindItem(item?.ItemKey ?? "");
    }

    private static int ZoneIndex(GalaxyZone zone)
    {
        return ObservedGalaxy?.Zones == null || zone == null ? -1 : Array.IndexOf(ObservedGalaxy.Zones, zone);
    }

    private static int FactionIndex(Faction faction)
    {
        return ObservedGalaxy?.Factions == null || faction == null ? -1 : Array.IndexOf(ObservedGalaxy.Factions, faction);
    }

    public void RequestLoadoutTemplateSave(EntityConstructionBlueprint blueprint)
    {
        var loadout = ProjectLoadoutTemplate(blueprint);
        SendRuntimeLoadoutTemplateCommand(loadout, "unity-inventory", "loadout template save");
    }

    private static void SendRuntimeLoadoutTemplateCommand(
        AetheriaRuntimeLoadoutTemplateCommit loadout,
        string clientId,
        string label)
    {
        try
        {
            if (!AetheriaRuntimeEveCommands.TrySendLoadoutTemplateCommand(
                    RuntimeStateFilePath,
                    loadout,
                    clientId,
                    out var submitted,
                    out var error))
            {
                Debug.LogError($"Failed to submit Aetheria {label} Eve command: {error}");
                return;
            }

            Debug.Log($"Submitted Aetheria {label} Eve command: {submitted!.CommandId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send Aetheria {label} Eve command: {ex}");
        }
    }

    public void RequestRuntimeLoadoutRestore(AetheriaRuntimeLoadoutTemplateSnapshot template)
    {
        var blueprint = CreateEntityConstructionBlueprint(template);
        if (blueprint == null ||
            Zone == null ||
            DockedEntity == null)
        {
            return;
        }

        var price = blueprint.Price(ItemManager);
        var observer = ResolveDaemonObserver();
        if (observer != null && observer.HasAuthoritativeState)
        {
            RequestDaemonLoadoutRestore(observer, template, price);
        }
    }

    private void RequestDaemonLoadoutRestore(
        AetheriaDaemonObserver observer,
        AetheriaRuntimeLoadoutTemplateSnapshot template,
        int price)
    {
        if (observer == null ||
            !observer.HasAuthoritativeState ||
            template == null ||
            string.IsNullOrWhiteSpace(template.Name) ||
            price < 0)
        {
            return;
        }

        var dockedEntityKey = ResolveEntityRecordKey(DockedEntity);
        if (string.IsNullOrWhiteSpace(dockedEntityKey))
        {
            return;
        }

        try
        {
            observer.Operations.RestoreLoadout(dockedEntityKey, template.Name, price);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon loadout restore operation: {ex.Message}");
        }
    }

    public void RequestDockedCurrentShip(Ship ship)
    {
        if (ship == null)
        {
            return;
        }

        TryRequestDaemonDockedCurrentShip(ship);
    }

    private static AetheriaRuntimeEntityLoadoutSnapshot CreateRuntimeEntityLoadoutSnapshot(
        AetheriaRuntimeEntityLoadoutCommit entity)
    {
        entity ??= new AetheriaRuntimeEntityLoadoutCommit();
        return new AetheriaRuntimeEntityLoadoutSnapshot(
            entity.Name ?? "",
            entity.Kind ?? "",
            entity.FactionKey ?? "",
            CreateRuntimeLoadoutItemSnapshot(entity.Hull),
            CreateRuntimeLoadoutItemSlotSnapshots(entity.Equipment),
            CreateRuntimeLoadoutItemSlotSnapshots(entity.CargoBays),
            CreateRuntimeLoadoutItemSlotSnapshots(entity.DockingBays),
            CreateRuntimeCargoBayLoadoutSnapshots(entity.CargoContents),
            CreateRuntimeCargoBayLoadoutSnapshots(entity.DockingBayContents),
            (entity.DockingBayAssignments ?? Array.Empty<int>()).ToArray(),
            (entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>())
                .Select(group => (IReadOnlyList<int>)group.ToArray())
                .ToArray(),
            (entity.Children ?? Array.Empty<AetheriaRuntimeEntityLoadoutCommit>())
                .Select(CreateRuntimeEntityLoadoutSnapshot)
                .ToArray());
    }

    private static AetheriaRuntimeLoadoutItemSnapshot CreateRuntimeLoadoutItemSnapshot(
        AetheriaRuntimeLoadoutItemCommit item)
    {
        item ??= new AetheriaRuntimeLoadoutItemCommit();
        return new AetheriaRuntimeLoadoutItemSnapshot(
            item.ItemKey ?? "",
            item.Quality,
            item.Durability,
            item.Quantity,
            item.Enabled,
            item.OverrideShutdown);
    }

    private static IReadOnlyList<AetheriaRuntimeLoadoutItemSlotSnapshot> CreateRuntimeLoadoutItemSlotSnapshots(
        IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> slots)
    {
        return (slots ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
            .Select(slot => new AetheriaRuntimeLoadoutItemSlotSnapshot(
                slot.X,
                slot.Y,
                CreateRuntimeLoadoutItemSnapshot(slot.Item)))
            .ToArray();
    }

    private static IReadOnlyList<AetheriaRuntimeCargoBayLoadoutSnapshot> CreateRuntimeCargoBayLoadoutSnapshots(
        IReadOnlyList<AetheriaRuntimeCargoBayLoadoutCommit> cargoBays)
    {
        return (cargoBays ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
            .Select(bay => new AetheriaRuntimeCargoBayLoadoutSnapshot(
                CreateRuntimeLoadoutItemSlotSnapshots(bay.Items)))
            .ToArray();
    }

    public void RequestEntityOverrideShutdown(Entity entity, bool enabled)
    {
        if (entity == null)
            return;

        if (TryRequestDaemonEntityOverrideShutdown(entity, enabled))
            return;
    }

    public void RequestEntityName(Entity entity, string name)
    {
        if (entity == null)
            return;

        if (TryRequestDaemonEntityName(entity, name))
            return;
    }

    public void RequestWeaponGroupMembership(EquippedItem item, int groupIndex, bool assigned)
    {
        if (item == null)
        {
            return;
        }

        TryRequestDaemonWeaponGroupMembership(item, groupIndex, assigned);
    }

    public void RequestCargoItemTransfer(EquippedCargoBay origin, EquippedCargoBay destination, ItemInstance item)
    {
        if (origin == null ||
            destination == null ||
            item == null)
        {
            return;
        }

        TryRequestDaemonCargoItemTransfer(origin, destination, item, default, false);
    }

    public void RequestCargoItemTransfer(
        EquippedCargoBay origin,
        EquippedCargoBay destination,
        ItemInstance item,
        int2 destinationPosition)
    {
        if (origin == null ||
            destination == null ||
            item == null)
        {
            return;
        }

        TryRequestDaemonCargoItemTransfer(origin, destination, item, destinationPosition, true);
    }

    private bool TryRequestDaemonCargoItemTransfer(
        EquippedCargoBay origin,
        EquippedCargoBay destination,
        ItemInstance item,
        int2 destinationPosition,
        bool hasDestinationPosition)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        if (!TryResolveCargoBayCommandTarget(origin, out var originEntityKey, out var originCargoIndex) ||
            !TryResolveCargoBayCommandTarget(destination, out var destinationEntityKey, out var destinationCargoIndex) ||
            item == null ||
            string.IsNullOrWhiteSpace(item.ItemKey))
        {
            return false;
        }

        var quantity = item is SimpleCommodity commodity ? commodity.Quantity : 1;
        if (quantity <= 0)
        {
            return false;
        }

        try
        {
            observer.Operations.TransferCargoItem(
                originEntityKey,
                originCargoIndex,
                destinationEntityKey,
                destinationCargoIndex,
                item.ItemKey,
                quantity,
                int.MinValue,
                int.MinValue,
                destinationPosition.x,
                destinationPosition.y,
                hasDestinationPosition);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon cargo transfer operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    public void RequestCargoItemEquip(EquippedCargoBay origin, Entity destination, EquippableItem item)
    {
        if (origin == null ||
            destination == null ||
            item == null)
        {
            return;
        }

        TryRequestDaemonCargoItemEquip(origin, destination, item, default, false);
    }

    public void RequestCargoItemEquip(
        EquippedCargoBay origin,
        Entity destination,
        EquippableItem item,
        int2 destinationPosition)
    {
        if (origin == null ||
            destination == null ||
            item == null)
        {
            return;
        }

        TryRequestDaemonCargoItemEquip(origin, destination, item, destinationPosition, true);
    }

    public void RequestEquippedItemStore(Entity origin, EquippedItem equippedItem, EquippedCargoBay destination)
    {
        if (origin == null ||
            equippedItem?.EquippableItem == null ||
            destination == null)
        {
            return;
        }

        TryRequestDaemonEquippedItemStore(origin, equippedItem, destination, default, false);
    }

    public void RequestEquippedItemStore(
        Entity origin,
        EquippedItem equippedItem,
        EquippedCargoBay destination,
        int2 destinationPosition)
    {
        if (origin == null ||
            equippedItem?.EquippableItem == null ||
            destination == null)
        {
            return;
        }

        TryRequestDaemonEquippedItemStore(origin, equippedItem, destination, destinationPosition, true);
    }

    public void RequestEquippedItemEquip(
        Entity origin,
        EquippedItem equippedItem,
        Entity destination,
        int2 destinationPosition)
    {
        if (origin == null ||
            equippedItem?.EquippableItem == null ||
            destination == null)
        {
            return;
        }

        TryRequestDaemonEquippedItemEquip(origin, equippedItem, destination, destinationPosition);
    }

    private bool TryRequestDaemonCargoItemEquip(
        EquippedCargoBay origin,
        Entity destination,
        EquippableItem item,
        int2 destinationPosition,
        bool hasDestinationPosition)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        if (!TryResolveCargoBayCommandTarget(origin, out var originEntityKey, out var originCargoIndex) ||
            destination == null ||
            item == null ||
            string.IsNullOrWhiteSpace(item.ItemKey))
        {
            return false;
        }

        var destinationEntityKey = ResolveEntityRecordKey(destination);
        if (string.IsNullOrWhiteSpace(destinationEntityKey))
        {
            return false;
        }

        try
        {
            observer.Operations.EquipItem(
                "cargo",
                originEntityKey,
                originCargoIndex,
                destinationEntityKey,
                item.ItemKey,
                int.MinValue,
                int.MinValue,
                destinationPosition.x,
                destinationPosition.y,
                hasDestinationPosition);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon item equip operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private bool TryRequestDaemonEquippedItemStore(
        Entity origin,
        EquippedItem equippedItem,
        EquippedCargoBay destination,
        int2 destinationPosition,
        bool hasDestinationPosition)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        if (!TryResolveEquippedItemCommandTarget(equippedItem, out var originEntityKey, out var sourceEquipmentIndex) ||
            !TryResolveCargoBayCommandTarget(destination, out var destinationEntityKey, out var destinationCargoIndex) ||
            origin == null ||
            equippedItem?.EquippableItem == null)
        {
            return false;
        }

        try
        {
            observer.Operations.StoreItem(
                originEntityKey,
                sourceEquipmentIndex,
                destinationEntityKey,
                destinationCargoIndex,
                equippedItem.EquippableItem.ItemKey,
                destinationPosition.x,
                destinationPosition.y,
                hasDestinationPosition);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon item store operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private bool TryRequestDaemonEquippedItemEquip(
        Entity origin,
        EquippedItem equippedItem,
        Entity destination,
        int2 destinationPosition)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        if (!TryResolveEquippedItemCommandTarget(equippedItem, out var originEntityKey, out var sourceEquipmentIndex) ||
            origin == null ||
            destination == null ||
            equippedItem?.EquippableItem == null)
        {
            return false;
        }

        var destinationEntityKey = ResolveEntityRecordKey(destination);
        if (string.IsNullOrWhiteSpace(destinationEntityKey))
        {
            return false;
        }

        try
        {
            observer.Operations.EquipItem(
                "equipment",
                originEntityKey,
                sourceEquipmentIndex,
                destinationEntityKey,
                equippedItem.EquippableItem.ItemKey,
                equippedItem.Position.x,
                equippedItem.Position.y,
                destinationPosition.x,
                destinationPosition.y,
                true);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon equipped-item equip operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    public void RequestTradePurchase(
        EquippedCargoBay stationInventory,
        EquippedCargoBay targetCargo,
        CraftedItemInstance item,
        int price,
        bool createsDockedShip)
    {
        if (item == null ||
            price < 0)
        {
            return;
        }

        if (createsDockedShip)
        {
            if (!(item is EquippableItem) ||
                Zone == null ||
                DockedEntity == null)
            {
                return;
            }

            TryRequestDaemonTradePurchase(stationInventory, targetCargo, item, 1, price, price, true);
            return;
        }

        if (stationInventory == null ||
            targetCargo == null)
        {
            return;
        }

        TryRequestDaemonTradePurchase(stationInventory, targetCargo, item, 1, price, price, false);
    }

    public void RequestTradePurchase(
        EquippedCargoBay stationInventory,
        EquippedCargoBay targetCargo,
        SimpleCommodity item,
        int quantity,
        int unitPrice)
    {
        if (stationInventory == null ||
            targetCargo == null ||
            item == null ||
            quantity <= 0 ||
            unitPrice < 0)
        {
            return;
        }

        var totalPrice = (long)quantity * unitPrice;
        if (totalPrice > int.MaxValue)
            return;

        TryRequestDaemonTradePurchase(stationInventory, targetCargo, item, quantity, unitPrice, (int)totalPrice, false);
    }

    private bool TryRequestDaemonTradePurchase(
        EquippedCargoBay stationInventory,
        EquippedCargoBay targetCargo,
        ItemInstance item,
        int quantity,
        int unitPrice,
        int totalPrice,
        bool createsDockedShip)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        if (item == null ||
            string.IsNullOrWhiteSpace(item.ItemKey) ||
            quantity <= 0 ||
            unitPrice < 0 ||
            totalPrice < 0)
        {
            return false;
        }

        var stationEntityKey = "";
        var stationCargoIndex = -1;
        var sourcePosition = new int2(-1, -1);
        if (stationInventory != null)
        {
            TryResolveCargoBayCommandTarget(stationInventory, out stationEntityKey, out stationCargoIndex);
            stationInventory.Cargo.TryGetValue(item, out sourcePosition);
        }

        var targetEntityKey = "";
        var targetCargoIndex = -1;
        if (targetCargo != null)
        {
            TryResolveCargoBayCommandTarget(targetCargo, out targetEntityKey, out targetCargoIndex);
        }

        if (createsDockedShip)
        {
            targetEntityKey = ResolveEntityRecordKey(DockedEntity);
            targetCargoIndex = -1;
        }
        else if (string.IsNullOrWhiteSpace(stationEntityKey) ||
                 stationCargoIndex < 0 ||
                 string.IsNullOrWhiteSpace(targetEntityKey) ||
                 targetCargoIndex < 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(targetEntityKey))
        {
            return false;
        }

        var purchaseKind = createsDockedShip
            ? "docked_ship"
            : item is SimpleCommodity
                ? "commodity"
                : "crafted";

        try
        {
            observer.Operations.TradePurchase(
                purchaseKind,
                item.ItemKey,
                quantity,
                unitPrice,
                totalPrice,
                stationEntityKey,
                stationCargoIndex,
                targetEntityKey,
                targetCargoIndex,
                sourcePosition.x,
                sourcePosition.y,
                createsDockedShip);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon trade purchase operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    public void RequestEquippedItemOverrideShutdown(EquippedItem item, bool enabled)
    {
        if (item?.EquippableItem == null)
            return;

        if (TryRequestDaemonEquippedItemOverrideShutdown(item, enabled))
            return;
    }

    public void RequestThermotoggleTargetTemperature(Thermotoggle thermotoggle, float targetTemperature)
    {
        if (thermotoggle == null)
            return;

        if (TryRequestDaemonThermotoggleTargetTemperature(thermotoggle, targetTemperature))
            return;
    }

    public void RequestEntityShutdownPerformance(Entity entity, float shutdownPerformance)
    {
        if (entity == null)
            return;

        if (TryRequestDaemonEntityShutdownPerformance(entity, shutdownPerformance))
            return;
    }

    public void RequestHullConductivityToggle(Entity entity, int2 position, int axis)
    {
        if (entity == null)
        {
            return;
        }

        TryRequestDaemonHullConductivityToggle(entity, position, axis);
    }

    private bool TryRequestDaemonHullConductivityToggle(Entity entity, int2 position, int axis)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        var targetEntityKey = ResolveEntityRecordKey(entity);
        if (string.IsNullOrWhiteSpace(targetEntityKey))
        {
            return false;
        }

        try
        {
            observer.Operations.ToggleHullConductivity(targetEntityKey, position.x, position.y, axis);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon hull-conductivity operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private bool TryRequestDaemonEntityName(Entity entity, string name)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        var targetEntityKey = ResolveEntityRecordKey(entity);
        if (string.IsNullOrWhiteSpace(targetEntityKey))
        {
            return false;
        }

        try
        {
            observer.Operations.SetEntityName(targetEntityKey, name ?? "");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon entity-name operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private bool TryRequestDaemonEquippedItemOverrideShutdown(EquippedItem item, bool enabled)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        if (!TryResolveEquippedItemCommandTarget(item, out var targetEntityKey, out var equipmentIndex))
        {
            return false;
        }

        try
        {
            observer.Operations.SetItemOverrideShutdown(targetEntityKey, equipmentIndex, enabled);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon item override-shutdown operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private bool TryRequestDaemonWeaponGroupMembership(EquippedItem item, int groupIndex, bool assigned)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        if (!TryResolveEquippedItemCommandTarget(item, out var targetEntityKey, out var equipmentIndex))
        {
            return false;
        }

        try
        {
            observer.Operations.SetWeaponGroupMembership(targetEntityKey, equipmentIndex, groupIndex, assigned);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon weapon-group membership operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private bool TryRequestDaemonThermotoggleTargetTemperature(Thermotoggle thermotoggle, float targetTemperature)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        var item = thermotoggle.Item;
        if (!TryResolveEquippedItemCommandTarget(item, out var targetEntityKey, out var equipmentIndex))
        {
            return false;
        }

        var behaviorIndex = Array.IndexOf(item.Behaviors, thermotoggle);
        if (behaviorIndex < 0)
        {
            return false;
        }

        try
        {
            observer.Operations.SetThermotoggleTargetTemperature(
                targetEntityKey,
                equipmentIndex,
                behaviorIndex,
                targetTemperature);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon thermotoggle operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private bool TryRequestDaemonEntityShutdownPerformance(Entity entity, float shutdownPerformance)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        var targetEntityKey = ResolveEntityRecordKey(entity);
        if (string.IsNullOrWhiteSpace(targetEntityKey))
        {
            return false;
        }

        try
        {
            observer.Operations.SetShutdownPerformance(targetEntityKey, shutdownPerformance);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon shutdown-performance operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private bool TryRequestDaemonEntityOverrideShutdown(Entity entity, bool enabled)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        var targetEntityKey = ResolveEntityRecordKey(entity);
        if (string.IsNullOrWhiteSpace(targetEntityKey))
        {
            return false;
        }

        try
        {
            observer.Operations.SetEntityOverrideShutdown(targetEntityKey, enabled);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon entity override-shutdown operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private bool TryRequestDaemonActionBarBinding(AetheriaRuntimeActionBarBindingCommit binding)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState || binding == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(binding.ControlPath) ||
            string.IsNullOrWhiteSpace(binding.Kind))
        {
            return false;
        }

        try
        {
            observer.Operations.SetActionBarBinding(
                binding.ControlPath,
                binding.Kind,
                binding.ItemKey ?? "",
                binding.EquipmentIndex,
                binding.BehaviorIndex,
                binding.WeaponGroup);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon action-bar binding operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private bool TryRequestDaemonActionBarBindingClear(ActionBarSlot slot)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState || slot == null)
        {
            return false;
        }

        var controlPath = slot.ControlPath ?? "";
        if (string.IsNullOrWhiteSpace(controlPath))
        {
            return false;
        }

        try
        {
            observer.Operations.ClearActionBarBinding(controlPath);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon action-bar clear operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private bool TryResolveEquippedItemCommandTarget(
        EquippedItem item,
        out string targetEntityKey,
        out int equipmentIndex)
    {
        targetEntityKey = "";
        equipmentIndex = -1;

        var entity = item?.Entity;
        if (entity?.Equipment == null)
        {
            return false;
        }

        equipmentIndex = entity.Equipment.IndexOf(item);
        if (equipmentIndex < 0)
        {
            return false;
        }

        targetEntityKey = ResolveEntityRecordKey(entity);
        return !string.IsNullOrWhiteSpace(targetEntityKey);
    }

    private bool TryResolveCargoBayCommandTarget(
        EquippedCargoBay cargoBay,
        out string targetEntityKey,
        out int equipmentIndex)
    {
        return TryResolveEquippedItemCommandTarget(cargoBay, out targetEntityKey, out equipmentIndex);
    }

    private void OnDisable()
    {
        Input.Dispose();
    }

    void Start()
    {
        Instance = this;
        var stateBoot = AetheriaRuntimeStateBoot.Inspect(GameDataDirectory);
        _runtimeStateFilePath = stateBoot.StateFilePath;
        Debug.Log(
            $"Aetheria runtime target: {stateBoot.TargetLabel} via {stateBoot.TargetKind} ({stateBoot.TargetSource})");
        Debug.Log($"Aetheria runtime state file: {stateBoot.StateFilePath}");
        if (!stateBoot.SupportsLocalStateFileRead)
        {
            throw new InvalidOperationException(
                $"Aetheria runtime target cannot read the daemon mirror state required for gameplay boot: {stateBoot.FailureMessage}");
        }

        if (!stateBoot.StateFileExists)
        {
            throw new InvalidOperationException(
                $"Aetheria runtime state file is missing at {stateBoot.StateFilePath}; gameplay requires an authoritative daemon mirror.");
        }

        RuntimeCatalog = AetheriaRuntimeStateReader.OpenRuntimeCatalog(stateBoot.StateFilePath);
        Debug.Log($"Aetheria runtime catalog: {RuntimeCatalog.Items.Count} items, {RuntimeCatalog.Corporations.Count} corporations, {RuntimeCatalog.NameFiles.Count} name files");

        if (RuntimeCatalog == null)
        {
            throw new InvalidOperationException("Aetheria typed runtime catalog is required before gameplay boot.");
        }

        ItemManager = new ItemManager(
            RuntimeCatalog,
            Settings.GameplaySettings,
            Debug.Log);
        LoadRuntimeLoadoutTemplates(stateBoot.StateFilePath);
        ZoneRenderer.ItemManager = ItemManager;

        // If hiding minimap asteroids, turn them off to start with
        if (!RuntimePlayerSettings.GraphicsSettings.ShowAsteroidsInMinimap)
            ZoneRenderer.ShowAsteroidUI = false;

        // TODO: Process Stories

        #region Input Handling

        Input = new AetheriaInput();
        foreach (var x in RuntimePlayerSettings.InputSettings.InputActionMap) Input.asset[x.Key.action].ApplyBindingOverride(x.Key.binding, x.Value);

        InputDisplayLayout.Input = Input.asset;
        Input.Global.Enable();

        _zoomLevelIndex = Settings.DefaultMinimapZoom;
        Input.Player.MinimapZoom.performed += context =>
        {
            _zoomLevelIndex = (_zoomLevelIndex + 1) % Settings.MinimapZoomLevels.Length;
            ZoneRenderer.MinimapDistance = Settings.MinimapZoomLevels[_zoomLevelIndex];
        };

        Input.Global.ZoneMap.performed += context =>
        {
            ToggleMenuTab(MenuTab.Map);
                MenuMap.Position = AetheriaMath.ToUnity(CurrentEntity.CultPositionXZ);
        };

        Input.Global.Inventory.performed += context => ToggleMenuTab(MenuTab.Inventory);

        Input.Global.GalaxyMap.performed += context => ToggleMenuTab(MenuTab.Galaxy);

        Input.Global.Interact.performed += context =>
        {
            if (EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null) return;
            if (MainMenu.gameObject.activeSelf) return;
            if (CurrentEntity == null)
            {
                // TODO: SFX: Fail
                Dialog.Clear();
                Dialog.Title.text = "Can't undock. You dont have a ship!";
                Dialog.Show();
                Dialog.MoveToCursor();
            }
            else RequestInteract();
        };

        Input.Global.MainMenu.performed += context =>
        {
            if(Menu.gameObject.activeSelf)
                ToggleMenuTab(Menu.CurrentTab);
            else
                ToggleFullscreenMenu(MainMenu.gameObject);
        };

        Input.Global.InputScreen.performed += context => ToggleFullscreenMenu(HelpScreen);

        Input.Player.HideUI.performed += context =>
        {
            _uiHidden = !_uiHidden;
            GameplayUI.alpha = _uiHidden ? 0 : 1;
            ActionBar.gameObject.SetActive(!_uiHidden);
        };

        Input.Player.OverrideShutdown.performed += context =>
        {
            if (CurrentEntity != null)
                RequestOverrideShutdown(!CurrentEntity.OverrideShutdown);
        };

        Input.Player.Ping.performed += context =>
        {
            RequestSensorPing();
        };

        Input.Player.ToggleHeatsinks.performed += context =>
        {
            RequestHeatsinksEnabled(!CurrentEntity.HeatsinksEnabled);
            // TODO: SFX: Success/Fail
        };

        Input.Player.ToggleShield.performed += context =>
        {
            RequestShieldToggle();
            // TODO: SFX: Success/Fail
        };

        #region Targeting

        Input.Player.TargetReticle.performed += context =>
        {
            RequestTargetReticle();
        };

        Input.Player.TargetNearest.performed += context =>
        {
            RequestTargetNearest();
        };

        Input.Player.TargetNext.performed += context =>
        {
            RequestTargetNext();
        };

        Input.Player.TargetPrevious.performed += context =>
        {
            RequestTargetPrevious();
        };

        #endregion


        #region Action Bar

        ActionBarSlot createBinding(string controlPath)
        {
            var action = new InputAction(binding: controlPath);
            _actionBarActions.Add(action);
            var slot = Instantiate(ActionBarSlot, ActionBar);
            slot.ControlPath = controlPath;
            slot.Binding = null;
            _actionBarSlots.Add(slot);
            action.started += context => slot.Binding?.Activate();
            action.canceled += context => slot.Binding?.Deactivate();

            var shortName = controlPath.Substring(controlPath.LastIndexOf('/') + 1);
            var sprite = Resources.Load<Sprite>($"Sprites/Input/{shortName}");
            if (sprite != null)
            {
                slot.InputIcon.sprite = sprite;
                slot.InputLabel.gameObject.SetActive(false);
            }
            else
            {
                slot.InputLabel.text = shortName;
                slot.InputIcon.gameObject.SetActive(false);
            }

            slot.PointerEnterTrigger.OnPointerEnterAsObservable().Subscribe(_ =>
            {
                //Debug.Log($"Pointer entered action bar slot {controlPath}");
                RegisterDragTarget(dragAction => RequestActionBarBinding(slot, dragAction));
            });
            slot.PointerExitTrigger.OnPointerExitAsObservable().Subscribe(_ =>
            {
                //Debug.Log($"Pointer exited action bar slot {controlPath}");
                UnregisterDragTarget();
            });
            return slot;
        }

        RuntimePlayerSettings.InputSettings.ActionBarInputs
            .OrderBy(i => i)
            .Select(createBinding)
            .ToList();

        #endregion

        #endregion

        StartGame();
    }

    public void BeginDrag(DragObject dragObject)
    {
        this.DragObject = dragObject;
    }

    public void RegisterDragTarget(Action<DragObject> onEndDrag)
    {
        _endDragCallback = onEndDrag;
    }

    public void UnregisterDragTarget()
    {
        _endDragCallback = null;
    }

    public bool HasDragTarget => _endDragCallback != null;

    public void EndDrag()
    {
        _endDragCallback?.Invoke(DragObject);
        DragObject = null;
    }

    public void EnablePlayerInput()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Input.Player.Enable();
        foreach (var a in _actionBarActions) a.Enable();
    }

    public void DisablePlayerInput()
    {
        Cursor.lockState = CursorLockMode.None;
        Input.Player.Disable();
        foreach (var a in _actionBarActions) a.Disable();
    }

    private void RequestInteract()
    {
        TryRequestDaemonInteract();
    }

    private bool TryRequestDaemonInteract()
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        try
        {
            observer.Operations.Interact(
                Settings.GameplaySettings.DockingDistance,
                Settings.GameplaySettings.WormholeExitRadius);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon interact operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    public void PopulateLevel(GalaxyZone galaxyZone, AetheriaRuntimeZoneSnapshotCommit daemonZone = null)
    {
        if (galaxyZone == null) throw new ArgumentNullException(nameof(galaxyZone));

        if (daemonZone == null)
        {
            Debug.LogWarning($"Daemon-authoritative zone population suppressed for {galaxyZone.Name}; no daemon zone snapshot was provided.");
            return;
        }

        if (galaxyZone.Contents == null)
        {
            var constructionBlueprint = CreateDaemonZoneConstructionBlueprint(daemonZone);
            galaxyZone.Contents = new Zone(ItemManager, Settings.PlanetSettings, constructionBlueprint, galaxyZone, ObservedGalaxy);
            RestoreDaemonAsteroidRuntimeState(galaxyZone.Contents, daemonZone);
        }
        Zone = galaxyZone.Contents;
        PlayMusic(MusicType.Overworld);

        Zone.Log = s => Debug.Log($"Zone: {s}");

        ZoneRenderer.LoadZone(Zone, daemonZone);

        if (CurrentEntity != null)
        {
            UnbindEntity();
            BindToEntity(CurrentEntity);
        }
    }

    public bool CanShowInputScreenFromMenu()
    {
        return CurrentEntity != null && HelpScreen != null;
    }

    public void ShowInputScreenFromMenu()
    {
        ShowFullscreenMenu(HelpScreen);
    }

    private void ToggleFullscreenMenu(GameObject menu)
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null) return;
        if (menu == null || CurrentEntity == null) return;
        if (menu.activeSelf)
        {
            HideFullscreenMenu(menu);
        }
        else
        {
            ShowFullscreenMenu(menu);
        }
    }

    private void ShowFullscreenMenu(GameObject menu)
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null) return;
        if (menu == null || CurrentEntity == null) return;

        if (MainMenu != null && MainMenu.gameObject != menu)
            MainMenu.gameObject.SetActive(false);
        if (HelpScreen != null && HelpScreen != menu)
            HelpScreen.SetActive(false);

        _paused = true;
        menu.SetActive(true);
        UiRoot.SetActive(false);
        _menuShown = Menu.gameObject.activeSelf;
        if (!_menuShown) DisablePlayerInput();
    }

    private void HideFullscreenMenu(GameObject menu)
    {
        if (menu == null || CurrentEntity == null) return;

        _paused = false;
        menu.SetActive(false);
        UiRoot.SetActive(true);
        if (!_menuShown)
        {
            EnablePlayerInput();
            UpdatePlayerPanel();
            UpdateTargetPanel(CurrentEntity.Target.Value);
        }
    }

    private void ToggleMenuTab(MenuTab tab)
    {
        if (EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null) return;
        if (MainMenu.gameObject.activeSelf) return;
        if (Menu.gameObject.activeSelf && Menu.CurrentTab == tab)
        {
            Menu.gameObject.SetActive(false);
            if (CurrentEntity != null && CurrentEntity.Parent == null)
            {
                EnablePlayerInput();
                UpdatePlayerPanel();
                UpdateTargetPanel(CurrentEntity.Target.Value);
                GameplayUI.gameObject.SetActive(true);
            }
            return;
        }

        DisablePlayerInput();
        Menu.ShowTab(tab);
        GameplayUI.gameObject.SetActive(false);
    }

    private void StartGame()
    {
        if (ObservedGalaxy == null)
        {
            return;
        }

        ApplyLatestAuthoritativeDaemonFrame();
    }

    private bool TryRestoreEntityGraphFromDaemonRun(AetheriaRuntimeRunCheckpointCommit run)
    {
        if (run == null || run.CurrentZoneIndex < 0 || ObservedGalaxy?.Zones == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(run.RunId))
        {
            Debug.LogWarning("Authoritative daemon frame does not identify a run id.");
            return false;
        }

        var runId = run.RunId;
        var zoneEntityKeyPrefix = $"global:aetheria.run_state.{runId}.zone.{run.CurrentZoneIndex}.entity.";
        var currentEntityKey = string.IsNullOrWhiteSpace(run.CurrentEntityKey) ? "" : run.CurrentEntityKey;
        if (string.IsNullOrWhiteSpace(currentEntityKey))
        {
            Debug.LogWarning($"Authoritative daemon frame for run {runId} does not identify a current entity.");
            return false;
        }
        Credits = run.Credits;

        var daemonZone = (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            .FirstOrDefault(zone => zone != null && zone.ZoneIndex == run.CurrentZoneIndex);
        if (daemonZone == null)
        {
            Debug.LogWarning($"Authoritative daemon frame has no zone snapshot for zone {run.CurrentZoneIndex}.");
            return false;
        }

        var entitySnapshots = CreateDaemonEntitySnapshots(runId, daemonZone)
            .OrderBy(entity => EntityIndexFromRecordKey(entity.RecordKey))
            .ToArray();

        if (entitySnapshots.Length == 0)
        {
            Debug.LogWarning($"Authoritative daemon frame has no entity snapshots for zone {zoneEntityKeyPrefix}.");
            return false;
        }

        var targetZone = run.CurrentZoneIndex < ObservedGalaxy.Zones.Length
            ? ObservedGalaxy.Zones[run.CurrentZoneIndex]
            : null;
        if (targetZone == null)
        {
            Debug.LogWarning($"Authoritative daemon frame references missing zone index {run.CurrentZoneIndex}.");
            return false;
        }

        if (CanApplyDaemonEntitySnapshotsInPlace(runId, run.CurrentZoneIndex, entitySnapshots, currentEntityKey))
        {
            RestoreDroppedPickupsFromDaemonZoneState(daemonZone);
            return true;
        }

        if (Zone?.GalaxyZone != targetZone)
        {
            PopulateLevel(targetZone, daemonZone);
        }

        var actionBarBindings = run.ActionBarBindings?
            .Select(ToActionBarBindingSnapshot)
            .Where(binding => binding != null)
            .ToArray() ?? Array.Empty<AetheriaRuntimeActionBarBindingSnapshot>();
        ReplaceZoneEntitiesFromTypedSnapshots(entitySnapshots, currentEntityKey, actionBarBindings);
        RestoreDroppedPickupsFromDaemonZoneState(daemonZone);
        _lastAppliedAuthoritativeDaemonRunId = runId;
        _lastAppliedAuthoritativeDaemonZoneIndex = run.CurrentZoneIndex;
        return true;
    }

    private void RestoreDroppedPickupsFromDaemonZoneState(AetheriaRuntimeZoneSnapshotCommit zone)
    {
        if (ZoneRenderer == null || zone == null)
            return;

        ClearRenderedLoot();

        foreach (var pickup in (zone.DroppedPickups ?? Array.Empty<AetheriaRuntimeDroppedPickupCommit>())
                     .Where(pickup => pickup != null)
                     .OrderBy(pickup => pickup.PickupIndex))
        {
            var item = CreateLoadoutItem(CreateRuntimeLoadoutItemSnapshot(pickup.Item));
            if (item == null)
                continue;

            ZoneRenderer.DropItem(
                new Vector3((float)pickup.PositionX, (float)pickup.PositionY, (float)pickup.PositionZ),
                new Vector3((float)pickup.VelocityX, (float)pickup.VelocityY, (float)pickup.VelocityZ),
                item);
        }
    }

    private static ZoneConstructionBlueprint CreateDaemonZoneConstructionBlueprint(AetheriaRuntimeZoneSnapshotCommit zone)
    {
        var blueprint = new ZoneConstructionBlueprint
        {
            Radius = 2000,
            Mass = 10000
        };

        foreach (var orbit in zone.Orbits ?? Array.Empty<AetheriaRuntimeOrbitSnapshotCommit>())
        {
            if (orbit == null)
                continue;

            blueprint.Orbits.Add(new OrbitConstructionData
            {
                OrbitKey = orbit.OrbitKey ?? "",
                ParentOrbitKey = orbit.ParentOrbitKey ?? "",
                Distance = (float)orbit.Distance,
                Phase = (float)orbit.Phase,
                FixedPosition = new float2((float)orbit.FixedPositionX, (float)orbit.FixedPositionY)
            });
        }

        foreach (var body in zone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
        {
            if (body == null)
                continue;

            blueprint.Bodies.Add(CreateDaemonBodyConstructionData(body));
        }

        return blueprint;
    }

    private static BodyConstructionData CreateDaemonBodyConstructionData(AetheriaRuntimeBodySnapshotCommit body)
    {
        BodyConstructionData data = (body.Kind ?? "").ToLowerInvariant() switch
        {
            "asteroid_belt" => new AsteroidBeltConstructionData
            {
                Asteroids = (body.Asteroids ?? Array.Empty<AetheriaRuntimeAsteroidCommit>())
                    .Where(asteroid => asteroid != null)
                    .Select(asteroid => new Asteroid
                    {
                        Distance = (float)asteroid.Distance,
                        Phase = (float)asteroid.Phase,
                        Size = (float)asteroid.Size,
                        RotationSpeed = (float)asteroid.RotationSpeed
                    })
                    .ToArray()
            },
            "sun" => CreateDaemonSunConstructionData(body),
            "gas_giant" => CreateDaemonGasGiantConstructionData(body),
            _ => new PlanetConstructionData()
        };

        PopulateDaemonBodyConstructionData(data, body);
        return data;
    }

    private static GasGiantConstructionData CreateDaemonGasGiantConstructionData(AetheriaRuntimeBodySnapshotCommit body)
    {
        var visual = body.GasGiantVisual ?? new AetheriaRuntimeGasGiantVisualCommit();
        return new GasGiantConstructionData
        {
            FirstOffsetDomainRotationSpeed = (float)visual.FirstOffsetDomainRotationSpeed,
            FirstOffsetRotationSpeed = (float)visual.FirstOffsetRotationSpeed,
            SecondOffsetDomainRotationSpeed = (float)visual.SecondOffsetDomainRotationSpeed,
            SecondOffsetRotationSpeed = (float)visual.SecondOffsetRotationSpeed,
            AlbedoRotationSpeed = (float)visual.AlbedoRotationSpeed,
            WaveRadiusMultiplier = (float)visual.WaveRadiusMultiplier,
            WaveDepthMultiplier = (float)visual.WaveDepthMultiplier,
            WaveDepthExponent = (float)visual.WaveDepthExponent,
            WaveSpeedMultiplier = (float)visual.WaveSpeedMultiplier,
            MaterialOverrides = (visual.MaterialOverrides ?? Array.Empty<string>()).ToList(),
            Colors = (visual.Colors ?? Array.Empty<AetheriaRuntimeColorCommit>())
                .Where(color => color != null)
                .Select(color => new float4((float)color.X, (float)color.Y, (float)color.Z, (float)color.W))
                .ToArray()
        };
    }

    private static SunConstructionData CreateDaemonSunConstructionData(AetheriaRuntimeBodySnapshotCommit body)
    {
        var gas = CreateDaemonGasGiantConstructionData(body);
        var visual = body.SunVisual ?? new AetheriaRuntimeSunVisualCommit();
        return new SunConstructionData
        {
            FirstOffsetDomainRotationSpeed = gas.FirstOffsetDomainRotationSpeed,
            FirstOffsetRotationSpeed = gas.FirstOffsetRotationSpeed,
            SecondOffsetDomainRotationSpeed = gas.SecondOffsetDomainRotationSpeed,
            SecondOffsetRotationSpeed = gas.SecondOffsetRotationSpeed,
            AlbedoRotationSpeed = gas.AlbedoRotationSpeed,
            WaveRadiusMultiplier = gas.WaveRadiusMultiplier,
            WaveDepthMultiplier = gas.WaveDepthMultiplier,
            WaveDepthExponent = gas.WaveDepthExponent,
            WaveSpeedMultiplier = gas.WaveSpeedMultiplier,
            MaterialOverrides = gas.MaterialOverrides,
            Colors = gas.Colors,
            LightColor = new CultMath.float3((float)visual.LightColorX, (float)visual.LightColorY, (float)visual.LightColorZ),
            FogTintColor = new CultMath.float3((float)visual.FogTintColorX, (float)visual.FogTintColorY, (float)visual.FogTintColorZ),
            LightRadiusMultiplier = (float)visual.LightRadiusMultiplier
        };
    }

    private static void PopulateDaemonBodyConstructionData(
        BodyConstructionData data,
        AetheriaRuntimeBodySnapshotCommit body)
    {
        data.BodyKey = body.BodyKey ?? "";
        data.Name = body.Name ?? "";
        data.OrbitKey = body.OrbitKey ?? "";
        data.Mass = (float)body.Mass;
        data.BodyRadiusMultiplier = (float)body.BodyRadiusMultiplier;
        data.GravityRadiusMultiplier = (float)body.GravityRadiusMultiplier;
        data.GravityDepthMultiplier = (float)body.GravityDepthMultiplier;
        data.GravityDepthExponent = (float)body.GravityDepthExponent;
        data.Resources = (body.Resources ?? Array.Empty<AetheriaRuntimeBodyResourceCommit>())
            .Where(resource => resource != null && !string.IsNullOrWhiteSpace(resource.ItemKey))
            .ToDictionary(resource => resource.ItemKey, resource => (float)resource.Amount, StringComparer.Ordinal);
    }

    private static void RestoreDaemonAsteroidRuntimeState(Zone zone, AetheriaRuntimeZoneSnapshotCommit daemonZone)
    {
        if (zone == null || daemonZone == null)
            return;

        foreach (var body in daemonZone.Bodies ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>())
        {
            if (body == null ||
                !string.Equals(body.Kind ?? "", "asteroid_belt", StringComparison.OrdinalIgnoreCase) ||
                !zone.AsteroidBelts.TryGetValue(body.BodyKey ?? "", out var belt))
            {
                continue;
            }

            belt.Damage.Clear();
            belt.RespawnTimers.Clear();
            var asteroids = body.Asteroids ?? Array.Empty<AetheriaRuntimeAsteroidCommit>();
            for (var index = 0; index < asteroids.Count; index++)
            {
                var asteroid = asteroids[index];
                if (asteroid == null)
                    continue;

                if (asteroid.Damage > 0)
                    belt.Damage[index] = (float)asteroid.Damage;
                if (asteroid.RespawnTimer > 0)
                    belt.RespawnTimers[index] = (float)asteroid.RespawnTimer;
            }
        }
    }

    private void ClearRenderedLoot()
    {
        if (ZoneRenderer?.ActiveLoot == null)
            return;

        foreach (var loot in ZoneRenderer.ActiveLoot.ToArray())
        {
            if (loot == null)
                continue;

            ZoneRenderer.DestroyLoot(loot);
            Destroy(loot.gameObject);
        }
    }

    private static AetheriaRuntimeEntitySnapshot[] CreateDaemonEntitySnapshots(
        string runId,
        AetheriaRuntimeZoneSnapshotCommit zone)
    {
        if (zone == null)
            return Array.Empty<AetheriaRuntimeEntitySnapshot>();

        return (zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            .Where(entity => entity != null)
            .Select(entity => CreateDaemonEntitySnapshot(runId, zone.ZoneIndex, entity))
            .ToArray();
    }

    private static AetheriaRuntimeEntitySnapshot CreateDaemonEntitySnapshot(
        string runId,
        int zoneIndex,
        AetheriaRuntimeEntitySnapshotCommit entity)
    {
        return new AetheriaRuntimeEntitySnapshot(
            DaemonEntityRecordKey(runId, zoneIndex, entity.EntityIndex),
            entity.Name ?? "",
            entity.Kind ?? "",
            entity.PositionX,
            entity.PositionY,
            entity.PositionZ,
            entity.DirectionX,
            entity.DirectionY,
            entity.FactionKey ?? "",
            entity.HullItemKey ?? "",
            CreateDaemonItemSlots(entity.Equipment),
            CreateDaemonItemSlots(entity.CargoBays),
            CreateDaemonItemSlots(entity.DockingBays),
            (entity.ChildEntityIndices ?? Array.Empty<int>())
                .Where(index => index >= 0)
                .Select(index => DaemonEntityRecordKey(runId, zoneIndex, index))
                .ToArray(),
            (entity.WeaponGroups ?? Array.Empty<IReadOnlyList<int>>())
                .Select(group => (IReadOnlyList<int>)(group ?? Array.Empty<int>()).ToArray())
                .ToArray(),
            (entity.StatGrids ?? Array.Empty<AetheriaRuntimeEntityStatGridCommit>())
                .Select(grid => new AetheriaRuntimeEntityStatGridSnapshot(
                    grid.Name ?? "",
                    grid.Width,
                    grid.Height,
                    (grid.Values ?? Array.Empty<double>()).ToArray()))
                .ToArray(),
            entity.VelocityX,
            entity.VelocityY,
            entity.TargetEntityIndex < 0 ? "" : DaemonEntityRecordKey(runId, zoneIndex, entity.TargetEntityIndex),
            entity.IsActive,
            entity.HeatsinksEnabled,
            entity.OverrideShutdown,
            entity.TractorPower,
            entity.Heatstroke,
            entity.Hypothermia,
            (entity.ActiveConsumables ?? Array.Empty<AetheriaRuntimeActiveConsumableCommit>())
                .Select(consumable => new AetheriaRuntimeActiveConsumableSnapshot(
                    consumable.ItemKey ?? "",
                    consumable.Quality,
                    consumable.RemainingDuration,
                    consumable.Duration))
                .ToArray(),
            (entity.BehaviorProgress ?? Array.Empty<AetheriaRuntimeBehaviorProgressCommit>())
                .Select(progress => new AetheriaRuntimeBehaviorProgressSnapshot(
                    progress.OwnerKind ?? "",
                    progress.OwnerIndex,
                    progress.BehaviorIndex,
                    progress.BehaviorKind ?? "",
                    progress.Progress))
                .ToArray(),
            CreateDaemonWeaponStates(runId, zoneIndex, entity.WeaponStates),
            CreateDaemonBehaviorStates(entity.BehaviorStates),
            CreateDaemonCargoBays(entity.CargoContents),
            CreateDaemonCargoBays(entity.DockingBayContents),
            (entity.DockingBayAssignments ?? Array.Empty<int>()).ToArray(),
            entity.Visibility,
            entity.VisibilitySourceCount,
            (entity.Contacts ?? Array.Empty<AetheriaRuntimeEntityContactCommit>())
                .Where(contact => contact != null && contact.TargetEntityIndex >= 0)
                .Select(contact => new AetheriaRuntimeEntityContactSnapshot(
                    DaemonEntityRecordKey(runId, zoneIndex, contact.TargetEntityIndex),
                    contact.InfoGathered,
                    contact.Hostile,
                    contact.Visible))
                .ToArray(),
            entity.ShutdownPerformance);
    }

    private static AetheriaRuntimeEntityItemSlotSnapshot[] CreateDaemonItemSlots(
        IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> slots)
    {
        return (slots ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
            .Where(slot => slot?.Item != null)
            .Select(slot => new AetheriaRuntimeEntityItemSlotSnapshot(
                slot.X,
                slot.Y,
                slot.Item.ItemKey ?? "",
                slot.Item.Quality,
                slot.Item.Durability,
                slot.Item.Quantity,
                slot.Item.Enabled,
                slot.Item.OverrideShutdown))
            .ToArray();
    }

    private static AetheriaRuntimeCargoBayLoadoutSnapshot[] CreateDaemonCargoBays(
        IReadOnlyList<AetheriaRuntimeCargoBayLoadoutCommit> cargoBays)
    {
        return (cargoBays ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>())
            .Select(bay => new AetheriaRuntimeCargoBayLoadoutSnapshot(
                CreateDaemonLoadoutSlots(bay?.Items)))
            .ToArray();
    }

    private static AetheriaRuntimeLoadoutItemSlotSnapshot[] CreateDaemonLoadoutSlots(
        IReadOnlyList<AetheriaRuntimeLoadoutItemSlotCommit> slots)
    {
        return (slots ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>())
            .Where(slot => slot?.Item != null)
            .Select(slot => new AetheriaRuntimeLoadoutItemSlotSnapshot(
                slot.X,
                slot.Y,
                new AetheriaRuntimeLoadoutItemSnapshot(
                    slot.Item.ItemKey ?? "",
                    slot.Item.Quality,
                    slot.Item.Durability,
                    slot.Item.Quantity,
                    slot.Item.Enabled,
                    slot.Item.OverrideShutdown)))
            .ToArray();
    }

    private static AetheriaRuntimeWeaponStateSnapshot[] CreateDaemonWeaponStates(
        string runId,
        int zoneIndex,
        IReadOnlyList<AetheriaRuntimeWeaponStateCommit> weaponStates)
    {
        return (weaponStates ?? Array.Empty<AetheriaRuntimeWeaponStateCommit>())
            .Where(state => state != null)
            .Select(state => new AetheriaRuntimeWeaponStateSnapshot(
                state.OwnerKind ?? "",
                state.OwnerIndex,
                state.BehaviorIndex,
                state.BehaviorKind ?? "",
                state.Firing,
                state.Ammo,
                state.BurstRemaining,
                state.BurstTimer,
                state.BurstInterval,
                state.CooldownProgress,
                state.CoolingDown,
                state.Charging,
                state.Charged,
                state.Charge,
                state.Reloading,
                state.ReloadProgress,
                state.AmmoIntervalProgress,
                state.LockProgress,
                state.LockTargetEntityIndex < 0 ? "" : DaemonEntityRecordKey(runId, zoneIndex, state.LockTargetEntityIndex)))
            .ToArray();
    }

    private static AetheriaRuntimeBehaviorStateSnapshot[] CreateDaemonBehaviorStates(
        IReadOnlyList<AetheriaRuntimeBehaviorStateCommit> behaviorStates)
    {
        return (behaviorStates ?? Array.Empty<AetheriaRuntimeBehaviorStateCommit>())
            .Where(state => state != null)
            .Select(state => new AetheriaRuntimeBehaviorStateSnapshot(
                state.OwnerKind ?? "",
                state.OwnerIndex,
                state.BehaviorIndex,
                state.BehaviorKind ?? "",
                state.Pinging,
                state.PingCooldown,
                state.PingLerp,
                state.PingRadius,
                state.PingedEntityCount,
                state.RadiatorTemperature,
                state.Emissivity,
                state.PumpedHeat,
                state.WasteHeat,
                state.EnergyUsage,
                state.ReactorDraw,
                state.ReactorLoadRatio,
                state.CapacitorCharge,
                state.CapacitorCapacity,
                state.CapacitorEfficiency,
                state.AetherDriveAxisX,
                state.AetherDriveAxisY,
                state.AetherDriveAxisZ,
                state.AetherDriveThrustX,
                state.AetherDriveThrustY,
                state.AetherDriveThrustZ,
                state.AetherDriveRpmX,
                state.AetherDriveRpmY,
                state.AetherDriveRpmZ,
                state.AetherDriveMaximumRpm,
                state.AetherDriveThrustDirectionX,
                state.AetherDriveThrustDirectionY,
                state.ResourceScannerTargetBodyKey ?? "",
                state.ResourceScannerAsteroidIndex,
                state.ResourceScannerScanTime,
                state.ResourceScannerRange,
                state.ResourceScannerMinimumDensity,
                state.ResourceScannerScanDuration,
                state.MiningToolAsteroidBeltKey ?? "",
                state.MiningToolAsteroidIndex,
                state.MiningToolRange,
                state.ThrusterAxis,
                state.ThrusterThrust,
                state.ThrusterTorque,
                state.ShieldEfficiency,
                state.ShieldEnergyUsage,
                state.VelocityLimit,
                state.ThermotoggleTargetTemperature,
                state.SwitchActivated,
                state.TriggerPulled,
                state.StatModifierApplied,
                state.StatModifierExecuted,
                state.StatModifierTargetStatCount,
                state.TurretControllerWeaponCount,
                state.TurretControllerShotSpeed,
                state.TurretControllerPredictShots))
            .ToArray();
    }

    private static string DaemonEntityRecordKey(string runId, int zoneIndex, int entityIndex)
    {
        return string.IsNullOrWhiteSpace(runId)
            ? ""
            : $"global:aetheria.run_state.{runId}.zone.{zoneIndex}.entity.{entityIndex}.v1";
    }

    private bool CanApplyDaemonEntitySnapshotsInPlace(
        string runId,
        int zoneIndex,
        IReadOnlyList<AetheriaRuntimeEntitySnapshot> entitySnapshots,
        string currentEntityKey)
    {
        if (!string.Equals(_lastAppliedAuthoritativeDaemonRunId, runId, StringComparison.Ordinal) ||
            _lastAppliedAuthoritativeDaemonZoneIndex != zoneIndex ||
            _authoritativeDaemonEntities.Count != entitySnapshots.Count)
        {
            return false;
        }

        foreach (var snapshot in entitySnapshots)
        {
            if (!_authoritativeDaemonEntities.ContainsKey(snapshot.RecordKey))
            {
                return false;
            }
        }

        ApplyDaemonEntitySnapshotsInPlace(entitySnapshots, currentEntityKey);
        return true;
    }

    private void ApplyDaemonEntitySnapshotsInPlace(
        IReadOnlyList<AetheriaRuntimeEntitySnapshot> entitySnapshots,
        string currentEntityKey)
    {
        foreach (var entitySnapshot in entitySnapshots)
        {
            if (!_authoritativeDaemonEntities.TryGetValue(entitySnapshot.RecordKey, out var entity))
                continue;

            entity.DaemonEntityIndex = EntityIndexFromRecordKey(entitySnapshot.RecordKey);
            entity.Name = entitySnapshot.Name ?? "";
            entity.CultPosition = new CultMath.float3((float)entitySnapshot.PositionX, (float)entitySnapshot.PositionY, (float)entitySnapshot.PositionZ);
            entity.CultDirection = new CultMath.float2((float)entitySnapshot.DirectionX, (float)entitySnapshot.DirectionY);
            entity.CultVelocity = new CultMath.float2((float)entitySnapshot.VelocityX, (float)entitySnapshot.VelocityY);
            entity.OverrideShutdown = entitySnapshot.OverrideShutdown;
            entity.TractorPower = (float)entitySnapshot.TractorPower;
            entity.HeatsinksEnabled = entitySnapshot.HeatsinksEnabled;
            if (entity.Settings != null)
                entity.Settings.ShutdownPerformance = (float)entitySnapshot.ShutdownPerformance;
            entity.RestoreThermalExposure((float)entitySnapshot.Heatstroke, (float)entitySnapshot.Hypothermia);
            RestoreRuntimeBehaviorStateFromTypedSnapshot(entity, entitySnapshot, _authoritativeDaemonEntities);
        }

        foreach (var entity in _authoritativeDaemonEntities.Values)
        {
            entity.EntityInfoGathered.Clear();
            entity.EntityHostility.Clear();
            entity.VisibleEntities.Clear();
            entity.VisibleEnemies.Clear();
            entity.VisibleFriendlies.Clear();
            entity.Target.Value = null;
        }

        foreach (var entitySnapshot in entitySnapshots)
        {
            if (!_authoritativeDaemonEntities.TryGetValue(entitySnapshot.RecordKey, out var entity))
                continue;

            RestoreEntityContactsFromTypedSnapshot(entity, entitySnapshot, _authoritativeDaemonEntities);
            if (_authoritativeDaemonEntities.TryGetValue(entitySnapshot.TargetEntityKey, out var target))
                entity.Target.Value = target;
        }

        if (_authoritativeDaemonEntities.TryGetValue(currentEntityKey, out var currentEntity) &&
            CurrentEntity != currentEntity)
        {
            RestoreCurrentEntityBinding(currentEntity, Array.Empty<AetheriaRuntimeActionBarBindingSnapshot>());
        }
    }

    private void ReplaceZoneEntitiesFromTypedSnapshots(
        IReadOnlyList<AetheriaRuntimeEntitySnapshot> entitySnapshots,
        string currentEntityKey,
        IReadOnlyList<AetheriaRuntimeActionBarBindingSnapshot> actionBarBindings)
    {
        foreach (var existingEntity in Zone.Entities.ToArray())
        {
            Zone.Entities.Remove(existingEntity);
        }
        Zone.Agents.Clear();

        var restoredEntities = new Dictionary<string, Entity>();
        foreach (var entitySnapshot in entitySnapshots)
        {
            var blueprint = CreateEntityConstructionBlueprint(
                entitySnapshot,
                string.Equals(entitySnapshot.RecordKey, currentEntityKey, StringComparison.Ordinal));
            if (blueprint == null)
            {
                Debug.LogWarning($"Typed entity snapshot {entitySnapshot.RecordKey} could not be lowered into a runtime entity.");
                continue;
            }

            var entity = EntityConstructionBlueprintProjector.ProjectObservedFromBlueprint(ItemManager, Zone, blueprint);
            if (entity == null)
                continue;

            entity.CultPosition = new CultMath.float3((float)entitySnapshot.PositionX, (float)entitySnapshot.PositionY, (float)entitySnapshot.PositionZ);
            entity.CultDirection = new CultMath.float2((float)entitySnapshot.DirectionX, (float)entitySnapshot.DirectionY);
            entity.CultVelocity = new CultMath.float2((float)entitySnapshot.VelocityX, (float)entitySnapshot.VelocityY);
            entity.OverrideShutdown = entitySnapshot.OverrideShutdown;
            entity.TractorPower = (float)entitySnapshot.TractorPower;
            entity.RestoreActiveState(entitySnapshot.IsActive);
            entity.DaemonEntityIndex = EntityIndexFromRecordKey(entitySnapshot.RecordKey);
            entity.Zone = Zone;
            Zone.Entities.Add(entity);
            restoredEntities[entitySnapshot.RecordKey] = entity;
        }

        RestoreChildAndDockingRelationships(entitySnapshots, restoredEntities);

        foreach (var entitySnapshot in entitySnapshots)
        {
            if (!restoredEntities.TryGetValue(entitySnapshot.RecordKey, out var entity))
                continue;

            entity.HeatsinksEnabled = entitySnapshot.HeatsinksEnabled;
            entity.RestoreStatGrids(entitySnapshot.StatGrids);
            entity.RestoreThermalExposure((float)entitySnapshot.Heatstroke, (float)entitySnapshot.Hypothermia);
            RestoreActiveConsumablesFromTypedEntitySnapshot(entity, entitySnapshot);
            RestoreRuntimeBehaviorStateFromTypedSnapshot(entity, entitySnapshot, restoredEntities);
            RestoreEntityContactsFromTypedSnapshot(entity, entitySnapshot, restoredEntities);
            if (restoredEntities.TryGetValue(entitySnapshot.TargetEntityKey, out var target))
                entity.Target.Value = target;
        }

        _authoritativeDaemonEntities.Clear();
        foreach (var restoredEntity in restoredEntities)
            _authoritativeDaemonEntities[restoredEntity.Key] = restoredEntity.Value;

        if (restoredEntities.TryGetValue(currentEntityKey, out var currentEntity))
            RestoreCurrentEntityBinding(currentEntity, actionBarBindings);
    }

    private void RestoreChildAndDockingRelationships(
        IReadOnlyList<AetheriaRuntimeEntitySnapshot> entitySnapshots,
        IReadOnlyDictionary<string, Entity> restoredEntities)
    {
        foreach (var entitySnapshot in entitySnapshots)
        {
            if (!restoredEntities.TryGetValue(entitySnapshot.RecordKey, out var parent))
                continue;

            foreach (var childKey in entitySnapshot.ChildEntityKeys)
            {
                if (!restoredEntities.TryGetValue(childKey, out var child) ||
                    child == parent ||
                    child.Parent == parent)
                {
                    continue;
                }

                child.RemoveParent();
                child.SetParent(parent);
            }

            for (var bayIndex = 0; bayIndex < entitySnapshot.DockingBayAssignments.Count; bayIndex++)
            {
                var childIndex = entitySnapshot.DockingBayAssignments[bayIndex];
                if (childIndex < 0 ||
                    childIndex >= entitySnapshot.ChildEntityKeys.Count ||
                    bayIndex >= parent.DockingBays.Count ||
                    !restoredEntities.TryGetValue(entitySnapshot.ChildEntityKeys[childIndex], out var child) ||
                    !(child is Ship ship))
                {
                    continue;
                }

                var dockingBay = parent.DockingBays[bayIndex];
                dockingBay.DockedShip = ship;
                if (ship.Parent != parent)
                {
                    ship.RemoveParent();
                    ship.SetParent(parent);
                }
                if (Zone.Entities.Contains(ship))
                    Zone.Entities.Remove(ship);
                ship.RestoreActiveState(false);
            }
        }
    }

    private void RestoreCurrentEntityBinding(
        Entity currentEntity,
        IReadOnlyList<AetheriaRuntimeActionBarBindingSnapshot> actionBarBindings)
    {
        if (currentEntity.Parent != null)
        {
            var dockingBay = currentEntity.Parent.DockingBays.FirstOrDefault(bay => bay.DockedShip == currentEntity);
            if (dockingBay != null && currentEntity.Parent is OrbitalEntity)
            {
                CurrentEntity = currentEntity;
                RestoreActionBarBindingsFromTypedRun(actionBarBindings);
                DoDock(currentEntity.Parent, dockingBay);
                return;
            }
        }

        BindToEntity(
            currentEntity,
            actionBarBindings?
                .Select(ToActionBarBindingCommit)
                .Where(binding => binding != null)
                .ToArray());
    }

    private void RestoreEntityContactsFromTypedSnapshot(
        Entity entity,
        AetheriaRuntimeEntitySnapshot snapshot,
        IReadOnlyDictionary<string, Entity> restoredEntities)
    {
        foreach (var contact in snapshot.Contacts)
        {
            if (!restoredEntities.TryGetValue(contact.TargetEntityKey, out var target))
                continue;

            entity.EntityInfoGathered[target] = (float)contact.InfoGathered;
            entity.EntityHostility[target] = contact.Hostile;
            if (contact.Visible && !entity.VisibleEntities.Contains(target))
                entity.VisibleEntities.Add(target);
            if (contact.Visible && contact.Hostile && !entity.VisibleEnemies.Contains(target))
                entity.VisibleEnemies.Add(target);
            if (contact.Visible && !contact.Hostile && !entity.VisibleFriendlies.Contains(target))
                entity.VisibleFriendlies.Add(target);
        }
    }

    private static int EntityIndexFromRecordKey(string recordKey)
    {
        var match = Regex.Match(recordKey ?? "", @"\.entity\.(\d+)\.v1$");
        return match.Success && int.TryParse(match.Groups[1].Value, out var index)
            ? index
            : int.MaxValue;
    }

    private void RestoreActiveConsumablesFromTypedEntitySnapshot(Entity entity, AetheriaRuntimeEntitySnapshot snapshot)
    {
        foreach (var activeConsumable in snapshot.ActiveConsumables)
        {
            var item = CreateLoadoutItem(new AetheriaRuntimeLoadoutItemSnapshot(
                activeConsumable.ItemKey,
                activeConsumable.Quality,
                1,
                1,
                true,
                false)) as ConsumableItem;
            if (item == null)
            {
                Debug.LogWarning($"Typed active consumable {activeConsumable.ItemKey} could not be lowered for restored entity {snapshot.RecordKey}.");
                continue;
            }

            entity.RestoreActiveConsumable(
                item,
                (float)activeConsumable.RemainingDuration,
                (float)activeConsumable.Duration);
        }
    }

    private void RestoreRuntimeBehaviorStateFromTypedSnapshot(
        Entity entity,
        AetheriaRuntimeEntitySnapshot snapshot,
        IReadOnlyDictionary<string, Entity> restoredEntities)
    {
        foreach (var weaponState in snapshot.WeaponStates)
        {
            if (!(ResolveRuntimeBehavior(entity, weaponState.OwnerKind, weaponState.OwnerIndex, weaponState.BehaviorIndex) is Weapon weapon))
                continue;

            if (weapon is LockWeapon lockWeapon)
            {
                restoredEntities.TryGetValue(weaponState.LockTargetEntityKey, out var lockTarget);
                lockWeapon.RestoreRuntimeState(
                    weaponState.Firing,
                    weaponState.Ammo,
                    weaponState.BurstRemaining,
                    (float)weaponState.BurstTimer,
                    (float)weaponState.BurstInterval,
                    (float)weaponState.CooldownProgress,
                    weaponState.CoolingDown,
                    (float)weaponState.LockProgress,
                    lockTarget);
            }
            else if (weapon is ChargedWeapon chargedWeapon)
            {
                chargedWeapon.RestoreRuntimeState(
                    weaponState.Firing,
                    weaponState.Ammo,
                    weaponState.BurstRemaining,
                    (float)weaponState.BurstTimer,
                    (float)weaponState.BurstInterval,
                    (float)weaponState.CooldownProgress,
                    weaponState.CoolingDown,
                    weaponState.Charging,
                    weaponState.Charged,
                    (float)weaponState.Charge);
            }
            else if (weapon is ConstantWeapon constantWeapon)
            {
                constantWeapon.RestoreRuntimeState(
                    weaponState.Firing,
                    weaponState.Ammo,
                    (float)weaponState.AmmoIntervalProgress,
                    (float)weaponState.ReloadProgress,
                    weaponState.Reloading);
            }
            else if (weapon is InstantWeapon instantWeapon)
            {
                instantWeapon.RestoreRuntimeState(
                    weaponState.Firing,
                    weaponState.Ammo,
                    weaponState.BurstRemaining,
                    (float)weaponState.BurstTimer,
                    (float)weaponState.BurstInterval,
                    (float)weaponState.CooldownProgress,
                    weaponState.CoolingDown);
            }
            else
            {
                weapon.RestoreRuntimeState(weaponState.Firing);
            }
        }

        foreach (var behaviorState in snapshot.BehaviorStates)
        {
            var behavior = ResolveRuntimeBehavior(entity, behaviorState.OwnerKind, behaviorState.OwnerIndex, behaviorState.BehaviorIndex);
            switch (behavior)
            {
                case Sensor sensor:
                    sensor.RestoreRuntimeState(
                        behaviorState.Pinging,
                        (float)behaviorState.PingCooldown,
                        (float)behaviorState.PingLerp,
                        (float)behaviorState.PingRadius);
                    break;
                case Radiator radiator:
                    radiator.RestoreRuntimeState(
                        (float)behaviorState.RadiatorTemperature,
                        (float)behaviorState.Emissivity,
                        (float)behaviorState.PumpedHeat,
                        (float)behaviorState.WasteHeat,
                        (float)behaviorState.EnergyUsage);
                    break;
                case Reactor reactor:
                    reactor.RestoreRuntimeState(
                        (float)behaviorState.ReactorDraw,
                        (float)behaviorState.ReactorLoadRatio);
                    break;
                case Capacitor capacitor:
                    capacitor.RestoreRuntimeState(
                        (float)behaviorState.CapacitorCharge,
                        (float)behaviorState.CapacitorCapacity,
                        (float)behaviorState.CapacitorEfficiency);
                    break;
                case AetherDrive drive:
                    drive.RestoreRuntimeState(
                        new CultMath.float3((float)behaviorState.AetherDriveAxisX, (float)behaviorState.AetherDriveAxisY, (float)behaviorState.AetherDriveAxisZ),
                        new CultMath.float3((float)behaviorState.AetherDriveThrustX, (float)behaviorState.AetherDriveThrustY, (float)behaviorState.AetherDriveThrustZ),
                        new CultMath.float3((float)behaviorState.AetherDriveRpmX, (float)behaviorState.AetherDriveRpmY, (float)behaviorState.AetherDriveRpmZ),
                        (float)behaviorState.AetherDriveMaximumRpm,
                        new CultMath.float2((float)behaviorState.AetherDriveThrustDirectionX, (float)behaviorState.AetherDriveThrustDirectionY));
                    break;
                case ResourceScanner resourceScanner:
                    resourceScanner.RestoreRuntimeState(
                        behaviorState.ResourceScannerTargetBodyKey,
                        behaviorState.ResourceScannerAsteroidIndex,
                        (float)behaviorState.ResourceScannerScanTime,
                        (float)behaviorState.ResourceScannerRange,
                        (float)behaviorState.ResourceScannerMinimumDensity,
                        (float)behaviorState.ResourceScannerScanDuration);
                    break;
                case MiningTool miningTool:
                    miningTool.RestoreRuntimeState(
                        behaviorState.MiningToolAsteroidBeltKey,
                        behaviorState.MiningToolAsteroidIndex,
                        (float)behaviorState.MiningToolRange);
                    break;
                case Thruster thruster:
                    thruster.RestoreRuntimeState(
                        (float)behaviorState.ThrusterAxis,
                        (float)behaviorState.ThrusterThrust);
                    break;
                case Shield shield:
                    shield.RestoreRuntimeState(
                        (float)behaviorState.ShieldEfficiency,
                        (float)behaviorState.ShieldEnergyUsage);
                    break;
                case VelocityLimit velocityLimit:
                    velocityLimit.RestoreRuntimeState((float)behaviorState.VelocityLimit);
                    break;
                case Thermotoggle thermotoggle:
                    thermotoggle.TargetTemperature = (float)behaviorState.ThermotoggleTargetTemperature;
                    break;
                case Switch switchBehavior:
                    switchBehavior.Activated = behaviorState.SwitchActivated;
                    break;
                case Trigger trigger:
                    trigger.RestoreRuntimeState(behaviorState.TriggerPulled);
                    break;
                case StatModifier statModifier:
                    statModifier.RestoreRuntimeState(
                        behaviorState.StatModifierApplied,
                        behaviorState.StatModifierExecuted);
                    break;
                case TurretController turretController:
                    turretController.RestoreRuntimeState(
                        (float)behaviorState.TurretControllerShotSpeed,
                        behaviorState.TurretControllerPredictShots);
                    break;
            }
        }
    }

    private static Behavior ResolveRuntimeBehavior(Entity entity, string ownerKind, int ownerIndex, int behaviorIndex)
    {
        var behaviors = ResolveRuntimeBehaviorList(entity, ownerKind, ownerIndex);
        return behaviors != null && behaviorIndex >= 0 && behaviorIndex < behaviors.Count
            ? behaviors[behaviorIndex]
            : null;
    }

    private static IReadOnlyList<Behavior> ResolveRuntimeBehaviorList(Entity entity, string ownerKind, int ownerIndex)
    {
        if (entity == null || ownerIndex < 0)
            return null;

        switch (ownerKind)
        {
            case "equipment":
                return ownerIndex < entity.Equipment.Count ? entity.Equipment[ownerIndex].Behaviors : null;
            case "active_consumable":
                return ownerIndex < entity.ActiveConsumables.Count ? entity.ActiveConsumables[ownerIndex].Behaviors : null;
            default:
                return null;
        }
    }

    private void DoDock(Entity entity, EquippedDockingBay dockingBay)
    {
        TradeMenu.Inventory = entity.CargoBays.First();
        DockedEntity = entity;
        ZoneRenderer.PerspectiveEntity = DockedEntity;
        DockingBay = dockingBay;
        DockCamera.enabled = true;
        FollowCamera.enabled = false;
        var orbital = (OrbitalEntity) entity;
        DockCamera.Follow = ZoneRenderer.EntityInstances[orbital].transform;
        var parentOrbitKey = Zone.TryGetOrbit(orbital.OrbitKey, out var orbit) ? orbit.ParentOrbitKey : "";
        var parentOrbitPlanetBodyKey = Zone.PlanetInstances.Values.FirstOrDefault(planet => planet.OrbitKey == parentOrbitKey)?.BodyKey ?? "";
        if (ZoneRenderer.Planets.ContainsKey(parentOrbitPlanetBodyKey))
            DockCamera.LookAt = ZoneRenderer.Planets[parentOrbitPlanetBodyKey].Body.transform;
        else DockCamera.LookAt = ZoneRenderer.ZoneRoot;
        if (entity is OrbitalEntity {CanTow: true})
            TowingStation = entity;
        Menu.ShowTab(MenuTab.Inventory);
    }

    public void RequestTowToStation()
    {
        TryRequestDaemonTowToStation();
    }

    private bool TryRequestDaemonTowToStation()
    {
        var observer = ResolveDaemonObserver();
        if (observer == null ||
            !observer.HasAuthoritativeState ||
            TowingStation == null)
        {
            return false;
        }

        var stationEntityKey = ResolveEntityRecordKey(TowingStation);
        var targetZoneIndex = ZoneIndex(TowingStation.Zone?.GalaxyZone);
        if (string.IsNullOrWhiteSpace(stationEntityKey) || targetZoneIndex < 0)
        {
            return false;
        }

        try
        {
            observer.Operations.TowToStation(
                stationEntityKey,
                targetZoneIndex,
                TowingStation.CultPositionXZ.x,
                TowingStation.CultPositionXZ.y);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon towing operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private void UnbindEntity()
    {
        foreach (var indicator in _visibleHostileIndicators) Destroy(indicator.Value.gameObject);
        _visibleHostileIndicators.Clear();

        foreach (var indicator in _visibleFriendlyIndicators) Destroy(indicator.Value.gameObject);
        _visibleFriendlyIndicators.Clear();

        if(_lockingIndicators!=null) foreach(var (_, indicator, _) in _lockingIndicators)
            indicator.GetComponent<Prototype>().ReturnToPool();
        DisablePlayerInput();
        Cursor.lockState = CursorLockMode.None;
        GameplayUI.gameObject.SetActive(false);

        foreach(var subscription in _shipSubscriptions) subscription.Dispose();
        _shipSubscriptions.Clear();
    }

    private void BindToEntity(
        Entity entity,
        IReadOnlyList<AetheriaRuntimeActionBarBindingCommit> actionBarBindings = null)
    {
        if (!ZoneRenderer.EntityInstances.ContainsKey(entity))
        {
            Debug.LogError($"Attempted to bind to entity {entity.Name}, but SectorRenderer has no such instance!");
            return;
        }

        var resolvedActionBarBindings = actionBarBindings ?? Array.Empty<AetheriaRuntimeActionBarBindingCommit>();
        CurrentEntity = entity;
        DeathPost.weight = 0;
        ZoneRenderer.PerspectiveEntity = CurrentEntity;

        Menu.gameObject.SetActive(false);
        DockedEntity = null;
        DockingBay = null;
        DockCamera.enabled = false;
        FollowCamera.enabled = true;

        if (CultMath.math.length(CurrentEntity.CultDirection) > .1f)
            _viewDirection = (float3)AetheriaMath.ToUnityXZ(CurrentEntity.CultDirection);

        Cursor.lockState = CursorLockMode.Locked;
        EnablePlayerInput();
        GameplayUI.gameObject.SetActive(true);
        ShipPanel.Display(CurrentEntity, true);
        SchematicDisplay.ShowShip(CurrentEntity);

        FollowCamera.LookAt = ZoneRenderer.EntityInstances[CurrentEntity].LookAtPoint;
        FollowCamera.Follow = ZoneRenderer.EntityInstances[CurrentEntity].transform;
        _articulationGroups = CurrentEntity.Equipment
            .Where(HasArticulatedWeaponBehavior)
            .GroupBy(item => ZoneRenderer.EntityInstances[CurrentEntity]
                .GetBarrel(CurrentEntity.Hardpoints[item.Position.x, item.Position.y])
                .GetComponentInParent<ArticulationPoint>()?.Group ?? -1)
            .Select((group, index) => {
                return (
                    group.Select(item => CurrentEntity.Hardpoints[item.Position.x, item.Position.y]).ToArray(),
                    group.Select(item => ZoneRenderer.EntityInstances[CurrentEntity].GetBarrel(CurrentEntity.Hardpoints[item.Position.x, item.Position.y])).ToArray(),
                    Crosshairs[index]
                );
            }).ToArray();

        foreach (var crosshair in Crosshairs)
            crosshair.gameObject.SetActive(false);
        foreach (var group in _articulationGroups)
            group.crosshair.gameObject.SetActive(true);

        _shipSubscriptions.Add(CurrentEntity.TargetedByCount.Subscribe(count =>
        {
            PlayMusic(count > 0 ? MusicType.Combat : MusicType.Overworld);
        }));

        _shipSubscriptions.Add(CurrentEntity.Target.Subscribe(target =>
        {
            // Clear previous subscriptions related to currently targeted enemy
            foreach(var subscription in _targetSubscriptions)
                subscription.Dispose();
            _targetSubscriptions.Clear();

            UpdateTargetPanel(target);
            if (target != null)
            {
                if (target.Shield != null)
                {
                    TargetShieldsBackground.color = new Color(ShieldColor.r, ShieldColor.g, ShieldColor.b, .4f);
                    TargetShieldsIcon.color = ShieldColor;
                    TargetShieldsIcon.sprite = ShieldIcon;
                }
                else
                {
                    TargetShieldsBackground.color = new Color(NoShieldColor.r, NoShieldColor.g, NoShieldColor.b, .4f);
                    TargetShieldsIcon.color = NoShieldColor;
                    TargetShieldsIcon.sprite = NoShieldIcon;
                }

                // Subscribe to incoming hits from the player ship to display the hit marker
                _targetSubscriptions.Add(target.IncomingHit.Where(e => e == CurrentEntity).Subscribe(_ =>
                {
                    HitMarker.SetActive(true);
                    _hitMarkerTime = HitMarkerDuration;
                }));
            }
        }));

        foreach (var hostile in CurrentEntity.VisibleEnemies)
        {
            var indicator = HostileTargetIndicator.Instantiate<VisibleTargetIndicator>();
            _visibleHostileIndicators.Add(hostile, indicator);
        }

        _shipSubscriptions.Add(CurrentEntity.VisibleEnemies.ObserveAdd().Subscribe(addEvent =>
        {
            var indicator = HostileTargetIndicator.Instantiate<VisibleTargetIndicator>();
            _visibleHostileIndicators.Add(addEvent.Value, indicator);
        }));

        _shipSubscriptions.Add(CurrentEntity.VisibleEnemies.ObserveRemove().Subscribe(removeEvent =>
        {
            _visibleHostileIndicators[removeEvent.Value].GetComponent<Prototype>().ReturnToPool();
            _visibleHostileIndicators.Remove(removeEvent.Value);
        }));

        foreach (var friendly in CurrentEntity.VisibleFriendlies)
        {
            var indicator = FriendlyTargetIndicator.Instantiate<VisibleTargetIndicator>();
            _visibleFriendlyIndicators.Add(friendly, indicator);
        }

        _shipSubscriptions.Add(CurrentEntity.VisibleFriendlies.ObserveAdd().Subscribe(addEvent =>
        {
            var indicator = FriendlyTargetIndicator.Instantiate<VisibleTargetIndicator>();
            _visibleFriendlyIndicators.Add(addEvent.Value, indicator);
        }));

        _shipSubscriptions.Add(CurrentEntity.VisibleFriendlies.ObserveRemove().Subscribe(removeEvent =>
        {
            _visibleFriendlyIndicators[removeEvent.Value].GetComponent<Prototype>().ReturnToPool();
            _visibleFriendlyIndicators.Remove(removeEvent.Value);
        }));

        _lockingIndicators = CurrentEntity.GetBehaviors<LockWeapon>()
            .Select(x =>
            {
                var i = LockIndicator.Instantiate<PlaceUIElementWorldspace>();
                return (x, i, i.GetComponent<Rotate>());
            }).ToArray();

        ApplyActionBarBindings(resolvedActionBarBindings);
    }

    private bool HasArticulatedWeaponBehavior(EquippedItem item)
    {
        var typedItem = ItemManager.GetRuntimeItem(item.EquippableItem);
        return typedItem?.BehaviorKinds.Any(ArticulatedWeaponBehaviorKinds.Contains) == true;
    }

    private void UpdatePlayerPanel()
    {
        ShipPanel.Display(CurrentEntity, true);
        SchematicDisplay.ShowShip(CurrentEntity);
    }

    private void UpdateTargetPanel(Entity target)
    {
        TargetIndicator.gameObject.SetActive(target != null);
        TargetShipPanel.gameObject.SetActive(target != null);
        if (target != null)
        {
            TargetShipPanel.Display(target, true);
            TargetSchematicDisplay.ShowShip(target, CurrentEntity);
        }
    }

    // public void ToggleEditMode()
    // {
    //     _editMode = !_editMode;
    //     FollowCamera.gameObject.SetActive(!_editMode);
    //     TopDownCamera.gameObject.SetActive(_editMode);
    // }

    public IEnumerable<EquippedCargoBay> AvailableCargoBays()
    {
        if (CurrentEntity.Parent != null)
        {
            foreach (var bay in CurrentEntity.Parent.DockingBays)
            {
                if (bay.DockedShip.IsPlayerShip) yield return bay;
            }
        }
    }

    public IEnumerable<Entity> AvailableEntities()
    {
        if(DockedEntity != null)
            foreach (var entity in DockedEntity.Children)
            {
                if (entity is Ship { IsPlayerShip: true }) yield return entity;
            }
        else if (CurrentEntity != null)
            yield return CurrentEntity;
    }

    public void PlayMusic(MusicType type)
    {
        // TODO: SFX: Music
    }

    void Update()
    {
        if(!_paused)
        {
            ApplyLatestAuthoritativeDaemonFrame();
            _time += Time.deltaTime;
            _hitMarkerTime -= Time.deltaTime;
            if(HitMarker.activeSelf && _hitMarkerTime < 0) HitMarker.SetActive(false);
            // ItemManager.Time = _time;
            if(CurrentEntity !=null && CurrentEntity.Parent==null)
            {
                foreach (var indicator in _visibleHostileIndicators)
                {
                    indicator.Value.gameObject.SetActive(indicator.Key!=CurrentEntity.Target.Value);
                    indicator.Value.Place.Target = indicator.Key.Position;
                    if (!indicator.Key.Active)
                        indicator.Value.Fill.enabled = false;
                    else
                    {
                        indicator.Value.Fill.fillAmount =
                            Saturate(indicator.Key.EntityInfoGathered[CurrentEntity] / Settings.GameplaySettings.TargetDetectionInfoThreshold);
                        indicator.Value.Fill.enabled =
                            !(indicator.Key.EntityInfoGathered[CurrentEntity] > Settings.GameplaySettings.TargetDetectionInfoThreshold) ||
                            Mathf.Sin(TargetSpottedBlinkFrequency * Time.time) + TargetSpottedBlinkOffset > 0;
                    }
                }
                foreach (var indicator in _visibleFriendlyIndicators)
                {
                    indicator.Value.gameObject.SetActive(indicator.Key!=CurrentEntity.Target.Value);
                    indicator.Value.Place.Target = indicator.Key.Position;
                    if (!indicator.Key.Active)
                        indicator.Value.Fill.enabled = false;
                    else
                    {
                        indicator.Value.Fill.enabled = true;
                        indicator.Value.Fill.fillAmount =
                            Saturate(indicator.Key.EntityInfoGathered[CurrentEntity] / Settings.GameplaySettings.TargetDetectionInfoThreshold);
                    }
                }
                var look = Input.Player.Look.ReadValue<Vector2>();
                _entityYawPitch = new float2(
                    _entityYawPitch.x + look.x * Sensitivity.x,
                    Mathf.Clamp(_entityYawPitch.y + look.y * Sensitivity.y, -.45f * Mathf.PI, .45f * Mathf.PI));
                _viewDirection = Quaternion.Euler(_entityYawPitch.y * Mathf.Rad2Deg, _entityYawPitch.x * Mathf.Rad2Deg, 0) * Vector3.forward;
                RequestLookDirection(_viewDirection);
                HeatstrokePost.weight = Saturate(Unlerp(0, Settings.GameplaySettings.SevereHeatstrokeRiskThreshold, CurrentEntity.Heatstroke));
                var severeHeatstrokeLerp = Saturate(Unlerp(Settings.GameplaySettings.SevereHeatstrokeRiskThreshold, 1, CurrentEntity.Heatstroke));
                SevereHeatstrokePost.weight =
                    severeHeatstrokeLerp + severeHeatstrokeLerp * (1 - severeHeatstrokeLerp) *
                    Mathf.Max(Settings.HeatstrokePhasingFloor, Mathf.Sin(Time.time * Settings.HeatstrokePhasingFrequency));

                if(CurrentEntity is Ship)
                {
                    var movement = Input.Player.Move.ReadValue<Vector2>();
                    TryRequestDaemonMoveVector(movement);
                }

                var target = CurrentEntity.Target.Value;
                if (target != null)
                {
                    var threshold = Settings.GameplaySettings.TargetDetectionInfoThreshold;
                    TargetVisibilityFill.fillAmount = Mathf.Lerp(.25f, .75f, (CurrentEntity.EntityInfoGathered[target] - threshold) / (1 - threshold));
                    VisibilityToTargetFill.fillAmount = Mathf.Lerp(.25f, .75f, target.EntityInfoGathered[CurrentEntity] / threshold);
                    var targetHull = ItemManager.GetRuntimeItem(target.Hull);
                    var targetMaxDurability = targetHull?.Durability > 0
                        ? (float)targetHull.Durability
                        : Math.Max(target.Hull.Durability, 1f);
                    TargetHitpointsFill.fillAmount = Mathf.Lerp(.25f, .75f, target.Hull.Durability / targetMaxDurability);
                    TargetShieldsFill.fillAmount = target.Shield == null ? 0 : Mathf.Lerp(.25f, .75f, target.Shield.Progress);
                }

                var tractorPower = Input.Player.TractorBeam.ReadValue<float>();
                RequestTractorPower(Saturate(CurrentEntity.TractorPower + Mathf.Sign(tractorPower - CurrentEntity.TractorPower) * Time.deltaTime * 2));
            }
        }
    }

    private void ApplyLatestAuthoritativeDaemonFrame()
    {
        var observer = ResolveDaemonObserver();
        var observed = observer?.LastObservedState;
        if (observed == null || !observed.IsAuthoritative)
        {
            return;
        }

        if (observed.Frame.FrameId == _lastAppliedAuthoritativeDaemonFrameId &&
            string.Equals(observed.FramePath, _lastAppliedAuthoritativeDaemonFramePath, StringComparison.Ordinal))
        {
            return;
        }

        if (TryRestoreEntityGraphFromDaemonRun(observed.Run))
        {
            _lastAppliedAuthoritativeDaemonFrameId = observed.Frame.FrameId;
            _lastAppliedAuthoritativeDaemonFramePath = observed.FramePath ?? "";
        }
    }

    private bool TryRequestDaemonMoveVector(Vector2 movement)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        var changed = !_hasSentDaemonMoveVector ||
                      (movement - _lastSentDaemonMoveVector).sqrMagnitude >
                      DaemonMoveCommandChangeThreshold * DaemonMoveCommandChangeThreshold;
        if (!changed && Time.unscaledTime < _nextDaemonMoveCommandTime)
        {
            return true;
        }

        try
        {
            observer.Operations.SetMoveVector(movement.x, movement.y, movement.magnitude);
            _lastSentDaemonMoveVector = movement;
            _hasSentDaemonMoveVector = true;
            _nextDaemonMoveCommandTime = Time.unscaledTime + DaemonMoveCommandIntervalSeconds;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon movement operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private void RequestLookDirection(Vector3 lookDirection)
    {
        TryRequestDaemonLookDirection(lookDirection);
    }

    private bool TryRequestDaemonLookDirection(Vector3 lookDirection)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        var changed = !_hasSentDaemonLookDirection ||
                      (lookDirection - _lastSentDaemonLookDirection).sqrMagnitude >
                      DaemonLookCommandChangeThreshold * DaemonLookCommandChangeThreshold;
        if (!changed && Time.unscaledTime < _nextDaemonLookCommandTime)
        {
            return true;
        }

        try
        {
            observer.Operations.SetLookDirection(lookDirection.x, lookDirection.y, lookDirection.z);
            _lastSentDaemonLookDirection = lookDirection;
            _hasSentDaemonLookDirection = true;
            _nextDaemonLookCommandTime = Time.unscaledTime + DaemonLookCommandIntervalSeconds;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon look operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private void RequestTractorPower(float power)
    {
        TryRequestDaemonTractorPower(power);
    }

    private bool TryRequestDaemonTractorPower(float power)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        var changed = !_hasSentDaemonTractorPower ||
                      Mathf.Abs(power - _lastSentDaemonTractorPower) >
                      DaemonTractorCommandChangeThreshold;
        if (!changed && Time.unscaledTime < _nextDaemonTractorCommandTime)
        {
            return true;
        }

        try
        {
            observer.Operations.SetTractorPower(power);
            _lastSentDaemonTractorPower = power;
            _hasSentDaemonTractorPower = true;
            _nextDaemonTractorCommandTime = Time.unscaledTime + DaemonTractorCommandIntervalSeconds;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon tractor-power operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private void RequestTargetSelection(Entity target)
    {
        TryRequestDaemonTargetSelection(target);
    }

    private void RequestTargetNearest()
    {
        TryRequestDaemonTargetCycle(AetheriaRuntimeDaemonCommandKinds.TargetNearest);
    }

    private void RequestTargetNext()
    {
        TryRequestDaemonTargetCycle(AetheriaRuntimeDaemonCommandKinds.TargetNext);
    }

    private void RequestTargetPrevious()
    {
        TryRequestDaemonTargetCycle(AetheriaRuntimeDaemonCommandKinds.TargetPrevious);
    }

    private void RequestTargetReticle()
    {
        TryRequestDaemonTargetReticle(_viewDirection);
    }

    private bool TryRequestDaemonTargetSelection(Entity target)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        try
        {
            if (target == null)
            {
                observer.Operations.ClearTarget();
                return true;
            }

            var targetEntityKey = ResolveEntityRecordKey(target);
            if (string.IsNullOrWhiteSpace(targetEntityKey))
            {
                return false;
            }

            observer.Operations.SetTarget(targetEntityKey);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon target operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private bool TryRequestDaemonTargetReticle(Vector3 lookDirection)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        try
        {
            observer.Operations.TargetReticle(lookDirection.x, lookDirection.y, lookDirection.z);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon reticle target operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private bool TryRequestDaemonTargetCycle(AetheriaRuntimeDaemonCommandKinds command)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        try
        {
            switch (command)
            {
                case AetheriaRuntimeDaemonCommandKinds.TargetNearest:
                    observer.Operations.TargetNearest();
                    return true;
                case AetheriaRuntimeDaemonCommandKinds.TargetNext:
                    observer.Operations.TargetNext();
                    return true;
                case AetheriaRuntimeDaemonCommandKinds.TargetPrevious:
                    observer.Operations.TargetPrevious();
                    return true;
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon target cycle operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private void RequestOverrideShutdown(bool enabled)
    {
        TryRequestDaemonOverrideShutdown(enabled);
    }

    private bool TryRequestDaemonOverrideShutdown(bool enabled)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        try
        {
            observer.Operations.SetOverrideShutdown(enabled);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon override-shutdown operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private void RequestSensorPing()
    {
        TryRequestDaemonSensorPing();
    }

    private bool TryRequestDaemonSensorPing()
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        try
        {
            observer.Operations.SensorPing();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon sensor-ping operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private void RequestHeatsinksEnabled(bool enabled)
    {
        TryRequestDaemonHeatsinksEnabled(enabled);
    }

    private bool TryRequestDaemonHeatsinksEnabled(bool enabled)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        try
        {
            observer.Operations.SetHeatsinksEnabled(enabled);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon heatsinks operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private void RequestShieldToggle()
    {
        TryRequestDaemonShieldToggle();
    }

    private bool TryRequestDaemonShieldToggle()
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        try
        {
            observer.Operations.ToggleShieldEnabled();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon shield enablement operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    public void RequestDock()
    {
        TryRequestDaemonDock();
    }

    private bool TryRequestDaemonDock()
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        try
        {
            observer.Operations.DockNearest(Settings.GameplaySettings.DockingDistance);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon dock operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    public void RequestUndock()
    {
        TryRequestDaemonUndock();
    }

    private bool TryRequestDaemonUndock()
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState || CurrentEntity == null)
        {
            return false;
        }

        try
        {
            observer.Operations.Undock();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon undock operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private bool TryRequestDaemonDockedCurrentShip(Ship ship)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState || ship == null)
        {
            return false;
        }

        var targetEntityKey = ResolveEntityRecordKey(ship);
        if (string.IsNullOrWhiteSpace(targetEntityKey))
        {
            return false;
        }

        try
        {
            observer.Operations.SetDockedCurrentShip(targetEntityKey);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon docked current ship operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private string ResolveEntityRecordKey(Entity entity)
    {
        if (entity == null || Zone == null)
        {
            return "";
        }

        foreach (var pair in _authoritativeDaemonEntities)
        {
            if (ReferenceEquals(pair.Value, entity))
                return pair.Key;
        }

        return "";
    }

    public void RequestActionBarConsumable(string itemKey)
    {
        TryRequestDaemonActionBarConsumable(itemKey);
    }

    public void RequestActionBarBehavior(int equipmentIndex, int behaviorIndex, bool active)
    {
        TryRequestDaemonActionBarBehavior(equipmentIndex, behaviorIndex, active);
    }

    public void RequestActionBarWeaponGroup(int weaponGroup, bool active)
    {
        TryRequestDaemonActionBarWeaponGroup(weaponGroup, active);
    }

    private bool TryRequestDaemonActionBarConsumable(string itemKey)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState || string.IsNullOrWhiteSpace(itemKey))
        {
            return false;
        }

        try
        {
            observer.Operations.ActivateConsumable(itemKey);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon consumable operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private bool TryRequestDaemonActionBarBehavior(int equipmentIndex, int behaviorIndex, bool active)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        try
        {
            observer.Operations.SetBehaviorActive(equipmentIndex, behaviorIndex, active);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon behavior activation operation; operation not submitted: {ex.Message}");
            return false;
        }
    }

    private bool TryRequestDaemonActionBarWeaponGroup(int weaponGroup, bool active)
    {
        var observer = ResolveDaemonObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        try
        {
            observer.Operations.SetWeaponGroupActive(weaponGroup, active);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon weapon-group operation; operation not submitted: {ex.Message}");
            return false;
        }
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
        UpdateTargetIndicators();
    }

    private void UpdateTargetIndicators()
    {
        if (CurrentEntity == null || CurrentEntity.Parent != null) return;

        ViewDot.Target = ZoneRenderer.EntityInstances[CurrentEntity].LookAtPoint.position;
        if (CurrentEntity.Target.Value != null)
            TargetIndicator.Target = CurrentEntity.Target.Value.Position;
        var distance = CultMath.math.length(AetheriaMath.ToCult((float3)ViewDot.Target) - CurrentEntity.CultPosition);
        foreach (var (_, barrels, crosshair) in _articulationGroups)
        {
            var averagePosition = Vector3.zero;
            foreach (var barrel in barrels)
                averagePosition += barrel.position + barrel.forward * distance;
            averagePosition /= barrels.Length;
            crosshair.Target = averagePosition;
        }

        foreach (var (targetLock, indicator, spin) in _lockingIndicators)
        {
            var showLockingIndicator = targetLock.Lock > .01f && CurrentEntity.Target.Value != null && CurrentEntity.Target.Value.IsHostileTo(CurrentEntity);
            indicator.gameObject.SetActive(showLockingIndicator);
            if(showLockingIndicator)
            {
                indicator.Target = CurrentEntity.Target.Value.Position;
                indicator.NoiseAmplitude = Settings.GameplaySettings.LockIndicatorNoiseAmplitude * (1 - targetLock.Lock);
                indicator.NoiseFrequency = Settings.GameplaySettings.LockIndicatorFrequency.Evaluate(targetLock.Lock);
                spin.Speed = Settings.GameplaySettings.LockSpinSpeed.Evaluate(targetLock.Lock);
            }
        }
    }
}

public abstract class DragObject{}

public abstract class ItemDragObject : DragObject
{
    protected ItemDragObject(int2 originCellOffset, ItemInstance item)
    {
        OriginCellOffset = originCellOffset;
        Item = item;
    }

    public ItemInstance Item { get; }
    public int2 OriginCellOffset { get; }
}

public class ItemInstanceDragObject : ItemDragObject
{
    public ItemInstanceDragObject(ItemInstance item, EquippedCargoBay originInventory, int2 originCellOffset) : base(originCellOffset, item)
    {
        OriginInventory = originInventory;
    }

    public EquippedCargoBay OriginInventory { get; }
}

public class EquippedItemDragObject : ItemDragObject
{
    public EquippedItemDragObject(EquippedItem item, Entity originEntity, int2 originCellOffset) : base(originCellOffset, item.EquippableItem)
    {
        EquippedItem = item;
        OriginEntity = originEntity;
    }

    public EquippedItem EquippedItem { get; }
    public Entity OriginEntity { get; }
}

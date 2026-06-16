/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cinemachine;
using GameCult.Aetheria.State.Unity;
using Ink;
using Ink.Runtime;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Unity.Mathematics;
using UnityEngine.EventSystems;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;
using float3 = Unity.Mathematics.float3;
using quaternion = Unity.Mathematics.quaternion;
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

    // Always check for null if accessing from anywhere that might not be in-game (e.g. menu UI)
    public static ActionGameManager Instance { get; private set; }
    private static DirectoryInfo _gameDataDirectory;
    public static DirectoryInfo GameDataDirectory
    {
        get => _gameDataDirectory ??= new DirectoryInfo(Application.dataPath).Parent.CreateSubdirectory("GameData");
    }

    private static string _runtimeStateFilePath;
    public static string RuntimeStateFilePath =>
        _runtimeStateFilePath ??= AetheriaRuntimeStateBoundary.GetStateFilePath(GameDataDirectory);

    private static RuntimePlayerSettings _runtimePlayerSettings;
    public static RuntimePlayerSettings RuntimePlayerSettings
    {
        get
        {
            return _runtimePlayerSettings ??= LoadRuntimePlayerSettings();
        }
    }

    public static void QueueRuntimePlayerSettingsCommit()
    {
        try
        {
            var commit = AetheriaRuntimeStateCommitLog.QueuePlayerSettings(
                RuntimeStateFilePath,
                ProjectRuntimePlayerSettings(RuntimePlayerSettings));
            Debug.Log($"Queued Aetheria Verse player settings commit: {commit.Path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to queue Aetheria Verse player settings commit: {ex}");
        }
    }

    public static void CommitRuntimeInputBindingOverride(string actionName, int bindingIndex, string inputSystemPath)
    {
        RuntimePlayerSettings.InputSettings.SetBindingOverride(actionName, bindingIndex, inputSystemPath);
        QueueRuntimePlayerSettingsCommit();
    }

    public static void CommitRuntimeActionBarInput(string inputSystemPath, bool enabled)
    {
        RuntimePlayerSettings.InputSettings.SetActionBarInputEnabled(inputSystemPath, enabled);
        QueueRuntimePlayerSettingsCommit();
    }

    public static void CommitRuntimePlayerName(string name)
    {
        RuntimePlayerSettings.Name = name ?? "";
        QueueRuntimePlayerSettingsCommit();
    }

    public static void CommitRuntimeTemperatureUnit(TemperatureUnit unit)
    {
        RuntimePlayerSettings.GameplaySettings.TemperatureUnit = unit;
        QueueRuntimePlayerSettingsCommit();
    }

    public static void CommitRuntimeSignificantDigits(int significantDigits)
    {
        RuntimePlayerSettings.GameplaySettings.SignificantDigits = significantDigits;
        QueueRuntimePlayerSettingsCommit();
    }

    public static void CommitRuntimeNebulaQuality(Quality quality)
    {
        RuntimePlayerSettings.GraphicsSettings.NebulaQuality = quality;
        QueueRuntimePlayerSettingsCommit();
    }

    public static void CommitRuntimeShowAsteroidsInMinimap(bool show)
    {
        RuntimePlayerSettings.GraphicsSettings.ShowAsteroidsInMinimap = show;
        QueueRuntimePlayerSettingsCommit();
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
            var stored = AetheriaRuntimeCatalogStore.ReadPlayerSettings(RuntimeStateFilePath);
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
            var loadouts = AetheriaRuntimeCatalogStore.ReadLoadoutTemplates(stateFilePath);
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
        blueprint.Faction = ParseLegacyGuidFromReferenceKey(blueprint.FactionKey, "aetheria.corporation");
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
        blueprint.Faction = ParseLegacyGuidFromReferenceKey(blueprint.FactionKey, "aetheria.corporation");
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

    private static Guid ParseLegacyGuidFromReferenceKey(string key, string documentName)
    {
        var legacyId = ParseLegacyIdFromReferenceKey(key, documentName);
        return Guid.TryParse(legacyId, out var id) ? id : Guid.Empty;
    }

    private static string ParseLegacyIdFromReferenceKey(string key, string documentName)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "";

        var prefix = $"{documentName}:legacy:";
        return key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? key.Substring(prefix.Length)
            : key;
    }

    public static Galaxy CurrentGalaxy;
    public static bool IsTutorial;
    public static AetheriaRuntimeRunStateSnapshot ContinueRunState { get; set; }
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
    public Transform EffectManagerParent;
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
    public ContextMenu Context;
    public DropdownMenu Dropdown;
    
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

    public float IntroDuration;
    
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
        (float2(0, 1), "Front"),
        (float2(1, 0), "Right"),
        (float2(-1, 0), "Left"),
        (float2(0, -1), "Rear")
    };

    public DragObject DragObject { get; private set; }
    private Func<DragObject, bool> _endDragCallback;

    private List<Story> _stories = new List<Story>();

    public EntitySettings NewEntitySettings
    {
        get => Settings.GameplaySettings.DefaultEntitySettings.Copy();
    }

    private static AetheriaRuntimePlayerSettingsCommit ProjectRuntimePlayerSettings(RuntimePlayerSettings settings)
    {
        return new AetheriaRuntimePlayerSettingsCommit
        {
            PlayerName = settings.Name,
            TutorialPassed = settings.TutorialPassed,
            StoryFileHashes = settings.HashedStoryFiles
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new AetheriaRuntimeStoryFileHashCommit
                {
                    StoryPath = pair.Key,
                    Hash = pair.Value
                })
                .ToArray(),
            TemperatureUnit = Enum.GetName(typeof(TemperatureUnit), settings.GameplaySettings.TemperatureUnit),
            SignificantDigits = settings.GameplaySettings.SignificantDigits,
            NebulaQuality = Enum.GetName(typeof(Quality), settings.GraphicsSettings.NebulaQuality),
            ShowAsteroidsInMinimap = settings.GraphicsSettings.ShowAsteroidsInMinimap,
            BindingOverrides = settings.InputSettings.InputActionMap
                .OrderBy(pair => pair.Key.action, StringComparer.Ordinal)
                .ThenBy(pair => pair.Key.binding)
                .Select(pair => new AetheriaRuntimeInputBindingCommit
                {
                    ActionName = pair.Key.action,
                    BindingIndex = pair.Key.binding,
                    BindingPath = pair.Value
                })
                .ToArray(),
            ActionBarInputs = settings.InputSettings.ActionBarInputs
                .OrderBy(input => input, StringComparer.Ordinal)
                .ToArray()
        };
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
            FactionKey = ReferenceKey(blueprint.FactionKey ?? "", "aetheria.corporation", LegacyId(blueprint.Faction)),
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

    private AetheriaRuntimeRunCheckpointCommit ProjectRunCheckpoint()
    {
        var entityGraph = FlattenEntityGraph(Zone);
        return new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = IsTutorial ? "tutorial" : "local",
            IsTutorial = IsTutorial,
            EntranceZoneIndex = ZoneIndex(CurrentGalaxy?.Entrance),
            ExitZoneIndex = ZoneIndex(CurrentGalaxy?.Exit),
            CurrentZoneIndex = ZoneIndex(Zone?.GalaxyZone),
            CurrentZoneEntityIndex = entityGraph.IndexOf(CurrentEntity),
            GenerationSeed = CurrentGalaxy?.GenerationSeed ?? 0,
            DiscoveredZoneIndices = CurrentGalaxy?.DiscoveredZones
                .Select(ZoneIndex)
                .Where(index => index >= 0)
                .OrderBy(index => index)
                .ToArray() ?? Array.Empty<int>(),
            ActionBarBindings = ProjectActionBarBindings(),
            FactionRelationships = ProjectFactionRelationships(),
            Zones = Zone == null ? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>() : new[] { ProjectZoneSnapshot(Zone) }
        };
    }

    private AetheriaRuntimeActionBarBindingCommit[] ProjectActionBarBindings()
    {
        return _actionBarSlots?
            .Select(ProjectActionBarBinding)
            .Where(binding => binding != null)
            .ToArray() ?? Array.Empty<AetheriaRuntimeActionBarBindingCommit>();
    }

    private static AetheriaRuntimeActionBarBindingCommit ProjectActionBarBinding(ActionBarSlot slot)
    {
        switch (slot?.Binding)
        {
            case ActionBarConsumableBinding consumable:
                return new AetheriaRuntimeActionBarBindingCommit
                {
                    ControlPath = slot.ControlPath ?? "",
                    Kind = "consumable",
                    ItemKey = consumable.TargetItemKey
                };
            case ActionBarGearBinding gear:
                return new AetheriaRuntimeActionBarBindingCommit
                {
                    ControlPath = slot.ControlPath ?? "",
                    Kind = "gear",
                    ItemKey = gear.TargetItemKey,
                    EquipmentIndex = gear.EquipmentIndex,
                    BehaviorIndex = gear.BehaviorIndex
                };
            case ActionBarWeaponGroupBinding weaponGroup:
                return new AetheriaRuntimeActionBarBindingCommit
                {
                    ControlPath = slot.ControlPath ?? "",
                    Kind = "weapon_group",
                    WeaponGroup = weaponGroup.Group
                };
            default:
                return null;
        }
    }

    private AetheriaRuntimeFactionRelationshipCommit[] ProjectFactionRelationships()
    {
        return CurrentGalaxy?.FactionRelationships?
            .Where(pair => pair.Key != null)
            .OrderBy(pair => pair.Key.ID)
            .Select(pair => new AetheriaRuntimeFactionRelationshipCommit
            {
                FactionKey = CorporationKey(pair.Key.ID),
                Relationship = pair.Value.ToString(),
                Standing = (int)pair.Value
            })
            .ToArray() ?? Array.Empty<AetheriaRuntimeFactionRelationshipCommit>();
    }

    private AetheriaRuntimeZoneSnapshotCommit ProjectZoneSnapshot(Zone zone)
    {
        var galaxyZone = zone.GalaxyZone;
        var entityGraph = FlattenEntityGraph(zone);
        return new AetheriaRuntimeZoneSnapshotCommit
        {
            ZoneIndex = ZoneIndex(galaxyZone),
            Name = galaxyZone?.Name ?? "",
            PositionX = galaxyZone?.Position.x ?? 0,
            PositionY = galaxyZone?.Position.y ?? 0,
            AdjacentZoneIndices = galaxyZone?.AdjacentZones
                .Select(ZoneIndex)
                .Where(index => index >= 0)
                .OrderBy(index => index)
                .ToArray() ?? Array.Empty<int>(),
            FactionIndices = galaxyZone?.Factions?
                .Select(FactionIndex)
                .Where(index => index >= 0)
                .OrderBy(index => index)
                .ToArray() ?? Array.Empty<int>(),
            OwnerFactionIndex = FactionIndex(galaxyZone?.Owner),
            Orbits = ProjectZoneOrbits(zone),
            Bodies = ProjectZoneBodies(zone),
            Entities = entityGraph
                .Select((entity, index) => ProjectEntitySnapshot(entityGraph, entity, index))
                .ToArray(),
            DroppedPickups = ProjectDroppedPickups(zone)
        };
    }

    private AetheriaRuntimeDroppedPickupCommit[] ProjectDroppedPickups(Zone zone)
    {
        if (Zone != zone || ZoneRenderer?.ActiveLoot == null)
            return Array.Empty<AetheriaRuntimeDroppedPickupCommit>();

        return ZoneRenderer.ActiveLoot
            .Where(pickup => pickup != null && pickup.Item != null)
            .Select((pickup, index) =>
            {
                var gridObject = pickup.GetComponent<GridObject>();
                var position = pickup.transform.position;
                var velocity = gridObject == null ? Vector3.zero : gridObject.Velocity;
                return new AetheriaRuntimeDroppedPickupCommit
                {
                    PickupIndex = index,
                    PositionX = position.x,
                    PositionY = position.y,
                    PositionZ = position.z,
                    VelocityX = velocity.x,
                    VelocityY = velocity.y,
                    VelocityZ = velocity.z,
                    Item = ProjectLoadoutItem(pickup.Item)
                };
            })
            .ToArray();
    }

    private static AetheriaRuntimeOrbitSnapshotCommit[] ProjectZoneOrbits(Zone zone)
    {
        return zone?.Orbits?.Values
            .OrderBy(orbit => orbit.ID)
            .Select(orbit => new AetheriaRuntimeOrbitSnapshotCommit
            {
                OrbitKey = OrbitKey(orbit.ID),
                ParentOrbitKey = OrbitKey(orbit.Parent),
                Distance = orbit.Distance,
                Phase = orbit.Phase,
                FixedPositionX = orbit.FixedPosition.x,
                FixedPositionY = orbit.FixedPosition.y
            })
            .ToArray() ?? Array.Empty<AetheriaRuntimeOrbitSnapshotCommit>();
    }

    private static AetheriaRuntimeBodySnapshotCommit[] ProjectZoneBodies(Zone zone)
    {
        if (zone == null)
            return Array.Empty<AetheriaRuntimeBodySnapshotCommit>();

        return zone.PlanetInstances.Values
            .Select(planet => ProjectZoneBody(planet))
            .Concat(zone.AsteroidBelts.Values.Select(belt => ProjectZoneBody(zone, belt)))
            .OrderBy(body => body.BodyKey)
            .ToArray();
    }

    private static AetheriaRuntimeBodySnapshotCommit ProjectZoneBody(Planet planet)
    {
        return new AetheriaRuntimeBodySnapshotCommit
        {
            BodyKey = BodyKey(planet.ID),
            Kind = BodyKind(planet),
            Name = planet.Name,
            OrbitKey = OrbitKey(planet.OrbitId),
            Mass = planet.Mass,
            Resources = planet.Resources?
                .OrderBy(pair => pair.Key)
                .Select(pair => new AetheriaRuntimeBodyResourceCommit
                {
                    ItemKey = pair.Key ?? "",
                    Amount = pair.Value
                })
                .ToArray() ?? Array.Empty<AetheriaRuntimeBodyResourceCommit>(),
            BodyRadiusMultiplier = planet.BodyRadiusMultiplier,
            GravityRadiusMultiplier = planet.GravityRadiusMultiplier,
            GravityDepthMultiplier = planet.GravityDepthMultiplier,
            GravityDepthExponent = planet.GravityDepthExponent,
            Asteroids = Array.Empty<AetheriaRuntimeAsteroidCommit>(),
            GasGiantVisual = planet is GasGiant gas
                ? ProjectGasGiantVisual(gas)
                : new AetheriaRuntimeGasGiantVisualCommit(),
            SunVisual = planet is Sun sun
                ? ProjectSunVisual(sun)
                : new AetheriaRuntimeSunVisualCommit()
        };
    }

    private static AetheriaRuntimeBodySnapshotCommit ProjectZoneBody(Zone zone, AsteroidBelt belt)
    {
        return new AetheriaRuntimeBodySnapshotCommit
        {
            BodyKey = BodyKey(belt.ID),
            Kind = "asteroid_belt",
            Name = belt.Name,
            OrbitKey = OrbitKey(belt.Orbit),
            Mass = belt.Mass,
            Resources = belt.Resources?
                .OrderBy(pair => pair.Key)
                .Select(pair => new AetheriaRuntimeBodyResourceCommit
                {
                    ItemKey = pair.Key ?? "",
                    Amount = pair.Value
                })
                .ToArray() ?? Array.Empty<AetheriaRuntimeBodyResourceCommit>(),
            BodyRadiusMultiplier = belt.BodyRadiusMultiplier,
            GravityRadiusMultiplier = belt.GravityRadiusMultiplier,
            GravityDepthMultiplier = belt.GravityDepthMultiplier,
            GravityDepthExponent = belt.GravityDepthExponent,
            Asteroids = ProjectAsteroids(zone, belt),
            GasGiantVisual = new AetheriaRuntimeGasGiantVisualCommit(),
            SunVisual = new AetheriaRuntimeSunVisualCommit()
        };
    }

    private static AetheriaRuntimeAsteroidCommit[] ProjectAsteroids(Zone zone, AsteroidBelt asteroidBelt)
    {
        return Enumerable.Range(0, asteroidBelt.AsteroidCount)
            .Select(asteroidIndex =>
            {
                var asteroid = asteroidBelt.GetAsteroid(asteroidIndex);
                return new AetheriaRuntimeAsteroidCommit
                {
                    Distance = asteroid.Distance,
                    Phase = asteroid.Phase,
                    Size = asteroid.Size,
                    RotationSpeed = asteroid.RotationSpeed,
                    Damage = asteroidBelt.Damage.TryGetValue(asteroidIndex, out var damage) ? damage : 0,
                    RespawnTimer = asteroidBelt.RespawnTimers.TryGetValue(asteroidIndex, out var respawnTimer) ? respawnTimer : 0,
                    MiningAccumulators = asteroidBelt.MiningAccumulator
                        .Where(pair => pair.Key.Item2 == asteroidIndex)
                        .Select(pair => new AetheriaRuntimeAsteroidMiningAccumulatorCommit
                        {
                            MinerEntityIndex = zone.Entities.IndexOf(pair.Key.Item1),
                            Amount = pair.Value
                        })
                        .Where(accumulator => accumulator.MinerEntityIndex >= 0)
                        .ToArray()
                };
            })
            .ToArray();
    }

    private static AetheriaRuntimeGasGiantVisualCommit ProjectGasGiantVisual(GasGiant gas)
    {
        return new AetheriaRuntimeGasGiantVisualCommit
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
            MaterialOverrides = gas.MaterialOverrides?.ToArray() ?? Array.Empty<string>(),
            Colors = gas.Colors?
                .Select(color => new AetheriaRuntimeColorCommit
                {
                    X = color.x,
                    Y = color.y,
                    Z = color.z,
                    W = color.w
                })
                .ToArray() ?? Array.Empty<AetheriaRuntimeColorCommit>()
        };
    }

    private static AetheriaRuntimeSunVisualCommit ProjectSunVisual(Sun sun)
    {
        return new AetheriaRuntimeSunVisualCommit
        {
            LightColorX = sun.LightColor.x,
            LightColorY = sun.LightColor.y,
            LightColorZ = sun.LightColor.z,
            FogTintColorX = sun.FogTintColor.x,
            FogTintColorY = sun.FogTintColor.y,
            FogTintColorZ = sun.FogTintColor.z,
            LightRadiusMultiplier = sun.LightRadiusMultiplier
        };
    }

    private static string BodyKind(Planet body)
    {
        return body switch
        {
            Sun => "sun",
            GasGiant => "gas_giant",
            _ => "planet"
        };
    }

    private static List<Entity> FlattenEntityGraph(Zone zone)
    {
        var entities = new List<Entity>();
        if (zone == null)
            return entities;

        var visited = new HashSet<Entity>();
        foreach (var entity in zone.Entities)
            AddEntityAndChildren(entity);
        return entities;

        void AddEntityAndChildren(Entity entity)
        {
            if (entity == null || !visited.Add(entity))
                return;

            entities.Add(entity);
            foreach (var child in entity.Children)
                AddEntityAndChildren(child);
        }
    }

    private AetheriaRuntimeEntitySnapshotCommit ProjectEntitySnapshot(IReadOnlyList<Entity> entityGraph, Entity entity, int entityIndex)
    {
        return new AetheriaRuntimeEntitySnapshotCommit
        {
            EntityIndex = entityIndex,
            Name = entity.Name ?? "",
            Kind = entity is Ship ? "ship" : entity is OrbitalEntity ? "orbital" : "entity",
            PositionX = entity.Position.x,
            PositionY = entity.Position.y,
            PositionZ = entity.Position.z,
            DirectionX = entity.Direction.x,
            DirectionY = entity.Direction.y,
            VelocityX = entity.Velocity.x,
            VelocityY = entity.Velocity.y,
            TargetEntityIndex = entity.Target?.Value == null ? -1 : entityGraph.IndexOf(entity.Target.Value),
            IsActive = entity.Active,
            HeatsinksEnabled = entity.HeatsinksEnabled,
            OverrideShutdown = entity.OverrideShutdown,
            TractorPower = entity.TractorPower,
            Heatstroke = entity.Heatstroke,
            Hypothermia = entity.Hypothermia,
            FactionKey = CorporationKey(entity.Faction?.ID ?? Guid.Empty),
            HullItemKey = entity.Hull?.ItemKey ?? "",
            Equipment = ProjectEquippedSlots(entity.Equipment),
            CargoBays = ProjectEquippedSlots(entity.CargoBays),
            DockingBays = ProjectEquippedSlots(entity.DockingBays),
            CargoContents = ProjectRuntimeCargoBays(entity.CargoBays),
            DockingBayContents = ProjectRuntimeCargoBays(entity.DockingBays),
            DockingBayAssignments = entity.DockingBays?
                .Select(bay => entity.Children.IndexOf(bay.DockedShip))
                .ToArray() ?? Array.Empty<int>(),
            Visibility = entity.Visibility,
            VisibilitySourceCount = entity.VisibilitySources.Count,
            Contacts = ProjectEntityContacts(entityGraph, entity),
            ChildEntityIndices = entity.Children?
                .Select(child => entityGraph.IndexOf(child))
                .Where(index => index >= 0)
                .ToArray() ?? Array.Empty<int>(),
            WeaponGroups = entity.WeaponGroups?
                .Select(group => (IReadOnlyList<int>)group.items.Select(item => entity.Equipment.IndexOf(item)).Where(index => index >= 0).ToArray())
                .ToArray() ?? Array.Empty<IReadOnlyList<int>>(),
            StatGrids = ProjectEntityStatGrids(entity),
            ActiveConsumables = ProjectActiveConsumables(entity),
            BehaviorProgress = ProjectBehaviorProgress(entity),
            WeaponStates = ProjectWeaponStates(entityGraph, entity),
            BehaviorStates = ProjectBehaviorStates(entity)
        };
    }

    private static AetheriaRuntimeEntityContactCommit[] ProjectEntityContacts(IReadOnlyList<Entity> entityGraph, Entity entity)
    {
        return entity.EntityInfoGathered
            .Select(contact =>
            {
                var targetIndex = entityGraph.IndexOf(contact.Key);
                entity.EntityHostility.TryGetValue(contact.Key, out var hostile);
                return new AetheriaRuntimeEntityContactCommit
                {
                    TargetEntityIndex = targetIndex,
                    InfoGathered = contact.Value,
                    Hostile = hostile,
                    Visible = entity.VisibleEntities.Contains(contact.Key)
                };
            })
            .Where(contact => contact.TargetEntityIndex >= 0)
            .ToArray();
    }

    private static AetheriaRuntimeCargoBayLoadoutCommit[] ProjectRuntimeCargoBays<TBay>(IEnumerable<TBay> bays)
        where TBay : EquippedCargoBay
    {
        return bays?
            .Select(bay => new AetheriaRuntimeCargoBayLoadoutCommit
            {
                Items = bay.Cargo?
                    .Select(slot => new AetheriaRuntimeLoadoutItemSlotCommit
                    {
                        X = slot.Value.x,
                        Y = slot.Value.y,
                        Item = ProjectLoadoutItem(slot.Key)
                    })
                    .ToArray() ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>()
            })
            .ToArray() ?? Array.Empty<AetheriaRuntimeCargoBayLoadoutCommit>();
    }

    private static AetheriaRuntimeBehaviorStateCommit[] ProjectBehaviorStates(Entity entity)
    {
        var states = new List<AetheriaRuntimeBehaviorStateCommit>();

        for (var ownerIndex = 0; ownerIndex < entity.Equipment.Count; ownerIndex++)
            states.AddRange(ProjectBehaviorStates("equipment", ownerIndex, entity.Equipment[ownerIndex].Behaviors));

        var activeConsumables = entity.ActiveConsumables;
        for (var ownerIndex = 0; ownerIndex < activeConsumables.Count; ownerIndex++)
            states.AddRange(ProjectBehaviorStates("active_consumable", ownerIndex, activeConsumables[ownerIndex].Behaviors));

        return states.ToArray();
    }

    private static IEnumerable<AetheriaRuntimeBehaviorStateCommit> ProjectBehaviorStates(
        string ownerKind,
        int ownerIndex,
        IReadOnlyList<Behavior> behaviors)
    {
        if (behaviors == null)
            yield break;

        for (var behaviorIndex = 0; behaviorIndex < behaviors.Count; behaviorIndex++)
        {
            var behavior = behaviors[behaviorIndex];
            AetheriaRuntimeBehaviorStateCommit state = null;

            if (behavior is Sensor sensor)
            {
                state = new AetheriaRuntimeBehaviorStateCommit
                {
                    Pinging = sensor.Pinging,
                    PingCooldown = sensor.Cooldown,
                    PingLerp = sensor.PingLerp,
                    PingRadius = sensor.PingRadius,
                    PingedEntityCount = sensor.PingedEntityCount
                };
            }
            else if (behavior is Radiator radiator)
            {
                state = new AetheriaRuntimeBehaviorStateCommit
                {
                    RadiatorTemperature = radiator.RadiatorTemperature,
                    Emissivity = radiator.Emissivity,
                    PumpedHeat = radiator.PumpedHeat,
                    WasteHeat = radiator.WasteHeat,
                    EnergyUsage = radiator.EnergyUsage
                };
            }
            else if (behavior is Reactor reactor)
            {
                state = new AetheriaRuntimeBehaviorStateCommit
                {
                    ReactorDraw = reactor.Draw,
                    ReactorLoadRatio = reactor.CurrentLoadRatio
                };
            }
            else if (behavior is Capacitor capacitor)
            {
                state = new AetheriaRuntimeBehaviorStateCommit
                {
                    CapacitorCharge = capacitor.Charge,
                    CapacitorCapacity = capacitor.Capacity,
                    CapacitorEfficiency = capacitor.Efficiency
                };
            }
            else if (behavior is AetherDrive drive)
            {
                state = new AetheriaRuntimeBehaviorStateCommit
                {
                    AetherDriveAxisX = drive.Axis.x,
                    AetherDriveAxisY = drive.Axis.y,
                    AetherDriveAxisZ = drive.Axis.z,
                    AetherDriveThrustX = drive.Thrust.x,
                    AetherDriveThrustY = drive.Thrust.y,
                    AetherDriveThrustZ = drive.Thrust.z,
                    AetherDriveRpmX = drive.Rpm.x,
                    AetherDriveRpmY = drive.Rpm.y,
                    AetherDriveRpmZ = drive.Rpm.z,
                    AetherDriveMaximumRpm = drive.MaximumRpm,
                    AetherDriveThrustDirectionX = drive.ThrustDirection.x,
                    AetherDriveThrustDirectionY = drive.ThrustDirection.y
                };
            }
            else if (behavior is ResourceScanner resourceScanner)
            {
                state = new AetheriaRuntimeBehaviorStateCommit
                {
                    ResourceScannerTargetBodyKey = BodyKey(resourceScanner.ScanTarget),
                    ResourceScannerAsteroidIndex = resourceScanner.Asteroid,
                    ResourceScannerScanTime = resourceScanner.ScanTime,
                    ResourceScannerRange = resourceScanner.Range,
                    ResourceScannerMinimumDensity = resourceScanner.MinimumDensity,
                    ResourceScannerScanDuration = resourceScanner.ScanDuration
                };
            }
            else if (behavior is MiningTool miningTool)
            {
                state = new AetheriaRuntimeBehaviorStateCommit
                {
                    MiningToolAsteroidBeltKey = BodyKey(miningTool.AsteroidBelt),
                    MiningToolAsteroidIndex = miningTool.Asteroid,
                    MiningToolRange = miningTool.Range
                };
            }
            else if (behavior is Thruster thruster)
            {
                state = new AetheriaRuntimeBehaviorStateCommit
                {
                    ThrusterAxis = thruster.Axis,
                    ThrusterThrust = thruster.Thrust,
                    ThrusterTorque = thruster.Torque
                };
            }
            else if (behavior is Shield shield)
            {
                state = new AetheriaRuntimeBehaviorStateCommit
                {
                    ShieldEfficiency = shield.Efficiency,
                    ShieldEnergyUsage = shield.EnergyUsage
                };
            }
            else if (behavior is VelocityLimit velocityLimit)
            {
                state = new AetheriaRuntimeBehaviorStateCommit
                {
                    VelocityLimit = velocityLimit.Limit
                };
            }
            else if (behavior is Thermotoggle thermotoggle)
            {
                state = new AetheriaRuntimeBehaviorStateCommit
                {
                    ThermotoggleTargetTemperature = thermotoggle.TargetTemperature
                };
            }
            else if (behavior is Switch switchBehavior)
            {
                state = new AetheriaRuntimeBehaviorStateCommit
                {
                    SwitchActivated = switchBehavior.Activated
                };
            }
            else if (behavior is Trigger trigger)
            {
                state = new AetheriaRuntimeBehaviorStateCommit
                {
                    TriggerPulled = trigger.Pulled
                };
            }
            else if (behavior is StatModifier statModifier)
            {
                state = new AetheriaRuntimeBehaviorStateCommit
                {
                    StatModifierApplied = statModifier.Applied,
                    StatModifierExecuted = statModifier.Executed,
                    StatModifierTargetStatCount = statModifier.TargetStatCount
                };
            }
            else if (behavior is TurretController turretController)
            {
                state = new AetheriaRuntimeBehaviorStateCommit
                {
                    TurretControllerWeaponCount = turretController.WeaponCount,
                    TurretControllerShotSpeed = turretController.ShotSpeed,
                    TurretControllerPredictShots = turretController.PredictShots
                };
            }

            if (state == null)
                continue;

            state.OwnerKind = ownerKind;
            state.OwnerIndex = ownerIndex;
            state.BehaviorIndex = behaviorIndex;
            state.BehaviorKind = behavior.Kind;
            yield return state;
        }
    }

    private static AetheriaRuntimeWeaponStateCommit[] ProjectWeaponStates(IReadOnlyList<Entity> entityGraph, Entity entity)
    {
        var states = new List<AetheriaRuntimeWeaponStateCommit>();

        for (var ownerIndex = 0; ownerIndex < entity.Equipment.Count; ownerIndex++)
            states.AddRange(ProjectWeaponStates(entityGraph, "equipment", ownerIndex, entity.Equipment[ownerIndex].Behaviors));

        var activeConsumables = entity.ActiveConsumables;
        for (var ownerIndex = 0; ownerIndex < activeConsumables.Count; ownerIndex++)
            states.AddRange(ProjectWeaponStates(entityGraph, "active_consumable", ownerIndex, activeConsumables[ownerIndex].Behaviors));

        return states.ToArray();
    }

    private static IEnumerable<AetheriaRuntimeWeaponStateCommit> ProjectWeaponStates(
        IReadOnlyList<Entity> entityGraph,
        string ownerKind,
        int ownerIndex,
        IReadOnlyList<Behavior> behaviors)
    {
        if (behaviors == null)
            yield break;

        for (var behaviorIndex = 0; behaviorIndex < behaviors.Count; behaviorIndex++)
        {
            if (!(behaviors[behaviorIndex] is Weapon weapon))
                continue;

            var state = new AetheriaRuntimeWeaponStateCommit
            {
                OwnerKind = ownerKind,
                OwnerIndex = ownerIndex,
                BehaviorIndex = behaviorIndex,
                BehaviorKind = weapon.Kind,
                Firing = weapon.Firing,
                Ammo = weapon.Ammo
            };

            if (weapon is InstantWeapon instant)
            {
                state.BurstRemaining = instant.BurstRemaining;
                state.BurstTimer = instant.BurstTimer;
                state.BurstInterval = instant.BurstInterval;
                state.CooldownProgress = instant.CooldownProgress;
                state.CoolingDown = instant.CoolingDown;
            }

            if (weapon is ChargedWeapon charged)
            {
                state.Charging = charged.Charging;
                state.Charged = charged.Charged;
                state.Charge = charged.Charge;
            }

            if (weapon is ConstantWeapon constant)
            {
                state.Reloading = constant.Reloading;
                state.ReloadProgress = constant.ReloadProgress;
                state.AmmoIntervalProgress = constant.AmmoIntervalProgress;
            }

            if (weapon is LockWeapon lockWeapon)
            {
                state.LockProgress = lockWeapon.LockProgress;
                state.LockTargetEntityIndex = lockWeapon.LockTarget == null ? -1 : entityGraph.IndexOf(lockWeapon.LockTarget);
            }

            yield return state;
        }
    }

    private static AetheriaRuntimeBehaviorProgressCommit[] ProjectBehaviorProgress(Entity entity)
    {
        var progress = new List<AetheriaRuntimeBehaviorProgressCommit>();

        for (var ownerIndex = 0; ownerIndex < entity.Equipment.Count; ownerIndex++)
            progress.AddRange(ProjectBehaviorProgress("equipment", ownerIndex, entity.Equipment[ownerIndex].Behaviors));

        var activeConsumables = entity.ActiveConsumables;
        for (var ownerIndex = 0; ownerIndex < activeConsumables.Count; ownerIndex++)
            progress.AddRange(ProjectBehaviorProgress("active_consumable", ownerIndex, activeConsumables[ownerIndex].Behaviors));

        return progress.ToArray();
    }

    private static IEnumerable<AetheriaRuntimeBehaviorProgressCommit> ProjectBehaviorProgress(
        string ownerKind,
        int ownerIndex,
        IReadOnlyList<Behavior> behaviors)
    {
        if (behaviors == null)
            yield break;

        for (var behaviorIndex = 0; behaviorIndex < behaviors.Count; behaviorIndex++)
        {
            if (!(behaviors[behaviorIndex] is IProgressBehavior progressBehavior))
                continue;

            yield return new AetheriaRuntimeBehaviorProgressCommit
            {
                OwnerKind = ownerKind,
                OwnerIndex = ownerIndex,
                BehaviorIndex = behaviorIndex,
                BehaviorKind = behaviors[behaviorIndex].Kind,
                Progress = progressBehavior.Progress
            };
        }
    }

    private static AetheriaRuntimeActiveConsumableCommit[] ProjectActiveConsumables(Entity entity)
    {
        return entity.ActiveConsumables?
            .Select(effect => new AetheriaRuntimeActiveConsumableCommit
            {
                ItemKey = effect.Item?.ItemKey ?? "",
                Quality = effect.Item?.Quality ?? 1.0,
                RemainingDuration = effect.RemainingDuration,
                Duration = effect.Duration
            })
            .ToArray() ?? Array.Empty<AetheriaRuntimeActiveConsumableCommit>();
    }

    private static AetheriaRuntimeEntityStatGridCommit[] ProjectEntityStatGrids(Entity entity)
    {
        return new[]
            {
                ProjectFloatGrid("temperature", entity.Temperature),
                ProjectFloatGrid("thermal_mass", entity.ThermalMass),
                ProjectFloatGrid("armor", entity.Armor),
                ProjectFloatGrid("max_armor", entity.MaxArmor),
                ProjectBool2Grid("hull_conductivity_x", entity.HullConductivity, axis: 0),
                ProjectBool2Grid("hull_conductivity_y", entity.HullConductivity, axis: 1)
            }
            .Where(grid => grid != null)
            .ToArray();
    }

    private static AetheriaRuntimeEntityStatGridCommit ProjectFloatGrid(string name, float[,] values)
    {
        if (values == null)
            return null;

        var width = values.GetLength(0);
        var height = values.GetLength(1);
        var flattened = new double[width * height];
        var index = 0;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            flattened[index++] = values[x, y];

        return new AetheriaRuntimeEntityStatGridCommit
        {
            Name = name,
            Width = width,
            Height = height,
            Values = flattened
        };
    }

    private static AetheriaRuntimeEntityStatGridCommit ProjectBool2Grid(string name, bool2[,] values, int axis)
    {
        if (values == null)
            return null;

        var width = values.GetLength(0);
        var height = values.GetLength(1);
        var flattened = new double[width * height];
        var index = 0;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            flattened[index++] = (axis == 0 ? values[x, y].x : values[x, y].y) ? 1.0 : 0.0;

        return new AetheriaRuntimeEntityStatGridCommit
        {
            Name = name,
            Width = width,
            Height = height,
            Values = flattened
        };
    }

    private static AetheriaRuntimeLoadoutItemSlotCommit[] ProjectEquippedSlots(IEnumerable<EquippedItem> slots)
    {
        return slots?
            .Select(slot => new AetheriaRuntimeLoadoutItemSlotCommit
            {
                X = slot.Position.x,
                Y = slot.Position.y,
                Item = ProjectLoadoutItem(slot.EquippableItem)
            })
            .ToArray() ?? Array.Empty<AetheriaRuntimeLoadoutItemSlotCommit>();
    }

    private static string LegacyId(Guid id)
    {
        return id == Guid.Empty ? "" : id.ToString("D");
    }

    private static string CorporationKey(Guid id)
    {
        return id == Guid.Empty ? "" : $"aetheria.corporation:legacy:{id:D}";
    }

    private static string OrbitKey(Guid id)
    {
        return id == Guid.Empty ? "" : $"aetheria.orbit:legacy:{id:D}";
    }

    private static string BodyKey(Guid id)
    {
        return id == Guid.Empty ? "" : $"aetheria.body:legacy:{id:D}";
    }

    private static int ZoneIndex(GalaxyZone zone)
    {
        return CurrentGalaxy?.Zones == null || zone == null ? -1 : Array.IndexOf(CurrentGalaxy.Zones, zone);
    }

    private static int FactionIndex(Faction faction)
    {
        return CurrentGalaxy?.Factions == null || faction == null ? -1 : Array.IndexOf(CurrentGalaxy.Factions, faction);
    }

    public void QueueRuntimeLoadoutTemplateCommit(EntityConstructionBlueprint blueprint)
    {
        var loadout = ProjectLoadoutTemplate(blueprint);
        LoadoutTemplates.RemoveAll(template => template.Name == loadout.Name);
        LoadoutTemplates.Add(CreateRuntimeLoadoutTemplateSnapshot(loadout));
        try
        {
            var commit = AetheriaRuntimeStateCommitLog.QueueLoadoutTemplate(
                RuntimeStateFilePath,
                loadout);
            Debug.Log($"Queued Aetheria Verse loadout commit: {commit.Path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to queue Aetheria Verse loadout commit: {ex}");
        }
    }

    public bool CommitRuntimeLoadoutRestore(AetheriaRuntimeLoadoutTemplateSnapshot template, out Entity entity)
    {
        entity = null;
        var blueprint = CreateEntityConstructionBlueprint(template);
        if (blueprint == null ||
            Zone == null ||
            DockedEntity == null)
        {
            return false;
        }

        var price = blueprint.Price(ItemManager);
        if (price > Credits)
            return false;

        entity = EntityConstructionBlueprintProjector.InstantiateFromBlueprint(ItemManager, Zone, blueprint, true);
        entity.SetParent(DockedEntity);
        Credits -= price;
        CurrentEntity = entity;
        if (entity is Ship ship)
        {
            ship.IsPlayerShip = true;
            if (DockingBay != null)
                DockingBay.DockedShip = ship;
        }

        QueueRunCheckpoint("loadout-restore");
        return true;
    }

    public bool CommitDockedCurrentShip(Ship ship)
    {
        if (ship == null ||
            !ship.IsPlayerShip ||
            DockedEntity == null ||
            DockingBay == null ||
            !DockedEntity.Children.Contains(ship))
        {
            return false;
        }

        CurrentEntity = ship;
        DockingBay.DockedShip = ship;
        QueueRunCheckpoint("docked-current-ship");
        return true;
    }

    private static AetheriaRuntimeLoadoutTemplateSnapshot CreateRuntimeLoadoutTemplateSnapshot(
        AetheriaRuntimeLoadoutTemplateCommit template)
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        return new AetheriaRuntimeLoadoutTemplateSnapshot(
            template.Name ?? "",
            template.OwnerPlayerKey ?? "",
            CreateRuntimeEntityLoadoutSnapshot(template.RootEntity),
            timestamp,
            timestamp);
    }

    private static AetheriaRuntimeEntityLoadoutSnapshot CreateRuntimeEntityLoadoutSnapshot(
        AetheriaRuntimeEntityLoadoutCommit entity)
    {
        entity ??= new AetheriaRuntimeEntityLoadoutCommit();
        return new AetheriaRuntimeEntityLoadoutSnapshot(
            entity.Name ?? "",
            entity.Kind ?? "",
            ReferenceKey(entity.FactionKey ?? "", "aetheria.corporation", entity.CorporationLegacyId ?? ""),
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

    private static string ReferenceKey(string prefix, string legacyId)
    {
        return string.IsNullOrWhiteSpace(legacyId) ? "" : $"{prefix}:legacy:{legacyId}";
    }

    private static string ReferenceKey(string typedKey, string prefix, string legacyId)
    {
        return !string.IsNullOrWhiteSpace(typedKey)
            ? typedKey
            : ReferenceKey(prefix, legacyId);
    }

    private void OnApplicationQuit() => QueueRunCheckpoint("application-quit");

    private void QueueRunCheckpoint(string reason)
    {
        if (CurrentGalaxy == null)
            return;

        try
        {
            var commit = AetheriaRuntimeStateCommitLog.QueueRunCheckpoint(
                RuntimeStateFilePath,
                ProjectRunCheckpoint());
            Debug.Log($"Queued Aetheria Verse run checkpoint ({reason}): {commit.Path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to queue Aetheria Verse run checkpoint ({reason}): {ex}");
        }
    }

    public void CommitEntityOverrideShutdown(Entity entity, bool enabled)
    {
        if (entity == null)
            return;

        entity.OverrideShutdown = enabled;
        QueueRunCheckpoint("entity-override-shutdown");
    }

    public void CommitEntityName(Entity entity, string name)
    {
        if (entity == null)
            return;

        entity.Name = name ?? "";
        QueueRunCheckpoint("entity-name");
    }

    public bool CommitWeaponGroupMembership(EquippedItem item, int groupIndex, bool assigned)
    {
        if (item?.Entity?.WeaponGroups == null ||
            groupIndex < 0 ||
            groupIndex >= item.Entity.WeaponGroups.Length)
        {
            return false;
        }

        var weapon = item.GetBehavior<Weapon>();
        if (weapon == null)
            return false;

        var group = item.Entity.WeaponGroups[groupIndex];
        if (assigned)
        {
            if (!group.items.Contains(item))
                group.items.Add(item);
            if (!group.weapons.Contains(weapon))
                group.weapons.Add(weapon);
        }
        else
        {
            group.items.Remove(item);
            group.weapons.Remove(weapon);
        }

        QueueRunCheckpoint("weapon-group-membership");
        return true;
    }

    public bool CommitCargoItemTransfer(EquippedCargoBay origin, EquippedCargoBay destination, ItemInstance item)
    {
        if (origin == null ||
            destination == null ||
            item == null ||
            origin == destination ||
            !origin.Cargo.ContainsKey(item) ||
            !destination.TryFindSpace(item))
        {
            return false;
        }

        if (item is SimpleCommodity commodity)
        {
            var quantity = commodity.Quantity;
            var transferItem = new SimpleCommodity
            {
                Reference = commodity.Reference,
                Quantity = quantity,
                Rotation = commodity.Rotation
            };

            if (!destination.TryStore(transferItem))
                return false;

            origin.Remove(commodity, quantity);
            QueueRunCheckpoint("cargo-item-transfer");
            return true;
        }

        if (!destination.TryStore(item))
            return false;

        origin.Remove(item);
        QueueRunCheckpoint("cargo-item-transfer");
        return true;
    }

    public bool CommitLootPickup(Entity entity, ItemInstance item)
    {
        if (entity == null || item == null)
            return false;

        foreach (var cargoBay in entity.CargoBays)
        {
            if (!cargoBay.TryStore(item))
                continue;

            QueueRunCheckpoint("loot-pickup");
            return true;
        }

        return false;
    }

    public bool CommitEntityDestroyed(Entity entity, ZoneRenderer zoneRenderer)
    {
        if (entity?.Zone == null)
            return false;

        if (zoneRenderer != null)
        {
            foreach (var gear in entity.Equipment)
            {
                if (gear == entity.EquippedHull || Random.value >= zoneRenderer.Settings.LootDropProbability)
                    continue;

                zoneRenderer.DropItem(
                    entity.Position,
                    Random.onUnitSphere * zoneRenderer.Settings.LootDropVelocity,
                    gear.EquippableItem);
            }

            foreach (var cargo in entity.CargoBays)
            {
                foreach (var item in cargo.Cargo.Keys.ToArray())
                {
                    zoneRenderer.DropItem(
                        entity.Position,
                        Random.onUnitSphere * zoneRenderer.Settings.LootDropVelocity,
                        item);
                }
            }
        }

        if (!entity.Zone.Entities.Remove(entity))
            return false;

        QueueRunCheckpoint("entity-destroyed");
        return true;
    }

    public bool CommitCargoItemTransfer(
        EquippedCargoBay origin,
        EquippedCargoBay destination,
        ItemInstance item,
        int2 destinationPosition)
    {
        if (origin == null ||
            destination == null ||
            item == null ||
            !origin.Cargo.ContainsKey(item) ||
            !destination.ItemFits(item, destinationPosition))
        {
            return false;
        }

        if (origin == destination)
        {
            var originalPosition = origin.Cargo[item];
            origin.Remove(item);
            if (!destination.TryStore(item, destinationPosition))
            {
                origin.TryStore(item, originalPosition);
                return false;
            }

            QueueRunCheckpoint("cargo-item-transfer");
            return true;
        }

        if (item is SimpleCommodity commodity)
        {
            var quantity = commodity.Quantity;
            var transferItem = new SimpleCommodity
            {
                Reference = commodity.Reference,
                Quantity = quantity,
                Rotation = commodity.Rotation
            };

            if (!destination.TryStore(transferItem, destinationPosition))
                return false;

            origin.Remove(commodity, quantity);
            QueueRunCheckpoint("cargo-item-transfer");
            return true;
        }

        if (!destination.TryStore(item, destinationPosition))
            return false;

        origin.Remove(item);
        QueueRunCheckpoint("cargo-item-transfer");
        return true;
    }

    public bool CommitCargoItemEquip(EquippedCargoBay origin, Entity destination, EquippableItem item)
    {
        if (origin == null ||
            destination == null ||
            item == null ||
            !origin.Cargo.ContainsKey(item) ||
            !destination.TryFindSpace(item, out _))
        {
            return false;
        }

        if (!destination.TryEquip(item))
            return false;

        origin.Remove(item);
        QueueRunCheckpoint("cargo-item-equip");
        return true;
    }

    public bool CommitCargoItemEquip(
        EquippedCargoBay origin,
        Entity destination,
        EquippableItem item,
        int2 destinationPosition)
    {
        if (origin == null ||
            destination == null ||
            item == null ||
            !origin.Cargo.ContainsKey(item) ||
            !destination.ItemFits(item, destinationPosition))
        {
            return false;
        }

        if (!destination.TryEquip(item, destinationPosition))
            return false;

        origin.Remove(item);
        QueueRunCheckpoint("cargo-item-equip");
        return true;
    }

    public bool CommitEquippedItemStore(Entity origin, EquippedItem equippedItem, EquippedCargoBay destination)
    {
        if (origin == null ||
            equippedItem?.EquippableItem == null ||
            destination == null ||
            !origin.Equipment.Contains(equippedItem) ||
            !destination.TryFindSpace(equippedItem.EquippableItem))
        {
            return false;
        }

        var item = origin.TryUnequip(equippedItem);
        if (item == null)
            return false;

        if (!destination.TryStore(item))
        {
            origin.TryEquip(item, equippedItem.Position);
            return false;
        }

        QueueRunCheckpoint("equipped-item-store");
        return true;
    }

    public bool CommitEquippedItemStore(
        Entity origin,
        EquippedItem equippedItem,
        EquippedCargoBay destination,
        int2 destinationPosition)
    {
        if (origin == null ||
            equippedItem?.EquippableItem == null ||
            destination == null ||
            !origin.Equipment.Contains(equippedItem) ||
            !destination.ItemFits(equippedItem.EquippableItem, destinationPosition))
        {
            return false;
        }

        var originalPosition = equippedItem.Position;
        var item = origin.TryUnequip(equippedItem);
        if (item == null)
            return false;

        if (!destination.TryStore(item, destinationPosition))
        {
            origin.TryEquip(item, originalPosition);
            return false;
        }

        QueueRunCheckpoint("equipped-item-store");
        return true;
    }

    public bool CommitEquippedItemEquip(
        Entity origin,
        EquippedItem equippedItem,
        Entity destination,
        int2 destinationPosition)
    {
        if (origin == null ||
            equippedItem?.EquippableItem == null ||
            destination == null ||
            !origin.Equipment.Contains(equippedItem) ||
            !destination.ItemFits(equippedItem.EquippableItem, destinationPosition))
        {
            return false;
        }

        var originalPosition = equippedItem.Position;
        var item = origin.TryUnequip(equippedItem);
        if (item == null)
            return false;

        if (!destination.TryEquip(item, destinationPosition))
        {
            origin.TryEquip(item, originalPosition);
            return false;
        }

        QueueRunCheckpoint("equipped-item-equip");
        return true;
    }

    public bool CommitTradePurchase(
        EquippedCargoBay stationInventory,
        EquippedCargoBay targetCargo,
        CraftedItemInstance item,
        int price,
        bool createsDockedShip)
    {
        if (item == null ||
            price < 0 ||
            price > Credits)
        {
            return false;
        }

        if (createsDockedShip)
        {
            if (!(item is EquippableItem hull) ||
                Zone == null ||
                DockedEntity == null)
            {
                return false;
            }

            var ship = new Ship(ItemManager, Zone, hull, NewEntitySettings) { IsPlayerShip = true };
            ship.SetParent(DockedEntity);
            Credits -= price;
            QueueRunCheckpoint("trade-purchase");
            return true;
        }

        if (stationInventory == null ||
            targetCargo == null ||
            !stationInventory.TryTransferItem(targetCargo, item))
        {
            return false;
        }

        Credits -= price;
        QueueRunCheckpoint("trade-purchase");
        return true;
    }

    public bool CommitTradePurchase(
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
            return false;
        }

        var totalPrice = (long)quantity * unitPrice;
        if (totalPrice > Credits || totalPrice > int.MaxValue)
            return false;

        if (!stationInventory.TryTransferItem(targetCargo, item, quantity))
            return false;

        Credits -= (int)totalPrice;
        QueueRunCheckpoint("trade-purchase");
        return true;
    }

    public void CommitEquippedItemOverrideShutdown(EquippedItem item, bool enabled)
    {
        if (item?.EquippableItem == null)
            return;

        item.EquippableItem.OverrideShutdown = enabled;
        QueueRunCheckpoint("item-override-shutdown");
    }

    public void CommitThermotoggleTargetTemperature(Thermotoggle thermotoggle, float targetTemperature)
    {
        if (thermotoggle == null)
            return;

        thermotoggle.TargetTemperature = targetTemperature;
        QueueRunCheckpoint("thermotoggle-target-temperature");
    }

    public void CommitEntityShutdownPerformance(Entity entity, float shutdownPerformance)
    {
        if (entity?.Settings == null)
            return;

        entity.Settings.ShutdownPerformance = shutdownPerformance;
        QueueRunCheckpoint("entity-shutdown-performance");
    }

    public bool CommitHullConductivityToggle(Entity entity, int2 position, int axis)
    {
        if (entity?.HullConductivity == null ||
            position.x < 0 ||
            position.y < 0 ||
            position.x >= entity.HullConductivity.GetLength(0) ||
            position.y >= entity.HullConductivity.GetLength(1) ||
            axis < 0 ||
            axis > 1)
        {
            return false;
        }

        var conductivity = entity.HullConductivity[position.x, position.y];
        if (axis == 0)
            conductivity.x = !conductivity.x;
        else
            conductivity.y = !conductivity.y;

        entity.HullConductivity[position.x, position.y] = conductivity;
        QueueRunCheckpoint("hull-conductivity-toggle");
        return true;
    }

    private void OnDisable()
    {
        Input.Dispose();
        EntityInstance.ClearWeaponManagers();
    }

    void Start()
    {
        Instance = this;
        EntityInstance.EffectManagerParent = EffectManagerParent;

        var stateBoot = AetheriaRuntimeStateBoot.Inspect(GameDataDirectory);
        _runtimeStateFilePath = stateBoot.StateFilePath;
        Debug.Log($"Aetheria typed state file: {stateBoot.StateFilePath}");
        if (!stateBoot.StateFileExists)
        {
            Debug.LogWarning("Aetheria typed state file is missing. Run the Aetheria.State importer before treating legacy catalog data as runtime state.");
        }
        else
        {
            RuntimeCatalog = AetheriaRuntimeCatalogStore.OpenReadOnly(stateBoot.StateFilePath);
            Debug.Log($"Aetheria typed runtime catalog: {RuntimeCatalog.Items.Count} items, {RuntimeCatalog.Corporations.Count} corporations, {RuntimeCatalog.NameFiles.Count} name files");
        }

        if (RuntimeCatalog == null)
        {
            throw new InvalidOperationException("Aetheria typed runtime catalog is required before gameplay boot.");
        }

        var runtimeItemCatalog = new AetheriaRuntimeItemCatalog(RuntimeCatalog);
        ItemManager = new ItemManager(
            runtimeItemCatalog,
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
                MenuMap.Position = CurrentEntity.Position.xz;
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
            else if (CurrentEntity.Parent == null)
            {
                foreach (var wormhole in ZoneRenderer.WormholeInstances.Keys)
                {
                    if (!(length(wormhole.Position - CurrentEntity.Position.xz) < Settings.GameplaySettings.WormholeExitRadius)) continue;
                    EnterWormhole(wormhole);
                }
                Dock();
            }
            else Undock();
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
                CommitEntityOverrideShutdown(CurrentEntity, !CurrentEntity.OverrideShutdown);
        };

        Input.Player.Ping.performed += context =>
        {
            CurrentEntity.Sensor?.Ping();
        };

        Input.Player.ToggleHeatsinks.performed += context =>
        {
            CurrentEntity.HeatsinksEnabled = !CurrentEntity.HeatsinksEnabled;
            // TODO: SFX: Success/Fail
        };

        Input.Player.ToggleShield.performed += context =>
        {
            if (CurrentEntity.Shield != null)
            {
                CurrentEntity.Shield.Item.Enabled.Value = !CurrentEntity.Shield.Item.Enabled.Value;
                // TODO: SFX: Success/Fail
            }
        };

        #region Targeting

        Input.Player.TargetReticle.performed += context =>
        {
            if (!CurrentEntity.VisibleEnemies.Any()) return;
            var underReticle = CurrentEntity.VisibleEnemies.Where(x => x != CurrentEntity)
                .MaxBy(x => dot(normalize(x.Position - CurrentEntity.Position), CurrentEntity.LookDirection));
            CurrentEntity.Target.Value = CurrentEntity.Target.Value == underReticle ? null : underReticle;
        };

        Input.Player.TargetNearest.performed += context =>
        {
            if(CurrentEntity.VisibleEnemies.Any())
            {
                CurrentEntity.Target.Value = CurrentEntity.VisibleEnemies.Where(x => x != CurrentEntity)
                    .MaxBy(x => length(x.Position - CurrentEntity.Position));
            }
        };

        Input.Player.TargetNext.performed += context =>
        {
            if (!CurrentEntity.VisibleEnemies.Any()) return;
            var targets = CurrentEntity.VisibleEnemies.Where(x => x != CurrentEntity).OrderBy(x => length(x.Position - CurrentEntity.Position)).ToArray();
            var currentTargetIndex = Array.IndexOf(targets, CurrentEntity.Target.Value);
            CurrentEntity.Target.Value = targets[(currentTargetIndex + 1) % targets.Length];
        };

        Input.Player.TargetPrevious.performed += context =>
        {
            if (!CurrentEntity.VisibleEnemies.Any()) return;
            var targets = CurrentEntity.VisibleEnemies.Where(x => x != CurrentEntity).OrderBy(x => length(x.Position - CurrentEntity.Position)).ToArray();
            var currentTargetIndex = Array.IndexOf(targets, CurrentEntity.Target.Value);
            CurrentEntity.Target.Value = targets[(currentTargetIndex + targets.Length - 1) % targets.Length];
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
                RegisterDragTarget(dragAction =>
                {
                    //Debug.Log("Registering binding!");
                    switch (dragAction)
                    {
                        case EquippedItemDragObject equippedItemDragAction:
                            var trigger = equippedItemDragAction.EquippedItem.GetBehavior<IActivatedBehavior>();
                            if (trigger == null) return false;
                            slot.Binding = new ActionBarGearBinding(CurrentEntity, slot, equippedItemDragAction.EquippedItem, trigger);
                            return true;
                        case ItemInstanceDragObject itemInstanceDragAction:
                            var consumable = FindTypedActionBarConsumable(itemInstanceDragAction.Item);
                            if (consumable == null) return false;
                            slot.Binding = new ActionBarConsumableBinding(
                                CurrentEntity,
                                slot,
                                consumable);
                            return true;
                        case WeaponGroupDragObject weaponGroupDragAction:
                            slot.Binding = new ActionBarWeaponGroupBinding(CurrentEntity, slot, weaponGroupDragAction.Group);
                            return true;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(dragAction));
                    }
                });
            });
            slot.PointerExitTrigger.OnPointerExitAsObservable().Subscribe(_ =>
            {
                //Debug.Log($"Pointer exited action bar slot {controlPath}");
                UnregisterDragTarget();
            });
            return slot;
        }

        AetheriaRuntimeCatalogItem FindTypedActionBarConsumable(ItemInstance item)
        {
            var typedItem = FindTypedActionBarItem(item);
            return typedItem != null &&
                   string.Equals(typedItem.Category, AetheriaRuntimeItemCategories.Consumable, StringComparison.Ordinal)
                ? typedItem
                : null;
        }

        AetheriaRuntimeCatalogItem FindTypedActionBarItem(ItemInstance item)
        {
            return RuntimeCatalog?.FindItem(item?.ItemKey ?? "");
        }

        var bindings = RuntimePlayerSettings.InputSettings.ActionBarInputs.OrderBy(i => i)
            .Select(createBinding).ToList();

        #endregion

        #endregion
        
        StartGame();
        
        //if (!RuntimePlayerSettings.InputSettings.ActionBarInputs.Any())
        {
            var newbinds = Enumerable.Range(0, 64)//CurrentEntity.WeaponGroups
                .Zip(
                    bindings,
                    (i, slot) =>
                        slot.Binding = new ActionBarWeaponGroupBinding(CurrentEntity, slot, i)
                );
        }
    }

    public void BeginDrag(DragObject dragObject)
    {
        this.DragObject = dragObject;
    }

    public void RegisterDragTarget(Func<DragObject, bool> onEndDrag)
    {
        _endDragCallback = onEndDrag;
    }

    public void UnregisterDragTarget()
    {
        _endDragCallback = null;
    }

    public bool EndDrag()
    {
        var success = _endDragCallback?.Invoke(DragObject);
        DragObject = null;
        return success ?? false;
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

    private void EnterWormhole(Wormhole wormhole)
    {
        if (!(CurrentEntity is Ship ship) || ship.WormholeAnimationInProgress) return;
        // var wormholeCameraFollow = new GameObject("Wormhole Camera Follow").transform;
        // wormholeCameraFollow.position = new Vector3(wormhole.Position.x, -50, wormhole.Position.y);
        // wormholeCameraFollow.rotation = Quaternion.LookRotation(Vector3.down, ship.LookDirection);
        // WormholeCamera.enabled = true;
        // WormholeCamera.Follow = wormholeCameraFollow;
        // FollowCamera.enabled = false;
        ship.EnterWormhole(wormhole.Position);
        ship.OnEnteredWormhole += () =>
        {
            var oldZone = Zone;
            PopulateLevel(wormhole.Target);
            foreach (var zone in wormhole.Target.AdjacentZones)
                CurrentGalaxy.DiscoveredZones.Add(zone);
            SectorMap.QueueZoneReveal(wormhole.Target.AdjacentZones);
            ship.ExitWormhole(ZoneRenderer.WormholeInstances.Keys.First(w => w.Target == oldZone.GalaxyZone).Position,
                Settings.GameplaySettings.WormholeExitVelocity * ItemManager.Random.NextFloat2Direction());
            CurrentEntity.Zone = Zone;
            QueueRunCheckpoint("wormhole-transition");
        };
    }

    public void PopulateLevel(GalaxyZone galaxyZone)
    {
        if (galaxyZone == null) throw new ArgumentNullException(nameof(galaxyZone));
        
        if (galaxyZone.Contents == null)
        {
            var constructionBlueprint = ZoneGenerator.GenerateZone(
                ItemManager,
                RuntimeCatalog,
                Settings.ZoneSettings,
                CurrentGalaxy,
                galaxyZone,
                IsTutorial
            );
            galaxyZone.Contents = new Zone(ItemManager, Settings.PlanetSettings, constructionBlueprint, galaxyZone, CurrentGalaxy);
        }
        Zone = galaxyZone.Contents;
        PlayMusic(MusicType.Overworld);
        
        Zone.Log = s => Debug.Log($"Zone: {s}");

        if (CurrentEntity != null)
        {
            CurrentEntity.Deactivate();
            CurrentEntity.Zone.Entities.Remove(CurrentEntity);
            CurrentEntity.Zone = Zone;
            Zone.Entities.Add(CurrentEntity);
            CurrentEntity.Activate();
        }
        
        ZoneRenderer.LoadZone(Zone);
        RestoreDroppedPickupsFromTypedZoneState(galaxyZone);
        
        if (CurrentEntity != null)
        {
            UnbindEntity();
            BindToEntity(CurrentEntity);
        }
    }

    private void RestoreDroppedPickupsFromTypedZoneState(GalaxyZone galaxyZone)
    {
        if (RuntimeCatalog == null ||
            ZoneRenderer == null ||
            galaxyZone == null)
        {
            return;
        }

        var zoneIndex = ZoneIndex(galaxyZone);
        if (zoneIndex < 0)
            return;

        var runId = IsTutorial ? "tutorial" : "local";
        var zoneKey = $"global:aetheria.run_state.{runId}.zone.{zoneIndex}.v1";
        AetheriaRuntimeZoneStateSnapshot zoneState;
        try
        {
            zoneState = AetheriaRuntimeCatalogStore.ReadZoneStates(RuntimeStateFilePath)
                .FirstOrDefault(zone => string.Equals(zone.RecordKey, zoneKey, StringComparison.Ordinal));
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to read typed dropped pickup state for zone {zoneKey}: {ex}");
            return;
        }

        if (zoneState?.DroppedPickups == null || zoneState.DroppedPickups.Count == 0)
            return;

        foreach (var pickup in zoneState.DroppedPickups.OrderBy(pickup => pickup.PickupIndex))
        {
            var item = CreateLoadoutItem(pickup.Item);
            if (item == null)
                continue;

            ZoneRenderer.DropItem(
                new Vector3((float)pickup.PositionX, (float)pickup.PositionY, (float)pickup.PositionZ),
                new Vector3((float)pickup.VelocityX, (float)pickup.VelocityY, (float)pickup.VelocityZ),
                item);
        }
    }

    private void ToggleFullscreenMenu(GameObject menu)
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null) return;
        if (CurrentEntity == null) return;
        if (menu.activeSelf)
        {
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
        else
        {
            _paused = true;
            menu.SetActive(true);
            UiRoot.SetActive(false);
            _menuShown = Menu.gameObject.activeSelf;
            if (!_menuShown) DisablePlayerInput();
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
        if (CurrentGalaxy != null)
        {
            var continuingRun = ContinueRunState;
            var startZone = ResolveStartZone(continuingRun);
            SectorMap.QueueZoneReveal(startZone.AdjacentZones.Prepend(startZone));
            PopulateLevel(startZone);

            if (continuingRun != null)
            {
                RestoreEntityGraphFromTypedRun(continuingRun);
                ContinueRunState = null;
                return;
            }

            var loadoutGenerator = new LoadoutGenerator(ref ItemManager.Random, ItemManager, RuntimeCatalog, CurrentGalaxy, Zone.GalaxyZone, IsTutorial ? CurrentGalaxy.ResolveFaction(Settings.TutorialGenerationSettings.ProtagonistFaction) : null, 2);
            var ship = EntityConstructionBlueprintProjector.InstantiateFromBlueprint(
                ItemManager,
                Zone,
                loadoutGenerator.GenerateShipLoadout(data => string.IsNullOrEmpty(Settings.StartingHullName) || data.Name==Settings.StartingHullName ),
                true);
            ((Ship) ship).IsPlayerShip = true;
            ship.Position = float3.zero;
            ship.Zone = Zone;
            Zone.Entities.Add(ship);
            ship.Activate();
            BindToEntity(ship);
        }
    }

    private void RestoreEntityGraphFromTypedRun(AetheriaRuntimeRunStateSnapshot run)
    {
        if (run == null ||
            run.CurrentZoneIndex < 0 ||
            run.CurrentZoneEntityIndex < 0)
        {
            return;
        }

        var zoneEntityKeyPrefix = $"global:aetheria.run_state.{run.RunId}.zone.{run.CurrentZoneIndex}.entity.";
        var currentEntityKey = $"{zoneEntityKeyPrefix}{run.CurrentZoneEntityIndex}.v1";
        AetheriaRuntimeEntitySnapshot[] entitySnapshots;
        try
        {
            entitySnapshots = AetheriaRuntimeCatalogStore.ReadEntitySnapshots(RuntimeStateFilePath)
                .Where(entity => entity.RecordKey.StartsWith(zoneEntityKeyPrefix, StringComparison.Ordinal))
                .OrderBy(entity => EntityIndexFromRecordKey(entity.RecordKey))
                .ToArray();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to read typed entity snapshots for zone {zoneEntityKeyPrefix}: {ex}");
            return;
        }

        if (entitySnapshots.Length == 0)
        {
            Debug.LogWarning($"No typed entity snapshots found for restored zone {zoneEntityKeyPrefix}.");
            return;
        }

        ReplaceZoneEntitiesFromTypedSnapshots(entitySnapshots, currentEntityKey);
    }

    private void ReplaceZoneEntitiesFromTypedSnapshots(
        IReadOnlyList<AetheriaRuntimeEntitySnapshot> entitySnapshots,
        string currentEntityKey)
    {
        foreach (var existingEntity in Zone.Entities.ToArray())
        {
            existingEntity.Deactivate();
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

            var entity = EntityConstructionBlueprintProjector.InstantiateFromBlueprint(ItemManager, Zone, blueprint, true);
            if (entity == null)
                continue;

            entity.Position = new float3((float)entitySnapshot.PositionX, (float)entitySnapshot.PositionY, (float)entitySnapshot.PositionZ);
            entity.Direction = new float2((float)entitySnapshot.DirectionX, (float)entitySnapshot.DirectionY);
            entity.Velocity = new float2((float)entitySnapshot.VelocityX, (float)entitySnapshot.VelocityY);
            entity.OverrideShutdown = entitySnapshot.OverrideShutdown;
            entity.TractorPower = (float)entitySnapshot.TractorPower;
            entity.Zone = Zone;
            Zone.Entities.Add(entity);
            restoredEntities[entitySnapshot.RecordKey] = entity;
        }

        foreach (var entity in restoredEntities.Values)
            entity.Activate();

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

        if (restoredEntities.TryGetValue(currentEntityKey, out var currentEntity))
            RestoreCurrentEntityBinding(currentEntity);
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
                ship.Deactivate();
            }
        }
    }

    private void RestoreCurrentEntityBinding(Entity currentEntity)
    {
        if (currentEntity.Parent != null)
        {
            var dockingBay = currentEntity.Parent.DockingBays.FirstOrDefault(bay => bay.DockedShip == currentEntity);
            if (dockingBay != null && currentEntity.Parent is OrbitalEntity)
            {
                CurrentEntity = currentEntity;
                DoDock(currentEntity.Parent, dockingBay);
                return;
            }
        }

        BindToEntity(currentEntity);
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
                        new float3((float)behaviorState.AetherDriveAxisX, (float)behaviorState.AetherDriveAxisY, (float)behaviorState.AetherDriveAxisZ),
                        new float3((float)behaviorState.AetherDriveThrustX, (float)behaviorState.AetherDriveThrustY, (float)behaviorState.AetherDriveThrustZ),
                        new float3((float)behaviorState.AetherDriveRpmX, (float)behaviorState.AetherDriveRpmY, (float)behaviorState.AetherDriveRpmZ),
                        (float)behaviorState.AetherDriveMaximumRpm,
                        new float2((float)behaviorState.AetherDriveThrustDirectionX, (float)behaviorState.AetherDriveThrustDirectionY));
                    break;
                case ResourceScanner resourceScanner:
                    resourceScanner.RestoreRuntimeState(
                        ParseLegacyGuidFromReferenceKey(behaviorState.ResourceScannerTargetBodyId, "aetheria.body"),
                        behaviorState.ResourceScannerAsteroidIndex,
                        (float)behaviorState.ResourceScannerScanTime,
                        (float)behaviorState.ResourceScannerRange,
                        (float)behaviorState.ResourceScannerMinimumDensity,
                        (float)behaviorState.ResourceScannerScanDuration);
                    break;
                case MiningTool miningTool:
                    miningTool.RestoreRuntimeState(
                        ParseLegacyGuidFromReferenceKey(behaviorState.MiningToolAsteroidBeltId, "aetheria.body"),
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

    private static GalaxyZone ResolveStartZone(AetheriaRuntimeRunStateSnapshot run)
    {
        if (CurrentGalaxy?.Zones == null || CurrentGalaxy.Zones.Length == 0)
            return CurrentGalaxy?.Entrance;

        if (run != null &&
            run.CurrentZoneIndex >= 0 &&
            run.CurrentZoneIndex < CurrentGalaxy.Zones.Length)
        {
            return CurrentGalaxy.Zones[run.CurrentZoneIndex];
        }

        return CurrentGalaxy.Entrance;
    }

    private IEnumerator IntroCutscene(Ship ship)
    {
        ZoneRenderer.PerspectiveEntity = ship;
        var entityPosition = ship.Position.xz;
        var followOrbit = Zone.Orbits.Keys.MinBy(o => lengthsq(Zone.GetOrbitPosition(o) - entityPosition));
        var followPlanetId = Zone.PlanetInstances.Values.First(planet => planet.OrbitId == followOrbit).ID;
        var followPlanet = ZoneRenderer.Planets[followPlanetId];
        DockCamera.Follow = followPlanet.Body.transform;
        var rootOrbit = followOrbit;
        while (Zone.Orbits[rootOrbit].Parent != Guid.Empty)
            rootOrbit = Zone.Orbits[rootOrbit].Parent;
        var rootPlanetId = Zone.PlanetInstances.Values.First(planet => planet.OrbitId == rootOrbit).ID;
        var rootPlanet = ZoneRenderer.Planets[rootPlanetId];
        DockCamera.LookAt = rootPlanet.Body.transform;

        var shipVelocity = ship.GetBehavior<VelocityLimit>().Limit;
        var followOrbitPosition = Zone.GetOrbitPosition(followOrbit);
        var shipDirection = normalize(Zone.GetOrbitPosition(rootOrbit) - followOrbitPosition);
        ship.Position.xz = followOrbitPosition - shipDirection * shipVelocity * IntroDuration;

        var startTime = Time.time;
        while (Time.time - startTime < IntroDuration)
        {
            ship.Direction = shipDirection;
            ship.Velocity = shipDirection * shipVelocity;
            yield return null;
        }
        
        BindToEntity(ship);
    }

    public void Dock()
    {
        if (CurrentEntity.Parent != null) return;
        if (CurrentEntity is Ship ship)
        {
            foreach (var entity in Zone.Entities.ToArray())
            {
                if (entity != CurrentEntity && lengthsq(entity.Position.xz - CurrentEntity.Position.xz) <
                    Settings.GameplaySettings.DockingDistance * Settings.GameplaySettings.DockingDistance)
                {
                    var bay = entity.TryDock(ship);
                    if (bay != null)
                    {
                        UnbindEntity();
                        DoDock(entity, bay);
                        // TODO: SFX: Docking
                        //AkSoundEngine.PostEvent("Dock", gameObject);
                        return;
                    }
                }
            }
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
        var parentOrbit = Zone.Orbits[orbital.OrbitId].Parent;
        var parentOrbitPlanet = Zone.PlanetInstances.Values.FirstOrDefault(planet => planet.OrbitId == parentOrbit)?.ID ?? Guid.Empty;
        if (ZoneRenderer.Planets.ContainsKey(parentOrbitPlanet))
            DockCamera.LookAt = ZoneRenderer.Planets[parentOrbitPlanet].Body.transform;
        else DockCamera.LookAt = ZoneRenderer.ZoneRoot;
        if (entity is OrbitalEntity {CanTow: true})
            TowingStation = entity;
        Menu.ShowTab(MenuTab.Inventory);
    }

    public void Undock()
    {
        if (CurrentEntity.Parent == null) return;
        if (CurrentEntity is Ship ship)
        {
            if (CurrentEntity.GetBehavior<Cockpit>() == null)
            {
                Dialog.Clear();
                Dialog.Title.text = "Can't undock. Missing cockpit component!";
                Dialog.Show();
                Dialog.MoveToCursor();
                // TODO: SFX: Fail
            }
            else if (CurrentEntity.GetBehavior<Thruster>() == null && CurrentEntity.GetBehavior<AetherDrive>() == null)
            {
                Dialog.Clear();
                Dialog.Title.text = "Can't undock. Missing thruster component!";
                Dialog.Show();
                Dialog.MoveToCursor();
                // TODO: SFX: Fail
            }
            else if (CurrentEntity.GetBehavior<Reactor>() == null)
            {
                Dialog.Clear();
                Dialog.Title.text = "Can't undock. Missing reactor component!";
                Dialog.Show();
                Dialog.MoveToCursor();
                // TODO: SFX: Fail
            }
            else if (CurrentEntity.Parent.TryUndock(ship))
            {
                BindToEntity(ship);
                // TODO: SFX: Undock
            }
            else
            {
                Dialog.Title.text = "Can't undock. Must empty docking bay!";
                Dialog.Show();
                Dialog.MoveToCursor();
                // TODO: SFX: Fail
            }
        }
    }

    public void TowShip()
    {
        if (CurrentEntity.Parent != null) return;
        if (CurrentEntity is Ship ship)
        {
            if (Zone.Equals(TowingStation.Zone))
            {
                var distance = (int)length(TowingStation.Position.xz - CurrentEntity.Position.xz);
                CurrentEntity.Position.xz = TowingStation.Position.xz;
                Dock();
                //Debug.Log($"${distance}");
                //payment = distance * TowingZoneRate;
            }
            else
            {
                var distance = Zone.GalaxyZone.Distance[TowingStation.Zone.GalaxyZone];
                PopulateLevel(TowingStation.Zone.GalaxyZone);
                CurrentEntity.Zone = Zone;
                CurrentEntity.Position.xz = TowingStation.Position.xz;
                Dock();
                //Debug.Log($"${distance * 1000}");
                //payment = distance * TowingGalaxyRate;
            }
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

    private void BindToEntity(Entity entity)
    {
        if (!ZoneRenderer.EntityInstances.ContainsKey(entity))
        {
            Debug.LogError($"Attempted to bind to entity {entity.Name}, but SectorRenderer has no such instance!");
            return;
        }
        
        CurrentEntity = entity;
        DeathPost.weight = 0;
        ZoneRenderer.PerspectiveEntity = CurrentEntity;
        
        Menu.gameObject.SetActive(false);
        DockedEntity = null;
        DockingBay = null;
        DockCamera.enabled = false;
        FollowCamera.enabled = true;

        if (length(CurrentEntity.Direction) > .1f)
            _viewDirection = float3(CurrentEntity.Direction.x,0,CurrentEntity.Direction.y);
        
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
        
        _shipSubscriptions.Add(CurrentEntity.Death.Subscribe(Die));
        
        _lockingIndicators = CurrentEntity.GetBehaviors<LockWeapon>()
            .Select(x =>
            {
                var i = LockIndicator.Instantiate<PlaceUIElementWorldspace>();
                return (x, i, i.GetComponent<Rotate>());
            }).ToArray();
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

    private void Die(CauseOfDeath cause)
    {
        var deathTime = Time.time;
        UnbindEntity();
        CurrentEntity = null;
        MainMenu.gameObject.SetActive(true);
        Menu.gameObject.SetActive(false);
        CurrentGalaxy = null;
        QueueRuntimePlayerSettingsCommit();
        Observable.EveryUpdate()
            .Where(_ => Time.time - deathTime < DeathPostTransitionTime)
            .Subscribe(_ =>
                {
                    var t = (Time.time - deathTime) / DeathPostTransitionTime;
                    if(cause==CauseOfDeath.Heatstroke)
                    {
                        HeatstrokePost.weight = 1 - t;
                        SevereHeatstrokePost.weight = 1 - t;
                    }
                    else if (cause == CauseOfDeath.Hypothermia)
                    {
                        HypothermiaPost.weight = 1 - t;
                        SevereHypothermiaPost.weight = 1 - t;
                    }
                    DeathPost.weight = t;
                },
                () =>
                {
                    HeatstrokePost.weight = 0;
                    SevereHeatstrokePost.weight = 0;
                    HypothermiaPost.weight = 0;
                    SevereHypothermiaPost.weight = 0;
                    DeathPost.weight = 1;
                });
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
                            saturate(indicator.Key.EntityInfoGathered[CurrentEntity] / Settings.GameplaySettings.TargetDetectionInfoThreshold);
                        indicator.Value.Fill.enabled =
                            !(indicator.Key.EntityInfoGathered[CurrentEntity] > Settings.GameplaySettings.TargetDetectionInfoThreshold) ||
                            sin(TargetSpottedBlinkFrequency * Time.time) + TargetSpottedBlinkOffset > 0;
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
                            saturate(indicator.Key.EntityInfoGathered[CurrentEntity] / Settings.GameplaySettings.TargetDetectionInfoThreshold);
                    }
                }
                var look = Input.Player.Look.ReadValue<Vector2>();
                _entityYawPitch = float2(_entityYawPitch.x + look.x * Sensitivity.x, clamp(_entityYawPitch.y + look.y * Sensitivity.y, -.45f * PI, .45f * PI));
                _viewDirection = mul(float3(0, 0, 1), Unity.Mathematics.float3x3.Euler(float3(_entityYawPitch.yx, 0), RotationOrder.YXZ));
                CurrentEntity.LookDirection = _viewDirection;
                HeatstrokePost.weight = saturate(unlerp(0, Settings.GameplaySettings.SevereHeatstrokeRiskThreshold, CurrentEntity.Heatstroke));
                var severeHeatstrokeLerp = saturate(unlerp(Settings.GameplaySettings.SevereHeatstrokeRiskThreshold, 1, CurrentEntity.Heatstroke));
                SevereHeatstrokePost.weight =
                    severeHeatstrokeLerp + severeHeatstrokeLerp * (1 - severeHeatstrokeLerp) *
                    max(Settings.HeatstrokePhasingFloor, sin(Time.time * Settings.HeatstrokePhasingFrequency));
                
                if(CurrentEntity is Ship ship)
                {
                    ship.MovementDirection = Input.Player.Move.ReadValue<Vector2>();
                }

                var target = CurrentEntity.Target.Value;
                if (target != null)
                {
                    var threshold = Settings.GameplaySettings.TargetDetectionInfoThreshold;
                    TargetVisibilityFill.fillAmount = lerp(.25f, .75f, (CurrentEntity.EntityInfoGathered[target] - threshold)/(1-threshold));
                    VisibilityToTargetFill.fillAmount = lerp(.25f, .75f, target.EntityInfoGathered[CurrentEntity] / threshold);
                    var targetHull = ItemManager.GetRuntimeItem(target.Hull);
                    var targetMaxDurability = targetHull?.Durability > 0
                        ? (float)targetHull.Durability
                        : Math.Max(target.Hull.Durability, 1f);
                    TargetHitpointsFill.fillAmount = lerp(.25f, .75f, target.Hull.Durability / targetMaxDurability);
                    TargetShieldsFill.fillAmount = target.Shield == null ? 0 : lerp(.25f, .75f, target.Shield.Progress);
                }

                var tractorPower = Input.Player.TractorBeam.ReadValue<float>();
                CurrentEntity.TractorPower =
                    saturate(CurrentEntity.TractorPower + sign(tractorPower - CurrentEntity.TractorPower) * Time.deltaTime * 2);
            }
            Zone.Update(Time.deltaTime);
        }
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
        var distance = length((float3)ViewDot.Target - CurrentEntity.Position);
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

public class WeaponGroupDragObject : DragObject
{
    public WeaponGroupDragObject(int group)
    {
        Group = group;
    }

    public int Group { get; }
}

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

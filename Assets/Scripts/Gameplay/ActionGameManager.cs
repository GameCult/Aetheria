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
    private static string RuntimeStateFilePath =>
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
            LoadoutBlueprints.Clear();
            foreach (var loadout in loadouts)
            {
                var blueprint = CreateRuntimeBlueprint(loadout.RootEntity);
                if (blueprint == null)
                    continue;

                if (string.IsNullOrWhiteSpace(blueprint.Name))
                    blueprint.Name = loadout.Name;

                LoadoutBlueprints.Add(blueprint);
            }

            if (LoadoutBlueprints.Count > 0)
                Debug.Log($"Loaded {LoadoutBlueprints.Count} Aetheria Verse loadout templates from typed state.");
        }
        catch (FileNotFoundException)
        {
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load Aetheria Verse loadout templates from typed state: {ex}");
        }
    }

    private RuntimeEntityBlueprint CreateRuntimeBlueprint(AetheriaRuntimeEntityLoadoutSnapshot entity)
    {
        var hull = CreateEquippableLoadoutItem(entity.Hull);
        if (hull == null)
            return null;

        RuntimeEntityBlueprint blueprint = string.Equals(entity.Kind, "orbital", StringComparison.OrdinalIgnoreCase)
            ? new RuntimeOrbitalEntityBlueprint()
            : new RuntimeShipBlueprint
            {
                Direction = new float2(0, 1)
            };

        blueprint.Name = entity.Name;
        blueprint.Faction = ParseLegacyGuidFromReferenceKey(entity.FactionKey, "aetheria.corporation");
        blueprint.Hull = hull;
        blueprint.Equipment = CreateEquippableSlots(entity.Equipment);
        blueprint.CargoBays = CreateEquippableSlots(entity.CargoBays);
        blueprint.DockingBays = CreateEquippableSlots(entity.DockingBays);
        blueprint.CargoContents = CreateCargoBayContents(entity.CargoContents);
        blueprint.DockingBayContents = CreateCargoBayContents(entity.DockingBayContents);
        blueprint.DockingBayAssignments = entity.DockingBayAssignments.ToArray();
        blueprint.WeaponGroups = entity.WeaponGroups.Select(group => group.ToArray()).ToArray();
        blueprint.Children = entity.Children
            .Select(CreateRuntimeBlueprint)
            .Where(child => child != null)
            .ToArray();
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
        var legacyId = ParseLegacyIdFromReferenceKey(item.ItemKey, "aetheria.item_definition");
        var typedItem = RuntimeCatalog?.FindItemByLegacyId(legacyId);
        if (typedItem == null)
            return null;

        if (typedItem.Stackable)
            return ItemManager.CreateSimpleCommodityInstance(typedItem, Math.Max(1, item.Quantity));

        var instance = ItemManager.CreateCraftedInstance(typedItem, (float)item.Quality);
        if (instance is EquippableItem equippable && item.Durability > 0)
            equippable.Durability = (float)item.Durability;
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
    public List<RuntimeEntityBlueprint> LoadoutBlueprints { get; } = new List<RuntimeEntityBlueprint>();

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

    private AetheriaRuntimeLoadoutTemplateCommit ProjectLoadoutTemplate(RuntimeEntityBlueprint blueprint)
    {
        return new AetheriaRuntimeLoadoutTemplateCommit
        {
            Name = blueprint.Name ?? "",
            OwnerPlayerKey = $"global:aetheria.player_settings.v1",
            RootEntity = ProjectEntityLoadout(blueprint)
        };
    }

    private AetheriaRuntimeEntityLoadoutCommit ProjectEntityLoadout(RuntimeEntityBlueprint blueprint)
    {
        return new AetheriaRuntimeEntityLoadoutCommit
        {
            Name = blueprint.Name ?? "",
            Kind = blueprint is RuntimeShipBlueprint ? "ship" : blueprint is RuntimeOrbitalEntityBlueprint ? "orbital" : "entity",
            CorporationLegacyId = blueprint.Faction == Guid.Empty ? "" : blueprint.Faction.ToString("D"),
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
            ItemDefinitionLegacyId = item.ItemId == Guid.Empty ? "" : item.ItemId.ToString("D"),
            Quality = item is CraftedItemInstance crafted ? crafted.Quality : 1.0,
            Durability = item is EquippableItem equippable ? equippable.Durability : 1.0,
            Quantity = item is SimpleCommodity commodity ? commodity.Quantity : 1
        };
    }

    private AetheriaRuntimeRunCheckpointCommit ProjectRunCheckpoint()
    {
        return new AetheriaRuntimeRunCheckpointCommit
        {
            RunId = IsTutorial ? "tutorial" : "local",
            IsTutorial = IsTutorial,
            EntranceZoneIndex = ZoneIndex(CurrentGalaxy?.Entrance),
            ExitZoneIndex = ZoneIndex(CurrentGalaxy?.Exit),
            CurrentZoneIndex = ZoneIndex(Zone?.GalaxyZone),
            CurrentZoneEntityIndex = Zone?.Entities?.IndexOf(CurrentEntity) ?? -1,
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
                    ItemDefinitionLegacyId = consumable.TargetItemDefinitionLegacyId
                };
            case ActionBarGearBinding gear:
                return new AetheriaRuntimeActionBarBindingCommit
                {
                    ControlPath = slot.ControlPath ?? "",
                    Kind = "gear",
                    ItemDefinitionLegacyId = gear.TargetItemDefinitionLegacyId,
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
                CorporationLegacyId = pair.Key.ID == Guid.Empty ? "" : pair.Key.ID.ToString("D"),
                Relationship = pair.Value.ToString(),
                Standing = (int)pair.Value
            })
            .ToArray() ?? Array.Empty<AetheriaRuntimeFactionRelationshipCommit>();
    }

    private AetheriaRuntimeZoneSnapshotCommit ProjectZoneSnapshot(Zone zone)
    {
        var galaxyZone = zone.GalaxyZone;
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
            Orbits = ProjectZoneOrbits(zone.Blueprint),
            Bodies = ProjectZoneBodies(zone.Blueprint),
            Entities = zone.Entities
                .Select((entity, index) => ProjectEntitySnapshot(zone, entity, index))
                .ToArray()
        };
    }

    private static AetheriaRuntimeOrbitSnapshotCommit[] ProjectZoneOrbits(RuntimeZoneBlueprint blueprint)
    {
        return blueprint?.Orbits?
            .OrderBy(orbit => orbit.ID)
            .Select(orbit => new AetheriaRuntimeOrbitSnapshotCommit
            {
                OrbitLegacyId = LegacyId(orbit.ID),
                ParentLegacyId = LegacyId(orbit.Parent),
                Distance = orbit.Distance,
                Phase = orbit.Phase,
                FixedPositionX = orbit.FixedPosition.x,
                FixedPositionY = orbit.FixedPosition.y
            })
            .ToArray() ?? Array.Empty<AetheriaRuntimeOrbitSnapshotCommit>();
    }

    private static AetheriaRuntimeBodySnapshotCommit[] ProjectZoneBodies(RuntimeZoneBlueprint blueprint)
    {
        return blueprint?.Planets?
            .OrderBy(body => body.ID)
            .Select(ProjectZoneBody)
            .ToArray() ?? Array.Empty<AetheriaRuntimeBodySnapshotCommit>();
    }

    private static AetheriaRuntimeBodySnapshotCommit ProjectZoneBody(BodyData body)
    {
        return new AetheriaRuntimeBodySnapshotCommit
        {
            BodyLegacyId = LegacyId(body.ID),
            Kind = BodyKind(body),
            Name = body.Name ?? "",
            OrbitLegacyId = LegacyId(body.Orbit),
            Mass = body.Mass,
            Resources = body.Resources?
                .OrderBy(pair => pair.Key)
                .Select(pair => new AetheriaRuntimeBodyResourceCommit
                {
                    ItemDefinitionLegacyId = LegacyId(pair.Key),
                    Amount = pair.Value
                })
                .ToArray() ?? Array.Empty<AetheriaRuntimeBodyResourceCommit>(),
            BodyRadiusMultiplier = body.BodyRadiusMultiplier,
            GravityRadiusMultiplier = body.GravityRadiusMultiplier,
            GravityDepthMultiplier = body.GravityDepthMultiplier,
            GravityDepthExponent = body.GravityDepthExponent,
            Asteroids = body is AsteroidBeltData belt
                ? ProjectAsteroids(belt.Asteroids)
                : Array.Empty<AetheriaRuntimeAsteroidCommit>(),
            GasGiantVisual = body is GasGiantData gas
                ? ProjectGasGiantVisual(gas)
                : new AetheriaRuntimeGasGiantVisualCommit(),
            SunVisual = body is SunData sun
                ? ProjectSunVisual(sun)
                : new AetheriaRuntimeSunVisualCommit()
        };
    }

    private static AetheriaRuntimeAsteroidCommit[] ProjectAsteroids(Asteroid[] asteroids)
    {
        return asteroids?
            .Select(asteroid => new AetheriaRuntimeAsteroidCommit
            {
                Distance = asteroid.Distance,
                Phase = asteroid.Phase,
                Size = asteroid.Size,
                RotationSpeed = asteroid.RotationSpeed
            })
            .ToArray() ?? Array.Empty<AetheriaRuntimeAsteroidCommit>();
    }

    private static AetheriaRuntimeGasGiantVisualCommit ProjectGasGiantVisual(GasGiantData gas)
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

    private static AetheriaRuntimeSunVisualCommit ProjectSunVisual(SunData sun)
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

    private static string BodyKind(BodyData body)
    {
        return body switch
        {
            SunData => "sun",
            GasGiantData => "gas_giant",
            AsteroidBeltData => "asteroid_belt",
            PlanetData => "planet",
            _ => "body"
        };
    }

    private AetheriaRuntimeEntitySnapshotCommit ProjectEntitySnapshot(Zone zone, Entity entity, int entityIndex)
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
            CorporationLegacyId = entity.Faction?.ID.ToString("D") ?? "",
            HullItemDefinitionLegacyId = entity.Hull?.ItemId == Guid.Empty ? "" : entity.Hull.ItemId.ToString("D"),
            Equipment = ProjectEquippedSlots(entity.Equipment),
            CargoBays = ProjectEquippedSlots(entity.CargoBays),
            DockingBays = ProjectEquippedSlots(entity.DockingBays),
            ChildEntityIndices = entity.Children?
                .Select(child => zone.Entities.IndexOf(child))
                .Where(index => index >= 0)
                .ToArray() ?? Array.Empty<int>(),
            WeaponGroups = entity.WeaponGroups?
                .Select(group => (IReadOnlyList<int>)group.items.Select(item => entity.Equipment.IndexOf(item)).Where(index => index >= 0).ToArray())
                .ToArray() ?? Array.Empty<IReadOnlyList<int>>(),
            StatGrids = ProjectEntityStatGrids(entity)
        };
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

    private static int ZoneIndex(GalaxyZone zone)
    {
        return CurrentGalaxy?.Zones == null || zone == null ? -1 : Array.IndexOf(CurrentGalaxy.Zones, zone);
    }

    private static int FactionIndex(Faction faction)
    {
        return CurrentGalaxy?.Factions == null || faction == null ? -1 : Array.IndexOf(CurrentGalaxy.Factions, faction);
    }

    public void QueueRuntimeLoadoutTemplateCommit(RuntimeEntityBlueprint blueprint)
    {
        LoadoutBlueprints.RemoveAll(loadout => loadout.Name == blueprint.Name);
        LoadoutBlueprints.Add(blueprint);
        try
        {
            var commit = AetheriaRuntimeStateCommitLog.QueueLoadoutTemplate(
                RuntimeStateFilePath,
                ProjectLoadoutTemplate(blueprint));
            Debug.Log($"Queued Aetheria Verse loadout commit: {commit.Path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to queue Aetheria Verse loadout commit: {ex}");
        }
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

    private void OnDisable()
    {
        Input.Dispose();
        ConsoleController.ClearCommands();
        EntityInstance.ClearWeaponManagers();
    }

    void Start()
    {
        Instance = this;
        EntityInstance.EffectManagerParent = EffectManagerParent;
        ConsoleController.MessageReceiver = this;

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
            CurrentEntity.OverrideShutdown = !CurrentEntity.OverrideShutdown;
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
            var itemId = item?.ItemId ?? Guid.Empty;
            return itemId == Guid.Empty
                ? null
                : RuntimeCatalog?.FindItemByLegacyId(itemId.ToString("D"));
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
        
        ConsoleController.AddCommand("revealzones",
            _ =>
            {
                foreach (var zones in CurrentGalaxy.Zones
                    .Where(z=>!CurrentGalaxy.DiscoveredZones.Contains(z))
                    .GroupBy(z=>z.Distance[CurrentGalaxy.Entrance])
                    .OrderBy(g=>g.Key))
                {
                    SectorMap.QueueZoneReveal(zones);
                }
            });
        
        ConsoleController.AddCommand("trackmissile",
            _ =>
            {
                foreach (var missileManager in FindObjectsByType<GuidedProjectileManager>(FindObjectsSortMode.None))
                {
                    missileManager.OnFireGuided.Where(x => x.source == _currentEntity).Take(1).Subscribe(x =>
                    {
                        FollowCamera.Follow = x.missile.transform;
                        FollowCamera.LookAt = x.target;
                        x.missile.OnKill += () =>
                        {
                            FollowCamera.LookAt = ZoneRenderer.EntityInstances[CurrentEntity].LookAtPoint;
                            FollowCamera.Follow = ZoneRenderer.EntityInstances[CurrentEntity].transform;
                        };
                    });
                }
            });
        
        ConsoleController.AddCommand("spawnturret",
            _ =>
            {
                var nearestFaction = CurrentGalaxy.Factions.MinBy(f => CurrentGalaxy.HomeZones[f].Distance[Zone.GalaxyZone]);

                var loadoutGenerator = IsTutorial ? new LoadoutGenerator(
                    ref ItemManager.Random,
                    ItemManager,
                    RuntimeCatalog,
                    CurrentGalaxy,
                    Zone.GalaxyZone,
                    nearestFaction,
                    .5f) : new LoadoutGenerator(
                    ref ItemManager.Random,
                    ItemManager,
                    RuntimeCatalog,
                    CurrentGalaxy, 
                    Zone.GalaxyZone,
                    nearestFaction,
                    .5f);

                var turret = RuntimeEntityBlueprintProjector.InstantiateFromBlueprint(ItemManager, Zone, loadoutGenerator.GenerateTurretLoadout(), true);
                turret.Position.xz = _currentEntity.Position.xz +
                                     ItemManager.Random.NextFloat2Direction() * ItemManager.Random.NextFloat(50, 500);
                turret.Zone = Zone;
                Zone.Entities.Add(turret);
                turret.Activate();
            });
        //Temporary, or not
        ConsoleController.AddCommand("tow", _ => TowShip());

        // ConsoleController.AddCommand("pingscene",
        //     _ =>
        //     {
        //         var startTime = Time.time;
        //         Observable.EveryUpdate().TakeWhile(_ => Time.time - startTime < 5).Subscribe(
        //             _ => Debug.Log($"{(int) (Time.time - startTime)}"),
        //             () =>
        //             {
        //                 var nearestFaction = CurrentSector.Factions.MinBy(f => CurrentSector.HomeZones[f].Distance[Zone.SectorZone]);
        //                 var nearestFactionHomeZone = CurrentSector.HomeZones[nearestFaction];
        //                 var factionPresence = nearestFaction.InfluenceDistance - nearestFactionHomeZone.Distance[Zone.SectorZone] + 1;
        //
        //                 var loadoutGenerator = new LoadoutGenerator(
        //                     ref ItemManager.Random,
        //                     ItemManager,
        //                     CurrentSector,
        //                     Zone.SectorZone,
        //                     nearestFaction,
        //                     .5f);
        //
        //                 for (int i = 0; i < 8; i++)
        //                 {
        //                     var ship = RuntimeEntityBlueprintProjector.InstantiateFromBlueprint(ItemManager, Zone, loadoutGenerator.GenerateShipLoadout(), true);
        //                     ship.Position.xz = _currentEntity.Position.xz +
        //                                        ItemManager.Random.NextFloat2Direction() * ItemManager.Random.NextFloat(50, 500);
        //                     ship.Zone = Zone;
        //                     Zone.Entities.Add(ship);
        //                     ship.Activate();
        //                 }
        //
        //                 for (int i = 0; i < 8; i++)
        //                 {
        //                     var turret = RuntimeEntityBlueprintProjector.InstantiateFromBlueprint(ItemManager, Zone, loadoutGenerator.GenerateTurretLoadout(), true);
        //                     turret.Position.xz = _currentEntity.Position.xz +
        //                                          ItemManager.Random.NextFloat2Direction() * ItemManager.Random.NextFloat(50, 500);
        //                     turret.Zone = Zone;
        //                     Zone.Entities.Add(turret);
        //                     turret.Activate();
        //                 }
        //             });
        //     });
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
            galaxyZone.RuntimeBlueprint ??= ZoneGenerator.GenerateZone(
                ItemManager,
                RuntimeCatalog,
                Settings.ZoneSettings,
                CurrentGalaxy,
                galaxyZone,
                IsTutorial
            );
            galaxyZone.Contents = new Zone(ItemManager, Settings.PlanetSettings, galaxyZone.RuntimeBlueprint, galaxyZone, CurrentGalaxy);
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
        
        if (CurrentEntity != null)
        {
            UnbindEntity();
            BindToEntity(CurrentEntity);
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
            SectorMap.QueueZoneReveal(CurrentGalaxy.Entrance.AdjacentZones.Prepend(CurrentGalaxy.Entrance));
            PopulateLevel(CurrentGalaxy.Entrance);
            var loadoutGenerator = new LoadoutGenerator(ref ItemManager.Random, ItemManager, RuntimeCatalog, CurrentGalaxy, Zone.GalaxyZone, IsTutorial ? CurrentGalaxy.ResolveFaction(Settings.TutorialGenerationSettings.ProtagonistFaction) : null, 2);
            var ship = RuntimeEntityBlueprintProjector.InstantiateFromBlueprint(
                ItemManager,
                Zone,
                loadoutGenerator.GenerateShipLoadout(data => string.IsNullOrEmpty(Settings.StartingHullName) || data.Name==Settings.StartingHullName ),
                true);
            // RuntimeEntityBlueprintProjector.InstantiateFromBlueprint(ItemManager, Zone, LoadoutBlueprints.First(x => x.Name == StarterShipTemplate), true);
            ((Ship) ship).IsPlayerShip = true;
            ship.Position = float3.zero;
            ship.Zone = Zone;
            Zone.Entities.Add(ship);
            ship.Activate();
            BindToEntity(ship);
        }
    }

    private IEnumerator IntroCutscene(Ship ship)
    {
        ZoneRenderer.PerspectiveEntity = ship;
        var entityPosition = ship.Position.xz;
        var followOrbit = Zone.Orbits.Keys.MinBy(o => lengthsq(Zone.GetOrbitPosition(o) - entityPosition));
        var followPlanet = ZoneRenderer.Planets[Zone.Planets.FirstOrDefault(p => p.Value.Orbit == followOrbit).Key];
        DockCamera.Follow = followPlanet.Body.transform;
        var rootOrbit = followOrbit;
        while (Zone.Orbits[rootOrbit].Data.Parent != Guid.Empty)
            rootOrbit = Zone.Orbits[rootOrbit].Data.Parent;
        var rootPlanet = ZoneRenderer.Planets[Zone.Planets.FirstOrDefault(p => p.Value.Orbit == rootOrbit).Key];
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
        var parentOrbit = Zone.Orbits[orbital.OrbitData].Data.Parent;
        var parentOrbitPlanet = Zone.Planets.FirstOrDefault(p => p.Value.Orbit == parentOrbit).Key;
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

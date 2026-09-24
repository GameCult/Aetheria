/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using GameCult.Caching;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cinemachine;
using Ink;
using Ink.Runtime;
using MessagePack;
using Newtonsoft.Json;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Serialization;
using UnityEngine.UI;
using CultMath;
using CultMath.UnityBridge;
using UnityEngine.EventSystems;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using Path = System.IO.Path;
using quaternion = CultMath.quaternion;
using Random = UnityEngine.Random;

public class ActionGameManager : MonoBehaviour
{
    // Always check for null if accessing from anywhere that might not be in-game (e.g. menu UI)
    public static ActionGameManager Instance { get; private set; }
    private static DirectoryInfo _gameDataDirectory;
    public static DirectoryInfo GameDataDirectory
    {
        get => _gameDataDirectory ??= new DirectoryInfo(Application.dataPath).Parent.CreateSubdirectory("GameData");
    }

    private static CultCache _cultCache;

    private static string CatalogPath => Path.Combine(GameDataDirectory.FullName, "Aetheria.cc");

    public static CultCache CultCache
    {
        get
        {
            if (_cultCache != null) return _cultCache;

            // All three stores attach once and stay attached until the process exits. The run lifecycle is record-level
            // inside this cache; reopening would replace the catalog instances the galaxy and live entities hold.
            // The catalog is read-only in every build; capturepreset writes through its own cache (Loadouts.Commit).
            _cultCache = AetheriaStores.Open(
                CatalogPath,
                runPath: Path.Combine(GameDataDirectory.FullName, "run.cc"),
                playerPath: Path.Combine(GameDataDirectory.FullName, "player.cc"));

            return _cultCache;
        }
    }

    // The only creator of PlayerSettings: an absent global means first launch, and the defaults are committed.
    public static PlayerSettings PlayerSettings
    {
        get
        {
            var settings = CultCache.GetGlobal<PlayerSettings>();
            if (settings != null) return settings;
            settings = GetDefaultPlayerSettings();
            CultCache.Commit(batch => batch.Upsert(settings));
            return settings;
        }
    }

    public static void SavePlayerSettings()
    {
        var settings = PlayerSettings;
        CultCache.Commit(batch => batch.Upsert(settings));
    }

    private static PlayerSettings GetDefaultPlayerSettings()
    {
        var settings = new PlayerSettings();
        settings.Name = Environment.UserName;
        settings.InputSettings.ActionBarInputs.Add("<Keyboard>/leftShift");
        settings.InputSettings.ActionBarInputs.Add("<Mouse>/leftButton");
        settings.InputSettings.ActionBarInputs.Add("<Mouse>/rightButton");
        settings.InputSettings.ActionBarInputs.Add("<Mouse>/middleButton");
        for (int i = 1; i < 6; i++) settings.InputSettings.ActionBarInputs.Add($"<Keyboard>/{i}");
        return settings;
    }

    public static Galaxy CurrentGalaxy;
    public static bool IsTutorial;

    public GameSettings Settings;
    //public string StarterShipTemplate = "Longinus";
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
    public TextMeshProUGUI DebugInfoText;
    
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
    private readonly Dictionary<EquippedItem, ShotOutcome> _debugLastShots = new Dictionary<EquippedItem, ShotOutcome>();
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

    private readonly (float2 direction, string name)[] _directions = {
        (float2(0, 1), "Front"),
        (float2(1, 0), "Right"),
        (float2(-1, 0), "Left"),
        (float2(0, -1), "Rear")
    };

    public DragObject DragObject { get; private set; }
    private Func<DragObject, bool> _endDragCallback;

    private List<Story> _stories = new List<Story>();

    private void OnApplicationQuit()
    {
        SaveRun();
        SavePlayerSettings();
    }

    // Wormhole arrival and quit save the run; a dead run has no galaxy, so it is not saved.
    public void SaveRun()
    {
        if (CurrentGalaxy != null)
        {
            var (game, zones) = RunSave.Capture(CultCache, CurrentGalaxy, Zone, DockedEntity ?? CurrentEntity, IsTutorial,
                _actionBarSlots.Select(s => s.Save()).ToArray());
            RunSave.Commit(CultCache, game, zones, ItemManager.Lots);
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
        
        ItemManager = new ItemManager(CultCache, RunSave.Lots(CultCache), Settings.GameplaySettings, Debug.Log);
        ZoneRenderer.ItemManager = ItemManager;
        
        // If hiding minimap asteroids, turn them off to start with
        if (!PlayerSettings.GraphicsSettings.ShowAsteroidsInMinimap)
            ZoneRenderer.ShowAsteroidUI = false;
        
        // TODO: Process Stories

        #region Input Handling

        Input = new AetheriaInput();
        foreach (var action in PlayerSettings.InputSettings.InputActionMap) foreach (var binding in action.Value) Input.asset[action.Key].ApplyBindingOverride(binding.Key, binding.Value);

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

        // Flips the player's own stance on the current target between hostile and neutral. Going
        // neutral mid-fight safes weapons (Weapon.StanceAllowsFire); an unmarked/undetected target
        // reads as non-hostile here, so the first press declares hostile.
        Input.Player.ToggleStance.performed += context =>
        {
            var target = CurrentEntity.Target.Value;
            if (target == null) return;
            CurrentEntity.SetIff(target, !CurrentEntity.IsHostileTo(target));
        };

        #region Targeting

        Input.Player.TargetReticle.performed += context =>
        {
            if (!CurrentEntity.VisibleEntities.Any()) return;
            var underReticle = CurrentEntity.VisibleEntities.Where(x => x != CurrentEntity)
                .MaxBy(x => dot(normalize(x.Position - CurrentEntity.Position), CurrentEntity.LookDirection));
            CurrentEntity.Target.Value = CurrentEntity.Target.Value == underReticle ? null : underReticle;
        };

        // Nearest is enemies-only (it drives weapon lock), and picks the closest target, not the farthest.
        Input.Player.TargetNearest.performed += context =>
        {
            if(CurrentEntity.VisibleEnemies.Any())
            {
                CurrentEntity.Target.Value = CurrentEntity.VisibleEnemies.Where(x => x != CurrentEntity)
                    .MinBy(x => length(x.Position - CurrentEntity.Position));
            }
        };

        Input.Player.TargetNext.performed += context =>
        {
            if (!CurrentEntity.VisibleEntities.Any()) return;
            var targets = CurrentEntity.VisibleEntities.Where(x => x != CurrentEntity).OrderBy(x => length(x.Position - CurrentEntity.Position)).ToArray();
            var currentTargetIndex = Array.IndexOf(targets, CurrentEntity.Target.Value);
            CurrentEntity.Target.Value = targets[(currentTargetIndex + 1) % targets.Length];
        };

        Input.Player.TargetPrevious.performed += context =>
        {
            if (!CurrentEntity.VisibleEntities.Any()) return;
            var targets = CurrentEntity.VisibleEntities.Where(x => x != CurrentEntity).OrderBy(x => length(x.Position - CurrentEntity.Position)).ToArray();
            var currentTargetIndex = Array.IndexOf(targets, CurrentEntity.Target.Value);
            CurrentEntity.Target.Value = targets[(currentTargetIndex + targets.Length - 1) % targets.Length];
        };

        // Cut 2 (docs/fire-control-cut.md): cycles the aim point among the current target's revealed
        // subsystems -- decides nothing itself, only calls the one writer (TrySelectTargetItem), same
        // predicate (FireControl.IsRevealed) the AI path uses. No generated typed accessor exists for this
        // action yet (it was hand-authored into Aetheria.inputactions rather than regenerated through the
        // Unity Input Actions editor, unavailable here), so it is looked up by name instead of through
        // Input.Player.
        Input.asset.FindAction("Player/Cycle Target Item").performed += context =>
        {
            var target = CurrentEntity.Target.Value;
            if (target == null) return;
            var revealed = target.Equipment.Where(x => FireControl.IsRevealed(CurrentEntity, x)).ToArray();
            if (revealed.Length == 0)
            {
                CurrentEntity.TrySelectTargetItem(null);
                return;
            }
            var currentItemIndex = Array.IndexOf(revealed, CurrentEntity.ResolvedTargetItem);
            CurrentEntity.TrySelectTargetItem(revealed[(currentItemIndex + 1) % revealed.Length]);
        };

        #endregion


        #region Action Bar

        ActionBarSlot createBinding(string controlPath)
        {
            var action = new InputAction(binding: controlPath);
            _actionBarActions.Add(action);
            var slot = Instantiate(ActionBarSlot, ActionBar);
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
                            if (!(ItemManager.GetData(itemInstanceDragAction.Item) is ConsumableItemData consumable)) return false;
                            slot.Binding = new ActionBarConsumableBinding(CurrentEntity, slot, consumable);
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

        var bindings = PlayerSettings.InputSettings.ActionBarInputs.OrderBy(i => i)
            .Select(createBinding).ToList();

        #endregion

        #endregion
        
        StartGame();
        
        //if (!PlayerSettings.InputSettings.ActionBarInputs.Any())
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
        
        ConsoleController.AddCommand("give",
            args =>
            {
                var itemName = string.Join(" ", args);
                var item = ItemManager.ItemData.GetAll<EquippableItemData>()
                    .FirstOrDefault(itemData => string.Equals(itemData.Name, itemName, StringComparison.InvariantCultureIgnoreCase));
                if (item != null)
                {
                    _currentEntity.CargoBays.First().TryStore(ItemManager.CreateInstance(ItemManager.CreateLot(item, default, .95f)));
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
                    CurrentGalaxy,
                    Zone.GalaxyZone,
                    nearestFaction,
                    .5f) : new LoadoutGenerator(
                    ref ItemManager.Random,
                    ItemManager,
                    CurrentGalaxy, 
                    Zone.GalaxyZone,
                    nearestFaction,
                    .5f);

                var turret = EntitySerializer.Unpack(ItemManager, Zone, loadoutGenerator.GenerateTurretLoadout());
                turret.Position.xz = _currentEntity.Position.xz +
                                     ItemManager.Random.NextFloat2Direction() * ItemManager.Random.NextFloat(50, 500);
                turret.Zone = Zone;
                Zone.Entities.Add(turret);
                turret.Activate();
            });
        //Temporary, or not
        ConsoleController.AddCommand("tow", _ => TowShip());

        // Manual IFF override for the player's current target, for hand-testing combat.
        // "iff hostile"/"iff neutral" force a stance; "iff clear" restores the derived faction rule.
        ConsoleController.AddCommand("iff", args =>
        {
            var console = ConsoleController.Instance;
            var target = _currentEntity?.Target.Value;
            if (target == null) { console.AppendLogLine("iff: no target selected"); return; }
            var mode = args.Length > 0 ? args[0] : "";
            if (mode != "hostile" && mode != "neutral" && mode != "clear")
            {
                console.AppendLogLine("usage: iff hostile|neutral|clear");
                return;
            }
            switch (mode)
            {
                case "hostile":
                    _currentEntity.SetIff(target, true);
                    break;
                case "neutral":
                    _currentEntity.SetIff(target, false);
                    break;
                case "clear":
                    _currentEntity.SetIff(target, null);
                    break;
            }
            console.AppendLogLine($"{target.Name}: {(_currentEntity.IsHostileTo(target) ? "hostile" : "neutral")}");
        });

        // Editor only: capturepreset "<name>" [replace] writes the piloted ship as a catalog preset. A multi-word name must
        // be quoted; any other second argument than replace is refused rather than dropped.
        if (Application.isEditor)
            ConsoleController.AddCommand("capturepreset", args =>
            {
                var console = ConsoleController.Instance;
                var name = args.Length > 0 ? args[0] : "";
                var replace = args.Length == 2 && args[1] == "replace";
                if (string.IsNullOrWhiteSpace(name) || args.Length > 2 || args.Length == 2 && !replace)
                {
                    console.AppendLogLine("usage: capturepreset \"<name>\" [replace] (quote a name with spaces)");
                    return;
                }
                if (!(_currentEntity is Ship ship)) { console.AppendLogLine("capturepreset: pilot a ship first"); return; }
                try
                {
                    var written = Loadouts.Commit(CatalogPath, Loadouts.Capture(ItemManager, ship, name), replace);
                    console.AppendLogLine(written
                        ? $"Preset '{name}' written to {CatalogPath}. This session sees it after the catalog reloads (next launch). " +
                          "Reopen CultCache Studio before saving there: a Studio session opened earlier overwrites captured presets."
                        : $"Preset '{name}' changed on disk since load; nothing written");
                }
                catch (InvalidOperationException e)
                {
                    console.AppendLogLine(e.Message);
                }
            });
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
            SaveRun();
        };
    }

    public void PopulateLevel(GalaxyZone galaxyZone)
    {
        if (galaxyZone == null) throw new ArgumentNullException(nameof(galaxyZone));
        
        if (galaxyZone.Contents == null)
        {
            galaxyZone.PackedContents ??= ZoneGenerator.GenerateZone(
                ItemManager,
                Settings.ZoneSettings,
                CurrentGalaxy,
                galaxyZone,
                IsTutorial
            );
            galaxyZone.Contents = new Zone(ItemManager, Settings.PlanetSettings, galaxyZone.PackedContents, galaxyZone, CurrentGalaxy);
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
            var saved = CultCache.GetGlobal<SavedGame>();
            if (saved == null)
            {
                SectorMap.QueueZoneReveal(CurrentGalaxy.Entrance.AdjacentZones.Prepend(CurrentGalaxy.Entrance));
                PopulateLevel(CurrentGalaxy.Entrance);
                var loadoutGenerator = new LoadoutGenerator(ref ItemManager.Random, ItemManager, CurrentGalaxy, Zone.GalaxyZone, IsTutorial ? CurrentGalaxy.ResolveFaction(Settings.TutorialGenerationSettings.ProtagonistFaction) : null, 2);
                var ship = EntitySerializer.Unpack(
                    ItemManager,
                    Zone,
                    loadoutGenerator.GenerateShipLoadout(data => string.IsNullOrEmpty(Settings.StartingHullName) || data.Name==Settings.StartingHullName ));
                ((Ship) ship).IsPlayerShip = true;
                ship.Position = float3.zero;
                ship.Zone = Zone;
                Zone.Entities.Add(ship);
                ship.Activate();
                BindToEntity(ship);
            }
            else
            {
                foreach(var group in CurrentGalaxy.DiscoveredZones
                    .GroupBy(dz=>dz.Distance[CurrentGalaxy.Entrance]))
                    SectorMap.QueueZoneReveal(group);
                PopulateLevel(CurrentGalaxy.Zones[saved.CurrentZone]);
                var targetEntity = Zone.Entities[saved.CurrentZoneEntity];
                if (targetEntity is OrbitalEntity orbitalEntity)
                {
                    CurrentEntity = targetEntity.Children.First(c => c is Ship {IsPlayerShip: true});
                    DoDock(orbitalEntity, orbitalEntity.DockingBays.First());
                }
                else
                {
                    //StartCoroutine(IntroCutscene(targetEntity as Ship));
                    BindToEntity(targetEntity);
                }
        
                for (var i = 0; i < _actionBarSlots.Count; i++)
                {
                    _actionBarSlots[i].Restore(saved.ActionBarBindings[i], CurrentEntity);
                }
            }
        }
    }

    private IEnumerator IntroCutscene(Ship ship)
    {
        ZoneRenderer.PerspectiveEntity = ship;
        var entityPosition = ship.Position.xz;
        var followOrbit = Zone.Orbits.Keys.MinBy(o => lengthsq(Zone.GetOrbitPosition(o) - entityPosition));
        var followPlanet = ZoneRenderer.Planets[Zone.Planets.FirstOrDefault(p => p.Value.Orbit.Key.Equals(followOrbit)).Key];
        DockCamera.Follow = followPlanet.Body.transform;
        var rootOrbit = followOrbit;
        while (Zone.Orbits[rootOrbit].Data.Parent.IsSet())
            rootOrbit = Zone.Orbits[rootOrbit].Data.Parent.Key;
        var rootPlanet = ZoneRenderer.Planets[Zone.Planets.FirstOrDefault(p => p.Value.Orbit.Key.Equals(rootOrbit)).Key];
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
        var parentOrbit = Zone.Orbits[orbital.OrbitData].Data.Parent.Key;
        var parentOrbitPlanet = Zone.Planets.FirstOrDefault(p => p.Value.Orbit.Key.Equals(parentOrbit)).Key;
        if (parentOrbitPlanet.IsSet() && ZoneRenderer.Planets.ContainsKey(parentOrbitPlanet))
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
        _debugLastShots.Clear();
        if (DebugInfoText != null) DebugInfoText.text = "";
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

        _shipSubscriptions.Add(CurrentEntity.Zone.ShotResolved
            .Where(outcome => outcome.Source == CurrentEntity)
            .Subscribe(outcome => _debugLastShots[outcome.Weapon] = outcome));
        
        FollowCamera.LookAt = ZoneRenderer.EntityInstances[CurrentEntity].LookAtPoint;
        FollowCamera.Follow = ZoneRenderer.EntityInstances[CurrentEntity].transform;
        _articulationGroups = CurrentEntity.Equipment
            .Where(item => item.Behaviors.Any(x => x.Data is WeaponData && !(x.Data is LauncherData)))
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
        RunSave.Clear(CultCache);
        SavePlayerSettings();
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
                    indicator.Value.Place.Target = indicator.Key.Position.ToUnity();
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
                    indicator.Value.Place.Target = indicator.Key.Position.ToUnity();
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
                var sensitivity = PlayerSettings.InputSettings.Sensitivity;
                _entityYawPitch = float2(_entityYawPitch.x + look.x * sensitivity.x, clamp(_entityYawPitch.y + look.y * sensitivity.y, -.45f * PI, .45f * PI));
                _viewDirection = mul(float3(0, 0, 1), CultMath.float3x3.Euler(float3(_entityYawPitch.yx, 0), RotationOrder.YXZ));
                CurrentEntity.LookDirection = _viewDirection;
                HeatstrokePost.weight = saturate(unlerp(0, Settings.GameplaySettings.SevereHeatstrokeRiskThreshold, CurrentEntity.Heatstroke));
                var severeHeatstrokeLerp = saturate(unlerp(Settings.GameplaySettings.SevereHeatstrokeRiskThreshold, 1, CurrentEntity.Heatstroke));
                SevereHeatstrokePost.weight =
                    severeHeatstrokeLerp + severeHeatstrokeLerp * (1 - severeHeatstrokeLerp) *
                    max(Settings.HeatstrokePhasingFloor, sin(Time.time * Settings.HeatstrokePhasingFrequency));
                
                if(CurrentEntity is Ship ship)
                {
                    ship.MovementDirection = Input.Player.Move.ReadValue<Vector2>().ToCultMath();
                }

                var target = CurrentEntity.Target.Value;
                UpdateFireControlDebug(target);
                if (target != null)
                {
                    var threshold = Settings.GameplaySettings.TargetDetectionInfoThreshold;
                    TargetVisibilityFill.fillAmount = lerp(.25f, .75f, (CurrentEntity.EntityInfoGathered[target] - threshold)/(1-threshold));
                    VisibilityToTargetFill.fillAmount = lerp(.25f, .75f, target.EntityInfoGathered[CurrentEntity] / threshold);
                    TargetHitpointsFill.fillAmount = lerp(.25f, .75f, target.Hull.Durability / target.EquippedHull.Data.Durability);
                    TargetShieldsFill.fillAmount = target.Shield == null ? 0 : lerp(.25f, .75f, target.Shield.Progress);
                }

                var tractorPower = Input.Player.TractorBeam.ReadValue<float>();
                CurrentEntity.TractorPower =
                    saturate(CurrentEntity.TractorPower + sign(tractorPower - CurrentEntity.TractorPower) * Time.deltaTime * 2);
            }
            Zone.Update(Time.deltaTime);
        }
    }

    private void UpdateFireControlDebug(Entity target)
    {
        if (DebugInfoText == null) return;

        EquippedItem selectedItem = null;
        Weapon selectedWeapon = null;
        foreach (var item in CurrentEntity.Equipment)
        {
            var weapon = item.Behaviors.OfType<Weapon>().FirstOrDefault(x => !(x is LockWeapon));
            if (weapon == null) continue;
            selectedItem = item;
            selectedWeapon = weapon;
            break;
        }

        if (selectedWeapon == null)
        {
            DebugInfoText.text = "FIRE CONTROL\nNo non-lock weapon";
            return;
        }

        var d = FireControl.Inspect(selectedWeapon, CurrentEntity, target);
        var pendingLine = "pending: none";
        for (var i = CurrentEntity.Zone.PendingShots.Count - 1; i >= 0; i--)
        {
            var shot = CurrentEntity.Zone.PendingShots[i];
            if (shot.Source != CurrentEntity || shot.Weapon != selectedItem) continue;
            if (shot.Committed)
                pendingLine = $"shot {shot.ShotId}: committed {(shot.Outcome.Hit ? "HIT" : "MISS")}";
            else
            {
                var pDeviation = FireControl.DeviationProbability(shot, CurrentEntity.Zone.Time, out var deviation);
                // Cut 12.2 (docs/fire-control-cut.md): retires the inline estimate (shot.PBase * pDeviation),
                // a second, wrong copy of the model now that the target's facing enters at Commit -- this
                // calls the same function Commit itself rolls against (TheHudEstimateIsTheCommitPrice).
                var estimate = FireControl.CommitProbability(shot, CurrentEntity.Zone.Time, out _);
                pendingLine = $"shot {shot.ShotId}: dev {deviation:F1}/{shot.Tracking:F1} x{pDeviation:F3} estimate {estimate:P1}";
            }
            break;
        }

        var lastLine = _debugLastShots.TryGetValue(selectedItem, out var lastShot)
            ? $"last {lastShot.ShotId}: {(lastShot.Hit ? "HIT" : "MISS")} cell {lastShot.Cell.x},{lastShot.Cell.y}"
            : "last: none";
        var gates = target == null
            ? "target: none"
            : $"gates vis {d.Visible} range {d.InRange} arc {d.InArc} lock {d.Locked}";

        DebugInfoText.text =
            $"FIRE CONTROL - {selectedItem.Data.Name}\n" +
            $"{gates}\n" +
            $"range {d.Range:F0} [{d.MinRange:F0}..{d.MaxRange:F0}]\n" +
            $"info {d.Info:F3}/{d.InfoDemandCeiling:F3} sensor {d.PSensor:F3}\n" +
            $"accuracy {d.Accuracy:F3} spread {d.PSpread:F3} hull {d.POnHull:F3}\n" +
            $"precision {d.Precision:F3} tracking {d.Tracking:F1}\n" +
            $"base {d.PBase:P1}\n" +
            $"{pendingLine}\n" +
            lastLine;
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
            TargetIndicator.Target = CurrentEntity.Target.Value.Position.ToUnity();
        var distance = length(ViewDot.Target.ToCultMath() - CurrentEntity.Position);
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
            var showLockingIndicator = targetLock.Lock > .01f && CurrentEntity.Target.Value != null && CurrentEntity.IsHostileTo(CurrentEntity.Target.Value);
            indicator.gameObject.SetActive(showLockingIndicator);
            if(showLockingIndicator)
            {
                indicator.Target = CurrentEntity.Target.Value.Position.ToUnity();
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

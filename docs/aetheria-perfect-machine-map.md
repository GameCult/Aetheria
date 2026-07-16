# Aetheria Perfect Machine Map

Date: 2026-06-13

This is the Proprioception and Imagination pass for turning Aetheria from a
Unity project with ancestral cache/network/UI machinery into a coherent
GameCult machine: typed CultCache state, CultMesh networking, and Eve CultUI
surfaces lowered into Unity without JSON or RethinkDB remaining as live
authority.

## Objective

Aetheria should persist and replicate game state as typed CultCache documents,
publish multiplayer/service state through CultMesh, and render operator/runtime
interfaces from Eve CultUI surfaces. The old JSON, RethinkDB, JsonKnownTypes,
legacy catalog cache, and legacy UI paths should be migration-only or deleted.

## Current Mechanism

- `Aetheria.State.Daemon` is the first-class local Verse daemon host. It opens
  `AetheriaStateNode` with the CultMesh server enabled, serves
  `AetheriaVerseDiscoveryHost`, accepts typed Eve command documents, advances
  authoritative daemon frames through `AetheriaRuntimeDaemonTickRunner`, and
  publishes provider advertisements, daemon health, command boundaries, and
  Eve GUI/TUI witness surfaces. Aetheria clients connect directly to the
  daemon's advertised CultMesh endpoint. Optional external indexes may ingest
  the provider advertisement, but they are not on the client, command, state,
  or cooperating-daemon path. The daemon owns provider state and surfaces;
  Unity and future clients lower or command that provider through CultMesh
  rather than becoming state owners.
- Unity client/runtime lives under `Assets/`. Gameplay now opens the typed
  runtime catalog from `GameData/aetheria-world.cc` and projects current
  `ItemData` DTOs through `ItemManager` over the typed runtime catalog snapshot. `ActionGameManager` no
  longer owns or opens a legacy catalog cache.
  `GameCult.Aetheria.State.Unity`, the embedded Unity package under
  `Packages/org.gamecult.aetheria.state`, owns the Unity runtime path to
  `GameData/aetheria-world.cc` through `AetheriaRuntimeStateBoundary` and owns
  the Unity boot-time state-file existence probe through
  `AetheriaRuntimeStateBoot`. `ActionGameManager` consumes that package-owned
  boot report and throws if `aetheria-world.cc` is missing before gameplay
  boot. When the typed state file exists,
  `ActionGameManager` opens a package-owned read-only runtime catalog snapshot
  from the `.cc` directory store before constructing the temporary legacy
  `ItemManager`. Player settings, loadout edits, shutdown checkpoints, and
  wormhole transitions now flow as typed Eve or daemon command records observed
  by the Aetheria Verse daemon. Run checkpoint documents include the current
  zone plus its runtime entity snapshots. `Aetheria.State` owns publishing
  canonical
  `AetheriaPlayerSettings`, `AetheriaLoadoutTemplate`, `AetheriaRunState`,
  `AetheriaZoneState`, and `AetheriaEntitySnapshot` documents through
  `AetheriaStateNode`. The embedded Unity state package can also read typed
  run, zone, and entity snapshot documents back from the `.cc` store as
  read-only runtime DTOs, including action-bar bindings, faction
  relationships, generation seed, zone entity/orbit/body references, entity
  session scalars, active consumables, behavior progress, weapon state,
  behavior state, and stat-grid rows. Local run saves, loadouts, player settings files, zone
  files, and generated keyboard layout caches no longer write bespoke durable files. The
  legacy
  `RuntimePlayerSettings` runtime object is no longer named or decorated as a
  MessagePack persistence shape. `Aetheria.State.Daemon` now observes typed
  daemon and Eve command records from the Aetheria state graph, accepts them
  through the provider-owned command bridge, and publishes canonical typed state
  while hosting the CultMesh state node. The standalone `Aetheria.State.DrainCommands`
  applicator is deleted so command acceptance remains daemon behavior.
- Shared item domain state is now built around typed item-key references,
  typed runtime catalog rows, direct `RuntimeBehaviorDefinition` behavior
  construction, and runtime geometry/stat primitives. The old `ItemData` DTO hierarchy is deleted
  from live Unity source. Dead
  user-record and galaxy-map-layer catalog roots have been deleted, and
  surviving runtime DTOs no longer carry legacy catalog group/table annotations. Newtonsoft,
  JsonKnownTypes, RethinkDB, LiteNetLib client transport, and the broken
  `Economy.Shared` wrapper have been removed from live source. The stale
  `StrategyGameManager.csbak` backup file and unused Unity asset MessagePack
  formatter classes have also been deleted, so the remaining direct
  `MessagePackSerializer` calls are no longer present in live gameplay source;
  renderer/client code now observes typed `.cc` state records and sends typed
  command records through CultNet/CultMesh boundaries.
- Local legacy catalog data remains in `GameData/AetherDB.msgpack` and
  `GameData/NameFile/*.msgpack` as migration inputs only. Unity gameplay no
  longer opens those MessagePack files at runtime. The old
  `PlayerSettings.msgpack`, `.loadout`, `.zone`, and
  `GameData/KeyboardLayouts/*.msgpack` authority paths are disabled or deleted.
  The old `SavedGame`/`SavedZone` DTOs and `Galaxy` save-loader constructor are
  deleted. The dead `SavedStory` JSON DTO is deleted.
  The unused Unity Localization package and imported Google Sheets localization
  sample have also been deleted, removing the transitive Unity Newtonsoft package
  from the manifest/lock. Unity's built-in `jsonserialize` module and generated
  InputSystem JSON remain engine/package boundaries, not Aetheria state.
  `ZoneConstructionBlueprint`, body/orbit zone runtime data, item-instance runtime
  data, `Ship`, and `EntitySettings` remain as runtime
  construction/loadout/session projections, but no longer use save-file or
  serializer vocabulary or declare themselves as MessagePack persistence
  documents.
- `Aetheria.State` now expands `AetheriaPlayerSettings` beyond an active-run
  pointer into the typed Verse replacement for `PlayerSettings.msgpack`: player
  name, tutorial flag, story-file hash cursors, gameplay formatting, graphics
  preferences, input binding overrides, and action-bar inputs. Unity's menu and
  input screens route edits through named typed Eve commands accepted by the
  daemon, which updates the session `RuntimePlayerSettings` projection as a
  read model. The in-memory projection is not portable state authority.
  Aetheria has its own
  remapping UI that calls Unity's InputSystem at the binding/action layer;
  Unity's generated
  `AetheriaInput` class is the edge consumer of typed binding overrides, not
  the durable owner. The runtime Eve input screen now owns low-level rebinding
  and action-bar input toggles, routing them through named runtime input commit
  methods instead of writing the input collections directly. The
  action bar also uses typed runtime catalog category rows to reject
  non-consumable inventory drops and creates consumable bindings around typed
  catalog rows. Missing typed rows are rejected for consumable binding instead
  of falling through to legacy DTO classification. Slot-binding drag/drop now
  routes through typed daemon operations, and rebuilt current-entity binds come from typed
  action-bar descriptor rows instead of stale slot-local `Entity` references.
  `AetheriaRuntimeConsumableSimulation` is the sole live owner of consumable
  activation and lifetime. It resolves semantic activation intents against the
  typed catalog and the actor's concrete cargo, atomically removes one unit,
  preserves that instance's quality, applies authored stackability, and creates
  the canonical active-effect row. Daemon simulation time ages and expires those
  rows and publishes lifecycle/refusal facts. Input capability projection derives
  only currently activatable consumable actions from this state; Unity's restored
  `ConsumableItemEffect` and action-bar fill are read models and cannot consume
  cargo or advance timers.
  Active effects execute their typed payload array in authored order after
  capacitor/reactor tick initialization. `EnergyDraw` delegates to the shared
  energy ledger and `ItemUsage` delegates to the shared cargo transaction; a
  failed or unsupported payload stops every later payload and publishes the
  exact behavior index and reason. Consumable performance stats preserve the
  fossil's lifetime-effectiveness and quality weighting. Consumable `Heat`
  remains a successful no-op because the fossil explicitly added heat only to
  equipped items; changing that balance requires an explicit design decision.
  Consumable activation, active-duration fill, runtime duration, and effectiveness curves now use typed
  item rows with neutral defaults for missing optional facets. Gear action-bar bindings read custom icon resource
  paths from typed item rows, then use typed weapon and hardpoint facets for
  fallback icon selection, and finally fall back to a generic tool icon when
  typed facets are incomplete. The current legacy catalog has zero populated
  custom action-bar icon paths, but the typed field is present and owns that
  surface when data appears.

  Every active effect owns a stable daemon `EffectId`; each authored payload
  owns a stable `BehaviorId` and nested scalar state beneath that effect. Array
  position is execution order, never identity. `Cooldown` follows the fossil's
  update-before-chain semantics: every cooldown ages before ordered execution,
  readiness is strictly below zero, successful execution resets it to one, and
  an earlier failed behavior cannot freeze it. Unity reconciles active-effect
  presentation objects by `EffectId` and removes absent effects. It does not
  restore active-consumable behavior state through the flattened equipment
  behavior table and cannot become a second cooldown owner.
  Player camera articulation grouping now classifies
  equipped non-launcher weapons through typed behavior kind rows instead of
  inspecting runtime behavior config classes. Unity boot reads typed player
  settings back through the package-owned CultCache reader before falling back
  to defaults. The main-menu settings navigation and input subpages now lower
  through local Eve/UI Toolkit surfaces instead of repopulating the old
  `PropertiesPanel` button shell, and the input page delegates to the live
  runtime remap screen when that owner exists. The fake audio page has been
  deleted until audio has a typed owner. Sector-map zone inspection now lowers
  through a local Eve/UI Toolkit surface as well instead of rebuilding
  `PropertiesPanel` rows on each zone click. The runtime in-game menu tab strip
  now lowers through a local Eve/UI Toolkit surface, with `MenuPanel`
  serializing tab metadata directly instead of routing authority through old
  `MenuTabButton` components. Inventory background ship tuning now lowers through a
  local Eve/UI Toolkit surface instead of rebuilding a `PropertiesPanel`
  float field. The dead generic `PropertiesPanel`/`DropdownMenu` shell is now
  deleted from the main menu, trade, inventory, and map serialized assets
  instead of lingering as inert prefab ballast. Trade target-cargo selection now lowers through a local
  Eve/UI Toolkit surface instead of rebuilding a `ContextMenu` option list.
  Inventory entity/bay/loadout dropdown navigation now lowers through a local
  Eve/UI Toolkit surface instead of rebuilding nested `ContextMenu`
  dropdowns/options.
  Trade filter selection and simple-commodity buy-quantity entry now lower
  through local Eve/UI Toolkit surfaces instead of rebuilding `ContextMenu`
  dropdowns/options in `TradeMenu`.
  Trade typed-item inspection now lowers through a local Eve/UI Toolkit surface
  instead of routing spreadsheet row clicks into `PropertiesPanel.Inspect`.
  Inventory cargo-item inspection now lowers through a local Eve/UI Toolkit
  surface instead of routing cargo clicks into `PropertiesPanel.Inspect`.
  Inventory equipped-item inspection now also lowers through a local
  Eve/UI Toolkit surface, with `InventoryMenu` owning weapon-group toggles,
  action-bar group binding, override shutdown, and thermotoggle target
  temperature controls directly instead of routing them through
  `PropertiesPanel`.
- Main-menu level presentation is now authored as a field-oriented Eve/CultUI
  surface, not as a renderer-owned Aetheria special case. The provider publishes
  `field.surface2d` with embedded document slots for `gravity.height`,
  `nebula.tint`, and object rows. `gravity.height` is a 2D scalar field;
  isolines are one declared visualizer, alongside other possible scalar
  visualizers such as shaded height and probes. `nebula.tint` is a 2D
  vector/color field accumulated from render splats as color times the
  PowerPulse window. The browser lowerer and the Unity client should both lower
  this same field surface. Unity's current `ZoneRenderer`, `PowerBrush`, and
  `Zone.GetHeight` behavior are parity evidence for the new Eve field renderer,
  not a permanent side path.
  Object and body icon selection is provider-owned too:
  `AetheriaRuntimeViewportObject.IconAsset` and
  `AetheriaRuntimeBodyView.IconAsset` carry the CultMesh CDN asset refs. A
  lowering target may draw the advertised asset or expose the missing provider
  data, but it must not synthesize icons, glow, asset paths, gravity fields,
  or backdrop noise as renderer-local compensation.
- The combat schematic HUD uses typed runtime catalog weapon facets for its
  static weapon icon strip; missing typed weapon facets no longer fall back to
  legacy `WeaponItemData`. Schematic weapon-row selection now uses typed
  behavior-kind rows instead of inspecting runtime behavior config classes;
  live weapon behavior instances still own ammo, cooldown, and active firing
  values until those session facts have typed runtime surfaces.
  Hull and item durability percentages use typed max durability only; incomplete
  typed rows use current runtime durability as the generic denominator instead
  of falling back to legacy `ItemData.Durability`. Target HUD hitpoint fill also
  reads target hull max durability from the typed runtime hull row instead of
  `EquippedHull.Data.Durability`.
  Gear heat-fill ranges in the schematic HUD also read typed catalog thermal
  bounds; rows without typed thermal bounds use a neutral current-temperature
  range instead of hydrating legacy `ItemData`.
  Ship thruster VFX emission scaling also uses typed max durability only.
  Runtime weapon behavior still owns ammo counts, active range, cooldowns,
  temperature, and current durability values until those session facts have
  typed runtime surfaces.
- `Aetheria.State` now exposes typed node put/get ports for run state, zone
  state, and entity snapshots. The state smoke writes a run referencing a zone,
  a zone referencing an entity snapshot, and an entity snapshot carrying
  generation seed, position, direction, faction, hull, equipment slots, weapon
  groups, and a stat grid. This proves the `.zone` replacement graph is durable typed state. Unity
  now queues current-zone/current-entity-collection snapshots, current
  action-bar bindings, and current faction relationship rows through the
  runtime commit log during run checkpoints; `ZoneConstructionBlueprint` and
  `EntityConstructionBlueprint` remain one-shot construction/loadout projections
  rather than durable file formats. Runtime blueprints no longer capture or
  restore live temperature, armor, max-armor, or hull-conductivity grids; those
  grids are typed checkpoint state, not loadout/construction template state.
  Blueprint price aggregation uses typed runtime item prices through
  `ItemManager` instead of hydrating `HullData.Shape` or `ItemData.Price`.
  Runtime blueprints no longer capture or restore behavior-private
  `PersistentBehaviorData` blobs; that dead save-shape hook and its
  `IPersistentBehavior` interface are deleted.
- Galaxy generation now exposes the actual nonzero seed used to create topology.
  The main menu passes explicit seeds into normal and tutorial galaxy
  construction, and run checkpoints persist that seed into `AetheriaRunState`.
  Current-zone snapshot restore must attach to a regenerated galaxy with the
  same seed; restoring zone indices against a fresh random galaxy is forbidden
  split-brain state, not a Continue feature.
- Run checkpoint zone snapshots include typed orbit and body rows. Orbit IDs,
  parent IDs, distance, phase, fixed positions, body kind/name/orbit,
  resources, gravity/body multipliers, asteroid belt entries, and gas/sun
  visual parameters now survive through the typed pending commit lane into
  `AetheriaZoneState`. Asteroid belt runtime damage, respawn timers, and
  miner accumulators also persist on typed asteroid rows. `ZoneConstructionBlueprint`
  feeds one-shot generated geometry into `Zone`; live zone radius/mass/orbit/body
  reads come from runtime wrappers and typed checkpoint projection rather than
  a retained construction payload.
- Run checkpoint entity snapshots include typed simulation stat grids for
  temperature, thermal mass, armor, max armor, and hull-conductivity axes.
  They also carry public runtime session state: velocity, target entity
  reference, active flag, heatsink toggle, shutdown override, tractor power,
  heatstroke/hypothermia accumulators, aggregate visibility/source-count state,
  contact rows for gathered information, hostility, and visible classification,
  and active consumable item/timer rows.
  Entity equipment, cargo-bay, and docking-bay slot rows preserve item-instance
  facts needed for restore: item key, quality, durability, quantity, and enabled
  state. Those facts are no longer discarded when pending checkpoint commands become
  canonical `AetheriaEntitySnapshot` documents or package readback DTOs.
  Cargo bay contents, docking bay contents, and docking bay child assignments
  also persist as typed snapshot rows, so inventories and docked-child
  placement do not need a bespoke `.zone` or runtime-blueprint save payload.
  Equipped-item and active-consumable behaviors that intentionally expose
  `IProgressBehavior` now publish typed progress rows keyed by owner kind,
  owner index, behavior index, behavior kind, and progress value.
  Weapon behaviors now also publish typed runtime rows for firing state, ammo,
  burst timing, cooldown progress, charged-weapon charging/charge state,
  constant-weapon reload/ammo-interval state, and lock-weapon lock progress
  plus target entity reference. Authored instant lock behavior evaluates every
  fixed daemon step whether or not that weapon currently owns a trigger or
  burst; trigger selection owns permission to fire, not permission for lock
  state to remain alive. Target changes, invalid targets, acquisition, and
  angular decay commit through the same lock-state path and append stable
  `weapon.lock.started`, `weapon.lock.acquired`, and `weapon.lock.lost` events
  with prior/current progress. Eve projects those events generically while the
  continuous combat presentation remains the reticle's state owner.
  Sensor, radiator, reactor, and capacitor behaviors publish typed state rows
  for ping state, radiator temperature/throughput, reactor draw/load, and
  capacitor charge/capacity/efficiency. Aether drives publish typed axis,
  thrust, RPM, maximum RPM, and thrust-direction rows, so rotor simulation
  state is no longer trapped inside live behavior memory. Resource scanners
  publish target body id, asteroid index, scan timer, range, minimum density,
  and scan duration rows. Mining tools publish asteroid belt id, asteroid
  index, and evaluated range rows. Thrusters publish analog axis input,
  evaluated thrust, and torque rows. Shields publish evaluated efficiency and
  energy usage, velocity limiters publish evaluated limit, and thermotoggles
  publish their active target temperature. Switches publish activated state,
  triggers publish pending pulled state, and stat modifiers publish
  applied/executed flags plus target-stat count. Turret controllers publish
  initialized weapon count, shot speed, and predictive-aim flag. Continue
  restore lowers those typed weapon and behavior rows back into the live
  equipped-item and active-consumable behavior instances through narrow
  behavior-owned restore methods; construction/loadout blueprints do not own
  behavior replay.
- Passive contact reach is daemon-owned and derived from enabled installed
  `Sensor` behavior sensitivity. Arrays combine by root-sum-square, so larger
  hulls can buy reach by devoting real equipment volume, mass, power, cooling,
  and computation to sensing. Entity kind is not an input. Cargo is not
  installed equipment. Hull-authored `Sensors` hardpoints own the physical
  installation envelope: a smaller same-type array may occupy a larger mount,
  while generated loadouts choose the largest compatible footprint before
  applying faction, distance, and price weighting. Station-scale reach emerges
  from station-scale hardpoints and gear rather than a station multiplier or
  sensor-count branch. Contact refresh consumes this derived reach but does
  not write `Visibility`; emitted signature remains owned by thermal,
  propulsion, weapons, reflectors, explicit visibility sources, and pings.
- The daemon observation query owns docked sensor sharing. A docked controlled
  craft's effective contact picture merges its own contacts with the dock
  parent's contacts, retains the primary sensor source on every projected row,
  and resolves duplicate targets to the strongest gathered information.
  Eve contact documents and SoA render bodies consume this same query; runtime
  clients do not discover docking relationships or union sensor state.
  `EntityConstructionBlueprint` still exists for construction/loadout projection,
  but those live hull grids and session scalars are not blueprint fields and
  cannot be restored by the old blueprint projector path.
- `Aetheria.State` now defines `AetheriaLoadoutTemplate` as the typed Verse
  replacement for bespoke `.loadout` files. It stores structured hull,
  equipment, cargo bay, docking bay, child-entity, assignment, and weapon-group
  state through record-key references and typed value slots instead of opaque
  runtime blueprint serialization. Unity's save/loadout UI now projects its
  in-memory `EntityConstructionBlueprint` into a typed Verse commit command, and
  gameplay boot reads typed loadout templates back from `aetheria-world.cc`
  through `AetheriaRuntimeCatalogStore` before lowering them into runtime
  construction blueprints for the restore menu. The in-memory list remains a
  UI/session cache, not durable authority.
- `ActionGameManager` opens `AetheriaRuntimeCatalogStore` over
  `aetheria-world.cc`, hands the typed runtime catalog snapshot directly to
  `ItemManager`, and gives it explicit item lookup authority. Item instances carry
  `AetheriaRuntimeItemReference`, a typed item key, not a process-global catalog
  resolver, hydrated projection cache, bare GUID owner, or durable item-data owner. The old
  `LegacyItemCatalogBoundary`,
  `LegacyItemCatalogCache`, and runtime MessagePack deserializer path have been
  deleted. The old `DatabaseEntry`/`RuntimeCatalogEntry`/`RuntimeItemProjectionEntry`
  identity base has been deleted from live source, and the old generic
  `DatabaseLink<T>`/`RuntimeCatalogLink<T>` path has collapsed into the
  item-specific runtime reference.
- `ItemManager` no longer exposes the raw runtime item catalog reader as public
  gameplay/UI API, and its old `GetData`/`Hydrate` item DTO projection path has
  been deleted. `ItemManager` no longer routes through a duplicate
  `AetheriaRuntimeItemCatalog` cache; it reads typed item rows from the shared
  runtime catalog snapshot only. The
  temporary behavior config bridge has moved under `ItemManager` behavior
  construction. The item properties UI no longer uses
  `ItemManager` for manufacturer display; it resolves the manufacturer through
  the package-owned `ActionGameManager.RuntimeCatalog` typed snapshot. Entity
  restore and loadout manufacturer-distance weighting no longer use `ItemManager`
  for faction lookup; they resolve factions through the `Galaxy` typed
  corporation projection. There is no live console item-spawn command; future
  operator item actions should be typed command documents or Eve/CultUI
  commands, not in-client catalog hydration shortcuts. `LoadoutGenerator` also
  receives the typed runtime catalog and uses typed candidate-kind selectors
  for item selection and instantiation. Hull type, hardpoint type, shape fit,
  station bay fit, hull/category, and behavior-kind prefilters run against typed
  catalog rows. `EquippedItem` and `ConsumableItemEffect` now create behavior
  instances through `ItemManager.CreateRuntimeBehaviors`, which switches on
  stable typed behavior kind and constructs live behavior classes directly from
  `RuntimeBehaviorDefinition`; layout metadata no longer selects or mutates
  runtime behavior configs.
  `StatModifier` requirement and stat-target lookup now inspects
  the live equipped behavior instances and asks package-owned typed behavior
  metadata for kind/family matching, not a freshly rebuilt config list or a
  behavior-local class taxonomy. Live behaviors no longer expose their
  construction config object as a public runtime authority; stat targeting now
  crosses a behavior-owned stat lookup. The unused `TradeMenuDebug` script has been
  deleted instead of preserving an old uGUI debug path that
  turned typed trade rows back into legacy `ItemData` objects. That hydration
  now comes from typed state, not `AetherDB.msgpack`. The surviving trade menu
  wraps rows in a typed `TradeRow`: name, mass, price, size, hardpoint type,
  commodity subtype, hull ownership, and behavior-kind filters read typed
  catalog rows before any legacy projection is hydrated. Active behavior
  filters store typed behavior kind keys and match typed behavior payloads
  through package-owned typed behavior metadata, including parent-kind matching
  for behavior families. PropertiesPanel and TradeMenu display metadata are now
  package-owned typed behavior metadata. The obsolete `RuntimeInspectable`
  attribute has been deleted from behavior classes and fields.
  Dynamic behavior columns read typed behavior payload fields by legacy payload key
  instead of hydrating legacy behavior DTOs. Trade buy decisions for crafted price,
  ship-hull classification, simple commodity base price, and simple commodity
  stack size also read typed catalog rows; the existing inventory transfer and
  ship-construction paths still own the actual runtime mutation.
  The sector properties UI also resolves
  station/turret/ship counts only through typed hull classifications from the runtime catalog;
  missing typed hull rows no longer fall back to legacy `HullData`. Loot pickup
  presentation now uses typed catalog category/name rows to choose weapon-vs-gear
  pickup visuals and scan labels; missing typed rows degrade to generic unknown
  pickup presentation instead of falling back to legacy item projections.
  Zone entity scene instantiation also reads typed hull prefab paths and hull
  classifications from the runtime catalog; missing typed hull prefab rows fail
  loudly instead of asking legacy `HullData` to choose the scene prefab.
  Entity scene instances use typed hull shape cells and typed hardpoint rows for
  collision damage masks and weapon-barrel presentation setup; behavior effect
  prefabs still come from behavior DTOs until behavior construction/execution
  moves to typed payloads.
  Inventory panel equipment-grid geometry, hull/interior cell drawing,
  temperature overlay masking, thermal edge toggles, and entity cell tinting now
  use typed hull shape/interior masks from the runtime catalog. `EquippedCargoBay`
  now reads cargo interior shape, item fit shape, simple commodity max stack,
  cargo bay mass, and cargo bay thermal mass from typed runtime catalog rows;
  `InventoryPanel` lowers cargo-grid display from that typed `InteriorShape`.
  Final equipment fit/equip acceptance also reads typed item shape, hardpoint
  type, hull shape, hull interior, hardpoint masks, cargo/docking categories,
  and mass/thermal mass from runtime catalog rows. `Entity.MapEntity`,
  `Entity.UnoccupiedSpace`, grid-offset placement, and `Entity.UpdateTemperature`
  also read typed hull shape/interior, hardpoints, base armor, mass, specific
  heat, and conductivity from runtime catalog rows. Entity construction now
  names entities from the typed hull row and no longer keeps a `HullData`
  property just to preserve the old naming bridge. `LoadoutGenerator`
  candidate weighting, hardpoint/cargo/capacitor fit, selected-item reuse, and
  hull conductivity setup now use typed runtime catalog rows. Generated loadout
  item birth also uses a typed-row instance primitive, so selected catalog rows
  no longer hydrate `EquippableItemData` just to create equipment. Docking bay
  max ship size is imported as typed item state and `EquippedDockingBay.MaxSize`
  derives from the typed runtime catalog row. `EquippedItem.InsetShape` now
  derives from typed hull/item shape rows. `EquippedItem` and
  `ConsumableItemEffect` behavior construction now read typed behavior payloads
  through `RuntimeBehaviorDefinition` before instantiating the current behavior
  classes. The old behavior config bridge is deleted; typed behavior kind and
  group flow directly into live behavior instances.
  Base `Behavior` now captures a runtime performance-stat table during
  construction and no longer retains the config object for later stat lookup.
  Heat, EnergyDraw, and Cooldown behavior instances now copy constructor
  stat/scalar inputs into explicit runtime fields and no longer retain their
  config subclasses after construction.
  Wear, Visibility, Reflector, VelocityLimit, VelocityConversion, and ItemUsage
  now follow the same rule for their constructor stat/key/scalar inputs.
  Capacitor, Shield, Thruster, MiningTool, ResourceScanner, Thermotoggle,
  Switch, HeatStorage, and Cockpit have also dropped runtime config retention;
  Thermotoggle adjustability is now owned by the live behavior instance.
  Reactor, Radiator, Sensor, and StatModifier now copy constructor performance
  stats, curves, flags, and target strings into explicit runtime fields instead
  of retaining config subclasses.
  AetherDrive now copies rotor geometry, performance stats, torque curve, audio
  parameters, and prefab path into explicit runtime fields. Weapon,
  InstantWeapon, ConstantWeapon, ChargedWeapon, and LockWeapon now copy
  constructor stats, curves, ammo/reload fields, guided projectile profile
  fields, burst/cooldown fields, charge multipliers, and lock parameters into
  runtime-owned fields. Behavior instances no longer retain construction config
  DTOs after construction.
  Cockpit, HeatStorage, Switch, Trigger, and TurretController read directly from
  typed runtime behavior definitions.
  Heat, EnergyDraw, Cooldown, Wear, Visibility, Reflector, VelocityLimit,
  VelocityConversion, and ItemUsage also bypass the temporary config bridge and
  read typed behavior fields through `RuntimeBehaviorDefinition`; stat-bearing
  behaviors register their runtime performance stats explicitly for
  `StatModifier` rather than relying on config reflection.
  Capacitor, Shield, MiningTool, ResourceScanner, Thermotoggle, and Thruster now
  follow the same direct-definition path.
  Reactor, Radiator, Sensor, StatModifier, and AetherDrive also read typed
  payload fields through `RuntimeBehaviorDefinition`; their live stat surfaces
  are registered explicitly rather than recovered through config reflection.
  Weapon, InstantWeapon, AutoWeapon, ConstantWeapon, ChargedWeapon, LockWeapon,
  Launcher, and GuidedWeapon payloads also construct directly from
  `RuntimeBehaviorDefinition`; the old `RuntimeBehaviorConfig`,
  `BehaviorPayloadReader`, and weapon `*Config` classes are deleted from live
  source.
  `StatModifier` behavior requirements and behavior-stat targets compare typed
  behavior kinds on the live equipped behavior instances through
  `AetheriaRuntimeBehaviorMetadataCatalog` instead of scanning config
  subclasses, rebuilding temporary configs, or hydrating
  `EquippableItemData.Behaviors`. `ConsumableItemData` and
  `EquippableItemData` no longer expose legacy `Behaviors` lists at all. The
  old item-DTO stat branch was deleted after inspection showed no active
  `PerformanceStat` fields on the live equippable item DTO hierarchy.
  Ship drag, combat/turret shot prediction height, and thruster
  torque geometry now consume typed hull facets and typed shape masks.
  The item properties panel also reads typed catalog title names, descriptions,
  manufacturer, base mass, max durability, thermal bounds, and thermal
  performance curve keys for basic item presentation without hydrating legacy
  DTOs. Runtime behavior stat display reads typed behavior payload fields
  through package-owned typed behavior display metadata; stat modifier
  presentation checks the package-owned typed `StatModifier` kind instead of a
  legacy data class name. Damage-range payload selection is keyed by
  package-owned typed weapon behavior metadata; legacy behavior config ancestry
  is no longer a fallback for weapon payload selection. The old explicit
  `Inspect(ItemData)` DTO inspection overload is deleted; trade row clicks now
  inspect the typed runtime catalog row directly. Incomplete typed thermal
  curve rows render a neutral fallback curve instead of hydrating legacy
  `EquippableItemData`. Inventory selection highlighting now uses typed item
  shape cells only for UI tint geometry; incomplete typed rows produce no
  selected-cell mask instead of falling back to legacy `ItemData.Shape`.
  Inventory cargo cell tint now uses typed hardpoint facets only; incomplete
  typed rows receive the generic tint instead of falling back to legacy
  `EquippableItemData.HardpointType`. Inventory HUD durability tint uses typed
  max durability only, with current runtime durability as the generic
  denominator for incomplete typed rows. `ItemManager.Evaluate` also uses typed
  runtime item max durability and item names when evaluating unequipped
  performance stats. Weapon group assignment and guided projectile diagnostics
  report names from equipped typed runtime rows instead of `ItemData.Name`.
  `ItemManager` typed item instantiation primitives create
  simple commodities, crafted items, and equippable items from runtime catalog
  rows; old DTO-based item creation is gone from blueprint cloning.
  `EquippedItem` also takes conductivity, max durability, thermal bounds, and
  thermal performance curve keys from the typed runtime item row for heat
  conduction, durability performance, thermal performance, and wear scaling.
  Thermal resilience is also typed and feeds wear scaling. Audio parameter stat
  bindings are typed item rows and feed equipped-item Wwise parameter updates;
  missing optional thermal/audio facets now degrade to neutral runtime defaults
  instead of hydrating legacy equippable DTOs. Consumable stackability, duration, and
  effectiveness curve slots are part of typed item rows, and action-bar
  consumable activation/fill now command typed item IDs. The current mapped
  legacy catalog has no consumable item rows, so consumable runtime behavior
  still needs future typed data coverage, but it no longer hydrates
  `ConsumableItemData` as a fallback.
  Inventory drag preview occupancy now projects typed item shape cells only into
  the local `Shape` grid, and final fit/equip acceptance shares the typed
  runtime catalog geometry path in `Entity.ItemFits` and `TryEquip`.
  Action-bar gear slots now resolve custom icon resource paths from typed
  runtime item rows before falling back to typed weapon/hardpoint facets; legacy
  `EquippableItemData.ActionBarIcon` is no longer a UI owner.
- `Galaxy` generation no longer accepts a runtime item catalog reader or `ItemManager`.
  Sector and tutorial generation receive the package-owned typed runtime
  catalog. `Galaxy` projects typed corporation v2 records into temporary legacy
  `Faction` DTOs for the existing simulation shape, but allegiance edges are
  typed-key-only through `AllegianceByKey`; the old GUID allegiance dictionary
  and GUID faction lookup helpers are deleted. Galaxy resolves full name arrays
  from `aetheria.name_file.v2` records. The
  runtime no longer opens the old `GameData/NameFile/*.msgpack` directory.
- Item instances carry `AetheriaRuntimeItemReference`, a typed item key resolved
  by `ItemManager` against the typed runtime item catalog projected
  from `aetheria-world.cc`. Runtime item references no longer carry hydrated DTO
  projection caches or global link-resolution authority. MessagePack catalog construction no longer grabs global link-resolution
  authority, and the old generic `RuntimeCatalogLink<T>` abstraction is gone.
- `Aetheria.State.Daemon` starts the modern `Aetheria.State` CultMesh node as
  the first-class Verse daemon and no longer shares authority with the old
  RethinkDB/LiteNetLib server.
- `Aetheria.State.Import` writes a typed quarantine manifest and migration
  ledger for the legacy catalog files. It also raw-decodes stable old
  MessagePack union fields into typed item/faction/name-file documents without
  compiling the old Unity domain model into `Aetheria.State`. A full import
  writes those documents into one monolithic, tracked `.cc`; ignored
  `.cc.records` directories are runtime materializations and cannot be required
  to open a fresh clone. The current checked-in catalog maps to 116 item
  definitions, 12 factions, and 12 name files. Item definitions now carry
  legacy manufacturer IDs, price, shape
  dimensions, occupied cell counts, full typed shape-cell masks, hardpoint
  type, hull type, interior shape masks for hull/cargo equipment, hull
  hardpoint definitions, behavior kind fingerprints, typed recursive behavior
  payloads, stack size, specific heat, conductivity, hull grid offset, hull
  armor, hull drag, hull towing flag, durability, and weapon
  range/caliber/type/fire/modifier classifications. Corporation documents now carry the legacy short name, true
  description from key 3, name-file and boss-hull legacy IDs, influence
  distance, allegiance count, full allegiance edges, and music bank IDs. Empty
  legacy GUID references are imported as absent links rather than as
  `Guid.Empty` catalog IDs.
  Name-file documents are now `aetheria.name_file.v2` records and carry both
  sample names for compact surfaces and the full legacy name array for future
  `Galaxy`/Markov name generation cutover. Corporation documents are now
  `aetheria.corporation.v2` records because runtime generation needs actual
  allegiance edges, not only a count. `Aetheria.State` now owns the canonical
  legacy-ID record key mapping for migrated item, corporation, and name-file
  documents.
- `AetheriaCatalogSnapshot` is the typed catalog read surface over materialized
  `.cc` records. It exposes trade-item, typed
  manufacturer/corporation/name-file key queries, corporation prefix queries,
  and legacy-ID migration lookup, plus equipment, hardpoint, and behavior
  queries without touching runtime projection DTOs.
- `ItemManager` can now ask its typed runtime item reader for the owning
  `AetheriaRuntimeCatalogItem` row by item ID. Its generic mass, thermal-mass,
  and crafted-price helpers read typed mass, specific heat, quantity, quality,
  and price rather than hydrating `ItemData`; legacy projection hydration is
  still used for behavior construction and simulation surfaces that have not
  been rebuilt yet.
- The embedded `GameCult.Aetheria.State.Unity` package now owns the
  Unity-visible immutable catalog read-model contract for
  trade/equipment/behavior/hardpoint/manufacturer/corporation/name queries,
  typed item shape masks, typed interior masks, hardpoints, and behavior
  payloads. It also owns a read-only Unity-compatible CultCache directory-store
  opener for the known Aetheria catalog schemas, so Unity runtime code can read
  `aetheria-world.cc` catalog records without runtime projection DTOs or a MessagePack
  catalog cache. The SDK-style `Aetheria.State.Unity` facade includes
  that package source and owns the full `Aetheria.State` mapper plus Eve surface
  read path for .NET smokes. Neither is a simulation owner and neither writes
  state.
  `Aetheria.Shared.Unity` references this package directly so `Galaxy` can
  consume typed corporation and name-file keys without loading legacy `Faction`
  or `NameFile` documents. The
  package also owns Unity's typed daemon/Eve command helpers for settings,
  loadout-template, and runtime requests. These helpers are transport-only:
  they cannot decide canonical state, and the Verse daemon accounts accepted
  command records after publishing typed documents. Run checkpoint documents now
  carry current-zone and entity snapshots so the old `.zone` file path has a
  live typed runtime projection path.
- `AetheriaRuntimeEveCommands` is the renderer-facing typed command helper. The
  internal Eve command path persists typed `AetheriaRuntimeEveCommandDocument`
  records; `AetheriaEveCommandBridge` consumes the same shared document type,
  validates provider/surface/command authority, and deletes accounted command
  records. The Eve command lane is transport, not renderer-owned state.
- `Aetheria.State.Daemon` hosts the CultMesh Verse member and owns long-running
  Eve command acceptance for provider-owned surfaces. `--once` runs one daemon
  tick for smoke/operator use without keeping the process alive.
- `AetheriaEveCommandAcceptanceStatus` and the `aetheria.operations` Eve surface
  publish Eve request acceptance health, observed depth, applied counts,
  failures, and timestamps as typed state. Console logs are notification-only.
- Daemon Verse/service identity now lives in typed
  `aetheria.verse_host_settings.v1`, owned by the Aetheria daemon. Provider
  advertisement and operations telemetry project from that document, so public
  or private Verse exposure and seed-galaxy Verse routing no longer hide in
  hardcoded projector strings.
- `AetheriaPlayerSettingsSurfaceProjector` now emits a provider-owned
  `aetheria.player_settings` Eve surface from canonical `AetheriaPlayerSettings`
  state. Gameplay and graphics settings are exposed as typed read models plus
  narrow typed command buttons, not renderer-local fields.
- `AetheriaProviderAdvertisementProjector` publishes
  `gamecult.eve.provider_advertisement.v1` for the `aetheria` provider,
  advertising the shared runtime Eve surface catalog, command boundaries,
  schemas, and `.cc` witness path. Its Verse/service identity now comes from
  typed daemon host settings instead of hardcoded projector fields. This is the
  discovery map for Odin/Eve, not a health page.
- `AetheriaRuntimeEveSurfaceCatalog` is the single inventory of known Aetheria
  Eve panels. The daemon advertisement and Odin-facing provider advertisement
  both derive from it, so daemon GUI/TUI records, operator records, menu shells,
  inventory/trade panels, contextual inspectors, and designer previews are
  visible without making Unity or Electron the owner.
- `AetheriaCatalogSurfaceProjector` now emits the first provider-owned Eve
  surface from typed catalog state. The importer materializes a
  `gamecult.eve.surface.v1` catalog operator document at
  `eve:surface:aetheria.catalog.operator` with summary, trade-catalog, and
  corporation views. This is a typed CultCache surface document, not a renderer
  dashboard or JSON status card.
- The embedded Unity state package can now read `gamecult.eve.surface` records
  directly from the CultCache `.cc` store into the shared Eve surface contract
  DTOs. This gives Unity the same provider-owned retained tree that the
  SDK-style state client reads, without JSON fixtures, HTTP dashboard
  summaries, or renderer-owned state.
- `AetheriaEveSurfacePresenter` is the first runtime UI Toolkit consumer of
  those typed surface documents. It owns mounting only: state file resolution,
  surface lookup, lowering, and command emission into a typed pending queue.
  Provider command acceptance belongs to `AetheriaEveCommandBridge`, which
  consumes typed Eve command documents from the CultCache/CultMesh state node.
- `AetheriaEveRuntimeBootstrap` mounts the first runtime Eve surface after
  scene load. The default surface is `aetheria.operations`, hosted in a
  runtime-created `UIDocument`; environment variables or a command-line switch
  can redirect/disable the mount for diagnostics. Batchmode disables the
  bootstrap so verification runs do not create renderer state.
- `GameData/aetheria-world.cc` is now materialized from the importer as the
  self-contained project-local typed state file for the checked-in catalog. The
  importer stores
  relative provenance in the state document, not machine-local absolute paths.
  Unity resolves this path through the embedded `GameCult.Aetheria.State.Unity`
  runtime package, not through the legacy catalog boundary. Unity also asks the
  package to inspect whether the typed state file exists during boot; the
  warning is notification-only and does not make legacy catalog data the typed
  state owner.
  Import rebuilds clear the generated `.cc`, `.cc.records`, and `.cultmesh`
  outputs for the selected state path after capturing legacy inputs, then writes
  the release artifact with the monolithic store. Schema evolution can
  rematerialize typed state instead of being blocked by stale embedded schema
  catalogs. Mutable runtime nodes may materialize their private directory-store
  overlay after deployment; that overlay is never release authority.
  `Aetheria.State.Verify` copies the tracked monolith into a fresh temporary
  directory before opening it and checks that migration
  ledger counts match actual typed catalog records, that name-file v2 records
  carry their full name arrays, that the legacy-ID lookup API resolves migrated
  item, corporation, and name-file documents, and that the expanded typed
  catalog facts, typed catalog snapshot queries, and typed Eve catalog surface
  are present in the `.cc` store. The current verifier sees 52 shaped item
  masks, 5 interior masks, 3 hardpoint-host hulls, 22 hardpoints, 39
  behavior-bearing item definitions, 65 behavior payloads, 661 behavior fields,
  18 behavior-side legacy-ID references, 51 hardpoint-tagged equipment items, 3
  hull items, 18 weapon-facet item definitions, and 143 corporation allegiance
  edges. Legacy content includes at least one overhanging hardpoint
  (`LonginusX`); migration verification
  preserves that payload and does not convert content repair into mapper
  authority.
- `.voidbot/state/aetheria.cc` is the repo Persona state witness. Aetheria is
  not registered as a VoidBot Discord identity yet, so mutations use the
  repo-local `void-self-state.mjs apply-operation` typed boundary rather than
  the registered Face MCP path.
- The old IMGUI DB inspector has been deleted. `NameTools` still
  clean/generate names, but it now uses a UI Toolkit editor shell, and the
  legacy NameFile `.msgpack` export control has been deleted instead of leaving
  a warning-only writer stub. The
  remaining Unity helper files formerly under `Assets/Scripts/CultCache` now
  live under `Assets/Scripts/UnitySupport` because they are color/curve helpers,
  not cache authority.
- Galaxy generation no longer reads legacy `Faction` or `NameFile` entries
  through `ItemManager`; it requires the typed runtime catalog opened from
  `GameData/aetheria-world.cc`.
- The dead Unity `NameFile` projection class has been deleted. Runtime name
  generation reads `AetheriaRuntimeNameFile` records from the typed catalog
  facade; legacy `GameData/NameFile/*.msgpack` remains migration input only.
- Pending Unity runtime commit envelopes now carry typed item keys only for
  loadout items, action-bar targets, body resources, entity hulls, and active
  consumables. The older `ItemDefinitionLegacyId` and
  `HullItemDefinitionLegacyId` fields have been deleted from the pending commit
  contract; the applier no longer reconstructs item keys from legacy item GUIDs.
  Corporation legacy ID fallback fields are also deleted from runtime
  commit DTOs; current commands must publish typed faction keys directly.
- Entity restore and loadout manufacturer-distance weighting no longer read
  legacy `Faction` entries through `ItemManager`; they use `Galaxy.ResolveFaction`
  over the typed corporation projection.
- The `give` command no longer enumerates legacy item catalog entries; typed
  catalog selection owned the command match before the command was deleted.
- `LoadoutGenerator` no longer enumerates legacy item catalog entries for its
  candidate pool; typed catalog filtering owns the first item selection pass,
  and typed runtime item rows are passed directly to `ItemManager` for
  instantiation. The remaining post-selection bridge is behavior config
  construction for old behavior constructors, not whole-item DTO hydration.
- `TradeMenuDebug` no longer enumerates legacy item catalog entries for its
  table rows; typed trade catalog records own the row set, with legacy DTO
  hydration kept for the old debug UI callbacks.
- MessagePack is no longer used as a runtime object-cloning shortcut for
  `EntitySettings`, and UI/player-settings startup no longer registers the old
  MessagePack resolver. The custom Unity-side MessagePack resolver and math/type
  formatter source has been deleted with the old bespoke file format path.
  Unused Unity asset formatters for GameObject, Material, Sprite, and Texture2D
  are deleted. Keyboard layout DTOs are plain runtime parse/display models, not
  MessagePack save shapes. `PlayerSettings` is a plain session-local runtime
  object until typed Verse settings are imported into Unity. `ZoneData` body/orbit DTOs, `ItemInstance` DTOs,
  `EntitySettings`, `Ship`, Unity inspector game settings, environment/volume
  settings, exponential curves, Wwise bindings, and enum dictionaries are also
  plain runtime/session/config projections now, not MessagePack persistence
  shapes. The global scene-load MessagePack resolver hook is deleted. The slime
  compute settings path no longer serializes its local parameter struct for
  change detection. Agent task runtime shapes no longer carry MessagePack
  object, union, key, or ignore metadata. `AgentTask` no longer inherits the
  shared runtime projection identity base; task objects are plain in-memory AI
  work orders until a typed Verse task document exists.
- Legacy player, faction, name-file, galaxy-map-layer, and narrative helper
  DTOs no longer carry MessagePack object/key/ignore metadata. Typed
  corporation/name-file/catalog documents own durable state; these classes are
  inspector/runtime projections only. Item and behavior DTO field layout for
  `AetheriaRuntimeItemCatalog` no longer depends on live Unity field
  attributes; importer key maps, typed payload fields, and explicit runtime
  mappers own migrated layout.
- The dead `PersonalityAttribute` projection DTO and its unused property-panel
  hook have been deleted; no typed catalog import or runtime caller owned that
  shape.
- Declaration-only personality fields on `Faction`/`Entity` and the unused
  production personality setting have also been cut; there is no surviving
  personality-state owner in live Unity source.
- `AgentTask` has been cut loose from the item projection base; AI tasks are
  local runtime work orders and no longer participate in the shared projection
  identity base.
- `BodyConstructionData` and `OrbitConstructionData` have also been cut loose from
  the item projection base; they keep local GUID fields for zone runtime lookup,
  but no longer inherit shared projection equality.
- `Faction` has been cut loose from the item projection base; it keeps local
  GUID equality for galaxy dictionaries while typed corporation records remain
  the durable catalog owner.
- `Aetheria.Shared.Unity` no longer references the vendored `MessagePack`
  assembly. The embedded `GameCult.Aetheria.State.Unity` package still depends
  on MessagePackReader to open CultCache `.cc` records; that dependency belongs
  to the typed state package boundary until it can move to a modern CultLib
  Unity package.
- The dead story compiled-JSON cache sketch and its SHA helper are deleted;
  story compilation currently reads Ink source directly until a typed Verse
  story/cache document exists.
- Runtime UI still contains old Unity UI/uGUI prefabs plus `MonoBehaviour`
  scripts under `Assets/Scripts/UI/` and `Assets/Prefabs/UI/`, but the old
  renderer-local debug console and field-tester panel authorities have been
  deleted. Runtime UI commands must be provider-advertised Eve command
  documents, not text parsed by a Unity component or a test-scene uGUI panel and
  invoked directly against gameplay objects.
- Aetheria already has Unity UIElements support available through
  `com.unity.modules.uielements`. It now imports
  `org.gamecult.eve.surface` and `org.gamecult.eve.unity-uitoolkit` from the
  neighboring Eve repository:
  the first carries no-engine `gamecult.eve.surface.v1` contract DTOs, and the
  second lowers those retained trees into UI Toolkit `VisualElement` trees.
  These packages are renderer boundaries, not UI truth owners.
  `org.gamecult.aetheria.eve-runtime` now adds the Aetheria-specific
  `UIDocument` presenter that reads provider-owned Eve surfaces from
  `GameData/aetheria-world.cc` and mounts them through the lowerer. Its command
  path sends typed `gamecult.eve.command.v1` records to the provider-owned
  command bridge instead of accepting renderer-local command effects.
  `AetheriaEveCommandBridge`
  currently accepts the advertised catalog/operations refresh commands plus the
  first provider-owned player-settings mutation commands, republishes
  provider-owned surfaces, rejects unknown/unadvertised commands, and records
  `AetheriaEveCommandAcceptanceStatus` as typed state.

## Invariants

- Durable game state is typed CultCache state, not JSON documents or ad hoc
  sidecars.
- Network-visible state is published through CultMesh/CultNet documents, not a
  RethinkDB changefeed or bespoke status endpoint.
- Record identity belongs to CultCache record keys and typed references, not to
  mutable domain base classes.
- UI surfaces are provider-owned Eve CultUI documents; Unity is a lowering
  runtime, not the owner of UI truth.
- Migration code may read old formats, but old stores cannot decide live state
  after migration.
- The runtime must have one commit path for user action, load/import, server
  replication, and editor mutation of the same state.

## Current Authority Map

### Direct CultMesh Session And Peer Transport

- Owner: each Aetheria daemon owns its advertised client CultMesh endpoint and
  its explicitly configured direct daemon-peer endpoints.
- Inputs: typed client sessions, commands, snapshot requests, committed facts,
  and optional `--peer-cultmesh-endpoint` routes. Odin publication is disabled
  unless an operator explicitly supplies `--odin-cultmesh-uri`.
- Outputs: typed state/surface snapshots, receipts, provider-owned assets, and
  policy-filtered committed-fact convergence between directly connected
  daemons.
- Derived state: discovery catalogs and external provider indexes may mirror
  advertisements, but they do not grant authority or mediate an Aetheria
  session.
- Forbidden writers: Odin, Eve lowerers, Unity, and remote peers cannot apply a
  fact directly. Imported facts pass the daemon authority policy and execute
  through the daemon's required Ymir world-physics owner.
- Shared paths: local commands and accepted remote facts enter the same daemon
  command transaction and receipt chronology.
- Cut line: the rejection that forced `--peer-cultmesh-endpoint` through Odin
  is deleted. Direct CultMesh is the Aetheria path; Odin is optional external
  indexing only.
- Verification layer: `Aetheria.State.AuthoritySmoke` boots real child daemons,
  connects replicas directly, proves accepted/rejected policy outcomes, and
  proves committed-fact convergence with production Ymir physics injected.

### Daemon Combat Lifecycle

Weapon admission, pending trigger, progressive target lock, burst/reload/charge
state, deterministic shot resolution, and transition chronology live in the
public daemon run checkpoint. A restart restores that exact checkpoint rather
than reconstructing combat from client input or replaying presentation events.
Daemon smoke now persists a partial instant-weapon lock, reopens it, and proves
the pending trigger completes exactly one shot with one `weapon.lock.started`,
one `weapon.lock.acquired`, and one `shot.committed` event. Ymir reconstructs
process-local bodies from committed world state; it does not own weapon
lifecycle state.

### Daemon Damage Transaction

- Owner: the daemon's canonical damage transaction owns damage-layer ordering
  and mutation for direct weapons and deployables.
- Inputs: hull and hardpoint catalog topology; initialized armor and
  maximum-armor grids; shield, equipment, and scalar hull state; deterministic
  impact position/direction; damage amount, orthogonal spread, 0.5-cell
  penetration, and typed damage type.
- Outputs: one atomic `shield -> armor cell -> equipment -> scalar hull`
  result, mutated canonical grids/equipment/hull state, receipts recording
  which layers were reached, and one `equipment.destroyed` fact whenever an
  installed item crosses the fossil durability threshold.
- Derived state: aggregate armor and exact schematic armor/maximum-armor facts
  are Eve projections of canonical cell state. Scalar hull is the terminal
  remainder, not an armor surrogate. Damage type is typed/pass-through; no
  resistance authority exists because the fossil did not apply resistances.
- Forbidden writers: direct-fire code, deployable handlers, Ymir contacts,
  Unity components, Eve lowerers, scalar-health helpers, and reconciliation
  loops cannot independently apply or repair armor, equipment, or hull damage.
- Shared paths: direct and deployable impacts enter the same transaction after
  their respective authoritative impact facts are resolved. All behavior
  consumers use the shared operational-equipment query, so disabled,
  thermally-offline, and destroyed items cannot keep acting through a forgotten
  subsystem-specific filter.
- Cut line: scalar-hull-only and caller-specific damage paths are no longer
  owners. Hull/hardpoint topology initializes armor grids; only the daemon
  transaction mutates them thereafter. Penetration always advances by a
  half-cell step, eliminating the fossil's non-advancing infinite loop.

### Daemon Destruction Transaction

- Owner: cause-specific daemon damage owns one destruction identity, active-world
  removal, reference cleanup, loot rolls, pickup state, and chronology.
- Inputs: canonical hull result or cockpit-destruction crossing, run seed,
  entity equipment/cargo, and fossil loot settings (`0.25` equipment
  probability, `25` launch speed, `30` second life).
- Outputs: an inactive identity-stable tombstone, deterministic typed pickups,
  `entity.destroyed` and `pickup.dropped` events, and Eve world removal/effects.
- Derived state: the retained entity row exists only for stable indexing and
  replay protection. It is not rendered, targetable, salvageable, or alive.
- Forbidden writers: Unity destruction callbacks, Eve feedback renderers, and
  the reserved `DestroyEntity` command cannot erase entities or create loot.
- Shared paths: direct shots and deployable splash invoke the same destruction
  commit after the shared damage resolver crosses zero hull or destroys an
  installed cockpit. Cockpit destruction preserves the remaining hull value and
  publishes `equipment.destroyed` before `entity.destroyed(cockpit-destroyed)`.
- Cut line: client-commanded removal and presentation-triggered loot are dead;
  EveUnity resolves `effect.feedback.entity.destroyed` only after the provider
  has published authoritative destruction chronology.

### Daemon Terminus Scenario Ownership

The generated Terminus world and deterministic released-client proofs are
separate daemon-owned scenarios. `standard` preserves the generated ship
loadouts. `released-client-proof` alone creates the one-hit raider and guaranteed
salvage transaction; `cargo-capacity-rejection-proof` additionally fills every
generated player cargo bay from typed catalog volume and bay capacity. The
scenario selects a distinct run identity, so an existing proof run cannot
impersonate ordinary play. Unity selects the scenario only as a launch input and
cannot write cargo, hull, pickups, or feedback.

### Daemon Pickup Contact Transaction

- Owner: the daemon simulation owns pickup collection by interpreting typed
  entity-pickup contact facts returned by embedded Ymir physics.
- Inputs: the exact Ymir contact, canonical active entity and live pickup,
  catalog-backed cargo capacity, and current frame identity. Tractor power and
  range influence pickup motion only.
- Outputs: capacity permits one atomic cargo insertion, pickup removal, and
  `pickup.collected` event. Capacity refusal retains the pickup, applies the
  fossil 25-unit outward kick along the contact normal, and emits
  `pickup.rejected`.
- Derived state: pickup transforms, tractor visuals, Eve world nodes, Unity
  colliders, and client selection are presentation or control projections and
  cannot admit collection.
- Forbidden writers: the reserved `PickUpLoot` command, proximity/range scans,
  Unity collision callbacks, and generic Eve lowerers cannot mutate cargo or
  despawn pickups.
- Shared paths: destruction drops, generated pickups, and restored live pickup
  rows all enter the same next-tick Ymir body/contact path. Duplicate contact
  facts for one entity-pickup pair in a frame reduce once; a collected pickup
  identity cannot commit again.
- Cut line: the command executor/builders and distance-based collection helper
  are deleted. The old enum ordinal remains reserved only to avoid shifting
  serialized command values and is rejected by the daemon.
- Verification layer: daemon/Ymir smoke proves one commit per consumed fact,
  duplicate-fact suppression, collection, capacity refusal, and outward kick.
  The released EveUnity `0.3.63` warm rejection witness proves full player cargo
  remains `5 -> 5`, the pickup remains live, and distinct player recontacts
  arrive as distinct `ymir-fact:` feedback identities without a player
  collection event.

### Daemon Gravity Field Contract

- Owner: typed daemon zone/body state owns positive gravity-depth magnitudes;
  embedded Ymir owns radial integration and contact facts without reinterpreting
  their sign.
- Inputs: body poses, influence radii, positive `GravityWellDepth`, positive
  zone `GravityTerrainDepth`, exponents, waves, and fixed delta.
- Outputs: Ymir produces inward acceleration, positions, velocities, and
  contacts. Daemon projections publish typed gravity viewport and render-splat
  documents from the same state.
- Derived state: terrain samples and `gravity.height` splat values are negative
  because presentation subtracts the positive magnitude. Bands, Eve fields,
  Unity meshes, and cameras remain derived.
- Forbidden writers: negative authored depths, adapter-side sign compensators,
  Unity gravity scripts, Eve lowerers, main-menu fallback fixtures, and stale
  persisted Terminus rows cannot author gravity semantics.
- Shared paths: generated, restored, and imported worlds use the same magnitude
  convention; physics and every render projection consume those canonical
  values.
- Cut line: the dead main-menu fallback world is deleted. Built-in Terminus
  state with the old sign fails the current-scenario check and is regenerated;
  only presentation conversion may negate depth.
- Verification layer: daemon smoke proves inward Ymir velocity, negative sampled
  height, and negative body splats. Released cold/warm witnesses exercise the
  persisted daemon path.

### Pilot Camera Presentation Contract

- Owner: the Aetheria daemon selects camera semantics from authoritative dock
  state and publishes the fossil framing and lens values. EveUnity owns only the
  native camera projection.
- Inputs: controlled entity identity and rotation, dock parentage, presentation
  mode, daemon-published aim convergence state, distance, field of view, target
  screen position, damping, and clip planes.
- Outputs: undocked flight uses `perspective.entity-forward-follow.v1` with the
  fossil `Third Person Rig` values and `aim.convergence-point.v1` look-at. Docked
  presentation remains a distinct mode and cannot lend its framing to flight.
- Coordinate boundary: Cinemachine serializes target screen Y from the top of
  the frame, while Eve's canonical viewport follows Unity viewport coordinates
  from the bottom. Aetheria translates fossil flight `0.81` to Eve `0.19` and
  docked `0.55` to Eve `0.45` before publication. EveUnity receives only the
  canonical value and has no Cinemachine compatibility branch.
- Derived state: Unity camera position, rotation, matrices, culling mask, and
  temporal fog history are presentation state.
- Forbidden writers: Unity cameras cannot choose dock state, alter entity look
  direction, or infer an Aetheria camera from product types. The old scene and
  Cinemachine components are reference evidence, not runtime authorities.
- Cut line: the unconditional `planar.top-down-follow.v1` projection no longer
  collapses the fossil dock and flight cameras into one false top-down view.

### Daemon Agent Combat Policy

Agent combat uses the fossil `CombatState` policy inside the daemon scheduler,
not a faction-wide attack radius. The planner reads operational typed weapon
behaviors and durable group membership, samples 32 range points with the fossil
`range^0.25` preference, evaluates range-conditioned damage and the authored
damage curve, and selects the highest current DPS group. Movement blends
target-relative strafe with closing/opening pressure using the fossil `0.5`
forward lerp and 50-unit forward-distance scale. Projectile aim uses CultMath
first-order intercept. Fire is emitted only through the shared weapon-group
command when the selected equipment's catalog placement rotation faces the
intercept direction; locking groups continue to request their daemon-owned
progressive locks. Flight and agent combat share one equipment-rotation parser.
The task's weapon-group field is now an observed last selection, not an attack
policy owner. Daemon smoke proves close/open strafe, DPS group selection,
projectile lead, hardpoint gating, progressive lock, and target destruction.

### Authoritative Flight Actuator Contract

- Owner: Aetheria daemon owns persistent local helm axes, desired look
  direction, installed-actuator evaluation, energy/heat/visibility consequences,
  hull drag, and the velocity/direction submitted to physics. Ymir owns body
  integration and contacts. Eve clients own input sampling and interpolation,
  never accepted motion.
- Inputs: accepted semantic movement/look commands; current hull direction and
  velocity; catalog hull/item shapes and masses; equipment placement and
  rotation; online/durability/thermal state; behavior payload performance stats;
  capacitor/reactor availability; fixed delta.
- Outputs: actual entity direction and velocity, persistent helm/look state,
  typed Thruster/AetherDrive behavior state, equipment heat, plume visibility,
  and the Ymir-integrated body pose.
- Ordinary-flight invariant: standard ships fly through directional thrusters.
  The daemon clears and reallocates their axes each tick using the fossil
  forward/reverse/left/right rotation groups, symmetric lateral-torque
  compensation, placement-derived torque, and desired-look turn demand. A
  thruster that cannot fund its authored energy demand produces no force, heat,
  or plume visibility.
- Variant invariant: AetherDrive is not baseline propulsion. The canonical
  catalog contains the single 250,000-credit `Traction` drive with hardpoint
  type `AetherDrive`, and only the 7,500,000-credit modified `LonginusX` hull
  exposes that hardpoint. The fingerprinted 2021-04-14 catalog restores the
  separate common `Longinus` row under its original GUID, including four
  directional-thruster mounts, and restores `Djinni`; `LonginusX` and every
  AetherDrive item carry typed `rarity:rare` catalog tags. The daemon applies a
  0.05 generation multiplier to those tags. Its rotor RPM, coupling, torque
  curve, energy, heat, and thrust run only when that rare equipment is actually
  installed. Ordinary flight fixtures explicitly prove that no AetherDrive
  state is manufactured.
- Catalog recovery owner: `Aetheria.State.Import` alone turns the quarantined
  April 2021 MessagePack fossil into typed hull documents. Its allowed inputs
  are the fingerprinted historical blob and the explicit `April2021` field
  layout; its outputs are ordinary typed item documents. The blob, Unity
  database classes, and current fixture catalog are not runtime authorities.
  Missing-row recovery is limited to the historical `Longinus` and `Djinni`
  GUIDs plus missing `Thruster` equipment required to fill their authored
  mounts, so the supplemental catalog cannot overwrite live rows or silently
  revive unrelated obsolete content.
- Generation owner: one daemon loadout generator owns one advancing mutable
  CultMath random stream. A readonly struct field is forbidden because it
  mutates a defensive copy and restarts every weighted draw. Price, allegiance,
  graph distance, footprint, and typed rarity are inputs; selected hull and
  immutable selection receipt are outputs. Eve and Unity only inspect them.
- Derived state: Eve actuator rows, RPM gauges, plume/effect intensity, sound
  parameters, and interpolated transforms are presentation projections. They
  cannot write velocity, direction, heat, energy, or equipment state back.
- Forbidden writers: faction speed constants, command reducers, task planners,
  Eve lowerers, and Unity transforms cannot assign accepted ship velocity or
  heading. `SetLookDirection` now writes desired look only; physical actuators
  turn the hull. World-space autonomous travel must be translated into the same
  local strafe/forward helm axes used by a pilot.
- Shared paths: manual movement, autonomous exploration/haul/patrol/attack,
  cross-zone approach, and home docking all enter the same command reducer,
  actuator step, and Ymir world step. AetherDrive and Thruster share mass,
  thermal, energy, drag, and physics owners without pretending to be the same
  equipment class.
- Cut line: `ApplyMovementIntent` and faction `PawnSpeed`/`RaiderSpeed` motion
  are gone. No compatibility fallback may restore direct velocity assignment.
- Verification: daemon smoke proves local-axis dominance through ordinary
  thrusters, placement-derived torque/thrust state, funded heat and plume,
  unfunded no-motion/no-plume behavior, progressive hardware turning before
  weapon lock, autonomous navigation and cross-zone travel through Ymir, and
  isolated rare AetherDrive spool/force on a modified-hull fixture. Canonical
  catalog assertions lock the restored common hull identities and the
  `Traction`/`LonginusX` rarity boundary; a 2,000-loadout deterministic witness
  proves rare candidates remain possible without becoming the default path.
- Open parity: model-local hardpoint transform directions (the daemon currently
  has catalog placement rotation, not the old renderer-populated barrel
  transform), exact per-equipment behavior execution ordering,
  float32/CultMath parity against the fossil, live
  production-world generation-frequency evidence, and generic native
  thrust/turn feedback still
  require proof.

### Flight Post-Processing Contract

- Owner: Aetheria selects the logical flight profile and owns its native asset
  variants. EveUnity owns only a reversible lowering lease for the active world
  camera.
- Inputs: `postProcessProfileAssetRef` from the world surface and the matching
  provider-catalog variant. The Unity authoring build derives
  `profile.environment.flight` from the fossil ARPG global profile's ACES,
  bloom, histogram dark-scene exposure bound, contrast, vignette, and grain.
- Outputs: the generic Unity lowerer resolves a `VolumeProfile`, enables URP
  post-processing on the leased camera, and installs one global runtime volume.
  The same world view advertises `temporal-reprojection.v1` with the fossil
  pilot camera's `0.99` history blend, `0.1` jitter scale, zero sharpening, and
  high quality; EveUnity lowers those neutral values to URP TAA.
- Derived state: the Unity `Volume`, additional-camera data, and enabled flag are
  client presentation state. They restore or disappear when the camera lease
  ends.
- Forbidden writers: scene-local Aetheria scripts, provider-id checks, fog-color
  heuristics, and client-authored exposure values cannot choose flight
  presentation.
- Cut line: the fossil scene's global `PostProcessVolume` is reference evidence,
  not a surviving runtime owner. Missing or incompatible advertised profiles
  fail closed instead of silently rendering a different look.

### Celestial World Presentation Contract

- Owner: the daemon owns which celestial bodies and asteroid instances exist,
  their authoritative orbit-derived XZ positions, terrain-derived altitude,
  authored scale, and presentation identity. Aetheria's provider catalog owns
  the native visual assets. EveUnity owns only generic entity instantiation.
- Inputs: the current zone's typed body and asteroid rows, daemon simulation
  time, canonical orbit evaluation, gravity-terrain evaluation, and daemon
  render settings.
- Outputs: planets, suns, gas giants, and asteroids occupy ordinary rows in the
  shared Eve entity SoA generation. Stable synthetic identities distinguish
  these presentation rows from simulation entities; each row carries a generic
  entity kind and provider asset reference.
- Derived state: the Unity `GameObject`, prefab instance, renderer bounds,
  material instances, native layer, and pilot-frustum intersection are client
  projections. Celestial synthetic identities are not selectable, controllable,
  damageable, or valid command targets.
- Forbidden writers: the fossil `ZoneRenderer`, planet scripts, Unity physics,
  and runtime-side terrain sampling cannot create, position, scale, rotate, or
  remove celestial rows. EveUnity cannot import Aetheria types or branch on
  Aetheria celestial asset keys.
- Shared paths: cold download, warm cache, reconnect, and every committed SoA
  generation use the same provider assets and immutable generation swap as
  ships, stations, pickups, and persistent payloads.
- Cut line: the Aetheria-specific zone-render document is not a client ABI.
  Provider packaging extracts only the advertised visual child from each fossil
  celestial prefab, then applies the common script, physics, material, and
  bundle verification pipeline. Gravity wells, map icons, runtime scripts, and
  other fossil authorities do not hitch a ride inside the presentation prefab.
- Verification layer: the daemon smoke proves stable, unique negative synthetic
  indices, provider asset references, body/asteroid positions, and equivalent
  local/network SoA bodies. The released generic-client witness records every
  presented entity's kind, asset, scale, renderer count, and pilot-frustum
  intersection. Its warm run instantiated eleven bodies and eighteen asteroids
  with enabled provider renderers; seven celestial instances intersected the
  pilot frustum. This proves projection and lowering, not final art parity.

### Native Environment Lighting Contract

- Owner: Aetheria owns stellar ambient tint, the gravity-fog frame fill, the
  custom reflection cubemap, and each provider material's lit/unlit semantics.
  EveUnity owns only the reversible application of those advertised environment
  facts.
- Inputs: daemon-owned sun `LightColor`, sun mass and light-radius multiplier,
  `Assets/Textures/studio2.hdr`, the gravity-fog field, and source shader lighting
  models. Multiple stellar tints blend by their authored light-radius weight.
- Outputs: the world surface advertises stellar-tinted flat ambient lighting at
  fossil intensity `1.46`, the studio HDRI as custom reflection at intensity `1`,
  no skybox, and no invented directional key light. Provider material conversion maps
  particle sources to URP Particles/Unlit, unlit or sprite sources to URP/Unlit,
  and genuinely lit sources to URP/Lit.
- Derived state: Unity ambient state, custom-reflection state, runtime material
  instances, and shader keywords are client presentation state. The studio HDRI
  textures reflected ambient light; it does not choose its color or own the
  visible background. The raymarch pass fills the frame.
- Forbidden writers: the daemon cannot invent a directional light to compensate
  for broken material conversion. EveUnity cannot promote the reflection HDRI to
  skybox/ambient authority or infer shader models from Aetheria asset names.
- Cut line: the obsolete provider skybox catalog entry and world binding are
  deleted, as is the invented `0.4,-1,0.25` warm key light. The embedded
  fossil shield renderer is removed from legacy ship roles and catalog-backed
  `entity.hull` presentation prefabs because its
  dithered collision response is already externalized as the provider-owned,
  receipt-driven shield-impact effect; it cannot remain permanently visible
  after generic material conversion.
- Verification layer: daemon smoke proves the stellar color projection and
  negatively proves that neither skybox nor key-light fields are published. The released witness records flat
  ambient mode, the exact lowered color, zero directional intensity,
  no player `Shield Visual`, seven active player renderers, and a map-channel
  material using `Universal Render Pipeline/Unlit`; visual inspection shows ship
  hulls instead of opaque shield ellipsoids and readable map glyphs.

### Stardust Field-Particle Contract

- Owner: Aetheria owns the authored Stardust program, its exact scalar values,
  its color ramp, and the choice to visualize the gravity-fog field with
  stateless camera-relative particles. EveUnity owns only generic program
  binding, compute dispatch, and procedural drawing.
- Inputs: the same typed `gamecult.eve.fields.splats.v1` document used by the
  fog volume; gravity surface height and gravity-derived tint (not patch or
  patch-height layers); the provider compute shader, material, and blackbody
  color texture; the provider dither texture; active camera XZ; daemon
  simulation time; and the client render-frame index used only for temporal
  coverage.
- Outputs: `field.particles3d` names the provider programs, logical field and
  asset ports, the 256-by-256 row-major particle grid, 128-thread dispatch,
  28-byte particle stride, exact fossil values, and camera-followed viewport
  policy.
- Hash invariant: no client CPU particle state exists. The provider compute
  program retains `id % span`, `id / span`, truncation toward zero, the `65535`
  unsigned cell offset, Wang/xorshift sequence, Perlin jitter, lifetime phase,
  and float32 evaluation. Changing any of these creates a different field.
  Buffer slots are not particle identities: after a one-cell X move,
  `before[localX + 1, localY]` and `after[localX, localY]` must be the same
  world-cell particle bit for bit. Every slot is reassigned while all
  overlapping world particles remain visually motionless.
- Viewport invariant: the particle grid and sampled gravity textures use one
  transform and one integer lattice. The 256-by-256 particle grid at six world
  units per cell owns a 1536-square viewport. The 2048-square surface-height
  target is therefore 0.75 world units per texel and exactly eight gravity
  texels per Stardust cell; the 512-square tint target is exactly two texels per
  cell. Camera XZ snaps by one six-unit Stardust cell/eight gravity pixels using
  truncation toward zero. The snapped center becomes both the raster viewport
  center and `_GridTransform.xy`. A 2000-wide viewport is rejected because it
  aliases the gravity and particle lattices even when particle hashing itself
  is correct. Fog and particles do not separately project that frame: both
  generic lowerers call the same viewport-frame resolver and receive identical
  `EveFieldsViewport` bounds.
- Derived state: GPU buffers, generated quads, raster targets, keywords, and
  draw calls are client presentation state. The viewport-scaled dither
  coordinates and render-frame index are also presentation state; they cannot
  feed particle identity, phase, position, or simulation.
- Forbidden writers: EveUnity cannot reseed, pool, randomly initialize, move,
  recolor, or CPU-generate particles. Aetheria cannot publish per-particle rows
  or make the field a gameplay body set.
- Cut line: the fossil `Stardust` MonoBehaviour and renderer feature remain only
  reference code. Generic clients execute advertised provider programs through
  logical ABI metadata; no Aetheria type enters EveUnity.
- Verification layer: daemon smoke locks every authored value and asset ref;
  generic package tests lock ABI validation, repeated daemon-time bindings,
  truncating camera snap, buffer layout, and dispatch dimensions. Provider
  bundle verification dispatches the bundled HLSL and reindexes by world cell;
  705 overlapping cells across X, Y, and diagonal whole-buffer remaps must keep
  all seven particle floats bit-identical while the same buffer slot changes
  identity. An asymmetric gravity texture also proves positive-world X samples
  positive texture X; the deleted fossil gravity camera's two-axis reversal is
  not part of the typed Eve Fields viewport. The released witness must record 65,536 particles, one compute
  dispatch and one pilot-camera draw while the map camera remains isolated.
  The released `0.3.57` witness additionally requires the live fog and particle
  grid centers to be equal and the leased pilot camera to report URP temporal
  anti-aliasing with the advertised history and jitter values. It resolves the
  material's logical ports from provider metadata and proves the cloned live
  material holds the provider dither texture, pixel scale, and advancing render
  frame. The bundle gate rejects a fixed `_AlphaClip` regression and requires
  the fossil temporal-coverage, overwrite-blend, and depth-write source tokens.

### Gravity Fog Presentation Contract

- Owner: Aetheria owns gravity/fog brush placement and values, authored volume
  tuning, and the provider shader asset. Eve Fields owns the portable splat
  document, radial falloff equations and parameters, and `field.volume3d`
  logical ports. EveUnity owns only the native render lifecycle.
- Inputs: daemon-derived brush centers, radii, depths and exponents; the
  canonical viewport splat document; logical surface-height,
  patch-height, patch, tint, scalar parameters, viewport-to-texture scale
  relations, provider asset reference, and the selected runtime asset variant's
  native program metadata.
- Outputs: the generic runtime rasterizes logical field layers, executes the
  advertised raymarch, optional temporal-history, and composite passes, and
  presents pixels. No pass can emit gameplay state or commands.
- Derived state: Unity materials, shader-property names, pass indices, keywords,
  raymarch targets, ping-pong history textures, previous camera matrices, and
  viewport-derived texture scales and composite counters are runtime projections.
  They are neither daemon truth nor portable Eve state.
- Native target topology: the provider advertises the fossil layer shapes rather
  than letting the lowerer invent one common texture: surface height is
  `2048x2048`, patch height is `256x256`, patch and tint are `512x512`, and only
  tint is mipmapped/trilinear. EveUnity applies those descriptors without owning
  the layer meanings.
- Time and camera bindings: the splat document's daemon-authored simulation time
  drives logical `flowScroll` through an advertised scale. The provider variant
  maps logical camera-to-world and flow-scroll ports to native uniforms; the
  generic runtime does not use local gameplay time or provider identifiers.
  The provider shader constructs both its raymarch and temporal-reprojection
  rays using the historical Unity shader camera transform, whose local forward
  is positive Z. EveUnity `0.3.63` supplies that transform instead of the
  negative-Z `Camera.cameraToWorldMatrix`. The provider also advertises
  `non-render-target-projection.previous-view.v1`, preserving the historical
  `Graphics.Blit` GPU-projection convention without making it a client guess;
  provider bundle verification rejects divergence between the two passes.
- Semantic split: `gravity.height` is the negative gameplay terrain projection;
  `fog.surface_height` is the positive visual displacement the fossil
  `PowerBrush` camera supplied to `_NebulaSurfaceHeight`. Both derive from the
  same daemon-owned radial brushes, but neither may impersonate the other.
- Fossil producer map: layer 9 coarse displacement and layer 10 fine waves
  compose `fog.surface_height`; those same producers plus layer 12 patch
  displacement compose `fog.patch_height`; layer 11 simplex/cellular density
  brushes compose `fog.patch`; layer 13 ambient and body light brushes compose
  `fog.tint`. These are additive field producers, not Unity camera objects.
- Procedural-source ownership: Eve Fields defines world-anchored Ashima simplex,
  the fossil moving cellular-B function, published simulation-time animation,
  splat-local animated radial cosine, absolute-value folding, and `PowerPulse`.
  Aetheria owns each brush's extent, target layer, depth, constant offset,
  frequency, speed, exponent, and color. The fossil brush objects use Unity's
  one-unit built-in Quad (`fileID 10210`), so authored scale converts to half
  extent by multiplying by `0.5`, not `5`. Canonical `ARPG.unity` scales the
  gameplay `Zone Brushes` parent to `3000`, making its global half extent
  `1500`; `TestScene`'s smaller `1024` value is not gameplay authority.
  The splat cameras independently use orthographic size `1000`, so the canonical
  field viewport is a 2000-by-2000 square. Brush coverage and sampling extent
  are separate provider-owned facts; the generic lowerer does not infer either.
  Constant offsets are separate constant splats. No provider texture reference
  is introduced: the fossil materials carry a stale `_MainTex`, but their
  shaders sample procedural functions and never read it.
- Entity altitude: after every Ymir XZ step, the daemon derives root entity Y as
  fossil `gravity.height + HullGridOffset`; children inherit their parent's full
  altitude. Eve clients consume the resulting 3D position and never sample or
  recreate terrain height as gameplay truth.
- Forbidden writers: the old `VolumeSampling`/`VolumeCloudRenderer` presentation
  path, shader code, Unity cameras, and generic lowerers cannot calculate gravity,
  move bodies, or publish world truth. EveUnity may not import an Aetheria assembly
  or branch on Aetheria asset identities. A viewport-sized solid splat cannot
  stand in for the zone's radial gravity terrain.
- Shared paths: first frame, camera motion, reconnect, viewport resize, and warm
  asset reuse all enter the same generic volume lifecycle. History resets on a
  new node, program, or render size. A provider that advertises a temporal pass
  without all required history ports, or a viewport scale relation with an
  unresolved logical port or texture, fails closed.
- Cut line: the daemon publishes the fossil zone and body `PowerPulse` brushes
  directly; the former full-viewport solid gravity writer is deleted. The two
  invented viewport-local patch noises are also deleted before the fossil
  producer stack is restored. Procedural samples are anchored to field-world
  coordinates, so viewport motion cannot move the fog through the world. The
  portable surface contains logical field and parameter names only.
  `_Nebula*`, `_PrevVP`, `_ResetHistory`, shader keywords, and pass indices live
  exclusively in the provider's Unity asset-variant metadata.
- Verification layer: EveUnity package tests prove two-pass compatibility,
  complete temporal-program acceptance, partial-program rejection, portable
  `PowerPulse` GPU evaluation, and absence of Aetheria assembly references.
  Aetheria daemon smoke proves radial brush parity and that the exact fossil
  shader ABI is advertised outside the portable surface. Released package
  `0.3.49` proves target topology, authoritative flow time, explicit camera
  transforms, float32 packed-output diagnostics, and both camera-channel masks.
  The released HD capture proves a live fog horizon using the original
  `-36.17` vertical-noise amplitude. The earlier opaque slab was caused by
  publishing Cinemachine's top-origin `0.81` as Eve bottom-origin `0.81`, which
  placed the camera below the fog surface. The controlled zero-noise run was a
  diagnostic, not the adopted rendering contract. Blue tint, luminance, patch
  structure, gravity-wave readability, and world geometry remain open visual
  parity work. Released EveUnity `0.3.63` also restores the provider-advertised
  previous-view/current-projection temporal policy, the fossil ultra-quality
  first history frame, adaptive histogram exposure, and HDR grading before
  ACES, and lowers the provider-selected non-render-target temporal projection.
  The released warm witness records 359 fog composites while retaining the
  daemon gameplay assertions and camera-channel isolation. The alpha-fixed cold
  HD witness uses bundle
  `a829e57731fbe08f4564fa131468148e99f6b4204708ad3e64498885532b5091`
  and passes in 52.7 seconds. Its temporal history stores direct density in
  alpha; bundle verification rejects any composite that again unpacks that
  value as the raymarch pass's packed distance/density payload. Cloud structure
  and Stardust occupy the same projected gravity surface. Remaining color and
  composition differences are not permission to alter the fossil scattering
  or tint inputs without stronger historical evidence.

### Daemon Asset Content Delivery

- Owner: the Aetheria daemon owns bundle content and serves bytes only through
  the managed `cultmesh.content.v1` session hosted by CultMesh.
- Inputs: provider bundle path, packed artifact manifest, content hash, byte
  length, negotiated chunk requests, and resumable client checkpoint.
- Outputs: snapshot delivery publishes the typed artifact manifest only;
  content-session requests return bounded byte ranges which the generic runtime
  verifies and atomically promotes to a hash-named `.body`, then binds to a
  read-only mapped-body lease for Unity loading.
- Derived state: Unity `AssetBundle` instances, native prefab lookup, cache
  files, progress UI, and screenshots cannot publish or mutate provider bytes.
- Forbidden writers: batched snapshot chunks and the former
  `gamecult.cultmesh.cdn.asset_blob.v1` fallback are deleted. Eve surfaces and
  renderer code cannot smuggle bundle bytes through state records.
- Shared paths: cold transfer, resume, and warm cache reuse all resolve the same
  advertised manifest and content hash through the generic runtime transport.
- Cut line: CultMesh snapshots index manifests; managed content sessions own
  remote bytes, verification, and promotion. Mapped-body negotiation begins only
  at the returned final path and cannot map partials or reconstruct cache names.

- Owner: `Aetheria.State` owns the new typed state spine for durable state.
  The embedded `GameCult.Aetheria.State.Unity` package owns Unity's runtime
  typed state file path plus the boot-time state-file probe, and is the landing
  zone for future typed catalog boot.
  `ActionGameManager` opens the package-owned typed runtime catalog snapshot
  and binds item reference resolution directly through `ItemManager`.
  Runtime code receives narrow `ItemManager` projection methods over typed
  runtime catalog rows and no longer hydrates old `ItemData` DTOs. `Galaxy`,
  entity restore, and faction-distance loadout weighting have been moved off
  legacy faction catalog reads.
  The embedded Unity state package owns the read-only runtime catalog model
  contract and the read-only known-schema `.cc` catalog opener. `ActionGameManager`
  now exposes that typed package snapshot at boot. The SDK-style
  `Aetheria.State.Unity` facade maps typed `.cc` documents into the same
  contract for full .NET smokes and Eve surface reads. Neither writes state or
  owns simulation. The runtime no longer has a MessagePack catalog cache or
  whole-item DTO projection cache. The typed runtime catalog snapshot exposes typed
  item rows only. `ItemManager.CreateRuntimeBehaviors` owns the explicit typed
  behavior-kind switch from typed payloads into live behavior instances. There
  is no remaining `RuntimeBehaviorConfig` projection and no reflection over
  `LegacyPayloadKeyAttribute` to select or assign config fields;
  gameplay stat modifiers target live behavior instances and package-owned
  behavior metadata instead of rebuilding config DTOs for lookup.
  `ItemInstance.Reference` stores the typed item key, `ItemInstance.ItemKey`
  is the runtime identity surface, and `ItemInstance.ItemId` is a derived
  legacy-GUID compatibility projection. `EquippedCargoBay.ItemsOfType`,
  consumable activation, action-bar consumable fill/quantity display, item
  transfer lookup, and trade owned-count rows now use item keys rather than
  GUID-owned cargo indexes. Weapon ammo and item-use behavior config item
  references are also item-key-only at the behavior API surface
  (`AmmoItemKey`/`ItemKey`); the derived `AmmoType` and `Item` GUID
  compatibility properties have been deleted. Runtime commit DTOs now
  publish item and hull identity through `ItemKey`/`HullItemKey`, with legacy
  ID fields demoted to compatibility. The package runtime catalog snapshot now
  indexes items by canonical item key, and Unity gameplay/UI/zone helper
  lookups resolve `ItemKey` directly rather than deriving legacy GUIDs for
  `FindItemByLegacyId`. The package snapshot no longer exposes item,
  corporation, or name-file `Find*ByLegacyId` indexes, and its runtime
  item/corporation/name-file DTOs no longer publish legacy relationship fields;
  legacy-ID lookup remains only on the canonical `Aetheria.State`
  migration/catalog inspection boundary.
  The shared runtime item catalog reader no longer has a GUID index or
  `GetRuntimeItem(Guid)` entry point; item-key strings are the only lookup authority;
  there is no remaining `ItemInstance.Data` identity/backing field. `ItemManager`
  reads typed catalog
  row lookup. Behavior
  type selection now uses an explicit runtime catalog map instead of
  `UnionAttribute` reflection, and the temporary behavior config constructor
  map is keyed by typed behavior payload kind rather than legacy union key.
  Live `Behavior` instances expose their typed payload kind and group directly,
  so runtime state projection, stat-modifier matching, and behavior grouping no
  longer reach back through config objects for behavior identity.
  Runtime item category classification uses package-owned
  `AetheriaRuntimeItemCategories` tokens; gameplay code should not ask C#
  legacy DTO class names to classify typed catalog rows. `Aetheria.State.Import`
  now materializes stable category tokens such as `hull`, `weapon`, `gear`,
  `cargo-bay`, and `docking-bay` into typed item documents, while old union
  names remain migration provenance. The typed state verifier also consumes
  those package-owned category tokens for consumable and docking-bay coverage
  checks and rejects item categories ending in `Data`.
  Item/behavior DTO field layout is no longer marked in live Unity source with
  MessagePack or project-owned payload-key attributes. Importer key maps and
  typed runtime payload fields own migrated layout; runtime behavior
  construction does not use field attributes as an authority. Item properties
  manufacturer display is a typed snapshot consumer; loadout generation uses
  typed catalog rows for hull type, shape, category, hardpoint type, behavior
  kind prefilters, and selected-item instantiation. The old trade debug UI
  and console `give` command have been deleted. The surviving trade menu also
  uses typed catalog rows for first-pass size, hardpoint, and behavior filters
  before hydrating row projections. No live caller can enumerate
  legacy catalog entries or request catalog-shaped single entries through
  `ItemManager`.
  Equipment placement rotation is durable semantic state, not an occupancy-loop
  temporary. The daemon generator emits the same quarter-turn used to reserve a
  hardpoint, free-space fit, or cargo fit; loadout documents, runtime commits,
  catalog and viewport snapshots, state mapping, and clone/transfer paths retain
  it. This is the required input for daemon-owned thruster direction and torque;
  Unity may visualize the resulting actuator state but cannot reconstruct a
  missing rotation from prefab transforms.
- Inputs: Unity gameplay code, legacy catalog files, typed state documents, and
  CultMesh server state.
- Outputs: `Aetheria.State` emits `.cc` state and CultMesh documents. Runtime
  typed item catalog projection emits in-memory domain objects only; the old
  local save and editor catalog write paths have been cut. Migrated catalog documents are
  addressed through `AetheriaCatalogKeys` and `AetheriaStateNode` legacy-ID
  methods, not importer-local string concatenation. Player settings, loadout
  templates, and run checkpoints have a shared Unity commit-log primitive plus
  a typed state node applicator. Run checkpoints now carry the current zone and
  its entity snapshots through that same command/apply spine. Whole-galaxy
  unloaded-zone projection still needs to be added once lazy generation has a
  typed state owner. The old `SaveState`/`SaveZone` command surface is gone.
  The catalog operator UI is emitted as a typed Eve surface document derived
  from the catalog snapshot.
- Derived state: UI panels, generated keyboard layouts, editor rows, typed
  state boot warnings, and serialized legacy payloads are projections,
  migration inputs, diagnostics, or disabled session-local DTOs, not durable
  authority.
- Forbidden writers: old JSON backing stores, JsonKnownTypes converters,
  Rethink/LiteNet paths, IMGUI database editor exports, local save-file writes,
  generated keyboard layout caches, server-side `DatabaseCache` paths, and item
  DTO projection caches.
- Shared paths: manual gameplay edits, editor edits, server updates, file load,
  file save, and migration all need to converge on one typed commit primitive.
- Deletion line: no new behavior should recreate old `ItemData` DTO metadata,
  whole-item projection caches, or MessagePack catalog paths except bounded
  migration readers.

## Target Authority Map

- Owner: `Aetheria.State`, a typed document layer backed by modern
  `GameCult.Caching` and opened through a CultMesh node.
- Inputs: typed commands, save/import migration records, CultMesh replication,
  editor mutations, and deterministic simulation facts.
- Outputs: CultCache `.cc` state files, CultMesh document streams, Eve surface
  documents, simulation events, and migration reports.
- Derived state: Unity scene objects, HUD panels, editor inspectors, debug
  views, server dashboards, and compatibility DTOs.
- Forbidden writers: Unity UI components, RethinkDB, legacy JSON stores,
  `RuntimeCatalogEntry.ID`, `RuntimeItemProjectionEntry`, global cache statics,
  and any compatibility reader.
- Shared paths: gameplay input, editor edits, import/deep-load, replication,
  simulation ticks, and tests all call the same typed state service.
- Deletion line: keep old MessagePack/catalog metadata quarantined at import or
  typed CultCache transport boundaries only. Delete or quarantine any remaining
  live Unity predicates that still need legacy DTO vocabulary, and replace
  operator/runtime UI truth with Eve surfaces rather than parallel status
  widgets.

## Intended Change

Create a modern Aetheria state runtime that opens a CultMesh node over a typed
CultCache file, registers Aetheria document types, and exposes narrow ports for
gameplay, editor tools, migration, and UI projection. The state runtime becomes
the only live writer. Everything else becomes a consumer, a command source, or
a migration reader.

Use modern CultLib surfaces:

- `GameCult.Caching` for `[CultDocument]`, record keys, indexes, references,
  schema catalog, and typed persistence.
- `GameCult.Caching.MessagePack` for the binary CultCache backing format.
- `GameCult.Mesh` for node/database illusion, durable shard logs, Verse
  descriptors, authority leases, replication, and peer discovery.
- Eve CultUI for portable UI documents and command envelopes.

## UI Direction

Unity should lower Eve surfaces through UI Toolkit, not through the old uGUI
CultUI package as the strategic path.

The package should live in the Eve repo as an importable Unity package, for
example `org.gamecult.eve.unity-uitoolkit`. It should consume
`gamecult.eve.surface.v1` and `gamecult.eve.command.v1`, lower retained
components into `UIDocument`/`VisualElement` trees, and publish commands back to
the provider through CultMesh/CultNet.

Aetheria should import that Eve package and provide surfaces. It should not own
a bespoke Unity lowering. The existing CultLib Unity uGUI package remains useful
as prior art for resolver-backed controls and inspector generation, but it is
not the new portable UI authority.

The shared Eve Unity package pair now lives upstream in the neighboring Eve
repository, and Aetheria imports it by sibling-repo file path:

- `org.gamecult.eve.surface`: no-engine DTOs for `gamecult.eve.surface.v1`.
- `org.gamecult.eve.unity-uitoolkit`: UI Toolkit lowering from those DTOs to
  native `VisualElement` trees.

These are import boundaries, not Aetheria UI authority. The provider-owned
surface document in CultCache/CultMesh remains the truth, and the lowerer only
projects it.

First Aetheria surfaces to publish:

- runtime HUD
- inventory
- menu shell
- sector/map view
- typed state inspector
- migration report / quarantine viewer
- server/session status

## Migration Phases

1. Baseline
   - Record the current compile/test status for Unity and .NET projects.
   - Keep this map current before cutting code.
   - Count old authority references with `rg` so deletion has a visible target.

2. Modern state spine
   - Add references to modern `GameCult.Caching`,
     `GameCult.Caching.MessagePack`, and `GameCult.Mesh`.
   - Create `Aetheria.State` document definitions using `[CultDocument]`,
     `[MessagePackObject]`, `[CultName]`, `[CultIndex]`, and typed references.
   - Add a smoke that writes, flushes, reopens, and reads typed state from a
     CultCache `.cc` file.

3. Legacy quarantine
   - Done: capture old `GameData/AetherDB.msgpack` and related name files as a
     typed quarantine manifest without granting them runtime authority.
   - Done: emit typed CultCache records plus a migration ledger for the catalog
     quarantine preflight.
   - Done: implement a bounded raw payload mapper that converts stable old
     `DatabaseEntry` union fields into typed item/faction/name-file catalog
     documents without making `Aetheria.State` depend on Unity's legacy model.
   - Done: promote typed name files to `aetheria.name_file.v2` with full name
     arrays, so `Galaxy` name generation has a Verse-owned replacement
     for legacy `NameFile.Names`.
   - Done: promote typed corporations to `aetheria.corporation.v2` with full
     allegiance edges, so temporary `Faction` DTO projection can preserve
     loadout generation behavior without reading legacy faction entries.
   - Done: expand the stable item mapper to include equipment facets and
     behavior kind fingerprints needed by future Unity/Eve catalog consumers.
   - Done: import full item shape-cell masks into typed catalog documents and
     expose them through the Unity-facing read facade.
   - Done: import typed interior shape masks and hull hardpoint definitions
     needed by future equipment/cargo layout and fitting consumers.
   - Done: import recursive typed behavior payloads so future behavior
     factories no longer have to depend on legacy `BehaviorData` objects just
     to see authored behavior fields.
   - Done: expand typed `AetheriaPlayerSettings` so `PlayerSettings.msgpack`
     no longer lacks a durable Verse replacement for actual runtime settings
     values.
   - Done: add typed `AetheriaLoadoutTemplate` documents and smoke coverage so
     `.loadout` no longer lacks a durable Verse replacement.
   - Done: add the Unity runtime read projection for typed loadout templates,
     so the restore menu can rehydrate saved loadouts from CultCache instead
     of relying on the in-memory session list after restart.
   - Done: route runtime loadout restore through typed daemon operations.
     `InventoryPanel` may list typed loadout templates and display the restored
     entity, but the daemon owns blueprint instantiation, credit spend,
     docked-ship assignment, current entity update, and typed run checkpoint.
   - Done: add typed node ports and smoke coverage for run -> zone -> entity
     snapshots so `.zone` no longer lacks a durable Verse replacement graph.
   - Done: extend run checkpoint commits and smoke coverage to carry typed
     action-bar bindings into `AetheriaRunState` as stable input-control and
     target references instead of UI or behavior-object payloads.
   - Done: extend run checkpoint commits and smoke coverage to carry typed
     faction relationship rows into `AetheriaRunState` as corporation
     references, relationship tokens, and numeric standings.
   - Done: make `AetheriaRuntimeSession` a live CultMesh document with state
     node ports, provider advertisement, server heartbeat publication, and
     smoke coverage; daemon liveness is no longer only console output or
     incidental drain status.
   - Done: extend run checkpoint commits and smoke coverage to carry typed
     zone orbit/body rows into `AetheriaZoneState`, so generated celestial
     graph facts no longer live only inside construction blueprints.
   - Done: extend run checkpoint commits and smoke coverage to carry typed
     entity simulation stat grids into `AetheriaEntitySnapshot`, so
     temperature/armor/conductivity no longer live only in runtime blueprints.
   - Done: extend run checkpoint commits and smoke coverage to carry public
     entity session state into `AetheriaEntitySnapshot`: velocity, target
     references, active/toggle scalars, thermal injury accumulators, and active
     consumable timers.
   - Done: extend run checkpoint commits and smoke coverage to carry typed
     behavior progress rows for equipped-item and active-consumable behaviors
     that expose `IProgressBehavior`.
   - Done: extend run checkpoint commits and smoke coverage to carry typed
     weapon behavior state rows for instant, charged, and constant weapon
     runtime internals.
   - Done: extend weapon behavior state rows for lock-weapon progress and
     target entity references, so lock acquisition state no longer evaporates
     at the commit boundary.
   - Done: extend run checkpoint commits and smoke coverage to carry typed
     behavior state rows for sensor ping state plus radiator, reactor, and
     capacitor runtime internals.
   - Done: extend behavior state rows for AetherDrive axis, thrust, RPM,
     maximum RPM, and thrust direction, so drive rotor state has a typed
     snapshot/readback path.
   - Done: extend behavior state rows for ResourceScanner target body key,
     asteroid index, scan timer, range, minimum density, and duration, so
     scanner progress and target ownership are typed state instead of
     live-only behavior memory.
   - Done: extend behavior state rows for MiningTool asteroid belt key,
     asteroid index, and range, so active mining target state is typed
     snapshot/readback state.
   - Done: extend behavior state rows for Thruster axis, thrust, and torque,
     so analog propulsion state is typed snapshot/readback state.
   - Done: extend behavior state rows for Shield efficiency/energy usage,
     VelocityLimit limit, and Thermotoggle target temperature.
   - Done: extend behavior state rows for Switch activation and Trigger
     pending pull state.
   - Done: extend behavior state rows for StatModifier applied/executed flags
     and target-stat count.
   - Done: extend behavior state rows for TurretController initialized weapon
     count, shot speed, and predictive-aim flag.
   - Done: move TurretController weapon range and shot-speed evaluation behind
     `Weapon` runtime methods, so turret AI no longer casts weapon config DTOs
     to decide firing range or predictive aim.
   - Done: cut live simulation grids out of `EntityConstructionBlueprint`; loadout
     and construction templates no longer capture or restore temperature,
     armor, max-armor, or hull-conductivity state.
   - Done: add Unity package readback for canonical typed run, zone, and
     entity snapshot documents, so the `.zone` replacement graph is not only
     writable through the state node but also inspectable by runtime package
     consumers.
   - Done: preserve asteroid belt runtime state on typed asteroid rows:
     damage, respawn timers, and miner accumulator references.
   - Done: persist the actual galaxy generation seed in typed run state, so
     future Continue/restore work has a reproducible topology body before
     applying current-zone/entity snapshots.
   - Done: preserve entity snapshot item-instance facts for equipment,
     cargo-bay, and docking-bay slot rows: item key, quality, durability, and
     quantity now survive canonical state and Unity package readback.
   - Done: preserve item enabled state on typed loadout items and entity item
     slots, so disabled equipment is no longer only runtime blueprint state.
   - Done: preserve cargo bay contents, docking bay contents, and docking bay
     child assignments in typed entity snapshots.
   - Done: preserve aggregate entity visibility and visibility-source count in
     typed entity snapshots, so `Entity.VisibilitySources` no longer hides
     untyped session pressure.
   - Done: preserve entity contact rows for gathered info, hostility, and
     visible classification, so detection/contact state is typed snapshot data
     instead of only reactive runtime collections.
   - Done: preserve dropped world-pickup rows in typed zone snapshots. Run
     checkpoints now carry pickup index, position, velocity, item key, quality,
     durability, quantity, and package readback through
     `AetheriaRuntimeZoneStateSnapshot.DroppedPickups`.
   - Done: lower typed dropped-pickup zone rows back into live scene pickups on
     zone load. `AetheriaRuntimeZoneStateSnapshot.RecordKey` preserves the exact
     typed zone identity, and `ActionGameManager` restores only the matching
     `RunId + ZoneIndex` pickup rows through `ZoneRenderer.DropItem`.
   - Done: replace the main-menu null `Continue` button with a typed run-state
     entry point. `MainMenu` reads available `AetheriaRunState` records through
     `AetheriaRuntimeCatalogStore`, selects the newest run, regenerates the
     galaxy from the saved generation seed, and passes the selected run to
     `ActionGameManager` so boot enters the saved current zone and lowers typed
     pickup rows.
   - Done: restore the current zone's entity graph from typed state during
     Continue boot. Package entity readback preserves `AetheriaEntitySnapshot`
     record identity, and `ActionGameManager` resolves the exact
     `RunId + ZoneIndex` entity-record prefix, removes generated zone entities
     and their agents as Continue authority, lowers all typed entity snapshots
     through the existing construction blueprint path, restores hull,
     equipment, cargo, docking contents, weapon groups, position, direction,
     velocity, shutdown override, heatsink toggle, tractor power,
     heatstroke/hypothermia exposure, active consumable item/timer rows,
     typed stat grids for temperature, thermal mass, armor, max armor, and
     hull-conductivity axes, then reconnects target/contact rows among restored
     entities, restores child/docking relationships from typed child keys and
     docking-bay assignments, restores typed weapon and behavior runtime rows
     into live behavior instances, and binds the saved current entity by
     `AetheriaRunState.CurrentEntityKey` so Continue restore follows exact typed
     entity identity instead of reconstructing the player ship from an integer
     slot. Canonical run state and package runtime snapshots no longer keep the
     old current-entity slot field; the package reader only synthesizes the key
     from older stored runs, and the pending commit reader only synthesizes it
     from older queued run checkpoints, when those compatibility seams are
     encountered.
   - Done: project run checkpoint entity snapshots from the flattened live
     entity graph, not only `Zone.Entities`, so docked child ships can receive
     typed entity records and stable child/docking references instead of
     disappearing from the `.zone` replacement graph.
   - Done: complete typed runtime behavior factory construction and snapshot
     coverage for the live mutable behavior families. Articulated weapon kinds
     restore through typed weapon rows, mutable non-weapon kinds restore through
     typed behavior rows, and generic progress kinds project through the shared
     progress path. Remaining work in this lane is catalog-field coverage, not
     a missing runtime behavior owner.

4. Runtime cutover
   - Done: add a Unity-facing typed catalog read facade and smoke proving it can
     read the materialized `.cc` catalog plus Eve surface without the legacy
     catalog reader.
   - Done: move `Galaxy` faction selection and name generation to the typed
     runtime catalog; legacy `Faction`/`NameFile` catalog entries no longer
     decide generated sector factions or zone names.
   - Done: move entity faction restore to `Galaxy.ResolveFactionByKey` through
     `EntityConstructionBlueprint.FactionKey`, so typed loadout readback and
     entity construction resolve corporation identity by typed key only. The
     old blueprint `Faction` GUID compatibility field is deleted, and runtime
     entity/faction-relationship commits no longer rebuild faction keys from
     legacy `Faction.ID` GUIDs.
   - Done: delete `Faction.Allegiance`, `Galaxy.ContainsFaction(Guid)`, and
     `Galaxy.ResolveFaction(Guid)`; galaxy/loadout faction relationship logic
     now uses `AllegianceByKey`, `ContainsFaction(string)`, and
     `ResolveFactionByKey` only.
   - Done: delete the temporary `Faction` shell's `GeonameFile` and `BossHull`
     GUID link fields; galaxy name generation and boss-zone eligibility use
     `GeonameFileKey` and `BossHullItemKey`.
   - Done: move temporary `Faction` equality, hashing, narrative constraints,
     sector display filtering, sector-map link/influence rendering, zone
     security ownership checks, and runtime faction-relationship ordering to
     `FactionKey`. The legacy `Faction.ID` projection field is deleted from
     the temporary simulation shell.
   - Done: move package and canonical manufacturer/corporation/name-file lookup
     to typed keys (`ManufacturerKey`, `CorporationKey`, `GeonameFileKey`,
     and `NameFileKey`), and move loadout manufacturer-distance weighting to
     `Galaxy.ResolveFactionByKey`, so loadout code no longer parses
     manufacturer legacy GUIDs to find faction influence.
   - Done: move `LoadoutGenerator` weighted candidate selection, hardpoint fit,
     station bay fit, cargo/capacitor fit, selected-item reuse, hull/category,
     hull type, hull conductivity setup, and behavior-kind prefilters onto typed
     runtime catalog rows; selected item instantiation now receives typed rows
     directly.
   - Done: replace `LoadoutGenerator`'s legacy DTO generic selectors with
     private typed candidate-kind selectors, so loadout code no longer asks for
     `HullData`, `GearData`, `CargoBayData`, or `EquippableItemData` as
     candidate-selection authority.
   - Done: add package-owned `AetheriaRuntimeItemCategories` tokens and move
     runtime category checks off `nameof(ConsumableItemData)`,
     `nameof(HullData)`, `nameof(WeaponItemData)`, and scattered raw category
     strings in gameplay/loadout/docking classification.
   - Done: remap imported item definition `Category` values from legacy union
     class names to stable typed tokens, regenerate `GameData/aetheria-world.cc`,
     and add a verifier guard that fails if item categories contain old `*Data`
     DTO names.
   - Done: delete unused `TradeMenuDebug`; the old debug uGUI trade path no
     longer hydrates typed trade rows back into legacy `ItemData` objects.
   - Done: move surviving trade menu size, hardpoint, and behavior filters onto
     typed catalog row prefilters before row-level `ItemData` hydration.
   - Done: move trade menu row presentation for name, mass, price, size,
     hardpoint type, commodity subtype, hull-owned counts, and behavior-kind
     filtering onto typed `TradeRow` fields.
   - Done: demote TradeMenu behavior filters from `BehaviorData` `Type`
     authority to typed behavior kind keys matched against typed payloads;
     `BehaviorData` reflection remains only for temporary filter labels and
     dynamic column metadata.
   - Done: move trade menu buy price, ship-hull classification, and simple
     commodity stack-size decisions onto typed catalog rows; inventory transfer
     and ship construction are daemon-owned runtime mutations.
   - Done: route trade purchase mutation through typed daemon operations.
     `TradeMenu` still presents typed trade rows and calculates display prices,
     but no longer subtracts credits, transfers purchased cargo, or constructs
     purchased ships directly. The daemon owns the purchase mutation and typed
     run checkpoint.
   - Done: move TradeMenu dynamic behavior columns onto typed behavior payload
     fields; the menu no longer hydrates `ItemData` or `BehaviorData` for row
     display, filtering, sorting, or buy decisions.
   - Done: move action-bar consumable drop binding, activation command, and
     active-duration fill onto typed catalog item IDs; legacy
     `ConsumableItemData` is now only a compatibility fallback because the
     current mapped legacy catalog contains no consumable item rows.
   - Done: move player camera articulation grouping for equipped non-launcher
     weapons onto typed behavior-kind rows instead of runtime `BehaviorData`
     type inspection.
   - Done: import typed thermal bounds and move schematic HUD heat-fill ranges
     onto typed catalog rows.
   - Done: move schematic HUD weapon-row classification onto typed behavior-kind
     rows; runtime `Weapon` instances remain the owner for live ammo/cooldown
     display values.
   - Done: move target HUD hitpoint fill denominator onto typed hull durability;
     live hull item durability remains the numerator/runtime damage owner.
   - Done: import typed item specific heat and move `ItemManager` generic mass,
     thermal-mass, and crafted-price helpers onto typed runtime catalog rows.
   - Done: import typed thermal performance curve keys and move PropertiesPanel
     thermal curve presentation onto typed catalog rows; incomplete typed rows
     use a neutral fallback curve instead of hydrating legacy thermal DTOs.
   - Done: move PropertiesPanel runtime behavior stat display and damage-range
     curves onto typed behavior payload fields, and delete the explicit
     `Inspect(ItemData)` projection inspector after moving trade row inspection
     onto typed runtime catalog rows.
   - Done: replace the PropertiesPanel stat modifier class-name check with the
     package-owned typed `StatModifier` behavior kind.
   - Done: demote PropertiesPanel weapon damage-range payload matching from
     `BehaviorData` `Type` authority to package-owned typed weapon behavior
     metadata.
   - Done: move PropertiesPanel generic behavior stat display off
     `BehaviorData` reflection and onto package-owned typed behavior display
     metadata keyed by behavior kind and payload field key.
   - Done: complete package-owned typed behavior metadata coverage for live
     `Heat`, `MiningTool`, and `Thermotoggle` payload fields plus `Switch` and
     `Trigger` shell kinds. `PropertiesPanel` and `TradeMenu` no longer
     silently drop those typed behavior families, and temperature-bearing
     payload values format through the runtime temperature unit.
   - Done: move TradeMenu behavior filter options, behavior family matching,
     and dynamic behavior columns off `BehaviorData` reflection and onto
     package-owned typed behavior metadata.
   - Done: delete the dead `RuntimeInspectable` attribute and annotations after
     the UI behavior metadata owner moved into the typed runtime package.
   - Done: remove the loot pickup presentation fallback to legacy `ItemData`;
     typed category/name rows now decide pickup visuals and scan labels, and
     missing typed rows render generic unknown gear pickup presentation.
   - Done: import typed hull prefab paths and move `ZoneRenderer` entity scene
     instantiation onto typed hull rows; legacy `HullData` no longer chooses
     zone entity prefabs or station compass classification.
   - Done: move `EntityInstance` hull shape and hardpoint presentation/damage
     masks onto typed catalog rows; legacy `HullData` no longer supplies hull
     shape or hardpoint lists to scene entity instances.
   - Done: move `InventoryPanel` entity-grid hull shape/interior rendering and
     thermal overlay masking onto typed catalog rows; legacy `HullData` no
     longer supplies equipment-grid display geometry.
   - Done: move `EquippedCargoBay` cargo geometry, item fit shape, simple stack
     limits, cargo mass, cargo thermal mass, and `InventoryPanel` cargo-grid
     lowering onto typed catalog rows; legacy `CargoBayData` remains only in
     later simulation projections such as behavior construction.
   - Done: import docking bay max ship size into typed item rows and move
     `EquippedDockingBay.MaxSize` onto the typed runtime catalog row; legacy
     `DockingBayData.MaxSize` is no longer the runtime docking-limit owner.
   - Done: move `EquippedItem.InsetShape` construction onto typed hull and item
     shape rows; legacy `HullData.Shape`/`EquippableItemData.Shape` no longer
     decide equipped-item temperature footprint geometry.
   - Done: move `EquippedItem` and `ConsumableItemEffect` behavior construction
     off `Data.Behaviors` and onto typed runtime behavior payload construction.
   - Done: route `EquippedItem` and `ConsumableItemEffect` behavior instance
     creation through `ItemManager.CreateRuntimeBehaviors`; behavior reads now
     go through `RuntimeBehaviorDefinition`.
   - Done: move the temporary behavior config constructor map from legacy union
     keys to typed behavior payload kind strings; union keys remain migration
     provenance only for this runtime path.
   - Done: move `StatModifier` behavior requirements and behavior-stat targets
     off `EquippableItemData.Behaviors` and old `BehaviorData` subclass
     identity checks. Live behaviors carry typed payload kinds, and
     `StatModifier` matches those kind strings for target and requirement
     selection. The obsolete item-DTO stat target branch is deleted because the
     live equippable item DTO hierarchy has no active `PerformanceStat` fields.
   - Done: move `StatModifier` target and requirement lookup onto the live
     equipped behavior instances. It now modifies the `PerformanceStat` objects
     actually held by runtime behaviors instead of rebuilding temporary config
     DTOs from the catalog.
   - Done: delete the generic `BehaviorPayloadReader.Guid(...)` helper from
     `ItemManager`; migrated behavior payload legacy-ID fields now cross the
     runtime bridge only through the explicit `ItemKey(...)` projection.
   - Done: move `StatModifier` behavior kind/family matching onto
     `AetheriaRuntimeBehaviorMetadataCatalog`; the behavior only normalizes
     migrated `*Data` tokens before asking the package-owned metadata owner.
   - Done: move live behavior identity reads onto `Behavior.Kind` and
     `Behavior.Group`, so state snapshots, weapon snapshots, progress rows,
     stat-modifier matching, and behavior grouping use runtime instance
     identity.
   - Done: move TurretController's weapon range/velocity reads off
     weapon config casts and onto `Weapon.EvaluateRange`/`EvaluateVelocity`;
     copied weapon range and velocity fields are owned by the weapon runtime.
   - Done: move projectile, hitscan, beam, lightning, and mine effect damage
     type reads off weapon configs and onto `Weapon.DamageType`; effect managers
     consume live weapon metadata instead of config DTO fields.
   - Done: move weapon effect prefab lookup and constant weapon visual startup
     off `InstantWeaponData`/`ConstantWeaponData`; `EntityInstance` caches
     managers by `Weapon.EffectPrefab`, and constant effect managers consume
     live `ConstantWeapon` stats.
   - Done: move thruster particle prefab lookup off `ThrusterData`;
     `ShipInstance` now consumes `Thruster.ParticlesPrefab`, so the renderer
     reads live behavior metadata instead of the temporary config bridge.
   - Done: move AetherDrive particle prefab lookup off `AetherDriveData`;
     `ShipInstance` now consumes `AetherDrive.Particles`, and the public
     `DriveData` escape hatch has been removed.
   - Done: move guided missile profile reads off `LauncherData` and
     `GuidedWeaponData`; `GuidedProjectileManager` now consumes
     `Weapon`-owned target mode, curve keys, dodge frequency, and evaluated
     thrust/top-speed values.
   - Done: move HUD ammo display reads off `WeaponData`; `SchematicDisplay`
     consumes `Weapon.AmmoItemKey`, `Weapon.MagazineSize`, and `Weapon.UsesAmmo`,
     and the public `WeaponData` escape hatch has been removed.
   - Done: stop `Weapon`, `InstantWeapon`, `ConstantWeapon`, `ChargedWeapon`,
     and `LockWeapon` from retaining runtime config subclasses; weapon-family
     constructors now copy stats, curves, ammo/reload data, guided projectile
     profile fields, burst/cooldown fields, charge multipliers, and lock
     parameters into runtime-owned fields.
   - Done: delete dormant legacy item audio reads in `EntityInstance` and stale
     commented thruster sound-trigger code; those paths had no live output and
     therefore earned deletion rather than a new runtime metadata owner.
   - Done: move runtime orbit parent/phase/distance/fixed-position reads off
     `OrbitConstructionData`; `Zone`, `ActionGameManager`, and `ZoneRenderer` consume
     `Orbit` runtime properties, and the old `Orbit.ToData()` construction
     capture bridge is deleted.
   - Done: move orbit phase evaluation off `OrbitConstructionData`; runtime zone updates,
     asteroid transforms, and generator rosette spacing now use `Orbit.Evaluate`,
     leaving `OrbitConstructionData` as a construction input only.
   - Done: move orbital runtime entity ownership from the old DTO-shaped
     `OrbitData` scar all the way onto `OrbitKey`; the obsolete `OrbitId`
     intermediate field is gone, and runtime entities now resolve through
     `Zone` instead of exposing wrapper orbit GUID authority.
   - Done: move patrol/tow orbit targets onto typed orbit keys: `PatrolOrbitsTask`,
     `MoveToOrbitState`, and `StationTowing` now retain keyed orbit references,
     and `Zone` owns orbit-key parsing/resolution for agent orbit movement.
   - Done: move live asteroid-belt simulation, scanning, and mesh setup reads
     off `AsteroidBeltConstructionData`; `Zone`, `ResourceScanner`, and `AsteroidBeltUI`
     consume `AsteroidBelt` runtime asteroid/resource/orbit properties, and
     the old `AsteroidBelt.ToData()` construction capture bridge is deleted.
   - Done: move live planet mass/orbit/gravity reads off `BodyConstructionData`; `Zone`
     gravity evaluation and `ZoneRenderer` consume `Planet` runtime properties,
     and the old `Planet.ToData()` construction capture bridge is deleted.
   - Done: move typed zone orbit snapshot projection off
     construction-blueprint orbit rows; run checkpoint commits now project current
     `Zone.Orbits` runtime wrappers into typed orbit snapshot rows.
   - Done: move typed zone body snapshot projection off `BodyConstructionData` and
     `AsteroidBeltConstructionData`; run checkpoint commits now project `Planet` and
     `AsteroidBelt` runtime wrappers, including body resources, asteroid
     runtime damage/respawn/miner accumulators, and gas/sun visual fields.
   - Done: move visited-sector summary counts off construction-blueprint
     body/entity DTOs; `SectorRenderer` now counts planets, belts, gas giants,
     stars, stations, turrets, and ships from `GalaxyZone.Contents` runtime
     wrappers and live entities.
   - Done: delete `GalaxyZone.RuntimeBlueprint` as a persistent zone-side cache;
     unvisited zone generation now feeds the `Zone` constructor directly, and
     visited zone state is owned by `GalaxyZone.Contents`/`Zone.CaptureBlueprint`.
   - Done: delete the `Zone.Planets` `BodyConstructionData` shadow dictionary; orbit
     targeting, resource scanning, and zone capture now read `PlanetInstances`
     and `AsteroidBelts` runtime wrappers, with DTOs retained only as
     construction/capture payloads.
   - Done: move `ZoneRenderer` body classification off `BodyConstructionData` subclasses;
     zone rendering now walks runtime orbits and lowers `Planet`, `GasGiant`,
     `Sun`, and `AsteroidBelt` wrappers directly.
   - Done: remove public `GasGiantConstructionData`/`SunConstructionData` handles from runtime wrappers;
     wave and light calculations now use copied wrapper fields after
     construction.
   - Done: move saved loadout template ownership off `EntityConstructionBlueprint`;
     `ActionGameManager` keeps typed `AetheriaRuntimeLoadoutTemplateSnapshot`
     documents and projects blueprints only for pricing, instantiation, and
     commit boundaries.
   - Done: rename `RuntimeZoneBlueprint` to `ZoneConstructionBlueprint`, delete
     retained construction-payload state from `Zone`, and delete the unused
     construction-capture bridge; `Zone` now keeps radius/mass as copied runtime
     facts after construction.
   - Done: delete the legacy `Behaviors` lists from `ConsumableItemData` and
     `EquippableItemData`; item DTOs can no longer carry behavior config state.
   - Done: move runtime blueprint price aggregation and conductivity restore
     geometry onto typed runtime item price and hull shape rows; legacy
     `ItemData.Price` and `HullData.Shape` no longer own those blueprint paths.
   - Done: move runtime blueprint item cloning off `ItemData` hydration and onto
     typed runtime item rows; `ItemManager.Instantiate` now clones simple
     commodities, crafted items, consumables, and equippables from
     `AetheriaRuntimeCatalogItem` identity and facets.
   - Done: delete dead runtime blueprint behavior-private persistence:
     `PersistedBehaviors`, `IPersistentBehavior`, and `PersistentBehaviorData`
     no longer exist, so behavior state cannot smuggle itself through the
     blueprint projection path.
   - Done: delete the runtime item DTO hydration bridge; `ItemManager.GetData`,
     `ItemManager.Hydrate`, the old projection reader item DTO API, the
     redundant `AetheriaRuntimeItemCatalog` cache wrapper, and
     `RuntimeItemReference.Projection` are gone.
   - Done: move final `Entity.ItemFits`, `TryFindSpace`, `TryEquip`, and
     `TryUnequip` gear occupancy/mass deltas onto typed catalog rows for item
     shape, hull shape/interior, hardpoint masks, cargo/docking category, and
     mass/thermal mass.
   - Done: move `LoadoutGenerator` selected item birth onto typed runtime item
     rows via `ItemManager.CreateEquippableInstance`; generated hulls, cargo,
     docking bays, controllers, capacitors, reused gear, and station inventory no
     longer hydrate `EquippableItemData` projections merely to instantiate.
   - Done: import typed conductivity and hull physical facets, regenerate the
     checked-in `.cc` catalog, and move `Entity.MapEntity`,
     `Entity.UnoccupiedSpace`, grid-offset placement, and
     `Entity.UpdateTemperature` onto typed runtime hull rows.
   - Done: move entity default naming onto typed hull rows and delete the
     entity-level `HullData` property that only preserved legacy name hydration.
   - Done: move unequipped `ItemManager.Evaluate` durability/name inputs onto
     typed runtime item rows; legacy `EquippableItemData.Durability` no longer
     owns that stat-evaluation path.
   - Done: move new equippable instance durability initialization onto typed
      runtime item rows.
   - Done: move `EquippedItem` conductivity and max-durability performance/wear
      inputs onto typed runtime item rows.
   - Done: move `EquippedItem` thermal performance evaluation onto typed
      runtime item thermal bounds and curve keys; missing typed curve/range data
      uses a neutral runtime default.
   - Done: import typed thermal resilience and move equipped-item wear scaling
      off `EquippableItemData.ThermalResilience`; missing typed resilience uses
      a neutral runtime default.
   - Done: import typed audio parameter stat bindings and move equipped-item
      Wwise parameter updates off `EquippableItemData.AudioStats`; missing
      typed audio stat bindings produce no parameter updates.
   - Done: add typed consumable stackability, duration, and effectiveness curve
     fields and route action-bar consumable activation/runtime evaluation
     through typed item rows; verifier records that the current mapped catalog
     has `0` consumable item rows, so future consumable content still needs
     typed coverage.
   - Done: delete `Entity` consumable activation overloads that accepted
     `ConsumableItemData`; active lookup uses item keys and activation commands
     accept typed runtime catalog rows only.
   - Done: delete dead `Entity.GetBehaviorData<T>()` and stale commented
     switch/trigger/axis behavior-query helpers; entity-level behavior queries
     expose live behavior instances, not DTO config.
   - Done: import action-bar icon resource paths into typed item definitions and
     move `ActionBarSlot` custom gear icon lookup onto typed runtime item rows.
   - Done: move `Ship` drag, combat/turret predicted shot height, and thruster
     torque geometry onto typed hull facets and typed shape masks.
   - Done: delete the console `give` command instead of preserving a debug
     operator path that hydrated typed item rows back into legacy item DTOs.
   - Done: delete `ItemManager.GetCatalogEntries<T>` after all live callers
     moved to typed catalog selection.
   - Done: delete `ILegacyItemCatalogReader.GetAll<T>` and the type/global indexes
     inside `LegacyItemCatalogCache`; the legacy cache is now only a pull-fed GUID
     lookup bridge.
   - Done: remove `GameData/NameFile/*.msgpack` from Unity runtime legacy
     catalog boot; typed name files now own runtime names, and old name files
     remain migration inputs only.
   - Done: narrow `ILegacyItemCatalogReader` and `DatabaseLink<T>` to `ItemData`;
     `LegacyItemCatalogCache` ignores non-item entries from `AetherDB.msgpack`.
   - Done: replace `ActionGameManager` cache bootstrap with the typed
     `AetheriaRuntimeCatalogSnapshot`; delete `LegacyItemCatalogBoundary`,
     `LegacyItemCatalogCache`, and the runtime MessagePack deserializer path.
   - Done: demote `DatabaseEntry` and `DatabaseLink<T>` from MessagePack
     union/object shapes to plain runtime identity/link helpers.
   - Done: rename live Unity projection helpers from `DatabaseEntry`,
     `DatabaseLink<T>`, and `InspectableDatabaseLinkAttribute` to runtime
     projection names, and move the old `ServerShared/CultCache` folder to
     `ServerShared/RuntimeProjection`.
   - Done: rename the surviving runtime DTO identity base from
     `RuntimeCatalogEntry` to `RuntimeItemProjectionEntry`.
   - Done: delete the `RuntimeItemProjectionEntry` base after item DTO hydration
     was removed; surviving `ItemData` DTOs own only their local legacy GUID.
   - Done: delete unused `InspectableRuntimeCatalogLinkAttribute` metadata from
     runtime DTOs; link inspection no longer masquerades as catalog authority.
   - Done: delete the empty `RuntimeProjection/Serialization` folder marker and
     unused projection-era attributes (`GlobalSettings`, `RangedFloat`,
     `Tooltip`, `Name`, and the commented `ExternalEntry`) from live Unity
     source.
   - Done: replace behavior union reflection with an explicit runtime catalog
     behavior map and remove all live `Union(...)` annotations.
   - Done: demote agent task runtime shapes from MessagePack object/key/union
     metadata to plain in-memory DTOs.
   - Done: demote legacy player, faction, name-file, galaxy-map-layer, and
     narrative helper DTOs from MessagePack metadata to plain runtime/inspector
     shapes.
   - Done: delete remaining item/behavior DTO MessagePack field metadata and
     the follow-on `LegacyPayloadKeyAttribute`; no live `Assets/Scripts` source
     depends on Newtonsoft, RethinkDB, bespoke save-file serializer symbols, or
     legacy payload-key annotations. Remaining package-level MessagePack usage
     is the typed CultCache `.cc` transport boundary, not a gameplay save
     format.
   - Done: delete dead `PlayerData` and `GalaxyMapLayerData` catalog roots, and
     remove legacy catalog group/table annotations from surviving runtime DTOs.
   - Done: move loadout generation's selected-item instantiation onto typed
     runtime item rows; typed catalog filtering remains the selection owner.
   - Done: remove stale main-menu/editor database vocabulary from Unity UI
     surfaces; galaxy generation now reports typed catalog loading.
   - Done: remove the stale `MessagePack` assembly reference from
     `Aetheria.Shared.Unity`; the remaining MessagePack reference is contained
     in the typed state package's `.cc` reader.
   - Done: convert domain references from GUID/base-class patterns to
     typed record refs. Item instances, cargo inventory indexes, weapon ammo
     references, item-use behavior references, and runtime commit item/hull
     surfaces now carry typed item keys. Runtime behavior resource-scanner and
     mining-tool body references are named and serialized as typed body keys
     through commit/readback state, with the behaviors retaining keyed runtime
     state directly and `Zone` owning key-indexed runtime lookup. Patrol/tow
     orbit task targets now also retain typed orbit keys with `Zone` owning
     orbit-key resolution. Orbital runtime entities and runtime construction
     blueprints now also retain typed `OrbitKey` values, with `Zone` owning
     orbital movement and dock-camera parent-orbit resolution for that seam.
     Renderer, intro-cutscene, and `ResourceScanner` orbit readers now also
     consume the wrapper `OrbitKey` edge instead of wrapper GUID orbit fields.
     `Planet`, `AsteroidBelt`, and `Orbit` runtime wrappers no longer retain
     GUID identity sidecars; they keep `BodyKey`/`OrbitKey`/`ParentOrbitKey`
     plus their runtime object links. `Zone` no longer keeps GUID-to-body
     lookup dictionaries or the `GetBodyKey(Guid)` bridge. Zone construction
     data and generator handoff now carry typed orbit/body keys directly, and
     the remaining agent-task shell placeholders also retain typed string keys
     rather than raw GUID fields. The only surviving GUID use in this lane is
     local key minting during zone generation, not runtime reference authority.
   - Done: delete the `ItemInstance.ItemId` legacy-GUID projection; diagnostics
     now report `ItemKey` so item instances expose only typed item-key identity.
   - Done: delete `Entity`/`EquippedCargoBay` cargo and consumable GUID lookup
     overloads; transfer, count, active-consumable, and first-item APIs now
     expose item-key contracts only.
   - Done: delete the runtime catalog raw-GUID string fallback and unused
     reverse legacy-GUID projections from `AetheriaRuntimeItemReference`; catalog
     lookup accepts canonical item keys only.
   - Done: move action-bar consumable bindings off `Target.LegacyId` parsing;
     they read the typed runtime row `ItemKey` directly for HUD fill/count and
     runtime commit projection.
   - Done: move zone body resource dictionaries from legacy item GUID keys to
     typed item-key strings; runtime body resource commit projection no longer
     calls `FromLegacyId`.
   - Done: move resource scanner targets and mining tool asteroid belts to
     typed body-key runtime commit fields; raw body ID command-document fields
     are deleted.
   - Done: delete the old `ItemData`/`EquippableItemData`/`HullData`/
     `WeaponItemData` DTO hierarchy from live Unity source; shared runtime
     geometry/stat primitives now live in `RuntimeGeometry.cs`.

5. Mesh host
   - Replace the old RethinkDB/LiteNetLib database authority with a
     CultMesh node.
   - Keep any transport bridge only if it delegates to CultMesh and protects a
     named external compatibility contract.
   - Publish Verse descriptors and state subscriptions through CultMesh.
   - In progress: the Unity state client can now open an explicit remote
     replica with `AetheriaRuntimeVerseClient.OpenRemoteAsync`. Snapshot reads
     arrive through CultMesh and non-primary writes route to the daemon shard;
     the local `.cc` scene adapter is no longer described as a live daemon
     transport. The remaining proof is a separately running daemon, observed
     receipts, and PlayMode reconciliation in an EveUnity-owned client.
   - Done: publish committed facts for every applied or rejected daemon command,
     including operation-level rejection. The remote Unity adapter correlates
     those facts by command ID and emits a terminal receipt only after the
     synchronized game surface reaches the fact's source frame.

6. Eve UI
   - Done: publish the typed catalog operator surface from `Aetheria.State`.
   - Done: stage the Eve surface contract DTO package and UI Toolkit lowering
     package as importable Unity packages, then move their shared home into the
     neighboring Eve repo once that worktree was clean.
   - Done: teach the embedded Unity state package to read
     `gamecult.eve.surface` records from the `.cc` store into that shared
     contract.
   - Done: add `org.gamecult.aetheria.eve-runtime` with a `UIDocument`
     presenter that mounts typed Eve surfaces from `GameData/aetheria-world.cc`
     through UI Toolkit without giving the renderer state authority.
   - Done: add the Unity editor `Aetheria/CultUI/Live Composition Surface`
     authoring window. It rebuilds a draft `EveSurfaceDocument` in editor
     memory, lowers it through the shared Eve UI Toolkit path, and emits
     provider-builder code for menu work; it does not publish state, accept
     commands, or become a menu authority.
   - Done: add a shared file-owned debug CultUI surface at
     `GameData/cultui-debug-surface.cultui`. Unity can mount it through
     `AETHERIA_EVE_FILE_SURFACE_PATH` / `AetheriaEveFileSurfacePresenter`;
     Electron reads and watches the same file through IPC and lowers it in the
     RTS side panel. The file owns only debug composition; daemon/provider
     surfaces and command acceptance remain separate authorities.
   - Done: publish renderer-emitted Eve commands as typed
     `gamecult.eve.command.v1` records, separate from runtime state commits so
     only the daemon-owned provider bridge accepts commands it owns.
   - Done: add the provider-owned Eve command bridge that accepts observed Eve commands,
     validates provider/surface/command templates, invokes the current refresh
     handlers, republishes accepted surfaces, rejects unknown commands, and
     records typed command acceptance status.
   - Done: delete provider-side Eve command emission from
     `AetheriaEveCommandBridge`; renderer/runtime requests submit through
     `AetheriaRuntimeEveCommands`, while the provider bridge only validates,
     applies, reports, and deletes accounted commands.
   - Done: replace the old pending-lane private raw MessagePack payload files
     with CultCache-shaped command documents. `AetheriaRuntimeCultCacheDocumentStore`
     owns the temporary envelope writer/reader until the Unity package can use
     generated CultCache serializers directly.
   - Done: extend the command bridge beyond refresh commands for the first
     mutating settings surface; player-settings Eve commands now mutate
     canonical `AetheriaPlayerSettings` and republish the provider-owned
     `aetheria.player_settings` surface.
   - Done: wire the presenter into runtime through `AetheriaEveRuntimeBootstrap`
     so the operations surface mounts as a UI Toolkit surface after scene load.
   - Done: delete the concrete uGUI debug console path: `ConsoleView`,
     `ConsoleController`, the `ARPG.unity` script binding, and
     `ActionGameManager` debug command registrations. Refresh commands now use
     the Eve command bridge; future gameplay/editor commands must add
     provider-owned Eve handlers instead of reintroducing renderer-local text
     command execution.
   - Done: delete the uGUI `FieldTester` debug panel and its
     `FieldShieldTest.unity` script binding. Future field prototype controls
     should be provider-owned Eve/prototype commands or local simulation code,
     not a `PropertiesPanel` mutating `FieldDriver` directly.
   - Done: delete `PropertiesPanel` reflection write authority. The legacy
     inspector can still project object fields for display, but `readWrite`
     mode and `FieldInfo.SetValue` mutation are gone; renderer-local object
     edits must become provider-owned typed commands or local simulation code.
   - Done: route runtime simulation tuning controls through typed daemon
     operations. UI fields for entity override shutdown, per-item override
     shutdown, thermotoggle target temperature, and entity shutdown performance
     now send daemon requests instead of writing simulation objects directly.
     The item override-shutdown bit is part of the typed loadout/entity
     item-slot state so the daemon checkpoint spine can see what the simulation
     sees.
   - Done: route hull conductivity toggles through typed daemon operations.
     `InventoryPanel` requests conductivity edge toggles; the daemon mutates
     `Entity.HullConductivity` and publishes the typed run checkpoint that
     projects `hull_conductivity_x` and `hull_conductivity_y` grids.
   - Done: route inventory entity renames through typed daemon operations.
     `InventoryPanel` may collect the name in a dialog, but the daemon owns the
     entity `Name` mutation and typed entity snapshot checkpoint.
   - Done: route weapon group membership changes through typed daemon
     operations. The old `WeaponGroupAssignment` uGUI shell and its prefab
     chain have been deleted; the daemon owns the membership mutation and typed
     checkpoint outright.
   - Done: route action-bar drag/drop bindings through typed daemon operations,
     and restore them from typed run-state descriptors when the current entity
     changes or a typed continue run rehydrates the session. Runtime UI may
     still lower the action bar, but the daemon owns both the binding mutation
     and the rebind path back onto the live current entity.
   - Done: route inventory double-click transfer and drag/drop placement
     through typed daemon operations. `InventoryMenu` and `InventoryPanel` no
     longer drop items, remove cargo, unequip gear, equip gear, or store cargo
     directly for those UI paths; they ask the daemon to commit cargo-to-cargo,
     cargo-to-equipment, equipment-to-cargo, or equipment-to-equipment movement
     and then refresh display. uGUI still owns the presentation shell for now,
     not the inventory mutation.
   - Done: route docked current-ship selection through typed daemon operations.
     `InventoryPanel` may request a selected docked player ship and update the
     button color, but the daemon owns `CurrentEntity`, `DockingBay.DockedShip`,
     and the typed checkpoint.
   - Done: delete client-owned loot pickup submission. Tractor power influences
     Ymir motion, but only an authenticated Ymir Begin contact fact can ask the
     daemon pickup transaction to mutate cargo or remove a pickup. Unity
     collision callbacks and the retired serialized command payload have no
     writer authority.
   - Done: route entity destruction through typed daemon operations.
     `EntityInstance` observes hull death and may spawn the local destruction
     effect, but the daemon owns equipment/cargo drop decisions, zone entity
     removal, and the typed run checkpoint. Dropped world pickups are now
     projected into typed zone snapshot state and lowered back into live scene
     pickups by exact typed zone record key during zone load.
   - Done: replace the main-menu gameplay/graphics settings screen with a
     locally lowered UI Toolkit projection of the shared
     `aetheria.player_settings` Eve surface contract. The menu shell still owns
     entry/exit flow, but the daemon now owns surface command semantics through
     typed Eve player-settings commands.
   - Done: move the main-menu player-name field into the shared
     `aetheria.player_settings` Eve surface contract. `MainMenu` no longer owns
     a special-case text field; UI Toolkit lowers the shared Eve text control
     and the daemon owns the typed player-settings command.
   - Done: replace the whole main-menu shell with Eve-owned UI Toolkit
     surfaces. Root navigation and settings/input pages now lower through a
     single Eve host instead of the old `PropertiesPanel`/fade shell. The menu
     remains a client-side projection: the Aetheria daemon owns Verse state and
     Unity renders and submits commands. The input page now reports typed state
     and opens the live runtime Eve input screen in the in-game overlay instead
     of the old drag/drop uGUI remap shell. The fake audio page is deleted
     until audio has a typed state owner.
   - Done: replace the live runtime input remap screen with an Eve-owned UI
     Toolkit surface that still calls into Aetheria's low-level InputSystem
     remapping layer through typed player-settings commands.
   - Done: replace the sector-map zone details panel with an Eve-owned UI
     Toolkit surface. `SectorRenderer` still owns reveal/camera behavior, but
     zone inspection no longer rebuilds `PropertiesPanel` rows when the player
     clicks a zone.
  - Done: replace the runtime in-game menu tab strip with an Eve-owned UI
    Toolkit surface. `MenuPanel` now owns tab-content activation and tab
    metadata directly; the old `MenuTabButton` component shell is gone.
   - Done: replace inventory background ship tuning with an Eve-owned UI
     Toolkit surface. `InventoryMenu` still owns click routing and the typed
     daemon operation owns mutation, but the old
     `PropertiesPanel.AddField("Shutdown Threshold", ...)` path no longer owns
     that settings shell.
   - Done: replace the trade target-cargo selector popup with an Eve-owned UI
     Toolkit surface. `TradeMenu` still owns local target-cargo presentation
     state, but the old `ContextMenu.AddOption(...)` popup path no longer owns
     that selector shell.
   - Done: replace the inventory entity/bay/loadout dropdown popup with an
     Eve-owned UI Toolkit surface. `InventoryPanel` still owns local display
     projection while daemon operations own entity/cargo selection plus loadout
     restore/save effects, but the old `ContextMenu` dropdown tree no longer
     owns that shell.
   - Done: replace the remaining `TradeMenu` filter-selector and row-action
     popups with Eve-owned UI Toolkit surfaces. `TradeMenu` still owns local
     filter state and the quantity dialog delegates to typed daemon purchase
     operations, but the old `ContextMenu` dropdown/option tree no longer
     owns any live trade-menu shell.
   - Done: replace `TradeMenu` typed-item inspection with an Eve-owned UI
     Toolkit surface. The surface still projects typed catalog facts from local
     trade rows, but spreadsheet row clicks no longer route through the old
     `PropertiesPanel` shell.
   - Done: replace `InventoryMenu` cargo-item inspection with an Eve-owned UI
     Toolkit surface. Inventory still owns selected-cell highlighting, but
     cargo clicks no longer route through `PropertiesPanel.Inspect`.
   - Done: replace `InventoryMenu` equipped-item inspection with an Eve-owned
     Toolkit surface. `InventoryMenu` now owns weapon-group toggles,
     action-bar group binding, override shutdown, and thermotoggle controls
     directly; the old live-control `PropertiesPanel` shell is gone from the
     inventory path.
   - Done: move the shared `org.gamecult.eve.surface` and
     `org.gamecult.eve.unity-uitoolkit` packages into the Eve repo and retarget
     Aetheria to import them from there instead of carrying local staged
     copies.
   - Done: delete the old IMGUI DB inspector; it is no longer a blocker on the
     live UI migration path.
   - Done: cut `ConfirmationDialog` loose from generic `PropertiesPanel`
     inheritance. Runtime prompts still use local uGUI content prefabs for now,
     but the dialog only owns prompt text and text/integer entry instead of
     serializing the whole dead inspector field zoo.
   - Done: delete the dead generic `PropertiesPanel` / `PropertiesList` /
     `DropdownMenu` shell from live source plus serialized scene/prefab assets.
   - Then replace runtime HUD/menu/inventory/map screens.

7. Purge
   - Done: delete vendored RethinkDB.
   - Done: delete root-level RethinkDB operator query notes; no old Rethink
     runbook remains as a live operational surface.
   - Done: delete JsonKnownTypes.
   - Done: delete Newtonsoft dependencies and attributes from live code.
   - Done: delete the unused Unity Localization package and imported localization
     sample, removing the transitive Unity Newtonsoft package from the package
     lock.
   - Done: delete old JSON backing stores.
   - Done: delete the broken `Economy.Shared` wrapper and tracked build output.
   - Done: disable legacy local save, loadout, zone, player-settings, and
     keyboard layout writers; delete the DB inspector and NameFile export
     control instead of preserving warning-only editor surfaces.
   - Done: delete the old `SavedGame`/`SavedZone` runtime save DTO and loader.
   - Done: remove the stale Unity `SaveState`/`SaveZone` command names after
     their bespoke serializers were deleted.
   - Done: replace warning-only player settings, loadout, shutdown, and
     wormhole save paths with typed Eve/daemon command records accepted by the
     Verse daemon.
   - Done: delete the `.cc.pending` runtime commit lane and its private raw
     MessagePack array protocol; canonical writes now happen behind daemon
     command acceptance.
   - Done: extend run checkpoint commands to carry current-zone and entity
     snapshots into canonical `AetheriaZoneState` and `AetheriaEntitySnapshot`
     documents.
   - Done: delete `Aetheria.State.DrainCommands`; bounded acceptance now uses
     `Aetheria.State.Daemon --once` so the Verse daemon remains the only command owner.
   - Done: move Eve request acceptance into `Aetheria.State.Daemon` startup and
     daemon ticking.
   - Done: publish Eve request acceptance health as typed CultCache state and an
     Eve operations surface.
   - Done: publish Eve command acceptance health as typed CultCache state and include
     it in the operations surface.
   - Done: move daemon Verse/service identity into typed
     `aetheria.verse_host_settings.v1`, with provider advertisement and
     operations telemetry deriving from that daemon-owned host document.
   - Done: publish provider-owned player-settings controls as a typed Eve
     surface and advertise their command boundary through the provider ad.
   - Done: publish an Eve provider advertisement so Odin/Eve can discover
     Aetheria surfaces and command boundaries through typed state.
   - Done: make the daemon/provider advertisements derive from the shared
     `AetheriaRuntimeEveSurfaceCatalog`, so Odin/Hermodr can index every known
     Aetheria Eve panel instead of only the daemon game/editor surfaces.
   - Done: stop legacy catalog pull/read paths from writing entries back to
     their source backing store.
   - Done: delete legacy catalog backing-store write/realtime APIs.
   - Done: delete public legacy catalog cache mutation APIs.
   - Done: delete the stale `StrategyGameManager.csbak` backup source and
     unused Unity asset MessagePack formatters.
   - Done: demote keyboard layout DTOs from MessagePack shapes, delete the
     global scene-load MessagePack resolver hook, and remove the now-unused
     Unity-side custom MessagePack resolver/formatter source.
   - Done: demote `PlayerSettings` and nested settings from MessagePack shapes
     after the legacy settings writer was disabled.
   - Done: delete the process-global runtime catalog resolver from
     `RuntimeCatalogLink<T>`; item instances now carry item-definition
     identifiers and `ItemManager` owns hydration through the typed runtime
     catalog reader.
   - Done: collapse the old generic `RuntimeCatalogLink<T>` abstraction into
     `RuntimeItemReference`, an item-specific runtime projection reference owned
     by `ItemManager`.
   - Done: demote `RuntimeItemReference.Value` to an item-definition id, then
      delete `RuntimeItemReference.Projection`; item references no longer carry
      hydrated `ItemData` DTOs.
   - Done: add `ItemInstance.ItemId` as an intermediate runtime identity
     surface and move live runtime/UI/catalog lookup call sites off
     `.Data.ItemId`. The old `Data` field became compatibility backing state
     before it was deleted.
   - Done: add `ItemInstance.Reference` as the explicit item-reference API and
     move factories/cargo stacking off direct `.Data` comparisons and
     assignments.
   - Done: rename `RuntimeItemReference` to
     `RuntimeItemDefinitionReference` and delete the `ItemInstance.Data`
     compatibility field; this was the intermediate reference shape before
     typed item keys became the owner.
   - Done: rename `RuntimeItemDefinitionReference` to
     `AetheriaRuntimeItemReference` and make the reference typed-key-first.
     `ItemInstance.ItemKey` is now the primary identity surface; `ItemId` is
     derived legacy compatibility for unfinished cargo and simulation paths.
   - Done: move `EquippedCargoBay.ItemsOfType`, consumable activation lookup,
     action-bar consumable quantity/fill, item transfer lookup, and trade owned
     counts from derived `Guid ItemId` keys to typed `ItemKey` strings.
   - Done: move weapon ammo and `ItemUsage` config references from GUID fields
     to typed item-key fields. The derived `AmmoType` and `Item` compatibility
     properties have been deleted; runtime ammo consumption, item use, and HUD
     ammo counts use `AmmoItemKey`/`ItemKey`.
   - Done: make runtime commit item surfaces item-key-only through
     `ItemKey`/`HullItemKey`; stale legacy-ID commit fields were deleted.
     Loadout items, action-bar targets, body resources, entity hulls, cargo
     contents, and active consumables now prove typed-key ownership in the
     Unity smoke without any item legacy-ID commit fields.
   - Done: add package runtime catalog lookup by canonical item key and move
     Unity gameplay, HUD, inventory, trade, ship, and zone helper lookups off
     `ItemId`/`FindItemByLegacyId` detours. The Unity runtime package catalog
     snapshot no longer exposes item/corporation/name-file legacy-ID lookup;
     legacy lookup remains only as a canonical `Aetheria.State`
     catalog/migration inspection API.
   - Done: move server-shared weapon grouping, generated loadout previous-item
     reuse, and hauling task item target identity off derived item GUIDs and
     onto typed `ItemKey` strings.
   - Done: delete the shared runtime item catalog GUID index, `GetRuntimeItem(Guid)`
     reader path, `ItemManager` item-ID fallback, and unused
     `AetheriaRuntimeItemReference(Guid)` constructor. Runtime item lookup is
     item-key-only outside explicit compatibility projection helpers.
   - Done: delete the dead `ItemManager.GetRuntimeItemProjection<T>` bridge
     after loadout generation stopped hydrating selected typed rows into
     `EquippableItemData` merely to instantiate equipment.
   - Done: rename the surviving runtime item read port from
     `IRuntimeItemProjectionReader` to an item-key-only typed catalog lookup,
     and rename behavior config methods to temporary behavior config construction so
     old projection vocabulary no longer implies item DTO authority.
   - Done: delete the temporary behavior-config reader path; the
     runtime catalog lookup now exposes typed item rows only.
   - Done: rename the temporary behavior construction DTO family from
     `BehaviorData`/`*Data` to `RuntimeBehaviorConfig`/`*Config`; live
     `Behavior` instances expose typed kind/group values while typed behavior
     kind strings such as `Cockpit`, `TurretController`, and `Capacitor` remain
     stable catalog facts rather than class-name-derived selectors.
   - Done: remove the public `Behavior.Config` runtime API; stat modifiers ask
     behavior instances for target performance stats instead of reflecting over
     exposed construction config objects.
   - Done: replace `ItemManager` behavior config reflection with an explicit
     typed behavior-kind mapper.
   - Done: remove runtime config retention from the remaining weapon family;
     all behavior subclasses now copy constructor config inputs into explicit
     runtime fields instead of keeping `RuntimeBehaviorConfig` subclass
     instances alive as state.
   - Done: delete the no-payload `CockpitConfig`, `HeatStorageConfig`,
     `SwitchConfig`, `TriggerConfig`, and `TurretControllerConfig` classes.
     `ItemManager` now constructs those behavior kinds directly from typed
     runtime behavior definitions rather than routing them through the
     temporary config bridge.
   - Done: delete the simple field-bearing `HeatConfig`, `EnergyDrawConfig`,
     `CooldownConfig`, `WearConfig`, `VisibilityConfig`, `ReflectorConfig`,
     `VelocityLimitConfig`, `VelocityConversionConfig`, and `ItemUsageConfig`
     classes. `RuntimeBehaviorDefinition` now owns the typed field reads for
     those behavior kinds, and the live behaviors explicitly register their
     performance stats for `StatModifier`.
   - Done: delete `CapacitorConfig`, `ShieldConfig`, `MiningToolConfig`,
     `ResourceScannerConfig`, `ThermotoggleConfig`, and `ThrusterConfig`.
     `RuntimeBehaviorDefinition` reads their typed stats, scalar flags, and
     prefab path directly, and live stat-bearing behavior instances explicitly
     register the performance-stat names used by `StatModifier`.
   - Done: delete `ReactorConfig`, `RadiatorConfig`, `SensorConfig`, and
     `StatModifierConfig`. `RuntimeBehaviorDefinition` now reads their typed
     stats, curves, enum, scalar, and stat-reference fields directly, and live
     behavior instances explicitly expose the stat names used by
     `StatModifier`.
   - Done: delete `AetherDriveConfig`. `RuntimeBehaviorDefinition` now reads
     rotor geometry, RPM/coupling/torque/energy/passive-coupling stats, torque
     curve, audio parameters, and particle prefab path directly.
   - Done: delete the weapon config hierarchy and the last
     `RuntimeBehaviorConfig` bridge. Weapon-family behavior construction now
     reads common stats, instant/constant/charged/lock fields, and guided
     projectile profiles directly through `RuntimeBehaviorDefinition`;
     `BehaviorPayloadReader`, `BuildBehaviorConfig`, and config reflection are
     gone from live Unity source.
   - Done: delete `LegacyPayloadKeyAttribute` from live Unity source after
     importer constants and explicit typed mappers became the only migrated
     field-key authorities.
   - Done: quarantine the vendored Unity `MessagePack` assembly by disabling
     asmdef auto-reference; only explicit state-spine assemblies should see it
     while the Unity CultCache bridge still needs a low-level `.cc` codec.
   - Done: demote `ZonePack`/`EntityPack` save-payload names to
     construction blueprint vocabulary, and rename loadout collections away
     from save-payload vocabulary. These are now explicitly construction
     projections, not portable state authority.
   - Done: continue the zone demotion by renaming `RuntimeZoneBlueprint` to
     `ZoneConstructionBlueprint`; the old intermediate name no longer appears
     in live Unity source.
   - Done: rename the old `EntitySerializer` helper to
     `EntityConstructionBlueprintProjector`; it captures/instantiates
     construction blueprint projections and no longer presents itself as a
     serializer or runtime-state owner.
   - Done: delete the unused `Zone.AddOrbit(OrbitConstructionData)` construction-row
     writer; zone orbit construction flows through `ZoneConstructionBlueprint`
     only.
   - Done: rename zone body/orbit construction DTOs to
     `BodyConstructionData`/`OrbitConstructionData` and move `ZoneData.cs` to
     `ZoneConstructionData.cs`; the construction blueprint now exposes
     `Bodies` rather than a planet list that also contained belts, gas giants,
     and suns.
   - Done: demote Unity's live `PlayerSettings` runtime object to
     `RuntimePlayerSettings`; `AetheriaPlayerSettings` remains the typed Verse
     state document owner, while Unity only keeps a session projection and
     sends typed player-settings commands.
   - Done: route input-screen binding/action-bar edits through named typed
     player-settings commands on the Unity runtime boundary, so the UI is
     no longer a direct writer of input binding or action-bar collections.
   - Done: route main-menu graphics settings returns through the same typed
     player-settings command path as gameplay settings, so graphics edits
     no longer survive only as session-local `RuntimePlayerSettings`.
   - Done: route main-menu player name, gameplay formatting, and graphics
     preference edits through typed Eve player-settings commands. MainMenu
     reads the `RuntimePlayerSettings` projection for display, but no longer
     assigns its player/gameplay/graphics fields directly.
   - Done: lower the shared `aetheria.player_settings` Eve surface contract
     directly inside `MainMenu` through UI Toolkit. The menu no longer owns a
     second bespoke gameplay/graphics widget tree for those settings; it lowers
     the shared document and dispatches command ids through typed Eve commands.
   - Done: delete `ItemManager`'s unused zone dictionary, commented corporation
     controller/galaxy-zone GUID caches, force-load GUID sketch, and dead time
     property sketch. ItemManager no longer pretends to own a zone/time cache
     while the runtime state spine owns durable state.
   - Done: delete the redundant `AetheriaRuntimeItemCatalog`/`IRuntimeItemCatalogReader`
     adapter layer. `ItemManager` now reads item rows straight from the shared
     typed runtime catalog snapshot instead of rebuilding a duplicate lookup
     cache over the same item-key index.
   - Done: delete the dead runtime-projection reflection caches. `ReflectionExtensions`
     now keeps only the live string-formatting helpers used by the surviving
     typed UI surfaces; it no longer scans loaded assemblies or keeps static
     type-cache dictionaries for the old inspector/projection path.
   - Done: delete the dead inspector-specialization metadata. Unused
     header/order/preferred-inspector attribute types and their stale
     annotations are gone.
   - Done: delete the generic `PropertiesPanel` reflection inspector fallback
     and the last `Inspectable`/`EntityTypeRestriction` attribute cargo cult.
     Live item inspection stays on the typed item/property paths; there is no
     remaining runtime object walker generating UI from reflection metadata.

## Verification

- `rg "Newtonsoft|JsonObject|JsonProperty|JsonConvert|JsonKnownTypes|RethinkDb|RethinkTable|DatabaseCache"` is zero outside migration quarantine and docs.
- `rg "DatabaseEntry|DatabaseLink|InspectableDatabaseLink|ServerShared\\\\CultCache" Assets Economy.*` shows no live runtime ownership path.
- `AetheriaRuntimeEnergySimulation` owns the equipped capacitor/reactor/radiator
  network. `AetheriaRuntimeThermalSimulation` owns catalog-derived cell topology,
  equipment-local temperature/performance, shutdown, wear, conduction, and
  radiation. The daemon orders radiator pumping before reactor settlement and
  thermal conduction; client operations have no direct energy, heat, wear, or
  equipment-online writer. `AetheriaRuntimeThermalMedicalSimulation` derives
  cockpit exposure, recovery, severe-risk crossings, and thermal death from the
  cockpit's canonical cells; all death causes use the same destruction commit.
  The Eve game surface publishes the resulting power, radiator, temperature,
  wear, offline-equipment, cockpit, risk, render-weight, and typed death facts.
  EveUnity now lowers the original severe heatstroke pulse and retained thermal
  death chronology into a renderer-neutral native sink frame, including an
  immediate settled result on reconnect rather than replaying stale death.
  Aetheria deterministically translates the five broken legacy thermal/death
  profiles into clean URP profiles and advertises those generated assets through
  its CultMesh CDN catalog. EveUnity resolves arbitrary typed native assets by
  semantic role, and the canonical client sink only binds profiles and applies
  normalized weights. Legacy SSR/AO/roundness equivalents, rotated equipment
  footprints, parent/child radiation, schematic composition, canonical
  render-pipeline activation, and rendered capture remain migration work rather
  than presentation authority.
- `dotnet list package --vulnerable --include-transitive` is clean for active
  maintained projects.
- CultCache smoke proves write, flush, reopen, query, and typed reference
  resolution, including full player settings as the `PlayerSettings.msgpack`
  replacement, loadout templates as the `.loadout` replacement, and a run ->
  zone -> entity snapshot graph as the `.zone` replacement. The zone smoke
  includes orbit/body rows for generated celestial state, and the entity smoke
  includes typed simulation stat grids, public entity session scalars, and
  active consumable timer rows, plus typed behavior progress rows for
  `IProgressBehavior` surfaces, typed weapon runtime rows, and typed
  sensor/radiator/reactor/capacitor behavior state rows. The Unity runtime smoke also proves
  package-owned readback of typed loadout templates from `.cc` records. It also
  proves `aetheria.runtime_session.v1` is advertised and survives reopen as the
  typed daemon-session signal.
- CultMesh smoke proves node start, typed put/get, subscription, flush, and
  reopen.
- Eve surface smoke proves provider-owned surface documents are generated from
  typed state and survive `.cc` store reopen before any renderer owns them.
- Quarantine import smoke proves old catalog file facts can be captured into
  typed state without old readers remaining on the live runtime path.
- Migration smoke proves stable old catalog payload fields can be read once and
  converted without old readers remaining on the live runtime path.
- Unity runtime catalog smoke proves read-only catalog consumers can open the
  typed `.cc` store through both the SDK-style `Aetheria.State.Unity` opener and
  the embedded package-owned read-only catalog opener, receiving the same
  package-owned runtime catalog read models without a MessagePack catalog cache,
  including typed item masks, interior masks, hardpoint definitions,
  corporation/name-file links, typed behavior payloads, and dropped world
  pickup rows in zone snapshots. It also proves the
  embedded package can read provider-owned Eve surface documents from the same
  CultCache store. The smoke proves legacy runtime snapshots can be
  imported into canonical typed settings/run/zone/entity state and cleared
  afterward, while renderer-emitted Eve commands stay typed command documents
  outside that legacy import lane. The smoke now inspects pending mailbox bytes
  directly and verifies each pending file is a CultCache store snapshot with a
  schema catalog and exactly one typed record.
- Unity batchmode compile with Editor `6000.4.2f1` is currently available and
  should be rerun after package/runtime C# edits; Unity may leave generated
  package metadata and project files that must be cleaned before commit.
- Direct `dotnet build .\Assembly-CSharp.csproj --no-restore` is not currently
  a valid gameplay compile substitute: the generated Unity project references a
  missing root-level `GameCult.Aetheria.State.Unity.csproj` instead of the live
  package/state project path, so it fails before reaching the gameplay rename
  checks.
- Unity batchmode compile with Editor `6000.4.2f1` returned cleanly after the
  runtime catalog resolver cut; `Logs/codex-unity-compile.log` has no compiler
  error hits.
- `rg ".Data.Value|SetValue|GetCatalogEntry|IRuntimeItemCatalogReader|AetheriaRuntimeItemCatalog|BindRuntimeItemCatalog|ResolveRuntimeItemCatalog|private static IRuntimeItemCatalogReader"` in
  `Assets/Scripts` is zero for the old item value/catalog-entry/resolver path.
- Live Unity source has no `RuntimeCatalogLink<T>` or `RuntimeCatalogLinkBase`;
  item instances expose `ItemId` and `Reference`; the old `ItemInstance.Data`
  identity field is deleted.
- Live Unity source has no `RuntimeCatalogEntry` or `RuntimeItemProjectionEntry`.
- Live Unity source has no `InspectableRuntimeCatalogLinkAttribute` or
  `LegacyPayloadKeyAttribute`.
- Unity batchmode compile also returned cleanly after disabling `MessagePack`
  auto-reference; `Assets/Scripts` and `Assets/Editor` have no `using
  MessagePack`, `MessagePackSerializer`, `[MessagePackObject]`, or
  `IMessagePackFormatter` hits.
- `Aetheria.State.Verify` now enforces the gameplay-source purity invariant:
  live `Assets/Scripts` cannot contain MessagePack, Newtonsoft,
  JsonKnownTypes, RethinkDB, old database-link symbols, or serializer metadata.
  Package/import MessagePack usage remains explicitly bounded to CultCache
  transport and migration.
- `Aetheria.State.Verify` also fences the Unity package serializer boundary:
  MessagePack symbols may appear only in typed CultCache transport and document
  shapes until CultLib generated serializers become the Unity runtime owner.
- `Aetheria.State.Verify` also guards runtime faction identity ownership:
  construction blueprints cannot carry a legacy faction GUID, and runtime
  entity/faction commits cannot rebuild `FactionKey` from `Faction.ID`.
- `Aetheria.State.Verify` guards galaxy faction relationships as typed-key-only:
  no `Faction.Allegiance` GUID dictionary, GUID faction containment, GUID
  faction resolver, or `CorporationLegacyId` relationship read may return to
  the live `Galaxy`/`LoadoutGenerator` path.
- `Aetheria.State.Verify` guards Unity runtime catalog lookup authority:
  package snapshots may expose typed-key lookup only, and runtime
  item/corporation/name-file DTOs may not publish legacy relationship fields;
  legacy-ID lookup remains quarantined in canonical migration/catalog
  inspection APIs.
- `Aetheria.State.Verify` guards behavior body reference naming:
  resource-scanner and mining-tool runtime state surfaces use `BodyKey` fields;
  old `*BodyId` readback names and generic legacy reference parsers may not
  return. `Zone`, `Planet`, `AsteroidBelt`, and `Orbit` now own the runtime
  body/orbit key surfaces consumed by `ActionGameManager` projection, so local
  body/orbit key formatters may not return there either.
- `Aetheria.State.Verify` guards orbit-targeting agent runtime naming:
  patrol/tow task orbit references and move-state orbit targets use `OrbitKey`
  fields, and `Zone` owns orbit-key parsing/resolution instead of agent-local
  raw GUID targets.
- `Aetheria.State.Verify` guards temporary `Faction` shell links: geoname and
  boss-hull relationships use typed keys, not legacy GUID projection fields.
- `Aetheria.State.Verify` guards temporary `Faction` identity: equality,
  hashing, ordering, narrative checks, sector filtering/rendering, and zone
  security checks must use `FactionKey`; the legacy `Faction.ID` projection
  field may not return.
- Unity batchmode compile returned cleanly after the runtime blueprint rename;
  live Unity source has no `EntityPack`, `ShipPack`, `OrbitalEntityPack`,
  `ZonePack`, `PackedContents`, `PackZone`, `EntitySerializer.Pack`, or
  `EntitySerializer.Unpack` hits.
- Entity construction blueprint projection no longer uses the `EntitySerializer`
  or runtime-state authority names; live Unity source calls
  `EntityConstructionBlueprintProjector`.
- Vendored Unity `MessagePack` remains a low-level codec boundary only; its
  typeless contractless resolver now follows the same `NET_STANDARD_2_0`
  dynamic-resolver guards as `StandardResolver`, so Unity batchmode cannot
  resurrect unsupported typeless dynamic code while compiling the quarantined
  assembly.
- Vendored Unity `MSAGL` debug timing remains diagnostic-only; `TimeMeasurer`
  now requires the same `REPORTING` symbol that defines
  `Microsoft.Msagl.DebugHelpers.Timer`, so `TEST_MSAGL` cannot accidentally
  summon a missing debug dependency during Unity compilation.
- Unity runtime settings projection is now `RuntimePlayerSettings`; live Unity
  source has no standalone `PlayerSettings` class/property/method symbols.
- Input binding and action-bar edits now send the same typed player-settings
  command path as menu settings changes; the old `SaveLayout` runtime-only
  warning is gone. The generated `AetheriaInput` class still calls Unity's
  `InputActionAsset.FromJson`, but that JSON belongs to Unity's generated input
  action lowering under Aetheria's remapping system. It is not durable Aetheria
  state and does not own remapping authority.
- Action-bar slot binding drag/drop now queues its own `action-bar-binding`
  typed run checkpoint immediately, and typed continue-run restore rebuilds slot
  bindings onto the live current entity instead of leaving stale `ActionBarBinding`
  instances pointed at the previous entity body.
- Main-menu settings navigation, input, and audio pages now lower through a
  shared UI Toolkit `UIDocument` host and local Eve surface documents instead
  of rebuilding those pages through `PropertiesPanel.AddButton(...)` calls.
- Sector-map zone inspection now lowers through a shared UI Toolkit
  `UIDocument` host and a local Eve surface document instead of rebuilding
  `PropertiesPanel.AddProperty(...)` rows on each zone click.
- Runtime in-game menu tab navigation now lowers through a shared UI Toolkit
  `UIDocument` host and a local Eve surface document, with `MenuPanel`
  owning tab metadata directly instead of `MenuTabButton` components.
- Inventory background ship tuning now lowers through a shared UI Toolkit
  `UIDocument` host and a local Eve surface document instead of rebuilding a
  mutable `PropertiesPanel.AddField(...)` control for shutdown threshold.
- Trade target-cargo selection now lowers through a shared UI Toolkit
  `UIDocument` host and a local Eve surface document instead of rebuilding a
  `ContextMenu.AddOption(...)` popup for docking-bay and ship-bay choices.
- Inventory entity/bay/loadout dropdown navigation now lowers through a shared
  UI Toolkit `UIDocument` host and a local Eve surface document instead of
  rebuilding nested `ContextMenu.AddDropdown(...)` and `ContextMenu.AddOption(...)`
  menus.
- The dead `ContextMenu` popup shell, its prefab assets, and orphan
  scene/prefab references are deleted outright; `InventoryPanel`,
  `ActionGameManager`, `TradeMenu`, and `SectorRenderer` no longer keep
  serialized hooks into dead popup or properties-panel shells.
- Trade filter selection and simple-commodity buy-quantity entry now lower
  through shared UI Toolkit `UIDocument` hosts and local Eve surface documents
  instead of rebuilding `ContextMenu.AddDropdown(...)`, `ContextMenu.AddOption(...)`,
  and `ContextMenu.Show()` shells in `TradeMenu`.
- Trade typed-item inspection now lowers through a shared UI Toolkit
  `UIDocument` host and a local Eve surface document instead of routing
  spreadsheet row clicks into `PropertiesPanel.Inspect(...)`.
- Inventory cargo-item inspection now lowers through a shared UI Toolkit
  `UIDocument` host and a local Eve surface document instead of routing cargo
  clicks into `PropertiesPanel.Inspect(...)`.
- Inventory equipped-item inspection now lowers through a shared UI Toolkit
  `UIDocument` host and a local Eve surface document, with `InventoryMenu`
  owning weapon-group toggles, action-bar group binding, override shutdown,
  and thermotoggle controls directly instead of `PropertiesPanel.Inspect(...)`.
- Keyboard display layout parsing is no longer JSON-backed:
  the runtime input screen builds its action-bar candidates and rebinding UI
  from typed input paths, and `InputLayout` remains the typed ANSI-104 keyboard
  geometry helper instead of a JSON text asset. The dead commented Ink `ToJson`
  write path and checked-in `ansi104.json` display file have been removed from
  live source.
- `Aetheria.State.Smoke` proves the provider-owned Eve command bridge accepts
  `gamecult.eve.command.v1` envelopes, accepts advertised refresh commands plus
  player-settings mutation commands, rejects unknown commands, persists
  `AetheriaEveCommandAcceptanceStatus`, and exposes Eve command acceptance through the
  operations surface while preserving the `aetheria.player_settings` surface.
- Unity play smoke proves runtime UI reads from Eve surfaces and sends commands
  through the shared state service.
- UI Toolkit lowering parity compares Aetheria surfaces against the Eve browser
  renderer for component tree, state bindings, command envelopes, disabled/stale
  states, and visible error surfaces.

## Immediate Cut Line

Do not add behavior to `ItemData` or resurrect `RuntimeItemProjectionEntry`.
The typed state spine, direct behavior factories, and migration quarantine
exist; the next cuts should remove remaining live predicates that still need
legacy DTO vocabulary, keep package-level MessagePack/JSON at explicit
boundaries, and replace runtime/operator UI truth with Eve surfaces. The old
Do not restore the deleted generic `PropertiesPanel` reflection inspector as a
shortcut around typed command/state ownership. Runtime simulation
tuning controls may remain temporarily on uGUI only when they delegate to typed
daemon operations; direct UI mutation of entity/item/behavior settings is
obsolete authority. Hull conductivity changes follow the same rule: UI requests
the toggle, and the daemon owns the grid mutation and checkpoint. Entity renames
follow the same rule: UI collects text, and the daemon owns the name mutation
and checkpoint. Weapon group assignment follows the same rule: UI requests
membership, and the daemon owns the group mutation and checkpoint. Inventory
double-click transfer follows the same
rule, and drag/drop placement now shares that commit family: UI requests
cargo/equipment movement, and the daemon owns the move and checkpoint. Trade
purchases follow the same rule: UI requests only item, quantity, stock slot,
and destination bay. The daemon derives the dock parent, current catalog price,
product kind, capacity, hull-vs-cargo result, credit changes, cargo transfer,
ship creation, and checkpoint. Historical price, station, target, purchase-kind,
and `CreatesDockedShip` fields remain serialization tombstones only; live clients
do not write them and daemon operations do not read them. Runtime loadout
restore follows the same rule: UI requests restoration, and the daemon owns
instantiation, credits, dock assignment, current entity, and checkpoint. Docked
current-ship selection follows the same rule: UI requests selection, and the
daemon owns `CurrentEntity`, `DockingBay.DockedShip`, and checkpoint. Undocking
also owns a collision-free departure pose and clears inherited velocity before
the detached ship re-enters world physics. Loot pickup is contact-gated: the
tractor applies force to loose bodies, Ymir owns typed Begin contact facts, and
the daemon alone interprets each persisted fact identity into capacity
validation, cargo storage or rejection bounce, pickup removal, feedback, and
checkpoint. Targeting, proximity, client commands, and presentation collisions
cannot decide inventory ownership. The portable Eve entity SoA publishes
`inventory.cargo.quantity`, allowing generic clients and witnesses to observe
the committed cargo delta without reading Aetheria internals. Entity destruction follows
the same rule: instance code observes death, and the daemon owns drop
decisions, zone removal, and checkpoint. Dropped world pickups are typed
zone-snapshot state, not only
renderer-local objects; live lowering consumes the exact typed zone record key
instead of rehydrating a parallel presentation list. Any
predicate that still needs legacy DTO objects must earn that dependency by
using behavior objects or simulation-only methods that typed facets do not yet
expose.

The released-package proof keeps two lifecycle claims separate. The
`cold-start-lowering` profile begins with an empty cache, receives and verifies
the 56,168,017-byte provider body through `cultmesh.content.v1`, promotes the
exact hash with no partial, negotiates `SharedFileMapping` over the exact
transfer-owned file, and proves generic world, field-volume, movement,
environment, and pilot/map camera lowering. The `full-session-gameplay` profile
starts a fresh warm daemon session, begins with no pickup, destroys the
deterministic salvage target, observes its canonical cargo drop, and collects
it exactly once from a persisted Ymir Begin fact. Witness runners use the
daemon's native 20 ms wall interval for its 20 ms simulation delta; download
latency is not allowed to redefine transient world lifetime. Managed network
delivery and verified local file mapping are proven. Provider-to-client network
zero-copy, shared-memory delivery, and GPU zero-copy remain CultMesh/runtime
work. The fog contract and provider shader execute. Terminus
now publishes its blue fog tint from the daemon-owned sun visual rather than an
unrelated brown calibration; the generic runtime applies the same field ABI.
The warm witness records `225,293` changed pilot pixels, average luminance
`0.11927`, 241 volume composites, intact pilot/map isolation, and the same
authoritative destruction-loot path. The current volume still reads as a broad
blue shelf against sparse world geometry, so patch readability, gravity-wave
readability, surrounding geometry, and final luminance/composition remain open
visual parity work.

Generated loadouts accept no scenario-authored cargo strings. Catalog selection
is the only loadout item-key owner: ordinary ships begin without generated
cargo, stations draw typed faction-available inventory, and both cargo slots and
selection receipts carry the same canonical catalog keys. Scenario flavor may
later select a typed loot table or authored item key, but it cannot silently
invent identity. `scripts/verify-aetheria-daemon.ps1` restores and runs the
daemon smoke with one explicit CultLib/EveUnity/Ymir root set so a stale NuGet
project graph cannot substitute another worktree during `--no-restore`.
The unused Starbridge bootstrap that wrote invented stock, attacker, boss, and
technology keys has been deleted. A future Starbridge mode must enter through
the active daemon game-mode owner and typed catalogs; a dormant document seeder
is not a game mode.

Ymir restart ownership is deliberately private and asymmetric:

- Owner: `AetheriaYmirPersistenceCoordinator` owns reconstruction durability;
  the public daemon frame still owns committed gameplay time.
- Inputs: an immutable completed-tick Ymir capture, the prior durable journal
  cursor, and the exact public `(RunId, FrameId, ZoneIndex)` boundary.
- Outputs: checksummed immutable journal chunks and immutable bounded resume
  records in `<state>.ymir.cc`.
- Derived state: native Box3D worlds and handles are reconstructed process
  state. Resume records are recovery inputs, not client world truth.
- Forbidden writers: the public Aetheria CultCache, CultMesh subscriptions,
  Eve surfaces, Unity, and broad public snapshots cannot store, enumerate, or
  select private replay history.
- Shared path: first publication and periodic publication both capture before a
  later simulation tick, flush private chunks, flush private resumes, publish
  and hard-flush the public frame, then activate fail-closed restart.
- Cut line: the rejected design that embedded complete Ymir checkpoints in
  every public zone/frame is absent; no public persistence document types are
  registered.
- Verification layer: daemon smoke tears down a live rejected-pickup contact,
  restores the exact durable frame into fresh Ymir sessions, and proves the
  next tick cannot duplicate its receipt, feedback event, or kick. Ymir unit
  tests prove incremental suffix capture, exact chunk coverage, checksums, and
  replay verifier convergence.

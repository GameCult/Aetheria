# Original Game To Perfect Machine Matrix

Status dimensions:

- `reference`: behavior exists only in the fossil/reference implementation;
- `owner-exists`: target subsystem implements the primitive in isolation;
- `wired`: production Aetheria paths invoke that owner;
- `exclusive`: obsolete writers cannot decide or repair the result;
- `projected`: authoritative state reaches Eve;
- `lowered`: a generic runtime presents or controls it;
- `proven`: live parity and negative-authority checks pass.

Dimensions are cumulative claims only when evidence proves each one. Fixture
validation proves contract shape, not wiring, exclusivity, lowering, or parity.

| Organ | Original authority/evidence | Target owner | Eve contract | Runtime lowering | Current status | Required proof |
|---|---|---|---|---|---|---|
| Run/session state | `ServerShared`, `ActionGameManager` | Aetheria daemon/CultCache | provider/session advertisement | all clients | owner-exists; wired; projected; not proven | restart and multi-client convergence |
| Entity identity/loadout | `ServerShared/Entity*` | daemon | typed entity and inventory surfaces | EveUnity/Electron | owner-exists; wired; projected; not proven | loadout round trip and reconnect |
| Loadout generation | map/faction availability, item cost, hull hardpoints and role constraints | daemon using canonical galaxy, faction knowledge/economy and typed catalog | generated loadout plus provenance/availability facts | Eve inspection and refit surfaces | daemon now uses the fossil candidate filter and seeded weighted selection: manufacturer must be present and allied, ally weight decays by home-zone graph distance, price uses the authored exponent, and hardpoints require matching semantic type, rotated footprint containment, and controller role; generation fills the largest compatible footprint before applying economic weighting, so small gear remains a valid large-mount fallback without displacing available station-scale gear; cargo/capacitor plus station docking equipment must fit remaining hull space; stations draw up to sixteen faction-available equipment rows and pack largest-first into the selected cargo bay's actual interior shape; zone generation derives one continuing deterministic RNG stream per availability corporation, so cross-faction calls cannot perturb another faction; every generated entity persists an immutable source/selection receipt that reaches Eve as `loadout.item` semantics; Terminus explicitly maps combat-role factions to catalog availability corporations | multi-zone generated-world parity and exact root-to-zone stream sequencing against the fossil |
| Flight intent | `Ship`, `Thruster`, `AetherDrive`, input paths | daemon intent + Ymir physics | analog movement/look/tractor | EveUnity input driver | movement intents feed Ymir-owned body integration; authored thrust allocation, torque, energy and heat remain simplified | prove original drive/equipment behavior ordering and runtime interpolation |
| Collision | Unity physics and entity handlers | Ymir | collision/result facts | visual interpolation only | Ymir.Core world stepping wired for entity bodies, radial fields and contacts; missing-owner negative proof passes | publish authoritative collision events and prove Unity cannot write transforms back |
| Targeting/contacts | sensors + `ActionGameManager` | daemon | contact/target semantics | target HUD and indicators | simplified owner wired/projected; daemon weapon lock now acquires and decays from facing, elapsed time, and observer-local contact confidence instead of granting an immediate perfect lock; the provider cockpit projects that same selected-target lock through a generic Eve progress component without client smoothing or reconstruction | target-switch reset, angular decay, visibility/nearest/explicit cases, lock transition events, and authored reticle animation |
| Projectile flight | weapon/projectile classes | Eve presentation for ordinary tracers; Ymir for persistent physical payloads | receipt trajectory/effect facts or persistent payload state | native projectile visual | ordinary instant, charged, and constant execution creates no projectile body; each weapon state owns a stable shot sequence and receipts carry origin, endpoint, nominal duration, and effect identity for Eve presentation; `SpawnProjectile` and its alternate writer are deleted; Ymir projectile contacts are feedback-only for explicitly persisted payload rows and cannot write damage; Ymir.Core remains embedded for world integration, radial fields, circle contacts, pickups, and genuinely persistent/interceptable payloads | split/rename the remaining physical-payload interface and remove ordinary projectile terminology from runtime contracts |
| Shield/armor/equipment/hull damage | energy-funded shield, schematic armor cells, equipment and scalar hull | daemon canonical damage transaction | aggregate/exact armor grid, entity status, and layer-aware shot/damage receipts | schematic, meters, impacts, damage VFX | owner-exists; wired; exclusive; projected: daemon initializes armor/maximum-armor grids from hull plus hardpoint catalog topology; direct and deployable damage share one `shield -> armor cell -> equipment -> scalar hull` transaction; direct impacts select a cell or complete hardpoint footprint before orthogonal spread and 0.5-cell penetration, while splash payloads select the source-facing hull half; the fossil penetration infinite-loop defect is fixed; damage type is typed/pass-through without resistances because the fossil never applied them; Eve exposes aggregate and exact armor state and receipts identify reached/applied layers; fake Ymir contacts and presentation cannot decide damage | fossil parity fixtures for topology, edge impacts, orthogonal spread, multi-cell penetration, equipment interception, and generic shield/equipment effect lifecycle |
| Thermal/energy | cell conduction, capacitor/reactor/radiator network, and cockpit thermal exposure | daemon | ship/equipment power, temperature, medical state, risk and death feedback | schematic/post effects | owner-exists; wired; exclusive; projected; generic timing and native asset delivery lowered: catalog hull and installed-item shapes derive cell mass/conductivity/topology; local temperature derives thermal performance, wear potential and online state; all consumers share capacitor-first/reactor-residual transactions; powered radiators pump before reactor settlement; reactor heat then enters the same cell network before conduction/radiation; cockpit-cell temperature drives the fossil nonlinear heatstroke/hypothermia accumulation, linear recovery, severe-risk crossings, and ordinary exactly-once destruction with a typed cause; Eve exposes cockpit, risk, heatstroke source-weight, phasing and death-transition facts; EveUnity generically resolves the original severe pulse plus live/reconnect-safe cause-specific death crossfade; Aetheria deterministically translates the five broken legacy profiles into clean URP profiles, advertises them by semantic role through CultMesh CDN, and binds them in the canonical client through a weight-only native sink; rotated equipment topology, parent/child radiation, schematic composition, legacy SSR/AO/roundness equivalents, canonical render-pipeline activation and rendered parity remain incomplete | multi-consumer ordering, multi-radiator visibility, rotated topology, generic schematic meters, renderer-feature parity, active URP capture and live/reconnect death-fade screenshots |
| Weapons | ServerShared behaviors + Unity effects | daemon plus Ymir geometry/kinematics | action capability, weapon state, shot/damage receipts | effects/audio/HUD | instant and constant resource/timing lifecycles are daemon-owned; charged weapons accept one semantic request, spool without requiring a solution, hold at full charge, commit automatically when a solution appears, and perform deterministic once-per-second malfunction hazard checks after the authored safe hold interval with increasing persisted risk; generic Eve `weapon.state` exposes charge hold/risk and feedback exposes readiness, commit, refusal, and malfunction chronology; committed direct damage now enters the shared daemon armor-cell transaction | guided persistent payloads, visibility/wear, remaining impact-distribution parity, and Unity effect reconciliation |
| Destruction/drops | hull death, 25% equipment rolls, unconditional cargo drops, 25-unit launch, 30-second pickups | daemon | destruction identity, death/drop events, typed pickups | provider-advertised destruction effect and pickups | owner-exists; wired; exclusive; projected: direct and deployable lethal damage commit one deterministic destruction transaction, clear live references, retain only a non-rendered identity tombstone, and emit exact pickups/events; the reserved client `DestroyEntity` command is rejected; Aetheria advertises its original big explosion as `effect.feedback.entity.destroyed`, lowered generically by EveUnity | equipment-destruction-before-hull parity and multiplayer reconnect chronology |
| Tractor/scoop | analog tractor and physical timed pickups | daemon + Ymir | tractor power, pickup body and receipt | pickup world entity plus beam/interaction feedback | fossil 2/s power ramp and authored forward radius/traction/distance feed pickup bodies into the shared Ymir world; ship contact collects exactly once when capacity permits or applies the fossil 25-unit rejection kick; Aetheria maps pickup collision only against ship bodies, so stations and other world bodies cannot become cargo writers; 30s daemon lifetime; instant cargo teleport deleted; current-zone pickups occupy ordinary rows in the shared Eve entity SoA body with stable logical identity and provider-owned assets; `pilot.scoop` advertises a generic `button-hold.v1` scalar contract, the released client sends `1` on press and `0` on release through `SetTractorPower`, both receipts reconcile, and daemon power returns exactly to zero; the provider build removes the fossil's embedded tractor object from ship prefabs, and `beam.presentation` attaches the sole standalone effect to the SoA ship; the warm released witness proves one pickup becomes zero after one Ymir Begin fact, one `pickup.collected` event, and an exact cargo delta from `0` to `1`; capacity rejection publishes `cargo-capacity` and unchanged cargo through Eve in the daemon smoke | polish provider beam shape/attachment and add a released-client capacity-rejection scenario |
| Dock/undock | wormhole-first interact; first eligible bay; component prerequisites | daemon + Ymir placement | dock commands/context | camera/UI transition | live generic Unity witness now proves advertised dock/undock actions, daemon bay assignment, dock-parent inclusion in the SoA view, sharing of the dock parent's equipment-derived contact picture, generic camera retargeting, controlled-subject hiding, movement suppression, and restoration on undock; the starter station and local raider form a deterministic Terminus departure proof | restore fossil component-prerequisite rejection reasons, exact undock placement, wormhole priority, reconnect, and docked inventory/trade surfaces |
| Inventory/equipment | inventory panels + ServerShared | daemon | inventory/equipment surfaces | generic UI | owner-exists; wired/projected in fragments; not proven | all transfer directions and rejection |
| Trade | trade UI/economy | daemon | stock, quote, purchase commands | generic UI | daemon derives station, stock row, catalog item, current price, quantity, credits, target capacity, and hull-vs-cargo result; Eve advertises only currently available stock actions; generic Unity live witness receives an authoritative purchase receipt while paused | sell flow, quote expiry, contested stock, and full trade surface parity |
| Action bar/input | input shell and rebinding UI | daemon advertises actions; client owns bindings | input capability and action descriptors | universal binding UI | contracts wired; cargo actions are catalog-filtered to consumables the current actor can actually activate, including suppression of active non-stackable items | dynamic equipment behavior validity and runtime action-bar quantity/fill proof |
| Pilot camera | ARPG follow/dock cameras | Eve view semantics + runtime | camera rig and semantic view id | EveUnity camera rig | daemon dock state now selects distinct contracts instead of collapsing both fossil cameras: undocked flight advertises the entity-forward `Third Person Rig` framing (distance 30, screen 0.64/0.81, FOV 60, lens 1-4096), while the retained dock projection remains separate; EveUnity lowers entity-forward framing without provider types | reproduce the dock camera's separate follow/look-at subjects, blend timeline, noise, and reconnect state |
| Render channels | Unity layers/culling masks | provider surface policy + runtime asset variant | semantic channel exclusions | EveUnity native layer mapping | wired, exclusive, and lowered: the pilot surface excludes the semantic `map` channel; the Unity asset variant maps `map` to its authored native layer; EveUnity subtracts that layer from the configured camera mask; portable state contains no Unity layer or mask; package tests and the clean-client dependency verifier pass | live pilot capture proving no map glyphs while the map camera retains them |
| Minimap/tactical map | `ZoneRenderer`, map cameras | daemon world facts | explicit map projections | Unity and Electron | facts fragmented; generic lowerers absent | same state in both runtimes |
| Fields/fog/gravity | zone renderer and shaders | daemon/plugin semantics | Fields plugin documents | EveUnity fields lowerer | daemon publishes four canonical gravity/fog splat layers through `field.volume3d`; the provider asset variant owns the fossil shader, native ports, quality/features, and raymarch/temporal/composite pass graph; released EveUnity `0.3.40` lowers temporal history and viewport-to-texture scale relations generically, and fails closed on partial native programs or unresolvable scale bindings; the warm released witness executes the provider bundle, four layers, and repeated composites without importing Aetheria code; derived dither scale is now proven but the raymarch remains nearly flat orange, so visual parity is explicitly absent | compare camera/world sampling coordinates against the published field viewport, then inspect finite aim look-at and capture temporal visual parity |
| HUD/schematic | ARPG HUD and `SchematicDisplay` | Eve composition from daemon facts | semantic status/components | EveUnity UI | provider-authored transparent cockpit is wired, projected, and lowered through generic UI Toolkit panes, metrics, and progress components for hull, shield, capacitor, temperature, cargo, weapon cooldown/groups, selected target, target lock, target shield, and target hull; the original armor/equipment schematic and transient warning choreography remain reference-only | schematic topology, warning timelines, dynamic action state, and composited live capture |
| Hit/lock/warnings | `ActionGameManager` feedback | daemon event facts | transient feedback stream | VFX/UI/audio | bounded deduplicated game-event ledger and Eve `feedback.stream` wired for pickup outcomes, projectile launch/impact, entity damage, alive-to-dead destruction, authoritative weapon reload chronology, and episode-deduplicated `weapon.fire.refused` facts for insufficient energy or missing ammunition; refusal identity, authored weapon, target, reason, and remaining magazine state are runtime-independent cockpit inputs | add lock transitions, thermal and docking event families, negative transaction fixtures, and runtime effect consumption |
| Assets | `Assets/Resources` | Aetheria provider | asset catalog + CultMesh CDN | runtime cache/load | provider manifests name deterministic bundle-internal paths; the authoring build derives presentation-only prefabs, strips missing and non-Eve `MonoBehaviour` components, verifies both saved prefabs and the loaded bundle, and publishes the immutable bundle through CultMesh; released-client warm-cache loading, content verification, and rendered provider identity are proven, while cold transfer still times out because the 46.1 MB body travels through batched snapshot records | mapped/network body transport followed by empty-cache download, atomic promotion, and cache reuse proof |
| Save/resume | original persistence paths | daemon/CultCache | published current state | clients rehydrate | owner-exists; not parity-proven | mid-loop restart |
| Terminus loop | original game composition | daemon/Ymir/Eve | pilot surface | Aetheria.Unity | live minimum-loop proof: generic Unity discovers the provider, lowers the pilot world/assets, single-steps dock and undock while paused, buys daemon-priced station stock, single-steps Ymir salvage collection, resumes, moves, targets, fires, and presents authoritative receipts/shot outcome | destruction-created loot pickup, richer trade content, docking transitions, and sustained combat encounters |
| Starbridge | original/shared architecture intent | one daemon session | pilot + commander surfaces | Unity + Electron | commander task board and autonomous explore execution wired; Electron Eve lowering and split-target proof missing | commander orders plus four pilots and autonomous workers |
| Behavior engine | `ServerShared/Behaviors` composition | daemon | executable action/status projection | action bar and feedback | tick reconciles equipped catalog payloads into persistent behavior state; shared evaluated-stat query proven; execution families remain partial | every behavior family, ordering, resource transaction and modifier lifecycle |
| Sensors/visibility | installed sensor sensitivity, decaying signature sources, and observer information thresholds | daemon | contacts and ping events | indicators/maps | passive reach derives from enabled installed Sensor behaviors with diminishing-return array composition regardless of entity kind; station-scale arrays are ordinary gear installed into larger hull-authored sensor hardpoints, with smaller same-type arrays remaining valid fallbacks; generation prefers the largest fitting footprint and contains no station/ship sensor-count branch; contact refresh no longer overwrites emitted visibility; docked craft consume one daemon-derived union of craft and dock-parent contacts through both Eve rows and SoA bodies, with sensor-source provenance; the old station range survives only as obsolete snapshot compatibility | prove shutdown, threshold decay, ping, classification timelines, and canonical catalog station-array data |
| AI/tasks | corporation task scheduler plus agent state machines | daemon | commander task board, corporation survey ledger, and shared semantic controls | Eve commander/pilot clients | point exploration, attack with optimum-range control, same-zone haul, mining/offload, resource surveying, physical station towing, persistent orbit patrol, route-aware assignment, and autonomous return home wired through shared commands; attack admission rejects missing/friendly targets and unavailable weapon groups before durable queueing; capability/priority assignment and rejection behavior proven; generic Electron commander lowering proves the worker roster | cross-zone logistics execution, cancellation, reconnect, and multi-client command transfer |
| Consumables | cargo-backed timed behavior containers | daemon | dynamic actions, active effects, and lifecycle facts | action bar/schematic | daemon atomically resolves typed metadata and concrete cargo, preserves consumed-instance quality, enforces authored stacking, evaluates effectiveness/quality, and executes ordered `EnergyDraw`, fossil-compatible no-op `Heat`, `ItemUsage`, and stateful `Cooldown` payloads after energy tick initialization; every effect and payload state has stable authored identity; cooldowns update before the ordered chain, require state strictly below zero, reset to one on success, and continue aging behind earlier failures; false and unsupported payloads stop later execution with a typed fact; timers retain the fossil's exact-zero final tick; Unity reconciles presentation by effect identity and cannot restore active behavior state by list index | catalog revision pinning, stat modifiers, remaining behavior families, and reconnect presentation proof |

## Ownership Invariants

1. Unity objects never decide durable game state.
2. Aetheria owns spatial state, game meaning, and persistence. The daemon calls
   Ymir directly as an injected in-process physics library and commits its
   deterministic results; Ymir never becomes a second daemon or state owner.
3. Eve publishes semantic facts and commands, not Aetheria implementation
   objects or runtime-specific layer numbers.
4. Runtime-specific asset metadata may map semantic view channels to native
   mechanisms such as Unity layers.
5. Provider assets own authored visuals; EveUnity owns generic projection and
   native hosting; Aetheria.Unity owns only provider client configuration.
6. Transient feedback has authoritative event identity so reconnect and refresh
   cannot duplicate hits, drops, purchases, or deaths.
7. RTS, minimap, and pilot views are projections of one world state.
8. Restored agent combat and future pilot cognition share semantic ship actions,
   command validation, and Ymir execution. They do not share an implicit combat
   authority: RTS task scheduling assigns work, while pilot cognition proposes
   actions from observer-local knowledge under daemon-owned doctrine.

## Migration Order

1. Finish archaeology and replace every `reference` row with exact behavior,
   source anchors, state inputs, outputs, timing, and proof cases.
2. Establish complete daemon simulation parity using the injected Ymir physics
   port before polishing clients.
3. Publish combat, thermal, contact, equipment, docking, trade, and feedback
   facts through Eve without runtime-specific leakage.
4. Implement generic EveUnity presentation primitives and plugin lowering.
5. Delete Unity's reconstructed `Entity`/`Zone`/`Behavior` mirror, then compose
   `Aetheria.Unity` from generic primitives and provider assets.
6. Run scenario parity against the fossil and preserve captures, receipts, and
   state timelines in conformance packs.

Original-game parity is the substrate, not the final pilot command model. New
combat work must follow `cockpit-doctrine-combat.md`: preserve semantic weapon
actions and physical constraints, then place daemon-hosted cognition planning
above them. The planner may propose sensing, maneuver, and fire-control actions;
the daemon command gate alone accepts them, and Ymir alone resolves spatial
motion and collision. Do not spend migration effort rebuilding manual aiming as
the primary skill test or let a Unity presentation path become a second combat
owner.

## Current P0 Faults

These are source-confirmed ownership failures, not feature backlog:

1. **Physics delegation is incomplete.** Daemon projectile and entity-world
   integration now use `Ymir.Core`, including radial fields and body contacts,
   but tractor forces, docking placement, authored drive torque, and the Unity
   bridge/query path still need full authority and parity proofs.
2. **Persisted behavior is not executable behavior.** Snapshot schemas preserve
   broad equipment/behavior state while active simulation executes one
   synthetic projectile weapon and simplified raider pursuit.
3. **Client feedback chronology is incomplete.** Pickup collection, rejection,
   expiry, projectile launch/impact, damage, and destruction now use a bounded
   deduplicated daemon ledger projected through Eve, but lock transitions,
   damage topology, thermal warnings, and docking effects still need event families.
4. **View composition remains partial.** Pilot surfaces now exclude semantic
   render channels and runtime asset variants map those channels to native
   layers without leaking Unity masks into portable state. Entity presentation
   graphs, attachment sockets, map products, and richer effect composition are
   not yet first-class contracts.
5. **The game loop has no single daemon owner.** Exploration, discovery,
   encounter, loot, docking, trade, refit, narrative, boss progression,
   completion, failure, and continue exist as pieces rather than one explicit
   run-state machine.
6. **Unity spatial feedback remains insufficiently fenced.** The daemon now
   requires Ymir for world advancement, but the Unity bridge/query path still
   needs a negative proof that presentation transforms cannot write spatial
   truth back into provider state.
7. **Live conformance has no complete owner.** Static provider packs can pass
   while daemon, runtime, or split-target dependencies are absent. The current
   Eve consumer smoke fails because its expected EveElectron proof is missing.
8. **Normative native presentation is incomplete.** EveUnity owns semantic
   render-channel lowering and immutable presented-entity generations. General
   presentation graphs, map products, attachment/effect lifecycles, material
   profiles, and source-version diagnostics still lack complete Eve contracts
   and generic runtime lowerers.
9. **Original-game acceptance lacks an evidence ledger.** No current owner
    records fossil baseline, target state timeline, commands/receipts, rendered
    captures, and negative-writer checks as one scenario result.

## Evidence Rules

- Static fixtures and provider packs prove schema/contract shape only.
- `wired`, `exclusive`, `lowered`, and `proven` require live executable
  witnesses at the claimed layer.
- A negative ownership claim must verify the obsolete path cannot produce,
  override, or repair the result.
- The current `verify-aetheria-ymir-cutover.ps1` proves selected Unity callback
  removal and static routing into an Aetheria bridge. It is not Ymir integration
  proof until it verifies an actual Ymir.Core/service dependency and rejects
  Aetheria-local spatial implementations.
- Conformance scenarios must preserve source baseline, authoritative state
  timeline, Ymir timeline, command/receipt chronology, rendered capture,
    runtime/package versions, and negative-authority results together.
10. **The fossil authoring project still contains a mutable gameplay graph.**
    `AetheriaUnityObservedEntityRestorer`, `AetheriaUnityObservedFrameApplier`,
    `ActionGameManager`, and `ZoneRenderer` remain reference and asset-authoring
    inputs in the original Unity project. They are absent from the canonical
    `Aetheria.Unity` client, whose verifier rejects Aetheria, ServerShared, and
    gameplay assemblies. EveUnity now publishes each committed SoA generation
    through a read-only presented-entity registry; richer selection and HUD
    adapters still need to consume it instead of rebuilding parallel indexes.
11. **Remote provider asset delivery remains unproven.** The authoring build now
    derives deterministic presentation-only variants, strips missing and
    non-Eve scripts, verifies the saved prefabs and loaded AssetBundle, and the
    daemon advertises those bundle-internal paths. A clean EveUnity client must
    still prove cold download, content verification, cache reuse, and rendered
    identity without access to the authoring project.
12. **The negotiated local SoA fast path remains unproven.** The daemon publishes
    exact immutable generations; the external Unity client rejects stale or
    mismatched bodies and now resolves the same generation through the
    Verse-owned network binding, manifest, and content-addressed chunks. The live
    witness proves remote/fallback correctness. Shared-memory handle exchange and
    proof that a same-machine client selected the zero-copy representation remain
    CultMesh work rather than EveUnity policy.

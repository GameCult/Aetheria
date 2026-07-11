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
| Flight intent | `Ship`, `Thruster`, `AetherDrive`, input paths | daemon intent + Ymir physics | analog movement/look/tractor | EveUnity input driver | movement intents feed Ymir-owned body integration; authored thrust allocation, torque, energy and heat remain simplified | prove original drive/equipment behavior ordering and runtime interpolation |
| Collision | Unity physics and entity handlers | Ymir | collision/result facts | visual interpolation only | Ymir.Core world stepping wired for entity bodies, radial fields and contacts; missing-owner negative proof passes | publish authoritative collision events and prove Unity cannot write transforms back |
| Targeting/contacts | sensors + `ActionGameManager` | daemon | contact/target semantics | target HUD and indicators | simplified owner wired/projected; not parity | visibility/nearest/explicit/lock cases |
| Projectile flight | weapon/projectile classes | Ymir | projectile entity/event projection | native projectile visual | Ymir.Core adapter wired for spawn/travel/contact/despawn; native presentation and broader weapon parity incomplete | prove every authored projectile family and runtime visual lifecycle |
| Shield/hull damage | energy-funded shield, armor cells, equipment and hull | daemon | entity status + damage events | meters, impacts, damage VFX | synthetic scalar owner wired; not parity | typed absorption, penetration, item damage and death causes |
| Thermal/energy | cell conduction and capacitor/reactor/radiator network | daemon | ship/equipment status and warnings | schematic/post effects | cell conduction/radiation wired; energy network and medical thresholds remain reference | capacitor/reactor/radiator ordering, shared energy and medical timelines |
| Weapons | ServerShared behaviors + Unity effects | daemon + Ymir where spatial | action capability, weapon state, attack events | effects/audio/HUD | one synthetic weapon wired; original engine reference-only | each weapon family |
| Destruction/drops | entity death and pickup paths | daemon | death/drop events and world entities | effects and pickups | fragments wired; chronology and original drop transaction absent | exactly-once drops and cleanup |
| Tractor/scoop | analog tractor and physical timed pickups | daemon + Ymir | tractor power, target, pickup entity and receipt | beam/interaction feedback | fossil 2/s power ramp and authored radius/traction/distance move targets through Ymir; instant cargo teleport deleted; pickup transaction remains partial | sphere-cast filtering, failed capacity, pickup lifetime and exactly-once receipt |
| Dock/undock | wormhole-first interact; first eligible bay; component prerequisites | daemon + Ymir placement | dock commands/context | camera/UI transition | daemon path wired/projected; Ymir placement and parity unproven | priority, eligibility, placement and reconnect |
| Inventory/equipment | inventory panels + ServerShared | daemon | inventory/equipment surfaces | generic UI | owner-exists; wired/projected in fragments; not proven | all transfer directions and rejection |
| Trade | trade UI/economy | daemon | stock, quote, purchase commands | generic UI | purchase owner wired/projected; not proven | atomic purchase and capacity |
| Action bar/input | input shell and rebinding UI | daemon advertises actions; client owns bindings | input capability and action descriptors | universal binding UI | contracts wired; dynamic original behavior source absent | dynamic equipment/cargo actions |
| Pilot camera | ARPG follow/dock cameras | Eve view semantics + runtime | camera rig and semantic view id | EveUnity camera rig | basic follow lowered; dock/view parity absent | authored framing and dock transition |
| Render channels | Unity layers/culling masks | provider variant + runtime | semantic view id | native layer policy | specification work; incomplete uncommitted prototype | no map glyphs in pilot view |
| Minimap/tactical map | `ZoneRenderer`, map cameras | daemon world facts | explicit map projections | Unity and Electron | facts fragmented; generic lowerers absent | same state in both runtimes |
| Fields/fog/gravity | zone renderer and shaders | daemon/plugin semantics | Fields plugin documents | EveUnity fields lowerer | owner-exists; basic lowering; composition parity absent | visual parity captures |
| HUD/schematic | ARPG HUD and `SchematicDisplay` | Eve composition from daemon facts | semantic status/components | EveUnity UI | reference | source-fact and timing matrix |
| Hit/lock/warnings | `ActionGameManager` feedback | daemon event facts | transient feedback stream | VFX/UI/audio | reference | event identity and duration |
| Assets | `Assets/Resources` | Aetheria provider | asset catalog + CultMesh CDN | runtime cache/load | wired; basic lowering; not live-proven remotely | cold/warm remote-only live load and cache proof |
| Save/resume | original persistence paths | daemon/CultCache | published current state | clients rehydrate | owner-exists; not parity-proven | mid-loop restart |
| Terminus loop | original game composition | daemon/Ymir/Eve | pilot surface | Aetheria.Unity | incomplete; no scenario proof | fly/fire/loot/dock/trade |
| Starbridge | original/shared architecture intent | one daemon session | pilot + commander surfaces | Unity + Electron | commander task board and autonomous explore execution wired; Electron Eve lowering and split-target proof missing | commander orders plus four pilots and autonomous workers |
| Behavior engine | `ServerShared/Behaviors` composition | daemon | executable action/status projection | action bar and feedback | tick reconciles equipped catalog payloads into persistent behavior state; shared evaluated-stat query proven; execution families remain partial | every behavior family, ordering, resource transaction and modifier lifecycle |
| Sensors/visibility | decaying sources and observer information thresholds | daemon | contacts and ping events | indicators/maps | reference | threshold, decay, ping and classification timelines |
| AI/tasks | corporation task scheduler plus agent state machines | daemon | commander task board, corporation survey ledger, and shared semantic controls | Eve commander/pilot clients | point exploration, attack, same-zone haul, mining/offload, resource surveying, physical station towing, and persistent orbit patrol wired through shared commands; capability/priority assignment and rejection behavior proven | optimum-range control, cross-zone logistics, cancellation, reconnect, and live commander lowering |
| Consumables | cargo-backed timed behavior containers | daemon | dynamic actions and active effects | action bar/schematic | reference | consume, stacking, duration and cleanup |

## Ownership Invariants

1. Unity objects never decide durable game state.
2. Target invariant: Ymir will own spatial truth; the daemon owns game meaning
   and persistence. Current state: Aetheria still owns multiple local spatial
   implementations.
3. Eve publishes semantic facts and commands, not Aetheria implementation
   objects or runtime-specific layer numbers.
4. Runtime-specific asset metadata may map semantic view channels to native
   mechanisms such as Unity layers.
5. Provider assets own authored visuals; EveUnity owns generic projection and
   native hosting; Aetheria.Unity owns only provider client configuration.
6. Transient feedback has authoritative event identity so reconnect and refresh
   cannot duplicate hits, drops, purchases, or deaths.
7. RTS, minimap, and pilot views are projections of one world state.

## Migration Order

1. Finish archaeology and replace every `reference` row with exact behavior,
   source anchors, state inputs, outputs, timing, and proof cases.
2. Establish complete daemon/Ymir simulation parity before polishing clients.
3. Publish combat, thermal, contact, equipment, docking, trade, and feedback
   facts through Eve without runtime-specific leakage.
4. Implement generic EveUnity presentation primitives and plugin lowering.
5. Reconstruct Aetheria's authored composition in `Aetheria.Unity` using those
   generic primitives and provider assets.
6. Run scenario parity against the fossil and preserve captures, receipts, and
   state timelines in conformance packs.

## Current P0 Faults

These are source-confirmed ownership failures, not feature backlog:

1. **Physics delegation is incomplete.** Daemon projectile and entity-world
   integration now use `Ymir.Core`, including radial fields and body contacts,
   but tractor forces, docking placement, authored drive torque, and the Unity
   bridge/query path still need full authority and parity proofs.
2. **Persisted behavior is not executable behavior.** Snapshot schemas preserve
   broad equipment/behavior state while active simulation executes one
   synthetic projectile weapon and simplified raider pursuit.
3. **Client feedback lacks authoritative chronology.** Hits, lock transitions,
   damage topology, weapon transitions, death causes, effects, and drops have
   no complete deduplicated event stream.
4. **View composition is implicit.** Provider prefabs contain pilot and map
   subviews, but portable render channels, entity presentation graphs, effects,
   attachment sockets, and map products are not yet first-class contracts.
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
8. **Normative native presentation has no implemented owner.** Render channels,
   presentation graphs, map products, attachment/effect lifecycles, material
   profiles, and source-version diagnostics are assigned in specification but
   lack complete Eve contracts and EveUnity lowerers.
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

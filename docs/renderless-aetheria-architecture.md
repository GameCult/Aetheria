# Renderless Aetheria Architecture

Date: 2026-07-06

This document records the intended architecture before the next demolition pass.
It is not an implementation claim. It is the map the implementation must be
measured against.

Canonical Eve-side doctrine lives in
`E:\Projects\Eve\docs\world-state-lowering.md`. This Aetheria document applies
that doctrine to Aetheria: the daemon is the provider, Eve owns world-state
lowering semantics, and Aetheria clients are conformance targets.

## Objective

Aetheria should become a renderless daemon-owned game.

The Aetheria daemon owns game state, simulation rules, generated content,
game-feel settings, authority policy, typed operations, assets, and
high-performance views of live state. Eve owns the portable UI and world-state
rendering instruction contract. Unity, Godot, Electron, Hermodr, and later
runtimes lower Eve surfaces and submit typed operations; they do not own
Aetheria gameplay truth.

Hermodr is the RTS sanity check: an unspecialized Eve/browser client must be
able to reconstruct the same RTS gameplay surface as Electron from the Eve spec,
daemon API, state pointers, render-field documents, operation descriptors, and
CultMesh CDN asset references.

Godot is the corresponding ARPG sanity check: an unspecialized non-Unity client
must be able to reconstruct the 3D action-game surface from the same kind of
daemon-authored contract, without inheriting Unity scene authority.

## Authority Map

Owner:
Aetheria daemon owns committed game state and rules.

Inputs:
The daemon may read typed CultCache/CultMesh documents, catalog data, authority
policy, player/client operation documents, simulation clock input, deterministic
generation seeds and asset manifests. Its simulation transaction may invoke the
embedded Ymir spatial kernel, but Ymir does not become a second state owner.

Outputs:
The daemon publishes typed state documents, committed facts, operation receipts,
simulation frames, SoA/native view descriptors, scalar and vector field
documents, render splats, entity/object render rows, typed CultMesh artifact
manifests, managed content sessions, and Eve/CultUI surfaces.

Derived state:
Unity GameObjects, Electron canvases, Hermodr browser nodes, Godot scene nodes,
local native buffers, local caches, and local input state are derived
presentation/runtime state. They may improve performance or UX. They do not
decide committed Aetheria truth.

Forbidden writers:
Renderer-local gameplay compensators, Unity scene lifetime, Unity collision
callbacks, Electron-only map builders, Hermodr Aetheria plugins, Godot-specific
daemon branches, local file sidecars, renderer-owned asset fallbacks, and
client-specific command routers must not write or define canonical gameplay
state.

Shared paths:
Unity, Godot, Electron, Hermodr, tests, tools, and future clients should all
consume the same daemon-published documents and Eve surfaces. Direct user input,
programmatic commands, replay/import, reconnect, and automation should all enter
through typed operations or policy-governed typed document writes.

Deletion line:
Before adding new client behavior, delete or demote any path that lets a client
invent gameplay state, reconstruct daemon-owned documents from broader frames,
or patch missing Eve semantics with Aetheria-specific renderer code.

## Spatial Kernel Boundary

The active Aetheria simulation authority owns committed transforms, velocities,
contacts that affect gameplay, and every consequence derived from them. All
authoritative integration, overlap, cast, contact, broadphase, and spatial-field
computation is performed through Ymir. In the production daemon Ymir is an
embedded deterministic kernel, not an independently persistent Aetheria world.

The daemon passes an explicit simulation slice to Ymir and commits the returned
spatial facts as part of the same tick transaction. Ymir neither reads Aetheria
state behind the daemon's back nor writes CultMesh documents of its own. Combat,
docking, sensors, AI decisions, damage, inventory, triggers, and event chronology
remain Aetheria rules even when they consume Ymir results.

Ymir is linked into the production daemon as an in-process library behind a
narrow injected physics port. That port exists for deterministic substitution
and language interop, not to create another daemon, transport, lease domain, or
copy of the world. Aetheria owns the input projection and every committed
result.

## Terminus Simulation Clock

Terminus uses a daemon-owned, user-controllable simulation clock. Pause and slow
rates provide unbounded tactical decision time. High rates provide transit time
compression so a physically larger world does not require the player to watch
uneventful coasting in real time. Supported requested rates currently range from
pause through `128x`; Starbridge remains real-time-owned.

While paused, `AdvanceSimulationStep` advances exactly one fixed daemon step
without changing the persistent requested rate. Eve advertises this as
`simulation.step`, giving players, bots, and conformance fixtures a precise
tactical inspection primitive instead of briefly unpausing and racing the
publication cadence.

The requested simulation rate is not a renderer time scale. The daemon advances
the deterministic fixed-step simulation repeatedly and publishes one coherent
sample of the resulting state. Unity, Electron, bots, and other Eve clients may
interpolate presentation between samples, but cannot advance committed game
time or invent intermediate outcomes.

Time compression distinguishes the persistent requested rate from the effective
rate currently being executed. During a compressed batch, the daemon inspects
new gameplay facts after every fixed step. Damage, destruction, pickup outcomes,
thermal risk, docking transitions, weapon failures, wormhole transitions, and
run failure stop the batch at the causal step, publish `simulation.interrupted`,
and set the effective rate to pause without erasing the requested rate. The
interruption names its cause and exact simulation time; the daemon never commits
later steps before publishing it. Contact, hostile targeting, weapon readiness,
arrival, resource discovery, and failed-order coverage remain to be added to the
explicit policy as their authoritative facts mature. This attention policy is
daemon-owned gameplay state, not client heuristics.

Compressed stepping must not multiply control-plane traffic. Commands and
receipts retain their ordinary typed CultMesh path, SoA consumers receive the
latest complete leased frame, and reactive UI state is sampled at a bounded
publication cadence. Internal fixed steps remain deterministic and testable
without requiring a renderer or wall-clock delays.

## Tutorial Generation

The fossil tutorial was not a scripted objective mode. Its surviving gameplay
difference is a distinct generated galaxy selected while `TutorialPassed` is
false: six authored faction roles, 64 weighted blue-noise zones, a pruned
Delaunay link graph, faction influence and ownership, an unowned entrance, and
initial discovery of the entrance plus its neighbors. The narrative processor
was commented out and no source-backed tutorial-completion trigger survived.

That topology now has a deterministic daemon owner driven by CultMath's
Unity-compatible simplex noise. Every topology zone also has a daemon-owned
celestial plan preserving the fossil's radius/mass curves, packed subzones,
rosettes, solar systems, satellite and binary passes, asteroid belts, stable
orbits, gravity curves, and sun/gas-giant presentation parameters. The old
process-dependent string/hash seed has been replaced by a stable typed-zone
seed; generated saves must reproduce across daemon processes.

The daemon continues the exact post-celestial random stream to populate each
zone with the fossil station-count curve, Lagrange station orbits, paired
defensive turrets, and faction ships. The entrance additionally receives the
controlled starter ship. Zone adjacency is the wormhole truth; exits remain a
derived render/interaction query rather than counterfeit wormhole entities.
Generated non-player ships are distributed through the fossil half-zone volume.
Generated orbits carry the authored distance-derived period; each fixed daemon
step advances canonical phase and projects orbital entity pose and velocity
before Ymir. Body queries and Eve lower the same orbit graph.
Each generated NPC also enters with a daemon-owned assigned patrol task over
four shuffled orbit identities, matching the fossil activation behavior without
letting the Unity client create agent state. The daemon derives hostility from
the fossil owner-security and faction-relationship rule. A visible hostile ship
pre-empts circuit movement through the existing combat planner; the patrol task
keeps its circuit index and resumes it when contact is lost.

New Game now submits its advertised Eve operation, waits for a newly published
daemon sector map, and selects the persisted 64-zone tutorial while
`TutorialPassed` is false. The handcrafted Terminus arena remains a proof
fixture and the fallback for the still-unmigrated post-tutorial sector path.
No surviving source defines tutorial completion, so entering this run does not
change `TutorialPassed`.

## Current Mechanism

The repo is mid-migration.

The C# daemon currently publishes typed runtime state, command boundaries, Eve
surfaces, Starbridge documents, viewport documents, asset manifests, and
CultMesh-facing transport hooks. Electron has been moved toward consuming
daemon-authored Eve surfaces and managed documents instead of rebuilding them
from a daemon frame. Unity has been moved toward observing daemon frames and
submitting typed operations instead of ticking local simulation, but it still
contains Unity-era presentation, object restoration, input, camera, UI, weapon,
and gameplay-adapter code.

This means Aetheria is not yet fully renderless. The desired ownership is clear,
but the repo still contains migration bridges and Unity-shaped presentation
state that must be audited before they can be deleted or demoted.

## Invariants

- Aetheria gameplay truth lives in daemon-owned typed state, not renderer state.
- Eve surfaces contain the UI and world-state render instructions required by
  clients.
- Dynamic contents are state pointers or managed document references, not copied
  live state embedded into the surface document.
- CultMesh CDN serves assets advertised by the daemon.
- Renderer-local code may lower, cache, batch, interpolate, and display. It may
  not invent gameplay semantics.
- Game-feel settings belong to daemon config or typed state, not client-local
  constants.
- Runtime-specific names may describe packaging or product shells, but not
  gameplay authority branches.
- Hermodr and Electron must render the same RTS surface from the same Eve
  contract.
- Godot and Unity must render the same ARPG surface from the same Eve contract.

## Eve Surface Contract

An Aetheria Eve surface should be rich enough to describe both the 2D RTS view
and the 3D ARPG view.

This is not only UI Toolkit-style widget lowering. Eve lowerers must be able to
lower world state with different presentation budgets: a compact 2D tactical
map, a richer browser canvas, a Unity or Godot 3D scene, a debug overlay, or a
future room-scale view. The sophistication belongs to the lowering target, but
the semantic instructions belong to Eve and the daemon-authored surface.

The surface should be able to declare:

- layout, rows, columns, grids, modal placement, fields, and command bindings;
- state pointers to daemon-owned typed documents;
- operation descriptors with typed payload schemas and route hints;
- 2D scalar fields, such as gravity heightmaps;
- 2D vector/color fields, such as nebula tint or fog/tint splats;
- 3D vector fields, such as flow fields derived from volumetric simulation;
- field visualizers, such as isolines, shaded height, probes, volume slices, or
  particle/flow overlays;
- render splat buffers and accumulation rules;
- entity/object render rows with asset refs, transforms, labels, and selection
  metadata;
- presentation-quality hints, level-of-detail policies, and native-view
  descriptors that let clients choose an appropriate 2D or 3D lowering without
  changing gameplay meaning;
- asset refs resolved through CultMesh CDN.

The Eve repo owns the game-agnostic lowering primitives for these contracts.
Aetheria may define game documents, author surfaces, publish assets, and provide
fixtures, but it should not require Electron, Hermodr, Unity, or Godot to carry
private Aetheria rendering knowledge.

## World-State Lowering

World-state lowering is an Eve responsibility, not an Aetheria client
responsibility.

The Aetheria daemon can publish a view of world state at several levels:

- canonical typed game documents for rules and committed facts;
- high-performance SoA/native view descriptors for hot rendering paths;
- field documents for scalar/vector/color data;
- object/entity render rows for things with transforms, sprites, meshes, labels,
  selection metadata, and operation affordances;
- asset refs for icons, sprites, materials, meshes, shaders, and generated
  textures.

Eve lowerers decide how sophisticated the presentation should be in a particular
runtime. Hermodr might draw an RTS surface as a 2D canvas with isolines, icons,
labels, and modal UI. Electron might use the same lowering path with richer
shell integration. Unity or Godot might lower the ARPG surface into meshes,
materials, particles, UI panels, input affordances, and camera-relative
overlays. Those are quality tiers over the same semantic surface, not separate
Aetheria clients with private gameplay knowledge.

If a lowerer needs to know that a value is "Aetheria gravity" in order to render
it, the contract is wrong. The surface should say it is a scalar field with
domain, units, sampling rules, visualization options, and asset/material refs.
If a lowerer needs to know that a row is an "Aetheria planet" to draw it, the
contract is wrong. The row should carry an entity kind, transform, display
label, state refs, and daemon-advertised assets.

## Field Documents

Gravity is a 2D scalar field for the RTS view. It should be produced from
daemon-owned simulation/generation state and exposed as a high-performance view
or field document. Isolines are one visualization of that scalar field, not the
field itself.

Nebula tint is a 2D vector/color field. Tint splats are color times the
appropriate Aetheria brush/window function accumulated into a color buffer.
Those rules belong in daemon-authored render-field declarations or
game-agnostic Eve field primitives, not in renderer-local compensation code.

The ARPG surface also needs 3D fields. Flow from the volumetric shader family is
a 3D vector field. A 2D renderer does not need the full 3D flow field, but the
contract should not be designed as if all Aetheria views are flat maps.

## Asset Contract

The daemon advertises immutable runtime variants through `EveAssetCatalogDocument`
and CultMesh CDN manifests/chunks. Each variant declares its content hash, byte
size, platform, format, and internal asset key. Clients resolve those refs
through their generic runtime asset bridge.

Unity `Resources` paths and source-project asset paths are packaging inputs, not
runtime identity. A migrated provider asset must load in a clean EveUnity client
without Aetheria gameplay assemblies. Provider bundles therefore contain
presentation-only prefabs and generic EveUnity components, never fossil gameplay
`MonoBehaviour` scripts.

Clients should not synthesize icon paths, substitute improvised SVGs, or embed
fallback asset lore when the daemon fails to advertise an asset. A missing asset
should be visible as missing provider data, not silently repaired by the
renderer.

## Runtime Roles

Hermodr:
Generic Eve/browser lowering witness for the RTS surface. It proves the
contract is not Electron-specific.

Electron:
Player-facing Starbridge RTS shell. It may own packaging, windowing, app
lifecycle, local daemon launch ergonomics, and platform bridges. It should not
own Aetheria gameplay semantics.

Unity:
Current ARPG reference client and migration source. It may own engine
presentation, input capture, camera, generic GameObject projection, native
world-state lowering, and temporary adapters. It must not reconstruct mutable
Aetheria `Entity`, `Zone`, `Behavior`, or item graphs, and it does not own rules,
state, level generation, physics, visibility, or UI/world semantics.

Godot:
Future ARPG conformance runtime. It should prove the ARPG client can be rebuilt
from the daemon and Eve contract rather than ported from Unity scene authority.

Eve:
Game-agnostic surface and world-state lowering contract. Eve owns reusable
primitives for state binding, operation invocation, modal placement, 2D/3D
field visualization, asset refs, native-view descriptors, and runtime-neutral
UI/render semantics.

Aetheria:
Game daemon, documents, rules, authored surfaces, assets, generation,
simulation, and authority policy.

## Migration Gates

1. Daemon publishes the RTS surface as Eve/CultUI plus state pointers, field
   documents, typed operations, and CultMesh CDN assets.
2. Hermodr lowers that RTS surface with no Aetheria-specific renderer plugin.
3. Electron renders the same RTS surface through shared Eve primitives.
4. Daemon publishes equivalent ARPG surfaces and 3D render contracts.
5. Unity consumes the ARPG contract as a renderer/input shell, with remaining
   shims named and bounded.
6. Godot lowers the ARPG contract without Unity scene authority.
7. Unity-only simulation, generation, physics, UI semantics, and asset fallback
   paths are deleted or demoted to renderer presentation.
8. The generic Unity client loads provider-published presentation-only assets,
   lowers exact CultMesh body generations, and contains no Aetheria gameplay
   assembly reference.

## Verification Strategy

Documentation comes first. Verifier pressure should be added only after this
map is accepted.

When enforcement starts, the verifier should prove:

- active docs describe renderless Aetheria and game-agnostic Eve lowerers;
- daemon code owns simulation, generation, operation acceptance, assets, and
  high-performance view publication;
- Electron does not rebuild daemon-owned documents from frames;
- Hermodr has no Aetheria-specific rendering branch;
- Unity gameplay code does not tick local simulation or write canonical state;
- Godot work does not introduce a daemon-side Godot mode;
- asset refs come from daemon manifests and CultMesh CDN;
- field visualization settings come from Eve/daemon documents, not renderer
  constants.
- world-state lowerers live in Eve packages or generic runtime packages, not as
  Aetheria-specific client renderer code.

The goal is not to outlaw useful runtime code. The goal is to make ownership
legible: renderers render, the daemon decides, Eve carries the contract.

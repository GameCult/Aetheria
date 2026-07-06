# Renderless Aetheria Architecture

Date: 2026-07-06

This document records the intended architecture before the next demolition pass.
It is not an implementation claim. It is the map the implementation must be
measured against.

## Objective

Aetheria should become a renderless daemon-owned game.

The Aetheria daemon owns game state, simulation rules, generated content,
game-feel settings, authority policy, typed operations, assets, and
high-performance views of live state. Eve owns the portable UI and rendering
instruction contract. Unity, Godot, Electron, Hermodr, and later runtimes lower
Eve surfaces and submit typed operations; they do not own Aetheria gameplay
truth.

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
generation seeds, asset manifests, and physics-service results.

Outputs:
The daemon publishes typed state documents, committed facts, operation receipts,
simulation frames, SoA/native view descriptors, scalar and vector field
documents, render splats, entity/object render rows, asset manifests, CultMesh
CDN asset blobs, and Eve/CultUI surfaces.

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
- Eve surfaces contain the UI and render instructions required by clients.
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
- asset refs resolved through CultMesh CDN.

The Eve package should own the game-agnostic lowering primitives for these
contracts. Aetheria may define game documents and author surfaces, but it should
not require Electron, Hermodr, Unity, or Godot to carry private Aetheria
rendering knowledge.

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

The daemon advertises assets through typed asset manifests and CultMesh CDN
refs. Clients resolve those refs through their runtime asset bridge.

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
presentation, input capture, camera, GameObject restoration, and temporary
adapters. It should not remain the owner of rules, state, level generation,
physics truth, or UI semantics.

Godot:
Future ARPG conformance runtime. It should prove the ARPG client can be rebuilt
from the daemon and Eve contract rather than ported from Unity scene authority.

Eve:
Game-agnostic surface and lowering contract. Eve owns reusable primitives for
state binding, operation invocation, modal placement, field visualization,
asset refs, and runtime-neutral UI/render semantics.

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

The goal is not to outlaw useful runtime code. The goal is to make ownership
legible: renderers render, the daemon decides, Eve carries the contract.

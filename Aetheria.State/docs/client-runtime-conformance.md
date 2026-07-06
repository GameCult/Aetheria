# Aetheria Client Runtime Conformance

This is the client-runtime authority map for removing Unity as a privileged
gameplay body without replacing it with a new privileged runtime.

The daemon owns Aetheria game state, simulation, generated level content,
assets, render-field declarations, Eve/CultUI surfaces, and typed operations.
Eve owns the game-agnostic surface language: layout, interaction, state
pointers, world-state lowering, render-field instructions, and 2D/3D
scalar/vector field visualization contracts. Client runtimes lower those
surfaces and submit typed operations. A runtime may own presentation, input
hardware, local caches, native buffers, and frame timing. It must not own
gameplay truth merely because it can draw the game.

The desired end state is renderless Aetheria. Aetheria should not preserve a
Unity, Godot, Electron, or browser side path for game behavior. If a runtime
needs an Aetheria-specific rendering rule to show the game, that rule belongs in
daemon-authored Eve/CultUI or in a game-agnostic Eve lowering primitive.

## Runtime Roles

| Runtime | Role | Pass condition |
| --- | --- | --- |
| Hermodr | Unspecialized Eve/browser lowering sanity check for the RTS surface. | It reconstructs the Aetheria RTS gameplay surface from daemon-advertised Eve surfaces, typed state pointers, typed operations, render-field declarations, and CultMesh CDN assets without Aetheria-specific renderer code. |
| Electron | Player-facing Starbridge RTS client. | It renders the same daemon-authored RTS surface through the shared Eve package, with only app shell, launch, packaging, and runtime ergonomics outside Eve. |
| Unity | Current ARPG reference client and demolition target. | It behaves as a renderer/input shell over daemon state and Eve/CultUI/world-state surfaces; remaining GameObject, UI Toolkit, camera, input, and presentation adapters are classified shims or renderer-owned concerns. |
| Godot | Future unspecialized Eve/runtime parity target for the ARPG surface. | It reconstructs the ARPG gameplay surface from daemon API plus Eve/CultUI/render-field specs and CultMesh CDN assets, not from a Unity port or daemon-side Godot mode. |

Hermodr is the purity test for the RTS surface. Electron is the shipped RTS
client. Those are different jobs. If Electron can display something Hermodr
cannot reconstruct from Eve plus daemon API, either Electron is using a private
shortcut or Eve/CultMesh is missing a reusable primitive.

Godot has the same relationship to the ARPG surface that Hermodr has to the RTS
surface. It should prove the ARPG client can be rebuilt from generic Eve,
CultMesh state, daemon-owned assets, and engine-native lowering. It should not
inherit Unity scene authority, Unity coordinate leakage, Unity UI Toolkit
assumptions, or Unity-only gameplay adapters.

## Forbidden Shapes

- No daemon-side `Godot` mode, `Electron` mode, `Hermodr` mode, or `Unity`
  behavior branch for gameplay state.
- No renderer-local Aetheria compensator that changes game behavior instead of
  being promoted into Eve/CultMesh or daemon-authored state.
- No Aetheria-specific Hermodr plugin for RTS map semantics.
- No Godot port of Unity scene code as the ARPG authority.
- No Electron-only field, asset, or command interpretation that Hermodr cannot
  consume through shared Eve/CultMesh primitives.
- No Aetheria-specific Unity, Godot, Electron, or Hermodr renderer compensator
  for 2D or 3D scalar/vector field visualization.
- No daemon-published Eve surface that requires a named runtime to understand
  Aetheria gameplay semantics.
- No Aetheria-owned Unity/Godot/Electron world renderer that should instead be a
  game-agnostic Eve lowerer.

## Acceptance Gates

1. The daemon publishes the RTS surface as Eve/CultUI plus typed state pointers,
   operation handles, render-field declarations, and CultMesh CDN asset refs.
2. Hermodr lowers that RTS surface without Aetheria-specific renderer code.
3. Electron renders the same RTS surface through the shared Eve package.
4. The daemon publishes the ARPG surface with the same kind of portable surface
   contract.
5. Unity consumes the ARPG contract as a renderer/input shell, with remaining
   shims listed for deletion.
6. Godot lowers the ARPG contract without inheriting Unity authority.
7. Eve lowerers can reconstruct the required 2D and 3D gameplay surfaces from
   daemon-authored state pointers, field documents, asset refs, and operation
   descriptors without Aetheria runtime branches.
8. World-state lowerers live in Eve or shared runtime packages, while Aetheria
   contributes authored surfaces, state documents, assets, and fixtures.

Until those gates pass, Aetheria is daemon-authoritative in pieces, not free of
Unity as a complete client dependency.

# Aetheria Agent Instructions

This repo is mid-migration toward CultMesh-native gameplay state. Treat access
simplicity as an architectural requirement, not a nice-to-have.

## Canonical Typed State

- Default to one canonical shared typed document for each gameplay state concept.
  The daemon may own authority over that document, but it must not imply a
  separate private daemon truth plus a copied client-facing state document.
- The shared runtime assembly defines the document. The daemon mutates/publishes
  it. Unity, Electron, tools, tests, and later runtimes read the same document
  type through CultMesh.
- Client input should feel like grabbing a managed typed state handle and either
  reading it for display or writing/submitting through it according to authority
  policy. Prediction, debounce, routing, quorum, reconciliation, and smoothing
  are CultMesh responsibilities.

Use projections only when the state shape is intentionally different:

- hidden-information filtering;
- expensive or shared derived aggregation;
- viewport/windowed selection;
- SoA/native render or physics layout;
- lossy UI summaries;
- Eve/CultUI surface documents;
- compatibility mirrors with a named removal stage.

"The client needs to see it" is not a projection reason.

## Access Simplicity

- A UI-only inline value should be derived from already accessible typed state.
- A shared UI read should usually be: define the typed document, then read it
  with `client.State.Reactive<TDocument>()` or `client.State.Latest<TDocument>()`.
- A simulation feature should usually be: define the canonical typed document,
  let daemon authority mutate/publish it, then read or modify that same managed
  document from clients according to authority policy.
- Add named handles only when type alone cannot identify the state, such as
  parameterized viewport/detail documents, multiple documents sharing one CLR
  type, semantic query identity, operation policy, or native view ownership.

## Heretek Shape

The facade/projector/adapter/surface-builder chain is heretek now. Do not add a
stack of translation layers to recover one typed domain value.

One adapter at a true boundary is allowed:

- Unity GameObject presentation;
- Eve/CultUI lowering;
- legacy import;
- persistence;
- native view ownership.

Two or more translation layers in a row mean the code is missing a CultMesh
primitive, generated typed handle, canonical document, query surface, operation
handle, state pointer, or native view descriptor.

## Verifier Discipline

The verifier should encode the desired architecture, not the current accidental
shape. When removing old access paths, add checks that reject their return. Do
not write verifier rules that require wrapper/session/facade/projector chains as
proof of correctness.

Good verifier pressure:

- client code owns `CultMeshReactiveDocument<TDocument>`;
- client code reads `.Current`;
- client code uses generic typed document access where possible;
- hot render paths use named SoA/native views;
- operations are typed and authority-aware;
- legacy adapters are quarantined and named for deletion.

Bad verifier pressure:

- requiring `LatestFoo()`/`ReactiveFoo()` wrappers for single fixed documents;
- requiring one-document session wrappers;
- requiring facade/projector/adapter/surface-builder chains;
- treating renderer-local facade indexes as gameplay-state APIs.

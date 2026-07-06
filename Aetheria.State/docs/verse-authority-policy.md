# Verse Authority Policy

Aetheria authority is Verse policy, not topology. A Raven Unity client, a
Starfire RTS client, a headless host, and later an MMO witness quorum should all
use the same policy boundary:

```text
typed operation
-> resolve subject and claim kind
-> resolve authority mode from the active Verse policy
-> apply, forward, defer, or reject
```

The first implementation is intentionally fast for trusted co-op. It does not
build quorum consensus on the hot path. It compiles the active policy document
and authority leases into an in-memory routing table, then tests incoming typed
commands before simulation.

## Policy Modes

The policy schema names the authority shapes Aetheria needs without requiring
all of them to be implemented at once.

- `any-trusted-runtime`: trusted co-op mode. Any runtime with the active Verse
  rules may author matching claims.
- `host-authoritative`: only the configured host runtime may author matching
  claims.
- `delegated-runtime`: one of the configured runtime ids may author matching
  claims.
- `owning-runtime`: the runtime that owns the subject may author matching
  claims. This is schema-level intent for now; ownership resolution must be
  supplied by the runtime before this mode is enabled.
- `interest-lease`: a runtime with a matching unexpired lease may author
  matching claims.
- `witness-quorum`: eligible witnesses publish observations; policy forms a
  candidate; authority commits a fact. This is not implemented in the current
  Aetheria hot path.
- `operator-finality`: operator or host finality is required before the fact is
  committed.
- `mergeable-crdt`: independently authored state converges through explicit
  merge laws. This is for state with real merge semantics, not simulation facts.

Unsupported modes fail closed with an explicit policy rejection. This keeps the
schema open without pretending future authority structures already exist.

## Command Claims

Daemon commands are lowered to a subject and claim kind before execution:

```text
subject key: entity/run/zone/item key affected by the operation
claim kind: movement, targeting, combat, inventory, economy, ai, metadata...
author runtime: command.AuthorRuntimeId, falling back to ClientId
```

For example:

```text
SetMoveVector(entity.player.raven)
  -> subject entity.player.raven
  -> claim movement

SetTarget(entity.player.raven -> entity.hostile.3)
  -> subject entity.player.raven
  -> claim targeting

TradePurchase(station -> player cargo)
  -> subject target/destination entity
  -> claim economy
```

The command executor only sees authorized commands. It should remain boring:
validate payload shape, mutate the run, emit intents. Authority decisions belong
to the policy router.

## Trusted Co-Op Shape

The default Aetheria policy is permissive trusted co-op:

```text
*
  *
  any-trusted-runtime
```

Raven/Starfire tests can then tighten this without changing gameplay systems:

```text
entity.player.raven
  movement,targeting,combat
  delegated-runtime: raven-unity

entity.rts.starfire.
  movement,targeting,ai
  delegated-runtime: starfire-rts

entity.hostile.
  ai,movement,targeting
  delegated-runtime: starfire-rts

entity.hostile.
  close-combat-response,combat
  interest-lease
```

That leaves the door open to witness-authoritative MMO policy later while
keeping the co-op hot path as a few string/prefix checks and set lookups.

## Simulation Host Packaging

Aetheria should not require the current C# daemon executable to be the only
packaging shape forever. It does require the Aetheria simulation host role to be
daemon-shaped: a runtime that owns the typed state graph, applies Verse policy,
advances simulation, publishes high-performance views, serves assets, and emits
Eve/CultUI surfaces.

A standalone process, embedded native host, or future WASM/native kernel can
package that role. Those are deployment choices, not gameplay authority modes.
Unity, Godot, Electron, Hermodr, and browser shells are render/input lowerers
unless they are explicitly running the daemon-shaped simulation host behind the
same typed CultMesh boundary. A renderer does not gain authority because it
spawned, embedded, or happens to sit beside the host.

The important boundary is not the executable. It is the policy document:

```text
runtime CultMesh client
  -> typed command/proposal document
  -> Verse authority policy
  -> simulation-capable runtime lease/host
  -> committed facts and local projections
```

If no standalone daemon process is deployed, an embedded host may still run the
same daemon-shaped simulation role and publish the same typed documents. The
client shell around it remains a renderer/input surface. Starbridge can refine
hot claims without changing client protocols:

```text
entity.player.raven
  movement,targeting,combat
  delegated-runtime: raven-unity

entity.hostile.
  ai,movement,targeting
  delegated-runtime: starfire-rts

*
  inventory,economy,system
  host-authoritative
```

This keeps every client honest: it can run CultMesh and submit typed commands,
but the daemon-shaped simulation host owns the committed game state.

## Current Local-Mirror Constraint

The current C# implementation is not a pure remote-client architecture yet.
Unity can select a remote CultMesh Verse, but gameplay boot still requires that
Verse to be hydrated into a readable local CultCache replica before the Unity
shell starts. The Electron Starbridge client likewise uses local daemon
publications while using CultMesh for typed command submission.

That constraint is acceptable as a migration bridge, but it must not become the
architecture. The end state is:

```text
CultMesh transport/subscription
  -> typed Verse documents and operations
  -> local runtime projection/cache when useful
  -> renderer/input surface
```

The local cache is an optimization and offline/debugging aid. It is not the
authority boundary.

## Rust/WASM Simulation Kernel Migration

If browser-only clients are expected to take over simulation authority when no
local .NET daemon is available, the daemon should be split into an embeddable
simulation kernel and thin host shells. Rust is the strongest candidate for that
kernel because the same core can target native hosts and WebAssembly without
preserving the current local process/file assumptions.

The staged migration is:

1. Freeze the simulation contract in typed CultMesh/CultCache documents:
   command inputs, authority policy inputs, tick inputs, committed facts,
   frame/projection outputs, and deterministic error outputs.
2. Keep the C# daemon as the compatibility host while extracting every hot
   simulation decision behind that contract.
3. Port the deterministic simulation kernel to Rust with matching schema
   bindings and fixture-based parity tests against the C# host.
4. Compile the Rust kernel to native and WASM. Native and WASM hosts package
   the same daemon-shaped simulation role behind the same typed CultMesh
   contract.
5. Replace local-file-only readers with CultMesh subscriptions plus optional
   local cache hydration, so remote Verse access does not require a C# process
   on the same machine.

The C# daemon should then become one host shell among several, not the canonical
definition of simulation authority.

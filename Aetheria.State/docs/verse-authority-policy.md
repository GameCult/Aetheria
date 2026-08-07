# Verse Authority Policy

Aetheria authority is Verse policy, not topology. A Raven Unity client, a
Starfire RTS client, a headless host, and later an MMO witness quorum should all
use the same policy boundary:

```text
typed operation
-> resolve subject and claim kind
-> resolve authority mode from the active Verse policy
-> build immutable tick envelope and jurisdiction assignment
-> simulate Commander default candidate
-> accept, compare, defer, or reject Pilot candidate
-> correct/replay Commander provisional state on validated mismatch
-> commit one finalized fact
```

Starbridge does not use quorum consensus on the hot path. The Commander daemon
simulates everything by default and owns the canonical log. Policy assigns
prediction jurisdiction to Pilot daemons for their own ship, daemon-assigned
nearest environment entities, and assigned combat engagements. This is the
reverse of ordinary server-authoritative client prediction: when a valid Pilot
result disagrees, the Commander daemon corrects to the Pilot result.

## Policy Modes

The policy schema names the authority shapes Aetheria needs without requiring
all of them to be implemented at once.

- `any-trusted-runtime`: a trusted runtime may submit matching operations or
  candidates. It does not gain canonical finality.
- `host-authoritative`: only the configured host runtime is eligible to produce
  matching candidates or operations.
- `delegated-runtime`: one configured runtime may submit matching operations or
  candidates.
- `owning-runtime`: the runtime assigned to the subject is eligible to produce
  matching candidates or operations. This is schema-level intent for now;
  ownership resolution must be supplied before this mode is enabled.
- `interest-lease`: a runtime with a matching unexpired lease may produce a
  prediction candidate for the leased fact slots. The lease grants neither log
  mutation nor finality.
- `witness-quorum`: eligible witnesses publish observations; policy forms a
  candidate; authority commits a fact. This is not implemented in the current
  Aetheria hot path.
- `operator-finality`: operator or host finality is required before the fact is
  committed.
- `mergeable-crdt`: independently authored state converges through explicit
  merge laws. This is for state with real merge semantics, not simulation facts.

Unsupported modes fail closed with an explicit policy rejection. This keeps the
schema open without pretending future authority structures already exist.
Eligibility never implies direct canonical-log mutation; the active fact
strategy still decides candidate comparison and finality.

The implementation order is product-grounded: Terminus proves local authority,
Arena proves server authority, and Starbridge proves Commander-default,
Pilot-corrected mixed authority. Witness-authoritative operation begins only
after the Arena and Starbridge configurations pass live, restart, deterministic
replay, and negative-authority proofs through this same typed policy seam.

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

## Starbridge Prediction Jurisdiction

The Commander daemon owns default simulation and finality for every fact slot.
Policy narrows the slots where a Pilot candidate can correct that default:

```text
entity.player.raven
  simulation-fact.*
  delegated-runtime: raven-unity

environment.assignment.raven.*
  simulation-fact.*
  interest-lease

engagement.assignment.raven.*
  simulation-fact.*
  delegated-runtime: raven-unity

*
  simulation-fact.*
  host-authoritative: commander-daemon
```

The Commander player owns operation/claim authorship for everything outside
Pilot jurisdiction; the Commander daemon still simulates the default result for
all slots. Nearest-entity and engagement assignments are Commander-published
typed state with an epoch and exact effective tick. A Pilot cannot calculate a
larger jurisdiction from its divergent local positions. Overlap is resolved to
one holder before the tick, never by packet arrival order.

Commander and Pilot candidates are comparable only when they share the exact
session, simulation/content version, state root, run/jurisdiction epoch,
tick/substep, fact kind, canonical subject, engagement/claim id, causal input
set, and deterministic seed position. Equality is typed semantic equality for
that fact kind. Presentation differences do not veto gameplay facts.

Inside an open Pilot slot, the Commander result is provisional. A valid
different Pilot result wins; validation proves producer identity, versions,
state/input roots, jurisdiction, causal inputs, schema, and gameplay invariants.
It does not require agreement with the Commander output. The Commander daemon
rolls back or corrects and deterministically replays dependent provisional
state.

The tick envelope names the deterministic barrier/deadline policy and eligible
candidate set. The finality receipt records how that set closed. If no eligible
Pilot candidate is present when the barrier closes, the Commander result
stands. Replay consumes the recorded candidate/selection chronology rather than
re-running wall-clock or packet timing. Restart preserves whether a slot is
open or closed plus its eligible candidates. Late candidates cannot rewrite
final history. Disconnect or seat transfer changes jurisdiction only at a
committed future epoch.

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
  -> typed operation or prediction candidate
  -> Verse jurisdiction and candidate gate
  -> Commander default simulation and candidate selection
  -> deterministic correction/replay when Pilot wins
  -> one finalized fact log and local projections
```

If no standalone daemon process is deployed, an embedded host may still run the
same Commander-daemon simulation/finality role and publish the same typed
documents. The client shell remains a renderer/input surface. Pilot simulation
may also be embedded beside its renderer, but its durable output is candidate
evidence; only the Commander daemon appends finalized state.

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

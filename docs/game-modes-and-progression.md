# Aetheria Game Modes And Progression

Status: adopted target design; shared Hangar state is not yet implemented

## Product Modes

The first product spine contains three named modes. All three must remain
runnable at a basic level from the start; depth may arrive unevenly, but no mode
is deferred behind a mode-specific rewrite.

`Terminus` is Aetheria's single-player roguelike mode. A run begins from a
hangar-selected ship and loadout, crosses a daemon-generated sequence of
encounters and locations, and ends through extraction, victory, failure, or
abandonment. Its local daemon is the sole gameplay authority. Its minimum
complete loop is:

```text
deploy -> fly -> fight -> salvage -> dock/refit -> choose risk -> continue
       -> extract or die -> settle once into the Hangar
```

`Starbridge` is the cooperative mode. One commander and one to four pilots act
in one shared session. Starbridge is mixed-authority: typed authority policy
assigns bounded claim kinds to the commander, pilots, daemon, or leases while
all participants converge on the same canonical typed state and receipt
history. It uses the same player identity, hull catalog, equipment vocabulary,
loadout templates, fitting rules, and Hangar progression as Terminus while
adding the shared base, RTS projection, waves, fabrication, support systems,
and recovered-technology decisions.

`Arena` is the PvP and competitive game-simulation mode. Arena is
server-authoritative: the server owns admission, world state, simulation,
rules, scoring, and outcomes.
Human players and AI controllers are clients of the same typed observation and
operation contracts. It is the primary harness for training and evaluating NPC
agent policies and for running build-versus-build balance matrices under exact
rules, seeds, and loadouts. An AI may run faster than real time or in a batch
harness, but it cannot mutate simulation state through a privileged in-process
shortcut. Arena admission policy may restrict, normalize, loan, or price
eligible Hangar equipment without forking the inventory/loadout schema or
trusting a client-authored ship.

Headless execution is a capability of the shared simulation, not an Arena-only
implementation. Terminus, Starbridge, and Arena can all run without renderers
for regression, agent development, batch simulation, and operational hosting.
Attaching or removing Unity, Electron, or another Eve lowerer cannot change the
authoritative result.

The tutorial is onboarding, not a progression authority or a fourth inventory.
It may constrain a deployment, teach a Terminus-style run, and grant rewards
only through the same settlement path as every other mode.

## Authority Map

- **Owner:** one authoritative typed `Hangar` aggregate owns durable cross-mode
  progression for a player: owned hulls, stored equipment and cargo, currencies
  and materials, unlocks, saved loadout templates, and docked ship
  configuration.
- **Inputs:** authenticated player identity, accepted typed Hangar operations,
  the typed catalog, and terminal settlement requests carrying session id,
  deployment id, terminal fact id, mode-policy/version identity, and proposed
  Hangar delta.
- **Outputs:** canonical Hangar state, saved loadout templates,
  eligibility/affordability facts, and canonical settlement receipts and
  chronology.
- **Derived state:** Eve menus, inventories, compatible-slot lists, run
  summaries, scoreboards, and Unity/Electron presentation. A loadout template
  is a recipe over canonical catalog keys, not a second inventory or a live
  entity graph.
- **Forbidden writers:** Unity, Electron, AI controllers, Eve lowerers, proof
  scenario seeders, run checkpoints, mode authorities, and mode-specific
  bootstrap code cannot apply Hangar deltas or mint accepted settlement
  receipts. The Hangar cannot rewrite a live session after deployment.
- **Shared paths:** every mode reads Hangar state by identity, requests
  admission through a typed Deployment, and submits terminal outcomes through
  the same Settlement boundary. Hangar refit and template edits use Hangar
  operations. In-session dock/refit uses session operations and crosses the
  boundary only at settlement. Resume reopens Hangar or session state by
  identity and cannot redeploy or resettle.
- **Deletion line:** no `TerminusHangar`, `StarbridgeHangar`, Arena inventory,
  client-local unlock store, or mode-private loadout authority may exist.
  Fixture-only mutations remain confined to explicitly named witness profiles
  and are structurally unable to settle progression.

## Session Ownership

A mode admission authority validates a typed deployment request against a
versioned Hangar snapshot and mode policy, then emits an immutable committed
Deployment receipt/snapshot. The Hangar owns available durable assets and
configuration; it does not own Arena admission or Starbridge role assignment.

A mode session owns its live world: instantiated ships and equipment, damage,
temperature, energy, cargo gathered during the session, encounter state,
score, mode-local stock, and outcome. Starting a mode instantiates session state
from one committed deployment. After launch, changing the Hangar does not reach
into the live world.

The authority policy differs without changing the document model:

- **Terminus:** the local daemon decides all gameplay state.
- **Starbridge:** mixed authority assigns bounded claim kinds to participants;
  typed policy and committed receipts make ownership visible and revocable.
- **Arena:** the authoritative server decides all gameplay state; humans and
  AIs submit operations and consume observations.

The exact deployment/risk policy belongs to typed mode policy. Terminus may
risk the deployed hull, insured value, carried equipment, extracted salvage,
or some combination without changing the ownership model. Starbridge may loan
or share session equipment. Arena may normalize deployments. In every case,
the policy is evaluated by the applicable authority and reported in the
deployment and settlement receipts, not inferred by a menu.

## First-Wave Vertical Slices

Each mode must prove the shared spine early:

1. **Terminus:** deploy, fly, fight, salvage, dock/refit, and settle one run.
2. **Starbridge:** one commander and one pilot share a base/session, exercise
   one policy-owned command each, and converge on one outcome.
3. **Arena:** start a deterministic dedicated-server match, attach at least two
   controllers (human or AI), exchange typed observations/operations, resolve a
   scored combat outcome, and reproduce it from the same seed and inputs.

Arena is the canonical competitive simulation harness for AI development and
balance work. Batch and accelerated execution use the same server simulation,
admission policy, equipment rules, and command gate as live PvP. Controllers
can pit NPC policy versions against each other, compare ship/loadout builds, and
produce scored replayable outcomes. Renderers are optional consumers; headless
execution is not a separate gameplay implementation.

Run-local loot and credits do not become durable merely because a client can
display them. A mode authority submits a terminal outcome and proposed delta;
the Hangar validates identity, policy, and idempotency before committing it.
Every accepted terminal outcome—extraction, victory, failure, or abandonment—
produces exactly one canonical Settlement receipt, even when its delta is zero.
Retries return the existing receipt. A session without an accepted terminal
fact produces none. Reconnect and restart resume canonical Hangar or session
state; they do not replay rewards.

## Terminus Product And Witness Profiles

Terminus is the product mode. Its standard run is not a fixture. The existing
deterministic scenarios remain useful subordinate verification profiles:

- `released-client-proof` creates controlled combat and salvage conditions;
- `cargo-capacity-rejection-proof` creates a full-cargo rejection condition.

Those profiles exercise the same Terminus simulation, Eve surfaces, operations,
and Unity lowering as the product. They use distinct run identities and cannot
write Hangar progression. A proof profile may simplify content; it may not own
a parallel gameplay path.

## Current Migration Seam

The daemon already owns typed run checkpoints, loadout templates, catalog keys,
combat, loot, docking, trade, and Terminus scenario generation. Starbridge has
typed session, station-stock, role, wave, and summary documents. These are
components of the design, not yet a durable shared Hangar owner.

Arena does not yet have a named server/session/admission/score document family
or a preserved headless controller harness. Shared simulation smokes are useful
substrate, but they do not constitute the Arena mode. Starbridge's existing
documents also do not by themselves prove mixed-authority convergence.

The next state-architecture cut is to define the canonical Hangar, Deployment,
and Settlement documents; define one typed mode-selection boundary; and keep a
minimal Terminus, Starbridge, and Arena vertical slice alive against it. Until
that cut lands, documentation and verification must not describe existing run
checkpoints or proof scenarios as cross-mode progression authority, or generic
simulation smokes as Arena product proof.

## Required Proofs

1. A saved Hangar loadout deploys into Terminus and Starbridge and enters Arena
   admission through the same typed operation and catalog keys.
2. A Terminus run completes the playable loop, settles exactly once, and a new
   session observes the resulting durable Hangar state.
3. Failure, abandonment, reconnect, and daemon restart cannot duplicate rewards
   or resurrect losses.
4. Witness profiles cannot mutate Hangar progression.
5. Unity and Electron can read and request operations but cannot write Hangar,
   deployment, session, or settlement state directly.
6. No mode-specific inventory or loadout document can override the canonical
   Hangar.
7. Arena produces the same authoritative result for the same initial state,
   seed, ordered typed operations, and simulation version whether controllers
   are humans, AIs, rendered clients, or a headless batch harness.
8. Starbridge policy changes authority without changing document or operation
   schemas, and denied claims expose the active owner and reason.
9. Terminus and Starbridge also complete representative scenarios headlessly;
   attaching a renderer does not change their ordered facts or outcome.
10. Arena balance batches record simulation version, content/catalog version,
    seed, admission policy, controller policy identities, deployments, ordered
    operations, and scored outcome so build and AI comparisons are reproducible.
11. A terminal outcome cannot mutate Hangar state before Hangar acceptance;
    duplicate or reordered delivery returns the same receipt and delta.
    In-session refit and resume cannot invoke deployment or settlement.

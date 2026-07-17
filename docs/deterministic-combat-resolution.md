# Deterministic Combat Resolution

Status: daemon armor-cell damage ownership implemented; broader combat parity remains

## Objective

Combat outcomes are deterministic probabilistic transactions resolved by the
daemon from observer-local information and authoritative spatial state. A
rendered projectile is evidence of a committed shot, not the owner of hit or
damage truth.

The same resolver serves pilot cognition, RTS agents, autonomous NPCs, replay,
and conformance. No caller receives hidden target truth or a more accurate
execution path because it is human-controlled.

## Authority

### Owner

The daemon combat resolver owns shot identity, solution quality, seeded outcome,
impact distribution, damage transaction, and combat receipts.

For committed direct and deployable hits, the daemon damage transaction is the
single owner of layer ordering and mutation. It applies one shared
`shield -> armor cell -> equipment -> scalar hull` transaction; neither the
weapon path nor the deployable path owns a second damage model.

### Inputs

The resolver may read:

- the attacker's observer-local contact and information state;
- selected target and intended effect or subsystem priority;
- fire-control cognition allocation, quality, load, and retained solution;
- weapon range, dispersion, damage spread, penetration, damage type, timing,
  guidance, visibility, ammunition, heat, and energy state;
- Ymir-authored positions, orientations, velocities, angular motion, body
  bounds, and line-of-fire geometry at commitment time;
- target signature, known silhouette/topology, countermaneuver,
  countermeasures, and only those defenses exposed by gathered information;
- doctrine constraints and accepted semantic action parameters;
- a deterministic entropy key derived from run seed and stable shot identity.
- the target hull shape and hardpoint catalog topology used to initialize its
  armor and maximum-armor grids;
- the resolved impact position and direction supplied by the direct-shot or
  deployable detonation path.

Missing knowledge remains uncertainty. The resolver must not replace an
observer's weak contact with canonical target internals when constructing its
forecast or firing solution.

### Outputs

One committed shot produces an immutable receipt containing:

- shot, attacker, target-contact, weapon, and semantic-action identity;
- observation versions and solution inputs used;
- normalized solution quality and factor contributions;
- hit probability and deterministic hit roll;
- miss or hit classification;
- aim intent and deterministic impact distribution;
- impact region/cell or miss vector;
- damage type, penetration, pre-mitigation damage, mitigations, and applied
  shield/armor/hull/equipment damage;
- resource, heat, signature, cooldown, ammunition, and wear transactions;
- causal refusal or cancellation facts when no shot was committed;
- presentation timing and trajectory hints derived from the result.

Receipts are CultCache/CultMesh state. Eve projects them without becoming a
second combat model.

The daemon also publishes the mutated armor and maximum-armor grids. Aggregate
armor is derived from those cells for compact Eve status; exact cell values are
available for the schematic. Scalar hull health is the terminal remainder of
the transaction, not a substitute armor owner.

## Pipeline

```text
semantic fire request
  -> command validation and doctrine gate
  -> observer-local firing-solution snapshot
  -> weapon resource transaction
  -> deterministic hit and impact sampling
  -> canonical damage transaction
  -> immutable shot receipt and feedback events
  -> derived Eve presentation trajectory/effects
```

The transaction boundary is the committed shot. Resource payment, outcome,
damage, and receipt publication either commit together or do not happen.

## Deterministic Entropy

Each stochastic dimension uses a named roll derived from:

```text
run generation seed
shot sequence or stable shot id
attacker entity id
weapon owner/behavior identity
target contact id
named dimension salt
```

Named dimensions include `hit`, `impact-angle`, `impact-radius`,
`penetration`, `damage-spread`, `countermeasure`, and `malfunction`. Adding a
new roll must not perturb existing dimensions. Replaying the same accepted
commands against the same state versions produces the same receipts.

## Solution Quality

Hit probability is not one opaque accuracy stat. The receipt publishes bounded
factor contributions, initially:

- contact information and evidence age;
- lock/solution maturity;
- fire-control cognition quality and allocated throughput;
- range against weapon effective range and damage curve;
- weapon dispersion and platform stability;
- target apparent size, aspect, transverse motion, and acceleration uncertainty;
- attacker motion and damaged-control penalties;
- target signature, jamming, decoys, and defensive maneuver;
- aim-point specificity and subsystem knowledge;
- guidance quality and time of flight where relevant.

The first implementation may use a documented weighted log-odds model. It must
retain named contributions so later calibration can change without turning the
result into an unexplained percentage.

## Impact Distribution

A hit samples an impact distribution rather than applying damage to an abstract
target total. Distribution width comes from residual solution error, weapon
spread, target motion uncertainty, range, and cognition quality. Distribution
center comes from accepted aim intent and the observer's known target topology.

Low-information subsystem targeting broadens or biases the distribution; it
does not secretly reveal the canonical component location. The sampled impact
then enters the target's shield, armor, hull-cell, equipment, thermal, and crew
damage pipeline.

The implemented damage transaction maps the deterministic impact onto the
target schematic. Damage spread expands through orthogonally adjacent cells.
Penetration advances through the schematic in 0.5-cell increments, including
the fix for the fossil path that could fail to advance and loop forever. It
stops at the first sample outside occupied hull topology, preserving the
fossil's contiguous penetration rule rather than tunnelling into a disconnected
hull island. Equipment interception uses the same rotation-aware footprint as
refit placement, thermal simulation, and schematic projection. Damage type
remains typed and is passed through state and receipts, but does not apply
resistance modifiers: the fossil carried damage types but never used them for
resistance.

Direct hits begin at the deterministic impact cell; a hit on a hardpoint begins
with that hardpoint's complete footprint before spread and penetration expand
the shape. Splash payloads such as mines preserve the fossil's distinct rule:
they select the source-facing half of the hull schematic, then enter the same
layer transaction.

Lethal damage commits destruction in the same daemon transaction boundary.
The target receives one durable destruction identity, leaves the active Eve
world, clears combat/docking references, and emits deterministic pickups. The
fossil rules are preserved: every cargo stack drops, each equipped item rolls
independently against `0.25`, launch velocity has magnitude `25`, and pickup
lifetime is `30` seconds. A retained entity row is a non-rendered tombstone for
stable identity and exactly-once replay protection; it is not a wreck or a
second gameplay owner. The reserved `DestroyEntity` command is rejected.

## Armor-Cell Authority Map

- Owner: the daemon's canonical damage transaction owns shield interception,
  schematic impact mapping, armor-cell mutation, equipment damage, and scalar
  hull remainder for both direct and deployable damage.
- Inputs: hull and hardpoint catalog topology, initialized armor and
  maximum-armor grids, authoritative target state, deterministic impact and
  direction, damage amount, spread, penetration, and typed damage type.
- Outputs: atomically mutated shield resources, armor cells, equipment state,
  scalar hull, aggregate/exact Eve armor facts, and shot/damage receipts with
  the layers reached and damage applied at each layer.
- Derived state: aggregate armor, schematic presentation, impact effects,
  meters, and scalar summaries are views of the canonical transaction. Damage
  type is currently pass-through evidence, not a resistance table.
- Forbidden writers: direct-weapon handlers, deployable detonation handlers,
  Ymir contacts, Unity/Eve presentation, scalar-hull helpers, and repair loops
  may not independently order layers or mutate armor/equipment/hull.
- Shared paths: direct shots and deployable detonations submit the same damage
  transaction after producing their authoritative impact facts.
- Cut line: the old scalar-hull-only and per-caller damage paths no longer own
  damage. Catalog topology initializes the grids once; after initialization,
  only the daemon damage transaction mutates them.

## Charged Weapons

A charged fire request commits time before it commits a shot:

1. The daemon begins spool without requiring a current firing solution.
2. Charge consumes authored energy and heat and reaches ready state.
3. If an acceptable solution exists at readiness, the shot commits.
4. Otherwise the weapon holds full charge while solution state continues to
   evolve.
5. After the authored safe hold interval, one deterministic malfunction hazard
   check occurs per completed held second. Risk increases with overdue time.
6. The first acceptable solution commits the shot automatically; a separate
   semantic cancel command may discharge it safely if the weapon supports that.

This preserves the tactical timing commitment without rebuilding a release-key
dexterity test that agents solve differently from humans.

## Ymir Boundary

The Aetheria simulation owns committed world geometry and kinematics and uses
Ymir as its deterministic spatial kernel. At commitment, the resolver may invoke
Ymir for relative transforms, motion, body bounds, occlusion, and line-of-fire
constraints. Ordinary weapon damage does not wait for a projectile body to
overlap a target.

Physical entities remain appropriate when their continued existence changes
the world: mines, drones, interceptable missiles, torpedoes, debris, boarding
craft, area fields, and recoverable or destructible payloads. Those objects are
not exempt from deterministic fire-control receipts. Their later motion and
collision are computed by Ymir and committed by the Aetheria simulation.

For deployables, physics contact is evidence rather than consequence. Aetheria
authors deployment, arming, trigger and detonation clocks, target eligibility,
blast magnitude, damage, and event chronology. The injected Ymir implementation
computes integration and overlap/contact facts inside that transaction. The
production daemon currently embeds it in-process; moving the computation across
CultNet must not create another persisted world or change simulation authority.

Mine lifetime and source-relative range expiry are also daemon-owned trigger
causes. They enter the same detonation transaction as proximity triggers and
retain their reason in the event chronology. Ymir movement happens before the
range check, matching the original simulation ordering.

Shield interception follows the original behavior contract rather than a
synthetic shield hitpoint pool. An enabled equipped Shield behavior may absorb
the full incoming damage only when canonical capacitors can fund
`damage * EnergyUsage`; absorption consumes that energy and adds
`damage / Efficiency` heat. Otherwise damage reaches the hull transaction.
Receipts publish absorbed damage, hull-applied damage, energy consumed, heat
generated, and the semantic `shield` or `hull` impact kind.

## Eve Presentation

Eve receives:

- persistent firing-solution and weapon state;
- immutable shot and damage receipts;
- aggregate armor status and the exact armor/maximum-armor schematic grid;
- hit/miss, impact, malfunction, refusal, and destruction chronology;
- derived trajectory hints such as origin, impact/miss endpoint, launch time,
  impact time, effect identity, and view channels.

Unity, Electron, and future runtimes may interpolate tracers, beams, recoil,
audio, impact effects, and cockpit instruments from those facts. A renderer may
drop or simplify an effect. It may not promote a visual collision into damage.

## Forbidden Writers

- `IAetheriaRuntimeWorldPhysics.StepPhysicalPayloads` may report Ymir-backed
  motion and query facts but may not decide ordinary weapon damage.
- Unity physics, effect managers, colliders, and particle systems may not report
  hits back as combat truth.
- cognition and agents may propose shots but may not sample or apply outcomes.
- Eve lowerers may not reconstruct hit probability from presentation props.
- a repair loop may not reconcile client-observed impacts into daemon health.
- direct and deployable callers may not bypass the shared layer transaction
  with scalar hull damage.
- Unity, Eve, and command clients may not remove an entity or manufacture its
  loot after observing a destruction effect.

## Cut Line

Ordinary weapon authority is the shot resolver and damage transaction. It does
not spawn a physical payload, advance one through Ymir, or apply damage from a
returned contact. Physical payload documents exist only for objects whose
continued motion and contact are gameplay state; their contact facts do not
implicitly carry weapon damage authority.

The cut is complete only when a physical payload contact can no longer alter
ordinary weapon damage and a missing presentation trajectory cannot suppress a
resolved hit.

## Conformance

Required proofs:

1. Identical state and semantic commands produce byte-equivalent shot receipts.
2. Changing only a named entropy salt does not perturb other roll dimensions.
3. Weak target information changes solution quality without reading hidden
   target topology.
4. Cognition quality, range, aspect, and target motion contribute independently
   and are visible in the receipt.
5. Hit and miss both consume the committed weapon resources.
6. Direct and deployable impacts share the shield, armor-cell, equipment, and
   scalar-hull transaction, with layer facts preserved in receipts.
7. Removing all projectile presentation still produces the same damage receipt.
8. Injecting a fake visual collision cannot produce damage.
9. Charged hold risk replays exactly and commits immediately when a valid
   solution appears.
10. Pilot and agent requests traverse the same command, resolver, and damage
    transaction.

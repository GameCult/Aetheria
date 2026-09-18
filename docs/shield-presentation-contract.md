# Shield and Pickup Presentation Contract

Date: 2026-09-18

Status: target, operator direction only. Nothing here is built or scheduled. The
panel interceptor's means live in `docs/shield-panel-cut.md`; the Lariat has no
map. This document owns the contract both answer to.

## Why

Aetheria is growing more than one presentation of the same capabilities: the
existing field shield (`FieldDriver`), the hard-light panel interceptor, and the
Lariat. They must be interchangeable without the simulation knowing which one is
equipped.

## Rulings (operator, 2026-09-18)

- **Every shield effect is a drop-in behavioural replacement.** A presentation
  takes capability events, presents them, and decides nothing.
- **Coverage is expressed through the ship's envelope**, not through an
  effect's own geometry. The envelope is derived from the existing per-ship
  shield transform, not authored twice (`docs/shield-panel-cut.md` R6).
- **Presentation may anticipate.** Operator: "anticipatory is great, and we'll
  want it for other one too". A presentation may begin reacting before a shot
  lands, and may be wrong when it does.
- **One presentation may serve several capabilities.** Operator: "the Lariat
  would be a presentation for both pickup and shielding capabilities."
- **The hits a presentation shows are cosmetic.** Fire control decides what
  happened; the intercept point is where the presentation says the shot arrived
  (`docs/shield-panel-cut.md` R6).

## The anticipation feed

Capability events (`ServerShared/CapabilityEvents.cs`) report what happened.
Anticipation needs what is *about to* happen, which is a different kind of
signal and must never be mistaken for the first:

- **It is speculative.** A threat may miss, be absorbed by something else, or
  be destroyed in flight. A presentation that committed to it is simply wrong,
  and a whip that lunges at a shot it does not block is characterful, not a bug.
- **Nothing outside presentation may read it.** No gameplay decision, no
  damage, no state change. If a consumer wants certainty it waits for the
  capability event.
- **Its source is the simulation's shots in flight**, not Unity's `Projectile`
  instances, once fire control moves shot resolution into the sim
  (`docs/three-gates-scope.md`). Until then a Unity-side feed stands in, and it
  is the presentation layer's own business.
- **It carries enough to aim at:** where the threat is, where it is going, when
  it is expected to arrive, and how big it is. Not who fired it, not what it
  will do.

## A presentation serving several capabilities

The Lariat answers absorb and grab with one object, so it owns an arbitration
the single-capability presenters never needed:

- **It decides what it is currently doing** — idle, snapping, coiling, reeling
  — and what interrupts what. That decision is presentation-local.
- **It may not delay or deny the simulation.** The pickup completed and the
  damage was absorbed when the simulation says so, whether or not the whip has
  finished reeling. A presentation that is still animating is a presentation
  that is behind, never a gameplay state.
- **It may refuse to show something** (a second absorb mid-coil), and that
  refusal is cosmetic.

## Open

- Where the anticipation feed lives, what publishes it today, and what publishes
  it after fire control.
- Whether the Lariat is one item carrying both capabilities, or a presentation
  shared by two items. The item data has to say which.
- The Lariat's strand: a node chain with distance and bending constraints, drawn
  as a tube, with the snap as a travelling kink. `FieldDriver`'s bezier tendril
  (extend, envelop, pull, damped sliding base) is the existing half, and it
  cannot leave the field surface.

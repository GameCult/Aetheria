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
  lands, performing an outcome the simulation has already committed (below).
- **One presentation may serve several capabilities.** Operator: "the Lariat
  would be a presentation for both pickup and shielding capabilities."
- **The hits a presentation shows are cosmetic.** Fire control decides what
  happened; the intercept point is where the presentation says the shot arrived
  (`docs/shield-panel-cut.md` R6).

## The commit window, which is where anticipation comes from

Operator, 2026-09-18: "we get anticipation for free when we move to roll-based combat, where
we fudge the presentation to show whatever the simulation rolled", and, on how that squares
with resolution on arrival, "That hybrid is the way".

So a shot still **resolves on arrival**, and evasion still matters during flight
(`docs/three-gates-scope.md`), but its outcome is **committed a short fixed time before
impact**. That commit is the anticipation feed, and it changes the contract:

- **A committed outcome is authoritative, not speculative.** A presentation that acts on it
  is performing a decision the simulation has already made. The earlier framing of
  anticipation as a guess that may be wrong is superseded: presentations do not predict.
- **Evasion counts until the commit.** Deviation from the predicted intercept feeds the roll
  right up to the commit horizon; after it, the result stands and the remaining flight is
  choreography.
- **The horizon is one authored number**, long enough for a whip to snap or a panel to
  materialise, short enough that late evasion still covers most of the flight. It belongs
  with the other fire-control settings, not in an effect.
- **A commit carries what a presentation must perform:** what happens (hit, absorbed, miss),
  where, when it arrives, and which capability answers it. Not who fired, not the damage
  numbers.
- **A miss is choreography too.** The presentation may show a near-miss, or a whip lunging
  and failing, because the roll said miss — a deliberate performance, not an error.
- **Nothing outside presentation reads a commit to change state.** Damage, pickups and
  everything else still happen through the capability events on arrival.

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

- Where the commit window lives (fire control's owner), its horizon value, and what
  presentations read before fire control exists: there is no commit to read yet, so the
  panel's first landing reacts on arrival and looks a beat late by construction.
- Whether the Lariat is one item carrying both capabilities, or a presentation
  shared by two items. The item data has to say which.
- The Lariat's strand: a node chain with distance and bending constraints, drawn
  as a tube, with the snap as a travelling kink. `FieldDriver`'s bezier tendril
  (extend, envelop, pull, damped sliding base) is the existing half, and it
  cannot leave the field surface.

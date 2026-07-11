# Original Game Presentation Specification

Baselines: RTS `d12d7c5c^`; ARPG `origin/master` at `ab2c2944`.

## Provider Documents Required

The original UI directly observes mutable entities and UniRx events. The target
must replace that graph with provider-owned typed documents:

1. `player_cockpit_state`: systems, schematic cells, thermal state, durability,
   energy, visibility, movement, shutdown, and medical state.
2. `zone_contacts`: identity, disposition, detection both ways, pose, selected
   and locked state, and health disclosure.
3. `action_slots`: action identity, source equipment/cargo, icon, availability,
   active state, progress/cooldown, quantity, and operation binding.
4. `inventory_layout`: shaped hull/cargo occupancy, cells, hardpoints,
   durability, armor, conductivity, and temperature.
5. `trade_session`: station, stock, destinations, quotes, capacity, commands,
   receipts, and typed rejection reasons.
6. `navigation_surface`: zone topology, contacts, fields, discovery, current
   location, and navigation commands.
7. `presentation_events`: deduplicated hit, damage, lock, thermal, death,
   weapon, effect, and audio cues.

Names are provisional; responsibilities are normative.

## Cockpit Schematic

The schematic presents equipment topology and polls reactor draw, capacitor
charge/capacity, temperatures, radiator stores, cargo temperature, override
shutdown, shield active state, drive RPM, visibility, hull/item durability,
weapon ammunition/range/cooldown, and heatstroke/hypothermia thresholds.

The target document must publish these source facts and cell topology. Eve may
compose them as grids, metrics, progress, imagery, and overlays. EveUnity may
animate transitions but may not derive state from materials or particles.

## Target And Contact Feedback

The selected target panel includes identity, relation, target shield state,
hull durability, gathered information in both directions, pose/range, and
selection. Hostile/friendly indicators are created from contact add/remove
facts. Detection fill, selected target, and lock indicators consume explicit
observer-relative state.

Crosshairs and lock reticles need camera projection and attachment semantics.
Barrel endpoints and articulation groups are presentation inputs, not combat
authority.

Hit marker is triggered only by an incoming-hit event on the selected target
whose source is the controlled entity. It remains visible for authored
`HitMarkerDuration`. This requires event identity, source, target, and time.

## Action Bar And Input

Slots derive from exposed action-bar inputs plus available equipment behaviors,
weapon groups, and consumables. They display binding, active state, cooldown or
duration, and cargo quantity. Activation submits semantic commands; it never
calls behavior objects in the client.

Bindings are client-owned and portable across providers exposing the same
semantic action. Dynamic action availability is daemon-owned.

## Inventory And Trade

Inventory presents shaped hull and cargo grids, armor, hardpoints, occupancy,
temperature, conductivity, item condition, comparison, selection, drag preview,
and valid destination feedback. Drag state is local; mutations and rejection
reasons are daemon receipts.

Trade presents station stock, player destinations, credits, valuation, item
details, filters, ship purchase, and resulting inventory state. Quote and commit
are separate when price can vary.

## Map Products

Minimap zoom changes camera/projection scale and icon scale independently from
pilot view distance. Zone map combines objects and field textures. Sector map
builds discovered topology and faction/current/entrance/exit/boss states.
These are explicit Eve projections, not hidden children of the pilot scene.

## Distress, Damage, And Death

Incoming damage identifies affected schematic cells and damage category so the
correct armor/item/hull feedback can pulse. Thermal distress publishes current
heatstroke/hypothermia, thresholds, and cause-specific warning transitions.
Screen post-processing intensity is a lowering of these facts plus authored
timing, with accessibility fallback.

Death unbinds gameplay input, presents cause, and transitions to appropriate
menu/restart flow. The client cannot clear or save authoritative world state as
a side effect of playing the animation.

## Effects And Audio

Weapon and impact effects consume semantic event facts and provider asset refs.
Original music selection attempted to use `TargetedByCount`, but playback was a
TODO in the inspected baseline. Audio parity therefore covers authored effect
cues that exist; a complete music system is future content, not a hidden
original requirement.

## Presentation Timing Proof

For every element record source document/event, trigger, duration, interpolation
owner, interruption rule, reconnect behavior, and accessibility fallback.
Verify initial load, user command, daemon update, mid-transition, settled state,
refresh/reconnect, and event deduplication.

# Reactive world: target draft

Status: **draft target**, 2026-10-03. Nothing here is mapped. The operator's
rulings are in the eureka mind (campaign `aetheria-release`); this note holds
the design as discussed, so it survives a context rotation. Prior art lives
beside it in `F:\Projects\`:

- `aetheria-authored-missions-prior-art.md`: FreeSpace 2, X4, Starsector;
  Aetheria's faction-presence consumers; dead `StoryProcessor`.
- `aetheria-reactive-narrative-prior-art.md`: storylets, Ruskin, Paradox,
  RimWorld, Wildermyth, Rete; Aetheria's readable state.
- `aetheria-radiant-prior-art.md`: Bethesda's package stack and Story Manager.
- `aetheria-story-sifting-prior-art.md` (pending): story sifting, lifting,
  weaving, and Ghostlight's actual state.

## Intent

The operator's words, in order:

> what I really want is for these scenarios to be flexible enough to represent
> scenes or missions that are fully authored. Only as much proceduralism as the
> author decides to inject. You know, like a mission in Freespace 2

> The scripting primitives we'll need for decent narrative coverage are things
> that were long overdue, things we'd have needed before release regardless for
> things like scripting fun boss encounters

> what we want is a system that can dynamically activate when the right
> conditions are met, and mutate the world state as the script demand. This can
> scale from simple conditions like an NPC being in a given state triggering an
> authored bark, to quest chains spanning multiple zones with stations chosen
> for their faction and economic role and authored NPCs that can be broken out
> of their loops by emergent interactions.

> I want to be able to listen in on radio chatter everywhere I go, intercepting
> dispatches, sightings, reports, news, gossip, threats, deals, random chatter.

> the only way to run Ghostlight here would be to first let it seed a world,
> elaborate it to our target detail level, then simulate the whole dang world
> for a while just to let things brew. This establishes the baseline state.
> From there we can identify promising narrative threads simulate a bounded set
> of player actions as branching state and let them propagate just enough that
> we can feel their consequences, and then extract each thread into a storylet
> that gives us a narrative blob with a bunch of pre and postconditions which
> our storyteller can weave a world out of.

Rulings in force: `scenario-is-compiled-script`, `game-modes-are-scenarios`,
`scripting-primitives-in-release-scope`. Scripts are C# compiled with the game;
Aetheria is open source, so modders compile with the same toolchain.

## Two halves

**Build time (Ghostlight): simulate, sift, lift.** Seed a world, elaborate it to
target detail, simulate until it has a baseline, sift the history for promising
threads, branch each over a bounded set of player-action archetypes (help,
betray, ignore, intercept), propagate to a fixed depth, then lift each thread
into a storylet: roles, preconditions, content, postconditions. Storylets ship
as typed CultCache data.

**Run time (Aetheria): the reactive engine weaves.** Typed events wake rules and
stories; role queries bind actors and places; effects change the world through
its owners. Extracted storylets compile into the same `Story` shape the engine
runs, so hand-authored scenarios, bosses and missions share one executor with
generated content.

The two halves meet at one artefact: **the shared state vocabulary** (below).

## Runtime engine (MVP shape)

One reaction = trigger (an event, never polling) + roles (ordered queries,
each may reference earlier roles; a required role that cannot be filled means
the reaction does not fire) + conditions + selection + effects (verbs that call
owners, never write state directly).

1. **Behaviour stack on `Agent`.** Override, then behaviour lent by the
   stories an agent fills roles in (by story priority), then its own routine,
   then default. Lent behaviour is derived from active roles, never added and
   removed by hand (Radiant's imperative alias side effects leak; ours must
   not). Every effective state change raises `AgentStateChanged`. Today
   `Agent._currentState` is private and `Transition` raises nothing
   (`Agents/Agent.cs:19, 38-43`).
2. **Typed event surface** over the existing streams (`Death`, `HullDamage`,
   `ItemDestroyed`, `WeaponDestroyed`, `TargetedBy`): `EntityDied`,
   `HullCrossed`, `TargetedBy`, `AgentStateChanged`, `Docked`, `ZoneEntered`,
   `TransmissionReceived`, `IntelReceived`, `StoryStageChanged`, timers.
3. **`Rule`** (stateless; barks and small incidents): trigger, criteria,
   action, cooldown; most-criteria-matched wins (Ruskin).
   **`Story`** (persistent): a start (trigger with filter, or manual from a
   scenario or another story), ordered roles with `Optional` and `Reserve`,
   stages of event handlers scoped to bound roles, and state saved as one typed
   CultCache document in the run save. `Scenario` becomes a story started from
   the menu. `ZoneConstraint` survives as location-role predicates; Ink does
   not carry this system.
4. **Verbs**: the `ScenarioStage` vocabulary grown up: fit, place, admit a
   wing, lend behaviour, transmit, set or complete an objective, hull floor and
   invulnerability, friend-or-foe, advance or end a story.
5. **Inspector from day one**: every event's candidates, the failing role or
   condition, what started; active stories with roles and stages.

MVP content, one per scale: a bark (pirate taunts, pleads at low hull); a boss
scenario (hull floor, phase change, wing, subsystem kill, objective); a radiant
bounty (role-bound target across zones, lent loiter behaviour, payout,
persisted), heard on the radio rather than only offered at a dock.

## Radio

A transmission is typed state: speaker, channel (open, faction band,
encrypted, distress), origin, reach, content. Interception is range plus
receiver capability plus decryption, so comms gear matters. Transmissions
deliver storylet beats and world facts; heard intel raises `IntelReceived`,
which stories can trigger on (stumble into a deal, a convoy dispatch, a
distress call that is bait). Prior art: STALKER's PDA news from A-Life events.

## Shared state vocabulary

Preconditions and postconditions must be checkable and writable in state
Aetheria tracks at run time. Ghostlight's ontology (knowledge, relationships,
debts, institutions) is richer than Aetheria's entity and faction state, so
Aetheria carries a bounded set of typed social qualities (Failbetter's
quality-based narrative). The vocabulary is CultCache schemas shared by both
halves, and it is designed before either pipeline.

## Open forks

1. **Lifting versus one canonical world.** Galaxies vary by seed, so a sifted
   thread about specific people must be lifted to role schemas, at the risk of
   flattening its specificity; or Aetheria ships one canonical simulated world
   and skips lifting, trading replay variety for specificity.
2. **Weave consistency.** Storylets coherent in their own branch can conflict
   when woven (both need the same captain alive). Extraction must emit
   conditions that make interference detectable; the build runs a weave
   validator over sampled orderings.
3. **Branch bound.** Which player-action archetypes, what propagation depth,
   which branches are kept.
4. **Live enrichment.** Whether a live Ghostlight ever runs beside the game
   (named characters, situations that last long enough), with the shipped
   corpus as the floor that always works.
5. **Off-screen in the runtime.** MVP rule: story handlers run regardless;
   lent behaviour matters only in the loaded zone (Bethesda's split).
6. **Director.** None in the MVP; selection stays pluggable. Radiant's lesson
   is that repetition comes from a small pool of places more than from a
   missing director.
7. **Persistent rivals.** WB holds a US patent on Nemesis (issued 2021); check
   its claims before designing NPCs whose rank changes through play.

## Dependencies

- Economic roles for role queries wait on the economy work
  (`docs/faction-territory-target.md:35-39`, `docs/item-provenance-target.md`).
- `Faction.Personality` (`Corporations.cs:28-29`) and `DemandProfile`
  (`ItemData.cs:335-336`) exist and nothing reads them.
- Events-not-polling is already ruled in `docs/tutorial-script-sketch.md:30-47`
  and `docs/three-gates-scope.md:71`.
- The scenarios harness (`eureka/aetheria-release-scenarios-menu`) is the seed
  of the `Story` executor.

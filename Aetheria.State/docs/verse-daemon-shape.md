# Aetheria Verse Daemon Shape

Aetheria should follow the Odin/VoidBot daemon shape instead of growing a private Unity-shaped command system.

## Authority

Odin is the all-seer, not the owner. It discovers Aetheria's provider advertisement and interface bindings, indexes the schema/catalog surface, and gives agents a registry overview. Idunn keeps the daemon alive from daemon-published health and command-boundary records. Bifrost hosts the MCP crossing for Codex and other xeno agents. Eve/CultUI defines presentation shape. The Aetheria daemon remains the side-effect owner.

The daemon owns authority over Aetheria simulation state, typed command
application, health publication, command boundaries, provider advertisement,
and provider-owned Eve GUI/TUI surfaces. Authority does not imply a private
daemon truth plus a separate client state copy. Wherever possible, the daemon
mutates and publishes the canonical shared typed document, and Unity, a browser,
a terminal, Blender, or another runtime grabs a managed handle to that same
document type through CultMesh.

Projection documents are for different shapes: hidden-information filtering,
derived aggregation, viewport/windowing, SoA/native render layout, lossy UI
summaries, Eve/CultUI surfaces, and named compatibility bridges. A projection is
not required merely because a non-daemon runtime needs to read state.

Witness-authoritative networking sharpens this rule. Unity should not be a dumb terminal; it may hold local projection caches, prediction state, render-native SoA views, and eventually immutable witness observations for facts it could actually observe under the active Verse rules. Those local records are admissible testimony, not committed truth. Observation, prediction, consensus candidates, and committed facts are separate stages. Unity can lower input documents, predict local presentation, and publish typed witness documents through CultMesh, but it must not mutate canonical Aetheria state or promote a local projection cache into authority.

Unity-side entity graph creation must name the boundary it is crossing. `EntityConstructionBlueprintProjector.InstantiateAuthoritativeFromBlueprint` is for local authoritative construction paths that still need to be migrated into the daemon; daemon frames use `EntityConstructionBlueprintProjector.ProjectObservedFromBlueprint` to lower observed state into Unity objects. A Unity frame consumer must not call the authoritative construction entry point.

The same rule applies at the galaxy shell. `Galaxy.ProjectObservedSectorMap` may build a temporary Unity navigation/rendering projection from the typed sector-map document. Main menu boot must not call a `Galaxy` constructor directly, run procedural generation, or read whole daemon frames for this path; the daemon owns the run and Unity lowers typed projection state. The Unity-side handle is `AetheriaUnityObservedRunProjection`, not an `ActionGameManager` gameplay property, because the graph is a quarantined scene adapter rather than portable client state.

VoidBot's Odin MCP shape is the useful boundary model for agents: list providers through Odin, list Verses through Odin, load a provider-owned interface context through Odin, and invoke only commands that the provider interface advertises. The MCP is not a Brokkr special case and not a raw daemon socket wrapper.

Brokkr is just one daemon that happens to publish Unity editor surfaces. Aetheria should behave the same way: publish provider advertisements and Eve GUI/TUI surfaces into the local Verse, let Odin index them, and let Bifrost expose ergonomic MCP tools that lower or invoke those surfaces without taking ownership.

## Publications

Required Aetheria daemon publications are the live typed schemas in `AetheriaRuntimeDaemonSchemas`:

- `gamecult.aetheria.daemon_provider_advertisement.v1`: Verse id, provider ids, daemon id, schema catalog, state witnesses, Eve surface keys, command boundaries, and transport profile metadata.
- `gamecult.aetheria.daemon_health.v1`: daemon id, Verse id, state path, frame id, command acceptance status, simulation status, publication source, transport, and command-boundary path.
- `gamecult.aetheria.daemon_command_boundary.v1`: typed command schemas accepted by the daemon, authority requirements, receipt schema, and delivery transport.
- `gamecult.aetheria.daemon_frame.v1` plus `gamecult.aetheria.daemon_soa_view.v1`: authoritative simulation state for renderer clients.
- `gamecult.aetheria.daemon_game_surface.v1` and `gamecult.aetheria.daemon_editor_surface.v1`: GUI/TUI views of the same provider-owned state.

The provider advertisement is not a dashboard index. It is the daemon's promise about what it owns, where its witnesses live, what command boundary it accepts, and what transports can reach it.

Aetheria currently has two advertisement layers during the bridge:

- `gamecult.eve.provider_advertisement.v1` is the Odin-visible Eve provider card for the Aetheria Verse daemon and compatibility surfaces such as catalog, operations, and player settings.
- `gamecult.aetheria.daemon_provider_advertisement.v1` is the daemon-owned Aetheria runtime contract for authoritative frames, SoA views, game/editor GUI surfaces, game/editor TUI surfaces, health, and command boundaries.

The bridge layer must point at daemon-owned witnesses instead of becoming a second source of truth. Long term, Odin should discover the daemon-owned provider advertisement and interface bindings directly; the older state-host card should become a compatibility adapter or disappear.

## Queues

Queues are an implementation detail. Eve commands and daemon commands are typed `gamecult.eve.command.v1` and `gamecult.aetheria.daemon_command.v1` records in the Aetheria state graph; the old `.pending` runtime commit outbox is gone. None of those folders are the public API, daemon identity, or a substitute for CultNet/CultMesh typed records. Anything still named `Queue*`, `CommandLog`, `Inbox`, `mailbox`, `.eve.commands`, `.daemon.commands`, `.cc.pending`, or `PendingCultCacheStore` in Unity-facing daemon/Eve command code should lower to typed command documents and be demoted behind typed operation methods as the CultNet command boundaries come online.

`AetheriaRuntimeVerseClient` is the only shared submission boundary for typed command records. Client-side command lowerers may use typed runtime clients over that same Verse client. Do not add command ports, cached submitters, mailboxes, or queue-like buses between clients and the typed Verse graph. They must not drain those records or mutate state in response; ownership stays with the daemon Verse member.

Migration rule: do not expose string command names and payload maps as public Aetheria APIs. Compatibility adapters may decode external Eve requests at the edge, but persisted Aetheria command documents and Unity gameplay calls should use typed command bodies and typed operation methods.

Daemon-published game surface buttons select typed daemon command documents by command id (`aetheria.daemon.commands.*`). They must not smuggle daemon command fields through generic Eve payload maps such as `commandKind`, `weaponGroup`, or `scalarValue`; richer commands need explicit typed body builders.

Unity menu handlers should not construct Aetheria command bodies by spelunking `EveSurfaceCommandRequest.Payload`. Pass the request to the shared typed command client at the Eve edge, then submit the resulting `gamecult.eve.command.v1` or `gamecult.aetheria.daemon_command.v1` document.

## Cut Line

If a status/control surface matters to operators or agents, publish it as typed CultCache state or a `.cc` witness plus an Eve/CultUI CultMesh surface. Do not add a private HTTP dashboard, Unity-only inspector, JSON status blob, or agent-specific daemon wrapper as canonical truth.

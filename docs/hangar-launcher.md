# Hangar Launcher

Status: the shared Hangar, deployment admission, and portable Hangar surface
contracts exist. Mode session bootstrap still has to consume accepted deployment
receipts; the old generic New Game path remains live until that cut is complete.
The state assembly and viewport queries compile against CultMath `0.1.2`, whose
canonical `rect` stores normalized `min`/`max` bounds.

The Hangar is the launcher and durable preparation space for Terminus,
Starbridge, and Arena. Its composition borrows the useful information geometry
of the early MechWarrior Online MechLab without copying its visual identity.

```text
+-----------------------------------------------------------------------+
| HANGAR                         TERMINUS | STARBRIDGE | ARENA | LAUNCH  |
+----------------------+-----------------------------+------------------+
| selected ship        |                             | fit summary      |
| hull / status        |     authored ship preview   | weapons/support  |
| readiness            |                             | mass/heat/power  |
| deployment policy    |                             | EDIT LOADOUT     |
+----------------------+-----------------------------+------------------+
| OWNED SHIPS: [bay] [bay] [bay] ...                                    |
+-----------------------------------------------------------------------+
```

The ship-bay strip selects one durable Hangar ship. Mode selection chooses one
typed authority/admission policy. `Launch` submits the selected ship, loadout
template, mode, policy id, and expected Hangar revision as one deployment
request. The Hangar owner validates and commits the immutable deployment before
any mode session may instantiate live state.

`Edit Loadout` opens the existing spatial inventory/refit interaction shape.
The left/source inventory behaves like the trade menu's stock or cargo source;
the ship equipment and cargo grids are the target. Drag, rotate, equip, remove,
buy, sell, and transfer remain typed refit/trade operations. The Hangar screen
does not own a second fitting model.

## Authority Map

Owner: the canonical `gamecult.aetheria.hangar.v1` document owns durable ships,
inventory, currencies, unlocks, template references, revision, and accepted
deployment receipts.

Inputs: authenticated player identity, a typed deployment request, the expected
Hangar revision, one owned ship, one owned loadout template, and the exact mode
policy id.

Outputs: one immutable accepted deployment receipt embedded atomically in the
new Hangar revision, or a typed rejection that does not mutate the Hangar.

Derived state: selected bay, selected mode, preview, fit metrics, affordability,
compatibility warnings, and launch readiness are UI projections. The preview
and metrics never become equipment or deployment owners.

Forbidden writers: Unity, Electron, Eve lowerers, mode session checkpoints,
witness seeders, and mode-local inventory stores cannot mutate Hangar assets or
mint accepted deployments. A mode cannot launch from an uncommitted UI
selection.

Shared paths: rendered clients, headless controllers, tests, reconnect, and AI
orchestration all submit the same deployment request and consume the same
receipt. Terminus, Starbridge, and Arena differ by policy and session owner, not
by Hangar schema.

Cut line: replace `MainMenuNewGame -> AetheriaDaemonRunFactory.WriteAsync` with
`Hangar launch operation -> deployment admission -> mode session bootstrap`.
Delete generic New Game as a gameplay-state writer after all three mode
bootstraps consume deployment receipts.

## Verification

- duplicate request id returns the original accepted receipt;
- stale Hangar revision, wrong player, unavailable ship, unowned template,
  mismatched hull, unknown mode, or wrong mode policy cannot mutate Hangar;
- accepted receipt contains a loadout snapshot, not a mutable reference to the
  saved template;
- all three modes pass through the same admission primitive;
- flush/reopen preserves the Hangar revision, ship deployment state, and
  receipts;
- the portable surface exposes owned bays, ship preview, fit summary, existing
  inventory/refit entry, three mode selectors, and Launch;
- attaching a renderer does not change admission output.

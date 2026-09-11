# Three Gates: Shipping Scope

Date: 2026-09-11

Aetheria ships as a short rogue-lite run on the existing Unity game: start at
the Entrance, fight through three boss chokepoints, reach the Exit. Everything
the player earns comes from what they destroy. More systems come after this
ships, one at a time.

## Core loop

Fight -> loot -> sell and refit at stations -> push to the next gate.

## Work, in order

1. **Run structure.** Death deletes the saved run (`ActionGameManager.Die`
   currently leaves `PlayerSettings.SavedRun` intact). Reaching the Exit shows a
   win screen. Credits persist in the save instead of the hardcoded 15M on
   `ActionGameManager`.
2. **Bosses as gates.** `Galaxy.PlaceFactionsMain` already puts boss zones on
   Entrance-to-Exit chokepoints and `SavedGame.BossZones` stores them.
   `ZoneGenerator` spawns the faction `BossHull` with an authored loadout in its
   boss zone; wormholes out of that zone stay locked until the boss dies.
   Difficulty scales per section.
3. **Reason to fight.** Kills already drop gear and cargo
   (`EntityInstance.cs`). Add Sell and Repair to station services
   (`TradeMenu` is buy-only; no repair exists).
4. **AI fairness.** Agent steering reads the gravity slope from
   `Zone.GetHeight`. Add aim error to `Combat` state lead prediction. Scale
   loadout quality by section rather than biasing toward the largest items.
5. **Minimal narrative.** One Ink beat per boss (pre-fight hail, aftermath),
   played through the existing `LocalMenu` Ink player. Story placement
   (`StoryProcessor`, commented out at `Galaxy.cs:239`) stays off.

## Combat experiment

Before any combat redesign: prototype a lock-on/turret-only loadout, using the
existing `LockWeapon` and turret behaviors. If aiming disappears and the fight
becomes positioning in gravity wells, heat, shield/weapon energy, and target
choice, the tactical direction is cheap. If it plays too passively, we learn
that before rewriting anything.

## Out of scope for now

Economy simulation, hauling, mining yield, crafting/blueprints, reputation
changes, story placement, multiplayer and `Economy.Server`, and the
CultMesh/daemon rebuild (parked in `F:\Projects\AetheriaEve`).

# Aetheria merge checklist: `codex/fire-control-12` into `master`

Compiled 2026-09-29 by Eyes from the maps on the branch. Pointers are `doc:line` in `docs/` at the branch tip. "Unrecorded" means the maps do not say the check was done.

**What merging does.** `origin/master..codex/fire-control-12` is 257 commits. It carries the item-provenance work after 2026-09-17, stats-and-power (resolver, power bus, input capacitors, priority tiers, power-supply term and brownout, nominal requests, condition ratio, shield reserve, authored roles), fire-control Cuts 1-12.4(b), locomotion Cut 1 (restored hulls), the shield-panel interceptor, and the Addressables cuts. Self performs the git merge after you sign off. Nothing here is for the merge itself; it is what only you can check first.

**Correction to the brief.** `codex/cultcache-cutover`, `codex/aetheria-state-rebuild` and `codex/item-provenance` are all ancestors of the branch tip, and the first two are already on `origin/master`. Master's merge commit `9b85211f` (2026-09-17) took cultcache-cutover and item-provenance at their 09-17 tips, after your play smoke passed (`cultcache-migration-cut.md:148`). Master has two commits the branch lacks (`9b85211f`, `263ac421`, which deletes dead narrative fixtures). `git merge-tree` of the two showed no conflicts. No `aetheria-state-rebuild` doc exists.

## 1. Before you start

- Open branch `codex/fire-control-12` at its tip, `4b594e116c4063ef5ee1dcb937bab0288008e63e` (2026-09-26, in sync with `origin/codex/fire-control-12`).
- Unity `6000.3.24f1` (`ProjectSettings/ProjectVersion.txt`).
- Batchmode: `tasklist` on 2026-09-29 showed Unity Hub and the licensing client but no `Unity.exe`, so none was running. Batchmode refuses an open editor and the maps say to close it and not kill anything (`item-provenance-cut.md:485,519`). Recheck when you start.
- The Unity manifest pins `cultmath-unity-v0.2.4` (`Packages/manifest.json`); the tag exists in CultLib. Unity needs network access to resolve it on first open (`fire-control-cut.md:2205-2212`).
- **Delete or move `GameData/run.cc` and `GameData/player.cc` before the first launch.** Both are git-ignored and stale (2026-09-22, 91,580 and 1,964 bytes). The maps' fresh-launch checks assume neither exists (`cultcache-migration-cut.md:2745`), and an old run store throws on the first lot lookup (`item-provenance-cut.md:595-596`). The persisted shape has changed since (power tier slot, fuse slot 32).

**Your uncommitted work.** The merge happens in a separate worktree and does not touch it. `git status` lists 22 modified tracked files and 3 untracked. `git diff --stat` shows content changes in only 9; I infer the other 13 (materials, render textures) differ by line endings, from the LF/CRLF warnings.
- Content changes: `Asset Sources/Adrasteia1.blend`, `Car Paint White.mat`, `Steel.mat`, `Map Gravity Lines.mat`, `Minimap Gravity Lines.mat`, `LiberationSans SDF - Fallback.asset`, `Assets/Scenes/ARPG.unity` (+374), `Assets/Scenes/FieldShieldTest.unity` (+263), `Assets/Scripts/AetheriaInput.cs` (+40).
- Line-ending-only (inferred): the other Map/Sector `.mat` files, 8 `.renderTexture` files, `Stardust.mat`.
- Untracked: `Asset Sources/Adrasteia1_ContourRoute.blend`, `Ships.blend`, `adra1.png`.
- **Decide about three that belong to merged campaigns (my reading of the diffs):**
  - `AetheriaInput.cs` (+40) adds the `Cycle Target Item` action (key J). The branch already has that action in `Assets/Resources/Aetheria.inputactions:72,326` and reads it in `ActionGameManager.cs:412`, but not in the generated wrapper. It is the wrapper catching up to committed data. Recommend including it.
  - `FieldShieldTest.unity` (+263) adds `ShieldInterceptor`, `ShieldEnvelope`, a `Panel` with `Prototype` and `ShieldPanel`. This is the shield-panel Cut 4 rig you clicked together (`shield-panel-cut.md:840-846`). It is not on the branch. Without it the interceptor code merges with no rig.
  - `ARPG.unity` (+374) adds "Debug Info Panel" and "Debug Info Text". `ActionGameManager.DebugInfoText` (`ActionGameManager.cs:151,1287,1333`) is the fire-control debug HUD and it returns early when null. Without the scene change the HUD "hull" factor in step 10 is invisible.

## 2. One play session, in order

### 2a. Fixtures to author first

1. **Scratch catalog for 12.4.** Copy the catalog; do not edit `GameData/Aetheria.cc`. Author one weapon with `Fuse = Proximity` and a `BlastRadius`, and one with `Fuse = Delayed`, a `BlastRadius` and a penetration of about 2 cells (`fire-control-cut.md:3094,2656-2658`). Shipped penetration is at most 0.25 and enclosed cockpits need about 2 cells, so a penetrator authored from shipped values cannot reach the interior (`:3224-3225`). Fields are `aetheria.weaponitemdata` slot 29 (`BlastRadius`) and slot 32 (`Fuse`). Unrecorded: how to point the game at the scratch catalog, and which tool you author with (the map says only "Studio or `AetherDb`", `:2665`).
2. **Studio open, no game running.** You need it at 2c and 2d. Batchmode-based checks are the agent's, not yours.

### 2b. Fresh launch (no `run.cc`, no `player.cc`)

3. **Launch to main menu.** Expect no Addressables errors, and the Groups window shows `Assets/Content` under `Default Local Group` (`addressables-cut.md:301`). `player.cc` appears and Continue is disabled (`cultcache-migration-cut.md:2746`). Proves: Addressables Cut 1, CultCache cutover. The 09-17 smoke covered the last two on the old tree; this is a cheap recheck on the new schema.
4. **New Game (tutorial).** Expect the galaxy generates and the start ship is the Longinus (`settings-globals-cut.md:498`, an expectation, not a recorded check for this stack). Expect a Zenith station with a docking bay in the entrance zone every time (`locomotion-cut.md:375`). Proves: locomotion Cut 1 station rule.
5. **Addressables, "Use Asset Database".** Ships, turret and Zenith spawn; thruster and aether-drive particles show; fire an instant weapon (Autocannon) and a charged one (ChargeBlast SG); action bar icons show (`addressables-cut.md:362-366`). Proves: Addressables Cut 2.

### 2c. Flight, power and economy

6. **Provenance in play.** Tier colour shows only after settings-globals Cut 1 (this expectation was falsified by the 2026-09-17 smoke, section 8); the properties panel shows Manufacturer, product name and flavour for branded gear. Dock and buy: the brand still shows in cargo. `give Lamp` (any design name): no Manufacturer row. Kill an NPC and pick up loot: brand shown (`item-provenance-cut.md:528-539`). Proves: item-provenance Cut C.
7. **Fly a restored hull (Longinus or Djinni).** Thrusters move and turn it (`locomotion-cut.md:501`). Watch the speed cap: `VelocityLimit` is gated by the hull's `Active`, and a hull taken offline with thrusters firing loses the cap (`:377`). It did not happen in 300 s on shipped settings; report if you see runaway speed. Unrecorded: how to spawn a restored hull in play (the map names no path).
8. **Weak reactor (O2).** Fly with a reactor too small for the loadout. Expect visible, legible starvation rather than a cooked reactor (`stats-and-power-cut.md:705-706`). Proves: power bus (map Cut 3).
9. **Brownout and refill (O6, O4).** Brownout reads as degradation, not breakage (`:774`, `:990`). A weapon refilling under brownout stutters instead of firing full rate and cooking the ship (`:729-730`). Proves: power-supply term, brownout, input capacitors.
10. **Memory across zones (O1).** Play across several zones with kills, open the Profiler or Task Manager, and confirm memory does not climb (`:672-673`, `:980-982`). Proves: stat resolver, the entity-leak fix (map Cut 2). Combine with steps 11 and 12.

### 2d. Combat (against a Longinus-class AI), then save and Studio

11. **Fire-control Cut 1 arcs.** Side-mounted weapons still fire at targets abeam; a turret tracks all the way round; nothing fires through the hull (`fire-control-cut.md:397-398`).
12. **Fire-control Cut 3 and shots.** Shots show rolled impacts and misses; a miss reads as a near-miss, not a bug; damage matches the HUD; aiming at a revealed subsystem concentrates damage; kill time is in the same order as before (`:659-661`). Reveal and selection work, kills drop loot, pickup stores it, the tractor beam still pulls (`headless-playground-cut.md:789-794`). No crash or frozen hot ship when the target vanishes (the 2026-09-20 failure, `fire-control-cut.md:1464-1470`). Recheck after the fixes: unrecorded.
13. **12.2, bow then beam.** The HUD `hull` factor reads higher from the beam. Turn your armoured face into a slow missile and watch where it lands (`:2380-2383`).
14. **12.3, launcher into a bow, then a flank.** Use a GT 3K or pswarm launcher. The schematic display pulses the facing edge only (`:2519-2520`).
15. **12.4, scratch catalog.** Watch a proximity airburst beside a Longinus and a delayed penetrator into its nose. Damage must land where the model shows the blast; this confirms the centre-of-mass anchor. Bow is already confirmed +y (`:3095-3097`, `:2028-2031`). Proves: 12.4(b) `Detonate`.
16. **Wormhole, save, Continue.** Take a wormhole, quit, Continue: same items, same tier and brand (`item-provenance-cut.md:536-537`).
17. **Die.** Continue is disabled and `run.cc` holds no ledger (`:540-541`).
18. **Shield panel, `FieldShieldTest`.** Follow the numbered look in `shield-panel-cut.md:683-700`. Nothing visible on load. Click the nose: a patch grows and the field ripple runs too. Click the flank: the panel lies flat, not tilted. Click the same spot twice: it strikes the same panel. Spam far-apart clicks: at most 12 panels, oldest replaced. Let them fade: none left. Then the Cut 5 payoff: strong hits dice the patch, and repeated hits on one spot break through on a later hit (`:737-739`). Proves: shield-panel Cuts 3, 4, 5.
19. **Studio click-through.**
    - Open `Aetheria.cc`. The fire-control behaviour union renders (`fire-control-cut.md:503-506`).
    - `Longinus` schematic still underlays the shape drawer (`addressables-cut.md:370-371`).
    - The restored hull records open (`locomotion-cut.md:501`; the map gives no checklist beyond "the Studio click-through").
    - The scratch catalog's new `Fuse` and `BlastRadius` fields edit (from 2a; no map line).
    - Open `GameData/run.cc` after step 16. `aetheria.provenanceledger` renders `Lots` with `Attributed` origins and per-role lists. If Studio cannot draw an int-keyed dictionary of unions, that is a Studio defect to raise, not a schema change (`item-provenance-cut.md:541-544`).
20. **Addressables, "Use Existing Build".** Run *Build > New Build > Default Build Script*, then repeat step 5. This proves the player catalog carries GUID keys. Then *Analyze*, "Check Resources to Addressable Duplicate Dependencies", and record the result; 5 known leftovers are expected (`addressables-cut.md:367-370`).

### 2e. Desk review (not play)

21. **Role content (O7).** Review `docs/stats-power-cut7-roles.md`. Rows marked "generic, needs review" are arbitrary-seed, not brand identity, and no design yet has a second product with a raised role (`:156`, `:194-202`; `stats-and-power-cut.md:797-799`, `:991`).

## 3. Feel calls during the session

Only what the maps name.
- **Balance numbers.** `CommitHorizon` (first value 0.3 s), `SchematicCellSize` (0.35) and the targeting stat ranges are first guesses; the smoke is the arbiter (`fire-control-cut.md:664-665`, `:855-859`). `UnaidedAccuracy` is ruled "really, really bad" (`:253-259`); judge whether it reads as a last resort.
- **Arc rule (Q2).** Arcs apply to the player as well as the AI. The map says it is a real difficulty change to be felt in the smoke (`:836-841`).
- **AI holds fire more** once `pOnHull` prices shots. The map calls that tuning, not broken AI (`:1396-1400`).
- **Penetration retune (F12-2)** is a ruled follow-up, "Retuning penetration is a follow up" (`:3223-3229`). The map does not tie it to a smoke. The only link is 2a step 1: shipped penetrators cannot reach interiors, so step 15 needs the scratch value.
- **Locomotion Q6, yaw weight (w = 3 recommended).** The map says settle it in the Cut 1 play check (`locomotion-cut.md:372-373`, `:903-904`), but the weight belongs to Cut 3's solver, which has not landed. There is nothing to feel today. Ambiguity, not a merge gate.

## 4. Already done: do not redo

- Play smoke for cultcache-cutover plus item-provenance, 2026-09-17: new game, combat, loot, save and Continue with brand and quality intact, death clears the run, stance toggle, `FieldShieldTest` (`cultcache-migration-cut.md:148-153`). It ran on the old tree, so steps 3, 6, 16 and 17 are rechecks, not repeats of a gap.
- Studio click-through for the CultCache port, 2026-09-17: AmmoType refs repointed at `515859cf`, `dangling` at 0, schematic underlay restored and checked (`cultcache-migration-cut.md:161-165`).
- Bow is +y on the schematic, confirmed 2026-09-22 from the schematic images (`fire-control-cut.md:2028-2029`). The blast-anchor half is still open (step 15).
- Shield-panel play evidence, 2026-09-18, rigged scene: a panel spawns, is reused and spiderwebs; it shatters only near the rim (`shield-panel-cut.md:119-124`). Partial: the Cut 5 fixes landed after, so step 18 is still open.
- A play smoke on 2026-09-20 found the frozen-ship, no-hits and `LockWeapon` crash (`fire-control-cut.md:1464-1470`). That was a failure report, not a pass. Cuts 8 and 9 answered it and no recheck is recorded.
- Stats-and-power Cuts 0-1 landed with Soul verification and your re-author ruling (`stats-and-power-cut.md:79-99` onward). No operator check remains from them.

## 5. Not part of this merge

- Stats-and-power tier control in the schematic UI (O5) and the deletion of `IOrderedBehavior`/`SortPosition` (O3): no `PowerTier` under `Assets/Scripts/UI` at the tip, and `SortPosition`/`IOrderedBehavior` still exist in `Reactor.cs`, `Entity.cs` (my grep). `stats-and-power-cut.md:752-753`, `:985-989`.
- Stats-and-power UI reconciliation (O8: properties panel, trade menu, schematic HUD, Studio over a stat with terms): the map's Cut 8 has not landed; the branch's "Cut 8" commits are the condition ratio (`:819`, `:991-992`).
- Settings-into-globals: only Cut 0 (delete 5 dead assets, `ea8b420e`) landed. Its play smoke and Studio click-through wait for Cut 1 (`settings-globals-cut.md:498-511`). The map header still says nothing landed.
- Build delivery: the two commits are the map and a ruling (`build-delivery-cut.md:5`, operator checks `:252,324,360,404`).
- Locomotion Cuts 2-5, including Q6 and Q4 (`locomotion-cut.md:360,905`).
- `content-batch-one.md` is a spec that writes no catalog data (`:5-8`).
- Fire-control follow-ups F12-2 to F12-8 (`fire-control-cut.md:3223-3250`).

## 6. Open questions still yours

- **Q12-5 (not ruled):** should the HUD show which target items are exposed from the current bearing? Recommended as a follow-up after 12.3, on the target-item cycling UI (`fire-control-cut.md:1959`, `:3215-3218`).
- **Locomotion Q4:** heading versus aim; blocks Cut 5 only (`locomotion-cut.md:905-913`, `:372`).
- **Locomotion Q6:** the yaw weight; open, does not block (`:372-373`).
- **Defaults Self took in 12.4, which you may overrule** (`fire-control-cut.md:3208-3213`): a blast ignores `DamageSpread` and P sits on the centre lane; `IncomingHit` fires for the host of a contact or delayed hit only; the delayed fuse point clamps to the last reached cell's exit; `Detonate` runs a flat armour, pool, hull pass.
- **Stale headers, not open questions:** the fire-control 12.3 "pending ruling" at `:2581` was ruled at `:2588`; Q12-9 is ruled A (`:3171`); the shield-panel Q1-Q7 block (`shield-panel-cut.md:61`, `:800-`) was not updated after Cuts 1-5 landed, Q5 being ruled C (`:839-846`).

## 7. What happens after you sign off

- **Self merges `codex/fire-control-12` to `master` with `--no-ff`**, in a separate worktree. That single merge carries item-provenance, the stats-and-power work, fire control and locomotion Cut 1. The cultcache-cutover and item-provenance merge into master already happened on 2026-09-17 (`9b85211f`, `cultcache-migration-cut.md:148`), so the maps' "merge the two together" instruction (`item-provenance-cut.md:82`) is spent. The tree merges clean today.
- Any of section 1's three uncommitted files you want included need to be committed to the branch first, or they stay out.
- **Soul gates outstanding:**
  - Fire-control 12.4 Soul passes: running tonight. The 12.4 section carries no "closed" status yet (`fire-control-cut.md:2602`). 12.0-12.3 are closed (`:2168-2210`, `:2253`, `:2385`, `:2522`).
  - CultMath bounded least squares is locomotion Cut 2, not in this stack (`locomotion-cut.md:508`, `:360`). It gates nothing here.
  - Unrecorded: a Unity batchmode compile at the tip `4b594e11`. 12.2 and 12.3 record clean compiles at their own heads.
- **Soul status at merge time: Self fills in.**

## 8. Results: play smoke, 2026-09-30 (operator plays, agent records)

Tree: `codex/fire-control-12` at `113164fa` plus the fixes listed below. `run.cc` and `player.cc` moved to
`GameData/stale-2026-09-22/` before launch.

**Compile.** The Unity 6000.3.24f1 batchmode compile at `113164fa` was clean (exit 0, no `error CS`). The four
never-compiled files needed no fix. One warning, `EntityInstance._destroyed` assigned but never read, is legacy
(`729ab0e8`).

**Changes made during the smoke.**
- `e0a80208`: `AetheriaInput.cs` regenerated; Cycle Target Item bound through `Input.Player.CycleTargetItem`,
  retiring the by-name `FindAction` stopgap. Batchmode recompile clean.
- `97edf48c`: `ARPG.unity` Debug Info Panel and `FieldShieldTest.unity` shield-panel Cut 4 rig committed.
- Console: control characters from text input are no longer appended, and Backspace deletes (see step 6 finding).
- `give` (operator ruling 2026-09-30): an unknown name and a full cargo bay now say so instead of doing nothing. A
  ship hull is refused unless docked; when docked it goes through `ActionGameManager.CommissionShip`, the same
  primitive `TradeMenu.Buy` now uses, and moors a bare player ship at the docked entity with no docking bay
  ("mothballed ships do not require docking bays"). Non-ship hulls are refused. This is the step 7 path to a
  restored hull.
- Scratch weapons authored in place in `GameData/Aetheria.cc` (not committed; restore with
  `git checkout -- GameData/Aetheria.cc`): `Smoke Proximity` (Proximity, blast 8), `Smoke Delayed` (Delayed,
  blast 4, penetration 2 cells), `Smoke Refused` (Proximity, blast 30, range 20). Each is a Spectra clone, energy,
  1 column by 2 rows, to fit the test ship's only energy hardpoint.

| Step | Result | Evidence |
|---|---|---|
| 3 Launch to main menu | pass | Operator reached play through step 6 with no reported Addressables errors. |
| 4 New Game | pass | As above. |
| 5 Addressables, Use Asset Database | partial | Charged weapon (ChargeBlast SG) unprovable: the test ship's only energy hardpoint is 1x2 and ChargeBlast SG is 2x2. Instant weapon not separately reported. |
| 6 Provenance in play | partial | Properties panel correct for starting gear and for a purchased item. **Tier colours missing everywhere**: known defect, `settings-globals-cut.md:150` (Unity-serialised `RarityTier.Color` loads as zero); fixed by settings-globals Cut 1, not part of this merge. `give Lamp` was first refused (console finding), then silently did nothing: `Lamp` is a test-fixture name, absent from the live catalog, and `give` reported no miss (now it does). No-Manufacturer check still open with a real unbranded design. Loot brand not yet reported. |

**Finding, console (not fire control).** Every console command was refused with "commands take only letters,
digits, spaces and hyphens", and Backspace inserted a glyph. Cause: `Keyboard.onTextInput` delivers control
characters (`\b`, `\r`), and `ConsoleView` appended them to the input line. Enter's `\r` rode into every command;
`d503f076` (2026-09-14, on master) correctly changed the parser from stripping disallowed characters to refusing
them, which exposed it. Backspace had never been handled (only Delete), which is legacy (`7006a6b0`).

**Feel notes.** Engaging an AI Djinni: the player was destroyed decisively. "Game is hard."

**Finding, ship purchase and docking bays (design gap, not a merge gate).** The intended UX is that a purchased
ship is assigned its own docking bay. The code does not do that:
- `TradeMenu.Buy` (`TradeMenu.cs:373-381`) creates a bare `Ship` parented to the docked station and assigns it no
  bay. It also charges `data.Price` rather than the lot's quality price that `GetPrice` computes for other items.
- `LoadoutGenerator` equips exactly one docking bay per station (`LoadoutGenerator.cs:84-92`), so Zenith has one.
- The inventory panel's Current button (`InventoryPanel.cs:110-120`) sets the new ship as current and overwrites
  the single bay's `DockedShip`; the previous ship stays a station child with no bay.

**Smoke stopped after step 6 (operator, 2026-09-30).** The operator rejected the script as a monolithic play order
that does not respect how the game works: step 7 alone meant spawning and fitting a whole Djinni by catalog name.
Direction: operator checks become a list of behaviours verified over time, each set up by an authored, retained
scenario selectable from the main menu (a Eureka cut). Steps 7-20 and handoff checks A-F are unplayed. The operator
prefers to merge now, with those as open entries to verify through scenarios; Self rules on the merge.

**Stale expectations found in this checklist.** Step 6 transcribes `item-provenance-cut.md:529-537` (written
2026-09-17 03:38, `7425ff1b`). "Starting-ship items show tier colour" was falsified by that day's smoke and recorded
at `settings-globals-cut.md:150` (`db420981`, 17:36); the provenance map was never corrected and this checklist
copied it despite its own section 5. "`give Lamp`" names a test fixture (`LoadoutTests.cs:289-312`), never a live
catalog design, and `give` could not have run at all between `d503f076` (09-14) and `f935c29b`.

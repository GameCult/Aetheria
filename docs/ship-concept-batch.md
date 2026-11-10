# Ship concept batch: brief

**Value:** a reproducible way to turn the lore's faction looks and the hull data's size limits into a few dozen Klein concept images, sifted by human votes.
**Entry points:** the mill's file contract (section 1), the Raven call (section 2), the prompt template (section 4), batch 1 (section 6), the sift plan (section 7).
**Status:** a plan. No image has been generated, Raven was not called, and `raven-image` was not yet installed on Yggdrasil when this was written (root install pending, `gamecult-ops` 3fdcf02).

All images this batch makes are **concept art only until a human has reviewed them** (section 8). Nothing here is canon: the lore pages it draws on are labelled brainstorming (`AetheriaLore` `Brainstorming/Ship Design Language.md`).

Sources: AetheriaLore `main` 14d827ca (`scripts/concept-mill/`, `Aetheria/media/concept-mill/`, `Aetheria/Brainstorming/`), Aetheria `master` c33feebde (`docs/content-batch-one.md`, `Assets/Scripts/ServerShared/Enums.cs`), gamecult-ops `tools/raven-image/README.md` (3fdcf02).

## 1. What the existing Concept Mill emits

The mill is in AetheriaLore, not Aetheria. It is a build script plus a hand-edited data file; it never calls an image model. Renders come from outside (so far the BFL `flux-3-image` API, operated by hand) and are ingested.

**Input, per render** (not in the repo; default root `Aetheria/Brainstorming/Renders/`, `--renders <dir>` overrides): `<source>.png` and `<source>.md`. The `.md` is the render record:

```
# <Title>
## Hypothesis          what this render tests
## What changed        delta from the previous round
## Agent verdict       (rounds 4-5 call it "Agent observation")
## Render record       Model, Parameters, Request id, Date, Composition (bullets)
## Prompt              the full composed prompt, blocks separated by blank lines
```

**Source of truth:** `scripts/concept-mill/ships.yaml`, hand-edited. `build.mjs` validates it and fails on a duplicate ship id, a duplicate variant id inside a ship, an unknown `status.kind`, a ship with no variants, more than one `pick`/`default` per ship, or a missing derivative file.

Per **ship** (the unit the page and the vote use):

| Field | Meaning |
|---|---|
| `id` | slug, unique; the URL hash `#<ship>/<variant>` and the vote key |
| `name`, `code`, `faction`, `role`, `desig` | display; `desig` is e.g. `Bench 1 · entry B · safe bet` |
| `crew`, `mass` | strings (`mass` is `Small (per prompt)` or `TBD`) |
| `hardpoints` | rows `[qty, size, mount-text]` (size: `Small`, `Medium`, `Utility`, `Sensor`, `Built-in`) |
| `loadout` | list of strings |
| `concept`, `notes`, `beaten` | readout prose: what it is for, design note, what counters it |
| `status` | `{kind: pass\|new\|scrap, stamp, verdict}`; `stamp`/`verdict` carry only the operator's word, or say she has not given one. `kind` places the ship in the menu: Kept / Unreviewed / Scrap bin |

Per **variant** (one render): `id`, `round` (display label), `image` (derivative basename), `prompt` (record basename, defaults to `image`), optional `source` (render basename to ingest from), `hypothesis`, `verdict`, `operator` (verbatim operator verdict), `pick: true` or `default: true` (at most one per ship). Hypothesis and verdict default to the record's `## Hypothesis` and `## Agent verdict` sections.

**Output, per variant** (committed, in `Aetheria/media/concept-mill/`):

- `<image>.webp`: PNG resized to 1600 px wide, quality 82
- `<image>-thumb.webp`: 720 px wide, quality 72
- `prompts/<prompt>.txt`: a byte copy of the render's `.md` record

**Output, manifest:** `site/quartz/components/data/conceptMill.json`: `{sections: [{kind, title}], votes: {enabled, endpoint, bifrostUrl}, ships: [...]}`. Each ship is its `ships.yaml` row with `crew` stringified, `hardpoints` as `{qty, size, mount}` objects, and `variants: [{id, round, image, thumb, prompt, hypothesis, verdict, operator, pick}]` where the three paths are URLs under `/media/concept-mill/`; plus `defaultVariant` (the pick/default variant, else the last). Ships are ordered by section, then faction in first-seen order, then file order. Today: 27 ships, 48 variants.

**Provenance the mill keeps today:** whatever the `.md` record holds, shown as a downloadable text file. It has no machine-readable provenance fields, no seed, no hash, no licence or review state; the BFL endpoint offered no seed. Section 5 adds them.

**Vote slot (what Heimdall's Bifrost page meets):** hidden until `votes.enabled` and `votes.endpoint` are set in `ships.yaml`. The page does `GET <endpoint>` (no credentials) and expects JSON `{ "<ship id>": {up, down, members?: {up, down}} }`, and links `<bifrostUrl>/<ship id>` ("Vote on Bifrost"). **Votes are per ship, not per variant.** A sift that must rank several seeds of one concept needs either one ship row per seed or a variant-keyed tally; the second is a change to `aetheriaConceptMill.inline.ts`. This batch uses the first (section 5), so no mill change is needed.

## 2. What the Raven client needs

`raven-image` (gamecult-ops `tools/raven-image`, one stdlib Python file plus `graphs/flux3-klein.json`):

- **Where it runs:** Yggdrasil only. The service is `http://10.77.0.4:8190` (`RAVEN_URL` overrides). Install is root's (`/opt/commons/opt/raven-image/`, symlink in `/opt/commons/bin/`); until then a caller can run the file from a checkout of 3fdcf02.
- **Call:** `raven-image "<prompt>" --size 1536x1024 --seed <n> [--no-enhance] -o <path>.png`. Prints one line per image: `path seed size seconds`. `-n` makes several images (`out-1.png`, ...); this batch passes explicit seeds, so one call per image.
- **Exit codes:** 0 ok; **75** Raven busy or off (no retry; the caller decides when to ask again); 1 for a rejected graph, failed render or timeout (900 s wait per image).
- **Cost:** about 45 s warm, 81 s cold, per 1536x1024 image (README check run). Raven is Deru's desktop: small batches, one client at a time.
- **What it leaves to the caller:** it prints the seed and, per the brief, the prompt; it writes only the PNG. It writes **no sidecar**, so the batch driver must write the record (section 5). The upsampler (on by default) rewrites the prompt; the record must keep both what we sent and what the printed prompt says Klein received, or use `--no-enhance`.
- **Driver needs:** a list of `(concept id, seed, size, prompt file)`; skip-if-PNG-exists so exit 75 resumes cleanly; stop the whole run on the first 75.

## 3. Proposal constraints from lore and hull data

**Hull classes.** `HullType` has only `Ship`, `Station`, `Turret` (`Enums.cs:139`). Lore and code define no size class for ships. The labels Light, Medium and Heavy below are **this batch's**, from the shipped hulls' schematic grids (cells, 2.0 m per cell, `hull_grid.py:11`; maximum 32 cells a side):

| Batch class | Grid (cells, width x length) | Hull precedent | Approx. size |
|---|---|---|---|
| Light | up to 8 x 18 | Longinus 6x17, Turret 8x8 | 12-16 m wide, up to ~36 m long |
| Medium | 10-12 x 12-19 | Smoke Test Hull 10x19, Zenith 12x12 | 20-24 m wide |
| Heavy | 7 x 16 at 4200 mass, or 14+ wide | Noka MKI spec 7x16 (`content-batch-one.md` section 2), Djinni 14x17 | 28 m+ |

The shipped hull grids are from `GameData/Aetheria.cc` as decoded in `/opt/commons/handoff/aetheria-ship-brief-for-brokkr-20261110.md`. No metre size, crew count or calibre is canon; prompts must not state metres.

**Hardpoints.** Types: Energy, Ballistic, Launcher, Sensors, Thruster, Radiator, Shield, Reactor, ControlModule (+Hull, Tool, Thermal, WarpDrive). Only weapons (Energy, Ballistic, Launcher), Thruster and Radiator have a visible mount on the model; Reactor, Shield, Sensors, ControlModule are internal (`ShipAuthoring.cs`). Spec precedent for a heavy: 2 Ballistic 2x2, 1 Launcher 1x3, 1 Sensors 1x1 visible-ish, 2 Thruster, 2 Radiator, 2 Shield. For concept prompts: **at most four counted features, each with a location** (Faction Ship Concept Prompts guideline 4). Thrusters and radiators are left out of prompts: the operator places them (Ship Design Language).

**Silhouettes** are the faction blocks in `Ship Design Language.md` (twelve are used here) and, where later rounds shortened them to livery and register, the `r8` records in `Aetheria/media/concept-mill/prompts/`. Rules the prompts keep: one analogy that places the parts; thick, closed, mirror-symmetric; matte; no wings, fins, wheels or tracks (vacuum hull); no real people, no political symbols.

## 4. Prompt templates

Composition follows the mill's latest practice (records `oboz-r8f`, global block **G3**): blocks separated by a blank line, in this order:

1. **Ship prompt** (below), most specific, first.
2. **Faction livery block**: the faction's block from `Ship Design Language.md`, cut to palette, finish and markings (the r8 Zhestokost block is the model).
3. **Global block G3**: the text in `prompts/oboz-r8f.txt` (third paragraph), copied unchanged. It is not restated here; copying it into this doc would make a second copy.
4. **Frame clause**, verbatim: *One complete ship, centered with clear margin around it, three-quarter front view from slightly above showing the dorsal surface and one side; moderate lens; plain seamless light gray background; soft key light with gentle form shadows; matte finishes throughout.*

**Ship prompt template:**

> Design the {name}, a {crew}-crew {faction} {role}. Its whole body follows {analogy}: one {mass-adjective} cast body, mirror-symmetric along its long axis. {feature 1 with location}. {feature 2 with location}. {feature 3 with location}. The cockpit is {cockpit form and location}. {class cue: proportion only, never metres}.

Slots are filled from the batch table (section 6): `analogy` is the "hook" column, features come from the "visible" column, class cue is one of: *light: long and narrow, about three times as long as wide*; *medium: compact and square-shouldered, about one and a half times as long as wide*; *heavy: broad and low, a little over twice as long as wide* (7x16 is 2.3:1). Names are working titles; the mill's `name` is set when a concept is kept.

**Variation axes** (one varied per sibling, never several): analogy (safe bet vs moonshot, as the Ship Prompt Bench labels them); feature placement; class cue. Mirror the bench's `Safe bet` / `Moonshot` label in `desig`.

## 5. Seed policy and record

- **Seeds are derived, not picked:** `seed(concept, k) = first 4 bytes (big-endian) of sha256("<concept-id>:<k>")`, for `k = 0..3`. Nobody chooses a lucky seed; anyone can regenerate. Raven's random default is never used.
- **One mill ship row per rendered seed:** ship id `<concept-id>-s<k>`, so Bifrost's per-ship tally ranks seeds with no mill change. (A later page change can fold them into variants of one ship.)
- **A seed does not carry across sizes.** The same seed at a different resolution makes a different image. So a hi-res re-render of a sifted winner is a **new image** with `parent: <low-res id>`, not a larger copy of it.
- **Upsampler off for the sift** (`--no-enhance`): the composed prompts are already full captions (about 450-550 words), and with it off the image is a function of (prompt, seed, size, graph). Not yet probed: whether Klein's text encoder truncates a prompt this long; the first render pair must answer it before the batch runs (cost: two images).
- **Record per image** (one `.md` sidecar in the mill's existing record format, so `build.mjs` ingests it unchanged), with the **Render record** bullets:

```
- Model: FLUX.3 Klein distilled (ComfyUI graph flux3-klein.json, sha256 <hash>), via raven-image <gamecult-ops commit>
- Parameters: size 1536x1024 | 768x512, seed <n>, steps/cfg/sampler as in the graph (16, 1, euler/simple), upsampler off|on
- Seed derivation: sha256("<concept-id>:<k>")
- Date: <UTC>; made by <Mind> run <run id>
- Parent: <image id or none>
- Composition: ship prompt, faction livery block (<source file@commit>), global block G3 (<source>), frame clause
- Upsampled prompt as printed by the client: <text, only when the upsampler was on>
- Review: UNREVIEWED concept-only; reviewer and date when a human has looked
- Licence: FLUX Kommunity License v.1.0; outputs are concept art until the legal root rules (gamecult-ops tools/raven-image/README.md); no training on outputs
```

  `status.stamp` in `ships.yaml` stays `UNREVIEWED` and `verdict` says the operator has not given one, per the file header.

## 6. Batch 1: 36 concepts

Twelve factions (Zhestokost, Alakrita, Aeronautics Unlimited, Finch Cybernetics, Rossum & Douglas, NiteLife Energy, Lightsail Express, Cetacean Navigators, Cryonix, Sol Dominion, Adrasteia, Death Monkey Explosives), three classes each. Chosen because each has a full block in `Ship Design Language.md`. Roles are **proposals** drawn from the mill's existing roster and the faction blocks, not from Faction Play (not read for this pass); the Noka MKI row is the content-batch spec's hull. `id` is `<slug>-<class letter>`; the ship id of a render is `<id>-s<k>`.

| id | Faction | Class | Role | Anatomy hook | Visible features (location) |
|---|---|---|---|---|---|
| zh-l | Zhestokost | L | column picket | cast-iron stove, narrowed | 1 short gun in a box turret (bow); 1 sensor block (dorsal); magazine hatch (flank) |
| zh-m | Zhestokost | M | line gunship | forklift | 2 box turrets (shoulders); magazine hatches (dorsal rows); tow lug (stern) |
| zh-h | Zhestokost | H | Noka MKI heavy | armored fist, one centerline cannon | 1 centerline cannon (bow); 2 smaller guns (shoulders); 1 launcher bank (dorsal) |
| alk-l | Alakrita | L | duel interceptor | racing scull | 2 lasers (hood edges); crease lines (full length) |
| alk-m | Alakrita | M | raid cutter | closed folding fan | 2 forward guns; fin stack (stern); gold inlay |
| alk-h | Alakrita | H | patron's heavy escort | folded fan, broadened | 3 guns (prow row); laurel plaque (cockpit) |
| au-l | Aeronautics Unlimited | L | swarm skirmisher | bison hump | rocket pod (hump face); gun (forward); sensor dome (dorsal) |
| au-m | Aeronautics Unlimited | M | tool gunboat | compact excavator | tool socket (bow); 2 guns (shoulders); module bays (flank) |
| au-h | Aeronautics Unlimited | H | frontier monitor | pack mule | 2 module bays; launcher (dorsal); survey mast |
| fin-l | Finch Cybernetics | L | tracker | bird skull | ear dish (flank); lens row (brow); 1 gun (chin) |
| fin-m | Finch Cybernetics | M | relay picket | diving bird, folded | 2 dishes (flanks); 2 guns (shoulders) |
| fin-h | Finch Cybernetics | H | sensor cruiser | prosthetic joint | 3 dishes; 1 gun battery (bow) |
| rd-l | Rossum & Douglas | L | wedge interceptor | flying wedge | 2 guns (nose); flush hatches (dorsal) |
| rd-m | Rossum & Douglas | M | MIRV picket | wedge, thickened | missile hatches (dorsal plane x4); sensor (nose) |
| rd-h | Rossum & Douglas | H | missile barge | wedge, widest | missile hatches (x4); 2 guns (shoulders) |
| nl-l | NiteLife Energy | L | station guard | espresso machine | cartridge (flank); 1 gun (bow); release catch (cartridge) |
| nl-m | NiteLife Energy | M | cartridge gunship | smart speaker | cartridge (dorsal); 2 guns (flanks) |
| nl-h | NiteLife Energy | H | station monitor | appliance, scaled | cartridge (rear); 3 guns (forward) |
| ls-l | Lightsail Express | L | courier | long-haul cab | cab; 1 clamp rail (spine); 1 gun (cab roof) |
| ls-m | Lightsail Express | M | armed hauler | truck and trailer, fused | 2 containers; 2 guns (cab); clamps (spine) |
| ls-h | Lightsail Express | H | convoy tug | road train, fused | 3 containers; docking collar (stern); 2 guns |
| cet-l | Cetacean Navigators | L | corridor escort | dolphin flanks | 2 lobes; 1 gun (boom); amber locks |
| cet-m | Cetacean Navigators | M | recovery tug | beluga back | 2 lobes; tow points (stern); 1 gun |
| cet-h | Cetacean Navigators | H | rescue carrier | whale decks | 2 lobes; transfer locks (ridge); 2 guns |
| cry-l | Cryonix | L | cold sniper | spruce cone | folded emitters (flank); 1 long gun (bow) |
| cry-m | Cryonix | M | heat-sink skirmisher | vacuum flask | folded emitters; 2 guns (waist) |
| cry-h | Cryonix | H | long-range heavy | closed cone, large | folded emitter scales; 1 long gun (bow) |
| sol-l | Sol Dominion | L | interdiction cutter | cast-iron flatiron | sensor plane (dorsal); 2 disablers (shoulders) |
| sol-m | Sol Dominion | M | patrol cutter | hammerhead | hammer sensor plane; 2 disablers (tips) |
| sol-h | Sol Dominion | H | registry cruiser | stamp handle | module bands; 3 disablers (bow) |
| adr-l | Adrasteia | L | deniable courier | flatfish | 1 gun, recessed (nose); 1 cut facet (camera side) |
| adr-m | Adrasteia | M | cold smuggler | cut gemstone, flat | 1 sensor (recessed); cargo collar (belly) |
| adr-h | Adrasteia | H | quiet gunship | stingray | 2 guns (recessed); asymmetric facet |
| dme-l | Death Monkey Explosives | L | charge runner | toolbox | charge feed (dorsal); 1 spike (bow) |
| dme-m | Death Monkey Explosives | M | infrastructure breaker | demolition frame | 2 spikes; detachable charge section (belly) |
| dme-h | Death Monkey Explosives | H | demolition barge | amp stack | 4 charge bays; 2 braces |

Hooks are drawn from the faction blocks and the bench where they exist (Zhestokost, Alakrita, AU, Finch, R&D, NiteLife, Lightsail, Cetacean, Cryonix, Sol Dominion, Adrasteia, DME entries) and are otherwise this brief's suggestions; the first batch is where they get tested. The guideline "one analogy per prompt" is kept: the hook column is the one analogy; the features are placements.

**Count:** 36 concepts x 4 seeds = 144 low-res images.

## 7. Sift plan

1. **Probe (2 images, before anything else):** one concept at 768x512 with the upsampler off, one at 1536x1024, same prompt, to check that the long composed prompt is not truncated and that 768x512 is legible. If the low-res image is unreadable, sift at 1152x768 and update the cost.
2. **Stage 1, low-res:** 144 images at 768x512, upsampler off, seeds from section 5. Cost is not measured; if time scales with pixels it is about a quarter of 45 s, so about 12-15 s each and roughly 35 minutes in all. Run in chunks of 12 so exit 75 loses little, one client at a time, driver stops at the first 75.
3. **Ingest** as `<concept>-s<k>` ships (status `new`, `UNREVIEWED`), through `build.mjs` from a renders root of PNG plus record. Votes: `up`/`down` per ship on Bifrost.
4. **Cull by vote:** per concept, keep the seed with the most net up votes; a concept with no net-positive seed is dropped. A human looks at the kept images before anything is re-rendered; a Mind's reading of a thumbnail is not a review.
5. **Stage 2, hi-res:** re-render each kept concept at 1536x1024, same prompt, 2 new seeds (`k = 4, 5`), `parent` set to the winning low-res image. About 36 x 2 x 45 s = 54 minutes at most.
6. **Review:** an operator verdict moves a ship out of `new`; only her word sets `pass` or `scrap` (ships.yaml header).

## 8. Rules for these images

- **Concept-only until human-reviewed.** Klein outputs are not shipped assets; the licence question (revenue line, affiliates, what concept art feeding a shipped product counts as) is open with whoever holds the legal root (`tools/raven-image/README.md`, "Licence: concept art only until ruled").
- **How each image was made is recorded** per section 5, one record per image, committed with the image.
- **No training on outputs**, nor on anything derived from them (FLUX licence 4.b).
- No likenesses of real people (licence 4.a); no political symbols (lore).
- Nothing in this batch calls Raven until the driver, the probe in section 7 and the install are done.

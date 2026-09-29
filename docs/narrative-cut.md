# Aetheria narrative: target verification and cut map

Status: Imagination pass, 2026-09-29; Q1-Q9 ruled 2026-09-30 (below). No code landed. The ends are owned by
`docs/narrative-target.md` (on `origin/codex/item-provenance`, `53d0f304`). This document owns the means and
the corrections that target needs before anyone cuts against it.

Anchors. Every `file:line` is against **`b66ba524`** (`origin/codex/fire-control-12` tip, 2026-09-29), unless
another SHA is named. The target's own citations were written against `cd846916`.

## Rulings (operator, 2026-09-30)

- **Q1 A.** A quest is a teaching and progression unit: the target's skeleton built on the engine's injection
  model. It is authored, credited, may grant items and tracks stages. The lore mission contract is a later layer.
- **Q2 → A (conditional ruling, resolved).** Operator: "I would go for Ink tags iff the assumption holds that
  Ink files can be fully self-contained questlines. If we need a NarrativeData record anyway, I'd start
  migrating metadata there." The condition fails: Emily's files cannot take tags without editing (C4, Q5), and
  RSA spans several files through `INCLUDE` (C3). So identity lives in the typed `NarrativeData` record, and
  the existing global-tag metadata (`#planet`, `#constraint`, `#trade`) migrates into it too. Ink carries no
  metadata tags; the loader refuses them.
- **Q3 A.** Every compiled story, place or quest, has a record and the sticky header.
- **Q4 both.** Rewards are ordinary lots with a `Granted { Quest }` provenance variant. Story objects are
  designs owned by a quest (a design-level link). Cut 4 is now mappable.
- **Q5 B.** `Corvus6.ink2`, `TestLociA-D.ink` and `TestQuestMultiLoci.ink` are Emily's: credited, never
  edited. `Locations/TestA.ink` and `Quests/TestQuest.ink` are engine fixtures and move under `tests/`.
- **Q6 A.** The header names the active story. Each injected choice carries a compact credit line until
  entered.
- **Q7 A.** Quest progress persists as Ink's state snapshot, an opaque blob sealed in one typed run-store
  document per quest.
- **Q8 A (default, follows from Q2 and Q5).** Fixtures live only under `tests/`. A record in the shipped
  catalog is the only admission.
- **Q9 A (default, follows from the operator's 2026-09-30 branch cleanup).** Once Cut 0 cherry-picks this doc
  onto the narrative branch, `origin/codex/item-provenance` is deleted.

## 0. Lineage (verified)

- `origin/codex/item-provenance` = `53d0f304`. Its 3 commits (`48b4b922`, `a6d06d0a`, `53d0f304`, all
  2026-09-25) sit on **`cd846916`** ("Merge Cuts 10 and 11", 2026-09-22). `cd846916` is an ancestor of
  fire-control-12. `git rev-list --left-right --count fc12...item-provenance` = **90 behind, 3 ahead**. So
  the commits sit on the fire-control lineage, but on a point 90 commits back, not on its tip.
- `git diff --stat cd846916 53d0f304` shows exactly one file: `docs/narrative-target.md` (+67). The commits
  contain no code. `docs/narrative-target.md` does not exist at `b66ba524`, and `git merge-tree` of
  fc12 with item-provenance is clean.
- The branch name is stale. The item-provenance campaign merged to master at `9b85211f` (2026-09-17). The name
  now points at a narrative doc. `docs/merge-to-master-checklist.md` says item-provenance is an ancestor of
  the fc12 tip. That was true of its 09-17 tip, but it is false now that the branch has moved.
- Master (`9b85211f`) has 2 commits that fc12 lacks. One is `263ac421` (REC-095), which deletes
  `GameData/Narrative/HeroOrZero.json`, `Locations/TestA.json` and `Quests/TestQuest.json`. `git merge-tree`
  of master and fc12 is clean. After fc12 merges, those stale compiled-JSON fixtures are gone. Every
  narrative `.cs` file cited below reaches master unchanged from `b66ba524`, because master's only extra
  code-bearing parent is the item-provenance tip that fc12 already contains.
- The operator's working tree (`F:\Projects\Aetheria`) is on local `codex/fire-control-12` at `4b594e11`,
  8 behind origin, with uncommitted work. This pass did not touch it. All reads used `git show`/`git grep`
  against SHAs.

**Plan consequence:** fc12 merges to master first. The narrative campaign then branches from that merge. It
cherry-picks the 3 doc commits with no conflict, applies the corrections in section 1, and leaves
`codex/item-provenance` for the operator to retire (Q9).

## 1. Target vs Body: corrections

Mechanism claims were established by probe where it mattered. **Ink probe:** a headless net10.0 console
compiles the repo's own `Assets/Plugins/Ink/**` and loads `GameData/Narrative` from a `git archive` snapshot
of `b66ba524`. It ran on Yggdrasil via `ygg-verify.sh` (dotnet image), exit 0. The source is in the
scratchpad at `inkprobe/Program.cs` and `inkprobe-repo/`.

| # | Target says | Body at `b66ba524` | Correction |
|---|---|---|---|
| C1 | "`StoryProcessor.cs` compiles `Locations/*.ink` and `Quests/*.ink`" | True of the class (`StoryProcessor.cs:57,64`). But **nothing calls it**: `Galaxy.cs:241-243` has been commented out since `ce1a0a46` ("It Runs", 2021-10-27, no reason given). No `LocationStory` or `GalaxyQuest` is ever built at runtime. | The Ink pipeline is dead code. Reviving it is the first mechanism cut, not an existing capability. |
| C2 | "The gap: the live catalog has no station hull ... Locomotion Cut 1 is recovering one" | Stale. `docs/locomotion-cut.md:375` (operator ruling 2026-09-25) records that the catalog already has a station hull, Zenith, and that the story-station path is dead because of `Galaxy.cs:242`. Stations now generate, and the tutorial entrance always gets one (`ZoneGenerator.cs:213-218, 312-325`). The Local tab never appears because `galaxyZone.Locations` is always empty (`ZoneGenerator.cs:208`), so no station gets `Story = i` (`:306`), so `MenuPanel.cs:52` hides the tab. | Replace "The gap" with: the gap is the disabled `StoryProcessor` call. locomotion-cut.md:375 obliges this campaign to "first find out why they were switched off". |
| C3 | "File-level global tags already carry metadata (`#planet:`, `#constraint:`, `#trade:` in `ReconStationAlpha.ink`)" | `ReconStationAlpha.ink` is at the narrative root, not `Locations/`, so it is never enumerated. Probe: **it fails to compile** under StoryProcessor's include handler (`StoryProcessor.cs:23` resolves `INCLUDE` against the narrative root, but the included files are in `RSA/`): "Failed to load: 'TheActualFactualFactory.ink'", and the same for `QuestTexts.ink`. With includes resolved from the story's own directory, it compiles with 0 errors, and its global tags are `planet: ReconStationAlpha`, `constraint: DistanceFrom start < 1`, `trade: disabled`. `ProcessLocation` reads neither `planet` nor `trade` (`StoryProcessor.cs:152-225`). | The convention exists only in `Locations/TestA.ink`, an engine fixture. RSA can be made loadable **without editing Emily's text** by resolving includes per story directory. |
| C4 | "`#title:` and `#author:` fit the existing convention" | Probe: `GetContentTags` (`StoryProcessor.cs:228-243`) splits on every `:` and keeps only `tokens[1]`. `#title: The Long Way: Part One` parses as **"The Long Way"**. Repeated `#author:` tags do collect into a list (`[Metacrat, Opus 5.5]`). A tag placed after the first content line is not global (`globalTags` = null). | Ink tags can hold identity only if the parser is fixed and the tag sits above the first line. Emily's files cannot take new tags at all without editing them (see Q2). |
| C5 | "Story stations are placed by `ZoneGenerator.cs:~270-288`" | At `cd846916` that is `:284`. At `b66ba524` it is `ZoneGenerator.cs:292-310` (`station.Story = i` at `:306`). `EntitySerializer.cs:77` is unchanged. | Re-anchor. |
| C6 | "`LocalMenu.cs` ... injects each quest's branch at that dock (`:78-87`)" | Correct: `LocalMenu.cs:79-88`. The quest's identity is lost at `StoryProcessor.cs:124-126`: `fileName` is used only in a log line. `GalaxyQuest` (`Galaxy.cs:569-573`) holds only `Story` and `KnotLocations`. **No quest has any identity today.** | State this. The header needs an identity that does not yet exist anywhere. |
| C7 | (silent) | Placement is not persisted. `SavedZone` (`SavedGame.cs:142-160`) has no locations. The load constructor `Galaxy(CultCache, SavedGame, ...)` (`Galaxy.cs:48-92`) never rebuilds `GalaxyZone.Locations`. So once a story station exists, `EntitySerializer.cs:77` indexes an empty list, and **Continue throws**. Quest progress (Ink state) is not persisted either. | Add to the target: reviving places requires typed placement state in the run store. |
| C8 | "Quests are self-contained: one quest, one author, one title" | Contradicts the bullet above it ("a list of credited authors, never a single owner slot"). | Change to "one quest, one title, one credit list". |
| C9 | Invariant 1 covers quests. The Emily boundary covers her words. | Place stories (dock text) are not quests under the target's own model, yet RSA, Corvus6 and HeroOrZero are place- or story-shaped prose by Emily. Under the invariant as written, her place text would show with no credit. | Open fork, Q3. |
| C10 | "a quest item" | No quest-item concept exists. `Provenance` is a union of `Attributed`, `Produced` and `Extracted` (`Provenance.cs:92-120`). Item inspection is `PropertiesPanel.cs:361-386` (Manufacturer from `ItemManager.Brand`, `ItemManager.cs:206-222`). | Open fork, Q4. Do not specify it before asking. |
| C11 | Emily file list: `HeroOrZero`, `ReconStationAlpha`, `RSA/*`, Terminus, `demooutline` | The narrative dir also holds `Locations/Corvus6.ink2` (prose), `TestLociA-D.ink` and `TestQuestMultiLoci.ink` (a state-machine test story), and `Locations/TestA.ink` and `Quests/TestQuest.ink` (injection fixtures). The ruling's enumeration does not mention them. Probe: TestLociA-D fail standalone (unresolved variables; they are INCLUDE targets), and `RSA/*` fail standalone the same way. | Open fork, Q5. The ruling is preserved verbatim. Git accounts are not used as evidence of authorship. |

Also found, not in the target:
- **Lore conflict.** AetheriaLore `Aetheria/Game Design/Narrative and Missions.md` is a design-lineage mission
  contract: admission, evidence, receiving owners, and "the unit of exclusivity is a historical consequence,
  not a quest journal slot". The target's Star Sonata skeleton is a different concept of a quest. Q1 must
  decide which one this campaign serves.
- **Lore precedent for credits.** The lore site already attributes by frontmatter `author` (string) or
  `authors` (list), rendered by `site/quartz/components/AetheriaAuthorMeta.tsx`, and `Stories/When We Get
  Home.md` carries `author: Emily Harvey`. So a typed credit list has a sibling precedent. That component joins
  names with ", ", while the target writes "+". The join is rendering, not data.
- **Revival hazards the first probe must cover:** `ProcessLocation` hands `selector.SelectZone` a possibly empty
  candidate list and then calls `zone.Locations.Add` (`StoryProcessor.cs:197-217`), which is a probable null
  dereference when no zone matches. `TestA.ink` places a station of faction "Miss Terri" in the start zone
  (`DistanceFrom start = 0`), so a revived pipeline would put an engine fixture into the tutorial entrance.
  The `StoryProcessor` constructor writes to disk (`:51-52`, `CreateSubdirectory`). `Galaxy`'s load
  constructor has no narrative directory to recompile from (`MainMenu.cs:106`).
- **Glob footgun (unverified for Unity).** `Quests` enumerates `"*ink"` (no dot, `:64`). On Linux
  (.NET 10), `"*.ink"` did not match `Corvus6.ink2`. Windows .NET Framework and Mono can match a three-letter
  extension pattern against longer extensions. That was not probed under Unity. The typed-record cut deletes
  the globs, so the question dies with them.

## 2. The standing ruling (preserve verbatim)

Already in the target's Open section at `53d0f304`. Carry it forward unchanged:

> **Existing narrative files are Emily Harvey's.** Operator, 2026-09-25: "Pretty sure those words are all
> Emily's. Attribution goes to Emily Harvey." `GameData/Narrative/*.ink` (`HeroOrZero`, `ReconStationAlpha`,
> `RSA/TheActualFactualFactory`, `RSA/QuestTexts`, the Terminus files, `demooutline`) are credited to her when the
> author field lands, and fall under the same boundary as `AetheriaTexts.ctd`: not edited in place, quoted and
> attributed, and any extension is a separate, labelled quest crediting its actual authors. Git commit accounts
> are not attribution and are not a source for it.

Consequences every cut must honour:
- No cut edits any file in the ruling. That includes adding `#title`/`#author` tags, fixing her INCLUDE paths,
  and renaming `Corvus6.ink2`. Loader changes that make her files load as written are allowed.
- A wrapper or record that places her text is placement, and "adds no name". Her text is credited
  "Emily Harvey" alone.

## 3. Replacement text for the target

Replace "Machinery that already exists" with:

> ## Machinery that exists, and its state
> - **Ink, compiled but switched off.** `ServerShared/Narrative/StoryProcessor.cs` compiles `Locations/*.ink` as
>   places and `Quests/*ink` as quests, but its only call site, `Galaxy.cs:241-243`, has been commented out
>   since `ce1a0a46` (2021, no reason recorded). No place or quest is built at runtime. Only
>   `Locations/TestA.ink` and `Quests/TestQuest.ink`, both engine fixtures, are in the enumerated folders.
>   Emily's files sit at the narrative root and are never loaded. `ReconStationAlpha.ink` fails to compile
>   under the current include handler and compiles cleanly when includes resolve from its own folder.
> - **Places.** A placed place gets a story station (`ZoneGenerator.cs:292-310`, `station.Story = i`). The index
>   resolves through `EntitySerializer.cs:77`. The station hull exists (Zenith), and the tutorial entrance
>   always gets a station (`docs/locomotion-cut.md:375`).
> - **Dock text.** The Local tab shows when the docked entity has a story (`UI/Menu/MenuPanel.cs:52`).
>   `UI/Menu/LocalMenu.cs` runs the place's story and injects quest choices at matching knots (`:79-88`).
> - **Identity.** None. A quest is its Ink `Story` and its knot map (`Galaxy.cs:569-573`). The file name is
>   discarded (`StoryProcessor.cs:124`).
> - **Persistence.** None. Placement and quest progress are not in the run store (`SavedGame.cs:142-160`).
>   Loading a run with a story station would throw at `EntitySerializer.cs:77`.

In "Quests are self-contained", change "one author" to "one credit list". Keep the ruling paragraph above
verbatim, and add the open forks below by reference.

## 4. Operator forks

One question per fork. Each can be decided cold. Q1 blocks everything else.

### Q1. What is a quest for?

Context: three concepts are in play. (a) The target's Star Sonata skeleton: each quest teaches one system at a
story station, and quests layer over places. (b) The engine's legacy model: Ink quest files inject choices
into a place's knots (`LocalMenu.cs:79-88`). (c) AetheriaLore's mission contract: admission, evidence, receiving
owners, and consequences that separate owners commit. I will not infer (a), (b) or (c) from the legacy code.
The answer decides whether quests have progress state, completion, rewards, items, and a journal.
- **A.** A teaching and progression unit: (a) implemented on (b). It is authored, credited, and may grant items
  and track stages. The lore contract is a later layer.
- **B.** The lore mission contract (c) from the start. Heavier, with typed evidence and consequences.
- **C.** Authored dock content only: (b) plus credits. No progress state, no items.
- **Recommended: ask, don't pick.** If forced: **A**. It is the target's own shape, the curated opening needs
  it, and it does not preclude (c) later.

### Q2. Where does quest identity (title and credits) live?

Context: the header needs identity at every surface. Emily's files cannot be edited, so her files cannot
carry tags. `GetContentTags` truncates titles at a second colon. Ink tags are prose inside content. A catalog
record is typed, lives in `GameData/Aetheria.cc` beside `ItemData`, is editable in CultCache Studio, and can be
validated when `AetheriaStores.Open` runs, as it already validates heat response and roles
(`AetheriaStores.cs:32-47`).
- **A. Typed record only.** A catalog document per story (working name `NarrativeData`, schema
  `aetheria.narrative`) with `Title`, `Authors[]`, `InkPath` and `Kind`. It is the one owner. Ink carries no
  identity tags, and the loader rejects `#title`/`#author` if present.
- **B. Ink tags only.** `#title`/`#author` global tags. This cannot credit Emily's files without editing them
  or wrapping them, and a wrapper would be authored by someone else.
- **C. Both.** The record owns the identity and Ink tags are checked against it. Two spellings of one fact,
  with a comparator to keep them agreeing.
- **Recommended: A.** It is the operator's standing preference for typed state. It is the only option that
  credits Emily without touching her files. It has one owner and no comparator. The record also replaces the
  directory globs as the list of what loads, so it deletes the file name as identity.

### Q3. Do places carry title and credits too?

Context: the invariant names quests, but dock text belongs to a place, and much of Emily's prose (RSA,
Corvus6) is place-shaped. Under the invariant as written, her place text would render uncredited.
- **A.** Every compiled story (place or quest) has a record and a sticky header.
- **B.** Places show credits in a quieter form (a footer or byline). Quests keep the sticky header.
- **C.** Quests only, as written. Place prose must then be wrapped as a quest in order to be credited.
- **Recommended: A.** One record type, one header rule, and her boundary holds everywhere without a special case.

### Q4. What is a quest item for?

Context: nothing like it exists. Two shapes are natural. (i) A `Provenance` variant (union slot 3, for example
`Granted { Quest }`), so a particular lot came from a quest and the inspection header reads it from the lot, as
`Brand` does. (ii) A design-level link, where an `ItemData` belongs to a quest, so every instance of that design
is a quest item. (i) fits "provenance is who made it and from what"; (ii) fits unique story objects.
- Question to the operator: is a quest item a reward (a normal item with quest origin), a story object (key,
  data chip, unique prop), or both?
- **Recommended:** decide after Q1. If both, (i) for rewards and (ii) for story objects. No cut is mapped
  until you answer.

### Q5. Does the Emily ruling cover the files it does not list?

Context: `Locations/Corvus6.ink2` is place prose in the same register as her other files. `TestLoci*.ink`
and `TestQuestMultiLoci.ink` are a small inventory and state test story. `Locations/TestA.ink` and
`Quests/TestQuest.ink` are injection fixtures whose content describes the mechanism ("This choice is being
injected from TestQuest"). Git history is not attribution, so I cannot settle this.
- **A.** All of them are hers: credit them, don't edit them, and move nothing.
- **B.** Corvus6 and the TestLoci set are hers. TestA and TestQuest are engine fixtures and move to
  `tests/Aetheria.Shared.Tests/Fixtures/Narrative/`, so they are not placed into real galaxies.
- **C.** You name the split.
- **Recommended: B, if it matches what you know.** Otherwise A, and Cut 2 must still keep TestA out of real
  galaxies without editing it (see Q8).

### Q6. What does the sticky header show when several quests are on screen at once?

Context: while a place's story is active, `LocalMenu.PresentCurrentChoices` (`:76-91`) lists choices injected
from every quest registered at that knot, alongside the place's own choices. One screen can hold words from
three sources.
- **A.** The header names the active story. Each injected choice carries a compact credit line (quest title and
  authors) until the player enters it, and then that quest's header takes over.
- **B.** The header lists every story with content on screen.
- **C.** Injected choices show only the place's header until entered.
- **Recommended: A.** It keeps "whose words are on screen" true for every line. C violates the invariant for
  choice text.

### Q7. Does quest progress persist across save and load?

Context: today nothing does. Placement persistence is not a fork, because it is required to stop the load
crash in C7. Quest progress (Ink story state per quest, in the run store beside `ProvenanceLedger`) depends on
Q1.
- **A.** Yes. A run-store document holds each quest's Ink state JSON as an opaque blob. It is typed at the
  document level, but the blob itself is Ink's JSON: a xenos boundary.
- **B.** Yes, but typed: stage and flag fields only, with Ink variables re-seeded from them.
- **C.** No. Quests restart on load.
- **Recommended:** decide after Q1. For A (teaching quests), **A**. Ink's state is the only faithful snapshot,
  and the JSON stays sealed inside one typed document.

### Q8. Should fixture stories ever reach a real galaxy?

Context: once the pipeline is revived, anything with a record, or anything in `Locations/` under today's
globs, is placed. TestA is placed in the start zone, which is the tutorial entrance.
- **A.** Fixtures live under `tests/` only, and the real catalog lists only content you rule placeable.
- **B.** Fixtures stay in `GameData`, and a record flag excludes them.
- **Recommended: A.** With Q2-A, "has a record in the shipped catalog" is the only admission, and it needs no
  flag.

### Q9. The `codex/item-provenance` branch pointer

Context: that campaign merged on 09-17. The branch now carries only the narrative doc, 90 commits behind fc12.
- **A.** After the doc is cherry-picked onto the narrative branch, delete `origin/codex/item-provenance`.
- **B.** Leave it.
- **Recommended: A.** A campaign name pointing at another campaign's doc is a false map. Deleting it is your
  call.

## 5. Cut map

Status: cut map, nothing landed. It depends on fire-control-12 merging to master (clean per `git merge-tree`).
The cuts are specified on the **recommended** answers (Q1-A, Q2-A, Q3-A, Q5-B, Q6-A, Q7-A, Q8-A). A different
ruling rewrites the named cuts. Cut 1 cannot start before Q1-Q3 are answered.

Branch for all cuts: **Aetheria `codex/narrative-1`** from the fc12→master merge commit (call it `M`). Line
anchors are `b66ba524`, and Hands re-anchors them at `M`. The expected drift in these files is none, because
master's only extra code parent is already contained in fc12.

**Verification transport (a rake found in this pass).** `ygg-verify.sh` cannot push Aetheria directly. The
repo's Git LFS `pre-push` hook rejects the scp-style remote (`Invalid remote name "ygg:eureka-verify/..."`).
Also, `Aetheria.Shared.csproj` needs CultLib at the pinned `45c2f400` (`Directory.Build.props`), and
`GameData/Aetheria.cc` is an LFS object. The working recipe used here is a scratch snapshot repo built with
`git archive` and pushed through `ygg-verify.sh`. For the test suite, the snapshot must also hold `CultLib/`
(`git -C F:/Projects/CultLib archive 45c2f400`), the smudged catalog (`git show <rev>:GameData/Aetheria.cc |
git lfs smudge`), and `-p:SkipCultLibRevisionCheck=true` (the pin is honoured by archiving that exact
revision). Only the Ink-only half of this recipe has been proven. Self should either land it as a stopgap
note or route it to the Idunn verify campaign. Hooks are not to be skipped.

### Cut 0. Land the corrected target

- **Repo/branch:** Aetheria `codex/narrative-1` from `M`.
- **First:** `git cherry-pick 48b4b922 a6d06d0a 53d0f304` (docs only, conflict-free).
- **Deletes first:** the target's "Machinery that already exists" section (replaced by section 3), and the
  "The gap" bullet.
- **Adds:** the section 3 text. The C8 wording fix. An "Open" list pointing at Q1-Q9 and the rulings once
  given. This map lands as `docs/narrative-cut.md`, with the status header per the Eureka shape.
- **Verification:** doc-only. Negative: `rg -n "no station hull" docs/narrative-target.md` must return
  nothing.
- **Subtraction:** about -15 / +35 doc lines. No code.

### Cut 1. Story identity is a typed catalog record

- **Deletes first:**
  - `StoryProcessor.cs:51-52` (`CreateSubdirectory`: the constructor writes to the data directory).
  - `StoryProcessor.cs:55-66` (directory glob enumeration of `Locations/*.ink` and `Quests/*ink`). This is the
    identity-by-location-and-filename path.
  - `StoryProcessor.cs:117` (the dead commented `WriteAllText`).
- **Adds:**
  - `Assets/Scripts/ServerShared/Narrative/NarrativeData.cs`:
    `[CultDocument("aetheria.narrative","1"), MessagePackObject] class NarrativeData`, with `Title` (string),
    `Authors` (string[]), `InkPath` (string, relative to the narrative root) and `Kind` (enum `Place`,
    `Quest`). This is a catalog type, so it is authored and read-only in game.
  - `AetheriaStores.cs:9`: add `typeof(NarrativeData)` to `CatalogTypes`.
  - Validation in `AetheriaStores.Open` next to `:34-47`: an empty `Title`, an empty or blank `Authors`, or an
    empty `InkPath` throws, naming the record. This follows the "fails loudly, naming the item" rule already
    there.
- **Per-file changes:**
  - `StoryProcessor.cs:41-53`: the constructor takes the catalog's `NarrativeData` records instead of scanning
    folders. `ProcessStories` iterates records by `Kind`, in stable key order, so placement is deterministic.
  - `StoryProcessor.cs:102-120` `GetStory`: key the compile cache by record key, not by file name. **Include
    resolution is per story directory**: construct `AetheriaInkFileHandler` with the Ink file's own directory
    (`:15-25`, `:49`, `:112`). This is the probe-proven fix that makes `ReconStationAlpha.ink` load unedited.
  - `StoryProcessor.cs:122-150`, `:152-225`: `GalaxyQuest` and `LocationStory` carry
    `CultRecordRef<NarrativeData> Narrative` (`Galaxy.cs:569-586`). A story whose globals include `title` or
    `author` is refused, naming the file. This enforces one owner.
  - `StoryProcessor.cs:228-243` `GetContentTags`: split on the first `:` only
    (`tag.Substring(0, i)`, `tag.Substring(i+1)`). Placement tags stay Ink-owned. Values containing `:` must
    survive (C4).
  - Catalog content: Hands authors no records for real content until the operator rules what is placeable
    (Q1, Q5). The catalog ships with zero `NarrativeData` records, so the pipeline has nothing to place. That is
    correct and harmless while `Galaxy.cs:241` stays commented until Cut 2.
- **Authority map:**
  - Owner: the `NarrativeData` catalog record owns what loads, its title, its credits, and whether it is a
    place or a quest.
  - Inputs: the catalog store, and the Ink file at `InkPath`.
  - Outputs: `LocationStory.Narrative` and `GalaxyQuest.Narrative`.
  - Derived state: the Ink file name is no longer identity. It is derived from `InkPath` for log text only.
    `LocationStory.Name` stays the in-world entity name (`#name`) and is not the story title.
  - Forbidden writers: directory globs (deleted), `#title`/`#author` Ink tags (refused), git history (never
    read).
  - Shared paths: new game (`MainMenu.cs:132,160` → `Galaxy` ctor) and load (Cut 2) both resolve stories only
    through records.
  - Deletion line: `StoryProcessor.cs:51-52, 55-66, 117`.
- **Verification** (new `tests/Aetheria.Shared.Tests/NarrativeTests.cs`, run on Yggdrasil):
  - `RecordIsTheOnlyAdmission`: a fixture narrative dir holds 2 Ink files, and a cache holds a record for 1.
    Only that one compiles or places. Pins: no record, no story.
  - `CatalogRefusesUncreditedStory`: `Open` throws on an empty `Authors` or `Title`, naming the record. Pins
    the invariant at the store boundary.
  - `InkIdentityTagsRefused`: a fixture with `#author:` throws, naming the file. Pins one owner.
  - `IncludesResolveFromStoryDirectory`: load the real `GameData/Narrative/ReconStationAlpha.ink` read-only
    through the loader and expect 0 compile errors. Pins that Emily's file loads unedited.
  - `TagValuesKeepColons`: `#select: DistanceFrom home.X > 2`-style values and colon-bearing values survive.
  - Negative: `rg -n 'EnumerateFiles\(' Assets/Scripts/ServerShared/Narrative` returns nothing. Also
    `git diff M -- GameData/Narrative` returns empty (her files are untouched).
  - Operator: Studio click-through that a `NarrativeData` record is creatable and editable in `Aetheria.cc`.
- **Subtraction:** about -20 / +70 code, about +120 test. Adds one schema. No new project, package or target.

### Cut 2. Revive placement, and make it survive save and load

- **Depends on:** Cut 1, Q5 and Q8.
- **First:** a headless probe test, `RevivedPipelineOnRealGalaxies`. It runs `ProcessStories` with fixture
  records on a generated tutorial galaxy and a generated standard galaxy (the `RestoredHullsTests.cs:303,393`
  construction path) across 20 seeds, and records every exception. This answers locomotion-cut.md:375 ("find
  out why they were switched off") with evidence before any fix. The expected findings to confirm or refute
  are the empty-candidate null dereference (`StoryProcessor.cs:197-217`) and an unresolved faction.
- **Deletes first:**
  - `Galaxy.cs:241-243`: the commented block becomes the live call, with records passed in.
  - `LocalMenu.cs:106-110` (empty `Update`).
  - Per Q5-B/Q8-A: `git mv` `GameData/Narrative/Locations/TestA.ink` and `Quests/TestQuest.ink` (plus
    `.meta`) to `tests/Aetheria.Shared.Tests/Fixtures/Narrative/`. The empty `Locations/` and `Quests/` folders
    go with them.
- **Adds and changes:**
  - `StoryProcessor.cs:197-200`: when no zone satisfies a place's constraints, log it naming the record and do
    not place it. Quests that `#required` it drop, using the existing path at `:136-140`.
  - `SavedGame.cs:142-160` `SavedZone`: add `[Key(6)] CultRecordRef<NarrativeData>[] Locations`, the zone's
    placed stories in index order. `RunSave.Capture` (`SavedGame.cs:82-90`) writes it from
    `zone.Locations`.
  - `Galaxy.cs:48-92` load constructor: takes the narrative root. For each zone, it recompiles its saved
    records in order into `LocationStory` (no RNG and no constraint evaluation, because placement is
    replayed, not re-rolled). It then re-registers quests against the restored places. A saved record the
    catalog lacks throws, naming it, like `ProvenanceLedger`'s loud absent lot (`Provenance.cs:25-34`).
    `MainMenu.cs:106` and `tools/AetherDb/Program.cs:804` pass the root.
  - `EntitySerializer.cs:77`: unchanged. The index is valid because order is persisted. The test pins it.
  - Q7-A (if ruled): add a run-store `aetheria.questprogress` global with record key → Ink state JSON, added
    to `RunTypes` (`AetheriaStores.cs:10`) and written in the same `RunSave.Commit` batch. If Q7 is not ruled,
    this piece waits. It is not needed to stop the crash.
- **Authority map:**
  - Owner: generation owns first placement (`StoryProcessor.ProcessLocation`). The run store (`SavedZone.Locations`)
    owns placement thereafter.
  - Inputs: catalog records, the galaxy, the RNG at generation only.
  - Outputs: `GalaxyZone.Locations`, and `OrbitalEntityPack.Story` indices.
  - Derived state: after a load, `GalaxyZone.Locations` is derived from `SavedZone.Locations` and is never
    re-rolled.
  - Forbidden writers: constraint evaluation on load. Any second placement pass.
  - Shared paths: new game, Continue, and `AetherDb` galaxy load all go through the same record-to-story compile.
  - Deletion line: `Galaxy.cs:241-243` comments, `LocalMenu.cs:106-110`, fixture files out of `GameData`.
- **Verification:**
  - `PlacedPlaceGetsStoryStation`: a fixture place with `DistanceFrom start = 0` on the tutorial galaxy gets a
    station whose `Story` resolves to that record. Pins the chain from place to station to tab condition.
  - `PlacementSurvivesSaveAndLoad`: generate, `RunSave.Capture`+`Commit`, `new Galaxy(cache, saved, root)`,
    then unpack the zone. The station's `Story.Narrative` equals the original. **Negative:** the same test at `M`
    throws `ArgumentOutOfRange` (C7). Keep that failure evidence in the Hands report.
  - `UnplaceablePlaceIsReportedNotThrown`: constraints matching no zone produce a log line and no exception.
  - `QuestInjectsAtRestoredPlace`: after a load, the fixture quest's knot is registered at the restored place.
  - `NoFixtureInShippedGalaxy`: generate with the shipped catalog. No `LocationStory` comes from `tests/`.
  - `RunSaveTests` stays green. The run-record set changes only by Q7's document, and `IsRunRecord` covers it.
  - Operator (play): New Game (tutorial) → dock at the story station → the Local tab shows and the story
    runs → an injected choice appears → save, quit, Continue → dock → the same story and choice appear.
    This needs one real record that Q1 and Q5 admit. If there is none, use a fixture record in a scratch
    catalog, per the checklist's scratch-catalog practice.
- **Subtraction:** about -10 / +80 code, about +200 test. Adds one `SavedZone` key (optionally one run
  document). Moves 2 fixtures (net 0).

### Cut 3. The sticky header

- **Depends on:** Cut 2 and Q6.
- **Deletes first:** `LocalMenu.cs:78` (the `Debug.Log` of the path/story kind). The header supersedes it as
  the observable signal.
- **Adds:**
  - `LocalMenu`: a header (TMP text) bound to the active story's `Narrative` (Title, Authors joined for
    display). It updates wherever `_activeStory` changes (`:28`, `:64-71`, `:99`). Per Q6-A, `PresentChoice`
    (`:93-104`) adds a credit line to choices whose `story` is not the active one.
  - One pure derivation, `NarrativeCredit.For(Story)`, that returns the record or throws. The UI calls only
    this, so no UI path can render content without an owner.
  - The Local menu prefab/scene gains the header element. That is a Unity asset edit. Hands edits the YAML only
    if the prefab path is certain. Otherwise it is an operator click-through.
- **Authority map:** the owner is the `NarrativeData` record, and the header is display-only. Forbidden
  writers: any string constant or file-name fallback. Shared paths: entering, finishing, and re-entering a
  story all go through `NarrativeCredit.For`.
- **Verification:**
  - `EveryReachableStoryHasCredit`: for every story the pipeline builds from the catalog, `For` resolves. This
    is a headless pin of "no content without an owner".
  - Negative: `rg -n '\.text\s*=.*FileName' Assets/Scripts/UI/Menu` returns nothing.
  - Operator (play, timeline): on dock, the header shows the place. Enter an injected choice, and the header
    switches to the quest mid-transition, with no frame showing the place credit over quest text. Reach the
    quest END and the header returns to the place. After save and Continue, the header shows on first dock.
- **Subtraction:** about -1 / +45 code, one prefab edit, about +40 test.

### Cut 4. Quest items: parked

This cut is not mapped. It needs Q1 and Q4. Candidate anchors for when it is: `Provenance.cs:92-95` (union
slot 3), `ItemManager.cs:136-170` (lot minting), `PropertiesPanel.cs:361-386` (the inspection header goes
above `Description`), and `SavedGame.cs` via `ProvenanceLedger.Reachable`, which already persists lots.

### Out of this map

- Content: the curated opening and the widening galaxy. Records for Emily's files and any new quests need Q1
  and Q5. Extensions of her premise are separate records crediting their actual authors.
- `Thread.Sleep(500)` at `Galaxy.cs:246` ("to make it seem like it's doing more work"). Unrelated. Note it for a
  separate sweep.

## 6. Subtraction ledger (estimate)

| Cut | Code − | Code + | Test + | Schemas / targets |
|---|---|---|---|---|
| 0 | 0 | 0 | 0 | none (doc about ±50) |
| 1 | ~20 | ~70 | ~120 | +1 catalog schema `aetheria.narrative` |
| 2 | ~10 | ~80 | ~200 | +1 `SavedZone` key; optionally +1 run schema (Q7) |
| 3 | ~1 | ~45 | ~40 | 1 prefab edit |
| 4 | parked | | | |

This is net additive. The capability it buys is explicit and ruled: credited, persisted narrative that does
not exist today. Nothing smaller works. Identity has no current owner to reuse, and the only existing carrier
(Ink tags) cannot reach Emily's files.

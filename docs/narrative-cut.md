# Aetheria narrative: target verification and cut map

Status: Imagination pass, 2026-09-29; Q1-Q9 ruled 2026-09-30 (below). v2 (2026-09-30, Imagination) amends
Cut 1 to migrate all Ink tag metadata into typed records per the Q2 ruling, maps Cut 4 (quest items) per the Q4
ruling, adds corrections C12-C15 and forks Q10-Q13 that those rulings raise, and expands the verification
transport note. No code landed. The ends are owned by `docs/narrative-target.md` (on
`origin/codex/item-provenance`, `53d0f304`). This document owns the means and the corrections that target needs
before anyone cuts against it. v2 was written from `origin/codex/item-provenance:docs/narrative-cut.md` at
`da9f51b9`.

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

- Update at v2: `origin/codex/item-provenance` is now `da9f51b9`, 5 ahead of `cd846916`. `42edc03a` and
  `da9f51b9` add only `docs/narrative-cut.md` (checked with `git diff --stat 53d0f304 da9f51b9`: one file). Cut 0
  therefore cherry-picks 5 doc commits, not 3. None of them conflicts, because neither doc exists on fc12.

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

### v2 additions: the tag metadata census

The Q2 ruling moves all tag metadata into typed records, so v2 took a census of every tag in the corpus
(`grep '#'` over every `.ink`/`.ink2` at `b66ba524`, excluding comment lines) and of every consumer of those tags.
Only three files carry any metadata tags:
- `Locations/TestA.ink:1-7`: `constraint`, `type`, `security`, `faction`, `name`, `nameZone`, `turrets`.
  This is an engine fixture that moves to `tests/` (Q5 B).
- `Quests/TestQuest.ink:2-3`: the knot-level tags `location` and `required`. Also an engine fixture.
- `ReconStationAlpha.ink:1-3`: `planet`, `constraint`, `trade`. **This is Emily's file.**

No other Emily file carries a tag of any kind.

| # | Tag / mechanism | Consumer at `b66ba524` | Finding |
|---|---|---|---|
| C12 | `name` → `LocationStory.Name` (`StoryProcessor.cs:206`) | **None.** No code reads `LocationStory.Name`. The station entity is not named from it (`ZoneGenerator.cs:292-310`). | Dead metadata. Its migration disposition is Q11. |
| C13 | `nameZone` → `zone.Name` (`:219-222`). TestA's comment says named zones are "excluded from being placed together" (`:198` filters `!z.NamedZone`). | `GalaxyZone.NamedZone` (`Galaxy.cs:565`) is **never set true anywhere** (`git grep 'NamedZone *='` finds nothing). The documented exclusion has never worked. | Migrate `ZoneName` and set `NamedZone` in the same write, so the documented rule holds. Alternatively drop both; see the Cut 1 disposition table. |
| C14 | `type` → `LocationStory.Type` (`:212-214`) | `ZoneGenerator.cs:208` builds stations only for `Type == Station`, and assigns `station.Story = i` with `i` indexing that **filtered** array (`:292-306`). `EntitySerializer.cs:77` resolves `pack.Story` against the **unfiltered** `zone.GalaxyZone.Locations`. So an `Asteroid` or `Planet` place listed before a station in the same zone makes that station resolve to the wrong story. Non-station places are also never given an entity. | This is a latent index bug. Only `Station` works end to end. Cut 1 drops `type`: every place is a station until another kind is built. That removes the misalignment by construction. |
| C15 | `constraint`/`select` anchors and `faction` | `ResolveFaction` matches by **name prefix** (`StoryProcessor.cs:97-100`, `StartsWith`). `ResolveZone("home.<faction>")` calls `HomeZones.ContainsKey(faction)` with a possibly null faction (`:77-82`), and `Dictionary.ContainsKey(null)` throws. Both selectors throw on an empty candidate list: `RandomSelector` calls `NextInt(0)`, and `OrderedZoneSelector` calls `First()` (`ZoneSelectors.cs:18-22, 45-48`). `DistanceConstraint` silently returns false on a malformed operator (`ZoneConstraints.cs:62-81`). | Typed refs and typed operators delete every one of these string-parsing failure modes. The empty-candidate case becomes an explicit "not placed" result (Cut 2). |

`planet` and `trade` (RSA) have **no consumer at all**: `ProcessLocation` never reads them. There is a
dock-gated `Trade` menu tab (`MenuPanel.cs:72-79`, `MenuTabButton.RequireDock`), which is a plausible consumer
for `trade`. I did not infer one. See Q11.

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

*History (v2): ruled, see Rulings. Cut 1 v2 supersedes option A's shape. There is no `Kind` field; `NarrativeData` is an abstract base with `PlaceData` and `QuestData` documents, and it carries all metadata, not only identity.*

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

*History (v2): ruled "both". Cut 4 is now mapped, and the option text below is history.*

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

### Forks raised by the Q2 and Q4 rulings (v2, open)

Cut 1 assumes the recommendation for Q10 and Q11. Cut 4 assumes the recommendation for Q12 and Q13. A
different ruling rewrites only the named pieces.

### Q10. "The loader refuses metadata tags", but Emily's RSA carries three and cannot be edited

Context: the Q2 ruling says Ink carries no metadata tags and the loader refuses them. `ReconStationAlpha.ink:1-3`
has `#planet`, `#constraint` and `#trade`, and the Emily ruling forbids editing it. A strict loader therefore
can never load RSA. Once migrated, the tags are inert, because no code reads tags any more (Cut 1 deletes every
tag reader). The question is only where the refusal lives, and how RSA passes it.
- **A. Refuse at the loader, with a typed acknowledgement.** The loader throws on any global or knot-start tag,
  unless the story's record lists that exact tag text in `SupersededTags` (string[]). Authors see the tag in
  Studio, next to the typed field that replaced it. For RSA that list is its three lines. For every new story it
  is empty.
- **B. Loader ignores tags; a corpus test refuses them.** Runtime never looks at tags. A test fails on a tag
  in any shipped story, and carries a named exemption for RSA.
- **C. Strict refusal, and RSA stays unloadable** until you rule otherwise.
- **Recommended: A.** It keeps your words ("the loader refuses them"), puts the exemption in typed state rather
  than in test code, and makes the migration visible per record. It is not a mode flag: it names the exact inert
  text, and any other tag still throws.

### Q11. What do `#planet`, `#trade` and `#name` mean?

Context: the ruling migrates `#planet`, `#constraint` and `#trade`. `constraint` has a live meaning. `planet`
and `trade` have never had a consumer (census, C12-C15). `#name` (TestA only) has no consumer either. Your
standing rule is to ask what a concept is for before specifying it from legacy text.
- **`#planet: ReconStationAlpha`.** A: it is the place's display name, so it becomes the station entity's name,
  and `#name` means the same thing. B: RSA should be a planet-type place (not built; see C14). C: drop it.
- **`#trade: disabled`.** A: a `TradeDisabled` bool that hides the dock-gated Trade tab (`MenuPanel.cs:50-52`)
  at this place's station. B: drop it.
- **Recommended:** `planet` A, merged with `name` into one `DisplayName` field that names the station entity
  (one field for one meaning). `trade` A, because the consumer already exists and the cost is one line at
  `MenuPanel.cs:52`. If you'd rather not decide now, record the values in `SupersededTags` only (Q10 A) and add no
  fields. No field ships without a consumer.

### Q12. What triggers a reward grant?

Context: Q4 ruled rewards are lots with `Granted { Quest }` provenance. Something must decide when a quest
grants. Ink cannot reach the game today: Emily's `pickup(x)`/`drop(x)` (`ReconStationAlpha.ink:39-45`) are pure
Ink functions over Ink variables, not engine calls.
- **A. Typed, per knot.** `QuestData.Rewards` lists `(Knot, what)`. The engine grants when a quest advance
  takes that knot's visit count from 0 to at least 1. Ink stays free of engine calls. Studio edits the rewards,
  and validation checks every knot path exists.
- **B. Ink calls out.** An `EXTERNAL grant(key)` function, bound by the engine. Game data then lives inside
  Ink text, which is the shape the Q2 ruling moved away from.
- **Recommended: A.** It matches the Q2 direction (metadata in typed state). Because Ink's persisted visit
  counts (Q7) own "already granted", a reload can never double-grant, and no second ledger is needed.

### Q13. Where does a reward go when the player's cargo is full?

Context: grants happen while docked, in the Local tab. `EquippedCargoBay.TryStore` (`Entity.cs:1805-1890`) returns false
when there is no room.
- **A.** Into the docked station's docking bay (`OrbitalEntityPack.DockingBayContents` persists it), so the
  player collects it there.
- **B.** The grant is refused and retried on the next advance. This needs "granted" state beyond visit counts,
  which is a second owner.
- **C.** The reward is lost.
- **Recommended: A.** It keeps visit counts as the single owner of "granted", and it matches how docked cargo
  already persists.

### Q14. Does RSA's record ship in the catalog now?

Context: with Q2's record-as-admission rule, a `PlaceData` for RSA in `GameData/Aetheria.cc` places RSA in every
galaxy once Cut 2 revives placement. Its constraint puts it in the entrance zone, which is the tutorial entrance
too. Its content is a Halloween candy-factory story (`trickortreat`, `TheActualFactualFactory`), which does not
obviously belong in the curated opening (Q1 A).
- **A.** The migration is proven with RSA's record as a **test fixture record** (Cut 1 pins it). The shipped
  catalog gains RSA only when the content campaign places it.
- **B.** Ship RSA's record now. It appears near the start of every run after Cut 2.
- **Recommended: A.** Migration fidelity and content admission are separate decisions. The content campaign owns
  the second.

## 5. Cut map

Status: cut map, nothing landed. It depends on fire-control-12 merging to master (clean per `git merge-tree`).
Q1-Q9 are ruled (2026-09-30), and the cuts follow those rulings. v2 also assumes the recommendations for the
still-open forks: Q10 A, Q11 as recommended, and Q14 A (all in Cut 1), and Q12 A and Q13 A (in Cut 4). A different
ruling on any of them rewrites only the piece that names it. Order: Cut 0, 1, 2, 3, 4. Each cut lands and passes
a Soul pass before the next starts.

Branch for all cuts: **Aetheria `codex/narrative-1`** from the fc12→master merge commit (call it `M`). Line
anchors are `b66ba524`, and Hands re-anchors them at `M`. The expected drift in these files is none, because
master's only extra code parent is already contained in fc12.

**Verification transport: the Aetheria LFS hook breaks `ygg-verify.sh` (v2, expanded).**

- **The break (reproduced 2026-09-29).** `ygg-verify.sh` starts with `git -C <repo> push ygg:eureka-verify/repos/<name>.git <sha>:refs/verify/<sha>`. Aetheria has a Git LFS `pre-push` hook (`.git/hooks/pre-push` runs `git lfs pre-push "$@"`, with `filter.lfs.required true`). That hook rejects the scp-style remote: `Invalid remote name "ygg:eureka-verify/repos/Aetheria.git": invalid remote name: "F:\Projects\Aetheria\ygg:eureka-verify\repos\Aetheria.git"`. The push fails before anything reaches Yggdrasil. Skipping the hook (`--no-verify`) is not an option, and editing the running script is not safe (its own EDITING note).
- **Two more obstacles behind it**, even with the push fixed:
  - `Aetheria.Shared.csproj` references CultLib and CultMath through `$(CultLibRoot)`, which defaults to `../CultLib` (`Directory.Build.props`). The pins are CultLib `45c2f4006f5f` and CultMath `6d5e2096563a`. `Directory.Build.targets` then runs `git rev-parse HEAD` and `git status --porcelain` in that root. A container clone of Aetheria has no sibling CultLib.
  - `GameData/Aetheria.cc` is an LFS object (`git check-attr filter` reports `lfs`). A plain clone or archive yields the pointer, not the catalog, so every test that opens the catalog fails.
- **Workaround: a `git archive` snapshot repo.** The Ink-only form is proven: it ran on Yggdrasil twice on 2026-09-29, exit 0. The full form is specified but not yet run.
  1. `S=<scratch>/aetheria-verify-<sha>; mkdir -p $S/Aetheria $S/CultLib`
  2. `git -C F:/Projects/Aetheria archive <sha> | tar -x -C $S/Aetheria`. Pass pathspecs to trim it, for example `Aetheria.Shared Assets/Scripts/ServerShared Assets/Plugins/Ink Assets/Plugins/UniRx GameData tests tools Directory.Build.props Directory.Build.targets`.
  3. `git -C F:/Projects/Aetheria show <sha>:GameData/Aetheria.cc | git -C F:/Projects/Aetheria lfs smudge > $S/Aetheria/GameData/Aetheria.cc`. This replaces the pointer with the real catalog.
  4. `git -C F:/Projects/CultLib archive 45c2f4006f5f7dab357426cabd3197e8d0a17f66 | tar -x -C $S/CultLib`, then `mkdir $S/CultMath && git -C F:/Projects/CultLib archive 6d5e2096563a320933b64ddfd6ded8f2e846ac91 packages/cultmath | tar -x -C $S/CultMath`. The two pins differ, and both commits exist in the local CultLib (checked). `CultMathRoot` defaults to `CultLibRoot`, so it must point at its own root.
  5. `cd $S && git init -q -b main && git add -A && git -c user.name=probe -c user.email=probe@local commit -qm "Aetheria <sha> + CultLib pin snapshot"`. The snapshot has no LFS hook, so the push succeeds.
  6. `ygg-verify.sh $S HEAD dotnet 'cd Aetheria && dotnet test tests/Aetheria.Shared.Tests -p:SkipCultLibRevisionCheck=true -p:CultMathRoot=$PWD/../CultMath'`. The skip is sound only because step 4 archived exactly the pinned revisions. Record both SHAs in the Hands report.
- **Owner.** This is a stopgap. The Idunn verify campaign owns the durable fix. The snapshot recipe dies when `idunn verify` accepts LFS repos and pinned siblings. Until then, Self should record the recipe beside `ygg-verify.sh`, not in this map.

### Cut 0. Land the corrected target

- **Repo/branch:** Aetheria `codex/narrative-1` from `M`.
- **First:** `git cherry-pick 48b4b922 a6d06d0a 53d0f304 42edc03a da9f51b9` (docs only, conflict-free; the
  last two carry `docs/narrative-cut.md`). The v2 text then replaces `docs/narrative-cut.md` in one commit.
- **Deletes first:** the target's "Machinery that already exists" section (replaced by section 3), and the
  "The gap" bullet.
- **Adds:** the section 3 text. The C8 wording fix. An "Open" list pointing at the rulings (Q1-Q9) and the
  open forks (Q10-Q14). This map lands as `docs/narrative-cut.md`, with the status header per the Eureka shape.
- **Verification:** doc-only. Negative: `rg -n "no station hull" docs/narrative-target.md` must return
  nothing.
- **Subtraction:** about -15 / +35 doc lines. No code.

### Cut 1. Story identity and all story metadata are typed catalog records; Ink carries none

v2: this cut is rewritten for the Q2 ruling ("start migrating metadata there"). It assumes Q10 A, Q11 as
recommended, and Q14 A. It replaces the v1 Cut 1, which kept placement tags Ink-owned and fixed the tag parser.
Under v2 the parser is deleted, which also closes C4.

- **Repo/branch:** Aetheria `codex/narrative-1` from `M`. Depends on Cut 0.
- **First (capture before any deletion).** Add `tests/Aetheria.Shared.Tests/NarrativeLegacyCapture.cs` and commit
  it alone. It calls the **legacy** `StoryProcessor` directly (Galaxy never does, C1) on a generated tutorial galaxy
  and a generated standard galaxy, for seeds 1-20. It records, per seed, the zone index the legacy tag parse
  picks for three inputs: `Locations/TestA.ink`, a copy of RSA's three global tags wrapped in a fixture, and one
  synthetic fixture exercising `select: DistanceFrom start` with and without `not`. It writes the result to
  `tests/Aetheria.Shared.Tests/Fixtures/Narrative/legacy-placement.txt`, a plain seed/zone table that exists only
  as test evidence. After the deletions, `MigratedPlacementMatchesLegacy` compares against it. Both TestA and RSA
  constrain to distance 0 from the entrance, so RNG order cannot perturb them. The synthetic selector fixture uses
  the ordered selector, so it does not depend on RNG either.
- **Deletes first** (lines at `b66ba524`):
  - `StoryProcessor.cs:13`: `: IZoneResolver, IFactionResolver`.
  - `StoryProcessor.cs:31-32, 51-52`: the folder fields and `CreateSubdirectory`, which writes to disk from a
    constructor.
  - `StoryProcessor.cs:55-66`: directory glob enumeration, the identity-by-folder-and-file-name path.
  - `StoryProcessor.cs:68-100`: `ResolveZone` (string anchors `start`, `end`, `home.<faction>`, and a file name),
    `ResolveLocation` (placement by file name), and `ResolveFaction` (name **prefix** match, C15).
  - `StoryProcessor.cs:117`: the dead commented `WriteAllText`.
  - `StoryProcessor.cs:127-143`: the knot-tag parse of `location` and `required`.
  - `StoryProcessor.cs:154-155`: the "infinite loop" guard. Cycles are refused statically at store open instead
    (below).
  - `StoryProcessor.cs:158-195, 202-222`: every global-tag read (`constraint`, `select`, `name`, `faction`,
    `security`, `type`, `turrets`, `namezone`).
  - `StoryProcessor.cs:227-243`: `GetContentTags`, the only tag parser.
  - `Narrative/ZoneConstraints.cs` (all 89 lines) and `Narrative/ZoneSelectors.cs` (all 50 lines). These are
    string-argument constructors over resolver interfaces, replaced by typed data plus one evaluator.
  - `Enums.cs:177-182` `LocationType`, and `ZoneGenerator.cs:208`'s `.Where(story => story.Type ==
    LocationType.Station)`. Every place is a station (C14). The index misalignment is gone because the list is
    no longer filtered.
  - `Galaxy.cs:575-586` `LocationStory`: the fields `FileName`, `Name`, `Security`, `Type` and `Turrets`.
  - Fixtures (Q5 B, Q8 A): `git mv GameData/Narrative/Locations/TestA.ink` and `Quests/TestQuest.ink` (with
    `.meta`) to `tests/Aetheria.Shared.Tests/Fixtures/Narrative/`, and strip their metadata tags. They are
    engine fixtures, not Emily's, so they may be edited. The strip is their migration: the values move into
    fixture records. The now-empty `Locations/` and `Quests/` folders go too. This moved here from v1 Cut 2,
    because once the loader refuses tags, a tagged fixture could not load.
- **Adds**, in `Assets/Scripts/ServerShared/Narrative/NarrativeData.cs` (catalog types; authored, read-only in
  game). The shape follows `ItemData`: an abstract base, one `CultDocument` per concrete kind, and MessagePack
  unions as in `Provenance.cs:92-95`.
  ```csharp
  public abstract class NarrativeData {            // base; keys 0-3 shared
      [Key(0)] public string Title;
      [Key(1)] public string[] Authors;            // credit list, never one owner slot
      [Key(2)] public string InkPath;              // relative to GameData/Narrative
      [Key(3)] public string[] SupersededTags;     // Q10 A: exact inert tag text the file still carries
  }
  [CultDocument("aetheria.place", "1"), Inspectable, MessagePackObject] public class PlaceData : NarrativeData {
      [Key(4)] public PlacementConstraint[] Constraints;   // all must hold
      [Key(5)] public ZoneSelection Selection;             // null = any candidate, uniformly (today's RandomSelector)
      [Key(6)] public CultRecordRef<Faction> Faction;      // unset = the zone's owner (today's default, :207)
      [Key(7)] public SecurityLevel Security;              // default Open (today's default)
      [Key(8)] public int Turrets;
      [Key(9)] public string ZoneName;                     // sets zone.Name AND zone.NamedZone (C13)
      [Key(10)] public string DisplayName;                 // Q11: was #planet / #name; names the station entity
      [Key(11)] public bool TradeDisabled;                 // Q11: hides the Trade tab at this station
  }
  [CultDocument("aetheria.quest", "1"), Inspectable, MessagePackObject] public class QuestData : NarrativeData {
      [Key(4)] public QuestInjection[] Injections;         // was knot tags #location / #required
  }
  [MessagePackObject] public class QuestInjection {
      [Key(0)] public string Knot; [Key(1)] public CultRecordRef<PlaceData> Place; [Key(2)] public bool Required;
  }
  [Union(0, typeof(EntranceAnchor)), Union(1, typeof(ExitAnchor)),
   Union(2, typeof(FactionHomeAnchor)), Union(3, typeof(PlaceAnchor))]
  public abstract class ZoneAnchor { }                     // was "start" | "end" | "home.<faction>" | file name
  //   FactionHomeAnchor { [Key(0)] CultRecordRef<Faction> Faction }   PlaceAnchor { [Key(0)] CultRecordRef<PlaceData> Place }
  [Union(0, typeof(DistanceFrom)), Union(1, typeof(FactionPresent)), Union(2, typeof(FactionOwns))]
  public abstract class PlacementConstraint { [Key(0)] public bool Negate; }   // was the "not" prefix
  //   DistanceFrom  { [Key(1)] ZoneAnchor Anchor; [Key(2)] Comparison Op; [Key(3)] int Hops }   Comparison { Less, Equal, Greater }
  //   FactionPresent { [Key(1)] CultRecordRef<Faction> Faction }   FactionOwns { [Key(1)] CultRecordRef<Faction> Faction }
  [Union(0, typeof(NearestTo)), Union(1, typeof(FarthestFrom))]
  public abstract class ZoneSelection { [Key(0)] public ZoneAnchor Anchor; }  // was "select: [not] DistanceFrom X"
  ```
  - `Narrative/Placement.cs`: the one evaluator. It is a pure function, `(Galaxy, PlaceData, resolved places) →
    GalaxyZone or NotPlaced(reason)`, switching over the unions the way `ItemManager.Brand` switches over
    `Provenance` (`ItemManager.cs:209-214`). An anchor that does not resolve in this galaxy (a faction absent from
    it, or an unplaced place) makes its constraint false, and makes a selection `NotPlaced`. An empty candidate set
    is `NotPlaced`, never an exception (C15). Cut 2 consumes `NotPlaced`.
  - `AetheriaStores.cs:9`: add `typeof(NarrativeData)` to `CatalogTypes`. It is routed by `IsAssignableFrom`,
    as `ItemData` is.
  - Validation in `AetheriaStores.Open`, next to `:34-47` and in the same "fails loudly, naming the item" form:
    - `Title`, `InkPath`, and at least one non-blank `Authors` entry are present.
    - Every `CultRecordRef` (`Faction`, `Place`) resolves.
    - `Hops >= 0` and `Turrets >= 0`.
    - `Injections[].Knot` is non-empty.
    - **The `PlaceAnchor` graph is acyclic**, which replaces the deleted runtime guard.
- **Per-file changes:**
  - `StoryProcessor.cs:41-53`: the constructor takes the catalog (`CultCache`) and the narrative root. It
    compiles records in stable key order: places first, then quests, with places ordered by the `PlaceAnchor`
    dependency order.
  - `StoryProcessor.cs:102-120` `GetStory(NarrativeData)`: the compile cache is keyed by record key. Includes
    resolve from the Ink file's own directory (`:15-25, :49, :112`), which is the probe-proven RSA fix. A missing
    `InkPath`, a compile error, or an unknown knot in `Injections` throws, naming the record.
  - **Tag refusal (Q10 A)** runs right after compile, as the only remaining reader of `globalTags` and
    `TagsForContentAtPath`. Every global tag and every knot-start tag must appear verbatim in `SupersededTags`,
    and every `SupersededTags` entry must be present in the file. Either mismatch throws, naming the record and
    the tag. A stale acknowledgement is refused as a lie.
  - `ProcessLocation` becomes `Place(PlaceData)`, a call to `Placement` plus construction of
    `LocationStory { Zone, Story, Place (ref), Data (resolved PlaceData), Faction (resolved: record, else zone
    owner), KnotQuests }`. `ZoneName` writes `zone.Name` and `zone.NamedZone = true` together.
  - `ProcessQuest` becomes `Register(QuestData)`: for each injection, look up the placed `LocationStory` by
    `Place` ref. A `Required` injection whose place is `NotPlaced` drops the quest and logs it (today's rule at
    `:136-140`, keyed by ref, not by file name).
  - `ZoneGenerator.cs:294-309` reads `story.Data.Security` and `story.Data.Turrets`, and sets the entity name from
    `story.Data.DisplayName` when present. `MenuPanel.cs:52` gains the Trade-tab rule: hidden when the docked
    station's `Story.Data.TradeDisabled` is set. (Q11 A; drop both if Q11 is ruled B.)
  - `Galaxy.cs:569-573` `GalaxyQuest`: adds `CultRecordRef<QuestData> Quest`.
- **Record migration** (every existing tag value, with its disposition):

  | Source | Tag | Becomes |
  |---|---|---|
  | TestA (fixture) | `constraint: DistanceFrom start = 0` | `DistanceFrom { Anchor = Entrance, Op = Equal, Hops = 0 }` |
  | TestA | `type: Station` | deleted (C14: every place is a station) |
  | TestA | `security: Open` | `Security = Open` |
  | TestA | `faction: Miss Terri` | `Faction` = a ref to the catalog `Faction` named "Miss Terri". The string occurs in the operator's working-tree `Aetheria.cc` (a byte grep only; that is the local `4b594e11` catalog). Hands confirms an exact-name faction at `M`. The capture test records what the legacy prefix match resolved to, and the ref must name that same faction. |
  | TestA | `name: Test Location 1` | `DisplayName` (Q11) |
  | TestA | `nameZone: Start Zone` | `ZoneName` |
  | TestA | `turrets: 1` | `Turrets = 1` |
  | TestQuest (fixture) | knot `A1`: `location: TestA`, `required` | `Injections = [{ Knot = "A1", Place = <TestA record>, Required = true }]` |
  | RSA (Emily; file untouched) | `constraint: DistanceFrom start < 1` | `DistanceFrom { Entrance, Less, 1 }` |
  | RSA | `planet: ReconStationAlpha` | `DisplayName = "ReconStationAlpha"` (Q11 A) |
  | RSA | `trade: disabled` | `TradeDisabled = true` (Q11 A) |
  | RSA | all three lines | `SupersededTags` = the three strings exactly as `globalTags` returns them (probe output: `planet: ReconStationAlpha`, `constraint: DistanceFrom start < 1`, `trade: disabled`) |

  The RSA record's credits are `Authors = ["Emily Harvey"]`. Its `Title` is the one piece of new text, and it is
  placement, not authorship. Q14 decides where the record lives.
- **Authority map:**
  - Owner: the `NarrativeData` catalog records (`PlaceData`, `QuestData`). They own what loads, the title, the
    credits, and every placement and injection parameter.
  - Inputs: the catalog store, and the Ink file at `InkPath` (words and knots only).
  - Outputs: `LocationStory` (with `Place` and `Data`), `GalaxyQuest.Quest`, zone names, station security,
    turrets, display name, and the Trade-tab rule.
  - Derived state: `LocationStory.Faction` is resolved from the record or the zone owner. The Ink file name is
    log text only. `GalaxyZone.NamedZone` is derived from `ZoneName`.
  - Forbidden writers: Ink tags of any kind (refused; `SupersededTags` is acknowledgement, never input). Directory
    globs. Name-prefix faction lookup. String anchors. The deleted `LocationType` filter. Git history.
  - Shared paths: new game (`MainMenu.cs:132,160` → `Galaxy` ctor), load (Cut 2), and `AetherDb`'s galaxy build
    all compile and place through `StoryProcessor` + `Placement` only.
  - Deletion line: every item under "Deletes first".
- **Verification** (new `tests/Aetheria.Shared.Tests/NarrativeTests.cs`; heavy runs on Yggdrasil through the
  snapshot transport):
  - `MigratedPlacementMatchesLegacy` pins that the migration preserves placement: typed records place into the
    captured zones for seeds 1-20.
  - `RecordIsTheOnlyAdmission` pins no record, no story: an Ink file with no record never compiles or places.
  - `CatalogRefusesUncreditedStory` pins the header invariant at the store boundary: an empty `Title` or
    `Authors` throws, naming the record.
  - `CatalogRefusesDanglingRefsAndCycles` pins store-open refusal: an unresolved `Faction`/`Place` ref, or an
    `A → B → A` `PlaceAnchor` cycle, throws.
  - `LoaderRefusesUnacknowledgedTags` pins Q10: a fixture with `#faction:` and empty `SupersededTags` throws,
    naming the record and the tag. `LoaderRefusesStaleAcknowledgement` pins that a listed tag the file lacks
    throws.
  - `RsaLoadsUneditedWithAcknowledgedTags` pins Emily's file loading as written: the real
    `GameData/Narrative/ReconStationAlpha.ink`, read-only, with a fixture `PlaceData` carrying its three
    `SupersededTags`, compiles with 0 errors and is not refused.
  - `UnresolvedAnchorIsNotPlaced` pins C15: a `FactionHome` anchor for a faction absent from the galaxy yields
    `NotPlaced` with no exception, as does an empty candidate set.
  - `ZoneNameSetsNamedZone` pins C13: the documented exclusion of named zones now holds.
  - `FactionIsExactRef` pins C15: two factions sharing a name prefix resolve to the referenced one only.
  - Negative: `rg -n 'globalTags|TagsForContentAtPath' Assets/Scripts` hits exactly one site, the refusal.
    `rg -n 'GetContentTags|ResolveFaction|IZoneResolver|IFactionResolver|LocationType|EnumerateFiles\(' Assets
    tools tests` returns nothing. Test these greps before relying on them: `LocationType` must not collide with a
    Unity or Ink type name at `M`. `git diff M -- GameData/Narrative` shows only the two fixture moves; no Emily
    file changes.
  - Operator: a Studio click-through that a `PlaceData` with a `DistanceFrom` constraint (a union-typed field) can
    be created and edited in a scratch copy of `Aetheria.cc`. **If Studio cannot author union fields, that is a
    Studio defect to raise against CultLib**, not a reason to flatten the shape.
- **Subtraction:** about -265 code (StoryProcessor about 115, ZoneConstraints plus ZoneSelectors 139, enum and
  fields about 11) against about +250 (types about 110, `Placement` about 70, validation about 40, wiring about 30).
  Net code about 0, while two parsers, two resolver interfaces, and a string DSL disappear. Tests add about 260. It
  adds two catalog schemas (`aetheria.place`, `aetheria.quest`) and removes one enum. There is no new project,
  package or target.

### Cut 2. Revive placement, and make it survive save and load

- **Depends on:** Cut 1 (Q5 and Q8 are ruled).
- **First:** a headless probe test, `RevivedPipelineOnRealGalaxies`. It runs the Cut 1 pipeline with fixture
  records on a generated tutorial galaxy and a generated standard galaxy (the `RestoredHullsTests.cs:303,393`
  construction path) across 20 seeds, and records every exception. v2: the historical question from
  locomotion-cut.md:375 ("find out why they were switched off") has to be answered against the **legacy** code.
  So Cut 1's First-step capture also records every exception the legacy `StoryProcessor` throws on those galaxies,
  before Cut 1 deletes it. The candidates are the empty-candidate throw (C15) and the null-faction `ContainsKey`
  (C15). This probe then confirms that the typed pipeline throws none of them.
- **Deletes first:**
  - `Galaxy.cs:241-243`: the commented block becomes the live call, with records passed in.
  - `LocalMenu.cs:106-110` (empty `Update`).
  - (v2: the fixture move is now in Cut 1.)
- **Adds and changes:**
  - `Placement`'s `NotPlaced` result (Cut 1) is logged, naming the record and the reason, and the place is
    skipped. Quests whose `Required` injection names it drop (Cut 1's `Register`).
  - `SavedGame.cs:142-160` `SavedZone`: add `[Key(6)] CultRecordRef<PlaceData>[] Locations`, the zone's
    placed stories in index order. `RunSave.Capture` (`SavedGame.cs:82-90`) writes it from
    `zone.Locations`.
  - `Galaxy.cs:48-92` load constructor: takes the narrative root. For each zone, it recompiles its saved
    records in order into `LocationStory` (no RNG and no constraint evaluation, because placement is
    replayed, not re-rolled). It then re-registers quests against the restored places. A saved record the
    catalog lacks throws, naming it, like `ProvenanceLedger`'s loud absent lot (`Provenance.cs:25-34`).
    `MainMenu.cs:106` and `tools/AetherDb/Program.cs:804` pass the root.
  - `EntitySerializer.cs:77`: unchanged. The index is valid because order is persisted. The test pins it.
  - Q7 A (ruled): add a run-store `aetheria.questprogress` global, mapping `QuestData` key → Ink state JSON (an
    opaque blob sealed in one typed document). It is added to `RunTypes` (`AetheriaStores.cs:10`) and written in
    the same `RunSave.Commit` batch. The load constructor restores each quest's state after re-registering it.
    A saved key the catalog lacks throws.
  - Recorded, not fixed here (C16): the injection preview (`LocalMenu.cs:85-86`) advances quest state on every
    dock, and Q7 A now persists that. Cut 4 refuses rewards on injection knots so no grant depends on it. The
    preview rebuild is a follow-up.
- **Authority map:**
  - Owner: generation owns first placement (`StoryProcessor.ProcessLocation`). The run store (`SavedZone.Locations`)
    owns placement thereafter.
  - Inputs: catalog records, the galaxy, the RNG at generation only.
  - Outputs: `GalaxyZone.Locations`, and `OrbitalEntityPack.Story` indices.
  - Derived state: after a load, `GalaxyZone.Locations` is derived from `SavedZone.Locations` and is never
    re-rolled.
  - Forbidden writers: constraint evaluation on load. Any second placement pass.
  - Shared paths: new game, Continue, and `AetherDb` galaxy load all go through the same record-to-story compile.
  - Deletion line: `Galaxy.cs:241-243` comments, `LocalMenu.cs:106-110`.
- **Verification:**
  - `PlacedPlaceGetsStoryStation`: a fixture place with `DistanceFrom { Entrance, Equal, 0 }` on the tutorial galaxy gets a
    station whose `Story` resolves to that record. Pins the chain from place to station to tab condition.
  - `PlacementSurvivesSaveAndLoad`: generate, `RunSave.Capture`+`Commit`, `new Galaxy(cache, saved, root)`,
    then unpack the zone. The station's `Story.Place` equals the original. **Negative:** the same test at `M`
    throws `ArgumentOutOfRange` (C7). Keep that failure evidence in the Hands report.
  - `UnplaceablePlaceIsReportedNotThrown`: constraints matching no zone produce a log line and no exception.
  - `QuestInjectsAtRestoredPlace`: after a load, the fixture quest's knot is registered at the restored place.
  - `NoFixtureInShippedGalaxy`: generate with the shipped catalog. No `LocationStory` comes from `tests/`.
  - `QuestProgressSurvivesSaveAndLoad`: advance a fixture quest, commit, load; the Ink state (visit counts and
    variables) is equal. Pins Q7 A.
  - `RunSaveTests` stays green. The run-record set changes only by Q7's document, and `IsRunRecord` covers it.
  - Operator (play): New Game (tutorial) → dock at the story station → the Local tab shows and the story
    runs → an injected choice appears → save, quit, Continue → dock → the same story and choice appear.
    This needs one real record that Q1 and Q5 admit. If there is none, use a fixture record in a scratch
    catalog, per the checklist's scratch-catalog practice.
- **Subtraction:** about -8 / +100 code, about +230 test. Adds one `SavedZone` key and one run schema
  (`aetheria.questprogress`).

### Cut 3. The sticky header

- **Depends on:** Cut 2 and Q6.
- **Deletes first:** `LocalMenu.cs:78` (the `Debug.Log` of the path/story kind). The header supersedes it as
  the observable signal.
- **Adds:**
  - `LocalMenu`: a header (TMP text) bound to the active story's record: `LocationStory.Place` or
    `GalaxyQuest.Quest`, both `NarrativeData` (Title, Authors joined for display). It updates wherever `_activeStory` changes (`:28`, `:64-71`, `:99`). Per Q6-A, `PresentChoice`
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

### Cut 4. Quest items: granted rewards and quest-owned story objects

v2: mapped per the Q4 ruling ("both"). It assumes Q12 A and Q13 A.

- **Repo/branch:** Aetheria `codex/narrative-1`, after Cut 3. Depends on:
  - Cut 1 (`QuestData`);
  - Cut 2 (quest progress persisted as Ink state, Q7 A; that is what makes grant-once survive a reload);
  - Cut 3 (`NarrativeCredit`, the one credit renderer).
- **First:**
  - Pin the current inspection and branding behaviour. `BrandIsDerivedFromAttributedAndProduced` covers
    `ItemManager.cs:206-222` over the existing `Attributed`/`Produced`/`Extracted` origins.
  - Pin `RunSaveTests` over a ledger holding every existing union case, so adding union slot 3 is proven not to
    disturb stored lots.
  - Record **C16**, found in this pass. `LocalMenu.PresentCurrentChoices` previews each injected quest by calling
    `quest.Story.ChoosePathString(_currentPath)` and then `ContinueMaximally()` (`LocalMenu.cs:85-86`). With
    `countAllVisits = true` (`StoryProcessor.cs:111`), merely docking increments the quest's visit count for the
    injection knot, before the player chooses anything. Under Q7 A that inflated count is persisted. So an
    injection knot cannot be a reward trigger. This cut refuses it by validation (below). Rebuilding the preview so
    it stops mutating quest state is recorded as a follow-up, not fixed here. It changes injection mechanics, which
    Cut 2 owns.
- **Deletes first:**
  - The duplicate role-fill construction. `ItemManager.CreateLot(FactionProductData)` (`ItemManager.cs:136-158`)
    hard-codes `Origin = new Attributed { ... }` (`:142`). It becomes `CreateLot(FactionProductData product,
    Provenance origin)`, and the one existing caller (`CreateInstance(FactionProductData)`, `:191-201`) passes
    `new Attributed { Faction = product.Manufacturer }`. Rewards then reuse the role-fill loop instead of copying
    it.
  - `Provenance.cs:90-91`: the stale comment "Terminus mints Attributed placeholders; Produced and Extracted are
    the forward shape". It is replaced by a comment naming all four cases.
- **Adds:**
  - `Provenance.cs:92-95`: `Union(3, typeof(Granted))`, with
    `[MessagePackObject] public class Granted : Provenance { [Key(0)] public CultRecordRef<QuestData> Quest;
    [Key(1)] public CultRecordRef<Faction> Faction; }`. `Faction` is the maker for a product reward, so the brand
    survives. It is unset for a story object, which has no brand.
  - `ItemData.cs:273-285` (abstract base): `[Key(33)] public CultRecordRef<QuestData> Quest;`. This is the
    story-object link (a design owned by a quest). Census at `b66ba524`: `ItemData.cs` uses keys 0-16 and 18-32,
    and every `ItemData` subclass lives in that file, so 33 is the next free key. Hands confirms nothing retired
    names 33 at `M`, and does not reuse Key 3 (retired, `:281-282`). It is optional in MessagePack, so existing
    catalog records read it as unset. No catalog rewrite is needed.
  - `NarrativeData.cs` `QuestData`: `[Key(5)] public QuestReward[] Rewards;`, where
    `QuestReward { [Key(0)] string Knot; [Key(1)] RewardSource Source; }` and `RewardSource` is a union:
    `ProductReward { [Key(0)] CultRecordRef<FactionProductData> Product; [Key(1)] int Count }`, or
    `DesignReward { [Key(0)] CultRecordRef<CraftedItemData> Design; [Key(1)] float Quality; [Key(2)] int Count }`.
    Rewards are crafted items only, because only crafted items carry a lot, and so only they can carry `Granted`.
    Commodity rewards are out of scope.
  - `ItemManager`: one primitive, `int CreateGrantedLot(QuestData quest, RewardSource source)`. For a product it
    calls `CreateLot(product, new Granted { Quest, Faction = product.Manufacturer })`. For a design it builds the
    lot with `Quality` and `Granted { Quest }`. Every quest-item lot is minted here.
  - `ItemManager.Brand` (`:209-214`): adds the arm `Granted g => g.Faction`.
  - `Narrative/QuestGrants.cs`: `Step(GalaxyQuest quest, Action<Story> mutate, IEnumerable<EquippedCargoBay> shipBays,
    EquippedDockingBay dockedBay)`.
    - It snapshots `VisitCountAtPathString` for the quest's reward knots, runs `mutate`, and then, for each knot
      that went from 0 to at least 1, mints `Count` instances through `CreateGrantedLot` and `CreateInstance`.
    - It stores each instance in the first ship bay with room (`TryStore`, `Entity.cs:1881`). Otherwise it goes
      into the docked bay (Q13 A), which persists through `EntitySerializer.cs:40`. If neither has room, it throws
      naming the quest and the knot. A silent loss is not allowed.
    - "Already granted" is derived from Ink's persisted visit counts (Q7 A). There is no second ledger.
  - `LocalMenu`: every mutation of a **quest** story is routed through `QuestGrants.Step`. That covers
    `Continue` (`:46`), `ChoosePath` (`:100`), and the injected-branch entry (`:99-101`). The preview at `:85-86`
    is deliberately not routed. It cannot grant, because validation forbids rewards on injection knots.
  - `PropertiesPanel.AddItemProperties` (`PropertiesPanel.cs:361-366`): before `AddProperty(data.Description)`,
    render the quest header through Cut 3's `NarrativeCredit`. The quest is `data.Quest` if set, else the lot's
    `Granted.Quest`, else none. Both inspection entry points pass through this method (`Inspect(EquippedItem)`
    `:439-446` and `Inspect(ItemInstance)` `:507`), as do the Inventory and Trade menus (`InventoryMenu.cs:98,152`,
    `TradeMenu.cs:316`). So the header is on every inspection surface, with one call site.
  - Validation in `AetheriaStores.Open`, next to Cut 1's:
    - Every reward ref resolves, and `Count >= 1`.
    - A `DesignReward` naming a quest-owned design is only allowed in that design's own quest.
    - **No `FactionProductData` names a quest-owned design.** Random generation and markets draw only through
      products (`LoadoutGenerator.cs:126`, `Loadout.cs:100`), so this makes a story object unmintable outside its
      quest by construction.
    - A `ProductReward`'s design is therefore never quest-owned.
    - Every `Rewards[].Knot` exists in the quest's Ink (checked at compile, as Cut 1 checks injection knots) and
      is **not** an injection knot (C16).
- **Authority map:**
  - Owner: `QuestData.Rewards` owns what a quest grants, and when. `ItemData.Quest` owns which designs are
    story objects. `CreateGrantedLot` is the single minter of quest-item lots.
  - Inputs: catalog records, and the quest's Ink visit counts.
  - Outputs: lots with `Granted` origin in `ProvenanceLedger`, instances in cargo or the docked bay, and the
    inspection header.
  - Derived state: "already granted" is derived from Ink visit counts. The inspection header's quest is derived
    from `ItemData.Quest`, else `Granted.Quest`. Brand is derived from `Granted.Faction`.
  - Forbidden writers:
    - Ink text (no `EXTERNAL`, Q12 A).
    - UI code minting lots directly.
    - Any `FactionProductData` for a quest-owned design (refused).
    - The injection preview (it cannot reach a reward knot).
    - The debug console `give` (`ActionGameManager.cs:522-526`) stays a dev-only writer, minting `Attributed`.
      A story object minted there still shows its quest header via `ItemData.Quest`. It is out of the invariant
      by being a dev tool, and it is named here so no one mistakes it for a path.
  - Shared paths: the direct choice path and the continue path both go through `QuestGrants.Step`. So does reload:
    visit counts restore from the run store, so re-entry after Continue cannot re-grant.
  - Deletion line: the hard-coded `Attributed` in `CreateLot(product)` (`ItemManager.cs:142`), and the stale
    comment at `Provenance.cs:90-91`.
- **Verification** (headless, in `NarrativeTests.cs` and `RunSaveTests.cs`; on Yggdrasil through the snapshot
  transport):
  - `RewardGrantedOnFirstVisitOnly` pins grant-once, with visit counts as the owner: stepping into a reward knot
    grants `Count`, and stepping into it again grants nothing.
  - `RewardNotRegrantedAfterReload` pins that the reload path shares the owner. Grant, `RunSave` commit, load
    (Cut 2), and re-enter the knot: no second grant, and the ledger holds exactly one `Granted` lot per grant.
  - `GrantedLotKeepsBrand`: a `ProductReward` lot's `Brand` returns the product's manufacturer and product. Pins
    the `Granted.Faction` arm.
  - `StoryObjectHasNoProductAndNoBrand` pins unmintable-outside-quest: a quest-owned design with a
    `FactionProductData` naming it makes `Open` throw, and its granted lot brands as `(null, null)`.
  - `RewardOnInjectionKnotRefused` pins C16: validation throws, naming the quest and the knot.
  - `FullCargoFallsToDockedBay` pins Q13 A: ship bays full means the item lands in the docked bay and persists
    through `PackEntity`. Both full throws.
  - `GrantedSurvivesLedgerPrune`: `ProvenanceLedger.Reachable` keeps a `Granted` lot reachable from a stored
    instance, and enqueues nothing for it (it has no inputs). Pins `Provenance.cs:38-58` over the new case.
  - `UnionSlot3DoesNotDisturbStoredLots`: the First-step ledger fixture round-trips unchanged.
  - Negative: `rg -n 'new Granted' Assets/Scripts` hits only `ItemManager.CreateGrantedLot`.
    `rg -n 'CreateGrantedLot' Assets/Scripts/UI` returns nothing. `rg -n 'EXTERNAL' GameData/Narrative` returns
    nothing new.
  - Operator (play):
    - Dock at a fixture quest station, take the branch to the reward knot, and see the item appear in cargo.
    - Inspect it: the quest header (title and authors) shows above the description, with the manufacturer line
      intact.
    - Save, quit, Continue, re-enter the knot: no second item.
    - Fill cargo and repeat on a second reward: the item appears in the station bay.
    - Inspect a story object: the quest header shows, and there is no manufacturer.
- **Subtraction:** about -8 / +150 code (`Granted` about 10, `ItemData.Quest` about 3, rewards types about 30,
  `CreateGrantedLot` about 20, `QuestGrants` about 50, `LocalMenu` routing about 10, header about 10,
  validation about 20). Tests add about 250. It adds one union case, one `ItemData` key, and one `QuestData` key.
  There is no new schema, project or target. The positive delta buys the ruled capability (Q4 "both"). No existing
  owner carries quest origin, so reuse was limited to `CreateLot`'s role fill and `Brand`'s switch.

### Out of this map

- Content: the curated opening and the widening galaxy. Records for Emily's files (Q14 for RSA) and any new
  quests are content decisions under Q1 A and Q5 B. Extensions of her premise are separate records crediting
  their actual authors.
- The injection preview mutating quest state (C16). It should preview without advancing the quest's story (for
  example, reading choices from a state copy). That changes injection mechanics. Cut 4 only fences it.
- `Thread.Sleep(500)` at `Galaxy.cs:246` ("to make it seem like it's doing more work"). Unrelated. Note it for a
  separate sweep.
- The verification transport (stopgap). It belongs beside `ygg-verify.sh` and in the Idunn verify campaign, not
  in this map.

## 6. Subtraction ledger (estimate, v2)

| Cut | Code − | Code + | Test + | Schemas / types / targets |
|---|---|---|---|---|
| 0 | 0 | 0 | 0 | none (doc about ±600, including this map) |
| 1 | ~265 | ~250 | ~260 | +2 catalog schemas (`aetheria.place`, `aetheria.quest`); -1 enum (`LocationType`); -2 files (`ZoneConstraints.cs`, `ZoneSelectors.cs`); -2 interfaces |
| 2 | ~8 | ~100 | ~230 | +1 `SavedZone` key; +1 run schema (`aetheria.questprogress`) |
| 3 | ~1 | ~45 | ~40 | 1 prefab edit |
| 4 | ~8 | ~150 | ~250 | +1 `Provenance` union case; +1 `ItemData` key (33); +1 `QuestData` key |

Code is net additive by about 270 lines across the campaign, and Cut 1 is roughly net zero. The capability it
buys is ruled: credited, placed, persisted narrative, and quest items, none of which exists today. Cut 1 buys its
schemas by deleting two parsers, two resolver interfaces, a string placement DSL, and the dead
`LocationType`/`Name`/`FileName` fields. Identity has no current owner to reuse, and the only existing carrier
(Ink tags) cannot reach Emily's files.

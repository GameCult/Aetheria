# Aetheria electronic warfare: target

Campaign `aetheria-ew`, target revision r2. Repo `GameCult/Aetheria`, working branch `master`, cut branches
`eureka/aetheria-ew-<cut>`. Written by Imagination (agent imagination-aeth-ew-target, session
self-2026-10-13-ag) against `origin/master` `e89d38e9`, with `origin/eureka/aetheria-release-reveals-replace-tiers`
read where master lacks it. Revision r2 (agent imagination-aeth-ew-r2, same session) folds the eight operator rulings
of 2026-10-13 (`aetheria-ew:ruling:<name>-ruled`, cited below by `<name>-ruled`); master now holds
reveals-replace-tiers, so probe P1's gap is closed. Prior art and lore: `docs/research/ew-prior-art.md` (cited below as `R:<line>`).
This is the target and the model page. No cut is specified here; the first cut is mapped when it is about to run.

## Purpose, in the operator's words

> I think of it as envelope shaping, which is making your emissions tell the wrong story. Masking your signal is
> one thing, but looking like something you're not is another. If you can match your cross section to the
> asteroids you're hiding in, you can be detected and it doesn't matter. When you're hot, EW means those emissions
> lie about your gear, or hide a launch, or make it look like you're somewhere else via heat tenders or space
> magic, I don't know, get creative. What do you think?

(ruling `aetheria-release:ruling:ew-is-envelope-shaping`, 2026-10-13)

> Remembered bearing? Does that mean we already have positional ambiguity for detection? That was going to be an EW
> feature. ... Decoys are also EW. How about we just map the EW campaign

(ruling `aetheria-release:ruling:ew-owns-ambiguity-and-decoys`, 2026-10-13)

The lore says the same thing from the inside: signature shaping is "not invisibility. It is false classification
bounded by heat capacity, emitter bandwidth, aspect, thrust history ..." (R:86), and in the novella "Every false
signature consumed real heat and closed a later option" (R:90).

## Self's reading, tested against the Body and the lore

The operator asked "What do you think?". Each point of Self's reading is adopted, amended or rejected here, with
the reason. The forks that needed the operator are now rulings (listed at the end).

1. **The lie lives in emissions; each observer's own Sense derives belief; EW never writes an observer's book.**
   *Adopted, and sharpened.* The Body already has exactly this shape: a ship's light is written only by its own
   `Entity.SetSignature` (Entity.cs:98-103), its active emissions only by `Entity.Emit` (:901), and an observer's
   book only by its own `RecordReveal` (:922, callers `Sense` and `PublishInbox`). EW adds a third thing a ship
   emits, its *claim*, written by its own EW gear, and a judge inside the observer's `Sense` that decides whether
   the claim is believed. Amendment: the judge is one function, used by `Entity.Sense` and later by a missile
   seeker's own reading, so a seeker is fooled by the same rules a ship is (follow-up `missile-decoys-signature`).

2. **A reveal records what the emission claims, not the true item; truth stays in the sim.** *Amended.* The book
   keeps the true item as its key, because the key is the sim's handle for aiming and for ending a reveal on
   perceived destruction (`PerceivesDestroyed`, Entity.cs:932). What changes is what the book *says*:
   - `Reveal` gains `Claimed` (an `EquippableItemData`): the class the act claimed, the true `Item.Data` when the
     ship tells no lie about that item. `RecordReveal(subject, item, channel)` takes the claimed class from the
     signature or emission that carried the act; `PublishInbox` takes it from the paint.
   - `Contact` gains `Profile` (the hull class, or background kind, the observer believes) and `SeenThrough`.
     `Identified` keeps its gate (track at `TargetGearInfoThreshold`) but now means "holds a hull belief", and the
     believed hull is the claim unless the observer has seen through it. Nothing reads `subject.Hull` to classify.
   - `IsRevealed` (the aiming read, reveals-replace-tiers `FireControl.IsRevealed` = live item fitted and whole and
     `HasReveal`) is unchanged for a gear lie: the observer aims at the real item that acted, believing it is the
     claimed class. The shot lands on what emitted; the lie misleads the choice, not the aim.
   - Under a believed *hull* lie, the observer's layout is the claimed hull's, so it cannot place the true item:
     ruling `ew-aim-under-hull-lie-ruled`: the observer can target the ship but no gear on it until the hull lie is
     seen through.
   - `Reveal.Item` stops being a classification source: consumers that label or rank (HUD, schematic, AI threat,
     point defence choice) read `Claimed` and `Profile`. `Item` stays for `IsRevealed` and the book's own gates.

3. **Verbs: blend, masquerade, hide a launch, displace.** *Adopted with three amendments.*
   - *Blend* needs a background to blend into. Asteroids are chunks, not entities (Zone.cs:453-527); they are never
     contacts. So blend is a claim of a *background kind* (rock, debris), believed only inside a belt where real
     chunks are near and only while the ship's light stays inside that kind's envelope (no drive plume, thermal and
     reflection no brighter than chunks there, `Zone.ChunkVisibility`). A contact believed to be background is
     detected and tracked, but it is clutter: not an enemy, not a target (operator: "you can be detected and it
     doesn't matter"). This needs allegiance to come from belief, not truth (ruling `ew-hostility-from-belief-ruled`). A believed
     background contact is shown as a faint background marker labelled as the kind, not hostile (ruling
     `ew-blend-presentation-ruled`).
   - *Masquerade* is two claims: a hull profile (the whole ship claims another hull class, and optionally shows
     another faction's telemetry) and item claims (one fitted item's acts claim another item class). The envelope is
     checked by each observer against what it perceives: drive plume and acceleration no greater than the claimed
     hull can make, thermal and cross-section within the claimed hull's band, a launch or a fired weapon that the
     claimed hull could carry. Acting outside the profile breaks the lie for that observer.
   - *Hide a launch* is the cheapest verb and needs no claim: a suppressor scales its own ship's `LaunchFlash`
     strength down inside `SetSignature`, paying heat. It is also how the pre-Cold-Wake tier hides.
   - *Displace* is not a false position. It is a real body somewhere else: a heat tender is an emitter body (an
     `Entity`) launched from the ship, carrying heat taken from the ship's cells and a claim that names the ship's
     profile, while the ship itself goes cold. A decoy is the same kind with a different load (bright, short-lived,
     a claim of whatever it imitates). One kind serves both, and the follow-up `missile-decoys-signature` asks for
     exactly this ("a decoy record or entity with Visibility, offered to the seeker scan"). The exotic Cold Wake
     imitation ("space magic") stays a later tier: EW lies about what, never where, and phantom positions with no
     emitter are their own later campaign (ruling `ew-where-lies-ruled`).

4. **The envelope bounds every lie and costs heat; lies break on envelope violation, multi-aspect observation,
   illumination, or heat exhaustion.** *Adopted; the quantities are mostly ones the Body already owns.*
   - *Heat* is not a new EW budget. Every lie adds heat to the EW item's own cell through the existing `AddHeat`
     (Entity.cs:1208), as every behaviour already does; that heat conducts, shows later as hull radiation (item-less
     Thermal, Entity.cs:1849) and radiator Thermal (Radiator.cs:121). The lore's heat debt (R:86) falls out of the
     thermal sim with no second owner. *Exhaustion* is the EW item's rated temperature (a gear stat): above it the
     behaviour stops writing its claim, exactly as `Radiator` stops below its floor.
   - *Bandwidth* is a gear stat: which profiles the gear can claim, as a window around the true hull (mass and
     size ratio) and a list of background kinds; a suppressor's channels and factor.
   - *Aspect* is geometry, not state: a masquerade emitter has an arc; an observer outside it sees the truth. This
     is how multi-aspect observation breaks a lie (the novella's "Proper Hot", R:90) with no extra rule.
   - *Thrust history* is read from what the observer perceives now (plume strength and acceleration while
     tracked). Recommended default: instantaneous, no history kept. A short history is a later refinement only if
     play shows the instantaneous check is too easy to game.
   - *Illumination* (burn-through, R:102, R:127): an observer whose own ping or lock paint crosses the subject with
     power over range above the lie's masking strength sees through. The illuminator pays as today: it is heard and
     the target is warned (rulings `scan-is-an-act`, `sensor-stat-set`). An illuminator's burn-through is
     shared with the allies receiving its ping return; a passive see-through (arc, envelope break) stays with the
     observer (ruling `ew-seen-through-sharing-ruled`).

5. **Noise jamming is in, as a crude loud tier; phantom items deferred.** *Amended by the operator* (ruling
   `ew-noise-jamming-ruled`: "sounds like an obvious thing to have"). A jammer adds exactly one term to
   `Sensor.Gain`, the one place track gain is computed: inside the jammer's cone, other ships' active-sensor gain is
   thinned, as the vapour cloud's obscuration thins it. The term is owned by the jammer's heard emission and cone, so
   it is not a second sensor model. The jamming is itself an emission heard far (`Emit`), which reveals its user and
   invites home-on-jam (R:130), as an illuminator is revealed. The research warns that games leading with noise
   jamming end as lock denial (EVE R:111, Starsector R:109); the loud, heard, cone-bound tier keeps the jammer a
   gamble with a signature rather than a switch. Full jamming (spot and barrage, J/S ratios, burn-through ranges)
   is not in this campaign unless the operator widens it. Phantom items (claims of gear the ship does not carry) are
   deferred: an item claim is always attached to a real fitted item that acts.

6. **Era tiers from lore.** *Amended: tiers are gear, not an era flag.* The lore's line (R:86: "Before the Cold Wake
   metamaterial threshold, actors hide, delay, dump, and throw crude decoys. After it, ships ... imitate") becomes a
   gear line. Probe P7 shows most of the early tier already exists: radiators off (`Thermotoggle`), heat held
   (`HeatStorage`), vapour dump (`VapourDump`). The early tier EW adds is the launch suppressor and the crude decoy
   (an emitter body with no claim). The late tier is masquerade and blend gear. Ruling
   `ew-imitation-availability-ruled`: both tiers ship; imitation gear is rare and made by a few makers whose identity
   fits, so most fights see hiding, crude decoys and jamming, and a masquerading ship is an event. The early tier
   (suppressor, crude decoy, jammer) is not scarce.

7. **Crew refusals and the political cost of jamming.** The sim has no crew model (Entity.cs:85-86, `Population`
   and `Personality` are commented out). Lore only in this campaign, no mechanic (ruling `ew-crew-refusals-ruled`).

## Design truths (ends, not means)

- A ship can be seen and still not be understood. Detection, tracking and classification stay separate, and EW
  acts on classification (R:86 "Detection is faster than identification").
- Every lie is a physical act with a price paid in heat, and it buys time, not safety: it ends when the liar acts
  outside its story, when someone looks from another side, when someone spends their own signature to illuminate,
  or when the heat runs out.
- What a player or an AI believes is what its own instruments told it. No consumer is quietly told the truth.
- Learning the truth is earned the same way capabilities are revealed now: by forcing the target to act
  (ruling `visibility-per-capability-reveal`).
- EW is legible to the liar: a ship knows what it is claiming, what it costs per second and what will break it.

## Invariants (to become the typed target)

- `belief-one-writer`: An observer's belief about a subject (book, claim belief, seen-through) is written only by
  that observer's own sensing (`Entity.Sense` and `PublishInbox`; a seeker's own reading through the same judge).
  No EW gear, presenter, AI or other entity writes it.
- `lie-is-own-emission`: EW acts only on what its own ship emits. Light stays written only by `SetSignature`,
  emissions only by `Emit`, and a ship's claim only by that ship's EW behaviour.
- `one-judge`: Whether a claim is believed is decided by one function of (claim, what the observer perceives of the
  subject, geometry, the observer's own illumination), shared by ships and seekers.
- `what-not-where`: Every position the sim exposes is the true position of a real entity; EW lies about what a
  contact is, not where an entity is. Displacement is a real emitter body elsewhere. (Ruling
  `ew-where-lies-ruled`.)
- `classify-from-belief`: Every consumer that labels, ranks or chooses (HUD, schematic, AI target choice, point
  defence choice, seeker) reads `Contact.Profile` and `Reveal.Claimed`; none reads `subject.Hull`, the true item's
  data or the subject's true faction to classify. Physics, damage and aiming keep the truth.
- `envelope-bounds-claim`: An observer believes a claim only while what it perceives of the subject lies inside
  the claimed profile's envelope, within the gear's bandwidth, and only from inside the gear's arc.
- `lies-cost-real-heat`: Every lie adds heat to its EW item's cell through `AddHeat`; there is no separate EW
  budget; an EW behaviour writes no claim above its rated temperature.
- `illumination-burns-through`: An observer whose own ping or lock paint crosses a subject with power above the
  lie's masking strength sees through it; the act reveals the illuminator as it does today. An illuminator's
  burn-through is shared with the allies receiving its ping return (ruling `ew-seen-through-sharing-ruled`); a
  passive see-through stays with the observer.
- `seen-through-holds-while-tracked`: A seen-through verdict holds while the observer keeps the subject tracked or
  remembered (`BearingMemory`), and ends with the contact. A new lie fools a fresh contact, not a held one.
- `decoy-is-emitter-body`: Decoys and heat tenders are one kind, a real `Entity` with its own signatures and a claim;
  seekers, point defence and the HUD treat it by belief, with no decoy-specific rule.
- `present-model-stays`: The exact track, bearing-only reveals remembered for `BearingMemory`, and range only where a
  channel gives it, are unchanged (ruling `ew-owns-ambiguity-and-decoys`). `Sensor.Gain` gains exactly one jamming
  term, owned by the jammer's heard emission and cone like the cloud's obscuration (ruling
  `ew-noise-jamming-ruled`); EW adds no other term to it.

## Not in scope

- Not EW: the two-listener fix (follow-up `two-listener-fix`) estimates a true position from true bearings; it stays
  in faction play. It is consistent with EW because a tender's bearings are a real body's bearings.
- Not EW: lock growth, hit odds and evasion (`FireControl`, `LockWeapon`). A lie about class does not change a
  firing solution on a tracked ship; a decoy changes odds only by being the thing chosen.
- Not EW: the vapour cloud's obscuration (ruling `vapour-cloud-obscuration`). The follow-up
  `cloud-vs-missiles-in-flight` is satisfied by missile-records' seeker reading through `PassiveRate`, which already
  multiplies `Zone.Obscuration` (Sensor.cs PassiveRate); EW owns only the pinning test, in the seeker cut.
- Not a consumer: AetheriaEve/AetheriaThing (archived).
- Not in this campaign: false positions or bearings with no emitter (`ew-where-lies-ruled`), error ellipses, phantom
  gear, crew refusals as mechanics (`ew-crew-refusals-ruled`), comms jamming, full jamming (spot and barrage, J/S,
  burn-through ranges: only if the operator widens `ew-noise-jamming-ruled`), persistent EW state in saves.

## Model page: identity, lifecycle, authority

None of these persist across a save: they are runtime state rebuilt from behaviours each tick, or observer state
cleared by `Deactivate`, except the catalog stats.

| Kind | Identity (what names it) | Lifecycle (what happens to it) | Authority (who decides; forbidden writers) |
|---|---|---|---|
| **Ship claim** (new) | One per ship, held on the subject `Entity`: a `Profile` (a hull's `HullItemData`, or a `BackgroundKind`) and an optional shown `Faction`. Namespace: the ship. Absent means the truth shows. Injective by construction (one slot per ship). | Written each tick its EW behaviour runs and is under its rated temperature; absent the tick it stops (switched off, too hot, destroyed, unpowered). Never stored, never replayed. | The ship's one active masquerade or blend behaviour. If two are fitted, the first in equipment order that runs (default; a fitting rule may forbid two). Forbidden: any other entity, presenters, the AI of other ships, the observer. |
| **Item claim** (new) | `(subject, item)`, carried inside the act: a `Claimed` class on `Signature` and on `ActiveEmission` and `PaintWarning`'s source. Defaults to `item.Data`. | Exists as long as the signature or emission it rides (one tick for emissions; until the source's next write for light). | The subject's EW behaviour sets the item's claim; `SetSignature` and `Emit` stamp it, so the one writer of light and emission stays the one writer of what they claim. Forbidden: the emitting behaviours choosing their own claim (a gun does not lie by itself). |
| **Channel suppression** (new) | `(ship, channel)` factor, 0..1, on the subject. | Set each tick a suppressor runs; 1 otherwise. Applied inside `SetSignature` to that channel's strength. | The ship's suppressor behaviour. Forbidden: weapons scaling their own flash; observers. |
| **Reveal** (changed) | Unchanged key `(observer, subject, true item)`; gains `Claimed`. | As today: entered by an act, replaced by the item's later act, ended on perceived destruction, departure or memory age, cleared by `Deactivate`. The claimed class is the latest act's. | `RecordReveal` only (callers `Sense`, `PublishInbox`). Readers that classify read `Claimed`; `Item` is read only by `IsRevealed` and the book's gates. |
| **Belief** (new) | `(observer, subject)` in the observer's book beside its reveals: believed `Profile`, believed `Faction` (ruling `ew-hostility-from-belief-ruled`: allies by datalink are truth; any other contact's allegiance is the claim's, unknown when none, background when blending), `SeenThrough`, time. | Created when the observer first identifies the subject or receives its claim; revised each `Sense` from the subject's current claim and the judge; `SeenThrough` set by an envelope break, an out-of-arc view or own illumination; ends with the contact (track lost and `BearingMemory` past), on departure, or on `Deactivate`. A new claim from a seen-through subject is ignored until the contact ends. | `Entity.Sense` through the one judge. Forbidden: EW gear, presenters, AI, allies, except that an illuminator's burn-through reaches its ping-return allies through `PublishInbox` (ruling `ew-seen-through-sharing-ruled`). |
| **Contact** (changed) | Derived `(observer, subject)`, never stored. Gains `Profile`, `SeenThrough` (and allegiance per `ew-hostility-from-belief-ruled`). | Derived on every read. | `Entity.ContactOf` only. |
| **Own illumination** (new, transient) | `(observer, subject)`, this tick: the power over range of the observer's own ping crossing or lock paint on the subject. | One tick: written in `Update`, read by `Sense`, cleared by `PublishInbox`, as `_emissions` is now. | The observer's own `Sensor` and `LockWeapon` write it on their own entity. Forbidden: the subject; other entities. |
| **EW gear stats** (new) | Fields on new `BehaviorData` types with append-only MessagePack keys: masquerade (bandwidth window, background kinds, arc, masking strength, heat per second, rated temperature), suppressor (channels, factor, heat per unit suppressed), deployer (body design, heat drawn, launch speed). | Catalog edits through AetherDb; resolved per item by the entity's stat owner (Cut 2). | The catalog. Forbidden: runtime writes. |
| **Background kind** (new) | Append-only enum (`Rock`, `Debris`, more by ruling) with an envelope per kind in `GameplaySettings`: plume ceiling, light relative to nearby chunks, the belt-proximity radius. | Static data. | `GameplaySettings`. Forbidden: per-zone overrides (default; revisit if a zone needs one). |
| **Emitter body** (new) | An `Entity` instanced from a catalog design, admitted through Zone's one admission (Zone.cs:208); it names its `Parent` (the deploying ship). Its claim defaults to its parent's profile. | Created by a deployer behaviour, carrying heat drawn from the parent's cells; lives on its own heat and fuel; removed through the zone's removal path (a `Departure`, so observers judge its end) when spent or destroyed. Not saved. | Its own behaviours write its signatures (its real heat is its signature); the deployer decides creation; the zone owns admission and removal. Forbidden: observers, the parent after release. |
| **Jammer** (new) | Gear `BehaviorData` (cone half-angle and facing, power, gain factor, heat per second, rated temperature) on a ship; its output is `(ship, cone, power)`, this tick. | Runs each tick under its rated temperature: adds heat to its own cell through `AddHeat`, emits a heard jamming emission through `Emit` (heard far: it reveals the user and invites home-on-jam), and its cone-and-power thins `Sensor.Gain` for other ships' active sensors inside the cone, the one gain term. Silent above rated temperature. Not saved. | The jammer's own behaviour owns the emission and the cone; `Sensor.Gain` only reads them, as it reads `Zone.Obscuration`. Forbidden: any other writer of the jamming term; observers; the jammer writing a sensor's state directly. |
| **Heat** (existing) | Per-cell `Temperature` on the hull grid. | Unchanged. | The thermal sim; EW behaviours are ordinary `AddHeat` writers. No new owner. |

Recommended defaults, not questions: the emitter body is a `Ship` from a catalog design rather than a new `Entity`
subclass (it reuses hull, thermal grid, radiators and thrusters; the two existing subclasses are `Ship` and
`OrbitalEntity`); thrust history is instantaneous; a seen-through verdict holds for the life of the contact;
background envelopes live in `GameplaySettings`.

## Probes run

| # | Question | Result |
|---|---|---|
| P1 | Does master hold reveals-replace-tiers? | No. Master `e89d38e9` holds emissions-heard (`3c628f54`); reveals-replace-tiers is 3 commits ahead on its branch (merge base `642a61eb`), replacing `IsRevealed`'s info tiers with `HasReveal` plus live gear. This doc reads that branch's `IsRevealed`. |
| P2 | Who writes light, emission and the book? | `SetSignature` (Entity.cs:98-103, refuses Emission), `Emit` (:901), `RecordReveal` (:922, callers `PublishInbox` :884 and `Sense` :996, :1007). One writer each, so a claim can ride them. |
| P3 | Does anything read `ContactOf` or `RevealsOf` outside Entity? | No product code; only `ContactRevealTests`. contact-hud r2 moves presenters onto them, so EW lands one read site after it. |
| P4 | Is allegiance of a tracked contact truth? | Yes: `EntityHostility[e] = IsHostileTo(e)` on zone entry (Entity.cs:266, :272), and track entry files the entity into `VisibleEnemies` or `VisibleFriendlies` by it (:313-330). Minion targets the first visible enemy (Agents/Minion.cs:14), TurretController the first visible enemy ship (:98), the HUD draws hostile indicators from `VisibleEnemies` (ActionGameManager.cs:1106). Blend cannot work while this stays truth. |
| P5 | Does FireControl read true positions? | Yes, 19 `.Position` reads (e.g. FireControl.cs:237, :257, :295). Consistent with `what-not-where`; a false position would need a second position owner. |
| P6 | What can be blended into? | Chunks are not entities (Zone.cs:453-527: `ChunkExists`, `ChunkVisibility`, `ChunkPose`); only `Ship` and `OrbitalEntity` are entities. Background must be a claimed kind judged against nearby chunk light. |
| P7 | What of the early tier exists? | `Thermotoggle`, `HeatStorage`, `VapourDump`, `Radiator` are behaviours now; `LaunchFlash` is written only by `InstantWeapon` (:280) and `ConstantWeapon` (:173); no suppressor, decoy, jammer or spoof exists. |
| P8 | Is heat a real cost? | Yes: per-cell `Temperature` and `ThermalMass` (Entity.cs:1165-1213); hull radiation is item-less Thermal from cell temperatures (:1849). An EW `AddHeat` is visible later as thermal with no new state. |
| P9 | Do seekers exist to fool? | No: `MunitionSeekerData` is data only (Behaviors/MunitionParts.cs:60-125); a missile in flight is a Unity `GuidedProjectile`. Seeker EW waits on missile-records. |

## Follow-ups this campaign takes

- `missile-decoys-signature`: answered by the emitter body plus `classify-from-belief`; point defence and the HUD
  treat a decoy as what they believe it is. The Nebulous "turrets take decoys first" note is second-hand and
  unconfirmed (R:106); no decoy-first rule.
- `cloud-vs-missiles-in-flight`: a pinning test in the seeker cut, riding missile-records' reading.
- `two-listener-fix`: stays in faction play (not EW).

## Prior art that shaped this

Nebulous: Fleet Command treats jamming as noise against radar with burn-through (R:100, R:102), track quality and
error per sensor (R:101), seekers fooled by decoys matching a ship's signature (R:105, R:106). Star Citizen's
design note has decoys that broadcast another ship's signature but cannot fake cross-section (R:119): a lie bounded
by an envelope, as here. Sea Power gives each seeker a chance to reject a false target and each decoy a tier that
works against it (R:115), the shape of masking strength against illumination. EVE and Starsector make EW an
aggregate lock or range penalty (R:109, R:111), which is the masking end the operator set aside. Real-world
vocabulary: deception jamming and DRFM change apparent range, velocity or angle (R:126, R:128), the false-position
family this target defers; burn-through range (R:127); a self-protecting jammer reveals itself to home-on-jam
(R:130), as an illuminator does here.

## Rulings folded (campaign aetheria-ew, 2026-10-13)

`ew-where-lies-ruled`, `ew-hostility-from-belief-ruled`, `ew-blend-presentation-ruled`,
`ew-seen-through-sharing-ruled`, `ew-aim-under-hull-lie-ruled`, `ew-noise-jamming-ruled`,
`ew-imitation-availability-ruled`, `ew-crew-refusals-ruled`.

## Cut sequence (titles only; each is mapped when it is about to run)

1. `ew-hide-launch`: a suppressor scales its ship's launch flash inside `SetSignature`, paying heat to its cell.
   Waits on: none (master and reveals-replace-tiers are in); early tier, so not scarce
   (`ew-imitation-availability-ruled`).
2. `ew-claims-in-the-book`: `Reveal.Claimed`, `Contact.Profile` and `SeenThrough`; classifying consumers read them;
   claims default to the truth, so behaviour is unchanged. Waits on: reveals-replace-tiers, contact-hud.
3. `ew-belief-allegiance`: enemies and friendlies derive from belief for non-allies. Waits on: cut 2.
4. `ew-masquerade`: masquerade gear writes ship and item claims; the one judge with envelope and arc checks; heat
   and rated temperature; aim under a hull lie. Waits on: cut 3.
4a. `ew-jammer`: the jammer gear: a heard jamming emission, a cone, and the one jamming term in `Sensor.Gain`, heat
    to its own cell. Waits on: cut 4 (ruling `ew-noise-jamming-ruled`).
5. `ew-burn-through`: own illumination record; paints and pings above masking strength see through. Waits on:
   cut 4.
6. `ew-blend`: background kinds and their envelopes; believed background is clutter. Waits on: cut 4.
7. `ew-emitter-bodies`: the deployer and the emitter body (heat tender, crude decoy). Waits on: cut 2 (claim
   defaults); cut 4 for a body that carries its parent's profile.
8. `ew-seekers-read-belief`: missile seekers use the one judge; decoys pull seekers; the cloud pin. Waits on:
   missile-records, cut 7.
9. `ew-ai-doctrine`: AI goes cold, launches tenders, masquerades when hunted, illuminates to burn through. Waits on:
   faction-play-1, cuts 4 and 7.
10. `ew-own-lie-hud`: the liar's own panel: what it claims, heat per second, which known observers see through.
    Waits on: cut 4.

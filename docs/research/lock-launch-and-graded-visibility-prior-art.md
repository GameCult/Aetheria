<!-- Serves aetheria-release:ruling:lock-and-visibility-rethink (operator, 2026-10-10). Eyes pass eyes-aeth-lock-visibility. -->

# facts.md - lock-on, visibility and launch signature: Aetheria today and prior art

Serves operator direction `aetheria-release:ruling:lock-and-visibility-rethink` (2026-10-10) and the in-force rulings it cites. Eyes (eyes-aeth-lock-visibility), session self-2026-10-10-ag2. Facts only; no conclusions or recommendations. Tags: FETCHED (page read this session), SEARCH (search-result summary only, page not opened), KNOWLEDGE (from memory, unverified).

## 0. Rulings read (eureka-state view)
- lock-and-visibility-rethink (2026-10-10): operator: "not sure what missile lock-on actually represents"; "no line between a missile and a kamikaze drone"; "seeding an area with loitering munitions while you gather tracks should be perfectly viable"; "visibility is one number right now. That launch reveals everything about the ship or nothing". Self holds consumable-ai (its vent-at-half-lock rule assumes the gate).
- ADDENDUM (operator, 2026-10-10, via coordinator, verbatim): "It's closer to the fantasy described in the novella, where you learn about a target by forcing it to reveal its capabilities one by one".
- shared-track-bearing-only (2026-10-03): shared track is a cue (bearing + grudge), never writes EntityInfoGathered, never counts toward Designated; "even a missile in flight only gets a bearing, it must still perform terminal maneuvering itself".
- missiles-fuel-and-seekers (2026-10-06): launch data aims; terminal guidance is the missile's own sensors; manoeuvre spends limited fuel.
- missiles-seeker-retarget (2026-10-06): missile with no/lost target takes the brightest reading above threshold among hostiles (to launch faction) in its cone/range; "going dark and firing decoys plausible".
- drones-and-munitions-are-entities (2026-10-04): both use the entity+gear system with dedicated gear; expendability etc. follow from gear, not a kind flag; how the pilot commands them is not ruled.
- missiles-sim-side-light (2026-10-06): munitions are lightweight sim records (PendingShot + flight struct) targetable via the targeting index, no Entity; full entities later. missile-stats-now-gear-later (2026-10-06): stats on missile data now, derived from gear later.
- lock-warning-strength (2026-10-10): painted ship learns lock strength 0..1; AI vents when one lock passes half.
- sensor-stat-set (2026-10-08): per-emitter power/beam width/intercept factor alpha; passive listening bearing-only, two listeners fix a position; allies share tracks silently; painted target always learns bearing, emitter class, search vs track; "Emission is one more signature, alongside heat."

## 1. Aetheria today (origin/master of GameCult/Aetheria after fetch; warning from branch origin/eureka/aetheria-release-lock-warning)
Paths under Assets/Scripts/ServerShared/ unless noted.

### Visibility / detection representation
- Observer-side knowledge: `Entity.EntityInfoGathered : ReactiveDictionary<Entity,float>`, one float per observer per other entity (Entity.cs:89). Seeded 0 for each zone entity (Entity.cs:210-221), removed on zone removal (:222-229).
- Only writer: `Sensor.Execute` (Behaviors/Sensor.cs:137-181; write at :178). Per entity: ping gain once when inside the expanding ping radius (:166-170), else passive gain via sensitivity curve on angle off sensor facing (:171-174); then times `1 - TargetInfoDecay*dt` (:176). Gain formula Sensor.cs:190-193: ping = visibility*sensitivity*PingBoost*distance; passive = visibility*sensitivity*curve(angle/pi)*dt/distance; times `Zone.Obscuration` (vapour clouds).
- Target-side signature: `Entity.Visibility` = sum of `VisibilitySources` values (Entity.cs:88, :161). Every observer's Gain reads this same scalar (Sensor.cs:169,174). Sources: thermal radiation (Entity.cs:1589), Radiator (Behaviors/Radiator.cs:121), Reflector cross-section*light (Reflector.cs:46), Visibility gear (Visibility.cs:44), ConstantWeapon (ConstantWeapon.cs:173), InstantWeapon on each fired round (InstantWeapon.cs:280; value Weapon.Visibility, Weapon.cs:140), Thruster = input*Visibility stat (Thruster.cs:132-134, max-in-place), Sensor ping (`PingVisibility`, Sensor.cs:117).
- Decay: every VisibilitySources entry decays by `GameplaySettings.VisibilityDecay` per Entity.Update, removed below 0.1 (Entity.cs:1395-1399; setting Settings.cs:216).
- Threshold to "visible": `EntityInfoGathered` crossing `TargetDetectionInfoThreshold` adds/removes the entity in `VisibleEntities`/`VisibleEnemies`/`VisibleFriendlies` (Entity.cs:263-280). `PerceivedStanceOf` returns hostility only if visible (Entity.cs:729-732).
- Graded use of the same scalar already exists: `FireControl.IsRevealed` ranks a target's non-hull equipment and reveals item i of N when info >= lerp(TargetArmorInfoThreshold, TargetGearInfoThreshold, i/(N-1)) (FireControl.cs:164-183), derived on every read, never stored. Hit odds read info against TargetingSystem Resolution (Behaviors/TargetingSystem.cs:23-30; FireControl.cs:323).
- Designated: `FireControl.Designated(weapon, source, target, out range)` = target in source.VisibleEntities AND range in [MinRange, Range] AND not (LockWeapon and not IsLocked) (FireControl.cs:291-298).
- Targeting index: `TargetSearch`/regions carry `ReachPerVisibility`; a region is skipped if Nearest > MaxVisibility*ReachPerVisibility (Targeting/TargetingIndex.cs:30-62); `Entity.DetectionReachPerVisibility` (Entity.cs:601-614) bounds detection reach per unit visibility; `EntityTargets` regions use MaxVisibility = +inf (Targeting/EntityTargets.cs:30); BeltTargets uses cross-section*light (BeltTargets.cs:190).

### LockWeapon (Behaviors/LockWeapon.cs)
- Stats LockSpeed, SensorImpact, LockAngle, DirectionImpact, Decay (:12-25, :48-52). `LauncherData : LockWeaponData` (Behaviors/Launcher.cs:10); its CreateInstance returns LockWeapon (Launcher.cs:30-33), so launchers are LockWeapons.
- Execute (:81-110): lock resets to 0 when Entity.Target changes (:85-90). If the target is hostile and within LockAngle degrees of aim: `_lock += pow(1-angle/90, DirectionImpact)*dt*LockSpeed*pow(EntityInfoGathered[target], SensorImpact)` (:100-105); outside the angle `_lock -= dt*Decay` (:106).
- `CanFire = base.CanFire && _lock > .99 && TargetRange in (MinRange, Range)` (:56); `IsLocked => _lock > .99` (:66); Progress shows cooldown or lock (:54). Events OnLocked/OnBeginLocking/OnLockLost declared (:44-46).
- Lock reads only the shooter's own EntityInfoGathered of the target; on master nothing in LockWeapon writes any state about the target.

### What a launch changes about the launcher (master)
- `InstantWeapon` (LockWeapon's base) sets `Entity.VisibilitySources[this] = Visibility` on each fired round (InstantWeapon.cs:280) and AddHeat (:279). That raises the launcher's single Visibility scalar for every observer equally, decaying per Entity.cs:1395-1399. Same path as any gun. No launcher-specific signature, no event, no bearing/identity/class channel; observers' info on the launcher rises only through their own Sensor.Gain on that scalar.
- cut_spec cut-missile-records.r3 (eureka-state): in-flight seeker reading = Sensor.SettledInfo(Sensor.Gain(seeker.Visibility, Sensitivity, cone ? 1 : 0, distance, 1, false), 1); Locked = reading > TargetDetectionInfoThreshold; reacquires hostiles in cone every SeekerInterval (0.5). The seeker's view of a target uses the same Visibility scalar.

### Lock warning (branch origin/eureka/aetheria-release-lock-warning, diff vs master)
- `LockWeapon.Execute` adds `target.ReceivePaint(Entity.Position, Item?.Data, Lock)` each tick the lock builds inside LockAngle (not on the decay branch) (LockWeapon.cs, +105).
- New `readonly struct LockWarning { float2 Bearing; EquippableItemData EmitterClass; float Strength }` (end of LockWeapon.cs).
- Entity: `_paintInbox`, `_incomingLocks`, `IncomingLocks`, `internal PublishPaints()`, `internal ReceivePaint(pos, class, strength)` with Bearing = normalize((emitterPos - Position).xz); Zone.Update calls PublishPaints for every entity before any Update (Zone.cs ~229). A warning lives one tick past the last paint. Comment: TargetedBy counts selection, which emits nothing; a warning comes only from a paint (Entity.cs near :173).
- HUD: the strongest warning by Strength drives an incoming-lock indicator placed IncomingLockIndicatorDistance toward the bearing, same curves as the outgoing indicator (Gameplay/ActionGameManager.cs, ApplyLockProgress). The warning is produced by lock accumulation, not by a launch.
- Master HUD fields reading EntityInfoGathered: ActionGameManager.cs:1277-1321.

### Transient/event signatures
- No event type exists. The only transient mechanism is VisibilitySources entries that decay: weapon fire (InstantWeapon.cs:280), sensor ping (Sensor.cs:117), thruster input (Thruster.cs:132-134). Radiator and thermal radiation are continuous (Radiator.cs:121; Entity.cs:1589). Source key is the behavior object, so sources are separable internally, but only the sum is exposed (`Visibility`, Entity.cs:161); the other external reader is SchematicDisplay.cs:192 (own visibility label).
- Lineage doc (AetheriaEve docs/original-game-specification.md:203-227, via voidbot): "Visibility is the sum of independently decaying sources produced by thermal radiation, weapons, propulsion, explicit visibility behavior, reflector cross section, and active pings."

## 2. The novella and design notes (AetheriaLore, via voidbot; read-only)
The exact passage about forcing a target to reveal capabilities one by one was not located verbatim. Nearest passages:
- `Aetheria/Brainstorming/Stories/Pirate Metagame Novella/11 Draft Movement II/Movement II Draft.md:1411-1428` (same text in 15 Revision Council/Revised Manuscript Candidate.md:4636-4663): a baited sensor officer; the ship "revealed active sensors, capture ballistics, and a surrender channel" - reveal as a chosen sequence. At :1351-1378: the crew reads the target's attention as "changes in emitted challenge, passive illumination, and local cognition allocation" and "what work it spent"; Ilya: "I am preserving uncertainty."
- `15 Revision Council/Candidate B - Chapters 4-8.md:149-176`: eleven unresolved traces; "No parent classification below confidence gate"; Ilya: "It is behaving like a parent"; launched drones go "cold at first, then brightening as they spread", their sensors "woke one by one", views "entered Sable's architecture without becoming agreement".
- `12 Draft Movement III/Movement III Draft.md:179-210`: "The ship opened its radiators before the enemy had finished pretending to be debris."
- `Aetheria/Game Design/Heat, Stealth, and Detection.md` (read in full): contact states Detected / Classified / Identified / Lost; contacts progress "through evidence, not fixed revelation timers"; every contact change needs an explanation (emission, thrust, radiator exposure, shared observation, geometry, occlusion, stale evidence); loop includes "deploy a bounded probe, or scan actively" and "the target reveals itself"; status: design lineage, no implementation established.
- `Aetheria/Worldbuilding/Post-Elysium/Reference/Transponder Credentials.md:1-10`: a heat trace does not identify itself; the transponder becomes readable after sensors have gathered enough.
- `Aetheria/Game Design/Faction Play.md:104-115`: what a faction does with radiators, thrust and pings is "the primary tell"; HUD faction identity only once gear is revealed (IsRevealed at TargetGearInfoThreshold).
- `Aetheria/Brainstorming/Technology/Drone Signature Warfare Simulation Artifacts.md:327-336`: metrics munition_loiter_time, munition_signature (visibility, thrust heat, sensor activity), lock_rate, active_scan_exposure.

## 3. Prior art
### 3a. LOBL/LOAL, fire-and-forget, datalink, loitering munitions
- FETCHED Wikipedia "Lock-on after launch": LOAL = seeker acquires after leaving the launcher; LOBL is a retronym; LOAL uses strapdown inertial guidance to know where to look, cued by helmet sight or onboard radar/FLIR; with a datalink the missile receives continuous target updates while its seeker is inactive; internal carriage and flight-limited range (AIM-9X range up about a third); "Most loitering munitions use LOAL"; 2003 Rafael Python-5 test: radar-cued pre-launch, high trajectory, seeker on about 5 nmi from a target 15 nmi away.
- SEARCH: MBDA Sea Venom/ANL fired LOBL or LOAL with two-way datalink and imaging seeker (operator can change aim point or abort); Spike NLOS supports both; engagement studies treat fire-and-forget vs precision and LOBL vs LOAL as separate axes; forum disagreement over whether LOAL requires a datalink.
- FETCHED Wikipedia "Loitering munition": most are operator-in-the-loop via EO sensors and datalink until a target is designated; anti-radiation types (IAI Harpy) search and attack radars autonomously with an anti-radiation seeker; Mini Harpy triple-homing (anti-radiation, EO day, EO night); early Harpy/Tacit Rainbow were placed over suspected SAM sites and struck when a battery was spotted; autonomous search may last hours, may request human approval; FPV types give the operator first-person view.
- FETCHED Wikipedia "AeroVironment Switchblade": EO/IR cameras with real-time video; operator designates, aided target tracker for moving targets; retargetable en route; wave-off until about 4 s before impact.
- FETCHED IAI Harpy page: no seeker band/loiter-pattern/radar-off detail (gap).

### 3b. RWR and launch/missile warning
- FETCHED Wikipedia "Radar warning receiver": measures frequency, signal shape, PRF; separates emitters by type and sorts by threat priority; several antennas give direction of arrival, drawn relative to heading; strength and waveform help estimate threat type; display distance may show estimated range or threat severity (tracking nearer centre than search); symbols show radar type or carrying vehicle; after a SARH missile is fired the RWR may detect the radar's guidance-mode change and the warning becomes more insistent.
- SEARCH (migflug, codex.uoaf F-16 notes, informal): audio tones differ for search/track/missile guidance; on some units display distance is signal strength not range; F-16 MISSILE LAUNCH indicator separate from mode lights.
- FETCHED Wikipedia "Missile approach warning system": pulse-Doppler MAW reports range and closing speed (time to impact), imprecise direction; IR MAW reports azimuth/elevation, no range, can see after burnout; UV MWS detects the burning solid motor, wide field of view, angle only, TTI estimated from signal-amplitude rise, fast response to nearby launches.
- SEARCH: submarine tube-ejection air blast radiates a detectable wavefront (patent US4313181); Cold Waters community: launch transient audible, gives launch bearing alert; enemy detects your torpedo's pings; radar mast or active sonar gives away position.
- KNOWLEDGE: RWR/ESM report bearing, emitter class and mode, not range; IR/UV launch detectors report bearing and a launch event.

### 3c. Graded contact information, transient signatures
- SEARCH: joint dictionary: identification = determining friend/hostile character of an unknown detected contact; IFF identifies friends only, non-response ambiguous. CMO Steam thread (secondary): in tutorials ground radar never classified a contact, airborne radar did. CMO manual definitions of its levels and uncertainty areas: NOT obtained.
- SEARCH (TMA literature): bearing-only gives direction only; range from observer manoeuvre or bearing history; uncertainty shown as an area of uncertainty plus course/speed uncertainty; single-ESM tracking is nonlinear without range.
- SEARCH: Steam thread app 1286220 (KNOWLEDGE: Command: Modern Operations): ships under EMCON tracked enemies by ESM alone; switching radars on let the AI ESM counter-detect and identify them as hostile; ESM contacts approximate and horizon-limited. USNI rule of thumb: emission detectable at about twice its usable range.
- SEARCH: Cold Waters labels contacts by how first detected (S1...), mast/ESM/periscope exposures convert sierras to master contacts; sonar estimate can differ from true position.
- SEARCH EVE University wiki: lock time = 40,000 mm / scan resolution * asinh(signature radius)^2; FETCHED "Lock time" page: Passive Targeter "allows target lock without alerting the ship", implying a normal lock alerts the target; what the target sees is not on the page. Directional scanner: not found.
- Nebulous: Fleet Command: wiki.hoodedhorse.com returned 403 (NOT read). SEARCH only: Modular Missiles update (Aug 2022); "Minor Missile" update (2023) mixes missile types in a salvo; ships with active comms coordinate interceptors; wiki file index lists dual-seeker and seeker-validator diagrams and a comms-jamming diagram; players: command guidance jammable so backup seeker advised, EO seekers hard to decoy. In-repo second-hand (docs/aetheria-release-map.md via question missiles-seeker-retarget): Nebulous re-selects every 0.5 s by signal strength with validators; command-guided missile that loses track cruises to last known position; HighFleet blind-fired missile locks the first fleet in its 90-degree cone.
- Children of a Dead Earth, Highfleet, Starsector sensor models: searches returned nothing usable (NOT read).

### 3d. Missiles and drones as one category from parts
- SEARCH (Steam workshop, community): a Space Engineers drone-carrier script uses drones as guided missiles ("ram" waypoint toward the host's current TargetVectors, then arm warheads), host commands over a channel; Whiplash141 missile script: a missile with no target flies semi-active on whatever a designator paints, then listens only to updates for its own lock. Avorion: only community reports (torpedoes/fighters prioritised by turret Defensive setting); nothing found on turret-target handoff to torpedo/fighter; developer says a weapon rework is planned. Nebulous missile builder/handoff detail: not obtained.

## 4. Provoking information out of a contact (addendum)
- SEARCH (Wikipedia Wild Weasel, Misawa AF article): the Wild Weasel role is to bait air-defence radars into tracking the aircraft, trace the emission to its source and hit it with anti-radiation missiles (AGM-45 Shrike); crews had no warning of SA-2 tracking; radars cycled on and off to avoid being homed on. Detailed SA-2 provocation technique not in results.
- SEARCH (NSA, CIA reading room, RAF "Listening In"): Cold War ELINT probing flights along the Soviet periphery recorded radar emissions to build an Electronic Order of Battle (location, function, parameters); results do not say flights were designed to make radars light up; "Radio Proving Flights" named, not elaborated.
- FETCHED Loitering munition page: Harpy-class loitered over suspected SAM sites and struck when a battery emitted.
- KNOWLEDGE (unverified): reconnaissance by provocation uses feints and decoys so radars, weapons or comms activate and ESM records them; ELINT reveals emitter class and mode and accumulates into order of battle over many events.
- Games: Cold Waters/CMO notes above (emission raises detectability and classification; EMCON ESM tracking; radar-on counter-detection). No source found for a game modelling "weapon class learned on fire, drive class on burn, sensor class on emission" as separate reveals; Aetheria's own IsRevealed (FireControl.cs:164-183) reveals gear by info tier, not by event.

## 5. Gaps
- sensor-stat-set cites scratchpad prior-art-sensors/findings.md (not read).
- Novella line the operator paraphrases not found verbatim; Candidate/Revised copies duplicate text.
- Nebulous wiki 403; CMO manual, Children of a Dead Earth, Highfleet, Avorion detail, EVE d-scan/target warning not obtained.
- Did not open Unity GuidedProjectile or AI code consuming IncomingLocks.
- SEARCH items are secondary summaries.

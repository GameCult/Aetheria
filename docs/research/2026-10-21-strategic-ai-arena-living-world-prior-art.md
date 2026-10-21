# Strategic AI arena and living world: prior art (Eyes pass)

Date: 2026-10-21. Eyes, read only. Facts with pointers. No conclusions or recommendations.

**What this serves.** This pass serves the operator's idea of 2026-10-21, before any fork is put to her. Her words:

> "You know how we were talking about setting up an arena where we have agents write combat routines and simulate
> thousands of matches in various scenarios, from which we can establish various faction combat doctrines (say,
> restrict each agent's fleet to a specific faction or set of loadouts)? I just thought, why don't we do the same thing
> for the RTS side of the game? That would have way more impact than trying to simulate either the economy via Njordr
> or the narrative via Ghostlight, because we already control the game state and all its possible mutations, and when
> we have established AI routines for both faction play and combat, we have a living world to present the player,
> where the jobs that are available all advance some employer's interests in real time."

Her scope addition, relayed by Self the same day:

> "If we're able to score on character, that's where this also takes over Ghostlight's job. And yes, of course the
> written routines are content. We run the arenas to surface solutions to the metagame we've established. We run that
> in a loop to explore how our games rules and the content we create (tech trees, crafting tree, manufacturing
> tradeoffs, combat metagame) cause interesting and fun strategies to emerge. We use agents to explore the metagame
> space so that we can iterate on the game design through virtual play."

**Provenance key.** **[L]** read locally in full: a repo file, the Eureka mind, or a PDF extracted with `pdftotext`.
**[F]** a fetched page, read by WebFetch's summarizer model. **[S]** a WebSearch summary only: treat it as a pointer
to chase. **[mem]** the agent's prior knowledge, not re-verified in this pass. Aetheria anchors are against `master`
`2f4b617e` (2026-10-19), which matches `origin/master`.

**Earlier passes this one builds on.** They are not repeated here.
- `F:\Projects\aetheria-colosseum-prior-art.md` (2026-10-04) covers bot platforms (Robocode, Screeps, Battlecode,
  Halite, CodinGame, the BWAPI ladders, SC2 AI Arena, Lux AI, CodeClash) and automated balance play in sections 1
  and 2.
- `F:\Projects\aetheria-authored-missions-prior-art.md` covers the X4 Mission Director and the Starsector hub
  missions (lines 37-75).
- `F:\Projects\aetheria-story-sifting-prior-art.md` covers story sifting.

---

## 1. Persistent worlds driven by AI factions, and jobs drawn from their needs

### X3 and X4: Foundations (Egosoft)
- **Economy.** Egosoft's marketing says hundreds of stations make wares, thousands of NPC ships carry them, and prices
  follow that simulated trade. It also says that in X4 every part of the NPC economy is manufactured from
  resources: ships, weapons, upgrades, ammunition and stations. [S] Steam/press copy via
  https://dsogaming.com/?p=120136 and https://www.gematsu.com/games/x4-foundations
- **Territory.** The Egosoft wiki says factions try to grow their ship and station counts and to annex whole sectors.
  [S] https://wiki.egosoft.com/X4%20Foundations%20Wiki/Manual%20and%20Guides/Objects%20in%20the%20Game%20Universe/Factions/
- **How NPC ships get their work.** NPC ships are spawned to fill per-faction quotas in a `jobs` library file. The
  `god` file seeds stations, and in X3 the "GOD engine" also kept the economy alive by adding and removing
  stations. [mem]
- **Missions.** Story and generic missions are both Mission Director cue trees. They act on the live universe's
  sectors, ships and factions, and observe and perturb the economy rather than replace it.
  [L] `F:\Projects\aetheria-authored-missions-prior-art.md:37-51`
- **Documented failures (anecdotal).** Players report early shortages at shipyards and wharfs that leave only one or
  two stations unaffected. Players also report that war is mostly triggered by the player, and one forum poster
  calls the economy shallow, with "only one product: ships". [S] https://forum.egosoft.com/viewtopic.php?p=4912608 ;
  https://steamcommunity.com/app/392160/discussions/0/3104642254774431342
- No GDC talk or postmortem on X4's economy was found.

### Elite Dangerous background simulation (BGS)
- **Structure.** Each populated system holds one to seven minor factions, and one of them controls the system.
  Player actions push factions into states covering economy, security, health and influence.
  [S] https://forums.frontier.co.uk/goto/post?id=4850407
- **Layered states.** Since Chapter Four (2018), a faction can hold several states at once, and a war applies only
  in the system where it is fought. [S] https://massivelyop.com/?p=192372 . Frontier names political, economic,
  outfitting and Thargoid layers that interact: Boom improves outfitting and changes prices, and a Thargoid state
  suspends political movement. [S] same forum guide
- **Missions as the lever.** Combat actions and most combat missions push a war toward its result. Elections move on
  non-combat missions, trade and exploration data. [S]
  https://www.gamingnexus.com/News/43299/Elite-Dangerous-update-to-combat-issues-that-keep-popping-up-in-Background-Simulation
- **Missions draw on faction state.** One player reports that a faction with no state offered no missions.
  Another reports that missions in Powerplay systems moved no BGS influence. Both are user reports.
  [S] https://steamcommunity.com/app/359320/discussions/0/1694920442961862234 ;
  https://forums.frontier.co.uk/threads/missions-not-impacting-bgs-especially-during-elections-in-pp-systems.633066
- **Cadence.** BGS states tick daily and Powerplay weekly. [S] same results
- **Critique.** Some players say the BGS has little effect on play. Others say it matters for prices, missions and
  player groups. [S] https://yesxorno.substack.com/p/ed-bgs-and-pp2

### EVE Online: NPC faction warfare
- **Warzone control.** Control is counted as systems held plus installed upgrades, per faction, against the enemy.
  Rising control raises a warzone tier, which originally scaled LP store prices from 4x dearer to 4x cheaper.
  [S] https://www.eveonline.com/news/view/fw-dev-blog-1-1-1
- **A failure CCP corrected.** Players "spiked" tiers to cash out LP in one day. CCP replaced the store discount with
  an LP gain bonus. [S] https://www.eveonline.com/news/view/fast-forward
- **Tuning.** Victory points from complexes were cut by 5x, which raised the average system capture from 6-7 hours
  to 30-35 hours. In March 2020, FW mission LP payouts were cut by about 30%. [S] same blogs;
  https://forums.eveonline.com/t/patch-notes-for-march-2020-releases/224859/213
- **Who moves the front.** Players do: the NPC militias are mission givers and site defenders, and do not drive the
  front themselves. [mem]

### Mount & Blade II: Bannerlord
- **Issues.** Quests are "issues" attached to notables, generated by campaign behaviours from settlement state; a
  village headman needing grain is one example. [mem] (the `IssueBase` type was not confirmed against the docs this
  pass). Modders can add campaign behaviours, which run while the player is on the campaign map. [S]
  https://docs.bannerlordmodding.com/ , https://www.gamebanshee.com/kdyqp
- **Documented failures.**
  - Issue quests are repetitive, and the conspiracy quests send the player across the map during campaigns. [S]
    https://gamebanshee.com/9k6hq
  - Bandit quests fail because too few bandit parties spawn. [S] Steam discussions in the same search
  - TaleWorlds planned to improve "the issue creation algorithm" so players meet a broader range of issues. [S]
    https://www.gamebanshee.com/383c4
  - AI armies leave newly won fiefs undefended, and AI lords mismanage conquered towns and lose them to rebels. [S]
    review and Steam threads
  - The AI declares war on whichever kingdom it deems strongest, often the player's. [S] Steam threads
  - Fief-less factions recruiting lords produced large armies with no territory, which players found confusing. [S]

### Dwarf Fortress: world generation
- **What is simulated.** World generation runs history for a set number of years: civilizations, races and religions
  spread, wars break out, and population and death counters climb. Legends mode exposes every civilization and
  historical figure. [S] https://dwarffortresswiki.org/index.php/DF2014:World_gen ;
  https://twinfinite.net/guides/best-dwarf-fortress-world-generation-settings/
- **Parameters.** History length, number of civilizations, number of beasts, savagery and maximum sites. [S] same
- **Adams on the game.** He calls it a "fantasy world generator" in which the player takes part in a small story.
  [S] https://scalar.usc.edu/works/game-changers-2016-exhibit/dwarf-fortress . His criterion for adding a
  simulation (Gamescom 2026) is whether playing through it yourself felt cool. [S]
  https://www.pcgamer.com/gaming-industry/game-development/dwarf-fortress-creator-says-we-have-a-natural-lean-towards-human-made-stories-but-but-were-all-going-to-be-very-sorely-tested-in-the-coming-years-by-ai/
- No primary source on army or site simulation was found. The FDG 2019 and UCSC 2019 emergent-narrative talks are
  listed at https://www.bay12games.com/dwarves/links.html. [S]

### Space Rangers 2: Dominators
- **AI rivals.** Other AI Rangers compete with the player on a ranked leaderboard, ask for help, and propose joint
  attacks. [S] https://www.gamespot.com/space-rangers-2-rise-of-the-dominators/user-reviews/2200-489673/
- **Economy.** Prices move with supply and demand, local Dominator attacks, and events such as famine. Many events
  happen without the player. [S] https://sf-encyclopedia.com/entry/space_rangers ; gamesfirst review
- The search found no developer documentation.

### Starsector
- **Colony crises (Nov 2023).** Crises replaced punitive expeditions for most factions and are tailored per faction.
  The developer notes that the old raids by Pirates and the Path were unlimited. [S]
  https://fractalsoftworks.com/2023/11/24/colony-crises
- **Crisis mechanics (wiki, 0.98a).**
  - Points accrue, preparation starts at 500, and the attack comes at 600.
  - One-time reductions build "blowback" that feeds 5% back each month, so they only postpone a crisis.
  - Resolutions can be narrative: a treaty with Kanta, or a deal with the Path.
  - [S] https://starsector.wiki.gg/wiki/Colony_Crises
- **Campaign missions.** Missions locate themselves in the generated sector by require, prefer and pick search.
  [L] `F:\Projects\aetheria-authored-missions-prior-art.md:53-73`
- **Fleets.** The economy is abstract supply and demand per market, and fleets are spawned by per-market managers
  (patrols, traders) rather than carrying actual goods. [mem]

### Stellaris AI
- **Dev diary #252.** Patch 3.3 aimed at AI economic management in the mid and late game. Patch 3.4 turned to fleet
  management and diplomacy, and its fleet changes were "mostly bug fixes". The diary opens on the AI splitting its
  fleets into tiny pieces. Once the AI grew stronger, players objected to it passing Galactic Community resolutions
  against them. [S]
  https://admin-forum.paradoxplaza.com/forum/developer-diary/stellaris-dev-diary-252-artificial-and-automated-intelligence.1521980/

### Ultima Online's original ecology
- **Design.** Raph and Kristen Koster designed it: grass grows, herbivores eat the grass, carnivores hunt the
  herbivores, and dragons pursue a Maslow-like hierarchy of needs. [S] https://en.wikipedia.org/wiki/Ultima_Online
- **What Koster says happened.** [F] https://www.raphkoster.com/?p=46439
  - The ecology ran in alpha. During beta an engineer rewrote it and then the AI was disabled; the resource values
    stayed and still feed crafting.
  - It was cut for performance: radial searches followed by pathfinding were too expensive.
  - He separates the cut from the "collapse". The economy was a closed loop with a fixed resource pool. Players
    hoarded (sheep killed, wool made into shirts for skill grinding, the shirts held), the central bank ran out of
    wool, and no more sheep could spawn.
  - Zach Simpson's analysis of UO's economy is the source of the term "faucet-drain economy".
- **Starr Long's account** is that players killed everything faster than it could respawn. [S] Wikipedia
- **Wolves.** In 2001 Koster said long-lived wolves grew cannier because there was no cap on how smart they could
  get. [S] https://ultimacodex.com/2014/07/pimps-and-dragons-ultima-online-profiled-in-the-new-yorker-circa-2001/

### Pre-simulation, snowballing, and level of detail

The operator added (2026-10-21): "And since we need all of this to run many times faster than realtime anyway, we
can easily simulate a living world for the player when we drop them into the game".

- **Worlds simulated before the player arrives.**
  - Dwarf Fortress runs history for a chosen number of years before play, and Legends mode exposes the result.
    [S] see Dwarf Fortress above
  - Caves of Qud generates the histories of five ancient Sultans at the start of each run. Grinblat (GDC 2018)
    avoided a full historical simulation: events are generated first and their causes rationalised afterwards with
    a state machine and a replacement grammar. [S] https://gdcvault.com/play/1024990/Procedurally-Generating-History-in-Caves ;
    https://pcgworkshop.com/archive/grinblat2017subverting.pdf
  - Ghostlight's own storylet ruling has the same shape: simulate to a baseline, then branch. [L] section 4.4
- **Runaway snowballing, and how games damp it.**
  - *Bannerlord.* Early-access patches added relation penalties after raids and sieges and stopped clans defecting
    to kingdoms that had taken their settlements, explicitly "to prevent players from easily snowballing". Players
    reported the snowballing persisted. [S] https://www.thesixthaxis.com/2020/04/06/mount-blade-2-bannerlord-crash-fix-update-1-0-5/ ;
    https://steamcommunity.com/app/261550/discussions/0/2264689848033111449
  - The AI declaring war on whichever kingdom it judges strongest acts as an emergent coalition. [S] section 1
    Bannerlord
  - *Stellaris.*
    - AI empires snowball through federations: one AI megacorp grew its influence by inviting everyone into its
      federation. [S] https://steamcommunity.com/app/281990/discussions/0/3826416272371455457
    - Damping comes through Galactic Community resolutions, the Custodian and Emperor mechanic (declaring Empire
      dissolves federations), and end-game crises that force cooperation. [S] same thread;
      https://guildorder.com/tr/games/stellaris/wiki/end-game-crises
    - Paradox also weights AI aggression against relative power, and difficulty scaling changes AI bonuses. [mem]
  - *Other catch-up mechanics* [mem]:
    - EU4: coalitions against high aggressive expansion, and overextension.
    - Civilization: rising costs as a civ grows.
    - Starsector: crisis "blowback" only postpones a crisis. [S] above
- **Level of detail: full simulation near the player, abstract far away.**
  - X3: objects out of sector reduce to statistical, turn-based events without collisions. [S] section 7
  - X4: high and low attention, set by distance from the player. Low-attention combat and movement are separately
    tuned and patched. [S] section 7
  - Elite's BGS is a daily, system-level state machine of influence and faction states, fed by player actions. It
    does not simulate ships. [S] section 1, Elite
  - Ghostlight's multiresolution gestalts "aggregate simulation work, not canonical identity or state": a villager
    becomes an individual when attended to and returns to a cheaper population turn while keeping a durable delta.
    [L via voidbot, `Ghostlight/docs/articles/multiresolution-gestalts.md:28-55`]
  - Aetheria today ticks only the player's zone; the other zones stay packed. [L] section 4.1

---

## 2. Bot tournaments and agent-written strategy

Platforms and ratings: see `F:\Projects\aetheria-colosseum-prior-art.md` sections 1.1-1.13 (Robocode, Screeps,
Battlecode, Halite, CodinGame, BWAPI/SSCAIT/AIIDE/CIG, SC2 AI Arena, Lux, CodeClash). This pass adds the following.

- **microRTS competition.**
  - It began at IEEE CIG 2017 with three entries (Ontañón et al., AI Magazine 2018). [S]
    https://ojs.aaai.org/index.php/aimagazine/article/view/2777/2687
  - It has been held at CoG since 2019: a round robin on 12 maps (8 open, 4 hidden) with a 100 ms limit per step. [S]
    https://arxiv.org/html/2402.08112v1
  - Scripted agents won the first five editions. The first deep RL winner, RAISocketAI, won in 2023. [S] same
- **Generals.io.** A public bot server and API exist (`bot.generals.io`, socket.io). [mem; the search returned
  nothing]
- **Screeps.** A persistent MMO of player-written JS bots with no ladder; ranking is by GCL and power. Tick length
  depends on player load. [L] `F:\Projects\aetheria-colosseum-prior-art.md:25-30`
- **LLM agents playing or writing strategy.**
  - **CICERO** (Meta FAIR, *Science* 2022).
    - It joins a 2.7B dialogue model to the piKL planner. piKL improves predicted policies toward higher expected
      value while a KL term keeps them close to human-like play.
    - It ranked in the top 10% over 40 webDiplomacy games, scoring 25.8% against a human average of 12.4%.
    - Reported weaknesses: it relies on human data and adapts poorly to out-of-distribution metagames such as
      variant maps (course slides).
    - [S] https://ai.meta.com/blog/cicero-ai-negotiates-persuades-and-cooperates-with-people/ ;
      https://cw.fel.cvut.cz/b251/_media/courses/xp36rgm/diplomacy.pdf
  - **Voyager** (Wang et al. 2023): an automatic curriculum, a growing skill library of executable code, and
    iterative prompting from environment feedback and self-verification. Code is its action space because programs
    express temporally extended, compositional behaviour. [S] https://arxiv.org/html/2305.16291v2
  - **TextStarCraft II and Chain of Summarization** (NeurIPS 2024): most of 10 LLMs beat the level-5 built-in AI,
    and fine-tuned LLMs played on par with Gold-league humans. [S] https://arxiv.org/abs/2312.11865v2
  - **LLM-SMAC** (Deng et al. 2024): a planner, a coder that writes decision-tree Python, and a critic, for StarCraft
    micro. [S] https://www.promptlayer.com/research-papers/can-llms-master-starcraft-a-new-approach-to-game-ai
  - **"From Code to Play"** (Eberhardinger et al., arXiv 2412.04057; ToG 2025): LLM-driven hill climbing over game
    programs on 29 tasks with 11 models. LLM performance depended more on the task than on model size. [S]
    https://arxiv.org/html/2412.04057v2
  - **Programmatic strategies for RTS** by script synthesis (AAAI 2021). [S] search result only
  - **CodeClash** (2025): LLM-written bots.
    - More than 65% of edits were ungrounded.
    - Models hallucinated reasons for losses.
    - Models recovered poorly after a loss.
    - No model beat expert human bots.
    - [L] `F:\Projects\aetheria-colosseum-prior-art.md:78-82`
- **Known failure modes, with sources.**
  - *Degenerate exploits.* Battlecode dominant strategies were patched mid-contest (2019, 2021, 2023). OpenAI
    hide-and-seek agents surfed boxes through physics bugs. Krakovna's specification-gaming list.
    [L] colosseum file :32-37, :110-112
  - *Superhuman or out-of-character optimal play.* OpenAI Five used instant buyback, five invincible couriers and
    death-ball aggression. AlphaStar needed APM and camera limits to be comparable to humans.
    [L] colosseum file :103-104
  - *Overfitting to the arena.* Opponent-learning bots dominate long round robins (AIIDE 2017). Halite's local
    results diverged from the ladder. CICERO adapts poorly to variant maps.
    [L] colosseum file :50-56, :39-44 ; [S] CICERO slides
  - *Drift when the ruleset changes.* Battlecode buffs created new dominant strategies (2023 anchoring).
    [L] colosseum file :37 . EVE's LP tier change was a rule change that players had arbitraged.
    [S] Fast Forward blog

---

## 3. Fitness for believability or interest, not winning

- **Left 4 Dead AI Director** (Booth, Valve 2009). [L] `pdftotext` of
  https://cdn.fastly.steamstatic.com/apps/valve/2009/ai_systems_of_l4d_mike_booth.pdf
  - The stated goal is "Generate dramatic game pacing", inspired by Counter-Strike's "spiky" pacing: constant combat
    is fatiguing and long inactivity is boring.
  - It estimates each survivor's "emotional intensity". Intensity rises with damage taken, incapacitation and being
    pulled off a ledge, decays toward zero, and does not decay while the survivor is engaged. The Director tracks the
    maximum over the four survivors.
  - It runs four states. **Build Up**: full threat population until intensity crosses the peak threshold.
    **Sustain Peak**: 3-5 more seconds. **Peak Fade**: minimal population until intensity decays and a natural break
    comes. **Relax**: 30-45 s, or until the survivors near the next safe room.
  - A second goal is "Promote Replayability: Procedurally Populated Environments".
- **RimWorld storytellers.** Cassandra Classic, Phoebe Chillax and Randy Random are the player's "unseeable God".
  Cassandra runs a classic rising curve of challenge and tension. The storyteller weighs colony wealth, colonist and
  animal counts, recent deaths or injuries, and time since the last major event, and difficulty is set separately.
  [S] https://rimworldwiki.com/wiki/AI_Storytellers . Sylvester's GDC 2017 talk framed the game "not as a game, but
  as a story generator". [S] https://gdconf.com/news/gdc-youll-see-rimworld-devs-succeeded-everything-wrong
- **Façade drama manager** (Mateas and Stern 2003). It sequences beats from a large pool so that tension rises and
  falls along an Aristotelian arc. Beats carry author-written preconditions and effects on story state, and the
  manager adds and retracts behaviours and discourse contexts as play runs. [S]
  https://users.soe.ucsc.edu/~michaelm/tenurereview/publications/mateas-tidse2003.pdf
- **Oz Project lineage.** ABL descends from Loyall's Hap (Bates 1992; Bates, Loyall and Reilly 1992; Loyall 1997),
  a reactive planning language for "believable agents" that express rich personality. [S]
  https://course.ccs.neu.edu/cs5150f14/readings/mateas_abl.pdf
- **2K BotPrize (UT2004), a Turing test for bots.** Judges tagged opponents as human or bot with a special gun. In
  2012 UT^2 and MirrorBot were both rated 52% human, while human players averaged 40%. In 2010 the best entry reached
  31.82%. [S] https://news.utexas.edu/2012/09/26/artificially-intelligent-game-bots-pass-the-turing-test-on-turings-centenary/ ;
  https://ercim-news.ercim.eu/en84/special/conscious-like-bot-wins-the-2k-botprize
- **Procedural personas.** Holmgård et al. used MCTS with evolved heuristics to give agents distinct play styles.
  [L] colosseum file :101
- **CICERO's KL regularisation toward human play** is a published way to keep a strong policy "in character" with a
  reference distribution. [S] above
- **Ghostlight's existing machinery for in-character faction decisions.** See section 4.6.

---

## 4. Aetheria's own state

### 4.1 The strategic or galaxy layer today
- **Ownership is fixed at generation.** A zone's owner is the faction whose home zone is nearest, if that zone lies
  within the faction's influence distance. [L] `Assets/Scripts/ServerShared/Galaxy.cs:327-347` (the assignment is
  at `:346`). Presence is every faction whose home lies within its influence (`:336-339`).
- **Nothing changes the owner after that.** The only writes to `GalaxyZone.Owner` are the generation write
  (`Galaxy.cs:346`) and the load from save (`Galaxy.cs:77`). [L] `git grep` on master
- **`GalaxyZone` holds topology plus owner and presence.** It has no strategic state: no stockpiles, strength or
  front. [L] `Galaxy.cs:563-576`
- **Relationships are player standing per faction.** `Galaxy.FactionRelationships` holds one value per faction, the
  player's standing (`Galaxy.cs:27`, read at `Entity.cs:1301`), on the ladder Hated through Beloved
  (`Enums.cs:183-189`). Faction-to-faction `Relations` is only a reserved key 16 (`Corporations.cs:133`).
  `Allegiance` (`Corporations.cs:121-122`) answers whose gear a faction buys. [L]
- **Only the player's zone ticks.** Unity steps the one current zone (`Assets/Scripts/Gameplay/ActionGameManager.cs:1357-1361`).
  Other zones persist as `PackedContents`. No code advances unloaded zones or the galaxy over time. [L]
- **Lore: two layers.** `Aetheria/Game Design/Playable Layers.md` (lines 1-36) is marked "historical design
  synthesis". It describes a cockpit layer and a corporate strategy layer that "may exchange periodic or regional
  snapshots rather than share one continuously simulated universe". Corporate play would hand the cockpit layer
  prices, shortages, patrol density, contracts and influence, and cockpit play would hand back purchases, losses,
  salvage and completed contracts. `Corporate Strategy Layer.md` is marked "Historical, non-product design
  lineage". [L via voidbot, AetheriaLore]
- **Archived RTS work.** The archived AetheriaEve repo once had a commander/RTS client (`Aetheria.Rts.Web`) and a
  "Starbridge" commander and pilot design (voidbot hits in `AetheriaEve`). Under the standing rule it is archived
  and is not a consumer. [L via voidbot]

### 4.2 Faction play and doctrine today
- **Doctrine is a typed record on the faction.** `FactionDoctrine` (`Assets/Scripts/ServerShared/Doctrine.cs:24-57`)
  holds `EngageOn` (Detection, Identified, Provoked, Trespass, Never), grace, hail and comply speed, plus one
  `RoleDoctrine` (`:59-74`). Keys 5-8 are reserved for faction-play-2 and 9-10 for faction-play-3 (`:25-26`).
  `Faction.Doctrine` is key 17 (`Corporations.cs:135-137`). Ruling `faction-doctrine-typed`, 2026-10-03. [L]
- **One flight per faction per zone decides.** A flight alone decides its members' targets and tasks, and the agent
  graph decides nothing (`Assets/Scripts/ServerShared/Agents/Flight.cs:7-15`). Flights are created per faction on
  first join (`Zone.cs:72-75, 311-313`). [L]
- **Patrol is the only task ever constructed.** `PatrolOrbitsTask` is built at `Zone.cs:323`. `HaulingTask`,
  `Mining`, `StationTowing` and `Survey` exist under `Agents/Tasks/` but no code constructs them. [L] grep for `new`
- **Trespass.** It is read from zone ownership and security (`Zone.cs:720-725`; `Entity.cs:1265, 1273`). [L]
- **Demo scope.** Faction-play-1 to -3 are demo scope; faction-play-4 (the tender loop) and "procedural regional
  politics and economy" are full game (`docs/aetheria-release-target.md:141-149`;
  `docs/aetheria-release-map.md:459-462`). [L]
- **The candidate design.** `AetheriaLore/Aetheria/Game Design/Faction Play.md` (Imagination, 2026-10-03, "not canon
  and not adopted") proposes five levers: NPC behaviour, loadouts, resource distribution, visuals and narrative
  hooks. Its stance table is at lines 396-411. Its smallest proof asks whether "a playtester, without being told,
  can describe how each faction fights" (lines 501-506). [L via voidbot]

### 4.3 Faction territory
- **The target.** `docs/faction-territory-target.md` (2026-09-19, "target, operator direction only. No map, no
  schedule") wants every sector and station to carry a supply-chain role and a colonization era (`:35-39`), a
  readable territory shape per faction (`:40-45`), and real supply links that the economy and piracy can use
  (`:49-54`). It requires deterministic generation with no runtime model calls (`:55-56, :60-63, :91`). Its open
  question at `:86-87` is "What happens to a sector when its owner changes hands". [L]
- **The doc's anchor is stale.** Its `Galaxy.cs:340` Markov anchor no longer holds: ruling
  `faction-curated-place-names` (2026-10-17) replaced name files with `Faction.Places` (`Corporations.cs:146-149`).
  [L]
- **Mind follow-up.** `aetheria-release:follow_up:faction-territory-target` is in force with full-game scope.
  [L] mind query

### 4.4 Jobs and missions
- **No job or mission generator exists on master.** `Galaxy.cs:261-263` leaves the `StoryProcessor` narrative pass
  commented out. `GalaxyQuest` and `LocationStory` (`Galaxy.cs:578-595`) are data shells. [L]
- **Mind follow-up.** `aetheria-release:follow_up:authored-missions` (in force, full game) wants FreeSpace 2 style
  authored scenes and missions, with mission logic as C# hooks. [L] mind
- **Runtime storyteller.** Ruling `storylets-simulate-sift-lift-weave` (2026-10-03): Ghostlight runs offline to make
  storylets with pre- and postconditions, and Aetheria's runtime storyteller weaves them. Radio chatter is a
  delivery surface. [L] mind. The draft is `docs/reactive-world-target.md` on branch
  `eureka/aetheria-release-reactive-world`, headings at lines 1-146. [L] `git show`
- **Lore.** The Action RPG Layer's smallest proof includes choosing work and inspecting "its route, payment,
  claimant, and known risks". [L via voidbot, `Aetheria/Game Design/Action RPG Layer.md:12-20`]

### 4.5 Headless simulation and the Colosseum
- **The playground map.** `docs/headless-playground-cut.md` (2026-09-17, an Imagination cut map) wants a scripted
  session run headless through one run-lifecycle owner, one input surface and seeded determinism (`:13-54`). Its
  finding was that movement and firing were already shared between player and AI, while combat resolution and
  `CombatState` needed Unity (`:56-100`). Its Cut 2 was re-mapped as fire control (commits `b1c241fb`, `a62d52b6`).
  [L]
- **What is headless on master.**
  - `RunStart` owns new runs, and every run is a `Scenario` (`RunStart.cs` header; `Scenario.cs` header).
  - Scenario tests step `Zone.Update` headless (e.g. `tests/Aetheria.Shared.Tests/PursuitScenarioTests.cs:35, 86-88`).
  - There is no `AetherDb play` command (`tools/AetherDb/` file list).
  - `ItemManager.Random` is still clock-seeded (`ItemManager.cs:20`), while the galaxy uses the run's seed
    (`GalaxyStage.cs:218-226`).
  - [L]
- **The Colosseum ruling.** `aetheria-release:ruling:colosseum-is-the-npc-behaviour-lab` (2026-10-04, operator):
  - The bots are the game's own NPC scripts, written in-repo by agents as compiled C# over a growing primitive
    vocabulary.
  - The plan is to "implement all options, and let the good combinations shake out in the arena".
  - What still matters is a typed observation and action seam, deterministic seeded runs and replays, enough rounds,
    and exploits treated as bugs.
  - Losing strategies become products or faction behaviours.
  - [L] mind
- **The Colosseum follow-up.** `aetheria-release:follow_up:colosseum-npc-lab` (in force; owner "Aetheria Agents and
  the headless simulation") names its foundations:
  - an observation and action seam (agents hold live `Ship`, `Zone` and `ItemManager` references);
  - seeding `ItemManager.Random` and the MIConvexHull randoms;
  - a replay recorder;
  - a headless tournament runner on Yggdrasil with ratings;
  - a primitive vocabulary;
  - a check of float determinism between Windows and Linux.
  - Why it can wait: "the control rebuild and the drone substrate come first". [L] mind.
  - The release target lists the Colosseum as not in demo scope (`docs/aetheria-release-target.md:148-149`). [L]

### 4.6 Njörðr and Ghostlight: what each owns
- **Njörðr.**
  - **What it is.** The GameCult economy daemon for Aetheria and Delvehold: every item comes from a supply chain,
    materials fill roles, and prices are a pure function of committed state.
    [L] `F:\Projects\Njordr\README.md:1-14`
  - **Engine.** A pure, deterministic step function, embedded in the Aetheria client over a C ABI.
    [L] `AGENTS.md:9-19`
  - **Status.** The spec reads "specification, 2026-09-11. Nothing is built"
    (`docs/architecture/njordr.md:3`). The repo is one scaffold commit, `a5bf35e` (2026-09-11), and its
    `state/map.yaml` says "No step function yet". [L]
  - **Ghostlight link.** Carrier policies are data: online, a Ghostlight carrier subject owns its policy; offline,
    seeded policies run. [L via voidbot, `docs/architecture/njordr.md:482-507`]
- **Ghostlight.**
  - **What it is.** A Rust world kernel. A language model proposes, the kernel checks the proposal against typed
    state, and one `WorldKernel` commits. [L] `F:\Projects\Ghostlight` origin/main `aa56736`, `README.md:1-16`
  - **Its stated direction.** Storylets for Aetheria, a persistent cast, and episodes for a video stream: "None of
    this exists yet. Nothing under `crates/` loads Aetheria lore" (`README.md:75-88`). Ruling
    `ghostlight-verse:ruling:verse-cast-crossmedia` (2026-10-09) names the same three products. [L]
  - **Strategic wave.** Already specified and documented as a pipeline:
    - resolution demand over geography, ideology, authority, economy and information;
    - a deterministic partitioner that builds the active cover;
    - Nemesis, which picks the subjects that "genuinely need a decision window", possibly none;
    - per-cell Projector, then Persona, then Interpreter, in parallel;
    - an effect verifier that stops borrowed authority or knowledge;
    - an outcome resolver and an outcome verifier;
    - an atomic kernel commit.
    - [L] `docs/public-architecture/Runtime pipelines.md:44-62`; stage table at
      `docs/public-architecture/Agents and model stages.md:111-123`
  - **Persona's role.** It lets "each required actor, institution, population, or named member appraise and choose
    independently" (`Agents and model stages.md:117`). [L]
  - **Ledger limit.** Ghostlight's `Custody`/`Transform` is a unitless, one-to-one ledger, so prices, production,
    orders and contracts are unrepresentable in Ghostlight. [L via voidbot,
    `Ghostlight/docs/architecture/delvehold-forced-ontology-integration.md:46`]
  - **Newspapers.** Agent journalists write over a frozen ledger
    (`Agents and model stages.md:125-132`). [L]
- **What each owns that the arena idea touches.** These are facts, not a recommendation.
  - Ghostlight owns the following: in-character appraisal by named actors and institutions through Persona;
    decision windows; durable identity, memory and commitments; prose products (storylets, newspapers, episodes).
  - Aetheria's game state owns the following: zones, entities, doctrine and flights. The game code applies every
    mutation (the operator's "we already control the game state and all its possible mutations").
  - Njörðr owns the following: prices, production, lots and provenance, as a pure step function.

---

## 5. Automated playtesting and simulation-driven game design

- **Jaffe et al., "Evaluating Competitive Game Balance with Restricted Play"** (AIIDE 2012, U. Washington). [L]
  `pdftotext` of https://cdn.aaai.org/ojs/12513/12513-52-16035-1-2-20201228.pdf
  - **Measure.** For a restriction R (never play an action, search only k steps, avoid certain outcome states, always
    pick starting condition s), balance is measured as the probability that the restricted agent A_R beats an
    unrestricted A that knows R and exploits it (§2).
  - **Questions it answers.** "Does every action serve a useful role?"
    - Restricting a piece q no longer lowering win rate flags q as useless.
    - Greedy versus random versus deep search shows how much the game rewards long-term strategy.
  - **Why restricted play.** It compares AI and human *skill levels*, not behaviour, because "AI play is not currently
    predictive of human play" (citing Hingston 2009).
  - **Case study.** About 20 iterations on the card game *Monsters Divided*, each judged by measures such as
    "players who never play Red 1/1 win 11.54%".
  - **Limitations (§5).** Properties "deeply rooted in human psychology, or the look and feel" are out of reach. The
    prototype used optimal agents on perfect-information discrete games. Hand-coded real-time agents would need
    adjusting every balancing iteration.
- **Tomašev, Paquet, Hassabis and Kramnik, "Assessing Game Balance with AlphaZero"** (arXiv 2009.04374, 2020).
  - They compared nine atomic rule variants of chess, including No-castling and Torpedo, by training AlphaZero from
    scratch on each.
  - Findings: piece values differ between variants, and some variants are more decisive than classical chess.
  - The paper calls AlphaZero "an alternative in silico means of game balance assessment".
  - Motivation: heavy opening theory and the high draw rate.
  - [F] https://arxiv.org/abs/2009.04374 ; [S] ChessBase posts
- **Quality-diversity search.**
  - MAP-Elites with Sliding Boundaries (Fontaine, de Mesentier Silva, Lee, Togelius, Soros, Hoover; GECCO 2019) finds
    "diverse ways of playing the game well" along chosen behaviour dimensions. It slides cell boundaries to the
    distribution of evolved individuals, and the paper applies it to Hearthstone design and rebalancing. Its code is
    `tehqin/EvoSabber`. [S] https://arxiv.org/abs/1904.10656
  - "Evolving the Hearthstone Meta" searched card changes that equalise deck win rates. Metagame autobalancing tunes
    rules toward a designer-specified target graph of matchups. [L] colosseum file :102-103
- **Browne: Ludi and Ludii.**
  - *Evolutionary Game Design* (IEEE TCIAIG 2010) defines 57 aesthetic criteria in three groups (intrinsic,
    viability, quality), measured by self-play, and uses them to evolve games. Its traits include drama, depth,
    uncertainty and tension. [S]
    https://gpbib.cs.ucl.ac.uk/gp-html/Browne_2012_sigevolution.html ;
    https://www.maastrichtuniversity.nl/news/ancient-games-and-artificial-intelligence
  - Yavalath, one of Ludi's two picks, is ranked in the top 2.5% of abstract board games. [S] same
  - Four of Browne's measures are restricted-player win rates. [L] Jaffe §4
  - Ludii implements some of the metrics in its Evaluation module, "not complete and ... not tested extensively"
    (Browne, 2022 forum). Ludii's current goal is reconstructing ancient games. [S]
    https://ludii.games/forums/printthread.php?tid=1048 ; https://arxiv.org/pdf/2310.20008
- **Machinations (Dormans).** Diagrams of how resources flow through a game's internal economy. The AIIDE 2011 paper
  "Simulating Mechanics to Study Emergence in Games" added artificial players and charts over many simulated
  sessions. Micro-Machinations (van Rozen and Dormans 2014) runs the diagrams inside the game software. In
  Machinations, users script strategies and observe them. [S] https://ojs.aaai.org/index.php/AIIDE/article/view/12477 ;
  [L] Jaffe §4
- **Smith, Nelson and Mateas.**
  - BIPED, "Computational Support for Play Testing Game Sketches" (AIIDE 2009), lets a game sketch be played by
    humans or analysed automatically on the Ludocore logic engine. It generates play traces that obey designer
    constraints. [S] https://ojs.aaai.org/index.php/AIIDE/article/view/12368 ; [L] Jaffe §4
  - Nelson, "Game Metrics Without Players" (AIIDE workshop 2011), sets out seven strategies for extracting metrics
    from the game artifact itself. [S] https://ojs.aaai.org/index.php/AIIDE/article/view/12479
- **Measures of metagame health in the literature.**
  - *Intransitivity.* Czarnecki et al., "Real World Games Look Like Spinning Tops" (NeurIPS 2020): games of skill
    decompose into a transitive strength axis plus cycles that are widest at middle strength, which is why training
    needs populations of strategies. [S] https://arxiv.org/abs/2004.09468
  - Balduzzi et al.'s Nash averaging corrects Elo for redundant agents. [L] colosseum file :86
  - *Strategy diversity.* QD archives (MAP-Elites) count distinct viable styles. [S] above. The 2025 arXiv paper
    "Beyond Win Rates" clusters character usage for balance in team games. [S] https://arxiv.org/html/2502.01250v2
  - *Dead content.* Jaffe's "never use action a" measure flags content whose removal costs nothing. [L]
  - Borovikov's Lanchester-law and rock-paper-scissors metrics apply to unit balance in a 4X. [L] colosseum file :97
  - *Difficulty distribution.* Isaksen, Gopstein and Nealen (FDG 2015) explored Flappy Bird's parameter space with
    Monte Carlo play by a human motor-skill model and survival analysis. [S]
    https://dblp.org/pid/31/1380 ; https://cs.ubc.ca/event/2015/10/exploring-game-space-using-survival-analysis-talk-aaron-isaksen-1
- **Cases where AI playtesting misled or fell short.**
  - AI play does not predict human play (Hingston 2009, cited in Jaffe §1).
  - Borovikov (EA): agent play was "far from what was observed from human gameplay" unless utility rewards were
    hand-specified. [L] colosseum file :97, :113
  - Exploits humans would not find, or could not execute: OpenAI Five's courier and buyback style, AlphaStar's APM,
    hide-and-seek box surfing, Battlecode mid-contest patches. [L] colosseum file :103-112
  - Industry survey: developers are skeptical of automated testing agents and want clear objectives and oracles.
    [L] colosseum file :106
  - Fun not measured: Jaffe §5 excludes "human psychology, or the look and feel". Browne's aesthetic metrics were
    fitted to human rankings and remain incomplete in Ludii. [L] / [S] above
  - Simulated economies diverging from player behaviour: UO's closed loop, broken by hoarding. [F] Koster
- **Studio use (from the earlier pass).**
  - Riot used self-play to find over- and under-powered decks, producing designer KPIs.
  - Ubisoft's SmartBots run overnight balance reports.
  - King predicts level difficulty from human-like CNN play.
  - [L] colosseum file :93-96

---

## 6. Computable proxies for fun, and how a human's taste calibrates them

The operator added (2026-10-21): "Finding the joy in the game is of course down to taste, but I think agents can do a
lot of the heavy lifting in a structured joy-finding approach, so to speak".

- **Browne's Ludi measures.**
  - Ludi plays each evolved game against itself and scores criteria chosen because human players found them
    interesting: drama, depth, uncertainty and tension. [S]
    https://www.maastrichtuniversity.nl/news/ancient-games-and-artificial-intelligence
  - There are 57 criteria in three groups (intrinsic, viability, quality). [S] section 5 sources
  - A "playability filter" culls about 98% of evolved games, and the survivors are judged on criteria "previously
    correlated with human player scores". [S] https://human-competitive.org/sites/default/files/browne-text.txt
  - The thesis criteria named in the request (lead changes, drama, killer moves, uncertainty) are among them. [mem]
    Their exact definitions were not retrieved this pass; the source is Browne's QUT thesis, eprints.qut.edu.au/17025.
  - Two Ludi games were commercially published. Yavalath is the best known, and is ranked in the top 2.5% of abstract
    board games. [S] same; https://opinionatedgamers.com/2018/04/23/james-nathan-cameron-brownes-automatic-generation-and-evaluation-of-recombination-games/
    The second is Ndengrod (later Pentalath), published by Nestorgames. [mem]
  - Browne also proposed elegance and shibui metrics, largely independent of tractability and quality. [S]
    https://cris.maastrichtuniversity.nl/en/publications/elegance-in-game-design/
- **Yannakakis and Togelius: player experience modelling.**
  - Experience-driven procedural content generation (EDPCG; IEEE Trans. Affective Computing 2(3):147-161, 2011)
    drives generation from computational models of player experience. [S]
    https://pure.itu.dk/en/publications/experience-driven-procedural-content-generation/
  - Player models map gameplay data to annotated experience. One named model type is neuroevolutionary preference
    learning, used as the evaluation function of search-based generation. [S] https://www.pcgbook.com/chapter10.pdf
  - The same authors' *Artificial Intelligence and Games* (2018) treats player modelling as one of three pillars.
    [mem]
  - Their reported practice is that experience labels are better collected as rank or pairwise comparisons than as
    absolute ratings (the "ordinal" line of work, with Martínez). [mem; not confirmed this pass]
- **Decision density and meaningful choice.**
  - No peer-reviewed metric that counts "meaningful choices" in a strategy game was found.
  - Sid Meier's "a game is a series of interesting decisions" (GDC 2012 talk). [mem]
  - Proxies in the literature:
    - Jaffe's depth-k restricted players measure how far ahead play must look, and a greedy versus random gap
      measures how much choice matters. [L] section 5
    - Browne's uncertainty and drama criteria. [S]
    - "Choice poetics" (Mawhorter, MIT thesis) is a qualitative unit of analysis for choices. [S]
      https://dspace.mit.edu/handle/1721.1/118436
    - Instance complexity measures the difficulty of a choice, tested on knapsack problems, not games. [S]
      https://www.biorxiv.org/content/10.1101/405449v2.full
- **Model judges over play traces.**
  - "Evaluating Language Models' Evaluations of Games" (arXiv 2510.10930) compares LLM ratings of game designs,
    made before play, with human ratings. [S] https://arxiv.org/pdf/2510.10930
  - RPGBench (arXiv 2502.00595) has a judge score interestingness, consistency of facts and of personality in role
    play, and action quality, checked against a human study. [S]
  - CausalGame (arXiv 2607.04293) scores agent sessions with a rubric and reports reliability across judge models
    with inter-rater agreement (ICC). [S]
  - GameCraft-Bench (arXiv 2606.17861) reruns a multimodal judge ten times on fixed gameplay evidence to test
    stability. [S]
  - "LLMs may not be human-level players, but they can be testers": LLM agents' performance correlates with how
    difficult humans find a game. [S]
    https://www.promptlayer.com/research-papers/llms-may-not-be-human-level-players-but-they-can-be-testers-measuring-game-difficulty-with-llm-agents
  - "Online Agent-as-a-Judge" (arXiv 2606.08200) covers believability in life-simulation settings. [S]
- **Steering a critic with a few human judgements.**
  - Christiano et al., "Deep RL from Human Preferences" (NeurIPS 2017) learn a reward model from human comparisons
    of pairs of trajectory segments. They give feedback on under 1% of interactions (about 0.1% in the NeurIPS
    listing), with "about an hour of human time" for new behaviours. [S] https://arxiv.org/abs/1706.03741
  - Sentient Sketchbook (Liapis, Yannakakis, Togelius) models designer style "via choice-based interactive
    evolution". When the designer picks some suggestions over others, the tool shifts the weight it gives each
    quality dimension. [S]
    https://mail.antoniosliapis.com/papers/designer_modeling_for_sentient_sketchbook.pdf
  - Galactic Arms Race (Hastings, Guha, Stanley) evolves weapons with cgNEAT from players' past preferences, read
    from use. In a study of over 1,000 online players, players found novel content that also extended weapons they
    had preferred. [S] https://course.ccs.neu.edu/cs5150f13/readings/hastings_gar.pdf
  - Browne's criteria weights were fitted to human rankings of games. [S] above
  - CICERO's piKL regularises a strong policy toward a human-play reference distribution. [S] section 2

---

## 7. Statistical combat models that stand in for simulated fights

The operator added (2026-10-21): "We'll also want to gather combat metagame statistics from the colosseum so we can
build a statistical model of matchups to feed the economy simulation with".

- **Lanchester's laws.** [mem]
  - The linear law (unaimed or area fire, or one-on-one fights): losses scale with the product of the two forces, so
    strength is proportional to N × effectiveness.
  - The square law (aimed fire, where every shooter can engage any target): dA/dt = -βB and dB/dt = -αA, so fighting
    strength is proportional to αA², and concentrating force pays quadratically.
  - Empirical tests are contested. A 1975 RAND critique found no clear verification of the square law, and the
    Dupuy Institute lists tests that could establish neither the square law nor the linear law. [S]
    https://dupuyinstitute.org/category/methodologies/lanchester-equations/
  - Borovikov (EA) used Lanchester-law metrics for unit balance in a 4X. [L] colosseum file :97
- **Matchup and rating models with covariates.**
  - Bradley-Terry: P(i beats j) = e^(βi−βj)/(1+e^(βi−βj)). `BradleyTerry2` models ability as a function of
    covariates, advises adding random effects when it does, and supports contest-level terms such as home advantage.
    [S] https://rdrr.io/cran/BradleyTerry2/man/BTm.html
  - Time-varying Bradley-Terry with team factors has been fitted to the Australian Football League. [S]
    https://arxiv.org/pdf/2405.12588
  - TrueSkill (Herbrich, Minka, Graepel, NIPS 2006) is a Bayesian factor-graph rating with a mean and variance per
    player that handles teams and more than two players. [mem] It is used by Halite and CodinGame.
    [L] colosseum file :39-48
  - Composition models (Dota 2 drafts):
    - Draft-only logistic regression reached about 58-70% accuracy.
    - Conley and Perry's model scored 69.8% but could not capture synergy and counters.
    - Song et al. went from 58% to 61% by adding hero-combination features.
    - dota2-predictor uses hero-by-hero synergy and counter matrices, with ROC AUC about 0.65.
    - Draft-only accuracy is reported as inflated by short matches.
    - [S] https://arxiv.org/pdf/2106.01782 ; https://cs229.stanford.edu/proj2015/249_report.pdf ;
      https://arxiv.org/pdf/1701.03162 ; https://github.com/andreiapostoae/dota2-predictor
  - Per-matchup win rates by race (StarCraft TvZ and the like) are standard community statistics. [mem]
  - Nash averaging and Elo's blindness to intransitivity are covered in section 5.
- **How strategy games resolve off-screen battles.**
  - **X3 out-of-sector (OOS).**
    - The community wiki says out-of-sector combat becomes a turn-based system over the full installed weapon power
      of each ship. It has no collisions, and ships may share a position. [S] https://x3.solasistim.net/index.php/OOS
    - Turns last 30 s, or 5 s when watched on the sector map; watching did not change the outcome in one forum test.
      [S] https://forum.egosoft.com/viewtopic.php?p=2474200
    - Weapons that are carried but not installed are ignored, and only the most damaging missiles matter. [S] same
  - **X4 "low attention".**
    - Egosoft now speaks of high and low attention, set by distance from the player rather than by sector. [S]
      https://steamcommunity.com/app/392160/discussions/0/6664812048260991709
    - Patch 4.20 "improved combat simulation when player not present".
    - Later fixes covered capital-ship movement in low attention and stopped main-cannon damage spreading to several
      targets at once, and station surface-element damage in low attention was reduced by hand. [S]
      https://steamdb.info/patchnotes/7854799 ; Steam changelogs
  - **Total War auto-resolve.** Warhammer III adds spells and abilities as a number to a unit's "Combat Potential",
    because they cannot act in an abstract fight. [S]
    https://steamcommunity.com/app/1142710/discussions/0/4697910068985751182
  - **Stellaris.** The Relative Power labels compare fleet power, economy and technology: Superior or Inferior at
    1.5x, Overwhelming or Pathetic at 2.5x. [S] https://steamcommunity.com/app/281990/discussions/0/4200238856304642295
    Per-hull weights of 1, 4, 8 and 16 are a player's recollection only. [S]
  - **Paradox abstractions.** EU4 battles alternate fire and shock phases of three days each, with a 0-9 die per
    side plus the general's pips, and stacking modifiers. HOI4 compares attack with defence or breakthrough per
    division. [mem]
- **Known complaints when off-screen and on-screen results disagree.**
  - **X3.**
    - OOS fights resolve at a 1:1 ratio "so numbers tend to matter more than ship class", and emergency jumps at
      0% shields were useless OOS. [S] https://steamcommunity.com/app/2820/discussions/0/496880503079542247
    - Missile and loadout behaviour differs from in-sector fights (forum tests above). [S]
    - Exploiting the difference (fighting OOS to win fights that are lost in sector) is a common player practice.
      [mem]
  - **X4.** Destroyers teleport erratically out of sector, and Kha'ak beams hit harder when the player is away. [S]
    Steam discussions above
  - **Total War.**
    - Manual-battle odds come out far below the campaign estimate (30-40% against 55-60%).
    - Auto-resolve ignores units that need manual control or specific tactics, and weighs armour and melee too much.
    - It wipes whole units instead of spreading casualties.
    - It behaves very differently under stat mods.
    - [S] https://steamcommunity.com/app/594570/discussions/0/1480982971175660014 and the other threads in this search
  - **Stellaris.** Fleet power ignores weapon range and composition, so a stronger fleet can lose. An overflow bug
    once made the total negative. [S] Steam threads above
- **How wargame designers fit combat-results tables.**
  - A combat-results table (CRT) reduces attacker and defender strength to an odds column, and terrain and weather
    shift the column. [S] https://pushingcardboard.com/base/glossary/1-combat-results-table
  - Dunnigan's *Complete Wargames Handbook* (1980) has chapters on reading the CRT and on the design process. Its
    derivation method was not retrieved. [S] https://en.wikipedia.org/wiki/The_Complete_Wargames_Handbook
  - Dupuy's Quantified Judgment Model fits combat factors to a historical engagement database.
    - The Joint Staff's 2017 Joint Training Tools reuse its data tables (terrain, weather, water obstacles, advance
      rates). [S] https://dupuyinstitute.org/category/wargaming/page/21/
    - The Orion paper rejects Dupuy's "new square law" as a valid attrition model. [S]
      https://orion.journals.ac.za/pub/article/view/455

---

## Gaps

- No primary developer source was found for X4's `jobs` and `god` mechanics, Bannerlord's issue generator, Dwarf
  Fortress army and site simulation, Space Rangers 2's AI, or Starsector's fleet spawning. Those lines are [mem] or
  [S].
- No GDC talk on the X4 economy was found. Nothing on the generals.io bot API was retrieved.
- The full texts of the Tomašev variant numbers, CICERO's limitations and Browne's 57 criteria were not read.
- Frontier documentation for current BGS mission generation was not found; only 2017-2020 community material was.
- Browne's exact definitions of lead change and killer moves were not retrieved.
- The Lanchester, TrueSkill, EU4 and HOI4 lines are [mem].
- No official formula was found for X3 OOS, X4 low attention, Total War auto-resolve or Stellaris fleet power; only
  community findings were.
- How CRTs are derived (Dunnigan, SPI) was not retrieved.
- No peer-reviewed metric of "meaningful choice" in strategy games was found.

# Prior art for campaign aetheria-fauna (spaceborne microfauna as EW props), 2026-10-13, eyes-fauna-prior-art

Provenance tags: [S] = surfaced by a web search this session (search summaries only, pages not opened); [M] = from memory, not re-fetched. Treat [M] items as pointers to verify, not citations. No recommendations.

## 1. Biological clutter in real sensing

- Radar "angels": WWII-era radar showed pale moving wisps called angels/ghosts/phantoms in Britain; Gray and Lack hypothesised and then confirmed birds as the source. Source: Wikipedia "Radar ornithology" https://en.wikipedia.org/wiki/Radar_ornithology [S]. (Lack and Varley, "Detection of birds by radar", Nature 1945 [M].)
- Birds vs insects vs weather in weather radar: no simple reliable method separates bird from insect echoes; QC for radar winds often flags insect echoes as birds. Source: Muller/ERAU JTECH paper https://wx.erau.edu/faculty/mullerb/Wx365/Radar_insects_birds/JTECH2038.pdf [S, summary only].
- Behavioural discriminators: insects drift with the wind or move slowly; birds and bats fly faster and move relative to the wind. Common filters threshold the standard deviation (texture) of radial velocity and/or absolute airspeed; a newer approach fits a Gaussian mixture to estimate bird/insect proportions per sampled volume. Source: bioRxiv "A simple method to separate birds and insects in single-pol weather radar data" (2021) https://www.biorxiv.org/content/10.1101/2021.03.08.434434.full.pdf [S].
- Dual-polarisation improves classification; most archived data is single-pol. Same bioRxiv source [S].
- Early extraction used reflectivity thresholds plus elevation scans (van Gasteren et al.). Same source [S].
- Wingbeat / micro-Doppler: coherent high-sample-rate radar with time-frequency analysis (STFT, cepstrograms) separates avians, bats, drones by wingbeat frequency, modulation depth, harmonic structure. Source: arXiv 2512.07153 review of bat monitoring sensing https://arxiv.org/pdf/2512.07153 [S]. Drone-vs-bird micro-Doppler is a large literature (e.g. Molchanov et al. 2014 [M]).
- Operational bird/migration products: BirdCast primer https://birdcast.org/weather-surveillance-radar-beyond-meteorology/ [S]; NEXRAD dual-pol hydrometeor/biological classification (Park et al. 2009 [M]).
- Deep scattering layer (DSL): WWII sonar showed a false sea floor ~300-500 m by day, shallower at night; later attributed to mesopelagic animals, chiefly gas-bladdered fish (lanternfish) plus squid/crustaceans; it migrates vertically with daylight. Sources: Wikipedia https://en.wikipedia.org/wiki/Deep_scattering_layer ; UT Marine Science Institute https://utmsi.utexas.edu/science-and-the-sea/radio-program/false-bottom/ ; MBARI https://www.mbari.org/news/undersea-robot-reveals-schools-of-animals-in-deep-scattering-layers/ [S]. Discovery by Eyring, Christensen, Raitt (1948) [M]. Krill are a separate strong acoustic scatterer measured by multi-frequency echosounders (CCAMLR krill biomass surveys; frequency differencing of 120 vs 38 kHz to isolate krill) [M].
- Acoustic classification of biologics: multi-frequency differencing; Kelly Benoit-Bird (MBARI) on layers [S pointer only; specific papers M].
- Submarines/aircraft exploiting biological clutter: NOT confirmed. The search found no source stating submarines deliberately hid in the DSL; only that the Navy treated layers as tactically important. Lore of subs using the layer, and of biologic noise (snapping shrimp, whales) masking, is [M], unsourced. Aircraft exploitation of bird/insect clutter: no source found. Early-warning false alarms from geese and meteors [M], unverified.

## 2. Large-population flocking at game scale

- Reynolds, "Flocks, Herds and Schools: A Distributed Behavioral Model", SIGGRAPH 1987 (separation, alignment, cohesion) [M]; Reynolds "Steering Behaviors For Autonomous Characters", GDC 1999 [M]. Reynolds PlayStation 3 PSCrowd (GDC 2006), ~15,000 boids on Cell with spatial binning [M].
- Neighbour search: uniform grid / spatial hashing is standard; Teschner et al. 2003 "Optimized spatial hashing for collision detection of deformable objects" [M]; Green, "Particle Simulation using CUDA" (NVIDIA 2010) grid + sort [M]; Hoetzlein "Fast Fixed-Radius Nearest Neighbors" (GTC 2014) [M].
- GPU/DOTS: CTU thesis comparing OOP vs Unity DOTS underwater flocking, ECS significantly faster https://dspace.cvut.cz/handle/10467/108795 [S]. Megapop "Life Below" ECS ocean, vertex animation textures for animals and procedural animation for schooling fish https://unity.com/resources/megapop-life-below [S]. Unity EntityComponentSystemSamples boids sample [M].
- Aggregate/continuum LOD: Treuille, Cooper, Popovic "Continuum Crowds", SIGGRAPH 2006 [M]; Narain et al. "Aggregate Dynamics for Dense Crowd Simulation", SIGGRAPH Asia 2009 [M]; Hughes continuum model of crowds 2002 [M]. No game postmortem using these for swarm LOD found.
- Determinism: fixed-step, ordered iteration, seeded PRNG; Glenn Fiedler "Fix Your Timestep" and "Deterministic Lockstep" (Gaffer on Games) [M]; Unity Burst FloatMode.Deterministic (experimental) [M]. Swarm-specific server-authoritative replay: no source found.

## 3. Predator-prey and herd behaviour in shipped games

- Abzu (Giant Squid, 2016): one article claims thousands of fish via shaders plus enhanced boids https://sales.communityimpact.com/unity-boids-flocking.html [S, weak source]. Engine was Unreal, predating Unity DOTS [M]. A Giant Squid talk was not located; check GDC Vault.
- Subnautica (Unknown Worlds, 2018): individual creature AI with spawn/despawn and LOD near the player; leviathans simulated only when loaded [M].
- Elite Dangerous: Thargoid and biological signals are authored encounters/POIs rather than an ecosystem [M]. No Man's Sky: procedural creatures with herd/predator flags, simulated in the loaded region (GDC 2017 talks, Sean Murray) [M]. EVE Online: no biologics; server-side time dilation under load [M]. Spore (2008): authored/procedural creatures, no ecosystem sim [M].
- Horizon Zero Dawn herd AI (Guerrilla, GDC 2017 "Machines of Horizon") [M]; Red Dead Redemption 2 predator-prey chains (press reports) [M]. None fetched.

## 4. Prop hunt design

- Garry's Mod Prop Hunt: hunters lose health when shooting real props (anti-spam tax). Source: Facepunch wiki https://wiki.facepunch.com/gmod/gamemodes:PropHunt [S, snippet].
- Call of Duty Black Ops 4 Prop Hunt (2019): props emit a loud whistle every 20 s after hunter release (tell limiting camping); props get head start, decoys, shape-shifting, stun; props tailored per map. Source: Activision primer https://blog.activision.com/call-of-duty/2019-05/Mode-Spotlight-A-Proper-Primer-on-Prop-Hunt-in-Call-of-Duty-Black-Ops-4 [S]. Ammo-balance commentary: https://www.thegamer.com/call-of-duty-prop-hunt-limited-ammo/ [S].
- History: https://www.blog.radiator.debacle.us/2017/05/from-modders-to-mimics-peoples-history.html [S, not opened].
- Skill vocabulary (mimicry, camouflage, deception via movement/sound) appears only in low-quality SEO guides [S].
- Prop-density postmortem: none found. Dead by Daylight / other hide-and-seek not searched.

## Gaps
No primary-source verification of any [M] item; no source for submarines exploiting biologics; no Abzu/Subnautica talk; no prop-density postmortem; no deterministic-swarm replay source.

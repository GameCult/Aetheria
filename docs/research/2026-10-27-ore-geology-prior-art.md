# Ore geology prior art: Dwarf Fortress, EVE, space games, real bodies, regional-scarcity design

Serves: aetheria-living-world economy batch 2 (ores to alloys) redesign; operator direction 2026-10-27 "closer to the Dwarf Fortress model"

Eyes pass, session self-2026-10-27-ag9. Facts with sources; the "implies" line per system is a design pointer, not a ruling. Evidence grades: **[src]** read from a fetched/searched source this session; **[mem]** from model memory, not re-verified this session (check before relying). The DF wiki rate-limited most direct fetches (HTTP 429), so DF ore data comes from a Japanese mirror of the 0.40.24 table plus search snippets; layer-class membership of individual stones was NOT retrieved directly.

---

## 1. Dwarf Fortress geology

### 1.1 Layers
- Four stone layer types, "very much based on real-life geology": soil, sedimentary, igneous extrusive, igneous intrusive, metamorphic (soil layers hold no ores or gems; they carry clay/sand/aquifer). [src: https://dwarffortresswiki.org/Stone_layers via search snippet]
- Sedimentary sits directly under soil and holds most "shallow metals"; poor in gems; **bituminous coal and lignite occur only in sedimentary**. Igneous extrusive *replaces* the sedimentary layers in some regions (the two never co-occur) and often signals magma nearby. [src: same]
- Layer stones (v0.34 list): andesite, basalt, chalk, chert, claystone, conglomerate, dacite, diorite, dolomite, gabbro, gneiss, granite, limestone, marble, mudstone, obsidian, phyllite, quartzite, rhyolite, rock salt, sandstone, schist, shale, siltstone, slate. [src: https://www.dwarffortresswiki.org/index.php/v0.34:The_Non-Dwarf%27s_Guide_to_Rock]
- Which region (world-gen biome/tile) gets which layer stack is what makes embark sites differ; the player reads the stack on the embark screen before landing. [mem; general DF knowledge]
- Occurrence forms: **V** vein (long, thin, per-layer), **L** large cluster, **S** small cluster; "inclusion" in the DF sense is a mineral occurring inside a specific host stone. [src: ore table below uses V/L/S; wiki-level definitions not fetched]

### 1.2 Ore table (DF2014/0.40.24 data; current v50 may differ)
Source: https://wikiwiki.jp/dwarffort/Ore (Japanese mirror, translated 2017), cross-checked on yields against the v0.34 wiki guide above.

| Ore | Yields | Occurs in | Form |
|---|---|---|---|
| Native copper | Cu | all igneous extrusive; sandstone | V |
| Malachite | Cu | limestone, marble | V |
| Tetrahedrite | Cu + 20% chance bonus Ag per bar (also a Billon route) | **all stone layers** | V |
| Cassiterite | Sn | all alluvial (soil) layers; granite | S in alluvium, V in granite |
| Hematite | Fe | all sedimentary; all igneous extrusive | V |
| Limonite | Fe | all sedimentary | V |
| Magnetite | Fe | all sedimentary | L |
| Galena | Pb + 50% chance bonus Ag per bar | igneous extrusive, all metamorphic, granite, limestone | V |
| Sphalerite | Zn | all metamorphic | V |
| Garnierite | Ni | gabbro only | V |
| Bismuthinite | Bi | granite | S |
| Native silver | Ag | granite, gneiss | V |
| Horn silver | Ag | inside native silver | S |
| Native gold | Au | all stone layers (V) and alluvium (S) | V/S |
| Native platinum | Pt | alluvium and chromite (S); olivine and magnetite (V) | S/V |
| Native aluminum | Al | all igneous extrusive | S |
| Raw adamantine | Adamantine | deep/special | n/a |

Bar yield: galena always gives 4 Pb bars plus 0-4 Ag (50% each); tetrahedrite 4 Cu plus 0-4 Ag (20% each). [src: v0.34 guide]

### 1.3 Flux, fuel, native metals
- Flux stones: calcite, chalk, dolomite, limestone, marble. Fuel: charcoal (from wood), coke from bituminous coal or lignite. [src: v0.34 guide; steel search below]
- Native metals (smelt as metal directly from ore, no gangue): adamantine, aluminum, copper, gold, platinum, silver. [src: v0.34 guide]
- Steel needs flux and fuel at both steps (per wiki search snippet), plus iron. Four of five flux stones and both coal types occur only in sedimentary layers. Without sedimentary layers the only steel paths are: hematite from extrusive layers or imported iron/scrap (goblinite, caravan iron), marble from metamorphic layers or imported flux, charcoal from wood or imported coke. [src: https://dwarffortresswiki.org/index.php/Steel via search]
- Alloys (wiki Alloy/Bronze pages via search; brass and billon ratios not retrieved): bronze = tin + copper (1:1 bars -> 2 bronze; or 1 cassiterite + 1 copper ore -> 8 bars); bismuth bronze = 1 Sn + 2 Cu + 1 Bi (halves tin cost); black bronze = 2 Cu + 1 Ag + 1 Au; brass = Zn + Cu; billon = Ag + Cu (tetrahedrite/galena can stand in for silver). [src: https://dwarffortresswiki.org/Alloy via search]

### 1.4 Embark, scarcity, play consequences
- Embark choice is a geology choice: players look for sedimentary (coal, flux, shallow iron), or extrusive for hematite and native copper; granite for tin/silver/bismuth. [src: search on steel/embark pages; commonsensegamer embark guide returned but unread]
- Tin is the classic bottleneck: cassiterite only in alluvium or granite, "neither guaranteed at every embark". Iron is usually the first ore reached; copper/tin often sit deeper. Bronze can be bootstrapped from a few embark-bought ore units (about 24 points for up to 8 bronze items, v0.34). [src: Steam discussion + v0.34 "Make your own weapons" via search]
- Caravans are the fallback when a site lacks a resource: trade goods include iron ore, flux, coal at low prices (coal about 3 value units). [src: steel search snippet]
- Consequence players describe: a missing flux/fuel/tin changes the fortress's whole industrial path (charcoal forests, magma smelters, import dependence), not just a price. No dedicated source found on tin-less trade strategy. [src: negative result]

**Implies for a design:** DF's regional identity comes from *host layer -> ore*, many ores per metal (Fe has 3, Cu has 3 plus tetrahedrite), a few "everywhere" ores (tetrahedrite, gold), native metals that skip smelting, co-products (Ag from Pb/Cu ores), and a separate flux/fuel axis that gates steel. Alloys then multiply scarcity (bronze needs two geologies).

---

## 2. EVE Online

- Ore -> mineral by reprocessing: 8 base minerals (Tritanium, Pyerite, Mexallon, Isogen, Nocxium, Zydrine, Megacyte, Morphite); every ore yields a fixed *mix* of them, so many ores feed one mineral. Examples: Tritanium from Veldspar, Scordite, Plagioclase, Spodumain; Megacyte from Spodumain, Arkonor, Ducinium, Bezdnacine, Talassonite, Ueganite; Morphite from Mercoxit only. [src: https://wiki.eveuniversity.org/Ore]
- Geography by security class: high-sec has Veldspar, Scordite (Pyroxeres, Plagioclase in some); low-sec adds Kernite, Jaspet, Omber, Hemorphite, Hedbergite; null-sec adds Bistot, Arkonor, Mercoxit; wormholes, Pochven (Talassonite, Rakovene, Bezdnacine, Spodumain, Mercoxit), border systems (Ducinium, Eifyrium, Mordunium, Ytirium) have their own. Rule of thumb: "the further from Empire, the more valuable the ore." [src: EVE Univ page + search]
- Rarest mineral is gated by a skill/module barrier too: Mercoxit needs Deep Core Mining and special modules. [src]
- CCP removed Veldspar and Spodumain from null-sec belts (17 Dec 2019) and cut level 3/4/5 anomaly ore 33/50/66%, to lower availability of some ores in null. [src: https://www.eveonline.com/article/q2tmpn]
- Moon minerals (separate system): CCP's Odyssey-era blog says Technetium was "relatively common and regionally concentrated" and the redesign moved value to the rarest R64 minerals, seeding R64 into 227 existing low/null moons; a 2012 player post argues moons drive conflict as alliance wealth, with the bottleneck moving (dysprosium, then technetium). [src: https://www.eveonline.com/news/view/resource-companion-blog via search; forums-archive.eveonline.com/message/1603898]
- No source retrieved on trade-route structure; the regional-mineral-monopoly claim rests on the CCP "regionally concentrated" wording only.

**Implies:** EVE separates *ore* (place-bound, many kinds) from *mineral* (shared commodity), joined by a reprocessing recipe with fixed mixes. Hub minerals are common, premium minerals are tied to dangerous or remote space. This is the "many ores -> shared commodity" shape the operator rejected in reverse (generic rock -> many metals); EVE is the inverse: *specific ore -> several shared minerals*.

---

## 3. Other space games (brief)

- **X4: Foundations:** economy built on Helium, Hydrogen, Ice, Methane, Ore, Silicon; Nividium is niche (luxury/research, tiny demand, limited Grand Exchange market). Sectors are uneven: some are rich in many resources (Trinity Sanctum VII, Eighteen Billion), others specialise (Holy Vision: Hydrogen+Silicon; Pious Mist IV: Methane, Ore, Silicon; Galactic Exchange I: most Ice). Large fields hold small high-density pockets; yield is depleted and regenerates. Yields changed in patch 4.0. [src: Steam discussions via search; community-level, not Egosoft docs]
- **Elite Dangerous:** ring type matters: metallic rings are for laser mining (platinum laser-minable only there); metal-rich rings give minerals via core mining only; hotspots matter mostly for core mining; players report "dry" rings missing promised minerals; meta rotates (void opals -> painite -> platinum). [src: Steam threads 2019-2022 via search; dated, anecdotal]
- **Starsector:** each body carries abundance ratings: ore and rare ore -1..+3; food, volatiles, organics -1..+2; Mining requires a deposit of ore, transplutonic ore, volatiles or organics; volatiles concentrate on gas/ice giants and frozen/toxic/cryovolcanic worlds; industries are gated or scaled by these conditions. [src: https://starsector.wiki.gg/wiki/Mining and /Volatiles via search; page flagged possibly outdated, 0.9.1]
- **Stellaris:** strategic resources are a separate category (Volatile Motes, Exotic Gases, Rare Crystals; Zro, Dark Matter, Living Metal). No distribution rules retrieved. [src: low-grade]

**Implies:** the games use either body-type tags (Starsector, Elite) or sector-level richness (X4) as the host for resource identity; none matches DF's per-ore host-layer table in granularity.

---

## 4. Real asteroid and planetary-body mineralogy

### 4.1 Taxonomy
- Bowell-era classes (C, S, M, E, R...) were defined from spectra and albedo and are deliberately independent of mineralogy; M-complex membership alone does not fix composition. [src: https://sci.esa.int/web/rosetta/-/47395-bowell-e-et-al-1978 via search]
- **C-type:** low albedo; analog carbonaceous chondrites; hydrated minerals, organics, possibly water ice. [src: arXiv polarimetry review via search]
- CI/CM chondrites: 3-22% water (CI about 22%) in hydrous phyllosilicates (serpentine), up to 3% carbon (kerogen-like organics, carbonates, graphite), magnetite, sulphides, olivine. Nickel-iron metal is *not* a feature of CI/CM (it is of CB/CH). [src: https://en.wikipedia.org/wiki/Carbonaceous_chondrite]
- **S-type:** silicate-rich, ordinary chondrite analogs. H chondrite mass-% (McSween 1991 as quoted): olivine 35.0, pyroxene 26.2, Fe-rich metal 18.0, feldspar 9.6, troilite (FeS) 5.5, diopside 4.1; metal about 8.4 vol% H, 4.1 L, 2 LL; L/LL similar mineralogy with less metal. [src: arXiv 1611.08734 family via search]
- **M-type:** spectra like iron meteorites or enstatite chondrites; inference is indirect (radar albedo). 16 Psyche about 222 km; measured densities (Psyche 3.8, Kalliope 4.1, Lutetia 3.4, Kleopatra 3.4 g/cm3) are too low for pure Fe-Ni (about 8), suggesting porous, silicate-bearing rubble; some show 3 um hydration. [src: https://en.wikipedia.org/wiki/M-type_asteroid]
- Iron meteorites (the real metal-rich analog): 70-95% Fe, 5-30% Ni, 0.2-2% Co; two alloys, kamacite (lower Ni, about 3.3-5.8% Ni in hexahedrites/octahedrites) and taenite (higher Ni); Co goes mostly in kamacite; Ir/Os/Pt strongly depleted in Ni-rich phases, Pd enriched; Ir varies by group from about 0.01 to >10 ppm. [src: wustl meteorite site, Caltech 1958, Southampton eprints, via search]

### 4.2 Moon
- Highland and mare regolith share one mineral inventory (plagioclase, olivine, augite, orthopyroxene, pigeonite, ilmenite, chromite, quartz, cristobalite, whitlockite), differing in ratios; oxygen 41-45%; mare is split into high/low/very-low Ti by ilmenite content. [src: https://en.wikipedia.org/wiki/Lunar_regolith]
- Ilmenite: 10-25 wt% in iron-rich mare basalts; 31.6% Ti and 36.8% Fe by mass; Fe is the easiest metal to win from regolith (NASA SP-509). Aluminium route: plagioclase concentrate (anorthite) by carbochlorination plus electrolysis; molten regolith electrolysis yields ferrosilicon alloys from highland soil (Guerrero-Gonzalez & Zabel, Acta Astronautica 2023). [src: https://arxiv.org/pdf/2408.05823, nss.org SP-509 extraction schemes, via search]
- KREEP (K, rare earths, P): about 1% K2O+P2O5, hosted in apatite/merrillite; also U, Th, 40K; concentrated under Oceanus Procellarum / Mare Imbrium (Procellarum KREEP Terrane), little in Crisium, Orientale, South Pole-Aitken. Low concentrations vs terrestrial ores. [src: https://en.wikipedia.org/wiki/KREEP]
- Polar permanently shadowed craters hold ice. [src: Lunar regolith page]

### 4.3 Plausible "ore" mapping per element (cross-element mapping is mine; individual facts sourced as marked)
- **Iron:** kamacite/taenite (S, M type, iron meteorite), magnetite (C-type, CI), ilmenite (Moon mare), troilite as a sulfur/iron source. [src as above]
- **Nickel:** taenite (high Ni) and kamacite; also an iron-meteorite co-product with Co. [src]
- **Titanium:** ilmenite (31.6% Ti). [src]
- **Aluminium:** anorthite/plagioclase, lunar highlands (the 8500 ppm CI Al figure shows chondrites carry it only thinly). [src: arXiv 1010.2746 for CI Al about 8500 ppm; anorthite route above]
- **Potassium:** KREEP (apatite/merrillite hosts, region-specific); CI chondrite K about 544 ppm (aqueously mobile, uncertain). [src: KREEP page; arXiv 1010.2746]
- **Uranium/thorium:** KREEP. [src]
- **PGMs:** iron-meteorite metal phases, with Pt-group fractionation between kamacite/taenite/plessite; no ore-grade table retrieved. [src: Southampton; gap]
- **Copper, gold:** not retrieved. From memory: Cu is a chalcophile element sitting in sulfide (chalcopyrite, troilite inclusions) at about 100-150 ppm in CI chondrites; Au about 0.15 ppm; Pt about 1 ppm; U about 0.007 ppm. [mem; Lodders et al. CI table not read this session, verify before use]
- **Volatiles/organics:** C-type phyllosilicate water, carbonates, kerogen-like carbon; ice at lunar poles. [src]

**Implies:** real bodies give regional identity by *body class* (C: volatiles and magnetite; S: olivine/pyroxene silicate plus some metal; M: Fe-Ni metal; Moon: Ti via ilmenite, Al via anorthite, K/P/U via KREEP location), not by a rock-layer stack. For rare metals (Cu, Au, PGM) real accumulation is trace-level, which in a game forces either native-metal "nuggets" in metal-rich bodies (DF's native gold/platinum analog) or a sulfide co-product (DF's tetrahedrite-silver analog).

---

## 5. Design writing on resource geography

- Trade pattern literature (Eliens, "pattern trading"): asymmetric resource distribution is one of the most effective incentives for trading, citing Catan, where randomness in resource generation (progress timed so it does not even out) creates the incentive; Catan's bank trade exists to balance radical asymmetries and avoid strategies that block progression entirely. [src: https://math.vu.nl/~eliens/media/pattern-trading.html via search]
- Civilization (V): bonus / strategic / luxury classes; strategic and luxury resources are counted per empire (not per source location), tradable between empires; a tile must be improved to access it; each source grants a limited quantity (preview: one iron deposit supports a few iron units); losing it penalises existing units. Civ community: regional scarcity encourages trade, and a player reports older versions guaranteed iron or copper near start, later versions did not; "each continent with its own set of resources makes interesting diplomatic issues." [src: civilization.fandom.com list of resources; civfanatics, Steam threads via search; no Firaxis rationale found]
- Factorio: vanilla ore richness rises roughly linearly with distance from spawn (plus a small bonus near spawn), so patches far away are richer; Oreverhaul makes start patches poor and far ones rich to push trains; Lean Resources flattens the curve. [src: forums.factorio.com/viewtopic.php?p=248111; mods.factorio.com/mod/Oreverhaul and /red-rafe-resources via search]
- Anno 1800: Old World and New World are separate regions; the New World makes goods (cotton, tobacco, gold named in the dev blog) the Old World's workers demand, requiring routes between regions; players report logistics overload (20-30 routes for ten goods; ships jam; blockades lengthen routes). [src: anno-union.com dev blog and Steam threads via search; which goods are region-locked was not confirmed]
- Frustration threshold reports: Farthest Frontier player complained about trading for basics; reply: maps are not meant to have all resources. Captain of Industry player: scarcity fine, but should not make completing tech nearly impossible. [src: Steam threads via search]
- Observed in the above: scarcity reads as trade when (a) the scarce item is not a hard gate on basic progress, (b) a fallback (bank trade, caravan, buying) exists, (c) the gap is closable by an action the player can take, and logistics cost is bounded. These are the sources' patterns, not tested rules.

---

## Cross-system comparison (facts only)

| System | Identity unit | Metal count per host | Shared intermediate | Gate beyond ore |
|---|---|---|---|---|
| DF | rock layer / host stone -> ore | 3 Fe ores, 3+ Cu ores, 1 Sn/Zn/Ni etc. | metal bars, alloys | flux + fuel |
| EVE | ore type by security/region | each ore -> several minerals | 8 minerals | skill/module (Mercoxit) |
| Starsector | body abundance tags | tag ranges | commodity | colony conditions |
| X4 | sector pockets | 6 raw types | n/a | station setup |
| Real bodies | body class and mineral | few (Fe-Ni, ilmenite, anorthite) | n/a | extraction chemistry |

## Gaps to close if the designer needs them
1. DF current-version (v50) ore table, exact layer membership of each stone (sedimentary/extrusive/intrusive/metamorphic), steel/pig-iron recipe, brass and billon ratios, vein-vs-cluster frequency. Wiki returned 429; retry Ore, Stone_layers, Metal_industry, Alloy later or use raw files (`inorganic_stone_layer.txt`, `inorganic_metal.txt`, `inorganic_stone_mineral.txt` in the DF install).
2. CI/ordinary chondrite Cu, Au, PGM, U ppm (Lodders, Bergemann and Palme 2025, Space Science Reviews 221:23; arXiv 1010.2746 Table 2).
3. Elite Dangerous and X4 official material counts; Stellaris distribution rules; Anno region-lock list.

using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;

// Economy content: ores and provinces (rulings ores-are-regional, ores-fill-metal-roles, provinces-within-regions,
// province-types-dealt-per-region, everyone-mines, new-metals-is-nickel-iron). It comes before ores-to-alloys, which is
// re-authored on top of it.
//
// The model is Dwarf Fortress's, moved to asteroids (docs/research/2026-10-27-ore-geology-prior-art.md). In DF a rock layer
// decides which ores a site holds and many named ores smelt into a few shared metals. Here a belt's field kind is its body
// class, read from real asteroid taxonomy (carbonaceous C, stony S, metallic M, basaltic V), and a province is the kind of
// ground a region's zones sit on: which body classes its belts draw and which regional ores exist there at all. The ground
// tells one story: the belts are the debris of protoplanets that differentiated and then broke, so a core province is
// metal, a crust province is basalt, and the incompatible elements (uranium, rare earths, niobium, tungsten) sit only in
// the last crust to freeze. Common ground (primitive and chondrite) is dealt most; specialty ground is dealt less, so a region
// holds a spread and the specialty ore it lacks must come by trade.
//
// Ores are named minerals, each a raw good the mining station extracts. A metal's recipe takes that metal's ore role, and an
// ore fills the role of every metal that is at least a tenth of the mineral by mass (ruling ores-fill-metal-roles: whatever else
// the ore holds is slag for that recipe). So ilmenite is both titanium ore and iron ore, and which it becomes is the smelter's
// choice. Grade is not modelled per role, by that ruling; how rich a belt is lives in its yield.
//
// This batch owns extraction and the first smelting step: every ore, the metal each role feeds, the field kinds and the
// province types. It owns nothing downstream of a metal (steel, alloys, mill products are ores-to-alloys). Every faction places
// mining (ruling everyone-mines): a Mega controls territory and the resources in it. A faction with no station model stands in on
// the supply depot (ruling faction-station-models), Aeronautics Unlimited on its trade hub (ruling station-kind-family-stand-in).
//
// Apply list (Hands makes these code changes when the batch applies; they are not in this file):
// 1. Follow-up negative-authored-weights: the catalog check refuses a negative or non-finite FieldKindData.GenerationWeight,
//    ProvinceData.GenerationWeight, ProvinceKindWeight.Weight, ProvinceOre.Weight and FieldYield.Abundance, and a yield, kind
//    weight or province ore naming no record; the rule is a shape invariant, so a batch that introduces one is refused.
// 2. StationKindsCatalog's mining row says what this batch writes, so a fresh catalog and an authored one agree.
// 3. The AetherDb field-kinds command (Program.cs FieldKindsCatalog) is deleted: this batch is the one writer of field kinds,
//    and the command would author a second "Asteroid" beside the renamed record.
public sealed class OresAndProvinces : EconomyBatch
{
    private EconomyAuthor _a;

    // Records this batch renames, found by key so a rerun converges after the rename.
    private static readonly CultRecordKey NewMetalsKey = new CultRecordKey("00b5b3b2-2fc5-4adb-a1d7-c1a35d19afd3");
    private static readonly CultRecordKey RockKey = new CultRecordKey("043997a8-0661-4206-ab8a-10215ecf4efc");
    private static readonly CultRecordKey AsteroidKindKey = new CultRecordKey("2b0deb8dd4ca44da9a36581877bf629d");

    public override void Author(EconomyAuthor author)
    {
        _a = author;

        foreach (var faction in _a.Catalog.GetAll<Faction>().OrderBy(f => f.Name, StringComparer.Ordinal).ToArray())
            StandIn("mining", new CultRecordRef<Faction>(_a.Catalog.RefOf(faction).Key), faction.Name == "Aeronautics Unlimited" ? "trade-hub" : "depot");

        var miningKey = StationKindData.KeyOf("mining");
        var mining = _a.Catalog.Get<StationKindData>(miningKey);
        mining.Description = "Extracts ore from belts: regolith, iron and nickel minerals everywhere, and the regional ores (copper, titanium, platinum, gold, uranium, rare earths, niobium, tungsten, potash) only where the ground holds them; consumes tools and food.";
        _a.Put(mining, miningKey);

        // ---- Ores: the raw goods. Fills are metal roles; abundance is the field kinds' business, below.

        var regolith = Rename(RockKey, "Regolith", "Crushed silicate rubble and dust, the bulk of every belt: what is left when the ore is picked out, and the aggregate cast habitats and berms are made of.", 1f, 1, 14, 6,
            "Regolith is what every belt is mostly made of, and nobody fights over it. Orbital Forge casts it into habitat shells on a cage of rebar, so a hauler full of regolith heading for an empty rock means someone means to live there.");

        var nickelIron = Rename(NewMetalsKey, "Nickel-Iron", "Native nickel-iron, kamacite and taenite, cut from metallic asteroids: about nine parts iron to one of nickel, with a trace of cobalt and platinum.", 1f, 10, 10, 4,
            "Metallic asteroids are the cores of broken protoplanets, iron that cooled so slowly it crystallised in plates a hand wide. The cut face rings when struck, and crews price it by the cut, not the tonne.",
            "iron ore", "nickel ore");

        var troilite = Ore("troilite", "Troilite", "Iron sulphide (FeS) in grains and nodules through stony asteroids: iron ore, with the sulphur driven off as gas.", 1f, 4, 10, 4,
            "Troilite is the bronze fleck in a broken chondrite, and the reason a stony-belt smelter smells of sulphur. It is poor iron, but it is everywhere, and a habitat that cannot buy iron can always roast troilite.",
            "iron ore");

        var magnetite = Ore("magnetite", "Magnetite", "Iron oxide (Fe3O4) grown in the wet clays of carbonaceous asteroids: rich iron ore that a magnet lifts straight out of the crushed rock.", 1f, 6, 10, 4,
            "Carbonaceous rocks were once wet, and the water grew magnetite in them as framboids and plates. Crews in the dark belts sweep crushed rock with a magnet drum and keep what sticks; the rest of the rock is somebody else's water.",
            "iron ore");

        var ilmenite = Ore("ilmenite", "Ilmenite", "Iron-titanium oxide (FeTiO3), the black sand of basaltic crust: titanium ore and iron ore at once.", 1f, 14, 12, 4,
            "Ilmenite is the dark sand of old basalt flows, and every grain is both titanium and iron. Which one a smelter takes out of it says what its owner is short of this year, so a buyer's choice of ilmenite contract is read like a budget.",
            "titanium ore", "iron ore");

        var anorthite = Ore("anorthite", "Anorthite", "Calcium plagioclase (CaAl2Si2O8), the pale feldspar of anorthosite crust: the aluminium ore that molten-salt cells take apart.", 1f, 6, 12, 4,
            "Anorthosite is the pale crust of a body that once had a magma ocean, the feldspar that floated to the top. Aluminium comes out of it only at a great cost in power, so where anorthite is cheap, ask where the power is coming from.",
            "aluminium ore");

        var pentlandite = Ore("pentlandite", "Pentlandite", "Iron-nickel sulphide ((Fe,Ni)9S8), bronze-yellow grains in sulphide melt and carbonaceous rock: the richest nickel ore, and iron ore besides.", 1f, 12, 12, 4,
            "Pentlandite settles out of a sulphide melt like cream, so the sulphide provinces where an impact once melted a body are where nickel is cheap. Crews call a good pentlandite seam a cream line.",
            "nickel ore", "iron ore");

        var chalcopyrite = Ore("chalcopyrite", "Chalcopyrite", "Copper-iron sulphide (CuFeS2), brassy and iridescent: the common copper ore, found only where a sulphide melt pooled.", 1f, 12, 12, 4,
            "Copper is scarce in the belts, and almost all of it is chalcopyrite in a sulphide province. Every wire, coil and heat pipe draws on it, so the regions that hold one trade copper for everything else, and the regions that do not wait in yards for their wiring.",
            "copper ore", "iron ore");

        var nativeCopper = Ore("native-copper", "Native Copper", "Metallic copper in sheets and wires through the vesicles of basalt, where hot water once carried it.", 1f, 16, 10, 2,
            "Native copper lies in the old lava as sheets you can peel; the oldest Belt prospectors' stories are of native copper found in a vent pipe. It is rare, rich and hand-picked, and a find is kept quiet until the claim is filed.",
            "copper ore");

        var sylvite = Ore("sylvite", "Sylvite", "Potassium chloride (KCl) crystallised from the brines of wet carbonaceous bodies: potash ore.", 1f, 2, 8, 4,
            "The wet bodies left salt where their brines froze out, the way Ceres wears its white spots. Farms buy sylvite first and arsenals second: the potassium that grows a habitat's algae is the potassium in its primers.",
            "potassium ore");

        var orthoclase = Ore("orthoclase", "Orthoclase", "Potassium feldspar (KAlSi3O8) from evolved granitic crust: a lean potash ore, worked where no brine salt is.", 1f, 1, 10, 4,
            "Potassium feldspar is potash the hard way, cooked out of granite. Regions with no salt flats grind it anyway, and a farm co-op that buys orthoclase is a co-op whose neighbours have closed the salt trade to it.",
            "potassium ore");

        var nativeGold = Ore("native-gold", "Native Gold", "Gold, alloyed with a little silver, in grains and nuggets through the metal of core fragments.", 0.2f, 350, 6, 1,
            "Gold sank with the iron when the protoplanets melted, so it is found in their broken cores, never in their crust. It wires every contact that must not corrode, and it is still the one metal people hoard without being told to.",
            "gold ore");

        var isoferroplatinum = Ore("isoferroplatinum", "Isoferroplatinum", "Platinum-iron alloy (Pt3Fe) in grains and nuggets, with iridium, osmium and rhodium: the native platinum of core fragments.", 0.2f, 650, 6, 1,
            "Platinum came down with the iron, like the gold, and sits in nuggets where a core broke. A single nugget can pay a crew's season; a platinum strike in a contested core province has started more than one war that its history books call a border dispute.",
            "platinum ore");

        var sperrylite = Ore("sperrylite", "Sperrylite", "Platinum arsenide (PtAs2), small tin-white crystals in sulphide ore: a platinum ore that comes out of the same melt as the nickel.", 0.3f, 450, 8, 1,
            "Sperrylite is platinum that rode up with the sulphide melt instead of sinking with the core, so a sulphide province holds platinum as well as copper and nickel. The arsenic that comes off in the roaster is why its smelters stand downwind of everything.",
            "platinum ore");

        var uraninite = Ore("uraninite", "Uraninite", "Uranium dioxide (UO2), pitch-black and heavy, in veins through the last crust to freeze: uranium ore.", 1f, 60, 10, 2,
            "Uranium is an incompatible element: it would not fit in any early crystal, so it ended up in the last melt to freeze, and only evolved crust holds it. It is mined under licence and shipped under seal, and a broken seal is a crime in every bloc.",
            "uranium ore");

        var brannerite = Ore("brannerite", "Brannerite", "Uranium titanate (UTi2O6), black and glassy: uranium ore and titanium ore in one, worked for both.", 1f, 45, 10, 2,
            "Brannerite is uranium locked in titanium oxide, hard to leach and worth it twice. A refinery that buys brannerite and sells only titanium has a uranium stockpile somewhere, and the licensing offices know to ask where.",
            "uranium ore", "titanium ore");

        var monazite = Ore("monazite", "Monazite", "Rare-earth phosphate ((Ce,La,Nd)PO4) with a little thorium, honey-coloured grains in evolved crust: the rare-earth ore.", 1f, 80, 10, 2,
            "Neodymium from monazite is in every strong magnet, from an actuator to a drive pump, and the rare earths are in the premium superconductor tape that Cryonix certifies. The monazite crust provinces are few, so whoever holds one has a hand on every motor in the region.",
            "rare earth ore");

        var columbite = Ore("columbite", "Columbite", "Iron niobate (FeNb2O6), black tabular crystals from the pegmatites of evolved crust: niobium ore, with iron besides.", 1f, 100, 10, 2,
            "Niobium is the conductor in the coils every yard but Cryonix's can wind: niobium-tin and niobium-titanium, heavier and hungrier for cold than the premium tape, and free of its licence. Columbite is how the gray-market fusion trade stays alive.",
            "niobium ore", "iron ore");

        var wolframite = Ore("wolframite", "Wolframite", "Iron tungstate (FeWO4), dense black blades from the vein ends of evolved crust: tungsten ore, with iron besides.", 1f, 60, 10, 2,
            "Tungsten faces the plasma in every reactor and lines the throat of every hot nozzle; Orbital Forge's ugly refractory hardware is mostly tungsten. Wolframite is heavy for its size, so its haulers ride low on thrust and are easy prey.",
            "tungsten ore", "iron ore");

        // ---- Metals: the first smelting step. Each takes its ore role; downstream of a metal is ores-to-alloys.

        Metal("iron", "Iron", "Reduced iron pellet and sponge, smelted from whatever iron ore the furnace was fed; the rest of the ore leaves as slag.", 1f, 22, 3, 2, 600, 14,
            "Iron is the one metal every province can make, from nickel-iron, troilite, magnetite or the iron in a copper ore, and the dry-refinery stack TerraCore's patents spread across Luna and the Belt still runs on all of them. A region short of iron is short of something else: power, crews or peace.",
            "iron ore");

        Metal("nickel", "Nickel", "Carbonyl nickel pellets, drawn off as a gas from nickel ore and redeposited almost pure.", 1f, 60, 4, 1, 900, 14,
            "Nickel comes off the carbonyl line as a poisonous gas and settles back as pellets: the same carbon monoxide that does it kills a crew that forgets its sensors. Every stainless fitting, hot alloy and battery plate in a habitat starts as one of these pellets.",
            "nickel ore");

        Metal("copper", "Copper", "Cathode copper: sulphide ore roasted and smelted, or native copper melted, then electrorefined.", 1f, 70, 4, 1, 900, 16,
            "Copper travels further than any other base metal because so few provinces hold it. A copper smelter in a region without a sulphide province runs on ore that crossed a boss line, and its prices carry the escort's fee.",
            "copper ore");

        Metal("aluminium", "Aluminium", "Aluminium ingot, reduced from anorthite in molten-salt electrolysis cells.", 0.4f, 50, 5, 1, 900, 12,
            "Aluminium is electricity made solid: the cells that win it from feldspar draw more power than the habitat beside them. Where a refinery sells aluminium cheap, its power is cheap too, and someone should ask why.",
            "aluminium ore");

        Metal("titanium", "Titanium", "Titanium sponge, reduced from ilmenite or brannerite in molten-salt cells and pressed into billet.", 0.5f, 140, 5, 1, 1200, 14,
            "Titanium is reduced a few kilograms at a time in salt baths that run for days, so a sponge shipment is small, heavy with value and always insured. Basalt-crust provinces make most of it; the brannerite crust makes the rest, and pays its licence fees twice.",
            "titanium ore");

        Metal("platinum-group-metals", "Platinum Group Metals", "Platinum with iridium, palladium, rhodium and osmium, refined to sponge from platinum ore.", 0.1f, 2400, 3, 1, 1800, 10,
            "Platinum is the catalyst gauze that turns ammonia into nitric acid, the electrode in a fuel cell and the contact in a sensor that must not drift. Every fertiliser plant and every arsenal in a region depends on a few kilograms of it, which is why a platinum province is guarded like a gate.",
            "platinum ore");

        Smelt("Gold", "Gold bullion, melted out of native gold and parted from its silver.", 0.2f, 900, 2, 1, 600, 6,
            "Gold is refined where it is mined, under guard, because a tonne of native gold ore is a crew's lifetime of wages. It goes into contacts and connectors, and into bars that nobody uses for anything at all.",
            "gold ore");

        Smelt("Uranium", "Uranium ore concentrate: yellowcake, leached from uranium ore and sealed in drums.", 0.5f, 150, 2, 1, 900, 10,
            "Yellowcake is milled at the mine head because nobody wants to haul the waste rock, and sealed there because nobody trusts the haulers. A drum's seal number is its whole history, and an unbroken seal on a drum that never reached its reactor is a better story than a broken one.",
            "uranium ore");

        Smelt("Potassium", "Potash: potassium chloride and carbonate refined from potassium ore, fertiliser and chemical grade.", 1f, 6, 2, 1, 300, 6,
            "Potash feeds farms first and arsenals second: the saltpetre in a primer is made from the same salt that grows a habitat's algae. A region with no brine-salt ground grinds feldspar for it, and pays for that in power.",
            "potassium ore");

        Metal("rare-earth-oxides", "Rare Earth Oxides", "Separated neodymium, praseodymium, lanthanum and cerium oxides, cracked from monazite and split by solvent extraction.", 0.5f, 320, 3, 1, 1800, 18,
            "Splitting the rare earths takes hundreds of solvent stages, each a little better than the last, and the thorium that comes off is someone's licensing problem. A separation plant is the most valuable building in an evolved-crust province, and the most watched.",
            "rare earth ore");

        Metal("niobium", "Niobium", "Niobium metal, reduced from columbite and electron-beam remelted to conductor grade.", 0.5f, 380, 3, 1, 1500, 14,
            "Niobium wire is what a fusion plant without a Cryonix contract runs on. It is heavier and colder to run than the premium tape, but no inspector can take it away, so the regions that hold columbite keep the independent reactors lit.",
            "niobium ore");

        Metal("tungsten", "Tungsten", "Tungsten powder and pressed bar, reduced from wolframite.", 1f, 240, 3, 1, 1200, 14,
            "Tungsten is the wall between a reactor's plasma and everything else, and it wears: every plasma-facing tile is replaced on a schedule. A region that cannot buy tungsten has a date when its reactors go dark, and its neighbours know the date.",
            "tungsten ore");

        // ---- Field kinds: a belt's body class. CrossSection is reflectivity per cell, scaled to albedo with stony at the 5 it had.

        var stony = Kind(AsteroidKindKey, "Stony", 5f, 2f,
            (regolith, 6f, false), (troilite, 1.5f, false), (nickelIron, 0.8f, false),
            (pentlandite, 0.6f, true), (chalcopyrite, 0.5f, true), (sperrylite, 0.1f, true));
        var carbonaceous = Kind(new CultRecordKey("field-kind:carbonaceous"), "Carbonaceous", 1.5f, 4f,
            (regolith, 5f, false), (magnetite, 1.5f, false), (pentlandite, 0.5f, false), (sylvite, 1f, true));
        var metallic = Kind(new CultRecordKey("field-kind:metallic"), "Metallic", 4f, 0.5f,
            (nickelIron, 5f, false), (troilite, 0.8f, false), (nativeGold, 0.15f, true), (isoferroplatinum, 0.1f, true));
        var basaltic = Kind(new CultRecordKey("field-kind:basaltic"), "Basaltic", 9f, 0.3f,
            (regolith, 5f, false), (ilmenite, 1.5f, false), (anorthite, 2f, false),
            (nativeCopper, 0.3f, true), (chalcopyrite, 0.3f, true), (orthoclase, 0.8f, true),
            (uraninite, 0.15f, true), (brannerite, 0.15f, true), (monazite, 0.2f, true), (columbite, 0.12f, true), (wolframite, 0.15f, true));

        // ---- Province types: dealt per region from a deck of these card counts (ruling province-types-dealt-per-region).

        Province("primitive-reach", "Primitive Reach", 3,
            new[] { (carbonaceous, 2f), (stony, 0.5f), (metallic, 0.2f), (basaltic, 0.1f) },
            new[] { (magnetite, 1.5f), (sylvite, 1f), (pentlandite, 1f) });
        Province("chondrite-plain", "Chondrite Plain", 3,
            new[] { (stony, 3f), (carbonaceous, 1f), (metallic, 0.5f), (basaltic, 0.3f) },
            new[] { (troilite, 1.5f), (nickelIron, 1.2f) });
        Province("shattered-core", "Shattered Core", 2,
            new[] { (metallic, 12f), (stony, 1f), (carbonaceous, 0.3f), (basaltic, 0.3f) },
            new[] { (nickelIron, 1.5f), (nativeGold, 1f), (isoferroplatinum, 1f) });
        Province("basalt-shards", "Basalt Shards", 2,
            new[] { (basaltic, 15f), (stony, 1f), (carbonaceous, 0.3f), (metallic, 0.3f) },
            new[] { (ilmenite, 1.5f), (anorthite, 1.5f), (nativeCopper, 1f) });
        Province("sulphide-melt", "Sulphide Melt", 1,
            new[] { (stony, 2f), (metallic, 6f), (basaltic, 10f), (carbonaceous, 0.3f) },
            new[] { (pentlandite, 2f), (chalcopyrite, 1.5f), (sperrylite, 1f), (nativeGold, 0.5f) });
        Province("evolved-crust", "Evolved Crust", 1,
            new[] { (basaltic, 12f), (stony, 1f), (carbonaceous, 0.2f), (metallic, 0.2f) },
            new[] { (ilmenite, 1f), (uraninite, 1f), (brannerite, 1f), (monazite, 1f), (columbite, 1f), (wolframite, 1f), (orthoclase, 1f) });
    }

    // Sets this faction's stand-in on a kind, replacing any it had, so a rerun converges.
    private void StandIn(string kindId, CultRecordRef<Faction> faction, string standInKindId)
    {
        var key = StationKindData.KeyOf(kindId);
        var kind = _a.Catalog.Get<StationKindData>(key) ?? throw new InvalidOperationException($"The catalog holds no station kind '{kindId}'.");
        kind.StandIns = kind.StandIns.Where(s => !s.Faction.Key.Equals(faction.Key)).ToList();
        kind.StandIns.Add(new StationKindStandIn { Faction = faction, Kind = new CultRecordRef<StationKindData>(StationKindData.KeyOf(standInKindId)) });
        _a.Put(kind, key);
    }

    private static CultRecordKey CommodityKey(string id) => new CultRecordKey("commodity:" + id);

    // A new ore: a raw good with an extraction recipe at the mining station that fills its metal roles.
    private CultRecordRef<SimpleCommodityData> Ore(string id, string name, string description, float mass, int price, int workforce, int perCycle, string lore, params string[] fills)
    {
        var key = CommodityKey(id);
        _a.Put(new SimpleCommodityData { Name = name, Description = description, Mass = mass, Price = price }, key);
        Recipe(key, "mining", 600, workforce, perCycle, lore, fills, new List<ItemRole>());
        return new CultRecordRef<SimpleCommodityData>(key);
    }

    // An existing raw record given its ore name and an extraction recipe; found by key so a rerun after the rename converges.
    private CultRecordRef<SimpleCommodityData> Rename(CultRecordKey key, string name, string description, float mass, int price, int workforce, int perCycle, string lore, params string[] fills)
    {
        var record = _a.Catalog.Get<SimpleCommodityData>(key) ?? throw new InvalidOperationException($"The catalog holds no simple commodity {key.Value}.");
        record.Name = name;
        record.Description = description;
        record.Mass = mass;
        record.Price = price;
        _a.Put(record, key);
        Recipe(key, "mining", 600, workforce, perCycle, lore, fills, new List<ItemRole>());
        return new CultRecordRef<SimpleCommodityData>(key);
    }

    // A new metal: a simple commodity whose processing recipe takes its ore role.
    private void Metal(string id, string name, string description, float mass, int price, int oreCount, int outputCount, float cycle, int workforce, string lore, string role)
    {
        var key = CommodityKey(id);
        _a.Put(new SimpleCommodityData { Name = name, Description = description, Mass = mass, Price = price }, key);
        Recipe(key, "processing", cycle, workforce, outputCount, lore, new string[0], new List<ItemRole> { new ItemRole { Name = role, Count = oreCount } });
    }

    // An existing good (Gold, Uranium, Potassium) given what it is and a processing recipe that takes its ore role.
    private void Smelt(string name, string description, float mass, int price, int oreCount, int outputCount, float cycle, int workforce, string lore, string role)
    {
        _a.Edit<SimpleCommodityData>(name, c =>
        {
            c.Description = description;
            c.Mass = mass;
            c.Price = price;
        });
        var key = _a.Catalog.RefOf(_a.Catalog.GetAll<SimpleCommodityData>().Single(c => c.Name == name)).Key;
        Recipe(key, "processing", cycle, workforce, outputCount, lore, new string[0], new List<ItemRole> { new ItemRole { Name = role, Count = oreCount } });
    }

    private void Recipe(CultRecordKey output, string kind, float cycle, int workforce, int outputCount, string lore, string[] fills, List<ItemRole> roles)
    {
        var outputRef = new CultRecordRef<ItemData>(output);
        _a.Put(new RecipeData
        {
            Output = outputRef,
            OutputCount = outputCount,
            Inputs = new List<CommodityCount>(),
            RoleInputs = roles,
            Cycle = cycle,
            Workforce = workforce,
            Upkeep = new List<CommodityCount>(),
            StationKind = new CultRecordRef<StationKindData>(StationKindData.KeyOf(kind)),
            LoreHook = lore,
            Fills = fills.ToList()
        }, RecipeData.KeyOf(output));
    }

    // A field kind under its key, written whole: a rerun converges, and the existing kind keeps its key so saved belts hold.
    private CultRecordRef<FieldKindData> Kind(CultRecordKey key, string name, float crossSection, float weight,
        params (CultRecordRef<SimpleCommodityData> Ore, float Abundance, bool Regional)[] yields)
    {
        _a.Put(new FieldKindData
        {
            Name = name,
            CrossSection = crossSection,
            GenerationWeight = weight,
            Yields = yields.Select(y => new FieldYield { Commodity = y.Ore, Abundance = y.Abundance, Regional = y.Regional }).ToList()
        }, key);
        return new CultRecordRef<FieldKindData>(key);
    }

    private void Province(string id, string name, int cards, (CultRecordRef<FieldKindData> Kind, float Weight)[] kinds,
        (CultRecordRef<SimpleCommodityData> Ore, float Weight)[] ores)
    {
        _a.Put(new ProvinceData
        {
            Name = name,
            GenerationWeight = cards,
            Kinds = kinds.Select(k => new ProvinceKindWeight { Kind = k.Kind, Weight = k.Weight }).ToList(),
            Ores = ores.Select(o => new ProvinceOre { Commodity = o.Ore, Weight = o.Weight }).ToList()
        }, new CultRecordKey("province:" + id));
    }
}

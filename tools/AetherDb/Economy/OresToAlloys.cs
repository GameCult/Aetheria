using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;

// Economy content, batch 2: ores to alloys. What the mining station takes out of rock and metal asteroids, and the refinery and
// foundry steps that turn it into the metals every other chain builds from: iron and steel above all (the hub munitions already
// draws), copper, aluminium, titanium and its alloy, carbon, synthetic diamond, and the rolled mill products (sheet and rebar).
// Zhestokost's foundries are the faction this batch places, extending batch 1's stand-ins to the mining kind.
//
// This batch owns Steel: batch 1 wrote it as an interface good, and this batch writes it unconditionally with its recipe. Goods
// owned by other chains (Methane, Hydrogen: volatiles) are read, never written.
public sealed class OresToAlloys : EconomyBatch
{
    private EconomyAuthor _a;

    public override void Author(EconomyAuthor author)
    {
        _a = author;

        // Zhestokost mines for its own foundries. Its first planned station model is the supply depot (ruling faction-station-models),
        // so mining stands in on the depot like batch 1's industrial kinds. One hop: depot has no Zhestokost stand-in.
        StandIn("mining", FactionRef("Zhestokost"), "depot");

        // Extraction: the mining station's raw goods, the existing records given what they are, a mass and a price.
        var rock = Raw("Rock", "Silicate regolith and stony asteroid rubble: the bulk ore that holds iron oxide, aluminium, titanium, copper sulphides and the oxygen bound to all of them.", 1f, 2, 12,
            "Rock is cheap and moved by the thousand tonnes, so a hauler full of it is never worth stealing. A hauler running rock away from a refinery instead of towards one is worth a question: somebody is building a berm, or burying something.",
            new string[0]);
        var newMetals = Raw("New Metals", "Native nickel-iron cut from metallic asteroids. Belt miners call it new metal to tell it from old metal, the salvage that has already been a ship.", 1f, 12, 10,
            "Belt crews price new metal by the cut, not the tonne: the face of a metallic asteroid comes off in slabs that ring when struck. A cargo manifest that says new metals has not yet been anybody's hull, which is why insurers and arsenal inspectors both ask for one.",
            new string[0]);
        var gold = Raw("Gold", "Native gold and the platinum-group residue that settles out of metal asteroid ore.", 0.2f, 600, 6,
            "Gold comes out of the same metal asteroids as iron, in the residue the iron leaves behind. It wires every contact that must not corrode and it is still the only metal people will hoard without being told to.",
            new[] { "jewellery" });
        var uranium = Raw("Uranium", "Uraninite ore, crushed and leached at the mine head to yellowcake.", 0.5f, 150, 10,
            "Uranium is mined under licence and shipped under seal. A broken seal on a yellowcake drum is a crime in every bloc; an unbroken one on a drum that never reached its reactor is a better story.",
            new string[0]);
        var potassium = Raw("Potassium", "Potash: potassium salts dug from evaporite beds on old wet moons.", 0.8f, 6, 8,
            "Potash feeds farms first and arsenals second: the saltpetre in a primer is made from the same salt that grows a habitat's algae. A moon with potash beds is fought over by people who never meant to farm it.",
            new[] { "fertiliser" });

        var methane = Existing("Methane");
        var hydrogen = Existing("Hydrogen");

        // Refining at the processing station.
        var carbon = Simple("carbon", "Carbon", "Carbon black and graphite from methane pyrolysis.", 0.5f, 10);
        Recipe(carbon, "processing", 300, 8, 2,
            "Methane split in a hot reactor leaves carbon on the walls and hydrogen in the pipe. The carbon goes into steel, electrodes and water filters; a habitat that stops buying it has either found another source or stopped filtering.",
            new[] { "filter" }, (methane, 2));

        var iron = Simple("iron", "Iron", "Carbonyl iron pellets: nickel-iron ore refined through carbon monoxide, the nickel and precious residue drawn off.", 1f, 22);
        Recipe(iron, "processing", 600, 14, 2,
            "The carbonyl refinery turns metal asteroid ore into a gas and back into almost pure iron, leaving nickel and gold in the residue. It is the oldest Belt refinery design still built, from the dry-refinery stack TerraCore's patents spread across Luna and the Belt, and the carbon monoxide it runs on kills a crew that forgets its sensors.",
            new[] { "cookware" }, (newMetals, 3));

        var steel = Steel();
        Recipe(steel, "processing", 900, 30, 3,
            "Steel is the hub of everything this economy makes: rounds, guns, hull frames, habitat rebar, cookware. Zhestokost's arsenal commissions plan foundry output a season ahead, so a foundry's steel going to rebar instead of shell casings is the first sign that the plan has turned from war to building.",
            new[] { "blade" }, (iron, 3), (carbon, 1));

        var sheet = Mill("Sheet Metal", "Hot-rolled steel sheet, coiled. (Mill slogan: 'Oh sheet, it's metal!')", 0.5f, 20);
        Recipe(sheet, "processing", 300, 10, 2,
            "Sheet is what a station buys when it is patching rather than building: hull skins, ducting, lockers, appliance shells. Repair yards read the price of sheet the way farmers read the weather.",
            new[] { "housing" }, (steel, 1));

        var rebar = Mill("Construction Rebar", "Ribbed steel bar for reinforcing cast regolith. Helps your building stay up.", 0.5f, 18);
        Recipe(rebar, "processing", 300, 10, 2,
            "Every hollowed asteroid habitat is cast regolith on a cage of rebar. Orbital Forge's engineers made space cheap to occupy this way, and a run of rebar heading out to an empty rock means someone means to live there.",
            new[] { "reinforcement" }, (steel, 1));

        // Electrolysis: molten regolith split by current. The same cells free the oxygen the refinery burns, so it is no input here.
        var aluminium = Simple("aluminium", "Aluminium", "Aluminium ingot from molten-regolith electrolysis.", 0.4f, 50);
        Recipe(aluminium, "processing", 900, 12, 1,
            "Aluminium is electricity made solid: the cells that win it from molten rock draw more power than the habitat beside them. Where a refinery sells aluminium cheap, its power is cheap too, and someone should ask why.",
            new[] { "cookware" }, (rock, 4));

        var titanium = Simple("titanium", "Titanium Sponge", "Titanium reduced from ilmenite-rich regolith in molten salt cells.", 0.5f, 140);
        Recipe(titanium, "processing", 1200, 14, 1,
            "Ilmenite is the dark sand in lunar and asteroid regolith; titanium is reduced out of it a few kilograms at a time in salt baths that run for days. A sponge shipment is small, heavy with value and always insured.",
            new string[0], (rock, 5));

        var tiAlloy = Simple("titanium-alloy", "Titanium Alloy", "Titanium with aluminium and vanadium traces, vacuum-arc remelted into billet.", 0.5f, 200);
        Recipe(tiAlloy, "processing", 1200, 20, 3,
            "Titanium alloy is light, hot-strong and slow to make, so it goes where mass matters and failure is unforgiving: drive mounts, pressure frames and the joints of people. Orbital Forge's refractory yards and the premium licensed shops buy it from the same remelt furnaces.",
            new[] { "prosthetic" }, (titanium, 3), (aluminium, 1));

        var copper = Simple("copper", "Copper", "Cathode copper from sulphide rock floated, smelted in the refinery's own oxygen and electrorefined.", 1f, 70);
        Recipe(copper, "processing", 900, 16, 1,
            "Copper is scarce in metal asteroids and common on wet moons, so it travels further than most metals. Every wire, coil and heat pipe draws on it; when copper runs short, it shows first as ships waiting in a yard for their wiring.",
            new[] { "wiring" }, (rock, 6));

        // Synthetic diamond, vapour-deposited from methane in a hydrogen plasma.
        var diamond = Existing("Diamond");
        _a.Edit<CompoundCommodityData>("Diamond", d =>
        {
            d.Description = "Lab-grown diamond plate and grit, vapour-deposited from methane in a hydrogen plasma. Used to harden a material.";
            d.Mass = 0.05f;
            d.Price = 300;
        });
        Recipe(diamond, "processing", 1800, 10, 1,
            "No one in Sol or Elysium mines diamond; it is grown, a few microns an hour, in plasma reactors that hum like a choir. It lines gun bores and cutting heads and sits in engagement rings, and the rings and the bores come off the same reactor.",
            new[] { "gemstone" }, (methane, 1), (hydrogen, 2));
    }

    private CultRecordRef<Faction> FactionRef(string name)
    {
        var faction = _a.Catalog.GetAll<Faction>().Single(f => f.Name == name);
        return new CultRecordRef<Faction>(_a.Catalog.RefOf(faction).Key);
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

    // An existing raw good: edited in place, given an extraction recipe at the mining station.
    private CultRecordRef<ItemData> Raw(string name, string description, float mass, int price, int workforce, string lore, string[] fills)
    {
        _a.Edit<SimpleCommodityData>(name, c =>
        {
            c.Description = description;
            c.Mass = mass;
            c.Price = price;
        });
        var raw = Existing(name);
        Recipe(raw, "mining", 600, workforce, 4, lore, fills);
        return raw;
    }

    // Steel was batch 1's interface good. This batch owns it, so it writes the record whatever is there.
    private CultRecordRef<ItemData> Steel() =>
        Simple("steel", "Steel", "Carbon steel billet and bar from carbonyl iron and carbon, the stock every mill product and forging starts from.", 1f, 30);

    // An existing mill product: edited in place.
    private CultRecordRef<ItemData> Mill(string name, string description, float mass, int price)
    {
        _a.Edit<CompoundCommodityData>(name, c =>
        {
            c.Description = description;
            c.Mass = mass;
            c.Price = price;
        });
        return Existing(name);
    }

    private CultRecordRef<ItemData> Simple(string id, string name, string description, float mass, int price)
    {
        var key = CommodityKey(id);
        _a.Put(new SimpleCommodityData { Name = name, Description = description, Mass = mass, Price = price }, key);
        return new CultRecordRef<ItemData>(key);
    }

    // An item the catalog already holds, by its unique name.
    private CultRecordRef<ItemData> Existing(string name)
    {
        var named = _a.Catalog.GetAll<ItemData>().Where(i => i.Name == name).ToArray();
        if (named.Length != 1) throw new InvalidOperationException($"{named.Length} items are named '{name}'; the ores batch needs exactly one.");
        return new CultRecordRef<ItemData>(_a.Catalog.RefOf(named[0]).Key);
    }

    private void Recipe(CultRecordRef<ItemData> output, string kind, float cycle, int workforce, int outputCount, string lore, string[] fills,
        params (CultRecordRef<ItemData> Commodity, int Count)[] inputs)
    {
        _a.Put(new RecipeData
        {
            Output = output,
            OutputCount = outputCount,
            Inputs = inputs.Select(i => new CommodityCount { Commodity = i.Commodity, Count = i.Count }).ToList(),
            Cycle = cycle,
            Workforce = workforce,
            Upkeep = new List<CommodityCount>(),
            StationKind = new CultRecordRef<StationKindData>(StationKindData.KeyOf(kind)),
            LoreHook = lore,
            Fills = fills.ToList()
        }, RecipeData.KeyOf(output.Key));
    }
}

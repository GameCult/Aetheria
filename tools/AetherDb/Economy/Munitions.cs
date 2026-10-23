using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;

// Economy content, batch 1: the munitions chain. Rounds (the Ammo commodity ballistic weapons draw), the energetics behind them,
// and the parts that fill the roles of ballistic guns, missile racks and the mine launcher: barrel, feed mechanism, warhead,
// thruster and dispenser. Zhestokost is the faction this chain is written around (its arsenal commissions plan foundry output,
// ammunition and transport together), so it is the faction that places the chain's station kinds until other factions' batches
// place them too.
//
// Goods owned by other chains are named here only as interface goods: written when no record holds the key yet, never
// overwritten, and given no recipe. Their own batch writes them unconditionally and owns their recipe, so the batch order
// does not decide their content.
public sealed class Munitions : EconomyBatch
{
    private EconomyAuthor _a;

    public override void Author(EconomyAuthor author)
    {
        _a = author;

        // The kind this chain adds. Energetics and loading are sited apart from other industry because a loading accident must not
        // take a yard with it; that separation is the reason it is its own kind rather than a line in a fabrication station.
        _a.Put(new StationKindData
        {
            Name = "Munitions Works",
            Description = "Makes energetics (nitrocellulose, propellant, high explosive), loads rounds, primers, fuzes, warheads and rocket motors; sited apart from other industry so an accident takes only itself.",
            StandIns = new List<StationKindStandIn>()
        }, StationKindData.KeyOf("munitions-works"));

        // Zhestokost has no station model of any kind yet. Its first planned model is the supply depot (ruling faction-station-models),
        // so its industrial kinds stand in on the depot until their own models land. One hop: depot itself has no Zhestokost stand-in.
        var zhestokost = FactionRef("Zhestokost");
        foreach (var kind in new[] { "processing", "fabrication", "munitions-works" })
            StandIn(kind, zhestokost, "depot");

        // Interface goods: named by this chain, owned by another.
        var steel = Interface("steel", "Steel", "Rolled and drawn structural steel: bar, sheet and billet.", 1f, 30, simple: true);
        var ammonia = Interface("ammonia", "Ammonia", "Ammonia ice and gas skimmed from cold bodies; the nitrogen in fertiliser and in every explosive.", 0.6f, 15, simple: true);
        var oxygen = Interface("oxygen", "Oxygen", "Liquid oxygen, cracked from water ice or recovered from life support.", 1.1f, 8, simple: true);
        var pulp = Interface("cellulose-pulp", "Cellulose Pulp", "Washed cellulose from algae and bacterial vats: the stock for paper, packaging, fibre and guncotton.", 0.8f, 20, simple: true);
        var actuator = Interface("actuator", "Actuator", "A sealed motor and gear train that moves a load on command; the same unit opens a hatch, feeds a gun or folds laundry.", 2f, 400, simple: false);
        var diamond = Existing("Diamond");
        var board = Existing("Circuit Board");
        var potassium = Existing("Potassium");
        var methane = Existing("Methane");
        var water = Existing("Water");

        // Chemistry: processing stations, the same acid plants that feed agriculture.
        var nitric = Simple("nitric-acid", "Nitric Acid", "Concentrated nitric acid from ammonia burned over a catalyst and absorbed in water.", 1.5f, 40);
        Recipe(nitric, "processing", 300, 12, 2,
            "The towers that burn ammonia into acid make fertiliser for the farms and nitrate for the arsenal. In Zhestokost the arsenal commission sets each season's split, so a hold of acid bound for a munitions works instead of an agriculture station tells you which plan won this year.",
            new[] { "fertiliser" }, (ammonia, 1), (oxygen, 2), (water, 1));

        var saltpetre = Simple("potassium-nitrate", "Potassium Nitrate", "Saltpetre: potassium neutralised with nitric acid and crystallised.", 2.1f, 60);
        Recipe(saltpetre, "processing", 240, 8, 2,
            "Saltpetre is fertiliser, curing salt for ration meat, and the oxidiser in powder and primers. A Zhestokost ration tin carries it twice: once in the meat and once in the gun that guards the convoy.",
            new[] { "fertiliser", "preservative" }, (potassium, 1), (nitric, 1));

        var hexamine = Simple("hexamine", "Hexamine", "White crystalline hexamine from ammonia and methane-derived formaldehyde.", 1.3f, 50);
        Recipe(hexamine, "processing", 300, 10, 2,
            "Pressed into tablets, hexamine heats a ration tin in a cold corridor; nitrated, it is the explosive in every warhead. A crate of it is either somebody's winter or somebody's siege.",
            new[] { "solid fuel", "preservative" }, (ammonia, 2), (methane, 3));

        // Energetics: the munitions works.
        var guncotton = Simple("nitrocellulose", "Nitrocellulose", "Guncotton: cellulose nitrated in mixed acid, washed and kept damp.", 1.2f, 90);
        Recipe(guncotton, "munitions-works", 600, 20, 2,
            "Guncotton comes from the same cellulose vats as paper and packaging, and it also makes the lacquer for stencils and decals: a Zhestokost hull's cream serial numbers and the charge behind its guns came out of the same nitrator.",
            new[] { "lacquer" }, (pulp, 2), (nitric, 2));

        var powder = Existing("Ballistic Powder");
        _a.Edit<CompoundCommodityData>("Ballistic Powder", p =>
        {
            p.Description = "Single-base propellant: nitrocellulose grains with a saltpetre flash reducer. The charge in every ballistic round and the grain in every rocket motor.";
            p.Price = 160;
        });
        Recipe(powder, "munitions-works", 600, 24, 3,
            "A ballistic gun fires on the energy this powder holds, not on its ship's reactor, so a heavy formation's reach is set by the powder in its tenders. Watch the powder trains and you know how long a siege is planned to last.",
            new string[0], (guncotton, 3), (saltpetre, 1));

        var fill = Simple("explosive-fill", "Explosive Fill", "Cast high explosive: hexamine nitrated to RDX and melt-cast with a binder.", 1.8f, 220);
        Recipe(fill, "munitions-works", 900, 30, 2,
            "Nothing peaceful is worth the risk of storing cast explosive, so a shipment of it in peacetime is news in itself: someone is filling warheads.",
            new string[0], (hexamine, 1), (nitric, 3));

        var primer = Compound("primer", "Primer", "Electric percussion primers in a steel cup, packed by the hundred.", 0.2f, 30);
        Recipe(primer, "munitions-works", 300, 16, 10,
            "Primers are small, sensitive and made in a hall of their own; a primer-line accident is how a new munitions works learns why its halls stand apart.",
            new string[0], (saltpetre, 1), (steel, 1));

        // Metal parts: fabrication and the state foundry.
        var casing = Compound("shell-casing", "Shell Casing", "Deep-drawn steel cartridge cases, lacquered against corrosion.", 0.5f, 40);
        Recipe(casing, "fabrication", 240, 12, 4,
            "Zhestokost draws its cases from lacquered steel, not brass: brass is scarce and the arsenal plans for volume, not for reloading. A spent case in a wreck field tells you whose guns fired.",
            new string[0], (steel, 1));

        var slug = Compound("projectile", "Projectile", "Turned steel slugs with a driving band, sized by calibre.", 0.8f, 60);
        Recipe(slug, "fabrication", 240, 12, 4,
            "Each slug carries the tolerance stamp of the foundry that cast its bar, so a quartermaster can trace a bad lot back to a named shift.",
            new string[0], (steel, 2));

        var ammo = Existing("Ammo");
        _a.Edit<SimpleCommodityData>("Ammo", a => a.Description = "Crated ballistic rounds: lacquered steel case, steel slug, propellant charge and primer. The round, not the reactor, carries the energy a ballistic gun fires.");
        Recipe(ammo, "munitions-works", 600, 30, 2,
            "A tender's hold of crated rounds is a battle's energy budget in transit. Zhestokost heavy formations fight as long as their tenders reach them, and cutting that line is how a faster enemy makes them go quiet.",
            new string[0], (casing, 2), (slug, 2), (powder, 1), (primer, 2));

        var forging = Compound("gun-forging", "Gun Forging", "Rotary-forged barrel blanks, stamped with the pour number.", 40f, 900);
        Recipe(forging, "processing", 1800, 40, 1,
            "Barrel blanks come from the same state foundries that pour hull plate. A foundry shipping forgings is one not shipping plate this quarter, and the arsenal commission's quarterly split is a secret worth stealing.",
            new string[0], (steel, 4));

        var barrel = Compound("gun-barrel", "Gun Barrel", "A forged blank bored, rifled and diamond-lined.", 35f, 2400);
        Recipe(barrel, "fabrication", 1200, 30, 1,
            "The diamond lining decides how many rounds a barrel takes before its crew swaps it. Zhestokost barrels are built to be swapped in the field, under fire, by crews trained on one pattern for twenty years.",
            new[] { "barrel" }, (forging, 1), (diamond, 1));

        var autoloader = Compound("autoloader", "Autoloader Mechanism", "Rammer, feed tray and cycling actuators that take rounds from a magazine into the breech.", 30f, 2600);
        Recipe(autoloader, "fabrication", 1200, 30, 1,
            "The feed is the largest moving part of a Zhestokost gun and the first thing its maintainers learn to strip. The Nakovalnya's feed collar exists so a tender can couple straight into these mechanisms.",
            new[] { "feed mechanism" }, (steel, 2), (actuator, 2));

        var fragCase = Compound("fragmentation-case", "Fragmentation Case", "A notched steel warhead body that breaks in a set pattern.", 6f, 120);
        Recipe(fragCase, "fabrication", 600, 14, 2,
            "The notch pattern is a maker's signature; salvage crews read it in the debris to tell whose warhead it was.",
            new string[0], (steel, 2));

        var fuze = Compound("fuze", "Fuze", "A proximity and impact fuze: a circuit board, a safe-arm switch and a booster pellet.", 0.6f, 300);
        Recipe(fuze, "munitions-works", 600, 18, 4,
            "Fuzes are where munitions meet electronics. When a region runs short of boards, it shows first as warheads without fuzes stacked in a depot.",
            new string[0], (board, 1), (fill, 1));

        var warhead = Compound("he-warhead", "High-Explosive Warhead", "Explosive fill cast into a fragmentation case, fuzed.", 12f, 1800);
        Recipe(warhead, "munitions-works", 900, 24, 1,
            "Death Monkey Explosives and the Zhestokost arsenal fill the same warhead from the same chemistry; only the stencils differ, and a recovered warhead's stencil is evidence in a claim.",
            new[] { "warhead" }, (fill, 2), (fragCase, 1), (fuze, 1));

        var motor = Compound("rocket-motor", "Rocket Motor", "Propellant cast into a steel case with a nozzle.", 10f, 1500);
        Recipe(motor, "munitions-works", 900, 20, 1,
            "The same powder as a gun charge, shaped to burn for seconds instead of milliseconds: missile stocks and gun stocks compete for one powder train.",
            new[] { "thruster" }, (powder, 2), (steel, 1));

        var dispenser = Compound("mine-dispenser", "Mine Dispenser", "A rack and ejector that lays mines in a pattern.", 20f, 1600);
        Recipe(dispenser, "fabrication", 900, 20, 1,
            "Little more than steel and actuators, which is why every convoy navy can build one and every blockade starts with one.",
            new[] { "dispenser" }, (steel, 2), (actuator, 1));

        // The designs whose every role this chain fills. Missile racks and rocket pods also need a guidance system, which the
        // electronics chain fills; their recipes wait for it.
        foreach (var gun in new[] { "Light Machine Gun", "Autocannon", "Long Slug Rifle", "Charged Scattergun", "Slug Rifle", "Flak Gun", "Cluster Cannon", "Point Defense Gun" })
            Design(gun, 1800, 30, "A ballistic gun is a barrel and a feed; the round brings the energy.");
        Design("Mine Launcher", 1800, 24, "A dispenser and the warheads it lays.");
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

    private CultRecordRef<ItemData> Interface(string id, string name, string description, float mass, int price, bool simple)
    {
        var key = CommodityKey(id);
        if (_a.Catalog.Get(key) == null)
        {
            if (simple) _a.Put(new SimpleCommodityData { Name = name, Description = description, Mass = mass, Price = price }, key);
            else _a.Put(new CompoundCommodityData { Name = name, Description = description, Mass = mass, Price = price }, key);
        }
        return new CultRecordRef<ItemData>(key);
    }

    private CultRecordRef<ItemData> Simple(string id, string name, string description, float mass, int price)
    {
        var key = CommodityKey(id);
        _a.Put(new SimpleCommodityData { Name = name, Description = description, Mass = mass, Price = price }, key);
        return new CultRecordRef<ItemData>(key);
    }

    private CultRecordRef<ItemData> Compound(string id, string name, string description, float mass, int price)
    {
        var key = CommodityKey(id);
        _a.Put(new CompoundCommodityData { Name = name, Description = description, Mass = mass, Price = price }, key);
        return new CultRecordRef<ItemData>(key);
    }

    // An item the catalog already holds, by its unique name.
    private CultRecordRef<ItemData> Existing(string name)
    {
        var named = _a.Catalog.GetAll<ItemData>().Where(i => i.Name == name).ToArray();
        if (named.Length != 1) throw new InvalidOperationException($"{named.Length} items are named '{name}'; the munitions batch needs exactly one.");
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

    // A role recipe: its inputs are the design's roles, so it names none.
    private void Design(string name, float cycle, int workforce, string lore) =>
        Recipe(Existing(name), "fabrication", cycle, workforce, 1, lore, new string[0]);
}

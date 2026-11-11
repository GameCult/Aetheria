using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;

// Economy content, batch 1b: where the munitions chain's raw materials come from. Munitions writes the chain and names its leaves
// (Water, Methane, Ammonia, Oxygen, Potassium, Diamond, Steel, Cellulose Pulp, Actuator, Circuit Board) without recipes; this batch
// gives each a source at a station kind whose description already says it does that work: input-free extraction at mining (ore,
// Gold, Potassium, Diamond) and volatiles (Water, Methane, Ammonia), and processing or fabrication from those goods for the rest.
// Run after Munitions: it edits nothing the other batch writes and every output it names must exist.
public sealed class MunitionsFeed : EconomyBatch
{
    private EconomyAuthor _a;

    public override void Author(EconomyAuthor author)
    {
        _a = author;

        // Extraction: no inputs, so these are the chain's raw goods.
        var rock = Make("Rock", "mining", 300, 10, 8,
            "Nobody on a mining station calls it rock; it is the overburden, the slag, the thing the real cargo was buried in. It is also what every smelter, board shop and cement yard starts from, so the cheapest hold in the verse is the one that decides who can build.");
        var metals = Make("New Metals", "mining", 300, 12, 6,
            "The ore that built the cities: whoever holds the claim feeds every foundry downstream, which is why mining rights are the one commodity the garrisons will not leave unguarded.");
        var gold = Make("Gold", "mining", 600, 8, 2,
            "Gold has a use besides vanity: it does not corrode in a contact. Boards bound for a hull in a cold or wet corridor are gold-plated, and a prospector who finds a seam sells it to the board shops before the jewellers.");
        var potassium = Make("Potassium", "mining", 300, 8, 4,
            "Potash and saltpetre salts turn up in the same evaporite beds that mark old water. Farms want it for fertiliser and the arsenal wants it for powder; the survey crews tag the beds by which buyer reached them first.");
        var diamond = Make("Diamond", "mining", 1200, 12, 1,
            "Industrial stones from the deep kimberlite cores: grit for lining barrels and cutting tools rather than jewellery. A miner who sells one for a ring has wasted a barrel.");
        var water = Make("Water", "volatiles", 300, 8, 6,
            "Every station buys water first and everything else second. The ice haulers run on the knowledge that a station's water reserve is the one stock it cannot be talked out of counting.");
        var methane = Make("Methane", "volatiles", 300, 8, 4,
            "Skimmed from the cold ice where it keeps for ever. Hauled home it is cooking gas, a feedstock for the formaldehyde plants, and the first thing that burns when a tank is struck.");
        var ammonia = Make("Ammonia", "volatiles", 300, 8, 4,
            "Ammonia ice is the nitrogen the whole verse eats and shoots: the farms spread it and the arsenals nitrate it. The skimmers who work cold bodies know exactly which of the two their hold is bound for by the hauler waiting at the dock.");

        // Processing from those goods.
        var oxygen = Make("Oxygen", "processing", 240, 10, 3,
            "Cracking water ice pays twice: the oxygen goes to lungs and acid towers, and the hydrogen is fuel. A station that sells its oxygen cheap is either swimming in ice or hiding a shortage.",
            (water, 3));

        var steel = Make("Steel", "processing", 600, 20, 4,
            "Foundries on the ore belts pour bar, sheet and billet from alloy ore and slag flux. A pour is cheap; the rolling mills that finish it are the part a blockade starves, and every gun, hull plate and nail in the verse is waiting behind them.",
            (metals, 3), (rock, 1));

        // Algae is a catalog commodity the agriculture station description says grows from Water and Potassium.
        var algae = Make("Algae", "agriculture", 600, 12, 6,
            "Vats of green water under lamp-light: the first thing a new habitat grows, because it eats ash and sunlight and gives back food, fibre and air. A farm that loses its algae loses its station within the season.",
            (water, 2), (potassium, 1));

        var pulp = Make("Cellulose Pulp", "processing", 300, 10, 2,
            "Algae from the farm vats is washed, digested and pressed into pulp. The same vats make paper for a Zhestokost order book and guncotton stock for its arsenal, and the split is seen in how many hands the washing hall hires.",
            (algae, 3));

        var board = Make("Circuit Board", "fabrication", 600, 14, 2,
            "Gold-plated contacts on a silicate board: the oldest reliable electronics. A region that cannot make its own boards imports a shortage in every device that has a fuze, a sensor or a lock.",
            (gold, 1), (rock, 2));

        Make("Actuator", "fabrication", 600, 16, 1,
            "A steel housing, a motor and a controller board. It opens hatches and feeds guns, and a repair crew keeps a drawer of them because everything on a ship eventually needs to move on command.",
            (steel, 2), (board, 1));
    }

    private CultRecordRef<ItemData> Make(string name, string kind, float cycle, int workforce, int count, string lore,
        params (CultRecordRef<ItemData> Commodity, int Count)[] inputs)
    {
        var named = _a.Catalog.GetAll<ItemData>().Where(i => i.Name == name).ToArray();
        if (named.Length != 1) throw new InvalidOperationException($"{named.Length} items are named '{name}'; the munitions feed needs exactly one.");
        var output = new CultRecordRef<ItemData>(_a.Catalog.RefOf(named[0]).Key);
        _a.Put(new RecipeData
        {
            Output = output,
            OutputCount = count,
            Inputs = inputs.Select(i => new CommodityCount { Commodity = i.Commodity, Count = i.Count }).ToList(),
            Cycle = cycle,
            Workforce = workforce,
            Upkeep = new List<CommodityCount>(),
            StationKind = new CultRecordRef<StationKindData>(StationKindData.KeyOf(kind)),
            LoreHook = lore,
            Fills = new List<string>()
        }, RecipeData.KeyOf(output.Key));
        return output;
    }
}

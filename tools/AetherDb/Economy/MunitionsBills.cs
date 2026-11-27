using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;

// Economy content, batch: the bills of the munitions chain's designs. A design recipe names roles, never components (L-roles), and a
// role is priced at its cheapest filler, so a top gun cannot be dearer than a starter by naming a rarer grade of the same role.
// It names a different role instead: a rifled barrel, a lined barrel or a coil barrel where the starter names a barrel, a tracking
// feed where it names a feed mechanism. The same stats read the renamed role, its sellers' maker profiles carry over, and a part
// built from rarer metals fills it, so the design's price climbs the ladder with its chain (rulings gear-ladder-wide-by-rare-inputs,
// top-gear-within-one-region-run). Counts, labour and the parts' metals are sim-tuned knobs (economy-numbers-are-sim-tuned-knobs).
// Missile racks and rocket pods also need a guidance system, which the electronics chain fills; their recipes wait for it.
//
// Owner of the munitions chain's design recipes, their role counts and tier role names, and the four tier parts. Munitions writes
// the parts the starter fills and the rounds; it writes no design recipe, so a Munitions rerun cannot undo a bill. Renames run
// before bills, and a design that already holds the new role name is left alone, so a rerun changes no price, role or count (it does rewrite the four part recipes as new bytes: CommodityCount carries a cache-minted element id).
public sealed class MunitionsBills : EconomyBatch
{
    private EconomyAuthor _a;

    public override void Author(EconomyAuthor author)
    {
        _a = author;
        var forging = Named("Gun Forging");
        var autoloader = Named("Autoloader Mechanism");
        var diamond = Named("Diamond");
        var board = Named("Circuit Board");
        var titanium = Commodity("titanium");
        var tungsten = Commodity("tungsten");
        var niobium = Commodity("niobium");
        var rareEarth = Commodity("rare-earth-oxides");
        var pgm = Commodity("platinum-group-metals");

        Part("rifled-barrel", "Rifled Barrel", "A forged blank bored, rifled and diamond-lined inside a titanium jacket that keeps a long tube true.", 40f, 1200, 30, "rifled barrel",
            "A titanium jacket keeps a long barrel straight when one side of it is in sunlight and the other in shadow. Rifles that hit at range come from regions with a basalt crust, and a rifleman's first question about a new posting is where its titanium comes from.",
            (forging, 1), (diamond, 1), (titanium, 2));
        var lined = Part("lined-barrel", "Lined Barrel", "A forged blank with a sintered tungsten liner that takes sustained fire without washing out.", 45f, 1500, 34, "lined barrel",
            "Diamond lining is cheap and wears fast; tungsten lasts as long as the gun is fed. Reactors and autocannon draw on the same tungsten, so a region short of it chooses each quarter between its plasma walls and its liners.",
            (forging, 1), (tungsten, 2));
        Part("coil-barrel", "Coil Barrel", "A tungsten-lined barrel wound with niobium coils and rare-earth magnets that charge the shot in the bore.", 60f, 1800, 40, "coil barrel",
            "A charged scattergun carries a fusion plant's worth of niobium wound round its barrels and platinum on every contact. Only a region that holds columbite and an evolved crust, or pays the gate for them, fields these, and a squadron that does is announcing it.",
            (lined, 1), (niobium, 4), (rareEarth, 2), (pgm, 1));
        Part("tracking-feed", "Tracking Feed", "An autoloader slaved to a fire-control board with platinum contacts, cycling rounds on a predicted intercept.", 35f, 1200, 30, "tracking feed",
            "Point defence and flak fire on a solution, not a trigger: the feed waits for the board to say now. The platinum is there because a contact that drifts by a millisecond puts the burst behind the missile.",
            (autoloader, 1), (board, 1), (pgm, 1));

        RenameRole(_a, "Point Defense Gun", "feed mechanism", "tracking feed");
        RenameRole(_a, "Slug Rifle", "barrel", "rifled barrel");
        RenameRole(_a, "Long Slug Rifle", "barrel", "rifled barrel");
        RenameRole(_a, "Flak Gun", "feed mechanism", "tracking feed");
        RenameRole(_a, "Autocannon", "barrel", "lined barrel");
        RenameRole(_a, "Cluster Cannon", "barrel", "lined barrel");
        RenameRole(_a, "Charged Scattergun", "barrel", "coil barrel");

        const string gun = "A ballistic gun is a barrel and a feed; the round brings the energy.";
        Bill("Light Machine Gun", 30, gun, ("barrel", 1), ("feed mechanism", 1));
        Bill("Point Defense Gun", 30, gun, ("barrel", 1), ("tracking feed", 1));
        Bill("Slug Rifle", 30, gun, ("rifled barrel", 1), ("feed mechanism", 1));
        Bill("Long Slug Rifle", 40, gun, ("rifled barrel", 1), ("feed mechanism", 1));
        Bill("Flak Gun", 40, gun, ("barrel", 2), ("tracking feed", 1));
        Bill("Autocannon", 40, gun, ("lined barrel", 1), ("feed mechanism", 2));
        Bill("Cluster Cannon", 60, gun, ("lined barrel", 4), ("feed mechanism", 2));
        Bill("Charged Scattergun", 80, gun, ("coil barrel", 4), ("feed mechanism", 2));
        Bill("Mine Launcher", 24, "A dispenser and the warheads it lays.", ("dispenser", 1), ("warhead", 1));
    }

    // An item the catalog holds, by its unique name.
    private CultRecordRef<ItemData> Named(string name)
    {
        var named = _a.Catalog.GetAll<ItemData>().Where(i => i.Name == name).ToArray();
        if (named.Length != 1) throw new InvalidOperationException($"{named.Length} items are named '{name}'; the munitions bills need exactly one.");
        return new CultRecordRef<ItemData>(_a.Catalog.RefOf(named[0]).Key);
    }

    // A metal the ores batch writes.
    private CultRecordRef<ItemData> Commodity(string id)
    {
        var key = new CultRecordKey("commodity:" + id);
        return _a.Catalog.Get(key) != null ? new CultRecordRef<ItemData>(key) : throw new InvalidOperationException($"The catalog holds no '{key.Value}'; the ores batch writes it.");
    }

    // A tier part: priced by its chain (author reprices), filling only its own role.
    private CultRecordRef<ItemData> Part(string id, string name, string description, float mass, float cycle, int workforce, string fills, string lore,
        params (CultRecordRef<ItemData> Commodity, int Count)[] inputs)
    {
        var key = new CultRecordKey("commodity:" + id);
        _a.Put(new CompoundCommodityData { Name = name, Description = description, Mass = mass, Price = 1 }, key);
        var output = new CultRecordRef<ItemData>(key);
        Recipe(output, cycle, workforce, lore, new[] { fills }, inputs);
        return output;
    }

    // Renames a role of a design everywhere it is read: the design's roles, every stat term of every behaviour, and every non-bin
    // seller's profile. A design that holds the new name and not the old is left alone, so a rerun renames nothing.
    public static void RenameRole(EconomyAuthor author, string design, string from, string to)
    {
        CultRecordKey key = default;
        var pending = true;
        author.Edit<EquippableItemData>(design, item =>
        {
            key = author.Catalog.RefOf(item).Key;
            if (item.Roles.All(r => r.Name != from))
            {
                if (item.Roles.Any(r => r.Name == to)) { pending = false; return; }
                throw new InvalidOperationException($"'{design}' declares no role '{from}' to rename.");
            }
            foreach (var role in item.Roles.Where(r => r.Name == from)) role.Name = to;
            foreach (var behavior in item.Behaviors.Where(b => b != null))
                foreach (var field in behavior.GetType().GetFields().Where(f => f.FieldType == typeof(PerformanceStat)))
                    if (field.GetValue(behavior) is PerformanceStat stat && stat.Terms != null)
                        foreach (var term in stat.Terms.Where(t => t.Source == StatSource.Quality && t.Role == from)) term.Role = to;
        });
        if (!pending) return;
        foreach (var product in author.Catalog.GetAll<FactionProductData>().Where(p => p.Bin == null && p.Design.Key.Equals(key)).ToArray())
        {
            foreach (var role in product.Roles.Where(r => r.Role == from)) role.Role = to;
            author.Put(product, author.Catalog.RefOf(product).Key);
        }
    }

    // A design's bill: each declared role's count, and the role recipe (its inputs are the roles, so it names none).
    private void Bill(string design, int workforce, string lore, params (string Role, int Count)[] counts)
    {
        CultRecordKey key = default;
        _a.Edit<EquippableItemData>(design, item =>
        {
            key = _a.Catalog.RefOf(item).Key;
            foreach (var (role, count) in counts)
                (item.Roles.SingleOrDefault(r => r.Name == role) ?? throw new InvalidOperationException($"'{design}' declares no role '{role}'.")).Count = count;
        });
        Recipe(new CultRecordRef<ItemData>(key), 1800, workforce, lore, new string[0]);
    }

    private void Recipe(CultRecordRef<ItemData> output, float cycle, int workforce, string lore, string[] fills,
        params (CultRecordRef<ItemData> Commodity, int Count)[] inputs)
    {
        _a.Put(new RecipeData
        {
            Output = output,
            OutputCount = 1,
            Inputs = inputs.Select(i => new CommodityCount { Commodity = i.Commodity, Count = i.Count }).ToList(),
            Cycle = cycle,
            Workforce = workforce,
            Upkeep = new List<CommodityCount>(),
            StationKind = new CultRecordRef<StationKindData>(StationKindData.KeyOf("fabrication")),
            LoreHook = lore,
            Fills = fills.ToList()
        }, RecipeData.KeyOf(output.Key));
    }
}

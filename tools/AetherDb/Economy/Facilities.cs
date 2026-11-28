using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using GameCult.Caching.MessagePack;
using MessagePack;

// Economy content: the facility gear a station is fitted with to run its recipes (aetheria-living-world, cut recipe-facilities;
// rulings demand-consumed-inputs-processed-by-facilities, installed-gear-decides-what-a-station-buys). One facility design per station
// kind that some recipe names, a 2x2 GearData on a Facility mount holding FacilityData, sold by a NotOffered product (a design no
// product sells is an unsold design; a not-offered product is on no market). Sets each kind's Facilities to its design and every
// recipe's Facility to its kind's. Writes whole values, only where they differ, so a rerun touches nothing. Price is a sim-tuned knob
// (ruling economy-numbers-are-sim-tuned-knobs); names and descriptions are first drafts for the economy content pass.
public sealed class Facilities : EconomyBatch
{
    // The thermal band of every facility design, sim-tuned knobs like the prices (ruling economy-numbers-are-sim-tuned-knobs). A facility
    // is Active only while its cells are inside the band, and a station's cells start at 280 and cool toward 0 with no heater, so the
    // band holds that whole range: full performance from 0 to 560 around 280, tapering to nothing at 1000, with the floor just below
    // 0 so a cell that has cooled all the way is still inside it. Gear that leaves these unauthored gets GearData's 0 to 100.
    private const float ThermalMin = -1, ThermalMax = 1000, ThermalOptimal = 280, ThermalPlateau = 560;

    private static readonly (string Kind, string Id, string Name, string Description)[] Table =
    {
        ("mining", "extraction-rig", "Extraction Rig", "Drills, crushers and loaders that turn a station's miners into a steady flow of ore."),
        ("volatiles", "volatiles-skimmer", "Volatiles Skimmer", "Scoops, condensers and tankage that skim gas giants and ice for Methane, Ammonia and Water."),
        ("agriculture", "algae-vats", "Algae Vats", "Lit, circulating vats that grow Algae from Water and Potassium."),
        ("processing", "refinery", "Refinery", "Furnaces, centrifuges and reactors that refine ore and volatiles into Metals, Alloys and Compounds."),
        ("fabrication", "fabrication-line", "Fabrication Line", "Presses, printers and assembly cells that turn Alloys and Compounds into finished goods."),
        ("munitions-works", "munitions-line", "Munitions Line", "A sealed line that makes energetics and loads rounds, primers, fuzes, warheads and motors."),
    };

    // The one storage facility: Zenith's ore silo (ruling demo-zenith-ore-depot). Storage, not a recipe facility, so it has no recipe and
    // its kind is named here; a silo is bulk storage on a Facility row and holds only commodity stacks (SiloData).
    private static readonly (string Kind, string Id, string Name, string Description) Silo =
        ("trade-hub", "ore-silo", "Ore Silo", "A sealed bulk store that holds ore waiting for the haul to the refinery.");

    // The kind's first facility design, or unset: the one lookup the recipe-writing batches use.
    public static CultRecordRef<ItemData> Of(CultCache catalog, string kindId)
    {
        return FirstOf(catalog.Get<StationKindData>(StationKindData.KeyOf(kindId)));
    }

    private static CultRecordRef<ItemData> FirstOf(StationKindData kind) => kind != null && kind.Facilities.Count > 0 ? kind.Facilities[0] : default;

    public override void Author(EconomyAuthor author)
    {
        var catalog = author.Catalog;
        var options = CultDocumentMessagePackSerialization.OptionsFor(typeof(ItemData).Assembly);
        // Writes a record only when its stored value differs from the one wanted, so a rerun converges.
        void Put<T>(T wanted, CultRecordKey key) where T : class
        {
            var held = catalog.Get(key) as T;
            if (held == null || !MessagePackSerializer.Serialize(held, options).SequenceEqual(MessagePackSerializer.Serialize(wanted, options))) author.Put(wanted, key);
        }

        var kinds = catalog.GetAll<RecipeData>().Select(recipe => recipe.StationKind.Key.Value).Distinct(StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToArray();
        foreach (var kindKey in kinds)
        {
            var (_, id, name, description) = Table.SingleOrDefault(row => StationKindData.KeyOf(row.Kind).Value == kindKey);
            if (id == null) throw new InvalidOperationException($"A recipe runs on station kind '{kindKey}', which the facilities batch has no facility for.");
            var designKey = new CultRecordKey("facility:" + id);
            var shape = new Shape(2, 2);
            foreach (var cell in shape.AllCoordinates) shape[cell] = true;
            // A stored behavior carries a cache-minted element id, so a design is compared by its fields, never by bytes.
            if (!(catalog.Get(designKey) is GearData held && held.Name == name && held.Description == description && held.Hardpoint == HardpointType.Facility
                  && held.Durability == 100 && held.Mass == 250 && held.Price == 1000 && held.Shape.Width == 2 && held.Shape.Height == 2 && held.Shape.Coordinates.Length == 4
                  && held.Behaviors.Count == 1 && held.Behaviors[0] is FacilityData
                  && held.MinimumTemperature == ThermalMin && held.MaximumTemperature == ThermalMax && held.OptimalTemperature == ThermalOptimal && held.PlateauWidth == ThermalPlateau))
                author.Put(new GearData
                {
                    Name = name, Description = description, Hardpoint = HardpointType.Facility, Shape = shape,
                    Durability = 100, Mass = 250, Price = 1000, Behaviors = { new FacilityData() },
                    MinimumTemperature = ThermalMin, MaximumTemperature = ThermalMax, OptimalTemperature = ThermalOptimal, PlateauWidth = ThermalPlateau
                }, designKey);
            var maker = MakerOf(catalog, kindKey);
            // The brand lives in the product (ruling designs-are-tech-nodes): the maker's name before the design's.
            Put(new FactionProductData
            {
                Name = $"{maker.Faction.Name} {name}", Description = description, Design = new CultRecordRef<CraftedItemData>(designKey),
                Manufacturer = new CultRecordRef<Faction>(maker.Key), NotOffered = true
            }, new CultRecordKey("product:facility:" + id));

            var kind = catalog.Get<StationKindData>(new CultRecordKey(kindKey)) ?? throw new InvalidOperationException($"A recipe names station kind '{kindKey}', which the catalog does not hold.");
            if (kind.Facilities.Count != 1 || !kind.Facilities[0].Key.Equals(designKey))
            {
                kind.Facilities = new List<CultRecordRef<ItemData>> { new CultRecordRef<ItemData>(designKey) };
                author.Put(kind, new CultRecordKey(kindKey));
            }
        }
        AuthorSilo(author, catalog, (wanted, key) => Put(wanted, key));
        foreach (var recipe in catalog.GetAll<RecipeData>().ToArray())
        {
            var facility = FirstOf(catalog.Get(recipe.StationKind));
            if (recipe.Facility.IsSet() && recipe.Facility.Key.Equals(facility.Key)) continue;
            recipe.Facility = facility;
            author.Put(recipe, catalog.RefOf(recipe).Key);
        }
    }

    // The first faction by key that places the kind, else the first by key: the brand of a facility's product.
    private static (CultRecordKey Key, Faction Faction) MakerOf(CultCache catalog, string kindKey)
    {
        var factions = catalog.GetAll<Faction>().Select(faction => (Key: catalog.RefOf(faction).Key, Faction: faction)).OrderBy(pair => pair.Key.Value, StringComparer.Ordinal).ToArray();
        var maker = factions.FirstOrDefault(pair => StationKinds.KindsOf(catalog, pair.Faction).Any(kind => kind.Key.Value == kindKey));
        return maker.Faction == null ? factions.First() : maker;
    }

    // The silo design, its NotOffered product, and its kind's Facilities holding it: each only where it differs. Compared by fields, as
    // the recipe facilities are.
    private static void AuthorSilo(EconomyAuthor author, CultCache catalog, Action<FactionProductData, CultRecordKey> put)
    {
        var (kindId, id, name, description) = Silo;
        var designKey = new CultRecordKey("facility:" + id);
        var shape = new Shape(2, 2);
        foreach (var cell in shape.AllCoordinates) shape[cell] = true;
        var interior = new Shape(8, 8);
        foreach (var cell in interior.AllCoordinates) interior[cell] = true;
        if (!(catalog.Get(designKey) is SiloData held && held.Name == name && held.Description == description
              && held.Durability == 100 && held.Mass == 250 && held.Price == 1000 && held.Shape.Width == 2 && held.Shape.Height == 2 && held.Shape.Coordinates.Length == 4
              && held.InteriorShape.Width == 8 && held.InteriorShape.Height == 8 && held.InteriorShape.Coordinates.Length == 64
              && held.MinimumTemperature == ThermalMin && held.MaximumTemperature == ThermalMax && held.OptimalTemperature == ThermalOptimal && held.PlateauWidth == ThermalPlateau))
            author.Put(new SiloData
            {
                Name = name, Description = description, Shape = shape, InteriorShape = interior,
                Durability = 100, Mass = 250, Price = 1000,
                MinimumTemperature = ThermalMin, MaximumTemperature = ThermalMax, OptimalTemperature = ThermalOptimal, PlateauWidth = ThermalPlateau
            }, designKey);
        var kindKey = StationKindData.KeyOf(kindId);
        var maker = MakerOf(catalog, kindKey.Value);
        put(new FactionProductData
        {
            Name = $"{maker.Faction.Name} {name}", Description = description, Design = new CultRecordRef<CraftedItemData>(designKey),
            Manufacturer = new CultRecordRef<Faction>(maker.Key), NotOffered = true
        }, new CultRecordKey("product:facility:" + id));
        var kind = catalog.Get<StationKindData>(kindKey) ?? throw new InvalidOperationException($"The silo's station kind '{kindId}' is not in the catalog.");
        if (kind.Facilities.Count != 1 || !kind.Facilities[0].Key.Equals(designKey))
        {
            kind.Facilities = new List<CultRecordRef<ItemData>> { new CultRecordRef<ItemData>(designKey) };
            author.Put(kind, kindKey);
        }
    }
}

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
    private static readonly (string Kind, string Id, string Name, string Description)[] Table =
    {
        ("mining", "extraction-rig", "Extraction Rig", "Drills, crushers and loaders that turn a station's miners into a steady flow of ore."),
        ("volatiles", "volatiles-skimmer", "Volatiles Skimmer", "Scoops, condensers and tankage that skim gas giants and ice for Methane, Ammonia and Water."),
        ("agriculture", "algae-vats", "Algae Vats", "Lit, circulating vats that grow Algae from Water and Potassium."),
        ("processing", "refinery", "Refinery", "Furnaces, centrifuges and reactors that refine ore and volatiles into Metals, Alloys and Compounds."),
        ("fabrication", "fabrication-line", "Fabrication Line", "Presses, printers and assembly cells that turn Alloys and Compounds into finished goods."),
        ("munitions-works", "munitions-line", "Munitions Line", "A sealed line that makes energetics and loads rounds, primers, fuzes, warheads and motors."),
    };

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
            Put(new GearData
            {
                Name = name, Description = description, Hardpoint = HardpointType.Facility, Shape = shape,
                Durability = 100, Mass = 250, Price = 1000, Behaviors = { new FacilityData() }
            }, designKey);
            var factions = catalog.GetAll<Faction>().Select(faction => (Key: catalog.RefOf(faction).Key, Faction: faction)).OrderBy(pair => pair.Key.Value, StringComparer.Ordinal).ToArray();
            var maker = factions.FirstOrDefault(pair => StationKinds.KindsOf(catalog, pair.Faction).Any(kind => kind.Key.Value == kindKey));
            if (maker.Faction == null) maker = factions.First();
            Put(new FactionProductData
            {
                Name = name, Description = description, Design = new CultRecordRef<CraftedItemData>(designKey),
                Manufacturer = new CultRecordRef<Faction>(maker.Key), NotOffered = true
            }, new CultRecordKey("product:facility:" + id));

            var kind = catalog.Get<StationKindData>(new CultRecordKey(kindKey)) ?? throw new InvalidOperationException($"A recipe names station kind '{kindKey}', which the catalog does not hold.");
            if (kind.Facilities.Count != 1 || !kind.Facilities[0].Key.Equals(designKey))
            {
                kind.Facilities = new List<CultRecordRef<ItemData>> { new CultRecordRef<ItemData>(designKey) };
                author.Put(kind, new CultRecordKey(kindKey));
            }
        }
        foreach (var recipe in catalog.GetAll<RecipeData>().ToArray())
        {
            var facility = FirstOf(catalog.Get(recipe.StationKind));
            if (recipe.Facility.IsSet() && recipe.Facility.Key.Equals(facility.Key)) continue;
            recipe.Facility = facility;
            author.Put(recipe, catalog.RefOf(recipe).Key);
        }
    }
}

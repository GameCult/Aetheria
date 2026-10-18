/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;
using CultMath;
using static CultMath.math;
using float2 = CultMath.float2;
using Random = CultMath.Random;
using JM.LinqFaster;
using UniRx;
using float4 = CultMath.float4;

public class ItemManager
{
    public Random Random = new Random((uint) (DateTime.Now.Ticks%uint.MaxValue));

    private Action<string> _logger;

    public CultCache ItemData { get; }
    public ProvenanceLedger Lots { get; }
    public GameplaySettings GameplaySettings { get; }

    public ItemManager(CultCache itemData, ProvenanceLedger lots, GameplaySettings settings, Action<string> logger)
    {
        ItemData = itemData;
        Lots = lots;
        GameplaySettings = settings;
        _logger = logger;
    }

    public void Log(string s)
    {
        _logger(s);
    }

    // The one resolution path from an item instance to its design
    public ItemData GetData(ItemInstance item) => ItemData.Get(item.Data);

    public SimpleCommodityData GetData(SimpleCommodity item) => GetData((ItemInstance) item) as SimpleCommodityData;

    public CraftedItemData GetData(CraftedItemInstance item) => GetData((ItemInstance) item) as CraftedItemData;

    public EquippableItemData GetData(EquippableItem item) => GetData((ItemInstance) item) as EquippableItemData;

    public float GetMass(ItemInstance item)
    {
        return item switch
        {
            CraftedItemInstance _ => GetData(item).Mass,
            SimpleCommodity commodity => GetData(item).Mass * commodity.Quantity,
            _ => 0
        };
    }

    public float GetThermalMass(ItemInstance item)
    {
        var data = GetData(item);
        return item switch
        {
            CraftedItemInstance _ => data.Mass * data.SpecificHeat,
            SimpleCommodity commodity => data.Mass * data.SpecificHeat * commodity.Quantity,
            _ => 0
        };
    }

    // The one resolution path from a crafted instance to the lot it was minted from.
    public Lot GetLot(CraftedItemInstance item) => Lots[item.Lot];

    // Returns stat when not equipped: a null-entity context with no heat, no power and no modifiers. It says so
    // in its own name (UnequippedStatContext) rather than being a third quiet dialect of the arithmetic.
    public float Evaluate(PerformanceStat stat, EquippableItem item)
    {
        var data = GetData(item);
        var result = stat.Evaluate(new UnequippedStatContext(this, item, data));
        if (float.IsNaN(result))
            throw new InvalidOperationException($"Performance Stat on {data.Name} evaluating as NaN: input data is invalid! Durability: {item.Durability} / {data.Durability}");
        return result;
    }

    // The unequipped case: quality (the lot's effective quality) and durability read live off the item and its lot; heat and modifiers do not
    // exist without an entity, so they resolve as the identity for their term rather than a bespoke formula.
    private sealed class UnequippedStatContext : IStatContext
    {
        private readonly EquippableItem _item;
        private readonly EquippableItemData _data;
        private readonly float _durabilityExponent;
        public Lot Lot { get; }

        public UnequippedStatContext(ItemManager items, EquippableItem item, EquippableItemData data)
        {
            _item = item;
            _data = data;
            Lot = items.GetLot(item);
            _durabilityExponent = lerp(
                items.GameplaySettings.DurabilityQualityMin,
                items.GameplaySettings.DurabilityQualityMax,
                pow(Lot.EffectiveQuality(), items.GameplaySettings.DurabilityQualityExponent));
        }

        public float HeatFactor(float exponent) => 1f;
        public float DurabilityFactor(float exponent) =>
            pow(_item.Durability / _data.Durability, _durabilityExponent * exponent);
        public float ConsumableProgressFactor(float exponent) => 1f;
        public float PowerSupplyFactor(float exponent) => 1f;
        public float ScaleModifier(PerformanceStat stat) => 1f;
        public float ConstantModifier(PerformanceStat stat) => 0f;
    }

    // Price follows effective quality (price-follows-effective-quality), so a sleeper's over-specced parts do not show in it.
    public int GetPrice(CraftedItemInstance item)
    {
        var data = GetData(item);
        return (int) (GameplaySettings.QualityPriceModifier.Evaluate(GetLot(item).EffectiveQuality()) * data.Price);
    }

    // Builds one of a manufacturer's branded products (quality-is-assembly-over-supply-chain): the unit's own quality is its
    // final assembly, drawn from the product's assembly distribution, else its maker's, else AssemblyDistribution's defaults,
    // and each of the design's roles is filled with a part whose quality is drawn from the product's distribution for it.
    public int CreateLot(FactionProductData product) => Mint(product, null);

    // The same lot at a stated quality: the assembly and every role fill are that quality and nothing is drawn, so a staged
    // fit does not ride the item stream (ScenarioStage.Fit).
    public int CreateLot(FactionProductData product, float quality) => Mint(product, clamp(quality, .01f, 1));

    private int Mint(FactionProductData product, float? stated)
    {
        var design = ItemData.Get(product.Design);
        var assembly = product.Assembly ?? ItemData.Get(product.Manufacturer)?.Assembly ?? new AssemblyDistribution();
        var lot = new Lot
        {
            Design = ItemData.RefOf<ItemData>(design),
            Origin = new Attributed { Faction = product.Manufacturer },
            Product = ItemData.RefOf(product),
            Quality = stated ?? clamp(Random.NextGaussian(assembly.Mean, assembly.StandardDeviation), .01f, 1),
            Roles = new List<RoleFill>()
        };
        if (design.Roles != null)
            foreach (var role in design.Roles)
            {
                var build = product.Roles?.FirstOrDefault(b => b.Role == role.Name) ?? new ProductRole();
                lot.Roles.Add(new RoleFill
                {
                    Role = role.Name,
                    Quality = stated ?? clamp(Random.NextGaussian(build.Mean, build.StandardDeviation), .01f, 1)
                });
            }

        return Lots.Add(lot);
    }

    // No branded product: a lot attributed to maker (which may be unset) with no role fills, e.g. console `give`.
    public int CreateLot(CraftedItemData design, CultRecordRef<Faction> maker, float quality)
    {
        var lot = new Lot
        {
            Design = ItemData.RefOf<ItemData>(design),
            Origin = new Attributed { Faction = maker },
            Quality = quality
        };
        return Lots.Add(lot);
    }

    // The one writer of a crafted instance's Data: copied from the lot's design, never independently set.
    public CraftedItemInstance CreateInstance(int lot)
    {
        var l = Lots[lot];
        var design = ItemData.Get(l.Design) as CraftedItemData;
        if (design is EquippableItemData equippableItemData)
        {
            return new EquippableItem
            {
                Data = l.Design, Lot = lot, Durability = equippableItemData.Durability
            };
        }

        if (design is ConsumableItemData)
            return new ConsumableItem { Data = l.Design, Lot = lot };

        return new CompoundCommodity
        {
            Data = l.Design, Lot = lot
        };
    }

    public CraftedItemInstance CreateInstance(FactionProductData product)
    {
        var design = ItemData.Get(product.Design);
        if (design == null)
        {
            _logger($"Product {product.Name} names a design that does not exist!");
            return null;
        }

        return CreateInstance(CreateLot(product));
    }

    // Who made an item and what it is branded as. The maker derives from its lot's provenance and the product is the
    // one its lot records; nothing re-derives it. Null maker (an unset faction, or an Extracted lot with none) means
    // no brand at all; a set maker whose lot records no product (or one the catalog no longer holds) still shows
    // the maker, with no product name or flavour text.
    public (Faction Maker, FactionProductData Product) Brand(CraftedItemInstance item)
    {
        var lot = GetLot(item);
        var maker = lot.Origin switch
        {
            Attributed attributed => attributed.Faction,
            Produced produced => produced.Faction,
            _ => default
        };
        if (!maker.IsSet()) return (null, null);

        var product = ItemData.Get(lot.Product);
        return (ItemData.Get(maker), product);
    }

    // What the player reads an item as: its lot's product name when it records one,
    // otherwise the design's own name (unbranded, console-given and extracted lots, and commodities).
    public string Title(ItemInstance item) =>
        (item is CraftedItemInstance crafted ? Brand(crafted).Product?.Name : null) ?? GetData(item).Name;

    // The tier name and colour follow price, so they hide the same sleepers.
    public (RarityTier tier, int upgrades) GetTier(CraftedItemInstance item)
    {
        var quality = GetLot(item).EffectiveQuality();
        var tier = GameplaySettings.Tiers[0];
        foreach (var t in GameplaySettings.Tiers)
            if (quality + .001f > t.Quality)
                tier = t;
        int upgrades = (int) ((quality - tier.Quality) / .0499f);
        return (tier, upgrades);
    }
}

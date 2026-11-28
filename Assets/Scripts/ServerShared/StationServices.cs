using System;
using System.Collections.Generic;
using System.Linq;

// What a station pays for goods and charges to mend gear. The one owner of the sell price, the repair cost, and the
// two commits that move goods or restore durability against the run's credits; TradeMenu shows these numbers and
// decides none of them. It holds no Unity type. Credits are the caller's: a refusal leaves them, the cargo and the
// durabilities exactly as they were.
public static class StationServices
{
    // What the station charges for the item, or for `quantity` units of a commodity, simple or compound (a crafted item
    // is one unit). The number TradeMenu shows, checks against the run's credits and charges.
    public static int BuyPrice(ItemManager items, ItemInstance item, int quantity = 1)
    {
        switch (item)
        {
            case CompoundCommodity compound:
                return (int) Math.Min((long) items.GetPrice(compound) * quantity, int.MaxValue);
            case CraftedItemInstance crafted:
                return items.GetPrice(crafted);
            case SimpleCommodity commodity:
                return (int) Math.Min((long) items.GetData(commodity).Price * quantity, int.MaxValue);
            default:
                throw new ArgumentException($"{item?.GetType().Name ?? "null"} has no buy price.", nameof(item));
        }
    }

    // What the station pays for the item: its price, scaled by SellFraction and by its condition. A commodity has no
    // wear, so it sells at the fraction alone; a commodity's price, simple or compound, is per unit.
    public static int SellPrice(ItemManager items, ItemInstance item)
    {
        var fraction = (double) items.GameplaySettings.SellFraction;
        switch (item)
        {
            case CompoundCommodity compound:
                return (int) Math.Floor((long) items.GetPrice(compound) * compound.Quantity * fraction);
            case CraftedItemInstance crafted:
                return SellValue(items.GetPrice(crafted), fraction, Condition(items, crafted));
            case SimpleCommodity commodity:
                return (int) Math.Floor((long) items.GetData(commodity).Price * commodity.Quantity * fraction);
            default:
                throw new ArgumentException($"{item?.GetType().Name ?? "null"} has no sell price.", nameof(item));
        }
    }

    // What the station charges to restore the entity's hull armour, hull and every item it carries to design: each
    // piece's share of its price in proportion to its wear, scaled by the repair fraction, rounded up. Armour has no
    // price of its own, so the worn share of the hull's total armour is charged as that share of the hull's price.
    public static int RepairCost(ItemManager items, Entity entity)
    {
        var fraction = RepairFraction(items);
        long total = (long) Math.Ceiling(ArmourWear(entity) * items.GetPrice(entity.Hull) * fraction);
        foreach (var gear in Gear(entity)) total += RepairShare(items, gear, fraction);
        return (int) Math.Min(total, int.MaxValue);
    }

    // What the station charges for each unit of wear it mends: RepairFraction, but never less than SellFraction, so
    // mending an item before selling it never pays more than the repair cost.
    private static double RepairFraction(ItemManager items) =>
        Math.Max(items.GameplaySettings.RepairFraction, items.GameplaySettings.SellFraction);

    // The worn share of the entity's armour, 0 when it is whole or has none.
    private static double ArmourWear(Entity entity)
    {
        double max = 0, worn = 0;
        for (var x = 0; x < entity.MaxArmor.GetLength(0); x++)
            for (var y = 0; y < entity.MaxArmor.GetLength(1); y++)
            {
                max += entity.MaxArmor[x, y];
                worn += Math.Max(0, entity.MaxArmor[x, y] - entity.Armor[x, y]);
            }
        return max > 0 ? worn / max : 0;
    }

    // The purchase: the station charges BuyPrice, and `deliver` puts the goods where they go. Credits are checked
    // and charged here, at the same boundary as TryRepair: a price equal to the credits held is affordable.
    public static BuyResult TryBuy(ItemManager items, ItemInstance item, int quantity, ref int credits, Func<bool> deliver)
    {
        var price = BuyPrice(items, item, quantity);
        if (price > credits) return BuyResult.ShortOfCredits;
        if (!deliver()) return BuyResult.NoRoom;
        credits -= price;
        return BuyResult.Bought;
    }

    // The purchase of `quantity` units of a station stack, delivered as one lot. A bay holds no stack above its
    // design's MaxStack, so a stack the station holds is a lot already; a stack above it comes from outside a bay and is
    // refused, as is a purchase of more than the stack holds or of a stack that holds nothing. All of that is refused
    // before anything moves, so `deliver` moves units the station has and the charge is for the units it delivered.
    public static BuyResult TryBuyLots(ItemManager items, ItemInstance stock, int quantity, ref int credits, Func<int, bool> deliver)
    {
        if (!(stock is ICommodityStack stack) || quantity < 1 || quantity > stack.Quantity || stack.Quantity > items.MaxStack(stock))
            return BuyResult.NoRoom;
        return TryBuy(items, stock, quantity, ref credits, () => deliver(quantity));
    }

    // The quantity a Buy Quantity field may hold: what was typed, at most what the credits cover and what the station
    // has, and never below one unit (a price the credits do not cover is then refused as short of credits).
    public static int BuyableQuantity(ItemManager items, ItemInstance stock, int typed, int credits) =>
        Math.Max(1, Math.Min(Math.Min(typed, credits / BuyPrice(items, stock)), stock is ICommodityStack stack ? stack.Quantity : 1));

    // The sale of a crafted item: it leaves the bay and the station pays SellPrice, into one of the station's cargo bays;
    // false, with the item in its bay and the credits unchanged, when no bay can take it. A commodity stack sells by
    // TrySellPart, which says how many units sold and why not more (aetheria-living-world:ruling:no-market-absorption: no
    // unit is paid for that no bay took). The crafted branch is ungated: follow-up gear-sale-by-answer gates it once the
    // operator answers question gear-resale-under-installed-gear.
    public static bool TrySell(EquippedCargoBay from, Entity station, ItemInstance item, ref int credits)
    {
        if (item is ICommodityStack)
            throw new ArgumentException("A commodity stack sells through TrySellPart, which says how many units sold.", nameof(item));
        var price = SellPrice(station.ItemManager, item);
        if (!station.CargoBays.Any(bay => from.TryTransferItem(bay, item, 1))) return false;
        credits += price;
        return true;
    }

    // The sale of a commodity stack: only a good the station wants (StationWants; NotWanted moves nothing). The station's bays
    // take what they can hold (aetheria-living-world:ruling:no-market-absorption), the station pays SellPrice for those units
    // alone, and the rest stays in the seller's hold with the refusal NoRoom. Sold units are moved, never absorbed.
    public static SaleResult TrySellPart(EquippedCargoBay from, Entity station, ICommodityStack stack, ref int credits)
    {
        var item = (ItemInstance) stack;
        var offered = stack.Quantity;
        if (!from.Cargo.ContainsKey(item)) return new SaleResult(0, offered, SaleRefusal.None);
        if (!StationWants.Of(station).Contains(item.Data.Key)) return new SaleResult(0, offered, SaleRefusal.NotWanted);
        var sold = 0;
        var remaining = offered;
        while (remaining > 0)
        {
            var moved = 0;
            for (var quantity = remaining; quantity >= 1 && moved == 0; quantity--)
            {
                var q = quantity;
                var part = stack.Split(q);
                if (station.CargoBays.Any(bay => from.TryTransferItem(bay, item, q))) moved = q;
                credits += moved > 0 ? SellPrice(station.ItemManager, part) : 0;
            }
            if (moved == 0) break;
            sold += moved;
            remaining -= moved;
        }
        return new SaleResult(sold, offered, sold < offered ? SaleRefusal.NoRoom : SaleRefusal.None);
    }

    // The ore stacks of the bays are the stacks of any design a field kind yields, mined, bought or looted alike (ruling
    // simple-commodities-fungible); ammunition and other simple goods no belt yields are not ore.
    private static ItemInstance[] OreStacks(ItemManager items, IEnumerable<EquippedCargoBay> bays) =>
        bays.SelectMany(bay => bay.Cargo.Keys.OfType<SimpleCommodity>().Where(stack => FieldKinds.IsOre(items.ItemData, items.GetData(stack))).Select(stack => (ItemInstance) stack)).ToArray();

    // What the station would pay for every ore stack of the bays that it wants: the sum of SellPrice over the stacks TrySellOre
    // sells. A display value: it ignores the station's room.
    public static int OreValue(ItemManager items, IEnumerable<EquippedCargoBay> bays, Entity station) =>
        (int) Math.Min(WantedOreStacks(items, bays, station).Sum(stack => (long) SellPrice(items, stack)), int.MaxValue);

    private static ItemInstance[] WantedOreStacks(ItemManager items, IEnumerable<EquippedCargoBay> bays, Entity station)
    {
        var wants = StationWants.Of(station);
        return OreStacks(items, bays).Where(stack => wants.Contains(stack.Data.Key)).ToArray();
    }

    // Sells every ore stack of the bays that the station wants, each through TrySellPart, so the price and the room rule are the single-stack
    // path's. Returns the units sold and offered, with the last refusal.
    public static SaleResult TrySellOre(IEnumerable<EquippedCargoBay> bays, Entity station, ref int credits)
    {
        var bayList = bays.ToArray();
        int sold = 0, offered = 0;
        var refusal = SaleRefusal.None;
        foreach (var stack in WantedOreStacks(station.ItemManager, bayList, station))
        {
            var from = bayList.First(bay => bay.Cargo.ContainsKey(stack));
            var result = TrySellPart(from, station, (ICommodityStack) stack, ref credits);
            sold += result.Sold;
            offered += result.Offered;
            if (result.Refusal != SaleRefusal.None) refusal = result.Refusal;
        }
        return new SaleResult(sold, offered, refusal);
    }

    // The repair: armour and every durability go back to design and the station charges RepairCost. False, with
    // nothing restored, when the credits fall short.
    public static bool TryRepair(Entity entity, ref int credits)
    {
        var items = entity.ItemManager;
        var cost = RepairCost(items, entity);
        if (cost > credits) return false;
        foreach (var gear in Gear(entity))
            if (items.GetData(gear) is EquippableItemData design && design.Durability > 0)
                gear.Durability = design.Durability;
        Array.Copy(entity.MaxArmor, entity.Armor, entity.MaxArmor.Length);
        credits -= cost;
        return true;
    }

    // Equipment holds the hull itself, so the hull is in the sum once.
    private static IEnumerable<EquippableItem> Gear(Entity entity) =>
        entity.Equipment.Select(equipped => equipped.EquippableItem)
            .Concat(entity.CargoBays.Select(bay => bay.EquippableItem))
            .Concat(entity.DockingBays.Select(bay => bay.EquippableItem));

    // Instance durability over the design's, for gear that wears; everything else is as good as new.
    private static double Condition(ItemManager items, CraftedItemInstance item)
    {
        if (!(item is EquippableItem gear) || !(items.GetData(gear) is EquippableItemData design) || design.Durability <= 0)
            return 1;
        return Math.Max(0, Math.Min(1, gear.Durability / (double) design.Durability));
    }

    // The sell value of a price at a condition, rounded down. The one place the sell rounding lives: RepairShare
    // prices the sell value a repair adds from the same function, so the two cannot round apart.
    private static int SellValue(int price, double fraction, double condition) =>
        (int) Math.Floor(price * fraction * condition);

    // The share of the repair charge for one piece of gear: its wear at the repair fraction, rounded up, and never
    // less than the sell value the repair adds, so mending before selling never nets credits.
    private static long RepairShare(ItemManager items, EquippableItem gear, double fraction)
    {
        var price = items.GetPrice(gear);
        var condition = Condition(items, gear);
        var sellFraction = (double) items.GameplaySettings.SellFraction;
        var added = SellValue(price, sellFraction, 1) - SellValue(price, sellFraction, condition);
        return Math.Max(added, (long) Math.Ceiling((1 - condition) * price * fraction));
    }
}

public enum BuyResult { Bought, ShortOfCredits, NoRoom }

// Why a commodity sale moved fewer units than it offered: the station does not buy the good, or its bays are full.
public enum SaleRefusal { None, NotWanted, NoRoom }

// A commodity sale: the units sold, the units offered, and why the rest stayed.
public readonly struct SaleResult
{
    public readonly int Sold;
    public readonly int Offered;
    public readonly SaleRefusal Refusal;

    public SaleResult(int sold, int offered, SaleRefusal refusal)
    {
        Sold = sold;
        Offered = offered;
        Refusal = refusal;
    }
}

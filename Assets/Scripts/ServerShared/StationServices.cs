using System;
using System.Collections.Generic;
using System.Linq;

// What a station pays for goods and charges to mend gear. The one owner of the sell price, the repair cost, and the
// two commits that move goods or restore durability against the run's credits; TradeMenu shows these numbers and
// decides none of them. It holds no Unity type. Credits are the caller's: a refusal leaves them, the cargo and the
// durabilities exactly as they were.
public static class StationServices
{
    // What the station charges for the item, or for `quantity` units of a commodity (a crafted item is one unit). The
    // number TradeMenu shows, checks against the run's credits and charges.
    public static int BuyPrice(ItemManager items, ItemInstance item, int quantity = 1)
    {
        switch (item)
        {
            case CraftedItemInstance crafted:
                return items.GetPrice(crafted);
            case SimpleCommodity commodity:
                return (int) Math.Min((long) items.GetData(commodity).Price * quantity, int.MaxValue);
            default:
                throw new ArgumentException($"{item?.GetType().Name ?? "null"} has no buy price.", nameof(item));
        }
    }

    // What the station pays for the item: its price, scaled by SellFraction and by its condition. A commodity has no
    // wear, so it sells at the fraction alone; a simple commodity's price is per unit.
    public static int SellPrice(ItemManager items, ItemInstance item)
    {
        var fraction = (double) items.GameplaySettings.SellFraction;
        switch (item)
        {
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

    // The sale: the item leaves the bay for one of the station's cargo bays and the station pays SellPrice. False,
    // with the item in its bay and the credits unchanged, when no station bay can take it.
    public static bool TrySell(EquippedCargoBay from, Entity station, ItemInstance item, ref int credits)
    {
        var price = SellPrice(station.ItemManager, item);
        var moved = item is SimpleCommodity commodity
            ? station.CargoBays.Any(bay => from.TryTransferItem(bay, commodity, commodity.Quantity))
            : station.CargoBays.Any(bay => from.TryTransferItem(bay, (CraftedItemInstance) item));
        if (!moved) return false;
        credits += price;
        return true;
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

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
                return (int) Math.Floor(items.GetPrice(crafted) * fraction * Condition(items, crafted));
            case SimpleCommodity commodity:
                return (int) Math.Floor((long) items.GetData(commodity).Price * commodity.Quantity * fraction);
            default:
                throw new ArgumentException($"{item?.GetType().Name ?? "null"} has no sell price.", nameof(item));
        }
    }

    // What the station charges to restore the entity's hull and every item it carries to design durability: each
    // piece's share of its price in proportion to its wear, scaled by RepairFraction, rounded up.
    public static int RepairCost(ItemManager items, Entity entity)
    {
        long total = 0;
        foreach (var gear in Gear(entity)) total += RepairShare(items, gear);
        return (int) Math.Min(total, int.MaxValue);
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

    // The repair: every durability goes back to design and the station charges RepairCost. False, with nothing
    // restored, when the credits fall short.
    public static bool TryRepair(Entity entity, ref int credits)
    {
        var items = entity.ItemManager;
        var cost = RepairCost(items, entity);
        if (cost > credits) return false;
        foreach (var gear in Gear(entity))
            if (items.GetData(gear) is EquippableItemData design && design.Durability > 0)
                gear.Durability = design.Durability;
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

    private static long RepairShare(ItemManager items, EquippableItem gear)
    {
        var worn = 1 - Condition(items, gear);
        return (long) Math.Ceiling(worn * items.GetPrice(gear) * items.GameplaySettings.RepairFraction);
    }
}

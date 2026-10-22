/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Linq;
using CultMath;
using static CultMath.math;

// Why a give ended. Notification only: nothing reads it to decide anything.
public enum GiveEnd
{
    Done,
    Stopped,
    OutOfRange,
    ReceiverGone,
    ReceiverFull,
    NothingLeft,
    GiverDocked
}

// A pilot's standing offer of goods to a ship alongside. Runtime only: nothing about a give is saved.
public sealed class GiveOrder
{
    public readonly Entity Receiver;
    // A commodity order is for any stack of this item's design in the giver's bays; a crafted order is for this instance.
    public readonly ItemInstance Item;
    // Units still to move.
    public int Remaining { get; internal set; }
    // Units earned and not yet moved, fractional between steps.
    internal double Budget;

    internal GiveOrder(Entity receiver, ItemInstance item, int quantity)
    {
        Receiver = receiver;
        Item = item;
        Remaining = quantity;
    }
}

// The one owner of a give's progress and its end (ruling tender-transfer-verb). A ship's own pilot asks for a give through
// PilotCommand.Give and PilotApplier starts it here; nothing else moves goods out of a hold in space (ruling
// transfer-is-give-only). Every unit moves through EquippedCargoBay.TryTransferItem, the one conserving bay-to-bay commit.
public static class CargoTransfer
{
    internal static void Begin(Entity giver, GiveOrder order)
    {
        End(giver, GiveEnd.Stopped);
        giver.Giving = order;
    }

    // Ends the giver's give, if it has one, for the reason.
    internal static void End(Entity giver, GiveEnd reason)
    {
        if (giver.Giving == null) return;
        giver.Giving = null;
        giver.LastGiveEnd = reason;
    }

    // One step of every give in the zone: the receiver is still here and alongside, and the rate earns units, each moved
    // into the first receiver bay that takes it.
    public static void Step(Zone zone, float deltaTime)
    {
        foreach (var giver in zone.Entities.ToArray())
        {
            var order = giver.Giving;
            if (order == null) continue;
            var settings = giver.ItemManager.GameplaySettings;
            var range = settings.DockingDistance;
            if (!zone.Entities.Contains(order.Receiver))
            {
                End(giver, GiveEnd.ReceiverGone);
                continue;
            }
            if (lengthsq(order.Receiver.Position.xz - giver.Position.xz) > range * range)
            {
                End(giver, GiveEnd.OutOfRange);
                continue;
            }

            order.Budget += (double) settings.TransferRate * deltaTime;
            while (order.Budget >= 1 && order.Remaining > 0)
            {
                var source = Source(giver, order);
                if (source.Bay == null)
                {
                    End(giver, GiveEnd.NothingLeft);
                    break;
                }
                if (!Move(source.Bay, source.Item, order.Receiver))
                {
                    End(giver, GiveEnd.ReceiverFull);
                    break;
                }
                order.Budget -= 1;
                order.Remaining--;
            }
            if (order.Remaining == 0) End(giver, GiveEnd.Done);
        }
    }

    // Where the next unit comes from: the first stack of the order's design in the giver's bays, or the crafted instance.
    private static (EquippedCargoBay Bay, ItemInstance Item) Source(Entity giver, GiveOrder order)
    {
        foreach (var bay in giver.CargoBays)
        {
            if (order.Item is CraftedItemInstance crafted)
            {
                if (bay.Cargo.ContainsKey(crafted)) return (bay, crafted);
            }
            else if (bay.ItemsOfType.TryGetValue(order.Item.Data.Key, out var stacks))
            {
                var stack = stacks.OfType<SimpleCommodity>().FirstOrDefault(s => s.Quantity > 0);
                if (stack != null) return (bay, stack);
            }
        }
        return (null, null);
    }

    // One unit of a commodity, or the crafted item, into the first receiver bay that takes it.
    private static bool Move(EquippedCargoBay from, ItemInstance item, Entity receiver) =>
        item is SimpleCommodity commodity
            ? receiver.CargoBays.Any(bay => from.TryTransferItem(bay, commodity, 1))
            : receiver.CargoBays.Any(bay => from.TryTransferItem(bay, (CraftedItemInstance) item));
}

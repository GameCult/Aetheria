using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;

// The interim haul (aetheria-living-world:ruling:transit-depots-buy-for-onward-haul, demo-zenith-ore-depot): which ship carries for which
// standing order (Entity.Haul), where it flies (the leg, derived from its hold every update), and the one dock call that moves goods:
// the hauler docks (ruling docking-is-enclosed-bays), the From station's silo loads it or the To station takes its cargo through
// StationServices.TryStorePart, and it undocks piloted. No payment moves: both ends are in one comms cell and no station holds an account
// (ruling comms-cells-are-zones). The ship's task, orders and cargo are the ship's own; nothing here is stored.
public static class HaulRun
{
    public static HaulOrderData OrderOf(ItemManager items, CultRecordKey order) =>
        order.IsSet() ? items.ItemData.Get<HaulOrderData>(order) : null;

    // The task a ship carrying a haul order starts on, derived at every admission; null for a ship with no order or whose order is gone.
    public static HaulingTask TaskOf(Ship ship) =>
        ship.Haul.Key.IsSet() && OrderOf(ship.ItemManager, ship.Haul.Key) != null ? new HaulingTask { Order = ship.Haul.Key } : null;

    // The orbit to steer for: the To station's while the hold holds any good of the order, the From station's otherwise.
    public static CultRecordKey Leg(Ship ship, HaulOrderData order) =>
        Carried(ship, order).Any() ? order.To.Key : order.From.Key;

    public static OrbitalEntity Station(Zone zone, CultRecordKey orbit) =>
        zone.Entities.OfType<OrbitalEntity>().FirstOrDefault(station => station.OrbitData.Equals(orbit));

    // Whether a call at this station would move a unit: read-only, stored nowhere. At the From station the silo holds a good of the order the
    // hold has room for; at the To station the station wants a carried good and has room for a unit of it. Either needs a free docking bay.
    public static bool CanCall(Ship ship, OrbitalEntity station, HaulOrderData order)
    {
        if (station.DockingBays.All(bay => bay.DockedShip != null)) return false;
        var orbit = station.OrbitData;
        if (orbit.Equals(order.From.Key))
            return station.CargoBays.Where(bay => bay.Data is SiloData).SelectMany(bay => bay.Cargo.Keys).OfType<ICommodityStack>()
                .Any(stack => Goods(order).Contains(((ItemInstance) stack).Data.Key) && ship.CargoBays.Any(bay => bay.TryFindSpace(stack.Split(1))));
        if (orbit.Equals(order.To.Key))
            return Carried(ship, order).Any(stack => StationWants.Bays(station, ((ItemInstance) stack).Data.Key).Any(bay => bay.TryFindSpace(stack.Split(1))));
        return false;
    }

    // Docks at the station, loads at the From station or delivers at the To station, and undocks piloted: a ship that cannot
    // move a unit does not dock. The pilot's Call verb is the only caller (PilotApplier).
    public static void Call(Ship ship, Entity station)
    {
        var order = OrderOf(ship.ItemManager, ship.Haul.Key);
        if (order == null || !(station is OrbitalEntity depot) || !CanCall(ship, depot, order) || depot.TryDock(ship) == null) return;
        if (depot.OrbitData.Equals(order.From.Key)) StationServices.Load(depot, Goods(order), ship);
        else
            foreach (var bay in ship.CargoBays.ToArray())
                foreach (var stack in bay.Cargo.Keys.OfType<ICommodityStack>().Where(stack => Goods(order).Contains(((ItemInstance) stack).Data.Key)).ToArray())
                    StationServices.TryStorePart(bay, depot, stack);
        depot.TryUndock(ship, piloted: true);
    }

    private static IReadOnlyCollection<CultRecordKey> Goods(HaulOrderData order) => order.Goods.Select(good => good.Key).ToArray();

    private static IEnumerable<ICommodityStack> Carried(Ship ship, HaulOrderData order)
    {
        var goods = Goods(order);
        return ship.CargoBays.SelectMany(bay => bay.Cargo.Keys).OfType<ICommodityStack>().Where(stack => goods.Contains(((ItemInstance) stack).Data.Key)).ToArray();
    }
}

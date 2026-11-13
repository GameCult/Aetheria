using System.Collections.Generic;
using System.Linq;
using GameCult.Caching;

public enum GainKind { Mined, Collected }

// One entry into a hold: what Extract stored (and let float) of one ore, or what a pickup collected. Stored plus Adrift is
// the units; a pickup has Adrift 0. Published on Zone.CargoGained by its two writers and read by presentations only.
public struct CargoGain
{
    public Entity Taker;
    public CultRecordRef<ItemData> Design;
    public int Lot;
    public int Stored;
    public int Adrift;
    public GainKind Kind;
}

// The words for a cargo gain, and the taker's hold fill. Nothing here is stored: the line and the fill are derived on
// every call.
public static class HaulFeed
{
    public static string Line(CargoGain gain, ItemManager items)
    {
        var name = items.ItemData.Get(gain.Design).Name;
        var (used, total) = HoldFill(gain.Taker?.CargoBays ?? Enumerable.Empty<EquippedCargoBay>());
        var hold = $"(hold {used}/{total})";
        if (gain.Kind == GainKind.Collected) return $"Collected {gain.Stored} {name} {hold}";
        return gain.Adrift > 0
            ? $"+{gain.Stored} {name}, {gain.Adrift} adrift {hold}"
            : $"+{gain.Stored} {name} {hold}";
    }

    // Occupied cells over the bays' interiors, a multi-cell item counted per cell.
    public static (int used, int total) HoldFill(IEnumerable<EquippedCargoBay> bays)
    {
        int used = 0, total = 0;
        foreach (var bay in bays)
            foreach (var v in bay.Data.InteriorShape.AllCoordinates)
            {
                if (!bay.Data.InteriorShape[v]) continue;
                total++;
                if (bay.Occupancy[v.x, v.y] != null) used++;
            }
        return (used, total);
    }
}

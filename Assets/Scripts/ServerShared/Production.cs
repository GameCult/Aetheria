using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using GameCult.Caching;
using MessagePack;
using Newtonsoft.Json;

// A facility's cycle in progress, on the installed unit (EquippableItem.Run): the recipe it last ran or is running, and the sim
// seconds of the running cycle. Progress 0 is idle, and Recipe is then the round-robin cursor. Written only by Facility.Execute
// from Production.Advance's result; the inputs of a running cycle are already taken and live nowhere else.
[MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class FacilityRun
{
    [JsonProperty("recipe"), Key(0)] public CultRecordRef<RecipeData> Recipe;
    [JsonProperty("progress"), Key(1)] public double Progress;
}

// Why a facility is not advancing, derived on each call and stored nowhere.
public enum Stall { None, Inputs, Room, Unrunnable }

// Where a facility's goods are: its host's bays on screen, a stock ledger off screen. Each write is whole or nothing.
public interface IProductionHold
{
    int Held(CultRecordKey design);
    bool TryTake(IReadOnlyList<(CultRecordKey design, int units)> units);
    bool TryPut(CultRecordKey design, int units);
}

// The one rule for a facility cycle: which recipe starts, which units it takes, how far it has run, what it stores. On screen its
// one caller is Facility.Execute, with one zone step; off screen the strategic tick calls it with a longer span. It reads no
// random, no clock and no crew.
public static class Production
{
    // A recipe a facility can run today: it takes something, and makes a simple commodity (a graded output needs a lot).
    public static bool Runs(CultCache catalog, RecipeData recipe) =>
        !Recipes.IsRaw(catalog, recipe) && catalog.Get(recipe.Output) is SimpleCommodityData;

    // The recipes a facility design runs, in recipe-key order. Derived from the catalog, never stored.
    public static IReadOnlyList<RecipeData> RunList(CultCache catalog, CultRecordKey facilityDesign) =>
        Recipes.RunOn(catalog, facilityDesign).Where(recipe => Runs(catalog, recipe)).ToArray();

    // The units one cycle of the recipe takes from the hold, summed per design: Inputs, then Upkeep, then each role slot filled
    // greedily across Recipes.Fillers order. Null when the hold is short of any.
    public static IReadOnlyList<(CultRecordKey design, int units)> Take(CultCache catalog, RecipeData recipe, IProductionHold hold)
    {
        var need = new List<(CultRecordKey design, int units)>();
        void Add(CultRecordKey design, int units)
        {
            var at = need.FindIndex(entry => entry.design.Equals(design));
            if (at < 0) need.Add((design, units));
            else need[at] = (design, need[at].units + units);
        }
        int Needed(CultRecordKey design) => need.Where(entry => entry.design.Equals(design)).Sum(entry => entry.units);

        foreach (var input in recipe.Inputs) Add(input.Commodity.Key, input.Count);
        foreach (var upkeep in recipe.Upkeep) Add(upkeep.Commodity.Key, upkeep.Count);
        if (need.Any(entry => hold.Held(entry.design) < entry.units)) return null;

        foreach (var role in Recipes.RoleInputsOf(catalog, recipe))
        {
            var left = role.Count;
            foreach (var filler in Recipes.Fillers(catalog, role.Name))
            {
                var units = Math.Min(left, hold.Held(filler) - Needed(filler));
                if (units < 1) continue;
                Add(filler, units);
                left -= units;
                if (left == 0) break;
            }
            if (left > 0) return null;
        }
        return need;
    }

    // The facility's state after `seconds` of sim time, and why it is not advancing. An idle facility (run null or Progress 0)
    // starts the first recipe after the cursor, wrapping, whose units the hold grants, and takes them at once; a running one adds
    // time; at Progress >= Cycle it stores the output and goes idle, the recipe staying as the cursor. A store the hold refuses
    // holds the batch at Cycle (Room) and retries on the next call. Time left after a cycle ends runs the next one in the same call,
    // so a long span runs several cycles (the rule that makes one span equal as many steps as it covers); time that no cycle can start is
// dropped. A facility with nothing to start writes nothing.
    public static (FacilityRun run, Stall stall) Advance(CultCache catalog, IReadOnlyList<RecipeData> runList, FacilityRun run,
        double seconds, IProductionHold hold)
    {
        if (runList.Count == 0) return (run, Stall.Unrunnable);
        while (true)
        {
            if (run == null || run.Progress <= 0)
            {
                if (seconds <= 0) return (run, Stall.None);
                var cursor = run == null ? -1 : IndexOf(runList, run.Recipe.Key);
                RecipeData start = null;
                for (var step = 1; step <= runList.Count && start == null; step++)
                {
                    var candidate = runList[(cursor + step + runList.Count) % runList.Count];
                    var units = Take(catalog, candidate, hold);
                    if (units != null && hold.TryTake(units)) start = candidate;
                }
                if (start == null) return (run, Stall.Inputs);
                run = new FacilityRun { Recipe = new CultRecordRef<RecipeData>(RecipeData.KeyOf(start.Output.Key)), Progress = 0 };
            }

            var recipe = catalog.Get(run.Recipe);
            // A run whose recipe the catalog no longer has, or no longer runs here (a save outliving a catalog edit), is dropped: its
            // progress is gone and the inputs it took stay consumed, as they are taken whole at cycle start and the recipe that named
            // them is not there to return them against. The facility then starts its next recipe, and Advance never throws on it.
            if (recipe == null || !Runs(catalog, recipe))
            {
                run = null;
                continue;
            }
            var progress = run.Progress;
            if (progress < recipe.Cycle)
            {
                var spent = Math.Min(seconds, recipe.Cycle - progress);
                progress += spent;
                seconds -= spent;
            }
            if (progress < recipe.Cycle) return (new FacilityRun { Recipe = run.Recipe, Progress = progress }, Stall.None);
            if (!hold.TryPut(recipe.Output.Key, recipe.OutputCount))
                return (new FacilityRun { Recipe = run.Recipe, Progress = recipe.Cycle }, Stall.Room);
            run = new FacilityRun { Recipe = run.Recipe, Progress = 0 };
            if (seconds <= 0) return (run, Stall.None);
        }
    }

    private static int IndexOf(IReadOnlyList<RecipeData> runList, CultRecordKey recipe)
    {
        for (var at = 0; at < runList.Count; at++)
            if (RecipeData.KeyOf(runList[at].Output.Key).Equals(recipe)) return at;
        return -1;
    }
}

// An entity's bays as a production hold. Takes go bay by bay in cell order, so a take never depends on how the stacks came to
// lie; a take or a put that cannot finish puts everything back.
public sealed class EntityHold : IProductionHold
{
    private readonly Entity _entity;

    public EntityHold(Entity entity) => _entity = entity;

    // The entity's cargo revision: moves on every change to the bays' contents and on every bay seated or removed, never repeats.
    public long Revision => _entity.CargoRevision;

    public int Held(CultRecordKey design) => _entity.CargoBays.Sum(bay => bay.Count(design));

    public bool TryTake(IReadOnlyList<(CultRecordKey design, int units)> units)
    {
        var taken = new List<(EquippedCargoBay bay, ItemInstance part, int2 origin)>();
        foreach (var (design, count) in units)
        {
            var left = count;
            foreach (var bay in _entity.CargoBays)
            {
                var stacks = new List<ItemInstance>();
                foreach (var cell in bay.Data.InteriorShape.Coordinates)
                {
                    var held = bay.Occupancy[cell.x, cell.y];
                    if (held is ICommodityStack && held.Data.Key.Equals(design) && !stacks.Contains(held)) stacks.Add(held);
                }
                foreach (var stack in stacks)
                {
                    if (left == 0) break;
                    var origin = bay.Cargo[stack];
                    var part = bay.Remove(stack, Math.Min(left, ((ICommodityStack) stack).Quantity));
                    taken.Add((bay, part, origin));
                    left -= ((ICommodityStack) part).Quantity;
                }
            }
            if (left == 0) continue;
            for (var at = taken.Count - 1; at >= 0; at--) taken[at].bay.TryStore(taken[at].part, taken[at].origin);
            return false;
        }
        return true;
    }

    public bool TryPut(CultRecordKey design, int units)
    {
        var data = _entity.ItemManager.ItemData.Get<SimpleCommodityData>(design);
        var stored = 0;
        for (var left = units; left > 0;)
        {
            var chunk = Math.Min(left, data.MaxStack);
            var part = new SimpleCommodity { Data = new CultRecordRef<ItemData>(design), Quantity = chunk };
            if (!_entity.CargoBays.Any(bay => bay.TryStore(part)))
            {
                if (stored > 0) TryTake(new[] { (design, stored) });
                return false;
            }
            stored += chunk;
            left -= chunk;
        }
        return true;
    }
}

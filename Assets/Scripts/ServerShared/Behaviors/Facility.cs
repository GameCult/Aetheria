using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;

// The facility gear a station carries to run recipes: a GearData of HardpointType Facility whose Behaviors hold one of these. The type
// says where it mounts (a hull's Facility rows), this says it runs recipes. It holds no field of its own: which recipes a facility
// runs is derived from RecipeData.Facility, so no list is stored here. A facility is installed gear, never a consumable's effect.
[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class FacilityData : BehaviorData
{
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Facility(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        throw new System.InvalidOperationException($"{item.Data.Name} cannot carry a facility: a facility is installed gear, never a consumable effect.");
    }
}

// The installed facility (ruling demand-consumed-inputs-processed-by-facilities). It ticks once per zone step, from EquippedItem.Update
// while the item is active, and on screen only; an unloaded zone is frozen and the strategic tick (target r3 cut 8) calls the same
// Production.Advance for what is off screen. Its recipes are the run list of its design, fixed for a run because the catalog is.
public class Facility : Behavior
{
    private readonly IReadOnlyList<RecipeData> _runList;
    private EntityHold _hold;

    // Why the facility advanced no further this step: derived by Advance every step, never stored on the unit.
    public Stall Stall { get; private set; }

    public Facility(FacilityData data, EquippedItem item) : base(data, item)
    {
        _runList = Production.RunList(ItemManager.ItemData, Item.EquippableItem.Data.Key);
    }

    public override bool Execute(float dt)
    {
        _hold ??= new EntityHold(Entity);
        var (run, stall) = Production.Advance(ItemManager.ItemData, _runList, Item.EquippableItem.Run, dt, _hold);
        Item.EquippableItem.Run = run;
        Stall = stall;
        return true;
    }
}

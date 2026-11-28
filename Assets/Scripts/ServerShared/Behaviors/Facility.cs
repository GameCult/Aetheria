using MessagePack;
using Newtonsoft.Json;

// The facility gear a station carries to run recipes: a GearData of HardpointType Facility whose Behaviors hold one of these. The type
// says where it mounts (a hull's Facility rows), this says it runs recipes. It holds no field of its own: which recipes a facility
// runs is derived from RecipeData.Facility, so no list is stored here.
[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class FacilityData : BehaviorData
{
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Facility(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Facility(this, item);
    }
}

// The installed facility that will run its recipes (ruling demand-consumed-inputs-processed-by-facilities). It runs nothing until
// follow-up facility-production: LoadoutGenerator.EquipFacilities seats it on a station's Facility row, and it is priced and carried,
// and that is all.
public class Facility : Behavior
{
    public Facility(FacilityData data, EquippedItem item) : base(data, item) { }
    public Facility(FacilityData data, ConsumableItemEffect item) : base(data, item) { }
}

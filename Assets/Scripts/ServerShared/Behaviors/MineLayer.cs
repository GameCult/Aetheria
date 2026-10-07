using MessagePack;
using Newtonsoft.Json;

// A weapon that lays a mine instead of firing a round: InstantWeapon pays for it (cooldown, ammo, energy, heat,
// wear, visibility) and FireControl.Fire routes it to Lay. The mine's own timings live here until gear supplies
// them (ruling missile-stats-now-gear-later). The blast radius is the item's BlastRadius, read through
// FireControl.FuseOf like every other blast.
[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class MineLayerData : InstantWeaponData
{
    // Seconds after laying before the mine can be triggered.
    [Inspectable, JsonProperty("armingDelay"), Key(21)]
    public float ArmingDelay = 2f;

    // Seconds from the trigger to the blast.
    [Inspectable, JsonProperty("fuseDelay"), Key(22)]
    public float FuseDelay = 2f;

    // Seconds after laying at which an untriggered mine detonates.
    [Inspectable, JsonProperty("lifetime"), Key(23)]
    public float Lifetime = 30f;

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new InstantWeapon(this, item);
    }
}

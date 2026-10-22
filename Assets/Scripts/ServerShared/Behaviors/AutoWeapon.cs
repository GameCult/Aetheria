using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using CultMath;
using static CultMath.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class AutoWeaponData : InstantWeaponData
{
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new AutoWeapon(this, item);
    }
}

public class AutoWeapon : InstantWeapon
{
    
    public AutoWeapon(InstantWeaponData data, EquippedItem item) : base(data, item) { }
    public AutoWeapon(InstantWeaponData data, ConsumableItemEffect item) : base(data, item) { }

    // The trigger is held while it is down: InstantWeapon starts the next burst the moment the cooldown ends.
    protected override bool Repeats => _firing;
}


using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;

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

    public override bool Execute(float dt)
    {
        if(_firing && _burstRemaining == 0 && _cooldown < 0)
            Trigger();
        return base.Execute(dt);
    }
}


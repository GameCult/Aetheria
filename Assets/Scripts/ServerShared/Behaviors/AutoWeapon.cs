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

// A fully automatic gun: it fires every round its cooldown allows while its trigger is held, from the press (Activate)
// to the let-go (Deactivate). A plain InstantWeapon is semi-automatic.
public class AutoWeapon : InstantWeapon
{
    protected override bool Repeats => _firing;

    public AutoWeapon(InstantWeaponData data, EquippedItem item) : base(data, item) { }
    public AutoWeapon(InstantWeaponData data, ConsumableItemEffect item) : base(data, item) { }
}


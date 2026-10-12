/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using MessagePack;
using Newtonsoft.Json;
using CultMath;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class LauncherData : LockWeaponData
{
    [InspectableAnimationCurve, JsonProperty("guidance"), Key(26)]  
    public float4[] GuidanceCurve;

    [InspectableAnimationCurve, JsonProperty("thrustCurve"), Key(27)]  
    public float4[] ThrustCurve;

    [InspectableAnimationCurve, JsonProperty("liftCurve"), Key(28)]  
    public float4[] LiftCurve;

    // Keys 29-31 were Thrust, DodgeFrequency and MissileVelocity, retired for the munition parts (MunitionParts.cs); do not reuse.

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new LockWeapon(this, item);
    }
}

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class GuidedWeaponData : InstantWeaponData
{
    [InspectableAnimationCurve, JsonProperty("guidance"), Key(21)]  
    public float4[] GuidanceCurve;

    [InspectableAnimationCurve, JsonProperty("thrustCurve"), Key(22)]  
    public float4[] ThrustCurve;

    [InspectableAnimationCurve, JsonProperty("liftCurve"), Key(23)]  
    public float4[] LiftCurve;

    // Keys 24-26 were Thrust, DodgeFrequency and MissileVelocity, retired for the munition parts (MunitionParts.cs); do not reuse.

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new InstantWeapon(this, item);
    }
}
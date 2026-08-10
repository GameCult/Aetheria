/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn)]
public class LauncherData : LockWeaponData
{
    [InspectableAnimationCurve, JsonProperty("guidance"), Key(26)]  
    public float4[] GuidanceCurve;

    [InspectableAnimationCurve, JsonProperty("thrustCurve"), Key(27)]  
    public float4[] ThrustCurve;

    [InspectableAnimationCurve, JsonProperty("liftCurve"), Key(28)]  
    public float4[] LiftCurve;

    [Inspectable, JsonProperty("thrust"), Key(29)]  
    public PerformanceStat Thrust = new PerformanceStat();

    [Inspectable, JsonProperty("frequency"), Key(30)]
    public float DodgeFrequency;

    [Inspectable, JsonProperty("missileSpeed"), Key(31), RuntimeInspectable]
    public PerformanceStat MissileVelocity = new PerformanceStat();

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

    [Inspectable, JsonProperty("thrust"), Key(24)]  
    public PerformanceStat Thrust = new PerformanceStat();

    [Inspectable, JsonProperty("frequency"), Key(25)]
    public float DodgeFrequency;

    [Inspectable, JsonProperty("missileSpeed"), Key(26), RuntimeInspectable]
    public PerformanceStat MissileVelocity = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new InstantWeapon(this, item);
    }
}
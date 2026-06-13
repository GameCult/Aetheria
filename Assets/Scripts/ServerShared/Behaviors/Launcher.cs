/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using MessagePack;
using Unity.Mathematics;

[Inspectable, MessagePackObject]
public class LauncherData : LockWeaponData
{
    [InspectableAnimationCurve, Key(26)]
    public float4[] GuidanceCurve;

    [InspectableAnimationCurve, Key(27)]
    public float4[] ThrustCurve;

    [InspectableAnimationCurve, Key(28)]
    public float4[] LiftCurve;

    [Inspectable, Key(29)]
    public PerformanceStat Thrust = new PerformanceStat();

    [Inspectable, Key(30)]
    public float DodgeFrequency;

    [Inspectable, Key(31), RuntimeInspectable]
    public PerformanceStat MissileVelocity = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new LockWeapon(this, item);
    }
}

[Inspectable, MessagePackObject]
public class GuidedWeaponData : InstantWeaponData
{
    [InspectableAnimationCurve, Key(21)]
    public float4[] GuidanceCurve;

    [InspectableAnimationCurve, Key(22)]
    public float4[] ThrustCurve;

    [InspectableAnimationCurve, Key(23)]
    public float4[] LiftCurve;

    [Inspectable, Key(24)]
    public PerformanceStat Thrust = new PerformanceStat();

    [Inspectable, Key(25)]
    public float DodgeFrequency;

    [Inspectable, Key(26), RuntimeInspectable]
    public PerformanceStat MissileVelocity = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new InstantWeapon(this, item);
    }
}
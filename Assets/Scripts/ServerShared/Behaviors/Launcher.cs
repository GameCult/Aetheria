/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Unity.Mathematics;

[Inspectable]
public class LauncherData : LockWeaponData
{
    [InspectableAnimationCurve, LegacyPayloadKey(26)]
    public float4[] GuidanceCurve;

    [InspectableAnimationCurve, LegacyPayloadKey(27)]
    public float4[] ThrustCurve;

    [InspectableAnimationCurve, LegacyPayloadKey(28)]
    public float4[] LiftCurve;

    [Inspectable, LegacyPayloadKey(29)]
    public PerformanceStat Thrust = new PerformanceStat();

    [Inspectable, LegacyPayloadKey(30)]
    public float DodgeFrequency;

    [Inspectable, LegacyPayloadKey(31), RuntimeInspectable]
    public PerformanceStat MissileVelocity = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new LockWeapon(this, item);
    }
}

[Inspectable]
public class GuidedWeaponData : InstantWeaponData
{
    [InspectableAnimationCurve, LegacyPayloadKey(21)]
    public float4[] GuidanceCurve;

    [InspectableAnimationCurve, LegacyPayloadKey(22)]
    public float4[] ThrustCurve;

    [InspectableAnimationCurve, LegacyPayloadKey(23)]
    public float4[] LiftCurve;

    [Inspectable, LegacyPayloadKey(24)]
    public PerformanceStat Thrust = new PerformanceStat();

    [Inspectable, LegacyPayloadKey(25)]
    public float DodgeFrequency;

    [Inspectable, LegacyPayloadKey(26), RuntimeInspectable]
    public PerformanceStat MissileVelocity = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new InstantWeapon(this, item);
    }
}
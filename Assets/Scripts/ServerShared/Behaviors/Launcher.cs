/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Unity.Mathematics;

[Inspectable]
public class LauncherConfig : LockWeaponConfig
{
    [InspectableAnimationCurve]
    public float4[] GuidanceCurve;

    [InspectableAnimationCurve]
    public float4[] ThrustCurve;

    [InspectableAnimationCurve]
    public float4[] LiftCurve;

    [Inspectable]
    public PerformanceStat Thrust = new PerformanceStat();

    [Inspectable]
    public float DodgeFrequency;

    [Inspectable]
    public PerformanceStat MissileVelocity = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new LockWeapon(this, item);
    }
}

[Inspectable]
public class GuidedWeaponConfig : InstantWeaponConfig
{
    [InspectableAnimationCurve]
    public float4[] GuidanceCurve;

    [InspectableAnimationCurve]
    public float4[] ThrustCurve;

    [InspectableAnimationCurve]
    public float4[] LiftCurve;

    [Inspectable]
    public PerformanceStat Thrust = new PerformanceStat();

    [Inspectable]
    public float DodgeFrequency;

    [Inspectable]
    public PerformanceStat MissileVelocity = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new InstantWeapon(this, item);
    }
}
/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Unity.Mathematics;

[Inspectable]
public class LauncherData : LockWeaponData
{
    [InspectableAnimationCurve, RuntimeCatalogKey(26)]
    public float4[] GuidanceCurve;

    [InspectableAnimationCurve, RuntimeCatalogKey(27)]
    public float4[] ThrustCurve;

    [InspectableAnimationCurve, RuntimeCatalogKey(28)]
    public float4[] LiftCurve;

    [Inspectable, RuntimeCatalogKey(29)]
    public PerformanceStat Thrust = new PerformanceStat();

    [Inspectable, RuntimeCatalogKey(30)]
    public float DodgeFrequency;

    [Inspectable, RuntimeCatalogKey(31), RuntimeInspectable]
    public PerformanceStat MissileVelocity = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new LockWeapon(this, item);
    }
}

[Inspectable]
public class GuidedWeaponData : InstantWeaponData
{
    [InspectableAnimationCurve, RuntimeCatalogKey(21)]
    public float4[] GuidanceCurve;

    [InspectableAnimationCurve, RuntimeCatalogKey(22)]
    public float4[] ThrustCurve;

    [InspectableAnimationCurve, RuntimeCatalogKey(23)]
    public float4[] LiftCurve;

    [Inspectable, RuntimeCatalogKey(24)]
    public PerformanceStat Thrust = new PerformanceStat();

    [Inspectable, RuntimeCatalogKey(25)]
    public float DodgeFrequency;

    [Inspectable, RuntimeCatalogKey(26), RuntimeInspectable]
    public PerformanceStat MissileVelocity = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new InstantWeapon(this, item);
    }
}
/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable]
public class RadiatorData : BehaviorData
{
    [Inspectable, LegacyPayloadKey(1)]
    public PerformanceStat Emissivity = new PerformanceStat();

    [Inspectable, LegacyPayloadKey(2)]
    public PerformanceStat PumpedHeat = new PerformanceStat();

    [InspectableTemperature, LegacyPayloadKey(3)]
    public float TemperatureFloor;

    [Inspectable, LegacyPayloadKey(4)]
    public PerformanceStat WasteHeat = new PerformanceStat();

    [Inspectable, LegacyPayloadKey(5)]
    public PerformanceStat EnergyUsage = new PerformanceStat();

    [Inspectable, LegacyPayloadKey(6)]
    public PerformanceStat ThermalMass = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Radiator(this, item);
    }

    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Radiator(this, item);
    }
}

public class Radiator : Behavior, IAlwaysUpdatedBehavior, IInitializableBehavior
{
    public float RadiatorTemperature { get; private set; }

    public float Emissivity { get; private set; }
    public float PumpedHeat { get; private set; }
    public float WasteHeat { get; private set; }
    public float EnergyUsage { get; private set; }

    private RadiatorData _data;

    public Radiator(RadiatorData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    public Radiator(RadiatorData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        PumpedHeat = Evaluate(_data.PumpedHeat);
        WasteHeat = Evaluate(_data.WasteHeat);
        EnergyUsage = Evaluate(_data.EnergyUsage);

        var itemTemperature = Temperature;
        var tempRatio = max(RadiatorTemperature / itemTemperature, 1);

        // Temperature ratio would cause more waste heat than pump capacity, stop executing
        if (tempRatio > PumpedHeat / WasteHeat) return true;

        if (!Entity.TryConsumeEnergy(EnergyUsage * tempRatio * dt)) return false;

        var pumpedHeat = PumpedHeat * max(itemTemperature - _data.TemperatureFloor, 0);

        // Radiator temperature is below temperature floor, stop executing
        if (pumpedHeat < 0.01f) return true;

        var wasteHeat = WasteHeat * tempRatio;

        AddHeat((wasteHeat - pumpedHeat) * dt);
        RadiatorTemperature += pumpedHeat / Evaluate(_data.ThermalMass) * dt;

        return true;
    }

    public void Update(float delta)
    {
        Emissivity = Evaluate(_data.Emissivity);
        var rad = pow(RadiatorTemperature, ItemManager.GameplaySettings.HeatRadiationExponent) * ItemManager.GameplaySettings.HeatRadiationMultiplier * Emissivity;
        RadiatorTemperature -= rad * delta;
        Entity.VisibilitySources[this] = rad;
    }

    public void Initialize()
    {
        RadiatorTemperature = Temperature;
    }
}
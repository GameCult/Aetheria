/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable]
public class RadiatorConfig : RuntimeBehaviorConfig
{
    [Inspectable]
    public PerformanceStat Emissivity = new PerformanceStat();

    [Inspectable]
    public PerformanceStat PumpedHeat = new PerformanceStat();

    [InspectableTemperature]
    public float TemperatureFloor;

    [Inspectable]
    public PerformanceStat WasteHeat = new PerformanceStat();

    [Inspectable]
    public PerformanceStat EnergyUsage = new PerformanceStat();

    [Inspectable]
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

    private readonly PerformanceStat _emissivity;
    private readonly PerformanceStat _pumpedHeat;
    private readonly float _temperatureFloor;
    private readonly PerformanceStat _wasteHeat;
    private readonly PerformanceStat _energyUsage;
    private readonly PerformanceStat _thermalMass;

    public Radiator(RadiatorConfig data, EquippedItem item) : base(data, item)
    {
        _emissivity = data.Emissivity;
        _pumpedHeat = data.PumpedHeat;
        _temperatureFloor = data.TemperatureFloor;
        _wasteHeat = data.WasteHeat;
        _energyUsage = data.EnergyUsage;
        _thermalMass = data.ThermalMass;
    }
    public Radiator(RadiatorConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _emissivity = data.Emissivity;
        _pumpedHeat = data.PumpedHeat;
        _temperatureFloor = data.TemperatureFloor;
        _wasteHeat = data.WasteHeat;
        _energyUsage = data.EnergyUsage;
        _thermalMass = data.ThermalMass;
    }

    public override bool Execute(float dt)
    {
        PumpedHeat = Evaluate(_pumpedHeat);
        WasteHeat = Evaluate(_wasteHeat);
        EnergyUsage = Evaluate(_energyUsage);

        var itemTemperature = Temperature;
        var tempRatio = max(RadiatorTemperature / itemTemperature, 1);

        // Temperature ratio would cause more waste heat than pump capacity, stop executing
        if (tempRatio > PumpedHeat / WasteHeat) return true;

        if (!Entity.TryConsumeEnergy(EnergyUsage * tempRatio * dt)) return false;

        var pumpedHeat = PumpedHeat * max(itemTemperature - _temperatureFloor, 0);

        // Radiator temperature is below temperature floor, stop executing
        if (pumpedHeat < 0.01f) return true;

        var wasteHeat = WasteHeat * tempRatio;

        AddHeat((wasteHeat - pumpedHeat) * dt);
        RadiatorTemperature += pumpedHeat / Evaluate(_thermalMass) * dt;

        return true;
    }

    public void Update(float delta)
    {
        Emissivity = Evaluate(_emissivity);
        var rad = pow(RadiatorTemperature, ItemManager.GameplaySettings.HeatRadiationExponent) * ItemManager.GameplaySettings.HeatRadiationMultiplier * Emissivity;
        RadiatorTemperature -= rad * delta;
        Entity.VisibilitySources[this] = rad;
    }

    public void Initialize()
    {
        RadiatorTemperature = Temperature;
    }
}

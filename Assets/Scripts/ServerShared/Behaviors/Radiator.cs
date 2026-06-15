/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Unity.Mathematics;
using static Unity.Mathematics.math;

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

    public Radiator(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _emissivity = definition.PerformanceStat(1, new PerformanceStat());
        _pumpedHeat = definition.PerformanceStat(2, new PerformanceStat());
        _temperatureFloor = definition.Float(3);
        _wasteHeat = definition.PerformanceStat(4, new PerformanceStat());
        _energyUsage = definition.PerformanceStat(5, new PerformanceStat());
        _thermalMass = definition.PerformanceStat(6, new PerformanceStat());
        RegisterPerformanceStat(nameof(Emissivity), _emissivity);
        RegisterPerformanceStat(nameof(PumpedHeat), _pumpedHeat);
        RegisterPerformanceStat(nameof(WasteHeat), _wasteHeat);
        RegisterPerformanceStat(nameof(EnergyUsage), _energyUsage);
        RegisterPerformanceStat("ThermalMass", _thermalMass);
    }

    public Radiator(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _emissivity = definition.PerformanceStat(1, new PerformanceStat());
        _pumpedHeat = definition.PerformanceStat(2, new PerformanceStat());
        _temperatureFloor = definition.Float(3);
        _wasteHeat = definition.PerformanceStat(4, new PerformanceStat());
        _energyUsage = definition.PerformanceStat(5, new PerformanceStat());
        _thermalMass = definition.PerformanceStat(6, new PerformanceStat());
        RegisterPerformanceStat(nameof(Emissivity), _emissivity);
        RegisterPerformanceStat(nameof(PumpedHeat), _pumpedHeat);
        RegisterPerformanceStat(nameof(WasteHeat), _wasteHeat);
        RegisterPerformanceStat(nameof(EnergyUsage), _energyUsage);
        RegisterPerformanceStat("ThermalMass", _thermalMass);
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

/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Unity.Mathematics;
using static Unity.Mathematics.math;

public class Shield : Behavior, IProgressBehavior
{
    public float Efficiency { get; private set; }
    public float EnergyUsage { get; private set; }

    private readonly PerformanceStat _efficiency;
    private readonly PerformanceStat _energyUsage;

    public Shield(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _efficiency = definition.PerformanceStat(1, new PerformanceStat());
        _energyUsage = definition.PerformanceStat(2, new PerformanceStat());
        RegisterPerformanceStat(nameof(Efficiency), _efficiency);
        RegisterPerformanceStat(nameof(EnergyUsage), _energyUsage);
    }

    public Shield(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _efficiency = definition.PerformanceStat(1, new PerformanceStat());
        _energyUsage = definition.PerformanceStat(2, new PerformanceStat());
        RegisterPerformanceStat(nameof(Efficiency), _efficiency);
        RegisterPerformanceStat(nameof(EnergyUsage), _energyUsage);
    }

    public override bool Execute(float dt)
    {
        Efficiency = Evaluate(_efficiency);
        EnergyUsage = Evaluate(_energyUsage);
        return true;
    }

    public bool CanTakeHit(DamageType type, float damage)
    {
        return Entity.CanConsumeEnergy(damage * EnergyUsage);
    }

    public void TakeHit(DamageType type, float damage)
    {
        Entity.TryConsumeEnergy(damage * EnergyUsage);
        AddHeat(damage / Efficiency);
    }

    public virtual float Progress => Item.ThermalPerformance;
}

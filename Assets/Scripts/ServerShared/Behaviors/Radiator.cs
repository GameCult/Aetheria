/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using MessagePack;
using Newtonsoft.Json;
using CultMath;
using static CultMath.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class RadiatorData : BehaviorData
{
    [Inspectable, JsonProperty("emissivity"), Key(1), RuntimeInspectable]  
    public PerformanceStat Emissivity = new PerformanceStat();
    
    [Inspectable, JsonProperty("pumpedHeat"), Key(2), RuntimeInspectable]  
    public PerformanceStat PumpedHeat = new PerformanceStat();
    
    [InspectableTemperature, JsonProperty("temperatureFloor"), Key(3), RuntimeInspectable]  
    public float TemperatureFloor;
    
    [Inspectable, JsonProperty("wasteHeat"), Key(4), RuntimeInspectable]  
    public PerformanceStat WasteHeat = new PerformanceStat();
    
    [Inspectable, JsonProperty("energyUsage"), Key(5), RuntimeInspectable]  
    public PerformanceStat EnergyUsage = new PerformanceStat();
    
    [Inspectable, JsonProperty("thermalMass"), Key(6), RuntimeInspectable]  
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

public class Radiator : Behavior, IAlwaysUpdatedBehavior, IInitializableBehavior, IPowerConsumer
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

    // Cut 3 (docs/stats-and-power-cut.md): mirrors Execute's own early-out below -- a radiator that would not
    // even try to pump this tick (waste would outrun pump capacity) requests nothing, exactly like before.
    //
    // Nominal-request ruling (docs/stats-and-power-cut.md, operator ruling 2026-09-19): PumpedHeat, WasteHeat and
    // EnergyUsage are all registered request fields (StatValidation.PowerRequestFields) -- read nominally (what
    // full power would pump) so PumpedHeat carrying its own PowerSupply term (Cut 7's brownout curve) no longer
    // makes this tick's request depend on this tick's own grant. Execute below is unchanged: it calls the real,
    // curved Evaluate, so the actual pumping still degrades with whatever the bus actually grants.
    public float PowerRequest(float dt)
    {
        var pumpedHeat = EvaluateNominalPower(_data.PumpedHeat);
        var wasteHeat = EvaluateNominalPower(_data.WasteHeat);
        var tempRatio = max(RadiatorTemperature / Temperature, 1);
        if (tempRatio > pumpedHeat / wasteHeat) return 0f;
        return EvaluateNominalPower(_data.EnergyUsage) * tempRatio * dt;
    }

    // Cut 5 (docs/stats-and-power-cut.md §1.3, PowerTiers.cs): Critical -- the closest thing this game has to
    // life support. A starved radiator cascades into wear and shutdown across every other item on the hull.
    public int DefaultPowerTier => PowerTiers.Critical;

    public override bool Execute(float dt)
    {
        PumpedHeat = Evaluate(_data.PumpedHeat);
        WasteHeat = Evaluate(_data.WasteHeat);
        EnergyUsage = Evaluate(_data.EnergyUsage);

        var itemTemperature = Temperature;
        var tempRatio = max(RadiatorTemperature / itemTemperature, 1);

        // Temperature ratio would cause more waste heat than pump capacity, stop executing
        if (tempRatio > PumpedHeat / WasteHeat) return true;

        // Cut 7 (docs/stats-and-power-target.md): no separate power gate at all. PumpedHeat above is already a
        // plain Evaluate() read, so a PumpedHeat stat carrying a PowerSupply term already pumps less under a
        // partial grant -- waste heat below is unaffected by the curve, so a starved radiator falls behind and
        // the ship heats up, which is the reduced-performance failure the ruling asks for instead of the pump
        // simply refusing to run. F1 (docs/stats-and-power-cut.md, operator ruling 2026-09-19): PowerSupply is a
        // multiplier PerformanceStat.Evaluate applies to the whole resolved value, not a term blended into the
        // Min/Max interpolation, so at true zero supply PumpedHeat resolves to exactly 0 regardless of Min -- so
        // "produces nothing" already falls out of Evaluate() without a special case here.
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
/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using MessagePack;
using Newtonsoft.Json;
using CultMath;
using static CultMath.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class ShieldData : BehaviorData
{
    [Inspectable, JsonProperty("efficiency"), Key(1), RuntimeInspectable]  
    public PerformanceStat Efficiency = new PerformanceStat();

    [Inspectable, JsonProperty("energy"), Key(2), RuntimeInspectable]  
    public PerformanceStat EnergyUsage = new PerformanceStat();
    
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Shield(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Shield(this, item);
    }
}

public class Shield : Behavior, IProgressBehavior, IPowerConsumer
{
    public float Efficiency { get; private set; }
    public float EnergyUsage { get; private set; }

    private ShieldData _data;

    // Cut 4 (docs/stats-and-power-cut.md, Cut 4): "a shield hit is not an activation the player chose, so its
    // 'instant' draw is really a continuous reserve. Name it as such rather than giving it an activation
    // buffer." Unlike InstantWeapon/Sensor above, TrySpend below is never called with exactly Capacity -- a hit
    // can partially draw the reserve, same as the old direct capacitor spend could. Shield has no Energy/
    // Cooldown pair to derive a size from (only EnergyUsage, a per-damage-point multiplier), so this is a
    // judgment call, not a formula the map states: the reserve's Capacity and Rate both default to EnergyUsage
    // itself, refilling in full every second (Cooldown's implicit identity value -- the same "absent means
    // identity" convention InputCapacitor and PerformanceStat itself already use). Flagged for operator review
    // against real catalog EnergyUsage magnitudes rather than presented as spec'd.
    private readonly InputCapacitor _reserve = new InputCapacitor();

    public Shield(ShieldData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    public Shield(ShieldData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    // Cut 4: capacity/rate, resolved fresh (Capacitor.ResolveCapacity's precedent -- PowerBus.Step calls
    // PowerRequest below before this behaviour's own Execute runs this tick).
    private void RefreshReserve()
    {
        var energyUsage = Evaluate(_data.EnergyUsage);
        _reserve.UpdateStats(energyUsage, cooldown: 1f);
    }

    // Cut 4: the request PowerBus needs before Execute runs.
    public float PowerRequest(float dt)
    {
        RefreshReserve();
        return _reserve.RequestedFill(dt);
    }

    public override bool Execute(float dt)
    {
        Efficiency = Evaluate(_data.Efficiency);
        EnergyUsage = Evaluate(_data.EnergyUsage);
        // Cut 4: this tick's grant, read back via Item.PowerSupply -- Item is non-null here because a null-Item
        // instance never reaches PowerBus.Step (see CanTakeHit/TakeHit) and so never accrues charge.
        if (Item != null)
            _reserve.AddCharge(_reserve.RequestedFill(dt) * Item.PowerSupply);
        return true;
    }

    // Cut 4 (docs/stats-and-power-cut.md, Cut 4): a hit taken, not a chosen activation, drawing from the
    // shield's own continuous reserve instead of the entity's shared bus capacitors -- closing Cut 3's named,
    // temporary exception. A consumable-hosted instance (Item == null) has no PowerBus entry, so nothing would
    // ever fill this reserve; bypass it rather than starving such an instance forever.
    public bool CanTakeHit(DamageType type, float damage)
    {
        return Item == null || _reserve.CanSpend(damage * EnergyUsage);
    }

    public void TakeHit(DamageType type, float damage)
    {
        if (Item != null) _reserve.TrySpend(damage * EnergyUsage);
        AddHeat(damage / Efficiency);
    }

    public virtual float Progress => Item.ThermalPerformance;
}
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

    // Operator ruling, 2026-09-19 (docs/stats-and-power-cut.md, shield reserve ruling block): supersedes Cut
    // 4's derived sizing (reserve = EnergyUsage, refilling every second). The reserve is now an authored energy
    // pool, sized independently of EnergyUsage (which stays the per-damage-point cost multiplier).
    [Inspectable, JsonProperty("capacity"), Key(3), RuntimeInspectable]
    public PerformanceStat Capacity = new PerformanceStat();

    // Seconds to go from empty to full while the shield is up (holding, not broken).
    [Inspectable, JsonProperty("refillDuration"), Key(4), RuntimeInspectable]
    public PerformanceStat RefillDuration = new PerformanceStat();

    // Seconds to go from empty to full while the shield is broken. This is the punish window: how long a hit
    // that overwhelms the reserve leaves the hull exposed.
    [Inspectable, JsonProperty("restoreDuration"), Key(5), RuntimeInspectable]
    public PerformanceStat RestoreDuration = new PerformanceStat();

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
    public float Capacity { get; private set; }
    public float RefillDuration { get; private set; }
    public float RestoreDuration { get; private set; }

    // Operator ruling, 2026-09-19 (docs/stats-and-power-cut.md, shield reserve ruling block): the state Cut 4's
    // reserve never had. A hit the reserve cannot fully cover breaks the shield instead of being silently
    // refused with its charge intact. Persists tick to tick (unlike Efficiency/EnergyUsage/Capacity above,
    // which are re-evaluated fresh every Execute): it is the shield's own history, not a derived stat.
    //
    // Operator's second ruling, same date, on the fork the first ruling left open: the breaking hit passes
    // through in full -- no partial absorption. The reserve does not spend the portion it could have covered;
    // TakeHit is never called for this hit at all (CanTakeHit's false branch below is the caller's sole signal
    // to route the whole hit to the hull instead -- see every caller under Assets/Scripts/Gameplay/Weapons).
    // The break is a distinct event the player feels, not a discount on an overkill hit.
    //
    // A second question the operator asked me to settle and state: what happens to whatever charge the reserve
    // held at the moment it broke. Chosen: emptied by the break, not left at its pre-hit charge. A reserve that
    // broke at 9/10 and kept that charge would refill to full in a heartbeat under RestoreDuration (which is
    // sized for a full recharge, not a top-up) -- the punish window would only ever bite on a shield broken
    // from near-empty. Zeroing it makes RestoreDuration mean the same thing every time a shield breaks,
    // regardless of how close to full it was when the hit that broke it landed.
    public bool Broken { get; private set; }

    private ShieldData _data;

    // Operator ruling, 2026-09-19: reserve capacity is now an authored number (Capacity), independent of
    // EnergyUsage (which stays the per-damage-point cost multiplier applied in CanTakeHit/TakeHit below). Rate
    // is derived the same way InputCapacitor already derives every other consumer's rate (Capacity/duration,
    // UpdateStats' own cooldown-to-rate formula) -- just fed RefillDuration while up and RestoreDuration while
    // Broken, so refilling from partial and restoring from broken are two different speeds through the one
    // mechanism every other IPowerConsumer already uses. A partial bus grant (Item.PowerSupply < 1) stretches
    // either duration proportionally for free: AddCharge below is scaled by PowerSupply same as every other
    // consumer, so a half grant simply takes twice as long to fill regardless of which duration is active.
    private readonly InputCapacitor _reserve = new InputCapacitor();

    public Shield(ShieldData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    public Shield(ShieldData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    // Resolved fresh (Capacitor.ResolveCapacity's precedent -- PowerBus.Step calls PowerRequest below before
    // this behaviour's own Execute runs this tick), reading Broken as it stood at the end of the previous tick.
    private void RefreshReserve()
    {
        var capacity = Evaluate(_data.Capacity);
        var duration = Broken ? Evaluate(_data.RestoreDuration) : Evaluate(_data.RefillDuration);
        _reserve.UpdateStats(capacity, cooldown: duration, capacityOverride: capacity);
    }

    // The request PowerBus needs before Execute runs.
    public float PowerRequest(float dt)
    {
        RefreshReserve();
        return _reserve.RequestedFill(dt);
    }

    // Cut 5 (docs/stats-and-power-cut.md §1.3, PowerTiers.cs): High -- hull survival the player is actively
    // trading power for, but not the life-support constant Radiator is, so it sits one tier below.
    public int DefaultPowerTier => PowerTiers.High;

    public override bool Execute(float dt)
    {
        Efficiency = Evaluate(_data.Efficiency);
        EnergyUsage = Evaluate(_data.EnergyUsage);
        Capacity = Evaluate(_data.Capacity);
        RefillDuration = Evaluate(_data.RefillDuration);
        RestoreDuration = Evaluate(_data.RestoreDuration);
        // This tick's grant, read back via Item.PowerSupply -- Item is non-null here because a null-Item
        // instance never reaches PowerBus.Step (see CanTakeHit/TakeHit) and so never accrues charge.
        if (Item != null)
        {
            _reserve.AddCharge(_reserve.RequestedFill(dt) * Item.PowerSupply);
            // Restored to full on the restore duration: back up.
            if (Broken && _reserve.Charge >= _reserve.Capacity - .01f) Broken = false;
        }
        return true;
    }

    // Cut 4 (docs/stats-and-power-cut.md, Cut 4): a hit taken, not a chosen activation, drawing from the
    // shield's own continuous reserve instead of the entity's shared bus capacitors -- closing Cut 3's named,
    // temporary exception. A consumable-hosted instance (Item == null) has no PowerBus entry, so nothing would
    // ever fill this reserve; bypass it rather than starving such an instance forever.
    //
    // Operator ruling, 2026-09-19: a broken shield absorbs nothing (Broken short-circuits to false). A hit the
    // reserve cannot fully cover breaks the shield as a side effect of this same check -- see the Broken field
    // comment above for why that mutation lives here rather than in TakeHit. Every caller queries CanTakeHit
    // exactly once per hit and only calls TakeHit when it returns true (Assets/Scripts/Gameplay/Weapons/*), so
    // this is not a repeated-query hazard in practice. Draining the reserve on break (rather than leaving its
    // pre-hit charge, see the Broken field comment) happens here too, in the same atomic decision.
    public bool CanTakeHit(DamageType type, float damage)
    {
        if (Item == null) return true;
        if (Broken) return false;
        if (_reserve.CanSpend(damage * EnergyUsage)) return true;
        Broken = true;
        _reserve.AddCharge(-_reserve.Charge); // emptied by the break, not left at its pre-hit charge
        return false;
    }

    public void TakeHit(DamageType type, float damage)
    {
        if (Item != null) _reserve.TrySpend(damage * EnergyUsage);
        AddHeat(damage / Efficiency);
    }

    public virtual float Progress => Item.ThermalPerformance;
}
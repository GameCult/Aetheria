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

public class Shield : Behavior, IProgressBehavior, IPowerConsumer, IAlwaysUpdatedBehavior
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
    // TakeHit is never called for this hit at all -- CanTakeHit's false return is the caller's signal to route
    // the whole hit to the hull instead, and Break() below (F5, Soul pass 2026-09-19) is what the caller invokes
    // at that same point to actually break the shield, since CanTakeHit itself is now a pure query (see every
    // caller under Assets/Scripts/Gameplay/Weapons). The break is a distinct event the player feels, not a
    // discount on an overkill hit.
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
        return true;
    }

    // F4 (docs/stats-and-power-cut.md, Soul pass 2026-09-19): the reserve's own recharge, and clearing Broken
    // once it is full, used to live in Execute above -- which Entity.Update only calls while Item.Active is
    // true. A hit that breaks the shield usually also cooks it (AddHeat in TakeHit below), which can knock the
    // item thermally offline in the same moment it breaks -- Execute then stops running for as long as the item
    // stays offline, so the shield could never come back no matter how long RestoreDuration allows for. Restoring
    // must not depend on whether the item is currently Active, so it moved to IAlwaysUpdatedBehavior.Update,
    // which Entity.Update calls every tick unconditionally (Cooldown.cs already relies on the same interface for
    // exactly this reason -- see Entity.cs's own alwaysUpdatedBehavior loop, outside the `if (Active.Value)`
    // block Execute lives in). PowerBus (F2) only bills and refreshes Item.PowerSupply while the item is Active,
    // so while offline this keeps recharging at the last grant it actually received rather than freezing dead or
    // fabricating a fresh one -- the punish window still runs down on its own clock instead of being held
    // hostage by an unrelated shutdown.
    public void Update(float dt)
    {
        // Item is non-null-checked the same way Execute's old version was: a null-Item (consumable-hosted)
        // instance never reaches PowerBus.Step and so never accrues charge -- see CanTakeHit's own comment.
        if (Item == null) return;
        RefreshReserve();
        _reserve.AddCharge(_reserve.RequestedFill(dt) * Item.PowerSupply);
        // Restored to full on the restore duration: back up.
        if (Broken && _reserve.Charge >= _reserve.Capacity - .01f) Broken = false;
    }

    // Cut 4 (docs/stats-and-power-cut.md, Cut 4): a hit taken, not a chosen activation, drawing from the
    // shield's own continuous reserve instead of the entity's shared bus capacitors -- closing Cut 3's named,
    // temporary exception. A consumable-hosted instance (Item == null) has no PowerBus entry, so nothing would
    // ever fill this reserve; bypass it rather than starving such an instance forever.
    //
    // F5 (docs/stats-and-power-cut.md, Soul pass 2026-09-19): this used to mutate -- breaking the shield and
    // draining the reserve -- as a side effect of the query itself, on the premise that "every caller queries
    // CanTakeHit exactly once per hit." That premise was false: RaycastAll returns both the shield collider and
    // the hull collider for one shot, and every caller under Assets/Scripts/Gameplay/Weapons queries this twice
    // per hit (once deciding whether the shield collider absorbs it, once deciding whether the hull collider is
    // exposed). A caller must be able to ask without consequence, so this is now a pure read: Broken and the
    // reserve's own CanSpend, nothing more. See Break() below for where the mutation moved.
    public bool CanTakeHit(DamageType type, float damage)
    {
        if (Item == null) return true;
        if (Broken) return false;
        return _reserve.CanSpend(damage * EnergyUsage);
    }

    // F5: the mutation CanTakeHit used to perform when it returned false, now a caller invokes explicitly at the
    // one point damage is actually applied -- specifically, the point a caller decides to route a hit past this
    // shield (to the hull) instead of absorbing it, which is also the only point that decision is made at all,
    // so this is naturally called at most once per hit despite the double CanTakeHit query above. Idempotent
    // (a no-op once Broken is already true) as a defense-in-depth against a caller structure that cannot
    // guarantee single-call discipline, not as licence to call it more than once on purpose.
    public void Break()
    {
        if (Item == null || Broken) return;
        Broken = true;
        _reserve.AddCharge(-_reserve.Charge); // emptied by the break, not left at its pre-hit charge
    }

    public void TakeHit(DamageType type, float damage)
    {
        if (Item != null) _reserve.TrySpend(damage * EnergyUsage);
        AddHeat(damage / Efficiency);
    }

    public virtual float Progress => Item.ThermalPerformance;
}
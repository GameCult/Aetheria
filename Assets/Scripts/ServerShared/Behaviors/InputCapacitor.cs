/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using static CultMath.math;

// Cut 4 (docs/stats-and-power-cut.md §1.2, §0b "An input capacitor"): the input-only buffer that lets an
// instant activation (a shot, a ping, a shield's damage reserve) draw from the PowerBus continuously instead
// of spending stored charge directly -- closing Cut 3's named, temporary exception
// (Entity.TrySpendCapacitorCharge/CanSpendCapacitorCharge, both deleted with their last caller this cut).
//
// Not a Behavior of its own: per §0b, an input capacitor is identified by the Behavior instance that owns it
// (InstantWeapon, Sensor, Shield), which holds one of these as a plain field. Two writers, disjoint signs,
// named: the bus fills it, through AddCharge, called only from the owning behaviour's own Execute after it
// reads back its granted fraction from Item.PowerSupply; the owning behaviour drains it, through TrySpend,
// called only when it actually activates. Nothing else may touch Charge.
public class InputCapacitor
{
    public float Capacity { get; private set; }
    public float Rate { get; private set; }
    public float Charge { get; private set; }

    // Cut 4 (§7 Q4, operator ruling): "derived from the item's Energy and Cooldown ... with an authored
    // override" so burst capacity and sustained rate stay separate levers. capacityOverride/rateOverride of 0
    // mean "not authored, use the derived default" -- the same identity-when-absent convention PerformanceStat
    // itself already uses (a term-less stat resolves to its identity factor). Cutting Charge down here (rather
    // than only clamping in AddCharge) is what lets a Capacity that shrinks under a stat change immediately
    // stop promising a shot it can no longer pay for.
    public void UpdateStats(float energy, float cooldown, float capacityOverride = 0f, float rateOverride = 0f)
    {
        Capacity = capacityOverride > 0f ? capacityOverride : max(0f, energy);
        Rate = rateOverride > 0f ? rateOverride : (cooldown > 0f ? Capacity / cooldown : Capacity);
        Charge = clamp(Charge, 0f, Capacity);
    }

    // What PowerBus should count as this consumer's demand this tick (via the owning behaviour's
    // IPowerConsumer.PowerRequest), and what that behaviour should scale by its own Item.PowerSupply and pass
    // to AddCharge once its own Execute runs. The two calls must agree on dt and must not have let Charge move
    // in between, because PowerBus.Step calls this before any behaviour's Execute runs this tick (§1.2:
    // "computable from state that is already current at that point").
    public float RequestedFill(float dt) => min(Rate * dt, max(0f, Capacity - Charge));

    public void AddCharge(float amount) => Charge = clamp(Charge + amount, 0f, Capacity);

    public bool CanSpend(float cost) => Charge >= cost;

    // Atomic and whole-or-nothing, mirroring Entity.TrySpendCapacitorCharge's old contract exactly (down to the
    // near-zero shortcut) but scoped to this one behaviour's own buffer instead of the entity's shared bus
    // capacitors: either the cost is covered in full, or nothing moves. This is the mechanism behind the
    // operator's ruling that no item ever receives a fraction of a shot -- callers that want that guarantee
    // spend exactly Capacity, which only ever succeeds at full charge.
    public bool TrySpend(float cost)
    {
        if (cost < .01f) return true;
        if (!CanSpend(cost)) return false;
        Charge -= cost;
        return true;
    }
}

/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using System.Linq;
using static CultMath.math;

// Cut 3 (docs/stats-and-power-cut.md §1.2): the sole owner of every power grant for one Entity, stepped once per
// tick from Entity.Update, before any equipped item's Behaviors execute. It replaces
// Entity.TryConsumeEnergy/CanConsumeEnergy: at HEAD every draw succeeded as long as one reactor was online, and
// any shortfall was silently taxed onto the reactor as heat instead of ever being refused (§0.5). The bus makes
// the ceiling real: demand beyond what this tick's generation and stored capacitor charge can cover goes unmet.
// There are no tiers yet (Cut 5 adds them), so a shortfall rations the whole pool by the same fraction for every
// consumer alike -- the death of the old "whoever equipped first drains the capacitors first" hidden priority
// (§0.5), not a replacement for it.
//
// Bus capacitors are charged and drained only here (§0b): Reactor no longer touches them, and neither does any
// IPowerConsumer. The four instant draws the cut map names (a burst, a shot, a ping, a hit taken) are
// IPowerConsumers too as of Cut 4: each spends from its own InputCapacitor (InstantWeapon, Sensor, Shield),
// never from these bus capacitors directly. Entity.TrySpendCapacitorCharge/CanSpendCapacitorCharge, Cut 3's
// named, temporary exception, died with their last caller.
public class PowerBus
{
    private readonly Entity _entity;

    public PowerBus(Entity entity) => _entity = entity;

    // Every figure below is this tick's, overwritten wholesale by the next Step -- nothing here is persisted
    // (§0.6); a fresh Entity starts with the defaults below and they read as "fully supplied, nothing drawn"
    // until the first Step runs.
    public float TotalGeneration { get; private set; }
    public float TotalDemand { get; private set; }

    // Energy actually delivered this tick: generation first, then whatever stored capacitor charge covers the
    // rest, capped at demand. Equal to TotalDemand whenever supply covers it; equal to (generation + available
    // charge) otherwise -- never more, which TryConsumeEnergy could never promise (a reactor always paid the
    // remainder as heat, so overdraw was invisible at this level).
    public float TotalGrant { get; private set; }

    // Fraction of every consumer's own request actually granted this tick, shared by every consumer alike (no
    // tiers yet). Also written onto every consuming EquippedItem.PowerSupply.
    public float GrantRatio { get; private set; } = 1f;

    // What Reactor.Execute reads to run its own heat/throttle arithmetic (Cut 3: "the arithmetic survives, moved
    // under the bus's numbers"). Positive: demand left unmet after generation AND stored charge, split evenly
    // across online reactors for overload heat. Negative: generation left over after demand and after topping up
    // every capacitor, split evenly for the throttling arithmetic. Reactor.Draw no longer feeds this -- it is now
    // a reported total, not a sink (§1.2).
    public float NetDraw { get; private set; }

    public void Step(float dt)
    {
        var reactors = new List<Reactor>();
        var capacitors = new List<Capacitor>();
        var consumers = new List<IPowerConsumer>();
        foreach (var item in _entity.Equipment)
        foreach (var behavior in item.Behaviors)
        {
            switch (behavior)
            {
                case Reactor reactor when reactor.Item.Online.Value:
                    reactors.Add(reactor);
                    break;
                case Capacitor capacitor:
                    capacitors.Add(capacitor);
                    break;
            }
            if (behavior is IPowerConsumer consumer)
                consumers.Add(consumer);
        }

        TotalGeneration = reactors.Sum(r => r.Generation(dt));
        TotalDemand = consumers.Sum(c => max(0f, c.PowerRequest(dt)));

        var availableCharge = capacitors.Sum(c => c.Charge);
        var preCapacitorNet = TotalDemand - TotalGeneration;

        float overload = 0f, unabsorbedSurplus = 0f;
        if (preCapacitorNet > 0f)
        {
            var chargeDrawn = min(preCapacitorNet, availableCharge);
            if (chargeDrawn > 0f) DrainEvenly(capacitors, chargeDrawn);
            overload = preCapacitorNet - chargeDrawn;
        }
        else if (preCapacitorNet < 0f)
        {
            unabsorbedSurplus = FillEvenly(capacitors, -preCapacitorNet);
        }

        NetDraw = overload - unabsorbedSurplus; // exactly one of the two is ever nonzero
        TotalGrant = TotalDemand - overload;
        GrantRatio = TotalDemand <= 1e-4f ? 1f : saturate(TotalGrant / TotalDemand);

        foreach (var item in _entity.Equipment)
            if (item.Behaviors.Any(b => b is IPowerConsumer))
                item.PowerSupply = GrantRatio;
    }

    // Mirrors Entity.TryConsumeEnergy's old do/while exactly (draw evenly across every capacitor that still has
    // charge, repeating as capacitors empty out), just relocated: bus capacitors are drained only by the bus now.
    private static void DrainEvenly(List<Capacitor> capacitors, float amount)
    {
        int chargedCount;
        do
        {
            chargedCount = capacitors.Count(c => c.Charge > .01f);
            if (chargedCount == 0) break;
            var share = amount / chargedCount;
            foreach (var cap in capacitors)
            {
                if (cap.Charge <= .01f) continue;
                var drawn = min(share, cap.Charge);
                cap.AddCharge(-drawn);
                amount -= drawn;
            }
        } while (chargedCount > 0 && amount > .01f);
    }

    // Fills every capacitor evenly up to its own capacity and returns whatever would not fit, for Reactor to
    // throttle away -- the same job Reactor.cs used to do to itself (surplus branch), moved here because bus
    // capacitors are charged only by the bus (§0b). Resolves Capacity itself rather than reading each
    // Capacitor's cached property: the bus steps before any behaviour's Execute runs this tick, so that property
    // still holds last tick's value.
    private static float FillEvenly(List<Capacitor> capacitors, float amount)
    {
        int nonFullCount;
        do
        {
            nonFullCount = capacitors.Count(c => c.Charge < c.ResolveCapacity() - .01f);
            if (nonFullCount == 0) break;
            var share = amount / nonFullCount;
            foreach (var cap in capacitors)
            {
                var capacity = cap.ResolveCapacity();
                if (cap.Charge >= capacity - .01f) continue;
                var added = min(share, capacity - cap.Charge);
                cap.AddCharge(added);
                amount -= added;
            }
        } while (nonFullCount > 0 && amount > .01f);
        return amount;
    }
}

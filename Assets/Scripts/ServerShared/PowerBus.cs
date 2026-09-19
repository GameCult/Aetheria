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
//
// Cut 5 (docs/stats-and-power-cut.md §1.3) replaces the single shared fraction with priority tiers
// (PowerTiers.cs): the total this tick's supply can cover (TotalGrant, unchanged by tiers -- see below) is
// walked tier by tier, lowest number first. A tier is fed in full before the next tier sees anything left; a
// tier with no demand simply passes its whole share down; within a tier, every consumer's own request is
// rationed by the same fraction, so two requests of unequal size still divide proportionally. This is the death
// of the old "whoever equipped first drains the capacitors first" hidden priority (§0.5), replaced with an
// authored one instead of no priority at all.
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

    // Energy actually delivered this tick, summed across every tier: generation first, then whatever stored
    // capacitor charge covers the rest, capped at demand. Equal to TotalDemand whenever supply covers it; equal
    // to (generation + available charge) otherwise -- never more, which TryConsumeEnergy could never promise (a
    // reactor always paid the remainder as heat, so overdraw was invisible at this level). Which consumers that
    // total actually reaches is what tiering (below) decides; this figure is unaffected by tiers -- the ceiling
    // is a property of supply, not of priority.
    public float TotalGrant { get; private set; }

    // Aggregate fraction of all demand granted this tick (TotalGrant / TotalDemand) -- a fleet-wide summary, not
    // what any one consumer necessarily received. Still written onto every consuming EquippedItem.PowerSupply
    // when every tier in play happens to land on the same ratio (single-tier loadouts, the common case in the
    // Cut 3/4 test fixtures), but a multi-tier shortfall gives different tiers different ratios -- read
    // TierGrantRatio or the item's own PowerSupply for that.
    public float GrantRatio { get; private set; } = 1f;

    // Cut 5: this tick's within-tier grant ratio, indexed by PowerTiers.Critical..Utility. 1f means that tier's
    // demand was fully met (including a tier with no demand at all, per the array's own default-init); anything
    // below 1f is exactly the "starved" state a future UI (Cut 8) reads instead of reconstructing it from
    // per-item PowerSupply. A tier below a starved one is always 0f -- §1.3's "a tier boundary does not leak".
    public float[] TierGrantRatio { get; } = InitFullTiers();

    private static float[] InitFullTiers()
    {
        var ratios = new float[PowerTiers.Count];
        for (var i = 0; i < ratios.Length; i++) ratios[i] = 1f;
        return ratios;
    }

    // What Reactor.Execute reads to run its own heat/throttle arithmetic (Cut 3: "the arithmetic survives, moved
    // under the bus's numbers"). Positive: demand left unmet after generation AND stored charge, split evenly
    // across online reactors for overload heat. Negative: generation left over after demand and after topping up
    // every capacitor, split evenly for the throttling arithmetic. Reactor.Draw no longer feeds this -- it is now
    // a reported total, not a sink (§1.2).
    public float NetDraw { get; private set; }

    // One equipped item's power request for this tick, captured once so PowerRequest -- which some behaviours
    // (Shield, InstantWeapon, Sensor) implement by refreshing their own cached stats -- is never called twice in
    // one Step.
    private readonly struct Draw
    {
        public readonly EquippedItem Item;
        public readonly int Tier;
        public readonly float Request;
        public Draw(EquippedItem item, int tier, float request) { Item = item; Tier = tier; Request = request; }
    }

    public void Step(float dt)
    {
        var reactors = new List<Reactor>();
        var capacitors = new List<Capacitor>();
        var draws = new List<Draw>();
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
            {
                // Cut 5 (docs/stats-and-power-cut.md §1.3): the item's own stored choice wins once it has one;
                // PowerTiers.Unassigned only survives past EquippedItem's constructor for a consumer added to
                // the game after this unit was minted, which the constructor never saw -- fall back to the
                // behaviour's own default rather than stranding it at an invalid tier.
                var tier = item.EquippableItem.PowerTier;
                if (tier == PowerTiers.Unassigned) tier = consumer.DefaultPowerTier;
                tier = clamp(tier, 0, PowerTiers.Count - 1);
                draws.Add(new Draw(item, tier, max(0f, consumer.PowerRequest(dt))));
            }
        }

        TotalGeneration = reactors.Sum(r => r.Generation(dt));
        TotalDemand = draws.Sum(d => d.Request);

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

        AllocateTiers(draws);
    }

    // Cut 5 (docs/stats-and-power-cut.md §1.3): walks tiers lowest-number-first, feeding each in full before the
    // next sees anything. remaining only ever shrinks, and once it hits 0 every later tier's ratio is 0 by the
    // same division -- a lower tier can never take from a higher one (§1.3 "a tier boundary does not leak"),
    // because a higher tier's share is committed (remaining -= grant) before a lower tier is even considered.
    // Within a tier every consumer shares that tier's one ratio against its own request, so two unequal requests
    // still divide proportionally -- the same property Cut 3 proved for the whole ship, now proved per tier.
    private void AllocateTiers(List<Draw> draws)
    {
        var tierDemand = new float[PowerTiers.Count];
        foreach (var draw in draws) tierDemand[draw.Tier] += draw.Request;

        var remaining = TotalGrant;
        var ratios = TierGrantRatio;
        for (var tier = 0; tier < PowerTiers.Count; tier++)
        {
            var demand = tierDemand[tier];
            if (demand <= 1e-4f) { ratios[tier] = 1f; continue; }
            var grant = min(demand, remaining);
            ratios[tier] = saturate(grant / demand);
            remaining -= grant;
        }

        foreach (var draw in draws)
        {
            draw.Item.PowerSupply = ratios[draw.Tier];
            // Cut 6 (docs/stats-and-power-cut.md): the missing half of the wiring -- a stat with a PowerSupply
            // term must recompute when the grant actually moves. Called unconditionally, once per draw per tick,
            // the same shape as EquippedItem.UpdatePerformance's own Heat/Durability invalidation: "recomputes at
            // most once per tick per (item, stat)," not "only when the value moved." An item with no PowerSupply
            // term pays nothing extra -- the resolver's per-source generation bookkeeping (StatResolver.Resolve)
            // only ever looks at sources a stat's own Terms declared.
            _entity.Resolver.InvalidateSource(draw.Item, StatSource.PowerSupply);
        }
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

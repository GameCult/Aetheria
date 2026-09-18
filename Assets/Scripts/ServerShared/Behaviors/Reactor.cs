/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Linq;
using MessagePack;
using Newtonsoft.Json;
using CultMath;
using static CultMath.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class ReactorData : BehaviorData
{
    [Inspectable, JsonProperty("charge"), Key(1), RuntimeInspectable]  
    public PerformanceStat Charge = new PerformanceStat();

    [Inspectable, JsonProperty("efficiency"), Key(2), RuntimeInspectable]  
    public PerformanceStat Efficiency = new PerformanceStat();

    [Inspectable, JsonProperty("overload"), Key(3), RuntimeInspectable]  
    public PerformanceStat OverloadEfficiency = new PerformanceStat();

    [Inspectable, JsonProperty("underload"), Key(4), RuntimeInspectable]  
    public PerformanceStat ThrottlingFactor = new PerformanceStat();
    
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Reactor(this, item);
    }
    
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Reactor(this, item);
    }
}

public class Reactor : Behavior, IOrderedBehavior
{
    private ReactorData _data;

    // Cut 3 (docs/stats-and-power-cut.md §1.2): reported total, not a sink. Nothing writes into this from
    // outside any more -- PowerBus.NetDraw is the only input Execute reads to decide overload/throttle.
    public float Draw { get; private set; }

    public float CurrentLoadRatio { get; private set; }

    public int Order => 100;

    public Reactor(ReactorData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    public Reactor(ReactorData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    // Cut 3: the resolved generation PowerBus.Step needs before this behaviour's own Execute runs this tick.
    public float Generation(float dt) => Evaluate(_data.Charge) * dt;

    public override bool Execute(float dt)
    {
        var charge = Generation(dt);
        var efficiency = Evaluate(_data.Efficiency);
        var heat = charge / efficiency;

        // Cut 3: PowerBus already decided, for the whole entity, how much demand generation and stored charge
        // could not cover (positive) or how much generation was left over after demand and after topping up
        // every capacitor (negative). Split evenly across every online reactor, the same way
        // Entity.TryConsumeEnergy used to divide unmet demand among reactors before this behaviour ever saw it.
        var onlineReactors = Entity.GetBehaviors<Reactor>().Count(r => r.Item.Online.Value);
        var share = onlineReactors > 0 ? Entity.PowerBus.NetDraw / onlineReactors : 0f;

        if (share > .01f)
        {
            // Deficit: the bus already drained every capacitor it could; this is what is left. Overload power
            // always neutralizes it, at overload efficiency.
            CurrentLoadRatio = (share + charge) / max(charge, .01f);
            var overloadEfficiency = Evaluate(_data.OverloadEfficiency);
            heat += share / overloadEfficiency;
            Draw = share;
        }
        else if (share < -.01f)
        {
            // Surplus the bus could not absorb into any capacitor (all full): throttle to reduce heat generation.
            CurrentLoadRatio = (share + charge) / max(charge, .01f);
            heat -= share / efficiency * (1 - 1 / Evaluate(_data.ThrottlingFactor));
            Draw = 0;
        }
        else
        {
            CurrentLoadRatio = 1;
            Draw = 0;
        }

        Item.SetAudioParameter(SpecialAudioParameter.Intensity, max(.25f, 1 - 1 / CurrentLoadRatio));

        AddHeat(heat);
        return true;
    }
}
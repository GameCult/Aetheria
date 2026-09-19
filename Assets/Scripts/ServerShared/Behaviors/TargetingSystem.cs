/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using MessagePack;
using Newtonsoft.Json;

// Cut 2 (docs/fire-control-cut.md): the required interior Tool gear that makes aimed fire possible at all
// (R2, Q4). An entity with no working targeting system is not merely worse -- FireControl falls back to
// GameplaySettings.UnaidedAccuracy and a Precision of 0, which is authored to make unaided fire a last
// resort (Q4: "really, really bad"). This behaviour carries no power machinery of its own: EnergyDraw and
// Heat, authored alongside it on the same item, already cover that, and it adds nothing new.
[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class TargetingSystemData : BehaviorData
{
    // The ceiling on hit probability (Cut 3's roll multiplies against this; nothing here computes a
    // probability). A stat, not a constant, so lot quality, wear, heat and PowerSupply all reach it through
    // EquippedItem.Evaluate -- a starved targeting system rolls worse rather than switching off (the
    // brownout ruling, docs/stats-and-power-cut.md, applied here).
    [Inspectable, JsonProperty("accuracy"), Key(1), RuntimeInspectable]
    public PerformanceStat Accuracy = new PerformanceStat();

    // The info level (see Entity.EntityInfoGathered) at which sensor state stops limiting hits. Below the
    // system's own resolution, an otherwise-earned hit still degrades on account of thin sensor data;
    // consumed by Cut 3's roll. Higher is better, same as every other stat here: Cut 6c.1 (operator ruling
    // 2026-09-19) has FireControl.HitProbability take Resolution's reciprocal to derive the actual info
    // ceiling (ceiling = detection + (1 - detection) / Resolution), so authoring a bigger number buys a lower
    // ceiling -- less info needed -- rather than the field's own value being read as the ceiling directly.
    [Inspectable, JsonProperty("resolution"), Key(2), RuntimeInspectable]
    public PerformanceStat Resolution = new PerformanceStat();

    // Cut 6d (docs/fire-control-cut.md): grouping tightness, not a probability. FireControl.Sigma derives a
    // Gaussian kernel's sigma as 1/Precision, in hull-schematic cell units -- a higher number groups tighter
    // around whatever is aimed at (Entity.TargetItem's cells, or the hull's own centre of mass unaimed).
    // Consumed by Cut 3's roll (folded into HitProbability as pOnHull) and by Commit's cell draw, both through
    // the one kernel function -- never re-derived separately.
    [Inspectable, JsonProperty("precision"), Key(3), RuntimeInspectable]
    public PerformanceStat Precision = new PerformanceStat();

    // How much target-side deviation from the predicted intercept this system forgives before a shot goes
    // wide. Consumed by Cut 3's roll; unused until then.
    [Inspectable, JsonProperty("tracking"), Key(4), RuntimeInspectable]
    public PerformanceStat Tracking = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new TargetingSystem(this, item);
    }

    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new TargetingSystem(this, item);
    }
}

public class TargetingSystem : Behavior
{
    private readonly TargetingSystemData _data;

    public float Accuracy { get; private set; }
    public float Resolution { get; private set; }
    public float Precision { get; private set; }
    public float Tracking { get; private set; }

    public TargetingSystem(TargetingSystemData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public TargetingSystem(TargetingSystemData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        Accuracy = Evaluate(_data.Accuracy);
        Resolution = Evaluate(_data.Resolution);
        Precision = Evaluate(_data.Precision);
        Tracking = Evaluate(_data.Tracking);
        return true;
    }
}

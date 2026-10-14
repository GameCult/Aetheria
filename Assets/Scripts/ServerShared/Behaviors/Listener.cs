/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using MessagePack;
using Newtonsoft.Json;

// Passive listening gear (ruling sensor-stat-set): hears other ships' emissions at a multiple of their own range,
// bearing only. It emits nothing; Entity.HearingMultiple asks each active listener at hearing time. Its power is the
// design's EnergyDraw and its activity is the item's Active, as for sensors.
[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class ListenerData : BehaviorData
{
    // How many times an emitter's own reach this listener hears it at.
    [Inspectable, JsonProperty("multiple"), Key(1), RuntimeInspectable]
    public PerformanceStat Multiple = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Listener(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Listener(this, item);
    }
}

public class Listener : Behavior
{
    private readonly ListenerData _data;

    public Listener(ListenerData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    public Listener(ListenerData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public float Multiple => Evaluate(_data.Multiple);

    public override bool Execute(float dt) => true;
}

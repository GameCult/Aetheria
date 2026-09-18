/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class EnergyDrawData : BehaviorData
{
    [Inspectable, JsonProperty("draw"), Key(1), RuntimeInspectable]
    public PerformanceStat EnergyDraw = new PerformanceStat();
    
    [Inspectable, JsonProperty("perSecond"), Key(2)]
    public bool PerSecond;
    
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new EnergyDraw(this, item);
    }
    
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new EnergyDraw(this, item);
    }
}

public class EnergyDraw : Behavior, IPowerConsumer
{
    private EnergyDrawData _data;

    public EnergyDraw(EnergyDrawData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public EnergyDraw(EnergyDrawData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    // Cut 3 (docs/stats-and-power-cut.md): the request PowerBus needs before Execute runs, in place of the
    // direct Entity.TryConsumeEnergy spend that used to happen inside Execute.
    public float PowerRequest(float dt) => Evaluate(_data.EnergyDraw) * (_data.PerSecond ? dt : 1);

    public override bool Execute(float dt)
    {
        // A consumable-hosted instance (Item null) has no PowerBus entry (§1.2's grants are keyed by
        // EquippedItem); named rather than silently assumed away, it always succeeds here, the same way every
        // other context-dependent factor a ConsumableItemEffect answers with the identity elsewhere in this cut.
        return Item == null || Item.PowerSupply >= 1f;
    }
}
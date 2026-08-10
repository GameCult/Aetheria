/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), Order(-25)]
public class SwitchData : BehaviorData
{
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Switch(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Switch(this, item);
    }
}

public class Switch : Behavior, IActivatedBehavior
{
    private SwitchData _data;

    public bool Activated { get; set; }

    public Switch(SwitchData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    public Switch(SwitchData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        return Activated;
    }

    public void Activate()
    {
        Activated = true;
    }

    public void Deactivate()
    {
        Activated = false;
    }
}
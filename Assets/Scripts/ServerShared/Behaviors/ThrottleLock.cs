/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using MessagePack;
using Newtonsoft.Json;

// Presence marks the lock: Entity.ThrottleLocked is true while an active effect carries one, and the thrust
// allocator then bounds every forward-pushing column at full throttle from below in its solve. The behaviour
// holds no state and does nothing itself; turning stays with the pilot.
[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class ThrottleLockData : BehaviorData
{
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new ThrottleLock(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new ThrottleLock(this, item);
    }
}

public class ThrottleLock : Behavior
{
    public ThrottleLock(ThrottleLockData data, EquippedItem item) : base(data, item)
    {
    }

    public ThrottleLock(ThrottleLockData data, ConsumableItemEffect item) : base(data, item)
    {
    }

    public override bool Execute(float dt) => true;
}

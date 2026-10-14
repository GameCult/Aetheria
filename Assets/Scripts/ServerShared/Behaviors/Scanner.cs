/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using MessagePack;
using Newtonsoft.Json;
using static CultMath.math;

// Inspection (campaign aetheria-release, rulings quiet-gear-identifiable-hull-layout-known, scan-held-beam,
// scan-is-an-act): a gear that, held on the ship's tracked target within Range for ScanTime, reveals everything fitted to
// that target, quiet gear that never acts included. The scan is an emission like a lock: the target is painted and
// third parties hear it at Range over the intercept factor.
[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class ScannerData : BehaviorData
{
    // The planar distance to the target within which a scan builds.
    [Inspectable, JsonProperty("range"), Key(1), RuntimeInspectable]
    public PerformanceStat Range = new PerformanceStat();

    // Seconds a held scan takes to finish; a broken scan decays over the same span.
    [Inspectable, JsonProperty("scanTime"), Key(2), RuntimeInspectable]
    public PerformanceStat ScanTime = new PerformanceStat();

    // The ratio of how far this scanner sees to how far others hear it (ruling sensor-stat-set); a design without it
    // reads as 1.
    [Inspectable, JsonProperty("interceptFactor"), Key(3)]
    public PerformanceStat InterceptFactor;

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Scanner(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Scanner(this, item);
    }
}

public class Scanner : Behavior, IProgressBehavior
{
    private readonly ScannerData _data;
    private Entity _target;
    private float _built;
    private double _heldAt;

    // The scan's progress now: what Execute last built, less ScanTime-relative decay over the time since a tick last held
    // it (ruling scan-held-beam: a broken scan decays over ScanTime, the item inactive or destroyed included). Derived from
    // the zone clock, never from tick order, so a scanner decays on the tick it breaks whether its host runs Execute
    // before the always-updated pass (EquippedItem) or after it (ConsumableItemEffect). The one decay site.
    public float Progress => Remaining(_heldAt);

    public Scanner(ScannerData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    public Scanner(ScannerData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    private float Remaining(double since)
    {
        if (_built <= 0f) return 0f;
        var now = Entity.Zone?.ExactTime ?? since;
        return saturate(_built - (float) (now - since) / Evaluate(_data.ScanTime));
    }

    // Runs only while the item is Active. The scan builds while the ship's own Target is a tracked ship within Range, and
    // is painted and emitted on every tick it builds; at 1 the target's fitted items enter the book and the scan starts over.
    // A tick that does not hold the scan builds nothing, and the zone clock carries its decay (Progress).
    public override bool Execute(float dt)
    {
        var target = Entity.Target.Value.Entity;
        if (_target != target)
        {
            _built = 0f;
            _target = target;
        }

        var range = Evaluate(_data.Range);
        if (target != null && Entity.VisibleEntities.Contains(target) && Entity.TargetRange <= range)
        {
            // Decay over the ticks since the last held one, not counting this tick, which holds.
            var progress = saturate(Remaining(_heldAt + dt) + dt / Evaluate(_data.ScanTime));
            _built = progress;
            _heldAt = Entity.Zone?.ExactTime ?? _heldAt;
            target.ReceivePaint(Entity, Item, PaintMode.Scan, progress);
            Entity.Emit(Item, range / (_data.InterceptFactor == null ? 1f : Evaluate(_data.InterceptFactor)));
            if (progress >= 1f)
            {
                Entity.RecordInspection(target);
                _built = 0f;
            }
        }

        return true;
    }
}

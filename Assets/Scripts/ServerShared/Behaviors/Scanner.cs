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

public class Scanner : Behavior, IAlwaysUpdatedBehavior, IProgressBehavior
{
    private readonly ScannerData _data;
    private Entity _target;
    private bool _held;

    public float Progress { get; private set; }

    public Scanner(ScannerData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    public Scanner(ScannerData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    // Runs only while the item is Active. The scan builds while the ship's own Target is a tracked ship within Range, and
    // is painted and emitted on every tick it builds; at 1 the target's fitted items enter the book and the scan starts over.
    public override bool Execute(float dt)
    {
        var target = Entity.Target.Value.Entity;
        if (_target != target)
        {
            Progress = 0;
            _target = target;
        }

        var range = Evaluate(_data.Range);
        if (target != null && Entity.VisibleEntities.Contains(target) && Entity.TargetRange <= range)
        {
            Progress = saturate(Progress + dt / Evaluate(_data.ScanTime));
            target.ReceivePaint(Entity, Item, PaintMode.Scan, Progress);
            Entity.Emit(Item, range / (_data.InterceptFactor == null ? 1f : Evaluate(_data.InterceptFactor)));
            _held = true;
            if (Progress >= 1f)
            {
                Entity.RecordInspection(target);
                Progress = 0;
            }
        }

        return true;
    }

    // Every tick, after Execute: a scan not held this tick (out of range, untracked, or the item inactive so Execute did
    // not run) decays over ScanTime (ruling scan-held-beam). The one decay site.
    public void Update(float delta)
    {
        if (!_held) Progress = saturate(Progress - delta / Evaluate(_data.ScanTime));
        _held = false;
    }
}

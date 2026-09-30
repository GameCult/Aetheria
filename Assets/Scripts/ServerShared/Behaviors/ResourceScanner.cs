/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using MessagePack;
using Newtonsoft.Json;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class ResourceScannerData : BehaviorData
{
    [Inspectable, JsonProperty("range"), Key(1), RuntimeInspectable]
    public PerformanceStat Range = new PerformanceStat();
    
    [Inspectable, JsonProperty("minDensity"), Key(2), RuntimeInspectable]
    public PerformanceStat MinimumDensity = new PerformanceStat();
    
    [Inspectable, JsonProperty("scanDuration"), Key(3), RuntimeInspectable]
    public PerformanceStat ScanDuration = new PerformanceStat();
    
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new ResourceScanner(this, item);
    }
    
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new ResourceScanner(this, item);
    }
}

// Cut 2 (docs/mining-cut.md, Q1=A): the dead survey Execute and its scan-target fields are gone
// (ScanTarget/Asteroid/_scanTime/_scanTarget, and the always-false-until-scanned PlanetSurveyFloor write it
// worked toward). Mining Cut 3 (docs/mining-cut-refresh.md): chunks are detected by reflected light, not by a
// scanner, so the per-tick evaluation of three stats nothing read is gone too. The behavior is parked: its data
// stays authored, and it does nothing.
public class ResourceScanner : Behavior
{
    public ResourceScanner(ResourceScannerData data, EquippedItem item) : base(data, item)
    {
    }

    public ResourceScanner(ResourceScannerData data, ConsumableItemEffect item) : base(data, item)
    {
    }
}
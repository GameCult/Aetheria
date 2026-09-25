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
// worked toward). Range, MinimumDensity and ScanDuration stay authored data on this behavior until Q2 rules
// whether the scanner becomes a chunk's detection-range gate (docs/mining-cut.md Q2 consequence "Visibility").
public class ResourceScanner : Behavior, IAlwaysUpdatedBehavior
{
    private ResourceScannerData _data;

    public float Range { get; private set; }
    public float MinimumDensity { get; private set; }
    public float ScanDuration { get; private set; }

    public ResourceScanner(ResourceScannerData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public ResourceScanner(ResourceScannerData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public void Update(float delta)
    {
        Range = Evaluate(_data.Range);
        MinimumDensity = Evaluate(_data.MinimumDensity);
        ScanDuration = Evaluate(_data.ScanDuration);
    }
}
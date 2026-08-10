/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;

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

public class ResourceScanner : Behavior, IAlwaysUpdatedBehavior
{
    public int Asteroid = -1;
    
    private ResourceScannerData _data;
    private float _scanTime;
    private Guid _scanTarget;

    public float Range { get; private set; }
    public float MinimumDensity { get; private set; }
    public float ScanDuration { get; private set; }

    public Guid ScanTarget
    {
        get => _scanTarget;
        set
        {
            if (value != _scanTarget)
            {
                _scanTarget = value;
                _scanTime = 0;
            }
        }
    }

    public ResourceScanner(ResourceScannerData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public ResourceScanner(ResourceScannerData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        var planetData = Entity.Zone.Planets[ScanTarget];
        if (planetData != null)
        {
            if (planetData is AsteroidBeltData beltData)
            {
                if(Asteroid > -1 &&
                   Asteroid < beltData.Asteroids.Length &&
                   length(Entity.Position.xz - Entity.Zone.AsteroidBelts[ScanTarget].Transforms[Asteroid].xy) < Range)
                {
                    _scanTime += dt;
                    if (_scanTime > ScanDuration)
                    {
                        // TODO: Implement Scanning!
                        //Context.ItemData.Get<Corporation>(Entity.Corporation).PlanetSurveyFloor[ScanTarget] = MinimumDensity;
                        _scanTime = 0;
                    }
                    return true;
                }
            }
            else
            {
                if(length(Entity.Position.xz - Entity.Zone.GetOrbitPosition(planetData.Orbit)) < Range)
                {
                    _scanTime += dt;
                    if (_scanTime > ScanDuration)
                    {
                        //Context.ItemData.Get<Corporation>(Entity.Corporation).PlanetSurveyFloor[ScanTarget] = MinimumDensity;
                        _scanTime = 0;
                    }
                    return true;
                }
            }
        }
        return false;
    }

    public void Update(float delta)
    {
        Range = Evaluate(_data.Range);
        MinimumDensity = Evaluate(_data.MinimumDensity);
        ScanDuration = Evaluate(_data.ScanDuration);
    }
}
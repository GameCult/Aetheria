/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable]
public class ResourceScannerData : BehaviorData
{
    [Inspectable, LegacyPayloadKey(1)]
    public PerformanceStat Range = new PerformanceStat();

    [Inspectable, LegacyPayloadKey(2)]
    public PerformanceStat MinimumDensity = new PerformanceStat();

    [Inspectable, LegacyPayloadKey(3)]
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
    public float ScanTime => _scanTime;

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
        if (Entity.Zone.AsteroidBelts.TryGetValue(ScanTarget, out var belt))
        {
            if (Asteroid > -1 &&
               belt.ContainsAsteroid(Asteroid) &&
               length(Entity.Position.xz - belt.Transforms[Asteroid].xy) < Range)
            {
                _scanTime += dt;
                if (_scanTime > ScanDuration)
                {
                    // TODO: Implement Scanning!
                    _scanTime = 0;
                }
                return true;
            }
        }
        else if (Entity.Zone.PlanetInstances.TryGetValue(ScanTarget, out var planet))
        {
            if(length(Entity.Position.xz - Entity.Zone.GetOrbitPosition(planet.OrbitId)) < Range)
            {
                _scanTime += dt;
                if (_scanTime > ScanDuration)
                {
                    _scanTime = 0;
                }
                return true;
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

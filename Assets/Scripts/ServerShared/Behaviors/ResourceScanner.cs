/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using Unity.Mathematics;
using static Unity.Mathematics.math;

public class ResourceScanner : Behavior, IAlwaysUpdatedBehavior
{
    public int Asteroid = -1;

    private readonly PerformanceStat _range;
    private readonly PerformanceStat _minimumDensity;
    private readonly PerformanceStat _scanDuration;
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

    public ResourceScanner(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
        _range = definition.PerformanceStat(1, new PerformanceStat());
        _minimumDensity = definition.PerformanceStat(2, new PerformanceStat());
        _scanDuration = definition.PerformanceStat(3, new PerformanceStat());
        RegisterPerformanceStat(nameof(Range), _range);
        RegisterPerformanceStat(nameof(MinimumDensity), _minimumDensity);
        RegisterPerformanceStat(nameof(ScanDuration), _scanDuration);
    }

    public ResourceScanner(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
    {
        _range = definition.PerformanceStat(1, new PerformanceStat());
        _minimumDensity = definition.PerformanceStat(2, new PerformanceStat());
        _scanDuration = definition.PerformanceStat(3, new PerformanceStat());
        RegisterPerformanceStat(nameof(Range), _range);
        RegisterPerformanceStat(nameof(MinimumDensity), _minimumDensity);
        RegisterPerformanceStat(nameof(ScanDuration), _scanDuration);
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
        Range = Evaluate(_range);
        MinimumDensity = Evaluate(_minimumDensity);
        ScanDuration = Evaluate(_scanDuration);
    }
}

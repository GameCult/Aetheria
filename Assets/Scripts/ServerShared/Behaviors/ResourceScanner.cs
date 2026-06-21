/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using static CultMath.math;

public class ResourceScanner : Behavior, IAlwaysUpdatedBehavior
{
    public int Asteroid = -1;

    private readonly PerformanceStat _range;
    private readonly PerformanceStat _minimumDensity;
    private readonly PerformanceStat _scanDuration;
    private float _scanTime;
    private string _scanTargetBodyKey = "";

    public float Range { get; private set; }
    public float MinimumDensity { get; private set; }
    public float ScanDuration { get; private set; }
    public float ScanTime => _scanTime;

    public string ScanTargetBodyKey
    {
        get => _scanTargetBodyKey;
        set
        {
            value ??= "";
            if (!string.Equals(value, _scanTargetBodyKey, StringComparison.Ordinal))
            {
                _scanTargetBodyKey = value;
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
        if (Entity.Zone.TryGetAsteroidBelt(ScanTargetBodyKey, out var belt))
        {
            if (Entity.Zone.TryGetCultAsteroidTransform(ScanTargetBodyKey, Asteroid, out var asteroidPosition, out _) &&
               length(Entity.CultPositionXZ - asteroidPosition) < Range)
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
        else if (Entity.Zone.TryGetPlanet(ScanTargetBodyKey, out var planet))
        {
            if(length(Entity.CultPositionXZ - AetheriaMath.ToCult(Entity.Zone.GetOrbitPosition(planet.OrbitKey))) < Range)
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

    public void RestoreRuntimeState(
        string scanTargetBodyKey,
        int asteroid,
        float scanTime,
        float range,
        float minimumDensity,
        float scanDuration)
    {
        _scanTargetBodyKey = scanTargetBodyKey ?? "";
        Asteroid = asteroid;
        _scanTime = scanTime;
        Range = range;
        MinimumDensity = minimumDensity;
        ScanDuration = scanDuration;
    }
}

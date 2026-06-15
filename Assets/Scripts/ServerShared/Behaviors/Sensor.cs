/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable]
public class SensorConfig : RuntimeBehaviorConfig
{
    [Inspectable]
    public PerformanceStat Sensitivity = new PerformanceStat();

    [InspectableAnimationCurve]
    public BezierCurve SensitivityCurve;

    [Inspectable]
    public PerformanceStat PingBoost;

    [Inspectable]
    public PerformanceStat PingEnergy;

    [Inspectable]
    public PerformanceStat PingVisibility;

    [Inspectable]
    public PerformanceStat PingRange;

    [Inspectable]
    public PerformanceStat PingCooldown;

    [Inspectable]
    public float PingDuration = 2;

    [Inspectable]
    public float PingRadiusExponent = .5f;

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new Sensor(this, item);
    }

    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new Sensor(this, item);
    }
}

public class Sensor : Behavior, IEventBehavior
{
    private readonly PerformanceStat _sensitivity;
    private readonly BezierCurve _sensitivityCurve;
    private readonly PerformanceStat _pingBoost;
    private readonly PerformanceStat _pingEnergy;
    private readonly PerformanceStat _pingVisibility;
    private readonly PerformanceStat _pingRange;
    private readonly PerformanceStat _pingCooldownDuration;
    private readonly float _pingDuration;
    private readonly float _pingRadiusExponent;
    private float _pingCooldown;
    private float _pingLerp;
    private bool _pinging;
    private float _pingRadius;
    private HashSet<Entity> _pingedEntities = new HashSet<Entity>();

    public float Cooldown
    {
        get => saturate(_pingCooldown);
    }

    public float PingRadius
    {
        get => _pingRadius;
    }
    public float PingLerp => _pingLerp;
    public bool Pinging => _pinging;
    public int PingedEntityCount => _pingedEntities.Count;

    public float PingBrightness => pow(1 - _pingLerp, _pingRadiusExponent);

    public event Action OnPingStart;
    public event Action OnPingEnd;

    public void ResetEvents()
    {
        OnPingStart = null;
        OnPingEnd = null;
    }

    public void Ping()
    {
        if(_pingCooldown < 0 && Entity.TryConsumeEnergy(Evaluate(_pingEnergy)))
        {
            Entity.VisibilitySources[this] = Evaluate(_pingVisibility);
            _pinging = true;
            _pingCooldown = 1;
            _pingLerp = 0;
            _pingRadius = 0;
            _pingedEntities.Clear();
            OnPingStart?.Invoke();
        }
    }

    public Sensor(SensorConfig data, EquippedItem item) : base(data, item)
    {
        _sensitivity = data.Sensitivity;
        _sensitivityCurve = data.SensitivityCurve;
        _pingBoost = data.PingBoost;
        _pingEnergy = data.PingEnergy;
        _pingVisibility = data.PingVisibility;
        _pingRange = data.PingRange;
        _pingCooldownDuration = data.PingCooldown;
        _pingDuration = data.PingDuration;
        _pingRadiusExponent = data.PingRadiusExponent;
    }

    public Sensor(SensorConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _sensitivity = data.Sensitivity;
        _sensitivityCurve = data.SensitivityCurve;
        _pingBoost = data.PingBoost;
        _pingEnergy = data.PingEnergy;
        _pingVisibility = data.PingVisibility;
        _pingRange = data.PingRange;
        _pingCooldownDuration = data.PingCooldown;
        _pingDuration = data.PingDuration;
        _pingRadiusExponent = data.PingRadiusExponent;
    }

    public override bool Execute(float dt)
    {
        if (_pinging)
        {
            _pingLerp += dt / _pingDuration;
            _pingRadius = lerp(0, Evaluate(_pingRange), pow(_pingLerp, _pingRadiusExponent));
            if (_pingLerp > 1)
            {
                _pinging = false;
                OnPingEnd?.Invoke();
            }
        }

        _pingCooldown -= dt / Evaluate(_pingCooldownDuration);

        // TODO: Handle Active Detection / Visibility From Reflected Radiance
        var forward = Direction.xz;
        foreach (var entity in Entity.Zone.Entities)
        {
            if (entity == Entity) continue;

            var diff = entity.Position.xz - Entity.Position.xz;
            var angle = acos(dot(forward, normalize(diff)));
            var dist = length(diff);
            float previous, next;
            Entity.EntityInfoGathered.TryGetValue(entity, out previous);
            if (!_pingedEntities.Contains(entity) && dist < _pingRadius)
            {
                _pingedEntities.Add(entity);
                next = saturate(
                    previous +
                    entity.Visibility *
                    Evaluate(_sensitivity) *
                    Evaluate(_pingBoost) *
                    dist);
            }
            else
            {
                next = saturate(
                    previous +
                    entity.Visibility *
                    Evaluate(_sensitivity) *
                    _sensitivityCurve.Evaluate(angle / PI) *
                    dt / dist);
            }
            next *= 1 - ItemManager.GameplaySettings.TargetInfoDecay * dt;
            //Context.Log($"{entity.Name} visibility {(int)(previous * 100)}% -> {(int)(next * 100)}%");
            Entity.EntityInfoGathered[entity] = next;
        }
        return true;
    }
}

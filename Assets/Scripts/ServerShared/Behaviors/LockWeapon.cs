using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable]
public class LockWeaponConfig : InstantWeaponConfig
{
    [Inspectable]
    public PerformanceStat LockSpeed = new PerformanceStat();

    [Inspectable]
    public PerformanceStat SensorImpact = new PerformanceStat();

    [Inspectable]
    public PerformanceStat LockAngle = new PerformanceStat();

    [Inspectable]
    public PerformanceStat DirectionImpact = new PerformanceStat();

    [Inspectable]
    public PerformanceStat Decay = new PerformanceStat();

    public override Behavior CreateInstance(EquippedItem item)
    {
        return new LockWeapon(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new LockWeapon(this, item);
    }
}

public class LockWeapon : InstantWeapon
{
    private readonly PerformanceStat _lockSpeed;
    private readonly PerformanceStat _sensorImpact;
    private readonly PerformanceStat _lockAngle;
    private readonly PerformanceStat _directionImpact;
    private readonly PerformanceStat _decay;
    private float _lock;
    private bool _locking;
    private Entity _target;

    public event Action OnLocked;
    public event Action OnBeginLocking;
    public event Action OnLockLost;

    public float LockSpeed { get; private set; }
    public float SensorImpact { get; private set; }
    public float LockAngle { get; private set; }
    public float DirectionImpact { get; private set; }
    public float Decay { get; private set; }

    public override float Progress => saturate(_cooldown > 0 ? _cooldown : _lock);

    public override bool CanFire => base.CanFire && _lock > .99f && Entity.TargetRange > MinRange && Entity.TargetRange < Range;

    public float Lock
    {
        get => saturate(_lock);
    }

    public float LockProgress => saturate(_lock);

    public Entity LockTarget => _target;

    public LockWeapon(LockWeaponConfig data, EquippedItem item) : base(data, item)
    {
        _lockSpeed = data.LockSpeed;
        _sensorImpact = data.SensorImpact;
        _lockAngle = data.LockAngle;
        _directionImpact = data.DirectionImpact;
        _decay = data.Decay;
    }
    public LockWeapon(LockWeaponConfig data, ConsumableItemEffect item) : base(data, item)
    {
        _lockSpeed = data.LockSpeed;
        _sensorImpact = data.SensorImpact;
        _lockAngle = data.LockAngle;
        _directionImpact = data.DirectionImpact;
        _decay = data.Decay;
    }

    public override bool Execute(float dt)
    {
        if (_target != Entity.Target.Value)
        {
            _lock = 0;
            _target = Entity.Target.Value;
        }

        if (Entity.Target.Value != null && Entity.Target.Value.IsHostileTo(Entity))
        {
            LockSpeed = Evaluate(_lockSpeed);
            SensorImpact = Evaluate(_sensorImpact);
            LockAngle = Evaluate(_lockAngle);
            DirectionImpact = Evaluate(_directionImpact);
            Decay = Evaluate(_decay);

            var degrees = acos(dot(normalize(Entity.Target.Value.Position - Entity.Position), normalize(Entity.LookDirection))) * 57.2958f;
            if (degrees < LockAngle)
            {
                var lerp = 1 - unlerp(0, 90, degrees);
                _lock = saturate(_lock + pow(lerp, DirectionImpact) * dt * LockSpeed * pow(Entity.EntityInfoGathered[Entity.Target.Value], SensorImpact));
            }
            else _lock = saturate(_lock - dt * Decay);
        }

        return base.Execute(dt);
    }
}


using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class LockWeaponData : InstantWeaponData
{
    [Inspectable, JsonProperty("speed"), Key(21), RuntimeInspectable]
    public PerformanceStat LockSpeed = new PerformanceStat();

    [Inspectable, JsonProperty("sensorImpact"), Key(22)]
    public PerformanceStat SensorImpact = new PerformanceStat();

    [Inspectable, JsonProperty("threshold"), Key(23), RuntimeInspectable]
    public PerformanceStat LockAngle = new PerformanceStat();

    [Inspectable, JsonProperty("directionImpact"), Key(24)]
    public PerformanceStat DirectionImpact = new PerformanceStat();

    [Inspectable, JsonProperty("decay"), Key(25)]
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
    private LockWeaponData _data;
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
    
    public LockWeapon(LockWeaponData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }
    public LockWeapon(LockWeaponData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
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
            LockSpeed = Evaluate(_data.LockSpeed);
            SensorImpact = Evaluate(_data.SensorImpact);
            LockAngle = Evaluate(_data.LockAngle);
            DirectionImpact = Evaluate(_data.DirectionImpact);
            Decay = Evaluate(_data.Decay);

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


/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using CultMath;
using static CultMath.math;

[Inspectable, MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class SensorData : BehaviorData
{
    [Inspectable, JsonProperty("sensitivity"), Key(3), RuntimeInspectable]
    public PerformanceStat Sensitivity = new PerformanceStat();

    [InspectableAnimationCurve, JsonProperty("sensCurve"), Key(4), RuntimeInspectable]
    public BezierCurve SensitivityCurve;

    [Inspectable, JsonProperty("pingBoost"), Key(5), RuntimeInspectable]
    public PerformanceStat PingBoost;

    [Inspectable, JsonProperty("pingEnergy"), Key(6), RuntimeInspectable]
    public PerformanceStat PingEnergy;

    [Inspectable, JsonProperty("pingVisibility"), Key(7), RuntimeInspectable]
    public PerformanceStat PingVisibility;

    [Inspectable, JsonProperty("pingRange"), Key(8), RuntimeInspectable]
    public PerformanceStat PingRange;

    [Inspectable, JsonProperty("pingCooldown"), Key(9), RuntimeInspectable]
    public PerformanceStat PingCooldown;

    [Inspectable, JsonProperty("pingDuration"), Key(10)]
    public float PingDuration = 2;

    [Inspectable, JsonProperty("pingRadiusExponent"), Key(11)]
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

public class Sensor : Behavior, IEventBehavior, IPowerConsumer
{
    private SensorData _data;

    // Cut 4 (docs/stats-and-power-cut.md, Cut 4): replaces the direct Entity.TrySpendCapacitorCharge spend in
    // Ping() below -- the named, temporary exception Cut 3 left standing.
    private readonly InputCapacitor _capacitor = new InputCapacitor();
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

    public float PingBrightness => pow(1 - _pingLerp, _data.PingRadiusExponent);

    public event Action OnPingStart;
    public event Action OnPingEnd;

    public void ResetEvents()
    {
        OnPingStart = null;
        OnPingEnd = null;
    }

    // Cut 4: capacity/rate, resolved fresh (Capacitor.ResolveCapacity's precedent -- PowerBus.Step calls
    // PowerRequest below before this behaviour's own Execute runs this tick).
    //
    // Nominal-request ruling (docs/stats-and-power-cut.md, operator ruling 2026-09-19): PingEnergy and
    // PingCooldown are registered request fields (StatValidation.PowerRequestFields) -- read nominally, same
    // reasoning as InstantWeapon's own RefreshInputCapacitor.
    private void RefreshInputCapacitor()
    {
        var energy = EvaluateNominalPower(_data.PingEnergy);
        var cooldown = EvaluateNominalPower(_data.PingCooldown);
        _capacitor.UpdateStats(energy, cooldown);
    }

    // Cut 4: the request PowerBus needs before Execute runs.
    public float PowerRequest(float dt)
    {
        RefreshInputCapacitor();
        return _capacitor.RequestedFill(dt);
    }

    // Cut 5 (docs/stats-and-power-cut.md §1.3, PowerTiers.cs): Utility -- everything else.
    public int DefaultPowerTier => PowerTiers.Utility;

    public void Ping()
    {
        // Whole-or-nothing against the ping's own input capacitor -- fires only at full charge (operator
        // ruling: no fraction of a shot). A consumable-hosted instance (Item == null) has no PowerBus entry, so
        // nothing would ever fill this capacitor; bypass it rather than starving such an instance forever.
        if(_pingCooldown < 0 && (Item == null || _capacitor.TrySpend(_capacitor.Capacity)))
        {
            Entity.VisibilitySources[this] = Evaluate(_data.PingVisibility);
            _pinging = true;
            _pingCooldown = 1;
            _pingLerp = 0;
            _pingRadius = 0;
            _pingedEntities.Clear();
            OnPingStart?.Invoke();
        }
    }

    public Sensor(SensorData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public Sensor(SensorData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public override bool Execute(float dt)
    {
        // Cut 4: this tick's grant, read back via Item.PowerSupply -- Item is non-null here because a null-Item
        // instance never reaches PowerBus.Step (see Ping()) and so never accrues charge.
        if (Item != null)
            _capacitor.AddCharge(_capacitor.RequestedFill(dt) * Item.PowerSupply);
        if (_pinging)
        {
            _pingLerp += dt / _data.PingDuration;
            _pingRadius = lerp(0, Evaluate(_data.PingRange), pow(_pingLerp, _data.PingRadiusExponent));
            if (_pingLerp > 1)
            {
                _pinging = false;
                OnPingEnd?.Invoke();
            }
        }

        _pingCooldown -= dt / Evaluate(_data.PingCooldown);
        
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
                    Evaluate(_data.Sensitivity) *
                    Evaluate(_data.PingBoost) *
                    dist);
            }
            else
            {
                next = saturate(
                    previous +
                    entity.Visibility *
                    Evaluate(_data.Sensitivity) *
                    _data.SensitivityCurve.Evaluate(angle / PI) *
                    dt / dist);
            }
            next *= 1 - ItemManager.GameplaySettings.TargetInfoDecay * dt;
            //Context.Log($"{entity.Name} visibility {(int)(previous * 100)}% -> {(int)(next * 100)}%");
            Entity.EntityInfoGathered[entity] = next;
        }
        return true;
    }
}
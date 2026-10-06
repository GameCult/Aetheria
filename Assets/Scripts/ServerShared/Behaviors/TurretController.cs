/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;
using UniRx;
using CultMath;
using static CultMath.math;
using float3 = CultMath.float3;

[MessagePackObject, JsonObject(MemberSerialization.OptIn), RuntimeInspectable]
public class TurretControllerData : BehaviorData
{
    public override Behavior CreateInstance(EquippedItem item)
    {
        return new TurretController(this, item);
    }
    public override Behavior CreateInstance(ConsumableItemEffect item)
    {
        return new TurretController(this, item);
    }
}

public class TurretController : Behavior, IInitializableBehavior
{
    private TurretControllerData _data;
    private List<Weapon> _weapons = new List<Weapon>();
    private float _shotSpeed;
    private bool _predictShots;

    public TurretController(TurretControllerData data, EquippedItem item) : base(data, item)
    {
        _data = data;
    }

    public TurretController(TurretControllerData data, ConsumableItemEffect item) : base(data, item)
    {
        _data = data;
    }

    public void Initialize()
    {
        foreach (var weapon in Entity.GetBehaviors<Weapon>())
        {
            _weapons.Add(weapon);
            var vel = weapon.Evaluate(weapon.WeaponData.Velocity);
            if (vel > .1f)
            {
                _predictShots = true;
                _shotSpeed = vel;
            }
        }
    }

    public override bool Execute(float dt)
    {
        // Mining Cut 3: a turret engages entities only; it never picks a chunk, and reads one as no target.
        var target = Entity.Target.Value.Entity;
        if (target != null)
        {
            var diff = target.Position - Entity.Position;
            if (_predictShots)
            {
                var targetHullData = Entity.ItemManager.GetData(target.Hull) as HullData;
                var targetVelocity = float3(target.Velocity.x, 0, target.Velocity.y);
                var predictedPosition = first_order_intercept(
                    Entity.Position, float3.zero, _shotSpeed,
                    target.Position, targetVelocity
                );
                predictedPosition.y = Entity.Zone.GetHeight(predictedPosition.xz) + targetHullData.GridOffset;
                Entity.LookDirection = normalize(predictedPosition - Entity.Position);
            }
            else
                Entity.LookDirection = normalize(diff);

            foreach (var x in _weapons)
            {
                // The same per-weapon decision Combat.cs makes; the range test is part of it (a shot out of range
                // is not designated and prices at zero).
                if (FireControl.AgentFires(x, Entity, target))
                {
                    x.Activate();
                }
                else if (x.Firing)
                    x.Deactivate();
            }
        }
        else
        {
            foreach (var x in _weapons)
            {
                if (x.Firing)
                    x.Deactivate();
            }
            Entity.SetTarget(Entity.VisibleEnemies.FirstOrDefault(e => e is Ship));
        }
        return true;
    }
}
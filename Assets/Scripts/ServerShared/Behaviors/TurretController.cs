/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using System.Linq;
using UniRx;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using float3 = Unity.Mathematics.float3;
public class TurretControllerConfig : RuntimeBehaviorConfig
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
    private List<Weapon> _weapons = new List<Weapon>();
    private float _shotSpeed;
    private bool _predictShots;

    public int WeaponCount => _weapons.Count;
    public float ShotSpeed => _shotSpeed;
    public bool PredictShots => _predictShots;

    public TurretController(TurretControllerConfig data, EquippedItem item) : base(data, item)
    {
    }

    public TurretController(TurretControllerConfig data, ConsumableItemEffect item) : base(data, item)
    {
    }

    public void Initialize()
    {
        foreach (var weapon in Entity.GetBehaviors<Weapon>())
        {
            _weapons.Add(weapon);
            var vel = weapon.EvaluateVelocity();
            if (vel > .1f)
            {
                _predictShots = true;
                _shotSpeed = vel;
            }
        }
    }

    public override bool Execute(float dt)
    {
        if (Entity.Target.Value != null)
        {
            var diff = Entity.Target.Value.Position - Entity.Position;
            if (_predictShots)
            {
                var targetHull = Entity.ItemManager.GetRuntimeItem(Entity.Target.Value.Hull);
                var targetVelocity = float3(Entity.Target.Value.Velocity.x, 0, Entity.Target.Value.Velocity.y);
                var predictedPosition = AetheriaMath.FirstOrderIntercept(
                    Entity.Position, float3.zero, _shotSpeed,
                    Entity.Target.Value.Position, targetVelocity
                );
                predictedPosition.y = Entity.Zone.GetHeight(predictedPosition.xz) + (float)(targetHull?.HullGridOffset ?? 0);
                Entity.LookDirection = normalize(predictedPosition - Entity.Position);
            }
            else
                Entity.LookDirection = normalize(diff);
            var dist = length(diff);

            foreach (var x in _weapons)
            {
                var fire = dot(
                    x.Direction,
                    Entity.LookDirection) > .99f;
                if (x.EvaluateRange() > dist && fire)
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
            Entity.Target.Value = Entity.VisibleEnemies.FirstOrDefault(e => e is Ship);
        }
        return true;
    }
}

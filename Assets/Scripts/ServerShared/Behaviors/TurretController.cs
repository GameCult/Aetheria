/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using System.Linq;
using UniRx;
using static CultMath.math;
using cfloat3 = CultMath.float3;

public class TurretController : Behavior, IInitializableBehavior
{
    private List<Weapon> _weapons = new List<Weapon>();
    private float _shotSpeed;
    private bool _predictShots;

    public int WeaponCount => _weapons.Count;
    public float ShotSpeed => _shotSpeed;
    public bool PredictShots => _predictShots;

    public TurretController(RuntimeBehaviorDefinition definition, EquippedItem item) : base(definition, item)
    {
    }

    public TurretController(RuntimeBehaviorDefinition definition, ConsumableItemEffect item) : base(definition, item)
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
            var entityPosition = Entity.CultPosition;
            var targetPosition = Entity.Target.Value.CultPosition;
            var diff = targetPosition - entityPosition;
            if (_predictShots)
            {
                var targetHull = Entity.ItemManager.GetRuntimeItem(Entity.Target.Value.Hull);
                var targetVelocity = new cfloat3(Entity.Target.Value.CultVelocity.x, 0, Entity.Target.Value.CultVelocity.y);
                var predictedPosition = AetheriaMath.FirstOrderIntercept(
                    entityPosition, cfloat3.zero, _shotSpeed,
                    targetPosition, targetVelocity
                );
                predictedPosition.y = Entity.Zone.GetHeight(predictedPosition.xz) + (float)(targetHull?.HullGridOffset ?? 0);
                Entity.CultLookDirection = normalize(predictedPosition - entityPosition);
            }
            else
                Entity.CultLookDirection = normalize(diff);
            var dist = length(diff);

            foreach (var x in _weapons)
            {
                var fire = dot(
                    x.Direction,
                    Entity.CultLookDirection) > .99f;
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

    public void RestoreRuntimeState(float shotSpeed, bool predictShots)
    {
        _shotSpeed = shotSpeed;
        _predictShots = predictShots;
    }
}

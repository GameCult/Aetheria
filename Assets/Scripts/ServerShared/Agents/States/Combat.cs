using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using static CultMath.math;
using cfloat3 = CultMath.float3;

public class CombatState : BaseState
{
    private const int DPS_SAMPLE_COUNT = 32;
    private float _optimumRange;
    private readonly List<(int index, float dps)> _availableGroups = new List<(int index, float dps)>();
    private readonly List<LockWeapon> _availableLockingWeapons = new List<LockWeapon>();

    public CombatState(Agent agent) : base(agent)
    {
        SampleDps();
        foreach (var weapon in _agent.Ship.Weapons)
        {
            weapon.Item.Online.Subscribe(b => SampleDps());
        }
    }

    public override void Update(float delta)
    {
        var target = _agent.Ship.Target.Value;
        if (target == null) return;
        
        _availableGroups.Clear();
        _availableLockingWeapons.Clear();
        
        var shipPosition = _agent.Ship.CultPosition;
        var targetPosition = target.CultPosition;
        var toTarget = targetPosition - shipPosition;
        var targetDistance = length(toTarget);
        for (var i = 0; i < _agent.Ship.WeaponGroups.Length; i++)
        {
            var group = _agent.Ship.WeaponGroups[i];
            var dps = 0f;
            foreach (var weapon in group.weapons)
                if (weapon.Item.Online.Value && 
                    weapon.MinRange < targetDistance && 
                    targetDistance < weapon.Range && 
                    (weapon is ConstantWeapon || 
                     weapon is InstantWeapon instantWeapon && instantWeapon.CanFire))
                {
                    if(weapon is LockWeapon lockWeapon)
                        _availableLockingWeapons.Add(lockWeapon);
                    else
                        dps += weapon.RangeDamagePerSecond(targetDistance);
                }

            if (dps > .1) _availableGroups.Add((i, dps));
        }
        
        foreach(var w in _availableLockingWeapons) w.Activate();

        var selectedGroup = -1;
        var maxDps = Single.MinValue;
        foreach (var group in _availableGroups)
        {
            if (group.dps > maxDps)
            {
                maxDps = group.dps;
                selectedGroup = group.index;
            }
        }

        var targetDirection = target.CultDirection;
        var targetRight = AetheriaMath.Rotate(targetDirection, ItemRotation.Clockwise);
        var optimumRangeDelta = abs(_optimumRange - targetDistance);
        var directionToTarget = normalize(toTarget.xz);
        var targetPortAlignment = dot(targetRight, directionToTarget);
        var targetForeAlignment = dot(-targetDirection, directionToTarget);
        var forwardness = saturate(optimumRangeDelta / _agent.Settings.AgentMaxForwardDistance) * _agent.Settings.AgentForwardLerp;
        if (targetForeAlignment > 0) forwardness = lerp(forwardness, 1, pow(targetPortAlignment, 2));

        var movementDirection = normalize(lerp(
            AetheriaMath.Rotate(directionToTarget, targetPortAlignment > 0 ? ItemRotation.Clockwise : ItemRotation.CounterClockwise),
            targetDistance > _optimumRange ? directionToTarget : -directionToTarget, forwardness));

        if (selectedGroup >= 0)
        {
            var testWeapon = _agent.Ship.WeaponGroups[selectedGroup].weapons.First();
            if(testWeapon.Velocity > 1)
            {
                var targetHull = _agent.ItemManager.GetRuntimeItem(target.Hull);
                var targetVelocity = new cfloat3(target.CultVelocity.x, 0, target.CultVelocity.y);
                var predictedPosition = AetheriaMath.FirstOrderIntercept(
                    shipPosition,
                    cfloat3.zero,
                    testWeapon.Velocity,
                    targetPosition,
                    targetVelocity
                );
                predictedPosition.y = _agent.Ship.Zone.GetHeight(predictedPosition.xz) + (float)(targetHull?.HullGridOffset ?? 0);
                toTarget = normalize(predictedPosition - shipPosition);
            }
            
            var shouldFire = dot(
                _agent.Ship.HardpointTransforms[_agent.Ship.Hardpoints[testWeapon.Item.Position.x, testWeapon.Item.Position.y]].direction,
                toTarget) > .99f;
            foreach (var weapon in _agent.Ship.WeaponGroups[selectedGroup].weapons)
            {
                if (shouldFire)
                    weapon.Activate();
                else if (weapon.Firing)
                    weapon.Deactivate();
            }
        }
        _agent.Ship.CultLookDirection = toTarget;
        _agent.Accelerate(movementDirection * _agent.TopSpeed, true);//selectedGroup >= 0);

        // Fire charged guns!
    }

    private void SampleDps()
    {
        var minRange = Single.MaxValue;
        var maxRange = Single.MinValue;
        foreach (var weapon in _agent.Ship.Weapons)
        {
            if (weapon.Range < minRange)
                minRange = weapon.Range;
            if (weapon.Range > maxRange)
                maxRange = weapon.Range;
        }

        var offset = _agent.ItemManager.Random.NextFloat((maxRange - minRange) / DPS_SAMPLE_COUNT);
        var optimumDPS = 0f;
        for (int i = 0; i < DPS_SAMPLE_COUNT; i++)
        {
            var range = offset + minRange + (maxRange - minRange) * ((float) i / DPS_SAMPLE_COUNT);
            float dps = 0;
            foreach (var weapon in _agent.Ship.Weapons)
            {
                if (weapon.Item.Online.Value && weapon.MinRange < range && range < weapon.Range)
                    dps += weapon.RangeDamagePerSecond(range);
            }

            // Multiply by range raised to a (sublinear) exponent to bias DPS preference towards longer ranges
            dps *= pow(range, _agent.Settings.AgentRangeExponent);

            if (dps > optimumDPS)
            {
                optimumDPS = dps;
                _optimumRange = range;
            }
        }
    }
}

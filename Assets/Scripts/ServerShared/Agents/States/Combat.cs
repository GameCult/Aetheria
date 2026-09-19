using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;
using float2x2 = CultMath.float2x2;

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
        
        var toTarget = target.Position - _agent.Ship.Position;
        var targetDistance = length(toTarget);

        // Cut 2 (docs/fire-control-cut.md): aim at whichever revealed weapon on the target hits hardest at
        // this range, or clear the aim point when nothing qualifies -- the same predicate
        // (FireControl.IsRevealed) and the same writer (TrySelectTargetItem) the player path uses.
        EquippedItem bestTargetItem = null;
        var bestTargetDps = float.MinValue;
        foreach (var targetItem in target.Equipment)
        {
            if (!(targetItem.Behaviors.FirstOrDefault(b => b is Weapon) is Weapon targetWeapon)) continue;
            if (!FireControl.IsRevealed(_agent.Ship, targetItem)) continue;
            var dps = targetWeapon.RangeDamagePerSecond(targetDistance);
            if (dps > bestTargetDps)
            {
                bestTargetDps = dps;
                bestTargetItem = targetItem;
            }
        }
        _agent.Ship.TrySelectTargetItem(bestTargetItem);

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

        var targetRight = target.Direction.Rotate(ItemRotation.Clockwise);
        var optimumRangeDelta = abs(_optimumRange - targetDistance);
        var directionToTarget = normalize(toTarget.xz);
        var targetPortAlignment = dot(targetRight, directionToTarget);
        var targetForeAlignment = dot(-target.Direction, directionToTarget);
        var forwardness = saturate(optimumRangeDelta / _agent.Settings.AgentMaxForwardDistance) * _agent.Settings.AgentForwardLerp;
        if (targetForeAlignment > 0) forwardness = lerp(forwardness, 1, pow(targetPortAlignment, 2));

        var movementDirection = normalize(lerp(
            directionToTarget.Rotate(targetPortAlignment > 0 ? ItemRotation.Clockwise : ItemRotation.CounterClockwise),
            targetDistance > _optimumRange ? directionToTarget : -directionToTarget, forwardness));

        if (selectedGroup >= 0)
        {
            var testWeapon = _agent.Ship.WeaponGroups[selectedGroup].weapons.First();
            if(testWeapon.Velocity > 1)
            {
                // Cut 3 (docs/fire-control-cut.md): the one prediction function, shared with FireControl.Fire's
                // own snapshot and the roll's deviation measurement -- two separate predictions here and in the
                // roll would let an AI aim at one point and get judged for missing a different one.
                var targetHullData = _agent.ItemManager.GetData(target.Hull) as HullData;
                var predictedPosition = FireControl.PredictedIntercept(testWeapon, _agent.Ship, target);
                predictedPosition.y = _agent.Ship.Zone.GetHeight(predictedPosition.xz) + targetHullData.GridOffset;
                toTarget = normalize(predictedPosition - _agent.Ship.Position);
            }

            // Cut 3: replaces Cut 1's bare InArc, which HitProbability already subsumes (it returns zero out
            // of arc) -- this is the AI's own "worth it" heuristic layered on top of the shared gate, not a
            // second gate.
            var shouldFire = FireControl.HitProbability(testWeapon, _agent.Ship, target) >= _agent.Settings.AgentMinHitProbability;
            foreach (var weapon in _agent.Ship.WeaponGroups[selectedGroup].weapons)
            {
                if (shouldFire)
                    weapon.Activate();
                else if (weapon.Firing)
                    weapon.Deactivate();
            }
        }
        _agent.Ship.LookDirection = toTarget;
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
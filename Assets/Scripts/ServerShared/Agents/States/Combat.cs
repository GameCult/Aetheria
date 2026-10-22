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
    // Overdrive only to close a target farther than this many optimum ranges, while heading within this many degrees of it.
    private const float OverdriveRangeFactor = 2f;
    private const float OverdriveArcDegrees = 15f;
    private float _optimumRange;
    public float OptimumRange => _optimumRange;
    private bool _optimumStale = true;
    private readonly List<(int index, float dps)> _availableGroups = new List<(int index, float dps)>();
    private readonly List<LockWeapon> _availableLockingWeapons = new List<LockWeapon>();
    
    public CombatState(Agent agent) : base(agent)
    {
        foreach (var item in _agent.Ship.Equipment) Watch(item);
        _agent.Ship.Equipment.ObserveAdd().Subscribe(add => Watch(add.Value));
        _agent.Ship.Equipment.ObserveRemove().Subscribe(_ => _optimumStale = true);
    }

    // The optimum is a function of the loadout's evaluated stats, so it is re-derived at the next Update (agents update
    // before the entities, so by then the weapons that came online this tick have evaluated) whenever a weapon is
    // added, removed or changes online state. Sampling on the event itself would read the stats before they exist: a
    // ship the zone just spawned would keep an optimum of 0.
    private void Watch(EquippedItem item)
    {
        if (item.Behaviors.OfType<Weapon>().Any()) item.Online.Subscribe(_ => _optimumStale = true);
    }

    public override void Update(float delta)
    {
        if (_optimumStale)
        {
            SampleDps();
            _optimumStale = false;
        }

        // The pilot's own pending designation (its Flight's, this step) when set, else last step's applied target; and
        // only a contact a member tracks has a track to fly on, so an untracked target is no target here.
        var view = _agent.View;
        var target = view.TargetOf(_agent.Ship);
        if (target == null) return;
        var track = view.TrackOf(_agent.Ship, target.Value);
        if (track == null) return;
        
        _availableGroups.Clear();
        _availableLockingWeapons.Clear();
        
        var toTarget = track.Value.Position - _agent.Ship.Position;
        var targetDistance = length(toTarget.xz);

        // Cut 2 (docs/fire-control-cut.md): aim at whichever revealed weapon on the target hits hardest at
        // this range, or clear the aim point when nothing qualifies -- the same predicate
        // (the view's Aimable) and the same writer (TrySelectTargetItem) the player path uses. Which weapons it
        // believes the target has, and how hard each hits, come from its book's claims, not the target's live gear
        // (aetheria-ew, classify-from-belief; ew-claim-weapon-measure-ruled: the claimed design's rated figures); the
        // reveal's Item is the aim and stays the truth.
        PilotView.RevealId? bestTargetItem = null;
        var bestTargetDps = float.MinValue;
        foreach (var reveal in view.WeaponReveals(_agent.Ship, target.Value))
        {
            if (!reveal.Aimable) continue;
            var dps = reveal.Claimed.Behaviors.OfType<WeaponData>().First().RatedRangeDamagePerSecond(targetDistance);
            if (dps > bestTargetDps)
            {
                bestTargetDps = dps;
                bestTargetItem = reveal.Id;
            }
        }
        view.SelectItem(_agent.Ship, bestTargetItem);

        for (var i = 0; i < _agent.Ship.WeaponGroups.Length; i++)
        {
            var group = _agent.Ship.WeaponGroups[i];
            var dps = 0f;
            foreach (var weapon in group.weapons)
                // The range test is FireControl's (Designated), and a weapon Solve refuses adds nothing to a group's
                // damage.
                if (weapon.Item.Online.Value &&
                    view.Designated(_agent.Ship, weapon, target.Value) &&
                    !view.Refuses(_agent.Ship, weapon, target.Value) &&
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
        
        foreach(var w in _availableLockingWeapons) _agent.Ship.Command.Pull(w);

        UseConsumables(toTarget, targetDistance);

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

        var targetRight = track.Value.Direction.Rotate(ItemRotation.Clockwise);
        var optimumRangeDelta = abs(_optimumRange - targetDistance);
        var directionToTarget = normalize(toTarget.xz);
        var targetPortAlignment = dot(targetRight, directionToTarget);
        var targetForeAlignment = dot(-track.Value.Direction, directionToTarget);
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
                // Cut 3 (docs/fire-control-cut.md): the one prediction function, shared with the shot's
                // own snapshot and the roll's deviation measurement -- two separate predictions here and in the
                // roll would let an AI aim at one point and get judged for missing a different one.
                // The target is tracked here (the guard above), so the intercept is never null.
                var predictedPosition = view.Intercept(_agent.Ship, testWeapon, target.Value);
                if (predictedPosition != null) toTarget = normalize(predictedPosition.Value - _agent.Ship.Position);
            }

            // Each weapon decides for itself (operator ruling 2026-09-30): a group shares a trigger, not a
            // verdict. The view's Fires is the AI's "worth it" heuristic for a weapon that hits what it
            // fires at, and "the target is designated" for a fused one.
            foreach (var weapon in _agent.Ship.WeaponGroups[selectedGroup].weapons)
            {
                if (view.Fires(_agent.Ship, weapon, target.Value, _agent.Doctrine.MinHitProbability, bestTargetItem))
                    _agent.Ship.Command.Pull(weapon);
                // A lock pulled above this step counts as firing, as it did when it was activated in place.
                else if (weapon.Firing || _agent.Ship.Command.Pulls(weapon))
                    _agent.Ship.Command.Release(weapon);
            }
        }
        _agent.Ship.Command.Aim = toTarget;
        _agent.Ship.Command.Turn = TurnCommand.Toward(toTarget.xz);
        _agent.Accelerate(movementDirection * _agent.TopSpeed, true);//selectedGroup >= 0);

        // Fire charged guns!
    }

    // Overdrive to close a distant target ahead, through the same consumable gate as the vent (Agent.VentOnTrack).
    private void UseConsumables(float3 toTarget, float targetDistance)
    {
        var ship = _agent.Ship;
        if (!_agent.TrackAlarm && targetDistance > OverdriveRangeFactor * _optimumRange &&
            dot(normalize(ship.Direction), normalize(toTarget.xz)) > cos(radians(OverdriveArcDegrees)))
            _agent.Use(b => b is ThrottleLockData);
    }

    private void SampleDps()
    {
        var minRange = Single.MaxValue;
        var maxRange = Single.MinValue;
        foreach (var weapon in _agent.Ship.Weapons)
        {
            if (weapon.MinRange < minRange)
                minRange = weapon.MinRange;
            if (weapon.Range > maxRange)
                maxRange = weapon.Range;
        }

        // The pilot's own dice, a pure function of the zone seed and its id: the same offset at every resample, and
        // nothing that mints an item can move it.
        var offset = SimulationDice.For(_agent.Ship.Zone.CombatSeed, SimulationDice.CombatSampleStream, _agent.Id)
            .NextFloat((maxRange - minRange) / DPS_SAMPLE_COUNT);
        var optimumDPS = 0f;
        for (int i = 0; i < DPS_SAMPLE_COUNT; i++)
        {
            var range = offset + minRange + (maxRange - minRange) * ((float) i / DPS_SAMPLE_COUNT);
            float dps = 0;
            foreach (var weapon in _agent.Ship.Weapons)
            {
                if (weapon.Item.Online.Value)
                    dps += weapon.RangeDamagePerSecond(range);
            }

            // Multiply by range raised to a (sublinear) exponent to bias DPS preference towards longer ranges
            dps *= pow(range, _agent.Doctrine.RangeExponent);

            if (dps > optimumDPS)
            {
                optimumDPS = dps;
                _optimumRange = range;
            }
        }
    }
}
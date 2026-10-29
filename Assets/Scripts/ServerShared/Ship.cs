/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MessagePack;
using Newtonsoft.Json;
using CultMath;
using static CultMath.math;
using quaternion = CultMath.quaternion;

[MessagePackObject]
public class Ship : Entity
{
    public override float LayerHeight => ItemManager.GameplaySettings.ShipLayerHeight;

    
    // [Key("bindings")]   public Dictionary<KeyCode,Guid>    Bindings = new Dictionary<KeyCode,Guid>();
    //[IgnoreMember] public int HullHardpointCount;

    // [IgnoreMember] public Dictionary<Targetable, float> Contacts = new Dictionary<Targetable, float>();
    // [IgnoreMember] public Targetable Target;
    public Entity HomeEntity;
    // The pilot-controlled hull fields. PilotApplier, in the zone step, is their pilot-side writer, from the command the
    // pilot (player input or the ship's agent) gave the entity; no Unity script can set them. Runtime only, never packed.
    public float2 MovementDirection { get; internal set; }
    // The one facing command, -1..1, positive clockwise; a heading becomes a demand only through Steering.Toward.
    public float Turn { get; internal set; }
    public bool IsPlayerShip;

    private Thruster[] _allThrusters;
    // One column per thruster (its effect per unit throttle, in the body frame) and the throttles the allocator answers with.
    private float3[] _columns;
    private float[] _throttles;
    private readonly ThrustAllocator _allocator = new ThrustAllocator();

    private bool _exitingWormhole = false;
    private bool _enteringWormhole = false;
    private float _wormholeAnimationProgress;
    private float2 _wormholeEntryPosition;
    private float2 _wormholeEntryDirection;
    private float2 _wormholePosition;
    private float2 _wormholeExitVelocity;

    public bool WormholeAnimationInProgress => _enteringWormhole || _exitingWormhole;

    public event Action OnExitedWormhole;
    public event Action OnEnteredWormhole;
    
    public quaternion Rotation { get; private set; }

    public void ExitWormhole(float2 wormholePosition, float2 exitVelocity)
    {
        _exitingWormhole = true;
        _wormholeAnimationProgress = 0;
        _wormholePosition = wormholePosition;
        _wormholeExitVelocity = exitVelocity;
        Direction = normalize(exitVelocity);
    }

    public void EnterWormhole(float2 wormholePosition)
    {
        SetTarget(TargetRef.None);
        _wormholeAnimationProgress = 0;
        _enteringWormhole = true;
        _wormholeEntryPosition = Position.xz;
        _wormholeEntryDirection = normalize(_wormholeEntryPosition-wormholePosition);
        _wormholePosition = wormholePosition;
    }

    public override void Activate()
    {
        base.Activate();

        _allThrusters = GetBehaviors<Thruster>().ToArray();
        _columns = new float3[_allThrusters.Length];
        _throttles = new float[_allThrusters.Length];
    }

    public Ship(ItemManager itemManager, Zone zone, EquippableItem hull, EntitySettings settings) : base(itemManager, zone, hull, settings)
    {
    }

    #region ThrustCalculation

    // What this ship can do at this instant (see ManoeuvreEnvelope): the box extremes of the thrusters' columns at their
    // live thrust (zero when unpowered or not Active), because it reports what the ship can do now. The allocator solves
    // with Column(NominalThrust), the full-grant column, because a column promises what a thruster does at a full grant
    // and the allocator must not reshape its answer as the grant moves. Same geometry (Thruster.Column), different thrust.
    // Derived each update, never saved.
    public ManoeuvreEnvelope Envelope { get; private set; }

    private void RecalculateEnvelope()
    {
        float forward = 0, back = 0, left = 0, right = 0, clockwise = 0, counterClockwise = 0;
        foreach (var thruster in _allThrusters)
        {
            var column = thruster.Column(thruster.Item != null && thruster.Item.PowerSupply > 1e-4f ? thruster.Thrust : 0f);
            forward += max(column.y, 0f);
            back += max(-column.y, 0f);
            left += max(-column.x, 0f);
            right += max(column.x, 0f);
            clockwise += max(column.z, 0f);
            counterClockwise += max(-column.z, 0f);
        }
        Envelope = new ManoeuvreEnvelope(forward, back, left, right, clockwise, counterClockwise);
    }

    #endregion

    // The player's turn axis as a fraction of the hull's yaw extreme, banded so each thruster joins over an equal share of
    // the input. The allocator serves a turn by the thrusters with the least translation per unit of yaw first and brings in
    // the next as they saturate; here the thrusters yawing the asked way are ranked by that ratio, equal ratios form one band,
    // and band i fills over input [i/k, (i+1)/k]. A hull whose best-ratio thrusters hold a small share of its yaw then gets
    // half the travel of a two-band turn before its drives join, instead of the first tenth. Full input is still full yaw.
    public float BandedTurn(float axis)
    {
        axis = clamp(axis, -1f, 1f);
        if (axis == 0 || _allThrusters == null) return axis;
        var direction = axis > 0 ? 1f : -1f;
        var thrusters = new List<(float ratio, float yaw)>();
        var total = 0f;
        foreach (var thruster in _allThrusters)
        {
            var column = thruster.Column(thruster.NominalThrust);
            var yaw = column.z * direction;
            if (!(yaw > 0)) continue;
            thrusters.Add((length(float2(column.x, column.y)) / yaw, yaw));
            total += yaw;
        }
        if (!(total > 0)) return axis;
        thrusters.Sort((a, b) => a.ratio.CompareTo(b.ratio));
        var bands = new List<float>();
        var bandRatio = float.NaN;
        foreach (var (ratio, yaw) in thrusters)
        {
            if (bands.Count > 0 && abs(ratio - bandRatio) <= .02f * max(bandRatio, 1e-6f)) bands[bands.Count - 1] += yaw;
            else { bands.Add(yaw); bandRatio = ratio; }
        }
        var travel = abs(axis) * bands.Count;
        var band = min((int)travel, bands.Count - 1);
        var served = 0f;
        for (var i = 0; i < band; i++) served += bands[i];
        served += (travel - band) * bands[band];
        return direction * min(served / total, 1f);
    }

    // Called by Entity.Update once the items' performance is current and before the power bus is stepped and the items
    // execute: liveness, thrust and the throttles they decide are one phase, the one Execute acts on. The lock is a
    // bound on the allocator's solve; intent reaches the allocator here and nowhere else.
    protected override void UpdateThrottles()
    {
        if (_exitingWormhole || _enteringWormhole) return;
        RecalculateEnvelope();
        for (var i = 0; i < _allThrusters.Length; i++)
            _columns[i] = _allThrusters[i].Column(_allThrusters[i].NominalThrust);
        _allocator.Allocate(_columns, MovementDirection, Turn, _throttles, ThrottleLocked);
        for (var i = 0; i < _allThrusters.Length; i++) _allThrusters[i].Axis = _throttles[i];
    }

    public override void Update(float delta)
    {
        // No drag below top speed: ships coast and orbits do not decay; drag bleeds off only the excess over the hull's top
        // speed (aetheria-gravity:ruling:no-drag-below-top-speed). The VelocityLimit clamp still caps it, so the excess is
        // what this step's forces added; a soft cap, where surfing the beat is a temporary boost, is the ruling's other half.
        var velocityMagnitude = length(Velocity);
        var topSpeed = float.PositiveInfinity;
        foreach (var limit in GetBehaviors<VelocityLimit>()) topSpeed = min(topSpeed, limit.Limit);
        if (velocityMagnitude > topSpeed)
            Velocity = normalize(Velocity) * (topSpeed + decay(velocityMagnitude - topSpeed, HullData.Drag * ItemManager.GameplaySettings.DragScale, delta));
        
        Position.xz += Velocity * delta;
        
        Velocity += Zone.GetForce(Position.xz) * delta;
        var normal = Zone.GetNormal(Position.xz);
        var shipRight = Direction.Rotate(ItemRotation.Clockwise);
        var forward = cross(float3(shipRight.x, 0, shipRight.y), normal);
        Rotation = quaternion.LookRotation(forward, normal);
        
        base.Update(delta);

        if (_exitingWormhole)
        {
            _wormholeAnimationProgress += delta / ItemManager.GameplaySettings.WormholeAnimationDuration;
            if(_wormholeAnimationProgress < 1)
            {
                if (_wormholeAnimationProgress < ItemManager.GameplaySettings.WormholeExitCurveStart)
                {
                    Position.xz = _wormholePosition;
                    Rotation = quaternion.LookRotation(float3(0, 1, 0), float3(-Direction.x, 0, -Direction.y));
                }
                else
                {
                    var exitLerp = (_wormholeAnimationProgress - ItemManager.GameplaySettings.WormholeExitCurveStart) /
                                   (1 - ItemManager.GameplaySettings.WormholeExitCurveStart);
                    exitLerp = smootherstep(exitLerp); // Square the interpolation variable to produce curve with zero slope at start
                    Position.xz = _wormholePosition + normalize(_wormholeExitVelocity) * exitLerp * ItemManager.GameplaySettings.WormholeExitRadius;
                    Rotation = quaternion.LookRotation(
                        lerp(float3(0, 1, 0), forward, exitLerp),
                        lerp(float3(-Direction.x, 0, -Direction.y), normal, exitLerp));
                }

                Position.y = Position.y - lerp(ItemManager.GameplaySettings.WormholeDepth, 0, _wormholeAnimationProgress);
            }
            else
            {
                _exitingWormhole = false;
                OnExitedWormhole?.Invoke();
                OnExitedWormhole = null;
                Velocity = _wormholeExitVelocity;
            }
        }

        if (_enteringWormhole)
        {
            _wormholeAnimationProgress += delta / ItemManager.GameplaySettings.WormholeAnimationDuration;
            if(_wormholeAnimationProgress < 1)
            {
                if (_wormholeAnimationProgress < 1 - ItemManager.GameplaySettings.WormholeExitCurveStart)
                {
                    var enterLerp = _wormholeAnimationProgress / (1 - ItemManager.GameplaySettings.WormholeExitCurveStart);
                    enterLerp = smootherstep(enterLerp); // Square the interpolation variable to produce curve with zero slope at vertical
                    Position.xz = lerp(_wormholeEntryPosition, _wormholePosition, enterLerp);
                    Rotation = quaternion.LookRotation(
                        lerp(forward, float3(0, -1, 0), enterLerp),
                        lerp(normal, float3(-_wormholeEntryDirection.x, 0, -_wormholeEntryDirection.y), enterLerp));
                }
                else
                {
                    Position.xz = _wormholePosition;
                    Rotation = quaternion.LookRotation(float3(0, -1, 0), 
                        float3(-_wormholeEntryDirection.x, 0, -_wormholeEntryDirection.y));
                }

                Position.y = Position.y - lerp(0, ItemManager.GameplaySettings.WormholeDepth, _wormholeAnimationProgress);
            }
            else
            {
                _enteringWormhole = false;
                OnEnteredWormhole?.Invoke();
                OnEnteredWormhole = null;
            }
        }
    }
}


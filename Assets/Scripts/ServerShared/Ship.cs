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
using UniRx;
using CultMath;
using static CultMath.math;
using quaternion = CultMath.quaternion;

[MessagePackObject]
public class Ship : Entity
{
    
    // [Key("bindings")]   public Dictionary<KeyCode,Guid>    Bindings = new Dictionary<KeyCode,Guid>();
    //[IgnoreMember] public int HullHardpointCount;

    // [IgnoreMember] public Dictionary<Targetable, float> Contacts = new Dictionary<Targetable, float>();
    // [IgnoreMember] public Targetable Target;
    public Entity HomeEntity;
    public float2 MovementDirection;
    // The one facing command, -1..1, positive clockwise. Written each tick by the pilot (player input or the
    // ship's agent); a heading becomes a demand only through Steering.Toward. Runtime only, never packed.
    public float Turn;
    public bool IsPlayerShip;

    private HashSet<EquippedItem> _thrusterItems;
    private Thruster[] _allThrusters;
    private HashSet<Thruster> _forwardThrusters;
    private HashSet<Thruster> _reverseThrusters;
    private HashSet<Thruster> _rightThrusters;
    private HashSet<Thruster> _leftThrusters;
    private HashSet<Thruster> _clockwiseThrusters;
    private HashSet<Thruster> _counterClockwiseThrusters;

    private bool _exitingWormhole = false;
    private bool _enteringWormhole = false;
    private float _wormholeAnimationProgress;
    private float2 _wormholeEntryPosition;
    private float2 _wormholeEntryDirection;
    private float2 _wormholePosition;
    private float2 _wormholeExitVelocity;

    public bool WormholeAnimationInProgress => _enteringWormhole || _exitingWormhole;
    public float ForwardThrust { get; private set; }
    public float ReverseThrust { get; private set; }
    public float LeftStrafeThrust { get; private set; }
    public float RightStrafeThrust { get; private set; }
    public float ClockwiseTorque { get; private set; }
    public float CounterClockwiseTorque { get; private set; }
    public float LeftStrafeTotalTorque { get; private set; }
    private List<Thruster> LeftStrafeTorqueThrusters = new List<Thruster>();
    public float RightStrafeTotalTorque { get; private set; }
    private List<Thruster> RightStrafeTorqueThrusters = new List<Thruster>();

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
        _thrusterItems = new HashSet<EquippedItem>(_allThrusters.Select(x=>x.Item));
        
        _forwardThrusters = new HashSet<Thruster>(_allThrusters
            .Where(x => x.Item.EquippableItem.Rotation == ItemRotation.Reversed));

        _reverseThrusters = new HashSet<Thruster>(_allThrusters
            .Where(x => x.Item.EquippableItem.Rotation == ItemRotation.None));
        
        _rightThrusters = new HashSet<Thruster>(_allThrusters
            .Where(x => x.Item.EquippableItem.Rotation == ItemRotation.CounterClockwise));
        
        _leftThrusters = new HashSet<Thruster>(_allThrusters
            .Where(x => x.Item.EquippableItem.Rotation == ItemRotation.Clockwise));
        
        _counterClockwiseThrusters = new HashSet<Thruster>(_allThrusters
            .Where(x => x.Torque < -ItemManager.GameplaySettings.TorqueFloor));
        
        _clockwiseThrusters = new HashSet<Thruster>(_allThrusters
            .Where(x => x.Torque > ItemManager.GameplaySettings.TorqueFloor));
    }

    public Ship(ItemManager itemManager, Zone zone, EquippableItem hull, EntitySettings settings) : base(itemManager, zone, hull, settings)
    {
        ItemDestroyed.Where(item=>_thrusterItems.Contains(item)).Subscribe(RemoveThruster);
    }

    private void RemoveThruster(EquippedItem item)
    {
        _thrusterItems.Remove(item);
        var thruster = item.GetBehavior<Thruster>();
        if (_forwardThrusters.Contains(thruster)) _forwardThrusters.Remove(thruster);
        if (_reverseThrusters.Contains(thruster)) _reverseThrusters.Remove(thruster);
        if (_rightThrusters.Contains(thruster)) _rightThrusters.Remove(thruster);
        if (_leftThrusters.Contains(thruster)) _leftThrusters.Remove(thruster);
        if (_clockwiseThrusters.Contains(thruster)) _clockwiseThrusters.Remove(thruster);
        if (_counterClockwiseThrusters.Contains(thruster)) _counterClockwiseThrusters.Remove(thruster);
    }

    #region ThrustCalculation

    // What this ship can do at this instant (see ManoeuvreEnvelope): the sum of what each live propulsor reports with
    // its own Execute arithmetic; thrusters are its only propulsors. Derived each update, never saved. The aggregates above are not an envelope (they
    // mix units and cancel a lone off-axis strafer), so nothing in it reads them.
    public ManoeuvreEnvelope Envelope { get; private set; }

    private void RecalculateEnvelope()
    {
        var envelope = default(ManoeuvreEnvelope);
        foreach (var thruster in _allThrusters) envelope += thruster.Manoeuvre();
        Envelope = envelope;
    }

    private void RecalculateThrust()
    {
        RecalculateEnvelope();
        RecalculateForwardThrust();
        RecalculateReverseThrust();
        RecalculateLeftStrafeThrust();
        RecalculateRightStrafeThrust();
        RecalculateClockwiseTorque();
        RecalculateCounterClockwiseTorque();
    }
    
    private void RecalculateForwardThrust()
    {
        ForwardThrust = 0;
        foreach (var thruster in _forwardThrusters)
            if (thruster.Item.Active.Value)
                ForwardThrust += thruster.Thrust;
    }

    private void RecalculateReverseThrust()
    {
        ReverseThrust = 0;
        foreach (var thruster in _reverseThrusters)
            if (thruster.Item.Active.Value)
                ReverseThrust += thruster.Thrust;
    }

    private void RecalculateLeftStrafeThrust()
    {
        LeftStrafeThrust = 0;
        LeftStrafeTotalTorque = 0;
        foreach (var thruster in _leftThrusters)
        {
            if(thruster.Item.Active.Value)
            {
                LeftStrafeThrust += thruster.Thrust;
                LeftStrafeTotalTorque += thruster.Torque * thruster.Thrust;
            }
        }
        LeftStrafeTorqueThrusters.Clear();
        foreach(var thruster in _leftThrusters)
            if (abs(sign(thruster.Torque) - sign(LeftStrafeTotalTorque)) < .01f)
                LeftStrafeTorqueThrusters.Add(thruster);
    }

    private void RecalculateRightStrafeThrust()
    {
        RightStrafeThrust = 0;
        RightStrafeTotalTorque = 0;
        foreach (var thruster in _rightThrusters)
        {
            if(thruster.Item.Active.Value)
            {
                RightStrafeThrust += thruster.Thrust;
                RightStrafeTotalTorque += thruster.Torque * thruster.Thrust;
            }
        }
        RightStrafeTorqueThrusters.Clear();
        foreach(var thruster in _rightThrusters)
            if (abs(sign(thruster.Torque) - sign(RightStrafeTotalTorque)) < .01f)
                RightStrafeTorqueThrusters.Add(thruster);
    }

    private void RecalculateClockwiseTorque()
    {
        ClockwiseTorque = 0;
        foreach (var thruster in _clockwiseThrusters)
            if (thruster.Item.Active.Value)
                ClockwiseTorque += thruster.Torque;
    }

    private void RecalculateCounterClockwiseTorque()
    {
        CounterClockwiseTorque = 0;
        foreach (var thruster in _counterClockwiseThrusters)
            if (thruster.Item.Active.Value)
                CounterClockwiseTorque -= thruster.Torque;
    }

    #endregion

    public override void Update(float delta)
    {
        if (_active && !_exitingWormhole && !_enteringWormhole)
        {
            RecalculateThrust();
            // The lock replaces intent here, once; player and agent intent both pass through this one read.
            var move = ThrottleLocked ? float2(0, 1) : MovementDirection;
            foreach (var thruster in _allThrusters) thruster.Axis = 0;
            var rightThrusterTorqueCompensation = abs(RightStrafeTotalTorque) / RightStrafeTorqueThrusters.Count;
            foreach (var thruster in _rightThrusters)
            {
                var thrust = 0f;
                thrust += move.x;
                if (RightStrafeTorqueThrusters.Contains(thruster))
                    thrust -= move.x * (rightThrusterTorqueCompensation / (abs(thruster.Torque) * thruster.Thrust));
                thruster.Axis = thrust;
            }
            var leftThrusterTorqueCompensation = abs(LeftStrafeTotalTorque) / LeftStrafeTorqueThrusters.Count;
            foreach (var thruster in _leftThrusters)
            {
                var thrust = 0f;
                thrust += -move.x;
                if (LeftStrafeTorqueThrusters.Contains(thruster))
                    thrust += move.x * (leftThrusterTorqueCompensation / (abs(thruster.Torque) * thruster.Thrust));
                thruster.Axis = thrust;
            }
            foreach (var thruster in _forwardThrusters) thruster.Axis += move.y;
            foreach (var thruster in _reverseThrusters) thruster.Axis += -move.y;

            foreach (var thruster in _clockwiseThrusters) thruster.Axis += Turn;
            foreach (var thruster in _counterClockwiseThrusters) thruster.Axis += -Turn;
        }

        var velocityMagnitude = length(Velocity);
        if(velocityMagnitude > .01f)
            Velocity = normalize(Velocity) * decay(velocityMagnitude, HullData.Drag, delta);
        
        Position.xz += Velocity * delta;
        
        var normal = Zone.GetNormal(Position.xz);
        var force = new float2(normal.x, normal.z);
        Velocity += Zone.GravityAcceleration(force, Zone.Settings.GravityStrength) * delta;
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


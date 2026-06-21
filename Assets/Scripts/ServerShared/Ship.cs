/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CultMath;
using UniRx;
using static CultMath.math;
using cfloat2 = CultMath.float2;
using cfloat3 = CultMath.float3;
using cquaternion = CultMath.quaternion;

public class Ship : Entity
{
    public Entity HomeEntity;
    public cfloat2 MovementDirection;
    public bool IsPlayerShip;

    private HashSet<EquippedItem> _thrusterItems;
    private Thruster[] _allThrusters;
    private HashSet<Thruster> _forwardThrusters;
    private HashSet<Thruster> _reverseThrusters;
    private HashSet<Thruster> _rightThrusters;
    private HashSet<Thruster> _leftThrusters;
    private HashSet<Thruster> _clockwiseThrusters;
    private HashSet<Thruster> _counterClockwiseThrusters;
    private HashSet<EquippedItem> _aetherDriveItems;
    private HashSet<AetherDrive> _aetherDrives;

    private bool _exitingWormhole = false;
    private bool _enteringWormhole = false;
    private float _wormholeAnimationProgress;
    private cfloat2 _wormholeEntryPosition;
    private cfloat2 _wormholeEntryDirection;
    private cfloat2 _wormholePosition;
    private cfloat2 _wormholeExitVelocity;

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

    public float TurnTime(cfloat2 direction)
    {
        var cultDirection = normalize(direction);
        var shipDirection = CultDirection;
        var angleDiff = AetheriaMath.AngleDegrees(shipDirection, cultDirection);
        var clockwise = dot(cultDirection, AetheriaMath.Rotate(shipDirection, ItemRotation.Clockwise)) > 0;
        return angleDiff / ((clockwise ? ClockwiseTorque : CounterClockwiseTorque) / Mass);
    }

    public event Action OnExitedWormhole;
    public event Action OnEnteredWormhole;

    public cquaternion Rotation { get; private set; }

    private static cquaternion LookRotation(cfloat3 forward, cfloat3 up) =>
        cquaternion.LookRotation(forward, up);

    public void ExitWormhole(cfloat2 wormholePosition, cfloat2 exitVelocity)
    {
        _exitingWormhole = true;
        _wormholeAnimationProgress = 0;
        _wormholePosition = wormholePosition;
        _wormholeExitVelocity = exitVelocity;
        CultDirection = normalize(_wormholeExitVelocity);
    }

    public void EnterWormhole(cfloat2 wormholePosition)
    {
        Target.Value = null;
        _wormholeAnimationProgress = 0;
        _enteringWormhole = true;
        _wormholeEntryPosition = CultPositionXZ;
        _wormholePosition = wormholePosition;
        _wormholeEntryDirection = normalize(_wormholeEntryPosition - _wormholePosition);
    }

    public override void Activate()
    {
        base.Activate();

        _aetherDrives = new HashSet<AetherDrive>(GetBehaviors<AetherDrive>());
        _aetherDriveItems = new HashSet<EquippedItem>(_aetherDrives.Select(x => x.Item));

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
        ItemDestroyed.Where(item=>_aetherDriveItems.Contains(item)).Subscribe(RemoveAetherDrive);
    }

    private void RemoveAetherDrive(EquippedItem item)
    {
        _aetherDriveItems.Remove(item);
        _aetherDrives.Remove(item.GetBehavior<AetherDrive>());
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

    private void RecalculateThrust()
    {
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

        foreach (var drive in _aetherDrives)
            if (drive.Item.Active.Value)
                ForwardThrust += drive.Thrust.x;
    }

    private void RecalculateReverseThrust()
    {
        ReverseThrust = 0;
        foreach (var thruster in _reverseThrusters)
            if (thruster.Item.Active.Value)
                ReverseThrust += thruster.Thrust;

        foreach (var drive in _aetherDrives)
            if (drive.Item.Active.Value)
                ReverseThrust += drive.Thrust.x;
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

        foreach (var drive in _aetherDrives)
            if (drive.Item.Active.Value)
                LeftStrafeThrust += drive.Thrust.y;
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

        foreach (var drive in _aetherDrives)
            if (drive.Item.Active.Value)
                RightStrafeThrust += drive.Thrust.y;
    }

    private void RecalculateClockwiseTorque()
    {
        ClockwiseTorque = 0;
        foreach (var thruster in _clockwiseThrusters)
            if (thruster.Item.Active.Value)
                ClockwiseTorque += thruster.Torque;

        foreach (var drive in _aetherDrives)
            if (drive.Item.Active.Value)
                ClockwiseTorque += drive.Thrust.z;
    }

    private void RecalculateCounterClockwiseTorque()
    {
        CounterClockwiseTorque = 0;
        foreach (var thruster in _counterClockwiseThrusters)
            if (thruster.Item.Active.Value)
                CounterClockwiseTorque -= thruster.Torque;

        foreach (var drive in _aetherDrives)
            if (drive.Item.Active.Value)
                CounterClockwiseTorque += drive.Thrust.z;
    }

    #endregion

    public override void Update(float delta)
    {
        if (_active && !_exitingWormhole && !_enteringWormhole)
        {
            RecalculateThrust();
            foreach (var thruster in _allThrusters) thruster.Axis = 0;
            var movementDirection = MovementDirection;
            var rightThrusterTorqueCompensation = abs(RightStrafeTotalTorque) / RightStrafeTorqueThrusters.Count;
            foreach (var thruster in _rightThrusters)
            {
                var thrust = 0f;
                thrust += movementDirection.x;
                if (RightStrafeTorqueThrusters.Contains(thruster))
                    thrust -= movementDirection.x * (rightThrusterTorqueCompensation / (abs(thruster.Torque) * thruster.Thrust));
                thruster.Axis = thrust;
            }
            var leftThrusterTorqueCompensation = abs(LeftStrafeTotalTorque) / LeftStrafeTorqueThrusters.Count;
            foreach (var thruster in _leftThrusters)
            {
                var thrust = 0f;
                thrust += -movementDirection.x;
                if (LeftStrafeTorqueThrusters.Contains(thruster))
                    thrust += movementDirection.x * (leftThrusterTorqueCompensation / (abs(thruster.Torque) * thruster.Thrust));
                thruster.Axis = thrust;
            }
            foreach (var thruster in _forwardThrusters) thruster.Axis += movementDirection.y;
            foreach (var thruster in _reverseThrusters) thruster.Axis += -movementDirection.y;

            var look = normalize(CultLookDirectionXZ);
            var shipDirection = normalize(CultDirection);
            var deltaRot = dot(look, AetheriaMath.Rotate(shipDirection, ItemRotation.Clockwise));
            if (abs(deltaRot) < .01f)
            {
                deltaRot = 0;
                CultDirection = lerp(shipDirection, look, min(delta, 1));
            }
            deltaRot = pow(abs(deltaRot), .5f) * sign(deltaRot);

            foreach (var thruster in _clockwiseThrusters) thruster.Axis += deltaRot;
            foreach (var thruster in _counterClockwiseThrusters) thruster.Axis += -deltaRot;

            foreach (var drive in _aetherDrives)
                drive.Axis = new cfloat3(movementDirection.y, movementDirection.x, deltaRot);
        }

        var velocity = CultVelocity;
        var velocityMagnitude = length(velocity);
        if(velocityMagnitude > .01f)
            CultVelocity = normalize(velocity) * AetheriaMath.Decay(velocityMagnitude, (float)(ItemManager.GetRuntimeItem(Hull)?.HullDrag ?? 0), delta);

        CultPositionXZ += CultVelocity * delta;

        var normal = Zone.GetNormal(CultPositionXZ);
        var force = new cfloat2(normal.x, normal.z);
        var forceMagnitude = lengthsq(force);
        if (forceMagnitude > .001f)
        {
            var fa = 1 / (1 - forceMagnitude) - 1;
            CultVelocity += normalize(force) * Zone.Settings.GravityStrength * fa;
        }
        var shipRight = AetheriaMath.Rotate(CultDirection, ItemRotation.Clockwise);
        var forward = cross(new cfloat3(shipRight.x, 0, shipRight.y), normal);
        Rotation = LookRotation(forward, normal);

        base.Update(delta);

        if (_exitingWormhole)
        {
            var inverseDirection = new cfloat3(-CultDirection.x, 0, -CultDirection.y);
            _wormholeAnimationProgress += delta / ItemManager.GameplaySettings.WormholeAnimationDuration;
            if(_wormholeAnimationProgress < 1)
            {
                if (_wormholeAnimationProgress < ItemManager.GameplaySettings.WormholeExitCurveStart)
                {
                    CultPositionXZ = _wormholePosition;
                    Rotation = LookRotation(new cfloat3(0, 1, 0), inverseDirection);
                }
                else
                {
                    var exitLerp = (_wormholeAnimationProgress - ItemManager.GameplaySettings.WormholeExitCurveStart) /
                                   (1 - ItemManager.GameplaySettings.WormholeExitCurveStart);
                    exitLerp = AetheriaMath.Smootherstep(exitLerp); // Square the interpolation variable to produce curve with zero slope at start
                    CultPositionXZ = _wormholePosition + normalize(_wormholeExitVelocity) * exitLerp * ItemManager.GameplaySettings.WormholeExitRadius;
                    Rotation = LookRotation(
                        lerp(new cfloat3(0, 1, 0), forward, exitLerp),
                        lerp(inverseDirection, normal, exitLerp));
                }

                CultPositionY -= lerp(ItemManager.GameplaySettings.WormholeDepth, 0, _wormholeAnimationProgress);
            }
            else
            {
                _exitingWormhole = false;
                OnExitedWormhole?.Invoke();
                OnExitedWormhole = null;
                CultVelocity = _wormholeExitVelocity;
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
                    enterLerp = AetheriaMath.Smootherstep(enterLerp); // Square the interpolation variable to produce curve with zero slope at vertical
                    CultPositionXZ = lerp(_wormholeEntryPosition, _wormholePosition, enterLerp);
                    Rotation = LookRotation(
                        lerp(forward, new cfloat3(0, -1, 0), enterLerp),
                        lerp(normal, new cfloat3(-_wormholeEntryDirection.x, 0, -_wormholeEntryDirection.y), enterLerp));
                }
                else
                {
                    CultPositionXZ = _wormholePosition;
                    Rotation = LookRotation(new cfloat3(0, -1, 0),
                        new cfloat3(-_wormholeEntryDirection.x, 0, -_wormholeEntryDirection.y));
                }

                CultPositionY -= lerp(0, ItemManager.GameplaySettings.WormholeDepth, _wormholeAnimationProgress);
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


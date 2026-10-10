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

    // What this ship can do at this instant (see ManoeuvreEnvelope): the sum of what each live propulsor reports with
    // its own Execute arithmetic; thrusters are its only propulsors. Derived each update, never saved.
    public ManoeuvreEnvelope Envelope { get; private set; }

    private void RecalculateEnvelope()
    {
        var envelope = default(ManoeuvreEnvelope);
        foreach (var thruster in _allThrusters) envelope += thruster.Manoeuvre();
        Envelope = envelope;
    }

    #endregion

    public override void Update(float delta)
    {
        if (_active && !_exitingWormhole && !_enteringWormhole)
        {
            RecalculateEnvelope();
            // The lock replaces intent here, once; player and agent intent both pass through this one read.
            var move = ThrottleLocked ? float2(0, 1) : MovementDirection;
            for (var i = 0; i < _allThrusters.Length; i++)
                _columns[i] = _allThrusters[i].Column(_allThrusters[i].NominalThrust);
            _allocator.Allocate(_columns, move, Turn, _throttles);
            for (var i = 0; i < _allThrusters.Length; i++) _allThrusters[i].Axis = _throttles[i];
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


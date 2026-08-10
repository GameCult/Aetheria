/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;

public class Agent
{
    private const float FORWARD_DELTA_THRESHOLD = 20;
    private const float THRUST_DELTA_THRESHOLD = 1;
    protected BaseState _rootState;
    private BaseState _currentState;
    private VelocityLimit _velocityLimit;
    
    public AgentTask Task { get; set; }
    public Ship Ship { get; }
    public ItemManager ItemManager { get; }
    public GameplaySettings Settings { get; }
    public float TopSpeed => _velocityLimit?.Limit ?? 100;

    public Agent(Ship ship)
    {
        Ship = ship;
        ItemManager = Ship.ItemManager;
        Settings = Ship.ItemManager.GameplaySettings;

        _rootState = _currentState = new BaseState(this);
        _velocityLimit = Ship.GetBehavior<VelocityLimit>();
    }

    public void Transition(BaseState targetState, Action onTransition = null)
    {
        //ItemManager.Log($"{Ship.Name} changing state from {_currentState.GetType().Name} to {targetState.GetType().Name}");
        _currentState.OnExitState();
        _currentState = targetState;
        onTransition?.Invoke();
        _currentState.OnEnterState();
    }

    public void Update(float delta)
    {
        _currentState.Update(delta);
        foreach (var transition in _currentState.Transitions)
            if (transition.Condition())
            {
                Transition(transition.TargetState, transition.OnTransition);
                break;
            }
    }

    public void Accelerate(float2 targetVelocity, bool noTurn = false)
    {
        var deltaV = targetVelocity - Ship.Velocity;
        var deltaVMag = length(deltaV);
        var deltaVDirection = normalize(deltaV);
        // If Delta V is above the threshold, direct the ship towards the delta and use only main thrusters
        if (!noTurn && deltaVMag > FORWARD_DELTA_THRESHOLD)
        {
            Ship.LookDirection = float3(deltaVDirection.x, 0, deltaVDirection.y);
            Ship.MovementDirection = float2(0, pow(dot(Ship.Direction, deltaVDirection), 2));
        }
        // If Delta V is low, direct the ship towards the target and use all thrusters
        else if(deltaVMag > THRUST_DELTA_THRESHOLD)
        {
            var right = Ship.Direction.Rotate(ItemRotation.Clockwise);
            Ship.MovementDirection = float2(dot(right, deltaVDirection), dot(Ship.Direction, deltaVDirection));
        }
        else
        {
            Ship.MovementDirection = float2.zero;
        }
    }
}

public class StateTransition
{
    public StateTransition(BaseState targetState, Func<bool> condition, Action onTransition)
    {
        TargetState = targetState;
        Condition = condition;
        OnTransition = onTransition;
    }

    public BaseState TargetState { get; }
    public Func<bool> Condition { get; }
    public Action OnTransition { get; }
}


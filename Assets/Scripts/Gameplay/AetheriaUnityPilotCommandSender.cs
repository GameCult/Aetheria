/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using GameCult.Aetheria.State.Verse;
using UnityEngine;

public sealed class AetheriaUnityPilotCommandSender
{
    private const float DaemonMoveCommandIntervalSeconds = 0.05f;
    private const float DaemonMoveCommandChangeThreshold = 0.001f;
    private const float DaemonLookCommandIntervalSeconds = 0.02f;
    private const float DaemonLookCommandChangeThreshold = 0.0001f;
    private const float DaemonTractorCommandIntervalSeconds = 0.05f;
    private const float DaemonTractorCommandChangeThreshold = 0.001f;

    private readonly Func<AetheriaControl> _resolveControl;
    private readonly Func<float> _time;
    private Vector2 _lastSentDaemonMoveVector;
    private Vector3 _lastSentDaemonLookDirection;
    private float _lastSentDaemonTractorPower;
    private float _nextDaemonMoveCommandTime;
    private float _nextDaemonLookCommandTime;
    private float _nextDaemonTractorCommandTime;
    private bool _hasSentDaemonMoveVector;
    private bool _hasSentDaemonLookDirection;
    private bool _hasSentDaemonTractorPower;

    public AetheriaUnityPilotCommandSender(
        Func<AetheriaControl> resolveControl,
        Func<float> time)
    {
        _resolveControl = resolveControl ?? throw new ArgumentNullException(nameof(resolveControl));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public void RequestMoveVector(Vector2 movement)
    {
        var changed = !_hasSentDaemonMoveVector ||
                      (movement - _lastSentDaemonMoveVector).sqrMagnitude >
                      DaemonMoveCommandChangeThreshold * DaemonMoveCommandChangeThreshold;
        if (!changed && _time() < _nextDaemonMoveCommandTime)
        {
            return;
        }

        if (TrySubmit(
                operations => operations.SetMoveVector(movement.x, movement.y, movement.magnitude),
                "movement"))
        {
            _lastSentDaemonMoveVector = movement;
            _hasSentDaemonMoveVector = true;
            _nextDaemonMoveCommandTime = _time() + DaemonMoveCommandIntervalSeconds;
        }
    }

    public void RequestLookDirection(Vector3 lookDirection)
    {
        var changed = !_hasSentDaemonLookDirection ||
                      (lookDirection - _lastSentDaemonLookDirection).sqrMagnitude >
                      DaemonLookCommandChangeThreshold * DaemonLookCommandChangeThreshold;
        if (!changed && _time() < _nextDaemonLookCommandTime)
        {
            return;
        }

        if (TrySubmit(
                operations => operations.SetLookDirection(lookDirection.x, lookDirection.y, lookDirection.z),
                "look"))
        {
            _lastSentDaemonLookDirection = lookDirection;
            _hasSentDaemonLookDirection = true;
            _nextDaemonLookCommandTime = _time() + DaemonLookCommandIntervalSeconds;
        }
    }

    public void RequestTractorPower(float power)
    {
        var changed = !_hasSentDaemonTractorPower ||
                      Mathf.Abs(power - _lastSentDaemonTractorPower) >
                      DaemonTractorCommandChangeThreshold;
        if (!changed && _time() < _nextDaemonTractorCommandTime)
        {
            return;
        }

        if (TrySubmit(
                operations => operations.SetTractorPower(power),
                "tractor-power"))
        {
            _lastSentDaemonTractorPower = power;
            _hasSentDaemonTractorPower = true;
            _nextDaemonTractorCommandTime = _time() + DaemonTractorCommandIntervalSeconds;
        }
    }

    public bool TrySubmit(
        Action<AetheriaControl> submit,
        string label)
    {
        if (submit == null)
        {
            return false;
        }

        try
        {
            var control = _resolveControl();
            if (control == null)
                return false;

            submit(control);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to send Aetheria daemon pilot {label} operation; operation not submitted: {ex.Message}");
            return false;
        }
    }
}

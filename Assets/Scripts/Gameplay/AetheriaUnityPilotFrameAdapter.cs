/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.PostProcessing;
using static Unity.Mathematics.math;

public sealed class AetheriaUnityPilotFrameAdapter
{
    private float2 _entityYawPitch;
    private readonly Action<float3> _setViewDirection;

    public AetheriaUnityPilotFrameAdapter(Action<float3> setViewDirection)
    {
        _setViewDirection = setViewDirection ?? (_ => { });
    }

    public ZoneRenderer ZoneRenderer { get; set; }
    public AetheriaInput Input { get; set; }
    public AetheriaUnityTargetPresentation TargetPresentation { get; set; }
    public AetheriaUnityPilotCommandSender PilotCommands { get; set; }
    public PostProcessVolume HeatstrokePost { get; set; }
    public PostProcessVolume SevereHeatstrokePost { get; set; }
    public float2 Sensitivity { get; set; }

    public void Tick(Entity currentEntity, float deltaTime, float timeSeconds)
    {
        if (currentEntity == null ||
            Input == null ||
            PilotCommands == null)
        {
            return;
        }

        var renderSettings = ZoneRenderer?.RenderSettings;
        TargetPresentation?.Tick(currentEntity, timeSeconds);

        var look = Input.Player.Look.ReadValue<Vector2>();
        _entityYawPitch = new float2(
            _entityYawPitch.x + look.x * Sensitivity.x,
            Mathf.Clamp(_entityYawPitch.y + look.y * Sensitivity.y, -.45f * Mathf.PI, .45f * Mathf.PI));
        var viewDirection = (float3)(Quaternion.Euler(
            _entityYawPitch.y * Mathf.Rad2Deg,
            _entityYawPitch.x * Mathf.Rad2Deg,
            0) * Vector3.forward);
        _setViewDirection(viewDirection);
        PilotCommands.RequestLookDirection(viewDirection);

        if (renderSettings != null)
        {
            if (HeatstrokePost != null)
                HeatstrokePost.weight = (float)renderSettings.Value.NormalizeHeatstrokePost(currentEntity.Heatstroke);
            if (SevereHeatstrokePost != null)
                SevereHeatstrokePost.weight =
                    (float)renderSettings.Value.ResolveSevereHeatstrokePostWeight(currentEntity.Heatstroke, timeSeconds);
        }

        if (currentEntity is Ship)
        {
            var movement = Input.Player.Move.ReadValue<Vector2>();
            PilotCommands.RequestMoveVector(movement);
        }

        var tractorPowerInput = Input.Player.TractorBeam.ReadValue<float>();
        PilotCommands.RequestTractorPower(Saturate(
            currentEntity.TractorPower +
            Mathf.Sign(tractorPowerInput - currentEntity.TractorPower) * deltaTime * 2));
    }

    private static float Saturate(float value) => Mathf.Clamp01(value);
}

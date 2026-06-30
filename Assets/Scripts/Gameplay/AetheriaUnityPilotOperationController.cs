/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using GameCult.Aetheria.State.Verse;
using Unity.Mathematics;

public sealed class AetheriaUnityPilotOperationController
{
    private readonly Func<AetheriaUnityPilotCommandSender> _resolvePilotCommands;
    private readonly AetheriaUnityPresentationEntityIndex _presentationEntityIndex;
    private readonly Func<float3> _resolveViewDirection;
    private readonly Func<Entity> _resolveCurrentEntity;

    public AetheriaUnityPilotOperationController(
        Func<AetheriaUnityPilotCommandSender> resolvePilotCommands,
        AetheriaUnityPresentationEntityIndex presentationEntityIndex,
        Func<float3> resolveViewDirection,
        Func<Entity> resolveCurrentEntity)
    {
        _resolvePilotCommands = resolvePilotCommands ?? throw new ArgumentNullException(nameof(resolvePilotCommands));
        _presentationEntityIndex = presentationEntityIndex ?? throw new ArgumentNullException(nameof(presentationEntityIndex));
        _resolveViewDirection = resolveViewDirection ?? (() => default);
        _resolveCurrentEntity = resolveCurrentEntity ?? (() => null);
    }

    public void RequestInteract()
    {
        Submit(operations => operations.Interact(), "interact");
    }

    public void RequestTargetSelection(Entity target)
    {
        if (target == null)
        {
            Submit(operations => operations.ClearTarget(), "target clear");
            return;
        }

        if (!_presentationEntityIndex.TryGetRecordKeyForPresentationEntity(target, out var targetEntityKey) ||
            string.IsNullOrWhiteSpace(targetEntityKey))
        {
            return;
        }

        Submit(
            operations => operations.SetTarget(targetEntityKey),
            "target");
    }

    public void RequestTargetNearest()
    {
        Submit(operations => operations.TargetNearest(), "target nearest");
    }

    public void RequestTargetNext()
    {
        Submit(operations => operations.TargetNext(), "target next");
    }

    public void RequestTargetPrevious()
    {
        Submit(operations => operations.TargetPrevious(), "target previous");
    }

    public void RequestTargetReticle()
    {
        var lookDirection = _resolveViewDirection();
        Submit(
            operations => operations.TargetReticle(lookDirection.x, lookDirection.y, lookDirection.z),
            "reticle target");
    }

    public void RequestOverrideShutdown(bool enabled)
    {
        Submit(operations => operations.SetOverrideShutdown(enabled), "override-shutdown");
    }

    public void RequestSensorPing()
    {
        Submit(operations => operations.SensorPing(), "sensor-ping");
    }

    public void RequestHeatsinksEnabled(bool enabled)
    {
        Submit(operations => operations.SetHeatsinksEnabled(enabled), "heatsinks");
    }

    public void RequestShieldToggle()
    {
        Submit(operations => operations.ToggleShieldEnabled(), "shield enablement");
    }

    public void RequestDock()
    {
        Submit(operations => operations.DockNearest(), "dock");
    }

    public void RequestUndock()
    {
        if (_resolveCurrentEntity() == null)
            return;

        Submit(operations => operations.Undock(), "undock");
    }

    private void Submit(
        Action<AetheriaControl> submit,
        string label)
    {
        _resolvePilotCommands()?.TrySubmit(submit, label);
    }
}

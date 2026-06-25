/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;

public sealed class AetheriaUnityGameplayLoopShell
{
    public bool Paused { get; set; }
    public Func<Entity> ResolveCurrentEntity { get; set; }
    public Func<bool> IsCurrentEntityUndocked { get; set; }
    public Action ApplyLatestZoneRender { get; set; }
    public AetheriaUnityCurrentEntityPresentation CurrentEntityPresentation { get; set; }
    public AetheriaUnityPilotFrameAdapter PilotFrameAdapter { get; set; }
    public AetheriaUnityTargetPresentation TargetPresentation { get; set; }

    public void Tick(float deltaTime, float time)
    {
        if (Paused)
            return;

        ApplyLatestZoneRender?.Invoke();
        CurrentEntityPresentation?.Tick(deltaTime);
        if (IsCurrentEntityUndocked?.Invoke() == true)
            PilotFrameAdapter?.Tick(ResolveCurrentEntity?.Invoke(), deltaTime, time);
    }

    public void LateTick()
    {
        TargetPresentation?.UpdateTargetIndicators(
            ResolveCurrentEntity?.Invoke(),
            IsCurrentEntityUndocked?.Invoke() == true,
            CurrentEntityPresentation);
    }
}

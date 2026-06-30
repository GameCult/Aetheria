/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using UnityEngine;
using UnityEngine.Rendering;
using float3 = Unity.Mathematics.float3;

public sealed class AetheriaUnityCurrentEntityBinder
{
    public ZoneRenderer ZoneRenderer { get; set; }
    public Volume DeathPost { get; set; }
    public CanvasGroup GameplayUI { get; set; }
    public AetheriaUnityCurrentEntityPresentation CurrentEntityPresentation { get; set; }
    public AetheriaUnityTargetPresentation TargetPresentation { get; set; }
    public Func<Entity> GetCurrentEntity { get; set; }
    public Func<Entity, Entity> ResolveDockParent { get; set; }
    public Action<Entity> SetCurrentEntity { get; set; }
    public Func<float3> GetViewDirection { get; set; }
    public Action<float3> SetViewDirection { get; set; }
    public Func<AetheriaRuntimeZoneRenderDocument> ResolveZoneRender { get; set; }
    public Action<IReadOnlyList<AetheriaUnityActionBarBinding>> ApplyActionBarBindings { get; set; }
    public Action EnablePlayerInput { get; set; }
    public Action DisablePlayerInput { get; set; }
    public Action<MusicType> PlayMusic { get; set; }
    public Action<Entity> UpdateTargetPanel { get; set; }

    public void RestoreBinding(Entity currentEntity)
    {
        var dockParent = ResolveDockParent?.Invoke(currentEntity);
        if (dockParent != null)
        {
            SetCurrentEntity?.Invoke(currentEntity);
            ApplyActionBarBindings?.Invoke(Array.Empty<AetheriaUnityActionBarBinding>());
            CurrentEntityPresentation?.BindDocked(
                dockParent,
                null,
                ResolveZoneRender?.Invoke());
            return;
        }

        BindUndocked(
            currentEntity);
    }

    public void ClearBinding()
    {
        TargetPresentation?.ClearIndicators();
        CurrentEntityPresentation?.ClearBinding();
        DisablePlayerInput?.Invoke();
        Cursor.lockState = CursorLockMode.None;
        if (GameplayUI != null)
            GameplayUI.gameObject.SetActive(false);
    }

    private void BindUndocked(Entity entity)
    {
        if (ZoneRenderer == null ||
            !ZoneRenderer.TryGetEntityInstance(entity, out var entityInstance))
        {
            Debug.LogError($"Attempted to bind to entity {entity?.Name}, but ZoneRenderer has no daemon-indexed instance.");
            return;
        }

        SetCurrentEntity?.Invoke(entity);
        if (DeathPost != null)
            DeathPost.weight = 0;

        if (CultMath.math.length(entity.CultDirection) > .1f)
            SetViewDirection?.Invoke((float3)AetheriaMath.ToUnityXZ(entity.CultDirection));

        CurrentEntityPresentation?.BindUndocked(
            entity,
            entityInstance,
            Array.Empty<AetheriaUnityActionBarBinding>(),
            EnablePlayerInput,
            PlayMusic,
            SetCurrentEntity,
            UpdateTargetPanel,
            () => TargetPresentation?.ReconcileVisibleTargetIndicators(GetCurrentEntity?.Invoke()),
            ApplyActionBarBindings);
    }
}

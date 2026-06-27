/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.State.Verse;

public sealed class AetheriaUnityObservedFrameApplier
{
    private readonly Func<AetheriaDaemonObserver> _resolveObserver;
    private readonly Func<int, GalaxyZone> _resolveObservedZone;
    private readonly Func<Zone> _getZone;
    private readonly Action<Zone> _setZone;
    private readonly AetheriaUnityObservedFacadeIndex _facadeIndex;
    private readonly AetheriaUnityObservedFacadeProjector _facadeProjector;
    private readonly AetheriaUnityObservedZoneContextProjector _zoneContextProjector;
    private readonly Func<ZoneRenderer> _resolveZoneRenderer;
    private readonly Func<Entity> _getCurrentEntity;
    private readonly Action<Entity> _restoreCurrentEntityBinding;
    private readonly Action<string> _logWarning;

    private long _lastAppliedZoneRenderFrameId = -1;
    private string _lastAppliedZoneRenderRunId = "";
    private int _lastAppliedZoneRenderZoneIndex = -1;
    private AetheriaRuntimeZoneRenderDocument _lastZoneRender;

    public AetheriaUnityObservedFrameApplier(
        Func<AetheriaDaemonObserver> resolveObserver,
        Func<int, GalaxyZone> resolveObservedZone,
        Func<Zone> getZone,
        Action<Zone> setZone,
        AetheriaUnityObservedFacadeIndex facadeIndex,
        AetheriaUnityObservedFacadeProjector facadeProjector,
        AetheriaUnityObservedZoneContextProjector zoneContextProjector,
        Func<ZoneRenderer> resolveZoneRenderer,
        Func<Entity> getCurrentEntity,
        Action<Entity> restoreCurrentEntityBinding,
        Action<string> logWarning)
    {
        _resolveObserver = resolveObserver ?? (() => null);
        _resolveObservedZone = resolveObservedZone ?? (_ => null);
        _getZone = getZone ?? (() => null);
        _setZone = setZone ?? (_ => { });
        _facadeIndex = facadeIndex ?? throw new ArgumentNullException(nameof(facadeIndex));
        _facadeProjector = facadeProjector ?? throw new ArgumentNullException(nameof(facadeProjector));
        _zoneContextProjector = zoneContextProjector ?? throw new ArgumentNullException(nameof(zoneContextProjector));
        _resolveZoneRenderer = resolveZoneRenderer ?? (() => null);
        _getCurrentEntity = getCurrentEntity ?? (() => null);
        _restoreCurrentEntityBinding = restoreCurrentEntityBinding ?? (_ => { });
        _logWarning = logWarning ?? (_ => { });
    }

    public bool ApplyLatestZoneRender()
    {
        var observer = _resolveObserver();
        if (observer == null || !observer.HasAuthoritativeState)
        {
            return false;
        }

        var render = ReadLatestZoneRender(observer);
        if (render == null)
        {
            return false;
        }

        return TryRestoreEntityGraphFromZoneRender(render);
    }

    private AetheriaRuntimeZoneRenderDocument ReadLatestZoneRender(AetheriaDaemonObserver observer)
    {
        try
        {
            return observer.Client
                .Aetheria()
                .ZoneRender
                .LatestAsync()
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            _logWarning($"Failed to read Aetheria zone-render feed from local Verse state: {ex.Message}");
            return null;
        }
    }

    private bool TryRestoreEntityGraphFromZoneRender(AetheriaRuntimeZoneRenderDocument render)
    {
        if (render == null || render.ZoneIndex < 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(render.RunId))
        {
            _logWarning("Aetheria zone-render feed does not identify a run id.");
            return false;
        }

        var runId = render.RunId;
        var currentEntityKey = string.IsNullOrWhiteSpace(render.CurrentEntityKey) ? "" : render.CurrentEntityKey;
        if (string.IsNullOrWhiteSpace(currentEntityKey))
        {
            _logWarning($"Aetheria zone-render feed for run {runId} does not identify a current entity.");
            return false;
        }

        var entitySnapshots = AetheriaUnityDaemonEntitySnapshotProjector.CreateSnapshots(runId, render.ZoneIndex, render.EntityFacades)
            .OrderBy(entity => AetheriaUnityDaemonEntitySnapshotProjector.EntityIndexFromRecordKey(entity.RecordKey))
            .ToArray();

        if (entitySnapshots.Length == 0)
        {
            _logWarning($"Aetheria zone-render feed has no entity facades for zone {render.ZoneIndex}.");
            return false;
        }

        var targetZone = _resolveObservedZone(render.ZoneIndex);
        if (targetZone == null)
        {
            _logWarning($"Aetheria zone-render feed references missing zone index {render.ZoneIndex}.");
            return false;
        }

        if (render.FrameId == _lastAppliedZoneRenderFrameId &&
            string.Equals(render.RunId, _lastAppliedZoneRenderRunId, StringComparison.Ordinal) &&
            render.ZoneIndex == _lastAppliedZoneRenderZoneIndex)
        {
            return false;
        }

        var zoneRenderer = _resolveZoneRenderer();
        if (_facadeProjector.TryApplyInPlace(
                _lastAppliedZoneRenderRunId,
                _lastAppliedZoneRenderZoneIndex,
                runId,
                render.ZoneIndex,
                entitySnapshots,
                currentEntityKey,
                _getCurrentEntity(),
                out var reboundCurrentEntity))
        {
            if (reboundCurrentEntity != null)
                _restoreCurrentEntityBinding(reboundCurrentEntity);
            zoneRenderer?.ApplyZoneRender(render);
            zoneRenderer?.RestoreDroppedPickupsFromZoneRender(render);
            _lastAppliedZoneRenderFrameId = render.FrameId;
            _lastZoneRender = render;
            return true;
        }

        if (_getZone()?.GalaxyZone != targetZone)
        {
            var resolvedZone = _zoneContextProjector.ResolveContext(targetZone, render);
            if (resolvedZone == null)
                return false;

            _setZone(resolvedZone);
        }

        _facadeProjector.Replace(entitySnapshots, currentEntityKey, _getZone());
        zoneRenderer?.LoadDaemonZoneView(_facadeIndex.EntitiesByDaemonIndex, render);
        if (_facadeIndex.TryResolveEntityByRecordKey(currentEntityKey, out var currentEntity))
            _restoreCurrentEntityBinding(currentEntity);
        zoneRenderer?.RestoreDroppedPickupsFromZoneRender(render);
        _lastAppliedZoneRenderFrameId = render.FrameId;
        _lastAppliedZoneRenderRunId = runId;
        _lastAppliedZoneRenderZoneIndex = render.ZoneIndex;
        _lastZoneRender = render;
        return true;
    }
}

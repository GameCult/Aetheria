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
    private readonly AetheriaUnityObservedEntityIndex _entityIndex;
    private readonly AetheriaUnityObservedEntityRestorer _entityRestorer;
    private readonly AetheriaUnityObservedZoneContextFactory _zoneContextFactory;
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
        AetheriaUnityObservedEntityIndex entityIndex,
        AetheriaUnityObservedEntityRestorer entityRestorer,
        AetheriaUnityObservedZoneContextFactory zoneContextFactory,
        Func<ZoneRenderer> resolveZoneRenderer,
        Func<Entity> getCurrentEntity,
        Action<Entity> restoreCurrentEntityBinding,
        Action<string> logWarning)
    {
        _resolveObserver = resolveObserver ?? (() => null);
        _resolveObservedZone = resolveObservedZone ?? (_ => null);
        _getZone = getZone ?? (() => null);
        _setZone = setZone ?? (_ => { });
        _entityIndex = entityIndex ?? throw new ArgumentNullException(nameof(entityIndex));
        _entityRestorer = entityRestorer ?? throw new ArgumentNullException(nameof(entityRestorer));
        _zoneContextFactory = zoneContextFactory ?? throw new ArgumentNullException(nameof(zoneContextFactory));
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

        var render = observer.LastRenderView?.ZoneRender;
        if (render == null)
        {
            return false;
        }

        return TryRestoreEntityGraphFromZoneRender(render);
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

        var entitySnapshots = render.CreateEntitySnapshots()
            .OrderBy(entity => entity.EntityIndex)
            .ToArray();

        if (entitySnapshots.Length == 0)
        {
            _logWarning($"Aetheria zone-render feed has no entity snapshots for zone {render.ZoneIndex}.");
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
        if (_entityRestorer.TryApplyInPlace(
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
            _lastAppliedZoneRenderRunId = runId;
            _lastAppliedZoneRenderZoneIndex = render.ZoneIndex;
            _lastZoneRender = render;
            return true;
        }

        if (_getZone()?.GalaxyZone != targetZone)
        {
            var resolvedZone = _zoneContextFactory.ResolveContext(targetZone, render);
            if (resolvedZone == null)
                return false;

            _setZone(resolvedZone);
        }

        _entityRestorer.Replace(entitySnapshots, currentEntityKey, _getZone());
        zoneRenderer?.LoadDaemonZoneView(_entityIndex.EntitiesByDaemonIndex, render);
        if (_entityIndex.TryResolveEntityByRecordKey(currentEntityKey, out var currentEntity))
            _restoreCurrentEntityBinding(currentEntity);
        zoneRenderer?.RestoreDroppedPickupsFromZoneRender(render);
        _lastAppliedZoneRenderFrameId = render.FrameId;
        _lastAppliedZoneRenderRunId = runId;
        _lastAppliedZoneRenderZoneIndex = render.ZoneIndex;
        _lastZoneRender = render;
        return true;
    }
}

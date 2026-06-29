/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;

public sealed class AetheriaUnityObservedDockingIndex : IDisposable
{
    private readonly Func<AetheriaClient> _resolveClient;
    private readonly AetheriaUnityObservedEntityIndex _observedEntityIndex;
    private CultMeshReactiveDocument<AetheriaRuntimeCurrentEntityDocument> _currentEntity;
    private CultMeshReactiveDocument<AetheriaRuntimeCurrentDockingDocument> _currentDocking;
    private CultMeshReactiveDocument<AetheriaRuntimeStationRefitDocument> _stationRefit;

    public AetheriaUnityObservedDockingIndex(
        Func<AetheriaClient> resolveClient,
        AetheriaUnityObservedEntityIndex observedEntityIndex)
    {
        _resolveClient = resolveClient ?? throw new ArgumentNullException(nameof(resolveClient));
        _observedEntityIndex = observedEntityIndex ?? throw new ArgumentNullException(nameof(observedEntityIndex));
    }

    public int ResolveEntityZoneIndex(Entity entity)
    {
        return TryResolveCurrentDocking(entity, out var docking)
            ? docking.ZoneIndex
            : -1;
    }

    public bool IsEntityUndocked(Entity entity)
    {
        return TryResolveCurrentDocking(entity, out var docking) &&
               !docking.IsDocked;
    }

    public bool TryResolveCurrentEntity(out Entity entity)
    {
        entity = null;
        return TryResolveCurrentEntityKey(out var currentEntityKey) &&
               _observedEntityIndex.TryResolveEntityByRecordKey(currentEntityKey, out entity);
    }

    public bool TryResolveCurrentEntityKey(out string currentEntityKey)
    {
        currentEntityKey = "";
        if (TryReadCurrentDockingDocuments(out var currentEntity, out var docking, out _))
            currentEntityKey = CurrentEntityKey(currentEntity, docking);
        return !string.IsNullOrWhiteSpace(currentEntityKey);
    }

    public bool TryResolveCurrentEntityDocument(out AetheriaRuntimeCurrentEntityDocument currentEntity)
    {
        currentEntity = null;
        if (!TryReadCurrentDockingDocuments(out var document, out _, out _))
            return false;

        currentEntity = document;
        return currentEntity != null;
    }

    public AetheriaRuntimeStationRefitDocument ResolveStationRefit()
    {
        return TryReadCurrentDockingDocuments(out _, out _, out var refit)
            ? refit
            : null;
    }

    public bool TryResolveCurrentDockingBayRow(out AetheriaRuntimeStationDockingBayRow dockingBay)
    {
        dockingBay = null;
        if (!TryReadCurrentDockingDocuments(out _, out _, out var refit) ||
            !refit.IsDocked ||
            refit.DockingBayIndex < 0)
            return false;

        dockingBay = (refit.DockingBays ?? Array.Empty<AetheriaRuntimeStationDockingBayRow>())
            .FirstOrDefault(row => row != null && row.DockingBayIndex == refit.DockingBayIndex);
        return dockingBay != null;
    }

    public bool TryResolveCurrentDockingBay(out EquippedDockingBay dockingBay)
    {
        dockingBay = null;
        var refit = ResolveStationRefit();
        if (!TryResolveCurrentDockingBayRow(out _) ||
            string.IsNullOrWhiteSpace(refit?.DockParentEntityKey) ||
            refit.DockingBayIndex < 0 ||
            !_observedEntityIndex.TryResolveDockingBayByRecordKey(
                refit.DockParentEntityKey,
                refit.DockingBayIndex,
                out dockingBay))
        {
            return false;
        }

        return dockingBay != null;
    }

    public bool TryResolveCurrentDocking(out AetheriaRuntimeCurrentDockingDocument docking)
    {
        docking = null;
        if (!TryReadCurrentDockingDocuments(out _, out var currentDocking, out _))
            return false;

        docking = currentDocking;
        return docking != null;
    }

    public bool TryResolveDockingBay(
        Entity child,
        out Entity dockParent,
        out EquippedDockingBay dockingBay)
    {
        return TryResolveDockingBay(child, out dockParent, out dockingBay, out _);
    }

    public bool TryResolveDockingBay(
        Entity child,
        out Entity dockParent,
        out EquippedDockingBay dockingBay,
        out AetheriaRuntimeCurrentDockingDocument docking)
    {
        dockParent = null;
        dockingBay = null;
        docking = null;
        if (!TryResolveCurrentDocking(child, out docking) || !docking.IsDocked)
            return false;

        if (!_observedEntityIndex.TryResolveEntityByRecordKey(docking.DockParentEntityKey, out var parent) ||
            !(parent is OrbitalEntity))
        {
            return false;
        }

        if (parent.DockingBays == null ||
            docking.DockingBayIndex < 0 ||
            docking.DockingBayIndex >= parent.DockingBays.Count)
        {
            return false;
        }

        dockParent = parent;
        dockingBay = parent.DockingBays[docking.DockingBayIndex];
        return dockingBay != null;
    }

    private bool TryResolveCurrentDocking(
        Entity entity,
        out AetheriaRuntimeCurrentDockingDocument docking)
    {
        TryResolveCurrentDocking(out docking);
        if (entity == null ||
            docking == null ||
            docking.CurrentEntityIndex != entity.DaemonEntityIndex)
        {
            return false;
        }

        if (_observedEntityIndex.TryResolveEntityRecordKey(entity, out var entityKey) &&
            !string.IsNullOrWhiteSpace(docking.CurrentEntityKey) &&
            !string.Equals(docking.CurrentEntityKey, entityKey, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private bool TryReadCurrentDockingDocuments(
        out AetheriaRuntimeCurrentEntityDocument entity,
        out AetheriaRuntimeCurrentDockingDocument docking,
        out AetheriaRuntimeStationRefitDocument refit)
    {
        entity = null;
        docking = null;
        refit = null;
        try
        {
            EnsureDockingDocuments();
            docking = _currentDocking?.Current;
            refit = _stationRefit?.Current;
            if (docking == null || refit == null)
                return false;

            entity = _currentEntity?.Current;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string CurrentEntityKey(
        AetheriaRuntimeCurrentEntityDocument entity,
        AetheriaRuntimeCurrentDockingDocument docking)
    {
        return !string.IsNullOrWhiteSpace(entity?.EntityKey)
            ? entity.EntityKey
            : docking?.CurrentEntityKey ?? "";
    }

    private void EnsureDockingDocuments()
    {
        if (_currentEntity != null && _currentDocking != null && _stationRefit != null)
            return;

        var state = _resolveClient()?.State;
        if (state == null)
            return;

        _currentEntity ??= state.Document<AetheriaRuntimeCurrentEntityDocument>().Reactive();
        _currentDocking ??= state.Document<AetheriaRuntimeCurrentDockingDocument>().Reactive();
        _stationRefit ??= state.Document<AetheriaRuntimeStationRefitDocument>().Reactive();
    }

    public void Dispose()
    {
        _currentEntity?.Dispose();
        _currentEntity = null;
        _currentDocking?.Dispose();
        _currentDocking = null;
        _stationRefit?.Dispose();
        _stationRefit = null;
    }
}

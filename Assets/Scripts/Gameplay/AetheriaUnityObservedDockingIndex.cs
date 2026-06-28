/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using GameCult.Aetheria.State.Verse;

public sealed class AetheriaUnityObservedDockingIndex : IDisposable
{
    private readonly Func<AetheriaClient> _resolveClient;
    private readonly AetheriaUnityObservedEntityIndex _observedEntityIndex;

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
        if (TryResolveCurrentDocking(out var docking))
            currentEntityKey = docking.CurrentEntityKey;
        return !string.IsNullOrWhiteSpace(currentEntityKey);
    }

    public bool TryResolveCurrentEntityDocument(out AetheriaRuntimeCurrentEntityDocument currentEntity)
    {
        currentEntity = null;
        if (!TryResolveCurrentDocking(out var docking))
            return false;

        currentEntity = docking.Entity;
        return currentEntity != null;
    }

    public AetheriaRuntimeStationRefitDocument ResolveStationRefit()
    {
        return TryResolveCurrentDocking(out var docking)
            ? docking.Refit
            : null;
    }

    public bool TryResolveCurrentDockingBayRow(out AetheriaRuntimeStationDockingBayRow dockingBay)
    {
        dockingBay = null;
        if (!TryResolveCurrentDocking(out var docking) ||
            !docking.TryResolveCurrentDockingBayRow(out var row))
            return false;

        dockingBay = row;
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
        if (!TryResolveCurrentDocking(out var currentDocking))
            return false;

        docking = currentDocking.Docking;
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

    private bool TryResolveCurrentDocking(out AetheriaRuntimeObservedDockingState docking)
    {
        docking = null;
        try
        {
            var state = _resolveClient()?.State;
            if (state == null)
                return false;

            var currentEntity = state.Current.LatestEntity();
            var currentDocking = state.Current.LatestDocking();
            var stationRefit = state.LatestStationRefit();
            if (currentDocking == null || stationRefit == null)
                return false;

            docking = new AetheriaRuntimeObservedDockingState(
                currentEntity,
                currentDocking,
                stationRefit);
            return docking != null;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
    }
}

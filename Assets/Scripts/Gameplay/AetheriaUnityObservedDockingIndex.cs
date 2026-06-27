/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using GameCult.Aetheria.State.Verse;

public sealed class AetheriaUnityObservedDockingIndex
{
    private readonly Func<AetheriaClient> _resolveClient;
    private readonly AetheriaUnityObservedFacadeIndex _observedFacadeIndex;

    public AetheriaUnityObservedDockingIndex(
        Func<AetheriaClient> resolveClient,
        AetheriaUnityObservedFacadeIndex observedFacadeIndex)
    {
        _resolveClient = resolveClient ?? throw new ArgumentNullException(nameof(resolveClient));
        _observedFacadeIndex = observedFacadeIndex ?? throw new ArgumentNullException(nameof(observedFacadeIndex));
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
               _observedFacadeIndex.TryResolveEntityByRecordKey(currentEntityKey, out entity);
    }

    public bool TryResolveCurrentEntityKey(out string currentEntityKey)
    {
        currentEntityKey = ReadCurrentEntity()?.EntityKey ?? "";
        return !string.IsNullOrWhiteSpace(currentEntityKey);
    }

    public bool TryResolveCurrentEntityDocument(out AetheriaRuntimeCurrentEntityDocument currentEntity)
    {
        currentEntity = ReadCurrentEntity();
        return currentEntity != null;
    }

    public AetheriaRuntimeStationRefitDocument ResolveStationRefit()
    {
        return ReadStationRefit();
    }

    public bool TryResolveCurrentDockingBayRow(out AetheriaRuntimeStationDockingBayRow dockingBay)
    {
        dockingBay = null;
        var stationRefit = ReadStationRefit();
        if (stationRefit?.IsDocked != true || stationRefit.DockingBayIndex < 0)
            return false;

        dockingBay = (stationRefit.DockingBays ?? Array.Empty<AetheriaRuntimeStationDockingBayRow>())
            .FirstOrDefault(row => row != null && row.DockingBayIndex == stationRefit.DockingBayIndex);
        return dockingBay != null;
    }

    public bool TryResolveCurrentDockingBay(out EquippedDockingBay dockingBay)
    {
        dockingBay = null;
        var stationRefit = ReadStationRefit();
        var dockParentEntityKey = stationRefit?.DockParentEntityKey ?? "";
        if (!TryResolveCurrentDockingBayRow(out var dockingBayRow) ||
            string.IsNullOrWhiteSpace(dockParentEntityKey) ||
            !_observedFacadeIndex.TryResolveDockingBayByRecordKey(
                dockParentEntityKey,
                dockingBayRow.DockingBayIndex,
                out dockingBay))
        {
            return false;
        }

        return dockingBay != null;
    }

    public bool TryResolveCurrentDocking(out AetheriaRuntimeCurrentDockingDocument docking)
    {
        docking = ReadCurrentDocking();
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

        if (!_observedFacadeIndex.TryResolveEntityByRecordKey(docking.DockParentEntityKey, out var parent) ||
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
        docking = ReadCurrentDocking();
        if (entity == null ||
            docking == null ||
            docking.CurrentEntityIndex != entity.DaemonEntityIndex)
        {
            return false;
        }

        if (_observedFacadeIndex.TryResolveEntityRecordKey(entity, out var entityKey) &&
            !string.IsNullOrWhiteSpace(docking.CurrentEntityKey) &&
            !string.Equals(docking.CurrentEntityKey, entityKey, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private AetheriaRuntimeCurrentDockingDocument ReadCurrentDocking()
    {
        try
        {
            return _resolveClient()
                ?.Aetheria()
                .Current
                .Docking
                .Latest();
        }
        catch
        {
            return null;
        }
    }

    private AetheriaRuntimeCurrentEntityDocument ReadCurrentEntity()
    {
        try
        {
            return _resolveClient()
                ?.Aetheria()
                .Current
                .Entity
                .Latest();
        }
        catch
        {
            return null;
        }
    }

    private AetheriaRuntimeStationRefitDocument ReadStationRefit()
    {
        try
        {
            return _resolveClient()
                ?.Aetheria()
                .StationRefit
                .Latest();
        }
        catch
        {
            return null;
        }
    }

}

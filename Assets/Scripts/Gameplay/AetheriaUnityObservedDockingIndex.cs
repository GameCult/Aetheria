/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using GameCult.Aetheria.State.Verse;

public sealed class AetheriaUnityObservedDockingIndex : IDisposable
{
    private readonly Func<AetheriaClient> _resolveClient;
    private readonly AetheriaUnityObservedEntityIndex _observedEntityIndex;
    private AetheriaClientReactiveDockingState _dockingState;

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
        return TryResolveCurrentDockingSnapshot(out var snapshot) &&
               !string.IsNullOrWhiteSpace(snapshot.CurrentEntityKey) &&
               _observedEntityIndex.TryResolveEntityByRecordKey(snapshot.CurrentEntityKey, out entity);
    }

    public bool TryResolveCurrentEntityKey(out string currentEntityKey)
    {
        currentEntityKey = TryResolveCurrentDockingSnapshot(out var snapshot)
            ? snapshot.CurrentEntityKey
            : "";
        return !string.IsNullOrWhiteSpace(currentEntityKey);
    }

    public bool TryResolveCurrentEntityDocument(out AetheriaRuntimeCurrentEntityDocument currentEntity)
    {
        currentEntity = TryResolveCurrentDockingSnapshot(out var snapshot)
            ? snapshot.CurrentEntity
            : null;
        return currentEntity != null;
    }

    public AetheriaRuntimeStationRefitDocument ResolveStationRefit()
    {
        return TryResolveCurrentDockingSnapshot(out var snapshot)
            ? snapshot.StationRefit
            : null;
    }

    public bool TryResolveCurrentDockingBayRow(out AetheriaRuntimeStationDockingBayRow dockingBay)
    {
        dockingBay = TryResolveCurrentDockingSnapshot(out var snapshot)
            ? snapshot.CurrentDockingBay
            : null;
        return dockingBay != null;
    }

    public bool TryResolveCurrentDockingBay(out EquippedDockingBay dockingBay)
    {
        dockingBay = null;
        if (!TryResolveCurrentDockingSnapshot(out var snapshot) ||
            snapshot.CurrentDockingBay == null ||
            string.IsNullOrWhiteSpace(snapshot.DockParentEntityKey) ||
            snapshot.DockingBayIndex < 0 ||
            !_observedEntityIndex.TryResolveDockingBayByRecordKey(
                snapshot.DockParentEntityKey,
                snapshot.DockingBayIndex,
                out dockingBay))
        {
            return false;
        }

        return dockingBay != null;
    }

    public bool TryResolveCurrentDocking(out AetheriaRuntimeCurrentDockingDocument docking)
    {
        docking = TryResolveCurrentDockingSnapshot(out var snapshot)
            ? snapshot.CurrentDocking
            : null;
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
        docking = TryResolveCurrentDockingSnapshot(out var snapshot)
            ? snapshot.CurrentDocking
            : null;
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

    private bool TryResolveCurrentDockingSnapshot(out AetheriaClientDockingSnapshot snapshot)
    {
        snapshot = null;
        try
        {
            _dockingState ??= _resolveClient()?.Aetheria().ReactiveDockingState();
            return _dockingState?.TryCurrent(out snapshot) == true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _dockingState?.Dispose();
        _dockingState = null;
    }
}

using System;
using System.Collections.Generic;

public sealed class AetheriaUnityObservedEntityIndex
{
    private readonly Dictionary<string, Entity> _entitiesByRecordKey = new Dictionary<string, Entity>(StringComparer.Ordinal);
    private readonly Dictionary<int, Entity> _entitiesByDaemonIndex = new Dictionary<int, Entity>();

    public IReadOnlyDictionary<string, Entity> EntitiesByRecordKey => _entitiesByRecordKey;

    public IReadOnlyDictionary<int, Entity> EntitiesByDaemonIndex => _entitiesByDaemonIndex;

    public int Count => _entitiesByRecordKey.Count;

    public bool ContainsRecordKey(string recordKey)
    {
        return !string.IsNullOrWhiteSpace(recordKey) &&
               _entitiesByRecordKey.ContainsKey(recordKey);
    }

    public void Replace(IReadOnlyDictionary<string, Entity> entitiesByRecordKey)
    {
        _entitiesByRecordKey.Clear();
        if (entitiesByRecordKey != null)
        {
            foreach (var pair in entitiesByRecordKey)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
                    _entitiesByRecordKey[pair.Key] = pair.Value;
            }
        }

        RebuildDaemonIndex();
    }

    public void RefreshDaemonIndex()
    {
        RebuildDaemonIndex();
    }

    public bool TryResolveEntityRecordKey(Entity entity, out string recordKey)
    {
        recordKey = "";
        if (entity == null)
            return false;

        foreach (var pair in _entitiesByRecordKey)
        {
            if (ReferenceEquals(pair.Value, entity))
            {
                recordKey = pair.Key;
                return true;
            }
        }

        return false;
    }

    public bool TryResolveEntityByRecordKey(string recordKey, out Entity entity)
    {
        entity = null;
        return !string.IsNullOrWhiteSpace(recordKey) &&
               _entitiesByRecordKey.TryGetValue(recordKey, out entity);
    }

    public bool TryResolveEntityByDaemonIndex(int daemonEntityIndex, out Entity entity)
    {
        return _entitiesByDaemonIndex.TryGetValue(daemonEntityIndex, out entity);
    }

    public bool TryResolveDockingBayByRecordKey(
        string parentRecordKey,
        int dockingBayIndex,
        out EquippedDockingBay dockingBay)
    {
        dockingBay = null;
        if (dockingBayIndex < 0 ||
            !TryResolveEntityByRecordKey(parentRecordKey, out var parent) ||
            parent?.DockingBays == null ||
            dockingBayIndex >= parent.DockingBays.Count)
        {
            return false;
        }

        dockingBay = parent.DockingBays[dockingBayIndex];
        return dockingBay != null;
    }

    private void RebuildDaemonIndex()
    {
        _entitiesByDaemonIndex.Clear();
        foreach (var entity in _entitiesByRecordKey.Values)
        {
            if (entity != null && entity.DaemonEntityIndex >= 0)
                _entitiesByDaemonIndex[entity.DaemonEntityIndex] = entity;
        }
    }
}

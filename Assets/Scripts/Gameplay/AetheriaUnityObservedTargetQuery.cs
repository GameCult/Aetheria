/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using GameCult.Aetheria.State.Verse;
using GameCult.Mesh;

public sealed class AetheriaUnityObservedTargetQuery : IDisposable
{
    private readonly Func<AetheriaClient> _resolveClient;
    private readonly AetheriaUnityObservedEntityIndex _entityIndex;
    private CultMeshReactiveDocument<AetheriaRuntimeZoneContactsDocument> _zoneContacts;

    public AetheriaUnityObservedTargetQuery(
        Func<AetheriaClient> resolveClient,
        AetheriaUnityObservedEntityIndex entityIndex)
    {
        _resolveClient = resolveClient ?? (() => null);
        _entityIndex = entityIndex ?? throw new ArgumentNullException(nameof(entityIndex));
    }

    public Entity GetObservedTarget(Entity observer)
    {
        if (TryQueryEntityTarget(observer, out var targetEntityIndex) &&
            _entityIndex.TryResolveEntityByDaemonIndex(targetEntityIndex, out var targetEntity))
        {
            return targetEntity;
        }

        return null;
    }

    public float GetObservedInfoGathered(Entity observer, Entity target)
    {
        return TryQueryEntityContact(observer, target, out var contact)
            ? (float)contact.InfoGathered
            : 0f;
    }

    public bool IsObservedHostileContact(Entity observer, Entity target)
    {
        return TryQueryEntityContact(observer, target, out var contact) && contact.Hostile;
    }

    public AetheriaRuntimeZoneContactRow[] GetObservedVisibleContacts(
        Entity observer,
        double minimumInfoGathered,
        bool visibleOnly)
    {
        var contacts = ReadZoneContacts();
        if (observer == null || contacts == null)
            return Array.Empty<AetheriaRuntimeZoneContactRow>();

        return (contacts.Contacts ?? Array.Empty<AetheriaRuntimeZoneContactRow>())
            .Where(contact =>
                contact.ObserverEntityIndex == observer.DaemonEntityIndex &&
                contact.InfoGathered > minimumInfoGathered &&
                (!visibleOnly || contact.Visible))
            .ToArray();
    }

    private bool TryQueryEntityContact(
        Entity observer,
        Entity target,
        out AetheriaRuntimeZoneContactRow contact)
    {
        contact = default;
        if (observer == null || target == null)
            return false;

        contact = (ReadZoneContacts()?.Contacts ?? Array.Empty<AetheriaRuntimeZoneContactRow>())
            .FirstOrDefault(row =>
                row.ObserverEntityIndex == observer.DaemonEntityIndex &&
                row.TargetEntityIndex == target.DaemonEntityIndex);
        return contact != null;
    }

    private bool TryQueryEntityTarget(
        Entity observer,
        out int targetEntityIndex)
    {
        targetEntityIndex = -1;
        if (observer == null)
            return false;

        targetEntityIndex = (ReadZoneContacts()?.Targets ?? Array.Empty<AetheriaRuntimeZoneTargetRow>())
            .FirstOrDefault(row => row.EntityIndex == observer.DaemonEntityIndex)
            ?.TargetEntityIndex ?? -1;
        return targetEntityIndex >= 0;
    }

    private AetheriaRuntimeZoneContactsDocument ReadZoneContacts()
    {
        if (_zoneContacts != null)
            return _zoneContacts.Current;

        try
        {
            _zoneContacts = _resolveClient()
                ?.State.Document<AetheriaRuntimeZoneContactsDocument>().Reactive();
            return _zoneContacts?.Current;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _zoneContacts?.Dispose();
        _zoneContacts = null;
    }
}

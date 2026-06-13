/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using MessagePack;
using UniRx;

public interface ILegacyItemCatalogReader
{
    ItemData Get(Guid guid);
    T Get<T>(Guid guid) where T : ItemData;
}

public class LegacyItemCatalogCache : ILegacyItemCatalogReader
{

    private readonly object addLock = new object();

    private List<CacheBackingStore> _backingStores = new List<CacheBackingStore>();

    private readonly Dictionary<Guid, ItemData> _entries = new Dictionary<Guid, ItemData>();

    public void AddBackingStore(CacheBackingStore store)
    {
        _backingStores.Add(store);
        store.EntryAdded.Subscribe(entry =>
        {
            AddInternal(entry, store);
        });
    }

    public void PullAllBackingStores()
    {
        foreach(var store in _backingStores) store.PullAll();
    }

    private void AddInternal(DatabaseEntry entry, CacheBackingStore source = null)
    {
        lock(addLock)
        {
            if (entry is ItemData item)
            {
                _entries[item.ID] = item;
            }
        }
    }

    public ItemData Get(Guid guid)
    {
        ItemData entry;
        _entries.TryGetValue(guid, out entry);
        return entry;
    }
	
    public T Get<T>(Guid guid) where T : ItemData
    {
        return Get(guid) as T;
    }
}

public abstract class CacheBackingStore
{
    protected CacheBackingStore()
    {
        EntryAdded = new Subject<DatabaseEntry>();
    }

    public abstract void PullAll();
    
    public Subject<DatabaseEntry> EntryAdded { get; }

    protected Dictionary<Guid, DatabaseEntry> Entries = new Dictionary<Guid, DatabaseEntry>();
}

public abstract class SingleFileBackingStore : CacheBackingStore
{
    public FileInfo FileInfo { get; }

    public abstract DatabaseEntry[] Deserialize(byte[] data);

    public SingleFileBackingStore(string filePath)
    {
        FileInfo = new FileInfo(filePath);
    }

    public override void PullAll()
    {
        if (!FileInfo.Exists) return;

        foreach (var entry in Deserialize(File.ReadAllBytes(FileInfo.FullName)))
        {
            Entries[entry.ID] = entry;
            EntryAdded.OnNext(entry);
        }
    }

}

public class SingleFileMessagePackBackingStore : SingleFileBackingStore
{
    public SingleFileMessagePackBackingStore(string filePath) : base(filePath)
    {
        RegisterResolver.Register();
    }

    public override DatabaseEntry[] Deserialize(byte[] data)
    {
        return MessagePackSerializer.Deserialize<DatabaseEntry[]>(data);
    }
}


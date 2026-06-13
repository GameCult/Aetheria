/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using MessagePack;
using UniRx;

public interface ILegacyCatalogReader
{
    DatabaseEntry Get(Guid guid);
    T Get<T>(Guid guid) where T : DatabaseEntry;
}

public class LegacyCatalogCache : ILegacyCatalogReader
{

    private readonly object addLock = new object();

    private List<CacheBackingStore> _backingStores = new List<CacheBackingStore>();
    
    private readonly Dictionary<CacheBackingStore, Type[]> _storeTypes = new Dictionary<CacheBackingStore, Type[]>();

    private readonly Dictionary<Guid, DatabaseEntry> _entries = new Dictionary<Guid, DatabaseEntry>();

    public void AddBackingStore(CacheBackingStore store, params Type[] domain)
    {
        if (domain.Length > 0)
        {
            _storeTypes[store] = domain;
        }
        else
        {
            _backingStores.Add(store);
        }
        store.EntryAdded.Subscribe(entry =>
        {
            AddInternal(entry, store);
        });
    }

    public void PullAllBackingStores()
    {
        foreach(var store in _backingStores) store.PullAll();
        foreach(var store in _storeTypes.Keys) store.PullAll();
    }

    private void AddInternal(DatabaseEntry entry, CacheBackingStore source = null)
    {
        lock(addLock)
        {
            if (entry != null)
            {
                _entries[entry.ID] = entry;
            }
        }
    }

    public DatabaseEntry Get(Guid guid)
    {
        DatabaseEntry entry;
        _entries.TryGetValue(guid, out entry);
        return entry;
    }
	
    public T Get<T>(Guid guid) where T : DatabaseEntry
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

public abstract class MultiFileBackingStore : CacheBackingStore
{
    public DirectoryInfo DirectoryInfo { get; }
    protected Dictionary<Type, DirectoryInfo> _entryTypeDirectories = new Dictionary<Type, DirectoryInfo>();
    
    public abstract DatabaseEntry Deserialize(byte[] data);
    public abstract string Extension { get; }

    public MultiFileBackingStore(string path)
    {
        DirectoryInfo = new DirectoryInfo(path);
        foreach (var type in typeof(DatabaseEntry).GetAllChildClasses())
        {
            _entryTypeDirectories[type] = new DirectoryInfo(Path.Combine(DirectoryInfo.FullName, type.Name));
        }
    }
    
    public override void PullAll()
    {
        if (!DirectoryInfo.Exists) return;

        foreach (var directory in _entryTypeDirectories.Values)
        {
            foreach (var file in directory.EnumerateFiles($"*.{Extension}"))
            {
                var entry = Deserialize(File.ReadAllBytes(file.FullName));
                Entries[entry.ID] = entry;
                EntryAdded.OnNext(entry);
            }
        }
    }

}

public class MultiFileMessagePackBackingStore : MultiFileBackingStore
{
    public MultiFileMessagePackBackingStore(string path) : base(path)
    {
        RegisterResolver.Register();
    }

    public override DatabaseEntry Deserialize(byte[] data)
    {
        return MessagePackSerializer.Deserialize<DatabaseEntry>(data);
    }

    public override string Extension => "msgpack";
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


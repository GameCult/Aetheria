/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MessagePack;
using UniRx;

public class LegacyCatalogCache
{

    private readonly object addLock = new object();

    private List<CacheBackingStore> _backingStores = new List<CacheBackingStore>();
    
    private readonly Dictionary<Type, CacheBackingStore> _typeStores = new Dictionary<Type, CacheBackingStore>();
    private readonly Dictionary<CacheBackingStore, Type[]> _storeTypes = new Dictionary<CacheBackingStore, Type[]>();

    private readonly Dictionary<Guid, DatabaseEntry> _entries = new Dictionary<Guid, DatabaseEntry>();

    private readonly Dictionary<Type, DatabaseEntry> _globals = new Dictionary<Type, DatabaseEntry>();
    private readonly Dictionary<Type, HashSet<DatabaseEntry>> _types = new Dictionary<Type, HashSet<DatabaseEntry>>();

    public IEnumerable<DatabaseEntry> AllEntries => _entries.Values;

    public LegacyCatalogCache()
    {
        foreach (var type in typeof(DatabaseEntry).GetAllChildClasses())
        {
            _types[type] = new HashSet<DatabaseEntry>();
            
            if (type.GetCustomAttribute<GlobalSettingsAttribute>() != null)
            {
                _globals[type] = null;
                AddInternal(Activator.CreateInstance(type) as DatabaseEntry);
            }
        }
    }

    public void AddBackingStore(CacheBackingStore store, params Type[] domain)
    {
        if (domain.Length > 0)
        {
            _storeTypes[store] = domain;
            foreach (var t in domain) _typeStores[t] = store;
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
                var exists = _entries.ContainsKey(entry.ID);
                
                var type = entry.GetType();

                if (_globals.ContainsKey(type))
                {
                    if(_globals[type]!=null)
                    {
                        RemoveInternal(_globals[type]);
                        exists = true;
                    }
                    _globals[type] = entry;
                }
                
                _entries[entry.ID] = entry;
                _types[type].Add(entry);
                foreach (var parentType in type.GetParentTypes())
                {
                    if(_types.ContainsKey(parentType))
                        _types[parentType].Add(entry);
                }

            }
        }
    }

    public bool IsGlobal(DatabaseEntry entry) => _globals.ContainsKey(entry.GetType());

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

    public DatabaseEntry GetGlobal(Type type)
    {
        if (_globals.ContainsKey(type)) return _globals[type];
        return null;
    }

    public T GetGlobal<T>() where T : DatabaseEntry
    {
        return GetGlobal(typeof(T)) as T;
    }

    public IEnumerable<DatabaseEntry> GetAll(Type type)
    {
        return !_types.ContainsKey(type) ? Enumerable.Empty<DatabaseEntry>() : _types[type];
    }

    public IEnumerable<T> GetAll<T>() where T : DatabaseEntry
    {
        var type = typeof(T);
        return !_types.ContainsKey(type) ? Enumerable.Empty<T>() : _types[type].Cast<T>();
    }

    private void RemoveInternal(DatabaseEntry entry)
    {
        _entries.Remove(entry.ID);
        var type = entry.GetType();
        foreach (var parentType in type.GetParentTypes())
        {
            if(_types.ContainsKey(parentType))
                _types[parentType].Remove(entry);
        }
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
    
    public abstract byte[] Serialize(DatabaseEntry entry);
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

    public override byte[] Serialize(DatabaseEntry entry)
    {
        return MessagePackSerializer.Serialize(entry);
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

    public abstract byte[] Serialize(DatabaseEntry[] entries);
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

    public override byte[] Serialize(DatabaseEntry[] entries)
    {
        return MessagePackSerializer.Serialize(entries);
    }

    public override DatabaseEntry[] Deserialize(byte[] data)
    {
        return MessagePackSerializer.Deserialize<DatabaseEntry[]>(data);
    }
}


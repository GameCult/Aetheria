/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using Newtonsoft.Json;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using UniRx;

public class CultCache
{

    private readonly object addLock = new object();

    //public Action<string> Logger = Console.WriteLine;
    //private string _filePath;

    public event Action<DatabaseEntry, DatabaseEntry> OnUpdate;

    private List<CacheBackingStore> _backingStores = new List<CacheBackingStore>();
    
    private readonly Dictionary<Type, CacheBackingStore> _typeStores = new Dictionary<Type, CacheBackingStore>();
    private readonly Dictionary<CacheBackingStore, Type[]> _storeTypes = new Dictionary<CacheBackingStore, Type[]>();

    private readonly Dictionary<Guid, DatabaseEntry> _entries = new Dictionary<Guid, DatabaseEntry>();

    private readonly Dictionary<Type, DatabaseEntry> _globals = new Dictionary<Type, DatabaseEntry>();
    private readonly Dictionary<Type, HashSet<DatabaseEntry>> _types = new Dictionary<Type, HashSet<DatabaseEntry>>();

    // private readonly Dictionary<Type, (DirectoryInfo directory, List<Guid> entries)> _externalTypes = new Dictionary<Type, (DirectoryInfo directory, List<Guid> entries)>();

    // private class ExternalEntry
    // {
    //     public string FilePath;
    //     public DatabaseEntry Entry;
    //
    //     public ExternalEntry(string filePath, DatabaseEntry entry)
    //     {
    //         FilePath = filePath;
    //         Entry = entry;
    //     }
    // }
    // private readonly Dictionary<Guid, ExternalEntry> _externalEntries = new Dictionary<Guid, ExternalEntry>();

    public IEnumerable<DatabaseEntry> AllEntries => _entries.Values;

    public CultCache()
    {
        DatabaseLinkBase.Cache = this;
        
        foreach (var type in typeof(DatabaseEntry).GetAllChildClasses())
        {
            _types[type] = new HashSet<DatabaseEntry>();
            
            if (type.GetCustomAttribute<GlobalSettingsAttribute>() != null)
            {
                _globals[type] = null;
                Add(Activator.CreateInstance(type) as DatabaseEntry);
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
            foreach(var existingStore in _backingStores) store.SubscribeTo(existingStore);
            _backingStores.Add(store);
        }
        store.EntryAdded.Subscribe(entry =>
        {
            Add(entry, store);
            OnUpdate?.Invoke(null, entry);
        });
        store.EntryUpdated.Subscribe(entry =>
        {
            Add(entry, store);
            OnUpdate?.Invoke(entry, entry);
        });
        store.EntryDeleted.Subscribe(entry =>
        {
            Remove(entry, store);
            OnUpdate?.Invoke(entry, null);
        });
    }

    public void PullAllBackingStores()
    {
        foreach(var store in _backingStores) store.PullAll();
        foreach(var store in _storeTypes.Keys) store.PullAll();
    }

    public void Add(DatabaseEntry entry, CacheBackingStore source = null)
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
                        Remove(_globals[type]);
                        exists = true;
                    }
                    _globals[type] = entry;
                }
                
                if(_typeStores.ContainsKey(type))
                    _typeStores[type].Push(entry);
                else
                {
                    var masterStore = _backingStores.First();
                    if(masterStore!=source)
                        masterStore.Push(entry);
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

    public void AddAll(IEnumerable<DatabaseEntry> entries)
    {
        foreach (var entry in entries)
        {
            Add(entry);
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

    public void Remove(DatabaseEntry entry, CacheBackingStore source = null)
    {
        _entries.Remove(entry.ID);
        var type = entry.GetType();
        if(_typeStores.ContainsKey(type))
            _typeStores[type].Delete(entry);
        else foreach(var store in _backingStores)
        {
            if(store != source)
                store.Delete(entry);
        }
        foreach (var parentType in type.GetParentTypes())
        {
            if(_types.ContainsKey(parentType))
                _types[parentType].Remove(entry);
        }
    }
}

public interface RealtimeBackingStore
{
    public void ObserveChanges();
}

public abstract class CacheBackingStore
{
    protected CacheBackingStore()
    {
        EntryAdded = new Subject<DatabaseEntry>();
        EntryDeleted = new Subject<DatabaseEntry>();
        EntryUpdated = new Subject<DatabaseEntry>();
    }

    public abstract void PullAll();
    public abstract void Push(DatabaseEntry entry);
    public abstract void Delete(DatabaseEntry entry);
    public abstract void PushAll(bool soft = false);
    
    public Subject<DatabaseEntry> EntryAdded { get; }
    public Subject<DatabaseEntry> EntryDeleted { get; }
    public Subject<DatabaseEntry> EntryUpdated { get; }

    protected Dictionary<Guid, DatabaseEntry> Entries = new Dictionary<Guid, DatabaseEntry>();

    public void SubscribeTo(CacheBackingStore targetStore)
    {
        targetStore.EntryAdded.Subscribe(Push);
        targetStore.EntryDeleted.Subscribe(Delete);
        targetStore.EntryUpdated.Subscribe(Push);
    }
}

public abstract class MultiFileBackingStore : CacheBackingStore, RealtimeBackingStore
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
            _entryTypeDirectories[type] = DirectoryInfo.CreateSubdirectory(type.Name);
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

    private string GetFileName(DatabaseEntry entry) =>
        $"{(entry is INamedEntry namedEntry ? namedEntry.EntryName : entry.ID.ToString())}.{Extension}";

    public override void Push(DatabaseEntry entry)
    {
        var type = entry.GetType();
        Entries[entry.ID] = entry;
        var directory = _entryTypeDirectories[type];
        File.WriteAllBytes(Path.Combine(directory.FullName, GetFileName(entry)), Serialize(entry));
    }

    public override void Delete(DatabaseEntry entry)
    {
        if(Entries.ContainsKey(entry.ID))
        {
            var type = entry.GetType();
            Entries.Remove(entry.ID);
            var directory = _entryTypeDirectories[type];
            var file = new FileInfo(Path.Combine(directory.FullName, GetFileName(entry)));
            if (file.Exists)
                file.Delete();
        }
    }

    public override void PushAll(bool soft = false)
    {
        foreach(var entry in Entries.Values.ToArray()) Push(entry);
    }

    public void ObserveChanges()
    {
        foreach (var directory in _entryTypeDirectories.Values)
        {
            var watcher = new FileSystemWatcher(directory.FullName);
            watcher.Changed += (sender, args) =>
            {
                var entry = Deserialize(File.ReadAllBytes(args.FullPath));
                Entries[entry.ID] = entry;
                EntryUpdated.OnNext(entry);
            };
            watcher.Created += (sender, args) =>
            {
                var entry = Deserialize(File.ReadAllBytes(args.FullPath));
                Entries[entry.ID] = entry;
                EntryAdded.OnNext(entry);
            };
            watcher.Deleted += (sender, args) =>
            {
                var entry = Deserialize(File.ReadAllBytes(args.FullPath));
                Entries[entry.ID] = entry;
                EntryDeleted.OnNext(entry);
            };
        }
    }
}

public class MultiFileJsonBackingStore : MultiFileBackingStore
{
    public MultiFileJsonBackingStore(string path) : base(path)
    {
        RegisterResolver.Register();
    }

    public override byte[] Serialize(DatabaseEntry entry)
    {
        return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entry));
    }

    public override DatabaseEntry Deserialize(byte[] data)
    {
        return JsonConvert.DeserializeObject<DatabaseEntry>(Encoding.UTF8.GetString(data));
    }

    public override string Extension => "json";
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

    public override void Push(DatabaseEntry entry)
    {
        var type = entry.GetType();
        Entries[entry.ID] = entry;
        PushAll();
    }

    public override void Delete(DatabaseEntry entry)
    {
        if(Entries.ContainsKey(entry.ID))
        {
            Entries.Remove(entry.ID);
            PushAll();
        }
    }

    public override void PushAll(bool soft = false)
    {
        var entriesArray = Entries.Values.ToArray();
        File.WriteAllBytes(FileInfo.FullName, Serialize(entriesArray));
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

public class SingleFileJsonBackingStore : SingleFileBackingStore
{
    public SingleFileJsonBackingStore(string filePath) : base(filePath)
    {
        RegisterResolver.Register();
    }

    public override byte[] Serialize(DatabaseEntry[] entries)
    {
        return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entries));
    }

    public override DatabaseEntry[] Deserialize(byte[] data)
    {
        return JsonConvert.DeserializeObject<DatabaseEntry[]>(Encoding.UTF8.GetString(data));
    }
}

public class RethinkBackingStore : CacheBackingStore, RealtimeBackingStore
{
    public const string DEFAULT_TABLE = "Default";
    public static RethinkDB R = RethinkDB.R;
    public string DatabaseName { get; }
    public string ConnectionString { get; }
    
    private string[] _syncTables;
    private Connection _connection;
    private Dictionary<DatabaseEntry, string> _entryHashes = new Dictionary<DatabaseEntry, string>();
    
    public void ObserveChanges()
    {
        foreach (var table in _syncTables)
        {
            // Subscribe to changes from RethinkDB
            Task.Run(async () =>
            {
                var result = await R
                    .Db(DatabaseName)
                    .Table(table)
                    .Changes()
                    .RunChangesAsync<DatabaseEntry>(_connection);
                while (await result.MoveNextAsync())
                {
                    var change = result.Current;
                    if(change.NewValue==null)
                        EntryDeleted.OnNext(change.OldValue);
                    else if(change.OldValue==null)
                        EntryAdded.OnNext(change.NewValue);
                    else EntryUpdated.OnNext(change.NewValue);
                }
            }).WrapAwait();
        }
    }

    public RethinkBackingStore(string databaseName, string connectionString)
    {
        DatabaseName = databaseName;
        ConnectionString = connectionString;
        
        RegisterResolver.Register();

        var connectionStringDomainLength = connectionString.IndexOf(':');
        if (connectionStringDomainLength < 1)
            throw new ArgumentException("Illegal Connection String: must include port!");
        
        var portString = connectionString.Substring(connectionStringDomainLength + 1);
        if (!int.TryParse(portString, out var port))
            throw new ArgumentException($"Illegal connection string! \"{portString}\" is not a valid port number!");
        
        _connection = R.Connection()
            .Hostname(connectionString.Substring(0,connectionStringDomainLength))
            .Port(port).Timeout(60).Connect();

        var tables = R.Db(DatabaseName).TableList().RunAtom<string[]>(_connection);

        _syncTables = typeof(DatabaseEntry).GetAllChildClasses()
            .Select(t => t.GetCustomAttribute<RethinkTableAttribute>()?.TableName ?? DEFAULT_TABLE).Distinct().ToArray();

        foreach (var st in _syncTables.Where(st => !tables.Contains(st)))
            R.Db(DatabaseName).TableCreate(st).RunNoReply(_connection);
    }
    
    public override void PullAll()
    {
        foreach (var table in _syncTables)
        {
            // Get entries from RethinkDB
            Task.Run(async () =>
            {
                var result = await R
                    .Db(DatabaseName)
                    .Table(table)
                    .RunCursorAsync<DatabaseEntry>(_connection);
                
                while (await result.MoveNextAsync())
                {
                    var entry = result.Current;
                    EntryAdded.OnNext(entry);
                }
            }).WrapAwait();
        }
    }

    public override async void Push(DatabaseEntry entry)
    {
        var type = entry.GetType();
        //(Entries.ContainsKey(entry.ID) ? EntryUpdated : EntryAdded).OnNext(entry);
        var table = type.GetCustomAttribute<RethinkTableAttribute>()?.TableName ?? DEFAULT_TABLE;
        var result = await R
            .Db(DatabaseName)
            .Table(table)
            .Get(entry.ID)
            .Replace(entry)
            .RunAsync(_connection);
    }

    public override async void Delete(DatabaseEntry entry)
    {
        if(Entries.ContainsKey(entry.ID))
        {
            var type = entry.GetType();
            var table = type.GetCustomAttribute<RethinkTableAttribute>()?.TableName ?? DEFAULT_TABLE;
            var result = await R
                .Db(DatabaseName)
                .Table(table)
                .Get(entry.ID)
                .Delete()
                .RunAsync(_connection);
        }
    }

    public override void PushAll(bool soft = false)
    {
        foreach(var entry in Entries.Values)
            Push(entry);
    }
}
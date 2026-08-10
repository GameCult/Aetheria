/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JsonKnownTypes;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUILayout;

public abstract class DatabaseListView : EditorWindow
{
    public static GUIStyle ListStyleOdd;
    public static GUIStyle ListStyleEven;
    
    public Guid SelectedItem;
    public GUIStyle SelectedStyle;

    public static RethinkDB R = RethinkDB.R;
    
    protected CultCache cultCache;
    private static DatabaseInspector _inspector;
    private Dictionary<string, (bool foldout, (Type entryType, bool foldout)[] types)> _tables;
    //private Type[] _entryTypes;
    private Type[] _globalTypes;
    private string _connectionString;
    private Vector2 _view;
    private RethinkQueryStatus _queryStatus;

    private HashSet<int> _groupFoldouts = new HashSet<int>(); 
    
    protected abstract string DatabaseName { get; }
    protected abstract string FilePath { get; }
    protected virtual DatabaseEntryGroup[] Groupers => new DatabaseEntryGroup[] { };

    protected class DatabaseEntryGroup<T, K> : DatabaseEntryGroup where T : DatabaseEntry
    {
        public Func<T,K> GroupSelector { get; }
        public Func<K,string> GroupLabel { get; }
        public Action<T, K> Activator { get; }
        
        public DatabaseEntryGroup(Func<T, K> groupSelector, Func<K, string> groupLabel, Action<T, K> activator)
        {
            GroupSelector = groupSelector;
            GroupLabel = groupLabel;
            Activator = activator;
        }

        public override bool CanGroup(Type type)
        {
            return typeof(T) == type;
        }

        public override object SelectGroup(DatabaseEntry o)
        {
            if (o is T t) return GroupSelector(t);
            return null;
        }

        public override string GetGroupName(object o)
        {
            if (o is K k) return GroupLabel(k);
            return "ERROR";
        }

        public override int HashGroup(object o)
        {
            return o.GetHashCode();
            if (o is K k) return k.GetHashCode();
            return 0;
        }

        public override void Activate(DatabaseEntry o, object g)
        {
            if(o is T t && g is K k) Activator(t, k);
            throw new ArgumentException(
                $"Entry group activation failed! Expected: object type {typeof(T)}, group type {typeof(K)}; received: object type {o.GetType()}, group type {g.GetType()}");
        }
    }

    protected abstract class DatabaseEntryGroup
    {
        public abstract bool CanGroup(Type type);
        public abstract object SelectGroup(DatabaseEntry o);
        public abstract string GetGroupName(object o);
        public abstract int HashGroup(object o);
        public abstract void Activate(DatabaseEntry o, object g);
    }

    public Color LabelColor => EditorGUIUtility.isProSkin ? Color.white : Color.black;

    private bool _listItemStyle;
    private SingleFileMessagePackBackingStore _msgPackStore;
    private MultiFileJsonBackingStore _jsonStore;
    public GUIStyle ListItemStyle => (_listItemStyle = !_listItemStyle) ? ListStyleEven : ListStyleOdd;
    
    void OnEnable()
    {
        ListStyleOdd = new GUIStyle
        {
            normal = {background = EditorGUIUtility.isProSkin ? XKCDColors.DarkGrey.ToTexture() : XKCDColors.LightGrey.ToTexture()},
            margin = new RectOffset(0, 0, 0, 0)
        };
        ListStyleEven = new GUIStyle
        {
            normal = {background = EditorGUIUtility.isProSkin ? Color.Lerp(XKCDColors.DarkGrey,Color.black,.5f).ToTexture() : Color.Lerp(XKCDColors.LightGrey,Color.white,.5f).ToTexture()},
            margin = new RectOffset(0, 0, 0, 0)
        };

        //_jsonStore = new MultiFileJsonBackingStore(FilePath);
        _msgPackStore = new SingleFileMessagePackBackingStore(Path.Combine(FilePath, "AetherDB.msgpack"));
        cultCache = new CultCache();
        //cultCache.AddBackingStore(_jsonStore);
        cultCache.AddBackingStore(_msgPackStore);
        cultCache.AddBackingStore(new MultiFileMessagePackBackingStore(FilePath), typeof(NameFile));
        cultCache.PullAllBackingStores();

        cultCache.OnUpdate += (_,__) => EditorDispatcher.Dispatch(Repaint);
        DatabaseInspector.CultCache = cultCache;
        _inspector = DatabaseInspector.Instance;
        _inspector.Show();
        
        _globalTypes = typeof(DatabaseEntry).GetAllChildClasses()
            .Where(t => t.GetCustomAttribute<InspectableAttribute>() != null && t.GetCustomAttribute<GlobalSettingsAttribute>() != null).ToArray();
        _tables = typeof(DatabaseEntry).GetAllChildClasses()
            .Where(t => t.GetCustomAttribute<InspectableAttribute>() != null && t.GetCustomAttribute<GlobalSettingsAttribute>() == null)
            .GroupBy(t=>t.GetCustomAttribute<RethinkTableAttribute>()?.TableName ?? "Default")
            .ToDictionary(group=>group.Key, group=> (false, group.Select(t=>(t, false)).ToArray()));

        if (EditorPrefs.HasKey("RethinkDB.URL"))
            _connectionString = EditorPrefs.GetString("RethinkDB.URL");
        else
            _connectionString = "Enter DB URL";
        
        InitStyles();
    }

    private void InitStyles()
    {
        SelectedStyle = new GUIStyle
        {
            normal = {background = EditorGUIUtility.isProSkin ? XKCDColors.DarkGreyBlue.ToTexture() : XKCDColors.Greyblue.ToTexture()},
            margin = new RectOffset(0, 0, 0, 0)
        };
    }

    public void OnGUI()
    {
        Event currentEvent = Event.current;
        EventType currentEventType = currentEvent.type;

        if (currentEventType == EventType.DragUpdated)
            // Indicate that we don't accept drags ourselves
            DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;

        using (new HorizontalScope())
        {
            _connectionString = TextField(_connectionString);
            if (GUILayout.Button("Connect"))
            {
                EditorPrefs.SetString("RethinkDB.URL", _connectionString);
                JsonKnownTypesSettingsManager.RegisterTypeAssembly<ItemData>();
                _queryStatus = RethinkConnection.RethinkConnect(cultCache, _connectionString, DatabaseName);
            }
        }
        using (new HorizontalScope())
        {
            GUILayout.Label("Export");
            //_fileName = TextField(_fileName);
            if (GUILayout.Button("JSON"))
            {
                _jsonStore.PushAll();
                Debug.Log("Exported DB to JSON!");
            }

            if (GUILayout.Button("MsgPack"))
            {
                _msgPackStore.PushAll();
                Debug.Log("Exported DB to MessagePack!");
            }
        }

        if (_queryStatus != null && _queryStatus.RetrievedEntries < _queryStatus.TotalEntries)
        {
            var progressRect = GetControlRect(false, 20);
            EditorGUI.ProgressBar(progressRect, (float)_queryStatus.RetrievedEntries/_queryStatus.TotalEntries, "Sync Progress");
        }
        GUILayout.Space(5);
        
        _view = BeginScrollView(
            _view, 
            false,
            false,
            GUIStyle.none,
            GUI.skin.verticalScrollbar,
            GUI.skin.scrollView,
            GUILayout.Width(EditorGUIUtility.currentViewWidth),
            GUILayout.ExpandHeight(true));

        foreach (var globalType in _globalTypes)
        {
            var globalEntry = cultCache.GetGlobal(globalType);
            var style = ListItemStyle;
            var selected = SelectedItem == globalEntry.ID;
            using (new HorizontalScope(selected?SelectedStyle:style))
            {
                using (var h = new HorizontalScope())
                {
                    if (h.rect.Contains(currentEvent.mousePosition))
                    {
                        if (currentEventType == EventType.MouseUp)
                            Select(globalEntry);
                    }

                    GUILayout.Label(globalType.Name.SplitCamelCase(), selected ? EditorStyles.whiteLabel : GUI.skin.label);
                }
            }
        }

        void DisplayGroup(Type entryType, IEnumerable<DatabaseEntry> entries, IEnumerable<DatabaseEntryGroup> nestedGroups, int indent = 2, Action<DatabaseEntry> activatePreviousGroups = null, string previousGroupNames = "")
        {
            var grouper = nestedGroups.FirstOrDefault();
            if (grouper != null)
            {
                var groups = entries.GroupBy(o => grouper.SelectGroup(o));
                foreach (var group in groups)
                {
                    //var groupHash = grouper.HashGroup(group.Key);
                    var groupName = grouper.GetGroupName(group.Key);
                    var groupHash = groupName.GetHashCode();
                    using (new HorizontalScope(ListItemStyle))
                    {
                        GUILayout.Space(indent*10);
                        if (Foldout(_groupFoldouts.Contains(groupHash), FormatTypeName(groupName), true))
                            _groupFoldouts.Add(groupHash);
                        else _groupFoldouts.Remove(groupHash);
                    }

                    if (_groupFoldouts.Contains(groupHash))
                    {
                        Action<DatabaseEntry> activate = entry =>
                        {
                            activatePreviousGroups?.Invoke(entry);
                            grouper.Activate(entry, group.Key);
                        };
                        
                        DisplayGroup(entryType, group, nestedGroups.Skip(1), indent + 1, activate, $"{groupName} {previousGroupNames}");
                        
                        using (var h = new HorizontalScope(ListItemStyle))
                        {
                            if (GUI.Button(h.rect, GUIContent.none, GUIStyle.none))
                                CreateItem(entryType, activate);
                            GUILayout.Space((indent+1)*10);
                            GUILayout.Label($"Create {groupName} {previousGroupNames}");
                        }
                    }
                }
            }
            else
            {
                int index = 0;
                foreach (var entry in entries)
                {
                    index++;
                    var style = ListItemStyle;
                    var selected = SelectedItem == entry.ID;
                    using (var h = new HorizontalScope(selected ? SelectedStyle : style))
                    {
                        if (h.rect.Contains(currentEvent.mousePosition))
                        {
                            if (currentEventType == EventType.MouseDrag)
                            {
                                DragAndDrop.PrepareStartDrag();
                                DragAndDrop.SetGenericData("Item", entry.ID);
                                DragAndDrop.StartDrag("Database Item");
                            }
                            else if (currentEventType == EventType.MouseUp)
                                Select(entry);
                        }

                        GUILayout.Space(indent*10);
                        GUILayout.Label((entry as INamedEntry)?.EntryName ?? index.ToString(),
                            selected ? EditorStyles.whiteLabel : GUI.skin.label);
                    }
                }
            }
        }

        foreach (var table in _tables.Keys.ToArray())
        {
            var (tableFoldout, _) = _tables[table];
            using (new HorizontalScope(ListItemStyle))
            {
                _tables[table] = (Foldout(tableFoldout = _tables[table].foldout, table, true), _tables[table].types);
            }

            if (tableFoldout)
            {
                for (var i = 0; i < _tables[table].types.Length; i++)
                {
                    var (entryType, typeFoldout) = _tables[table].types[i];
                    using (new HorizontalScope(ListItemStyle))
                    {
                        GUILayout.Space(10);
                        _tables[table].types[i] = (entryType, typeFoldout = Foldout(typeFoldout,
                            ((NameAttribute) entryType.GetCustomAttribute(typeof(NameAttribute)))?.Name ?? FormatTypeName(entryType.Name),
                            true));
                    }

                    if (typeFoldout)
                    {
                        var entries = cultCache.GetAll(entryType)
                            .OrderBy(entry => entry is INamedEntry namedEntry ? namedEntry.EntryName : entry.ID.ToString());
                        var groupers = Groupers.Where(g => g.CanGroup(entryType));
                        DisplayGroup(entryType, entries, groupers);
                        
                        using (var h = new HorizontalScope(ListItemStyle))
                        {
                            if (GUI.Button(h.rect, GUIContent.none, GUIStyle.none))
                                CreateItem(entryType);
                            GUILayout.Space(20);
                            GUILayout.Label($"Create {FormatTypeName(entryType.Name)}");
                        }
                    }
                }
            }
        }
            
        EndScrollView();
    }

    public static string FormatTypeName(string typeName)
    {
        return (typeName.EndsWith("Data")
            ? typeName.Substring(0, typeName.Length - 4)
            : typeName).SplitCamelCase();
    }
    
    void Select(DatabaseEntry item)
    {
        SelectedItem = item.ID;
        Repaint();
        _inspector.entry = cultCache.Get(SelectedItem);
        _inspector.Repaint();
    }

    private DatabaseEntry CreateItem(Type type, Action<DatabaseEntry> onCreate = null)
    {
        var newEntry = (DatabaseEntry) Activator.CreateInstance(type);
        if(newEntry is INamedEntry entry)
            entry.EntryName = $"New {FormatTypeName(type.Name)}";
        onCreate?.Invoke(newEntry);
        cultCache.Add(newEntry);
        Select(newEntry);
        return newEntry;
    }
}

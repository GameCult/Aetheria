/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MessagePack;
using Newtonsoft.Json;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static Unity.Mathematics.math;
using static UnityEditor.EditorGUILayout;
using int2 = Unity.Mathematics.int2;
using Object = UnityEngine.Object;

public class DatabaseInspector : EditorWindow
{
    public static CultCache CultCache;
    public DatabaseEntry entry;
    
    // Singleton to avoid multiple instances of window.
    private static DatabaseInspector _instance;
    public static DatabaseInspector Instance => _instance ? _instance : GetWindow<DatabaseInspector>();
    private static bool _listItemStyle;
    public static GUIStyle ListItemStyle => (_listItemStyle = !_listItemStyle) ? DatabaseListView.ListStyleEven : DatabaseListView.ListStyleOdd;

    public const int Width = 150;

    private Vector2 _view;
    private GUIStyle _warning;
    private List<(Type targetType, HashSet<Type> attributes, Type inspectorType, object instance)> _inspectors = 
        new List<(Type targetType, HashSet<Type> attributes, Type inspectorType, object instance)>();

    private List<(Type targetType, HashSet<Type> attributes, Type inspectorType, Dictionary<Type, object> instances)> _genericInspectors = 
        new List<(Type targetType, HashSet<Type> attributes, Type inspectorType, Dictionary<Type, object> instances)>();
    private HashSet<object> _foldouts = new HashSet<object>();

    public Color LabelColor => EditorGUIUtility.isProSkin ? Color.white : Color.black;

    void OnEnable()
    {
        _instance = this;
        CultCache.OnUpdate += (_,__) => EditorDispatcher.Dispatch(Repaint);
        wantsMouseMove = true;
        _warning = new GUIStyle(EditorStyles.boldLabel);
        _warning.normal.textColor = Color.red;

        var inspectorTypes = typeof(BaseInspector<>).GetAllGenericChildClasses()
            .Concat(typeof(BaseInspector<,>).GetAllGenericChildClasses())
            .Concat(typeof(BaseInspector<,,>).GetAllGenericChildClasses());
        foreach (var type in inspectorTypes)
        {
            var genericArguments = type.BaseType.GetGenericArguments();
            if(genericArguments.Any())
            {
                if (genericArguments[0].ContainsGenericParameters)
                {
                    _genericInspectors.Add((
                        genericArguments[0].GetGenericTypeDefinition(), 
                        new HashSet<Type>(genericArguments.Skip(1)), 
                        type,
                        new Dictionary<Type, object>()
                        ));
                }
                else
                {
                    _inspectors.Add((
                        genericArguments[0], 
                        new HashSet<Type>(genericArguments.Skip(1)), 
                        type, 
                        Activator.CreateInstance(type)
                        ));
                }
            }
        }
    }
    
    public void Inspect(object obj, object parent = null)
    {
        foreach (var field in obj.GetType().GetFields().OrderBy(f=>f.GetCustomAttribute<KeyAttribute>()?.IntKey ?? 0))
        {
            Inspect(obj, field, parent);
        }
    }
    
    public void Inspect(object obj, FieldInfo field, object parent = null)
    {
        var inspectable = field.GetCustomAttribute<InspectableAttribute>();
        if (inspectable == null) return;

        var attributes = field.GetCustomAttributes(false);

        var type = field.FieldType;
        var value = field.GetValue(obj);
        
        field.SetValue(obj, InspectMember(field.Name.SplitCamelCase(), value, parent ?? obj, type, false, attributes));
    }

    public object InspectMember(string label, object value, object parent, Type type, bool suppressContainer = false, params object[] attributes)
    {
        var style = _listItemStyle;
        object GetAttribute(Type attributeType)
        {
            return attributes.FirstOrDefault(attributeType.IsInstanceOfType);
        }

        if (value == null && type != typeof(string) && !(type.IsInterface || type.IsAbstract)) value = Activator.CreateInstance(type);

        var potentialInspectors = _inspectors
            .Where(pi =>
                pi.targetType == type &&
                pi.attributes.All(attributeType => GetAttribute(attributeType) != null));
        if (potentialInspectors.Any())
        {
            var preferredInspector = potentialInspectors.MaxBy(i => i.attributes.Count);
            var parameters = new[] {label, value, parent, this}
                .Concat(preferredInspector.attributes.Select(GetAttribute))
                .ToArray();
            value = preferredInspector.inspectorType.GetMethod("Inspect").Invoke(preferredInspector.instance, parameters);
            _listItemStyle = style;
            return value;
        }
        
        if (type.IsEnum)
        {
            var isflags = type.GetCustomAttributes<FlagsAttribute>().Any();
            var names = Enum.GetNames(type);
            value = InspectEnum(label, (int) value, isflags ? names.Skip(1).ToArray() : names, isflags);
            _listItemStyle = style;
            return value;
        }
        
        if (type.GetCustomAttribute<InspectableAttribute>() != null)
        {
            if(!suppressContainer)
                GUILayout.BeginVertical(GUI.skin.box);
            using (new HorizontalScope())
            {
                if(!suppressContainer)
                {
                    if (Foldout(_foldouts.Contains(value), label, true, EditorStyles.boldLabel))
                        _foldouts.Add(value);
                    else _foldouts.Remove(value);
                }
                if (type.IsInterface || type.IsAbstract)
                {
                    var subTypes = type.GetAllChildClasses().Where(t=>!(t.IsInterface || t.IsAbstract)).ToArray();
                    var selected = value != null ? Array.IndexOf(subTypes, value.GetType()) + 1 : 0;
                    var newSelection = Popup(selected,
                        subTypes.Select(t => t.Name.SplitCamelCase()).Prepend("None").ToArray());
                    if (newSelection != selected)
                        value = newSelection == 0 ? null : Activator.CreateInstance(subTypes[newSelection - 1]);
                }
                else if (value == null) value = Activator.CreateInstance(type);
            }

            if (value != null && (_foldouts.Contains(value) || suppressContainer)) Inspect(value, parent);
            if(!suppressContainer)
                GUILayout.EndVertical();
            _listItemStyle = style;
            return value;
        }
        
        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            var potentialGenericInspectors = _genericInspectors
                .Where(pi =>
                    pi.targetType == genericTypeDefinition &&
                    pi.attributes.All(attributeType => GetAttribute(attributeType) != null));
            if (potentialGenericInspectors.Any())
            {
                var genericTypeArguments = type.GenericTypeArguments;
                var preferredInspector = potentialGenericInspectors.MaxBy(i => i.attributes.Count);
                var genericInspectorType = preferredInspector.inspectorType.MakeGenericType(genericTypeArguments);
                if (!preferredInspector.instances.ContainsKey(genericInspectorType))
                    preferredInspector.instances[genericInspectorType] =
                        Activator.CreateInstance(genericInspectorType);
                var parameters = new[] {label, value, parent, this};
                value = genericInspectorType.GetMethod("Inspect").Invoke(preferredInspector.instances[genericInspectorType], parameters);
                _listItemStyle = style;
                return value;
            }
        }
        
        Debug.Log($"Field \"{label}\" has unknown type {type.Name}. No inspector was generated.");

        _listItemStyle = style;
        return value;
    }

    public int InspectEnum(string label, int value, string[] enumOptions, bool flags)
    {
        if (flags)
        {
            Space();
            LabelField(label, EditorStyles.boldLabel);
            using (new VerticalScope(GUI.skin.box))
            {
                for (var i = 0; i < enumOptions.Length; i++)
                {
                    using (var h = new HorizontalScope())
                    {
                        GUILayout.Label(enumOptions[i], GUILayout.Width(Width));
                        var set = Toggle(1 << i == (1 << i & value));
                        if (set)
                            value |= 1 << i;
                        else value &= ~(1 << i);
                    }
                }
            }

            return value;
        }

        using (var h = new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(Width));
            return Popup(value, enumOptions);
        }
    }

    public void OnGUI()
    {
        if (entry==null)
        {
            LabelField("No Entry Selected!");
            return;
        }
        
        if(entry is INamedEntry namedEntry)
            GUILayout.Label(namedEntry.EntryName, EditorStyles.boldLabel);
        
        _view = BeginScrollView(
            _view, 
            false,
            false,
            GUIStyle.none,
            GUI.skin.verticalScrollbar,
            GUI.skin.scrollView,
            GUILayout.Width(EditorGUIUtility.currentViewWidth),
            GUILayout.ExpandHeight(true));

        // Serialize inspected object so we can check whether it has actually changed
        RegisterResolver.Register();
        var previousBytes = MessagePackSerializer.Serialize<DatabaseEntry>(entry);
        var previousHash = previousBytes.GetHashSHA1();
        
        EditorGUI.BeginChangeCheck();

        var dataEntry = entry;
        
        using (new HorizontalScope())
        {
            // if(GUILayout.Button("Print JSON"))
            //     Debug.Log(entry.ToJson());
            if (GUILayout.Button("Delete Entry"))
            {
                CultCache.Remove(entry);
                entry = null;
                return;
            }

            if (GUILayout.Button("Clone Entry"))
            {
                var copy = MessagePackSerializer.Deserialize<DatabaseEntry>(previousBytes);
                copy.ID = Guid.NewGuid();
                CultCache.Add(copy);
            }
        }
        
        using (var h = new HorizontalScope())
        {
            GUILayout.Label("ID");
            LabelField(dataEntry.ID.ToString());
        }

        _listItemStyle = false;
        Inspect(entry);

        if (EditorGUI.EndChangeCheck())
        {
            if(previousHash != MessagePackSerializer.Serialize(entry).GetHashSHA1())
                CultCache.Add(entry);
        }
            
        EndScrollView();
    }
}

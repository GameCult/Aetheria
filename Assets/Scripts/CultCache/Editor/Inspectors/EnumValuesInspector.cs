using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Unity.Mathematics.math;
using static UnityEditor.EditorGUILayout;

public class EnumInspector<T> : BaseInspector<List<T>, InspectableEnumValuesAttribute>
{
    public override List<T> Inspect(string label, List<T> list, object parent, DatabaseInspector inspectorWindow, InspectableEnumValuesAttribute attribute)
    {
        var names = Enum.GetNames(attribute.EnumType);
        list ??= new List<T>();
        
        while(list.Count < names.Length) list.Add(Activator.CreateInstance<T>());
        while(list.Count > names.Length) list.RemoveAt(list.Count-1);

        for (var i = 0; i < names.Length; i++)
            inspectorWindow.InspectMember(names[i], list[i], parent, typeof(T));

        return list;
    }
}

public class EnumDictionaryInspector<E, T> : BaseInspector<EnumDictionary<E, T>> where E : Enum
{
    private static HashSet<object> _foldouts = new HashSet<object>();
    
    public override EnumDictionary<E, T> Inspect(string label, EnumDictionary<E, T> dict, object parent, DatabaseInspector inspectorWindow)
    {
        var names = Enum.GetNames(typeof(E));
        if (dict == null || dict.Values.Length != names.Length)
        {
            var newValue = new T[names.Length];
            if(dict != null && dict.Values.Length > 0)
                Array.Copy(dict.Values, newValue, min(dict.Values.Length, newValue.Length));
            dict.Values = newValue;
        }

        using (new VerticalScope(GUI.skin.box))
        {
            if (Foldout(_foldouts.Contains(dict), label, true, EditorStyles.boldLabel))
                _foldouts.Add(dict);
            else _foldouts.Remove(dict);
            if(_foldouts.Contains(dict))
            {
                for (var i = 0; i < names.Length; i++)
                {
                    using (new VerticalScope(DatabaseInspector.ListItemStyle))
                    {
                        dict.Values[i] = (T) inspectorWindow.InspectMember(names[i], dict.Values[i], parent, typeof(T));
                    }
                }
            }
        }

        return dict;
    }
}
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUILayout;

public class TypeInspector : BaseInspector<Type, InspectableTypeAttribute>
{
    public override Type Inspect(string label, Type value, object parent, DatabaseInspector inspectorWindow, InspectableTypeAttribute attribute)
    {
        var types = attribute.Type.GetAllChildClasses();
        var enumOptions = new[] {"None"}.Concat(types.Select(t => t.Name)).ToArray();
        var index = Array.IndexOf(types, value) + 1;

        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            index = Popup(index, enumOptions);
        }

        return index == 0 ? null : types[index - 1];
    }
}
using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUILayout;

public class BoolInspector : BaseInspector<bool>
{
    public override bool Inspect(string label, bool value, object parent, DatabaseInspector inspectorWindow)
    {
        using (var h = new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            return Toggle(value);
        }
    }
}

public class StringInspector : BaseInspector<string>
{
    public override string Inspect(string label, string value, object parent, DatabaseInspector inspectorWindow)
    {
        using (var h = new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            return DelayedTextField(value);
        }
    }
}

public class StringAreaInspector : BaseInspector<string, InspectableTextAttribute>
{
    public override string Inspect(string label, string value, object parent, DatabaseInspector inspectorWindow, InspectableTextAttribute attribute)
    {
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            EditorStyles.textArea.wordWrap = true;
            return DelayedTextField(value, EditorStyles.textArea, GUILayout.Height(100));
        }
    }
}
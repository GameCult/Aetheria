using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUILayout;

public class TextureInspector : BaseInspector<string, InspectableTextureAttribute>
{
    public override string Inspect(string label,
        string value,
        object parent,
        DatabaseInspector inspectorWindow,
        InspectableTextureAttribute attribute)
    {
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            return AssetDatabase.GetAssetPath(ObjectField(AssetDatabase.LoadAssetAtPath<Texture2D>(value), typeof(Texture2D), false));
        }
    }
}

public class GameObjectInspector : BaseInspector<string, InspectablePrefabAttribute>
{
    public override string Inspect(string label, string value, object parent, DatabaseInspector inspectorWindow, InspectablePrefabAttribute attribute)
    {
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            return AssetDatabase.GetAssetPath(ObjectField(AssetDatabase.LoadAssetAtPath<GameObject>(value), typeof(GameObject), false));
        }
    }
}

public class TextAssetInspector : BaseInspector<string, InspectableTextAssetAttribute>
{
    public override string Inspect(string label,
        string value,
        object parent,
        DatabaseInspector inspectorWindow,
        InspectableTextAssetAttribute attribute)
    {
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            return AssetDatabase.GetAssetPath(ObjectField(AssetDatabase.LoadAssetAtPath<TextAsset>(value), typeof(TextAsset), false));
        }
    }
}
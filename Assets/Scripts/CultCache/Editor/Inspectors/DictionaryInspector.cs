using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUILayout;

public class FloatDictionaryInspector : BaseInspector<Dictionary<Guid, float>, InspectableDatabaseLinkAttribute>
{
    public override Dictionary<Guid, float> Inspect(string label,
        Dictionary<Guid, float> value,
        object parent,
        DatabaseInspector inspectorWindow,
        InspectableDatabaseLinkAttribute attribute)
    {
        Space();
        LabelField(label, EditorStyles.boldLabel);

        using (var v = new VerticalScope(GUI.skin.box))
        {
            if (value.Count == 0)
            {
                using (new HorizontalScope(DatabaseInspector.ListItemStyle))
                {
                    GUILayout.Label("Drag from list to add item");
                }
            }
            foreach (var ingredient in value.ToArray())
            {
                using (new HorizontalScope(DatabaseInspector.ListItemStyle))
                {
                    var entry = DatabaseInspector.CultCache.Get(ingredient.Key);
                    if (entry == null)
                    {
                        value.Remove(ingredient.Key);
                        GUI.changed = true;
                        return value;
                    }
                    
                    if(entry is INamedEntry named)
                        GUILayout.Label(named.EntryName);
                    else GUILayout.Label(entry.ID.ToString());
                    value[ingredient.Key] = DelayedFloatField(value[ingredient.Key], GUILayout.Width(50));
                    
                    // var rect = GetControlRect(false, GUILayout.Width(EditorGUIUtility.singleLineHeight));
                    // GUI.DrawTexture(rect, Icons.Instance.minus, ScaleMode.StretchToFill, true, 1, LabelColor, 0, 0);
                    if (GUILayout.Button("-", GUILayout.Width((EditorGUIUtility.singleLineHeight - 3)*2), GUILayout.Height(EditorGUIUtility.singleLineHeight-3)))
                        value.Remove(ingredient.Key);
                }
            }

            if (v.rect.Contains(Event.current.mousePosition))
            {
                var dragData = DragAndDrop.GetGenericData("Item");
                var isId = dragData is Guid;
                var dragEntry = isId ? DatabaseInspector.CultCache.Get((Guid)dragData) : null;
                var correctType = attribute.EntryType.IsInstanceOfType(dragEntry);
                if(Event.current.type == EventType.DragUpdated)
                    DragAndDrop.visualMode = correctType ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                else if(Event.current.type == EventType.DragPerform)
                {
                    if (isId && correctType)
                    {
                        DragAndDrop.AcceptDrag();
                        value[(Guid)dragData] = 1;
                        GUI.changed = true;
                    }
                }
            }
        }

        return value;
    }
}

public class RangedFloatDictionaryInspector : BaseInspector<Dictionary<Guid, float>, InspectableDatabaseLinkAttribute, RangedFloatAttribute>
{
    public override Dictionary<Guid, float> Inspect(string label,
        Dictionary<Guid, float> value,
        object parent,
        DatabaseInspector inspectorWindow,
        InspectableDatabaseLinkAttribute link,
        RangedFloatAttribute range)
    {
        Space();
        LabelField(label, EditorStyles.boldLabel);

        using (var v = new VerticalScope(GUI.skin.box))
        {
            if (value.Count == 0)
            {
                using (new HorizontalScope(DatabaseInspector.ListItemStyle))
                {
                    GUILayout.Label("Drag from list to add item");
                }
            }
            foreach (var ingredient in value.ToArray())
            {
                using (new HorizontalScope(DatabaseInspector.ListItemStyle))
                {
                    var entry = DatabaseInspector.CultCache.Get(ingredient.Key);
                    if (entry == null)
                    {
                        value.Remove(ingredient.Key);
                        GUI.changed = true;
                        return value;
                    }
                    
                    if(entry is INamedEntry named)
                        GUILayout.Label(named.EntryName);
                    else GUILayout.Label(entry.ID.ToString());
                    value[ingredient.Key] = Slider(value[ingredient.Key], range.Min, range.Max);
                    
                    // var rect = GetControlRect(false, GUILayout.Width(EditorGUIUtility.singleLineHeight));
                    // GUI.DrawTexture(rect, Icons.Instance.minus, ScaleMode.StretchToFill, true, 1, LabelColor, 0, 0);
                    if (GUILayout.Button("-", GUILayout.Width((EditorGUIUtility.singleLineHeight - 3)*2), GUILayout.Height(EditorGUIUtility.singleLineHeight-3)))
                        value.Remove(ingredient.Key);
                }
            }

            if (v.rect.Contains(Event.current.mousePosition))
            {
                var dragData = DragAndDrop.GetGenericData("Item");
                var isId = dragData is Guid;
                var dragEntry = isId ? DatabaseInspector.CultCache.Get((Guid)dragData) : null;
                var correctType = link.EntryType.IsInstanceOfType(dragEntry);
                if(Event.current.type == EventType.DragUpdated)
                    DragAndDrop.visualMode = correctType ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                else if(Event.current.type == EventType.DragPerform)
                {
                    if (isId && correctType)
                    {
                        DragAndDrop.AcceptDrag();
                        value[(Guid)dragData] = range.Max;
                        GUI.changed = true;
                    }
                }
            }
        }

        return value;
    }
}

public class IntDictionaryInspector : BaseInspector<Dictionary<Guid, int>, InspectableDatabaseLinkAttribute>
{
    public override Dictionary<Guid, int> Inspect(string label,
        Dictionary<Guid, int> value,
        object parent,
        DatabaseInspector inspectorWindow,
        InspectableDatabaseLinkAttribute attribute)
    {
        Space();
        LabelField(label, EditorStyles.boldLabel);

        using (var v = new VerticalScope(GUI.skin.box))
        {
            if (value.Count == 0)
            {
                using (var h = new HorizontalScope(DatabaseInspector.ListItemStyle))
                {
                    GUILayout.Label("Drag from list to add item");
                }
            }
            foreach (var ingredient in value.ToArray())
            {
                using (var h = new HorizontalScope(DatabaseInspector.ListItemStyle))
                {
                    var entry = DatabaseInspector.CultCache.Get(ingredient.Key);
                    if (entry == null)
                    {
                        value.Remove(ingredient.Key);
                        GUI.changed = true;
                        return value;
                    }
                    if(entry is INamedEntry named)
                        GUILayout.Label(named.EntryName);
                    else GUILayout.Label(entry.ID.ToString());
                    
                    value[ingredient.Key] = DelayedIntField(value[ingredient.Key], GUILayout.Width(50));
                    
                    // var rect = GetControlRect(false, GUILayout.Width(EditorGUIUtility.singleLineHeight));
                    // GUI.DrawTexture(rect, Icons.Instance.minus, ScaleMode.StretchToFill, true, 1, LabelColor, 0, 0);
                    if (GUILayout.Button("-", GUILayout.Width((EditorGUIUtility.singleLineHeight - 3)*2), GUILayout.Height(EditorGUIUtility.singleLineHeight-3)) || value[ingredient.Key]==0)
                        value.Remove(ingredient.Key);
                }
            }

            if (v.rect.Contains(Event.current.mousePosition))
            {
                var dragData = DragAndDrop.GetGenericData("Item");
                var isId = dragData is Guid;
                var dragEntry = isId ? DatabaseInspector.CultCache.Get((Guid)dragData) : null;
                var correctType = attribute.EntryType.IsInstanceOfType(dragEntry);
                if(Event.current.type == EventType.DragUpdated)
                    DragAndDrop.visualMode = correctType ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                else if(Event.current.type == EventType.DragPerform)
                {
                    if (isId && correctType)
                    {
                        DragAndDrop.AcceptDrag();
                        value[(Guid)dragData] = 1;
                        GUI.changed = true;
                    }
                }
            }
        }

        return value;
    }
}
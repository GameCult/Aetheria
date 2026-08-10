using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUILayout;

public class DatabaseLinkInspector : BaseInspector<Guid, InspectableDatabaseLinkAttribute>
{
    public override Guid Inspect(string label, Guid value, object parent, DatabaseInspector inspectorWindow, InspectableDatabaseLinkAttribute link)
    {
        using (new HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(width));
            var valueEntry = DatabaseInspector.CultCache.Get(value);
            using (var h = new HorizontalScope(GUI.skin.box))
            {
                GUILayout.Label((valueEntry as INamedEntry)?.EntryName ?? valueEntry?.ID.ToString() ?? $"Drag and drop: {DatabaseListView.FormatTypeName(link.EntryType.Name)}");
                
                if (h.rect.Contains(Event.current.mousePosition) && (Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform))
                {
                    var guid = (Guid) DragAndDrop.GetGenericData("Item");
                    var dragEntry = DatabaseInspector.CultCache.Get(guid);
                    var dragValid = dragEntry != null && link.EntryType.IsInstanceOfType(dragEntry) && dragEntry != valueEntry;
                    if(Event.current.type == EventType.DragUpdated)
                        DragAndDrop.visualMode = dragValid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                    else if(Event.current.type == EventType.DragPerform)
                    {
                        if (dragValid)
                        {
                            DragAndDrop.AcceptDrag();
                            GUI.changed = true;
                            return guid;
                        }
                    }
                }
                if (GUILayout.Button("-", GUILayout.Width((EditorGUIUtility.singleLineHeight - 3)*2), GUILayout.Height(EditorGUIUtility.singleLineHeight-3)))
                {
                    return Guid.Empty;
                }
            }
        }

        return value;
    }
}

public class DatabaseLinkObjectInspector<T> : BaseInspector<DatabaseLink<T>> where T : DatabaseEntry
{
    public override DatabaseLink<T> Inspect(string label, DatabaseLink<T> value, object parent, DatabaseInspector inspectorWindow)
    {
        if (value == null)
            value = new DatabaseLink<T>();

        if(!string.IsNullOrEmpty(label))
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(width));
        }
        
        var valueEntry = value.Value;
        using (var h = string.IsNullOrEmpty(label) ? new HorizontalScope() : new HorizontalScope(GUI.skin.box))
        {
            GUILayout.Label((valueEntry as INamedEntry)?.EntryName ?? valueEntry?.ID.ToString() ?? $"Drag and drop: {DatabaseListView.FormatTypeName(typeof(T).Name)}");
            
            if (h.rect.Contains(Event.current.mousePosition) && (Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform))
            {
                var guid = (Guid) DragAndDrop.GetGenericData("Item");
                var dragEntry = DatabaseInspector.CultCache.Get(guid);
                var dragValid = dragEntry is T && dragEntry != valueEntry;
                if(Event.current.type == EventType.DragUpdated)
                    DragAndDrop.visualMode = dragValid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                else if(Event.current.type == EventType.DragPerform)
                {
                    if (dragValid)
                    {
                        DragAndDrop.AcceptDrag();
                        value.LinkID = guid;
                        return value;
                    }
                }
            }
            if(value.LinkID != Guid.Empty)
            {
                if (GUILayout.Button("-",
                    GUILayout.Width((EditorGUIUtility.singleLineHeight - 3) * 2),
                    GUILayout.Height(EditorGUIUtility.singleLineHeight - 3)))
                {
                    value.LinkID = Guid.Empty;
                }
            }
        }
        
        if(!string.IsNullOrEmpty(label))
        {
            GUILayout.EndHorizontal();
        }

        return value;
    }
}

public class DatabaseLinkListInspector : BaseInspector<List<Guid>, InspectableDatabaseLinkAttribute>
{
    private bool _listItemStyle;
    public GUIStyle ListItemStyle =>
        (_listItemStyle = !_listItemStyle) ? DatabaseListView.ListStyleEven : DatabaseListView.ListStyleOdd;
    
    public override List<Guid> Inspect(string label,
        List<Guid> value,
        object parent,
        DatabaseInspector inspectorWindow,
        InspectableDatabaseLinkAttribute link)
    {
        using (var v = new VerticalScope(GUI.skin.box))
        {
            GUILayout.Label(label, EditorStyles.boldLabel);
            if (value.Count == 0)
            {
                using (new HorizontalScope(ListItemStyle))
                {
                    GUILayout.Label($"Drag from list to add {link.EntryType.Name}");
                }
            }
            foreach (var guid in value.ToArray())
            {
                using (new HorizontalScope(ListItemStyle))
                {
                    var entry = DatabaseInspector.CultCache.Get(guid);
                    if (entry == null)
                    {
                        value.Remove(guid);
                        GUI.changed = true;
                    }
                    else
                    {
                        GUILayout.Label((entry as INamedEntry)?.EntryName ?? entry.ID.ToString());
                    
                        // var rect = GetControlRect(false, GUILayout.Width(EditorGUIUtility.singleLineHeight));
                        // GUI.DrawTexture(rect, Icons.Instance.minus, ScaleMode.StretchToFill, true, 1, LabelColor, 0, 0);
                        if (GUILayout.Button("-", GUILayout.Width((EditorGUIUtility.singleLineHeight - 3)*2), GUILayout.Height(EditorGUIUtility.singleLineHeight-3)))
                        {
                            value.Remove(guid);
                        }
                    }
                }
            }

            if (v.rect.Contains(Event.current.mousePosition))
            {
                var guid = DragAndDrop.GetGenericData("Item");
                var dragObj = DragAndDrop.GetGenericData("Item") is Guid ? DatabaseInspector.CultCache.Get((Guid) guid) : null;
                var dragValid = dragObj != null && link.EntryType.IsInstanceOfType(dragObj) && !value.Contains(dragObj.ID);
                if(Event.current.type == EventType.DragUpdated)
                    DragAndDrop.visualMode = dragValid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                else if(Event.current.type == EventType.DragPerform)
                {
                    if (dragValid)
                    {
                        DragAndDrop.AcceptDrag();
                        value.Add((Guid) guid);
                        GUI.changed = true;
                    }
                }
            }
        }

        return value;
    }
}
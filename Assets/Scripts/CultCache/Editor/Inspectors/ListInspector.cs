using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUILayout;

public class ListInspector<T> : BaseInspector<List<T>>
{
    private static HashSet<object> _foldouts = new HashSet<object>();
    
    public override List<T> Inspect(string label, List<T> list, object parent, DatabaseInspector inspectorWindow)
    {
        var listType = typeof(T);
        var inspectable = listType.GetCustomAttribute<InspectableAttribute>();
        // if (listType.GetCustomAttribute<InspectableAttribute>() != null)
        // {
            //Space();
        using (new VerticalScope(GUI.skin.box))
        {
            if (list == null)
            {
                list = new List<T>();
            }
            else
            {
                var sorted = true;
                var order = int.MinValue;
                foreach(var element in list)
                {
                    if (element == null)
                    {
                        list.Remove(element);
                        return list;
                    }
                    var elementType = element.GetType();
                    var elementOrder = elementType.GetCustomAttribute<OrderAttribute>()?.Order ?? 0;
                    if (elementOrder < order)
                        sorted = false;
                    order = elementOrder;
                }
            
                if (!sorted)
                {
                    list.Sort((x, y) => (x.GetType().GetCustomAttribute<OrderAttribute>()?.Order ?? 0).CompareTo(y.GetType().GetCustomAttribute<OrderAttribute>()?.Order ?? 0));
                }
            }
            if (Foldout(_foldouts.Contains(list), label, true, EditorStyles.boldLabel))
                _foldouts.Add(list);
            else _foldouts.Remove(list);
            if (_foldouts.Contains(list))
            {
                foreach (var o in list)
                {
                    if (o == null)
                    {
                        list.Remove(o);
                        break;
                    }
                    bool _tinted = false;
                    var originalColor = GUI.backgroundColor;
                    if (o is ITintInspector tint)
                    {
                        _tinted = true;
                        GUI.backgroundColor = tint.TintColor.ToColor();
                    }
                    
                    using (new VerticalScope(DatabaseInspector.ListItemStyle))
                    {
                        using (new HorizontalScope())
                        {
                            if (inspectable != null)
                            {
                                if (Foldout(_foldouts.Contains(o), o.ToString(), true, EditorStyles.boldLabel))
                                    _foldouts.Add(o);
                                else
                                    _foldouts.Remove(o);
                            }
                            else inspectorWindow.InspectMember("", o, parent, listType, true);
                            
                            if (GUILayout.Button("-", GUILayout.Width((EditorGUIUtility.singleLineHeight - 3)*2), GUILayout.Height(EditorGUIUtility.singleLineHeight-3)))
                            {
                                list.Remove(o);
                                break;
                            }
                        }
                        if(inspectable != null && _foldouts.Contains(o))
                            inspectorWindow.InspectMember("", o, parent, o.GetType(), true);
                    }
                    
                    if(_tinted)
                        GUI.backgroundColor = originalColor;
                }
                using (new HorizontalScope(DatabaseInspector.ListItemStyle))
                {
                    GUILayout.Label($"Add new {listType.GetFullName()}", GUILayout.ExpandWidth(true));
                    // var rect = GetControlRect(false, GUILayout.Width(EditorGUIUtility.singleLineHeight));
                    // GUI.DrawTexture(rect, Icons.Instance.plus, ScaleMode.StretchToFill, true, 1, LabelColor, 0, 0);
                    if (GUILayout.Button("+", GUILayout.Width((EditorGUIUtility.singleLineHeight - 3)*2), GUILayout.Height(EditorGUIUtility.singleLineHeight-3)))
                    {
                        if (listType.IsAbstract || listType.IsInterface)
                        {
                            var menu = new GenericMenu();
                            foreach (var childType in listType.GetAllChildClasses().Where(t=>!t.IsAbstract && !t.IsInterface))
                            {
                                menu.AddItem(new GUIContent(childType.Name), false, () =>
                                {
                                    var o = (T) Activator.CreateInstance(childType);
                                    _foldouts.Add(o);
                                    list.Add(o);
                                });
                            }
                            menu.ShowAsContext();
                        }
                        else
                        {
                            var o = Activator.CreateInstance<T>();
                            _foldouts.Add(o);
                            list.Add(o);
                        }
                    }
                }
            }
        }
        // }
        // else Debug.Log($"Field \"{label}\" is a list of non-Inspectable type {listType.Name}. No inspector was generated.");

        return list;
    }
}
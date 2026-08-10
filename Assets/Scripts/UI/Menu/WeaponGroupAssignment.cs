using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;

public class WeaponGroupAssignment : MonoBehaviour
{
    public RectTransform Container;
    public WeaponGroupElement GroupPrefab;
    public Color ActiveColor;
    public Color InactiveColor;
    
    Subject<(PointerEventData pointerEventData, int group)> _onBeginDrag;
    Subject<(PointerEventData pointerEventData, int group)> _onDrag;
    Subject<(PointerEventData pointerEventData, int group)> _onEndDrag;

    public List<WeaponGroupElement> Groups { get; } = new List<WeaponGroupElement>();

    public void Inspect(EquippedItem item)
    {
        var weapon = item.GetBehavior<Weapon>();
        if (weapon == null) throw new ArgumentException($"Attempted to inspect weapon groups on item \"{item.Data.Name}\" but no weapon is present!");
        var groupCount = ActionGameManager.Instance.Settings.GameplaySettings.WeaponGroupCount;
        for (int i = 0; i < groupCount; i++)
        {
            var i1 = i;
            bool groupContainsItem() => item.Entity.WeaponGroups[i1].items.Contains(item);
            var group = Instantiate(GroupPrefab, Container);
            Groups.Add(group);
            group.Label.text = $"G{i+1}";
            group.Label.color = groupContainsItem() ? ActiveColor : InactiveColor;
            group.Button.onClick.AddListener(() =>
            {
                if (groupContainsItem())
                {
                    item.Entity.WeaponGroups[i1].items.Remove(item);
                    item.Entity.WeaponGroups[i1].weapons.Remove(weapon);
                    group.Label.color = InactiveColor;
                }
                else
                {
                    item.Entity.WeaponGroups[i1].items.Add(item);
                    item.Entity.WeaponGroups[i1].weapons.Add(weapon);
                    group.Label.color = ActiveColor;
                }
            });
            
            group.BeginDragTrigger.OnBeginDragAsObservable()
                .Subscribe(data => _onBeginDrag?.OnNext((data, i1)));
            group.DragTrigger.OnDragAsObservable()
                .Subscribe(data => _onDrag?.OnNext((data, i1)));
            group.EndDragTrigger.OnEndDragAsObservable()
                .Subscribe(data => _onEndDrag?.OnNext((data, i1)));
        }
    }
    
    public UniRx.IObservable<(PointerEventData pointerEventData, int group)> OnBeginDragAsObservable() => 
        _onBeginDrag ?? (_onBeginDrag = new Subject<(PointerEventData pointerEventData, int group)>());
    public UniRx.IObservable<(PointerEventData pointerEventData, int group)> OnDragAsObservable() => 
        _onDrag ?? (_onDrag = new Subject<(PointerEventData pointerEventData, int group)>());
    public UniRx.IObservable<(PointerEventData pointerEventData, int group)> OnEndDragAsObservable() => 
        _onEndDrag ?? (_onEndDrag = new Subject<(PointerEventData pointerEventData, int group)>());
}

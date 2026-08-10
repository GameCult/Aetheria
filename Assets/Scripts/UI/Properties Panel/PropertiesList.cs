/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PropertiesList : PropertiesPanel
{
    public Image FoldoutIcon;
    public float FoldoutRotationDamping;
    public Button Button;
    public VerticalLayoutGroup LayoutGroup;
    public int FoldedPadding = -8;
    public int ExpandedPadding = 8;
    public event Action<bool> OnExpand;

    private bool _expanded = false;
    private float _targetFoldoutRotation = 0;
    private float _foldoutRotation = 0;

    public bool Expanded => _expanded;

    public new void Awake()
    {
        Button.onClick.AddListener(ToggleExpand);
        OnPropertyAdded += go => go.SetActive(_expanded);
        //SetExpanded(true, true);
    }

    public override void Update()
    {
        RefreshValues();
        _foldoutRotation =
            Mathf.Lerp(_foldoutRotation, _targetFoldoutRotation, FoldoutRotationDamping * Time.deltaTime);
        FoldoutIcon.transform.localRotation = Quaternion.Euler(0,0, _foldoutRotation);
    }

    public void ToggleExpand() => SetExpanded(!_expanded, false);

    public void SetExpanded(bool expanded, bool force)
    {
        _expanded = expanded;
        var padding = LayoutGroup.padding;
        padding = new RectOffset(padding.left, padding.right, padding.top, _expanded ? ExpandedPadding : FoldedPadding);
        LayoutGroup.padding = padding;
        foreach (var property in Properties) property.SetActive(_expanded);
        _targetFoldoutRotation = _expanded ? -90 : 0;
        if (force)
        {
            _foldoutRotation = _targetFoldoutRotation;
            FoldoutIcon.transform.localRotation = Quaternion.Euler(0,0, _foldoutRotation);
        }
        OnExpand?.Invoke(_expanded);
    }

    // public override PropertyLabel AddProperty(string name, Func<string> read = null, Action<PointerEventData> onClick = null, bool radio = false)
    // {
    //     var prop = base.AddProperty(name, read, onClick, radio);
    //     prop.gameObject.SetActive(false);
    //     return prop;
    // }
    //
    // public override PropertiesList AddList(string name)
    // {
    //     var list = base.AddList(name);
    //     list.gameObject.SetActive(false);
    //     return list;
    // }
    //
    // public override RectTransform AddSection(string name)
    // {
    //     var section = base.AddSection(name);
    //     section.gameObject.SetActive(false);
    //     return section;
    // }
    //
    // public override PropertyButton AddButton(string name, Action<PointerEventData> onClick)
    // {
    //     var button = base.AddButton(name, onClick);
    //     button.gameObject.SetActive(false);
    //     return button;
    // }
}

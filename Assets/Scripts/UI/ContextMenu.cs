/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UniRx;

public class ContextMenu : MonoBehaviour
{
    public Canvas Canvas;
    public RectTransform Root;
    public ClickCatcher CancelClickCatcher;
    public ContextMenuOption OptionPrefab;
    public ContextMenu DropMenuPrefab;
    public ContextMenu Parent;
    public bool ForceDirectionRight;
    public Vector3 ForcePosition;

    public RectTransform ContentRoot;

    public float OptionHeight;
    public float PaddingHeight;
    
    private List<GameObject> _options = new List<GameObject>();
    private bool _hasDropdown;
    private bool _dropdownRight;
    private GameObject _dropdown;
    private ContextMenuOption _currentDropdown;
    private IDisposable _backgroundClickSubscription;

    private void OnEnable()
    {
        if (CancelClickCatcher != null)
            _backgroundClickSubscription = CancelClickCatcher.OnClick.Subscribe(data =>
            {
                End();
            });
    }

    private void OnDisable()
    {
        _backgroundClickSubscription?.Dispose();
        _backgroundClickSubscription = null;
    }

    private void End()
    {
        if (_dropdown != null)
            Destroy(_dropdown);
        CancelClickCatcher.gameObject.SetActive(false);
        gameObject.SetActive(false);
        ActionGameManager.Instance?.Input.Global.Enable();
    }

    public void Clear()
    {
        // Destroy previous options
        foreach(var option in _options)
            Destroy(option);
        _options.Clear();
        _hasDropdown = false;
    }

    public void AddOption(string text, Action action, bool enabled = true)
    {
        var optionButton = Instantiate(OptionPrefab, ContentRoot);
        optionButton.Label.text = text;
        optionButton.Button.onClick.AddListener(() =>
        {
            if(Parent!=null)
                Parent.End();
            else
                End();
            action();
        });
        optionButton.Hover.OnEnter += data =>
        {
            if (_dropdown != null)
                Destroy(_dropdown);
        };
        optionButton.Arrow.gameObject.SetActive(false);
        optionButton.Button.interactable = enabled;
        _options.Add(optionButton.gameObject);
    }

    public void AddDropdown(string text, IEnumerable<(string text, Action action, bool enabled)> options)
    {
        _hasDropdown = true;
        var optionButton = Instantiate(OptionPrefab, ContentRoot);
        optionButton.Label.text = text;
        optionButton.Hover.OnEnter += data =>
        {
            if (_currentDropdown != optionButton)
            {
                if(_dropdown!=null)
                    Destroy(_dropdown);
                var drop = Instantiate(DropMenuPrefab, Root);
                _dropdown = drop.gameObject;
                foreach (var option in options)
                    drop.AddOption(option.text, option.action, option.enabled);
                drop.Parent = this;
                drop.ForceDirectionRight = _dropdownRight;
                Vector3[] corners = new Vector3[4];
                ((RectTransform) optionButton.transform).GetWorldCorners(corners);
                drop.ForcePosition = corners[_dropdownRight ? 2 : 1];
                drop.Show();
            }
        };
        _options.Add(optionButton.gameObject);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        var rect = transform as RectTransform;
        var pivot = rect.pivot;
        var canvas = Parent != null ? Parent.Canvas : Canvas;
        var mousePosition = Mouse.current.position.ReadValue();
        if (Parent!=null)
            pivot.x = ForceDirectionRight ? 0 : 1;
        else
            pivot.x = mousePosition.x > Screen.width - rect.sizeDelta.x * canvas.scaleFactor ? 1 : 0;
        var scaleFactor = canvas.scaleFactor;
        _dropdownRight = mousePosition.x < Screen.width - rect.sizeDelta.x * scaleFactor * 2;
        var pos = Parent==null ? mousePosition.y : ForcePosition.y;
        var space = (PaddingHeight + _options.Count * OptionHeight) * scaleFactor;
        var deltaY = pos < space ? space - pos : PaddingHeight / 2 * scaleFactor;
        pivot.y = 1;//pivotTop ? 1 : 0;
        rect.pivot = pivot;
        rect.position = (Parent != null ? ForcePosition : (Vector3) mousePosition) + Vector3.up * deltaY;
        if (Parent==null)
            CancelClickCatcher.gameObject.SetActive(true);
        ActionGameManager.Instance?.Input.Global.Disable();
    }
}

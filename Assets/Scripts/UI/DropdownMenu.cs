/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UniRx;

public class DropdownMenu : MonoBehaviour
{
    public Canvas Canvas;
    public ClickCatcher CancelClickCatcher;
    public PropertyButton OptionPrefab;

    public RectTransform ContentRoot;

    public float OptionHeight;
    public float PaddingHeight;
    
    private List<GameObject> _options = new List<GameObject>();
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
    }

    public void AddOption(string text, Action action, bool selected = false, bool enabled = true)
    {
        var optionButton = Instantiate(OptionPrefab, ContentRoot);
        optionButton.Label.text = text;
        optionButton.Button.onClick.AddListener(() =>
        {
            action();
            End();
        });
        optionButton.Button.interactable = enabled;
        //optionButton.Button..CurrentState = !enabled ? FlatButtonState.Disabled : selected ? FlatButtonState.Selected : FlatButtonState.Unselected;
        _options.Add(optionButton.gameObject);
    }

    public void Show(RectTransform parent)
    {
        var rect = transform as RectTransform;
        var pivot = rect.pivot;
        pivot.x = 0;
        var scaleFactor = Canvas.scaleFactor;
        var corners = new Vector3[4];
        parent.GetWorldCorners(corners);
        var pivotTop = corners[0].y > (PaddingHeight + _options.Count * OptionHeight) * scaleFactor;
        pivot.y = pivotTop ? 1 : 0;
        rect.pivot = pivot;
        rect.sizeDelta = parent.sizeDelta;
        rect.position = pivotTop ? corners[0] : corners[1];
        CancelClickCatcher.gameObject.SetActive(true);
        ActionGameManager.Instance?.Input.Global.Disable();
    }
}

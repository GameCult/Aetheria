/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UniRx;

public class ConfirmationDialog : PropertiesPanel
{
    public ClickCatcher CancelClickCatcher;
    public GameObject ButtonGroup;
    public Button Cancel;
    public Button Confirm;
    public TextMeshProUGUI ConfirmText;
    public TextMeshProUGUI CancelText;
    
    public bool LockDialog { get; set; }

    private Action _onCancel;
    private Action _onConfirm;

    private void Start()
    {
        Cancel.onClick.AddListener(() => End());
        Confirm.onClick.AddListener(() => End(true));
        if (CancelClickCatcher != null)
            CancelClickCatcher.OnClick.Subscribe(data =>
            {
                if (!LockDialog) End();
            });

        OnPropertyAdded += go =>
        {
            go.SetActive(true);
            go.transform.SetSiblingIndex(Title.transform.GetSiblingIndex() + 1);
        };
        gameObject.SetActive(false);
    }
    
    public void End(bool success = false)
    {
        if(success) _onConfirm?.Invoke();
        else _onCancel?.Invoke();
        CancelClickCatcher?.gameObject.SetActive(false);
        gameObject.SetActive(false);
        ActionGameManager.Instance?.Input.Global.Enable();
    }

    public void MoveToCursor()
    {
        transform.position = Mouse.current.position.ReadValue();
    }

    public void Show(Action onConfirm = null, Action onCancel = null, string confirmText = "OK", string cancelText = "Cancel")
    {
        gameObject.SetActive(true);
        
        _onConfirm = onConfirm;
        Confirm.gameObject.SetActive(onConfirm!=null);
        ConfirmText.text = confirmText;
        
        _onCancel = onCancel;
        Cancel.gameObject.SetActive(onCancel!=null);
        CancelText.text = cancelText;
        
        ButtonGroup.SetActive(onConfirm!=null || onCancel!=null);
        
        CancelClickCatcher?.gameObject.SetActive(true);
        ActionGameManager.Instance?.Input.Global.Disable();
    }
}

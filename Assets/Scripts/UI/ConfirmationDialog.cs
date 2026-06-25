/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ConfirmationDialog : MonoBehaviour
{
    public TextMeshProUGUI Title;
    public RectTransform Content;
    public Property Property;
    public InputField InputField;
    public ClickCatcher CancelClickCatcher;
    public GameObject ButtonGroup;
    public Button Cancel;
    public Button Confirm;
    public TextMeshProUGUI ConfirmText;
    public TextMeshProUGUI CancelText;

    public bool LockDialog { get; set; }

    private readonly List<GameObject> _entries = new();
    private readonly List<Action> _refreshers = new();
    private Action _onCancel;
    private Action _onConfirm;
    private Action _enableGlobalInput;
    private Action _disableGlobalInput;

    private void Start()
    {
        Cancel.onClick.AddListener(() => End());
        Confirm.onClick.AddListener(() => End(true));
        if (CancelClickCatcher != null)
        {
            CancelClickCatcher.OnClick.Subscribe(_ =>
            {
                if (!LockDialog)
                {
                    End();
                }
            });
        }

        gameObject.SetActive(false);
    }

    private void Update()
    {
        RefreshValues();
    }

    public void Clear()
    {
        foreach (var entry in _entries)
        {
            Destroy(entry);
        }

        _entries.Clear();
        _refreshers.Clear();
        Title.text = "Properties";
    }

    public Property AddProperty(Func<string> read)
    {
        if (read == null)
        {
            throw new ArgumentException("Attempted to add confirmation-dialog property with null read function!");
        }

        var property = Instantiate(Property, Content ?? (RectTransform)transform);
        _refreshers.Add(() => property.Label.text = read());
        property.Label.text = read();
        RegisterEntry(property.gameObject);
        return property;
    }

    public Property AddProperty(string text)
    {
        var property = Instantiate(Property, Content ?? (RectTransform)transform);
        property.Label.text = text;
        RegisterEntry(property.gameObject);
        return property;
    }

    public void SetInputGate(Action enableGlobalInput, Action disableGlobalInput)
    {
        _enableGlobalInput = enableGlobalInput;
        _disableGlobalInput = disableGlobalInput;
    }

    public void AddField(string name, Func<string> read, Action<string> write)
    {
        var field = Instantiate(InputField, Content ?? (RectTransform)transform);
        field.Label.text = name;
        field.Field.contentType = TMP_InputField.ContentType.Standard;
        field.Field.onValueChanged.AddListener(write.Invoke);
        _refreshers.Add(() => SyncField(field.Field, read()));
        SyncField(field.Field, read());
        RegisterEntry(field.gameObject);
    }

    public void AddField(string name, Func<int> read, Action<int> write)
    {
        var field = Instantiate(InputField, Content ?? (RectTransform)transform);
        field.Label.text = name;
        field.Field.contentType = TMP_InputField.ContentType.IntegerNumber;
        field.Field.onValueChanged.AddListener(value =>
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                write(parsed);
            }
        });
        _refreshers.Add(() => SyncField(field.Field, read().ToString(CultureInfo.InvariantCulture)));
        SyncField(field.Field, read().ToString(CultureInfo.InvariantCulture));
        RegisterEntry(field.gameObject);
    }

    public void End(bool success = false)
    {
        if (success)
        {
            _onConfirm?.Invoke();
        }
        else
        {
            _onCancel?.Invoke();
        }

        CancelClickCatcher?.gameObject.SetActive(false);
        gameObject.SetActive(false);
        _enableGlobalInput?.Invoke();
    }

    public void MoveToCursor()
    {
        transform.position = Mouse.current.position.ReadValue();
    }

    public void Show(Action onConfirm = null, Action onCancel = null, string confirmText = "OK", string cancelText = "Cancel")
    {
        RefreshValues();
        gameObject.SetActive(true);

        _onConfirm = onConfirm;
        Confirm.gameObject.SetActive(onConfirm != null);
        ConfirmText.text = confirmText;

        _onCancel = onCancel;
        Cancel.gameObject.SetActive(onCancel != null);
        CancelText.text = cancelText;

        ButtonGroup.SetActive(onConfirm != null || onCancel != null);

        CancelClickCatcher?.gameObject.SetActive(true);
        _disableGlobalInput?.Invoke();
    }

    private void RegisterEntry(GameObject entry)
    {
        _entries.Add(entry);
        entry.SetActive(true);
        entry.transform.SetSiblingIndex(Title.transform.GetSiblingIndex() + _entries.Count);
    }

    private void RefreshValues()
    {
        foreach (var refresh in _refreshers)
        {
            refresh();
        }
    }

    private static void SyncField(TMP_InputField field, string value)
    {
        if (!string.Equals(field.text, value, StringComparison.Ordinal))
        {
            field.SetTextWithoutNotify(value);
        }
    }
}

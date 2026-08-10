/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using UniRx.Triggers;
using UnityEngine;

public class MenuPanel : MonoBehaviour
{
    public ActionGameManager GameManager;
    public RectTransform TabButtons;
    public Color ActiveTabColor;
    public Color InactiveTabColor;

    public event Action<MenuTab> TabChanged;

    private Dictionary<MenuTab, MenuTabButton> _tabs = new Dictionary<MenuTab, MenuTabButton>();
    private MenuTabButton _current;
    
    public MenuTab CurrentTab { get; private set; }

    public void ShowTab(MenuTab tab)
    {
        gameObject.SetActive(true);
        
        if (_current == _tabs[tab]) return;
        
        if(_current != null)
        {
            _current.TabContents.SetActive(false);
            _current.Text.color = InactiveTabColor;
        }

        CurrentTab = tab;
        _current = _tabs[tab];
        
        _current.TabContents.SetActive(true);
        _current.Text.color = ActiveTabColor;
        
        TabChanged?.Invoke(tab);
    }

    private void OnEnable()
    {
        foreach (var tabButton in _tabs.Values)
        {
            tabButton.gameObject.SetActive(!tabButton.RequireDock || GameManager.DockedEntity != null);
        }
        _tabs[MenuTab.Local].gameObject.SetActive((GameManager.DockedEntity as OrbitalEntity)?.Story!=null);
    }

    private void Awake()
    {
        foreach (var tabButton in TabButtons.GetComponentsInChildren<MenuTabButton>())
        {
            tabButton.TabContents.SetActive(false);
            tabButton.Text.color = InactiveTabColor;
            _tabs.Add(tabButton.Tab, tabButton);
            tabButton.Button.onClick.AddListener(() => ShowTab(tabButton.Tab));
        }
    }

    // void Start()
    // {
    //     ShowTab(MenuTab.Inventory);
    // }
}

public enum MenuTab
{
    Map,
    Inventory,
    Trade,
    Galaxy,
    Local
}
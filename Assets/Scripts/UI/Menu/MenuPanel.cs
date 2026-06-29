/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.EveRuntime;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
using UnityEngine;
using UnityEngine.UIElements;

public class MenuPanel : MonoBehaviour
{
    [Serializable]
    private sealed class MenuTabBinding
    {
        public MenuTab Tab;
        public GameObject TabContents;
        public string Label;
        public bool RequireDock;
    }

    public RectTransform TabButtons;
    [SerializeField] private MenuTabBinding[] TabBindings = Array.Empty<MenuTabBinding>();

    public event Action<MenuTab> TabChanged;

    private readonly Dictionary<MenuTab, MenuTabBinding> _tabs = new Dictionary<MenuTab, MenuTabBinding>();
    private MenuTabBinding _current;
    private UIDocument _tabSurfaceDocument;
    private readonly AetheriaEveUnitySurfaceChrome _tabSurfaceChrome = new AetheriaEveUnitySurfaceChrome();
    private AetheriaUnityObservedEntityIndex _observedEntityIndex;
    private AetheriaUnityObservedDockingIndex _observedDockingIndex;
    
    public MenuTab CurrentTab { get; private set; }

    public void SetObservedEntityIndex(AetheriaUnityObservedEntityIndex observedEntityIndex)
    {
        if (!ReferenceEquals(_observedEntityIndex, observedEntityIndex))
        {
            _observedDockingIndex = null;
        }

        _observedEntityIndex = observedEntityIndex;
    }

    public void ShowTab(MenuTab tab)
    {
        gameObject.SetActive(true);

        if (!_tabs.TryGetValue(tab, out var next))
            return;

        if (_current == next)
        {
            RenderTabSurface();
            return;
        }
        
        if(_current != null)
        {
            _current.TabContents.SetActive(false);
        }

        CurrentTab = tab;
        _current = next;
        
        _current.TabContents.SetActive(true);
        RenderTabSurface();
        
        TabChanged?.Invoke(tab);
    }

    private void OnEnable()
    {
        if (TabButtons != null)
            TabButtons.gameObject.SetActive(false);

        RenderTabSurface();
    }

    private void Awake()
    {
        foreach (var tabBinding in TabBindings)
        {
            if (tabBinding?.TabContents == null)
                continue;

            tabBinding.TabContents.SetActive(false);
            _tabs[tabBinding.Tab] = tabBinding;
        }
    }

    private void OnDisable()
    {
        HideTabSurface();
    }

    private void OnDestroy()
    {
        if (_tabSurfaceDocument != null)
        {
            AetheriaEveUnitySurfaceHost.DestroyDocument(_tabSurfaceDocument);
            _tabSurfaceDocument = null;
        }

        _observedDockingIndex = null;
    }

    private void RenderTabSurface()
    {
        if (_tabs.Count == 0)
            return;

        if (TabButtons != null)
            TabButtons.gameObject.SetActive(false);

        _tabSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _tabSurfaceDocument,
            "Aetheria Runtime Menu Tabs Surface",
            AetheriaRuntimeMenuTabsSurfaceBuilder.Build(
                ToRuntimeTabKey(CurrentTab),
                ResolveVisibleTabs()
                    .Select(tabBinding => new AetheriaRuntimeMenuTabModelOption(
                        ToRuntimeTabKey(tabBinding.Tab),
                        GetTabLabel(tabBinding),
                        (int)tabBinding.Tab)),
                DateTime.UtcNow.ToString("O")),
            HandleTabSurfaceCommand,
            _tabSurfaceChrome);
    }

    private void HandleTabSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (!AetheriaRuntimeMenuTabsSurfaceCommands.TryRead(request, out var command))
        {
            Debug.LogWarning($"Unknown runtime menu tab command: {request?.Command}");
            return;
        }

        foreach (var tab in _tabs.Keys)
        {
            if (command.Kind == AetheriaRuntimeMenuTabCommandKind.SelectTab &&
                string.Equals(command.TabKey, ToRuntimeTabKey(tab), StringComparison.Ordinal))
            {
                ShowTab(tab);
                return;
            }
        }

        Debug.LogWarning($"Unknown runtime menu tab command: {request?.Command}");
    }

    private void HideTabSurface()
    {
        if (_tabSurfaceDocument == null)
            return;

        AetheriaEveUnitySurfaceHost.Hide(_tabSurfaceDocument);
    }

    private MenuTabBinding[] ResolveVisibleTabs()
    {
        var isDocked = TryResolveObservedDockingIndex(out var dockingIndex) &&
                       dockingIndex.TryResolveCurrentDocking(out var docking) &&
                       docking.IsDocked;
        return _tabs.Values
            .Where(tabBinding => !tabBinding.RequireDock || isDocked)
            .Where(tabBinding => tabBinding.Tab != MenuTab.Local || isDocked)
            .OrderBy(tabBinding => (int)tabBinding.Tab)
            .ToArray();
    }

    private bool TryResolveObservedDockingIndex(out AetheriaUnityObservedDockingIndex dockingIndex)
    {
        dockingIndex = null;
        if (_observedEntityIndex == null)
            return false;

        dockingIndex = _observedDockingIndex ??= new AetheriaUnityObservedDockingIndex(_observedEntityIndex);
        return true;
    }

    private static string GetTabLabel(MenuTabBinding tabBinding)
    {
        return string.IsNullOrWhiteSpace(tabBinding.Label)
            ? tabBinding.Tab.ToString()
            : tabBinding.Label;
    }

    private static string ToRuntimeTabKey(MenuTab tab)
    {
        return AetheriaRuntimeMenuTabsSurfaceBuilder.NormalizeTabKey(tab.ToString());
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

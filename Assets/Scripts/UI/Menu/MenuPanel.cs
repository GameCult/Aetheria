/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Eve.Surface;
using GameCult.Eve.UnityUIToolkit;
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

    private const string MenuTabsSurfaceType = "surface-state";
    private const string MenuTabsSurfaceSchema = "gamecult.eve.surface.v1";
    private const string MenuTabsSurfaceProviderId = "aetheria";
    private const string MenuTabsSurfaceProviderKind = "runtime.menu";
    private const string MenuTabsSurfaceId = "aetheria.runtime_menu.tabs";

    public ActionGameManager GameManager;
    public RectTransform TabButtons;
    [SerializeField] private MenuTabBinding[] TabBindings = Array.Empty<MenuTabBinding>();

    public event Action<MenuTab> TabChanged;

    private readonly Dictionary<MenuTab, MenuTabBinding> _tabs = new Dictionary<MenuTab, MenuTabBinding>();
    private MenuTabBinding _current;
    private UIDocument _tabSurfaceDocument;
    
    public MenuTab CurrentTab { get; private set; }

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
            Destroy(_tabSurfaceDocument.gameObject);
            _tabSurfaceDocument = null;
        }
    }

    private void RenderTabSurface()
    {
        if (_tabs.Count == 0)
            return;

        if (TabButtons != null)
            TabButtons.gameObject.SetActive(false);

        var document = ResolveTabSurfaceDocument();
        document.gameObject.SetActive(true);

        var root = document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1;
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.top = 0;
        root.style.right = 0;
        root.style.bottom = 0;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.FlexStart;
        root.style.paddingTop = 16;
        root.pickingMode = PickingMode.Ignore;

        var shell = new VisualElement();
        shell.style.minWidth = 420;
        shell.style.maxWidth = 760;
        shell.style.backgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.94f);
        shell.style.borderTopLeftRadius = 8;
        shell.style.borderTopRightRadius = 8;
        shell.style.borderBottomLeftRadius = 8;
        shell.style.borderBottomRightRadius = 8;
        shell.style.paddingLeft = 16;
        shell.style.paddingRight = 16;
        shell.style.paddingTop = 12;
        shell.style.paddingBottom = 12;
        shell.style.borderLeftWidth = 1;
        shell.style.borderRightWidth = 1;
        shell.style.borderTopWidth = 1;
        shell.style.borderBottomWidth = 1;
        shell.style.borderLeftColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderRightColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderTopColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.style.borderBottomColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        shell.pickingMode = PickingMode.Position;
        root.Add(shell);

        var lowerer = new EveUiToolkitSurfaceLowerer();
        shell.Add(lowerer.Lower(BuildTabSurfaceDefinition(), HandleTabSurfaceCommand));
    }

    private void HandleTabSurfaceCommand(EveSurfaceCommandRequest request)
    {
        foreach (var tab in _tabs.Keys)
        {
            if (string.Equals(request.Command, GetTabCommand(tab), StringComparison.Ordinal))
            {
                ShowTab(tab);
                return;
            }
        }

        Debug.LogWarning($"Unknown runtime menu tab command: {request.Command}");
    }

    private void HideTabSurface()
    {
        if (_tabSurfaceDocument == null)
            return;

        _tabSurfaceDocument.rootVisualElement.Clear();
        _tabSurfaceDocument.gameObject.SetActive(false);
    }

    private UIDocument ResolveTabSurfaceDocument()
    {
        if (_tabSurfaceDocument != null)
            return _tabSurfaceDocument;

        var host = new GameObject("Aetheria Runtime Menu Tabs Surface");
        host.transform.SetParent(transform, false);
        var document = host.AddComponent<UIDocument>();
        document.sortingOrder = 1000;
        host.SetActive(false);
        _tabSurfaceDocument = document;
        return document;
    }

    private EveSurfaceDocument BuildTabSurfaceDefinition()
    {
        var visibleTabs = ResolveVisibleTabs();
        var commands = visibleTabs
            .Select(tabBinding => new EveCommandTemplate(
                GetTabCommand(tabBinding.Tab),
                GetTabLabel(tabBinding),
                "unity-uitoolkit"))
            .ToArray();

        var buttons = visibleTabs
            .Select(tabBinding =>
            {
                var label = GetTabLabel(tabBinding);
                if (tabBinding.Tab == CurrentTab)
                    label = $"{label} *";

                return Button(
                    $"{MenuTabsSurfaceId}.{tabBinding.Tab.ToString().ToLowerInvariant()}",
                    label,
                    GetTabCommand(tabBinding.Tab));
            })
            .ToArray();

        return new EveSurfaceDocument(
            MenuTabsSurfaceType,
            MenuTabsSurfaceSchema,
            MenuTabsSurfaceProviderId,
            MenuTabsSurfaceProviderKind,
            "Runtime Menu Tabs",
            version: 1,
            DateTime.UtcNow.ToString("O"),
            new EveSurfaceTree(
                MenuTabsSurfaceId,
                Node(
                    $"{MenuTabsSurfaceId}.root",
                    "surface",
                    Array.Empty<(string Key, string Value)>(),
                    Text(
                        $"{MenuTabsSurfaceId}.current",
                        $"Current: {CurrentTab}"),
                    ButtonRow($"{MenuTabsSurfaceId}.tabs", buttons)),
                Array.Empty<EveStyleToken>()),
            commands);
    }

    private MenuTabBinding[] ResolveVisibleTabs()
    {
        return _tabs.Values
            .Where(tabBinding => !tabBinding.RequireDock || GameManager.DockedEntity != null)
            .Where(tabBinding => tabBinding.Tab != MenuTab.Local || (GameManager.DockedEntity as OrbitalEntity)?.Story != null)
            .OrderBy(tabBinding => (int)tabBinding.Tab)
            .ToArray();
    }

    private static string GetTabLabel(MenuTabBinding tabBinding)
    {
        return string.IsNullOrWhiteSpace(tabBinding.Label)
            ? tabBinding.Tab.ToString()
            : tabBinding.Label;
    }

    private static string GetTabCommand(MenuTab tab)
    {
        return $"aetheria.runtime_menu.tab.{tab.ToString().ToLowerInvariant()}";
    }

    private static EveSurfaceComponent Text(string id, string value)
    {
        return Node(id, "text", new[] { ("value", value) });
    }

    private static EveSurfaceComponent Button(string id, string label, string command)
    {
        return Node(id, "control.button", new[] { ("label", label), ("command", command) });
    }

    private static EveSurfaceComponent ButtonRow(
        string id,
        params EveSurfaceComponent[] children)
    {
        return Node(id, "row", Array.Empty<(string Key, string Value)>(), children);
    }

    private static EveSurfaceComponent Node(
        string id,
        string kind,
        IEnumerable<(string Key, string Value)> props,
        params EveSurfaceComponent[] children)
    {
        return new EveSurfaceComponent(
            id,
            kind,
            props.ToDictionary(prop => prop.Key, prop => prop.Value, StringComparer.Ordinal),
            children ?? Array.Empty<EveSurfaceComponent>());
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

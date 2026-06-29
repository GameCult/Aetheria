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
    private const string TabSurfaceId = "aetheria.runtime_menu.tabs";
    private const string TabCommandPrefix = "aetheria.runtime_menu.tab.";

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
            AetheriaEveUnitySurfaceHost.DestroyDocument(_tabSurfaceDocument);
            _tabSurfaceDocument = null;
        }
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
            BuildTabSurfaceDocument(
                ToRuntimeTabKey(CurrentTab),
                ResolveVisibleTabs(),
                DateTime.UtcNow.ToString("O")),
            HandleTabSurfaceCommand,
            _tabSurfaceChrome);
    }

    private void HandleTabSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (!TryReadTabSurfaceCommand(request, out var tabKey))
        {
            Debug.LogWarning($"Unknown runtime menu tab command: {request?.Command}");
            return;
        }

        foreach (var tab in _tabs.Keys)
        {
            if (string.Equals(tabKey, ToRuntimeTabKey(tab), StringComparison.Ordinal))
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
        var isDocked = TryResolveCurrentDocking(out var docking) &&
                       docking.IsDocked;
        return _tabs.Values
            .Where(tabBinding => !tabBinding.RequireDock || isDocked)
            .Where(tabBinding => tabBinding.Tab != MenuTab.Local || isDocked)
            .OrderBy(tabBinding => (int)tabBinding.Tab)
            .ToArray();
    }

    private static bool TryResolveCurrentDocking(out AetheriaRuntimeCurrentDockingDocument docking)
    {
        docking = null;
        try
        {
            docking = AetheriaUnityRuntimeClientProvider.CurrentDockingState("unity-runtime-menu-tabs");
            return docking != null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to read Aetheria runtime docking for menu tabs: {ex.Message}");
            return false;
        }
    }

    private static string GetTabLabel(MenuTabBinding tabBinding)
    {
        return string.IsNullOrWhiteSpace(tabBinding.Label)
            ? tabBinding.Tab.ToString()
            : tabBinding.Label;
    }

    private static string ToRuntimeTabKey(MenuTab tab)
    {
        return NormalizeTabKey(tab.ToString());
    }

    private static string NormalizeTabKey(string tabKey)
    {
        return string.IsNullOrWhiteSpace(tabKey)
            ? "unknown"
            : tabKey.Trim().ToLowerInvariant();
    }

    private static string TabCommandFor(string tabKey)
    {
        return $"{TabCommandPrefix}{NormalizeTabKey(tabKey)}";
    }

    private static bool TryReadTabSurfaceCommand(EveSurfaceCommandRequest request, out string tabKey)
    {
        tabKey = "";
        if (request == null ||
            !string.Equals(request.SurfaceId, TabSurfaceId, StringComparison.Ordinal))
            return false;

        var commandText = request.Operation?.OperationId ?? "";
        if (!commandText.StartsWith(TabCommandPrefix, StringComparison.Ordinal))
            return false;

        tabKey = commandText.Substring(TabCommandPrefix.Length);
        return !string.IsNullOrWhiteSpace(tabKey);
    }

    private static AetheriaRuntimeSurfaceDocument BuildTabSurfaceDocument(
        string currentTabKey,
        IEnumerable<MenuTabBinding> visibleTabs,
        string updatedAtUtc)
    {
        var normalizedCurrent = NormalizeTabKey(currentTabKey);
        var tabs = (visibleTabs ?? Array.Empty<MenuTabBinding>())
            .Where(tab => tab != null)
            .OrderBy(tab => (int)tab.Tab)
            .Select(tab =>
            {
                var key = ToRuntimeTabKey(tab.Tab);
                var label = GetTabLabel(tab);
                return new
                {
                    Key = key,
                    Label = label,
                    Selected = string.Equals(key, normalizedCurrent, StringComparison.Ordinal)
                };
            })
            .ToArray();

        return new AetheriaRuntimeSurfaceDocument(
            providerId: "aetheria",
            providerKind: "runtime.menu",
            title: "Runtime Menu Tabs",
            version: 1,
            updatedAtUtc: updatedAtUtc ?? "",
            surface: new AetheriaRuntimeSurfaceTree(
                TabSurfaceId,
                TabSurfaceNode(
                    $"{TabSurfaceId}.root",
                    "surface",
                    Array.Empty<(string Key, string Value)>(),
                    TabSurfaceText(
                        $"{TabSurfaceId}.current",
                        $"Current: {normalizedCurrent}"),
                    TabSurfaceButtonRow(
                        $"{TabSurfaceId}.tabs",
                        tabs
                            .Select(tab => TabSurfaceButton(
                                $"{TabSurfaceId}.{SafeSurfaceId(tab.Key)}",
                                tab.Selected ? $"{tab.Label} *" : tab.Label,
                                TabCommandFor(tab.Key)))
                            .ToArray())),
                Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
            commands: tabs
                .Select(tab => new AetheriaRuntimeSurfaceCommandTemplate(
                    TabCommandFor(tab.Key),
                    tab.Label,
                    AetheriaRuntimeSurfaceCommandTemplate.CultMeshTransport))
                .ToArray());
    }

    private static string SafeSurfaceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "empty";

        return new string(value
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray()).Trim('-');
    }

    private static AetheriaRuntimeSurfaceComponent TabSurfaceText(string id, string value)
    {
        return TabSurfaceNode(id, "text", new[] { ("value", value ?? "") });
    }

    private static AetheriaRuntimeSurfaceComponent TabSurfaceButton(string id, string label, string command)
    {
        return TabSurfaceNode(id, "control.button", new[] { ("label", label ?? ""), ("command", command ?? "") });
    }

    private static AetheriaRuntimeSurfaceComponent TabSurfaceButtonRow(
        string id,
        params AetheriaRuntimeSurfaceComponent[] children)
    {
        return TabSurfaceNode(id, "row", Array.Empty<(string Key, string Value)>(), children);
    }

    private static AetheriaRuntimeSurfaceComponent TabSurfaceNode(
        string id,
        string kind,
        IEnumerable<(string Key, string Value)> props,
        params AetheriaRuntimeSurfaceComponent[] children)
    {
        return new AetheriaRuntimeSurfaceComponent(
            id,
            kind,
            props.ToDictionary(prop => prop.Key, prop => prop.Value ?? "", StringComparer.Ordinal),
            children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>());
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

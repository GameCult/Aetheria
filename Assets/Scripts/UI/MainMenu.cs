using System;
using System.Collections.Generic;
using GameCult.Aetheria.EveRuntime;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UiButton = UnityEngine.UIElements.Button;

public class MainMenu : MonoBehaviour
{
    public VolumeCloudRenderer CloudRenderer;
    public GameSettings Settings;
    public ConfirmationDialog Dialog;
    public bool InGame;
    public float HoverSpacing = 8f;
    public float HoverAnimationDuration = 0.16f;

    private UIDocument _menuSurfaceDocument;
    private Func<bool> _canOpenRuntimeInputScreen;
    private Action _openRuntimeInputScreen;
    private readonly List<MenuHoverButton> _hoverButtons = new List<MenuHoverButton>();
    private readonly AetheriaEveUnitySurfaceChrome _menuSurfaceChrome = new AetheriaEveUnitySurfaceChrome
    {
        UseShell = false,
        RootAlignItems = Align.FlexStart,
        RootJustifyContent = Justify.FlexStart,
        RootPaddingLeft = 96f,
        RootPaddingTop = 92f,
        RootPaddingRight = 24f,
        RootPaddingBottom = 24f,
        RootBackgroundColor = new Color(0f, 0f, 0f, 0f),
        RootPickingMode = PickingMode.Position
    };
    
    void Start()
    {
        ShowMain(false);
    }

    void Update()
    {
        UpdateHoverButtons();
    }

    private void ShowMain(bool animateFromRight = true)
    {
        RenderMenuSurface(
            ResolveMainMenuSurface(AetheriaRuntimeMainMenuCommands.RootSurfaceId),
            HandleMainSurfaceCommand,
            animateFromRight);
    }

    private void HandleMainSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (!AetheriaRuntimeMainMenuSurfaceCommands.TryRead(request, out var command))
        {
            Debug.LogWarning("Unknown main menu command.");
            return;
        }

        switch (command.Kind)
        {
            case AetheriaRuntimeMainMenuCommandKind.ContinueRun:
                if (ResolveSectorMap(CurrentStateBoot()) == null)
                {
                    Debug.LogWarning("Main-menu Continue requested without a typed Aetheria sector-map projection.");
                    ShowMain(false);
                    return;
                }

                ContinueGame();
                return;
            case AetheriaRuntimeMainMenuCommandKind.NewGame:
                StartNewGame();
                return;
            case AetheriaRuntimeMainMenuCommandKind.ShowSettings:
                ShowSettings(true);
                return;
            case AetheriaRuntimeMainMenuCommandKind.Quit:
                Application.Quit();
                return;
            default:
                Debug.LogWarning($"Unhandled main menu command kind: {command.Kind}");
                return;
        }
    }

    private void StartNewGame()
    {
        if (!TryStartDaemonObservedGame("Starting Daemon Run"))
        {
            ShowMain(false);
        }
    }

    private bool TryStartDaemonObservedGame(string title)
    {
        var stateBoot = CurrentStateBoot();
        var sectorMap = ResolveSectorMap(stateBoot);
        if (sectorMap == null)
        {
            Debug.LogWarning($"Cannot start Aetheria observer scene without a typed Aetheria sector-map projection in {stateBoot.StateFilePath}.");
            return false;
        }

        var generatorState = "Loading daemon sector";
        Action<string> setState = s => generatorState = s;

        HideMenuSurface();
        Dialog.Clear();
        Dialog.Title.text = title;
        Dialog.AddProperty(() => generatorState);
        Dialog.Show();

        setState($"Observing sector-map frame {sectorMap.FrameId}");
        SceneManager.LoadScene("ARPG");
        return true;
    }

    private static AetheriaRuntimeStateBootReport CurrentStateBoot()
    {
        return AetheriaRuntimeStateBoot.Inspect(AetheriaUnityRuntimePaths.GameDataDirectory);
    }

    private AetheriaRuntimeSectorMapDocument ResolveSectorMap(AetheriaRuntimeStateBootReport stateBoot)
    {
        if (!stateBoot.SupportsLocalStateFileRead || !stateBoot.StateFileExists)
        {
            return null;
        }

        try
        {
            return AetheriaUnityRuntimeClientProvider
                .RuntimeState(stateBoot, "unity-main-menu")
                .SectorMap
                .Latest();
        }
        catch (KeyNotFoundException ex) when (IsMissingDaemonFrame(ex))
        {
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to bind typed Aetheria sector-map state for the main menu: {ex}");
            return null;
        }
    }

    private static bool IsMissingDaemonFrame(KeyNotFoundException ex)
    {
        return ex?.Message?.Contains("daemon:aetheria.frame.latest.v1", StringComparison.Ordinal) == true;
    }

    private void ContinueGame()
    {
        if (!TryStartDaemonObservedGame("Continuing Daemon Run"))
        {
            ShowMain(false);
        }
    }

    private void ShowSettings(bool animateFromRight = true)
    {
        RenderMenuSurface(
            ResolveMainMenuSurface(AetheriaRuntimeMainMenuCommands.SettingsSurfaceId),
            HandleSettingsSurfaceCommand,
            animateFromRight);
    }

    private void ShowInputSettings(bool animateFromRight = true)
    {
        RenderMenuSurface(
            ResolveMainMenuSurface(AetheriaRuntimeMainMenuCommands.InputSettingsSurfaceId),
            HandleInputSettingsSurfaceCommand,
            animateFromRight);
    }

    private void ShowPlayerSettingsSurface(bool animateFromRight = true)
    {
        RenderMenuSurface(
            ResolveMainMenuSurface(AetheriaRuntimeMainMenuCommands.PlayerSettingsSurfaceId),
            HandlePlayerSettingsSurfaceCommand,
            animateFromRight);
    }

    private void ShowVerseSettingsSurface(bool animateFromRight = true)
    {
        RenderMenuSurface(
            ResolveMainMenuSurface(AetheriaRuntimeMainMenuCommands.VerseSettingsSurfaceId),
            HandleVerseSettingsSurfaceCommand,
            animateFromRight);
    }

    private AetheriaRuntimeSurfaceDocument ResolveMainMenuSurface(string surfaceId)
    {
        var stateBoot = CurrentStateBoot();
        return AetheriaUnityRuntimeClientProvider
            .RuntimeState(stateBoot, "unity-main-menu")
            .MainMenuSurface(surfaceId, CanOpenRuntimeInputScreen(), InGame)
            .Latest();
    }

    private void RenderMenuSurface(
        AetheriaRuntimeSurfaceDocument document,
        Action<EveSurfaceCommandRequest> commandHandler,
        bool animateFromRight)
    {
        _menuSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _menuSurfaceDocument,
            "Aetheria Menu Surface",
            document,
            commandHandler,
            _menuSurfaceChrome,
            sortingOrder: 1000);

        ApplyMenuStyles(_menuSurfaceDocument.rootVisualElement, IsRootMainMenu(document));
    }

    private static bool IsRootMainMenu(AetheriaRuntimeSurfaceDocument document)
    {
        return string.Equals(
            document?.Surface?.Id,
            AetheriaRuntimeMainMenuCommands.RootSurfaceId,
            StringComparison.Ordinal);
    }

    private void UpdateHoverButtons()
    {
        for (var i = _hoverButtons.Count - 1; i >= 0; i--)
        {
            var hover = _hoverButtons[i];
            if (hover.Button == null)
            {
                _hoverButtons.RemoveAt(i);
                continue;
            }

            var direction = hover.Hovering && hover.Button.enabledInHierarchy ? 1f : -1f;
            hover.Lerp = Mathf.Clamp01(hover.Lerp + Time.unscaledDeltaTime / Mathf.Max(0.01f, HoverAnimationDuration) * direction);
            hover.Button.style.letterSpacing = HoverSpacing * hover.Lerp;
        }
    }

    private void ApplyMenuStyles(VisualElement root, bool rootMainMenu)
    {
        if (root == null)
            return;

        StyleMenuTree(root, rootMainMenu);

        var surface = rootMainMenu
            ? FindElement(root, $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.root")
            : FindFirstKind(root, "surface");
        if (surface != null)
        {
            surface.style.flexGrow = 0f;
            surface.style.width = rootMainMenu ? 720f : 560f;
            surface.style.alignItems = Align.FlexStart;
            surface.pickingMode = PickingMode.Position;
        }

        var title = FindElement(root, $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.title") as Label;
        if (title != null)
        {
            title.style.fontSize = 86f;
            title.style.color = new Color(0.82f, 0.95f, 1f, 0.96f);
            title.style.unityFontStyleAndWeight = FontStyle.Normal;
            title.style.unityTextAlign = TextAnchor.MiddleRight;
            title.style.marginLeft = 0f;
            title.style.marginBottom = -12f;
            title.style.width = 720f;
        }

        var subtitle = FindElement(root, $"{AetheriaRuntimeMainMenuCommands.RootSurfaceId}.subtitle") as Label;
        if (subtitle != null)
        {
            subtitle.style.fontSize = 39f;
            subtitle.style.color = new Color(0.82f, 0.95f, 1f, 0.92f);
            subtitle.style.unityFontStyleAndWeight = FontStyle.Normal;
            subtitle.style.unityTextAlign = TextAnchor.MiddleRight;
            subtitle.style.marginTop = 0f;
            subtitle.style.marginBottom = 36f;
            subtitle.style.width = 720f;
        }

        RegisterHoverButtons(root);
    }

    private void StyleMenuTree(VisualElement element, bool rootMainMenu)
    {
        if (element == null)
            return;

        element.pickingMode = PickingMode.Position;

        if (element is UiButton button)
            StyleMenuButton(button, rootMainMenu);
        else if (element is TextField textField)
            StyleMenuTextField(textField, rootMainMenu);
        else if (element is Label label)
            StyleMenuLabel(label, rootMainMenu);
        else
            StyleMenuContainer(element, rootMainMenu);

        foreach (var child in element.Children())
            StyleMenuTree(child, rootMainMenu);
    }

    private static void StyleMenuContainer(VisualElement element, bool rootMainMenu)
    {
        element.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        element.style.borderLeftWidth = 0f;
        element.style.borderRightWidth = 0f;
        element.style.borderTopWidth = 0f;
        element.style.borderBottomWidth = 0f;
        element.style.marginLeft = 0f;
        element.style.marginTop = 0f;
        element.style.marginBottom = 0f;

        if (element.ClassListContains("eve-row"))
        {
            element.style.flexDirection = FlexDirection.Row;
            element.style.flexWrap = Wrap.Wrap;
            element.style.alignItems = Align.Center;
            element.style.marginTop = 2f;
            element.style.marginBottom = 6f;
            return;
        }

        if (element.ClassListContains("eve-card"))
        {
            element.style.flexDirection = FlexDirection.Column;
            element.style.alignItems = Align.FlexStart;
            element.style.width = rootMainMenu ? 680f : 540f;
            element.style.marginBottom = 18f;
            element.style.paddingTop = 0f;
            element.style.paddingBottom = 2f;
            return;
        }

        if (element.ClassListContains("eve-metric") || element.ClassListContains("eve-field"))
        {
            element.style.flexDirection = FlexDirection.Row;
            element.style.alignItems = Align.Center;
            element.style.width = rootMainMenu ? 320f : 520f;
            element.style.marginBottom = 5f;
            return;
        }

        if (element.ClassListContains("eve-kind-control-text"))
        {
            element.style.flexDirection = FlexDirection.Row;
            element.style.alignItems = Align.Center;
            element.style.width = rootMainMenu ? 520f : 520f;
            element.style.marginBottom = rootMainMenu ? 6f : 10f;
            return;
        }

        if (element.ClassListContains("eve-surface-root") || element.ClassListContains("eve-kind-surface"))
        {
            element.style.flexDirection = FlexDirection.Column;
            element.style.alignItems = Align.FlexStart;
            element.style.width = rootMainMenu ? 680f : 560f;
            return;
        }

        element.style.flexDirection = FlexDirection.Column;
        element.style.alignItems = Align.FlexStart;
    }

    private static void StyleMenuLabel(Label label, bool rootMainMenu)
    {
        label.style.color = new Color(0.86f, 0.98f, 1f, 0.94f);
        label.style.unityFontStyleAndWeight = FontStyle.Normal;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginLeft = 0f;
        label.style.marginTop = 0f;
        label.style.marginBottom = 0f;

        if (label.ClassListContains("eve-title"))
        {
            label.style.fontSize = rootMainMenu ? 24f : 24f;
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
            label.style.marginTop = rootMainMenu ? 0f : 2f;
            label.style.marginBottom = rootMainMenu ? 0f : 10f;
            label.style.maxWidth = rootMainMenu ? 620f : 520f;
            return;
        }

        if (label.ClassListContains("eve-muted"))
        {
            label.style.fontSize = rootMainMenu ? 18f : 15f;
            label.style.color = new Color(0.78f, 0.92f, 0.98f, 0.84f);
            label.style.width = rootMainMenu ? 120f : 190f;
            label.style.marginRight = 12f;
            return;
        }

        if (label.ClassListContains("eve-value"))
        {
            label.style.fontSize = rootMainMenu ? 20f : 16f;
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
            label.style.maxWidth = rootMainMenu ? 360f : 300f;
            return;
        }

        label.style.fontSize = rootMainMenu ? 24f : 16f;
        label.style.maxWidth = rootMainMenu ? 620f : 520f;
        label.style.marginBottom = rootMainMenu ? 0f : 10f;
    }

    private static void StyleMenuTextField(TextField field, bool rootMainMenu)
    {
        field.style.width = rootMainMenu ? 360f : 520f;
        field.style.height = rootMainMenu ? 30f : 28f;
        field.style.marginLeft = 0f;
        field.style.marginTop = 0f;
        field.style.marginBottom = rootMainMenu ? 4f : 8f;
        field.style.color = new Color(0.86f, 0.98f, 1f, 0.96f);
        field.style.fontSize = rootMainMenu ? 18f : 15f;
        field.pickingMode = PickingMode.Position;

        field.labelElement.style.minWidth = rootMainMenu ? 100f : 190f;
        field.labelElement.style.width = rootMainMenu ? 100f : 190f;
        field.labelElement.style.color = new Color(0.78f, 0.92f, 0.98f, 0.84f);
    }

    private static void StyleMenuButton(UiButton button, bool rootMainMenu)
    {
        button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        button.style.borderLeftWidth = 0f;
        button.style.borderRightWidth = 0f;
        button.style.borderTopWidth = 0f;
        button.style.borderBottomWidth = 0f;
        button.style.color = new Color(0.86f, 0.98f, 1f, 0.96f);
        button.style.fontSize = rootMainMenu ? 24f : 16f;
        button.style.unityFontStyleAndWeight = FontStyle.Normal;
        button.style.unityTextAlign = TextAnchor.MiddleLeft;
        button.style.width = rootMainMenu ? 220f : 230f;
        button.style.height = rootMainMenu ? 32f : 24f;
        button.style.marginLeft = 0f;
        button.style.marginTop = 0f;
        button.style.marginRight = rootMainMenu ? 0f : 14f;
        button.style.marginBottom = rootMainMenu ? 0f : 4f;
        button.style.paddingLeft = 0f;
        button.style.paddingRight = 0f;
        button.style.paddingTop = 0f;
        button.style.paddingBottom = 0f;
        button.style.letterSpacing = 0f;
        button.focusable = true;
        button.pickingMode = PickingMode.Position;
    }

    private void RegisterHoverButtons(VisualElement root)
    {
        foreach (var button in FindButtons(root))
        {
            var hover = new MenuHoverButton(button);
            _hoverButtons.Add(hover);
            button.RegisterCallback<PointerEnterEvent>(_ => hover.Hovering = true);
            button.RegisterCallback<PointerLeaveEvent>(_ => hover.Hovering = false);
        }
    }

    private static IEnumerable<UiButton> FindButtons(VisualElement root)
    {
        if (root == null)
            yield break;

        if (root is UiButton button)
            yield return button;

        foreach (var child in root.Children())
        {
            foreach (var nested in FindButtons(child))
                yield return nested;
        }
    }

    private static VisualElement FindFirstKind(VisualElement root, string kind)
    {
        if (root == null)
            return null;

        if (root.ClassListContains($"eve-kind-{kind}"))
            return root;

        foreach (var child in root.Children())
        {
            var match = FindFirstKind(child, kind);
            if (match != null)
                return match;
        }

        return null;
    }

    private static VisualElement FindElement(VisualElement root, string name)
    {
        if (root == null)
            return null;

        if (string.Equals(root.name, name, StringComparison.Ordinal))
            return root;

        foreach (var child in root.Children())
        {
            var match = FindElement(child, name);
            if (match != null)
                return match;
        }

        return null;
    }

    private void HandleSettingsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (!AetheriaRuntimeMainMenuSurfaceCommands.TryRead(request, out var command))
        {
            Debug.LogWarning("Unknown settings menu command.");
            return;
        }

        switch (command.Kind)
        {
            case AetheriaRuntimeMainMenuCommandKind.ShowPlayerSettings:
                ShowPlayerSettingsSurface(true);
                return;
            case AetheriaRuntimeMainMenuCommandKind.ShowVerseSettings:
                ShowVerseSettingsSurface(true);
                return;
            case AetheriaRuntimeMainMenuCommandKind.ShowInputSettings:
                ShowInputSettings(true);
                return;
            case AetheriaRuntimeMainMenuCommandKind.BackToMain:
                ShowMain(false);
                return;
            default:
                Debug.LogWarning($"Unhandled settings menu command kind: {command.Kind}");
                return;
        }
    }

    private void HandleInputSettingsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (!AetheriaRuntimeMainMenuSurfaceCommands.TryRead(request, out var command))
        {
            Debug.LogWarning("Unknown input settings command.");
            return;
        }

        switch (command.Kind)
        {
            case AetheriaRuntimeMainMenuCommandKind.BackToSettings:
                ShowSettings(false);
                return;
            case AetheriaRuntimeMainMenuCommandKind.OpenRuntimeInputScreen:
                if (!TryOpenRuntimeInputScreen())
                {
                    ShowInputSettings(false);
                }

                return;
            default:
                Debug.LogWarning($"Unhandled input settings command kind: {command.Kind}");
                return;
        }
    }

    private void HandlePlayerSettingsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (!AetheriaRuntimeMainMenuSurfaceCommands.TryRead(request, out var command))
        {
            Debug.LogWarning("Unknown player-settings command.");
            return;
        }

        switch (command.Kind)
        {
            case AetheriaRuntimeMainMenuCommandKind.BackToSettings:
                ShowSettings(false);
                return;
            case AetheriaRuntimeMainMenuCommandKind.PlayerSettingsCommand:
                SendKnownAetheriaEveCommand(request, "player-settings");
                ShowPlayerSettingsSurface(false);
                return;
            default:
                Debug.LogWarning($"Unhandled player-settings command kind: {command.Kind}");
                return;
        }
    }

    private void HandleVerseSettingsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (!AetheriaRuntimeMainMenuSurfaceCommands.TryRead(request, out var command))
        {
            Debug.LogWarning("Unknown verse-settings command.");
            return;
        }

        switch (command.Kind)
        {
            case AetheriaRuntimeMainMenuCommandKind.BackToSettings:
                ShowSettings(false);
                return;
            case AetheriaRuntimeMainMenuCommandKind.ClientTargetCommand:
                RequestClientTargetCommand(request);
                ShowVerseSettingsSurface(false);
                return;
            case AetheriaRuntimeMainMenuCommandKind.VerseHostCommand:
                SendKnownAetheriaEveCommand(request, "Verse-host");
                ShowVerseSettingsSurface(false);
                return;
            default:
                Debug.LogWarning($"Unhandled verse-settings command kind: {command.Kind}");
                return;
        }
    }

    private void HideMenuSurface()
    {
        AetheriaEveUnitySurfaceHost.Hide(_menuSurfaceDocument);
    }

    public void SetRuntimeInputScreenShell(Func<bool> canOpenRuntimeInputScreen, Action openRuntimeInputScreen)
    {
        _canOpenRuntimeInputScreen = canOpenRuntimeInputScreen;
        _openRuntimeInputScreen = openRuntimeInputScreen;
    }

    private bool CanOpenRuntimeInputScreen()
    {
        return InGame &&
               _canOpenRuntimeInputScreen?.Invoke() == true;
    }

    private bool TryOpenRuntimeInputScreen()
    {
        if (!CanOpenRuntimeInputScreen())
            return false;

        HideMenuSurface();
        gameObject.SetActive(false);
        _openRuntimeInputScreen?.Invoke();
        return true;
    }

    private static void RequestClientTargetCommand(EveSurfaceCommandRequest request)
    {
        try
        {
            if (!AetheriaRuntimeClientTargetSurfaceCommands.TryRequest(
                    AetheriaState.At(AetheriaUnityRuntimePaths.GameDataDirectory).ClientTarget,
                    request,
                    out _))
            {
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to update typed Aetheria client target: {ex}");
        }
    }

    private void SendKnownAetheriaEveCommand(
        EveSurfaceCommandRequest request,
        string label)
    {
        if (request == null)
            return;

        var stateBoot = CurrentStateBoot();
        if (!CanSendLocalEveCommand(stateBoot, label))
            return;

        try
        {
            var submitted = AetheriaUnityRuntimeClientProvider
                .Ui(stateBoot, "unity-main-menu")
                .SurfaceCommandAsync(request, "unity-main-menu")
                .GetAwaiter()
                .GetResult();

            Debug.Log($"Submitted Aetheria {label} Eve operation: {submitted.OperationId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send Aetheria {label} Eve command: {ex}");
        }
    }

    private static bool CanSendLocalEveCommand(
        AetheriaRuntimeStateBootReport stateBoot,
        string label)
    {
        if (!stateBoot.SupportsLocalStateFileRead || !stateBoot.StateFileExists)
        {
            Debug.LogWarning(
                $"Cannot send {label} command because the active target is not a readable local Verse state file.");
            return false;
        }

        return true;
    }

    private void OnDestroy()
    {
        if (_menuSurfaceDocument != null)
        {
            Destroy(_menuSurfaceDocument.gameObject);
            _menuSurfaceDocument = null;
        }
    }

    private sealed class MenuHoverButton
    {
        public MenuHoverButton(UiButton button)
        {
            Button = button;
        }

        public UiButton Button { get; }
        public bool Hovering { get; set; }
        public float Lerp { get; set; }
    }
}

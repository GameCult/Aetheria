using System;
using GameCult.Aetheria.EveRuntime;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class MainMenu : MonoBehaviour
{
    public VolumeCloudRenderer CloudRenderer;
    public GameSettings Settings;
    public ConfirmationDialog Dialog;
    public bool InGame;

    private UIDocument _menuSurfaceDocument;
    private AetheriaClient _client;
    private string _clientStatePath;
    private AetheriaRuntimeCatalogSnapshot _catalog;
    private Func<bool> _canOpenRuntimeInputScreen;
    private Action _openRuntimeInputScreen;
    private readonly AetheriaEveUnitySurfaceChrome _menuSurfaceChrome = new AetheriaEveUnitySurfaceChrome
    {
        RootAlignItems = Align.Center,
        RootJustifyContent = Justify.Center,
        RootPaddingLeft = 24f,
        RootPaddingRight = 24f,
        RootPaddingTop = 24f,
        RootPaddingBottom = 24f,
        RootBackgroundColor = new Color(0f, 0f, 0f, 0.6f),
        Width = 560f,
        MinWidth = 0f,
        MaxWidth = 560f,
        PaddingLeft = 20f,
        PaddingRight = 20f,
        PaddingTop = 20f,
        PaddingBottom = 20f,
        BorderRadius = 0f,
        BorderWidth = 0f,
        BackgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.96f)
    };
    
    void Start()
    {
        ShowMain();
    }

    private void ShowMain()
    {
        var stateBoot = CurrentStateBoot();
        var sectorMap = LatestSectorMap(stateBoot);
        var verseHost = LatestVerseHostSettings(stateBoot);
        var playerSettings = LatestPlayerSettings(stateBoot);
        RenderMenuSurface(
            AetheriaRuntimeMainMenuSurfaceBuilder.BuildRoot(
                AetheriaRuntimeMainMenuSurfaceBuilder.ProjectRoot(
                    stateBoot,
                    sectorMap,
                    verseHost,
                    playerSettings,
                    CanOpenRuntimeInputScreen(),
                    InGame,
                    DateTime.UtcNow.ToString("O"))),
            HandleMainSurfaceCommand);
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
                if (LatestSectorMap(CurrentStateBoot()) == null)
                {
                    Debug.LogWarning("Main-menu Continue requested without a typed Aetheria sector-map projection.");
                    ShowMain();
                    return;
                }

                ContinueGame();
                return;
            case AetheriaRuntimeMainMenuCommandKind.NewGame:
                StartNewGame();
                return;
            case AetheriaRuntimeMainMenuCommandKind.ShowSettings:
                ShowSettings();
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
            ShowMain();
        }
    }

    private bool TryStartDaemonObservedGame(string title)
    {
        var stateBoot = CurrentStateBoot();
        var sectorMap = LatestSectorMap(stateBoot);
        if (sectorMap == null)
        {
            Debug.LogWarning($"Cannot start Aetheria observer scene without a typed Aetheria sector-map projection in {stateBoot.StateFilePath}.");
            return false;
        }

        var generatorState = "Loading runtime catalog";
        Action<string> setState = s => generatorState = s;

        HideMenuSurface();
        Dialog.Clear();
        Dialog.Title.text = title;
        Dialog.AddProperty(() => generatorState);
        Dialog.Show();

        setState($"Observing sector-map frame {sectorMap.FrameId}");
        var backgroundSettings = sectorMap.IsTutorial ? Settings.TutorialBackgroundSettings : Settings.SectorBackgroundSettings;
        backgroundSettings.NoisePosition = sectorMap.GenerationSeed == 0 ? 1 : sectorMap.GenerationSeed;
        var runtimeCatalog = OpenRuntimeCatalog(stateBoot);
        if (runtimeCatalog == null)
        {
            Debug.LogWarning($"Cannot start Aetheria observer scene without a runtime catalog in {stateBoot.StateFilePath}.");
            return false;
        }

        AetheriaUnityObservedRunProjection.Project(
            sectorMap,
            backgroundSettings,
            runtimeCatalog,
            Debug.Log);
        SceneManager.LoadScene("ARPG");
        return true;
    }

    private static AetheriaRuntimeStateBootReport CurrentStateBoot()
    {
        return AetheriaRuntimeStateBoot.Inspect(AetheriaUnityRuntimePaths.GameDataDirectory);
    }

    private AetheriaRuntimeSectorMapDocument LatestSectorMap(AetheriaRuntimeStateBootReport stateBoot)
    {
        if (!stateBoot.SupportsLocalStateFileRead || !stateBoot.StateFileExists)
        {
            return null;
        }

        try
        {
            return ResolveClient(stateBoot)
                .SectorMapAsync()
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to read typed Aetheria sector-map state for the main menu: {ex}");
            return null;
        }
    }

    private AetheriaRuntimePlayerSettingsSnapshot LatestPlayerSettings(AetheriaRuntimeStateBootReport stateBoot)
    {
        if (!stateBoot.SupportsLocalStateFileRead || !stateBoot.StateFileExists)
            return null;

        try
        {
            return ResolveClient(stateBoot)
                .PlayerSettingsAsync()
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to read typed Aetheria player settings for the main menu: {ex}");
            return null;
        }
    }

    private AetheriaRuntimeVerseHostSettingsSnapshot LatestVerseHostSettings(AetheriaRuntimeStateBootReport stateBoot)
    {
        if (!stateBoot.SupportsLocalStateFileRead || !stateBoot.StateFileExists)
            return null;

        try
        {
            return ResolveClient(stateBoot)
                .VerseHostSettingsAsync()
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to read typed Aetheria Verse host settings for the main menu: {ex}");
            return null;
        }
    }

    private AetheriaRuntimeCatalogSnapshot OpenRuntimeCatalog(AetheriaRuntimeStateBootReport stateBoot)
    {
        if (!stateBoot.SupportsLocalStateFileRead || !stateBoot.StateFileExists)
            return null;

        if (_catalog != null)
            return _catalog;

        try
        {
            _catalog = ResolveClient(stateBoot).OpenRuntimeCatalog();
            return _catalog;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to read typed Aetheria runtime catalog for the main menu: {ex}");
            return null;
        }
    }

    private void ContinueGame()
    {
        if (!TryStartDaemonObservedGame("Continuing Daemon Run"))
        {
            ShowMain();
        }
    }

    private void ShowSettings()
    {
        RenderMenuSurface(
            AetheriaRuntimeMainMenuSurfaceBuilder.BuildSettings(DateTime.UtcNow.ToString("O")),
            HandleSettingsSurfaceCommand);
    }

    private void ShowInputSettings()
    {
        var stateBoot = CurrentStateBoot();
        RenderMenuSurface(
            AetheriaRuntimeMainMenuSurfaceBuilder.BuildInputSettings(
                AetheriaRuntimeMainMenuSurfaceBuilder.ProjectRoot(
                    stateBoot,
                    null,
                    null,
                    LatestPlayerSettings(stateBoot),
                    CanOpenRuntimeInputScreen(),
                    InGame,
                    DateTime.UtcNow.ToString("O"))),
            HandleInputSettingsSurfaceCommand);
    }

    private void ShowPlayerSettingsSurface()
    {
        RenderMenuSurface(
            AetheriaRuntimeMainMenuSurfaceBuilder.BuildPlayerSettingsShell(
                AetheriaRuntimeMainMenuSurfaceBuilder.ProjectPlayerSettings(
                    LatestPlayerSettings(CurrentStateBoot()),
                    DateTime.UtcNow.ToString("O"))),
            HandlePlayerSettingsSurfaceCommand);
    }

    private void ShowVerseSettingsSurface()
    {
        var stateBoot = CurrentStateBoot();
        RenderMenuSurface(
            AetheriaRuntimeMainMenuSurfaceBuilder.BuildVerseSettingsShell(
                AetheriaRuntimeMainMenuSurfaceBuilder.ProjectVerseSettings(
                    stateBoot,
                    LatestVerseHostSettings(stateBoot),
                    DateTime.UtcNow.ToString("O"))),
            HandleVerseSettingsSurfaceCommand);
    }

    private void RenderMenuSurface(
        AetheriaRuntimeSurfaceDocument document,
        Action<EveSurfaceCommandRequest> commandHandler)
    {
        _menuSurfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _menuSurfaceDocument,
            "Aetheria Menu Surface",
            document,
            commandHandler,
            _menuSurfaceChrome);
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
                ShowPlayerSettingsSurface();
                return;
            case AetheriaRuntimeMainMenuCommandKind.ShowVerseSettings:
                ShowVerseSettingsSurface();
                return;
            case AetheriaRuntimeMainMenuCommandKind.ShowInputSettings:
                ShowInputSettings();
                return;
            case AetheriaRuntimeMainMenuCommandKind.BackToMain:
                ShowMain();
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
                ShowSettings();
                return;
            case AetheriaRuntimeMainMenuCommandKind.OpenRuntimeInputScreen:
                if (!TryOpenRuntimeInputScreen())
                {
                    ShowInputSettings();
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
                ShowSettings();
                return;
            case AetheriaRuntimeMainMenuCommandKind.PlayerSettingsCommand:
                SendKnownAetheriaEveCommand(request, "player-settings");
                ShowPlayerSettingsSurface();
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
                ShowSettings();
                return;
            case AetheriaRuntimeMainMenuCommandKind.ClientTargetCommand:
                RequestClientTargetCommand(request);
                ShowVerseSettingsSurface();
                return;
            case AetheriaRuntimeMainMenuCommandKind.VerseHostCommand:
                SendKnownAetheriaEveCommand(request, "Verse-host");
                ShowVerseSettingsSurface();
                return;
            default:
                Debug.LogWarning($"Unhandled verse-settings command kind: {command.Kind}");
                return;
        }
    }

    private void HideMenuSurface()
    {
        if (_menuSurfaceDocument == null)
            return;

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
            var submitted = ResolveClient(stateBoot)
                .Ui.SurfaceCommandAsync(request, "unity-main-menu")
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

    private AetheriaClient ResolveClient(AetheriaRuntimeStateBootReport stateBoot)
    {
        var statePath = stateBoot.StateFilePath;
        if (_client != null && string.Equals(_clientStatePath, statePath, StringComparison.Ordinal))
        {
            return _client;
        }

        DisposeClient();
        _client = AetheriaClient
            .OpenAsync(
                statePath,
                "unity-main-menu",
                "local",
                startServer: false,
                pullOnOpen: true)
            .GetAwaiter()
            .GetResult();
        _clientStatePath = statePath;
        return _client;
    }

    private void DisposeClient()
    {
        _client?.Dispose();
        _client = null;
        _clientStatePath = null;
        _catalog = null;
    }

    private void OnDestroy()
    {
        DisposeClient();
        if (_menuSurfaceDocument != null)
        {
            Destroy(_menuSurfaceDocument.gameObject);
            _menuSurfaceDocument = null;
        }
    }
}

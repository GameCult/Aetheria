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
        var frame = LatestDaemonFrame(stateBoot);
        var verseHost = LatestVerseHostSettings(stateBoot);
        var playerSettings = LatestPlayerSettings(stateBoot);
        RenderMenuSurface(
            AetheriaRuntimeMainMenuSurfaceBuilder.BuildRoot(
                AetheriaRuntimeMainMenuSurfaceBuilder.ProjectRoot(
                    stateBoot,
                    frame,
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
                if (LatestDaemonFrame(CurrentStateBoot()) == null)
                {
                    Debug.LogWarning("Main-menu Continue requested without an authoritative daemon frame.");
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
        if (!AetheriaRuntimeStateReader.TryReadDaemonFrame(stateBoot.StateFilePath, out var frame) ||
            frame == null ||
            !frame.IsAuthoritative ||
            frame.Run == null ||
            frame.Run.Zones == null ||
            frame.Run.Zones.Count == 0)
        {
            Debug.LogWarning($"Cannot start Aetheria observer scene without an authoritative daemon frame at {AetheriaRuntimeDaemonFrameStore.GetFramePath(stateBoot.StateFilePath)}.");
            return false;
        }

        var generatorState = "Loading runtime catalog";
        Action<string> setState = s => generatorState = s;

        HideMenuSurface();
        Dialog.Clear();
        Dialog.Title.text = title;
        Dialog.AddProperty(() => generatorState);
        Dialog.Show();

        setState($"Observing daemon frame {frame.FrameId}");
        ActionGameManager.IsTutorial = frame.Run.IsTutorial;
        var backgroundSettings = frame.Run.IsTutorial ? Settings.TutorialBackgroundSettings : Settings.SectorBackgroundSettings;
        backgroundSettings.NoisePosition = frame.Run.GenerationSeed == 0 ? 1 : frame.Run.GenerationSeed;
        ActionGameManager.ObservedGalaxy = Galaxy.ProjectObservedDaemonRun(
            frame.Run,
            backgroundSettings,
            ActionGameManager.RuntimeCatalog,
            Debug.Log);
        SceneManager.LoadScene("ARPG");
        return true;
    }

    private static AetheriaRuntimeStateBootReport CurrentStateBoot()
    {
        return AetheriaRuntimeStateBoot.Inspect(ActionGameManager.GameDataDirectory);
    }

    private static AetheriaRuntimeDaemonFrameDocument LatestDaemonFrame(AetheriaRuntimeStateBootReport stateBoot)
    {
        if (!AetheriaRuntimeStateReader.TryReadDaemonFrame(stateBoot.StateFilePath, out var frame) ||
            frame == null ||
            !frame.IsAuthoritative ||
            frame.Run == null ||
            frame.Run.Zones == null ||
            frame.Run.Zones.Count == 0)
        {
            return null;
        }

        return frame;
    }

    private static AetheriaRuntimePlayerSettingsSnapshot LatestPlayerSettings(AetheriaRuntimeStateBootReport stateBoot)
    {
        if (!stateBoot.SupportsLocalStateFileRead || !stateBoot.StateFileExists)
            return null;

        try
        {
            return AetheriaRuntimeStateReader.ReadPlayerSettings(stateBoot.StateFilePath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to read typed Aetheria player settings for the main menu: {ex}");
            return null;
        }
    }

    private static AetheriaRuntimeVerseHostSettingsSnapshot LatestVerseHostSettings(AetheriaRuntimeStateBootReport stateBoot)
    {
        if (!stateBoot.SupportsLocalStateFileRead || !stateBoot.StateFileExists)
            return null;

        try
        {
            return AetheriaRuntimeStateReader.ReadVerseHostSettings(stateBoot.StateFilePath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to read typed Aetheria Verse host settings for the main menu: {ex}");
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

    private bool CanOpenRuntimeInputScreen()
    {
        return InGame &&
               ActionGameManager.Instance != null &&
               ActionGameManager.Instance.CanShowInputScreenFromMenu() &&
               ActionGameManager.Instance.InputDisplayLayout != null;
    }

    private bool TryOpenRuntimeInputScreen()
    {
        if (!CanOpenRuntimeInputScreen())
            return false;

        HideMenuSurface();
        gameObject.SetActive(false);
        ActionGameManager.Instance.ShowInputScreenFromMenu();
        return true;
    }

    private static void RequestClientTargetCommand(EveSurfaceCommandRequest request)
    {
        try
        {
            if (!AetheriaRuntimeClientTargetSurfaceCommands.TryRequest(
                    AetheriaState.At(ActionGameManager.GameDataDirectory).ClientTarget,
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

    private static void SendKnownAetheriaEveCommand(
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
            if (!AetheriaRuntimeEveCommands.TrySendKnownSurfaceCommand(
                    stateBoot.StateFilePath,
                    request,
                    "unity-main-menu",
                    out var submitted,
                    out var error))
            {
                Debug.LogError($"Failed to submit Aetheria {label} Eve command: {error}");
                return;
            }

            Debug.Log($"Submitted Aetheria {label} Eve command: {submitted!.CommandId}");
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
}

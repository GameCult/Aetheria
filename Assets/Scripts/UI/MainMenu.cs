using System;
using System.Linq;
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
        RenderMenuSurface(
            AetheriaRuntimeMainMenuSurfaceBuilder.BuildRoot(
                ProjectMainMenuSurfaceState(
                    stateBoot,
                    frame,
                    verseHost,
                    CanOpenRuntimeInputScreen(),
                    InGame)),
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
        RenderMenuSurface(
            AetheriaRuntimeMainMenuSurfaceBuilder.BuildInputSettings(
                ProjectMainMenuSurfaceState(
                    CurrentStateBoot(),
                    null,
                    null,
                    CanOpenRuntimeInputScreen(),
                    InGame)),
            HandleInputSettingsSurfaceCommand);
    }

    private void ShowPlayerSettingsSurface()
    {
        RenderMenuSurface(
            AetheriaRuntimeMainMenuSurfaceBuilder.WithBackAction(
                BuildPlayerSettingsSurfaceDefinition(),
                AetheriaRuntimeMainMenuCommands.PlayerSettingsShellSurfaceId,
                AetheriaRuntimeMainMenuCommands.BackToSettings,
                "Back"),
            HandlePlayerSettingsSurfaceCommand);
    }

    private void ShowVerseSettingsSurface()
    {
        RenderMenuSurface(
            AetheriaRuntimeMainMenuSurfaceBuilder.WithBackAction(
                BuildVerseSettingsSurfaceDefinition(),
                AetheriaRuntimeMainMenuCommands.VerseSettingsShellSurfaceId,
                AetheriaRuntimeMainMenuCommands.BackToSettings,
                "Back"),
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
                if (!TrySendPlayerSettingsCommand(request, command.Command))
                {
                    Debug.LogWarning($"Unhandled player-settings command kind: {command.Kind}");
                    return;
                }

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
                if (TryRequestClientTargetCommand(request))
                {
                    ShowVerseSettingsSurface();
                }

                return;
            case AetheriaRuntimeMainMenuCommandKind.VerseHostCommand:
                if (TrySendVerseHostCommand(AetheriaRuntimeEveCommandClient.CommandKindForSurface(request)))
                {
                    ShowVerseSettingsSurface();
                }

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

    private static AetheriaRuntimeSurfaceDocument BuildPlayerSettingsSurfaceDefinition()
    {
        return AetheriaRuntimePlayerSettingsSurfaceBuilder.Build(
            new AetheriaRuntimePlayerSettingsSurfaceState(
                ActionGameManager.RuntimePlayerSettings.Name,
                ActionGameManager.RuntimePlayerSettings.TutorialPassed,
                "",
                ActionGameManager.RuntimePlayerSettings.GameplaySettings.TemperatureUnit.ToString(),
                Math.Max(0, ActionGameManager.RuntimePlayerSettings.GameplaySettings.SignificantDigits),
                ActionGameManager.RuntimePlayerSettings.GraphicsSettings.NebulaQuality.ToString(),
                ActionGameManager.RuntimePlayerSettings.GraphicsSettings.ShowAsteroidsInMinimap,
                DateTime.UtcNow.ToString("O")));
    }

    private static AetheriaRuntimeSurfaceDocument BuildVerseSettingsSurfaceDefinition()
    {
        var stateBoot = CurrentStateBoot();
        var verseHost = LatestVerseHostSettings(stateBoot);
        return AetheriaRuntimeClientTargetSurfaceBuilder.Build(
            new AetheriaRuntimeClientTargetSurfaceState(
                stateBoot.TargetKind,
                stateBoot.Title,
                stateBoot.VerseId,
                stateBoot.CultMeshAddress,
                stateBoot.StateFilePath,
                stateBoot.ReplicaStateFilePath,
                string.Join(", ", stateBoot.DiscoveryEndpoints ?? Array.Empty<string>()),
                stateBoot.DiscoveredVerses ?? Array.Empty<AetheriaRuntimeDiscoveredVerse>(),
                stateBoot.LastDiscoveryAtUtc,
                stateBoot.LastDiscoveryError,
                stateBoot.LastReplicaSyncAtUtc,
                stateBoot.LastReplicaSyncError,
                stateBoot.TargetSource,
                stateBoot.SupportsLocalStateFileRead,
                stateBoot.FailureMessage,
                verseHost?.Title ?? stateBoot.Title,
                verseHost?.VerseId ?? stateBoot.VerseId,
                verseHost?.Visibility ?? "unknown",
                verseHost?.CultMeshAddress ?? stateBoot.CultMeshAddress,
                DateTime.UtcNow.ToString("O")));
    }

    private static AetheriaRuntimeMainMenuSurfaceState ProjectMainMenuSurfaceState(
        AetheriaRuntimeStateBootReport stateBoot,
        AetheriaRuntimeDaemonFrameDocument daemonFrame,
        AetheriaRuntimeVerseHostSettingsSnapshot verseHost,
        bool canOpenRuntimeInputScreen,
        bool inGame)
    {
        return new AetheriaRuntimeMainMenuSurfaceState(
            stateBoot.TargetLabel,
            stateBoot.TargetKind,
            stateBoot.TargetSource,
            verseHost?.Title ?? stateBoot.Title,
            verseHost?.VerseId ?? stateBoot.VerseId,
            verseHost?.Visibility ?? "unknown",
            verseHost?.CultMeshAddress ?? stateBoot.CultMeshAddress,
            inGame,
            daemonFrame != null,
            daemonFrame?.Run?.RunId ?? "",
            daemonFrame?.FrameId ?? -1,
            ActionGameManager.RuntimePlayerSettings.InputSettings.InputActionMap.Count,
            ActionGameManager.RuntimePlayerSettings.InputSettings.ActionBarInputs.Count,
            canOpenRuntimeInputScreen,
            DateTime.UtcNow.ToString("O"));
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

    private static bool TryRequestClientTargetCommand(EveSurfaceCommandRequest request)
    {
        try
        {
            if (!AetheriaRuntimeClientTargetSurfaceCommands.TryRequest(
                    AetheriaState.At(ActionGameManager.GameDataDirectory).ClientTarget,
                    request,
                    out _))
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to update typed Aetheria client target: {ex}");
            return true;
        }
    }

    private static bool TrySendVerseHostCommand(AetheriaRuntimeEveCommandKind command)
    {
        var stateBoot = CurrentStateBoot();
        if (!CanSendLocalEveCommand(stateBoot, "Verse-host", command.ToString()))
            return true;

        try
        {
            if (!AetheriaRuntimeEveCommands.TrySendVerseHostCommand(
                    stateBoot.StateFilePath,
                    command,
                    "unity-main-menu",
                    out var submitted,
                    out var error))
            {
                Debug.LogError($"Failed to submit Aetheria Verse-host Eve command '{command}': {error}");
                return true;
            }

            Debug.Log($"Submitted Aetheria Verse-host Eve command: {submitted!.CommandId}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send Aetheria Verse-host Eve command '{command}': {ex}");
            return true;
        }
    }

    private static bool TrySendPlayerSettingsCommand(
        EveSurfaceCommandRequest request,
        string command)
    {
        if (request == null)
            return false;

        var stateBoot = CurrentStateBoot();
        if (!CanSendLocalEveCommand(stateBoot, "player-settings", command))
            return true;

        try
        {
            if (!AetheriaRuntimeEveCommands.TrySendPlayerSettingsCommand(
                    stateBoot.StateFilePath,
                    request,
                    "unity-main-menu",
                    out var submitted,
                    out var error))
            {
                Debug.LogError($"Failed to submit Aetheria player-settings Eve command '{command}': {error}");
                return true;
            }

            Debug.Log($"Submitted Aetheria player-settings Eve command: {submitted!.CommandId}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send Aetheria player-settings Eve command '{command}': {ex}");
            return true;
        }
    }

    private static bool CanSendLocalEveCommand(
        AetheriaRuntimeStateBootReport stateBoot,
        string label,
        string command)
    {
        if (!stateBoot.SupportsLocalStateFileRead || !stateBoot.StateFileExists)
        {
            Debug.LogWarning(
                $"Cannot send {label} command '{command}' because the active target is not a readable local Verse state file.");
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

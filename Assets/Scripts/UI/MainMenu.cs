using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameCult.Aetheria.State.Unity;
using GameCult.Eve.Surface;
using GameCult.Eve.UnityUIToolkit;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using static Unity.Mathematics.math;
using float2 = Unity.Mathematics.float2;
using Random = UnityEngine.Random;

public class MainMenu : MonoBehaviour
{
    private const string MenuSurfaceType = "surface-state";
    private const string MenuSurfaceSchema = "gamecult.eve.surface.v1";
    private const string MenuSurfaceProviderId = "aetheria";
    private const string MenuSurfaceProviderKind = "game.menu";
    private const string MainSurfaceId = "aetheria.main_menu.root";
    private const string SettingsSurfaceId = "aetheria.main_menu.settings";
    private const string InputSettingsSurfaceId = "aetheria.main_menu.input_settings";
    private const string PlayerSettingsShellSurfaceId = "aetheria.main_menu.player_settings";
    private const string ContinueRunCommand = "aetheria.main_menu.root.continue";
    private const string NewGameCommand = "aetheria.main_menu.root.new_game";
    private const string ShowSettingsCommand = "aetheria.main_menu.root.show_settings";
    private const string QuitCommand = "aetheria.main_menu.root.quit";
    private const string OpenRuntimeInputScreenCommand = "aetheria.main_menu.input_settings.open_runtime_screen";
    private const string ShowPlayerSettingsCommand = "aetheria.main_menu.settings.show_player_settings";
    private const string ShowInputSettingsCommand = "aetheria.main_menu.settings.show_input_settings";
    private const string BackToMainCommand = "aetheria.main_menu.settings.back_to_main";
    private const string BackToSettingsCommand = "aetheria.main_menu.settings.back_to_settings";

    public VolumeCloudRenderer CloudRenderer;
    public GameSettings Settings;
    public ConfirmationDialog Dialog;
    public bool InGame;

    private UIDocument _menuSurfaceDocument;
    
    void Start()
    {
        ShowMain();
    }

    private void ShowMain()
    {
        RenderMenuSurface(BuildMainSurfaceDefinition(LatestContinueRun(), InGame), HandleMainSurfaceCommand);
    }

    private void HandleMainSurfaceCommand(EveSurfaceCommandRequest request)
    {
        switch (request.Command)
        {
            case ContinueRunCommand:
                var continueRun = LatestContinueRun();
                if (continueRun == null)
                {
                    Debug.LogWarning("Main-menu Continue requested without a typed run state.");
                    ShowMain();
                    return;
                }

                ContinueGame(continueRun);
                return;
            case NewGameCommand:
                StartNewGame();
                return;
            case ShowSettingsCommand:
                ShowSettings();
                return;
            case QuitCommand:
                Application.Quit();
                return;
            default:
                Debug.LogWarning($"Unknown main menu command: {request.Command}");
                return;
        }
    }

    private void StartNewGame()
    {
        var generatorState = "Loading typed catalog";
        Action<string> setState = s => generatorState = s;

        HideMenuSurface();
        Dialog.Clear();
        Dialog.Title.text = "Generating Galaxy";
        Dialog.AddProperty(() => generatorState);
        Dialog.Show();

        if (ActionGameManager.RuntimePlayerSettings.TutorialPassed)
        {
            Settings.SectorBackgroundSettings.NoisePosition = Random.value * 1000;
            ActionGameManager.IsTutorial = false;
            var generationSeed = NextGenerationSeed();
            Task.Run(() =>
            {
                var sector = new Galaxy(
                    Settings.SectorGenerationSettings,
                    Settings.SectorBackgroundSettings,
                    Settings.NameGeneratorSettings,
                    ActionGameManager.RuntimeCatalog,
                    Debug.Log,
                    setState,
                    generationSeed);
                Observable.NextFrame().Subscribe(_ =>
                {
                    ActionGameManager.CurrentGalaxy = sector;
                    SceneManager.LoadScene("ARPG");
                });
            }).WrapErrors();
        }
        else
        {
            int iteration = 1;
            do
            {
                Settings.TutorialBackgroundSettings.NoisePosition = Random.value * 1000;
                setState($"Finding Galaxy Position: iteration {iteration++}");
            } while (Settings.TutorialBackgroundSettings.CloudDensity(float2(0.5f)) < .5f);

            ActionGameManager.IsTutorial = true;
            var generationSeed = NextGenerationSeed();
            Task.Run(() =>
            {
                var sector = new Galaxy(
                    Settings.TutorialGenerationSettings,
                    Settings.TutorialBackgroundSettings,
                    Settings.NameGeneratorSettings,
                    ActionGameManager.RuntimeCatalog,
                    ActionGameManager.RuntimePlayerSettings,
                    ActionGameManager.GameDataDirectory.CreateSubdirectory("Narrative"),
                    Debug.Log,
                    setState,
                    generationSeed);
                Observable.NextFrame().Subscribe(_ =>
                {
                    ActionGameManager.CurrentGalaxy = sector;
                    SceneManager.LoadScene("ARPG");
                });
            }).WrapErrors();
        }
    }

    private static uint NextGenerationSeed()
    {
        var seed = (uint)Random.Range(1, int.MaxValue);
        return seed == 0 ? 1u : seed;
    }

    private static AetheriaRuntimeRunStateSnapshot LatestContinueRun()
    {
        try
        {
            return AetheriaRuntimeCatalogStore
                .ReadRunStates(ActionGameManager.RuntimeStateFilePath)
                .Where(run => !string.IsNullOrWhiteSpace(run.RunId))
                .OrderByDescending(run => run.UpdatedAtUtc, StringComparer.Ordinal)
                .ThenByDescending(run => run.RunId, StringComparer.Ordinal)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to read typed Aetheria run state for Continue: {ex}");
            return null;
        }
    }

    private void ContinueGame(AetheriaRuntimeRunStateSnapshot run)
    {
        if (run == null)
            return;

        var generatorState = "Loading typed run";
        Action<string> setState = s => generatorState = s;

        HideMenuSurface();
        Dialog.Clear();
        Dialog.Title.text = "Continuing Run";
        Dialog.AddProperty(() => generatorState);
        Dialog.Show();

        ActionGameManager.IsTutorial = run.IsTutorial;
        ActionGameManager.ContinueRunState = run;
        var generationSeed = run.GenerationSeed == 0 ? NextGenerationSeed() : run.GenerationSeed;
        var backgroundSettings = run.IsTutorial ? Settings.TutorialBackgroundSettings : Settings.SectorBackgroundSettings;
        backgroundSettings.NoisePosition = generationSeed;

        Task.Run(() =>
        {
            var sector = run.IsTutorial
                ? new Galaxy(
                    Settings.TutorialGenerationSettings,
                    Settings.TutorialBackgroundSettings,
                    Settings.NameGeneratorSettings,
                    ActionGameManager.RuntimeCatalog,
                    ActionGameManager.RuntimePlayerSettings,
                    ActionGameManager.GameDataDirectory.CreateSubdirectory("Narrative"),
                    Debug.Log,
                    setState,
                    generationSeed)
                : new Galaxy(
                    Settings.SectorGenerationSettings,
                    backgroundSettings,
                    Settings.NameGeneratorSettings,
                    ActionGameManager.RuntimeCatalog,
                    Debug.Log,
                    setState,
                    generationSeed);
            Observable.NextFrame().Subscribe(_ =>
            {
                ActionGameManager.CurrentGalaxy = sector;
                SceneManager.LoadScene("ARPG");
            });
        }).WrapErrors();
    }

    private void ShowSettings()
    {
        RenderMenuSurface(BuildSettingsSurfaceDefinition(), HandleSettingsSurfaceCommand);
    }

    private void ShowInputSettings()
    {
        RenderMenuSurface(BuildInputSettingsSurfaceDefinition(CanOpenRuntimeInputScreen(), InGame), HandleInputSettingsSurfaceCommand);
    }

    private void ShowPlayerSettingsSurface()
    {
        RenderMenuSurface(
            WithBackAction(
                ToEveSurfaceDocument(BuildPlayerSettingsSurfaceDefinition()),
                PlayerSettingsShellSurfaceId,
                BackToSettingsCommand,
                "Back"),
            HandlePlayerSettingsSurfaceCommand);
    }

    private void RenderMenuSurface(
        EveSurfaceDocument document,
        Action<EveSurfaceCommandRequest> commandHandler)
    {
        var surfaceDocument = ResolveMenuSurfaceDocument();
        surfaceDocument.gameObject.SetActive(true);

        var root = surfaceDocument.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1;
        root.style.justifyContent = Justify.Center;
        root.style.alignItems = Align.Center;
        root.style.paddingLeft = 24;
        root.style.paddingRight = 24;
        root.style.paddingTop = 24;
        root.style.paddingBottom = 24;
        root.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);

        var shell = new VisualElement();
        shell.style.flexDirection = FlexDirection.Column;
        shell.style.width = 560;
        shell.style.maxWidth = 560;
        shell.style.paddingLeft = 20;
        shell.style.paddingRight = 20;
        shell.style.paddingTop = 20;
        shell.style.paddingBottom = 20;
        shell.style.backgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.96f);
        root.Add(shell);

        var lowerer = new EveUiToolkitSurfaceLowerer();
        shell.Add(lowerer.Lower(document, commandHandler));
    }

    private void HandleSettingsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        switch (request.Command)
        {
            case ShowPlayerSettingsCommand:
                ShowPlayerSettingsSurface();
                return;
            case ShowInputSettingsCommand:
                ShowInputSettings();
                return;
            case BackToMainCommand:
                ShowMain();
                return;
            default:
                Debug.LogWarning($"Unknown settings menu command: {request.Command}");
                return;
        }
    }

    private void HandleInputSettingsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (string.Equals(request.Command, BackToSettingsCommand, StringComparison.Ordinal))
        {
            ShowSettings();
            return;
        }

        if (string.Equals(request.Command, OpenRuntimeInputScreenCommand, StringComparison.Ordinal))
        {
            if (!TryOpenRuntimeInputScreen())
            {
                ShowInputSettings();
            }

            return;
        }

        Debug.LogWarning($"Unknown input settings command: {request.Command}");
    }

    private void HandlePlayerSettingsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (string.Equals(request.Command, BackToSettingsCommand, StringComparison.Ordinal))
        {
            ShowSettings();
            return;
        }

        if (!ActionGameManager.CommitRuntimePlayerSettingsCommand(request.Command, request.Payload))
        {
            Debug.LogWarning($"Unknown player-settings command: {request.Command}");
            return;
        }

        CloudRenderer.quality = ActionGameManager.RuntimePlayerSettings.GraphicsSettings.NebulaQuality;
        ShowPlayerSettingsSurface();
    }

    private void HideMenuSurface()
    {
        if (_menuSurfaceDocument == null)
            return;

        _menuSurfaceDocument.rootVisualElement.Clear();
        _menuSurfaceDocument.gameObject.SetActive(false);
    }

    private UIDocument ResolveMenuSurfaceDocument()
    {
        if (_menuSurfaceDocument != null)
            return _menuSurfaceDocument;

        var host = new GameObject("Aetheria Menu Surface");
        host.transform.SetParent(transform, false);
        var document = host.AddComponent<UIDocument>();
        document.sortingOrder = 1000;
        host.SetActive(false);
        _menuSurfaceDocument = document;
        return document;
    }

    private static AetheriaRuntimeSurfaceDocument BuildPlayerSettingsSurfaceDefinition()
    {
        return AetheriaRuntimePlayerSettingsSurfaceBuilder.Build(
            new AetheriaRuntimePlayerSettingsSurfaceState(
                ActionGameManager.RuntimePlayerSettings.Name,
                ActionGameManager.RuntimePlayerSettings.TutorialPassed,
                ActionGameManager.ContinueRunState?.RunId ?? "",
                ActionGameManager.RuntimePlayerSettings.GameplaySettings.TemperatureUnit.ToString(),
                max(0, ActionGameManager.RuntimePlayerSettings.GameplaySettings.SignificantDigits),
                ActionGameManager.RuntimePlayerSettings.GraphicsSettings.NebulaQuality.ToString(),
                ActionGameManager.RuntimePlayerSettings.GraphicsSettings.ShowAsteroidsInMinimap,
                DateTime.UtcNow.ToString("O")));
    }

    private static EveSurfaceDocument BuildMainSurfaceDefinition(
        AetheriaRuntimeRunStateSnapshot continueRun,
        bool inGame)
    {
        var commands = new List<EveCommandTemplate>();
        var actionButtons = new List<EveSurfaceComponent>();
        var cardChildren = new List<EveSurfaceComponent>();

        if (!inGame)
        {
            if (continueRun == null)
            {
                cardChildren.Add(Text(
                    $"{MainSurfaceId}.continue.note",
                    "No typed run state is available yet. Start a new galaxy or connect to a Verse that already has one."));
            }
            else
            {
                commands.Add(new EveCommandTemplate(ContinueRunCommand, "Continue", "unity-uitoolkit"));
                actionButtons.Add(Button($"{MainSurfaceId}.continue", "Continue", ContinueRunCommand));
                cardChildren.Add(Metric(
                    $"{MainSurfaceId}.continue.run",
                    "Latest Run",
                    continueRun.RunId));
            }
        }

        commands.Add(new EveCommandTemplate(NewGameCommand, "New Game", "unity-uitoolkit"));
        commands.Add(new EveCommandTemplate(ShowSettingsCommand, "Settings", "unity-uitoolkit"));
        commands.Add(new EveCommandTemplate(QuitCommand, "Quit", "unity-uitoolkit"));

        actionButtons.Add(Button($"{MainSurfaceId}.newGame", "New Game", NewGameCommand));
        actionButtons.Add(Button($"{MainSurfaceId}.settings", "Settings", ShowSettingsCommand));
        actionButtons.Add(Button($"{MainSurfaceId}.quit", "Quit", QuitCommand));

        cardChildren.Add(Text(
            $"{MainSurfaceId}.note",
            "The client lowers this shell through Eve. Verse state and game truth belong to the daemon."));
        cardChildren.Add(ButtonColumn($"{MainSurfaceId}.actions", actionButtons.ToArray()));

        return BuildMenuSurfaceDocument(
            MainSurfaceId,
            "Aetheria Terminus",
            commands,
            Card(
                $"{MainSurfaceId}.card",
                "Aetheria Terminus",
                cardChildren.ToArray()));
    }

    private static EveSurfaceDocument BuildSettingsSurfaceDefinition()
    {
        return BuildMenuSurfaceDocument(
            SettingsSurfaceId,
            "Aetheria Settings",
            new[]
            {
                new EveCommandTemplate(ShowPlayerSettingsCommand, "Player Settings", "unity-uitoolkit"),
                new EveCommandTemplate(ShowInputSettingsCommand, "Input", "unity-uitoolkit"),
                new EveCommandTemplate(BackToMainCommand, "Back", "unity-uitoolkit")
            },
            Card(
                "aetheria.mainMenu.settings.card",
                "Settings",
                Text(
                    "aetheria.mainMenu.settings.note",
                    "Typed Verse settings already lower through Eve. Input rebinding now opens the runtime Eve input screen, and audio still has no typed surface."),
                ButtonRow(
                    "aetheria.mainMenu.settings.actions",
                    Button("aetheria.mainMenu.settings.playerSettings", "Player Settings", ShowPlayerSettingsCommand),
                    Button("aetheria.mainMenu.settings.input", "Input", ShowInputSettingsCommand),
                    Button("aetheria.mainMenu.settings.back", "Back", BackToMainCommand))));
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

    private static EveSurfaceDocument BuildInputSettingsSurfaceDefinition(bool canOpenRuntimeInputScreen, bool inGame)
    {
        var commands = new List<EveCommandTemplate>
        {
            new EveCommandTemplate(BackToSettingsCommand, "Back", "unity-uitoolkit")
        };

        var cardChildren = new List<EveSurfaceComponent>
        {
            Metric(
                "aetheria.mainMenu.input.bindingOverrides",
                "Binding Overrides",
                ActionGameManager.RuntimePlayerSettings.InputSettings.InputActionMap.Count.ToString()),
            Metric(
                "aetheria.mainMenu.input.actionBarInputs",
                "Action-Bar Inputs",
                ActionGameManager.RuntimePlayerSettings.InputSettings.ActionBarInputs.Count.ToString())
        };

        if (canOpenRuntimeInputScreen)
        {
            commands.Insert(0, new EveCommandTemplate(OpenRuntimeInputScreenCommand, "Open Remap Screen", "unity-uitoolkit"));
            cardChildren.Add(Text(
                "aetheria.mainMenu.input.note",
                "The runtime Eve input screen owns low-level InputSystem rebinding and action-bar input edits. This title shell reports typed player-settings state and hands off to that owner."));
        }
        else if (inGame)
        {
            cardChildren.Add(Text(
                "aetheria.mainMenu.input.note",
                "The runtime Eve input screen should own rebinding here, but this scene has no active input surface to hand off to."));
        }
        else
        {
            cardChildren.Add(Text(
                "aetheria.mainMenu.input.note",
                "This title shell reports the typed player-settings state. Launch a run to open the runtime Eve input screen that owns low-level InputSystem rebinding."));
        }

        var buttons = new List<EveSurfaceComponent>();
        if (canOpenRuntimeInputScreen)
        {
            buttons.Add(Button("aetheria.mainMenu.input.openRuntimeScreen", "Open Remap Screen", OpenRuntimeInputScreenCommand));
        }

        buttons.Add(Button("aetheria.mainMenu.input.back", "Back", BackToSettingsCommand));
        cardChildren.Add(ButtonRow("aetheria.mainMenu.input.actions", buttons.ToArray()));

        return BuildMenuSurfaceDocument(
            InputSettingsSurfaceId,
            "Aetheria Input Settings",
            commands,
            Card(
                "aetheria.mainMenu.input.card",
                "Input Settings",
                cardChildren.ToArray()));
    }

    private static EveSurfaceDocument WithBackAction(
        EveSurfaceDocument document,
        string surfaceId,
        string backCommand,
        string backLabel)
    {
        return new EveSurfaceDocument(
            document.Type,
            document.Schema,
            document.ProviderId,
            document.ProviderKind,
            document.Title,
            document.Version,
            document.UpdatedAtUtc,
            new EveSurfaceTree(
                surfaceId,
                Node(
                    $"{surfaceId}.root",
                    "surface",
                    Array.Empty<(string Key, string Value)>(),
                    document.Surface.Root,
                    ButtonRow(
                        $"{surfaceId}.actions",
                        Button($"{surfaceId}.back", backLabel, backCommand))),
                document.Surface.Styles),
            document.Commands
                .Concat(new[]
                {
                    new EveCommandTemplate(backCommand, backLabel, "unity-uitoolkit")
                })
                .ToArray());
    }

    private static EveSurfaceDocument BuildMenuSurfaceDocument(
        string surfaceId,
        string title,
        IReadOnlyList<EveCommandTemplate> commands,
        params EveSurfaceComponent[] children)
    {
        return new EveSurfaceDocument(
            MenuSurfaceType,
            MenuSurfaceSchema,
            MenuSurfaceProviderId,
            MenuSurfaceProviderKind,
            title,
            version: 1,
            DateTime.UtcNow.ToString("O"),
            new EveSurfaceTree(
                surfaceId,
                Node($"{surfaceId}.root", "surface", Array.Empty<(string Key, string Value)>(), children),
                Array.Empty<EveStyleToken>()),
            commands);
    }

    private static EveSurfaceComponent Card(
        string id,
        string title,
        params EveSurfaceComponent[] children)
    {
        return Node(id, "card", new[] { ("title", title) }, children);
    }

    private static EveSurfaceComponent Metric(string id, string label, string value)
    {
        return Node(id, "metric", new[] { ("label", label), ("value", value) });
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

    private static EveSurfaceComponent ButtonColumn(
        string id,
        params EveSurfaceComponent[] children)
    {
        return Node(id, "column", Array.Empty<(string Key, string Value)>(), children);
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

    private static EveSurfaceDocument ToEveSurfaceDocument(AetheriaRuntimeSurfaceDocument document)
    {
        return new EveSurfaceDocument(
            "surface-state",
            "gamecult.eve.surface.v1",
            document.ProviderId,
            document.ProviderKind,
            document.Title,
            document.Version,
            document.UpdatedAtUtc,
            new EveSurfaceTree(
                document.Surface.Id,
                ToEveSurfaceComponent(document.Surface.Root),
                document.Surface.Styles
                    .Select(style => new EveStyleToken(style.Name, style.Value))
                    .ToArray()),
            document.Commands
                .Select(command => new EveCommandTemplate(command.Command, command.Label, command.Transport))
                .ToArray());
    }

    private static EveSurfaceComponent ToEveSurfaceComponent(AetheriaRuntimeSurfaceComponent component)
    {
        return new EveSurfaceComponent(
            component.Id,
            component.Kind,
            new Dictionary<string, string>(component.Props, StringComparer.Ordinal),
            component.Children.Select(ToEveSurfaceComponent).ToArray());
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

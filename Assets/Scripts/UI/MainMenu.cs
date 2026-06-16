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
    private const string SettingsSurfaceId = "aetheria.main_menu.settings";
    private const string InputSettingsSurfaceId = "aetheria.main_menu.input_settings";
    private const string AudioSettingsSurfaceId = "aetheria.main_menu.audio_settings";
    private const string PlayerSettingsShellSurfaceId = "aetheria.main_menu.player_settings";
    private const string ShowPlayerSettingsCommand = "aetheria.main_menu.settings.show_player_settings";
    private const string ShowInputSettingsCommand = "aetheria.main_menu.settings.show_input_settings";
    private const string ShowAudioSettingsCommand = "aetheria.main_menu.settings.show_audio_settings";
    private const string BackToMainCommand = "aetheria.main_menu.settings.back_to_main";
    private const string BackToSettingsCommand = "aetheria.main_menu.settings.back_to_settings";

    public VolumeCloudRenderer CloudRenderer;
    public GameSettings Settings;
    public ConfirmationDialog Dialog;
    public bool InGame;
    public Prototype PanelPrototype;
    public float FadeTime = .5f;
    public float FadeDistance = 512;
    public float FadeAlphaExponent = 2;
    public float FadePositionExponent = 2;

    private (PropertiesPanel panel, CanvasGroup group) _currentMenu, _nextMenu;
    private bool _fadeFromRight;
    private float _fadeLerp;
    private bool _fading;
    private Vector3 _panelPosition;
    private UIDocument _menuSurfaceDocument;
    
    void Start()
    {
        _panelPosition = PanelPrototype.transform.position;
        
        var panel1 = PanelPrototype.Instantiate<PropertiesPanel>();
        _currentMenu = (panel1, panel1.GetComponent<CanvasGroup>());
        
        var panel2 = PanelPrototype.Instantiate<PropertiesPanel>();
        _nextMenu = (panel2, panel2.GetComponent<CanvasGroup>());

        _currentMenu.panel.gameObject.SetActive(false);
        
        ShowMain();
        Fade(true);
    }

    private void Update()
    {
        if (_fading)
        {
            _fadeLerp += Time.deltaTime / FadeTime;

            _currentMenu.panel.transform.position = 
                _panelPosition + (_fadeFromRight ? Vector3.left : Vector3.right) * (FadeDistance * pow(_fadeLerp, FadePositionExponent));
            _nextMenu.panel.transform.position = 
                _panelPosition + (_fadeFromRight ? Vector3.right : Vector3.left) * (FadeDistance * pow(1-_fadeLerp, FadePositionExponent));
            _currentMenu.group.alpha = pow(1 - _fadeLerp, FadeAlphaExponent);
            _nextMenu.group.alpha = pow(_fadeLerp, FadeAlphaExponent);
            
            if (_fadeLerp > 1)
            {
                _fading = false;
                _currentMenu.panel.gameObject.SetActive(false);
                var temp = _currentMenu;
                _currentMenu = _nextMenu;
                _nextMenu = temp;
                if (IsMenuSurfaceVisible())
                    _currentMenu.panel.gameObject.SetActive(false);
            }
        }
    }

    private string TitleSubtitle(string title, string subtitle) => $"{title}\n<smallcaps><size=50%>{subtitle}";

    private void Fade(bool fromRight)
    {
        _nextMenu.panel.gameObject.SetActive(true);
        _nextMenu.group.alpha = 0;
        _fading = true;
        _fadeLerp = 0;
        _fadeFromRight = fromRight;
    }

    private bool IsMenuSurfaceVisible()
    {
        return _menuSurfaceDocument != null && _menuSurfaceDocument.gameObject.activeSelf;
    }

    private void ShowMain()
    {
        HideMenuSurface();
        _nextMenu.panel.Clear();
        _nextMenu.panel.Title.text = TitleSubtitle("aetheria", "terminus");
        if (!InGame)
        {
            var continueRun = LatestContinueRun();
            _nextMenu.panel.AddButton("Continue", continueRun == null ? null : () => ContinueGame(continueRun));
        }
        _nextMenu.panel.AddButton("New Game",
            () =>
            {
                var generatorState = "Loading typed catalog";
                Action<string> setState = s => generatorState = s;

                Fade(true);
                _nextMenu.panel.gameObject.SetActive(false);
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
            });
        _nextMenu.panel.AddButton("Settings",
            () =>
            {
                ShowSettings();
                Fade(true);
            });
        _nextMenu.panel.AddButton("Quit", Application.Quit);
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

        Fade(true);
        _nextMenu.panel.gameObject.SetActive(false);
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
        _nextMenu.panel.Clear();
        _nextMenu.panel.gameObject.SetActive(false);
        RenderMenuSurface(BuildSettingsSurfaceDefinition(), HandleSettingsSurfaceCommand);
    }

    private void ShowInputSettings()
    {
        _nextMenu.panel.Clear();
        _nextMenu.panel.gameObject.SetActive(false);
        RenderMenuSurface(BuildInputSettingsSurfaceDefinition(), HandleInputSettingsSurfaceCommand);
    }

    private void ShowAudioSettings()
    {
        _nextMenu.panel.Clear();
        _nextMenu.panel.gameObject.SetActive(false);
        RenderMenuSurface(BuildAudioSettingsSurfaceDefinition(), HandleAudioSettingsSurfaceCommand);
    }

    private void ShowPlayerSettingsSurface()
    {
        _nextMenu.panel.Clear();
        _nextMenu.panel.gameObject.SetActive(false);
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
            case ShowAudioSettingsCommand:
                ShowAudioSettings();
                return;
            case BackToMainCommand:
                HideMenuSurface();
                ShowMain();
                Fade(false);
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

        Debug.LogWarning($"Unknown input settings command: {request.Command}");
    }

    private void HandleAudioSettingsSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (string.Equals(request.Command, BackToSettingsCommand, StringComparison.Ordinal))
        {
            ShowSettings();
            return;
        }

        Debug.LogWarning($"Unknown audio settings command: {request.Command}");
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

    private static EveSurfaceDocument BuildSettingsSurfaceDefinition()
    {
        return BuildMenuSurfaceDocument(
            SettingsSurfaceId,
            "Aetheria Settings",
            new[]
            {
                new EveCommandTemplate(ShowPlayerSettingsCommand, "Player Settings", "unity-uitoolkit"),
                new EveCommandTemplate(ShowInputSettingsCommand, "Input", "unity-uitoolkit"),
                new EveCommandTemplate(ShowAudioSettingsCommand, "Audio", "unity-uitoolkit"),
                new EveCommandTemplate(BackToMainCommand, "Back", "unity-uitoolkit")
            },
            Card(
                "aetheria.mainMenu.settings.card",
                "Settings",
                Text(
                    "aetheria.mainMenu.settings.note",
                    "Typed Verse settings already lower through Eve. Input rebinding and audio still have narrower local authority."),
                ButtonRow(
                    "aetheria.mainMenu.settings.actions",
                    Button("aetheria.mainMenu.settings.playerSettings", "Player Settings", ShowPlayerSettingsCommand),
                    Button("aetheria.mainMenu.settings.input", "Input", ShowInputSettingsCommand),
                    Button("aetheria.mainMenu.settings.audio", "Audio", ShowAudioSettingsCommand),
                    Button("aetheria.mainMenu.settings.back", "Back", BackToMainCommand))));
    }

    private static EveSurfaceDocument BuildInputSettingsSurfaceDefinition()
    {
        return BuildMenuSurfaceDocument(
            InputSettingsSurfaceId,
            "Aetheria Input Settings",
            new[]
            {
                new EveCommandTemplate(BackToSettingsCommand, "Back", "unity-uitoolkit")
            },
            Card(
                "aetheria.mainMenu.input.card",
                "Input Settings",
                Metric(
                    "aetheria.mainMenu.input.bindingOverrides",
                    "Binding Overrides",
                    ActionGameManager.RuntimePlayerSettings.InputSettings.InputActionMap.Count.ToString()),
                Metric(
                    "aetheria.mainMenu.input.actionBarInputs",
                    "Action-Bar Inputs",
                    ActionGameManager.RuntimePlayerSettings.InputSettings.ActionBarInputs.Count.ToString()),
                Text(
                    "aetheria.mainMenu.input.note",
                    "Typed input rebinding controls are not lowered through Eve yet. The live remapping screen still owns drag/drop rebinding and low-level InputSystem edits."),
                ButtonRow(
                    "aetheria.mainMenu.input.actions",
                    Button("aetheria.mainMenu.input.back", "Back", BackToSettingsCommand))));
    }

    private static EveSurfaceDocument BuildAudioSettingsSurfaceDefinition()
    {
        return BuildMenuSurfaceDocument(
            AudioSettingsSurfaceId,
            "Aetheria Audio Settings",
            new[]
            {
                new EveCommandTemplate(BackToSettingsCommand, "Back", "unity-uitoolkit")
            },
            Card(
                "aetheria.mainMenu.audio.card",
                "Audio Settings",
                Text(
                    "aetheria.mainMenu.audio.note",
                    "No typed audio controls are published yet. This screen is lowered through Eve so the old menu shell stops owning the page while the real audio surface catches up."),
                ButtonRow(
                    "aetheria.mainMenu.audio.actions",
                    Button("aetheria.mainMenu.audio.back", "Back", BackToSettingsCommand))));
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

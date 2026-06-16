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
    private UIDocument _playerSettingsSurfaceDocument;
    
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

    private void ShowMain()
    {
        HidePlayerSettingsSurface();
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
        HidePlayerSettingsSurface();
        _nextMenu.panel.Clear();
        _nextMenu.panel.Title.text = "settings";
        _nextMenu.panel.AddButton("Player Settings",
            () =>
            {
                ShowPlayerSettingsSurface();
                Fade(true);
            });
        _nextMenu.panel.AddButton("Input",
            () =>
            {
                ShowInputSettings();
                Fade(true);
            });
        _nextMenu.panel.AddButton("Audio",
            () =>
            {
                ShowAudioSettings();
                Fade(true);
            });
        _nextMenu.panel.AddButton("Back",
            () =>
            {
                ShowMain();
                Fade(false);
            });
    }

    private void ShowInputSettings()
    {
        HidePlayerSettingsSurface();
        _nextMenu.panel.Clear();
        _nextMenu.panel.Title.text = TitleSubtitle("input", "settings");
        _nextMenu.panel.AddButton("Back",
            () =>
            {
                ShowSettings();
                Fade(false);
            });
    }

    private void ShowAudioSettings()
    {
        HidePlayerSettingsSurface();
        _nextMenu.panel.Clear();
        _nextMenu.panel.Title.text = TitleSubtitle("audio", "settings");
        _nextMenu.panel.AddButton("Back",
            () =>
            {
                ShowSettings();
                Fade(false);
            });
    }

    private void ShowPlayerSettingsSurface()
    {
        _nextMenu.panel.Clear();
        _nextMenu.panel.gameObject.SetActive(false);
        RenderPlayerSettingsSurface();
    }

    private void RenderPlayerSettingsSurface()
    {
        var document = ResolvePlayerSettingsSurfaceDocument();
        document.gameObject.SetActive(true);

        var root = document.rootVisualElement;
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

        var title = new Label("Player Settings");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.fontSize = 24;
        title.style.marginBottom = 6;
        shell.Add(title);

        var subtitle = new Label("Shared Eve surface lowered locally while gameplay keeps commit authority.");
        subtitle.style.marginBottom = 12;
        shell.Add(subtitle);

        var nameField = new TextField("Name")
        {
            value = ActionGameManager.RuntimePlayerSettings.Name
        };
        nameField.style.marginBottom = 14;
        nameField.RegisterValueChangedCallback(evt =>
        {
            if (string.Equals(evt.newValue, ActionGameManager.RuntimePlayerSettings.Name, StringComparison.Ordinal))
                return;

            ActionGameManager.CommitRuntimePlayerName(evt.newValue);
            RenderPlayerSettingsSurface();
        });
        shell.Add(nameField);

        var lowerer = new EveUiToolkitSurfaceLowerer();
        var surface = lowerer.Lower(
            ToEveSurfaceDocument(BuildPlayerSettingsSurfaceDefinition()),
            request =>
            {
                if (!ActionGameManager.CommitRuntimePlayerSettingsCommand(request.Command))
                {
                    Debug.LogWarning($"Unknown player-settings command: {request.Command}");
                    return;
                }

                CloudRenderer.quality = ActionGameManager.RuntimePlayerSettings.GraphicsSettings.NebulaQuality;
                RenderPlayerSettingsSurface();
            });
        surface.style.marginBottom = 14;
        shell.Add(surface);

        var actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        shell.Add(actions);

        var back = new UnityEngine.UIElements.Button(() =>
        {
            HidePlayerSettingsSurface();
            ShowSettings();
            Fade(false);
        })
        {
            text = "Back"
        };
        actions.Add(back);
    }

    private void HidePlayerSettingsSurface()
    {
        if (_playerSettingsSurfaceDocument == null)
            return;

        _playerSettingsSurfaceDocument.rootVisualElement.Clear();
        _playerSettingsSurfaceDocument.gameObject.SetActive(false);
    }

    private UIDocument ResolvePlayerSettingsSurfaceDocument()
    {
        if (_playerSettingsSurfaceDocument != null)
            return _playerSettingsSurfaceDocument;

        var host = new GameObject("Aetheria Player Settings Surface");
        host.transform.SetParent(transform, false);
        var document = host.AddComponent<UIDocument>();
        document.sortingOrder = 1000;
        host.SetActive(false);
        _playerSettingsSurfaceDocument = document;
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
        if (_playerSettingsSurfaceDocument != null)
        {
            Destroy(_playerSettingsSurfaceDocument.gameObject);
            _playerSettingsSurfaceDocument = null;
        }
    }
}

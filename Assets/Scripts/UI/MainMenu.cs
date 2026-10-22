using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static CultMath.math;

public class MainMenu : MonoBehaviour
{
    public VolumeCloudRenderer CloudRenderer;
    public GameSettings Settings;
    public ConfirmationDialog Dialog;
    public Prototype PanelPrototype;
    public float FadeTime = .5f;
    public float FadeDistance = 512;
    public float FadeAlphaExponent = 2;
    public float FadePositionExponent = 2;

    private (PropertiesPanel panel, CanvasGroup group, PropertiesPanel detail) _currentMenu, _nextMenu;
    private bool _fadeFromRight;
    private float _fadeLerp;
    private bool _fading;
    private Vector3 _panelPosition;
    //private Task<DatabaseCache> _databaseLoad;
    
    void Start()
    {
        // Start loading the database in the background 'cause it takes a few seconds
        //_databaseLoad = Task.Run(() => ActionGameManager.Database);
        
        _panelPosition = PanelPrototype.transform.position;
        
        var panel1 = PanelPrototype.Instantiate<PropertiesPanel>();
        var detail1 = PanelPrototype.Instantiate<PropertiesPanel>();
        ScenarioPicker.Dock(detail1, panel1);
        _currentMenu = (panel1, panel1.GetComponent<CanvasGroup>(), detail1);
        
        var panel2 = PanelPrototype.Instantiate<PropertiesPanel>();
        var detail2 = PanelPrototype.Instantiate<PropertiesPanel>();
        ScenarioPicker.Dock(detail2, panel2);
        _nextMenu = (panel2, panel2.GetComponent<CanvasGroup>(), detail2);

        _currentMenu.panel.gameObject.SetActive(false);
        //_saveDirectory = ActionGameManager.GameDataDirectory.CreateSubdirectory("Saves");
        
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

    public static string TitleSubtitle(string title, string subtitle) => $"{title}\n<smallcaps><size=50%>{subtitle}";

    private void Fade(bool fromRight)
    {
        _nextMenu.panel.gameObject.SetActive(true);
        _nextMenu.group.alpha = 0;
        _fading = true;
        _fadeLerp = 0;
        _fadeFromRight = fromRight;
    }

    private static bool _exclusionsShown;

    // Starts the next screen on an empty panel with a title and its scenario detail pane hidden and empty.
    private void Next(string title)
    {
        _nextMenu.panel.Clear();
        _nextMenu.panel.Title.text = title;
        _nextMenu.detail.Clear();
        _nextMenu.detail.gameObject.SetActive(false);
    }

    private void ShowMain()
    {
        Next(TitleSubtitle("aetheria", "terminus"));
        if (ActionGameManager.CurrentGalaxy == null)
        {
            // No run is live: at the title, after death, or after a new run failed to start. A saved run exists only as
            // the run store's SavedGame global.
            var cache = ActionGameManager.CultCache;
            if (ActionGameManager.ModExclusions.Length > 0 && !_exclusionsShown)
            {
                _exclusionsShown = true;
                RefuseMods("Some mods were not loaded", ActionGameManager.ModExclusions);
            }
            var saved = cache.GetGlobal<SavedGame>();
            if (saved == null && cache.AllStoredDocuments.Any(stored => RunSave.IsRunRecord(stored.Descriptor.DocumentType)))
                Debug.Log("run store has no SavedGame; Continue disabled");
            _nextMenu.panel.AddButton("Continue", saved == null ? (Action) null : () =>
            {
                try { RunSave.RequireDesigns(cache, saved); }
                catch (InvalidOperationException error) { Refuse("Cannot continue this run", error.Message); return; }
                ActionGameManager.CurrentGalaxy = new Galaxy(cache, saved, Debug.Log);
                EnterGame();
            });
        }
        _nextMenu.panel.AddButton("New Game",
            () =>
            {
                ShowScenarios();
                Fade(true);
            });
        _nextMenu.panel.AddButton("Settings",
            () =>
            {
                ShowSettings();
                Fade(true);
            });
        _nextMenu.panel.AddButton("Quit", Application.Quit);
    }

    // New Game: every way to set up a run, each with its brief. A release build lists only the modes; the other ways
    // to set up a run and the test arenas are listed only in editor and development builds.
    private void ShowScenarios()
    {
        Next(TitleSubtitle("new game", "scenarios"));
        var groups = new List<(string label, Scenario[] scenarios)> { ("modes", Scenarios.Modes) };
        if (Debug.isDebugBuild)
        {
            groups.Add(("development", Scenarios.Development));
            groups.Add(("test arenas", Scenarios.Tests));
        }
        ScenarioPicker.Show(_nextMenu.panel, _nextMenu.detail, groups, Launch, () =>
        {
            ShowMain();
            Fade(false);
        }, Dialog.Confirm);
    }

    // The only way into a new run: generates the scenario's galaxy off the main thread and enters the game, where
    // StartGame has RunStart stage it and only then replaces the saved run. A galaxy that cannot generate is refused
    // here, with the saved run untouched.
    private void Launch(Scenario scenario)
    {
        var generatorState = "Loading Database Contents";

        Fade(true);
        _nextMenu.panel.gameObject.SetActive(false);
        Dialog.Clear();
        Dialog.Title.text = "Generating Galaxy";
        Dialog.AddProperty(() => generatorState);
        Dialog.Show();

        var stage = new GalaxyStage(
            Settings.SectorGenerationSettings,
            Settings.SectorBackgroundSettings,
            Settings.TutorialGenerationSettings,
            Settings.TutorialBackgroundSettings,
            ActionGameManager.CultCache,
            ActionGameManager.PlayerSettings,
            ActionGameManager.GameDataDirectory.CreateSubdirectory("Narrative"),
            Debug.Log,
            state => generatorState = state);
        Task.Run(() => RunStart.Generate(scenario, stage)).ContinueWith(generation =>
            Observable.NextFrame().Subscribe(_ =>
            {
                if (generation.IsFaulted)
                {
                    Debug.LogException(generation.Exception);
                    ShowMain();
                    Fade(false);
                    Refuse($"{scenario.Name} could not generate its galaxy", generation.Exception.GetBaseException().Message);
                    return;
                }
                ActionGameManager.CurrentGalaxy = generation.Result;
                ActionGameManager.PendingScenario = scenario;
                EnterGame();
            }));
    }

    // The one way into the game scene. Mod ship prototypes import asynchronously at boot, so the scene waits for them,
    // and a preload that could not run at all stops here with its reason rather than in the first zone.
    private void EnterGame() => StartCoroutine(EnterGameWhenModShipsAreReady());

    private IEnumerator EnterGameWhenModShipsAreReady()
    {
        var loading = ShipModShips.Loading;
        while (!loading.IsCompleted) yield return null;
        if (loading.IsFaulted)
        {
            if (loading.Exception.GetBaseException() is ShipModShips.ImportFault fault) RefuseMods("Mod ships failed to load", fault.Faults);
            else Refuse("Mod ships failed to load", loading.Exception.GetBaseException().Message);
            yield break;
        }
        SceneManager.LoadScene("ARPG");
    }

    public void Refuse(string title, string reason)
    {
        Debug.LogError($"{title}: {reason}");
        Dialog.Clear();
        Dialog.Title.text = title;
        Dialog.AddProperty(() => reason);
        Dialog.Show(onCancel: () => { }, cancelText: "OK");
    }

    // Refuse for faults that name mod packages: each distinct package gets a Remove button, the one RemovePackage. The catalog
    // stays as this boot opened it, so the dialog says that removal takes effect at the next launch.
    private readonly HashSet<string> _removedPackages = new();

    public void RefuseMods(string title, ShipModCatalog.Exclusion[] faults)
    {
        // A package moved aside this session stays named but loses its button: this boot's catalog still holds it.
        var open = faults.Where(fault => !_removedPackages.Contains(fault.Package)).ToArray();
        var reason = string.Join("\n", faults.Select(fault => fault.ToString()));
        Debug.LogError($"{title}: {reason}");
        Dialog.Clear();
        Dialog.Title.text = title;
        Dialog.AddProperty(() => reason);
        foreach (var package in open.Select(fault => fault.Package).Where(package => package != "(all mods)").Distinct())
            Dialog.AddButton($"Remove {package}", () => Remove(title, faults, package));
        Dialog.Show(onCancel: () => { }, cancelText: "OK");
    }

    private void Remove(string title, ShipModCatalog.Exclusion[] faults, string package)
    {
        string destination;
        try { destination = ShipModCatalog.RemovePackage(ActionGameManager.ModsPath, package); }
        catch (Exception error)
        {
            Refuse($"Could not remove {package}", $"{error.Message} Remove it by hand from {ActionGameManager.ModsPath}.");
            return;
        }
        _removedPackages.Add(package);
        var done = $"{package} moved to {destination}. Mods load at launch: restart Aetheria to play without it, or move it back into Mods to reinstall.";
        Debug.Log(done);
        Dialog.Clear();
        Dialog.Title.text = $"Removed {package}";
        Dialog.AddProperty(() => done);
        Dialog.Show(onConfirm: Application.Quit, onCancel: () => { if (faults.Any(fault => fault.Package != "(all mods)" && !_removedPackages.Contains(fault.Package))) RefuseMods(title, faults); }, confirmText: "Quit", cancelText: "OK");
    }

    private void ShowSettings()
    {
        Next("settings");
        _nextMenu.panel.AddButton("Gameplay",
            () =>
            {
                ShowGameplaySettings();
                Fade(true);
            });
        _nextMenu.panel.AddButton("Graphics",
            () =>
            {
                ShowGraphicsSettings();
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
    
    private void ShowGameplaySettings()
    {
        Next(TitleSubtitle("gameplay", "settings"));
        _nextMenu.panel.AddField("Name", 
            () => ActionGameManager.PlayerSettings.Name,
            s => ActionGameManager.PlayerSettings.Name = s);
        _nextMenu.panel.AddField("Temperature Unit", 
            () => (int) ActionGameManager.PlayerSettings.GameplaySettings.TemperatureUnit,
            i => ActionGameManager.PlayerSettings.GameplaySettings.TemperatureUnit = (TemperatureUnit) i,
            Enum.GetNames(typeof(TemperatureUnit)));
        _nextMenu.panel.AddField("Significant Digits", 
            () => ActionGameManager.PlayerSettings.GameplaySettings.SignificantDigits,
            i => ActionGameManager.PlayerSettings.GameplaySettings.SignificantDigits = i);
        _nextMenu.panel.AddButton("Back",
            () =>
            {
                ActionGameManager.SavePlayerSettings();
                ShowSettings();
                Fade(false);
            });
    }

    private void ShowGraphicsSettings()
    {
        Next(TitleSubtitle("graphics", "settings"));
        _nextMenu.panel.AddField("Nebula Quality",
            () => (int)ActionGameManager.PlayerSettings.GraphicsSettings.NebulaQuality,
            i =>
            {
                ActionGameManager.PlayerSettings.GraphicsSettings.NebulaQuality = (Quality)i;
                CloudRenderer.quality = ActionGameManager.PlayerSettings.GraphicsSettings.NebulaQuality;
            },
            Enum.GetNames(typeof(Quality)));
        _nextMenu.panel.AddField("Screen Shake",
            () => ActionGameManager.PlayerSettings.GraphicsSettings.ScreenShake,
            f => ActionGameManager.PlayerSettings.GraphicsSettings.ScreenShake = f,
            0, 1);
        _nextMenu.panel.AddButton("Back",
            () =>
            {
                ActionGameManager.SavePlayerSettings();
                ShowSettings();
                Fade(false);
            });
    }

    private void ShowInputSettings()
    {
        Next(TitleSubtitle("input", "settings"));
        _nextMenu.panel.AddButton("Back",
            () =>
            {
                ShowSettings();
                Fade(false);
            });
    }

    private void ShowAudioSettings()
    {
        Next(TitleSubtitle("audio", "settings"));
        _nextMenu.panel.AddButton("Back",
            () =>
            {
                ShowSettings();
                Fade(false);
            });
    }
}

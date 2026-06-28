using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Aetheria.EveRuntime;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
using Ink.Runtime;
using TMPro;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UIElements;

public class LocalMenu : MonoBehaviour
{
    public ObservablePointerClickTrigger ContinueTrigger;
    public TextMeshProUGUI Output;
    public RectTransform ChoiceParent;
    public ChoicePrefab ChoicePrefab;

    private string _currentPath;
    private LocationStory _currentLocation;
    private Story _activeStory;
    private readonly List<ActiveStoryChoice> _activeChoices = new List<ActiveStoryChoice>();
    private UIDocument _surfaceDocument;
    private readonly AetheriaEveUnitySurfaceChrome _surfaceChrome = new AetheriaEveUnitySurfaceChrome();
    private AetheriaUnityObservedEntityIndex _observedEntityIndex;
    private string _clientStatePath = "";
    private AetheriaClientReactiveDockingState _dockingState;

    public void SetObservedEntityIndex(AetheriaUnityObservedEntityIndex observedEntityIndex)
    {
        _observedEntityIndex = observedEntityIndex;
    }

    private sealed class ActiveStoryChoice
    {
        public ActiveStoryChoice(int index, Story story, Choice choice)
        {
            Index = index;
            Story = story;
            Choice = choice;
        }

        public int Index { get; }
        public Story Story { get; }
        public Choice Choice { get; }
    }
    
    private void OnEnable()
    {
        if (!TryResolveDockedLocalStory(out _currentLocation))
        {
            HideStorySurface();
            return;
        }

        _activeStory = _currentLocation.Story;
        Continue();
    }

    void Start()
    {
        ContinueTrigger.OnPointerClickAsObservable().Subscribe(pointerEvent =>
        {
            if (_activeStory == null) return;
            Continue();
        });
    }

    void Continue()
    {
        if (!_activeStory.state.previousPointer.isNull) _currentPath = _activeStory.state.previousPointer.path.head.name;
        if(_activeStory.canContinue) _activeStory.Continue();
        if (!_activeStory.state.previousPointer.isNull) _currentPath = _activeStory.state.previousPointer.path.head.name;
        if (Output != null)
            Output.text = _activeStory.currentText;

        _activeChoices.Clear();
        
        if(_activeStory.currentChoices.Any())
        {
            PresentCurrentChoices();
        }
        else if (!_activeStory.canContinue)
        {
            // There's no choices, but we also can't continue; indicates we hit an END
            if (_activeStory == _currentLocation.Story)
            {
                // END inside location-based story thread, restart the story
                _activeStory.ResetState();
                Continue();
            }
            else
            {
                // END inside quest content, switch back to location thread and present choices
                _activeStory = _currentLocation.Story;
                Continue();
            }
        }

        RenderStorySurface();
    }

    void PresentCurrentChoices()
    {
        Debug.Log($"Current Path: \"{_currentPath}\" in {(_activeStory == _currentLocation.Story ? "Location Story" : "Quest Story")}");
        if(!string.IsNullOrEmpty(_currentPath) && _currentLocation.KnotQuests.ContainsKey(_currentPath))
        {
            foreach (var quest in _currentLocation.KnotQuests[_currentPath])
            {
                if (quest.Story == _activeStory) continue; // Don't repeat choices for active injected branch
                
                quest.Story.ChoosePathString(_currentPath);
                quest.Story.ContinueMaximally();
                foreach (var choice in quest.Story.currentChoices) PresentChoice(quest.Story, choice);
            }
        }
        foreach (var choice in _activeStory.currentChoices) PresentChoice(_activeStory, choice);
    }

    void PresentChoice(Story story, Choice choice)
    {
        _activeChoices.Add(new ActiveStoryChoice(_activeChoices.Count, story, choice));
    }

    private void RenderStorySurface()
    {
        if (_activeStory == null)
            return;

        if (Output != null)
            Output.gameObject.SetActive(false);
        if (ChoiceParent != null)
            ChoiceParent.gameObject.SetActive(false);

        _surfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _surfaceDocument,
            "Aetheria Runtime Local Story Surface",
            AetheriaRuntimeLocalStorySurfaceBuilder.Build(ProjectStorySurface()),
            HandleStorySurfaceCommand,
            _surfaceChrome);
    }

    private AetheriaRuntimeLocalStorySurfaceState ProjectStorySurface()
    {
        return AetheriaRuntimeLocalStorySurfaceBuilder.Project(
            ResolveLocationLabel(),
            _currentPath,
            _activeStory?.currentText ?? "",
            _activeStory?.canContinue == true,
            _activeChoices.Select(choice => new AetheriaRuntimeLocalStoryChoiceState(
                choice.Index,
                choice.Choice?.text ?? "")),
            DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
    }

    private void HandleStorySurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (!AetheriaRuntimeLocalStorySurfaceCommands.TryRead(request, out var command))
        {
            Debug.LogWarning($"Unknown runtime local story command: {request?.Command}");
            return;
        }

        switch (command.Kind)
        {
            case AetheriaRuntimeLocalStoryCommandKind.Continue:
                Continue();
                return;
            case AetheriaRuntimeLocalStoryCommandKind.Choose:
                ChooseStoryOption(command.ChoiceIndex);
                return;
            default:
                Debug.LogWarning($"Unknown runtime local story command: {request?.Command}");
                return;
        }
    }

    private void ChooseStoryOption(int choiceIndex)
    {
        var selected = _activeChoices.FirstOrDefault(choice => choice.Index == choiceIndex);
        if (selected == null)
        {
            Debug.LogWarning($"Unknown runtime local story choice index: {choiceIndex}");
            return;
        }

        _activeStory = selected.Story;
        _activeStory.ChoosePath(selected.Choice.targetPath);
        Continue();
    }

    private string ResolveLocationLabel()
    {
        if (!string.IsNullOrWhiteSpace(_currentLocation?.Name))
            return _currentLocation.Name;
        if (!string.IsNullOrWhiteSpace(_currentLocation?.FileName))
            return _currentLocation.FileName;
        return "Local";
    }

    private bool TryResolveDockingState(out AetheriaClientDockingSnapshot dockingState)
    {
        _dockingState ??= ResolveClient().Aetheria().ReactiveDockingState();
        var resolved = _dockingState.TryCurrent(out dockingState);
        if (!resolved)
            Debug.LogWarning("Failed to read Aetheria docking state for local story surface.");
        return resolved;
    }

    private bool TryResolveDockedLocalStory(out LocationStory story)
    {
        story = null;
        if (_observedEntityIndex == null ||
            !TryResolveDockingState(out var dockingState) ||
            dockingState?.IsDocked != true ||
            string.IsNullOrWhiteSpace(dockingState.DockParentEntityKey) ||
            dockingState.DockingBayIndex < 0 ||
            !_observedEntityIndex.TryResolveDockingBayByRecordKey(
                dockingState.DockParentEntityKey,
                dockingState.DockingBayIndex,
                out var dockingBay) ||
            dockingBay?.Entity is not OrbitalEntity { Story: { } dockedStory })
        {
            return false;
        }

        story = dockedStory;
        return true;
    }

    private AetheriaClient ResolveClient()
    {
        var stateBoot = AetheriaRuntimeStateBoot.Inspect(AetheriaUnityRuntimePaths.GameDataDirectory);
        if (!string.Equals(_clientStatePath, stateBoot.StateFilePath, StringComparison.Ordinal))
        {
            _clientStatePath = stateBoot.StateFilePath;
            _dockingState?.Dispose();
            _dockingState = null;
        }

        return AetheriaUnityRuntimeClientProvider.ResolveClient(stateBoot, "unity-runtime-local-story");
    }

    private void HideStorySurface()
    {
        if (_surfaceDocument == null)
            return;

        AetheriaEveUnitySurfaceHost.Hide(_surfaceDocument);
    }

    private void OnDisable()
    {
        HideStorySurface();
    }

    private void OnDestroy()
    {
        if (_surfaceDocument != null)
        {
            AetheriaEveUnitySurfaceHost.DestroyDocument(_surfaceDocument);
            _surfaceDocument = null;
        }
        _dockingState?.Dispose();
        _dockingState = null;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

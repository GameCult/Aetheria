using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ink.Runtime;
using TMPro;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

public class LocalMenu : MonoBehaviour
{
    public ObservablePointerClickTrigger ContinueTrigger;
    public TextMeshProUGUI Output;
    public RectTransform ChoiceParent;
    public ChoicePrefab ChoicePrefab;

    private string _currentPath;
    private LocationStory _currentLocation;
    private Story _activeStory;
    private List<GameObject> _choiceInstances = new List<GameObject>();
    
    private void OnEnable()
    {
        if (ActionGameManager.Instance.DockedEntity is OrbitalEntity orbital)
        {
            _currentLocation = orbital.Story;
            _activeStory = _currentLocation.Story;
        }
        else return;
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
        Output.text = _activeStory.currentText;

        foreach(var instance in _choiceInstances)
            Destroy(instance);
        _choiceInstances.Clear();
        
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
        var choiceInstance = Instantiate(ChoicePrefab, ChoiceParent);
        choiceInstance.Label.text = choice.text;
        choiceInstance.Button.onClick.AddListener(() =>
        {
            _activeStory = story;
            _activeStory.ChoosePath(choice.targetPath);
            Continue();
        });
        _choiceInstances.Add(choiceInstance.gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

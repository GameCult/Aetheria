using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;
using Unity.Mathematics;
using UnityEngine.EventSystems;
using static Unity.Mathematics.math;

public class InputDisplayLayout : MonoBehaviour
{
    public RectTransform UnassignedBindingsGroup;
    public RectTransform AssignedBindingsGroup;
    public InputDisplayButton MouseLeft;
    public InputDisplayButton MouseRight;
    public InputDisplayButton MouseMiddle;
    public InputDisplayButton MouseForward;
    public InputDisplayButton MouseBack;
    public VerticalLayoutGroup RowContainer;
    public TextAsset LayoutFile;
    public Prototype LabelPrototype;
    public Prototype LinePrototype;
    //public UILineRendererList LineRenderer;
    public Prototype RowPrototype;
    public Prototype RowSpacerPrototype;
    public int KeySize = 64;
    public Color DefaultColor;
    public Color HighlightColor;
    public float FillBrightness = .5f;
    public float FillAlpha;
    public float FillMultiplier = .25f;
    public float InactiveMultiplier = .333f;
    public int PlacementSearchArea = 4;
    public float TestPadding = 4;
    public float BoxLineExpand = -1;
    public float GlobalHue = .333f;
    public float Saturation;
    public float GlobalHueRange = .1f;

    private Canvas _canvas;
    private InputLayout _inputLayout;
    private ActionMapping _dragAction;
    private ButtonMapping _originalButton;
    private ButtonMapping _previewButton;
    private ActionMapping _previewOriginalAction;
    private bool _previewOriginallyActionBar;
    
    public InputActionAsset Input { get; set; }

    private class ButtonMapping
    {
        public InputAction TestAction;
        public IBindableButton Button;
        public InputDisplayButton DisplayButton;
        public ActionMapping ActionMapping;
        public UILineRenderer LabelLine;
        public bool IsActionBarButton;
        public Rect ButtonRect => DisplayButton.GetComponent<RectTransform>().ScreenSpaceRect();
    }

    private class ActionMapping
    {
        public string Name;
        public InputBinding Binding;
        public InputAction Action;
        public InputDisplayLabel Label;
        public float Hue;
        public List<ButtonMapping> ButtonMappings = new List<ButtonMapping>();
        public Rect LabelRect => Label.GetComponent<RectTransform>().ScreenSpaceRect();
    }
    
    private Dictionary<string, ButtonMapping> _bindButtons = new Dictionary<string, ButtonMapping>();
    private List<ButtonMapping> _buttonMappings = new List<ButtonMapping>();
    private List<ActionMapping> _actionMappings = new List<ActionMapping>();
    
    void Start()
    {
        _canvas = transform.root.GetComponent<Canvas>();
        var path = Path.Combine(ActionGameManager.GameDataDirectory.CreateSubdirectory("KeyboardLayouts").FullName, $"{LayoutFile.name}.msgpack");
        // _inputLayout = ParseJson(LayoutFile.text);
        RegisterResolver.Register();
        _inputLayout = MessagePackSerializer.Deserialize<InputLayout>(File.ReadAllBytes(path));
        // _inputLayout = JsonConvert.DeserializeObject<InputLayout>(File.ReadAllText(path));
        // SaveLayout();
        DisplayLayout(_inputLayout);
        
        _buttonMappings.Add(MapMouseButton(MouseLeft, "<Mouse>/leftButton"));
        _buttonMappings.Add(MapMouseButton(MouseRight, "<Mouse>/rightButton"));
        _buttonMappings.Add(MapMouseButton(MouseMiddle, "<Mouse>/middleButton"));
        _buttonMappings.Add(MapMouseButton(MouseForward, "<Mouse>/forwardButton"));
        _buttonMappings.Add(MapMouseButton(MouseBack, "<Mouse>/backButton"));
        
        Canvas.ForceUpdateCanvases();

        foreach (var buttonMapping in _buttonMappings)
        {
            _bindButtons[buttonMapping.Button.InputSystemPath] = buttonMapping;
            
            buttonMapping.TestAction = new InputAction(binding: buttonMapping.Button.InputSystemPath);
            buttonMapping.TestAction.started += context =>
            {
                buttonMapping.DisplayButton.Outline.color = HighlightColor;
                buttonMapping.DisplayButton.Outline.gameObject.SetActive(true);
                var fillColor = HighlightColor;
                fillColor *= FillBrightness;
                fillColor.a = FillAlpha;
                buttonMapping.DisplayButton.Fill.color = fillColor;
            };
            buttonMapping.TestAction.canceled += context => AssignColor(buttonMapping);
            buttonMapping.TestAction.Enable();
        }
        
        foreach(var actionBarInput in ActionGameManager.PlayerSettings.InputSettings.ActionBarInputs)
        {
            if (!_bindButtons.ContainsKey(actionBarInput)) Debug.LogError($"Unable to find input button for \"{actionBarInput}\"");
            else
            {
                _bindButtons[actionBarInput].IsActionBarButton = true;
                AssignColor(_bindButtons[actionBarInput]);
            }
        }
        
        if(Input==null)
        {
            var input = new AetheriaInput();
            Input = input.asset;
        }
        ProcessActions(Input);
        // input.Enable();
        // input.Global.Interact.performed += context => Debug.Log($"Interact Performed! Time = {((int) (Time.time * 1000)).ToString()}ms");

        Observable.NextFrame().Subscribe(_ =>
        {
            foreach (var actionMapping in _actionMappings)
            {
                actionMapping.Label = CreateLabel(actionMapping);
                PlaceLabel(actionMapping);
            }

            RegisterMouseCallbacks();
        });

        //StartCoroutine(AssociateInputKeys(_inputLayout));
    }

    private ButtonMapping MapMouseButton(InputDisplayButton button, string path)
    {
        var mapping = new ButtonMapping
        {
            Button = new InputLayoutMouseButton {Path = path},
            DisplayButton = button
        };
        AssignUnboundColor(mapping.DisplayButton);
        return mapping;
    }

    private void ProcessActions(InputActionAsset input)
    {
        var bindableActions = input.Where(a => a.actionMap.name != "UI" && (a.type==InputActionType.Button || a.bindings.Any(b=>b.isComposite))).ToArray();
        for (var i = 0; i < bindableActions.Length; i++)
        {
            var action = bindableActions[i];
            foreach (var binding in action.bindings)
            {
                if (_bindButtons.ContainsKey(binding.effectivePath))
                {
                    var name = action.name.Replace(' ', '\n');
                    if (binding.isPartOfComposite) name = $"{name} {binding.name}";
                    var actionMapping = new ActionMapping
                    {
                        Name = name,
                        Binding = binding,
                        Action = action,
                        Hue = action.actionMap.name == "Global"
                            ? GlobalHue
                            : frac(GlobalHue + GlobalHueRange + (float) i / (bindableActions.Length - 1) * (1 - GlobalHueRange * 2))
                    };
                    _actionMappings.Add(actionMapping);
                    
                    var buttonMapping = _bindButtons[binding.effectivePath];
                    buttonMapping.ActionMapping = actionMapping;
                    AssignColor(buttonMapping);
                    actionMapping.ButtonMappings.Add(buttonMapping);
                }
            }
        }
    }

    private void AssignColor(ButtonMapping buttonMapping)
    {
        if (buttonMapping.IsActionBarButton)
        {
            var outlineColor = Color.white;
            var fillColor = Color.white * FillBrightness;
            fillColor.a = FillAlpha;
            buttonMapping.DisplayButton.Fill.color = fillColor;
            buttonMapping.DisplayButton.Outline.gameObject.SetActive(true);
            buttonMapping.DisplayButton.Outline.color = outlineColor;
            if (buttonMapping.DisplayButton is InputDisplayKey key) key.MainLabel.color = key.AltLabel.color = outlineColor;
        }
        else if(buttonMapping.ActionMapping!=null)
        {
            var outlineColor = Color.HSVToRGB(buttonMapping.ActionMapping.Hue, Saturation, 1);
            var fillColor = Color.HSVToRGB(buttonMapping.ActionMapping.Hue, 1, FillBrightness);
            fillColor.a = FillAlpha;
            buttonMapping.DisplayButton.Fill.color = fillColor;
            buttonMapping.DisplayButton.Outline.gameObject.SetActive(true);
            buttonMapping.DisplayButton.Outline.color = outlineColor;
            if (buttonMapping.DisplayButton is InputDisplayKey key) key.MainLabel.color = key.AltLabel.color = outlineColor;
        }
        else AssignUnboundColor(buttonMapping.DisplayButton);
    }

    private void AssignUnboundColor(InputDisplayButton button, bool bindable = true)
    {
        button.Outline.gameObject.SetActive(false);
        var outlineColor = DefaultColor;
        outlineColor.a *= InactiveMultiplier;
        var fillColor = DefaultColor * FillBrightness;
        fillColor *= FillBrightness;
        if(!bindable)
            fillColor *= InactiveMultiplier;
        fillColor.a = FillAlpha;
        button.Fill.color = fillColor;
        if (button is InputDisplayKey key) key.MainLabel.color = key.AltLabel.color = Color.Lerp(outlineColor, fillColor, .5f);
    }

    private InputDisplayLabel CreateLabel(ActionMapping actionMapping)
    {
        var label = LabelPrototype.Instantiate<InputDisplayLabel>();
        label.Label.color = Color.HSVToRGB(actionMapping.Hue, Saturation, 1);
        actionMapping.Label = label;
        label.Label.text = actionMapping.Name;
        return label;
    }

    private void PlaceLabel(ActionMapping actionMapping)
    {
        if(actionMapping.ButtonMappings.Any())
        {
            var labelRect = actionMapping.Label.Label.rectTransform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(labelRect);
            var rect = FindLabelPosition(actionMapping.ButtonMappings.First().DisplayButton.GetComponent<RectTransform>(), labelRect);
            //actionMapping.LabelRect = rect;
            actionMapping.Label.transform.parent = AssignedBindingsGroup;
            actionMapping.Label.transform.position = rect.center;
        }
        else
        {
            actionMapping.Label.transform.parent = UnassignedBindingsGroup;
        }
        foreach(var buttonMapping in actionMapping.ButtonMappings) ConnectButtonToLabel(buttonMapping);
    }

    private void ConnectButtonToLabel(ButtonMapping buttonMapping)
    {
        if (buttonMapping.ActionMapping != null)
        {
            var keyBounds = buttonMapping.DisplayButton.Outline.rectTransform.GetBounds(BoxLineExpand);
            var labelPoint = (Vector2) buttonMapping.ActionMapping.Label.Label.rectTransform.GetBounds().ClosestPoint(keyBounds.center);
            var keyPoint = (Vector2) keyBounds.ClosestPoint(labelPoint);
            labelPoint /= _canvas.scaleFactor;
            keyPoint /= _canvas.scaleFactor;

            buttonMapping.LabelLine ??= LinePrototype.Instantiate<UILineRenderer>();
            buttonMapping.LabelLine.color = buttonMapping.ActionMapping.Label.Label.color;
            buttonMapping.LabelLine.Points = new[] {keyPoint, labelPoint};
        }
        else
        {
            buttonMapping.LabelLine?.GetComponent<Prototype>().ReturnToPool();
            buttonMapping.LabelLine = null;
        }
    }

    private Rect FindLabelPosition(RectTransform key, RectTransform label)
    {
        var keyRect = key.ScreenSpaceRect();
        var labelRect = label.ScreenSpaceRect(TestPadding*_canvas.scaleFactor);
        var localRects = Overlap(key.ScreenSpaceRect(PlacementSearchArea * KeySize * _canvas.scaleFactor));
        var centroid = localRects.Aggregate(Vector2.zero, (v, r) => v + r.center) / localRects.Length;
        var keyCenter = keyRect.center;
        var dir = (keyCenter - centroid).normalized;
        for (int dist = KeySize; dist < PlacementSearchArea * KeySize; dist++)
        {
            for (float theta = 0; theta < PI * 2; theta += PI / 32)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    var testPos = keyCenter + dir.Rotate(theta * s) * dist * _canvas.scaleFactor;
                    var testRect = new Rect(testPos - labelRect.size / 2, labelRect.size);
                    if(!localRects.Any(r => r.Overlaps(testRect)))
                    {
                        return testRect;
                    }
                }
            }
        }
        return default;
    }

    private ActionMapping[] OverlappingLabels(Rect rect) => _actionMappings
        .Where(a => a.LabelRect.Overlaps(rect)).ToArray();
    
    private Rect[] Overlap(Rect rect) => _buttonMappings
        .Where(b=>b.IsActionBarButton || b.ActionMapping!=null)
        .Select(b=>b.ButtonRect)
        .Concat(_actionMappings
            .Where(a=>a.Label && a.LabelRect.width > .1f)
            .Select(a=>a.LabelRect))
        .Where(r => r.Overlaps(rect)).ToArray();

    public void DisplayLayout(InputLayout layout)
    {
        foreach (var row in layout.Rows)
        {
            if (row is InputLayoutKeyRow keyRow)
            {
                var displayRow = RowPrototype.Instantiate<InputDisplayRow>();
                foreach (var column in keyRow.Columns)
                {
                    if (column is InputLayoutColumnSpacer)
                    {
                        displayRow.KeySpacerPrototype.Instantiate<LayoutElement>().preferredWidth = KeySize * column.Width;
                    }
                    else if (column is InputLayoutKey)
                    {
                        var displayKey = displayRow.KeyPrototype.Instantiate<InputDisplayKey>();
                        displayKey.LayoutElement.preferredHeight = KeySize;
                        displayKey.LayoutElement.preferredWidth = KeySize * column.Width;
                        displayKey.Outline.gameObject.SetActive(false);
                        displayKey.Outline.color = DefaultColor;
                        AssignUnboundColor(displayKey, column is InputLayoutBindableKey);

                        if (column is InputLayoutBindableKey key)
                        {
                            var buttonMapping = new ButtonMapping
                            {
                                Button = key, 
                                DisplayButton = displayKey
                            };
                            _bindButtons[key.InputSystemPath] = buttonMapping;
                            _buttonMappings.Add(buttonMapping);
                            displayKey.MainLabel.text = key.MainLabel;
                            displayKey.AltLabel.text = key.AltLabel;

                            if (column is InputLayoutMultiRowKey multiRowKey)
                            {
                                displayKey.Outline.rectTransform.anchorMin =
                                    displayKey.MainLabel.rectTransform.anchorMin =
                                        displayKey.AltLabel.rectTransform.anchorMin =
                                            displayKey.Fill.rectTransform.anchorMin = Vector2.down * (multiRowKey.Height - 1);
                            }
                        }
                        else
                        {
                            displayKey.MainLabel.text = "";
                            displayKey.AltLabel.text = "";
                        }
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(nameof(column));
                    }
                }
            }
            else if (row is InputLayoutRowSpacer rowSpacer)
            {
                RowSpacerPrototype.Instantiate<LayoutElement>().preferredHeight = KeySize * rowSpacer.Height;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }
        }
    }

    public InputLayout ParseJson(string layout)
    {
        var rows = new List<InputLayoutRow>();
        var nextWidth = 1f;
        var nextHeight = 1;
        var reader = new JsonTextReader(new StringReader(layout));
        
        reader.Read();
        if (reader.TokenType != JsonToken.StartArray)
            throw new JsonReaderException($"Unexpected JSON format in line {reader.LineNumber}:{reader.LinePosition}, expected: StartArray, received: {Enum.GetName(typeof(JsonToken), reader.TokenType)}");
        
        while(reader.Read() && reader.TokenType != JsonToken.EndArray)
        {
            if (reader.TokenType != JsonToken.StartArray)
                throw new JsonReaderException($"Unexpected JSON format in line {reader.LineNumber}:{reader.LinePosition}, expected: StartArray, received: {Enum.GetName(typeof(JsonToken), reader.TokenType)}");

            var row = new InputLayoutKeyRow();
            var columns = new List<InputLayoutColumn>();

            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                switch (reader.TokenType)
                {
                    case JsonToken.StartObject:
                        while(reader.Read() && reader.TokenType != JsonToken.EndObject)
                        {
                            switch (reader.Value.ToString())
                            {
                                case "a": break;
                                case "w":
                                    nextWidth = (float) (reader.ReadAsDouble() ?? 1.0);
                                    break;
                                case "h":
                                    nextHeight = reader.ReadAsInt32() ?? 1;
                                    break;
                                case "y":
                                    rows.Add(new InputLayoutRowSpacer{Height = (float) (reader.ReadAsDouble() ?? 0.0)});
                                    break;
                                case "x":
                                    columns.Add(new InputLayoutColumnSpacer{Width = (float) (reader.ReadAsDouble() ?? 0.0)});
                                    break;
                            }
                        }
                        break;
                    case JsonToken.String:
                        InputLayoutKey key;
                        var v = reader.Value.ToString().Trim();
                        if (!string.IsNullOrEmpty(v))
                        {
                            var labels = v.Split('\n');
                            key = nextHeight != 1 ? new InputLayoutMultiRowKey {Height = nextHeight} : new InputLayoutBindableKey();
                            nextHeight = 1;
                            ((InputLayoutBindableKey) key).MainLabel = labels.Length == 2 ? labels[1] : v;
                            ((InputLayoutBindableKey) key).AltLabel = labels.Length == 2 ? labels[0] : "";
                        }
                        else
                            key = new InputLayoutKey();

                        key.Width = nextWidth;
                        columns.Add(key);
                        nextWidth = 1f;
                        break;
                    default:
                        throw new JsonReaderException($"Unexpected JSON format in line {reader.LineNumber}:{reader.LinePosition}");
                }
            }

            row.Columns = columns.ToArray();
            rows.Add(row);
        }

        return new InputLayout {Rows = rows.ToArray()};
    }

    private IEnumerator AssociateInputKeys()
    {
        var path = "";
        var keyPress = new InputAction(binding: "/<Keyboard>/<button>");
        keyPress.performed += context =>
        {
            if (context.control.path.EndsWith("anyKey")) return;
            path = context.control.path;
        };
        keyPress.Enable();
        foreach (var button in _buttonMappings)
        {
            button.DisplayButton.Outline.color = HighlightColor;
            var fillColor = HighlightColor;
            fillColor *= FillMultiplier;
            fillColor.a = FillAlpha;
            button.DisplayButton.Fill.color = fillColor;
                        
            path = "";
            while (string.IsNullOrEmpty(path)) yield return null;
            button.Button.InputSystemPath = path;
            //Debug.Log($"Bound \"{path}\" to \"{bindableKey.MainLabel}\"");
                        
            button.DisplayButton.Outline.color = DefaultColor;
            fillColor = DefaultColor;
            fillColor *= FillMultiplier;
            fillColor.a = FillAlpha;
            button.DisplayButton.Fill.color = fillColor;
        }
        keyPress.Disable();

        SaveLayout();
    }

    private void SaveLayout()
    {
        RegisterResolver.Register();
        File.WriteAllBytes(
            Path.Combine(ActionGameManager.GameDataDirectory.CreateSubdirectory("KeyboardLayouts").FullName, $"{LayoutFile.name}.msgpack"),
            MessagePackSerializer.Serialize(_inputLayout));
    }

    private void OnEnable()
    {
        foreach(var buttonMapping in _buttonMappings)
            buttonMapping.TestAction.Enable();
        RegisterMouseCallbacks();
    }

    private void RegisterMouseCallbacks()
    {
        void EndDrag(PointerEventData _)
        {
            if (_previewButton == null)
            {
                if(_originalButton!=null)
                {
                    _originalButton.ActionMapping = _dragAction;
                    _dragAction.ButtonMappings.Add(_originalButton);
                    AssignColor(_originalButton);
                }
                PlaceLabel(_dragAction);
            }
            else
            {
                _dragAction.Binding.overridePath = _previewButton.Button.InputSystemPath;
                _dragAction.Action.ApplyBindingOverride(_dragAction.Binding);
                ActionGameManager.PlayerSettings.InputSettings.InputActionMap[(_dragAction.Action.name,
                    _dragAction.Action.GetBindingIndex(_dragAction.Binding))] = _previewButton.Button.InputSystemPath;
                // TODO: Assign New Binding
            }

            foreach(var action in _actionMappings)
                action.Label.Label.raycastTarget = true;
            _dragAction = null;
            _originalButton = null;
            _previewButton = null;
            _previewOriginalAction = null;
        }

        foreach (var buttonMapping in _buttonMappings)
        {
            // On click: when a specific action is not assigned, toggle its availability on the action bar
            buttonMapping.DisplayButton.ClickTrigger.OnPointerClickAsObservable()
                .Where(_=>buttonMapping.ActionMapping==null)
                .Subscribe(data =>
            {
                buttonMapping.IsActionBarButton = !buttonMapping.IsActionBarButton;
                if (buttonMapping.IsActionBarButton)
                {
                    ActionGameManager.PlayerSettings.InputSettings.ActionBarInputs.Add(buttonMapping.Button.InputSystemPath);
                    foreach(var overlap in OverlappingLabels(buttonMapping.ButtonRect))
                        PlaceLabel(overlap);
                }
                else
                {
                    ActionGameManager.PlayerSettings.InputSettings.ActionBarInputs.Remove(buttonMapping.Button.InputSystemPath);
                }
                AssignColor(buttonMapping);
            });

            buttonMapping.DisplayButton.BeginDragTrigger.OnBeginDragAsObservable()
                .Where(_ => buttonMapping.ActionMapping != null)
                .Subscribe(_ =>
                {
                    _originalButton = buttonMapping;
                    buttonMapping.LabelLine.Points = new Vector2[0];
                    _dragAction = buttonMapping.ActionMapping;
                    foreach(var action in _actionMappings)
                        action.Label.Label.raycastTarget = false;
                });

            buttonMapping.DisplayButton.EnterTrigger.OnPointerEnterAsObservable()
                .Where(_ => _dragAction != null)
                .Subscribe(_ =>
                {
                    _previewButton = buttonMapping;
                    _previewOriginalAction = buttonMapping.ActionMapping;
                    _previewOriginallyActionBar = buttonMapping.IsActionBarButton;
                    buttonMapping.IsActionBarButton = false;

                    buttonMapping.ActionMapping?.ButtonMappings.Remove(buttonMapping);
                    buttonMapping.ActionMapping = _dragAction;
                    _dragAction.ButtonMappings.Add(buttonMapping);
                    if(_previewOriginalAction!=null)
                        PlaceLabel(_previewOriginalAction);
                    PlaceLabel(_dragAction);
                    AssignColor(buttonMapping);
                    foreach(var overlap in OverlappingLabels(buttonMapping.ButtonRect))
                        PlaceLabel(overlap);
                });

            buttonMapping.DisplayButton.ExitTrigger.OnPointerExitAsObservable()
                .Where(_ => _dragAction != null)
                .Subscribe(_ =>
                {
                    _dragAction.ButtonMappings.Remove(buttonMapping);
                    if (buttonMapping == _originalButton)
                    {
                        buttonMapping.ActionMapping = null;
                    }
                    else if(buttonMapping == _previewButton)
                    {
                        buttonMapping.IsActionBarButton = _previewOriginallyActionBar;
                        buttonMapping.ActionMapping = _previewOriginalAction;
                        if (!_previewOriginallyActionBar)
                        {
                            if(_previewOriginalAction != null)
                            {
                                _previewOriginalAction.ButtonMappings.Add(buttonMapping);
                                PlaceLabel(_previewOriginalAction);
                            }
                        }
                    }

                    ConnectButtonToLabel(buttonMapping);
                    AssignColor(buttonMapping);
                    _previewButton = null;
                    _previewOriginalAction = null;
                });

            buttonMapping.DisplayButton.EndDragTrigger.OnEndDragAsObservable()
                .Where(_ => _dragAction != null)
                .Subscribe(EndDrag);
        }
        
        foreach (var actionMapping in _actionMappings)
        {
            actionMapping.Label.BeginDragTrigger.OnBeginDragAsObservable().Subscribe(_ =>
            {
                _dragAction = actionMapping;
                actionMapping.Label.transform.parent = AssignedBindingsGroup;
                foreach (var action in _actionMappings)
                    action.Label.Label.raycastTarget = false;
                if (actionMapping.ButtonMappings.Count == 1)
                {
                    var buttonMapping = actionMapping.ButtonMappings[0];
                    _originalButton = buttonMapping;
                    buttonMapping.LabelLine.Points = new Vector2[0];
                    buttonMapping.ActionMapping = null;
                    actionMapping.ButtonMappings.Clear();
                    ConnectButtonToLabel(buttonMapping);
                    AssignColor(buttonMapping);
                }
            });
            actionMapping.Label.EndDragTrigger.OnEndDragAsObservable().Subscribe(EndDrag);
        }
    }

    private void OnDisable()
    {
        foreach(var buttonMapping in _buttonMappings)
            buttonMapping.TestAction.Disable();
    }

    void Update()
    {
        if (_dragAction != null && _previewButton == null)
        {
            _dragAction.Label.transform.position = Mouse.current.position.ReadValue();
        }
    }
}

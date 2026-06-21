using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Aetheria.EveRuntime;
using GameCult.Aetheria.State.Verse;
using GameCult.Eve.Surface;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UIElements;

public class InputDisplayLayout : MonoBehaviour
{
    private static readonly string[] DefaultActionBarCandidatePaths =
    {
        "<Mouse>/leftButton",
        "<Mouse>/rightButton",
        "<Mouse>/middleButton",
        "<Mouse>/forwardButton",
        "<Mouse>/backButton",
        "<Keyboard>/1",
        "<Keyboard>/2",
        "<Keyboard>/3",
        "<Keyboard>/4",
        "<Keyboard>/5",
        "<Keyboard>/leftShift"
    };

    private UIDocument _surfaceDocument;
    private AetheriaInput _ownedInput;
    private InputAction _captureAction;
    private InputActionAsset _input;
    private string _captureActionName = "";
    private int _captureBindingIndex = -1;
    private string _captureBindingLabel = "";
    private readonly AetheriaEveUnitySurfaceChrome _surfaceChrome = new AetheriaEveUnitySurfaceChrome
    {
        RootAlignItems = Align.Center,
        RootJustifyContent = Justify.Center,
        RootPaddingLeft = 24f,
        RootPaddingRight = 24f,
        RootPaddingTop = 24f,
        RootPaddingBottom = 24f,
        RootBackgroundColor = new Color(0f, 0f, 0f, 0.72f),
        Width = 1080f,
        MinWidth = 0f,
        MaxWidth = 1080f,
        MaxHeight = 900f,
        FlexGrow = 1f,
        PaddingLeft = 20f,
        PaddingRight = 20f,
        PaddingTop = 20f,
        PaddingBottom = 20f,
        BorderRadius = 0f,
        BorderWidth = 0f,
        BackgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.96f)
    };

    public InputActionAsset Input
    {
        get => _input;
        set
        {
            _input = value;
            if (isActiveAndEnabled)
            {
                RenderSurface();
            }
        }
    }

    private void Start()
    {
        EnsureInputAsset();
        EnsureCaptureAction();
        HideLegacyChildren();
        RenderSurface();
    }

    private void OnEnable()
    {
        EnsureInputAsset();
        EnsureCaptureAction();
        HideLegacyChildren();
        RenderSurface();
    }

    private void OnDisable()
    {
        ClearCapture();
        if (_captureAction != null)
        {
            _captureAction.Disable();
        }

        if (_surfaceDocument != null)
        {
            AetheriaEveUnitySurfaceHost.Hide(_surfaceDocument);
        }
    }

    private void OnDestroy()
    {
        _captureAction?.Dispose();
        _captureAction = null;

        _ownedInput?.Dispose();
        _ownedInput = null;

        if (_surfaceDocument != null)
        {
            AetheriaEveUnitySurfaceHost.DestroyDocument(_surfaceDocument);
            _surfaceDocument = null;
        }
    }

    private void EnsureInputAsset()
    {
        if (Input != null)
        {
            return;
        }

        _ownedInput ??= new AetheriaInput();
        Input = _ownedInput.asset;
    }

    private void HideLegacyChildren()
    {
        for (var index = 0; index < transform.childCount; index++)
        {
            var child = transform.GetChild(index);
            if (_surfaceDocument != null && child.gameObject == _surfaceDocument.gameObject)
            {
                continue;
            }

            child.gameObject.SetActive(false);
        }
    }

    private void EnsureCaptureAction()
    {
        if (_captureAction != null)
        {
            return;
        }

        _captureAction = new InputAction("Aetheria Input Capture");
        _captureAction.AddBinding("<Keyboard>/anyKey");
        _captureAction.AddBinding("<Mouse>/leftButton");
        _captureAction.AddBinding("<Mouse>/rightButton");
        _captureAction.AddBinding("<Mouse>/middleButton");
        _captureAction.AddBinding("<Mouse>/forwardButton");
        _captureAction.AddBinding("<Mouse>/backButton");
        _captureAction.performed += OnCapturePerformed;
    }

    private void RenderSurface()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        EnsureInputAsset();
        EnsureCaptureAction();

        var document = AetheriaRuntimeInputSettingsSurfaceBuilder.Build(ProjectSurfaceState());

        _surfaceDocument = AetheriaEveUnitySurfaceHost.RenderRuntime(
            transform,
            _surfaceDocument,
            "Aetheria Input Surface",
            document,
            HandleSurfaceCommand,
            _surfaceChrome);
    }

    private AetheriaRuntimeInputSettingsSurfaceState ProjectSurfaceState()
    {
        return new AetheriaRuntimeInputSettingsSurfaceState(
            ProjectBindingRows(),
            ProjectActionBarRows(),
            capturePending: _captureBindingIndex >= 0 && !string.IsNullOrWhiteSpace(_captureActionName),
            capturePrompt: BuildCapturePrompt(),
            updatedAtUtc: DateTime.UtcNow.ToString("O"));
    }

    private IReadOnlyList<AetheriaRuntimeInputBindingSurfaceState> ProjectBindingRows()
    {
        if (Input == null)
        {
            return Array.Empty<AetheriaRuntimeInputBindingSurfaceState>();
        }

        return Input
            .Where(action => action.actionMap.name != "UI")
            .SelectMany(action =>
                action.bindings
                    .Select((binding, bindingIndex) => new { action, binding, bindingIndex }))
            .Where(entry =>
                !entry.binding.isComposite &&
                !string.IsNullOrWhiteSpace(entry.binding.effectivePath) &&
                IsSupportedCapturePath(entry.binding.effectivePath))
            .Select(entry => new AetheriaRuntimeInputBindingSurfaceState(
                entry.action.name,
                entry.bindingIndex,
                DescribeBinding(entry.action.name, entry.binding),
                DescribeInputPath(entry.binding.effectivePath)))
            .OrderBy(entry => entry.BindingLabel, StringComparer.Ordinal)
            .ToArray();
    }

    private IReadOnlyList<AetheriaRuntimeActionBarInputSurfaceState> ProjectActionBarRows()
    {
        var runtimeSettings = ActionGameManager.RuntimePlayerSettings.InputSettings;
        var candidates = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var defaultPath in DefaultActionBarCandidatePaths)
        {
            candidates[defaultPath] = DescribeInputPath(defaultPath);
        }

        foreach (var inputPath in runtimeSettings.ActionBarInputs)
        {
            if (!string.IsNullOrWhiteSpace(inputPath))
            {
                candidates[inputPath] = DescribeInputPath(inputPath);
            }
        }

        if (Input != null)
        {
            foreach (var action in Input.Where(action => action.actionMap.name != "UI"))
            {
                foreach (var binding in action.bindings)
                {
                    if (binding.isComposite || string.IsNullOrWhiteSpace(binding.effectivePath) || !IsSupportedCapturePath(binding.effectivePath))
                    {
                        continue;
                    }

                    candidates[binding.effectivePath] = DescribeInputPath(binding.effectivePath);
                }
            }
        }

        return candidates
            .Select(entry => new AetheriaRuntimeActionBarInputSurfaceState(
                entry.Key,
                entry.Value,
                runtimeSettings.ActionBarInputs.Contains(entry.Key)))
            .ToArray();
    }

    private static bool IsSupportedCapturePath(string path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
               (path.StartsWith("<Keyboard>/", StringComparison.Ordinal) ||
                path.StartsWith("<Mouse>/", StringComparison.Ordinal));
    }

    private static string DescribeBinding(string actionName, InputBinding binding)
    {
        if (binding.isPartOfComposite && !string.IsNullOrWhiteSpace(binding.name))
        {
            return $"{actionName} {binding.name}";
        }

        return actionName;
    }

    private static string DescribeInputPath(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return "Unbound";
        }

        var label = InputControlPath.ToHumanReadableString(
            inputPath,
            InputControlPath.HumanReadableStringOptions.OmitDevice);
        return string.IsNullOrWhiteSpace(label) ? inputPath : label;
    }

    private string BuildCapturePrompt()
    {
        if (_captureBindingIndex < 0 || string.IsNullOrWhiteSpace(_captureActionName))
        {
            return "";
        }

        return $"Capture pending for {_captureBindingLabel}. Press a keyboard or mouse input now.";
    }

    private void HandleSurfaceCommand(EveSurfaceCommandRequest request)
    {
        if (!AetheriaRuntimeInputSettingsSurfaceCommands.TryRead(request, out var command))
        {
            Debug.LogWarning($"Unknown input-settings command: {request?.Command}");
            return;
        }

        switch (command.Kind)
        {
            case AetheriaRuntimeInputSettingsCommandKind.Refresh:
                RenderSurface();
                return;
            case AetheriaRuntimeInputSettingsCommandKind.CancelCapture:
                ClearCapture();
                RenderSurface();
                return;
            case AetheriaRuntimeInputSettingsCommandKind.BeginCapture:
                BeginCapture(command);
                return;
            case AetheriaRuntimeInputSettingsCommandKind.ToggleActionBar:
                ToggleActionBarInput(command);
                return;
            default:
                Debug.LogWarning($"Unknown input-settings command: {request?.Command}");
                return;
        }
    }

    private void BeginCapture(AetheriaRuntimeInputSettingsSurfaceCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ActionName) || command.BindingIndex < 0)
        {
            Debug.LogWarning("Input capture requested without a valid action name and binding index.");
            return;
        }

        _captureActionName = command.ActionName;
        _captureBindingIndex = command.BindingIndex;
        _captureBindingLabel = string.IsNullOrWhiteSpace(command.BindingLabel)
            ? command.ActionName
            : command.BindingLabel;
        _captureAction?.Enable();
        RenderSurface();
    }

    private void ToggleActionBarInput(AetheriaRuntimeInputSettingsSurfaceCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.InputPath))
        {
            Debug.LogWarning("Action-bar toggle requested without a valid input path and enabled state.");
            return;
        }

        ActionGameManager.RequestRuntimeActionBarInput(command.InputPath, command.Enabled);
        RenderSurface();
    }

    private void OnCapturePerformed(InputAction.CallbackContext context)
    {
        if (_captureBindingIndex < 0 || string.IsNullOrWhiteSpace(_captureActionName))
        {
            return;
        }

        var inputPath = context.control?.path ?? "";
        if (string.IsNullOrWhiteSpace(inputPath) ||
            inputPath.EndsWith("anyKey", StringComparison.Ordinal) ||
            !IsSupportedCapturePath(inputPath) ||
            Input == null)
        {
            return;
        }

        var action = Input.FindAction(_captureActionName, throwIfNotFound: false);
        if (action == null || _captureBindingIndex >= action.bindings.Count)
        {
            Debug.LogWarning($"Input capture target no longer exists for {_captureActionName}:{_captureBindingIndex}.");
            ClearCapture();
            RenderSurface();
            return;
        }

        action.ApplyBindingOverride(_captureBindingIndex, inputPath);
        ActionGameManager.RequestRuntimeInputBindingOverride(action.name, _captureBindingIndex, inputPath);
        ClearCapture();
        RenderSurface();
    }

    private void ClearCapture()
    {
        _captureActionName = "";
        _captureBindingIndex = -1;
        _captureBindingLabel = "";
    }

}

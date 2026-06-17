using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCult.Aetheria.State.Unity;
using GameCult.Eve.Surface;
using GameCult.Eve.UnityUIToolkit;
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
        EnsureSurfaceDocument();
        EnsureCaptureAction();
        HideLegacyChildren();
        RenderSurface();
    }

    private void OnEnable()
    {
        EnsureInputAsset();
        EnsureSurfaceDocument();
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
            _surfaceDocument.rootVisualElement.Clear();
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
            Destroy(_surfaceDocument.gameObject);
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

    private void EnsureSurfaceDocument()
    {
        if (_surfaceDocument != null)
        {
            return;
        }

        var host = new GameObject("Aetheria Input Surface");
        host.transform.SetParent(transform, false);
        host.layer = gameObject.layer;
        _surfaceDocument = host.AddComponent<UIDocument>();
        _surfaceDocument.sortingOrder = 1000;
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
        EnsureSurfaceDocument();
        EnsureCaptureAction();

        var document = ToEveSurfaceDocument(
            AetheriaRuntimeInputSettingsSurfaceBuilder.Build(ProjectSurfaceState()));

        var root = _surfaceDocument.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1;
        root.style.justifyContent = Justify.Center;
        root.style.alignItems = Align.Center;
        root.style.paddingLeft = 24;
        root.style.paddingRight = 24;
        root.style.paddingTop = 24;
        root.style.paddingBottom = 24;
        root.style.backgroundColor = new Color(0f, 0f, 0f, 0.72f);

        var shell = new VisualElement();
        shell.style.flexDirection = FlexDirection.Column;
        shell.style.width = 1080;
        shell.style.maxWidth = 1080;
        shell.style.maxHeight = 900;
        shell.style.flexGrow = 1;
        shell.style.paddingLeft = 20;
        shell.style.paddingRight = 20;
        shell.style.paddingTop = 20;
        shell.style.paddingBottom = 20;
        shell.style.backgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.96f);
        root.Add(shell);

        var lowerer = new EveUiToolkitSurfaceLowerer();
        shell.Add(lowerer.Lower(document, HandleSurfaceCommand));
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
        switch (request.Command)
        {
            case var command when string.Equals(command, AetheriaRuntimeInputSettingsCommands.Refresh, StringComparison.Ordinal):
                RenderSurface();
                return;
            case var command when string.Equals(command, AetheriaRuntimeInputSettingsCommands.CancelCapture, StringComparison.Ordinal):
                ClearCapture();
                RenderSurface();
                return;
            case var command when string.Equals(command, AetheriaRuntimeInputSettingsCommands.BeginCapture, StringComparison.Ordinal):
                BeginCapture(request.Payload);
                return;
            case var command when string.Equals(command, AetheriaRuntimeInputSettingsCommands.ToggleActionBar, StringComparison.Ordinal):
                ToggleActionBarInput(request.Payload);
                return;
            default:
                Debug.LogWarning($"Unknown input-settings command: {request.Command}");
                return;
        }
    }

    private void BeginCapture(IReadOnlyDictionary<string, string> payload)
    {
        if (payload == null ||
            !payload.TryGetValue("actionName", out var actionName) ||
            !payload.TryGetValue("bindingIndex", out var bindingIndexText) ||
            !int.TryParse(bindingIndexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bindingIndex))
        {
            Debug.LogWarning("Input capture requested without a valid action name and binding index.");
            return;
        }

        _captureActionName = actionName;
        _captureBindingIndex = bindingIndex;
        _captureBindingLabel = payload.TryGetValue("bindingLabel", out var bindingLabel)
            ? bindingLabel ?? actionName
            : actionName;
        _captureAction?.Enable();
        RenderSurface();
    }

    private void ToggleActionBarInput(IReadOnlyDictionary<string, string> payload)
    {
        if (payload == null ||
            !payload.TryGetValue("inputPath", out var inputPath) ||
            !payload.TryGetValue("enabled", out var enabledText) ||
            !bool.TryParse(enabledText, out var enabled))
        {
            Debug.LogWarning("Action-bar toggle requested without a valid input path and enabled state.");
            return;
        }

        ActionGameManager.CommitRuntimeActionBarInput(inputPath, enabled);
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
        ActionGameManager.CommitRuntimeInputBindingOverride(action.name, _captureBindingIndex, inputPath);
        ClearCapture();
        RenderSurface();
    }

    private void ClearCapture()
    {
        _captureActionName = "";
        _captureBindingIndex = -1;
        _captureBindingLabel = "";
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
}

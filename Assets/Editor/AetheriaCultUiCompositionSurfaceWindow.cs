using System;
using System.Collections.Generic;
using System.Text;
using GameCult.Eve.Surface;
using GameCult.Eve.UnityUIToolkit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class AetheriaCultUiCompositionSurfaceWindow : EditorWindow
{
    private const string DefaultSurfaceId = "aetheria.menu.draft";
    private const string DefaultProviderId = "aetheria";
    private const string DefaultProviderKind = "game.menu.draft";

    private readonly List<MenuActionDraft> _actions = new List<MenuActionDraft>
    {
        new MenuActionDraft("Continue", "aetheria.menu.draft.continue"),
        new MenuActionDraft("New Game", "aetheria.menu.draft.new_game"),
        new MenuActionDraft("Settings", "aetheria.menu.draft.settings"),
        new MenuActionDraft("Quit", "aetheria.menu.draft.quit")
    };

    private TextField _surfaceId = null!;
    private TextField _providerId = null!;
    private TextField _providerKind = null!;
    private TextField _title = null!;
    private TextField _subtitle = null!;
    private TextField _note = null!;
    private Toggle _showStatusMetrics = null!;
    private Toggle _useButtonRow = null!;
    private VisualElement _actionList = null!;
    private VisualElement _previewHost = null!;
    private TextField _builderText = null!;
    private Label _commandTrace = null!;
    private long _version = 1;

    [MenuItem("Aetheria/CultUI/Live Composition Surface")]
    public static void Open()
    {
        var window = GetWindow<AetheriaCultUiCompositionSurfaceWindow>();
        window.titleContent = new GUIContent("CultUI Composition");
        window.minSize = new Vector2(860f, 520f);
        window.Show();
    }

    private void CreateGUI()
    {
        rootVisualElement.Clear();
        rootVisualElement.style.flexDirection = FlexDirection.Column;
        rootVisualElement.style.backgroundColor = new Color(0.07f, 0.08f, 0.1f, 1f);

        var toolbar = new Toolbar();
        var addButton = new ToolbarButton(AddAction) { text = "+ Action" };
        var copyButton = new ToolbarButton(CopyBuilder) { text = "Copy Builder" };
        var resetButton = new ToolbarButton(ResetDraft) { text = "Reset" };
        toolbar.Add(addButton);
        toolbar.Add(copyButton);
        toolbar.Add(resetButton);
        rootVisualElement.Add(toolbar);

        var body = new TwoPaneSplitView(0, 360, TwoPaneSplitViewOrientation.Horizontal);
        rootVisualElement.Add(body);
        body.style.flexGrow = 1f;

        var controls = new ScrollView();
        controls.style.paddingLeft = 10f;
        controls.style.paddingRight = 10f;
        controls.style.paddingTop = 10f;
        controls.style.paddingBottom = 10f;
        body.Add(controls);

        _surfaceId = TextInput("Surface Id", DefaultSurfaceId, controls);
        _providerId = TextInput("Provider Id", DefaultProviderId, controls);
        _providerKind = TextInput("Provider Kind", DefaultProviderKind, controls);
        _title = TextInput("Title", "AETHERIA", controls);
        _subtitle = TextInput("Subtitle", "TERMINUS", controls);
        _note = TextInput("Note", "Live CultUI draft lowered through the shared Eve UI Toolkit path.", controls);
        _showStatusMetrics = ToggleInput("Status Metrics", true, controls);
        _useButtonRow = ToggleInput("Button Row", false, controls);

        controls.Add(SectionLabel("Actions"));
        _actionList = new VisualElement();
        _actionList.style.flexDirection = FlexDirection.Column;
        controls.Add(_actionList);

        _commandTrace = new Label("No commands invoked.");
        _commandTrace.style.whiteSpace = WhiteSpace.Normal;
        _commandTrace.style.marginTop = 10f;
        controls.Add(_commandTrace);

        var output = new VisualElement();
        output.style.flexDirection = FlexDirection.Column;
        output.style.flexGrow = 1f;
        body.Add(output);

        _previewHost = new VisualElement();
        _previewHost.style.flexGrow = 1f;
        _previewHost.style.paddingLeft = 16f;
        _previewHost.style.paddingRight = 16f;
        _previewHost.style.paddingTop = 16f;
        _previewHost.style.paddingBottom = 16f;
        _previewHost.style.backgroundColor = new Color(0.02f, 0.025f, 0.035f, 1f);
        output.Add(_previewHost);

        _builderText = new TextField("Provider Builder");
        _builderText.multiline = true;
        _builderText.isReadOnly = true;
        _builderText.style.height = 170f;
        _builderText.style.marginLeft = 8f;
        _builderText.style.marginRight = 8f;
        _builderText.style.marginBottom = 8f;
        output.Add(_builderText);

        RebuildActionList();
        RebuildSurface();
    }

    private TextField TextInput(string label, string value, VisualElement parent)
    {
        var field = new TextField(label) { value = value };
        field.RegisterValueChangedCallback(_ => RebuildSurface());
        parent.Add(field);
        return field;
    }

    private Toggle ToggleInput(string label, bool value, VisualElement parent)
    {
        var toggle = new Toggle(label) { value = value };
        toggle.RegisterValueChangedCallback(_ => RebuildSurface());
        parent.Add(toggle);
        return toggle;
    }

    private static Label SectionLabel(string text)
    {
        var label = new Label(text);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginTop = 12f;
        label.style.marginBottom = 4f;
        return label;
    }

    private void RebuildActionList()
    {
        _actionList.Clear();
        for (var index = 0; index < _actions.Count; index++)
        {
            var action = _actions[index];
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4f;

            var label = new TextField { value = action.Label };
            label.style.flexGrow = 1f;
            label.RegisterValueChangedCallback(evt =>
            {
                action.Label = evt.newValue ?? "";
                RebuildSurface();
            });
            row.Add(label);

            var command = new TextField { value = action.Command };
            command.style.flexGrow = 1f;
            command.RegisterValueChangedCallback(evt =>
            {
                action.Command = evt.newValue ?? "";
                RebuildSurface();
            });
            row.Add(command);

            var remove = new Button(() =>
            {
                _actions.Remove(action);
                RebuildActionList();
                RebuildSurface();
            })
            {
                text = "X"
            };
            remove.style.width = 28f;
            row.Add(remove);
            _actionList.Add(row);
        }
    }

    private void AddAction()
    {
        var count = _actions.Count + 1;
        var surfaceId = Clean(_surfaceId == null ? "" : _surfaceId.value, DefaultSurfaceId);
        _actions.Add(new MenuActionDraft($"Action {count}", $"{surfaceId}.action_{count}"));
        RebuildActionList();
        RebuildSurface();
    }

    private void ResetDraft()
    {
        _actions.Clear();
        _actions.Add(new MenuActionDraft("Continue", "aetheria.menu.draft.continue"));
        _actions.Add(new MenuActionDraft("New Game", "aetheria.menu.draft.new_game"));
        _actions.Add(new MenuActionDraft("Settings", "aetheria.menu.draft.settings"));
        _actions.Add(new MenuActionDraft("Quit", "aetheria.menu.draft.quit"));

        _surfaceId.value = DefaultSurfaceId;
        _providerId.value = DefaultProviderId;
        _providerKind.value = DefaultProviderKind;
        _title.value = "AETHERIA";
        _subtitle.value = "TERMINUS";
        _note.value = "Live CultUI draft lowered through the shared Eve UI Toolkit path.";
        _showStatusMetrics.value = true;
        _useButtonRow.value = false;
        RebuildActionList();
        RebuildSurface();
    }

    private void RebuildSurface()
    {
        if (_previewHost == null)
            return;

        var document = BuildSurface();
        _previewHost.Clear();

        var lowerer = new EveUiToolkitSurfaceLowerer();
        var lowered = lowerer.Lower(document, request =>
        {
            _commandTrace.text = $"Command preview: {request.Operation.OperationId}";
        });
        ApplyPreviewFrame(lowered);
        _previewHost.Add(lowered);
        _builderText.value = BuildProviderSnippet(document);
    }

    private EveSurfaceDocument BuildSurface()
    {
        var surfaceId = Clean(_surfaceId == null ? "" : _surfaceId.value, DefaultSurfaceId);
        var builder = EveSurface
            .Create(surfaceId)
            .Provider(
                Clean(_providerId == null ? "" : _providerId.value, DefaultProviderId),
                Clean(_providerKind == null ? "" : _providerKind.value, DefaultProviderKind))
            .Version(++_version)
            .UpdatedAtUtc(DateTime.UtcNow.ToString("O"));

        foreach (var token in StyleTokens())
            builder.Style(token.Name, token.Value);

        var title = _title?.value ?? "";
        var subtitle = _subtitle?.value ?? "";
        if (string.IsNullOrWhiteSpace(subtitle))
            builder.Title(title);
        else
            builder.TitleSubtitle(title, subtitle);

        if (_note != null && !string.IsNullOrWhiteSpace(_note.value))
            builder.Text(_note.value, $"{surfaceId}.note");

        if (_showStatusMetrics?.value == true)
        {
            builder.Form(
                $"{surfaceId}.status",
                form => form
                    .Metric("Surface", surfaceId)
                    .Metric("Actions", _actions.Count.ToString())
                    .Metric("Version", _version.ToString()));
        }

        if (_useButtonRow?.value == true)
        {
            builder.ButtonRow($"{surfaceId}.actions", group => AddButtons(group));
        }
        else
        {
            builder.ButtonColumn($"{surfaceId}.actions", group => AddButtons(group));
        }

        return builder.Build();
    }

    private void AddButtons(EveSurfaceGroupBuilder group)
    {
        foreach (var action in _actions)
        {
            if (string.IsNullOrWhiteSpace(action.Label) || string.IsNullOrWhiteSpace(action.Command))
                continue;

            group.Button(action.Label, action.Command);
        }
    }

    private static IReadOnlyList<EveStyleToken> StyleTokens()
    {
        return new[]
        {
            new EveStyleToken("font.title.family", "Montserrat"),
            new EveStyleToken("font.title.style", "Thin"),
            new EveStyleToken("font.body.family", "Ubuntu"),
            new EveStyleToken("font.body.style", "Regular")
        };
    }

    private static void ApplyPreviewFrame(VisualElement root)
    {
        root.style.maxWidth = 520f;
        root.style.paddingLeft = 16f;
        root.style.paddingRight = 16f;
        root.style.paddingTop = 14f;
        root.style.paddingBottom = 14f;
        root.style.backgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.96f);
        root.style.borderTopLeftRadius = 8f;
        root.style.borderTopRightRadius = 8f;
        root.style.borderBottomLeftRadius = 8f;
        root.style.borderBottomRightRadius = 8f;
        root.style.borderLeftWidth = 1f;
        root.style.borderRightWidth = 1f;
        root.style.borderTopWidth = 1f;
        root.style.borderBottomWidth = 1f;
        root.style.borderLeftColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        root.style.borderRightColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        root.style.borderTopColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
        root.style.borderBottomColor = new Color(0.3f, 0.47f, 0.71f, 0.8f);
    }

    private void CopyBuilder()
    {
        EditorGUIUtility.systemCopyBuffer = _builderText?.value ?? "";
    }

    private string BuildProviderSnippet(EveSurfaceDocument document)
    {
        var surfaceId = CSharpString(document.Surface.Id);
        var builder = new StringBuilder();
        builder.AppendLine("var surface = EveSurface");
        builder.AppendLine($"    .Create({surfaceId})");
        builder.AppendLine($"    .Provider({CSharpString(document.ProviderId)}, {CSharpString(document.ProviderKind)})");
        builder.AppendLine("    .Version(version)");
        builder.AppendLine("    .UpdatedAtUtc(updatedAtUtc)");
        foreach (var token in document.Surface.Styles)
            builder.AppendLine($"    .Style({CSharpString(token.Name)}, {CSharpString(token.Value)})");

        if (string.IsNullOrWhiteSpace(_subtitle?.value))
            builder.AppendLine($"    .Title({CSharpString(_title?.value ?? "")})");
        else
            builder.AppendLine($"    .TitleSubtitle({CSharpString(_title?.value ?? "")}, {CSharpString(_subtitle?.value ?? "")})");

        var note = _note == null ? "" : _note.value;
        if (!string.IsNullOrWhiteSpace(note))
            builder.AppendLine($"    .Text({CSharpString(note)}, {CSharpString(document.Surface.Id + ".note")})");

        if (_showStatusMetrics?.value == true)
        {
            builder.AppendLine($"    .Form({CSharpString(document.Surface.Id + ".status")}, form => form");
            builder.AppendLine($"        .Metric(@\"Surface\", {CSharpString(document.Surface.Id)})");
            builder.AppendLine($"        .Metric(@\"Actions\", {_actions.Count}.ToString())");
            builder.AppendLine("        .Metric(@\"Version\", version.ToString()))");
        }

        builder.AppendLine($"    .Button{(_useButtonRow?.value == true ? "Row" : "Column")}({CSharpString(document.Surface.Id + ".actions")}, actions =>");
        builder.AppendLine("    {");
        foreach (var action in _actions)
        {
            if (!string.IsNullOrWhiteSpace(action.Label) && !string.IsNullOrWhiteSpace(action.Command))
                builder.AppendLine($"        actions.Button({CSharpString(action.Label)}, {CSharpString(action.Command)});");
        }

        builder.AppendLine("    })");
        builder.AppendLine("    .Build();");
        return builder.ToString();
    }

    private static string Clean(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string CSharpString(string value)
    {
        return "@\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    }

    private sealed class MenuActionDraft
    {
        public MenuActionDraft(string label, string command)
        {
            Label = label;
            Command = command;
        }

        public string Label;
        public string Command;
    }
}

using System;
using System.Collections.Generic;
using GameCult.Eve.Surface;
using UnityEngine;
using UnityEngine.UIElements;

#nullable enable

namespace GameCult.Eve.UnityUIToolkit
{
    public sealed class EveUiToolkitSurfaceLowerer
    {
        private readonly EveUiToolkitSurfaceOptions _options;

        public EveUiToolkitSurfaceLowerer(EveUiToolkitSurfaceOptions? options = null)
        {
            _options = options ?? EveUiToolkitSurfaceOptions.Default;
        }

        public VisualElement Lower(EveSurfaceDocument document, Action<EveSurfaceCommandRequest>? commandSink = null)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            var root = LowerComponent(document.Surface.Root, document, commandSink);
            root.name = string.IsNullOrWhiteSpace(root.name) ? document.Surface.Id : root.name;
            root.AddToClassList("eve-surface-root");
            ApplyStyleTokens(root, document.Surface.Styles);
            return root;
        }

        private VisualElement LowerComponent(
            EveSurfaceComponent component,
            EveSurfaceDocument document,
            Action<EveSurfaceCommandRequest>? commandSink)
        {
            var element = CreateElement(component, document, commandSink);
            element.name = SafeName(component.Id);
            element.AddToClassList("eve-component");
            element.AddToClassList($"eve-kind-{SafeClass(component.Kind)}");
            element.userData = component;

            foreach (var child in component.Children)
                element.Add(LowerComponent(child, document, commandSink));

            return element;
        }

        private VisualElement CreateElement(
            EveSurfaceComponent component,
            EveSurfaceDocument document,
            Action<EveSurfaceCommandRequest>? commandSink)
        {
            switch (NormalizeKind(component.Kind))
            {
                case "surface":
                {
                    var element = new VisualElement();
                    element.style.flexGrow = 1;
                    element.style.flexDirection = FlexDirection.Column;
                    return element;
                }
                case "grid":
                {
                    var element = new VisualElement();
                    element.style.flexDirection = FlexDirection.Row;
                    element.style.flexWrap = Wrap.Wrap;
                    element.style.alignItems = Align.Stretch;
                    return element;
                }
                case "card":
                {
                    var card = new VisualElement();
                    card.AddToClassList("eve-card");
                    card.style.flexDirection = FlexDirection.Column;
                    var title = component.GetProp("title");
                    if (!string.IsNullOrWhiteSpace(title))
                        card.Add(TitleLabel(title));
                    return card;
                }
                case "metric":
                {
                    var metric = new VisualElement();
                    metric.AddToClassList("eve-metric");
                    metric.style.flexDirection = FlexDirection.Column;
                    metric.Add(MutedLabel(component.GetProp("label")));
                    metric.Add(ValueLabel(component.GetProp("value")));
                    return metric;
                }
                case "inspector.kv":
                {
                    var inspector = new VisualElement();
                    inspector.AddToClassList("eve-inspector");
                    inspector.style.flexDirection = FlexDirection.Column;
                    return inspector;
                }
                case "row":
                {
                    var row = new VisualElement();
                    row.AddToClassList("eve-row");
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.flexWrap = Wrap.Wrap;
                    foreach (var prop in component.Props)
                        row.Add(FieldLabel(prop.Key, prop.Value));
                    return row;
                }
                case "text":
                {
                    return BodyLabel(component.GetProp("text", component.GetProp("value", component.GetProp("title"))));
                }
                case "control.button":
                {
                    var label = component.GetProp("label", component.GetProp("title", "Invoke"));
                    var command = component.GetProp("command", component.GetProp("action", "invoke"));
                    return new Button(() => EmitCommand(document, component, command, commandSink)) { text = label };
                }
                case "control.text":
                {
                    var label = component.GetProp("label", component.GetProp("title", "Value"));
                    var command = component.GetProp("command", component.GetProp("action", "set"));
                    var value = component.GetProp("value");
                    var field = new TextField(label);
                    field.SetValueWithoutNotify(value);
                    field.RegisterValueChangedCallback(evt =>
                    {
                        if (string.Equals(evt.newValue, value, StringComparison.Ordinal))
                            return;

                        var payload = new Dictionary<string, string>(component.Props, StringComparer.Ordinal)
                        {
                            ["value"] = evt.newValue ?? ""
                        };
                        EmitCommand(document, component, command, payload, commandSink);
                    });
                    return field;
                }
                default:
                {
                    var element = new VisualElement();
                    element.AddToClassList("eve-unknown");
                    var title = component.GetProp("title", component.GetProp("label"));
                    if (!string.IsNullOrWhiteSpace(title))
                        element.Add(BodyLabel(title));
                    return element;
                }
            }
        }

        private static void EmitCommand(
            EveSurfaceDocument document,
            EveSurfaceComponent component,
            string command,
            Action<EveSurfaceCommandRequest>? commandSink)
        {
            EmitCommand(
                document,
                component,
                command,
                new Dictionary<string, string>(component.Props, StringComparer.Ordinal),
                commandSink);
        }

        private static void EmitCommand(
            EveSurfaceDocument document,
            EveSurfaceComponent component,
            string command,
            IReadOnlyDictionary<string, string> payload,
            Action<EveSurfaceCommandRequest>? commandSink)
        {
            if (commandSink == null || string.IsNullOrWhiteSpace(command))
                return;

            commandSink(new EveSurfaceCommandRequest(
                document.ProviderId,
                document.Surface.Id,
                command,
                payload,
                DateTimeOffset.UtcNow,
                "unity-uitoolkit"));
        }

        private static Label TitleLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("eve-title");
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static Label BodyLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("eve-text");
            return label;
        }

        private static Label MutedLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("eve-muted");
            return label;
        }

        private static Label ValueLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("eve-value");
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static VisualElement FieldLabel(string key, string value)
        {
            var field = new VisualElement();
            field.AddToClassList("eve-field");
            field.style.flexDirection = FlexDirection.Row;
            field.Add(MutedLabel(key));
            field.Add(BodyLabel(value));
            return field;
        }

        private static void ApplyStyleTokens(VisualElement root, IReadOnlyList<EveStyleToken> tokens)
        {
            foreach (var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token.Name) || string.IsNullOrWhiteSpace(token.Value))
                    continue;

                root.AddToClassList($"eve-style-token-{SafeClass(token.Name)}");
            }
        }

        private static string NormalizeKind(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
                return "";

            if (kind == "panel.dialogue")
                return "panel";
            if (kind.StartsWith("text.", StringComparison.Ordinal))
                return "text";
            if (kind.StartsWith("control.button.", StringComparison.Ordinal))
                return "control.button";
            if (kind.StartsWith("control.text.", StringComparison.Ordinal))
                return "control.text";

            return kind;
        }

        private static string SafeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "eve.component" : value;
        }

        private static string SafeClass(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            var characters = value.ToCharArray();
            for (var index = 0; index < characters.Length; index++)
            {
                if (!char.IsLetterOrDigit(characters[index]))
                    characters[index] = '-';
            }

            return new string(characters).Trim('-').ToLowerInvariant();
        }
    }
}

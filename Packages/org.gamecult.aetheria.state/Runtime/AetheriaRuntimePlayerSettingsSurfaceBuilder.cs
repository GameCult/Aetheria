using System;
using System.Collections.Generic;
using System.Linq;
using GameCult.Mesh;

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeSurfaceStateRefs
    {
        public const string Source = "stateRef";
        public const string Value = "valueRef";
        public const string Label = "labelRef";
        public const string Format = "stateFormat";

        public static (string Key, string Value) SourceRef(string reference)
        {
            return (Source, reference ?? "");
        }

        public static (string Key, string Value) ValueRef(string reference)
        {
            return (Value, reference ?? "");
        }

        public static (string Key, string Value) LabelRef(string reference)
        {
            return (Label, reference ?? "");
        }

        public static (string Key, string Value) FormatRef(string format)
        {
            return (Format, format ?? "");
        }
    }

    public static class AetheriaRuntimeSurfaceStateBindings
    {
        public const string PropPrefix = "cultmesh.statePointer.";
        public const string PointerIdSuffix = ".pointerId";
        public const string SourceIdSuffix = ".sourceId";
        public const string SchemaIdSuffix = ".schemaId";
        public const string RouteKindSuffix = ".routeKind";
        public const string RouteDescriptionSuffix = ".routeDescription";

        public static CultMeshStateBindingDescriptor ForDaemonStateRef(
            string targetProp,
            string stateRef,
            string schemaId = AetheriaRuntimeDaemonSchemas.Frame)
        {
            return new CultMeshStateBindingDescriptor(
                targetProp,
                ToPointerId(stateRef),
                stateRef,
                schemaId,
                new CultMeshRouteHint(
                    CultMeshLocalityKind.SharedMemory,
                    "daemon-published CultCache state"));
        }

        public static IReadOnlyList<CultMeshStateBindingDescriptor> FromProps(
            IReadOnlyDictionary<string, string> props)
        {
            if (props == null || props.Count == 0)
                return Array.Empty<CultMeshStateBindingDescriptor>();

            var bindings = new List<CultMeshStateBindingDescriptor>();
            AddPropBinding(bindings, "value", Get(props, AetheriaRuntimeSurfaceStateRefs.Source));
            foreach (var prop in props)
            {
                if (string.IsNullOrWhiteSpace(prop.Value) ||
                    string.Equals(prop.Key, AetheriaRuntimeSurfaceStateRefs.Source, StringComparison.Ordinal) ||
                    !prop.Key.EndsWith("Ref", StringComparison.Ordinal))
                {
                    continue;
                }

                AddPropBinding(bindings, prop.Key.Substring(0, prop.Key.Length - "Ref".Length), prop.Value);
            }

            return bindings;
        }

        public static void AddPointerProps(
            IDictionary<string, string> props,
            IReadOnlyList<CultMeshStateBindingDescriptor> bindings)
        {
            if (props == null || bindings == null)
                return;

            foreach (var binding in bindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.PointerId))
                    continue;

                var record = CultMesh.StateBindingRecord(binding);
                var prefix = PropPrefix + record.TargetProp;
                props[prefix + PointerIdSuffix] = record.PointerId;
                props[prefix + SourceIdSuffix] = record.SourceId;
                props[prefix + SchemaIdSuffix] = record.SchemaId;
                props[prefix + RouteKindSuffix] = record.RouteKind;
                props[prefix + RouteDescriptionSuffix] = record.RouteDescription;
            }
        }

        private static void AddPropBinding(
            List<CultMeshStateBindingDescriptor> bindings,
            string targetProp,
            string stateRef)
        {
            if (string.IsNullOrWhiteSpace(stateRef))
                return;

            var schemaId = stateRef.StartsWith(AetheriaRuntimeDaemonItemStatQueries.StateRefPrefix + "/", StringComparison.Ordinal)
                ? AetheriaRuntimeDaemonSchemas.CurrentEntity
                : AetheriaRuntimeDaemonSchemas.Frame;
            bindings.Add(ForDaemonStateRef(targetProp, stateRef, schemaId));
        }

        private static string Get(IReadOnlyDictionary<string, string> props, string key)
        {
            return props.TryGetValue(key, out var value) ? value : "";
        }

        private static string ToPointerId(string stateRef)
        {
            if (string.IsNullOrWhiteSpace(stateRef))
                return "";

            var chars = stateRef
                .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '.')
                .ToArray();
            var pointerId = new string(chars).Trim('.');
            while (pointerId.Contains("..", StringComparison.Ordinal))
                pointerId = pointerId.Replace("..", ".", StringComparison.Ordinal);
            return string.IsNullOrWhiteSpace(pointerId) ? "aetheria.state.unknown" : pointerId;
        }
    }

    public static class AetheriaRuntimePlayerSettingsSurfaceBuilder
    {
        public static AetheriaRuntimeSurfaceDocument Build(
            AetheriaRuntimePlayerSettingsDocument settings,
            string updatedAtUtc,
            long version = 1)
        {
            return Build(
                settings?.PlayerName ?? "",
                settings?.TutorialPassed ?? false,
                activeRunKey: "",
                settings?.TemperatureUnit ?? "",
                Math.Max(0, settings?.SignificantDigits ?? 0),
                settings?.NebulaQuality ?? "",
                settings?.ShowAsteroidsInMinimap ?? false,
                updatedAtUtc,
                version);
        }

        public static AetheriaRuntimeSurfaceDocument Build(
            string playerName,
            bool tutorialPassed,
            string activeRunKey,
            string temperatureUnit,
            int significantDigits,
            string nebulaQuality,
            bool showAsteroidsInMinimap,
            string updatedAtUtc,
            long version = 1)
        {
            playerName ??= "";
            activeRunKey ??= "";
            temperatureUnit ??= "";
            nebulaQuality ??= "";
            updatedAtUtc ??= "";
            significantDigits = Math.Max(0, significantDigits);
            return new AetheriaRuntimeSurfaceDocument(
                providerId: "aetheria",
                providerKind: "game.runtime",
                title: "Aetheria Player Settings",
                version: version,
                updatedAtUtc: updatedAtUtc,
                surface: new AetheriaRuntimeSurfaceTree(
                    AetheriaRuntimePlayerSettingsCommands.SurfaceId,
                    Node(
                        "aetheria.playerSettings.root",
                        "surface",
                        Array.Empty<(string Key, string Value)>(),
                        Node(
                            "aetheria.playerSettings.summary",
                            "card",
                            new[] { ("title", "Player Settings") },
                            TextInput(
                                "playerSettings.summary.playerName",
                                "Name",
                                playerName,
                                AetheriaRuntimePlayerSettingsCommands.SetPlayerName),
                            Row(
                                "playerSettings.summary.values",
                                ("tutorialPassed", tutorialPassed ? "Yes" : "No"),
                                ("activeRun", activeRunKey)),
                            Text(
                                "playerSettings.summary.note",
                                "Input remapping lowers through the runtime Eve input screen and sends typed input-setting requests.")),
                        Node(
                            "aetheria.playerSettings.gameplay",
                            "card",
                            new[] { ("title", "Gameplay") },
                            Metric("playerSettings.gameplay.temperatureUnit", "Temperature Unit", temperatureUnit),
                            ButtonRow(
                                "playerSettings.gameplay.temperatureUnit.buttons",
                                Button(
                                    "playerSettings.gameplay.temperatureUnit.cycle",
                                    "Cycle Temperature Unit",
                                    AetheriaRuntimePlayerSettingsCommands.CycleTemperatureUnit)),
                            Metric(
                                "playerSettings.gameplay.significantDigits",
                                "Significant Digits",
                                significantDigits.ToString()),
                            ButtonRow(
                                "playerSettings.gameplay.significantDigits.buttons",
                                Button(
                                    "playerSettings.gameplay.significantDigits.decrement",
                                    "Digits -",
                                    AetheriaRuntimePlayerSettingsCommands.DecrementSignificantDigits),
                                Button(
                                    "playerSettings.gameplay.significantDigits.increment",
                                    "Digits +",
                                    AetheriaRuntimePlayerSettingsCommands.IncrementSignificantDigits))),
                        Node(
                            "aetheria.playerSettings.graphics",
                            "card",
                            new[] { ("title", "Graphics") },
                            Metric("playerSettings.graphics.nebulaQuality", "Nebula Quality", nebulaQuality),
                            ButtonRow(
                                "playerSettings.graphics.nebulaQuality.buttons",
                                Button(
                                    "playerSettings.graphics.nebulaQuality.cycle",
                                    "Cycle Nebula Quality",
                                    AetheriaRuntimePlayerSettingsCommands.CycleNebulaQuality)),
                            Metric(
                                "playerSettings.graphics.showAsteroids",
                                "Show Asteroids In Minimap",
                                showAsteroidsInMinimap ? "Enabled" : "Disabled"),
                            ButtonRow(
                                "playerSettings.graphics.showAsteroids.buttons",
                                Button(
                                    "playerSettings.graphics.showAsteroids.toggle",
                                    showAsteroidsInMinimap ? "Disable Minimap Asteroids" : "Enable Minimap Asteroids",
                                    AetheriaRuntimePlayerSettingsCommands.ToggleShowAsteroidsInMinimap)))),
                    Array.Empty<AetheriaRuntimeSurfaceStyleToken>()),
                commands: new[]
                {
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimePlayerSettingsCommands.Refresh,
                        "Refresh",
                        "cultmesh"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimePlayerSettingsCommands.SetPlayerName,
                        "Set Player Name",
                        "cultmesh"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimePlayerSettingsCommands.CycleTemperatureUnit,
                        "Cycle Temperature Unit",
                        "cultmesh"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimePlayerSettingsCommands.DecrementSignificantDigits,
                        "Digits -",
                        "cultmesh"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimePlayerSettingsCommands.IncrementSignificantDigits,
                        "Digits +",
                        "cultmesh"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimePlayerSettingsCommands.CycleNebulaQuality,
                        "Cycle Nebula Quality",
                        "cultmesh"),
                    new AetheriaRuntimeSurfaceCommandTemplate(
                        AetheriaRuntimePlayerSettingsCommands.ToggleShowAsteroidsInMinimap,
                        "Toggle Minimap Asteroids",
                        "cultmesh")
                });
        }

        private static AetheriaRuntimeSurfaceComponent Metric(string id, string label, string value)
        {
            return Node(id, "metric", new[] { ("label", label), ("value", value) });
        }

        private static AetheriaRuntimeSurfaceComponent Text(string id, string value)
        {
            return Node(id, "text", new[] { ("value", value) });
        }

        private static AetheriaRuntimeSurfaceComponent Button(string id, string label, string command)
        {
            return Node(id, "control.button", new[] { ("label", label), ("command", command) });
        }

        private static AetheriaRuntimeSurfaceComponent TextInput(string id, string label, string value, string command)
        {
            return Node(id, "control.text", new[] { ("label", label), ("value", value), ("command", command) });
        }

        private static AetheriaRuntimeSurfaceComponent ButtonRow(
            string id,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return Node(id, "row", Array.Empty<(string Key, string Value)>(), children);
        }

        private static AetheriaRuntimeSurfaceComponent Row(
            string id,
            params (string Key, string Value)[] props)
        {
            return Node(id, "row", props);
        }

        private static AetheriaRuntimeSurfaceComponent Node(
            string id,
            string kind,
            IEnumerable<(string Key, string Value)> props,
            params AetheriaRuntimeSurfaceComponent[] children)
        {
            return new AetheriaRuntimeSurfaceComponent(
                id,
                kind,
                props.ToDictionary(prop => prop.Key, prop => prop.Value, StringComparer.Ordinal),
                children ?? Array.Empty<AetheriaRuntimeSurfaceComponent>());
        }
    }
}

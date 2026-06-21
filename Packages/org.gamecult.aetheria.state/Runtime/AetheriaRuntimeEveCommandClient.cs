using System;
using GameCult.Eve.Surface;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public static class AetheriaRuntimeEveCommands
    {
        public static bool TryCreateKnownSurfaceCommand(
            string stateFilePath,
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeEveCommandEnvelope? envelope)
        {
            return AetheriaRuntimeEveCommandClient.TryCreateKnownSurfaceCommand(request, out envelope);
        }

        public static AetheriaRuntimeEveCommandEnvelope SubmitPlayerSettingsCommand(
            string stateFilePath,
            AetheriaRuntimeEveCommandKind command,
            AetheriaRuntimePlayerSettingsCommandBody body,
            string clientId)
        {
            return AetheriaRuntimeEveCommandClient.CreatePlayerSettingsCommand(command, body, clientId);
        }

        public static bool TrySendPlayerSettingsCommand(
            string stateFilePath,
            EveSurfaceCommandRequest request,
            string clientId,
            out AetheriaRuntimeEveCommandEnvelope? envelope,
            out string error)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return TrySend(
                stateFilePath,
                AetheriaRuntimeEveCommandClient.CreatePlayerSettingsCommand(request, clientId),
                clientId,
                out envelope,
                out error);
        }

        public static AetheriaRuntimeEveCommandEnvelope SubmitPlayerSettingsCommand(
            string stateFilePath,
            EveSurfaceCommandRequest request)
        {
            return AetheriaRuntimeEveCommandClient.CreatePlayerSettingsCommand(request, request?.ClientId ?? "");
        }

        public static AetheriaRuntimeEveCommandEnvelope SubmitPlayerSettingsCommand(
            string stateFilePath,
            EveSurfaceCommandRequest request,
            string clientId)
        {
            return AetheriaRuntimeEveCommandClient.CreatePlayerSettingsCommand(request, clientId);
        }

        public static AetheriaRuntimeEveCommandEnvelope SubmitInputSettingsCommand(
            string stateFilePath,
            AetheriaRuntimeEveCommandKind command,
            AetheriaRuntimeInputSettingsCommandBody body,
            string clientId)
        {
            return AetheriaRuntimeEveCommandClient.CreateInputSettingsCommand(command, body, clientId);
        }

        public static bool TrySendInputSettingsCommand(
            string stateFilePath,
            AetheriaRuntimeEveCommandKind command,
            AetheriaRuntimeInputSettingsCommandBody body,
            string clientId,
            out AetheriaRuntimeEveCommandEnvelope? envelope,
            out string error)
        {
            return TrySend(
                stateFilePath,
                AetheriaRuntimeEveCommandClient.CreateInputSettingsCommand(command, body, clientId),
                clientId,
                out envelope,
                out error);
        }

        public static AetheriaRuntimeEveCommandEnvelope SubmitCatalogCommand(
            string stateFilePath,
            AetheriaRuntimeEveCommandKind command,
            string clientId)
        {
            return AetheriaRuntimeEveCommandClient.CreateCatalogCommand(command, clientId);
        }

        public static AetheriaRuntimeEveCommandEnvelope SubmitOperationsCommand(
            string stateFilePath,
            AetheriaRuntimeEveCommandKind command,
            string clientId)
        {
            return AetheriaRuntimeEveCommandClient.CreateOperationsCommand(command, clientId);
        }

        public static AetheriaRuntimeEveCommandEnvelope SubmitVerseHostCommand(
            string stateFilePath,
            AetheriaRuntimeEveCommandKind command,
            string clientId)
        {
            return AetheriaRuntimeEveCommandClient.CreateVerseHostCommand(command, clientId);
        }

        public static bool TrySendVerseHostCommand(
            string stateFilePath,
            AetheriaRuntimeEveCommandKind command,
            string clientId,
            out AetheriaRuntimeEveCommandEnvelope? envelope,
            out string error)
        {
            return TrySend(
                stateFilePath,
                AetheriaRuntimeEveCommandClient.CreateVerseHostCommand(command, clientId),
                clientId,
                out envelope,
                out error);
        }

        public static AetheriaRuntimeEveCommandEnvelope SubmitLoadoutTemplateCommand(
            string stateFilePath,
            AetheriaRuntimeLoadoutTemplateCommit loadoutTemplate,
            string clientId)
        {
            return AetheriaRuntimeEveCommandClient.CreateLoadoutTemplateCommand(loadoutTemplate, clientId);
        }

        public static bool TrySendLoadoutTemplateCommand(
            string stateFilePath,
            AetheriaRuntimeLoadoutTemplateCommit loadoutTemplate,
            string clientId,
            out AetheriaRuntimeEveCommandEnvelope? envelope,
            out string error)
        {
            return TrySend(
                stateFilePath,
                AetheriaRuntimeEveCommandClient.CreateLoadoutTemplateCommand(loadoutTemplate, clientId),
                clientId,
                out envelope,
                out error);
        }

        public static bool TrySendKnownSurfaceCommand(
            string stateFilePath,
            EveSurfaceCommandRequest request,
            string clientId,
            out AetheriaRuntimeEveCommandEnvelope? envelope,
            out string error)
        {
            if (!AetheriaRuntimeEveCommandClient.TryCreateKnownSurfaceCommand(request, out var commandEnvelope))
            {
                envelope = null;
                error = $"Unknown Aetheria Eve surface command: {request?.ProviderId}/{request?.SurfaceId}/{request?.Command}";
                return false;
            }

            return TrySend(
                stateFilePath,
                commandEnvelope!,
                clientId,
                out envelope,
                out error);
        }

        private static bool TrySend(
            string stateFilePath,
            AetheriaRuntimeEveCommandEnvelope commandEnvelope,
            string clientId,
            out AetheriaRuntimeEveCommandEnvelope? envelope,
            out string error)
        {
            return AetheriaRuntimeCommandSubmitter.TrySubmitEveCommand(
                stateFilePath,
                AetheriaRuntimeEveCommandClient.ToDocument(commandEnvelope),
                string.IsNullOrWhiteSpace(clientId) ? "aetheria-eve-client" : clientId,
                out envelope,
                out error);
        }
    }

    public static class AetheriaRuntimeEveCommandClient
    {
        public const string CommandSchema = AetheriaRuntimeEveCommandDocument.SchemaId;

        public static bool TryCreateKnownSurfaceCommand(
            EveSurfaceCommandRequest request,
            out AetheriaRuntimeEveCommandEnvelope? envelope)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var command = request.Command ?? "";
            var clientId = request.ClientId ?? "";
            switch (request.SurfaceId ?? "")
            {
                case AetheriaRuntimePlayerSettingsCommands.SurfaceId
                    when AetheriaRuntimePlayerSettingsCommands.IsKnown(command):
                    envelope = CreatePlayerSettingsCommand(CommandKindForSurface(request), request, clientId);
                    return true;
                case AetheriaRuntimeInputSettingsCommands.SurfaceId
                    when AetheriaRuntimeInputSettingsCommands.IsKnown(command):
                    envelope = CreateInputSettingsCommand(CommandKindForSurface(request), request, clientId);
                    return true;
                case AetheriaRuntimeCatalogCommands.SurfaceId
                    when AetheriaRuntimeCatalogCommands.IsKnown(command):
                    envelope = CreateCatalogCommand(CommandKindForSurface(request), clientId);
                    return true;
                case AetheriaRuntimeOperationsCommands.SurfaceId
                    when AetheriaRuntimeOperationsCommands.IsKnown(command):
                    envelope = CreateOperationsCommand(CommandKindForSurface(request), clientId);
                    return true;
                case AetheriaRuntimeVerseHostCommands.SurfaceId
                    when AetheriaRuntimeVerseHostCommands.IsKnown(command):
                    envelope = CreateVerseHostCommand(CommandKindForSurface(request), clientId);
                    return true;
            }

            envelope = null;
            return false;
        }

        public static AetheriaRuntimeEveCommandEnvelope CreatePlayerSettingsCommand(
            AetheriaRuntimeEveCommandKind command,
            AetheriaRuntimePlayerSettingsCommandBody body,
            string clientId)
        {
            return CreateTypedCommand(
                AetheriaRuntimePlayerSettingsCommands.SurfaceId,
                command,
                clientId,
                playerSettings: body ?? new AetheriaRuntimePlayerSettingsCommandBody(),
                inputSettings: new AetheriaRuntimeInputSettingsCommandBody(),
                loadoutTemplate: null);
        }

        public static AetheriaRuntimeEveCommandEnvelope CreatePlayerSettingsCommand(
            EveSurfaceCommandRequest request)
        {
            return CreatePlayerSettingsCommand(request, request?.ClientId ?? "");
        }

        public static AetheriaRuntimeEveCommandEnvelope CreatePlayerSettingsCommand(
            EveSurfaceCommandRequest request,
            string clientId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return CreatePlayerSettingsCommand(CommandKindForSurface(request), request, clientId);
        }

        private static AetheriaRuntimeEveCommandEnvelope CreatePlayerSettingsCommand(
            AetheriaRuntimeEveCommandKind command,
            EveSurfaceCommandRequest request,
            string clientId)
        {
            return CreatePlayerSettingsCommand(
                command,
                ReadPlayerSettingsBody(request),
                string.IsNullOrWhiteSpace(clientId) ? request.ClientId ?? "" : clientId);
        }

        public static AetheriaRuntimeEveCommandEnvelope CreateInputSettingsCommand(
            AetheriaRuntimeEveCommandKind command,
            AetheriaRuntimeInputSettingsCommandBody body,
            string clientId)
        {
            return CreateTypedCommand(
                AetheriaRuntimeInputSettingsCommands.SurfaceId,
                command,
                clientId,
                playerSettings: new AetheriaRuntimePlayerSettingsCommandBody(),
                inputSettings: body ?? new AetheriaRuntimeInputSettingsCommandBody(),
                loadoutTemplate: null);
        }

        public static AetheriaRuntimeEveCommandEnvelope CreateInputSettingsCommand(
            EveSurfaceCommandRequest request,
            string clientId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return CreateInputSettingsCommand(CommandKindForSurface(request), request, clientId);
        }

        private static AetheriaRuntimeEveCommandEnvelope CreateInputSettingsCommand(
            AetheriaRuntimeEveCommandKind command,
            EveSurfaceCommandRequest request,
            string clientId)
        {
            return CreateInputSettingsCommand(
                command,
                ReadInputSettingsBody(request),
                string.IsNullOrWhiteSpace(clientId) ? request.ClientId ?? "" : clientId);
        }

        public static AetheriaRuntimeEveCommandEnvelope CreateCatalogCommand(
            AetheriaRuntimeEveCommandKind command,
            string clientId)
        {
            return CreateTypedCommand(
                AetheriaRuntimeCatalogCommands.SurfaceId,
                command,
                clientId,
                playerSettings: new AetheriaRuntimePlayerSettingsCommandBody(),
                inputSettings: new AetheriaRuntimeInputSettingsCommandBody(),
                loadoutTemplate: null);
        }

        public static AetheriaRuntimeEveCommandEnvelope CreateOperationsCommand(
            AetheriaRuntimeEveCommandKind command,
            string clientId)
        {
            return CreateTypedCommand(
                AetheriaRuntimeOperationsCommands.SurfaceId,
                command,
                clientId,
                playerSettings: new AetheriaRuntimePlayerSettingsCommandBody(),
                inputSettings: new AetheriaRuntimeInputSettingsCommandBody(),
                loadoutTemplate: null);
        }

        public static AetheriaRuntimeEveCommandEnvelope CreateVerseHostCommand(
            AetheriaRuntimeEveCommandKind command,
            string clientId)
        {
            return CreateTypedCommand(
                AetheriaRuntimeVerseHostCommands.SurfaceId,
                command,
                clientId,
                playerSettings: new AetheriaRuntimePlayerSettingsCommandBody(),
                inputSettings: new AetheriaRuntimeInputSettingsCommandBody(),
                loadoutTemplate: null);
        }

        public static AetheriaRuntimeEveCommandEnvelope CreateLoadoutTemplateCommand(
            AetheriaRuntimeLoadoutTemplateCommit loadoutTemplate,
            string clientId)
        {
            return CreateTypedCommand(
                AetheriaRuntimeLoadoutTemplateCommands.SurfaceId,
                AetheriaRuntimeEveCommandKind.SaveLoadoutTemplate,
                clientId,
                playerSettings: new AetheriaRuntimePlayerSettingsCommandBody(),
                inputSettings: new AetheriaRuntimeInputSettingsCommandBody(),
                loadoutTemplate: loadoutTemplate);
        }

        public static AetheriaRuntimeEveCommandDocument ToDocument(AetheriaRuntimeEveCommandEnvelope envelope)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));

            return NormalizeDocument(new AetheriaRuntimeEveCommandDocument
            {
                Schema = string.IsNullOrWhiteSpace(envelope.Schema) ? CommandSchema : envelope.Schema,
                CommandId = envelope.CommandId ?? "",
                ProviderId = envelope.ProviderId ?? "",
                SurfaceId = envelope.SurfaceId ?? "",
                Command = envelope.Command ?? "",
                Kind = envelope.Kind,
                IssuedAtUtc = envelope.IssuedAtUtc ?? "",
                ClientId = envelope.ClientId ?? "",
                PlayerSettings = envelope.PlayerSettings ?? new AetheriaRuntimePlayerSettingsCommandBody(),
                InputSettings = envelope.InputSettings ?? new AetheriaRuntimeInputSettingsCommandBody(),
                LoadoutTemplate = envelope.LoadoutTemplate
            });
        }

        private static AetheriaRuntimeEveCommandEnvelope CreateTypedCommand(
            string surfaceId,
            AetheriaRuntimeEveCommandKind kind,
            string clientId,
            AetheriaRuntimePlayerSettingsCommandBody playerSettings,
            AetheriaRuntimeInputSettingsCommandBody inputSettings,
            AetheriaRuntimeLoadoutTemplateCommit? loadoutTemplate)
        {
            var command = CommandText(kind);
            var commandId = Guid.NewGuid().ToString("N");
            var issuedAtUtc = DateTime.UtcNow.ToString("O");
            var document = new AetheriaRuntimeEveCommandDocument
            {
                Schema = CommandSchema,
                CommandId = commandId,
                ProviderId = "aetheria",
                SurfaceId = surfaceId ?? "",
                Command = command,
                Kind = kind,
                IssuedAtUtc = issuedAtUtc,
                ClientId = clientId ?? "",
                PlayerSettings = playerSettings,
                InputSettings = inputSettings,
                LoadoutTemplate = loadoutTemplate
            };

            return ToEnvelope(document);
        }

        public static AetheriaRuntimeEveCommandEnvelope ToEnvelope(AetheriaRuntimeEveCommandDocument document)
        {
            NormalizeDocument(document);
            return new AetheriaRuntimeEveCommandEnvelope(
                document.Schema ?? "",
                document.CommandId ?? "",
                document.ProviderId ?? "",
                document.SurfaceId ?? "",
                document.Command ?? "",
                document.Kind,
                document.IssuedAtUtc ?? "",
                document.ClientId ?? "",
                document.PlayerSettings ?? new AetheriaRuntimePlayerSettingsCommandBody(),
                document.InputSettings ?? new AetheriaRuntimeInputSettingsCommandBody(),
                "",
                document.LoadoutTemplate);
        }

        public static AetheriaRuntimeEveCommandDocument NormalizeDocument(AetheriaRuntimeEveCommandDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            document.Schema = string.IsNullOrWhiteSpace(document.Schema) ? CommandSchema : document.Schema;
            if (document.Kind == AetheriaRuntimeEveCommandKind.Unknown)
                document.Kind = CommandKindForSurface(document.SurfaceId ?? "", document.Command ?? "");
            if (string.IsNullOrWhiteSpace(document.Command) && document.Kind != AetheriaRuntimeEveCommandKind.Unknown)
                document.Command = CommandText(document.Kind);
            return document;
        }

        public static AetheriaRuntimeEveCommandKind CommandKindForSurface(EveSurfaceCommandRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return CommandKindForSurface(request.SurfaceId ?? "", request.Command ?? "");
        }

        public static AetheriaRuntimeEveCommandKind CommandKindForSurface(string surfaceId, string command)
        {
            switch (surfaceId ?? "")
            {
                case AetheriaRuntimeCatalogCommands.SurfaceId:
                    return command == AetheriaRuntimeCatalogCommands.Refresh
                        ? AetheriaRuntimeEveCommandKind.CatalogRefresh
                        : AetheriaRuntimeEveCommandKind.Unknown;
                case AetheriaRuntimeOperationsCommands.SurfaceId:
                    return command == AetheriaRuntimeOperationsCommands.Refresh
                        ? AetheriaRuntimeEveCommandKind.OperationsRefresh
                        : AetheriaRuntimeEveCommandKind.Unknown;
                case AetheriaRuntimePlayerSettingsCommands.SurfaceId:
                    return PlayerSettingsKind(command);
                case AetheriaRuntimeInputSettingsCommands.SurfaceId:
                    return InputSettingsKind(command);
                case AetheriaRuntimeLoadoutTemplateCommands.SurfaceId:
                    return command == AetheriaRuntimeLoadoutTemplateCommands.Save
                        ? AetheriaRuntimeEveCommandKind.SaveLoadoutTemplate
                        : AetheriaRuntimeEveCommandKind.Unknown;
                case AetheriaRuntimeVerseHostCommands.SurfaceId:
                    return command == AetheriaRuntimeVerseHostCommands.Refresh
                        ? AetheriaRuntimeEveCommandKind.VerseHostRefresh
                        : command == AetheriaRuntimeVerseHostCommands.CycleVisibility
                            ? AetheriaRuntimeEveCommandKind.CycleVerseHostVisibility
                            : AetheriaRuntimeEveCommandKind.Unknown;
                default:
                    return AetheriaRuntimeEveCommandKind.Unknown;
            }
        }

        public static string CommandText(AetheriaRuntimeEveCommandKind kind)
        {
            switch (kind)
            {
                case AetheriaRuntimeEveCommandKind.CatalogRefresh:
                    return AetheriaRuntimeCatalogCommands.Refresh;
                case AetheriaRuntimeEveCommandKind.OperationsRefresh:
                    return AetheriaRuntimeOperationsCommands.Refresh;
                case AetheriaRuntimeEveCommandKind.PlayerSettingsRefresh:
                    return AetheriaRuntimePlayerSettingsCommands.Refresh;
                case AetheriaRuntimeEveCommandKind.SetPlayerName:
                    return AetheriaRuntimePlayerSettingsCommands.SetPlayerName;
                case AetheriaRuntimeEveCommandKind.CycleTemperatureUnit:
                    return AetheriaRuntimePlayerSettingsCommands.CycleTemperatureUnit;
                case AetheriaRuntimeEveCommandKind.DecrementSignificantDigits:
                    return AetheriaRuntimePlayerSettingsCommands.DecrementSignificantDigits;
                case AetheriaRuntimeEveCommandKind.IncrementSignificantDigits:
                    return AetheriaRuntimePlayerSettingsCommands.IncrementSignificantDigits;
                case AetheriaRuntimeEveCommandKind.CycleNebulaQuality:
                    return AetheriaRuntimePlayerSettingsCommands.CycleNebulaQuality;
                case AetheriaRuntimeEveCommandKind.ToggleShowAsteroidsInMinimap:
                    return AetheriaRuntimePlayerSettingsCommands.ToggleShowAsteroidsInMinimap;
                case AetheriaRuntimeEveCommandKind.InputSettingsRefresh:
                    return AetheriaRuntimeInputSettingsCommands.Refresh;
                case AetheriaRuntimeEveCommandKind.BeginInputCapture:
                    return AetheriaRuntimeInputSettingsCommands.BeginCapture;
                case AetheriaRuntimeEveCommandKind.CancelInputCapture:
                    return AetheriaRuntimeInputSettingsCommands.CancelCapture;
                case AetheriaRuntimeEveCommandKind.SetBindingOverride:
                    return AetheriaRuntimeInputSettingsCommands.SetBindingOverride;
                case AetheriaRuntimeEveCommandKind.ToggleActionBar:
                    return AetheriaRuntimeInputSettingsCommands.ToggleActionBar;
                case AetheriaRuntimeEveCommandKind.SetActionBarEnabled:
                    return AetheriaRuntimeInputSettingsCommands.SetActionBarEnabled;
                case AetheriaRuntimeEveCommandKind.SaveLoadoutTemplate:
                    return AetheriaRuntimeLoadoutTemplateCommands.Save;
                case AetheriaRuntimeEveCommandKind.VerseHostRefresh:
                    return AetheriaRuntimeVerseHostCommands.Refresh;
                case AetheriaRuntimeEveCommandKind.CycleVerseHostVisibility:
                    return AetheriaRuntimeVerseHostCommands.CycleVisibility;
                default:
                    return "";
            }
        }

        private static AetheriaRuntimeEveCommandKind PlayerSettingsKind(string command)
        {
            if (command == AetheriaRuntimePlayerSettingsCommands.Refresh)
                return AetheriaRuntimeEveCommandKind.PlayerSettingsRefresh;
            if (command == AetheriaRuntimePlayerSettingsCommands.SetPlayerName)
                return AetheriaRuntimeEveCommandKind.SetPlayerName;
            if (command == AetheriaRuntimePlayerSettingsCommands.CycleTemperatureUnit)
                return AetheriaRuntimeEveCommandKind.CycleTemperatureUnit;
            if (command == AetheriaRuntimePlayerSettingsCommands.DecrementSignificantDigits)
                return AetheriaRuntimeEveCommandKind.DecrementSignificantDigits;
            if (command == AetheriaRuntimePlayerSettingsCommands.IncrementSignificantDigits)
                return AetheriaRuntimeEveCommandKind.IncrementSignificantDigits;
            if (command == AetheriaRuntimePlayerSettingsCommands.CycleNebulaQuality)
                return AetheriaRuntimeEveCommandKind.CycleNebulaQuality;
            if (command == AetheriaRuntimePlayerSettingsCommands.ToggleShowAsteroidsInMinimap)
                return AetheriaRuntimeEveCommandKind.ToggleShowAsteroidsInMinimap;
            return AetheriaRuntimeEveCommandKind.Unknown;
        }

        private static AetheriaRuntimeEveCommandKind InputSettingsKind(string command)
        {
            if (command == AetheriaRuntimeInputSettingsCommands.Refresh)
                return AetheriaRuntimeEveCommandKind.InputSettingsRefresh;
            if (command == AetheriaRuntimeInputSettingsCommands.BeginCapture)
                return AetheriaRuntimeEveCommandKind.BeginInputCapture;
            if (command == AetheriaRuntimeInputSettingsCommands.CancelCapture)
                return AetheriaRuntimeEveCommandKind.CancelInputCapture;
            if (command == AetheriaRuntimeInputSettingsCommands.SetBindingOverride)
                return AetheriaRuntimeEveCommandKind.SetBindingOverride;
            if (command == AetheriaRuntimeInputSettingsCommands.ToggleActionBar)
                return AetheriaRuntimeEveCommandKind.ToggleActionBar;
            if (command == AetheriaRuntimeInputSettingsCommands.SetActionBarEnabled)
                return AetheriaRuntimeEveCommandKind.SetActionBarEnabled;
            return AetheriaRuntimeEveCommandKind.Unknown;
        }

        private static AetheriaRuntimePlayerSettingsCommandBody ReadPlayerSettingsBody(EveSurfaceCommandRequest request)
        {
            if (!string.Equals(request.SurfaceId, AetheriaRuntimePlayerSettingsCommands.SurfaceId, StringComparison.Ordinal))
                return new AetheriaRuntimePlayerSettingsCommandBody();

            return new AetheriaRuntimePlayerSettingsCommandBody
            {
                PlayerName = ReadPayload(request, "value")
            };
        }

        private static AetheriaRuntimeInputSettingsCommandBody ReadInputSettingsBody(EveSurfaceCommandRequest request)
        {
            if (!string.Equals(request.SurfaceId, AetheriaRuntimeInputSettingsCommands.SurfaceId, StringComparison.Ordinal))
                return new AetheriaRuntimeInputSettingsCommandBody();

            return new AetheriaRuntimeInputSettingsCommandBody
            {
                ActionName = ReadPayload(request, "actionName"),
                BindingIndex = ReadPayloadInt(request, "bindingIndex", -1),
                InputSystemPath = ReadPayload(request, "inputSystemPath"),
                Enabled = string.Equals(ReadPayload(request, "enabled"), "true", StringComparison.OrdinalIgnoreCase)
            };
        }

        private static string ReadPayload(EveSurfaceCommandRequest request, string key)
        {
            return request.Payload != null && request.Payload.TryGetValue(key, out var value)
                ? value ?? ""
                : "";
        }

        private static int ReadPayloadInt(EveSurfaceCommandRequest request, string key, int defaultValue)
        {
            return int.TryParse(ReadPayload(request, key), out var value)
                ? value
                : defaultValue;
        }

    }
}

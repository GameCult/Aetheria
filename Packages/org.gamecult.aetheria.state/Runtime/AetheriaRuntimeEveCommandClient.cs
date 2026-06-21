using System;
using GameCult.Eve.Surface;

#nullable enable

namespace GameCult.Aetheria.State.Unity
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
            string command,
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
            string command,
            AetheriaRuntimeInputSettingsCommandBody body,
            string clientId)
        {
            return AetheriaRuntimeEveCommandClient.CreateInputSettingsCommand(command, body, clientId);
        }

        public static bool TrySendInputSettingsCommand(
            string stateFilePath,
            string command,
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
            string command,
            string clientId)
        {
            return AetheriaRuntimeEveCommandClient.CreateCatalogCommand(command, clientId);
        }

        public static AetheriaRuntimeEveCommandEnvelope SubmitOperationsCommand(
            string stateFilePath,
            string command,
            string clientId)
        {
            return AetheriaRuntimeEveCommandClient.CreateOperationsCommand(command, clientId);
        }

        public static AetheriaRuntimeEveCommandEnvelope SubmitVerseHostCommand(
            string stateFilePath,
            string command,
            string clientId)
        {
            return AetheriaRuntimeEveCommandClient.CreateVerseHostCommand(command, clientId);
        }

        public static bool TrySendVerseHostCommand(
            string stateFilePath,
            string command,
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
                    envelope = CreatePlayerSettingsCommand(request, clientId);
                    return true;
                case AetheriaRuntimeInputSettingsCommands.SurfaceId
                    when AetheriaRuntimeInputSettingsCommands.IsKnown(command):
                    envelope = CreateInputSettingsCommand(request, clientId);
                    return true;
                case AetheriaRuntimeCatalogCommands.SurfaceId
                    when AetheriaRuntimeCatalogCommands.IsKnown(command):
                    envelope = CreateCatalogCommand(command, clientId);
                    return true;
                case AetheriaRuntimeOperationsCommands.SurfaceId
                    when AetheriaRuntimeOperationsCommands.IsKnown(command):
                    envelope = CreateOperationsCommand(command, clientId);
                    return true;
                case AetheriaRuntimeVerseHostCommands.SurfaceId
                    when AetheriaRuntimeVerseHostCommands.IsKnown(command):
                    envelope = CreateVerseHostCommand(command, clientId);
                    return true;
            }

            envelope = null;
            return false;
        }

        public static AetheriaRuntimeEveCommandEnvelope CreatePlayerSettingsCommand(
            string command,
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
            return CreatePlayerSettingsCommand(
                request.Command ?? "",
                ReadPlayerSettingsBody(request),
                string.IsNullOrWhiteSpace(clientId) ? request.ClientId ?? "" : clientId);
        }

        public static AetheriaRuntimeEveCommandEnvelope CreateInputSettingsCommand(
            string command,
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
            return CreateInputSettingsCommand(
                request.Command ?? "",
                ReadInputSettingsBody(request),
                string.IsNullOrWhiteSpace(clientId) ? request.ClientId ?? "" : clientId);
        }

        public static AetheriaRuntimeEveCommandEnvelope CreateCatalogCommand(
            string command,
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
            string command,
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
            string command,
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
                AetheriaRuntimeLoadoutTemplateCommands.Save,
                clientId,
                playerSettings: new AetheriaRuntimePlayerSettingsCommandBody(),
                inputSettings: new AetheriaRuntimeInputSettingsCommandBody(),
                loadoutTemplate: loadoutTemplate);
        }

        public static AetheriaRuntimeEveCommandDocument ToDocument(AetheriaRuntimeEveCommandEnvelope envelope)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));

            return new AetheriaRuntimeEveCommandDocument
            {
                Schema = string.IsNullOrWhiteSpace(envelope.Schema) ? CommandSchema : envelope.Schema,
                CommandId = envelope.CommandId ?? "",
                ProviderId = envelope.ProviderId ?? "",
                SurfaceId = envelope.SurfaceId ?? "",
                Command = envelope.Command ?? "",
                IssuedAtUtc = envelope.IssuedAtUtc ?? "",
                ClientId = envelope.ClientId ?? "",
                PlayerSettings = envelope.PlayerSettings ?? new AetheriaRuntimePlayerSettingsCommandBody(),
                InputSettings = envelope.InputSettings ?? new AetheriaRuntimeInputSettingsCommandBody(),
                LoadoutTemplate = envelope.LoadoutTemplate
            };
        }

        private static AetheriaRuntimeEveCommandEnvelope CreateTypedCommand(
            string surfaceId,
            string command,
            string clientId,
            AetheriaRuntimePlayerSettingsCommandBody playerSettings,
            AetheriaRuntimeInputSettingsCommandBody inputSettings,
            AetheriaRuntimeLoadoutTemplateCommit? loadoutTemplate)
        {
            var commandId = Guid.NewGuid().ToString("N");
            var issuedAtUtc = DateTime.UtcNow.ToString("O");
            var document = new AetheriaRuntimeEveCommandDocument
            {
                Schema = CommandSchema,
                CommandId = commandId,
                ProviderId = "aetheria",
                SurfaceId = surfaceId ?? "",
                Command = command ?? "",
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
            return new AetheriaRuntimeEveCommandEnvelope(
                document.Schema ?? "",
                document.CommandId ?? "",
                document.ProviderId ?? "",
                document.SurfaceId ?? "",
                document.Command ?? "",
                document.IssuedAtUtc ?? "",
                document.ClientId ?? "",
                document.PlayerSettings ?? new AetheriaRuntimePlayerSettingsCommandBody(),
                document.InputSettings ?? new AetheriaRuntimeInputSettingsCommandBody(),
                "",
                document.LoadoutTemplate);
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

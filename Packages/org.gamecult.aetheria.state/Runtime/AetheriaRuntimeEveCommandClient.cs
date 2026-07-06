using System;
using GameCult.Eve.Surface;
using GameCult.Mesh;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    internal static class AetheriaRuntimeEveCommands
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

        public static AetheriaRuntimeEveCommandEnvelope SubmitTradeValuePolicyCommand(
            string stateFilePath,
            AetheriaRuntimeEveCommandKind command,
            AetheriaRuntimeTradeValuePolicyCommandBody body,
            string clientId)
        {
            return AetheriaRuntimeEveCommandClient.CreateTradeValuePolicyCommand(command, body, clientId);
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
            envelope = null;
            error = "";

            try
            {
                using var client = AetheriaClient
                    .OpenAsync(
                        stateFilePath,
                        string.IsNullOrWhiteSpace(clientId) ? "aetheria-eve-client" : clientId,
                        "local",
                        startServer: false,
                        pullOnOpen: true)
                    .GetAwaiter()
                    .GetResult();
                envelope = client
                    .SubmitEveCommandDocument(AetheriaRuntimeEveCommandClient.ToDocument(commandEnvelope));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
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

            var command = OperationIdFor(request);
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
                case AetheriaRuntimeTradeValuePolicyCommands.SurfaceId
                    when AetheriaRuntimeTradeValuePolicyCommands.IsKnown(command):
                    envelope = CreateTradeValuePolicyCommand(CommandKindForSurface(request), request, clientId);
                    return true;
                case AetheriaRuntimeMainMenuCommands.RootSurfaceId:
                case AetheriaRuntimeMainMenuCommands.SettingsSurfaceId:
                case AetheriaRuntimeMainMenuCommands.InputSettingsSurfaceId:
                case AetheriaRuntimeMainMenuCommands.PlayerSettingsSurfaceId:
                case AetheriaRuntimeMainMenuCommands.VerseSettingsSurfaceId:
                    envelope = CreateMainMenuCommand(CommandKindForSurface(request), request, clientId);
                    return envelope.Kind != AetheriaRuntimeEveCommandKind.Unknown;
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
            return CreateTypedCommand(
                AetheriaRuntimePlayerSettingsCommands.SurfaceId,
                command,
                string.IsNullOrWhiteSpace(clientId) ? request.ClientId ?? "" : clientId,
                playerSettings: ReadPlayerSettingsBody(request),
                inputSettings: new AetheriaRuntimeInputSettingsCommandBody(),
                loadoutTemplate: null,
                invocation: request.Operation,
                payload: request.Payload);
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
            return CreateTypedCommand(
                AetheriaRuntimeInputSettingsCommands.SurfaceId,
                command,
                string.IsNullOrWhiteSpace(clientId) ? request.ClientId ?? "" : clientId,
                playerSettings: new AetheriaRuntimePlayerSettingsCommandBody(),
                inputSettings: ReadInputSettingsBody(request),
                loadoutTemplate: null,
                invocation: request.Operation,
                payload: request.Payload);
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

        public static AetheriaRuntimeEveCommandEnvelope CreateTradeValuePolicyCommand(
            AetheriaRuntimeEveCommandKind command,
            AetheriaRuntimeTradeValuePolicyCommandBody body,
            string clientId)
        {
            return CreateTypedCommand(
                AetheriaRuntimeTradeValuePolicyCommands.SurfaceId,
                command,
                clientId,
                playerSettings: new AetheriaRuntimePlayerSettingsCommandBody(),
                inputSettings: new AetheriaRuntimeInputSettingsCommandBody(),
                loadoutTemplate: null,
                tradeValuePolicy: body ?? new AetheriaRuntimeTradeValuePolicyCommandBody());
        }

        private static AetheriaRuntimeEveCommandEnvelope CreateTradeValuePolicyCommand(
            AetheriaRuntimeEveCommandKind command,
            EveSurfaceCommandRequest request,
            string clientId)
        {
            return CreateTypedCommand(
                AetheriaRuntimeTradeValuePolicyCommands.SurfaceId,
                command,
                string.IsNullOrWhiteSpace(clientId) ? request.ClientId ?? "" : clientId,
                playerSettings: new AetheriaRuntimePlayerSettingsCommandBody(),
                inputSettings: new AetheriaRuntimeInputSettingsCommandBody(),
                loadoutTemplate: null,
                tradeValuePolicy: ReadTradeValuePolicyBody(request),
                invocation: request.Operation,
                payload: request.Payload);
        }

        private static AetheriaRuntimeEveCommandEnvelope CreateMainMenuCommand(
            AetheriaRuntimeEveCommandKind command,
            EveSurfaceCommandRequest request,
            string clientId)
        {
            return CreateTypedCommand(
                request.SurfaceId ?? AetheriaRuntimeMainMenuCommands.RootSurfaceId,
                command,
                string.IsNullOrWhiteSpace(clientId) ? request.ClientId ?? "" : clientId,
                playerSettings: ReadPlayerSettingsBody(request),
                inputSettings: ReadInputSettingsBody(request),
                loadoutTemplate: null,
                tradeValuePolicy: null,
                invocation: request.Operation,
                payload: request.Payload);
        }

        public static AetheriaRuntimeEveCommandDocument ToDocument(AetheriaRuntimeEveCommandEnvelope envelope)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));

            var invocation = CultMesh.OperationInvocationRecord(
                envelope.Invocation,
                fallbackOperationId: envelope.Command,
                fallbackSchemaId: CommandSchema,
                fallbackRouteHint: envelope.Receipt.Route,
                fallbackIdempotencyKey: envelope.CommandId);
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
                LoadoutTemplate = envelope.LoadoutTemplate,
                TradeValuePolicy = envelope.TradeValuePolicy ?? new AetheriaRuntimeTradeValuePolicyCommandBody(),
                Operation = invocation,
                Payload = envelope.Payload.ToDictionary()
            });
        }

        private static AetheriaRuntimeEveCommandEnvelope CreateTypedCommand(
            string surfaceId,
            AetheriaRuntimeEveCommandKind kind,
            string clientId,
            AetheriaRuntimePlayerSettingsCommandBody playerSettings,
            AetheriaRuntimeInputSettingsCommandBody inputSettings,
            AetheriaRuntimeLoadoutTemplateCommit? loadoutTemplate,
            AetheriaRuntimeTradeValuePolicyCommandBody? tradeValuePolicy = null,
            CultMeshOperationInvocationDescriptor? invocation = null,
            CultMeshOperationPayload? payload = null)
        {
            var command = CommandText(kind);
            var commandId = Guid.NewGuid().ToString("N");
            var issuedAtUtc = DateTime.UtcNow.ToString("O");
            var route = new CultMeshRouteHint(CultMeshLocalityKind.Network, "aetheria-eve-command");
            var invocationRecord = CultMesh.OperationInvocationRecord(
                invocation,
                fallbackOperationId: command,
                fallbackSchemaId: CommandSchema,
                fallbackRouteHint: route,
                fallbackIdempotencyKey: commandId);
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
                LoadoutTemplate = loadoutTemplate,
                TradeValuePolicy = tradeValuePolicy ?? new AetheriaRuntimeTradeValuePolicyCommandBody(),
                Operation = invocationRecord,
                Payload = (payload ?? CultMeshOperationPayload.Empty).ToDictionary()
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
                document.LoadoutTemplate,
                document.TradeValuePolicy ?? new AetheriaRuntimeTradeValuePolicyCommandBody(),
                receipt: null,
                invocation: CreateInvocation(document),
                payload: CultMesh.OperationPayload(document.Payload));
        }

        public static AetheriaRuntimeEveCommandDocument NormalizeDocument(AetheriaRuntimeEveCommandDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            document.Schema = string.IsNullOrWhiteSpace(document.Schema) ? CommandSchema : document.Schema;
            if (document.Kind == AetheriaRuntimeEveCommandKind.Unknown)
                document.Kind = CommandKindForSurface(document.SurfaceId ?? "", document.Command ?? "");
            if (string.IsNullOrWhiteSpace(document.Command) && document.Kind != AetheriaRuntimeEveCommandKind.Unknown)
                document.Command = CommandText(document.Kind);
            document.Operation = NormalizeInvocationRecord(document);
            if (document.Payload == null)
                document.Payload = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
            return document;
        }

        private static CultMeshOperationInvocationDescriptor CreateInvocation(AetheriaRuntimeEveCommandDocument document)
        {
            return (document.Operation ?? new CultMeshOperationInvocationRecord()).ToInvocation(
                fallbackOperationId: document.Command ?? "",
                fallbackSchemaId: CommandSchema,
                fallbackRouteHint: new CultMeshRouteHint(CultMeshLocalityKind.Network, "aetheria-eve-command"),
                fallbackIdempotencyKey: document.CommandId);
        }

        private static CultMeshOperationInvocationRecord NormalizeInvocationRecord(
            AetheriaRuntimeEveCommandDocument document)
        {
            var invocation = (document.Operation ?? new CultMeshOperationInvocationRecord()).ToInvocation(
                fallbackOperationId: document.Command ?? "",
                fallbackSchemaId: CommandSchema,
                fallbackRouteHint: new CultMeshRouteHint(CultMeshLocalityKind.Network, "aetheria-eve-command"),
                fallbackIdempotencyKey: document.CommandId);
            return CultMesh.OperationInvocationRecord(invocation);
        }

        public static AetheriaRuntimeEveCommandKind CommandKindForSurface(EveSurfaceCommandRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return CommandKindForSurface(request.SurfaceId ?? "", OperationIdFor(request));
        }

        private static string OperationIdFor(EveSurfaceCommandRequest request)
        {
            return request?.Operation?.OperationId ?? "";
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
                case AetheriaRuntimeTradeValuePolicyCommands.SurfaceId:
                    return TradeValuePolicyKind(command);
                case AetheriaRuntimeMainMenuCommands.RootSurfaceId:
                    return MainMenuRootKind(command);
                case AetheriaRuntimeMainMenuCommands.SettingsSurfaceId:
                    return MainMenuSettingsKind(command);
                case AetheriaRuntimeMainMenuCommands.InputSettingsSurfaceId:
                    return command == AetheriaRuntimeMainMenuCommands.OpenRuntimeInputScreen
                        ? AetheriaRuntimeEveCommandKind.MainMenuOpenRuntimeInputScreen
                        : command == AetheriaRuntimeMainMenuCommands.BackToSettings
                            ? AetheriaRuntimeEveCommandKind.MainMenuBackToSettings
                            : InputSettingsKind(command);
                case AetheriaRuntimeMainMenuCommands.PlayerSettingsSurfaceId:
                    return command == AetheriaRuntimeMainMenuCommands.BackToSettings
                        ? AetheriaRuntimeEveCommandKind.MainMenuBackToSettings
                        : PlayerSettingsKind(command);
                case AetheriaRuntimeMainMenuCommands.VerseSettingsSurfaceId:
                    return command == AetheriaRuntimeMainMenuCommands.BackToSettings
                        ? AetheriaRuntimeEveCommandKind.MainMenuBackToSettings
                        : command == AetheriaRuntimeVerseHostCommands.Refresh
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
                case AetheriaRuntimeEveCommandKind.TradeValuePolicyRefresh:
                    return AetheriaRuntimeTradeValuePolicyCommands.Refresh;
                case AetheriaRuntimeEveCommandKind.SetTradeValueQualityMinimum:
                    return AetheriaRuntimeTradeValuePolicyCommands.SetQualityMinimum;
                case AetheriaRuntimeEveCommandKind.SetTradeValueQualityMaximum:
                    return AetheriaRuntimeTradeValuePolicyCommands.SetQualityMaximum;
                case AetheriaRuntimeEveCommandKind.SetTradeValueQualityExponent:
                    return AetheriaRuntimeTradeValuePolicyCommands.SetQualityExponent;
                case AetheriaRuntimeEveCommandKind.SetTradeValueTierQuality:
                    return AetheriaRuntimeTradeValuePolicyCommands.SetTierQuality;
                case AetheriaRuntimeEveCommandKind.MainMenuContinueRun:
                    return AetheriaRuntimeMainMenuCommands.ContinueRun;
                case AetheriaRuntimeEveCommandKind.MainMenuNewGame:
                    return AetheriaRuntimeMainMenuCommands.NewGame;
                case AetheriaRuntimeEveCommandKind.MainMenuShowSettings:
                    return AetheriaRuntimeMainMenuCommands.ShowSettings;
                case AetheriaRuntimeEveCommandKind.MainMenuQuit:
                    return AetheriaRuntimeMainMenuCommands.Quit;
                case AetheriaRuntimeEveCommandKind.MainMenuShowPlayerSettings:
                    return AetheriaRuntimeMainMenuCommands.ShowPlayerSettings;
                case AetheriaRuntimeEveCommandKind.MainMenuShowVerseSettings:
                    return AetheriaRuntimeMainMenuCommands.ShowVerseSettings;
                case AetheriaRuntimeEveCommandKind.MainMenuShowInputSettings:
                    return AetheriaRuntimeMainMenuCommands.ShowInputSettings;
                case AetheriaRuntimeEveCommandKind.MainMenuBackToMain:
                    return AetheriaRuntimeMainMenuCommands.BackToMain;
                case AetheriaRuntimeEveCommandKind.MainMenuBackToSettings:
                    return AetheriaRuntimeMainMenuCommands.BackToSettings;
                case AetheriaRuntimeEveCommandKind.MainMenuOpenRuntimeInputScreen:
                    return AetheriaRuntimeMainMenuCommands.OpenRuntimeInputScreen;
                default:
                    return "";
            }
        }

        private static AetheriaRuntimeEveCommandKind MainMenuRootKind(string command)
        {
            if (command == AetheriaRuntimeMainMenuCommands.ContinueRun)
                return AetheriaRuntimeEveCommandKind.MainMenuContinueRun;
            if (command == AetheriaRuntimeMainMenuCommands.NewGame)
                return AetheriaRuntimeEveCommandKind.MainMenuNewGame;
            if (command == AetheriaRuntimeMainMenuCommands.ShowSettings)
                return AetheriaRuntimeEveCommandKind.MainMenuShowSettings;
            if (command == AetheriaRuntimeMainMenuCommands.Quit)
                return AetheriaRuntimeEveCommandKind.MainMenuQuit;
            return AetheriaRuntimeEveCommandKind.Unknown;
        }

        private static AetheriaRuntimeEveCommandKind MainMenuSettingsKind(string command)
        {
            if (command == AetheriaRuntimeMainMenuCommands.ShowPlayerSettings)
                return AetheriaRuntimeEveCommandKind.MainMenuShowPlayerSettings;
            if (command == AetheriaRuntimeMainMenuCommands.ShowVerseSettings)
                return AetheriaRuntimeEveCommandKind.MainMenuShowVerseSettings;
            if (command == AetheriaRuntimeMainMenuCommands.ShowInputSettings)
                return AetheriaRuntimeEveCommandKind.MainMenuShowInputSettings;
            if (command == AetheriaRuntimeMainMenuCommands.BackToMain)
                return AetheriaRuntimeEveCommandKind.MainMenuBackToMain;
            return AetheriaRuntimeEveCommandKind.Unknown;
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

        private static AetheriaRuntimeEveCommandKind TradeValuePolicyKind(string command)
        {
            if (command == AetheriaRuntimeTradeValuePolicyCommands.Refresh)
                return AetheriaRuntimeEveCommandKind.TradeValuePolicyRefresh;
            if (command == AetheriaRuntimeTradeValuePolicyCommands.SetQualityMinimum)
                return AetheriaRuntimeEveCommandKind.SetTradeValueQualityMinimum;
            if (command == AetheriaRuntimeTradeValuePolicyCommands.SetQualityMaximum)
                return AetheriaRuntimeEveCommandKind.SetTradeValueQualityMaximum;
            if (command == AetheriaRuntimeTradeValuePolicyCommands.SetQualityExponent)
                return AetheriaRuntimeEveCommandKind.SetTradeValueQualityExponent;
            if (command == AetheriaRuntimeTradeValuePolicyCommands.SetTierQuality)
                return AetheriaRuntimeEveCommandKind.SetTradeValueTierQuality;
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

        private static AetheriaRuntimeTradeValuePolicyCommandBody ReadTradeValuePolicyBody(EveSurfaceCommandRequest request)
        {
            if (!string.Equals(request.SurfaceId, AetheriaRuntimeTradeValuePolicyCommands.SurfaceId, StringComparison.Ordinal))
                return new AetheriaRuntimeTradeValuePolicyCommandBody();

            return new AetheriaRuntimeTradeValuePolicyCommandBody
            {
                Value = ReadPayloadDouble(request, "value", 0),
                TierIndex = ReadPayloadInt(request, "tierIndex", -1)
            };
        }

        private static string ReadPayload(EveSurfaceCommandRequest request, string key)
        {
            return request.Payload.GetString(key);
        }

        private static int ReadPayloadInt(EveSurfaceCommandRequest request, string key, int defaultValue)
        {
            return request.Payload.GetInt32(key, defaultValue);
        }

        private static double ReadPayloadDouble(EveSurfaceCommandRequest request, string key, double defaultValue)
        {
            return request.Payload.GetDouble(key, defaultValue);
        }

    }
}

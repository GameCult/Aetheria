using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeDaemonTickOptions
    {
        public string DaemonId { get; set; } = "aetheria-daemon";
        public string SessionId { get; set; } = "local";
        public string VerseId { get; set; } = "aetheria.local";
        public string CultMeshAddress { get; set; } = "cultmesh://aetheria.local/eve/providers/aetheria.daemon";
        public long FrameId { get; set; }
        public double SimulationTimeSeconds { get; set; }
        public double FixedDeltaSeconds { get; set; }
        public IReadOnlyList<AetheriaRuntimeDaemonCommandDocument> ObservedCommands { get; set; } =
            Array.Empty<AetheriaRuntimeDaemonCommandDocument>();
        public IReadOnlyList<string> AccountedCommandIds { get; set; } =
            Array.Empty<string>();
        public IReadOnlyList<string> PreRejectedCommandIds { get; set; } =
            Array.Empty<string>();
        public IReadOnlyList<string> CumulativeAppliedCommandIds { get; set; } =
            Array.Empty<string>();
        public IReadOnlyList<string> CumulativeRejectedCommandIds { get; set; } =
            Array.Empty<string>();
        public AetheriaRuntimeDaemonOperationContext OperationContext { get; set; } =
            new AetheriaRuntimeDaemonOperationContext();
        public AetheriaRuntimeStarbridgeScenarioDocument? StarbridgeScenario { get; set; }
        public AetheriaRuntimeStarbridgeSessionDocument? StarbridgeSession { get; set; }
        public AetheriaRuntimeCatalogSnapshot? Catalog { get; set; }
        public bool PublishWitnesses { get; set; } = true;
    }

    public sealed class AetheriaRuntimeDaemonTickResult
    {
        public AetheriaRuntimeDaemonTickResult(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonOperationResult operationResult,
            AetheriaRuntimeDaemonFrameDocument frame,
            string framePath,
            string providerAdvertisementPath,
            string healthPath,
            string commandBoundaryPath,
            string gameSurfacePath,
            string gameTuiSurfacePath,
            string editorSurfacePath,
            string editorTuiSurfacePath,
            AetheriaRuntimeDaemonProviderAdvertisementDocument? providerAdvertisement = null,
            AetheriaRuntimeDaemonHealthDocument? health = null,
            AetheriaRuntimeDaemonCommandBoundaryDocument? commandBoundary = null,
            AetheriaRuntimeStarbridgeSessionSummaryDocument? starbridgeSessionSummary = null,
            AetheriaRuntimeSurfaceDocument? gameSurface = null,
            AetheriaRuntimeSurfaceDocument? gameTuiSurface = null,
            AetheriaRuntimeSurfaceDocument? editorSurface = null,
            AetheriaRuntimeSurfaceDocument? editorTuiSurface = null)
        {
            Run = run ?? new AetheriaRuntimeRunCheckpointCommit();
            OperationResult = operationResult ?? new AetheriaRuntimeDaemonOperationResult(
                Run,
                Array.Empty<string>(),
                Array.Empty<string>());
            Frame = frame ?? new AetheriaRuntimeDaemonFrameDocument();
            FramePath = framePath ?? "";
            ProviderAdvertisementPath = providerAdvertisementPath ?? "";
            HealthPath = healthPath ?? "";
            CommandBoundaryPath = commandBoundaryPath ?? "";
            GameSurfacePath = gameSurfacePath ?? "";
            GameTuiSurfacePath = gameTuiSurfacePath ?? "";
            EditorSurfacePath = editorSurfacePath ?? "";
            EditorTuiSurfacePath = editorTuiSurfacePath ?? "";
            ProviderAdvertisement = providerAdvertisement;
            Health = health;
            CommandBoundary = commandBoundary;
            StarbridgeSessionSummary = starbridgeSessionSummary;
            GameSurface = gameSurface;
            GameTuiSurface = gameTuiSurface;
            EditorSurface = editorSurface;
            EditorTuiSurface = editorTuiSurface;
        }

        public AetheriaRuntimeRunCheckpointCommit Run { get; }
        public AetheriaRuntimeDaemonOperationResult OperationResult { get; }
        public AetheriaRuntimeDaemonFrameDocument Frame { get; }
        public string FramePath { get; }
        public string ProviderAdvertisementPath { get; }
        public string HealthPath { get; }
        public string CommandBoundaryPath { get; }
        public string GameSurfacePath { get; }
        public string GameTuiSurfacePath { get; }
        public string EditorSurfacePath { get; }
        public string EditorTuiSurfacePath { get; }
        public AetheriaRuntimeDaemonProviderAdvertisementDocument? ProviderAdvertisement { get; }
        public AetheriaRuntimeDaemonHealthDocument? Health { get; }
        public AetheriaRuntimeDaemonCommandBoundaryDocument? CommandBoundary { get; }
        public AetheriaRuntimeStarbridgeSessionSummaryDocument? StarbridgeSessionSummary { get; }
        public AetheriaRuntimeSurfaceDocument? GameSurface { get; }
        public AetheriaRuntimeSurfaceDocument? GameTuiSurface { get; }
        public AetheriaRuntimeSurfaceDocument? EditorSurface { get; }
        public AetheriaRuntimeSurfaceDocument? EditorTuiSurface { get; }
        public AetheriaRuntimeDaemonIntentState Intents => OperationResult.Intents;
    }

    public static class AetheriaRuntimeDaemonTickRunner
    {
        public static AetheriaRuntimeDaemonTickResult Tick(
            string stateFilePath,
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonTickOptions options)
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new ArgumentException("State file path must be non-empty.", nameof(stateFilePath));

            run ??= new AetheriaRuntimeRunCheckpointCommit();
            options ??= new AetheriaRuntimeDaemonTickOptions();
            options.OperationContext ??= new AetheriaRuntimeDaemonOperationContext();

            var observedCommands = (options.ObservedCommands ?? Array.Empty<AetheriaRuntimeDaemonCommandDocument>())
                .Where(command => command != null)
                .ToArray();
            var accountedBeforeTick = new HashSet<string>(
                options.AccountedCommandIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            var commands = observedCommands
                .Where(command => !string.IsNullOrWhiteSpace(command.CommandId))
                .Where(command => !accountedBeforeTick.Contains(command.CommandId))
                .ToArray();
            var operationResult = AetheriaRuntimeDaemonOperations.Execute(
                run,
                commands,
                options.OperationContext);
            var preRejectedCommandIds = (options.PreRejectedCommandIds ?? Array.Empty<string>())
                .Where(commandId => !string.IsNullOrWhiteSpace(commandId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (preRejectedCommandIds.Length > 0)
            {
                operationResult = new AetheriaRuntimeDaemonOperationResult(
                    operationResult.Run,
                    operationResult.AppliedCommandIds,
                    preRejectedCommandIds
                        .Concat(operationResult.RejectedCommandIds)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                    operationResult.Intents);
            }
            AetheriaRuntimeRtsSimulation.Step(
                operationResult.Run,
                operationResult.Intents,
                options.FixedDeltaSeconds);
            StampZoneSimulationTime(operationResult.Run, options.SimulationTimeSeconds);

            var frame = AetheriaRuntimeDaemonFrameDocument.Create(
                operationResult.Run,
                options.DaemonId,
                options.SessionId,
                options.FrameId,
                options.SimulationTimeSeconds,
                options.FixedDeltaSeconds);
            frame.AppliedCommandIds = operationResult.AppliedCommandIds;
            frame.RejectedCommandIds = operationResult.RejectedCommandIds;
            frame.AccountedCommandIds = accountedBeforeTick
                .Concat(operationResult.AppliedCommandIds)
                .Concat(operationResult.RejectedCommandIds)
                .Where(commandId => !string.IsNullOrWhiteSpace(commandId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            frame.CumulativeAppliedCommandIds = (options.CumulativeAppliedCommandIds ?? Array.Empty<string>())
                .Concat(operationResult.AppliedCommandIds)
                .Where(commandId => !string.IsNullOrWhiteSpace(commandId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            frame.CumulativeRejectedCommandIds = (options.CumulativeRejectedCommandIds ?? Array.Empty<string>())
                .Concat(operationResult.RejectedCommandIds)
                .Where(commandId => !string.IsNullOrWhiteSpace(commandId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            frame.Capabilities = new[]
            {
                "aetheria.daemon.operation_execute.v1",
                "aetheria.daemon.intent_state.v1",
                "aetheria.daemon.authoritative_frame.v1",
                AetheriaRuntimeDaemonSchemas.AssetManifest,
                AetheriaRuntimeDaemonSchemas.ProviderAdvertisement,
                AetheriaRuntimeDaemonSchemas.Health,
                AetheriaRuntimeDaemonSchemas.CommandBoundary,
                AetheriaRuntimeDaemonSchemas.StarbridgeSessionSummary,
                AetheriaRuntimeDaemonSchemas.GameSurface,
                AetheriaRuntimeDaemonSchemas.EditorSurface
            };

            if (!options.PublishWitnesses)
            {
                return new AetheriaRuntimeDaemonTickResult(
                    operationResult.Run,
                    operationResult,
                    frame,
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "");
            }

            var framePath = AetheriaRuntimeDaemonFrameStore.PublishFrame(stateFilePath, frame);
            AetheriaRuntimeDaemonSoaFramePublisher.PublishCurrentZoneEntities(stateFilePath, frame);
            var commandBoundary = AetheriaRuntimeDaemonCommandBoundaryDocument.Create(options.DaemonId);
            var commandBoundaryPath = AetheriaRuntimeDaemonPublicationStore.PublishCommandBoundary(
                stateFilePath,
                commandBoundary);
            var providerAdvertisement = AetheriaRuntimeDaemonProviderAdvertisementDocument.Create(
                stateFilePath,
                options.DaemonId,
                options.VerseId,
                options.CultMeshAddress);
            var providerAdvertisementPath = AetheriaRuntimeDaemonPublicationStore.PublishProviderAdvertisement(
                stateFilePath,
                providerAdvertisement);
            var health = new AetheriaRuntimeDaemonHealthDocument
            {
                DaemonId = string.IsNullOrWhiteSpace(options.DaemonId) ? "aetheria-daemon" : options.DaemonId,
                VerseId = string.IsNullOrWhiteSpace(options.VerseId) ? "aetheria.local" : options.VerseId,
                PublishedAtUtc = DateTime.UtcNow.ToString("O"),
                StatePath = stateFilePath,
                FrameId = frame.FrameId,
                ObservedCommandCount = observedCommands.Length,
                AppliedCommandCount = operationResult.AppliedCommandIds.Count,
                RejectedCommandCount = operationResult.RejectedCommandIds.Count,
                Status = operationResult.RejectedCommandIds.Count == 0 ? "healthy" : "commands-rejected",
                PublicationSource = "daemon-published",
                Transport = "cultcache-witness",
                CommandBoundaryPath = commandBoundaryPath
            };
            var healthPath = AetheriaRuntimeDaemonPublicationStore.PublishHealth(
                stateFilePath,
                health);
            AetheriaRuntimeDaemonPublicationStore.PublishAssetManifest(
                stateFilePath,
                AetheriaRuntimeAssets.ProjectManifest(
                    options.Catalog,
                    frame.Run?.RunId ?? "",
                    $"cultmesh://{options.VerseId}/assets"));
            var starbridgeSummary = AetheriaRuntimeStarbridgeProjection.ProjectSessionSummary(
                frame,
                options.StarbridgeScenario,
                options.StarbridgeSession,
                options.Catalog);
            AetheriaRuntimeDaemonPublicationStore.PublishStarbridgeSessionSummary(
                stateFilePath,
                starbridgeSummary);
            var gameSurface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
                frame,
                health,
                commandBoundary,
                starbridgeSummary);
            var gameSurfacePath = AetheriaRuntimeDaemonPublicationStore.PublishGameSurface(
                stateFilePath,
                gameSurface);
            var gameTuiSurfacePath = AetheriaRuntimeDaemonPublicationStore.PublishGameTuiSurface(
                stateFilePath,
                gameSurface);
            var designerSurfaces = new[]
            {
                AetheriaRuntimeCatalogStore.ProjectStatRecipeSurfaceDocument(stateFilePath),
                AetheriaRuntimeCatalogStore.ProjectTradeValuePolicySurfaceDocument(stateFilePath)
            };
            var editorSurface = AetheriaRuntimeDaemonEditorSurfaceBuilder.Build(
                providerAdvertisement,
                health,
                commandBoundary,
                designerSurfaces);
            var editorSurfacePath = AetheriaRuntimeDaemonPublicationStore.PublishEditorSurface(
                stateFilePath,
                editorSurface);
            var editorTuiSurfacePath = AetheriaRuntimeDaemonPublicationStore.PublishEditorTuiSurface(
                stateFilePath,
                editorSurface);
            return new AetheriaRuntimeDaemonTickResult(
                operationResult.Run,
                operationResult,
                frame,
                framePath,
                providerAdvertisementPath,
                healthPath,
                commandBoundaryPath,
                gameSurfacePath,
                gameTuiSurfacePath,
                editorSurfacePath,
                editorTuiSurfacePath,
                providerAdvertisement,
                health,
                commandBoundary,
                starbridgeSummary,
                gameSurface,
                gameSurface,
                editorSurface,
                editorSurface);
        }

        private static void StampZoneSimulationTime(
            AetheriaRuntimeRunCheckpointCommit run,
            double simulationTimeSeconds)
        {
            foreach (var zone in run?.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            {
                if (zone != null)
                    zone.SimulationTimeSeconds = simulationTimeSeconds;
            }
        }

    }
}

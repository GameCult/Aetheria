using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public AetheriaRuntimeDaemonRenderSettings RenderSettings { get; set; } =
            AetheriaRuntimeDaemonRenderSettings.AetheriaDefault;
        public AetheriaRuntimeDaemonSimulationSettings SimulationSettings { get; set; } =
            AetheriaRuntimeDaemonSimulationSettings.AetheriaDefault;
        public IAetheriaRuntimeWorldPhysics? WorldPhysics { get; set; }
        public bool AdvanceSimulation { get; set; } = true;
        public int SimulationStepCount { get; set; } = 1;
        public bool BuildPublications { get; set; } = true;
        public AetheriaRuntimeDaemonSoaFramePublisher? SoaFramePublisher { get; set; }
    }

    public sealed class AetheriaRuntimeDaemonTickResult
    {
        public AetheriaRuntimeDaemonTickResult(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeDaemonOperationResult operationResult,
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeDaemonSoaFrame? soaFrame = null,
            AetheriaRuntimeDaemonProviderAdvertisementDocument? providerAdvertisement = null,
            AetheriaRuntimeDaemonHealthDocument? health = null,
            AetheriaRuntimeDaemonCommandBoundaryDocument? commandBoundary = null,
            AetheriaRuntimeAssetManifestDocument? assetManifest = null,
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
            SoaFrame = soaFrame;
            ProviderAdvertisement = providerAdvertisement;
            Health = health;
            CommandBoundary = commandBoundary;
            AssetManifest = assetManifest;
            StarbridgeSessionSummary = starbridgeSessionSummary;
            GameSurface = gameSurface;
            GameTuiSurface = gameTuiSurface;
            EditorSurface = editorSurface;
            EditorTuiSurface = editorTuiSurface;
        }

        public AetheriaRuntimeRunCheckpointCommit Run { get; }
        public AetheriaRuntimeDaemonOperationResult OperationResult { get; }
        public AetheriaRuntimeDaemonFrameDocument Frame { get; }
        public AetheriaRuntimeDaemonSoaFrame? SoaFrame { get; }
        public AetheriaRuntimeDaemonSoaViewDocument? SoaView => SoaFrame?.View;
        public AetheriaRuntimeDaemonProviderAdvertisementDocument? ProviderAdvertisement { get; }
        public AetheriaRuntimeDaemonHealthDocument? Health { get; }
        public AetheriaRuntimeDaemonCommandBoundaryDocument? CommandBoundary { get; }
        public AetheriaRuntimeAssetManifestDocument? AssetManifest { get; }
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
            options.OperationContext.Catalog = options.Catalog;
            var trace = string.Equals(Environment.GetEnvironmentVariable("AETHERIA_TRACE_TICK_PHASES"), "1", StringComparison.Ordinal);
            var phase = Stopwatch.StartNew();
            void Trace(string name)
            {
                if (trace && phase.ElapsedMilliseconds >= 20)
                    Console.WriteLine($"Aetheria simulation phase {name} took {phase.ElapsedMilliseconds}ms.");
                phase.Restart();
            }
            EnsureEntityIds(run);
            EnsureBehaviorStates(run, options.Catalog);
            Trace("state-projection");

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
            Trace("command-execution");
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
            var simulationStepCount = options.AdvanceSimulation ? Math.Max(1, options.SimulationStepCount) : 0;
            for (var simulationStep = 0; simulationStep < simulationStepCount; simulationStep++)
            {
                var stepTime = options.SimulationTimeSeconds -
                    ((simulationStepCount - simulationStep - 1) * options.FixedDeltaSeconds);
                var agentCommands = AetheriaRuntimeAgentScheduler.AssignAndPlan(
                    operationResult.Run,
                    options.FrameId,
                    options.Catalog,
                    stepTime,
                    options.SimulationSettings);
                Trace("agent-planning");
                if (agentCommands.Count > 0)
                {
                    var agentResult = AetheriaRuntimeDaemonOperations.Execute(
                        operationResult.Run,
                        agentCommands,
                        options.OperationContext);
                    AetheriaRuntimeAgentScheduler.Reconcile(
                        agentResult.Run,
                        options.FrameId,
                        agentResult.AppliedCommandIds,
                        agentResult.RejectedCommandIds);
                    operationResult = new AetheriaRuntimeDaemonOperationResult(
                        agentResult.Run,
                        operationResult.AppliedCommandIds.Concat(agentResult.AppliedCommandIds).ToArray(),
                        operationResult.RejectedCommandIds.Concat(agentResult.RejectedCommandIds).ToArray(),
                        MergeIntents(operationResult.Intents, agentResult.Intents));
                }
                AetheriaRuntimeDaemonSimulation.Step(
                    operationResult.Run,
                    operationResult.Intents,
                    options.FixedDeltaSeconds,
                    options.SimulationSettings,
                    options.WorldPhysics ?? throw new InvalidOperationException("Ymir world physics owner is required."),
                    options.Catalog,
                    options.FrameId,
                    stepTime,
                    simulationStep);
                Trace("world-step");
                StampZoneSimulationTime(operationResult.Run, stepTime);
            }

            var frame = AetheriaRuntimeDaemonFrameDocument.Create(
                operationResult.Run,
                options.DaemonId,
                options.SessionId,
                options.FrameId,
                options.SimulationTimeSeconds,
                options.FixedDeltaSeconds,
                renderSettings: options.RenderSettings,
                simulationSettings: options.SimulationSettings);
            Trace("frame-projection");
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

            if (!options.BuildPublications)
            {
                return new AetheriaRuntimeDaemonTickResult(
                    operationResult.Run,
                    operationResult,
                    frame);
            }

            return BuildPublications(stateFilePath, operationResult, frame, options, observedCommands.Length);
        }

        public static AetheriaRuntimeDaemonTickResult BuildPublications(
            string stateFilePath,
            AetheriaRuntimeDaemonOperationResult operationResult,
            AetheriaRuntimeDaemonFrameDocument frame,
            AetheriaRuntimeDaemonTickOptions options,
            int observedCommandCount)
        {
            var soaPublisher = options.SoaFramePublisher ??
                throw new InvalidOperationException("The daemon-lifetime Aetheria SoA publisher is required.");
            var catalog = options.Catalog ?? new AetheriaRuntimeCatalogSnapshot(
                Array.Empty<AetheriaRuntimeCatalogItem>(),
                Array.Empty<AetheriaRuntimeCorporation>(),
                Array.Empty<AetheriaRuntimeNameFile>());
            var soaFrame = soaPublisher.BuildCurrentZoneEntities(frame, catalog);
            var commandBoundary = AetheriaRuntimeDaemonCommandBoundaryDocument.Create(options.DaemonId);
            var providerAdvertisement = AetheriaRuntimeDaemonProviderAdvertisementDocument.Create(
                stateFilePath,
                options.DaemonId,
                options.VerseId,
                options.CultMeshAddress);
            var health = new AetheriaRuntimeDaemonHealthDocument
            {
                DaemonId = string.IsNullOrWhiteSpace(options.DaemonId) ? "aetheria-daemon" : options.DaemonId,
                VerseId = string.IsNullOrWhiteSpace(options.VerseId) ? "aetheria.local" : options.VerseId,
                PublishedAtUtc = DateTime.UtcNow.ToString("O"),
                StatePath = stateFilePath,
                FrameId = frame.FrameId,
                ObservedCommandCount = observedCommandCount,
                AppliedCommandCount = operationResult.AppliedCommandIds.Count,
                RejectedCommandCount = operationResult.RejectedCommandIds.Count,
                Status = operationResult.RejectedCommandIds.Count == 0 ? "healthy" : "commands-rejected",
                PublicationSource = "daemon-published",
                Transport = "cultmesh-managed",
                CommandBoundaryPath = AetheriaRuntimeVerseRecordKeys.DaemonCommandBoundary.ToString()
            };
            var assetManifest = AetheriaRuntimeAssets.ProjectManifest(
                catalog,
                frame.Run?.RunId ?? "",
                "cultmesh://aetheria.local/assets");
            var starbridgeSummary = AetheriaRuntimeStarbridgeDocuments.SessionSummary(
                frame,
                options.StarbridgeScenario,
                options.StarbridgeSession,
                catalog);
            var gameSurface = AetheriaRuntimeDaemonGameSurfaceBuilder.Build(
                frame,
                health,
                commandBoundary,
                catalog: catalog);
            var designerSurfaces = new[]
            {
                AetheriaRuntimeStatRecipeSurfaceBuilder.BuildFromCatalog(catalog),
                AetheriaRuntimeTradeValuePolicySurfaceBuilder.BuildFromCatalog(catalog)
            };
            var editorSurface = AetheriaRuntimeDaemonEditorSurfaceBuilder.Build(
                providerAdvertisement,
                health,
                commandBoundary,
                designerSurfaces);
            return new AetheriaRuntimeDaemonTickResult(
                operationResult.Run,
                operationResult,
                frame,
                soaFrame,
                providerAdvertisement,
                health,
                commandBoundary,
                assetManifest,
                starbridgeSummary,
                gameSurface,
                gameSurface,
                editorSurface,
                editorSurface);
        }

        private static AetheriaRuntimeDaemonIntentState MergeIntents(
            AetheriaRuntimeDaemonIntentState commands,
            AetheriaRuntimeDaemonIntentState agents)
        {
            var merged = new AetheriaRuntimeDaemonIntentState
            {
                SensorPingRequested = commands.SensorPingRequested || agents.SensorPingRequested
            };
            merged.Movements.AddRange(commands.Movements);
            merged.Movements.AddRange(agents.Movements);
            merged.WeaponGroups.AddRange(commands.WeaponGroups);
            merged.WeaponGroups.AddRange(agents.WeaponGroups);
            merged.Behaviors.AddRange(commands.Behaviors);
            merged.Behaviors.AddRange(agents.Behaviors);
            merged.Consumables.AddRange(commands.Consumables);
            merged.Consumables.AddRange(agents.Consumables);
            merged.Docking.AddRange(commands.Docking);
            merged.Docking.AddRange(agents.Docking);
            merged.Wormholes.AddRange(commands.Wormholes);
            merged.Wormholes.AddRange(agents.Wormholes);
            return merged;
        }

        private static void EnsureEntityIds(AetheriaRuntimeRunCheckpointCommit run)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var zone in run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
            foreach (var entity in zone?.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>())
            {
                if (entity == null)
                    continue;
                if (string.IsNullOrWhiteSpace(entity.EntityId))
                    entity.EntityId = $"aetheria.entity:{(string.IsNullOrWhiteSpace(run.RunId) ? "local" : run.RunId)}:{zone.ZoneIndex}:{entity.EntityIndex}";
                if (!used.Add(entity.EntityId))
                    throw new InvalidOperationException($"Duplicate runtime entity identity '{entity.EntityId}'.");
            }
        }

        private static void EnsureBehaviorStates(
            AetheriaRuntimeRunCheckpointCommit run,
            AetheriaRuntimeCatalogSnapshot? catalog)
        {
            if (catalog == null)
                return;
            foreach (var entity in (run.Zones ?? Array.Empty<AetheriaRuntimeZoneSnapshotCommit>())
                .Where(zone => zone != null)
                .SelectMany(zone => zone.Entities ?? Array.Empty<AetheriaRuntimeEntitySnapshotCommit>()))
            {
                AetheriaRuntimeBehaviorStateProjector.EnsureEquipmentBehaviorStates(entity, catalog);
            }
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

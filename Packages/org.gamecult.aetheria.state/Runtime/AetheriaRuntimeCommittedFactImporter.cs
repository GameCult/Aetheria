using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Verse
{
    public sealed class AetheriaRuntimeCommittedFactImportResult
    {
        public AetheriaRuntimeCommittedFactImportResult(
            AetheriaRuntimeDaemonTickResult tick,
            IReadOnlyList<string> acceptedFactIds,
            IReadOnlyList<string> rejectedFactIds,
            IReadOnlyList<string> duplicateFactIds)
        {
            Tick = tick ?? throw new ArgumentNullException(nameof(tick));
            AcceptedFactIds = acceptedFactIds ?? Array.Empty<string>();
            RejectedFactIds = rejectedFactIds ?? Array.Empty<string>();
            DuplicateFactIds = duplicateFactIds ?? Array.Empty<string>();
        }

        public AetheriaRuntimeDaemonTickResult Tick { get; }
        public IReadOnlyList<string> AcceptedFactIds { get; }
        public IReadOnlyList<string> RejectedFactIds { get; }
        public IReadOnlyList<string> DuplicateFactIds { get; }
        public AetheriaRuntimeRunCheckpointCommit Run => Tick.Run;
        public AetheriaRuntimeDaemonFrameDocument Frame => Tick.Frame;
    }

    public static class AetheriaRuntimeCommittedFactImporter
    {
        public static AetheriaRuntimeCommittedFactImportResult ImportIntoFrame(
            string stateFilePath,
            AetheriaRuntimeDaemonFrameDocument? currentFrame,
            IEnumerable<AetheriaRuntimeCommittedCommandFactDocument> facts,
            AetheriaRuntimeVerseAuthorityPolicyDocument? policy,
            IEnumerable<AetheriaRuntimeAuthorityLeaseDocument>? leases,
            string localRuntimeId,
            string daemonId,
            string sessionId,
            string verseId,
            IEnumerable<string>? importedFactIds = null,
            AetheriaRuntimeCatalogSnapshot? catalog = null)
        {
            currentFrame ??= new AetheriaRuntimeDaemonFrameDocument
            {
                Run = new AetheriaRuntimeRunCheckpointCommit(),
                FixedDeltaSeconds = 0.02
            };

            var alreadyImported = new HashSet<string>(
                importedFactIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            var acceptedFacts = new List<string>();
            var rejectedFacts = new List<string>();
            var duplicateFacts = new List<string>();
            var commands = new List<AetheriaRuntimeDaemonCommandDocument>();
            var acceptedCommands = new List<AcceptedCommandFact>();

            foreach (var fact in facts ?? Enumerable.Empty<AetheriaRuntimeCommittedCommandFactDocument>())
            {
                if (fact == null || string.IsNullOrWhiteSpace(fact.FactId))
                    continue;

                if (!alreadyImported.Add(fact.FactId))
                {
                    duplicateFacts.Add(fact.FactId);
                    continue;
                }

                if (!string.Equals(fact.Outcome, AetheriaRuntimeCommandFactOutcomes.Applied, StringComparison.Ordinal))
                {
                    rejectedFacts.Add(fact.FactId);
                    continue;
                }

                var command = NormalizeCommand(fact);
                var decision = AetheriaRuntimeAuthorityRouter.Authorize(
                    command,
                    policy,
                    leases,
                    localRuntimeId);
                if (!decision.Authorized)
                {
                    rejectedFacts.Add(fact.FactId);
                    continue;
                }

                acceptedFacts.Add(fact.FactId);
                commands.Add(command);
                acceptedCommands.Add(new AcceptedCommandFact(fact.FactId, command.CommandId));
            }

            var fixedDeltaSeconds = currentFrame.FixedDeltaSeconds > 0
                ? currentFrame.FixedDeltaSeconds
                : 0.02;
            var frameId = currentFrame.FrameId + 1;
            var simulationTimeSeconds = currentFrame.SimulationTimeSeconds + fixedDeltaSeconds;
            var tick = AetheriaRuntimeDaemonTickRunner.Tick(
                stateFilePath,
                currentFrame.Run ?? new AetheriaRuntimeRunCheckpointCommit(),
                new AetheriaRuntimeDaemonTickOptions
                {
                    DaemonId = string.IsNullOrWhiteSpace(daemonId) ? localRuntimeId : daemonId,
                    SessionId = string.IsNullOrWhiteSpace(sessionId) ? currentFrame.SessionId : sessionId,
                    VerseId = string.IsNullOrWhiteSpace(verseId) ? "aetheria.local" : verseId,
                    FrameId = frameId,
                    SimulationTimeSeconds = simulationTimeSeconds,
                    FixedDeltaSeconds = fixedDeltaSeconds,
                    ObservedCommands = commands,
                    AccountedCommandIds = currentFrame.AccountedCommandIds,
                    CumulativeAppliedCommandIds = currentFrame.CumulativeAppliedCommandIds,
                    CumulativeRejectedCommandIds = currentFrame.CumulativeRejectedCommandIds,
                    Catalog = catalog ?? EmptyCatalog(),
                    BuildPublications = false
                });

            var operationRejectedCommandIds = new HashSet<string>(
                tick.OperationResult.RejectedCommandIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            if (operationRejectedCommandIds.Count > 0)
            {
                acceptedFacts.Clear();
                foreach (var item in acceptedCommands)
                {
                    if (!string.IsNullOrWhiteSpace(item.CommandId) && operationRejectedCommandIds.Contains(item.CommandId))
                        rejectedFacts.Add(item.FactId);
                    else
                        acceptedFacts.Add(item.FactId);
                }
            }

            return new AetheriaRuntimeCommittedFactImportResult(
                tick,
                acceptedFacts.Distinct(StringComparer.Ordinal).ToArray(),
                rejectedFacts.Distinct(StringComparer.Ordinal).ToArray(),
                duplicateFacts.Distinct(StringComparer.Ordinal).ToArray());
        }

        private static AetheriaRuntimeDaemonCommandDocument NormalizeCommand(
            AetheriaRuntimeCommittedCommandFactDocument fact)
        {
            var command = fact.Command ?? new AetheriaRuntimeDaemonCommandDocument();
            command.Schema = AetheriaRuntimeDaemonSchemas.Command;
            if (string.IsNullOrWhiteSpace(command.CommandId))
                command.CommandId = fact.CommandId;
            if (string.IsNullOrWhiteSpace(command.AuthorRuntimeId))
                command.AuthorRuntimeId = fact.SourceRuntimeId;
            if (string.IsNullOrWhiteSpace(command.ClientId))
                command.ClientId = fact.SourceRuntimeId;
            if (string.IsNullOrWhiteSpace(command.SubjectKey))
                command.SubjectKey = fact.SubjectKey;
            if (string.IsNullOrWhiteSpace(command.ClaimKind))
                command.ClaimKind = fact.ClaimKind;

            return command;
        }

        private static AetheriaRuntimeCatalogSnapshot EmptyCatalog()
        {
            return new AetheriaRuntimeCatalogSnapshot(
                Array.Empty<AetheriaRuntimeCatalogItem>(),
                Array.Empty<AetheriaRuntimeCorporation>(),
                Array.Empty<AetheriaRuntimeNameFile>());
        }

        private sealed class AcceptedCommandFact
        {
            public AcceptedCommandFact(string factId, string commandId)
            {
                FactId = factId ?? "";
                CommandId = commandId ?? "";
            }

            public string FactId { get; }
            public string CommandId { get; }
        }
    }
}

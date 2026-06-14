using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#nullable enable

namespace GameCult.Aetheria.State.Unity
{
    public static class AetheriaRuntimeStateCommitLog
    {
        public const string CommitSchema = AetheriaRuntimeStateCommitDocument.SchemaId;

        public static string GetPendingDirectory(string stateFilePath)
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new ArgumentException("State file path must be non-empty.", nameof(stateFilePath));

            return stateFilePath + ".pending";
        }

        public static AetheriaRuntimeCommitEnvelope QueuePlayerSettings(
            string stateFilePath,
            AetheriaRuntimePlayerSettingsCommit settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return WriteCommit(stateFilePath, AetheriaRuntimeCommitKind.PlayerSettings, document => document.PlayerSettings = settings);
        }

        public static AetheriaRuntimeCommitEnvelope QueueLoadoutTemplate(
            string stateFilePath,
            AetheriaRuntimeLoadoutTemplateCommit loadout)
        {
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            return WriteCommit(stateFilePath, AetheriaRuntimeCommitKind.LoadoutTemplate, document => document.LoadoutTemplate = loadout);
        }

        public static AetheriaRuntimeCommitEnvelope QueueRunCheckpoint(
            string stateFilePath,
            AetheriaRuntimeRunCheckpointCommit run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            return WriteCommit(stateFilePath, AetheriaRuntimeCommitKind.RunCheckpoint, document => document.RunCheckpoint = run);
        }

        public static IReadOnlyList<AetheriaRuntimeCommitEnvelope> ReadPending(string stateFilePath)
        {
            var pendingDirectory = GetPendingDirectory(stateFilePath);
            if (!Directory.Exists(pendingDirectory))
                return Array.Empty<AetheriaRuntimeCommitEnvelope>();

            return Directory
                .EnumerateFiles(pendingDirectory, "*.cc")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(ReadEnvelope)
                .ToArray();
        }

        public static AetheriaRuntimeStateCommitDocument ReadDocument(string path)
        {
            return AetheriaRuntimePendingCultCacheStore.ReadStateCommit(path);
        }

        private static AetheriaRuntimeCommitEnvelope WriteCommit(
            string stateFilePath,
            AetheriaRuntimeCommitKind kind,
            Action<AetheriaRuntimeStateCommitDocument> attachPayload)
        {
            var commandId = Guid.NewGuid().ToString("N");
            var createdAtUtc = DateTime.UtcNow.ToString("O");
            var pendingDirectory = GetPendingDirectory(stateFilePath);
            Directory.CreateDirectory(pendingDirectory);

            var finalPath = Path.Combine(
                pendingDirectory,
                $"{createdAtUtc.Replace(':', '-')}.{KindToken(kind)}.{commandId}.cc");
            var document = new AetheriaRuntimeStateCommitDocument
            {
                Schema = CommitSchema,
                Kind = KindToken(kind),
                CommandId = commandId,
                CreatedAtUtc = createdAtUtc
            };
            attachPayload(document);

            AetheriaRuntimePendingCultCacheStore.WriteStateCommit(finalPath, document);

            return new AetheriaRuntimeCommitEnvelope(CommitSchema, kind, commandId, createdAtUtc, finalPath);
        }

        private static AetheriaRuntimeCommitEnvelope ReadEnvelope(string path)
        {
            var document = ReadDocument(path);
            return new AetheriaRuntimeCommitEnvelope(
                document.Schema ?? "",
                ParseKind(document.Kind ?? ""),
                document.CommandId ?? "",
                document.CreatedAtUtc ?? "",
                path);
        }

        private static string KindToken(AetheriaRuntimeCommitKind kind)
        {
            return kind switch
            {
                AetheriaRuntimeCommitKind.PlayerSettings => "player_settings",
                AetheriaRuntimeCommitKind.LoadoutTemplate => "loadout_template",
                AetheriaRuntimeCommitKind.RunCheckpoint => "run_checkpoint",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }

        private static AetheriaRuntimeCommitKind ParseKind(string token)
        {
            return token switch
            {
                "player_settings" => AetheriaRuntimeCommitKind.PlayerSettings,
                "loadout_template" => AetheriaRuntimeCommitKind.LoadoutTemplate,
                "run_checkpoint" => AetheriaRuntimeCommitKind.RunCheckpoint,
                _ => throw new InvalidDataException($"Unknown Aetheria runtime commit kind '{token}'.")
            };
        }

    }
}

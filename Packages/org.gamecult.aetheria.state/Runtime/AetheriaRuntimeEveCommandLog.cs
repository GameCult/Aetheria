using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Eve.Surface;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Unity
{
    public static class AetheriaRuntimeEveCommandLog
    {
        public const string CommandSchema = AetheriaRuntimeEveCommandDocument.SchemaId;

        public static string GetPendingDirectory(string stateFilePath)
        {
            if (string.IsNullOrWhiteSpace(stateFilePath))
                throw new ArgumentException("State file path must be non-empty.", nameof(stateFilePath));

            return stateFilePath + ".eve.pending";
        }

        public static AetheriaRuntimeEveCommandEnvelope QueueCommand(
            string stateFilePath,
            EveSurfaceCommandRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var commandId = Guid.NewGuid().ToString("N");
            var issuedAtUtc = request.IssuedAt.UtcDateTime.ToString("O");
            var pendingDirectory = GetPendingDirectory(stateFilePath);
            Directory.CreateDirectory(pendingDirectory);

            var finalPath = Path.Combine(
                pendingDirectory,
                $"{issuedAtUtc.Replace(':', '-')}.{StableToken(request.SurfaceId)}.{StableToken(request.Command)}.{commandId}.cc");
            var tempPath = finalPath + ".tmp";

            var document = new AetheriaRuntimeEveCommandDocument
            {
                Schema = CommandSchema,
                CommandId = commandId,
                ProviderId = request.ProviderId ?? "",
                SurfaceId = request.SurfaceId ?? "",
                Command = request.Command ?? "",
                IssuedAtUtc = issuedAtUtc,
                ClientId = request.ClientId ?? "",
                Payload = CopyPayload(request.Payload)
            };

            File.WriteAllBytes(tempPath, MessagePackSerializer.Serialize(document));
            if (File.Exists(finalPath))
                File.Delete(finalPath);
            File.Move(tempPath, finalPath);

            return new AetheriaRuntimeEveCommandEnvelope(
                document.Schema,
                document.CommandId,
                document.ProviderId,
                document.SurfaceId,
                document.Command,
                document.IssuedAtUtc,
                document.ClientId,
                document.Payload,
                finalPath);
        }

        public static IReadOnlyList<AetheriaRuntimeEveCommandEnvelope> ReadPending(string stateFilePath)
        {
            var pendingDirectory = GetPendingDirectory(stateFilePath);
            if (!Directory.Exists(pendingDirectory))
                return Array.Empty<AetheriaRuntimeEveCommandEnvelope>();

            return Directory
                .EnumerateFiles(pendingDirectory, "*.cc")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(ReadEnvelope)
                .ToArray();
        }

        private static AetheriaRuntimeEveCommandEnvelope ReadEnvelope(string path)
        {
            var document = MessagePackSerializer.Deserialize<AetheriaRuntimeEveCommandDocument>(File.ReadAllBytes(path));

            return new AetheriaRuntimeEveCommandEnvelope(
                document.Schema ?? "",
                document.CommandId ?? "",
                document.ProviderId ?? "",
                document.SurfaceId ?? "",
                document.Command ?? "",
                document.IssuedAtUtc ?? "",
                document.ClientId ?? "",
                document.Payload ?? EmptyPayload(),
                path);
        }

        private static Dictionary<string, string> CopyPayload(IReadOnlyDictionary<string, string>? payload)
        {
            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in (payload ?? EmptyPayload()).OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(entry.Key))
                    copy[entry.Key] = entry.Value ?? "";
            }

            return copy;
        }

        private static Dictionary<string, string> EmptyPayload()
        {
            return new Dictionary<string, string>(0, StringComparer.Ordinal);
        }

        private static string StableToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "empty";

            var chars = value
                .Select(character => char.IsLetterOrDigit(character) ? character : '-')
                .ToArray();
            var token = new string(chars).Trim('-').ToLowerInvariant();
            while (token.Contains("--", StringComparison.Ordinal))
                token = token.Replace("--", "-", StringComparison.Ordinal);
            return string.IsNullOrWhiteSpace(token) ? "empty" : token;
        }

    }
}

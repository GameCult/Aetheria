using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCult.Eve.Surface;
using MessagePack;

#nullable enable

namespace GameCult.Aetheria.State.Unity
{
    public sealed class AetheriaRuntimeEveCommandEnvelope
    {
        public AetheriaRuntimeEveCommandEnvelope(
            string schema,
            string commandId,
            string providerId,
            string surfaceId,
            string command,
            string issuedAtUtc,
            string clientId,
            IReadOnlyDictionary<string, string> payload,
            string path)
        {
            Schema = schema;
            CommandId = commandId;
            ProviderId = providerId;
            SurfaceId = surfaceId;
            Command = command;
            IssuedAtUtc = issuedAtUtc;
            ClientId = clientId;
            Payload = payload;
            Path = path;
        }

        public string Schema { get; }
        public string CommandId { get; }
        public string ProviderId { get; }
        public string SurfaceId { get; }
        public string Command { get; }
        public string IssuedAtUtc { get; }
        public string ClientId { get; }
        public IReadOnlyDictionary<string, string> Payload { get; }
        public string Path { get; }
    }

    public static class AetheriaRuntimeEveCommandLog
    {
        public const string CommandSchema = "gamecult.eve.command.v1";

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

            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteArrayHeader(8);
            writer.Write(CommandSchema);
            writer.Write(commandId);
            writer.Write(request.ProviderId ?? "");
            writer.Write(request.SurfaceId ?? "");
            writer.Write(request.Command ?? "");
            writer.Write(issuedAtUtc);
            writer.Write(request.ClientId ?? "");
            WritePayload(ref writer, request.Payload);
            writer.Flush();

            File.WriteAllBytes(tempPath, buffer.WrittenSpan.ToArray());
            if (File.Exists(finalPath))
                File.Delete(finalPath);
            File.Move(tempPath, finalPath);

            return new AetheriaRuntimeEveCommandEnvelope(
                CommandSchema,
                commandId,
                request.ProviderId ?? "",
                request.SurfaceId ?? "",
                request.Command ?? "",
                issuedAtUtc,
                request.ClientId ?? "",
                new Dictionary<string, string>(request.Payload, StringComparer.Ordinal),
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
            var reader = new MessagePackReader(File.ReadAllBytes(path));
            var fields = reader.ReadArrayHeader();
            var schema = fields > 0 ? ReadString(ref reader) : "";
            var commandId = fields > 1 ? ReadString(ref reader) : "";
            var providerId = fields > 2 ? ReadString(ref reader) : "";
            var surfaceId = fields > 3 ? ReadString(ref reader) : "";
            var command = fields > 4 ? ReadString(ref reader) : "";
            var issuedAtUtc = fields > 5 ? ReadString(ref reader) : "";
            var clientId = fields > 6 ? ReadString(ref reader) : "";
            var payload = fields > 7 ? ReadPayload(ref reader) : EmptyPayload();
            for (var field = 8; field < fields; field++)
                reader.Skip();

            return new AetheriaRuntimeEveCommandEnvelope(
                schema,
                commandId,
                providerId,
                surfaceId,
                command,
                issuedAtUtc,
                clientId,
                payload,
                path);
        }

        private static void WritePayload(ref MessagePackWriter writer, IReadOnlyDictionary<string, string>? payload)
        {
            payload ??= EmptyPayload();
            writer.WriteMapHeader(payload.Count);
            foreach (var entry in payload.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                writer.Write(entry.Key ?? "");
                writer.Write(entry.Value ?? "");
            }
        }

        private static IReadOnlyDictionary<string, string> ReadPayload(ref MessagePackReader reader)
        {
            var count = reader.ReadMapHeader();
            if (count == 0)
                return EmptyPayload();

            var payload = new Dictionary<string, string>(count, StringComparer.Ordinal);
            for (var index = 0; index < count; index++)
            {
                var key = ReadString(ref reader);
                var value = ReadString(ref reader);
                if (!string.IsNullOrWhiteSpace(key))
                    payload[key] = value;
            }

            return payload;
        }

        private static IReadOnlyDictionary<string, string> EmptyPayload()
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

        private static string ReadString(ref MessagePackReader reader)
        {
            return reader.ReadString() ?? "";
        }
    }
}
